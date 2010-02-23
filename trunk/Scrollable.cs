using System;
using System.Collections.Generic;

namespace TextReader {

public interface IScrollable<T> {
    T Current { get; }
    bool IsLast { get; }
    bool IsFirst { get; }
    void ToNext();
    void ToPrev();
    Position Position { get; set; }
}

public interface Position {
}

public class CachingScrollable<T> : IScrollable<T> {
    private int cacheSize;
    private IScrollable<T> underlying;
    private LinkedList<CacheItem> cache;
    private LinkedListNode<CacheItem> currentNode;
    private bool atLeft;
    private long generation;
    public CachingScrollable(IScrollable<T> underlying, int cacheSize) {
        this.cacheSize = cacheSize;
        this.underlying = underlying;
        this.cache = new LinkedList<CacheItem>();
        initCurrent();
    }
    public T Current { 
        get {
            initCurrent();
            return currentNode.Value.value; 
        } 
    }
    public bool IsLast { 
        get { 
            initCurrent();
            toRight(); 
            return currentNode.Next == null && underlying.IsLast; 
        } 
    }
    public bool IsFirst { 
        get {
            initCurrent();
            toLeft();
            return currentNode.Previous == null && underlying.IsFirst; 
        } 
    }
    public void ToNext() {
        initCurrent();
        if (currentNode.Next == null) {
            toRight();
            if (underlying.IsLast) {
                throw new ArgumentException("Already at bottom");
            } else {
                underlying.ToNext();
                cache.AddLast(new CacheItem { 
                    pos = underlying.Position, 
                    value = underlying.Current, 
                    index = currentNode.Value.index + 1 
                });
                if (cache.Count > cacheSize) {
                    cache.RemoveFirst();
                }
            }
        }
        currentNode = currentNode.Next;
    }
    public void ToPrev() {
        initCurrent();
        if (currentNode.Previous == null) {
            toLeft();
            if (underlying.IsFirst) {
                throw new ArgumentException("Already at top");
            } else {
                underlying.ToPrev();
                cache.AddFirst(new CacheItem { 
                    pos = underlying.Position, 
                    value = underlying.Current,
                    index = currentNode.Value.index - 1
                });
                if (cache.Count > cacheSize) {
                    cache.RemoveLast();
                }
            }
        }
        currentNode = currentNode.Previous;
    }
    public Position Position { 
        get {
            if (currentNode != null) {
                return new Pos() { pos = currentNode.Value.pos, generation = generation, index = currentNode.Value.index };
            } else {
                return new Pos() { pos = underlying.Position, generation = Environment.TickCount };
            }
        }
        set {
            if (value is Pos) {
                Pos p = (Pos) value;
                if (p.generation == this.generation && cache.Count > 0 
                    && cache.First.Value.index <= p.index && cache.Last.Value.index >= p.index) {
                    int diff = p.index - currentNode.Value.index;
                    while (diff > 0) {
                        ToNext();
                        diff--;
                    }
                    while (diff < 0) {
                        ToPrev();
                        diff++;
                    }
                } else {
                    cache.Clear();
                    currentNode = null;
                    underlying.Position = p.pos;
                }
            } else {
                cache.Clear();
                currentNode = null;
                underlying.Position = value;
            }
        }
    }

    protected void Invalidate(bool contentChanged) {
        if (!contentChanged && currentNode != null) {
            underlying.Position = currentNode.Value.pos;
        }
        cache.Clear();
        currentNode = null;
    }

    protected void initCurrent() {
        if (currentNode == null) {
            if (cache.Count != 0) {
                throw new ArgumentException("currenNode is null in non-empty cache"); // a bug, that is
            }
            generation = Environment.TickCount;
            cache.AddLast(new CacheItem { pos = underlying.Position, value = underlying.Current, index = 0 });
            currentNode = cache.First;
            atLeft = false;
        }
    }

    private void toRight() {
        if (atLeft) {
            underlying.Position = cache.Last.Value.pos;
            atLeft = false;
        }
    }
    private void toLeft() {
        if (!atLeft) {
            underlying.Position = cache.First.Value.pos;
            atLeft = true;
        }
    }
    private class CacheItem {
        public Position pos;
        public T value;
        public int index;
    }
    private class Pos : Position {
        public Position pos;
        public int index;
        public long generation;
    }
}

public class ArrayScrollable<T> : IScrollable<T> {
    private T[] array;
    private int index;
    public ArrayScrollable(T[] array) {
        if (array.Length == 0) {
            throw new ArgumentException("Empty array");
        }
        this.array = array;
        this.index = 0;
    }
    public T Current { get { return array[index]; } }
    public bool IsLast { get { return index == array.Length - 1; } }
    public bool IsFirst { get { return index == 0; } }
    public void ToNext() { index++; }
    public void ToPrev() { index--; }
    public Position Position { 
        get { return new Pos() { index = index }; }
        set {
            if (!(value is Pos)) {
                throw new ArgumentException("Unsupported position type. Expected: " + typeof(Pos) + ", found: " + value.GetType());
            }
            index = ((Pos) value).index; 
        }
    }
    private class Pos : Position {
        public int index;
    }
}

public class MappingScrollable<T, U> : IScrollable<T> {
    protected IScrollable<U> underlying;
    protected Func<U, T> map;
    public MappingScrollable(IScrollable<U> underlying, Func<U, T> map) {
        this.underlying = underlying;
        this.map = map;
    }
    public virtual T Current { get { return map(underlying.Current); } }
    public bool IsLast { get { return underlying.IsLast; } }
    public bool IsFirst { get { return underlying.IsFirst; } }
    public void ToNext() { underlying.ToNext(); }
    public void ToPrev() { underlying.ToPrev(); }
    public virtual Position Position {
        get { return underlying.Position; }
        set { underlying.Position = value; }
    }
}

public class CachedMappingScrollable<T, U> : MappingScrollable<T, U> {
    private U currentUndelying;
    private T current;
    private bool dirty;
    public CachedMappingScrollable(IScrollable<U> underlying, Func<U, T> map) : base(underlying, map) { }
    public override T Current { 
        get {
            if (dirty || !underlying.Current.Equals(currentUndelying)) {
                currentUndelying = underlying.Current;
                current = map(underlying.Current);
                dirty = false;
            }
            return current;
        } 
    }
    public void Invalidate() {
        dirty = true;
    }
}

public abstract class AggregatingScrollable<T, U> : IScrollable<T> {
    protected IScrollable<U> underlying;
    protected T current;
    protected bool isFirst;
    protected bool isLast;
    protected bool atLeft;
    protected Position beginPos;
    protected Position endPos;
    public AggregatingScrollable(IScrollable<U> underlying) {
        this.underlying = underlying;
    }
    public virtual T Current { get { return current; } }
    public virtual bool IsFirst { get { return isFirst; } }
    public virtual bool IsLast { get { return isLast; } }
    public virtual void ToNext() { 
        if (atLeft) {
            underlying.Position = endPos;
            atLeft = false;
        }
        skipForward();
        beginPos = underlying.Position;
        current = fetchForward(ref isLast);
        endPos = underlying.Position;
    }
    public virtual void ToPrev() { 
        if (!atLeft) {
            underlying.Position = beginPos;
            atLeft = true;
        }
        skipBackward();
        endPos = underlying.Position;
        current = fetchBackward(ref isFirst); 
        beginPos = underlying.Position;
    }
    public virtual Position Position {
        get {
            return beginPos;
        }
        set {
            underlying.Position = value;
            beginPos = value;
            isFirst = checkFirst();
            current = fetchForward(ref isLast);
            endPos = underlying.Position;
            atLeft = false;
        }
    }

    protected virtual void init() {
        this.isFirst = checkFirst();
        this.beginPos = underlying.Position;
        this.current = fetchForward(ref isLast);
        this.endPos = underlying.Position;
        this.atLeft = false;
    }
    protected abstract T fetchForward(ref bool isLast);
    protected abstract void skipForward();
    protected abstract T fetchBackward(ref bool isFirst);
    protected abstract void skipBackward();
    protected virtual bool checkFirst() {
        return underlying.IsFirst;
    }
}

public class SplittingScrollable<T, C> : IScrollable<T> where C : IList<T> {
    private IScrollable<C> underlying;
    private int index;
    private int underlyingIndex;
    public SplittingScrollable(IScrollable<C> underlying) {
        this.underlying = underlying;
        this.index = 0;
        this.underlyingIndex = 0;
    }
    public T Current { get { return underlying.Current[index]; } }
    public bool IsFirst { get { return index == 0 && underlying.IsFirst; } }
    public bool IsLast { get { return index == underlying.Current.Count - 1 && underlying.IsLast; } }
    public void ToNext() {
        index++;
        if (index == underlying.Current.Count) {
            underlying.ToNext();
            underlyingIndex++;
            index = 0;
        }
    }
    public void ToPrev() {
        index--;
        if (index < 0) {
            underlying.ToPrev();
            underlyingIndex--;
            index = underlying.Current.Count - 1;
        }
    }
    public Position Position {
        get {
            return new Pos() { pos = underlying.Position, index = index, underlyingIndex = underlyingIndex };
        }
        set {
            if (value is Pos) {
                Pos p = (Pos) value;
                if (p.underlyingIndex == underlyingIndex) {
                    index = p.index;
                } else {
                    underlying.Position = p.pos;
                    index = p.index;
                    underlyingIndex = p.underlyingIndex;
                }
            } else {
                underlying.Position = value;
                index = 0;
                underlyingIndex = 0;
            }
        }
    }
    private class Pos : Position {
        public Position pos;
        public int index;
        public int underlyingIndex;
    }
}

public class TimedScrollable<T> : IScrollable<T> {
    long current;
    long last;
    long first;
    long next;
    long prev;
    long getpos;
    long setpos;
    IScrollable<T> underlying;
    public TimedScrollable(IScrollable<T> underlying) {
        this.underlying = underlying;
    }
    public T Current { 
        get {
            long t = Environment.TickCount;
            T result = underlying.Current;
            current += Environment.TickCount - t;
            return result;
        } 
    }
    public bool IsLast {
        get {
            long t = Environment.TickCount;
            bool result = underlying.IsLast;
            last += Environment.TickCount - t;
            return result;
        } 
    }
    public bool IsFirst { 
        get {
            long t = Environment.TickCount;
            bool result = underlying.IsFirst;
            first += Environment.TickCount - t;
            return result;
        } 
    }
    public void ToNext() {
        long t = Environment.TickCount;
        underlying.ToNext();
        next += Environment.TickCount - t;
    }
    public void ToPrev() {
        long t = Environment.TickCount;
        underlying.ToPrev();
        prev += Environment.TickCount - t;
    }
    public Position Position {
        get {
            long t = Environment.TickCount;
            Position result = underlying.Position;
            getpos += Environment.TickCount - t;
            return result;
        } 
        set {
            long t = Environment.TickCount;
            underlying.Position = value;
            setpos += Environment.TickCount - t;
        } 
    }
    public override string ToString() {
        return "c:" + current + " f:" + first + " l:" + last + " n:" + next + " p:" + prev + " gp:" + getpos + " sp:" + setpos;
    }
}

public class DebugTrackingScrollable<T> : IScrollable<T> {
    IScrollable<T> underlying;
    public DebugTrackingScrollable(IScrollable<T> underlying) {
        this.underlying = underlying;
    }
    public T Current { 
        get {
            T result = underlying.Current;
            Console.WriteLine("Current: " + result);
            return result;
        } 
    }
    public bool IsLast {
        get {
            bool result = underlying.IsLast;
            Console.WriteLine("IsLast: " + result);
            return result;
        } 
    }
    public bool IsFirst { 
        get {
            bool result = underlying.IsFirst;
            Console.WriteLine("IsFirst: " + result);
            return result;
        } 
    }
    public void ToNext() {
        Console.WriteLine("ToNext");
        underlying.ToNext();
    }
    public void ToPrev() {
        Console.WriteLine("ToPrev");
        underlying.ToPrev();
    }
    public Position Position {
        get {
            Position result = underlying.Position;
            Console.WriteLine("Position: " + result);
            return result;
        } 
        set {
            Console.WriteLine("ToPosition: " + value);
            underlying.Position = value;
        } 
    }
}

}
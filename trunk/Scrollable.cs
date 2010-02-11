﻿using System;
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
            return new Pos { pos = currentNode.Value.pos, index = currentNode.Value.index };
        }
        set {
            Pos p = (Pos) value;
            if (cache.Count > 0 && cache.First.Value.index <= p.index && cache.Last.Value.index >= p.index) {
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
        set { index = ((Pos) value).index; }
    }
    private class Pos : Position {
        public int index;
    }
}

public class MappingScrollable<T, U> : IScrollable<T> {
    private IScrollable<U> underlying;
    private Func<U, T> map;
    public MappingScrollable(IScrollable<U> underlying, Func<U, T> map) {
        this.underlying = underlying;
        this.map = map;
    }
    public T Current { get { return map(underlying.Current); } }
    public bool IsLast { get { return underlying.IsLast; } }
    public bool IsFirst { get { return underlying.IsFirst; } }
    public void ToNext() { underlying.ToNext(); }
    public void ToPrev() { underlying.ToPrev(); }
    public Position Position {
        get { return underlying.Position; }
        set { underlying.Position = value; }
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

}
using System;
using System.Collections.Generic;
using System.IO;

namespace TextReader.Parsing {

public enum TokenType { Word, Begin, End }

public struct Token {
    public static readonly Token Empty = new Token(null, TokenType.Word);
    public string Text { get; private set; }
    public TokenType Type { get; private set; }
    public Token(string text, TokenType type) : this() {
        this.Text = text;
        this.Type = type;
    }
    public bool Equals(string text, TokenType type) {
        return this.Text == text && this.Type == type;
    }
    public override bool Equals(object o) {
        if (!(o is Token)) {
            return false;
        }
        Token that = (Token) o;
        return this.Text == that.Text && this.Type == that.Type;
    }
    public override int GetHashCode() {
        return Text.GetHashCode() * 29 + Type.GetHashCode();
    }
    public override string ToString() {
        return (Type.Equals(TokenType.End) ? "/" : "") + Text;
    }
    public const string DOCUMENT = "doc";
    public const string PARAGRAPH = "p";
    public static bool operator == (Token t1, Token t2) {
        return t1.Equals(t2);
    }
    public static bool operator != (Token t1, Token t2) {
        return !t1.Equals(t2);
    }
}

public interface ISeekable<T> {
    T Position { get; set; }
}

class SeekableStreamReader : StreamReader, ISeekable<long> {
    private long streamPos;

    public SeekableStreamReader(Stream stream, bool detectEncodingFromByteOrderMarks) : base(stream, detectEncodingFromByteOrderMarks) { 
        if (!stream.CanSeek) {
            throw new ArgumentException("Stream is not seekable");
        }
        stream.Seek(0, SeekOrigin.Begin);
    }
    public long Position {
        get {
            return streamPos;
        }
        set {
            streamPos = value;
            BaseStream.Seek(streamPos, SeekOrigin.Begin);
            DiscardBufferedData();
        }
    }
    public override int Read() {
        int c = base.Read();
        if (c >= 0) {
            streamPos += CurrentEncoding.GetByteCount(new char[] { (char)c });
        }
        return c;
    }
    public override int Read(char[] buffer, int index, int count) {
        if (index + count > buffer.Length) {
            throw new ArgumentException("index + count >= buffer.Length");
        }
        for (int i = 0; i < count; i++) {
            int c = Read();
            if (c == -1) {
                return i;
            }
            buffer[index + i] = (char) c;
        }
        return count;
    }
    public override string ReadLine() {
        int c;
        string s = "";
        while (true) {
            c = Read();
            if ((char) c == '\r') {
                c = Peek();
                if ((char) c == '\n') {
                    c = Read();
                }
                return s;
            }
            if ((char) c == '\n') {
                return s;
            }
            if (c == -1) {
                if (s.Length > 0) {
                    return s;
                } else {
                    return null;
                }
            }
            s += (char) c;
        }
    }
}

class BufferedCharProvider : IScrollable<char> {
    private const int blockSize = 4096;
    private const int numBlocks = 64;
    private SeekableStreamReader reader;
    private Dictionary<long, char[]> blocks = new Dictionary<long, char[]>(numBlocks);
    private Dictionary<long, long> blockPos = new Dictionary<long, long>();
    private LinkedList<long> lastBlocks = new LinkedList<long>();
    private long charPos;
    private long lastCharPos = -1;
    private int current;

    public BufferedCharProvider(SeekableStreamReader reader) {
        this.reader = reader;
        current = getCurrent();
    }
    public Position Position {
        get {
            Pos p = new Pos();
            long blockNo = charPos / blockSize;
            if (blockPos.ContainsKey(blockNo)) {
                p.knownBlockNo = blockNo;
                p.knownBlockPos = blockPos[blockNo];
            } else {
                long prevNo = findPrevKnown(blockNo);
                p.knownBlockNo = prevNo;
                p.knownBlockPos = blockPos[prevNo];
            }
            p.charPos = charPos;
            return p;
        }
        set {
            if (!(value is Pos)) {
                throw new ArgumentException("Unsupported position type. Expected: " + typeof(Pos) + ", found: " + value.GetType());
            }
            Pos p = (Pos) value;
            this.charPos = p.charPos;
            if (!this.blockPos.ContainsKey(p.knownBlockNo)) {
                this.blockPos.Add(p.knownBlockNo, p.knownBlockPos);
            }
            current = getCurrent();
        }
    }
    private class Pos : Position {
        public long charPos;
        public long knownBlockNo;
        public long knownBlockPos;
    }
    private char[] readBlock(long blockNo) {
        reader.Position = blockPos[blockNo];
        reader.DiscardBufferedData();
        char[] block = new char[blockSize];
        int c = reader.Read(block, 0, blockSize);
        if (c < block.Length) {
            Array.Resize(ref block, c);
            lastCharPos = blockNo * blockSize + c - 1;
        } else if (!blockPos.ContainsKey(blockNo + 1)) {
            blockPos.Add(blockNo + 1, reader.Position);
        }
        return block;
    }
    private long findPrevKnown(long blockNo) {
        long prevNo = -1;
        foreach (long bn in blockPos.Keys) {
            if (prevNo < bn && bn < blockNo) {
                prevNo = bn;
            }
        }
        if (prevNo == -1) {
            prevNo = 0;
            blockPos[prevNo] = 0;
        }
        return prevNo;
    }
    private int getCurrent() {
        long blockNo = charPos / blockSize;
        if (!blockPos.ContainsKey(blockNo)) {
            long prevNo = findPrevKnown(blockNo);
            for (long n = prevNo; n < blockNo; n++) {
                char[] b = readBlock(n);
                if (b.Length < blockSize) {
                    charPos = lastCharPos;
                    blockNo = charPos / blockSize;
                    break;
                }
            }
        }
        if (!blocks.ContainsKey(blockNo)) {
            blocks.Add(blockNo, readBlock(blockNo));
        }
        long pos = charPos % blockSize;
        char[] block = blocks[blockNo];
        if (pos >= block.Length) {
            return -1;
        } else {
            return block[pos];
        }
    }
    public char Current { get { return (char)current; } }
    public bool IsFirst { get { return charPos == 0; } }
    public bool IsLast { get { return lastCharPos >= 0 && charPos == lastCharPos; } }
    public void ToNext() {
        if (IsLast) {
            throw new ArgumentException("Already at bottom");
        }
        charPos++;
        current = getCurrent();
    }
    public void ToPrev() {
        if (IsFirst) {
            throw new ArgumentException("Already at top");
        }
        charPos--;
        current = getCurrent();
    }
}

public class LineOfTextTokenizer : AggregatingScrollable<string, char> {
    public LineOfTextTokenizer(IScrollable<char> underlying) : base(underlying) {
        init();
    }
    protected override string fetchForward(ref bool isLast) {
        string line = "";
        while (underlying.Current != '\r' && underlying.Current != '\n') {
            line += underlying.Current;
            if (underlying.IsLast) {
                isLast = true;
                return line;
            } else {
                underlying.ToNext();
            }
        }
        Position pos = underlying.Position;
        skipForward();
        isLast = underlying.IsLast;
        underlying.Position = pos;
        return line;
    }
    protected override void skipForward() { 
        bool isCR = underlying.Current == '\r';
        if (!underlying.IsLast) {
            underlying.ToNext();
            if (isCR && underlying.Current == '\n' && !underlying.IsLast) {
                underlying.ToNext();
            }
        }
    }
    protected override string fetchBackward(ref bool isFirst) {
        string line = "";
        while (underlying.Current != '\r' && underlying.Current != '\n') {
            line = underlying.Current + line;
            if (underlying.IsFirst) {
                isFirst = true;
                return line;
            } else {
                underlying.ToPrev();
            }
        }
        underlying.ToNext();
        isFirst = false;
        return line;
    }
    protected override void skipBackward() {
        underlying.ToPrev();
        bool isLF = underlying.Current == '\n';
        if (!underlying.IsFirst) {
            underlying.ToPrev();
            if (isLF && underlying.Current == '\r' && !underlying.IsFirst) {
                underlying.ToPrev();
            }
        }
    }
}

}
using System;
using System.IO;
using System.Collections.Generic;

namespace TextReader.Parsing {

public enum TokenType { Word, Begin, End }

public struct Token {
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
    public static readonly Token DOCUMENT_BEGIN = new Token(DOCUMENT, TokenType.Begin);
    public static readonly Token DOCUMENT_END = new Token(DOCUMENT, TokenType.End);
    public static readonly Token PARAGRAPH_BEGIN = new Token(PARAGRAPH, TokenType.Begin);
    public static readonly Token PARAGRAPH_END = new Token(PARAGRAPH, TokenType.End);
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
            throw new ArgumentException("stream is not seekable");
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

public class WhiteSpaceTokenizer : IScrollable<string>, IDisposable {
    private SeekableStreamReader reader;
    private IScrollable<char> charProvider;
    private Position beginWord;
    private Position endWord;
    private string current;
    private bool isLast;
    private bool isFirst;

    public WhiteSpaceTokenizer(Stream s) {
        this.reader = new SeekableStreamReader(s, true);
        this.charProvider = new BufferedCharProvider(this.reader);
        current = fetchWord(skipForward, "\r\n", ref beginWord, ref endWord);
        isFirst = true;
    }
    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    protected virtual void Dispose(bool disposing) {
        if (disposing) {
            if (reader != null) {
                reader.Dispose();
                reader = null;
            }
            if (charProvider != null && charProvider is IDisposable) {
                (charProvider as IDisposable).Dispose();
                charProvider = null;
            }
        }
    }
    public Position Position {
        get {
            return beginWord;
        }
        set {
            charProvider.Position = value;
            isFirst = charProvider.IsFirst;
            isLast = false;
            current = fetchWordForward();
        }
    }
    public string Current { get { return current; } }
    public bool IsFirst { get { return isFirst; } }
    public bool IsLast { get { return isLast; } }
    public void ToNext() {
        if (isLast) {
            throw new ArgumentException("Already at bottom");
        }
        isFirst = false;
        charProvider.Position = endWord;
        current = fetchWordForward();
    }
    public void ToPrev() {
        if (isFirst) {
            throw new ArgumentException("Already at top");
        }
        isLast = false;
        charProvider.Position = beginWord;
        current = fetchWordBackward();
    }
    private delegate bool Skip();
    private string fetchWord(Skip skip, string doubleNL, ref Position start, ref Position end) {
        start = charProvider.Position;
        while (isWhiteSpace()) {
            if (!skip()) {
                end = charProvider.Position;
                return "";
            }
        }
        if (isNewLine()) {
            bool isDoubleNLStart = charProvider.Current == doubleNL[0];
            if (skip()) {
                if (isDoubleNLStart && charProvider.Current == doubleNL[1]) {
                    skip();
                }
            }
            end = charProvider.Position;
            return "\n";
        }
        string s = "";
        while (!Char.IsWhiteSpace(charProvider.Current)) {
            s += charProvider.Current;
            if (!skip()) {
                break;
            }
        }
        while (isWhiteSpace()) {
            if (!skip()) {
                break;
            }
        }
        end = charProvider.Position;
        return s;
    }
    private string fetchWordForward() {
        return fetchWord(skipForward, "\r\n", ref beginWord, ref endWord);
    }
    private string fetchWordBackward() {
        string s = fetchWord(skipBackward, "\n\r", ref endWord, ref beginWord);
        char[] arr = s.ToCharArray();
        Array.Reverse(arr);
        return new string(arr);
    }
    private bool skipForward() {
        if (charProvider.IsLast) {
            isLast = true;
            return false;
        } else {
            charProvider.ToNext();
            return true;
        }
    }
    private bool skipBackward() {
        if (charProvider.IsFirst) {
            isFirst = true;
            return false;
        } else {
            charProvider.ToPrev();
            return true;
        }
    }
    private bool isWhiteSpace() {
        return Char.IsWhiteSpace(charProvider.Current) && !isNewLine();
    }
    private bool isNewLine() {
        return charProvider.Current == '\n' || charProvider.Current == '\r';
    }
}

public class PlainTextParser : IScrollable<Token>, IDisposable {
    private WhiteSpaceTokenizer tokenizer;
    private Token currentToken;
    private enum State { BeginDoc, ImplicitBeginLine, BeginLine, Token, EndLine, ImplicitEndLine, EndDoc }
    private State state;

    public PlainTextParser(Stream s) {
        this.tokenizer = new WhiteSpaceTokenizer(s);
        this.state = State.BeginDoc;
        this.currentToken = getCurrent();
    }
    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    protected virtual void Dispose(bool disposing) {
        if (disposing) {
            if (tokenizer != null) {
                tokenizer.Dispose();
                tokenizer = null;
            }
        }
    }
    private class Pos : Position {
        public State state;
        public Position pos;
    }
    public Token Current { get { return currentToken; } }
    public Position Position { 
        get { 
            return new Pos { state = state, pos = tokenizer.Position }; 
        }
        set {
            Pos p = (Pos) value;
            state = p.state;
            tokenizer.Position = p.pos;
            currentToken = getCurrent();
        } 
    }
    public bool IsFirst { get { return state == State.BeginDoc; } }
    public bool IsLast { get { return state == State.EndDoc; } }
    private Token getCurrent() {
        switch (state) {
            case State.BeginDoc:
                return Token.DOCUMENT_BEGIN;
            case State.ImplicitBeginLine:
            case State.BeginLine:
                return Token.PARAGRAPH_BEGIN;
            case State.ImplicitEndLine:
            case State.EndLine:
                return Token.PARAGRAPH_END;
            case State.EndDoc:
                return Token.DOCUMENT_END;
            case State.Token:
                return new Token(tokenizer.Current, TokenType.Word);
            default:
                throw new ArgumentException("Unhandled state");
        }
    }
    public void ToNext() {
        switch (state) {
            case State.BeginDoc:
                state = State.ImplicitBeginLine;
                break;
            case State.ImplicitBeginLine:
                if (tokenizer.Current == "\n") {
                    state = State.EndLine;
                } else {
                    state = State.Token;
                }
                break;
            case State.BeginLine:
                if (tokenizer.IsLast) {
                    state = State.ImplicitEndLine;
                } else {
                    tokenizer.ToNext();
                    if (tokenizer.Current == "\n") {
                        state = State.EndLine;
                    } else {
                        state = State.Token;
                    }
                }
                break;
            case State.EndLine:
                state = State.BeginLine;
                break;
            case State.ImplicitEndLine:
                state = State.EndDoc;
                break;
            case State.EndDoc:
                throw new ArgumentException("Already at bottom");
            case State.Token:
                if (tokenizer.IsLast) {
                    state = State.ImplicitEndLine;
                } else {
                    tokenizer.ToNext();
                    if (tokenizer.Current == "\n") {
                        state = State.EndLine;
                    }
                }
                break;
        }
        currentToken = getCurrent();
    }
    public void ToPrev() {
        switch (state) {
            case State.BeginDoc:
                throw new ArgumentException("Already at bottom");
            case State.ImplicitBeginLine:
                state = State.BeginDoc;
                break;
            case State.BeginLine:
                state = State.EndLine;
                break;
            case State.EndLine:
                if (tokenizer.IsFirst) {
                    state = State.ImplicitBeginLine;
                } else {
                    tokenizer.ToPrev();
                    if (tokenizer.Current == "\n") {
                        state = State.BeginLine;
                    } else {
                        state = State.Token;
                    }
                }
                break;
            case State.ImplicitEndLine:
                if (tokenizer.Current == "\n") {
                    state = State.BeginLine;
                } else {
                    state = State.Token;
                }
                break;
            case State.EndDoc:
                state = State.ImplicitEndLine;
                break;
            case State.Token:
                if (tokenizer.IsFirst) {
                    state = State.ImplicitBeginLine;
                } else {
                    tokenizer.ToPrev();
                    if (tokenizer.Current == "\n") {
                        state = State.BeginLine;
                    }
                }
                break;
        }
        currentToken = getCurrent();
    }
}

public class SimplePlainTextParser : IScrollable<Token>, IDisposable {
    private SeekableStreamReader stream;
    private LinkedList<long> lineStartPos = new LinkedList<long>();

    private List<string> line;
    private int lineIndex;
    private Token currentToken;

    public SimplePlainTextParser(Stream s) {
        this.stream = new SeekableStreamReader(s, true);
        initStart();
    }
    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    protected virtual void Dispose(bool disposing) {
        if (disposing) {
            if (stream != null) {
                stream.Dispose();
                stream = null;
            }
        }
    }

    private List<string> fetchLine() {
        string s = stream.ReadLine();
        if (s == null) {
            return null;
        }
        List<string> result = new List<string>();
        int i = 0;
        while (i < s.Length) {
            while (i < s.Length && Char.IsWhiteSpace(s[i])) {
                i++;
            }
            string word = "";
            while (i < s.Length && !Char.IsWhiteSpace(s[i])) {
                word += s[i];
                i++;
            }
            if (word != "") {
                result.Add(word);
            }
        }
        return result;
    }

    private void initStart() {
        currentToken = new Token(Token.DOCUMENT, TokenType.Begin);
        line = new List<string>(0);
        lineIndex = 0;
        stream.Position = 0;
        lineStartPos.Clear();
    }

    public Token Current { get { return currentToken; } }
    public bool IsFirst { get { return currentToken.Text == Token.DOCUMENT && currentToken.Type == TokenType.Begin; } }
    public bool IsLast { get { return currentToken.Text == Token.DOCUMENT && currentToken.Type == TokenType.End; } }
    public void ToNext() {
        if (IsLast) {
            throw new ArgumentException("Already at bottom");
        }
        lineIndex++;
        if (lineIndex == line.Count) {
            currentToken = new Token(Token.PARAGRAPH, TokenType.End);
        } else if (lineIndex > line.Count) {
            lineIndex = -1;
            lineStartPos.AddLast(stream.Position);
            line = fetchLine();
            if (line != null) {
                currentToken = new Token(Token.PARAGRAPH, TokenType.Begin);
            } else {
                currentToken = new Token(Token.DOCUMENT, TokenType.End);
            }
        } else {
            currentToken = new Token(line[lineIndex], TokenType.Word);
        }
    }
    public void ToPrev() {
        if (IsFirst) {
            throw new ArgumentException("Already at top");
        }
        lineIndex--;
        if (lineIndex == -2) {
            if (lineStartPos.Count > 1) {
                lineStartPos.RemoveLast();
                stream.Position = lineStartPos.Last.Value;
                line = fetchLine();
                if (line != null) {
                    lineIndex = line.Count;
                } else {
                    lineIndex = 0;
                }
            } else {
                line = null;
            }
            if (line != null) {
                currentToken = new Token(Token.PARAGRAPH, TokenType.End);
            } else {
                initStart();
            }
        } else if (lineIndex == -1) {
            currentToken = new Token(Token.PARAGRAPH, TokenType.Begin);
        } else {
            currentToken = new Token(line[lineIndex], TokenType.Word);
        }
    }
    public class Pos : Position {
        public int lineIndex;
        public long lineStartPos;
    }
    public Position Position {
        get {
            return new Pos() {
                lineIndex = this.lineIndex,
                lineStartPos = this.lineStartPos.Count > 0 ? this.lineStartPos.Last.Value : -1
            };
        }
        set {
            Pos p = (Pos) value;
            if (p.lineStartPos < 0) {
                initStart();
                return;
            } else if (this.lineStartPos.Count != 0 && p.lineStartPos < this.lineStartPos.Last.Value) {
                while (p.lineStartPos < this.lineStartPos.Last.Value) {
                    lineStartPos.RemoveLast();
                }
                stream.Position = lineStartPos.Last.Value;
                line = fetchLine();
            } else {
                while (p.lineStartPos > this.lineStartPos.Last.Value) {
                    lineStartPos.AddLast(stream.Position);
                    line = fetchLine();
                }
            }
            this.lineIndex = p.lineIndex;
            if (line != null) {
                if (lineIndex == -1) {
                    currentToken = new Token(Token.PARAGRAPH, TokenType.Begin);
                } else if (lineIndex == line.Count) {
                    currentToken = new Token(Token.PARAGRAPH, TokenType.End);
                } else {
                    currentToken = new Token(line[lineIndex], TokenType.Word);
                }
            } else {
                if (lineIndex == -1) {
                    currentToken = new Token(Token.DOCUMENT, TokenType.Begin);
                } else if (lineIndex == 0) {
                    currentToken = new Token(Token.DOCUMENT, TokenType.End);
                } else {
                    throw new ArgumentException("Invalid position");
                }
            }
        }
    }

}

}
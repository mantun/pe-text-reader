using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

using TextReader.Configuration;
using TextReader.Formatting;
using TextReader.ScrollingView;
using TextReader.ScrollingView.RowProviders;
using TextReader.Serialization;

namespace TextReader.Parsing {

public class Bookmark {
    private string text;
    private Position pos;
    private List<Bookmark> subItems = new List<Bookmark>();

    public string Text { get { return text; } }
    public Position Position { get { return pos; } }
    public List<Bookmark> SubItems { get { return subItems; } }

    private Bookmark() {
    }

    public Bookmark(string text, Position pos) {
        this.text = text;
        this.pos = pos;
    }
}

public class BookIndex {
    public Bookmark Autosave;
    public List<Bookmark> UserBookmarks = new List<Bookmark>();
    public List<Bookmark> TOC = new List<Bookmark>();
}

public abstract class BookFile : IDisposable {
    protected FileInfo file;
    protected string indexFile;
    protected BookIndex index;
    protected IScrollable<Token> parser;
    protected RowProvider rowProvider;
    protected BookFile(FileInfo file) {
        this.file = file;
        indexFile = Config.AppData() + "\\" + getIndexFileName(file);
        if (!loadIndex()) {
            index = new BookIndex();
            index.TOC = buildTOC();
        }
    }
    ~BookFile() {
        Dispose(false);
    }
    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    protected virtual void Dispose(bool disposing) {
        if (disposing) {
            if (rowProvider != null) {
                if (rowProvider is IDisposable) {
                    (rowProvider as IDisposable).Dispose();
                }
                rowProvider = null;
            }
            if (parser != null) {
                if (parser is IDisposable) {
                    (parser as IDisposable).Dispose();
                }
                parser = null;
            }
        }
    }
    public FileInfo File { get { return file; } }
    public static BookFile Create(FileInfo file) {
        if (file.Extension == ".txt") {
            return new PlainTextFile(file);
        } else if (file.Extension == ".sfb") {
            return new SFBFile(file);
        }
        return new PlainTextFile(file);
    }
    public IScrollable<Token> Parser {
        get {
            if (parser == null) {
                parser = createParser();
            }
            return parser;
        }
    }
    public RowProvider RowProvider {
        get {
            if (rowProvider == null) {
                rowProvider = createRowProvider();
            }
            return rowProvider;
        }
    }
    public BookIndex Index { get { return index; } }
    public void SaveIndex() {
        using (FileStream s = new FileStream(indexFile, FileMode.Create)) {
            Serializer.Serialize(index, s);
        }
    }
    private string getIndexFileName(FileInfo file) {
        int i = 0;
        foreach (char c in file.DirectoryName) {
            i <<= 1;
            i ^= c;
        }
        long l = file.LastWriteTime.Millisecond ^ file.Length;
        return i.ToString("X8") + "-" + ((l >> 32) ^ l).ToString("X8") + "-" + file.Name + ".idx";
    }
    private bool loadIndex() {
        if (System.IO.File.Exists(indexFile)) {
            using (FileStream s = new FileStream(indexFile, FileMode.Open)) {
                index = (BookIndex) Serializer.Deserialize(s);
            }
            return true;
        } else {
            return false;
        }
    }
    protected abstract IScrollable<Token> createParser();
    protected abstract RowProvider createRowProvider();
    protected abstract List<Bookmark> buildTOC();
}

public class PlainTextFile : BookFile {
    private FileStream stream;
    private SeekableStreamReader reader; 

    public PlainTextFile(FileInfo file) : base(file) { }
    protected override void Dispose(bool disposing) {
        base.Dispose(disposing);
        if (reader != null) {
            reader.Dispose();
            reader = null;
        }
        if (stream != null) {
            stream.Dispose();
            stream = null;
        }
    }
    protected override IScrollable<Token> createParser() {
        if (reader != null) {
            reader.Dispose();
        }
        if (stream != null) {
            stream.Dispose();
        }
        stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
        reader = new SeekableStreamReader(stream, true);
        IScrollable<string> lineReader = new LineOfTextTokenizer(new BufferedCharProvider(reader));
        return new MappingScrollable<Token, string>(lineReader, s => new Token(s, TokenType.Word));
    }
    protected override RowProvider createRowProvider() {
        return new PlainTextRowProvider(new Font("Courier New", 10, FontStyle.Regular), Parser);
    }
    protected override List<Bookmark> buildTOC() {
        return new List<Bookmark>();
    }
}

public class SFBFile : BookFile {
    private FileStream stream;
    private SeekableStreamReader reader; 

    public SFBFile(FileInfo file) : base(file) { }
    protected override void Dispose(bool disposing) {
        base.Dispose(disposing);
        if (reader != null) {
            reader.Dispose();
            reader = null;
        }
        if (stream != null) {
            stream.Dispose();
            stream = null;
        }
    }
    protected override IScrollable<Token> createParser() {
        if (reader != null) {
            reader.Dispose();
        }
        if (stream != null) {
            stream.Dispose();
        }
        stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
        reader = new SeekableStreamReader(stream, true);
        IScrollable<string> lineReader = new LineOfTextTokenizer(new BufferedCharProvider(reader));
        return new SFBParser(lineReader);
    }
    protected override RowProvider createRowProvider() {
        return new FormattedRowProvider(Parser);
    }
    protected override List<Bookmark> buildTOC() {
        var root = new BmkTree();
        using (var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read)) {
            using (var reader = new SeekableStreamReader(stream, true)) {
                IScrollable<string> lineReader = new LineOfTextTokenizer(new BufferedCharProvider(reader));
                bool isNew = true;
                BmkTree current = root;
                while (true) {
                    string line = lineReader.Current;
                    int level = hlevel(line);
                    if (level > 0) {
                        current = add(current, line.Substring(level).Trim(), lineReader.Position, level, isNew);
                        isNew = false;
                    } else {
                        isNew = true;
                    }
                    if (lineReader.IsLast) {
                        break;
                    }
                    lineReader.ToNext();
                }
            }
        }
        return root.createBookmark().SubItems;
    }
    private int hlevel(string line) {
        int l = 0;
        if (line.StartsWith(">")) {
            for (int i = 0; i < 5; i++) {
                if (i >= line.Length) {
                    break;
                }
                if (line[i] == '>') {
                    l++;
                }
            }
        }
        return l;
    }
    private class BmkTree {
        public BmkTree parent;
        public List<BmkTree> items = new List<BmkTree>();
        public string text;
        public Position pos;
        public int level;
        public Bookmark createBookmark() {
            Bookmark result = new Bookmark(text, pos);
            foreach (var b in items) {
                result.SubItems.Add(b.createBookmark());
            }
            return result;
        }
        public override string ToString() {
            return text;
        }
    }
    private BmkTree add(BmkTree current, string text, Position pos, int level, bool isNew) {
        if (!isNew && level == current.level) {
            current.text += "\n" + text;
            return current;
        } else {
            while (level < current.level) {
                current = current.parent;
            }
            if (level == current.level) {
                BmkTree sibling = new BmkTree() { parent = current.parent, pos = pos, level = level, text = text };
                current.parent.items.Add(sibling);
                return sibling;
            } 
            while (level >= current.level + 1) {
                BmkTree sub = new BmkTree() { parent = current, pos = pos, level = current.level + 1, text = text };
                current.items.Add(sub);
                current = sub;
            }
            return current;
        }
    }
}

}
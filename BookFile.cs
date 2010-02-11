using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;

using TextReader.Interop;
using TextReader.ScrollingView;
using TextReader.ScrollingView.RowProviders;
using TextReader.Serialization;
using TextReader.Configuration;

namespace TextReader.Parsing {

public class Bookmark {
    private string text;
    private Position pos;
    private List<Bookmark> subNodes = new List<Bookmark>();

    public string Text { get { return text; } }
    public Position Position { get { return pos; } }
    public ICollection<Bookmark> SubNodes { get { return subNodes.AsReadOnly(); } }

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
        }
        return null;
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
    public FileStream stream;

    public PlainTextFile(FileInfo file) : base(file) { }
    protected override void Dispose(bool disposing) {
        base.Dispose(disposing);
        if (stream != null) {
            stream.Dispose();
            stream = null;
        }
    }
    protected override IScrollable<Token> createParser() {
        if (stream != null) {
            stream.Dispose();
        }
        stream = new FileStream(file.FullName, FileMode.Open);
        return new PlainTextParser(stream);
    }
    protected override RowProvider createRowProvider() {
        return new PlainTextRowProvider(new Font("Courier New", 10, FontStyle.Regular), Parser);
    }
    protected override List<Bookmark> buildTOC() {
        return new List<Bookmark>();
    }
}

}
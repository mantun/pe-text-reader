using System;
using System.Collections.Generic;
using System.Drawing;

using TextReader.Interop;
using TextReader.Parsing;

namespace TextReader.ScrollingView.RowProviders {

public class PlainTextRowProvider : RowProviderBase, IDisposable {
    private const int tabSize = 4;

    private SplittingScrollable<Row, List<Row>> rowScroller;
    private CachedMappingScrollable<List<Row>, Token> paragraphConverter;
    private int width;
    private int rowHeight;
    private Font basicFont;
    private IntPtr measuringDC;
    private IntPtr hFont;
    private SolidBrush brush;

    public PlainTextRowProvider(Font basicFont, IScrollable<Token> parser) {
        this.brush = new SolidBrush(Color.Black);
        this.measuringDC = GDI.CreateCompatibleDC(IntPtr.Zero);
        this.basicFont = basicFont;
        this.hFont = basicFont.ToHfont();
        GDI.SelectObject(measuringDC, hFont);
        this.rowHeight = calcRowHeight();

        paragraphConverter = new CachedMappingScrollable<List<Row>, Token>(parser, reflowParagraph);
        this.rowScroller = new SplittingScrollable<Row, List<Row>>(paragraphConverter);
    }
    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    ~PlainTextRowProvider() {
        Dispose(false);
    }
    protected virtual void Dispose(bool disposing) {
        if (disposing) {
            if (this.brush != null) {
                this.brush.Dispose();
                this.brush = null;
            }
        }
        if (this.measuringDC != IntPtr.Zero) {
            GDI.DeleteDC(this.measuringDC);
            this.measuringDC = IntPtr.Zero;
        }
        if (this.hFont != IntPtr.Zero) {
            GDI.DeleteObject(this.hFont);
            this.hFont = IntPtr.Zero;
        }
    }
    private List<Row> reflowParagraph(Token t) {
        var result = new List<Row>();
        int i = 0;
        while (i < t.Text.Length) {
            result.Add(buildRow(ref i, t.Text));
        }
        if (result.Count == 0) {
            result.Add(new PlainTextRow("", this));
        }
        return result;
    }
    private Row buildRow(ref int inLineIndex, string line) {
        string text = "";
        while (true) {
            string t;

            // get whitespace
            int i = inLineIndex;
            while (i < line.Length && Char.IsWhiteSpace(line[i])) {
                i++;
            }

            // keep whitespace at line beginning only if it is paragraph beginning
            if (inLineIndex > 0 && text.Length == 0) {
                inLineIndex = i;
            }

            // get word
            while (i < line.Length && !Char.IsWhiteSpace(line[i])) {
                i++;
            }

            if (inLineIndex == i) { // no more words (eol or only whitespace)
                return new PlainTextRow(text, this);
            }

            string word = line.Substring(inLineIndex, i - inLineIndex);
            t = expandTabs(text + word, tabSize);

            if (textWidth(t) < width) {
                text = t;
                inLineIndex = i;
            } else {
                if (text.Length == 0) {
                    if (inLineIndex == 0) {
                        inLineIndex++;
                        return new PlainTextRow(text, this);
                    }
                    int l = fitLongWord(word);
                    inLineIndex += l;
                    return new PlainTextRow(word.Substring(0, l), this);
                } else {
                    return new PlainTextRow(text, this);
                }
            }
        }
    }
    private int fitLongWord(string word) {
        int index = 0;
        while (index < word.Length && Char.IsWhiteSpace(word[index])) {
            index++;
        }
        if (index > 0) {
            return index;
        }
        int max = word.Length;
        int min = 0;
        int candidate = max / 2;
        while (true) {
            int w = textWidth(word.Substring(0, candidate));
            int c;
            if (w < width) {
                min = candidate;
                c = (candidate + max) / 2;
                if (c == candidate) {
                    break;
                }
            } else {
                max = candidate;
                c = (min + candidate) / 2;
                if (c == candidate) {
                    break;
                }
            }
            candidate = c;
        }
        if (candidate == 0) {
            candidate++;
        }
        return candidate;
    }
    private int textWidth(string text) {
        GDI.Rect rect = textBounds(text);
        return rect.Right - rect.Left;
    }
    private int calcRowHeight() {
        GDI.Rect rect = textBounds("0");
        return rect.Bottom - rect.Top;
    }
    private GDI.Rect textBounds(string text) {
        GDI.Rect rect = new GDI.Rect(0, 0, 5, 5);
        GDI.DrawText(measuringDC, text, text.Length, ref rect, GDI.DT_NOPREFIX | GDI.DT_CALCRECT);
        return rect;
    }
    public override Row Current { get { return rowScroller.Current; } }
    public override bool IsFirst { get { return rowScroller.IsFirst; } }
    public override bool IsLast { get { return rowScroller.IsLast; } }
    public override void ToNext() {
        rowScroller.ToNext();
    }
    public override void ToPrev() {
        rowScroller.ToPrev();
    }
    public override Position Position { 
        get { return rowScroller.Position; }
        set { rowScroller.Position = value; }
    }
    public override int Width {
        get { return width; }
        set { 
            width = value;
            paragraphConverter.Invalidate();
        }
    }
    public Color ForeColor {
        get { return brush.Color; }
        set { brush.Color = value; }
    }
    public Font BasicFont {
        get {
            return basicFont;
        }
        set {
            basicFont = value;
            if (hFont != IntPtr.Zero) {
                GDI.DeleteObject(hFont);
            }
            hFont = basicFont.ToHfont();
            GDI.SelectObject(measuringDC, hFont);
            rowHeight = calcRowHeight();
            paragraphConverter.Invalidate();
        }
    }

    private string expandTabs(string s, int tabSize) {
        int i;
        while ((i = s.IndexOf("\t")) >= 0) {
            s = s.Replace("\t", new string(' ', tabSize - i % tabSize));
        }
        return s;
    }

    private class PlainTextRow : Row {
        private string text;
        private PlainTextRowProvider parent;
        public PlainTextRow(string text, PlainTextRowProvider parent) {
            this.text = text;
            this.parent = parent;
        }
        public string Text { get { return text; } }
        public int Height { get { return parent.rowHeight; } }
        public void Draw(Graphics g, int y, bool highlighted) {
            GDI.Rect rect = new GDI.Rect(0, y, parent.width, y + parent.rowHeight);
            IntPtr hdc = g.GetHdc();
            IntPtr originalObject = GDI.SelectObject(hdc, parent.hFont);
            GDI.DrawText(hdc, text, text.Length, ref rect, GDI.DT_NOPREFIX | GDI.DT_NOCLIP);
            GDI.SelectObject(hdc, originalObject);
            g.ReleaseHdc(hdc);
        }
        public override string ToString() {
            return text + '\n';
        }
    }
}

}
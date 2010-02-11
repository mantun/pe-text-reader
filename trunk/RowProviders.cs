using System;
using System.Collections.Generic;
using System.Drawing;

using TextReader.Interop;
using TextReader.Parsing;

namespace TextReader.ScrollingView.RowProviders {

public class PlainTextRowProvider : RowProviderBase, IDisposable {
    private IScrollable<Token> parser;
    private Row currentRow;
    private int width;
    private int rowHeight;
    private Font basicFont;
    private IntPtr measuringDC;
    private IntPtr hFont;
    private SolidBrush brush;
    LinkedList<Position> rowPos = new LinkedList<Position>();

    public PlainTextRowProvider(Font basicFont, IScrollable<Token> parser) {
        this.parser = parser;
        this.brush = new SolidBrush(Color.Black);
        this.measuringDC = GDI.CreateCompatibleDC(IntPtr.Zero);
        while (!parser.IsFirst) {
            parser.ToPrev();
        }
        rowPos.AddLast(parser.Position);
        this.BasicFont = basicFont;
        currentRow = buildRow();
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
    private Row buildRow() {
        string text = "";
        if (parser.Current.Equals(Token.DOCUMENT, TokenType.Begin)) {
            parser.ToNext();
        }
        if (parser.Current.Equals(Token.PARAGRAPH, TokenType.Begin)) {
            parser.ToNext();
        }
        if (parser.IsLast) {
            return null;
        }
        while (true) {
            if (parser.Current.Equals(Token.PARAGRAPH, TokenType.End)) {
                parser.ToNext();
                return new PlainTextRow(text, this);
            }
            string t = text;
            if (t.Length > 0){
                t += ' ';
            }
            t += parser.Current.Text;
            if (textWidth(t) < width) {
                parser.ToNext();
                text = t;
            } else {
                if (text.Length == 0) {
                    parser.ToNext();
                    return new PlainTextRow(t, this);
                } else {
                    return new PlainTextRow(text, this);
                }
            }
        }
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
    public override Row Current { get { return currentRow; } }
    public override bool IsFirst { get { return rowPos.Count == 1; } }
    public override bool IsLast { get { return parser.IsLast; } }
    public override void ToNext() {
        if (IsLast) {
            throw new ArgumentException("Already at bottom");
        }
        rowPos.AddLast(parser.Position);
        currentRow = buildRow();
    }
    public override void ToPrev() {
        if (IsFirst) {
            throw new ArgumentException("Already at top");
        }
        rowPos.RemoveLast();
        parser.Position = rowPos.Last.Value;
        currentRow = buildRow();
    }
    private class Pos : Position {
        public int n;
        public Position pos;
    }
    public override Position Position { 
        get { 
            return new Pos() { pos = rowPos.Last.Value, n = rowPos.Count }; 
        }
        set { 
            Pos p = (Pos) value;
            if (p.n < rowPos.Count) {
                while (p.n < rowPos.Count) {
                    rowPos.RemoveLast();
                }
                parser.Position = p.pos;
                currentRow = buildRow();
            } else {
                while (p.n > rowPos.Count) {
                    rowPos.AddLast(parser.Position);
                    currentRow = buildRow();
                }
            }
        }
    }
    public override int Width {
        get {
            return width;
        }
        set {
            width = value;
            LinkedListNode<Position> first = rowPos.First;
            rowPos.Clear();
            rowPos.AddLast(first);
            parser.Position = first.Value;
            currentRow = buildRow();
        }
    }

    public Color ForeColor {
        get {
            return brush.Color;
        }
        set {
            brush.Color = value;
        }
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
            Position pos = rowPos.First.Value;
!            rowPos.Clear();
            rowPos.AddLast(pos);
            parser.Position = pos;
            currentRow = buildRow();
        }
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
        public void Draw(Graphics g, int y) {
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
using System;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;

using TextReader;
using TextReader.Interop;
using TextReader.Parsing;
using TextReader.ScrollingView;

namespace TextReader.Formatting {

public class FormattedRowProvider : RowProviderBase {
    private IScrollable<Row> rowScroller;
    private Builder builder;
    public FormattedRowProvider(IScrollable<Token> parser) {
        builder = new Builder(new Font("Arial", 10, FontStyle.Regular), parser);
        rowScroller = new SplittingScrollable<Row, List<Row>>(builder);
    }

    public override Row Current { get { return rowScroller.Current; } }
    public override bool IsLast { get { return rowScroller.IsLast; } }
    public override bool IsFirst { get { return rowScroller.IsFirst; } }
    public override void ToNext() { rowScroller.ToNext(); }
    public override void ToPrev() { rowScroller.ToPrev(); }
    public override Position Position {
        get { return rowScroller.Position; }
        set { rowScroller.Position = value; }
    }
    public override int Width {
        get { return builder.Width; }
        set { builder.Width = value; }
    }

    private class Builder : AggregatingScrollable<List<Row>, Token> {
        private Stack<Style> styles;
        private StyleFactory styleFactory;
        private int spaceWidth;
        private int indentSize;
        private int width;
        public Builder(Font baseFont, IScrollable<Token> parser) : base(parser) {
            styleFactory = new StyleFactory(baseFont);
            styles = new Stack<Style>();
            styles.Push(styleFactory.Normal);
            spaceWidth = textBounds(" ", styleFactory.Normal.Font).Right;
            indentSize = spaceWidth * 4;
            init();
        }
        protected override void init() {
            skipForward();
            base.init();
        }
        public int Width {
            get {
                return width;
            }
            set {
                width = value;
                moveBackwardToParagraphBeginning();
                init();
            }
        }
        protected override List<Row> fetchForward(ref bool isLast) {
            var result = buildRows();
            Position pos = underlying.Position;
            while (!underlying.IsLast && underlying.Current.Type != TokenType.Word) {
                underlying.ToNext();
            }
            isLast = underlying.IsLast;
            underlying.Position = pos;
            return result;
        }

        protected override List<Row> fetchBackward(ref bool isFirst) {
            moveBackwardToParagraphBeginning();
            isFirst = checkFirst();
            Position pos = underlying.Position;
            var result = buildRows();
            underlying.Position = pos;
            return result;
        }

        protected override void skipForward() {
            while (!underlying.Current.Equals(Token.PARAGRAPH, TokenType.Begin)) {
                if (underlying.Current.Type == TokenType.Begin) {
                    enterStyle(underlying.Current.Text);
                } else if (underlying.Current.Type == TokenType.End) {
                    leaveStyle(underlying.Current.Text);
                }
                underlying.ToNext();
            }
        }

        protected override void skipBackward() {
            while (!underlying.Current.Equals(Token.PARAGRAPH, TokenType.End)) {
                if (underlying.Current.Type == TokenType.End) {
                    enterStyle(underlying.Current.Text);
                } else if (underlying.Current.Type == TokenType.Begin) {
                    leaveStyle(underlying.Current.Text);
                }
                underlying.ToPrev();
            }
        }

        protected override bool checkFirst() {
            Position pos = underlying.Position;
            while (!underlying.IsFirst && underlying.Current.Type != TokenType.Word) {
                underlying.ToPrev();
            }
            bool isFirst = underlying.IsFirst;
            underlying.Position = pos;
            return isFirst;
        }

        public override Position Position {
            get {
                return new Pos() { pos = base.Position, activeStyles = new List<StyleName>(from Style s in styles select s.Name) };
            }
            set {
                styles.Clear();
                styles.Push(styleFactory.Normal);
                if (value is Pos) {
                    Pos p = (Pos) value;
                    for (int i = p.activeStyles.Count - 2; i >= 0; i--) {
                        styles.Push(styleFactory.createStyle(p.activeStyles[i], styles.Peek()));
                    }
                    base.Position = p.pos;
                } else {
                    underlying.Position = value;
                    skipForward();
                    base.Position = underlying.Position;
                }
            }
        }

        private class Pos : Position {
            public Position pos;
            public List<StyleName> activeStyles;
        }

        private void moveBackwardToParagraphBeginning() {
            while (!underlying.Current.Equals(Token.PARAGRAPH, TokenType.Begin)) {
                if (underlying.Current.Type == TokenType.End) {
                    enterStyle(underlying.Current.Text);
                } else if (underlying.Current.Type == TokenType.Begin) {
                    leaveStyle(underlying.Current.Text);
                }
                underlying.ToPrev();
            }
        }

        private List<Row> buildRows() {
            if (!underlying.Current.Equals(Token.PARAGRAPH, TokenType.Begin)) {
                throw new ArgumentException("Paragraph start expected");
            }
            underlying.ToNext();
            List<Row> rows = new List<Row>();
            if (!(styles.Peek() is ParagraphStyle)) {
                throw new ArgumentException("Character styles found outside paragraph");
            }
            ParagraphStyle style = styles.Peek() as ParagraphStyle;
            FormattedRow currentRow = new FormattedRow(style, true, spaceWidth, this);
            foreach (RowItem word in getWords()) {
                if (currentRow.Fits(word)) {
                    currentRow.Add(word);
                } else {
                    rows.Add(currentRow);
                    currentRow = new FormattedRow(style, false, spaceWidth, this);
                    currentRow.Add(word);
                }
            }
            rows.Add(currentRow);
            return rows;
        }
        private IEnumerable<RowItem> getWords() {
            while (!underlying.Current.Equals(Token.PARAGRAPH, TokenType.End)) {
                if (underlying.Current.Type == TokenType.Begin) {
                    enterStyle(underlying.Current.Text);
                } else if (underlying.Current.Type == TokenType.End) {
                    leaveStyle(underlying.Current.Text);
                } else {
                    yield return new Word(underlying.Current.Text, styles.Peek().Font);
                }
                underlying.ToNext();
            }
        }
        private void enterStyle(string styleName) {
            if (!Enum.IsDefined(typeof(StyleName), styleName)) {
                return;
            }
            StyleName newStyleName = (StyleName) Enum.Parse(typeof(StyleName), styleName, false);
            styles.Push(styleFactory.createStyle(newStyleName, styles.Peek()));
            spaceWidth = textBounds(" ", styles.Peek().Font).Right;
        }
        private void leaveStyle(string styleName) {
            if (!Enum.IsDefined(typeof(StyleName), styleName)) {
                return;
            }
            if (styles.Count == 1) {
                System.Diagnostics.Debug.WriteLine("Trying to leave normal style: " + styleName);
            }
            StyleName currStyleName = (StyleName) Enum.Parse(typeof(StyleName), styleName, false);
            if (styles.Peek().Name != currStyleName) {
                System.Diagnostics.Debug.WriteLine("Unbalanced tags: in " + styles.Peek().Name.ToString()
                    + " trying to close " + styleName);
            }
            styles.Pop();
            spaceWidth = textBounds(" ", styles.Peek().Font).Right;
        }

        private class FormattedRow : Row {
            private List<RowItem> words = new List<RowItem>();
            private ParagraphStyle style;
            private bool isFirst;
            private int height;
            private int width;
            private int spaceWidth;
            private Builder parent;
            public FormattedRow(ParagraphStyle style, bool isFirst, int spaceWidth, Builder parent) {
                this.style = style;
                this.isFirst = isFirst;
                this.spaceWidth = spaceWidth;
                this.parent = parent;
                this.height = textBounds("0", style.Font).Bottom;
            }
            public void Add(RowItem word) {
                if (height < word.Height) {
                    height = word.Height;
                }
                if (words.Count == 0) {
                    width += indent() + word.Width;
                } else {
                    width += spaceWidth + word.Width;
                }
                words.Add(word);
            }
            public bool Fits(RowItem word) {
                if (words.Count == 0) {
                    return true;
                }
                return width + spaceWidth + word.Width < parent.Width;
            }
            private int indent() {
                if (style.Align == ParagraphAlign.Center) {
                    return 0;
                }
                return parent.indentSize * (isFirst ? style.FirstLineIndent : style.Indent);
            }
            public string Text {
                get {
                    string result = "";
                    foreach (RowItem word in words) {
                        if (result != "") {
                            result += " ";
                        }
                        result += word.Text;
                    }
                    return result;
                }
            }
            public int Height { get { return height; } }

            public void Draw(Graphics g, int y) {
                int w;
                if (style.Align == ParagraphAlign.Center) {
                    w = (parent.width - width) / 2;
                } else if (style.Align == ParagraphAlign.Right) {
                    w = parent.width - width;
                } else {
                    w = indent();
                }
                foreach (RowItem word in words) {
                    word.Draw(g, w, y);
                    w += spaceWidth + word.Width;
                }
            }
        }

        private static GDI.Rect textBounds(string text, Font font) {
            IntPtr measuringDC = GDI.CreateCompatibleDC(IntPtr.Zero);
            IntPtr hFont = font.ToHfont();
            GDI.SelectObject(measuringDC, hFont);
            GDI.Rect rect = new GDI.Rect(0, 0, 5, 5);
            GDI.DrawText(measuringDC, text, text.Length, ref rect, GDI.DT_NOPREFIX | GDI.DT_CALCRECT);
            GDI.DeleteDC(measuringDC);
            GDI.DeleteObject(hFont);
            return rect;
        }

        private interface RowItem {
            int Width { get; }
            int Height { get; }
            string Text { get; }
            void Draw(Graphics g, int x, int y);
        }
        private class Word : RowItem {
            private Font font;
            private int width;
            private int height;
            private string text;
            public Word(string text, Font font) {
                GDI.Rect rect = textBounds(text, font);
                this.width = rect.Right;
                this.height = rect.Bottom;
                this.text = text;
                this.font = font;
            }
            public int Width { get { return width; } }
            public int Height { get { return height; } }
            public string Text { get { return text; } }
            public void Draw(Graphics g, int x, int y) {
                GDI.Rect rect = new GDI.Rect(x, y, x + width, y + height);
                IntPtr hdc = g.GetHdc();
                IntPtr hFont = font.ToHfont();
                IntPtr originalObject = GDI.SelectObject(hdc, hFont);
                GDI.DrawText(hdc, text, text.Length, ref rect, GDI.DT_NOPREFIX | GDI.DT_NOCLIP);
                GDI.SelectObject(hdc, originalObject);
                GDI.DeleteObject(hFont);
                g.ReleaseHdc(hdc);
            }
        }
    }
}

}
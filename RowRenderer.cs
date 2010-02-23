using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace TextReader.ScrollingView {

public class RowRenderer {
    private int width;
    private int height;
    private int rowPixelOffset = 0;
    private RowProvider rows;

    public RowRenderer(int width, int height, RowProvider rowProvider) {
        this.width = width;
        this.height = height;
        this.rows = rowProvider;
    }

    public Position Position {
        get {
            return new Pos() { rowOffs = rowPixelOffset, pos = rows.Position };
        }
        set {
            if (value is Pos) {
                Pos p = (Pos) value;
                rows.Position = p.pos;
                rowPixelOffset = p.rowOffs;
            } else {
                rows.Position = value;
                rowPixelOffset = 0;
            }
        }
    }
    private class Pos : Position {
        public int rowOffs;
        public Position pos;
    }
    public class Result : IDisposable {
        public Image Image;
        public int Offset;
        public Position position;
        public Row[] Rows;
        public int[] RowOffsets;
        ~Result() {
            Dispose();
        }
        public void Dispose() {
            Image.Dispose();
            GC.SuppressFinalize(this);
        }
    }
    public int Width { get { return width; } set { width = value; } }
    public int Height { get { return height; } set { height = value; } }
    public Result DrawImage(int pixelDelta) {
        pixelDelta = normalizeDelta(pixelDelta);
        int h = checkBottom(ref pixelDelta);

        // draw
        Bitmap image = new Bitmap(width, h);
        Graphics g = Graphics.FromImage(image);

        Rectangle r = new Rectangle(0, 0, image.Width, image.Height);
        g.FillRectangle(new SolidBrush(Color.White), r);

        List<Row> visibleRows = new List<Row>();
        List<int> rowOffsets = new List<int>();
        h = -rowPixelOffset;
        Position pos = rows.Position;
        while (h < image.Height) {
            visibleRows.Add(rows.Current);
            rowOffsets.Add(h);
            rows.Current.Draw(g, h);
            h += rows.Current.Height;
            if (rows.IsLast) {
                break;
            }
            rows.ToNext();
        }
        rows.Position = pos;

        g.Dispose();
        return new Result() { Image = image, position = Position, Offset = pixelDelta, 
            Rows = visibleRows.ToArray(), RowOffsets = rowOffsets.ToArray() };
    }

    private int normalizeDelta(int pixelDelta) {
        rowPixelOffset += pixelDelta;
        if (rowPixelOffset >= 0) {
            while (rowPixelOffset >= rows.Current.Height) {
                rowPixelOffset -= rows.Current.Height;
                if (rows.IsLast) {
                    pixelDelta = rowPixelOffset;
                    rowPixelOffset = rows.Current.Height - 1;
                    return pixelDelta;
                } else {
                    rows.ToNext();
                }
            }
        } else {
            while (rowPixelOffset < 0) {
                if (rows.IsFirst) {
                    pixelDelta = rowPixelOffset;
                    rowPixelOffset = 0;
                    return pixelDelta;
                } else {
                    rows.ToPrev();
                }
                rowPixelOffset += rows.Current.Height;
            }
        }
        return 0;
    }
    
    private int checkBottom(ref int pixelDelta) {
        // check bottom
        int h = -rowPixelOffset;
        while (true) {
            h += rows.Current.Height;
            if (rows.IsLast || h >= height) {
                break;
            }
            rows.ToNext();
        }
        if (h < height) { // implies IsLast
            pixelDelta += height - h;
            h = 0;
        } else {
            h = height - h;
        }
        // back to top
        while (true) {
            h += rows.Current.Height;
            if (rows.IsFirst || h >= height) {
                break;
            }
            rows.ToPrev();
        }
        if (h < height) { // implies IsFirst
            pixelDelta -= height - h;
            rowPixelOffset = 0;
        } else {
            rowPixelOffset = h - height;
            h = height;
        }
        return h;
    }
}

public interface Row {
    int Height { get; }
    void Draw(Graphics g, int y);
    string Text { get; }
}

public delegate void RowClickHandler(Row row, MouseEventArgs e);
public delegate void ContentChangedHandler(int direction);
public interface RowProvider : IScrollable<Row> {
    int Width { get; set; }
    event RowClickHandler OnRowClick;
    void RowClicked(Row row, MouseEventArgs e);
    event ContentChangedHandler OnContentChanged;
    void ContentChanged(int direction);
}

public class CachingRowProvider : CachingScrollable<Row>, RowProvider {
    private RowProvider rowProvider;
    public CachingRowProvider(RowProvider rowProvider, int cacheSize) : base(rowProvider, cacheSize) {
        this.rowProvider = rowProvider;
        rowProvider.OnContentChanged += contentChanged;
    }
    public int Width {
        get {
            return rowProvider.Width;
        }
        set {
            Invalidate(false);
            rowProvider.Width = value;
        }
    }
    public event RowClickHandler OnRowClick {
        add { rowProvider.OnRowClick += value; }
        remove { rowProvider.OnRowClick -= value; } 
    }
    public event ContentChangedHandler OnContentChanged;
    public void RowClicked(Row row, MouseEventArgs e) {
        rowProvider.RowClicked(row, e);
    }
    public void ContentChanged(int direction) {
        rowProvider.ContentChanged(direction);
    }
    private void contentChanged(int direction) {
        Invalidate(true);
        if (OnContentChanged != null) {
            OnContentChanged(direction);
        }
    }
}

public abstract class RowProviderBase : RowProvider {

    public event ContentChangedHandler OnContentChanged;
    public event RowClickHandler OnRowClick;
    public virtual void RowClicked(Row row, MouseEventArgs e) {
        if (OnRowClick != null) {
            OnRowClick(row, e);
        }
    }
    public virtual void ContentChanged(int direction) {
        if (OnContentChanged != null) {
            OnContentChanged(direction);
        }
    }
    public abstract Row Current { get; }
    public abstract bool IsLast { get; }
    public abstract bool IsFirst { get; }
    public abstract void ToNext();
    public abstract void ToPrev();
    public abstract Position Position { get; set; }
    public abstract int Width { get; set; }
}

public class ContentRowProvider : RowProviderBase {
    private IScrollable<Row> content;
    public void SetContent(IScrollable<Row> newContent, int direction) {
        this.content = newContent;
        ContentChanged(direction);
    }
    public override Row Current { get { return content.Current; } }
    public override bool IsLast { get { return content.IsLast; } }
    public override bool IsFirst { get { return content.IsFirst; } }
    public override void ToNext() { content.ToNext(); }
    public override void ToPrev() { content.ToPrev(); }
    public override Position Position {
        get { return content.Position; }
        set { content.Position = value; }
    }
    public override int Width { get; set; }
}

}
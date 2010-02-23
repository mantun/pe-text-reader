using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

using TextReader.Interop;
using TextReader.ScrollingView;

namespace TextReader.TreeBrowse {

public class TreeBrowser : Form {

    private ScrollablePanel itemsPanel;
    private Panel locationPanel;

    private ContentRowProvider rowProvider;
    private ItemsHolder itemsHolder;
    private int fontHeight;
    private int iconWidth;
    private int rowHeight;
    private bool locationExpanded;
    private TextureBrush arrowBrush;

    private TreeItem selected;
    private TreeItem current;

    public TreeItem Selected { get { return selected; } }
    public TreeItem Current {
        get { return current; }
        set { 
            int direction = 0;
            if (current != null) {
                if (current.IsChildOf(value)) {
                    direction = -1;
                } else if (value.IsChildOf(current)) {
                    direction = 1;
                }
            }
            current = value;
            if (itemsHolder != null) {
                itemsHolder.Dispose();
            }
            itemsHolder = current.CreateItems();
            if (itemsPanel.RowProvider == null) {
                rowProvider.SetContent(itemsHolder.CreateRows(), direction);
                itemsPanel.RowProvider = new CachingRowProvider(rowProvider, 100);
            } else {
                lock (itemsPanel.RowProvider) {
                    rowProvider.SetContent(itemsHolder.CreateRows(), direction);
                }
            }
        }
    }

    public override Font Font {
        get {
            return base.Font;
        }
        set {
            base.Font = value;
            this.fontHeight = GDI.FontHeight(Font);
        }
    }
    public TreeBrowser() {
        this.MinimizeBox = false;
        this.iconWidth = GDI.GetSystemMetrics(GDI.SM_CXICON);
        this.rowHeight = GDI.GetSystemMetrics(GDI.SM_CYICON);
        this.fontHeight = GDI.FontHeight(Font);

        itemsPanel = new ScrollablePanel();
        Rectangle r = this.ClientRectangle;
        r.Height -= rowHeight;
        r.Y += rowHeight;
        itemsPanel.Location = new Point(r.Left, r.Top);
        itemsPanel.Size = new Size(r.Right - r.Left, r.Bottom - r.Top);
        itemsPanel.Anchor = AnchorStyles.Bottom | AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        this.Controls.Add(itemsPanel);

        this.locationPanel = new Panel();
        r = this.ClientRectangle;
        r.Height = rowHeight;
        locationPanel.Location = new Point(r.Left, r.Top);
        locationPanel.Size = new Size(r.Right - r.Left, r.Bottom - r.Top);
        locationPanel.Paint += paint;
        locationPanel.MouseUp += locationPanelClick;
        locationPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        locationPanel.BringToFront();
        this.Controls.Add(locationPanel);

        using (Image gradient = new Bitmap(1, rowHeight)) {
            int c = 0xFF;
            using (Graphics g = Graphics.FromImage(gradient)) {
                for (int i = gradient.Height / 6; i < 5 * gradient.Height / 6; i++) {
                    Color color = Color.FromArgb(c, c, c);
                    g.DrawLine(new Pen(color), 0, i, 0, i + 1);
                    c -= 0x80 / (5 * gradient.Height / 6);
                }
            }
            this.arrowBrush = new TextureBrush(gradient);
        }

        MenuItem cancelMenuItem = new MenuItem();
        cancelMenuItem.Text = "Cancel";
        cancelMenuItem.Click += cancelClick;
        MainMenu menu = new MainMenu();
        menu.MenuItems.Add(cancelMenuItem);
        this.Menu = menu;

        rowProvider = new ContentRowProvider();
        rowProvider.OnRowClick += selectRow;
    }

    protected override void Dispose(bool disposing) {
        if (disposing) {
            if (itemsPanel != null) {
                itemsPanel.Dispose();
                itemsPanel = null;
            }
            if (locationPanel != null) {
                locationPanel.Dispose();
                locationPanel = null;
            }
            if (arrowBrush != null) {
                arrowBrush.Dispose();
                arrowBrush = null;
            }
            if (itemsHolder != null) {
                itemsHolder.Dispose();
                itemsHolder = null;
            }
        }
        base.Dispose(disposing);
    }

    private void paint(Object sender, PaintEventArgs e) {
        Panel p = sender as Panel;
        int y = p.ClientRectangle.Height - rowHeight;
        TreeItem item = Current;
        e.Graphics.FillRectangle(new SolidBrush(Color.LightGray), p.ClientRectangle);
        while (y >= 0 && item != null) {
            e.Graphics.DrawString(item.Name, Font, new SolidBrush(Color.Black), iconWidth, y + (rowHeight - fontHeight) / 2);
            y -= rowHeight;
            item = item.Parent;
        }
        if (!locationExpanded && Current.Parent != null) {
            int r = rowHeight;
            e.Graphics.FillPolygon(arrowBrush, new Point[] { new Point(2 * r / 3, r / 6), new Point(2 * r / 3, 5 * r / 6), new Point(r / 3, r / 2) });
        }
    }

    private void selectRow(Row row, MouseEventArgs e) {
        TreeItem item = current.ChildFromRow(row);
        if (item == null) {
            return;
        }
        if (item.IsLeaf) {
            selected = item;
            Thread.Sleep(100); // give the user a chance to see the highlighted row
            Close();
        } else {
            Current = item;
            locationPanel.Invalidate();
        }
    }
    private void cancelClick(Object sender, EventArgs e) {
        selected = null;
        Close();
    }
    private void locationPanelClick(Object sender, MouseEventArgs e) {
        if (locationExpanded) {
            int targetLevel = (locationPanel.Height - e.Y) / rowHeight;
            if (targetLevel > 0) {
                TreeItem item = Current;
                while (targetLevel > 0) {
                    targetLevel--;
                    item = item.Parent;
                }
                Current = item;
            }
            collapseLocation();
        } else {
            if (e.X < this.iconWidth) {
                if (Current.Parent != null) {
                    Current = Current.Parent;
                    locationPanel.Invalidate();
                }
            } else {
                int levels = 0;
                TreeItem item = Current;
                while (item != null) {
                    levels++;
                    item = item.Parent;
                }
                if (levels > 1) {
                    if (locationPanel.Top + levels * rowHeight > this.ClientRectangle.Height) {
                        levels = (this.ClientRectangle.Height - locationPanel.Top) / rowHeight;
                    }
                    locationPanel.BringToFront();
                    expandLocation(levels * rowHeight);
                }
            }
        }
    }

    private void expandLocation(int target) {
        while (true) {
            locationExpanded = true;
            locationPanel.Height += 20;
            if (locationPanel.Height >= target) {
                locationPanel.Height = target;
                locationPanel.Refresh();
                break;
            }
            locationPanel.Refresh();
            Thread.Sleep(20);
        }
    }

    private void collapseLocation() {
        while (true) {
            locationExpanded = false;
            locationPanel.Height -= 20;
            if (locationPanel.Height < rowHeight) {
                locationPanel.Height = rowHeight;
                locationPanel.Refresh();
                break;
            }
            locationPanel.Refresh();
            itemsPanel.Refresh();
            Thread.Sleep(20);
        }
    }

}

public class DrawParams {
    public int Width;
    public int RowHeight;
    public int FontHeight;
    public Font Font;
    public DrawParams(Font font, int width) {
        Font = font;
        Width = width;
        FontHeight = GDI.FontHeight(Font);
        RowHeight = GDI.GetSystemMetrics(GDI.SM_CYICON);
    }
}

public interface ItemsHolder : IDisposable {
    IScrollable<Row> CreateRows();
}

public interface TreeItem {
    String Name { get; }
    ItemsHolder CreateItems();
    TreeItem Parent { get; }
    bool IsChildOf(TreeItem other);
    bool IsLeaf { get; }
    TreeItem ChildFromRow(Row row);
}

}
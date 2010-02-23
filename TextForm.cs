using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using TextReader.Configuration;
using TextReader.Parsing;
using TextReader.ScrollingView;
using TextReader.TreeBrowse;
using TextReader.TreeBrowse.FileSystem;
using TextReader.TreeBrowse.Bookmarks;

namespace TextReader {

class TextForm : System.Windows.Forms.Form {

    private ScrollablePanel panel;
    private BookFile bookFile;
    private MenuItem setBookmarkMenuItem;
    private MenuItem tocMenuItem;
    private DirectoryInfo lastDir;

    public BookFile BookFile {
        get {
            return bookFile;
        }
        set {
            if (bookFile != null) {
                bookFile.Dispose();
            }
            bookFile = value;
            setBookmarkMenuItem.Enabled = bookFile != null;
            tocMenuItem.Enabled = bookFile != null;
            if (bookFile != null) {
                panel.RowProvider = new CachingRowProvider(bookFile.RowProvider, 300);
                if (bookFile.Index.Autosave != null) {
                    panel.Position = bookFile.Index.Autosave.Position;
                }
            }
        }
    }

    public static void Main() {
        var textForm = new TextForm();
        Application.Run(textForm);
    }

    public TextForm() {
        Text = "PE Text Reader";
        panel = new ScrollablePanel();
        Rectangle r = this.ClientRectangle;
        r.Inflate(-10, -10);
        panel.Left = r.Left;
        panel.Top = r.Top;
        panel.Size = new Size(r.Right - r.Left, r.Bottom - r.Top);
        panel.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom;
        this.Controls.Add(panel);

        this.Deactivate += onDeactivate;
        this.Activated += onActivate;

        MenuItem exitMenuItem = new MenuItem() { Text = "Exit" };
        exitMenuItem.Click += exitClick;
        MenuItem openMenuItem = new MenuItem() { Text = "Open" };
        openMenuItem.Click += openClick;
        tocMenuItem = new MenuItem() { Text = "TOC", Enabled = false };
        tocMenuItem.Click += tocClick;
        setBookmarkMenuItem = new MenuItem() { Text = "Set Bookmark Here", Enabled = false };
        setBookmarkMenuItem.Click += setBookmarkClick;
        MenuItem menuRoot = new MenuItem() { Text = "Menu" };
        menuRoot.MenuItems.Add(openMenuItem);
        menuRoot.MenuItems.Add(tocMenuItem);
        menuRoot.MenuItems.Add(setBookmarkMenuItem);
        menuRoot.MenuItems.Add(exitMenuItem);

        MenuItem autoscrollMenuItem = new MenuItem() { Text = "Autoscroll" };
        autoscrollMenuItem.Click += autoscrollClick;
        
        MainMenu menu = new MainMenu();
        menu.MenuItems.Add(menuRoot);
        menu.MenuItems.Add(autoscrollMenuItem);
        this.Menu = menu;
    }

    protected override void OnLoad(EventArgs e) {
        var book = SelectBook();
        if (book == null) {
            Close();
        }
        BookFile = book;
        base.OnLoad(e);
    }

    public BookFile SelectBook() {
        BookFile file;
        List<FileInfo> recent = Config.LoadRecentFiles();
        using (TreeBrowser fileBrowser = new TreeBrowser()) {
            fileBrowser.Font = new Font("Arial", 10, FontStyle.Regular);
            if (lastDir == null) {
                lastDir = new DirectoryInfo(".");
            }
            if (recent.Count == 0) {
                fileBrowser.Current = new DirectoryTreeItem(lastDir, new DrawParams(fileBrowser.Font, fileBrowser.ClientRectangle.Width));
            } else {
                fileBrowser.Current = new RecentRootItem(recent.ToArray(), lastDir, new DrawParams(fileBrowser.Font, fileBrowser.ClientRectangle.Width));
            }
            fileBrowser.ShowDialog();
            if (fileBrowser.Selected == null) {
                return null;
            }
            FileInfo f;
            if (fileBrowser.Selected is FileTreeItem) {
                f = (fileBrowser.Selected as FileTreeItem).File;
            } else if (fileBrowser.Selected is RecentItem) {
                f = (fileBrowser.Selected as RecentItem).File;
            } else {
                throw new ArgumentException("Unknown item type");
            }
            lastDir = f.Directory;
            file = BookFile.Create(f);
        }
        recent.RemoveAll(f => f.FullName.Equals(file.File.FullName));
        recent.Insert(0, file.File);
        if (recent.Count > 20) {
            recent.RemoveRange(20, recent.Count - 20);
        }
        Config.SaveRecentFiles(recent);
        return file;
    }

    private void tocClick(object sender, EventArgs e) {
        using (TreeBrowser browser = new TreeBrowser()) {
            browser.Current = new BookmarkRootItem(BookFile.Index);
            browser.ShowDialog();
            if (browser.Selected == null) {
                return;
            }
            panel.Position = (browser.Selected as BookmarkItem).Bookmark.Position;
        }
    }

    private void openClick(object sender, EventArgs e) {
        var book = SelectBook();
        if (book != null) {
            BookFile = book;
        }
    }

    private void exitClick(object sender, EventArgs e) {
        autosaveBookmark();
        Close();
    }

    private void onDeactivate(object sender, EventArgs e) {
        panel.Freeze();
        autosaveBookmark();
    }

    private void onActivate(object sender, EventArgs e) {
        panel.Unfreeze();
    }

    private void autoscrollClick(object sender, EventArgs e) {
    }

    private void setBookmarkClick(object sender, EventArgs e) {
        int n = BookFile.Index.UserBookmarks.Count + 1;
        Bookmark bookmark;
        lock (panel.RowProvider) {
            int y = 0;
            bookmark = new Bookmark(n + ": " + panel.RowAt(ref y).Text + "\u2026", panel.Position);
        }
        BookFile.Index.UserBookmarks.Add(bookmark);
        BookFile.SaveIndex();
    }

    private void autosaveBookmark() {
        Bookmark bookmark;
        lock (panel.RowProvider) {
            int y = 0;
            bookmark = new Bookmark("A: " + panel.RowAt(ref y).Text + "\u2026", panel.Position);
        }
        bookFile.Index.Autosave = bookmark;
        bookFile.SaveIndex();
    }
}

}
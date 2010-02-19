using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;

using TextReader.Interop;
using TextReader.ScrollingView;

namespace TextReader.TreeBrowse.FileSystem {

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

class FileSystemItemsHolder : ItemsHolder {
    private Dictionary<FileSystemInfo, FileRow> createdRows = new Dictionary<FileSystemInfo, FileRow>();
    private DrawParams drawParams;
    private DirectoryInfo dir;
    private FileRow emptyRow;
    public FileSystemItemsHolder(DirectoryInfo dir, DrawParams drawParams) {
        this.dir = dir;
        this.drawParams = drawParams;
    }
    public void Dispose() {
        if (createdRows != null) {
            foreach (FileRow row in createdRows.Values) {
                row.Dispose();
            }
            createdRows = null;
        }
    }
    public IScrollable<Row> createRows() {
        var files = new ArrayScrollable<FileSystemInfo>(listFiles(dir));
        return new MappingScrollable<Row, FileSystemInfo>(files, getRow);
    }
    private Row getRow(FileSystemInfo f) {
        if (f == null) {
            if (emptyRow == null) {
                emptyRow = new FileRow(f, drawParams);
            }
            return emptyRow;
        }
        if (!createdRows.ContainsKey(f)) {
            createdRows.Add(f, new FileRow(f, drawParams));
        }
        return createdRows[f];
    }
    private FileSystemInfo[] listFiles(DirectoryInfo dir) {
        DirectoryInfo[] dirNames = dir.GetDirectories();
        FileInfo[] fileNames = dir.GetFiles();
        List<FileSystemInfo> fileRows = new List<FileSystemInfo>(fileNames.Length + dirNames.Length);
        for (int i = 0; i < dirNames.Length; i++) {
            if ((dirNames[i].Attributes & (FileAttributes.System | FileAttributes.Hidden)) == 0) {
                fileRows.Add(dirNames[i]);
            }
        }
        for (int i = 0; i < fileNames.Length; i++) {
            if ((fileNames[i].Attributes & (FileAttributes.System | FileAttributes.Hidden)) == 0) {
                fileRows.Add(fileNames[i]);
            }
        }
        if (fileRows.Count == 0) {
            return new FileSystemInfo[] { null };
        } else {
            return fileRows.ToArray();
        }
    }
}

class FileRow : Row, IDisposable {
    private FileSystemInfo info;
    private DrawParams drawParams;
    private Icon icon;

    public void Dispose() {
        if (icon != null) {
            icon.Dispose();
            icon = null;
        }
    }
    public FileRow(FileSystemInfo info, DrawParams drawParams) {
        this.drawParams = drawParams;
        this.info = info;
        if (info != null) {
            Shell.SHFILEINFO shinfo = new Shell.SHFILEINFO();
            IntPtr hImageList = Shell.SHGetFileInfo(info.FullName, 0, ref shinfo,
                (uint) Marshal.SizeOf(shinfo), Shell.SHGFI_SYSICONINDEX | Shell.SHGFI_ICON);
            // Fetch the icon
            IntPtr hIcon = Shell.ImageList_GetIcon(hImageList, shinfo.iIcon, Shell.ILD_NORMAL);
            this.icon = Icon.FromHandle(hIcon);
        }
    }
    public void Draw(Graphics g, int y) {
        g.DrawLine(new Pen(Color.Gray), 0, y, drawParams.Width, y);
        if (info != null) {
            g.DrawIcon(icon, 0, y + (drawParams.RowHeight - icon.Height) / 2);
            g.DrawString(info.Name, drawParams.Font, new SolidBrush(Color.Black), icon.Width, y + (drawParams.RowHeight - drawParams.FontHeight) / 2);
        } else {
            g.DrawString("(no items)", drawParams.Font, new SolidBrush(Color.Gray), 0, y + (drawParams.RowHeight - drawParams.FontHeight) / 2);
        }
    }
    public int Height { get { return drawParams.RowHeight; } }
    public string Text { get { return info != null ? info.Name : ""; } }
    public FileSystemInfo Info { get { return info; } }
}

public class DirectoryTreeItem : TreeItem {
    private DrawParams drawParams;
    public readonly DirectoryInfo Dir;
    public DirectoryTreeItem(DirectoryInfo dir, DrawParams drawParams) {
        this.Dir = dir;
        this.drawParams = drawParams;
    }
    public String Name { get { return Dir.Name; } }
    public ItemsHolder CreateItems() {
        return new FileSystemItemsHolder(Dir, drawParams);
    }
    public TreeItem Parent { get { return Dir.Parent == null ? null : new DirectoryTreeItem(Dir.Parent, drawParams); } }
    public bool IsChildOf(TreeItem other) {
        if (other is DirectoryTreeItem) {
            return Dir.FullName.StartsWith((other as DirectoryTreeItem).Dir.FullName);
        }
        return false;
    }
    public bool IsLeaf { get { return false; } }
    public TreeItem ChildFromRow(Row row) {
        if (row is FileRow) {
            FileRow fileRow = row as FileRow;
            if (fileRow.Info is FileInfo) {
                return new FileTreeItem(fileRow.Info as FileInfo, drawParams);
            } else if (fileRow.Info is DirectoryInfo) {
                return new DirectoryTreeItem(fileRow.Info as DirectoryInfo, drawParams);
            } else {
                return null;
            }
        } else {
            throw new ArgumentException("Not a FileRow");
        }
    }
}

public class FileTreeItem : TreeItem {
    private DrawParams drawParams;
    public readonly FileInfo File;
    public FileTreeItem(FileInfo file, DrawParams drawParams) {
        this.File = file;
        this.drawParams = drawParams;
    }
    public String Name { get { return File.Name; } }
    public ItemsHolder CreateItems() {
        throw new ArgumentException("Leaf node");
    }
    public TreeItem Parent { get { return new DirectoryTreeItem(File.Directory, drawParams); } }
    public bool IsChildOf(TreeItem other) {
        if (other is DirectoryTreeItem) {
            return File.FullName.StartsWith((other as DirectoryTreeItem).Dir.FullName);
        }
        return false;
    }
    public bool IsLeaf { get { return true; } }
    public TreeItem ChildFromRow(Row row) {
        throw new ArgumentException("Leaf node");
    }
}

}
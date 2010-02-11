using System;
using System.Windows.Forms;
using System.Drawing;
using System.Collections.Generic;
using System.IO;

using TextReader.ScrollingView;
using TextReader.Configuration;

namespace TextReader.TreeBrowse.FileSystem {

class RecentItemsHolder : ItemsHolder {
    private Dictionary<FileSystemInfo, FileRow> createdRows = new Dictionary<FileSystemInfo, FileRow>();
    private DrawParams drawParams;
    private FileInfo[] recent;
    public RecentItemsHolder(FileInfo[] recent, DrawParams drawParams) {
        this.drawParams = drawParams;
        this.recent = recent;
    }
    public IScrollable<Row> createRows() {
        var files = new ArrayScrollable<FileInfo>(recent);
        return new MappingScrollable<Row, FileInfo>(files, getRow);
    }
    private Row getRow(FileSystemInfo f) {
        if (!createdRows.ContainsKey(f)) {
            createdRows.Add(f, new FileRow(f, drawParams));
        }
        return createdRows[f];
    }
    public void Dispose() {
        if (createdRows != null) {
            foreach (FileRow row in createdRows.Values) {
                row.Dispose();
            }
            createdRows = null;
        }
    }
}

public class RecentRootItem : TreeItem {
    private DrawParams drawParams;
    private DirectoryInfo startDir;
    private FileInfo[] recent;
    public RecentRootItem(FileInfo[] recent, DirectoryInfo startDir, DrawParams drawParams) {
        this.drawParams = drawParams;
        this.startDir = startDir;
        this.recent = recent;
    }
    public String Name { get { return "(Recent)"; } }
    public ItemsHolder CreateItems() {
        return new RecentItemsHolder(recent, drawParams);
    }
    public TreeItem Parent { get { return new DirectoryTreeItem(startDir, drawParams); } }
    public bool IsParentOf(TreeItem other) {
        if (other is RecentItem) {
            return true;
        }
        return false;
    }
    public bool IsLeaf { get { return false; } }
    public TreeItem ChildFromRow(Row row) {
        if (row is FileRow) {
            FileRow fileRow = row as FileRow;
            return new RecentItem(fileRow.Info as FileInfo);
        } else {
            throw new ArgumentException("Not a FileRow");
        }
    }
}

public class RecentItem : TreeItem {
    public readonly FileInfo File;
    public RecentItem(FileInfo file) {
        this.File = file;
    }
    public String Name { get { return File.Name; } }
    public ItemsHolder CreateItems() {
        throw new ArgumentException("Leaf node");
    }
    public TreeItem Parent { get { return null; } }
    public bool IsParentOf(TreeItem other) {
        return false;
    }
    public bool IsLeaf { get { return true; } }
    public TreeItem ChildFromRow(Row row) {
        throw new ArgumentException("Leaf node");
    }
}

}
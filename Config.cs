using System;
using System.Text;
using System.IO;
using System.Collections.Generic;

using TextReader.Interop;

namespace TextReader.Configuration {

public class Config {
    public static string AppData() {
        StringBuilder resultPath = new StringBuilder(255);
        Shell.SHGetSpecialFolderPath((IntPtr)0, resultPath, (int) Shell.CSIDL.CSIDL_APPDATA, 0);
        string appDir = resultPath.ToString() + "\\TextReader";
        if (!Directory.Exists(appDir)) {
            Directory.CreateDirectory(appDir);
        }
        return appDir;
    }
    public static List<FileInfo> LoadRecentFiles() {
        List<string> fileNames = loadRecentFileNames();
        List<FileInfo> recentFiles = new List<FileInfo>();
        foreach (string fileName in fileNames) {
            if (File.Exists(fileName)) {
                recentFiles.Add(new FileInfo(fileName));
            }
        }
        return recentFiles;
    }
    private static List<string> loadRecentFileNames() {
        string recentFilesFile = AppData() + "\\recent.txt";
        if (!File.Exists(recentFilesFile)) {
            return new List<string>();
        }
        List<string> result = new List<string>();
        using (StreamReader r = new StreamReader(recentFilesFile)) {
            string s;
            while ((s = r.ReadLine()) != null) {
                result.Add(s);
            }
        }
        return result;
    }
    public static void SaveRecentFiles(List<FileInfo> recentFiles) {
        using (StreamWriter w = new StreamWriter(AppData() + "\\recent.txt")) {
            foreach (FileInfo f in recentFiles) {
                w.WriteLine(f.FullName);
            }
        }
    }
}

}
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace TextReader.Interop {

public class GDI {
    public const int DT_NOCLIP = 0x00000100;
    public const int DT_NOPREFIX = 0x00000800;
    public const int DT_CALCRECT = 0x00000400;
    public const int DT_TABSTOP = 0x00000080;
#if PPC
    [DllImport("coredll.dll")]
#else
    [DllImport("GDI32.dll")]
#endif
    public static extern bool DeleteObject(IntPtr hObject); 

#if PPC
    [DllImport("coredll.dll")]
#else
    [DllImport("GDI32.dll")]
#endif
    public static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

#if PPC
    [DllImport("coredll.dll")]
#else
    [DllImport("USER32.dll")]
#endif
    public static extern IntPtr GetDC(IntPtr hwnd);

#if PPC
    [DllImport("coredll.dll")]
#else
    [DllImport("USER32.dll")]
#endif
    public static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

#if PPC
    [DllImport("coredll.dll")]
#else
    [DllImport("GDI32.dll")]
#endif
    public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

#if PPC
    [DllImport("coredll.dll")]
#else
    [DllImport("GDI32.dll")]
#endif
    public static extern bool DeleteDC(IntPtr hdc);

#if PPC
    [DllImport("coredll.dll")]
#else
    [DllImport("USER32.dll")]
#endif
    public static extern int DrawText(IntPtr hdc, string lpStr, int nCount, ref Rect lpRect, int wFormat);

    public struct Rect {
        public int Left, Top, Right, Bottom;
        public Rect(int left, int top, int right, int bottom) {
            this.Left = left;
            this.Top = top;
            this.Right = right;
            this.Bottom = bottom;
        }
        public Rect(Rectangle r) {
          this.Left = r.Left;
          this.Top = r.Top;
          this.Bottom = r.Bottom;
          this.Right = r.Right;
        }
    }

#if PPC
    [DllImport("coredll.dll")]
#else
    [DllImport("USER32.dll")]
#endif
    public static extern int DestroyIcon(IntPtr hIcon);

    public const int SM_CXICON = 11;
    public const int SM_CYICON = 12;
    public const int SM_CYICONSPACING = 39;
    public const int SM_CXSMICON = 49;
    public const int SM_CYSMICON = 50;

#if PPC
    [DllImport("coredll.dll")]
#else
    [DllImport("USER32.dll")]
#endif
    public static extern int GetSystemMetrics(int nIndex);

    public static int FontHeight(Font font) {
        IntPtr measuringDC = GDI.CreateCompatibleDC(IntPtr.Zero);
        IntPtr hFont = font.ToHfont();
        GDI.SelectObject(measuringDC, hFont);
        GDI.Rect rect = new GDI.Rect(0, 0, 5, 5);
        GDI.DrawText(measuringDC, "0", 1, ref rect, GDI.DT_NOPREFIX | GDI.DT_CALCRECT);
        GDI.DeleteObject(hFont);
        GDI.DeleteDC(measuringDC);
        return rect.Bottom - rect.Top;
    }
}

public class Shell {
    public const uint SHGFI_SYSICONINDEX = 0x000004000;   // get system icon index
    public const uint SHGFI_SMALLICON = 0x1; // Small icon
    public const uint SHGFI_ICON = 0x000000100;

    [StructLayout(LayoutKind.Sequential)]
    public struct SHFILEINFO {
        public IntPtr hIcon;
        public Int32 iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    };

#if PPC
    [DllImport("coredll.dll")]
#else
    [DllImport("SHELL32.dll")]
#endif
    public static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

    public const uint ILD_NORMAL = 0x00;

#if PPC
    [DllImport("coredll.dll")]
#else
    [DllImport("ComCtl32.dll")]
#endif
    public static extern IntPtr ImageList_GetIcon(IntPtr himl, int i, uint flags);

    public enum CSIDL : int {
        CSIDL_DESKTOP = 0,
        CSIDL_PROGRAMS = 2,
        CSIDL_PERSONAL = 5,
        CSIDL_FAVORITES_GRYPHON = 6,
        CSIDL_STARTUP = 7,
        CSIDL_APPDATA = 26            // \Storage\Application Data
    }

#if PPC
    [DllImport("coredll.dll")]
#else
    [DllImport("SHELL32.dll")]
#endif
    public static extern int SHGetSpecialFolderPath(IntPtr hwndOwner, StringBuilder lpszPath, int nFolder, int fCreate);
}

}


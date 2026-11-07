using System;
using System.IO;
using System.Runtime.InteropServices;

namespace CorbiePet {
  public static class RecycleBinHoard {
    [StructLayout(LayoutKind.Sequential)]
    struct SHQUERYRBINFO {
      public int cbSize;
      public long i64Size;
      public long i64NumItems;
    }

    [DllImport("shell32.dll", CharSet=CharSet.Auto)]
    static extern int SHQueryRecycleBin(string pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Auto)]
    struct SHFILEOPSTRUCT {
      public IntPtr hwnd;
      public uint wFunc;
      [MarshalAs(UnmanagedType.LPTStr)]
      public string pFrom;
      [MarshalAs(UnmanagedType.LPTStr)]
      public string pTo;
      public ushort fFlags;
      [MarshalAs(UnmanagedType.Bool)]
      public bool fAnyOperationsAborted;
      public IntPtr hNameMappings;
      [MarshalAs(UnmanagedType.LPTStr)]
      public string lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet=CharSet.Auto)]
    static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);

    const uint FO_DELETE = 0x0003;
    const ushort FOF_ALLOWUNDO = 0x0040;
    const ushort FOF_NOCONFIRMATION = 0x0010;
    const ushort FOF_SILENT = 0x0004;

    public static bool WarnBeforeHoarding { get; set; }

    static RecycleBinHoard() {
      WarnBeforeHoarding = true;
    }

    public sealed class StashInfo {
      public long ItemCount { get; set; }
      public long TotalBytes { get; set; }
      public string FormattedSize { get; set; }
      public bool IsEmpty { get { return ItemCount <= 0; } }
    }

    public static string FormatBytes(long bytes) {
      if(bytes < 1024) return bytes + " B";
      if(bytes < 1024 * 1024) return (bytes / 1024.0).ToString("0.#") + " KB";
      if(bytes < 1024 * 1024 * 1024) return (bytes / (1024.0 * 1024.0)).ToString("0.#") + " MB";
      return (bytes / (1024.0 * 1024.0 * 1024.0)).ToString("0.##") + " GB";
    }

    public static StashInfo Query() {
      var info = new SHQUERYRBINFO();
      info.cbSize = Marshal.SizeOf(typeof(SHQUERYRBINFO));
      int hr = SHQueryRecycleBin(null, ref info);
      if(hr != 0) {
        return new StashInfo { ItemCount = 0, TotalBytes = 0, FormattedSize = "0 B" };
      }
      return new StashInfo {
        ItemCount = info.i64NumItems,
        TotalBytes = info.i64Size,
        FormattedSize = FormatBytes(info.i64Size)
      };
    }

    public static bool SendToHoard(string path) {
      if(string.IsNullOrEmpty(path)) return false;
      if(!File.Exists(path) && !Directory.Exists(path)) return false;

      var fileOp = new SHFILEOPSTRUCT();
      fileOp.wFunc = FO_DELETE;
      fileOp.pFrom = path + "\0\0";
      fileOp.fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT;
      int res = SHFileOperation(ref fileOp);
      return res == 0 && !fileOp.fAnyOperationsAborted;
    }

    public static void Check() {
      if(FormatBytes(500) != "500 B") throw new Exception("FormatBytes 500 B failed");
      if(FormatBytes(1024 * 1024 * 15) != "15 MB") throw new Exception("FormatBytes 15 MB failed");
      var info = Query();
      if(info == null) throw new Exception("RecycleBinHoard.Query returned null");
      if(!WarnBeforeHoarding) throw new Exception("WarnBeforeHoarding should default to true");
    }
  }
}

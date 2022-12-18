// https://qiita.com/kirurobo/items/5d0d5b4fb7dcf6bfe487

using System;
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
public class OpenFileName
{
    public int structSize = 0;
    public IntPtr dlgOwner = IntPtr.Zero;
    public IntPtr instance = IntPtr.Zero;
    public string filter = null;
    public string customFilter = null;
    public int maxCustFilter = 0;
    public int filterIndex = 0;
    public string file = null;
    public int maxFile = 0;
    public string fileTitle = null;
    public int maxFileTitle = 0;
    public string initialDir = null;
    public string title = null;
    public int flags = 0;
    public short fileOffset = 0;
    public short fileExtension = 0;
    public string defExt = null;
    public IntPtr custData = IntPtr.Zero;
    public IntPtr hook = IntPtr.Zero;
    public string templateName = null;
    public IntPtr reservedPtr = IntPtr.Zero;
    public int reservedInt = 0;
    public int flagsEx = 0;

    public static readonly int OFN_ALLOWMULTISELECT = 0x00000200;
    public static readonly int OFN_CREATEPROMPT = 0x00000200;
    public static readonly int OFN_DONTADDTORECENT = 0x02000000;
    public static readonly int OFN_ENABLEHOOK = 0x00000020;
    public static readonly int OFN_ENABLEINCLUDENOTIFY = 0x00400000;
    public static readonly int OFN_ENABLESIZING = 0x00800000;
    public static readonly int OFN_ENABLETEMPLATE = 0x00000040;
    public static readonly int OFN_ENABLETEMPLATEHANDLE = 0x00000080;
    public static readonly int OFN_EXPLORER = 0x00080000;
    public static readonly int OFN_EXTENSIONDIFFERENT = 0x00000400;
    public static readonly int OFN_FILEMUSTEXIST = 0x00001000;
    public static readonly int OFN_FORCESHOWHIDDEN = 0x10000000;
    public static readonly int OFN_HIDEREADONLY = 0x00000004;
    public static readonly int OFN_LONGNAMES = 0x00200000;
    public static readonly int OFN_NOCHANGEDIR = 0x00000008;
    public static readonly int OFN_NODEREFERENCELINKS = 0x00100000;
    public static readonly int OFN_NOLONGNAMES = 0x00040000;
    public static readonly int OFN_NONETWORKBUTTON = 0x00020000;
    public static readonly int OFN_NOREADONLYRETURN = 0x00008000;
    public static readonly int OFN_NOTESTFILECREATE = 0x00010000;
    public static readonly int OFN_NOVALIDATE = 0x00000100;
    public static readonly int OFN_OVERWRITEPROMPT = 0x00000002;
    public static readonly int OFN_PATHMUSTEXIST = 0x00000800;
    public static readonly int OFN_READONLY = 0x00000001;
    public static readonly int OFN_SHAREAWARE = 0x00004000;
    public static readonly int OFN_SHOWHELP = 0x00000010;

    public OpenFileName()
    {
        this.structSize = Marshal.SizeOf(this);
        this.filter = "All Files\0*.*\0\0";
        this.file = new string('\0', 4096);
        this.maxFile = this.file.Length;
        this.fileTitle = new string('\0', 256);
        this.maxFileTitle = this.fileTitle.Length;
        this.title = "Open";
        this.flags = OFN_EXPLORER
            | OFN_FILEMUSTEXIST
            | OFN_PATHMUSTEXIST
            | OFN_NOCHANGEDIR;
    }

    [DllImport("Comdlg32.dll", SetLastError = true, ThrowOnUnmappableChar = true, CharSet = CharSet.Auto)]
    public static extern bool GetOpenFileName([In, Out] OpenFileName lpOfn);

    /// <summary>
    /// このメソッドでダイアログが開く
    /// </summary>
    /// <returns>ファイルが選択されればそのパス、非選択ならnullを返す</returns>
    public static string ShowDialog()
    {
        OpenFileName ofn = new OpenFileName();
        if (GetOpenFileName(ofn))
        {
            return ofn.file;
        }
        return null;
    }
}
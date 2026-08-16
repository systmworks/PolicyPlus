using System;
using System.Runtime.InteropServices;

internal static class PInvoke
{
    [DllImport("user32.dll")]
    public static extern bool ShowScrollBar(IntPtr Handle, int Scrollbar, bool Show);

    [DllImport("userenv.dll")]
    public static extern bool RefreshPolicyEx(bool IsMachine, uint Options);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    public static extern int RegLoadKeyW(IntPtr HiveKey, string Subkey, string File);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    public static extern int RegUnLoadKeyW(IntPtr HiveKey, string Subkey);

    [DllImport("kernel32.dll")]
    public static extern IntPtr GetCurrentProcess();

    [DllImport("advapi32.dll")]
    public static extern bool OpenProcessToken(IntPtr Process, uint Access, out IntPtr TokenHandle);

    [DllImport("advapi32.dll")]
    public static extern bool AdjustTokenPrivileges(IntPtr Token, bool DisableAll, ref PInvokeTokenPrivileges NewState, uint BufferLength, IntPtr Null, out uint ReturnLength);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    public static extern bool LookupPrivilegeValueW(string SystemName, string Name, out PInvokeLuid LUID);

    [DllImport("kernel32.dll")]
    public static extern bool CloseHandle(IntPtr Handle);

    [DllImport("kernel32.dll")]
    public static extern bool GetProductInfo(int MajorVersion, int MinorVersion, int SPMajor, int SPMinor, out int EditionCode);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool SendNotifyMessageW(IntPtr Handle, int Message, UIntPtr WParam, IntPtr LParam);

    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr Handle, int Message, IntPtr WParam, IntPtr LParam);

    // Used to draw the sort arrow (HDF_SORTUP/HDF_SORTDOWN) on a ListView column header - a native
    // common-control header feature with no managed WinForms equivalent
    [DllImport("user32.dll", EntryPoint = "SendMessage")]
    public static extern IntPtr SendMessageHdItem(IntPtr Handle, int Message, IntPtr WParam, ref PInvokeHdItem LParam);

    // Releases the GDI handle produced by Bitmap.GetHbitmap() - required after
    // Imaging.CreateBitmapSourceFromHBitmap to avoid leaking GDI objects.
    [DllImport("gdi32.dll")]
    public static extern bool DeleteObject(IntPtr Object);

    // GetActiveWindow() is thread-queue-scoped and proved unreliable right after a menu click
    // (still returning the app instead of the just-closed menu's own transient window in some
    // cases) - GetForegroundWindow() is the OS-wide "what does the user see as active" query and
    // is used as the fallback when no WinForms Form is active (e.g. a WPF window is on top).
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();
}

[StructLayout(LayoutKind.Sequential)]
internal struct PInvokeHdItem
{
    public int Mask;
    public int Cxy;
    public IntPtr PszText;
    public IntPtr Hbm;
    public int CchTextMax;
    public int Fmt;
    public IntPtr LParam;
    public int IImage;
    public int IOrder;
    public int Type;
    public IntPtr PvFilter;
    public int State;
}

[StructLayout(LayoutKind.Sequential)]
internal struct PInvokeTokenPrivileges
{
    public uint PrivilegeCount;
    public PInvokeLuid LUID;
    public uint Attributes;
}

[StructLayout(LayoutKind.Sequential)]
internal struct PInvokeLuid
{
    public uint LowPart;
    public int HighPart;
}

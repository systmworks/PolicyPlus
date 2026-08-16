using System;
using System.Runtime.InteropServices;

internal static class PInvoke
{
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

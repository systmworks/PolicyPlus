using System;
using System.Runtime.InteropServices;

public static class Privilege
{
    // Enable the given Win32 privilege
    public static void EnablePrivilege(string Name)
    {
        PInvoke.OpenProcessToken(PInvoke.GetCurrentProcess(), 0x28, out IntPtr thisProcToken); // 0x28 = TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY
        PInvoke.LookupPrivilegeValueW(null, Name, out PInvokeLuid luid);
        var priv = new PInvokeTokenPrivileges
        {
            Attributes = 2, // SE_PRIVILEGE_ENABLED
            PrivilegeCount = 1,
            LUID = luid
        };
        PInvoke.AdjustTokenPrivileges(thisProcToken, false, ref priv, (uint)Marshal.SizeOf(priv), IntPtr.Zero, out _);
        PInvoke.CloseHandle(thisProcToken);
    }
}

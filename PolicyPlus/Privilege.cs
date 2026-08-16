using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

public static class Privilege
{
    // Enable the given Win32 privilege
    public static void EnablePrivilege(string Name)
    {
        if (!PInvoke.OpenProcessToken(PInvoke.GetCurrentProcess(), 0x28, out IntPtr thisProcToken)) // 0x28 = TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY
            throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            if (!PInvoke.LookupPrivilegeValueW(null, Name, out PInvokeLuid luid))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            var priv = new PInvokeTokenPrivileges
            {
                Attributes = 2, // SE_PRIVILEGE_ENABLED
                PrivilegeCount = 1,
                LUID = luid
            };
            if (!PInvoke.AdjustTokenPrivileges(thisProcToken, false, ref priv, (uint)Marshal.SizeOf(priv), IntPtr.Zero, out _))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally
        {
            PInvoke.CloseHandle(thisProcToken);
        }
    }
}

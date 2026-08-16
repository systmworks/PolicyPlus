using System;
using System.Windows.Forms;

// Thin wrapper preserving Interaction.MsgBox's implicit "Policy Plus" title, so call sites
// converting from the VB MsgBox convention only need to swap style flags for buttons/icon.
public static class MsgBoxCompat
{
    // MessageBox.Show(text, ...) with no owner centers on the screen, not the app - re-queries
    // the active window's handle on every call so it works whether the active window is a
    // WinForms Form or a WPF Window.
    private sealed class ActiveWindowOwner : IWin32Window
    {
        public IntPtr Handle => PInvoke.GetActiveWindow();
    }

    private static readonly IWin32Window Owner = new ActiveWindowOwner();

    public static DialogResult Show(string text, MessageBoxButtons buttons, MessageBoxIcon icon) =>
        MessageBox.Show(Owner, text, "Policy Plus", buttons, icon);
}

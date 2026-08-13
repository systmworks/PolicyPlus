using System.Windows.Forms;

// Thin wrapper preserving Interaction.MsgBox's implicit "Policy Plus" title, so call sites
// converting from the VB MsgBox convention only need to swap style flags for buttons/icon.
public static class MsgBoxCompat
{
    public static DialogResult Show(string text, MessageBoxButtons buttons, MessageBoxIcon icon) =>
        MessageBox.Show(text, "Policy Plus", buttons, icon);
}

using System.Linq;
using Wpf.Ui.Controls;

public enum MsgBoxButtons
{
    OK,
    YesNo,
    YesNoCancel,
}

public enum MsgBoxIcon
{
    None,
    Information,
    Warning,
    Question,
}

public enum MsgBoxResult
{
    OK,
    Yes,
    No,
    Cancel,
}

// Thin wrapper around WPF-UI's Fluent MessageBox control, preserving the implicit "Policy
// Plus" title and a simple Show(text, buttons, icon) call convention so the ~65 call sites
// converted from Interaction.MsgBox didn't need reshaping.
public static class MsgBoxCompat
{
    public static MsgBoxResult Show(string text, MsgBoxButtons buttons, MsgBoxIcon icon = MsgBoxIcon.None)
    {
        var box = new MessageBox
        {
            Title = "Policy Plus",
            Content = text,
            Owner = ActiveWindow(),
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
            // A Warning/Exclamation icon in the old API meant "this needs the user's
            // attention/caution" - carried into the Fluent idiom as a Danger-colored primary
            // button rather than a separate icon glyph.
            PrimaryButtonAppearance = icon == MsgBoxIcon.Warning ? ControlAppearance.Danger : ControlAppearance.Primary,
        };

        switch (buttons)
        {
            case MsgBoxButtons.OK:
                box.PrimaryButtonText = "OK";
                box.IsCloseButtonEnabled = false;
                break;
            case MsgBoxButtons.YesNo:
                box.PrimaryButtonText = "Yes";
                box.SecondaryButtonText = "No";
                box.IsCloseButtonEnabled = false;
                break;
            case MsgBoxButtons.YesNoCancel:
                box.PrimaryButtonText = "Yes";
                box.SecondaryButtonText = "No";
                box.CloseButtonText = "Cancel";
                break;
        }

        // Safe to block synchronously here: ShowDialogAsync's implementation calls the base
        // Window.ShowDialog() (a blocking, message-pumping call) before its own first await,
        // so by the time that await is reached the result is already available and the
        // returned Task completes synchronously - no risk of deadlocking the UI thread.
        var result = box.ShowDialogAsync().GetAwaiter().GetResult();

        return buttons switch
        {
            MsgBoxButtons.YesNo => result == MessageBoxResult.Primary ? MsgBoxResult.Yes : MsgBoxResult.No,
            MsgBoxButtons.YesNoCancel => result switch
            {
                MessageBoxResult.Primary => MsgBoxResult.Yes,
                MessageBoxResult.Secondary => MsgBoxResult.No,
                _ => MsgBoxResult.Cancel,
            },
            _ => MsgBoxResult.OK,
        };
    }

    // WPF equivalent of WinForms' Form.ActiveForm: the currently-active top-level window, so
    // the message box centers on and stays modal to whichever window the user is looking at.
    private static System.Windows.Window ActiveWindow() =>
        System.Windows.Application.Current.Windows.OfType<System.Windows.Window>().FirstOrDefault(w => w.IsActive)
            ?? System.Windows.Application.Current.MainWindow;
}

using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace PolicyPlus.Views
{
    public partial class AboutWindow : FluentWindow
    {
        public AboutWindow()
        {
            InitializeComponent();
            WpfInterop.FixSizeToContent(this);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_KeyDown(object sender, KeyEventArgs e) => WpfInterop.HandleEscapeToClose(this, e);

        public static void PresentDialog(System.Windows.Window owner)
        {
            ThemeService.ApplyPersisted();
            var window = new AboutWindow();

            string version = VersionHolder.AppVersion.Trim();
            window.VersionText.Text = string.IsNullOrEmpty(version)
                ? ""
                : $"Version {version} (commit {VersionHolder.Version.Trim()})";
            window.VersionText.Visibility = string.IsNullOrEmpty(version) ? Visibility.Collapsed : Visibility.Visible;

            WpfInterop.SetOwner(window, owner);
            window.ShowDialog();
        }
    }
}

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
            var window = WpfInterop.PreparePresented(new AboutWindow(), owner);

            string version = VersionHolder.AppVersion.Trim();
            window.VersionText.Text = string.IsNullOrEmpty(version)
                ? ""
                : $"Version {version} (commit {VersionHolder.Version.Trim()})";
            window.VersionText.Visibility = string.IsNullOrEmpty(version) ? Visibility.Collapsed : Visibility.Visible;

            window.ShowDialog();
        }
    }
}

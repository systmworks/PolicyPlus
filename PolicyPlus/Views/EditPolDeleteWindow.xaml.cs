using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace PolicyPlus.Views
{
    public partial class EditPolDeleteWindow : FluentWindow
    {
        private bool _accepted;

        public EditPolDeleteWindow()
        {
            InitializeComponent();
            WpfInterop.FixSizeToContent(this);
        }

        private void OptDeleteOne_CheckedChanged(object sender, RoutedEventArgs e)
        {
            TextValueName.IsEnabled = OptDeleteOne.IsChecked == true;
        }

        private void ButtonOK_Click(object sender, RoutedEventArgs e)
        {
            if (OptDeleteOne.IsChecked == true && string.IsNullOrEmpty(TextValueName.Text))
            {
                MsgBoxCompat.Show("You must enter a value name.", MsgBoxButtons.OK, MsgBoxIcon.Warning);
                return;
            }

            if (OptPurge.IsChecked == true || OptClearFirst.IsChecked == true || OptDeleteOne.IsChecked == true)
            {
                _accepted = true;
                Close();
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }

        public static (bool Purge, bool ClearFirst, string ValueName)? PresentDialog(System.Windows.Window owner, string containerKey)
        {
            ThemeService.ApplyPersisted();
            var window = new EditPolDeleteWindow { TextKey = { Text = containerKey } };
            WpfInterop.SetOwner(window, owner);
            window.ShowDialog();
            if (!window._accepted)
            {
                return null;
            }

            return (window.OptPurge.IsChecked == true, window.OptClearFirst.IsChecked == true, window.TextValueName.Text);
        }
    }
}

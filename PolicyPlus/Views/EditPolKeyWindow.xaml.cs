using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace PolicyPlus.Views
{
    public partial class EditPolKeyWindow : FluentWindow
    {
        private bool _accepted;

        public EditPolKeyWindow()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                TextName.Focus();
                TextName.SelectAll();
            };
        }

        private void ButtonOK_Click(object sender, RoutedEventArgs e)
        {
            _accepted = true;
            Close();
        }

        private void TextName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
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

        public static string PresentDialog(System.Windows.Forms.IWin32Window owner, string initialName)
        {
            ThemeService.ApplyPersisted();
            var window = new EditPolKeyWindow { TextName = { Text = initialName } };
            WpfInterop.SetOwner(window, owner);
            window.ShowDialog();
            return window._accepted ? window.TextName.Text : "";
        }
    }
}

using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace PolicyPlus.Views
{
    public partial class EditPolStringDataWindow : FluentWindow
    {
        private bool _accepted;

        public EditPolStringDataWindow()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                TextData.Focus();
                TextData.SelectAll();
            };
        }

        private void ButtonOK_Click(object sender, RoutedEventArgs e)
        {
            _accepted = true;
            Close();
        }

        private void TextData_KeyDown(object sender, KeyEventArgs e)
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

        public static string PresentDialog(System.Windows.Forms.IWin32Window owner, string valueName, string initialData)
        {
            ThemeService.ApplyPersisted();
            var window = new EditPolStringDataWindow
            {
                TextName = { Text = valueName },
                TextData = { Text = initialData },
            };
            WpfInterop.SetOwner(window, owner);
            window.ShowDialog();
            return window._accepted ? window.TextData.Text : null;
        }
    }
}

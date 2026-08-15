using System;
using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace PolicyPlus.Views
{
    public partial class EditPolMultiStringDataWindow : FluentWindow
    {
        private bool _accepted;

        public EditPolMultiStringDataWindow()
        {
            InitializeComponent();
            Loaded += (s, e) => TextData.Focus();
        }

        private void ButtonOK_Click(object sender, RoutedEventArgs e)
        {
            _accepted = true;
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }

        public static string[] PresentDialog(System.Windows.Forms.IWin32Window owner, string valueName, string[] initialData)
        {
            ThemeService.ApplyPersisted();
            var window = new EditPolMultiStringDataWindow
            {
                TextName = { Text = valueName },
                TextData = { Text = string.Join(Environment.NewLine, initialData ?? Array.Empty<string>()) },
            };
            WpfInterop.SetOwner(window, owner);
            window.ShowDialog();
            return window._accepted ? window.TextData.Text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None) : null;
        }
    }
}

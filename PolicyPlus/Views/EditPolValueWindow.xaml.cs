using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Wpf.Ui.Controls;

namespace PolicyPlus.Views
{
    public partial class EditPolValueWindow : FluentWindow
    {
        private bool _accepted;

        private static readonly RegistryValueKind[] Kinds =
        {
            RegistryValueKind.String,
            RegistryValueKind.ExpandString,
            RegistryValueKind.MultiString,
            RegistryValueKind.DWord,
            RegistryValueKind.QWord,
        };

        public EditPolValueWindow()
        {
            InitializeComponent();
            Loaded += (s, e) => TextName.Focus();
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

        public static (string Name, RegistryValueKind Kind)? PresentDialog(System.Windows.Window owner)
        {
            ThemeService.ApplyPersisted();
            var window = new EditPolValueWindow();
            WpfInterop.SetOwner(window, owner);
            window.ShowDialog();
            if (!window._accepted)
            {
                return null;
            }

            return (window.TextName.Text, Kinds[window.ComboKind.SelectedIndex]);
        }
    }
}

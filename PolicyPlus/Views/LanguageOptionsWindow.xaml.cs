using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace PolicyPlus.Views
{
    public partial class LanguageOptionsWindow : FluentWindow
    {
        private string _originalLanguage;
        private string _newLanguage;

        public LanguageOptionsWindow()
        {
            InitializeComponent();
        }

        private void ButtonOK_Click(object sender, RoutedEventArgs e)
        {
            string selection = TextAdmlLanguage.Text.Trim();
            if (selection.Split('-').Length != 2)
            {
                MsgBoxCompat.Show("Please enter a valid language code.", MsgBoxButtons.OK, MsgBoxIcon.Warning);
                return;
            }

            if (selection != (_originalLanguage ?? ""))
            {
                _newLanguage = selection;
            }

            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }

        public static string PresentDialog(System.Windows.Forms.IWin32Window owner, string currentLanguage)
        {
            ThemeService.ApplyPersisted();
            var window = new LanguageOptionsWindow { _originalLanguage = currentLanguage, TextAdmlLanguage = { Text = currentLanguage } };
            WpfInterop.SetOwner(window, owner);
            window.ShowDialog();
            return window._newLanguage;
        }
    }
}

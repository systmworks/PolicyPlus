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
            WpfInterop.FixSizeToContent(this);
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

        private void Window_KeyDown(object sender, KeyEventArgs e) => WpfInterop.HandleEscapeToClose(this, e);

        public static string PresentDialog(System.Windows.Window owner, string valueName, string initialData)
        {
            var window = WpfInterop.PreparePresented(new EditPolStringDataWindow
            {
                TextName = { Text = valueName },
                TextData = { Text = initialData },
            }, owner);
            window.ShowDialog();
            return window._accepted ? window.TextData.Text : null;
        }
    }
}

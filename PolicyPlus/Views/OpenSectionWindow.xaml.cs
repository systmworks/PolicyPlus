using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace PolicyPlus.Views
{
    public partial class OpenSectionWindow : FluentWindow
    {
        private AdmxPolicySection? _selectedSection;

        public OpenSectionWindow()
        {
            InitializeComponent();
            WpfInterop.FixSizeToContent(this);
        }

        private void ButtonOK_Click(object sender, RoutedEventArgs e)
        {
            if (OptUser.IsChecked == true || OptComputer.IsChecked == true)
            {
                _selectedSection = OptUser.IsChecked == true ? AdmxPolicySection.User : AdmxPolicySection.Machine;
                Close();
            }
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e) => WpfInterop.HandleEscapeToClose(this, e);

        public static AdmxPolicySection? PresentDialog(System.Windows.Window owner, bool userEnabled, bool compEnabled)
        {
            var window = WpfInterop.PreparePresented(new OpenSectionWindow
            {
                OptUser = { IsEnabled = userEnabled },
                OptComputer = { IsEnabled = compEnabled },
            }, owner);
            window.ShowDialog();
            return window._selectedSection;
        }
    }
}

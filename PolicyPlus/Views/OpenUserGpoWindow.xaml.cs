using System;
using System.Security.Principal;
using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace PolicyPlus.Views
{
    public partial class OpenUserGpoWindow : FluentWindow
    {
        private bool _accepted;

        public OpenUserGpoWindow()
        {
            InitializeComponent();
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var userAccount = new NTAccount(UsernameTextbox.Text);
                var sid = (SecurityIdentifier)userAccount.Translate(typeof(SecurityIdentifier));
                SidTextbox.Text = sid.ToString();
            }
            catch (Exception)
            {
                MsgBoxCompat.Show("The name could not be translated to a SID.", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Exclamation);
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(SidTextbox.Text) && !string.IsNullOrEmpty(UsernameTextbox.Text))
            {
                SearchButton_Click(null, null);
                if (string.IsNullOrEmpty(SidTextbox.Text))
                {
                    return;
                }
            }

            try
            {
                _ = new SecurityIdentifier(SidTextbox.Text);
            }
            catch (Exception)
            {
                MsgBoxCompat.Show("The SID is not valid. Enter a SID in the lower box, or enter a username in the top box and press Search to translate.", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Exclamation);
                return;
            }

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

        public static string PresentDialog(System.Windows.Forms.IWin32Window owner)
        {
            ThemeService.ApplyPersisted();
            var window = new OpenUserGpoWindow();
            WpfInterop.SetOwner(window, owner);
            window.ShowDialog();
            return window._accepted ? window.SidTextbox.Text : null;
        }
    }
}

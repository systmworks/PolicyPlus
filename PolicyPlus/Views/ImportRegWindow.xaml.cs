using System;
using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace PolicyPlus.Views
{
    public partial class ImportRegWindow : FluentWindow
    {
        private IPolicySource _policySource;
        private bool _accepted;

        public ImportRegWindow()
        {
            InitializeComponent();
        }

        private void ButtonBrowse_Click(object sender, RoutedEventArgs e)
        {
            using var ofd = new System.Windows.Forms.OpenFileDialog { Filter = "Registry scripts|*.reg" };
            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            TextReg.Text = ofd.FileName;
            if (string.IsNullOrEmpty(TextRoot.Text))
            {
                try
                {
                    var reg = RegFile.Load(ofd.FileName, "");
                    TextRoot.Text = reg.GuessPrefix();
                    if (reg.HasDefaultValues())
                    {
                        MsgBoxCompat.Show("This REG file contains data for default values, which cannot be applied to all policy sources.", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Exclamation);
                    }
                }
                catch (Exception)
                {
                    MsgBoxCompat.Show("An error occurred while trying to guess the prefix.", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Exclamation);
                }
            }
        }

        private void ButtonImport_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TextReg.Text))
            {
                MsgBoxCompat.Show("Please specify a REG file to import.", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Exclamation);
                return;
            }

            if (string.IsNullOrEmpty(TextRoot.Text))
            {
                MsgBoxCompat.Show("Please specify the prefix used to fully qualify paths in the REG file.", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Exclamation);
                return;
            }

            try
            {
                var reg = RegFile.Load(TextReg.Text, TextRoot.Text);
                reg.Apply(_policySource);
                _accepted = true;
                Close();
            }
            catch (Exception)
            {
                MsgBoxCompat.Show("Failed to import the REG file.", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Exclamation);
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }

        public static bool PresentDialog(System.Windows.Forms.IWin32Window owner, IPolicySource target)
        {
            ThemeService.ApplyPersisted();
            var window = new ImportRegWindow { _policySource = target };
            WpfInterop.SetOwner(window, owner);
            window.ShowDialog();
            return window._accepted;
        }
    }
}

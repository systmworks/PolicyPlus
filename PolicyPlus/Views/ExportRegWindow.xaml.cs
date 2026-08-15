using System;
using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace PolicyPlus.Views
{
    public partial class ExportRegWindow : FluentWindow
    {
        private PolFile _source;

        public ExportRegWindow()
        {
            InitializeComponent();
        }

        private void ButtonBrowse_Click(object sender, RoutedEventArgs e)
        {
            using var sfd = new System.Windows.Forms.SaveFileDialog { Filter = "Registry scripts|*.reg" };
            if (sfd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                TextReg.Text = sfd.FileName;
            }
        }

        private void ButtonExport_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TextReg.Text))
            {
                MsgBoxCompat.Show("Please specify a filename and path for the exported REG.", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Exclamation);
                return;
            }

            var reg = new RegFile();
            reg.SetPrefix(TextRoot.Text);
            reg.SetSourceBranch(TextBranch.Text);
            try
            {
                _source.Apply(reg);
                reg.Save(TextReg.Text);
                MsgBoxCompat.Show("REG exported successfully.", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
                Close();
            }
            catch (Exception)
            {
                MsgBoxCompat.Show("Failed to export REG!", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Exclamation);
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }

        public static void PresentDialog(System.Windows.Forms.IWin32Window owner, string branch, PolFile pol, bool isUser)
        {
            ThemeService.ApplyPersisted();
            var window = new ExportRegWindow
            {
                _source = pol,
                TextBranch = { Text = branch },
                TextRoot = { Text = isUser ? @"HKEY_CURRENT_USER\" : @"HKEY_LOCAL_MACHINE\" },
            };
            WpfInterop.SetOwner(window, owner);
            window.ShowDialog();
        }
    }
}

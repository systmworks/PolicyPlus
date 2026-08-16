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
            WpfInterop.FixSizeToContent(this);
        }

        private void ButtonBrowse_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new Microsoft.Win32.SaveFileDialog { Filter = "Registry scripts|*.reg" };
            if (sfd.ShowDialog() == true)
            {
                TextReg.Text = sfd.FileName;
            }
        }

        private void ButtonExport_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TextReg.Text))
            {
                MsgBoxCompat.Show("Please specify a filename and path for the exported REG.", MsgBoxButtons.OK, MsgBoxIcon.Warning);
                return;
            }

            var reg = new RegFile();
            reg.SetPrefix(TextRoot.Text);
            reg.SetSourceBranch(TextBranch.Text);
            try
            {
                _source.Apply(reg);
                reg.Save(TextReg.Text);
                MsgBoxCompat.Show("REG exported successfully.", MsgBoxButtons.OK, MsgBoxIcon.Information);
                Close();
            }
            catch (Exception)
            {
                MsgBoxCompat.Show("Failed to export REG!", MsgBoxButtons.OK, MsgBoxIcon.Warning);
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e) => WpfInterop.HandleEscapeToClose(this, e);

        public static void PresentDialog(System.Windows.Window owner, string branch, PolFile pol, bool isUser)
        {
            var window = WpfInterop.PreparePresented(new ExportRegWindow
            {
                _source = pol,
                TextBranch = { Text = branch },
                TextRoot = { Text = isUser ? @"HKEY_CURRENT_USER\" : @"HKEY_LOCAL_MACHINE\" },
            }, owner);
            window.ShowDialog();
        }
    }
}

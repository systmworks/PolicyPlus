using System;
using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace PolicyPlus.Views
{
    public partial class ImportSpolWindow : FluentWindow
    {
        private SpolFile _spol;

        public ImportSpolWindow()
        {
            InitializeComponent();
            TextSpol.Text = "Policy Plus Semantic Policy\r\n\r\n";
        }

        private void ButtonOpenFile_Click(object sender, RoutedEventArgs e)
        {
            using var ofd = new System.Windows.Forms.OpenFileDialog { Filter = "Semantic Policy files|*.spol|All files|*.*" };
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                TextSpol.Text = System.IO.File.ReadAllText(ofd.FileName);
            }
        }

        private void ButtonVerify_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var spol = SpolFile.FromText(TextSpol.Text);
                MsgBoxCompat.Show("Validation successful, " + spol.Policies.Count + " policy settings found.", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MsgBoxCompat.Show("SPOL validation failed: " + ex.Message, System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Exclamation);
            }
        }

        private void ButtonApply_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _spol = SpolFile.FromText(TextSpol.Text);
                Close();
            }
            catch (Exception ex)
            {
                MsgBoxCompat.Show("The SPOL text is invalid: " + ex.Message, System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Exclamation);
            }
        }

        private void ButtonReset_Click(object sender, RoutedEventArgs e)
        {
            if (MsgBoxCompat.Show("Are you sure you want to reset the text box?", System.Windows.Forms.MessageBoxButtons.YesNo, System.Windows.Forms.MessageBoxIcon.Question) == System.Windows.Forms.DialogResult.Yes)
            {
                TextSpol.Text = "Policy Plus Semantic Policy\r\n\r\n";
            }
        }

        private void TextSpol_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control)
            {
                TextSpol.SelectAll();
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && !(TextSpol.IsFocused && TextSpol.SelectionLength > 0))
            {
                Close();
            }
        }

        public static SpolFile PresentDialog(System.Windows.Forms.IWin32Window owner)
        {
            ThemeService.ApplyPersisted();
            var window = new ImportSpolWindow();
            WpfInterop.SetOwner(window, owner);
            window.ShowDialog();
            return window._spol;
        }
    }
}

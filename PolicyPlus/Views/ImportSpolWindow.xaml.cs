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
            var ofd = new Microsoft.Win32.OpenFileDialog { Filter = "Semantic Policy files|*.spol|All files|*.*" };
            if (ofd.ShowDialog() == true)
            {
                TextSpol.Text = System.IO.File.ReadAllText(ofd.FileName);
            }
        }

        private void ButtonVerify_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var spol = SpolFile.FromText(TextSpol.Text);
                MsgBoxCompat.Show("Validation successful, " + spol.Policies.Count + " policy settings found.", MsgBoxButtons.OK, MsgBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MsgBoxCompat.Show("SPOL validation failed: " + ex.Message, MsgBoxButtons.OK, MsgBoxIcon.Warning);
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
                MsgBoxCompat.Show("The SPOL text is invalid: " + ex.Message, MsgBoxButtons.OK, MsgBoxIcon.Warning);
            }
        }

        private void ButtonReset_Click(object sender, RoutedEventArgs e)
        {
            if (MsgBoxCompat.Show("Are you sure you want to reset the text box?", MsgBoxButtons.YesNo, MsgBoxIcon.Question) == MsgBoxResult.Yes)
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

        public static SpolFile PresentDialog(System.Windows.Window owner)
        {
            ThemeService.ApplyPersisted();
            var window = new ImportSpolWindow();
            WpfInterop.SetOwner(window, owner);
            window.ShowDialog();
            return window._spol;
        }
    }
}

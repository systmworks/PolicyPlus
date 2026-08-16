using System;
using System.DirectoryServices.ActiveDirectory;
using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace PolicyPlus.Views
{
    public partial class OpenAdmxFolderWindow : FluentWindow
    {
        private string _sysvolPolicyDefinitionsPath = "";
        private bool _accepted;
        private string _selectedFolder;

        public OpenAdmxFolderWindow()
        {
            InitializeComponent();
            WpfInterop.FixSizeToContent(this);
            Loaded += OpenAdmxFolderWindow_Loaded;
        }

        private void OpenAdmxFolderWindow_Loaded(object sender, RoutedEventArgs e)
        {
            OptCustomFolder.IsChecked = true;
            Domain compDomain = null;
            try
            {
                compDomain = Domain.GetComputerDomain();
            }
            catch (Exception)
            {
                // Not domain-joined, or no domain controller is available
            }

            if (compDomain is null)
            {
                _sysvolPolicyDefinitionsPath = "";
            }
            else
            {
                string possiblePath = @"\\" + compDomain.Name + @"\SYSVOL\" + compDomain.Name + @"\Policies\PolicyDefinitions";
                _sysvolPolicyDefinitionsPath = System.IO.Directory.Exists(possiblePath) ? possiblePath : "";
            }

            OptSysvol.IsEnabled = !string.IsNullOrEmpty(_sysvolPolicyDefinitionsPath);
        }

        private void Options_CheckedChanged(object sender, RoutedEventArgs e)
        {
            bool customSelected = OptCustomFolder.IsChecked == true;
            TextFolder.IsEnabled = customSelected;
        }

        private void ButtonOK_Click(object sender, RoutedEventArgs e)
        {
            if (OptLocalFolder.IsChecked == true)
            {
                _selectedFolder = Environment.ExpandEnvironmentVariables(@"%windir%\PolicyDefinitions");
            }
            else if (OptSysvol.IsChecked == true)
            {
                _selectedFolder = _sysvolPolicyDefinitionsPath;
            }
            else if (OptCustomFolder.IsChecked == true)
            {
                _selectedFolder = TextFolder.Text;
            }

            if (System.IO.Directory.Exists(_selectedFolder))
            {
                _accepted = true;
                Close();
            }
            else
            {
                MsgBoxCompat.Show("The folder you specified does not exist.", MsgBoxButtons.OK, MsgBoxIcon.Warning);
            }
        }

        private void ButtonBrowse_Click(object sender, RoutedEventArgs e)
        {
            var fbd = new Microsoft.Win32.OpenFolderDialog();
            if (fbd.ShowDialog() != true)
            {
                return;
            }

            TextFolder.Text = fbd.FolderName;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e) => WpfInterop.HandleEscapeToClose(this, e);

        public static (string Folder, bool ClearWorkspace)? PresentDialog(System.Windows.Window owner)
        {
            ThemeService.ApplyPersisted();
            var window = new OpenAdmxFolderWindow();
            WpfInterop.SetOwner(window, owner);
            window.ShowDialog();
            return window._accepted ? (window._selectedFolder, window.ClearWorkspaceCheckbox.IsChecked == true) : null;
        }
    }
}

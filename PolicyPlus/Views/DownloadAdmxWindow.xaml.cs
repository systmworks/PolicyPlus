using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.AccessControl;
using System.Threading.Tasks;
using System.Windows;
using Wpf.Ui.Controls;

namespace PolicyPlus.Views
{
    public partial class DownloadAdmxWindow : FluentWindow
    {
        private const string MicrosoftMsiDownloadLink = "https://download.microsoft.com/download/31897b32-df9e-4585-b5bb-442f1f444c92/Administrative%20Templates%20(.admx)%20for%20Windows%2011%20Oct%202025%20Update.msi";
        private const string PolicyDefinitionsMsiSubdirectory = @"\Microsoft Group Policy\Windows 11 Oct 2025 Update (25H2)\PolicyDefinitions";
        private bool _downloading;
        private string _newPolicySourceFolder;

        public DownloadAdmxWindow()
        {
            InitializeComponent();
            WpfInterop.FixSizeToContent(this);
            Loaded += (s, e) =>
            {
                TextDestFolder.Text = Environment.ExpandEnvironmentVariables(@"%windir%\PolicyDefinitions");
                _newPolicySourceFolder = "";
                SetIsBusy(false);
            };
        }

        private void ButtonBrowse_Click(object sender, RoutedEventArgs e)
        {
            var fbd = new Microsoft.Win32.OpenFolderDialog();
            if (fbd.ShowDialog() == true)
            {
                TextDestFolder.Text = fbd.FolderName;
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (_downloading)
            {
                e.Cancel = true;
            }
        }

        private void ButtonClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SetIsBusy(bool busy)
        {
            _downloading = busy;
            LabelProgress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            ButtonClose.IsEnabled = !busy;
            ButtonStart.IsEnabled = !busy;
            TextDestFolder.IsEnabled = !busy;
            ButtonBrowse.IsEnabled = !busy;
            ProgressSpinner.IsIndeterminate = busy;
        }

        private void ButtonStart_Click(object sender, RoutedEventArgs e)
        {
            void SetProgress(string progress) => Dispatcher.Invoke(() => LabelProgress.Text = progress);
            LabelProgress.Text = "";
            SetIsBusy(true);
            string destination = TextDestFolder.Text;
            bool isAdmin;
            using (var identity = System.Security.Principal.WindowsIdentity.GetCurrent())
            {
                isAdmin = new System.Security.Principal.WindowsPrincipal(identity).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }

            void TakeOwnership(string folder)
            {
                var folderInfo = new DirectoryInfo(folder);
                var dacl = folderInfo.GetAccessControl();
                var adminSid = new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.BuiltinAdministratorsSid, null);
                dacl.SetOwner(adminSid);
                folderInfo.SetAccessControl(dacl);
                dacl = folderInfo.GetAccessControl();
                var allowRule = new FileSystemAccessRule(adminSid, FileSystemRights.FullControl, AccessControlType.Allow);
                dacl.AddAccessRule(allowRule);
                folderInfo.SetAccessControl(dacl);
            }

            void MoveFilesInDir(string source, string dest, bool inheritAcl)
            {
                bool creatingNew = !Directory.Exists(dest);
                Directory.CreateDirectory(dest);
                if (isAdmin)
                {
                    if (creatingNew & inheritAcl)
                    {
                        var dirAcl = new DirectorySecurity();
                        dirAcl.SetAccessRuleProtection(false, true);
                        var destInfo = new DirectoryInfo(dest);
                        destInfo.SetAccessControl(dirAcl);
                    }
                    else if (!creatingNew)
                    {
                        TakeOwnership(dest);
                    }
                }

                foreach (var file in Directory.EnumerateFiles(source))
                {
                    string plainFilename = Path.GetFileName(file);
                    string newName = Path.Combine(dest, plainFilename);
                    if (File.Exists(newName))
                    {
                        File.Delete(newName);
                    }

                    File.Move(file, newName);
                    if (isAdmin)
                    {
                        var fileAcl = new FileSecurity();
                        fileAcl.SetAccessRuleProtection(false, true);
                        var newFileInfo = new FileInfo(newName);
                        newFileInfo.SetAccessControl(fileAcl);
                    }
                }
            }

            Task.Factory.StartNew(() =>
            {
                string failPhase = "create a scratch space";
                try
                {
                    string tempPath = Environment.ExpandEnvironmentVariables(@"%localappdata%\PolicyPlusAdmxDownload\");
                    Directory.CreateDirectory(tempPath);
                    failPhase = "download the package";
                    SetProgress("Downloading MSI from Microsoft...");
                    string downloadPath = tempPath + "W11Admx.msi";
                    using (var webcli = new System.Net.WebClient())
                    {
                        webcli.DownloadFile(MicrosoftMsiDownloadLink, downloadPath);
                    }

                    failPhase = "extract the package";
                    SetProgress("Unpacking MSI...");
                    string unpackPath = tempPath + "MsiUnpack";
                    var proc = Process.Start("msiexec", "/a \"" + downloadPath + "\" /quiet /qn TARGETDIR=\"" + unpackPath + "\"");
                    proc.WaitForExit();
                    if (proc.ExitCode != 0)
                    {
                        throw new Exception(); // msiexec failed
                    }

                    File.Delete(downloadPath);
                    if (Directory.Exists(destination) & isAdmin)
                    {
                        failPhase = "take control of the destination";
                        SetProgress("Securing destination...");
                        Privilege.EnablePrivilege("SeTakeOwnershipPrivilege");
                        Privilege.EnablePrivilege("SeRestorePrivilege");
                        TakeOwnership(destination);
                    }

                    failPhase = "move the ADMX files";
                    SetProgress("Moving files to destination...");
                    string unpackedDefsPath = unpackPath + PolicyDefinitionsMsiSubdirectory;
                    string langSubfolder = System.Globalization.CultureInfo.CurrentCulture.Name;
                    MoveFilesInDir(unpackedDefsPath, destination, false);
                    string sourceAdmlPath = unpackedDefsPath + @"\" + langSubfolder;
                    if (Directory.Exists(sourceAdmlPath))
                    {
                        MoveFilesInDir(sourceAdmlPath, destination + @"\" + langSubfolder, true);
                    }

                    if (langSubfolder != "en-US")
                    {
                        // Also copy the English language files as a fallback
                        MoveFilesInDir(unpackedDefsPath + @"\en-US", destination + @"\en-US", true);
                    }

                    failPhase = "remove temporary files";
                    SetProgress("Cleaning up...");
                    Directory.Delete(tempPath, true);
                    SetProgress("Done.");
                    Dispatcher.Invoke(() =>
                    {
                        SetIsBusy(false);
                        if (MsgBoxCompat.Show("ADMX files downloaded successfully. Open them now?", MsgBoxButtons.YesNo, MsgBoxIcon.Question) == MsgBoxResult.Yes)
                        {
                            _newPolicySourceFolder = destination;
                        }

                        Close();
                    });
                }
                catch (Exception)
                {
                    Dispatcher.Invoke(() =>
                    {
                        SetIsBusy(false);
                        MsgBoxCompat.Show("Failed to " + failPhase + ".", MsgBoxButtons.OK, MsgBoxIcon.Warning);
                    });
                }
            });
        }

        public static string PresentDialog(System.Windows.Forms.IWin32Window owner)
        {
            ThemeService.ApplyPersisted();
            var window = new DownloadAdmxWindow();
            WpfInterop.SetOwner(window, owner);
            window.ShowDialog();
            return window._newPolicySourceFolder;
        }
    }
}

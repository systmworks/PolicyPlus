using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace PolicyPlus.Views
{
    public partial class OpenUserRegistryWindow : FluentWindow
    {
        private class Row
        {
            public string Folder;
            public string Access;
        }

        private string _selectedFilePath;

        public OpenUserRegistryWindow()
        {
            InitializeComponent();
            Loaded += OpenUserRegistryWindow_Loaded;
        }

        private void OpenUserRegistryWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var rows = new List<Row>();
            bool canMountHives = new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent()).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            foreach (var folder in System.IO.Directory.EnumerateDirectories(@"C:\Users"))
            {
                var dirInfo = new System.IO.DirectoryInfo(folder);
                if ((int)(dirInfo.Attributes & System.IO.FileAttributes.ReparsePoint) > 0)
                {
                    continue;
                }

                string ntuserPath = folder + @"\ntuser.dat";
                string access;
                try
                {
                    using (var fNtuser = new System.IO.FileStream(ntuserPath, System.IO.FileMode.Open, System.IO.FileAccess.ReadWrite))
                    {
                        access = canMountHives ? "Yes" : "No (unprivileged)";
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    access = "No";
                }
                catch (System.IO.FileNotFoundException)
                {
                    access = "";
                }
                catch (Exception)
                {
                    access = "No (in use)";
                }

                if (!string.IsNullOrEmpty(access))
                {
                    rows.Add(new Row { Folder = System.IO.Path.GetFileName(folder), Access = access });
                }
            }

            SubfoldersListview.ItemsSource = rows;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (SubfoldersListview.SelectedItem is not Row row)
            {
                return;
            }

            _selectedFilePath = System.IO.Path.Combine(@"C:\Users", row.Folder, "ntuser.dat");
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
            var window = new OpenUserRegistryWindow();
            WpfInterop.SetOwner(window, owner);
            window.ShowDialog();
            return window._selectedFilePath;
        }
    }
}

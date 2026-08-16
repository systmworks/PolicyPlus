using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace PolicyPlus.Views
{
    public partial class OpenPolWindow : FluentWindow
    {
        private PolicyLoader _selectedUser, _selectedComputer;
        private bool _accepted;

        public OpenPolWindow()
        {
            InitializeComponent();
        }

        private void SetLastSources(PolicyLoaderSource compType, string compData, PolicyLoaderSource userType, string userData)
        {
            switch (compType)
            {
                case PolicyLoaderSource.LocalGpo:
                    CompLocalOption.IsChecked = true;
                    break;
                case PolicyLoaderSource.LocalRegistry:
                    CompRegistryOption.IsChecked = true;
                    CompRegTextbox.Text = compData;
                    break;
                case PolicyLoaderSource.PolFile:
                    CompFileOption.IsChecked = true;
                    CompPolFilenameTextbox.Text = compData;
                    break;
                case PolicyLoaderSource.Null:
                    CompNullOption.IsChecked = true;
                    break;
            }

            switch (userType)
            {
                case PolicyLoaderSource.LocalGpo:
                    UserLocalOption.IsChecked = true;
                    break;
                case PolicyLoaderSource.LocalRegistry:
                    UserRegistryOption.IsChecked = true;
                    UserRegTextbox.Text = userData;
                    break;
                case PolicyLoaderSource.PolFile:
                    UserFileOption.IsChecked = true;
                    UserPolFilenameTextbox.Text = userData;
                    break;
                case PolicyLoaderSource.SidGpo:
                    UserPerUserGpoOption.IsChecked = true;
                    UserGpoSidTextbox.Text = userData;
                    break;
                case PolicyLoaderSource.NtUserDat:
                    UserPerUserRegOption.IsChecked = true;
                    UserHivePathTextbox.Text = userData;
                    break;
                case PolicyLoaderSource.Null:
                    UserNullOption.IsChecked = true;
                    break;
            }

            CompOptionsCheckedChanged(null, null);
            UserOptionsCheckedChanged(null, null);
        }

        private void BrowseForPol(Wpf.Ui.Controls.TextBox destTextbox)
        {
            var sfd = new Microsoft.Win32.SaveFileDialog { OverwritePrompt = false, Filter = "Registry policy files|*.pol" };
            if (sfd.ShowDialog() == true)
            {
                destTextbox.Text = sfd.FileName;
            }
        }

        private void CompOptionsCheckedChanged(object sender, RoutedEventArgs e)
        {
            bool regMount = CompRegistryOption.IsChecked == true;
            CompRegTextbox.IsEnabled = regMount;
            bool polActive = CompFileOption.IsChecked == true;
            CompPolFilenameTextbox.IsEnabled = polActive;
            CompFileBrowseButton.IsEnabled = polActive;
        }

        private void UserOptionsCheckedChanged(object sender, RoutedEventArgs e)
        {
            bool regMount = UserRegistryOption.IsChecked == true;
            UserRegTextbox.IsEnabled = regMount;
            bool file = UserFileOption.IsChecked == true;
            UserPolFilenameTextbox.IsEnabled = file;
            UserFileBrowseButton.IsEnabled = file;
            bool perUserGpo = UserPerUserGpoOption.IsChecked == true;
            UserGpoSidTextbox.IsEnabled = perUserGpo;
            UserBrowseGpoButton.IsEnabled = perUserGpo;
            bool perUserHive = UserPerUserRegOption.IsChecked == true;
            UserHivePathTextbox.IsEnabled = perUserHive;
            UserBrowseHiveButton.IsEnabled = perUserHive;
        }

        private void CompFileBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            BrowseForPol(CompPolFilenameTextbox);
        }

        private void UserFileBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            BrowseForPol(UserPolFilenameTextbox);
        }

        private void UserBrowseRegistryButton_Click(object sender, RoutedEventArgs e)
        {
            var hivePath = OpenUserRegistryWindow.PresentDialog(WpfInterop.AsIWin32Window(this));
            if (hivePath is not null)
            {
                UserHivePathTextbox.Text = hivePath;
            }
        }

        private void UserBrowseGpoButton_Click(object sender, RoutedEventArgs e)
        {
            var sid = OpenUserGpoWindow.PresentDialog(WpfInterop.AsIWin32Window(this));
            if (sid is not null)
            {
                UserGpoSidTextbox.Text = sid;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (CompLocalOption.IsChecked == true)
                {
                    _selectedComputer = new PolicyLoader(PolicyLoaderSource.LocalGpo, "", false);
                }
                else if (CompRegistryOption.IsChecked == true)
                {
                    _selectedComputer = new PolicyLoader(PolicyLoaderSource.LocalRegistry, CompRegTextbox.Text, false);
                }
                else if (CompFileOption.IsChecked == true)
                {
                    _selectedComputer = new PolicyLoader(PolicyLoaderSource.PolFile, CompPolFilenameTextbox.Text, false);
                }
                else
                {
                    _selectedComputer = new PolicyLoader(PolicyLoaderSource.Null, "", false);
                }
            }
            catch (System.Exception ex)
            {
                MsgBoxCompat.Show("The computer policy loader could not be created. " + ex.Message, MsgBoxButtons.OK, MsgBoxIcon.Warning);
                return;
            }

            try
            {
                if (UserLocalOption.IsChecked == true)
                {
                    _selectedUser = new PolicyLoader(PolicyLoaderSource.LocalGpo, "", true);
                }
                else if (UserRegistryOption.IsChecked == true)
                {
                    _selectedUser = new PolicyLoader(PolicyLoaderSource.LocalRegistry, UserRegTextbox.Text, true);
                }
                else if (UserFileOption.IsChecked == true)
                {
                    _selectedUser = new PolicyLoader(PolicyLoaderSource.PolFile, UserPolFilenameTextbox.Text, true);
                }
                else if (UserPerUserGpoOption.IsChecked == true)
                {
                    _selectedUser = new PolicyLoader(PolicyLoaderSource.SidGpo, UserGpoSidTextbox.Text, true);
                }
                else if (UserPerUserRegOption.IsChecked == true)
                {
                    _selectedUser = new PolicyLoader(PolicyLoaderSource.NtUserDat, UserHivePathTextbox.Text, true);
                }
                else
                {
                    _selectedUser = new PolicyLoader(PolicyLoaderSource.Null, "", true);
                }
            }
            catch (System.Exception ex)
            {
                MsgBoxCompat.Show("The user policy loader could not be created. " + ex.Message, MsgBoxButtons.OK, MsgBoxIcon.Warning);
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

        public static (PolicyLoader User, PolicyLoader Computer)? PresentDialog(
            System.Windows.Forms.IWin32Window owner,
            PolicyLoaderSource compType,
            string compData,
            PolicyLoaderSource userType,
            string userData)
        {
            ThemeService.ApplyPersisted();
            var window = new OpenPolWindow();
            window.SetLastSources(compType, compData, userType, userData);
            WpfInterop.SetOwner(window, owner);
            window.ShowDialog();
            return window._accepted ? (window._selectedUser, window._selectedComputer) : null;
        }
    }
}

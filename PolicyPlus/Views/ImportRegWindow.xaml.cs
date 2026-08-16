using System;
using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace PolicyPlus.Views
{
    public partial class ImportRegWindow : FluentWindow
    {
        private IPolicySource _userPolicySource;
        private IPolicySource _compPolicySource;

        // Set instead of _userPolicySource/_compPolicySource when there's only ever one possible
        // target regardless of the REG file's hive (EditPolWindow's "Import" button, which always
        // imports into the specific raw POL section already being edited) - skips hive detection
        // and any prompt entirely.
        private IPolicySource _fixedTarget;

        private bool _accepted;

        public ImportRegWindow()
        {
            InitializeComponent();
            WpfInterop.FixSizeToContent(this);
        }

        private void ButtonBrowse_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog { Filter = "Registry scripts|*.reg" };
            if (ofd.ShowDialog() != true)
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
                        MsgBoxCompat.Show("This REG file contains data for default values, which cannot be applied to all policy sources.", MsgBoxButtons.OK, MsgBoxIcon.Warning);
                    }
                }
                catch (Exception)
                {
                    MsgBoxCompat.Show("An error occurred while trying to guess the prefix.", MsgBoxButtons.OK, MsgBoxIcon.Warning);
                }
            }
        }

        private void ButtonImport_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TextReg.Text))
            {
                MsgBoxCompat.Show("Please specify a REG file to import.", MsgBoxButtons.OK, MsgBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrEmpty(TextRoot.Text))
            {
                MsgBoxCompat.Show("Please specify the prefix used to fully qualify paths in the REG file.", MsgBoxButtons.OK, MsgBoxIcon.Warning);
                return;
            }

            RegFile reg;
            try
            {
                reg = RegFile.Load(TextReg.Text, TextRoot.Text);
            }
            catch (Exception)
            {
                MsgBoxCompat.Show("Failed to import the REG file.", MsgBoxButtons.OK, MsgBoxIcon.Warning);
                return;
            }

            var target = ResolveTarget(reg);
            if (target is null)
            {
                return; // Either the user cancelled the (mixed-hive) section prompt, or the file has nothing importable
            }

            try
            {
                reg.Apply(target);
                _accepted = true;
                Close();
            }
            catch (Exception)
            {
                MsgBoxCompat.Show("Failed to import the REG file.", MsgBoxButtons.OK, MsgBoxIcon.Warning);
            }
        }

        // Infers the target policy source from the REG file's own hive path
        // (HKEY_LOCAL_MACHINE vs HKEY_CURRENT_USER/HKEY_USERS) instead of always asking - only
        // prompts when the file genuinely mixes both hives and the choice can't be inferred.
        private IPolicySource ResolveTarget(RegFile reg)
        {
            if (_fixedTarget is not null)
            {
                return _fixedTarget;
            }

            var hiveCounts = reg.CountKeysByHive();
            bool hasComputer = hiveCounts[RegFileHive.Computer] > 0;
            bool hasUser = hiveCounts[RegFileHive.User] > 0;
            if (hasComputer && hasUser)
            {
                var section = OpenSectionWindow.PresentDialog(this, true, true);
                return section is null ? null : (section == AdmxPolicySection.Machine ? _compPolicySource : _userPolicySource);
            }

            if (hasComputer)
            {
                return _compPolicySource;
            }

            if (hasUser)
            {
                return _userPolicySource;
            }

            MsgBoxCompat.Show("This REG file doesn't contain any Computer (HKEY_LOCAL_MACHINE) or User (HKEY_CURRENT_USER/HKEY_USERS) entries to import.", MsgBoxButtons.OK, MsgBoxIcon.Warning);
            return null;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }

        public static bool PresentDialog(System.Windows.Window owner, IPolicySource userPolicySource, IPolicySource compPolicySource)
        {
            ThemeService.ApplyPersisted();
            var window = new ImportRegWindow { _userPolicySource = userPolicySource, _compPolicySource = compPolicySource };
            WpfInterop.SetOwner(window, owner);
            window.ShowDialog();
            return window._accepted;
        }

        // For callers with only one possible target regardless of hive (see _fixedTarget).
        public static bool PresentDialog(System.Windows.Window owner, IPolicySource fixedTarget)
        {
            ThemeService.ApplyPersisted();
            var window = new ImportRegWindow { _fixedTarget = fixedTarget };
            WpfInterop.SetOwner(window, owner);
            window.ShowDialog();
            return window._accepted;
        }
    }
}

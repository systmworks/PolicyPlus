using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Wpf.Ui.Controls;

namespace PolicyPlus.Views
{
    public partial class FindByRegistryWindow : FluentWindow
    {
        private Func<PolicyPlusPolicy, bool> _searcher;

        public FindByRegistryWindow()
        {
            InitializeComponent();
            Loaded += (s, e) => KeyTextbox.Focus();
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            string keyName = KeyTextbox.Text.ToLowerInvariant();
            string valName = ValueTextbox.Text.ToLowerInvariant();
            if (string.IsNullOrEmpty(keyName) & string.IsNullOrEmpty(valName))
            {
                MsgBoxCompat.Show("Please enter search terms.", MsgBoxButtons.OK, MsgBoxIcon.Warning);
                return;
            }

            if (new[] { @"HKLM\", @"HKCU\", @"HKEY_LOCAL_MACHINE\", @"HKEY_CURRENT_USER\" }.Any(bad => keyName.StartsWith(bad, StringComparison.InvariantCultureIgnoreCase)))
            {
                MsgBoxCompat.Show("Policies' root keys are determined only by their section. Remove the root key from the search terms and try again.", MsgBoxButtons.OK, MsgBoxIcon.Warning);
                return;
            }

            _searcher = new Func<PolicyPlusPolicy, bool>((policy) =>
            {
                var affected = PolicyProcessing.GetReferencedRegistryValues(policy);
                foreach (var rkvp in affected)
                {
                    if (!string.IsNullOrEmpty(valName))
                    {
                        if (!LikeOperator.LikeString(rkvp.Value.ToLowerInvariant(), valName, CompareMethod.Binary))
                            continue;
                    }

                    if (!string.IsNullOrEmpty(keyName))
                    {
                        if (keyName.Contains("*") | keyName.Contains("?"))
                        {
                            if (!LikeOperator.LikeString(rkvp.Key.ToLowerInvariant(), keyName, CompareMethod.Binary))
                                continue;
                        }
                        else if (keyName.Contains(@"\"))
                        {
                            if (!rkvp.Key.StartsWith(keyName, StringComparison.InvariantCultureIgnoreCase))
                                continue;
                        }
                        else if (!Strings.Split(rkvp.Key, @"\").Any(part => part.Equals(keyName, StringComparison.InvariantCultureIgnoreCase)))
                            continue;
                    }

                    return true;
                }

                return false;
            });
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }

        public static Func<PolicyPlusPolicy, bool> PresentDialog(System.Windows.Forms.IWin32Window owner)
        {
            ThemeService.ApplyPersisted();
            var window = new FindByRegistryWindow();
            WpfInterop.SetOwner(window, owner);
            window.ShowDialog();
            return window._searcher;
        }
    }
}

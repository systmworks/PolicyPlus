using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace PolicyPlus.Views
{
    public partial class InspectSpolFragmentWindow : FluentWindow
    {
        private string _spolFragment;

        public InspectSpolFragmentWindow()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                TextSpol.Focus();
                TextSpol.SelectAll();
            };
        }

        private void ButtonCopy_Click(object sender, RoutedEventArgs e)
        {
            TextSpol.SelectAll();
            TextSpol.Copy();
        }

        private void ButtonClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void CheckHeader_CheckedChanged(object sender, RoutedEventArgs e)
        {
            UpdateTextbox();
        }

        private void UpdateTextbox()
        {
            TextSpol.Text = CheckHeader.IsChecked == true
                ? "Policy Plus Semantic Policy\r\n\r\n" + _spolFragment
                : _spolFragment;
        }

        private void TextSpol_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control)
            {
                TextSpol.SelectAll();
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e) => WpfInterop.HandleEscapeToClose(this, e);

        public static void PresentDialog(
            System.Windows.Window owner,
            PolicyPlusPolicy policy,
            AdmxBundle admxWorkspace,
            IPolicySource compSource,
            IPolicySource userSource,
            Dictionary<string, string> compComments,
            Dictionary<string, string> userComments)
        {
            ThemeService.ApplyPersisted();
            var window = new InspectSpolFragmentWindow { TextPolicyName = { Text = policy.DisplayName } };

            var sb = new StringBuilder();
            bool AddSection(AdmxPolicySection section)
            {
                if ((policy.RawPolicy.Section & section) == 0)
                {
                    return false;
                }

                var polSource = section == AdmxPolicySection.Machine ? compSource : userSource;
                var commentsMap = section == AdmxPolicySection.Machine ? compComments : userComments;
                var spolState = new SpolPolicyState { UniqueID = policy.UniqueID, Section = section };
                if (commentsMap is not null && commentsMap.ContainsKey(policy.UniqueID))
                {
                    spolState.Comment = commentsMap[policy.UniqueID];
                }

                spolState.BasicState = PolicyProcessing.GetPolicyState(polSource, policy);
                if (spolState.BasicState == PolicyState.Enabled)
                {
                    spolState.ExtraOptions = PolicyProcessing.GetPolicyOptionStates(polSource, policy);
                }

                sb.AppendLine(SpolFile.GetFragment(spolState));
                return true;
            }

            AddSection(AdmxPolicySection.Machine);
            AddSection(AdmxPolicySection.User);
            window._spolFragment = sb.ToString();
            window.UpdateTextbox();

            WpfInterop.SetOwner(window, owner);
            window.ShowDialog();
        }
    }
}

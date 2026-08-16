using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace PolicyPlus.Views
{
    public partial class DetailPolicyWindow : FluentWindow
    {
        private PolicyPlusPolicy _selectedPolicy;

        public DetailPolicyWindow()
        {
            InitializeComponent();
        }

        private void PrepareDialog(PolicyPlusPolicy policy)
        {
            _selectedPolicy = policy;
            NameTextbox.Text = policy.DisplayName;
            IdTextbox.Text = policy.UniqueID;
            DefinedTextbox.Text = policy.RawPolicy.DefinedIn.SourceFile;
            DisplayCodeTextbox.Text = policy.RawPolicy.DisplayCode;
            InfoCodeTextbox.Text = policy.RawPolicy.ExplainCode;
            PresentCodeTextbox.Text = policy.RawPolicy.PresentationID;
            SectionTextbox.Text = policy.RawPolicy.Section switch
            {
                AdmxPolicySection.Both => "User or computer",
                AdmxPolicySection.Machine => "Computer",
                AdmxPolicySection.User => "User",
                _ => SectionTextbox.Text,
            };
            SupportButton.IsEnabled = policy.SupportedOn is not null;
            if (policy.SupportedOn is not null)
            {
                SupportTextbox.Text = policy.SupportedOn.DisplayName;
            }
            else if (!string.IsNullOrEmpty(policy.RawPolicy.SupportedCode))
            {
                SupportTextbox.Text = "<missing: " + policy.RawPolicy.SupportedCode + ">";
            }
            else
            {
                SupportTextbox.Text = "";
            }

            CategoryButton.IsEnabled = policy.Category is not null;
            if (policy.Category is not null)
            {
                CategoryTextbox.Text = policy.Category.DisplayName;
            }
            else if (!string.IsNullOrEmpty(policy.RawPolicy.CategoryID))
            {
                CategoryTextbox.Text = "<orphaned from " + policy.RawPolicy.CategoryID + ">";
            }
            else
            {
                CategoryTextbox.Text = "<uncategorized>";
            }

            PathTextbox.Text = BuildTemplatePath(policy);
        }

        private static string BuildTemplatePath(PolicyPlusPolicy policy)
        {
            string sectionPrefix = policy.RawPolicy.Section switch
            {
                AdmxPolicySection.Machine => "Computer",
                AdmxPolicySection.User => "User",
                _ => "Computer and User",
            };
            var segments = new List<string> { sectionPrefix, "Administrative Templates" };
            var chain = new List<string>();
            for (var cat = policy.Category; cat is not null; cat = cat.Parent)
            {
                chain.Insert(0, cat.DisplayName);
            }

            segments.AddRange(chain);
            segments.Add(policy.DisplayName);
            return string.Join(" > ", segments);
        }

        private void SupportButton_Click(object sender, RoutedEventArgs e)
        {
            DetailSupportWindow.PresentDialog(this, _selectedPolicy.SupportedOn);
        }

        private void CategoryButton_Click(object sender, RoutedEventArgs e)
        {
            DetailCategoryWindow.PresentDialog(this, _selectedPolicy.Category);
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }

        public static void PresentDialog(System.Windows.Window owner, PolicyPlusPolicy policy)
        {
            ThemeService.ApplyPersisted();
            var window = new DetailPolicyWindow();
            window.PrepareDialog(policy);
            WpfInterop.SetOwner(window, owner);
            window.ShowDialog();
        }
    }
}

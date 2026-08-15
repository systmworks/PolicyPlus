using System;
using System.Collections.Generic;

namespace PolicyPlus
{
    public partial class DetailPolicy
    {
        private PolicyPlusPolicy SelectedPolicy;

        public DetailPolicy()
        {
            InitializeComponent();
        }
        public void PresentDialog(PolicyPlusPolicy Policy)
        {
            SelectedPolicy = Policy;
            NameTextbox.Text = Policy.DisplayName;
            IdTextbox.Text = Policy.UniqueID;
            DefinedTextbox.Text = Policy.RawPolicy.DefinedIn.SourceFile;
            DisplayCodeTextbox.Text = Policy.RawPolicy.DisplayCode;
            InfoCodeTextbox.Text = Policy.RawPolicy.ExplainCode;
            PresentCodeTextbox.Text = Policy.RawPolicy.PresentationID;
            switch (Policy.RawPolicy.Section)
            {
                case AdmxPolicySection.Both:
                    {
                        SectionTextbox.Text = "User or computer";
                        break;
                    }
                case AdmxPolicySection.Machine:
                    {
                        SectionTextbox.Text = "Computer";
                        break;
                    }
                case AdmxPolicySection.User:
                    {
                        SectionTextbox.Text = "User";
                        break;
                    }
            }
            SupportButton.Enabled = Policy.SupportedOn is not null;
            if (Policy.SupportedOn is not null)
            {
                SupportTextbox.Text = Policy.SupportedOn.DisplayName;
            }
            else if (!string.IsNullOrEmpty(Policy.RawPolicy.SupportedCode))
            {
                SupportTextbox.Text = "<missing: " + Policy.RawPolicy.SupportedCode + ">";
            }
            else
            {
                SupportTextbox.Text = "";
            }
            CategoryButton.Enabled = Policy.Category is not null;
            if (Policy.Category is not null)
            {
                CategoryTextbox.Text = Policy.Category.DisplayName;
            }
            else if (!string.IsNullOrEmpty(Policy.RawPolicy.CategoryID))
            {
                CategoryTextbox.Text = "<orphaned from " + Policy.RawPolicy.CategoryID + ">";
            }
            else
            {
                CategoryTextbox.Text = "<uncategorized>";
            }
            PathTextbox.Text = BuildTemplatePath(Policy);
            ShowDialog();
        }
        // Breadcrumb-style path matching where the policy appears in the real Group Policy Editor
        // tree, e.g. "Computer > Administrative Templates > Windows Components > ... > <policy>".
        private static string BuildTemplatePath(PolicyPlusPolicy Policy)
        {
            string sectionPrefix = Policy.RawPolicy.Section switch
            {
                AdmxPolicySection.Machine => "Computer",
                AdmxPolicySection.User => "User",
                _ => "Computer and User"
            };
            var segments = new List<string> { sectionPrefix, "Administrative Templates" };
            var chain = new List<string>();
            for (var cat = Policy.Category; cat is not null; cat = cat.Parent)
                chain.Insert(0, cat.DisplayName);
            segments.AddRange(chain);
            segments.Add(Policy.DisplayName);
            return string.Join(" > ", segments);
        }
        private void SupportButton_Click(object sender, EventArgs e)
        {
            My.MyProject.Forms.DetailSupport.PresentDialog(SelectedPolicy.SupportedOn);
        }
        private void CategoryButton_Click(object sender, EventArgs e)
        {
            My.MyProject.Forms.DetailCategory.PresentDialog(SelectedPolicy.Category);
        }
    }
}
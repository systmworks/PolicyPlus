using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace PolicyPlus
{
    public partial class FindByText
    {
        private Dictionary<string, string>[] CommentSources;
        public Func<PolicyPlusPolicy, bool> Searcher;

        public FindByText()
        {
            InitializeComponent();
        }
        public DialogResult PresentDialog(params Dictionary<string, string>[] CommentDicts)
        {
            CommentSources = CommentDicts.Where(d => d is not null).ToArray();
            return ShowDialog();
        }
        private void FindByText_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
                DialogResult = DialogResult.Cancel;
        }
        private void SearchButton_Click(object sender, EventArgs e)
        {
            string text = StringTextbox.Text;
            if (string.IsNullOrEmpty(text))
            {
                MsgBoxCompat.Show("Please enter search terms.", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            bool checkTitle = TitleCheckbox.Checked;
            bool checkDesc = DescriptionCheckbox.Checked;
            bool checkComment = CommentCheckbox.Checked;
            if (!(checkTitle | checkDesc | checkComment))
            {
                MsgBoxCompat.Show("At least one attribute must be searched. Check one of the boxes and try again.", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            Searcher = PolicySearch.BuildMatcher(text, checkTitle, checkDesc, checkComment, CommentSources);
            DialogResult = DialogResult.OK;
        }
    }
}
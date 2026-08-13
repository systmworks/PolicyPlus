using System;
using System.Windows.Forms;

namespace PolicyPlus
{
    public partial class LanguageOptions
    {
        private string OriginalLanguage;
        public string NewLanguage;

        public LanguageOptions()
        {
            InitializeComponent();
        }
        public DialogResult PresentDialog(string CurrentLanguage)
        {
            OriginalLanguage = CurrentLanguage;
            TextAdmlLanguage.Text = CurrentLanguage;
            return ShowDialog();
        }
        private void ButtonOK_Click(object sender, EventArgs e)
        {
            string selection = TextAdmlLanguage.Text.Trim();
            if (selection.Split('-').Length != 2)
            {
                MsgBoxCompat.Show("Please enter a valid language code.", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            if ((selection ?? "") == (OriginalLanguage ?? ""))
            {
                DialogResult = DialogResult.Cancel;
            }
            else
            {
                NewLanguage = selection;
                DialogResult = DialogResult.OK;
            }
        }
    }
}
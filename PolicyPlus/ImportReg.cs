using System;
using System.Windows.Forms;

namespace PolicyPlus
{
    public partial class ImportReg
    {
        private IPolicySource PolicySource;

        public ImportReg()
        {
            InitializeComponent();
        }
        public DialogResult PresentDialog(IPolicySource Target)
        {
            TextReg.Text = "";
            TextRoot.Text = "";
            PolicySource = Target;
            return ShowDialog();
        }
        private void ButtonBrowse_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "Registry scripts|*.reg";
                if (ofd.ShowDialog() != DialogResult.OK)
                    return;
                TextReg.Text = ofd.FileName;
                if (string.IsNullOrEmpty(TextRoot.Text))
                {
                    try
                    {
                        var reg = RegFile.Load(ofd.FileName, "");
                        TextRoot.Text = reg.GuessPrefix();
                        if (reg.HasDefaultValues())
                            MsgBoxCompat.Show("This REG file contains data for default values, which cannot be applied to all policy sources.", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    }
                    catch (Exception ex)
                    {
                        MsgBoxCompat.Show("An error occurred while trying to guess the prefix.", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    }
                }
            }
        }
        private void ImportReg_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
                DialogResult = DialogResult.Cancel;
        }
        private void ButtonImport_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(TextReg.Text))
            {
                MsgBoxCompat.Show("Please specify a REG file to import.", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            if (string.IsNullOrEmpty(TextRoot.Text))
            {
                MsgBoxCompat.Show("Please specify the prefix used to fully qualify paths in the REG file.", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            try
            {
                var reg = RegFile.Load(TextReg.Text, TextRoot.Text);
                reg.Apply(PolicySource);
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                MsgBoxCompat.Show("Failed to import the REG file.", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }
    }
}
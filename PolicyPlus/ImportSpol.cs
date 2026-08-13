using System;
using System.Windows.Forms;
using Microsoft.VisualBasic;

namespace PolicyPlus
{
    public partial class ImportSpol
    {
        public SpolFile Spol;

        public ImportSpol()
        {
            InitializeComponent();
        }
        private void ButtonOpenFile_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "Semantic Policy files|*.spol|All files|*.*";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    TextSpol.Text = System.IO.File.ReadAllText(ofd.FileName);
                }
            }
        }
        private void ButtonVerify_Click(object sender, EventArgs e)
        {
            try
            {
                var spol = SpolFile.FromText(TextSpol.Text);
                MsgBoxCompat.Show("Validation successful, " + spol.Policies.Count + " policy settings found.", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MsgBoxCompat.Show("SPOL validation failed: " + ex.Message, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }
        private void ButtonApply_Click(object sender, EventArgs e)
        {
            try
            {
                Spol = SpolFile.FromText(TextSpol.Text); // Tell the main form that the SPOL is ready to be committed
                DialogResult = DialogResult.OK;
            }
            catch (Exception ex)
            {
                MsgBoxCompat.Show("The SPOL text is invalid: " + ex.Message, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }
        private void ImportSpol_Shown(object sender, EventArgs e)
        {
            Spol = null;
        }
        private void ImportSpol_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape & !(TextSpol.Focused & TextSpol.SelectionLength > 0))
                DialogResult = DialogResult.Cancel;
        }
        private void TextSpol_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.A & e.Control)
                TextSpol.SelectAll();
        }
        private void ButtonReset_Click(object sender, EventArgs e)
        {
            if (MsgBoxCompat.Show("Are you sure you want to reset the text box?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                TextSpol.Text = "Policy Plus Semantic Policy" + Constants.vbCrLf + Constants.vbCrLf;
            }
        }
    }
}
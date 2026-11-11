using System;
using System.Windows.Forms;

namespace Civil3DCsharp
{
    public partial class CutSolidBodyToClipboardForm : Form
    {
        public CutSolidBodyToClipboardForm()
        {
            InitializeComponent();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}

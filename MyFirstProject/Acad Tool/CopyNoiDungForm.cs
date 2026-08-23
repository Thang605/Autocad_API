using System;
using System.Drawing;
using System.Windows.Forms;
using WinFormsLabel = System.Windows.Forms.Label;

namespace MyFirstProject.Acad_Tool
{
    /// <summary>
    /// Form tuỳ chọn cho lệnh CN (Copy Nội dung Text)
    /// - Link: khi text nguồn thay đổi, text đích cũng tự động cập nhật (dùng Field)
    /// - Không link: chỉ copy nội dung một lần
    /// </summary>
    public class CopyNoiDungForm : Form
    {
        // Static fields nhớ giá trị lần trước
        private static bool _lastIsLinked = false;

        // Properties trả về
        public bool IsLinked { get; private set; } = false;
        public bool FormAccepted { get; private set; } = false;

        // Controls
        private RadioButton rdoNoLink = null!;
        private RadioButton rdoLink = null!;

        public CopyNoiDungForm()
        {
            InitializeComponent();
            RestoreLastValues();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Form settings
            this.Text = "Copy Nội Dung Text (CN)";
            this.ClientSize = new Size(360, 220);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Title
            var lblTitle = new WinFormsLabel
            {
                Text = "COPY NỘI DUNG TEXT",
                Font = new Font("Microsoft Sans Serif", 12F, FontStyle.Bold),
                Location = new Point(20, 12),
                Size = new Size(320, 28),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.DarkBlue
            };

            // ========== Group Tuỳ chọn ==========
            var grpOptions = new GroupBox
            {
                Text = "Chế độ copy",
                Location = new Point(12, 50),
                Size = new Size(330, 100)
            };

            rdoNoLink = new RadioButton
            {
                Text = "Copy nội dung (không link)",
                Location = new Point(20, 28),
                Size = new Size(290, 25),
                Checked = true,
                Font = new Font("Microsoft Sans Serif", 9.5F)
            };

            var lblNoLinkDesc = new WinFormsLabel
            {
                Text = "Chỉ copy nội dung text một lần, không liên kết.",
                Location = new Point(38, 50),
                Size = new Size(280, 18),
                ForeColor = Color.Gray,
                Font = new Font("Microsoft Sans Serif", 8F, FontStyle.Italic)
            };

            rdoLink = new RadioButton
            {
                Text = "Link nội dung (Field liên kết)",
                Location = new Point(20, 68),
                Size = new Size(290, 25),
                Checked = false,
                Font = new Font("Microsoft Sans Serif", 9.5F)
            };

            grpOptions.Controls.AddRange(new Control[] { rdoNoLink, lblNoLinkDesc, rdoLink });

            // Link description (bên ngoài group, hiển thị khi chọn Link)
            var lblLinkDesc = new WinFormsLabel
            {
                Text = "⚡ Link: Text đích sẽ liên kết với text nguồn bằng Field.\n     Khi text nguồn thay đổi → text đích tự cập nhật (REGEN).",
                Location = new Point(20, 155),
                Size = new Size(320, 32),
                ForeColor = Color.FromArgb(0, 120, 60),
                Font = new Font("Microsoft Sans Serif", 7.8F)
            };

            // ========== Buttons ==========
            var btnOK = new Button
            {
                Text = "OK",
                Location = new Point(155, 188),
                Size = new Size(90, 28),
                Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold)
            };
            btnOK.Click += BtnOK_Click;

            var btnCancel = new Button
            {
                Text = "Hủy",
                Location = new Point(252, 188),
                Size = new Size(90, 28)
            };
            btnCancel.Click += BtnCancel_Click;

            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;

            // Add to form
            this.Controls.AddRange(new Control[]
            {
                lblTitle,
                grpOptions,
                lblLinkDesc,
                btnOK,
                btnCancel
            });

            this.ResumeLayout(false);
        }

        private void RestoreLastValues()
        {
            rdoLink.Checked = _lastIsLinked;
            rdoNoLink.Checked = !_lastIsLinked;
        }

        private void BtnOK_Click(object? sender, EventArgs e)
        {
            IsLinked = rdoLink.Checked;

            // Save for next time
            _lastIsLinked = IsLinked;

            FormAccepted = true;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void BtnCancel_Click(object? sender, EventArgs e)
        {
            FormAccepted = false;
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}

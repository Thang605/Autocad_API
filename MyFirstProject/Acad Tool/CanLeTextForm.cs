using System;
using System.Drawing;
using System.Windows.Forms;
using WinFormsLabel = System.Windows.Forms.Label;

namespace MyFirstProject.Acad_Tool
{
    /// <summary>
    /// Form cài đặt căn lề cho Text/MText
    /// </summary>
    public class CanLeTextForm : Form
    {
        // Static fields nhớ giá trị lần trước (mặc định lần đầu)
        private static int _lastAlignX = 1;  // 0=Không căn, 1=Left, 2=Center, 3=Right
        private static int _lastAlignY = 0;  // 0=Không căn, 1=Top, 2=Middle, 3=Bottom
        private static int _lastJustify = 10; // index trong combobox (10=BaseLeft)
        private static bool _lastPickPoint = true;

        // Properties trả về
        public int AlignX { get; private set; } = 0;
        public int AlignY { get; private set; } = 0;
        public int JustifyIndex { get; private set; } = 0;
        public bool PickPoint { get; private set; } = false;
        public bool FormAccepted { get; private set; } = false;

        // Controls
        private ComboBox cboAlignX = null!;
        private ComboBox cboAlignY = null!;
        private CheckBox chkPickPoint = null!;
        private ComboBox cboJustify = null!;

        // Justify options mapping
        public static readonly string[] JustifyOptions = new string[]
        {
            "Không đổi",
            "TopLeft",
            "TopCenter",
            "TopRight",
            "MiddleLeft",
            "MiddleCenter",
            "MiddleRight",
            "BottomLeft",
            "BottomCenter",
            "BottomRight",
            "BaseLeft",
            "BaseCenter",
            "BaseRight"
        };

        public CanLeTextForm()
        {
            InitializeComponent();
            RestoreLastSettings();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Form settings
            this.Text = "Căn Lề cho Text";
            this.ClientSize = new Size(340, 310);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Title
            var lblTitle = new WinFormsLabel
            {
                Text = "CĂN LỀ CHO TEXT / MTEXT",
                Font = new Font("Microsoft Sans Serif", 11F, FontStyle.Bold),
                Location = new Point(20, 12),
                Size = new Size(300, 25),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.DarkBlue
            };

            // ========== Group Căn lề ==========
            var grpCanLe = new GroupBox
            {
                Text = "Căn lề theo phương",
                Location = new Point(12, 45),
                Size = new Size(310, 115)
            };

            var lblX = new WinFormsLabel
            {
                Text = "Phương X:",
                Location = new Point(15, 28),
                Size = new Size(70, 23)
            };

            cboAlignX = new ComboBox
            {
                Location = new Point(90, 25),
                Size = new Size(100, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cboAlignX.Items.AddRange(new object[] { "Không căn", "Left", "Center", "Right" });
            cboAlignX.SelectedIndex = 0;

            var lblY = new WinFormsLabel
            {
                Text = "Phương Y:",
                Location = new Point(15, 55),
                Size = new Size(70, 23)
            };

            cboAlignY = new ComboBox
            {
                Location = new Point(90, 52),
                Size = new Size(100, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cboAlignY.Items.AddRange(new object[] { "Không căn", "Top", "Middle", "Bottom" });
            cboAlignY.SelectedIndex = 0;

            chkPickPoint = new CheckBox
            {
                Text = "Chọn điểm căn lề (pick point)",
                Location = new Point(15, 82),
                Size = new Size(280, 23),
                Checked = false
            };

            grpCanLe.Controls.AddRange(new Control[] { lblX, cboAlignX, lblY, cboAlignY, chkPickPoint });

            // ========== Group Justify ==========
            var grpJustify = new GroupBox
            {
                Text = "Thiết lập Justify cho mỗi Text",
                Location = new Point(12, 170),
                Size = new Size(310, 65)
            };

            var lblJustify = new WinFormsLabel
            {
                Text = "Justify:",
                Location = new Point(15, 28),
                Size = new Size(55, 23)
            };

            cboJustify = new ComboBox
            {
                Location = new Point(75, 25),
                Size = new Size(220, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cboJustify.Items.AddRange(JustifyOptions);
            cboJustify.SelectedIndex = 0;

            grpJustify.Controls.AddRange(new Control[] { lblJustify, cboJustify });

            // ========== Buttons ==========
            var btnOK = new Button
            {
                Text = "OK",
                Location = new Point(130, 260),
                Size = new Size(90, 30),
                Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold)
            };
            btnOK.Click += BtnOK_Click;

            var btnCancel = new Button
            {
                Text = "Hủy",
                Location = new Point(230, 260),
                Size = new Size(90, 30)
            };
            btnCancel.Click += BtnCancel_Click;

            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;

            // Add to form
            this.Controls.AddRange(new Control[]
            {
                lblTitle,
                grpCanLe,
                grpJustify,
                btnOK,
                btnCancel
            });

            // Form events
            this.FormClosing += (s, e) => SaveCurrentSettings();

            this.ResumeLayout(false);
        }

        private void RestoreLastSettings()
        {
            cboAlignX.SelectedIndex = _lastAlignX;
            cboAlignY.SelectedIndex = _lastAlignY;
            cboJustify.SelectedIndex = _lastJustify;
            chkPickPoint.Checked = _lastPickPoint;
        }

        private void SaveCurrentSettings()
        {
            if (cboAlignX.SelectedIndex != -1) _lastAlignX = cboAlignX.SelectedIndex;
            if (cboAlignY.SelectedIndex != -1) _lastAlignY = cboAlignY.SelectedIndex;
            if (cboJustify.SelectedIndex != -1) _lastJustify = cboJustify.SelectedIndex;
            _lastPickPoint = chkPickPoint.Checked;
        }

        private void BtnOK_Click(object? sender, EventArgs e)
        {
            AlignX = cboAlignX.SelectedIndex;
            AlignY = cboAlignY.SelectedIndex;
            JustifyIndex = cboJustify.SelectedIndex;
            PickPoint = chkPickPoint.Checked;

            // Validate: phải chọn ít nhất 1 thao tác
            if (AlignX == 0 && AlignY == 0 && JustifyIndex == 0)
            {
                MessageBox.Show("Vui lòng chọn ít nhất một thao tác căn lề hoặc justify!",
                    "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Save settings handled in FormClosing/SaveCurrentSettings

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

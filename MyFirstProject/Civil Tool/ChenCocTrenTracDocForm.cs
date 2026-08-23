using System;
using System.Drawing;
using System.Windows.Forms;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsFont = System.Drawing.Font;
using WinFormsPoint = System.Drawing.Point;

namespace MyFirstProject.Civil_Tool
{
    /// <summary>
    /// Form nhập tên cọc cho lệnh CTS_ChenCoc_TrenTracDoc.
    /// Hỗ trợ đặt tên tự động tăng dần, ghi nhớ lần dùng trước.
    /// Workflow: Mở form 1 lần → thiết lập pattern → click liên tục trên trắc dọc.
    /// </summary>
    public class ChenCocTrenTracDocForm : Form
    {
        // ============================================
        // Static: Ghi nhớ giữa các lần gọi lệnh
        // ============================================
        private static string _lastPrefix = "C";
        private static int _lastStartNumber = 1;
        private static string _lastSuffix = "";
        private static bool _lastAutoIncrement = true;
        private static string _lastSampleLineStyle = "Road Sample Line";
        private static int _lastPresetIndex = 0; // 0 = Cọc C

        // Biến đếm nội bộ (tăng dần trong cùng 1 phiên)
        private static int _currentNumber = 1;
        private static bool _sessionActive = false;

        // ============================================
        // Properties trả về cho command
        // ============================================
        public string Prefix { get; private set; } = "C";
        public int StartNumber { get; private set; } = 1;
        public string Suffix { get; private set; } = "";
        public bool AutoIncrement { get; private set; } = true;
        public string SampleLineStyleName { get; private set; } = "Road Sample Line";
        public bool FormAccepted { get; private set; } = false;

        // ============================================
        // UI Controls
        // ============================================
        private WinFormsLabel lblTitle = null!;

        private GroupBox grpPreset = null!;
        private FlowLayoutPanel pnlPresets = null!;

        private GroupBox grpTenCoc = null!;
        private WinFormsLabel lblPrefix = null!;
        private TextBox txtPrefix = null!;
        private WinFormsLabel lblStartNumber = null!;
        private NumericUpDown numStartNumber = null!;
        private WinFormsLabel lblSuffix = null!;
        private TextBox txtSuffix = null!;
        private CheckBox chkAutoIncrement = null!;

        private GroupBox grpStyle = null!;
        private WinFormsLabel lblStyle = null!;
        private TextBox txtStyle = null!;

        private GroupBox grpPreview = null!;
        private WinFormsLabel lblPreviewLabel = null!;
        private WinFormsLabel lblPreviewValue = null!;
        private WinFormsLabel lblNextLabel = null!;
        private WinFormsLabel lblNextValue = null!;

        private Button btnOK = null!;
        private Button btnCancel = null!;
        private Button btnResetCounter = null!;

        // Preset definitions
        private readonly (string Name, string Prefix, string Suffix)[] _presets = new[]
        {
            ("Cọc C", "C", ""),
            ("Cọc H", "H", ""),
            ("Cọc Km", "Km", ""),
            ("Cọc TC", "TC", ""),
            ("Cọc ND", "ND", ""),
            ("Cọc P", "P", ""),
            ("Cọc TD", "TD", ""),
            ("Cọc NC", "NC", ""),
            ("Tùy chỉnh", "", ""),
        };

        public ChenCocTrenTracDocForm()
        {
            InitializeComponent();
            RestoreLastUsedValues();
            UpdatePreview();
        }

        private void InitializeComponent()
        {
            // Fonts
            var standardFont = new WinFormsFont("Segoe UI", 10F, FontStyle.Regular);
            var boldFont = new WinFormsFont("Segoe UI", 10F, FontStyle.Bold);
            var titleFont = new WinFormsFont("Segoe UI", 13F, FontStyle.Bold);
            var previewFont = new WinFormsFont("Segoe UI", 12F, FontStyle.Bold);
            var smallFont = new WinFormsFont("Segoe UI", 9F, FontStyle.Regular);

            // Initialize all controls
            this.lblTitle = new WinFormsLabel();
            this.grpPreset = new GroupBox();
            this.pnlPresets = new FlowLayoutPanel();
            this.grpTenCoc = new GroupBox();
            this.lblPrefix = new WinFormsLabel();
            this.txtPrefix = new TextBox();
            this.lblStartNumber = new WinFormsLabel();
            this.numStartNumber = new NumericUpDown();
            this.lblSuffix = new WinFormsLabel();
            this.txtSuffix = new TextBox();
            this.chkAutoIncrement = new CheckBox();
            this.grpStyle = new GroupBox();
            this.lblStyle = new WinFormsLabel();
            this.txtStyle = new TextBox();
            this.grpPreview = new GroupBox();
            this.lblPreviewLabel = new WinFormsLabel();
            this.lblPreviewValue = new WinFormsLabel();
            this.lblNextLabel = new WinFormsLabel();
            this.lblNextValue = new WinFormsLabel();
            this.btnOK = new Button();
            this.btnCancel = new Button();
            this.btnResetCounter = new Button();

            this.SuspendLayout();

            // ==========================================
            // Form
            // ==========================================
            this.Text = "Chèn Cọc Trên Trắc Dọc";
            this.Size = new Size(480, 490);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Font = standardFont;
            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;

            // ==========================================
            // Title
            // ==========================================
            this.lblTitle.Text = "CHÈN CỌC TRÊN TRẮC DỌC";
            this.lblTitle.Font = titleFont;
            this.lblTitle.Location = new WinFormsPoint(20, 15);
            this.lblTitle.Size = new Size(430, 28);
            this.lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            this.lblTitle.ForeColor = Color.FromArgb(0, 102, 204);

            // ==========================================
            // Preset Group — Chọn nhanh loại cọc
            // ==========================================
            this.grpPreset.Text = "Chọn nhanh loại cọc";
            this.grpPreset.Font = boldFont;
            this.grpPreset.Location = new WinFormsPoint(20, 50);
            this.grpPreset.Size = new Size(430, 70);
            this.grpPreset.ForeColor = Color.Black;

            this.pnlPresets.Location = new WinFormsPoint(10, 22);
            this.pnlPresets.Size = new Size(410, 40);
            this.pnlPresets.FlowDirection = FlowDirection.LeftToRight;
            this.pnlPresets.WrapContents = true;
            this.pnlPresets.AutoScroll = false;
            this.pnlPresets.Font = smallFont;

            // Tạo các button preset
            for (int i = 0; i < _presets.Length; i++)
            {
                var btn = new Button
                {
                    Text = _presets[i].Name,
                    Size = new Size(70, 30),
                    Tag = i,
                    FlatStyle = FlatStyle.Flat,
                    Font = smallFont,
                    Margin = new Padding(2),
                    Cursor = Cursors.Hand,
                };
                btn.FlatAppearance.BorderColor = Color.FromArgb(180, 180, 180);
                btn.Click += PresetButton_Click;

                // Cọc cuối cùng "Tùy chỉnh" rộng hơn
                if (i == _presets.Length - 1)
                {
                    btn.Size = new Size(82, 30);
                }

                pnlPresets.Controls.Add(btn);
            }

            this.grpPreset.Controls.Add(pnlPresets);

            // ==========================================
            // Tên cọc Group
            // ==========================================
            this.grpTenCoc.Text = "Đặt tên cọc";
            this.grpTenCoc.Font = boldFont;
            this.grpTenCoc.Location = new WinFormsPoint(20, 130);
            this.grpTenCoc.Size = new Size(430, 130);
            this.grpTenCoc.ForeColor = Color.Black;

            // Prefix
            this.lblPrefix.Text = "Tiền tố:";
            this.lblPrefix.Font = standardFont;
            this.lblPrefix.Location = new WinFormsPoint(15, 30);
            this.lblPrefix.Size = new Size(80, 23);

            this.txtPrefix.Location = new WinFormsPoint(100, 28);
            this.txtPrefix.Size = new Size(100, 25);
            this.txtPrefix.Font = standardFont;
            this.txtPrefix.PlaceholderText = "VD: C, H, TC...";
            this.txtPrefix.TextChanged += OnNamingChanged;

            // Start Number
            this.lblStartNumber.Text = "Số bắt đầu:";
            this.lblStartNumber.Font = standardFont;
            this.lblStartNumber.Location = new WinFormsPoint(220, 30);
            this.lblStartNumber.Size = new Size(90, 23);

            this.numStartNumber.Location = new WinFormsPoint(315, 28);
            this.numStartNumber.Size = new Size(100, 25);
            this.numStartNumber.Minimum = 0;
            this.numStartNumber.Maximum = 9999;
            this.numStartNumber.Value = 1;
            this.numStartNumber.Font = standardFont;
            this.numStartNumber.ValueChanged += OnNamingChanged;

            // Suffix
            this.lblSuffix.Text = "Hậu tố:";
            this.lblSuffix.Font = standardFont;
            this.lblSuffix.Location = new WinFormsPoint(15, 65);
            this.lblSuffix.Size = new Size(80, 23);

            this.txtSuffix.Location = new WinFormsPoint(100, 63);
            this.txtSuffix.Size = new Size(200, 25);
            this.txtSuffix.Font = standardFont;
            this.txtSuffix.PlaceholderText = "VD: (Km0), TK...";
            this.txtSuffix.TextChanged += OnNamingChanged;

            // Auto-increment checkbox
            this.chkAutoIncrement.Text = "Tự động tăng số thứ tự sau mỗi lần chèn";
            this.chkAutoIncrement.Font = standardFont;
            this.chkAutoIncrement.Location = new WinFormsPoint(15, 98);
            this.chkAutoIncrement.Size = new Size(380, 23);
            this.chkAutoIncrement.Checked = true;
            this.chkAutoIncrement.CheckedChanged += OnNamingChanged;

            this.grpTenCoc.Controls.AddRange(new Control[]
            {
                lblPrefix, txtPrefix,
                lblStartNumber, numStartNumber,
                lblSuffix, txtSuffix,
                chkAutoIncrement
            });

            // ==========================================
            // Style Group
            // ==========================================
            this.grpStyle.Text = "SampleLine Style";
            this.grpStyle.Font = boldFont;
            this.grpStyle.Location = new WinFormsPoint(20, 270);
            this.grpStyle.Size = new Size(430, 60);
            this.grpStyle.ForeColor = Color.Black;

            this.lblStyle.Text = "Style:";
            this.lblStyle.Font = standardFont;
            this.lblStyle.Location = new WinFormsPoint(15, 25);
            this.lblStyle.Size = new Size(55, 23);

            this.txtStyle.Location = new WinFormsPoint(75, 23);
            this.txtStyle.Size = new Size(340, 25);
            this.txtStyle.Font = standardFont;
            this.txtStyle.Text = "Road Sample Line";
            this.txtStyle.PlaceholderText = "Tên Style SampleLine";

            this.grpStyle.Controls.AddRange(new Control[] { lblStyle, txtStyle });

            // ==========================================
            // Preview Group
            // ==========================================
            this.grpPreview.Text = "Xem trước tên cọc";
            this.grpPreview.Font = boldFont;
            this.grpPreview.Location = new WinFormsPoint(20, 340);
            this.grpPreview.Size = new Size(430, 60);
            this.grpPreview.ForeColor = Color.Black;

            this.lblPreviewLabel.Text = "Cọc tiếp theo:";
            this.lblPreviewLabel.Font = standardFont;
            this.lblPreviewLabel.Location = new WinFormsPoint(15, 25);
            this.lblPreviewLabel.Size = new Size(105, 23);

            this.lblPreviewValue.Text = "C1";
            this.lblPreviewValue.Font = previewFont;
            this.lblPreviewValue.Location = new WinFormsPoint(120, 23);
            this.lblPreviewValue.Size = new Size(140, 25);
            this.lblPreviewValue.ForeColor = Color.FromArgb(0, 130, 60);

            this.lblNextLabel.Text = "→ kế tiếp:";
            this.lblNextLabel.Font = standardFont;
            this.lblNextLabel.Location = new WinFormsPoint(270, 25);
            this.lblNextLabel.Size = new Size(80, 23);
            this.lblNextLabel.ForeColor = Color.Gray;

            this.lblNextValue.Text = "C2, C3, C4...";
            this.lblNextValue.Font = standardFont;
            this.lblNextValue.Location = new WinFormsPoint(350, 25);
            this.lblNextValue.Size = new Size(75, 23);
            this.lblNextValue.ForeColor = Color.Gray;

            this.grpPreview.Controls.AddRange(new Control[]
            {
                lblPreviewLabel, lblPreviewValue,
                lblNextLabel, lblNextValue
            });

            // ==========================================
            // Buttons
            // ==========================================
            this.btnResetCounter.Text = "Reset STT";
            this.btnResetCounter.Location = new WinFormsPoint(20, 415);
            this.btnResetCounter.Size = new Size(100, 32);
            this.btnResetCounter.Font = smallFont;
            this.btnResetCounter.FlatStyle = FlatStyle.Flat;
            this.btnResetCounter.FlatAppearance.BorderColor = Color.FromArgb(180, 180, 180);
            this.btnResetCounter.Click += BtnResetCounter_Click;

            this.btnOK.Text = "Chèn cọc";
            this.btnOK.Location = new WinFormsPoint(250, 415);
            this.btnOK.Size = new Size(95, 32);
            this.btnOK.Font = boldFont;
            this.btnOK.BackColor = Color.FromArgb(0, 120, 212);
            this.btnOK.ForeColor = Color.White;
            this.btnOK.FlatStyle = FlatStyle.Flat;
            this.btnOK.FlatAppearance.BorderSize = 0;
            this.btnOK.Cursor = Cursors.Hand;
            this.btnOK.Click += BtnOK_Click;

            this.btnCancel.Text = "Hủy";
            this.btnCancel.Location = new WinFormsPoint(355, 415);
            this.btnCancel.Size = new Size(95, 32);
            this.btnCancel.Font = standardFont;
            this.btnCancel.Click += BtnCancel_Click;

            // ==========================================
            // Add all to form
            // ==========================================
            this.Controls.AddRange(new Control[]
            {
                lblTitle,
                grpPreset,
                grpTenCoc,
                grpStyle,
                grpPreview,
                btnResetCounter,
                btnOK,
                btnCancel
            });

            this.ResumeLayout(false);
        }

        // ============================================
        // Tạo tên cọc từ pattern
        // ============================================
        /// <summary>
        /// Tạo tên cọc cho lần chèn hiện tại (dùng _currentNumber)
        /// </summary>
        public string GetCurrentStakeName()
        {
            string prefix = Prefix;
            int number = _currentNumber;
            string suffix = Suffix;

            string name = $"{prefix}{number}";
            if (!string.IsNullOrEmpty(suffix))
            {
                name = $"{prefix}{number} {suffix}";
            }
            return name;
        }

        /// <summary>
        /// Tạo tên cọc tại số cụ thể
        /// </summary>
        private string BuildName(int number)
        {
            string prefix = txtPrefix.Text.Trim();
            string suffix = txtSuffix.Text.Trim();

            string name = $"{prefix}{number}";
            if (!string.IsNullOrEmpty(suffix))
            {
                name = $"{prefix}{number} {suffix}";
            }
            return name;
        }

        /// <summary>
        /// Tăng số thứ tự lên 1 (gọi sau mỗi lần chèn cọc thành công)
        /// </summary>
        public void IncrementCounter()
        {
            if (AutoIncrement)
            {
                _currentNumber++;
            }
        }

        /// <summary>
        /// Lấy tên hiện tại rồi tự tăng (gọi nhanh trong vòng lặp)
        /// </summary>
        public string GetNameAndIncrement()
        {
            string name = GetCurrentStakeName();
            IncrementCounter();
            return name;
        }

        // ============================================
        // Event handlers
        // ============================================
        private void PresetButton_Click(object? sender, EventArgs e)
        {
            if (sender is Button btn && btn.Tag is int index)
            {
                // Highlight button được chọn
                foreach (Control ctrl in pnlPresets.Controls)
                {
                    if (ctrl is Button b)
                    {
                        b.BackColor = SystemColors.Control;
                        b.ForeColor = SystemColors.ControlText;
                    }
                }
                btn.BackColor = Color.FromArgb(0, 120, 212);
                btn.ForeColor = Color.White;

                // Cập nhật prefix/suffix
                if (index < _presets.Length - 1) // Không phải "Tùy chỉnh"
                {
                    txtPrefix.Text = _presets[index].Prefix;
                    txtSuffix.Text = _presets[index].Suffix;
                }
                _lastPresetIndex = index;
            }
        }

        private void OnNamingChanged(object? sender, EventArgs e)
        {
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            int startNum = (int)numStartNumber.Value;

            // Nếu session đang active, dùng _currentNumber
            int previewNum = _sessionActive ? _currentNumber : startNum;

            lblPreviewValue.Text = BuildName(previewNum);

            if (chkAutoIncrement.Checked)
            {
                lblNextLabel.Visible = true;
                lblNextValue.Visible = true;
                lblNextValue.Text = $"{BuildName(previewNum + 1)}, {BuildName(previewNum + 2)}...";
            }
            else
            {
                lblNextLabel.Visible = false;
                lblNextValue.Visible = false;
            }
        }

        private void BtnResetCounter_Click(object? sender, EventArgs e)
        {
            _currentNumber = (int)numStartNumber.Value;
            _sessionActive = false;
            UpdatePreview();
            MessageBox.Show(
                $"Đã reset bộ đếm về {_currentNumber}.",
                "Reset STT",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        // ============================================
        // Save / Restore
        // ============================================
        private void RestoreLastUsedValues()
        {
            txtPrefix.Text = _lastPrefix;
            txtSuffix.Text = _lastSuffix;
            txtStyle.Text = _lastSampleLineStyle;
            chkAutoIncrement.Checked = _lastAutoIncrement;

            // Nếu session đang active (chạy lại lệnh), giữ nguyên _currentNumber
            if (_sessionActive)
            {
                numStartNumber.Value = _lastStartNumber;
            }
            else
            {
                numStartNumber.Value = _lastStartNumber;
                _currentNumber = _lastStartNumber;
            }

            // Highlight preset đã chọn
            if (_lastPresetIndex >= 0 && _lastPresetIndex < pnlPresets.Controls.Count)
            {
                var btn = pnlPresets.Controls[_lastPresetIndex] as Button;
                if (btn != null)
                {
                    btn.BackColor = Color.FromArgb(0, 120, 212);
                    btn.ForeColor = Color.White;
                }
            }
        }

        private void SaveLastUsedValues()
        {
            _lastPrefix = txtPrefix.Text.Trim();
            _lastStartNumber = (int)numStartNumber.Value;
            _lastSuffix = txtSuffix.Text.Trim();
            _lastAutoIncrement = chkAutoIncrement.Checked;
            _lastSampleLineStyle = txtStyle.Text.Trim();
        }

        // ============================================
        // OK / Cancel
        // ============================================
        private void BtnOK_Click(object? sender, EventArgs e)
        {
            // Validate prefix
            if (string.IsNullOrWhiteSpace(txtPrefix.Text))
            {
                MessageBox.Show(
                    "Vui lòng nhập tiền tố cho tên cọc (VD: C, H, TC...)!",
                    "Thiếu thông tin",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                txtPrefix.Focus();
                return;
            }

            // Set properties
            Prefix = txtPrefix.Text.Trim();
            StartNumber = (int)numStartNumber.Value;
            Suffix = txtSuffix.Text.Trim();
            AutoIncrement = chkAutoIncrement.Checked;
            SampleLineStyleName = txtStyle.Text.Trim();

            // Khởi tạo counter nếu chưa có session
            if (!_sessionActive)
            {
                _currentNumber = StartNumber;
                _sessionActive = true;
            }

            // Save for next time
            SaveLastUsedValues();

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

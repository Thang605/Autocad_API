using System;
using System.Drawing;
using System.Windows.Forms;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsFont = System.Drawing.Font;
using WinFormsPoint = System.Drawing.Point;

namespace MyFirstProject.Civil_Tool
{
    /// <summary>
    /// Form nhập mô tả cho CogoPoint: Tiền tố + Tên cọc + Hậu tố
    /// </summary>
    public class ThemMoTaCogoPointForm : Form
    {
        // Static variables to remember last input values
        private static string _lastPrefix = "";
        private static string _lastPointName = "";
        private static string _lastSuffix = "";
        private static string _lastSeparator = " ";
        private static bool _lastOverwriteExisting = false;

        // Properties to return data
        public string Prefix { get; private set; } = "";
        public string PointName { get; private set; } = "";
        public string Suffix { get; private set; } = "";
        public string Separator { get; private set; } = " ";
        public bool OverwriteExisting { get; private set; } = false;
        public bool FormAccepted { get; private set; } = false;

        // UI Controls
        private GroupBox grpDescription = null!;
        private WinFormsLabel lblPrefix = null!;
        private TextBox txtPrefix = null!;
        private WinFormsLabel lblPointName = null!;
        private TextBox txtPointName = null!;
        private WinFormsLabel lblSuffix = null!;
        private TextBox txtSuffix = null!;
        private WinFormsLabel lblSeparator = null!;
        private ComboBox cmbSeparator = null!;

        private GroupBox grpOptions = null!;
        private CheckBox chkOverwrite = null!;

        private GroupBox grpPreview = null!;
        private WinFormsLabel lblPreviewResult = null!;

        private Button btnOK = null!;
        private Button btnCancel = null!;
        private Button btnHelp = null!;

        public ThemMoTaCogoPointForm()
        {
            InitializeComponent();
            RestoreLastUsedValues();
            UpdatePreview();
        }

        private void InitializeComponent()
        {
            // Initialize all controls
            this.grpDescription = new GroupBox();
            this.lblPrefix = new WinFormsLabel();
            this.txtPrefix = new TextBox();
            this.lblPointName = new WinFormsLabel();
            this.txtPointName = new TextBox();
            this.lblSuffix = new WinFormsLabel();
            this.txtSuffix = new TextBox();
            this.lblSeparator = new WinFormsLabel();
            this.cmbSeparator = new ComboBox();

            this.grpOptions = new GroupBox();
            this.chkOverwrite = new CheckBox();

            this.grpPreview = new GroupBox();
            this.lblPreviewResult = new WinFormsLabel();

            this.btnOK = new Button();
            this.btnCancel = new Button();
            this.btnHelp = new Button();

            this.SuspendLayout();

            // === Form ===
            this.Text = "Thêm Mô Tả CogoPoint";
            this.Size = new Size(520, 360);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowIcon = true;

            // === Description Group ===
            this.grpDescription.Text = "Thông tin mô tả";
            this.grpDescription.Location = new WinFormsPoint(12, 12);
            this.grpDescription.Size = new Size(480, 140);
            this.grpDescription.Font = new WinFormsFont("Segoe UI", 9f);

            // Prefix
            this.lblPrefix.Text = "Tiền tố (Prefix):";
            this.lblPrefix.Location = new WinFormsPoint(15, 28);
            this.lblPrefix.Size = new Size(110, 20);

            this.txtPrefix.Location = new WinFormsPoint(130, 25);
            this.txtPrefix.Size = new Size(335, 23);
            this.txtPrefix.PlaceholderText = "VD: KS, DINH, TN...";
            this.txtPrefix.TextChanged += OnDescriptionChanged;

            // Point Name (Tên cọc)
            this.lblPointName.Text = "Tên cọc:";
            this.lblPointName.Location = new WinFormsPoint(15, 60);
            this.lblPointName.Size = new Size(110, 20);

            this.txtPointName.Location = new WinFormsPoint(130, 57);
            this.txtPointName.Size = new Size(335, 23);
            this.txtPointName.PlaceholderText = "VD: C1, H1, KM0+100...";
            this.txtPointName.TextChanged += OnDescriptionChanged;

            // Suffix
            this.lblSuffix.Text = "Hậu tố (Suffix):";
            this.lblSuffix.Location = new WinFormsPoint(15, 92);
            this.lblSuffix.Size = new Size(110, 20);

            this.txtSuffix.Location = new WinFormsPoint(130, 89);
            this.txtSuffix.Size = new Size(335, 23);
            this.txtSuffix.PlaceholderText = "VD: TK, HT, T, P...";
            this.txtSuffix.TextChanged += OnDescriptionChanged;

            // Separator
            this.lblSeparator.Text = "Ký tự nối:";
            this.lblSeparator.Location = new WinFormsPoint(15, 120);
            this.lblSeparator.Size = new Size(110, 20);

            this.cmbSeparator.Location = new WinFormsPoint(130, 117);
            this.cmbSeparator.Size = new Size(120, 23);
            this.cmbSeparator.DropDownStyle = ComboBoxStyle.DropDownList;
            this.cmbSeparator.Items.AddRange(new object[]
            {
                "Dấu cách ( )",
                "Gạch ngang (-)",
                "Gạch dưới (_)",
                "Dấu chấm (.)",
                "Không có"
            });
            this.cmbSeparator.SelectedIndex = 0;
            this.cmbSeparator.SelectedIndexChanged += OnDescriptionChanged;

            // Add controls to Description group
            this.grpDescription.Controls.AddRange(new Control[] {
                lblPrefix, txtPrefix,
                lblPointName, txtPointName,
                lblSuffix, txtSuffix,
                lblSeparator, cmbSeparator
            });

            // === Options Group ===
            this.grpOptions.Text = "Tùy chọn";
            this.grpOptions.Location = new WinFormsPoint(12, 160);
            this.grpOptions.Size = new Size(480, 50);
            this.grpOptions.Font = new WinFormsFont("Segoe UI", 9f);

            this.chkOverwrite.Text = "Ghi đè mô tả hiện có (nếu không chọn, sẽ nối thêm vào mô tả cũ)";
            this.chkOverwrite.Location = new WinFormsPoint(15, 22);
            this.chkOverwrite.Size = new Size(450, 20);
            this.chkOverwrite.Checked = false;

            this.grpOptions.Controls.Add(chkOverwrite);

            // === Preview Group ===
            this.grpPreview.Text = "Xem trước mô tả";
            this.grpPreview.Location = new WinFormsPoint(12, 218);
            this.grpPreview.Size = new Size(480, 55);
            this.grpPreview.Font = new WinFormsFont("Segoe UI", 9f);

            this.lblPreviewResult.Location = new WinFormsPoint(15, 22);
            this.lblPreviewResult.Size = new Size(450, 25);
            this.lblPreviewResult.Font = new WinFormsFont("Segoe UI", 10f, FontStyle.Bold);
            this.lblPreviewResult.ForeColor = Color.FromArgb(0, 100, 180);
            this.lblPreviewResult.Text = "(trống)";

            this.grpPreview.Controls.Add(lblPreviewResult);

            // === Buttons ===
            this.btnOK.Text = "OK";
            this.btnOK.Location = new WinFormsPoint(248, 285);
            this.btnOK.Size = new Size(80, 27);
            this.btnOK.Click += BtnOK_Click;

            this.btnCancel.Text = "Cancel";
            this.btnCancel.Location = new WinFormsPoint(335, 285);
            this.btnCancel.Size = new Size(80, 27);
            this.btnCancel.Click += BtnCancel_Click;

            this.btnHelp.Text = "Help";
            this.btnHelp.Location = new WinFormsPoint(422, 285);
            this.btnHelp.Size = new Size(70, 27);
            this.btnHelp.Click += BtnHelp_Click;

            // Add all to form
            this.Controls.AddRange(new Control[] {
                grpDescription,
                grpOptions,
                grpPreview,
                btnOK, btnCancel, btnHelp
            });

            this.ResumeLayout(false);
        }

        private string GetSeparator()
        {
            return cmbSeparator.SelectedIndex switch
            {
                0 => " ",
                1 => "-",
                2 => "_",
                3 => ".",
                4 => "",
                _ => " "
            };
        }

        /// <summary>
        /// Tạo mô tả từ Prefix + PointName + Suffix
        /// </summary>
        public string GenerateDescription()
        {
            string sep = Separator;
            var parts = new System.Collections.Generic.List<string>();

            if (!string.IsNullOrEmpty(Prefix)) parts.Add(Prefix);
            if (!string.IsNullOrEmpty(PointName)) parts.Add(PointName);
            if (!string.IsNullOrEmpty(Suffix)) parts.Add(Suffix);

            return string.Join(sep, parts);
        }

        /// <summary>
        /// Tạo mô tả từ Prefix + tên cọc tùy chỉnh + Suffix
        /// (dùng khi mỗi cọc có tên riêng khác)
        /// </summary>
        public string GenerateDescriptionWithCustomName(string customPointName)
        {
            string sep = Separator;
            var parts = new System.Collections.Generic.List<string>();

            if (!string.IsNullOrEmpty(Prefix)) parts.Add(Prefix);
            if (!string.IsNullOrEmpty(customPointName)) parts.Add(customPointName);
            if (!string.IsNullOrEmpty(Suffix)) parts.Add(Suffix);

            return string.Join(sep, parts);
        }

        private void UpdatePreview()
        {
            string sep = GetSeparator();
            var parts = new System.Collections.Generic.List<string>();

            string prefix = txtPrefix.Text.Trim();
            string pointName = txtPointName.Text.Trim();
            string suffix = txtSuffix.Text.Trim();

            if (!string.IsNullOrEmpty(prefix)) parts.Add(prefix);
            if (!string.IsNullOrEmpty(pointName)) parts.Add(pointName);
            else parts.Add("<Tên cọc>");
            if (!string.IsNullOrEmpty(suffix)) parts.Add(suffix);

            string preview = string.Join(sep, parts);
            lblPreviewResult.Text = string.IsNullOrWhiteSpace(preview) ? "(trống)" : preview;
        }

        private void OnDescriptionChanged(object? sender, EventArgs e)
        {
            UpdatePreview();
        }

        private void RestoreLastUsedValues()
        {
            if (!string.IsNullOrEmpty(_lastPrefix))
                txtPrefix.Text = _lastPrefix;
            if (!string.IsNullOrEmpty(_lastPointName))
                txtPointName.Text = _lastPointName;
            if (!string.IsNullOrEmpty(_lastSuffix))
                txtSuffix.Text = _lastSuffix;

            // Restore separator
            int sepIndex = _lastSeparator switch
            {
                " " => 0,
                "-" => 1,
                "_" => 2,
                "." => 3,
                "" => 4,
                _ => 0
            };
            cmbSeparator.SelectedIndex = sepIndex;

            chkOverwrite.Checked = _lastOverwriteExisting;
        }

        private void SaveLastUsedValues()
        {
            _lastPrefix = txtPrefix.Text.Trim();
            _lastPointName = txtPointName.Text.Trim();
            _lastSuffix = txtSuffix.Text.Trim();
            _lastSeparator = GetSeparator();
            _lastOverwriteExisting = chkOverwrite.Checked;
        }

        private void BtnOK_Click(object? sender, EventArgs e)
        {
            // Validate: at least one field must have value
            if (string.IsNullOrWhiteSpace(txtPrefix.Text) &&
                string.IsNullOrWhiteSpace(txtPointName.Text) &&
                string.IsNullOrWhiteSpace(txtSuffix.Text))
            {
                MessageBox.Show(
                    "Vui lòng nhập ít nhất một thông tin (Tiền tố, Tên cọc, hoặc Hậu tố)!",
                    "Lỗi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                txtPrefix.Focus();
                return;
            }

            // Get values
            Prefix = txtPrefix.Text.Trim();
            PointName = txtPointName.Text.Trim();
            Suffix = txtSuffix.Text.Trim();
            Separator = GetSeparator();
            OverwriteExisting = chkOverwrite.Checked;

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

        private void BtnHelp_Click(object? sender, EventArgs e)
        {
            string helpText = @"Hướng dẫn sử dụng Thêm Mô Tả CogoPoint:

1. Tiền tố (Prefix):
   - Ký tự đứng trước tên cọc
   - VD: KS, DINH, TN, TC...

2. Tên cọc:
   - Tên chung cho tất cả các cọc được chọn
   - Nếu để trống, sẽ dùng PointName hiện có
   - VD: C1, H1, KM0+100...

3. Hậu tố (Suffix):
   - Ký tự đứng sau tên cọc
   - VD: TK, HT, T, P...

4. Ký tự nối:
   - Ký tự dùng để nối giữa các phần
   - VD: dấu cách, gạch ngang, gạch dưới...

5. Ghi đè mô tả:
   - Tích chọn: Thay thế mô tả cũ hoàn toàn
   - Không tích: Nối thêm vào mô tả cũ

Ví dụ: Prefix='KS', Tên cọc='C1', Suffix='TK'
→ Mô tả = 'KS C1 TK' (dấu cách)
→ Mô tả = 'KS-C1-TK' (gạch ngang)";

            MessageBox.Show(helpText, "Trợ giúp - Thêm Mô Tả CogoPoint", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}

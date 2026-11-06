using System;
using System.Drawing;
using System.Windows.Forms;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsFont = System.Drawing.Font;
using WinFormsPoint = System.Drawing.Point;

namespace MyFirstProject.Civil_Tool_2
{
    /// <summary>
    /// Form nhập template tên cho lệnh Đổi Tên Parcel
    /// </summary>
    public class DoiTenParcelForm : Form
    {
        // Static variables to remember last input values
        private static string _lastNameTemplate = "";
        private static int _lastNumberStyleIndex = 0;
        private static int _lastStartingNumber = 1;
        private static int _lastIncrementValue = 1;

        // Properties to return data
        public string NameTemplate { get; private set; } = "";
        public NumberStyle SelectedNumberStyle { get; private set; } = NumberStyle.Arabic;
        public int StartingNumber { get; private set; } = 1;
        public int IncrementValue { get; private set; } = 1;
        public bool FormAccepted { get; private set; } = false;

        // Number style enum
        public enum NumberStyle
        {
            Arabic,      // 1, 2, 3...
            RomanUpper,  // I, II, III...
            RomanLower,  // i, ii, iii...
            LetterUpper, // A, B, C...
            LetterLower  // a, b, c...
        }

        // Property field enum for template
        public enum PropertyField
        {
            NextCounter,
            ParcelName,
            ParcelNumber,
            Area
        }

        // UI Controls - Name formatting template group
        private GroupBox grpNameTemplate = null!;
        private WinFormsLabel lblPropertyFields = null!;
        private ComboBox cmbPropertyFields = null!;
        private Button btnInsert = null!;
        private WinFormsLabel lblName = null!;
        private TextBox txtName = null!;

        // UI Controls - Incremental number format group
        private GroupBox grpNumberFormat = null!;
        private WinFormsLabel lblNumberStyle = null!;
        private ComboBox cmbNumberStyle = null!;
        private WinFormsLabel lblStartingNumber = null!;
        private NumericUpDown numStartingNumber = null!;
        private WinFormsLabel lblIncrementValue = null!;
        private NumericUpDown numIncrementValue = null!;

        // UI Controls - Preview group
        private GroupBox grpPreview = null!;
        private WinFormsLabel lblPreviewName = null!;
        private TextBox txtPreviewName = null!;

        // Buttons
        private Button btnOK = null!;
        private Button btnCancel = null!;
        private Button btnHelp = null!;

        public DoiTenParcelForm()
        {
            InitializeComponent();
            LoadPropertyFields();
            LoadNumberStyles();
            RestoreLastUsedValues();
            UpdatePreview();
        }

        private void InitializeComponent()
        {
            // Initialize controls
            this.grpNameTemplate = new GroupBox();
            this.lblPropertyFields = new WinFormsLabel();
            this.cmbPropertyFields = new ComboBox();
            this.btnInsert = new Button();
            this.lblName = new WinFormsLabel();
            this.txtName = new TextBox();

            this.grpNumberFormat = new GroupBox();
            this.lblNumberStyle = new WinFormsLabel();
            this.cmbNumberStyle = new ComboBox();
            this.lblStartingNumber = new WinFormsLabel();
            this.numStartingNumber = new NumericUpDown();
            this.lblIncrementValue = new WinFormsLabel();
            this.numIncrementValue = new NumericUpDown();

            this.grpPreview = new GroupBox();
            this.lblPreviewName = new WinFormsLabel();
            this.txtPreviewName = new TextBox();

            this.btnOK = new Button();
            this.btnCancel = new Button();
            this.btnHelp = new Button();

            this.SuspendLayout();

            // Form
            this.Text = "Đổi Tên Parcel - Name Template";
            this.Size = new Size(520, 380);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowIcon = true;

            // === Name formatting template Group ===
            this.grpNameTemplate.Text = "Mẫu định dạng tên (Name Template)";
            this.grpNameTemplate.Location = new WinFormsPoint(12, 12);
            this.grpNameTemplate.Size = new Size(480, 100);

            // Property fields label
            this.lblPropertyFields.Text = "Trường thuộc tính:";
            this.lblPropertyFields.Location = new WinFormsPoint(15, 25);
            this.lblPropertyFields.Size = new Size(120, 20);

            // Property fields combobox
            this.cmbPropertyFields.Location = new WinFormsPoint(15, 45);
            this.cmbPropertyFields.Size = new Size(350, 23);
            this.cmbPropertyFields.DropDownStyle = ComboBoxStyle.DropDownList;

            // Insert button
            this.btnInsert.Text = "Chèn";
            this.btnInsert.Location = new WinFormsPoint(375, 44);
            this.btnInsert.Size = new Size(90, 25);
            this.btnInsert.Click += BtnInsert_Click;

            // Name label
            this.lblName.Text = "Tên:";
            this.lblName.Location = new WinFormsPoint(15, 75);
            this.lblName.Size = new Size(50, 20);

            // Name textbox
            this.txtName.Location = new WinFormsPoint(65, 72);
            this.txtName.Size = new Size(400, 23);
            this.txtName.TextChanged += TxtName_TextChanged;

            // Add controls to Name Template group
            this.grpNameTemplate.Controls.AddRange(new Control[] {
                lblPropertyFields, cmbPropertyFields, btnInsert,
                lblName, txtName
            });

            // === Incremental number format Group ===
            this.grpNumberFormat.Text = "Định dạng số tăng dần";
            this.grpNumberFormat.Location = new WinFormsPoint(12, 120);
            this.grpNumberFormat.Size = new Size(480, 100);

            // Number style label
            this.lblNumberStyle.Text = "Kiểu số:";
            this.lblNumberStyle.Location = new WinFormsPoint(15, 22);
            this.lblNumberStyle.Size = new Size(100, 20);

            // Number style combobox
            this.cmbNumberStyle.Location = new WinFormsPoint(15, 42);
            this.cmbNumberStyle.Size = new Size(450, 23);
            this.cmbNumberStyle.DropDownStyle = ComboBoxStyle.DropDownList;
            this.cmbNumberStyle.SelectedIndexChanged += CmbNumberStyle_SelectedIndexChanged;

            // Starting number label
            this.lblStartingNumber.Text = "Số bắt đầu:";
            this.lblStartingNumber.Location = new WinFormsPoint(15, 72);
            this.lblStartingNumber.Size = new Size(80, 20);

            // Starting number input
            this.numStartingNumber.Location = new WinFormsPoint(100, 70);
            this.numStartingNumber.Size = new Size(80, 23);
            this.numStartingNumber.Minimum = 1;
            this.numStartingNumber.Maximum = 99999;
            this.numStartingNumber.Value = 1;
            this.numStartingNumber.ValueChanged += NumStartingNumber_ValueChanged;

            // Increment value label
            this.lblIncrementValue.Text = "Bước nhảy:";
            this.lblIncrementValue.Location = new WinFormsPoint(250, 72);
            this.lblIncrementValue.Size = new Size(80, 20);

            // Increment value input
            this.numIncrementValue.Location = new WinFormsPoint(340, 70);
            this.numIncrementValue.Size = new Size(80, 23);
            this.numIncrementValue.Minimum = 1;
            this.numIncrementValue.Maximum = 100;
            this.numIncrementValue.Value = 1;

            // Add controls to Number Format group
            this.grpNumberFormat.Controls.AddRange(new Control[] {
                lblNumberStyle, cmbNumberStyle,
                lblStartingNumber, numStartingNumber,
                lblIncrementValue, numIncrementValue
            });

            // === Preview Group ===
            this.grpPreview.Text = "Xem trước tên";
            this.grpPreview.Location = new WinFormsPoint(12, 228);
            this.grpPreview.Size = new Size(480, 60);

            // Preview name label
            this.lblPreviewName.Text = "Ví dụ:";
            this.lblPreviewName.Location = new WinFormsPoint(15, 28);
            this.lblPreviewName.Size = new Size(50, 20);

            // Preview textbox (read-only)
            this.txtPreviewName.Location = new WinFormsPoint(65, 25);
            this.txtPreviewName.Size = new Size(400, 23);
            this.txtPreviewName.ReadOnly = true;
            this.txtPreviewName.BackColor = Color.LightYellow;
            this.txtPreviewName.Font = new WinFormsFont("Segoe UI", 9, FontStyle.Bold);

            // Add controls to Preview group
            this.grpPreview.Controls.AddRange(new Control[] {
                lblPreviewName, txtPreviewName
            });

            // === Buttons ===
            // OK Button
            this.btnOK.Text = "OK";
            this.btnOK.Location = new WinFormsPoint(238, 300);
            this.btnOK.Size = new Size(80, 30);
            this.btnOK.Click += BtnOK_Click;

            // Cancel Button
            this.btnCancel.Text = "Hủy";
            this.btnCancel.Location = new WinFormsPoint(325, 300);
            this.btnCancel.Size = new Size(80, 30);
            this.btnCancel.Click += BtnCancel_Click;

            // Help Button
            this.btnHelp.Text = "Trợ giúp";
            this.btnHelp.Location = new WinFormsPoint(412, 300);
            this.btnHelp.Size = new Size(80, 30);
            this.btnHelp.Click += BtnHelp_Click;

            // Add controls to form
            this.Controls.AddRange(new Control[] {
                grpNameTemplate,
                grpNumberFormat,
                grpPreview,
                btnOK, btnCancel, btnHelp
            });

            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;

            this.ResumeLayout(false);
        }

        private void LoadPropertyFields()
        {
            cmbPropertyFields.Items.Clear();
            cmbPropertyFields.Items.Add("Số thứ tự (Next Counter)");
            cmbPropertyFields.Items.Add("Tên Parcel hiện tại (Parcel Name)");
            cmbPropertyFields.Items.Add("Số Parcel (Parcel Number)");
            cmbPropertyFields.Items.Add("Diện tích (Area)");
            cmbPropertyFields.SelectedIndex = 0;
        }

        private void LoadNumberStyles()
        {
            cmbNumberStyle.Items.Clear();
            cmbNumberStyle.Items.Add("Số Ả Rập: 1, 2, 3...");
            cmbNumberStyle.Items.Add("Số La Mã hoa: I, II, III...");
            cmbNumberStyle.Items.Add("Số La Mã thường: i, ii, iii...");
            cmbNumberStyle.Items.Add("Chữ cái hoa: A, B, C...");
            cmbNumberStyle.Items.Add("Chữ cái thường: a, b, c...");
            cmbNumberStyle.SelectedIndex = 0;
        }

        private void RestoreLastUsedValues()
        {
            if (!string.IsNullOrEmpty(_lastNameTemplate))
                txtName.Text = _lastNameTemplate;

            if (_lastNumberStyleIndex >= 0 && _lastNumberStyleIndex < cmbNumberStyle.Items.Count)
                cmbNumberStyle.SelectedIndex = _lastNumberStyleIndex;

            numStartingNumber.Value = _lastStartingNumber;
            numIncrementValue.Value = _lastIncrementValue;
        }

        private void SaveLastUsedValues()
        {
            _lastNameTemplate = txtName.Text;
            _lastNumberStyleIndex = cmbNumberStyle.SelectedIndex;
            _lastStartingNumber = (int)numStartingNumber.Value;
            _lastIncrementValue = (int)numIncrementValue.Value;
        }

        private void UpdatePreview()
        {
            if (string.IsNullOrEmpty(txtName.Text))
            {
                txtPreviewName.Text = "(Nhập mẫu tên ở trên)";
                return;
            }

            // Generate preview with sample data
            string preview = GenerateName(
                counter: (int)numStartingNumber.Value,
                parcelName: "Parcel-001",
                parcelNumber: 1,
                area: 1234.56
            );

            txtPreviewName.Text = preview;
        }

        private void TxtName_TextChanged(object? sender, EventArgs e)
        {
            UpdatePreview();
        }

        private void CmbNumberStyle_SelectedIndexChanged(object? sender, EventArgs e)
        {
            UpdatePreview();
        }

        private void NumStartingNumber_ValueChanged(object? sender, EventArgs e)
        {
            UpdatePreview();
        }

        private void BtnInsert_Click(object? sender, EventArgs e)
        {
            string fieldCode = cmbPropertyFields.SelectedIndex switch
            {
                0 => "<[Next Counter]>",
                1 => "<[Parcel Name]>",
                2 => "<[Parcel Number]>",
                3 => "<[Area]>",
                _ => ""
            };

            // Insert at cursor position
            int cursorPos = txtName.SelectionStart;
            txtName.Text = txtName.Text.Insert(cursorPos, fieldCode);
            txtName.SelectionStart = cursorPos + fieldCode.Length;
            txtName.Focus();
        }

        private void BtnOK_Click(object? sender, EventArgs e)
        {
            // Validate
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Vui lòng nhập mẫu tên (template)!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtName.Focus();
                return;
            }

            // Get values
            NameTemplate = txtName.Text;
            SelectedNumberStyle = (NumberStyle)cmbNumberStyle.SelectedIndex;
            StartingNumber = (int)numStartingNumber.Value;
            IncrementValue = (int)numIncrementValue.Value;

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
            string helpText = @"HƯỚNG DẪN SỬ DỤNG - ĐỔI TÊN PARCEL

1. MẪU ĐỊNH DẠNG TÊN:
   • Next Counter: Số thứ tự tự động tăng dần
   • Parcel Name: Tên hiện tại của Parcel
   • Parcel Number: Số thứ tự Parcel
   • Area: Diện tích Parcel (m²)

2. CÁCH SỬ DỤNG:
   • Nhập mẫu tên mong muốn vào ô 'Tên'
   • Nhấn nút 'Chèn' để thêm trường thuộc tính
   • Xem trước kết quả ở phần 'Xem trước tên'

3. VÍ DỤ:
   • Mẫu: 'Lô <[Next Counter]>' → Lô 1, Lô 2, Lô 3...
   • Mẫu: 'KDC-<[Next Counter]>-<[Area]>m2' → KDC-1-1234.56m2

4. ĐỊNH DẠNG SỐ:
   • Số Ả Rập: 1, 2, 3...
   • Số La Mã: I, II, III... hoặc i, ii, iii...
   • Chữ cái: A, B, C... hoặc a, b, c...

5. SỬ DỤNG:
   • Sau khi nhấn OK, chọn từng Parcel để đổi tên
   • Tên Parcel sẽ thay đổi ngay lập tức
   • Nhấn ESC để kết thúc";

            MessageBox.Show(helpText, "Trợ giúp - Đổi Tên Parcel", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// Tạo tên mới cho Parcel dựa trên template
        /// </summary>
        public string GenerateName(int counter, string parcelName, int parcelNumber, double area)
        {
            string result = NameTemplate;

            // Replace Next Counter with formatted number
            string counterStr = FormatNumber(counter);
            result = result.Replace("<[Next Counter]>", counterStr);

            // Replace other fields
            result = result.Replace("<[Parcel Name]>", parcelName);
            result = result.Replace("<[Parcel Number]>", parcelNumber.ToString());
            result = result.Replace("<[Area]>", area.ToString("F2"));

            return result;
        }

        private string FormatNumber(int num)
        {
            return SelectedNumberStyle switch
            {
                NumberStyle.Arabic => num.ToString(),
                NumberStyle.RomanUpper => ToRoman(num).ToUpper(),
                NumberStyle.RomanLower => ToRoman(num).ToLower(),
                NumberStyle.LetterUpper => ToLetter(num).ToUpper(),
                NumberStyle.LetterLower => ToLetter(num).ToLower(),
                _ => num.ToString()
            };
        }

        private static string ToRoman(int number)
        {
            if (number < 1) return "";
            if (number >= 1000) return "M" + ToRoman(number - 1000);
            if (number >= 900) return "CM" + ToRoman(number - 900);
            if (number >= 500) return "D" + ToRoman(number - 500);
            if (number >= 400) return "CD" + ToRoman(number - 400);
            if (number >= 100) return "C" + ToRoman(number - 100);
            if (number >= 90) return "XC" + ToRoman(number - 90);
            if (number >= 50) return "L" + ToRoman(number - 50);
            if (number >= 40) return "XL" + ToRoman(number - 40);
            if (number >= 10) return "X" + ToRoman(number - 10);
            if (number >= 9) return "IX" + ToRoman(number - 9);
            if (number >= 5) return "V" + ToRoman(number - 5);
            if (number >= 4) return "IV" + ToRoman(number - 4);
            if (number >= 1) return "I" + ToRoman(number - 1);
            return "";
        }

        private static string ToLetter(int number)
        {
            string result = "";
            while (number > 0)
            {
                number--;
                result = (char)('A' + number % 26) + result;
                number /= 26;
            }
            return result;
        }
    }
}

namespace MyFirstProject.Civil_Tool
{
    partial class BoTriCongDungForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.grpStandard = new System.Windows.Forms.GroupBox();
            this.lblTerrain = new System.Windows.Forms.Label();
            this.cmbTerrain = new System.Windows.Forms.ComboBox();
            this.lblVtk = new System.Windows.Forms.Label();
            this.cmbVtk = new System.Windows.Forms.ComboBox();
            this.lblStandard = new System.Windows.Forms.Label();
            this.cmbStandard = new System.Windows.Forms.ComboBox();
            this.grpObjects = new System.Windows.Forms.GroupBox();
            this.btnPickPvi = new System.Windows.Forms.Button();
            this.cmbPvi = new System.Windows.Forms.ComboBox();
            this.lblPviSelect = new System.Windows.Forms.Label();
            this.btnPickProfile = new System.Windows.Forms.Button();
            this.cmbProfile = new System.Windows.Forms.ComboBox();
            this.lblProfile = new System.Windows.Forms.Label();
            this.btnPickProfileView = new System.Windows.Forms.Button();
            this.txtProfileViewName = new System.Windows.Forms.TextBox();
            this.lblProfileView = new System.Windows.Forms.Label();
            this.grpPviInfo = new System.Windows.Forms.GroupBox();
            this.lblGrades = new System.Windows.Forms.Label();
            this.lblPviDetails = new System.Windows.Forms.Label();
            this.grpSuggest = new System.Windows.Forms.GroupBox();
            this.lblWarningThreshold = new System.Windows.Forms.Label();
            this.lblSuggestLength = new System.Windows.Forms.Label();
            this.lblSuggestRadius = new System.Windows.Forms.Label();
            this.lblSuggestTitle = new System.Windows.Forms.Label();
            this.grpInput = new System.Windows.Forms.GroupBox();
            this.lblValidation = new System.Windows.Forms.Label();
            this.lblCalculatedVal = new System.Windows.Forms.Label();
            this.txtLength = new System.Windows.Forms.TextBox();
            this.txtRadius = new System.Windows.Forms.TextBox();
            this.radLength = new System.Windows.Forms.RadioButton();
            this.radRadius = new System.Windows.Forms.RadioButton();
            this.btnApply = new System.Windows.Forms.Button();
            this.btnPickPviNext = new System.Windows.Forms.Button();
            this.btnApplyAndClose = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnApplyAll = new System.Windows.Forms.Button();
            this.grpStandard.SuspendLayout();
            this.grpObjects.SuspendLayout();
            this.grpPviInfo.SuspendLayout();
            this.grpSuggest.SuspendLayout();
            this.grpInput.SuspendLayout();
            this.SuspendLayout();
            // 
            // grpStandard
            // 
            this.grpStandard.Controls.Add(this.lblTerrain);
            this.grpStandard.Controls.Add(this.cmbTerrain);
            this.grpStandard.Controls.Add(this.lblVtk);
            this.grpStandard.Controls.Add(this.cmbVtk);
            this.grpStandard.Controls.Add(this.lblStandard);
            this.grpStandard.Controls.Add(this.cmbStandard);
            this.grpStandard.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.grpStandard.ForeColor = System.Drawing.Color.Navy;
            this.grpStandard.Location = new System.Drawing.Point(12, 10);
            this.grpStandard.Name = "grpStandard";
            this.grpStandard.Size = new System.Drawing.Size(536, 75);
            this.grpStandard.TabIndex = 0;
            this.grpStandard.TabStop = false;
            this.grpStandard.Text = "1. Tiêu chuẩn thiết kế & Vận tốc";
            // 
            // lblTerrain
            // 
            this.lblTerrain.AutoSize = true;
            this.lblTerrain.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.lblTerrain.ForeColor = System.Drawing.Color.Black;
            this.lblTerrain.Location = new System.Drawing.Point(370, 21);
            this.lblTerrain.Name = "lblTerrain";
            this.lblTerrain.Size = new System.Drawing.Size(54, 15);
            this.lblTerrain.TabIndex = 4;
            this.lblTerrain.Text = "Địa hình:";
            // 
            // cmbTerrain
            // 
            this.cmbTerrain.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbTerrain.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.cmbTerrain.FormattingEnabled = true;
            this.cmbTerrain.Items.AddRange(new object[] {
            "Đồng bằng",
            "Đồi",
            "Núi"});
            this.cmbTerrain.Location = new System.Drawing.Point(370, 40);
            this.cmbTerrain.Name = "cmbTerrain";
            this.cmbTerrain.Size = new System.Drawing.Size(150, 23);
            this.cmbTerrain.TabIndex = 5;
            // 
            // lblVtk
            // 
            this.lblVtk.AutoSize = true;
            this.lblVtk.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.lblVtk.ForeColor = System.Drawing.Color.Black;
            this.lblVtk.Location = new System.Drawing.Point(265, 21);
            this.lblVtk.Name = "lblVtk";
            this.lblVtk.Size = new System.Drawing.Size(85, 15);
            this.lblVtk.TabIndex = 2;
            this.lblVtk.Text = "Vtk (km/h):";
            // 
            // cmbVtk
            // 
            this.cmbVtk.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbVtk.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.cmbVtk.FormattingEnabled = true;
            this.cmbVtk.Location = new System.Drawing.Point(265, 40);
            this.cmbVtk.Name = "cmbVtk";
            this.cmbVtk.Size = new System.Drawing.Size(95, 23);
            this.cmbVtk.TabIndex = 3;
            // 
            // lblStandard
            // 
            this.lblStandard.AutoSize = true;
            this.lblStandard.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.lblStandard.ForeColor = System.Drawing.Color.Black;
            this.lblStandard.Location = new System.Drawing.Point(12, 21);
            this.lblStandard.Name = "lblStandard";
            this.lblStandard.Size = new System.Drawing.Size(65, 15);
            this.lblStandard.TabIndex = 0;
            this.lblStandard.Text = "Tiêu chuẩn:";
            // 
            // cmbStandard
            // 
            this.cmbStandard.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbStandard.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.cmbStandard.FormattingEnabled = true;
            this.cmbStandard.Location = new System.Drawing.Point(12, 40);
            this.cmbStandard.Name = "cmbStandard";
            this.cmbStandard.Size = new System.Drawing.Size(245, 23);
            this.cmbStandard.TabIndex = 1;
            // 
            // grpObjects
            // 
            this.grpObjects.Controls.Add(this.btnPickPvi);
            this.grpObjects.Controls.Add(this.cmbPvi);
            this.grpObjects.Controls.Add(this.lblPviSelect);
            this.grpObjects.Controls.Add(this.btnPickProfile);
            this.grpObjects.Controls.Add(this.cmbProfile);
            this.grpObjects.Controls.Add(this.lblProfile);
            this.grpObjects.Controls.Add(this.btnPickProfileView);
            this.grpObjects.Controls.Add(this.txtProfileViewName);
            this.grpObjects.Controls.Add(this.lblProfileView);
            this.grpObjects.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.grpObjects.ForeColor = System.Drawing.Color.Navy;
            this.grpObjects.Location = new System.Drawing.Point(12, 90);
            this.grpObjects.Name = "grpObjects";
            this.grpObjects.Size = new System.Drawing.Size(536, 115);
            this.grpObjects.TabIndex = 1;
            this.grpObjects.TabStop = false;
            this.grpObjects.Text = "2. Chọn Trắc dọc, Đường đỏ & Đỉnh PVI";
            // 
            // btnPickPvi
            // 
            this.btnPickPvi.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.btnPickPvi.ForeColor = System.Drawing.Color.Black;
            this.btnPickPvi.Location = new System.Drawing.Point(395, 80);
            this.btnPickPvi.Name = "btnPickPvi";
            this.btnPickPvi.Size = new System.Drawing.Size(125, 26);
            this.btnPickPvi.TabIndex = 8;
            this.btnPickPvi.Text = "Pick PVI";
            this.btnPickPvi.UseVisualStyleBackColor = true;
            // 
            // cmbPvi
            // 
            this.cmbPvi.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbPvi.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.cmbPvi.FormattingEnabled = true;
            this.cmbPvi.Location = new System.Drawing.Point(115, 81);
            this.cmbPvi.Name = "cmbPvi";
            this.cmbPvi.Size = new System.Drawing.Size(270, 23);
            this.cmbPvi.TabIndex = 7;
            // 
            // lblPviSelect
            // 
            this.lblPviSelect.AutoSize = true;
            this.lblPviSelect.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.lblPviSelect.ForeColor = System.Drawing.Color.Black;
            this.lblPviSelect.Location = new System.Drawing.Point(12, 84);
            this.lblPviSelect.Name = "lblPviSelect";
            this.lblPviSelect.Size = new System.Drawing.Size(58, 15);
            this.lblPviSelect.TabIndex = 6;
            this.lblPviSelect.Text = "Đỉnh PVI:";
            // 
            // btnPickProfile
            // 
            this.btnPickProfile.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.btnPickProfile.ForeColor = System.Drawing.Color.Black;
            this.btnPickProfile.Location = new System.Drawing.Point(395, 50);
            this.btnPickProfile.Name = "btnPickProfile";
            this.btnPickProfile.Size = new System.Drawing.Size(125, 26);
            this.btnPickProfile.TabIndex = 5;
            this.btnPickProfile.Text = "Pick Profile";
            this.btnPickProfile.UseVisualStyleBackColor = true;
            // 
            // cmbProfile
            // 
            this.cmbProfile.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbProfile.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.cmbProfile.FormattingEnabled = true;
            this.cmbProfile.Location = new System.Drawing.Point(115, 51);
            this.cmbProfile.Name = "cmbProfile";
            this.cmbProfile.Size = new System.Drawing.Size(270, 23);
            this.cmbProfile.TabIndex = 4;
            // 
            // lblProfile
            // 
            this.lblProfile.AutoSize = true;
            this.lblProfile.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.lblProfile.ForeColor = System.Drawing.Color.Black;
            this.lblProfile.Location = new System.Drawing.Point(12, 54);
            this.lblProfile.Name = "lblProfile";
            this.lblProfile.Size = new System.Drawing.Size(100, 15);
            this.lblProfile.TabIndex = 3;
            this.lblProfile.Text = "Profile (Đường đỏ):";
            // 
            // btnPickProfileView
            // 
            this.btnPickProfileView.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.btnPickProfileView.ForeColor = System.Drawing.Color.Black;
            this.btnPickProfileView.Location = new System.Drawing.Point(395, 20);
            this.btnPickProfileView.Name = "btnPickProfileView";
            this.btnPickProfileView.Size = new System.Drawing.Size(125, 26);
            this.btnPickProfileView.TabIndex = 2;
            this.btnPickProfileView.Text = "Pick Trắc dọc";
            this.btnPickProfileView.UseVisualStyleBackColor = true;
            // 
            // txtProfileViewName
            // 
            this.txtProfileViewName.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.txtProfileViewName.Location = new System.Drawing.Point(115, 21);
            this.txtProfileViewName.Name = "txtProfileViewName";
            this.txtProfileViewName.ReadOnly = true;
            this.txtProfileViewName.Size = new System.Drawing.Size(270, 23);
            this.txtProfileViewName.TabIndex = 1;
            // 
            // lblProfileView
            // 
            this.lblProfileView.AutoSize = true;
            this.lblProfileView.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.lblProfileView.ForeColor = System.Drawing.Color.Black;
            this.lblProfileView.Location = new System.Drawing.Point(12, 24);
            this.lblProfileView.Name = "lblProfileView";
            this.lblProfileView.Size = new System.Drawing.Size(56, 15);
            this.lblProfileView.TabIndex = 0;
            this.lblProfileView.Text = "Trắc dọc:";
            // 
            // grpPviInfo
            // 
            this.grpPviInfo.Controls.Add(this.lblGrades);
            this.grpPviInfo.Controls.Add(this.lblPviDetails);
            this.grpPviInfo.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.grpPviInfo.ForeColor = System.Drawing.Color.Navy;
            this.grpPviInfo.Location = new System.Drawing.Point(12, 210);
            this.grpPviInfo.Name = "grpPviInfo";
            this.grpPviInfo.Size = new System.Drawing.Size(536, 70);
            this.grpPviInfo.TabIndex = 2;
            this.grpPviInfo.TabStop = false;
            this.grpPviInfo.Text = "3. Thông số đỉnh PVI đã chọn";
            // 
            // lblGrades
            // 
            this.lblGrades.AutoSize = true;
            this.lblGrades.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.lblGrades.ForeColor = System.Drawing.Color.DarkGreen;
            this.lblGrades.Location = new System.Drawing.Point(12, 44);
            this.lblGrades.Name = "lblGrades";
            this.lblGrades.Size = new System.Drawing.Size(306, 15);
            this.lblGrades.TabIndex = 1;
            this.lblGrades.Text = "Dốc i1: --- %  |  Dốc i2: --- %  |  Hiệu dốc Δi: --- % (---)";
            // 
            // lblPviDetails
            // 
            this.lblPviDetails.AutoSize = true;
            this.lblPviDetails.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.lblPviDetails.ForeColor = System.Drawing.Color.Black;
            this.lblPviDetails.Location = new System.Drawing.Point(12, 22);
            this.lblPviDetails.Name = "lblPviDetails";
            this.lblPviDetails.Size = new System.Drawing.Size(262, 15);
            this.lblPviDetails.TabIndex = 0;
            this.lblPviDetails.Text = "PVI #: ---  |  Lý trình: --- m  |  Cao độ: --- m";
            // 
            // grpSuggest
            // 
            this.grpSuggest.Controls.Add(this.lblWarningThreshold);
            this.grpSuggest.Controls.Add(this.lblSuggestLength);
            this.grpSuggest.Controls.Add(this.lblSuggestRadius);
            this.grpSuggest.Controls.Add(this.lblSuggestTitle);
            this.grpSuggest.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.grpSuggest.ForeColor = System.Drawing.Color.DarkOliveGreen;
            this.grpSuggest.Location = new System.Drawing.Point(12, 285);
            this.grpSuggest.Name = "grpSuggest";
            this.grpSuggest.Size = new System.Drawing.Size(536, 110);
            this.grpSuggest.TabIndex = 3;
            this.grpSuggest.TabStop = false;
            this.grpSuggest.Text = "4. Gợi ý thông số theo tiêu chuẩn thiết kế";
            // 
            // lblWarningThreshold
            // 
            this.lblWarningThreshold.AutoSize = true;
            this.lblWarningThreshold.Font = new System.Drawing.Font("Segoe UI", 8.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.lblWarningThreshold.ForeColor = System.Drawing.Color.Maroon;
            this.lblWarningThreshold.Location = new System.Drawing.Point(12, 86);
            this.lblWarningThreshold.Name = "lblWarningThreshold";
            this.lblWarningThreshold.Size = new System.Drawing.Size(374, 15);
            this.lblWarningThreshold.TabIndex = 3;
            this.lblWarningThreshold.Text = "[!] Hiệu dốc Δi nhỏ hơn ngưỡng cắm cong đứng tối thiểu (1.0%)";
            this.lblWarningThreshold.Visible = false;
            // 
            // lblSuggestLength
            // 
            this.lblSuggestLength.AutoSize = true;
            this.lblSuggestLength.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.lblSuggestLength.ForeColor = System.Drawing.Color.Black;
            this.lblSuggestLength.Location = new System.Drawing.Point(12, 65);
            this.lblSuggestLength.Name = "lblSuggestLength";
            this.lblSuggestLength.Size = new System.Drawing.Size(315, 15);
            this.lblSuggestLength.TabIndex = 2;
            this.lblSuggestLength.Text = "• Chiều dài L: Giới hạn ≥ --- m  |  Thông thường ≥ --- m";
            // 
            // lblSuggestRadius
            // 
            this.lblSuggestRadius.AutoSize = true;
            this.lblSuggestRadius.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.lblSuggestRadius.ForeColor = System.Drawing.Color.Black;
            this.lblSuggestRadius.Location = new System.Drawing.Point(12, 43);
            this.lblSuggestRadius.Name = "lblSuggestRadius";
            this.lblSuggestRadius.Size = new System.Drawing.Size(306, 15);
            this.lblSuggestRadius.TabIndex = 1;
            this.lblSuggestRadius.Text = "• Bán kính R: Giới hạn ≥ --- m  |  Thông thường ≥ --- m";
            // 
            // lblSuggestTitle
            // 
            this.lblSuggestTitle.AutoSize = true;
            this.lblSuggestTitle.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.lblSuggestTitle.ForeColor = System.Drawing.Color.DarkOliveGreen;
            this.lblSuggestTitle.Location = new System.Drawing.Point(12, 22);
            this.lblSuggestTitle.Name = "lblSuggestTitle";
            this.lblSuggestTitle.Size = new System.Drawing.Size(256, 15);
            this.lblSuggestTitle.TabIndex = 0;
            this.lblSuggestTitle.Text = "Yêu cầu cho đường cong Lồi (Vtk = 100 km/h):";
            // 
            // grpInput
            // 
            this.grpInput.Controls.Add(this.lblValidation);
            this.grpInput.Controls.Add(this.lblCalculatedVal);
            this.grpInput.Controls.Add(this.txtLength);
            this.grpInput.Controls.Add(this.txtRadius);
            this.grpInput.Controls.Add(this.radLength);
            this.grpInput.Controls.Add(this.radRadius);
            this.grpInput.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.grpInput.ForeColor = System.Drawing.Color.Navy;
            this.grpInput.Location = new System.Drawing.Point(12, 400);
            this.grpInput.Name = "grpInput";
            this.grpInput.Size = new System.Drawing.Size(536, 125);
            this.grpInput.TabIndex = 4;
            this.grpInput.TabStop = false;
            this.grpInput.Text = "5. Khai báo thông số & Đánh giá đạt tiêu chuẩn";
            // 
            // lblValidation
            // 
            this.lblValidation.AutoSize = true;
            this.lblValidation.Font = new System.Drawing.Font("Segoe UI", 8.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.lblValidation.ForeColor = System.Drawing.Color.DarkGreen;
            this.lblValidation.Location = new System.Drawing.Point(12, 98);
            this.lblValidation.Name = "lblValidation";
            this.lblValidation.Size = new System.Drawing.Size(126, 15);
            this.lblValidation.TabIndex = 5;
            this.lblValidation.Text = "[OK] Đạt chuẩn giới hạn";
            // 
            // lblCalculatedVal
            // 
            this.lblCalculatedVal.AutoSize = true;
            this.lblCalculatedVal.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Italic, System.Drawing.GraphicsUnit.Point);
            this.lblCalculatedVal.ForeColor = System.Drawing.Color.DarkSlateGray;
            this.lblCalculatedVal.Location = new System.Drawing.Point(280, 30);
            this.lblCalculatedVal.Name = "lblCalculatedVal";
            this.lblCalculatedVal.Size = new System.Drawing.Size(193, 15);
            this.lblCalculatedVal.TabIndex = 4;
            this.lblCalculatedVal.Text = "-> Tương ứng chiều dài L = --- m";
            // 
            // txtLength
            // 
            this.txtLength.Enabled = false;
            this.txtLength.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.txtLength.Location = new System.Drawing.Point(165, 61);
            this.txtLength.Name = "txtLength";
            this.txtLength.Size = new System.Drawing.Size(105, 25);
            this.txtLength.TabIndex = 3;
            // 
            // txtRadius
            // 
            this.txtRadius.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.txtRadius.Location = new System.Drawing.Point(165, 26);
            this.txtRadius.Name = "txtRadius";
            this.txtRadius.Size = new System.Drawing.Size(105, 25);
            this.txtRadius.TabIndex = 1;
            // 
            // radLength
            // 
            this.radLength.AutoSize = true;
            this.radLength.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.radLength.ForeColor = System.Drawing.Color.Black;
            this.radLength.Location = new System.Drawing.Point(12, 64);
            this.radLength.Name = "radLength";
            this.radLength.Size = new System.Drawing.Size(147, 19);
            this.radLength.TabIndex = 2;
            this.radLength.Text = "Theo Chiều dài L (m):";
            this.radLength.UseVisualStyleBackColor = true;
            // 
            // radRadius
            // 
            this.radRadius.AutoSize = true;
            this.radRadius.Checked = true;
            this.radRadius.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.radRadius.ForeColor = System.Drawing.Color.Black;
            this.radRadius.Location = new System.Drawing.Point(12, 29);
            this.radRadius.Name = "radRadius";
            this.radRadius.Size = new System.Drawing.Size(145, 19);
            this.radRadius.TabIndex = 0;
            this.radRadius.TabStop = true;
            this.radRadius.Text = "Theo Bán kính R (m):";
            this.radRadius.UseVisualStyleBackColor = true;
            // 
            // btnApply
            // 
            this.btnApply.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.btnApply.ForeColor = System.Drawing.Color.DarkGreen;
            this.btnApply.Location = new System.Drawing.Point(12, 535);
            this.btnApply.Name = "btnApply";
            this.btnApply.Size = new System.Drawing.Size(115, 35);
            this.btnApply.TabIndex = 5;
            this.btnApply.Text = "Áp dụng";
            this.btnApply.UseVisualStyleBackColor = true;
            // 
            // btnPickPviNext
            // 
            this.btnPickPviNext.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.btnPickPviNext.ForeColor = System.Drawing.Color.Navy;
            this.btnPickPviNext.Location = new System.Drawing.Point(133, 535);
            this.btnPickPviNext.Name = "btnPickPviNext";
            this.btnPickPviNext.Size = new System.Drawing.Size(130, 35);
            this.btnPickPviNext.TabIndex = 6;
            this.btnPickPviNext.Text = "Pick PVI tiếp";
            this.btnPickPviNext.UseVisualStyleBackColor = true;
            // 
            // btnApplyAndClose
            // 
            this.btnApplyAndClose.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.btnApplyAndClose.ForeColor = System.Drawing.Color.DarkBlue;
            this.btnApplyAndClose.Location = new System.Drawing.Point(269, 535);
            this.btnApplyAndClose.Name = "btnApplyAndClose";
            this.btnApplyAndClose.Size = new System.Drawing.Size(145, 35);
            this.btnApplyAndClose.TabIndex = 7;
            this.btnApplyAndClose.Text = "Áp dụng & Đóng";
            this.btnApplyAndClose.UseVisualStyleBackColor = true;
            // 
            // btnCancel
            // 
            this.btnCancel.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.btnCancel.Location = new System.Drawing.Point(420, 535);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(128, 35);
            this.btnCancel.TabIndex = 8;
            this.btnCancel.Text = "Đóng";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnApplyAll
            // 
            this.btnApplyAll.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.btnApplyAll.ForeColor = System.Drawing.Color.DarkRed;
            this.btnApplyAll.Location = new System.Drawing.Point(12, 576);
            this.btnApplyAll.Name = "btnApplyAll";
            this.btnApplyAll.Size = new System.Drawing.Size(536, 35);
            this.btnApplyAll.TabIndex = 9;
            this.btnApplyAll.Text = "Tự động áp cong đứng cho tất cả PVI";
            this.btnApplyAll.UseVisualStyleBackColor = true;
            // 
            // BoTriCongDungForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(560, 623);
            this.Controls.Add(this.btnApplyAll);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnApplyAndClose);
            this.Controls.Add(this.btnPickPviNext);
            this.Controls.Add(this.btnApply);
            this.Controls.Add(this.grpInput);
            this.Controls.Add(this.grpSuggest);
            this.Controls.Add(this.grpPviInfo);
            this.Controls.Add(this.grpObjects);
            this.Controls.Add(this.grpStandard);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "BoTriCongDungForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Bố Trí Đường Cong Đứng PVI (CTP_BoTri_CongDung)";
            this.grpStandard.ResumeLayout(false);
            this.grpStandard.PerformLayout();
            this.grpObjects.ResumeLayout(false);
            this.grpObjects.PerformLayout();
            this.grpPviInfo.ResumeLayout(false);
            this.grpPviInfo.PerformLayout();
            this.grpSuggest.ResumeLayout(false);
            this.grpSuggest.PerformLayout();
            this.grpInput.ResumeLayout(false);
            this.grpInput.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox grpStandard;
        private System.Windows.Forms.Label lblStandard;
        private System.Windows.Forms.ComboBox cmbStandard;
        private System.Windows.Forms.Label lblVtk;
        private System.Windows.Forms.ComboBox cmbVtk;
        private System.Windows.Forms.Label lblTerrain;
        private System.Windows.Forms.ComboBox cmbTerrain;
        private System.Windows.Forms.GroupBox grpObjects;
        private System.Windows.Forms.Label lblProfileView;
        private System.Windows.Forms.TextBox txtProfileViewName;
        private System.Windows.Forms.Button btnPickProfileView;
        private System.Windows.Forms.Label lblProfile;
        private System.Windows.Forms.ComboBox cmbProfile;
        private System.Windows.Forms.Button btnPickProfile;
        private System.Windows.Forms.Label lblPviSelect;
        private System.Windows.Forms.ComboBox cmbPvi;
        private System.Windows.Forms.Button btnPickPvi;
        private System.Windows.Forms.GroupBox grpPviInfo;
        private System.Windows.Forms.Label lblPviDetails;
        private System.Windows.Forms.Label lblGrades;
        private System.Windows.Forms.GroupBox grpSuggest;
        private System.Windows.Forms.Label lblSuggestTitle;
        private System.Windows.Forms.Label lblSuggestRadius;
        private System.Windows.Forms.Label lblSuggestLength;
        private System.Windows.Forms.Label lblWarningThreshold;
        private System.Windows.Forms.GroupBox grpInput;
        private System.Windows.Forms.RadioButton radRadius;
        private System.Windows.Forms.RadioButton radLength;
        private System.Windows.Forms.TextBox txtRadius;
        private System.Windows.Forms.TextBox txtLength;
        private System.Windows.Forms.Label lblCalculatedVal;
        private System.Windows.Forms.Label lblValidation;
        private System.Windows.Forms.Button btnApply;
        private System.Windows.Forms.Button btnPickPviNext;
        private System.Windows.Forms.Button btnApplyAndClose;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnApplyAll;
    }
}

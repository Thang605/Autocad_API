namespace MyFirstProject.Menu_form
{
    partial class KiemTraProfileForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.label3 = new System.Windows.Forms.Label();
            this.cbbStandard = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.cbbAlignments = new System.Windows.Forms.ComboBox();
            this.btnPickAlignment = new System.Windows.Forms.Button();
            this.labelProfile = new System.Windows.Forms.Label();
            this.cbbProfiles = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.cbbDesignSpeed = new System.Windows.Forms.ComboBox();
            this.labelTerrain = new System.Windows.Forms.Label();
            this.cbbTerrain = new System.Windows.Forms.ComboBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.chkMaxGrade = new System.Windows.Forms.CheckBox();
            this.chkMinGrade = new System.Windows.Forms.CheckBox();
            this.chkVerticalCurve = new System.Windows.Forms.CheckBox();
            this.chkGradeLength = new System.Windows.Forms.CheckBox();
            this.btnCheck = new System.Windows.Forms.Button();
            this.dgvResults = new System.Windows.Forms.DataGridView();
            this.colSTT = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colStation = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colElevation = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colItemName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colProposedValue = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colStandardRequirement = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colStatus = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colNote = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.chkShowPassed = new System.Windows.Forms.CheckBox();
            this.chkShowWarning = new System.Windows.Forms.CheckBox();
            this.chkShowFailed = new System.Windows.Forms.CheckBox();
            this.btnZoomPVI = new System.Windows.Forms.Button();
            this.btnDrawErrors = new System.Windows.Forms.Button();
            this.btnExportExcel = new System.Windows.Forms.Button();
            this.btnClose = new System.Windows.Forms.Button();
            this.groupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvResults)).BeginInit();
            this.SuspendLayout();
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(12, 15);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(62, 13);
            this.label3.TabIndex = 0;
            this.label3.Text = "Tiêu chuẩn:";
            // 
            // cbbStandard
            // 
            this.cbbStandard.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbbStandard.FormattingEnabled = true;
            this.cbbStandard.Location = new System.Drawing.Point(95, 12);
            this.cbbStandard.Name = "cbbStandard";
            this.cbbStandard.Size = new System.Drawing.Size(240, 21);
            this.cbbStandard.TabIndex = 1;
            this.cbbStandard.SelectedIndexChanged += new System.EventHandler(this.cbbStandard_SelectedIndexChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 45);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(59, 13);
            this.label1.TabIndex = 2;
            this.label1.Text = "Tim tuyến:";
            // 
            // cbbAlignments
            // 
            this.cbbAlignments.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbbAlignments.FormattingEnabled = true;
            this.cbbAlignments.Location = new System.Drawing.Point(95, 42);
            this.cbbAlignments.Name = "cbbAlignments";
            this.cbbAlignments.Size = new System.Drawing.Size(240, 21);
            this.cbbAlignments.TabIndex = 3;
            this.cbbAlignments.SelectedIndexChanged += new System.EventHandler(this.cbbAlignments_SelectedIndexChanged);
            // 
            // btnPickAlignment
            // 
            this.btnPickAlignment.Location = new System.Drawing.Point(341, 40);
            this.btnPickAlignment.Name = "btnPickAlignment";
            this.btnPickAlignment.Size = new System.Drawing.Size(35, 23);
            this.btnPickAlignment.TabIndex = 4;
            this.btnPickAlignment.Text = "...";
            this.btnPickAlignment.UseVisualStyleBackColor = true;
            this.btnPickAlignment.Click += new System.EventHandler(this.btnPickAlignment_Click);
            // 
            // labelProfile
            // 
            this.labelProfile.AutoSize = true;
            this.labelProfile.Location = new System.Drawing.Point(12, 75);
            this.labelProfile.Name = "labelProfile";
            this.labelProfile.Size = new System.Drawing.Size(77, 13);
            this.labelProfile.TabIndex = 5;
            this.labelProfile.Text = "Profile (Đường):";
            // 
            // cbbProfiles
            // 
            this.cbbProfiles.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbbProfiles.FormattingEnabled = true;
            this.cbbProfiles.Location = new System.Drawing.Point(95, 72);
            this.cbbProfiles.Name = "cbbProfiles";
            this.cbbProfiles.Size = new System.Drawing.Size(281, 21);
            this.cbbProfiles.TabIndex = 6;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(395, 15);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(91, 13);
            this.label2.TabIndex = 7;
            this.label2.Text = "Tốc độ TK (V_tk):";
            // 
            // cbbDesignSpeed
            // 
            this.cbbDesignSpeed.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbbDesignSpeed.FormattingEnabled = true;
            this.cbbDesignSpeed.Location = new System.Drawing.Point(492, 12);
            this.cbbDesignSpeed.Name = "cbbDesignSpeed";
            this.cbbDesignSpeed.Size = new System.Drawing.Size(120, 21);
            this.cbbDesignSpeed.TabIndex = 8;
            // 
            // labelTerrain
            // 
            this.labelTerrain.AutoSize = true;
            this.labelTerrain.Location = new System.Drawing.Point(395, 45);
            this.labelTerrain.Name = "labelTerrain";
            this.labelTerrain.Size = new System.Drawing.Size(68, 13);
            this.labelTerrain.TabIndex = 9;
            this.labelTerrain.Text = "Loại địa hình:";
            // 
            // cbbTerrain
            // 
            this.cbbTerrain.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbbTerrain.FormattingEnabled = true;
            this.cbbTerrain.Items.AddRange(new object[] {
            "Đồng bằng",
            "Đồi",
            "Núi"});
            this.cbbTerrain.Location = new System.Drawing.Point(492, 42);
            this.cbbTerrain.Name = "cbbTerrain";
            this.cbbTerrain.Size = new System.Drawing.Size(120, 21);
            this.cbbTerrain.TabIndex = 10;
            // 
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox1.Controls.Add(this.chkMaxGrade);
            this.groupBox1.Controls.Add(this.chkMinGrade);
            this.groupBox1.Controls.Add(this.chkVerticalCurve);
            this.groupBox1.Controls.Add(this.chkGradeLength);
            this.groupBox1.Location = new System.Drawing.Point(12, 105);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(795, 65);
            this.groupBox1.TabIndex = 11;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Nội dung kiểm tra:";
            // 
            // chkMaxGrade
            // 
            this.chkMaxGrade.AutoSize = true;
            this.chkMaxGrade.Checked = true;
            this.chkMaxGrade.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkMaxGrade.Location = new System.Drawing.Point(15, 22);
            this.chkMaxGrade.Name = "chkMaxGrade";
            this.chkMaxGrade.Size = new System.Drawing.Size(130, 17);
            this.chkMaxGrade.TabIndex = 0;
            this.chkMaxGrade.Text = "Kiểm tra dốc tối đa (i_max)";
            this.chkMaxGrade.UseVisualStyleBackColor = true;
            // 
            // chkMinGrade
            // 
            this.chkMinGrade.AutoSize = true;
            this.chkMinGrade.Checked = true;
            this.chkMinGrade.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkMinGrade.Location = new System.Drawing.Point(160, 22);
            this.chkMinGrade.Name = "chkMinGrade";
            this.chkMinGrade.Size = new System.Drawing.Size(135, 17);
            this.chkMinGrade.TabIndex = 1;
            this.chkMinGrade.Text = "Kiểm tra dốc tối thiểu (i_min)";
            this.chkMinGrade.UseVisualStyleBackColor = true;
            // 
            // chkVerticalCurve
            // 
            this.chkVerticalCurve.AutoSize = true;
            this.chkVerticalCurve.Checked = true;
            this.chkVerticalCurve.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkVerticalCurve.Location = new System.Drawing.Point(310, 22);
            this.chkVerticalCurve.Name = "chkVerticalCurve";
            this.chkVerticalCurve.Size = new System.Drawing.Size(161, 17);
            this.chkVerticalCurve.TabIndex = 2;
            this.chkVerticalCurve.Text = "Đường cong đứng (Lồi / Lõm)";
            this.chkVerticalCurve.UseVisualStyleBackColor = true;
            // 
            // chkGradeLength
            // 
            this.chkGradeLength.AutoSize = true;
            this.chkGradeLength.Checked = true;
            this.chkGradeLength.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkGradeLength.Location = new System.Drawing.Point(15, 43);
            this.chkGradeLength.Name = "chkGradeLength";
            this.chkGradeLength.Size = new System.Drawing.Size(175, 17);
            this.chkGradeLength.TabIndex = 3;
            this.chkGradeLength.Text = "Chiều dài đoạn dốc tối thiểu (L_doc)";
            this.chkGradeLength.UseVisualStyleBackColor = true;
            // 
            // btnCheck
            // 
            this.btnCheck.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCheck.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnCheck.Location = new System.Drawing.Point(820, 12);
            this.btnCheck.Name = "btnCheck";
            this.btnCheck.Size = new System.Drawing.Size(148, 158);
            this.btnCheck.TabIndex = 12;
            this.btnCheck.Text = "KIỂM TRA PROFILE";
            this.btnCheck.UseVisualStyleBackColor = true;
            this.btnCheck.Click += new System.EventHandler(this.btnCheck_Click);
            // 
            // dgvResults
            // 
            this.dgvResults.AllowUserToAddRows = false;
            this.dgvResults.AllowUserToDeleteRows = false;
            this.dgvResults.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dgvResults.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvResults.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvResults.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colSTT,
            this.colStation,
            this.colElevation,
            this.colItemName,
            this.colProposedValue,
            this.colStandardRequirement,
            this.colStatus,
            this.colNote});
            this.dgvResults.Location = new System.Drawing.Point(12, 180);
            this.dgvResults.MultiSelect = false;
            this.dgvResults.Name = "dgvResults";
            this.dgvResults.ReadOnly = true;
            this.dgvResults.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvResults.Size = new System.Drawing.Size(956, 385);
            this.dgvResults.TabIndex = 13;
            this.dgvResults.SelectionChanged += new System.EventHandler(this.dgvResults_SelectionChanged);
            // 
            // colSTT
            // 
            this.colSTT.FillWeight = 35F;
            this.colSTT.HeaderText = "STT";
            this.colSTT.Name = "colSTT";
            this.colSTT.ReadOnly = true;
            // 
            // colStation
            // 
            this.colStation.FillWeight = 60F;
            this.colStation.HeaderText = "Lý trình (m)";
            this.colStation.Name = "colStation";
            this.colStation.ReadOnly = true;
            // 
            // colElevation
            // 
            this.colElevation.FillWeight = 60F;
            this.colElevation.HeaderText = "Cao độ (m)";
            this.colElevation.Name = "colElevation";
            this.colElevation.ReadOnly = true;
            // 
            // colItemName
            // 
            this.colItemName.FillWeight = 110F;
            this.colItemName.HeaderText = "Hạng mục kiểm tra";
            this.colItemName.Name = "colItemName";
            this.colItemName.ReadOnly = true;
            // 
            // colProposedValue
            // 
            this.colProposedValue.FillWeight = 110F;
            this.colProposedValue.HeaderText = "Giá trị thiết kế";
            this.colProposedValue.Name = "colProposedValue";
            this.colProposedValue.ReadOnly = true;
            // 
            // colStandardRequirement
            // 
            this.colStandardRequirement.FillWeight = 130F;
            this.colStandardRequirement.HeaderText = "Yêu cầu tiêu chuẩn";
            this.colStandardRequirement.Name = "colStandardRequirement";
            this.colStandardRequirement.ReadOnly = true;
            // 
            // colStatus
            // 
            this.colStatus.FillWeight = 65F;
            this.colStatus.HeaderText = "Trạng thái";
            this.colStatus.Name = "colStatus";
            this.colStatus.ReadOnly = true;
            // 
            // colNote
            // 
            this.colNote.FillWeight = 180F;
            this.colNote.HeaderText = "Ghi chú chi tiết";
            this.colNote.Name = "colNote";
            this.colNote.ReadOnly = true;
            // 
            // chkShowPassed
            // 
            this.chkShowPassed.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.chkShowPassed.AutoSize = true;
            this.chkShowPassed.Checked = true;
            this.chkShowPassed.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkShowPassed.Location = new System.Drawing.Point(15, 583);
            this.chkShowPassed.Name = "chkShowPassed";
            this.chkShowPassed.Size = new System.Drawing.Size(95, 17);
            this.chkShowPassed.TabIndex = 14;
            this.chkShowPassed.Text = "Hiển thị ĐẠT";
            this.chkShowPassed.UseVisualStyleBackColor = true;
            this.chkShowPassed.CheckedChanged += new System.EventHandler(this.Filter_CheckedChanged);
            // 
            // chkShowWarning
            // 
            this.chkShowWarning.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.chkShowWarning.AutoSize = true;
            this.chkShowWarning.Checked = true;
            this.chkShowWarning.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkShowWarning.Location = new System.Drawing.Point(120, 583);
            this.chkShowWarning.Name = "chkShowWarning";
            this.chkShowWarning.Size = new System.Drawing.Size(126, 17);
            this.chkShowWarning.TabIndex = 15;
            this.chkShowWarning.Text = "Hiển thị CẢNH BÁO";
            this.chkShowWarning.UseVisualStyleBackColor = true;
            this.chkShowWarning.CheckedChanged += new System.EventHandler(this.Filter_CheckedChanged);
            // 
            // chkShowFailed
            // 
            this.chkShowFailed.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.chkShowFailed.AutoSize = true;
            this.chkShowFailed.Checked = true;
            this.chkShowFailed.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkShowFailed.Location = new System.Drawing.Point(260, 583);
            this.chkShowFailed.Name = "chkShowFailed";
            this.chkShowFailed.Size = new System.Drawing.Size(117, 17);
            this.chkShowFailed.TabIndex = 16;
            this.chkShowFailed.Text = "Hiển thị VI PHẠM";
            this.chkShowFailed.UseVisualStyleBackColor = true;
            this.chkShowFailed.CheckedChanged += new System.EventHandler(this.Filter_CheckedChanged);
            // 
            // btnDrawErrors
            // 
            this.btnDrawErrors.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnDrawErrors.Location = new System.Drawing.Point(525, 578);
            this.btnDrawErrors.Name = "btnDrawErrors";
            this.btnDrawErrors.Size = new System.Drawing.Size(118, 28);
            this.btnDrawErrors.TabIndex = 20;
            this.btnDrawErrors.Text = "Đánh dấu vi phạm";
            this.btnDrawErrors.UseVisualStyleBackColor = true;
            this.btnDrawErrors.Click += new System.EventHandler(this.btnDrawErrors_Click);
            // 
            // btnZoomPVI
            // 
            this.btnZoomPVI.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnZoomPVI.Location = new System.Drawing.Point(650, 578);
            this.btnZoomPVI.Name = "btnZoomPVI";
            this.btnZoomPVI.Size = new System.Drawing.Size(100, 28);
            this.btnZoomPVI.TabIndex = 17;
            this.btnZoomPVI.Text = "Zoom vị trí";
            this.btnZoomPVI.UseVisualStyleBackColor = true;
            this.btnZoomPVI.Click += new System.EventHandler(this.btnZoomPVI_Click);
            // 
            // btnExportExcel
            // 
            this.btnExportExcel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnExportExcel.Location = new System.Drawing.Point(758, 578);
            this.btnExportExcel.Name = "btnExportExcel";
            this.btnExportExcel.Size = new System.Drawing.Size(100, 28);
            this.btnExportExcel.TabIndex = 18;
            this.btnExportExcel.Text = "Xuất Excel";
            this.btnExportExcel.UseVisualStyleBackColor = true;
            this.btnExportExcel.Click += new System.EventHandler(this.btnExportExcel_Click);
            // 
            // btnClose
            // 
            this.btnClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClose.Location = new System.Drawing.Point(866, 578);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(102, 28);
            this.btnClose.TabIndex = 19;
            this.btnClose.Text = "Đóng";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            // 
            // KiemTraProfileForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(980, 618);
            this.MinimumSize = new System.Drawing.Size(850, 550);
            this.Controls.Add(this.btnDrawErrors);
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.btnExportExcel);
            this.Controls.Add(this.btnZoomPVI);
            this.Controls.Add(this.chkShowFailed);
            this.Controls.Add(this.chkShowWarning);
            this.Controls.Add(this.chkShowPassed);
            this.Controls.Add(this.dgvResults);
            this.Controls.Add(this.btnCheck);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.cbbTerrain);
            this.Controls.Add(this.labelTerrain);
            this.Controls.Add(this.cbbDesignSpeed);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.cbbProfiles);
            this.Controls.Add(this.labelProfile);
            this.Controls.Add(this.btnPickAlignment);
            this.Controls.Add(this.cbbAlignments);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.cbbStandard);
            this.Controls.Add(this.label3);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = true;
            this.Name = "KiemTraProfileForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Kiểm Tra Profile Theo Tiêu Chuẩn Thiết Kế Đường";
            this.AcceptButton = this.btnCheck;
            this.CancelButton = this.btnClose;
            this.Load += new System.EventHandler(this.KiemTraProfileForm_Load);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvResults)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ComboBox cbbStandard;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox cbbAlignments;
        private System.Windows.Forms.Button btnPickAlignment;
        private System.Windows.Forms.Label labelProfile;
        private System.Windows.Forms.ComboBox cbbProfiles;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox cbbDesignSpeed;
        private System.Windows.Forms.Label labelTerrain;
        private System.Windows.Forms.ComboBox cbbTerrain;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.CheckBox chkMaxGrade;
        private System.Windows.Forms.CheckBox chkMinGrade;
        private System.Windows.Forms.CheckBox chkVerticalCurve;
        private System.Windows.Forms.CheckBox chkGradeLength;
        private System.Windows.Forms.Button btnCheck;
        private System.Windows.Forms.DataGridView dgvResults;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSTT;
        private System.Windows.Forms.DataGridViewTextBoxColumn colStation;
        private System.Windows.Forms.DataGridViewTextBoxColumn colElevation;
        private System.Windows.Forms.DataGridViewTextBoxColumn colItemName;
        private System.Windows.Forms.DataGridViewTextBoxColumn colProposedValue;
        private System.Windows.Forms.DataGridViewTextBoxColumn colStandardRequirement;
        private System.Windows.Forms.DataGridViewTextBoxColumn colStatus;
        private System.Windows.Forms.DataGridViewTextBoxColumn colNote;
        private System.Windows.Forms.CheckBox chkShowPassed;
        private System.Windows.Forms.CheckBox chkShowWarning;
        private System.Windows.Forms.CheckBox chkShowFailed;
        private System.Windows.Forms.Button btnDrawErrors;
        private System.Windows.Forms.Button btnZoomPVI;
        private System.Windows.Forms.Button btnExportExcel;
        private System.Windows.Forms.Button btnClose;
    }
}

namespace MyFirstProject.Menu_form
{
    partial class KiemTraTimTuyenForm
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
            this.label2 = new System.Windows.Forms.Label();
            this.cbbDesignSpeed = new System.Windows.Forms.ComboBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.chkTransitionLength = new System.Windows.Forms.CheckBox();
            this.chkStraightBetweenCurves = new System.Windows.Forms.CheckBox();
            this.chkMaxStraightLength = new System.Windows.Forms.CheckBox();
            this.chkMinCurveLength = new System.Windows.Forms.CheckBox();
            this.chkMinRadius = new System.Windows.Forms.CheckBox();
            this.chkCheckSuperelevation = new System.Windows.Forms.CheckBox();
            this.btnCheck = new System.Windows.Forms.Button();
            this.dgvResults = new System.Windows.Forms.DataGridView();
            this.colStatus = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colType = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colStation = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colDetail = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.btnDrawErrors = new System.Windows.Forms.Button();
            this.btnClose = new System.Windows.Forms.Button();
            this.chkShowPassed = new System.Windows.Forms.CheckBox();
            this.chkShowFailed = new System.Windows.Forms.CheckBox();
            this.btnViewTable = new System.Windows.Forms.Button();
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
            this.label3.TabIndex = 13;
            this.label3.Text = "Tiêu chuẩn:";
            // 
            // cbbStandard
            // 
            this.cbbStandard.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.cbbStandard.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbbStandard.FormattingEnabled = true;
            this.cbbStandard.Location = new System.Drawing.Point(80, 12);
            this.cbbStandard.Name = "cbbStandard";
            this.cbbStandard.Size = new System.Drawing.Size(425, 21);
            this.cbbStandard.TabIndex = 14;
            this.cbbStandard.SelectedIndexChanged += new System.EventHandler(this.cbbStandard_SelectedIndexChanged);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 45);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(59, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Tim tuyến:";
            // 
            // cbbAlignments
            // 
            this.cbbAlignments.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.cbbAlignments.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbbAlignments.FormattingEnabled = true;
            this.cbbAlignments.Location = new System.Drawing.Point(77, 42);
            this.cbbAlignments.Name = "cbbAlignments";
            this.cbbAlignments.Size = new System.Drawing.Size(387, 21);
            this.cbbAlignments.TabIndex = 1;
            // 
            // btnPickAlignment
            // 
            this.btnPickAlignment.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnPickAlignment.Location = new System.Drawing.Point(470, 40);
            this.btnPickAlignment.Name = "btnPickAlignment";
            this.btnPickAlignment.Size = new System.Drawing.Size(35, 23);
            this.btnPickAlignment.TabIndex = 2;
            this.btnPickAlignment.Text = "...";
            this.btnPickAlignment.UseVisualStyleBackColor = true;
            this.btnPickAlignment.Click += new System.EventHandler(this.btnPickAlignment_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(12, 75);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(91, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "Tốc độ TK (V_tk):";
            // 
            // cbbDesignSpeed
            // 
            this.cbbDesignSpeed.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbbDesignSpeed.FormattingEnabled = true;
            this.cbbDesignSpeed.Items.AddRange(new object[] {
            "20",
            "30",
            "40",
            "60",
            "80",
            "100",
            "120"});
            this.cbbDesignSpeed.Location = new System.Drawing.Point(109, 72);
            this.cbbDesignSpeed.Name = "cbbDesignSpeed";
            this.cbbDesignSpeed.Size = new System.Drawing.Size(121, 21);
            this.cbbDesignSpeed.TabIndex = 4;
            this.cbbDesignSpeed.SelectedIndexChanged += new System.EventHandler(this.cbbDesignSpeed_SelectedIndexChanged);
            // 
            // btnViewTable
            // 
            this.btnViewTable.Location = new System.Drawing.Point(236, 70);
            this.btnViewTable.Name = "btnViewTable";
            this.btnViewTable.Size = new System.Drawing.Size(103, 23);
            this.btnViewTable.TabIndex = 12;
            this.btnViewTable.Text = "Xem bảng tra";
            this.btnViewTable.UseVisualStyleBackColor = true;
            this.btnViewTable.Click += new System.EventHandler(this.btnViewTable_Click);
            // 
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox1.Controls.Add(this.chkTransitionLength);
            this.groupBox1.Controls.Add(this.chkStraightBetweenCurves);
            this.groupBox1.Controls.Add(this.chkMaxStraightLength);
            this.groupBox1.Controls.Add(this.chkMinCurveLength);
            this.groupBox1.Controls.Add(this.chkMinRadius);
            this.groupBox1.Controls.Add(this.chkCheckSuperelevation);
            this.groupBox1.Location = new System.Drawing.Point(15, 108);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(490, 155);
            this.groupBox1.TabIndex = 5;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Tuỳ chọn kiểm tra";
            // 
            // chkTransitionLength
            // 
            this.chkTransitionLength.AutoSize = true;
            this.chkTransitionLength.Checked = true;
            this.chkTransitionLength.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkTransitionLength.Location = new System.Drawing.Point(17, 111);
            this.chkTransitionLength.Name = "chkTransitionLength";
            this.chkTransitionLength.Size = new System.Drawing.Size(193, 17);
            this.chkTransitionLength.TabIndex = 4;
            this.chkTransitionLength.Text = "Chiều dài cong chuyển tiếp tối thiểu";
            this.chkTransitionLength.UseVisualStyleBackColor = true;
            // 
            // chkCheckSuperelevation
            // 
            this.chkCheckSuperelevation.AutoSize = true;
            this.chkCheckSuperelevation.Checked = true;
            this.chkCheckSuperelevation.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkCheckSuperelevation.Location = new System.Drawing.Point(17, 134);
            this.chkCheckSuperelevation.Name = "chkCheckSuperelevation";
            this.chkCheckSuperelevation.Size = new System.Drawing.Size(260, 17);
            this.chkCheckSuperelevation.TabIndex = 5;
            this.chkCheckSuperelevation.Text = "Kiểm tra Siêu cao (Isc_max <= 8% và L_vuốt)";
            this.chkCheckSuperelevation.UseVisualStyleBackColor = true;
            // 
            // chkStraightBetweenCurves
            // 
            this.chkStraightBetweenCurves.AutoSize = true;
            this.chkStraightBetweenCurves.Checked = true;
            this.chkStraightBetweenCurves.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkStraightBetweenCurves.Location = new System.Drawing.Point(17, 88);
            this.chkStraightBetweenCurves.Name = "chkStraightBetweenCurves";
            this.chkStraightBetweenCurves.Size = new System.Drawing.Size(201, 17);
            this.chkStraightBetweenCurves.TabIndex = 3;
            this.chkStraightBetweenCurves.Text = "Chiều dài đoạn thẳng giữa 2 đường cong";
            this.chkStraightBetweenCurves.UseVisualStyleBackColor = true;
            // 
            // chkMaxStraightLength
            // 
            this.chkMaxStraightLength.AutoSize = true;
            this.chkMaxStraightLength.Checked = true;
            this.chkMaxStraightLength.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkMaxStraightLength.Location = new System.Drawing.Point(17, 65);
            this.chkMaxStraightLength.Name = "chkMaxStraightLength";
            this.chkMaxStraightLength.Size = new System.Drawing.Size(161, 17);
            this.chkMaxStraightLength.TabIndex = 2;
            this.chkMaxStraightLength.Text = "Chiều dài đường thẳng tối đa";
            this.chkMaxStraightLength.UseVisualStyleBackColor = true;
            // 
            // chkMinCurveLength
            // 
            this.chkMinCurveLength.AutoSize = true;
            this.chkMinCurveLength.Checked = true;
            this.chkMinCurveLength.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkMinCurveLength.Location = new System.Drawing.Point(17, 42);
            this.chkMinCurveLength.Name = "chkMinCurveLength";
            this.chkMinCurveLength.Size = new System.Drawing.Size(164, 17);
            this.chkMinCurveLength.TabIndex = 1;
            this.chkMinCurveLength.Text = "Chiều dài đường cong tối thiểu";
            this.chkMinCurveLength.UseVisualStyleBackColor = true;
            // 
            // chkMinRadius
            // 
            this.chkMinRadius.AutoSize = true;
            this.chkMinRadius.Checked = true;
            this.chkMinRadius.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkMinRadius.Location = new System.Drawing.Point(17, 19);
            this.chkMinRadius.Name = "chkMinRadius";
            this.chkMinRadius.Size = new System.Drawing.Size(155, 17);
            this.chkMinRadius.TabIndex = 0;
            this.chkMinRadius.Text = "Bán kính đường cong tối thiểu";
            this.chkMinRadius.UseVisualStyleBackColor = true;
            // 
            // btnCheck
            // 
            this.btnCheck.Location = new System.Drawing.Point(15, 269);
            this.btnCheck.Name = "btnCheck";
            this.btnCheck.Size = new System.Drawing.Size(75, 23);
            this.btnCheck.TabIndex = 6;
            this.btnCheck.Text = "Kiểm tra";
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
            this.colStatus,
            this.colType,
            this.colStation,
            this.colDetail});
            this.dgvResults.Location = new System.Drawing.Point(15, 298);
            this.dgvResults.Name = "dgvResults";
            this.dgvResults.ReadOnly = true;
            this.dgvResults.Size = new System.Drawing.Size(490, 163);
            this.dgvResults.TabIndex = 7;
            // 
            // colStatus
            // 
            this.colStatus.HeaderText = "Trạng thái";
            this.colStatus.Name = "colStatus";
            this.colStatus.ReadOnly = true;
            this.colStatus.FillWeight = 25F;
            // 
            // colType
            // 
            this.colType.HeaderText = "Loại lỗi";
            this.colType.Name = "colType";
            this.colType.ReadOnly = true;
            this.colType.FillWeight = 30F;
            // 
            // colStation
            // 
            this.colStation.HeaderText = "Lý trình";
            this.colStation.Name = "colStation";
            this.colStation.ReadOnly = true;
            this.colStation.FillWeight = 20F;
            // 
            // colDetail
            // 
            this.colDetail.HeaderText = "Chi tiết";
            this.colDetail.Name = "colDetail";
            this.colDetail.ReadOnly = true;
            this.colDetail.FillWeight = 50F;
            // 
            // btnDrawErrors
            // 
            this.btnDrawErrors.Location = new System.Drawing.Point(96, 269);
            this.btnDrawErrors.Name = "btnDrawErrors";
            this.btnDrawErrors.Size = new System.Drawing.Size(126, 23);
            this.btnDrawErrors.TabIndex = 8;
            this.btnDrawErrors.Text = "Đánh dấu bản vẽ";
            this.btnDrawErrors.UseVisualStyleBackColor = true;
            this.btnDrawErrors.Click += new System.EventHandler(this.btnDrawErrors_Click);
            // 
            // btnClose
            // 
            this.btnClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnClose.Location = new System.Drawing.Point(430, 476);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(75, 23);
            this.btnClose.TabIndex = 9;
            this.btnClose.Text = "Đóng";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            // 
            // chkShowPassed
            // 
            this.chkShowPassed.AutoSize = true;
            this.chkShowPassed.Location = new System.Drawing.Point(240, 273);
            this.chkShowPassed.Name = "chkShowPassed";
            this.chkShowPassed.Size = new System.Drawing.Size(89, 17);
            this.chkShowPassed.TabIndex = 10;
            this.chkShowPassed.Text = "Hiển thị Đạt";
            this.chkShowPassed.UseVisualStyleBackColor = true;
            this.chkShowPassed.CheckedChanged += new System.EventHandler(this.chkFilter_CheckedChanged);
            // 
            // chkShowFailed
            // 
            this.chkShowFailed.AutoSize = true;
            this.chkShowFailed.Checked = true;
            this.chkShowFailed.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkShowFailed.Location = new System.Drawing.Point(340, 273);
            this.chkShowFailed.Name = "chkShowFailed";
            this.chkShowFailed.Size = new System.Drawing.Size(117, 17);
            this.chkShowFailed.TabIndex = 11;
            this.chkShowFailed.Text = "Hiển thị Không đạt";
            this.chkShowFailed.UseVisualStyleBackColor = true;
            this.chkShowFailed.CheckedChanged += new System.EventHandler(this.chkFilter_CheckedChanged);
            // 
            // KiemTraTimTuyenForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(517, 511);
            this.MinimumSize = new System.Drawing.Size(533, 450);
            this.Controls.Add(this.cbbStandard);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.btnViewTable);
            this.Controls.Add(this.chkShowFailed);
            this.Controls.Add(this.chkShowPassed);
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.btnDrawErrors);
            this.Controls.Add(this.dgvResults);
            this.Controls.Add(this.btnCheck);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.cbbDesignSpeed);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.btnPickAlignment);
            this.Controls.Add(this.cbbAlignments);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = true;
            this.Name = "KiemTraTimTuyenForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Kiểm Tra Tim Tuyến";
            this.AcceptButton = this.btnCheck;
            this.CancelButton = this.btnClose;
            this.Load += new System.EventHandler(this.KiemTraTimTuyenForm_Load);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvResults)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ComboBox cbbStandard;
        private System.Windows.Forms.ComboBox cbbAlignments;
        private System.Windows.Forms.Button btnPickAlignment;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox cbbDesignSpeed;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.CheckBox chkTransitionLength;
        private System.Windows.Forms.CheckBox chkStraightBetweenCurves;
        private System.Windows.Forms.CheckBox chkMaxStraightLength;
        private System.Windows.Forms.CheckBox chkMinCurveLength;
        private System.Windows.Forms.CheckBox chkMinRadius;
        private System.Windows.Forms.CheckBox chkCheckSuperelevation;
        private System.Windows.Forms.Button btnCheck;
        private System.Windows.Forms.DataGridView dgvResults;
        private System.Windows.Forms.DataGridViewTextBoxColumn colType;
        private System.Windows.Forms.DataGridViewTextBoxColumn colStation;
        private System.Windows.Forms.DataGridViewTextBoxColumn colDetail;
        private System.Windows.Forms.Button btnDrawErrors;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.CheckBox chkShowPassed;
        private System.Windows.Forms.CheckBox chkShowFailed;
        private System.Windows.Forms.DataGridViewTextBoxColumn colStatus;
        private System.Windows.Forms.Button btnViewTable;
    }
}

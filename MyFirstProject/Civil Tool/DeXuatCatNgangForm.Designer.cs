namespace Civil3DCsharp
{
    partial class DeXuatCatNgangForm
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
            this.cboDesignSpeed = new System.Windows.Forms.ComboBox();
            this.lblSpeed = new System.Windows.Forms.Label();
            this.cboRoadType = new System.Windows.Forms.ComboBox();
            this.lblRoadType = new System.Windows.Forms.Label();
            this.cboStandard = new System.Windows.Forms.ComboBox();
            this.lblStandard = new System.Windows.Forms.Label();
            this.grpInputs = new System.Windows.Forms.GroupBox();
            this.numTargetROW = new System.Windows.Forms.NumericUpDown();
            this.lblTargetROW = new System.Windows.Forms.Label();
            this.numShoulderSlope = new System.Windows.Forms.NumericUpDown();
            this.lblShoulderSlope = new System.Windows.Forms.Label();
            this.numRoadSlope = new System.Windows.Forms.NumericUpDown();
            this.lblRoadSlope = new System.Windows.Forms.Label();
            this.numBikeLane = new System.Windows.Forms.NumericUpDown();
            this.lblBikeLane = new System.Windows.Forms.Label();
            this.numGreenery = new System.Windows.Forms.NumericUpDown();
            this.lblGreenery = new System.Windows.Forms.Label();
            this.numSidewalk = new System.Windows.Forms.NumericUpDown();
            this.lblSidewalk = new System.Windows.Forms.Label();
            this.numSoftShoulder = new System.Windows.Forms.NumericUpDown();
            this.lblSoftShoulder = new System.Windows.Forms.Label();
            this.numHardShoulder = new System.Windows.Forms.NumericUpDown();
            this.lblHardShoulder = new System.Windows.Forms.Label();
            this.numSafetyStrip = new System.Windows.Forms.NumericUpDown();
            this.lblSafetyStrip = new System.Windows.Forms.Label();
            this.numMedianWidth = new System.Windows.Forms.NumericUpDown();
            this.lblMedianWidth = new System.Windows.Forms.Label();
            this.numLaneWidth = new System.Windows.Forms.NumericUpDown();
            this.lblLaneWidth = new System.Windows.Forms.Label();
            this.numLanesCount = new System.Windows.Forms.NumericUpDown();
            this.lblLanesCount = new System.Windows.Forms.Label();
            this.grpPreview = new System.Windows.Forms.GroupBox();
            this.picPreview = new System.Windows.Forms.PictureBox();
            this.grpResults = new System.Windows.Forms.GroupBox();
            this.lblStatusSummary = new System.Windows.Forms.Label();
            this.lblTotalProposedWidth = new System.Windows.Forms.Label();
            this.lblCarriagewayWidth = new System.Windows.Forms.Label();
            this.dgvEvaluationResults = new System.Windows.Forms.DataGridView();
            this.colSTT = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colElement = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colProposed = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colStandard = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colStatus = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colNote = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.btnEvaluate = new System.Windows.Forms.Button();
            this.btnDrawCadTable = new System.Windows.Forms.Button();
            this.btnExportExcel = new System.Windows.Forms.Button();
            this.btnClose = new System.Windows.Forms.Button();
            this.grpStandard.SuspendLayout();
            this.grpInputs.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numTargetROW)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numShoulderSlope)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numRoadSlope)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numBikeLane)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numGreenery)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numSidewalk)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numSoftShoulder)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numHardShoulder)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numSafetyStrip)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numMedianWidth)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numLaneWidth)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.numLanesCount)).BeginInit();
            this.grpPreview.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picPreview)).BeginInit();
            this.grpResults.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvEvaluationResults)).BeginInit();
            this.SuspendLayout();
            // 
            // grpStandard
            // 
            this.grpStandard.Controls.Add(this.cboDesignSpeed);
            this.grpStandard.Controls.Add(this.lblSpeed);
            this.grpStandard.Controls.Add(this.cboRoadType);
            this.grpStandard.Controls.Add(this.lblRoadType);
            this.grpStandard.Controls.Add(this.cboStandard);
            this.grpStandard.Controls.Add(this.lblStandard);
            this.grpStandard.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.grpStandard.ForeColor = System.Drawing.Color.Navy;
            this.grpStandard.Location = new System.Drawing.Point(12, 10);
            this.grpStandard.Name = "grpStandard";
            this.grpStandard.Size = new System.Drawing.Size(996, 60);
            this.grpStandard.TabIndex = 0;
            this.grpStandard.TabStop = false;
            this.grpStandard.Text = "1. LỰA CHỌN TIÊU CHUẨN & CẤP ĐƯỜNG THIẾT KẾ";
            // 
            // cboDesignSpeed
            // 
            this.cboDesignSpeed.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboDesignSpeed.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cboDesignSpeed.FormattingEnabled = true;
            this.cboDesignSpeed.Location = new System.Drawing.Point(855, 24);
            this.cboDesignSpeed.Name = "cboDesignSpeed";
            this.cboDesignSpeed.Size = new System.Drawing.Size(125, 23);
            this.cboDesignSpeed.TabIndex = 5;
            this.cboDesignSpeed.SelectedIndexChanged += new System.EventHandler(this.OnStandardOrTypeChanged);
            // 
            // lblSpeed
            // 
            this.lblSpeed.AutoSize = true;
            this.lblSpeed.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblSpeed.ForeColor = System.Drawing.Color.Black;
            this.lblSpeed.Location = new System.Drawing.Point(770, 27);
            this.lblSpeed.Name = "lblSpeed";
            this.lblSpeed.Size = new System.Drawing.Size(79, 15);
            this.lblSpeed.TabIndex = 4;
            this.lblSpeed.Text = "Vận tốc Vtk:";
            // 
            // cboRoadType
            // 
            this.cboRoadType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboRoadType.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cboRoadType.FormattingEnabled = true;
            this.cboRoadType.Location = new System.Drawing.Point(465, 24);
            this.cboRoadType.Name = "cboRoadType";
            this.cboRoadType.Size = new System.Drawing.Size(285, 23);
            this.cboRoadType.TabIndex = 3;
            this.cboRoadType.SelectedIndexChanged += new System.EventHandler(this.OnRoadTypeChanged);
            // 
            // lblRoadType
            // 
            this.lblRoadType.AutoSize = true;
            this.lblRoadType.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblRoadType.ForeColor = System.Drawing.Color.Black;
            this.lblRoadType.Location = new System.Drawing.Point(390, 27);
            this.lblRoadType.Name = "lblRoadType";
            this.lblRoadType.Size = new System.Drawing.Size(67, 15);
            this.lblRoadType.TabIndex = 2;
            this.lblRoadType.Text = "Cấp đường:";
            // 
            // cboStandard
            // 
            this.cboStandard.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboStandard.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cboStandard.FormattingEnabled = true;
            this.cboStandard.Location = new System.Drawing.Point(105, 24);
            this.cboStandard.Name = "cboStandard";
            this.cboStandard.Size = new System.Drawing.Size(265, 23);
            this.cboStandard.TabIndex = 1;
            this.cboStandard.SelectedIndexChanged += new System.EventHandler(this.OnStandardChanged);
            // 
            // lblStandard
            // 
            this.lblStandard.AutoSize = true;
            this.lblStandard.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblStandard.ForeColor = System.Drawing.Color.Black;
            this.lblStandard.Location = new System.Drawing.Point(15, 27);
            this.lblStandard.Name = "lblStandard";
            this.lblStandard.Size = new System.Drawing.Size(79, 15);
            this.lblStandard.TabIndex = 0;
            this.lblStandard.Text = "Bộ Tiêu chuẩn:";
            // 
            // grpInputs
            // 
            this.grpInputs.Controls.Add(this.numTargetROW);
            this.grpInputs.Controls.Add(this.lblTargetROW);
            this.grpInputs.Controls.Add(this.numShoulderSlope);
            this.grpInputs.Controls.Add(this.lblShoulderSlope);
            this.grpInputs.Controls.Add(this.numRoadSlope);
            this.grpInputs.Controls.Add(this.lblRoadSlope);
            this.grpInputs.Controls.Add(this.numBikeLane);
            this.grpInputs.Controls.Add(this.lblBikeLane);
            this.grpInputs.Controls.Add(this.numGreenery);
            this.grpInputs.Controls.Add(this.lblGreenery);
            this.grpInputs.Controls.Add(this.numSidewalk);
            this.grpInputs.Controls.Add(this.lblSidewalk);
            this.grpInputs.Controls.Add(this.numSoftShoulder);
            this.grpInputs.Controls.Add(this.lblSoftShoulder);
            this.grpInputs.Controls.Add(this.numHardShoulder);
            this.grpInputs.Controls.Add(this.lblHardShoulder);
            this.grpInputs.Controls.Add(this.numSafetyStrip);
            this.grpInputs.Controls.Add(this.lblSafetyStrip);
            this.grpInputs.Controls.Add(this.numMedianWidth);
            this.grpInputs.Controls.Add(this.lblMedianWidth);
            this.grpInputs.Controls.Add(this.numLaneWidth);
            this.grpInputs.Controls.Add(this.lblLaneWidth);
            this.grpInputs.Controls.Add(this.numLanesCount);
            this.grpInputs.Controls.Add(this.lblLanesCount);
            this.grpInputs.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.grpInputs.ForeColor = System.Drawing.Color.Navy;
            this.grpInputs.Location = new System.Drawing.Point(12, 75);
            this.grpInputs.Name = "grpInputs";
            this.grpInputs.Size = new System.Drawing.Size(996, 145);
            this.grpInputs.TabIndex = 1;
            this.grpInputs.TabStop = false;
            this.grpInputs.Text = "2. NHẬP THÔNG SỐ MẶT CẮT NGANG ĐỀ XUẤT";
            // 
            // numTargetROW
            // 
            this.numTargetROW.DecimalPlaces = 2;
            this.numTargetROW.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.numTargetROW.ForeColor = System.Drawing.Color.DarkRed;
            this.numTargetROW.Increment = new decimal(new int[] { 1, 0, 0, 0 });
            this.numTargetROW.Location = new System.Drawing.Point(865, 105);
            this.numTargetROW.Maximum = new decimal(new int[] { 200, 0, 0, 0 });
            this.numTargetROW.Name = "numTargetROW";
            this.numTargetROW.Size = new System.Drawing.Size(115, 23);
            this.numTargetROW.TabIndex = 23;
            this.numTargetROW.Value = new decimal(new int[] { 20, 0, 0, 0 });
            this.numTargetROW.ValueChanged += new System.EventHandler(this.OnInputChanged);
            // 
            // lblTargetROW
            // 
            this.lblTargetROW.AutoSize = true;
            this.lblTargetROW.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblTargetROW.ForeColor = System.Drawing.Color.DarkRed;
            this.lblTargetROW.Location = new System.Drawing.Point(710, 107);
            this.lblTargetROW.Name = "lblTargetROW";
            this.lblTargetROW.Size = new System.Drawing.Size(144, 15);
            this.lblTargetROW.TabIndex = 22;
            this.lblTargetROW.Text = "Chỉ giới đường đỏ (m):";
            // 
            // numShoulderSlope
            // 
            this.numShoulderSlope.DecimalPlaces = 1;
            this.numShoulderSlope.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.numShoulderSlope.Increment = new decimal(new int[] { 5, 0, 0, 65536 });
            this.numShoulderSlope.Location = new System.Drawing.Point(575, 105);
            this.numShoulderSlope.Maximum = new decimal(new int[] { 10, 0, 0, 0 });
            this.numShoulderSlope.Name = "numShoulderSlope";
            this.numShoulderSlope.Size = new System.Drawing.Size(115, 23);
            this.numShoulderSlope.TabIndex = 21;
            this.numShoulderSlope.Value = new decimal(new int[] { 40, 0, 0, 65536 });
            this.numShoulderSlope.ValueChanged += new System.EventHandler(this.OnInputChanged);
            // 
            // lblShoulderSlope
            // 
            this.lblShoulderSlope.AutoSize = true;
            this.lblShoulderSlope.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblShoulderSlope.ForeColor = System.Drawing.Color.Black;
            this.lblShoulderSlope.Location = new System.Drawing.Point(455, 107);
            this.lblShoulderSlope.Name = "lblShoulderSlope";
            this.lblShoulderSlope.Size = new System.Drawing.Size(107, 15);
            this.lblShoulderSlope.TabIndex = 20;
            this.lblShoulderSlope.Text = "Độ dốc lề đường (%):";
            // 
            // numRoadSlope
            // 
            this.numRoadSlope.DecimalPlaces = 1;
            this.numRoadSlope.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.numRoadSlope.Increment = new decimal(new int[] { 5, 0, 0, 65536 });
            this.numRoadSlope.Location = new System.Drawing.Point(575, 68);
            this.numRoadSlope.Maximum = new decimal(new int[] { 6, 0, 0, 0 });
            this.numRoadSlope.Name = "numRoadSlope";
            this.numRoadSlope.Size = new System.Drawing.Size(115, 23);
            this.numRoadSlope.TabIndex = 19;
            this.numRoadSlope.Value = new decimal(new int[] { 20, 0, 0, 65536 });
            this.numRoadSlope.ValueChanged += new System.EventHandler(this.OnInputChanged);
            // 
            // lblRoadSlope
            // 
            this.lblRoadSlope.AutoSize = true;
            this.lblRoadSlope.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblRoadSlope.ForeColor = System.Drawing.Color.Black;
            this.lblRoadSlope.Location = new System.Drawing.Point(455, 70);
            this.lblRoadSlope.Name = "lblRoadSlope";
            this.lblRoadSlope.Size = new System.Drawing.Size(117, 15);
            this.lblRoadSlope.TabIndex = 18;
            this.lblRoadSlope.Text = "Dốc ngang mặt (im%):";
            // 
            // numBikeLane
            // 
            this.numBikeLane.DecimalPlaces = 2;
            this.numBikeLane.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.numBikeLane.Increment = new decimal(new int[] { 25, 0, 0, 131072 });
            this.numBikeLane.Location = new System.Drawing.Point(575, 28);
            this.numBikeLane.Maximum = new decimal(new int[] { 10, 0, 0, 0 });
            this.numBikeLane.Name = "numBikeLane";
            this.numBikeLane.Size = new System.Drawing.Size(115, 23);
            this.numBikeLane.TabIndex = 17;
            this.numBikeLane.ValueChanged += new System.EventHandler(this.OnInputChanged);
            // 
            // lblBikeLane
            // 
            this.lblBikeLane.AutoSize = true;
            this.lblBikeLane.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblBikeLane.ForeColor = System.Drawing.Color.Black;
            this.lblBikeLane.Location = new System.Drawing.Point(455, 30);
            this.lblBikeLane.Name = "lblBikeLane";
            this.lblBikeLane.Size = new System.Drawing.Size(109, 15);
            this.lblBikeLane.TabIndex = 16;
            this.lblBikeLane.Text = "Dải xe thô sơ/bên (m):";
            // 
            // numGreenery
            // 
            this.numGreenery.DecimalPlaces = 2;
            this.numGreenery.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.numGreenery.Increment = new decimal(new int[] { 5, 0, 0, 65536 });
            this.numGreenery.Location = new System.Drawing.Point(325, 105);
            this.numGreenery.Maximum = new decimal(new int[] { 10, 0, 0, 0 });
            this.numGreenery.Name = "numGreenery";
            this.numGreenery.Size = new System.Drawing.Size(115, 23);
            this.numGreenery.TabIndex = 15;
            this.numGreenery.ValueChanged += new System.EventHandler(this.OnInputChanged);
            // 
            // lblGreenery
            // 
            this.lblGreenery.AutoSize = true;
            this.lblGreenery.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblGreenery.ForeColor = System.Drawing.Color.Black;
            this.lblGreenery.Location = new System.Drawing.Point(225, 107);
            this.lblGreenery.Name = "lblGreenery";
            this.lblGreenery.Size = new System.Drawing.Size(95, 15);
            this.lblGreenery.TabIndex = 14;
            this.lblGreenery.Text = "Dải cây xanh/bên:";
            // 
            // numSidewalk
            // 
            this.numSidewalk.DecimalPlaces = 2;
            this.numSidewalk.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.numSidewalk.Increment = new decimal(new int[] { 5, 0, 0, 65536 });
            this.numSidewalk.Location = new System.Drawing.Point(325, 68);
            this.numSidewalk.Maximum = new decimal(new int[] { 20, 0, 0, 0 });
            this.numSidewalk.Name = "numSidewalk";
            this.numSidewalk.Size = new System.Drawing.Size(115, 23);
            this.numSidewalk.TabIndex = 13;
            this.numSidewalk.ValueChanged += new System.EventHandler(this.OnInputChanged);
            // 
            // lblSidewalk
            // 
            this.lblSidewalk.AutoSize = true;
            this.lblSidewalk.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblSidewalk.ForeColor = System.Drawing.Color.Black;
            this.lblSidewalk.Location = new System.Drawing.Point(225, 70);
            this.lblSidewalk.Name = "lblSidewalk";
            this.lblSidewalk.Size = new System.Drawing.Size(95, 15);
            this.lblSidewalk.TabIndex = 12;
            this.lblSidewalk.Text = "Rộng Vỉa hè/bên:";
            // 
            // numSoftShoulder
            // 
            this.numSoftShoulder.DecimalPlaces = 2;
            this.numSoftShoulder.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.numSoftShoulder.Increment = new decimal(new int[] { 25, 0, 0, 131072 });
            this.numSoftShoulder.Location = new System.Drawing.Point(325, 28);
            this.numSoftShoulder.Maximum = new decimal(new int[] { 5, 0, 0, 0 });
            this.numSoftShoulder.Name = "numSoftShoulder";
            this.numSoftShoulder.Size = new System.Drawing.Size(115, 23);
            this.numSoftShoulder.TabIndex = 11;
            this.numSoftShoulder.Value = new decimal(new int[] { 5, 0, 0, 65536 });
            this.numSoftShoulder.ValueChanged += new System.EventHandler(this.OnInputChanged);
            // 
            // lblSoftShoulder
            // 
            this.lblSoftShoulder.AutoSize = true;
            this.lblSoftShoulder.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblSoftShoulder.ForeColor = System.Drawing.Color.Black;
            this.lblSoftShoulder.Location = new System.Drawing.Point(225, 30);
            this.lblSoftShoulder.Name = "lblSoftShoulder";
            this.lblSoftShoulder.Size = new System.Drawing.Size(94, 15);
            this.lblSoftShoulder.TabIndex = 10;
            this.lblSoftShoulder.Text = "Lề đất Bld (m/bên):";
            // 
            // numHardShoulder
            // 
            this.numHardShoulder.DecimalPlaces = 2;
            this.numHardShoulder.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.numHardShoulder.Increment = new decimal(new int[] { 25, 0, 0, 131072 });
            this.numHardShoulder.Location = new System.Drawing.Point(95, 105);
            this.numHardShoulder.Maximum = new decimal(new int[] { 5, 0, 0, 0 });
            this.numHardShoulder.Name = "numHardShoulder";
            this.numHardShoulder.Size = new System.Drawing.Size(115, 23);
            this.numHardShoulder.TabIndex = 9;
            this.numHardShoulder.Value = new decimal(new int[] { 15, 0, 0, 65536 });
            this.numHardShoulder.ValueChanged += new System.EventHandler(this.OnInputChanged);
            // 
            // lblHardShoulder
            // 
            this.lblHardShoulder.AutoSize = true;
            this.lblHardShoulder.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblHardShoulder.ForeColor = System.Drawing.Color.Black;
            this.lblHardShoulder.Location = new System.Drawing.Point(10, 107);
            this.lblHardShoulder.Name = "lblHardShoulder";
            this.lblHardShoulder.Size = new System.Drawing.Size(83, 15);
            this.lblHardShoulder.TabIndex = 8;
            this.lblHardShoulder.Text = "Lề gia cố Blgc:";
            // 
            // numSafetyStrip
            // 
            this.numSafetyStrip.DecimalPlaces = 2;
            this.numSafetyStrip.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.numSafetyStrip.Increment = new decimal(new int[] { 25, 0, 0, 131072 });
            this.numSafetyStrip.Location = new System.Drawing.Point(865, 28);
            this.numSafetyStrip.Maximum = new decimal(new int[] { 3, 0, 0, 0 });
            this.numSafetyStrip.Name = "numSafetyStrip";
            this.numSafetyStrip.Size = new System.Drawing.Size(115, 23);
            this.numSafetyStrip.TabIndex = 7;
            this.numSafetyStrip.ValueChanged += new System.EventHandler(this.OnInputChanged);
            // 
            // lblSafetyStrip
            // 
            this.lblSafetyStrip.AutoSize = true;
            this.lblSafetyStrip.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblSafetyStrip.ForeColor = System.Drawing.Color.Black;
            this.lblSafetyStrip.Location = new System.Drawing.Point(710, 30);
            this.lblSafetyStrip.Name = "lblSafetyStrip";
            this.lblSafetyStrip.Size = new System.Drawing.Size(107, 15);
            this.lblSafetyStrip.TabIndex = 6;
            this.lblSafetyStrip.Text = "Dải an toàn (m/bên):";
            // 
            // numMedianWidth
            // 
            this.numMedianWidth.DecimalPlaces = 2;
            this.numMedianWidth.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.numMedianWidth.Increment = new decimal(new int[] { 5, 0, 0, 65536 });
            this.numMedianWidth.Location = new System.Drawing.Point(865, 68);
            this.numMedianWidth.Maximum = new decimal(new int[] { 20, 0, 0, 0 });
            this.numMedianWidth.Name = "numMedianWidth";
            this.numMedianWidth.Size = new System.Drawing.Size(115, 23);
            this.numMedianWidth.TabIndex = 5;
            this.numMedianWidth.ValueChanged += new System.EventHandler(this.OnInputChanged);
            // 
            // lblMedianWidth
            // 
            this.lblMedianWidth.AutoSize = true;
            this.lblMedianWidth.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblMedianWidth.ForeColor = System.Drawing.Color.Black;
            this.lblMedianWidth.Location = new System.Drawing.Point(710, 70);
            this.lblMedianWidth.Name = "lblMedianWidth";
            this.lblMedianWidth.Size = new System.Drawing.Size(146, 15);
            this.lblMedianWidth.TabIndex = 4;
            this.lblMedianWidth.Text = "Dải phân cách giữa (Bdpc):";
            // 
            // numLaneWidth
            // 
            this.numLaneWidth.DecimalPlaces = 2;
            this.numLaneWidth.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.numLaneWidth.Increment = new decimal(new int[] { 25, 0, 0, 131072 });
            this.numLaneWidth.Location = new System.Drawing.Point(95, 68);
            this.numLaneWidth.Maximum = new decimal(new int[] { 5, 0, 0, 0 });
            this.numLaneWidth.Minimum = new decimal(new int[] { 15, 0, 0, 65536 });
            this.numLaneWidth.Name = "numLaneWidth";
            this.numLaneWidth.Size = new System.Drawing.Size(115, 23);
            this.numLaneWidth.TabIndex = 3;
            this.numLaneWidth.Value = new decimal(new int[] { 35, 0, 0, 65536 });
            this.numLaneWidth.ValueChanged += new System.EventHandler(this.OnInputChanged);
            // 
            // lblLaneWidth
            // 
            this.lblLaneWidth.AutoSize = true;
            this.lblLaneWidth.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblLaneWidth.ForeColor = System.Drawing.Color.Black;
            this.lblLaneWidth.Location = new System.Drawing.Point(10, 70);
            this.lblLaneWidth.Name = "lblLaneWidth";
            this.lblLaneWidth.Size = new System.Drawing.Size(81, 15);
            this.lblLaneWidth.TabIndex = 2;
            this.lblLaneWidth.Text = "Rộng 1 làn (m):";
            // 
            // numLanesCount
            // 
            this.numLanesCount.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.numLanesCount.Location = new System.Drawing.Point(95, 28);
            this.numLanesCount.Maximum = new decimal(new int[] { 10, 0, 0, 0 });
            this.numLanesCount.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numLanesCount.Name = "numLanesCount";
            this.numLanesCount.Size = new System.Drawing.Size(115, 23);
            this.numLanesCount.TabIndex = 1;
            this.numLanesCount.Value = new decimal(new int[] { 2, 0, 0, 0 });
            this.numLanesCount.ValueChanged += new System.EventHandler(this.OnInputChanged);
            // 
            // lblLanesCount
            // 
            this.lblLanesCount.AutoSize = true;
            this.lblLanesCount.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblLanesCount.ForeColor = System.Drawing.Color.Black;
            this.lblLanesCount.Location = new System.Drawing.Point(10, 30);
            this.lblLanesCount.Name = "lblLanesCount";
            this.lblLanesCount.Size = new System.Drawing.Size(61, 15);
            this.lblLanesCount.TabIndex = 0;
            this.lblLanesCount.Text = "Số làn xe:";
            // 
            // grpPreview
            // 
            this.grpPreview.Controls.Add(this.picPreview);
            this.grpPreview.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.grpPreview.ForeColor = System.Drawing.Color.Navy;
            this.grpPreview.Location = new System.Drawing.Point(12, 225);
            this.grpPreview.Name = "grpPreview";
            this.grpPreview.Size = new System.Drawing.Size(996, 210);
            this.grpPreview.TabIndex = 2;
            this.grpPreview.TabStop = false;
            this.grpPreview.Text = "3. SƠ ĐỒ HÌNH ẢNH MẶT CẮT NGANG ĐỀ XUẤT (2D PREVIEW)";
            // 
            // picPreview
            // 
            this.picPreview.BackColor = System.Drawing.Color.White;
            this.picPreview.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.picPreview.Dock = System.Windows.Forms.DockStyle.Fill;
            this.picPreview.Location = new System.Drawing.Point(3, 19);
            this.picPreview.Name = "picPreview";
            this.picPreview.Size = new System.Drawing.Size(990, 188);
            this.picPreview.TabIndex = 0;
            this.picPreview.TabStop = false;
            this.picPreview.Paint += new System.Windows.Forms.PaintEventHandler(this.PicPreview_Paint);
            // 
            // grpResults
            // 
            this.grpResults.Controls.Add(this.lblStatusSummary);
            this.grpResults.Controls.Add(this.lblTotalProposedWidth);
            this.grpResults.Controls.Add(this.lblCarriagewayWidth);
            this.grpResults.Controls.Add(this.dgvEvaluationResults);
            this.grpResults.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.grpResults.ForeColor = System.Drawing.Color.Navy;
            this.grpResults.Location = new System.Drawing.Point(12, 440);
            this.grpResults.Name = "grpResults";
            this.grpResults.Size = new System.Drawing.Size(996, 310);
            this.grpResults.TabIndex = 3;
            this.grpResults.TabStop = false;
            this.grpResults.Text = "4. BẢNG ĐÁNH GIÁ VÀ KIỂM TRA SỰ PHÙ HỢP TIÊU CHUẨN";
            // 
            // lblStatusSummary
            // 
            this.lblStatusSummary.AutoSize = true;
            this.lblStatusSummary.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblStatusSummary.ForeColor = System.Drawing.Color.DarkGreen;
            this.lblStatusSummary.Location = new System.Drawing.Point(640, 23);
            this.lblStatusSummary.Name = "lblStatusSummary";
            this.lblStatusSummary.Size = new System.Drawing.Size(306, 19);
            this.lblStatusSummary.TabIndex = 3;
            this.lblStatusSummary.Text = "TRẠNG THÁI: ĐẠT CHUẨN TOÀN BỘ TIÊU CHÍ";
            // 
            // lblTotalProposedWidth
            // 
            this.lblTotalProposedWidth.AutoSize = true;
            this.lblTotalProposedWidth.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblTotalProposedWidth.ForeColor = System.Drawing.Color.DarkRed;
            this.lblTotalProposedWidth.Location = new System.Drawing.Point(260, 24);
            this.lblTotalProposedWidth.Name = "lblTotalProposedWidth";
            this.lblTotalProposedWidth.Size = new System.Drawing.Size(185, 17);
            this.lblTotalProposedWidth.TabIndex = 2;
            this.lblTotalProposedWidth.Text = "Tổng bề rộng MC: 11.00 m";
            // 
            // lblCarriagewayWidth
            // 
            this.lblCarriagewayWidth.AutoSize = true;
            this.lblCarriagewayWidth.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblCarriagewayWidth.ForeColor = System.Drawing.Color.DarkBlue;
            this.lblCarriagewayWidth.Location = new System.Drawing.Point(15, 24);
            this.lblCarriagewayWidth.Name = "lblCarriagewayWidth";
            this.lblCarriagewayWidth.Size = new System.Drawing.Size(176, 17);
            this.lblCarriagewayWidth.TabIndex = 1;
            this.lblCarriagewayWidth.Text = "Mặt xe chạy (Bxc): 7.00 m";
            // 
            // dgvEvaluationResults
            // 
            this.dgvEvaluationResults.AllowUserToAddRows = false;
            this.dgvEvaluationResults.AllowUserToDeleteRows = false;
            this.dgvEvaluationResults.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvEvaluationResults.BackgroundColor = System.Drawing.Color.White;
            this.dgvEvaluationResults.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvEvaluationResults.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.colSTT,
            this.colElement,
            this.colProposed,
            this.colStandard,
            this.colStatus,
            this.colNote});
            this.dgvEvaluationResults.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.dgvEvaluationResults.Location = new System.Drawing.Point(12, 50);
            this.dgvEvaluationResults.MultiSelect = false;
            this.dgvEvaluationResults.Name = "dgvEvaluationResults";
            this.dgvEvaluationResults.ReadOnly = true;
            this.dgvEvaluationResults.RowHeadersVisible = false;
            this.dgvEvaluationResults.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvEvaluationResults.Size = new System.Drawing.Size(972, 245);
            this.dgvEvaluationResults.TabIndex = 0;
            // 
            // colSTT
            // 
            this.colSTT.FillWeight = 30F;
            this.colSTT.HeaderText = "STT";
            this.colSTT.Name = "colSTT";
            this.colSTT.ReadOnly = true;
            // 
            // colElement
            // 
            this.colElement.FillWeight = 120F;
            this.colElement.HeaderText = "Yếu tố Cắt ngang";
            this.colElement.Name = "colElement";
            this.colElement.ReadOnly = true;
            // 
            // colProposed
            // 
            this.colProposed.FillWeight = 80F;
            this.colProposed.HeaderText = "Giá trị Đề xuất";
            this.colProposed.Name = "colProposed";
            this.colProposed.ReadOnly = true;
            // 
            // colStandard
            // 
            this.colStandard.FillWeight = 110F;
            this.colStandard.HeaderText = "Yêu cầu Tiêu chuẩn";
            this.colStandard.Name = "colStandard";
            this.colStandard.ReadOnly = true;
            // 
            // colStatus
            // 
            this.colStatus.FillWeight = 70F;
            this.colStatus.HeaderText = "Đánh giá";
            this.colStatus.Name = "colStatus";
            this.colStatus.ReadOnly = true;
            // 
            // colNote
            // 
            this.colNote.FillWeight = 150F;
            this.colNote.HeaderText = "Ghi chú Đánh giá";
            this.colNote.Name = "colNote";
            this.colNote.ReadOnly = true;
            // 
            // btnEvaluate
            // 
            this.btnEvaluate.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnEvaluate.Location = new System.Drawing.Point(365, 765);
            this.btnEvaluate.Name = "btnEvaluate";
            this.btnEvaluate.Size = new System.Drawing.Size(140, 35);
            this.btnEvaluate.TabIndex = 4;
            this.btnEvaluate.Text = "Kiểm tra lại";
            this.btnEvaluate.UseVisualStyleBackColor = true;
            this.btnEvaluate.Click += new System.EventHandler(this.BtnEvaluate_Click);
            // 
            // btnDrawCadTable
            // 
            this.btnDrawCadTable.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnDrawCadTable.ForeColor = System.Drawing.Color.DarkGreen;
            this.btnDrawCadTable.Location = new System.Drawing.Point(515, 765);
            this.btnDrawCadTable.Name = "btnDrawCadTable";
            this.btnDrawCadTable.Size = new System.Drawing.Size(170, 35);
            this.btnDrawCadTable.TabIndex = 5;
            this.btnDrawCadTable.Text = "Vẽ Bảng vào CAD";
            this.btnDrawCadTable.UseVisualStyleBackColor = true;
            this.btnDrawCadTable.Click += new System.EventHandler(this.BtnDrawCadTable_Click);
            // 
            // btnExportExcel
            // 
            this.btnExportExcel.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnExportExcel.ForeColor = System.Drawing.Color.DarkBlue;
            this.btnExportExcel.Location = new System.Drawing.Point(695, 765);
            this.btnExportExcel.Name = "btnExportExcel";
            this.btnExportExcel.Size = new System.Drawing.Size(160, 35);
            this.btnExportExcel.TabIndex = 6;
            this.btnExportExcel.Text = "Xuất Excel / CSV";
            this.btnExportExcel.UseVisualStyleBackColor = true;
            this.btnExportExcel.Click += new System.EventHandler(this.BtnExportExcel_Click);
            // 
            // btnClose
            // 
            this.btnClose.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnClose.Location = new System.Drawing.Point(865, 765);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(130, 35);
            this.btnClose.TabIndex = 7;
            this.btnClose.Text = "Đóng";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.BtnClose_Click);
            // 
            // DeXuatCatNgangForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1020, 815);
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.btnExportExcel);
            this.Controls.Add(this.btnDrawCadTable);
            this.Controls.Add(this.btnEvaluate);
            this.Controls.Add(this.grpResults);
            this.Controls.Add(this.grpPreview);
            this.Controls.Add(this.grpInputs);
            this.Controls.Add(this.grpStandard);
            this.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "DeXuatCatNgangForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "ĐỀ XUẤT MẶT CẮT NGANG ĐƯỜNG & KIỂM TRA TIÊU CHUẨN QUY CHUẨN";
            this.AcceptButton = this.btnEvaluate;
            this.CancelButton = this.btnClose;
            this.grpStandard.ResumeLayout(false);
            this.grpStandard.PerformLayout();
            this.grpInputs.ResumeLayout(false);
            this.grpInputs.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numTargetROW)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numShoulderSlope)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numRoadSlope)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numBikeLane)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numGreenery)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numSidewalk)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numSoftShoulder)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numHardShoulder)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numSafetyStrip)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numMedianWidth)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numLaneWidth)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.numLanesCount)).EndInit();
            this.grpPreview.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.picPreview)).EndInit();
            this.grpResults.ResumeLayout(false);
            this.grpResults.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvEvaluationResults)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox grpStandard;
        private System.Windows.Forms.Label lblStandard;
        private System.Windows.Forms.ComboBox cboStandard;
        private System.Windows.Forms.Label lblRoadType;
        private System.Windows.Forms.ComboBox cboRoadType;
        private System.Windows.Forms.Label lblSpeed;
        private System.Windows.Forms.ComboBox cboDesignSpeed;
        private System.Windows.Forms.GroupBox grpInputs;
        private System.Windows.Forms.Label lblLanesCount;
        private System.Windows.Forms.NumericUpDown numLanesCount;
        private System.Windows.Forms.Label lblLaneWidth;
        private System.Windows.Forms.NumericUpDown numLaneWidth;
        private System.Windows.Forms.Label lblMedianWidth;
        private System.Windows.Forms.NumericUpDown numMedianWidth;
        private System.Windows.Forms.Label lblSafetyStrip;
        private System.Windows.Forms.NumericUpDown numSafetyStrip;
        private System.Windows.Forms.Label lblHardShoulder;
        private System.Windows.Forms.NumericUpDown numHardShoulder;
        private System.Windows.Forms.Label lblSoftShoulder;
        private System.Windows.Forms.NumericUpDown numSoftShoulder;
        private System.Windows.Forms.Label lblSidewalk;
        private System.Windows.Forms.NumericUpDown numSidewalk;
        private System.Windows.Forms.Label lblGreenery;
        private System.Windows.Forms.NumericUpDown numGreenery;
        private System.Windows.Forms.Label lblBikeLane;
        private System.Windows.Forms.NumericUpDown numBikeLane;
        private System.Windows.Forms.Label lblRoadSlope;
        private System.Windows.Forms.NumericUpDown numRoadSlope;
        private System.Windows.Forms.Label lblShoulderSlope;
        private System.Windows.Forms.NumericUpDown numShoulderSlope;
        private System.Windows.Forms.Label lblTargetROW;
        private System.Windows.Forms.NumericUpDown numTargetROW;
        private System.Windows.Forms.GroupBox grpPreview;
        private System.Windows.Forms.PictureBox picPreview;
        private System.Windows.Forms.GroupBox grpResults;
        private System.Windows.Forms.DataGridView dgvEvaluationResults;
        private System.Windows.Forms.DataGridViewTextBoxColumn colSTT;
        private System.Windows.Forms.DataGridViewTextBoxColumn colElement;
        private System.Windows.Forms.DataGridViewTextBoxColumn colProposed;
        private System.Windows.Forms.DataGridViewTextBoxColumn colStandard;
        private System.Windows.Forms.DataGridViewTextBoxColumn colStatus;
        private System.Windows.Forms.DataGridViewTextBoxColumn colNote;
        private System.Windows.Forms.Label lblCarriagewayWidth;
        private System.Windows.Forms.Label lblTotalProposedWidth;
        private System.Windows.Forms.Label lblStatusSummary;
        private System.Windows.Forms.Button btnEvaluate;
        private System.Windows.Forms.Button btnDrawCadTable;
        private System.Windows.Forms.Button btnExportExcel;
        private System.Windows.Forms.Button btnClose;
    }
}

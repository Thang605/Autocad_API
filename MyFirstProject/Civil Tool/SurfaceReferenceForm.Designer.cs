namespace MyFirstProject
{
    partial class SurfaceReferenceForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.Label lblTitle = null!;
        private System.Windows.Forms.Label lblSurfaceInfo = null!;
        private System.Windows.Forms.GroupBox groupBoxMode = null!;
        private System.Windows.Forms.RadioButton radioSelectedObjects = null!;
        private System.Windows.Forms.RadioButton radioEntireNetwork = null!;
        private System.Windows.Forms.GroupBox groupBoxNetwork = null!;
        private System.Windows.Forms.Label lblNetwork = null!;
        private System.Windows.Forms.ComboBox comboBoxNetworks = null!;
        private System.Windows.Forms.Button btnRefreshNetworks = null!;
        private System.Windows.Forms.Label lblInstructions = null!;
        private System.Windows.Forms.Button btnOK = null!;
        private System.Windows.Forms.Button btnCancel = null!;

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
            this.lblTitle = new System.Windows.Forms.Label();
            this.lblSurfaceInfo = new System.Windows.Forms.Label();
            this.groupBoxMode = new System.Windows.Forms.GroupBox();
            this.radioSelectedObjects = new System.Windows.Forms.RadioButton();
            this.radioEntireNetwork = new System.Windows.Forms.RadioButton();
            this.groupBoxNetwork = new System.Windows.Forms.GroupBox();
            this.lblNetwork = new System.Windows.Forms.Label();
            this.comboBoxNetworks = new System.Windows.Forms.ComboBox();
            this.btnRefreshNetworks = new System.Windows.Forms.Button();
            this.lblInstructions = new System.Windows.Forms.Label();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.groupBoxMode.SuspendLayout();
            this.groupBoxNetwork.SuspendLayout();
            this.SuspendLayout();
            // 
            // lblTitle
            // 
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblTitle.Location = new System.Drawing.Point(12, 9);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(234, 20);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "Thiết lập Surface Reference";
            // 
            // lblSurfaceInfo
            // 
            this.lblSurfaceInfo.AutoSize = true;
            this.lblSurfaceInfo.Location = new System.Drawing.Point(13, 40);
            this.lblSurfaceInfo.Name = "lblSurfaceInfo";
            this.lblSurfaceInfo.Size = new System.Drawing.Size(149, 13);
            this.lblSurfaceInfo.TabIndex = 1;
            this.lblSurfaceInfo.Text = "Mặt phẳng được chọn: Chưa chọn";
            // 
            // groupBoxMode
            // 
            this.groupBoxMode.Controls.Add(this.radioEntireNetwork);
            this.groupBoxMode.Controls.Add(this.radioSelectedObjects);
            this.groupBoxMode.Location = new System.Drawing.Point(16, 65);
            this.groupBoxMode.Name = "groupBoxMode";
            this.groupBoxMode.Size = new System.Drawing.Size(456, 80);
            this.groupBoxMode.TabIndex = 2;
            this.groupBoxMode.TabStop = false;
            this.groupBoxMode.Text = "Chế độ áp dụng";
            // 
            // radioSelectedObjects
            // 
            this.radioSelectedObjects.AutoSize = true;
            this.radioSelectedObjects.Location = new System.Drawing.Point(15, 25);
            this.radioSelectedObjects.Name = "radioSelectedObjects";
            this.radioSelectedObjects.Size = new System.Drawing.Size(208, 17);
            this.radioSelectedObjects.TabIndex = 0;
            this.radioSelectedObjects.TabStop = true;
            this.radioSelectedObjects.Text = "Áp dụng cho các đối tượng được chọn";
            this.radioSelectedObjects.UseVisualStyleBackColor = true;
            this.radioSelectedObjects.CheckedChanged += new System.EventHandler(this.radioSelectedObjects_CheckedChanged);
            // 
            // radioEntireNetwork
            // 
            this.radioEntireNetwork.AutoSize = true;
            this.radioEntireNetwork.Location = new System.Drawing.Point(15, 48);
            this.radioEntireNetwork.Name = "radioEntireNetwork";
            this.radioEntireNetwork.Size = new System.Drawing.Size(271, 17);
            this.radioEntireNetwork.TabIndex = 1;
            this.radioEntireNetwork.TabStop = true;
            this.radioEntireNetwork.Text = "Áp dụng cho toàn bộ đối tượng trong mạng lưới";
            this.radioEntireNetwork.UseVisualStyleBackColor = true;
            this.radioEntireNetwork.CheckedChanged += new System.EventHandler(this.radioEntireNetwork_CheckedChanged);
            // 
            // groupBoxNetwork
            // 
            this.groupBoxNetwork.Controls.Add(this.btnRefreshNetworks);
            this.groupBoxNetwork.Controls.Add(this.comboBoxNetworks);
            this.groupBoxNetwork.Controls.Add(this.lblNetwork);
            this.groupBoxNetwork.Location = new System.Drawing.Point(16, 151);
            this.groupBoxNetwork.Name = "groupBoxNetwork";
            this.groupBoxNetwork.Size = new System.Drawing.Size(456, 70);
            this.groupBoxNetwork.TabIndex = 3;
            this.groupBoxNetwork.TabStop = false;
            this.groupBoxNetwork.Text = "Chọn mạng lưới";
            // 
            // lblNetwork
            // 
            this.lblNetwork.AutoSize = true;
            this.lblNetwork.Location = new System.Drawing.Point(15, 25);
            this.lblNetwork.Name = "lblNetwork";
            this.lblNetwork.Size = new System.Drawing.Size(58, 13);
            this.lblNetwork.TabIndex = 0;
            this.lblNetwork.Text = "Mạng lưới:";
            // 
            // comboBoxNetworks
            // 
            this.comboBoxNetworks.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxNetworks.FormattingEnabled = true;
            this.comboBoxNetworks.Location = new System.Drawing.Point(15, 41);
            this.comboBoxNetworks.Name = "comboBoxNetworks";
            this.comboBoxNetworks.Size = new System.Drawing.Size(320, 21);
            this.comboBoxNetworks.TabIndex = 1;
            // 
            // btnRefreshNetworks
            // 
            this.btnRefreshNetworks.Location = new System.Drawing.Point(341, 39);
            this.btnRefreshNetworks.Name = "btnRefreshNetworks";
            this.btnRefreshNetworks.Size = new System.Drawing.Size(75, 23);
            this.btnRefreshNetworks.TabIndex = 2;
            this.btnRefreshNetworks.Text = "Làm mới";
            this.btnRefreshNetworks.UseVisualStyleBackColor = true;
            this.btnRefreshNetworks.Click += new System.EventHandler(this.btnRefreshNetworks_Click);
            // 
            // lblInstructions
            // 
            this.lblInstructions.Location = new System.Drawing.Point(16, 230);
            this.lblInstructions.Name = "lblInstructions";
            this.lblInstructions.Size = new System.Drawing.Size(456, 40);
            this.lblInstructions.TabIndex = 4;
            this.lblInstructions.Text = "Chế độ: Áp dụng cho các đối tượng được chọn thủ công.";
            // 
            // btnOK
            // 
            this.btnOK.Location = new System.Drawing.Point(316, 280);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 5;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Location = new System.Drawing.Point(397, 280);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 6;
            this.btnCancel.Text = "Hủy";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // SurfaceReferenceForm
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(484, 315);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.lblInstructions);
            this.Controls.Add(this.groupBoxNetwork);
            this.Controls.Add(this.groupBoxMode);
            this.Controls.Add(this.lblSurfaceInfo);
            this.Controls.Add(this.lblTitle);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SurfaceReferenceForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Thiết lập Surface Reference";
            this.groupBoxMode.ResumeLayout(false);
            this.groupBoxMode.PerformLayout();
            this.groupBoxNetwork.ResumeLayout(false);
            this.groupBoxNetwork.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
    }
}

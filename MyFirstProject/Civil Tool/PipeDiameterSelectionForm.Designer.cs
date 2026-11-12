namespace MyFirstProject
{
    partial class PipeDiameterSelectionForm
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
            listBoxPipeSizes = new ListBox();
            btnOK = new Button();
            btnCancel = new Button();
            lblTitle = new Label();
            lblInstructions = new Label();
            lblCurrentInfo = new Label();
            SuspendLayout();
            // 
            // listBoxPipeSizes
            // 
            listBoxPipeSizes.Font = new Font("Segoe UI", 10F);
            listBoxPipeSizes.FormattingEnabled = true;
            listBoxPipeSizes.ItemHeight = 28;
            listBoxPipeSizes.Location = new Point(40, 220);
            listBoxPipeSizes.Name = "listBoxPipeSizes";
            listBoxPipeSizes.Size = new Size(880, 564);
            listBoxPipeSizes.TabIndex = 0;
            listBoxPipeSizes.DoubleClick += listBoxPipeSizes_DoubleClick;
            // 
            // btnOK
            // 
            btnOK.Font = new Font("Segoe UI", 10F);
            btnOK.Location = new Point(560, 820);
            btnOK.Name = "btnOK";
            btnOK.Size = new Size(180, 70);
            btnOK.TabIndex = 1;
            btnOK.Text = "OK";
            btnOK.UseVisualStyleBackColor = true;
            btnOK.Click += btnOK_Click;
            // 
            // btnCancel
            // 
            btnCancel.Font = new Font("Segoe UI", 10F);
            btnCancel.Location = new Point(760, 820);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new Size(180, 70);
            btnCancel.TabIndex = 2;
            btnCancel.Text = "Hủy";
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Click += btnCancel_Click;
            // 
            // lblTitle
            // 
            lblTitle.AutoSize = true;
            lblTitle.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            lblTitle.Location = new Point(40, 30);
            lblTitle.Name = "lblTitle";
            lblTitle.Size = new Size(247, 32);
            lblTitle.TabIndex = 3;
            lblTitle.Text = "Chọn đường kính cống";
            // 
            // lblInstructions
            // 
            lblInstructions.AutoSize = true;
            lblInstructions.Font = new Font("Segoe UI", 9F);
            lblInstructions.Location = new Point(40, 100);
            lblInstructions.Name = "lblInstructions";
            lblInstructions.Size = new Size(421, 25);
            lblInstructions.TabIndex = 4;
            lblInstructions.Text = "Chọn kích thước đường kính cống từ danh sách dưới đây:";
            // 
            // lblCurrentInfo
            // 
            lblCurrentInfo.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblCurrentInfo.ForeColor = Color.DarkBlue;
            lblCurrentInfo.Location = new Point(40, 140);
            lblCurrentInfo.Name = "lblCurrentInfo";
            lblCurrentInfo.Size = new Size(880, 60);
            lblCurrentInfo.TabIndex = 5;
            lblCurrentInfo.Text = "Đường kính hiện tại: Đang tải...";
            // 
            // PipeDiameterSelectionForm
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(968, 922);
            Controls.Add(lblCurrentInfo);
            Controls.Add(lblInstructions);
            Controls.Add(lblTitle);
            Controls.Add(btnCancel);
            Controls.Add(btnOK);
            Controls.Add(listBoxPipeSizes);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "PipeDiameterSelectionForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Chọn đường kính cống";
            AcceptButton = btnOK;
            CancelButton = btnCancel;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private ListBox listBoxPipeSizes;
        private Button btnOK;
        private Button btnCancel;
        private Label lblTitle;
        private Label lblInstructions;
        private Label lblCurrentInfo;
    }
}

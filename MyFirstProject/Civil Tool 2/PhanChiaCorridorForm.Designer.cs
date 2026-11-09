namespace MyFirstProject.Civil_Tool_2
{
    partial class PhanChiaCorridorForm
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
            this.btnChonCorridor = new System.Windows.Forms.Button();
            this.txtCorridorName = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.btnPickPoints = new System.Windows.Forms.Button();
            this.lstStations = new System.Windows.Forms.ListBox();
            this.btnXoaLyTrinh = new System.Windows.Forms.Button();
            this.btnThucHien = new System.Windows.Forms.Button();
            this.btnHuy = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnChonCorridor
            // 
            this.btnChonCorridor.Location = new System.Drawing.Point(270, 26);
            this.btnChonCorridor.Name = "btnChonCorridor";
            this.btnChonCorridor.Size = new System.Drawing.Size(50, 23);
            this.btnChonCorridor.TabIndex = 0;
            this.btnChonCorridor.Text = "...";
            this.btnChonCorridor.UseVisualStyleBackColor = true;
            this.btnChonCorridor.Click += new System.EventHandler(this.btnChonCorridor_Click);
            // 
            // txtCorridorName
            // 
            this.txtCorridorName.Location = new System.Drawing.Point(82, 28);
            this.txtCorridorName.Name = "txtCorridorName";
            this.txtCorridorName.ReadOnly = true;
            this.txtCorridorName.Size = new System.Drawing.Size(182, 20);
            this.txtCorridorName.TabIndex = 1;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(19, 31);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(46, 13);
            this.label1.TabIndex = 2;
            this.label1.Text = "Corridor:";
            // 
            // btnPickPoints
            // 
            this.btnPickPoints.Location = new System.Drawing.Point(22, 30);
            this.btnPickPoints.Name = "btnPickPoints";
            this.btnPickPoints.Size = new System.Drawing.Size(298, 30);
            this.btnPickPoints.TabIndex = 3;
            this.btnPickPoints.Text = "Pick các điểm trên Trắc dọc (Profile View)";
            this.btnPickPoints.UseVisualStyleBackColor = true;
            this.btnPickPoints.Click += new System.EventHandler(this.btnPickPoints_Click);
            // 
            // lstStations
            // 
            this.lstStations.FormattingEnabled = true;
            this.lstStations.Location = new System.Drawing.Point(22, 90);
            this.lstStations.Name = "lstStations";
            this.lstStations.Size = new System.Drawing.Size(298, 121);
            this.lstStations.TabIndex = 4;
            // 
            // btnXoaLyTrinh
            // 
            this.btnXoaLyTrinh.Location = new System.Drawing.Point(22, 217);
            this.btnXoaLyTrinh.Name = "btnXoaLyTrinh";
            this.btnXoaLyTrinh.Size = new System.Drawing.Size(298, 23);
            this.btnXoaLyTrinh.TabIndex = 5;
            this.btnXoaLyTrinh.Text = "Xóa lý trình đang chọn";
            this.btnXoaLyTrinh.UseVisualStyleBackColor = true;
            this.btnXoaLyTrinh.Click += new System.EventHandler(this.btnXoaLyTrinh_Click);
            // 
            // btnThucHien
            // 
            this.btnThucHien.Location = new System.Drawing.Point(125, 351);
            this.btnThucHien.Name = "btnThucHien";
            this.btnThucHien.Size = new System.Drawing.Size(117, 34);
            this.btnThucHien.TabIndex = 6;
            this.btnThucHien.Text = "Thực hiện";
            this.btnThucHien.UseVisualStyleBackColor = true;
            this.btnThucHien.Click += new System.EventHandler(this.btnThucHien_Click);
            // 
            // btnHuy
            // 
            this.btnHuy.Location = new System.Drawing.Point(248, 351);
            this.btnHuy.Name = "btnHuy";
            this.btnHuy.Size = new System.Drawing.Size(100, 34);
            this.btnHuy.TabIndex = 7;
            this.btnHuy.Text = "Hủy bỏ";
            this.btnHuy.UseVisualStyleBackColor = true;
            this.btnHuy.Click += new System.EventHandler(this.btnHuy_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(19, 74);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(147, 13);
            this.label2.TabIndex = 8;
            this.label2.Text = "Danh sách Lý trình chia (Trạm):";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Controls.Add(this.txtCorridorName);
            this.groupBox1.Controls.Add(this.btnChonCorridor);
            this.groupBox1.Location = new System.Drawing.Point(12, 12);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(336, 73);
            this.groupBox1.TabIndex = 9;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "1. Đối tượng chia";
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.btnPickPoints);
            this.groupBox2.Controls.Add(this.label2);
            this.groupBox2.Controls.Add(this.lstStations);
            this.groupBox2.Controls.Add(this.btnXoaLyTrinh);
            this.groupBox2.Location = new System.Drawing.Point(12, 91);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(336, 254);
            this.groupBox2.TabIndex = 10;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "2. Lý trình cắt";
            // 
            // PhanChiaCorridorForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(360, 397);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.btnHuy);
            this.Controls.Add(this.btnThucHien);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "PhanChiaCorridorForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Phân chia Corridor theo Profile";
            this.AcceptButton = this.btnThucHien;
            this.CancelButton = this.btnHuy;
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button btnChonCorridor;
        private System.Windows.Forms.TextBox txtCorridorName;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button btnPickPoints;
        private System.Windows.Forms.ListBox lstStations;
        private System.Windows.Forms.Button btnXoaLyTrinh;
        private System.Windows.Forms.Button btnThucHien;
        private System.Windows.Forms.Button btnHuy;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupBox2;
    }
}

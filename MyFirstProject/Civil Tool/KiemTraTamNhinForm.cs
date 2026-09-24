using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Civil3DCsharp;
using MyFirstProject.Extensions;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using CivSurface = Autodesk.Civil.DatabaseServices.TinSurface;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using WinFormsColor = System.Drawing.Color;
using WinFormsFont = System.Drawing.Font;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsPoint = System.Drawing.Point;

namespace MyFirstProject.Civil_Tool
{
    public class ObjectIdItem
    {
        public string Name { get; set; } = "";
        public ObjectId Id { get; set; } = ObjectId.Null;
        public override string ToString() => Name;
    }

    public class KiemTraTamNhinForm : Form
    {
        #region Persistent State (Ghi nhớ lựa chọn)

        private static int _lastSourceMode = 0; // 0: Alignment, 1: Polyline
        private static ObjectId _lastAlignmentId = ObjectId.Null;
        private static ObjectId _lastProfileId = ObjectId.Null;
        private static ObjectId _lastSurfaceId = ObjectId.Null;
        private static ObjectId _lastPolylineId = ObjectId.Null;

        private static string _lastStandardName = "TCVN 4054:2005";
        private static int _lastSpeed = 60;
        private static int _lastSightTypeIndex = 0; // 0: Dừng xe (SSD), 1: Vượt xe (PSD)
        private static decimal _lastReqSight = 75m;
        private static decimal _lastInterval = 20m;
        private static decimal _lastLaneOffset = 1.75m;
        private static decimal _lastEyeHeight = 1.25m;
        private static decimal _lastTargetHeight = 0.10m;

        private static bool _lastCheckGrade = true;
        private static bool _lastCheckClearance = true;
        private static bool _lastCheck3D = true;
        private static bool _lastFilterOnlyErrors = false;
        private static bool _lastDrawEyePath = true;
        private static Size _lastFormSize = new Size(1180, 780);

        #endregion

        #region Controls

        // Group 1: Nguồn đường kiểm tra (Alignment vs Polyline)
        private RadioButton rbSourceAlignment;
        private RadioButton rbSourcePolyline;

        private Panel pnlAlignmentControls;
        private ComboBox cbbAlignment;
        private Button btnPickAlignment;
        private ComboBox cbbProfile;

        private Panel pnlPolylineControls;
        private TextBox txtPolylineInfo;
        private Button btnPickPolyline;
        private Button btnConvertTo3D;

        private ComboBox cbbSurface;
        private Button btnPickSurface;

        // Group 2: Tiêu chuẩn & Vận tốc
        private ComboBox cbbStandard;
        private ComboBox cbbSpeed;
        private Button btnLookupTable;
        private Button btnResetStandard;
        private RadioButton rbStoppingSight;
        private RadioButton rbPassingSight;
        private WinFormsLabel lblReqSight;
        private NumericUpDown numReqSight;
        private NumericUpDown numInterval;
        private NumericUpDown numLaneOffset;
        private NumericUpDown numEyeHeight;
        private NumericUpDown numTargetHeight;
        private Panel pnlStdSummary;
        private WinFormsLabel lblStdSummaryTitle;
        private WinFormsLabel lblStdSight;
        private WinFormsLabel lblStdVertical;
        private WinFormsLabel lblStdHorizontal;

        // Group 3: Tùy chọn kiểm tra
        private CheckBox chkCheckGrade;
        private CheckBox chkCheckClearance;
        private CheckBox chkCheck3DSurface;
        private CheckBox chkFilterOnlyErrors;
        private CheckBox chkDrawEyePath;

        // Group 4: Phạm vi lý trình
        private TextBox txtStartStation;
        private TextBox txtEndStation;

        // Group 5: Bảng kết quả & Thống kê
        private DataGridView dgvResults;
        private WinFormsLabel lblSummary;

        // Group 6: Nút hành động
        private Button btnAnalyze;
        private Button btnZoomCAD;
        private Button btnDrawRays;
        private Button btnDrawMarks;
        private Button btnExportExcel;
        private Button btnClose;

        #endregion

        private List<SightCheckItem> _analysisResults = new List<SightCheckItem>();
        private ObjectId _selectedPolylineId = ObjectId.Null;
        private bool _isLoading = true;

        public KiemTraTamNhinForm()
        {
            _isLoading = true;
            try
            {
                InitializeCustomComponents();
                LoadCivilObjects();
                RestoreLastSettings();
            }
            finally
            {
                _isLoading = false;
            }

            // Sau khi khôi phục xong toàn bộ cài đặt người dùng, cập nhật thẻ tóm tắt chuẩn mà KHÔNG đè controls
            ApplyStandardLookup(updateControls: false);

            this.FormClosing += (s, e) => SaveCurrentSettings();
        }

        #region UI Setup (Bố Cục Rộng Rãi, Chuẩn Giao Diện, Không Bị Lấp)

        private void InitializeCustomComponents()
        {
            this.Text = "Kiểm Tra & Phân Tích Tầm Nhìn Xe Chạy (Sight Distance Analysis) - TCVN / AASHTO";
            this.Font = new WinFormsFont("Segoe UI", 9.0f, FontStyle.Regular);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(1160, 740);
            this.Size = (_lastFormSize.Width < 1160 || _lastFormSize.Height < 740) ? new Size(1180, 780) : _lastFormSize;

            // 1. Panel Header trên cùng
            var pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 56,
                BackColor = WinFormsColor.FromArgb(28, 64, 112)
            };

            var lblTitle = new WinFormsLabel
            {
                Text = "KIỂM TRA & ĐÁNH GIÁ TẦM NHÌN XE CHẠY (SIGHT DISTANCE)",
                ForeColor = WinFormsColor.White,
                Font = new WinFormsFont("Segoe UI", 12.0f, FontStyle.Bold),
                Location = new WinFormsPoint(16, 8),
                AutoSize = true
            };

            var lblSub = new WinFormsLabel
            {
                Text = "Hỗ trợ Tuyến Civil 3D (Alignment) & Đường Polyline 2D/3D | TCVN 4054, 13592, 5729, 10380 & AASHTO",
                ForeColor = WinFormsColor.FromArgb(200, 220, 245),
                Font = new WinFormsFont("Segoe UI", 8.5f, FontStyle.Regular),
                Location = new WinFormsPoint(17, 32),
                AutoSize = true
            };

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSub);
            this.Controls.Add(pnlHeader);

            // 2. Panel Bottom dưới cùng (Chứa nút bấm & Thống kê)
            var pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                BackColor = WinFormsColor.FromArgb(245, 247, 250),
                Padding = new Padding(12, 8, 12, 8)
            };
            this.Controls.Add(pnlBottom);

            var pnlButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft,
                AutoSize = true,
                WrapContents = false
            };
            pnlBottom.Controls.Add(pnlButtons);

            btnClose = new Button { Text = "Đóng", Width = 75, Height = 34 };
            btnClose.Click += (s, e) => this.Close();
            pnlButtons.Controls.Add(btnClose);

            btnExportExcel = new Button { Text = "Xuất Excel...", Width = 100, Height = 34 };
            btnExportExcel.Click += BtnExportExcel_Click;
            pnlButtons.Controls.Add(btnExportExcel);

            btnDrawMarks = new Button { Text = "Đánh dấu lỗi CAD", Width = 120, Height = 34 };
            btnDrawMarks.Click += BtnDrawMarks_Click;
            pnlButtons.Controls.Add(btnDrawMarks);

            btnDrawRays = new Button { Text = "Vẽ tia ngắm CAD", Width = 120, Height = 34 };
            btnDrawRays.Click += BtnDrawRays_Click;
            pnlButtons.Controls.Add(btnDrawRays);

            btnZoomCAD = new Button { Text = "Zoom CAD", Width = 85, Height = 34 };
            btnZoomCAD.Click += BtnZoomCAD_Click;
            pnlButtons.Controls.Add(btnZoomCAD);

            btnAnalyze = new Button
            {
                Text = "Phân Tích & Kiểm Tra",
                Width = 165,
                Height = 34,
                BackColor = WinFormsColor.FromArgb(28, 64, 112),
                ForeColor = WinFormsColor.White,
                Font = new WinFormsFont("Segoe UI", 9.0f, FontStyle.Bold)
            };
            btnAnalyze.Click += BtnAnalyze_Click;
            pnlButtons.Controls.Add(btnAnalyze);

            lblSummary = new WinFormsLabel
            {
                Text = "Chưa phân tích.",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new WinFormsFont("Segoe UI", 9.0f, FontStyle.Bold),
                ForeColor = WinFormsColor.DarkBlue
            };
            pnlBottom.Controls.Add(lblSummary);

            // 3. Panel Top Controls (Dock: Top, Chiều cao 312px thoáng đãng)
            var pnlTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 312,
                Padding = new Padding(12, 10, 12, 4)
            };
            this.Controls.Add(pnlTop);

            var topTable = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1
            };
            topTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34f));
            topTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43f));
            topTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 23f));
            pnlTop.Controls.Add(topTable);

            // ==========================================
            // GROUP 1: NGUỒN ĐƯỜNG KIỂM TRA (ALIGNMENT HOẶC POLYLINE)
            // ==========================================
            var grpSource = new GroupBox
            {
                Text = "1. Đối tượng đường kiểm tra",
                Dock = DockStyle.Fill,
                Font = new WinFormsFont("Segoe UI", 9.0f, FontStyle.Bold)
            };
            topTable.Controls.Add(grpSource, 0, 0);

            var pnlGrp1 = new Panel { Dock = DockStyle.Fill, Font = new WinFormsFont("Segoe UI", 8.75f, FontStyle.Regular), Padding = new Padding(6) };
            grpSource.Controls.Add(pnlGrp1);

            int y = 4;
            rbSourceAlignment = new RadioButton { Text = "Tuyến Civil 3D", Location = new WinFormsPoint(6, y), AutoSize = true, Checked = true };
            rbSourceAlignment.CheckedChanged += RbSourceMode_CheckedChanged;
            pnlGrp1.Controls.Add(rbSourceAlignment);

            rbSourcePolyline = new RadioButton { Text = "Đường Polyline (CAD 2D/3D)", Location = new WinFormsPoint(125, y), AutoSize = true };
            rbSourcePolyline.CheckedChanged += RbSourceMode_CheckedChanged;
            pnlGrp1.Controls.Add(rbSourcePolyline);

            // Panel con cho Alignment
            y += 26;
            pnlAlignmentControls = new Panel { Location = new WinFormsPoint(2, y), Size = new Size(310, 112) };
            pnlGrp1.Controls.Add(pnlAlignmentControls);

            int subY = 0;
            var lblAlign = new WinFormsLabel { Text = "Tim tuyến (Alignment):", Location = new WinFormsPoint(4, subY), AutoSize = true };
            pnlAlignmentControls.Controls.Add(lblAlign);

            cbbAlignment = new ComboBox { Location = new WinFormsPoint(4, subY + 19), Width = 232, DropDownStyle = ComboBoxStyle.DropDownList };
            cbbAlignment.SelectedIndexChanged += CbbAlignment_SelectedIndexChanged;
            pnlAlignmentControls.Controls.Add(cbbAlignment);

            btnPickAlignment = new Button { Text = "Pick", Location = new WinFormsPoint(240, subY + 18), Width = 52, Height = 25 };
            btnPickAlignment.Click += BtnPickAlignment_Click;
            pnlAlignmentControls.Controls.Add(btnPickAlignment);

            subY += 46;
            var lblProf = new WinFormsLabel { Text = "Trắc dọc thiết kế (Profile):", Location = new WinFormsPoint(4, subY), AutoSize = true };
            pnlAlignmentControls.Controls.Add(lblProf);

            cbbProfile = new ComboBox { Location = new WinFormsPoint(4, subY + 19), Width = 288, DropDownStyle = ComboBoxStyle.DropDownList };
            pnlAlignmentControls.Controls.Add(cbbProfile);

            // Panel con cho Polyline
            pnlPolylineControls = new Panel { Location = new WinFormsPoint(2, y), Size = new Size(310, 112), Visible = false };
            pnlGrp1.Controls.Add(pnlPolylineControls);

            subY = 0;
            var lblPoly = new WinFormsLabel { Text = "Đường Polyline đã chọn:", Location = new WinFormsPoint(4, subY), AutoSize = true };
            pnlPolylineControls.Controls.Add(lblPoly);

            txtPolylineInfo = new TextBox { Location = new WinFormsPoint(4, subY + 19), Width = 232, ReadOnly = true, BackColor = WinFormsColor.WhiteSmoke, Text = "(Chưa chọn Polyline)" };
            pnlPolylineControls.Controls.Add(txtPolylineInfo);

            btnPickPolyline = new Button { Text = "Pick", Location = new WinFormsPoint(240, subY + 18), Width = 52, Height = 25 };
            btnPickPolyline.Click += BtnPickPolyline_Click;
            pnlPolylineControls.Controls.Add(btnPickPolyline);

            subY += 48;
            btnConvertTo3D = new Button
            {
                Text = "⚡ Chuyển Polyline 2D sang 3D theo Surface",
                Location = new WinFormsPoint(4, subY),
                Width = 288,
                Height = 28,
                BackColor = WinFormsColor.FromArgb(235, 245, 255),
                ForeColor = WinFormsColor.FromArgb(10, 60, 130)
            };
            btnConvertTo3D.Click += BtnConvertTo3D_Click;
            pnlPolylineControls.Controls.Add(btnConvertTo3D);

            // Surface nằm cố định bên dưới Group 1
            y += 114;
            var lblSurf = new WinFormsLabel { Text = "Bề mặt địa hình (Surface - kiểm tra 3D Ray):", Location = new WinFormsPoint(6, y), AutoSize = true };
            pnlGrp1.Controls.Add(lblSurf);

            cbbSurface = new ComboBox { Location = new WinFormsPoint(6, y + 20), Width = 232, DropDownStyle = ComboBoxStyle.DropDownList };
            pnlGrp1.Controls.Add(cbbSurface);

            btnPickSurface = new Button { Text = "Pick", Location = new WinFormsPoint(242, y + 19), Width = 52, Height = 25 };
            btnPickSurface.Click += BtnPickSurface_Click;
            pnlGrp1.Controls.Add(btnPickSurface);

            // ==========================================
            // GROUP 2: TIÊU CHUẨN & THÔNG SỐ (BỐ CỤC LƯỚI RÕ RÀNG)
            // ==========================================
            var grpStandard = new GroupBox
            {
                Text = "2. Tiêu chuẩn, Vận tốc & Tra cứu",
                Dock = DockStyle.Fill,
                Font = new WinFormsFont("Segoe UI", 9.0f, FontStyle.Bold)
            };
            topTable.Controls.Add(grpStandard, 1, 0);

            var pnlGrp2 = new Panel { Dock = DockStyle.Fill, Font = new WinFormsFont("Segoe UI", 8.75f, FontStyle.Regular), Padding = new Padding(6) };
            grpStandard.Controls.Add(pnlGrp2);

            // Hàng 1: Tiêu chuẩn, Vận tốc, Bảng tra, Tra lại
            y = 2;
            var lblStd = new WinFormsLabel { Text = "Tiêu chuẩn:", Location = new WinFormsPoint(6, y), AutoSize = true };
            pnlGrp2.Controls.Add(lblStd);

            cbbStandard = new ComboBox { Location = new WinFormsPoint(6, y + 18), Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
            cbbStandard.SelectedIndexChanged += CbbStandard_SelectedIndexChanged;
            pnlGrp2.Controls.Add(cbbStandard);

            var lblSpd = new WinFormsLabel { Text = "Vận tốc V:", Location = new WinFormsPoint(172, y), AutoSize = true };
            pnlGrp2.Controls.Add(lblSpd);

            cbbSpeed = new ComboBox { Location = new WinFormsPoint(172, y + 18), Width = 72, DropDownStyle = ComboBoxStyle.DropDown };
            cbbSpeed.SelectedIndexChanged += CbbSpeed_SelectedIndexChanged;
            cbbSpeed.TextChanged += CbbSpeed_TextChanged;
            cbbSpeed.Leave += (s, e) => ApplyStandardLookup(updateControls: true);
            cbbSpeed.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    ApplyStandardLookup(updateControls: true);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };
            pnlGrp2.Controls.Add(cbbSpeed);

            btnLookupTable = new Button
            {
                Text = "📋 Bảng tra...",
                Location = new WinFormsPoint(250, y + 17),
                Width = 92,
                Height = 26,
                BackColor = WinFormsColor.FromArgb(235, 245, 255),
                ForeColor = WinFormsColor.FromArgb(10, 60, 130)
            };
            btnLookupTable.Click += BtnLookupTable_Click;
            pnlGrp2.Controls.Add(btnLookupTable);

            btnResetStandard = new Button
            {
                Text = "🔄 Tra lại",
                Location = new WinFormsPoint(348, y + 17),
                Width = 82,
                Height = 26
            };
            btnResetStandard.Click += BtnResetStandard_Click;
            pnlGrp2.Controls.Add(btnResetStandard);

            // Hàng 2: Loại tầm nhìn (Dừng xe / Vượt xe) & Ô nhập Tầm nhìn yêu cầu S (m)
            y += 46;
            var lblType = new WinFormsLabel { Text = "Loại tầm nhìn:", Location = new WinFormsPoint(6, y), AutoSize = true };
            pnlGrp2.Controls.Add(lblType);

            rbStoppingSight = new RadioButton { Text = "Dừng xe (SSD)", Location = new WinFormsPoint(6, y + 18), Width = 108, Checked = true };
            rbStoppingSight.CheckedChanged += (s, e) =>
            {
                if (rbStoppingSight.Checked) ApplyStandardSightTypeChanged();
            };
            pnlGrp2.Controls.Add(rbStoppingSight);

            rbPassingSight = new RadioButton { Text = "Vượt xe (PSD)", Location = new WinFormsPoint(118, y + 18), Width = 108 };
            rbPassingSight.CheckedChanged += (s, e) =>
            {
                if (rbPassingSight.Checked) ApplyStandardSightTypeChanged();
            };
            pnlGrp2.Controls.Add(rbPassingSight);

            lblReqSight = new WinFormsLabel
            {
                Text = "Tầm nhìn yêu cầu S (m):",
                Location = new WinFormsPoint(232, y),
                AutoSize = true,
                Font = new WinFormsFont("Segoe UI", 9.0f, FontStyle.Bold),
                ForeColor = WinFormsColor.FromArgb(15, 60, 140)
            };
            pnlGrp2.Controls.Add(lblReqSight);

            numReqSight = new NumericUpDown
            {
                Location = new WinFormsPoint(232, y + 18),
                Width = 98,
                DecimalPlaces = 1,
                Increment = 5m,
                Minimum = 5m,
                Maximum = 2000m,
                Value = 75m,
                Font = new WinFormsFont("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = WinFormsColor.FromArgb(10, 50, 130),
                BackColor = WinFormsColor.FromArgb(240, 248, 255)
            };
            pnlGrp2.Controls.Add(numReqSight);

            var lblReqSightHint = new WinFormsLabel
            {
                Text = "(Tra chuẩn / Sửa)",
                Location = new WinFormsPoint(335, y + 21),
                AutoSize = true,
                Font = new WinFormsFont("Segoe UI", 7.75f, FontStyle.Regular),
                ForeColor = WinFormsColor.Gray
            };
            pnlGrp2.Controls.Add(lblReqSightHint);

            // Hàng 3: 4 thông số hình học xe chạy: Mắt h1, Vật cản h2, Offset làn, Bước kiểm tra
            y += 46;
            var lblEye = new WinFormsLabel { Text = "Mắt h1 (m):", Location = new WinFormsPoint(6, y), AutoSize = true };
            pnlGrp2.Controls.Add(lblEye);

            numEyeHeight = new NumericUpDown { Location = new WinFormsPoint(6, y + 18), Width = 80, DecimalPlaces = 2, Increment = 0.05m, Minimum = 0.5m, Maximum = 3.0m, Value = 1.25m };
            pnlGrp2.Controls.Add(numEyeHeight);

            var lblTgt = new WinFormsLabel { Text = "Vật cản h2 (m):", Location = new WinFormsPoint(92, y), AutoSize = true };
            pnlGrp2.Controls.Add(lblTgt);

            numTargetHeight = new NumericUpDown { Location = new WinFormsPoint(92, y + 18), Width = 80, DecimalPlaces = 2, Increment = 0.05m, Minimum = 0.05m, Maximum = 3.0m, Value = 0.10m };
            pnlGrp2.Controls.Add(numTargetHeight);

            var lblLane = new WinFormsLabel { Text = "Offset làn (m):", Location = new WinFormsPoint(178, y), AutoSize = true };
            pnlGrp2.Controls.Add(lblLane);

            numLaneOffset = new NumericUpDown { Location = new WinFormsPoint(178, y + 18), Width = 82, DecimalPlaces = 2, Increment = 0.25m, Minimum = -20m, Maximum = 20m, Value = 1.75m };
            pnlGrp2.Controls.Add(numLaneOffset);

            var lblStep = new WinFormsLabel { Text = "Bước kt (m):", Location = new WinFormsPoint(266, y), AutoSize = true };
            pnlGrp2.Controls.Add(lblStep);

            numInterval = new NumericUpDown { Location = new WinFormsPoint(266, y + 18), Width = 72, DecimalPlaces = 0, Increment = 5m, Minimum = 5m, Maximum = 200m, Value = 20m };
            pnlGrp2.Controls.Add(numInterval);

            // Hàng 4: Thẻ tóm tắt thông số tra cứu chuẩn (4 dòng độc lập, không che khuất)
            y += 44;
            pnlStdSummary = new Panel
            {
                Location = new WinFormsPoint(6, y),
                Size = new Size(430, 88),
                BackColor = WinFormsColor.FromArgb(240, 247, 255),
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(6, 4, 6, 4)
            };
            pnlGrp2.Controls.Add(pnlStdSummary);

            lblStdSummaryTitle = new WinFormsLabel
            {
                Location = new WinFormsPoint(6, 4),
                Size = new Size(416, 18),
                Font = new WinFormsFont("Segoe UI", 8.25f, FontStyle.Bold),
                ForeColor = WinFormsColor.FromArgb(20, 70, 140),
                Text = "💡 Thông số tra cứu chuẩn:"
            };
            pnlStdSummary.Controls.Add(lblStdSummaryTitle);

            lblStdSight = new WinFormsLabel
            {
                Location = new WinFormsPoint(6, 23),
                Size = new Size(416, 18),
                Font = new WinFormsFont("Segoe UI", 8.0f, FontStyle.Regular),
                ForeColor = WinFormsColor.FromArgb(20, 30, 45),
                Text = "• Tầm nhìn: Đang nạp dữ liệu..."
            };
            pnlStdSummary.Controls.Add(lblStdSight);

            lblStdVertical = new WinFormsLabel
            {
                Location = new WinFormsPoint(6, 42),
                Size = new Size(416, 18),
                Font = new WinFormsFont("Segoe UI", 8.0f, FontStyle.Regular),
                ForeColor = WinFormsColor.FromArgb(20, 30, 45),
                Text = "• Cong đứng: Đang nạp dữ liệu..."
            };
            pnlStdSummary.Controls.Add(lblStdVertical);

            lblStdHorizontal = new WinFormsLabel
            {
                Location = new WinFormsPoint(6, 61),
                Size = new Size(416, 18),
                Font = new WinFormsFont("Segoe UI", 8.0f, FontStyle.Regular),
                ForeColor = WinFormsColor.FromArgb(20, 30, 45),
                Text = "• Cong nằm & Dốc: Đang nạp dữ liệu..."
            };
            pnlStdSummary.Controls.Add(lblStdHorizontal);

            // ==========================================
            // GROUP 3: PHẠM VI & TÙY CHỌN PHÂN TÍCH
            // ==========================================
            var grpOptions = new GroupBox
            {
                Text = "3. Phạm vi & Tùy chọn",
                Dock = DockStyle.Fill,
                Font = new WinFormsFont("Segoe UI", 9.0f, FontStyle.Bold)
            };
            topTable.Controls.Add(grpOptions, 2, 0);

            var pnlGrp3 = new Panel { Dock = DockStyle.Fill, Font = new WinFormsFont("Segoe UI", 8.75f, FontStyle.Regular), Padding = new Padding(6) };
            grpOptions.Controls.Add(pnlGrp3);

            y = 4;
            var lblStRange = new WinFormsLabel { Text = "Lý trình kiểm tra (Từ - Đến):", Location = new WinFormsPoint(6, y), AutoSize = true };
            pnlGrp3.Controls.Add(lblStRange);

            txtStartStation = new TextBox { Location = new WinFormsPoint(6, y + 20), Width = 120, ReadOnly = true, BackColor = WinFormsColor.WhiteSmoke };
            pnlGrp3.Controls.Add(txtStartStation);

            txtEndStation = new TextBox { Location = new WinFormsPoint(135, y + 20), Width = 120, ReadOnly = true, BackColor = WinFormsColor.WhiteSmoke };
            pnlGrp3.Controls.Add(txtEndStation);

            y += 48;
            chkCheckGrade = new CheckBox { Text = "Xét độ dốc dọc i (%) đến tầm nhìn SSD", Location = new WinFormsPoint(6, y), AutoSize = true, Checked = true };
            pnlGrp3.Controls.Add(chkCheckGrade);

            y += 24;
            chkCheckClearance = new CheckBox { Text = "Tính khoảng dạt tầm nhìn bụng cong M", Location = new WinFormsPoint(6, y), AutoSize = true, Checked = true };
            pnlGrp3.Controls.Add(chkCheckClearance);

            y += 24;
            chkCheck3DSurface = new CheckBox { Text = "Kiểm tra tia ngắm 3D cắt địa hình (Surface)", Location = new WinFormsPoint(6, y), AutoSize = true, Checked = true };
            pnlGrp3.Controls.Add(chkCheck3DSurface);

            y += 26;
            chkFilterOnlyErrors = new CheckBox { Text = "Chỉ lọc các vị trí Cảnh báo / Không đạt", Location = new WinFormsPoint(6, y), AutoSize = true, Checked = false };
            chkFilterOnlyErrors.CheckedChanged += (s, e) => FilterAndDisplayResults();
            pnlGrp3.Controls.Add(chkFilterOnlyErrors);

            y += 24;
            chkDrawEyePath = new CheckBox { Text = "Vẽ quỹ đạo mắt lái xe (Polyline offset làn)", Location = new WinFormsPoint(6, y), AutoSize = true, Checked = true };
            pnlGrp3.Controls.Add(chkDrawEyePath);

            // ==========================================
            // 4. BẢNG KẾT QUẢ DATAGRIDVIEW (DOCK FILL Ở GIỮA)
            // ==========================================
            var grpResults = new GroupBox
            {
                Text = "4. Bảng Kết Quả Phân Tích Chi Tiết (Double-click dòng để Zoom đến vị trí trên CAD)",
                Dock = DockStyle.Fill,
                Font = new WinFormsFont("Segoe UI", 9.0f, FontStyle.Bold)
            };
            this.Controls.Add(grpResults);

            var pnlGrid = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10), Font = new WinFormsFont("Segoe UI", 8.75f, FontStyle.Regular) };
            grpResults.Controls.Add(pnlGrid);

            dgvResults = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = WinFormsColor.White,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None
            };
            dgvResults.DoubleClick += DgvResults_DoubleClick;
            pnlGrid.Controls.Add(dgvResults);

            SetupGridColumns();
        }

        private void SetupGridColumns()
        {
            dgvResults.Columns.Clear();
            dgvResults.Columns.Add("colStt", "STT");
            dgvResults.Columns.Add("colStatus", "Trạng thái");
            dgvResults.Columns.Add("colStation", "Lý trình");
            dgvResults.Columns.Add("colGrade", "Độ dốc i (%)");
            dgvResults.Columns.Add("colRadius", "Bán kính R (m)");
            dgvResults.Columns.Add("colReqSight", "Tầm nhìn yc (m)");
            dgvResults.Columns.Add("colActSight", "Khả kiến (m)");
            dgvResults.Columns.Add("colClearance", "Dạt bụng M (m)");
            dgvResults.Columns.Add("colDetail", "Chi tiết đánh giá");

            dgvResults.Columns["colStt"].Width = 45;
            dgvResults.Columns["colStatus"].Width = 95;
            dgvResults.Columns["colStation"].Width = 105;
            dgvResults.Columns["colGrade"].Width = 95;
            dgvResults.Columns["colRadius"].Width = 110;
            dgvResults.Columns["colReqSight"].Width = 115;
            dgvResults.Columns["colActSight"].Width = 105;
            dgvResults.Columns["colClearance"].Width = 105;
            dgvResults.Columns["colDetail"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            foreach (DataGridViewColumn col in dgvResults.Columns)
            {
                col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }
        }

        private void RbSourceMode_CheckedChanged(object sender, EventArgs e)
        {
            if (_isLoading) return;
            bool isAlign = rbSourceAlignment.Checked;
            pnlAlignmentControls.Visible = isAlign;
            pnlPolylineControls.Visible = !isAlign;

            if (isAlign)
            {
                CbbAlignment_SelectedIndexChanged(null, EventArgs.Empty);
            }
            else
            {
                UpdatePolylineStationDisplay();
            }
        }

        #endregion

        #region Data Loading & Events

        private void LoadCivilObjects()
        {
            cbbAlignment.Items.Clear();
            cbbSurface.Items.Clear();
            cbbSurface.Items.Add(new ObjectIdItem { Name = "(Không sử dụng bề mặt 3D)", Id = ObjectId.Null });

            try
            {
                using (var tr = A.Db.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(A.Db.BlockTableId, OpenMode.ForRead);
                    var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                    foreach (ObjectId id in btr)
                    {
                        if (id.ObjectClass.Name == "AeccDbAlignment")
                        {
                            var align = tr.GetObject(id, OpenMode.ForRead) as Alignment;
                            if (align != null && !align.IsConnectedAlignment)
                            {
                                cbbAlignment.Items.Add(new ObjectIdItem { Name = align.Name, Id = id });
                            }
                        }
                        else if (id.ObjectClass.Name == "AeccDbTinSurface")
                        {
                            var surf = tr.GetObject(id, OpenMode.ForRead) as CivSurface;
                            if (surf != null)
                            {
                                cbbSurface.Items.Add(new ObjectIdItem { Name = surf.Name, Id = id });
                            }
                        }
                    }
                    tr.Commit();
                }

                // Khôi phục Alignment đã chọn
                if (cbbAlignment.Items.Count > 0)
                {
                    int selectIdx = 0;
                    if (!_lastAlignmentId.IsNull && _lastAlignmentId.IsValid && !_lastAlignmentId.IsErased)
                    {
                        for (int i = 0; i < cbbAlignment.Items.Count; i++)
                        {
                            if (((ObjectIdItem)cbbAlignment.Items[i]).Id == _lastAlignmentId)
                            {
                                selectIdx = i;
                                break;
                            }
                        }
                    }
                    cbbAlignment.SelectedIndex = selectIdx;
                }

                // Khôi phục Surface đã chọn
                if (cbbSurface.Items.Count > 0)
                {
                    int surfIdx = 0;
                    if (!_lastSurfaceId.IsNull && _lastSurfaceId.IsValid && !_lastSurfaceId.IsErased)
                    {
                        for (int i = 0; i < cbbSurface.Items.Count; i++)
                        {
                            if (((ObjectIdItem)cbbSurface.Items[i]).Id == _lastSurfaceId)
                            {
                                surfIdx = i;
                                break;
                            }
                        }
                    }
                    cbbSurface.SelectedIndex = surfIdx;
                }

                // Khôi phục Polyline đã chọn
                if (!_lastPolylineId.IsNull && _lastPolylineId.IsValid && !_lastPolylineId.IsErased)
                {
                    _selectedPolylineId = _lastPolylineId;
                    UpdatePolylineDisplayInfo(_selectedPolylineId);
                }
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi nạp đối tượng Civil 3D: {ex.Message}");
            }

            // Nạp danh sách tiêu chuẩn
            var standards = SightStandardRepo.GetAllStandards();
            cbbStandard.DataSource = standards;
            cbbStandard.DisplayMember = "Name";
        }

        private void LoadProfilesForAlignment(ObjectId alignId, ObjectId selectProfileId)
        {
            cbbProfile.Items.Clear();
            cbbProfile.Items.Add(new ObjectIdItem { Name = "(Không sử dụng Profile)", Id = ObjectId.Null });

            if (alignId.IsNull || !alignId.IsValid) return;

            try
            {
                using (var tr = A.Db.TransactionManager.StartTransaction())
                {
                    var align = tr.GetObject(alignId, OpenMode.ForRead) as Alignment;
                    if (align != null)
                    {
                        txtStartStation.Text = $"Km{Math.Floor(align.StartingStation / 1000):0}+{align.StartingStation % 1000:000.00}";
                        txtEndStation.Text = $"Km{Math.Floor(align.EndingStation / 1000):0}+{align.EndingStation % 1000:000.00}";

                        var profIds = align.GetProfileIds();
                        foreach (ObjectId pId in profIds)
                        {
                            var prof = tr.GetObject(pId, OpenMode.ForRead) as Profile;
                            if (prof != null)
                            {
                                cbbProfile.Items.Add(new ObjectIdItem { Name = prof.Name, Id = pId });
                            }
                        }
                    }
                    tr.Commit();
                }

                int profIdx = 0;
                if (!selectProfileId.IsNull && selectProfileId.IsValid && !selectProfileId.IsErased)
                {
                    for (int i = 0; i < cbbProfile.Items.Count; i++)
                    {
                        if (((ObjectIdItem)cbbProfile.Items[i]).Id == selectProfileId)
                        {
                            profIdx = i;
                            break;
                        }
                    }
                }
                else if (cbbProfile.Items.Count > 1 && selectProfileId != ObjectId.Null)
                {
                    profIdx = 1;
                }
                cbbProfile.SelectedIndex = profIdx;
            }
            catch { }
        }

        private void CbbAlignment_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_isLoading) return;
            if (!rbSourceAlignment.Checked) return;

            if (cbbAlignment.SelectedItem is ObjectIdItem alignItem && !alignItem.Id.IsNull)
            {
                LoadProfilesForAlignment(alignItem.Id, _lastProfileId);
            }
        }

        private bool _isUpdatingSpeed = false;

        private int GetSelectedSpeed()
        {
            if (int.TryParse(cbbSpeed.Text.Trim(), out int spd) && spd > 0)
                return spd;
            if (cbbSpeed.SelectedItem != null && int.TryParse(cbbSpeed.SelectedItem.ToString(), out int itemSpd) && itemSpd > 0)
                return itemSpd;
            return 60;
        }

        private void CbbSpeed_TextChanged(object? sender, EventArgs e)
        {
            if (_isLoading || _isUpdatingSpeed) return;
            string text = cbbSpeed.Text.Trim();
            if (int.TryParse(text, out int spd) && spd >= 10 && spd <= 200)
            {
                ApplyStandardLookup(updateControls: true);
            }
        }

        private void CbbStandard_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_isLoading) return;
            if (cbbStandard.SelectedItem is not SightStandardDefinition std) return;

            _isUpdatingSpeed = true;
            int curSpeed = GetSelectedSpeed();
            cbbSpeed.Items.Clear();
            foreach (var entry in std.Entries)
            {
                cbbSpeed.Items.Add(entry.Speed);
            }

            int idx = cbbSpeed.Items.IndexOf(curSpeed);
            if (idx >= 0)
            {
                cbbSpeed.SelectedIndex = idx;
            }
            else
            {
                // Chọn cấp tốc độ gần nhất trong tiêu chuẩn
                var closest = std.Entries.OrderBy(x => Math.Abs(x.Speed - curSpeed)).FirstOrDefault();
                if (closest != null)
                {
                    int cIdx = cbbSpeed.Items.IndexOf(closest.Speed);
                    if (cIdx >= 0) cbbSpeed.SelectedIndex = cIdx;
                    else cbbSpeed.Text = closest.Speed.ToString();
                }
                else
                {
                    cbbSpeed.Text = curSpeed.ToString();
                }
            }
            _isUpdatingSpeed = false;

            ApplyStandardLookup(updateControls: true);
        }

        private void CbbSpeed_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_isLoading || _isUpdatingSpeed) return;
            ApplyStandardLookup(updateControls: true);
        }

        private void BtnLookupTable_Click(object? sender, EventArgs e)
        {
            string curStd = cbbStandard.Text;
            int curSpeed = GetSelectedSpeed();

            using (var dlg = new BangTraTieuChuanTamNhinForm(curStd, curSpeed))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && dlg.SelectedStandard != null && dlg.SelectedEntry != null)
                {
                    // Chọn lại tiêu chuẩn
                    for (int i = 0; i < cbbStandard.Items.Count; i++)
                    {
                        if (cbbStandard.Items[i] is SightStandardDefinition s && s.Name == dlg.SelectedStandard.Name)
                        {
                            cbbStandard.SelectedIndex = i;
                            break;
                        }
                    }

                    // Chọn lại vận tốc
                    _isUpdatingSpeed = true;
                    int newSpeed = dlg.SelectedEntry.Speed;
                    int spdIdx = cbbSpeed.Items.IndexOf(newSpeed);
                    if (spdIdx >= 0) cbbSpeed.SelectedIndex = spdIdx;
                    else cbbSpeed.Text = newSpeed.ToString();
                    _isUpdatingSpeed = false;

                    ApplyStandardLookup(updateControls: true);
                }
            }
        }

        private void BtnResetStandard_Click(object? sender, EventArgs e)
        {
            ApplyStandardLookup(updateControls: true);
            MessageBox.Show("Đã nạp lại toàn bộ thông số chuẩn (Tầm nhìn yêu cầu S, Mắt h1, Vật cản h2, Offset làn, Bước kiểm tra) theo Tiêu chuẩn & Vận tốc đang chọn!", "Tra Lại Chuẩn", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ApplyStandardSightTypeChanged()
        {
            if (_isLoading) return;
            if (cbbStandard.SelectedItem is not SightStandardDefinition std) return;
            int speed = GetSelectedSpeed();
            var entry = std.GetEntry(speed);
            if (entry == null) return;

            bool isStopping = rbStoppingSight.Checked;
            double stdSight = isStopping ? entry.StoppingSightDist : (entry.PassingSightDist > 0 ? entry.PassingSightDist : entry.StoppingSightDist);
            double stdTargetH = isStopping ? std.TargetHeightStopping : std.TargetHeightPassing;

            if (numReqSight != null)
            {
                decimal sightVal = (decimal)Math.Round(stdSight, 1);
                if (sightVal >= numReqSight.Minimum && sightVal <= numReqSight.Maximum)
                    numReqSight.Value = sightVal;
            }

            if (numTargetHeight != null)
            {
                decimal tgtVal = (decimal)stdTargetH;
                if (tgtVal >= numTargetHeight.Minimum && tgtVal <= numTargetHeight.Maximum)
                    numTargetHeight.Value = tgtVal;
            }
        }

        private void ApplyStandardLookup(bool updateControls)
        {
            if (cbbStandard.SelectedItem is not SightStandardDefinition std) return;

            int speed = GetSelectedSpeed();
            var entry = std.GetEntry(speed);
            if (entry == null) return;

            bool isStopping = rbStoppingSight.Checked;
            double stdSight = isStopping ? entry.StoppingSightDist : (entry.PassingSightDist > 0 ? entry.PassingSightDist : entry.StoppingSightDist);
            double stdTargetH = isStopping ? std.TargetHeightStopping : std.TargetHeightPassing;

            if (updateControls)
            {
                if (numReqSight != null)
                {
                    decimal sightVal = (decimal)Math.Round(stdSight, 1);
                    if (sightVal >= numReqSight.Minimum && sightVal <= numReqSight.Maximum)
                        numReqSight.Value = sightVal;
                }

                if (numEyeHeight != null)
                {
                    decimal eyeVal = (decimal)std.EyeHeight;
                    if (eyeVal >= numEyeHeight.Minimum && eyeVal <= numEyeHeight.Maximum)
                        numEyeHeight.Value = eyeVal;
                }

                if (numTargetHeight != null)
                {
                    decimal tgtVal = (decimal)stdTargetH;
                    if (tgtVal >= numTargetHeight.Minimum && tgtVal <= numTargetHeight.Maximum)
                        numTargetHeight.Value = tgtVal;
                }

                if (numLaneOffset != null)
                {
                    decimal laneVal = (decimal)entry.DefaultLaneOffset;
                    if (laneVal >= numLaneOffset.Minimum && laneVal <= numLaneOffset.Maximum)
                        numLaneOffset.Value = laneVal;
                }

                if (numInterval != null)
                {
                    decimal intervalVal = (entry.Speed >= 80) ? 20m : ((entry.Speed >= 40) ? 10m : 5m);
                    if (intervalVal >= numInterval.Minimum && intervalVal <= numInterval.Maximum)
                        numInterval.Value = intervalVal;
                }
            }

            // Luôn cập nhật thẻ tóm tắt tra cứu đầy đủ, rõ ràng
            if (lblStdSummaryTitle != null && lblStdSight != null && lblStdVertical != null && lblStdHorizontal != null)
            {
                bool isExactMatch = std.Entries.Any(e => e.Speed == speed);
                string tagStr = isExactMatch ? "Chính xác theo bảng" : "Nội suy tuyến tính";
                lblStdSummaryTitle.Text = $"💡 Thông số tra chuẩn {std.Name} (V = {speed} km/h - {tagStr}):";
                string passStr = entry.PassingSightDist > 0 ? $"{entry.PassingSightDist:F0} m" : "Không áp dụng";

                lblStdSight.Text = $"• Tầm nhìn: S_dừng ≥ {entry.StoppingSightDist:F0} m | S_vượt ≥ {passStr} | B_làn = {entry.LaneWidth:F2} m (Offset tim = {entry.DefaultLaneOffset:F2} m)";
                lblStdVertical.Text = $"• Cong đứng: R_lồi ≥ {entry.MinConvexRadiusLimit:F0} m (tt {entry.MinConvexRadius:F0} m) | R_lõm ≥ {entry.MinConcaveRadiusLimit:F0} m (tt {entry.MinConcaveRadius:F0} m)";
                lblStdHorizontal.Text = $"• Cong nằm & Dốc: R_nằm min ≥ {entry.MinHorizontalRadius:F0} m (tt {entry.MinHorizontalRadiusNormal:F0} m) | i_max ≤ {entry.MaxGrade:F1}% | f = {entry.FrictionFactor:F2}";
            }
        }

        #endregion

        #region Polyline Interaction & Conversion

        private void UpdatePolylineDisplayInfo(ObjectId polyId)
        {
            if (polyId.IsNull || !polyId.IsValid)
            {
                txtPolylineInfo.Text = "(Chưa chọn Polyline)";
                return;
            }

            try
            {
                using (var tr = A.Db.TransactionManager.StartTransaction())
                {
                    var ent = tr.GetObject(polyId, OpenMode.ForRead) as Entity;
                    if (ent is Polyline pl)
                    {
                        txtPolylineInfo.Text = $"2D Polyline (Dài {pl.Length:F1}m, {pl.NumberOfVertices} đỉnh, Layer: {pl.Layer})";
                    }
                    else if (ent is Polyline3d p3d)
                    {
                        double len = p3d.Length;
                        txtPolylineInfo.Text = $"3D Polyline (Dài {len:F1}m, Layer: {p3d.Layer})";
                    }
                    tr.Commit();
                }
            }
            catch { }

            UpdatePolylineStationDisplay();
        }

        private void UpdatePolylineStationDisplay()
        {
            if (_selectedPolylineId.IsNull || !_selectedPolylineId.IsValid)
            {
                txtStartStation.Text = "Km0+000.00";
                txtEndStation.Text = "Km0+000.00";
                return;
            }

            try
            {
                using (var tr = A.Db.TransactionManager.StartTransaction())
                {
                    var curve = tr.GetObject(_selectedPolylineId, OpenMode.ForRead) as Curve;
                    if (curve != null)
                    {
                        double totalLength = curve.GetDistanceAtParameter(curve.EndParam);
                        txtStartStation.Text = "Km0+000.00";
                        txtEndStation.Text = $"Km{Math.Floor(totalLength / 1000):0}+{totalLength % 1000:000.00}";
                    }
                    tr.Commit();
                }
            }
            catch { }
        }

        private void BtnPickPolyline_Click(object sender, EventArgs e)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            using (ed.StartUserInteraction(this))
            {
                PromptEntityOptions peo = new PromptEntityOptions("\nChọn đường Polyline 2D hoặc 3D Polyline trên bản vẽ: ");
                peo.SetRejectMessage("\nChỉ được chọn Polyline hoặc Polyline3d!");
                peo.AddAllowedClass(typeof(Polyline), true);
                peo.AddAllowedClass(typeof(Polyline3d), true);

                PromptEntityResult per = ed.GetEntity(peo);
                if (per.Status == PromptStatus.OK)
                {
                    _selectedPolylineId = per.ObjectId;
                    _lastPolylineId = _selectedPolylineId;
                    UpdatePolylineDisplayInfo(_selectedPolylineId);
                    UpdatePolylineStationDisplay();
                    SaveCurrentSettings();
                }
            }
        }

        private void BtnConvertTo3D_Click(object sender, EventArgs e)
        {
            if (_selectedPolylineId.IsNull || !_selectedPolylineId.IsValid)
            {
                MessageBox.Show("Vui lòng Pick chọn đường Polyline trước!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            CivSurface? surface = null;
            if (cbbSurface.SelectedItem is ObjectIdItem sItem && !sItem.Id.IsNull)
            {
                try
                {
                    using (var tr = A.Db.TransactionManager.StartTransaction())
                    {
                        surface = tr.GetObject(sItem.Id, OpenMode.ForRead) as CivSurface;
                        tr.Commit();
                    }
                }
                catch { }
            }

            try
            {
                ObjectId newPoly3dId = ObjectId.Null;
                using (var tr = A.Db.TransactionManager.StartTransaction())
                {
                    var ent = tr.GetObject(_selectedPolylineId, OpenMode.ForRead);
                    if (ent is Polyline pline)
                    {
                        newPoly3dId = SightDistanceEngine.ConvertPolylineToPolyline3d(pline, surface, 5.0);
                    }
                    else if (ent is Polyline3d)
                    {
                        MessageBox.Show("Đường đã chọn vốn là 3D Polyline!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    tr.Commit();
                }

                if (!newPoly3dId.IsNull)
                {
                    _selectedPolylineId = newPoly3dId;
                    _lastPolylineId = _selectedPolylineId;
                    UpdatePolylineDisplayInfo(_selectedPolylineId);
                    UpdatePolylineStationDisplay();
                    SaveCurrentSettings();
                    MessageBox.Show("Đã chuyển đổi thành công sang 3D Polyline và nạp vào kiểm tra!\nLayer: C3D_TIM_TUYEN_3D_POLYLINE", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Lỗi chuyển đổi Polyline 3D: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        #region Canvas Pick Interaction

        private void BtnPickAlignment_Click(object sender, EventArgs e)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            using (ed.StartUserInteraction(this))
            {
                PromptEntityOptions peo = new PromptEntityOptions("\nChọn tim tuyến (Alignment) trên bản vẽ: ");
                peo.SetRejectMessage("\nChỉ được chọn Alignment!");
                peo.AddAllowedClass(typeof(Alignment), true);

                PromptEntityResult per = ed.GetEntity(peo);
                if (per.Status == PromptStatus.OK)
                {
                    ObjectId selId = per.ObjectId;
                    _lastAlignmentId = selId;
                    for (int i = 0; i < cbbAlignment.Items.Count; i++)
                    {
                        if (((ObjectIdItem)cbbAlignment.Items[i]).Id == selId)
                        {
                            cbbAlignment.SelectedIndex = i;
                            break;
                        }
                    }
                    SaveCurrentSettings();
                }
            }
        }

        private void BtnPickSurface_Click(object sender, EventArgs e)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            using (ed.StartUserInteraction(this))
            {
                PromptEntityOptions peo = new PromptEntityOptions("\nChọn Bề mặt địa hình (TIN Surface) trên bản vẽ: ");
                peo.SetRejectMessage("\nChỉ được chọn TinSurface!");
                peo.AddAllowedClass(typeof(CivSurface), true);

                PromptEntityResult per = ed.GetEntity(peo);
                if (per.Status == PromptStatus.OK)
                {
                    ObjectId selId = per.ObjectId;
                    _lastSurfaceId = selId;
                    for (int i = 0; i < cbbSurface.Items.Count; i++)
                    {
                        if (((ObjectIdItem)cbbSurface.Items[i]).Id == selId)
                        {
                            cbbSurface.SelectedIndex = i;
                            break;
                        }
                    }
                    SaveCurrentSettings();
                }
            }
        }

        #endregion

        #region Analysis Execution & Filtering

        private void BtnAnalyze_Click(object sender, EventArgs e)
        {
            SaveCurrentSettings();
            if (cbbStandard.SelectedItem is not SightStandardDefinition std)
            {
                MessageBox.Show("Vui lòng chọn Tiêu chuẩn thiết kế!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int speed = GetSelectedSpeed();
            if (speed <= 0)
            {
                MessageBox.Show("Vui lòng nhập vận tốc thiết kế hợp lệ (> 0 km/h)!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            SightType sightType = rbStoppingSight.Checked ? SightType.Stopping : SightType.Passing;

            ObjectId surfId = (cbbSurface.SelectedItem is ObjectIdItem sItem) ? sItem.Id : ObjectId.Null;

            double reqSight = (numReqSight != null) ? (double)numReqSight.Value : 0.0;
            double interval = (double)numInterval.Value;
            double laneOffset = (double)numLaneOffset.Value;
            double eyeH = (double)numEyeHeight.Value;
            double targetH = (double)numTargetHeight.Value;

            bool checkGrade = chkCheckGrade.Checked;
            bool checkClearance = chkCheckClearance.Checked;
            bool check3D = chkCheck3DSurface.Checked;

            var doc = Application.DocumentManager.MdiActiveDocument;

            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                CivSurface? surface = null;
                if (!surfId.IsNull)
                {
                    try { surface = tr.GetObject(surfId, OpenMode.ForRead) as CivSurface; } catch { }
                }

                // 1. Phân tích theo Alignment
                if (rbSourceAlignment.Checked)
                {
                    if (cbbAlignment.SelectedItem is not ObjectIdItem alignItem || alignItem.Id.IsNull)
                    {
                        MessageBox.Show("Vui lòng chọn Alignment cần kiểm tra!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    var align = tr.GetObject(alignItem.Id, OpenMode.ForRead) as Alignment;
                    if (align == null)
                    {
                        MessageBox.Show("Không thể mở Alignment!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    ObjectId profId = (cbbProfile.SelectedItem is ObjectIdItem pItem) ? pItem.Id : ObjectId.Null;
                    Profile? profile = null;
                    if (!profId.IsNull)
                    {
                        try { profile = tr.GetObject(profId, OpenMode.ForRead) as Profile; } catch { }
                    }

                    _analysisResults = SightDistanceEngine.AnalyzeSightDistance(
                        align,
                        profile,
                        surface,
                        std,
                        speed,
                        sightType,
                        align.StartingStation,
                        align.EndingStation,
                        interval,
                        laneOffset,
                        eyeH,
                        targetH,
                        checkGrade,
                        checkClearance,
                        check3D,
                        reqSight
                    );
                }
                // 2. Phân tích theo Polyline / 3D Polyline
                else
                {
                    if (_selectedPolylineId.IsNull || !_selectedPolylineId.IsValid)
                    {
                        MessageBox.Show("Vui lòng Pick chọn đường Polyline cần kiểm tra!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    var ent = tr.GetObject(_selectedPolylineId, OpenMode.ForRead);
                    Curve? curve = ent as Curve;

                    // Nếu là Polyline 2D và có surface mà chưa chuyển 3D, tự động chuyển 3D Polyline để có trắc dọc
                    if (ent is Polyline pline && surface != null)
                    {
                        ObjectId poly3dId = SightDistanceEngine.ConvertPolylineToPolyline3d(pline, surface, 5.0);
                        if (!poly3dId.IsNull)
                        {
                            curve = tr.GetObject(poly3dId, OpenMode.ForRead) as Curve;
                            _selectedPolylineId = poly3dId;
                            UpdatePolylineDisplayInfo(_selectedPolylineId);
                        }
                    }

                    if (curve == null)
                    {
                        MessageBox.Show("Không thể đọc đường hình học của Polyline!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    double totalLength = curve.GetDistanceAtParameter(curve.EndParam);

                    _analysisResults = SightDistanceEngine.AnalyzeSightDistanceForCurve(
                        curve,
                        surface,
                        std,
                        speed,
                        sightType,
                        0.0,
                        totalLength,
                        interval,
                        laneOffset,
                        eyeH,
                        targetH,
                        checkGrade,
                        checkClearance,
                        check3D,
                        reqSight
                    );
                }

                tr.Commit();
            }

            FilterAndDisplayResults();
        }

        private void FilterAndDisplayResults()
        {
            dgvResults.Rows.Clear();

            if (_analysisResults == null || _analysisResults.Count == 0)
            {
                lblSummary.Text = "Không có dữ liệu phân tích.";
                return;
            }

            int passCount = 0;
            int warnCount = 0;
            int failCount = 0;

            int rowStt = 1;
            foreach (var item in _analysisResults)
            {
                if (item.Status == SightCheckStatus.Pass) passCount++;
                else if (item.Status == SightCheckStatus.Warning) warnCount++;
                else if (item.Status == SightCheckStatus.Fail) failCount++;

                if (chkFilterOnlyErrors.Checked && item.Status == SightCheckStatus.Pass)
                    continue;

                string statusText = (item.Status == SightCheckStatus.Pass) ? "Đạt" : (item.Status == SightCheckStatus.Warning ? "Cảnh báo" : "Không đạt");
                string rStr = item.HorizontalRadius > 0 ? $"R_nam={item.HorizontalRadius:F0}" : (item.VerticalRadius > 0 ? $"R_dung={item.VerticalRadius:F0}" : "-");
                string clearStr = item.ClearanceWidthM > 0 ? $"{item.ClearanceWidthM:F2}" : "-";

                int rowIdx = dgvResults.Rows.Add(
                    rowStt++,
                    statusText,
                    item.StationFormatted,
                    $"{item.GradePercent:F2}%",
                    rStr,
                    $"{item.RequiredSight:F1}",
                    $"{item.ActualSight:F1}",
                    clearStr,
                    item.Detail
                );

                var row = dgvResults.Rows[rowIdx];
                row.Tag = item;

                if (item.Status == SightCheckStatus.Pass)
                {
                    row.Cells["colStatus"].Style.ForeColor = WinFormsColor.DarkGreen;
                    row.Cells["colStatus"].Style.Font = new WinFormsFont("Segoe UI", 9.0f, FontStyle.Bold);
                }
                else if (item.Status == SightCheckStatus.Warning)
                {
                    row.Cells["colStatus"].Style.ForeColor = WinFormsColor.DarkOrange;
                    row.Cells["colStatus"].Style.Font = new WinFormsFont("Segoe UI", 9.0f, FontStyle.Bold);
                    row.DefaultCellStyle.BackColor = WinFormsColor.FromArgb(255, 252, 240);
                }
                else
                {
                    row.Cells["colStatus"].Style.ForeColor = WinFormsColor.Crimson;
                    row.Cells["colStatus"].Style.Font = new WinFormsFont("Segoe UI", 9.0f, FontStyle.Bold);
                    row.DefaultCellStyle.BackColor = WinFormsColor.FromArgb(255, 240, 240);
                }
            }

            lblSummary.Text = $"Tổng số: {_analysisResults.Count} vị trí | Đạt: {passCount} | Cảnh báo: {warnCount} | Không đạt: {failCount}";
            if (failCount > 0) lblSummary.ForeColor = WinFormsColor.Crimson;
            else if (warnCount > 0) lblSummary.ForeColor = WinFormsColor.DarkOrange;
            else lblSummary.ForeColor = WinFormsColor.DarkGreen;
        }

        #endregion

        #region CAD Zoom & Draw

        private void ZoomToStation(double x, double y)
        {
            try
            {
                var ed = Application.DocumentManager.MdiActiveDocument.Editor;
                using (ViewTableRecord view = ed.GetCurrentView())
                {
                    view.CenterPoint = new Point2d(x, y);
                    view.Height = 80.0;
                    view.Width = 120.0;
                    ed.SetCurrentView(view);
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Lỗi Zoom CAD: {ex.Message}");
            }
        }

        private void DgvResults_DoubleClick(object sender, EventArgs e)
        {
            if (dgvResults.CurrentRow?.Tag is SightCheckItem item)
            {
                ZoomToStation(item.X, item.Y);
            }
        }

        private void BtnZoomCAD_Click(object sender, EventArgs e)
        {
            if (dgvResults.CurrentRow?.Tag is SightCheckItem item)
            {
                ZoomToStation(item.X, item.Y);
            }
            else
            {
                MessageBox.Show("Vui lòng chọn một dòng trong bảng kết quả để Zoom!");
            }
        }

        private void BtnDrawRays_Click(object sender, EventArgs e)
        {
            SaveCurrentSettings();
            if (_analysisResults == null || _analysisResults.Count == 0)
            {
                MessageBox.Show("Chưa có dữ liệu phân tích. Hãy nhấn 'Phân Tích & Kiểm Tra' trước!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                ObjectId alignId = ObjectId.Null;
                if (rbSourceAlignment.Checked && cbbAlignment.SelectedItem is ObjectIdItem aItem)
                {
                    alignId = aItem.Id;
                }

                double laneOffset = (numLaneOffset != null) ? (double)numLaneOffset.Value : 1.75;
                bool drawEyeTrajectory = (chkDrawEyePath != null) ? chkDrawEyePath.Checked : true;

                int count = SightDistanceEngine.DrawSightRaysToCAD(
                    _analysisResults,
                    chkFilterOnlyErrors.Checked,
                    drawEyeTrajectory: drawEyeTrajectory,
                    alignmentId: alignId,
                    polylineId: _selectedPolylineId,
                    laneOffset: laneOffset);

                string extraMsg = drawEyeTrajectory ? "\nĐã vẽ Polyline quỹ đạo mắt người lái xe (Layer: C3D_QUY_DAO_MAT_LAI_XE - Màu Cyan)" : "";
                MessageBox.Show($"Đã vẽ thành công {count} tia ngắm lên bản vẽ CAD!\nLayer: C3D_TAM_NHIN_DAT (Xanh) & C3D_TAM_NHIN_KHUAT (Đỏ){extraMsg}", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Lỗi vẽ tia ngắm: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnDrawMarks_Click(object sender, EventArgs e)
        {
            SaveCurrentSettings();
            if (_analysisResults == null || _analysisResults.Count == 0)
            {
                MessageBox.Show("Chưa có dữ liệu phân tích!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var failOrWarn = _analysisResults.Where(i => i.Status != SightCheckStatus.Pass).ToList();
            if (failOrWarn.Count == 0)
            {
                MessageBox.Show("Tất cả vị trí đều Đạt yêu cầu! Không có lỗi để đánh dấu.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                var db = doc.Database;
                int count = 0;

                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                    string layerMark = "C3D_TAM_NHIN_CANH_BAO";
                    SightDistanceEngine.GetOrCreateLayer(db, tr, layerMark, 1);

                    foreach (var item in failOrWarn)
                    {
                        short colorIdx = (item.Status == SightCheckStatus.Fail) ? (short)1 : (short)2; // 1 = Đỏ, 2 = Vàng

                        var circle = new Circle
                        {
                            Center = new Point3d(item.X, item.Y, 0),
                            Radius = 15.0,
                            Layer = layerMark,
                            ColorIndex = colorIdx
                        };
                        btr.AppendEntity(circle);
                        tr.AddNewlyCreatedDBObject(circle, true);

                        var l1 = new Line(new Point3d(item.X - 20, item.Y, 0), new Point3d(item.X + 20, item.Y, 0)) { Layer = layerMark, ColorIndex = colorIdx };
                        var l2 = new Line(new Point3d(item.X, item.Y - 20, 0), new Point3d(item.X, item.Y + 20, 0)) { Layer = layerMark, ColorIndex = colorIdx };
                        btr.AppendEntity(l1);
                        btr.AppendEntity(l2);
                        tr.AddNewlyCreatedDBObject(l1, true);
                        tr.AddNewlyCreatedDBObject(l2, true);

                        var txt = new DBText
                        {
                            Position = new Point3d(item.X + 18, item.Y + 18, 0),
                            Height = 5.0,
                            TextString = $"[{item.StationFormatted}] {item.Detail}",
                            Layer = layerMark,
                            ColorIndex = colorIdx
                        };
                        btr.AppendEntity(txt);
                        tr.AddNewlyCreatedDBObject(txt, true);

                        count++;
                    }

                    tr.Commit();
                }

                MessageBox.Show($"Đã đánh dấu {count} vị trí cảnh báo / không đạt lên mặt bằng tuyến CAD!\nLayer: C3D_TAM_NHIN_CANH_BAO", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Lỗi đánh dấu CAD: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnExportExcel_Click(object sender, EventArgs e)
        {
            SaveCurrentSettings();
            if (_analysisResults == null || _analysisResults.Count == 0)
            {
                MessageBox.Show("Chưa có kết quả để xuất Excel!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var sfd = new SaveFileDialog())
            {
                string sourceName = rbSourceAlignment.Checked ? cbbAlignment.Text : "Polyline";
                sfd.Filter = "Excel Workbook (*.xlsx)|*.xlsx";
                sfd.FileName = $"BaoCao_TamNhin_{sourceName}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        string stdName = cbbStandard.Text;
                        int speed = GetSelectedSpeed();
                        string sightName = rbStoppingSight.Checked ? "Tầm nhìn dừng xe (SSD)" : "Tầm nhìn vượt xe (PSD)";

                        SightDistanceEngine.ExportToExcel(_analysisResults, sfd.FileName, sourceName, stdName, speed, sightName);

                        var res = MessageBox.Show("Xuất file Excel thành công! Bạn có muốn mở file ngay bây giờ không?", "Thành công", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                        if (res == DialogResult.Yes)
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(sfd.FileName) { UseShellExecute = true });
                        }
                    }
                    catch (System.Exception ex)
                    {
                        MessageBox.Show($"Lỗi xuất Excel: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        #endregion

        #region Persistent State Implementation

        private void SaveCurrentSettings()
        {
            try
            {
                _lastSourceMode = rbSourceAlignment.Checked ? 0 : 1;

                if (cbbAlignment.SelectedItem is ObjectIdItem aItem)
                    _lastAlignmentId = aItem.Id;

                if (cbbProfile.SelectedItem is ObjectIdItem pItem)
                    _lastProfileId = pItem.Id;

                if (cbbSurface.SelectedItem is ObjectIdItem sItem)
                    _lastSurfaceId = sItem.Id;

                _lastPolylineId = _selectedPolylineId;

                if (cbbStandard.SelectedItem is SightStandardDefinition std)
                    _lastStandardName = std.Name;

                _lastSpeed = GetSelectedSpeed();

                _lastSightTypeIndex = rbStoppingSight.Checked ? 0 : 1;
                if (numReqSight != null)
                    _lastReqSight = numReqSight.Value;
                if (numInterval != null)
                    _lastInterval = numInterval.Value;
                if (numLaneOffset != null)
                    _lastLaneOffset = numLaneOffset.Value;
                if (numEyeHeight != null)
                    _lastEyeHeight = numEyeHeight.Value;
                if (numTargetHeight != null)
                    _lastTargetHeight = numTargetHeight.Value;

                if (chkCheckGrade != null)
                    _lastCheckGrade = chkCheckGrade.Checked;
                if (chkCheckClearance != null)
                    _lastCheckClearance = chkCheckClearance.Checked;
                if (chkCheck3DSurface != null)
                    _lastCheck3D = chkCheck3DSurface.Checked;
                if (chkFilterOnlyErrors != null)
                    _lastFilterOnlyErrors = chkFilterOnlyErrors.Checked;
                if (chkDrawEyePath != null)
                    _lastDrawEyePath = chkDrawEyePath.Checked;

                _lastFormSize = this.Size;
            }
            catch { }
        }

        private void RestoreLastSettings()
        {
            try
            {
                // 1. Khôi phục Tiêu chuẩn thiết kế
                if (!string.IsNullOrEmpty(_lastStandardName))
                {
                    for (int i = 0; i < cbbStandard.Items.Count; i++)
                    {
                        if (cbbStandard.Items[i] is SightStandardDefinition s && s.Name == _lastStandardName)
                        {
                            cbbStandard.SelectedIndex = i;
                            break;
                        }
                    }
                }

                // 2. Nạp danh sách vận tốc của tiêu chuẩn đã chọn vào cbbSpeed
                if (cbbStandard.SelectedItem is SightStandardDefinition curStd)
                {
                    cbbSpeed.Items.Clear();
                    foreach (var entry in curStd.Entries)
                    {
                        cbbSpeed.Items.Add(entry.Speed);
                    }
                }

                // 3. Khôi phục Vận tốc đã chọn
                _isUpdatingSpeed = true;
                int spdIdx = cbbSpeed.Items.IndexOf(_lastSpeed);
                if (spdIdx >= 0) cbbSpeed.SelectedIndex = spdIdx;
                else cbbSpeed.Text = _lastSpeed.ToString();
                _isUpdatingSpeed = false;

                // 4. Khôi phục Nguồn đường kiểm tra (Alignment vs Polyline)
                if (_lastSourceMode == 0)
                {
                    rbSourceAlignment.Checked = true;
                    rbSourcePolyline.Checked = false;
                    pnlAlignmentControls.Visible = true;
                    pnlPolylineControls.Visible = false;
                }
                else
                {
                    rbSourceAlignment.Checked = false;
                    rbSourcePolyline.Checked = true;
                    pnlAlignmentControls.Visible = false;
                    pnlPolylineControls.Visible = true;
                }

                // 5. Khôi phục Loại tầm nhìn
                if (_lastSightTypeIndex == 0)
                {
                    rbStoppingSight.Checked = true;
                    rbPassingSight.Checked = false;
                }
                else
                {
                    rbStoppingSight.Checked = false;
                    rbPassingSight.Checked = true;
                }

                // 6. Khôi phục chính xác các thông số số học (không bị bảng tra ghi đè)
                if (numInterval != null && _lastInterval >= numInterval.Minimum && _lastInterval <= numInterval.Maximum)
                    numInterval.Value = _lastInterval;
                if (numLaneOffset != null && _lastLaneOffset >= numLaneOffset.Minimum && _lastLaneOffset <= numLaneOffset.Maximum)
                    numLaneOffset.Value = _lastLaneOffset;
                if (numEyeHeight != null && _lastEyeHeight >= numEyeHeight.Minimum && _lastEyeHeight <= numEyeHeight.Maximum)
                    numEyeHeight.Value = _lastEyeHeight;
                if (numTargetHeight != null && _lastTargetHeight >= numTargetHeight.Minimum && _lastTargetHeight <= numTargetHeight.Maximum)
                    numTargetHeight.Value = _lastTargetHeight;
                if (numReqSight != null && _lastReqSight >= numReqSight.Minimum && _lastReqSight <= numReqSight.Maximum)
                    numReqSight.Value = _lastReqSight;

                // 7. Khôi phục CheckBoxes
                if (chkCheckGrade != null) chkCheckGrade.Checked = _lastCheckGrade;
                if (chkCheckClearance != null) chkCheckClearance.Checked = _lastCheckClearance;
                if (chkCheck3DSurface != null) chkCheck3DSurface.Checked = _lastCheck3D;
                if (chkFilterOnlyErrors != null) chkFilterOnlyErrors.Checked = _lastFilterOnlyErrors;
                if (chkDrawEyePath != null) chkDrawEyePath.Checked = _lastDrawEyePath;

                // 8. Khôi phục Profile cho Alignment và hiển thị Lý trình
                if (cbbAlignment.SelectedItem is ObjectIdItem aItem && !aItem.Id.IsNull)
                {
                    LoadProfilesForAlignment(aItem.Id, _lastProfileId);
                }

                if (_lastSourceMode == 1)
                {
                    UpdatePolylineStationDisplay();
                }

                // 9. Khôi phục kích thước form nếu có
                if (_lastFormSize.Width >= 1160 && _lastFormSize.Height >= 740)
                {
                    this.Size = _lastFormSize;
                }
            }
            catch { }
        }

        #endregion
    }
}

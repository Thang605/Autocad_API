// (C) Copyright 2026 by T27
// Form tạo CogoPoint đa nguồn từ Text, Circle, Point, Table, Excel/CSV

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using ClosedXML.Excel;
using MyFirstProject.Extensions;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using ATable = Autodesk.AutoCAD.DatabaseServices.Table;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using DrawingFont = System.Drawing.Font;
using SysDataRow = System.Data.DataRow;
using SysDataTable = System.Data.DataTable;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsPoint = System.Drawing.Point;

namespace Civil3DCsharp
{
    public class TaoCogoPointForm : Form
    {
        #region Persistent State (Rule 2)
        private static Size _lastFormSize = new Size(760, 680);
        private static int _lastSelectedTab = 0;

        // General settings
        private static string _lastPrefix = "P-";
        private static decimal _lastStartNo = 1;
        private static decimal _lastStep = 1;
        private static string _lastSuffix = "";
        private static string _lastDefaultDesc = "TN";
        private static bool _lastAddToPointGroup = true;
        private static string _lastPointGroupName = "COGO_POINTS";
        private static string _lastPointStyle = "<default>";
        private static string _lastPointLabelStyle = "<default>";
        private static bool _lastIgnoreDuplicates = true;
        private static decimal _lastDuplicateTolerance = 0.005m;

        // Tab Text
        private static int _lastTextZMode = 0; // 0: Parse content, 1: Text.Z 3D, 2: Fixed Z, 3: Surface
        private static decimal _lastTextFixedZ = 0;
        private static string _lastTextSurface = "<Không chọn>";
        private static int _lastTextNameMode = 0; // 0: Auto STT, 1: From Text content
        private static int _lastTextDescMode = 0; // 0: Default desc, 1: From Layer
        private static bool _lastTextDeleteSource = false;
        private static string _lastTextLayer = "<Tất cả>";
        private static bool _lastTextFilterLayer = false;
        private static bool _lastTextForceLeftJustify = true;

        // Tab Circle
        private static int _lastCircleZMode = 0; // 0: Center.Z, 1: Nearby Text, 2: Surface, 3: Fixed Z
        private static decimal _lastCircleRadiusZ = 2.0m;
        private static string _lastCircleSurface = "<Không chọn>";
        private static decimal _lastCircleFixedZ = 0;
        private static int _lastCircleNameMode = 0; // 0: Auto STT, 1: Nearby Text
        private static decimal _lastCircleRadiusName = 2.0m;
        private static bool _lastCircleDeleteSource = false;
        private static string _lastCircleLayer = "<Tất cả>";
        private static bool _lastCircleFilterLayer = false;

        // Tab Point
        private static int _lastPointZMode = 0; // 0: Point.Z, 1: Nearby Text, 2: Surface, 3: Fixed Z
        private static decimal _lastPointRadiusZ = 2.0m;
        private static string _lastPointSurface = "<Không chọn>";
        private static decimal _lastPointFixedZ = 0;
        private static int _lastPointNameMode = 0; // 0: Auto STT, 1: Nearby Text
        private static decimal _lastPointRadiusName = 2.0m;
        private static bool _lastPointDeleteSource = false;
        private static string _lastPointLayer = "<Tất cả>";
        private static bool _lastPointFilterLayer = false;

        // Tab Table
        private static ObjectId _lastSelectedTableId = ObjectId.Null;
        private static decimal _lastTableStartRow = 2;
        private static int _lastTableColName = 0;
        private static int _lastTableColX = 1;
        private static int _lastTableColY = 2;
        private static int _lastTableColZ = 3;
        private static int _lastTableColDesc = 0;

        // Tab Excel
        private static string _lastExcelFilePath = "";
        private static string _lastExcelSheetName = "";
        private static decimal _lastExcelHeaderRow = 1;
        private static decimal _lastExcelStartRow = 2;
        private static int _lastExcelColName = 0;
        private static int _lastExcelColX = 1;
        private static int _lastExcelColY = 2;
        private static int _lastExcelColZ = 3;
        private static int _lastExcelColDesc = 0;
        #endregion

        #region UI Controls
        private TabControl tabSources = null!;
        private TabPage tabText = null!, tabCircle = null!, tabPoint = null!, tabTable = null!, tabExcel = null!;

        // Tab 1: Text
        private Button btnPickText = null!;
        private WinFormsLabel lblTextCount = null!;
        private CheckBox chkTextFilterLayer = null!;
        private ComboBox cmbTextLayer = null!;
        private RadioButton radTextZParse = null!, radTextZ3D = null!, radTextZFixed = null!, radTextZSurface = null!;
        private NumericUpDown numTextFixedZ = null!;
        private ComboBox cmbTextSurface = null!;
        private RadioButton radTextNameAuto = null!, radTextNameContent = null!;
        private RadioButton radTextDescDefault = null!, radTextDescLayer = null!;
        private CheckBox chkTextDeleteSource = null!;
        private CheckBox chkTextForceLeftJustify = null!;

        // Tab 2: Circle
        private Button btnPickCircle = null!;
        private WinFormsLabel lblCircleCount = null!;
        private CheckBox chkCircleFilterLayer = null!;
        private ComboBox cmbCircleLayer = null!;
        private RadioButton radCircleZCenter = null!, radCircleZNearby = null!, radCircleZSurface = null!, radCircleZFixed = null!;
        private NumericUpDown numCircleRadiusZ = null!, numCircleFixedZ = null!;
        private ComboBox cmbCircleSurface = null!;
        private RadioButton radCircleNameAuto = null!, radCircleNameNearby = null!;
        private NumericUpDown numCircleRadiusName = null!;
        private CheckBox chkCircleDeleteSource = null!;

        // Tab 3: Point
        private Button btnPickPoint = null!;
        private WinFormsLabel lblPointCount = null!;
        private CheckBox chkPointFilterLayer = null!;
        private ComboBox cmbPointLayer = null!;
        private RadioButton radPointZPoint = null!, radPointZNearby = null!, radPointZSurface = null!, radPointZFixed = null!;
        private NumericUpDown numPointRadiusZ = null!, numPointFixedZ = null!;
        private ComboBox cmbPointSurface = null!;
        private RadioButton radPointNameAuto = null!, radPointNameNearby = null!;
        private NumericUpDown numPointRadiusName = null!;
        private CheckBox chkPointDeleteSource = null!;

        // Tab 4: Table
        private Button btnPickTable = null!;
        private WinFormsLabel lblTableInfo = null!;
        private NumericUpDown numTableStartRow = null!;
        private ComboBox cmbTableColName = null!, cmbTableColX = null!, cmbTableColY = null!, cmbTableColZ = null!, cmbTableColDesc = null!;
        private DataGridView dgvTablePreview = null!;

        // Tab 5: Excel
        private TextBox txtExcelFilePath = null!;
        private Button btnBrowseExcel = null!;
        private ComboBox cmbExcelSheet = null!;
        private NumericUpDown numExcelHeaderRow = null!, numExcelStartRow = null!;
        private ComboBox cmbExcelColName = null!, cmbExcelColX = null!, cmbExcelColY = null!, cmbExcelColZ = null!, cmbExcelColDesc = null!;
        private DataGridView dgvExcelPreview = null!;

        // General settings
        private TextBox txtPrefix = null!, txtSuffix = null!, txtDefaultDesc = null!, txtPointGroupName = null!;
        private NumericUpDown numStartNo = null!, numStep = null!, numDuplicateTolerance = null!;
        private CheckBox chkAddToPointGroup = null!, chkIgnoreDuplicates = null!;
        private ComboBox cmbPointStyle = null!, cmbPointLabelStyle = null!;

        // Bottom Actions
        private Panel pnlBottom = null!;
        private Button btnExecute = null!;
        private Button btnClose = null!;
        private WinFormsLabel lblStatus = null!;
        #endregion

        #region Properties & Selection Data
        public int ActiveSourceTab => tabSources.SelectedIndex;
        public bool FormAccepted { get; private set; } = false;

        public List<ObjectId> SelectedTextIds { get; set; } = new List<ObjectId>();
        public List<ObjectId> SelectedCircleIds { get; set; } = new List<ObjectId>();
        public List<ObjectId> SelectedPointIds { get; set; } = new List<ObjectId>();
        public ObjectId SelectedTableId { get; set; } = ObjectId.Null;

        // Callback delegates for Canvas Interaction
        public Func<TaoCogoPointForm, List<ObjectId>>? OnPickTexts { get; set; }
        public Func<TaoCogoPointForm, List<ObjectId>>? OnPickCircles { get; set; }
        public Func<TaoCogoPointForm, List<ObjectId>>? OnPickPoints { get; set; }
        public Func<TaoCogoPointForm, ObjectId>? OnPickTable { get; set; }
        #endregion

        #region Constructor
        public TaoCogoPointForm(List<string> layers, List<string> surfaces, List<string> pointStyles, List<string> pointLabelStyles)
        {
            InitializeComponent();
            PopulateInitialData(layers, surfaces, pointStyles, pointLabelStyles);
            RestoreLastSettings();
            AttachEvents();
        }
        #endregion

        #region Initialization
        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.Text = "Tạo COGO Point Đa Nguồn (Text, Circle, Point, Table, Excel) - T27";
            this.Font = new DrawingFont("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.ClientSize = new Size(760, 660);
            this.MinimumSize = new Size(740, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.Sizable;

            // Main Layout: Split vertically into TabControl (fill top/middle), GroupBox General (bottom 140px), Bottom Buttons (bottom 58px)
            tabSources = new TabControl { Dock = DockStyle.Fill, Padding = new WinFormsPoint(12, 6) };

            tabText = new TabPage("1. Text / MText");
            tabCircle = new TabPage("2. Circle (Đường tròn)");
            tabPoint = new TabPage("3. Point (CAD Point)");
            tabTable = new TabPage("4. AutoCAD Table");
            tabExcel = new TabPage("5. Excel / CSV File");

            tabSources.TabPages.AddRange(new TabPage[] { tabText, tabCircle, tabPoint, tabTable, tabExcel });

            // Preset TabPage sizes before building controls so Anchor calculations are accurate
            Size tabInitialSize = new Size(740, 380);
            tabText.Size = tabInitialSize;
            tabCircle.Size = tabInitialSize;
            tabPoint.Size = tabInitialSize;
            tabTable.Size = tabInitialSize;
            tabExcel.Size = tabInitialSize;

            BuildTabText();
            BuildTabCircle();
            BuildTabPoint();
            BuildTabTable();
            BuildTabExcel();

            // General Settings GroupBox (Dock = Bottom, height 140)
            GroupBox grpGeneral = BuildGeneralSettingsGroup();
            grpGeneral.Dock = DockStyle.Bottom;
            grpGeneral.Height = 140;

            // Bottom Action Panel (Dock = Bottom, height 58)
            pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 58,
                BackColor = Color.FromArgb(240, 243, 248),
                Padding = new Padding(15, 8, 15, 8)
            };

            // Top divider line for pnlBottom
            pnlBottom.Paint += (s, pe) =>
            {
                pe.Graphics.DrawLine(new Pen(Color.FromArgb(215, 222, 230), 1), 0, 0, pnlBottom.Width, 0);
            };

            lblStatus = new WinFormsLabel
            {
                Text = "Sẵn sàng tạo COGO Point.",
                AutoSize = true,
                ForeColor = Color.FromArgb(0, 102, 204),
                Font = new DrawingFont("Segoe UI", 9.25F, FontStyle.Bold)
            };

            btnExecute = new Button
            {
                Text = "▶ TẠO COGO POINT",
                Size = new Size(240, 38),
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                Font = new DrawingFont("Segoe UI", 10F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnExecute.FlatAppearance.BorderSize = 0;

            btnClose = new Button
            {
                Text = "Đóng",
                Size = new Size(100, 38),
                Font = new DrawingFont("Segoe UI", 9.5F),
                Cursor = Cursors.Hand
            };

            pnlBottom.Controls.AddRange(new Control[] { lblStatus, btnExecute, btnClose });

            // Dynamic layout for bottom controls to guarantee visibility regardless of DPI or resizing
            pnlBottom.Resize += (s, e) => LayoutBottomPanel();

            // Controls docking hierarchy:
            this.Controls.Add(tabSources);
            this.Controls.Add(grpGeneral);
            this.Controls.Add(pnlBottom);

            tabSources.BringToFront();

            this.Load += (s, e) => LayoutBottomPanel();
            this.Shown += (s, e) => LayoutBottomPanel();
            this.Resize += (s, e) => LayoutBottomPanel();

            this.ResumeLayout(false);
        }

        private void LayoutBottomPanel()
        {
            if (pnlBottom == null || btnExecute == null || btnClose == null || lblStatus == null) return;
            int w = pnlBottom.ClientSize.Width;
            int h = pnlBottom.ClientSize.Height;

            btnClose.Size = new Size(100, 38);
            btnExecute.Size = new Size(240, 38);

            btnClose.Location = new WinFormsPoint(w - btnClose.Width - 16, (h - btnClose.Height) / 2);
            btnExecute.Location = new WinFormsPoint(btnClose.Left - btnExecute.Width - 12, (h - btnExecute.Height) / 2);
            lblStatus.Location = new WinFormsPoint(16, (h - lblStatus.Height) / 2);
        }

        private void BuildTabText()
        {
            tabText.Padding = new Padding(10);

            // Selection Group
            GroupBox grpSelect = new GroupBox { Text = "Đối tượng Text nguồn", Location = new WinFormsPoint(10, 10), Size = new Size(710, 60), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            btnPickText = new Button { Text = "🎯 Chọn Text trên bản vẽ", Location = new WinFormsPoint(15, 22), Size = new Size(180, 28) };
            lblTextCount = new WinFormsLabel { Text = "Đã chọn: 0 Text / MText", Location = new WinFormsPoint(205, 27), AutoSize = true, ForeColor = Color.DarkGreen, Font = new DrawingFont("Segoe UI", 9F, FontStyle.Bold) };
            chkTextFilterLayer = new CheckBox { Text = "Lọc theo Layer:", Location = new WinFormsPoint(380, 26), AutoSize = true };
            cmbTextLayer = new ComboBox { Location = new WinFormsPoint(495, 24), Size = new Size(195, 24), DropDownStyle = ComboBoxStyle.DropDownList, Enabled = false };
            grpSelect.Controls.AddRange(new Control[] { btnPickText, lblTextCount, chkTextFilterLayer, cmbTextLayer });

            // Elevation Z Group
            GroupBox grpZ = new GroupBox { Text = "Xác định Cao độ Z", Location = new WinFormsPoint(10, 75), Size = new Size(350, 175) };
            radTextZParse = new RadioButton { Text = "Nội dung Text (Số thực: '12.35' -> Z = 12.35)", Location = new WinFormsPoint(15, 25), Size = new Size(325, 24), Checked = true };
            radTextZ3D = new RadioButton { Text = "Tọa độ Z 3D của Text (Text.Position.Z)", Location = new WinFormsPoint(15, 55), Size = new Size(325, 24) };
            radTextZFixed = new RadioButton { Text = "Cao độ cố định:", Location = new WinFormsPoint(15, 85), Size = new Size(130, 24) };
            numTextFixedZ = new NumericUpDown { Location = new WinFormsPoint(150, 86), Size = new Size(100, 23), DecimalPlaces = 3, Minimum = -10000, Maximum = 10000, Value = 0, Enabled = false };
            radTextZSurface = new RadioButton { Text = "Lấy từ Surface:", Location = new WinFormsPoint(15, 118), Size = new Size(130, 24) };
            cmbTextSurface = new ComboBox { Location = new WinFormsPoint(150, 118), Size = new Size(185, 24), DropDownStyle = ComboBoxStyle.DropDownList, Enabled = false };
            grpZ.Controls.AddRange(new Control[] { radTextZParse, radTextZ3D, radTextZFixed, numTextFixedZ, radTextZSurface, cmbTextSurface });

            // Point Name & Description Group
            GroupBox grpProps = new GroupBox { Text = "Tên điểm & Mô tả", Location = new WinFormsPoint(370, 75), Size = new Size(350, 175), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            WinFormsLabel lblName = new WinFormsLabel { Text = "Tên điểm:", Location = new WinFormsPoint(15, 22), AutoSize = true, Font = new DrawingFont("Segoe UI", 9F, FontStyle.Bold) };
            radTextNameAuto = new RadioButton { Text = "Tự động theo STT (Prefix + Số)", Location = new WinFormsPoint(15, 42), Size = new Size(320, 22), Checked = true };
            radTextNameContent = new RadioButton { Text = "Lấy từ nội dung Text (khi Z=3D/Surface)", Location = new WinFormsPoint(15, 66), Size = new Size(320, 22) };

            WinFormsLabel lblDesc = new WinFormsLabel { Text = "Mô tả (Raw Description):", Location = new WinFormsPoint(15, 96), AutoSize = true, Font = new DrawingFont("Segoe UI", 9F, FontStyle.Bold) };
            radTextDescDefault = new RadioButton { Text = "Theo mô tả mặc định", Location = new WinFormsPoint(15, 116), Size = new Size(320, 22), Checked = true };
            radTextDescLayer = new RadioButton { Text = "Lấy theo tên Layer của Text", Location = new WinFormsPoint(15, 140), Size = new Size(320, 22) };
            grpProps.Controls.AddRange(new Control[] { lblName, radTextNameAuto, radTextNameContent, lblDesc, radTextDescDefault, radTextDescLayer });

            // Option delete, Left Justify & Direct Action Button
            chkTextDeleteSource = new CheckBox { Text = "🗑️ Xóa đối tượng Text nguồn sau khi tạo thành công", Location = new WinFormsPoint(15, 258), AutoSize = true, ForeColor = Color.DarkRed };
            chkTextForceLeftJustify = new CheckBox
            {
                Text = "📐 Tự động chuyển Text về căn trái (Justify = Left) để lấy tọa độ chuẩn",
                Location = new WinFormsPoint(15, 285),
                AutoSize = true,
                Checked = true,
                ForeColor = Color.FromArgb(0, 102, 204),
                Font = new DrawingFont("Segoe UI", 9F, FontStyle.Bold)
            };

            Button btnRunText = new Button
            {
                Text = "▶ Tạo COGO Point từ Text",
                Location = new WinFormsPoint(440, 256),
                Size = new Size(280, 52),
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                Font = new DrawingFont("Segoe UI", 10F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnRunText.FlatAppearance.BorderSize = 0;
            btnRunText.Click += BtnExecute_Click;

            tabText.Controls.AddRange(new Control[] { grpSelect, grpZ, grpProps, chkTextDeleteSource, chkTextForceLeftJustify, btnRunText });
        }

        private void BuildTabCircle()
        {
            tabCircle.Padding = new Padding(10);

            // Selection Group
            GroupBox grpSelect = new GroupBox { Text = "Đối tượng Circle nguồn", Location = new WinFormsPoint(10, 10), Size = new Size(710, 60), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            btnPickCircle = new Button { Text = "🎯 Chọn Circle trên bản vẽ", Location = new WinFormsPoint(15, 22), Size = new Size(180, 28) };
            lblCircleCount = new WinFormsLabel { Text = "Đã chọn: 0 Circle", Location = new WinFormsPoint(205, 27), AutoSize = true, ForeColor = Color.DarkGreen, Font = new DrawingFont("Segoe UI", 9F, FontStyle.Bold) };
            chkCircleFilterLayer = new CheckBox { Text = "Lọc theo Layer:", Location = new WinFormsPoint(380, 26), AutoSize = true };
            cmbCircleLayer = new ComboBox { Location = new WinFormsPoint(495, 24), Size = new Size(195, 24), DropDownStyle = ComboBoxStyle.DropDownList, Enabled = false };
            grpSelect.Controls.AddRange(new Control[] { btnPickCircle, lblCircleCount, chkCircleFilterLayer, cmbCircleLayer });

            // Elevation Z Group
            GroupBox grpZ = new GroupBox { Text = "Xác định Cao độ Z", Location = new WinFormsPoint(10, 75), Size = new Size(350, 175) };
            radCircleZCenter = new RadioButton { Text = "Tọa độ Z tâm Circle (Center.Z)", Location = new WinFormsPoint(15, 25), Size = new Size(325, 24), Checked = true };
            radCircleZNearby = new RadioButton { Text = "Tìm Text cao độ gần nhất trong R =", Location = new WinFormsPoint(15, 55), Size = new Size(220, 24) };
            numCircleRadiusZ = new NumericUpDown { Location = new WinFormsPoint(240, 56), Size = new Size(60, 23), DecimalPlaces = 1, Minimum = 0.1m, Maximum = 100m, Value = 2.0m, Increment = 0.5m, Enabled = false };
            radCircleZFixed = new RadioButton { Text = "Cao độ cố định:", Location = new WinFormsPoint(15, 85), Size = new Size(130, 24) };
            numCircleFixedZ = new NumericUpDown { Location = new WinFormsPoint(150, 86), Size = new Size(100, 23), DecimalPlaces = 3, Minimum = -10000, Maximum = 10000, Value = 0, Enabled = false };
            radCircleZSurface = new RadioButton { Text = "Lấy từ Surface:", Location = new WinFormsPoint(15, 118), Size = new Size(130, 24) };
            cmbCircleSurface = new ComboBox { Location = new WinFormsPoint(150, 118), Size = new Size(185, 24), DropDownStyle = ComboBoxStyle.DropDownList, Enabled = false };
            grpZ.Controls.AddRange(new Control[] { radCircleZCenter, radCircleZNearby, numCircleRadiusZ, radCircleZFixed, numCircleFixedZ, radCircleZSurface, cmbCircleSurface });

            // Point Name Group
            GroupBox grpProps = new GroupBox { Text = "Tên điểm & Tìm Text tên lân cận", Location = new WinFormsPoint(370, 75), Size = new Size(350, 175), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            radCircleNameAuto = new RadioButton { Text = "Tự động theo STT (Prefix + Số thứ tự)", Location = new WinFormsPoint(15, 28), Size = new Size(320, 24), Checked = true };
            radCircleNameNearby = new RadioButton { Text = "Tìm Text tên điểm gần nhất trong R =", Location = new WinFormsPoint(15, 65), Size = new Size(225, 24) };
            numCircleRadiusName = new NumericUpDown { Location = new WinFormsPoint(245, 66), Size = new Size(60, 23), DecimalPlaces = 1, Minimum = 0.1m, Maximum = 100m, Value = 2.0m, Increment = 0.5m, Enabled = false };
            WinFormsLabel lblUnit2 = new WinFormsLabel { Text = "m", Location = new WinFormsPoint(310, 68), AutoSize = true };
            WinFormsLabel lblNote = new WinFormsLabel { Text = "💡 Thuật toán tự động ghép cặp tâm Circle với Text cao độ/tên điểm lân cận trong bán kính tìm kiếm.", Location = new WinFormsPoint(15, 105), Size = new Size(320, 55), ForeColor = Color.DimGray };
            grpProps.Controls.AddRange(new Control[] { radCircleNameAuto, radCircleNameNearby, numCircleRadiusName, lblUnit2, lblNote });

            // Option delete & Direct Action Button
            chkCircleDeleteSource = new CheckBox { Text = "🗑️ Xóa đối tượng Circle nguồn sau khi tạo thành công", Location = new WinFormsPoint(15, 260), AutoSize = true, ForeColor = Color.DarkRed };
            Button btnRunCircle = new Button
            {
                Text = "▶ Tạo COGO Point từ Circle",
                Location = new WinFormsPoint(440, 256),
                Size = new Size(280, 32),
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                Font = new DrawingFont("Segoe UI", 9.5F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnRunCircle.FlatAppearance.BorderSize = 0;
            btnRunCircle.Click += BtnExecute_Click;

            tabCircle.Controls.AddRange(new Control[] { grpSelect, grpZ, grpProps, chkCircleDeleteSource, btnRunCircle });
        }

        private void BuildTabPoint()
        {
            tabPoint.Padding = new Padding(10);

            // Selection Group
            GroupBox grpSelect = new GroupBox { Text = "Đối tượng Point (CAD Point) nguồn", Location = new WinFormsPoint(10, 10), Size = new Size(710, 60), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            btnPickPoint = new Button { Text = "🎯 Chọn Point trên bản vẽ", Location = new WinFormsPoint(15, 22), Size = new Size(180, 28) };
            lblPointCount = new WinFormsLabel { Text = "Đã chọn: 0 Point", Location = new WinFormsPoint(205, 27), AutoSize = true, ForeColor = Color.DarkGreen, Font = new DrawingFont("Segoe UI", 9F, FontStyle.Bold) };
            chkPointFilterLayer = new CheckBox { Text = "Lọc theo Layer:", Location = new WinFormsPoint(380, 26), AutoSize = true };
            cmbPointLayer = new ComboBox { Location = new WinFormsPoint(495, 24), Size = new Size(195, 24), DropDownStyle = ComboBoxStyle.DropDownList, Enabled = false };
            grpSelect.Controls.AddRange(new Control[] { btnPickPoint, lblPointCount, chkPointFilterLayer, cmbPointLayer });

            // Elevation Z Group
            GroupBox grpZ = new GroupBox { Text = "Xác định Cao độ Z", Location = new WinFormsPoint(10, 75), Size = new Size(350, 175) };
            radPointZPoint = new RadioButton { Text = "Tọa độ Z của Point (Point.Position.Z)", Location = new WinFormsPoint(15, 25), Size = new Size(325, 24), Checked = true };
            radPointZNearby = new RadioButton { Text = "Tìm Text cao độ gần nhất trong R =", Location = new WinFormsPoint(15, 55), Size = new Size(220, 24) };
            numPointRadiusZ = new NumericUpDown { Location = new WinFormsPoint(240, 56), Size = new Size(60, 23), DecimalPlaces = 1, Minimum = 0.1m, Maximum = 100m, Value = 2.0m, Increment = 0.5m, Enabled = false };
            WinFormsLabel lblUnit1 = new WinFormsLabel { Text = "m", Location = new WinFormsPoint(305, 58), AutoSize = true };
            radPointZSurface = new RadioButton { Text = "Lấy từ Surface:", Location = new WinFormsPoint(15, 88), Size = new Size(120, 24) };
            cmbPointSurface = new ComboBox { Location = new WinFormsPoint(140, 88), Size = new Size(195, 24), DropDownStyle = ComboBoxStyle.DropDownList, Enabled = false };
            radPointZFixed = new RadioButton { Text = "Cao độ cố định:", Location = new WinFormsPoint(15, 120), Size = new Size(120, 24) };
            numPointFixedZ = new NumericUpDown { Location = new WinFormsPoint(140, 121), Size = new Size(100, 23), DecimalPlaces = 3, Minimum = -10000, Maximum = 10000, Value = 0, Enabled = false };
            grpZ.Controls.AddRange(new Control[] { radPointZPoint, radPointZNearby, numPointRadiusZ, lblUnit1, radPointZSurface, cmbPointSurface, radPointZFixed, numPointFixedZ });

            // Point Name Group
            GroupBox grpProps = new GroupBox { Text = "Tên điểm & Tìm Text tên lân cận", Location = new WinFormsPoint(370, 75), Size = new Size(350, 175), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            radPointNameAuto = new RadioButton { Text = "Tự động theo STT (Prefix + Số thứ tự)", Location = new WinFormsPoint(15, 28), Size = new Size(320, 24), Checked = true };
            radPointNameNearby = new RadioButton { Text = "Tìm Text tên điểm gần nhất trong R =", Location = new WinFormsPoint(15, 65), Size = new Size(225, 24) };
            numPointRadiusName = new NumericUpDown { Location = new WinFormsPoint(245, 66), Size = new Size(60, 23), DecimalPlaces = 1, Minimum = 0.1m, Maximum = 100m, Value = 2.0m, Increment = 0.5m, Enabled = false };
            WinFormsLabel lblUnitPt2 = new WinFormsLabel { Text = "m", Location = new WinFormsPoint(310, 68), AutoSize = true };
            WinFormsLabel lblPtNote = new WinFormsLabel { Text = "💡 Thích hợp chuyển đổi các điểm CAD Point từ khảo sát sang CogoPoint chuẩn của Civil 3D.", Location = new WinFormsPoint(15, 105), Size = new Size(320, 55), ForeColor = Color.DimGray };
            grpProps.Controls.AddRange(new Control[] { radPointNameAuto, radPointNameNearby, numPointRadiusName, lblUnitPt2, lblPtNote });

            // Option delete & Direct Action Button
            chkPointDeleteSource = new CheckBox { Text = "🗑️ Xóa đối tượng Point nguồn sau khi tạo thành công", Location = new WinFormsPoint(15, 260), AutoSize = true, ForeColor = Color.DarkRed };
            Button btnRunPoint = new Button
            {
                Text = "▶ Tạo COGO Point từ CAD Point",
                Location = new WinFormsPoint(440, 256),
                Size = new Size(280, 32),
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                Font = new DrawingFont("Segoe UI", 9.5F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnRunPoint.FlatAppearance.BorderSize = 0;
            btnRunPoint.Click += BtnExecute_Click;

            tabPoint.Controls.AddRange(new Control[] { grpSelect, grpZ, grpProps, chkPointDeleteSource, btnRunPoint });
        }

        private void BuildTabTable()
        {
            tabTable.Padding = new Padding(10);

            // Selection Group
            GroupBox grpSelect = new GroupBox { Text = "Chọn Bảng AutoCAD trên bản vẽ", Location = new WinFormsPoint(10, 10), Size = new Size(710, 60), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            btnPickTable = new Button { Text = "🎯 Chọn Bảng Table trên bản vẽ", Location = new WinFormsPoint(15, 22), Size = new Size(210, 28) };
            lblTableInfo = new WinFormsLabel { Text = "Chưa chọn bảng.", Location = new WinFormsPoint(235, 27), AutoSize = true, ForeColor = Color.DarkBlue, Font = new DrawingFont("Segoe UI", 9F, FontStyle.Bold) };
            WinFormsLabel lblStartRow = new WinFormsLabel { Text = "Dòng bắt đầu dữ liệu:", Location = new WinFormsPoint(500, 26), AutoSize = true };
            numTableStartRow = new NumericUpDown { Location = new WinFormsPoint(630, 24), Size = new Size(60, 23), Minimum = 1, Maximum = 1000, Value = 2 };
            grpSelect.Controls.AddRange(new Control[] { btnPickTable, lblTableInfo, lblStartRow, numTableStartRow });

            // Mapping Group
            GroupBox grpMapping = new GroupBox { Text = "Ghép Cột (Column Mapping)", Location = new WinFormsPoint(10, 75), Size = new Size(710, 95), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

            WinFormsLabel lblName = new WinFormsLabel { Text = "Cột Tên điểm:", Location = new WinFormsPoint(15, 25), AutoSize = true };
            cmbTableColName = new ComboBox { Location = new WinFormsPoint(15, 48), Size = new Size(125, 24), DropDownStyle = ComboBoxStyle.DropDownList };

            WinFormsLabel lblX = new WinFormsLabel { Text = "Cột X (East)*:", Location = new WinFormsPoint(155, 25), AutoSize = true, Font = new DrawingFont("Segoe UI", 9F, FontStyle.Bold) };
            cmbTableColX = new ComboBox { Location = new WinFormsPoint(155, 48), Size = new Size(125, 24), DropDownStyle = ComboBoxStyle.DropDownList };

            WinFormsLabel lblY = new WinFormsLabel { Text = "Cột Y (North)*:", Location = new WinFormsPoint(295, 25), AutoSize = true, Font = new DrawingFont("Segoe UI", 9F, FontStyle.Bold) };
            cmbTableColY = new ComboBox { Location = new WinFormsPoint(295, 48), Size = new Size(125, 24), DropDownStyle = ComboBoxStyle.DropDownList };

            WinFormsLabel lblZ = new WinFormsLabel { Text = "Cột Cao độ Z:", Location = new WinFormsPoint(435, 25), AutoSize = true };
            cmbTableColZ = new ComboBox { Location = new WinFormsPoint(435, 48), Size = new Size(125, 24), DropDownStyle = ComboBoxStyle.DropDownList };

            WinFormsLabel lblDesc = new WinFormsLabel { Text = "Cột Mô tả:", Location = new WinFormsPoint(575, 25), AutoSize = true };
            cmbTableColDesc = new ComboBox { Location = new WinFormsPoint(575, 48), Size = new Size(125, 24), DropDownStyle = ComboBoxStyle.DropDownList };

            grpMapping.Controls.AddRange(new Control[] { lblName, cmbTableColName, lblX, cmbTableColX, lblY, cmbTableColY, lblZ, cmbTableColZ, lblDesc, cmbTableColDesc });

            // Preview DataGridView & Action Button
            WinFormsLabel lblPreview = new WinFormsLabel { Text = "Xem trước bảng (5-10 dòng đầu):", Location = new WinFormsPoint(10, 177), AutoSize = true, Font = new DrawingFont("Segoe UI", 9F, FontStyle.Bold) };
            Button btnRunTable = new Button
            {
                Text = "▶ Tạo COGO Point từ Table",
                Location = new WinFormsPoint(450, 172),
                Size = new Size(270, 26),
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                Font = new DrawingFont("Segoe UI", 9F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnRunTable.FlatAppearance.BorderSize = 0;
            btnRunTable.Click += BtnExecute_Click;

            dgvTablePreview = new DataGridView
            {
                Location = new WinFormsPoint(10, 202),
                Size = new Size(710, 88),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                BackgroundColor = Color.White,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            tabTable.Controls.AddRange(new Control[] { grpSelect, grpMapping, lblPreview, btnRunTable, dgvTablePreview });
        }

        private void BuildTabExcel()
        {
            tabExcel.Padding = new Padding(10);

            // File selection Group
            GroupBox grpSelect = new GroupBox { Text = "File Excel / CSV Nguồn", Location = new WinFormsPoint(10, 10), Size = new Size(710, 60), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            txtExcelFilePath = new TextBox { Location = new WinFormsPoint(15, 24), Size = new Size(340, 23), ReadOnly = true };
            btnBrowseExcel = new Button { Text = "📂 Chọn file...", Location = new WinFormsPoint(365, 22), Size = new Size(100, 28) };

            WinFormsLabel lblSheet = new WinFormsLabel { Text = "Sheet:", Location = new WinFormsPoint(475, 26), AutoSize = true };
            cmbExcelSheet = new ComboBox { Location = new WinFormsPoint(520, 24), Size = new Size(170, 24), DropDownStyle = ComboBoxStyle.DropDownList };
            grpSelect.Controls.AddRange(new Control[] { txtExcelFilePath, btnBrowseExcel, lblSheet, cmbExcelSheet });

            // Mapping & Row Settings Group
            GroupBox grpMapping = new GroupBox { Text = "Cấu hình Dòng & Ghép Cột", Location = new WinFormsPoint(10, 75), Size = new Size(710, 95), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

            WinFormsLabel lblHeader = new WinFormsLabel { Text = "Tiêu đề (Header):", Location = new WinFormsPoint(15, 20), AutoSize = true };
            numExcelHeaderRow = new NumericUpDown { Location = new WinFormsPoint(15, 40), Size = new Size(65, 23), Minimum = 1, Maximum = 100, Value = 1 };

            WinFormsLabel lblStart = new WinFormsLabel { Text = "Dòng đầu (Data):", Location = new WinFormsPoint(90, 20), AutoSize = true };
            numExcelStartRow = new NumericUpDown { Location = new WinFormsPoint(90, 40), Size = new Size(65, 23), Minimum = 1, Maximum = 10000, Value = 2 };

            WinFormsLabel lblName = new WinFormsLabel { Text = "Cột Tên:", Location = new WinFormsPoint(170, 20), AutoSize = true };
            cmbExcelColName = new ComboBox { Location = new WinFormsPoint(170, 40), Size = new Size(100, 24), DropDownStyle = ComboBoxStyle.DropDownList };

            WinFormsLabel lblX = new WinFormsLabel { Text = "Cột X (East)*:", Location = new WinFormsPoint(280, 20), AutoSize = true, Font = new DrawingFont("Segoe UI", 9F, FontStyle.Bold) };
            cmbExcelColX = new ComboBox { Location = new WinFormsPoint(280, 40), Size = new Size(100, 24), DropDownStyle = ComboBoxStyle.DropDownList };

            WinFormsLabel lblY = new WinFormsLabel { Text = "Cột Y (North)*:", Location = new WinFormsPoint(390, 20), AutoSize = true, Font = new DrawingFont("Segoe UI", 9F, FontStyle.Bold) };
            cmbExcelColY = new ComboBox { Location = new WinFormsPoint(390, 40), Size = new Size(100, 24), DropDownStyle = ComboBoxStyle.DropDownList };

            WinFormsLabel lblZ = new WinFormsLabel { Text = "Cột Z (Cao độ):", Location = new WinFormsPoint(500, 20), AutoSize = true };
            cmbExcelColZ = new ComboBox { Location = new WinFormsPoint(500, 40), Size = new Size(100, 24), DropDownStyle = ComboBoxStyle.DropDownList };

            WinFormsLabel lblDesc = new WinFormsLabel { Text = "Cột Mô tả:", Location = new WinFormsPoint(605, 20), AutoSize = true };
            cmbExcelColDesc = new ComboBox { Location = new WinFormsPoint(605, 40), Size = new Size(95, 24), DropDownStyle = ComboBoxStyle.DropDownList };

            grpMapping.Controls.AddRange(new Control[] { lblHeader, numExcelHeaderRow, lblStart, numExcelStartRow, lblName, cmbExcelColName, lblX, cmbExcelColX, lblY, cmbExcelColY, lblZ, cmbExcelColZ, lblDesc, cmbExcelColDesc });

            // Preview DataGridView & Action Button
            WinFormsLabel lblPreview = new WinFormsLabel { Text = "Xem trước dữ liệu Excel/CSV:", Location = new WinFormsPoint(10, 177), AutoSize = true, Font = new DrawingFont("Segoe UI", 9F, FontStyle.Bold) };
            Button btnRunExcel = new Button
            {
                Text = "▶ Tạo COGO Point từ Excel/CSV",
                Location = new WinFormsPoint(450, 172),
                Size = new Size(270, 26),
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                Font = new DrawingFont("Segoe UI", 9F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnRunExcel.FlatAppearance.BorderSize = 0;
            btnRunExcel.Click += BtnExecute_Click;

            dgvExcelPreview = new DataGridView
            {
                Location = new WinFormsPoint(10, 202),
                Size = new Size(710, 88),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                BackgroundColor = Color.White,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            tabExcel.Controls.AddRange(new Control[] { grpSelect, grpMapping, lblPreview, btnRunExcel, dgvExcelPreview });
        }

        private GroupBox BuildGeneralSettingsGroup()
        {
            GroupBox grp = new GroupBox { Text = "Cài Đặt Chung Cho COGO Point", Padding = new Padding(10) };

            // Row 1: Numbering
            WinFormsLabel lblPrefix = new WinFormsLabel { Text = "Tiền tố:", Location = new WinFormsPoint(15, 25), AutoSize = true };
            txtPrefix = new TextBox { Location = new WinFormsPoint(70, 22), Size = new Size(70, 23), Text = "P-" };

            WinFormsLabel lblStartNo = new WinFormsLabel { Text = "STT đầu:", Location = new WinFormsPoint(150, 25), AutoSize = true };
            numStartNo = new NumericUpDown { Location = new WinFormsPoint(210, 22), Size = new Size(65, 23), Minimum = 1, Maximum = 9999999, Value = 1 };

            WinFormsLabel lblStep = new WinFormsLabel { Text = "Bước nhảy:", Location = new WinFormsPoint(285, 25), AutoSize = true };
            numStep = new NumericUpDown { Location = new WinFormsPoint(355, 22), Size = new Size(50, 23), Minimum = 1, Maximum = 100, Value = 1 };

            WinFormsLabel lblSuffix = new WinFormsLabel { Text = "Hậu tố:", Location = new WinFormsPoint(415, 25), AutoSize = true };
            txtSuffix = new TextBox { Location = new WinFormsPoint(465, 22), Size = new Size(60, 23) };

            WinFormsLabel lblDefDesc = new WinFormsLabel { Text = "Mô tả mặc định:", Location = new WinFormsPoint(535, 25), AutoSize = true };
            txtDefaultDesc = new TextBox { Location = new WinFormsPoint(635, 22), Size = new Size(80, 23), Text = "TN" };

            // Row 2: Point Group & Duplicate Filter
            chkAddToPointGroup = new CheckBox { Text = "Thêm vào Point Group:", Location = new WinFormsPoint(15, 60), AutoSize = true, Checked = true };
            txtPointGroupName = new TextBox { Location = new WinFormsPoint(170, 58), Size = new Size(160, 23), Text = "COGO_POINTS" };

            chkIgnoreDuplicates = new CheckBox { Text = "Bỏ qua điểm trùng tọa độ (Sai số =", Location = new WinFormsPoint(355, 60), AutoSize = true, Checked = true };
            numDuplicateTolerance = new NumericUpDown { Location = new WinFormsPoint(565, 58), Size = new Size(65, 23), DecimalPlaces = 3, Minimum = 0.001m, Maximum = 1.0m, Value = 0.005m, Increment = 0.005m };
            WinFormsLabel lblUnitTol = new WinFormsLabel { Text = "m)", Location = new WinFormsPoint(635, 60), AutoSize = true };

            // Row 3: Styles
            WinFormsLabel lblPtStyle = new WinFormsLabel { Text = "Point Style:", Location = new WinFormsPoint(15, 95), AutoSize = true };
            cmbPointStyle = new ComboBox { Location = new WinFormsPoint(95, 92), Size = new Size(235, 24), DropDownStyle = ComboBoxStyle.DropDownList };

            WinFormsLabel lblPtLabelStyle = new WinFormsLabel { Text = "Label Style:", Location = new WinFormsPoint(355, 95), AutoSize = true };
            cmbPointLabelStyle = new ComboBox { Location = new WinFormsPoint(435, 92), Size = new Size(280, 24), DropDownStyle = ComboBoxStyle.DropDownList };

            grp.Controls.AddRange(new Control[] {
                lblPrefix, txtPrefix, lblStartNo, numStartNo, lblStep, numStep, lblSuffix, txtSuffix, lblDefDesc, txtDefaultDesc,
                chkAddToPointGroup, txtPointGroupName, chkIgnoreDuplicates, numDuplicateTolerance, lblUnitTol,
                lblPtStyle, cmbPointStyle, lblPtLabelStyle, cmbPointLabelStyle
            });

            return grp;
        }
        #endregion

        #region Event Wiring
        private void AttachEvents()
        {
            // Radio enable/disable triggers
            radTextZFixed.CheckedChanged += (s, e) => numTextFixedZ.Enabled = radTextZFixed.Checked;
            radTextZSurface.CheckedChanged += (s, e) => cmbTextSurface.Enabled = radTextZSurface.Checked;
            chkTextFilterLayer.CheckedChanged += (s, e) => cmbTextLayer.Enabled = chkTextFilterLayer.Checked;

            radCircleZNearby.CheckedChanged += (s, e) => numCircleRadiusZ.Enabled = radCircleZNearby.Checked;
            radCircleZSurface.CheckedChanged += (s, e) => cmbCircleSurface.Enabled = radCircleZSurface.Checked;
            radCircleZFixed.CheckedChanged += (s, e) => numCircleFixedZ.Enabled = radCircleZFixed.Checked;
            radCircleNameNearby.CheckedChanged += (s, e) => numCircleRadiusName.Enabled = radCircleNameNearby.Checked;
            chkCircleFilterLayer.CheckedChanged += (s, e) => cmbCircleLayer.Enabled = chkCircleFilterLayer.Checked;

            radPointZNearby.CheckedChanged += (s, e) => numPointRadiusZ.Enabled = radPointZNearby.Checked;
            radPointZSurface.CheckedChanged += (s, e) => cmbPointSurface.Enabled = radPointZSurface.Checked;
            radPointZFixed.CheckedChanged += (s, e) => numPointFixedZ.Enabled = radPointZFixed.Checked;
            radPointNameNearby.CheckedChanged += (s, e) => numPointRadiusName.Enabled = radPointNameNearby.Checked;
            chkPointFilterLayer.CheckedChanged += (s, e) => cmbPointLayer.Enabled = chkPointFilterLayer.Checked;

            chkAddToPointGroup.CheckedChanged += (s, e) => txtPointGroupName.Enabled = chkAddToPointGroup.Checked;
            chkIgnoreDuplicates.CheckedChanged += (s, e) => numDuplicateTolerance.Enabled = chkIgnoreDuplicates.Checked;

            // Pick canvas buttons
            btnPickText.Click += BtnPickText_Click;
            btnPickCircle.Click += BtnPickCircle_Click;
            btnPickPoint.Click += BtnPickPoint_Click;
            btnPickTable.Click += BtnPickTable_Click;

            // Excel buttons & changes
            btnBrowseExcel.Click += BtnBrowseExcel_Click;
            cmbExcelSheet.SelectedIndexChanged += CmbExcelSheet_SelectedIndexChanged;
            numExcelHeaderRow.ValueChanged += (s, e) => RefreshExcelPreview();
            numExcelStartRow.ValueChanged += (s, e) => RefreshExcelPreview();

            // Form action buttons
            btnExecute.Click += BtnExecute_Click;
            btnClose.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
            this.FormClosing += TaoCogoPointForm_FormClosing;
        }
        #endregion

        #region Canvas Interaction Handlers
        private void BtnPickText_Click(object? sender, EventArgs e)
        {
            if (OnPickTexts != null)
            {
                var ids = OnPickTexts(this);
                if (ids != null)
                {
                    SelectedTextIds = ids;
                    lblTextCount.Text = $"Đã chọn: {SelectedTextIds.Count} Text / MText";
                }
            }
        }

        private void BtnPickCircle_Click(object? sender, EventArgs e)
        {
            if (OnPickCircles != null)
            {
                var ids = OnPickCircles(this);
                if (ids != null)
                {
                    SelectedCircleIds = ids;
                    lblCircleCount.Text = $"Đã chọn: {SelectedCircleIds.Count} Circle";
                }
            }
        }

        private void BtnPickPoint_Click(object? sender, EventArgs e)
        {
            if (OnPickPoints != null)
            {
                var ids = OnPickPoints(this);
                if (ids != null)
                {
                    SelectedPointIds = ids;
                    lblPointCount.Text = $"Đã chọn: {SelectedPointIds.Count} Point";
                }
            }
        }

        private void BtnPickTable_Click(object? sender, EventArgs e)
        {
            if (OnPickTable != null)
            {
                var id = OnPickTable(this);
                if (id != ObjectId.Null)
                {
                    SelectedTableId = id;
                    LoadTableStructureAndPreview(SelectedTableId);
                }
            }
        }
        #endregion

        #region Table Loading & Preview
        public void LoadTableStructureAndPreview(ObjectId tableId)
        {
            if (tableId.IsNull || !tableId.IsValid) return;

            try
            {
                using (Transaction tr = A.Db.TransactionManager.StartTransaction())
                {
                    var table = tr.GetObject(tableId, OpenMode.ForRead) as ATable;
                    if (table == null) return;

                    lblTableInfo.Text = $"Bảng (Handle: {table.Handle}) - {table.Rows.Count} hàng x {table.Columns.Count} cột";

                    // Setup ComboBox columns
                    List<string> colNames = new List<string>();
                    for (int c = 0; c < table.Columns.Count; c++)
                    {
                        string header = $"Cột {GetColumnLetter(c + 1)}";
                        // Read title/header text if available
                        if (table.Rows.Count > 0)
                        {
                            try
                            {
                                string text = table.Cells[0, c]?.TextString?.Trim() ?? "";
                                if (string.IsNullOrEmpty(text) && table.Rows.Count > 1)
                                    text = table.Cells[1, c]?.TextString?.Trim() ?? "";

                                if (!string.IsNullOrEmpty(text))
                                    header += $" ({CleanMText(text)})";
                            }
                            catch { }
                        }
                        colNames.Add(header);
                    }

                    PopulateMappingComboBoxes(cmbTableColName, colNames, "<Tự động theo STT>");
                    PopulateMappingComboBoxes(cmbTableColX, colNames, null);
                    PopulateMappingComboBoxes(cmbTableColY, colNames, null);
                    PopulateMappingComboBoxes(cmbTableColZ, colNames, "<Z = 0 / Không có>");
                    PopulateMappingComboBoxes(cmbTableColDesc, colNames, "<Theo mô tả mặc định>");

                    // Auto match column names
                    AutoDetectColumns(colNames, cmbTableColName, cmbTableColX, cmbTableColY, cmbTableColZ, cmbTableColDesc);

                    // Build Preview DataGridView
                    SysDataTable dt = new SysDataTable();
                    for (int c = 0; c < table.Columns.Count; c++)
                    {
                        dt.Columns.Add($"Col_{c}", typeof(string));
                    }

                    int previewMaxRows = Math.Min(10, table.Rows.Count);
                    for (int r = 0; r < previewMaxRows; r++)
                    {
                        SysDataRow dr = dt.NewRow();
                        for (int c = 0; c < table.Columns.Count; c++)
                        {
                            try { dr[c] = CleanMText(table.Cells[r, c]?.TextString ?? ""); }
                            catch { dr[c] = ""; }
                        }
                        dt.Rows.Add(dr);
                    }

                    dgvTablePreview.DataSource = dt;
                    for (int c = 0; c < table.Columns.Count; c++)
                    {
                        dgvTablePreview.Columns[c].HeaderText = GetColumnLetter(c + 1);
                    }

                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                lblTableInfo.Text = $"Lỗi đọc bảng: {ex.Message}";
            }
        }
        #endregion

        #region Excel Loading & Preview
        private void BtnBrowseExcel_Click(object? sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "Chọn file dữ liệu Excel / CSV";
                ofd.Filter = "Tất cả định dạng hỗ trợ (*.xlsx;*.xls;*.csv)|*.xlsx;*.xls;*.csv|Excel Workbook (*.xlsx)|*.xlsx|Excel 97-2003 (*.xls)|*.xls|CSV Files (*.csv)|*.csv";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    txtExcelFilePath.Text = ofd.FileName;
                    LoadExcelSheets(ofd.FileName);
                }
            }
        }

        private void LoadExcelSheets(string filePath)
        {
            try
            {
                cmbExcelSheet.Items.Clear();
                string ext = Path.GetExtension(filePath).ToLowerInvariant();

                if (ext == ".csv")
                {
                    cmbExcelSheet.Items.Add("CSV File");
                    cmbExcelSheet.SelectedIndex = 0;
                    cmbExcelSheet.Enabled = false;
                }
                else if (ext == ".xlsx")
                {
                    using (var workbook = new XLWorkbook(filePath))
                    {
                        foreach (var ws in workbook.Worksheets)
                        {
                            cmbExcelSheet.Items.Add(ws.Name);
                        }
                    }
                    if (cmbExcelSheet.Items.Count > 0) cmbExcelSheet.SelectedIndex = 0;
                    cmbExcelSheet.Enabled = true;
                }
                else
                {
                    // Fallback
                    cmbExcelSheet.Items.Add("Sheet1");
                    cmbExcelSheet.SelectedIndex = 0;
                }

                RefreshExcelPreview();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Lỗi đọc file: {ex.Message}", "Lỗi File", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CmbExcelSheet_SelectedIndexChanged(object? sender, EventArgs e)
        {
            RefreshExcelPreview();
        }

        private void RefreshExcelPreview()
        {
            string filePath = txtExcelFilePath.Text;
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

            try
            {
                string ext = Path.GetExtension(filePath).ToLowerInvariant();
                SysDataTable dt = new SysDataTable();
                List<string> colHeaders = new List<string>();

                if (ext == ".csv")
                {
                    var lines = File.ReadLines(filePath, Encoding.UTF8).Take(15).ToList();
                    if (lines.Count == 0) return;

                    char separator = lines[0].Contains(';') ? ';' : (lines[0].Contains('\t') ? '\t' : ',');
                    int headerRow = (int)numExcelHeaderRow.Value - 1;
                    if (headerRow < 0) headerRow = 0;

                    string[] headerParts = headerRow < lines.Count ? lines[headerRow].Split(separator) : lines[0].Split(separator);
                    for (int c = 0; c < headerParts.Length; c++)
                    {
                        string name = $"Cột {GetColumnLetter(c + 1)} ({headerParts[c].Trim()})";
                        colHeaders.Add(name);
                        dt.Columns.Add($"Col_{c}", typeof(string));
                    }

                    int startRow = (int)numExcelStartRow.Value - 1;
                    for (int r = startRow; r < lines.Count; r++)
                    {
                        var parts = lines[r].Split(separator);
                        SysDataRow dr = dt.NewRow();
                        for (int c = 0; c < Math.Min(parts.Length, dt.Columns.Count); c++)
                        {
                            dr[c] = parts[c].Trim();
                        }
                        dt.Rows.Add(dr);
                    }
                }
                else if (ext == ".xlsx")
                {
                    using (var workbook = new XLWorkbook(filePath))
                    {
                        string sheetName = cmbExcelSheet.SelectedItem?.ToString() ?? "";
                        var worksheet = string.IsNullOrEmpty(sheetName) ? workbook.Worksheet(1) : workbook.Worksheet(sheetName);

                        int headerRow = (int)numExcelHeaderRow.Value;
                        var hRow = worksheet.Row(headerRow);
                        int lastCol = worksheet.LastColumnUsed()?.ColumnNumber() ?? 10;

                        for (int c = 1; c <= lastCol; c++)
                        {
                            string headerText = hRow.Cell(c).GetString().Trim();
                            string colName = $"Cột {GetColumnLetter(c)}" + (string.IsNullOrEmpty(headerText) ? "" : $" ({headerText})");
                            colHeaders.Add(colName);
                            dt.Columns.Add($"Col_{c}", typeof(string));
                        }

                        int startRow = (int)numExcelStartRow.Value;
                        int previewCount = 0;
                        for (int r = startRow; r <= startRow + 10 && r <= worksheet.LastRowUsed()?.RowNumber(); r++)
                        {
                            var row = worksheet.Row(r);
                            SysDataRow dr = dt.NewRow();
                            for (int c = 1; c <= lastCol; c++)
                            {
                                dr[c - 1] = row.Cell(c).GetString().Trim();
                            }
                            dt.Rows.Add(dr);
                            previewCount++;
                        }
                    }
                }

                // Populate mapping combos
                PopulateMappingComboBoxes(cmbExcelColName, colHeaders, "<Tự động theo STT>");
                PopulateMappingComboBoxes(cmbExcelColX, colHeaders, null);
                PopulateMappingComboBoxes(cmbExcelColY, colHeaders, null);
                PopulateMappingComboBoxes(cmbExcelColZ, colHeaders, "<Z = 0 / Không có>");
                PopulateMappingComboBoxes(cmbExcelColDesc, colHeaders, "<Theo mô tả mặc định>");

                AutoDetectColumns(colHeaders, cmbExcelColName, cmbExcelColX, cmbExcelColY, cmbExcelColZ, cmbExcelColDesc);

                dgvExcelPreview.DataSource = dt;
                for (int c = 0; c < colHeaders.Count && c < dgvExcelPreview.Columns.Count; c++)
                {
                    dgvExcelPreview.Columns[c].HeaderText = GetColumnLetter(c + 1);
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Lỗi xem trước Excel: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        #endregion

        #region Helper Methods for Mapping & Detection
        private void PopulateMappingComboBoxes(ComboBox cmb, List<string> columns, string? defaultOption)
        {
            cmb.Items.Clear();
            if (defaultOption != null) cmb.Items.Add(defaultOption);

            foreach (var col in columns)
            {
                cmb.Items.Add(col);
            }

            if (cmb.Items.Count > 0) cmb.SelectedIndex = 0;
        }

        private void AutoDetectColumns(List<string> columns, ComboBox cmbName, ComboBox cmbX, ComboBox cmbY, ComboBox cmbZ, ComboBox cmbDesc)
        {
            // Detect common keywords: X/East/Easting, Y/North/Northing, Z/Elevation/CaoDo/H, Name/Point/Diem/STT, Code/Desc/MoTa
            for (int i = 0; i < columns.Count; i++)
            {
                string text = columns[i].ToLowerInvariant();
                int itemIdx = i + 1; // because index 0 is optional default

                if (Regex.IsMatch(text, @"\b(tên|ten|name|stt|point|pt|no|id)\b") && cmbName.SelectedIndex <= 0)
                {
                    cmbName.SelectedIndex = itemIdx;
                }
                else if (Regex.IsMatch(text, @"\b(x|east|easting|kinh_do|toadox)\b") && cmbX.SelectedIndex <= 0)
                {
                    cmbX.SelectedIndex = i; // cmbX has no default option
                }
                else if (Regex.IsMatch(text, @"\b(y|north|northing|vi_do|toadoy)\b") && cmbY.SelectedIndex <= 0)
                {
                    cmbY.SelectedIndex = i; // cmbY has no default option
                }
                else if (Regex.IsMatch(text, @"\b(z|caodo|cao_do|elev|elevation|h|cot)\b") && cmbZ.SelectedIndex <= 0)
                {
                    cmbZ.SelectedIndex = itemIdx;
                }
                else if (Regex.IsMatch(text, @"\b(desc|mota|mo_ta|ghichu|ghi_chu|code|layer)\b") && cmbDesc.SelectedIndex <= 0)
                {
                    cmbDesc.SelectedIndex = itemIdx;
                }
            }

            // Fallback default index if not detected
            if (cmbX.Items.Count > 0 && cmbX.SelectedIndex < 0) cmbX.SelectedIndex = 0;
            if (cmbY.Items.Count > 1 && cmbY.SelectedIndex < 0) cmbY.SelectedIndex = 1;
            else if (cmbY.Items.Count > 0 && cmbY.SelectedIndex < 0) cmbY.SelectedIndex = 0;
            if (cmbZ.Items.Count > 3 && cmbZ.SelectedIndex <= 0) cmbZ.SelectedIndex = 3;
        }

        private static string GetColumnLetter(int columnNumber)
        {
            string columnLetter = "";
            while (columnNumber > 0)
            {
                int modulo = (columnNumber - 1) % 26;
                columnLetter = Convert.ToChar('A' + modulo) + columnLetter;
                columnNumber = (columnNumber - modulo) / 26;
            }
            return columnLetter;
        }

        private static string CleanMText(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return Regex.Replace(text, @"\\[A-Za-z0-9]+|\\[PX].*?;|[{}]", "").Trim();
        }
        #endregion

        #region Initial Data & Restore Settings (Rule 2)
        private void PopulateInitialData(List<string> layers, List<string> surfaces, List<string> pointStyles, List<string> pointLabelStyles)
        {
            // Layers
            cmbTextLayer.Items.Add("<Tất cả>");
            cmbCircleLayer.Items.Add("<Tất cả>");
            cmbPointLayer.Items.Add("<Tất cả>");

            foreach (var l in layers)
            {
                cmbTextLayer.Items.Add(l);
                cmbCircleLayer.Items.Add(l);
                cmbPointLayer.Items.Add(l);
            }
            cmbTextLayer.SelectedIndex = 0;
            cmbCircleLayer.SelectedIndex = 0;
            cmbPointLayer.SelectedIndex = 0;

            // Surfaces
            cmbTextSurface.Items.Clear();
            cmbCircleSurface.Items.Clear();
            cmbPointSurface.Items.Clear();

            foreach (var s in surfaces)
            {
                cmbTextSurface.Items.Add(s);
                cmbCircleSurface.Items.Add(s);
                cmbPointSurface.Items.Add(s);
            }
            if (cmbTextSurface.Items.Count > 0) cmbTextSurface.SelectedIndex = 0;
            if (cmbCircleSurface.Items.Count > 0) cmbCircleSurface.SelectedIndex = 0;
            if (cmbPointSurface.Items.Count > 0) cmbPointSurface.SelectedIndex = 0;

            // Point Styles & Label Styles
            cmbPointStyle.Items.Clear();
            foreach (var ps in pointStyles) cmbPointStyle.Items.Add(ps);
            if (cmbPointStyle.Items.Count > 0) cmbPointStyle.SelectedIndex = 0;

            cmbPointLabelStyle.Items.Clear();
            foreach (var pls in pointLabelStyles) cmbPointLabelStyle.Items.Add(pls);
            if (cmbPointLabelStyle.Items.Count > 0) cmbPointLabelStyle.SelectedIndex = 0;
        }

        private void RestoreLastSettings()
        {
            try
            {
                this.Size = _lastFormSize;
                if (_lastSelectedTab >= 0 && _lastSelectedTab < tabSources.TabPages.Count)
                    tabSources.SelectedIndex = _lastSelectedTab;

                // General
                txtPrefix.Text = _lastPrefix;
                numStartNo.Value = Math.Max(1, Math.Min(numStartNo.Maximum, _lastStartNo));
                numStep.Value = Math.Max(1, Math.Min(numStep.Maximum, _lastStep));
                txtSuffix.Text = _lastSuffix;
                txtDefaultDesc.Text = _lastDefaultDesc;
                chkAddToPointGroup.Checked = _lastAddToPointGroup;
                txtPointGroupName.Text = _lastPointGroupName;
                txtPointGroupName.Enabled = _lastAddToPointGroup;
                chkIgnoreDuplicates.Checked = _lastIgnoreDuplicates;
                numDuplicateTolerance.Value = Math.Max(numDuplicateTolerance.Minimum, Math.Min(numDuplicateTolerance.Maximum, _lastDuplicateTolerance));

                if (_lastPointStyle == "<Mặc định>") _lastPointStyle = "<default>";
                if (cmbPointStyle.Items.Contains(_lastPointStyle)) cmbPointStyle.SelectedItem = _lastPointStyle;
                else if (cmbPointStyle.Items.Count > 0) cmbPointStyle.SelectedIndex = 0;

                if (_lastPointLabelStyle == "<Mặc định>") _lastPointLabelStyle = "<default>";
                if (cmbPointLabelStyle.Items.Contains(_lastPointLabelStyle)) cmbPointLabelStyle.SelectedItem = _lastPointLabelStyle;
                else if (cmbPointLabelStyle.Items.Count > 0) cmbPointLabelStyle.SelectedIndex = 0;

                // Tab Text
                radTextZParse.Checked = (_lastTextZMode == 0);
                radTextZ3D.Checked = (_lastTextZMode == 1);
                radTextZFixed.Checked = (_lastTextZMode == 2);
                radTextZSurface.Checked = (_lastTextZMode == 3);
                numTextFixedZ.Value = Math.Max(numTextFixedZ.Minimum, Math.Min(numTextFixedZ.Maximum, _lastTextFixedZ));
                numTextFixedZ.Enabled = radTextZFixed.Checked;
                if (cmbTextSurface.Items.Contains(_lastTextSurface)) cmbTextSurface.SelectedItem = _lastTextSurface;
                cmbTextSurface.Enabled = radTextZSurface.Checked;
                radTextNameAuto.Checked = (_lastTextNameMode == 0);
                radTextNameContent.Checked = (_lastTextNameMode == 1);
                radTextDescDefault.Checked = (_lastTextDescMode == 0);
                radTextDescLayer.Checked = (_lastTextDescMode == 1);
                chkTextDeleteSource.Checked = _lastTextDeleteSource;
                chkTextFilterLayer.Checked = _lastTextFilterLayer;
                cmbTextLayer.Enabled = _lastTextFilterLayer;
                if (cmbTextLayer.Items.Contains(_lastTextLayer)) cmbTextLayer.SelectedItem = _lastTextLayer;
                chkTextForceLeftJustify.Checked = _lastTextForceLeftJustify;

                // Tab Circle
                radCircleZCenter.Checked = (_lastCircleZMode == 0);
                radCircleZNearby.Checked = (_lastCircleZMode == 1);
                radCircleZSurface.Checked = (_lastCircleZMode == 2);
                radCircleZFixed.Checked = (_lastCircleZMode == 3);
                numCircleRadiusZ.Value = Math.Max(numCircleRadiusZ.Minimum, Math.Min(numCircleRadiusZ.Maximum, _lastCircleRadiusZ));
                numCircleRadiusZ.Enabled = radCircleZNearby.Checked;
                if (cmbCircleSurface.Items.Contains(_lastCircleSurface)) cmbCircleSurface.SelectedItem = _lastCircleSurface;
                cmbCircleSurface.Enabled = radCircleZSurface.Checked;
                numCircleFixedZ.Value = Math.Max(numCircleFixedZ.Minimum, Math.Min(numCircleFixedZ.Maximum, _lastCircleFixedZ));
                numCircleFixedZ.Enabled = radCircleZFixed.Checked;
                radCircleNameAuto.Checked = (_lastCircleNameMode == 0);
                radCircleNameNearby.Checked = (_lastCircleNameMode == 1);
                numCircleRadiusName.Value = Math.Max(numCircleRadiusName.Minimum, Math.Min(numCircleRadiusName.Maximum, _lastCircleRadiusName));
                numCircleRadiusName.Enabled = radCircleNameNearby.Checked;
                chkCircleDeleteSource.Checked = _lastCircleDeleteSource;
                chkCircleFilterLayer.Checked = _lastCircleFilterLayer;
                cmbCircleLayer.Enabled = _lastCircleFilterLayer;
                if (cmbCircleLayer.Items.Contains(_lastCircleLayer)) cmbCircleLayer.SelectedItem = _lastCircleLayer;

                // Tab Point
                radPointZPoint.Checked = (_lastPointZMode == 0);
                radPointZNearby.Checked = (_lastPointZMode == 1);
                radPointZSurface.Checked = (_lastPointZMode == 2);
                radPointZFixed.Checked = (_lastPointZMode == 3);
                numPointRadiusZ.Value = Math.Max(numPointRadiusZ.Minimum, Math.Min(numPointRadiusZ.Maximum, _lastPointRadiusZ));
                numPointRadiusZ.Enabled = radPointZNearby.Checked;
                if (cmbPointSurface.Items.Contains(_lastPointSurface)) cmbPointSurface.SelectedItem = _lastPointSurface;
                cmbPointSurface.Enabled = radPointZSurface.Checked;
                numPointFixedZ.Value = Math.Max(numPointFixedZ.Minimum, Math.Min(numPointFixedZ.Maximum, _lastPointFixedZ));
                numPointFixedZ.Enabled = radPointZFixed.Checked;
                radPointNameAuto.Checked = (_lastPointNameMode == 0);
                radPointNameNearby.Checked = (_lastPointNameMode == 1);
                numPointRadiusName.Value = Math.Max(numPointRadiusName.Minimum, Math.Min(numPointRadiusName.Maximum, _lastPointRadiusName));
                numPointRadiusName.Enabled = radPointNameNearby.Checked;
                chkPointDeleteSource.Checked = _lastPointDeleteSource;
                chkPointFilterLayer.Checked = _lastPointFilterLayer;
                cmbPointLayer.Enabled = _lastPointFilterLayer;
                if (cmbPointLayer.Items.Contains(_lastPointLayer)) cmbPointLayer.SelectedItem = _lastPointLayer;

                // Tab Table
                numTableStartRow.Value = Math.Max(1, Math.Min(numTableStartRow.Maximum, _lastTableStartRow));
                if (!_lastSelectedTableId.IsNull && _lastSelectedTableId.IsValid && !_lastSelectedTableId.IsErased)
                {
                    SelectedTableId = _lastSelectedTableId;
                    LoadTableStructureAndPreview(SelectedTableId);
                }

                // Tab Excel
                if (!string.IsNullOrEmpty(_lastExcelFilePath) && File.Exists(_lastExcelFilePath))
                {
                    txtExcelFilePath.Text = _lastExcelFilePath;
                    LoadExcelSheets(_lastExcelFilePath);
                    if (!string.IsNullOrEmpty(_lastExcelSheetName) && cmbExcelSheet.Items.Contains(_lastExcelSheetName))
                        cmbExcelSheet.SelectedItem = _lastExcelSheetName;
                    numExcelHeaderRow.Value = Math.Max(1, Math.Min(numExcelHeaderRow.Maximum, _lastExcelHeaderRow));
                    numExcelStartRow.Value = Math.Max(1, Math.Min(numExcelStartRow.Maximum, _lastExcelStartRow));
                }
            }
            catch { }
        }

        private void SaveCurrentSettings()
        {
            try
            {
                _lastFormSize = this.Size;
                _lastSelectedTab = tabSources.SelectedIndex;

                // General
                _lastPrefix = txtPrefix.Text;
                _lastStartNo = numStartNo.Value;
                _lastStep = numStep.Value;
                _lastSuffix = txtSuffix.Text;
                _lastDefaultDesc = txtDefaultDesc.Text;
                _lastAddToPointGroup = chkAddToPointGroup.Checked;
                _lastPointGroupName = txtPointGroupName.Text;
                _lastPointStyle = cmbPointStyle.SelectedItem?.ToString() ?? "<default>";
                _lastPointLabelStyle = cmbPointLabelStyle.SelectedItem?.ToString() ?? "<default>";
                _lastIgnoreDuplicates = chkIgnoreDuplicates.Checked;
                _lastDuplicateTolerance = numDuplicateTolerance.Value;

                // Tab Text
                _lastTextZMode = radTextZParse.Checked ? 0 : (radTextZ3D.Checked ? 1 : (radTextZFixed.Checked ? 2 : 3));
                _lastTextFixedZ = numTextFixedZ.Value;
                _lastTextSurface = cmbTextSurface.SelectedItem?.ToString() ?? "<Không chọn>";
                _lastTextNameMode = radTextNameAuto.Checked ? 0 : 1;
                _lastTextDescMode = radTextDescDefault.Checked ? 0 : 1;
                _lastTextDeleteSource = chkTextDeleteSource.Checked;
                _lastTextFilterLayer = chkTextFilterLayer.Checked;
                _lastTextLayer = cmbTextLayer.SelectedItem?.ToString() ?? "<Tất cả>";
                _lastTextForceLeftJustify = chkTextForceLeftJustify.Checked;

                // Tab Circle
                _lastCircleZMode = radCircleZCenter.Checked ? 0 : (radCircleZNearby.Checked ? 1 : (radCircleZSurface.Checked ? 2 : 3));
                _lastCircleRadiusZ = numCircleRadiusZ.Value;
                _lastCircleSurface = cmbCircleSurface.SelectedItem?.ToString() ?? "<Không chọn>";
                _lastCircleFixedZ = numCircleFixedZ.Value;
                _lastCircleNameMode = radCircleNameAuto.Checked ? 0 : 1;
                _lastCircleRadiusName = numCircleRadiusName.Value;
                _lastCircleDeleteSource = chkCircleDeleteSource.Checked;
                _lastCircleFilterLayer = chkCircleFilterLayer.Checked;
                _lastCircleLayer = cmbCircleLayer.SelectedItem?.ToString() ?? "<Tất cả>";

                // Tab Point
                _lastPointZMode = radPointZPoint.Checked ? 0 : (radPointZNearby.Checked ? 1 : (radPointZSurface.Checked ? 2 : 3));
                _lastPointRadiusZ = numPointRadiusZ.Value;
                _lastPointSurface = cmbPointSurface.SelectedItem?.ToString() ?? "<Không chọn>";
                _lastPointFixedZ = numPointFixedZ.Value;
                _lastPointNameMode = radPointNameAuto.Checked ? 0 : 1;
                _lastPointRadiusName = numPointRadiusName.Value;
                _lastPointDeleteSource = chkPointDeleteSource.Checked;
                _lastPointFilterLayer = chkPointFilterLayer.Checked;
                _lastPointLayer = cmbPointLayer.SelectedItem?.ToString() ?? "<Tất cả>";

                // Tab Table
                _lastSelectedTableId = SelectedTableId;
                _lastTableStartRow = numTableStartRow.Value;
                _lastTableColName = cmbTableColName.SelectedIndex;
                _lastTableColX = cmbTableColX.SelectedIndex;
                _lastTableColY = cmbTableColY.SelectedIndex;
                _lastTableColZ = cmbTableColZ.SelectedIndex;
                _lastTableColDesc = cmbTableColDesc.SelectedIndex;

                // Tab Excel
                _lastExcelFilePath = txtExcelFilePath.Text;
                _lastExcelSheetName = cmbExcelSheet.SelectedItem?.ToString() ?? "";
                _lastExcelHeaderRow = numExcelHeaderRow.Value;
                _lastExcelStartRow = numExcelStartRow.Value;
                _lastExcelColName = cmbExcelColName.SelectedIndex;
                _lastExcelColX = cmbExcelColX.SelectedIndex;
                _lastExcelColY = cmbExcelColY.SelectedIndex;
                _lastExcelColZ = cmbExcelColZ.SelectedIndex;
                _lastExcelColDesc = cmbExcelColDesc.SelectedIndex;
            }
            catch { }
        }

        private void TaoCogoPointForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            SaveCurrentSettings();
        }
        #endregion

        #region Execution Validation & Config Object Creation
        private void BtnExecute_Click(object? sender, EventArgs e)
        {
            // Validate based on active tab
            int tab = tabSources.SelectedIndex;

            if (tab == 0) // Text
            {
                if (SelectedTextIds.Count == 0 && !chkTextFilterLayer.Checked)
                {
                    MessageBox.Show("Vui lòng nhấn nút 'Chọn Text trên bản vẽ' hoặc chọn 'Lọc theo Layer'!", "Chưa chọn đối tượng", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (radTextZSurface.Checked && (cmbTextSurface.SelectedIndex <= 0 || cmbTextSurface.SelectedItem?.ToString() == "<Không chọn>"))
                {
                    MessageBox.Show("Vui lòng chọn Surface để lấy cao độ!", "Thiếu Surface", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
            else if (tab == 1) // Circle
            {
                if (SelectedCircleIds.Count == 0 && !chkCircleFilterLayer.Checked)
                {
                    MessageBox.Show("Vui lòng nhấn nút 'Chọn Circle trên bản vẽ' hoặc chọn 'Lọc theo Layer'!", "Chưa chọn đối tượng", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (radCircleZSurface.Checked && (cmbCircleSurface.SelectedIndex <= 0 || cmbCircleSurface.SelectedItem?.ToString() == "<Không chọn>"))
                {
                    MessageBox.Show("Vui lòng chọn Surface để lấy cao độ!", "Thiếu Surface", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
            else if (tab == 2) // Point
            {
                if (SelectedPointIds.Count == 0 && !chkPointFilterLayer.Checked)
                {
                    MessageBox.Show("Vui lòng nhấn nút 'Chọn Point trên bản vẽ' hoặc chọn 'Lọc theo Layer'!", "Chưa chọn đối tượng", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (radPointZSurface.Checked && (cmbPointSurface.SelectedIndex <= 0 || cmbPointSurface.SelectedItem?.ToString() == "<Không chọn>"))
                {
                    MessageBox.Show("Vui lòng chọn Surface để lấy cao độ!", "Thiếu Surface", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
            else if (tab == 3) // Table
            {
                if (SelectedTableId.IsNull || !SelectedTableId.IsValid)
                {
                    MessageBox.Show("Vui lòng nhấn nút 'Chọn Bảng Table trên bản vẽ'!", "Chưa chọn Bảng", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (cmbTableColX.SelectedIndex < 0 || cmbTableColY.SelectedIndex < 0)
                {
                    MessageBox.Show("Vui lòng ghép (Map) Cột X và Cột Y!", "Chưa ghép cột", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
            else if (tab == 4) // Excel
            {
                if (string.IsNullOrEmpty(txtExcelFilePath.Text) || !File.Exists(txtExcelFilePath.Text))
                {
                    MessageBox.Show("Vui lòng chọn file Excel / CSV hợp lệ!", "Chưa chọn file", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (cmbExcelColX.SelectedIndex < 0 || cmbExcelColY.SelectedIndex < 0)
                {
                    MessageBox.Show("Vui lòng ghép (Map) Cột X và Cột Y!", "Chưa ghép cột", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            if (chkAddToPointGroup.Checked && string.IsNullOrWhiteSpace(txtPointGroupName.Text))
            {
                MessageBox.Show("Vui lòng nhập tên Point Group!", "Thiếu tên nhóm điểm", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SaveCurrentSettings();
            this.FormAccepted = true;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        public CogoPointCreationConfig GetConfig()
        {
            return new CogoPointCreationConfig
            {
                SourceType = (CogoPointSourceType)tabSources.SelectedIndex,

                // General
                Prefix = txtPrefix.Text.Trim(),
                StartNumber = (int)numStartNo.Value,
                Step = (int)numStep.Value,
                Suffix = txtSuffix.Text.Trim(),
                DefaultDescription = txtDefaultDesc.Text.Trim(),
                AddToPointGroup = chkAddToPointGroup.Checked,
                PointGroupName = txtPointGroupName.Text.Trim(),
                PointStyleName = cmbPointStyle.SelectedItem?.ToString() ?? "<default>",
                PointLabelStyleName = cmbPointLabelStyle.SelectedItem?.ToString() ?? "<default>",
                IgnoreDuplicates = chkIgnoreDuplicates.Checked,
                DuplicateTolerance = (double)numDuplicateTolerance.Value,

                // Text
                TextZMode = radTextZParse.Checked ? 0 : (radTextZ3D.Checked ? 1 : (radTextZFixed.Checked ? 2 : 3)),
                TextFixedZ = (double)numTextFixedZ.Value,
                TextSurfaceName = cmbTextSurface.SelectedItem?.ToString() ?? "",
                TextNameMode = radTextNameAuto.Checked ? 0 : 1,
                TextDescMode = radTextDescDefault.Checked ? 0 : 1,
                TextDeleteSource = chkTextDeleteSource.Checked,
                TextFilterLayer = chkTextFilterLayer.Checked,
                TextLayerName = cmbTextLayer.SelectedItem?.ToString() ?? "",
                TextForceLeftJustify = chkTextForceLeftJustify.Checked,

                // Circle
                CircleZMode = radCircleZCenter.Checked ? 0 : (radCircleZNearby.Checked ? 1 : (radCircleZSurface.Checked ? 2 : 3)),
                CircleRadiusZ = (double)numCircleRadiusZ.Value,
                CircleSurfaceName = cmbCircleSurface.SelectedItem?.ToString() ?? "",
                CircleFixedZ = (double)numCircleFixedZ.Value,
                CircleNameMode = radCircleNameAuto.Checked ? 0 : 1,
                CircleRadiusName = (double)numCircleRadiusName.Value,
                CircleDeleteSource = chkCircleDeleteSource.Checked,
                CircleFilterLayer = chkCircleFilterLayer.Checked,
                CircleLayerName = cmbCircleLayer.SelectedItem?.ToString() ?? "",

                // Point
                PointZMode = radPointZPoint.Checked ? 0 : (radPointZNearby.Checked ? 1 : (radPointZSurface.Checked ? 2 : 3)),
                PointRadiusZ = (double)numPointRadiusZ.Value,
                PointSurfaceName = cmbPointSurface.SelectedItem?.ToString() ?? "",
                PointFixedZ = (double)numPointFixedZ.Value,
                PointNameMode = radPointNameAuto.Checked ? 0 : 1,
                PointRadiusName = (double)numPointRadiusName.Value,
                PointDeleteSource = chkPointDeleteSource.Checked,
                PointFilterLayer = chkPointFilterLayer.Checked,
                PointLayerName = cmbPointLayer.SelectedItem?.ToString() ?? "",

                // Table
                TableStartRow = (int)numTableStartRow.Value,
                TableColNameIndex = cmbTableColName.SelectedIndex - 1, // -1 if default
                TableColXIndex = cmbTableColX.SelectedIndex,
                TableColYIndex = cmbTableColY.SelectedIndex,
                TableColZIndex = cmbTableColZ.SelectedIndex - 1,
                TableColDescIndex = cmbTableColDesc.SelectedIndex - 1,

                // Excel
                ExcelFilePath = txtExcelFilePath.Text,
                ExcelSheetName = cmbExcelSheet.SelectedItem?.ToString() ?? "",
                ExcelHeaderRow = (int)numExcelHeaderRow.Value,
                ExcelStartRow = (int)numExcelStartRow.Value,
                ExcelColNameIndex = cmbExcelColName.SelectedIndex - 1,
                ExcelColXIndex = cmbExcelColX.SelectedIndex,
                ExcelColYIndex = cmbExcelColY.SelectedIndex,
                ExcelColZIndex = cmbExcelColZ.SelectedIndex - 1,
                ExcelColDescIndex = cmbExcelColDesc.SelectedIndex - 1
            };
        }
        #endregion
    }

    #region Data Transfer Objects & Enums
    public enum CogoPointSourceType
    {
        Text = 0,
        Circle = 1,
        Point = 2,
        Table = 3,
        Excel = 4
    }

    public class CogoPointCreationConfig
    {
        public CogoPointSourceType SourceType { get; set; }

        // General
        public string Prefix { get; set; } = "P-";
        public int StartNumber { get; set; } = 1;
        public int Step { get; set; } = 1;
        public string Suffix { get; set; } = "";
        public string DefaultDescription { get; set; } = "TN";
        public bool AddToPointGroup { get; set; } = true;
        public string PointGroupName { get; set; } = "COGO_POINTS";
        public string PointStyleName { get; set; } = "<default>";
        public string PointLabelStyleName { get; set; } = "<default>";
        public bool IgnoreDuplicates { get; set; } = true;
        public double DuplicateTolerance { get; set; } = 0.005;

        // Text
        public int TextZMode { get; set; }
        public double TextFixedZ { get; set; }
        public string TextSurfaceName { get; set; } = "";
        public int TextNameMode { get; set; }
        public int TextDescMode { get; set; }
        public bool TextDeleteSource { get; set; }
        public bool TextFilterLayer { get; set; }
        public string TextLayerName { get; set; } = "";
        public bool TextForceLeftJustify { get; set; } = true;

        // Circle
        public int CircleZMode { get; set; }
        public double CircleRadiusZ { get; set; } = 2.0;
        public string CircleSurfaceName { get; set; } = "";
        public double CircleFixedZ { get; set; }
        public int CircleNameMode { get; set; }
        public double CircleRadiusName { get; set; } = 2.0;
        public bool CircleDeleteSource { get; set; }
        public bool CircleFilterLayer { get; set; }
        public string CircleLayerName { get; set; } = "";

        // Point
        public int PointZMode { get; set; }
        public double PointRadiusZ { get; set; } = 2.0;
        public string PointSurfaceName { get; set; } = "";
        public double PointFixedZ { get; set; }
        public int PointNameMode { get; set; }
        public double PointRadiusName { get; set; } = 2.0;
        public bool PointDeleteSource { get; set; }
        public bool PointFilterLayer { get; set; }
        public string PointLayerName { get; set; } = "";

        // Table
        public int TableStartRow { get; set; } = 2;
        public int TableColNameIndex { get; set; } = -1;
        public int TableColXIndex { get; set; } = 0;
        public int TableColYIndex { get; set; } = 1;
        public int TableColZIndex { get; set; } = -1;
        public int TableColDescIndex { get; set; } = -1;

        // Excel
        public string ExcelFilePath { get; set; } = "";
        public string ExcelSheetName { get; set; } = "";
        public int ExcelHeaderRow { get; set; } = 1;
        public int ExcelStartRow { get; set; } = 2;
        public int ExcelColNameIndex { get; set; } = -1;
        public int ExcelColXIndex { get; set; } = 0;
        public int ExcelColYIndex { get; set; } = 1;
        public int ExcelColZIndex { get; set; } = -1;
        public int ExcelColDescIndex { get; set; } = -1;
    }

    public class RawPointInput
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public string? CustomName { get; set; }
        public string? CustomDescription { get; set; }
        public ObjectId? SourceObjectId { get; set; }
    }
    #endregion
}

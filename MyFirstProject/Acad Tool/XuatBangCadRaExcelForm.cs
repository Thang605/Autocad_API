// (C) Copyright 2026 by T27
// Form giao diện Xuất Bảng AutoCAD & Civil 3D sang Excel (.xlsx / .csv)
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.DatabaseServices;

using DrawingFont = System.Drawing.Font;
using DrawingFontStyle = System.Drawing.FontStyle;
using DrawingColor = System.Drawing.Color;
using DrawingSize = System.Drawing.Size;
using DrawingPoint = System.Drawing.Point;
using DrawingGraphicsUnit = System.Drawing.GraphicsUnit;

using WinFormsButton = System.Windows.Forms.Button;
using WinFormsCheckBox = System.Windows.Forms.CheckBox;
using WinFormsComboBox = System.Windows.Forms.ComboBox;
using WinFormsGroupBox = System.Windows.Forms.GroupBox;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsPanel = System.Windows.Forms.Panel;
using WinFormsProgressBar = System.Windows.Forms.ProgressBar;
using WinFormsRadioButton = System.Windows.Forms.RadioButton;
using WinFormsTextBox = System.Windows.Forms.TextBox;

namespace Civil3DCsharp
{
    /// <summary>
    /// Model đại diện cho một bảng được chọn trong bản vẽ
    /// </summary>
    public class CadTableInfoItem
    {
        public ObjectId ObjectId { get; set; }
        public string Handle { get; set; } = string.Empty;
        public string TableType { get; set; } = "AutoCAD Table";
        public string SpaceName { get; set; } = "Model";
        public int RowCount { get; set; }
        public int ColumnCount { get; set; }
        public string Title { get; set; } = string.Empty;
        public string TableStyleName { get; set; } = string.Empty;
        public bool IsSelected { get; set; } = true;
    }

    /// <summary>
    /// Cấu hình các tùy chọn xuất bảng ra Excel
    /// </summary>
    public class TableExportExcelConfig
    {
        public string OutputFilePath { get; set; } = string.Empty;
        public bool IsXlsxFormat { get; set; } = true;
        public bool ExportEachTableToSeparateSheet { get; set; } = true;
        public int BlankRowsBetweenTables { get; set; } = 2;

        public bool AutoFitColumns { get; set; } = true;
        public bool AddCellBorders { get; set; } = true;
        public bool KeepMergedCells { get; set; } = true;
        public bool FormatHeaderRows { get; set; } = true;
        public bool AutoDetectNumericValues { get; set; } = true;
        public bool CleanMTextFormatting { get; set; } = true;
        public bool OpenFileAfterExport { get; set; } = true;

        public List<CadTableInfoItem> SelectedTables { get; set; } = new List<CadTableInfoItem>();
    }

    /// <summary>
    /// Giao diện Form Xuất Bảng AutoCAD Ra Excel
    /// </summary>
    public class XuatBangCadRaExcelForm : Form
    {
        #region Persistent State (Ghi nhớ giá trị qua các phiên)

        private static string _lastExportDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        private static string _lastFileName = "Bang_ThongKe_CAD.xlsx";
        private static bool _lastIsXlsxFormat = true;
        private static bool _lastExportSeparateSheets = true;
        private static decimal _lastBlankRows = 2m;
        private static int _lastScopeIndex = 0; // 0: Pick, 1: Model, 2: All

        private static bool _lastAutoFit = true;
        private static bool _lastAddBorders = true;
        private static bool _lastKeepMerged = true;
        private static bool _lastFormatHeader = true;
        private static bool _lastParseNumbers = true;
        private static bool _lastCleanMText = true;
        private static bool _lastOpenAfterExport = true;
        private static DrawingSize _lastFormSize = new DrawingSize(820, 750);

        #endregion

        #region Callbacks tương tác với AutoCAD Engine

        public Func<List<CadTableInfoItem>>? OnPickTablesOnScreen { get; set; }
        public Func<List<CadTableInfoItem>>? OnScanModelTables { get; set; }
        public Func<List<CadTableInfoItem>>? OnScanAllDrawingTables { get; set; }
        public Action<TableExportExcelConfig, Action<string, int>>? OnExecuteExport { get; set; }

        #endregion

        #region UI Controls

        private WinFormsPanel pnlHeader = null!;
        private WinFormsLabel lblHeaderTitle = null!;
        private WinFormsLabel lblHeaderSubtitle = null!;

        // Group 1: Source & Scope
        private WinFormsGroupBox grpSource = null!;
        private WinFormsRadioButton radPickTables = null!;
        private WinFormsRadioButton radModelTables = null!;
        private WinFormsRadioButton radAllTables = null!;
        private WinFormsButton btnPickOnScreen = null!;
        private WinFormsButton btnRefreshScan = null!;

        private DataGridView dgvTables = null!;
        private WinFormsButton btnSelectAll = null!;
        private WinFormsButton btnDeselectAll = null!;
        private WinFormsButton btnInvertSelection = null!;
        private WinFormsLabel lblTableCount = null!;

        // Group 2: Output File & Sheet Configuration
        private WinFormsGroupBox grpOutput = null!;
        private WinFormsLabel lblFilePath = null!;
        private WinFormsTextBox txtFilePath = null!;
        private WinFormsButton btnBrowse = null!;
        private WinFormsRadioButton radFormatXlsx = null!;
        private WinFormsRadioButton radFormatCsv = null!;
        private WinFormsRadioButton radSeparateSheets = null!;
        private WinFormsRadioButton radSingleSheet = null!;
        private WinFormsLabel lblBlankRows = null!;
        private NumericUpDown numBlankRows = null!;

        // Group 3: Formatting & Excel Styling
        private WinFormsGroupBox grpStyling = null!;
        private WinFormsCheckBox chkAutoFitColumns = null!;
        private WinFormsCheckBox chkAddBorders = null!;
        private WinFormsCheckBox chkKeepMergedCells = null!;
        private WinFormsCheckBox chkFormatHeader = null!;
        private WinFormsCheckBox chkParseNumbers = null!;
        private WinFormsCheckBox chkCleanMText = null!;
        private WinFormsCheckBox chkOpenAfterExport = null!;

        // Footer & Actions
        private WinFormsPanel pnlFooter = null!;
        private WinFormsLabel lblStatus = null!;
        private WinFormsProgressBar prgProgress = null!;
        private WinFormsButton btnExport = null!;
        private WinFormsButton btnClose = null!;

        #endregion

        private readonly List<CadTableInfoItem> _currentTablesList = new List<CadTableInfoItem>();

        public XuatBangCadRaExcelForm(List<CadTableInfoItem>? initialTables = null)
        {
            InitializeComponent();
            RestoreLastSettings();

            if (initialTables != null && initialTables.Count > 0)
            {
                _currentTablesList.Clear();
                _currentTablesList.AddRange(initialTables);
                PopulateGrid();
            }
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Form Properties
            this.Text = "Xuất Bảng AutoCAD Ra Excel - T27 Tool";
            this.Font = new DrawingFont("Segoe UI", 9F, DrawingFontStyle.Regular, DrawingGraphicsUnit.Point);
            this.ForeColor = DrawingColor.FromArgb(30, 30, 30);
            this.BackColor = DrawingColor.FromArgb(246, 248, 250);
            this.Size = _lastFormSize;
            this.MinimumSize = new DrawingSize(750, 680);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ShowIcon = true;

            // 1. Header Panel (Excel Dark Green Modern Style)
            pnlHeader = new WinFormsPanel
            {
                Dock = DockStyle.Top,
                Height = 65,
                BackColor = DrawingColor.FromArgb(16, 124, 65), // Microsoft Excel Green
                Padding = new Padding(18, 10, 18, 8)
            };

            lblHeaderTitle = new WinFormsLabel
            {
                Text = "XUẤT BẢNG AUTOCAD / CIVIL 3D RA EXCEL",
                Font = new DrawingFont("Segoe UI", 12F, DrawingFontStyle.Bold, DrawingGraphicsUnit.Point),
                ForeColor = DrawingColor.White,
                AutoSize = true,
                Location = new DrawingPoint(16, 10)
            };

            lblHeaderSubtitle = new WinFormsLabel
            {
                Text = "Trích xuất dữ liệu bảng CAD, giữ cấu trúc ô gộp (Merge), định dạng thẩm mỹ & xuất file .xlsx / .csv",
                Font = new DrawingFont("Segoe UI", 8.75F, DrawingFontStyle.Regular, DrawingGraphicsUnit.Point),
                ForeColor = DrawingColor.FromArgb(225, 245, 235),
                AutoSize = true,
                Location = new DrawingPoint(18, 36)
            };

            pnlHeader.Controls.Add(lblHeaderTitle);
            pnlHeader.Controls.Add(lblHeaderSubtitle);

            // 2. Footer Panel
            pnlFooter = new WinFormsPanel
            {
                Dock = DockStyle.Bottom,
                Height = 70,
                BackColor = DrawingColor.FromArgb(235, 240, 245),
                Padding = new Padding(16, 8, 16, 8)
            };
            pnlFooter.Paint += (s, e) =>
            {
                // Kẻ đường line ngăn cách trên top footer
                using var pen = new System.Drawing.Pen(DrawingColor.FromArgb(205, 215, 225), 1);
                e.Graphics.DrawLine(pen, 0, 0, pnlFooter.Width, 0);
            };

            lblStatus = new WinFormsLabel
            {
                Text = "Sẵn sàng trích xuất bảng...",
                Font = new DrawingFont("Segoe UI", 8.75F, DrawingFontStyle.Italic, DrawingGraphicsUnit.Point),
                ForeColor = DrawingColor.FromArgb(60, 70, 80),
                Location = new DrawingPoint(16, 12),
                Size = new DrawingSize(420, 20),
                AutoEllipsis = true
            };

            prgProgress = new WinFormsProgressBar
            {
                Location = new DrawingPoint(16, 36),
                Size = new DrawingSize(420, 20),
                Style = ProgressBarStyle.Continuous,
                Visible = false
            };

            btnExport = new WinFormsButton
            {
                Text = "XUẤT RA FILE EXCEL",
                Font = new DrawingFont("Segoe UI", 10F, DrawingFontStyle.Bold, DrawingGraphicsUnit.Point),
                Size = new DrawingSize(190, 44),
                Location = new DrawingPoint(pnlFooter.Width - 310, 13),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = DrawingColor.FromArgb(16, 124, 65),
                ForeColor = DrawingColor.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnExport.FlatAppearance.BorderSize = 0;
            btnExport.Click += BtnExport_Click;

            btnClose = new WinFormsButton
            {
                Text = "Đóng",
                Font = new DrawingFont("Segoe UI", 9.5F, DrawingFontStyle.Regular, DrawingGraphicsUnit.Point),
                Size = new DrawingSize(100, 44),
                Location = new DrawingPoint(pnlFooter.Width - 110, 13),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = DrawingColor.FromArgb(215, 222, 230),
                ForeColor = DrawingColor.FromArgb(30, 30, 30),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => this.Close();

            pnlFooter.Controls.Add(lblStatus);
            pnlFooter.Controls.Add(prgProgress);
            pnlFooter.Controls.Add(btnExport);
            pnlFooter.Controls.Add(btnClose);

            // Gán AcceptButton và CancelButton
            this.AcceptButton = btnExport;
            this.CancelButton = btnClose;

            // 3. Main Content Container
            Panel pnlContent = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(16, 10, 16, 10)
            };

            // GROUP 1: NGUỒN VÀ DANH SÁCH BẢNG
            grpSource = new WinFormsGroupBox
            {
                Text = " 1. Danh Sách Bảng Cần Xuất ",
                Font = new DrawingFont("Segoe UI", 9.25F, DrawingFontStyle.Bold, DrawingGraphicsUnit.Point),
                ForeColor = DrawingColor.FromArgb(16, 124, 65),
                Location = new DrawingPoint(16, 10),
                Size = new DrawingSize(760, 260),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            radPickTables = new WinFormsRadioButton
            {
                Text = "Chọn trên màn hình (Pick)",
                Font = new DrawingFont("Segoe UI", 9F, DrawingFontStyle.Regular, DrawingGraphicsUnit.Point),
                ForeColor = DrawingColor.Black,
                Location = new DrawingPoint(16, 24),
                Size = new DrawingSize(180, 24),
                Checked = true
            };
            radPickTables.CheckedChanged += RadScope_CheckedChanged;

            radModelTables = new WinFormsRadioButton
            {
                Text = "Tất cả bảng trong Model Space",
                Font = new DrawingFont("Segoe UI", 9F, DrawingFontStyle.Regular, DrawingGraphicsUnit.Point),
                ForeColor = DrawingColor.Black,
                Location = new DrawingPoint(205, 24),
                Size = new DrawingSize(210, 24)
            };
            radModelTables.CheckedChanged += RadScope_CheckedChanged;

            radAllTables = new WinFormsRadioButton
            {
                Text = "Toàn bộ bản vẽ (Model + Layouts)",
                Font = new DrawingFont("Segoe UI", 9F, DrawingFontStyle.Regular, DrawingGraphicsUnit.Point),
                ForeColor = DrawingColor.Black,
                Location = new DrawingPoint(425, 24),
                Size = new DrawingSize(230, 24)
            };
            radAllTables.CheckedChanged += RadScope_CheckedChanged;

            btnPickOnScreen = new WinFormsButton
            {
                Text = "Chọn Bảng Trên Bản Vẽ",
                Font = new DrawingFont("Segoe UI", 9F, DrawingFontStyle.Bold, DrawingGraphicsUnit.Point),
                Location = new DrawingPoint(16, 54),
                Size = new DrawingSize(180, 28),
                BackColor = DrawingColor.FromArgb(235, 245, 238),
                ForeColor = DrawingColor.FromArgb(16, 124, 65),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnPickOnScreen.FlatAppearance.BorderColor = DrawingColor.FromArgb(16, 124, 65);
            btnPickOnScreen.Click += BtnPickOnScreen_Click;

            btnRefreshScan = new WinFormsButton
            {
                Text = "Quét Lại Dữ Liệu",
                Font = new DrawingFont("Segoe UI", 9F, DrawingFontStyle.Regular, DrawingGraphicsUnit.Point),
                Location = new DrawingPoint(205, 54),
                Size = new DrawingSize(140, 28),
                BackColor = DrawingColor.White,
                ForeColor = DrawingColor.FromArgb(50, 50, 50),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnRefreshScan.FlatAppearance.BorderColor = DrawingColor.FromArgb(180, 190, 200);
            btnRefreshScan.Click += BtnRefreshScan_Click;

            lblTableCount = new WinFormsLabel
            {
                Text = "Đã phát hiện: 0 bảng (0 được chọn)",
                Font = new DrawingFont("Segoe UI", 8.75F, DrawingFontStyle.Italic, DrawingGraphicsUnit.Point),
                ForeColor = DrawingColor.FromArgb(90, 100, 110),
                Location = new DrawingPoint(360, 59),
                AutoSize = true
            };

            // DataGridView Tables
            dgvTables = new DataGridView
            {
                Location = new DrawingPoint(16, 88),
                Size = new DrawingSize(726, 132),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackgroundColor = DrawingColor.White,
                BorderStyle = BorderStyle.Fixed3D,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                Font = new DrawingFont("Segoe UI", 8.75F, DrawingFontStyle.Regular, DrawingGraphicsUnit.Point),
                ForeColor = DrawingColor.FromArgb(40, 40, 40)
            };

            var colCheck = new DataGridViewCheckBoxColumn
            {
                HeaderText = "Chọn",
                Name = "colCheck",
                Width = 48,
                Resizable = DataGridViewTriState.False
            };
            var colIndex = new DataGridViewTextBoxColumn
            {
                HeaderText = "STT",
                Name = "colIndex",
                Width = 45,
                ReadOnly = true
            };
            var colType = new DataGridViewTextBoxColumn
            {
                HeaderText = "Loại Bảng",
                Name = "colType",
                Width = 140,
                ReadOnly = true
            };
            var colSpace = new DataGridViewTextBoxColumn
            {
                HeaderText = "Vị Trí (Space)",
                Name = "colSpace",
                Width = 95,
                ReadOnly = true
            };
            var colSize = new DataGridViewTextBoxColumn
            {
                HeaderText = "Kích Thước",
                Name = "colSize",
                Width = 85,
                ReadOnly = true
            };
            var colStyle = new DataGridViewTextBoxColumn
            {
                HeaderText = "Table Style",
                Name = "colStyle",
                Width = 100,
                ReadOnly = true
            };
            var colTitle = new DataGridViewTextBoxColumn
            {
                HeaderText = "Tiêu Đề / Nội Dung Đầu",
                Name = "colTitle",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                ReadOnly = true
            };

            dgvTables.Columns.AddRange(colCheck, colIndex, colType, colSpace, colSize, colStyle, colTitle);
            dgvTables.CellValueChanged += DgvTables_CellValueChanged;
            dgvTables.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (dgvTables.IsCurrentCellDirty) dgvTables.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            btnSelectAll = new WinFormsButton
            {
                Text = "Chọn tất cả",
                Font = new DrawingFont("Segoe UI", 8.25F, DrawingFontStyle.Regular, DrawingGraphicsUnit.Point),
                Location = new DrawingPoint(16, 226),
                Size = new DrawingSize(85, 24),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                FlatStyle = FlatStyle.Flat
            };
            btnSelectAll.Click += (s, e) => SetAllGridChecked(true);

            btnDeselectAll = new WinFormsButton
            {
                Text = "Bỏ chọn",
                Font = new DrawingFont("Segoe UI", 8.25F, DrawingFontStyle.Regular, DrawingGraphicsUnit.Point),
                Location = new DrawingPoint(106, 226),
                Size = new DrawingSize(75, 24),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                FlatStyle = FlatStyle.Flat
            };
            btnDeselectAll.Click += (s, e) => SetAllGridChecked(false);

            btnInvertSelection = new WinFormsButton
            {
                Text = "Đảo lựa chọn",
                Font = new DrawingFont("Segoe UI", 8.25F, DrawingFontStyle.Regular, DrawingGraphicsUnit.Point),
                Location = new DrawingPoint(186, 226),
                Size = new DrawingSize(95, 24),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                FlatStyle = FlatStyle.Flat
            };
            btnInvertSelection.Click += (s, e) => InvertGridChecked();

            grpSource.Controls.Add(radPickTables);
            grpSource.Controls.Add(radModelTables);
            grpSource.Controls.Add(radAllTables);
            grpSource.Controls.Add(btnPickOnScreen);
            grpSource.Controls.Add(btnRefreshScan);
            grpSource.Controls.Add(lblTableCount);
            grpSource.Controls.Add(dgvTables);
            grpSource.Controls.Add(btnSelectAll);
            grpSource.Controls.Add(btnDeselectAll);
            grpSource.Controls.Add(btnInvertSelection);

            // GROUP 2: TÙY CHỌN FILE VÀ SHEET XUẤT
            grpOutput = new WinFormsGroupBox
            {
                Text = " 2. Đường Dẫn File & Cấu Trúc Sheet ",
                Font = new DrawingFont("Segoe UI", 9.25F, DrawingFontStyle.Bold, DrawingGraphicsUnit.Point),
                ForeColor = DrawingColor.FromArgb(16, 124, 65),
                Location = new DrawingPoint(16, 280),
                Size = new DrawingSize(760, 130),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            lblFilePath = new WinFormsLabel
            {
                Text = "File xuất:",
                Font = new DrawingFont("Segoe UI", 9F, DrawingFontStyle.Regular, DrawingGraphicsUnit.Point),
                ForeColor = DrawingColor.Black,
                Location = new DrawingPoint(16, 26),
                AutoSize = true
            };

            txtFilePath = new WinFormsTextBox
            {
                Location = new DrawingPoint(80, 23),
                Size = new DrawingSize(550, 24),
                Font = new DrawingFont("Segoe UI", 9F, DrawingFontStyle.Regular, DrawingGraphicsUnit.Point),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            btnBrowse = new WinFormsButton
            {
                Text = "Chọn File...",
                Font = new DrawingFont("Segoe UI", 9F, DrawingFontStyle.Regular, DrawingGraphicsUnit.Point),
                Location = new DrawingPoint(638, 22),
                Size = new DrawingSize(104, 26),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnBrowse.Click += BtnBrowse_Click;

            radFormatXlsx = new WinFormsRadioButton
            {
                Text = "Excel Workbook (.xlsx) - Khuyên dùng",
                Font = new DrawingFont("Segoe UI", 9F, DrawingFontStyle.Regular, DrawingGraphicsUnit.Point),
                ForeColor = DrawingColor.Black,
                Location = new DrawingPoint(80, 56),
                Size = new DrawingSize(250, 24),
                Checked = true
            };
            radFormatXlsx.CheckedChanged += RadFormat_CheckedChanged;

            radFormatCsv = new WinFormsRadioButton
            {
                Text = "File CSV (.csv) - Text thuần",
                Font = new DrawingFont("Segoe UI", 9F, DrawingFontStyle.Regular, DrawingGraphicsUnit.Point),
                ForeColor = DrawingColor.Black,
                Location = new DrawingPoint(340, 56),
                Size = new DrawingSize(200, 24)
            };

            radSeparateSheets = new WinFormsRadioButton
            {
                Text = "Mỗi bảng 1 Sheet riêng",
                Font = new DrawingFont("Segoe UI", 9F, DrawingFontStyle.Regular, DrawingGraphicsUnit.Point),
                ForeColor = DrawingColor.Black,
                Location = new DrawingPoint(80, 88),
                Size = new DrawingSize(180, 24),
                Checked = true
            };

            radSingleSheet = new WinFormsRadioButton
            {
                Text = "Gộp tất cả vào 1 Sheet chung (Cách nhau:",
                Font = new DrawingFont("Segoe UI", 9F, DrawingFontStyle.Regular, DrawingGraphicsUnit.Point),
                ForeColor = DrawingColor.Black,
                Location = new DrawingPoint(270, 88),
                Size = new DrawingSize(260, 24)
            };
            radSingleSheet.CheckedChanged += RadSheet_CheckedChanged;

            numBlankRows = new NumericUpDown
            {
                Location = new DrawingPoint(532, 89),
                Size = new DrawingSize(45, 24),
                Minimum = 1,
                Maximum = 10,
                Value = 2,
                Enabled = false
            };

            lblBlankRows = new WinFormsLabel
            {
                Text = "dòng trống)",
                Font = new DrawingFont("Segoe UI", 9F, DrawingFontStyle.Regular, DrawingGraphicsUnit.Point),
                ForeColor = DrawingColor.Black,
                Location = new DrawingPoint(582, 91),
                AutoSize = true
            };

            grpOutput.Controls.Add(lblFilePath);
            grpOutput.Controls.Add(txtFilePath);
            grpOutput.Controls.Add(btnBrowse);
            grpOutput.Controls.Add(radFormatXlsx);
            grpOutput.Controls.Add(radFormatCsv);
            grpOutput.Controls.Add(radSeparateSheets);
            grpOutput.Controls.Add(radSingleSheet);
            grpOutput.Controls.Add(numBlankRows);
            grpOutput.Controls.Add(lblBlankRows);

            // GROUP 3: TÙY CHỌN ĐỊNH DẠNG & THẨM MỸ EXCEL
            grpStyling = new WinFormsGroupBox
            {
                Text = " 3. Tùy Chọn Định Dạng & Thẩm Mỹ Excel ",
                Font = new DrawingFont("Segoe UI", 9.25F, DrawingFontStyle.Bold, DrawingGraphicsUnit.Point),
                ForeColor = DrawingColor.FromArgb(16, 124, 65),
                Location = new DrawingPoint(16, 420),
                Size = new DrawingSize(760, 140),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            chkAutoFitColumns = new WinFormsCheckBox
            {
                Text = "Tự động căn chỉnh độ rộng cột (Auto-fit Columns)",
                Font = new DrawingFont("Segoe UI", 9F, DrawingFontStyle.Regular, DrawingGraphicsUnit.Point),
                ForeColor = DrawingColor.Black,
                Location = new DrawingPoint(20, 26),
                Size = new DrawingSize(340, 24),
                Checked = true
            };

            chkAddBorders = new WinFormsCheckBox
            {
                Text = "Kẻ khung viền ô (Cell Borders)",
                Font = new DrawingFont("Segoe UI", 9F, DrawingFontStyle.Regular, DrawingGraphicsUnit.Point),
                ForeColor = DrawingColor.Black,
                Location = new DrawingPoint(375, 26),
                Size = new DrawingSize(340, 24),
                Checked = true
            };

            chkKeepMergedCells = new WinFormsCheckBox
            {
                Text = "Giữ nguyên các ô gộp (Merge Cells) từ CAD",
                Font = new DrawingFont("Segoe UI", 9F, DrawingFontStyle.Regular, DrawingGraphicsUnit.Point),
                ForeColor = DrawingColor.Black,
                Location = new DrawingPoint(20, 54),
                Size = new DrawingSize(340, 24),
                Checked = true
            };

            chkFormatHeader = new WinFormsCheckBox
            {
                Text = "Định dạng nổi bật dòng Header/Title (In đậm, nền màu)",
                Font = new DrawingFont("Segoe UI", 9F, DrawingFontStyle.Regular, DrawingGraphicsUnit.Point),
                ForeColor = DrawingColor.Black,
                Location = new DrawingPoint(375, 54),
                Size = new DrawingSize(360, 24),
                Checked = true
            };

            chkParseNumbers = new WinFormsCheckBox
            {
                Text = "Tự động nhận diện kiểu số (Hỗ trợ SUM, AVERAGE...)",
                Font = new DrawingFont("Segoe UI", 9F, DrawingFontStyle.Regular, DrawingGraphicsUnit.Point),
                ForeColor = DrawingColor.Black,
                Location = new DrawingPoint(20, 82),
                Size = new DrawingSize(340, 24),
                Checked = true
            };

            chkCleanMText = new WinFormsCheckBox
            {
                Text = "Làm sạch mã định dạng chữ MText (\\P, \\f, \\C...)",
                Font = new DrawingFont("Segoe UI", 9F, DrawingFontStyle.Regular, DrawingGraphicsUnit.Point),
                ForeColor = DrawingColor.Black,
                Location = new DrawingPoint(375, 82),
                Size = new DrawingSize(340, 24),
                Checked = true
            };

            chkOpenAfterExport = new WinFormsCheckBox
            {
                Text = "Tự động mở file Excel sau khi xuất hoàn tất",
                Font = new DrawingFont("Segoe UI", 9F, DrawingFontStyle.Bold, DrawingGraphicsUnit.Point),
                ForeColor = DrawingColor.FromArgb(16, 124, 65),
                Location = new DrawingPoint(20, 110),
                Size = new DrawingSize(350, 24),
                Checked = true
            };

            grpStyling.Controls.Add(chkAutoFitColumns);
            grpStyling.Controls.Add(chkAddBorders);
            grpStyling.Controls.Add(chkKeepMergedCells);
            grpStyling.Controls.Add(chkFormatHeader);
            grpStyling.Controls.Add(chkParseNumbers);
            grpStyling.Controls.Add(chkCleanMText);
            grpStyling.Controls.Add(chkOpenAfterExport);

            // Add to Content
            pnlContent.Controls.Add(grpSource);
            pnlContent.Controls.Add(grpOutput);
            pnlContent.Controls.Add(grpStyling);

            // Add all to Form in proper docking order
            this.Controls.Add(pnlContent);
            this.Controls.Add(pnlFooter);
            this.Controls.Add(pnlHeader);

            pnlHeader.BringToFront();
            pnlFooter.BringToFront();
            pnlContent.BringToFront();

            this.FormClosing += XuatBangCadRaExcelForm_FormClosing;

            this.ResumeLayout(false);
        }

        #region Event Handlers

        private void RadScope_CheckedChanged(object? sender, EventArgs e)
        {
            if (radPickTables.Checked)
            {
                btnPickOnScreen.Enabled = true;
            }
            else if (radModelTables.Checked)
            {
                btnPickOnScreen.Enabled = false;
                ScanTablesFromModel();
            }
            else if (radAllTables.Checked)
            {
                btnPickOnScreen.Enabled = false;
                ScanTablesFromAll();
            }
        }

        private void RadFormat_CheckedChanged(object? sender, EventArgs e)
        {
            bool isXlsx = radFormatXlsx.Checked;
            string currentPath = txtFilePath.Text.Trim();
            if (!string.IsNullOrEmpty(currentPath))
            {
                string dir = Path.GetDirectoryName(currentPath) ?? _lastExportDirectory;
                string fileWithoutExt = Path.GetFileNameWithoutExtension(currentPath);
                string newExt = isXlsx ? ".xlsx" : ".csv";
                txtFilePath.Text = Path.Combine(dir, fileWithoutExt + newExt);
            }

            // CSV doesn't support multiple sheets or rich formatting
            radSeparateSheets.Enabled = isXlsx;
            radSingleSheet.Enabled = isXlsx;
            chkAddBorders.Enabled = isXlsx;
            chkKeepMergedCells.Enabled = isXlsx;
            chkFormatHeader.Enabled = isXlsx;
            chkAutoFitColumns.Enabled = isXlsx;
        }

        private void RadSheet_CheckedChanged(object? sender, EventArgs e)
        {
            numBlankRows.Enabled = radSingleSheet.Checked;
        }

        private void BtnPickOnScreen_Click(object? sender, EventArgs e)
        {
            if (OnPickTablesOnScreen != null)
            {
                var picked = OnPickTablesOnScreen();
                if (picked != null && picked.Count > 0)
                {
                    _currentTablesList.Clear();
                    _currentTablesList.AddRange(picked);
                    PopulateGrid();
                }
            }
        }

        private void BtnRefreshScan_Click(object? sender, EventArgs e)
        {
            if (radPickTables.Checked)
            {
                BtnPickOnScreen_Click(sender, e);
            }
            else if (radModelTables.Checked)
            {
                ScanTablesFromModel();
            }
            else if (radAllTables.Checked)
            {
                ScanTablesFromAll();
            }
        }

        private void ScanTablesFromModel()
        {
            if (OnScanModelTables != null)
            {
                var tables = OnScanModelTables();
                _currentTablesList.Clear();
                if (tables != null) _currentTablesList.AddRange(tables);
                PopulateGrid();
            }
        }

        private void ScanTablesFromAll()
        {
            if (OnScanAllDrawingTables != null)
            {
                var tables = OnScanAllDrawingTables();
                _currentTablesList.Clear();
                if (tables != null) _currentTablesList.AddRange(tables);
                PopulateGrid();
            }
        }

        private void BtnBrowse_Click(object? sender, EventArgs e)
        {
            using SaveFileDialog sfd = new SaveFileDialog
            {
                Title = "Chọn nơi lưu file Excel xuất bảng CAD",
                Filter = radFormatXlsx.Checked
                    ? "Excel Workbook (*.xlsx)|*.xlsx|Tất cả tệp (*.*)|*.*"
                    : "CSV File (*.csv)|*.csv|Tất cả tệp (*.*)|*.*",
                DefaultExt = radFormatXlsx.Checked ? "xlsx" : "csv",
                AddExtension = true,
                InitialDirectory = Directory.Exists(_lastExportDirectory) ? _lastExportDirectory : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                FileName = Path.GetFileName(txtFilePath.Text.Trim())
            };

            if (sfd.ShowDialog(this) == DialogResult.OK)
            {
                txtFilePath.Text = sfd.FileName;
                _lastExportDirectory = Path.GetDirectoryName(sfd.FileName) ?? _lastExportDirectory;
                _lastFileName = Path.GetFileName(sfd.FileName);
            }
        }

        private void SetAllGridChecked(bool check)
        {
            foreach (DataGridViewRow row in dgvTables.Rows)
            {
                row.Cells["colCheck"].Value = check;
            }
            foreach (var item in _currentTablesList)
            {
                item.IsSelected = check;
            }
            UpdateTableCountLabel();
        }

        private void InvertGridChecked()
        {
            foreach (DataGridViewRow row in dgvTables.Rows)
            {
                bool cur = Convert.ToBoolean(row.Cells["colCheck"].Value ?? false);
                row.Cells["colCheck"].Value = !cur;
            }
            for (int i = 0; i < _currentTablesList.Count && i < dgvTables.Rows.Count; i++)
            {
                _currentTablesList[i].IsSelected = Convert.ToBoolean(dgvTables.Rows[i].Cells["colCheck"].Value);
            }
            UpdateTableCountLabel();
        }

        private void DgvTables_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == dgvTables.Columns["colCheck"].Index)
            {
                if (e.RowIndex < _currentTablesList.Count)
                {
                    _currentTablesList[e.RowIndex].IsSelected = Convert.ToBoolean(dgvTables.Rows[e.RowIndex].Cells["colCheck"].Value ?? false);
                }
                UpdateTableCountLabel();
            }
        }

        private void BtnExport_Click(object? sender, EventArgs e)
        {
            // Kiểm tra các bảng được chọn
            var selected = _currentTablesList.Where(t => t.IsSelected).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show(this, "Vui lòng chọn ít nhất 1 bảng trong danh sách để xuất!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string outPath = txtFilePath.Text.Trim();
            if (string.IsNullOrEmpty(outPath))
            {
                MessageBox.Show(this, "Vui lòng chọn đường dẫn file xuất Excel hợp lệ!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Tạo thư mục nếu chưa có
            try
            {
                string? dir = Path.GetDirectoryName(outPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(this, $"Không thể tạo thư mục lưu file:\n{ex.Message}", "Lỗi đường dẫn", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Đóng gói cấu hình
            TableExportExcelConfig config = new TableExportExcelConfig
            {
                OutputFilePath = outPath,
                IsXlsxFormat = radFormatXlsx.Checked,
                ExportEachTableToSeparateSheet = radSeparateSheets.Checked,
                BlankRowsBetweenTables = (int)numBlankRows.Value,
                AutoFitColumns = chkAutoFitColumns.Checked,
                AddCellBorders = chkAddBorders.Checked,
                KeepMergedCells = chkKeepMergedCells.Checked,
                FormatHeaderRows = chkFormatHeader.Checked,
                AutoDetectNumericValues = chkParseNumbers.Checked,
                CleanMTextFormatting = chkCleanMText.Checked,
                OpenFileAfterExport = chkOpenAfterExport.Checked,
                SelectedTables = selected
            };

            SaveCurrentSettings();

            // Khóa giao diện trong khi xuất
            SetControlsEnabled(false);
            prgProgress.Visible = true;
            prgProgress.Value = 10;
            lblStatus.Text = "Đang trích xuất dữ liệu bảng...";

            try
            {
                if (OnExecuteExport != null)
                {
                    OnExecuteExport(config, (msg, percent) =>
                    {
                        if (this.InvokeRequired)
                        {
                            this.Invoke(new Action(() =>
                            {
                                lblStatus.Text = msg;
                                if (percent >= 0 && percent <= 100) prgProgress.Value = percent;
                            }));
                        }
                        else
                        {
                            lblStatus.Text = msg;
                            if (percent >= 0 && percent <= 100) prgProgress.Value = percent;
                        }
                    });
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(this, $"Lỗi trong quá trình xuất Excel:\n{ex.Message}", "Lỗi xuất file", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetControlsEnabled(true);
                prgProgress.Visible = false;
                lblStatus.Text = "Hoàn tất xuất dữ liệu!";
            }
        }

        private void SetControlsEnabled(bool enabled)
        {
            btnExport.Enabled = enabled;
            grpSource.Enabled = enabled;
            grpOutput.Enabled = enabled;
            grpStyling.Enabled = enabled;
        }

        #endregion

        #region Helper Methods & Grid Population

        public void SetTableList(List<CadTableInfoItem> tables)
        {
            _currentTablesList.Clear();
            if (tables != null) _currentTablesList.AddRange(tables);
            PopulateGrid();
        }

        private void PopulateGrid()
        {
            dgvTables.Rows.Clear();
            int stt = 1;
            foreach (var item in _currentTablesList)
            {
                int rowIndex = dgvTables.Rows.Add();
                var row = dgvTables.Rows[rowIndex];

                row.Cells["colCheck"].Value = item.IsSelected;
                row.Cells["colIndex"].Value = stt++;
                row.Cells["colType"].Value = item.TableType;
                row.Cells["colSpace"].Value = item.SpaceName;
                row.Cells["colSize"].Value = $"{item.RowCount} x {item.ColumnCount}";
                row.Cells["colStyle"].Value = string.IsNullOrEmpty(item.TableStyleName) ? "-" : item.TableStyleName;
                row.Cells["colTitle"].Value = string.IsNullOrEmpty(item.Title) ? $"(Bảng {item.Handle})" : item.Title;
            }

            UpdateTableCountLabel();
        }

        private void UpdateTableCountLabel()
        {
            int total = _currentTablesList.Count;
            int selected = _currentTablesList.Count(t => t.IsSelected);
            lblTableCount.Text = $"Đã phát hiện: {total} bảng ({selected} được chọn)";
        }

        #endregion

        #region Persistent State Implementation

        private void SaveCurrentSettings()
        {
            try
            {
                string path = txtFilePath.Text.Trim();
                if (!string.IsNullOrEmpty(path))
                {
                    _lastExportDirectory = Path.GetDirectoryName(path) ?? _lastExportDirectory;
                    _lastFileName = Path.GetFileName(path);
                }

                _lastIsXlsxFormat = radFormatXlsx.Checked;
                _lastExportSeparateSheets = radSeparateSheets.Checked;
                _lastBlankRows = numBlankRows.Value;

                if (radPickTables.Checked) _lastScopeIndex = 0;
                else if (radModelTables.Checked) _lastScopeIndex = 1;
                else if (radAllTables.Checked) _lastScopeIndex = 2;

                _lastAutoFit = chkAutoFitColumns.Checked;
                _lastAddBorders = chkAddBorders.Checked;
                _lastKeepMerged = chkKeepMergedCells.Checked;
                _lastFormatHeader = chkFormatHeader.Checked;
                _lastParseNumbers = chkParseNumbers.Checked;
                _lastCleanMText = chkCleanMText.Checked;
                _lastOpenAfterExport = chkOpenAfterExport.Checked;

                _lastFormSize = this.Size;
            }
            catch { }
        }

        private void RestoreLastSettings()
        {
            try
            {
                string defaultName = string.IsNullOrEmpty(_lastFileName) ? "Bang_ThongKe_CAD.xlsx" : _lastFileName;
                string dir = Directory.Exists(_lastExportDirectory) ? _lastExportDirectory : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                txtFilePath.Text = Path.Combine(dir, defaultName);

                radFormatXlsx.Checked = _lastIsXlsxFormat;
                radFormatCsv.Checked = !_lastIsXlsxFormat;

                radSeparateSheets.Checked = _lastExportSeparateSheets;
                radSingleSheet.Checked = !_lastExportSeparateSheets;
                numBlankRows.Value = Math.Max(1, Math.Min(10, _lastBlankRows));
                numBlankRows.Enabled = radSingleSheet.Checked;

                switch (_lastScopeIndex)
                {
                    case 1: radModelTables.Checked = true; break;
                    case 2: radAllTables.Checked = true; break;
                    default: radPickTables.Checked = true; break;
                }

                chkAutoFitColumns.Checked = _lastAutoFit;
                chkAddBorders.Checked = _lastAddBorders;
                chkKeepMergedCells.Checked = _lastKeepMerged;
                chkFormatHeader.Checked = _lastFormatHeader;
                chkParseNumbers.Checked = _lastParseNumbers;
                chkCleanMText.Checked = _lastCleanMText;
                chkOpenAfterExport.Checked = _lastOpenAfterExport;
            }
            catch { }
        }

        private void XuatBangCadRaExcelForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            SaveCurrentSettings();
        }

        #endregion
    }
}

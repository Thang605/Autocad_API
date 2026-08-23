// (C) Copyright 2026 by T27
// Form giao diện chuẩn hoá đối tượng Bảng (Table & TableStyle t27)
//
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsButton = System.Windows.Forms.Button;
using WinFormsTextBox = System.Windows.Forms.TextBox;
using WinFormsCheckBox = System.Windows.Forms.CheckBox;
using WinFormsRadioButton = System.Windows.Forms.RadioButton;
using WinFormsGroupBox = System.Windows.Forms.GroupBox;
using WinFormsComboBox = System.Windows.Forms.ComboBox;
using DrawingFont = System.Drawing.Font;
using DrawingColor = System.Drawing.Color;

namespace Civil3DCsharp
{
    /// <summary>
    /// Cấu hình chuẩn hoá bảng
    /// </summary>
    public class TableStandardizeConfig
    {
        public string TableStyleName { get; set; } = "t27";
        public string TextStyleName { get; set; } = "Standard";
        public double TitleHeight { get; set; } = 4.0;
        public double HeaderHeight { get; set; } = 3.0;
        public double DataHeight { get; set; } = 2.0;
        public double CellMargin { get; set; } = 1.0;
        public CellAlignment DataAlignment { get; set; } = CellAlignment.MiddleCenter;
        public CellAlignment HeaderAlignment { get; set; } = CellAlignment.MiddleCenter;
        public bool ApplyToAll { get; set; } = true;
        public bool CreateOrUpdateStyle { get; set; } = true;
        public bool SetCurrentStyle { get; set; } = true;
        public bool StandardizeCells { get; set; } = true;
        public bool RecalculateRowHeight { get; set; } = true;
    }

    /// <summary>
    /// Giao diện Form Chuẩn Hoá Đối Tượng Bảng
    /// </summary>
    public class ChuanHoaBangForm : Form
    {
        #region Persistent State (Ghi nhớ giá trị)

        private static string _lastTableStyleName = "t27";
        private static string _lastTextStyleName = "Standard";
        private static decimal _lastTitleHeight = 4.0m;
        private static decimal _lastHeaderHeight = 3.0m;
        private static decimal _lastDataHeight = 2.0m;
        private static decimal _lastCellMargin = 1.0m;
        private static int _lastDataAlignIndex = 0; // MiddleCenter
        private static int _lastHeaderAlignIndex = 0; // MiddleCenter
        private static bool _lastApplyToAll = true;
        private static bool _lastCreateOrUpdateStyle = true;
        private static bool _lastSetCurrentStyle = true;
        private static bool _lastStandardizeCells = true;
        private static bool _lastRecalculateRowHeight = true;
        private static Size _lastFormSize = new Size(620, 680);
        private static List<ObjectId> _lastSelectedTableIds = new List<ObjectId>();

        #endregion

        #region Callbacks tương tác AutoCAD

        public Func<List<ObjectId>>? OnSelectTablesOnScreen { get; set; }
        public Func<int>? OnScanTablesCount { get; set; }
        public Action<TableStandardizeConfig, List<ObjectId>, Action<string>>? OnExecuteStandardize { get; set; }
        public Func<List<string>>? OnGetAvailableTextStyles { get; set; }

        #endregion

        #region UI Controls

        private Panel pnlHeader = null!;
        private WinFormsLabel lblHeaderTitle = null!;
        private WinFormsLabel lblHeaderSubtitle = null!;

        private TabControl tabControlMain = null!;
        private TabPage tabTable = null!;
        private TabPage tabAbout = null!;

        // Tab Table - Group 1: Style parameters
        private WinFormsGroupBox grpStyleParams = null!;
        private WinFormsLabel lblTableStyleName = null!;
        private WinFormsTextBox txtTableStyleName = null!;
        private WinFormsLabel lblTextStyle = null!;
        private WinFormsComboBox cmbTextStyle = null!;
        private WinFormsButton btnRefreshTextStyles = null!;

        private WinFormsLabel lblTitleHeight = null!;
        private NumericUpDown numTitleHeight = null!;
        private WinFormsLabel lblHeaderHeight = null!;
        private NumericUpDown numHeaderHeight = null!;
        private WinFormsLabel lblDataHeight = null!;
        private NumericUpDown numDataHeight = null!;

        private WinFormsLabel lblMargin = null!;
        private NumericUpDown numCellMargin = null!;
        private WinFormsLabel lblDataAlign = null!;
        private WinFormsComboBox cmbDataAlign = null!;
        private WinFormsLabel lblHeaderAlign = null!;
        private WinFormsComboBox cmbHeaderAlign = null!;

        // Tab Table - Group 2: Scope & Options
        private WinFormsGroupBox grpScope = null!;
        private WinFormsRadioButton radScopeAll = null!;
        private WinFormsRadioButton radScopeSelection = null!;
        private WinFormsButton btnPickTables = null!;
        private WinFormsLabel lblSelectedCount = null!;

        private WinFormsCheckBox chkCreateUpdateStyle = null!;
        private WinFormsCheckBox chkSetCurrentStyle = null!;
        private WinFormsCheckBox chkStandardizeCells = null!;
        private WinFormsCheckBox chkRecalculateRowHeight = null!;

        // Tab Table - Group 3: Log / Results
        private WinFormsGroupBox grpLog = null!;
        private WinFormsTextBox txtLog = null!;

        // Bottom Controls
        private Panel pnlBottom = null!;
        private WinFormsButton btnScan = null!;
        private WinFormsButton btnExecute = null!;
        private WinFormsButton btnClose = null!;

        // Tab About controls
        private WinFormsLabel lblAboutTitle = null!;
        private WinFormsTextBox txtAboutInfo = null!;

        #endregion

        private List<ObjectId> _currentSelectedTableIds = new List<ObjectId>();
        private List<string> _availableTextStyles = new List<string>();

        public ChuanHoaBangForm(List<string> textStyles, int initialTableCount = 0)
        {
            _availableTextStyles = textStyles ?? new List<string>();
            InitializeComponent();
            RestoreLastSettings();
            UpdateTotalTableCountDisplay(initialTableCount);

            this.FormClosing += (s, e) => SaveCurrentSettings();
        }

        private void InitializeComponent()
        {
            var fontRegular = new DrawingFont("Segoe UI", 9.0F, FontStyle.Regular);
            var fontBold = new DrawingFont("Segoe UI", 9.0F, FontStyle.Bold);
            var fontHeaderTitle = new DrawingFont("Segoe UI", 12.0F, FontStyle.Bold);
            var fontHeaderSub = new DrawingFont("Segoe UI", 8.5F, FontStyle.Regular);
            var fontLog = new DrawingFont("Consolas", 8.5F, FontStyle.Regular);

            this.SuspendLayout();

            // Form Properties
            this.Text = "Chuẩn Hoá Đối Tượng Bản Vẽ - Tiêu Chuẩn T27";
            this.Size = _lastFormSize.Width > 400 ? _lastFormSize : new Size(620, 680);
            this.MinimumSize = new Size(580, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = fontRegular;
            this.BackColor = DrawingColor.FromArgb(245, 247, 250);

            // ================= HEADER PANEL =================
            pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 65,
                BackColor = DrawingColor.FromArgb(24, 43, 73),
                Padding = new Padding(15, 8, 15, 8)
            };

            lblHeaderTitle = new WinFormsLabel
            {
                Text = "📊 CHUẨN HOÁ ĐỐI TƯỢNG BẢNG (TABLE STANDARDIZATION)",
                Font = fontHeaderTitle,
                ForeColor = DrawingColor.White,
                AutoSize = true,
                Location = new Point(15, 10)
            };

            lblHeaderSubtitle = new WinFormsLabel
            {
                Text = "Tạo / Cập nhật Table Style 't27' (Text Standard, Tên=4, Tiêu đề=3, Data=2, Margin=1) & Đồng bộ bản vẽ",
                Font = fontHeaderSub,
                ForeColor = DrawingColor.FromArgb(200, 215, 235),
                AutoSize = true,
                Location = new Point(16, 35)
            };

            pnlHeader.Controls.Add(lblHeaderTitle);
            pnlHeader.Controls.Add(lblHeaderSubtitle);

            // ================= BOTTOM PANEL =================
            pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 55,
                BackColor = DrawingColor.FromArgb(235, 239, 245),
                Padding = new Padding(15, 10, 15, 10)
            };

            btnScan = new WinFormsButton
            {
                Text = "🔍 Quét lại bản vẽ",
                Size = new Size(130, 34),
                Location = new Point(15, 10),
                Font = fontRegular,
                UseVisualStyleBackColor = true,
                Cursor = Cursors.Hand
            };
            btnScan.Click += BtnScan_Click;

            btnExecute = new WinFormsButton
            {
                Text = "▶️ Thực Hiện Chuẩn Hoá",
                Size = new Size(190, 34),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(pnlBottom.ClientSize.Width - 300, 10),
                Font = fontBold,
                BackColor = DrawingColor.FromArgb(0, 120, 215),
                ForeColor = DrawingColor.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnExecute.FlatAppearance.BorderSize = 0;
            btnExecute.Click += BtnExecute_Click;

            btnClose = new WinFormsButton
            {
                Text = "Đóng",
                Size = new Size(85, 34),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(pnlBottom.ClientSize.Width - 95, 10),
                Font = fontRegular,
                UseVisualStyleBackColor = true,
                Cursor = Cursors.Hand
            };
            btnClose.Click += (s, e) => this.Close();

            pnlBottom.Controls.Add(btnScan);
            pnlBottom.Controls.Add(btnExecute);
            pnlBottom.Controls.Add(btnClose);

            // ================= TAB CONTROL =================
            tabControlMain = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = fontRegular,
                Padding = new Point(12, 6)
            };

            // ----------------- TAB 1: BẢNG (TABLE) -----------------
            tabTable = new TabPage
            {
                Text = "  📊 Chuẩn Hoá Bảng (Table)  ",
                BackColor = DrawingColor.FromArgb(245, 247, 250),
                AutoScroll = true,
                Padding = new Padding(12)
            };

            // Group 1: Style Parameters
            grpStyleParams = new WinFormsGroupBox
            {
                Text = "Thiết Lập Thông Số Style Bảng (TableStyle 't27')",
                Font = fontBold,
                Location = new Point(10, 10),
                Size = new Size(575, 160),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                ForeColor = DrawingColor.FromArgb(20, 50, 90)
            };

            lblTableStyleName = new WinFormsLabel
            {
                Text = "Tên Table Style:",
                Font = fontRegular,
                ForeColor = DrawingColor.Black,
                Location = new Point(15, 25),
                Size = new Size(110, 20)
            };

            txtTableStyleName = new WinFormsTextBox
            {
                Text = "t27",
                Font = fontBold,
                Location = new Point(130, 22),
                Size = new Size(120, 24)
            };

            lblTextStyle = new WinFormsLabel
            {
                Text = "Text Style áp dụng:",
                Font = fontRegular,
                ForeColor = DrawingColor.Black,
                Location = new Point(270, 25),
                Size = new Size(125, 20)
            };

            cmbTextStyle = new WinFormsComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = fontRegular,
                Location = new Point(400, 22),
                Size = new Size(130, 24)
            };

            btnRefreshTextStyles = new WinFormsButton
            {
                Text = "🔄",
                Size = new Size(30, 24),
                Location = new Point(533, 21),
                Font = fontRegular,
                Cursor = Cursors.Hand
            };
            btnRefreshTextStyles.Click += (s, e) => ReloadTextStyles();

            // Row 2: Heights
            lblTitleHeight = new WinFormsLabel
            {
                Text = "Tên bảng (Title):",
                Font = fontRegular,
                ForeColor = DrawingColor.Black,
                Location = new Point(15, 58),
                Size = new Size(110, 20)
            };

            numTitleHeight = new NumericUpDown
            {
                DecimalPlaces = 2,
                Increment = 0.5m,
                Minimum = 0.1m,
                Maximum = 100.0m,
                Value = 4.0m,
                Font = fontRegular,
                Location = new Point(130, 55),
                Size = new Size(70, 24)
            };

            lblHeaderHeight = new WinFormsLabel
            {
                Text = "Tiêu đề (Header):",
                Font = fontRegular,
                ForeColor = DrawingColor.Black,
                Location = new Point(215, 58),
                Size = new Size(110, 20)
            };

            numHeaderHeight = new NumericUpDown
            {
                DecimalPlaces = 2,
                Increment = 0.5m,
                Minimum = 0.1m,
                Maximum = 100.0m,
                Value = 3.0m,
                Font = fontRegular,
                Location = new Point(330, 55),
                Size = new Size(70, 24)
            };

            lblDataHeight = new WinFormsLabel
            {
                Text = "Dữ liệu (Data):",
                Font = fontRegular,
                ForeColor = DrawingColor.Black,
                Location = new Point(415, 58),
                Size = new Size(90, 20)
            };

            numDataHeight = new NumericUpDown
            {
                DecimalPlaces = 2,
                Increment = 0.5m,
                Minimum = 0.1m,
                Maximum = 100.0m,
                Value = 2.0m,
                Font = fontRegular,
                Location = new Point(500, 55),
                Size = new Size(62, 24)
            };

            // Row 3: Margins & Alignment
            lblMargin = new WinFormsLabel
            {
                Text = "Khoảng căn lề (Margin):",
                Font = fontRegular,
                ForeColor = DrawingColor.Black,
                Location = new Point(15, 92),
                Size = new Size(140, 20)
            };

            numCellMargin = new NumericUpDown
            {
                DecimalPlaces = 2,
                Increment = 0.2m,
                Minimum = 0.0m,
                Maximum = 50.0m,
                Value = 1.0m,
                Font = fontRegular,
                Location = new Point(160, 89),
                Size = new Size(65, 24)
            };

            lblDataAlign = new WinFormsLabel
            {
                Text = "Căn lề Dữ liệu:",
                Font = fontRegular,
                ForeColor = DrawingColor.Black,
                Location = new Point(245, 92),
                Size = new Size(95, 20)
            };

            cmbDataAlign = new WinFormsComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = fontRegular,
                Location = new Point(345, 89),
                Size = new Size(115, 24)
            };
            PopulateAlignmentOptions(cmbDataAlign);

            lblHeaderAlign = new WinFormsLabel
            {
                Text = "Căn lề Tiêu đề:",
                Font = fontRegular,
                ForeColor = DrawingColor.Black,
                Location = new Point(15, 125),
                Size = new Size(140, 20)
            };

            cmbHeaderAlign = new WinFormsComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = fontRegular,
                Location = new Point(160, 122),
                Size = new Size(115, 24)
            };
            PopulateAlignmentOptions(cmbHeaderAlign);

            grpStyleParams.Controls.AddRange(new Control[] {
                lblTableStyleName, txtTableStyleName,
                lblTextStyle, cmbTextStyle, btnRefreshTextStyles,
                lblTitleHeight, numTitleHeight,
                lblHeaderHeight, numHeaderHeight,
                lblDataHeight, numDataHeight,
                lblMargin, numCellMargin,
                lblDataAlign, cmbDataAlign,
                lblHeaderAlign, cmbHeaderAlign
            });

            // Group 2: Scope & Execution Options
            grpScope = new WinFormsGroupBox
            {
                Text = "Phạm Vi Áp Dụng & Tùy Chọn",
                Font = fontBold,
                Location = new Point(10, 178),
                Size = new Size(575, 140),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                ForeColor = DrawingColor.FromArgb(20, 50, 90)
            };

            radScopeAll = new WinFormsRadioButton
            {
                Text = "Tất cả các bảng trong bản vẽ (ModelSpace & Layouts)",
                Font = fontRegular,
                ForeColor = DrawingColor.Black,
                Location = new Point(15, 22),
                Size = new Size(340, 22),
                Checked = true
            };
            radScopeAll.CheckedChanged += (s, e) => UpdateScopeUI();

            radScopeSelection = new WinFormsRadioButton
            {
                Text = "Chỉ áp dụng cho các bảng được chọn",
                Font = fontRegular,
                ForeColor = DrawingColor.Black,
                Location = new Point(15, 48),
                Size = new Size(240, 22)
            };
            radScopeSelection.CheckedChanged += (s, e) => UpdateScopeUI();

            btnPickTables = new WinFormsButton
            {
                Text = "🖱️ Chọn bảng trên bản vẽ...",
                Size = new Size(185, 26),
                Location = new Point(260, 46),
                Font = fontRegular,
                Enabled = false,
                Cursor = Cursors.Hand
            };
            btnPickTables.Click += BtnPickTables_Click;

            lblSelectedCount = new WinFormsLabel
            {
                Text = "(Đã chọn: 0 bảng)",
                Font = fontRegular,
                ForeColor = DrawingColor.DarkBlue,
                Location = new Point(450, 50),
                Size = new Size(115, 20)
            };

            chkCreateUpdateStyle = new WinFormsCheckBox
            {
                Text = "Tạo mới / Cập nhật Table Style 't27' vào Database",
                Font = fontRegular,
                ForeColor = DrawingColor.Black,
                Location = new Point(15, 78),
                Size = new Size(310, 22),
                Checked = true
            };

            chkSetCurrentStyle = new WinFormsCheckBox
            {
                Text = "Đặt 't27' làm Table Style mặc định hiện hành",
                Font = fontRegular,
                ForeColor = DrawingColor.Black,
                Location = new Point(330, 78),
                Size = new Size(240, 22),
                Checked = true
            };

            chkStandardizeCells = new WinFormsCheckBox
            {
                Text = "Chuẩn hoá định dạng từng Cell (Text Style, Chiều cao chữ, Margin, Căn lề)",
                Font = fontRegular,
                ForeColor = DrawingColor.Black,
                Location = new Point(15, 105),
                Size = new Size(360, 22),
                Checked = true
            };

            chkRecalculateRowHeight = new WinFormsCheckBox
            {
                Text = "Tối ưu chiều cao dòng (Row Height)",
                Font = fontRegular,
                ForeColor = DrawingColor.Black,
                Location = new Point(380, 105),
                Size = new Size(190, 22),
                Checked = true
            };

            grpScope.Controls.AddRange(new Control[] {
                radScopeAll, radScopeSelection, btnPickTables, lblSelectedCount,
                chkCreateUpdateStyle, chkSetCurrentStyle,
                chkStandardizeCells, chkRecalculateRowHeight
            });

            // Group 3: Log & Results
            grpLog = new WinFormsGroupBox
            {
                Text = "Nhật Ký & Kết Quả Chuẩn Hoá",
                Font = fontBold,
                Location = new Point(10, 325),
                Size = new Size(575, 185),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                ForeColor = DrawingColor.FromArgb(20, 50, 90)
            };

            txtLog = new WinFormsTextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                Font = fontLog,
                BackColor = DrawingColor.White,
                ForeColor = DrawingColor.FromArgb(30, 30, 30),
                Dock = DockStyle.Fill,
                Padding = new Padding(5)
            };

            grpLog.Controls.Add(txtLog);

            tabTable.Controls.Add(grpStyleParams);
            tabTable.Controls.Add(grpScope);
            tabTable.Controls.Add(grpLog);

            // ----------------- TAB 2: ABOUT / EXTENSION -----------------
            tabAbout = new TabPage
            {
                Text = "  ℹ️ Giới Thiệu & Mở Rộng  ",
                BackColor = DrawingColor.White,
                Padding = new Padding(15)
            };

            lblAboutTitle = new WinFormsLabel
            {
                Text = "HỆ THỐNG CHUẨN HOÁ ĐỐI TƯỢNG BẢN VẼ T27",
                Font = fontHeaderTitle,
                ForeColor = DrawingColor.FromArgb(24, 43, 73),
                Location = new Point(15, 15),
                Size = new Size(540, 30)
            };

            txtAboutInfo = new WinFormsTextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = fontRegular,
                BackColor = DrawingColor.White,
                Location = new Point(15, 55),
                Size = new Size(540, 440),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Text =
@"QUY CHUẨN THIẾT KẾ & ĐỊNH DẠNG ĐỐI TƯỢNG T27:

1. ĐỐI TƯỢNG BẢNG (TABLE & TABLESTYLE):
- Tên Style chuẩn: t27
- Kiểu chữ (Text Style): Standard
- Chiều cao chữ Tên bảng (Title): 4.0 mm
- Chiều cao chữ Tiêu đề cột (Header): 3.0 mm
- Chiều cao chữ Dữ liệu ô (Data): 2.0 mm
- Khoảng cách căn lề ô (Cell Margins): 1.0 mm (Top, Bottom, Left, Right)
- Căn lề chuẩn: Middle Center

2. CÁC ĐỐI TƯỢNG ĐANG PHÁT TRIỂN TIẾP THEO:
- Text / MText: Standardize Font, Text Height theo tỷ lệ bản vẽ.
- Dimension Style: T27 DimStyle 1/100, 1/200, 1/500, 1/1000.
- Multileader (MLeader): T27 Leader Style chuẩn.
- Layer & Linetype: Hệ thống lớp chuẩn theo quy phạm giao thông, hạ tầng kỹ thuật.

LỆNH TẮT:
- AT_ChuanHoaBang
- AT_ChuanHoaDoiTuong
- CH_BANG
- CHUANHOABANG"
            };

            tabAbout.Controls.Add(lblAboutTitle);
            tabAbout.Controls.Add(txtAboutInfo);

            tabControlMain.TabPages.Add(tabTable);
            tabControlMain.TabPages.Add(tabAbout);

            // Add main panels
            this.Controls.Add(tabControlMain);
            this.Controls.Add(pnlBottom);
            this.Controls.Add(pnlHeader);

            this.ResumeLayout(false);
        }

        #region Helpers & UI Logic

        private void PopulateAlignmentOptions(WinFormsComboBox cmb)
        {
            cmb.Items.Clear();
            cmb.Items.Add("Middle Center");
            cmb.Items.Add("Middle Left");
            cmb.Items.Add("Middle Right");
            cmb.Items.Add("Top Center");
            cmb.Items.Add("Top Left");
            cmb.Items.Add("Top Right");
            cmb.Items.Add("Bottom Center");
            cmb.Items.Add("Bottom Left");
            cmb.Items.Add("Bottom Right");
            if (cmb.Items.Count > 0) cmb.SelectedIndex = 0;
        }

        private CellAlignment GetCellAlignment(int index)
        {
            return index switch
            {
                0 => CellAlignment.MiddleCenter,
                1 => CellAlignment.MiddleLeft,
                2 => CellAlignment.MiddleRight,
                3 => CellAlignment.TopCenter,
                4 => CellAlignment.TopLeft,
                5 => CellAlignment.TopRight,
                6 => CellAlignment.BottomCenter,
                7 => CellAlignment.BottomLeft,
                8 => CellAlignment.BottomRight,
                _ => CellAlignment.MiddleCenter
            };
        }

        private void ReloadTextStyles()
        {
            if (OnGetAvailableTextStyles != null)
            {
                _availableTextStyles = OnGetAvailableTextStyles();
            }

            string currentSelected = cmbTextStyle.SelectedItem?.ToString() ?? "Standard";
            cmbTextStyle.Items.Clear();

            foreach (var style in _availableTextStyles)
            {
                cmbTextStyle.Items.Add(style);
            }

            if (!cmbTextStyle.Items.Contains("Standard"))
            {
                cmbTextStyle.Items.Insert(0, "Standard");
            }

            int foundIndex = cmbTextStyle.FindStringExact(currentSelected);
            if (foundIndex >= 0)
            {
                cmbTextStyle.SelectedIndex = foundIndex;
            }
            else
            {
                int stdIndex = cmbTextStyle.FindStringExact("Standard");
                cmbTextStyle.SelectedIndex = stdIndex >= 0 ? stdIndex : 0;
            }
        }

        private void UpdateScopeUI()
        {
            bool isSelection = radScopeSelection.Checked;
            btnPickTables.Enabled = isSelection;
            lblSelectedCount.Text = $"(Đã chọn: {_currentSelectedTableIds.Count} bảng)";
        }

        public void UpdateTotalTableCountDisplay(int count)
        {
            AppendLog($"🔍 Phát hiện {count} đối tượng Bảng (Table) trong bản vẽ hiện hành.");
        }

        public void AppendLog(string message)
        {
            if (txtLog.InvokeRequired)
            {
                txtLog.Invoke(new Action(() => AppendLog(message)));
                return;
            }

            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            txtLog.AppendText($"[{timestamp}] {message}{Environment.NewLine}");
            txtLog.SelectionStart = txtLog.Text.Length;
            txtLog.ScrollToCaret();
        }

        #endregion

        #region Events

        private void BtnPickTables_Click(object? sender, EventArgs e)
        {
            if (OnSelectTablesOnScreen == null) return;

            try
            {
                var pickedIds = OnSelectTablesOnScreen();
                if (pickedIds != null && pickedIds.Count > 0)
                {
                    _currentSelectedTableIds = pickedIds;
                    lblSelectedCount.Text = $"(Đã chọn: {_currentSelectedTableIds.Count} bảng)";
                    AppendLog($"✅ Đã chọn {_currentSelectedTableIds.Count} bảng từ bản vẽ.");
                }
                else
                {
                    AppendLog("⚠ Không có bảng nào được chọn thêm.");
                }
            }
            catch (System.Exception ex)
            {
                AppendLog($"❌ Lỗi chọn bảng: {ex.Message}");
            }
        }

        private void BtnScan_Click(object? sender, EventArgs e)
        {
            if (OnScanTablesCount != null)
            {
                try
                {
                    int count = OnScanTablesCount();
                    UpdateTotalTableCountDisplay(count);
                }
                catch (System.Exception ex)
                {
                    AppendLog($"❌ Lỗi quét bản vẽ: {ex.Message}");
                }
            }
            ReloadTextStyles();
        }

        private void BtnExecute_Click(object? sender, EventArgs e)
        {
            string tableName = txtTableStyleName.Text.Trim();
            if (string.IsNullOrEmpty(tableName))
            {
                MessageBox.Show("Vui lòng nhập tên Table Style!", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtTableStyleName.Focus();
                return;
            }

            if (radScopeSelection.Checked && _currentSelectedTableIds.Count == 0)
            {
                MessageBox.Show("Bạn đang chọn phạm vi 'Chỉ áp dụng cho các bảng được chọn' nhưng chưa chọn bảng nào.\nVui lòng bấm 'Chọn bảng trên bản vẽ' hoặc chuyển sang 'Tất cả các bảng trong bản vẽ'.",
                    "Chưa chọn bảng", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            TableStandardizeConfig config = new TableStandardizeConfig
            {
                TableStyleName = tableName,
                TextStyleName = cmbTextStyle.SelectedItem?.ToString() ?? "Standard",
                TitleHeight = (double)numTitleHeight.Value,
                HeaderHeight = (double)numHeaderHeight.Value,
                DataHeight = (double)numDataHeight.Value,
                CellMargin = (double)numCellMargin.Value,
                DataAlignment = GetCellAlignment(cmbDataAlign.SelectedIndex),
                HeaderAlignment = GetCellAlignment(cmbHeaderAlign.SelectedIndex),
                ApplyToAll = radScopeAll.Checked,
                CreateOrUpdateStyle = chkCreateUpdateStyle.Checked,
                SetCurrentStyle = chkSetCurrentStyle.Checked,
                StandardizeCells = chkStandardizeCells.Checked,
                RecalculateRowHeight = chkRecalculateRowHeight.Checked
            };

            AppendLog("==================================================");
            AppendLog($"🚀 BẮT ĐẦU CHUẨN HOÁ: Style '{config.TableStyleName}', Text='{config.TextStyleName}'");
            AppendLog($"📏 Chiều cao chữ: Tên={config.TitleHeight}, Tiêu đề={config.HeaderHeight}, Data={config.DataHeight}, Margin={config.CellMargin}");

            btnExecute.Enabled = false;
            try
            {
                OnExecuteStandardize?.Invoke(config, _currentSelectedTableIds, msg => AppendLog(msg));
            }
            finally
            {
                btnExecute.Enabled = true;
            }
        }

        #endregion

        #region Save / Restore Settings

        private void SaveCurrentSettings()
        {
            _lastTableStyleName = txtTableStyleName.Text.Trim();
            _lastTextStyleName = cmbTextStyle.SelectedItem?.ToString() ?? "Standard";
            _lastTitleHeight = numTitleHeight.Value;
            _lastHeaderHeight = numHeaderHeight.Value;
            _lastDataHeight = numDataHeight.Value;
            _lastCellMargin = numCellMargin.Value;
            _lastDataAlignIndex = cmbDataAlign.SelectedIndex;
            _lastHeaderAlignIndex = cmbHeaderAlign.SelectedIndex;
            _lastApplyToAll = radScopeAll.Checked;
            _lastCreateOrUpdateStyle = chkCreateUpdateStyle.Checked;
            _lastSetCurrentStyle = chkSetCurrentStyle.Checked;
            _lastStandardizeCells = chkStandardizeCells.Checked;
            _lastRecalculateRowHeight = chkRecalculateRowHeight.Checked;
            _lastFormSize = this.Size;
            _lastSelectedTableIds = new List<ObjectId>(_currentSelectedTableIds);
        }

        private void RestoreLastSettings()
        {
            ReloadTextStyles();

            txtTableStyleName.Text = string.IsNullOrEmpty(_lastTableStyleName) ? "t27" : _lastTableStyleName;

            int styleIdx = cmbTextStyle.FindStringExact(_lastTextStyleName);
            if (styleIdx >= 0) cmbTextStyle.SelectedIndex = styleIdx;
            else
            {
                int stdIdx = cmbTextStyle.FindStringExact("Standard");
                cmbTextStyle.SelectedIndex = stdIdx >= 0 ? stdIdx : 0;
            }

            if (_lastTitleHeight >= numTitleHeight.Minimum && _lastTitleHeight <= numTitleHeight.Maximum)
                numTitleHeight.Value = _lastTitleHeight;

            if (_lastHeaderHeight >= numHeaderHeight.Minimum && _lastHeaderHeight <= numHeaderHeight.Maximum)
                numHeaderHeight.Value = _lastHeaderHeight;

            if (_lastDataHeight >= numDataHeight.Minimum && _lastDataHeight <= numDataHeight.Maximum)
                numDataHeight.Value = _lastDataHeight;

            if (_lastCellMargin >= numCellMargin.Minimum && _lastCellMargin <= numCellMargin.Maximum)
                numCellMargin.Value = _lastCellMargin;

            if (_lastDataAlignIndex >= 0 && _lastDataAlignIndex < cmbDataAlign.Items.Count)
                cmbDataAlign.SelectedIndex = _lastDataAlignIndex;

            if (_lastHeaderAlignIndex >= 0 && _lastHeaderAlignIndex < cmbHeaderAlign.Items.Count)
                cmbHeaderAlign.SelectedIndex = _lastHeaderAlignIndex;

            radScopeAll.Checked = _lastApplyToAll;
            radScopeSelection.Checked = !_lastApplyToAll;
            chkCreateUpdateStyle.Checked = _lastCreateOrUpdateStyle;
            chkSetCurrentStyle.Checked = _lastSetCurrentStyle;
            chkStandardizeCells.Checked = _lastStandardizeCells;
            chkRecalculateRowHeight.Checked = _lastRecalculateRowHeight;

            // Khôi phục danh sách đã chọn nếu còn hợp lệ
            _currentSelectedTableIds = new List<ObjectId>();
            if (_lastSelectedTableIds != null && _lastSelectedTableIds.Count > 0)
            {
                foreach (var id in _lastSelectedTableIds)
                {
                    if (!id.IsNull && id.IsValid && !id.IsErased)
                    {
                        _currentSelectedTableIds.Add(id);
                    }
                }
            }

            UpdateScopeUI();
        }

        #endregion
    }
}

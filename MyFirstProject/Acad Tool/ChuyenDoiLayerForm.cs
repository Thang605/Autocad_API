// (C) Copyright 2026 by T27
// Form giao diện Chuyển Đổi Đối Tượng Sang Layer Khác (AT_Chuyen_Doi_Layer)
//
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using CadColor = Autodesk.AutoCAD.Colors.Color;
using DrawingColor = System.Drawing.Color;
using DrawingFont = System.Drawing.Font;
using WinFormsButton = System.Windows.Forms.Button;
using WinFormsCheckBox = System.Windows.Forms.CheckBox;
using WinFormsComboBox = System.Windows.Forms.ComboBox;
using WinFormsGroupBox = System.Windows.Forms.GroupBox;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsPanel = System.Windows.Forms.Panel;
using WinFormsRadioButton = System.Windows.Forms.RadioButton;
using WinFormsTextBox = System.Windows.Forms.TextBox;

namespace MyFirstProject.Acad_Tool
{
    /// <summary>
    /// Model dữ liệu ánh xạ từng Layer cũ sang Layer mới trong DataGridView
    /// </summary>
    public class LayerMappingItem
    {
        public const string NO_CHANGE_TEXT = "[-- Giữ nguyên --]";

        public ObjectId LayerId { get; set; } = ObjectId.Null;
        public string OldLayerName { get; set; } = "";
        public CadColor CadColor { get; set; } = CadColor.FromColorIndex(ColorMethod.ByAci, 7);
        public DrawingColor DrawingColor { get; set; } = DrawingColor.White;

        public int ObjectCountCurrentSpace { get; set; } = 0;
        public int ObjectCountModelSpace { get; set; } = 0;
        public int ObjectCountAllSpaces { get; set; } = 0;

        public bool IsLocked { get; set; } = false;
        public bool IsOff { get; set; } = false;
        public bool IsFrozen { get; set; } = false;

        public bool IsSelectedToConvert { get; set; } = false;
        public string NewLayerName { get; set; } = NO_CHANGE_TEXT;
        public string StatusText { get; set; } = "Giữ nguyên";
        public int LastConvertedCount { get; set; } = 0;

        public int GetCountForScope(int scopeIndex)
        {
            return scopeIndex switch
            {
                1 => ObjectCountModelSpace,
                2 => ObjectCountAllSpaces,
                _ => ObjectCountCurrentSpace
            };
        }
    }

    /// <summary>
    /// Form giao diện chuyên nghiệp cho lệnh chuyển tất cả đối tượng từ Layer cũ sang Layer mới
    /// </summary>
    public class ChuyenDoiLayerForm : Form
    {
        #region Persistent State (RULE 2)

        private static readonly Dictionary<string, string> _lastMapping = new(StringComparer.OrdinalIgnoreCase);
        private static int _lastScopeIndex = 2; // 0=CurrentSpace, 1=ModelSpace, 2=AllSpaces, 3=Window (Mặc định toàn bản vẽ)
        private static bool _lastHideEmpty = true;
        private static bool _lastOnlyChanged = false;
        private static bool _lastAutoUnlock = true;
        private static bool _lastPurgeOld = false;
        private static bool _lastIncludeBlocks = false;
        private static bool _lastCloseOnFinish = false;
        private static Size _lastFormSize = new Size(960, 700);
        private static Point _lastFormLocation = Point.Empty;

        #endregion

        #region Fields

        private readonly Document _doc;
        private readonly Database _db;
        private readonly Editor _ed;

        private List<LayerMappingItem> _allLayers = new();
        private List<LayerMappingItem> _displayLayers = new();
        private readonly List<string> _availableTargetLayers = new();
        private bool _isUpdatingUi = false;

        // UI Header
        private WinFormsLabel lblTitle = null!;
        private WinFormsLabel lblSubtitle = null!;

        // Quick Assign Toolbar
        private WinFormsPanel pnlQuickBar = null!;
        private WinFormsButton btnPickSample = null!;
        private WinFormsComboBox cboTargetQuick = null!;
        private WinFormsButton btnQuickAssign = null!;
        private WinFormsButton btnAddNewLayer = null!;
        private WinFormsButton btnResetAll = null!;

        // Filter & Search bar
        private WinFormsPanel pnlFilterBar = null!;
        private WinFormsTextBox txtSearch = null!;
        private WinFormsCheckBox chkHideEmpty = null!;
        private WinFormsCheckBox chkOnlyChanged = null!;
        private WinFormsButton btnSelectAll = null!;
        private WinFormsButton btnDeselectAll = null!;
        private WinFormsLabel lblStats = null!;

        // DataGridView
        private DataGridView dgvMapping = null!;
        private DataGridViewCheckBoxColumn colCheck = null!;
        private DataGridViewTextBoxColumn colColor = null!;
        private DataGridViewTextBoxColumn colOldLayer = null!;
        private DataGridViewTextBoxColumn colCount = null!;
        private DataGridViewComboBoxColumn colNewLayer = null!;
        private DataGridViewTextBoxColumn colStatus = null!;

        // Options GroupBox
        private WinFormsGroupBox grpOptions = null!;
        private WinFormsRadioButton rbScopeAllSpaces = null!;
        private WinFormsRadioButton rbScopeModelSpace = null!;
        private WinFormsRadioButton rbScopeCurrentSpace = null!;
        private WinFormsRadioButton rbScopeWindow = null!;
        private WinFormsCheckBox chkAutoUnlock = null!;
        private WinFormsCheckBox chkPurgeOld = null!;
        private WinFormsCheckBox chkIncludeBlocks = null!;
        private WinFormsCheckBox chkCloseOnFinish = null!;

        // Bottom Action Bar
        private WinFormsPanel pnlBottom = null!;
        private WinFormsButton btnExecute = null!;
        private WinFormsButton btnExportMapping = null!;
        private WinFormsButton btnImportMapping = null!;
        private WinFormsButton btnRefresh = null!;
        private WinFormsButton btnClose = null!;

        #endregion

        #region Constructor & Initialization

        public ChuyenDoiLayerForm(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _db = doc.Database;
            _ed = doc.Editor;

            InitializeComponent();
            RestoreLastSettings();
            LoadLayersData();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.Text = "Chuyển Đổi Đối Tượng Sang Layer Khác (AT_Chuyen_Doi_Layer)";
            this.Font = new DrawingFont("Segoe UI", 9F, FontStyle.Regular);
            this.Size = _lastFormSize;
            this.MinimumSize = new Size(880, 580);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = DrawingColor.FromArgb(248, 250, 252);
            this.ForeColor = DrawingColor.FromArgb(30, 41, 59);

            // ================= 1. HEADER PANEL =================
            var pnlHeader = new WinFormsPanel
            {
                Dock = DockStyle.Top,
                Height = 62,
                BackColor = DrawingColor.FromArgb(24, 32, 47),
                Padding = new Padding(16, 8, 16, 8)
            };

            lblTitle = new WinFormsLabel
            {
                Text = "🔄 CHUYỂN ĐỔI ĐỐI TƯỢNG SANG LAYER KHÁC",
                Location = new Point(14, 10),
                AutoSize = true,
                Font = new DrawingFont("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = DrawingColor.White
            };

            lblSubtitle = new WinFormsLabel
            {
                Text = "Bảng ánh xạ 2 cột: Layer cũ và Layer mới tương ứng trên cùng 1 dòng | Tự động chuyển toàn bộ đối tượng",
                Location = new Point(15, 34),
                AutoSize = true,
                Font = new DrawingFont("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = DrawingColor.FromArgb(148, 163, 184)
            };

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSubtitle);

            // ================= 2. QUICK ASSIGN TOOLBAR =================
            pnlQuickBar = new WinFormsPanel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = DrawingColor.FromArgb(241, 245, 249),
                Padding = new Padding(12, 6, 12, 6)
            };

            btnPickSample = new WinFormsButton
            {
                Text = "🎯 Pick mẫu trên CAD",
                Location = new Point(12, 7),
                Size = new Size(140, 30),
                Font = new DrawingFont("Segoe UI", 8.5F, FontStyle.Bold),
                BackColor = DrawingColor.White,
                ForeColor = DrawingColor.FromArgb(30, 41, 59),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnPickSample.FlatAppearance.BorderColor = DrawingColor.FromArgb(203, 213, 225);
            btnPickSample.Click += BtnPickSample_Click;

            var lblAssign = new WinFormsLabel
            {
                Text = "Gán Layer đích:",
                Location = new Point(165, 12),
                AutoSize = true,
                Font = new DrawingFont("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = DrawingColor.FromArgb(51, 65, 85)
            };

            cboTargetQuick = new WinFormsComboBox
            {
                Location = new Point(265, 8),
                Size = new Size(210, 28),
                DropDownStyle = ComboBoxStyle.DropDown,
                Font = new DrawingFont("Segoe UI", 9F, FontStyle.Regular)
            };

            btnQuickAssign = new WinFormsButton
            {
                Text = "⚡ Gán cho dòng chọn",
                Location = new Point(482, 7),
                Size = new Size(145, 30),
                Font = new DrawingFont("Segoe UI", 8.5F, FontStyle.Bold),
                BackColor = DrawingColor.FromArgb(14, 116, 144),
                ForeColor = DrawingColor.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnQuickAssign.FlatAppearance.BorderSize = 0;
            btnQuickAssign.Click += BtnQuickAssign_Click;

            btnAddNewLayer = new WinFormsButton
            {
                Text = "➕ Tạo Layer mới...",
                Location = new Point(634, 7),
                Size = new Size(130, 30),
                Font = new DrawingFont("Segoe UI", 8.5F, FontStyle.Regular),
                BackColor = DrawingColor.White,
                ForeColor = DrawingColor.FromArgb(15, 23, 42),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnAddNewLayer.FlatAppearance.BorderColor = DrawingColor.FromArgb(203, 213, 225);
            btnAddNewLayer.Click += BtnAddNewLayer_Click;

            btnResetAll = new WinFormsButton
            {
                Text = "🔄 Đặt lại tất cả",
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(818, 7),
                Size = new Size(115, 30),
                Font = new DrawingFont("Segoe UI", 8.5F, FontStyle.Regular),
                BackColor = DrawingColor.White,
                ForeColor = DrawingColor.FromArgb(185, 28, 28),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnResetAll.FlatAppearance.BorderColor = DrawingColor.FromArgb(254, 202, 202);
            btnResetAll.Click += BtnResetAll_Click;

            pnlQuickBar.Controls.Add(btnPickSample);
            pnlQuickBar.Controls.Add(lblAssign);
            pnlQuickBar.Controls.Add(cboTargetQuick);
            pnlQuickBar.Controls.Add(btnQuickAssign);
            pnlQuickBar.Controls.Add(btnAddNewLayer);
            pnlQuickBar.Controls.Add(btnResetAll);

            // ================= 3. FILTER & SEARCH BAR =================
            pnlFilterBar = new WinFormsPanel
            {
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = DrawingColor.White,
                Padding = new Padding(12, 6, 12, 6)
            };

            var lblSearch = new WinFormsLabel
            {
                Text = "🔍 Tìm:",
                Location = new Point(12, 10),
                AutoSize = true,
                Font = new DrawingFont("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = DrawingColor.FromArgb(100, 116, 139)
            };

            txtSearch = new WinFormsTextBox
            {
                Location = new Point(58, 7),
                Size = new Size(180, 24),
                Font = new DrawingFont("Segoe UI", 9F, FontStyle.Regular)
            };
            txtSearch.TextChanged += (s, e) => ApplyFilterAndRefreshGrid();

            chkHideEmpty = new WinFormsCheckBox
            {
                Text = "Ẩn Layer trống (0 đ.tượng)",
                Location = new Point(250, 8),
                AutoSize = true,
                Font = new DrawingFont("Segoe UI", 8.5F, FontStyle.Regular),
                Checked = _lastHideEmpty
            };
            chkHideEmpty.CheckedChanged += (s, e) => ApplyFilterAndRefreshGrid();

            chkOnlyChanged = new WinFormsCheckBox
            {
                Text = "Chỉ hiện dòng có đổi Layer",
                Location = new Point(425, 8),
                AutoSize = true,
                Font = new DrawingFont("Segoe UI", 8.5F, FontStyle.Regular),
                Checked = _lastOnlyChanged
            };
            chkOnlyChanged.CheckedChanged += (s, e) => ApplyFilterAndRefreshGrid();

            btnSelectAll = new WinFormsButton
            {
                Text = "✔ Chọn tất cả",
                Location = new Point(605, 6),
                Size = new Size(95, 26),
                Font = new DrawingFont("Segoe UI", 8F, FontStyle.Regular),
                BackColor = DrawingColor.FromArgb(248, 250, 252),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnSelectAll.FlatAppearance.BorderColor = DrawingColor.FromArgb(226, 232, 240);
            btnSelectAll.Click += (s, e) => ToggleSelectAllVisible(true);

            btnDeselectAll = new WinFormsButton
            {
                Text = "✖ Bỏ chọn",
                Location = new Point(706, 6),
                Size = new Size(80, 26),
                Font = new DrawingFont("Segoe UI", 8F, FontStyle.Regular),
                BackColor = DrawingColor.FromArgb(248, 250, 252),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnDeselectAll.FlatAppearance.BorderColor = DrawingColor.FromArgb(226, 232, 240);
            btnDeselectAll.Click += (s, e) => ToggleSelectAllVisible(false);

            lblStats = new WinFormsLabel
            {
                Text = "Đang tải dữ liệu...",
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(795, 10),
                AutoSize = true,
                Font = new DrawingFont("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = DrawingColor.FromArgb(71, 85, 105)
            };

            pnlFilterBar.Controls.Add(lblSearch);
            pnlFilterBar.Controls.Add(txtSearch);
            pnlFilterBar.Controls.Add(chkHideEmpty);
            pnlFilterBar.Controls.Add(chkOnlyChanged);
            pnlFilterBar.Controls.Add(btnSelectAll);
            pnlFilterBar.Controls.Add(btnDeselectAll);
            pnlFilterBar.Controls.Add(lblStats);

            // ================= 4. DATAGRIDVIEW =================
            dgvMapping = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = DrawingColor.White,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                EnableHeadersVisualStyles = false,
                EditMode = DataGridViewEditMode.EditOnEnter,
                RowTemplate = { Height = 28 }
            };

            dgvMapping.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = DrawingColor.FromArgb(241, 245, 249),
                ForeColor = DrawingColor.FromArgb(51, 65, 85),
                Font = new DrawingFont("Segoe UI", 9F, FontStyle.Bold),
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                Padding = new Padding(4)
            };

            colCheck = new DataGridViewCheckBoxColumn
            {
                Name = "colCheck",
                HeaderText = "✔",
                Width = 36,
                Resizable = DataGridViewTriState.False
            };

            colColor = new DataGridViewTextBoxColumn
            {
                Name = "colColor",
                HeaderText = "Màu",
                Width = 46,
                ReadOnly = true,
                Resizable = DataGridViewTriState.False
            };

            colOldLayer = new DataGridViewTextBoxColumn
            {
                Name = "colOldLayer",
                HeaderText = "Layer Cũ (Source)",
                Width = 240,
                ReadOnly = true
            };

            colCount = new DataGridViewTextBoxColumn
            {
                Name = "colCount",
                HeaderText = "Số đ.tượng",
                Width = 90,
                ReadOnly = true,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight }
            };

            colNewLayer = new DataGridViewComboBoxColumn
            {
                Name = "colNewLayer",
                HeaderText = "Layer Mới (Target - Chọn để chuyển)",
                Width = 260,
                FlatStyle = FlatStyle.Flat
            };

            colStatus = new DataGridViewTextBoxColumn
            {
                Name = "colStatus",
                HeaderText = "Trạng thái",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 140,
                ReadOnly = true
            };

            dgvMapping.Columns.AddRange(new DataGridViewColumn[]
            {
                colCheck, colColor, colOldLayer, colCount, colNewLayer, colStatus
            });

            dgvMapping.CellPainting += DgvMapping_CellPainting;
            dgvMapping.CellValueChanged += DgvMapping_CellValueChanged;
            dgvMapping.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (dgvMapping.IsCurrentCellDirty)
                {
                    dgvMapping.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };
            dgvMapping.DataError += (s, e) => { e.Cancel = true; };

            // ================= 5. OPTIONS GROUPBOX =================
            grpOptions = new WinFormsGroupBox
            {
                Dock = DockStyle.Bottom,
                Height = 84,
                Text = "  ⚙️ Tùy Chọn Phạm Vi & Xử Lý  ",
                Font = new DrawingFont("Segoe UI", 8.5F, FontStyle.Bold),
                ForeColor = DrawingColor.FromArgb(15, 23, 42),
                Padding = new Padding(12, 6, 12, 6),
                BackColor = DrawingColor.FromArgb(248, 250, 252)
            };

            rbScopeAllSpaces = new WinFormsRadioButton
            {
                Text = "Toàn bộ bản vẽ (Model + All Layouts)",
                Location = new Point(14, 20),
                AutoSize = true,
                Font = new DrawingFont("Segoe UI", 8.5F, FontStyle.Regular),
                Checked = (_lastScopeIndex == 2)
            };
            rbScopeAllSpaces.CheckedChanged += (s, e) => { if (rbScopeAllSpaces.Checked) UpdateCountsColumnHeader(2); };

            rbScopeModelSpace = new WinFormsRadioButton
            {
                Text = "Chỉ trong Model Space",
                Location = new Point(275, 20),
                AutoSize = true,
                Font = new DrawingFont("Segoe UI", 8.5F, FontStyle.Regular),
                Checked = (_lastScopeIndex == 1)
            };
            rbScopeModelSpace.CheckedChanged += (s, e) => { if (rbScopeModelSpace.Checked) UpdateCountsColumnHeader(1); };

            rbScopeCurrentSpace = new WinFormsRadioButton
            {
                Text = "Không gian hiện hành",
                Location = new Point(445, 20),
                AutoSize = true,
                Font = new DrawingFont("Segoe UI", 8.5F, FontStyle.Regular),
                Checked = (_lastScopeIndex == 0)
            };
            rbScopeCurrentSpace.CheckedChanged += (s, e) => { if (rbScopeCurrentSpace.Checked) UpdateCountsColumnHeader(0); };

            rbScopeWindow = new WinFormsRadioButton
            {
                Text = "Quét chọn vùng trên CAD",
                Location = new Point(620, 20),
                AutoSize = true,
                Font = new DrawingFont("Segoe UI", 8.5F, FontStyle.Regular),
                Checked = (_lastScopeIndex == 3)
            };

            chkAutoUnlock = new WinFormsCheckBox
            {
                Text = "Tự động mở khóa (Unlock) Layer bị khóa",
                Location = new Point(14, 50),
                AutoSize = true,
                Font = new DrawingFont("Segoe UI", 8.5F, FontStyle.Regular),
                Checked = _lastAutoUnlock
            };

            chkPurgeOld = new WinFormsCheckBox
            {
                Text = "Xóa (Purge) Layer cũ nếu không còn đối tượng",
                Location = new Point(275, 50),
                AutoSize = true,
                Font = new DrawingFont("Segoe UI", 8.5F, FontStyle.Regular),
                Checked = _lastPurgeOld
            };

            chkIncludeBlocks = new WinFormsCheckBox
            {
                Text = "Chuyển đối tượng con trong Block Definition",
                Location = new Point(560, 50),
                AutoSize = true,
                Font = new DrawingFont("Segoe UI", 8.5F, FontStyle.Regular),
                Checked = _lastIncludeBlocks
            };

            chkCloseOnFinish = new WinFormsCheckBox
            {
                Text = "Đóng sau khi xong",
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(818, 50),
                AutoSize = true,
                Font = new DrawingFont("Segoe UI", 8.5F, FontStyle.Regular),
                Checked = _lastCloseOnFinish
            };

            grpOptions.Controls.Add(rbScopeAllSpaces);
            grpOptions.Controls.Add(rbScopeModelSpace);
            grpOptions.Controls.Add(rbScopeCurrentSpace);
            grpOptions.Controls.Add(rbScopeWindow);
            grpOptions.Controls.Add(chkAutoUnlock);
            grpOptions.Controls.Add(chkPurgeOld);
            grpOptions.Controls.Add(chkIncludeBlocks);
            grpOptions.Controls.Add(chkCloseOnFinish);

            // ================= 6. BOTTOM ACTION BAR =================
            pnlBottom = new WinFormsPanel
            {
                Dock = DockStyle.Bottom,
                Height = 56,
                BackColor = DrawingColor.FromArgb(238, 242, 246),
                Padding = new Padding(12, 10, 12, 10)
            };

            btnExecute = new WinFormsButton
            {
                Text = "🚀 THỰC HIỆN CHUYỂN ĐỔI",
                Location = new Point(12, 10),
                Size = new Size(230, 36),
                Font = new DrawingFont("Segoe UI", 10F, FontStyle.Bold),
                BackColor = DrawingColor.FromArgb(16, 149, 193),
                ForeColor = DrawingColor.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnExecute.FlatAppearance.BorderSize = 0;
            btnExecute.Click += BtnExecute_Click;

            btnExportMapping = new WinFormsButton
            {
                Text = "💾 Lưu mẫu ánh xạ",
                Location = new Point(252, 10),
                Size = new Size(130, 36),
                Font = new DrawingFont("Segoe UI", 8.5F, FontStyle.Regular),
                BackColor = DrawingColor.White,
                ForeColor = DrawingColor.FromArgb(30, 41, 59),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnExportMapping.FlatAppearance.BorderColor = DrawingColor.FromArgb(203, 213, 225);
            btnExportMapping.Click += BtnExportMapping_Click;

            btnImportMapping = new WinFormsButton
            {
                Text = "📂 Nạp mẫu ánh xạ",
                Location = new Point(390, 10),
                Size = new Size(130, 36),
                Font = new DrawingFont("Segoe UI", 8.5F, FontStyle.Regular),
                BackColor = DrawingColor.White,
                ForeColor = DrawingColor.FromArgb(30, 41, 59),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnImportMapping.FlatAppearance.BorderColor = DrawingColor.FromArgb(203, 213, 225);
            btnImportMapping.Click += BtnImportMapping_Click;

            btnRefresh = new WinFormsButton
            {
                Text = "🔄 Quét lại",
                Location = new Point(528, 10),
                Size = new Size(95, 36),
                Font = new DrawingFont("Segoe UI", 8.5F, FontStyle.Regular),
                BackColor = DrawingColor.White,
                ForeColor = DrawingColor.FromArgb(30, 41, 59),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnRefresh.FlatAppearance.BorderColor = DrawingColor.FromArgb(203, 213, 225);
            btnRefresh.Click += (s, e) => LoadLayersData();

            btnClose = new WinFormsButton
            {
                Text = "Đóng",
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(838, 10),
                Size = new Size(95, 36),
                Font = new DrawingFont("Segoe UI", 9F, FontStyle.Regular),
                BackColor = DrawingColor.White,
                ForeColor = DrawingColor.FromArgb(71, 85, 105),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderColor = DrawingColor.FromArgb(203, 213, 225);
            btnClose.Click += (s, e) => this.Close();

            pnlBottom.Controls.Add(btnExecute);
            pnlBottom.Controls.Add(btnExportMapping);
            pnlBottom.Controls.Add(btnImportMapping);
            pnlBottom.Controls.Add(btnRefresh);
            pnlBottom.Controls.Add(btnClose);

            // Thêm controls vào Form theo thứ tự hiển thị Dock
            this.Controls.Add(dgvMapping);
            this.Controls.Add(grpOptions);
            this.Controls.Add(pnlBottom);
            this.Controls.Add(pnlFilterBar);
            this.Controls.Add(pnlQuickBar);
            this.Controls.Add(pnlHeader);

            this.FormClosing += ChuyenDoiLayerForm_FormClosing;
            this.ResumeLayout(false);
        }

        #endregion

        #region Persistent State Load / Save

        private void RestoreLastSettings()
        {
            if (!_lastFormLocation.IsEmpty)
            {
                this.StartPosition = FormStartPosition.Manual;
                this.Location = _lastFormLocation;
            }
        }

        private void SaveCurrentSettings()
        {
            _lastFormSize = this.Size;
            _lastFormLocation = this.Location;

            _lastHideEmpty = chkHideEmpty.Checked;
            _lastOnlyChanged = chkOnlyChanged.Checked;
            _lastAutoUnlock = chkAutoUnlock.Checked;
            _lastPurgeOld = chkPurgeOld.Checked;
            _lastIncludeBlocks = chkIncludeBlocks.Checked;
            _lastCloseOnFinish = chkCloseOnFinish.Checked;

            if (rbScopeCurrentSpace.Checked) _lastScopeIndex = 0;
            else if (rbScopeModelSpace.Checked) _lastScopeIndex = 1;
            else if (rbScopeAllSpaces.Checked) _lastScopeIndex = 2;
            else if (rbScopeWindow.Checked) _lastScopeIndex = 3;

            // Lưu mapping của các layer
            foreach (var item in _allLayers)
            {
                if (!string.IsNullOrWhiteSpace(item.NewLayerName) &&
                    !item.NewLayerName.Equals(LayerMappingItem.NO_CHANGE_TEXT, StringComparison.OrdinalIgnoreCase) &&
                    !item.NewLayerName.Equals(item.OldLayerName, StringComparison.OrdinalIgnoreCase))
                {
                    _lastMapping[item.OldLayerName] = item.NewLayerName;
                }
                else
                {
                    _lastMapping.Remove(item.OldLayerName);
                }
            }
        }

        private void ChuyenDoiLayerForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            SaveCurrentSettings();
        }

        #endregion

        #region Data Loading & Scanning

        /// <summary>
        /// Nạp danh sách Layer trong bản vẽ và đếm số lượng đối tượng
        /// </summary>
        private void LoadLayersData()
        {
            try
            {
                _allLayers.Clear();
                _availableTargetLayers.Clear();
                _availableTargetLayers.Add(LayerMappingItem.NO_CHANGE_TEXT);

                var layerDict = new Dictionary<string, LayerMappingItem>(StringComparer.OrdinalIgnoreCase);

                using (var tr = _db.TransactionManager.StartTransaction())
                {
                    var lt = (LayerTable)tr.GetObject(_db.LayerTableId, OpenMode.ForRead);
                    foreach (ObjectId lId in lt)
                    {
                        var ltr = (LayerTableRecord)tr.GetObject(lId, OpenMode.ForRead);
                        var item = new LayerMappingItem
                        {
                            LayerId = lId,
                            OldLayerName = ltr.Name,
                            CadColor = ltr.Color,
                            IsLocked = ltr.IsLocked,
                            IsOff = ltr.IsOff,
                            IsFrozen = ltr.IsFrozen
                        };

                        try
                        {
                            item.DrawingColor = ltr.Color.ColorValue;
                        }
                        catch
                        {
                            item.DrawingColor = DrawingColor.Black;
                        }

                        // Khôi phục mapping đã lưu trước đó nếu có
                        if (_lastMapping.TryGetValue(ltr.Name, out var savedTarget) && !string.IsNullOrWhiteSpace(savedTarget))
                        {
                            item.NewLayerName = savedTarget;
                            item.IsSelectedToConvert = true;
                            item.StatusText = $"➡️ Sẽ chuyển sang: {savedTarget}";
                        }
                        else
                        {
                            item.NewLayerName = LayerMappingItem.NO_CHANGE_TEXT;
                            item.IsSelectedToConvert = false;
                            item.StatusText = "Giữ nguyên";
                        }

                        layerDict[ltr.Name] = item;
                        _allLayers.Add(item);
                        _availableTargetLayers.Add(ltr.Name);
                    }

                    // 1. Quét đối tượng trong CurrentSpace
                    var curBtr = (BlockTableRecord)tr.GetObject(_db.CurrentSpaceId, OpenMode.ForRead);
                    foreach (ObjectId entId in curBtr)
                    {
                        if (entId.IsErased || !entId.IsValid) continue;
                        var ent = tr.GetObject(entId, OpenMode.ForRead) as Entity;
                        if (ent != null && layerDict.TryGetValue(ent.Layer, out var itm))
                        {
                            itm.ObjectCountCurrentSpace++;
                        }
                    }

                    // 2. Quét đối tượng trong ModelSpace
                    var bt = (BlockTable)tr.GetObject(_db.BlockTableId, OpenMode.ForRead);
                    ObjectId modelId = bt[BlockTableRecord.ModelSpace];
                    if (modelId == _db.CurrentSpaceId)
                    {
                        foreach (var itm in _allLayers)
                        {
                            itm.ObjectCountModelSpace = itm.ObjectCountCurrentSpace;
                        }
                    }
                    else
                    {
                        var modelBtr = (BlockTableRecord)tr.GetObject(modelId, OpenMode.ForRead);
                        foreach (ObjectId entId in modelBtr)
                        {
                            if (entId.IsErased || !entId.IsValid) continue;
                            var ent = tr.GetObject(entId, OpenMode.ForRead) as Entity;
                            if (ent != null && layerDict.TryGetValue(ent.Layer, out var itm))
                            {
                                itm.ObjectCountModelSpace++;
                            }
                        }
                    }

                    // 3. Quét tất cả Layouts để tính AllSpaces
                    foreach (var itm in _allLayers)
                    {
                        itm.ObjectCountAllSpaces = itm.ObjectCountModelSpace;
                    }

                    foreach (ObjectId btrId in bt)
                    {
                        var btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
                        if (btr.IsLayout && btrId != modelId)
                        {
                            foreach (ObjectId entId in btr)
                            {
                                if (entId.IsErased || !entId.IsValid) continue;
                                var ent = tr.GetObject(entId, OpenMode.ForRead) as Entity;
                                if (ent != null && layerDict.TryGetValue(ent.Layer, out var itm))
                                {
                                    itm.ObjectCountAllSpaces++;
                                }
                            }
                        }
                    }

                    tr.Commit();
                }

                _allLayers = _allLayers.OrderBy(l => l.OldLayerName).ToList();

                // Cập nhật Items cho ComboBox chọn Target Layer
                UpdateTargetLayerComboBoxes();

                ApplyFilterAndRefreshGrid();
            }
            catch (System.Exception ex)
            {
                _ed.WriteMessage($"\n❌ [Lỗi nạp danh sách Layer]: {ex.Message}");
            }
        }

        private void UpdateTargetLayerComboBoxes()
        {
            var targets = _availableTargetLayers.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            // Cập nhật cho ComboBox trên toolbar
            cboTargetQuick.Items.Clear();
            foreach (var t in targets)
            {
                if (t != LayerMappingItem.NO_CHANGE_TEXT)
                {
                    cboTargetQuick.Items.Add(t);
                }
            }
            if (cboTargetQuick.Items.Count > 0)
            {
                cboTargetQuick.SelectedIndex = 0;
            }

            // Cập nhật cho DataGridViewComboBoxColumn
            colNewLayer.Items.Clear();
            foreach (var t in targets)
            {
                colNewLayer.Items.Add(t);
            }
        }

        private void EnsureTargetLayerExistsInLists(string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName)) return;
            if (!_availableTargetLayers.Contains(layerName, StringComparer.OrdinalIgnoreCase))
            {
                _availableTargetLayers.Add(layerName);
                if (!colNewLayer.Items.Contains(layerName)) colNewLayer.Items.Add(layerName);
                if (!cboTargetQuick.Items.Contains(layerName)) cboTargetQuick.Items.Add(layerName);
            }
        }

        private int GetCurrentScopeIndex()
        {
            if (rbScopeCurrentSpace.Checked) return 0;
            if (rbScopeModelSpace.Checked) return 1;
            if (rbScopeAllSpaces.Checked) return 2;
            return 3;
        }

        private void UpdateCountsColumnHeader(int scopeIndex)
        {
            string scopeText = scopeIndex switch
            {
                1 => "Model",
                2 => "Toàn bản vẽ",
                _ => "Current Space"
            };
            colCount.HeaderText = $"Số đ.tượng ({scopeText})";
            ApplyFilterAndRefreshGrid();
        }

        #endregion

        #region Grid Filtering & Rendering

        private void ApplyFilterAndRefreshGrid()
        {
            _isUpdatingUi = true;
            try
            {
                int scope = GetCurrentScopeIndex();
                string keyword = txtSearch.Text.Trim();
                bool hideEmpty = chkHideEmpty.Checked;
                bool onlyChanged = chkOnlyChanged.Checked;

                _displayLayers = _allLayers.Where(item =>
                {
                    // Lọc tìm kiếm
                    if (!string.IsNullOrEmpty(keyword) &&
                        item.OldLayerName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0 &&
                        item.NewLayerName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        return false;
                    }

                    // Lọc layer trống
                    int count = item.GetCountForScope(scope);
                    if (hideEmpty && count <= 0)
                    {
                        return false;
                    }

                    // Lọc chỉ dòng có thay đổi
                    if (onlyChanged && (string.IsNullOrEmpty(item.NewLayerName) ||
                                        item.NewLayerName.Equals(LayerMappingItem.NO_CHANGE_TEXT, StringComparison.OrdinalIgnoreCase) ||
                                        item.NewLayerName.Equals(item.OldLayerName, StringComparison.OrdinalIgnoreCase)))
                    {
                        return false;
                    }

                    return true;
                }).ToList();

                dgvMapping.Rows.Clear();
                foreach (var item in _displayLayers)
                {
                    int rowIdx = dgvMapping.Rows.Add();
                    var row = dgvMapping.Rows[rowIdx];
                    row.Tag = item;

                    row.Cells["colCheck"].Value = item.IsSelectedToConvert;
                    row.Cells["colOldLayer"].Value = item.OldLayerName;
                    row.Cells["colCount"].Value = item.GetCountForScope(scope);

                    EnsureTargetLayerExistsInLists(item.NewLayerName);
                    row.Cells["colNewLayer"].Value = item.NewLayerName;
                    row.Cells["colStatus"].Value = item.StatusText;

                    ApplyRowStyle(row, item);
                }

                UpdateStatsLabel();
            }
            finally
            {
                _isUpdatingUi = false;
            }
        }

        private void ApplyRowStyle(DataGridViewRow row, LayerMappingItem item)
        {
            bool hasChange = !string.IsNullOrEmpty(item.NewLayerName) &&
                             !item.NewLayerName.Equals(LayerMappingItem.NO_CHANGE_TEXT, StringComparison.OrdinalIgnoreCase) &&
                             !item.NewLayerName.Equals(item.OldLayerName, StringComparison.OrdinalIgnoreCase);

            if (item.LastConvertedCount > 0)
            {
                row.DefaultCellStyle.BackColor = DrawingColor.FromArgb(240, 253, 244); // Xanh lá nhạt
                row.DefaultCellStyle.ForeColor = DrawingColor.FromArgb(22, 101, 52);
            }
            else if (hasChange)
            {
                row.DefaultCellStyle.BackColor = DrawingColor.FromArgb(239, 246, 255); // Xanh dương nhạt
                row.DefaultCellStyle.ForeColor = DrawingColor.FromArgb(29, 78, 216);
            }
            else
            {
                row.DefaultCellStyle.BackColor = DrawingColor.White;
                row.DefaultCellStyle.ForeColor = DrawingColor.FromArgb(30, 41, 59);
            }
        }

        private void UpdateStatsLabel()
        {
            int total = _allLayers.Count;
            int withObjects = _allLayers.Count(l => l.GetCountForScope(GetCurrentScopeIndex()) > 0);
            int willConvertCount = _allLayers.Count(l => l.IsSelectedToConvert &&
                                                       !string.IsNullOrEmpty(l.NewLayerName) &&
                                                       !l.NewLayerName.Equals(LayerMappingItem.NO_CHANGE_TEXT, StringComparison.OrdinalIgnoreCase) &&
                                                       !l.NewLayerName.Equals(l.OldLayerName, StringComparison.OrdinalIgnoreCase));

            int totalObjectsToConvert = _allLayers.Where(l => l.IsSelectedToConvert &&
                                                            !string.IsNullOrEmpty(l.NewLayerName) &&
                                                            !l.NewLayerName.Equals(LayerMappingItem.NO_CHANGE_TEXT, StringComparison.OrdinalIgnoreCase) &&
                                                            !l.NewLayerName.Equals(l.OldLayerName, StringComparison.OrdinalIgnoreCase))
                                                 .Sum(l => l.GetCountForScope(GetCurrentScopeIndex()));

            lblStats.Text = $"Tổng: {total} layer | Có đối tượng: {withObjects} | Sẽ chuyển: {willConvertCount} layer ({totalObjectsToConvert} đối tượng)";
        }

        private void DgvMapping_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (_isUpdatingUi || e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var row = dgvMapping.Rows[e.RowIndex];
            if (row.Tag is not LayerMappingItem item) return;

            string colName = dgvMapping.Columns[e.ColumnIndex].Name;

            if (colName == "colNewLayer")
            {
                string newVal = row.Cells["colNewLayer"].Value?.ToString() ?? LayerMappingItem.NO_CHANGE_TEXT;
                item.NewLayerName = newVal;

                bool isDifferent = !newVal.Equals(LayerMappingItem.NO_CHANGE_TEXT, StringComparison.OrdinalIgnoreCase) &&
                                   !newVal.Equals(item.OldLayerName, StringComparison.OrdinalIgnoreCase);

                if (isDifferent)
                {
                    item.IsSelectedToConvert = true;
                    item.StatusText = $"➡️ Sẽ chuyển sang: {newVal}";
                }
                else
                {
                    item.IsSelectedToConvert = false;
                    item.StatusText = "Giữ nguyên";
                }

                _isUpdatingUi = true;
                row.Cells["colCheck"].Value = item.IsSelectedToConvert;
                row.Cells["colStatus"].Value = item.StatusText;
                ApplyRowStyle(row, item);
                _isUpdatingUi = false;

                UpdateStatsLabel();
            }
            else if (colName == "colCheck")
            {
                item.IsSelectedToConvert = Convert.ToBoolean(row.Cells["colCheck"].Value ?? false);
                UpdateStatsLabel();
            }
        }

        private void DgvMapping_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && dgvMapping.Columns[e.ColumnIndex].Name == "colColor")
            {
                e.PaintBackground(e.ClipBounds, (e.State & DataGridViewElementStates.Selected) != 0);

                if (dgvMapping.Rows[e.RowIndex].Tag is LayerMappingItem item)
                {
                    DrawingColor gdiColor;
                    try
                    {
                        gdiColor = item.CadColor.ColorValue;
                    }
                    catch
                    {
                        gdiColor = DrawingColor.Black;
                    }

                    int boxW = 24;
                    int boxH = 14;
                    int boxX = e.CellBounds.Left + (e.CellBounds.Width - boxW) / 2;
                    int boxY = e.CellBounds.Top + (e.CellBounds.Height - boxH) / 2;

                    using (var brush = new SolidBrush(gdiColor))
                    {
                        e.Graphics.FillRectangle(brush, boxX, boxY, boxW, boxH);
                    }
                    using (var pen = new Pen(DrawingColor.FromArgb(160, 160, 160)))
                    {
                        e.Graphics.DrawRectangle(pen, boxX, boxY, boxW, boxH);
                    }
                }

                e.Handled = true;
            }
        }

        #endregion

        #region Toolbar & Selection Actions

        private void ToggleSelectAllVisible(bool isChecked)
        {
            _isUpdatingUi = true;
            try
            {
                foreach (DataGridViewRow row in dgvMapping.Rows)
                {
                    if (row.Tag is LayerMappingItem item)
                    {
                        item.IsSelectedToConvert = isChecked;
                        row.Cells["colCheck"].Value = isChecked;
                    }
                }
                UpdateStatsLabel();
            }
            finally
            {
                _isUpdatingUi = false;
            }
        }

        private void BtnResetAll_Click(object? sender, EventArgs e)
        {
            if (MessageBox.Show("Bạn có chắc chắn muốn đặt lại tất cả các hàng về trạng thái Giữ nguyên không?",
                "Xác nhận đặt lại", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            _isUpdatingUi = true;
            try
            {
                foreach (var item in _allLayers)
                {
                    item.NewLayerName = LayerMappingItem.NO_CHANGE_TEXT;
                    item.IsSelectedToConvert = false;
                    item.StatusText = "Giữ nguyên";
                    item.LastConvertedCount = 0;
                }
                _lastMapping.Clear();

                foreach (DataGridViewRow row in dgvMapping.Rows)
                {
                    if (row.Tag is LayerMappingItem item)
                    {
                        row.Cells["colCheck"].Value = false;
                        row.Cells["colNewLayer"].Value = LayerMappingItem.NO_CHANGE_TEXT;
                        row.Cells["colStatus"].Value = "Giữ nguyên";
                        ApplyRowStyle(row, item);
                    }
                }

                UpdateStatsLabel();
            }
            finally
            {
                _isUpdatingUi = false;
            }
        }

        private void BtnQuickAssign_Click(object? sender, EventArgs e)
        {
            string targetLayer = cboTargetQuick.Text.Trim();
            if (string.IsNullOrWhiteSpace(targetLayer))
            {
                MessageBox.Show("Vui lòng chọn hoặc nhập tên Layer đích cần gán!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            EnsureTargetLayerExistsInLists(targetLayer);

            var selectedRows = dgvMapping.SelectedRows.Cast<DataGridViewRow>().ToList();
            if (selectedRows.Count == 0)
            {
                // Nếu không bôi đen dòng nào, kiểm tra các dòng có tích check
                selectedRows = dgvMapping.Rows.Cast<DataGridViewRow>()
                    .Where(r => Convert.ToBoolean(r.Cells["colCheck"].Value ?? false)).ToList();
            }

            if (selectedRows.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn ít nhất 1 dòng trong bảng để gán Layer đích!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _isUpdatingUi = true;
            try
            {
                foreach (var row in selectedRows)
                {
                    if (row.Tag is LayerMappingItem item)
                    {
                        item.NewLayerName = targetLayer;
                        bool isDiff = !targetLayer.Equals(item.OldLayerName, StringComparison.OrdinalIgnoreCase);
                        item.IsSelectedToConvert = isDiff;
                        item.StatusText = isDiff ? $"➡️ Sẽ chuyển sang: {targetLayer}" : "Giữ nguyên";

                        row.Cells["colCheck"].Value = item.IsSelectedToConvert;
                        row.Cells["colNewLayer"].Value = targetLayer;
                        row.Cells["colStatus"].Value = item.StatusText;
                        ApplyRowStyle(row, item);
                    }
                }

                UpdateStatsLabel();
            }
            finally
            {
                _isUpdatingUi = false;
            }
        }

        private void BtnAddNewLayer_Click(object? sender, EventArgs e)
        {
            using (var dlg = new NewLayerInputDialog())
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    string newName = dlg.LayerName.Trim();
                    if (!string.IsNullOrEmpty(newName))
                    {
                        EnsureTargetLayerExistsInLists(newName);
                        cboTargetQuick.Text = newName;
                        MessageBox.Show($"Đã thêm tên Layer '{newName}' vào danh sách đích thành công!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
        }

        private void BtnPickSample_Click(object? sender, EventArgs e)
        {
            try
            {
                using (_ed.StartUserInteraction(this))
                {
                    var ppo = new PromptEntityOptions("\n🎯 Chọn đối tượng mẫu trên bản vẽ CAD để lấy Layer: ");
                    ppo.SetRejectMessage("\nChỉ được chọn đối tượng đồ họa.");
                    ppo.AddAllowedClass(typeof(Entity), true);

                    var per = _ed.GetEntity(ppo);
                    if (per.Status != PromptStatus.OK) return;

                    string pickedLayerName = "";
                    using (var tr = _db.TransactionManager.StartTransaction())
                    {
                        var ent = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Entity;
                        if (ent != null)
                        {
                            pickedLayerName = ent.Layer;
                        }
                        tr.Commit();
                    }

                    if (!string.IsNullOrEmpty(pickedLayerName))
                    {
                        // Tìm và scroll đến dòng chứa layer này
                        txtSearch.Text = pickedLayerName;
                        foreach (DataGridViewRow row in dgvMapping.Rows)
                        {
                            if (row.Tag is LayerMappingItem item && item.OldLayerName.Equals(pickedLayerName, StringComparison.OrdinalIgnoreCase))
                            {
                                row.Selected = true;
                                dgvMapping.CurrentCell = row.Cells["colOldLayer"];
                                break;
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                _ed.WriteMessage($"\n❌ [Lỗi Pick đối tượng]: {ex.Message}");
            }
        }

        #endregion

        #region Export / Import Mapping Profile

        private void BtnExportMapping_Click(object? sender, EventArgs e)
        {
            var mappingDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in _allLayers)
            {
                if (!string.IsNullOrWhiteSpace(item.NewLayerName) &&
                    !item.NewLayerName.Equals(LayerMappingItem.NO_CHANGE_TEXT, StringComparison.OrdinalIgnoreCase) &&
                    !item.NewLayerName.Equals(item.OldLayerName, StringComparison.OrdinalIgnoreCase))
                {
                    mappingDict[item.OldLayerName] = item.NewLayerName;
                }
            }

            if (mappingDict.Count == 0)
            {
                MessageBox.Show("Hiện chưa có quy tắc đổi Layer nào được thiết lập để lưu!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var sfd = new SaveFileDialog())
            {
                sfd.Title = "Lưu cấu hình ánh xạ Layer";
                sfd.Filter = "JSON Files (*.json)|*.json|Text Files (*.txt)|*.txt|All Files (*.*)|*.*";
                sfd.FileName = "LayerMapping_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json";

                if (sfd.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        string json = JsonSerializer.Serialize(mappingDict, new JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText(sfd.FileName, json, Encoding.UTF8);
                        MessageBox.Show($"Đã lưu {mappingDict.Count} quy tắc ánh xạ Layer thành công!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (System.Exception ex)
                    {
                        MessageBox.Show($"Lỗi khi lưu file: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void BtnImportMapping_Click(object? sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Title = "Nạp cấu hình ánh xạ Layer";
                ofd.Filter = "JSON Files (*.json)|*.json|Text Files (*.txt)|*.txt|All Files (*.*)|*.*";

                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        string content = File.ReadAllText(ofd.FileName, Encoding.UTF8);
                        Dictionary<string, string>? loadedDict = null;

                        if (ofd.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                        {
                            loadedDict = JsonSerializer.Deserialize<Dictionary<string, string>>(content);
                        }
                        else
                        {
                            // Đọc file text dạng: LayerCu=LayerMoi hoặc LayerCu,LayerMoi
                            loadedDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var line in lines)
                            {
                                var parts = line.Split(new[] { '=', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);
                                if (parts.Length >= 2)
                                {
                                    loadedDict[parts[0].Trim()] = parts[1].Trim();
                                }
                            }
                        }

                        if (loadedDict != null && loadedDict.Count > 0)
                        {
                            int matchCount = 0;
                            foreach (var kvp in loadedDict)
                            {
                                EnsureTargetLayerExistsInLists(kvp.Value);

                                var found = _allLayers.FirstOrDefault(l => l.OldLayerName.Equals(kvp.Key, StringComparison.OrdinalIgnoreCase));
                                if (found != null)
                                {
                                    found.NewLayerName = kvp.Value;
                                    found.IsSelectedToConvert = true;
                                    found.StatusText = $"➡️ Sẽ chuyển sang: {kvp.Value}";
                                    matchCount++;
                                }
                            }

                            ApplyFilterAndRefreshGrid();
                            MessageBox.Show($"Đã nạp thành công {matchCount}/{loadedDict.Count} quy tắc ánh xạ Layer tương ứng với bản vẽ!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        MessageBox.Show($"Lỗi nạp file: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        #endregion

        #region Execution (CAD Transaction)

        private void BtnExecute_Click(object? sender, EventArgs e)
        {
            // 1. Thu thập danh sách mapping hợp lệ
            var mappingList = _allLayers.Where(l => l.IsSelectedToConvert &&
                                                   !string.IsNullOrWhiteSpace(l.NewLayerName) &&
                                                   !l.NewLayerName.Equals(LayerMappingItem.NO_CHANGE_TEXT, StringComparison.OrdinalIgnoreCase) &&
                                                   !l.NewLayerName.Equals(l.OldLayerName, StringComparison.OrdinalIgnoreCase))
                                       .ToList();

            if (mappingList.Count == 0)
            {
                MessageBox.Show("Chưa có Layer nào được thiết lập chuyển đổi sang Layer mới!\nVui lòng chọn Layer đích trên cột 'Layer Mới' cho các dòng cần chuyển.",
                    "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var mapDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in mappingList)
            {
                mapDict[m.OldLayerName] = m.NewLayerName;
            }

            // 2. Kiểm tra nếu chế độ là Quét vùng trên CAD
            ObjectIdCollection? targetEntityIds = null;
            if (rbScopeWindow.Checked)
            {
                using (_ed.StartUserInteraction(this))
                {
                    var pso = new PromptSelectionOptions
                    {
                        MessageForAdding = "\n🎯 Quét chọn các đối tượng trên bản vẽ cần chuyển Layer: "
                    };
                    var psr = _ed.GetSelection(pso);
                    if (psr.Status != PromptStatus.OK || psr.Value == null || psr.Value.Count == 0)
                    {
                        _ed.WriteMessage("\n⚠️ Đã hủy thao tác chọn đối tượng.");
                        return;
                    }
                    targetEntityIds = new ObjectIdCollection(psr.Value.GetObjectIds());
                }
            }

            // 3. Thực hiện chuyển đổi qua Transaction
            int totalConverted = 0;
            int purgedCount = 0;
            var perLayerConverted = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in mapDict)
            {
                perLayerConverted[kvp.Key] = 0;
            }

            try
            {
                using (var docLock = _doc.LockDocument())
                using (var tr = _db.TransactionManager.StartTransaction())
                {
                    var lt = (LayerTable)tr.GetObject(_db.LayerTableId, OpenMode.ForWrite);

                    // A. Đảm bảo tất cả Layer đích đã tồn tại trong LayerTable
                    var targetDistinctNames = mapDict.Values.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    foreach (var targetName in targetDistinctNames)
                    {
                        if (!lt.Has(targetName))
                        {
                            var newLtr = new LayerTableRecord
                            {
                                Name = targetName,
                                Color = CadColor.FromColorIndex(ColorMethod.ByAci, 7) // Trắng/Đen mặc định
                            };

                            // Tìm màu của layer nguồn đầu tiên chuyển sang để gán cho layer mới nếu có
                            var sourceItem = mappingList.FirstOrDefault(m => m.NewLayerName.Equals(targetName, StringComparison.OrdinalIgnoreCase));
                            if (sourceItem != null)
                            {
                                newLtr.Color = sourceItem.CadColor;
                            }

                            lt.Add(newLtr);
                            tr.AddNewlyCreatedDBObject(newLtr, true);
                        }
                    }

                    // B. Tự động mở khóa (Unlock) các layer cũ và mới nếu bị khóa
                    var unlockedLayers = new List<LayerTableRecord>();
                    if (chkAutoUnlock.Checked)
                    {
                        foreach (ObjectId lId in lt)
                        {
                            var ltr = (LayerTableRecord)tr.GetObject(lId, OpenMode.ForRead);
                            if (ltr.IsLocked && (mapDict.ContainsKey(ltr.Name) || targetDistinctNames.Contains(ltr.Name, StringComparer.OrdinalIgnoreCase)))
                            {
                                ltr.UpgradeOpen();
                                ltr.IsLocked = false;
                                unlockedLayers.Add(ltr);
                            }
                        }
                    }

                    // C. Lấy danh sách các Entity cần duyệt
                    var entitiesToProcess = new List<ObjectId>();

                    if (targetEntityIds != null)
                    {
                        // Chế độ Quét vùng
                        foreach (ObjectId id in targetEntityIds)
                        {
                            entitiesToProcess.Add(id);
                        }
                    }
                    else if (rbScopeCurrentSpace.Checked)
                    {
                        // Chế độ Current Space
                        var curBtr = (BlockTableRecord)tr.GetObject(_db.CurrentSpaceId, OpenMode.ForRead);
                        foreach (ObjectId id in curBtr) entitiesToProcess.Add(id);
                    }
                    else if (rbScopeModelSpace.Checked)
                    {
                        // Chế độ Model Space
                        var bt = (BlockTable)tr.GetObject(_db.BlockTableId, OpenMode.ForRead);
                        var modelBtr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                        foreach (ObjectId id in modelBtr) entitiesToProcess.Add(id);
                    }
                    else
                    {
                        // Chế độ Toàn bộ bản vẽ (All Spaces: Model + Layouts)
                        var bt = (BlockTable)tr.GetObject(_db.BlockTableId, OpenMode.ForRead);
                        var modelBtr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                        foreach (ObjectId id in modelBtr) entitiesToProcess.Add(id);

                        foreach (ObjectId btrId in bt)
                        {
                            var btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
                            if (btr.IsLayout && btrId != bt[BlockTableRecord.ModelSpace])
                            {
                                foreach (ObjectId id in btr) entitiesToProcess.Add(id);
                            }
                        }
                    }

                    // Thêm các entity trong Block Definition nếu người dùng bật tùy chọn
                    if (chkIncludeBlocks.Checked && targetEntityIds == null)
                    {
                        var bt = (BlockTable)tr.GetObject(_db.BlockTableId, OpenMode.ForRead);
                        foreach (ObjectId btrId in bt)
                        {
                            var btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
                            if (!btr.IsLayout && !btr.IsFromExternalReference && !btr.IsDependent)
                            {
                                foreach (ObjectId id in btr) entitiesToProcess.Add(id);
                            }
                        }
                    }

                    // D. Duyệt và đổi Layer
                    foreach (ObjectId entId in entitiesToProcess)
                    {
                        if (entId.IsErased || !entId.IsValid) continue;
                        var ent = tr.GetObject(entId, OpenMode.ForRead) as Entity;
                        if (ent == null) continue;

                        string oldLayer = ent.Layer;
                        if (mapDict.TryGetValue(oldLayer, out var newLayerName))
                        {
                            try
                            {
                                ent.UpgradeOpen();
                                ent.Layer = newLayerName;
                                totalConverted++;

                                if (perLayerConverted.ContainsKey(oldLayer))
                                {
                                    perLayerConverted[oldLayer]++;
                                }
                                else
                                {
                                    perLayerConverted[oldLayer] = 1;
                                }
                            }
                            catch
                            {
                                // Bỏ qua nếu entity bị hạn chế quyền sửa đổi
                            }
                        }
                    }

                    // E. Xóa (Purge) các layer cũ nếu được yêu cầu
                    if (chkPurgeOld.Checked)
                    {
                        var layerIdsToPurge = new ObjectIdCollection();
                        foreach (var kvp in mapDict)
                        {
                            if (lt.Has(kvp.Key))
                            {
                                ObjectId oldId = lt[kvp.Key];
                                // Không xóa layer đặc biệt: 0, Defpoints, và Clayer
                                if (oldId != _db.Clayer)
                                {
                                    var oldLtr = (LayerTableRecord)tr.GetObject(oldId, OpenMode.ForRead);
                                    if (!oldLtr.Name.Equals("0", StringComparison.OrdinalIgnoreCase) &&
                                        !oldLtr.Name.Equals("Defpoints", StringComparison.OrdinalIgnoreCase))
                                    {
                                        layerIdsToPurge.Add(oldId);
                                    }
                                }
                            }
                        }

                        if (layerIdsToPurge.Count > 0)
                        {
                            _db.Purge(layerIdsToPurge);
                            foreach (ObjectId purgeId in layerIdsToPurge)
                            {
                                var ltr = tr.GetObject(purgeId, OpenMode.ForWrite) as LayerTableRecord;
                                if (ltr != null && !ltr.IsErased)
                                {
                                    ltr.Erase();
                                    purgedCount++;
                                }
                            }
                        }
                    }

                    tr.Commit();
                }

                // F. Cập nhật lại kết quả lên Form
                foreach (var item in _allLayers)
                {
                    if (perLayerConverted.TryGetValue(item.OldLayerName, out int cnt))
                    {
                        item.LastConvertedCount = cnt;
                        item.StatusText = cnt > 0 ? $"✅ Đã chuyển {cnt} đ.tượng sang {item.NewLayerName}" : "Không có đ.tượng";
                    }
                }

                // Quét lại dữ liệu mới từ bản vẽ
                LoadLayersData();

                string msg = $"🎉 CHUYỂN ĐỔI THÀNH CÔNG!\n\n" +
                             $"• Tổng số đối tượng đã đổi Layer: {totalConverted:#,##0}\n" +
                             $"• Số Layer đã xử lý: {mapDict.Count}\n";
                if (chkPurgeOld.Checked)
                {
                    msg += $"• Số Layer cũ đã được dọn sạch (Purged): {purgedCount}\n";
                }

                _ed.WriteMessage($"\n✅ [AT_Chuyen_Doi_Layer]: Chuyển đổi thành công {totalConverted} đối tượng từ {mapDict.Count} layer.\n");
                MessageBox.Show(msg, "Kết Quả Thực Hiện", MessageBoxButtons.OK, MessageBoxIcon.Information);

                if (chkCloseOnFinish.Checked)
                {
                    this.Close();
                }
            }
            catch (System.Exception ex)
            {
                _ed.WriteMessage($"\n❌ [Lỗi chuyển đổi Layer]: {ex.Message}");
                MessageBox.Show($"Đã xảy ra lỗi trong quá trình chuyển đổi:\n{ex.Message}", "Lỗi Thực Thi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion
    }

    /// <summary>
    /// Hộp thoại phụ cho phép nhập tên Layer mới
    /// </summary>
    public class NewLayerInputDialog : Form
    {
        public string LayerName { get; private set; } = "";

        public NewLayerInputDialog()
        {
            this.Text = "Tạo Tên Layer Mới";
            this.Size = new Size(380, 160);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Font = new DrawingFont("Segoe UI", 9F, FontStyle.Regular);

            var lbl = new WinFormsLabel
            {
                Text = "Nhập tên Layer mới cần tạo:",
                Location = new Point(16, 16),
                AutoSize = true
            };

            var txt = new WinFormsTextBox
            {
                Location = new Point(18, 40),
                Size = new Size(328, 26)
            };

            var btnOk = new WinFormsButton
            {
                Text = "Đồng ý",
                Location = new Point(170, 80),
                Size = new Size(85, 30),
                DialogResult = DialogResult.OK,
                BackColor = DrawingColor.FromArgb(14, 116, 144),
                ForeColor = DrawingColor.White,
                FlatStyle = FlatStyle.Flat
            };
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Click += (s, e) =>
            {
                LayerName = txt.Text.Trim();
                this.Close();
            };

            var btnCancel = new WinFormsButton
            {
                Text = "Hủy",
                Location = new Point(262, 80),
                Size = new Size(85, 30),
                DialogResult = DialogResult.Cancel,
                BackColor = DrawingColor.White,
                FlatStyle = FlatStyle.Flat
            };
            btnCancel.FlatAppearance.BorderColor = DrawingColor.FromArgb(203, 213, 225);
            btnCancel.Click += (s, e) => this.Close();

            this.Controls.Add(lbl);
            this.Controls.Add(txt);
            this.Controls.Add(btnOk);
            this.Controls.Add(btnCancel);
            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
        }
    }
}

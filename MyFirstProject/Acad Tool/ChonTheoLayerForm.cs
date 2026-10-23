// (C) Copyright 2026 by T27
// Form giao diện Lọc và Chọn tất cả đối tượng thuộc Layer đã chọn
//
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using CadColor = Autodesk.AutoCAD.Colors.Color;
using DrawingColor = System.Drawing.Color;
using DrawingFont = System.Drawing.Font;
using WinFormsButton = System.Windows.Forms.Button;
using WinFormsCheckBox = System.Windows.Forms.CheckBox;
using WinFormsGroupBox = System.Windows.Forms.GroupBox;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsRadioButton = System.Windows.Forms.RadioButton;

namespace MyFirstProject.Acad_Tool
{
    /// <summary>
    /// Model dữ liệu hiển thị thông tin từng Layer trong DataGridView
    /// </summary>
    public class LayerSelectionItem
    {
        public ObjectId LayerId { get; set; } = ObjectId.Null;
        public string Name { get; set; } = "";
        public CadColor CadColor { get; set; } = CadColor.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 7);
        public DrawingColor DrawingColor { get; set; } = DrawingColor.White;
        public bool IsSelected { get; set; } = false;
        public int ObjectCountCurrentSpace { get; set; } = 0;
        public int ObjectCountModelSpace { get; set; } = 0;
        public int ObjectCountAllSpaces { get; set; } = 0;
        public bool IsLocked { get; set; } = false;
        public bool IsOff { get; set; } = false;
        public bool IsFrozen { get; set; } = false;
        public string Linetype { get; set; } = "Continuous";
        public string Description { get; set; } = "";

        /// <summary>
        /// Lấy số lượng đối tượng tương ứng với phạm vi đã chọn
        /// </summary>
        public int GetCountForScope(int scopeIndex)
        {
            return scopeIndex switch
            {
                1 => ObjectCountModelSpace,
                2 => ObjectCountAllSpaces,
                _ => ObjectCountCurrentSpace
            };
        }

        /// <summary>
        /// Chuỗi trạng thái hiển thị
        /// </summary>
        public string StatusSummary
        {
            get
            {
                var parts = new List<string>();
                parts.Add(IsOff ? "🔅 Tắt" : "💡 Bật");
                if (IsFrozen) parts.Add("❄️ Đóng băng");
                if (IsLocked) parts.Add("🔒 Khóa");
                return string.Join(" | ", parts);
            }
        }
    }

    /// <summary>
    /// Form giao diện chuyên nghiệp cho lệnh AT_Chon_Theo_Layer
    /// </summary>
    public class ChonTheoLayerForm : Form
    {
        #region Persistent State (RULE 2)

        private static readonly HashSet<string> _lastSelectedLayerNames = new(StringComparer.OrdinalIgnoreCase);
        private static int _lastScopeIndex = 0; // 0=CurrentSpace, 1=ModelSpace, 2=AllSpaces, 3=Window
        private static bool _lastIgnoreLocked = true;
        private static bool _lastZoomSelected = true;
        private static bool _lastCloseOnSelect = true;
        private static string _lastSearchText = "";
        private static int _lastFilterStatusIndex = 0;
        private static Size _lastFormSize = new Size(840, 640);
        private static Point _lastFormLocation = Point.Empty;

        #endregion

        #region Fields

        private readonly Document _doc;
        private readonly Database _db;
        private readonly Editor _ed;

        private List<LayerSelectionItem> _allLayers = new();
        private List<LayerSelectionItem> _displayLayers = new();
        private bool _isUpdatingUi = false;

        // Header controls
        private WinFormsLabel lblTitle = null!;
        private WinFormsLabel lblSubtitle = null!;

        // Pick & Quick tools
        private WinFormsButton btnPickSample = null!;
        private WinFormsButton btnPickWindowSample = null!;
        private WinFormsButton btnSelectAll = null!;
        private WinFormsButton btnDeselectAll = null!;
        private WinFormsButton btnInvertSelect = null!;
        private WinFormsButton btnSelectNonEmpty = null!;

        // Search & Filter
        private TextBox txtSearch = null!;
        private ComboBox cboFilterStatus = null!;
        private WinFormsLabel lblStats = null!;

        // Grid
        private DataGridView dgvLayers = null!;

        // Options
        private WinFormsGroupBox grpOptions = null!;
        private WinFormsRadioButton rbScopeCurrentSpace = null!;
        private WinFormsRadioButton rbScopeModelSpace = null!;
        private WinFormsRadioButton rbScopeAllSpaces = null!;
        private WinFormsRadioButton rbScopeWindow = null!;
        private WinFormsCheckBox chkIgnoreLocked = null!;
        private WinFormsCheckBox chkZoomSelected = null!;
        private WinFormsCheckBox chkCloseOnSelect = null!;

        // Bottom action buttons
        private WinFormsButton btnExecuteSelect = null!;
        private WinFormsButton btnIsolateLayers = null!;
        private WinFormsButton btnUnisolateLayers = null!;
        private WinFormsButton btnCopyLayerNames = null!;
        private WinFormsButton btnClose = null!;

        #endregion

        #region Constructor

        public ChonTheoLayerForm(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _db = doc.Database;
            _ed = doc.Editor;

            InitializeComponent();
            RestoreLastSettings();
            LoadLayersData();
        }

        #endregion

        #region UI Initialization

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.Text = "Chọn Đối Tượng Theo Layer (AT_Chon_Theo_Layer)";
            this.Font = new DrawingFont("Segoe UI", 9F, FontStyle.Regular);
            this.ClientSize = new Size(840, 640);
            this.MinimumSize = new Size(760, 520);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = DrawingColor.FromArgb(248, 249, 250);

            // ================= 1. HEADER PANEL =================
            var pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 62,
                BackColor = DrawingColor.FromArgb(22, 53, 90),
                Padding = new Padding(16, 8, 16, 8)
            };

            lblTitle = new WinFormsLabel
            {
                Text = "🎯 CHỌN ĐỐI TƯỢNG THEO LAYER (AT_Chon_Theo_Layer)",
                Font = new DrawingFont("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = DrawingColor.White,
                AutoSize = true,
                Location = new Point(14, 10)
            };

            lblSubtitle = new WinFormsLabel
            {
                Text = "Lọc và đưa tất cả đối tượng thuộc các Layer đã chọn vào tập chọn hiện hành (Pickfirst Selection)",
                Font = new DrawingFont("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = DrawingColor.FromArgb(215, 230, 248),
                AutoSize = true,
                Location = new Point(16, 34)
            };

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSubtitle);

            // ================= 2. TOOLBAR / QUICK PICK PANEL =================
            var pnlToolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = DrawingColor.FromArgb(238, 242, 246),
                Padding = new Padding(12, 6, 12, 6)
            };

            btnPickSample = new WinFormsButton
            {
                Text = "🎯 Pick đối tượng mẫu",
                Location = new Point(12, 7),
                Size = new Size(150, 30),
                Font = new DrawingFont("Segoe UI", 9F, FontStyle.Bold),
                BackColor = DrawingColor.FromArgb(37, 99, 235),
                ForeColor = DrawingColor.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnPickSample.FlatAppearance.BorderSize = 0;
            btnPickSample.Click += BtnPickSample_Click;

            btnPickWindowSample = new WinFormsButton
            {
                Text = "🔲 Quét chọn nhiều mẫu",
                Location = new Point(168, 7),
                Size = new Size(150, 30),
                Font = new DrawingFont("Segoe UI", 9F, FontStyle.Regular),
                BackColor = DrawingColor.White,
                ForeColor = DrawingColor.FromArgb(30, 41, 59),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnPickWindowSample.FlatAppearance.BorderColor = DrawingColor.FromArgb(203, 213, 225);
            btnPickWindowSample.Click += BtnPickWindowSample_Click;

            var sep1 = new Panel
            {
                Location = new Point(326, 9),
                Size = new Size(1, 26),
                BackColor = DrawingColor.FromArgb(203, 213, 225)
            };

            btnSelectAll = CreateToolbarButton("☑️ Chọn tất cả", 336, 7, 95, BtnSelectAll_Click);
            btnDeselectAll = CreateToolbarButton("⬜ Bỏ chọn hết", 437, 7, 95, BtnDeselectAll_Click);
            btnInvertSelect = CreateToolbarButton("🔄 Đảo chọn", 538, 7, 85, BtnInvertSelect_Click);
            btnSelectNonEmpty = CreateToolbarButton("📊 Layer có đ.tượng", 629, 7, 125, BtnSelectNonEmpty_Click);

            pnlToolbar.Controls.Add(btnPickSample);
            pnlToolbar.Controls.Add(btnPickWindowSample);
            pnlToolbar.Controls.Add(sep1);
            pnlToolbar.Controls.Add(btnSelectAll);
            pnlToolbar.Controls.Add(btnDeselectAll);
            pnlToolbar.Controls.Add(btnInvertSelect);
            pnlToolbar.Controls.Add(btnSelectNonEmpty);

            // ================= 3. FILTER & SEARCH PANEL =================
            var pnlSearch = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = DrawingColor.White,
                Padding = new Padding(12, 6, 12, 6)
            };

            var lblSearchIcon = new WinFormsLabel
            {
                Text = "🔍 Tìm:",
                AutoSize = true,
                Location = new Point(12, 10),
                ForeColor = DrawingColor.FromArgb(71, 85, 105),
                Font = new DrawingFont("Segoe UI", 9F, FontStyle.Bold)
            };

            txtSearch = new TextBox
            {
                Location = new Point(62, 7),
                Size = new Size(240, 25),
                Font = new DrawingFont("Segoe UI", 9.5F, FontStyle.Regular)
            };
            txtSearch.TextChanged += (s, e) => ApplyFilterAndRefreshGrid();

            var lblFilterIcon = new WinFormsLabel
            {
                Text = "Lọc:",
                AutoSize = true,
                Location = new Point(314, 10),
                ForeColor = DrawingColor.FromArgb(71, 85, 105),
                Font = new DrawingFont("Segoe UI", 9F, FontStyle.Bold)
            };

            cboFilterStatus = new ComboBox
            {
                Location = new Point(348, 7),
                Size = new Size(180, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new DrawingFont("Segoe UI", 9F, FontStyle.Regular)
            };
            cboFilterStatus.Items.AddRange(new object[]
            {
                "Tất cả Layer",
                "Chỉ Layer có đối tượng (> 0)",
                "Chỉ Layer đang được chọn",
                "Chỉ Layer đang Bật",
                "Chỉ Layer bị Khóa"
            });
            cboFilterStatus.SelectedIndex = 0;
            cboFilterStatus.SelectedIndexChanged += (s, e) => ApplyFilterAndRefreshGrid();

            lblStats = new WinFormsLabel
            {
                Text = "Tổng cộng: 0 layer | Đang chọn: 0 layer (0 đối tượng)",
                AutoSize = true,
                Location = new Point(540, 10),
                ForeColor = DrawingColor.FromArgb(37, 99, 235),
                Font = new DrawingFont("Segoe UI", 9F, FontStyle.Bold)
            };

            pnlSearch.Controls.Add(lblSearchIcon);
            pnlSearch.Controls.Add(txtSearch);
            pnlSearch.Controls.Add(lblFilterIcon);
            pnlSearch.Controls.Add(cboFilterStatus);
            pnlSearch.Controls.Add(lblStats);

            // ================= 4. DATAGRIDVIEW =================
            dgvLayers = new DataGridView
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
                AutoGenerateColumns = false,
                EnableHeadersVisualStyles = false,
                GridColor = DrawingColor.FromArgb(226, 232, 240)
            };

            dgvLayers.ColumnHeadersDefaultCellStyle.BackColor = DrawingColor.FromArgb(241, 245, 249);
            dgvLayers.ColumnHeadersDefaultCellStyle.ForeColor = DrawingColor.FromArgb(30, 41, 59);
            dgvLayers.ColumnHeadersDefaultCellStyle.Font = new DrawingFont("Segoe UI", 9F, FontStyle.Bold);
            dgvLayers.ColumnHeadersHeight = 32;
            dgvLayers.RowTemplate.Height = 27;

            // Columns
            var colCheck = new DataGridViewCheckBoxColumn
            {
                Name = "colCheck",
                HeaderText = "✓",
                Width = 38,
                Resizable = DataGridViewTriState.False
            };

            var colColor = new DataGridViewTextBoxColumn
            {
                Name = "colColor",
                HeaderText = "Màu",
                Width = 52,
                ReadOnly = true,
                Resizable = DataGridViewTriState.False
            };

            var colName = new DataGridViewTextBoxColumn
            {
                Name = "colName",
                HeaderText = "Tên Layer",
                Width = 260,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.Automatic
            };

            var colCount = new DataGridViewTextBoxColumn
            {
                Name = "colCount",
                HeaderText = "Số đối tượng",
                Width = 110,
                ReadOnly = true,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N0" },
                SortMode = DataGridViewColumnSortMode.Automatic
            };

            var colStatus = new DataGridViewTextBoxColumn
            {
                Name = "colStatus",
                HeaderText = "Trạng thái",
                Width = 140,
                ReadOnly = true
            };

            var colLinetype = new DataGridViewTextBoxColumn
            {
                Name = "colLinetype",
                HeaderText = "Linetype",
                Width = 110,
                ReadOnly = true
            };

            dgvLayers.Columns.AddRange(colCheck, colColor, colName, colCount, colStatus, colLinetype);

            // Grid Events
            dgvLayers.CellPainting += DgvLayers_CellPainting;
            dgvLayers.CellContentClick += DgvLayers_CellContentClick;
            dgvLayers.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0)
                {
                    ToggleRowSelection(e.RowIndex);
                }
            };
            dgvLayers.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Space && dgvLayers.CurrentRow != null)
                {
                    ToggleRowSelection(dgvLayers.CurrentRow.Index);
                    e.Handled = true;
                }
            };

            // ================= 5. OPTIONS & SCOPE PANEL =================
            grpOptions = new WinFormsGroupBox
            {
                Dock = DockStyle.Bottom,
                Height = 85,
                Text = "Tùy chọn chọn đối tượng & Phạm vi quét",
                Font = new DrawingFont("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = DrawingColor.FromArgb(30, 41, 59),
                Padding = new Padding(12, 4, 12, 6)
            };

            rbScopeCurrentSpace = new WinFormsRadioButton
            {
                Text = "Không gian hiện hành (Model / Layout)",
                Location = new Point(14, 22),
                Size = new Size(250, 24),
                Font = new DrawingFont("Segoe UI", 8.5F, FontStyle.Regular),
                Checked = true
            };
            rbScopeCurrentSpace.CheckedChanged += (s, e) => { if (rbScopeCurrentSpace.Checked) UpdateCountsColumnHeader(0); };

            rbScopeModelSpace = new WinFormsRadioButton
            {
                Text = "Không gian Model (Model Space)",
                Location = new Point(270, 22),
                Size = new Size(210, 24),
                Font = new DrawingFont("Segoe UI", 8.5F, FontStyle.Regular)
            };
            rbScopeModelSpace.CheckedChanged += (s, e) => { if (rbScopeModelSpace.Checked) UpdateCountsColumnHeader(1); };

            rbScopeAllSpaces = new WinFormsRadioButton
            {
                Text = "Toàn bộ bản vẽ (Model + All Layouts)",
                Location = new Point(486, 22),
                Size = new Size(230, 24),
                Font = new DrawingFont("Segoe UI", 8.5F, FontStyle.Regular)
            };
            rbScopeAllSpaces.CheckedChanged += (s, e) => { if (rbScopeAllSpaces.Checked) UpdateCountsColumnHeader(2); };

            rbScopeWindow = new WinFormsRadioButton
            {
                Text = "Quét chọn vùng trên bản vẽ",
                Location = new Point(14, 50),
                Size = new Size(200, 24),
                Font = new DrawingFont("Segoe UI", 8.5F, FontStyle.Regular)
            };

            chkIgnoreLocked = new WinFormsCheckBox
            {
                Text = "Bỏ qua Layer bị Khóa",
                Location = new Point(270, 50),
                Size = new Size(160, 24),
                Font = new DrawingFont("Segoe UI", 8.5F, FontStyle.Regular),
                Checked = true
            };

            chkZoomSelected = new WinFormsCheckBox
            {
                Text = "Zoom tới đối tượng",
                Location = new Point(440, 50),
                Size = new Size(140, 24),
                Font = new DrawingFont("Segoe UI", 8.5F, FontStyle.Regular),
                Checked = true
            };

            chkCloseOnSelect = new WinFormsCheckBox
            {
                Text = "Đóng Form sau khi chọn",
                Location = new Point(590, 50),
                Size = new Size(170, 24),
                Font = new DrawingFont("Segoe UI", 8.5F, FontStyle.Regular),
                Checked = true
            };

            grpOptions.Controls.Add(rbScopeCurrentSpace);
            grpOptions.Controls.Add(rbScopeModelSpace);
            grpOptions.Controls.Add(rbScopeAllSpaces);
            grpOptions.Controls.Add(rbScopeWindow);
            grpOptions.Controls.Add(chkIgnoreLocked);
            grpOptions.Controls.Add(chkZoomSelected);
            grpOptions.Controls.Add(chkCloseOnSelect);

            // ================= 6. BOTTOM ACTION BAR =================
            var pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                BackColor = DrawingColor.FromArgb(238, 242, 246),
                Padding = new Padding(12, 8, 12, 8)
            };

            btnExecuteSelect = new WinFormsButton
            {
                Text = "🎯 CHỌN ĐỐI TƯỢNG (SELECT)",
                Location = new Point(12, 8),
                Size = new Size(220, 36),
                Font = new DrawingFont("Segoe UI", 9.5F, FontStyle.Bold),
                BackColor = DrawingColor.FromArgb(16, 149, 193),
                ForeColor = DrawingColor.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnExecuteSelect.FlatAppearance.BorderSize = 0;
            btnExecuteSelect.Click += BtnExecuteSelect_Click;

            btnIsolateLayers = new WinFormsButton
            {
                Text = "👁️ Cô lập Layer",
                Location = new Point(240, 8),
                Size = new Size(120, 36),
                Font = new DrawingFont("Segoe UI", 9F, FontStyle.Regular),
                BackColor = DrawingColor.White,
                ForeColor = DrawingColor.FromArgb(30, 41, 59),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnIsolateLayers.FlatAppearance.BorderColor = DrawingColor.FromArgb(203, 213, 225);
            btnIsolateLayers.Click += BtnIsolateLayers_Click;

            btnUnisolateLayers = new WinFormsButton
            {
                Text = "☀️ Bật tất cả",
                Location = new Point(366, 8),
                Size = new Size(100, 36),
                Font = new DrawingFont("Segoe UI", 9F, FontStyle.Regular),
                BackColor = DrawingColor.White,
                ForeColor = DrawingColor.FromArgb(30, 41, 59),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnUnisolateLayers.FlatAppearance.BorderColor = DrawingColor.FromArgb(203, 213, 225);
            btnUnisolateLayers.Click += BtnUnisolateLayers_Click;

            btnCopyLayerNames = new WinFormsButton
            {
                Text = "📋 Copy tên",
                Location = new Point(472, 8),
                Size = new Size(95, 36),
                Font = new DrawingFont("Segoe UI", 9F, FontStyle.Regular),
                BackColor = DrawingColor.White,
                ForeColor = DrawingColor.FromArgb(30, 41, 59),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnCopyLayerNames.FlatAppearance.BorderColor = DrawingColor.FromArgb(203, 213, 225);
            btnCopyLayerNames.Click += BtnCopyLayerNames_Click;

            btnClose = new WinFormsButton
            {
                Text = "Đóng",
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(730, 8),
                Size = new Size(95, 36),
                Font = new DrawingFont("Segoe UI", 9F, FontStyle.Regular),
                BackColor = DrawingColor.White,
                ForeColor = DrawingColor.FromArgb(71, 85, 105),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderColor = DrawingColor.FromArgb(203, 213, 225);
            btnClose.Click += (s, e) => this.Close();

            pnlBottom.Controls.Add(btnExecuteSelect);
            pnlBottom.Controls.Add(btnIsolateLayers);
            pnlBottom.Controls.Add(btnUnisolateLayers);
            pnlBottom.Controls.Add(btnCopyLayerNames);
            pnlBottom.Controls.Add(btnClose);

            // Add all in correct Dock order (Bottom controls first, Fill in middle)
            this.Controls.Add(dgvLayers);
            this.Controls.Add(grpOptions);
            this.Controls.Add(pnlBottom);
            this.Controls.Add(pnlSearch);
            this.Controls.Add(pnlToolbar);
            this.Controls.Add(pnlHeader);

            this.FormClosing += ChonTheoLayerForm_FormClosing;
            this.ResumeLayout(false);
        }

        private WinFormsButton CreateToolbarButton(string text, int x, int y, int width, EventHandler onClick)
        {
            var btn = new WinFormsButton
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, 30),
                Font = new DrawingFont("Segoe UI", 8.5F, FontStyle.Regular),
                BackColor = DrawingColor.White,
                ForeColor = DrawingColor.FromArgb(30, 41, 59),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = DrawingColor.FromArgb(203, 213, 225);
            btn.Click += onClick;
            return btn;
        }

        #endregion

        #region Data Loading & Scanning

        /// <summary>
        /// Nạp danh sách tất cả Layer và quét số lượng đối tượng trong bản vẽ
        /// </summary>
        private void LoadLayersData()
        {
            try
            {
                _allLayers.Clear();
                var layerDict = new Dictionary<string, LayerSelectionItem>(StringComparer.OrdinalIgnoreCase);

                using (var tr = _db.TransactionManager.StartTransaction())
                {
                    var lt = (LayerTable)tr.GetObject(_db.LayerTableId, OpenMode.ForRead);
                    foreach (ObjectId lId in lt)
                    {
                        var ltr = (LayerTableRecord)tr.GetObject(lId, OpenMode.ForRead);
                        var item = new LayerSelectionItem
                        {
                            LayerId = lId,
                            Name = ltr.Name,
                            CadColor = ltr.Color,
                            IsLocked = ltr.IsLocked,
                            IsOff = ltr.IsOff,
                            IsFrozen = ltr.IsFrozen,
                            IsSelected = _lastSelectedLayerNames.Contains(ltr.Name)
                        };

                        try
                        {
                            item.DrawingColor = ltr.Color.ColorValue;
                        }
                        catch
                        {
                            item.DrawingColor = DrawingColor.Black;
                        }

                        try
                        {
                            var ltrLt = tr.GetObject(ltr.LinetypeObjectId, OpenMode.ForRead) as LinetypeTableRecord;
                            item.Linetype = ltrLt?.Name ?? "Continuous";
                        }
                        catch
                        {
                            item.Linetype = "Continuous";
                        }

                        item.Description = ltr.Description ?? "";

                        layerDict[ltr.Name] = item;
                        _allLayers.Add(item);
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

                    // 3. Quét tất cả các BlockTableRecord là PaperSpace / Layouts để tính AllSpaces
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

                _allLayers = _allLayers.OrderBy(l => l.Name).ToList();
                ApplyFilterAndRefreshGrid();
            }
            catch (System.Exception ex)
            {
                _ed.WriteMessage($"\n❌ [Lỗi nạp danh sách Layer]: {ex.Message}");
            }
        }

        #endregion

        #region Grid Filtering & Rendering

        private int GetCurrentScopeIndex()
        {
            if (rbScopeModelSpace.Checked) return 1;
            if (rbScopeAllSpaces.Checked) return 2;
            if (rbScopeWindow.Checked) return 3;
            return 0; // Current space
        }

        private void UpdateCountsColumnHeader(int scopeIndex)
        {
            string header = scopeIndex switch
            {
                1 => "Đ.tượng (Model)",
                2 => "Đ.tượng (Tất cả)",
                3 => "Đ.tượng (Vùng chọn)",
                _ => "Đ.tượng (Hiện hành)"
            };
            dgvLayers.Columns["colCount"].HeaderText = header;
            ApplyFilterAndRefreshGrid();
        }

        private void ApplyFilterAndRefreshGrid()
        {
            if (_isUpdatingUi) return;
            _isUpdatingUi = true;

            try
            {
                string search = txtSearch.Text.Trim();
                int filterStatus = cboFilterStatus.SelectedIndex;
                int scope = GetCurrentScopeIndex();

                IEnumerable<LayerSelectionItem> query = _allLayers;

                // Lọc theo từ khóa tìm kiếm
                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(l => l.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
                }

                // Lọc theo trạng thái
                query = filterStatus switch
                {
                    1 => query.Where(l => l.GetCountForScope(scope) > 0),
                    2 => query.Where(l => l.IsSelected),
                    3 => query.Where(l => !l.IsOff),
                    4 => query.Where(l => l.IsLocked),
                    _ => query
                };

                _displayLayers = query.ToList();

                dgvLayers.Rows.Clear();
                foreach (var item in _displayLayers)
                {
                    int rowIndex = dgvLayers.Rows.Add(
                        item.IsSelected,
                        "",
                        item.Name,
                        item.GetCountForScope(scope),
                        item.StatusSummary,
                        item.Linetype
                    );

                    dgvLayers.Rows[rowIndex].Tag = item;

                    // Tô màu xám nếu layer không có đối tượng
                    if (item.GetCountForScope(scope) == 0)
                    {
                        dgvLayers.Rows[rowIndex].DefaultCellStyle.ForeColor = DrawingColor.FromArgb(148, 163, 184);
                    }
                    else
                    {
                        dgvLayers.Rows[rowIndex].DefaultCellStyle.ForeColor = DrawingColor.FromArgb(15, 23, 42);
                    }
                }

                UpdateSummaryLabel();
            }
            finally
            {
                _isUpdatingUi = false;
            }
        }

        private void UpdateSummaryLabel()
        {
            int scope = GetCurrentScopeIndex();
            int selectedCount = _allLayers.Count(l => l.IsSelected);
            int selectedObjects = _allLayers.Where(l => l.IsSelected).Sum(l => l.GetCountForScope(scope));
            int totalLayers = _allLayers.Count;

            lblStats.Text = $"Hiển thị: {_displayLayers.Count}/{totalLayers} layer | Đang chọn: {selectedCount} layer ({selectedObjects:N0} đối tượng)";
        }

        private void ToggleRowSelection(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= dgvLayers.Rows.Count) return;

            var row = dgvLayers.Rows[rowIndex];
            if (row.Tag is LayerSelectionItem item)
            {
                item.IsSelected = !item.IsSelected;
                row.Cells["colCheck"].Value = item.IsSelected;
                UpdateSummaryLabel();
            }
        }

        private void DgvLayers_CellContentClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == dgvLayers.Columns["colCheck"].Index)
            {
                dgvLayers.CommitEdit(DataGridViewDataErrorContexts.Commit);
                if (dgvLayers.Rows[e.RowIndex].Tag is LayerSelectionItem item)
                {
                    item.IsSelected = Convert.ToBoolean(dgvLayers.Rows[e.RowIndex].Cells["colCheck"].Value);
                    UpdateSummaryLabel();
                }
            }
        }

        private void DgvLayers_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && dgvLayers.Columns[e.ColumnIndex].Name == "colColor")
            {
                e.PaintBackground(e.ClipBounds, (e.State & DataGridViewElementStates.Selected) != 0);

                if (dgvLayers.Rows[e.RowIndex].Tag is LayerSelectionItem item)
                {
                    DrawingColor gdiColor;
                    try
                    {
                        gdiColor = item.CadColor.ColorValue;
                    }
                    catch
                    {
                        gdiColor = DrawingColor.Gray;
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

        #region Quick Pick Actions

        /// <summary>
        /// Tạm ẩn Form và pick 1 đối tượng trên bản vẽ (hỗ trợ cả đối tượng trong Block)
        /// </summary>
        private void BtnPickSample_Click(object? sender, EventArgs e)
        {
            try
            {
                using (var interaction = _ed.StartUserInteraction(this))
                {
                    var pneo = new PromptNestedEntityOptions("\n🎯 Chọn đối tượng mẫu trên bản vẽ (nhận diện được cả đối tượng bên trong Block): ");
                    var pner = _ed.GetNestedEntity(pneo);
                    if (pner.Status == PromptStatus.OK)
                    {
                        string pickedLayer = "";
                        using (var tr = _db.TransactionManager.StartTransaction())
                        {
                            var ent = tr.GetObject(pner.ObjectId, OpenMode.ForRead) as Entity;
                            if (ent != null)
                            {
                                pickedLayer = ent.Layer;
                            }
                            tr.Commit();
                        }

                        if (!string.IsNullOrEmpty(pickedLayer))
                        {
                            SelectAndHighlightLayer(pickedLayer);
                            _ed.WriteMessage($"\n✅ Đã chọn Layer: \"{pickedLayer}\"");
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                _ed.WriteMessage($"\n❌ [Lỗi pick mẫu]: {ex.Message}");
            }
        }

        /// <summary>
        /// Tạm ẩn Form và quét chọn nhiều đối tượng mẫu trên bản vẽ
        /// </summary>
        private void BtnPickWindowSample_Click(object? sender, EventArgs e)
        {
            try
            {
                using (var interaction = _ed.StartUserInteraction(this))
                {
                    var pso = new PromptSelectionOptions
                    {
                        MessageForAdding = "\n🔲 Quét chọn các đối tượng mẫu trên bản vẽ: "
                    };

                    var psr = _ed.GetSelection(pso);
                    if (psr.Status == PromptStatus.OK && psr.Value != null)
                    {
                        var pickedLayers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        using (var tr = _db.TransactionManager.StartTransaction())
                        {
                            foreach (SelectedObject so in psr.Value)
                            {
                                if (so == null) continue;
                                var ent = tr.GetObject(so.ObjectId, OpenMode.ForRead) as Entity;
                                if (ent != null)
                                {
                                    pickedLayers.Add(ent.Layer);
                                }
                            }
                            tr.Commit();
                        }

                        if (pickedLayers.Count > 0)
                        {
                            foreach (var itm in _allLayers)
                            {
                                if (pickedLayers.Contains(itm.Name))
                                {
                                    itm.IsSelected = true;
                                }
                            }
                            ApplyFilterAndRefreshGrid();
                            ScrollToLayer(pickedLayers.First());
                            _ed.WriteMessage($"\n✅ Đã chọn {pickedLayers.Count} Layer từ tập đối tượng mẫu.");
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                _ed.WriteMessage($"\n❌ [Lỗi quét chọn mẫu]: {ex.Message}");
            }
        }

        private void SelectAndHighlightLayer(string layerName)
        {
            var item = _allLayers.FirstOrDefault(l => string.Equals(l.Name, layerName, StringComparison.OrdinalIgnoreCase));
            if (item != null)
            {
                item.IsSelected = true;
                ApplyFilterAndRefreshGrid();
                ScrollToLayer(layerName);
            }
        }

        private void ScrollToLayer(string layerName)
        {
            for (int i = 0; i < dgvLayers.Rows.Count; i++)
            {
                if (dgvLayers.Rows[i].Tag is LayerSelectionItem itm &&
                    string.Equals(itm.Name, layerName, StringComparison.OrdinalIgnoreCase))
                {
                    dgvLayers.ClearSelection();
                    dgvLayers.Rows[i].Selected = true;
                    dgvLayers.FirstDisplayedScrollingRowIndex = i;
                    break;
                }
            }
        }

        private void BtnSelectAll_Click(object? sender, EventArgs e)
        {
            foreach (var item in _displayLayers) item.IsSelected = true;
            ApplyFilterAndRefreshGrid();
        }

        private void BtnDeselectAll_Click(object? sender, EventArgs e)
        {
            foreach (var item in _allLayers) item.IsSelected = false;
            ApplyFilterAndRefreshGrid();
        }

        private void BtnInvertSelect_Click(object? sender, EventArgs e)
        {
            foreach (var item in _displayLayers) item.IsSelected = !item.IsSelected;
            ApplyFilterAndRefreshGrid();
        }

        private void BtnSelectNonEmpty_Click(object? sender, EventArgs e)
        {
            int scope = GetCurrentScopeIndex();
            foreach (var item in _allLayers)
            {
                item.IsSelected = item.GetCountForScope(scope) > 0;
            }
            ApplyFilterAndRefreshGrid();
        }

        #endregion

        #region Execution & Selection

        /// <summary>
        /// Nút thực thi chính: Chọn tất cả đối tượng thuộc các Layer đã chọn
        /// </summary>
        private void BtnExecuteSelect_Click(object? sender, EventArgs e)
        {
            var selectedLayerItems = _allLayers.Where(l => l.IsSelected).ToList();
            if (selectedLayerItems.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn ít nhất một Layer để chọn đối tượng.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selectedLayerNames = new HashSet<string>(selectedLayerItems.Select(l => l.Name), StringComparer.OrdinalIgnoreCase);
            bool ignoreLocked = chkIgnoreLocked.Checked;
            bool zoom = chkZoomSelected.Checked;
            int scope = GetCurrentScopeIndex();

            SaveCurrentSettings();

            List<ObjectId> resultIds = new();

            try
            {
                if (scope == 3) // Quét chọn vùng trên bản vẽ
                {
                    using (var interaction = _ed.StartUserInteraction(this))
                    {
                        var pso = new PromptSelectionOptions
                        {
                            MessageForAdding = "\n🎯 Quét chọn vùng đối tượng cần lọc theo layer đã chọn: "
                        };
                        var psr = _ed.GetSelection(pso);
                        if (psr.Status == PromptStatus.OK && psr.Value != null)
                        {
                            using (var tr = _db.TransactionManager.StartTransaction())
                            {
                                foreach (SelectedObject so in psr.Value)
                                {
                                    if (so == null) continue;
                                    var ent = tr.GetObject(so.ObjectId, OpenMode.ForRead) as Entity;
                                    if (ent == null) continue;

                                    if (selectedLayerNames.Contains(ent.Layer))
                                    {
                                        if (ignoreLocked)
                                        {
                                            var lt = (LayerTable)tr.GetObject(_db.LayerTableId, OpenMode.ForRead);
                                            if (lt.Has(ent.Layer))
                                            {
                                                var ltr = (LayerTableRecord)tr.GetObject(lt[ent.Layer], OpenMode.ForRead);
                                                if (ltr.IsLocked) continue;
                                            }
                                        }
                                        resultIds.Add(so.ObjectId);
                                    }
                                }
                                tr.Commit();
                            }
                        }
                    }
                }
                else
                {
                    // Lấy đối tượng theo Space
                    using (var tr = _db.TransactionManager.StartTransaction())
                    {
                        var lockedLayerNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        if (ignoreLocked)
                        {
                            var lt = (LayerTable)tr.GetObject(_db.LayerTableId, OpenMode.ForRead);
                            foreach (ObjectId lId in lt)
                            {
                                var ltr = (LayerTableRecord)tr.GetObject(lId, OpenMode.ForRead);
                                if (ltr.IsLocked) lockedLayerNames.Add(ltr.Name);
                            }
                        }

                        var bt = (BlockTable)tr.GetObject(_db.BlockTableId, OpenMode.ForRead);
                        var targetBtrs = new List<BlockTableRecord>();

                        if (scope == 1) // Model Space
                        {
                            // Nếu đang ở Layout thì chuyển về Model Space
                            if (!_db.TileMode)
                            {
                                _db.TileMode = true;
                            }
                            targetBtrs.Add((BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead));
                        }
                        else if (scope == 2) // Toàn bộ bản vẽ
                        {
                            // Model space
                            targetBtrs.Add((BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead));
                            // Tất cả layout
                            foreach (ObjectId btrId in bt)
                            {
                                var btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
                                if (btr.IsLayout && btrId != bt[BlockTableRecord.ModelSpace])
                                {
                                    targetBtrs.Add(btr);
                                }
                            }
                        }
                        else // Scope 0: Current Space
                        {
                            targetBtrs.Add((BlockTableRecord)tr.GetObject(_db.CurrentSpaceId, OpenMode.ForRead));
                        }

                        foreach (var btr in targetBtrs)
                        {
                            foreach (ObjectId entId in btr)
                            {
                                if (entId.IsErased || !entId.IsValid) continue;
                                var ent = tr.GetObject(entId, OpenMode.ForRead) as Entity;
                                if (ent == null) continue;

                                if (selectedLayerNames.Contains(ent.Layer))
                                {
                                    if (ignoreLocked && lockedLayerNames.Contains(ent.Layer)) continue;
                                    resultIds.Add(entId);
                                }
                            }
                        }

                        tr.Commit();
                    }
                }

                if (resultIds.Count == 0)
                {
                    _ed.WriteMessage("\n⚠️ Không tìm thấy đối tượng nào thỏa mãn các Layer và tùy chọn đã chọn.");
                    MessageBox.Show("Không tìm thấy đối tượng nào thỏa mãn trên các Layer đã chọn.", "Kết quả", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                // Đặt vào tập chọn hiện hành (Pickfirst Selection)
                // Lưu ý: SetImpliedSelection chỉ hiển thị grips cho các đối tượng thuộc CurrentSpace
                var currentSpaceIds = resultIds.Where(id =>
                {
                    using (var tr = _db.TransactionManager.StartTransaction())
                    {
                        var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        bool match = ent != null && ent.BlockId == _db.CurrentSpaceId;
                        tr.Commit();
                        return match;
                    }
                }).ToArray();

                if (currentSpaceIds.Length > 0)
                {
                    _ed.SetImpliedSelection(currentSpaceIds);
                }

                // Zoom nếu được chọn
                if (zoom)
                {
                    ZoomToObjects(_ed, _db, currentSpaceIds.Length > 0 ? currentSpaceIds : resultIds);
                }

                _ed.WriteMessage($"\n═══════════════════════════════════════════════════════════════");
                _ed.WriteMessage($"\n  ✅ ĐÃ CHỌN THÀNH CÔNG {resultIds.Count:N0} ĐỐI TƯỢNG THUỘC {selectedLayerNames.Count} LAYER");
                _ed.WriteMessage($"\n  👉 Đối tượng đã được đặt vào tập chọn hiện hành (Grips bật).");
                _ed.WriteMessage($"\n  👉 Bạn có thể dùng ngay các lệnh: MOVE, COPY, ERASE, PROPERTIES, ROTATE...");
                _ed.WriteMessage($"\n═══════════════════════════════════════════════════════════════\n");

                if (chkCloseOnSelect.Checked)
                {
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
            }
            catch (System.Exception ex)
            {
                _ed.WriteMessage($"\n❌ [Lỗi chọn đối tượng]: {ex.Message}");
            }
        }

        #endregion

        #region Layer Utilities (Isolate, Unisolate, Copy)

        private void BtnIsolateLayers_Click(object? sender, EventArgs e)
        {
            var selectedLayerItems = _allLayers.Where(l => l.IsSelected).ToList();
            if (selectedLayerItems.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn ít nhất một Layer để cô lập.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selectedNames = new HashSet<string>(selectedLayerItems.Select(l => l.Name), StringComparer.OrdinalIgnoreCase);

            try
            {
                int turnedOff = 0;
                using (var tr = _db.TransactionManager.StartTransaction())
                {
                    var lt = (LayerTable)tr.GetObject(_db.LayerTableId, OpenMode.ForRead);
                    foreach (ObjectId lId in lt)
                    {
                        var ltr = (LayerTableRecord)tr.GetObject(lId, OpenMode.ForWrite);
                        if (!selectedNames.Contains(ltr.Name))
                        {
                            if (!ltr.IsOff)
                            {
                                ltr.IsOff = true;
                                turnedOff++;
                            }
                        }
                        else
                        {
                            if (ltr.IsOff) ltr.IsOff = false;
                        }
                    }
                    tr.Commit();
                }

                _ed.Regen();
                LoadLayersData();
                _ed.WriteMessage($"\n✅ Đã cô lập {selectedNames.Count} Layer (Đã tắt {turnedOff} layer khác).");
            }
            catch (System.Exception ex)
            {
                _ed.WriteMessage($"\n❌ [Lỗi cô lập Layer]: {ex.Message}");
            }
        }

        private void BtnUnisolateLayers_Click(object? sender, EventArgs e)
        {
            try
            {
                int turnedOn = 0;
                using (var tr = _db.TransactionManager.StartTransaction())
                {
                    var lt = (LayerTable)tr.GetObject(_db.LayerTableId, OpenMode.ForRead);
                    foreach (ObjectId lId in lt)
                    {
                        var ltr = (LayerTableRecord)tr.GetObject(lId, OpenMode.ForWrite);
                        if (ltr.IsOff)
                        {
                            ltr.IsOff = false;
                            turnedOn++;
                        }
                    }
                    tr.Commit();
                }

                _ed.Regen();
                LoadLayersData();
                _ed.WriteMessage($"\n✅ Đã bật lại tất cả Layer ({turnedOn} layer được bật lại).");
            }
            catch (System.Exception ex)
            {
                _ed.WriteMessage($"\n❌ [Lỗi bật tất cả Layer]: {ex.Message}");
            }
        }

        private void BtnCopyLayerNames_Click(object? sender, EventArgs e)
        {
            var selectedLayers = _allLayers.Where(l => l.IsSelected).ToList();
            if (selectedLayers.Count == 0)
            {
                MessageBox.Show("Chưa có Layer nào được chọn.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int scope = GetCurrentScopeIndex();
            var lines = selectedLayers.Select(l => $"{l.Name}\t{l.GetCountForScope(scope)} đối tượng\t{l.StatusSummary}");
            string content = string.Join(Environment.NewLine, lines);

            try
            {
                Clipboard.SetText(content);
                MessageBox.Show($"Đã sao chép {selectedLayers.Count} tên Layer vào Clipboard!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Lỗi copy: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        #region Helper: Zoom to Extents

        private static void ZoomToObjects(Editor ed, Database db, IEnumerable<ObjectId> objectIds)
        {
            try
            {
                Extents3d? combinedExtents = null;
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    foreach (var id in objectIds)
                    {
                        if (id.IsErased || !id.IsValid) continue;
                        var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        if (ent == null) continue;

                        try
                        {
                            Extents3d ext = ent.GeometricExtents;
                            if (combinedExtents == null)
                            {
                                combinedExtents = ext;
                            }
                            else
                            {
                                var cur = combinedExtents.Value;
                                cur.AddExtents(ext);
                                combinedExtents = cur;
                            }
                        }
                        catch { }
                    }
                    tr.Commit();
                }

                if (combinedExtents.HasValue)
                {
                    ZoomToExtents(ed, combinedExtents.Value);
                }
            }
            catch { }
        }

        private static void ZoomToExtents(Editor ed, Extents3d extents)
        {
            try
            {
                using (ViewTableRecord view = ed.GetCurrentView())
                {
                    double width = Math.Abs(extents.MaxPoint.X - extents.MinPoint.X);
                    double height = Math.Abs(extents.MaxPoint.Y - extents.MinPoint.Y);

                    if (width < 2.0) width = 10.0;
                    if (height < 2.0) height = 10.0;

                    Point2d center = new Point2d(
                        (extents.MinPoint.X + extents.MaxPoint.X) / 2.0,
                        (extents.MinPoint.Y + extents.MaxPoint.Y) / 2.0
                    );

                    double margin = 1.25;
                    view.CenterPoint = center;
                    view.Height = Math.Max(height, width) * margin;
                    view.Width = Math.Max(height, width) * margin;

                    ed.SetCurrentView(view);
                }
            }
            catch { }
        }

        #endregion

        #region Persistent State Implementation (RULE 2)

        private void RestoreLastSettings()
        {
            try
            {
                if (_lastFormSize.Width >= 700 && _lastFormSize.Height >= 500)
                {
                    this.Size = _lastFormSize;
                }

                if (_lastFormLocation != Point.Empty)
                {
                    this.StartPosition = FormStartPosition.Manual;
                    this.Location = _lastFormLocation;
                }

                switch (_lastScopeIndex)
                {
                    case 1: rbScopeModelSpace.Checked = true; break;
                    case 2: rbScopeAllSpaces.Checked = true; break;
                    case 3: rbScopeWindow.Checked = true; break;
                    default: rbScopeCurrentSpace.Checked = true; break;
                }

                chkIgnoreLocked.Checked = _lastIgnoreLocked;
                chkZoomSelected.Checked = _lastZoomSelected;
                chkCloseOnSelect.Checked = _lastCloseOnSelect;

                if (!string.IsNullOrEmpty(_lastSearchText))
                {
                    txtSearch.Text = _lastSearchText;
                }

                if (_lastFilterStatusIndex >= 0 && _lastFilterStatusIndex < cboFilterStatus.Items.Count)
                {
                    cboFilterStatus.SelectedIndex = _lastFilterStatusIndex;
                }
            }
            catch { }
        }

        private void SaveCurrentSettings()
        {
            try
            {
                _lastSelectedLayerNames.Clear();
                foreach (var item in _allLayers.Where(l => l.IsSelected))
                {
                    _lastSelectedLayerNames.Add(item.Name);
                }

                _lastScopeIndex = GetCurrentScopeIndex();
                _lastIgnoreLocked = chkIgnoreLocked.Checked;
                _lastZoomSelected = chkZoomSelected.Checked;
                _lastCloseOnSelect = chkCloseOnSelect.Checked;
                _lastSearchText = txtSearch.Text;
                _lastFilterStatusIndex = cboFilterStatus.SelectedIndex;

                if (this.WindowState == FormWindowState.Normal)
                {
                    _lastFormSize = this.Size;
                    _lastFormLocation = this.Location;
                }
            }
            catch { }
        }

        private void ChonTheoLayerForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            SaveCurrentSettings();
        }

        #endregion
    }
}

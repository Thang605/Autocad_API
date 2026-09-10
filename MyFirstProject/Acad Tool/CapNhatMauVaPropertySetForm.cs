// (C) Copyright 2026 by T27
//
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using ClosedXML.Excel;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Color = Autodesk.AutoCAD.Colors.Color;
using DrawingColor = System.Drawing.Color;
using DrawingFont = System.Drawing.Font;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsPoint = System.Drawing.Point;
using MyFirstProject.Extensions;

namespace Civil3DCsharp
{
    /// <summary>
    /// Model dữ liệu cho mỗi dòng Layer trong bảng phối hợp Màu & Property Set
    /// </summary>
    public class LayerBimRowModel
    {
        public bool IsSelected { get; set; } = false;
        public string LayerName { get; set; } = "";
        public int SolidCount { get; set; }
        public int BodyCount { get; set; }
        public int TotalCount => SolidCount + BodyCount;

        public string SelectedPresetCode { get; set; } = ""; // Mã mẫu (hoặc "CUSTOM" / "")
        public string CauKien { get; set; } = "";             // Hạng mục / Cấu kiện (Property Set "Cấu kiện" & Description)
        public string VatLieu { get; set; } = "";             // Vật liệu (Property Set "Vật liệu" & Description)

        public byte? NewR { get; set; }
        public byte? NewG { get; set; }
        public byte? NewB { get; set; }

        public DrawingColor CurrentColor { get; set; }
        public string CurrentColorDesc { get; set; } = "";
        public string CurrentDescription { get; set; } = "";
        public string Linetype { get; set; } = "";
        public bool IsLocked { get; set; }
        public bool IsFrozen { get; set; }
        public bool IsOff { get; set; }

        public DrawingColor? NewColor => (NewR.HasValue && NewG.HasValue && NewB.HasValue) ? DrawingColor.FromArgb(NewR.Value, NewG.Value, NewB.Value) : (DrawingColor?)null;
        public string NewRgbText => (NewR.HasValue && NewG.HasValue && NewB.HasValue) ? $"({NewR.Value}, {NewG.Value}, {NewB.Value})" : "---";
        public string NewHexText => (NewR.HasValue && NewG.HasValue && NewB.HasValue) ? $"#{NewR.Value:X2}{NewG.Value:X2}{NewB.Value:X2}" : "---";
    }

    /// <summary>
    /// Lưu vết trạng thái cho Layer giữa các lần chạy
    /// </summary>
    public class LayerBimSavedState
    {
        public string PresetCode { get; set; } = "";
        public string CauKien { get; set; } = "";
        public string VatLieu { get; set; } = "";
        public byte? R { get; set; }
        public byte? G { get; set; }
        public byte? B { get; set; }
        public bool IsSelected { get; set; }
    }

    /// <summary>
    /// Form giao diện phối hợp Cập Nhật Màu Layer và Cập Nhật Property Set theo mẫu BIM
    /// Hỗ trợ Xuất/Nhập Excel, Tự động nhận diện từ khóa, Pick CAD và ghi nhớ toàn bộ thông số.
    /// </summary>
    public class CapNhatMauVaPropertySetForm : Form
    {
        #region Persistent State (Ghi nhớ giữa các lần chạy)
        private static Dictionary<string, LayerBimSavedState> _savedLayerStates = new(StringComparer.OrdinalIgnoreCase);
        private static string _lastPropertySetName = PropertySetUtils.DefaultPropertySetName;
        private static string _lastExcelPath = "";
        private static bool _lastUpdateColor = true;
        private static bool _lastUpdatePropSet = true;
        private static bool _lastUpdateDescription = true;
        private static bool _lastApplyByLayer = true;
        private static bool _lastUnlockLayers = true;
        private static Size _lastFormSize = new Size(1180, 760);
        private static int _lastSelectedTab = 0;
        #endregion

        #region UI Controls
        private Panel pnlHeader = null!;
        private Label lblHeaderTitle = null!;
        private Label lblHeaderSub = null!;

        private TabControl tabControlMain = null!;
        private TabPage tabMainCoordination = null!;
        private TabPage tabPresetReference = null!;

        // Tab 1: Toolbar & Search
        private Panel pnlToolbar = null!;
        private Label lblSearch = null!;
        private TextBox txtSearch = null!;
        private Button btnPickObject = null!;
        private Button btnAutoMatchAll = null!;
        private Button btnSelectAll = null!;
        private Button btnDeselectAll = null!;
        private Button btnRefresh = null!;

        // Tab 1: Quick Batch Assign
        private Panel pnlBatch = null!;
        private Label lblBatch = null!;
        private ComboBox cboBatchPreset = null!;
        private Button btnApplyBatchPreset = null!;
        private Label lblStats = null!;

        private DataGridView dgvLayers = null!;

        // Tab 2: Preset Reference Grid
        private DataGridView dgvPresets = null!;

        // Bottom Configuration & Actions
        private Panel pnlBottom = null!;
        private GroupBox grpPropSetAndExcel = null!;
        private Label lblPropSetName = null!;
        private TextBox txtPropSetName = null!;
        private Button btnImportExcel = null!;
        private Button btnExportExcel = null!;

        private GroupBox grpOptions = null!;
        private CheckBox chkUpdateColor = null!;
        private CheckBox chkUpdatePropSet = null!;
        private CheckBox chkUpdateDescription = null!;
        private CheckBox chkApplyByLayer = null!;
        private CheckBox chkUnlockLayers = null!;

        private GroupBox grpLog = null!;
        private TextBox txtLog = null!;

        private Panel pnlButtons = null!;
        private Button btnExecute = null!;
        private Button btnClose = null!;
        #endregion

        private List<BimStandardPreset> _presets = new();
        private List<LayerBimRowModel> _allLayerRows = new();

        public CapNhatMauVaPropertySetForm()
        {
            InitializeComponent();
            _presets = CapNhatMauLayerForm.GetDefaultPresets();
            LoadDataFromDrawing();
            PopulatePresetReferenceGrid();
            PopulateLayersGrid();
            RestoreLastSettings();
        }

        private void InitializeComponent()
        {
            var regularFont = new DrawingFont("Segoe UI", 9F, FontStyle.Regular);
            var boldFont = new DrawingFont("Segoe UI", 9F, FontStyle.Bold);
            var titleFont = new DrawingFont("Segoe UI", 12F, FontStyle.Bold);
            var subFont = new DrawingFont("Segoe UI", 8.25F, FontStyle.Regular);

            this.SuspendLayout();
            this.Text = "Phối Hợp Cập Nhật Màu Layer & Cập Nhật Property Set Theo Mẫu Chuẩn BIM / EIR";
            this.Size = _lastFormSize;
            this.MinimumSize = new Size(1000, 620);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = regularFont;
            this.BackColor = DrawingColor.FromArgb(246, 248, 250);

            // ================= 1. HEADER BANNER =================
            pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 65,
                BackColor = DrawingColor.FromArgb(20, 38, 66),
                Padding = new Padding(16, 8, 16, 8)
            };

            lblHeaderTitle = new Label
            {
                Text = "⚡ PHỐI HỢP CẬP NHẬT MÀU LAYER VÀ PROPERTY SET THEO MẪU CHUẨN BIM",
                Font = titleFont,
                ForeColor = DrawingColor.White,
                AutoSize = true,
                Location = new Point(14, 10)
            };

            lblHeaderSub = new Label
            {
                Text = "Chọn Cấu kiện & Vật liệu -> Tự động nhận Màu TrueColor và Property Set tương ứng. Hỗ trợ Xuất / Nhập Excel tái sử dụng.",
                Font = subFont,
                ForeColor = DrawingColor.FromArgb(195, 215, 245),
                AutoSize = true,
                Location = new Point(16, 36)
            };

            pnlHeader.Controls.Add(lblHeaderTitle);
            pnlHeader.Controls.Add(lblHeaderSub);

            // ================= 2. TAB CONTROL =================
            tabControlMain = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = boldFont,
                Padding = new Point(12, 6)
            };

            tabMainCoordination = new TabPage { Text = "📑 Danh Sách Layer & Gán Thuộc Tính BIM", BackColor = DrawingColor.White };
            tabPresetReference = new TabPage { Text = "📋 Bảng Tra Cứu Mẫu Màu & Cấu Kiện Chuẩn (BEP T27)", BackColor = DrawingColor.White };

            tabControlMain.TabPages.Add(tabMainCoordination);
            tabControlMain.TabPages.Add(tabPresetReference);

            BuildTabMainCoordination(regularFont, boldFont);
            BuildTabPresetReference(regularFont, boldFont);

            // ================= 3. BOTTOM AREA =================
            pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 240,
                Padding = new Padding(10, 4, 10, 4),
                BackColor = DrawingColor.FromArgb(246, 248, 250)
            };

            // Group 1: Property Set & Excel Config
            grpPropSetAndExcel = new GroupBox
            {
                Text = "🛠️ Thiết lập Property Set & Tái Sử Dụng Cấu Hình (Excel)",
                Font = boldFont,
                Dock = DockStyle.Top,
                Height = 56,
                Padding = new Padding(8, 2, 8, 2)
            };

            lblPropSetName = new Label
            {
                Text = "Tên Property Set:",
                AutoSize = true,
                Font = boldFont,
                Location = new Point(12, 22)
            };

            txtPropSetName = new TextBox
            {
                Location = new Point(126, 19),
                Width = 230,
                Font = regularFont,
                Text = _lastPropertySetName
            };

            btnImportExcel = CreateFlatButton("📥 Nhập từ Excel (.xlsx)", 175, DrawingColor.FromArgb(13, 110, 253), boldFont);
            btnImportExcel.Location = new Point(370, 16);
            btnImportExcel.Click += BtnImportExcel_Click;

            btnExportExcel = CreateFlatButton("📤 Xuất ra Excel (.xlsx)", 175, DrawingColor.FromArgb(25, 135, 84), boldFont);
            btnExportExcel.Location = new Point(555, 16);
            btnExportExcel.Click += BtnExportExcel_Click;

            grpPropSetAndExcel.Controls.Add(lblPropSetName);
            grpPropSetAndExcel.Controls.Add(txtPropSetName);
            grpPropSetAndExcel.Controls.Add(btnImportExcel);
            grpPropSetAndExcel.Controls.Add(btnExportExcel);

            // Group 2: Options
            grpOptions = new GroupBox
            {
                Text = "⚙️ Tùy chọn thực thi",
                Font = boldFont,
                Dock = DockStyle.Top,
                Height = 50,
                Padding = new Padding(8, 2, 8, 2)
            };

            chkUpdateColor = new CheckBox
            {
                Text = "Đổi Màu Layer (TrueColor)",
                Font = regularFont,
                AutoSize = true,
                Location = new Point(12, 20),
                Checked = _lastUpdateColor
            };

            chkUpdatePropSet = new CheckBox
            {
                Text = "Gán / Cập nhật Property Set cho 3D Solid & Body",
                Font = regularFont,
                AutoSize = true,
                Location = new Point(205, 20),
                Checked = _lastUpdatePropSet
            };

            chkUpdateDescription = new CheckBox
            {
                Text = "Ghi Description ([Cấu kiện] | [Vật liệu])",
                Font = regularFont,
                AutoSize = true,
                Location = new Point(515, 20),
                Checked = _lastUpdateDescription
            };

            chkApplyByLayer = new CheckBox
            {
                Text = "Chuyển đối tượng về ByLayer",
                Font = regularFont,
                AutoSize = true,
                Location = new Point(785, 20),
                Checked = _lastApplyByLayer
            };

            chkUnlockLayers = new CheckBox
            {
                Text = "Tự mở khóa Layer",
                Font = regularFont,
                AutoSize = true,
                Location = new Point(995, 20),
                Checked = _lastUnlockLayers
            };

            grpOptions.Controls.Add(chkUpdateColor);
            grpOptions.Controls.Add(chkUpdatePropSet);
            grpOptions.Controls.Add(chkUpdateDescription);
            grpOptions.Controls.Add(chkApplyByLayer);
            grpOptions.Controls.Add(chkUnlockLayers);

            // Group 3: Log
            grpLog = new GroupBox
            {
                Text = "📝 Nhật ký hoạt động",
                Font = boldFont,
                Dock = DockStyle.Fill,
                Padding = new Padding(6, 3, 6, 3)
            };

            txtLog = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                Font = new DrawingFont("Consolas", 8.5F),
                BackColor = DrawingColor.FromArgb(250, 250, 250)
            };
            grpLog.Controls.Add(txtLog);

            // Panel Action Buttons
            pnlButtons = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                BackColor = DrawingColor.FromArgb(235, 239, 244),
                Padding = new Padding(10, 6, 16, 6)
            };

            btnExecute = new Button
            {
                Text = "🚀 ÁP DỤNG CẬP NHẬT MÀU & PROPERTY SET",
                Font = boldFont,
                BackColor = DrawingColor.FromArgb(13, 110, 253),
                ForeColor = DrawingColor.White,
                FlatStyle = FlatStyle.Flat,
                Height = 36,
                Width = 370,
                Location = new Point(pnlButtons.Width - 500, 6),
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                Cursor = Cursors.Hand
            };
            btnExecute.FlatAppearance.BorderSize = 0;
            btnExecute.Click += BtnExecute_Click;

            btnClose = new Button
            {
                Text = "❌ Đóng",
                Font = boldFont,
                BackColor = DrawingColor.FromArgb(108, 117, 125),
                ForeColor = DrawingColor.White,
                FlatStyle = FlatStyle.Flat,
                Height = 36,
                Width = 110,
                Location = new Point(pnlButtons.Width - 120, 6),
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => this.Close();

            pnlButtons.Controls.Add(btnExecute);
            pnlButtons.Controls.Add(btnClose);

            pnlBottom.Controls.Add(grpLog);
            pnlBottom.Controls.Add(grpOptions);
            pnlBottom.Controls.Add(grpPropSetAndExcel);

            this.Controls.Add(tabControlMain);
            this.Controls.Add(pnlBottom);
            this.Controls.Add(pnlButtons);
            this.Controls.Add(pnlHeader);

            this.FormClosing += CapNhatMauVaPropertySetForm_FormClosing;
            this.ResumeLayout(false);
        }

        #region Build Tab 1: Coordination List
        private void BuildTabMainCoordination(DrawingFont regularFont, DrawingFont boldFont)
        {
            // Panel 1: Filter & Action Buttons
            pnlToolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = DrawingColor.FromArgb(240, 243, 248),
                Padding = new Padding(8, 4, 8, 4)
            };

            lblSearch = new Label
            {
                Text = "🔍 Tìm kiếm:",
                AutoSize = true,
                Font = boldFont,
                Location = new Point(8, 10)
            };

            txtSearch = new TextBox
            {
                Width = 160,
                Location = new Point(90, 7),
                Font = regularFont
            };
            txtSearch.TextChanged += (s, e) => FilterLayersGrid();

            btnPickObject = CreateFlatButton("🎯 Pick CAD", 105, DrawingColor.FromArgb(111, 66, 193), boldFont);
            btnPickObject.Location = new Point(258, 5);
            btnPickObject.Click += BtnPickObject_Click;

            btnAutoMatchAll = CreateFlatButton("⚡ Tự động nhận diện (Auto-Match)", 245, DrawingColor.FromArgb(25, 135, 84), boldFont);
            btnAutoMatchAll.Location = new Point(370, 5);
            btnAutoMatchAll.Click += BtnAutoMatchAll_Click;

            btnSelectAll = CreateFlatButton("☑ Chọn hết", 90, DrawingColor.FromArgb(70, 80, 95), regularFont);
            btnSelectAll.Location = new Point(622, 5);
            btnSelectAll.Click += (s, e) => SetAllLayersChecked(true);

            btnDeselectAll = CreateFlatButton("⬜ Bỏ chọn", 85, DrawingColor.FromArgb(100, 110, 125), regularFont);
            btnDeselectAll.Location = new Point(718, 5);
            btnDeselectAll.Click += (s, e) => SetAllLayersChecked(false);

            btnRefresh = CreateFlatButton("🔄 Quét lại CAD", 115, DrawingColor.FromArgb(13, 110, 253), regularFont);
            btnRefresh.Location = new Point(810, 5);
            btnRefresh.Click += (s, e) =>
            {
                LoadDataFromDrawing();
                PopulateLayersGrid();
                AppendLog("Đã nạp lại danh sách Layer và thống kê đối tượng 3D từ bản vẽ.");
            };

            pnlToolbar.Controls.AddRange(new Control[]
            {
                lblSearch, txtSearch, btnPickObject, btnAutoMatchAll, btnSelectAll, btnDeselectAll, btnRefresh
            });

            // Panel 2: Batch Assign & Stats
            pnlBatch = new Panel
            {
                Dock = DockStyle.Top,
                Height = 36,
                BackColor = DrawingColor.FromArgb(248, 249, 250),
                Padding = new Padding(8, 3, 8, 3)
            };

            lblBatch = new Label
            {
                Text = "⚡ Gán nhanh mẫu cho Layer đã chọn:",
                AutoSize = true,
                Font = boldFont,
                ForeColor = DrawingColor.FromArgb(10, 50, 100),
                Location = new Point(8, 8)
            };

            cboBatchPreset = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 320,
                Location = new Point(265, 5),
                Font = regularFont
            };

            btnApplyBatchPreset = CreateFlatButton("👉 Áp dụng", 90, DrawingColor.FromArgb(220, 53, 69), boldFont);
            btnApplyBatchPreset.Location = new Point(592, 4);
            btnApplyBatchPreset.Click += BtnApplyBatchPreset_Click;

            lblStats = new Label
            {
                Text = "Đang tải dữ liệu...",
                AutoSize = true,
                Font = boldFont,
                ForeColor = DrawingColor.FromArgb(20, 80, 150),
                Location = new Point(695, 8)
            };

            pnlBatch.Controls.AddRange(new Control[]
            {
                lblBatch, cboBatchPreset, btnApplyBatchPreset, lblStats
            });

            // DataGridView dgvLayers
            dgvLayers = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = DrawingColor.White,
                BorderStyle = BorderStyle.None,
                GridColor = DrawingColor.FromArgb(230, 235, 240),
                RowTemplate = { Height = 28 },
                Font = regularFont,
                EnableHeadersVisualStyles = false
            };

            dgvLayers.ColumnHeadersDefaultCellStyle.BackColor = DrawingColor.FromArgb(238, 242, 248);
            dgvLayers.ColumnHeadersDefaultCellStyle.ForeColor = DrawingColor.FromArgb(20, 35, 60);
            dgvLayers.ColumnHeadersDefaultCellStyle.Font = boldFont;
            dgvLayers.ColumnHeadersHeight = 32;

            var colCheck = new DataGridViewCheckBoxColumn
            {
                HeaderText = "Chọn",
                Width = 45,
                Name = "colCheck",
                HeaderCell = { Style = { Alignment = DataGridViewContentAlignment.MiddleCenter } }
            };

            var colLayerName = new DataGridViewTextBoxColumn
            {
                HeaderText = "Tên Layer CAD",
                Width = 160,
                ReadOnly = true,
                Name = "colLayerName"
            };

            var colSolidCount = new DataGridViewTextBoxColumn
            {
                HeaderText = "Solid 3D",
                Width = 65,
                ReadOnly = true,
                Name = "colSolidCount",
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight }
            };

            var colBodyCount = new DataGridViewTextBoxColumn
            {
                HeaderText = "3D Body",
                Width = 65,
                ReadOnly = true,
                Name = "colBodyCount",
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight }
            };

            var colPreset = new DataGridViewComboBoxColumn
            {
                HeaderText = "Chọn Mẫu BIM / EIR (BEP T27)",
                Width = 240,
                Name = "colPreset",
                FlatStyle = FlatStyle.Flat
            };

            var colCauKien = new DataGridViewTextBoxColumn
            {
                HeaderText = "Cấu kiện (Property Set)",
                Width = 175,
                Name = "colCauKien"
            };

            var colVatLieu = new DataGridViewTextBoxColumn
            {
                HeaderText = "Vật liệu (Property Set)",
                Width = 175,
                Name = "colVatLieu"
            };

            var colNewColorPreview = new DataGridViewTextBoxColumn
            {
                HeaderText = "Màu mới",
                Width = 65,
                ReadOnly = true,
                Name = "colNewColorPreview"
            };

            var colNewRgb = new DataGridViewTextBoxColumn
            {
                HeaderText = "RGB mới",
                Width = 95,
                ReadOnly = true,
                Name = "colNewRgb"
            };

            var colCustomColorBtn = new DataGridViewButtonColumn
            {
                HeaderText = "Tự chọn",
                Text = "🎨",
                UseColumnTextForButtonValue = true,
                Width = 55,
                Name = "colCustomColorBtn",
                FlatStyle = FlatStyle.Flat
            };

            var colCurrentColorPreview = new DataGridViewTextBoxColumn
            {
                HeaderText = "Màu hiện tại",
                Width = 75,
                ReadOnly = true,
                Name = "colCurrentColorPreview"
            };

            var colCurrentDesc = new DataGridViewTextBoxColumn
            {
                HeaderText = "Mô tả hiện tại trong CAD",
                Width = 150,
                ReadOnly = true,
                Name = "colCurrentDesc"
            };

            dgvLayers.Columns.AddRange(new DataGridViewColumn[]
            {
                colCheck, colLayerName, colSolidCount, colBodyCount,
                colPreset, colCauKien, colVatLieu, colNewColorPreview,
                colNewRgb, colCustomColorBtn, colCurrentColorPreview, colCurrentDesc
            });

            dgvLayers.CellPainting += DgvLayers_CellPainting;
            dgvLayers.CellContentClick += DgvLayers_CellContentClick;
            dgvLayers.CellValueChanged += DgvLayers_CellValueChanged;
            dgvLayers.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (dgvLayers.IsCurrentCellDirty)
                {
                    dgvLayers.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };

            tabMainCoordination.Controls.Add(dgvLayers);
            tabMainCoordination.Controls.Add(pnlBatch);
            tabMainCoordination.Controls.Add(pnlToolbar);
        }
        #endregion

        #region Build Tab 2: Preset Reference
        private void BuildTabPresetReference(DrawingFont regularFont, DrawingFont boldFont)
        {
            dgvPresets = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = DrawingColor.White,
                BorderStyle = BorderStyle.None,
                GridColor = DrawingColor.FromArgb(230, 235, 240),
                RowTemplate = { Height = 28 },
                Font = regularFont,
                EnableHeadersVisualStyles = false
            };

            dgvPresets.ColumnHeadersDefaultCellStyle.BackColor = DrawingColor.FromArgb(238, 242, 248);
            dgvPresets.ColumnHeadersDefaultCellStyle.ForeColor = DrawingColor.FromArgb(20, 35, 60);
            dgvPresets.ColumnHeadersDefaultCellStyle.Font = boldFont;
            dgvPresets.ColumnHeadersHeight = 32;

            var colPCode = new DataGridViewTextBoxColumn { HeaderText = "Mã", Width = 55, ReadOnly = true, Name = "colPCode" };
            var colPGroup = new DataGridViewTextBoxColumn { HeaderText = "Hạng mục / Cấu kiện", Width = 230, ReadOnly = true, Name = "colPGroup" };
            var colPMaterial = new DataGridViewTextBoxColumn { HeaderText = "Vật liệu / Loại kết cấu", Width = 280, ReadOnly = true, Name = "colPMaterial" };
            var colPColor = new DataGridViewTextBoxColumn { HeaderText = "Màu sắc", Width = 70, ReadOnly = true, Name = "colPColor" };
            var colPRgb = new DataGridViewTextBoxColumn { HeaderText = "RGB (R, G, B)", Width = 120, ReadOnly = true, Name = "colPRgb" };
            var colPHex = new DataGridViewTextBoxColumn { HeaderText = "Mã HEX", Width = 85, ReadOnly = true, Name = "colPHex" };
            var colPKeywords = new DataGridViewTextBoxColumn { HeaderText = "Từ khóa nhận diện tự động", Width = 250, ReadOnly = true, Name = "colPKeywords" };

            dgvPresets.Columns.AddRange(new DataGridViewColumn[]
            {
                colPCode, colPGroup, colPMaterial, colPColor, colPRgb, colPHex, colPKeywords
            });

            dgvPresets.CellPainting += DgvPresets_CellPainting;

            tabPresetReference.Controls.Add(dgvPresets);
        }

        private void PopulatePresetReferenceGrid()
        {
            dgvPresets.Rows.Clear();
            foreach (var p in _presets)
            {
                int rIdx = dgvPresets.Rows.Add();
                var row = dgvPresets.Rows[rIdx];
                row.Tag = p;
                row.Cells["colPCode"].Value = p.Code;
                row.Cells["colPGroup"].Value = p.GroupName;
                row.Cells["colPMaterial"].Value = p.MaterialName;
                row.Cells["colPColor"].Value = ""; // Custom painted
                row.Cells["colPRgb"].Value = p.RgbText;
                row.Cells["colPHex"].Value = p.HexCode;
                row.Cells["colPKeywords"].Value = string.Join(", ", p.Keywords);
            }
        }
        #endregion

        #region Data Loading & Binding
        private void LoadDataFromDrawing()
        {
            _allLayerRows.Clear();

            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var db = doc.Database;

            // 1. Quét số lượng 3D Solid và Body theo từng Layer
            var solidBodyCounts = new Dictionary<string, (int solidCount, int bodyCount)>(StringComparer.OrdinalIgnoreCase);
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var mappings = PropertySetUtils.ScanSolidsAndBodies(tr);
                foreach (var m in mappings)
                {
                    solidBodyCounts[m.LayerName] = (m.SolidCount, m.BodyCount);
                }
                tr.Commit();
            }

            // 2. Duyệt qua tất cả Layer trong LayerTable của CAD
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var layTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                foreach (ObjectId layId in layTable)
                {
                    var ltr = (LayerTableRecord)tr.GetObject(layId, OpenMode.ForRead);
                    string name = ltr.Name;

                    var acadColor = ltr.Color;
                    DrawingColor drawCol = DrawingColor.FromArgb(acadColor.ColorValue.R, acadColor.ColorValue.G, acadColor.ColorValue.B);
                    string colDesc = acadColor.IsByAci ? $"ACI {acadColor.ColorIndex}" : $"RGB({drawCol.R},{drawCol.G},{drawCol.B})";

                    string linetypeName = "Continuous";
                    try
                    {
                        var ltObj = tr.GetObject(ltr.LinetypeObjectId, OpenMode.ForRead) as LinetypeTableRecord;
                        if (ltObj != null) linetypeName = ltObj.Name;
                    }
                    catch { }

                    int solidCount = 0;
                    int bodyCount = 0;
                    if (solidBodyCounts.TryGetValue(name, out var counts))
                    {
                        solidCount = counts.solidCount;
                        bodyCount = counts.bodyCount;
                    }

                    var rowModel = new LayerBimRowModel
                    {
                        LayerName = name,
                        SolidCount = solidCount,
                        BodyCount = bodyCount,
                        CurrentColor = drawCol,
                        CurrentColorDesc = colDesc,
                        CurrentDescription = ltr.Description ?? "",
                        Linetype = linetypeName,
                        IsLocked = ltr.IsLocked,
                        IsFrozen = ltr.IsFrozen,
                        IsOff = ltr.IsOff,
                        IsSelected = false
                    };

                    // Khôi phục từ bộ nhớ tạm (Persistent State) nếu có
                    if (_savedLayerStates.TryGetValue(name, out var saved))
                    {
                        rowModel.SelectedPresetCode = saved.PresetCode;
                        rowModel.CauKien = saved.CauKien;
                        rowModel.VatLieu = saved.VatLieu;
                        rowModel.NewR = saved.R;
                        rowModel.NewG = saved.G;
                        rowModel.NewB = saved.B;
                        rowModel.IsSelected = saved.IsSelected;
                    }

                    _allLayerRows.Add(rowModel);
                }
                tr.Commit();
            }

            _allLayerRows = _allLayerRows.OrderBy(x => x.LayerName).ToList();

            // Cập nhật danh sách mẫu trong ComboBox cột Grid & thanh Batch
            var colPresetCombo = (DataGridViewComboBoxColumn)dgvLayers.Columns["colPreset"];
            colPresetCombo.Items.Clear();
            colPresetCombo.Items.Add("-- (Chưa gán mẫu) --");
            foreach (var p in _presets)
            {
                colPresetCombo.Items.Add(p.DisplayName);
            }
            colPresetCombo.Items.Add("🎨 [Tùy chỉnh] Tự chọn màu...");

            cboBatchPreset.Items.Clear();
            foreach (var p in _presets)
            {
                cboBatchPreset.Items.Add(p.DisplayName);
            }
            if (cboBatchPreset.Items.Count > 0) cboBatchPreset.SelectedIndex = 0;
        }

        private void PopulateLayersGrid()
        {
            FilterLayersGrid();
        }

        private void FilterLayersGrid()
        {
            dgvLayers.Rows.Clear();
            string keyword = txtSearch.Text.Trim().ToLower();

            var filtered = string.IsNullOrEmpty(keyword)
                ? _allLayerRows
                : _allLayerRows.Where(x => x.LayerName.ToLower().Contains(keyword) ||
                                           x.CauKien.ToLower().Contains(keyword) ||
                                           x.VatLieu.ToLower().Contains(keyword)).ToList();

            foreach (var rowModel in filtered)
            {
                int rIdx = dgvLayers.Rows.Add();
                var row = dgvLayers.Rows[rIdx];
                row.Tag = rowModel;

                row.Cells["colCheck"].Value = rowModel.IsSelected;
                row.Cells["colLayerName"].Value = rowModel.LayerName;
                row.Cells["colSolidCount"].Value = rowModel.SolidCount > 0 ? rowModel.SolidCount.ToString("N0") : "-";
                row.Cells["colBodyCount"].Value = rowModel.BodyCount > 0 ? rowModel.BodyCount.ToString("N0") : "-";

                // Set ComboBox display
                string comboDisplay = "-- (Chưa gán mẫu) --";
                if (!string.IsNullOrEmpty(rowModel.SelectedPresetCode))
                {
                    var preset = _presets.FirstOrDefault(p => p.Code == rowModel.SelectedPresetCode);
                    if (preset != null)
                    {
                        comboDisplay = preset.DisplayName;
                    }
                    else if (rowModel.SelectedPresetCode == "CUSTOM")
                    {
                        comboDisplay = "🎨 [Tùy chỉnh] Tự chọn màu...";
                    }
                }
                row.Cells["colPreset"].Value = comboDisplay;

                row.Cells["colCauKien"].Value = rowModel.CauKien;
                row.Cells["colVatLieu"].Value = rowModel.VatLieu;
                row.Cells["colNewColorPreview"].Value = ""; // Paint
                row.Cells["colNewRgb"].Value = rowModel.NewRgbText;
                row.Cells["colCurrentColorPreview"].Value = ""; // Paint
                row.Cells["colCurrentDesc"].Value = rowModel.CurrentDescription;
            }

            UpdateStats();
        }

        private void UpdateStats()
        {
            int totalLayers = _allLayerRows.Count;
            int totalSolids = _allLayerRows.Sum(x => x.SolidCount);
            int totalBodies = _allLayerRows.Sum(x => x.BodyCount);
            int selectedLayers = _allLayerRows.Count(x => x.IsSelected);

            lblStats.Text = $"📊 {totalLayers} Layer ({selectedLayers} chọn) | Solid 3D: {totalSolids:N0} | Body: {totalBodies:N0} | Tổng 3D: {totalSolids + totalBodies:N0}";
        }
        #endregion

        #region Custom Cell Painting
        private void DgvLayers_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            string colName = dgvLayers.Columns[e.ColumnIndex].Name;

            if (colName == "colNewColorPreview" || colName == "colCurrentColorPreview")
            {
                e.PaintBackground(e.ClipBounds, true);

                var row = dgvLayers.Rows[e.RowIndex];
                if (row.Tag is LayerBimRowModel model)
                {
                    DrawingColor? colorToDraw = null;
                    if (colName == "colNewColorPreview")
                    {
                        colorToDraw = model.NewColor;
                    }
                    else if (colName == "colCurrentColorPreview")
                    {
                        colorToDraw = model.CurrentColor;
                    }

                    if (colorToDraw.HasValue)
                    {
                        var rect = new Rectangle(e.CellBounds.X + 5, e.CellBounds.Y + 4, e.CellBounds.Width - 10, e.CellBounds.Height - 8);
                        using (var brush = new SolidBrush(colorToDraw.Value))
                        using (var pen = new Pen(DrawingColor.FromArgb(90, 90, 90), 1))
                        {
                            e.Graphics.FillRectangle(brush, rect);
                            e.Graphics.DrawRectangle(pen, rect);
                        }
                    }
                    else
                    {
                        using (var brush = new SolidBrush(DrawingColor.Gray))
                        {
                            e.Graphics.DrawString("---", this.Font, brush, e.CellBounds.X + 12, e.CellBounds.Y + 6);
                        }
                    }
                }

                e.Handled = true;
            }
        }

        private void DgvPresets_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (dgvPresets.Columns[e.ColumnIndex].Name == "colPColor")
            {
                e.PaintBackground(e.ClipBounds, true);
                var row = dgvPresets.Rows[e.RowIndex];
                if (row.Tag is BimStandardPreset preset)
                {
                    var rect = new Rectangle(e.CellBounds.X + 5, e.CellBounds.Y + 4, e.CellBounds.Width - 10, e.CellBounds.Height - 8);
                    using (var brush = new SolidBrush(preset.DrawingColor))
                    using (var pen = new Pen(DrawingColor.FromArgb(90, 90, 90), 1))
                    {
                        e.Graphics.FillRectangle(brush, rect);
                        e.Graphics.DrawRectangle(pen, rect);
                    }
                }
                e.Handled = true;
            }
        }
        #endregion

        #region Grid Events & Auto Inference
        private void DgvLayers_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            var row = dgvLayers.Rows[e.RowIndex];
            var model = row.Tag as LayerBimRowModel;
            if (model == null) return;

            string colName = dgvLayers.Columns[e.ColumnIndex].Name;

            if (colName == "colCheck")
            {
                model.IsSelected = Convert.ToBoolean(row.Cells["colCheck"].Value);
                UpdateStats();
            }
            else if (colName == "colPreset")
            {
                string selectedText = row.Cells["colPreset"].Value?.ToString() ?? "";
                if (selectedText.StartsWith("--"))
                {
                    model.SelectedPresetCode = "";
                    model.CauKien = "";
                    model.VatLieu = "";
                    model.NewR = null;
                    model.NewG = null;
                    model.NewB = null;
                    model.IsSelected = false;
                }
                else if (selectedText.Contains("[Tùy chỉnh]"))
                {
                    model.SelectedPresetCode = "CUSTOM";
                    PromptCustomColor(model, row);
                }
                else
                {
                    var preset = _presets.FirstOrDefault(p => p.DisplayName == selectedText);
                    if (preset != null)
                    {
                        model.SelectedPresetCode = preset.Code;
                        model.CauKien = preset.GroupName;
                        model.VatLieu = preset.MaterialName;
                        model.NewR = preset.R;
                        model.NewG = preset.G;
                        model.NewB = preset.B;
                        model.IsSelected = true;
                    }
                }

                // Cập nhật giao diện dòng
                row.Cells["colCheck"].Value = model.IsSelected;
                row.Cells["colCauKien"].Value = model.CauKien;
                row.Cells["colVatLieu"].Value = model.VatLieu;
                row.Cells["colNewRgb"].Value = model.NewRgbText;
                dgvLayers.InvalidateCell(row.Cells["colNewColorPreview"]);
                UpdateStats();
            }
            else if (colName == "colCauKien")
            {
                model.CauKien = row.Cells["colCauKien"].Value?.ToString() ?? "";
                TryAutoInferColorFromNames(model, row);
            }
            else if (colName == "colVatLieu")
            {
                model.VatLieu = row.Cells["colVatLieu"].Value?.ToString() ?? "";
                TryAutoInferColorFromNames(model, row);
            }
        }

        private void DgvLayers_CellContentClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            string colName = dgvLayers.Columns[e.ColumnIndex].Name;

            var row = dgvLayers.Rows[e.RowIndex];
            var model = row.Tag as LayerBimRowModel;
            if (model == null) return;

            if (colName == "colCustomColorBtn")
            {
                PromptCustomColor(model, row);
            }
        }

        /// <summary>
        /// Tự động tra cứu màu sắc theo Cấu kiện và Vật liệu vừa nhập
        /// </summary>
        private void TryAutoInferColorFromNames(LayerBimRowModel model, DataGridViewRow row)
        {
            if (string.IsNullOrWhiteSpace(model.VatLieu) && string.IsNullOrWhiteSpace(model.CauKien)) return;

            // 1. So khớp chính xác tên vật liệu hoặc cấu kiện
            var matched = _presets.FirstOrDefault(p =>
                (!string.IsNullOrWhiteSpace(model.VatLieu) && p.MaterialName.Equals(model.VatLieu.Trim(), StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(model.VatLieu) && NormalizeString(p.MaterialName) == NormalizeString(model.VatLieu)));

            if (matched == null && !string.IsNullOrWhiteSpace(model.VatLieu))
            {
                // So khớp từ khóa
                matched = _presets.FirstOrDefault(p =>
                    p.Keywords.Any(kw => NormalizeString(model.VatLieu).Contains(NormalizeString(kw))));
            }

            if (matched != null)
            {
                model.SelectedPresetCode = matched.Code;
                if (string.IsNullOrWhiteSpace(model.CauKien)) model.CauKien = matched.GroupName;
                model.NewR = matched.R;
                model.NewG = matched.G;
                model.NewB = matched.B;
                model.IsSelected = true;

                row.Cells["colCheck"].Value = true;
                row.Cells["colPreset"].Value = matched.DisplayName;
                row.Cells["colCauKien"].Value = model.CauKien;
                row.Cells["colNewRgb"].Value = model.NewRgbText;
                dgvLayers.InvalidateCell(row.Cells["colNewColorPreview"]);
                UpdateStats();
            }
        }

        private void PromptCustomColor(LayerBimRowModel model, DataGridViewRow row)
        {
            using (var cd = new ColorDialog())
            {
                cd.Color = model.NewColor ?? model.CurrentColor;
                cd.FullOpen = true;
                if (cd.ShowDialog(this) == DialogResult.OK)
                {
                    model.NewR = cd.Color.R;
                    model.NewG = cd.Color.G;
                    model.NewB = cd.Color.B;
                    model.SelectedPresetCode = "CUSTOM";
                    model.IsSelected = true;

                    row.Cells["colCheck"].Value = true;
                    row.Cells["colPreset"].Value = "🎨 [Tùy chỉnh] Tự chọn màu...";
                    row.Cells["colNewRgb"].Value = model.NewRgbText;
                    dgvLayers.InvalidateCell(row.Cells["colNewColorPreview"]);
                    UpdateStats();
                    AppendLog($"Đã chọn màu RGB({cd.Color.R},{cd.Color.G},{cd.Color.B}) cho Layer '{model.LayerName}'.");
                }
            }
        }
        #endregion

        #region Toolbar Actions: Auto Match, Batch Assign, Pick CAD
        private void BtnAutoMatchAll_Click(object? sender, EventArgs e)
        {
            int matchedCount = 0;

            foreach (var model in _allLayerRows)
            {
                var matchedPreset = FindBestMatchingPreset(model.LayerName);
                if (matchedPreset != null)
                {
                    model.SelectedPresetCode = matchedPreset.Code;
                    model.CauKien = matchedPreset.GroupName;
                    model.VatLieu = matchedPreset.MaterialName;
                    model.NewR = matchedPreset.R;
                    model.NewG = matchedPreset.G;
                    model.NewB = matchedPreset.B;
                    model.IsSelected = true;
                    matchedCount++;
                }
            }

            FilterLayersGrid();
            AppendLog($"⚡ Đã tự động nhận diện và gán mẫu Màu & Property Set cho {matchedCount}/{_allLayerRows.Count} Layer theo từ khóa.");
            MessageBox.Show($"Đã tự động nhận diện và gán mẫu Màu & Property Set cho {matchedCount}/{_allLayerRows.Count} Layer!", "Tự động nhận diện", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private BimStandardPreset? FindBestMatchingPreset(string layerName)
        {
            if (string.IsNullOrEmpty(layerName)) return null;
            string cleanLayer = NormalizeString(layerName);

            foreach (var preset in _presets)
            {
                string cleanMaterial = NormalizeString(preset.MaterialName);
                if (cleanLayer.Contains(cleanMaterial))
                    return preset;

                foreach (var kw in preset.Keywords)
                {
                    string cleanKw = NormalizeString(kw);
                    if (cleanLayer.Contains(cleanKw))
                        return preset;
                }
            }

            return null;
        }

        private static string NormalizeString(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            string normalized = input.ToUpper().Trim();
            string[] vietnameseSigns = new string[]
            {
                "aAeEoOuUiIdDyY",
                "áàạảãâấầậẩẫăắằặẳẵ",
                "ÁÀẠẢÃÂẤẦẬẨẪĂẮẰẶẲẴ",
                "éèẹẻẽêếềệểễ",
                "ÉÈẸẺẼÊẾỀỆỂỄ",
                "óòọỏõôốồộổỗơớờợởỡ",
                "ÓÒỌỎÕÔỐỒỘỔỖƠỚỜỢỞỠ",
                "úùụủũưứừựửữ",
                "ÚÙỤỦŨƯỨỪỰỬỮ",
                "íìịỉĩ",
                "ÍÌỊỈĨ",
                "đ",
                "Đ",
                "ýỳỵỷỹ",
                "ÝỲỴỶỸ"
            };
            for (int i = 1; i < vietnameseSigns.Length; i++)
            {
                for (int j = 0; j < vietnameseSigns[i].Length; j++)
                    normalized = normalized.Replace(vietnameseSigns[i][j], vietnameseSigns[0][i - 1]);
            }
            return Regex.Replace(normalized, @"[^A-Z0-9_]", "");
        }

        private void BtnApplyBatchPreset_Click(object? sender, EventArgs e)
        {
            string selectedPresetText = cboBatchPreset.SelectedItem?.ToString() ?? "";
            var preset = _presets.FirstOrDefault(p => p.DisplayName == selectedPresetText);
            if (preset == null)
            {
                MessageBox.Show("Vui lòng chọn 1 mẫu chuẩn từ danh sách thả xuống!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selectedLayers = _allLayerRows.Where(x => x.IsSelected).ToList();
            if (selectedLayers.Count == 0)
            {
                MessageBox.Show("Vui lòng tích chọn ít nhất 1 Layer trong bảng để gán nhanh!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            foreach (var model in selectedLayers)
            {
                model.SelectedPresetCode = preset.Code;
                model.CauKien = preset.GroupName;
                model.VatLieu = preset.MaterialName;
                model.NewR = preset.R;
                model.NewG = preset.G;
                model.NewB = preset.B;
            }

            FilterLayersGrid();
            AppendLog($"👉 Đã gán mẫu [{preset.MaterialName}] cho {selectedLayers.Count} Layer được chọn.");
        }

        private void BtnPickObject_Click(object? sender, EventArgs e)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            string selectedLayerName = "";
            using (var interaction = ed.StartUserInteraction(this))
            {
                var pOpt = new PromptEntityOptions("\nChọn đối tượng trên bản vẽ để tìm Layer: ");
                pOpt.SetRejectMessage("\nChỉ chọn đối tượng AutoCAD hợp lệ!");
                pOpt.AddAllowedClass(typeof(Entity), true);

                var pRes = ed.GetEntity(pOpt);
                interaction.End();

                if (pRes.Status == PromptStatus.OK)
                {
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        var ent = tr.GetObject(pRes.ObjectId, OpenMode.ForRead) as Entity;
                        if (ent != null)
                        {
                            selectedLayerName = ent.Layer;
                        }
                        tr.Commit();
                    }
                }
            }

            if (!string.IsNullOrEmpty(selectedLayerName))
            {
                txtSearch.Text = selectedLayerName;
                AppendLog($"🎯 Đã tìm thấy Layer '{selectedLayerName}' từ đối tượng vừa pick trên bản vẽ.");
            }
        }

        private void SetAllLayersChecked(bool isChecked)
        {
            foreach (var model in _allLayerRows)
            {
                model.IsSelected = isChecked;
            }
            foreach (DataGridViewRow row in dgvLayers.Rows)
            {
                row.Cells["colCheck"].Value = isChecked;
            }
            UpdateStats();
        }
        #endregion

        #region Excel Import & Export (Tái Sử Dụng Cấu Hình)
        private void BtnExportExcel_Click(object? sender, EventArgs e)
        {
            if (_allLayerRows.Count == 0)
            {
                MessageBox.Show("Không có dữ liệu Layer để xuất ra Excel!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                Title = "Xuất cấu hình Màu & Property Set Layer ra Excel",
                FileName = $"BIM_Layer_PropertySet_Mapping_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                InitialDirectory = !string.IsNullOrEmpty(_lastExcelPath) ? Path.GetDirectoryName(_lastExcelPath) : ""
            };

            if (sfd.ShowDialog() != DialogResult.OK) return;

            try
            {
                _lastExcelPath = sfd.FileName;
                string propSetName = txtPropSetName.Text.Trim();

                using var workbook = new XLWorkbook();
                var ws = workbook.Worksheets.Add("BIM_Layer_Config");

                // Tiêu đề cột
                string[] headers = {
                    "STT", "Tên Layer", "Cấu kiện", "Vật liệu", "Mã RGB", "Mã HEX",
                    "Tên Property Set", "Solid 3D", "3D Body", "Tổng đối tượng", "Mã Mẫu BIM"
                };

                for (int c = 0; c < headers.Length; c++)
                {
                    var cell = ws.Cell(1, c + 1);
                    cell.Value = headers[c];
                    cell.Style.Font.Bold = true;
                    cell.Style.Font.FontColor = XLColor.White;
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1B365D");
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                }
                ws.Row(1).Height = 26;

                int row = 2;
                int stt = 1;
                foreach (var item in _allLayerRows)
                {
                    ws.Cell(row, 1).SetValue(stt++);
                    ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    ws.Cell(row, 2).SetValue(item.LayerName);
                    ws.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

                    ws.Cell(row, 3).SetValue(item.CauKien ?? "");
                    ws.Cell(row, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

                    ws.Cell(row, 4).SetValue(item.VatLieu ?? "");
                    ws.Cell(row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

                    string rgbVal = item.NewColor.HasValue ? $"{item.NewR},{item.NewG},{item.NewB}" : "";
                    ws.Cell(row, 5).SetValue(rgbVal);
                    ws.Cell(row, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    ws.Cell(row, 6).SetValue(item.NewHexText);
                    ws.Cell(row, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    ws.Cell(row, 7).SetValue(propSetName);
                    ws.Cell(row, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

                    ws.Cell(row, 8).SetValue(item.SolidCount);
                    ws.Cell(row, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                    ws.Cell(row, 9).SetValue(item.BodyCount);
                    ws.Cell(row, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                    ws.Cell(row, 10).SetValue(item.TotalCount);
                    ws.Cell(row, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    ws.Cell(row, 10).Style.Font.Bold = true;

                    ws.Cell(row, 11).SetValue(item.SelectedPresetCode ?? "");
                    ws.Cell(row, 11).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    // Tô màu nền ô HEX trực quan
                    if (item.NewColor.HasValue)
                    {
                        try
                        {
                            ws.Cell(row, 6).Style.Fill.BackgroundColor = XLColor.FromArgb(item.NewR!.Value, item.NewG!.Value, item.NewB!.Value);
                            // Nếu màu tối thì chữ trắng, màu sáng thì chữ đen
                            double lum = (0.299 * item.NewR.Value + 0.587 * item.NewG.Value + 0.114 * item.NewB.Value);
                            ws.Cell(row, 6).Style.Font.FontColor = lum < 140 ? XLColor.White : XLColor.Black;
                            ws.Cell(row, 6).Style.Font.Bold = true;
                        }
                        catch { }
                    }

                    row++;
                }

                var range = ws.Range(1, 1, row - 1, headers.Length);
                range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                ws.Columns().AdjustToContents(10, 45);

                workbook.SaveAs(sfd.FileName);
                AppendLog($"📤 Đã xuất thành công cấu hình {stt - 1} Layer ra file: {sfd.FileName}");

                var dr = MessageBox.Show($"Xuất Excel thành công:\n{sfd.FileName}\n\nBạn có muốn mở file ngay bây giờ không?", "Thành công", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (dr == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = sfd.FileName,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi xuất file Excel: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                AppendLog($"❌ Lỗi xuất Excel: {ex.Message}");
            }
        }

        private void BtnImportExcel_Click(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                Title = "Chọn file Excel cấu hình Màu & Property Set",
                InitialDirectory = !string.IsNullOrEmpty(_lastExcelPath) ? Path.GetDirectoryName(_lastExcelPath) : ""
            };

            if (ofd.ShowDialog() != DialogResult.OK) return;

            try
            {
                _lastExcelPath = ofd.FileName;

                using var workbook = new XLWorkbook(ofd.FileName);
                var ws = workbook.Worksheets.FirstOrDefault();
                if (ws == null)
                {
                    MessageBox.Show("File Excel không chứa bất kỳ Sheet nào!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var rangeUsed = ws.RangeUsed();
                if (rangeUsed == null)
                {
                    MessageBox.Show("File Excel không có dữ liệu!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                int firstRow = rangeUsed.FirstRow().RowNumber();
                int lastRow = rangeUsed.LastRow().RowNumber();
                int firstCol = rangeUsed.FirstColumn().ColumnNumber();
                int lastCol = rangeUsed.LastColumn().ColumnNumber();

                // Xác định các cột dựa theo tên tiêu đề
                int headerRow = firstRow;
                int colLayerIdx = -1;
                int colCauKienIdx = -1;
                int colVatLieuIdx = -1;
                int colRgbIdx = -1;
                int colPresetIdx = -1;
                int colPropSetIdx = -1;

                int maxHeaderScan = Math.Min(firstRow + 4, lastRow);
                for (int r = firstRow; r <= maxHeaderScan; r++)
                {
                    for (int c = firstCol; c <= lastCol; c++)
                    {
                        string val = ws.Cell(r, c).GetString().Trim().ToLower();
                        if (val.Contains("layer") || val == "tên layer") colLayerIdx = c;
                        else if (val.Contains("cấu kiện") || val.Contains("cau kien") || val.Contains("hạng mục")) colCauKienIdx = c;
                        else if (val.Contains("vật liệu") || val.Contains("vat lieu")) colVatLieuIdx = c;
                        else if (val.Contains("rgb") || val.Contains("màu")) colRgbIdx = c;
                        else if (val.Contains("mẫu") || val.Contains("preset")) colPresetIdx = c;
                        else if (val.Contains("property set") || val.Contains("propertyset")) colPropSetIdx = c;
                    }

                    if (colLayerIdx > 0 && (colCauKienIdx > 0 || colVatLieuIdx > 0))
                    {
                        headerRow = r;
                        break;
                    }
                }

                if (colLayerIdx <= 0)
                {
                    // Fallback
                    colLayerIdx = Math.Min(firstCol + 1, lastCol);
                    colCauKienIdx = Math.Min(firstCol + 2, lastCol);
                    colVatLieuIdx = Math.Min(firstCol + 3, lastCol);
                    colRgbIdx = Math.Min(firstCol + 4, lastCol);
                }

                int importedCount = 0;
                var layerMap = _allLayerRows.ToDictionary(x => x.LayerName, x => x, StringComparer.OrdinalIgnoreCase);

                for (int r = headerRow + 1; r <= lastRow; r++)
                {
                    string layerName = ws.Cell(r, colLayerIdx).GetString().Trim();
                    if (string.IsNullOrEmpty(layerName)) continue;

                    string cauKien = colCauKienIdx > 0 ? ws.Cell(r, colCauKienIdx).GetString().Trim() : "";
                    string vatLieu = colVatLieuIdx > 0 ? ws.Cell(r, colVatLieuIdx).GetString().Trim() : "";
                    string rgbStr = colRgbIdx > 0 ? ws.Cell(r, colRgbIdx).GetString().Trim() : "";
                    string presetCode = colPresetIdx > 0 ? ws.Cell(r, colPresetIdx).GetString().Trim() : "";
                    string propSetName = colPropSetIdx > 0 ? ws.Cell(r, colPropSetIdx).GetString().Trim() : "";

                    if (!string.IsNullOrEmpty(propSetName))
                    {
                        txtPropSetName.Text = propSetName;
                    }

                    if (layerMap.TryGetValue(layerName, out var targetModel))
                    {
                        if (!string.IsNullOrEmpty(cauKien)) targetModel.CauKien = cauKien;
                        if (!string.IsNullOrEmpty(vatLieu)) targetModel.VatLieu = vatLieu;

                        // Tìm màu sắc:
                        // 1. Thử giải mã RGB từ file Excel
                        bool parsedColor = false;
                        if (!string.IsNullOrEmpty(rgbStr))
                        {
                            var parts = rgbStr.Replace("(", "").Replace(")", "").Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 3 && byte.TryParse(parts[0], out byte rVal) && byte.TryParse(parts[1], out byte gVal) && byte.TryParse(parts[2], out byte bVal))
                            {
                                targetModel.NewR = rVal;
                                targetModel.NewG = gVal;
                                targetModel.NewB = bVal;
                                targetModel.SelectedPresetCode = !string.IsNullOrEmpty(presetCode) ? presetCode : "CUSTOM";
                                parsedColor = true;
                            }
                        }

                        // 2. Nếu chưa có màu, tìm theo presetCode hoặc theo Vật liệu / Cấu kiện
                        if (!parsedColor)
                        {
                            BimStandardPreset? p = null;
                            if (!string.IsNullOrEmpty(presetCode))
                            {
                                p = _presets.FirstOrDefault(x => x.Code == presetCode);
                            }
                            if (p == null && !string.IsNullOrEmpty(vatLieu))
                            {
                                p = _presets.FirstOrDefault(x => NormalizeString(x.MaterialName) == NormalizeString(vatLieu));
                            }
                            if (p != null)
                            {
                                targetModel.SelectedPresetCode = p.Code;
                                targetModel.NewR = p.R;
                                targetModel.NewG = p.G;
                                targetModel.NewB = p.B;
                            }
                        }

                        targetModel.IsSelected = true;
                        importedCount++;
                    }
                }

                FilterLayersGrid();
                AppendLog($"📥 Đã nạp thành công cấu hình cho {importedCount} Layer từ file Excel: {Path.GetFileName(ofd.FileName)}");
                MessageBox.Show($"Đã nạp thành công cấu hình cho {importedCount} Layer từ file Excel!", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi nhập dữ liệu từ Excel: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                AppendLog($"❌ Lỗi nhập Excel: {ex.Message}");
            }
        }
        #endregion

        #region Execute Updates
        private void BtnExecute_Click(object? sender, EventArgs e)
        {
            var selectedRows = _allLayerRows.Where(x => x.IsSelected).ToList();
            if (selectedRows.Count == 0)
            {
                MessageBox.Show("Bạn chưa tích chọn Layer nào trong danh sách để áp dụng cập nhật!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string propSetName = txtPropSetName.Text.Trim();
            bool updateColor = chkUpdateColor.Checked;
            bool updatePropSet = chkUpdatePropSet.Checked;
            bool updateDescription = chkUpdateDescription.Checked;
            bool applyByLayer = chkApplyByLayer.Checked;
            bool unlockLayers = chkUnlockLayers.Checked;

            if (updatePropSet && string.IsNullOrEmpty(propSetName))
            {
                MessageBox.Show("Vui lòng nhập Tên Property Set!", "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtPropSetName.Focus();
                return;
            }

            try
            {
                btnExecute.Enabled = false;
                Cursor.Current = Cursors.WaitCursor;

                SaveCurrentSettings();

                int result = CapNhatMauVaPropertySetCmd.ExecuteUpdateAll(
                    selectedRows,
                    propSetName,
                    updateColor,
                    updatePropSet,
                    updateDescription,
                    applyByLayer,
                    unlockLayers,
                    msg => AppendLog(msg)
                );

                LoadDataFromDrawing();
                PopulateLayersGrid();

                MessageBox.Show($"Đã hoàn tất cập nhật thành công cho {selectedRows.Count} Layer được chọn!\n(Đã cập nhật Property Set cho {result} đối tượng 3D Solid / Body).", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                AppendLog($"❌ Lỗi: {ex.Message}");
                MessageBox.Show($"Đã xảy ra lỗi: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnExecute.Enabled = true;
                Cursor.Current = Cursors.Default;
            }
        }
        #endregion

        #region Helpers & Persistent Settings
        private void AppendLog(string message)
        {
            if (txtLog.IsDisposed) return;
            string time = DateTime.Now.ToString("HH:mm:ss");
            txtLog.AppendText($"[{time}] {message}\r\n");
            txtLog.SelectionStart = txtLog.Text.Length;
            txtLog.ScrollToCaret();
        }

        private static Button CreateFlatButton(string text, int width, DrawingColor bgColor, DrawingFont font)
        {
            var btn = new Button
            {
                Text = text,
                Width = width,
                Height = 30,
                BackColor = bgColor,
                ForeColor = DrawingColor.White,
                FlatStyle = FlatStyle.Flat,
                Font = font,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private void CapNhatMauVaPropertySetForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            SaveCurrentSettings();
        }

        private void SaveCurrentSettings()
        {
            _savedLayerStates.Clear();
            foreach (var r in _allLayerRows)
            {
                if (!string.IsNullOrEmpty(r.SelectedPresetCode) || !string.IsNullOrEmpty(r.CauKien) || !string.IsNullOrEmpty(r.VatLieu) || r.IsSelected)
                {
                    _savedLayerStates[r.LayerName] = new LayerBimSavedState
                    {
                        PresetCode = r.SelectedPresetCode,
                        CauKien = r.CauKien,
                        VatLieu = r.VatLieu,
                        R = r.NewR,
                        G = r.NewG,
                        B = r.NewB,
                        IsSelected = r.IsSelected
                    };
                }
            }

            _lastPropertySetName = txtPropSetName.Text.Trim();
            _lastUpdateColor = chkUpdateColor.Checked;
            _lastUpdatePropSet = chkUpdatePropSet.Checked;
            _lastUpdateDescription = chkUpdateDescription.Checked;
            _lastApplyByLayer = chkApplyByLayer.Checked;
            _lastUnlockLayers = chkUnlockLayers.Checked;
            _lastSelectedTab = tabControlMain.SelectedIndex;
            _lastFormSize = this.Size;
        }

        private void RestoreLastSettings()
        {
            txtPropSetName.Text = !string.IsNullOrWhiteSpace(_lastPropertySetName) ? _lastPropertySetName : PropertySetUtils.DefaultPropertySetName;
            chkUpdateColor.Checked = _lastUpdateColor;
            chkUpdatePropSet.Checked = _lastUpdatePropSet;
            chkUpdateDescription.Checked = _lastUpdateDescription;
            chkApplyByLayer.Checked = _lastApplyByLayer;
            chkUnlockLayers.Checked = _lastUnlockLayers;

            if (_lastSelectedTab >= 0 && _lastSelectedTab < tabControlMain.TabCount)
            {
                tabControlMain.SelectedIndex = _lastSelectedTab;
            }
        }
        #endregion
    }
}

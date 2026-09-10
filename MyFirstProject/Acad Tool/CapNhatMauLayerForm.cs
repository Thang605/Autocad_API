// (C) Copyright 2026 by T27
//
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Color = Autodesk.AutoCAD.Colors.Color;
using DrawingColor = System.Drawing.Color;
using DrawingFont = System.Drawing.Font;

namespace Civil3DCsharp
{
    /// <summary>
    /// Model định nghĩa mẫu màu chuẩn theo Bảng 8-7 BEP T27
    /// </summary>
    public class BimStandardPreset
    {
        public string Code { get; set; } = "";
        public string GroupName { get; set; } = "";      // Tên Hạng mục chính (VD: 1-Hệ thống đường giao thông)
        public string MaterialName { get; set; } = "";   // Tên Vật liệu / Cấu kiện (VD: Bê tông nhựa chặt 16)
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }
        public string[] Keywords { get; set; } = Array.Empty<string>();

        public string DisplayName => $"[{Code}] {MaterialName} - RGB({R},{G},{B})";
        public DrawingColor DrawingColor => DrawingColor.FromArgb(R, G, B);
        public string HexCode => $"#{R:X2}{G:X2}{B:X2}";
        public string RgbText => $"({R}, {G}, {B})";

        public BimStandardPreset Clone()
        {
            return new BimStandardPreset
            {
                Code = this.Code,
                GroupName = this.GroupName,
                MaterialName = this.MaterialName,
                R = this.R,
                G = this.G,
                B = this.B,
                Keywords = (string[])this.Keywords.Clone()
            };
        }
    }

    /// <summary>
    /// Model dữ liệu cho mỗi dòng Layer trong danh sách bản vẽ
    /// </summary>
    public class LayerEditRowModel
    {
        public bool IsSelected { get; set; } = false;
        public string LayerName { get; set; } = "";
        public string SelectedPresetCode { get; set; } = ""; // Code của mẫu màu (hoặc "CUSTOM" / "")
        public string CategoryName { get; set; } = "";       // Tên Hạng mục (VD: 1-Hệ thống đường giao thông)
        public string MaterialName { get; set; } = "";       // Tên Vật liệu (VD: Bê tông nhựa chặt 16)
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
    }

    /// <summary>
    /// Lưu trạng thái persistent cho Layer giữa các lần chạy
    /// </summary>
    public class LayerSavedState
    {
        public string PresetCode { get; set; } = "";
        public string CategoryName { get; set; } = "";
        public string MaterialName { get; set; } = "";
        public byte? R { get; set; }
        public byte? G { get; set; }
        public byte? B { get; set; }
        public bool IsSelected { get; set; }
    }

    /// <summary>
    /// Form giao diện chính Cập Nhật Màu Layer theo bảng mẫu màu & tên hạng mục, tên vật liệu
    /// </summary>
    public class CapNhatMauLayerForm : Form
    {
        #region Persistent State (Ghi nhớ giữa các lần chạy)
        private static Dictionary<string, LayerSavedState> _savedLayerStates = new Dictionary<string, LayerSavedState>(StringComparer.OrdinalIgnoreCase);
        private static bool _lastApplyByLayer = true;
        private static bool _lastUpdateDescription = true;
        private static bool _lastUnlockLayers = true;
        private static Size _lastFormSize = new Size(1120, 750);
        private static int _lastSelectedTab = 0;
        #endregion

        #region Bảng Mẫu Màu Chuẩn BIM / EIR BEP T27 (Bảng 8-7)
        public static List<BimStandardPreset> GetDefaultPresets()
        {
            return new List<BimStandardPreset>
            {
                // 1-Hệ thống đường giao thông
                new BimStandardPreset {
                    Code = "1.1",
                    GroupName = "1-Hệ thống đường giao thông",
                    MaterialName = "Bê tông nhựa chặt 16",
                    R = 102, G = 102, B = 102,
                    Keywords = new[] { "BTNC16", "BTNC_16", "BTN16", "ASPHALT16", "C16", "MAT_DUONG_16", "BTN_16" }
                },
                new BimStandardPreset {
                    Code = "1.2",
                    GroupName = "1-Hệ thống đường giao thông",
                    MaterialName = "Bê tông nhựa chặt 19",
                    R = 80, G = 80, B = 80,
                    Keywords = new[] { "BTNC19", "BTNC_19", "BTN19", "ASPHALT19", "C19", "MAT_DUONG_19", "BTN_19" }
                },
                new BimStandardPreset {
                    Code = "1.3",
                    GroupName = "1-Hệ thống đường giao thông",
                    MaterialName = "CPĐD loại 1",
                    R = 55, G = 108, B = 189,
                    Keywords = new[] { "CPDD1", "CPDD_1", "CPDDLOAI1", "CPDD_LOAI_1", "BASE", "CPDD_L1", "CPDD_1" }
                },
                new BimStandardPreset {
                    Code = "1.4",
                    GroupName = "1-Hệ thống đường giao thông",
                    MaterialName = "CPĐD loại 2",
                    R = 61, G = 120, B = 210,
                    Keywords = new[] { "CPDD2", "CPDD_2", "CPDDLOAI2", "CPDD_LOAI_2", "SUBBASE", "CPDD_L2", "CPDD_2" }
                },
                new BimStandardPreset {
                    Code = "1.5",
                    GroupName = "1-Hệ thống đường giao thông",
                    MaterialName = "Đất đầm chặt K98",
                    R = 242, G = 108, B = 19,
                    Keywords = new[] { "K98", "K_98", "DATK98", "DAT_K98", "K_95", "DAM_CHAT", "K95" }
                },
                new BimStandardPreset {
                    Code = "1.6",
                    GroupName = "1-Hệ thống đường giao thông",
                    MaterialName = "Bề mặt vỉa hè trồng cỏ",
                    R = 101, G = 168, B = 67,
                    Keywords = new[] { "TRONGCO", "TRONG_CO", "CO", "GRASS", "THAM_CO", "VIA_HE_CO", "TRONG_CO" }
                },
                new BimStandardPreset {
                    Code = "1.7",
                    GroupName = "1-Hệ thống đường giao thông",
                    MaterialName = "Bề mặt vỉa hè",
                    R = 204, G = 204, B = 204,
                    Keywords = new[] { "VIAHE", "VIA_HE", "LAT_GACH", "PAVEMENT", "SIDEWALK", "GACH_VIA_HE", "BE_MAT_VIA_HE" }
                },
                new BimStandardPreset {
                    Code = "1.8",
                    GroupName = "1-Hệ thống đường giao thông",
                    MaterialName = "Đắp nền",
                    R = 70, G = 110, B = 50,
                    Keywords = new[] { "DAP", "DAP_NEN", "EMBANKMENT", "FILL", "NEN_DAP", "TALUY_DAP" }
                },
                new BimStandardPreset {
                    Code = "1.9",
                    GroupName = "1-Hệ thống đường giao thông",
                    MaterialName = "Đào nền",
                    R = 80, G = 80, B = 50,
                    Keywords = new[] { "DAO", "DAO_NEN", "EXCAVATION", "CUT", "NEN_DAO", "TALUY_DAO" }
                },
                new BimStandardPreset {
                    Code = "1.10",
                    GroupName = "1-Hệ thống đường giao thông",
                    MaterialName = "Đá dăm đệm",
                    R = 181, G = 53, B = 53,
                    Keywords = new[] { "DADAM", "DA_DAM", "DEM", "AGGREGATE", "DADAMDEM", "DA_DAM_DEM" }
                },
                new BimStandardPreset {
                    Code = "1.11",
                    GroupName = "1-Hệ thống đường giao thông",
                    MaterialName = "Các cấu kiện BTXM",
                    R = 230, G = 152, B = 0,
                    Keywords = new[] { "BTXM", "BO_VIA", "DANH_RANG", "CONCRETE_STRUCTURE", "BOVIA", "TAM_DAN" }
                },
                new BimStandardPreset {
                    Code = "1.12",
                    GroupName = "1-Hệ thống đường giao thông",
                    MaterialName = "Các loại cấu kiện khác",
                    R = 130, G = 130, B = 130,
                    Keywords = new[] { "CAUKIEN", "STRUCTURE", "OTHER", "CAU_KIEN", "PHU_TRO" }
                },

                // 2-Mạng lưới thoát nước mưa
                new BimStandardPreset {
                    Code = "2",
                    GroupName = "2-Mạng lưới thoát nước mưa",
                    MaterialName = "Mạng lưới thoát nước mưa",
                    R = 0, G = 0, B = 255,
                    Keywords = new[] { "MUA", "TNM", "THOATNUOCMUA", "THOAT_NUOC_MUA", "STORM", "RAIN", "DRAINAGE", "HO_GA_MUA", "CONG_MUA" }
                },

                // 3-Mạng lưới thoát nước thải
                new BimStandardPreset {
                    Code = "3",
                    GroupName = "3-Mạng lưới thoát nước thải",
                    MaterialName = "Mạng lưới thoát nước thải",
                    R = 100, G = 50, B = 150,
                    Keywords = new[] { "THAI", "TNT", "THOATNUOCTHAI", "THOAT_NUOC_THAI", "SEWER", "WASTE", "NUOCTHAI", "HO_GA_THAI", "CONG_THAI" }
                },

                // 4-Mạng lưới chiếu sáng
                new BimStandardPreset {
                    Code = "4",
                    GroupName = "4-Mạng lưới chiếu sáng",
                    MaterialName = "Mạng lưới chiếu sáng",
                    R = 255, G = 150, B = 0,
                    Keywords = new[] { "CHIEUSANG", "CHIEU_SANG", "CS", "LIGHTING", "DEN", "COT_DEN", "CAP_CS" }
                },

                // 5-Mạng lưới cấp điện
                new BimStandardPreset {
                    Code = "5",
                    GroupName = "5-Mạng lưới cấp điện",
                    MaterialName = "Mạng lưới cấp điện",
                    R = 255, G = 250, B = 0,
                    Keywords = new[] { "DIEN", "CAPDIEN", "CAP_DIEN", "POWER", "ELECTRIC", "HA_THE", "TRUNG_THE", "TBA" }
                },

                // 6-Mạng lưới thông tin liên lạc
                new BimStandardPreset {
                    Code = "6",
                    GroupName = "6-Mạng lưới thông tin liên lạc",
                    MaterialName = "Mạng lưới thông tin liên lạc",
                    R = 0, G = 255, B = 0,
                    Keywords = new[] { "TTLL", "THONGTIN", "THONG_TIN", "LIENLAC", "LIEN_LAC", "TELECOM", "CAP_QUANG", "BE_CAP" }
                },

                // 7-Phần cống ngang, cầu
                new BimStandardPreset {
                    Code = "7.1",
                    GroupName = "7-Phần cống ngang, cầu",
                    MaterialName = "Bê tông nhựa (BTNR 12.5, BTNC 12.5, BTNC 16, ...)",
                    R = 102, G = 102, B = 102,
                    Keywords = new[] { "BTNR", "BTNC12", "BTNC_12", "BTNR12", "ASPHALT_BRIDGE", "MAT_CAU" }
                },
                new BimStandardPreset {
                    Code = "7.2",
                    GroupName = "7-Phần cống ngang, cầu",
                    MaterialName = "Bê tông cốt thép",
                    R = 130, G = 130, B = 130,
                    Keywords = new[] { "BTCT", "BETONGCOTTHEP", "BE_TONG_COT_THEP", "RC", "M400", "M300", "M350", "MO_TRU", "DAM_CAU", "THAN_CONG" }
                },
                new BimStandardPreset {
                    Code = "7.3",
                    GroupName = "7-Phần cống ngang, cầu",
                    MaterialName = "Thép",
                    R = 224, G = 223, B = 219,
                    Keywords = new[] { "THEP", "STEEL", "GIA_CO", "COT_THEP", "LAN_CAN_THEP", "GIA_CO_THEP" }
                }
            };
        }
        #endregion

        #region UI Controls
        private Panel pnlHeader = null!;
        private Label lblHeaderTitle = null!;
        private Label lblHeaderSub = null!;

        private TabControl tabControlMain = null!;
        private TabPage tabLayerList = null!;
        private TabPage tabPresetReference = null!;

        // Tab 1 (Layer List) Controls
        private Panel pnlTopFilter = null!;
        private Label lblSearch = null!;
        private TextBox txtSearch = null!;
        private Button btnPickObject = null!;
        private Button btnAutoMatchAll = null!;
        private Button btnSelectAll = null!;
        private Button btnDeselectAll = null!;
        private Button btnRefresh = null!;

        private Panel pnlBatchAssign = null!;
        private Label lblBatch = null!;
        private ComboBox cboBatchPreset = null!;
        private Button btnApplyBatchPreset = null!;

        private DataGridView dgvLayers = null!;

        // Tab 2 (Preset Reference) Controls
        private DataGridView dgvPresets = null!;

        // Bottom Options & Log
        private Panel pnlBottomArea = null!;
        private GroupBox grpOptions = null!;
        private CheckBox chkApplyByLayer = null!;
        private CheckBox chkUpdateDescription = null!;
        private CheckBox chkUnlockLayers = null!;

        private GroupBox grpLog = null!;
        private TextBox txtLog = null!;

        private Panel pnlActionButtons = null!;
        private Button btnExecute = null!;
        private Button btnClose = null!;
        #endregion

        private List<BimStandardPreset> _presets = new List<BimStandardPreset>();
        private List<LayerEditRowModel> _allLayerRows = new List<LayerEditRowModel>();

        public CapNhatMauLayerForm()
        {
            InitializeComponent();
            _presets = GetDefaultPresets();
            LoadLayersFromDrawing();
            PopulatePresetReferenceGrid();
            PopulateLayersGrid();
            RestoreLastSettings();
        }

        private void InitializeComponent()
        {
            var regularFont = new DrawingFont("Segoe UI", 9.25F, FontStyle.Regular);
            var boldFont = new DrawingFont("Segoe UI", 9.25F, FontStyle.Bold);
            var titleFont = new DrawingFont("Segoe UI", 12.5F, FontStyle.Bold);
            var subFont = new DrawingFont("Segoe UI", 8.5F, FontStyle.Regular);

            this.SuspendLayout();
            this.Text = "Cập Nhật Màu Layer & Hạng Mục, Vật Liệu Theo Mẫu Màu Chuẩn BIM / EIR";
            this.Size = _lastFormSize;
            this.MinimumSize = new Size(950, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = regularFont;
            this.BackColor = DrawingColor.FromArgb(246, 248, 250);

            // 1. Header Banner
            pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 65,
                BackColor = DrawingColor.FromArgb(24, 43, 73),
                Padding = new Padding(16, 8, 16, 8)
            };

            lblHeaderTitle = new Label
            {
                Text = "🎨 CẬP NHẬT MÀU LAYER, HẠNG MỤC & VẬT LIỆU THEO BẢNG MẪU MÀU",
                Font = titleFont,
                ForeColor = DrawingColor.White,
                AutoSize = true,
                Location = new Point(14, 10)
            };

            lblHeaderSub = new Label
            {
                Text = "Bảng 8-7: Bảng gán mã màu hệ thống hạ tầng kỹ thuật - Dự án KĐT - Công viên - TT Hành chính (BEP T27)",
                Font = subFont,
                ForeColor = DrawingColor.FromArgb(195, 215, 245),
                AutoSize = true,
                Location = new Point(16, 36)
            };

            pnlHeader.Controls.Add(lblHeaderTitle);
            pnlHeader.Controls.Add(lblHeaderSub);

            // 2. Tab Control
            tabControlMain = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = boldFont,
                Padding = new Point(12, 6)
            };

            tabLayerList = new TabPage { Text = "📑 Danh Sách Layer Bản Vẽ & Gán Mẫu Màu", BackColor = DrawingColor.White };
            tabPresetReference = new TabPage { Text = "📋 Bảng Tra Cứu Mã Màu Chuẩn (BEP T27)", BackColor = DrawingColor.White };

            tabControlMain.TabPages.Add(tabLayerList);
            tabControlMain.TabPages.Add(tabPresetReference);

            // Build Tab 1
            BuildTabLayerList(regularFont, boldFont);

            // Build Tab 2
            BuildTabPresetReference(regularFont, boldFont);

            // 3. Bottom Panel (Options + Log + Buttons)
            pnlBottomArea = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 215,
                Padding = new Padding(12, 4, 12, 4),
                BackColor = DrawingColor.FromArgb(246, 248, 250)
            };

            grpOptions = new GroupBox
            {
                Text = "⚙️ Tùy chọn thực thi",
                Font = boldFont,
                Dock = DockStyle.Top,
                Height = 62,
                Padding = new Padding(10, 4, 10, 4)
            };

            chkUpdateDescription = new CheckBox
            {
                Text = "Cập nhật Mô tả Layer (Description = [Hạng mục] | [Vật liệu])",
                Font = regularFont,
                AutoSize = true,
                Location = new Point(15, 24),
                Checked = _lastUpdateDescription
            };

            chkApplyByLayer = new CheckBox
            {
                Text = "Chuyển màu đối tượng về ByLayer (Xóa Color Override)",
                Font = regularFont,
                AutoSize = true,
                Location = new Point(440, 24),
                Checked = _lastApplyByLayer
            };

            chkUnlockLayers = new CheckBox
            {
                Text = "Tự động mở khóa Layer nếu bị Lock",
                Font = regularFont,
                AutoSize = true,
                Location = new Point(810, 24),
                Checked = _lastUnlockLayers
            };

            grpOptions.Controls.Add(chkUpdateDescription);
            grpOptions.Controls.Add(chkApplyByLayer);
            grpOptions.Controls.Add(chkUnlockLayers);

            grpLog = new GroupBox
            {
                Text = "📝 Nhật ký thực hiện",
                Font = boldFont,
                Dock = DockStyle.Fill,
                Padding = new Padding(8, 4, 8, 4)
            };

            txtLog = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                Font = new DrawingFont("Consolas", 8.75F),
                BackColor = DrawingColor.FromArgb(250, 250, 250)
            };
            grpLog.Controls.Add(txtLog);

            pnlActionButtons = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                BackColor = DrawingColor.FromArgb(235, 239, 244),
                Padding = new Padding(12, 7, 16, 7)
            };

            btnExecute = new Button
            {
                Text = "🚀 ÁP DỤNG CẬP NHẬT MÀU & THÔNG TIN LAYER",
                Font = boldFont,
                BackColor = DrawingColor.FromArgb(13, 110, 253),
                ForeColor = DrawingColor.White,
                FlatStyle = FlatStyle.Flat,
                Height = 38,
                Width = 350,
                Location = new Point(pnlActionButtons.Width - 490, 7),
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
                Height = 38,
                Width = 110,
                Location = new Point(pnlActionButtons.Width - 130, 7),
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => { this.Close(); };

            pnlActionButtons.Controls.Add(btnExecute);
            pnlActionButtons.Controls.Add(btnClose);

            pnlBottomArea.Controls.Add(grpLog);
            pnlBottomArea.Controls.Add(grpOptions);

            this.Controls.Add(tabControlMain);
            this.Controls.Add(pnlBottomArea);
            this.Controls.Add(pnlActionButtons);
            this.Controls.Add(pnlHeader);

            this.FormClosing += CapNhatMauLayerForm_FormClosing;
            this.ResumeLayout(false);
        }

        #region Build Tab 1: Layer List
        private void BuildTabLayerList(DrawingFont regularFont, DrawingFont boldFont)
        {
            // Panel 1: Filter & Actions
            pnlTopFilter = new Panel
            {
                Dock = DockStyle.Top,
                Height = 42,
                BackColor = DrawingColor.FromArgb(240, 243, 248),
                Padding = new Padding(8, 5, 8, 5)
            };

            lblSearch = new Label
            {
                Text = "🔍 Tìm Layer:",
                AutoSize = true,
                Font = boldFont,
                Location = new Point(8, 11)
            };

            txtSearch = new TextBox
            {
                Width = 160,
                Location = new Point(95, 8),
                Font = regularFont
            };
            txtSearch.TextChanged += (s, e) => FilterLayersGrid();

            btnPickObject = CreateFlatButton("🎯 Pick CAD tìm Layer", 160, DrawingColor.FromArgb(111, 66, 193), boldFont);
            btnPickObject.Location = new Point(265, 5);
            btnPickObject.Click += BtnPickObject_Click;

            btnAutoMatchAll = CreateFlatButton("⚡ Tự động nhận diện mẫu màu (Auto-Match)", 285, DrawingColor.FromArgb(25, 135, 84), boldFont);
            btnAutoMatchAll.Location = new Point(433, 5);
            btnAutoMatchAll.Click += BtnAutoMatchAll_Click;

            btnSelectAll = CreateFlatButton("☑️ Chọn tất cả", 100, DrawingColor.FromArgb(70, 80, 95), regularFont);
            btnSelectAll.Location = new Point(725, 5);
            btnSelectAll.Click += (s, e) => SetAllLayersChecked(true);

            btnDeselectAll = CreateFlatButton("⬜ Bỏ chọn", 85, DrawingColor.FromArgb(100, 110, 125), regularFont);
            btnDeselectAll.Location = new Point(832, 5);
            btnDeselectAll.Click += (s, e) => SetAllLayersChecked(false);

            btnRefresh = CreateFlatButton("🔄 Nạp lại", 85, DrawingColor.FromArgb(13, 110, 253), regularFont);
            btnRefresh.Location = new Point(924, 5);
            btnRefresh.Click += (s, e) =>
            {
                LoadLayersFromDrawing();
                PopulateLayersGrid();
                AppendLog("Đã nạp lại danh sách Layer từ bản vẽ.");
            };

            pnlTopFilter.Controls.Add(lblSearch);
            pnlTopFilter.Controls.Add(txtSearch);
            pnlTopFilter.Controls.Add(btnPickObject);
            pnlTopFilter.Controls.Add(btnAutoMatchAll);
            pnlTopFilter.Controls.Add(btnSelectAll);
            pnlTopFilter.Controls.Add(btnDeselectAll);
            pnlTopFilter.Controls.Add(btnRefresh);

            // Panel 2: Quick Batch Assign Preset
            pnlBatchAssign = new Panel
            {
                Dock = DockStyle.Top,
                Height = 38,
                BackColor = DrawingColor.FromArgb(248, 249, 250),
                Padding = new Padding(8, 4, 8, 4)
            };

            lblBatch = new Label
            {
                Text = "⚡ Gán nhanh mẫu màu cho các Layer được tích chọn:",
                AutoSize = true,
                Font = boldFont,
                ForeColor = DrawingColor.FromArgb(10, 50, 100),
                Location = new Point(8, 9)
            };

            cboBatchPreset = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 360,
                Location = new Point(365, 6),
                Font = regularFont
            };

            btnApplyBatchPreset = CreateFlatButton("👉 Gán mẫu màu", 125, DrawingColor.FromArgb(220, 53, 69), boldFont);
            btnApplyBatchPreset.Location = new Point(735, 4);
            btnApplyBatchPreset.Click += BtnApplyBatchPreset_Click;

            pnlBatchAssign.Controls.Add(lblBatch);
            pnlBatchAssign.Controls.Add(cboBatchPreset);
            pnlBatchAssign.Controls.Add(btnApplyBatchPreset);

            // Setup DataGridView Layers
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
                RowTemplate = { Height = 29 },
                Font = regularFont
            };

            var colCheck = new DataGridViewCheckBoxColumn
            {
                HeaderText = "Chọn",
                Width = 50,
                Name = "colCheck",
                HeaderCell = { Style = { Alignment = DataGridViewContentAlignment.MiddleCenter } }
            };

            var colLayerName = new DataGridViewTextBoxColumn
            {
                HeaderText = "Tên Layer trong CAD",
                Width = 180,
                ReadOnly = true,
                Name = "colLayerName"
            };

            var colPreset = new DataGridViewComboBoxColumn
            {
                HeaderText = "Chọn Bảng Mẫu Màu (BIM/EIR)",
                Width = 260,
                Name = "colPreset",
                FlatStyle = FlatStyle.Flat
            };

            var colCategory = new DataGridViewTextBoxColumn
            {
                HeaderText = "Tên Hạng mục",
                Width = 190,
                Name = "colCategory"
            };

            var colMaterial = new DataGridViewTextBoxColumn
            {
                HeaderText = "Tên Vật liệu / Cấu kiện",
                Width = 200,
                Name = "colMaterial"
            };

            var colNewColorPreview = new DataGridViewTextBoxColumn
            {
                HeaderText = "Màu mới",
                Width = 70,
                ReadOnly = true,
                Name = "colNewColorPreview"
            };

            var colNewRgb = new DataGridViewTextBoxColumn
            {
                HeaderText = "Mã RGB mới",
                Width = 100,
                ReadOnly = true,
                Name = "colNewRgb"
            };

            var colCustomColorBtn = new DataGridViewButtonColumn
            {
                HeaderText = "Tự chọn",
                Text = "🎨 Chọn",
                UseColumnTextForButtonValue = true,
                Width = 65,
                Name = "colCustomColorBtn",
                FlatStyle = FlatStyle.Flat
            };

            var colCurrentColorPreview = new DataGridViewTextBoxColumn
            {
                HeaderText = "Màu hiện tại",
                Width = 85,
                ReadOnly = true,
                Name = "colCurrentColorPreview"
            };

            var colCurrentDesc = new DataGridViewTextBoxColumn
            {
                HeaderText = "Mô tả hiện tại trong CAD",
                Width = 160,
                ReadOnly = true,
                Name = "colCurrentDesc"
            };

            dgvLayers.Columns.AddRange(new DataGridViewColumn[]
            {
                colCheck, colLayerName, colPreset, colCategory, colMaterial, colNewColorPreview, colNewRgb, colCustomColorBtn, colCurrentColorPreview, colCurrentDesc
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

            tabLayerList.Controls.Add(dgvLayers);
            tabLayerList.Controls.Add(pnlBatchAssign);
            tabLayerList.Controls.Add(pnlTopFilter);
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
                Font = regularFont
            };

            var colPCode = new DataGridViewTextBoxColumn { HeaderText = "Mã", Width = 60, ReadOnly = true, Name = "colPCode" };
            var colPGroup = new DataGridViewTextBoxColumn { HeaderText = "Phân nhóm / Hạng mục", Width = 230, ReadOnly = true, Name = "colPGroup" };
            var colPMaterial = new DataGridViewTextBoxColumn { HeaderText = "Tên Vật liệu / Cấu kiện", Width = 280, ReadOnly = true, Name = "colPMaterial" };
            var colPColor = new DataGridViewTextBoxColumn { HeaderText = "Màu sắc", Width = 80, ReadOnly = true, Name = "colPColor" };
            var colPRgb = new DataGridViewTextBoxColumn { HeaderText = "RGB (R, G, B)", Width = 130, ReadOnly = true, Name = "colPRgb" };
            var colPHex = new DataGridViewTextBoxColumn { HeaderText = "Mã HEX", Width = 90, ReadOnly = true, Name = "colPHex" };
            var colPKeywords = new DataGridViewTextBoxColumn { HeaderText = "Từ khóa nhận diện tự động", Width = 240, ReadOnly = true, Name = "colPKeywords" };

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
                row.Cells["colPColor"].Value = ""; // Custom draw
                row.Cells["colPRgb"].Value = p.RgbText;
                row.Cells["colPHex"].Value = p.HexCode;
                row.Cells["colPKeywords"].Value = string.Join(", ", p.Keywords);
            }
        }
        #endregion

        #region Data Loading & Binding
        private void LoadLayersFromDrawing()
        {
            _allLayerRows.Clear();

            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var db = doc.Database;

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

                    var rowModel = new LayerEditRowModel
                    {
                        LayerName = name,
                        CurrentColor = drawCol,
                        CurrentColorDesc = colDesc,
                        CurrentDescription = ltr.Description ?? "",
                        Linetype = linetypeName,
                        IsLocked = ltr.IsLocked,
                        IsFrozen = ltr.IsFrozen,
                        IsOff = ltr.IsOff,
                        IsSelected = false
                    };

                    // Khôi phục từ _savedLayerStates nếu có
                    if (_savedLayerStates.TryGetValue(name, out var saved))
                    {
                        rowModel.SelectedPresetCode = saved.PresetCode;
                        rowModel.CategoryName = saved.CategoryName;
                        rowModel.MaterialName = saved.MaterialName;
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

            // Populate Comboboxes items
            var colPresetCombo = (DataGridViewComboBoxColumn)dgvLayers.Columns["colPreset"];
            colPresetCombo.Items.Clear();
            colPresetCombo.Items.Add("-- (Chưa gán mẫu màu) --");
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
                                          x.CategoryName.ToLower().Contains(keyword) ||
                                          x.MaterialName.ToLower().Contains(keyword)).ToList();

            foreach (var rowModel in filtered)
            {
                int rIdx = dgvLayers.Rows.Add();
                var row = dgvLayers.Rows[rIdx];
                row.Tag = rowModel;

                row.Cells["colCheck"].Value = rowModel.IsSelected;
                row.Cells["colLayerName"].Value = rowModel.LayerName;

                // Set ComboBox value
                string comboDisplay = "-- (Chưa gán mẫu màu) --";
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

                row.Cells["colCategory"].Value = rowModel.CategoryName;
                row.Cells["colMaterial"].Value = rowModel.MaterialName;
                row.Cells["colNewColorPreview"].Value = ""; // Custom draw
                row.Cells["colNewRgb"].Value = rowModel.NewRgbText;
                row.Cells["colCurrentColorPreview"].Value = ""; // Custom draw
                row.Cells["colCurrentDesc"].Value = rowModel.CurrentDescription;
            }
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
                if (row.Tag is LayerEditRowModel model)
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
                        var rect = new Rectangle(e.CellBounds.X + 6, e.CellBounds.Y + 4, e.CellBounds.Width - 12, e.CellBounds.Height - 8);
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
                            e.Graphics.DrawString("---", this.Font, brush, e.CellBounds.X + 15, e.CellBounds.Y + 6);
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
                    var rect = new Rectangle(e.CellBounds.X + 6, e.CellBounds.Y + 4, e.CellBounds.Width - 12, e.CellBounds.Height - 8);
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

        #region Grid Value Changed & Interaction Handlers
        private void DgvLayers_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            var row = dgvLayers.Rows[e.RowIndex];
            var model = row.Tag as LayerEditRowModel;
            if (model == null) return;

            string colName = dgvLayers.Columns[e.ColumnIndex].Name;

            if (colName == "colCheck")
            {
                model.IsSelected = Convert.ToBoolean(row.Cells["colCheck"].Value);
            }
            else if (colName == "colPreset")
            {
                string selectedText = row.Cells["colPreset"].Value?.ToString() ?? "";
                if (selectedText.StartsWith("--"))
                {
                    model.SelectedPresetCode = "";
                    model.CategoryName = "";
                    model.MaterialName = "";
                    model.NewR = null;
                    model.NewG = null;
                    model.NewB = null;
                    model.IsSelected = false;
                }
                else if (selectedText.Contains("[Tùy chỉnh]"))
                {
                    model.SelectedPresetCode = "CUSTOM";
                    PromptCustomColor(model, row, e.RowIndex);
                }
                else
                {
                    var preset = _presets.FirstOrDefault(p => p.DisplayName == selectedText);
                    if (preset != null)
                    {
                        model.SelectedPresetCode = preset.Code;
                        model.CategoryName = preset.GroupName;
                        model.MaterialName = preset.MaterialName;
                        model.NewR = preset.R;
                        model.NewG = preset.G;
                        model.NewB = preset.B;
                        model.IsSelected = true;
                    }
                }

                // Update row values
                row.Cells["colCheck"].Value = model.IsSelected;
                row.Cells["colCategory"].Value = model.CategoryName;
                row.Cells["colMaterial"].Value = model.MaterialName;
                row.Cells["colNewRgb"].Value = model.NewRgbText;
                dgvLayers.InvalidateCell(row.Cells["colNewColorPreview"]);
            }
            else if (colName == "colCategory")
            {
                model.CategoryName = row.Cells["colCategory"].Value?.ToString() ?? "";
            }
            else if (colName == "colMaterial")
            {
                model.MaterialName = row.Cells["colMaterial"].Value?.ToString() ?? "";
            }
        }

        private void DgvLayers_CellContentClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            string colName = dgvLayers.Columns[e.ColumnIndex].Name;

            var row = dgvLayers.Rows[e.RowIndex];
            var model = row.Tag as LayerEditRowModel;
            if (model == null) return;

            if (colName == "colCustomColorBtn")
            {
                PromptCustomColor(model, row, e.RowIndex);
            }
        }

        private void PromptCustomColor(LayerEditRowModel model, DataGridViewRow row, int rowIndex)
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
                    AppendLog($"Đã gán màu tùy chỉnh RGB({cd.Color.R},{cd.Color.G},{cd.Color.B}) cho Layer '{model.LayerName}'.");
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
                    model.CategoryName = matchedPreset.GroupName;
                    model.MaterialName = matchedPreset.MaterialName;
                    model.NewR = matchedPreset.R;
                    model.NewG = matchedPreset.G;
                    model.NewB = matchedPreset.B;
                    model.IsSelected = true;
                    matchedCount++;
                }
            }

            FilterLayersGrid();
            AppendLog($"⚡ Đã tự động nhận diện và gán mẫu màu cho {matchedCount}/{_allLayerRows.Count} Layer theo từ khóa.");
        }

        private BimStandardPreset? FindBestMatchingPreset(string layerName)
        {
            if (string.IsNullOrEmpty(layerName)) return null;
            string cleanLayer = NormalizeString(layerName);

            // Duyệt qua từng preset và kiểm tra keywords
            foreach (var preset in _presets)
            {
                // So khớp tên vật liệu
                string cleanMaterial = NormalizeString(preset.MaterialName);
                if (cleanLayer.Contains(cleanMaterial))
                    return preset;

                // So khớp danh sách từ khóa
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
                MessageBox.Show("Vui lòng chọn 1 mẫu màu từ danh sách thả xuống!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selectedLayers = _allLayerRows.Where(x => x.IsSelected).ToList();
            if (selectedLayers.Count == 0)
            {
                MessageBox.Show("Vui lòng tích chọn ít nhất 1 Layer trong bảng để gán nhanh mẫu màu!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            foreach (var model in selectedLayers)
            {
                model.SelectedPresetCode = preset.Code;
                model.CategoryName = preset.GroupName;
                model.MaterialName = preset.MaterialName;
                model.NewR = preset.R;
                model.NewG = preset.G;
                model.NewB = preset.B;
            }

            FilterLayersGrid();
            AppendLog($"👉 Đã gán mẫu màu [{preset.MaterialName}] cho {selectedLayers.Count} Layer được chọn.");
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
                AppendLog($"🎯 Đã tìm thấy Layer '{selectedLayerName}' từ đối tượng vừa chọn.");
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
        }
        #endregion

        #region Execute Updates
        private void BtnExecute_Click(object? sender, EventArgs e)
        {
            var layersToUpdate = _allLayerRows.Where(x => x.IsSelected && x.NewColor.HasValue).ToList();

            if (layersToUpdate.Count == 0)
            {
                MessageBox.Show("Không có Layer nào được tích chọn hoặc chưa chọn mẫu màu mới để cập nhật!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            bool applyByLayer = chkApplyByLayer.Checked;
            bool updateDescription = chkUpdateDescription.Checked;
            bool unlockLayers = chkUnlockLayers.Checked;

            try
            {
                btnExecute.Enabled = false;
                Cursor.Current = Cursors.WaitCursor;

                int count = CapNhatMauLayerCmd.ExecuteUpdateLayerColors(
                    layersToUpdate.Select(x => (
                        layerName: x.LayerName,
                        r: x.NewR!.Value,
                        g: x.NewG!.Value,
                        b: x.NewB!.Value,
                        categoryName: x.CategoryName,
                        materialName: x.MaterialName
                    )).ToList(),
                    applyByLayer,
                    updateDescription,
                    unlockLayers,
                    msg => AppendLog(msg)
                );

                SaveCurrentSettings();
                LoadLayersFromDrawing();
                PopulateLayersGrid();

                MessageBox.Show($"Đã cập nhật thành công {count} Layer theo bảng mẫu màu!", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

        #region Helpers & Settings
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
                Height = 32,
                BackColor = bgColor,
                ForeColor = DrawingColor.White,
                FlatStyle = FlatStyle.Flat,
                Font = font,
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private void CapNhatMauLayerForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            SaveCurrentSettings();
        }

        private void SaveCurrentSettings()
        {
            _savedLayerStates.Clear();
            foreach (var r in _allLayerRows)
            {
                if (!string.IsNullOrEmpty(r.SelectedPresetCode) || r.IsSelected)
                {
                    _savedLayerStates[r.LayerName] = new LayerSavedState
                    {
                        PresetCode = r.SelectedPresetCode,
                        CategoryName = r.CategoryName,
                        MaterialName = r.MaterialName,
                        R = r.NewR,
                        G = r.NewG,
                        B = r.NewB,
                        IsSelected = r.IsSelected
                    };
                }
            }

            _lastApplyByLayer = chkApplyByLayer.Checked;
            _lastUpdateDescription = chkUpdateDescription.Checked;
            _lastUnlockLayers = chkUnlockLayers.Checked;
            _lastSelectedTab = tabControlMain.SelectedIndex;
            _lastFormSize = this.Size;
        }

        private void RestoreLastSettings()
        {
            chkApplyByLayer.Checked = _lastApplyByLayer;
            chkUpdateDescription.Checked = _lastUpdateDescription;
            chkUnlockLayers.Checked = _lastUnlockLayers;
            if (_lastSelectedTab >= 0 && _lastSelectedTab < tabControlMain.TabCount)
            {
                tabControlMain.SelectedIndex = _lastSelectedTab;
            }
        }
        #endregion
    }
}

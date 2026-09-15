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
using Autodesk.Aec.PropertyData.DatabaseServices;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Color = Autodesk.AutoCAD.Colors.Color;
using DrawingColor = System.Drawing.Color;
using DrawingFont = System.Drawing.Font;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsPoint = System.Drawing.Point;

namespace Civil3DCsharp
{
    /// <summary>
    /// Model dữ liệu cho mỗi dòng Layer trong bảng cập nhật Màu và Property Set theo chuẩn BIM
    /// </summary>
    public class LayerBimRowModel
    {
        public bool IsSelected { get; set; } = false;
        public string LayerName { get; set; } = "";

        public int SolidCount { get; set; } = 0;              // Số lượng 3D Solid / Body thuộc layer này
        public string SelectedPresetCode { get; set; } = ""; // Mã mẫu (hoặc "CUSTOM" / "")
        public string CauKien { get; set; } = "";             // Hạng mục / Cấu kiện (dùng cho Layer Description & Property Set "Tên cấu kiện")
        public string VatLieu { get; set; } = "";             // Vật liệu (dùng cho Layer Description & Property Set "Loại vật liệu")
        public string DoChat { get; set; } = "";              // Độ chặt (Property Set "Độ chặt", VD: K95, K98)
        public double? BeDay { get; set; }                    // Bề dày m (Property Set "Bề dày", VD: 0.05, 0.15)
        public string HangMuc { get; set; } = "Đường giao thông"; // Hạng mục công trình (Property Set "Hạng mục")

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
        public string BeDayText => BeDay.HasValue ? BeDay.Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) : "";
    }

    /// <summary>
    /// Lưu vết trạng thái cho Layer giữa các lần chạy
    /// </summary>
    public class LayerBimSavedState
    {
        public string PresetCode { get; set; } = "";
        public string CauKien { get; set; } = "";
        public string VatLieu { get; set; } = "";
        public string DoChat { get; set; } = "";
        public double? BeDay { get; set; }
        public string HangMuc { get; set; } = "";
        public byte? R { get; set; }
        public byte? G { get; set; }
        public byte? B { get; set; }
        public bool IsSelected { get; set; }
    }

    /// <summary>
    /// Form giao diện Cập Nhật Màu Layer & Thuộc Tính Property Set cho 3D Solid theo Tiêu Chuẩn BIM / EIR (BEP T27)
    /// Hỗ trợ Xuất/Nhập Excel, Tự động nhận diện từ khóa, Pick CAD và ghi nhớ toàn bộ thông số.
    /// </summary>
    public class CapNhatMauVaPropertySetForm : Form
    {
        #region Persistent State (Ghi nhớ giữa các lần chạy)
        private static Dictionary<string, LayerBimSavedState> _savedLayerStates = new(StringComparer.OrdinalIgnoreCase);
        private static string _lastExcelPath = "";
        private static bool _lastUpdateColor = true;
        private static bool _lastUpdateDescription = true;
        private static bool _lastApplyByLayer = true;
        private static bool _lastUnlockLayers = true;
        private static bool _lastUpdatePropertySet = true;
        private static bool _lastOnlyMissing = true;
        private static string _lastTenCongTrinh = "";
        private static string _lastViTri = "";
        private static string _lastNhomCauKien = "Hạ tầng kỹ thuật";
        private static string _lastHangMuc = "Đường giao thông";
        private static Size _lastFormSize = new Size(1280, 780);
        private static int _lastSelectedTab = 0;
        #endregion

        #region Smart Extractors: Độ chặt & Bề dày từ Tên Layer
        public static string ExtractDoChat(string layerName)
        {
            if (string.IsNullOrEmpty(layerName)) return "";
            var m = Regex.Match(layerName, @"\bK\s*([0-9]{2,3})\b", RegexOptions.IgnoreCase);
            if (m.Success) return "K" + m.Groups[1].Value;
            var m2 = Regex.Match(layerName, @"K\s*=\s*0?\.?([0-9]{2,3})", RegexOptions.IgnoreCase);
            if (m2.Success) return "K" + m2.Groups[1].Value;
            return "";
        }

        public static double? ExtractBeDay(string layerName)
        {
            if (string.IsNullOrEmpty(layerName)) return null;

            // Pattern 1: có từ khóa day, chieu day, h=, d=, t=
            var m = Regex.Match(layerName, @"(?:d[aà]y|chieu\s*d[aà]y|thickness|[_\s-]h\s*=|d\s*=|t\s*=)[_\s-]*(\d+(?:[.,]\d+)?)\s*(cm|mm|m)?", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                if (double.TryParse(m.Groups[1].Value.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
                {
                    string unit = m.Groups[2].Value.ToLowerInvariant();
                    if (unit == "cm") return Math.Round(val / 100.0, 3);
                    if (unit == "mm") return Math.Round(val / 1000.0, 3);
                    if (unit == "m") return Math.Round(val, 3);
                    if (val >= 1.0) return Math.Round(val / 100.0, 3);
                    return Math.Round(val, 3);
                }
            }

            // Pattern 2: số liền kề đơn vị cm hoặc mm (vd: 5cm, 15cm, 20cm, 50mm)
            var m2 = Regex.Match(layerName, @"[_\s-](\d+(?:[.,]\d+)?)\s*(cm|mm)\b", RegexOptions.IgnoreCase);
            if (m2.Success)
            {
                if (double.TryParse(m2.Groups[1].Value.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
                {
                    string unit = m2.Groups[2].Value.ToLowerInvariant();
                    if (unit == "cm") return Math.Round(val / 100.0, 3);
                    if (unit == "mm") return Math.Round(val / 1000.0, 3);
                }
            }

            return null;
        }
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
        private Label lblBatchBeDay = null!;
        private TextBox txtBatchBeDay = null!;
        private Button btnApplyBatchBeDay = null!;
        private Label lblStats = null!;

        private DataGridView dgvLayers = null!;

        // Tab 2: Preset Management & Reference
        private Panel pnlPresetToolbar = null!;
        private Button btnAddPreset = null!;
        private Button btnPickPresetColor = null!;
        private Button btnDeletePreset = null!;
        private Button btnSavePresets = null!;
        private Button btnResetPresets = null!;
        private Button btnImportPresetExcel = null!;
        private Button btnExportPresetExcel = null!;
        private Label lblPresetStats = null!;
        private DataGridView dgvPresets = null!;

        // Bottom Configuration & Actions
        private Panel pnlBottom = null!;

        // Group 0: Project Information
        private GroupBox grpProjectInfo = null!;
        private Label lblTenCongTrinh = null!;
        private TextBox txtTenCongTrinh = null!;
        private Label lblViTri = null!;
        private TextBox txtViTri = null!;
        private Label lblNhomCauKien = null!;
        private TextBox txtNhomCauKien = null!;
        private Label lblHangMuc = null!;
        private TextBox txtHangMuc = null!;

        // Group 1: Excel
        private GroupBox grpExcelConfig = null!;
        private Button btnImportExcel = null!;
        private Button btnExportExcel = null!;
        private Label lblExcelHint = null!;

        // Group 2: Options
        private GroupBox grpOptions = null!;
        private CheckBox chkUpdatePropertySet = null!;
        private CheckBox chkOnlyMissing = null!;
        private CheckBox chkUpdateColor = null!;
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
            _presets = BimPresetManager.LoadPresets();

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
            this.Text = "Cập Nhật Màu & Property Set Cho 3D Solid (Chuẩn BIM / EIR)";
            this.Size = _lastFormSize;
            this.MinimumSize = new Size(1080, 640);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = regularFont;
            this.BackColor = DrawingColor.FromArgb(246, 248, 250);

            // ================= 1. HEADER BANNER =================
            pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 62,
                BackColor = DrawingColor.FromArgb(27, 54, 93),
                Padding = new Padding(16, 8, 16, 8)
            };

            lblHeaderTitle = new Label
            {
                Text = "🎨 CẬP NHẬT MÀU & PROPERTY SET CHO 3D SOLID (CHUẨN BIM / EIR)",
                Font = titleFont,
                ForeColor = DrawingColor.White,
                AutoSize = true,
                Location = new Point(14, 8)
            };

            lblHeaderSub = new Label
            {
                Text = "Tự động nhận diện màu sắc, cấu kiện, vật liệu, độ chặt, bề dày. Tính toán Diện tích (S=V/h) & Khối lượng Property Set cho 3D Solid.",
                Font = subFont,
                ForeColor = DrawingColor.FromArgb(200, 220, 245),
                AutoSize = true,
                Location = new Point(16, 34)
            };

            pnlHeader.Controls.Add(lblHeaderTitle);
            pnlHeader.Controls.Add(lblHeaderSub);

            // ================= 2. TAB CONTROL =================
            tabControlMain = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = boldFont,
                Padding = new Point(14, 6)
            };

            tabMainCoordination = new TabPage { Text = "🎨 Cập Nhật Màu & Property Set (3D Solid)", BackColor = DrawingColor.White };
            tabPresetReference = new TabPage { Text = "📚 Bảng Tra Cứu & Quản Lý Mẫu Màu (BIM / EIR)", BackColor = DrawingColor.White };

            tabControlMain.TabPages.Add(tabMainCoordination);
            tabControlMain.TabPages.Add(tabPresetReference);

            BuildTabMainCoordination(regularFont, boldFont);
            BuildTabPresetReference(regularFont, boldFont);

            // ================= 3. BOTTOM PANEL =================
            pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 285,
                Padding = new Padding(10, 4, 10, 8),
                BackColor = DrawingColor.FromArgb(246, 248, 250)
            };

            // Group 0: Project Information (Property Set "1. Thông tin dự án")
            grpProjectInfo = new GroupBox
            {
                Text = "🏢 Thông tin dự án (Gán cho Property Set '1. Thông tin dự án' nếu còn thiếu)",
                Font = boldFont,
                Dock = DockStyle.Top,
                Height = 50,
                Padding = new Padding(8, 2, 8, 2)
            };

            lblTenCongTrinh = new Label
            {
                Text = "Tên công trình:",
                Font = regularFont,
                AutoSize = true,
                Location = new Point(10, 20)
            };

            txtTenCongTrinh = new TextBox
            {
                Location = new Point(102, 17),
                Width = 230,
                Font = regularFont,
                Text = _lastTenCongTrinh
            };

            lblViTri = new Label
            {
                Text = "Vị trí:",
                Font = regularFont,
                AutoSize = true,
                Location = new Point(342, 20)
            };

            txtViTri = new TextBox
            {
                Location = new Point(382, 17),
                Width = 190,
                Font = regularFont,
                Text = _lastViTri
            };

            lblNhomCauKien = new Label
            {
                Text = "Nhóm cấu kiện:",
                Font = regularFont,
                AutoSize = true,
                Location = new Point(582, 20)
            };

            txtNhomCauKien = new TextBox
            {
                Location = new Point(680, 17),
                Width = 140,
                Font = regularFont,
                Text = _lastNhomCauKien
            };

            lblHangMuc = new Label
            {
                Text = "Hạng mục mặc định:",
                Font = regularFont,
                AutoSize = true,
                Location = new Point(830, 20)
            };

            txtHangMuc = new TextBox
            {
                Location = new Point(955, 17),
                Width = 150,
                Font = regularFont,
                Text = _lastHangMuc
            };

            grpProjectInfo.Controls.AddRange(new Control[]
            {
                lblTenCongTrinh, txtTenCongTrinh,
                lblViTri, txtViTri,
                lblNhomCauKien, txtNhomCauKien,
                lblHangMuc, txtHangMuc
            });

            // Group 1: Excel Config
            grpExcelConfig = new GroupBox
            {
                Text = "📊 Tái Sử Dụng Cấu Hình Qua Excel",
                Font = boldFont,
                Dock = DockStyle.Top,
                Height = 52,
                Padding = new Padding(8, 2, 8, 2)
            };

            btnImportExcel = CreateFlatButton("📥 Nhập từ Excel (.xlsx)", 175, DrawingColor.FromArgb(13, 110, 253), boldFont);
            btnImportExcel.Location = new Point(12, 16);
            btnImportExcel.Click += BtnImportExcel_Click;

            btnExportExcel = CreateFlatButton("📤 Xuất ra Excel (.xlsx)", 175, DrawingColor.FromArgb(25, 135, 84), boldFont);
            btnExportExcel.Location = new Point(195, 16);
            btnExportExcel.Click += BtnExportExcel_Click;

            lblExcelHint = new Label
            {
                Text = "💡 Xuất ra Excel để chỉnh sửa hàng loạt hoặc nạp file cấu hình có sẵn để tự động gán cho bản vẽ.",
                Font = subFont,
                ForeColor = DrawingColor.FromArgb(100, 110, 120),
                AutoSize = true,
                Location = new Point(385, 21)
            };

            grpExcelConfig.Controls.Add(btnImportExcel);
            grpExcelConfig.Controls.Add(btnExportExcel);
            grpExcelConfig.Controls.Add(lblExcelHint);

            // Group 2: Options
            grpOptions = new GroupBox
            {
                Text = "⚙️ Tùy chọn thực thi",
                Font = boldFont,
                Dock = DockStyle.Top,
                Height = 50,
                Padding = new Padding(8, 2, 8, 2)
            };

            chkUpdatePropertySet = new CheckBox
            {
                Text = "🔹 Cập nhật Property Set cho 3D Solid",
                Font = boldFont,
                ForeColor = DrawingColor.FromArgb(10, 50, 120),
                AutoSize = true,
                Location = new Point(12, 18),
                Checked = _lastUpdatePropertySet
            };

            chkOnlyMissing = new CheckBox
            {
                Text = "🔹 Chỉ điền thuộc tính còn thiếu (không ghi đè)",
                Font = boldFont,
                ForeColor = DrawingColor.FromArgb(20, 110, 50),
                AutoSize = true,
                Location = new Point(275, 18),
                Checked = _lastOnlyMissing
            };

            chkUpdateColor = new CheckBox
            {
                Text = "Đổi Màu Layer",
                Font = regularFont,
                AutoSize = true,
                Location = new Point(590, 18),
                Checked = _lastUpdateColor
            };

            chkUpdateDescription = new CheckBox
            {
                Text = "Ghi Description",
                Font = regularFont,
                AutoSize = true,
                Location = new Point(720, 18),
                Checked = _lastUpdateDescription
            };

            chkApplyByLayer = new CheckBox
            {
                Text = "Về ByLayer",
                Font = regularFont,
                AutoSize = true,
                Location = new Point(855, 18),
                Checked = _lastApplyByLayer
            };

            chkUnlockLayers = new CheckBox
            {
                Text = "Tự mở khóa Layer",
                Font = regularFont,
                AutoSize = true,
                Location = new Point(965, 18),
                Checked = _lastUnlockLayers
            };

            grpOptions.Controls.Add(chkUpdatePropertySet);
            grpOptions.Controls.Add(chkOnlyMissing);
            grpOptions.Controls.Add(chkUpdateColor);
            grpOptions.Controls.Add(chkUpdateDescription);
            grpOptions.Controls.Add(chkApplyByLayer);
            grpOptions.Controls.Add(chkUnlockLayers);

            // Group 3: Log Box
            grpLog = new GroupBox
            {
                Text = "📋 Nhật ký tiến trình",
                Font = boldFont,
                Dock = DockStyle.Fill,
                Padding = new Padding(6)
            };

            txtLog = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                BackColor = DrawingColor.FromArgb(250, 252, 255),
                Font = new DrawingFont("Consolas", 8.25F, FontStyle.Regular),
                BorderStyle = BorderStyle.None
            };
            grpLog.Controls.Add(txtLog);

            // Action Buttons
            pnlButtons = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 36,
                Padding = new Padding(0, 4, 0, 0)
            };

            btnExecute = CreateFlatButton("🚀 Cập Nhật Ngay", 170, DrawingColor.FromArgb(220, 53, 69), boldFont);
            btnExecute.Dock = DockStyle.Right;
            btnExecute.Click += BtnExecute_Click;

            btnClose = CreateFlatButton("Đóng", 100, DrawingColor.FromArgb(108, 117, 125), regularFont);
            btnClose.Dock = DockStyle.Left;
            btnClose.Click += (s, e) => this.Close();

            pnlButtons.Controls.Add(btnExecute);
            pnlButtons.Controls.Add(btnClose);

            pnlBottom.Controls.Add(grpLog);
            pnlBottom.Controls.Add(grpOptions);
            pnlBottom.Controls.Add(grpExcelConfig);
            pnlBottom.Controls.Add(grpProjectInfo);
            pnlBottom.Controls.Add(pnlButtons);

            // Add all main controls
            this.Controls.Add(tabControlMain);
            this.Controls.Add(pnlBottom);
            this.Controls.Add(pnlHeader);

            this.FormClosing += CapNhatMauVaPropertySetForm_FormClosing;
            this.ResumeLayout(false);
        }

        #region Build Tab 1: Main Coordination
        private void BuildTabMainCoordination(DrawingFont regularFont, DrawingFont boldFont)
        {
            // Panel 1: Toolbar
            pnlToolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 38,
                BackColor = DrawingColor.FromArgb(243, 245, 248),
                Padding = new Padding(8, 4, 8, 4)
            };

            lblSearch = new Label
            {
                Text = "🔍 Tìm kiếm:",
                AutoSize = true,
                Font = boldFont,
                Location = new Point(8, 9)
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
                AppendLog("Đã nạp lại danh sách Layer từ bản vẽ.");
            };

            pnlToolbar.Controls.AddRange(new Control[]
            {
                lblSearch, txtSearch, btnPickObject, btnAutoMatchAll, btnSelectAll, btnDeselectAll, btnRefresh
            });

            // Panel 2: Batch Assign & Stats
            pnlBatch = new Panel
            {
                Dock = DockStyle.Top,
                Height = 38,
                BackColor = DrawingColor.FromArgb(248, 249, 250),
                Padding = new Padding(8, 3, 8, 3)
            };

            lblBatch = new Label
            {
                Text = "⚡ Mẫu BIM:",
                AutoSize = true,
                Font = boldFont,
                ForeColor = DrawingColor.FromArgb(10, 50, 100),
                Location = new Point(8, 9)
            };

            cboBatchPreset = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 230,
                Location = new Point(90, 6),
                Font = regularFont
            };

            btnApplyBatchPreset = CreateFlatButton("👉 Gán Mẫu", 85, DrawingColor.FromArgb(220, 53, 69), boldFont);
            btnApplyBatchPreset.Location = new Point(325, 5);
            btnApplyBatchPreset.Click += BtnApplyBatchPreset_Click;

            lblBatchBeDay = new Label
            {
                Text = "📏 Bề dày kết cấu (m):",
                AutoSize = true,
                Font = boldFont,
                ForeColor = DrawingColor.FromArgb(180, 50, 0),
                Location = new Point(425, 9)
            };

            txtBatchBeDay = new TextBox
            {
                Location = new Point(565, 6),
                Width = 55,
                Font = regularFont,
                Text = "0.05",
                TextAlign = HorizontalAlignment.Right
            };

            btnApplyBatchBeDay = CreateFlatButton("📏 Gán Bề Dày", 105, DrawingColor.FromArgb(13, 110, 253), boldFont);
            btnApplyBatchBeDay.Location = new Point(625, 5);
            btnApplyBatchBeDay.Click += BtnApplyBatchBeDay_Click;

            lblStats = new Label
            {
                Text = "Đang tải dữ liệu...",
                AutoSize = true,
                Font = boldFont,
                ForeColor = DrawingColor.FromArgb(20, 80, 150),
                Location = new Point(740, 9)
            };

            pnlBatch.Controls.AddRange(new Control[]
            {
                lblBatch, cboBatchPreset, btnApplyBatchPreset,
                lblBatchBeDay, txtBatchBeDay, btnApplyBatchBeDay,
                lblStats
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
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    Font = boldFont,
                    ForeColor = DrawingColor.FromArgb(10, 50, 120)
                }
            };

            var colPreset = new DataGridViewComboBoxColumn
            {
                HeaderText = "Chọn Mẫu BIM / EIR (BEP T27)",
                Width = 210,
                Name = "colPreset",
                FlatStyle = FlatStyle.Flat
            };

            var colCauKien = new DataGridViewTextBoxColumn
            {
                HeaderText = "Tên cấu kiện",
                Width = 140,
                Name = "colCauKien"
            };

            var colVatLieu = new DataGridViewTextBoxColumn
            {
                HeaderText = "Loại vật liệu",
                Width = 140,
                Name = "colVatLieu"
            };

            var colDoChat = new DataGridViewTextBoxColumn
            {
                HeaderText = "Độ chặt",
                Width = 75,
                Name = "colDoChat",
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            };

            var colBeDay = new DataGridViewTextBoxColumn
            {
                HeaderText = "Bề dày h (m)",
                Width = 100,
                Name = "colBeDay",
                ToolTipText = "Bề dày kết cấu theo Layer (m). Dùng để tính Diện tích S = V / h",
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    BackColor = DrawingColor.FromArgb(255, 255, 230),
                    ForeColor = DrawingColor.FromArgb(170, 40, 0),
                    Font = boldFont
                }
            };

            var colHangMuc = new DataGridViewTextBoxColumn
            {
                HeaderText = "Hạng mục",
                Width = 130,
                Name = "colHangMuc"
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
                Width = 160,
                ReadOnly = true,
                Name = "colCurrentDesc"
            };

            dgvLayers.Columns.AddRange(new DataGridViewColumn[]
            {
                colCheck, colLayerName, colSolidCount, colPreset, colCauKien, colVatLieu,
                colDoChat, colBeDay, colHangMuc,
                colNewColorPreview, colNewRgb, colCustomColorBtn, colCurrentColorPreview, colCurrentDesc
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

        #region Build Tab 2: Preset Reference & Management
        private void BuildTabPresetReference(DrawingFont regularFont, DrawingFont boldFont)
        {
            pnlPresetToolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 38,
                BackColor = DrawingColor.FromArgb(243, 245, 248),
                Padding = new Padding(8, 4, 8, 4)
            };

            btnAddPreset = CreateFlatButton("➕ Thêm Mẫu Mới", 130, DrawingColor.FromArgb(25, 135, 84), boldFont);
            btnAddPreset.Location = new Point(8, 4);
            btnAddPreset.Click += BtnAddPreset_Click;

            btnPickPresetColor = CreateFlatButton("🎨 Chọn Màu...", 110, DrawingColor.FromArgb(111, 66, 193), boldFont);
            btnPickPresetColor.Location = new Point(144, 4);
            btnPickPresetColor.Click += BtnPickPresetColor_Click;

            btnDeletePreset = CreateFlatButton("🗑️ Xóa Mẫu", 90, DrawingColor.FromArgb(220, 53, 69), regularFont);
            btnDeletePreset.Location = new Point(260, 4);
            btnDeletePreset.Click += BtnDeletePreset_Click;

            btnSavePresets = CreateFlatButton("💾 Lưu & Cập Nhật Bảng Mẫu", 200, DrawingColor.FromArgb(13, 110, 253), boldFont);
            btnSavePresets.Location = new Point(356, 4);
            btnSavePresets.Click += BtnSavePresets_Click;

            btnResetPresets = CreateFlatButton("🔄 Mặc Định Ban Đầu", 145, DrawingColor.FromArgb(108, 117, 125), regularFont);
            btnResetPresets.Location = new Point(562, 4);
            btnResetPresets.Click += BtnResetPresets_Click;

            btnImportPresetExcel = CreateFlatButton("📥 Nạp Mẫu Từ Excel", 150, DrawingColor.FromArgb(10, 88, 202), regularFont);
            btnImportPresetExcel.Location = new Point(713, 4);
            btnImportPresetExcel.Click += BtnImportPresetExcel_Click;

            btnExportPresetExcel = CreateFlatButton("📤 Xuất Mẫu Ra Excel", 150, DrawingColor.FromArgb(20, 108, 67), regularFont);
            btnExportPresetExcel.Location = new Point(869, 4);
            btnExportPresetExcel.Click += BtnExportPresetExcel_Click;

            lblPresetStats = new Label
            {
                Text = "0 mẫu",
                AutoSize = true,
                Font = boldFont,
                ForeColor = DrawingColor.FromArgb(50, 70, 90),
                Location = new Point(1025, 10)
            };

            pnlPresetToolbar.Controls.AddRange(new Control[]
            {
                btnAddPreset, btnPickPresetColor, btnDeletePreset, btnSavePresets,
                btnResetPresets, btnImportPresetExcel, btnExportPresetExcel, lblPresetStats
            });

            // DataGridView dgvPresets
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

            var colPCode = new DataGridViewTextBoxColumn
            {
                HeaderText = "Mã Mẫu",
                Width = 70,
                Name = "colPCode"
            };

            var colPGroup = new DataGridViewTextBoxColumn
            {
                HeaderText = "Hạng Mục / Nhóm Cấu Kiện",
                Width = 240,
                Name = "colPGroup"
            };

            var colPMaterial = new DataGridViewTextBoxColumn
            {
                HeaderText = "Tên Cấu Kiện / Vật Liệu",
                Width = 240,
                Name = "colPMaterial"
            };

            var colPColor = new DataGridViewTextBoxColumn
            {
                HeaderText = "Màu Sắc",
                Width = 75,
                ReadOnly = true,
                Name = "colPColor"
            };

            var colPRgb = new DataGridViewTextBoxColumn
            {
                HeaderText = "Mã RGB",
                Width = 100,
                ReadOnly = true,
                Name = "colPRgb"
            };

            var colPHex = new DataGridViewTextBoxColumn
            {
                HeaderText = "Mã HEX (#RRGGBB)",
                Width = 135,
                Name = "colPHex"
            };

            var colPKeywords = new DataGridViewTextBoxColumn
            {
                HeaderText = "Từ Khóa Nhận Diện (phân cách bằng dấu phẩy)",
                Width = 280,
                Name = "colPKeywords"
            };

            var colPPickBtn = new DataGridViewButtonColumn
            {
                HeaderText = "Đổi Màu",
                Text = "🎨",
                UseColumnTextForButtonValue = true,
                Width = 65,
                Name = "colPPickBtn",
                FlatStyle = FlatStyle.Flat
            };

            dgvPresets.Columns.AddRange(new DataGridViewColumn[]
            {
                colPCode, colPGroup, colPMaterial, colPColor, colPRgb, colPHex, colPKeywords, colPPickBtn
            });

            dgvPresets.CellPainting += DgvPresets_CellPainting;
            dgvPresets.CellContentClick += DgvPresets_CellContentClick;
            dgvPresets.CellDoubleClick += DgvPresets_CellDoubleClick;
            dgvPresets.CellValueChanged += DgvPresets_CellValueChanged;
            dgvPresets.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (dgvPresets.IsCurrentCellDirty)
                {
                    dgvPresets.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };

            tabPresetReference.Controls.Add(dgvPresets);
            tabPresetReference.Controls.Add(pnlPresetToolbar);
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
            UpdatePresetStats();
        }

        private void UpdatePresetStats()
        {
            if (lblPresetStats != null && !lblPresetStats.IsDisposed)
            {
                lblPresetStats.Text = $"📊 {_presets.Count} mẫu chuẩn";
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

            using (var tr = db.TransactionManager.StartTransaction())
            {
                // Đếm số lượng 3D Solid / Body theo Layer
                var solidCountByLayer = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                    foreach (ObjectId entId in ms)
                    {
                        if (entId.ObjectClass.DxfName.Equals("3DSOLID", StringComparison.OrdinalIgnoreCase) ||
                            entId.ObjectClass.DxfName.Equals("BODY", StringComparison.OrdinalIgnoreCase))
                        {
                            var ent = tr.GetObject(entId, OpenMode.ForRead) as Entity;
                            if (ent != null)
                            {
                                string lay = ent.Layer;
                                solidCountByLayer[lay] = solidCountByLayer.TryGetValue(lay, out int count) ? count + 1 : 1;
                            }
                        }
                    }
                }
                catch { }

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

                    int solidCount = solidCountByLayer.TryGetValue(name, out int sc) ? sc : 0;
                    double? beDayExtracted = ExtractBeDay(name);
                    string doChatExtracted = ExtractDoChat(name);

                    var rowModel = new LayerBimRowModel
                    {
                        LayerName = name,
                        SolidCount = solidCount,
                        CurrentColor = drawCol,
                        CurrentColorDesc = colDesc,
                        CurrentDescription = ltr.Description ?? "",
                        Linetype = linetypeName,
                        IsLocked = ltr.IsLocked,
                        IsFrozen = ltr.IsFrozen,
                        IsOff = ltr.IsOff,
                        IsSelected = solidCount > 0,
                        DoChat = doChatExtracted,
                        BeDay = beDayExtracted,
                        HangMuc = _lastHangMuc
                    };

                    // Khôi phục từ bộ nhớ tạm (Persistent State) nếu có
                    if (_savedLayerStates.TryGetValue(name, out var saved))
                    {
                        rowModel.SelectedPresetCode = saved.PresetCode;
                        rowModel.CauKien = saved.CauKien;
                        rowModel.VatLieu = saved.VatLieu;
                        if (!string.IsNullOrEmpty(saved.DoChat)) rowModel.DoChat = saved.DoChat;
                        if (saved.BeDay.HasValue) rowModel.BeDay = saved.BeDay;
                        if (!string.IsNullOrEmpty(saved.HangMuc)) rowModel.HangMuc = saved.HangMuc;
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
            PopulatePresetDropdowns();
        }

        private void PopulatePresetDropdowns()
        {
            var colPresetCombo = (DataGridViewComboBoxColumn)dgvLayers.Columns["colPreset"];
            colPresetCombo.Items.Clear();
            colPresetCombo.Items.Add("-- (Chưa gán mẫu) --");
            foreach (var p in _presets)
            {
                colPresetCombo.Items.Add(p.DisplayName);
            }
            colPresetCombo.Items.Add("🎨 [Tùy chỉnh] Tự chọn màu...");

            string prevBatch = cboBatchPreset.SelectedItem?.ToString() ?? "";
            cboBatchPreset.Items.Clear();
            foreach (var p in _presets)
            {
                cboBatchPreset.Items.Add(p.DisplayName);
            }
            if (!string.IsNullOrEmpty(prevBatch) && cboBatchPreset.Items.Contains(prevBatch))
            {
                cboBatchPreset.SelectedItem = prevBatch;
            }
            else if (cboBatchPreset.Items.Count > 0)
            {
                cboBatchPreset.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// Đồng bộ danh sách mẫu màu sang Tab 1 (Layer Grid và Batch Assign)
        /// </summary>
        private void SyncPresetsToMainTab()
        {
            PopulatePresetDropdowns();

            // Đồng bộ lại các model Layer đang dùng mẫu
            foreach (var model in _allLayerRows)
            {
                if (!string.IsNullOrEmpty(model.SelectedPresetCode) && model.SelectedPresetCode != "CUSTOM")
                {
                    var matched = _presets.FirstOrDefault(p => p.Code == model.SelectedPresetCode);
                    if (matched != null)
                    {
                        model.CauKien = matched.GroupName;
                        model.VatLieu = matched.MaterialName;
                        model.NewR = matched.R;
                        model.NewG = matched.G;
                        model.NewB = matched.B;
                    }
                }
            }

            FilterLayersGrid();
            UpdatePresetStats();
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
                                           x.VatLieu.ToLower().Contains(keyword) ||
                                           x.HangMuc.ToLower().Contains(keyword)).ToList();

            foreach (var rowModel in filtered)
            {
                int rIdx = dgvLayers.Rows.Add();
                var row = dgvLayers.Rows[rIdx];
                row.Tag = rowModel;

                row.Cells["colCheck"].Value = rowModel.IsSelected;
                row.Cells["colLayerName"].Value = rowModel.LayerName;
                row.Cells["colSolidCount"].Value = rowModel.SolidCount > 0 ? rowModel.SolidCount.ToString("N0") : "-";

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
                row.Cells["colDoChat"].Value = rowModel.DoChat;
                row.Cells["colBeDay"].Value = rowModel.BeDayText;
                row.Cells["colHangMuc"].Value = rowModel.HangMuc;
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
            int selectedLayers = _allLayerRows.Count(x => x.IsSelected);
            int totalSolids = _allLayerRows.Sum(x => x.SolidCount);
            int selectedSolids = _allLayerRows.Where(x => x.IsSelected).Sum(x => x.SolidCount);

            lblStats.Text = $"📊 {totalLayers} Layer ({selectedLayers} chọn) | 🧊 {totalSolids} 3D Solid ({selectedSolids} chọn)";
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

        private void DgvPresets_CellContentClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            string colName = dgvPresets.Columns[e.ColumnIndex].Name;

            if (colName == "colPPickBtn" || colName == "colPColor")
            {
                PromptPresetColor(e.RowIndex);
            }
        }

        private void DgvPresets_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            string colName = dgvPresets.Columns[e.ColumnIndex].Name;

            if (colName == "colPColor" || colName == "colPRgb" || colName == "colPHex")
            {
                PromptPresetColor(e.RowIndex);
            }
        }

        private void PromptPresetColor(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= dgvPresets.Rows.Count) return;
            var row = dgvPresets.Rows[rowIndex];
            if (row.Tag is not BimStandardPreset preset) return;

            using var cd = new ColorDialog
            {
                Color = preset.DrawingColor,
                FullOpen = true
            };

            if (cd.ShowDialog(this) == DialogResult.OK)
            {
                preset.R = cd.Color.R;
                preset.G = cd.Color.G;
                preset.B = cd.Color.B;

                row.Cells["colPRgb"].Value = preset.RgbText;
                row.Cells["colPHex"].Value = preset.HexCode;
                dgvPresets.InvalidateCell(row.Cells["colPColor"]);
                AppendLog($"Đã đổi màu mẫu [{preset.Code}] {preset.MaterialName} -> RGB({preset.R},{preset.G},{preset.B}). Bấm '💾 Lưu & Cập Nhật Bảng Mẫu' để lưu lại.");
            }
        }

        private void DgvPresets_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            var row = dgvPresets.Rows[e.RowIndex];
            if (row.Tag is not BimStandardPreset preset) return;

            string colName = dgvPresets.Columns[e.ColumnIndex].Name;
            if (colName == "colPRgb")
            {
                string rgbStr = row.Cells["colPRgb"].Value?.ToString()?.Trim().Trim('(', ')') ?? "";
                var parts = rgbStr.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3 && byte.TryParse(parts[0], out byte r) && byte.TryParse(parts[1], out byte g) && byte.TryParse(parts[2], out byte b))
                {
                    preset.R = r;
                    preset.G = g;
                    preset.B = b;
                    row.Cells["colPHex"].Value = preset.HexCode;
                    dgvPresets.InvalidateCell(row.Cells["colPColor"]);
                }
            }
            else if (colName == "colPHex")
            {
                string hexStr = row.Cells["colPHex"].Value?.ToString()?.Trim().TrimStart('#') ?? "";
                if (hexStr.Length == 6)
                {
                    try
                    {
                        byte r = Convert.ToByte(hexStr.Substring(0, 2), 16);
                        byte g = Convert.ToByte(hexStr.Substring(2, 2), 16);
                        byte b = Convert.ToByte(hexStr.Substring(4, 2), 16);
                        preset.R = r;
                        preset.G = g;
                        preset.B = b;
                        row.Cells["colPRgb"].Value = preset.RgbText;
                        dgvPresets.InvalidateCell(row.Cells["colPColor"]);
                    }
                    catch { }
                }
            }
        }

        #region Tab 2 Toolbar Actions: Add, Color, Delete, Save, Reset, Import/Export Excel
        private void BtnAddPreset_Click(object? sender, EventArgs e)
        {
            var newPreset = new BimStandardPreset
            {
                Code = (_presets.Count + 1).ToString(),
                GroupName = "Hạng mục mới",
                MaterialName = "Vật liệu mới",
                R = 120,
                G = 150,
                B = 200,
                Keywords = new[] { "MOI" }
            };

            _presets.Add(newPreset);
            int rIdx = dgvPresets.Rows.Add();
            var row = dgvPresets.Rows[rIdx];
            row.Tag = newPreset;
            row.Cells["colPCode"].Value = newPreset.Code;
            row.Cells["colPGroup"].Value = newPreset.GroupName;
            row.Cells["colPMaterial"].Value = newPreset.MaterialName;
            row.Cells["colPColor"].Value = "";
            row.Cells["colPRgb"].Value = newPreset.RgbText;
            row.Cells["colPHex"].Value = newPreset.HexCode;
            row.Cells["colPKeywords"].Value = string.Join(", ", newPreset.Keywords);

            dgvPresets.ClearSelection();
            row.Selected = true;
            dgvPresets.FirstDisplayedScrollingRowIndex = rIdx;
            UpdatePresetStats();

            AppendLog("➕ Đã thêm dòng mẫu màu mới. Vui lòng sửa thông tin và bấm '💾 Lưu & Cập Nhật Bảng Mẫu'.");
        }

        private void BtnPickPresetColor_Click(object? sender, EventArgs e)
        {
            if (dgvPresets.SelectedRows.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn 1 dòng mẫu màu trong bảng để chọn màu!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            int rowIndex = dgvPresets.SelectedRows[0].Index;
            PromptPresetColor(rowIndex);
        }

        private void BtnDeletePreset_Click(object? sender, EventArgs e)
        {
            if (dgvPresets.SelectedRows.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn 1 dòng mẫu màu trong bảng để xóa!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var row = dgvPresets.SelectedRows[0];
            if (row.Tag is not BimStandardPreset preset) return;

            var dr = MessageBox.Show($"Bạn có chắc chắn muốn xóa mẫu [{preset.Code}] {preset.MaterialName} khỏi danh sách?", "Xác nhận xóa", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (dr == DialogResult.Yes)
            {
                _presets.Remove(preset);
                dgvPresets.Rows.Remove(row);
                UpdatePresetStats();
                AppendLog($"🗑️ Đã xóa mẫu [{preset.Code}] {preset.MaterialName}. Bấm '💾 Lưu & Cập Nhật Bảng Mẫu' để lưu thay đổi.");
            }
        }

        private void BtnSavePresets_Click(object? sender, EventArgs e)
        {
            try
            {
                SyncGridToPresetsList();
                BimPresetManager.SavePresets(_presets);
                SyncPresetsToMainTab();

                AppendLog($"💾 Đã lưu thành công {_presets.Count} mẫu màu vào cấu hình JSON và đồng bộ sang danh sách Layer!");
                MessageBox.Show($"Đã lưu thành công {_presets.Count} mẫu màu chuẩn BIM!\nToàn bộ danh sách lựa chọn và Layer ở Tab 1 đã được đồng bộ.", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi lưu bảng mẫu màu: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                AppendLog($"❌ Lỗi lưu bảng mẫu: {ex.Message}");
            }
        }

        private void SyncGridToPresetsList()
        {
            var list = new List<BimStandardPreset>();
            foreach (DataGridViewRow row in dgvPresets.Rows)
            {
                if (row.Tag is BimStandardPreset p)
                {
                    p.Code = row.Cells["colPCode"].Value?.ToString()?.Trim() ?? p.Code;
                    p.GroupName = row.Cells["colPGroup"].Value?.ToString()?.Trim() ?? p.GroupName;
                    p.MaterialName = row.Cells["colPMaterial"].Value?.ToString()?.Trim() ?? p.MaterialName;
                    string kwStr = row.Cells["colPKeywords"].Value?.ToString() ?? "";
                    p.Keywords = kwStr.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                                      .Select(k => k.Trim())
                                      .Where(k => !string.IsNullOrEmpty(k))
                                      .ToArray();
                    list.Add(p);
                }
            }
            _presets = list;
        }

        private void BtnResetPresets_Click(object? sender, EventArgs e)
        {
            var dr = MessageBox.Show("Bạn có chắc chắn muốn khôi phục toàn bộ Bảng Mẫu Màu Chuẩn BIM về mặc định ban đầu (BEP T27)?\nCác chỉnh sửa tùy biến sẽ bị thay thế.", "Xác nhận khôi phục mặc định", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (dr == DialogResult.Yes)
            {
                _presets = BimPresetManager.ResetToDefaultPresets();
                PopulatePresetReferenceGrid();
                SyncPresetsToMainTab();
                AppendLog("🔄 Đã khôi phục toàn bộ bảng mẫu màu chuẩn về mặc định ban đầu (BEP T27).");
                MessageBox.Show("Đã khôi phục thành công bảng mẫu màu về mặc định ban đầu!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void BtnImportPresetExcel_Click(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                Title = "Chọn file Excel chứa Bảng Mẫu Màu Chuẩn BIM",
                InitialDirectory = !string.IsNullOrEmpty(_lastExcelPath) ? Path.GetDirectoryName(_lastExcelPath) : ""
            };

            if (ofd.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                var imported = BimPresetManager.ImportPresetsFromExcel(ofd.FileName);
                if (imported.Count > 0)
                {
                    _presets = imported;
                    BimPresetManager.SavePresets(_presets);
                    PopulatePresetReferenceGrid();
                    SyncPresetsToMainTab();

                    AppendLog($"📥 Đã nạp thành công {_presets.Count} mẫu màu từ file Excel: {Path.GetFileName(ofd.FileName)}");
                    MessageBox.Show($"Đã nạp và cập nhật thành công {_presets.Count} mẫu màu từ file Excel!", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi nhập bảng mẫu màu từ Excel: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                AppendLog($"❌ Lỗi nhập Excel bảng mẫu: {ex.Message}");
            }
        }

        private void BtnExportPresetExcel_Click(object? sender, EventArgs e)
        {
            if (_presets.Count == 0)
            {
                MessageBox.Show("Không có dữ liệu mẫu màu để xuất ra Excel!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                Title = "Xuất Bảng Mẫu Màu Chuẩn BIM ra Excel",
                FileName = $"BIM_Color_Presets_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                InitialDirectory = !string.IsNullOrEmpty(_lastExcelPath) ? Path.GetDirectoryName(_lastExcelPath) : ""
            };

            if (sfd.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                SyncGridToPresetsList();
                BimPresetManager.ExportPresetsToExcel(sfd.FileName, _presets);
                AppendLog($"📤 Đã xuất thành công {_presets.Count} mẫu màu ra file Excel: {sfd.FileName}");

                var dr = MessageBox.Show($"Xuất Excel bảng mẫu màu thành công:\n{sfd.FileName}\n\nBạn có muốn mở file ngay bây giờ không?", "Thành công", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
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
                MessageBox.Show($"Lỗi khi xuất bảng mẫu màu ra Excel: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                AppendLog($"❌ Lỗi xuất Excel bảng mẫu: {ex.Message}");
            }
        }
        #endregion
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
            else if (colName == "colDoChat")
            {
                model.DoChat = row.Cells["colDoChat"].Value?.ToString()?.Trim() ?? "";
            }
            else if (colName == "colBeDay")
            {
                string val = row.Cells["colBeDay"].Value?.ToString()?.Trim() ?? "";
                if (double.TryParse(val.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double dVal) && dVal > 0)
                {
                    model.BeDay = dVal;
                    row.Cells["colBeDay"].Value = model.BeDayText;
                }
                else
                {
                    model.BeDay = null;
                    row.Cells["colBeDay"].Value = "";
                }
            }
            else if (colName == "colHangMuc")
            {
                model.HangMuc = row.Cells["colHangMuc"].Value?.ToString()?.Trim() ?? "";
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
            AppendLog($"⚡ Đã tự động nhận diện và gán mẫu màu cho {matchedCount}/{_allLayerRows.Count} Layer theo từ khóa.");
            MessageBox.Show($"Đã tự động nhận diện và gán mẫu màu cho {matchedCount}/{_allLayerRows.Count} Layer!", "Tự động nhận diện", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

        private void BtnApplyBatchBeDay_Click(object? sender, EventArgs e)
        {
            string text = txtBatchBeDay.Text.Trim().Replace(',', '.');
            if (!double.TryParse(text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double thickness) || thickness <= 0)
            {
                MessageBox.Show("Vui lòng nhập giá trị bề dày kết cấu hợp lệ (> 0 m), ví dụ: 0.05 hoặc 0.15!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtBatchBeDay.Focus();
                return;
            }

            var selectedLayers = _allLayerRows.Where(x => x.IsSelected).ToList();
            if (selectedLayers.Count == 0)
            {
                MessageBox.Show("Vui lòng tích chọn ít nhất 1 Layer trong bảng để gán bề dày kết cấu!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            foreach (var model in selectedLayers)
            {
                model.BeDay = thickness;
            }

            foreach (DataGridViewRow row in dgvLayers.Rows)
            {
                if (row.Tag is LayerBimRowModel m && m.IsSelected)
                {
                    row.Cells["colBeDay"].Value = m.BeDayText;
                }
            }

            AppendLog($"📏 Đã gán bề dày kết cấu h = {thickness:0.###} m cho {selectedLayers.Count} Layer được chọn.");
        }

        private void BtnPickObject_Click(object? sender, EventArgs e)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            string selectedLayerName = "";
            using (var interaction = ed.StartUserInteraction(this))
            {
                var pOpt = new PromptEntityOptions("\nChọn đối tượng (3D Solid, Body,...) trên bản vẽ: ");
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

                            // Đọc thử các PropertySet hiện có trên đối tượng được chọn
                            try
                            {
                                var propSetIds = PropertyDataServices.GetPropertySets(ent);
                                if (propSetIds != null)
                                {
                                    foreach (ObjectId psId in propSetIds)
                                    {
                                        var ps = tr.GetObject(psId, OpenMode.ForRead) as PropertySet;
                                        if (ps == null) continue;
                                        var def = tr.GetObject(ps.PropertySetDefinition, OpenMode.ForRead) as PropertySetDefinition;
                                        if (def == null) continue;

                                        foreach (PropertyDefinition pDef in def.Definitions)
                                        {
                                            int pId = ps.PropertyNameToId(pDef.Name);
                                            object? val = ps.GetAt(pId);
                                            string strVal = val?.ToString()?.Trim() ?? "";
                                            if (string.IsNullOrEmpty(strVal)) continue;

                                            string norm = CapNhatMauVaPropertySetCmd.NormalizePropertyName(pDef.Name);
                                            if (norm == "TENCONGTRINH" && string.IsNullOrEmpty(txtTenCongTrinh.Text.Trim()))
                                                txtTenCongTrinh.Text = strVal;
                                            else if (norm == "VITRI" && string.IsNullOrEmpty(txtViTri.Text.Trim()))
                                                txtViTri.Text = strVal;
                                            else if (norm == "NHOMCAUKIEN" && string.IsNullOrEmpty(txtNhomCauKien.Text.Trim()))
                                                txtNhomCauKien.Text = strVal;
                                            else if (norm == "HANGMUC" && string.IsNullOrEmpty(txtHangMuc.Text.Trim()))
                                                txtHangMuc.Text = strVal;
                                        }
                                    }
                                }
                            }
                            catch { }
                        }
                        tr.Commit();
                    }
                }
            }

            if (!string.IsNullOrEmpty(selectedLayerName))
            {
                txtSearch.Text = selectedLayerName;
                AppendLog($"🎯 Đã chọn đối tượng trên Layer '{selectedLayerName}', nạp thông tin dự án từ Property Set.");
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
                Title = "Xuất cấu hình Màu Layer và BIM ra Excel",
                FileName = $"BIM_Layer_Config_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                InitialDirectory = !string.IsNullOrEmpty(_lastExcelPath) ? Path.GetDirectoryName(_lastExcelPath) : ""
            };

            if (sfd.ShowDialog() != DialogResult.OK) return;

            try
            {
                _lastExcelPath = sfd.FileName;

                using var workbook = new XLWorkbook();
                var ws = workbook.Worksheets.Add("BIM_Layer_Config");

                var headerList = new List<string>
                {
                    "STT", "Tên Layer", "Số Solid", "Cấu kiện", "Vật liệu", "Độ chặt", "Bề dày (m)", "Hạng mục", "Mã RGB", "Mã HEX", "Mã Mẫu BIM"
                };

                for (int c = 0; c < headerList.Count; c++)
                {
                    var cell = ws.Cell(1, c + 1);
                    cell.Value = headerList[c];
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
                    int col = 1;
                    ws.Cell(row, col++).SetValue(stt++);
                    ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    ws.Cell(row, col++).SetValue(item.LayerName);
                    ws.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

                    ws.Cell(row, col++).SetValue(item.SolidCount);
                    ws.Cell(row, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    ws.Cell(row, col++).SetValue(item.CauKien ?? "");
                    ws.Cell(row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

                    ws.Cell(row, col++).SetValue(item.VatLieu ?? "");
                    ws.Cell(row, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

                    ws.Cell(row, col++).SetValue(item.DoChat ?? "");
                    ws.Cell(row, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    ws.Cell(row, col++).SetValue(item.BeDay.HasValue ? item.BeDay.Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) : "");
                    ws.Cell(row, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                    ws.Cell(row, col++).SetValue(item.HangMuc ?? "");
                    ws.Cell(row, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

                    string rgbVal = item.NewColor.HasValue ? $"{item.NewR},{item.NewG},{item.NewB}" : "";
                    ws.Cell(row, col).SetValue(rgbVal);
                    ws.Cell(row, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    col++;

                    int hexCol = col;
                    ws.Cell(row, col).SetValue(item.NewHexText);
                    ws.Cell(row, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    col++;

                    ws.Cell(row, col).SetValue(item.SelectedPresetCode ?? "");
                    ws.Cell(row, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    col++;

                    // Tô màu nền ô HEX trực quan
                    if (item.NewColor.HasValue)
                    {
                        try
                        {
                            ws.Cell(row, hexCol).Style.Fill.BackgroundColor = XLColor.FromArgb(item.NewR!.Value, item.NewG!.Value, item.NewB!.Value);
                            double lum = (0.299 * item.NewR.Value + 0.587 * item.NewG.Value + 0.114 * item.NewB.Value);
                            ws.Cell(row, hexCol).Style.Font.FontColor = lum < 140 ? XLColor.White : XLColor.Black;
                            ws.Cell(row, hexCol).Style.Font.Bold = true;
                        }
                        catch { }
                    }

                    row++;
                }

                var range = ws.Range(1, 1, row - 1, headerList.Count);
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
                Title = "Chọn file Excel cấu hình Màu Layer",
                InitialDirectory = !string.IsNullOrEmpty(_lastExcelPath) ? Path.GetDirectoryName(_lastExcelPath) : ""
            };

            if (ofd.ShowDialog(this) != DialogResult.OK) return;

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
                int colDoChatIdx = -1;
                int colBeDayIdx = -1;
                int colHangMucIdx = -1;
                int colRgbIdx = -1;
                int colPresetIdx = -1;

                int maxHeaderScan = Math.Min(firstRow + 4, lastRow);
                for (int r = firstRow; r <= maxHeaderScan; r++)
                {
                    for (int c = firstCol; c <= lastCol; c++)
                    {
                        string headerText = ws.Cell(r, c).GetString().Trim();
                        string val = headerText.ToLower();
                        if (string.IsNullOrEmpty(val)) continue;

                        if (val.Contains("layer") || val == "tên layer") colLayerIdx = c;
                        else if (val.Contains("cấu kiện") || val.Contains("cau kien")) colCauKienIdx = c;
                        else if (val.Contains("vật liệu") || val.Contains("vat lieu")) colVatLieuIdx = c;
                        else if (val.Contains("độ chặt") || val.Contains("do chat")) colDoChatIdx = c;
                        else if (val.Contains("bề dày") || val.Contains("be day") || val.Contains("chiều dày")) colBeDayIdx = c;
                        else if (val.Contains("hạng mục") || val.Contains("hang muc")) colHangMucIdx = c;
                        else if (val.Contains("rgb") || val.Contains("màu")) colRgbIdx = c;
                        else if (val.Contains("mẫu") || val.Contains("preset")) colPresetIdx = c;
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
                    string doChat = colDoChatIdx > 0 ? ws.Cell(r, colDoChatIdx).GetString().Trim() : "";
                    string beDayStr = colBeDayIdx > 0 ? ws.Cell(r, colBeDayIdx).GetString().Trim() : "";
                    string hangMuc = colHangMucIdx > 0 ? ws.Cell(r, colHangMucIdx).GetString().Trim() : "";
                    string rgbStr = colRgbIdx > 0 ? ws.Cell(r, colRgbIdx).GetString().Trim() : "";
                    string presetCode = colPresetIdx > 0 ? ws.Cell(r, colPresetIdx).GetString().Trim() : "";

                    if (layerMap.TryGetValue(layerName, out var targetModel))
                    {
                        if (!string.IsNullOrEmpty(cauKien)) targetModel.CauKien = cauKien;
                        if (!string.IsNullOrEmpty(vatLieu)) targetModel.VatLieu = vatLieu;
                        if (!string.IsNullOrEmpty(doChat)) targetModel.DoChat = doChat;
                        if (!string.IsNullOrEmpty(hangMuc)) targetModel.HangMuc = hangMuc;
                        if (!string.IsNullOrEmpty(beDayStr) && (double.TryParse(beDayStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double bdv) || double.TryParse(beDayStr.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out bdv)))
                        {
                            targetModel.BeDay = bdv;
                        }

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

            bool updateColor = chkUpdateColor.Checked;
            bool updateDescription = chkUpdateDescription.Checked;
            bool applyByLayer = chkApplyByLayer.Checked;
            bool unlockLayers = chkUnlockLayers.Checked;
            bool updatePropertySet = chkUpdatePropertySet.Checked;
            bool onlyMissing = chkOnlyMissing.Checked;

            try
            {
                btnExecute.Enabled = false;
                Cursor.Current = Cursors.WaitCursor;

                SaveCurrentSettings();

                var result = CapNhatMauVaPropertySetCmd.ExecuteUpdateAll(
                    selectedRows,
                    updateColor,
                    updateDescription,
                    applyByLayer,
                    unlockLayers,
                    updatePropertySet,
                    onlyMissing,
                    txtTenCongTrinh.Text.Trim(),
                    txtViTri.Text.Trim(),
                    txtNhomCauKien.Text.Trim(),
                    txtHangMuc.Text.Trim(),
                    msg => AppendLog(msg)
                );

                LoadDataFromDrawing();
                PopulateLayersGrid();

                string psetMsg = updatePropertySet ? $", cập nhật {result.updatedSolids} đối tượng 3D Solid / Body" : "";
                string byLayMsg = applyByLayer ? $", chuyển {result.byLayerCount} đối tượng về ByLayer" : "";
                MessageBox.Show($"Đã hoàn tất cập nhật thành công cho {result.updatedLayers} Layer{psetMsg}{byLayMsg}!", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            try
            {
                SyncGridToPresetsList();
                BimPresetManager.SavePresets(_presets);
            }
            catch { }
            SaveCurrentSettings();
        }

        private void SaveCurrentSettings()
        {
            _savedLayerStates.Clear();
            foreach (var r in _allLayerRows)
            {
                if (!string.IsNullOrEmpty(r.SelectedPresetCode) || !string.IsNullOrEmpty(r.CauKien) || !string.IsNullOrEmpty(r.VatLieu) || r.IsSelected || !string.IsNullOrEmpty(r.DoChat) || r.BeDay.HasValue || !string.IsNullOrEmpty(r.HangMuc))
                {
                    _savedLayerStates[r.LayerName] = new LayerBimSavedState
                    {
                        PresetCode = r.SelectedPresetCode,
                        CauKien = r.CauKien,
                        VatLieu = r.VatLieu,
                        DoChat = r.DoChat,
                        BeDay = r.BeDay,
                        HangMuc = r.HangMuc,
                        R = r.NewR,
                        G = r.NewG,
                        B = r.NewB,
                        IsSelected = r.IsSelected
                    };
                }
            }

            _lastUpdateColor = chkUpdateColor.Checked;
            _lastUpdateDescription = chkUpdateDescription.Checked;
            _lastApplyByLayer = chkApplyByLayer.Checked;
            _lastUnlockLayers = chkUnlockLayers.Checked;
            _lastUpdatePropertySet = chkUpdatePropertySet.Checked;
            _lastOnlyMissing = chkOnlyMissing.Checked;
            _lastTenCongTrinh = txtTenCongTrinh.Text;
            _lastViTri = txtViTri.Text;
            _lastNhomCauKien = txtNhomCauKien.Text;
            _lastHangMuc = txtHangMuc.Text;
            _lastSelectedTab = tabControlMain.SelectedIndex;
            _lastFormSize = this.Size;
        }

        private void RestoreLastSettings()
        {
            chkUpdateColor.Checked = _lastUpdateColor;
            chkUpdateDescription.Checked = _lastUpdateDescription;
            chkApplyByLayer.Checked = _lastApplyByLayer;
            chkUnlockLayers.Checked = _lastUnlockLayers;
            chkUpdatePropertySet.Checked = _lastUpdatePropertySet;
            chkOnlyMissing.Checked = _lastOnlyMissing;
            txtTenCongTrinh.Text = _lastTenCongTrinh;
            txtViTri.Text = _lastViTri;
            txtNhomCauKien.Text = _lastNhomCauKien;
            txtHangMuc.Text = _lastHangMuc;

            if (_lastSelectedTab >= 0 && _lastSelectedTab < tabControlMain.TabCount)
            {
                tabControlMain.SelectedIndex = _lastSelectedTab;
            }
        }
        #endregion
    }
}


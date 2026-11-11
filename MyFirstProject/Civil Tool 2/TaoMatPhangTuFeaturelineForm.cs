using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using CadEntity = Autodesk.AutoCAD.DatabaseServices.Entity;
using CivSurface = Autodesk.Civil.DatabaseServices.TinSurface;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsButton = System.Windows.Forms.Button;
using WinFormsTextBox = System.Windows.Forms.TextBox;
using WinFormsComboBox = System.Windows.Forms.ComboBox;
using FlowDirection = System.Windows.Forms.FlowDirection;
using Font = System.Drawing.Font;
using WinFormsColor = System.Drawing.Color;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace MyFirstProject.Civil_Tool_2
{
    public class SelectedFeatureLineBoundaryItem
    {
        public ObjectId Id { get; set; }
        public string Handle { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public double Length2D { get; set; }
        public bool IsClosed { get; set; }
        public double MinZ { get; set; }
        public double MaxZ { get; set; }
        public string SourceInfo { get; set; } = string.Empty;
        public List<Point3d> SamplePoints { get; set; } = new List<Point3d>();
    }

    public class TaoMatPhangTuFeaturelineForm : Form
    {
        // ═══════════════════════════════════════════════════════════════
        //  PERSISTENT STATE (BẮT BUỘC THEO QUY TẮC RULE 2)
        // ═══════════════════════════════════════════════════════════════
        private static Size _lastFormSize = new Size(1040, 760);
        private static bool _lastCreateNewSurface = true;
        private static string _lastSurfaceName = "";
        private static ObjectId _lastExistingSurfaceId = ObjectId.Null;
        private static string _lastExistingSurfaceName = "";
        private static string _lastMasterSurfaceStyle = "";
        private static int _lastFilterSourceIdx = 0;
        private static string _lastFilterValue = "";
        private static FeatureLineBoundaryMode _lastBoundaryMode = FeatureLineBoundaryMode.AutoSmart;
        private static double _lastMidOrdDistance = 0.10;
        private static bool _lastUseSupplementing = true;
        private static double _lastSupplementingDistance = 2.00;
        private static bool _lastLimitMaxEdge = true;
        private static double _lastMaxEdgeLength = 25.00;
        private static bool _lastAutoClose = true;
        private static bool _lastTreatNestedAsHole = false;
        private static bool _lastRebuildAuto = true;
        private static bool _lastOverwriteOverlap = true;
        private static bool _lastSafeCorridorCombine = true;
        private static List<ObjectId> _lastSelectedIds = new List<ObjectId>();
        private static List<ObjectId> _lastCustomOuterIds = new List<ObjectId>();
        private static List<ObjectId> _lastCustomHideIds = new List<ObjectId>();

        // ═══════════════════════════════════════════════════════════════
        //  FIELDS
        // ═══════════════════════════════════════════════════════════════
        private readonly Database _db;
        private readonly Editor _ed;
        private readonly List<SelectedFeatureLineBoundaryItem> _items = new List<SelectedFeatureLineBoundaryItem>();
        private readonly List<ObjectId> _customOuterIds = new List<ObjectId>();
        private readonly List<ObjectId> _customHideIds = new List<ObjectId>();
        private readonly Dictionary<string, ObjectId> _existingSurfaceMap = new Dictionary<string, ObjectId>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, SurfaceOriginationType> _surfaceOriginationMap = new Dictionary<string, SurfaceOriginationType>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _surfaceTriCountMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly List<Font> _managedFonts = new List<Font>();
        private Font _fontBold9 = null!;
        private Font _fontRegular9 = null!;
        private ToolTip _toolTip = null!;

        // Dữ liệu cache cho bộ lọc nạp nhanh
        private readonly Dictionary<string, List<ObjectId>> _bySite = new Dictionary<string, List<ObjectId>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<ObjectId>> _byLayer = new Dictionary<string, List<ObjectId>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<ObjectId>> _byCorridor = new Dictionary<string, List<ObjectId>>(StringComparer.OrdinalIgnoreCase);
        private readonly List<ObjectId> _allFeatureLineIdsInDrawing = new List<ObjectId>();

        // UI Controls
        private ListView lvObjects = null!;
        private WinFormsLabel lblSummary = null!;
        private WinFormsLabel lblStatus = null!;
        private WinFormsComboBox cboFilterSource = null!;
        private WinFormsComboBox cboFilterValue = null!;
        private WinFormsButton btnLoadByFilter = null!;
        private WinFormsButton btnSelectNew = null!;
        private WinFormsButton btnSelectAdd = null!;
        private WinFormsButton btnRemoveSelected = null!;
        private WinFormsButton btnClearAll = null!;
        private CheckBox chkAutoClose = null!;

        private RadioButton radNewSurface = null!;
        private RadioButton radExistingSurface = null!;
        private WinFormsTextBox txtSurfaceName = null!;
        private WinFormsButton btnGenName = null!;
        private WinFormsComboBox cboExistingSurfaces = null!;
        private WinFormsButton btnPickExistingSurface = null!;
        private Panel pnlExistingOptions = null!;
        private WinFormsLabel lblExistingSurfInfo = null!;
        private CheckBox chkOverwriteOverlap = null!;
        private CheckBox chkSafeCorridorCombine = null!;
        private WinFormsComboBox cboMasterStyles = null!;
        private CheckBox chkRebuildAuto = null!;

        private RadioButton radBoundaryAuto = null!;
        private RadioButton radBoundaryCustom = null!;
        private RadioButton radBoundaryNone = null!;
        private Panel pnlCustomBoundary = null!;
        private WinFormsButton btnPickOuterBnd = null!;
        private WinFormsButton btnPickHideBnd = null!;
        private WinFormsButton btnClearBnd = null!;
        private WinFormsLabel lblCustomBndInfo = null!;
        private CheckBox chkNestedClosedAsHole = null!;

        private NumericUpDown numMidOrd = null!;
        private CheckBox chkUseSupplementing = null!;
        private NumericUpDown numSupplementing = null!;
        private CheckBox chkLimitMaxEdge = null!;
        private NumericUpDown numMaxEdgeLength = null!;
        private WinFormsButton btnSuggestMaxEdge = null!;

        private ProgressBar progressBar = null!;
        private WinFormsButton btnExecute = null!;
        private WinFormsButton btnClose = null!;

        public TaoMatPhangTuFeaturelineForm(Database db, Editor ed)
        {
            _db = db;
            _ed = ed;

            InitializeCustomUI();
            ScanDrawingSourcesAndStyles();
            RestoreLastSettings();
            TryRestorePreviousSelection();
            UpdateControlStates();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _toolTip?.Dispose();
                foreach (Font f in _managedFonts)
                {
                    f?.Dispose();
                }
                _managedFonts.Clear();
            }
            base.Dispose(disposing);
        }

        private Font CreateFont(float size, FontStyle style = FontStyle.Regular)
        {
            var f = new Font("Segoe UI", size, style, GraphicsUnit.Point);
            _managedFonts.Add(f);
            return f;
        }

        private void InitializeCustomUI()
        {
            _toolTip = new ToolTip { AutoPopDelay = 10000, InitialDelay = 350, ReshowDelay = 200 };
            _fontBold9 = CreateFont(9F, FontStyle.Bold);
            _fontRegular9 = CreateFont(9F, FontStyle.Regular);

            this.Text = "Tạo & Bổ Sung Mặt Phẳng TIN Từ Feature Line (0 Mặt Phẳng Phụ)";
            this.Size = _lastFormSize;
            this.MinimumSize = new Size(960, 680);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = _fontRegular9;
            this.BackColor = WinFormsColor.FromArgb(245, 247, 250);
            this.KeyPreview = true;

            // ═══════════════════════════════════════════════════════════════
            //  1. HEADER BANNER
            // ═══════════════════════════════════════════════════════════════
            Panel pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 68,
                BackColor = WinFormsColor.FromArgb(20, 72, 118),
                Padding = new Padding(16, 10, 16, 8)
            };

            WinFormsLabel lblHeaderTitle = new WinFormsLabel
            {
                Text = "TẠO MỚI & BỔ SUNG MẶT PHẲNG TIN TỪ FEATURE LINE (HỞ & KÍN)",
                Font = CreateFont(11.5F, FontStyle.Bold),
                ForeColor = WinFormsColor.White,
                Dock = DockStyle.Top,
                Height = 26,
                UseMnemonic = false
            };

            WinFormsLabel lblHeaderSub = new WinFormsLabel
            {
                Text = "Tạo mới hoặc ghép bổ sung vào Surface có sẵn — Tự động khóa biên cũ, xử lý chồng lấn & cắt tam giác bắc cầu (0 mặt phẳng phụ)",
                Font = CreateFont(8.5F, FontStyle.Regular),
                ForeColor = WinFormsColor.FromArgb(212, 232, 252),
                Dock = DockStyle.Fill,
                UseMnemonic = false
            };

            pnlHeader.Controls.Add(lblHeaderSub);
            pnlHeader.Controls.Add(lblHeaderTitle);

            // ═══════════════════════════════════════════════════════════════
            //  2. BOTTOM FOOTER (EXECUTION BAR)
            // ═══════════════════════════════════════════════════════════════
            Panel pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 92,
                BackColor = WinFormsColor.White,
                Padding = new Padding(16, 8, 16, 10)
            };

            progressBar = new ProgressBar
            {
                Dock = DockStyle.Top,
                Height = 8,
                Visible = false
            };

            lblStatus = new WinFormsLabel
            {
                Text = "Sẵn sàng. Hãy chọn các đường Feature Line / Corridor FL / Polyline (hở hoặc kín) trên bản vẽ...",
                Font = CreateFont(8.5F, FontStyle.Italic),
                ForeColor = WinFormsColor.FromArgb(85, 98, 115),
                Dock = DockStyle.Top,
                Height = 22,
                UseMnemonic = false
            };

            FlowLayoutPanel flpBottomButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 46,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 2, 0, 0)
            };

            btnClose = new WinFormsButton
            {
                Text = "Đóng",
                Width = 110,
                Height = 42,
                BackColor = WinFormsColor.FromArgb(232, 236, 241),
                ForeColor = WinFormsColor.FromArgb(45, 55, 70),
                FlatStyle = FlatStyle.Flat,
                Font = CreateFont(9.5F, FontStyle.Regular),
                UseMnemonic = false,
                Margin = new Padding(8, 0, 0, 0)
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => this.Close();

            btnExecute = new WinFormsButton
            {
                Text = "⚡ Thực Hiện Tạo Mặt Phẳng",
                Width = 270,
                Height = 42,
                BackColor = WinFormsColor.FromArgb(13, 110, 253),
                ForeColor = WinFormsColor.White,
                FlatStyle = FlatStyle.Flat,
                Font = CreateFont(9.5F, FontStyle.Bold),
                UseMnemonic = false,
                Margin = new Padding(0)
            };
            btnExecute.FlatAppearance.BorderSize = 0;
            btnExecute.Click += BtnExecute_Click;

            flpBottomButtons.Controls.Add(btnClose);
            flpBottomButtons.Controls.Add(btnExecute);

            pnlBottom.Controls.Add(flpBottomButtons);
            pnlBottom.Controls.Add(lblStatus);
            pnlBottom.Controls.Add(progressBar);

            // ═══════════════════════════════════════════════════════════════
            //  3. MAIN TWO-COLUMN BODY
            // ═══════════════════════════════════════════════════════════════
            TableLayoutPanel tlpMain = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(12, 10, 12, 8),
                BackColor = WinFormsColor.Transparent
            };
            tlpMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(SizeType.Percent, 53F));
            tlpMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(SizeType.Percent, 47F));

            // ───────────────────────────────────────────────────────────────
            //  CỘT TRÁI: ĐỐI TƯỢNG NGUỒN & BỘ LỌC NẠP NHANH
            // ───────────────────────────────────────────────────────────────
            GroupBox grpSource = new GroupBox
            {
                Text = "1. Đối tượng nguồn (Feature Line / Corridor FL / 3D && 2D Polyline)",
                Font = CreateFont(9.5F, FontStyle.Bold),
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            TableLayoutPanel tlpLeft = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5
            };
            tlpLeft.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Hàng 1: Nút chọn trên CAD
            tlpLeft.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Hàng 2: Lọc nhanh theo Dropdown
            tlpLeft.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // Hàng 3: ListView
            tlpLeft.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Hàng 4: Tổng kết
            tlpLeft.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Hàng 5: AutoClose

            // Hàng 1: Thanh nút chọn trên CAD (Height = 40px chuẩn chống cắt chữ High-DPI)
            FlowLayoutPanel flpPickBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = true,
                Margin = new Padding(0, 0, 0, 6)
            };

            btnSelectNew = CreateActionButton("🎯 Chọn trên CAD", 148, 40, WinFormsColor.FromArgb(20, 72, 118), WinFormsColor.White, true);
            btnSelectNew.Click += (s, e) => PickSourceEntitiesOnCad(append: false);
            _toolTip.SetToolTip(btnSelectNew, "Quét chọn mới các đường Feature Line, Auto Corridor FL, Polyline trên bản vẽ");

            btnSelectAdd = CreateActionButton("➕ Chọn thêm", 118, 40, WinFormsColor.FromArgb(25, 135, 84), WinFormsColor.White, true);
            btnSelectAdd.Click += (s, e) => PickSourceEntitiesOnCad(append: true);
            _toolTip.SetToolTip(btnSelectAdd, "Chọn bổ sung thêm đối tượng trên CAD vào danh sách hiện tại");

            btnRemoveSelected = CreateActionButton("🗑️ Xóa dòng", 105, 40, WinFormsColor.FromArgb(232, 236, 241), WinFormsColor.FromArgb(55, 65, 80), false);
            btnRemoveSelected.Click += (s, e) => RemoveSelectedItemsInList();
            _toolTip.SetToolTip(btnRemoveSelected, "Loại bỏ các dòng đang bôi đen trong bảng (hoặc nhấn phím Delete)");

            btnClearAll = CreateActionButton("🧹 Xóa hết", 96, 40, WinFormsColor.FromArgb(248, 225, 228), WinFormsColor.FromArgb(155, 30, 45), false);
            btnClearAll.Click += (s, e) => ClearAllSourceItems();

            flpPickBar.Controls.Add(btnSelectNew);
            flpPickBar.Controls.Add(btnSelectAdd);
            flpPickBar.Controls.Add(btnRemoveSelected);
            flpPickBar.Controls.Add(btnClearAll);

            // Hàng 2: Lọc & nạp nhanh theo Dropdown (Site / Layer / Corridor)
            TableLayoutPanel tlpQuickFilter = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 3,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 6)
            };
            tlpQuickFilter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(SizeType.Percent, 42F));
            tlpQuickFilter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(SizeType.Percent, 58F));
            tlpQuickFilter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(SizeType.AutoSize));

            cboFilterSource = new WinFormsComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = CreateFont(9F),
                Margin = new Padding(0, 6, 6, 0)
            };
            cboFilterSource.Items.AddRange(new object[]
            {
                "Lọc: Tất cả FL bản vẽ",
                "Lọc theo Site",
                "Lọc theo Layer",
                "Lọc theo Corridor"
            });
            cboFilterSource.SelectedIndexChanged += (s, e) => PopulateFilterValuesDropdown();

            cboFilterValue = new WinFormsComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = CreateFont(9F),
                Margin = new Padding(0, 6, 6, 0)
            };

            btnLoadByFilter = CreateActionButton("📥 Nạp lọc", 104, 40, WinFormsColor.FromArgb(224, 236, 252), WinFormsColor.FromArgb(15, 68, 130), true);
            btnLoadByFilter.Margin = new Padding(0);
            btnLoadByFilter.Click += BtnLoadByFilter_Click;
            _toolTip.SetToolTip(btnLoadByFilter, "Thêm nhanh tất cả đối tượng khớp với bộ lọc vào bảng danh sách");

            tlpQuickFilter.Controls.Add(cboFilterSource, 0, 0);
            tlpQuickFilter.Controls.Add(cboFilterValue, 1, 0);
            tlpQuickFilter.Controls.Add(btnLoadByFilter, 2, 0);

            // Hàng 3: Bảng ListView hiển thị chi tiết
            lvObjects = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                Font = CreateFont(8.75F),
                MultiSelect = true,
                HideSelection = false
            };
            lvObjects.Columns.Add("STT", 42);
            lvObjects.Columns.Add("Tên / Mã Code", 130);
            lvObjects.Columns.Add("Loại", 92);
            lvObjects.Columns.Add("Dạng", 62);
            lvObjects.Columns.Add("Dài 2D (m)", 78);
            lvObjects.Columns.Add("Min Z", 66);
            lvObjects.Columns.Add("Max Z", 66);
            lvObjects.Columns.Add("Layer / Nguồn", 125);
            lvObjects.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Delete)
                {
                    RemoveSelectedItemsInList();
                    e.Handled = true;
                }
            };

            // Hàng 4: Tổng kết
            lblSummary = new WinFormsLabel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                Padding = new Padding(0, 6, 0, 2),
                Font = CreateFont(8.75F, FontStyle.Regular),
                ForeColor = WinFormsColor.FromArgb(40, 60, 85),
                Text = "Đã chọn: 0 đối tượng (0 đường hở, 0 đường kín) | Tổng chiều dài: 0.00 m",
                UseMnemonic = false
            };

            // Hàng 5: Checkbox AutoClose
            chkAutoClose = new CheckBox
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                Padding = new Padding(0, 2, 0, 0),
                Text = "Tự động coi đường gần kín (khoảng hở đầu-cuối ≤ 0.10 m) là đường khép kín",
                Font = CreateFont(8.75F),
                Checked = _lastAutoClose,
                UseMnemonic = false
            };
            chkAutoClose.CheckedChanged += (s, e) => RefreshItemsClosedState();

            tlpLeft.Controls.Add(flpPickBar, 0, 0);
            tlpLeft.Controls.Add(tlpQuickFilter, 0, 1);
            tlpLeft.Controls.Add(lvObjects, 0, 2);
            tlpLeft.Controls.Add(lblSummary, 0, 3);
            tlpLeft.Controls.Add(chkAutoClose, 0, 4);

            grpSource.Controls.Add(tlpLeft);
            tlpMain.Controls.Add(grpSource, 0, 0);

            // ───────────────────────────────────────────────────────────────
            //  CỘT PHẢI: CẤU HÌNH MẶT PHẲNG ĐÍCH, BIÊN BAO & LƯỚI TAM GIÁC
            // ───────────────────────────────────────────────────────────────
            Panel pnlRightScroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(4, 0, 4, 0)
            };

            TableLayoutPanel tlpRightStack = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 3
            };
            tlpRightStack.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(SizeType.Percent, 100F));
            tlpRightStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tlpRightStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tlpRightStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // ═══ GROUP 2: MẶT PHẲNG TIN ĐÍCH ═══
            GroupBox grpTarget = new GroupBox
            {
                Text = "2. Mặt phẳng TIN đích (Chỉ 1 Surface - Không tạo mặt phẳng phụ)",
                Font = CreateFont(9.5F, FontStyle.Bold),
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(12, 10, 12, 10),
                Margin = new Padding(0, 0, 0, 8)
            };

            TableLayoutPanel tlpTarget = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 7
            };
            tlpTarget.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(SizeType.Percent, 100F));
            tlpTarget.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(SizeType.AutoSize));

            radNewSurface = new RadioButton
            {
                Text = "🆕 Tạo mặt phẳng TIN mới:",
                Font = _fontBold9,
                AutoSize = true,
                Checked = true,
                UseMnemonic = false,
                Margin = new Padding(0, 2, 0, 4)
            };
            radNewSurface.CheckedChanged += (s, e) => UpdateControlStates();

            txtSurfaceName = new WinFormsTextBox
            {
                Dock = DockStyle.Fill,
                Font = CreateFont(9.25F),
                Margin = new Padding(0, 4, 8, 6)
            };

            btnGenName = CreateActionButton("🔄 Tạo tên", 98, 40, WinFormsColor.FromArgb(232, 236, 241), WinFormsColor.FromArgb(45, 55, 70), false);
            btnGenName.Margin = new Padding(0, 0, 0, 4);
            btnGenName.Click += (s, e) => GenerateDefaultSurfaceName();

            radExistingSurface = new RadioButton
            {
                Text = "➕ Bổ sung mặt phẳng từ Feature Line vào Mặt phẳng có sẵn:",
                Font = _fontRegular9,
                AutoSize = true,
                UseMnemonic = false,
                Margin = new Padding(0, 4, 0, 4)
            };
            radExistingSurface.CheckedChanged += (s, e) => UpdateControlStates();

            cboExistingSurfaces = new WinFormsComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = _fontRegular9,
                Margin = new Padding(0, 6, 8, 4)
            };
            cboExistingSurfaces.SelectedIndexChanged += (s, e) => UpdateExistingSurfaceMetadataUI();

            btnPickExistingSurface = CreateActionButton("🎯 Chọn", 98, 40, WinFormsColor.FromArgb(224, 236, 252), WinFormsColor.FromArgb(15, 68, 130), false);
            btnPickExistingSurface.Margin = new Padding(0, 0, 0, 2);
            btnPickExistingSurface.Click += BtnPickExistingSurface_Click;
            _toolTip.SetToolTip(btnPickExistingSurface, "Chọn trực tiếp một TIN Surface có sẵn trên màn hình CAD để bổ sung thêm vùng mặt phẳng mới");

            // Khung tùy chọn chuyên sâu khi Bổ sung vào mặt phẳng có sẵn
            pnlExistingOptions = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                BackColor = WinFormsColor.FromArgb(236, 245, 240),
                Padding = new Padding(10, 6, 8, 6),
                Margin = new Padding(0, 2, 0, 6)
            };

            TableLayoutPanel tlpExistingOpts = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 3
            };
            tlpExistingOpts.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(SizeType.Percent, 100F));

            lblExistingSurfInfo = new WinFormsLabel
            {
                Text = "📊 Mặt phẳng đích: (Chưa chọn)",
                Font = CreateFont(8.25F, FontStyle.Italic),
                ForeColor = WinFormsColor.FromArgb(24, 95, 60),
                AutoSize = true,
                MaximumSize = new Size(395, 0),
                UseMnemonic = false,
                Margin = new Padding(0, 0, 0, 4)
            };

            chkOverwriteOverlap = new CheckBox
            {
                Text = "Đắp đè cao độ mới tại vùng chồng lấn (thay điểm cũ dưới FL)",
                Font = CreateFont(8.5F, FontStyle.Regular),
                AutoSize = true,
                Checked = _lastOverwriteOverlap,
                UseMnemonic = false,
                Margin = new Padding(0, 0, 0, 2)
            };
            _toolTip.SetToolTip(
                chkOverwriteOverlap,
                "• Tích chọn (Khuyên dùng): Tại vùng giao chồng, cao độ của mảng Feature Line mới sẽ thay thế điểm/lưới cũ (tương tự Paste Surface nhưng 0 mặt phẳng phụ).\n" +
                "• Bỏ tích: Giữ nguyên 100% cao độ mặt phẳng cũ tại nơi giao chồng, chỉ nối mở rộng thêm phần diện tích mới nằm ngoài mặt phẳng cũ.");

            chkSafeCorridorCombine = new CheckBox
            {
                Text = "🛡️ Mặt phẳng Corridor -> Gộp vào '<Tên>_Gop' (an toàn khi Rebuild)",
                Font = CreateFont(8.5F, FontStyle.Bold),
                ForeColor = WinFormsColor.FromArgb(145, 72, 0),
                AutoSize = true,
                Checked = _lastSafeCorridorCombine,
                Visible = false,
                UseMnemonic = false,
                Margin = new Padding(0, 2, 0, 0)
            };
            _toolTip.SetToolTip(
                chkSafeCorridorCombine,
                "Mặt phẳng đang chọn sinh trực tiếp từ Corridor/Grading (bị giới hạn bởi Corridor Extents và bị ghi đè khi Rebuild Corridor).\n" +
                "Tích chọn để tự động tạo/cập nhật mặt phẳng tổng hợp '<Tên>_Gop' kế thừa động mặt phẳng gốc + ghép thêm Feature Line (0 mặt phẳng phụ thừa).");

            tlpExistingOpts.Controls.Add(lblExistingSurfInfo, 0, 0);
            tlpExistingOpts.Controls.Add(chkOverwriteOverlap, 0, 1);
            tlpExistingOpts.Controls.Add(chkSafeCorridorCombine, 0, 2);
            pnlExistingOptions.Controls.Add(tlpExistingOpts);

            WinFormsLabel lblStyle = new WinFormsLabel
            {
                Text = "Surface Style hiển thị (khi tạo mới):",
                Font = _fontRegular9,
                AutoSize = true,
                UseMnemonic = false,
                Margin = new Padding(0, 4, 0, 4)
            };

            cboMasterStyles = new WinFormsComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = _fontRegular9,
                Margin = new Padding(0, 0, 0, 6)
            };

            chkRebuildAuto = new CheckBox
            {
                Text = "Tự động cập nhật khi đối tượng nguồn thay đổi (Auto-Rebuild)",
                Font = CreateFont(8.75F),
                AutoSize = true,
                Checked = _lastRebuildAuto,
                UseMnemonic = false,
                Margin = new Padding(0, 2, 0, 0)
            };

            tlpTarget.Controls.Add(radNewSurface, 0, 0);
            tlpTarget.SetColumnSpan(radNewSurface, 2);
            tlpTarget.Controls.Add(txtSurfaceName, 0, 1);
            tlpTarget.Controls.Add(btnGenName, 1, 1);

            tlpTarget.Controls.Add(radExistingSurface, 0, 2);
            tlpTarget.SetColumnSpan(radExistingSurface, 2);
            tlpTarget.Controls.Add(cboExistingSurfaces, 0, 3);
            tlpTarget.Controls.Add(btnPickExistingSurface, 1, 3);
            tlpTarget.Controls.Add(pnlExistingOptions, 0, 4);
            tlpTarget.SetColumnSpan(pnlExistingOptions, 2);

            tlpTarget.Controls.Add(lblStyle, 0, 5);
            tlpTarget.SetColumnSpan(lblStyle, 2);
            tlpTarget.Controls.Add(cboMasterStyles, 0, 6);
            tlpTarget.SetColumnSpan(cboMasterStyles, 2);

            TableLayoutPanel tlpTargetOuter = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 2
            };
            tlpTargetOuter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(SizeType.Percent, 100F));
            tlpTargetOuter.Controls.Add(tlpTarget, 0, 0);
            tlpTargetOuter.Controls.Add(chkRebuildAuto, 0, 1);
            grpTarget.Controls.Add(tlpTargetOuter);

            // ═══ GROUP 3: KIỂM SOÁT BIÊN BAO (CHỐNG PHÓNG TAM GIÁC THỪA) ═══
            GroupBox grpBoundary = new GroupBox
            {
                Text = "3. Kiểm soát Biên bao && Chống tam giác thừa",
                Font = CreateFont(9.5F, FontStyle.Bold),
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(12, 10, 12, 10),
                Margin = new Padding(0, 0, 0, 8)
            };

            TableLayoutPanel tlpBoundary = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 6
            };
            tlpBoundary.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(SizeType.Percent, 100F));

            radBoundaryAuto = new RadioButton
            {
                Text = "Tự động thông minh (Khuyên dùng cho cả dải đường Hở & đường Kín)",
                Font = CreateFont(9F, FontStyle.Bold),
                AutoSize = true,
                Checked = true,
                UseMnemonic = false,
                Margin = new Padding(0, 2, 0, 2)
            };
            radBoundaryAuto.CheckedChanged += (s, e) => UpdateControlStates();

            WinFormsLabel lblAutoHint = new WinFormsLabel
            {
                Text = "💡 Tự nhận diện đường khép kín & dải đường hở (taluy, lề, rãnh, nút giao), cắt 100% tam giác dây cung qua bụng đường cong và khoảng trống mà không sinh mặt phẳng phụ.",
                Font = CreateFont(8.25F, FontStyle.Italic),
                ForeColor = WinFormsColor.FromArgb(30, 105, 55),
                Dock = DockStyle.Top,
                AutoSize = true,
                MaximumSize = new Size(410, 0),
                Padding = new Padding(20, 0, 0, 6),
                UseMnemonic = false
            };

            chkNestedClosedAsHole = new CheckBox
            {
                Text = "Coi đường khép kín nằm lọt trong đường kín khác là Lỗ khoét (Hide)",
                Font = CreateFont(8.75F, FontStyle.Regular),
                AutoSize = true,
                Checked = _lastTreatNestedAsHole,
                UseMnemonic = false,
                Margin = new Padding(22, 0, 0, 6)
            };
            _toolTip.SetToolTip(chkNestedClosedAsHole, "Bỏ tích: đường kín bên trong đóng vai trò đường đồng mức/mặt bệ. Tích chọn: khoét rỗng đảo bên trong.");

            radBoundaryCustom = new RadioButton
            {
                Text = "Giới hạn theo đường Biên bao chọn riêng trên CAD (ví dụ layer 2.BAO):",
                Font = CreateFont(9F, FontStyle.Regular),
                AutoSize = true,
                UseMnemonic = false,
                Margin = new Padding(0, 4, 0, 4)
            };
            radBoundaryCustom.CheckedChanged += (s, e) => UpdateControlStates();

            pnlCustomBoundary = new Panel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(18, 2, 0, 6)
            };

            TableLayoutPanel tlpCustomBnd = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 2
            };
            tlpCustomBnd.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(SizeType.Percent, 100F));

            FlowLayoutPanel flpCustomBtns = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                WrapContents = true
            };

            btnPickOuterBnd = CreateActionButton("📐 Chọn Biên ngoài", 142, 40, WinFormsColor.FromArgb(20, 72, 118), WinFormsColor.White, false);
            btnPickOuterBnd.Click += (s, e) => PickCustomBoundaryEntities(isOuter: true);

            btnPickHideBnd = CreateActionButton("🕳️ Chọn Lỗ khoét", 132, 40, WinFormsColor.FromArgb(108, 117, 125), WinFormsColor.White, false);
            btnPickHideBnd.Click += (s, e) => PickCustomBoundaryEntities(isOuter: false);

            btnClearBnd = CreateActionButton("Xóa biên", 85, 40, WinFormsColor.FromArgb(232, 236, 241), WinFormsColor.FromArgb(55, 65, 80), false);
            btnClearBnd.Click += (s, e) =>
            {
                _customOuterIds.Clear();
                _customHideIds.Clear();
                UpdateCustomBoundaryLabel();
            };

            flpCustomBtns.Controls.Add(btnPickOuterBnd);
            flpCustomBtns.Controls.Add(btnPickHideBnd);
            flpCustomBtns.Controls.Add(btnClearBnd);

            lblCustomBndInfo = new WinFormsLabel
            {
                Text = "Biên riêng đã chọn: 0 Outer | 0 Hide",
                Font = CreateFont(8.5F),
                ForeColor = WinFormsColor.FromArgb(70, 80, 95),
                AutoSize = true,
                Padding = new Padding(2, 4, 0, 0),
                UseMnemonic = false
            };

            tlpCustomBnd.Controls.Add(flpCustomBtns, 0, 0);
            tlpCustomBnd.Controls.Add(lblCustomBndInfo, 0, 1);
            pnlCustomBoundary.Controls.Add(tlpCustomBnd);

            radBoundaryNone = new RadioButton
            {
                Text = "Chỉ thêm Breaklines thuần túy (Không cắt biên tam giác)",
                Font = CreateFont(9F, FontStyle.Regular),
                AutoSize = true,
                UseMnemonic = false,
                Margin = new Padding(0, 2, 0, 0)
            };
            radBoundaryNone.CheckedChanged += (s, e) => UpdateControlStates();

            tlpBoundary.Controls.Add(radBoundaryAuto, 0, 0);
            tlpBoundary.Controls.Add(lblAutoHint, 0, 1);
            tlpBoundary.Controls.Add(chkNestedClosedAsHole, 0, 2);
            tlpBoundary.Controls.Add(radBoundaryCustom, 0, 3);
            tlpBoundary.Controls.Add(pnlCustomBoundary, 0, 4);
            tlpBoundary.Controls.Add(radBoundaryNone, 0, 5);
            grpBoundary.Controls.Add(tlpBoundary);

            // ═══ GROUP 4: THÔNG SỐ CHIA LƯỚI TAM GIÁC & BỀ RỘNG DẢI ═══
            GroupBox grpMeshParams = new GroupBox
            {
                Text = "4. Thông số độ mịn Breakline && Giới hạn cạnh bắc cầu",
                Font = CreateFont(9.5F, FontStyle.Bold),
                Dock = DockStyle.Top,
                AutoSize = true,
                Padding = new Padding(12, 10, 12, 10),
                Margin = new Padding(0, 0, 0, 4)
            };

            TableLayoutPanel tlpMesh = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 3,
                RowCount = 3
            };
            tlpMesh.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(SizeType.Percent, 100F));
            tlpMesh.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(SizeType.Absolute, 105F));
            tlpMesh.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(SizeType.AutoSize));

            // Dòng 1: Mid-ordinate
            WinFormsLabel lblMidOrd = new WinFormsLabel
            {
                Text = "Độ mịn cung cong (Mid-ordinate, m):",
                Font = CreateFont(8.75F, FontStyle.Regular),
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                UseMnemonic = false,
                Margin = new Padding(0, 6, 6, 6)
            };

            numMidOrd = new NumericUpDown
            {
                Dock = DockStyle.Fill,
                DecimalPlaces = 2,
                Minimum = 0.01M,
                Maximum = 10.00M,
                Increment = 0.05M,
                Value = 0.10M,
                Font = CreateFont(9F),
                Margin = new Padding(0, 4, 6, 6)
            };

            // Dòng 2: Supplementing Distance
            chkUseSupplementing = new CheckBox
            {
                Text = "Chèn thêm đỉnh đoạn thẳng (Supplementing, m):",
                Font = CreateFont(8.75F, FontStyle.Regular),
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Checked = true,
                UseMnemonic = false,
                Margin = new Padding(0, 6, 6, 6)
            };
            chkUseSupplementing.CheckedChanged += (s, e) => numSupplementing.Enabled = chkUseSupplementing.Checked;
            _toolTip.SetToolTip(chkUseSupplementing, "Chia mịn các đoạn Feature Line thẳng dài để lưới tam giác bám đều theo mặt cắt ngang dải đường/taluy");

            numSupplementing = new NumericUpDown
            {
                Dock = DockStyle.Fill,
                DecimalPlaces = 2,
                Minimum = 0.20M,
                Maximum = 500.00M,
                Increment = 0.50M,
                Value = 2.00M,
                Font = CreateFont(9F),
                Margin = new Padding(0, 4, 6, 6)
            };

            // Dòng 3: Max Edge Length + Nút Gợi ý
            chkLimitMaxEdge = new CheckBox
            {
                Text = "Cắt cạnh tam giác bắc cầu dài quá (m):",
                Font = CreateFont(8.75F, FontStyle.Regular),
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Checked = true,
                UseMnemonic = false,
                Margin = new Padding(0, 8, 6, 4)
            };
            chkLimitMaxEdge.CheckedChanged += (s, e) =>
            {
                numMaxEdgeLength.Enabled = chkLimitMaxEdge.Checked;
                btnSuggestMaxEdge.Enabled = chkLimitMaxEdge.Checked;
            };
            _toolTip.SetToolTip(chkLimitMaxEdge, "Ngăn các dải Feature Line rời nhau (ví dụ Taluy trái & Taluy phải cách nhau qua lòng đường) nối tam giác sang nhau");

            numMaxEdgeLength = new NumericUpDown
            {
                Dock = DockStyle.Fill,
                DecimalPlaces = 2,
                Minimum = 0.50M,
                Maximum = 5000.00M,
                Increment = 1.00M,
                Value = 25.00M,
                Font = CreateFont(9F),
                Margin = new Padding(0, 8, 6, 4)
            };

            btnSuggestMaxEdge = CreateActionButton("⚡ Gợi ý", 84, 40, WinFormsColor.FromArgb(255, 243, 205), WinFormsColor.FromArgb(120, 80, 0), true);
            btnSuggestMaxEdge.Margin = new Padding(0, 2, 0, 2);
            btnSuggestMaxEdge.Click += BtnSuggestMaxEdge_Click;
            _toolTip.SetToolTip(btnSuggestMaxEdge, "Tự động đo khoảng cách ngang giữa các đường Feature Line đang chọn để gợi ý chiều dài cạnh tối đa hợp lý");

            tlpMesh.Controls.Add(lblMidOrd, 0, 0);
            tlpMesh.Controls.Add(numMidOrd, 1, 0);

            tlpMesh.Controls.Add(chkUseSupplementing, 0, 1);
            tlpMesh.Controls.Add(numSupplementing, 1, 1);

            tlpMesh.Controls.Add(chkLimitMaxEdge, 0, 2);
            tlpMesh.Controls.Add(numMaxEdgeLength, 1, 2);
            tlpMesh.Controls.Add(btnSuggestMaxEdge, 2, 2);

            grpMeshParams.Controls.Add(tlpMesh);

            tlpRightStack.Controls.Add(grpTarget, 0, 0);
            tlpRightStack.Controls.Add(grpBoundary, 0, 1);
            tlpRightStack.Controls.Add(grpMeshParams, 0, 2);

            pnlRightScroll.Controls.Add(tlpRightStack);
            tlpMain.Controls.Add(pnlRightScroll, 1, 0);

            this.Controls.Add(tlpMain);
            this.Controls.Add(pnlBottom);
            this.Controls.Add(pnlHeader);

            this.AcceptButton = btnExecute;
            this.CancelButton = btnClose;

            this.FormClosing += (s, e) => SaveCurrentSettings();
            this.ResizeEnd += (s, e) => _lastFormSize = this.Size;
        }

        private WinFormsButton CreateActionButton(string text, int width, int height, WinFormsColor backColor, WinFormsColor foreColor, bool bold)
        {
            var btn = new WinFormsButton
            {
                Text = text,
                Width = width,
                Height = height,
                BackColor = backColor,
                ForeColor = foreColor,
                FlatStyle = FlatStyle.Flat,
                Font = CreateFont(8.75F, bold ? FontStyle.Bold : FontStyle.Regular),
                UseMnemonic = false,
                Margin = new Padding(0, 0, 6, 4)
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private void UpdateControlStates()
        {
            bool createNew = radNewSurface.Checked;
            radNewSurface.Font = createNew ? _fontBold9 : _fontRegular9;
            radExistingSurface.Font = !createNew ? _fontBold9 : _fontRegular9;

            txtSurfaceName.Enabled = createNew;
            btnGenName.Enabled = createNew;
            cboMasterStyles.Enabled = createNew;

            cboExistingSurfaces.Enabled = !createNew;
            btnPickExistingSurface.Enabled = !createNew;
            pnlExistingOptions.Enabled = !createNew;

            if (createNew)
            {
                btnExecute.Text = "⚡ Thực Hiện Tạo Mặt Phẳng";
                btnExecute.BackColor = WinFormsColor.FromArgb(13, 110, 253);
            }
            else
            {
                btnExecute.Text = "➕ Bổ Sung Vào Mặt Phẳng Có Sẵn";
                btnExecute.BackColor = WinFormsColor.FromArgb(25, 135, 84);
            }

            UpdateExistingSurfaceMetadataUI();

            chkNestedClosedAsHole.Enabled = radBoundaryAuto.Checked;
            pnlCustomBoundary.Enabled = radBoundaryCustom.Checked;

            numSupplementing.Enabled = chkUseSupplementing.Checked;
            numMaxEdgeLength.Enabled = chkLimitMaxEdge.Checked;
            btnSuggestMaxEdge.Enabled = chkLimitMaxEdge.Checked;
        }

        private void UpdateExistingSurfaceMetadataUI()
        {
            string selectedSurf = cboExistingSurfaces.SelectedItem?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(selectedSurf) || !_existingSurfaceMap.ContainsKey(selectedSurf))
            {
                lblExistingSurfInfo.Text = "📊 Mặt phẳng đích: (Chưa có mặt phẳng TIN trong bản vẽ)";
                chkSafeCorridorCombine.Visible = false;
                return;
            }

            int triCount = _surfaceTriCountMap.TryGetValue(selectedSurf, out int tc) ? tc : 0;
            SurfaceOriginationType origType = _surfaceOriginationMap.TryGetValue(selectedSurf, out var ot)
                ? ot
                : SurfaceOriginationType.None;

            bool isSpecialOrig = origType != SurfaceOriginationType.None;
            chkSafeCorridorCombine.Visible = isSpecialOrig;

            if (isSpecialOrig)
            {
                string origLabel = origType == SurfaceOriginationType.FromCorridor ? "Corridor Surface" : "Grading Surface";
                lblExistingSurfInfo.Text = $"⚠️ '{selectedSurf}' ({origLabel}, {triCount:N0} tam giác)";
                lblExistingSurfInfo.ForeColor = WinFormsColor.FromArgb(150, 75, 0);
                chkSafeCorridorCombine.Text = $"🛡️ Gộp an toàn vào '{selectedSurf}_Gop' (tránh mất khi Rebuild Corridor)";
            }
            else
            {
                lblExistingSurfInfo.Text = $"📊 Đang chọn: '{selectedSurf}' — Mặt phẳng TIN ({triCount:N0} tam giác hiện có)";
                lblExistingSurfInfo.ForeColor = WinFormsColor.FromArgb(24, 95, 60);
            }
        }

        #region DRAWING METADATA SCAN (STYLES, EXISTING SURFACES, FILTER GROUPS)

        private void ScanDrawingSourcesAndStyles()
        {
            cboMasterStyles.Items.Clear();
            cboExistingSurfaces.Items.Clear();
            _existingSurfaceMap.Clear();
            _surfaceOriginationMap.Clear();
            _surfaceTriCountMap.Clear();
            _bySite.Clear();
            _byLayer.Clear();
            _byCorridor.Clear();
            _allFeatureLineIdsInDrawing.Clear();

            try
            {
                CivilDocument civDoc = CivilApplication.ActiveDocument;
                using (Transaction tr = _db.TransactionManager.StartTransaction())
                {
                    if (civDoc != null)
                    {
                        // 1. Nạp Surface Styles
                        foreach (ObjectId styleId in civDoc.Styles.SurfaceStyles)
                        {
                            if (tr.GetObject(styleId, OpenMode.ForRead) is SurfaceStyle style &&
                                !string.IsNullOrWhiteSpace(style.Name))
                            {
                                cboMasterStyles.Items.Add(style.Name);
                            }
                        }

                        // 2. Nạp danh sách TIN Surfaces hiện có kèm nguồn gốc & số lượng tam giác
                        foreach (ObjectId surfId in civDoc.GetSurfaceIds())
                        {
                            if (surfId.IsNull || !surfId.IsValid || surfId.IsErased) continue;
                            if (tr.GetObject(surfId, OpenMode.ForRead) is CivSurface surf &&
                                !string.IsNullOrWhiteSpace(surf.Name))
                            {
                                _existingSurfaceMap[surf.Name] = surfId;
                                try { _surfaceOriginationMap[surf.Name] = surf.OriginationType; }
                                catch { _surfaceOriginationMap[surf.Name] = SurfaceOriginationType.None; }
                                try { _surfaceTriCountMap[surf.Name] = surf.GetTriangles(false).Count; }
                                catch { _surfaceTriCountMap[surf.Name] = 0; }
                                cboExistingSurfaces.Items.Add(surf.Name);
                            }
                        }
                    }

                    // 3. Quét ModelSpace để lập chỉ mục Site / Layer / Corridor cho bộ lọc nạp nhanh
                    Dictionary<ObjectId, string> siteNameLookup = new Dictionary<ObjectId, string>();
                    if (civDoc != null)
                    {
                        foreach (ObjectId siteId in civDoc.GetSiteIds())
                        {
                            if (tr.GetObject(siteId, OpenMode.ForRead) is Site site && !string.IsNullOrWhiteSpace(site.Name))
                            {
                                siteNameLookup[siteId] = site.Name;
                            }
                        }
                    }

                    if (tr.GetObject(_db.BlockTableId, OpenMode.ForRead) is BlockTable bt &&
                        tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) is BlockTableRecord ms)
                    {
                        foreach (ObjectId entId in ms)
                        {
                            if (entId.IsNull || !entId.IsValid || entId.IsErased) continue;
                            string dxfName = entId.ObjectClass?.DxfName ?? "";

                            bool isFl = dxfName == "AECC_FEATURE_LINE" || dxfName == "AECC_AUTO_CORRIDOR_FEATURE_LINE";
                            bool isPoly = dxfName == "LWPOLYLINE" || dxfName == "POLYLINE";
                            if (!isFl && !isPoly) continue;

                            if (tr.GetObject(entId, OpenMode.ForRead) is not CadEntity ent) continue;

                            string layerName = string.IsNullOrWhiteSpace(ent.Layer) ? "0" : ent.Layer;
                            if (!_byLayer.TryGetValue(layerName, out var layerList))
                            {
                                layerList = new List<ObjectId>();
                                _byLayer[layerName] = layerList;
                            }
                            layerList.Add(entId);

                            if (ent is AutoCorridorFeatureLine acfl)
                            {
                                _allFeatureLineIdsInDrawing.Add(entId);
                                string corrName = "Corridor";
                                try
                                {
                                    if (!string.IsNullOrWhiteSpace(acfl.CorridorName))
                                        corrName = acfl.CorridorName;
                                }
                                catch { }

                                if (!_byCorridor.TryGetValue(corrName, out var corrList))
                                {
                                    corrList = new List<ObjectId>();
                                    _byCorridor[corrName] = corrList;
                                }
                                corrList.Add(entId);
                            }
                            else if (ent is FeatureLine fl)
                            {
                                _allFeatureLineIdsInDrawing.Add(entId);
                                string siteLabel = "<None> (Không thuộc Site)";
                                try
                                {
                                    if (!fl.SiteId.IsNull && siteNameLookup.TryGetValue(fl.SiteId, out string? sName))
                                    {
                                        siteLabel = sName;
                                    }
                                }
                                catch { }

                                if (!_bySite.TryGetValue(siteLabel, out var siteList))
                                {
                                    siteList = new List<ObjectId>();
                                    _bySite[siteLabel] = siteList;
                                }
                                siteList.Add(entId);
                            }
                        }
                    }

                    tr.Commit();
                }
            }
            catch { }

            if (cboMasterStyles.Items.Count == 0)
            {
                cboMasterStyles.Items.Add("Standard");
            }
            cboMasterStyles.SelectedIndex = 0;

            if (cboExistingSurfaces.Items.Count > 0)
            {
                cboExistingSurfaces.SelectedIndex = 0;
            }
            else
            {
                radExistingSurface.Enabled = false;
                radNewSurface.Checked = true;
            }
        }

        private void PopulateFilterValuesDropdown()
        {
            cboFilterValue.Items.Clear();
            int mode = cboFilterSource.SelectedIndex;

            if (mode == 0)
            {
                cboFilterValue.Items.Add($"Tất cả Feature Line ({_allFeatureLineIdsInDrawing.Count} đường)");
                cboFilterValue.Enabled = false;
            }
            else if (mode == 1)
            {
                cboFilterValue.Enabled = true;
                foreach (var kvp in _bySite.OrderBy(k => k.Key))
                {
                    cboFilterValue.Items.Add($"{kvp.Key} ({kvp.Value.Count})");
                }
            }
            else if (mode == 2)
            {
                cboFilterValue.Enabled = true;
                foreach (var kvp in _byLayer.OrderBy(k => k.Key))
                {
                    cboFilterValue.Items.Add($"{kvp.Key} ({kvp.Value.Count})");
                }
            }
            else if (mode == 3)
            {
                cboFilterValue.Enabled = true;
                foreach (var kvp in _byCorridor.OrderBy(k => k.Key))
                {
                    cboFilterValue.Items.Add($"{kvp.Key} ({kvp.Value.Count})");
                }
            }

            if (cboFilterValue.Items.Count > 0)
            {
                cboFilterValue.SelectedIndex = 0;
            }
            else
            {
                cboFilterValue.Items.Add("(Không có đối tượng phù hợp)");
                cboFilterValue.SelectedIndex = 0;
                cboFilterValue.Enabled = false;
            }
        }

        private void BtnLoadByFilter_Click(object? sender, EventArgs e)
        {
            int mode = cboFilterSource.SelectedIndex;
            string selectedText = cboFilterValue.SelectedItem?.ToString() ?? "";
            string rawKey = ExtractKeyBeforeCount(selectedText);

            List<ObjectId>? targetIds = null;
            if (mode == 0)
            {
                targetIds = _allFeatureLineIdsInDrawing;
            }
            else if (mode == 1 && _bySite.TryGetValue(rawKey, out var siteIds))
            {
                targetIds = siteIds;
            }
            else if (mode == 2 && _byLayer.TryGetValue(rawKey, out var layerIds))
            {
                targetIds = layerIds;
            }
            else if (mode == 3 && _byCorridor.TryGetValue(rawKey, out var corrIds))
            {
                targetIds = corrIds;
            }

            if (targetIds == null || targetIds.Count == 0)
            {
                MessageBox.Show("Không tìm thấy đối tượng nào khớp với bộ lọc đã chọn.", "Bộ lọc trống", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var merged = _items.Select(x => x.Id).Concat(targetIds).Distinct().ToList();
            PopulateItems(merged);
            lblStatus.Text = $"Đã nạp từ bộ lọc: hiện có {_items.Count} đối tượng trong danh sách.";
        }

        private static string ExtractKeyBeforeCount(string display)
        {
            int lastParen = display.LastIndexOf(" (", StringComparison.Ordinal);
            return lastParen > 0 ? display.Substring(0, lastParen) : display;
        }

        #endregion

        #region PERSISTENT STATE SAVE & RESTORE (RULE 2)

        private void SaveCurrentSettings()
        {
            _lastFormSize = this.Size;
            _lastCreateNewSurface = radNewSurface.Checked;
            _lastSurfaceName = txtSurfaceName.Text.Trim();

            if (cboExistingSurfaces.SelectedItem != null)
            {
                string surfName = cboExistingSurfaces.SelectedItem.ToString() ?? "";
                _lastExistingSurfaceName = surfName;
                if (_existingSurfaceMap.TryGetValue(surfName, out ObjectId sId))
                    _lastExistingSurfaceId = sId;
            }

            if (cboMasterStyles.SelectedItem != null)
                _lastMasterSurfaceStyle = cboMasterStyles.SelectedItem.ToString() ?? "";

            _lastFilterSourceIdx = Math.Max(0, cboFilterSource.SelectedIndex);
            _lastFilterValue = cboFilterValue.SelectedItem?.ToString() ?? "";

            if (radBoundaryAuto.Checked) _lastBoundaryMode = FeatureLineBoundaryMode.AutoSmart;
            else if (radBoundaryCustom.Checked) _lastBoundaryMode = FeatureLineBoundaryMode.CustomBoundaryEntities;
            else _lastBoundaryMode = FeatureLineBoundaryMode.BreaklinesOnly;

            _lastMidOrdDistance = (double)numMidOrd.Value;
            _lastUseSupplementing = chkUseSupplementing.Checked;
            _lastSupplementingDistance = (double)numSupplementing.Value;
            _lastLimitMaxEdge = chkLimitMaxEdge.Checked;
            _lastMaxEdgeLength = (double)numMaxEdgeLength.Value;
            _lastAutoClose = chkAutoClose.Checked;
            _lastTreatNestedAsHole = chkNestedClosedAsHole.Checked;
            _lastRebuildAuto = chkRebuildAuto.Checked;
            _lastOverwriteOverlap = chkOverwriteOverlap.Checked;
            _lastSafeCorridorCombine = chkSafeCorridorCombine.Checked;

            _lastSelectedIds = _items.Select(x => x.Id).Where(IsIdAlive).ToList();
            _lastCustomOuterIds = _customOuterIds.Where(IsIdAlive).ToList();
            _lastCustomHideIds = _customHideIds.Where(IsIdAlive).ToList();
        }

        private void RestoreLastSettings()
        {
            if (string.IsNullOrWhiteSpace(_lastSurfaceName))
                GenerateDefaultSurfaceName();
            else
                txtSurfaceName.Text = _lastSurfaceName;

            if (!_lastCreateNewSurface && cboExistingSurfaces.Items.Count > 0)
                radExistingSurface.Checked = true;
            else
                radNewSurface.Checked = true;

            if (!string.IsNullOrEmpty(_lastExistingSurfaceName) && cboExistingSurfaces.Items.Contains(_lastExistingSurfaceName))
                cboExistingSurfaces.SelectedItem = _lastExistingSurfaceName;

            if (!string.IsNullOrEmpty(_lastMasterSurfaceStyle) && cboMasterStyles.Items.Contains(_lastMasterSurfaceStyle))
                cboMasterStyles.SelectedItem = _lastMasterSurfaceStyle;

            if (_lastFilterSourceIdx >= 0 && _lastFilterSourceIdx < cboFilterSource.Items.Count)
                cboFilterSource.SelectedIndex = _lastFilterSourceIdx;
            else
                cboFilterSource.SelectedIndex = 0;

            PopulateFilterValuesDropdown();
            if (!string.IsNullOrEmpty(_lastFilterValue) && cboFilterValue.Items.Contains(_lastFilterValue))
                cboFilterValue.SelectedItem = _lastFilterValue;

            radBoundaryAuto.Checked = _lastBoundaryMode == FeatureLineBoundaryMode.AutoSmart;
            radBoundaryCustom.Checked = _lastBoundaryMode == FeatureLineBoundaryMode.CustomBoundaryEntities;
            radBoundaryNone.Checked = _lastBoundaryMode == FeatureLineBoundaryMode.BreaklinesOnly;

            numMidOrd.Value = ClampDecimal((decimal)_lastMidOrdDistance, numMidOrd.Minimum, numMidOrd.Maximum);
            chkUseSupplementing.Checked = _lastUseSupplementing;
            numSupplementing.Value = ClampDecimal((decimal)_lastSupplementingDistance, numSupplementing.Minimum, numSupplementing.Maximum);
            chkLimitMaxEdge.Checked = _lastLimitMaxEdge;
            numMaxEdgeLength.Value = ClampDecimal((decimal)_lastMaxEdgeLength, numMaxEdgeLength.Minimum, numMaxEdgeLength.Maximum);

            chkAutoClose.Checked = _lastAutoClose;
            chkNestedClosedAsHole.Checked = _lastTreatNestedAsHole;
            chkRebuildAuto.Checked = _lastRebuildAuto;
            chkOverwriteOverlap.Checked = _lastOverwriteOverlap;
            chkSafeCorridorCombine.Checked = _lastSafeCorridorCombine;

            _customOuterIds.Clear();
            _customOuterIds.AddRange(_lastCustomOuterIds.Where(IsIdAlive));
            _customHideIds.Clear();
            _customHideIds.AddRange(_lastCustomHideIds.Where(IsIdAlive));
            UpdateCustomBoundaryLabel();
        }

        private static decimal ClampDecimal(decimal val, decimal min, decimal max)
        {
            if (val < min) return min;
            if (val > max) return max;
            return val;
        }

        private static bool IsIdAlive(ObjectId id) => !id.IsNull && id.IsValid && !id.IsErased;

        private void GenerateDefaultSurfaceName()
        {
            txtSurfaceName.Text = $"MatPhang_FL_{DateTime.Now:yyyyMMdd_HHmm}";
        }

        private void TryRestorePreviousSelection()
        {
            if (_lastSelectedIds == null || _lastSelectedIds.Count == 0) return;

            List<ObjectId> validIds = _lastSelectedIds.Where(IsIdAlive).ToList();
            if (validIds.Count > 0)
            {
                PopulateItems(validIds);
                lblStatus.Text = $"Đã khôi phục {validIds.Count} đối tượng từ lần chạy trước.";
            }
        }

        #endregion

        #region TWO-WAY CANVAS INTERACTION & SELECTION

        private void PickSourceEntitiesOnCad(bool append)
        {
            PromptSelectionOptions pso = new PromptSelectionOptions
            {
                MessageForAdding = "\nChọn các đường Feature Line, Corridor FL, 3D/2D Polyline (hở hoặc kín): ",
                AllowDuplicates = false
            };

            SelectionFilter filter = new SelectionFilter(new TypedValue[]
            {
                new TypedValue((int)DxfCode.Operator, "<OR"),
                new TypedValue((int)DxfCode.Start, "AECC_FEATURE_LINE"),
                new TypedValue((int)DxfCode.Start, "AECC_AUTO_CORRIDOR_FEATURE_LINE"),
                new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),
                new TypedValue((int)DxfCode.Start, "POLYLINE"),
                new TypedValue((int)DxfCode.Start, "LINE"),
                new TypedValue((int)DxfCode.Start, "ARC"),
                new TypedValue((int)DxfCode.Operator, "OR>")
            });

            PromptSelectionResult psr;
            using (EditorUserInteraction interaction = _ed.StartUserInteraction(this))
            {
                psr = _ed.GetSelection(pso, filter);
            }

            if (psr.Status != PromptStatus.OK || psr.Value == null || psr.Value.Count == 0)
                return;

            List<ObjectId> pickedIds = psr.Value.GetObjectIds().ToList();
            if (append && _items.Count > 0)
            {
                pickedIds = _items.Select(x => x.Id).Concat(pickedIds).Distinct().ToList();
            }

            PopulateItems(pickedIds);
        }

        private void BtnPickExistingSurface_Click(object? sender, EventArgs e)
        {
            PromptEntityOptions peo = new PromptEntityOptions("\nChọn mặt phẳng TIN Surface đích trên bản vẽ: ");
            peo.SetRejectMessage("\nĐối tượng chọn phải là TIN Surface của Civil 3D.");
            peo.AddAllowedClass(typeof(CivSurface), true);

            PromptEntityResult per;
            using (EditorUserInteraction interaction = _ed.StartUserInteraction(this))
            {
                per = _ed.GetEntity(peo);
            }

            if (per.Status != PromptStatus.OK || !IsIdAlive(per.ObjectId)) return;

            using (Transaction tr = _db.TransactionManager.StartTransaction())
            {
                if (tr.GetObject(per.ObjectId, OpenMode.ForRead) is CivSurface surf && !string.IsNullOrWhiteSpace(surf.Name))
                {
                    _existingSurfaceMap[surf.Name] = per.ObjectId;
                    try { _surfaceOriginationMap[surf.Name] = surf.OriginationType; }
                    catch { _surfaceOriginationMap[surf.Name] = SurfaceOriginationType.None; }
                    try { _surfaceTriCountMap[surf.Name] = surf.GetTriangles(false).Count; }
                    catch { _surfaceTriCountMap[surf.Name] = 0; }

                    if (!cboExistingSurfaces.Items.Contains(surf.Name))
                        cboExistingSurfaces.Items.Add(surf.Name);

                    cboExistingSurfaces.SelectedItem = surf.Name;
                    radExistingSurface.Enabled = true;
                    radExistingSurface.Checked = true;
                    UpdateExistingSurfaceMetadataUI();
                }
                tr.Commit();
            }
        }

        private void PickCustomBoundaryEntities(bool isOuter)
        {
            PromptSelectionOptions pso = new PromptSelectionOptions
            {
                MessageForAdding = isOuter
                    ? "\nChọn một hoặc nhiều đường Biên bao ngoài (Outer Boundary) khép kín: "
                    : "\nChọn các đường Lỗ khoét (Hide Boundary) khép kín: ",
                AllowDuplicates = false
            };

            SelectionFilter filter = new SelectionFilter(new TypedValue[]
            {
                new TypedValue((int)DxfCode.Operator, "<OR"),
                new TypedValue((int)DxfCode.Start, "AECC_FEATURE_LINE"),
                new TypedValue((int)DxfCode.Start, "AECC_AUTO_CORRIDOR_FEATURE_LINE"),
                new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),
                new TypedValue((int)DxfCode.Start, "POLYLINE"),
                new TypedValue((int)DxfCode.Operator, "OR>")
            });

            PromptSelectionResult psr;
            using (EditorUserInteraction interaction = _ed.StartUserInteraction(this))
            {
                psr = _ed.GetSelection(pso, filter);
            }

            if (psr.Status != PromptStatus.OK || psr.Value == null || psr.Value.Count == 0)
                return;

            var targetList = isOuter ? _customOuterIds : _customHideIds;
            targetList.Clear();
            targetList.AddRange(psr.Value.GetObjectIds().Where(IsIdAlive));
            UpdateCustomBoundaryLabel();
        }

        private void UpdateCustomBoundaryLabel()
        {
            lblCustomBndInfo.Text = $"Biên riêng đã chọn: {_customOuterIds.Count} Outer | {_customHideIds.Count} Hide";
            lblCustomBndInfo.ForeColor = _customOuterIds.Count > 0
                ? WinFormsColor.FromArgb(20, 110, 55)
                : WinFormsColor.FromArgb(70, 80, 95);
        }

        private void PopulateItems(List<ObjectId> ids)
        {
            _items.Clear();
            lvObjects.BeginUpdate();
            lvObjects.Items.Clear();

            try
            {
                if (ids == null || ids.Count == 0)
                {
                    UpdateSummary();
                    return;
                }

                double midOrd = (double)numMidOrd.Value;
                bool autoClose = chkAutoClose.Checked;

                using (Transaction tr = _db.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId id in ids)
                    {
                        if (!IsIdAlive(id)) continue;

                        CadEntity? ent = tr.GetObject(id, OpenMode.ForRead) as CadEntity;
                        if (ent == null) continue;

                        var curveInfo = CTT_Tao_Mat_Phang_Tu_Featureline_Command.ExtractSourceCurveInfo(
                            id, tr, midOrd, 0.10, autoClose, _items.Count);

                        if (curveInfo == null || curveInfo.Points3D.Count < 2)
                            continue;

                        double length2d = 0.0;
                        double minZ = double.MaxValue;
                        double maxZ = double.MinValue;
                        var pts = curveInfo.Points3D;

                        for (int i = 0; i < pts.Count; i++)
                        {
                            if (pts[i].Z < minZ) minZ = pts[i].Z;
                            if (pts[i].Z > maxZ) maxZ = pts[i].Z;
                            if (i > 0)
                            {
                                double dx = pts[i].X - pts[i - 1].X;
                                double dy = pts[i].Y - pts[i - 1].Y;
                                length2d += Math.Sqrt(dx * dx + dy * dy);
                            }
                        }

                        var item = new SelectedFeatureLineBoundaryItem
                        {
                            Id = id,
                            Handle = ent.Handle.ToString(),
                            DisplayName = curveInfo.DisplayName,
                            TypeName = curveInfo.EntityTypeName,
                            Length2D = length2d,
                            IsClosed = curveInfo.IsClosed,
                            MinZ = minZ == double.MaxValue ? 0.0 : minZ,
                            MaxZ = maxZ == double.MinValue ? 0.0 : maxZ,
                            SourceInfo = ent.Layer ?? "0",
                            SamplePoints = pts
                        };

                        _items.Add(item);
                    }
                    tr.Commit();
                }

                RebuildListViewRows();
            }
            finally
            {
                lvObjects.EndUpdate();
            }

            UpdateSummary();
            lblStatus.Text = $"Đã tải {_items.Count} đối tượng nguồn.";
        }

        private void RefreshItemsClosedState()
        {
            if (_items.Count == 0) return;
            List<ObjectId> currentIds = _items.Select(x => x.Id).ToList();
            PopulateItems(currentIds);
        }

        private void RebuildListViewRows()
        {
            lvObjects.Items.Clear();
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                var lvi = new ListViewItem((i + 1).ToString());
                lvi.SubItems.Add(item.DisplayName);
                lvi.SubItems.Add(item.TypeName);
                lvi.SubItems.Add(item.IsClosed ? "Kín" : "Hở");
                lvi.SubItems.Add(item.Length2D.ToString("F2"));
                lvi.SubItems.Add(item.MinZ.ToString("F2"));
                lvi.SubItems.Add(item.MaxZ.ToString("F2"));
                lvi.SubItems.Add(item.SourceInfo);
                lvi.Tag = item;

                if (item.IsClosed)
                {
                    lvi.ForeColor = WinFormsColor.FromArgb(20, 105, 55);
                }
                else
                {
                    lvi.ForeColor = WinFormsColor.FromArgb(18, 72, 135);
                }

                lvObjects.Items.Add(lvi);
            }
        }

        private void RemoveSelectedItemsInList()
        {
            if (lvObjects.SelectedIndices.Count == 0) return;

            var indicesToRemove = lvObjects.SelectedIndices.Cast<int>().OrderByDescending(i => i).ToList();
            foreach (int idx in indicesToRemove)
            {
                if (idx >= 0 && idx < _items.Count)
                    _items.RemoveAt(idx);
            }

            lvObjects.BeginUpdate();
            RebuildListViewRows();
            lvObjects.EndUpdate();
            UpdateSummary();
        }

        private void ClearAllSourceItems()
        {
            _items.Clear();
            lvObjects.Items.Clear();
            UpdateSummary();
            lblStatus.Text = "Đã xóa toàn bộ danh sách đối tượng nguồn.";
        }

        private void UpdateSummary()
        {
            int total = _items.Count;
            int closedCount = _items.Count(x => x.IsClosed);
            int openCount = total - closedCount;
            double totalLen = _items.Sum(x => x.Length2D);

            if (total == 0)
            {
                lblSummary.Text = "Đã chọn: 0 đối tượng (0 đường hở, 0 đường kín) | Tổng chiều dài: 0.00 m";
                return;
            }

            double minZ = _items.Min(x => x.MinZ);
            double maxZ = _items.Max(x => x.MaxZ);
            lblSummary.Text = $"Đã chọn: {total} đối tượng ({openCount} Hở, {closedCount} Kín) | Tổng dài: {totalLen:N2} m | Cao độ Z: [{minZ:F2} .. {maxZ:F2}] m";
        }

        private void BtnSuggestMaxEdge_Click(object? sender, EventArgs e)
        {
            var openItems = _items.Where(x => !x.IsClosed && x.SamplePoints.Count >= 2).ToList();
            if (openItems.Count < 2)
            {
                MessageBox.Show(
                    "Cần có ít nhất 2 đường Feature Line hở trong danh sách để tự động đo bề rộng dải lân cận.\nGiá trị mặc định 25.0 m vẫn hoạt động tốt cho đa số taluy & nền đường.",
                    "Gợi ý chiều dài cạnh tối đa",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            List<double> nearestGaps = new List<double>();
            for (int i = 0; i < openItems.Count; i++)
            {
                var ptsA = openItems[i].SamplePoints;
                int stepA = Math.Max(1, ptsA.Count / 12);

                for (int p = 0; p < ptsA.Count; p += stepA)
                {
                    Point3d pa = ptsA[p];
                    double bestDistSq = double.MaxValue;

                    for (int j = 0; j < openItems.Count; j++)
                    {
                        if (i == j) continue;
                        var ptsB = openItems[j].SamplePoints;
                        for (int q = 0; q < ptsB.Count; q++)
                        {
                            double dx = pa.X - ptsB[q].X;
                            double dy = pa.Y - ptsB[q].Y;
                            double dSq = dx * dx + dy * dy;
                            if (dSq > 0.04 && dSq < bestDistSq) // bỏ qua trùng nút < 0.2m
                            {
                                bestDistSq = dSq;
                            }
                        }
                    }

                    if (bestDistSq < double.MaxValue)
                    {
                        nearestGaps.Add(Math.Sqrt(bestDistSq));
                    }
                }
            }

            if (nearestGaps.Count == 0) return;

            nearestGaps.Sort();
            // Lấy phân vị 75% của khoảng cách ngang sang đường gần nhất và nhân hệ số đường chéo an toàn 2.2x
            double p75Gap = nearestGaps[(int)Math.Min(nearestGaps.Count - 1, nearestGaps.Count * 0.75)];
            double supp = chkUseSupplementing.Checked ? (double)numSupplementing.Value : 5.0;
            double suggested = Math.Ceiling(Math.Sqrt(p75Gap * p75Gap + supp * supp) * 1.85);
            suggested = Math.Max(3.0, Math.Min(500.0, suggested));

            numMaxEdgeLength.Value = ClampDecimal((decimal)suggested, numMaxEdgeLength.Minimum, numMaxEdgeLength.Maximum);
            lblStatus.Text = $"⚡ Đã gợi ý cạnh biên tối đa = {suggested:F2} m (khoảng cách ngang dải ~ {p75Gap:F2} m).";
        }

        #endregion

        #region EXECUTION

        private void BtnExecute_Click(object? sender, EventArgs e)
        {
            if (_items.Count == 0)
            {
                MessageBox.Show(
                    "Vui lòng chọn ít nhất một đường Feature Line / Corridor FL / Polyline nguồn!",
                    "Chưa chọn đối tượng nguồn",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            bool createNew = radNewSurface.Checked;
            string surfaceName = txtSurfaceName.Text.Trim();
            ObjectId existingSurfaceId = ObjectId.Null;
            bool overwriteSameName = false;

            if (createNew)
            {
                if (string.IsNullOrWhiteSpace(surfaceName))
                {
                    MessageBox.Show("Vui lòng nhập tên cho Mặt phẳng TIN mới!", "Thiếu tên Mặt phẳng", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtSurfaceName.Focus();
                    return;
                }

                // Kiểm tra trùng tên mặt phẳng hiện có
                if (_existingSurfaceMap.TryGetValue(surfaceName, out ObjectId dupSurfId) && IsIdAlive(dupSurfId))
                {
                    DialogResult dupChoice = MessageBox.Show(
                        $"Mặt phẳng tên '{surfaceName}' đã tồn tại trong bản vẽ!\n\n" +
                        $"• Nhấn [Yes]  : Ghi đè (Xóa mặt phẳng '{surfaceName}' cũ và tạo mới lại)\n" +
                        $"• Nhấn [No]   : Tự động đánh số tên mới ('{surfaceName}_1')\n" +
                        $"• Nhấn [Cancel]: Hủy để đổi tên khác",
                        "Trùng tên Mặt phẳng TIN",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Question);

                    if (dupChoice == DialogResult.Cancel) return;
                    overwriteSameName = dupChoice == DialogResult.Yes;
                }
            }
            else
            {
                string selectedSurf = cboExistingSurfaces.SelectedItem?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(selectedSurf) || !_existingSurfaceMap.TryGetValue(selectedSurf, out existingSurfaceId) || !IsIdAlive(existingSurfaceId))
                {
                    MessageBox.Show("Vui lòng chọn một mặt phẳng TIN hiện có hợp lệ!", "Chưa chọn mặt phẳng đích", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                surfaceName = selectedSurf;
            }

            FeatureLineBoundaryMode bndMode = FeatureLineBoundaryMode.AutoSmart;
            if (radBoundaryCustom.Checked)
            {
                bndMode = FeatureLineBoundaryMode.CustomBoundaryEntities;
                if (_customOuterIds.Count == 0)
                {
                    MessageBox.Show(
                        "Bạn đang chọn chế độ 'Giới hạn theo đường Biên bao chọn riêng trên CAD' nhưng chưa chọn đường Biên ngoài (Outer) nào!",
                        "Thiếu đường Biên ngoài",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }
            }
            else if (radBoundaryNone.Checked)
            {
                bndMode = FeatureLineBoundaryMode.BreaklinesOnly;
            }

            var options = new TaoMatPhangOptions
            {
                CreateNewSurface = createNew,
                SurfaceName = surfaceName,
                ExistingSurfaceId = existingSurfaceId,
                OverwriteExistingIfNameMatches = overwriteSameName,
                SurfaceStyleName = cboMasterStyles.SelectedItem?.ToString() ?? "",
                SourceEntityIds = _items.Select(x => x.Id).ToList(),
                BoundaryMode = bndMode,
                CustomOuterBoundaryIds = _customOuterIds.Where(IsIdAlive).ToList(),
                CustomHideBoundaryIds = _customHideIds.Where(IsIdAlive).ToList(),
                MidOrdinateDistance = (double)numMidOrd.Value,
                UseSupplementing = chkUseSupplementing.Checked,
                SupplementingDistance = (double)numSupplementing.Value,
                LimitMaxEdgeLength = chkLimitMaxEdge.Checked,
                MaxEdgeLength = (double)numMaxEdgeLength.Value,
                AutoCloseNearClosed = chkAutoClose.Checked,
                AutoCloseTolerance = 0.10,
                TreatNestedClosedAsHole = chkNestedClosedAsHole.Checked,
                AutoRebuild = chkRebuildAuto.Checked,
                OverwriteOverlapOnExisting = chkOverwriteOverlap.Checked,
                CreateCombinedForCorridorSurface = chkSafeCorridorCombine.Checked,
                CombinedSurfaceName = $"{surfaceName}_Gop"
            };

            this.UseWaitCursor = true;
            progressBar.Visible = true;
            progressBar.Style = ProgressBarStyle.Marquee;
            btnExecute.Enabled = false;
            lblStatus.Text = createNew
                ? "Đang xây dựng lưới tam giác TIN mới và cắt lọc biên trực tiếp (0 mặt phẳng phụ)..."
                : $"Đang bổ sung mảng Feature Line vào mặt phẳng '{surfaceName}' (0 mặt phẳng phụ)...";
            System.Windows.Forms.Application.DoEvents();

            try
            {
                SurfaceCreationResult result = CTT_Tao_Mat_Phang_Tu_Featureline_Command.ThucHienTaoMatPhang(_db, _ed, options);

                if (result.Success)
                {
                    lblStatus.Text = $"✅ {result.Message}";
                    string triInfoLine = result.IsSupplementedIntoExisting
                        ? $"• Lưới tam giác : {result.PreviousTriangleCount:N0} → {result.FinalTriangleCount:N0} (đã cắt lọc {result.TrimmedTriangleCount:N0} tam giác bắc cầu/thừa)\n"
                        : $"• Tam giác hợp lệ: {result.FinalTriangleCount:N0} (đã cắt {result.TrimmedTriangleCount:N0} tam giác thừa)\n";

                    MessageBox.Show(
                        $"{result.Message}\n\n" +
                        $"• Mặt phẳng TIN : '{result.FinalSurfaceName}'\n" +
                        $"• Đường đã xử lý: {result.ProcessedCurveCount} ({result.OpenCurveCount} Hở, {result.ClosedCurveCount} Kín)\n" +
                        triInfoLine +
                        $"• Mặt phẳng phụ : 0 (Bản vẽ sạch 100%)",
                        result.IsSupplementedIntoExisting ? "Bổ Sung Mặt Phẳng Thành Công" : "Tạo Mặt Phẳng Thành Công",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);

                    SaveCurrentSettings();
                    if (!string.IsNullOrWhiteSpace(result.FinalSurfaceName) && IsIdAlive(result.MasterSurfaceId))
                    {
                        _lastExistingSurfaceName = result.FinalSurfaceName;
                        _lastExistingSurfaceId = result.MasterSurfaceId;
                    }
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else
                {
                    lblStatus.Text = $"❌ Lỗi: {result.Message}";
                    MessageBox.Show($"Không thể tạo mặt phẳng:\n{result.Message}", "Lỗi thực thi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (System.Exception ex)
            {
                lblStatus.Text = $"❌ Lỗi ngoại lệ: {ex.Message}";
                MessageBox.Show($"Xảy ra lỗi ngoại lệ:\n{ex.Message}", "Lỗi ngoại lệ", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.UseWaitCursor = false;
                progressBar.Visible = false;
                btnExecute.Enabled = true;
            }
        }

        #endregion
    }
}

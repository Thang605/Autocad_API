using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsButton = System.Windows.Forms.Button;
using WinFormsTextBox = System.Windows.Forms.TextBox;
using WinFormsComboBox = System.Windows.Forms.ComboBox;
using FlowDirection = System.Windows.Forms.FlowDirection;
using ColumnStyle = System.Windows.Forms.ColumnStyle;
using Font = System.Drawing.Font;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace MyFirstProject.Civil_Tool_2
{
    public enum CorridorComponentBuildMode
    {
        SingleOrExistingCorridor = 0,
        SeparateCorridorPerComponent = 1
    }

    public enum ComponentBaselineSourceType
    {
        AlignmentAndProfile = 0,
        FeatureLine = 1
    }

    public class CorridorComponentRowItem
    {
        public bool IsEnabled { get; set; } = true;
        public string ComponentCategory { get; set; } = "Lòng đường chính";
        public string ComponentName { get; set; } = "CauKien_1";
        public ComponentBaselineSourceType SourceType { get; set; } = ComponentBaselineSourceType.AlignmentAndProfile;
        public List<ObjectId> BaselineEntityIds { get; set; } = new List<ObjectId>();
        public string BaselineDisplayText { get; set; } = "(Chưa chọn)";
        public ObjectId ExplicitProfileId { get; set; } = ObjectId.Null;
        public string ProfileName { get; set; } = "(Tự động lấy Đường đỏ)";
        public ObjectId AssemblyId { get; set; } = ObjectId.Null;
        public string AssemblyName { get; set; } = "";
        public double StartStation { get; set; } = 0.0;
        public double EndStation { get; set; } = 0.0; // <= 0 nghĩa là lấy toàn bộ chiều dài
        public double FrequencyTangent { get; set; } = 10.0;
        public double FrequencyCurve { get; set; } = 5.0;
        public ObjectId TargetSurfaceId { get; set; } = ObjectId.Null;
        public string TargetSurfaceName { get; set; } = "(Theo mặc định)";
        public List<ObjectId> OffsetElevationTargetIds { get; set; } = new List<ObjectId>();
        public string OffsetTargetsDisplay { get; set; } = "(Không)";
        public ObjectId SourceCorridorId { get; set; } = ObjectId.Null;
    }

    public class TaoCorridorTheoCauKienForm : Form
    {
        // ═══════════════════════════════════════════════════════════════
        //  PERSISTENT STATE (Ghi nhớ toàn bộ cấu hình lần chạy trước)
        // ═══════════════════════════════════════════════════════════════
        private static CorridorComponentBuildMode _lastBuildMode = CorridorComponentBuildMode.SingleOrExistingCorridor;
        private static ObjectId _lastExistingCorridorId = ObjectId.Null;
        private static string _lastCorridorPrefix = "COR_";
        private static ObjectId _lastCodeSetStyleId = ObjectId.Null;
        private static ObjectId _lastDefaultSurfaceId = ObjectId.Null;
        private static decimal _lastDefaultFreqTangent = 10m;
        private static decimal _lastDefaultFreqCurve = 5m;
        private static bool _lastAutoRebuild = true;
        private static bool _lastDeleteMergedSourceCorridors = true;
        private static Size _lastFormSize = new Size(1240, 720);
        private static List<CorridorComponentRowItem> _savedRows = new List<CorridorComponentRowItem>();

        private readonly Database _db;
        private readonly Editor _ed;
        private bool _updatingRadioMode = false;

        // Danh mục dữ liệu từ bản vẽ
        private readonly List<(string Name, ObjectId Id)> _corridorList = new List<(string, ObjectId)>();
        private readonly List<(string Name, ObjectId Id)> _assemblyList = new List<(string, ObjectId)>();
        private readonly List<(string Name, ObjectId Id)> _surfaceList = new List<(string, ObjectId)>();
        private readonly List<(string Name, ObjectId Id)> _codeSetStyleList = new List<(string, ObjectId)>();
        private readonly List<(string Name, ObjectId Id)> _alignmentList = new List<(string, ObjectId)>();

        public static readonly string[] ComponentCategories = new[]
        {
            "Lòng đường chính",
            "Dải phân cách giữa",
            "Vỉa hè & Bó vỉa Trái",
            "Vỉa hè & Bó vỉa Phải",
            "Rãnh biên / Tường chắn",
            "Nhánh nút giao rẽ phải",
            "Taluy Đào / Đắp",
            "Cấu kiện tùy chỉnh"
        };

        public static readonly string[] SourceTypeNames = new[]
        {
            "Alignment + Profile",
            "Feature Line"
        };

        // ═══════════════════════════════════════════════════════════════
        //  OUTPUT PROPERTIES
        // ═══════════════════════════════════════════════════════════════
        public bool FormAccepted { get; private set; } = false;
        public CorridorComponentBuildMode BuildMode { get; private set; } = CorridorComponentBuildMode.SingleOrExistingCorridor;
        public ObjectId ExistingCorridorId { get; private set; } = ObjectId.Null;
        public string CorridorNamePrefix { get; private set; } = "COR_";
        public ObjectId SelectedCodeSetStyleId { get; private set; } = ObjectId.Null;
        public ObjectId DefaultTargetSurfaceId { get; private set; } = ObjectId.Null;
        public double DefaultFreqTangent { get; private set; } = 10.0;
        public double DefaultFreqCurve { get; private set; } = 5.0;
        public bool AutoRebuild { get; private set; } = true;
        public bool DeleteMergedSourceCorridors { get; private set; } = true;
        public List<CorridorComponentRowItem> ComponentRows { get; private set; } = new List<CorridorComponentRowItem>();
        public List<ObjectId> SourceCorridorsToMerge { get; private set; } = new List<ObjectId>();

        // ═══════════════════════════════════════════════════════════════
        //  UI CONTROLS
        // ═══════════════════════════════════════════════════════════════
        private RadioButton radSeparateCorridor = null!;
        private RadioButton radSingleOrExistingCorridor = null!;
        private WinFormsComboBox cmbExistingCorridor = null!;
        private WinFormsButton btnPickExistingCorridor = null!;
        private WinFormsButton btnMergeExistingCorridors = null!;
        private CheckBox chkDeleteMergedSourceCorridors = null!;
        private WinFormsTextBox txtCorridorPrefix = null!;
        private WinFormsComboBox cmbCodeSetStyle = null!;
        private WinFormsComboBox cmbDefaultSurface = null!;
        private NumericUpDown numFreqTangent = null!;
        private NumericUpDown numFreqCurve = null!;
        private CheckBox chkAutoRebuild = null!;

        private DataGridView dgvComponents = null!;
        private WinFormsLabel lblEmptyListNotice = null!;
        private TableLayoutPanel pnlSubActions = null!;
        private WinFormsButton btnAddAlignmentComponent = null!;
        private WinFormsButton btnAddFeatureLineComponent = null!;
        private WinFormsButton btnRestoreSavedRows = null!;
        private WinFormsButton btnPickBaselineForRow = null!;
        private WinFormsButton btnPickAssemblyForRow = null!;
        private WinFormsButton btnPickOffsetTargetsForRow = null!;
        private WinFormsButton btnRemoveSelectedRow = null!;
        private WinFormsButton btnClearAllRows = null!;

        private WinFormsLabel lblSummary = null!;
        private WinFormsButton btnExecute = null!;
        private WinFormsButton btnCancel = null!;

        public TaoCorridorTheoCauKienForm(Database db, Editor ed)
        {
            _db = db;
            _ed = ed;
            LoadDrawingCatalogs();
            InitializeComponent();
            RestoreLastSettings();
            UpdateComponentListVisibility();
            UpdateSummaryLabel();
        }

        private void LoadDrawingCatalogs()
        {
            _corridorList.Clear();
            _assemblyList.Clear();
            _surfaceList.Clear();
            _codeSetStyleList.Clear();
            _alignmentList.Clear();

            using (Transaction tr = _db.TransactionManager.StartTransaction())
            {
                CivilDocument cdoc = CivilDocument.GetCivilDocument(_db);

                foreach (ObjectId id in cdoc.CorridorCollection)
                {
                    if (tr.GetObject(id, OpenMode.ForRead) is Corridor corr)
                        _corridorList.Add((corr.Name, id));
                }

                foreach (ObjectId id in cdoc.AssemblyCollection)
                {
                    if (tr.GetObject(id, OpenMode.ForRead) is Assembly asm)
                        _assemblyList.Add((asm.Name, id));
                }

                foreach (ObjectId id in cdoc.GetSurfaceIds())
                {
                    if (tr.GetObject(id, OpenMode.ForRead) is CivSurface surf)
                        _surfaceList.Add((surf.Name, id));
                }

                foreach (ObjectId id in cdoc.Styles.CodeSetStyles)
                {
                    if (tr.GetObject(id, OpenMode.ForRead) is CodeSetStyle css)
                        _codeSetStyleList.Add((css.Name, id));
                }

                foreach (ObjectId id in cdoc.GetAlignmentIds())
                {
                    if (tr.GetObject(id, OpenMode.ForRead) is Alignment al)
                        _alignmentList.Add((al.Name, id));
                }

                tr.Commit();
            }
        }

        private void InitializeComponent()
        {
            this.Text = "CTC_Tao_Corridor_Theo_Cau_Kien — Xây Dựng & Gộp Corridor Theo Cấu Kiện (Modular BIM)";
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimumSize = new Size(1050, 620);

            Rectangle workArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1366, 768);
            int initW = Math.Min(_lastFormSize.Width, workArea.Width - 40);
            int initH = Math.Min(_lastFormSize.Height, workArea.Height - 40);
            this.Size = new Size(Math.Max(1050, initW), Math.Max(620, initH));

            this.FormClosing += (s, e) => SaveCurrentSettings();

            // ── BỐ CỤC CHÍNH (TableLayoutPanel 4 hàng: Header | Group 1 | Group 2 (Fill) | Footer) ──
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(12, 8, 12, 10)
            };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));          // Hàng 0: Tiêu đề
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));          // Hàng 1: Group 1
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));     // Hàng 2: Group 2 (DataGridView giãn nở)
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));          // Hàng 3: Thanh nút thực thi dưới đáy

            // ── HÀNG 0: Tiêu đề ──
            var lblHeader = new WinFormsLabel
            {
                Text = "XÂY DỰNG & GỘP CORRIDOR THEO TỪNG CẤU KIỆN (LÒNG ĐƯỜNG, DẢI PHÂN CÁCH, VỈA HÈ, RÃNH, TALUY)",
                AutoSize = true,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 70, 150),
                UseMnemonic = false,
                Margin = new Padding(2, 0, 2, 6)
            };
            mainLayout.Controls.Add(lblHeader, 0, 0);

            // ── HÀNG 1: GROUP 1 (Phương thức tổ chức Corridor và Thông số mặc định) ──
            var grpGeneral = new GroupBox
            {
                Text = "1. Phương thức tổ chức / Gộp Corridor && Thông số mặc định",
                Dock = DockStyle.Fill,
                AutoSize = true,
                Padding = new Padding(10, 6, 10, 8),
                Margin = new Padding(0, 0, 0, 6)
            };

            var grp1Layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                AutoSize = true
            };
            grp1Layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            grp1Layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grp1Layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grp1Layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // Dòng 1: Radio tách riêng
            radSeparateCorridor = new RadioButton
            {
                Text = "Tách riêng mỗi Cấu kiện thành 1 Corridor độc lập (Mỗi dòng cấu kiện tạo 1 Corridor riêng)",
                AutoSize = true,
                Checked = false,
                AutoCheck = false,
                UseMnemonic = false,
                Margin = new Padding(3, 2, 3, 2)
            };
            radSeparateCorridor.Click += (s, e) => SetBuildModeUI(CorridorComponentBuildMode.SeparateCorridorPerComponent);
            grp1Layout.Controls.Add(radSeparateCorridor, 0, 0);

            // Dòng 2: Radio gộp + ComboBox chọn Corridor + Nút Pick + Nút Gộp Corridor có sẵn
            var pnlSingleRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoSize = true,
                Margin = new Padding(0, 2, 0, 4)
            };

            radSingleOrExistingCorridor = new RadioButton
            {
                Text = "Gộp tất cả cấu kiện vào 1 Corridor (Tạo mới hoặc thêm vào Corridor có sẵn):",
                AutoSize = true,
                Checked = true,
                AutoCheck = false,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                UseMnemonic = false,
                Margin = new Padding(3, 6, 8, 2)
            };
            radSingleOrExistingCorridor.Click += (s, e) => SetBuildModeUI(CorridorComponentBuildMode.SingleOrExistingCorridor);

            cmbExistingCorridor = new WinFormsComboBox
            {
                Width = 240,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 3, 8, 2)
            };
            cmbExistingCorridor.Items.Add("(Tạo 1 Corridor gộp mới từ Tiền tố)");
            foreach (var c in _corridorList)
                cmbExistingCorridor.Items.Add(c.Name);
            cmbExistingCorridor.SelectedIndex = 0;
            cmbExistingCorridor.SelectedIndexChanged += (s, e) =>
            {
                if (cmbExistingCorridor.SelectedIndex > 0)
                    SetBuildModeUI(CorridorComponentBuildMode.SingleOrExistingCorridor);
                else
                    UpdateSummaryLabel();
            };

            btnPickExistingCorridor = new WinFormsButton
            {
                Text = "Pick Corridor đích...",
                Size = new Size(145, 34),
                UseMnemonic = false,
                Margin = new Padding(0, 0, 8, 2)
            };
            btnPickExistingCorridor.Click += BtnPickExistingCorridor_Click;

            btnMergeExistingCorridors = new WinFormsButton
            {
                Text = "Gộp các Corridor có sẵn...",
                Size = new Size(185, 34),
                BackColor = Color.FromArgb(255, 244, 214),
                UseMnemonic = false,
                Margin = new Padding(0, 0, 8, 2)
            };
            btnMergeExistingCorridors.Click += BtnMergeExistingCorridors_Click;

            chkDeleteMergedSourceCorridors = new CheckBox
            {
                Text = "Xóa Corridor nguồn sau khi gộp",
                AutoSize = true,
                Checked = true,
                UseMnemonic = false,
                Margin = new Padding(2, 7, 0, 2)
            };

            pnlSingleRow.Controls.AddRange(new Control[]
            {
                radSingleOrExistingCorridor,
                cmbExistingCorridor,
                btnPickExistingCorridor,
                btnMergeExistingCorridors,
                chkDeleteMergedSourceCorridors
            });
            grp1Layout.Controls.Add(pnlSingleRow, 0, 1);

            // Dòng 3: Tiền tố + Code Set Style + Bề mặt tự nhiên + Bước cọc
            var pnlParamsRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoSize = true,
                Margin = new Padding(0, 2, 0, 0)
            };

            var lblPrefix = new WinFormsLabel
            {
                Text = "Tên / Tiền tố Corridor:",
                AutoSize = true,
                UseMnemonic = false,
                Margin = new Padding(3, 6, 4, 2)
            };
            txtCorridorPrefix = new WinFormsTextBox
            {
                Text = "COR_",
                Width = 105,
                Margin = new Padding(0, 3, 14, 2)
            };
            txtCorridorPrefix.TextChanged += (s, e) => UpdateSummaryLabel();

            var lblCodeSet = new WinFormsLabel
            {
                Text = "Code Set Style:",
                AutoSize = true,
                UseMnemonic = false,
                Margin = new Padding(0, 6, 4, 2)
            };
            cmbCodeSetStyle = new WinFormsComboBox
            {
                Width = 175,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 3, 14, 2)
            };
            foreach (var css in _codeSetStyleList)
                cmbCodeSetStyle.Items.Add(css.Name);
            if (cmbCodeSetStyle.Items.Count > 0)
                cmbCodeSetStyle.SelectedIndex = 0;

            var lblDefSurface = new WinFormsLabel
            {
                Text = "Bề mặt tự nhiên (EG):",
                AutoSize = true,
                UseMnemonic = false,
                Margin = new Padding(0, 6, 4, 2)
            };
            cmbDefaultSurface = new WinFormsComboBox
            {
                Width = 155,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 3, 14, 2)
            };
            cmbDefaultSurface.Items.Add("(Không gán Surface)");
            foreach (var s in _surfaceList)
                cmbDefaultSurface.Items.Add(s.Name);
            int egIdx = _surfaceList.FindIndex(x => x.Name.Contains("EG", StringComparison.OrdinalIgnoreCase) || x.Name.Contains("TN", StringComparison.OrdinalIgnoreCase));
            cmbDefaultSurface.SelectedIndex = egIdx >= 0 ? egIdx + 1 : (_surfaceList.Count > 0 ? 1 : 0);

            var lblFreqT = new WinFormsLabel
            {
                Text = "Bước cọc Thẳng / Cong (m):",
                AutoSize = true,
                UseMnemonic = false,
                Margin = new Padding(0, 6, 4, 2)
            };
            numFreqTangent = new NumericUpDown
            {
                Width = 60,
                Minimum = 1m,
                Maximum = 200m,
                DecimalPlaces = 1,
                Value = 10m,
                Margin = new Padding(0, 3, 4, 2)
            };
            numFreqCurve = new NumericUpDown
            {
                Width = 60,
                Minimum = 0.5m,
                Maximum = 200m,
                DecimalPlaces = 1,
                Value = 5m,
                Margin = new Padding(0, 3, 0, 2)
            };

            pnlParamsRow.Controls.AddRange(new Control[]
            {
                lblPrefix, txtCorridorPrefix,
                lblCodeSet, cmbCodeSetStyle,
                lblDefSurface, cmbDefaultSurface,
                lblFreqT, numFreqTangent, numFreqCurve
            });
            grp1Layout.Controls.Add(pnlParamsRow, 0, 2);

            grpGeneral.Controls.Add(grp1Layout);
            mainLayout.Controls.Add(grpGeneral, 0, 1);

            // ── HÀNG 2: GROUP 2 (Chọn Alignment / FeatureLine & Bảng danh sách các Cấu kiện Corridor) ──
            var grpComponents = new GroupBox
            {
                Text = "2. Chọn Đường dẫn (Alignment / FeatureLine) && Danh sách Cấu kiện Corridor",
                Dock = DockStyle.Fill,
                Padding = new Padding(10, 6, 10, 8),
                Margin = new Padding(0, 0, 0, 6)
            };

            var grp2Layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4
            };
            grp2Layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            grp2Layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // Hàng 0: Thanh nút chọn Alignment / FeatureLine
            grp2Layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // Hàng 1: Thông báo khi chưa chọn Alignment/FeatureLine
            grp2Layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // Hàng 2: Bảng DataGridView (chỉ hiện khi đã chọn)
            grp2Layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // Hàng 3: Thanh nút Target & Rebuild dưới bảng

            // Thanh nút công cụ phía trên bảng
            var pnlToolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoSize = true,
                Margin = new Padding(0, 2, 0, 6)
            };

            btnAddAlignmentComponent = new WinFormsButton
            {
                Text = "+ Chọn Alignment (Tuyến)",
                Size = new Size(200, 40),
                BackColor = Color.FromArgb(228, 242, 255),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                UseMnemonic = false,
                Margin = new Padding(0, 0, 6, 2)
            };
            btnAddAlignmentComponent.Click += BtnAddAlignmentComponent_Click;

            btnAddFeatureLineComponent = new WinFormsButton
            {
                Text = "+ Chọn FeatureLine",
                Size = new Size(180, 40),
                BackColor = Color.FromArgb(228, 242, 255),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                UseMnemonic = false,
                Margin = new Padding(0, 0, 6, 2)
            };
            btnAddFeatureLineComponent.Click += BtnAddFeatureLineComponent_Click;

            btnRestoreSavedRows = new WinFormsButton
            {
                Text = "Nạp lại ĐT lần trước",
                Size = new Size(170, 40),
                UseMnemonic = false,
                Visible = false,
                Margin = new Padding(0, 0, 6, 2)
            };
            btnRestoreSavedRows.Click += BtnRestoreSavedRows_Click;

            btnPickBaselineForRow = new WinFormsButton
            {
                Text = "Đổi Đường dẫn dòng chọn",
                Size = new Size(190, 40),
                UseMnemonic = false,
                Margin = new Padding(0, 0, 6, 2)
            };
            btnPickBaselineForRow.Click += BtnPickBaselineForRow_Click;

            btnPickAssemblyForRow = new WinFormsButton
            {
                Text = "Pick Assembly trên CAD",
                Size = new Size(175, 40),
                UseMnemonic = false,
                Margin = new Padding(0, 0, 6, 2)
            };
            btnPickAssemblyForRow.Click += BtnPickAssemblyForRow_Click;

            btnRemoveSelectedRow = new WinFormsButton
            {
                Text = "Xóa dòng",
                Size = new Size(85, 40),
                ForeColor = Color.DarkRed,
                UseMnemonic = false,
                Margin = new Padding(0, 0, 6, 2)
            };
            btnRemoveSelectedRow.Click += BtnRemoveSelectedRow_Click;

            btnClearAllRows = new WinFormsButton
            {
                Text = "Xóa tất cả",
                Size = new Size(95, 40),
                ForeColor = Color.DarkRed,
                UseMnemonic = false,
                Margin = new Padding(0, 0, 0, 2)
            };
            btnClearAllRows.Click += BtnClearAllRows_Click;

            pnlToolbar.Controls.AddRange(new Control[]
            {
                btnAddAlignmentComponent,
                btnAddFeatureLineComponent,
                btnRestoreSavedRows,
                btnPickBaselineForRow,
                btnPickAssemblyForRow,
                btnRemoveSelectedRow,
                btnClearAllRows
            });
            grp2Layout.Controls.Add(pnlToolbar, 0, 0);

            // Thông báo hướng dẫn khi chưa chọn Alignment hoặc FeatureLine
            lblEmptyListNotice = new WinFormsLabel
            {
                Text = "👉 Danh sách cấu kiện chỉ hiển thị sau khi bạn chọn Alignment hoặc FeatureLine.\r\n" +
                       "Hãy bấm nút [+ Chọn Alignment (Tuyến)] hoặc [+ Chọn FeatureLine] ở phía trên để chọn đối tượng trên bản vẽ.",
                Dock = DockStyle.Fill,
                AutoSize = true,
                Padding = new Padding(14, 18, 14, 18),
                BackColor = Color.FromArgb(248, 250, 252),
                ForeColor = Color.FromArgb(60, 80, 110),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Italic),
                UseMnemonic = false,
                Margin = new Padding(0, 4, 0, 6)
            };
            grp2Layout.Controls.Add(lblEmptyListNotice, 0, 1);

            // Bảng DataGridView
            dgvComponents = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersWidth = 26,
                RowTemplate = { Height = 28 },
                ColumnHeadersHeight = 32,
                BackgroundColor = Color.White,
                Margin = new Padding(0, 0, 0, 6)
            };
            SetupDataGridViewColumns();
            dgvComponents.CellValueChanged += (s, e) =>
            {
                if (e.RowIndex >= 0) SyncGridRowToModel(e.RowIndex);
                UpdateSummaryLabel();
            };
            dgvComponents.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (dgvComponents.IsCurrentCellDirty)
                    dgvComponents.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            dgvComponents.DataError += (s, e) => { e.ThrowException = false; };
            grp2Layout.Controls.Add(dgvComponents, 0, 2);

            // Dòng dưới bảng: Gán Target & CheckBox AutoRebuild
            pnlSubActions = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                AutoSize = true,
                Margin = new Padding(0)
            };
            pnlSubActions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pnlSubActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            btnPickOffsetTargetsForRow = new WinFormsButton
            {
                Text = "Gán Target Mở rộng / Cao độ (Alignment, FeatureLine, Polyline) cho dòng chọn...",
                Size = new Size(540, 40),
                UseMnemonic = false,
                Margin = new Padding(0, 0, 16, 0)
            };
            btnPickOffsetTargetsForRow.Click += BtnPickOffsetTargetsForRow_Click;

            chkAutoRebuild = new CheckBox
            {
                Text = "Tự động Rebuild Corridor ngay sau khi lắp ghép các cấu kiện",
                AutoSize = true,
                Checked = true,
                UseMnemonic = false,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Anchor = AnchorStyles.Left,
                Margin = new Padding(4, 8, 0, 0)
            };

            pnlSubActions.Controls.Add(btnPickOffsetTargetsForRow, 0, 0);
            pnlSubActions.Controls.Add(chkAutoRebuild, 1, 0);
            grp2Layout.Controls.Add(pnlSubActions, 0, 3);

            grpComponents.Controls.Add(grp2Layout);
            mainLayout.Controls.Add(grpComponents, 0, 2);

            // ── HÀNG 3: FOOTER (Summary + Nút Thực Thi & Đóng — Luôn hiển thị 100%) ──
            var pnlFooter = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                AutoSize = true,
                Margin = new Padding(0, 2, 0, 0)
            };
            pnlFooter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            pnlFooter.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pnlFooter.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            lblSummary = new WinFormsLabel
            {
                Text = "Sẵn sàng.",
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 100, 60),
                UseMnemonic = false,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(2, 8, 8, 0)
            };

            btnExecute = new WinFormsButton
            {
                Text = "Xây Dựng / Gộp Corridor",
                Size = new Size(255, 42),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                UseMnemonic = false,
                Margin = new Padding(0, 0, 8, 0)
            };
            btnExecute.Click += BtnExecute_Click;

            btnCancel = new WinFormsButton
            {
                Text = "Đóng",
                Size = new Size(110, 42),
                UseMnemonic = false,
                Margin = new Padding(0)
            };
            btnCancel.Click += (s, e) =>
            {
                this.FormAccepted = false;
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };

            pnlFooter.Controls.Add(lblSummary, 0, 0);
            pnlFooter.Controls.Add(btnExecute, 1, 0);
            pnlFooter.Controls.Add(btnCancel, 2, 0);
            mainLayout.Controls.Add(pnlFooter, 0, 3);

            this.AcceptButton = btnExecute;
            this.CancelButton = btnCancel;
            this.Controls.Add(mainLayout);
        }

        private void SetupDataGridViewColumns()
        {
            dgvComponents.Columns.Clear();

            var colEnable = new DataGridViewCheckBoxColumn
            {
                Name = "colEnable",
                HeaderText = "Bật",
                FillWeight = 38,
                MinimumWidth = 42
            };

            var colCategory = new DataGridViewComboBoxColumn
            {
                Name = "colCategory",
                HeaderText = "Nhóm Cấu Kiện",
                FillWeight = 135,
                MinimumWidth = 125,
                FlatStyle = FlatStyle.Flat
            };
            colCategory.Items.AddRange(ComponentCategories);

            var colName = new DataGridViewTextBoxColumn
            {
                Name = "colName",
                HeaderText = "Tên Cấu Kiện / Corridor",
                FillWeight = 130,
                MinimumWidth = 120
            };

            var colSourceType = new DataGridViewComboBoxColumn
            {
                Name = "colSourceType",
                HeaderText = "Loại Đường Dẫn",
                FillWeight = 115,
                MinimumWidth = 110,
                FlatStyle = FlatStyle.Flat
            };
            colSourceType.Items.AddRange(SourceTypeNames);

            var colBaseline = new DataGridViewTextBoxColumn
            {
                Name = "colBaseline",
                HeaderText = "Đường Dẫn (Alignment / FeatureLine)",
                FillWeight = 195,
                MinimumWidth = 160,
                ReadOnly = true
            };

            var colAssembly = new DataGridViewComboBoxColumn
            {
                Name = "colAssembly",
                HeaderText = "Mặt Cắt Cấu Kiện (Assembly)",
                FillWeight = 155,
                MinimumWidth = 140,
                FlatStyle = FlatStyle.Flat
            };
            foreach (var asm in _assemblyList)
                colAssembly.Items.Add(asm.Name);

            var colStartSta = new DataGridViewTextBoxColumn
            {
                Name = "colStartSta",
                HeaderText = "Lý Trình Đầu",
                FillWeight = 75,
                MinimumWidth = 75
            };

            var colEndSta = new DataGridViewTextBoxColumn
            {
                Name = "colEndSta",
                HeaderText = "Lý Trình Cuối (0=Hết)",
                FillWeight = 95,
                MinimumWidth = 90
            };

            var colFreq = new DataGridViewTextBoxColumn
            {
                Name = "colFreq",
                HeaderText = "Bước Cọc (m)",
                FillWeight = 72,
                MinimumWidth = 70
            };

            var colTargets = new DataGridViewTextBoxColumn
            {
                Name = "colTargets",
                HeaderText = "Target Mở Rộng / Cao Độ",
                FillWeight = 115,
                MinimumWidth = 105,
                ReadOnly = true
            };

            dgvComponents.Columns.AddRange(new DataGridViewColumn[]
            {
                colEnable, colCategory, colName, colSourceType,
                colBaseline, colAssembly, colStartSta, colEndSta, colFreq, colTargets
            });
        }

        private void SetBuildModeUI(CorridorComponentBuildMode mode)
        {
            if (_updatingRadioMode) return;
            _updatingRadioMode = true;
            try
            {
                bool singleMode = (mode == CorridorComponentBuildMode.SingleOrExistingCorridor);
                radSingleOrExistingCorridor.Checked = singleMode;
                radSeparateCorridor.Checked = !singleMode;
                radSingleOrExistingCorridor.Font = new Font("Segoe UI", 9F, singleMode ? FontStyle.Bold : FontStyle.Regular);
                radSeparateCorridor.Font = new Font("Segoe UI", 9F, !singleMode ? FontStyle.Bold : FontStyle.Regular);

                cmbExistingCorridor.Enabled = singleMode;
                btnPickExistingCorridor.Enabled = singleMode;
                btnMergeExistingCorridors.Enabled = singleMode;
                chkDeleteMergedSourceCorridors.Enabled = singleMode;
            }
            finally
            {
                _updatingRadioMode = false;
            }
            UpdateSummaryLabel();
        }

        /// <summary>
        /// Chỉ hiển thị bảng danh sách cấu kiện khi đã chọn ít nhất 1 Alignment hoặc FeatureLine
        /// </summary>
        private void UpdateComponentListVisibility()
        {
            bool hasComponents = dgvComponents.Rows.Count > 0;
            dgvComponents.Visible = hasComponents;
            lblEmptyListNotice.Visible = !hasComponents;

            btnPickBaselineForRow.Visible = hasComponents;
            btnPickAssemblyForRow.Visible = hasComponents;
            btnRemoveSelectedRow.Visible = hasComponents;
            btnClearAllRows.Visible = hasComponents;
            btnPickOffsetTargetsForRow.Visible = hasComponents;

            int validSavedCount = _savedRows.Count(r =>
                r.BaselineEntityIds != null &&
                r.BaselineEntityIds.Any(id => !id.IsNull && id.IsValid && !id.IsErased && id.Database == _db));
            btnRestoreSavedRows.Visible = !hasComponents && validSavedCount > 0;
            if (btnRestoreSavedRows.Visible)
            {
                btnRestoreSavedRows.Text = $"Nạp lại ĐT lần trước ({validSavedCount})";
            }
        }

        private void AddRowToGrid(CorridorComponentRowItem item)
        {
            if (item.BaselineEntityIds == null || item.BaselineEntityIds.Count == 0)
                return;

            string asmName = item.AssemblyName;
            if (string.IsNullOrEmpty(asmName) || !_assemblyList.Any(a => a.Name.Equals(asmName, StringComparison.OrdinalIgnoreCase)))
            {
                asmName = _assemblyList.Count > 0 ? _assemblyList[0].Name : "";
                item.AssemblyName = asmName;
                if (_assemblyList.Count > 0) item.AssemblyId = _assemblyList[0].Id;
            }
            else
            {
                item.AssemblyId = _assemblyList.First(a => a.Name.Equals(asmName, StringComparison.OrdinalIgnoreCase)).Id;
            }

            string category = ComponentCategories.Contains(item.ComponentCategory)
                ? item.ComponentCategory
                : ComponentCategories[0];

            string srcTypeStr = item.SourceType == ComponentBaselineSourceType.FeatureLine
                ? SourceTypeNames[1]
                : SourceTypeNames[0];

            int rowIdx = dgvComponents.Rows.Add(
                item.IsEnabled,
                category,
                item.ComponentName,
                srcTypeStr,
                item.BaselineDisplayText,
                asmName,
                item.StartStation.ToString("F2"),
                item.EndStation.ToString("F2"),
                item.FrequencyTangent.ToString("F1"),
                item.OffsetTargetsDisplay
            );
            dgvComponents.Rows[rowIdx].Tag = item;
        }

        private void SyncGridRowToModel(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= dgvComponents.Rows.Count) return;
            var row = dgvComponents.Rows[rowIndex];
            if (row.Tag is not CorridorComponentRowItem item) return;

            item.IsEnabled = Convert.ToBoolean(row.Cells["colEnable"].Value ?? true);
            item.ComponentCategory = Convert.ToString(row.Cells["colCategory"].Value) ?? ComponentCategories[0];
            item.ComponentName = (Convert.ToString(row.Cells["colName"].Value) ?? "CauKien").Trim();

            string srcStr = Convert.ToString(row.Cells["colSourceType"].Value) ?? SourceTypeNames[0];
            item.SourceType = srcStr == SourceTypeNames[1]
                ? ComponentBaselineSourceType.FeatureLine
                : ComponentBaselineSourceType.AlignmentAndProfile;

            string asmName = Convert.ToString(row.Cells["colAssembly"].Value) ?? "";
            item.AssemblyName = asmName;
            var foundAsm = _assemblyList.FirstOrDefault(a => a.Name.Equals(asmName, StringComparison.OrdinalIgnoreCase));
            if (!foundAsm.Id.IsNull) item.AssemblyId = foundAsm.Id;

            if (double.TryParse(Convert.ToString(row.Cells["colStartSta"].Value), out double sSta))
                item.StartStation = Math.Max(0.0, sSta);
            if (double.TryParse(Convert.ToString(row.Cells["colEndSta"].Value), out double eSta))
                item.EndStation = Math.Max(0.0, eSta);
            if (double.TryParse(Convert.ToString(row.Cells["colFreq"].Value), out double freq) && freq >= 0.5)
                item.FrequencyTangent = freq;
        }

        // ═══════════════════════════════════════════════════════════════
        //  TƯƠNG TÁC HAI CHIỀU VỚI AUTOCAD CANVAS (StartUserInteraction)
        // ═══════════════════════════════════════════════════════════════
        private void BtnPickExistingCorridor_Click(object? sender, EventArgs e)
        {
            using (EditorUserInteraction ui = _ed.StartUserInteraction(this))
            {
                var peo = new PromptEntityOptions("\nChọn Corridor đích hiện có trên bản vẽ để gộp vào: ");
                peo.SetRejectMessage("\nĐối tượng được chọn phải là Corridor.");
                peo.AddAllowedClass(typeof(Corridor), true);

                PromptEntityResult per = _ed.GetEntity(peo);
                if (per.Status == PromptStatus.OK && !per.ObjectId.IsNull)
                {
                    int idx = _corridorList.FindIndex(c => c.Id == per.ObjectId);
                    if (idx >= 0)
                    {
                        cmbExistingCorridor.SelectedIndex = idx + 1;
                        SetBuildModeUI(CorridorComponentBuildMode.SingleOrExistingCorridor);
                    }
                }
            }
        }

        private void BtnMergeExistingCorridors_Click(object? sender, EventArgs e)
        {
            var pickedCorridorIds = PickEntitiesOnCanvas(
                "\nChọn các Corridor trên bản vẽ cần gộp chung: ",
                typeof(Corridor));

            if (pickedCorridorIds.Count == 0) return;

            SetBuildModeUI(CorridorComponentBuildMode.SingleOrExistingCorridor);

            // Nếu người dùng chưa chọn Corridor đích trong ComboBox thì lấy Corridor đầu tiên được chọn làm Corridor đích
            if (cmbExistingCorridor.SelectedIndex == 0 && pickedCorridorIds.Count > 0)
            {
                int firstIdx = _corridorList.FindIndex(c => c.Id == pickedCorridorIds[0]);
                if (firstIdx >= 0)
                    cmbExistingCorridor.SelectedIndex = firstIdx + 1;
            }

            ObjectId targetCorrId = cmbExistingCorridor.SelectedIndex > 0 && cmbExistingCorridor.SelectedIndex - 1 < _corridorList.Count
                ? _corridorList[cmbExistingCorridor.SelectedIndex - 1].Id
                : ObjectId.Null;

            using (Transaction tr = _db.TransactionManager.StartTransaction())
            {
                foreach (ObjectId corrId in pickedCorridorIds)
                {
                    if (tr.GetObject(corrId, OpenMode.ForRead) is not Corridor corr)
                        continue;

                    if (corrId == targetCorrId)
                        continue; // Corridor đích đã có sẵn các Baseline của chính nó

                    if (!SourceCorridorsToMerge.Contains(corrId))
                        SourceCorridorsToMerge.Add(corrId);

                    foreach (Baseline bl in corr.Baselines)
                    {
                        bool isFl = bl.IsFeatureLineBased();
                        ObjectId baseEntId = isFl ? bl.FeatureLineId : bl.AlignmentId;
                        if (baseEntId.IsNull || !baseEntId.IsValid || baseEntId.IsErased)
                            continue;

                        string entDisplay = DescribeSingleBaselineEntity(tr, baseEntId);

                        foreach (BaselineRegion rg in bl.BaselineRegions)
                        {
                            var asmTuple = _assemblyList.FirstOrDefault(a => a.Id == rg.AssemblyId);
                            string asmName = !asmTuple.Id.IsNull ? asmTuple.Name : (_assemblyList.FirstOrDefault().Name ?? "");

                            double freqT = (double)numFreqTangent.Value;
                            double freqC = (double)numFreqCurve.Value;
                            try
                            {
                                if (rg.AppliedAssemblySetting != null)
                                {
                                    freqT = rg.AppliedAssemblySetting.FrequencyAlongTangents;
                                    freqC = rg.AppliedAssemblySetting.FrequencyAlongCurves;
                                }
                            }
                            catch { }

                            ExtractRegionTargets(rg, out ObjectId surfTargetId, out List<ObjectId> offsetTargetIds);

                            var rowItem = new CorridorComponentRowItem
                            {
                                IsEnabled = true,
                                ComponentCategory = isFl ? "Taluy Đào / Đắp" : "Lòng đường chính",
                                ComponentName = $"{corr.Name}",
                                SourceType = isFl ? ComponentBaselineSourceType.FeatureLine : ComponentBaselineSourceType.AlignmentAndProfile,
                                BaselineEntityIds = new List<ObjectId> { baseEntId },
                                BaselineDisplayText = entDisplay,
                                ExplicitProfileId = isFl ? ObjectId.Null : bl.ProfileId,
                                AssemblyId = rg.AssemblyId,
                                AssemblyName = asmName,
                                StartStation = rg.StartStation,
                                EndStation = rg.EndStation,
                                FrequencyTangent = freqT,
                                FrequencyCurve = freqC,
                                TargetSurfaceId = surfTargetId,
                                OffsetElevationTargetIds = offsetTargetIds,
                                OffsetTargetsDisplay = offsetTargetIds.Count > 0 ? $"[{offsetTargetIds.Count} đối tượng]" : "(Không)",
                                SourceCorridorId = corrId
                            };

                            AddRowToGrid(rowItem);
                        }
                    }
                }
                tr.Commit();
            }

            UpdateComponentListVisibility();
            UpdateSummaryLabel();
        }

        private void ExtractRegionTargets(BaselineRegion rg, out ObjectId surfTargetId, out List<ObjectId> offsetTargetIds)
        {
            surfTargetId = ObjectId.Null;
            offsetTargetIds = new List<ObjectId>();
            try
            {
                var targets = rg.GetTargets();
                if (targets == null) return;
                foreach (SubassemblyTargetInfo ti in targets)
                {
                    if (ti.TargetIds == null || ti.TargetIds.Count == 0) continue;
                    string tType = ti.TargetType.ToString();
                    if (tType.Contains("Surface", StringComparison.OrdinalIgnoreCase))
                    {
                        if (surfTargetId.IsNull && !ti.TargetIds[0].IsNull)
                            surfTargetId = ti.TargetIds[0];
                    }
                    else
                    {
                        foreach (ObjectId id in ti.TargetIds)
                        {
                            if (!id.IsNull && !offsetTargetIds.Contains(id))
                                offsetTargetIds.Add(id);
                        }
                    }
                }
            }
            catch { }
        }

        private void BtnAddAlignmentComponent_Click(object? sender, EventArgs e)
        {
            var pickedIds = PickEntitiesOnCanvas(
                "\nChọn một hoặc nhiều Alignment (Tuyến) cho cấu kiện: ",
                typeof(Alignment));

            if (pickedIds.Count == 0) return;

            var defaultAsm = _assemblyList.FirstOrDefault();

            using (Transaction tr = _db.TransactionManager.StartTransaction())
            {
                foreach (ObjectId alId in pickedIds)
                {
                    if (tr.GetObject(alId, OpenMode.ForRead) is not Alignment al)
                        continue;

                    int nextIdx = dgvComponents.Rows.Count + 1;
                    string compName = string.IsNullOrWhiteSpace(al.Name)
                        ? $"CauKien_Tuyen_{nextIdx}"
                        : al.Name.Trim();

                    var newItem = new CorridorComponentRowItem
                    {
                        IsEnabled = true,
                        ComponentCategory = al.Name.Contains("Connected", StringComparison.OrdinalIgnoreCase)
                            ? "Nhánh nút giao rẽ phải"
                            : "Lòng đường chính",
                        ComponentName = compName,
                        SourceType = ComponentBaselineSourceType.AlignmentAndProfile,
                        BaselineEntityIds = new List<ObjectId> { alId },
                        BaselineDisplayText = $"[Tuyến] {al.Name} ({al.StartingStation:F1}→{al.EndingStation:F1}m)",
                        AssemblyId = defaultAsm.Id,
                        AssemblyName = defaultAsm.Name ?? "",
                        StartStation = 0.0,
                        EndStation = 0.0,
                        FrequencyTangent = (double)numFreqTangent.Value,
                        FrequencyCurve = (double)numFreqCurve.Value
                    };

                    AddRowToGrid(newItem);
                }
                tr.Commit();
            }

            UpdateComponentListVisibility();
            UpdateSummaryLabel();
        }

        private void BtnAddFeatureLineComponent_Click(object? sender, EventArgs e)
        {
            var pickedIds = PickEntitiesOnCanvas(
                "\nChọn một hoặc nhiều Feature Line cho cấu kiện (VD: Bó lề, Dải phân cách, Vỉa hè, Taluy): ",
                typeof(FeatureLine));

            if (pickedIds.Count == 0) return;

            var defaultAsm = _assemblyList.FirstOrDefault(a =>
                a.Name.Contains("bo le", StringComparison.OrdinalIgnoreCase) ||
                a.Name.Contains("taluy", StringComparison.OrdinalIgnoreCase) ||
                a.Name.Contains("Phân cách", StringComparison.OrdinalIgnoreCase) ||
                a.Name.Contains("via he", StringComparison.OrdinalIgnoreCase));
            if (defaultAsm.Id.IsNull && _assemblyList.Count > 0)
                defaultAsm = _assemblyList[0];

            using (Transaction tr = _db.TransactionManager.StartTransaction())
            {
                foreach (ObjectId flId in pickedIds)
                {
                    if (tr.GetObject(flId, OpenMode.ForRead) is not FeatureLine fl)
                        continue;

                    int nextIdx = dgvComponents.Rows.Count + 1;
                    string flLabel = string.IsNullOrWhiteSpace(fl.Name) ? $"FL#{fl.Handle}" : fl.Name;

                    var newItem = new CorridorComponentRowItem
                    {
                        IsEnabled = true,
                        ComponentCategory = "Vỉa hè & Bó vỉa Phải",
                        ComponentName = $"CauKien_FL_{nextIdx}",
                        SourceType = ComponentBaselineSourceType.FeatureLine,
                        BaselineEntityIds = new List<ObjectId> { flId },
                        BaselineDisplayText = $"[FeatureLine] {flLabel} (L={fl.Length2D:F2}m)",
                        AssemblyId = defaultAsm.Id,
                        AssemblyName = defaultAsm.Name ?? "",
                        StartStation = 0.0,
                        EndStation = 0.0,
                        FrequencyTangent = (double)numFreqTangent.Value,
                        FrequencyCurve = (double)numFreqCurve.Value
                    };

                    AddRowToGrid(newItem);
                }
                tr.Commit();
            }

            UpdateComponentListVisibility();
            UpdateSummaryLabel();
        }

        private void BtnRestoreSavedRows_Click(object? sender, EventArgs e)
        {
            dgvComponents.Rows.Clear();
            foreach (var saved in _savedRows)
            {
                var validBaselines = saved.BaselineEntityIds
                    .Where(id => !id.IsNull && id.IsValid && !id.IsErased && id.Database == _db)
                    .ToList();
                if (validBaselines.Count == 0)
                    continue;

                var validTargets = saved.OffsetElevationTargetIds
                    .Where(id => !id.IsNull && id.IsValid && !id.IsErased && id.Database == _db)
                    .ToList();

                saved.BaselineEntityIds = validBaselines;
                saved.OffsetElevationTargetIds = validTargets;
                AddRowToGrid(saved);
            }

            UpdateComponentListVisibility();
            UpdateSummaryLabel();
        }

        private void BtnPickBaselineForRow_Click(object? sender, EventArgs e)
        {
            if (dgvComponents.CurrentRow == null || dgvComponents.CurrentRow.Index < 0)
            {
                MessageBox.Show("Vui lòng chọn một dòng cấu kiện trong bảng trước.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int rowIdx = dgvComponents.CurrentRow.Index;
            SyncGridRowToModel(rowIdx);
            if (dgvComponents.Rows[rowIdx].Tag is not CorridorComponentRowItem item) return;

            if (item.SourceType == ComponentBaselineSourceType.FeatureLine)
            {
                var ids = PickEntitiesOnCanvas("\nChọn Feature Line làm đường dẫn cho cấu kiện: ", typeof(FeatureLine));
                if (ids.Count > 0)
                {
                    item.BaselineEntityIds = ids;
                    item.BaselineDisplayText = DescribePickedEntities(ids, "FeatureLine");
                    dgvComponents.Rows[rowIdx].Cells["colBaseline"].Value = item.BaselineDisplayText;
                }
            }
            else
            {
                var ids = PickEntitiesOnCanvas("\nChọn Alignment làm đường dẫn cho cấu kiện: ", typeof(Alignment));
                if (ids.Count > 0)
                {
                    item.BaselineEntityIds = ids;
                    item.BaselineDisplayText = DescribePickedEntities(ids, "Tuyến");
                    dgvComponents.Rows[rowIdx].Cells["colBaseline"].Value = item.BaselineDisplayText;
                }
            }

            UpdateSummaryLabel();
        }

        private void BtnPickAssemblyForRow_Click(object? sender, EventArgs e)
        {
            if (dgvComponents.CurrentRow == null || dgvComponents.CurrentRow.Index < 0)
            {
                MessageBox.Show("Vui lòng chọn một dòng cấu kiện trong bảng trước.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int rowIdx = dgvComponents.CurrentRow.Index;
            SyncGridRowToModel(rowIdx);
            if (dgvComponents.Rows[rowIdx].Tag is not CorridorComponentRowItem item) return;

            using (EditorUserInteraction ui = _ed.StartUserInteraction(this))
            {
                var peo = new PromptEntityOptions("\nChọn Assembly trên bản vẽ cho cấu kiện: ");
                peo.SetRejectMessage("\nVui lòng chọn đúng đối tượng Assembly.");
                peo.AddAllowedClass(typeof(Assembly), true);

                var per = _ed.GetEntity(peo);
                if (per.Status == PromptStatus.OK && !per.ObjectId.IsNull)
                {
                    var found = _assemblyList.FirstOrDefault(a => a.Id == per.ObjectId);
                    if (!found.Id.IsNull)
                    {
                        item.AssemblyId = found.Id;
                        item.AssemblyName = found.Name;
                        dgvComponents.Rows[rowIdx].Cells["colAssembly"].Value = found.Name;
                    }
                }
            }
        }

        private void BtnPickOffsetTargetsForRow_Click(object? sender, EventArgs e)
        {
            if (dgvComponents.CurrentRow == null || dgvComponents.CurrentRow.Index < 0)
            {
                MessageBox.Show("Vui lòng chọn một dòng cấu kiện trong bảng trước.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int rowIdx = dgvComponents.CurrentRow.Index;
            SyncGridRowToModel(rowIdx);
            if (dgvComponents.Rows[rowIdx].Tag is not CorridorComponentRowItem item) return;

            using (EditorUserInteraction ui = _ed.StartUserInteraction(this))
            {
                _ed.WriteMessage("\nChọn các đối tượng Target Mở rộng / Cao độ (Alignment, FeatureLine, Polyline): ");
                var pso = new PromptSelectionOptions
                {
                    MessageForAdding = "\nChọn Alignment, FeatureLine hoặc Polyline làm Target: "
                };

                var psr = _ed.GetSelection(pso);
                if (psr.Status == PromptStatus.OK && psr.Value != null)
                {
                    var validTargetIds = new List<ObjectId>();
                    using (Transaction tr = _db.TransactionManager.StartTransaction())
                    {
                        foreach (SelectedObject so in psr.Value)
                        {
                            if (so == null || so.ObjectId.IsNull) continue;
                            var ent = tr.GetObject(so.ObjectId, OpenMode.ForRead);
                            if (ent is Alignment || ent is FeatureLine || ent is Polyline || ent is Polyline2d || ent is Polyline3d)
                            {
                                validTargetIds.Add(so.ObjectId);
                            }
                        }
                        tr.Commit();
                    }

                    item.OffsetElevationTargetIds = validTargetIds;
                    item.OffsetTargetsDisplay = validTargetIds.Count > 0
                        ? $"[{validTargetIds.Count} đối tượng]"
                        : "(Không)";
                    dgvComponents.Rows[rowIdx].Cells["colTargets"].Value = item.OffsetTargetsDisplay;
                }
            }
        }

        private void BtnRemoveSelectedRow_Click(object? sender, EventArgs e)
        {
            if (dgvComponents.CurrentRow == null || dgvComponents.CurrentRow.Index < 0) return;
            dgvComponents.Rows.RemoveAt(dgvComponents.CurrentRow.Index);
            UpdateComponentListVisibility();
            UpdateSummaryLabel();
        }

        private void BtnClearAllRows_Click(object? sender, EventArgs e)
        {
            dgvComponents.Rows.Clear();
            SourceCorridorsToMerge.Clear();
            UpdateComponentListVisibility();
            UpdateSummaryLabel();
        }

        private List<ObjectId> PickEntitiesOnCanvas(string promptMessage, Type allowedClass)
        {
            var result = new List<ObjectId>();
            using (EditorUserInteraction ui = _ed.StartUserInteraction(this))
            {
                _ed.WriteMessage(promptMessage);
                var pso = new PromptSelectionOptions
                {
                    MessageForAdding = promptMessage
                };

                var psr = _ed.GetSelection(pso);
                if (psr.Status == PromptStatus.OK && psr.Value != null)
                {
                    using (Transaction tr = _db.TransactionManager.StartTransaction())
                    {
                        foreach (SelectedObject so in psr.Value)
                        {
                            if (so == null || so.ObjectId.IsNull) continue;
                            var obj = tr.GetObject(so.ObjectId, OpenMode.ForRead);
                            if (obj != null && allowedClass.IsInstanceOfType(obj))
                            {
                                if (!result.Contains(so.ObjectId))
                                    result.Add(so.ObjectId);
                            }
                        }
                        tr.Commit();
                    }
                }
            }
            return result;
        }

        private string DescribeSingleBaselineEntity(Transaction tr, ObjectId id)
        {
            var obj = tr.GetObject(id, OpenMode.ForRead);
            if (obj is Alignment al)
                return $"[Tuyến] {al.Name} ({al.StartingStation:F1}→{al.EndingStation:F1}m)";
            if (obj is FeatureLine fl)
            {
                string flName = string.IsNullOrWhiteSpace(fl.Name) ? $"FL#{fl.Handle}" : fl.Name;
                return $"[FeatureLine] {flName} (L={fl.Length2D:F2}m)";
            }
            return "(Đã chọn)";
        }

        private string DescribePickedEntities(List<ObjectId> ids, string typeLabel)
        {
            if (ids == null || ids.Count == 0) return "(Chưa chọn)";
            using (Transaction tr = _db.TransactionManager.StartTransaction())
            {
                var names = new List<string>();
                foreach (ObjectId id in ids.Take(2))
                {
                    var obj = tr.GetObject(id, OpenMode.ForRead);
                    if (obj is Alignment al) names.Add(al.Name);
                    else if (obj is FeatureLine fl) names.Add(string.IsNullOrWhiteSpace(fl.Name) ? $"FL#{fl.Handle}" : fl.Name);
                }
                tr.Commit();
                string joined = string.Join(", ", names);
                return ids.Count > 2
                    ? $"[{ids.Count} {typeLabel}] {joined}..."
                    : $"[{ids.Count} {typeLabel}] {joined}";
            }
        }

        private void UpdateSummaryLabel()
        {
            int total = dgvComponents.Rows.Count;
            int enabledCount = 0;
            int totalBaselines = 0;

            for (int i = 0; i < total; i++)
            {
                if (dgvComponents.Rows[i].Tag is CorridorComponentRowItem item)
                {
                    bool en = Convert.ToBoolean(dgvComponents.Rows[i].Cells["colEnable"].Value ?? true);
                    if (en)
                    {
                        enabledCount++;
                        totalBaselines += item.BaselineEntityIds.Count;
                    }
                }
            }

            bool isSingleMode = radSingleOrExistingCorridor.Checked;
            string prefix = string.IsNullOrWhiteSpace(txtCorridorPrefix?.Text) ? "COR_" : txtCorridorPrefix.Text.Trim();
            string newMergedName = prefix.EndsWith("_") ? $"{prefix}Gop_CauKien" : prefix;

            string modeText = !isSingleMode
                ? $"Tách riêng {enabledCount} Corridor theo từng cấu kiện"
                : (cmbExistingCorridor.SelectedIndex > 0
                    ? $"Gộp tất cả vào Corridor '{cmbExistingCorridor.SelectedItem}'"
                    : $"Gộp tất cả vào 1 Corridor '{newMergedName}'");

            lblSummary.Text = total == 0
                ? $"Chưa chọn Alignment / FeatureLine — Chế độ: {modeText}"
                : $"Tổng hợp: {enabledCount}/{total} cấu kiện ({totalBaselines} đường dẫn Baseline) — Chế độ: {modeText}";
        }

        // ═══════════════════════════════════════════════════════════════
        //  PERSISTENT STATE SAVE & RESTORE
        // ═══════════════════════════════════════════════════════════════
        private void SaveCurrentSettings()
        {
            _lastBuildMode = radSingleOrExistingCorridor.Checked
                ? CorridorComponentBuildMode.SingleOrExistingCorridor
                : CorridorComponentBuildMode.SeparateCorridorPerComponent;

            if (cmbExistingCorridor.SelectedIndex > 0 && cmbExistingCorridor.SelectedIndex - 1 < _corridorList.Count)
                _lastExistingCorridorId = _corridorList[cmbExistingCorridor.SelectedIndex - 1].Id;
            else
                _lastExistingCorridorId = ObjectId.Null;

            _lastCorridorPrefix = txtCorridorPrefix.Text.Trim();

            if (cmbCodeSetStyle.SelectedIndex >= 0 && cmbCodeSetStyle.SelectedIndex < _codeSetStyleList.Count)
                _lastCodeSetStyleId = _codeSetStyleList[cmbCodeSetStyle.SelectedIndex].Id;

            if (cmbDefaultSurface.SelectedIndex > 0 && cmbDefaultSurface.SelectedIndex - 1 < _surfaceList.Count)
                _lastDefaultSurfaceId = _surfaceList[cmbDefaultSurface.SelectedIndex - 1].Id;
            else
                _lastDefaultSurfaceId = ObjectId.Null;

            _lastDefaultFreqTangent = numFreqTangent.Value;
            _lastDefaultFreqCurve = numFreqCurve.Value;
            _lastAutoRebuild = chkAutoRebuild.Checked;
            _lastDeleteMergedSourceCorridors = chkDeleteMergedSourceCorridors.Checked;

            if (this.WindowState == FormWindowState.Normal)
                _lastFormSize = this.Size;

            if (dgvComponents.Rows.Count > 0)
            {
                _savedRows.Clear();
                for (int i = 0; i < dgvComponents.Rows.Count; i++)
                {
                    SyncGridRowToModel(i);
                    if (dgvComponents.Rows[i].Tag is CorridorComponentRowItem item && item.BaselineEntityIds.Count > 0)
                    {
                        _savedRows.Add(item);
                    }
                }
            }
        }

        private void RestoreLastSettings()
        {
            if (!_lastExistingCorridorId.IsNull && _lastExistingCorridorId.IsValid && !_lastExistingCorridorId.IsErased)
            {
                int cIdx = _corridorList.FindIndex(c => c.Id == _lastExistingCorridorId);
                if (cIdx >= 0) cmbExistingCorridor.SelectedIndex = cIdx + 1;
            }

            SetBuildModeUI(_lastBuildMode);

            if (!string.IsNullOrWhiteSpace(_lastCorridorPrefix))
                txtCorridorPrefix.Text = _lastCorridorPrefix;

            if (!_lastCodeSetStyleId.IsNull && _lastCodeSetStyleId.IsValid && !_lastCodeSetStyleId.IsErased)
            {
                int cssIdx = _codeSetStyleList.FindIndex(c => c.Id == _lastCodeSetStyleId);
                if (cssIdx >= 0) cmbCodeSetStyle.SelectedIndex = cssIdx;
            }

            if (!_lastDefaultSurfaceId.IsNull && _lastDefaultSurfaceId.IsValid && !_lastDefaultSurfaceId.IsErased)
            {
                int sIdx = _surfaceList.FindIndex(s => s.Id == _lastDefaultSurfaceId);
                if (sIdx >= 0) cmbDefaultSurface.SelectedIndex = sIdx + 1;
            }

            numFreqTangent.Value = Math.Clamp(_lastDefaultFreqTangent, numFreqTangent.Minimum, numFreqTangent.Maximum);
            numFreqCurve.Value = Math.Clamp(_lastDefaultFreqCurve, numFreqCurve.Minimum, numFreqCurve.Maximum);
            chkAutoRebuild.Checked = _lastAutoRebuild;
            chkDeleteMergedSourceCorridors.Checked = _lastDeleteMergedSourceCorridors;

            // Bảng danh sách cấu kiện khởi đầu trống — chỉ hiển thị khi người dùng chọn Alignment hoặc FeatureLine
            dgvComponents.Rows.Clear();
        }

        private void BtnExecute_Click(object? sender, EventArgs e)
        {
            SaveCurrentSettings();

            var currentRows = new List<CorridorComponentRowItem>();
            for (int i = 0; i < dgvComponents.Rows.Count; i++)
            {
                SyncGridRowToModel(i);
                if (dgvComponents.Rows[i].Tag is CorridorComponentRowItem item &&
                    item.IsEnabled &&
                    item.BaselineEntityIds.Count > 0 &&
                    !item.AssemblyId.IsNull)
                {
                    currentRows.Add(item);
                }
            }

            if (currentRows.Count == 0)
            {
                MessageBox.Show(
                    "Vui lòng chọn ít nhất 1 Alignment hoặc Feature Line để hiển thị và lắp ghép cấu kiện.",
                    "Chưa chọn Alignment / FeatureLine",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            this.BuildMode = _lastBuildMode;
            this.ExistingCorridorId = _lastExistingCorridorId;
            this.CorridorNamePrefix = string.IsNullOrWhiteSpace(_lastCorridorPrefix) ? "COR_" : _lastCorridorPrefix;
            this.SelectedCodeSetStyleId = _lastCodeSetStyleId;
            this.DefaultTargetSurfaceId = _lastDefaultSurfaceId;
            this.DefaultFreqTangent = (double)numFreqTangent.Value;
            this.DefaultFreqCurve = (double)numFreqCurve.Value;
            this.AutoRebuild = chkAutoRebuild.Checked;
            this.DeleteMergedSourceCorridors = chkDeleteMergedSourceCorridors.Checked;
            this.ComponentRows = currentRows;
            this.FormAccepted = true;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}

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
    }

    public class TaoCorridorTheoCauKienForm : Form
    {
        // ═══════════════════════════════════════════════════════════════
        //  PERSISTENT STATE (Ghi nhớ toàn bộ cấu hình lần chạy trước)
        // ═══════════════════════════════════════════════════════════════
        private static CorridorComponentBuildMode _lastBuildMode = CorridorComponentBuildMode.SeparateCorridorPerComponent;
        private static ObjectId _lastExistingCorridorId = ObjectId.Null;
        private static string _lastCorridorPrefix = "COR_";
        private static ObjectId _lastCodeSetStyleId = ObjectId.Null;
        private static ObjectId _lastDefaultSurfaceId = ObjectId.Null;
        private static decimal _lastDefaultFreqTangent = 10m;
        private static decimal _lastDefaultFreqCurve = 5m;
        private static bool _lastAutoRebuild = true;
        private static Size _lastFormSize = new Size(1180, 740);
        private static List<CorridorComponentRowItem> _savedRows = new List<CorridorComponentRowItem>();

        private readonly Database _db;
        private readonly Editor _ed;

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
        public CorridorComponentBuildMode BuildMode { get; private set; } = CorridorComponentBuildMode.SeparateCorridorPerComponent;
        public ObjectId ExistingCorridorId { get; private set; } = ObjectId.Null;
        public string CorridorNamePrefix { get; private set; } = "COR_";
        public ObjectId SelectedCodeSetStyleId { get; private set; } = ObjectId.Null;
        public ObjectId DefaultTargetSurfaceId { get; private set; } = ObjectId.Null;
        public double DefaultFreqTangent { get; private set; } = 10.0;
        public double DefaultFreqCurve { get; private set; } = 5.0;
        public bool AutoRebuild { get; private set; } = true;
        public List<CorridorComponentRowItem> ComponentRows { get; private set; } = new List<CorridorComponentRowItem>();

        // ═══════════════════════════════════════════════════════════════
        //  UI CONTROLS
        // ═══════════════════════════════════════════════════════════════
        private RadioButton radSeparateCorridor = null!;
        private RadioButton radSingleOrExistingCorridor = null!;
        private WinFormsComboBox cmbExistingCorridor = null!;
        private WinFormsButton btnPickExistingCorridor = null!;
        private WinFormsTextBox txtCorridorPrefix = null!;
        private WinFormsComboBox cmbCodeSetStyle = null!;
        private WinFormsComboBox cmbDefaultSurface = null!;
        private NumericUpDown numFreqTangent = null!;
        private NumericUpDown numFreqCurve = null!;
        private CheckBox chkAutoRebuild = null!;

        private DataGridView dgvComponents = null!;
        private WinFormsButton btnAddAlignmentComponent = null!;
        private WinFormsButton btnAddFeatureLineComponent = null!;
        private WinFormsButton btnAutoPopulateFromDrawing = null!;
        private WinFormsButton btnPickBaselineForRow = null!;
        private WinFormsButton btnPickAssemblyForRow = null!;
        private WinFormsButton btnPickOffsetTargetsForRow = null!;
        private WinFormsButton btnRemoveSelectedRow = null!;

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
            this.Text = "CTC_Tao_Corridor_Theo_Cau_Kien — Xây Dựng Corridor Theo Từng Cấu Kiện (Modular BIM)";
            this.Size = _lastFormSize;
            this.MinimumSize = new Size(1060, 660);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            this.FormClosing += (s, e) => SaveCurrentSettings();

            var lblHeader = new WinFormsLabel
            {
                Text = "XÂY DỰNG CORRIDOR THEO TỪNG CẤU KIỆN (LÒNG ĐƯỜNG, DẢI PHÂN CÁCH, VỈA HÈ, RÃNH, NÚT GIAO)",
                Location = new Point(16, 10),
                Size = new Size(1130, 26),
                Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 70, 150),
                UseMnemonic = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            this.Controls.Add(lblHeader);

            // ── GROUP 1: Cấu hình chung & Chế độ tổ chức Corridor ──
            var grpGeneral = new GroupBox
            {
                Text = "1. Phương thức tổ chức Corridor & Thông số mặc định",
                Location = new Point(16, 40),
                Size = new Size(1132, 142),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            radSeparateCorridor = new RadioButton
            {
                Text = "Tách riêng mỗi Cấu kiện thành 1 Corridor độc lập (Khuyên dùng cho BIM & kiểm soát từng phần)",
                Location = new Point(16, 24),
                Size = new Size(620, 24),
                Checked = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            radSeparateCorridor.CheckedChanged += (s, e) => UpdateModeUI();

            radSingleOrExistingCorridor = new RadioButton
            {
                Text = "Gộp tất cả cấu kiện vào 1 Corridor (Tạo mới hoặc thêm Baseline vào Corridor có sẵn):",
                Location = new Point(16, 54),
                Size = new Size(545, 24)
            };
            radSingleOrExistingCorridor.CheckedChanged += (s, e) => UpdateModeUI();

            cmbExistingCorridor = new WinFormsComboBox
            {
                Location = new Point(565, 53),
                Size = new Size(260, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbExistingCorridor.Items.Add("(Tạo Corridor mới từ Tiền tố)");
            foreach (var c in _corridorList)
                cmbExistingCorridor.Items.Add(c.Name);
            cmbExistingCorridor.SelectedIndex = 0;

            btnPickExistingCorridor = new WinFormsButton
            {
                Text = "Pick Corridor...",
                Location = new Point(832, 48),
                Size = new Size(125, 34),
                UseMnemonic = false
            };
            btnPickExistingCorridor.Click += BtnPickExistingCorridor_Click;

            var lblPrefix = new WinFormsLabel
            {
                Text = "Tiền tố tên Corridor:",
                Location = new Point(16, 94),
                Size = new Size(125, 22)
            };
            txtCorridorPrefix = new WinFormsTextBox
            {
                Text = "COR_",
                Location = new Point(142, 91),
                Size = new Size(115, 25)
            };

            var lblCodeSet = new WinFormsLabel
            {
                Text = "Code Set Style:",
                Location = new Point(270, 94),
                Size = new Size(92, 22)
            };
            cmbCodeSetStyle = new WinFormsComboBox
            {
                Location = new Point(364, 91),
                Size = new Size(175, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            foreach (var css in _codeSetStyleList)
                cmbCodeSetStyle.Items.Add(css.Name);
            if (cmbCodeSetStyle.Items.Count > 0)
                cmbCodeSetStyle.SelectedIndex = 0;

            var lblDefSurface = new WinFormsLabel
            {
                Text = "Bề mặt tự nhiên (EG):",
                Location = new Point(552, 94),
                Size = new Size(130, 22)
            };
            cmbDefaultSurface = new WinFormsComboBox
            {
                Location = new Point(684, 91),
                Size = new Size(155, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbDefaultSurface.Items.Add("(Không gán Surface)");
            foreach (var s in _surfaceList)
                cmbDefaultSurface.Items.Add(s.Name);
            int egIdx = _surfaceList.FindIndex(x => x.Name.Contains("EG", StringComparison.OrdinalIgnoreCase) || x.Name.Contains("TN", StringComparison.OrdinalIgnoreCase));
            cmbDefaultSurface.SelectedIndex = egIdx >= 0 ? egIdx + 1 : (_surfaceList.Count > 0 ? 1 : 0);

            var lblFreqT = new WinFormsLabel
            {
                Text = "Bước cọc Thẳng / Cong (m):",
                Location = new Point(850, 94),
                Size = new Size(162, 22)
            };
            numFreqTangent = new NumericUpDown
            {
                Location = new Point(1014, 91),
                Size = new Size(52, 25),
                Minimum = 1m,
                Maximum = 200m,
                DecimalPlaces = 1,
                Value = 10m
            };
            numFreqCurve = new NumericUpDown
            {
                Location = new Point(1070, 91),
                Size = new Size(52, 25),
                Minimum = 0.5m,
                Maximum = 200m,
                DecimalPlaces = 1,
                Value = 5m
            };

            grpGeneral.Controls.AddRange(new Control[]
            {
                radSeparateCorridor, radSingleOrExistingCorridor,
                cmbExistingCorridor, btnPickExistingCorridor,
                lblPrefix, txtCorridorPrefix,
                lblCodeSet, cmbCodeSetStyle,
                lblDefSurface, cmbDefaultSurface,
                lblFreqT, numFreqTangent, numFreqCurve
            });
            this.Controls.Add(grpGeneral);

            // ── GROUP 2: Bảng danh sách các Cấu kiện Corridor ──
            var grpComponents = new GroupBox
            {
                Text = "2. Danh sách các Cấu kiện xây dựng Corridor (Mỗi dòng là 1 cấu kiện độc lập)",
                Location = new Point(16, 190),
                Size = new Size(1132, 475),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            // Thanh nút công cụ thao tác nhanh trên CAD
            btnAutoPopulateFromDrawing = new WinFormsButton
            {
                Text = "Nạp mẫu Cấu kiện từ Bản vẽ hiện hành",
                Location = new Point(14, 24),
                Size = new Size(255, 42),
                BackColor = Color.FromArgb(228, 242, 255),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                UseMnemonic = false
            };
            btnAutoPopulateFromDrawing.Click += BtnAutoPopulateFromDrawing_Click;

            btnAddAlignmentComponent = new WinFormsButton
            {
                Text = "+ Thêm Cấu kiện (Chọn Alignment)",
                Location = new Point(275, 24),
                Size = new Size(220, 42),
                UseMnemonic = false
            };
            btnAddAlignmentComponent.Click += BtnAddAlignmentComponent_Click;

            btnAddFeatureLineComponent = new WinFormsButton
            {
                Text = "+ Thêm Cấu kiện (Chọn FeatureLine)",
                Location = new Point(501, 24),
                Size = new Size(225, 42),
                UseMnemonic = false
            };
            btnAddFeatureLineComponent.Click += BtnAddFeatureLineComponent_Click;

            btnPickBaselineForRow = new WinFormsButton
            {
                Text = "Đổi Đường dẫn dòng chọn",
                Location = new Point(732, 24),
                Size = new Size(175, 42),
                UseMnemonic = false
            };
            btnPickBaselineForRow.Click += BtnPickBaselineForRow_Click;

            btnPickAssemblyForRow = new WinFormsButton
            {
                Text = "Pick Assembly trên CAD",
                Location = new Point(913, 24),
                Size = new Size(155, 42),
                UseMnemonic = false
            };
            btnPickAssemblyForRow.Click += BtnPickAssemblyForRow_Click;

            btnRemoveSelectedRow = new WinFormsButton
            {
                Text = "Xóa",
                Location = new Point(1072, 24),
                Size = new Size(48, 42),
                ForeColor = Color.DarkRed,
                UseMnemonic = false
            };
            btnRemoveSelectedRow.Click += BtnRemoveSelectedRow_Click;

            dgvComponents = new DataGridView
            {
                Location = new Point(14, 74),
                Size = new Size(1106, 342),
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersWidth = 28,
                BackgroundColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
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

            btnPickOffsetTargetsForRow = new WinFormsButton
            {
                Text = "Gán Target Mở rộng / Cao độ (Alignment, FeatureLine, Polyline) cho dòng đang chọn...",
                Location = new Point(14, 422),
                Size = new Size(560, 42),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                UseMnemonic = false
            };
            btnPickOffsetTargetsForRow.Click += BtnPickOffsetTargetsForRow_Click;

            chkAutoRebuild = new CheckBox
            {
                Text = "Tự động Rebuild Corridor ngay sau khi lắp ghép các cấu kiện",
                Location = new Point(600, 432),
                Size = new Size(510, 24),
                Checked = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };

            grpComponents.Controls.AddRange(new Control[]
            {
                btnAutoPopulateFromDrawing,
                btnAddAlignmentComponent,
                btnAddFeatureLineComponent,
                btnPickBaselineForRow,
                btnPickAssemblyForRow,
                btnRemoveSelectedRow,
                dgvComponents,
                btnPickOffsetTargetsForRow,
                chkAutoRebuild
            });
            this.Controls.Add(grpComponents);

            // ── FOOTER: Summary + Execute / Cancel ──
            lblSummary = new WinFormsLabel
            {
                Text = "Sẵn sàng.",
                Location = new Point(16, 676),
                Size = new Size(760, 26),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 100, 60),
                UseMnemonic = false,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            btnExecute = new WinFormsButton
            {
                Text = "Xây Dựng Corridor Theo Cấu Kiện",
                Location = new Point(795, 670),
                Size = new Size(235, 42),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                UseMnemonic = false,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            btnExecute.Click += BtnExecute_Click;

            btnCancel = new WinFormsButton
            {
                Text = "Đóng",
                Location = new Point(1038, 670),
                Size = new Size(110, 42),
                UseMnemonic = false,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            btnCancel.Click += (s, e) =>
            {
                this.FormAccepted = false;
                this.DialogResult = DialogResult.Cancel;
                this.Close();
            };

            this.AcceptButton = btnExecute;
            this.CancelButton = btnCancel;
            this.Controls.AddRange(new Control[] { lblSummary, btnExecute, btnCancel });

            UpdateModeUI();
        }

        private void SetupDataGridViewColumns()
        {
            dgvComponents.Columns.Clear();

            var colEnable = new DataGridViewCheckBoxColumn
            {
                Name = "colEnable",
                HeaderText = "Bật",
                Width = 45
            };

            var colCategory = new DataGridViewComboBoxColumn
            {
                Name = "colCategory",
                HeaderText = "Nhóm Cấu Kiện",
                Width = 155,
                FlatStyle = FlatStyle.Flat
            };
            colCategory.Items.AddRange(ComponentCategories);

            var colName = new DataGridViewTextBoxColumn
            {
                Name = "colName",
                HeaderText = "Tên Cấu Kiện / Corridor",
                Width = 140
            };

            var colSourceType = new DataGridViewComboBoxColumn
            {
                Name = "colSourceType",
                HeaderText = "Loại Đường Dẫn",
                Width = 130,
                FlatStyle = FlatStyle.Flat
            };
            colSourceType.Items.AddRange(SourceTypeNames);

            var colBaseline = new DataGridViewTextBoxColumn
            {
                Name = "colBaseline",
                HeaderText = "Đường Dẫn (Alignment / FeatureLine)",
                Width = 185,
                ReadOnly = true
            };

            var colAssembly = new DataGridViewComboBoxColumn
            {
                Name = "colAssembly",
                HeaderText = "Mặt Cắt Cấu Kiện (Assembly)",
                Width = 175,
                FlatStyle = FlatStyle.Flat
            };
            foreach (var asm in _assemblyList)
                colAssembly.Items.Add(asm.Name);

            var colStartSta = new DataGridViewTextBoxColumn
            {
                Name = "colStartSta",
                HeaderText = "Lý Trình Đầu",
                Width = 85
            };

            var colEndSta = new DataGridViewTextBoxColumn
            {
                Name = "colEndSta",
                HeaderText = "Lý Trình Cuối (0=Hết)",
                Width = 105
            };

            var colFreq = new DataGridViewTextBoxColumn
            {
                Name = "colFreq",
                HeaderText = "Bước Cọc (m)",
                Width = 80
            };

            var colTargets = new DataGridViewTextBoxColumn
            {
                Name = "colTargets",
                HeaderText = "Target Mở Rộng/Cao Độ",
                Width = 140,
                ReadOnly = true
            };

            dgvComponents.Columns.AddRange(new DataGridViewColumn[]
            {
                colEnable, colCategory, colName, colSourceType,
                colBaseline, colAssembly, colStartSta, colEndSta, colFreq, colTargets
            });
        }

        private void UpdateModeUI()
        {
            bool singleMode = radSingleOrExistingCorridor.Checked;
            cmbExistingCorridor.Enabled = singleMode;
            btnPickExistingCorridor.Enabled = singleMode;
            UpdateSummaryLabel();
        }

        private void AddRowToGrid(CorridorComponentRowItem item)
        {
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
        //  TỰ ĐỘNG NẠP CẤU KIỆN TỪ BẢN VẼ HIỆN HÀNH
        // ═══════════════════════════════════════════════════════════════
        private void BtnAutoPopulateFromDrawing_Click(object? sender, EventArgs e)
        {
            if (_assemblyList.Count == 0)
            {
                MessageBox.Show("Bản vẽ hiện tại chưa có Assembly nào.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            dgvComponents.Rows.Clear();

            using (Transaction tr = _db.TransactionManager.StartTransaction())
            {
                CivilDocument cdoc = CivilDocument.GetCivilDocument(_db);

                // 1. Tìm tuyến chính & Assembly lòng đường chính
                var mainAlign = _alignmentList.FirstOrDefault(a =>
                    a.Name.Contains("TUYEN CHINH", StringComparison.OrdinalIgnoreCase) ||
                    !a.Name.Contains("Connected", StringComparison.OrdinalIgnoreCase));

                var mainAsm = _assemblyList.FirstOrDefault(a =>
                    !a.Name.Contains("nhanh", StringComparison.OrdinalIgnoreCase) &&
                    !a.Name.Contains("Phân cách", StringComparison.OrdinalIgnoreCase) &&
                    !a.Name.Contains("via he", StringComparison.OrdinalIgnoreCase));
                if (mainAsm.Id.IsNull && _assemblyList.Count > 0) mainAsm = _assemblyList[0];

                if (!mainAlign.Id.IsNull && !mainAsm.Id.IsNull)
                {
                    AddRowToGrid(new CorridorComponentRowItem
                    {
                        IsEnabled = true,
                        ComponentCategory = "Lòng đường chính",
                        ComponentName = "LongDuong_Chinh",
                        SourceType = ComponentBaselineSourceType.AlignmentAndProfile,
                        BaselineEntityIds = new List<ObjectId> { mainAlign.Id },
                        BaselineDisplayText = $"[1 Tuyến] {mainAlign.Name}",
                        AssemblyId = mainAsm.Id,
                        AssemblyName = mainAsm.Name,
                        StartStation = 0.0,
                        EndStation = 0.0,
                        FrequencyTangent = (double)numFreqTangent.Value,
                        FrequencyCurve = (double)numFreqCurve.Value
                    });
                }

                // 2. Tìm Assembly Dải phân cách & các FeatureLine trong bản vẽ (hoặc từ Corridor 'Phan cach')
                var medianAsm = _assemblyList.FirstOrDefault(a =>
                    a.Name.Contains("Phân cách", StringComparison.OrdinalIgnoreCase) ||
                    a.Name.Contains("Phan cach", StringComparison.OrdinalIgnoreCase));
                if (!medianAsm.Id.IsNull)
                {
                    var flIds = CollectFeatureLinesFromCorridorOrDrawing(tr, cdoc, "Phan cach");
                    AddRowToGrid(new CorridorComponentRowItem
                    {
                        IsEnabled = flIds.Count > 0,
                        ComponentCategory = "Dải phân cách giữa",
                        ComponentName = "DaiPhanCach_Giua",
                        SourceType = ComponentBaselineSourceType.FeatureLine,
                        BaselineEntityIds = flIds,
                        BaselineDisplayText = flIds.Count > 0 ? $"[{flIds.Count} FeatureLine] Dải phân cách" : "(Bấm Đổi Đường dẫn để chọn FeatureLine)",
                        AssemblyId = medianAsm.Id,
                        AssemblyName = medianAsm.Name,
                        StartStation = 0.0,
                        EndStation = 0.0,
                        FrequencyTangent = (double)numFreqTangent.Value,
                        FrequencyCurve = (double)numFreqCurve.Value
                    });
                }

                // 3. Tìm Assembly Vỉa hè trái / phải
                var sidewalkAsm = _assemblyList.FirstOrDefault(a =>
                    a.Name.Contains("via he", StringComparison.OrdinalIgnoreCase) ||
                    a.Name.Contains("vỉa hè", StringComparison.OrdinalIgnoreCase));
                if (!sidewalkAsm.Id.IsNull)
                {
                    AddRowToGrid(new CorridorComponentRowItem
                    {
                        IsEnabled = false,
                        ComponentCategory = "Vỉa hè & Bó vỉa Trái",
                        ComponentName = "ViaHe_Trai",
                        SourceType = ComponentBaselineSourceType.FeatureLine,
                        BaselineEntityIds = new List<ObjectId>(),
                        BaselineDisplayText = "(Bấm Đổi Đường dẫn để chọn FeatureLine/Alignment)",
                        AssemblyId = sidewalkAsm.Id,
                        AssemblyName = sidewalkAsm.Name,
                        StartStation = 0.0,
                        EndStation = 0.0,
                        FrequencyTangent = (double)numFreqTangent.Value,
                        FrequencyCurve = (double)numFreqCurve.Value
                    });
                }

                // 4. Tìm các Connected Alignment nút giao rẽ phải & Assembly rẽ phải
                var turnRightAsm = _assemblyList.FirstOrDefault(a =>
                    a.Name.Contains("re phai", StringComparison.OrdinalIgnoreCase) ||
                    a.Name.Contains("rẽ phải", StringComparison.OrdinalIgnoreCase));
                var connectedAlignIds = _alignmentList
                    .Where(a => a.Name.Contains("Connected Alignment", StringComparison.OrdinalIgnoreCase))
                    .Select(a => a.Id)
                    .ToList();

                if (!turnRightAsm.Id.IsNull && connectedAlignIds.Count > 0)
                {
                    AddRowToGrid(new CorridorComponentRowItem
                    {
                        IsEnabled = true,
                        ComponentCategory = "Nhánh nút giao rẽ phải",
                        ComponentName = "NutGiao_RePhai",
                        SourceType = ComponentBaselineSourceType.AlignmentAndProfile,
                        BaselineEntityIds = connectedAlignIds,
                        BaselineDisplayText = $"[{connectedAlignIds.Count} Tuyến] Connected Alignments",
                        AssemblyId = turnRightAsm.Id,
                        AssemblyName = turnRightAsm.Name,
                        StartStation = 0.0,
                        EndStation = 0.0,
                        FrequencyTangent = 2.0,
                        FrequencyCurve = 1.0
                    });
                }

                tr.Commit();
            }

            UpdateSummaryLabel();
        }

        private List<ObjectId> CollectFeatureLinesFromCorridorOrDrawing(Transaction tr, CivilDocument cdoc, string corridorNameHint)
        {
            var result = new List<ObjectId>();
            foreach (ObjectId corrId in cdoc.CorridorCollection)
            {
                if (tr.GetObject(corrId, OpenMode.ForRead) is Corridor corr &&
                    corr.Name.Contains(corridorNameHint, StringComparison.OrdinalIgnoreCase))
                {
                    foreach (Baseline bl in corr.Baselines)
                    {
                        if (bl.IsFeatureLineBased() && !bl.FeatureLineId.IsNull && bl.FeatureLineId.IsValid && !bl.FeatureLineId.IsErased)
                        {
                            if (!result.Contains(bl.FeatureLineId))
                                result.Add(bl.FeatureLineId);
                        }
                    }
                }
            }
            return result;
        }

        // ═══════════════════════════════════════════════════════════════
        //  TƯƠNG TÁC HAI CHIỀU VỚI AUTOCAD CANVAS (StartUserInteraction)
        // ═══════════════════════════════════════════════════════════════
        private void BtnPickExistingCorridor_Click(object? sender, EventArgs e)
        {
            using (EditorUserInteraction ui = _ed.StartUserInteraction(this))
            {
                var peo = new PromptEntityOptions("\nChọn Corridor hiện có trên bản vẽ: ");
                peo.SetRejectMessage("\nĐối tượng được chọn phải là Corridor.");
                peo.AddAllowedClass(typeof(Corridor), true);

                PromptEntityResult per = _ed.GetEntity(peo);
                if (per.Status == PromptStatus.OK && !per.ObjectId.IsNull)
                {
                    int idx = _corridorList.FindIndex(c => c.Id == per.ObjectId);
                    if (idx >= 0)
                        cmbExistingCorridor.SelectedIndex = idx + 1;
                }
            }
        }

        private void BtnAddAlignmentComponent_Click(object? sender, EventArgs e)
        {
            var pickedIds = PickEntitiesOnCanvas(
                "\nChọn một hoặc nhiều Alignment cho cấu kiện mới: ",
                typeof(Alignment));

            if (pickedIds.Count == 0) return;

            string display = DescribePickedEntities(pickedIds, "Tuyến");
            var defaultAsm = _assemblyList.FirstOrDefault();

            var newItem = new CorridorComponentRowItem
            {
                IsEnabled = true,
                ComponentCategory = "Lòng đường chính",
                ComponentName = $"CauKien_Tuyen_{dgvComponents.Rows.Count + 1}",
                SourceType = ComponentBaselineSourceType.AlignmentAndProfile,
                BaselineEntityIds = pickedIds,
                BaselineDisplayText = display,
                AssemblyId = defaultAsm.Id,
                AssemblyName = defaultAsm.Name ?? "",
                StartStation = 0.0,
                EndStation = 0.0,
                FrequencyTangent = (double)numFreqTangent.Value,
                FrequencyCurve = (double)numFreqCurve.Value
            };

            AddRowToGrid(newItem);
            UpdateSummaryLabel();
        }

        private void BtnAddFeatureLineComponent_Click(object? sender, EventArgs e)
        {
            var pickedIds = PickEntitiesOnCanvas(
                "\nChọn một hoặc nhiều Feature Line cho cấu kiện mới (VD: Dải phân cách, Vỉa hè, Rãnh biên): ",
                typeof(FeatureLine));

            if (pickedIds.Count == 0) return;

            string display = DescribePickedEntities(pickedIds, "FeatureLine");
            var defaultAsm = _assemblyList.FirstOrDefault(a =>
                a.Name.Contains("Phân cách", StringComparison.OrdinalIgnoreCase) ||
                a.Name.Contains("via he", StringComparison.OrdinalIgnoreCase));
            if (defaultAsm.Id.IsNull && _assemblyList.Count > 0)
                defaultAsm = _assemblyList[0];

            var newItem = new CorridorComponentRowItem
            {
                IsEnabled = true,
                ComponentCategory = "Dải phân cách giữa",
                ComponentName = $"CauKien_FL_{dgvComponents.Rows.Count + 1}",
                SourceType = ComponentBaselineSourceType.FeatureLine,
                BaselineEntityIds = pickedIds,
                BaselineDisplayText = display,
                AssemblyId = defaultAsm.Id,
                AssemblyName = defaultAsm.Name ?? "",
                StartStation = 0.0,
                EndStation = 0.0,
                FrequencyTangent = (double)numFreqTangent.Value,
                FrequencyCurve = (double)numFreqCurve.Value
            };

            AddRowToGrid(newItem);
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
                var ids = PickEntitiesOnCanvas("\nChọn các Feature Line làm đường dẫn cho cấu kiện: ", typeof(FeatureLine));
                if (ids.Count > 0)
                {
                    item.BaselineEntityIds = ids;
                    item.BaselineDisplayText = DescribePickedEntities(ids, "FeatureLine");
                    dgvComponents.Rows[rowIdx].Cells["colBaseline"].Value = item.BaselineDisplayText;
                }
            }
            else
            {
                var ids = PickEntitiesOnCanvas("\nChọn các Alignment làm đường dẫn cho cấu kiện: ", typeof(Alignment));
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
                                result.Add(so.ObjectId);
                            }
                        }
                        tr.Commit();
                    }
                }
            }
            return result;
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

            string modeText = radSeparateCorridor.Checked
                ? $"Tách riêng {enabledCount} Corridor theo từng cấu kiện"
                : (cmbExistingCorridor.SelectedIndex > 0
                    ? $"Gộp vào Corridor '{cmbExistingCorridor.SelectedItem}'"
                    : $"Tạo 1 Corridor tổng '{txtCorridorPrefix.Text.Trim()}Modular'");

            lblSummary.Text = $"Tổng hợp: {enabledCount}/{total} cấu kiện đang bật ({totalBaselines} đường dẫn Baseline) — Chế độ: {modeText}";
        }

        // ═══════════════════════════════════════════════════════════════
        //  PERSISTENT STATE SAVE & RESTORE
        // ═══════════════════════════════════════════════════════════════
        private void SaveCurrentSettings()
        {
            _lastBuildMode = radSeparateCorridor.Checked
                ? CorridorComponentBuildMode.SeparateCorridorPerComponent
                : CorridorComponentBuildMode.SingleOrExistingCorridor;

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

            if (this.WindowState == FormWindowState.Normal)
                _lastFormSize = this.Size;

            _savedRows.Clear();
            for (int i = 0; i < dgvComponents.Rows.Count; i++)
            {
                SyncGridRowToModel(i);
                if (dgvComponents.Rows[i].Tag is CorridorComponentRowItem item)
                {
                    _savedRows.Add(item);
                }
            }
        }

        private void RestoreLastSettings()
        {
            radSeparateCorridor.Checked = (_lastBuildMode == CorridorComponentBuildMode.SeparateCorridorPerComponent);
            radSingleOrExistingCorridor.Checked = (_lastBuildMode == CorridorComponentBuildMode.SingleOrExistingCorridor);

            if (!_lastExistingCorridorId.IsNull && _lastExistingCorridorId.IsValid && !_lastExistingCorridorId.IsErased)
            {
                int cIdx = _corridorList.FindIndex(c => c.Id == _lastExistingCorridorId);
                if (cIdx >= 0) cmbExistingCorridor.SelectedIndex = cIdx + 1;
            }

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

            // Khôi phục các dòng cấu kiện đã lưu nếu các ObjectId còn hợp lệ
            if (_savedRows.Count > 0)
            {
                dgvComponents.Rows.Clear();
                foreach (var saved in _savedRows)
                {
                    var validBaselines = saved.BaselineEntityIds
                        .Where(id => !id.IsNull && id.IsValid && !id.IsErased && id.Database == _db)
                        .ToList();
                    var validTargets = saved.OffsetElevationTargetIds
                        .Where(id => !id.IsNull && id.IsValid && !id.IsErased && id.Database == _db)
                        .ToList();

                    saved.BaselineEntityIds = validBaselines;
                    saved.OffsetElevationTargetIds = validTargets;
                    if (validBaselines.Count == 0)
                        saved.BaselineDisplayText = "(Chưa chọn)";

                    AddRowToGrid(saved);
                }
            }
            else
            {
                // Lần đầu mở trong phiên: tự động quét bản vẽ để gợi ý bộ cấu kiện
                BtnAutoPopulateFromDrawing_Click(null, EventArgs.Empty);
            }
        }

        private void BtnExecute_Click(object? sender, EventArgs e)
        {
            SaveCurrentSettings();

            var activeRows = _savedRows
                .Where(r => r.IsEnabled && r.BaselineEntityIds.Count > 0 && !r.AssemblyId.IsNull)
                .ToList();

            if (activeRows.Count == 0)
            {
                MessageBox.Show(
                    "Vui lòng bật ít nhất 1 cấu kiện có đầy đủ Đường dẫn (Alignment / Feature Line) và Mặt cắt (Assembly).",
                    "Thiếu thông tin cấu kiện",
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
            this.ComponentRows = activeRows;
            this.FormAccepted = true;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;

using Civil3DCsharp;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsFont = System.Drawing.Font;
using FlowDirection = System.Windows.Forms.FlowDirection;
using C3DAssembly = Autodesk.Civil.DatabaseServices.Assembly;
using C3DSubassembly = Autodesk.Civil.DatabaseServices.Subassembly;

namespace MyFirstProject.Civil_Tool_2
{
    public class QuanLySubassemblyDotNetForm : Form
    {
        private readonly Database _db;
        private readonly Editor _ed;

        // ==================== PERSISTENT STATE (GHI NHỚ LỰA CHỌN LẦN TRƯỚC) ====================
        private static ObjectId _lastSelectedAssemblyId = ObjectId.Null;
        private static ObjectId _lastSelectedSubassemblyId = ObjectId.Null;
        private static int _lastSourceFilterIndex = 0; // 0: Tất cả, 1: C# Tự Viết, 2: Stock Civil 3D
        private static string _lastSelectedClassFullName = "Civil3DCsharp.CustomBasicLaneCS";
        private static string _lastNewSubassemblyName = "SA_CSharp_01";
        private static bool _lastAttachToAssembly = true;
        private static bool _lastRebuildCorridors = true;
        private static bool _lastForceUpdateAll = true;
        private static string _lastSearchKeyword = "";
        private static Size _lastFormSize = Size.Empty;
        private static readonly Dictionary<string, string> _lastCustomParamValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // ==================== DỮ LIỆU RUNTIME ====================
        private List<DotNetSubassemblyClassInfo> _allAvailableClasses = new List<DotNetSubassemblyClassInfo>();
        private List<DrawingSubassemblyInfo> _drawingSubassemblies = new List<DrawingSubassemblyInfo>();
        private readonly List<(ObjectId Id, string Name)> _drawingAssemblies = new List<(ObjectId Id, string Name)>();
        private Point3d _pickedInsertPoint = Point3d.Origin;

        // ==================== UI CONTROLS ====================
        private SplitContainer _mainSplit = null!;

        // Panel Trái: Thư viện Class .NET (C# tự viết & C3DStockSubassemblies) + Tạo mới
        private ComboBox _cboSourceFilter = null!;
        private TextBox _txtSearchClass = null!;
        private ListBox _lstDotNetClasses = null!;
        private TextBox _txtClassDetails = null!;
        private TextBox _txtNewSaName = null!;
        private CheckBox _chkAttachToAssembly = null!;
        private ComboBox _cboTargetAssembly = null!;
        private Button _btnPickAssembly = null!;
        private DataGridView _dgvCreateParams = null!;
        private Button _btnCreateSubassembly = null!;
        private Button _btnExportAtcCatalog = null!;

        // Panel Phải: Danh sách Subassembly trong bản vẽ hiện hành + Chỉnh sửa thông số
        private DataGridView _dgvDrawingSubassemblies = null!;
        private Button _btnPickSubassemblyOnScreen = null!;
        private Button _btnRefreshList = null!;
        private Button _btnUpdateAllDotNet = null!;
        private Button _btnPackageZip = null!;
        private CheckBox _chkForceUpdateAll = null!;
        private CheckBox _chkRebuildCorridor = null!;

        private DataGridView _dgvEditExistingParams = null!;
        private TextBox _txtExistingDllPath = null!;
        private TextBox _txtExistingClassName = null!;
        private Button _btnAssignSelectedClassToExisting = null!;
        private Button _btnApplyExistingChanges = null!;
        private WinFormsLabel _lblStatus = null!;

        public QuanLySubassemblyDotNetForm(Database db, Editor ed)
        {
            _db = db;
            _ed = ed;

            InitializeComponent();
            LoadDrawingAssemblies();
            LoadAvailableSubassemblyClasses();
            RefreshDrawingSubassembliesGrid();
            RestoreLastSettings();
        }

        private void InitializeComponent()
        {
            this.Text = "QUẢN LÝ & KHỞI TẠO SUBASSEMBLY .NET (Autodesk.Civil.Runtime & C3DStockSubassemblies)";
            this.Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Regular);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Size = (_lastFormSize != Size.Empty && _lastFormSize.Width >= 1000 && _lastFormSize.Height >= 640)
                ? _lastFormSize
                : new Size(1220, 740);
            this.MinimumSize = new Size(1020, 640);

            var rootLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(8)
            };
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));

            _mainSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 500,
                SplitterWidth = 6
            };

            BuildLeftPanel(_mainSplit.Panel1);
            BuildRightPanel(_mainSplit.Panel2);

            rootLayout.Controls.Add(_mainSplit, 0, 0);

            // Thanh trạng thái
            _lblStatus = new WinFormsLabel
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(0, 90, 160),
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold),
                Text = "Sẵn sàng. Đã nạp thư viện Autodesk.Civil.Runtime & C3DStockSubassemblies.dll."
            };
            rootLayout.Controls.Add(_lblStatus, 0, 1);

            // Hàng nút dưới cùng
            var bottomFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 2, 0, 0)
            };

            var btnClose = new Button
            {
                Text = "Đóng (ESC)",
                Width = 110,
                Height = 32,
                DialogResult = DialogResult.Cancel
            };
            btnClose.Click += (s, e) => this.Close();
            this.CancelButton = btnClose;

            bottomFlow.Controls.Add(btnClose);
            rootLayout.Controls.Add(bottomFlow, 0, 2);

            this.Controls.Add(rootLayout);
            this.FormClosing += (s, e) => SaveCurrentSettings();
        }

        private void BuildLeftPanel(SplitterPanel panel)
        {
            var grpLibrary = new GroupBox
            {
                Text = "1. Kho thư viện Subassembly .NET (C# Tự Viết && 118+ C3DStockSubassemblies)",
                Dock = DockStyle.Fill,
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold),
                Padding = new Padding(8)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 6,
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Regular)
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F)); // Nguồn lọc
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F)); // Tìm kiếm
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 42F));  // ListBox class
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F)); // Chi tiết class
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 58F));  // Thông số khởi tạo
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F)); // Nút tạo & Xuất .atc

            // Row 0: Nguồn thư viện
            layout.Controls.Add(new WinFormsLabel { Text = "Nguồn thư viện:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            _cboSourceFilter = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            _cboSourceFilter.Items.AddRange(new object[]
            {
                "Tất cả (C# Tự Viết + Civil 3D Stock)",
                "Chỉ hiện Subassembly C# Tự Viết (Dự án hiện tại)",
                "Chỉ hiện Civil 3D Stock (C3DStockSubassemblies.dll)"
            });
            _cboSourceFilter.SelectedIndex = 0;
            _cboSourceFilter.SelectedIndexChanged += (s, e) => FilterClassList();
            layout.Controls.Add(_cboSourceFilter, 1, 0);

            // Row 1: Tìm kiếm
            layout.Controls.Add(new WinFormsLabel { Text = "Tìm nhanh Class:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
            _txtSearchClass = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "Nhập tên cấu kiện (VD: BasicLane, Curb, Ditch, Daylight...)" };
            _txtSearchClass.TextChanged += (s, e) => FilterClassList();
            layout.Controls.Add(_txtSearchClass, 1, 1);

            // Row 2: ListBox danh sách Class
            _lstDotNetClasses = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
            _lstDotNetClasses.SelectedIndexChanged += OnSelectedDotNetClassChanged;
            layout.SetColumnSpan(_lstDotNetClasses, 2);
            layout.Controls.Add(_lstDotNetClasses, 0, 2);

            // Row 3: Thông tin chi tiết Class
            _txtClassDetails = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                BackColor = Color.FromArgb(245, 248, 252),
                ScrollBars = ScrollBars.Vertical
            };
            layout.SetColumnSpan(_txtClassDetails, 2);
            layout.Controls.Add(_txtClassDetails, 0, 3);

            // Row 4: Group khởi tạo & bảng tham số
            var grpCreateConfig = new GroupBox
            {
                Text = "Thông số khởi tạo Subassembly mới",
                Dock = DockStyle.Fill,
                Padding = new Padding(6)
            };
            var createLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 3
            };
            createLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145F));
            createLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            createLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95F));
            createLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            createLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            createLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            createLayout.Controls.Add(new WinFormsLabel { Text = "Tên Subassembly:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            _txtNewSaName = new TextBox { Dock = DockStyle.Fill, Text = "SA_CSharp_01" };
            createLayout.SetColumnSpan(_txtNewSaName, 2);
            createLayout.Controls.Add(_txtNewSaName, 1, 0);

            _chkAttachToAssembly = new CheckBox { Text = "Gắn vào Assembly:", Checked = true, Dock = DockStyle.Fill };
            _chkAttachToAssembly.CheckedChanged += (s, e) =>
            {
                _cboTargetAssembly.Enabled = _chkAttachToAssembly.Checked;
                _btnPickAssembly.Enabled = _chkAttachToAssembly.Checked;
            };
            createLayout.Controls.Add(_chkAttachToAssembly, 0, 1);

            _cboTargetAssembly = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            createLayout.Controls.Add(_cboTargetAssembly, 1, 1);

            _btnPickAssembly = new Button { Text = "Chọn CAD...", Dock = DockStyle.Fill };
            _btnPickAssembly.Click += OnPickAssemblyClick;
            createLayout.Controls.Add(_btnPickAssembly, 2, 1);

            _dgvCreateParams = CreateParameterGrid();
            createLayout.SetColumnSpan(_dgvCreateParams, 3);
            createLayout.Controls.Add(_dgvCreateParams, 0, 2);

            grpCreateConfig.Controls.Add(createLayout);
            layout.SetColumnSpan(grpCreateConfig, 2);
            layout.Controls.Add(grpCreateConfig, 0, 4);

            // Row 5: Các nút tạo mới & Xuất Catalog .atc
            var btnPanelLeft = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1
            };
            btnPanelLeft.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58F));
            btnPanelLeft.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42F));

            _btnCreateSubassembly = new Button
            {
                Text = "➕ Tạo && Đưa Subassembly .NET vào Bản vẽ",
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold)
            };
            _btnCreateSubassembly.Click += OnCreateSubassemblyClick;

            _btnExportAtcCatalog = new Button
            {
                Text = "📄 Xuất Catalog (.atc) cho Tool Palette",
                Dock = DockStyle.Fill
            };
            _btnExportAtcCatalog.Click += OnExportAtcCatalogClick;

            btnPanelLeft.Controls.Add(_btnCreateSubassembly, 0, 0);
            btnPanelLeft.Controls.Add(_btnExportAtcCatalog, 1, 0);

            layout.SetColumnSpan(btnPanelLeft, 2);
            layout.Controls.Add(btnPanelLeft, 0, 5);

            grpLibrary.Controls.Add(layout);
            panel.Controls.Add(grpLibrary);
        }

        private void BuildRightPanel(SplitterPanel panel)
        {
            var grpDrawing = new GroupBox
            {
                Text = "2. Danh sách Subassembly trong Bản vẽ hiện hành && Chỉnh sửa .NET Runtime",
                Dock = DockStyle.Fill,
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold),
                Padding = new Padding(8)
            };

            var rightLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Regular)
            };
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F)); // Thanh công cụ trên
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 48F));  // Bảng Subassembly trong bản vẽ
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 52F));  // Chỉnh sửa tham số & GeometryGenerator
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F)); // Tùy chọn Rebuild

            // Row 0: Thanh nút công cụ
            var topToolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };

            _btnPickSubassemblyOnScreen = new Button { Text = "🎯 Chọn Subassembly trên CAD", Width = 185, Height = 28 };
            _btnPickSubassemblyOnScreen.Click += OnPickSubassemblyOnScreenClick;

            _btnRefreshList = new Button { Text = "🔄 Làm mới", Width = 95, Height = 28 };
            _btnRefreshList.Click += (s, e) => RefreshDrawingSubassembliesGrid();

            _btnUpdateAllDotNet = new Button
            {
                Text = "⚡ Cập nhật (Run) toàn bộ SA .NET",
                Width = 205,
                Height = 28,
                BackColor = Color.FromArgb(225, 240, 255)
            };
            _btnUpdateAllDotNet.Click += OnUpdateAllDotNetClick;

            _btnPackageZip = new Button { Text = "📦 Đóng gói DWG + DLL (.zip)", Width = 175, Height = 28 };
            _btnPackageZip.Click += OnPackageZipClick;

            topToolbar.Controls.AddRange(new Control[]
            {
                _btnPickSubassemblyOnScreen,
                _btnRefreshList,
                _btnUpdateAllDotNet,
                _btnPackageZip
            });
            rightLayout.Controls.Add(topToolbar, 0, 0);

            // Row 1: Bảng danh sách Subassembly trong bản vẽ
            _dgvDrawingSubassemblies = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                RowHeadersVisible = false
            };
            _dgvDrawingSubassemblies.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColName", HeaderText = "Tên Subassembly", FillWeight = 24 });
            _dgvDrawingSubassemblies.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColAsm", HeaderText = "Assembly", FillWeight = 22 });
            _dgvDrawingSubassemblies.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColClass", HeaderText = "Class .NET (MacroOrClassName)", FillWeight = 28 });
            _dgvDrawingSubassemblies.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColStatus", HeaderText = "Trạng thái", FillWeight = 18 });
            _dgvDrawingSubassemblies.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColFile", HeaderText = "File DLL", FillWeight = 22 });
            _dgvDrawingSubassemblies.SelectionChanged += OnDrawingSubassemblySelectionChanged;

            rightLayout.Controls.Add(_dgvDrawingSubassemblies, 0, 1);

            // Row 2: Group chỉnh sửa Subassembly đang chọn
            var grpEditSelected = new GroupBox
            {
                Text = "Chỉnh sửa Tham số (ParamsLong / ParamsDouble / ParamsString) && Liên kết Class .NET",
                Dock = DockStyle.Fill,
                Padding = new Padding(6)
            };

            var editLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 4
            };
            editLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105F));
            editLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            editLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 185F));
            editLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            editLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            editLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            editLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));

            editLayout.Controls.Add(new WinFormsLabel { Text = "Đường dẫn DLL:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            _txtExistingDllPath = new TextBox { Dock = DockStyle.Fill };
            editLayout.Controls.Add(_txtExistingDllPath, 1, 0);

            _btnAssignSelectedClassToExisting = new Button
            {
                Text = "⬅ Gán Class đang chọn ở cột trái",
                Dock = DockStyle.Fill
            };
            _btnAssignSelectedClassToExisting.Click += OnAssignSelectedClassToExistingClick;
            editLayout.SetRowSpan(_btnAssignSelectedClassToExisting, 2);
            editLayout.Controls.Add(_btnAssignSelectedClassToExisting, 2, 0);

            editLayout.Controls.Add(new WinFormsLabel { Text = "Tên Class .NET:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
            _txtExistingClassName = new TextBox { Dock = DockStyle.Fill };
            editLayout.Controls.Add(_txtExistingClassName, 1, 1);

            _dgvEditExistingParams = CreateParameterGrid();
            editLayout.SetColumnSpan(_dgvEditExistingParams, 3);
            editLayout.Controls.Add(_dgvEditExistingParams, 0, 2);

            _btnApplyExistingChanges = new Button
            {
                Text = "💾 Áp dụng Tham số && Chạy lại Subassembly.Run()",
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(40, 140, 60),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold)
            };
            _btnApplyExistingChanges.Click += OnApplyExistingChangesClick;
            editLayout.SetColumnSpan(_btnApplyExistingChanges, 3);
            editLayout.Controls.Add(_btnApplyExistingChanges, 0, 3);

            grpEditSelected.Controls.Add(editLayout);
            rightLayout.Controls.Add(grpEditSelected, 0, 2);

            // Row 3: Tùy chọn dưới
            var optPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight
            };
            _chkRebuildCorridor = new CheckBox
            {
                Text = "Tự động Rebuild Corridor sau khi cập nhật Subassembly",
                Checked = true,
                AutoSize = true
            };
            _chkForceUpdateAll = new CheckBox
            {
                Text = "Chạy lại (Force Run) cả các Subassembly đang Up-To-Date",
                Checked = true,
                AutoSize = true
            };
            optPanel.Controls.Add(_chkRebuildCorridor);
            optPanel.Controls.Add(_chkForceUpdateAll);
            rightLayout.Controls.Add(optPanel, 0, 3);

            grpDrawing.Controls.Add(rightLayout);
            panel.Controls.Add(grpDrawing);
        }

        private static DataGridView CreateParameterGrid()
        {
            var dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                RowHeadersVisible = false
            };

            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColKey", HeaderText = "Mã Tham Số (Key)", FillWeight = 28 });
            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColDisp", HeaderText = "Mô tả / Tên hiển thị", FillWeight = 34 });

            var colType = new DataGridViewComboBoxColumn
            {
                Name = "ColType",
                HeaderText = "Kiểu (Type)",
                FillWeight = 16
            };
            colType.Items.AddRange("Double", "Long", "String");
            dgv.Columns.Add(colType);

            dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColVal", HeaderText = "Giá trị (Value)", FillWeight = 22 });
            return dgv;
        }

        // ==================== NẠP DỮ LIỆU & LỌC DANH SÁCH ====================

        private void LoadDrawingAssemblies()
        {
            _drawingAssemblies.Clear();
            _cboTargetAssembly.Items.Clear();

            using (Transaction tr = _db.TransactionManager.StartTransaction())
            {
                var civilDoc = CivilDocument.GetCivilDocument(_db);
                foreach (ObjectId asmId in civilDoc.AssemblyCollection)
                {
                    if (tr.GetObject(asmId, OpenMode.ForRead) is C3DAssembly asm)
                    {
                        _drawingAssemblies.Add((asmId, asm.Name));
                        _cboTargetAssembly.Items.Add(asm.Name);
                    }
                }
                tr.Commit();
            }

            if (_cboTargetAssembly.Items.Count > 0)
            {
                _cboTargetAssembly.SelectedIndex = 0;
            }
            else
            {
                _chkAttachToAssembly.Checked = false;
            }
        }

        private void LoadAvailableSubassemblyClasses()
        {
            _allAvailableClasses = SubassemblyDotNetEngine.DiscoverAvailableSubassemblyClasses();
            FilterClassList();
        }

        private void FilterClassList()
        {
            string kw = (_txtSearchClass.Text ?? "").Trim();
            int filterIdx = _cboSourceFilter.SelectedIndex;

            var filtered = _allAvailableClasses.Where(c =>
            {
                if (filterIdx == 1 && !c.DisplayGroup.StartsWith("C#", StringComparison.OrdinalIgnoreCase))
                    return false;
                if (filterIdx == 2 && !c.DisplayGroup.StartsWith("Civil 3D Stock", StringComparison.OrdinalIgnoreCase))
                    return false;

                if (!string.IsNullOrEmpty(kw))
                {
                    return c.ClassName.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                           c.Description.Contains(kw, StringComparison.OrdinalIgnoreCase);
                }
                return true;
            }).ToList();

            _lstDotNetClasses.BeginUpdate();
            _lstDotNetClasses.Items.Clear();
            foreach (var item in filtered)
            {
                _lstDotNetClasses.Items.Add(item);
            }
            _lstDotNetClasses.EndUpdate();

            if (_lstDotNetClasses.Items.Count > 0 && _lstDotNetClasses.SelectedIndex < 0)
            {
                _lstDotNetClasses.SelectedIndex = 0;
            }
        }

        private void OnSelectedDotNetClassChanged(object? sender, EventArgs e)
        {
            if (_lstDotNetClasses.SelectedItem is not DotNetSubassemblyClassInfo info)
                return;

            _txtClassDetails.Text =
                $"Class: {info.FullClassName}\r\n" +
                $"DLL: {info.DllPath}\r\n" +
                $"Mô tả: {info.Description}";

            if (string.IsNullOrWhiteSpace(_txtNewSaName.Text) || _txtNewSaName.Text.StartsWith("SA_", StringComparison.OrdinalIgnoreCase))
            {
                _txtNewSaName.Text = $"SA_{info.ClassName}";
            }

            PopulateParameterGrid(_dgvCreateParams, info.DefaultParams, true);
        }

        private void PopulateParameterGrid(DataGridView dgv, List<SubassemblyParamItem> items, bool restoreSavedValues)
        {
            dgv.Rows.Clear();
            if (items == null) return;

            foreach (var p in items)
            {
                string val = p.Value;
                if (restoreSavedValues && _lastCustomParamValues.TryGetValue(p.Key, out string? savedVal))
                {
                    val = savedVal;
                }
                string dt = (p.DataType == "Long" || p.DataType == "String") ? p.DataType : "Double";
                dgv.Rows.Add(p.Key, p.DisplayName, dt, val);
            }
        }

        private static List<SubassemblyParamItem> ReadParameterGrid(DataGridView dgv)
        {
            var list = new List<SubassemblyParamItem>();
            foreach (DataGridViewRow row in dgv.Rows)
            {
                if (row.IsNewRow) continue;
                string key = Convert.ToString(row.Cells["ColKey"].Value)?.Trim() ?? "";
                if (string.IsNullOrEmpty(key)) continue;

                string disp = Convert.ToString(row.Cells["ColDisp"].Value)?.Trim() ?? key;
                string type = Convert.ToString(row.Cells["ColType"].Value)?.Trim() ?? "Double";
                string val = Convert.ToString(row.Cells["ColVal"].Value)?.Trim() ?? "0";

                list.Add(new SubassemblyParamItem
                {
                    Key = key,
                    DisplayName = disp,
                    DataType = type,
                    Value = val
                });
            }
            return list;
        }

        public void RefreshDrawingSubassembliesGrid()
        {
            _drawingSubassemblies = SubassemblyDotNetEngine.GetDrawingSubassemblies(_db);
            _dgvDrawingSubassemblies.Rows.Clear();

            foreach (var sa in _drawingSubassemblies)
            {
                int rowIdx = _dgvDrawingSubassemblies.Rows.Add(
                    sa.Name,
                    sa.AssemblyName,
                    sa.ClassName,
                    sa.StatusText,
                    Path.GetFileName(sa.AssemblyDllPath));
                _dgvDrawingSubassemblies.Rows[rowIdx].Tag = sa;
            }

            _lblStatus.Text = $"Bản vẽ hiện có {_drawingSubassemblies.Count} Subassembly | Kho thư viện có {_allAvailableClasses.Count} Class .NET.";
        }

        private void OnDrawingSubassemblySelectionChanged(object? sender, EventArgs e)
        {
            if (_dgvDrawingSubassemblies.SelectedRows.Count == 0 ||
                _dgvDrawingSubassemblies.SelectedRows[0].Tag is not DrawingSubassemblyInfo info)
            {
                return;
            }

            _lastSelectedSubassemblyId = info.SubassemblyId;
            _txtExistingDllPath.Text = info.AssemblyDllPath;
            _txtExistingClassName.Text = info.ClassName;
            PopulateParameterGrid(_dgvEditExistingParams, info.Parameters, false);
        }

        // ==================== CÁC SỰ KIỆN NÚT BẤM TƯƠNG TÁC ====================

        private void OnPickAssemblyClick(object? sender, EventArgs e)
        {
            using (EditorUserInteraction ui = _ed.StartUserInteraction(this))
            {
                var peo = new PromptEntityOptions("\nChọn Assembly trên bản vẽ: ");
                peo.SetRejectMessage("\nĐối tượng chọn phải là Civil 3D Assembly.");
                peo.AddAllowedClass(typeof(C3DAssembly), false);

                var per = _ed.GetEntity(peo);
                if (per.Status == PromptStatus.OK)
                {
                    _lastSelectedAssemblyId = per.ObjectId;
                    int idx = _drawingAssemblies.FindIndex(a => a.Id == per.ObjectId);
                    if (idx >= 0)
                    {
                        _cboTargetAssembly.SelectedIndex = idx;
                        _chkAttachToAssembly.Checked = true;
                    }
                }
            }
        }

        private void OnPickSubassemblyOnScreenClick(object? sender, EventArgs e)
        {
            using (EditorUserInteraction ui = _ed.StartUserInteraction(this))
            {
                var peo = new PromptEntityOptions("\nChọn Subassembly trên màn hình CAD: ");
                peo.SetRejectMessage("\nĐối tượng chọn phải là Civil 3D Subassembly.");
                peo.AddAllowedClass(typeof(C3DSubassembly), false);

                var per = _ed.GetEntity(peo);
                if (per.Status == PromptStatus.OK)
                {
                    _lastSelectedSubassemblyId = per.ObjectId;
                    SelectSubassemblyRowById(per.ObjectId);
                }
            }
        }

        private void SelectSubassemblyRowById(ObjectId saId)
        {
            foreach (DataGridViewRow row in _dgvDrawingSubassemblies.Rows)
            {
                if (row.Tag is DrawingSubassemblyInfo info && info.SubassemblyId == saId)
                {
                    row.Selected = true;
                    _dgvDrawingSubassemblies.FirstDisplayedScrollingRowIndex = row.Index;
                    break;
                }
            }
        }

        private void OnCreateSubassemblyClick(object? sender, EventArgs e)
        {
            if (_lstDotNetClasses.SelectedItem is not DotNetSubassemblyClassInfo clsInfo)
            {
                MessageBox.Show("Vui lòng chọn một Class Subassembly .NET ở danh sách bên trái.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string saName = (_txtNewSaName.Text ?? "").Trim();
            if (string.IsNullOrEmpty(saName))
                saName = $"SA_{clsInfo.ClassName}";

            ObjectId targetAsmId = ObjectId.Null;
            if (_chkAttachToAssembly.Checked && _cboTargetAssembly.SelectedIndex >= 0 && _cboTargetAssembly.SelectedIndex < _drawingAssemblies.Count)
            {
                targetAsmId = _drawingAssemblies[_cboTargetAssembly.SelectedIndex].Id;
            }
            else
            {
                // Nếu không gắn vào Assembly thì cho người dùng chọn điểm đặt trên CAD
                using (EditorUserInteraction ui = _ed.StartUserInteraction(this))
                {
                    var ppo = new PromptPointOptions($"\nChọn vị trí đặt Subassembly '{saName}' trên bản vẽ: ");
                    var ppr = _ed.GetPoint(ppo);
                    if (ppr.Status != PromptStatus.OK)
                        return;
                    _pickedInsertPoint = ppr.Value;
                }
            }

            var paramList = ReadParameterGrid(_dgvCreateParams);
            SaveCurrentSettings();

            try
            {
                ObjectId createdId = SubassemblyDotNetEngine.CreateAndAttachDotNetSubassembly(
                    _db,
                    saName,
                    clsInfo.DllPath,
                    clsInfo.FullClassName,
                    paramList,
                    targetAsmId,
                    _pickedInsertPoint,
                    out string msg);

                _lblStatus.Text = msg;
                _ed.WriteMessage($"\n[CTC Subassembly .NET] {msg}");

                RefreshDrawingSubassembliesGrid();
                if (!createdId.IsNull)
                {
                    SelectSubassemblyRowById(createdId);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi tạo Subassembly .NET:\n" + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnAssignSelectedClassToExistingClick(object? sender, EventArgs e)
        {
            if (_lstDotNetClasses.SelectedItem is not DotNetSubassemblyClassInfo clsInfo)
                return;

            _txtExistingDllPath.Text = clsInfo.DllPath;
            _txtExistingClassName.Text = clsInfo.FullClassName;

            if (_dgvEditExistingParams.Rows.Count <= 1)
            {
                PopulateParameterGrid(_dgvEditExistingParams, clsInfo.DefaultParams, true);
            }
            _lblStatus.Text = $"Đã gán Class '{clsInfo.FullClassName}' vào mục chỉnh sửa. Nhấn 'Áp dụng Tham số & Chạy lại' để thực thi.";
        }

        private void OnApplyExistingChangesClick(object? sender, EventArgs e)
        {
            if (_dgvDrawingSubassemblies.SelectedRows.Count == 0 ||
                _dgvDrawingSubassemblies.SelectedRows[0].Tag is not DrawingSubassemblyInfo selectedSa)
            {
                MessageBox.Show("Vui lòng chọn một Subassembly trong bảng bên phải.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var editedParams = ReadParameterGrid(_dgvEditExistingParams);
            string dllPath = _txtExistingDllPath.Text.Trim();
            string className = _txtExistingClassName.Text.Trim();

            try
            {
                bool ok = SubassemblyDotNetEngine.UpdateExistingSubassembly(
                    _db,
                    selectedSa.SubassemblyId,
                    dllPath,
                    className,
                    editedParams,
                    _chkRebuildCorridor.Checked,
                    out string msg);

                _lblStatus.Text = msg;
                _ed.WriteMessage($"\n[CTC Subassembly .NET] {msg}");

                if (ok)
                {
                    RefreshDrawingSubassembliesGrid();
                    SelectSubassemblyRowById(selectedSa.SubassemblyId);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi cập nhật Subassembly:\n" + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnUpdateAllDotNetClick(object? sender, EventArgs e)
        {
            try
            {
                int count = SubassemblyDotNetEngine.UpdateAllDotNetSubassemblies(
                    _db,
                    _chkForceUpdateAll.Checked,
                    _chkRebuildCorridor.Checked,
                    out List<string> logs);

                foreach (string log in logs)
                {
                    _ed.WriteMessage($"\n  {log}");
                }

                RefreshDrawingSubassembliesGrid();
                _lblStatus.Text = $"Đã cập nhật (Run) thành công {count} Subassembly .NET trong bản vẽ.";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi cập nhật hàng loạt:\n" + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnExportAtcCatalogClick(object? sender, EventArgs e)
        {
            using var sfd = new SaveFileDialog
            {
                Title = "Lưu file Catalog Subassembly .NET (.atc)",
                Filter = "Autodesk Tool Catalog (*.atc)|*.atc",
                FileName = "Civil3D_CustomDotNet_Subassemblies.atc"
            };

            if (sfd.ShowDialog(this) != DialogResult.OK)
                return;

            try
            {
                var customClasses = _allAvailableClasses
                    .Where(c => c.DisplayGroup.StartsWith("C#", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (_lstDotNetClasses.SelectedItem is DotNetSubassemblyClassInfo sel &&
                    !customClasses.Any(c => c.FullClassName == sel.FullClassName))
                {
                    customClasses.Add(sel);
                }

                var tools = customClasses.Select(c => (
                    ToolName: c.ClassName,
                    GuidStr: "{" + Guid.NewGuid().ToString().ToUpperInvariant() + "}",
                    DllPath: c.DllPath,
                    FullClassName: c.FullClassName,
                    Units: "meter",
                    Params: c.DefaultParams
                ));

                string xml = SubassemblyDotNetEngine.GenerateAtcCatalogXml("Civil3D_Tools .NET Subassemblies", tools);
                File.WriteAllText(sfd.FileName, xml, System.Text.Encoding.UTF8);

                _lblStatus.Text = $"Đã xuất Catalog .atc gồm {customClasses.Count} Subassembly tại: {sfd.FileName}";
                MessageBox.Show(
                    $"Đã xuất thành công file Catalog (.atc):\n{sfd.FileName}\n\n" +
                    "Bạn có thể mở Tool Palettes trong Civil 3D -> Chuột phải chọn 'Import' hoặc kéo thả cấu kiện từ Catalog vào Palette.",
                    "Xuất Catalog .atc thành công",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi xuất file .atc:\n" + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnPackageZipClick(object? sender, EventArgs e)
        {
            using var sfd = new SaveFileDialog
            {
                Title = "Đóng gói Bản vẽ kèm toàn bộ DLL Subassembly (.zip)",
                Filter = "Zip Archive (*.zip)|*.zip",
                FileName = string.IsNullOrWhiteSpace(_db.Filename)
                    ? "SubassemblyPackage.zip"
                    : Path.GetFileNameWithoutExtension(_db.Filename) + "_WithSubassemblies.zip"
            };

            if (sfd.ShowDialog(this) != DialogResult.OK)
                return;

            if (SubassemblyDotNetEngine.PackageDrawingWithSubassemblies(_db, sfd.FileName, out string msg))
            {
                _lblStatus.Text = msg;
                MessageBox.Show(msg, "Đóng gói thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(msg, "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ==================== SAVE & RESTORE PERSISTENT STATE ====================

        private void SaveCurrentSettings()
        {
            _lastFormSize = this.Size;
            _lastSourceFilterIndex = _cboSourceFilter.SelectedIndex;
            _lastSearchKeyword = _txtSearchClass.Text ?? "";
            _lastNewSubassemblyName = _txtNewSaName.Text ?? "";
            _lastAttachToAssembly = _chkAttachToAssembly.Checked;
            _lastRebuildCorridors = _chkRebuildCorridor.Checked;
            _lastForceUpdateAll = _chkForceUpdateAll.Checked;

            if (_lstDotNetClasses.SelectedItem is DotNetSubassemblyClassInfo clsInfo)
            {
                _lastSelectedClassFullName = clsInfo.FullClassName;
            }

            if (_cboTargetAssembly.SelectedIndex >= 0 && _cboTargetAssembly.SelectedIndex < _drawingAssemblies.Count)
            {
                _lastSelectedAssemblyId = _drawingAssemblies[_cboTargetAssembly.SelectedIndex].Id;
            }

            foreach (var p in ReadParameterGrid(_dgvCreateParams))
            {
                _lastCustomParamValues[p.Key] = p.Value;
            }
        }

        private void RestoreLastSettings()
        {
            if (_lastSourceFilterIndex >= 0 && _lastSourceFilterIndex < _cboSourceFilter.Items.Count)
            {
                _cboSourceFilter.SelectedIndex = _lastSourceFilterIndex;
            }

            if (!string.IsNullOrEmpty(_lastSearchKeyword))
            {
                _txtSearchClass.Text = _lastSearchKeyword;
            }

            if (!string.IsNullOrEmpty(_lastSelectedClassFullName))
            {
                for (int i = 0; i < _lstDotNetClasses.Items.Count; i++)
                {
                    if (_lstDotNetClasses.Items[i] is DotNetSubassemblyClassInfo item &&
                        item.FullClassName.Equals(_lastSelectedClassFullName, StringComparison.OrdinalIgnoreCase))
                    {
                        _lstDotNetClasses.SelectedIndex = i;
                        break;
                    }
                }
            }

            if (!string.IsNullOrEmpty(_lastNewSubassemblyName))
            {
                _txtNewSaName.Text = _lastNewSubassemblyName;
            }

            _chkAttachToAssembly.Checked = _lastAttachToAssembly && _drawingAssemblies.Count > 0;
            _chkRebuildCorridor.Checked = _lastRebuildCorridors;
            _chkForceUpdateAll.Checked = _lastForceUpdateAll;

            if (!_lastSelectedAssemblyId.IsNull && _lastSelectedAssemblyId.IsValid && !_lastSelectedAssemblyId.IsErased)
            {
                int asmIdx = _drawingAssemblies.FindIndex(a => a.Id == _lastSelectedAssemblyId);
                if (asmIdx >= 0)
                {
                    _cboTargetAssembly.SelectedIndex = asmIdx;
                }
            }

            if (!_lastSelectedSubassemblyId.IsNull && _lastSelectedSubassemblyId.IsValid && !_lastSelectedSubassemblyId.IsErased)
            {
                SelectSubassemblyRowById(_lastSelectedSubassemblyId);
            }
        }
    }
}

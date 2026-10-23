using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsFont = System.Drawing.Font;
using WinFormsColor = System.Drawing.Color;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace MyFirstProject.Civil_Tool_2
{
    /// <summary>
    /// Model dữ liệu cho từng Region của Corridor
    /// </summary>
    public class CorridorRegionBoundaryItem
    {
        public ObjectId CorridorId { get; set; } = ObjectId.Null;
        public string CorridorName { get; set; } = string.Empty;
        public int BaselineIndex { get; set; }
        public string BaselineName { get; set; } = string.Empty;
        public ObjectId AlignmentId { get; set; } = ObjectId.Null;
        public string AlignmentName { get; set; } = string.Empty;
        public int RegionIndex { get; set; }
        public string RegionName { get; set; } = string.Empty;
        public double StartStation { get; set; }
        public double EndStation { get; set; }
        public double Length => Math.Max(0, EndStation - StartStation);
        public int StationCount { get; set; }
        public string AssemblyName { get; set; } = string.Empty;
        public bool IsSelected { get; set; } = true;
    }

    /// <summary>
    /// Item dùng cho ComboBox danh sách Corridor
    /// </summary>
    public class CorridorBoundaryComboItem
    {
        public ObjectId Id { get; }
        public string Name { get; }

        public CorridorBoundaryComboItem(ObjectId id, string name)
        {
            Id = id;
            Name = name;
        }

        public override string ToString() => Name;
    }

    /// <summary>
    /// Item chọn màu AutoCAD ACI
    /// </summary>
    public class CadColorItem
    {
        public string DisplayName { get; }
        public short ColorIndex { get; }

        public CadColorItem(string displayName, short colorIndex)
        {
            DisplayName = displayName;
            ColorIndex = colorIndex;
        }

        public override string ToString() => DisplayName;
    }

    /// <summary>
    /// Form giao diện trích xuất ranh giới Corridor
    /// </summary>
    public class TrichXuatRanhGioiCorridorForm : Form
    {
        // ═══════════════════════════════════════════════════════════════
        //  PERSISTENT STATE (RULE 2)
        // ═══════════════════════════════════════════════════════════════
        private static ObjectId _lastCorridorId = ObjectId.Null;
        private static bool _lastScanAllCorridors = false;
        private static int _lastExtractionMode = 0; // 0: Region Boundaries, 1: Outer Boundary, 2: Left & Right Lines
        private static bool _lastIs2D = true;
        private static bool _lastIsClosed = true;
        private static string _lastLayerName = "C-CORR-BOUNDARY";
        private static short _lastColorIndex = 5; // Blue (theo style BinhDo)
        private static bool _lastCreateHatch = false;
        private static string _lastHatchPattern = "ANSI31";
        private static decimal _lastHatchScale = 1.0m;
        private static bool _lastCreateAnnotation = false;
        private static decimal _lastTextHeight = 2.0m;
        private static Size _lastFormSize = new Size(1020, 680);

        // ═══════════════════════════════════════════════════════════════
        //  FIELDS & PROPERTIES
        // ═══════════════════════════════════════════════════════════════
        private readonly Database _db;
        private readonly Editor _ed;

        public List<CorridorRegionBoundaryItem> AllRegions { get; private set; } = new List<CorridorRegionBoundaryItem>();
        public List<CorridorRegionBoundaryItem> SelectedRegions { get; private set; } = new List<CorridorRegionBoundaryItem>();
        public bool FormAccepted { get; private set; } = false;

        public ObjectId SelectedCorridorId { get; private set; } = ObjectId.Null;
        public int ExtractionMode => radModeCorridorOuterBoundary.Checked ? 1 : (radModeLeftRightLines.Checked ? 2 : 0);
        public bool Is2D => radPoly2D.Checked;
        public bool IsClosed => chkClosed.Checked;
        public string TargetLayerName => string.IsNullOrWhiteSpace(cmbLayer.Text) ? "C-CORR-BOUNDARY" : cmbLayer.Text.Trim();
        public short SelectedColorIndex => (cmbColor.SelectedItem is CadColorItem ci) ? ci.ColorIndex : (short)5;
        public bool CreateHatch => chkCreateHatch.Checked;
        public string HatchPattern => cmbHatchPattern.Text;
        public double HatchScale => (double)numHatchScale.Value;
        public bool CreateAnnotation => chkCreateAnnotation.Checked;
        public double TextHeight => (double)numTextHeight.Value;

        // UI Controls - GroupBox 1: Chọn Corridor
        private RadioButton radSelectedCorridor;
        private RadioButton radAllCorridors;
        private ComboBox cmbCorridors;
        private Button btnPickCorridor;
        private Button btnRefresh;

        // UI Controls - GroupBox 2: Danh sách Region
        private WinFormsLabel lblSummary;
        private DataGridView dgvRegions;
        private Button btnSelectAll;
        private Button btnUnselectAll;
        private Button btnInvertSelection;
        private Button btnZoom;

        // UI Controls - GroupBox 3: Tùy chọn trích xuất
        private RadioButton radModeRegionBoundaries;
        private RadioButton radModeCorridorOuterBoundary;
        private RadioButton radModeLeftRightLines;
        private RadioButton radPoly2D;
        private RadioButton radPoly3D;
        private CheckBox chkClosed;

        // UI Controls - GroupBox 4: Layer & Thuộc tính
        private ComboBox cmbLayer;
        private ComboBox cmbColor;
        private CheckBox chkCreateHatch;
        private ComboBox cmbHatchPattern;
        private NumericUpDown numHatchScale;
        private CheckBox chkCreateAnnotation;
        private NumericUpDown numTextHeight;

        // Action buttons
        private Button btnExecute;
        private Button btnCancel;

        public TrichXuatRanhGioiCorridorForm(Database db, Editor ed)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _ed = ed ?? throw new ArgumentNullException(nameof(ed));

            InitializeComponent();
            RestoreLastSettings();
            LoadCorridors();
            LoadLayers();
            AnalyzeRegions();
        }

        private void InitializeComponent()
        {
            this.Text = "Trích Xuất Đường Ranh Giới Corridor (Corridor Boundaries Extraction)";
            this.Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Regular);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(950, 620);
            this.Size = _lastFormSize;
            this.ShowIcon = false;
            this.MaximizeBox = true;
            this.MinimizeBox = false;

            // ═══════════════════════════════════════════════════════════
            // 1. GROUPBOX: CHỌN CORRIDOR
            // ═══════════════════════════════════════════════════════════
            var grpCorridor = new GroupBox
            {
                Text = "1. Nguồn Corridor",
                Dock = DockStyle.Top,
                Height = 82,
                Padding = new Padding(12, 8, 12, 8)
            };

            radSelectedCorridor = new RadioButton
            {
                Text = "Corridor được chọn:",
                Location = new Point(16, 26),
                AutoSize = true,
                Checked = true
            };
            radSelectedCorridor.CheckedChanged += (s, e) =>
            {
                cmbCorridors.Enabled = radSelectedCorridor.Checked;
                btnPickCorridor.Enabled = radSelectedCorridor.Checked;
                AnalyzeRegions();
            };

            cmbCorridors = new ComboBox
            {
                Location = new Point(160, 24),
                Width = 260,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbCorridors.SelectedIndexChanged += (s, e) =>
            {
                if (cmbCorridors.SelectedItem is CorridorBoundaryComboItem item)
                {
                    SelectedCorridorId = item.Id;
                    AnalyzeRegions();
                }
            };

            btnPickCorridor = new Button
            {
                Text = "📍 Chọn trên bản vẽ",
                Location = new Point(430, 23),
                Size = new Size(140, 28),
                BackColor = WinFormsColor.FromArgb(240, 244, 250),
                FlatStyle = FlatStyle.System
            };
            btnPickCorridor.Click += BtnPickCorridor_Click;

            radAllCorridors = new RadioButton
            {
                Text = "Tất cả Corridor trong bản vẽ",
                Location = new Point(16, 52),
                AutoSize = true
            };
            radAllCorridors.CheckedChanged += (s, e) =>
            {
                cmbCorridors.Enabled = !radAllCorridors.Checked;
                btnPickCorridor.Enabled = !radAllCorridors.Checked;
                AnalyzeRegions();
            };

            btnRefresh = new Button
            {
                Text = "🔄 Quét lại",
                Location = new Point(430, 52),
                Size = new Size(140, 26),
                FlatStyle = FlatStyle.System
            };
            btnRefresh.Click += (s, e) =>
            {
                LoadCorridors();
                AnalyzeRegions();
            };

            grpCorridor.Controls.Add(radSelectedCorridor);
            grpCorridor.Controls.Add(cmbCorridors);
            grpCorridor.Controls.Add(btnPickCorridor);
            grpCorridor.Controls.Add(radAllCorridors);
            grpCorridor.Controls.Add(btnRefresh);

            // ═══════════════════════════════════════════════════════════
            // 2. GROUPBOX: DANH SÁCH REGION (CENTER)
            // ═══════════════════════════════════════════════════════════
            var grpRegions = new GroupBox
            {
                Text = "2. Danh sách phân đoạn (Regions) của Corridor",
                Dock = DockStyle.Fill,
                Padding = new Padding(12, 8, 12, 8)
            };

            var pnlTopGrid = new Panel
            {
                Dock = DockStyle.Top,
                Height = 32
            };

            lblSummary = new WinFormsLabel
            {
                Text = "Đang quét danh sách Region...",
                Location = new Point(4, 8),
                AutoSize = true,
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = WinFormsColor.FromArgb(0, 102, 204)
            };
            pnlTopGrid.Controls.Add(lblSummary);

            dgvRegions = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = WinFormsColor.White,
                BorderStyle = BorderStyle.Fixed3D,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None
            };
            BuildDataGridViewColumns();

            dgvRegions.CellValueChanged += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex == 0)
                {
                    if (dgvRegions.Rows[e.RowIndex].Tag is CorridorRegionBoundaryItem item)
                    {
                        item.IsSelected = Convert.ToBoolean(dgvRegions.Rows[e.RowIndex].Cells[0].Value);
                        UpdateSummaryCount();
                    }
                }
            };
            dgvRegions.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (dgvRegions.IsCurrentCellDirty && dgvRegions.CurrentCell is DataGridViewCheckBoxCell)
                {
                    dgvRegions.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };

            var pnlGridButtons = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 36,
                Padding = new Padding(0, 6, 0, 0)
            };

            btnSelectAll = new Button
            {
                Text = "Chọn tất cả",
                Location = new Point(4, 6),
                Size = new Size(95, 26),
                FlatStyle = FlatStyle.System
            };
            btnSelectAll.Click += (s, e) => SetAllSelection(true);

            btnUnselectAll = new Button
            {
                Text = "Bỏ chọn",
                Location = new Point(105, 6),
                Size = new Size(85, 26),
                FlatStyle = FlatStyle.System
            };
            btnUnselectAll.Click += (s, e) => SetAllSelection(false);

            btnInvertSelection = new Button
            {
                Text = "Đảo chọn",
                Location = new Point(196, 6),
                Size = new Size(85, 26),
                FlatStyle = FlatStyle.System
            };
            btnInvertSelection.Click += (s, e) => InvertSelection();

            btnZoom = new Button
            {
                Text = "🔍 Zoom đến Region",
                Location = new Point(287, 6),
                Size = new Size(140, 26),
                FlatStyle = FlatStyle.System
            };
            btnZoom.Click += BtnZoom_Click;

            pnlGridButtons.Controls.Add(btnSelectAll);
            pnlGridButtons.Controls.Add(btnUnselectAll);
            pnlGridButtons.Controls.Add(btnInvertSelection);
            pnlGridButtons.Controls.Add(btnZoom);

            grpRegions.Controls.Add(dgvRegions);
            grpRegions.Controls.Add(pnlGridButtons);
            grpRegions.Controls.Add(pnlTopGrid);

            // ═══════════════════════════════════════════════════════════
            // 3. GROUPBOX: TÙY CHỌN TRÍCH XUẤT & ĐỐI TƯỢNG (BOTTOM)
            // ═══════════════════════════════════════════════════════════
            var pnlSettings = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 220,
                Padding = new Padding(0, 6, 0, 0)
            };

            var grpMode = new GroupBox
            {
                Text = "3. Kiểu đường ranh giới trích xuất",
                Location = new Point(0, 6),
                Size = new Size(490, 155),
                Anchor = AnchorStyles.Left | AnchorStyles.Top
            };

            radModeRegionBoundaries = new RadioButton
            {
                Text = "Ranh giới từng Region (Region Boundaries) - Mỗi Region 1 Polyline",
                Location = new Point(16, 24),
                AutoSize = true,
                Checked = true
            };

            radModeCorridorOuterBoundary = new RadioButton
            {
                Text = "Hợp nhất ranh giới ngoài tổng thể (Corridor Outer Boundary - GPMB)",
                Location = new Point(16, 50),
                AutoSize = true
            };

            radModeLeftRightLines = new RadioButton
            {
                Text = "Xuất 2 đường mép biên riêng biệt (Mép ngoài Trái & Mép ngoài Phải)",
                Location = new Point(16, 76),
                AutoSize = true
            };

            var lblObjType = new WinFormsLabel
            {
                Text = "Loại đối tượng:",
                Location = new Point(16, 112),
                AutoSize = true,
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold)
            };

            radPoly2D = new RadioButton
            {
                Text = "2D Polyline (Z = 0)",
                Location = new Point(115, 110),
                AutoSize = true,
                Checked = true
            };

            radPoly3D = new RadioButton
            {
                Text = "3D Polyline (Z thực tế)",
                Location = new Point(255, 110),
                AutoSize = true
            };

            chkClosed = new CheckBox
            {
                Text = "Đóng khép kín",
                Location = new Point(395, 110),
                AutoSize = true,
                Checked = true
            };

            grpMode.Controls.Add(radModeRegionBoundaries);
            grpMode.Controls.Add(radModeCorridorOuterBoundary);
            grpMode.Controls.Add(radModeLeftRightLines);
            grpMode.Controls.Add(lblObjType);
            grpMode.Controls.Add(radPoly2D);
            grpMode.Controls.Add(radPoly3D);
            grpMode.Controls.Add(chkClosed);

            // GroupBox 4: Thiết lập Layer & Tiện ích
            var grpLayerProps = new GroupBox
            {
                Text = "4. Thuộc tính CAD & Tiện ích bổ sung",
                Location = new Point(500, 6),
                Size = new Size(500, 155),
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };

            var lblLayer = new WinFormsLabel
            {
                Text = "Layer xuất:",
                Location = new Point(16, 26),
                AutoSize = true
            };

            cmbLayer = new ComboBox
            {
                Location = new Point(95, 23),
                Width = 190,
                DropDownStyle = ComboBoxStyle.DropDown
            };

            var lblColor = new WinFormsLabel
            {
                Text = "Màu sắc:",
                Location = new Point(300, 26),
                AutoSize = true
            };

            cmbColor = new ComboBox
            {
                Location = new Point(365, 23),
                Width = 115,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            PopulateCadColors();

            chkCreateHatch = new CheckBox
            {
                Text = "Tạo Hatch tô mặt bằng",
                Location = new Point(16, 62),
                AutoSize = true
            };

            var lblHatchPattern = new WinFormsLabel
            {
                Text = "Mẫu:",
                Location = new Point(190, 63),
                AutoSize = true
            };

            cmbHatchPattern = new ComboBox
            {
                Location = new Point(230, 60),
                Width = 100,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbHatchPattern.Items.AddRange(new object[] { "ANSI31", "SOLID", "ANSI32", "AR-CONC", "DOTS" });
            cmbHatchPattern.SelectedIndex = 0;

            var lblHatchScale = new WinFormsLabel
            {
                Text = "Tỷ lệ:",
                Location = new Point(345, 63),
                AutoSize = true
            };

            numHatchScale = new NumericUpDown
            {
                Location = new Point(390, 60),
                Width = 65,
                DecimalPlaces = 2,
                Minimum = 0.01m,
                Maximum = 1000m,
                Value = 1.0m
            };

            chkCreateAnnotation = new CheckBox
            {
                Text = "Ghi nhãn MText (Tên Region + Diện tích m²)",
                Location = new Point(16, 98),
                AutoSize = true
            };

            var lblTextH = new WinFormsLabel
            {
                Text = "Cỡ chữ (H):",
                Location = new Point(300, 99),
                AutoSize = true
            };

            numTextHeight = new NumericUpDown
            {
                Location = new Point(390, 96),
                Width = 65,
                DecimalPlaces = 2,
                Minimum = 0.1m,
                Maximum = 100m,
                Value = 2.0m
            };

            grpLayerProps.Controls.Add(lblLayer);
            grpLayerProps.Controls.Add(cmbLayer);
            grpLayerProps.Controls.Add(lblColor);
            grpLayerProps.Controls.Add(cmbColor);
            grpLayerProps.Controls.Add(chkCreateHatch);
            grpLayerProps.Controls.Add(lblHatchPattern);
            grpLayerProps.Controls.Add(cmbHatchPattern);
            grpLayerProps.Controls.Add(lblHatchScale);
            grpLayerProps.Controls.Add(numHatchScale);
            grpLayerProps.Controls.Add(chkCreateAnnotation);
            grpLayerProps.Controls.Add(lblTextH);
            grpLayerProps.Controls.Add(numTextHeight);

            // Bottom action panel
            var pnlActions = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 48
            };

            btnExecute = new Button
            {
                Text = "🚀 Trích Xuất Ranh Giới",
                Size = new Size(185, 34),
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
                Location = new Point(pnlActions.Width - 305, 7),
                BackColor = WinFormsColor.FromArgb(0, 120, 215),
                ForeColor = WinFormsColor.White,
                Font = new WinFormsFont("Segoe UI", 9.5F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat
            };
            btnExecute.FlatAppearance.BorderSize = 0;
            btnExecute.Click += BtnExecute_Click;

            btnCancel = new Button
            {
                Text = "Đóng",
                Size = new Size(100, 34),
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
                Location = new Point(pnlActions.Width - 110, 7),
                FlatStyle = FlatStyle.System
            };
            btnCancel.Click += (s, e) => this.Close();

            pnlActions.Controls.Add(btnExecute);
            pnlActions.Controls.Add(btnCancel);

            pnlSettings.Controls.Add(grpMode);
            pnlSettings.Controls.Add(grpLayerProps);
            pnlSettings.Controls.Add(pnlActions);

            // Thêm các control chính vào Form
            this.Controls.Add(grpRegions);
            this.Controls.Add(pnlSettings);
            this.Controls.Add(grpCorridor);

            this.FormClosing += TrichXuatRanhGioiCorridorForm_FormClosing;
        }

        private void PopulateCadColors()
        {
            cmbColor.Items.Clear();
            cmbColor.Items.Add(new CadColorItem("ByLayer", 256));
            cmbColor.Items.Add(new CadColorItem("1. Đỏ (Red)", 1));
            cmbColor.Items.Add(new CadColorItem("2. Vàng (Yellow)", 2));
            cmbColor.Items.Add(new CadColorItem("3. Xanh lá (Green)", 3));
            cmbColor.Items.Add(new CadColorItem("4. Xanh lơ (Cyan)", 4));
            cmbColor.Items.Add(new CadColorItem("5. Xanh dương (Blue)", 5));
            cmbColor.Items.Add(new CadColorItem("6. Tím (Magenta)", 6));
            cmbColor.Items.Add(new CadColorItem("7. Trắng (White)", 7));

            // Chọn mặc định
            int selectedIdx = 5; // Blue
            for (int i = 0; i < cmbColor.Items.Count; i++)
            {
                if (cmbColor.Items[i] is CadColorItem ci && ci.ColorIndex == _lastColorIndex)
                {
                    selectedIdx = i;
                    break;
                }
            }
            cmbColor.SelectedIndex = selectedIdx;
        }

        private void BuildDataGridViewColumns()
        {
            dgvRegions.Columns.Clear();

            var colCheck = new DataGridViewCheckBoxColumn
            {
                HeaderText = "Chọn",
                Width = 55,
                Resizable = DataGridViewTriState.False
            };

            var colStt = new DataGridViewTextBoxColumn
            {
                HeaderText = "STT",
                Width = 45,
                ReadOnly = true
            };

            var colCorridor = new DataGridViewTextBoxColumn
            {
                HeaderText = "Corridor",
                Width = 140,
                ReadOnly = true
            };

            var colBaseline = new DataGridViewTextBoxColumn
            {
                HeaderText = "Baseline (Tuyến)",
                Width = 140,
                ReadOnly = true
            };

            var colRegion = new DataGridViewTextBoxColumn
            {
                HeaderText = "Tên Region",
                Width = 140,
                ReadOnly = true
            };

            var colStartStation = new DataGridViewTextBoxColumn
            {
                HeaderText = "Lý trình đầu",
                Width = 95,
                ReadOnly = true
            };

            var colEndStation = new DataGridViewTextBoxColumn
            {
                HeaderText = "Lý trình cuối",
                Width = 95,
                ReadOnly = true
            };

            var colLength = new DataGridViewTextBoxColumn
            {
                HeaderText = "Dài (m)",
                Width = 75,
                ReadOnly = true
            };

            var colStations = new DataGridViewTextBoxColumn
            {
                HeaderText = "Số mặt cắt",
                Width = 85,
                ReadOnly = true
            };

            var colAssembly = new DataGridViewTextBoxColumn
            {
                HeaderText = "Assembly (Mặt cắt)",
                Width = 150,
                ReadOnly = true
            };

            dgvRegions.Columns.AddRange(new DataGridViewColumn[] {
                colCheck, colStt, colCorridor, colBaseline, colRegion,
                colStartStation, colEndStation, colLength, colStations, colAssembly
            });
        }

        // ═══════════════════════════════════════════════════════════════
        //  PERSISTENT STATE METHODS (RULE 2)
        // ═══════════════════════════════════════════════════════════════
        private void RestoreLastSettings()
        {
            try
            {
                if (_lastFormSize.Width >= this.MinimumSize.Width && _lastFormSize.Height >= this.MinimumSize.Height)
                {
                    this.Size = _lastFormSize;
                }

                if (_lastExtractionMode == 1) radModeCorridorOuterBoundary.Checked = true;
                else if (_lastExtractionMode == 2) radModeLeftRightLines.Checked = true;
                else radModeRegionBoundaries.Checked = true;

                radPoly2D.Checked = _lastIs2D;
                radPoly3D.Checked = !_lastIs2D;
                chkClosed.Checked = _lastIsClosed;

                chkCreateHatch.Checked = _lastCreateHatch;
                if (!string.IsNullOrEmpty(_lastHatchPattern)) cmbHatchPattern.Text = _lastHatchPattern;
                if (_lastHatchScale > 0) numHatchScale.Value = _lastHatchScale;

                chkCreateAnnotation.Checked = _lastCreateAnnotation;
                if (_lastTextHeight > 0) numTextHeight.Value = _lastTextHeight;

                if (_lastScanAllCorridors)
                {
                    radAllCorridors.Checked = true;
                    cmbCorridors.Enabled = false;
                    btnPickCorridor.Enabled = false;
                }
                else
                {
                    radSelectedCorridor.Checked = true;
                    cmbCorridors.Enabled = true;
                    btnPickCorridor.Enabled = true;
                }
            }
            catch { }
        }

        private void SaveCurrentSettings()
        {
            try
            {
                _lastFormSize = this.Size;
                _lastScanAllCorridors = radAllCorridors.Checked;
                _lastExtractionMode = ExtractionMode;
                _lastIs2D = radPoly2D.Checked;
                _lastIsClosed = chkClosed.Checked;
                _lastLayerName = TargetLayerName;
                _lastColorIndex = SelectedColorIndex;
                _lastCreateHatch = chkCreateHatch.Checked;
                _lastHatchPattern = cmbHatchPattern.Text;
                _lastHatchScale = numHatchScale.Value;
                _lastCreateAnnotation = chkCreateAnnotation.Checked;
                _lastTextHeight = numTextHeight.Value;

                if (!SelectedCorridorId.IsNull)
                {
                    _lastCorridorId = SelectedCorridorId;
                }
            }
            catch { }
        }

        private void TrichXuatRanhGioiCorridorForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            SaveCurrentSettings();
        }

        // ═══════════════════════════════════════════════════════════════
        //  DATA LOADING & ANALYSIS
        // ═══════════════════════════════════════════════════════════════
        private void LoadCorridors()
        {
            cmbCorridors.Items.Clear();

            try
            {
                using var tr = _db.TransactionManager.StartTransaction();
                var c3dDoc = CivilApplication.ActiveDocument;
                if (c3dDoc == null)
                {
                    tr.Commit();
                    return;
                }

                int selectedIdx = -1;
                int currentIdx = 0;

                foreach (ObjectId corrId in c3dDoc.CorridorCollection)
                {
                    if (corrId.IsNull || !corrId.IsValid || corrId.IsErased) continue;
                    if (tr.GetObject(corrId, OpenMode.ForRead) is not Corridor corr) continue;

                    var item = new CorridorBoundaryComboItem(corrId, corr.Name);
                    cmbCorridors.Items.Add(item);

                    if (!SelectedCorridorId.IsNull && corrId == SelectedCorridorId)
                    {
                        selectedIdx = currentIdx;
                    }
                    else if (selectedIdx == -1 && !_lastCorridorId.IsNull && corrId == _lastCorridorId)
                    {
                        selectedIdx = currentIdx;
                    }

                    currentIdx++;
                }

                tr.Commit();

                if (cmbCorridors.Items.Count > 0)
                {
                    cmbCorridors.SelectedIndex = selectedIdx >= 0 ? selectedIdx : 0;
                    if (cmbCorridors.SelectedItem is CorridorBoundaryComboItem selectedItem)
                    {
                        SelectedCorridorId = selectedItem.Id;
                    }
                }
                else
                {
                    SelectedCorridorId = ObjectId.Null;
                }
            }
            catch (System.Exception ex)
            {
                _ed.WriteMessage($"\n[Lỗi nạp danh sách Corridor]: {ex.Message}");
            }
        }

        private void LoadLayers()
        {
            cmbLayer.Items.Clear();
            try
            {
                using var tr = _db.TransactionManager.StartTransaction();
                if (tr.GetObject(_db.LayerTableId, OpenMode.ForRead) is LayerTable lt)
                {
                    foreach (ObjectId lId in lt)
                    {
                        if (tr.GetObject(lId, OpenMode.ForRead) is LayerTableRecord ltr)
                        {
                            cmbLayer.Items.Add(ltr.Name);
                        }
                    }
                }
                tr.Commit();
            }
            catch { }

            if (!cmbLayer.Items.Contains(_lastLayerName))
            {
                cmbLayer.Items.Insert(0, _lastLayerName);
            }
            cmbLayer.Text = _lastLayerName;
        }

        public void AnalyzeRegions()
        {
            AllRegions.Clear();
            dgvRegions.Rows.Clear();

            try
            {
                using var tr = _db.TransactionManager.StartTransaction();
                var c3dDoc = CivilApplication.ActiveDocument;
                if (c3dDoc == null)
                {
                    tr.Commit();
                    UpdateSummaryCount();
                    return;
                }

                List<ObjectId> corridorsToScan = new List<ObjectId>();

                if (radAllCorridors.Checked)
                {
                    foreach (ObjectId cId in c3dDoc.CorridorCollection)
                    {
                        corridorsToScan.Add(cId);
                    }
                }
                else if (!SelectedCorridorId.IsNull && SelectedCorridorId.IsValid && !SelectedCorridorId.IsErased)
                {
                    corridorsToScan.Add(SelectedCorridorId);
                }

                foreach (var corrId in corridorsToScan)
                {
                    if (tr.GetObject(corrId, OpenMode.ForRead) is not Corridor corr) continue;

                    for (int bIdx = 0; bIdx < corr.Baselines.Count; bIdx++)
                    {
                        var baseline = corr.Baselines[bIdx];
                        string alignName = string.Empty;

                        if (!baseline.AlignmentId.IsNull && baseline.AlignmentId.IsValid)
                        {
                            if (tr.GetObject(baseline.AlignmentId, OpenMode.ForRead) is Alignment align)
                            {
                                alignName = align.Name;
                            }
                        }

                        for (int rIdx = 0; rIdx < baseline.BaselineRegions.Count; rIdx++)
                        {
                            var region = baseline.BaselineRegions[rIdx];

                            string asmName = string.Empty;
                            if (!region.AssemblyId.IsNull && region.AssemblyId.IsValid)
                            {
                                if (tr.GetObject(region.AssemblyId, OpenMode.ForRead) is Assembly asm)
                                {
                                    asmName = asm.Name;
                                }
                            }

                            int stCount = 0;
                            try { stCount = region.AppliedAssemblies.Count; } catch { }

                            var item = new CorridorRegionBoundaryItem
                            {
                                CorridorId = corrId,
                                CorridorName = corr.Name,
                                BaselineIndex = bIdx,
                                BaselineName = string.IsNullOrEmpty(alignName) ? baseline.Name : $"{baseline.Name} ({alignName})",
                                AlignmentId = baseline.AlignmentId,
                                AlignmentName = alignName,
                                RegionIndex = rIdx,
                                RegionName = region.Name,
                                StartStation = region.StartStation,
                                EndStation = region.EndStation,
                                StationCount = stCount,
                                AssemblyName = asmName,
                                IsSelected = true
                            };

                            AllRegions.Add(item);
                        }
                    }
                }

                tr.Commit();
                PopulateGrid();
                UpdateSummaryCount();
            }
            catch (System.Exception ex)
            {
                _ed.WriteMessage($"\n[Lỗi phân tích Region]: {ex.Message}");
            }
        }

        private void PopulateGrid()
        {
            dgvRegions.Rows.Clear();

            for (int i = 0; i < AllRegions.Count; i++)
            {
                var item = AllRegions[i];
                int rowIdx = dgvRegions.Rows.Add(
                    item.IsSelected,
                    i + 1,
                    item.CorridorName,
                    item.BaselineName,
                    item.RegionName,
                    item.StartStation.ToString("F2"),
                    item.EndStation.ToString("F2"),
                    item.Length.ToString("F2"),
                    item.StationCount,
                    item.AssemblyName
                );

                var row = dgvRegions.Rows[rowIdx];
                row.Tag = item;
            }
        }

        private void UpdateSummaryCount()
        {
            int selectedCount = AllRegions.Count(r => r.IsSelected);
            int total = AllRegions.Count;

            if (total == 0)
            {
                lblSummary.Text = "ℹ Không tìm thấy Region nào trong Corridor được chọn.";
                lblSummary.ForeColor = WinFormsColor.FromArgb(180, 100, 0);
                btnExecute.Enabled = false;
            }
            else
            {
                lblSummary.Text = $"✅ Đã tìm thấy {total} Region (Đã chọn trích xuất: {selectedCount})";
                lblSummary.ForeColor = WinFormsColor.FromArgb(0, 102, 204);
                btnExecute.Enabled = selectedCount > 0;
            }
        }

        private void SetAllSelection(bool isSelected)
        {
            foreach (var item in AllRegions)
            {
                item.IsSelected = isSelected;
            }

            foreach (DataGridViewRow row in dgvRegions.Rows)
            {
                row.Cells[0].Value = isSelected;
            }

            UpdateSummaryCount();
        }

        private void InvertSelection()
        {
            foreach (var item in AllRegions)
            {
                item.IsSelected = !item.IsSelected;
            }

            foreach (DataGridViewRow row in dgvRegions.Rows)
            {
                row.Cells[0].Value = !Convert.ToBoolean(row.Cells[0].Value);
            }

            UpdateSummaryCount();
        }

        // ═══════════════════════════════════════════════════════════════
        //  CANVAS INTERACTION (PICK CORRIDOR & ZOOM REGION)
        // ═══════════════════════════════════════════════════════════════
        private void BtnPickCorridor_Click(object? sender, EventArgs e)
        {
            using var interaction = _ed.StartUserInteraction(this);
            var peo = new PromptEntityOptions("\nChọn Corridor trên bản vẽ: ");
            peo.SetRejectMessage("\nĐối tượng không phải là Corridor.");
            peo.AddAllowedClass(typeof(Corridor), true);

            var per = _ed.GetEntity(peo);
            interaction.End();

            if (per.Status == PromptStatus.OK)
            {
                SelectedCorridorId = per.ObjectId;
                radSelectedCorridor.Checked = true;
                LoadCorridors();
                AnalyzeRegions();
            }
        }

        private void BtnZoom_Click(object? sender, EventArgs e)
        {
            if (dgvRegions.CurrentRow == null || dgvRegions.CurrentRow.Tag is not CorridorRegionBoundaryItem item)
            {
                MessageBox.Show("Vui lòng chọn một dòng Region trong bảng để Zoom.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                using var interaction = _ed.StartUserInteraction(this);
                using var tr = _db.TransactionManager.StartTransaction();

                if (!item.AlignmentId.IsNull && item.AlignmentId.IsValid)
                {
                    if (tr.GetObject(item.AlignmentId, OpenMode.ForRead) is Alignment align)
                    {
                        double stStart = Math.Max(align.StartingStation, item.StartStation);
                        double stEnd = Math.Min(align.EndingStation, item.EndStation);
                        double midStation = (stStart + stEnd) / 2.0;

                        double eStart = 0, nStart = 0, eEnd = 0, nEnd = 0, eMid = 0, nMid = 0;
                        align.PointLocation(stStart, 0, ref eStart, ref nStart);
                        align.PointLocation(stEnd, 0, ref eEnd, ref nEnd);
                        align.PointLocation(midStation, 0, ref eMid, ref nMid);

                        Extents3d ext = new Extents3d();
                        ext.AddPoint(new Point3d(eStart, nStart, 0));
                        ext.AddPoint(new Point3d(eEnd, nEnd, 0));
                        ext.AddPoint(new Point3d(eMid, nMid, 0));

                        ZoomToExtents(_ed, ext);
                    }
                }
                tr.Commit();
                interaction.End();
            }
            catch (System.Exception ex)
            {
                _ed.WriteMessage($"\n[Lỗi Zoom Region]: {ex.Message}");
            }
        }

        private static void ZoomToExtents(Editor ed, Extents3d extents)
        {
            try
            {
                using (ViewTableRecord view = ed.GetCurrentView())
                {
                    double width = Math.Abs(extents.MaxPoint.X - extents.MinPoint.X);
                    double height = Math.Abs(extents.MaxPoint.Y - extents.MinPoint.Y);

                    if (width < 5.0) width = 20.0;
                    if (height < 5.0) height = 20.0;

                    Point2d center = new Point2d(
                        (extents.MinPoint.X + extents.MaxPoint.X) / 2.0,
                        (extents.MinPoint.Y + extents.MaxPoint.Y) / 2.0
                    );

                    double margin = 1.6;
                    view.CenterPoint = center;
                    view.Height = Math.Max(height, width) * margin;
                    view.Width = Math.Max(height, width) * margin;

                    ed.SetCurrentView(view);
                }
            }
            catch { }
        }

        // ═══════════════════════════════════════════════════════════════
        //  EXECUTION TRIGGER
        // ═══════════════════════════════════════════════════════════════
        private void BtnExecute_Click(object? sender, EventArgs e)
        {
            SelectedRegions = AllRegions.Where(r => r.IsSelected).ToList();

            if (SelectedRegions.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn ít nhất một Region để trích xuất ranh giới.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SaveCurrentSettings();
            FormAccepted = true;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}

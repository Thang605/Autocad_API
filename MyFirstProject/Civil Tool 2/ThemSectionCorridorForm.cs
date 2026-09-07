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
using MyFirstProject.Extensions;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsFont = System.Drawing.Font;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;
using Color = System.Drawing.Color;

namespace MyFirstProject.Civil_Tool_2
{
    /// <summary>
    /// Item dùng cho ComboBox lưu tên và ObjectId
    /// </summary>
    public class IdItem
    {
        public string Name { get; set; }
        public ObjectId Id { get; set; }

        public IdItem(string name, ObjectId id)
        {
            Name = name;
            Id = id;
        }

        public override string ToString() => Name;
    }

    /// <summary>
    /// Model lưu thông tin chi tiết của 1 Baseline Region để hiển thị trên Grid
    /// </summary>
    public class CorridorRegionInfo
    {
        public int BaselineIndex { get; set; }
        public int RegionIndex { get; set; }
        public string BaselineName { get; set; } = string.Empty;
        public string RegionName { get; set; } = string.Empty;
        public double StartStation { get; set; }
        public double EndStation { get; set; }
        public double Length => Math.Max(0, EndStation - StartStation);
        public string AssemblyName { get; set; } = string.Empty;
        public ObjectId AssemblyId { get; set; }
        public bool IsSelected { get; set; } = true;
    }

    /// <summary>
    /// Form giao diện cho lệnh Thêm Section / Cập nhật Tần suất cho tất cả Region của Corridor
    /// </summary>
    public class ThemSectionCorridorForm : Form
    {
        // ═══════════════════════════════════════════════════════════════
        //  PERSISTENT STATE (GHI NHỚ THÔNG SỐ TRƯỚC ĐÓ)
        // ═══════════════════════════════════════════════════════════════
        private static ObjectId _lastCorridorId = ObjectId.Null;
        private static string _lastCorridorName = string.Empty;
        private static double _lastInterval = 10.0;
        private static bool _lastUpdateFrequency = true;
        private static bool _lastAddExplicitStations = false;
        private static bool _lastApplyHorizGeom = true;
        private static bool _lastApplyProfileGeom = true;
        private static bool _lastApplyProfileHighLow = true;
        private static bool _lastApplySuperelevation = true;
        private static bool _lastApplyOffsetTarget = true;
        private static bool _lastRebuildCorridor = true;
        private static Size _lastFormSize = new Size(760, 720);

        // ═══════════════════════════════════════════════════════════════
        //  UI CONTROLS
        // ═══════════════════════════════════════════════════════════════
        private GroupBox grpCorridor = null!;
        private WinFormsLabel lblCorridor = null!;
        private ComboBox cmbCorridor = null!;
        private Button btnPickCorridor = null!;
        private Button btnRefresh = null!;
        private WinFormsLabel lblCorridorSummary = null!;

        private GroupBox grpRegions = null!;
        private CheckBox chkSelectAll = null!;
        private DataGridView dgvRegions = null!;
        private Button btnCheckAll = null!;
        private Button btnUncheckAll = null!;
        private WinFormsLabel lblRegionCountInfo = null!;

        private GroupBox grpSectionSettings = null!;
        private WinFormsLabel lblInterval = null!;
        private TextBox txtInterval = null!;
        private WinFormsLabel lblUnit = null!;
        private CheckBox chkUpdateFrequency = null!;
        private CheckBox chkAddExplicitStations = null!;

        private GroupBox grpGeomPoints = null!;
        private CheckBox chkHorizGeom = null!;
        private CheckBox chkProfileGeom = null!;
        private CheckBox chkProfileHighLow = null!;
        private CheckBox chkSuperelevation = null!;
        private CheckBox chkOffsetTarget = null!;

        private GroupBox grpExecution = null!;
        private CheckBox chkRebuild = null!;

        private Button btnExecute = null!;
        private Button btnCancel = null!;

        // ═══════════════════════════════════════════════════════════════
        //  OUTPUT PROPERTIES
        // ═══════════════════════════════════════════════════════════════
        public bool FormAccepted { get; private set; } = false;
        public ObjectId SelectedCorridorId { get; private set; } = ObjectId.Null;
        public List<CorridorRegionInfo> SelectedRegions { get; private set; } = new List<CorridorRegionInfo>();
        public double SectionInterval { get; private set; } = 10.0;
        public bool UpdateFrequencySettings { get; private set; } = true;
        public bool AddExplicitStations { get; private set; } = false;
        public bool ApplyHorizGeom { get; private set; } = true;
        public bool ApplyProfileGeom { get; private set; } = true;
        public bool ApplyProfileHighLow { get; private set; } = true;
        public bool ApplySuperelevation { get; private set; } = true;
        public bool ApplyOffsetTarget { get; private set; } = true;
        public bool RebuildAfterExecution { get; private set; } = true;

        private List<CorridorRegionInfo> _allRegions = new List<CorridorRegionInfo>();
        private bool _isUpdatingGrid = false;

        public ThemSectionCorridorForm()
        {
            InitializeComponent();
            RestoreLastSettings();
            LoadCorridors();

            this.FormClosing += (s, e) => SaveCurrentSettings();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            var fontTitle = new WinFormsFont("Segoe UI", 11.5F, FontStyle.Bold);
            var fontGroup = new WinFormsFont("Segoe UI", 9.5F, FontStyle.Bold);
            var fontNormal = new WinFormsFont("Segoe UI", 9F, FontStyle.Regular);
            var fontBold = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold);

            // Form base settings
            this.Text = "Thêm Section Cho Tất Cả Region Của Corridor";
            this.Size = _lastFormSize;
            this.MinimumSize = new Size(720, 650);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = false;
            this.Font = fontNormal;
            this.BackColor = Color.FromArgb(248, 249, 250);

            // ═══════════════════════════════════════════════════════════════
            //  HEADER
            // ═══════════════════════════════════════════════════════════════
            var panelHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 52,
                BackColor = Color.FromArgb(24, 90, 157),
                Padding = new Padding(15, 8, 15, 8)
            };

            var lblHeaderTitle = new WinFormsLabel
            {
                Text = "THÊM SECTION / THIẾT LẬP TẦN SUẤT CHO CORRIDOR REGIONS",
                Font = fontTitle,
                ForeColor = Color.White,
                Dock = DockStyle.Top,
                Height = 22
            };

            var lblHeaderSub = new WinFormsLabel
            {
                Text = "Tự động cập nhật khoảng cách Section & tần suất lấy mẫu cho toàn bộ các phân đoạn (Regions)",
                Font = new WinFormsFont("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = Color.FromArgb(220, 235, 252),
                Dock = DockStyle.Bottom,
                Height = 18
            };

            panelHeader.Controls.Add(lblHeaderSub);
            panelHeader.Controls.Add(lblHeaderTitle);

            // Main Content Panel
            var panelMain = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                AutoScroll = true
            };

            // ═══════════════════════════════════════════════════════════════
            //  GROUP 1: CHỌN CORRIDOR
            // ═══════════════════════════════════════════════════════════════
            grpCorridor = new GroupBox
            {
                Text = "1. Chọn Corridor Áp Dụng",
                Font = fontGroup,
                Location = new Point(12, 10),
                Size = new Size(718, 92),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            lblCorridor = new WinFormsLabel
            {
                Text = "Corridor:",
                Location = new Point(15, 28),
                Size = new Size(65, 24),
                Font = fontNormal,
                TextAlign = ContentAlignment.MiddleLeft
            };

            cmbCorridor = new ComboBox
            {
                Location = new Point(85, 27),
                Size = new Size(390, 26),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = fontNormal,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            cmbCorridor.SelectedIndexChanged += CmbCorridor_SelectedIndexChanged;

            btnPickCorridor = new Button
            {
                Text = "🎯 Pick Bản Vẽ",
                Location = new Point(485, 26),
                Size = new Size(110, 28),
                Font = fontNormal,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnPickCorridor.Click += BtnPickCorridor_Click;

            btnRefresh = new Button
            {
                Text = "🔄 Làm Mới",
                Location = new Point(602, 26),
                Size = new Size(100, 28),
                Font = fontNormal,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnRefresh.Click += (s, e) => LoadCorridors();

            lblCorridorSummary = new WinFormsLabel
            {
                Text = "Chưa chọn Corridor",
                Location = new Point(85, 58),
                Size = new Size(615, 22),
                Font = new WinFormsFont("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = Color.FromArgb(0, 102, 204),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            grpCorridor.Controls.AddRange(new Control[] {
                lblCorridor, cmbCorridor, btnPickCorridor, btnRefresh, lblCorridorSummary
            });

            // ═══════════════════════════════════════════════════════════════
            //  GROUP 2: DANH SÁCH REGIONS
            // ═══════════════════════════════════════════════════════════════
            grpRegions = new GroupBox
            {
                Text = "2. Danh Sách Phân Đoạn (Baseline Regions)",
                Font = fontGroup,
                Location = new Point(12, 110),
                Size = new Size(718, 215),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };

            chkSelectAll = new CheckBox
            {
                Text = "Chọn tất cả phân đoạn (Regions)",
                Location = new Point(15, 24),
                Size = new Size(230, 22),
                Font = fontBold,
                Checked = true
            };
            chkSelectAll.CheckedChanged += ChkSelectAll_CheckedChanged;

            btnCheckAll = new Button
            {
                Text = "Chọn tất cả",
                Location = new Point(510, 21),
                Size = new Size(95, 26),
                Font = fontNormal,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnCheckAll.Click += (s, e) => SetAllRegionsChecked(true);

            btnUncheckAll = new Button
            {
                Text = "Bỏ chọn",
                Location = new Point(612, 21),
                Size = new Size(90, 26),
                Font = fontNormal,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnUncheckAll.Click += (s, e) => SetAllRegionsChecked(false);

            dgvRegions = new DataGridView
            {
                Location = new Point(15, 52),
                Size = new Size(688, 132),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = fontNormal,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            var colCheck = new DataGridViewCheckBoxColumn
            {
                Name = "colSelect",
                HeaderText = "Chọn",
                Width = 50,
                FillWeight = 8
            };
            var colIndex = new DataGridViewTextBoxColumn
            {
                Name = "colIndex",
                HeaderText = "STT",
                ReadOnly = true,
                Width = 40,
                FillWeight = 7
            };
            var colBaseline = new DataGridViewTextBoxColumn
            {
                Name = "colBaseline",
                HeaderText = "Baseline",
                ReadOnly = true,
                FillWeight = 22
            };
            var colRegion = new DataGridViewTextBoxColumn
            {
                Name = "colRegion",
                HeaderText = "Tên Region",
                ReadOnly = true,
                FillWeight = 23
            };
            var colStart = new DataGridViewTextBoxColumn
            {
                Name = "colStart",
                HeaderText = "Lý Trình Đầu (m)",
                ReadOnly = true,
                FillWeight = 16
            };
            var colEnd = new DataGridViewTextBoxColumn
            {
                Name = "colEnd",
                HeaderText = "Lý Trình Cuối (m)",
                ReadOnly = true,
                FillWeight = 16
            };
            var colLength = new DataGridViewTextBoxColumn
            {
                Name = "colLength",
                HeaderText = "Dài (m)",
                ReadOnly = true,
                FillWeight = 12
            };
            var colAssembly = new DataGridViewTextBoxColumn
            {
                Name = "colAssembly",
                HeaderText = "Assembly",
                ReadOnly = true,
                FillWeight = 22
            };

            dgvRegions.Columns.AddRange(new DataGridViewColumn[] {
                colCheck, colIndex, colBaseline, colRegion, colStart, colEnd, colLength, colAssembly
            });

            dgvRegions.CellValueChanged += DgvRegions_CellValueChanged;
            dgvRegions.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (dgvRegions.IsCurrentCellDirty)
                    dgvRegions.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            lblRegionCountInfo = new WinFormsLabel
            {
                Text = "Đang chọn 0 / 0 phân đoạn",
                Location = new Point(15, 188),
                Size = new Size(400, 20),
                Font = new WinFormsFont("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = Color.DimGray,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };

            grpRegions.Controls.AddRange(new Control[] {
                chkSelectAll, btnCheckAll, btnUncheckAll, dgvRegions, lblRegionCountInfo
            });

            // ═══════════════════════════════════════════════════════════════
            //  GROUP 3: THÔNG SỐ SECTION & TẦN SUẤT
            // ═══════════════════════════════════════════════════════════════
            grpSectionSettings = new GroupBox
            {
                Text = "3. Thiết Lập Khoảng Cách Section / Tần Suất",
                Font = fontGroup,
                Location = new Point(12, 332),
                Size = new Size(718, 92),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            lblInterval = new WinFormsLabel
            {
                Text = "Khoảng cách / Tần suất Section:",
                Location = new Point(15, 26),
                Size = new Size(200, 24),
                Font = fontBold,
                TextAlign = ContentAlignment.MiddleLeft
            };

            txtInterval = new TextBox
            {
                Location = new Point(220, 25),
                Size = new Size(110, 26),
                Font = fontBold,
                Text = _lastInterval.ToString("F1")
            };

            lblUnit = new WinFormsLabel
            {
                Text = "(m)  (Ví dụ: 5, 10, 20...)",
                Location = new Point(335, 26),
                Size = new Size(160, 24),
                Font = fontNormal,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.DimGray
            };

            chkUpdateFrequency = new CheckBox
            {
                Text = "Cập nhật Tần suất lấy mẫu của Assembly (Tangent/Curve/Spiral/Profile = X mét)",
                Location = new Point(18, 58),
                Size = new Size(520, 24),
                Font = fontNormal,
                Checked = true
            };
            chkUpdateFrequency.CheckedChanged += (s, e) => UpdateGeomCheckboxesState();

            chkAddExplicitStations = new CheckBox
            {
                Text = "Chèn thêm các Section cố định (Add Stations) theo bước X mét",
                Location = new Point(540, 58),
                Size = new Size(380, 24),
                Font = fontNormal,
                Checked = false
            };

            grpSectionSettings.Controls.AddRange(new Control[] {
                lblInterval, txtInterval, lblUnit, chkUpdateFrequency, chkAddExplicitStations
            });

            // ═══════════════════════════════════════════════════════════════
            //  GROUP 4: ĐIỂM HÌNH HỌC (GEOMETRY POINTS)
            // ═══════════════════════════════════════════════════════════════
            grpGeomPoints = new GroupBox
            {
                Text = "4. Tùy Chọn Lấy Mẫu Tại Điểm Hình Học Tuyến (Geometry Points)",
                Font = fontGroup,
                Location = new Point(12, 430),
                Size = new Size(718, 75),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            chkHorizGeom = new CheckBox
            {
                Text = "Điểm HH Bình đồ",
                Location = new Point(15, 24),
                Size = new Size(130, 22),
                Font = fontNormal,
                Checked = true
            };
            chkProfileGeom = new CheckBox
            {
                Text = "Điểm HH Trắc dọc",
                Location = new Point(150, 24),
                Size = new Size(135, 22),
                Font = fontNormal,
                Checked = true
            };
            chkProfileHighLow = new CheckBox
            {
                Text = "Điểm Cao/Thấp trắc dọc",
                Location = new Point(290, 24),
                Size = new Size(160, 22),
                Font = fontNormal,
                Checked = true
            };
            chkSuperelevation = new CheckBox
            {
                Text = "Điểm tới hạn Siêu cao",
                Location = new Point(455, 24),
                Size = new Size(150, 22),
                Font = fontNormal,
                Checked = true
            };
            chkOffsetTarget = new CheckBox
            {
                Text = "Điểm HH Target Offset",
                Location = new Point(15, 48),
                Size = new Size(180, 22),
                Font = fontNormal,
                Checked = true
            };

            grpGeomPoints.Controls.AddRange(new Control[] {
                chkHorizGeom, chkProfileGeom, chkProfileHighLow, chkSuperelevation, chkOffsetTarget
            });

            // ═══════════════════════════════════════════════════════════════
            //  GROUP 5: TÙY CHỌN THỰC THI & BUTTONS
            // ═══════════════════════════════════════════════════════════════
            grpExecution = new GroupBox
            {
                Text = "5. Tùy Chọn Thực Thi",
                Font = fontGroup,
                Location = new Point(12, 510),
                Size = new Size(718, 52),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            chkRebuild = new CheckBox
            {
                Text = "Tự động Rebuild Corridor sau khi cập nhật section",
                Location = new Point(15, 22),
                Size = new Size(380, 22),
                Font = fontBold,
                Checked = true,
                ForeColor = Color.FromArgb(0, 100, 0)
            };

            grpExecution.Controls.Add(chkRebuild);

            // Bottom action panel
            var panelBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 56,
                BackColor = Color.FromArgb(235, 238, 242),
                Padding = new Padding(15, 10, 15, 10)
            };

            btnExecute = new Button
            {
                Text = "✔ Thực Hiện",
                Size = new Size(130, 36),
                Font = fontBold,
                BackColor = Color.FromArgb(24, 90, 157),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnExecute.FlatAppearance.BorderSize = 0;
            btnExecute.Location = new Point(panelBottom.Width - 285, 10);
            btnExecute.Click += BtnExecute_Click;

            btnCancel = new Button
            {
                Text = "✖ Hủy Bỏ",
                Size = new Size(110, 36),
                Font = fontNormal,
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.Cancel,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnCancel.Location = new Point(panelBottom.Width - 140, 10);

            panelBottom.Controls.AddRange(new Control[] { btnExecute, btnCancel });

            // Add all to panelMain
            panelMain.Controls.AddRange(new Control[] {
                grpCorridor, grpRegions, grpSectionSettings, grpGeomPoints, grpExecution
            });

            this.Controls.Add(panelMain);
            this.Controls.Add(panelBottom);
            this.Controls.Add(panelHeader);

            this.AcceptButton = btnExecute;
            this.CancelButton = btnCancel;

            this.ResumeLayout(false);
        }

        // ═══════════════════════════════════════════════════════════════
        //  PERSISTENT SETTINGS LOGIC
        // ═══════════════════════════════════════════════════════════════
        private void SaveCurrentSettings()
        {
            if (cmbCorridor.SelectedItem is IdItem item)
            {
                _lastCorridorId = item.Id;
                _lastCorridorName = item.Name;
            }

            if (double.TryParse(txtInterval.Text.Trim(), out double val) && val > 0)
            {
                _lastInterval = val;
            }

            _lastUpdateFrequency = chkUpdateFrequency.Checked;
            _lastAddExplicitStations = chkAddExplicitStations.Checked;
            _lastApplyHorizGeom = chkHorizGeom.Checked;
            _lastApplyProfileGeom = chkProfileGeom.Checked;
            _lastApplyProfileHighLow = chkProfileHighLow.Checked;
            _lastApplySuperelevation = chkSuperelevation.Checked;
            _lastApplyOffsetTarget = chkOffsetTarget.Checked;
            _lastRebuildCorridor = chkRebuild.Checked;

            if (this.WindowState == FormWindowState.Normal)
            {
                _lastFormSize = this.Size;
            }
        }

        private void RestoreLastSettings()
        {
            txtInterval.Text = _lastInterval.ToString("F1");
            chkUpdateFrequency.Checked = _lastUpdateFrequency;
            chkAddExplicitStations.Checked = _lastAddExplicitStations;
            chkHorizGeom.Checked = _lastApplyHorizGeom;
            chkProfileGeom.Checked = _lastApplyProfileGeom;
            chkProfileHighLow.Checked = _lastApplyProfileHighLow;
            chkSuperelevation.Checked = _lastApplySuperelevation;
            chkOffsetTarget.Checked = _lastApplyOffsetTarget;
            chkRebuild.Checked = _lastRebuildCorridor;

            UpdateGeomCheckboxesState();
        }

        private void UpdateGeomCheckboxesState()
        {
            grpGeomPoints.Enabled = chkUpdateFrequency.Checked;
        }

        // ═══════════════════════════════════════════════════════════════
        //  DATA LOADING & CORRIDOR SELECTION
        // ═══════════════════════════════════════════════════════════════
        public void LoadCorridors()
        {
            cmbCorridor.Items.Clear();
            _allRegions.Clear();
            dgvRegions.Rows.Clear();

            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var db = doc.Database;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    int selectedIndex = -1;
                    int index = 0;

                    foreach (ObjectId id in A.Cdoc.CorridorCollection)
                    {
                        if (tr.GetObject(id, OpenMode.ForRead) is Corridor c)
                        {
                            cmbCorridor.Items.Add(new IdItem(c.Name, id));

                            if (!_lastCorridorId.IsNull && _lastCorridorId.IsValid && !_lastCorridorId.IsErased && id == _lastCorridorId)
                            {
                                selectedIndex = index;
                            }
                            else if (selectedIndex == -1 && !string.IsNullOrEmpty(_lastCorridorName) && c.Name.Equals(_lastCorridorName, StringComparison.OrdinalIgnoreCase))
                            {
                                selectedIndex = index;
                            }
                            index++;
                        }
                    }

                    tr.Commit();

                    if (cmbCorridor.Items.Count > 0)
                    {
                        cmbCorridor.SelectedIndex = (selectedIndex >= 0) ? selectedIndex : 0;
                    }
                    else
                    {
                        lblCorridorSummary.Text = "Không có Corridor nào trong bản vẽ!";
                        lblCorridorSummary.ForeColor = Color.Red;
                        btnExecute.Enabled = false;
                    }
                }
                catch (System.Exception ex)
                {
                    lblCorridorSummary.Text = $"Lỗi đọc dữ liệu: {ex.Message}";
                    lblCorridorSummary.ForeColor = Color.Red;
                }
            }
        }

        private void CmbCorridor_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (cmbCorridor.SelectedItem is not IdItem item) return;

            SelectedCorridorId = item.Id;
            LoadRegionsForCorridor(SelectedCorridorId);
        }

        private void LoadRegionsForCorridor(ObjectId corridorId)
        {
            _allRegions.Clear();
            _isUpdatingGrid = true;
            dgvRegions.Rows.Clear();

            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var db = doc.Database;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    if (tr.GetObject(corridorId, OpenMode.ForRead) is Corridor corridor)
                    {
                        int totalBaselines = corridor.Baselines.Count;
                        int totalRegions = 0;
                        double totalLength = 0;
                        int stt = 1;

                        for (int bIdx = 0; bIdx < corridor.Baselines.Count; bIdx++)
                        {
                            var baseline = corridor.Baselines[bIdx];
                            for (int rIdx = 0; rIdx < baseline.BaselineRegions.Count; rIdx++)
                            {
                                var region = baseline.BaselineRegions[rIdx];
                                totalRegions++;

                                string assemblyName = "Không xác định";
                                if (!region.AssemblyId.IsNull && region.AssemblyId.IsValid)
                                {
                                    if (tr.GetObject(region.AssemblyId, OpenMode.ForRead) is Assembly assembly)
                                    {
                                        assemblyName = assembly.Name;
                                    }
                                }

                                var regInfo = new CorridorRegionInfo
                                {
                                    BaselineIndex = bIdx,
                                    RegionIndex = rIdx,
                                    BaselineName = string.IsNullOrEmpty(baseline.Name) ? $"Baseline {bIdx + 1}" : baseline.Name,
                                    RegionName = string.IsNullOrEmpty(region.Name) ? $"Region {rIdx + 1}" : region.Name,
                                    StartStation = region.StartStation,
                                    EndStation = region.EndStation,
                                    AssemblyName = assemblyName,
                                    AssemblyId = region.AssemblyId,
                                    IsSelected = true
                                };

                                totalLength += regInfo.Length;
                                _allRegions.Add(regInfo);

                                dgvRegions.Rows.Add(
                                    true,
                                    stt++,
                                    regInfo.BaselineName,
                                    regInfo.RegionName,
                                    regInfo.StartStation.ToString("F3"),
                                    regInfo.EndStation.ToString("F3"),
                                    regInfo.Length.ToString("F2"),
                                    regInfo.AssemblyName
                                );
                            }
                        }

                        lblCorridorSummary.Text = $"Corridor '{corridor.Name}': {totalBaselines} Baseline(s), {totalRegions} Phân đoạn (Region), Tổng chiều dài: {totalLength:F2} m";
                        lblCorridorSummary.ForeColor = Color.FromArgb(0, 102, 204);
                        btnExecute.Enabled = totalRegions > 0;
                    }
                    tr.Commit();
                }
                catch (System.Exception ex)
                {
                    lblCorridorSummary.Text = $"Lỗi đọc phân đoạn: {ex.Message}";
                    lblCorridorSummary.ForeColor = Color.Red;
                }
                finally
                {
                    _isUpdatingGrid = false;
                    UpdateRegionCountSummary();
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  PICK CORRIDOR FROM CAD CANVAS
        // ═══════════════════════════════════════════════════════════════
        private void BtnPickCorridor_Click(object? sender, EventArgs e)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            using (var interaction = ed.StartUserInteraction(this))
            {
                var pOpt = new PromptEntityOptions("\nChọn đối tượng Corridor trên bản vẽ: ");
                pOpt.SetRejectMessage("\nĐối tượng được chọn không phải là Corridor.");
                pOpt.AddAllowedClass(typeof(Corridor), true);

                var pRes = ed.GetEntity(pOpt);
                interaction.End();

                if (pRes.Status == PromptStatus.OK)
                {
                    ObjectId pickedId = pRes.ObjectId;
                    // Find in combo
                    for (int i = 0; i < cmbCorridor.Items.Count; i++)
                    {
                        if (cmbCorridor.Items[i] is IdItem item && item.Id == pickedId)
                        {
                            cmbCorridor.SelectedIndex = i;
                            return;
                        }
                    }

                    // If not found in combo, refresh and select
                    LoadCorridors();
                    for (int i = 0; i < cmbCorridor.Items.Count; i++)
                    {
                        if (cmbCorridor.Items[i] is IdItem item && item.Id == pickedId)
                        {
                            cmbCorridor.SelectedIndex = i;
                            return;
                        }
                    }
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  GRID SELECTION & SUMMARY
        // ═══════════════════════════════════════════════════════════════
        private void ChkSelectAll_CheckedChanged(object? sender, EventArgs e)
        {
            if (_isUpdatingGrid) return;
            SetAllRegionsChecked(chkSelectAll.Checked);
        }

        private void SetAllRegionsChecked(bool check)
        {
            _isUpdatingGrid = true;
            for (int i = 0; i < dgvRegions.Rows.Count; i++)
            {
                dgvRegions.Rows[i].Cells["colSelect"].Value = check;
                if (i < _allRegions.Count)
                    _allRegions[i].IsSelected = check;
            }
            chkSelectAll.Checked = check;
            _isUpdatingGrid = false;
            UpdateRegionCountSummary();
        }

        private void DgvRegions_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (_isUpdatingGrid || e.RowIndex < 0 || e.ColumnIndex != dgvRegions.Columns["colSelect"].Index) return;

            bool isChecked = Convert.ToBoolean(dgvRegions.Rows[e.RowIndex].Cells["colSelect"].Value);
            if (e.RowIndex < _allRegions.Count)
            {
                _allRegions[e.RowIndex].IsSelected = isChecked;
            }

            _isUpdatingGrid = true;
            chkSelectAll.Checked = _allRegions.Count > 0 && _allRegions.All(r => r.IsSelected);
            _isUpdatingGrid = false;

            UpdateRegionCountSummary();
        }

        private void UpdateRegionCountSummary()
        {
            int selected = _allRegions.Count(r => r.IsSelected);
            int total = _allRegions.Count;
            lblRegionCountInfo.Text = $"Đang chọn {selected} / {total} phân đoạn";
            btnExecute.Enabled = selected > 0;
        }

        // ═══════════════════════════════════════════════════════════════
        //  EXECUTE BUTTON CLICK & VALIDATION
        // ═══════════════════════════════════════════════════════════════
        private void BtnExecute_Click(object? sender, EventArgs e)
        {
            if (SelectedCorridorId.IsNull || !SelectedCorridorId.IsValid)
            {
                MessageBox.Show("Vui lòng chọn một Corridor hợp lệ!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                cmbCorridor.Focus();
                return;
            }

            if (!double.TryParse(txtInterval.Text.Trim(), out double interval) || interval <= 0.001)
            {
                MessageBox.Show("Vui lòng nhập khoảng cách Section hợp lệ (> 0 m)!", "Lỗi nhập liệu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtInterval.Focus();
                txtInterval.SelectAll();
                return;
            }

            if (!chkUpdateFrequency.Checked && !chkAddExplicitStations.Checked)
            {
                MessageBox.Show("Vui lòng tích chọn ít nhất 1 phương thức: 'Cập nhật Tần suất' hoặc 'Chèn thêm Section bổ sung'!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                chkUpdateFrequency.Focus();
                return;
            }

            SelectedRegions = _allRegions.Where(r => r.IsSelected).ToList();
            if (SelectedRegions.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn ít nhất 1 phân đoạn (Region) để áp dụng!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SectionInterval = interval;
            UpdateFrequencySettings = chkUpdateFrequency.Checked;
            AddExplicitStations = chkAddExplicitStations.Checked;
            ApplyHorizGeom = chkHorizGeom.Checked;
            ApplyProfileGeom = chkProfileGeom.Checked;
            ApplyProfileHighLow = chkProfileHighLow.Checked;
            ApplySuperelevation = chkSuperelevation.Checked;
            ApplyOffsetTarget = chkOffsetTarget.Checked;
            RebuildAfterExecution = chkRebuild.Checked;

            SaveCurrentSettings();
            FormAccepted = true;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}

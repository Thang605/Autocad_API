using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsFont = System.Drawing.Font;
using WinFormsColor = System.Drawing.Color;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace MyFirstProject.Civil_Tool_2
{
    public enum RegionSpatialRelation
    {
        FullyInside,    // Nằm trọn trong Polyline
        Intersecting,   // Giao cắt Polyline (một phần trong, một phần ngoài)
        Outside         // Nằm ngoài Polyline
    }

    public class RegionDisplayItem
    {
        public int BaselineIndex { get; set; }
        public int RegionIndex { get; set; }
        public string BaselineName { get; set; } = string.Empty;
        public string AlignmentName { get; set; } = string.Empty;
        public string RegionName { get; set; } = string.Empty;
        public string AssemblyName { get; set; } = string.Empty;
        public ObjectId AssemblyId { get; set; } = ObjectId.Null;
        public double StartStation { get; set; }
        public double EndStation { get; set; }
        public double Length => Math.Max(0, EndStation - StartStation);
        public bool IsEnabled { get; set; } = true;
        public bool OriginalIsEnabled { get; set; } = true;
        public RegionSpatialRelation Relation { get; set; } = RegionSpatialRelation.Outside;

        public string RelationDisplay
        {
            get
            {
                return Relation switch
                {
                    RegionSpatialRelation.FullyInside => "✔ Nằm trọn bên trong",
                    RegionSpatialRelation.Intersecting => "⚠ Giao cắt ranh giới",
                    _ => "Nằm bên ngoài"
                };
            }
        }
    }


    public class BatTatCorridorRegionPolylineForm : Form
    {
        // ═══════════════════════════════════════════════════════════════
        //  PERSISTENT STATE (RULE 2)
        // ═══════════════════════════════════════════════════════════════
        private static ObjectId _lastCorridorId = ObjectId.Null;
        private static ObjectId _lastPolylineId = ObjectId.Null;
        private static bool _lastOnlyCenterline = true;
        private static bool _lastFilterInsideOnly = false;
        private static bool _lastAutoRebuild = true;
        private static Size _lastFormSize = new Size(950, 620);

        // ═══════════════════════════════════════════════════════════════
        //  FIELDS & PROPERTIES
        // ═══════════════════════════════════════════════════════════════
        private readonly Database _db;
        private readonly Editor _ed;

        public ObjectId SelectedCorridorId { get; private set; } = ObjectId.Null;
        public ObjectId SelectedPolylineId { get; private set; } = ObjectId.Null;
        public List<RegionDisplayItem> RegionItems { get; private set; } = new List<RegionDisplayItem>();
        public bool FormAccepted { get; private set; } = false;
        public bool AutoRebuild => chkAutoRebuild.Checked;

        // UI Controls
        private ComboBox cmbCorridor;
        private Button btnPickCorridor;
        private TextBox txtPolylineInfo;
        private Button btnPickPolyline;
        private CheckBox chkOnlyCenterline;
        private WinFormsLabel lblSummary;
        private CheckBox chkFilterInside;
        private DataGridView dgvRegions;
        private Button btnDisableInside;
        private Button btnEnableInside;
        private Button btnToggleInside;
        private Button btnSplitIntersecting;
        private Button btnCheckAll;
        private Button btnUncheckAll;
        private CheckBox chkAutoRebuild;
        private Button btnApply;
        private Button btnCancel;

        public BatTatCorridorRegionPolylineForm(Database db, Editor ed)
        {
            _db = db;
            _ed = ed;

            InitializeComponents();
            RestoreLastSettings();
            LoadCorridors();

            // Nếu đã có corridor và polyline lưu trước đó, tự động phân tích
            if (!SelectedCorridorId.IsNull && !SelectedPolylineId.IsNull)
            {
                AnalyzeRegions();
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  INITIALIZE COMPONENTS
        // ═══════════════════════════════════════════════════════════════
        private void InitializeComponents()
        {
            this.Text = "CTC - Bật/Tắt & Chia nhỏ Corridor Region theo Polyline";
            this.Size = _lastFormSize;
            this.MinimumSize = new Size(850, 520);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Regular);
            this.ShowIcon = false;

            // Header Panel
            var pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 45,
                BackColor = WinFormsColor.FromArgb(240, 244, 250)
            };
            var lblTitle = new WinFormsLabel
            {
                Text = "QUẢN LÝ & BẬT TẮT CORRIDOR REGION THEO RANH GIỚI POLYLINE",
                Font = new WinFormsFont("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = WinFormsColor.FromArgb(20, 50, 110),
                Location = new Point(15, 12),
                AutoSize = true
            };
            pnlHeader.Controls.Add(lblTitle);

            // Group 1: Chọn đối tượng
            var grpObjects = new GroupBox
            {
                Text = " 1. Đối tượng đầu vào ",
                Location = new Point(15, 55),
                Size = new Size(this.ClientSize.Width - 30, 95),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var lblCorridor = new WinFormsLabel { Text = "Corridor:", Location = new Point(15, 25), Size = new Size(65, 22) };
            cmbCorridor = new ComboBox
            {
                Location = new Point(85, 22),
                Size = new Size(260, 24),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbCorridor.SelectedIndexChanged += (s, e) =>
            {
                if (cmbCorridor.SelectedItem is CorridorItem ci)
                {
                    SelectedCorridorId = ci.Id;
                    AnalyzeRegions();
                }
            };

            btnPickCorridor = new Button
            {
                Text = "🎯 Chọn trên bản vẽ",
                Location = new Point(355, 21),
                Size = new Size(130, 26),
                Cursor = Cursors.Hand
            };
            btnPickCorridor.Click += BtnPickCorridor_Click;

            var lblPolyline = new WinFormsLabel { Text = "Polyline:", Location = new Point(510, 25), Size = new Size(60, 22) };
            txtPolylineInfo = new TextBox
            {
                Location = new Point(575, 22),
                Size = new Size(220, 23),
                ReadOnly = true,
                BackColor = WinFormsColor.WhiteSmoke,
                Text = "Chưa chọn Polyline ranh giới"
            };

            btnPickPolyline = new Button
            {
                Text = "🎯 Chọn Polyline",
                Location = new Point(805, 21),
                Size = new Size(110, 26),
                Cursor = Cursors.Hand
            };
            btnPickPolyline.Click += BtnPickPolyline_Click;

            chkOnlyCenterline = new CheckBox
            {
                Text = "Kiểm tra theo tim tuyến (Alignment) - Khuyến nghị nhanh & chính xác",
                Location = new Point(85, 58),
                Size = new Size(480, 24),
                Checked = true
            };
            chkOnlyCenterline.CheckedChanged += (s, e) => AnalyzeRegions();

            grpObjects.Controls.AddRange(new Control[] {
                lblCorridor, cmbCorridor, btnPickCorridor,
                lblPolyline, txtPolylineInfo, btnPickPolyline,
                chkOnlyCenterline
            });

            // Group 2: Danh sách Region
            var grpRegions = new GroupBox
            {
                Text = " 2. Danh sách Region & Trạng thái phân tích ",
                Location = new Point(15, 160),
                Size = new Size(this.ClientSize.Width - 30, this.ClientSize.Height - 270),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            lblSummary = new WinFormsLabel
            {
                Text = "Chưa có dữ liệu.",
                Location = new Point(15, 22),
                Size = new Size(450, 20),
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = WinFormsColor.FromArgb(40, 40, 40)
            };

            chkFilterInside = new CheckBox
            {
                Text = "Chỉ hiện Region nằm trong / giao cắt",
                Location = new Point(480, 20),
                Size = new Size(250, 22),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            chkFilterInside.CheckedChanged += (s, e) => RefreshGrid();

            // Action Toolbar for Regions
            var pnlTools = new Panel
            {
                Location = new Point(15, 46),
                Size = new Size(grpRegions.Width - 30, 32),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            btnDisableInside = new Button
            {
                Text = "❌ Tắt Region bên trong",
                Location = new Point(0, 0),
                Size = new Size(160, 30),
                Cursor = Cursors.Hand,
                Font = new WinFormsFont("Segoe UI", 8.5F, FontStyle.Bold),
                BackColor = WinFormsColor.FromArgb(255, 235, 235)
            };
            btnDisableInside.Click += (s, e) => SetInsideRegionsStatus(false);

            btnEnableInside = new Button
            {
                Text = "✔ Bật Region bên trong",
                Location = new Point(168, 0),
                Size = new Size(160, 30),
                Cursor = Cursors.Hand,
                Font = new WinFormsFont("Segoe UI", 8.5F, FontStyle.Bold),
                BackColor = WinFormsColor.FromArgb(235, 250, 235)
            };
            btnEnableInside.Click += (s, e) => SetInsideRegionsStatus(true);

            btnToggleInside = new Button
            {
                Text = "🔄 Đảo trạng thái bên trong",
                Location = new Point(336, 0),
                Size = new Size(170, 30),
                Cursor = Cursors.Hand,
                Font = new WinFormsFont("Segoe UI", 8.5F)
            };
            btnToggleInside.Click += (s, e) => ToggleInsideRegionsStatus();

            btnSplitIntersecting = new Button
            {
                Text = "✂️ Chia nhỏ Region giao cắt",
                Location = new Point(514, 0),
                Size = new Size(185, 30),
                Cursor = Cursors.Hand,
                Font = new WinFormsFont("Segoe UI", 8.5F, FontStyle.Bold),
                BackColor = WinFormsColor.FromArgb(255, 245, 220)
            };
            btnSplitIntersecting.Click += BtnSplitIntersecting_Click;

            btnCheckAll = new Button
            {
                Text = "Bật hết",
                Location = new Point(707, 0),
                Size = new Size(65, 30),
                Cursor = Cursors.Hand
            };
            btnCheckAll.Click += (s, e) => SetAllStatus(true);

            btnUncheckAll = new Button
            {
                Text = "Tắt hết",
                Location = new Point(778, 0),
                Size = new Size(65, 30),
                Cursor = Cursors.Hand
            };
            btnUncheckAll.Click += (s, e) => SetAllStatus(false);

            pnlTools.Controls.AddRange(new Control[] {
                btnDisableInside, btnEnableInside, btnToggleInside,
                btnSplitIntersecting, btnCheckAll, btnUncheckAll
            });

            // DataGridView
            dgvRegions = new DataGridView
            {
                Location = new Point(15, 84),
                Size = new Size(grpRegions.Width - 30, grpRegions.Height - 95),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = WinFormsColor.White,
                BorderStyle = BorderStyle.Fixed3D,
                RowHeadersVisible = false,
                AutoGenerateColumns = false
            };

            BuildGridColumns();
            grpRegions.Controls.AddRange(new Control[] { lblSummary, chkFilterInside, pnlTools, dgvRegions });

            // Bottom Panel
            var pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 55,
                BackColor = WinFormsColor.FromArgb(248, 249, 250)
            };

            chkAutoRebuild = new CheckBox
            {
                Text = "Tự động Rebuild Corridor sau khi áp dụng",
                Location = new Point(20, 16),
                Size = new Size(300, 22),
                Checked = true
            };

            btnApply = new Button
            {
                Text = "Áp dụng & Thực thi",
                Location = new Point(pnlBottom.Width - 235, 12),
                Size = new Size(130, 32),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold),
                BackColor = WinFormsColor.FromArgb(0, 120, 215),
                ForeColor = WinFormsColor.White,
                Cursor = Cursors.Hand
            };
            btnApply.Click += BtnApply_Click;

            btnCancel = new Button
            {
                Text = "Đóng",
                Location = new Point(pnlBottom.Width - 95, 12),
                Size = new Size(80, 32),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                DialogResult = DialogResult.Cancel,
                Cursor = Cursors.Hand
            };

            pnlBottom.Controls.AddRange(new Control[] { chkAutoRebuild, btnApply, btnCancel });

            this.Controls.AddRange(new Control[] { grpRegions, grpObjects, pnlHeader, pnlBottom });
            this.AcceptButton = btnApply;
            this.CancelButton = btnCancel;

            this.FormClosing += (s, e) => SaveCurrentSettings();
        }

        private void BuildGridColumns()
        {
            dgvRegions.Columns.Clear();

            var colCheck = new DataGridViewCheckBoxColumn
            {
                HeaderText = "Bật/Tắt",
                Name = "colEnabled",
                Width = 65,
                SortMode = DataGridViewColumnSortMode.Automatic
            };

            var colName = new DataGridViewTextBoxColumn
            {
                HeaderText = "Tên Region",
                Name = "colName",
                Width = 140,
                ReadOnly = true
            };

            var colBaseline = new DataGridViewTextBoxColumn
            {
                HeaderText = "Baseline (Tuyến)",
                Name = "colBaseline",
                Width = 130,
                ReadOnly = true
            };

            var colAssembly = new DataGridViewTextBoxColumn
            {
                HeaderText = "Assembly",
                Name = "colAssembly",
                Width = 130,
                ReadOnly = true
            };

            var colStart = new DataGridViewTextBoxColumn
            {
                HeaderText = "Lý trình đầu",
                Name = "colStart",
                Width = 95,
                ReadOnly = true,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight }
            };

            var colEnd = new DataGridViewTextBoxColumn
            {
                HeaderText = "Lý trình cuối",
                Name = "colEnd",
                Width = 95,
                ReadOnly = true,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight }
            };

            var colLength = new DataGridViewTextBoxColumn
            {
                HeaderText = "Chiều dài (m)",
                Name = "colLength",
                Width = 95,
                ReadOnly = true,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight }
            };

            var colStatus = new DataGridViewTextBoxColumn
            {
                HeaderText = "Vị trí so với Polyline",
                Name = "colStatus",
                Width = 150,
                ReadOnly = true
            };

            dgvRegions.Columns.AddRange(new DataGridViewColumn[] {
                colCheck, colName, colBaseline, colAssembly,
                colStart, colEnd, colLength, colStatus
            });

            dgvRegions.CellValueChanged += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex == 0)
                {
                    var item = dgvRegions.Rows[e.RowIndex].Tag as RegionDisplayItem;
                    if (item != null)
                    {
                        item.IsEnabled = Convert.ToBoolean(dgvRegions.Rows[e.RowIndex].Cells[0].Value);
                    }
                }
            };

            dgvRegions.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (dgvRegions.IsCurrentCellDirty && dgvRegions.CurrentCellAddress.X == 0)
                {
                    dgvRegions.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };
        }

        // ═══════════════════════════════════════════════════════════════
        //  LOAD DATA & RESTORE SETTINGS
        // ═══════════════════════════════════════════════════════════════
        private void RestoreLastSettings()
        {
            this.Size = _lastFormSize;
            chkOnlyCenterline.Checked = _lastOnlyCenterline;
            chkFilterInside.Checked = _lastFilterInsideOnly;
            chkAutoRebuild.Checked = _lastAutoRebuild;

            if (!_lastCorridorId.IsNull && _lastCorridorId.IsValid && !_lastCorridorId.IsErased)
            {
                SelectedCorridorId = _lastCorridorId;
            }

            if (!_lastPolylineId.IsNull && _lastPolylineId.IsValid && !_lastPolylineId.IsErased)
            {
                SelectedPolylineId = _lastPolylineId;
                UpdatePolylineDisplay(SelectedPolylineId);
            }
        }

        private void SaveCurrentSettings()
        {
            _lastCorridorId = SelectedCorridorId;
            _lastPolylineId = SelectedPolylineId;
            _lastOnlyCenterline = chkOnlyCenterline.Checked;
            _lastFilterInsideOnly = chkFilterInside.Checked;
            _lastAutoRebuild = chkAutoRebuild.Checked;
            _lastFormSize = this.Size;
        }

        private void LoadCorridors()
        {
            cmbCorridor.Items.Clear();
            try
            {
                using var tr = _db.TransactionManager.StartTransaction();
                var cdoc = Autodesk.Civil.ApplicationServices.CivilApplication.ActiveDocument;
                if (cdoc != null)
                {
                    foreach (ObjectId id in cdoc.CorridorCollection)
                    {
                        if (tr.GetObject(id, OpenMode.ForRead) is Corridor c)
                        {
                            var item = new CorridorItem(c.Name, id);
                            cmbCorridor.Items.Add(item);
                            if (id == SelectedCorridorId)
                            {
                                cmbCorridor.SelectedItem = item;
                            }
                        }
                    }
                }
                tr.Commit();
            }
            catch { }

            if (cmbCorridor.SelectedIndex == -1 && cmbCorridor.Items.Count > 0)
            {
                cmbCorridor.SelectedIndex = 0;
            }
        }

        private void UpdatePolylineDisplay(ObjectId plineId)
        {
            try
            {
                using var tr = _db.TransactionManager.StartTransaction();
                if (tr.GetObject(plineId, OpenMode.ForRead) is Polyline pl)
                {
                    txtPolylineInfo.Text = $"Layer: {pl.Layer} | {pl.NumberOfVertices} đỉnh | L={pl.Length:F1}m";
                }
                else if (tr.GetObject(plineId, OpenMode.ForRead) is Curve cv)
                {
                    txtPolylineInfo.Text = $"Layer: {cv.Layer} | L={cv.GetDistanceAtParameter(cv.EndParam):F1}m";
                }
                tr.Commit();
            }
            catch
            {
                txtPolylineInfo.Text = "Polyline ID hợp lệ";
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  CANVAS INTERACTION (PICK CORRIDOR / POLYLINE)
        // ═══════════════════════════════════════════════════════════════
        private void BtnPickCorridor_Click(object sender, EventArgs e)
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
                LoadCorridors();
                AnalyzeRegions();
            }
        }

        private void BtnPickPolyline_Click(object sender, EventArgs e)
        {
            using var interaction = _ed.StartUserInteraction(this);
            var peo = new PromptEntityOptions("\nChọn Polyline ranh giới kín trên bản vẽ: ");
            peo.SetRejectMessage("\nĐối tượng phải là Polyline (LWPOLYLINE hoặc POLYLINE).");
            peo.AddAllowedClass(typeof(Polyline), true);
            peo.AddAllowedClass(typeof(Polyline2d), true);
            peo.AddAllowedClass(typeof(Polyline3d), true);

            var per = _ed.GetEntity(peo);
            interaction.End();

            if (per.Status == PromptStatus.OK)
            {
                SelectedPolylineId = per.ObjectId;
                UpdatePolylineDisplay(SelectedPolylineId);
                AnalyzeRegions();
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  GEOMETRY ANALYSIS (POLYLINE & CORRIDOR REGIONS)
        // ═══════════════════════════════════════════════════════════════
        public void AnalyzeRegions()
        {
            RegionItems.Clear();

            if (SelectedCorridorId.IsNull || !SelectedCorridorId.IsValid)
            {
                lblSummary.Text = "Chưa chọn Corridor.";
                RefreshGrid();
                return;
            }

            try
            {
                using var tr = _db.TransactionManager.StartTransaction();
                var corridor = tr.GetObject(SelectedCorridorId, OpenMode.ForRead) as Corridor;
                if (corridor == null || corridor.Baselines.Count == 0)
                {
                    lblSummary.Text = "Corridor không có Baseline nào.";
                    tr.Commit();
                    RefreshGrid();
                    return;
                }

                // Lấy đa giác từ Polyline nếu có
                List<Point2d> polygon = null;
                Curve boundaryCurve = null;
                if (!SelectedPolylineId.IsNull && SelectedPolylineId.IsValid && !SelectedPolylineId.IsErased)
                {
                    boundaryCurve = tr.GetObject(SelectedPolylineId, OpenMode.ForRead) as Curve;
                    if (boundaryCurve is Polyline pl)
                    {
                        polygon = GetTessellatedPolygon(pl);
                    }
                    else if (boundaryCurve != null)
                    {
                        polygon = SampleCurveToPolygon(boundaryCurve);
                    }
                }

                // Duyệt qua tất cả Baselines và Regions
                for (int bIdx = 0; bIdx < corridor.Baselines.Count; bIdx++)
                {
                    var baseline = corridor.Baselines[bIdx];
                    var alignment = tr.GetObject(baseline.AlignmentId, OpenMode.ForRead) as Alignment;
                    string alignName = alignment?.Name ?? baseline.Name;

                    for (int rIdx = 0; rIdx < baseline.BaselineRegions.Count; rIdx++)
                    {
                        var region = baseline.BaselineRegions[rIdx];

                        // Lấy tên Assembly
                        string asmName = "N/A";
                        if (!region.AssemblyId.IsNull && region.AssemblyId.IsValid)
                        {
                            try
                            {
                                if (tr.GetObject(region.AssemblyId, OpenMode.ForRead) is Assembly asm)
                                {
                                    asmName = asm.Name;
                                }
                            }
                            catch { }
                        }

                        // Phân loại quan hệ không gian với Polyline
                        var relation = RegionSpatialRelation.Outside;
                        if (polygon != null && alignment != null)
                        {
                            relation = ClassifyRegion(region, alignment, polygon, boundaryCurve, chkOnlyCenterline.Checked);
                        }

                        var item = new RegionDisplayItem
                        {
                            BaselineIndex = bIdx,
                            RegionIndex = rIdx,
                            BaselineName = baseline.Name,
                            AlignmentName = alignName,
                            RegionName = region.Name,
                            AssemblyName = asmName,
                            AssemblyId = region.AssemblyId,
                            StartStation = region.StartStation,
                            EndStation = region.EndStation,
                            IsEnabled = region.NeedsProcessing,
                            OriginalIsEnabled = region.NeedsProcessing,
                            Relation = relation
                        };

                        RegionItems.Add(item);
                    }
                }

                tr.Commit();
            }
            catch (System.Exception ex)
            {
                _ed.WriteMessage($"\n[Lỗi phân tích Region]: {ex.Message}");
            }

            UpdateSummaryText();
            RefreshGrid();
        }

        private void UpdateSummaryText()
        {
            if (RegionItems.Count == 0)
            {
                lblSummary.Text = "Không tìm thấy Region nào.";
                return;
            }

            int insideCount = RegionItems.Count(r => r.Relation == RegionSpatialRelation.FullyInside);
            int intersectCount = RegionItems.Count(r => r.Relation == RegionSpatialRelation.Intersecting);
            int outsideCount = RegionItems.Count(r => r.Relation == RegionSpatialRelation.Outside);
            int enabledCount = RegionItems.Count(r => r.IsEnabled);

            lblSummary.Text = $"Tổng: {RegionItems.Count} Region (Đang bật: {enabledCount}) | " +
                              $"Trong ranh giới: {insideCount} | Giao cắt: {intersectCount} | Ngoài: {outsideCount}";

            btnSplitIntersecting.Enabled = intersectCount > 0;
            btnDisableInside.Enabled = insideCount > 0;
            btnEnableInside.Enabled = insideCount > 0;
            btnToggleInside.Enabled = insideCount > 0;
        }

        private void RefreshGrid()
        {
            dgvRegions.Rows.Clear();

            bool filterInside = chkFilterInside.Checked;
            var displayList = filterInside
                ? RegionItems.Where(r => r.Relation != RegionSpatialRelation.Outside).ToList()
                : RegionItems;

            foreach (var item in displayList)
            {
                int rowIndex = dgvRegions.Rows.Add(
                    item.IsEnabled,
                    item.RegionName,
                    $"{item.AlignmentName} ({item.BaselineName})",
                    item.AssemblyName,
                    $"{item.StartStation:F2}",
                    $"{item.EndStation:F2}",
                    $"{item.Length:F2}",
                    item.RelationDisplay
                );

                var row = dgvRegions.Rows[rowIndex];
                row.Tag = item;

                // Tô màu theo trạng thái không gian
                if (item.Relation == RegionSpatialRelation.FullyInside)
                {
                    row.DefaultCellStyle.BackColor = WinFormsColor.FromArgb(235, 250, 235); // Light green
                    row.Cells[7].Style.ForeColor = WinFormsColor.DarkGreen;
                    row.Cells[7].Style.Font = new WinFormsFont(dgvRegions.Font, FontStyle.Bold);
                }
                else if (item.Relation == RegionSpatialRelation.Intersecting)
                {
                    row.DefaultCellStyle.BackColor = WinFormsColor.FromArgb(255, 248, 230); // Light amber
                    row.Cells[7].Style.ForeColor = WinFormsColor.FromArgb(180, 100, 0);
                    row.Cells[7].Style.Font = new WinFormsFont(dgvRegions.Font, FontStyle.Bold);
                }
                else
                {
                    row.Cells[7].Style.ForeColor = WinFormsColor.Gray;
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  QUICK ACTIONS (BẬT / TẮT / ĐẢO TRẠNG THÁI)
        // ═══════════════════════════════════════════════════════════════
        private void SetInsideRegionsStatus(bool enable)
        {
            int count = 0;
            foreach (var item in RegionItems)
            {
                if (item.Relation == RegionSpatialRelation.FullyInside)
                {
                    item.IsEnabled = enable;
                    count++;
                }
            }
            RefreshGrid();
            UpdateSummaryText();
            MessageBox.Show($"Đã {(enable ? "BẬT" : "TẮT")} {count} Region nằm trọn trong Polyline.",
                "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ToggleInsideRegionsStatus()
        {
            int count = 0;
            foreach (var item in RegionItems)
            {
                if (item.Relation == RegionSpatialRelation.FullyInside)
                {
                    item.IsEnabled = !item.IsEnabled;
                    count++;
                }
            }
            RefreshGrid();
            UpdateSummaryText();
        }

        private void SetAllStatus(bool enable)
        {
            foreach (var item in RegionItems)
            {
                item.IsEnabled = enable;
            }
            RefreshGrid();
            UpdateSummaryText();
        }

        // ═══════════════════════════════════════════════════════════════
        //  CHIA NHỎ (SPLIT) CÁC REGION GIAO CẮT
        // ═══════════════════════════════════════════════════════════════
        private void BtnSplitIntersecting_Click(object sender, EventArgs e)
        {
            var intersectingItems = RegionItems.Where(r => r.Relation == RegionSpatialRelation.Intersecting).ToList();
            if (intersectingItems.Count == 0)
            {
                MessageBox.Show("Không có Region nào đang giao cắt với Polyline ranh giới.",
                    "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var confirm = MessageBox.Show(
                $"Tìm thấy {intersectingItems.Count} Region giao cắt với Polyline ranh giới.\n\n" +
                "Bạn có muốn tự động chia nhỏ (Split) các Region này tại vị trí giao cắt để tạo thành các đoạn nằm trọn bên trong ranh giới không?",
                "Xác nhận chia nhỏ Region", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            int splitCount = SplitRegionsAtBoundary();
            if (splitCount > 0)
            {
                MessageBox.Show($"Đã chia nhỏ thành công {splitCount} vị trí giao cắt!\nDanh sách Region đã được cập nhật lại.",
                    "Hoàn tất chia nhỏ", MessageBoxButtons.OK, MessageBoxIcon.Information);
                AnalyzeRegions();
            }
            else
            {
                MessageBox.Show("Không thể chia nhỏ thêm Region nào (vị trí giao cắt có thể quá gần điểm đầu/cuối).",
                    "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        public int SplitRegionsAtBoundary()
        {
            if (SelectedCorridorId.IsNull || SelectedPolylineId.IsNull) return 0;

            int totalSplits = 0;

            using (var tr = _db.TransactionManager.StartTransaction())
            {
                try
                {
                    var corridor = tr.GetObject(SelectedCorridorId, OpenMode.ForWrite) as Corridor;
                    var boundaryCurve = tr.GetObject(SelectedPolylineId, OpenMode.ForRead) as Curve;

                    if (corridor == null || boundaryCurve == null)
                    {
                        tr.Commit();
                        return 0;
                    }

                    var plane = new Plane(Point3d.Origin, Vector3d.ZAxis);

                    foreach (Baseline baseline in corridor.Baselines)
                    {
                        var alignment = tr.GetObject(baseline.AlignmentId, OpenMode.ForRead) as Alignment;
                        if (alignment == null) continue;

                        // Tìm tất cả giao điểm 2D giữa Alignment và Boundary Curve
                        var pts = new Point3dCollection();
                        alignment.IntersectWith(boundaryCurve, Intersect.OnBothOperands, plane, pts, IntPtr.Zero, IntPtr.Zero);

                        if (pts.Count == 0) continue;

                        // Chuyển thành danh sách lý trình và sắp xếp
                        var splitStations = new List<double>();
                        foreach (Point3d pt in pts)
                        {
                            double st = 0, off = 0;
                            alignment.StationOffset(pt.X, pt.Y, ref st, ref off);
                            if (st >= alignment.StartingStation && st <= alignment.EndingStation)
                            {
                                splitStations.Add(st);
                            }
                        }

                        splitStations = splitStations.Distinct().OrderBy(s => s).ToList();

                        // Thực hiện cắt từng lý trình
                        foreach (double splitStation in splitStations)
                        {
                            // Tìm Region đang chứa lý trình này
                            BaselineRegion targetRegion = null;
                            foreach (BaselineRegion reg in baseline.BaselineRegions)
                            {
                                // Cách điểm đầu/cuối tối thiểu 0.1m
                                if (splitStation > reg.StartStation + 0.1 && splitStation < reg.EndStation - 0.1)
                                {
                                    targetRegion = reg;
                                    break;
                                }
                            }

                            if (targetRegion == null) continue;

                            // Tiến hành chia Region
                            bool success = false;
                            try
                            {
                                // Thử phương thức Split có sẵn của API
                                targetRegion.Split(splitStation);
                                success = true;
                                totalSplits++;
                            }
                            catch
                            {
                                // Fallback: Chia thủ công 2 bước
                                try
                                {
                                    double origEnd = targetRegion.EndStation;
                                    ObjectId asmId = targetRegion.AssemblyId;
                                    var origTargets = targetRegion.GetTargets();

                                    targetRegion.EndStation = splitStation;

                                    string newName = $"{targetRegion.Name}_Cut_{Math.Round(splitStation)}";
                                    int counter = 1;
                                    while (baseline.BaselineRegions.Cast<BaselineRegion>().Any(r => r.Name.Equals(newName, StringComparison.OrdinalIgnoreCase)))
                                    {
                                        newName = $"{targetRegion.Name}_Cut_{Math.Round(splitStation)}_{counter++}";
                                    }

                                    var newReg = baseline.BaselineRegions.Add(newName, asmId, splitStation, origEnd);
                                    if (origTargets.Count > 0)
                                    {
                                        try { newReg.SetTargets(origTargets); } catch { }
                                    }

                                    success = true;
                                    totalSplits++;
                                }
                                catch (System.Exception ex2)
                                {
                                    _ed.WriteMessage($"\n[Lỗi chia nhỏ fallback]: {ex2.Message}");
                                }
                            }
                        }
                    }

                    if (totalSplits > 0)
                    {
                        try { corridor.Rebuild(); } catch { }
                    }

                    tr.Commit();
                }
                catch (System.Exception ex)
                {
                    _ed.WriteMessage($"\n[Lỗi chia nhỏ Region]: {ex.Message}");
                    tr.Abort();
                }
            }

            return totalSplits;
        }

        // ═══════════════════════════════════════════════════════════════
        //  THỰC THI (APPLY)
        // ═══════════════════════════════════════════════════════════════
        private void BtnApply_Click(object sender, EventArgs e)
        {
            if (SelectedCorridorId.IsNull || !SelectedCorridorId.IsValid)
            {
                MessageBox.Show("Vui lòng chọn Corridor trước khi áp dụng.",
                    "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            FormAccepted = true;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        // ═══════════════════════════════════════════════════════════════
        //  GEOMETRIC HELPERS (RAY-CASTING & POINT IN POLYGON)
        // ═══════════════════════════════════════════════════════════════
        private static RegionSpatialRelation ClassifyRegion(BaselineRegion region, Alignment alignment,
            List<Point2d> polygon, Curve boundaryCurve, bool onlyCenterline)
        {
            double len = region.EndStation - region.StartStation;
            if (len <= 0) return RegionSpatialRelation.Outside;

            // Lấy mẫu dọc theo tim tuyến (tối thiểu 6 mẫu, bước nhảy tối đa 5m)
            int samples = Math.Max(6, (int)Math.Ceiling(len / 5.0));
            double step = len / (samples - 1);

            int insideCount = 0;
            int totalPoints = 0;

            for (int i = 0; i < samples; i++)
            {
                double st = region.StartStation + i * step;
                if (st > region.EndStation) st = region.EndStation;

                double x = 0, y = 0;
                try
                {
                    alignment.PointLocation(st, 0, ref x, ref y);
                    totalPoints++;
                    if (IsPointInPolygon(new Point2d(x, y), polygon))
                    {
                        insideCount++;
                    }
                }
                catch { }
            }

            if (totalPoints == 0) return RegionSpatialRelation.Outside;

            if (insideCount == totalPoints)
            {
                return RegionSpatialRelation.FullyInside;
            }
            if (insideCount == 0)
            {
                return RegionSpatialRelation.Outside;
            }
            return RegionSpatialRelation.Intersecting;
        }

        public static bool IsPointInPolygon(Point2d pt, List<Point2d> polygon)
        {
            if (polygon == null || polygon.Count < 3) return false;

            bool inside = false;
            int n = polygon.Count;

            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                if (((polygon[i].Y > pt.Y) != (polygon[j].Y > pt.Y)) &&
                    (pt.X < (polygon[j].X - polygon[i].X) * (pt.Y - polygon[i].Y) /
                     (polygon[j].Y - polygon[i].Y + 1e-12) + polygon[i].X))
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private static List<Point2d> GetTessellatedPolygon(Polyline pline, double maxSegmentLength = 2.0)
        {
            var polygon = new List<Point2d>();
            int numVerts = pline.NumberOfVertices;

            for (int i = 0; i < numVerts; i++)
            {
                polygon.Add(pline.GetPoint2dAt(i));
                double bulge = pline.GetBulgeAt(i);

                if (Math.Abs(bulge) > 1e-6)
                {
                    // Phân rã cung tròn
                    double startDist = pline.GetDistanceAtParameter(i);
                    double endParam = (i == numVerts - 1) ? (pline.Closed ? numVerts : i) : (i + 1);
                    double endDist = pline.GetDistanceAtParameter(endParam);
                    double arcLen = Math.Abs(endDist - startDist);

                    int arcSamples = Math.Max(2, (int)Math.Ceiling(arcLen / maxSegmentLength));
                    double step = arcLen / arcSamples;

                    for (int s = 1; s < arcSamples; s++)
                    {
                        double d = startDist + s * step;
                        Point3d samplePt = pline.GetPointAtDist(d);
                        polygon.Add(new Point2d(samplePt.X, samplePt.Y));
                    }
                }
            }

            return polygon;
        }

        private static List<Point2d> SampleCurveToPolygon(Curve cv, double maxSegmentLength = 2.0)
        {
            var polygon = new List<Point2d>();
            try
            {
                double len = cv.GetDistanceAtParameter(cv.EndParam) - cv.GetDistanceAtParameter(cv.StartParam);
                int samples = Math.Max(8, (int)Math.Ceiling(len / maxSegmentLength));
                double step = len / samples;

                for (int i = 0; i <= samples; i++)
                {
                    double d = i * step;
                    Point3d pt = cv.GetPointAtDist(d);
                    polygon.Add(new Point2d(pt.X, pt.Y));
                }
            }
            catch { }
            return polygon;
        }
    }
}

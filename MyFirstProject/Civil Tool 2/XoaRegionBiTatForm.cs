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
    /// Model hiển thị thông tin Region bị tắt trong Corridor
    /// </summary>
    public class DisabledRegionItem
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
        public string AssemblyName { get; set; } = string.Empty;
        public ObjectId AssemblyId { get; set; } = ObjectId.Null;
        public bool IsSelectedForDeletion { get; set; } = true;
    }

    /// <summary>
    /// Item dùng cho ComboBox danh sách Corridor
    /// </summary>
    public class CorridorComboItem
    {
        public ObjectId Id { get; }
        public string Name { get; }

        public CorridorComboItem(ObjectId id, string name)
        {
            Id = id;
            Name = name;
        }

        public override string ToString() => Name;
    }

    public class XoaRegionBiTatForm : Form
    {
        // ═══════════════════════════════════════════════════════════════
        //  PERSISTENT STATE (RULE 2)
        // ═══════════════════════════════════════════════════════════════
        private static ObjectId _lastCorridorId = ObjectId.Null;
        private static bool _lastScanAllCorridors = false;
        private static bool _lastAutoRebuild = true;
        private static bool _lastDeleteEmptyBaseline = false;
        private static Size _lastFormSize = new Size(1020, 640);

        // ═══════════════════════════════════════════════════════════════
        //  FIELDS & PROPERTIES
        // ═══════════════════════════════════════════════════════════════
        private readonly Database _db;
        private readonly Editor _ed;

        public List<DisabledRegionItem> DisabledRegions { get; private set; } = new List<DisabledRegionItem>();
        public List<DisabledRegionItem> SelectedRegionsToDelete { get; private set; } = new List<DisabledRegionItem>();
        public bool FormAccepted { get; private set; } = false;
        public bool AutoRebuild => chkAutoRebuild.Checked;
        public bool DeleteEmptyBaseline => chkDeleteEmptyBaseline.Checked;
        public ObjectId SelectedCorridorId { get; private set; } = ObjectId.Null;

        // UI Controls
        private RadioButton radSelectedCorridor;
        private RadioButton radAllCorridors;
        private ComboBox cmbCorridors;
        private Button btnPickCorridor;
        private Button btnRefresh;

        private WinFormsLabel lblSummary;
        private DataGridView dgvRegions;
        private Button btnSelectAll;
        private Button btnUnselectAll;
        private Button btnInvertSelection;
        private Button btnZoom;

        private CheckBox chkAutoRebuild;
        private CheckBox chkDeleteEmptyBaseline;
        private Button btnExecute;
        private Button btnCancel;

        public XoaRegionBiTatForm(Database db, Editor ed)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _ed = ed ?? throw new ArgumentNullException(nameof(ed));

            InitializeComponent();
            RestoreLastSettings();
            LoadCorridors();
            AnalyzeDisabledRegions();
        }

        private void InitializeComponent()
        {
            this.Text = "Xóa Các Corridor Region Đang Bị Tắt (Bypass)";
            this.Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Regular);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(880, 500);
            this.Size = _lastFormSize;
            this.BackColor = WinFormsColor.FromArgb(246, 248, 250);

            // ─────────────────────────────────────────────────────────────
            // Header Panel
            // ─────────────────────────────────────────────────────────────
            var pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 62,
                BackColor = WinFormsColor.FromArgb(24, 43, 73),
                Padding = new Padding(15, 8, 15, 8)
            };

            var lblTitle = new WinFormsLabel
            {
                Text = "🗑 QUẢN LÝ & XÓA CÁC CORRIDOR REGION ĐANG BỊ TẮT",
                Font = new WinFormsFont("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = WinFormsColor.White,
                AutoSize = true,
                Location = new Point(14, 8)
            };

            var lblSubtitle = new WinFormsLabel
            {
                Text = "Tự động rà soát các Baseline Region có trạng thái Bypass / NeedsProcessing = False để dọn dẹp Corridor sạch sẽ.",
                Font = new WinFormsFont("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = WinFormsColor.FromArgb(200, 220, 245),
                AutoSize = true,
                Location = new Point(16, 33)
            };

            pnlHeader.Controls.AddRange(new Control[] { lblTitle, lblSubtitle });

            // ─────────────────────────────────────────────────────────────
            // Group 1: Phạm vi & Nguồn Corridor
            // ─────────────────────────────────────────────────────────────
            var grpScope = new GroupBox
            {
                Text = " 1. Phạm vi kiểm tra Corridor ",
                Location = new Point(12, 72),
                Size = new Size(this.ClientSize.Width - 24, 75),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold)
            };

            radSelectedCorridor = new RadioButton
            {
                Text = "Chỉ Corridor được chọn:",
                Location = new Point(15, 28),
                Size = new Size(165, 24),
                Checked = true,
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Regular)
            };
            radSelectedCorridor.CheckedChanged += (s, e) =>
            {
                cmbCorridors.Enabled = radSelectedCorridor.Checked;
                btnPickCorridor.Enabled = radSelectedCorridor.Checked;
                if (radSelectedCorridor.Checked)
                {
                    if (cmbCorridors.SelectedItem is CorridorComboItem item)
                    {
                        SelectedCorridorId = item.Id;
                    }
                    AnalyzeDisabledRegions();
                }
            };

            cmbCorridors = new ComboBox
            {
                Location = new Point(185, 27),
                Size = new Size(260, 24),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Regular)
            };
            cmbCorridors.SelectedIndexChanged += (s, e) =>
            {
                if (cmbCorridors.SelectedItem is CorridorComboItem item)
                {
                    SelectedCorridorId = item.Id;
                    AnalyzeDisabledRegions();
                }
            };

            btnPickCorridor = new Button
            {
                Text = "🎯 Chọn trên CAD",
                Location = new Point(455, 26),
                Size = new Size(125, 27),
                Font = new WinFormsFont("Segoe UI", 8.5F, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            btnPickCorridor.Click += BtnPickCorridor_Click;

            radAllCorridors = new RadioButton
            {
                Text = "Quét TẤT CẢ Corridor trong bản vẽ",
                Location = new Point(600, 28),
                Size = new Size(240, 24),
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Regular)
            };
            radAllCorridors.CheckedChanged += (s, e) =>
            {
                if (radAllCorridors.Checked)
                {
                    cmbCorridors.Enabled = false;
                    btnPickCorridor.Enabled = false;
                    AnalyzeDisabledRegions();
                }
            };

            btnRefresh = new Button
            {
                Text = "🔄 Quét lại",
                Location = new Point(grpScope.Width - 110, 26),
                Size = new Size(95, 27),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Font = new WinFormsFont("Segoe UI", 8.5F, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            btnRefresh.Click += (s, e) =>
            {
                LoadCorridors();
                AnalyzeDisabledRegions();
            };

            grpScope.Controls.AddRange(new Control[] {
                radSelectedCorridor, cmbCorridors, btnPickCorridor,
                radAllCorridors, btnRefresh
            });

            // ─────────────────────────────────────────────────────────────
            // Group 2: Danh sách Region đang bị tắt
            // ─────────────────────────────────────────────────────────────
            var grpRegions = new GroupBox
            {
                Text = " 2. Danh sách Region đang bị tắt (Bypass / NeedsProcessing = False) ",
                Location = new Point(12, 155),
                Size = new Size(this.ClientSize.Width - 24, this.ClientSize.Height - 310),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold)
            };

            // Thanh công cụ phía trên DataGridView
            var pnlToolbar = new Panel
            {
                Location = new Point(12, 24),
                Size = new Size(grpRegions.Width - 24, 32),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            lblSummary = new WinFormsLabel
            {
                Text = "Đang kiểm tra dữ liệu...",
                Location = new Point(0, 7),
                Size = new Size(450, 20),
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = WinFormsColor.FromArgb(180, 40, 20)
            };

            btnSelectAll = new Button
            {
                Text = "✔ Chọn tất cả",
                Location = new Point(pnlToolbar.Width - 410, 2),
                Size = new Size(95, 27),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Font = new WinFormsFont("Segoe UI", 8.5F, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            btnSelectAll.Click += (s, e) => SetAllSelection(true);

            btnUnselectAll = new Button
            {
                Text = "✖ Bỏ chọn hết",
                Location = new Point(pnlToolbar.Width - 310, 2),
                Size = new Size(95, 27),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Font = new WinFormsFont("Segoe UI", 8.5F, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            btnUnselectAll.Click += (s, e) => SetAllSelection(false);

            btnInvertSelection = new Button
            {
                Text = "🔄 Đảo chọn",
                Location = new Point(pnlToolbar.Width - 210, 2),
                Size = new Size(85, 27),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Font = new WinFormsFont("Segoe UI", 8.5F, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            btnInvertSelection.Click += (s, e) => InvertSelection();

            btnZoom = new Button
            {
                Text = "🔍 Zoom Region",
                Location = new Point(pnlToolbar.Width - 120, 2),
                Size = new Size(115, 27),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Font = new WinFormsFont("Segoe UI", 8.5F, FontStyle.Bold),
                BackColor = WinFormsColor.FromArgb(235, 243, 255),
                Cursor = Cursors.Hand
            };
            btnZoom.Click += BtnZoom_Click;

            pnlToolbar.Controls.AddRange(new Control[] {
                lblSummary, btnSelectAll, btnUnselectAll, btnInvertSelection, btnZoom
            });

            // DataGridView
            dgvRegions = new DataGridView
            {
                Location = new Point(12, 60),
                Size = new Size(grpRegions.Width - 24, grpRegions.Height - 70),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackgroundColor = WinFormsColor.White,
                BorderStyle = BorderStyle.Fixed3D,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoGenerateColumns = false,
                Font = new WinFormsFont("Segoe UI", 8.5F, FontStyle.Regular),
                EnableHeadersVisualStyles = false
            };

            dgvRegions.ColumnHeadersDefaultCellStyle.BackColor = WinFormsColor.FromArgb(240, 243, 248);
            dgvRegions.ColumnHeadersDefaultCellStyle.Font = new WinFormsFont("Segoe UI", 8.5F, FontStyle.Bold);
            dgvRegions.ColumnHeadersHeight = 30;
            dgvRegions.RowTemplate.Height = 26;

            SetupGridColumns();

            dgvRegions.CellContentClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex == 0)
                {
                    dgvRegions.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };
            dgvRegions.CellValueChanged += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex == 0)
                {
                    if (dgvRegions.Rows[e.RowIndex].Tag is DisabledRegionItem item)
                    {
                        item.IsSelectedForDeletion = Convert.ToBoolean(dgvRegions.Rows[e.RowIndex].Cells[0].Value);
                        UpdateSummaryCount();
                    }
                }
            };
            dgvRegions.CellDoubleClick += (s, e) =>
            {
                if (e.RowIndex >= 0)
                {
                    BtnZoom_Click(s, e);
                }
            };

            grpRegions.Controls.AddRange(new Control[] { pnlToolbar, dgvRegions });

            // ─────────────────────────────────────────────────────────────
            // Group 3: Tùy chọn thực thi
            // ─────────────────────────────────────────────────────────────
            var grpOptions = new GroupBox
            {
                Text = " 3. Tùy chọn khi xóa ",
                Location = new Point(12, this.ClientSize.Height - 145),
                Size = new Size(this.ClientSize.Width - 24, 60),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold)
            };

            chkAutoRebuild = new CheckBox
            {
                Text = "Tự động Rebuild Corridor sau khi xóa (Khuyến nghị để cập nhật mô hình ngay)",
                Location = new Point(15, 24),
                Size = new Size(520, 24),
                Checked = true,
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Regular)
            };

            chkDeleteEmptyBaseline = new CheckBox
            {
                Text = "Xóa Baseline nếu Baseline không còn Region nào",
                Location = new Point(560, 24),
                Size = new Size(360, 24),
                Checked = false,
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Regular)
            };

            grpOptions.Controls.AddRange(new Control[] { chkAutoRebuild, chkDeleteEmptyBaseline });

            // ─────────────────────────────────────────────────────────────
            // Footer Panel (Buttons)
            // ─────────────────────────────────────────────────────────────
            var pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 65,
                BackColor = WinFormsColor.FromArgb(238, 242, 246),
                Padding = new Padding(15, 12, 15, 12)
            };

            btnExecute = new Button
            {
                Text = "🗑 XÓA CÁC REGION ĐÃ CHỌN",
                Location = new Point(pnlBottom.Width - 290, 14),
                Size = new Size(200, 38),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                BackColor = WinFormsColor.FromArgb(215, 45, 30),
                ForeColor = WinFormsColor.White,
                Font = new WinFormsFont("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                FlatStyle = FlatStyle.Flat
            };
            btnExecute.FlatAppearance.BorderSize = 0;
            btnExecute.Click += BtnExecute_Click;

            btnCancel = new Button
            {
                Text = "Đóng",
                Location = new Point(pnlBottom.Width - 80, 14),
                Size = new Size(70, 38),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            btnCancel.Click += (s, e) => this.Close();

            pnlBottom.Controls.AddRange(new Control[] { btnExecute, btnCancel });

            // Thêm controls vào Form
            this.Controls.AddRange(new Control[] {
                pnlHeader, grpScope, grpRegions, grpOptions, pnlBottom
            });

            this.FormClosing += (s, e) => SaveCurrentSettings();
        }

        private void SetupGridColumns()
        {
            dgvRegions.Columns.Clear();

            var colCheck = new DataGridViewCheckBoxColumn
            {
                HeaderText = "Xóa?",
                Width = 50,
                DataPropertyName = nameof(DisabledRegionItem.IsSelectedForDeletion)
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
                Width = 150,
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

            var colAssembly = new DataGridViewTextBoxColumn
            {
                HeaderText = "Assembly (Mặt cắt)",
                Width = 140,
                ReadOnly = true
            };

            var colStatus = new DataGridViewTextBoxColumn
            {
                HeaderText = "Trạng thái",
                Width = 120,
                ReadOnly = true
            };

            dgvRegions.Columns.AddRange(new DataGridViewColumn[] {
                colCheck, colStt, colCorridor, colBaseline, colRegion,
                colStartStation, colEndStation, colLength, colAssembly, colStatus
            });
        }

        // ═══════════════════════════════════════════════════════════════
        //  PERSISTENT STATE METHODS
        // ═══════════════════════════════════════════════════════════════
        private void RestoreLastSettings()
        {
            try
            {
                if (_lastFormSize.Width >= this.MinimumSize.Width && _lastFormSize.Height >= this.MinimumSize.Height)
                {
                    this.Size = _lastFormSize;
                }

                chkAutoRebuild.Checked = _lastAutoRebuild;
                chkDeleteEmptyBaseline.Checked = _lastDeleteEmptyBaseline;

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
                _lastAutoRebuild = chkAutoRebuild.Checked;
                _lastDeleteEmptyBaseline = chkDeleteEmptyBaseline.Checked;
                if (!SelectedCorridorId.IsNull && SelectedCorridorId.IsValid)
                {
                    _lastCorridorId = SelectedCorridorId;
                }
            }
            catch { }
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
                if (c3dDoc == null || c3dDoc.CorridorCollection.Count == 0)
                {
                    tr.Commit();
                    return;
                }

                CorridorComboItem? itemToSelect = null;

                foreach (ObjectId corrId in c3dDoc.CorridorCollection)
                {
                    if (tr.GetObject(corrId, OpenMode.ForRead) is Corridor corr)
                    {
                        var comboItem = new CorridorComboItem(corrId, corr.Name);
                        cmbCorridors.Items.Add(comboItem);

                        if (corrId == _lastCorridorId)
                        {
                            itemToSelect = comboItem;
                        }
                    }
                }

                tr.Commit();

                if (itemToSelect != null)
                {
                    cmbCorridors.SelectedItem = itemToSelect;
                    SelectedCorridorId = itemToSelect.Id;
                }
                else if (cmbCorridors.Items.Count > 0)
                {
                    cmbCorridors.SelectedIndex = 0;
                    SelectedCorridorId = ((CorridorComboItem)cmbCorridors.Items[0]).Id;
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

        private void AnalyzeDisabledRegions()
        {
            DisabledRegions.Clear();
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

                int totalRegionsScanned = 0;

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
                            totalRegionsScanned++;
                            var region = baseline.BaselineRegions[rIdx];

                            // Region bị TẮT khi NeedsProcessing == false
                            if (!region.NeedsProcessing)
                            {
                                string asmName = string.Empty;
                                if (!region.AssemblyId.IsNull && region.AssemblyId.IsValid)
                                {
                                    if (tr.GetObject(region.AssemblyId, OpenMode.ForRead) is Assembly asm)
                                    {
                                        asmName = asm.Name;
                                    }
                                }

                                var item = new DisabledRegionItem
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
                                    AssemblyName = asmName,
                                    AssemblyId = region.AssemblyId,
                                    IsSelectedForDeletion = true
                                };

                                DisabledRegions.Add(item);
                            }
                        }
                    }
                }

                tr.Commit();
                PopulateGrid();
                UpdateSummaryCount(totalRegionsScanned);
            }
            catch (System.Exception ex)
            {
                _ed.WriteMessage($"\n[Lỗi phân tích Region]: {ex.Message}");
            }
        }

        private void PopulateGrid()
        {
            dgvRegions.Rows.Clear();

            for (int i = 0; i < DisabledRegions.Count; i++)
            {
                var item = DisabledRegions[i];
                int rowIdx = dgvRegions.Rows.Add(
                    item.IsSelectedForDeletion,
                    (i + 1).ToString(),
                    item.CorridorName,
                    item.BaselineName,
                    item.RegionName,
                    item.StartStation.ToString("F2"),
                    item.EndStation.ToString("F2"),
                    item.Length.ToString("F2"),
                    item.AssemblyName,
                    "⚠ Đang bị tắt"
                );

                var row = dgvRegions.Rows[rowIdx];
                row.Tag = item;
                row.DefaultCellStyle.ForeColor = WinFormsColor.FromArgb(160, 30, 20);
                row.Cells[9].Style.Font = new WinFormsFont(dgvRegions.Font, FontStyle.Bold);
            }
        }

        private void UpdateSummaryCount(int totalScanned = -1)
        {
            int selectedCount = DisabledRegions.Count(r => r.IsSelectedForDeletion);
            int totalDisabled = DisabledRegions.Count;

            if (totalDisabled == 0)
            {
                lblSummary.Text = "✅ Không phát hiện Region nào đang bị tắt.";
                lblSummary.ForeColor = WinFormsColor.FromArgb(20, 130, 40);
                btnExecute.Enabled = false;
            }
            else
            {
                lblSummary.Text = $"⚠ Tìm thấy {totalDisabled} Region đang bị tắt (Đã chọn xóa: {selectedCount})";
                lblSummary.ForeColor = WinFormsColor.FromArgb(190, 40, 20);
                btnExecute.Enabled = selectedCount > 0;
            }
        }

        private void SetAllSelection(bool isSelected)
        {
            foreach (var item in DisabledRegions)
            {
                item.IsSelectedForDeletion = isSelected;
            }

            foreach (DataGridViewRow row in dgvRegions.Rows)
            {
                row.Cells[0].Value = isSelected;
            }

            UpdateSummaryCount();
        }

        private void InvertSelection()
        {
            foreach (var item in DisabledRegions)
            {
                item.IsSelectedForDeletion = !item.IsSelectedForDeletion;
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
                AnalyzeDisabledRegions();
            }
        }

        private void BtnZoom_Click(object? sender, EventArgs e)
        {
            if (dgvRegions.CurrentRow == null || dgvRegions.CurrentRow.Tag is not DisabledRegionItem item)
            {
                MessageBox.Show("Vui lòng chọn một dòng Region trong bảng để Zoom.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                using var interaction = _ed.StartUserInteraction(this);
                using var tr = _db.TransactionManager.StartTransaction();

                // Lấy tim tuyến Alignment để tìm tọa độ
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

                        Point3d pt1 = new Point3d(eStart, nStart, 0);
                        Point3d pt2 = new Point3d(eEnd, nEnd, 0);
                        Point3d pt3 = new Point3d(eMid, nMid, 0);

                        Extents3d ext = new Extents3d();
                        ext.AddPoint(pt1);
                        ext.AddPoint(pt2);
                        ext.AddPoint(pt3);

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
            SelectedRegionsToDelete = DisabledRegions.Where(r => r.IsSelectedForDeletion).ToList();

            if (SelectedRegionsToDelete.Count == 0)
            {
                MessageBox.Show("Vui lòng tích chọn ít nhất một Region để xóa.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var confirmResult = MessageBox.Show(
                $"Bạn có chắc chắn muốn xóa {SelectedRegionsToDelete.Count} Region đang bị tắt đã chọn không?\n\n" +
                $"• Tự động Rebuild Corridor: {(chkAutoRebuild.Checked ? "CÓ" : "KHÔNG")}\n" +
                $"• Xóa Baseline rỗng nếu có: {(chkDeleteEmptyBaseline.Checked ? "CÓ" : "KHÔNG")}\n\n" +
                "⚠ LƯU Ý: Thao tác này sẽ xóa vĩnh viễn các Region này khỏi Corridor!",
                "Xác nhận xóa Region Corridor",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (confirmResult == DialogResult.Yes)
            {
                FormAccepted = true;
                SaveCurrentSettings();
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Aec.PropertyData.DatabaseServices;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using CadEntity = Autodesk.AutoCAD.DatabaseServices.Entity;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsButton = System.Windows.Forms.Button;
using WinFormsComboBox = System.Windows.Forms.ComboBox;
using Font = System.Drawing.Font;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace MyFirstProject.Civil_Tool_2
{
    /// <summary>
    /// Thông tin nhóm đối tượng 3D Solid / Body trích xuất từ Corridor trên bản vẽ
    /// </summary>
    public class CorridorSolidGroupItem
    {
        public string GroupKey { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty; // "3DSOLID" hoặc "BODY"
        public string CorridorName { get; set; } = string.Empty;
        public string CorridorHandle { get; set; } = string.Empty;
        public string SubassemblyName { get; set; } = string.Empty;
        public string CodeName { get; set; } = string.Empty;
        public string LayerName { get; set; } = string.Empty;
        public bool HasCorridorPropertySet { get; set; }
        public List<ObjectId> EntityIds { get; set; } = new List<ObjectId>();
        public int Count => EntityIds.Count;
        public bool IsSelected { get; set; } = true;
    }

    /// <summary>
    /// Form quản lý ngắt liên kết động (Dynamic Link: AeccDbCorridorSolidMgr)
    /// và dọn dẹp 3D Solid / Body sinh ra từ Corridor.
    /// </summary>
    public class NgatLienKetSolidCorridorForm : Form
    {
        // ═══════════════════════════════════════════════════════════════
        //  PERSISTENT STATE (RULE 2)
        // ═══════════════════════════════════════════════════════════════
        private static ObjectId _lastCorridorId = ObjectId.Null;
        private static bool _lastAllCorridors = true;
        private static bool _lastBreakDynamicLink = true;
        private static bool _lastDeleteSelectedEntities = true;
        private static bool _lastCleanPropertySets = false;
        private static bool _lastPurgePropertySetDefs = false;
        private static bool _lastAutoRebuild = false;
        private static Size _lastFormSize = new Size(1040, 680);

        // ═══════════════════════════════════════════════════════════════
        //  FIELDS & PROPERTIES
        // ═══════════════════════════════════════════════════════════════
        private readonly Database _db;
        private readonly Editor _ed;

        public List<CorridorSolidGroupItem> SolidGroups { get; private set; } = new List<CorridorSolidGroupItem>();
        public List<ObjectId> SelectedEntityIdsToProcess { get; private set; } = new List<ObjectId>();
        public bool HasDynamicLinkManager { get; private set; }
        public string DynamicLinkHandle { get; private set; } = string.Empty;

        public bool FormAccepted { get; private set; }
        public bool BreakDynamicLink => chkBreakDynamicLink.Checked;
        public bool DeleteSelectedEntities => chkDeleteSelectedEntities.Checked;
        public bool CleanPropertySets => chkCleanPropertySets.Checked;
        public bool PurgePropertySetDefs => chkPurgePropertySetDefs.Checked;
        public bool AutoRebuild => chkAutoRebuild.Checked;
        public bool ScanAllCorridors => radAllCorridors.Checked;
        public ObjectId SelectedCorridorId { get; private set; } = ObjectId.Null;

        // UI Controls
        private WinFormsLabel lblDynamicLinkStatus = null!;
        private RadioButton radAllCorridors = null!;
        private RadioButton radSelectedCorridor = null!;
        private WinFormsComboBox cmbCorridors = null!;
        private WinFormsButton btnPickCorridor = null!;
        private WinFormsButton btnPickSolids = null!;
        private WinFormsButton btnRefresh = null!;

        private WinFormsLabel lblSummary = null!;
        private DataGridView dgvGroups = null!;
        private WinFormsButton btnSelectAll = null!;
        private WinFormsButton btnUnselectAll = null!;
        private WinFormsButton btnSelectOnlyBody = null!;
        private WinFormsButton btnSelectOnlySolid = null!;
        private WinFormsButton btnZoomSelected = null!;

        private CheckBox chkBreakDynamicLink = null!;
        private CheckBox chkDeleteSelectedEntities = null!;
        private CheckBox chkCleanPropertySets = null!;
        private CheckBox chkPurgePropertySetDefs = null!;
        private CheckBox chkAutoRebuild = null!;
        private WinFormsButton btnExecute = null!;
        private WinFormsButton btnCancel = null!;
        private ToolTip _toolTip = null!;

        // GDI+ Fonts (Disposed properly)
        private Font _fontRegular = null!;
        private Font _fontBold = null!;
        private Font _fontTitle = null!;
        private Font _fontSubtitle = null!;
        private Font _fontButton = null!;
        private Font _fontExecute = null!;

        public NgatLienKetSolidCorridorForm(Database db, Editor ed)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _ed = ed ?? throw new ArgumentNullException(nameof(ed));

            InitializeComponent();
            RestoreLastSettings();
            LoadCorridors();
            ScanDrawingData();
        }

        private void InitializeComponent()
        {
            _fontRegular = new Font("Segoe UI", 9F, FontStyle.Regular);
            _fontBold = new Font("Segoe UI", 9F, FontStyle.Bold);
            _fontTitle = new Font("Segoe UI", 12F, FontStyle.Bold);
            _fontSubtitle = new Font("Segoe UI", 8.5F, FontStyle.Regular);
            _fontButton = new Font("Segoe UI", 8.5F, FontStyle.Regular);
            _fontExecute = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            _toolTip = new ToolTip();

            this.Text = "Ngắt Liên Kết Động & Xóa 3D Solid / Body Của Corridor";
            this.Font = _fontRegular;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(920, 580);
            this.Size = _lastFormSize;
            this.BackColor = Color.FromArgb(246, 248, 250);

            // ─────────────────────────────────────────────────────────────
            // Header Panel
            // ─────────────────────────────────────────────────────────────
            var pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 64,
                BackColor = Color.FromArgb(24, 43, 73),
                Padding = new Padding(15, 8, 15, 8)
            };

            var lblTitle = new WinFormsLabel
            {
                Text = "🔗 NGẮT LIÊN KẾT ĐỘNG & DỌN DẸP 3D SOLID / BODY CORRIDOR",
                Font = _fontTitle,
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(14, 8)
            };

            var lblSubtitle = new WinFormsLabel
            {
                Text = "Gỡ bỏ bộ quản lý AeccDbCorridorSolidMgr (Dynamic Link) để khi Rebuild Corridor không tự động sinh lại 3D Solid & Body.",
                Font = _fontSubtitle,
                ForeColor = Color.FromArgb(200, 220, 245),
                AutoSize = true,
                Location = new Point(16, 35)
            };

            pnlHeader.Controls.AddRange(new Control[] { lblTitle, lblSubtitle });

            // ─────────────────────────────────────────────────────────────
            // Group 1: Trạng thái Liên kết động & Phạm vi lọc
            // ─────────────────────────────────────────────────────────────
            var grpScope = new GroupBox
            {
                Text = " 1. Trạng thái Liên kết động (Dynamic Link) & Phạm vi lọc ",
                Location = new Point(12, 72),
                Size = new Size(this.ClientSize.Width - 24, 96),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Font = _fontBold
            };

            lblDynamicLinkStatus = new WinFormsLabel
            {
                Text = "Đang kiểm tra trạng thái AeccDbCorridorSolidMgr trong Named Objects Dictionary...",
                Location = new Point(15, 24),
                Size = new Size(grpScope.Width - 30, 22),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Font = _fontBold,
                ForeColor = Color.FromArgb(180, 40, 20)
            };

            radAllCorridors = new RadioButton
            {
                Text = "Toàn bộ bản vẽ (Tất cả Corridor)",
                Location = new Point(15, 55),
                Size = new Size(215, 24),
                Checked = true,
                Font = _fontRegular
            };
            radAllCorridors.CheckedChanged += (s, e) =>
            {
                UpdateScopeControlsState();
                if (radAllCorridors.Checked) ScanDrawingData();
            };

            radSelectedCorridor = new RadioButton
            {
                Text = "Lọc theo Corridor:",
                Location = new Point(235, 55),
                Size = new Size(130, 24),
                Font = _fontRegular
            };
            radSelectedCorridor.CheckedChanged += (s, e) =>
            {
                UpdateScopeControlsState();
                if (radSelectedCorridor.Checked)
                {
                    if (cmbCorridors.SelectedItem is CorridorComboItem item)
                    {
                        SelectedCorridorId = item.Id;
                    }
                    ScanDrawingData();
                }
            };

            cmbCorridors = new WinFormsComboBox
            {
                Location = new Point(365, 54),
                Size = new Size(230, 24),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = _fontRegular
            };
            cmbCorridors.SelectedIndexChanged += (s, e) =>
            {
                if (cmbCorridors.SelectedItem is CorridorComboItem item)
                {
                    SelectedCorridorId = item.Id;
                    if (radSelectedCorridor.Checked) ScanDrawingData();
                }
            };

            btnPickCorridor = new WinFormsButton
            {
                Text = "🎯 Chọn Corridor",
                Location = new Point(603, 53),
                Size = new Size(120, 27),
                Font = _fontButton,
                Cursor = Cursors.Hand
            };
            _toolTip.SetToolTip(btnPickCorridor, "Chọn trực tiếp đối tượng Corridor trên màn hình CAD");
            btnPickCorridor.Click += BtnPickCorridor_Click;

            btnPickSolids = new WinFormsButton
            {
                Text = "🎯 Chọn Solid/Body",
                Location = new Point(730, 53),
                Size = new Size(130, 27),
                Font = _fontButton,
                Cursor = Cursors.Hand
            };
            _toolTip.SetToolTip(btnPickSolids, "Quét chọn trực tiếp các đối tượng 3D Solid / Body trên màn hình CAD");
            btnPickSolids.Click += BtnPickSolids_Click;

            btnRefresh = new WinFormsButton
            {
                Text = "🔄 Quét lại",
                Location = new Point(grpScope.Width - 108, 53),
                Size = new Size(95, 27),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Font = _fontButton,
                Cursor = Cursors.Hand
            };
            _toolTip.SetToolTip(btnRefresh, "Quét lại dữ liệu liên kết động và các khối 3DSOLID / BODY trong bản vẽ");
            btnRefresh.Click += (s, e) =>
            {
                LoadCorridors();
                ScanDrawingData();
            };

            grpScope.Controls.AddRange(new Control[]
            {
                lblDynamicLinkStatus,
                radAllCorridors, radSelectedCorridor, cmbCorridors,
                btnPickCorridor, btnPickSolids, btnRefresh
            });

            // ─────────────────────────────────────────────────────────────
            // Group 2: Danh sách nhóm 3DSOLID & BODY phát hiện trên bản vẽ
            // ─────────────────────────────────────────────────────────────
            var grpGroups = new GroupBox
            {
                Text = " 2. Danh sách các nhóm 3D Solid & Body trên ModelSpace ",
                Location = new Point(12, 174),
                Size = new Size(this.ClientSize.Width - 24, this.ClientSize.Height - 355),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Font = _fontBold
            };

            var pnlToolbar = new Panel
            {
                Location = new Point(12, 22),
                Size = new Size(grpGroups.Width - 24, 32),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            lblSummary = new WinFormsLabel
            {
                Text = "Đang thống kê đối tượng...",
                Location = new Point(0, 7),
                Size = new Size(410, 20),
                Font = _fontBold,
                ForeColor = Color.FromArgb(24, 43, 73)
            };

            btnSelectAll = new WinFormsButton
            {
                Text = "✔ Chọn tất cả",
                Location = new Point(pnlToolbar.Width - 555, 2),
                Size = new Size(95, 27),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Font = _fontButton,
                Cursor = Cursors.Hand
            };
            btnSelectAll.Click += (s, e) => SetSelectionByFilter(_ => true);

            btnUnselectAll = new WinFormsButton
            {
                Text = "✖ Bỏ chọn hết",
                Location = new Point(pnlToolbar.Width - 455, 2),
                Size = new Size(95, 27),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Font = _fontButton,
                Cursor = Cursors.Hand
            };
            btnUnselectAll.Click += (s, e) => SetSelectionByFilter(_ => false);

            btnSelectOnlyBody = new WinFormsButton
            {
                Text = "🔶 Chỉ chọn BODY",
                Location = new Point(pnlToolbar.Width - 355, 2),
                Size = new Size(115, 27),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Font = _fontButton,
                Cursor = Cursors.Hand
            };
            _toolTip.SetToolTip(btnSelectOnlyBody, "Chỉ tích chọn các đối tượng BODY (thường là mặt hở sinh ra từ Link / Layer 0)");
            btnSelectOnlyBody.Click += (s, e) => SetSelectionByFilter(g => g.EntityType.Equals("BODY", StringComparison.OrdinalIgnoreCase));

            btnSelectOnlySolid = new WinFormsButton
            {
                Text = "🔷 Chỉ chọn 3DSOLID",
                Location = new Point(pnlToolbar.Width - 235, 2),
                Size = new Size(125, 27),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Font = _fontButton,
                Cursor = Cursors.Hand
            };
            _toolTip.SetToolTip(btnSelectOnlySolid, "Chỉ tích chọn các đối tượng 3DSOLID (khối đặc)");
            btnSelectOnlySolid.Click += (s, e) => SetSelectionByFilter(g => g.EntityType.Equals("3DSOLID", StringComparison.OrdinalIgnoreCase));

            btnZoomSelected = new WinFormsButton
            {
                Text = "🔍 Zoom xem",
                Location = new Point(pnlToolbar.Width - 105, 2),
                Size = new Size(105, 27),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Font = _fontButton,
                Cursor = Cursors.Hand
            };
            _toolTip.SetToolTip(btnZoomSelected, "Phóng tới vị trí của nhóm đang chọn trên màn hình AutoCAD");
            btnZoomSelected.Click += BtnZoomSelected_Click;

            pnlToolbar.Controls.AddRange(new Control[]
            {
                lblSummary, btnSelectAll, btnUnselectAll,
                btnSelectOnlyBody, btnSelectOnlySolid, btnZoomSelected
            });

            dgvGroups = new DataGridView
            {
                Location = new Point(12, 58),
                Size = new Size(grpGroups.Width - 24, grpGroups.Height - 70),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                Font = _fontRegular
            };

            dgvGroups.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name = "colSelect",
                HeaderText = "Chọn",
                FillWeight = 8,
                MinimumWidth = 50
            });
            dgvGroups.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colType",
                HeaderText = "Loại",
                ReadOnly = true,
                FillWeight = 12,
                MinimumWidth = 75
            });
            dgvGroups.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colCorridor",
                HeaderText = "Corridor",
                ReadOnly = true,
                FillWeight = 18,
                MinimumWidth = 110
            });
            dgvGroups.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colSubassembly",
                HeaderText = "Subassembly (Cấu kiện)",
                ReadOnly = true,
                FillWeight = 22,
                MinimumWidth = 140
            });
            dgvGroups.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colCode",
                HeaderText = "Mã Shape / Link Code",
                ReadOnly = true,
                FillWeight = 22,
                MinimumWidth = 140
            });
            dgvGroups.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colLayer",
                HeaderText = "Layer",
                ReadOnly = true,
                FillWeight = 24,
                MinimumWidth = 150
            });
            dgvGroups.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colCount",
                HeaderText = "Số lượng",
                ReadOnly = true,
                FillWeight = 10,
                MinimumWidth = 70,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
            });

            dgvGroups.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (dgvGroups.IsCurrentCellDirty)
                {
                    dgvGroups.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };
            dgvGroups.CellValueChanged += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex == 0 && e.RowIndex < SolidGroups.Count)
                {
                    bool val = Convert.ToBoolean(dgvGroups.Rows[e.RowIndex].Cells[0].Value);
                    SolidGroups[e.RowIndex].IsSelected = val;
                    UpdateSummaryLabel();
                }
            };

            grpGroups.Controls.AddRange(new Control[] { pnlToolbar, dgvGroups });

            // ─────────────────────────────────────────────────────────────
            // Group 3: Tùy chọn xử lý
            // ─────────────────────────────────────────────────────────────
            var grpOptions = new GroupBox
            {
                Text = " 3. Tùy chọn xử lý khi thực thi ",
                Location = new Point(12, this.ClientSize.Height - 173),
                Size = new Size(this.ClientSize.Width - 24, 112),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Font = _fontBold
            };

            chkBreakDynamicLink = new CheckBox
            {
                Text = "Ngắt liên kết động Corridor (Xóa AeccDbCorridorSolidMgr để Rebuild Corridor không tự sinh lại Solid/Body)",
                Location = new Point(15, 24),
                Size = new Size(760, 22),
                Checked = true,
                Font = _fontBold,
                ForeColor = Color.FromArgb(180, 40, 20)
            };

            chkDeleteSelectedEntities = new CheckBox
            {
                Text = "Xóa các đối tượng 3D Solid & Body đang được tích chọn trong bảng trên",
                Location = new Point(15, 50),
                Size = new Size(500, 22),
                Checked = true,
                Font = _fontRegular
            };
            chkDeleteSelectedEntities.CheckedChanged += (s, e) =>
            {
                chkCleanPropertySets.Enabled = !chkDeleteSelectedEntities.Checked;
            };

            chkCleanPropertySets = new CheckBox
            {
                Text = "Chỉ gỡ bỏ Property Sets của Corridor trên các khối chọn (giữ lại khối 3D sạch)",
                Location = new Point(530, 50),
                Size = new Size(460, 22),
                Checked = false,
                Enabled = false,
                Font = _fontRegular
            };

            chkPurgePropertySetDefs = new CheckBox
            {
                Text = "Xóa luôn các định nghĩa Property Set Corridor (Corridor Identity / Shape / Model Info) nếu không còn khối nào",
                Location = new Point(15, 76),
                Size = new Size(640, 22),
                Checked = false,
                Font = _fontRegular
            };

            chkAutoRebuild = new CheckBox
            {
                Text = "Tự động Rebuild Corridor sau khi xử lý xong",
                Location = new Point(670, 76),
                Size = new Size(310, 22),
                Checked = false,
                Font = _fontRegular
            };

            grpOptions.Controls.AddRange(new Control[]
            {
                chkBreakDynamicLink,
                chkDeleteSelectedEntities,
                chkCleanPropertySets,
                chkPurgePropertySetDefs,
                chkAutoRebuild
            });

            // ─────────────────────────────────────────────────────────────
            // Bottom Action Buttons
            // ─────────────────────────────────────────────────────────────
            btnExecute = new WinFormsButton
            {
                Text = "⚡ THỰC THI NGẮT LIÊN KẾT & DỌN DẸP",
                Location = new Point(this.ClientSize.Width - 405, this.ClientSize.Height - 50),
                Size = new Size(275, 38),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                BackColor = Color.FromArgb(192, 45, 28),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = _fontExecute,
                Cursor = Cursors.Hand
            };
            btnExecute.FlatAppearance.BorderSize = 0;
            btnExecute.Click += BtnExecute_Click;

            btnCancel = new WinFormsButton
            {
                Text = "Đóng",
                Location = new Point(this.ClientSize.Width - 118, this.ClientSize.Height - 50),
                Size = new Size(105, 38),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Font = _fontRegular,
                Cursor = Cursors.Hand,
                DialogResult = DialogResult.Cancel
            };
            btnCancel.Click += (s, e) => this.Close();

            this.AcceptButton = btnExecute;
            this.CancelButton = btnCancel;

            this.Controls.AddRange(new Control[]
            {
                pnlHeader, grpScope, grpGroups, grpOptions, btnExecute, btnCancel
            });

            this.FormClosing += (s, e) => SaveCurrentSettings();
        }

        private void UpdateScopeControlsState()
        {
            cmbCorridors.Enabled = radSelectedCorridor.Checked;
            btnPickCorridor.Enabled = radSelectedCorridor.Checked;
        }

        // ═══════════════════════════════════════════════════════════════
        //  PERSISTENT STATE METHODS (RULE 2)
        // ═══════════════════════════════════════════════════════════════
        private void RestoreLastSettings()
        {
            radAllCorridors.Checked = _lastAllCorridors;
            radSelectedCorridor.Checked = !_lastAllCorridors;
            chkBreakDynamicLink.Checked = _lastBreakDynamicLink;
            chkDeleteSelectedEntities.Checked = _lastDeleteSelectedEntities;
            chkCleanPropertySets.Checked = _lastCleanPropertySets;
            chkCleanPropertySets.Enabled = !chkDeleteSelectedEntities.Checked;
            chkPurgePropertySetDefs.Checked = _lastPurgePropertySetDefs;
            chkAutoRebuild.Checked = _lastAutoRebuild;

            if (!_lastCorridorId.IsNull && _lastCorridorId.IsValid && !_lastCorridorId.IsErased)
            {
                SelectedCorridorId = _lastCorridorId;
            }

            UpdateScopeControlsState();
        }

        private void SaveCurrentSettings()
        {
            _lastAllCorridors = radAllCorridors.Checked;
            _lastBreakDynamicLink = chkBreakDynamicLink.Checked;
            _lastDeleteSelectedEntities = chkDeleteSelectedEntities.Checked;
            _lastCleanPropertySets = chkCleanPropertySets.Checked;
            _lastPurgePropertySetDefs = chkPurgePropertySetDefs.Checked;
            _lastAutoRebuild = chkAutoRebuild.Checked;

            if (!SelectedCorridorId.IsNull && SelectedCorridorId.IsValid && !SelectedCorridorId.IsErased)
            {
                _lastCorridorId = SelectedCorridorId;
            }

            if (this.WindowState == FormWindowState.Normal)
            {
                _lastFormSize = this.Size;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  DATA LOADING & SCANNING
        // ═══════════════════════════════════════════════════════════════
        private void LoadCorridors()
        {
            cmbCorridors.Items.Clear();
            try
            {
                var civilDoc = CivilDocument.GetCivilDocument(_db);
                using (Transaction tr = _db.TransactionManager.StartTransaction())
                {
                    int selectIndex = -1;
                    int idx = 0;
                    foreach (ObjectId corrId in civilDoc.CorridorCollection)
                    {
                        if (!corrId.IsValid || corrId.IsErased) continue;
                        if (tr.GetObject(corrId, OpenMode.ForRead) is Corridor corr)
                        {
                            cmbCorridors.Items.Add(new CorridorComboItem(corrId, corr.Name));
                            if (corrId == SelectedCorridorId)
                            {
                                selectIndex = idx;
                            }
                            idx++;
                        }
                    }
                    tr.Commit();

                    if (selectIndex >= 0)
                    {
                        cmbCorridors.SelectedIndex = selectIndex;
                    }
                    else if (cmbCorridors.Items.Count > 0)
                    {
                        cmbCorridors.SelectedIndex = 0;
                        SelectedCorridorId = ((CorridorComboItem)cmbCorridors.Items[0]!).Id;
                    }
                }
            }
            catch { }
        }

        public void ScanDrawingData(HashSet<ObjectId>? manualFilterIds = null)
        {
            SolidGroups.Clear();
            HasDynamicLinkManager = false;
            DynamicLinkHandle = string.Empty;

            string targetCorridorName = string.Empty;
            string targetCorridorHandle = string.Empty;

            try
            {
                using (Transaction tr = _db.TransactionManager.StartTransaction())
                {
                    // 1. Kiểm tra AeccDbCorridorSolidMgr trong NamedObjectsDictionary
                    if (_db.NamedObjectsDictionaryId.IsValid && !_db.NamedObjectsDictionaryId.IsErased)
                    {
                        if (tr.GetObject(_db.NamedObjectsDictionaryId, OpenMode.ForRead) is DBDictionary nod)
                        {
                            foreach (DBDictionaryEntry entry in nod)
                            {
                                if (entry.Key.Equals("AeccDbCorridorSolidMgr", StringComparison.OrdinalIgnoreCase) ||
                                    entry.Key.IndexOf("CorridorSolidMgr", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    if (!entry.Value.IsNull && entry.Value.IsValid && !entry.Value.IsErased)
                                    {
                                        HasDynamicLinkManager = true;
                                        DynamicLinkHandle = entry.Value.Handle.ToString();
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    // 2. Lấy tên và Handle của Corridor cần lọc (nếu chọn lọc theo Corridor)
                    if (radSelectedCorridor.Checked && !SelectedCorridorId.IsNull && SelectedCorridorId.IsValid && !SelectedCorridorId.IsErased)
                    {
                        if (tr.GetObject(SelectedCorridorId, OpenMode.ForRead) is Corridor selCorr)
                        {
                            targetCorridorName = selCorr.Name ?? string.Empty;
                            targetCorridorHandle = SelectedCorridorId.Handle.ToString();
                        }
                    }

                    // 3. Quét ModelSpace tìm 3DSOLID và BODY
                    var groupDict = new Dictionary<string, CorridorSolidGroupItem>(StringComparer.OrdinalIgnoreCase);

                    if (tr.GetObject(_db.BlockTableId, OpenMode.ForRead) is BlockTable bt &&
                        tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) is BlockTableRecord ms)
                    {
                        foreach (ObjectId entId in ms)
                        {
                            if (!entId.IsValid || entId.IsErased) continue;
                            if (manualFilterIds != null && !manualFilterIds.Contains(entId)) continue;

                            var ent = tr.GetObject(entId, OpenMode.ForRead) as CadEntity;
                            if (ent == null || !(ent is Solid3d || ent is Body)) continue;

                            string entType = ent is Solid3d ? "3DSOLID" : "BODY";
                            string corrName = string.Empty;
                            string corrHandle = string.Empty;
                            string subName = string.Empty;
                            string codeName = string.Empty;
                            bool hasCorrPSet = false;

                            ReadCorridorInfoFromEntity(ent, tr, out corrName, out corrHandle, out subName, out codeName, out hasCorrPSet);

                            // Lọc theo Corridor nếu đang bật radSelectedCorridor và không phải chọn tay
                            if (manualFilterIds == null && radSelectedCorridor.Checked && !string.IsNullOrEmpty(targetCorridorName))
                            {
                                bool matchName = !string.IsNullOrEmpty(corrName) &&
                                                 corrName.Equals(targetCorridorName, StringComparison.OrdinalIgnoreCase);
                                bool matchHandle = !string.IsNullOrEmpty(corrHandle) &&
                                                   corrHandle.Equals(targetCorridorHandle, StringComparison.OrdinalIgnoreCase);
                                if (!matchName && !matchHandle)
                                {
                                    continue;
                                }
                            }

                            string dispCorr = string.IsNullOrEmpty(corrName)
                                ? (hasCorrPSet ? $"Handle:{corrHandle}" : "(Không gắn PSet)")
                                : corrName;
                            string dispSub = string.IsNullOrEmpty(subName) ? "-" : subName;
                            string dispCode = string.IsNullOrEmpty(codeName) ? "-" : codeName;
                            string dispLayer = ent.Layer ?? "0";

                            string key = $"{entType}|{dispCorr}|{dispSub}|{dispCode}|{dispLayer}";
                            if (!groupDict.TryGetValue(key, out var group))
                            {
                                group = new CorridorSolidGroupItem
                                {
                                    GroupKey = key,
                                    EntityType = entType,
                                    CorridorName = dispCorr,
                                    CorridorHandle = corrHandle,
                                    SubassemblyName = dispSub,
                                    CodeName = dispCode,
                                    LayerName = dispLayer,
                                    HasCorridorPropertySet = hasCorrPSet,
                                    IsSelected = true
                                };
                                groupDict[key] = group;
                            }

                            group.EntityIds.Add(entId);
                        }
                    }

                    SolidGroups = groupDict.Values
                        .OrderBy(g => g.EntityType)
                        .ThenBy(g => g.CorridorName)
                        .ThenBy(g => g.LayerName)
                        .ToList();

                    tr.Commit();
                }
            }
            catch (Exception ex)
            {
                _ed.WriteMessage($"\n⚠ Cảnh báo khi quét dữ liệu Solid/Body: {ex.Message}");
            }

            UpdateDynamicLinkStatusUI();
            PopulateGrid();
        }

        private static void ReadCorridorInfoFromEntity(
            CadEntity ent,
            Transaction tr,
            out string corridorName,
            out string corridorHandle,
            out string subassemblyName,
            out string codeName,
            out bool hasCorridorPSet)
        {
            corridorName = string.Empty;
            corridorHandle = string.Empty;
            subassemblyName = string.Empty;
            codeName = string.Empty;
            hasCorridorPSet = false;

            try
            {
                ObjectIdCollection psetIds = PropertyDataServices.GetPropertySets(ent);
                if (psetIds != null && psetIds.Count > 0)
                {
                    foreach (ObjectId psId in psetIds)
                    {
                        if (!psId.IsValid || psId.IsErased) continue;
                        if (tr.GetObject(psId, OpenMode.ForRead) is not PropertySet pset) continue;
                        if (tr.GetObject(pset.PropertySetDefinition, OpenMode.ForRead) is not PropertySetDefinition psDef) continue;

                        string defName = psDef.Name ?? string.Empty;
                        if (defName.IndexOf("Corridor", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            hasCorridorPSet = true;
                        }

                        for (int i = 0; i < psDef.Definitions.Count; i++)
                        {
                            try
                            {
                                var propDef = psDef.Definitions[i];
                                string propName = propDef.Name ?? string.Empty;
                                string propUpper = propName.ToUpperInvariant();

                                object? val = pset.GetAt(pset.PropertyNameToId(propName));
                                string valStr = val?.ToString()?.Trim() ?? string.Empty;
                                if (string.IsNullOrEmpty(valStr)) continue;

                                switch (propUpper)
                                {
                                    case "CORRIDORNAME":
                                        corridorName = valStr;
                                        hasCorridorPSet = true;
                                        break;
                                    case "CORRIDORHANDLE":
                                        corridorHandle = valStr;
                                        hasCorridorPSet = true;
                                        break;
                                    case "SUBASSEMBLYNAME":
                                    case "COMPONENTNAME":
                                        subassemblyName = valStr;
                                        break;
                                    case "CODENAME":
                                    case "SHAPECODE":
                                    case "LINKCODE":
                                        codeName = valStr;
                                        break;
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }
        }

        private void UpdateDynamicLinkStatusUI()
        {
            if (HasDynamicLinkManager)
            {
                lblDynamicLinkStatus.Text = $"⚠ PHÁT HIỆN LIÊN KẾT ĐỘNG ĐANG BẬT: AeccDbCorridorSolidMgr (Handle: {DynamicLinkHandle}) — Rebuild Corridor sẽ tự động tạo lại 3D Solid & Body!";
                lblDynamicLinkStatus.ForeColor = Color.FromArgb(192, 35, 20);
            }
            else
            {
                lblDynamicLinkStatus.Text = "✅ Trạng thái Liên kết động: ĐÃ NGẮT (Không có AeccDbCorridorSolidMgr trong bản vẽ — Rebuild Corridor sẽ không tự tạo 3D Solid/Body).";
                lblDynamicLinkStatus.ForeColor = Color.FromArgb(20, 125, 50);
            }
        }

        private void PopulateGrid()
        {
            dgvGroups.Rows.Clear();

            foreach (var grp in SolidGroups)
            {
                int rowIdx = dgvGroups.Rows.Add(
                    grp.IsSelected,
                    grp.EntityType,
                    grp.CorridorName,
                    grp.SubassemblyName,
                    grp.CodeName,
                    grp.LayerName,
                    grp.Count
                );

                if (grp.EntityType.Equals("BODY", StringComparison.OrdinalIgnoreCase))
                {
                    dgvGroups.Rows[rowIdx].DefaultCellStyle.BackColor = Color.FromArgb(255, 248, 238);
                }
            }

            UpdateSummaryLabel();
        }

        private void UpdateSummaryLabel()
        {
            int totalSolid = SolidGroups.Where(g => g.EntityType == "3DSOLID").Sum(g => g.Count);
            int totalBody = SolidGroups.Where(g => g.EntityType == "BODY").Sum(g => g.Count);
            int selSolid = SolidGroups.Where(g => g.IsSelected && g.EntityType == "3DSOLID").Sum(g => g.Count);
            int selBody = SolidGroups.Where(g => g.IsSelected && g.EntityType == "BODY").Sum(g => g.Count);

            lblSummary.Text = $"Phát hiện: {totalSolid} 3DSOLID + {totalBody} BODY  |  Đang chọn xử lý: {selSolid} 3DSOLID + {selBody} BODY (Tổng: {selSolid + selBody})";
        }

        private void SetSelectionByFilter(Func<CorridorSolidGroupItem, bool> predicate)
        {
            for (int i = 0; i < SolidGroups.Count; i++)
            {
                bool check = predicate(SolidGroups[i]);
                SolidGroups[i].IsSelected = check;
                if (i < dgvGroups.Rows.Count)
                {
                    dgvGroups.Rows[i].Cells[0].Value = check;
                }
            }
            UpdateSummaryLabel();
        }

        // ═══════════════════════════════════════════════════════════════
        //  CANVAS INTERACTION HANDLERS (RULE 1)
        // ═══════════════════════════════════════════════════════════════
        private void BtnPickCorridor_Click(object? sender, EventArgs e)
        {
            using (var interaction = _ed.StartUserInteraction(this))
            {
                var peo = new PromptEntityOptions("\nChọn Corridor trên bản vẽ: ");
                peo.SetRejectMessage("\nĐối tượng chọn phải là Corridor!");
                peo.AddAllowedClass(typeof(Corridor), true);

                PromptEntityResult per = _ed.GetEntity(peo);
                interaction.End();

                if (per.Status == PromptStatus.OK && !per.ObjectId.IsNull)
                {
                    SelectedCorridorId = per.ObjectId;
                    radSelectedCorridor.Checked = true;
                    for (int i = 0; i < cmbCorridors.Items.Count; i++)
                    {
                        if (cmbCorridors.Items[i] is CorridorComboItem item && item.Id == SelectedCorridorId)
                        {
                            cmbCorridors.SelectedIndex = i;
                            break;
                        }
                    }
                    ScanDrawingData();
                }
            }
        }

        private void BtnPickSolids_Click(object? sender, EventArgs e)
        {
            using (var interaction = _ed.StartUserInteraction(this))
            {
                var filter = new SelectionFilter(new[]
                {
                    new TypedValue((int)DxfCode.Start, "3DSOLID,BODY")
                });
                var pso = new PromptSelectionOptions
                {
                    MessageForAdding = "\nQuét chọn các đối tượng 3D Solid hoặc Body trên bản vẽ: "
                };

                PromptSelectionResult psr = _ed.GetSelection(pso, filter);
                interaction.End();

                if (psr.Status == PromptStatus.OK && psr.Value != null && psr.Value.Count > 0)
                {
                    var pickedSet = new HashSet<ObjectId>(psr.Value.GetObjectIds());
                    ScanDrawingData(pickedSet);
                }
            }
        }

        private void BtnZoomSelected_Click(object? sender, EventArgs e)
        {
            if (dgvGroups.SelectedRows.Count == 0) return;
            int rowIdx = dgvGroups.SelectedRows[0].Index;
            if (rowIdx < 0 || rowIdx >= SolidGroups.Count) return;

            var group = SolidGroups[rowIdx];
            if (group.EntityIds.Count == 0) return;

            try
            {
                using (Transaction tr = _db.TransactionManager.StartTransaction())
                {
                    Extents3d combinedExt = default;
                    bool hasExt = false;

                    foreach (ObjectId id in group.EntityIds)
                    {
                        if (!id.IsValid || id.IsErased) continue;
                        if (tr.GetObject(id, OpenMode.ForRead) is CadEntity ent)
                        {
                            try
                            {
                                Extents3d ext = ent.GeometricExtents;
                                if (!hasExt)
                                {
                                    combinedExt = ext;
                                    hasExt = true;
                                }
                                else
                                {
                                    combinedExt.AddExtents(ext);
                                }
                            }
                            catch { }
                        }
                    }

                    tr.Commit();

                    if (hasExt)
                    {
                        double padX = Math.Max(5.0, (combinedExt.MaxPoint.X - combinedExt.MinPoint.X) * 0.2);
                        double padY = Math.Max(5.0, (combinedExt.MaxPoint.Y - combinedExt.MinPoint.Y) * 0.2);

                        using (ViewTableRecord view = _ed.GetCurrentView())
                        {
                            view.CenterPoint = new Point2d(
                                (combinedExt.MinPoint.X + combinedExt.MaxPoint.X) * 0.5,
                                (combinedExt.MinPoint.Y + combinedExt.MaxPoint.Y) * 0.5);
                            view.Width = (combinedExt.MaxPoint.X - combinedExt.MinPoint.X) + padX * 2;
                            view.Height = (combinedExt.MaxPoint.Y - combinedExt.MinPoint.Y) + padY * 2;
                            _ed.SetCurrentView(view);
                        }
                    }
                }
            }
            catch { }
        }

        private void BtnExecute_Click(object? sender, EventArgs e)
        {
            SelectedEntityIdsToProcess = SolidGroups
                .Where(g => g.IsSelected)
                .SelectMany(g => g.EntityIds)
                .Distinct()
                .ToList();

            if (!BreakDynamicLink &&
                !PurgePropertySetDefs &&
                !AutoRebuild &&
                SelectedEntityIdsToProcess.Count == 0)
            {
                MessageBox.Show(
                    "Vui lòng chọn ít nhất một tác vụ (Ngắt liên kết động hoặc chọn các nhóm 3D Solid / Body cần xử lý).",
                    "Thông báo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            SaveCurrentSettings();
            FormAccepted = true;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _toolTip?.Dispose();
                _fontRegular?.Dispose();
                _fontBold?.Dispose();
                _fontTitle?.Dispose();
                _fontSubtitle?.Dispose();
                _fontButton?.Dispose();
                _fontExecute?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

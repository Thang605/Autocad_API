// (C) Copyright 2026 by T27
// Form giao diện Tắt/Bật VP Freeze Layer của đối tượng trong Viewport hiện hành
//
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

using Civil3DCsharp;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsButton = System.Windows.Forms.Button;
using WinFormsCheckBox = System.Windows.Forms.CheckBox;
using WinFormsRadioButton = System.Windows.Forms.RadioButton;
using DrawingColor = System.Drawing.Color;
using DrawingFont = System.Drawing.Font;

namespace MyFirstProject.Acad_Tool
{
    /// <summary>
    /// Form quản lý và tắt/bật VP Freeze layer theo đối tượng chọn trong Viewport
    /// </summary>
    public class TatVPFreezeLayerForm : Form
    {
        #region Persistent State (RULE 2)

        private static bool _lastUseNestedLayer = true;
        private static int _lastFilterIndex = 0;
        private static string _lastSearchText = "";
        private static Size _lastFormSize = new Size(820, 680);
        private static Point _lastFormLocation = Point.Empty;
        private static ObjectId _lastViewportId = ObjectId.Null;

        #endregion

        #region Fields

        private ObjectId _currentViewportId = ObjectId.Null;
        private int _currentVpNumber = -1;
        private List<VpLayerItem> _allLayers = new List<VpLayerItem>();
        private bool _isUpdatingUi = false;

        // Controls
        private ComboBox cboLayouts = null!;
        private WinFormsButton btnSwitchLayout = null!;
        private WinFormsLabel lblVpStatus = null!;
        private WinFormsButton btnPickViewport = null!;
        private WinFormsButton btnRefresh = null!;

        private WinFormsRadioButton rbNested = null!;
        private WinFormsRadioButton rbBlock = null!;
        private WinFormsButton btnPickAndFreeze = null!;
        private WinFormsButton btnWindowFreeze = null!;
        private WinFormsButton btnPickAndThaw = null!;

        private TextBox txtSearch = null!;
        private ComboBox cboFilter = null!;
        private WinFormsCheckBox chkSelectAll = null!;
        private DataGridView dgvLayers = null!;

        private WinFormsButton btnFreezeChecked = null!;
        private WinFormsButton btnThawChecked = null!;
        private WinFormsButton btnToggleChecked = null!;
        private WinFormsButton btnThawAll = null!;

        private WinFormsLabel lblStatusSummary = null!;
        private WinFormsButton btnClose = null!;

        #endregion

        public TatVPFreezeLayerForm()
        {
            InitializeComponent();
            RestoreLastSettings();
            LoadLayoutsAndDetectViewport();
        }

        #region Initialize UI

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.Text = "Tắt / Bật VP Freeze Layer Theo Đối Tượng (Viewport Hiện Hành)";
            this.Font = new DrawingFont("Segoe UI", 9F, FontStyle.Regular);
            this.ClientSize = new Size(820, 680);
            this.MinimumSize = new Size(740, 560);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = DrawingColor.FromArgb(248, 249, 250);

            // ================= 1. HEADER PANEL =================
            var pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 62,
                BackColor = DrawingColor.FromArgb(22, 53, 90),
                Padding = new Padding(16, 8, 16, 8)
            };

            var lblTitle = new WinFormsLabel
            {
                Text = "❄️ TẮT / BẬT VP FREEZE LAYER TRONG VIEWPORT HIỆN HÀNH",
                Font = new DrawingFont("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = DrawingColor.White,
                AutoSize = true,
                Location = new Point(14, 10)
            };

            var lblSubtitle = new WinFormsLabel
            {
                Text = "Đóng băng (VP Freeze) hoặc rã đông (VP Thaw) layer theo đối tượng chọn trong Viewport Layout mà không ảnh hưởng Model",
                Font = new DrawingFont("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = DrawingColor.FromArgb(215, 230, 248),
                AutoSize = true,
                Location = new Point(15, 34)
            };

            pnlHeader.Controls.Add(lblTitle);
            pnlHeader.Controls.Add(lblSubtitle);

            // ================= 2. GROUP VIEWPORT STATUS =================
            var grpViewport = new GroupBox
            {
                Text = "Không gian & Viewport thao tác",
                Font = new DrawingFont("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(12, 70),
                Size = new Size(796, 75),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var lblLayout = new WinFormsLabel
            {
                Text = "Layout:",
                Font = new DrawingFont("Segoe UI", 9F, FontStyle.Regular),
                Location = new Point(12, 23),
                AutoSize = true
            };

            cboLayouts = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new DrawingFont("Segoe UI", 9F, FontStyle.Regular),
                Location = new Point(62, 20),
                Width = 140
            };

            btnSwitchLayout = new WinFormsButton
            {
                Text = "Chuyển",
                Font = new DrawingFont("Segoe UI", 8.5F, FontStyle.Regular),
                Location = new Point(206, 19),
                Size = new Size(62, 25),
                BackColor = DrawingColor.White
            };
            btnSwitchLayout.Click += BtnSwitchLayout_Click;

            lblVpStatus = new WinFormsLabel
            {
                Text = "Đang kiểm tra viewport...",
                Font = new DrawingFont("Segoe UI", 9F, FontStyle.Regular),
                Location = new Point(278, 23),
                Size = new Size(350, 42),
                ForeColor = DrawingColor.DarkBlue
            };

            btnPickViewport = new WinFormsButton
            {
                Text = "🎯 Chọn Viewport",
                Font = new DrawingFont("Segoe UI", 8.5F, FontStyle.Bold),
                Location = new Point(636, 18),
                Size = new Size(115, 27),
                BackColor = DrawingColor.FromArgb(235, 243, 255),
                ForeColor = DrawingColor.FromArgb(16, 72, 140),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnPickViewport.Click += BtnPickViewport_Click;

            btnRefresh = new WinFormsButton
            {
                Text = "🔄",
                Font = new DrawingFont("Segoe UI", 9F, FontStyle.Regular),
                Location = new Point(755, 18),
                Size = new Size(32, 27),
                BackColor = DrawingColor.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnRefresh.Click += (s, e) => DetectViewportAndLoadLayers();

            grpViewport.Controls.Add(lblLayout);
            grpViewport.Controls.Add(cboLayouts);
            grpViewport.Controls.Add(btnSwitchLayout);
            grpViewport.Controls.Add(lblVpStatus);
            grpViewport.Controls.Add(btnPickViewport);
            grpViewport.Controls.Add(btnRefresh);

            // ================= 3. GROUP CANVAS ACTIONS =================
            var grpCanvas = new GroupBox
            {
                Text = "Thao tác trực tiếp trên bản vẽ (Canvas Quick Actions)",
                Font = new DrawingFont("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(12, 150),
                Size = new Size(796, 92),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            rbNested = new WinFormsRadioButton
            {
                Text = "Lấy Layer đối tượng con trong Block / Xref (Nested Entity - Khuyên dùng)",
                Font = new DrawingFont("Segoe UI", 8.5F, FontStyle.Regular),
                Location = new Point(15, 20),
                AutoSize = true,
                Checked = true
            };

            rbBlock = new WinFormsRadioButton
            {
                Text = "Lấy Layer của Block cha (Block Reference)",
                Font = new DrawingFont("Segoe UI", 8.5F, FontStyle.Regular),
                Location = new Point(480, 20),
                AutoSize = true
            };

            btnPickAndFreeze = new WinFormsButton
            {
                Text = "🎯 Click chọn tắt ngay (Pick & Freeze)",
                Font = new DrawingFont("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(15, 48),
                Size = new Size(260, 34),
                BackColor = DrawingColor.FromArgb(196, 43, 28),
                ForeColor = DrawingColor.White,
                Cursor = Cursors.Hand
            };
            btnPickAndFreeze.Click += BtnPickAndFreeze_Click;

            btnWindowFreeze = new WinFormsButton
            {
                Text = "📦 Quét chọn nhiều đối tượng (Window)",
                Font = new DrawingFont("Segoe UI", 8.5F, FontStyle.Bold),
                Location = new Point(285, 48),
                Size = new Size(260, 34),
                BackColor = DrawingColor.FromArgb(240, 240, 240),
                ForeColor = DrawingColor.FromArgb(30, 30, 30),
                Cursor = Cursors.Hand
            };
            btnWindowFreeze.Click += BtnWindowFreeze_Click;

            btnPickAndThaw = new WinFormsButton
            {
                Text = "☀️ Click chọn để Rã đông (Pick & Thaw)",
                Font = new DrawingFont("Segoe UI", 8.5F, FontStyle.Bold),
                Location = new Point(555, 48),
                Size = new Size(226, 34),
                BackColor = DrawingColor.FromArgb(235, 247, 238),
                ForeColor = DrawingColor.FromArgb(18, 118, 34),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnPickAndThaw.Click += BtnPickAndThaw_Click;

            grpCanvas.Controls.Add(rbNested);
            grpCanvas.Controls.Add(rbBlock);
            grpCanvas.Controls.Add(btnPickAndFreeze);
            grpCanvas.Controls.Add(btnWindowFreeze);
            grpCanvas.Controls.Add(btnPickAndThaw);

            // ================= 4. GROUP LAYER MANAGEMENT =================
            var grpLayers = new GroupBox
            {
                Text = "Danh sách Layer trong Viewport (Layer Management)",
                Font = new DrawingFont("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(12, 247),
                Size = new Size(796, 375),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            // Search & Filter Toolbar
            var lblSearch = new WinFormsLabel
            {
                Text = "Tìm kiếm:",
                Font = new DrawingFont("Segoe UI", 9F, FontStyle.Regular),
                Location = new Point(12, 24),
                AutoSize = true
            };

            txtSearch = new TextBox
            {
                Font = new DrawingFont("Segoe UI", 9F, FontStyle.Regular),
                Location = new Point(75, 21),
                Width = 190
            };
            txtSearch.TextChanged += (s, e) => ApplyFilter();

            var lblFilter = new WinFormsLabel
            {
                Text = "Lọc:",
                Font = new DrawingFont("Segoe UI", 9F, FontStyle.Regular),
                Location = new Point(275, 24),
                AutoSize = true
            };

            cboFilter = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new DrawingFont("Segoe UI", 9F, FontStyle.Regular),
                Location = new Point(306, 21),
                Width = 160
            };
            cboFilter.Items.AddRange(new object[]
            {
                "Tất cả Layer",
                "❄️ Đang bị VP Freeze",
                "☀️ Đang hiển thị (Thawed)"
            });
            cboFilter.SelectedIndex = 0;
            cboFilter.SelectedIndexChanged += (s, e) => ApplyFilter();

            chkSelectAll = new WinFormsCheckBox
            {
                Text = "Chọn tất cả hiển thị",
                Font = new DrawingFont("Segoe UI", 8.5F, FontStyle.Regular),
                Location = new Point(480, 23),
                AutoSize = true
            };
            chkSelectAll.CheckedChanged += ChkSelectAll_CheckedChanged;

            // Batch action buttons
            btnFreezeChecked = new WinFormsButton
            {
                Text = "❄️ VP Freeze đã chọn",
                Font = new DrawingFont("Segoe UI", 8F, FontStyle.Bold),
                Location = new Point(620, 19),
                Size = new Size(165, 26),
                BackColor = DrawingColor.FromArgb(253, 237, 237),
                ForeColor = DrawingColor.FromArgb(196, 43, 28),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnFreezeChecked.Click += BtnFreezeChecked_Click;

            // DataGridView setup
            dgvLayers = new DataGridView
            {
                Location = new Point(12, 53),
                Size = new Size(772, 275),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                MultiSelect = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                BackgroundColor = DrawingColor.White,
                BorderStyle = BorderStyle.Fixed3D,
                Font = new DrawingFont("Segoe UI", 8.5F, FontStyle.Regular),
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = DrawingColor.FromArgb(240, 242, 245),
                    ForeColor = DrawingColor.FromArgb(30, 30, 30),
                    Font = new DrawingFont("Segoe UI", 8.5F, FontStyle.Bold),
                    Alignment = DataGridViewContentAlignment.MiddleLeft
                },
                EnableHeadersVisualStyles = false
            };

            SetupDataGridViewColumns();
            dgvLayers.CellPainting += DgvLayers_CellPainting;

            // Bottom toolbar in Group
            btnThawChecked = new WinFormsButton
            {
                Text = "☀️ VP Thaw đã chọn",
                Font = new DrawingFont("Segoe UI", 8.5F, FontStyle.Bold),
                Location = new Point(12, 336),
                Size = new Size(150, 28),
                BackColor = DrawingColor.FromArgb(235, 247, 238),
                ForeColor = DrawingColor.FromArgb(18, 118, 34),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            btnThawChecked.Click += BtnThawChecked_Click;

            btnToggleChecked = new WinFormsButton
            {
                Text = "🔄 Đảo trạng thái",
                Font = new DrawingFont("Segoe UI", 8.5F, FontStyle.Regular),
                Location = new Point(170, 336),
                Size = new Size(125, 28),
                BackColor = DrawingColor.White,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            btnToggleChecked.Click += BtnToggleChecked_Click;

            btnThawAll = new WinFormsButton
            {
                Text = "☀️ Rã đông TẤT CẢ layer trong VP",
                Font = new DrawingFont("Segoe UI", 8.5F, FontStyle.Bold),
                Location = new Point(303, 336),
                Size = new Size(220, 28),
                BackColor = DrawingColor.White,
                ForeColor = DrawingColor.DarkGreen,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            btnThawAll.Click += BtnThawAll_Click;

            grpLayers.Controls.Add(lblSearch);
            grpLayers.Controls.Add(txtSearch);
            grpLayers.Controls.Add(lblFilter);
            grpLayers.Controls.Add(cboFilter);
            grpLayers.Controls.Add(chkSelectAll);
            grpLayers.Controls.Add(btnFreezeChecked);
            grpLayers.Controls.Add(dgvLayers);
            grpLayers.Controls.Add(btnThawChecked);
            grpLayers.Controls.Add(btnToggleChecked);
            grpLayers.Controls.Add(btnThawAll);

            // ================= 5. FOOTER STATUS =================
            lblStatusSummary = new WinFormsLabel
            {
                Text = "Tổng: 0 layer | Đang VP Freeze: 0 layer",
                Font = new DrawingFont("Segoe UI", 8.5F, FontStyle.Italic),
                Location = new Point(15, 642),
                Size = new Size(600, 22),
                ForeColor = DrawingColor.FromArgb(70, 70, 70),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };

            btnClose = new WinFormsButton
            {
                Text = "Đóng",
                Font = new DrawingFont("Segoe UI", 9F, FontStyle.Regular),
                Location = new Point(720, 638),
                Size = new Size(88, 30),
                BackColor = DrawingColor.White,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            btnClose.Click += (s, e) => this.Close();

            // Add all to Form
            this.Controls.Add(pnlHeader);
            this.Controls.Add(grpViewport);
            this.Controls.Add(grpCanvas);
            this.Controls.Add(grpLayers);
            this.Controls.Add(lblStatusSummary);
            this.Controls.Add(btnClose);

            this.FormClosing += TatVPFreezeLayerForm_FormClosing;
            this.ResumeLayout(false);
        }

        private void SetupDataGridViewColumns()
        {
            dgvLayers.Columns.Clear();

            // 0. Checkbox
            var colCheck = new DataGridViewCheckBoxColumn
            {
                Name = "colCheck",
                HeaderText = "",
                Width = 32,
                Resizable = DataGridViewTriState.False
            };
            dgvLayers.Columns.Add(colCheck);

            // 1. Color badge
            var colColor = new DataGridViewTextBoxColumn
            {
                Name = "colColor",
                HeaderText = "Màu",
                Width = 52,
                ReadOnly = true,
                Resizable = DataGridViewTriState.False
            };
            dgvLayers.Columns.Add(colColor);

            // 2. Layer Name
            var colName = new DataGridViewTextBoxColumn
            {
                Name = "colName",
                HeaderText = "Tên Layer",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                ReadOnly = true
            };
            dgvLayers.Columns.Add(colName);

            // 3. VP Status (Frozen vs Thawed)
            var colStatus = new DataGridViewTextBoxColumn
            {
                Name = "colStatus",
                HeaderText = "Trạng thái trong VP",
                Width = 145,
                ReadOnly = true
            };
            dgvLayers.Columns.Add(colStatus);

            // 4. Current Layer
            var colCurrent = new DataGridViewTextBoxColumn
            {
                Name = "colCurrent",
                HeaderText = "Hiện hành",
                Width = 85,
                ReadOnly = true
            };
            dgvLayers.Columns.Add(colCurrent);

            // 5. Lock status
            var colLock = new DataGridViewTextBoxColumn
            {
                Name = "colLock",
                HeaderText = "Khóa",
                Width = 65,
                ReadOnly = true
            };
            dgvLayers.Columns.Add(colLock);
        }

        #endregion

        #region Persistent Settings (RULE 2)

        private void RestoreLastSettings()
        {
            try
            {
                if (_lastFormSize.Width > 200 && _lastFormSize.Height > 200)
                {
                    this.Size = _lastFormSize;
                }
                if (!_lastFormLocation.IsEmpty && IsOnScreen(_lastFormLocation))
                {
                    this.Location = _lastFormLocation;
                }

                if (_lastUseNestedLayer)
                    rbNested.Checked = true;
                else
                    rbBlock.Checked = true;

                if (_lastFilterIndex >= 0 && _lastFilterIndex < cboFilter.Items.Count)
                    cboFilter.SelectedIndex = _lastFilterIndex;

                if (!string.IsNullOrEmpty(_lastSearchText))
                    txtSearch.Text = _lastSearchText;
            }
            catch { }
        }

        private void SaveCurrentSettings()
        {
            try
            {
                _lastFormSize = this.Size;
                _lastFormLocation = this.Location;
                _lastUseNestedLayer = rbNested.Checked;
                _lastFilterIndex = cboFilter.SelectedIndex;
                _lastSearchText = txtSearch.Text.Trim();
                _lastViewportId = _currentViewportId;
            }
            catch { }
        }

        private void TatVPFreezeLayerForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            SaveCurrentSettings();
        }

        private static bool IsOnScreen(Point pt)
        {
            foreach (Screen screen in Screen.AllScreens)
            {
                if (screen.WorkingArea.Contains(pt)) return true;
            }
            return false;
        }

        #endregion

        #region Data Loading & Viewport Detection

        private void LoadLayoutsAndDetectViewport()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            _isUpdatingUi = true;
            cboLayouts.Items.Clear();

            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var layoutMgr = LayoutManager.Current;
                var dbDict = (DBDictionary)tr.GetObject(doc.Database.LayoutDictionaryId, OpenMode.ForRead);

                foreach (DBDictionaryEntry entry in dbDict)
                {
                    if (!entry.Key.Equals("Model", StringComparison.OrdinalIgnoreCase))
                    {
                        cboLayouts.Items.Add(entry.Key);
                    }
                }
                tr.Commit();
            }

            string currentLayout = LayoutManager.Current.CurrentLayout;
            if (!currentLayout.Equals("Model", StringComparison.OrdinalIgnoreCase) && cboLayouts.Items.Contains(currentLayout))
            {
                cboLayouts.SelectedItem = currentLayout;
            }
            else if (cboLayouts.Items.Count > 0)
            {
                cboLayouts.SelectedIndex = 0;
            }

            _isUpdatingUi = false;
            DetectViewportAndLoadLayers();
        }

        private void DetectViewportAndLoadLayers()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var db = doc.Database;

            _currentViewportId = AT_TatVPFreezeLayerCmd.GetActiveOrLayoutViewportId(db, out string msg, out int vpNum);
            _currentVpNumber = vpNum;

            if (_currentViewportId == ObjectId.Null && !_lastViewportId.IsNull && _lastViewportId.IsValid && !_lastViewportId.IsErased)
            {
                // Thử khôi phục viewport lần trước nếu cùng layout
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    try
                    {
                        var vp = tr.GetObject(_lastViewportId, OpenMode.ForRead) as Viewport;
                        if (vp != null && vp.Number > 1)
                        {
                            _currentViewportId = _lastViewportId;
                            _currentVpNumber = vp.Number;
                            msg = $"✓ Khôi phục Viewport #{vp.Number} từ phiên trước.";
                        }
                    }
                    catch { }
                    tr.Commit();
                }
            }

            UpdateViewportStatusLabel(msg);
            LoadLayers();
        }

        private void UpdateViewportStatusLabel(string msg)
        {
            lblVpStatus.Text = msg;
            bool hasValidVp = (!_currentViewportId.IsNull && _currentViewportId.IsValid);

            if (hasValidVp)
            {
                lblVpStatus.ForeColor = DrawingColor.FromArgb(16, 124, 65); // Green
                btnPickAndFreeze.Enabled = true;
                btnWindowFreeze.Enabled = true;
                btnPickAndThaw.Enabled = true;
                btnFreezeChecked.Enabled = true;
                btnThawChecked.Enabled = true;
                btnToggleChecked.Enabled = true;
                btnThawAll.Enabled = true;
            }
            else
            {
                lblVpStatus.ForeColor = DrawingColor.FromArgb(196, 43, 28); // Red
                btnPickAndFreeze.Enabled = false;
                btnWindowFreeze.Enabled = false;
                btnPickAndThaw.Enabled = false;
                btnFreezeChecked.Enabled = false;
                btnThawChecked.Enabled = false;
                btnToggleChecked.Enabled = false;
                btnThawAll.Enabled = false;
            }
        }

        private void LoadLayers()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            _allLayers = AT_TatVPFreezeLayerCmd.GetAllLayersWithVpStatus(doc.Database, _currentViewportId);
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (_isUpdatingUi) return;

            string keyword = txtSearch.Text.Trim();
            int filterMode = cboFilter.SelectedIndex; // 0=All, 1=Frozen, 2=Thawed

            var filtered = _allLayers.Where(l =>
            {
                if (!string.IsNullOrEmpty(keyword) && l.Name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0)
                    return false;

                if (filterMode == 1 && !l.IsFrozenInVp)
                    return false;

                if (filterMode == 2 && l.IsFrozenInVp)
                    return false;

                return true;
            }).ToList();

            dgvLayers.Rows.Clear();
            foreach (var item in filtered)
            {
                int rowIndex = dgvLayers.Rows.Add();
                var row = dgvLayers.Rows[rowIndex];
                row.Tag = item;

                row.Cells["colCheck"].Value = false;
                row.Cells["colColor"].Value = item.CadColor.ColorIndex;
                row.Cells["colName"].Value = item.Name;

                if (item.IsFrozenInVp)
                {
                    row.Cells["colStatus"].Value = "❄️ Đang Tắt (Frozen)";
                    row.Cells["colStatus"].Style.ForeColor = DrawingColor.FromArgb(196, 43, 28);
                    row.Cells["colStatus"].Style.Font = new DrawingFont("Segoe UI", 8.5F, FontStyle.Bold);
                }
                else
                {
                    row.Cells["colStatus"].Value = "☀️ Đang Hiện (Thawed)";
                    row.Cells["colStatus"].Style.ForeColor = DrawingColor.FromArgb(16, 124, 65);
                }

                row.Cells["colCurrent"].Value = item.IsCurrent ? "★ Hiện hành" : "";
                if (item.IsCurrent)
                {
                    row.Cells["colCurrent"].Style.ForeColor = DrawingColor.DarkBlue;
                    row.Cells["colCurrent"].Style.Font = new DrawingFont("Segoe UI", 8.5F, FontStyle.Bold);
                }

                row.Cells["colLock"].Value = item.IsLocked ? "🔒 Khóa" : "Mở";
            }

            int frozenCount = _allLayers.Count(x => x.IsFrozenInVp);
            lblStatusSummary.Text = $"Tổng số: {_allLayers.Count} layer | Đang VP Freeze: {frozenCount} layer | Hiển thị: {filtered.Count} layer";
        }

        #endregion

        #region Canvas Interactions (Click & Freeze, Window, Click & Thaw)

        /// <summary>
        /// Click chọn từng đối tượng liên tục trên Canvas để tắt (VP Freeze) layer ngay lập tức
        /// </summary>
        private void BtnPickAndFreeze_Click(object? sender, EventArgs e)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null || _currentViewportId.IsNull) return;
            var ed = doc.Editor;

            bool useNested = rbNested.Checked;
            int countFrozen = 0;

            using (var interaction = ed.StartUserInteraction(this))
            {
                ed.WriteMessage("\n=======================================================");
                ed.WriteMessage($"\n❄️ BẮT ĐẦU CHỌN ĐỐI TƯỢNG ĐỂ TẮT (VP FREEZE) TRONG VIEWPORT #{_currentVpNumber}");
                ed.WriteMessage($"\n   Chế độ: {(useNested ? "Lấy layer đối tượng con (Nested)" : "Lấy layer Block cha")}");
                ed.WriteMessage("\n   Nhấn [Enter] hoặc [ESC] để kết thúc và quay lại Form.");
                ed.WriteMessage("\n=======================================================");

                while (true)
                {
                    var pneo = new PromptNestedEntityOptions(
                        $"\n[VP Freeze - Đã tắt: {countFrozen}] Chọn đối tượng để tắt layer [ESC/Enter để dừng]: ");
                    pneo.AllowNone = true;

                    var pner = ed.GetNestedEntity(pneo);
                    if (pner.Status != PromptStatus.OK)
                        break;

                    ObjectId targetEntId = ObjectId.Null;
                    if (useNested)
                    {
                        targetEntId = pner.ObjectId;
                    }
                    else
                    {
                        ObjectId[] containers = pner.GetContainers();
                        if (containers != null && containers.Length > 0)
                        {
                            targetEntId = containers[containers.Length - 1]; // Outermost block
                        }
                        else
                        {
                            targetEntId = pner.ObjectId;
                        }
                    }

                    if (targetEntId.IsNull || !targetEntId.IsValid) continue;

                    string layerName = "";
                    ObjectId layerId = ObjectId.Null;

                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        var ent = tr.GetObject(targetEntId, OpenMode.ForRead) as Entity;
                        if (ent != null)
                        {
                            layerName = ent.Layer;
                            layerId = ent.LayerId;
                        }
                        tr.Commit();
                    }

                    if (layerId != ObjectId.Null)
                    {
                        AT_TatVPFreezeLayerCmd.FreezeLayersInVp(_currentViewportId, new[] { layerId });
                        countFrozen++;
                        ed.WriteMessage($"\n❄️ [VP Freeze #{countFrozen}] Đã tắt layer: '{layerName}' trong Viewport.");
                    }
                }
            }

            // Phục hồi lại dữ liệu sau khi tương tác
            LoadLayers();
        }

        /// <summary>
        /// Quét chọn nhiều đối tượng (Window/Crossing) để tắt (VP Freeze) tất cả layer của chúng
        /// </summary>
        private void BtnWindowFreeze_Click(object? sender, EventArgs e)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null || _currentViewportId.IsNull) return;
            var ed = doc.Editor;

            using (var interaction = ed.StartUserInteraction(this))
            {
                var pso = new PromptSelectionOptions();
                pso.MessageForAdding = "\n[VP Freeze] Quét chọn các đối tượng cần tắt layer trong Viewport: ";

                var psr = ed.GetSelection(pso);
                if (psr.Status == PromptStatus.OK && psr.Value.Count > 0)
                {
                    var layerIds = new HashSet<ObjectId>();
                    var layerNames = new List<string>();

                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        foreach (ObjectId entId in psr.Value.GetObjectIds())
                        {
                            var ent = tr.GetObject(entId, OpenMode.ForRead) as Entity;
                            if (ent != null && layerIds.Add(ent.LayerId))
                            {
                                layerNames.Add(ent.Layer);
                            }
                        }
                        tr.Commit();
                    }

                    if (layerIds.Count > 0)
                    {
                        int count = AT_TatVPFreezeLayerCmd.FreezeLayersInVp(_currentViewportId, layerIds);
                        ed.WriteMessage($"\n❄️ [VP Freeze] Đã tắt {count} layer: {string.Join(", ", layerNames)}");
                    }
                }
            }

            LoadLayers();
        }

        /// <summary>
        /// Click chọn từng đối tượng liên tục trên Canvas để rã đông (VP Thaw) layer
        /// </summary>
        private void BtnPickAndThaw_Click(object? sender, EventArgs e)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null || _currentViewportId.IsNull) return;
            var ed = doc.Editor;

            bool useNested = rbNested.Checked;
            int countThawed = 0;

            using (var interaction = ed.StartUserInteraction(this))
            {
                ed.WriteMessage("\n=======================================================");
                ed.WriteMessage($"\n☀️ BẮT ĐẦU CHỌN ĐỐI TƯỢNG ĐỂ RÃ ĐÔNG (VP THAW) TRONG VIEWPORT #{_currentVpNumber}");
                ed.WriteMessage("\n   Nhấn [Enter] hoặc [ESC] để kết thúc và quay lại Form.");
                ed.WriteMessage("\n=======================================================");

                while (true)
                {
                    var pneo = new PromptNestedEntityOptions(
                        $"\n[VP Thaw - Đã rã đông: {countThawed}] Chọn đối tượng để bật lại layer [ESC/Enter để dừng]: ");
                    pneo.AllowNone = true;

                    var pner = ed.GetNestedEntity(pneo);
                    if (pner.Status != PromptStatus.OK)
                        break;

                    ObjectId targetEntId = pner.ObjectId;
                    if (!useNested)
                    {
                        ObjectId[] containers = pner.GetContainers();
                        if (containers != null && containers.Length > 0)
                            targetEntId = containers[containers.Length - 1];
                    }

                    if (targetEntId.IsNull || !targetEntId.IsValid) continue;

                    string layerName = "";
                    ObjectId layerId = ObjectId.Null;

                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        var ent = tr.GetObject(targetEntId, OpenMode.ForRead) as Entity;
                        if (ent != null)
                        {
                            layerName = ent.Layer;
                            layerId = ent.LayerId;
                        }
                        tr.Commit();
                    }

                    if (layerId != ObjectId.Null)
                    {
                        AT_TatVPFreezeLayerCmd.ThawLayersInVp(_currentViewportId, new[] { layerId });
                        countThawed++;
                        ed.WriteMessage($"\n☀️ [VP Thaw #{countThawed}] Đã bật lại layer: '{layerName}' trong Viewport.");
                    }
                }
            }

            LoadLayers();
        }

        #endregion

        #region UI Button Handlers & Layout Operations

        private void BtnSwitchLayout_Click(object? sender, EventArgs e)
        {
            if (cboLayouts.SelectedItem == null) return;
            string selectedLayout = cboLayouts.SelectedItem.ToString() ?? "";
            if (string.IsNullOrEmpty(selectedLayout)) return;

            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            try
            {
                LayoutManager.Current.CurrentLayout = selectedLayout;
                DetectViewportAndLoadLayers();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể chuyển sang layout '{selectedLayout}': {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnPickViewport_Click(object? sender, EventArgs e)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            ObjectId selectedVp = ObjectId.Null;
            using (var interaction = doc.Editor.StartUserInteraction(this))
            {
                selectedVp = AT_TatVPFreezeLayerCmd.SelectViewportOnCanvas(doc.Editor, doc.Database);
            }

            if (!selectedVp.IsNull && selectedVp.IsValid)
            {
                _currentViewportId = selectedVp;
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    var vp = tr.GetObject(selectedVp, OpenMode.ForRead) as Viewport;
                    if (vp != null)
                    {
                        _currentVpNumber = vp.Number;
                        UpdateViewportStatusLabel($"✓ Đã chọn Viewport #{vp.Number} trên layout.");
                    }
                    tr.Commit();
                }
                LoadLayers();
            }
        }

        private void ChkSelectAll_CheckedChanged(object? sender, EventArgs e)
        {
            bool isChecked = chkSelectAll.Checked;
            foreach (DataGridViewRow row in dgvLayers.Rows)
            {
                row.Cells["colCheck"].Value = isChecked;
            }
        }

        private List<ObjectId> GetCheckedLayerIds()
        {
            var list = new List<ObjectId>();
            foreach (DataGridViewRow row in dgvLayers.Rows)
            {
                bool isChecked = Convert.ToBoolean(row.Cells["colCheck"].Value);
                if (isChecked && row.Tag is VpLayerItem item)
                {
                    list.Add(item.LayerId);
                }
            }
            return list;
        }

        private void BtnFreezeChecked_Click(object? sender, EventArgs e)
        {
            var ids = GetCheckedLayerIds();
            if (ids.Count == 0)
            {
                MessageBox.Show("Vui lòng tick chọn ít nhất một Layer trong bảng.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int count = AT_TatVPFreezeLayerCmd.FreezeLayersInVp(_currentViewportId, ids);
            LoadLayers();
        }

        private void BtnThawChecked_Click(object? sender, EventArgs e)
        {
            var ids = GetCheckedLayerIds();
            if (ids.Count == 0)
            {
                MessageBox.Show("Vui lòng tick chọn ít nhất một Layer trong bảng.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int count = AT_TatVPFreezeLayerCmd.ThawLayersInVp(_currentViewportId, ids);
            LoadLayers();
        }

        private void BtnToggleChecked_Click(object? sender, EventArgs e)
        {
            var checkedItems = new List<VpLayerItem>();
            foreach (DataGridViewRow row in dgvLayers.Rows)
            {
                bool isChecked = Convert.ToBoolean(row.Cells["colCheck"].Value);
                if (isChecked && row.Tag is VpLayerItem item)
                {
                    checkedItems.Add(item);
                }
            }

            if (checkedItems.Count == 0)
            {
                MessageBox.Show("Vui lòng tick chọn ít nhất một Layer trong bảng.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var toFreeze = checkedItems.Where(x => !x.IsFrozenInVp).Select(x => x.LayerId).ToList();
            var toThaw = checkedItems.Where(x => x.IsFrozenInVp).Select(x => x.LayerId).ToList();

            if (toFreeze.Count > 0)
                AT_TatVPFreezeLayerCmd.FreezeLayersInVp(_currentViewportId, toFreeze);

            if (toThaw.Count > 0)
                AT_TatVPFreezeLayerCmd.ThawLayersInVp(_currentViewportId, toThaw);

            LoadLayers();
        }

        private void BtnThawAll_Click(object? sender, EventArgs e)
        {
            int frozenCount = _allLayers.Count(x => x.IsFrozenInVp);
            if (frozenCount == 0)
            {
                MessageBox.Show("Không có Layer nào đang bị VP Freeze trong Viewport này.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var dr = MessageBox.Show(
                $"Bạn có chắc chắn muốn RÃ ĐÔNG (Hiện lại) toàn bộ {frozenCount} layer đang bị đóng băng trong Viewport này?",
                "Xác nhận Rã đông tất cả",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (dr == DialogResult.Yes)
            {
                AT_TatVPFreezeLayerCmd.ThawAllLayersInVp(_currentViewportId);
                LoadLayers();
            }
        }

        #endregion

        #region Custom Cell Painting (CAD Color Badge)

        private void DgvLayers_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && dgvLayers.Columns[e.ColumnIndex].Name == "colColor")
            {
                e.PaintBackground(e.ClipBounds, (e.State & DataGridViewElementStates.Selected) != 0);

                if (dgvLayers.Rows[e.RowIndex].Tag is VpLayerItem item)
                {
                    DrawingColor gdiColor;
                    try
                    {
                        gdiColor = item.CadColor.ColorValue;
                    }
                    catch
                    {
                        gdiColor = DrawingColor.Black;
                    }

                    // Vẽ badge hình chữ nhật bo tròn / vuông nhỏ ở giữa ô
                    int boxW = 22;
                    int boxH = 14;
                    int boxX = e.CellBounds.Left + (e.CellBounds.Width - boxW) / 2;
                    int boxY = e.CellBounds.Top + (e.CellBounds.Height - boxH) / 2;

                    using (var brush = new SolidBrush(gdiColor))
                    {
                        e.Graphics.FillRectangle(brush, boxX, boxY, boxW, boxH);
                    }
                    using (var pen = new Pen(DrawingColor.FromArgb(160, 160, 160)))
                    {
                        e.Graphics.DrawRectangle(pen, boxX, boxY, boxW, boxH);
                    }
                }

                e.Handled = true;
            }
        }

        #endregion
    }
}

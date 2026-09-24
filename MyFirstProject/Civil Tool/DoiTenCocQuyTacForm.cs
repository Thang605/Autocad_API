// (C) Copyright 2026 by T27
// Form Đổi Tên Cọc Theo Quy Tắc (Tham khảo từ CTS_PHATSINHCOC)
// Hỗ trợ tự động phân tích: Cọc đặc biệt (DT, CT, TD, P, TC, ND, NC, D), Cọc Km tròn, Cọc H (H1, H2...), Cọc chi tiết (1, 2, 3...)

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using DrawingColor = System.Drawing.Color;
using DrawingFont = System.Drawing.Font;
using WinFormsButton = System.Windows.Forms.Button;
using WinFormsCheckBox = System.Windows.Forms.CheckBox;
using WinFormsComboBox = System.Windows.Forms.ComboBox;
using WinFormsDataGridView = System.Windows.Forms.DataGridView;
using WinFormsGroupBox = System.Windows.Forms.GroupBox;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsNumericUpDown = System.Windows.Forms.NumericUpDown;
using WinFormsPoint = System.Drawing.Point;
using WinFormsTextBox = System.Windows.Forms.TextBox;

namespace MyFirstProject.Civil_Tool
{
    /// <summary>
    /// Model dữ liệu cọc hiển thị trên DataGridView
    /// </summary>
    public class SampleLineItemModel
    {
        public ObjectId Id { get; set; } = ObjectId.Null;
        public bool IsSelected { get; set; } = true;
        public int Stt { get; set; }
        public double Station { get; set; }
        public string CurrentName { get; set; } = "";
        public string NewName { get; set; } = "";
        public string StakeType { get; set; } = "";
        public string Description { get; set; } = "";
    }

    /// <summary>
    /// Điểm đặc biệt của tuyến (Geometry Point)
    /// </summary>
    public class SpecialPointInfo
    {
        public double Station { get; set; }
        public string Code { get; set; } = "";
        public string Description { get; set; } = "";
    }

    /// <summary>
    /// Form giao diện đổi tên cọc theo quy tắc
    /// </summary>
    public class DoiTenCocQuyTacForm : Form
    {
        #region Persistent State (Rule 2)

        private static ObjectId _lastAlignmentId = ObjectId.Null;
        private static ObjectId _lastGroupId = ObjectId.Null;
        private static int _lastKmBatDau = 0;
        private static decimal _lastTolerance = 0.05m;
        private static bool _lastRenameDacBiet = true;
        private static bool _lastRenameKm = true;
        private static bool _lastRenameH = true;
        private static int _lastFormatH = 0; // 0: H1 (Km0), 1: H1, 2: Km0+H1
        private static bool _lastRenameChiTiet = true;
        private static string _lastPrefixChiTiet = "";
        private static int _lastStartNoChiTiet = 1;
        private static int _lastChiTietMode = 0; // 0: Liên tục toàn tuyến, 1: Reset theo Km
        private static Size _lastFormSize = new Size(960, 680);

        #endregion

        #region Fields

        private ObjectId _currentAlignmentId = ObjectId.Null;
        private ObjectId _currentGroupId = ObjectId.Null;
        private List<SampleLineItemModel> _items = new List<SampleLineItemModel>();
        private bool _isUpdatingUI = false;

        #endregion

        #region UI Controls

        // Banner
        private WinFormsLabel lblTitle = null!;
        private WinFormsLabel lblSubtitle = null!;

        // Group 1: Alignment & Group
        private WinFormsGroupBox grpAlignment = null!;
        private WinFormsLabel lblAlignment = null!;
        private WinFormsComboBox cmbAlignment = null!;
        private WinFormsButton btnPickAlignment = null!;
        private WinFormsLabel lblGroup = null!;
        private WinFormsComboBox cmbGroup = null!;
        private WinFormsLabel lblAlignInfo = null!;

        // Group 2: Quy tắc đặt tên
        private WinFormsGroupBox grpRules = null!;
        private WinFormsLabel lblKmBatDau = null!;
        private WinFormsNumericUpDown numKmBatDau = null!;
        private WinFormsLabel lblTolerance = null!;
        private WinFormsNumericUpDown numTolerance = null!;

        private WinFormsCheckBox chkRenameDacBiet = null!;
        private WinFormsCheckBox chkRenameKm = null!;
        private WinFormsCheckBox chkRenameH = null!;
        private WinFormsLabel lblFormatH = null!;
        private WinFormsComboBox cmbFormatH = null!;

        private WinFormsCheckBox chkRenameChiTiet = null!;
        private WinFormsLabel lblPrefixChiTiet = null!;
        private WinFormsTextBox txtPrefixChiTiet = null!;
        private WinFormsLabel lblStartNoChiTiet = null!;
        private WinFormsNumericUpDown numStartNoChiTiet = null!;
        private WinFormsLabel lblChiTietMode = null!;
        private WinFormsComboBox cmbChiTietMode = null!;

        // Group 3: Xem trước
        private WinFormsGroupBox grpPreview = null!;
        private WinFormsDataGridView dgvPreview = null!;
        private WinFormsButton btnSelectAll = null!;
        private WinFormsButton btnDeselectAll = null!;
        private WinFormsButton btnRefreshPreview = null!;
        private WinFormsLabel lblSummary = null!;

        // Action Buttons
        private WinFormsButton btnExecute = null!;
        private WinFormsButton btnCancel = null!;

        #endregion

        public DoiTenCocQuyTacForm(ObjectId initAlignmentId = default)
        {
            InitializeComponent();

            if (!initAlignmentId.IsNull && initAlignmentId.IsValid)
            {
                _currentAlignmentId = initAlignmentId;
            }
            else if (!_lastAlignmentId.IsNull && _lastAlignmentId.IsValid && !_lastAlignmentId.IsErased)
            {
                _currentAlignmentId = _lastAlignmentId;
            }

            RestoreLastSettings();
            LoadAlignments();

            this.FormClosing += (s, e) => SaveCurrentSettings();
        }

        #region Khởi tạo Giao diện (GUI)

        private void InitializeComponent()
        {
            var regularFont = new DrawingFont("Segoe UI", 9.5F, FontStyle.Regular);
            var boldFont = new DrawingFont("Segoe UI", 9.5F, FontStyle.Bold);
            var headerFont = new DrawingFont("Segoe UI", 13F, FontStyle.Bold);

            this.SuspendLayout();

            // Form Properties
            this.Text = "Đổi Tên Cọc Theo Quy Tắc (Geometry, Km, H, Chi Tiết)";
            this.Font = regularFont;
            this.Size = _lastFormSize;
            this.MinimumSize = new Size(880, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = DrawingColor.FromArgb(248, 249, 251);

            // Banner Title
            lblTitle = new WinFormsLabel
            {
                Text = "ĐỔI TÊN CỌC THEO QUY TẮC CHUẨN",
                Font = headerFont,
                ForeColor = DrawingColor.FromArgb(16, 76, 140),
                Location = new WinFormsPoint(20, 12),
                AutoSize = true
            };

            lblSubtitle = new WinFormsLabel
            {
                Text = "Tự động nhận diện cọc Đặc biệt (DT, CT, TD, P, TC, ND, NC), cọc Km, cọc H và đánh số cọc chi tiết theo chuẩn CTS_PHATSINHCOC.",
                ForeColor = DrawingColor.FromArgb(100, 110, 120),
                Location = new WinFormsPoint(21, 38),
                AutoSize = true
            };

            // Group 1: Tuyến & Nhóm cọc
            grpAlignment = new WinFormsGroupBox
            {
                Text = " 1. Tuyến thiết kế & Nhóm cọc (Sample Line Group) ",
                Font = boldFont,
                ForeColor = DrawingColor.FromArgb(30, 41, 59),
                Location = new WinFormsPoint(20, 65),
                Size = new Size(905, 95),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            lblAlignment = new WinFormsLabel
            {
                Text = "Tim tuyến:",
                Font = regularFont,
                Location = new WinFormsPoint(15, 28),
                Size = new Size(75, 23)
            };

            cmbAlignment = new WinFormsComboBox
            {
                Font = regularFont,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new WinFormsPoint(95, 25),
                Size = new Size(260, 25)
            };
            cmbAlignment.SelectedIndexChanged += CmbAlignment_SelectedIndexChanged;

            btnPickAlignment = new WinFormsButton
            {
                Text = "🎯 Pick tim...",
                Font = regularFont,
                Location = new WinFormsPoint(362, 24),
                Size = new Size(95, 27),
                BackColor = DrawingColor.White,
                FlatStyle = FlatStyle.System
            };
            btnPickAlignment.Click += BtnPickAlignment_Click;

            lblGroup = new WinFormsLabel
            {
                Text = "Nhóm cọc:",
                Font = regularFont,
                Location = new WinFormsPoint(475, 28),
                Size = new Size(75, 23)
            };

            cmbGroup = new WinFormsComboBox
            {
                Font = regularFont,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new WinFormsPoint(555, 25),
                Size = new Size(330, 25),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            cmbGroup.SelectedIndexChanged += CmbGroup_SelectedIndexChanged;

            lblAlignInfo = new WinFormsLabel
            {
                Text = "Chưa chọn tim tuyến.",
                Font = regularFont,
                ForeColor = DrawingColor.FromArgb(100, 116, 139),
                Location = new WinFormsPoint(95, 60),
                Size = new Size(790, 23),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            grpAlignment.Controls.AddRange(new Control[] {
                lblAlignment, cmbAlignment, btnPickAlignment,
                lblGroup, cmbGroup, lblAlignInfo
            });

            // Group 2: Quy tắc đặt tên
            grpRules = new WinFormsGroupBox
            {
                Text = " 2. Cấu hình quy tắc đặt tên cọc ",
                Font = boldFont,
                ForeColor = DrawingColor.FromArgb(30, 41, 59),
                Location = new WinFormsPoint(20, 168),
                Size = new Size(905, 130),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            // Hàng 1: Km bắt đầu & Dung sai
            lblKmBatDau = new WinFormsLabel
            {
                Text = "Km bắt đầu:",
                Font = regularFont,
                Location = new WinFormsPoint(15, 27),
                Size = new Size(85, 23)
            };

            numKmBatDau = new WinFormsNumericUpDown
            {
                Font = regularFont,
                Location = new WinFormsPoint(105, 25),
                Size = new Size(65, 25),
                Minimum = 0,
                Maximum = 9999,
                Value = 0
            };
            numKmBatDau.ValueChanged += (s, e) => UpdatePreview();

            lblTolerance = new WinFormsLabel
            {
                Text = "Dung sai (m):",
                Font = regularFont,
                Location = new WinFormsPoint(190, 27),
                Size = new Size(90, 23)
            };

            numTolerance = new WinFormsNumericUpDown
            {
                Font = regularFont,
                Location = new WinFormsPoint(285, 25),
                Size = new Size(65, 25),
                Minimum = 0.001m,
                Maximum = 2.0m,
                DecimalPlaces = 3,
                Increment = 0.01m,
                Value = 0.05m
            };
            numTolerance.ValueChanged += (s, e) => UpdatePreview();

            chkRenameDacBiet = new WinFormsCheckBox
            {
                Text = "Đổi tên Cọc đặc biệt (DT, CT, TD, P, TC, ND, NC, D...)",
                Font = regularFont,
                Location = new WinFormsPoint(375, 25),
                Size = new Size(380, 25),
                Checked = true
            };
            chkRenameDacBiet.CheckedChanged += (s, e) => UpdatePreview();

            // Hàng 2: Cọc Km & Cọc H
            chkRenameKm = new WinFormsCheckBox
            {
                Text = "Đổi tên cọc Km tròn (Km0, Km1...)",
                Font = regularFont,
                Location = new WinFormsPoint(15, 60),
                Size = new Size(240, 25),
                Checked = true
            };
            chkRenameKm.CheckedChanged += (s, e) => UpdatePreview();

            chkRenameH = new WinFormsCheckBox
            {
                Text = "Đổi tên cọc H (tròn 100m):",
                Font = regularFont,
                Location = new WinFormsPoint(260, 60),
                Size = new Size(185, 25),
                Checked = true
            };
            chkRenameH.CheckedChanged += (s, e) => {
                cmbFormatH.Enabled = chkRenameH.Checked;
                UpdatePreview();
            };

            lblFormatH = new WinFormsLabel
            {
                Text = "Định dạng:",
                Font = regularFont,
                Location = new WinFormsPoint(450, 62),
                Size = new Size(70, 23)
            };

            cmbFormatH = new WinFormsComboBox
            {
                Font = regularFont,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new WinFormsPoint(525, 59),
                Size = new Size(160, 25)
            };
            cmbFormatH.Items.AddRange(new object[] {
                "H1 (Km0) [Chuẩn CTS]",
                "H1, H2 [Rút gọn]",
                "Km0+H1 [Lý trình]"
            });
            cmbFormatH.SelectedIndex = 0;
            cmbFormatH.SelectedIndexChanged += (s, e) => UpdatePreview();

            // Hàng 3: Cọc chi tiết
            chkRenameChiTiet = new WinFormsCheckBox
            {
                Text = "Đổi tên cọc chi tiết",
                Font = regularFont,
                Location = new WinFormsPoint(15, 95),
                Size = new Size(150, 25),
                Checked = true
            };
            chkRenameChiTiet.CheckedChanged += (s, e) => {
                txtPrefixChiTiet.Enabled = chkRenameChiTiet.Checked;
                numStartNoChiTiet.Enabled = chkRenameChiTiet.Checked;
                cmbChiTietMode.Enabled = chkRenameChiTiet.Checked;
                UpdatePreview();
            };

            lblPrefixChiTiet = new WinFormsLabel
            {
                Text = "Tiền tố:",
                Font = regularFont,
                Location = new WinFormsPoint(175, 97),
                Size = new Size(55, 23)
            };

            txtPrefixChiTiet = new WinFormsTextBox
            {
                Font = regularFont,
                Location = new WinFormsPoint(235, 94),
                Size = new Size(60, 25),
                Text = ""
            };
            txtPrefixChiTiet.TextChanged += (s, e) => UpdatePreview();

            lblStartNoChiTiet = new WinFormsLabel
            {
                Text = "Số bắt đầu:",
                Font = regularFont,
                Location = new WinFormsPoint(310, 97),
                Size = new Size(80, 23)
            };

            numStartNoChiTiet = new WinFormsNumericUpDown
            {
                Font = regularFont,
                Location = new WinFormsPoint(395, 94),
                Size = new Size(60, 25),
                Minimum = 1,
                Maximum = 9999,
                Value = 1
            };
            numStartNoChiTiet.ValueChanged += (s, e) => UpdatePreview();

            lblChiTietMode = new WinFormsLabel
            {
                Text = "Cách đánh số:",
                Font = regularFont,
                Location = new WinFormsPoint(475, 97),
                Size = new Size(95, 23)
            };

            cmbChiTietMode = new WinFormsComboBox
            {
                Font = regularFont,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new WinFormsPoint(575, 94),
                Size = new Size(210, 25)
            };
            cmbChiTietMode.Items.AddRange(new object[] {
                "Liên tục toàn tuyến (1, 2, 3...)",
                "Reset theo từng Km (1, 2 trong mỗi Km)"
            });
            cmbChiTietMode.SelectedIndex = 0;
            cmbChiTietMode.SelectedIndexChanged += (s, e) => UpdatePreview();

            grpRules.Controls.AddRange(new Control[] {
                lblKmBatDau, numKmBatDau, lblTolerance, numTolerance, chkRenameDacBiet,
                chkRenameKm, chkRenameH, lblFormatH, cmbFormatH,
                chkRenameChiTiet, lblPrefixChiTiet, txtPrefixChiTiet,
                lblStartNoChiTiet, numStartNoChiTiet, lblChiTietMode, cmbChiTietMode
            });

            // Group 3: Preview DataGridView
            grpPreview = new WinFormsGroupBox
            {
                Text = " 3. Xem trước danh sách cọc & Tên mới dự kiến ",
                Font = boldFont,
                ForeColor = DrawingColor.FromArgb(30, 41, 59),
                Location = new WinFormsPoint(20, 305),
                Size = new Size(905, 275),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            btnSelectAll = new WinFormsButton
            {
                Text = "☑ Chọn tất cả",
                Font = regularFont,
                Location = new WinFormsPoint(15, 24),
                Size = new Size(110, 26),
                BackColor = DrawingColor.White,
                FlatStyle = FlatStyle.System
            };
            btnSelectAll.Click += (s, e) => SetSelectAll(true);

            btnDeselectAll = new WinFormsButton
            {
                Text = "☐ Bỏ chọn tất cả",
                Font = regularFont,
                Location = new WinFormsPoint(132, 24),
                Size = new Size(125, 26),
                BackColor = DrawingColor.White,
                FlatStyle = FlatStyle.System
            };
            btnDeselectAll.Click += (s, e) => SetSelectAll(false);

            btnRefreshPreview = new WinFormsButton
            {
                Text = "🔄 Cập nhật xem trước",
                Font = regularFont,
                Location = new WinFormsPoint(265, 24),
                Size = new Size(160, 26),
                BackColor = DrawingColor.White,
                FlatStyle = FlatStyle.System
            };
            btnRefreshPreview.Click += (s, e) => UpdatePreview();

            lblSummary = new WinFormsLabel
            {
                Text = "Tổng số: 0 cọc",
                Font = boldFont,
                ForeColor = DrawingColor.FromArgb(16, 76, 140),
                Location = new WinFormsPoint(440, 27),
                Size = new Size(450, 23),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                TextAlign = ContentAlignment.MiddleRight
            };

            dgvPreview = new WinFormsDataGridView
            {
                Font = regularFont,
                Location = new WinFormsPoint(15, 56),
                Size = new Size(875, 205),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = DrawingColor.White,
                BorderStyle = BorderStyle.Fixed3D,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false
            };
            SetupDataGridViewColumns();

            grpPreview.Controls.AddRange(new Control[] {
                btnSelectAll, btnDeselectAll, btnRefreshPreview, lblSummary, dgvPreview
            });

            // Action Buttons
            btnExecute = new WinFormsButton
            {
                Text = "✔ Thực hiện đổi tên",
                Font = boldFont,
                ForeColor = DrawingColor.White,
                BackColor = DrawingColor.FromArgb(16, 120, 220),
                Location = new WinFormsPoint(665, 592),
                Size = new Size(160, 36),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnExecute.FlatAppearance.BorderSize = 0;
            btnExecute.Click += BtnExecute_Click;

            btnCancel = new WinFormsButton
            {
                Text = "Đóng",
                Font = regularFont,
                Location = new WinFormsPoint(835, 592),
                Size = new Size(90, 36),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                BackColor = DrawingColor.White,
                FlatStyle = FlatStyle.System
            };
            btnCancel.Click += (s, e) => this.Close();

            // Add all to Form
            this.Controls.AddRange(new Control[] {
                lblTitle, lblSubtitle,
                grpAlignment, grpRules, grpPreview,
                btnExecute, btnCancel
            });

            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private void SetupDataGridViewColumns()
        {
            dgvPreview.Columns.Clear();

            var colCheck = new DataGridViewCheckBoxColumn
            {
                HeaderText = "Chọn",
                DataPropertyName = "IsSelected",
                Width = 55,
                FillWeight = 30
            };

            var colStt = new DataGridViewTextBoxColumn
            {
                HeaderText = "STT",
                DataPropertyName = "Stt",
                Width = 50,
                FillWeight = 25,
                ReadOnly = true
            };

            var colStation = new DataGridViewTextBoxColumn
            {
                HeaderText = "Lý trình (Station)",
                DataPropertyName = "StationText",
                Width = 130,
                FillWeight = 60,
                ReadOnly = true
            };

            var colOldName = new DataGridViewTextBoxColumn
            {
                HeaderText = "Tên hiện tại",
                DataPropertyName = "CurrentName",
                Width = 140,
                FillWeight = 70,
                ReadOnly = true
            };

            var colNewName = new DataGridViewTextBoxColumn
            {
                HeaderText = "Tên mới dự kiến",
                DataPropertyName = "NewName",
                Width = 160,
                FillWeight = 85
            };

            var colType = new DataGridViewTextBoxColumn
            {
                HeaderText = "Phân loại",
                DataPropertyName = "StakeType",
                Width = 160,
                FillWeight = 80,
                ReadOnly = true
            };

            var colDesc = new DataGridViewTextBoxColumn
            {
                HeaderText = "Ghi chú",
                DataPropertyName = "Description",
                Width = 180,
                FillWeight = 90,
                ReadOnly = true
            };

            dgvPreview.Columns.AddRange(new DataGridViewColumn[] {
                colCheck, colStt, colStation, colOldName, colNewName, colType, colDesc
            });

            dgvPreview.CellContentClick += (s, e) => {
                if (e.RowIndex >= 0 && e.ColumnIndex == 0)
                {
                    dgvPreview.CommitEdit(DataGridViewDataErrorContexts.Commit);
                    UpdateSummaryText();
                }
            };

            dgvPreview.CellValueChanged += (s, e) => {
                if (e.RowIndex >= 0 && (e.ColumnIndex == 0 || e.ColumnIndex == 4))
                {
                    UpdateSummaryText();
                }
            };
        }

        #endregion

        #region Nạp Dữ liệu & Sự kiện Alignment / Group

        private void LoadAlignments()
        {
            _isUpdatingUI = true;
            cmbAlignment.Items.Clear();

            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                _isUpdatingUI = false;
                return;
            }

            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var cdoc = CivilApplication.ActiveDocument;
                if (cdoc != null)
                {
                    int selectedIdx = -1;
                    int i = 0;
                    foreach (ObjectId alId in cdoc.GetAlignmentIds())
                    {
                        var al = tr.GetObject(alId, OpenMode.ForRead) as Alignment;
                        if (al != null)
                        {
                            cmbAlignment.Items.Add(new AlignmentComboItem(al.Name, alId));
                            if (alId == _currentAlignmentId)
                            {
                                selectedIdx = i;
                            }
                            i++;
                        }
                    }

                    if (selectedIdx >= 0)
                    {
                        cmbAlignment.SelectedIndex = selectedIdx;
                    }
                    else if (cmbAlignment.Items.Count > 0)
                    {
                        cmbAlignment.SelectedIndex = 0;
                    }
                }
                tr.Commit();
            }

            _isUpdatingUI = false;
            OnAlignmentChanged();
        }

        private void CmbAlignment_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_isUpdatingUI) return;
            OnAlignmentChanged();
        }

        private void OnAlignmentChanged()
        {
            if (cmbAlignment.SelectedItem is AlignmentComboItem item)
            {
                _currentAlignmentId = item.Id;
                LoadGroupsForAlignment(_currentAlignmentId);
                UpdateAlignmentInfo();
                UpdatePreview();
            }
            else
            {
                _currentAlignmentId = ObjectId.Null;
                cmbGroup.Items.Clear();
                lblAlignInfo.Text = "Chưa chọn tim tuyến.";
                _items.Clear();
                RefreshGrid();
            }
        }

        private void LoadGroupsForAlignment(ObjectId alignmentId)
        {
            _isUpdatingUI = true;
            cmbGroup.Items.Clear();

            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null || alignmentId.IsNull)
            {
                _isUpdatingUI = false;
                return;
            }

            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var al = tr.GetObject(alignmentId, OpenMode.ForRead) as Alignment;
                if (al != null)
                {
                    ObjectIdCollection groupIds = al.GetSampleLineGroupIds();
                    int selectedIdx = -1;
                    int i = 0;
                    foreach (ObjectId grpId in groupIds)
                    {
                        var grp = tr.GetObject(grpId, OpenMode.ForRead) as SampleLineGroup;
                        if (grp != null)
                        {
                            int slCount = grp.GetSampleLineIds().Count;
                            cmbGroup.Items.Add(new SampleLineGroupComboItem($"{grp.Name} ({slCount} cọc)", grpId));
                            if (grpId == _currentGroupId)
                            {
                                selectedIdx = i;
                            }
                            i++;
                        }
                    }

                    if (selectedIdx >= 0)
                    {
                        cmbGroup.SelectedIndex = selectedIdx;
                    }
                    else if (cmbGroup.Items.Count > 0)
                    {
                        cmbGroup.SelectedIndex = 0;
                    }
                }
                tr.Commit();
            }

            _isUpdatingUI = false;
            if (cmbGroup.SelectedItem is SampleLineGroupComboItem gItem)
            {
                _currentGroupId = gItem.Id;
            }
            else
            {
                _currentGroupId = ObjectId.Null;
            }
        }

        private void CmbGroup_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_isUpdatingUI) return;
            if (cmbGroup.SelectedItem is SampleLineGroupComboItem gItem)
            {
                _currentGroupId = gItem.Id;
            }
            else
            {
                _currentGroupId = ObjectId.Null;
            }
            UpdatePreview();
        }

        private void UpdateAlignmentInfo()
        {
            if (_currentAlignmentId.IsNull)
            {
                lblAlignInfo.Text = "Chưa chọn tim tuyến.";
                return;
            }

            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var al = tr.GetObject(_currentAlignmentId, OpenMode.ForRead) as Alignment;
                if (al != null)
                {
                    lblAlignInfo.Text = $"Chiều dài tuyến: {al.Length:F2} m | Lý trình: {FormatStation(al.StartingStation)} ➔ {FormatStation(al.EndingStation)}";
                }
                tr.Commit();
            }
        }

        private void BtnPickAlignment_Click(object? sender, EventArgs e)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var ed = doc.Editor;
            this.Hide();

            try
            {
                using (ed.StartUserInteraction(this))
                {
                    var peo = new PromptEntityOptions("\nChọn tim tuyến Alignment trên bản vẽ: ");
                    peo.SetRejectMessage("\nĐối tượng chọn phải là Alignment!");
                    peo.AddAllowedClass(typeof(Alignment), exactMatch: true);

                    var per = ed.GetEntity(peo);
                    if (per.Status == PromptStatus.OK)
                    {
                        _currentAlignmentId = per.ObjectId;
                        LoadAlignments();
                    }
                }
            }
            finally
            {
                this.Show();
                this.BringToFront();
            }
        }

        #endregion

        #region Thuật toán Phân tích & Tính Tên Mới (Tham khảo CTS_PHATSINHCOC)

        /// <summary>
        /// Thu thập danh sách các điểm đặc biệt của tuyến
        /// </summary>
        private List<SpecialPointInfo> GetAlignmentSpecialPoints(Alignment al, Transaction tr)
        {
            var points = new List<SpecialPointInfo>();

            // 1. Điểm đầu & cuối
            points.Add(new SpecialPointInfo { Station = al.StartingStation, Code = "DT", Description = "Điểm đầu tuyến" });
            points.Add(new SpecialPointInfo { Station = al.EndingStation, Code = "CT", Description = "Điểm cuối tuyến" });

            // 2. Phân tích qua Alignment Entities (Các đoạn cong tròn & chuyển tiếp)
            try
            {
                int curveIdx = 1;
                for (int i = 0; i < al.Entities.Count; i++)
                {
                    var entity = al.Entities[i];

                    // Đường cong tròn
                    if (entity is AlignmentArc arc)
                    {
                        // Kiểm tra xem trước đó có Spiral không (đường cong chuyển tiếp)
                        bool hasLeadingSpiral = (i > 0 && al.Entities[i - 1] is AlignmentSpiral);
                        bool hasTrailingSpiral = (i < al.Entities.Count - 1 && al.Entities[i + 1] is AlignmentSpiral);

                        if (hasLeadingSpiral && i > 0)
                        {
                            var spLead = (AlignmentSpiral)al.Entities[i - 1];
                            points.Add(new SpecialPointInfo { Station = spLead.StartStation, Code = $"ND{curveIdx}", Description = $"Nối đầu đường cong {curveIdx}" });
                            points.Add(new SpecialPointInfo { Station = spLead.EndStation, Code = $"TD{curveIdx}", Description = $"Tiếp đầu đường cong {curveIdx}" });
                        }
                        else
                        {
                            points.Add(new SpecialPointInfo { Station = arc.StartStation, Code = $"TD{curveIdx}", Description = $"Tiếp đầu đường cong {curveIdx}" });
                        }

                        // Điểm giữa cong (Phân giác)
                        double midStation = (arc.StartStation + arc.EndStation) / 2.0;
                        points.Add(new SpecialPointInfo { Station = midStation, Code = $"P{curveIdx}", Description = $"Phân giác đường cong {curveIdx}" });

                        if (hasTrailingSpiral && i < al.Entities.Count - 1)
                        {
                            var spTrail = (AlignmentSpiral)al.Entities[i + 1];
                            points.Add(new SpecialPointInfo { Station = spTrail.StartStation, Code = $"TC{curveIdx}", Description = $"Tiếp cuối đường cong {curveIdx}" });
                            points.Add(new SpecialPointInfo { Station = spTrail.EndStation, Code = $"NC{curveIdx}", Description = $"Nối cuối đường cong {curveIdx}" });
                        }
                        else
                        {
                            points.Add(new SpecialPointInfo { Station = arc.EndStation, Code = $"TC{curveIdx}", Description = $"Tiếp cuối đường cong {curveIdx}" });
                        }

                        curveIdx++;
                    }
                }
            }
            catch
            {
                // Fallback nếu không đọc được entities
            }

            // 3. Đọc thêm từ GetStationSet(StationTypes.GeometryPoint) cho các đỉnh TanTan (D1, D2...)
            try
            {
                Station[] geomStations = al.GetStationSet(StationTypes.GeometryPoint);
                int dinhIdx = 1;
                foreach (var st in geomStations)
                {
                    if (st.GeometryStationType == AlignmentGeometryPointStationType.TanTan)
                    {
                        // Kiểm tra nếu chưa trùng lý trình với điểm nào
                        if (!points.Any(p => Math.Abs(p.Station - st.RawStation) < 0.05))
                        {
                            points.Add(new SpecialPointInfo { Station = st.RawStation, Code = $"D{dinhIdx}", Description = $"Đỉnh chuyển hướng {dinhIdx}" });
                            dinhIdx++;
                        }
                    }
                }
            }
            catch
            {
                // Bỏ qua nếu lỗi
            }

            // Sắp xếp theo station
            return points.OrderBy(p => p.Station).ToList();
        }

        /// <summary>
        /// Cập nhật bảng xem trước Preview theo các tùy chọn hiện tại
        /// </summary>
        private void UpdatePreview()
        {
            if (_isUpdatingUI) return;

            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null || _currentAlignmentId.IsNull || _currentGroupId.IsNull)
            {
                _items.Clear();
                RefreshGrid();
                return;
            }

            // Lưu lại trạng thái checkbox đã chọn trước đó nếu cùng danh sách
            var prevSelectedMap = _items.ToDictionary(x => x.Id, x => x.IsSelected);

            _items.Clear();

            int kmBatDau = (int)numKmBatDau.Value;
            double tolerance = (double)numTolerance.Value;
            bool doDacBiet = chkRenameDacBiet.Checked;
            bool doKm = chkRenameKm.Checked;
            bool doH = chkRenameH.Checked;
            int formatH = cmbFormatH.SelectedIndex;
            bool doChiTiet = chkRenameChiTiet.Checked;
            string prefixChiTiet = txtPrefixChiTiet.Text.Trim();
            int startNoChiTiet = (int)numStartNoChiTiet.Value;
            int chiTietMode = cmbChiTietMode.SelectedIndex;

            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var al = tr.GetObject(_currentAlignmentId, OpenMode.ForRead) as Alignment;
                var grp = tr.GetObject(_currentGroupId, OpenMode.ForRead) as SampleLineGroup;

                if (al != null && grp != null)
                {
                    // Lấy danh sách điểm đặc biệt
                    List<SpecialPointInfo> specialPoints = GetAlignmentSpecialPoints(al, tr);

                    // Đọc tất cả cọc trong Group
                    var rawSampleLines = new List<Tuple<ObjectId, double, string>>();
                    foreach (ObjectId slId in grp.GetSampleLineIds())
                    {
                        var sl = tr.GetObject(slId, OpenMode.ForRead) as SampleLine;
                        if (sl != null)
                        {
                            rawSampleLines.Add(Tuple.Create(slId, sl.Station, sl.Name));
                        }
                    }

                    // Sắp xếp cọc theo Station tăng dần
                    rawSampleLines = rawSampleLines.OrderBy(x => x.Item2).ToList();

                    int stt = 1;
                    int chiTietCounter = startNoChiTiet;
                    int lastKmIndex = -1;

                    foreach (var raw in rawSampleLines)
                    {
                        ObjectId slId = raw.Item1;
                        double station = raw.Item2;
                        string oldName = raw.Item3;
                        string newName = oldName;
                        string stakeType = "Giữ nguyên";
                        string desc = "";

                        // Tính số Km tương ứng: kmIndex
                        int kmIndex = (int)Math.Floor(station / 1000.0) + kmBatDau;

                        // Nếu mode reset theo Km: reset bộ đếm khi sang Km mới
                        if (chiTietMode == 1 && kmIndex != lastKmIndex)
                        {
                            chiTietCounter = startNoChiTiet;
                            lastKmIndex = kmIndex;
                        }

                        // 1. Kiểm tra cọc Đặc biệt (Ưu tiên cao nhất)
                        var matchedSpec = specialPoints.FirstOrDefault(p => Math.Abs(p.Station - station) <= tolerance);
                        if (matchedSpec != null && doDacBiet)
                        {
                            newName = matchedSpec.Code;
                            stakeType = $"Đặc biệt ({matchedSpec.Code})";
                            desc = matchedSpec.Description;
                        }
                        // 2. Kiểm tra cọc Km tròn (chia hết cho 1000m)
                        else if (IsDivisibleBy(station, 1000.0, tolerance) && doKm)
                        {
                            newName = $"Km{kmIndex}";
                            stakeType = "Cọc Km";
                            desc = $"Cọc tròn Km tại lý trình {FormatStation(station)}";
                        }
                        // 3. Kiểm tra cọc H (chia hết cho 100m nhưng không chia hết cho 1000m)
                        else if (IsDivisibleBy(station, 100.0, tolerance) && doH)
                        {
                            int hNumber = (int)Math.Round((station % 1000.0) / 100.0);
                            if (hNumber == 0) hNumber = 10;

                            if (formatH == 0) // Chuẩn CTS_PhatSinhCoc: H1 (Km0)
                            {
                                newName = $"H{hNumber} (Km{kmIndex})";
                            }
                            else if (formatH == 1) // Rút gọn: H1
                            {
                                newName = $"H{hNumber}";
                            }
                            else // Km0+H1
                            {
                                newName = $"Km{kmIndex}+H{hNumber}";
                            }

                            stakeType = $"Cọc H (H{hNumber})";
                            desc = $"Cọc H {hNumber} tại lý trình {FormatStation(station)}";
                        }
                        // 4. Cọc chi tiết còn lại
                        else if (doChiTiet)
                        {
                            newName = $"{prefixChiTiet}{chiTietCounter}";
                            stakeType = "Cọc chi tiết";
                            desc = $"Đánh số thứ tự #{chiTietCounter}";
                            chiTietCounter++;
                        }
                        else
                        {
                            newName = oldName;
                            stakeType = "Cọc chi tiết (Giữ nguyên)";
                            desc = "Không đổi tên theo tùy chọn";
                        }

                        bool isSelected = true;
                        if (prevSelectedMap.TryGetValue(slId, out bool wasSelected))
                        {
                            isSelected = wasSelected;
                        }

                        _items.Add(new SampleLineItemModel
                        {
                            Id = slId,
                            IsSelected = isSelected,
                            Stt = stt++,
                            Station = station,
                            CurrentName = oldName,
                            NewName = newName,
                            StakeType = stakeType,
                            Description = desc
                        });
                    }
                }
                tr.Commit();
            }

            RefreshGrid();
        }

        private static bool IsDivisibleBy(double station, double step, double tolerance)
        {
            double rem = Math.Abs(station % step);
            return rem <= tolerance || Math.Abs(rem - step) <= tolerance;
        }

        private static string FormatStation(double station)
        {
            long km = (long)Math.Floor(station / 1000.0);
            double m = station - (km * 1000.0);
            return $"Km{km}+{m:000.00}";
        }

        private void RefreshGrid()
        {
            _isUpdatingUI = true;

            dgvPreview.Rows.Clear();
            foreach (var item in _items)
            {
                int rowIdx = dgvPreview.Rows.Add(
                    item.IsSelected,
                    item.Stt,
                    FormatStation(item.Station),
                    item.CurrentName,
                    item.NewName,
                    item.StakeType,
                    item.Description
                );

                var row = dgvPreview.Rows[rowIdx];
                row.Tag = item;

                // Đổi màu nhẹ theo loại cọc để phân biệt trực quan
                if (item.StakeType.StartsWith("Đặc biệt"))
                {
                    row.DefaultCellStyle.BackColor = DrawingColor.FromArgb(254, 243, 199); // Vàng nhạt
                }
                else if (item.StakeType.StartsWith("Cọc Km"))
                {
                    row.DefaultCellStyle.BackColor = DrawingColor.FromArgb(224, 242, 254); // Xanh dương nhạt
                }
                else if (item.StakeType.StartsWith("Cọc H"))
                {
                    row.DefaultCellStyle.BackColor = DrawingColor.FromArgb(240, 253, 244); // Xanh lá nhạt
                }
            }

            _isUpdatingUI = false;
            UpdateSummaryText();
        }

        private void SetSelectAll(bool select)
        {
            _isUpdatingUI = true;
            foreach (DataGridViewRow row in dgvPreview.Rows)
            {
                row.Cells[0].Value = select;
                if (row.Tag is SampleLineItemModel item)
                {
                    item.IsSelected = select;
                }
            }
            _isUpdatingUI = false;
            UpdateSummaryText();
        }

        private void UpdateSummaryText()
        {
            int selectedCount = 0;
            int total = dgvPreview.Rows.Count;

            foreach (DataGridViewRow row in dgvPreview.Rows)
            {
                if (Convert.ToBoolean(row.Cells[0].Value))
                {
                    selectedCount++;
                }
            }

            lblSummary.Text = $"Đã chọn: {selectedCount}/{total} cọc sẽ được đổi tên.";
        }

        #endregion

        #region Thực thi Đổi Tên (2-Phase Rename Tránh Conflict)

        private void BtnExecute_Click(object? sender, EventArgs e)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null || _currentGroupId.IsNull)
            {
                MessageBox.Show("Vui lòng chọn tim tuyến và nhóm cọc hợp lệ!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Đồng bộ dữ liệu từ DataGridView vào model
            var itemsToRename = new List<SampleLineItemModel>();
            foreach (DataGridViewRow row in dgvPreview.Rows)
            {
                bool isSelected = Convert.ToBoolean(row.Cells[0].Value);
                string newName = Convert.ToString(row.Cells[4].Value)?.Trim() ?? "";

                if (row.Tag is SampleLineItemModel item)
                {
                    item.IsSelected = isSelected;
                    item.NewName = newName;

                    if (isSelected && !string.IsNullOrEmpty(newName) && newName != item.CurrentName)
                    {
                        itemsToRename.Add(item);
                    }
                }
            }

            if (itemsToRename.Count == 0)
            {
                MessageBox.Show("Không có cọc nào cần đổi tên (tên mới trùng tên cũ hoặc chưa chọn cọc nào)!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Kiểm tra trùng tên trong danh sách mới
            var nameGroups = itemsToRename.GroupBy(x => x.NewName).Where(g => g.Count() > 1).ToList();
            if (nameGroups.Any())
            {
                string dupNames = string.Join(", ", nameGroups.Select(g => $"'{g.Key}' ({g.Count()} lần)"));
                var dr = MessageBox.Show(
                    $"Phát hiện trùng tên trong danh sách tên mới: {dupNames}.\n\nBạn có muốn tự động thêm hậu tố phân biệt (_1, _2...) để tiếp tục không?",
                    "Cảnh báo trùng tên cọc",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (dr != DialogResult.Yes)
                {
                    return;
                }

                // Tự động phân biệt trùng tên
                foreach (var group in nameGroups)
                {
                    int subIdx = 1;
                    foreach (var itm in group)
                    {
                        itm.NewName = $"{itm.NewName}_{subIdx++}";
                    }
                }
            }

            var confirmResult = MessageBox.Show(
                $"Xác nhận đổi tên cho {itemsToRename.Count} cọc trong nhóm cọc đã chọn?",
                "Xác nhận thực hiện",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirmResult != DialogResult.Yes) return;

            var ed = doc.Editor;
            int successCount = 0;
            int errorCount = 0;

            try
            {
                using (var docLock = doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    var grp = tr.GetObject(_currentGroupId, OpenMode.ForWrite) as SampleLineGroup;
                    if (grp == null)
                    {
                        MessageBox.Show("Không thể mở SampleLineGroup để ghi!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    // PHA 1: Đổi toàn bộ các cọc được chọn sang tên tạm duy nhất để giải phóng không gian tên
                    string batchId = Guid.NewGuid().ToString("N").Substring(0, 6);
                    for (int i = 0; i < itemsToRename.Count; i++)
                    {
                        var itm = itemsToRename[i];
                        var sl = tr.GetObject(itm.Id, OpenMode.ForWrite) as SampleLine;
                        if (sl != null)
                        {
                            sl.Name = $"__TMP_{batchId}_{i}";
                        }
                    }

                    // PHA 2: Đổi từ tên tạm sang tên mới chính thức
                    for (int i = 0; i < itemsToRename.Count; i++)
                    {
                        var itm = itemsToRename[i];
                        var sl = tr.GetObject(itm.Id, OpenMode.ForWrite) as SampleLine;
                        if (sl != null)
                        {
                            try
                            {
                                sl.Name = itm.NewName;
                                successCount++;
                            }
                            catch (Exception ex)
                            {
                                ed.WriteMessage($"\n⚠️ Lỗi khi gán tên '{itm.NewName}' cho cọc tại station {FormatStation(itm.Station)}: {ex.Message}");
                                errorCount++;
                            }
                        }
                    }

                    tr.Commit();
                }

                ed.WriteMessage($"\n🎉 Đã hoàn thành đổi tên: {successCount} cọc thành công, {errorCount} cọc lỗi.\n");

                MessageBox.Show(
                    $"Đã đổi tên thành công cho {successCount} cọc!" + (errorCount > 0 ? $"\n({errorCount} cọc gặp lỗi, xem chi tiết trong cửa sổ CAD Command)." : ""),
                    "Thành công",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                // Cập nhật lại giao diện
                UpdatePreview();
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n❌ Lỗi trong quá trình đổi tên cọc: {ex.Message}\n");
                MessageBox.Show($"Lỗi thực thi: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        #region Lưu & Khôi phục Trạng thái (Rule 2)

        private void SaveCurrentSettings()
        {
            _lastAlignmentId = _currentAlignmentId;
            _lastGroupId = _currentGroupId;
            _lastKmBatDau = (int)numKmBatDau.Value;
            _lastTolerance = numTolerance.Value;
            _lastRenameDacBiet = chkRenameDacBiet.Checked;
            _lastRenameKm = chkRenameKm.Checked;
            _lastRenameH = chkRenameH.Checked;
            _lastFormatH = cmbFormatH.SelectedIndex;
            _lastRenameChiTiet = chkRenameChiTiet.Checked;
            _lastPrefixChiTiet = txtPrefixChiTiet.Text;
            _lastStartNoChiTiet = (int)numStartNoChiTiet.Value;
            _lastChiTietMode = cmbChiTietMode.SelectedIndex;
            _lastFormSize = this.Size;
        }

        private void RestoreLastSettings()
        {
            numKmBatDau.Value = _lastKmBatDau;
            numTolerance.Value = _lastTolerance;
            chkRenameDacBiet.Checked = _lastRenameDacBiet;
            chkRenameKm.Checked = _lastRenameKm;
            chkRenameH.Checked = _lastRenameH;
            if (_lastFormatH >= 0 && _lastFormatH < cmbFormatH.Items.Count)
            {
                cmbFormatH.SelectedIndex = _lastFormatH;
            }
            chkRenameChiTiet.Checked = _lastRenameChiTiet;
            txtPrefixChiTiet.Text = _lastPrefixChiTiet;
            numStartNoChiTiet.Value = _lastStartNoChiTiet;
            if (_lastChiTietMode >= 0 && _lastChiTietMode < cmbChiTietMode.Items.Count)
            {
                cmbChiTietMode.SelectedIndex = _lastChiTietMode;
            }
            this.Size = _lastFormSize;
        }

        #endregion

        #region Helper Classes for ComboBox

        private class AlignmentComboItem
        {
            public string Name { get; }
            public ObjectId Id { get; }

            public AlignmentComboItem(string name, ObjectId id)
            {
                Name = name;
                Id = id;
            }

            public override string ToString() => Name;
        }

        private class SampleLineGroupComboItem
        {
            public string Name { get; }
            public ObjectId Id { get; }

            public SampleLineGroupComboItem(string name, ObjectId id)
            {
                Name = name;
                Id = id;
            }

            public override string ToString() => Name;
        }

        #endregion
    }
}

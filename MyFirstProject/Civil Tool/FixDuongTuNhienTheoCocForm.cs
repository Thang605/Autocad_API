// (C) Copyright 2026 by T27
// Form Sửa Đường Tự Nhiên Theo Cọc (CTP_Fix_DuongTuNhien_TheoCoc)
// Cho phép lựa chọn linh hoạt phạm vi đoạn cần fix (toàn tuyến hoặc theo đoạn cọc / lý trình),
// chọn lọc cọc trong DataGridView, kiểm soát tạo Profile mới hoặc cập nhật Profile có sẵn.

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
using Autodesk.Civil.DatabaseServices.Styles;
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
using WinFormsPanel = System.Windows.Forms.Panel;
using WinFormsPoint = System.Drawing.Point;
using WinFormsRadioButton = System.Windows.Forms.RadioButton;
using WinFormsTextBox = System.Windows.Forms.TextBox;

namespace MyFirstProject.Civil_Tool
{
    /// <summary>
    /// Model cọc hiển thị trong bảng dữ liệu
    /// </summary>
    public class FixStakeItemModel
    {
        public ObjectId SampleLineId { get; set; } = ObjectId.Null;
        public bool IsSelected { get; set; } = true;
        public int Stt { get; set; }
        public string Name { get; set; } = "";
        public double Station { get; set; }
        public string FormattedStation { get; set; } = "";
        public double Elevation { get; set; }
        public bool HasElevation { get; set; } = true;
        public string Status { get; set; } = "Hợp lệ";
    }

    /// <summary>
    /// Item dùng cho ComboBox
    /// </summary>
    public class EntityComboItem
    {
        public string Name { get; set; } = "";
        public ObjectId Id { get; set; } = ObjectId.Null;
        public double TagValue { get; set; } = 0;

        public EntityComboItem(string name, ObjectId id, double tagValue = 0)
        {
            Name = name;
            Id = id;
            TagValue = tagValue;
        }

        public override string ToString() => Name;
    }

    /// <summary>
    /// Form giao diện chọn đoạn cần fix đường tự nhiên theo cọc
    /// </summary>
    public class FixDuongTuNhienTheoCocForm : Form
    {
        #region Persistent State (Rule 2)

        private static ObjectId _lastAlignmentId = ObjectId.Null;
        private static ObjectId _lastProfileId = ObjectId.Null;
        private static ObjectId _lastGroupId = ObjectId.Null;
        private static bool _lastIsRangeMode = false;
        private static decimal _lastStartStation = 0;
        private static decimal _lastEndStation = 0;
        private static bool _lastIncludeBoundary = true;
        private static string _lastTargetProfileName = "";
        private static bool _lastIsUpdateExisting = false;
        private static string _lastProfileStyleName = "0.TN";
        private static string _lastLabelSetStyleName = "_No Labels";
        private static bool _lastChangeSourceStyle = true;
        private static Size _lastFormSize = new Size(980, 750);

        #endregion

        #region Fields

        private ObjectId _currentAlignmentId = ObjectId.Null;
        private ObjectId _currentProfileId = ObjectId.Null;
        private ObjectId _currentGroupId = ObjectId.Null;
        private List<FixStakeItemModel> _stakes = new List<FixStakeItemModel>();
        private bool _isUpdatingUI = false;

        #endregion

        #region UI Controls

        // Banner Title
        private WinFormsLabel lblTitle = null!;
        private WinFormsLabel lblSubtitle = null!;

        // Group 1: Tuyến & Đường tự nhiên gốc
        private WinFormsGroupBox grpSource = null!;
        private WinFormsLabel lblAlignment = null!;
        private WinFormsComboBox cmbAlignment = null!;
        private WinFormsButton btnPickAlignment = null!;
        private WinFormsLabel lblProfile = null!;
        private WinFormsComboBox cmbProfile = null!;
        private WinFormsButton btnPickProfile = null!;
        private WinFormsLabel lblGroup = null!;
        private WinFormsComboBox cmbGroup = null!;
        private WinFormsLabel lblSourceInfo = null!;

        // Group 2: Phạm vi đoạn cần fix
        private WinFormsGroupBox grpRange = null!;
        private WinFormsRadioButton radAll = null!;
        private WinFormsRadioButton radRange = null!;
        private WinFormsPanel pnlRange = null!;
        private WinFormsLabel lblStartStake = null!;
        private WinFormsComboBox cmbStartStake = null!;
        private WinFormsButton btnPickStartStake = null!;
        private WinFormsLabel lblEndStake = null!;
        private WinFormsComboBox cmbEndStake = null!;
        private WinFormsButton btnPickEndStake = null!;
        private WinFormsLabel lblStartStation = null!;
        private WinFormsNumericUpDown numStartStation = null!;
        private WinFormsLabel lblEndStation = null!;
        private WinFormsNumericUpDown numEndStation = null!;
        private WinFormsCheckBox chkIncludeBoundary = null!;

        // Group 3: Xem trước & Danh sách cọc
        private WinFormsGroupBox grpPreview = null!;
        private WinFormsDataGridView dgvPreview = null!;
        private WinFormsButton btnSelectAll = null!;
        private WinFormsButton btnDeselectAll = null!;
        private WinFormsButton btnInvertSelection = null!;
        private WinFormsButton btnApplyRange = null!;
        private WinFormsLabel lblSummary = null!;

        // Group 4: Thiết lập Profile kết quả
        private WinFormsGroupBox grpTarget = null!;
        private WinFormsLabel lblTargetName = null!;
        private WinFormsTextBox txtTargetProfileName = null!;
        private WinFormsRadioButton radTargetNew = null!;
        private WinFormsRadioButton radTargetUpdate = null!;
        private WinFormsLabel lblProfileStyle = null!;
        private WinFormsComboBox cmbProfileStyle = null!;
        private WinFormsLabel lblLabelSetStyle = null!;
        private WinFormsComboBox cmbLabelSetStyle = null!;
        private WinFormsCheckBox chkChangeSourceStyle = null!;

        // Action Buttons & Status
        private WinFormsLabel lblStatus = null!;
        private WinFormsButton btnExecute = null!;
        private WinFormsButton btnClose = null!;

        #endregion

        public FixDuongTuNhienTheoCocForm(ObjectId initProfileId = default, ObjectId initAlignmentId = default)
        {
            InitializeComponent();

            if (!initProfileId.IsNull && initProfileId.IsValid)
            {
                _currentProfileId = initProfileId;
            }
            else if (!_lastProfileId.IsNull && _lastProfileId.IsValid && !_lastProfileId.IsErased)
            {
                _currentProfileId = _lastProfileId;
            }

            if (!initAlignmentId.IsNull && initAlignmentId.IsValid)
            {
                _currentAlignmentId = initAlignmentId;
            }
            else if (!_lastAlignmentId.IsNull && _lastAlignmentId.IsValid && !_lastAlignmentId.IsErased)
            {
                _currentAlignmentId = _lastAlignmentId;
            }

            RestoreLastSettings();
            LoadStyles();
            LoadAlignments();

            this.FormClosing += (s, e) => SaveCurrentSettings();
        }

        #region Khởi tạo Giao diện (UI)

        private void InitializeComponent()
        {
            var regularFont = new DrawingFont("Segoe UI", 9.5F, FontStyle.Regular);
            var boldFont = new DrawingFont("Segoe UI", 9.5F, FontStyle.Bold);
            var headerFont = new DrawingFont("Segoe UI", 12.5F, FontStyle.Bold);
            var smallFont = new DrawingFont("Segoe UI", 8.5F, FontStyle.Regular);

            this.SuspendLayout();

            // Form Properties
            this.Text = "Sửa Đường Tự Nhiên Theo Cọc (CTP_Fix_DuongTuNhien_TheoCoc)";
            this.Font = regularFont;
            this.Size = _lastFormSize;
            this.MinimumSize = new Size(920, 680);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = DrawingColor.FromArgb(248, 249, 251);

            // 1. Banner
            lblTitle = new WinFormsLabel
            {
                Text = "SỬA ĐƯỜNG TỰ NHIÊN THEO CỌC (FIX PROFILE)",
                Font = headerFont,
                ForeColor = DrawingColor.FromArgb(16, 76, 140),
                Location = new WinFormsPoint(18, 10),
                AutoSize = true
            };

            lblSubtitle = new WinFormsLabel
            {
                Text = "Tạo Profile thiết kế số hóa qua các cọc (Sample Lines) và cho phép tùy chọn phạm vi đoạn cần fix.",
                Font = smallFont,
                ForeColor = DrawingColor.FromArgb(100, 110, 120),
                Location = new WinFormsPoint(19, 34),
                AutoSize = true
            };

            // 2. Group 1: Tuyến & Đường tự nhiên gốc
            grpSource = new WinFormsGroupBox
            {
                Text = " 1. Tuyến thiết kế & Đường tự nhiên nguồn ",
                Font = boldFont,
                ForeColor = DrawingColor.FromArgb(30, 41, 59),
                Location = new WinFormsPoint(18, 56),
                Size = new Size(930, 95),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            lblAlignment = new WinFormsLabel
            {
                Text = "Tim tuyến:",
                Font = regularFont,
                Location = new WinFormsPoint(12, 26),
                Size = new Size(72, 23)
            };

            cmbAlignment = new WinFormsComboBox
            {
                Font = regularFont,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new WinFormsPoint(85, 23),
                Size = new Size(210, 25)
            };
            cmbAlignment.SelectedIndexChanged += CmbAlignment_SelectedIndexChanged;

            btnPickAlignment = new WinFormsButton
            {
                Text = "🎯 Pick tim...",
                Font = regularFont,
                Location = new WinFormsPoint(300, 22),
                Size = new Size(90, 26),
                BackColor = DrawingColor.White,
                FlatStyle = FlatStyle.System
            };
            btnPickAlignment.Click += BtnPickAlignment_Click;

            lblProfile = new WinFormsLabel
            {
                Text = "Đường TN:",
                Font = regularFont,
                Location = new WinFormsPoint(405, 26),
                Size = new Size(72, 23)
            };

            cmbProfile = new WinFormsComboBox
            {
                Font = regularFont,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new WinFormsPoint(480, 23),
                Size = new Size(210, 25)
            };
            cmbProfile.SelectedIndexChanged += CmbProfile_SelectedIndexChanged;

            btnPickProfile = new WinFormsButton
            {
                Text = "🎯 Pick TN...",
                Font = regularFont,
                Location = new WinFormsPoint(695, 22),
                Size = new Size(90, 26),
                BackColor = DrawingColor.White,
                FlatStyle = FlatStyle.System
            };
            btnPickProfile.Click += BtnPickProfile_Click;

            lblGroup = new WinFormsLabel
            {
                Text = "Nhóm cọc:",
                Font = regularFont,
                Location = new WinFormsPoint(12, 60),
                Size = new Size(72, 23)
            };

            cmbGroup = new WinFormsComboBox
            {
                Font = regularFont,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new WinFormsPoint(85, 57),
                Size = new Size(210, 25)
            };
            cmbGroup.SelectedIndexChanged += CmbGroup_SelectedIndexChanged;

            lblSourceInfo = new WinFormsLabel
            {
                Text = "Chưa chọn tim tuyến và đường tự nhiên.",
                Font = smallFont,
                ForeColor = DrawingColor.FromArgb(70, 80, 95),
                Location = new WinFormsPoint(305, 61),
                Size = new Size(610, 22),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            grpSource.Controls.AddRange(new Control[] {
                lblAlignment, cmbAlignment, btnPickAlignment,
                lblProfile, cmbProfile, btnPickProfile,
                lblGroup, cmbGroup, lblSourceInfo
            });

            // 3. Group 2: Phạm vi đoạn cần fix
            grpRange = new WinFormsGroupBox
            {
                Text = " 2. Lựa chọn phạm vi đoạn cần fix ",
                Font = boldFont,
                ForeColor = DrawingColor.FromArgb(30, 41, 59),
                Location = new WinFormsPoint(18, 156),
                Size = new Size(930, 100),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            radAll = new WinFormsRadioButton
            {
                Text = "Toàn bộ tuyến (Tất cả cọc trong phạm vi TN)",
                Font = regularFont,
                Location = new WinFormsPoint(15, 22),
                Size = new Size(320, 24),
                Checked = true
            };
            radAll.CheckedChanged += RadRangeMode_CheckedChanged;

            radRange = new WinFormsRadioButton
            {
                Text = "Theo đoạn cọc / lý trình lựa chọn:",
                Font = regularFont,
                Location = new WinFormsPoint(350, 22),
                Size = new Size(240, 24)
            };
            radRange.CheckedChanged += RadRangeMode_CheckedChanged;

            pnlRange = new WinFormsPanel
            {
                Location = new WinFormsPoint(10, 48),
                Size = new Size(910, 45),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Enabled = false
            };

            lblStartStake = new WinFormsLabel
            {
                Text = "Từ cọc:",
                Font = regularFont,
                Location = new WinFormsPoint(5, 12),
                Size = new Size(52, 23)
            };

            cmbStartStake = new WinFormsComboBox
            {
                Font = regularFont,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new WinFormsPoint(60, 9),
                Size = new Size(165, 25)
            };
            cmbStartStake.SelectedIndexChanged += CmbStartStake_SelectedIndexChanged;

            btnPickStartStake = new WinFormsButton
            {
                Text = "🎯",
                Font = regularFont,
                Location = new WinFormsPoint(228, 8),
                Size = new Size(34, 26),
                BackColor = DrawingColor.White,
                FlatStyle = FlatStyle.System
            };
            btnPickStartStake.Click += (s, e) => PickStakePoint(true);

            lblEndStake = new WinFormsLabel
            {
                Text = "Đến cọc:",
                Font = regularFont,
                Location = new WinFormsPoint(275, 12),
                Size = new Size(60, 23)
            };

            cmbEndStake = new WinFormsComboBox
            {
                Font = regularFont,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new WinFormsPoint(338, 9),
                Size = new Size(165, 25)
            };
            cmbEndStake.SelectedIndexChanged += CmbEndStake_SelectedIndexChanged;

            btnPickEndStake = new WinFormsButton
            {
                Text = "🎯",
                Font = regularFont,
                Location = new WinFormsPoint(506, 8),
                Size = new Size(34, 26),
                BackColor = DrawingColor.White,
                FlatStyle = FlatStyle.System
            };
            btnPickEndStake.Click += (s, e) => PickStakePoint(false);

            lblStartStation = new WinFormsLabel
            {
                Text = "Lý trình:",
                Font = smallFont,
                Location = new WinFormsPoint(550, 13),
                Size = new Size(48, 20)
            };

            numStartStation = new WinFormsNumericUpDown
            {
                Font = regularFont,
                DecimalPlaces = 2,
                Minimum = -10000000m,
                Maximum = 10000000m,
                Location = new WinFormsPoint(600, 9),
                Size = new Size(80, 25)
            };
            numStartStation.ValueChanged += NumStation_ValueChanged;

            lblEndStation = new WinFormsLabel
            {
                Text = "➜",
                Font = boldFont,
                Location = new WinFormsPoint(685, 12),
                Size = new Size(20, 20)
            };

            numEndStation = new WinFormsNumericUpDown
            {
                Font = regularFont,
                DecimalPlaces = 2,
                Minimum = -10000000m,
                Maximum = 10000000m,
                Location = new WinFormsPoint(708, 9),
                Size = new Size(80, 25)
            };
            numEndStation.ValueChanged += NumStation_ValueChanged;

            chkIncludeBoundary = new WinFormsCheckBox
            {
                Text = "Nối thêm PVI 2 đầu",
                Font = smallFont,
                Location = new WinFormsPoint(795, 11),
                Size = new Size(120, 22),
                Checked = true
            };

            pnlRange.Controls.AddRange(new Control[] {
                lblStartStake, cmbStartStake, btnPickStartStake,
                lblEndStake, cmbEndStake, btnPickEndStake,
                lblStartStation, numStartStation, lblEndStation, numEndStation,
                chkIncludeBoundary
            });

            grpRange.Controls.AddRange(new Control[] {
                radAll, radRange, pnlRange
            });

            // 4. Group 3: Xem trước & Danh sách cọc
            grpPreview = new WinFormsGroupBox
            {
                Text = " 3. Danh sách cọc trong đoạn lựa chọn ",
                Font = boldFont,
                ForeColor = DrawingColor.FromArgb(30, 41, 59),
                Location = new WinFormsPoint(18, 262),
                Size = new Size(930, 260),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            btnSelectAll = new WinFormsButton
            {
                Text = "☑ Chọn tất cả",
                Font = regularFont,
                Location = new WinFormsPoint(12, 23),
                Size = new Size(105, 27),
                BackColor = DrawingColor.White,
                FlatStyle = FlatStyle.System
            };
            btnSelectAll.Click += (s, e) => SetAllStakesSelection(true);

            btnDeselectAll = new WinFormsButton
            {
                Text = "☐ Bỏ chọn hết",
                Font = regularFont,
                Location = new WinFormsPoint(122, 23),
                Size = new Size(105, 27),
                BackColor = DrawingColor.White,
                FlatStyle = FlatStyle.System
            };
            btnDeselectAll.Click += (s, e) => SetAllStakesSelection(false);

            btnInvertSelection = new WinFormsButton
            {
                Text = "⇄ Đảo chọn",
                Font = regularFont,
                Location = new WinFormsPoint(232, 23),
                Size = new Size(95, 27),
                BackColor = DrawingColor.White,
                FlatStyle = FlatStyle.System
            };
            btnInvertSelection.Click += (s, e) => InvertStakesSelection();

            btnApplyRange = new WinFormsButton
            {
                Text = "⚡ Áp dụng đoạn lọc",
                Font = regularFont,
                Location = new WinFormsPoint(332, 23),
                Size = new Size(130, 27),
                BackColor = DrawingColor.White,
                FlatStyle = FlatStyle.System
            };
            btnApplyRange.Click += (s, e) => ApplyRangeFilter();

            lblSummary = new WinFormsLabel
            {
                Text = "Chưa có cọc nào được chọn.",
                Font = boldFont,
                ForeColor = DrawingColor.FromArgb(16, 76, 140),
                Location = new WinFormsPoint(475, 27),
                Size = new Size(445, 22),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                TextAlign = ContentAlignment.MiddleRight
            };

            dgvPreview = new WinFormsDataGridView
            {
                Location = new WinFormsPoint(12, 56),
                Size = new Size(905, 192),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            SetupDataGridViewColumns();

            grpPreview.Controls.AddRange(new Control[] {
                btnSelectAll, btnDeselectAll, btnInvertSelection, btnApplyRange,
                lblSummary, dgvPreview
            });

            // 5. Group 4: Thiết lập Profile kết quả
            grpTarget = new WinFormsGroupBox
            {
                Text = " 4. Thiết lập Profile kết quả (Fix Profile) ",
                Font = boldFont,
                ForeColor = DrawingColor.FromArgb(30, 41, 59),
                Location = new WinFormsPoint(18, 528),
                Size = new Size(930, 115),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            lblTargetName = new WinFormsLabel
            {
                Text = "Tên Profile đích:",
                Font = regularFont,
                Location = new WinFormsPoint(12, 25),
                Size = new Size(105, 23)
            };

            txtTargetProfileName = new WinFormsTextBox
            {
                Font = regularFont,
                Location = new WinFormsPoint(120, 22),
                Size = new Size(220, 25)
            };

            radTargetNew = new WinFormsRadioButton
            {
                Text = "Tạo mới (tự tăng hậu tố nếu trùng)",
                Font = regularFont,
                Location = new WinFormsPoint(355, 23),
                Size = new Size(245, 23),
                Checked = true
            };

            radTargetUpdate = new WinFormsRadioButton
            {
                Text = "Cập nhật / Ghi đè vào Profile đã có",
                Font = regularFont,
                Location = new WinFormsPoint(605, 23),
                Size = new Size(250, 23)
            };

            lblProfileStyle = new WinFormsLabel
            {
                Text = "Style Profile:",
                Font = regularFont,
                Location = new WinFormsPoint(12, 57),
                Size = new Size(105, 23)
            };

            cmbProfileStyle = new WinFormsComboBox
            {
                Font = regularFont,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new WinFormsPoint(120, 54),
                Size = new Size(220, 25)
            };

            lblLabelSetStyle = new WinFormsLabel
            {
                Text = "Style nhãn:",
                Font = regularFont,
                Location = new WinFormsPoint(355, 57),
                Size = new Size(80, 23)
            };

            cmbLabelSetStyle = new WinFormsComboBox
            {
                Font = regularFont,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new WinFormsPoint(440, 54),
                Size = new Size(220, 25)
            };

            chkChangeSourceStyle = new WinFormsCheckBox
            {
                Text = "Đổi style đường TN gốc thành 'Existing Ground Profile (KHONG IN)'",
                Font = regularFont,
                Location = new WinFormsPoint(120, 85),
                Size = new Size(540, 24),
                Checked = true
            };

            grpTarget.Controls.AddRange(new Control[] {
                lblTargetName, txtTargetProfileName, radTargetNew, radTargetUpdate,
                lblProfileStyle, cmbProfileStyle, lblLabelSetStyle, cmbLabelSetStyle,
                chkChangeSourceStyle
            });

            // 6. Action Buttons & Status
            lblStatus = new WinFormsLabel
            {
                Text = "Sẵn sàng thực hiện.",
                Font = regularFont,
                ForeColor = DrawingColor.FromArgb(50, 60, 75),
                Location = new WinFormsPoint(20, 658),
                Size = new Size(670, 30),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                TextAlign = ContentAlignment.MiddleLeft
            };

            btnExecute = new WinFormsButton
            {
                Text = "🚀 Thực Hiện Fix",
                Font = boldFont,
                Location = new WinFormsPoint(700, 654),
                Size = new Size(140, 36),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                BackColor = DrawingColor.FromArgb(16, 76, 140),
                ForeColor = DrawingColor.White,
                FlatStyle = FlatStyle.Flat
            };
            btnExecute.FlatAppearance.BorderSize = 0;
            btnExecute.Click += BtnExecute_Click;

            btnClose = new WinFormsButton
            {
                Text = "Đóng",
                Font = regularFont,
                Location = new WinFormsPoint(848, 654),
                Size = new Size(100, 36),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                BackColor = DrawingColor.White,
                FlatStyle = FlatStyle.System
            };
            btnClose.Click += (s, e) => this.Close();

            // Form assembly
            this.Controls.AddRange(new Control[] {
                lblTitle, lblSubtitle,
                grpSource, grpRange, grpPreview, grpTarget,
                lblStatus, btnExecute, btnClose
            });

            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private void SetupDataGridViewColumns()
        {
            dgvPreview.AllowUserToAddRows = false;
            dgvPreview.AllowUserToDeleteRows = false;
            dgvPreview.AllowUserToResizeRows = false;
            dgvPreview.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvPreview.BackgroundColor = DrawingColor.White;
            dgvPreview.BorderStyle = BorderStyle.Fixed3D;
            dgvPreview.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            dgvPreview.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            dgvPreview.ColumnHeadersDefaultCellStyle.BackColor = DrawingColor.FromArgb(241, 245, 249);
            dgvPreview.ColumnHeadersDefaultCellStyle.ForeColor = DrawingColor.FromArgb(51, 65, 85);
            dgvPreview.ColumnHeadersDefaultCellStyle.Font = new DrawingFont("Segoe UI", 9F, FontStyle.Bold);
            dgvPreview.ColumnHeadersHeight = 30;
            dgvPreview.EnableHeadersVisualStyles = false;
            dgvPreview.GridColor = DrawingColor.FromArgb(226, 232, 240);
            dgvPreview.RowHeadersVisible = false;
            dgvPreview.RowTemplate.Height = 25;
            dgvPreview.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

            var colCheck = new DataGridViewCheckBoxColumn
            {
                HeaderText = "Chọn",
                DataPropertyName = "IsSelected",
                Width = 55,
                FillWeight = 30,
                Resizable = DataGridViewTriState.False
            };

            var colStt = new DataGridViewTextBoxColumn
            {
                HeaderText = "STT",
                DataPropertyName = "Stt",
                Width = 50,
                FillWeight = 25,
                ReadOnly = true
            };

            var colName = new DataGridViewTextBoxColumn
            {
                HeaderText = "Tên cọc",
                DataPropertyName = "Name",
                Width = 140,
                FillWeight = 75,
                ReadOnly = true
            };

            var colStation = new DataGridViewTextBoxColumn
            {
                HeaderText = "Lý trình (m)",
                DataPropertyName = "Station",
                Width = 110,
                FillWeight = 60,
                ReadOnly = true
            };

            var colKm = new DataGridViewTextBoxColumn
            {
                HeaderText = "Định dạng Km",
                DataPropertyName = "FormattedStation",
                Width = 140,
                FillWeight = 75,
                ReadOnly = true
            };

            var colElev = new DataGridViewTextBoxColumn
            {
                HeaderText = "Cao độ TN (m)",
                DataPropertyName = "Elevation",
                Width = 120,
                FillWeight = 65,
                ReadOnly = true
            };

            var colStatus = new DataGridViewTextBoxColumn
            {
                HeaderText = "Trạng thái",
                DataPropertyName = "Status",
                Width = 160,
                FillWeight = 85,
                ReadOnly = true
            };

            dgvPreview.Columns.AddRange(new DataGridViewColumn[] {
                colCheck, colStt, colName, colStation, colKm, colElev, colStatus
            });

            dgvPreview.CellContentClick += (s, e) => {
                if (e.RowIndex >= 0 && e.ColumnIndex == 0)
                {
                    dgvPreview.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };

            dgvPreview.CellValueChanged += (s, e) => {
                if (!_isUpdatingUI && e.RowIndex >= 0 && e.ColumnIndex == 0)
                {
                    if (dgvPreview.Rows[e.RowIndex].Tag is FixStakeItemModel item)
                    {
                        item.IsSelected = Convert.ToBoolean(dgvPreview.Rows[e.RowIndex].Cells[0].Value);
                    }
                    UpdateSummaryText();
                }
            };
        }

        #endregion

        #region Nạp Dữ Liệu & Khôi phục Cài Đặt (Restore & Populate)

        private void RestoreLastSettings()
        {
            radRange.Checked = _lastIsRangeMode;
            radAll.Checked = !_lastIsRangeMode;
            pnlRange.Enabled = _lastIsRangeMode;
            chkIncludeBoundary.Checked = _lastIncludeBoundary;
            radTargetUpdate.Checked = _lastIsUpdateExisting;
            radTargetNew.Checked = !_lastIsUpdateExisting;
            chkChangeSourceStyle.Checked = _lastChangeSourceStyle;
        }

        private void SaveCurrentSettings()
        {
            _lastAlignmentId = _currentAlignmentId;
            _lastProfileId = _currentProfileId;
            _lastGroupId = _currentGroupId;
            _lastIsRangeMode = radRange.Checked;
            _lastStartStation = numStartStation.Value;
            _lastEndStation = numEndStation.Value;
            _lastIncludeBoundary = chkIncludeBoundary.Checked;
            _lastTargetProfileName = txtTargetProfileName.Text.Trim();
            _lastIsUpdateExisting = radTargetUpdate.Checked;
            _lastProfileStyleName = cmbProfileStyle.SelectedItem?.ToString() ?? _lastProfileStyleName;
            _lastLabelSetStyleName = cmbLabelSetStyle.SelectedItem?.ToString() ?? _lastLabelSetStyleName;
            _lastChangeSourceStyle = chkChangeSourceStyle.Checked;
            _lastFormSize = this.Size;
        }

        private void LoadStyles()
        {
            cmbProfileStyle.Items.Clear();
            cmbLabelSetStyle.Items.Clear();

            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var cdoc = CivilApplication.ActiveDocument;
            if (cdoc == null) return;

            int selectedStyleIdx = -1;
            int i = 0;
            foreach (ObjectId styleId in cdoc.Styles.ProfileStyles)
            {
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    var st = tr.GetObject(styleId, OpenMode.ForRead) as ProfileStyle;
                    if (st != null)
                    {
                        cmbProfileStyle.Items.Add(new EntityComboItem(st.Name, styleId));
                        if (string.Equals(st.Name, _lastProfileStyleName, StringComparison.OrdinalIgnoreCase) ||
                            (selectedStyleIdx == -1 && string.Equals(st.Name, "0.TN", StringComparison.OrdinalIgnoreCase)))
                        {
                            selectedStyleIdx = i;
                        }
                        i++;
                    }
                    tr.Commit();
                }
            }

            if (selectedStyleIdx >= 0)
                cmbProfileStyle.SelectedIndex = selectedStyleIdx;
            else if (cmbProfileStyle.Items.Count > 0)
                cmbProfileStyle.SelectedIndex = 0;

            int selectedLabelIdx = -1;
            i = 0;
            foreach (ObjectId labelId in cdoc.Styles.LabelSetStyles.ProfileLabelSetStyles)
            {
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    var lst = tr.GetObject(labelId, OpenMode.ForRead) as ProfileLabelSetStyle;
                    if (lst != null)
                    {
                        cmbLabelSetStyle.Items.Add(new EntityComboItem(lst.Name, labelId));
                        if (string.Equals(lst.Name, _lastLabelSetStyleName, StringComparison.OrdinalIgnoreCase) ||
                            (selectedLabelIdx == -1 && string.Equals(lst.Name, "_No Labels", StringComparison.OrdinalIgnoreCase)))
                        {
                            selectedLabelIdx = i;
                        }
                        i++;
                    }
                    tr.Commit();
                }
            }

            if (selectedLabelIdx >= 0)
                cmbLabelSetStyle.SelectedIndex = selectedLabelIdx;
            else if (cmbLabelSetStyle.Items.Count > 0)
                cmbLabelSetStyle.SelectedIndex = 0;
        }

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
                            cmbAlignment.Items.Add(new EntityComboItem(al.Name, alId));
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
            if (cmbAlignment.SelectedItem is EntityComboItem item)
            {
                _currentAlignmentId = item.Id;
                LoadProfilesForAlignment(_currentAlignmentId);
                LoadGroupsForAlignment(_currentAlignmentId);
            }
            else
            {
                _currentAlignmentId = ObjectId.Null;
                cmbProfile.Items.Clear();
                cmbGroup.Items.Clear();
                _stakes.Clear();
                RefreshGrid();
            }
        }

        private void LoadProfilesForAlignment(ObjectId alignmentId)
        {
            _isUpdatingUI = true;
            cmbProfile.Items.Clear();

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
                    int selectedIdx = -1;
                    int i = 0;
                    foreach (ObjectId pId in al.GetProfileIds())
                    {
                        var p = tr.GetObject(pId, OpenMode.ForRead) as Profile;
                        if (p != null)
                        {
                            cmbProfile.Items.Add(new EntityComboItem(p.Name, pId));
                            if (pId == _currentProfileId)
                            {
                                selectedIdx = i;
                            }
                            i++;
                        }
                    }

                    if (selectedIdx >= 0)
                    {
                        cmbProfile.SelectedIndex = selectedIdx;
                    }
                    else if (cmbProfile.Items.Count > 0)
                    {
                        cmbProfile.SelectedIndex = 0;
                    }
                }
                tr.Commit();
            }

            _isUpdatingUI = false;
            OnProfileChanged();
        }

        private void CmbProfile_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_isUpdatingUI) return;
            OnProfileChanged();
        }

        private void OnProfileChanged()
        {
            if (cmbProfile.SelectedItem is EntityComboItem item)
            {
                _currentProfileId = item.Id;
                txtTargetProfileName.Text = "Fix-" + item.Name;
                LoadStakesData();
            }
            else
            {
                _currentProfileId = ObjectId.Null;
                _stakes.Clear();
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
                    ObjectIdCollection grpIds = al.GetSampleLineGroupIds();
                    int selectedIdx = -1;
                    int i = 0;
                    foreach (ObjectId gId in grpIds)
                    {
                        var grp = tr.GetObject(gId, OpenMode.ForRead) as SampleLineGroup;
                        if (grp != null)
                        {
                            cmbGroup.Items.Add(new EntityComboItem(grp.Name, gId));
                            if (gId == _currentGroupId)
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
            OnGroupChanged();
        }

        private void CmbGroup_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_isUpdatingUI) return;
            OnGroupChanged();
        }

        private void OnGroupChanged()
        {
            if (cmbGroup.SelectedItem is EntityComboItem item)
            {
                _currentGroupId = item.Id;
            }
            else
            {
                _currentGroupId = ObjectId.Null;
            }
            LoadStakesData();
        }

        #endregion

        #region Tải Dữ Liệu Cọc & Tính Cao Độ Tự Nhiên

        private void LoadStakesData()
        {
            _stakes.Clear();

            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null || _currentAlignmentId.IsNull || _currentGroupId.IsNull || _currentProfileId.IsNull)
            {
                lblSourceInfo.Text = "Chưa đủ thông tin để nạp danh sách cọc.";
                RefreshGrid();
                PopulateStakeDropdowns();
                return;
            }

            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var al = tr.GetObject(_currentAlignmentId, OpenMode.ForRead) as Alignment;
                var prof = tr.GetObject(_currentProfileId, OpenMode.ForRead) as Profile;
                var grp = tr.GetObject(_currentGroupId, OpenMode.ForRead) as SampleLineGroup;

                if (al != null && prof != null && grp != null)
                {
                    double profStart = prof.StartingStation;
                    double profEnd = prof.EndingStation;

                    lblSourceInfo.Text = $"Phạm vi đường TN: {FormatStation(profStart)} ➜ {FormatStation(profEnd)} ({profStart:F2}m - {profEnd:F2}m)";

                    ObjectIdCollection slIds = grp.GetSampleLineIds();
                    var rawStakes = new List<(ObjectId id, string name, double station)>();

                    foreach (ObjectId slId in slIds)
                    {
                        var sl = tr.GetObject(slId, OpenMode.ForRead) as SampleLine;
                        if (sl != null)
                        {
                            rawStakes.Add((slId, sl.Name, Math.Round(sl.Station, 2)));
                        }
                    }

                    // Sắp xếp cọc theo lý trình
                    rawStakes = rawStakes.OrderBy(s => s.station).ToList();

                    int stt = 1;
                    foreach (var raw in rawStakes)
                    {
                        var item = new FixStakeItemModel
                        {
                            SampleLineId = raw.id,
                            Stt = stt++,
                            Name = raw.name,
                            Station = raw.station,
                            FormattedStation = FormatStation(raw.station)
                        };

                        if (raw.station < profStart || raw.station > profEnd)
                        {
                            item.HasElevation = false;
                            item.IsSelected = false;
                            item.Status = "Ngoài phạm vi TN";
                        }
                        else
                        {
                            try
                            {
                                item.Elevation = prof.ElevationAt(raw.station);
                                item.HasElevation = true;
                                item.IsSelected = true;
                                item.Status = "Hợp lệ";
                            }
                            catch
                            {
                                item.HasElevation = false;
                                item.IsSelected = false;
                                item.Status = "Không lấy được cao độ";
                            }
                        }

                        _stakes.Add(item);
                    }
                }

                tr.Commit();
            }

            PopulateStakeDropdowns();
            ApplyRangeFilter();
        }

        private void PopulateStakeDropdowns()
        {
            _isUpdatingUI = true;
            cmbStartStake.Items.Clear();
            cmbEndStake.Items.Clear();

            var validStakes = _stakes.Where(s => s.HasElevation).ToList();

            foreach (var st in validStakes)
            {
                var cbItem = new EntityComboItem($"{st.Name} ({st.FormattedStation})", st.SampleLineId, st.Station);
                cmbStartStake.Items.Add(cbItem);
                cmbEndStake.Items.Add(cbItem);
            }

            if (validStakes.Count > 0)
            {
                cmbStartStake.SelectedIndex = 0;
                cmbEndStake.SelectedIndex = cmbEndStake.Items.Count - 1;

                numStartStation.Value = (decimal)validStakes.First().Station;
                numEndStation.Value = (decimal)validStakes.Last().Station;
            }

            _isUpdatingUI = false;
        }

        #endregion

        #region Xử lý Phạm vi & Bộ lọc (Range Filtering)

        private void RadRangeMode_CheckedChanged(object? sender, EventArgs e)
        {
            pnlRange.Enabled = radRange.Checked;
            ApplyRangeFilter();
        }

        private void CmbStartStake_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_isUpdatingUI) return;
            if (cmbStartStake.SelectedItem is EntityComboItem item)
            {
                _isUpdatingUI = true;
                numStartStation.Value = (decimal)item.TagValue;
                _isUpdatingUI = false;
                ApplyRangeFilter();
            }
        }

        private void CmbEndStake_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_isUpdatingUI) return;
            if (cmbEndStake.SelectedItem is EntityComboItem item)
            {
                _isUpdatingUI = true;
                numEndStation.Value = (decimal)item.TagValue;
                _isUpdatingUI = false;
                ApplyRangeFilter();
            }
        }

        private void NumStation_ValueChanged(object? sender, EventArgs e)
        {
            if (_isUpdatingUI) return;
            ApplyRangeFilter();
        }

        private void ApplyRangeFilter()
        {
            if (_stakes.Count == 0)
            {
                RefreshGrid();
                return;
            }

            if (radAll.Checked)
            {
                foreach (var st in _stakes)
                {
                    st.IsSelected = st.HasElevation;
                }
            }
            else
            {
                double minSt = Math.Min((double)numStartStation.Value, (double)numEndStation.Value);
                double maxSt = Math.Max((double)numStartStation.Value, (double)numEndStation.Value);

                foreach (var st in _stakes)
                {
                    if (st.HasElevation)
                    {
                        st.IsSelected = (st.Station >= minSt - 0.01 && st.Station <= maxSt + 0.01);
                    }
                    else
                    {
                        st.IsSelected = false;
                    }
                }
            }

            RefreshGrid();
        }

        private void SetAllStakesSelection(bool selectAll)
        {
            foreach (var st in _stakes)
            {
                st.IsSelected = selectAll && st.HasElevation;
            }
            RefreshGrid();
        }

        private void InvertStakesSelection()
        {
            foreach (var st in _stakes)
            {
                if (st.HasElevation)
                {
                    st.IsSelected = !st.IsSelected;
                }
            }
            RefreshGrid();
        }

        private void RefreshGrid()
        {
            _isUpdatingUI = true;
            dgvPreview.Rows.Clear();

            foreach (var item in _stakes)
            {
                int rowIdx = dgvPreview.Rows.Add(
                    item.IsSelected,
                    item.Stt,
                    item.Name,
                    item.Station.ToString("F2"),
                    item.FormattedStation,
                    item.HasElevation ? item.Elevation.ToString("F3") : "--",
                    item.Status
                );

                var row = dgvPreview.Rows[rowIdx];
                row.Tag = item;

                if (!item.HasElevation)
                {
                    row.DefaultCellStyle.BackColor = DrawingColor.FromArgb(254, 242, 242);
                    row.DefaultCellStyle.ForeColor = DrawingColor.FromArgb(185, 28, 28);
                }
            }

            _isUpdatingUI = false;
            UpdateSummaryText();
        }

        private void UpdateSummaryText()
        {
            var selected = _stakes.Where(s => s.IsSelected && s.HasElevation).ToList();
            int total = _stakes.Count(s => s.HasElevation);

            if (selected.Count == 0)
            {
                lblSummary.Text = $"Đã chọn: 0 / {total} cọc.";
                lblSummary.ForeColor = DrawingColor.FromArgb(185, 28, 28);
            }
            else
            {
                double minStation = selected.Min(s => s.Station);
                double maxStation = selected.Max(s => s.Station);
                lblSummary.Text = $"Đã chọn: {selected.Count} / {total} cọc (Từ {FormatStation(minStation)} đến {FormatStation(maxStation)})";
                lblSummary.ForeColor = DrawingColor.FromArgb(16, 76, 140);
            }
        }

        #endregion

        #region Tương Tác Canvas Pick (StartUserInteraction)

        private void BtnPickAlignment_Click(object? sender, EventArgs e)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            ObjectId pickedId = ObjectId.Null;
            using (var interaction = ed.StartUserInteraction(this))
            {
                var peo = new PromptEntityOptions("\nChọn tim tuyến (Alignment) trên bản vẽ: ");
                peo.SetRejectMessage("\nĐối tượng phải là Alignment!");
                peo.AddAllowedClass(typeof(Alignment), true);
                var res = ed.GetEntity(peo);
                interaction.End();

                if (res.Status == PromptStatus.OK)
                {
                    pickedId = res.ObjectId;
                }
            }

            if (!pickedId.IsNull)
            {
                SelectAlignmentInCombo(pickedId);
            }
        }

        private void BtnPickProfile_Click(object? sender, EventArgs e)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            ObjectId pickedProfileId = ObjectId.Null;
            ObjectId alignId = ObjectId.Null;

            using (var interaction = ed.StartUserInteraction(this))
            {
                var peo = new PromptEntityOptions("\nChọn đường tự nhiên (Profile) trên trắc dọc: ");
                peo.SetRejectMessage("\nĐối tượng phải là Profile!");
                peo.AddAllowedClass(typeof(Profile), true);
                var res = ed.GetEntity(peo);
                interaction.End();

                if (res.Status == PromptStatus.OK)
                {
                    pickedProfileId = res.ObjectId;
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        var prof = tr.GetObject(pickedProfileId, OpenMode.ForRead) as Profile;
                        if (prof != null)
                        {
                            alignId = prof.AlignmentId;
                        }
                        tr.Commit();
                    }
                }
            }

            if (!pickedProfileId.IsNull && !alignId.IsNull)
            {
                SelectAlignmentInCombo(alignId);
                SelectProfileInCombo(pickedProfileId);
            }
        }

        private void PickStakePoint(bool isStartStake)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null || _currentAlignmentId.IsNull)
            {
                MessageBox.Show("Vui lòng chọn tim tuyến trước khi pick điểm!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var ed = doc.Editor;

            double? pickedStation = null;

            using (var interaction = ed.StartUserInteraction(this))
            {
                var pOpt = new PromptPointOptions(isStartStake ? "\nPick điểm/cọc bắt đầu trên trắc dọc hoặc bình đồ: " : "\nPick điểm/cọc kết thúc trên trắc dọc hoặc bình đồ: ");
                pOpt.AllowNone = true;
                var pRes = ed.GetPoint(pOpt);
                interaction.End();

                if (pRes.Status == PromptStatus.OK)
                {
                    Point3d pt = pRes.Value;
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        var al = tr.GetObject(_currentAlignmentId, OpenMode.ForRead) as Alignment;
                        if (al != null)
                        {
                            // Thử kiểm tra xem điểm có nằm trong ProfileView nào của Alignment không
                            bool foundInPv = false;
                            foreach (ObjectId pvId in al.GetProfileViewIds())
                            {
                                var pv = tr.GetObject(pvId, OpenMode.ForRead) as ProfileView;
                                if (pv != null)
                                {
                                    try
                                    {
                                        double st = 0, el = 0;
                                        pv.FindStationAndElevationAtXY(pt.X, pt.Y, ref st, ref el);
                                        if (st >= al.StartingStation - 1.0 && st <= al.EndingStation + 1.0)
                                        {
                                            pickedStation = st;
                                            foundInPv = true;
                                            break;
                                        }
                                    }
                                    catch { }
                                }
                            }

                            // Nếu không nằm trong ProfileView, coi như pick trên bình đồ
                            if (!foundInPv)
                            {
                                try
                                {
                                    double st = 0, off = 0;
                                    al.StationOffset(pt.X, pt.Y, ref st, ref off);
                                    pickedStation = st;
                                }
                                catch { }
                            }
                        }
                        tr.Commit();
                    }
                }
            }

            if (pickedStation.HasValue)
            {
                ApplyPickedStationToDropdown(pickedStation.Value, isStartStake);
            }
        }

        private void ApplyPickedStationToDropdown(double station, bool isStartStake)
        {
            if (_stakes.Count == 0) return;

            // Tìm cọc gần nhất với lý trình đã pick
            var nearest = _stakes.Where(s => s.HasElevation)
                                 .OrderBy(s => Math.Abs(s.Station - station))
                                 .FirstOrDefault();

            if (nearest != null)
            {
                radRange.Checked = true;
                pnlRange.Enabled = true;

                if (isStartStake)
                {
                    for (int i = 0; i < cmbStartStake.Items.Count; i++)
                    {
                        if (cmbStartStake.Items[i] is EntityComboItem item && item.Id == nearest.SampleLineId)
                        {
                            cmbStartStake.SelectedIndex = i;
                            break;
                        }
                    }
                    numStartStation.Value = (decimal)nearest.Station;
                }
                else
                {
                    for (int i = 0; i < cmbEndStake.Items.Count; i++)
                    {
                        if (cmbEndStake.Items[i] is EntityComboItem item && item.Id == nearest.SampleLineId)
                        {
                            cmbEndStake.SelectedIndex = i;
                            break;
                        }
                    }
                    numEndStation.Value = (decimal)nearest.Station;
                }

                ApplyRangeFilter();
            }
        }

        private void SelectAlignmentInCombo(ObjectId alignmentId)
        {
            for (int i = 0; i < cmbAlignment.Items.Count; i++)
            {
                if (cmbAlignment.Items[i] is EntityComboItem item && item.Id == alignmentId)
                {
                    cmbAlignment.SelectedIndex = i;
                    break;
                }
            }
        }

        private void SelectProfileInCombo(ObjectId profileId)
        {
            for (int i = 0; i < cmbProfile.Items.Count; i++)
            {
                if (cmbProfile.Items[i] is EntityComboItem item && item.Id == profileId)
                {
                    cmbProfile.SelectedIndex = i;
                    break;
                }
            }
        }

        #endregion

        #region Thực Thi Lệnh (Execute Fix Profile)

        private void BtnExecute_Click(object? sender, EventArgs e)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null || _currentAlignmentId.IsNull || _currentProfileId.IsNull)
            {
                MessageBox.Show("Vui lòng chọn tim tuyến và đường tự nhiên hợp lệ!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selectedStakes = _stakes.Where(s => s.IsSelected && s.HasElevation).OrderBy(s => s.Station).ToList();
            if (selectedStakes.Count == 0)
            {
                MessageBox.Show("Không có cọc hợp lệ nào được chọn trong phạm vi!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string targetName = txtTargetProfileName.Text.Trim();
            if (string.IsNullOrEmpty(targetName))
            {
                targetName = "Fix-Profile";
            }

            try
            {
                btnExecute.Enabled = false;
                lblStatus.Text = "Đang xử lý tạo đường tự nhiên theo cọc...";
                lblStatus.ForeColor = DrawingColor.FromArgb(16, 76, 140);
                this.Update();

                using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                {
                    var alignment = tr.GetObject(_currentAlignmentId, OpenMode.ForRead) as Alignment;
                    var sourceProfile = tr.GetObject(_currentProfileId, OpenMode.ForRead) as Profile;

                    if (alignment == null || sourceProfile == null)
                    {
                        MessageBox.Show("Không thể truy cập dữ liệu Alignment hoặc Profile!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    // 1. Thu thập danh sách (Station, Elevation)
                    var pviList = new List<(double station, double elevation)>();
                    foreach (var st in selectedStakes)
                    {
                        pviList.Add((Math.Round(st.Station, 2), Math.Round(st.Elevation, 3)));
                    }

                    // Bổ sung PVI 2 đầu đoạn nếu lý trình bắt đầu/kết thúc không trùng cọc
                    if (radRange.Checked && chkIncludeBoundary.Checked)
                    {
                        double minSt = Math.Min((double)numStartStation.Value, (double)numEndStation.Value);
                        double maxSt = Math.Max((double)numStartStation.Value, (double)numEndStation.Value);

                        if (minSt < pviList.First().station && minSt >= sourceProfile.StartingStation && minSt <= sourceProfile.EndingStation)
                        {
                            try
                            {
                                double elev = sourceProfile.ElevationAt(minSt);
                                pviList.Insert(0, (Math.Round(minSt, 2), Math.Round(elev, 3)));
                            }
                            catch { }
                        }

                        if (maxSt > pviList.Last().station && maxSt >= sourceProfile.StartingStation && maxSt <= sourceProfile.EndingStation)
                        {
                            try
                            {
                                double elev = sourceProfile.ElevationAt(maxSt);
                                pviList.Add((Math.Round(maxSt, 2), Math.Round(elev, 3)));
                            }
                            catch { }
                        }
                    }

                    // Đảm bảo không trùng Station và đã sort
                    var uniquePoints = new List<(double station, double elevation)>();
                    var seenStations = new HashSet<double>();
                    foreach (var pt in pviList.OrderBy(p => p.station))
                    {
                        if (seenStations.Add(pt.station))
                        {
                            uniquePoints.Add(pt);
                        }
                    }

                    // 2. Tìm Profile đích nếu đã tồn tại
                    ObjectId existingTargetProfileId = ObjectId.Null;
                    foreach (ObjectId pId in alignment.GetProfileIds())
                    {
                        var p = tr.GetObject(pId, OpenMode.ForRead) as Profile;
                        if (p != null && string.Equals(p.Name, targetName, StringComparison.OrdinalIgnoreCase))
                        {
                            existingTargetProfileId = pId;
                            break;
                        }
                    }

                    Profile? targetProfile = null;
                    int addedCount = 0;

                    if (!existingTargetProfileId.IsNull && radTargetUpdate.Checked)
                    {
                        // CẬP NHẬT / GHI ĐÈ VÀO PROFILE ĐÃ CÓ
                        targetProfile = tr.GetObject(existingTargetProfileId, OpenMode.ForWrite) as Profile;
                        if (targetProfile == null)
                        {
                            MessageBox.Show("Không thể mở Profile đích để ghi đè!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }

                        double startRange = uniquePoints.First().station;
                        double endRange = uniquePoints.Last().station;

                        // Thêm / Cập nhật các điểm PVI mới trước
                        foreach (var (station, elevation) in uniquePoints)
                        {
                            try
                            {
                                if (station >= alignment.StartingStation - 0.001 && station <= alignment.EndingStation + 0.001)
                                {
                                    targetProfile.PVIs.AddPVI(station, elevation);
                                    addedCount++;
                                }
                            }
                            catch (Exception ex)
                            {
                                doc.Editor.WriteMessage($"\nCảnh báo PVI tại {station:F2}: {ex.Message}");
                            }
                        }

                        // Xóa các PVI cũ nằm trong phạm vi nhưng không thuộc danh sách mới
                        var newSet = new HashSet<double>(uniquePoints.Select(u => u.station));
                        for (int i = targetProfile.PVIs.Count - 1; i >= 0; i--)
                        {
                            var pvi = targetProfile.PVIs[i];
                            double pviSt = Math.Round(pvi.RawStation, 2);
                            if (pviSt >= startRange - 0.001 && pviSt <= endRange + 0.001)
                            {
                                if (!newSet.Contains(pviSt))
                                {
                                    try
                                    {
                                        targetProfile.PVIs.RemoveAt(i);
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                    else
                    {
                        // TẠO PROFILE MỚI
                        if (!existingTargetProfileId.IsNull)
                        {
                            int suffix = 1;
                            string baseName = targetName;
                            while (IsProfileNameExists(tr, alignment.GetProfileIds(), targetName))
                            {
                                targetName = $"{baseName}_{suffix}";
                                suffix++;
                            }
                        }

                        ObjectId layerID = alignment.LayerId;
                        ObjectId styleId = GetSelectedProfileStyleId();
                        ObjectId labelSetId = GetSelectedLabelSetStyleId();

                        ObjectId newProfId = Profile.CreateByLayout(targetName, alignment.ObjectId, layerID, styleId, labelSetId);
                        targetProfile = tr.GetObject(newProfId, OpenMode.ForWrite) as Profile;

                        if (targetProfile == null)
                        {
                            MessageBox.Show("Không thể tạo Profile mới!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }

                        foreach (var (station, elevation) in uniquePoints)
                        {
                            try
                            {
                                if (station >= alignment.StartingStation - 0.001 && station <= alignment.EndingStation + 0.001)
                                {
                                    targetProfile.PVIs.AddPVI(station, elevation);
                                    addedCount++;
                                }
                            }
                            catch (Exception ex)
                            {
                                doc.Editor.WriteMessage($"\nCảnh báo PVI tại {station:F2}: {ex.Message}");
                            }
                        }
                    }

                    // Đổi style Profile gốc thành không in nếu được chọn
                    if (chkChangeSourceStyle.Checked)
                    {
                        var cdoc = CivilApplication.ActiveDocument;
                        if (cdoc != null && cdoc.Styles.ProfileStyles.Contains("Existing Ground Profile (KHONG IN)"))
                        {
                            try
                            {
                                sourceProfile.UpgradeOpen();
                                sourceProfile.StyleName = "Existing Ground Profile (KHONG IN)";
                            }
                            catch { }
                        }
                    }

                    tr.Commit();

                    string msg = $"Đã tạo/cập nhật thành công {addedCount} đỉnh PVI cho Profile '{targetName}'!";
                    lblStatus.Text = msg;
                    lblStatus.ForeColor = DrawingColor.FromArgb(16, 120, 40);
                    doc.Editor.WriteMessage($"\n✔ {msg}\n");

                    MessageBox.Show(msg, "Hoàn thành", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Lỗi: " + ex.Message;
                lblStatus.ForeColor = DrawingColor.FromArgb(185, 28, 28);
                MessageBox.Show("Lỗi khi thực hiện: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnExecute.Enabled = true;
            }
        }

        private bool IsProfileNameExists(Transaction tr, ObjectIdCollection profileIds, string name)
        {
            foreach (ObjectId pId in profileIds)
            {
                var p = tr.GetObject(pId, OpenMode.ForRead) as Profile;
                if (p != null && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private ObjectId GetSelectedProfileStyleId()
        {
            if (cmbProfileStyle.SelectedItem is EntityComboItem item && !item.Id.IsNull)
            {
                return item.Id;
            }

            var cdoc = CivilApplication.ActiveDocument;
            if (cdoc != null)
            {
                if (cdoc.Styles.ProfileStyles.Contains("0.TN"))
                    return cdoc.Styles.ProfileStyles["0.TN"];
                if (cdoc.Styles.ProfileStyles.Count > 0)
                    return cdoc.Styles.ProfileStyles[0];
            }
            return ObjectId.Null;
        }

        private ObjectId GetSelectedLabelSetStyleId()
        {
            if (cmbLabelSetStyle.SelectedItem is EntityComboItem item && !item.Id.IsNull)
            {
                return item.Id;
            }

            var cdoc = CivilApplication.ActiveDocument;
            if (cdoc != null)
            {
                if (cdoc.Styles.LabelSetStyles.ProfileLabelSetStyles.Contains("_No Labels"))
                    return cdoc.Styles.LabelSetStyles.ProfileLabelSetStyles["_No Labels"];
                if (cdoc.Styles.LabelSetStyles.ProfileLabelSetStyles.Count > 0)
                    return cdoc.Styles.LabelSetStyles.ProfileLabelSetStyles[0];
            }
            return ObjectId.Null;
        }

        private static string FormatStation(double station)
        {
            long km = (long)Math.Floor(station / 1000.0);
            double m = station - (km * 1000.0);
            return $"Km{km}+{m:000.00}";
        }

        #endregion
    }
}

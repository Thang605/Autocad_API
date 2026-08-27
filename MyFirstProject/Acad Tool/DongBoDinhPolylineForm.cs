// (C) Copyright 2026 by T27
// Form giao diện cho lệnh Đồng Bộ Đỉnh Polyline (AT_DongBoDinhPolyline)
//

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsButton = System.Windows.Forms.Button;
using WinFormsGroupBox = System.Windows.Forms.GroupBox;
using WinFormsPanel = System.Windows.Forms.Panel;
using DrawingFont = System.Drawing.Font;
using DrawingColor = System.Drawing.Color;
using DrawingSize = System.Drawing.Size;
using DrawingPoint = System.Drawing.Point;

namespace Civil3DCsharp
{
    /// <summary>
    /// Thông tin chi tiết một đỉnh của Polyline
    /// </summary>
    public class PolylineVertexInfo
    {
        public int Index { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double Bulge { get; set; }
    }

    /// <summary>
    /// Thông tin tổng quan của Polyline
    /// </summary>
    public class PolylineInfoItem
    {
        public ObjectId Id { get; set; } = ObjectId.Null;
        public string Handle { get; set; } = string.Empty;
        public string Layer { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public int VertexCount { get; set; } = 0;
        public double Length { get; set; } = 0;
        public bool IsClosed { get; set; } = false;
        public double Elevation { get; set; } = 0;
        public List<PolylineVertexInfo> Vertices { get; set; } = new List<PolylineVertexInfo>();
    }

    /// <summary>
    /// Cấu hình tùy chọn đồng bộ
    /// </summary>
    public class PolylineSyncConfig
    {
        public bool SyncBulge { get; set; } = true;
        public bool SyncElevation { get; set; } = true;
        public bool SyncClosed { get; set; } = false;
        public bool ReverseOrder { get; set; } = false;
        public bool KeepFormOpen { get; set; } = false;
    }

    /// <summary>
    /// Form giao diện Đồng Bộ Đỉnh Polyline
    /// </summary>
    public class DongBoDinhPolylineForm : Form
    {
        #region Persistent State (Ghi nhớ giá trị qua các phiên)
        private static ObjectId _lastTargetPolylineId = ObjectId.Null;
        private static ObjectId _lastSourcePolylineId = ObjectId.Null;
        private static bool _lastSyncBulge = true;
        private static bool _lastSyncElevation = true;
        private static bool _lastSyncClosed = false;
        private static bool _lastReverseOrder = false;
        private static bool _lastKeepFormOpen = false;
        private static DrawingSize _lastFormSize = new DrawingSize(820, 680);
        #endregion

        #region Callbacks tương tác với AutoCAD Engine
        public Func<ObjectId, PolylineInfoItem?>? OnGetPolylineInfo { get; set; }
        public Func<PolylineInfoItem?>? OnPickTargetPolyline { get; set; }
        public Func<PolylineInfoItem?>? OnPickSourcePolyline { get; set; }
        public Func<PolylineInfoItem, PolylineInfoItem, PolylineSyncConfig, bool>? OnExecuteSync { get; set; }
        #endregion

        #region Dữ liệu hiện tại
        public PolylineInfoItem? TargetPolyline { get; private set; }
        public PolylineInfoItem? SourcePolyline { get; private set; }
        #endregion

        #region UI Controls
        private WinFormsPanel pnlHeader = null!;
        private WinFormsLabel lblTitle = null!;
        private WinFormsLabel lblSubtitle = null!;

        // Group 1: Polyline 1 (Target)
        private WinFormsGroupBox grpTarget = null!;
        private WinFormsButton btnPickTarget = null!;
        private WinFormsLabel lblTargetStatus = null!;
        private WinFormsLabel lblTargetLayer = null!;
        private WinFormsLabel lblTargetCount = null!;
        private WinFormsLabel lblTargetLength = null!;
        private WinFormsLabel lblTargetClosed = null!;

        // Group 2: Polyline 2 (Source)
        private WinFormsGroupBox grpSource = null!;
        private WinFormsButton btnPickSource = null!;
        private WinFormsLabel lblSourceStatus = null!;
        private WinFormsLabel lblSourceLayer = null!;
        private WinFormsLabel lblSourceCount = null!;
        private WinFormsLabel lblSourceLength = null!;
        private WinFormsLabel lblSourceClosed = null!;

        // Swap button
        private WinFormsButton btnSwap = null!;

        // Status Banner
        private WinFormsPanel pnlStatusBanner = null!;
        private WinFormsLabel lblStatusMessage = null!;

        // DataGridView Preview
        private WinFormsGroupBox grpPreview = null!;
        private DataGridView dgvVertices = null!;

        // Group Options
        private WinFormsGroupBox grpOptions = null!;
        private CheckBox chkSyncBulge = null!;
        private CheckBox chkSyncElevation = null!;
        private CheckBox chkSyncClosed = null!;
        private CheckBox chkReverseOrder = null!;
        private CheckBox chkKeepFormOpen = null!;
        private WinFormsButton btnAutoMatchDirection = null!;

        // Bottom Action Buttons
        private WinFormsButton btnExecute = null!;
        private WinFormsButton btnPickNewPair = null!;
        private WinFormsButton btnClose = null!;
        #endregion

        public DongBoDinhPolylineForm()
        {
            InitializeComponent();
            RestoreLastSettings();
        }

        private void InitializeComponent()
        {
            var regularFont = new DrawingFont("Segoe UI", 9.5F, FontStyle.Regular);
            var boldFont = new DrawingFont("Segoe UI", 9.5F, FontStyle.Bold);
            var titleFont = new DrawingFont("Segoe UI", 13.5F, FontStyle.Bold);
            var subtitleFont = new DrawingFont("Segoe UI", 8.5F, FontStyle.Italic);

            this.SuspendLayout();

            // Form Properties
            this.Text = "Đồng Bộ Đỉnh Polyline - T27 Tools";
            this.ClientSize = _lastFormSize;
            this.MinimumSize = new DrawingSize(760, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = regularFont;
            this.Icon = SystemIcons.Application;

            // ==================== HEADER PANEL ====================
            pnlHeader = new WinFormsPanel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = DrawingColor.FromArgb(24, 80, 150),
                Padding = new Padding(15, 8, 15, 8)
            };

            lblTitle = new WinFormsLabel
            {
                Text = "ĐỒNG BỘ TỌA ĐỘ ĐỈNH POLYLINE",
                Font = titleFont,
                ForeColor = DrawingColor.White,
                AutoSize = true,
                Location = new DrawingPoint(12, 8)
            };

            lblSubtitle = new WinFormsLabel
            {
                Text = "Kiểm tra số đỉnh và đồng bộ tọa độ đỉnh của Polyline Đích (PL1) theo Polyline Nguồn (PL2)",
                Font = subtitleFont,
                ForeColor = DrawingColor.FromArgb(215, 235, 255),
                AutoSize = true,
                Location = new DrawingPoint(14, 34)
            };

            pnlHeader.Controls.AddRange(new Control[] { lblTitle, lblSubtitle });

            // ==================== TOP CONTAINER (PL1 & PL2) ====================
            var pnlTop = new WinFormsPanel
            {
                Location = new DrawingPoint(12, 68),
                Size = new DrawingSize(796, 175),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            // Group 1: Polyline 1 (Đích)
            grpTarget = new WinFormsGroupBox
            {
                Text = " Polyline 1 (Đích - Cần đổi tọa độ) ",
                Font = boldFont,
                ForeColor = DrawingColor.FromArgb(160, 50, 0),
                Location = new DrawingPoint(0, 0),
                Size = new DrawingSize(370, 170),
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };

            btnPickTarget = new WinFormsButton
            {
                Text = "📍 Chọn Polyline 1 trên CAD",
                Font = boldFont,
                Location = new DrawingPoint(12, 24),
                Size = new DrawingSize(346, 32),
                BackColor = DrawingColor.FromArgb(255, 240, 230),
                ForeColor = DrawingColor.FromArgb(180, 40, 0),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnPickTarget.FlatAppearance.BorderColor = DrawingColor.FromArgb(220, 120, 80);
            btnPickTarget.Click += (s, e) => DoPickTarget();

            lblTargetStatus = CreateInfoLabel("Đối tượng: (Chưa chọn)", 14, 62, boldFont, DrawingColor.FromArgb(100, 100, 100));
            lblTargetLayer = CreateInfoLabel("Layer: ---", 14, 84, regularFont, DrawingColor.Black);
            lblTargetCount = CreateInfoLabel("Số đỉnh: ---", 14, 104, boldFont, DrawingColor.FromArgb(180, 40, 0));
            lblTargetLength = CreateInfoLabel("Chiều dài: ---", 14, 124, regularFont, DrawingColor.Black);
            lblTargetClosed = CreateInfoLabel("Trạng thái: ---", 14, 144, regularFont, DrawingColor.Black);

            grpTarget.Controls.AddRange(new Control[] {
                btnPickTarget, lblTargetStatus, lblTargetLayer, lblTargetCount, lblTargetLength, lblTargetClosed
            });

            // Center Swap Button
            btnSwap = new WinFormsButton
            {
                Text = "⇄\nHoán\nđổi",
                Font = new DrawingFont("Segoe UI", 9F, FontStyle.Bold),
                Location = new DrawingPoint(375, 48),
                Size = new DrawingSize(46, 80),
                BackColor = DrawingColor.FromArgb(240, 243, 248),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top
            };
            btnSwap.FlatAppearance.BorderColor = DrawingColor.FromArgb(180, 200, 220);
            btnSwap.Click += (s, e) => SwapPolylines();

            // Group 2: Polyline 2 (Nguồn)
            grpSource = new WinFormsGroupBox
            {
                Text = " Polyline 2 (Nguồn - Mẫu tọa độ) ",
                Font = boldFont,
                ForeColor = DrawingColor.FromArgb(0, 100, 40),
                Location = new DrawingPoint(426, 0),
                Size = new DrawingSize(370, 170),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            btnPickSource = new WinFormsButton
            {
                Text = "📍 Chọn Polyline 2 trên CAD",
                Font = boldFont,
                Location = new DrawingPoint(12, 24),
                Size = new DrawingSize(346, 32),
                BackColor = DrawingColor.FromArgb(235, 252, 235),
                ForeColor = DrawingColor.FromArgb(0, 120, 50),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnPickSource.FlatAppearance.BorderColor = DrawingColor.FromArgb(100, 180, 120);
            btnPickSource.Click += (s, e) => DoPickSource();

            lblSourceStatus = CreateInfoLabel("Đối tượng: (Chưa chọn)", 14, 62, boldFont, DrawingColor.FromArgb(100, 100, 100));
            lblSourceLayer = CreateInfoLabel("Layer: ---", 14, 84, regularFont, DrawingColor.Black);
            lblSourceCount = CreateInfoLabel("Số đỉnh: ---", 14, 104, boldFont, DrawingColor.FromArgb(0, 120, 50));
            lblSourceLength = CreateInfoLabel("Chiều dài: ---", 14, 124, regularFont, DrawingColor.Black);
            lblSourceClosed = CreateInfoLabel("Trạng thái: ---", 14, 144, regularFont, DrawingColor.Black);

            grpSource.Controls.AddRange(new Control[] {
                btnPickSource, lblSourceStatus, lblSourceLayer, lblSourceCount, lblSourceLength, lblSourceClosed
            });

            pnlTop.Controls.AddRange(new Control[] { grpTarget, btnSwap, grpSource });

            // ==================== STATUS BANNER ====================
            pnlStatusBanner = new WinFormsPanel
            {
                Location = new DrawingPoint(12, 248),
                Size = new DrawingSize(796, 36),
                BackColor = DrawingColor.FromArgb(245, 245, 245),
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Padding = new Padding(10, 6, 10, 6)
            };

            lblStatusMessage = new WinFormsLabel
            {
                Text = "ℹ️ Vui lòng chọn cả Polyline 1 và Polyline 2 để kiểm tra số đỉnh.",
                Font = boldFont,
                ForeColor = DrawingColor.FromArgb(80, 80, 80),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            pnlStatusBanner.Controls.Add(lblStatusMessage);

            // ==================== PREVIEW DATAGRIDVIEW ====================
            grpPreview = new WinFormsGroupBox
            {
                Text = " So sánh tọa độ đỉnh trước & sau khi đồng bộ ",
                Font = boldFont,
                Location = new DrawingPoint(12, 290),
                Size = new DrawingSize(796, 180),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            dgvVertices = new DataGridView
            {
                Dock = DockStyle.Fill,
                Font = new DrawingFont("Segoe UI", 9F, FontStyle.Regular),
                BackgroundColor = DrawingColor.White,
                BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            SetupDataGridViewColumns();
            grpPreview.Controls.Add(dgvVertices);

            // ==================== OPTIONS GROUP ====================
            grpOptions = new WinFormsGroupBox
            {
                Text = " Tùy chọn đồng bộ ",
                Font = boldFont,
                Location = new DrawingPoint(12, 476),
                Size = new DrawingSize(796, 95),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            chkSyncBulge = new CheckBox
            {
                Text = "Đồng bộ Bulge (đoạn cong/cung tròn)",
                Font = regularFont,
                Location = new DrawingPoint(16, 24),
                Size = new DrawingSize(270, 24),
                Checked = true
            };
            chkSyncBulge.CheckedChanged += (s, e) => UpdatePreviewTable();

            chkSyncElevation = new CheckBox
            {
                Text = "Đồng bộ Cao độ (Elevation / Z)",
                Font = regularFont,
                Location = new DrawingPoint(296, 24),
                Size = new DrawingSize(240, 24),
                Checked = true
            };

            chkSyncClosed = new CheckBox
            {
                Text = "Đồng bộ trạng thái Đóng/Mở (Closed)",
                Font = regularFont,
                Location = new DrawingPoint(546, 24),
                Size = new DrawingSize(240, 24),
                Checked = false
            };

            chkReverseOrder = new CheckBox
            {
                Text = "Đảo ngược thứ tự đỉnh khi gán (Reverse Direction)",
                Font = regularFont,
                Location = new DrawingPoint(16, 56),
                Size = new DrawingSize(340, 24),
                Checked = false,
                ForeColor = DrawingColor.FromArgb(150, 50, 0)
            };
            chkReverseOrder.CheckedChanged += (s, e) => UpdatePreviewTable();

            chkKeepFormOpen = new CheckBox
            {
                Text = "Giữ hộp thoại mở sau khi đồng bộ",
                Font = regularFont,
                Location = new DrawingPoint(365, 56),
                Size = new DrawingSize(240, 24),
                Checked = false
            };

            btnAutoMatchDirection = new WinFormsButton
            {
                Text = "⚡ Tự động so khớp chiều",
                Font = regularFont,
                Location = new DrawingPoint(615, 54),
                Size = new DrawingSize(168, 28),
                BackColor = DrawingColor.FromArgb(240, 245, 255),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnAutoMatchDirection.FlatAppearance.BorderColor = DrawingColor.FromArgb(160, 190, 230);
            btnAutoMatchDirection.Click += (s, e) => AutoMatchDirection();

            grpOptions.Controls.AddRange(new Control[] {
                chkSyncBulge, chkSyncElevation, chkSyncClosed,
                chkReverseOrder, chkKeepFormOpen, btnAutoMatchDirection
            });

            // ==================== BOTTOM BUTTONS ====================
            var pnlBottom = new WinFormsPanel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                BackColor = DrawingColor.FromArgb(245, 248, 252),
                Padding = new Padding(12, 8, 12, 8)
            };

            btnExecute = new WinFormsButton
            {
                Text = "⚡ THỰC HIỆN ĐỒNG BỘ",
                Font = new DrawingFont("Segoe UI", 10.5F, FontStyle.Bold),
                Location = new DrawingPoint(12, 8),
                Size = new DrawingSize(240, 36),
                BackColor = DrawingColor.FromArgb(0, 130, 60),
                ForeColor = DrawingColor.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Enabled = false
            };
            btnExecute.FlatAppearance.BorderSize = 0;
            btnExecute.Click += (s, e) => DoExecuteSync();

            btnPickNewPair = new WinFormsButton
            {
                Text = "📍 Chọn Cặp Mới",
                Font = regularFont,
                Location = new DrawingPoint(262, 8),
                Size = new DrawingSize(150, 36),
                BackColor = DrawingColor.FromArgb(235, 242, 252),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnPickNewPair.FlatAppearance.BorderColor = DrawingColor.FromArgb(180, 205, 235);
            btnPickNewPair.Click += (s, e) => PickBothPolylines();

            btnClose = new WinFormsButton
            {
                Text = "Đóng",
                Font = regularFont,
                Location = new DrawingPoint(688, 8),
                Size = new DrawingSize(110, 36),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = DrawingColor.FromArgb(240, 240, 240),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderColor = DrawingColor.FromArgb(200, 200, 200);
            btnClose.Click += (s, e) => this.Close();

            pnlBottom.Controls.AddRange(new Control[] { btnExecute, btnPickNewPair, btnClose });

            // Add all main components
            this.Controls.AddRange(new Control[] {
                pnlHeader, pnlTop, pnlStatusBanner, grpPreview, grpOptions, pnlBottom
            });

            this.FormClosing += (s, e) => SaveCurrentSettings();
            this.ResumeLayout(false);
        }

        private WinFormsLabel CreateInfoLabel(string text, int x, int y, DrawingFont font, DrawingColor color)
        {
            return new WinFormsLabel
            {
                Text = text,
                Location = new DrawingPoint(x, y),
                AutoSize = true,
                Font = font,
                ForeColor = color
            };
        }

        private void SetupDataGridViewColumns()
        {
            dgvVertices.Columns.Clear();
            dgvVertices.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "# Đỉnh", FillWeight = 30 });
            dgvVertices.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "PL1 Cũ (X, Y)", FillWeight = 85 });
            dgvVertices.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "PL2 Nguồn (X, Y)", FillWeight = 85 });
            dgvVertices.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "PL1 Sau Đồng Bộ (X, Y)", FillWeight = 85 });
            dgvVertices.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Độ lệch dXY (m)", FillWeight = 50 });
            dgvVertices.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Bulge", FillWeight = 35 });
        }

        #region Public API nạp dữ liệu ban đầu
        public void LoadInitialPolylines(PolylineInfoItem? target, PolylineInfoItem? source)
        {
            TargetPolyline = target;
            SourcePolyline = source;
            UpdateTargetUI();
            UpdateSourceUI();
            ValidateAndRefreshUI();
        }
        #endregion

        #region UI Update & Validation Logic
        private void UpdateTargetUI()
        {
            if (TargetPolyline != null)
            {
                lblTargetStatus.Text = $"Đối tượng: {TargetPolyline.TypeName} (Handle: {TargetPolyline.Handle})";
                lblTargetStatus.ForeColor = DrawingColor.FromArgb(0, 80, 180);
                lblTargetLayer.Text = $"Layer: {TargetPolyline.Layer}";
                lblTargetCount.Text = $"Số đỉnh: {TargetPolyline.VertexCount} đỉnh";
                lblTargetCount.ForeColor = DrawingColor.FromArgb(180, 40, 0);
                lblTargetLength.Text = $"Chiều dài: {TargetPolyline.Length:F3} m";
                lblTargetClosed.Text = $"Trạng thái: {(TargetPolyline.IsClosed ? "Đóng (Closed)" : "Hở (Open)")} | Elev: {TargetPolyline.Elevation:F3}";
            }
            else
            {
                lblTargetStatus.Text = "Đối tượng: (Chưa chọn)";
                lblTargetStatus.ForeColor = DrawingColor.FromArgb(100, 100, 100);
                lblTargetLayer.Text = "Layer: ---";
                lblTargetCount.Text = "Số đỉnh: ---";
                lblTargetLength.Text = "Chiều dài: ---";
                lblTargetClosed.Text = "Trạng thái: ---";
            }
        }

        private void UpdateSourceUI()
        {
            if (SourcePolyline != null)
            {
                lblSourceStatus.Text = $"Đối tượng: {SourcePolyline.TypeName} (Handle: {SourcePolyline.Handle})";
                lblSourceStatus.ForeColor = DrawingColor.FromArgb(0, 100, 40);
                lblSourceLayer.Text = $"Layer: {SourcePolyline.Layer}";
                lblSourceCount.Text = $"Số đỉnh: {SourcePolyline.VertexCount} đỉnh";
                lblSourceCount.ForeColor = DrawingColor.FromArgb(0, 120, 50);
                lblSourceLength.Text = $"Chiều dài: {SourcePolyline.Length:F3} m";
                lblSourceClosed.Text = $"Trạng thái: {(SourcePolyline.IsClosed ? "Đóng (Closed)" : "Hở (Open)")} | Elev: {SourcePolyline.Elevation:F3}";
            }
            else
            {
                lblSourceStatus.Text = "Đối tượng: (Chưa chọn)";
                lblSourceStatus.ForeColor = DrawingColor.FromArgb(100, 100, 100);
                lblSourceLayer.Text = "Layer: ---";
                lblSourceCount.Text = "Số đỉnh: ---";
                lblSourceLength.Text = "Chiều dài: ---";
                lblSourceClosed.Text = "Trạng thái: ---";
            }
        }

        public void ValidateAndRefreshUI()
        {
            if (TargetPolyline == null || SourcePolyline == null)
            {
                pnlStatusBanner.BackColor = DrawingColor.FromArgb(245, 245, 245);
                lblStatusMessage.ForeColor = DrawingColor.FromArgb(80, 80, 80);
                lblStatusMessage.Text = "ℹ️ Vui lòng chọn cả Polyline 1 và Polyline 2 trên bản vẽ CAD.";
                btnExecute.Enabled = false;
                btnExecute.BackColor = DrawingColor.FromArgb(180, 180, 180);
                dgvVertices.Rows.Clear();
                return;
            }

            if (TargetPolyline.Id == SourcePolyline.Id)
            {
                pnlStatusBanner.BackColor = DrawingColor.FromArgb(255, 240, 230);
                lblStatusMessage.ForeColor = DrawingColor.FromArgb(200, 50, 0);
                lblStatusMessage.Text = "⚠️ Cảnh báo: Polyline 1 và Polyline 2 là cùng 1 đối tượng!";
                btnExecute.Enabled = false;
                btnExecute.BackColor = DrawingColor.FromArgb(180, 180, 180);
                UpdatePreviewTable();
                return;
            }

            int count1 = TargetPolyline.VertexCount;
            int count2 = SourcePolyline.VertexCount;

            if (count1 != count2)
            {
                // Khác số đỉnh -> Báo lỗi đỏ
                pnlStatusBanner.BackColor = DrawingColor.FromArgb(255, 232, 232);
                lblStatusMessage.ForeColor = DrawingColor.FromArgb(190, 20, 20);
                lblStatusMessage.Text = $"❌ KHÔNG CÙNG SỐ ĐỈNH: Polyline 1 có {count1} đỉnh, Polyline 2 có {count2} đỉnh (Lệch {Math.Abs(count1 - count2)} đỉnh). Không thể đồng bộ trực tiếp!";
                btnExecute.Enabled = false;
                btnExecute.BackColor = DrawingColor.FromArgb(180, 180, 180);
                UpdatePreviewTable();
            }
            else
            {
                // Cùng số đỉnh -> Báo hợp lệ xanh
                pnlStatusBanner.BackColor = DrawingColor.FromArgb(232, 252, 235);
                lblStatusMessage.ForeColor = DrawingColor.FromArgb(0, 130, 40);
                lblStatusMessage.Text = $"✅ HỢP LỆ: Cả 2 Polyline đều có {count1} đỉnh. Sẵn sàng đồng bộ tọa độ đỉnh!";
                btnExecute.Enabled = true;
                btnExecute.BackColor = DrawingColor.FromArgb(0, 130, 60);
                UpdatePreviewTable();
            }
        }

        private void UpdatePreviewTable()
        {
            dgvVertices.Rows.Clear();
            if (TargetPolyline == null || SourcePolyline == null) return;

            int count = Math.Max(TargetPolyline.Vertices.Count, SourcePolyline.Vertices.Count);
            bool reverse = chkReverseOrder.Checked;
            bool sameCount = TargetPolyline.VertexCount == SourcePolyline.VertexCount;

            var srcVerts = new List<PolylineVertexInfo>(SourcePolyline.Vertices);
            if (reverse)
            {
                srcVerts.Reverse();
            }

            for (int i = 0; i < count; i++)
            {
                string idxText = $"#{i + 1}";
                string targetOldText = i < TargetPolyline.Vertices.Count
                    ? $"{TargetPolyline.Vertices[i].X:F3}, {TargetPolyline.Vertices[i].Y:F3}"
                    : "---";

                string sourceText = i < srcVerts.Count
                    ? $"{srcVerts[i].X:F3}, {srcVerts[i].Y:F3}"
                    : "---";

                string targetNewText = "---";
                string deltaText = "---";
                string bulgeText = "---";

                if (sameCount && i < TargetPolyline.Vertices.Count && i < srcVerts.Count)
                {
                    var tv = TargetPolyline.Vertices[i];
                    var sv = srcVerts[i];
                    targetNewText = $"{sv.X:F3}, {sv.Y:F3}";
                    double dist = Math.Sqrt(Math.Pow(tv.X - sv.X, 2) + Math.Pow(tv.Y - sv.Y, 2));
                    deltaText = $"{dist:F3}";
                    bulgeText = chkSyncBulge.Checked ? $"{sv.Bulge:F4}" : $"{tv.Bulge:F4}";
                }

                int rowIndex = dgvVertices.Rows.Add(idxText, targetOldText, sourceText, targetNewText, deltaText, bulgeText);

                if (!sameCount)
                {
                    dgvVertices.Rows[rowIndex].DefaultCellStyle.ForeColor = DrawingColor.Gray;
                }
            }
        }

        private void AutoMatchDirection()
        {
            if (TargetPolyline == null || SourcePolyline == null || TargetPolyline.VertexCount != SourcePolyline.VertexCount)
            {
                MessageBox.Show("Vui lòng chọn 2 Polyline có cùng số đỉnh để so khớp chiều!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Tính tổng khoảng cách theo chiều thuận và chiều ngược
            int n = TargetPolyline.Vertices.Count;
            double sumDistNormal = 0;
            double sumDistReverse = 0;

            for (int i = 0; i < n; i++)
            {
                var ptT = TargetPolyline.Vertices[i];
                var ptSNormal = SourcePolyline.Vertices[i];
                var ptSRev = SourcePolyline.Vertices[n - 1 - i];

                sumDistNormal += Math.Sqrt(Math.Pow(ptT.X - ptSNormal.X, 2) + Math.Pow(ptT.Y - ptSNormal.Y, 2));
                sumDistReverse += Math.Sqrt(Math.Pow(ptT.X - ptSRev.X, 2) + Math.Pow(ptT.Y - ptSRev.Y, 2));
            }

            if (sumDistReverse < sumDistNormal)
            {
                chkReverseOrder.Checked = true;
                MessageBox.Show($"Đã tự động chuyển sang chiều ĐẢO NGƯỢC (Độ lệch tổng: {sumDistReverse:F2}m < Thuận: {sumDistNormal:F2}m)",
                    "Kết quả so khớp chiều", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                chkReverseOrder.Checked = false;
                MessageBox.Show($"Chiều THUẬN hiện tại là tối ưu nhất (Độ lệch tổng: {sumDistNormal:F2}m <= Đảo: {sumDistReverse:F2}m)",
                    "Kết quả so khớp chiều", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        #endregion

        #region User Actions
        private void DoPickTarget()
        {
            if (OnPickTargetPolyline == null) return;
            var picked = OnPickTargetPolyline();
            if (picked != null)
            {
                TargetPolyline = picked;
                _lastTargetPolylineId = picked.Id;
                UpdateTargetUI();
                ValidateAndRefreshUI();
            }
        }

        private void DoPickSource()
        {
            if (OnPickSourcePolyline == null) return;
            var picked = OnPickSourcePolyline();
            if (picked != null)
            {
                SourcePolyline = picked;
                _lastSourcePolylineId = picked.Id;
                UpdateSourceUI();
                ValidateAndRefreshUI();
            }
        }

        private void PickBothPolylines()
        {
            DoPickTarget();
            if (TargetPolyline != null)
            {
                DoPickSource();
            }
        }

        private void SwapPolylines()
        {
            var temp = TargetPolyline;
            TargetPolyline = SourcePolyline;
            SourcePolyline = temp;

            if (TargetPolyline != null) _lastTargetPolylineId = TargetPolyline.Id;
            if (SourcePolyline != null) _lastSourcePolylineId = SourcePolyline.Id;

            UpdateTargetUI();
            UpdateSourceUI();
            ValidateAndRefreshUI();
        }

        private void DoExecuteSync()
        {
            if (TargetPolyline == null || SourcePolyline == null) return;
            if (TargetPolyline.VertexCount != SourcePolyline.VertexCount)
            {
                MessageBox.Show("2 Polyline không cùng số đỉnh! Không thể đồng bộ.", "Lỗi số đỉnh", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var config = new PolylineSyncConfig
            {
                SyncBulge = chkSyncBulge.Checked,
                SyncElevation = chkSyncElevation.Checked,
                SyncClosed = chkSyncClosed.Checked,
                ReverseOrder = chkReverseOrder.Checked,
                KeepFormOpen = chkKeepFormOpen.Checked
            };

            if (OnExecuteSync != null)
            {
                bool success = OnExecuteSync(TargetPolyline, SourcePolyline, config);
                if (success)
                {
                    // Refresh lại dữ liệu Target sau khi đã đồng bộ
                    if (OnGetPolylineInfo != null && !TargetPolyline.Id.IsNull)
                    {
                        var refreshedTarget = OnGetPolylineInfo(TargetPolyline.Id);
                        if (refreshedTarget != null)
                        {
                            TargetPolyline = refreshedTarget;
                            UpdateTargetUI();
                            ValidateAndRefreshUI();
                        }
                    }

                    if (!config.KeepFormOpen)
                    {
                        this.DialogResult = DialogResult.OK;
                        this.Close();
                    }
                }
            }
        }
        #endregion

        #region Persistent State Management
        private void SaveCurrentSettings()
        {
            if (TargetPolyline != null && !TargetPolyline.Id.IsNull)
                _lastTargetPolylineId = TargetPolyline.Id;
            if (SourcePolyline != null && !SourcePolyline.Id.IsNull)
                _lastSourcePolylineId = SourcePolyline.Id;

            _lastSyncBulge = chkSyncBulge.Checked;
            _lastSyncElevation = chkSyncElevation.Checked;
            _lastSyncClosed = chkSyncClosed.Checked;
            _lastReverseOrder = chkReverseOrder.Checked;
            _lastKeepFormOpen = chkKeepFormOpen.Checked;
            _lastFormSize = this.ClientSize;
        }

        private void RestoreLastSettings()
        {
            chkSyncBulge.Checked = _lastSyncBulge;
            chkSyncElevation.Checked = _lastSyncElevation;
            chkSyncClosed.Checked = _lastSyncClosed;
            chkReverseOrder.Checked = _lastReverseOrder;
            chkKeepFormOpen.Checked = _lastKeepFormOpen;
            if (_lastFormSize.Width >= 700 && _lastFormSize.Height >= 500)
            {
                this.ClientSize = _lastFormSize;
            }
        }

        public static ObjectId GetLastTargetId() => _lastTargetPolylineId;
        public static ObjectId GetLastSourceId() => _lastSourcePolylineId;
        #endregion
    }
}

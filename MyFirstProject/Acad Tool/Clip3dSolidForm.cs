// (C) Copyright 2026 by T27
// Form giao diện người dùng cho lệnh Cắt / Clip 3D Solid bằng Polyline khép kín trên mặt bằng
//
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Exception = System.Exception;
using Font = System.Drawing.Font;

namespace MyFirstProject.Acad_Tool
{
    public enum ClipSolidMode
    {
        KeepInside = 0,   // Giữ bên trong (Intersect)
        KeepOutside = 1,  // Giữ bên ngoài (Subtract)
        SplitBoth = 2     // Tách thành 2 khối (Inside + Outside)
    }

    /// <summary>
    /// Form giao diện điều khiển lệnh Clip 3D Solid
    /// </summary>
    public class Clip3dSolidForm : Form
    {
        #region Persistent State (Ghi nhớ lựa chọn)
        private static List<ObjectId> _lastSolidIds = new List<ObjectId>();
        private static List<ObjectId> _lastBoundaryIds = new List<ObjectId>();
        private static ObjectId _lastBoundaryId = ObjectId.Null;
        private static ClipSolidMode _lastMode = ClipSolidMode.KeepOutside;
        private static bool _lastDeleteOriginal = true;
        private static bool _lastAssignLayer = false;
        private static string _lastLayerInside = "SOLID_INSIDE";
        private static string _lastLayerOutside = "SOLID_OUTSIDE";
        private static decimal _lastBuffer = 1.0m;
        private static Size _lastFormSize = new Size(580, 680);
        #endregion

        #region Public Properties
        public List<ObjectId> SelectedSolidIds { get; private set; } = new List<ObjectId>();
        public List<ObjectId> SelectedBoundaryIds { get; private set; } = new List<ObjectId>();
        public ObjectId BoundaryId => SelectedBoundaryIds.Count > 0 ? SelectedBoundaryIds[0] : ObjectId.Null;
        public ClipSolidMode ClipMode { get; private set; } = ClipSolidMode.KeepOutside;
        public bool DeleteOriginal { get; private set; } = true;
        public bool AssignLayer { get; private set; } = false;
        public string LayerInside { get; private set; } = "SOLID_INSIDE";
        public string LayerOutside { get; private set; } = "SOLID_OUTSIDE";
        public double BufferHeight { get; private set; } = 1.0;
        public bool IsConfirmed { get; private set; } = false;
        #endregion

        #region UI Controls
        private GroupBox grpSolids;
        private Button btnPickSolids;
        private Button btnSelectAllSolids;
        private Label lblSolidStatus;

        private GroupBox grpBoundary;
        private Button btnPickBoundary;
        private Label lblBoundaryStatus;

        private GroupBox grpMode;
        private RadioButton radInside;
        private RadioButton radOutside;
        private RadioButton radSplit;

        private GroupBox grpOptions;
        private CheckBox chkDeleteOriginal;
        private Label lblBuffer;
        private NumericUpDown nudBuffer;
        private CheckBox chkAssignLayer;
        private Label lblLayerInside;
        private TextBox txtLayerInside;
        private Label lblLayerOutside;
        private TextBox txtLayerOutside;

        private ProgressBar progressBar;
        private Label lblProgress;
        private Button btnExecute;
        private Button btnCancel;
        #endregion

        public Clip3dSolidForm()
        {
            InitializeComponent();
            RestoreLastSettings();
        }

        private void InitializeComponent()
        {
            this.Text = "Cắt / Clip 3D Solid & Body Theo Polyline Mặt Bằng";
            this.Font = new Font("Segoe UI", 9.25F, FontStyle.Regular, GraphicsUnit.Point);
            this.Size = _lastFormSize.Width > 0 ? _lastFormSize : new Size(580, 680);
            this.MinimumSize = new Size(540, 640);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.ShowIcon = true;
            this.MaximizeBox = false;

            int margin = 14;
            int clientWidth = this.ClientSize.Width;

            // 1. GroupBox Chọn 3D Solid / Body / Surface
            grpSolids = new GroupBox
            {
                Text = " 1. Đối tượng 3D Solid / Body / Surface cần cắt ",
                Location = new Point(margin, 12),
                Size = new Size(clientWidth - margin * 2, 95),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Segoe UI", 9.25F, FontStyle.Bold)
            };

            btnPickSolids = new Button
            {
                Text = "Chọn 3D Solid / Body trên CAD",
                Location = new Point(14, 25),
                Size = new Size(220, 32),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                UseVisualStyleBackColor = true
            };
            btnPickSolids.Click += BtnPickSolids_Click;

            btnSelectAllSolids = new Button
            {
                Text = "Chọn tất cả 3D Solid & Body",
                Location = new Point(244, 25),
                Size = new Size(200, 32),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                UseVisualStyleBackColor = true
            };
            btnSelectAllSolids.Click += BtnSelectAllSolids_Click;

            lblSolidStatus = new Label
            {
                Text = "Chưa chọn đối tượng nào.",
                Location = new Point(16, 65),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Italic),
                ForeColor = Color.DarkSlateGray
            };

            grpSolids.Controls.Add(btnPickSolids);
            grpSolids.Controls.Add(btnSelectAllSolids);
            grpSolids.Controls.Add(lblSolidStatus);

            // 2. GroupBox Ranh giới Polyline
            grpBoundary = new GroupBox
            {
                Text = " 2. Ranh giới cắt (Một hoặc nhiều Polyline khép kín) ",
                Location = new Point(margin, grpSolids.Bottom + 10),
                Size = new Size(clientWidth - margin * 2, 95),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Segoe UI", 9.25F, FontStyle.Bold)
            };

            btnPickBoundary = new Button
            {
                Text = "Chọn các Polyline khép kín trên CAD",
                Location = new Point(14, 25),
                Size = new Size(260, 32),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                UseVisualStyleBackColor = true
            };
            btnPickBoundary.Click += BtnPickBoundary_Click;

            lblBoundaryStatus = new Label
            {
                Text = "Chưa chọn đường ranh giới.",
                Location = new Point(16, 65),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Italic),
                ForeColor = Color.DarkSlateGray
            };

            grpBoundary.Controls.Add(btnPickBoundary);
            grpBoundary.Controls.Add(lblBoundaryStatus);

            // 3. GroupBox Chế độ cắt
            grpMode = new GroupBox
            {
                Text = " 3. Chế độ cắt (Clipping Mode) ",
                Location = new Point(margin, grpBoundary.Bottom + 10),
                Size = new Size(clientWidth - margin * 2, 115),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Segoe UI", 9.25F, FontStyle.Bold)
            };

            radInside = new RadioButton
            {
                Text = "Giữ phần BÊN TRONG ranh giới (Intersect - Khuôn cắt)",
                Location = new Point(18, 25),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            radInside.CheckedChanged += (s, e) => UpdateLayerUiState();

            radOutside = new RadioButton
            {
                Text = "Giữ phần BÊN NGOÀI ranh giới (Subtract - Xóa / Khoét bỏ phần bên trong)",
                Location = new Point(18, 52),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Checked = true
            };
            radOutside.CheckedChanged += (s, e) => UpdateLayerUiState();

            radSplit = new RadioButton
            {
                Text = "TÁCH THÀNH CẢ 2 PHẦN (Split - Tạo cả khối Trong và Ngoài)",
                Location = new Point(18, 79),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            radSplit.CheckedChanged += (s, e) => UpdateLayerUiState();

            grpMode.Controls.Add(radInside);
            grpMode.Controls.Add(radOutside);
            grpMode.Controls.Add(radSplit);

            // 4. GroupBox Tùy chọn && Layer
            grpOptions = new GroupBox
            {
                Text = " 4. Tùy chọn nâng cao && Layer ",
                Location = new Point(margin, grpMode.Bottom + 10),
                Size = new Size(clientWidth - margin * 2, 160),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Segoe UI", 9.25F, FontStyle.Bold)
            };

            chkDeleteOriginal = new CheckBox
            {
                Text = "Xóa đối tượng 3D Solid gốc sau khi cắt hoàn tất",
                Location = new Point(18, 25),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                Checked = true
            };

            lblBuffer = new Label
            {
                Text = "Độ đệm chiều cao Z (m):",
                Location = new Point(16, 56),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            nudBuffer = new NumericUpDown
            {
                Location = new Point(180, 53),
                Size = new Size(80, 24),
                Minimum = 0.1m,
                Maximum = 1000m,
                DecimalPlaces = 2,
                Increment = 0.5m,
                Value = 1.0m,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            chkAssignLayer = new CheckBox
            {
                Text = "Tự động phân bổ đối tượng kết quả vào Layer mới:",
                Location = new Point(18, 88),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };
            chkAssignLayer.CheckedChanged += (s, e) => UpdateLayerUiState();

            lblLayerInside = new Label
            {
                Text = "Layer Bên Trong:",
                Location = new Point(36, 120),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular)
            };

            txtLayerInside = new TextBox
            {
                Text = "SOLID_INSIDE",
                Location = new Point(135, 117),
                Size = new Size(130, 23),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular)
            };

            lblLayerOutside = new Label
            {
                Text = "Layer Bên Ngoài:",
                Location = new Point(285, 120),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular)
            };

            txtLayerOutside = new TextBox
            {
                Text = "SOLID_OUTSIDE",
                Location = new Point(390, 117),
                Size = new Size(130, 23),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular)
            };

            grpOptions.Controls.Add(chkDeleteOriginal);
            grpOptions.Controls.Add(lblBuffer);
            grpOptions.Controls.Add(nudBuffer);
            grpOptions.Controls.Add(chkAssignLayer);
            grpOptions.Controls.Add(lblLayerInside);
            grpOptions.Controls.Add(txtLayerInside);
            grpOptions.Controls.Add(lblLayerOutside);
            grpOptions.Controls.Add(txtLayerOutside);

            // Progress & Actions
            progressBar = new ProgressBar
            {
                Location = new Point(margin, grpOptions.Bottom + 12),
                Size = new Size(clientWidth - margin * 2, 16),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Visible = false
            };

            lblProgress = new Label
            {
                Text = "",
                Location = new Point(margin, progressBar.Bottom + 4),
                Size = new Size(clientWidth - margin * 2, 18),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = Color.DarkBlue
            };

            btnExecute = new Button
            {
                Text = "THỰC HIỆN CẮT",
                Location = new Point(clientWidth - margin - 290, lblProgress.Bottom + 8),
                Size = new Size(170, 36),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnExecute.FlatAppearance.BorderSize = 0;
            btnExecute.Click += BtnExecute_Click;

            btnCancel = new Button
            {
                Text = "Đóng",
                Location = new Point(clientWidth - margin - 110, lblProgress.Bottom + 8),
                Size = new Size(105, 36),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                DialogResult = DialogResult.Cancel
            };
            btnCancel.Click += (s, e) => { this.Close(); };

            this.Controls.Add(grpSolids);
            this.Controls.Add(grpBoundary);
            this.Controls.Add(grpMode);
            this.Controls.Add(grpOptions);
            this.Controls.Add(progressBar);
            this.Controls.Add(lblProgress);
            this.Controls.Add(btnExecute);
            this.Controls.Add(btnCancel);

            this.CancelButton = btnCancel;
            this.AcceptButton = btnExecute;
            this.FormClosing += Clip3dSolidForm_FormClosing;

            UpdateLayerUiState();
        }

        private void UpdateLayerUiState()
        {
            bool enable = chkAssignLayer.Checked;
            lblLayerInside.Enabled = enable && (radInside.Checked || radSplit.Checked);
            txtLayerInside.Enabled = enable && (radInside.Checked || radSplit.Checked);

            lblLayerOutside.Enabled = enable && (radOutside.Checked || radSplit.Checked);
            txtLayerOutside.Enabled = enable && (radOutside.Checked || radSplit.Checked);
        }

        #region Event Handlers - Selection
        private void BtnPickSolids_Click(object sender, EventArgs e)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;

            using (EditorUserInteraction ui = ed.StartUserInteraction(this))
            {
                TypedValue[] tv = new TypedValue[]
                {
                    new TypedValue((int)DxfCode.Operator, "<or"),
                    new TypedValue((int)DxfCode.Start, "3DSOLID"),
                    new TypedValue((int)DxfCode.Start, "BODY"),
                    new TypedValue((int)DxfCode.Start, "*SURFACE"),
                    new TypedValue((int)DxfCode.Operator, "or>")
                };
                SelectionFilter filter = new SelectionFilter(tv);

                PromptSelectionOptions pso = new PromptSelectionOptions
                {
                    MessageForAdding = "\nChọn các đối tượng 3D Solid / Body / Surface cần cắt: "
                };

                PromptSelectionResult psr = ed.GetSelection(pso, filter);
                if (psr.Status == PromptStatus.OK && psr.Value != null && psr.Value.Count > 0)
                {
                    SelectedSolidIds.Clear();
                    foreach (SelectedObject so in psr.Value)
                    {
                        if (so != null && !SelectedSolidIds.Contains(so.ObjectId))
                        {
                            SelectedSolidIds.Add(so.ObjectId);
                        }
                    }
                    UpdateSolidStatusLabel();
                }
            }
        }

        private void BtnSelectAllSolids_Click(object sender, EventArgs e)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            SelectedSolidIds.Clear();

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead);

                RXClass solidClass = RXClass.GetClass(typeof(Solid3d));
                RXClass bodyClass = RXClass.GetClass(typeof(Body));
                RXClass surfClass = RXClass.GetClass(typeof(Autodesk.AutoCAD.DatabaseServices.Surface));

                foreach (ObjectId id in btr)
                {
                    if (id.IsNull || id.IsErased) continue;
                    if (id.ObjectClass.IsDerivedFrom(solidClass) ||
                        id.ObjectClass.IsDerivedFrom(bodyClass) ||
                        id.ObjectClass.IsDerivedFrom(surfClass))
                    {
                        SelectedSolidIds.Add(id);
                    }
                }
                tr.Commit();
            }

            UpdateSolidStatusLabel();
        }

        private void BtnPickBoundary_Click(object sender, EventArgs e)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            using (EditorUserInteraction ui = ed.StartUserInteraction(this))
            {
                PromptSelectionOptions pso = new PromptSelectionOptions
                {
                    MessageForAdding = "\nQuét chọn một hoặc nhiều Polyline / Circle / Region khép kín làm ranh giới: "
                };

                TypedValue[] tvs = new TypedValue[]
                {
                    new TypedValue((int)DxfCode.Operator, "<or"),
                    new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),
                    new TypedValue((int)DxfCode.Start, "POLYLINE"),
                    new TypedValue((int)DxfCode.Start, "CIRCLE"),
                    new TypedValue((int)DxfCode.Start, "ELLIPSE"),
                    new TypedValue((int)DxfCode.Start, "REGION"),
                    new TypedValue((int)DxfCode.Operator, "or>")
                };
                SelectionFilter filter = new SelectionFilter(tvs);

                PromptSelectionResult psr = ed.GetSelection(pso, filter);
                if (psr.Status == PromptStatus.OK && psr.Value != null && psr.Value.Count > 0)
                {
                    SelectedBoundaryIds.Clear();
                    int unclosedCount = 0;

                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        foreach (SelectedObject so in psr.Value)
                        {
                            if (so == null || so.ObjectId.IsNull) continue;

                            Entity ent = tr.GetObject(so.ObjectId, OpenMode.ForRead) as Entity;
                            if (ent == null) continue;

                            bool isClosed = false;
                            if (ent is Polyline pl)
                            {
                                isClosed = pl.Closed || pl.StartPoint.DistanceTo(pl.EndPoint) < 1e-4;
                            }
                            else if (ent is Polyline2d p2d)
                            {
                                isClosed = p2d.Closed;
                            }
                            else if (ent is Polyline3d p3d)
                            {
                                isClosed = p3d.Closed;
                            }
                            else if (ent is Circle || ent is Autodesk.AutoCAD.DatabaseServices.Region)
                            {
                                isClosed = true;
                            }
                            else if (ent is Ellipse el)
                            {
                                isClosed = el.Closed;
                            }

                            if (isClosed)
                            {
                                if (!SelectedBoundaryIds.Contains(so.ObjectId))
                                    SelectedBoundaryIds.Add(so.ObjectId);
                            }
                            else
                            {
                                unclosedCount++;
                            }
                        }
                        tr.Commit();
                    }

                    if (SelectedBoundaryIds.Count > 0)
                    {
                        lblBoundaryStatus.Text = $"Đã chọn: {SelectedBoundaryIds.Count} đường ranh giới khép kín." +
                            (unclosedCount > 0 ? $" (Bỏ qua {unclosedCount} đối tượng hở)" : "");
                        lblBoundaryStatus.ForeColor = Color.DarkGreen;
                    }
                    else
                    {
                        lblBoundaryStatus.Text = "Không có đường khép kín nào hợp lệ được chọn.";
                        lblBoundaryStatus.ForeColor = Color.Red;
                    }
                }
            }
        }

        private void UpdateSolidStatusLabel()
        {
            if (SelectedSolidIds.Count > 0)
            {
                int solidCount = 0;
                int bodyCount = 0;
                int surfCount = 0;

                RXClass solidClass = RXClass.GetClass(typeof(Solid3d));
                RXClass bodyClass = RXClass.GetClass(typeof(Body));
                RXClass surfClass = RXClass.GetClass(typeof(Autodesk.AutoCAD.DatabaseServices.Surface));

                foreach (ObjectId id in SelectedSolidIds)
                {
                    if (id.IsNull || id.IsErased) continue;
                    if (id.ObjectClass.IsDerivedFrom(solidClass)) solidCount++;
                    else if (id.ObjectClass.IsDerivedFrom(bodyClass)) bodyCount++;
                    else if (id.ObjectClass.IsDerivedFrom(surfClass)) surfCount++;
                }

                List<string> parts = new List<string>();
                if (solidCount > 0) parts.Add($"{solidCount} Solid");
                if (bodyCount > 0) parts.Add($"{bodyCount} Body");
                if (surfCount > 0) parts.Add($"{surfCount} Surface");

                string detail = parts.Count > 0 ? $" ({string.Join(", ", parts)})" : "";
                lblSolidStatus.Text = $"Đã chọn: {SelectedSolidIds.Count} đối tượng{detail}.";
                lblSolidStatus.ForeColor = Color.DarkGreen;
            }
            else
            {
                lblSolidStatus.Text = "Chưa chọn đối tượng nào.";
                lblSolidStatus.ForeColor = Color.DarkSlateGray;
            }
        }
        #endregion

        #region Execution & Validation
        private void BtnExecute_Click(object sender, EventArgs e)
        {
            // Kiểm tra tính hợp lệ
            if (SelectedSolidIds == null || SelectedSolidIds.Count == 0)
            {
                MessageBox.Show(this, "Vui lòng chọn ít nhất một đối tượng 3D Solid hoặc Body cần cắt!",
                    "Chưa chọn đối tượng", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (SelectedBoundaryIds == null || SelectedBoundaryIds.Count == 0)
            {
                MessageBox.Show(this, "Vui lòng chọn ít nhất một Polyline khép kín làm ranh giới cắt!",
                    "Chưa chọn ranh giới", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Lưu các thuộc tính cấu hình
            if (radInside.Checked) ClipMode = ClipSolidMode.KeepInside;
            else if (radOutside.Checked) ClipMode = ClipSolidMode.KeepOutside;
            else ClipMode = ClipSolidMode.SplitBoth;

            DeleteOriginal = chkDeleteOriginal.Checked;
            AssignLayer = chkAssignLayer.Checked;
            LayerInside = string.IsNullOrWhiteSpace(txtLayerInside.Text) ? "SOLID_INSIDE" : txtLayerInside.Text.Trim();
            LayerOutside = string.IsNullOrWhiteSpace(txtLayerOutside.Text) ? "SOLID_OUTSIDE" : txtLayerOutside.Text.Trim();
            BufferHeight = (double)nudBuffer.Value;

            SaveCurrentSettings();
            IsConfirmed = true;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
        #endregion

        #region Settings Persistence
        private void RestoreLastSettings()
        {
            try
            {
                // Khôi phục chế độ
                radInside.Checked = (_lastMode == ClipSolidMode.KeepInside);
                radOutside.Checked = (_lastMode == ClipSolidMode.KeepOutside);
                radSplit.Checked = (_lastMode == ClipSolidMode.SplitBoth);

                chkDeleteOriginal.Checked = _lastDeleteOriginal;
                chkAssignLayer.Checked = _lastAssignLayer;
                txtLayerInside.Text = _lastLayerInside;
                txtLayerOutside.Text = _lastLayerOutside;

                if (_lastBuffer >= nudBuffer.Minimum && _lastBuffer <= nudBuffer.Maximum)
                    nudBuffer.Value = _lastBuffer;

                // Khôi phục đối tượng đã chọn nếu còn hợp lệ
                Document doc = Application.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    Database db = doc.Database;
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        // Kiểm tra solids
                        SelectedSolidIds.Clear();
                        if (_lastSolidIds != null && _lastSolidIds.Count > 0)
                        {
                            foreach (ObjectId id in _lastSolidIds)
                            {
                                if (!id.IsNull && id.IsValid && !id.IsErased)
                                {
                                    SelectedSolidIds.Add(id);
                                }
                            }
                        }
                        UpdateSolidStatusLabel();

                        // Kiểm tra boundaries
                        SelectedBoundaryIds.Clear();
                        if (_lastBoundaryIds != null && _lastBoundaryIds.Count > 0)
                        {
                            foreach (ObjectId bId in _lastBoundaryIds)
                            {
                                if (!bId.IsNull && bId.IsValid && !bId.IsErased)
                                {
                                    SelectedBoundaryIds.Add(bId);
                                }
                            }
                        }
                        else if (!_lastBoundaryId.IsNull && _lastBoundaryId.IsValid && !_lastBoundaryId.IsErased)
                        {
                            SelectedBoundaryIds.Add(_lastBoundaryId);
                        }

                        if (SelectedBoundaryIds.Count > 0)
                        {
                            lblBoundaryStatus.Text = $"Đã nạp lại {SelectedBoundaryIds.Count} đường ranh giới trước.";
                            lblBoundaryStatus.ForeColor = Color.DarkGreen;
                        }
                        tr.Commit();
                    }
                }
            }
            catch { }
        }

        private void SaveCurrentSettings()
        {
            try
            {
                _lastSolidIds = new List<ObjectId>(SelectedSolidIds);
                _lastBoundaryIds = new List<ObjectId>(SelectedBoundaryIds);
                _lastBoundaryId = BoundaryId;
                if (radInside.Checked) _lastMode = ClipSolidMode.KeepInside;
                else if (radOutside.Checked) _lastMode = ClipSolidMode.KeepOutside;
                else _lastMode = ClipSolidMode.SplitBoth;

                _lastDeleteOriginal = chkDeleteOriginal.Checked;
                _lastAssignLayer = chkAssignLayer.Checked;
                _lastLayerInside = txtLayerInside.Text.Trim();
                _lastLayerOutside = txtLayerOutside.Text.Trim();
                _lastBuffer = nudBuffer.Value;
                _lastFormSize = this.Size;
            }
            catch { }
        }

        private void Clip3dSolidForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveCurrentSettings();
        }
        #endregion
    }
}

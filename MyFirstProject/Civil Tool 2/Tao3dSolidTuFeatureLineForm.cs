using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using CadEntity = Autodesk.AutoCAD.DatabaseServices.Entity;
using CadRegion = Autodesk.AutoCAD.DatabaseServices.Region;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsFont = System.Drawing.Font;
using WinFormsColor = System.Drawing.Color;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace MyFirstProject.Civil_Tool_2
{
    public class SelectedFeatureLineItem
    {
        public ObjectId Id { get; set; }
        public string Handle { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public int VertexCount { get; set; }
        public double Length { get; set; }
        public bool IsClosed { get; set; }
        public double MinZ { get; set; }
        public double MaxZ { get; set; }
        public string SourceLayer { get; set; } = string.Empty;
        public List<Point3d> Points { get; set; } = new List<Point3d>();
    }

    public class Tao3dSolidTuFeatureLineForm : Form
    {
        // ═══════════════════════════════════════════════════════════════
        //  PERSISTENT STATE (RULE 2)
        // ═══════════════════════════════════════════════════════════════
        private static double _lastThickness = 0.20;
        private static bool _lastDirectionDown = true;
        private static bool _lastAutoClose = true;
        private static bool _lastDeleteSource = false;
        private static double _lastCurveSampling = 0.50;
        private static string _lastLayer = "[Layer của đối tượng nguồn]";
        private static Size _lastFormSize = new Size(780, 640);
        private static List<ObjectId> _lastSelectedIds = new List<ObjectId>();

        // ═══════════════════════════════════════════════════════════════
        //  FIELDS
        // ═══════════════════════════════════════════════════════════════
        private readonly Database _db;
        private readonly Editor _ed;
        private readonly List<SelectedFeatureLineItem> _items = new List<SelectedFeatureLineItem>();

        // Controls
        private ListView lvObjects = null!;
        private WinFormsLabel lblSummary = null!;
        private WinFormsLabel lblStatus = null!;
        private NumericUpDown numThickness = null!;
        private RadioButton radDown = null!;
        private RadioButton radUp = null!;
        private CheckBox chkAutoClose = null!;
        private CheckBox chkDeleteSource = null!;
        private NumericUpDown numCurveSampling = null!;
        private ComboBox cboLayer = null!;
        private ProgressBar progressBar = null!;
        private Panel pnlBottom = null!;
        private Button btnSelect = null!;
        private Button btnExecute = null!;
        private Button btnClose = null!;

        public Tao3dSolidTuFeatureLineForm(Database db, Editor ed)
        {
            _db = db;
            _ed = ed;

            InitializeCustomUI();
            RestoreLastSettings();
            LoadDrawingLayers();
            TryRestorePreviousSelection();
        }

        private void InitializeCustomUI()
        {
            this.Text = "Tạo 3D Solid Từ Feature Line / Polyline 3D - Civil 3D Tools";
            this.Size = _lastFormSize;
            this.MinimumSize = new Size(700, 560);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.BackColor = WinFormsColor.FromArgb(245, 247, 250);

            // 1. Header Panel
            Panel pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 62,
                BackColor = WinFormsColor.FromArgb(24, 43, 73)
            };

            WinFormsLabel lblTitle = new WinFormsLabel
            {
                Text = "TẠO 3D SOLID TỪ FEATURE LINE / POLYLINE 3D",
                Font = new WinFormsFont("Segoe UI", 11.5F, FontStyle.Bold),
                ForeColor = WinFormsColor.White,
                Location = new Point(16, 10),
                AutoSize = true
            };

            WinFormsLabel lblSubTitle = new WinFormsLabel
            {
                Text = "Tạo khối 3D Solid dạng tấm theo địa hình 3D từ đường bao Feature Line với độ dày tự chọn",
                Font = new WinFormsFont("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = WinFormsColor.FromArgb(200, 215, 235),
                Location = new Point(17, 34),
                AutoSize = true
            };

            pnlHeader.Controls.AddRange(new Control[] { lblTitle, lblSubTitle });

            // 2. GroupBox: Nguồn dữ liệu (Source Objects)
            GroupBox grpSource = new GroupBox
            {
                Text = "1. Danh sách Feature Line / Polyline đã chọn",
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = WinFormsColor.FromArgb(30, 41, 59),
                Location = new Point(14, 72),
                Size = new Size(736, 235),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            btnSelect = new Button
            {
                Text = "Chọn Feature Line / Polyline trên bản vẽ (1 hoặc nhiều)",
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold),
                BackColor = WinFormsColor.FromArgb(224, 238, 255),
                ForeColor = WinFormsColor.FromArgb(12, 74, 156),
                FlatStyle = FlatStyle.Flat,
                Location = new Point(14, 25),
                Size = new Size(420, 32),
                Cursor = Cursors.Hand
            };
            btnSelect.FlatAppearance.BorderColor = WinFormsColor.FromArgb(147, 197, 253);
            btnSelect.Click += BtnSelect_Click;

            Button btnSelectAll = new Button
            {
                Text = "Chọn hết",
                Font = new WinFormsFont("Segoe UI", 8.5F),
                Location = new Point(442, 26),
                Size = new Size(80, 30),
                UseVisualStyleBackColor = true
            };
            btnSelectAll.Click += (s, e) => { foreach (ListViewItem itm in lvObjects.Items) itm.Checked = true; };

            Button btnUncheckAll = new Button
            {
                Text = "Bỏ chọn",
                Font = new WinFormsFont("Segoe UI", 8.5F),
                Location = new Point(528, 26),
                Size = new Size(80, 30),
                UseVisualStyleBackColor = true
            };
            btnUncheckAll.Click += (s, e) => { foreach (ListViewItem itm in lvObjects.Items) itm.Checked = false; };

            Button btnClear = new Button
            {
                Text = "Xóa danh sách",
                Font = new WinFormsFont("Segoe UI", 8.5F),
                Location = new Point(614, 26),
                Size = new Size(105, 30),
                UseVisualStyleBackColor = true
            };
            btnClear.Click += (s, e) => { _items.Clear(); RefreshListView(); };

            lvObjects = new ListView
            {
                View = View.Details,
                CheckBoxes = true,
                FullRowSelect = true,
                GridLines = true,
                Font = new WinFormsFont("Segoe UI", 8.5F, FontStyle.Regular),
                Location = new Point(14, 65),
                Size = new Size(708, 135),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            lvObjects.Columns.Add("STT", 45);
            lvObjects.Columns.Add("Loại đối tượng", 130);
            lvObjects.Columns.Add("Handle", 75);
            lvObjects.Columns.Add("Số đỉnh", 65);
            lvObjects.Columns.Add("Chu vi / Dài (m)", 110);
            lvObjects.Columns.Add("Khép kín", 90);
            lvObjects.Columns.Add("Cao độ Z min - max (m)", 175);

            lblSummary = new WinFormsLabel
            {
                Text = "Chưa có đối tượng nào được chọn.",
                Font = new WinFormsFont("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = WinFormsColor.FromArgb(71, 85, 105),
                Location = new Point(14, 206),
                AutoSize = true
            };

            grpSource.Controls.AddRange(new Control[] { btnSelect, btnSelectAll, btnUncheckAll, btnClear, lvObjects, lblSummary });

            // 3. GroupBox: Thiết lập Độ dày & Phương đùn
            GroupBox grpGeometry = new GroupBox
            {
                Text = "2. Thiết lập Độ dày & Phương đùn",
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = WinFormsColor.FromArgb(30, 41, 59),
                Location = new Point(14, 314),
                Size = new Size(360, 195),
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };

            WinFormsLabel lblThick = new WinFormsLabel
            {
                Text = "Độ dày Solid T (m):",
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(16, 28),
                AutoSize = true
            };

            numThickness = new NumericUpDown
            {
                DecimalPlaces = 3,
                Minimum = 0.001M,
                Maximum = 9999.000M,
                Increment = 0.05M,
                Value = 0.200M,
                Font = new WinFormsFont("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = WinFormsColor.FromArgb(15, 23, 42),
                Location = new Point(160, 26),
                Size = new Size(120, 25)
            };

            WinFormsLabel lblDirection = new WinFormsLabel
            {
                Text = "Hướng đùn:",
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Regular),
                Location = new Point(16, 62),
                AutoSize = true
            };

            radDown = new RadioButton
            {
                Text = "Đùn xuống dưới (-Z / Hạ đáy)",
                Font = new WinFormsFont("Segoe UI", 8.5F, FontStyle.Bold),
                Checked = true,
                Location = new Point(30, 85),
                AutoSize = true
            };

            radUp = new RadioButton
            {
                Text = "Đùn lên trên (+Z / Nâng đỉnh)",
                Font = new WinFormsFont("Segoe UI", 8.5F),
                Location = new Point(30, 110),
                AutoSize = true
            };

            chkAutoClose = new CheckBox
            {
                Text = "Tự động khép kín nếu đường chưa đóng",
                Font = new WinFormsFont("Segoe UI", 8.5F),
                Checked = true,
                Location = new Point(16, 138),
                AutoSize = true
            };
            chkAutoClose.CheckedChanged += (s, e) => { RecomputeItemsGeometry(); };

            WinFormsLabel lblCurveStep = new WinFormsLabel
            {
                Text = "Chia cung cong (m):",
                Font = new WinFormsFont("Segoe UI", 8.5F),
                Location = new Point(16, 166),
                AutoSize = true
            };

            numCurveSampling = new NumericUpDown
            {
                DecimalPlaces = 2,
                Minimum = 0.05M,
                Maximum = 50.00M,
                Increment = 0.10M,
                Value = 0.50M,
                Font = new WinFormsFont("Segoe UI", 8.5F),
                Location = new Point(160, 163),
                Size = new Size(80, 23)
            };
            numCurveSampling.ValueChanged += (s, e) => { RecomputeItemsGeometry(); };

            grpGeometry.Controls.AddRange(new Control[] {
                lblThick, numThickness, lblDirection, radDown, radUp, chkAutoClose, lblCurveStep, numCurveSampling
            });

            // 4. GroupBox: Tùy chọn xuất & Quản lý
            GroupBox grpOutput = new GroupBox
            {
                Text = "3. Tùy chọn xuất & Quản lý",
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = WinFormsColor.FromArgb(30, 41, 59),
                Location = new Point(386, 314),
                Size = new Size(364, 195),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            WinFormsLabel lblLayer = new WinFormsLabel
            {
                Text = "Layer đích:",
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Regular),
                Location = new Point(16, 28),
                AutoSize = true
            };

            cboLayer = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new WinFormsFont("Segoe UI", 9F),
                Location = new Point(16, 52),
                Size = new Size(328, 25),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            chkDeleteSource = new CheckBox
            {
                Text = "Xóa Feature Line / Polyline gốc sau khi tạo 3D Solid",
                Font = new WinFormsFont("Segoe UI", 8.5F),
                Checked = false,
                ForeColor = WinFormsColor.FromArgb(153, 27, 27),
                Location = new Point(16, 95),
                AutoSize = true
            };

            WinFormsLabel lblHelp = new WinFormsLabel
            {
                Text = "- Lưu ý:\n- Feature Line có thể lồi lõm 3D tự do (từng đỉnh Z khác nhau).\n- Khối 3D Solid tạo ra sẽ ôm trọn cao độ 3D của đường bao.\n- Dễ dàng tính thể tích, xuất IFC hoặc Property Set.",
                Font = new WinFormsFont("Segoe UI", 8F, FontStyle.Italic),
                ForeColor = WinFormsColor.FromArgb(100, 116, 139),
                Location = new Point(16, 125),
                Size = new Size(330, 60),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            grpOutput.Controls.AddRange(new Control[] { lblLayer, cboLayer, chkDeleteSource, lblHelp });

            // 5. Bottom Panel (Actions & Progress)
            pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 65,
                BackColor = WinFormsColor.FromArgb(238, 242, 246)
            };

            progressBar = new ProgressBar
            {
                Location = new Point(14, 6),
                Size = new Size(400, 12),
                Visible = false
            };

            lblStatus = new WinFormsLabel
            {
                Text = "Sẵn sàng.",
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = WinFormsColor.FromArgb(51, 65, 85),
                Location = new Point(14, 22),
                AutoSize = true
            };

            btnExecute = new Button
            {
                Text = "Tạo 3D Solid",
                Font = new WinFormsFont("Segoe UI", 9.5F, FontStyle.Bold),
                BackColor = WinFormsColor.FromArgb(22, 101, 192),
                ForeColor = WinFormsColor.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(150, 38),
                Cursor = Cursors.Hand
            };
            btnExecute.FlatAppearance.BorderSize = 0;
            btnExecute.Click += BtnExecute_Click;

            btnClose = new Button
            {
                Text = "Đóng",
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Regular),
                BackColor = WinFormsColor.FromArgb(226, 232, 240),
                ForeColor = WinFormsColor.FromArgb(30, 41, 59),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(100, 38),
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderColor = WinFormsColor.FromArgb(203, 213, 225);
            btnClose.Click += (s, e) => this.Close();

            pnlBottom.Controls.AddRange(new Control[] { progressBar, lblStatus, btnExecute, btnClose });
            pnlBottom.Resize += (s, e) => UpdateBottomLayout();

            // Add all main panels
            this.Controls.AddRange(new Control[] { grpOutput, grpGeometry, grpSource, pnlHeader, pnlBottom });
            pnlBottom.BringToFront();

            this.Load += (s, e) => UpdateBottomLayout();
            this.Resize += (s, e) => UpdateBottomLayout();
            this.FormClosing += Tao3dSolidTuFeatureLineForm_FormClosing;
        }

        private void UpdateBottomLayout()
        {
            if (pnlBottom == null || btnClose == null || btnExecute == null) return;

            int padRight = 16;
            int rightEdge = pnlBottom.ClientSize.Width - padRight;
            int btnY = (pnlBottom.ClientSize.Height - btnExecute.Height) / 2;

            btnClose.Location = new Point(rightEdge - btnClose.Width, btnY);
            btnExecute.Location = new Point(btnClose.Left - btnExecute.Width - 12, btnY);

            if (lblStatus != null)
            {
                lblStatus.Location = new Point(14, (pnlBottom.ClientSize.Height - lblStatus.Height) / 2);
                lblStatus.MaximumSize = new Size(Math.Max(50, btnExecute.Left - 28), 0);
            }

            if (progressBar != null)
            {
                progressBar.Location = new Point(14, 6);
                progressBar.Width = Math.Max(100, btnExecute.Left - 28);
            }
        }

        #region PERSISTENT STATE LOGIC (RULE 2)

        private void RestoreLastSettings()
        {
            try
            {
                if (_lastThickness > 0) numThickness.Value = (decimal)_lastThickness;
                radDown.Checked = _lastDirectionDown;
                radUp.Checked = !_lastDirectionDown;
                chkAutoClose.Checked = _lastAutoClose;
                chkDeleteSource.Checked = _lastDeleteSource;
                if (_lastCurveSampling > 0) numCurveSampling.Value = (decimal)_lastCurveSampling;

                if (_lastFormSize.Width >= 600 && _lastFormSize.Height >= 500)
                {
                    this.Size = _lastFormSize;
                }
            }
            catch { }
        }

        private void SaveCurrentSettings()
        {
            try
            {
                _lastThickness = (double)numThickness.Value;
                _lastDirectionDown = radDown.Checked;
                _lastAutoClose = chkAutoClose.Checked;
                _lastDeleteSource = chkDeleteSource.Checked;
                _lastCurveSampling = (double)numCurveSampling.Value;
                if (cboLayer.SelectedItem != null)
                {
                    _lastLayer = cboLayer.SelectedItem.ToString();
                }
                _lastFormSize = this.Size;
                _lastSelectedIds = _items.Select(i => i.Id).ToList();
            }
            catch { }
        }

        private void Tao3dSolidTuFeatureLineForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveCurrentSettings();
        }

        #endregion

        #region DATA & LAYER HANDLING

        private void LoadDrawingLayers()
        {
            cboLayer.Items.Clear();
            cboLayer.Items.Add("[Layer của đối tượng nguồn]");
            cboLayer.Items.Add("[Layer hiện hành]");

            try
            {
                using (Transaction tr = _db.TransactionManager.StartTransaction())
                {
                    LayerTable lt = tr.GetObject(_db.LayerTableId, OpenMode.ForRead) as LayerTable;
                    if (lt != null)
                    {
                        List<string> layerNames = new List<string>();
                        foreach (ObjectId lId in lt)
                        {
                            LayerTableRecord ltr = tr.GetObject(lId, OpenMode.ForRead) as LayerTableRecord;
                            if (ltr != null) layerNames.Add(ltr.Name);
                        }
                        layerNames.Sort();
                        foreach (string name in layerNames) cboLayer.Items.Add(name);
                    }
                    tr.Commit();
                }
            }
            catch { }

            // Restore last selected layer
            int idx = cboLayer.FindStringExact(_lastLayer);
            cboLayer.SelectedIndex = idx >= 0 ? idx : 0;
        }

        private void TryRestorePreviousSelection()
        {
            if (_lastSelectedIds == null || _lastSelectedIds.Count == 0) return;

            List<ObjectId> validIds = new List<ObjectId>();
            using (Transaction tr = _db.TransactionManager.StartTransaction())
            {
                foreach (ObjectId id in _lastSelectedIds)
                {
                    if (!id.IsNull && id.IsValid && !id.IsErased)
                    {
                        validIds.Add(id);
                    }
                }
                tr.Commit();
            }

            if (validIds.Count > 0)
            {
                LoadEntitiesData(validIds);
            }
        }

        private void BtnSelect_Click(object sender, EventArgs e)
        {
            List<ObjectId> pickedIds = new List<ObjectId>();

            using (var interaction = _ed.StartUserInteraction(this))
            {
                PromptSelectionOptions pso = new PromptSelectionOptions
                {
                    MessageForAdding = "\nChọn Feature Line hoặc Polyline (kéo vùng hoặc chọn từng đối tượng): "
                };

                PromptSelectionResult psr = _ed.GetSelection(pso);
                if (psr.Status == PromptStatus.OK && psr.Value != null)
                {
                    using (Transaction tr = _db.TransactionManager.StartTransaction())
                    {
                        foreach (ObjectId id in psr.Value.GetObjectIds())
                        {
                            var ent = tr.GetObject(id, OpenMode.ForRead) as CadEntity;
                            if (ent is FeatureLine || ent is Polyline || ent is Polyline3d)
                            {
                                pickedIds.Add(id);
                            }
                        }
                        tr.Commit();
                    }
                }
            }

            if (pickedIds.Count > 0)
            {
                LoadEntitiesData(pickedIds);
            }
            else
            {
                _ed.WriteMessage("\nKhông có Feature Line hoặc Polyline nào được chọn.");
            }
        }

        private void LoadEntitiesData(List<ObjectId> ids)
        {
            _items.Clear();
            double sampling = (double)numCurveSampling.Value;
            bool autoClose = chkAutoClose.Checked;

            using (Transaction tr = _db.TransactionManager.StartTransaction())
            {
                foreach (ObjectId id in ids)
                {
                    try
                    {
                        var ent = tr.GetObject(id, OpenMode.ForRead) as CadEntity;
                        if (ent == null) continue;

                        var pts = CT_Tao_3d_Solid_Tu_Feature_Line_Command.ExtractBoundaryPoints(
                            id, tr, sampling, autoClose,
                            out string entType, out double length, out bool isClosed, out double minZ, out double maxZ);

                        if (pts.Count >= 3)
                        {
                            _items.Add(new SelectedFeatureLineItem
                            {
                                Id = id,
                                Handle = ent.Handle.ToString(),
                                TypeName = entType,
                                VertexCount = pts.Count,
                                Length = length,
                                IsClosed = isClosed,
                                MinZ = minZ,
                                MaxZ = maxZ,
                                SourceLayer = ent.Layer,
                                Points = pts
                            });
                        }
                    }
                    catch { }
                }
                tr.Commit();
            }

            RefreshListView();
        }

        private void RecomputeItemsGeometry()
        {
            if (_items.Count == 0) return;
            double sampling = (double)numCurveSampling.Value;
            bool autoClose = chkAutoClose.Checked;

            using (Transaction tr = _db.TransactionManager.StartTransaction())
            {
                foreach (var item in _items)
                {
                    try
                    {
                        var pts = CT_Tao_3d_Solid_Tu_Feature_Line_Command.ExtractBoundaryPoints(
                            item.Id, tr, sampling, autoClose,
                            out string entType, out double length, out bool isClosed, out double minZ, out double maxZ);

                        item.Points = pts;
                        item.VertexCount = pts.Count;
                        item.Length = length;
                        item.IsClosed = isClosed;
                        item.MinZ = minZ;
                        item.MaxZ = maxZ;
                    }
                    catch { }
                }
                tr.Commit();
            }

            RefreshListView();
        }

        private void RefreshListView()
        {
            lvObjects.BeginUpdate();
            lvObjects.Items.Clear();

            int stt = 1;
            int validCount = 0;

            foreach (var item in _items)
            {
                ListViewItem lvi = new ListViewItem(stt.ToString());
                lvi.SubItems.Add(item.TypeName);
                lvi.SubItems.Add(item.Handle);
                lvi.SubItems.Add(item.VertexCount.ToString());
                lvi.SubItems.Add(item.Length.ToString("F2"));
                lvi.SubItems.Add(item.IsClosed ? "Khép kín" : "Chưa kín");
                lvi.SubItems.Add($"{item.MinZ:F2} m ~ {item.MaxZ:F2} m");

                lvi.Tag = item;
                lvi.Checked = item.IsClosed;

                if (!item.IsClosed)
                {
                    lvi.ForeColor = WinFormsColor.FromArgb(180, 83, 9);
                }
                else
                {
                    validCount++;
                }

                lvObjects.Items.Add(lvi);
                stt++;
            }

            lvObjects.EndUpdate();

            lblSummary.Text = $"Đã nạp: {_items.Count} đối tượng | Hợp lệ để tạo 3D Solid: {validCount}";
            lblStatus.Text = _items.Count > 0 ? "Sẵn sàng tạo 3D Solid." : "Vui lòng chọn Feature Line trên bản vẽ.";
        }

        #endregion

        #region EXECUTION LOGIC

        private void BtnExecute_Click(object sender, EventArgs e)
        {
            var checkedItems = lvObjects.CheckedItems.Cast<ListViewItem>()
                .Select(itm => itm.Tag as SelectedFeatureLineItem)
                .Where(itm => itm != null && itm.Points.Count >= 3)
                .ToList();

            if (checkedItems.Count == 0)
            {
                MessageBox.Show(
                    "Vui lòng chọn ít nhất 1 Feature Line khép kín (có đánh dấu tích) để tạo 3D Solid.",
                    "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            double thickness = (double)numThickness.Value;
            bool isDown = radDown.Checked;
            bool deleteSource = chkDeleteSource.Checked;
            string targetLayerSetting = cboLayer.SelectedItem?.ToString() ?? "[Layer của đối tượng nguồn]";

            btnExecute.Enabled = false;
            btnClose.Enabled = false;
            btnSelect.Enabled = false;
            progressBar.Visible = true;
            progressBar.Minimum = 0;
            progressBar.Maximum = checkedItems.Count;
            progressBar.Value = 0;

            int successCount = 0;
            int failedCount = 0;
            List<ObjectId> createdSolidIds = new List<ObjectId>();

            using (DocumentLock docLock = Application.DocumentManager.MdiActiveDocument.LockDocument())
            {
                using (Transaction tr = _db.TransactionManager.StartTransaction())
                {
                    BlockTable bt = tr.GetObject(_db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord btr = tr.GetObject(_db.CurrentSpaceId, OpenMode.ForWrite) as BlockTableRecord;

                    foreach (var item in checkedItems)
                    {
                        try
                        {
                            lblStatus.Text = $"Đang tạo 3D Solid cho [{item.TypeName} - #{item.Handle}]...";
                            System.Windows.Forms.Application.DoEvents();

                            // Tạo khối Solid3d
                            Solid3d solid = CT_Tao_3d_Solid_Tu_Feature_Line_Command.CreateSolidFromPolygon(
                                item.Points, thickness, isDown);

                            if (solid != null)
                            {
                                // Quyết định Layer
                                string finalLayer = item.SourceLayer;
                                if (targetLayerSetting == "[Layer hiện hành]")
                                {
                                    finalLayer = _db.Clayer != ObjectId.Null ?
                                        ((LayerTableRecord)tr.GetObject(_db.Clayer, OpenMode.ForRead)).Name : item.SourceLayer;
                                }
                                else if (!targetLayerSetting.StartsWith("["))
                                {
                                    finalLayer = targetLayerSetting;
                                }

                                solid.Layer = finalLayer;
                                solid.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(ColorMethod.ByLayer, 256);

                                ObjectId solidId = btr.AppendEntity(solid);
                                tr.AddNewlyCreatedDBObject(solid, true);
                                createdSolidIds.Add(solidId);

                                // Xóa đối tượng nguồn nếu người dùng chọn
                                if (deleteSource)
                                {
                                    CadEntity sourceEnt = tr.GetObject(item.Id, OpenMode.ForWrite) as CadEntity;
                                    if (sourceEnt != null && !sourceEnt.IsErased)
                                    {
                                        sourceEnt.Erase(true);
                                    }
                                }

                                successCount++;
                            }
                            else
                            {
                                failedCount++;
                            }
                        }
                        catch (System.Exception ex)
                        {
                            failedCount++;
                            _ed.WriteMessage($"\n[Lỗi #{item.Handle}]: {ex.Message}");
                        }

                        progressBar.Value = Math.Min(progressBar.Maximum, progressBar.Value + 1);
                    }

                    tr.Commit();
                }
            }

            progressBar.Visible = false;
            btnExecute.Enabled = true;
            btnClose.Enabled = true;
            btnSelect.Enabled = true;

            SaveCurrentSettings();

            string resultMsg = $"Hoàn thành tạo 3D Solid!\n- Thành công: {successCount} khối Solid\n- Thất bại: {failedCount}";
            lblStatus.Text = $"Tạo thành công {successCount} 3D Solid.";
            _ed.WriteMessage($"\n[KẾT QUẢ] {resultMsg}\n");

            MessageBox.Show(resultMsg, "Kết quả tạo 3D Solid", MessageBoxButtons.OK,
                successCount > 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

            if (successCount > 0)
            {
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        }

        #endregion
    }
}

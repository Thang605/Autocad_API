using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using LabelGroup = Autodesk.Civil.DatabaseServices.LabelGroup;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using DrawingFont = System.Drawing.Font;

namespace Civil3DCsharp
{
    /// <summary>
    /// Form UI cho lệnh CTP_Dich_Chuyen_Label_Chan
    /// Hiệu chỉnh grip / điểm đặt của các label trong Band Label Group (không nổ/explode)
    /// Hỗ trợ chọn nhóm cách nhau theo số tùy chọn (chu kỳ 2, 3 hoặc N đối tượng)
    /// </summary>
    public class DichChuyenLabelChanTrongBandForm : Form
    {
        // =================== PERSISTENT STATE ===================
        private static double _lastOffsetY = 2.0;
        private static double _lastOffsetX = 0.0;
        private static bool _lastOffsetPositive = true; // true = Lên (+Y), false = Xuống (-Y)
        private static int _lastStep = 2; // Bước lặp: 2, 3 hoặc N tùy chọn
        private static int _lastCustomStep = 4; // Giá trị nhớ cho ô tùy chọn N
        private static Dictionary<int, int> _lastPatternByStep = new Dictionary<int, int>()
        {
            { 2, 0 },
            { 3, 0 }
        };
        private static Size _lastFormSize = Size.Empty;
        private static List<ObjectId> _lastSelectedIds = new List<ObjectId>();

        // =================== CONTROLS ===================
        private GroupBox grpInfo;
        private Label lblSelectedCount;
        private Label lblSubEntityCount;
        private Button btnPick;
        private Button btnReset;

        private GroupBox grpTarget;
        private Label lblStep;
        private RadioButton rdoStep2;
        private RadioButton rdoStep3;
        private RadioButton rdoStepCustom;
        private NumericUpDown numCustomStep;
        private Label lblCustomUnit;
        private Label lblPattern;
        private ComboBox cboPattern;

        private GroupBox grpOffset;
        private Label lblOffsetY;
        private NumericUpDown numOffsetY;
        private Label lblUnitY;
        private RadioButton rdoUp;
        private RadioButton rdoDown;
        private Label lblOffsetX;
        private NumericUpDown numOffsetX;
        private Label lblUnitX;

        private GroupBox grpPreview;
        private Label lblPreview;

        private Panel pnlButtons;
        private Button btnOK;
        private Button btnCancel;

        // =================== DATA & SERVICES ===================
        private Editor _ed;
        private Database _db;
        private List<ObjectId> _selectedIds = new List<ObjectId>();
        private bool _isInitializing = false;

        public double OffsetY => (double)numOffsetY.Value;
        public double OffsetX => (double)numOffsetX.Value;
        public bool OffsetPositive => rdoUp.Checked;
        public int StepInterval
        {
            get
            {
                if (rdoStep2 != null && rdoStep2.Checked) return 2;
                if (rdoStep3 != null && rdoStep3.Checked) return 3;
                if (numCustomStep != null) return (int)numCustomStep.Value;
                return 2;
            }
        }
        public int PatternIndex => cboPattern.SelectedIndex >= 0 ? cboPattern.SelectedIndex : 0;
        public ObjectId[] SelectedIds => _selectedIds.ToArray();

        public DichChuyenLabelChanTrongBandForm() : this(null, null) { }

        public DichChuyenLabelChanTrongBandForm(Editor ed, Database db)
        {
            _ed = ed ?? Application.DocumentManager.MdiActiveDocument?.Editor;
            _db = db ?? Application.DocumentManager.MdiActiveDocument?.Database;

            _isInitializing = true;
            InitializeComponent();
            RestoreLastSettings();
            _isInitializing = false;

            OnStepChanged(this.StepInterval);
            UpdatePreview();
        }

        private void InitializeComponent()
        {
            _isInitializing = true;

            this.Text = "Dịch Chuyển Label Trong Band (Bảo Toàn Label Group)";
            this.Font = new DrawingFont("Segoe UI", 9.5f);
            this.MinimumSize = new Size(520, 620);
            this.Size = new Size(540, 640);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.BackColor = Color.FromArgb(30, 30, 32);
            this.ForeColor = Color.FromArgb(225, 225, 225);

            int fullWidth = 500;

            // ===== Group 1: Thông tin đối tượng =====
            grpInfo = CreateGroupBox("Đối Tượng Band Label Group", 12, 10, fullWidth, 95);

            lblSelectedCount = new Label
            {
                Text = "Đã chọn: 0 đối tượng",
                ForeColor = Color.FromArgb(120, 220, 120),
                Location = new Point(15, 24),
                Size = new Size(310, 20),
                Font = new DrawingFont("Segoe UI", 9.5f, FontStyle.Bold)
            };

            lblSubEntityCount = new Label
            {
                Text = "Tổng số nhãn/grip tìm thấy: 0",
                ForeColor = Color.FromArgb(160, 200, 255),
                Location = new Point(15, 48),
                Size = new Size(310, 38),
                Font = new DrawingFont("Segoe UI", 8.5f)
            };

            btnPick = new Button
            {
                Text = "Chọn Trên CAD",
                Location = new Point(fullWidth - 150, 20),
                Size = new Size(135, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                Font = new DrawingFont("Segoe UI", 9f, FontStyle.Bold)
            };
            btnPick.FlatAppearance.BorderSize = 0;
            btnPick.Click += BtnPick_Click;

            btnReset = new Button
            {
                Text = "Khôi Phục Gốc",
                Location = new Point(fullWidth - 150, 55),
                Size = new Size(135, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(75, 75, 80),
                ForeColor = Color.FromArgb(230, 230, 230),
                Font = new DrawingFont("Segoe UI", 8.5f)
            };
            btnReset.FlatAppearance.BorderSize = 0;
            btnReset.Click += BtnReset_Click;

            grpInfo.Controls.AddRange(new Control[] { lblSelectedCount, lblSubEntityCount, btnPick, btnReset });
            this.Controls.Add(grpInfo);

            // ===== Group 2: Nhóm Vị Trí Dịch Chuyển (Chu kỳ 2, 3 hoặc tùy chọn N) =====
            grpTarget = CreateGroupBox("Nhóm Vị Trí Dịch Chuyển", 12, 115, fullWidth, 120);

            lblStep = new Label
            {
                Text = "Chu kỳ cách nhau:",
                ForeColor = Color.FromArgb(220, 220, 220),
                Location = new Point(15, 25),
                Size = new Size(125, 24),
                TextAlign = ContentAlignment.MiddleLeft
            };

            rdoStep2 = new RadioButton
            {
                Text = "Cách 2",
                ForeColor = Color.FromArgb(255, 215, 0),
                Location = new Point(142, 25),
                Size = new Size(72, 24),
                Checked = (_lastStep == 2),
                Font = new DrawingFont("Segoe UI", 9.2f, FontStyle.Bold)
            };
            rdoStep2.CheckedChanged += (s, e) => {
                if (_isInitializing) return;
                if (rdoStep2.Checked)
                {
                    numCustomStep.Value = 2;
                    OnStepChanged(2);
                }
            };

            rdoStep3 = new RadioButton
            {
                Text = "Cách 3",
                ForeColor = Color.FromArgb(135, 206, 250),
                Location = new Point(218, 25),
                Size = new Size(72, 24),
                Checked = (_lastStep == 3),
                Font = new DrawingFont("Segoe UI", 9.2f, FontStyle.Bold)
            };
            rdoStep3.CheckedChanged += (s, e) => {
                if (_isInitializing) return;
                if (rdoStep3.Checked)
                {
                    numCustomStep.Value = 3;
                    OnStepChanged(3);
                }
            };

            rdoStepCustom = new RadioButton
            {
                Text = "Tùy chọn:",
                ForeColor = Color.FromArgb(255, 180, 100),
                Location = new Point(294, 25),
                Size = new Size(82, 24),
                Checked = (_lastStep != 2 && _lastStep != 3),
                Font = new DrawingFont("Segoe UI", 9.2f, FontStyle.Bold)
            };
            rdoStepCustom.CheckedChanged += (s, e) => {
                if (_isInitializing) return;
                if (rdoStepCustom.Checked)
                {
                    int val = (int)numCustomStep.Value;
                    if (val == 2 || val == 3)
                    {
                        val = _lastCustomStep > 3 ? _lastCustomStep : 4;
                        numCustomStep.Value = val;
                    }
                    OnStepChanged(val);
                }
            };

            numCustomStep = new NumericUpDown
            {
                Location = new Point(378, 24),
                Size = new Size(58, 25),
                Minimum = 1m,
                Maximum = 50m,
                Value = (_lastStep != 2 && _lastStep != 3) ? (decimal)Math.Max(1, _lastStep) : (decimal)Math.Max(1, _lastCustomStep),
                BackColor = Color.FromArgb(45, 45, 50),
                ForeColor = Color.FromArgb(255, 230, 90),
                Font = new DrawingFont("Segoe UI", 9.5f, FontStyle.Bold)
            };
            numCustomStep.ValueChanged += (s, e) => {
                if (_isInitializing) return;
                int currentVal = (int)numCustomStep.Value;
                if (currentVal == 2)
                {
                    if (!rdoStep2.Checked) rdoStep2.Checked = true;
                    else OnStepChanged(2);
                }
                else if (currentVal == 3)
                {
                    if (!rdoStep3.Checked) rdoStep3.Checked = true;
                    else OnStepChanged(3);
                }
                else
                {
                    if (!rdoStepCustom.Checked) rdoStepCustom.Checked = true;
                    else OnStepChanged(currentVal);
                }
            };

            lblCustomUnit = new Label
            {
                Text = "đt",
                ForeColor = Color.FromArgb(160, 160, 160),
                Location = new Point(440, 25),
                Size = new Size(35, 24),
                TextAlign = ContentAlignment.MiddleLeft
            };

            lblPattern = new Label
            {
                Text = "Mẫu áp dụng:",
                ForeColor = Color.FromArgb(220, 220, 220),
                Location = new Point(15, 68),
                Size = new Size(125, 26),
                TextAlign = ContentAlignment.MiddleLeft
            };

            cboPattern = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(142, 66),
                Size = new Size(fullWidth - 160, 26),
                BackColor = Color.FromArgb(45, 45, 50),
                ForeColor = Color.White,
                Font = new DrawingFont("Segoe UI", 9.5f)
            };
            cboPattern.SelectedIndexChanged += (s, e) => UpdatePreview();

            grpTarget.Controls.AddRange(new Control[] {
                lblStep, rdoStep2, rdoStep3, rdoStepCustom, numCustomStep, lblCustomUnit,
                lblPattern, cboPattern
            });
            this.Controls.Add(grpTarget);

            // ===== Group 3: Giá trị dịch chuyển =====
            grpOffset = CreateGroupBox("Giá Trị Dịch Chuyển (Đơn Vị Bản Vẽ)", 12, 245, fullWidth, 110);

            lblOffsetY = new Label
            {
                Text = "Khoảng dịch Y:",
                ForeColor = Color.FromArgb(220, 220, 220),
                Location = new Point(15, 26),
                Size = new Size(105, 24),
                TextAlign = ContentAlignment.MiddleLeft
            };

            numOffsetY = new NumericUpDown
            {
                Location = new Point(125, 25),
                Size = new Size(85, 26),
                Minimum = 0.01m,
                Maximum = 9999m,
                DecimalPlaces = 2,
                Increment = 0.5m,
                Value = (decimal)_lastOffsetY,
                BackColor = Color.FromArgb(45, 45, 50),
                ForeColor = Color.FromArgb(255, 230, 90),
                Font = new DrawingFont("Segoe UI", 10.5f, FontStyle.Bold)
            };
            numOffsetY.ValueChanged += (s, e) => UpdatePreview();

            lblUnitY = new Label
            {
                Text = "đvbv",
                ForeColor = Color.FromArgb(160, 160, 160),
                Location = new Point(215, 26),
                Size = new Size(40, 24),
                TextAlign = ContentAlignment.MiddleLeft
            };

            rdoUp = new RadioButton
            {
                Text = "Lên (+Y)",
                ForeColor = Color.FromArgb(100, 230, 150),
                Location = new Point(275, 25),
                Size = new Size(95, 26),
                Checked = _lastOffsetPositive,
                Font = new DrawingFont("Segoe UI", 9f, FontStyle.Bold)
            };
            rdoUp.CheckedChanged += (s, e) => UpdatePreview();

            rdoDown = new RadioButton
            {
                Text = "Xuống (-Y)",
                ForeColor = Color.FromArgb(255, 140, 80),
                Location = new Point(380, 25),
                Size = new Size(100, 26),
                Checked = !_lastOffsetPositive,
                Font = new DrawingFont("Segoe UI", 9f, FontStyle.Bold)
            };
            rdoDown.CheckedChanged += (s, e) => UpdatePreview();

            // Offset X (tùy chọn)
            lblOffsetX = new Label
            {
                Text = "Khoảng dịch X:",
                ForeColor = Color.FromArgb(180, 180, 180),
                Location = new Point(15, 66),
                Size = new Size(105, 24),
                TextAlign = ContentAlignment.MiddleLeft
            };

            numOffsetX = new NumericUpDown
            {
                Location = new Point(125, 65),
                Size = new Size(85, 26),
                Minimum = -9999m,
                Maximum = 9999m,
                DecimalPlaces = 2,
                Increment = 0.5m,
                Value = (decimal)_lastOffsetX,
                BackColor = Color.FromArgb(45, 45, 50),
                ForeColor = Color.FromArgb(200, 220, 255),
                Font = new DrawingFont("Segoe UI", 10f)
            };
            numOffsetX.ValueChanged += (s, e) => UpdatePreview();

            lblUnitX = new Label
            {
                Text = "đvbv (0 = không dịch theo trục X)",
                ForeColor = Color.FromArgb(150, 150, 150),
                Location = new Point(215, 66),
                Size = new Size(260, 24),
                TextAlign = ContentAlignment.MiddleLeft
            };

            grpOffset.Controls.AddRange(new Control[] {
                lblOffsetY, numOffsetY, lblUnitY, rdoUp, rdoDown,
                lblOffsetX, numOffsetX, lblUnitX
            });
            this.Controls.Add(grpOffset);

            // ===== Group 4: Xem trước & trạng thái =====
            grpPreview = CreateGroupBox("Xem Trước Thao Tác", 12, 365, fullWidth, 130);

            lblPreview = new Label
            {
                Text = "",
                ForeColor = Color.FromArgb(240, 220, 140),
                Location = new Point(15, 22),
                Size = new Size(fullWidth - 30, 98),
                Font = new DrawingFont("Segoe UI", 8.8f)
            };

            grpPreview.Controls.Add(lblPreview);
            this.Controls.Add(grpPreview);

            // ===== Panel nút bấm =====
            pnlButtons = new Panel
            {
                Location = new Point(12, 505),
                Size = new Size(fullWidth, 50),
                BackColor = Color.Transparent
            };

            btnOK = new Button
            {
                Text = "Thực Hiện",
                Location = new Point(110, 6),
                Size = new Size(160, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                Font = new DrawingFont("Segoe UI", 10f, FontStyle.Bold),
                DialogResult = DialogResult.OK
            };
            btnOK.FlatAppearance.BorderSize = 0;
            btnOK.Click += (s, e) => SaveCurrentSettings();

            btnCancel = new Button
            {
                Text = "Đóng",
                Location = new Point(290, 6),
                Size = new Size(110, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(70, 70, 75),
                ForeColor = Color.White,
                Font = new DrawingFont("Segoe UI", 9.5f),
                DialogResult = DialogResult.Cancel
            };
            btnCancel.FlatAppearance.BorderSize = 0;

            pnlButtons.Controls.AddRange(new Control[] { btnOK, btnCancel });
            this.Controls.Add(pnlButtons);

            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;

            // Form events
            this.FormClosing += (s, e) => SaveCurrentSettings();
            this.Resize += (s, e) => UpdateLayout();
        }

        private void OnStepChanged(int step)
        {
            cboPattern.Items.Clear();

            if (step == 1)
            {
                cboPattern.Items.Add("Dịch tất cả đối tượng (mọi cọc từ trái sang)");
            }
            else if (step == 2)
            {
                cboPattern.Items.Add("Vị trí Chẵn (Cột 2, 4, 6... từ trái sang)");
                cboPattern.Items.Add("Vị trí Lẻ (Cột 1, 3, 5... từ trái sang)");
                cboPattern.Items.Add("So le 2 chiều (Cột Chẵn Lên, Cột Lẻ Xuống)");
            }
            else if (step == 3)
            {
                cboPattern.Items.Add("Bậc thang 3 cấp (Cột 1: Giữ nguyên, Cột 2: +Y, Cột 3: +2Y)");
                cboPattern.Items.Add("So le 3 hướng (Cột 1: Giữ nguyên, Cột 2: +Y, Cột 3: -Y)");
                cboPattern.Items.Add("Vị trí thứ 2 (Cột 2, 5, 8... từ trái sang)");
                cboPattern.Items.Add("Vị trí thứ 3 (Cột 3, 6, 9... từ trái sang)");
                cboPattern.Items.Add("Vị trí thứ 1 (Cột 1, 4, 7... từ trái sang)");
            }
            else // step >= 4 (hoặc số tùy chọn bất kỳ)
            {
                cboPattern.Items.Add($"Bậc thang {step} cấp (Cột 1: 0, Cột 2: +Y, ..., Cột {step}: +{step - 1}Y)");
                cboPattern.Items.Add($"So le 2 chiều xen kẽ (Cột chẵn: +Y, Cột lẻ: -Y)");
                cboPattern.Items.Add($"So le xen kẽ (Cột chẵn: +Y, Cột lẻ: Giữ nguyên)");

                for (int k = 1; k <= step; k++)
                {
                    string eg = $"{k}, {k + step}, {k + 2 * step}...";
                    cboPattern.Items.Add($"Vị trí thứ {k} (Cột {eg} từ trái sang)");
                }
            }

            int idx = 0;
            if (_lastPatternByStep.TryGetValue(step, out int savedIdx) && savedIdx >= 0 && savedIdx < cboPattern.Items.Count)
            {
                idx = savedIdx;
            }
            cboPattern.SelectedIndex = idx;

            UpdatePreview();
        }

        private GroupBox CreateGroupBox(string title, int x, int y, int w, int h)
        {
            return new GroupBox
            {
                Text = title,
                Location = new Point(x, y),
                Size = new Size(w, h),
                ForeColor = Color.FromArgb(100, 185, 255),
                Font = new DrawingFont("Segoe UI", 9f, FontStyle.Bold),
                Padding = new Padding(6)
            };
        }

        private void UpdatePreview()
        {
            if (_isInitializing || cboPattern.SelectedIndex < 0) return;

            double dy = (double)numOffsetY.Value;
            double actualY = rdoUp.Checked ? dy : -dy;
            double dx = (double)numOffsetX.Value;

            string dirY = actualY >= 0 ? $"+{actualY:F2}" : $"{actualY:F2}";
            string infoX = Math.Abs(dx) > 0.001 ? $", Offset X: {dx:+0.00;-0.00}" : "";
            string patternDesc = cboPattern.SelectedItem?.ToString() ?? "";

            int step = this.StepInterval;
            string stepDesc = $"Chu kỳ {step} (cách {step} đối tượng)";

            lblPreview.Text =
                $"• Kiểu xử lý: {stepDesc}\n" +
                $"• Mẫu áp dụng: {patternDesc}\n" +
                $"• Độ dời Y cơ sở: {dirY} đvbv {infoX}\n" +
                $"• Cơ chế: Hiệu chỉnh LabelLocation trực tiếp, KHÔNG nổ (explode) Label Group.\n" +
                $"• Đảm bảo dữ liệu Civil 3D động luôn bảo toàn 100%.";
        }

        private void UpdateLayout()
        {
            int w = this.ClientSize.Width - 24;
            if (grpInfo != null)
            {
                grpInfo.Width = w;
                if (btnPick != null) btnPick.Left = w - 150;
                if (btnReset != null) btnReset.Left = w - 150;
            }
            if (grpTarget != null)
            {
                grpTarget.Width = w;
                if (cboPattern != null) cboPattern.Width = w - 160;
            }
            if (grpOffset != null) grpOffset.Width = w;
            if (grpPreview != null)
            {
                grpPreview.Width = w;
                if (lblPreview != null) lblPreview.Width = w - 30;
            }
            if (pnlButtons != null)
            {
                pnlButtons.Width = w;
                int totalBtnW = btnOK.Width + 20 + btnCancel.Width;
                btnOK.Left = Math.Max(10, (w - totalBtnW) / 2);
                btnCancel.Left = btnOK.Right + 20;
            }
        }

        private void BtnPick_Click(object sender, EventArgs e)
        {
            if (_ed == null) return;

            using (_ed.StartUserInteraction(this))
            {
                PromptSelectionOptions pso = new PromptSelectionOptions
                {
                    MessageForAdding = "\nChọn Section Data Band Label Group hoặc Label trong Band (Enter khi chọn xong): "
                };
                PromptSelectionResult selRes = _ed.GetSelection(pso);
                if (selRes.Status == PromptStatus.OK)
                {
                    SetSelectedIds(selRes.Value.GetObjectIds());
                }
            }
        }

        private void BtnReset_Click(object sender, EventArgs e)
        {
            if (_selectedIds.Count == 0)
            {
                MessageBox.Show(this, "Vui lòng chọn đối tượng Band Label Group trước khi khôi phục.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_db == null || _ed == null) return;

            int resetCount = 0;
            using (Transaction tr = _db.TransactionManager.StartTransaction())
            {
                foreach (ObjectId oid in _selectedIds)
                {
                    if (oid.IsNull || !oid.IsValid) continue;
                    try
                    {
                        var dbObj = tr.GetObject(oid, OpenMode.ForWrite);
                        if (dbObj is LabelGroup labelGroup)
                        {
                            labelGroup.ResetAllSubCommonLabelLocations();
                            labelGroup.RecordGraphicsModified(true);
                            resetCount++;
                        }
                    }
                    catch { }
                }
                tr.Commit();
            }

            _ed.Regen();
            _ed.WriteMessage($"\n✓ Đã khôi phục vị trí gốc cho {resetCount} Band Label Group.");
            UpdateCounts();
            MessageBox.Show(this, $"Đã khôi phục vị trí gốc cho {resetCount} Band Label Group thành công!", "Hoàn thành", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public void SetSelectedIds(ObjectId[] ids)
        {
            _selectedIds.Clear();
            if (ids != null)
            {
                foreach (var id in ids)
                {
                    if (!id.IsNull && id.IsValid && !_selectedIds.Contains(id))
                        _selectedIds.Add(id);
                }
            }
            UpdateCounts();
        }

        private void UpdateCounts()
        {
            if (lblSelectedCount == null) return;

            lblSelectedCount.Text = $"Đã chọn: {_selectedIds.Count} đối tượng";

            if (_db == null || _selectedIds.Count == 0)
            {
                lblSubEntityCount.Text = "Chưa có đối tượng nào. Bấm nút bên phải để chọn.";
                return;
            }

            int totalSubs = 0;
            int totalGroups = 0;
            using (Transaction tr = _db.TransactionManager.StartTransaction())
            {
                foreach (ObjectId oid in _selectedIds)
                {
                    if (oid.IsNull || !oid.IsValid) continue;
                    try
                    {
                        var obj = tr.GetObject(oid, OpenMode.ForRead);
                        if (obj is LabelGroup lg)
                        {
                            totalGroups++;
                            totalSubs += (int)lg.SubEntityCount;
                        }
                    }
                    catch { }
                }
            }

            if (totalGroups > 0)
            {
                lblSubEntityCount.Text = $"Tìm thấy {totalGroups} Label Group (tổng {totalSubs:#,##0} nhãn con).";
            }
            else
            {
                lblSubEntityCount.Text = $"Đã chọn {_selectedIds.Count} đối tượng thông thường.";
            }
        }

        public void SaveCurrentSettings()
        {
            _lastOffsetY = (double)numOffsetY.Value;
            _lastOffsetX = (double)numOffsetX.Value;
            _lastOffsetPositive = rdoUp.Checked;
            _lastStep = this.StepInterval;
            if (_lastStep != 2 && _lastStep != 3)
            {
                _lastCustomStep = _lastStep;
            }

            if (cboPattern.SelectedIndex >= 0)
            {
                _lastPatternByStep[_lastStep] = cboPattern.SelectedIndex;
            }

            _lastFormSize = this.Size;
            _lastSelectedIds = new List<ObjectId>(_selectedIds);
        }

        public void RestoreLastSettings()
        {
            numOffsetY.Value = (decimal)Math.Min(Math.Max(_lastOffsetY, 0.01), 9999);
            numOffsetX.Value = (decimal)Math.Min(Math.Max(_lastOffsetX, -9999), 9999);

            rdoUp.Checked = _lastOffsetPositive;
            rdoDown.Checked = !_lastOffsetPositive;

            numCustomStep.Value = (decimal)Math.Max(1, Math.Min(50, _lastCustomStep));

            if (_lastStep == 2)
            {
                rdoStep2.Checked = true;
            }
            else if (_lastStep == 3)
            {
                rdoStep3.Checked = true;
            }
            else
            {
                numCustomStep.Value = (decimal)Math.Max(1, Math.Min(50, _lastStep));
                rdoStepCustom.Checked = true;
            }

            if (_lastFormSize != Size.Empty)
                this.Size = _lastFormSize;

            if (_selectedIds.Count == 0 && _lastSelectedIds != null && _lastSelectedIds.Count > 0)
            {
                SetSelectedIds(_lastSelectedIds.ToArray());
            }
            else
            {
                UpdateCounts();
            }

            UpdatePreview();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                this.ClientRectangle,
                Color.FromArgb(32, 34, 40),
                Color.FromArgb(20, 22, 26),
                90f))
            {
                e.Graphics.FillRectangle(brush, this.ClientRectangle);
            }
        }
    }
}

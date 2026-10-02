using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using CadEntity = Autodesk.AutoCAD.DatabaseServices.Entity;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsFont = System.Drawing.Font;
using WinFormsColor = System.Drawing.Color;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace MyFirstProject.Civil_Tool_2
{
    public class SelectedFeatureLineBoundaryItem
    {
        public ObjectId Id { get; set; }
        public string Handle { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public double Length { get; set; }
        public bool IsClosed { get; set; }
        public double MinZ { get; set; }
        public double MaxZ { get; set; }
        public string SourceLayer { get; set; } = string.Empty;
    }

    public class TaoMatPhangTuFeaturelineForm : Form
    {
        // ═══════════════════════════════════════════════════════════════
        //  PERSISTENT STATE (BẮT BUỘC THEO QUY TẮC RULE 2)
        // ═══════════════════════════════════════════════════════════════
        private static Size _lastFormSize = new Size(840, 700);
        private static string _lastSurfaceName = "";
        private static string _lastMasterSurfaceStyle = "";
        private static string _lastSubSurfaceStyle = "";
        private static bool _lastIsMergeMode = true;
        private static bool _lastCreateSnapshot = true;
        private static bool _lastDeleteSubSurfaces = false;
        private static double _lastMidOrdDistance = 0.10;
        private static bool _lastAutoClose = true;
        private static bool _lastRebuildAuto = true;
        private static List<ObjectId> _lastSelectedIds = new List<ObjectId>();

        // ═══════════════════════════════════════════════════════════════
        //  FIELDS
        // ═══════════════════════════════════════════════════════════════
        private readonly Database _db;
        private readonly Editor _ed;
        private readonly List<SelectedFeatureLineBoundaryItem> _items = new List<SelectedFeatureLineBoundaryItem>();

        // UI Controls
        private ListView lvObjects = null!;
        private WinFormsLabel lblSummary = null!;
        private WinFormsLabel lblStatus = null!;
        private TextBox txtSurfaceName = null!;
        private ComboBox cboMasterStyles = null!;
        private ComboBox cboSubStyles = null!;
        private RadioButton radMergeMode = null!;
        private RadioButton radDirectMode = null!;
        private CheckBox chkCreateSnapshot = null!;
        private CheckBox chkDeleteSubSurfaces = null!;
        private NumericUpDown numMidOrd = null!;
        private CheckBox chkAutoClose = null!;
        private CheckBox chkRebuildAuto = null!;
        private ProgressBar progressBar = null!;
        private Button btnSelect = null!;
        private Button btnClear = null!;
        private Button btnExecute = null!;
        private Button btnClose = null!;

        public TaoMatPhangTuFeaturelineForm(Database db, Editor ed)
        {
            _db = db;
            _ed = ed;

            InitializeCustomUI();
            RestoreLastSettings();
            LoadSurfaceStyles();
            TryRestorePreviousSelection();
        }

        private void InitializeCustomUI()
        {
            this.Text = "Tạo Mặt Phẳng Từ Feature Line Khép Kín - Civil 3D Tools";
            this.Size = _lastFormSize;
            this.MinimumSize = new Size(780, 620);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.BackColor = WinFormsColor.FromArgb(245, 247, 250);

            // 1. Header Panel
            Panel pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 68,
                BackColor = WinFormsColor.FromArgb(24, 76, 120),
                Padding = new Padding(16, 10, 16, 10)
            };

            WinFormsLabel lblHeaderTitle = new WinFormsLabel
            {
                Text = "TẠO MẶT PHẲNG TỪ FEATURE LINE KHÉP KÍN",
                Font = new WinFormsFont("Segoe UI", 12F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = WinFormsColor.White,
                Dock = DockStyle.Top,
                Height = 26
            };

            WinFormsLabel lblHeaderSub = new WinFormsLabel
            {
                Text = "Tạo TIN Surface có phạm vi chỉ nằm trong các đường kín bằng phương pháp gộp từng mặt phẳng nhỏ",
                Font = new WinFormsFont("Segoe UI", 8.5F, FontStyle.Regular, GraphicsUnit.Point),
                ForeColor = WinFormsColor.FromArgb(210, 230, 250),
                Dock = DockStyle.Fill
            };

            pnlHeader.Controls.Add(lblHeaderSub);
            pnlHeader.Controls.Add(lblHeaderTitle);
            this.Controls.Add(pnlHeader);

            // 2. Bottom Panel (Actions & Status)
            Panel pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 85,
                BackColor = WinFormsColor.White,
                Padding = new Padding(16, 10, 16, 10)
            };

            progressBar = new ProgressBar
            {
                Dock = DockStyle.Top,
                Height = 10,
                Visible = false
            };

            lblStatus = new WinFormsLabel
            {
                Text = "Sẵn sàng. Hãy chọn các đường Feature Line khép kín trên bản vẽ...",
                Font = new WinFormsFont("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = WinFormsColor.FromArgb(100, 110, 125),
                Dock = DockStyle.Top,
                Height = 22
            };

            Panel pnlButtons = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 38
            };

            btnExecute = new Button
            {
                Text = "⚡ Tạo Mặt Phẳng",
                Width = 150,
                Height = 35,
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
                Left = pnlButtons.Width - 260,
                Top = 2,
                BackColor = WinFormsColor.FromArgb(13, 110, 253),
                ForeColor = WinFormsColor.White,
                FlatStyle = FlatStyle.Flat,
                Font = new WinFormsFont("Segoe UI", 9.5F, FontStyle.Bold)
            };
            btnExecute.FlatAppearance.BorderSize = 0;
            btnExecute.Click += BtnExecute_Click;

            btnClose = new Button
            {
                Text = "Đóng",
                Width = 90,
                Height = 35,
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
                Left = pnlButtons.Width - 95,
                Top = 2,
                BackColor = WinFormsColor.FromArgb(235, 238, 242),
                ForeColor = WinFormsColor.FromArgb(50, 60, 75),
                FlatStyle = FlatStyle.Flat,
                Font = new WinFormsFont("Segoe UI", 9F)
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => this.Close();

            pnlButtons.Controls.Add(btnExecute);
            pnlButtons.Controls.Add(btnClose);

            pnlBottom.Controls.Add(pnlButtons);
            pnlBottom.Controls.Add(lblStatus);
            pnlBottom.Controls.Add(progressBar);
            this.Controls.Add(pnlBottom);

            // 3. Main Center Layout
            TableLayoutPanel tlpMain = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(12),
                BackColor = WinFormsColor.Transparent
            };
            tlpMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(SizeType.Percent, 54F));
            tlpMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(SizeType.Percent, 46F));
            this.Controls.Add(tlpMain);
            tlpMain.BringToFront();

            // ═══════════════════════════════════════════════════════════════
            //  CỘT TRÁI: DANH SÁCH ĐỐI TƯỢNG (FEATURE LINES / POLYLINES)
            // ═══════════════════════════════════════════════════════════════
            GroupBox grpSource = new GroupBox
            {
                Text = "1. Đối tượng nguồn (Feature Lines / Polylines)",
                Font = new WinFormsFont("Segoe UI", 9.5F, FontStyle.Bold),
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            Panel pnlSelectActions = new Panel
            {
                Dock = DockStyle.Top,
                Height = 38
            };

            btnSelect = new Button
            {
                Text = "🎯 Chọn trên CAD",
                Width = 145,
                Height = 32,
                Left = 0,
                Top = 2,
                BackColor = WinFormsColor.FromArgb(24, 76, 120),
                ForeColor = WinFormsColor.White,
                FlatStyle = FlatStyle.Flat,
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold)
            };
            btnSelect.FlatAppearance.BorderSize = 0;
            btnSelect.Click += BtnSelect_Click;

            btnClear = new Button
            {
                Text = "Xóa chọn",
                Width = 90,
                Height = 32,
                Left = 152,
                Top = 2,
                BackColor = WinFormsColor.FromArgb(235, 238, 242),
                ForeColor = WinFormsColor.FromArgb(60, 70, 85),
                FlatStyle = FlatStyle.Flat,
                Font = new WinFormsFont("Segoe UI", 8.5F)
            };
            btnClear.FlatAppearance.BorderSize = 0;
            btnClear.Click += (s, e) => ClearSelection();

            pnlSelectActions.Controls.Add(btnSelect);
            pnlSelectActions.Controls.Add(btnClear);

            lvObjects = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                Font = new WinFormsFont("Segoe UI", 8.5F),
                MultiSelect = true
            };
            lvObjects.Columns.Add("STT", 45);
            lvObjects.Columns.Add("Loại", 95);
            lvObjects.Columns.Add("Kín?", 55);
            lvObjects.Columns.Add("Chu vi (m)", 80);
            lvObjects.Columns.Add("Min Z (m)", 70);
            lvObjects.Columns.Add("Max Z (m)", 70);
            lvObjects.Columns.Add("Layer", 95);

            lblSummary = new WinFormsLabel
            {
                Dock = DockStyle.Bottom,
                Height = 34,
                Font = new WinFormsFont("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = WinFormsColor.FromArgb(70, 80, 95),
                Text = "Đã chọn: 0 đối tượng | 0 khép kín | Tổng chu vi: 0.00 m"
            };

            chkAutoClose = new CheckBox
            {
                Dock = DockStyle.Bottom,
                Height = 26,
                Text = "Tự động coi đường gần kín (khoảng cách hở < 0.1m) là khép kín",
                Font = new WinFormsFont("Segoe UI", 8.5F),
                Checked = _lastAutoClose
            };

            grpSource.Controls.Add(lvObjects);
            grpSource.Controls.Add(lblSummary);
            grpSource.Controls.Add(chkAutoClose);
            grpSource.Controls.Add(pnlSelectActions);

            tlpMain.Controls.Add(grpSource, 0, 0);

            // ═══════════════════════════════════════════════════════════════
            //  CỘT PHẢI: CẤU HÌNH SURFACE & PHƯƠNG THỨC GỘP
            // ═══════════════════════════════════════════════════════════════
            Panel pnlRight = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true
            };

            // Group 2: Thông số Mặt phẳng tổng hợp
            GroupBox grpSurface = new GroupBox
            {
                Text = "2. Mặt phẳng tổng hợp (Master Surface)",
                Font = new WinFormsFont("Segoe UI", 9.5F, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 165,
                Padding = new Padding(12)
            };

            WinFormsLabel lblName = new WinFormsLabel
            {
                Text = "Tên mặt phẳng mới:",
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Regular),
                Left = 12,
                Top = 26,
                Width = 140
            };

            txtSurfaceName = new TextBox
            {
                Left = 12,
                Top = 48,
                Width = 275,
                Font = new WinFormsFont("Segoe UI", 9F),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            Button btnGenName = new Button
            {
                Text = "🔄 Tạo tên",
                Left = 292,
                Top = 47,
                Width = 75,
                Height = 26,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Font = new WinFormsFont("Segoe UI", 8F),
                FlatStyle = FlatStyle.Flat
            };
            btnGenName.Click += (s, e) => GenerateDefaultSurfaceName();

            WinFormsLabel lblStyle = new WinFormsLabel
            {
                Text = "Surface Style hiển thị:",
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Regular),
                Left = 12,
                Top = 82,
                Width = 140
            };

            cboMasterStyles = new ComboBox
            {
                Left = 12,
                Top = 104,
                Width = 355,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new WinFormsFont("Segoe UI", 9F),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            chkRebuildAuto = new CheckBox
            {
                Text = "Tự động cập nhật khi nguồn thay đổi (Auto-Rebuild)",
                Font = new WinFormsFont("Segoe UI", 8.5F),
                Left = 12,
                Top = 136,
                Width = 355,
                Checked = _lastRebuildAuto,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            grpSurface.Controls.Add(lblName);
            grpSurface.Controls.Add(txtSurfaceName);
            grpSurface.Controls.Add(btnGenName);
            grpSurface.Controls.Add(lblStyle);
            grpSurface.Controls.Add(cboMasterStyles);
            grpSurface.Controls.Add(chkRebuildAuto);

            // Group 3: Phương thức xử lý & Gộp mặt phẳng
            GroupBox grpMethod = new GroupBox
            {
                Text = "3. Phương thức xử lý & Gộp mặt phẳng",
                Font = new WinFormsFont("Segoe UI", 9.5F, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 310,
                Top = 175,
                Padding = new Padding(12)
            };

            radMergeMode = new RadioButton
            {
                Text = "Gộp từng mặt phẳng nhỏ (Khuyên dùng)",
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold),
                Left = 12,
                Top = 25,
                Width = 355,
                Checked = _lastIsMergeMode,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            radMergeMode.CheckedChanged += RadMergeMode_CheckedChanged;

            Panel pnlMergeOptions = new Panel
            {
                Left = 24,
                Top = 50,
                Width = 345,
                Height = 135,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            chkCreateSnapshot = new CheckBox
            {
                Text = "Tạo Snapshot (Đóng băng lưới tam giác gộp)",
                Font = new WinFormsFont("Segoe UI", 8.5F),
                Left = 0,
                Top = 2,
                Width = 340,
                Checked = _lastCreateSnapshot,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            chkCreateSnapshot.CheckedChanged += (s, e) =>
            {
                chkDeleteSubSurfaces.Enabled = chkCreateSnapshot.Checked;
                if (!chkCreateSnapshot.Checked) chkDeleteSubSurfaces.Checked = false;
            };

            chkDeleteSubSurfaces = new CheckBox
            {
                Text = "Xóa các mặt phẳng con sau khi gộp Snapshot (Gọn bản vẽ)",
                Font = new WinFormsFont("Segoe UI", 8.5F),
                Left = 0,
                Top = 28,
                Width = 340,
                Checked = _lastDeleteSubSurfaces,
                Enabled = _lastCreateSnapshot,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            WinFormsLabel lblSubStyle = new WinFormsLabel
            {
                Text = "Style cho mặt phẳng con (nếu giữ lại):",
                Font = new WinFormsFont("Segoe UI", 8.5F),
                Left = 0,
                Top = 56,
                Width = 340
            };

            cboSubStyles = new ComboBox
            {
                Left = 0,
                Top = 78,
                Width = 340,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new WinFormsFont("Segoe UI", 8.5F),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            WinFormsLabel lblMergeTip = new WinFormsLabel
            {
                Text = "💡 Mỗi Feature Line sẽ có 1 Outer Boundary riêng, triệt tiêu 100% tam giác nối ra ngoài.",
                Font = new WinFormsFont("Segoe UI", 8F, FontStyle.Italic),
                ForeColor = WinFormsColor.FromArgb(40, 110, 40),
                Left = 0,
                Top = 106,
                Width = 340,
                Height = 26,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            pnlMergeOptions.Controls.Add(chkCreateSnapshot);
            pnlMergeOptions.Controls.Add(chkDeleteSubSurfaces);
            pnlMergeOptions.Controls.Add(lblSubStyle);
            pnlMergeOptions.Controls.Add(cboSubStyles);
            pnlMergeOptions.Controls.Add(lblMergeTip);

            radDirectMode = new RadioButton
            {
                Text = "Tạo 1 mặt phẳng trực tiếp (Cho 1 đường hoặc có đảo lồng nhau)",
                Font = new WinFormsFont("Segoe UI", 8.5F),
                Left = 12,
                Top = 192,
                Width = 355,
                Checked = !_lastIsMergeMode,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            WinFormsLabel lblMidOrd = new WinFormsLabel
            {
                Text = "Độ mịn cung cong (Mid-ordinate):",
                Font = new WinFormsFont("Segoe UI", 8.5F),
                Left = 12,
                Top = 228,
                Width = 230
            };

            numMidOrd = new NumericUpDown
            {
                Left = 250,
                Top = 225,
                Width = 117,
                DecimalPlaces = 2,
                Minimum = 0.01M,
                Maximum = 10.0M,
                Increment = 0.05M,
                Value = (decimal)_lastMidOrdDistance,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            WinFormsLabel lblNote = new WinFormsLabel
            {
                Text = "📌 Lưu ý: Với nhiều đường rời rạc, chế độ Gộp đảm bảo lưới bề mặt chỉ sinh ra trong lòng các đường khép kín đã chọn.",
                Font = new WinFormsFont("Segoe UI", 8F, FontStyle.Regular),
                ForeColor = WinFormsColor.FromArgb(80, 95, 110),
                Left = 12,
                Top = 258,
                Width = 355,
                Height = 42,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            grpMethod.Controls.Add(radMergeMode);
            grpMethod.Controls.Add(pnlMergeOptions);
            grpMethod.Controls.Add(radDirectMode);
            grpMethod.Controls.Add(lblMidOrd);
            grpMethod.Controls.Add(numMidOrd);
            grpMethod.Controls.Add(lblNote);

            pnlRight.Controls.Add(grpMethod);
            pnlRight.Controls.Add(grpSurface);

            tlpMain.Controls.Add(pnlRight, 1, 0);

            // Form Closing & Resize Events (RULE 2)
            this.FormClosing += (s, e) => SaveCurrentSettings();
            this.ResizeEnd += (s, e) => _lastFormSize = this.Size;
        }

        private void RadMergeMode_CheckedChanged(object? sender, EventArgs e)
        {
            chkCreateSnapshot.Enabled = radMergeMode.Checked;
            chkDeleteSubSurfaces.Enabled = radMergeMode.Checked && chkCreateSnapshot.Checked;
            cboSubStyles.Enabled = radMergeMode.Checked;
        }

        #region PERSISTENCE LOGIC (RULE 2)

        private void SaveCurrentSettings()
        {
            _lastFormSize = this.Size;
            _lastSurfaceName = txtSurfaceName.Text.Trim();
            if (cboMasterStyles.SelectedItem != null)
                _lastMasterSurfaceStyle = cboMasterStyles.SelectedItem.ToString() ?? "";
            if (cboSubStyles.SelectedItem != null)
                _lastSubSurfaceStyle = cboSubStyles.SelectedItem.ToString() ?? "";

            _lastIsMergeMode = radMergeMode.Checked;
            _lastCreateSnapshot = chkCreateSnapshot.Checked;
            _lastDeleteSubSurfaces = chkDeleteSubSurfaces.Checked;
            _lastMidOrdDistance = (double)numMidOrd.Value;
            _lastAutoClose = chkAutoClose.Checked;
            _lastRebuildAuto = chkRebuildAuto.Checked;

            _lastSelectedIds = _items.Select(x => x.Id).Where(id => !id.IsNull && id.IsValid && !id.IsErased).ToList();
        }

        private void RestoreLastSettings()
        {
            if (string.IsNullOrWhiteSpace(_lastSurfaceName))
            {
                GenerateDefaultSurfaceName();
            }
            else
            {
                txtSurfaceName.Text = _lastSurfaceName;
            }

            radMergeMode.Checked = _lastIsMergeMode;
            radDirectMode.Checked = !_lastIsMergeMode;
            chkCreateSnapshot.Checked = _lastCreateSnapshot;
            chkDeleteSubSurfaces.Checked = _lastDeleteSubSurfaces;
            chkDeleteSubSurfaces.Enabled = _lastCreateSnapshot;

            numMidOrd.Value = Math.Max(numMidOrd.Minimum, Math.Min(numMidOrd.Maximum, (decimal)_lastMidOrdDistance));
            chkAutoClose.Checked = _lastAutoClose;
            chkRebuildAuto.Checked = _lastRebuildAuto;
        }

        private void GenerateDefaultSurfaceName()
        {
            txtSurfaceName.Text = $"MatPhang_FL_{DateTime.Now:yyyyMMdd_HHmm}";
        }

        private void LoadSurfaceStyles()
        {
            cboMasterStyles.Items.Clear();
            cboSubStyles.Items.Clear();

            try
            {
                CivilDocument civDoc = CivilApplication.ActiveDocument;
                if (civDoc != null)
                {
                    using (Transaction tr = _db.TransactionManager.StartTransaction())
                    {
                        foreach (ObjectId styleId in civDoc.Styles.SurfaceStyles)
                        {
                            if (tr.GetObject(styleId, OpenMode.ForRead) is SurfaceStyle style && !string.IsNullOrWhiteSpace(style.Name))
                            {
                                cboMasterStyles.Items.Add(style.Name);
                                cboSubStyles.Items.Add(style.Name);
                            }
                        }
                        tr.Commit();
                    }
                }
            }
            catch { }

            // Xử lý Master Surface Style
            if (cboMasterStyles.Items.Count > 0)
            {
                if (!string.IsNullOrEmpty(_lastMasterSurfaceStyle) && cboMasterStyles.Items.Contains(_lastMasterSurfaceStyle))
                {
                    cboMasterStyles.SelectedItem = _lastMasterSurfaceStyle;
                }
                else
                {
                    cboMasterStyles.SelectedIndex = 0;
                }
            }
            else
            {
                cboMasterStyles.Items.Add("_Default_");
                cboMasterStyles.SelectedIndex = 0;
            }

            // Xử lý Sub-surface Style (ưu tiên _No Display hoặc No Display)
            if (cboSubStyles.Items.Count > 0)
            {
                if (!string.IsNullOrEmpty(_lastSubSurfaceStyle) && cboSubStyles.Items.Contains(_lastSubSurfaceStyle))
                {
                    cboSubStyles.SelectedItem = _lastSubSurfaceStyle;
                }
                else
                {
                    int noDisplayIdx = -1;
                    for (int i = 0; i < cboSubStyles.Items.Count; i++)
                    {
                        string name = cboSubStyles.Items[i].ToString() ?? "";
                        if (name.IndexOf("No Display", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            name.IndexOf("Border Only", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            noDisplayIdx = i;
                            break;
                        }
                    }

                    if (noDisplayIdx >= 0) cboSubStyles.SelectedIndex = noDisplayIdx;
                    else cboSubStyles.SelectedIndex = 0;
                }
            }
            else
            {
                cboSubStyles.Items.Add("_No Display_");
                cboSubStyles.SelectedIndex = 0;
            }
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
                PopulateItems(validIds);
                lblStatus.Text = $"Đã khôi phục {validIds.Count} đối tượng từ phiên làm việc trước.";
            }
        }

        #endregion

        #region SELECTION & UI POPULATION

        private void BtnSelect_Click(object? sender, EventArgs e)
        {
            PromptSelectionOptions pso = new PromptSelectionOptions
            {
                MessageForAdding = "\nChọn các đường Feature Line hoặc Polyline khép kín: ",
                AllowDuplicates = false
            };

            SelectionFilter filter = new SelectionFilter(new TypedValue[]
            {
                new TypedValue((int)DxfCode.Operator, "<OR"),
                new TypedValue((int)DxfCode.Start, "AECC_FEATURE_LINE"),
                new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),
                new TypedValue((int)DxfCode.Start, "POLYLINE"),
                new TypedValue((int)DxfCode.Operator, "OR>")
            });

            PromptSelectionResult psr;
            using (EditorUserInteraction interaction = _ed.StartUserInteraction(this))
            {
                psr = _ed.GetSelection(pso, filter);
            }

            if (psr.Status != PromptStatus.OK || psr.Value == null || psr.Value.Count == 0)
            {
                return;
            }

            List<ObjectId> selectedIds = psr.Value.GetObjectIds().ToList();
            PopulateItems(selectedIds);
        }

        private void PopulateItems(List<ObjectId> ids)
        {
            _items.Clear();
            lvObjects.Items.Clear();

            if (ids == null || ids.Count == 0)
            {
                UpdateSummary();
                return;
            }

            using (Transaction tr = _db.TransactionManager.StartTransaction())
            {
                int stt = 1;
                foreach (ObjectId id in ids)
                {
                    if (id.IsNull || !id.IsValid || id.IsErased) continue;

                    CadEntity? ent = tr.GetObject(id, OpenMode.ForRead) as CadEntity;
                    if (ent == null) continue;

                    string typeName = ent.GetType().Name;
                    double length = 0;
                    bool isClosed = false;
                    double minZ = 0;
                    double maxZ = 0;

                    if (ent is FeatureLine fl)
                    {
                        typeName = "FeatureLine";
                        isClosed = fl.Closed;
                        try
                        {
                            length = fl.GetDistanceAtParameter(fl.EndParam) - fl.GetDistanceAtParameter(fl.StartParam);
                        }
                        catch
                        {
                            length = 0;
                        }
                        minZ = fl.MinElevation;
                        maxZ = fl.MaxElevation;

                        if (!isClosed && chkAutoClose.Checked)
                        {
                            try
                            {
                                if (fl.StartPoint.DistanceTo(fl.EndPoint) <= 0.1)
                                    isClosed = true;
                            }
                            catch { }
                        }
                    }
                    else if (ent is Polyline pl)
                    {
                        typeName = "Polyline";
                        isClosed = pl.Closed;
                        length = pl.Length;
                        minZ = pl.Elevation;
                        maxZ = pl.Elevation;

                        if (!isClosed && chkAutoClose.Checked)
                        {
                            if (pl.StartPoint.DistanceTo(pl.EndPoint) <= 0.1)
                                isClosed = true;
                        }
                    }
                    else if (ent is Polyline3d p3d)
                    {
                        typeName = "Polyline3D";
                        isClosed = p3d.Closed;
                        length = p3d.Length;
                        try
                        {
                            var pts = new List<Point3d>();
                            foreach (ObjectId vId in p3d)
                            {
                                if (tr.GetObject(vId, OpenMode.ForRead) is PolylineVertex3d v)
                                    pts.Add(v.Position);
                            }
                            if (pts.Count > 0)
                            {
                                minZ = pts.Min(p => p.Z);
                                maxZ = pts.Max(p => p.Z);
                            }
                        }
                        catch { }

                        if (!isClosed && chkAutoClose.Checked)
                        {
                            if (p3d.StartPoint.DistanceTo(p3d.EndPoint) <= 0.1)
                                isClosed = true;
                        }
                    }

                    var item = new SelectedFeatureLineBoundaryItem
                    {
                        Id = id,
                        Handle = ent.Handle.ToString(),
                        TypeName = typeName,
                        Length = length,
                        IsClosed = isClosed,
                        MinZ = minZ,
                        MaxZ = maxZ,
                        SourceLayer = ent.Layer
                    };
                    _items.Add(item);

                    ListViewItem lvi = new ListViewItem((stt++).ToString());
                    lvi.SubItems.Add(typeName);
                    lvi.SubItems.Add(isClosed ? "Có" : "Hở ⚠️");
                    lvi.SubItems.Add(length.ToString("F2"));
                    lvi.SubItems.Add(minZ.ToString("F2"));
                    lvi.SubItems.Add(maxZ.ToString("F2"));
                    lvi.SubItems.Add(ent.Layer);

                    if (!isClosed)
                    {
                        lvi.ForeColor = WinFormsColor.Crimson;
                        lvi.ToolTipText = "Đường này chưa khép kín!";
                    }

                    lvObjects.Items.Add(lvi);
                }
                tr.Commit();
            }

            UpdateSummary();
            lblStatus.Text = $"Đã tải {_items.Count} đối tượng.";
        }

        private void ClearSelection()
        {
            _items.Clear();
            lvObjects.Items.Clear();
            UpdateSummary();
            lblStatus.Text = "Đã xóa toàn bộ đối tượng chọn.";
        }

        private void UpdateSummary()
        {
            int total = _items.Count;
            int closedCount = _items.Count(x => x.IsClosed);
            double totalLen = _items.Sum(x => x.Length);

            lblSummary.Text = $"Đã chọn: {total} đối tượng | {closedCount} khép kín | Tổng chu vi: {totalLen:F2} m";

            if (total > closedCount)
            {
                lblSummary.ForeColor = WinFormsColor.Crimson;
            }
            else
            {
                lblSummary.ForeColor = WinFormsColor.FromArgb(70, 80, 95);
            }
        }

        #endregion

        #region EXECUTION LOGIC

        private void BtnExecute_Click(object? sender, EventArgs e)
        {
            if (_items.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn ít nhất một đường Feature Line hoặc Polyline!", "Chưa chọn đối tượng", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string surfaceName = txtSurfaceName.Text.Trim();
            if (string.IsNullOrWhiteSpace(surfaceName))
            {
                MessageBox.Show("Vui lòng nhập tên cho Mặt phẳng mới!", "Thiếu tên Mặt phẳng", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtSurfaceName.Focus();
                return;
            }

            int unclosedCount = _items.Count(x => !x.IsClosed);
            if (unclosedCount > 0 && !chkAutoClose.Checked)
            {
                var dr = MessageBox.Show(
                    $"Có {unclosedCount} đối tượng chưa khép kín. Bạn có muốn tiếp tục và để công cụ cố gắng tự động đóng (Auto-close) không?",
                    "Cảnh báo đường hở",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );
                if (dr != DialogResult.Yes) return;
            }

            string masterStyleName = cboMasterStyles.SelectedItem?.ToString() ?? "";
            string subStyleName = cboSubStyles.SelectedItem?.ToString() ?? "";
            bool isMergeMode = radMergeMode.Checked;
            bool createSnapshot = chkCreateSnapshot.Checked;
            bool deleteSubSurfaces = chkDeleteSubSurfaces.Checked;
            double midOrd = (double)numMidOrd.Value;
            bool autoClose = chkAutoClose.Checked;
            bool rebuildAuto = chkRebuildAuto.Checked;

            List<ObjectId> entityIds = _items.Select(x => x.Id).ToList();

            progressBar.Visible = true;
            progressBar.Style = ProgressBarStyle.Marquee;
            lblStatus.Text = isMergeMode ? "Đang tạo các mặt phẳng con và gộp dữ liệu..." : "Đang tạo mặt phẳng trực tiếp...";
            btnExecute.Enabled = false;

            try
            {
                var result = CTT_Tao_Mat_Phang_Tu_Featureline_Command.ThucHienTaoMatPhang(
                    _db,
                    _ed,
                    surfaceName,
                    masterStyleName,
                    subStyleName,
                    entityIds,
                    isMergeMode,
                    createSnapshot,
                    deleteSubSurfaces,
                    midOrd,
                    autoClose,
                    rebuildAuto
                );

                if (result.Success)
                {
                    lblStatus.Text = $"✅ {result.Message}";
                    MessageBox.Show(
                        $"{result.Message}\n\n" +
                        $"• Tên Mặt phẳng: '{surfaceName}'\n" +
                        $"• Số Feature Lines xử lý: {entityIds.Count}\n" +
                        $"• Phương thức: {(isMergeMode ? $"Gộp {result.SubSurfaceCount} mặt phẳng nhỏ" : "Tạo trực tiếp")}\n" +
                        $"• Snapshot: {(createSnapshot ? "ĐÃ TẠO (Độc lập dữ liệu)" : "Không")}\n" +
                        $"• Mặt phẳng con: {(deleteSubSurfaces ? "Đã xóa (Bản vẽ sạch)" : "Giữ lại")}",
                        "Tạo Mặt Phẳng Thành Công",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                    SaveCurrentSettings();
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else
                {
                    lblStatus.Text = $"❌ Lỗi: {result.Message}";
                    MessageBox.Show($"Không thể tạo mặt phẳng: {result.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (System.Exception ex)
            {
                lblStatus.Text = $"❌ Lỗi ngoại lệ: {ex.Message}";
                MessageBox.Show($"Xảy ra lỗi trong quá trình thực thi: {ex.Message}", "Lỗi ngoại lệ", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                progressBar.Visible = false;
                btnExecute.Enabled = true;
            }
        }

        #endregion
    }
}

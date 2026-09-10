using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsFont = System.Drawing.Font;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace MyFirstProject.Civil_Tool_2
{
    public class XuatSolidCorridorForm : Form
    {
        // ═══════════════════════════════════════════════════════════════
        //  PERSISTENT SETTINGS (Ghi nhớ cấu hình lần chạy trước)
        // ═══════════════════════════════════════════════════════════════
        private static ObjectId _lastCorridorId = ObjectId.Null;
        private static bool _lastExportShapes = true;
        private static bool _lastCreateSolidForShape = true;
        private static bool _lastSweepSolidForShape = true;
        private static bool _lastExportLinks = true;
        private static bool _lastFilterCodes = false;
        private static HashSet<string> _lastSelectedShapeCodes = new(StringComparer.OrdinalIgnoreCase);
        private static HashSet<string> _lastSelectedLinkCodes = new(StringComparer.OrdinalIgnoreCase);
        private static string _lastExportFolder = string.Empty;
        private static bool _lastOverwrite = true;
        private static bool _lastOpenAfterExport = false;
        private static bool _lastOpenFolderAfterExport = false;
        private static Size _lastFormSize = new Size(720, 680);

        private readonly Database _db;

        // ═══════════════════════════════════════════════════════════════
        //  OUTPUT PROPERTIES (Dữ liệu trả về cho Command)
        // ═══════════════════════════════════════════════════════════════
        public ObjectId SelectedCorridorId { get; private set; } = ObjectId.Null;
        public bool ExportShapes => chkExportShapes.Checked;
        public bool CreateSolidForShape => radSolid3d.Checked;
        public bool SweepSolidForShape => chkSweepSolid.Checked;
        public bool ExportLinks => chkExportLinks.Checked;
        public bool FilterCodes => radSelectedCodes.Checked;
        public List<string> IncludedCodes { get; private set; } = new();
        public string OutputFilePath => txtFilePath.Text.Trim();
        public bool OverwriteIfExists => chkOverwrite.Checked;
        public bool OpenAfterExport => chkOpenAfterExport.Checked;
        public bool OpenFolderAfterExport => chkOpenFolderAfterExport.Checked;
        public bool FormAccepted { get; private set; } = false;

        // ═══════════════════════════════════════════════════════════════
        //  UI CONTROLS
        // ═══════════════════════════════════════════════════════════════
        private ComboBox cmbCorridor = null!;
        private Button btnPickCorridor = null!;
        private WinFormsLabel lblCorridorInfo = null!;

        private CheckBox chkExportShapes = null!;
        private RadioButton radSolid3d = null!;
        private RadioButton radBody = null!;
        private CheckBox chkSweepSolid = null!;
        private CheckBox chkExportLinks = null!;

        private RadioButton radAllCodes = null!;
        private RadioButton radSelectedCodes = null!;
        private TabControl tabCodes = null!;
        private CheckedListBox chkListShapes = null!;
        private CheckedListBox chkListLinks = null!;
        private Button btnSelectAllShapes = null!;
        private Button btnDeselectAllShapes = null!;
        private Button btnSelectAllLinks = null!;
        private Button btnDeselectAllLinks = null!;

        private TextBox txtFilePath = null!;
        private Button btnBrowse = null!;
        private CheckBox chkOverwrite = null!;
        private CheckBox chkOpenAfterExport = null!;
        private CheckBox chkOpenFolderAfterExport = null!;

        private WinFormsLabel lblStatus = null!;
        private Button btnExecute = null!;
        private Button btnCancel = null!;

        public XuatSolidCorridorForm(Database db)
        {
            _db = db;
            InitializeComponent();
            LoadData();
            RestoreLastSettings();
        }

        private void InitializeComponent()
        {
            this.Text = "CTC_XuatSolidCorridor — Xuất 3D Solid & Body từ Corridor";
            this.Size = _lastFormSize;
            this.MinimumSize = new Size(680, 640);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.ShowIcon = false;
            this.Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Regular);

            // Tiêu đề
            var lblTitle = new WinFormsLabel
            {
                Text = "XUẤT 3D SOLID & BODY TỪ CORRIDOR RA FILE DWG",
                Location = new Point(15, 10),
                Size = new Size(670, 24),
                Font = new WinFormsFont("Segoe UI", 11.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 70, 150),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            // ── Group 1: Chọn Corridor nguồn ──
            var grpCorridor = new GroupBox
            {
                Text = "1. Corridor nguồn",
                Location = new Point(15, 38),
                Size = new Size(674, 92),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var lblSelect = new WinFormsLabel
            {
                Text = "Chọn Corridor:",
                Location = new Point(15, 24),
                Size = new Size(95, 22)
            };

            cmbCorridor = new ComboBox
            {
                Location = new Point(115, 21),
                Size = new Size(415, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            cmbCorridor.SelectedIndexChanged += (s, e) => OnCorridorChanged();

            btnPickCorridor = new Button
            {
                Text = "🎯 Pick trên bản vẽ",
                Location = new Point(538, 19),
                Size = new Size(122, 27),
                Cursor = Cursors.Hand,
                Font = new WinFormsFont("Segoe UI", 8.5F, FontStyle.Bold),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnPickCorridor.Click += BtnPickCorridor_Click;

            lblCorridorInfo = new WinFormsLabel
            {
                Text = "Thông tin: Chưa chọn Corridor",
                Location = new Point(15, 54),
                Size = new Size(645, 30),
                ForeColor = Color.DarkSlateGray,
                Font = new WinFormsFont("Segoe UI", 8.5F, FontStyle.Italic),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            grpCorridor.Controls.AddRange(new Control[] { lblSelect, cmbCorridor, btnPickCorridor, lblCorridorInfo });

            // ── Group 2: Thiết lập Xuất Khối 3D ──
            var grpSolidOptions = new GroupBox
            {
                Text = "2. Tùy chọn xuất khối 3D",
                Location = new Point(15, 134),
                Size = new Size(674, 98),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            chkExportShapes = new CheckBox
            {
                Text = "Xuất Shapes (Mặt cắt / Kết cấu áo đường, vỉa hè, bó vỉa...)",
                Location = new Point(15, 22),
                Size = new Size(400, 22),
                Checked = true,
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold)
            };
            chkExportShapes.CheckedChanged += (s, e) => UpdateSolidOptionsState();

            radSolid3d = new RadioButton
            {
                Text = "AutoCAD 3D Solid chuẩn (Khuyên dùng)",
                Location = new Point(35, 46),
                Size = new Size(260, 22),
                Checked = true
            };

            radBody = new RadioButton
            {
                Text = "3D Body",
                Location = new Point(300, 46),
                Size = new Size(120, 22)
            };

            chkSweepSolid = new CheckBox
            {
                Text = "Quét mịn theo tim tuyến (Sweep Solids dọc đường cong)",
                Location = new Point(425, 46),
                Size = new Size(235, 22),
                Checked = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            chkExportLinks = new CheckBox
            {
                Text = "Xuất Links thành 3D Body (Mặt đáy, taluy, lớp bề mặt khuôn đường)",
                Location = new Point(15, 70),
                Size = new Size(450, 22),
                Checked = true,
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold)
            };
            chkExportLinks.CheckedChanged += (s, e) => ValidateForm();

            grpSolidOptions.Controls.AddRange(new Control[] {
                chkExportShapes, radSolid3d, radBody, chkSweepSolid, chkExportLinks
            });

            // ── Group 3: Lọc mã Code ──
            var grpCodes = new GroupBox
            {
                Text = "3. Lọc mã Code (Shape & Link Codes)",
                Location = new Point(15, 236),
                Size = new Size(674, 195),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            radAllCodes = new RadioButton
            {
                Text = "Xuất tất cả mã Code có trong Corridor",
                Location = new Point(15, 20),
                Size = new Size(260, 22),
                Checked = true
            };
            radAllCodes.CheckedChanged += (s, e) => UpdateCodeFilterState();

            radSelectedCodes = new RadioButton
            {
                Text = "Chỉ xuất các mã Code được chọn bên dưới:",
                Location = new Point(285, 20),
                Size = new Size(300, 22)
            };
            radSelectedCodes.CheckedChanged += (s, e) => UpdateCodeFilterState();

            tabCodes = new TabControl
            {
                Location = new Point(15, 46),
                Size = new Size(645, 140),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Enabled = false
            };

            // Tab 1: Shapes
            var tabShapes = new TabPage("Mã Shape Codes (Khối kết cấu)");
            chkListShapes = new CheckedListBox
            {
                Location = new Point(8, 8),
                Size = new Size(490, 95),
                CheckOnClick = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            chkListShapes.ItemCheck += (s, e) => this.BeginInvoke(new Action(ValidateForm));

            btnSelectAllShapes = new Button
            {
                Text = "Chọn tất cả",
                Location = new Point(506, 8),
                Size = new Size(120, 26),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnSelectAllShapes.Click += (s, e) => SetAllChecked(chkListShapes, true);

            btnDeselectAllShapes = new Button
            {
                Text = "Bỏ chọn tất cả",
                Location = new Point(506, 38),
                Size = new Size(120, 26),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnDeselectAllShapes.Click += (s, e) => SetAllChecked(chkListShapes, false);

            tabShapes.Controls.AddRange(new Control[] { chkListShapes, btnSelectAllShapes, btnDeselectAllShapes });

            // Tab 2: Links
            var tabLinks = new TabPage("Mã Link Codes (Bề mặt liên kết)");
            chkListLinks = new CheckedListBox
            {
                Location = new Point(8, 8),
                Size = new Size(490, 95),
                CheckOnClick = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            chkListLinks.ItemCheck += (s, e) => this.BeginInvoke(new Action(ValidateForm));

            btnSelectAllLinks = new Button
            {
                Text = "Chọn tất cả",
                Location = new Point(506, 8),
                Size = new Size(120, 26),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnSelectAllLinks.Click += (s, e) => SetAllChecked(chkListLinks, true);

            btnDeselectAllLinks = new Button
            {
                Text = "Bỏ chọn tất cả",
                Location = new Point(506, 38),
                Size = new Size(120, 26),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnDeselectAllLinks.Click += (s, e) => SetAllChecked(chkListLinks, false);

            tabLinks.Controls.AddRange(new Control[] { chkListLinks, btnSelectAllLinks, btnDeselectAllLinks });

            tabCodes.TabPages.Add(tabShapes);
            tabCodes.TabPages.Add(tabLinks);

            grpCodes.Controls.AddRange(new Control[] { radAllCodes, radSelectedCodes, tabCodes });

            // ── Group 4: Thiết lập File DWG đích ──
            var grpTargetFile = new GroupBox
            {
                Text = "4. File DWG đích",
                Location = new Point(15, 436),
                Size = new Size(674, 125),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            var lblFile = new WinFormsLabel
            {
                Text = "Đường dẫn file DWG:",
                Location = new Point(15, 25),
                Size = new Size(130, 22)
            };

            txtFilePath = new TextBox
            {
                Location = new Point(145, 22),
                Size = new Size(410, 23),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            txtFilePath.TextChanged += (s, e) => ValidateForm();

            btnBrowse = new Button
            {
                Text = "📂 Chọn vị trí...",
                Location = new Point(562, 20),
                Size = new Size(98, 27),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnBrowse.Click += BtnBrowse_Click;

            chkOverwrite = new CheckBox
            {
                Text = "Ghi đè file nếu đã tồn tại",
                Location = new Point(145, 52),
                Size = new Size(200, 22),
                Checked = true
            };

            chkOpenAfterExport = new CheckBox
            {
                Text = "Mở file bản vẽ sau khi xuất thành công",
                Location = new Point(360, 52),
                Size = new Size(290, 22),
                Checked = false
            };

            chkOpenFolderAfterExport = new CheckBox
            {
                Text = "Mở thư mục chứa file sau khi xuất",
                Location = new Point(145, 78),
                Size = new Size(250, 22),
                Checked = false
            };

            grpTargetFile.Controls.AddRange(new Control[] {
                lblFile, txtFilePath, btnBrowse, chkOverwrite, chkOpenAfterExport, chkOpenFolderAfterExport
            });

            // ── Thanh trạng thái & Nút hành động ──
            lblStatus = new WinFormsLabel
            {
                Text = "Sẵn sàng",
                Location = new Point(15, 595),
                Size = new Size(400, 32),
                ForeColor = Color.DarkSlateGray,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            btnExecute = new Button
            {
                Text = "🚀 Xuất Solid ra File",
                Location = new Point(420, 590),
                Size = new Size(155, 36),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                Font = new WinFormsFont("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Enabled = false
            };
            btnExecute.Click += BtnExecute_Click;

            btnCancel = new Button
            {
                Text = "Đóng",
                Location = new Point(584, 590),
                Size = new Size(105, 36),
                DialogResult = DialogResult.Cancel,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };

            this.Controls.AddRange(new Control[] {
                lblTitle, grpCorridor, grpSolidOptions, grpCodes, grpTargetFile,
                lblStatus, btnExecute, btnCancel
            });

            this.AcceptButton = btnExecute;
            this.CancelButton = btnCancel;
            this.FormClosing += (s, e) => SaveCurrentSettings();
        }

        // ═══════════════════════════════════════════════════════════════
        //  DATA LOADING & BINDING
        // ═══════════════════════════════════════════════════════════════
        private void LoadData()
        {
            cmbCorridor.Items.Clear();
            try
            {
                using (var tr = _db.TransactionManager.StartTransaction())
                {
                    CivilDocument cdoc = CivilDocument.GetCivilDocument(_db);
                    if (cdoc != null)
                    {
                        foreach (ObjectId id in cdoc.CorridorCollection)
                        {
                            var c = tr.GetObject(id, OpenMode.ForRead) as Corridor;
                            if (c != null && !string.IsNullOrEmpty(c.Name))
                            {
                                cmbCorridor.Items.Add(new CorridorItem(c.Name, id));
                            }
                        }
                    }
                    tr.Commit();
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Lỗi nạp dữ liệu: {ex.Message}";
            }
        }

        private void OnCorridorChanged()
        {
            chkListShapes.Items.Clear();
            chkListLinks.Items.Clear();

            var item = cmbCorridor.SelectedItem as CorridorItem;
            if (item == null || item.Id.IsNull)
            {
                lblCorridorInfo.Text = "Chưa chọn Corridor";
                txtFilePath.Text = string.Empty;
                btnExecute.Enabled = false;
                return;
            }

            try
            {
                using (var tr = _db.TransactionManager.StartTransaction())
                {
                    var corridor = tr.GetObject(item.Id, OpenMode.ForRead) as Corridor;
                    if (corridor != null)
                    {
                        int totalBaselines = corridor.Baselines.Count;
                        int totalRegions = 0;
                        foreach (Baseline bl in corridor.Baselines)
                        {
                            totalRegions += bl.BaselineRegions.Count;
                        }

                        // Lấy các mã Shape Codes
                        string[] shapeCodes = corridor.GetShapeCodes();
                        if (shapeCodes != null)
                        {
                            foreach (string sc in shapeCodes.OrderBy(x => x))
                            {
                                if (!string.IsNullOrEmpty(sc))
                                {
                                    bool isChecked = _lastSelectedShapeCodes.Count == 0 || _lastSelectedShapeCodes.Contains(sc);
                                    chkListShapes.Items.Add(sc, isChecked);
                                }
                            }
                        }

                        // Lấy các mã Link Codes
                        string[] linkCodes = corridor.GetLinkCodes();
                        if (linkCodes != null)
                        {
                            foreach (string lc in linkCodes.OrderBy(x => x))
                            {
                                if (!string.IsNullOrEmpty(lc))
                                {
                                    bool isChecked = _lastSelectedLinkCodes.Count == 0 || _lastSelectedLinkCodes.Contains(lc);
                                    chkListLinks.Items.Add(lc, isChecked);
                                }
                            }
                        }

                        lblCorridorInfo.Text = $"Thông tin: {totalBaselines} Baseline(s), {totalRegions} Phân đoạn (Region). {chkListShapes.Items.Count} mã Shape, {chkListLinks.Items.Count} mã Link.";

                        // Tự động tạo đường dẫn file mặc định
                        GenerateDefaultFilePath(corridor.Name);
                    }
                    tr.Commit();
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Lỗi đọc Corridor: {ex.Message}";
            }

            ValidateForm();
        }

        private void GenerateDefaultFilePath(string corridorName)
        {
            string targetDir = _lastExportFolder;
            if (string.IsNullOrEmpty(targetDir) || !Directory.Exists(targetDir))
            {
                try
                {
                    string currentDrawingPath = _db.Filename;
                    if (!string.IsNullOrEmpty(currentDrawingPath) && File.Exists(currentDrawingPath))
                    {
                        targetDir = Path.GetDirectoryName(currentDrawingPath)!;
                    }
                }
                catch { }

                if (string.IsNullOrEmpty(targetDir) || !Directory.Exists(targetDir))
                {
                    targetDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                }
            }

            string drawingBaseName = "Drawing";
            try
            {
                if (!string.IsNullOrEmpty(_db.Filename))
                {
                    drawingBaseName = Path.GetFileNameWithoutExtension(_db.Filename);
                }
            }
            catch { }

            string safeCorridorName = string.Join("_", corridorName.Split(Path.GetInvalidFileNameChars()));
            string fileName = $"{drawingBaseName}_{safeCorridorName}_Solids.dwg";
            txtFilePath.Text = Path.Combine(targetDir, fileName);
        }

        private void UpdateSolidOptionsState()
        {
            bool exportShapes = chkExportShapes.Checked;
            radSolid3d.Enabled = exportShapes;
            radBody.Enabled = exportShapes;
            chkSweepSolid.Enabled = exportShapes;
            ValidateForm();
        }

        private void UpdateCodeFilterState()
        {
            tabCodes.Enabled = radSelectedCodes.Checked;
            ValidateForm();
        }

        private void SetAllChecked(CheckedListBox list, bool check)
        {
            for (int i = 0; i < list.Items.Count; i++)
            {
                list.SetItemChecked(i, check);
            }
            ValidateForm();
        }

        // ═══════════════════════════════════════════════════════════════
        //  PICK CORRIDOR TRÊN CANVAS
        // ═══════════════════════════════════════════════════════════════
        private void BtnPickCorridor_Click(object? sender, EventArgs e)
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;

            using (var interaction = ed.StartUserInteraction(this))
            {
                var peo = new PromptEntityOptions("\nChọn Corridor trên bản vẽ: ");
                peo.SetRejectMessage("\nĐối tượng chọn phải là Corridor Civil 3D.");
                peo.AddAllowedClass(typeof(Corridor), exactMatch: false);

                var per = ed.GetEntity(peo);
                interaction.End();

                if (per.Status == PromptStatus.OK)
                {
                    SelectCorridorById(per.ObjectId);
                }
            }
        }

        public void SelectCorridorById(ObjectId id)
        {
            if (id.IsNull || !id.IsValid || id.IsErased) return;

            for (int i = 0; i < cmbCorridor.Items.Count; i++)
            {
                var item = cmbCorridor.Items[i] as CorridorItem;
                if (item != null && item.Id == id)
                {
                    cmbCorridor.SelectedIndex = i;
                    return;
                }
            }
        }

        private void BtnBrowse_Click(object? sender, EventArgs e)
        {
            using (var sfd = new SaveFileDialog())
            {
                sfd.Title = "Chọn vị trí lưu file 3D Solid / Body DWG";
                sfd.Filter = "AutoCAD Drawing (*.dwg)|*.dwg|Tất cả tập tin (*.*)|*.*";
                sfd.DefaultExt = "dwg";

                string currentText = txtFilePath.Text.Trim();
                if (!string.IsNullOrEmpty(currentText))
                {
                    try
                    {
                        string dir = Path.GetDirectoryName(currentText)!;
                        if (Directory.Exists(dir)) sfd.InitialDirectory = dir;
                        sfd.FileName = Path.GetFileName(currentText);
                    }
                    catch { }
                }

                if (sfd.ShowDialog(this) == DialogResult.OK)
                {
                    txtFilePath.Text = sfd.FileName;
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  FORM VALIDATION
        // ═══════════════════════════════════════════════════════════════
        private void ValidateForm()
        {
            var item = cmbCorridor.SelectedItem as CorridorItem;
            if (item == null || item.Id.IsNull)
            {
                lblStatus.Text = "⚠ Vui lòng chọn Corridor cần xuất solid.";
                lblStatus.ForeColor = Color.Red;
                btnExecute.Enabled = false;
                return;
            }

            if (!chkExportShapes.Checked && !chkExportLinks.Checked)
            {
                lblStatus.Text = "⚠ Vui lòng chọn ít nhất một tùy chọn: Xuất Shapes hoặc Xuất Links.";
                lblStatus.ForeColor = Color.Red;
                btnExecute.Enabled = false;
                return;
            }

            if (radSelectedCodes.Checked)
            {
                int selectedCount = 0;
                if (chkExportShapes.Checked) selectedCount += chkListShapes.CheckedItems.Count;
                if (chkExportLinks.Checked) selectedCount += chkListLinks.CheckedItems.Count;

                if (selectedCount == 0)
                {
                    lblStatus.Text = "⚠ Bạn đang chọn lọc mã code nhưng chưa tích chọn mã nào.";
                    lblStatus.ForeColor = Color.Red;
                    btnExecute.Enabled = false;
                    return;
                }
            }

            string filePath = txtFilePath.Text.Trim();
            if (string.IsNullOrWhiteSpace(filePath))
            {
                lblStatus.Text = "⚠ Vui lòng chỉ định đường dẫn file DWG xuất.";
                lblStatus.ForeColor = Color.Red;
                btnExecute.Enabled = false;
                return;
            }

            try
            {
                string dir = Path.GetDirectoryName(filePath)!;
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                {
                    lblStatus.Text = "⚠ Thư mục lưu file không tồn tại.";
                    lblStatus.ForeColor = Color.Red;
                    btnExecute.Enabled = false;
                    return;
                }
            }
            catch
            {
                lblStatus.Text = "⚠ Đường dẫn file không hợp lệ.";
                lblStatus.ForeColor = Color.Red;
                btnExecute.Enabled = false;
                return;
            }

            lblStatus.Text = "✓ Cấu hình hợp lệ. Sẵn sàng xuất 3D Solid / Body.";
            lblStatus.ForeColor = Color.DarkGreen;
            btnExecute.Enabled = true;
        }

        // ═══════════════════════════════════════════════════════════════
        //  EXECUTE BUTTON CLICK
        // ═══════════════════════════════════════════════════════════════
        private void BtnExecute_Click(object? sender, EventArgs e)
        {
            var item = cmbCorridor.SelectedItem as CorridorItem;
            if (item == null || item.Id.IsNull) return;

            string filePath = txtFilePath.Text.Trim();
            if (File.Exists(filePath) && !chkOverwrite.Checked)
            {
                var confirm = MessageBox.Show(
                    $"File '{Path.GetFileName(filePath)}' đã tồn tại trong thư mục.\nBạn có muốn ghi đè lên file này không?",
                    "Xác nhận ghi đè file",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (confirm != DialogResult.Yes) return;
            }

            SelectedCorridorId = item.Id;

            // Thu thập mã code cần xuất
            IncludedCodes.Clear();
            if (radSelectedCodes.Checked)
            {
                if (chkExportShapes.Checked)
                {
                    foreach (var sc in chkListShapes.CheckedItems)
                    {
                        if (sc != null) IncludedCodes.Add(sc.ToString()!);
                    }
                }
                if (chkExportLinks.Checked)
                {
                    foreach (var lc in chkListLinks.CheckedItems)
                    {
                        if (lc != null) IncludedCodes.Add(lc.ToString()!);
                    }
                }
            }

            SaveCurrentSettings();

            FormAccepted = true;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        // ═══════════════════════════════════════════════════════════════
        //  PERSISTENT STATE: SAVE & RESTORE
        // ═══════════════════════════════════════════════════════════════
        public void SaveCurrentSettings()
        {
            var item = cmbCorridor.SelectedItem as CorridorItem;
            if (item != null && !item.Id.IsNull)
            {
                _lastCorridorId = item.Id;
            }

            _lastExportShapes = chkExportShapes.Checked;
            _lastCreateSolidForShape = radSolid3d.Checked;
            _lastSweepSolidForShape = chkSweepSolid.Checked;
            _lastExportLinks = chkExportLinks.Checked;
            _lastFilterCodes = radSelectedCodes.Checked;

            _lastSelectedShapeCodes.Clear();
            foreach (var sc in chkListShapes.CheckedItems)
            {
                if (sc != null) _lastSelectedShapeCodes.Add(sc.ToString()!);
            }

            _lastSelectedLinkCodes.Clear();
            foreach (var lc in chkListLinks.CheckedItems)
            {
                if (lc != null) _lastSelectedLinkCodes.Add(lc.ToString()!);
            }

            string filePath = txtFilePath.Text.Trim();
            if (!string.IsNullOrEmpty(filePath))
            {
                try
                {
                    string dir = Path.GetDirectoryName(filePath)!;
                    if (Directory.Exists(dir)) _lastExportFolder = dir;
                }
                catch { }
            }

            _lastOverwrite = chkOverwrite.Checked;
            _lastOpenAfterExport = chkOpenAfterExport.Checked;
            _lastOpenFolderAfterExport = chkOpenFolderAfterExport.Checked;
            _lastFormSize = this.Size;
        }

        public void RestoreLastSettings()
        {
            chkExportShapes.Checked = _lastExportShapes;
            radSolid3d.Checked = _lastCreateSolidForShape;
            radBody.Checked = !_lastCreateSolidForShape;
            chkSweepSolid.Checked = _lastSweepSolidForShape;
            chkExportLinks.Checked = _lastExportLinks;
            radSelectedCodes.Checked = _lastFilterCodes;
            radAllCodes.Checked = !_lastFilterCodes;
            chkOverwrite.Checked = _lastOverwrite;
            chkOpenAfterExport.Checked = _lastOpenAfterExport;
            chkOpenFolderAfterExport.Checked = _lastOpenFolderAfterExport;

            UpdateSolidOptionsState();
            UpdateCodeFilterState();

            // Khôi phục corridor cũ nếu còn hợp lệ
            if (!_lastCorridorId.IsNull && _lastCorridorId.IsValid && !_lastCorridorId.IsErased)
            {
                SelectCorridorById(_lastCorridorId);
            }
            else if (cmbCorridor.Items.Count > 0)
            {
                cmbCorridor.SelectedIndex = 0;
            }
        }
    }
}

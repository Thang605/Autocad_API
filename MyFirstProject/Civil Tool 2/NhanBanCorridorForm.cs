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
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsFont = System.Drawing.Font;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace MyFirstProject.Civil_Tool_2
{
    public class NhanBanCorridorForm : Form
    {
        // ═══════════════════════════════════════════════════════════════
        //  PERSISTENT SETTINGS (Ghi nhớ cấu hình lần chạy trước)
        // ═══════════════════════════════════════════════════════════════
        private static ObjectId _lastCorridorId = ObjectId.Null;
        private static string _lastSuffix = "_Copy";
        private static bool _lastCopyTargets = true;
        private static bool _lastCopyFrequencies = true;
        private static bool _lastCopySurfaces = true;
        private static bool _lastAutoRebuild = true;
        private static Size _lastFormSize = new Size(680, 620);

        private Database _db;

        // ═══════════════════════════════════════════════════════════════
        //  OUTPUT PROPERTIES (Dữ liệu trả về cho Command)
        // ═══════════════════════════════════════════════════════════════
        public ObjectId SelectedCorridorId { get; private set; } = ObjectId.Null;
        public string NewCorridorName { get; private set; } = string.Empty;
        public ObjectId SelectedCodeSetStyleId { get; private set; } = ObjectId.Null;
        public bool CopyTargets { get; private set; } = true;
        public bool CopyFrequencies { get; private set; } = true;
        public bool CopySurfaces { get; private set; } = true;
        public bool AutoRebuild { get; private set; } = true;
        public bool FormAccepted { get; private set; } = false;

        // ═══════════════════════════════════════════════════════════════
        //  UI CONTROLS
        // ═══════════════════════════════════════════════════════════════
        private ComboBox cmbSourceCorridor = null!;
        private Button btnPickCorridor = null!;
        private WinFormsLabel lblCorridorInfo = null!;

        private TextBox txtNewCorridorName = null!;
        private ComboBox cmbCodeSetStyle = null!;

        private CheckBox chkCopyTargets = null!;
        private CheckBox chkCopyFrequencies = null!;
        private CheckBox chkCopySurfaces = null!;
        private CheckBox chkAutoRebuild = null!;

        private DataGridView dgvRegions = null!;
        private WinFormsLabel lblStatus = null!;

        private Button btnExecute = null!;
        private Button btnCancel = null!;

        public NhanBanCorridorForm(Database db)
        {
            _db = db;
            InitializeComponent();
            LoadData();
            RestoreLastSettings();
        }

        private void InitializeComponent()
        {
            this.Text = "CTC_NhanBan_Corridor — Nhân bản Corridor Civil 3D";
            this.Size = _lastFormSize;
            this.MinimumSize = new Size(620, 550);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.ShowIcon = false;
            this.Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Regular);

            // Tiêu đề
            var lblTitle = new WinFormsLabel
            {
                Text = "NHÂN BẢN CORRIDOR CIVIL 3D",
                Location = new Point(15, 12),
                Size = new Size(630, 24),
                Font = new WinFormsFont("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 70, 150),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            // ── Group 1: Corridor Nguồn ──
            var grpSource = new GroupBox
            {
                Text = "Corridor nguồn",
                Location = new Point(15, 42),
                Size = new Size(634, 100),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var lblSelect = new WinFormsLabel
            {
                Text = "Chọn Corridor:",
                Location = new Point(15, 26),
                Size = new Size(95, 22)
            };

            cmbSourceCorridor = new ComboBox
            {
                Location = new Point(115, 23),
                Size = new Size(375, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            cmbSourceCorridor.SelectedIndexChanged += (s, e) => OnSourceCorridorChanged();

            btnPickCorridor = new Button
            {
                Text = "🎯 Pick trên bản vẽ",
                Location = new Point(498, 21),
                Size = new Size(122, 27),
                Cursor = Cursors.Hand,
                Font = new WinFormsFont("Segoe UI", 8.5F, FontStyle.Bold),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnPickCorridor.Click += BtnPickCorridor_Click;

            lblCorridorInfo = new WinFormsLabel
            {
                Text = "Thông tin: Chưa chọn Corridor",
                Location = new Point(15, 58),
                Size = new Size(605, 34),
                ForeColor = Color.DarkSlateGray,
                Font = new WinFormsFont("Segoe UI", 8.5F, FontStyle.Italic),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            grpSource.Controls.AddRange(new Control[] { lblSelect, cmbSourceCorridor, btnPickCorridor, lblCorridorInfo });

            // ── Group 2: Cấu hình Corridor Mới ──
            var grpNew = new GroupBox
            {
                Text = "Thiết lập Corridor mới",
                Location = new Point(15, 148),
                Size = new Size(634, 90),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var lblNewName = new WinFormsLabel
            {
                Text = "Tên Corridor mới:",
                Location = new Point(15, 26),
                Size = new Size(115, 22)
            };

            txtNewCorridorName = new TextBox
            {
                Location = new Point(135, 23),
                Size = new Size(485, 23),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            txtNewCorridorName.TextChanged += (s, e) => ValidateForm();

            var lblCodeSet = new WinFormsLabel
            {
                Text = "Code Set Style:",
                Location = new Point(15, 56),
                Size = new Size(115, 22)
            };

            cmbCodeSetStyle = new ComboBox
            {
                Location = new Point(135, 53),
                Size = new Size(485, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            grpNew.Controls.AddRange(new Control[] { lblNewName, txtNewCorridorName, lblCodeSet, cmbCodeSetStyle });

            // ── Group 3: Tùy chọn nhân bản ──
            var grpOptions = new GroupBox
            {
                Text = "Tùy chọn sao chép chi tiết",
                Location = new Point(15, 244),
                Size = new Size(634, 75),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            chkCopyTargets = new CheckBox
            {
                Text = "Sao chép Mục tiêu thiết kế (Targets)",
                Location = new Point(15, 22),
                Size = new Size(270, 22),
                Checked = true
            };

            chkCopyFrequencies = new CheckBox
            {
                Text = "Sao chép Tần suất phân đoạn (Frequencies)",
                Location = new Point(310, 22),
                Size = new Size(295, 22),
                Checked = true
            };

            chkCopySurfaces = new CheckBox
            {
                Text = "Sao chép Bề mặt Corridor (Surfaces & Boundaries)",
                Location = new Point(15, 47),
                Size = new Size(290, 22),
                Checked = true
            };

            chkAutoRebuild = new CheckBox
            {
                Text = "Tự động Rebuild Corridor sau khi tạo",
                Location = new Point(310, 47),
                Size = new Size(280, 22),
                Checked = true
            };

            grpOptions.Controls.AddRange(new Control[] {
                chkCopyTargets, chkCopyFrequencies, chkCopySurfaces, chkAutoRebuild
            });

            // ── Group 4: Xem trước danh sách Phân đoạn ──
            var grpPreview = new GroupBox
            {
                Text = "Danh sách phân đoạn (Regions Preview)",
                Location = new Point(15, 325),
                Size = new Size(634, 200),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            dgvRegions = new DataGridView
            {
                Location = new Point(15, 23),
                Size = new Size(605, 165),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.Fixed3D
            };

            dgvRegions.Columns.Add("RegionName", "Tên Phân Đoạn");
            dgvRegions.Columns.Add("BaselineName", "Baseline / Tim Tuyến");
            dgvRegions.Columns.Add("Assembly", "Mặt Cắt Mẫu (Assembly)");
            dgvRegions.Columns.Add("Profile", "Đường Đỏ (Profile)");
            dgvRegions.Columns.Add("Stations", "Phạm Vi Lý Trình");

            grpPreview.Controls.Add(dgvRegions);

            // ── Thanh trạng thái & Nút hành động ──
            lblStatus = new WinFormsLabel
            {
                Text = "Sẵn sàng",
                Location = new Point(15, 538),
                Size = new Size(380, 26),
                ForeColor = Color.DarkSlateGray,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            btnExecute = new Button
            {
                Text = "🚀 Nhân bản Corridor",
                Location = new Point(400, 532),
                Size = new Size(140, 34),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                Enabled = false
            };
            btnExecute.Click += BtnExecute_Click;

            btnCancel = new Button
            {
                Text = "Đóng",
                Location = new Point(548, 532),
                Size = new Size(100, 34),
                DialogResult = DialogResult.Cancel,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };

            this.Controls.AddRange(new Control[] {
                lblTitle, grpSource, grpNew, grpOptions, grpPreview,
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
            // Load Corridors
            cmbSourceCorridor.Items.Clear();
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
                            if (c != null)
                            {
                                cmbSourceCorridor.Items.Add(new CorridorItem(c.Name, id));
                            }
                        }

                        // Load CodeSetStyles
                        cmbCodeSetStyle.Items.Clear();
                        cmbCodeSetStyle.Items.Add(new StyleItem("(Giữ nguyên kiểu của Corridor nguồn)", ObjectId.Null));
                        foreach (ObjectId styleId in cdoc.Styles.CodeSetStyles)
                        {
                            var st = tr.GetObject(styleId, OpenMode.ForRead) as CodeSetStyle;
                            if (st != null)
                            {
                                cmbCodeSetStyle.Items.Add(new StyleItem(st.Name, styleId));
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

            if (cmbCodeSetStyle.Items.Count > 0)
                cmbCodeSetStyle.SelectedIndex = 0;
        }

        private void OnSourceCorridorChanged()
        {
            dgvRegions.Rows.Clear();
            var item = cmbSourceCorridor.SelectedItem as CorridorItem;
            if (item == null || item.Id.IsNull)
            {
                lblCorridorInfo.Text = "Chưa chọn Corridor";
                txtNewCorridorName.Text = string.Empty;
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

                            string alignmentName = "N/A";
                            string profileName = "N/A";

                            if (!bl.IsFeatureLineBased())
                            {
                                if (!bl.AlignmentId.IsNull)
                                {
                                    var align = tr.GetObject(bl.AlignmentId, OpenMode.ForRead) as Alignment;
                                    if (align != null) alignmentName = align.Name;
                                }
                                if (!bl.ProfileId.IsNull)
                                {
                                    var prof = tr.GetObject(bl.ProfileId, OpenMode.ForRead) as Profile;
                                    if (prof != null) profileName = prof.Name;
                                }
                            }
                            else
                            {
                                alignmentName = "(Feature Line)";
                                profileName = "(Feature Line)";
                            }

                            foreach (BaselineRegion rg in bl.BaselineRegions)
                            {
                                string assemblyName = "N/A";
                                if (!rg.AssemblyId.IsNull)
                                {
                                    var asm = tr.GetObject(rg.AssemblyId, OpenMode.ForRead) as Assembly;
                                    if (asm != null) assemblyName = asm.Name;
                                }

                                dgvRegions.Rows.Add(
                                    rg.Name,
                                    $"{bl.Name} ({alignmentName})",
                                    assemblyName,
                                    profileName,
                                    $"{rg.StartStation:F2}m – {rg.EndStation:F2}m"
                                );
                            }
                        }

                        lblCorridorInfo.Text = $"Thông tin: {totalBaselines} Baseline(s), {totalRegions} Phân đoạn (Region). Bề mặt liên kết: {corridor.CorridorSurfaces.Count}";

                        // Tự động gợi ý tên mới
                        txtNewCorridorName.Text = GenerateUniqueCorridorName(corridor.Name + _lastSuffix);
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

        private string GenerateUniqueCorridorName(string baseName)
        {
            HashSet<string> existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
                            if (c != null) existingNames.Add(c.Name);
                        }
                    }
                    tr.Commit();
                }
            }
            catch { }

            string candidate = baseName;
            int count = 1;
            while (existingNames.Contains(candidate))
            {
                candidate = $"{baseName}_{count}";
                count++;
            }
            return candidate;
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

            for (int i = 0; i < cmbSourceCorridor.Items.Count; i++)
            {
                var item = cmbSourceCorridor.Items[i] as CorridorItem;
                if (item != null && item.Id == id)
                {
                    cmbSourceCorridor.SelectedIndex = i;
                    return;
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  FORM VALIDATION
        // ═══════════════════════════════════════════════════════════════
        private void ValidateForm()
        {
            string newName = txtNewCorridorName.Text.Trim();
            var srcItem = cmbSourceCorridor.SelectedItem as CorridorItem;

            if (srcItem == null || srcItem.Id.IsNull)
            {
                lblStatus.Text = "⚠ Vui lòng chọn Corridor nguồn.";
                lblStatus.ForeColor = Color.Red;
                btnExecute.Enabled = false;
                return;
            }

            if (string.IsNullOrWhiteSpace(newName))
            {
                lblStatus.Text = "⚠ Tên Corridor mới không được để trống.";
                lblStatus.ForeColor = Color.Red;
                btnExecute.Enabled = false;
                return;
            }

            // Kiểm tra trùng tên với các Corridor hiện hữu
            bool nameExists = false;
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
                            if (c != null && c.Name.Equals(newName, StringComparison.OrdinalIgnoreCase))
                            {
                                nameExists = true;
                                break;
                            }
                        }
                    }
                    tr.Commit();
                }
            }
            catch { }

            if (nameExists)
            {
                lblStatus.Text = $"⚠ Tên Corridor '{newName}' đã tồn tại trong bản vẽ!";
                lblStatus.ForeColor = Color.Red;
                btnExecute.Enabled = false;
                return;
            }

            lblStatus.Text = "✓ Thông số hợp lệ. Sẵn sàng nhân bản.";
            lblStatus.ForeColor = Color.DarkGreen;
            btnExecute.Enabled = true;
        }

        // ═══════════════════════════════════════════════════════════════
        //  EXECUTE BUTTON CLICK
        // ═══════════════════════════════════════════════════════════════
        private void BtnExecute_Click(object? sender, EventArgs e)
        {
            var srcItem = cmbSourceCorridor.SelectedItem as CorridorItem;
            if (srcItem == null || srcItem.Id.IsNull) return;

            SelectedCorridorId = srcItem.Id;
            NewCorridorName = txtNewCorridorName.Text.Trim();

            var styleItem = cmbCodeSetStyle.SelectedItem as StyleItem;
            SelectedCodeSetStyleId = styleItem != null ? styleItem.Id : ObjectId.Null;

            CopyTargets = chkCopyTargets.Checked;
            CopyFrequencies = chkCopyFrequencies.Checked;
            CopySurfaces = chkCopySurfaces.Checked;
            AutoRebuild = chkAutoRebuild.Checked;

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
            var srcItem = cmbSourceCorridor.SelectedItem as CorridorItem;
            if (srcItem != null && !srcItem.Id.IsNull)
            {
                _lastCorridorId = srcItem.Id;
            }

            _lastCopyTargets = chkCopyTargets.Checked;
            _lastCopyFrequencies = chkCopyFrequencies.Checked;
            _lastCopySurfaces = chkCopySurfaces.Checked;
            _lastAutoRebuild = chkAutoRebuild.Checked;
            _lastFormSize = this.Size;
        }

        public void RestoreLastSettings()
        {
            chkCopyTargets.Checked = _lastCopyTargets;
            chkCopyFrequencies.Checked = _lastCopyFrequencies;
            chkCopySurfaces.Checked = _lastCopySurfaces;
            chkAutoRebuild.Checked = _lastAutoRebuild;

            // Nạp lại Corridor chọn lần trước nếu còn hợp lệ
            if (!_lastCorridorId.IsNull && _lastCorridorId.IsValid && !_lastCorridorId.IsErased)
            {
                SelectCorridorById(_lastCorridorId);
            }
            else if (cmbSourceCorridor.Items.Count > 0)
            {
                cmbSourceCorridor.SelectedIndex = 0;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  HELPER ITEMS CHO COMBOBOX
    // ═══════════════════════════════════════════════════════════════
    internal class CorridorItem
    {
        public string Name { get; }
        public ObjectId Id { get; }

        public CorridorItem(string name, ObjectId id)
        {
            Name = name;
            Id = id;
        }

        public override string ToString() => Name;
    }

    internal class StyleItem
    {
        public string Name { get; }
        public ObjectId Id { get; }

        public StyleItem(string name, ObjectId id)
        {
            Name = name;
            Id = id;
        }

        public override string ToString() => Name;
    }
}

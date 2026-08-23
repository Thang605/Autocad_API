using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;
using MyFirstProject.Extensions;
using WinFormsLabel = System.Windows.Forms.Label;
using DrawingFont = System.Drawing.Font;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;
using Color = System.Drawing.Color;
using ContentAlignment = System.Drawing.ContentAlignment;
using FontStyle = System.Drawing.FontStyle;

namespace MyFirstProject.Civil_Tool
{
    /// <summary>
    /// Form cho lệnh CTC_AddAllSection
    /// Cho phép chọn nhiều corridor hoặc pick corridor từ model,
    /// hiển thị sample line group cho mỗi corridor và thêm section.
    /// </summary>
    public class AddAllSectionForm : Form
    {
        // ===== Controls =====
        private GroupBox grpCorridors;
        private ListBox lstCorridors;
        private Button btnPickCorridor;
        private Button btnSelectAll;
        private Button btnClearSelection;

        private GroupBox grpSampleLineGroups;
        private DataGridView dgvSampleLineInfo;

        private GroupBox grpOptions;
        private CheckBox chkRebuildCorridor;

        private Button btnOK;
        private Button btnCancel;

        // ===== Properties trả về =====
        public bool FormAccepted { get; private set; } = false;
        public List<CorridorSectionInfo> SelectedCorridors { get; private set; } = new List<CorridorSectionInfo>();
        public bool RebuildAfterAdd { get; private set; } = true;

        public AddAllSectionForm()
        {
            InitializeComponent();
            LoadAvailableCorridors();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Form settings
            this.Text = "Add All Section - Thêm Section cho Corridor";
            this.ClientSize = new Size(620, 520);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            int yPos = 8;

            // ========== Title ==========
            var lblTitle = new WinFormsLabel
            {
                Text = "ADD ALL SECTION",
                Font = new DrawingFont("Microsoft Sans Serif", 13F, FontStyle.Bold),
                Location = new Point(12, yPos),
                Size = new Size(596, 30),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(0, 80, 160)
            };
            yPos += 35;

            // ========== Group 1: Chọn Corridors ==========
            grpCorridors = new GroupBox
            {
                Text = "1. Chọn Corridor(s)",
                Location = new Point(12, yPos),
                Size = new Size(596, 160),
                Font = new DrawingFont("Microsoft Sans Serif", 9F, FontStyle.Bold)
            };

            var lblHint = new WinFormsLabel
            {
                Text = "Danh sách corridor trong bản vẽ (giữ Ctrl để chọn nhiều):",
                Location = new Point(10, 22),
                Size = new Size(400, 18),
                Font = new DrawingFont("Microsoft Sans Serif", 8.5F, FontStyle.Regular),
                ForeColor = Color.DimGray
            };

            lstCorridors = new ListBox
            {
                Location = new Point(10, 42),
                Size = new Size(480, 108),
                SelectionMode = SelectionMode.MultiExtended,
                Font = new DrawingFont("Microsoft Sans Serif", 9F, FontStyle.Regular)
            };
            lstCorridors.SelectedIndexChanged += LstCorridors_SelectedIndexChanged;

            btnPickCorridor = new Button
            {
                Text = "Pick 🖱",
                Location = new Point(500, 42),
                Size = new Size(85, 28),
                Font = new DrawingFont("Microsoft Sans Serif", 8.5F, FontStyle.Regular)
            };
            btnPickCorridor.Click += BtnPickCorridor_Click;

            btnSelectAll = new Button
            {
                Text = "Chọn tất cả",
                Location = new Point(500, 76),
                Size = new Size(85, 28),
                Font = new DrawingFont("Microsoft Sans Serif", 8.5F, FontStyle.Regular)
            };
            btnSelectAll.Click += BtnSelectAll_Click;

            btnClearSelection = new Button
            {
                Text = "Bỏ chọn",
                Location = new Point(500, 110),
                Size = new Size(85, 28),
                Font = new DrawingFont("Microsoft Sans Serif", 8.5F, FontStyle.Regular)
            };
            btnClearSelection.Click += BtnClearSelection_Click;

            grpCorridors.Controls.AddRange(new Control[]
            {
                lblHint, lstCorridors, btnPickCorridor, btnSelectAll, btnClearSelection
            });

            yPos += 168;

            // ========== Group 2: Thông tin Sample Line Group ==========
            grpSampleLineGroups = new GroupBox
            {
                Text = "2. Thông tin Sample Line Group",
                Location = new Point(12, yPos),
                Size = new Size(596, 200),
                Font = new DrawingFont("Microsoft Sans Serif", 9F, FontStyle.Bold)
            };

            dgvSampleLineInfo = new DataGridView
            {
                Location = new Point(10, 22),
                Size = new Size(576, 168),
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new DrawingFont("Microsoft Sans Serif", 8.5F, FontStyle.Regular)
            };

            // Cấu hình cột
            dgvSampleLineInfo.Columns.Add("colCorridor", "Corridor");
            dgvSampleLineInfo.Columns.Add("colAlignment", "Alignment");
            dgvSampleLineInfo.Columns.Add("colSLGroup", "Sample Line Group");
            dgvSampleLineInfo.Columns.Add("colSLCount", "Số SL");
            dgvSampleLineInfo.Columns.Add("colStatus", "Trạng thái");

            dgvSampleLineInfo.Columns["colCorridor"].FillWeight = 25;
            dgvSampleLineInfo.Columns["colAlignment"].FillWeight = 22;
            dgvSampleLineInfo.Columns["colSLGroup"].FillWeight = 25;
            dgvSampleLineInfo.Columns["colSLCount"].FillWeight = 10;
            dgvSampleLineInfo.Columns["colStatus"].FillWeight = 18;

            // Enable alternating row colors
            dgvSampleLineInfo.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(240, 245, 255);

            grpSampleLineGroups.Controls.Add(dgvSampleLineInfo);

            yPos += 208;

            // ========== Group 3: Tuỳ chọn ==========
            grpOptions = new GroupBox
            {
                Text = "3. Tuỳ chọn",
                Location = new Point(12, yPos),
                Size = new Size(596, 50),
                Font = new DrawingFont("Microsoft Sans Serif", 9F, FontStyle.Bold)
            };

            chkRebuildCorridor = new CheckBox
            {
                Text = "Rebuild corridor sau khi thêm section",
                Location = new Point(10, 22),
                Size = new Size(280, 22),
                Checked = true,
                Font = new DrawingFont("Microsoft Sans Serif", 9F, FontStyle.Regular)
            };

            var lblNote = new WinFormsLabel
            {
                Text = "⚡ Nếu nhiều corridor, rebuild có thể mất vài phút.",
                Location = new Point(300, 24),
                Size = new Size(290, 18),
                ForeColor = Color.FromArgb(180, 100, 0),
                Font = new DrawingFont("Microsoft Sans Serif", 8F, FontStyle.Italic)
            };

            grpOptions.Controls.AddRange(new Control[] { chkRebuildCorridor, lblNote });

            yPos += 58;

            // ========== Buttons ==========
            btnOK = new Button
            {
                Text = "Thực hiện",
                Location = new Point(410, yPos),
                Size = new Size(95, 32),
                Font = new DrawingFont("Microsoft Sans Serif", 9.5F, FontStyle.Bold),
                BackColor = Color.FromArgb(0, 120, 200),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnOK.FlatAppearance.BorderSize = 0;
            btnOK.Click += BtnOK_Click;

            btnCancel = new Button
            {
                Text = "Hủy",
                Location = new Point(513, yPos),
                Size = new Size(95, 32),
                Font = new DrawingFont("Microsoft Sans Serif", 9F)
            };
            btnCancel.Click += BtnCancel_Click;

            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;

            // Add to form
            this.Controls.AddRange(new Control[]
            {
                lblTitle, grpCorridors, grpSampleLineGroups, grpOptions, btnOK, btnCancel
            });

            this.ResumeLayout(false);
        }

        // ===== Load tất cả corridor từ bản vẽ =====
        private void LoadAvailableCorridors()
        {
            try
            {
                lstCorridors.Items.Clear();

                using (var tr = A.Db.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId corridorId in A.Cdoc.CorridorCollection)
                    {
                        if (tr.GetObject(corridorId, OpenMode.ForWrite) is Corridor corridor)
                        {
                            var item = new CorridorListItem(corridor.Name ?? "Unnamed", corridorId);
                            lstCorridors.Items.Add(item);
                        }
                    }
                    tr.Commit();
                }

                if (lstCorridors.Items.Count == 0)
                {
                    MessageBox.Show("Không tìm thấy corridor nào trong bản vẽ.", "Thông báo",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi khi tải danh sách corridors: {ex.Message}");
            }
        }

        // ===== Pick corridor từ model =====
        private void BtnPickCorridor_Click(object sender, EventArgs e)
        {
            try
            {
                this.Hide();

                ObjectId corridorId = UserInput.GCorridorId("\nChọn corridor trên bản vẽ:");

                if (corridorId != ObjectId.Null)
                {
                    using (var tr = A.Db.TransactionManager.StartTransaction())
                    {
                        if (tr.GetObject(corridorId, OpenMode.ForWrite) is Corridor corridor)
                        {
                            string name = corridor.Name ?? "Unnamed";

                            // Kiểm tra đã tồn tại chưa
                            bool exists = false;
                            for (int i = 0; i < lstCorridors.Items.Count; i++)
                            {
                                if (lstCorridors.Items[i] is CorridorListItem item && item.CorridorId == corridorId)
                                {
                                    // Đã có → chọn nó
                                    lstCorridors.SetSelected(i, true);
                                    exists = true;
                                    break;
                                }
                            }

                            if (!exists)
                            {
                                var newItem = new CorridorListItem(name, corridorId);
                                int idx = lstCorridors.Items.Add(newItem);
                                lstCorridors.SetSelected(idx, true);
                            }
                        }
                        tr.Commit();
                    }
                }

                this.Show();
                this.BringToFront();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Show();
            }
        }

        private void BtnSelectAll_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < lstCorridors.Items.Count; i++)
                lstCorridors.SetSelected(i, true);
        }

        private void BtnClearSelection_Click(object sender, EventArgs e)
        {
            lstCorridors.ClearSelected();
            dgvSampleLineInfo.Rows.Clear();
            SelectedCorridors.Clear();
        }

        // ===== Khi thay đổi lựa chọn corridor → load thông tin sample line =====
        private void LstCorridors_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadSampleLineInfo();
        }

        private void LoadSampleLineInfo()
        {
            dgvSampleLineInfo.Rows.Clear();
            SelectedCorridors.Clear();

            if (lstCorridors.SelectedItems.Count == 0) return;

            try
            {
                using (var tr = A.Db.TransactionManager.StartTransaction())
                {
                    foreach (CorridorListItem selectedItem in lstCorridors.SelectedItems)
                    {
                        if (tr.GetObject(selectedItem.CorridorId, OpenMode.ForWrite) is Corridor corridor)
                        {
                            for (int bi = 0; bi < corridor.Baselines.Count; bi++)
                            {
                                Baseline baseline = corridor.Baselines[bi];
                                var alignment = tr.GetObject(baseline.AlignmentId, OpenMode.ForWrite) as Alignment;
                                if (alignment == null) continue;

                                var slGroupIds = alignment.GetSampleLineGroupIds();

                                if (slGroupIds.Count == 0)
                                {
                                    dgvSampleLineInfo.Rows.Add(
                                        corridor.Name, alignment.Name,
                                        "(Không có SL Group)", "0", "⚠ Bỏ qua");
                                    continue;
                                }

                                // Dùng group đầu tiên (hoặc có thể cho user chọn)
                                for (int gi = 0; gi < slGroupIds.Count; gi++)
                                {
                                    var slGroup = tr.GetObject(slGroupIds[gi], OpenMode.ForWrite) as SampleLineGroup;
                                    if (slGroup == null) continue;

                                    int slCount = slGroup.GetSampleLineIds().Count;
                                    string status = slCount > 0 ? "✅ Sẵn sàng" : "⚠ Rỗng";

                                    dgvSampleLineInfo.Rows.Add(
                                        corridor.Name, alignment.Name,
                                        slGroup.Name, slCount.ToString(), status);

                                    // Lưu thông tin để xử lý
                                    SelectedCorridors.Add(new CorridorSectionInfo
                                    {
                                        CorridorId = selectedItem.CorridorId,
                                        CorridorName = corridor.Name,
                                        AlignmentId = baseline.AlignmentId,
                                        SampleLineGroupId = slGroupIds[gi],
                                        SampleLineGroupName = slGroup.Name,
                                        SampleLineCount = slCount,
                                        BaselineIndex = bi
                                    });
                                }
                            }
                        }
                    }
                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi khi load thông tin sample line: {ex.Message}");
            }
        }

        // ===== OK =====
        private void BtnOK_Click(object sender, EventArgs e)
        {
            if (lstCorridors.SelectedItems.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn ít nhất một corridor.", "Cảnh báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (SelectedCorridors.Count == 0 || SelectedCorridors.All(c => c.SampleLineCount == 0))
            {
                MessageBox.Show("Không có sample line nào để thêm section.\nHãy tạo Sample Line Group trước.",
                    "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            RebuildAfterAdd = chkRebuildCorridor.Checked;
            FormAccepted = true;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        // ===== Cancel =====
        private void BtnCancel_Click(object sender, EventArgs e)
        {
            FormAccepted = false;
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }

    // ===== Helper classes =====
    public class CorridorListItem
    {
        public string Name { get; set; }
        public ObjectId CorridorId { get; set; }

        public CorridorListItem(string name, ObjectId id)
        {
            Name = name;
            CorridorId = id;
        }

        public override string ToString() => Name;
    }

    public class CorridorSectionInfo
    {
        public ObjectId CorridorId { get; set; }
        public string CorridorName { get; set; } = "";
        public ObjectId AlignmentId { get; set; }
        public ObjectId SampleLineGroupId { get; set; }
        public string SampleLineGroupName { get; set; } = "";
        public int SampleLineCount { get; set; }
        public int BaselineIndex { get; set; }
    }
}

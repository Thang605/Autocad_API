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
    /// Form cho lệnh AT_UpdateLayoutSectionview
    /// Cho phép chọn Section View Groups từ danh sách hoặc pick từ model,
    /// sau đó gọi UpdateLayout() cho các groups đã chọn.
    /// </summary>
    public class UpdateLayoutSectionviewForm : Form
    {
        // ===== Controls =====
        private GroupBox grpSectionViewGroups;
        private CheckedListBox clbSectionViewGroups;
        private Button btnPickFromModel;
        private Button btnSelectAll;
        private Button btnClearSelection;
        private Button btnRefresh;

        private GroupBox grpInfo;
        private DataGridView dgvInfo;

        private Button btnOK;
        private Button btnCancel;

        // ===== Data =====
        private List<SectionViewGroupItem> _allItems = new List<SectionViewGroupItem>();

        // ===== Properties trả về =====
        public bool FormAccepted { get; private set; } = false;
        public List<SectionViewGroupItem> SelectedItems { get; private set; } = new List<SectionViewGroupItem>();

        public UpdateLayoutSectionviewForm()
        {
            InitializeComponent();
            LoadAllSectionViewGroups();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Form settings
            this.Text = "Update Layout Section View";
            this.ClientSize = new Size(640, 500);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            int yPos = 8;

            // ========== Title ==========
            var lblTitle = new WinFormsLabel
            {
                Text = "UPDATE LAYOUT SECTION VIEW",
                Font = new DrawingFont("Microsoft Sans Serif", 13F, FontStyle.Bold),
                Location = new Point(12, yPos),
                Size = new Size(616, 30),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(0, 80, 160)
            };
            yPos += 35;

            // ========== Group 1: Chọn Section View Groups ==========
            grpSectionViewGroups = new GroupBox
            {
                Text = "1. Chọn Section View Group(s)",
                Location = new Point(12, yPos),
                Size = new Size(616, 200),
                Font = new DrawingFont("Microsoft Sans Serif", 9F, FontStyle.Bold)
            };

            var lblHint = new WinFormsLabel
            {
                Text = "Danh sách Section View Group trong bản vẽ (tick chọn để cập nhật layout):",
                Location = new Point(10, 22),
                Size = new Size(500, 18),
                Font = new DrawingFont("Microsoft Sans Serif", 8.5F, FontStyle.Regular),
                ForeColor = Color.DimGray
            };

            clbSectionViewGroups = new CheckedListBox
            {
                Location = new Point(10, 42),
                Size = new Size(490, 148),
                CheckOnClick = true,
                Font = new DrawingFont("Microsoft Sans Serif", 9F, FontStyle.Regular)
            };
            clbSectionViewGroups.ItemCheck += ClbSectionViewGroups_ItemCheck;

            btnPickFromModel = new Button
            {
                Text = "Pick 🖱",
                Location = new Point(510, 42),
                Size = new Size(95, 28),
                Font = new DrawingFont("Microsoft Sans Serif", 8.5F, FontStyle.Regular)
            };
            btnPickFromModel.Click += BtnPickFromModel_Click;

            btnSelectAll = new Button
            {
                Text = "Chọn tất cả",
                Location = new Point(510, 76),
                Size = new Size(95, 28),
                Font = new DrawingFont("Microsoft Sans Serif", 8.5F, FontStyle.Regular)
            };
            btnSelectAll.Click += BtnSelectAll_Click;

            btnClearSelection = new Button
            {
                Text = "Bỏ chọn",
                Location = new Point(510, 110),
                Size = new Size(95, 28),
                Font = new DrawingFont("Microsoft Sans Serif", 8.5F, FontStyle.Regular)
            };
            btnClearSelection.Click += BtnClearSelection_Click;

            btnRefresh = new Button
            {
                Text = "Làm mới 🔄",
                Location = new Point(510, 144),
                Size = new Size(95, 28),
                Font = new DrawingFont("Microsoft Sans Serif", 8.5F, FontStyle.Regular)
            };
            btnRefresh.Click += BtnRefresh_Click;

            grpSectionViewGroups.Controls.AddRange(new Control[]
            {
                lblHint, clbSectionViewGroups, btnPickFromModel, btnSelectAll, btnClearSelection, btnRefresh
            });

            yPos += 208;

            // ========== Group 2: Thông tin chi tiết ==========
            grpInfo = new GroupBox
            {
                Text = "2. Thông tin chi tiết",
                Location = new Point(12, yPos),
                Size = new Size(616, 190),
                Font = new DrawingFont("Microsoft Sans Serif", 9F, FontStyle.Bold)
            };

            dgvInfo = new DataGridView
            {
                Location = new Point(10, 22),
                Size = new Size(596, 158),
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

            dgvInfo.Columns.Add("colAlignment", "Alignment");
            dgvInfo.Columns.Add("colSLGroup", "Sample Line Group");
            dgvInfo.Columns.Add("colSVGroup", "Section View Group");
            dgvInfo.Columns.Add("colSVCount", "Số SV");
            dgvInfo.Columns.Add("colStatus", "Trạng thái");

            dgvInfo.Columns["colAlignment"].FillWeight = 22;
            dgvInfo.Columns["colSLGroup"].FillWeight = 25;
            dgvInfo.Columns["colSVGroup"].FillWeight = 25;
            dgvInfo.Columns["colSVCount"].FillWeight = 10;
            dgvInfo.Columns["colStatus"].FillWeight = 18;

            dgvInfo.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(240, 245, 255);

            grpInfo.Controls.Add(dgvInfo);

            yPos += 198;

            // ========== Buttons ==========
            btnOK = new Button
            {
                Text = "Thực hiện",
                Location = new Point(430, yPos),
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
                Location = new Point(533, yPos),
                Size = new Size(95, 32),
                Font = new DrawingFont("Microsoft Sans Serif", 9F)
            };
            btnCancel.Click += BtnCancel_Click;

            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;

            // Add to form
            this.Controls.AddRange(new Control[]
            {
                lblTitle, grpSectionViewGroups, grpInfo, btnOK, btnCancel
            });

            this.ResumeLayout(false);
        }

        // ===== Load tất cả Section View Groups từ bản vẽ =====
        private void LoadAllSectionViewGroups()
        {
            try
            {
                clbSectionViewGroups.Items.Clear();
                _allItems.Clear();
                dgvInfo.Rows.Clear();

                using (var tr = A.Db.TransactionManager.StartTransaction())
                {
                    // Duyệt tất cả alignment trong bản vẽ
                    ObjectIdCollection alignmentIds = A.Cdoc.GetAlignmentIds();

                    foreach (ObjectId alignmentId in alignmentIds)
                    {
                        var alignment = tr.GetObject(alignmentId, OpenMode.ForWrite) as Alignment;
                        if (alignment == null) continue;

                        ObjectIdCollection slGroupIds = alignment.GetSampleLineGroupIds();
                        if (slGroupIds.Count == 0) continue;

                        foreach (ObjectId slGroupId in slGroupIds)
                        {
                            var slGroup = tr.GetObject(slGroupId, OpenMode.ForWrite) as SampleLineGroup;
                            if (slGroup == null) continue;

                            SectionViewGroupCollection svGroups = slGroup.SectionViewGroups;
                            if (svGroups.Count == 0) continue;

                            int svgIndex = 0;
                            foreach (SectionViewGroup svGroup in svGroups)
                            {
                                int svCount = svGroup.GetSectionViewIds().Count;

                                var item = new SectionViewGroupItem
                                {
                                    AlignmentName = alignment.Name ?? "",
                                    SampleLineGroupName = slGroup.Name ?? "",
                                    SampleLineGroupId = slGroupId,
                                    SectionViewGroupName = svGroup.Name ?? "",
                                    SectionViewGroupIndex = svgIndex,
                                    SectionViewCount = svCount
                                };

                                _allItems.Add(item);
                                clbSectionViewGroups.Items.Add(item, false);
                                svgIndex++;
                            }
                        }
                    }

                    tr.Commit();
                }

                if (_allItems.Count == 0)
                {
                    MessageBox.Show("Không tìm thấy Section View Group nào trong bản vẽ.",
                        "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi khi tải danh sách Section View Groups: {ex.Message}");
            }
        }

        // ===== Pick từ model =====
        private void BtnPickFromModel_Click(object sender, EventArgs e)
        {
            try
            {
                this.Hide();

                ObjectId sectionViewId = UserInput.GSectionView("\nChọn 1 cắt ngang (Section View) trên bản vẽ:");

                if (sectionViewId != ObjectId.Null)
                {
                    using (var tr = A.Db.TransactionManager.StartTransaction())
                    {
                        var sectionView = tr.GetObject(sectionViewId, OpenMode.ForWrite) as SectionView;
                        if (sectionView == null)
                        {
                            MessageBox.Show("Đối tượng không phải Section View.", "Cảnh báo",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            tr.Commit();
                            this.Show();
                            return;
                        }

                        // Tìm SectionViewGroup chứa section view này
                        var sampleLine = tr.GetObject(sectionView.SampleLineId, OpenMode.ForWrite) as SampleLine;
                        if (sampleLine == null) { tr.Commit(); this.Show(); return; }

                        ObjectId slGroupId = sampleLine.GroupId;
                        var slGroup = tr.GetObject(slGroupId, OpenMode.ForWrite) as SampleLineGroup;
                        if (slGroup == null) { tr.Commit(); this.Show(); return; }

                        SectionViewGroupCollection svGroups = slGroup.SectionViewGroups;
                        string targetSvgName = "";
                        int targetSvgIndex = -1;

                        int idx = 0;
                        foreach (SectionViewGroup svGroup in svGroups)
                        {
                            if (svGroup.GetSectionViewIds().Contains(sectionViewId))
                            {
                                targetSvgName = svGroup.Name ?? "";
                                targetSvgIndex = idx;
                                break;
                            }
                            idx++;
                        }

                        if (targetSvgIndex >= 0)
                        {
                            // Tìm trong danh sách và tick
                            bool found = false;
                            for (int i = 0; i < clbSectionViewGroups.Items.Count; i++)
                            {
                                if (clbSectionViewGroups.Items[i] is SectionViewGroupItem item
                                    && item.SampleLineGroupId == slGroupId
                                    && item.SectionViewGroupIndex == targetSvgIndex)
                                {
                                    clbSectionViewGroups.SetItemChecked(i, true);
                                    found = true;
                                    break;
                                }
                            }

                            if (!found)
                            {
                                // Chưa có trong list → reload rồi tick
                                LoadAllSectionViewGroups();
                                for (int i = 0; i < clbSectionViewGroups.Items.Count; i++)
                                {
                                    if (clbSectionViewGroups.Items[i] is SectionViewGroupItem item
                                        && item.SampleLineGroupId == slGroupId
                                        && item.SectionViewGroupIndex == targetSvgIndex)
                                    {
                                        clbSectionViewGroups.SetItemChecked(i, true);
                                        break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            MessageBox.Show("Không tìm thấy Section View Group chứa cắt ngang này.",
                                "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }

                        tr.Commit();
                    }
                }

                this.Show();
                this.BringToFront();
                UpdateInfoGrid();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.Show();
            }
        }

        private void BtnSelectAll_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < clbSectionViewGroups.Items.Count; i++)
                clbSectionViewGroups.SetItemChecked(i, true);
            UpdateInfoGrid();
        }

        private void BtnClearSelection_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < clbSectionViewGroups.Items.Count; i++)
                clbSectionViewGroups.SetItemChecked(i, false);
            UpdateInfoGrid();
        }

        private void BtnRefresh_Click(object sender, EventArgs e)
        {
            LoadAllSectionViewGroups();
        }

        private void ClbSectionViewGroups_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            // Dùng BeginInvoke để delay update vì ItemCheck fire trước khi state thực sự thay đổi
            this.BeginInvoke(new Action(() => UpdateInfoGrid()));
        }

        private void UpdateInfoGrid()
        {
            dgvInfo.Rows.Clear();
            SelectedItems.Clear();

            for (int i = 0; i < clbSectionViewGroups.Items.Count; i++)
            {
                if (clbSectionViewGroups.GetItemChecked(i)
                    && clbSectionViewGroups.Items[i] is SectionViewGroupItem item)
                {
                    string status = item.SectionViewCount > 0 ? "✅ Sẵn sàng" : "⚠ Rỗng";
                    dgvInfo.Rows.Add(
                        item.AlignmentName,
                        item.SampleLineGroupName,
                        item.SectionViewGroupName,
                        item.SectionViewCount.ToString(),
                        status);

                    SelectedItems.Add(item);
                }
            }
        }

        // ===== OK =====
        private void BtnOK_Click(object sender, EventArgs e)
        {
            // Cập nhật lại danh sách selected
            SelectedItems.Clear();
            for (int i = 0; i < clbSectionViewGroups.Items.Count; i++)
            {
                if (clbSectionViewGroups.GetItemChecked(i)
                    && clbSectionViewGroups.Items[i] is SectionViewGroupItem item)
                {
                    SelectedItems.Add(item);
                }
            }

            if (SelectedItems.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn ít nhất một Section View Group.", "Cảnh báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

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

    // ===== Helper class =====
    public class SectionViewGroupItem
    {
        public string AlignmentName { get; set; } = "";
        public string SampleLineGroupName { get; set; } = "";
        public ObjectId SampleLineGroupId { get; set; }
        public string SectionViewGroupName { get; set; } = "";
        public int SectionViewGroupIndex { get; set; }
        public int SectionViewCount { get; set; }

        public override string ToString()
        {
            return $"{AlignmentName} ► {SampleLineGroupName} ► {SectionViewGroupName} ({SectionViewCount} SV)";
        }
    }
}

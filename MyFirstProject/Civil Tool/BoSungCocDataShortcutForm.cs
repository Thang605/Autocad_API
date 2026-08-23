// Form cho lệnh CTS_BoSung_Coc_DataShortcut
// Hiển thị các tuyến đường và các cọc sẽ được bổ sung từ file nguồn (Data Shortcut)
//
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.DatabaseServices;

using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsFont = System.Drawing.Font;
using WinFormsPoint = System.Drawing.Point;

namespace MyFirstProject.Civil_Tool
{
    // ===== Data Classes =====

    /// <summary>
    /// Thông tin so sánh cho một tuyến đường (Alignment)
    /// </summary>
    public class AlignmentCompareData
    {
        public string AlignmentName { get; set; } = "";
        public ObjectId AlignmentId { get; set; } = ObjectId.Null;
        public ObjectId TargetGroupId { get; set; } = ObjectId.Null;
        public string TargetGroupName { get; set; } = "";
        public List<SampleLineToAdd> MissingSampleLines { get; set; } = new();
    }

    /// <summary>
    /// Thông tin một cọc (SampleLine) cần bổ sung
    /// </summary>
    public class SampleLineToAdd
    {
        public string Name { get; set; } = "";
        public double Station { get; set; }
        public bool IsSelected { get; set; } = true;
    }

    // ===== Form =====

    /// <summary>
    /// Form hiển thị kết quả so sánh cọc giữa file nguồn và file hiện tại.
    /// Cho phép chọn các cọc cần bổ sung.
    /// </summary>
    public class BoSungCocDataShortcutForm : Form
    {
        // Properties
        public bool FormAccepted { get; private set; } = false;
        public List<AlignmentCompareData> CompareResults { get; private set; }

        // Controls
        private WinFormsLabel lblTitle = null!;
        private WinFormsLabel lblSourceInfo = null!;
        private GroupBox grpResults = null!;
        private TreeView treeView = null!;
        private WinFormsLabel lblSummary = null!;
        private Button btnSelectAll = null!;
        private Button btnDeselectAll = null!;
        private Button btnOK = null!;
        private Button btnCancel = null!;

        public BoSungCocDataShortcutForm(List<AlignmentCompareData> results, string sourcePath)
        {
            CompareResults = results;
            InitializeComponent(sourcePath);
            PopulateTreeView();
            UpdateSummary();
        }

        private void InitializeComponent(string sourcePath)
        {
            var standardFont = new WinFormsFont("Segoe UI", 10F, FontStyle.Regular);
            var boldFont = new WinFormsFont("Segoe UI", 10F, FontStyle.Bold);
            var titleFont = new WinFormsFont("Segoe UI", 14F, FontStyle.Bold);
            var infoFont = new WinFormsFont("Segoe UI", 9F, FontStyle.Italic);

            this.SuspendLayout();

            // Form settings
            this.Text = "Bổ Sung Cọc Từ Data Shortcut";
            this.Size = new Size(650, 580);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Font = standardFont;

            int currentY = 15;
            int leftMargin = 20;
            int contentWidth = 595;

            // Title
            this.lblTitle = new WinFormsLabel
            {
                Text = "BỔ SUNG CỌC TỪ DATA SHORTCUT",
                Font = titleFont,
                Location = new WinFormsPoint(leftMargin, currentY),
                Size = new Size(contentWidth, 35),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(0, 102, 204)
            };
            currentY += 40;

            // Source info
            string sourceDisplay = string.IsNullOrEmpty(sourcePath)
                ? "Không xác định được file nguồn"
                : System.IO.Path.GetFileName(sourcePath);

            this.lblSourceInfo = new WinFormsLabel
            {
                Text = $"📁 File nguồn: {sourceDisplay}",
                Font = infoFont,
                Location = new WinFormsPoint(leftMargin, currentY),
                Size = new Size(contentWidth, 22),
                ForeColor = Color.FromArgb(80, 80, 80)
            };
            currentY += 30;

            // Results GroupBox
            this.grpResults = new GroupBox
            {
                Text = "Kết quả so sánh - Cọc cần bổ sung",
                Font = boldFont,
                Location = new WinFormsPoint(leftMargin, currentY),
                Size = new Size(contentWidth, 350),
                ForeColor = Color.FromArgb(0, 51, 153)
            };

            // TreeView inside GroupBox
            this.treeView = new TreeView
            {
                Location = new WinFormsPoint(15, 25),
                Size = new Size(contentWidth - 30, 310),
                CheckBoxes = true,
                Font = standardFont,
                BorderStyle = BorderStyle.FixedSingle,
                ShowLines = true,
                ShowPlusMinus = true,
                ShowRootLines = true,
                FullRowSelect = true,
                ItemHeight = 24
            };
            this.treeView.AfterCheck += TreeView_AfterCheck;
            this.grpResults.Controls.Add(this.treeView);
            currentY += 360;

            // Summary
            this.lblSummary = new WinFormsLabel
            {
                Text = "Tổng cộng: 0 cọc sẽ được bổ sung vào 0 tuyến đường",
                Font = boldFont,
                Location = new WinFormsPoint(leftMargin, currentY),
                Size = new Size(contentWidth, 28),
                ForeColor = Color.FromArgb(0, 100, 0),
                TextAlign = ContentAlignment.MiddleLeft
            };
            currentY += 35;

            // Buttons
            this.btnSelectAll = new Button
            {
                Text = "Chọn tất cả",
                Font = standardFont,
                Location = new WinFormsPoint(leftMargin, currentY),
                Size = new Size(120, 35)
            };
            this.btnSelectAll.Click += BtnSelectAll_Click;

            this.btnDeselectAll = new Button
            {
                Text = "Bỏ chọn",
                Font = standardFont,
                Location = new WinFormsPoint(leftMargin + 130, currentY),
                Size = new Size(120, 35)
            };
            this.btnDeselectAll.Click += BtnDeselectAll_Click;

            this.btnOK = new Button
            {
                Text = "OK",
                Font = boldFont,
                Location = new WinFormsPoint(leftMargin + 375, currentY),
                Size = new Size(100, 35),
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            this.btnOK.FlatAppearance.BorderSize = 0;
            this.btnOK.Click += BtnOK_Click;

            this.btnCancel = new Button
            {
                Text = "Hủy",
                Font = standardFont,
                Location = new WinFormsPoint(leftMargin + 490, currentY),
                Size = new Size(100, 35)
            };
            this.btnCancel.Click += BtnCancel_Click;

            // Add all controls to form
            this.Controls.AddRange(new Control[]
            {
                lblTitle,
                lblSourceInfo,
                grpResults,
                lblSummary,
                btnSelectAll,
                btnDeselectAll,
                btnOK,
                btnCancel
            });

            this.ResumeLayout(false);
        }

        private void PopulateTreeView()
        {
            treeView.Nodes.Clear();

            for (int i = 0; i < CompareResults.Count; i++)
            {
                var alignData = CompareResults[i];
                if (alignData.MissingSampleLines.Count == 0) continue;

                // Parent node: Alignment name
                string parentText = $"🛣️ {alignData.AlignmentName}  →  Nhóm: {alignData.TargetGroupName}  ({alignData.MissingSampleLines.Count} cọc)";
                TreeNode alignNode = new TreeNode(parentText)
                {
                    Tag = i,
                    Checked = true,
                    NodeFont = new WinFormsFont("Segoe UI", 10F, FontStyle.Bold)
                };

                // Child nodes: Missing sample lines
                for (int j = 0; j < alignData.MissingSampleLines.Count; j++)
                {
                    var sl = alignData.MissingSampleLines[j];
                    string childText = $"{sl.Name}   -   Km{sl.Station / 1000:F3}   (Station: {sl.Station:F2})";
                    TreeNode slNode = new TreeNode(childText)
                    {
                        Tag = new int[] { i, j },
                        Checked = sl.IsSelected
                    };
                    alignNode.Nodes.Add(slNode);
                }

                treeView.Nodes.Add(alignNode);
            }

            treeView.ExpandAll();
        }

        private bool _suppressAfterCheck = false;

        private void TreeView_AfterCheck(object? sender, TreeViewEventArgs e)
        {
            if (_suppressAfterCheck) return;
            _suppressAfterCheck = true;

            try
            {
                TreeNode node = e.Node;

                // If parent node is checked/unchecked, cascade to children
                if (node.Tag is int alignIndex)
                {
                    foreach (TreeNode child in node.Nodes)
                    {
                        child.Checked = node.Checked;
                        if (child.Tag is int[] indices && indices.Length == 2)
                        {
                            CompareResults[indices[0]].MissingSampleLines[indices[1]].IsSelected = node.Checked;
                        }
                    }
                }
                // If child node is checked/unchecked, update the data
                else if (node.Tag is int[] childIndices && childIndices.Length == 2)
                {
                    CompareResults[childIndices[0]].MissingSampleLines[childIndices[1]].IsSelected = node.Checked;

                    // Update parent if all children unchecked
                    TreeNode parent = node.Parent;
                    if (parent != null)
                    {
                        bool anyChecked = false;
                        foreach (TreeNode sibling in parent.Nodes)
                        {
                            if (sibling.Checked) { anyChecked = true; break; }
                        }
                        parent.Checked = anyChecked;
                    }
                }

                UpdateSummary();
            }
            finally
            {
                _suppressAfterCheck = false;
            }
        }

        private void UpdateSummary()
        {
            int totalSL = 0;
            int totalAlign = 0;

            foreach (var data in CompareResults)
            {
                int selected = data.MissingSampleLines.Count(x => x.IsSelected);
                if (selected > 0)
                {
                    totalSL += selected;
                    totalAlign++;
                }
            }

            lblSummary.Text = $"Tổng cộng: {totalSL} cọc sẽ được bổ sung vào {totalAlign} tuyến đường";

            if (totalSL == 0)
            {
                lblSummary.ForeColor = Color.FromArgb(180, 0, 0);
            }
            else
            {
                lblSummary.ForeColor = Color.FromArgb(0, 100, 0);
            }
        }

        private void BtnSelectAll_Click(object? sender, EventArgs e)
        {
            _suppressAfterCheck = true;
            foreach (TreeNode node in treeView.Nodes)
            {
                node.Checked = true;
                foreach (TreeNode child in node.Nodes)
                {
                    child.Checked = true;
                }
            }
            foreach (var data in CompareResults)
            {
                foreach (var sl in data.MissingSampleLines)
                {
                    sl.IsSelected = true;
                }
            }
            _suppressAfterCheck = false;
            UpdateSummary();
        }

        private void BtnDeselectAll_Click(object? sender, EventArgs e)
        {
            _suppressAfterCheck = true;
            foreach (TreeNode node in treeView.Nodes)
            {
                node.Checked = false;
                foreach (TreeNode child in node.Nodes)
                {
                    child.Checked = false;
                }
            }
            foreach (var data in CompareResults)
            {
                foreach (var sl in data.MissingSampleLines)
                {
                    sl.IsSelected = false;
                }
            }
            _suppressAfterCheck = false;
            UpdateSummary();
        }

        private void BtnOK_Click(object? sender, EventArgs e)
        {
            int totalSelected = CompareResults.Sum(x => x.MissingSampleLines.Count(sl => sl.IsSelected));
            if (totalSelected == 0)
            {
                MessageBox.Show("Vui lòng chọn ít nhất 1 cọc cần bổ sung!", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            FormAccepted = true;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void BtnCancel_Click(object? sender, EventArgs e)
        {
            FormAccepted = false;
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        /// <summary>
        /// Lấy danh sách các cọc đã chọn để bổ sung, nhóm theo alignment
        /// </summary>
        public List<AlignmentCompareData> GetSelectedData()
        {
            return CompareResults
                .Where(x => x.MissingSampleLines.Any(sl => sl.IsSelected))
                .Select(x => new AlignmentCompareData
                {
                    AlignmentName = x.AlignmentName,
                    AlignmentId = x.AlignmentId,
                    TargetGroupId = x.TargetGroupId,
                    TargetGroupName = x.TargetGroupName,
                    MissingSampleLines = x.MissingSampleLines.Where(sl => sl.IsSelected).ToList()
                })
                .ToList();
        }
    }
}

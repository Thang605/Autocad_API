// Lệnh gộp: Xuất Toạ Độ Cọc (gộp CTS_TaoBang_ToaDoCoc, ToaDoCoc2, ToaDoCoc3)
// Hiển thị Form cho user chọn nhiều Alignment + SampleLineGroup
// TreeView phân loại Alignment, checkbox chọn nhóm cọc
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;
using ATable = Autodesk.AutoCAD.DatabaseServices.Table;

using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.ApplicationServices;
using CivSurface = Autodesk.Civil.DatabaseServices.TinSurface;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Label = System.Windows.Forms.Label;
using MyFirstProject.Extensions;

[assembly: CommandClass(typeof(Civil3DCsharp.CTS_XuatToaDocCoc_Commands))]

namespace Civil3DCsharp
{
    // ===================== DATA CLASSES =====================
    public class AlignmentData
    {
        public ObjectId Id { get; set; }
        public string Name { get; set; } = "";
        public string TypeName { get; set; } = "";
        public List<GroupData> Groups { get; set; } = new();
    }

    public class GroupData
    {
        public ObjectId AlignmentId { get; set; }
        public string AlignmentName { get; set; } = "";
        public ObjectId GroupId { get; set; }
        public string GroupName { get; set; } = "";
        public int SampleLineCount { get; set; }
    }

    // ===================== COMMAND =====================
    public class CTS_XuatToaDocCoc_Commands
    {
        [CommandMethod("CTS_XuatToaDocCoc")]
        public static void CTSXuatToaDocCoc()
        {
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                CivilDocument civilDoc = CivilApplication.ActiveDocument;

                // 1. Thu thập danh sách Alignment + phân loại + Sample Line Group
                var alignDataList = new List<AlignmentData>();

                foreach (ObjectId alignId in civilDoc.GetAlignmentIds())
                {
                    Alignment? align = tr.GetObject(alignId, OpenMode.ForRead) as Alignment;
                    if (align == null) continue;

                    var groups = new List<GroupData>();
                    foreach (ObjectId gId in align.GetSampleLineGroupIds())
                    {
                        SampleLineGroup? grp = tr.GetObject(gId, OpenMode.ForRead) as SampleLineGroup;
                        if (grp != null)
                        {
                            groups.Add(new GroupData
                            {
                                AlignmentId = alignId,
                                AlignmentName = align.Name,
                                GroupId = gId,
                                GroupName = grp.Name,
                                SampleLineCount = grp.GetSampleLineIds().Count
                            });
                        }
                    }
                    if (groups.Count == 0) continue;

                    string typeName = align.AlignmentType switch
                    {
                        AlignmentType.Centerline => "Tim tuyến (Centerline)",
                        AlignmentType.Offset => "Offset",
                        AlignmentType.CurbReturn => "Đường cong rẽ (Curb Return)",
                        _ => align.AlignmentType.ToString()
                    };

                    alignDataList.Add(new AlignmentData
                    {
                        Id = alignId,
                        Name = align.Name,
                        TypeName = typeName,
                        Groups = groups
                    });
                }

                if (alignDataList.Count == 0)
                {
                    A.Ed.WriteMessage("\nKhông tìm thấy Alignment nào có chứa Sample Line Group.");
                    return;
                }

                // 2. Thu thập danh sách Surface
                var surfaceDict = new Dictionary<ObjectId, string>();
                foreach (ObjectId sId in civilDoc.GetSurfaceIds())
                {
                    try
                    {
                        Autodesk.Civil.DatabaseServices.Surface? surf = tr.GetObject(sId, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Surface;
                        if (surf != null)
                            surfaceDict.Add(sId, surf.Name);
                    }
                    catch { }
                }

                // 3. Hiển thị Form
                var form = new XuatToaDocCocForm(alignDataList, surfaceDict);
                var result = Application.ShowModalDialog(form);
                if (result != DialogResult.OK)
                {
                    A.Ed.WriteMessage("\nĐã hủy lệnh.");
                    return;
                }

                // 4. Lấy kết quả
                List<GroupData> selectedGroups = form.SelectedGroups;
                bool includeStation = form.IncludeStation;
                bool includeElevation = form.IncludeElevation;
                bool drawPolyline = form.DrawPolyline;
                ObjectId selectedSurfaceId = form.SelectedSurfaceId;

                CivSurface? civSurface = null;
                if (includeElevation && selectedSurfaceId != ObjectId.Null)
                {
                    civSurface = tr.GetObject(selectedSurfaceId, OpenMode.ForRead) as CivSurface;
                }

                // 5. Chọn điểm đặt bảng đầu tiên
                Point3d basePoint = UserInput.GPoint("\n Chọn vị trí đặt bảng đầu tiên:\n");
                double horizontalSpacing = form.HorizontalSpacing;
                Point3d currentPos = basePoint;

                // 6. Xử lý từng nhóm cọc đã chọn
                int totalStakes = 0;
                int tableCount = 0;

                foreach (var gd in selectedGroups)
                {
                    Alignment? alignment = tr.GetObject(gd.AlignmentId, OpenMode.ForRead) as Alignment;
                    SampleLineGroup? group = tr.GetObject(gd.GroupId, OpenMode.ForRead) as SampleLineGroup;
                    if (alignment == null || group == null) continue;

                    // Thu thập dữ liệu cọc
                    var dataList = new List<StakeData>();
                    int stt = 1;
                    foreach (ObjectId slId in group.GetSampleLineIds())
                    {
                        SampleLine? sl = tr.GetObject(slId, OpenMode.ForWrite) as SampleLine;
                        if (sl == null) continue;

                        double station = sl.Station;
                        double easting = 0, northing = 0;
                        alignment.PointLocation(station, 0, ref easting, ref northing);

                        var data = new StakeData
                        {
                            STT = stt++,
                            Name = sl.Name.ToUpper(),
                            Easting = Math.Round(easting, 3),
                            Northing = Math.Round(northing, 3),
                            Station = Math.Round(station, 3)
                        };

                        if (includeElevation && civSurface != null)
                        {
                            try { data.Elevation = Math.Round(civSurface.FindElevationAtXY(easting, northing), 3); }
                            catch { data.Elevation = 0; }
                        }

                        dataList.Add(data);
                    }

                    if (dataList.Count == 0) continue;

                    // Tạo bảng
                    string tableTitle = alignment.Name;
                    if (selectedGroups.Count(g => g.AlignmentId == gd.AlignmentId) > 1)
                        tableTitle += $" [{gd.GroupName}]";

                    double tableWidth = CreateStakeTable(tr, tableTitle, dataList, includeStation, includeElevation, currentPos);
                    tableCount++;
                    totalStakes += dataList.Count;

                    // Tính vị trí bảng tiếp theo: dịch sang phải = tableWidth + spacing
                    currentPos = new Point3d(currentPos.X + tableWidth + horizontalSpacing, currentPos.Y, 0);

                    // Vẽ polyline kiểm tra
                    if (drawPolyline)
                    {
                        string[] eastings = new string[dataList.Count + 1];
                        string[] northings = new string[dataList.Count + 1];
                        for (int i = 0; i < dataList.Count; i++)
                        {
                            eastings[i + 1] = dataList[i].Easting.ToString();
                            northings[i + 1] = dataList[i].Northing.ToString();
                        }
                        UtilitiesCAD.CreateOpenPolyline(dataList.Count + 1, eastings, northings);
                    }
                }

                tr.Commit();
                A.Ed.WriteMessage($"\n Hoàn tất! Đã xuất {tableCount} bảng toạ độ, tổng {totalStakes} cọc.");
            }
            catch (System.Exception e)
            {
                A.Ed.WriteMessage("\nLỗi: " + e.Message);
            }
        }

        /// <summary>Tạo bảng toạ độ cọc tại vị trí chỉ định. Trả về tổng chiều rộng bảng.</summary>
        private static double CreateStakeTable(Transaction tr, string alignmentName, List<StakeData> dataList, bool includeStation, bool includeElevation, Point3d position)
        {
            int numCols = 4;
            if (includeStation) numCols++;
            if (includeElevation) numCols++;
            int numRows = dataList.Count + 2;

            BlockTable bt = (BlockTable)tr.GetObject(A.Doc.Database.BlockTableId, OpenMode.ForRead);

            ATable table = new();
            table.SetSize(numRows, numCols);
            table.SetRowHeight(5);             // data rows mặc định
            table.Rows[0].Height = 7;          // title row
            table.Rows[1].Height = 6;          // header row
            table.SetColumnWidth(18);
            table.Columns[0].Width = 8;
            table.Position = position;

            table.Cells[0, 0].TextHeight = 2;
            table.Cells[0, 0].TextString = "Bảng tọa độ cọc: đường " + alignmentName;
            table.Cells[0, 0].Alignment = CellAlignment.MiddleCenter;

            int col = 0;
            SetHeader(table, 1, col++, "STT");
            SetHeader(table, 1, col++, "Tên cọc");
            SetHeader(table, 1, col++, "Tọa độ Y");
            SetHeader(table, 1, col++, "Tọa độ X");
            if (includeStation) SetHeader(table, 1, col++, "Lý trình");
            if (includeElevation) SetHeader(table, 1, col++, "Cao độ");

            for (int i = 0; i < dataList.Count; i++)
            {
                int row = i + 2;
                col = 0;
                SetCell(table, row, col++, dataList[i].STT.ToString());
                SetCell(table, row, col++, dataList[i].Name);
                SetCell(table, row, col++, dataList[i].Easting.ToString());
                SetCell(table, row, col++, dataList[i].Northing.ToString());
                if (includeStation) SetCell(table, row, col++, dataList[i].Station.ToString());
                if (includeElevation) SetCell(table, row, col++, dataList[i].Elevation.ToString());
            }

            table.GenerateLayout();

            // Set specific row heights after layout generation to prevent overrides
            table.SetRowHeight(5);             // Cập nhật lại toàn bảng trước (data = 5)
            table.Rows[0].Height = 7;          // Title = 7
            table.Rows[1].Height = 6;          // Header = 6

            // Tính tổng chiều rộng bảng
            double totalWidth = 0;
            for (int c = 0; c < numCols; c++)
                totalWidth += table.Columns[c].Width;

            BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            btr.AppendEntity(table);
            tr.AddNewlyCreatedDBObject(table, true);
            return totalWidth;
        }

        private static void SetHeader(ATable table, int row, int col, string text)
        {
            table.Cells[row, col].TextHeight = 2;
            table.Cells[row, col].TextString = text;
            table.Cells[row, col].Alignment = CellAlignment.MiddleCenter;
        }

        private static void SetCell(ATable table, int row, int col, string text)
        {
            table.Cells[row, col].TextHeight = 2;
            table.Cells[row, col].TextString = text;
            table.Cells[row, col].Alignment = CellAlignment.MiddleCenter;
        }

        private class StakeData
        {
            public int STT { get; set; }
            public string Name { get; set; } = "";
            public double Easting { get; set; }
            public double Northing { get; set; }
            public double Station { get; set; }
            public double Elevation { get; set; }
        }
    }

    // ===================== FORM =====================
    public class XuatToaDocCocForm : Form
    {
        public List<GroupData> SelectedGroups { get; private set; } = new();
        public bool IncludeStation { get; private set; }
        public bool IncludeElevation { get; private set; }
        public bool DrawPolyline { get; private set; }
        public ObjectId SelectedSurfaceId { get; private set; }
        public double HorizontalSpacing { get; private set; } = 10;

        private GroupBox gbTree;
        private TreeView tvAlignments;
        private Button btnSelectAll;
        private Button btnDeselectAll;
        private Label lblCount;
        private GroupBox gbColumns;
        private CheckBox chkStation;
        private CheckBox chkElevation;
        private Label lblSurface;
        private ComboBox cboSurface;
        private GroupBox gbOptions;
        private CheckBox chkPolyline;
        private Label lblSpacing;
        private NumericUpDown numSpacing;
        private Button btnOK;
        private Button btnCancel;

        private List<AlignmentData> _alignDataList;
        private Dictionary<ObjectId, string> _surfaces;

        public XuatToaDocCocForm(List<AlignmentData> alignDataList, Dictionary<ObjectId, string> surfaces)
        {
            _alignDataList = alignDataList;
            _surfaces = surfaces;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Xuất Toạ Độ Cọc";
            this.Size = new System.Drawing.Size(520, 590);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // ===== TreeView Alignment + Group =====
            gbTree = new GroupBox
            {
                Text = "Chọn tuyến đường và nhóm cọc cần xuất (tick ✓ để chọn)",
                Location = new System.Drawing.Point(10, 10),
                Size = new System.Drawing.Size(485, 240)
            };

            tvAlignments = new TreeView
            {
                Location = new System.Drawing.Point(10, 20),
                Size = new System.Drawing.Size(465, 180),
                CheckBoxes = true,
                Font = new System.Drawing.Font("Segoe UI", 9.5f),
                ShowLines = true,
                ShowPlusMinus = true,
                ShowRootLines = true,
                HideSelection = false,
                ItemHeight = 22
            };

            // Nhóm theo AlignmentType
            var typeGroups = _alignDataList
                .GroupBy(a => a.TypeName)
                .OrderBy(g => g.Key);

            foreach (var typeGroup in typeGroups)
            {
                // Node loại alignment (level 0) - ví dụ: "Tim tuyến (Centerline)"
                TreeNode typeNode = new TreeNode($"📁 {typeGroup.Key}")
                {
                    NodeFont = new System.Drawing.Font("Segoe UI", 9.5f, System.Drawing.FontStyle.Bold),
                    ForeColor = System.Drawing.Color.FromArgb(0, 90, 158)
                };

                foreach (var ad in typeGroup.OrderBy(a => a.Name))
                {
                    // Node alignment (level 1)
                    TreeNode alignNode = new TreeNode($"🛣 {ad.Name}  ({ad.Groups.Count} nhóm)")
                    {
                        Tag = ad,
                        NodeFont = new System.Drawing.Font("Segoe UI", 9.5f, System.Drawing.FontStyle.Regular),
                        ForeColor = System.Drawing.Color.FromArgb(30, 30, 30)
                    };

                    foreach (var gd in ad.Groups)
                    {
                        // Node sample line group (level 2) - đây là cái user tick chọn
                        TreeNode groupNode = new TreeNode($"📋 {gd.GroupName}  ({gd.SampleLineCount} cọc)")
                        {
                            Tag = gd,
                            NodeFont = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Regular),
                            ForeColor = System.Drawing.Color.FromArgb(60, 60, 60)
                        };
                        alignNode.Nodes.Add(groupNode);
                    }

                    typeNode.Nodes.Add(alignNode);
                }

                tvAlignments.Nodes.Add(typeNode);
            }

            tvAlignments.ExpandAll();
            tvAlignments.AfterCheck += TvAlignments_AfterCheck;

            // Buttons chọn hết / bỏ hết
            btnSelectAll = new Button
            {
                Text = "Chọn hết",
                Location = new System.Drawing.Point(10, 205),
                Size = new System.Drawing.Size(80, 25),
                Font = new System.Drawing.Font("Segoe UI", 8.5f)
            };
            btnSelectAll.Click += (s, e) => SetAllChecked(true);

            btnDeselectAll = new Button
            {
                Text = "Bỏ hết",
                Location = new System.Drawing.Point(95, 205),
                Size = new System.Drawing.Size(80, 25),
                Font = new System.Drawing.Font("Segoe UI", 8.5f)
            };
            btnDeselectAll.Click += (s, e) => SetAllChecked(false);

            lblCount = new Label
            {
                Text = "Đã chọn: 0 nhóm cọc",
                Location = new System.Drawing.Point(260, 208),
                Size = new System.Drawing.Size(210, 20),
                Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold),
                ForeColor = System.Drawing.Color.FromArgb(0, 100, 0),
                TextAlign = System.Drawing.ContentAlignment.MiddleRight
            };

            gbTree.Controls.AddRange(new Control[] { tvAlignments, btnSelectAll, btnDeselectAll, lblCount });

            // ===== Column options =====
            gbColumns = new GroupBox
            {
                Text = "Cột dữ liệu xuất",
                Location = new System.Drawing.Point(10, 258),
                Size = new System.Drawing.Size(485, 140)
            };

            var lblFixed = new Label
            {
                Text = "✅ STT  |  ✅ Tên cọc  |  ✅ Toạ độ Y  |  ✅ Toạ độ X",
                Location = new System.Drawing.Point(15, 22),
                Size = new System.Drawing.Size(450, 18),
                ForeColor = System.Drawing.Color.DarkGreen
            };

            chkStation = new CheckBox
            {
                Text = "Thêm cột Lý trình (Station)",
                Location = new System.Drawing.Point(15, 48),
                Size = new System.Drawing.Size(250, 22),
                Checked = false
            };

            chkElevation = new CheckBox
            {
                Text = "Thêm cột Cao độ (Elevation từ Surface)",
                Location = new System.Drawing.Point(15, 74),
                Size = new System.Drawing.Size(300, 22),
                Checked = false
            };
            chkElevation.CheckedChanged += (s, e) => UpdateSurfaceVisibility();

            lblSurface = new Label
            {
                Text = "Chọn Surface:",
                Location = new System.Drawing.Point(35, 100),
                Size = new System.Drawing.Size(90, 20),
                Visible = false
            };

            cboSurface = new ComboBox
            {
                Location = new System.Drawing.Point(130, 97),
                Size = new System.Drawing.Size(340, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Visible = false
            };
            foreach (var kvp in _surfaces)
            {
                cboSurface.Items.Add(new ComboItem { Id = kvp.Key, Name = kvp.Value });
            }
            if (cboSurface.Items.Count > 0) cboSurface.SelectedIndex = 0;

            gbColumns.Controls.AddRange(new Control[] { lblFixed, chkStation, chkElevation, lblSurface, cboSurface });

            // ===== Extra options =====
            gbOptions = new GroupBox
            {
                Text = "Tùy chọn khác",
                Location = new System.Drawing.Point(10, 406),
                Size = new System.Drawing.Size(485, 80)
            };
            chkPolyline = new CheckBox
            {
                Text = "Vẽ Polyline kiểm tra toạ độ (trên layer Defpoints)",
                Location = new System.Drawing.Point(15, 20),
                Size = new System.Drawing.Size(450, 22),
                Checked = true
            };

            lblSpacing = new Label
            {
                Text = "Khoảng cách ngang giữa các bảng:",
                Location = new System.Drawing.Point(15, 50),
                Size = new System.Drawing.Size(220, 20)
            };
            numSpacing = new NumericUpDown
            {
                Location = new System.Drawing.Point(240, 48),
                Size = new System.Drawing.Size(70, 23),
                Minimum = 0,
                Maximum = 500,
                Value = 10,
                DecimalPlaces = 0,
                TextAlign = HorizontalAlignment.Center
            };

            gbOptions.Controls.AddRange(new Control[] { chkPolyline, lblSpacing, numSpacing });

            // ===== Buttons =====
            btnOK = new Button
            {
                Text = "OK",
                Location = new System.Drawing.Point(325, 498),
                Size = new System.Drawing.Size(80, 30)
            };
            btnOK.Click += BtnOK_Click;

            btnCancel = new Button
            {
                Text = "Hủy",
                Location = new System.Drawing.Point(415, 498),
                Size = new System.Drawing.Size(80, 30)
            };
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            this.Size = new System.Drawing.Size(520, 580);
            this.Controls.AddRange(new Control[] { gbTree, gbColumns, gbOptions, btnOK, btnCancel });
            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;
        }

        // ===== TreeView checkbox cascade logic =====
        private bool _isUpdating = false;
        private void TvAlignments_AfterCheck(object? sender, TreeViewEventArgs e)
        {
            if (_isUpdating) return;
            _isUpdating = true;

            TreeNode node = e.Node!;
            bool isChecked = node.Checked;

            // Cascade down: tick parent → tick tất cả child
            SetChildrenChecked(node, isChecked);

            // Cascade up: nếu tất cả child đã tick → tick parent
            if (node.Parent != null)
                UpdateParentCheck(node.Parent);

            UpdateCountLabel();
            _isUpdating = false;
        }

        private void SetChildrenChecked(TreeNode parent, bool isChecked)
        {
            foreach (TreeNode child in parent.Nodes)
            {
                child.Checked = isChecked;
                SetChildrenChecked(child, isChecked);
            }
        }

        private void UpdateParentCheck(TreeNode parent)
        {
            bool allChecked = true;
            bool anyChecked = false;
            foreach (TreeNode child in parent.Nodes)
            {
                if (child.Checked) anyChecked = true;
                else allChecked = false;
            }
            parent.Checked = anyChecked;
            if (parent.Parent != null)
                UpdateParentCheck(parent.Parent);
        }

        private void SetAllChecked(bool isChecked)
        {
            _isUpdating = true;
            foreach (TreeNode node in tvAlignments.Nodes)
            {
                node.Checked = isChecked;
                SetChildrenChecked(node, isChecked);
            }
            _isUpdating = false;
            UpdateCountLabel();
        }

        private void UpdateCountLabel()
        {
            int count = GetSelectedGroups().Count;
            lblCount.Text = $"Đã chọn: {count} nhóm cọc";
        }

        private List<GroupData> GetSelectedGroups()
        {
            var result = new List<GroupData>();
            CollectCheckedGroups(tvAlignments.Nodes, result);
            return result;
        }

        private void CollectCheckedGroups(TreeNodeCollection nodes, List<GroupData> result)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Checked && node.Tag is GroupData gd)
                {
                    result.Add(gd);
                }
                CollectCheckedGroups(node.Nodes, result);
            }
        }

        private void UpdateSurfaceVisibility()
        {
            bool show = chkElevation.Checked;
            lblSurface.Visible = show;
            cboSurface.Visible = show;
        }

        private void BtnOK_Click(object? sender, EventArgs e)
        {
            SelectedGroups = GetSelectedGroups();
            if (SelectedGroups.Count == 0)
            {
                MessageBox.Show("Vui lòng tick chọn ít nhất 1 nhóm cọc (📋) trong cây.", "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            IncludeStation = chkStation.Checked;
            IncludeElevation = chkElevation.Checked;
            DrawPolyline = chkPolyline.Checked;
            HorizontalSpacing = (double)numSpacing.Value;

            if (IncludeElevation)
            {
                if (cboSurface.SelectedItem == null)
                {
                    MessageBox.Show("Vui lòng chọn Surface để lấy cao độ.", "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                SelectedSurfaceId = ((ComboItem)cboSurface.SelectedItem).Id;
            }
            else
            {
                SelectedSurfaceId = ObjectId.Null;
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private class ComboItem
        {
            public ObjectId Id;
            public string Name = "";
            public override string ToString() => Name;
        }
    }
}

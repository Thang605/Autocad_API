using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using Autodesk.AutoCAD.Runtime;
using Acad = Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;

using Civil = Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.Civil.ApplicationServices;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using MyFirstProject.Extensions;

[assembly: CommandClass(typeof(Civil3DCsharp.CTS_DieuChinh_Orthogonal_Commands))]

namespace Civil3DCsharp
{
    public class CTS_DieuChinh_Orthogonal_Commands
    {
        private static int lastSelectionMode = 0; // 0 = All in file, 1 = Quét chọn, 2 = Theo nhóm, 3 = Chọn từ list

        [CommandMethod("CTS_DieuChinh_Orthogonal")]
        public static void CTSDieuChinhOrthogonal()
        {
            Dictionary<ObjectId, string> slGroups = new Dictionary<ObjectId, string>();
            List<string> handles = new List<string>();

            // Phase 1: Thu thập Sample Line handles trong Transaction
            using (Transaction tr = A.Db.TransactionManager.StartTransaction())
            {
                try
                {
                    CivilDocument civilDoc = CivilApplication.ActiveDocument;

                    foreach (ObjectId alignId in civilDoc.GetAlignmentIds())
                    {
                        Alignment? align = tr.GetObject(alignId, OpenMode.ForRead) as Alignment;
                        if (align != null)
                        {
                            foreach (ObjectId groupId in align.GetSampleLineGroupIds())
                            {
                                SampleLineGroup? grp = tr.GetObject(groupId, OpenMode.ForRead) as SampleLineGroup;
                                if (grp != null)
                                {
                                    slGroups.Add(groupId, $"{align.Name} - {grp.Name}");
                                }
                            }
                        }
                    }

                    var form = new DieuChinhOrthogonalForm(lastSelectionMode, slGroups);
                    var result = Application.ShowModalDialog(form);
                    if (result != DialogResult.OK)
                    {
                        A.Ed.WriteMessage("\nĐã hủy lệnh.");
                        return;
                    }

                    lastSelectionMode = form.SelectionMode;

                    ObjectIdCollection sampleLineIds = new ObjectIdCollection();

                    switch (lastSelectionMode)
                    {
                        case 0: // All
                            foreach (var grpId in slGroups.Keys)
                            {
                                SampleLineGroup? grp = tr.GetObject(grpId, OpenMode.ForRead) as SampleLineGroup;
                                if (grp != null)
                                {
                                    foreach (ObjectId id in grp.GetSampleLineIds()) sampleLineIds.Add(id);
                                }
                            }
                            break;
                        case 1: // Quét chọn
                            A.Ed.WriteMessage("\nQuét chọn các sample line cần điều chỉnh...");
                            sampleLineIds = UserInput.GSelectionSetWithType("Quét chọn các sample line cần thay đổi: \n", "AECC_SAMPLE_LINE");
                            break;
                        case 2: // Chọn 1 cho nhóm
                            ObjectId selectedSampleLineId = UserInput.GSampleLineId("Chọn một sample line từ nhóm cần thay đổi: \n");
                            if (selectedSampleLineId == ObjectId.Null) return;
                            SampleLine? selectedSampleLine = tr.GetObject(selectedSampleLineId, OpenMode.ForRead) as SampleLine;
                            if (selectedSampleLine == null) return;
                            SampleLineGroup? group = tr.GetObject(selectedSampleLine.GroupId, OpenMode.ForRead) as SampleLineGroup;
                            if (group == null) return;
                            sampleLineIds = group.GetSampleLineIds();
                            break;
                        case 3: // Chọn nhóm từ danh sách
                            foreach (ObjectId grpId in form.SelectedGroupIds)
                            {
                                SampleLineGroup? grp = tr.GetObject(grpId, OpenMode.ForRead) as SampleLineGroup;
                                if (grp != null)
                                {
                                    foreach (ObjectId id in grp.GetSampleLineIds()) sampleLineIds.Add(id);
                                }
                            }
                            break;
                    }

                    if (sampleLineIds.Count == 0)
                    {
                        A.Ed.WriteMessage("\nKhông có sample line nào được chọn để xử lý.");
                        return;
                    }

                    // Thu thập handles để dùng sau khi commit transaction
                    foreach (ObjectId id in sampleLineIds)
                    {
                        handles.Add(id.Handle.ToString());
                    }

                    tr.Commit();
                }
                catch (System.Exception e)
                {
                    A.Ed.WriteMessage("\nLỗi: " + e.Message);
                    return;
                }
            }

            // Phase 2: Gọi lệnh MakeSampleLineOrthogonal cho từng Sample Line qua command line
            // SendStringToExecute sẽ queue lệnh và chạy sau khi method này return
            Document doc = Application.DocumentManager.MdiActiveDocument;
            foreach (string handle in handles)
            {
                // LISP: chọn entity theo handle → set implied selection → gọi lệnh
                string lisp = $"(if (handent \"{handle}\") (progn (sssetfirst nil (ssadd (handent \"{handle}\"))) (command \"._MakeSampleLineOrthogonal\")))\n";
                doc.SendStringToExecute(lisp, true, false, false);
            }

            A.Ed.WriteMessage($"\nĐã gửi lệnh Make Orthogonal cho {handles.Count} Sample Lines.");
        }
    }

    public partial class DieuChinhOrthogonalForm : Form
    {
        public int SelectionMode { get; private set; }
        public List<ObjectId> SelectedGroupIds { get; private set; } = new List<ObjectId>();

        private RadioButton rbAll;
        private RadioButton rbScopeWindow;
        private RadioButton rbScopeGroup;
        private RadioButton rbScopeList;
        private CheckedListBox clbGroups;
        private Button btnSelectAll;
        private Button btnOK;
        private Button btnCancel;
        private GroupBox gbScope;
        private Dictionary<ObjectId, string> _slGroups;

        public DieuChinhOrthogonalForm(int defaultMode, Dictionary<ObjectId, string> slGroups)
        {
            _slGroups = slGroups;
            InitializeComponent();

#pragma warning disable CS8602
            rbAll.Checked = (defaultMode == 0);
            rbScopeWindow.Checked = (defaultMode == 1);
            rbScopeGroup.Checked = (defaultMode == 2);
            rbScopeList.Checked = (defaultMode == 3);
#pragma warning restore CS8602

            foreach (var kvp in _slGroups)
            {
                clbGroups.Items.Add(new GroupItem { Id = kvp.Key, Name = kvp.Value });
            }
            UpdateUI();
        }

        public class GroupItem
        {
            public ObjectId Id;
            public string Name = "";
            public override string ToString() => Name;
        }

        private void InitializeComponent()
        {
            this.Text = "Make Orthogonal - Sample Line";
            this.Size = new System.Drawing.Size(400, 460);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            gbScope = new GroupBox { Text = "Phạm vi áp dụng", Location = new System.Drawing.Point(10, 10), Size = new System.Drawing.Size(360, 350) };
            rbAll = new RadioButton { Text = "1. Toàn bộ bản vẽ", Location = new System.Drawing.Point(15, 25), Size = new System.Drawing.Size(300, 20) };
            rbScopeWindow = new RadioButton { Text = "2. Quét chọn các Sample Line", Location = new System.Drawing.Point(15, 50), Size = new System.Drawing.Size(300, 20) };
            rbScopeGroup = new RadioButton { Text = "3. Chọn 1 Sample Line (áp dụng toàn nhóm)", Location = new System.Drawing.Point(15, 75), Size = new System.Drawing.Size(300, 20) };
            rbScopeList = new RadioButton { Text = "4. Chọn các Nhóm từ danh sách", Location = new System.Drawing.Point(15, 100), Size = new System.Drawing.Size(300, 20) };

            rbAll.CheckedChanged += (s, e) => UpdateUI();
            rbScopeWindow.CheckedChanged += (s, e) => UpdateUI();
            rbScopeGroup.CheckedChanged += (s, e) => UpdateUI();
            rbScopeList.CheckedChanged += (s, e) => UpdateUI();

            clbGroups = new CheckedListBox { Location = new System.Drawing.Point(35, 125), Size = new System.Drawing.Size(310, 160), CheckOnClick = true };
            btnSelectAll = new Button { Text = "Chọn/Bỏ hết", Location = new System.Drawing.Point(245, 295), Size = new System.Drawing.Size(100, 25) };
            btnSelectAll.Click += (s, e) =>
            {
                if (clbGroups.Items.Count == 0) return;
                bool allChecked = (clbGroups.CheckedItems.Count == clbGroups.Items.Count);
                for (int i = 0; i < clbGroups.Items.Count; i++) clbGroups.SetItemChecked(i, !allChecked);
                clbGroups.Invalidate();
                System.Windows.Forms.Application.DoEvents();
            };

            gbScope.Controls.AddRange(new Control[] { rbAll, rbScopeWindow, rbScopeGroup, rbScopeList, clbGroups, btnSelectAll });

            btnOK = new Button { Text = "OK", Location = new System.Drawing.Point(210, 380), Size = new System.Drawing.Size(75, 25) };
            btnOK.Click += BtnOK_Click;
            btnCancel = new Button { Text = "Hủy", Location = new System.Drawing.Point(295, 380), Size = new System.Drawing.Size(75, 25) };
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            this.Controls.AddRange(new Control[] { gbScope, btnOK, btnCancel });
            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;
        }

        private void UpdateUI()
        {
            bool isList = rbScopeList.Checked;
            clbGroups.Enabled = isList;
            btnSelectAll.Enabled = isList;
        }

        private void BtnOK_Click(object? sender, EventArgs e)
        {
            if (rbAll.Checked) SelectionMode = 0;
            else if (rbScopeWindow.Checked) SelectionMode = 1;
            else if (rbScopeGroup.Checked) SelectionMode = 2;
            else
            {
                SelectionMode = 3;
                if (clbGroups.CheckedItems.Count == 0)
                {
                    MessageBox.Show("Vui lòng chọn ít nhất một Sample Line Group.", "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                foreach (object item in clbGroups.CheckedItems)
                    SelectedGroupIds.Add(((GroupItem)item).Id);
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}

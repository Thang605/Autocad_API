// (C) Copyright 2015 by  
//
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
using ATable = Autodesk.AutoCAD.DatabaseServices.Table;

using Civil = Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using Autodesk.Civil.Runtime;
using Autodesk.Civil.Settings;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.Civil.ApplicationServices;
using CivSurface = Autodesk.Civil.DatabaseServices.TinSurface;
using Section = Autodesk.Civil.DatabaseServices.Section;
using Autodesk.Civil;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Schema;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using MyFirstProject.Extensions;
//using Autodesk.Aec.DatabaseServices;
// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3DCsharp.CTS_ThayDoi_BeRong_Sampleline_Commands))]

namespace Civil3DCsharp
{
    public class CTS_ThayDoi_BeRong_Sampleline_Commands
    {
        // Static variables to remember settings for next command execution
        private static double lastLeftOffset = 50.0;
        private static double lastRightOffset = 50.0;
        private static bool lastUseInputMode = true; // true for input, false for pick from model
        private static int lastSelectionMode = 3; // 0 = quét chọn, 1 = nhặt 1 cái lấy nhóm, 2 = chọn từng cái, 3 = chọn từ danh sách nhóm

        [CommandMethod("CTS_Thay_Doi_Be_Rong_Sample_Line")]
        public static void CTSThayDoiBeRongSampleline()
        {
            // start transaction
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput uI = new();

                // Collect SampleLineGroups
                Dictionary<ObjectId, string> slGroups = new Dictionary<ObjectId, string>();
                CivilDocument civilDoc = CivilApplication.ActiveDocument;
                foreach (ObjectId alignId in civilDoc.GetAlignmentIds())
                {
                    Alignment align = tr.GetObject(alignId, OpenMode.ForRead) as Alignment;
                    if (align != null)
                    {
                        foreach (ObjectId groupId in align.GetSampleLineGroupIds())
                        {
                            SampleLineGroup grp = tr.GetObject(groupId, OpenMode.ForRead) as SampleLineGroup;
                            if (grp != null)
                            {
                                slGroups.Add(groupId, $"{align.Name} - {grp.Name}");
                            }
                        }
                    }
                }

                // Show settings form to user
                var settingsForm = new SamplelineWidthForm(lastLeftOffset, lastRightOffset, lastUseInputMode, lastSelectionMode, slGroups);
                var result = settingsForm.ShowDialog();
                
                if (result != DialogResult.OK)
                {
                    A.Ed.WriteMessage("\nLệnh đã bị hủy bởi người dùng.");
                    return;
                }

                // Get settings from form and save for next time
                lastLeftOffset = settingsForm.LeftOffset;
                lastRightOffset = settingsForm.RightOffset;
                lastUseInputMode = settingsForm.UseInputMode;
                lastSelectionMode = settingsForm.SelectionMode;

                double leftOffset = lastLeftOffset;
                double rightOffset = lastRightOffset;

                // If user chose to pick from model, get reference distances
                if (!lastUseInputMode)
                {
                    Point3d centerPoint = UserInput.GPoint("\nChọn điểm trung tâm: ");
                    Point3d leftPoint = UserInput.GPoint("\nChọn điểm biên trái: ");
                    Point3d rightPoint = UserInput.GPoint("\nChọn điểm biên phải: ");
                    
                    leftOffset = centerPoint.DistanceTo(leftPoint);
                    rightOffset = centerPoint.DistanceTo(rightPoint);
                    
                    A.Ed.WriteMessage($"\nKhoảng cách trái: {leftOffset:F2}");
                    A.Ed.WriteMessage($"\nKhoảng cách phải: {rightOffset:F2}");
                }

                // Get sample lines to modify
                ObjectIdCollection sampleLineIds;

                switch (lastSelectionMode)
                {
                    case 0: // Quét chọn các sampleline
                        A.Ed.WriteMessage("\nQuét chọn các sample line cần điều chỉnh...");
                        sampleLineIds = UserInput.GSelectionSetWithType("Quét chọn các sample line cần thay đổi bề rộng: \n", "AECC_SAMPLE_LINE");
                        break;

                    case 1: // Áp dụng cho toàn bộ nhóm (Pick)
                        ObjectId selectedSampleLineId = UserInput.GSampleLineId("Chọn một sample line từ nhóm cần thay đổi bề rộng: \n");
                        if (selectedSampleLineId == ObjectId.Null)
                        {
                            A.Ed.WriteMessage("\nKhông có sample line nào được chọn.");
                            return;
                        }

                        SampleLine? selectedSampleLine = tr.GetObject(selectedSampleLineId, OpenMode.ForRead) as SampleLine;
                        if (selectedSampleLine == null)
                        {
                            A.Ed.WriteMessage("\nKhông thể đọc sample line đã chọn.");
                            return;
                        }

                        ObjectId groupId = selectedSampleLine.GroupId;
                        SampleLineGroup? group = tr.GetObject(groupId, OpenMode.ForRead) as SampleLineGroup;
                        if (group == null)
                        {
                            A.Ed.WriteMessage("\nKhông thể đọc sample line group.");
                            return;
                        }

                        sampleLineIds = group.GetSampleLineIds();
                        A.Ed.WriteMessage($"\nSẽ áp dụng cho tất cả {sampleLineIds.Count} sample line(s) trong nhóm '{group.Name}'.");
                        break;
                    
                    case 3: // Áp dụng cho danh sách nhóm đã chọn
                        sampleLineIds = new ObjectIdCollection();
                        foreach (ObjectId grpId in settingsForm.SelectedGroupIds)
                        {
                            SampleLineGroup grp = tr.GetObject(grpId, OpenMode.ForRead) as SampleLineGroup;
                            if (grp != null)
                            {
                                ObjectIdCollection ids = grp.GetSampleLineIds();
                                foreach (ObjectId id in ids)
                                {
                                    sampleLineIds.Add(id);
                                }
                            }
                        }
                        A.Ed.WriteMessage($"\nSẽ áp dụng cho tất cả {sampleLineIds.Count} sample line(s) trong các nhóm đã chọn.");
                        break;

                    case 2: // Chọn từng sampleline (Individual)
                    default:
                        sampleLineIds = UserInput.GSelectionSetWithType("Chọn các sample line cần thay đổi bề rộng: \n", "AECC_SAMPLE_LINE");
                        break;
                }
                
                if (sampleLineIds.Count == 0)
                {
                    A.Ed.WriteMessage("\nKhông có sample line nào được chọn.");
                    return;
                }

                int successCount = 0;
                int errorCount = 0;

                A.Ed.WriteMessage($"\nBắt đầu xử lý với offset trái: {leftOffset:F2}, offset phải: {rightOffset:F2}");

                // Process each selected sample line
                foreach (ObjectId sampleLineId in sampleLineIds)
                {
                    try
                    {
                        SampleLine? sampleLine = tr.GetObject(sampleLineId, OpenMode.ForWrite) as SampleLine;
                        if (sampleLine == null)
                        {
                            errorCount++;
                            continue;
                        }

                        // Get parent alignment
                        ObjectId alignmentId = sampleLine.GetParentAlignmentId();
                        Alignment? alignment = tr.GetObject(alignmentId, OpenMode.ForRead) as Alignment;
                        if (alignment == null)
                        {
                            errorCount++;
                            continue;
                        }

                        // Update sample line vertices using the improved method
                        UpdateSampleLineVerticesImproved(sampleLine, alignment, leftOffset, rightOffset);
                        
                        successCount++;
                    }
                    catch (System.Exception ex)
                    {
                        A.Ed.WriteMessage($"\nLỗi khi xử lý sample line: {ex.Message}");
                        errorCount++;
                    }
                }

                tr.Commit();
                
                string modeText = lastSelectionMode == 1 ? "trong nhóm" : "được chọn";
                A.Ed.WriteMessage($"\nHoàn thành! Đã cập nhật {successCount} sample line(s) {modeText}. Lỗi: {errorCount}");
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage($"\nLỗi: {e.Message}");
            }
        }

        private static void UpdateSampleLineVerticesImproved(SampleLine sampleLine, Alignment alignment, double leftOffset, double rightOffset)
        {
            A.Ed.WriteMessage($"\nCập nhật sample line '{sampleLine.Name}' có {sampleLine.Vertices.Count} vertex:");
            
            if (sampleLine.Vertices.Count > 3)
            {
                A.Ed.WriteMessage("  Cảnh báo: SampleLine có nhiều hơn 3 điểm, cần kiểm tra lại!");
            }

            // Step 1: Find center vertex and get its station
            Point3d centerPoint = new();
            double station = 0;
            double offset = 0;
            
            foreach (SampleLineVertex vertex in sampleLine.Vertices)
            {
                if (vertex.Side == SampleLineVertexSideType.Center)
                {
                    centerPoint = vertex.Location;
                    alignment.StationOffset(centerPoint.X, centerPoint.Y, ref station, ref offset);
                    A.Ed.WriteMessage($"  Center vertex tại station: {station:F2}, offset: {offset:F2}");
                    break;
                }
            }

            // Step 2: Update left vertex
            foreach (SampleLineVertex vertex in sampleLine.Vertices)
            {
                if (vertex.Side == SampleLineVertexSideType.Left)
                {
                    Point3d oldLocation = vertex.Location;
                    double easting = 0, northing = 0;
                    alignment.PointLocation(station, -leftOffset, ref easting, ref northing);
                    vertex.Location = new Point3d(easting, northing, oldLocation.Z);
                    A.Ed.WriteMessage($"  Left vertex: ({oldLocation.X:F2}, {oldLocation.Y:F2}) -> ({easting:F2}, {northing:F2}) với offset: {-leftOffset:F2}");
                    break;
                }
            }

            // Step 3: Update right vertex
            foreach (SampleLineVertex vertex in sampleLine.Vertices)
            {
                if (vertex.Side == SampleLineVertexSideType.Right)
                {
                    Point3d oldLocation = vertex.Location;
                    double easting = 0, northing = 0;
                    alignment.PointLocation(station, rightOffset, ref easting, ref northing);
                    vertex.Location = new Point3d(easting, northing, oldLocation.Z);
                    A.Ed.WriteMessage($"  Right vertex: ({oldLocation.X:F2}, {oldLocation.Y:F2}) -> ({easting:F2}, {northing:F2}) với offset: {rightOffset:F2}");
                    break;
                }
            }
        }
    }

    // Form for user input settings
    public partial class SamplelineWidthForm : Form
    {
        public double LeftOffset { get; private set; }
        public double RightOffset { get; private set; }
        public bool UseInputMode { get; private set; }
        public int SelectionMode { get; private set; } // 0 = quét chọn, 1 = áp dụng cho nhóm, 2 = chọn từng cái

        public List<ObjectId> SelectedGroupIds { get; private set; } = new List<ObjectId>();
        private Dictionary<ObjectId, string> _slGroups;

        private TextBox txtLeftOffset;
        private TextBox txtRightOffset;
        private RadioButton rbInputMode;
        private RadioButton rbPickMode;
        private RadioButton rbScopeWindow;   // Quét chọn các sampleline
        private RadioButton rbScopeGroup;    // Áp dụng cho toàn bộ nhóm (Pick)
        private RadioButton rbScopeList;     // Chọn từ danh sách
        private RadioButton rbScopeIndividual; // Chọn từng cái
        private CheckedListBox clbGroups;    // Danh sách các group
        private Button btnSelectAll;         // Nút chọn hết
        private Button btnOK;
        private Button btnCancel;
        private System.Windows.Forms.Label lblLeftOffset;
        private System.Windows.Forms.Label lblRightOffset;
        private GroupBox gbMode;
        private GroupBox gbOffsets;
        private GroupBox gbApplyScope;

        public SamplelineWidthForm(double defaultLeftOffset, double defaultRightOffset, bool defaultUseInputMode, int defaultSelectionMode, Dictionary<ObjectId, string> slGroups)
        {
            _slGroups = slGroups;
            InitializeComponent();
            
            // Set default values
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            txtLeftOffset.Text = defaultLeftOffset.ToString("F2");
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            txtRightOffset.Text = defaultRightOffset.ToString("F2");
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            rbInputMode.Checked = defaultUseInputMode;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            rbPickMode.Checked = !defaultUseInputMode;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            
            // Set selection mode radio buttons
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            rbScopeWindow.Checked = (defaultSelectionMode == 0);
            rbScopeGroup.Checked = (defaultSelectionMode == 1);
            rbScopeIndividual.Checked = (defaultSelectionMode == 2);
            rbScopeList.Checked = (defaultSelectionMode == 3);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            
            // Populate groups
            foreach (var kvp in _slGroups)
            {
                clbGroups.Items.Add(new SampleLineGroupItem { Id = kvp.Key, DisplayName = kvp.Value });
            }

            // Update controls state
            UpdateControlsState();
        }

        public class SampleLineGroupItem
        {
            public ObjectId Id { get; set; }
            public string DisplayName { get; set; } = "";
            public override string ToString() { return DisplayName; }
        }

        private void InitializeComponent()
        {
            this.txtLeftOffset = new TextBox();
            this.txtRightOffset = new TextBox();
            this.rbInputMode = new RadioButton();
            this.rbPickMode = new RadioButton();
            this.rbScopeWindow = new RadioButton();
            this.rbScopeGroup = new RadioButton();
            this.rbScopeList = new RadioButton();
            this.rbScopeIndividual = new RadioButton();
            this.clbGroups = new CheckedListBox();
            this.btnSelectAll = new Button();
            this.btnOK = new Button();
            this.btnCancel = new Button();
            this.lblLeftOffset = new System.Windows.Forms.Label();
            this.lblRightOffset = new System.Windows.Forms.Label();
            this.gbMode = new GroupBox();
            this.gbOffsets = new GroupBox();
            this.gbApplyScope = new GroupBox();
            this.SuspendLayout();

            // Form properties
            this.Text = "Thay đổi bề rộng Sample Line";
            this.Size = new System.Drawing.Size(450, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Mode GroupBox
            this.gbMode.Text = "Chế độ nhập liệu";
            this.gbMode.Location = new System.Drawing.Point(12, 12);
            this.gbMode.Size = new System.Drawing.Size(360, 80);

            // Radio buttons
            this.rbInputMode.Text = "Nhập khoảng offset 2 bên";
            this.rbInputMode.Location = new System.Drawing.Point(15, 25);
            this.rbInputMode.Size = new System.Drawing.Size(200, 20);
            this.rbInputMode.CheckedChanged += RbInputMode_CheckedChanged;

            this.rbPickMode.Text = "Chọn khoảng cách trên model";
            this.rbPickMode.Location = new System.Drawing.Point(15, 50);
            this.rbPickMode.Size = new System.Drawing.Size(200, 20);

            // Offsets GroupBox
            this.gbOffsets.Text = "Khoảng cách offset";
            this.gbOffsets.Location = new System.Drawing.Point(12, 100);
            this.gbOffsets.Size = new System.Drawing.Size(360, 100);

            // Labels
            this.lblLeftOffset.Text = "Offset trái:";
            this.lblLeftOffset.Location = new System.Drawing.Point(15, 30);
            this.lblLeftOffset.Size = new System.Drawing.Size(80, 20);

            this.lblRightOffset.Text = "Offset phải:";
            this.lblRightOffset.Location = new System.Drawing.Point(15, 60);
            this.lblRightOffset.Size = new System.Drawing.Size(80, 20);

            // TextBoxes
            this.txtLeftOffset.Location = new System.Drawing.Point(110, 28);
            this.txtLeftOffset.Size = new System.Drawing.Size(150, 22);

            this.txtRightOffset.Location = new System.Drawing.Point(110, 58);
            this.txtRightOffset.Size = new System.Drawing.Size(150, 22);

            // Apply Scope GroupBox
            this.gbApplyScope.Text = "Phạm vi áp dụng";
            this.gbApplyScope.Location = new System.Drawing.Point(12, 210);
            this.gbApplyScope.Size = new System.Drawing.Size(410, 300);

            // Radio buttons for selection scope
            this.rbScopeWindow.Text = "Quét chọn các sample line cần điều chỉnh";
            this.rbScopeWindow.Location = new System.Drawing.Point(15, 25);
            this.rbScopeWindow.Size = new System.Drawing.Size(380, 20);
            this.rbScopeWindow.CheckedChanged += RbScope_CheckedChanged;

            this.rbScopeGroup.Text = "Chọn 1 SL mẫu để áp dụng cho cả nhóm";
            this.rbScopeGroup.Location = new System.Drawing.Point(15, 50);
            this.rbScopeGroup.Size = new System.Drawing.Size(380, 20);
            this.rbScopeGroup.CheckedChanged += RbScope_CheckedChanged;

            this.rbScopeList.Text = "Chọn các nhóm (tích chọn bên dưới)";
            this.rbScopeList.Location = new System.Drawing.Point(15, 75);
            this.rbScopeList.Size = new System.Drawing.Size(380, 20);
            this.rbScopeList.CheckedChanged += RbScope_CheckedChanged;

            this.clbGroups.Location = new System.Drawing.Point(35, 100);
            this.clbGroups.Size = new System.Drawing.Size(350, 130);
            this.clbGroups.CheckOnClick = true;

            this.btnSelectAll.Text = "Chọn hết";
            this.btnSelectAll.Location = new System.Drawing.Point(305, 235);
            this.btnSelectAll.Size = new System.Drawing.Size(80, 23);
            this.btnSelectAll.Click += BtnSelectAll_Click;

            this.rbScopeIndividual.Text = "Chọn từng sample line riêng lẻ";
            this.rbScopeIndividual.Location = new System.Drawing.Point(15, 265);
            this.rbScopeIndividual.Size = new System.Drawing.Size(380, 20);
            this.rbScopeIndividual.CheckedChanged += RbScope_CheckedChanged;

            // Buttons
            this.btnOK.Text = "OK";
            this.btnOK.Location = new System.Drawing.Point(267, 520);
            this.btnOK.Size = new System.Drawing.Size(75, 25);
            this.btnOK.Click += BtnOK_Click;

            
            this.btnCancel.Text = "Hủy";
            this.btnCancel.Location = new System.Drawing.Point(347, 520);
            this.btnCancel.Size = new System.Drawing.Size(75, 25);
            this.btnCancel.Click += BtnCancel_Click;
            this.btnCancel.DialogResult = DialogResult.Cancel;

            // Add controls to form
            this.gbMode.Controls.Add(this.rbInputMode);
            this.gbMode.Controls.Add(this.rbPickMode);
            
            this.gbOffsets.Controls.Add(this.lblLeftOffset);
            this.gbOffsets.Controls.Add(this.txtLeftOffset);
            this.gbOffsets.Controls.Add(this.lblRightOffset);
            this.gbOffsets.Controls.Add(this.txtRightOffset);

            this.gbApplyScope.Controls.Add(this.rbScopeWindow);
            this.gbApplyScope.Controls.Add(this.rbScopeGroup);
            this.gbApplyScope.Controls.Add(this.rbScopeList);
            this.gbApplyScope.Controls.Add(this.clbGroups);
            this.gbApplyScope.Controls.Add(this.btnSelectAll);
            this.gbApplyScope.Controls.Add(this.rbScopeIndividual);

            this.Controls.Add(this.gbMode);
            this.Controls.Add(this.gbOffsets);
            this.Controls.Add(this.gbApplyScope);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.btnCancel);

            this.ResumeLayout(false);
        }

        private void RbInputMode_CheckedChanged(object? sender, EventArgs e)
        {
            UpdateControlsState();
        }

        private void RbScope_CheckedChanged(object? sender, EventArgs e)
        {
            UpdateControlsState();
        }

        private void UpdateControlsState()
        {
            bool enableOffsets = rbInputMode.Checked;
            txtLeftOffset.Enabled = enableOffsets;
            txtRightOffset.Enabled = enableOffsets;
            lblLeftOffset.Enabled = enableOffsets;
            lblRightOffset.Enabled = enableOffsets;

            // Enable CheckedListBox only when Scope List is selected
            clbGroups.Enabled = rbScopeList.Checked;
            btnSelectAll.Enabled = rbScopeList.Checked;
        }

        private void BtnSelectAll_Click(object? sender, EventArgs e)
        {
            if (clbGroups.Items.Count == 0) return;

            // Check if all are currently checked
            bool allChecked = (clbGroups.CheckedItems.Count == clbGroups.Items.Count);
            
            // Toggle
            for (int i = 0; i < clbGroups.Items.Count; i++)
            {
                clbGroups.SetItemChecked(i, !allChecked);
            }

            // Force visual update
            clbGroups.Invalidate();
            
            // Allow events to process
            System.Windows.Forms.Application.DoEvents();
        }

        private void BtnOK_Click(object? sender, EventArgs e)
        {
            try
            {
                UseInputMode = rbInputMode.Checked;
                
                // Determine selection mode
                if (rbScopeWindow.Checked)
                    SelectionMode = 0;
                else if (rbScopeGroup.Checked)
                    SelectionMode = 1;
                else if (rbScopeList.Checked)
                {
                    SelectionMode = 3;
                    if (clbGroups.CheckedItems.Count == 0)
                    {
                        MessageBox.Show("Vui lòng chọn ít nhất một nhóm Sample Line.", "Lỗi chọn nhóm",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    
                    SelectedGroupIds.Clear();
                    foreach (object item in clbGroups.CheckedItems)
                    {
                        if (item is SampleLineGroupItem groupItem)
                        {
                            SelectedGroupIds.Add(groupItem.Id);
                        }
                    }
                }
                else
                    SelectionMode = 2;
                
                if (UseInputMode)
                {
                    // Validate input values
                    if (!double.TryParse(txtLeftOffset.Text, out double leftOffset) || leftOffset <= 0)
                    {
                        MessageBox.Show("Vui lòng nhập giá trị offset trái hợp lệ (> 0).", "Lỗi nhập liệu", 
                                      MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        txtLeftOffset.Focus();
                        return;
                    }

                    if (!double.TryParse(txtRightOffset.Text, out double rightOffset) || rightOffset <= 0)
                    {
                        MessageBox.Show("Vui lòng nhập giá trị offset phải hợp lệ (> 0).", "Lỗi nhập liệu", 
                                      MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        txtRightOffset.Focus();
                        return;
                    }

                    LeftOffset = leftOffset;
                    RightOffset = rightOffset;
                }

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnCancel_Click(object? sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}

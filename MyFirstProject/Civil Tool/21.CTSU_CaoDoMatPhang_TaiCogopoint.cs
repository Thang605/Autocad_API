using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Acad = Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;

using Civil = Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using Autodesk.Civil.Runtime;
using Autodesk.Civil.Settings;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.Civil.ApplicationServices;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using System.Windows.Forms;
using MyFirstProject.Extensions;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsButton = System.Windows.Forms.Button;
using WinFormsComboBox = System.Windows.Forms.ComboBox;

// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3DCsharp.CTSU_CaoDoMatPhang_TaiCogopoint_Commands))]

namespace Civil3DCsharp
{
    class CTSU_CaoDoMatPhang_TaiCogopoint_Commands
    {
        [CommandMethod("CTT_Cao_Do_Mat_Phang_Tai_Cogo_Point")]
        public static void CTSU_CaoDoMatPhang_TaiCogopoint()
        {
            // start transaction
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();

                // Show selection form
                string selectedPointGroup = "";
                string selectedSpotElevationStyle = "";
                string selectedMarkerStyle = "";
                string selectedSurface = "";

                using (var selectionForm = new PointGroupStyleSelectionForm())
                {
                    if (selectionForm.ShowDialog() != DialogResult.OK)
                    {
                        A.Ed.WriteMessage("\n Lệnh đã bị hủy bởi người dùng.");
                        return;
                    }

                    selectedPointGroup = selectionForm.SelectedPointGroup;
                    selectedSpotElevationStyle = selectionForm.SelectedSpotElevationStyle;
                    selectedMarkerStyle = selectionForm.SelectedMarkerStyle;
                    selectedSurface = selectionForm.SelectedSurface;
                }

                // Find the selected point group
                PointGroupCollection pointGroupColl = A.Cdoc.PointGroups;
                PointGroup? targetPointGroup = null;
                
                foreach (ObjectId pointGroupId in pointGroupColl)
                {
                    PointGroup? pointGroup = tr.GetObject(pointGroupId, OpenMode.ForRead) as PointGroup;
                    if (pointGroup != null && pointGroup.Name == selectedPointGroup)
                    {
                        targetPointGroup = pointGroup;
                        break;
                    }
                }

                if (targetPointGroup == null)
                {
                    A.Ed.WriteMessage($"\n Không tìm thấy point group: {selectedPointGroup}");
                    return;
                }

                // Find the selected surface
                ObjectId surfaceId = ObjectId.Null;
                CivSurface? surface = null;
                
                var surfaces = A.Cdoc.GetSurfaceIds();
                foreach (ObjectId id in surfaces)
                {
                    CivSurface? surf = tr.GetObject(id, OpenMode.ForRead) as CivSurface;
                    if (surf != null && surf.Name == selectedSurface)
                    {
                        surfaceId = id;
                        surface = surf;
                        break;
                    }
                }
                
                if (surface == null)
                {
                    A.Ed.WriteMessage($"\n Không tìm thấy surface: {selectedSurface}");
                    return;
                }

                // Lấy tất cả các point trong point group
                ObjectIdCollection pointIds = UtilitiesC3D.GPointIdsFromPointGroup(targetPointGroup.ObjectId);
                
                if (pointIds.Count == 0)
                {
                    A.Ed.WriteMessage("\n Point group không chứa điểm nào!");
                    return;
                }

                // Lấy style cho spot elevation label
                ObjectId spotElevationLabelStyleId;
                ObjectId markStyleId;
                
                try
                {
                    spotElevationLabelStyleId = A.Cdoc.Styles.LabelStyles.SurfaceLabelStyles.SpotElevationLabelStyles[selectedSpotElevationStyle];
                    markStyleId = A.Cdoc.Styles.MarkerStyles[selectedMarkerStyle];
                }
                catch
                {
                    A.Ed.WriteMessage("\n Không tìm thấy style đã chọn!");
                    return;
                }

                int labelCount = 0;
                int skippedCount = 0;

                // Tạo elevation label tại tọa độ của từng cogo point
                foreach (ObjectId pointId in pointIds)
                {
                    CogoPoint? cogoPoint = tr.GetObject(pointId, OpenMode.ForRead) as CogoPoint;
                    if (cogoPoint != null)
                    {
                        try
                        {
                            // Lấy tọa độ X, Y của cogo point
                            Point2d point2D = new(cogoPoint.Easting, cogoPoint.Northing);
                            
                            // Kiểm tra xem điểm có nằm trong phạm vi surface không
                            double elevation;
                            try
                            {
                                elevation = surface.FindElevationAtXY(point2D.X, point2D.Y);
                            }
                            catch
                            {
                                // Điểm nằm ngoài phạm vi surface, bỏ qua
                                skippedCount++;
                                A.Ed.WriteMessage($"\n Bỏ qua điểm {cogoPoint.PointNumber} - nằm ngoài phạm vi surface");
                                continue;
                            }
                            
                            // Tạo spot elevation label trên surface tại vị trí này
                            ObjectId surfaceElevnLblId = SurfaceElevationLabel.Create(
                                surfaceId, 
                                point2D, 
                                spotElevationLabelStyleId, 
                                markStyleId);
                            
                            labelCount++;
                        }
                        catch (Autodesk.AutoCAD.Runtime.Exception ex)
                        {
                            A.Ed.WriteMessage($"\n Lỗi khi tạo label tại điểm {cogoPoint.PointNumber}: {ex.Message}");
                            skippedCount++;
                        }
                    }
                }

                A.Ed.WriteMessage($"\n Kết quả:");
                A.Ed.WriteMessage($"\n - Đã tạo thành công {labelCount} elevation labels trên surface!");
                A.Ed.WriteMessage($"\n - Đã bỏ qua {skippedCount} điểm (nằm ngoài surface hoặc có lỗi)");

                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage($"\n Lỗi: {e.Message}");
            }
        }
    }

    // Simple form for selection without designer file
    public class PointGroupStyleSelectionForm : Form
    {
        public string SelectedPointGroup { get; private set; } = "";
        public string SelectedSpotElevationStyle { get; private set; } = "";
        public string SelectedMarkerStyle { get; private set; } = "";
        public string SelectedSurface { get; private set; } = "";

        private WinFormsComboBox comboBoxPointGroup;
        private WinFormsComboBox comboBoxSpotElevationStyle;
        private WinFormsComboBox comboBoxMarkerStyle;
        private WinFormsComboBox comboBoxSurface;
        private WinFormsButton buttonOK;
        private WinFormsButton buttonCancel;

        public PointGroupStyleSelectionForm()
        {
            InitializeComponent();
            LoadData();
        }

        private void InitializeComponent()
        {
            var labelPointGroup = new WinFormsLabel();
            comboBoxPointGroup = new WinFormsComboBox();
            var labelSurface = new WinFormsLabel();
            comboBoxSurface = new WinFormsComboBox();
            var labelSpotElevationStyle = new WinFormsLabel();
            comboBoxSpotElevationStyle = new WinFormsComboBox();
            var labelMarkerStyle = new WinFormsLabel();
            comboBoxMarkerStyle = new WinFormsComboBox();
            buttonOK = new WinFormsButton();
            buttonCancel = new WinFormsButton();

            SuspendLayout();

            // labelPointGroup
            labelPointGroup.AutoSize = true;
            labelPointGroup.Location = new System.Drawing.Point(30, 30);
            labelPointGroup.Name = "labelPointGroup";
            labelPointGroup.Size = new System.Drawing.Size(90, 20);
            labelPointGroup.TabIndex = 0;
            labelPointGroup.Text = "Point Group:";

            // comboBoxPointGroup
            comboBoxPointGroup.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxPointGroup.FormattingEnabled = true;
            comboBoxPointGroup.Location = new System.Drawing.Point(30, 55);
            comboBoxPointGroup.Name = "comboBoxPointGroup";
            comboBoxPointGroup.Size = new System.Drawing.Size(660, 28);
            comboBoxPointGroup.TabIndex = 1;

            // labelSurface
            labelSurface.AutoSize = true;
            labelSurface.Location = new System.Drawing.Point(30, 100);
            labelSurface.Name = "labelSurface";
            labelSurface.Size = new System.Drawing.Size(60, 20);
            labelSurface.TabIndex = 2;
            labelSurface.Text = "Surface:";

            // comboBoxSurface
            comboBoxSurface.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxSurface.FormattingEnabled = true;
            comboBoxSurface.Location = new System.Drawing.Point(30, 125);
            comboBoxSurface.Name = "comboBoxSurface";
            comboBoxSurface.Size = new System.Drawing.Size(660, 28);
            comboBoxSurface.TabIndex = 3;

            // labelSpotElevationStyle
            labelSpotElevationStyle.AutoSize = true;
            labelSpotElevationStyle.Location = new System.Drawing.Point(30, 170);
            labelSpotElevationStyle.Name = "labelSpotElevationStyle";
            labelSpotElevationStyle.Size = new System.Drawing.Size(150, 20);
            labelSpotElevationStyle.TabIndex = 4;
            labelSpotElevationStyle.Text = "Spot Elevation Style:";

            // comboBoxSpotElevationStyle
            comboBoxSpotElevationStyle.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxSpotElevationStyle.FormattingEnabled = true;
            comboBoxSpotElevationStyle.Location = new System.Drawing.Point(30, 195);
            comboBoxSpotElevationStyle.Name = "comboBoxSpotElevationStyle";
            comboBoxSpotElevationStyle.Size = new System.Drawing.Size(660, 28);
            comboBoxSpotElevationStyle.TabIndex = 5;

            // labelMarkerStyle
            labelMarkerStyle.AutoSize = true;
            labelMarkerStyle.Location = new System.Drawing.Point(30, 240);
            labelMarkerStyle.Name = "labelMarkerStyle";
            labelMarkerStyle.Size = new System.Drawing.Size(100, 20);
            labelMarkerStyle.TabIndex = 6;
            labelMarkerStyle.Text = "Marker Style:";

            // comboBoxMarkerStyle
            comboBoxMarkerStyle.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxMarkerStyle.FormattingEnabled = true;
            comboBoxMarkerStyle.Location = new System.Drawing.Point(30, 265);
            comboBoxMarkerStyle.Name = "comboBoxMarkerStyle";
            comboBoxMarkerStyle.Size = new System.Drawing.Size(660, 28);
            comboBoxMarkerStyle.TabIndex = 7;

            // buttonOK
            buttonOK.Location = new System.Drawing.Point(280, 320);
            buttonOK.Name = "buttonOK";
            buttonOK.Size = new System.Drawing.Size(75, 30);
            buttonOK.TabIndex = 8;
            buttonOK.Text = "OK";
            buttonOK.UseVisualStyleBackColor = true;
            buttonOK.Click += ButtonOK_Click;

            // buttonCancel
            buttonCancel.Location = new System.Drawing.Point(380, 320);
            buttonCancel.Name = "buttonCancel";
            buttonCancel.Size = new System.Drawing.Size(75, 30);
            buttonCancel.TabIndex = 9;
            buttonCancel.Text = "Cancel";
            buttonCancel.UseVisualStyleBackColor = true;
            buttonCancel.Click += ButtonCancel_Click;

            // PointGroupStyleSelectionForm
            AutoScaleDimensions = new System.Drawing.SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(720, 380);
            Controls.Add(buttonCancel);
            Controls.Add(buttonOK);
            Controls.Add(comboBoxMarkerStyle);
            Controls.Add(labelMarkerStyle);
            Controls.Add(comboBoxSpotElevationStyle);
            Controls.Add(labelSpotElevationStyle);
            Controls.Add(comboBoxSurface);
            Controls.Add(labelSurface);
            Controls.Add(comboBoxPointGroup);
            Controls.Add(labelPointGroup);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "PointGroupStyleSelectionForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Tạo nhãn cao độ cho mặt phẳng theo nhóm điểm cogo point";
            ResumeLayout(false);
            PerformLayout();
        }

        private void LoadData()
        {
            try
            {
                // Load Point Groups
                var pointGroupNames = new List<string>();
                PointGroupCollection pointGroupColl = A.Cdoc.PointGroups;
                foreach (ObjectId pointGroupId in pointGroupColl)
                {
                    using (Transaction tr = A.Db.TransactionManager.StartTransaction())
                    {
                        PointGroup? pointGroup = tr.GetObject(pointGroupId, OpenMode.ForRead) as PointGroup;
                        if (pointGroup != null)
                        {
                            pointGroupNames.Add(pointGroup.Name);
                        }
                        tr.Commit();
                    }
                }
                // Sort alphabetically and add to combobox
                pointGroupNames.Sort();
                foreach (string name in pointGroupNames)
                {
                    comboBoxPointGroup.Items.Add(name);
                }

                // Load Surfaces
                var surfaceNames = new List<string>();
                var surfaceIds = A.Cdoc.GetSurfaceIds();
                foreach (ObjectId surfaceId in surfaceIds)
                {
                    using (Transaction tr = A.Db.TransactionManager.StartTransaction())
                    {
                        CivSurface? surface = tr.GetObject(surfaceId, OpenMode.ForRead) as CivSurface;
                        if (surface != null)
                        {
                            surfaceNames.Add(surface.Name);
                        }
                        tr.Commit();
                    }
                }
                // Sort alphabetically and add to combobox
                surfaceNames.Sort();
                foreach (string name in surfaceNames)
                {
                    comboBoxSurface.Items.Add(name);
                }

                // Load Spot Elevation Label Styles
                var spotElevationStyleNames = new List<string>();
                var spotElevationStyles = A.Cdoc.Styles.LabelStyles.SurfaceLabelStyles.SpotElevationLabelStyles;
                for (int i = 0; i < spotElevationStyles.Count; i++)
                {
                    using (Transaction tr = A.Db.TransactionManager.StartTransaction())
                    {
                        var styleId = spotElevationStyles[i];
                        var style = tr.GetObject(styleId, OpenMode.ForRead) as LabelStyle;
                        if (style != null)
                        {
                            spotElevationStyleNames.Add(style.Name);
                        }
                        tr.Commit();
                    }
                }
                // Sort alphabetically and add to combobox
                spotElevationStyleNames.Sort();
                foreach (string name in spotElevationStyleNames)
                {
                    comboBoxSpotElevationStyle.Items.Add(name);
                }

                // Load Marker Styles
                var markerStyleNames = new List<string>();
                var markerStyles = A.Cdoc.Styles.MarkerStyles;
                for (int i = 0; i < markerStyles.Count; i++)
                {
                    using (Transaction tr = A.Db.TransactionManager.StartTransaction())
                    {
                        var styleId = markerStyles[i];
                        var style = tr.GetObject(styleId, OpenMode.ForRead) as MarkerStyle;
                        if (style != null)
                        {
                            markerStyleNames.Add(style.Name);
                        }
                        tr.Commit();
                    }
                }
                // Sort alphabetically and add to combobox
                markerStyleNames.Sort();
                foreach (string name in markerStyleNames)
                {
                    comboBoxMarkerStyle.Items.Add(name);
                }

                // Set default selections if available
                if (comboBoxPointGroup.Items.Count > 0)
                    comboBoxPointGroup.SelectedIndex = 0;
                if (comboBoxSurface.Items.Count > 0)
                    comboBoxSurface.SelectedIndex = 0;
                if (comboBoxSpotElevationStyle.Items.Count > 0)
                    comboBoxSpotElevationStyle.SelectedIndex = 0;
                if (comboBoxMarkerStyle.Items.Count > 0)
                    comboBoxMarkerStyle.SelectedIndex = 0;
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Lỗi khi load dữ liệu: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ButtonOK_Click(object sender, EventArgs e)
        {
            if (comboBoxPointGroup.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn Point Group!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (comboBoxSurface.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn Surface!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (comboBoxSpotElevationStyle.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn Spot Elevation Style!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (comboBoxMarkerStyle.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn Marker Style!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SelectedPointGroup = comboBoxPointGroup.SelectedItem.ToString() ?? "";
            SelectedSurface = comboBoxSurface.SelectedItem.ToString() ?? "";
            SelectedSpotElevationStyle = comboBoxSpotElevationStyle.SelectedItem.ToString() ?? "";
            SelectedMarkerStyle = comboBoxMarkerStyle.SelectedItem.ToString() ?? "";

            DialogResult = DialogResult.OK;
            Close();
        }

        private void ButtonCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}

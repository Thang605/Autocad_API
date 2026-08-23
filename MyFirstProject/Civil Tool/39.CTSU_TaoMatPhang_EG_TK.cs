// CTSU_TaoMatPhang_EG_TK
// Chọn 1 section → tìm tất cả section cùng surface name → lấy tọa độ x,y,z → add vào surface
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using Autodesk.AutoCAD.Runtime;
using Acad = Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;
using ATable = Autodesk.AutoCAD.DatabaseServices.Table;

using Civil = Autodesk.Civil.ApplicationServices;
using Autodesk.Civil;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using Autodesk.Civil.Runtime;
using Autodesk.Civil.Settings;
using CivSurface = Autodesk.Civil.DatabaseServices.TinSurface;
using Section = Autodesk.Civil.DatabaseServices.Section;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Label = Autodesk.Civil.DatabaseServices.Label;
using MyFirstProject.Extensions;
using MyFirstProject;

// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3DCsharp.CTSU_TaoMatPhang_EG_TK_Commands))]

namespace Civil3DCsharp
{
    public class CTSU_TaoMatPhang_EG_TK_Commands
    {
        [CommandMethod("CTSU_TaoMatPhang_EG_TK")]
        public static void CTSUTaoMatPhangEGTK()
        {
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                CivilDocument civDoc = CivilApplication.ActiveDocument;

                // ═══════════════════════════════════════════════════════
                // Step 1: Chọn section trên section view
                // ═══════════════════════════════════════════════════════
                A.Ed.WriteMessage("\nChọn section trên trắc ngang:");
                PromptEntityOptions peo = new("\nChọn section trên trắc ngang: ");
                peo.SetRejectMessage("\nĐối tượng được chọn không phải là section.");
                peo.AddAllowedClass(typeof(Section), true);

                PromptEntityResult per = A.Ed.GetEntity(peo);
                if (per.Status != PromptStatus.OK)
                {
                    A.Ed.WriteMessage("\nĐã hủy lệnh.");
                    return;
                }

                ObjectId sectionId = per.ObjectId;
                Section? section = tr.GetObject(sectionId, OpenMode.ForWrite) as Section;
                if (section == null)
                {
                    A.Ed.WriteMessage("\nKhông thể lấy thông tin section.");
                    return;
                }

                // ═══════════════════════════════════════════════════════
                // Step 2: Tìm SampleLineGroup, Alignment, và SourceId của section đã chọn
                // ═══════════════════════════════════════════════════════
                SampleLineGroup? sampleLineGroup = null;
                SampleLine? currentSampleLine = null;
                Alignment? alignment = null;
                ObjectId sourceSurfaceSourceId = ObjectId.Null;
                string sourceSurfaceName = "";

                ObjectIdCollection alignmentIds = civDoc.GetAlignmentIds();
                bool found = false;

                foreach (ObjectId alId in alignmentIds)
                {
                    if (found) break;
                    Alignment? al = tr.GetObject(alId, OpenMode.ForWrite) as Alignment;
                    if (al == null) continue;

                    foreach (ObjectId slgId in al.GetSampleLineGroupIds())
                    {
                        if (found) break;
                        SampleLineGroup? slg = tr.GetObject(slgId, OpenMode.ForWrite) as SampleLineGroup;
                        if (slg == null) continue;

                        SectionSourceCollection sectionSources = slg.GetSectionSources();
                        foreach (ObjectId slId in slg.GetSampleLineIds())
                        {
                            if (found) break;
                            SampleLine? sl = tr.GetObject(slId, OpenMode.ForWrite) as SampleLine;
                            if (sl == null) continue;

                            foreach (SectionSource source in sectionSources)
                            {
                                try
                                {
                                    if (sl.GetSectionId(source.SourceId) == sectionId)
                                    {
                                        currentSampleLine = sl;
                                        sampleLineGroup = slg;
                                        alignment = al;
                                        sourceSurfaceSourceId = source.SourceId;

                                        // Lấy tên surface nguồn
                                        try
                                        {
                                            CivSurface? surf = tr.GetObject(source.SourceId, OpenMode.ForWrite) as CivSurface;
                                            if (surf != null)
                                                sourceSurfaceName = surf.Name;
                                        }
                                        catch
                                        {
                                            // Nếu SourceId không phải TinSurface, dùng section name
                                            sourceSurfaceName = section.Name;
                                        }

                                        found = true;
                                        break;
                                    }
                                }
                                catch { continue; }
                            }
                        }
                    }
                }

                if (currentSampleLine == null || sampleLineGroup == null || alignment == null)
                {
                    A.Ed.WriteMessage("\nKhông tìm thấy sample line chứa section này.");
                    return;
                }

                if (sourceSurfaceSourceId == ObjectId.Null)
                {
                    A.Ed.WriteMessage("\nKhông tìm thấy surface nguồn của section.");
                    return;
                }

                A.Ed.WriteMessage($"\n══════════════════════════════════════");
                A.Ed.WriteMessage($"\n📋 Surface section name: '{sourceSurfaceName}'");
                A.Ed.WriteMessage($"\n📋 Alignment: '{alignment.Name}'");
                A.Ed.WriteMessage($"\n📋 Sample line group: '{sampleLineGroup.Name}'");
                A.Ed.WriteMessage($"\n══════════════════════════════════════");

                // ═══════════════════════════════════════════════════════
                // Step 3: Thu thập tất cả sample line, sắp xếp theo station
                // ═══════════════════════════════════════════════════════
                var sampleLinesWithStations = new List<(ObjectId id, double station, SampleLine sampleLine)>();
                foreach (ObjectId slId in sampleLineGroup.GetSampleLineIds())
                {
                    try
                    {
                        SampleLine? sl = tr.GetObject(slId, OpenMode.ForWrite) as SampleLine;
                        if (sl != null) sampleLinesWithStations.Add((slId, sl.Station, sl));
                    }
                    catch { continue; }
                }
                sampleLinesWithStations.Sort((a, b) => a.station.CompareTo(b.station));

                int totalSections = sampleLinesWithStations.Count;
                A.Ed.WriteMessage($"\n📊 Tổng số sample line: {totalSections}");

                // ═══════════════════════════════════════════════════════
                // Step 4: Hỏi tên surface đích (tạo mới hoặc chọn có sẵn)
                // ═══════════════════════════════════════════════════════
                ObjectId targetSurfaceId = ObjectId.Null;
                string targetSurfaceName = "";

                // Tạo form chọn surface đích
                using (var form = new TaoMatPhangEG_choTK_Form(civDoc, tr, sourceSurfaceName))
                {
                    var result = Application.ShowModalDialog(form);
                    if (result != System.Windows.Forms.DialogResult.OK)
                    {
                        A.Ed.WriteMessage("\nĐã hủy lệnh.");
                        return;
                    }

                    targetSurfaceId = form.SelectedSurfaceId;
                    targetSurfaceName = form.SelectedSurfaceName;
                    bool isNewSurface = form.IsNewSurface;

                    if (isNewSurface)
                    {
                        // Tạo surface mới
                        ObjectId surfaceStyleId = ObjectId.Null;
                        SurfaceStyleCollection surfaceStyles = civDoc.Styles.SurfaceStyles;
                        foreach (ObjectId styleId in surfaceStyles)
                        {
                            surfaceStyleId = styleId;
                            break;
                        }

                        if (surfaceStyleId == ObjectId.Null)
                        {
                            A.Ed.WriteMessage("\nKhông tìm được surface style.");
                            return;
                        }

                        targetSurfaceId = TinSurface.Create(targetSurfaceName, surfaceStyleId);
                        A.Ed.WriteMessage($"\n✅ Đã tạo surface mới: '{targetSurfaceName}'");
                    }
                }

                CivSurface? targetSurface = tr.GetObject(targetSurfaceId, OpenMode.ForWrite) as CivSurface;
                if (targetSurface == null)
                {
                    A.Ed.WriteMessage("\nKhông thể truy cập surface đích.");
                    return;
                }

                A.Ed.WriteMessage($"\n🎯 Surface đích: '{targetSurface.Name}'");

                // ═══════════════════════════════════════════════════════
                // Step 5: Duyệt tất cả section cùng surface name → thu thập tọa độ 3D
                // ═══════════════════════════════════════════════════════
                Point3dCollection allPoints = new();
                int sectionCount = 0;

                foreach (var (slId, station, sl) in sampleLinesWithStations)
                {
                    try
                    {
                        ObjectId secId = sl.GetSectionId(sourceSurfaceSourceId);
                        Section? sec = tr.GetObject(secId, OpenMode.ForWrite) as Section;
                        if (sec == null || sec.SectionPoints.Count < 2) continue;

                        List<Point3d> worldPoints = GetWorldPointsFromSection(sec, station, alignment);
                        if (worldPoints.Count < 2) continue;

                        foreach (Point3d pt in worldPoints)
                            allPoints.Add(pt);

                        sectionCount++;
                        A.Ed.WriteMessage($"\n  ✓ Station {station:F3} — {worldPoints.Count} điểm");
                    }
                    catch (System.Exception ex)
                    {
                        A.Ed.WriteMessage($"\n  ⚠ Station {station:F3}: {ex.Message}");
                    }
                }

                A.Ed.WriteMessage($"\n\n══════════════════════════════════════");
                A.Ed.WriteMessage($"\n📊 Đã xử lý {sectionCount}/{totalSections} sections");
                A.Ed.WriteMessage($"\n📊 Tổng số điểm: {allPoints.Count}");

                // ═══════════════════════════════════════════════════════
                // Step 6: Add tất cả điểm vào surface
                // ═══════════════════════════════════════════════════════
                if (allPoints.Count == 0)
                {
                    A.Ed.WriteMessage("\n⚠ Không có điểm nào để thêm vào surface.");
                    tr.Commit();
                    return;
                }

                A.Ed.WriteMessage($"\n🚀 Đang thêm {allPoints.Count} điểm vào surface '{targetSurface.Name}'...");

                try
                {
                    targetSurface.AddVertices(allPoints);
                    targetSurface.Rebuild();
                    A.Ed.WriteMessage($"\n✅ Hoàn thành! Đã thêm {allPoints.Count} điểm từ {sectionCount} sections vào '{targetSurface.Name}'.");
                }
                catch (System.Exception ex)
                {
                    A.Ed.WriteMessage($"\n❌ Lỗi khi thêm điểm vào surface: {ex.Message}");
                }

                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage($"\nLỗi AutoCAD: {e.Message}");
                A.Ed.WriteMessage($"\nError Code: {e.ErrorStatus}");
                tr.Abort();
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi hệ thống: {ex.Message}");
                A.Ed.WriteMessage($"\nStack trace: {ex.StackTrace}");
                tr.Abort();
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // Helper: Lấy tọa độ 3D world trực tiếp từ Section points
        // Section points đã chứa offset + elevation
        // Dùng alignment.PointLocation để chuyển (station, offset) → (easting, northing)
        // Elevation = SectionPoint.Location.Y (cao độ thực)
        // ═══════════════════════════════════════════════════════════════
        private static List<Point3d> GetWorldPointsFromSection(
            Section section, double station, Alignment alignment)
        {
            var worldPoints = new List<Point3d>();

            SectionPointCollection sectionPoints = section.SectionPoints;
            foreach (SectionPoint sectionPoint in sectionPoints)
            {
                Point3d loc = sectionPoint.Location;
                double offset = loc.X;    // offset so với tim tuyến
                double elevation = loc.Y; // cao độ thực

                double easting = 0, northing = 0;
                alignment.PointLocation(station, offset, ref easting, ref northing);
                worldPoints.Add(new Point3d(easting, northing, elevation));
            }

            return worldPoints;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Form chọn surface đích (tạo mới hoặc chọn có sẵn)
    // ═══════════════════════════════════════════════════════════════
    public class TaoMatPhangEG_choTK_Form : Form
    {
        public ObjectId SelectedSurfaceId { get; private set; } = ObjectId.Null;
        public string SelectedSurfaceName { get; private set; } = "";
        public bool IsNewSurface { get; private set; } = false;

        private RadioButton rdoExisting;
        private RadioButton rdoNew;
        private ComboBox cboSurfaces;
        private TextBox txtNewName;
        private Button btnOK;
        private Button btnCancel;
        private System.Windows.Forms.Label lblInfo;
        private System.Windows.Forms.Label lblSurfaceLabel;
        private System.Windows.Forms.Label lblNewLabel;

        private readonly Dictionary<string, ObjectId> surfaceMap = new();

        public TaoMatPhangEG_choTK_Form(CivilDocument civDoc, Transaction tr, string sourceSurfaceName)
        {
            InitializeComponents();
            LoadSurfaces(civDoc, tr);
            txtNewName.Text = $"EG_{sourceSurfaceName}";
            lblInfo.Text = $"Surface section: {sourceSurfaceName}\nChọn surface đích để thêm điểm section:";
        }

        private void InitializeComponents()
        {
            this.Text = "Tạo Mặt Phẳng EG cho Thiết Kế";
            this.Size = new System.Drawing.Size(450, 320);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;

            lblInfo = new System.Windows.Forms.Label
            {
                Location = new System.Drawing.Point(15, 15),
                Size = new System.Drawing.Size(400, 40),
                Font = new System.Drawing.Font("Segoe UI", 9F)
            };
            this.Controls.Add(lblInfo);

            // Option 1: Chọn surface có sẵn
            rdoExisting = new RadioButton
            {
                Text = "Chọn surface có sẵn:",
                Location = new System.Drawing.Point(15, 65),
                Size = new System.Drawing.Size(200, 25),
                Checked = true,
                Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold)
            };
            rdoExisting.CheckedChanged += (s, e) => UpdateUI();
            this.Controls.Add(rdoExisting);

            lblSurfaceLabel = new System.Windows.Forms.Label
            {
                Text = "Surface:",
                Location = new System.Drawing.Point(35, 95),
                Size = new System.Drawing.Size(60, 23),
                Font = new System.Drawing.Font("Segoe UI", 9F)
            };
            this.Controls.Add(lblSurfaceLabel);

            cboSurfaces = new ComboBox
            {
                Location = new System.Drawing.Point(100, 92),
                Size = new System.Drawing.Size(310, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new System.Drawing.Font("Segoe UI", 9F)
            };
            this.Controls.Add(cboSurfaces);

            // Option 2: Tạo surface mới
            rdoNew = new RadioButton
            {
                Text = "Tạo surface mới:",
                Location = new System.Drawing.Point(15, 130),
                Size = new System.Drawing.Size(200, 25),
                Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold)
            };
            rdoNew.CheckedChanged += (s, e) => UpdateUI();
            this.Controls.Add(rdoNew);

            lblNewLabel = new System.Windows.Forms.Label
            {
                Text = "Tên:",
                Location = new System.Drawing.Point(35, 160),
                Size = new System.Drawing.Size(60, 23),
                Font = new System.Drawing.Font("Segoe UI", 9F)
            };
            this.Controls.Add(lblNewLabel);

            txtNewName = new TextBox
            {
                Location = new System.Drawing.Point(100, 157),
                Size = new System.Drawing.Size(310, 25),
                Font = new System.Drawing.Font("Segoe UI", 9F)
            };
            this.Controls.Add(txtNewName);

            // Buttons
            btnOK = new Button
            {
                Text = "OK",
                Location = new System.Drawing.Point(220, 230),
                Size = new System.Drawing.Size(90, 30),
                DialogResult = DialogResult.OK,
                Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold)
            };
            btnOK.Click += BtnOK_Click;
            this.Controls.Add(btnOK);

            btnCancel = new Button
            {
                Text = "Hủy",
                Location = new System.Drawing.Point(320, 230),
                Size = new System.Drawing.Size(90, 30),
                DialogResult = DialogResult.Cancel,
                Font = new System.Drawing.Font("Segoe UI", 9F)
            };
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;

            UpdateUI();
        }

        private void LoadSurfaces(CivilDocument civDoc, Transaction tr)
        {
            ObjectIdCollection surfaceIds = civDoc.GetSurfaceIds();
            foreach (ObjectId surfId in surfaceIds)
            {
                try
                {
                    CivSurface? surf = tr.GetObject(surfId, OpenMode.ForWrite) as CivSurface;
                    if (surf != null)
                    {
                        surfaceMap[surf.Name] = surfId;
                        cboSurfaces.Items.Add(surf.Name);
                    }
                }
                catch { continue; }
            }

            if (cboSurfaces.Items.Count > 0)
                cboSurfaces.SelectedIndex = 0;
        }

        private void UpdateUI()
        {
            bool isExisting = rdoExisting.Checked;
            cboSurfaces.Enabled = isExisting;
            lblSurfaceLabel.Enabled = isExisting;
            txtNewName.Enabled = !isExisting;
            lblNewLabel.Enabled = !isExisting;
        }

        private void BtnOK_Click(object? sender, EventArgs e)
        {
            if (rdoExisting.Checked)
            {
                if (cboSurfaces.SelectedItem == null)
                {
                    MessageBox.Show("Vui lòng chọn surface.", "Cảnh báo",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    this.DialogResult = DialogResult.None;
                    return;
                }

                string selectedName = cboSurfaces.SelectedItem.ToString()!;
                SelectedSurfaceId = surfaceMap[selectedName];
                SelectedSurfaceName = selectedName;
                IsNewSurface = false;
            }
            else
            {
                string newName = txtNewName.Text.Trim();
                if (string.IsNullOrEmpty(newName))
                {
                    MessageBox.Show("Vui lòng nhập tên surface mới.", "Cảnh báo",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    this.DialogResult = DialogResult.None;
                    return;
                }

                // Kiểm tra trùng tên
                if (surfaceMap.ContainsKey(newName))
                {
                    var confirmResult = MessageBox.Show(
                        $"Surface '{newName}' đã tồn tại.\nBạn có muốn sử dụng surface này?",
                        "Surface đã tồn tại",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Question);

                    if (confirmResult == DialogResult.Yes)
                    {
                        SelectedSurfaceId = surfaceMap[newName];
                        SelectedSurfaceName = newName;
                        IsNewSurface = false;
                    }
                    else if (confirmResult == DialogResult.No)
                    {
                        this.DialogResult = DialogResult.None;
                        return;
                    }
                    else
                    {
                        this.DialogResult = DialogResult.Cancel;
                        return;
                    }
                }
                else
                {
                    SelectedSurfaceName = newName;
                    IsNewSurface = true;
                }
            }
        }
    }
}

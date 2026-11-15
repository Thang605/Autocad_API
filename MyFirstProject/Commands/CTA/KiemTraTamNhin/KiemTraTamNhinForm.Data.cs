using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Civil3DCsharp;
using MyFirstProject.Extensions;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using CivSurface = Autodesk.Civil.DatabaseServices.TinSurface;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using WinFormsColor = System.Drawing.Color;
using WinFormsFont = System.Drawing.Font;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsPoint = System.Drawing.Point;

namespace MyFirstProject.Civil_Tool
{
    public partial class KiemTraTamNhinForm : Form
    {
        private void LoadCivilObjects()
        {
            cbbAlignment.Items.Clear();
            cbbSurface.Items.Clear();
            cbbSurface.Items.Add(new ObjectIdItem { Name = "(Không sử dụng bề mặt 3D)", Id = ObjectId.Null });

            try
            {
                using (var tr = A.Db.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(A.Db.BlockTableId, OpenMode.ForRead);
                    var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                    foreach (ObjectId id in btr)
                    {
                        if (id.ObjectClass.Name == "AeccDbAlignment")
                        {
                            var align = tr.GetObject(id, OpenMode.ForRead) as Alignment;
                            if (align != null && !align.IsConnectedAlignment)
                            {
                                cbbAlignment.Items.Add(new ObjectIdItem { Name = align.Name, Id = id });
                            }
                        }
                        else if (id.ObjectClass.Name == "AeccDbTinSurface")
                        {
                            var surf = tr.GetObject(id, OpenMode.ForRead) as CivSurface;
                            if (surf != null)
                            {
                                cbbSurface.Items.Add(new ObjectIdItem { Name = surf.Name, Id = id });
                            }
                        }
                    }
                    tr.Commit();
                }

                // Khôi phục Alignment đã chọn
                if (cbbAlignment.Items.Count > 0)
                {
                    int selectIdx = 0;
                    if (!_lastAlignmentId.IsNull && _lastAlignmentId.IsValid && !_lastAlignmentId.IsErased)
                    {
                        for (int i = 0; i < cbbAlignment.Items.Count; i++)
                        {
                            if (((ObjectIdItem)cbbAlignment.Items[i]).Id == _lastAlignmentId)
                            {
                                selectIdx = i;
                                break;
                            }
                        }
                    }
                    cbbAlignment.SelectedIndex = selectIdx;
                }

                // Khôi phục Surface đã chọn
                if (cbbSurface.Items.Count > 0)
                {
                    int surfIdx = 0;
                    if (!_lastSurfaceId.IsNull && _lastSurfaceId.IsValid && !_lastSurfaceId.IsErased)
                    {
                        for (int i = 0; i < cbbSurface.Items.Count; i++)
                        {
                            if (((ObjectIdItem)cbbSurface.Items[i]).Id == _lastSurfaceId)
                            {
                                surfIdx = i;
                                break;
                            }
                        }
                    }
                    cbbSurface.SelectedIndex = surfIdx;
                }

                // Khôi phục Polyline đã chọn
                if (!_lastPolylineId.IsNull && _lastPolylineId.IsValid && !_lastPolylineId.IsErased)
                {
                    _selectedPolylineId = _lastPolylineId;
                    UpdatePolylineDisplayInfo(_selectedPolylineId);
                }
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi nạp đối tượng Civil 3D: {ex.Message}");
            }

            // Nạp danh sách tiêu chuẩn
            var standards = SightStandardRepo.GetAllStandards();
            cbbStandard.DataSource = standards;
            cbbStandard.DisplayMember = "Name";
        }

        private void LoadProfilesForAlignment(ObjectId alignId, ObjectId selectProfileId)
        {
            cbbProfile.Items.Clear();
            cbbProfile.Items.Add(new ObjectIdItem { Name = "(Không sử dụng Profile)", Id = ObjectId.Null });

            if (alignId.IsNull || !alignId.IsValid) return;

            try
            {
                using (var tr = A.Db.TransactionManager.StartTransaction())
                {
                    var align = tr.GetObject(alignId, OpenMode.ForRead) as Alignment;
                    if (align != null)
                    {
                        txtStartStation.Text = $"Km{Math.Floor(align.StartingStation / 1000):0}+{align.StartingStation % 1000:000.00}";
                        txtEndStation.Text = $"Km{Math.Floor(align.EndingStation / 1000):0}+{align.EndingStation % 1000:000.00}";

                        var profIds = align.GetProfileIds();
                        foreach (ObjectId pId in profIds)
                        {
                            var prof = tr.GetObject(pId, OpenMode.ForRead) as Profile;
                            if (prof != null)
                            {
                                cbbProfile.Items.Add(new ObjectIdItem { Name = prof.Name, Id = pId });
                            }
                        }
                    }
                    tr.Commit();
                }

                int profIdx = 0;
                if (!selectProfileId.IsNull && selectProfileId.IsValid && !selectProfileId.IsErased)
                {
                    for (int i = 0; i < cbbProfile.Items.Count; i++)
                    {
                        if (((ObjectIdItem)cbbProfile.Items[i]).Id == selectProfileId)
                        {
                            profIdx = i;
                            break;
                        }
                    }
                }
                else if (cbbProfile.Items.Count > 1 && selectProfileId != ObjectId.Null)
                {
                    profIdx = 1;
                }
                cbbProfile.SelectedIndex = profIdx;
            }
            catch { }
        }

        private void CbbAlignment_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_isLoading) return;
            if (!rbSourceAlignment.Checked) return;

            if (cbbAlignment.SelectedItem is ObjectIdItem alignItem && !alignItem.Id.IsNull)
            {
                LoadProfilesForAlignment(alignItem.Id, _lastProfileId);
            }
        }
    }
}

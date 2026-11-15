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


        private void UpdatePolylineDisplayInfo(ObjectId polyId)
        {
            if (polyId.IsNull || !polyId.IsValid)
            {
                txtPolylineInfo.Text = "(Chưa chọn Polyline)";
                return;
            }

            try
            {
                using (var tr = A.Db.TransactionManager.StartTransaction())
                {
                    var ent = tr.GetObject(polyId, OpenMode.ForRead) as Entity;
                    if (ent is Polyline pl)
                    {
                        txtPolylineInfo.Text = $"2D Polyline (Dài {pl.Length:F1}m, {pl.NumberOfVertices} đỉnh, Layer: {pl.Layer})";
                    }
                    else if (ent is Polyline3d p3d)
                    {
                        double len = p3d.Length;
                        txtPolylineInfo.Text = $"3D Polyline (Dài {len:F1}m, Layer: {p3d.Layer})";
                    }
                    tr.Commit();
                }
            }
            catch { }

            UpdatePolylineStationDisplay();
        }

        private void UpdatePolylineStationDisplay()
        {
            if (_selectedPolylineId.IsNull || !_selectedPolylineId.IsValid)
            {
                txtStartStation.Text = "Km0+000.00";
                txtEndStation.Text = "Km0+000.00";
                return;
            }

            try
            {
                using (var tr = A.Db.TransactionManager.StartTransaction())
                {
                    var curve = tr.GetObject(_selectedPolylineId, OpenMode.ForRead) as Curve;
                    if (curve != null)
                    {
                        double totalLength = curve.GetDistanceAtParameter(curve.EndParam);
                        txtStartStation.Text = "Km0+000.00";
                        txtEndStation.Text = $"Km{Math.Floor(totalLength / 1000):0}+{totalLength % 1000:000.00}";
                    }
                    tr.Commit();
                }
            }
            catch { }
        }

        private void BtnPickPolyline_Click(object sender, EventArgs e)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            using (ed.StartUserInteraction(this))
            {
                PromptEntityOptions peo = new PromptEntityOptions("\nChọn đường Polyline 2D hoặc 3D Polyline trên bản vẽ: ");
                peo.SetRejectMessage("\nChỉ được chọn Polyline hoặc Polyline3d!");
                peo.AddAllowedClass(typeof(Polyline), true);
                peo.AddAllowedClass(typeof(Polyline3d), true);

                PromptEntityResult per = ed.GetEntity(peo);
                if (per.Status == PromptStatus.OK)
                {
                    _selectedPolylineId = per.ObjectId;
                    _lastPolylineId = _selectedPolylineId;
                    UpdatePolylineDisplayInfo(_selectedPolylineId);
                    UpdatePolylineStationDisplay();
                    SaveCurrentSettings();
                }
            }
        }

        private void BtnConvertTo3D_Click(object sender, EventArgs e)
        {
            if (_selectedPolylineId.IsNull || !_selectedPolylineId.IsValid)
            {
                MessageBox.Show("Vui lòng Pick chọn đường Polyline trước!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            CivSurface? surface = null;
            if (cbbSurface.SelectedItem is ObjectIdItem sItem && !sItem.Id.IsNull)
            {
                try
                {
                    using (var tr = A.Db.TransactionManager.StartTransaction())
                    {
                        surface = tr.GetObject(sItem.Id, OpenMode.ForRead) as CivSurface;
                        tr.Commit();
                    }
                }
                catch { }
            }

            try
            {
                ObjectId newPoly3dId = ObjectId.Null;
                using (var tr = A.Db.TransactionManager.StartTransaction())
                {
                    var ent = tr.GetObject(_selectedPolylineId, OpenMode.ForRead);
                    if (ent is Polyline pline)
                    {
                        newPoly3dId = SightDistanceEngine.ConvertPolylineToPolyline3d(pline, surface, 5.0);
                    }
                    else if (ent is Polyline3d)
                    {
                        MessageBox.Show("Đường đã chọn vốn là 3D Polyline!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    tr.Commit();
                }

                if (!newPoly3dId.IsNull)
                {
                    _selectedPolylineId = newPoly3dId;
                    _lastPolylineId = _selectedPolylineId;
                    UpdatePolylineDisplayInfo(_selectedPolylineId);
                    UpdatePolylineStationDisplay();
                    SaveCurrentSettings();
                    MessageBox.Show("Đã chuyển đổi thành công sang 3D Polyline và nạp vào kiểm tra!\nLayer: C3D_TIM_TUYEN_3D_POLYLINE", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Lỗi chuyển đổi Polyline 3D: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }



        private void BtnPickAlignment_Click(object sender, EventArgs e)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            using (ed.StartUserInteraction(this))
            {
                PromptEntityOptions peo = new PromptEntityOptions("\nChọn tim tuyến (Alignment) trên bản vẽ: ");
                peo.SetRejectMessage("\nChỉ được chọn Alignment!");
                peo.AddAllowedClass(typeof(Alignment), true);

                PromptEntityResult per = ed.GetEntity(peo);
                if (per.Status == PromptStatus.OK)
                {
                    ObjectId selId = per.ObjectId;
                    _lastAlignmentId = selId;
                    for (int i = 0; i < cbbAlignment.Items.Count; i++)
                    {
                        if (((ObjectIdItem)cbbAlignment.Items[i]).Id == selId)
                        {
                            cbbAlignment.SelectedIndex = i;
                            break;
                        }
                    }
                    SaveCurrentSettings();
                }
            }
        }

        private void BtnPickSurface_Click(object sender, EventArgs e)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            using (ed.StartUserInteraction(this))
            {
                PromptEntityOptions peo = new PromptEntityOptions("\nChọn Bề mặt địa hình (TIN Surface) trên bản vẽ: ");
                peo.SetRejectMessage("\nChỉ được chọn TinSurface!");
                peo.AddAllowedClass(typeof(CivSurface), true);

                PromptEntityResult per = ed.GetEntity(peo);
                if (per.Status == PromptStatus.OK)
                {
                    ObjectId selId = per.ObjectId;
                    _lastSurfaceId = selId;
                    for (int i = 0; i < cbbSurface.Items.Count; i++)
                    {
                        if (((ObjectIdItem)cbbSurface.Items[i]).Id == selId)
                        {
                            cbbSurface.SelectedIndex = i;
                            break;
                        }
                    }
                    SaveCurrentSettings();
                }
            }
        }
    }
}

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


        private void ZoomToStation(double x, double y)
        {
            try
            {
                var ed = Application.DocumentManager.MdiActiveDocument.Editor;
                using (ViewTableRecord view = ed.GetCurrentView())
                {
                    view.CenterPoint = new Point2d(x, y);
                    view.Height = 80.0;
                    view.Width = 120.0;
                    ed.SetCurrentView(view);
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Lỗi Zoom CAD: {ex.Message}");
            }
        }

        private void DgvResults_DoubleClick(object sender, EventArgs e)
        {
            if (dgvResults.CurrentRow?.Tag is SightCheckItem item)
            {
                ZoomToStation(item.X, item.Y);
            }
        }

        private void BtnZoomCAD_Click(object sender, EventArgs e)
        {
            if (dgvResults.CurrentRow?.Tag is SightCheckItem item)
            {
                ZoomToStation(item.X, item.Y);
            }
            else
            {
                MessageBox.Show("Vui lòng chọn một dòng trong bảng kết quả để Zoom!");
            }
        }

        private void BtnDrawRays_Click(object sender, EventArgs e)
        {
            SaveCurrentSettings();
            if (_analysisResults == null || _analysisResults.Count == 0)
            {
                MessageBox.Show("Chưa có dữ liệu phân tích. Hãy nhấn 'Phân Tích & Kiểm Tra' trước!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                ObjectId alignId = ObjectId.Null;
                if (rbSourceAlignment.Checked && cbbAlignment.SelectedItem is ObjectIdItem aItem)
                {
                    alignId = aItem.Id;
                }

                double laneOffset = (numLaneOffset != null) ? (double)numLaneOffset.Value : 1.75;
                bool drawEyeTrajectory = (chkDrawEyePath != null) ? chkDrawEyePath.Checked : true;

                int count = SightDistanceEngine.DrawSightRaysToCAD(
                    _analysisResults,
                    chkFilterOnlyErrors.Checked,
                    drawEyeTrajectory: drawEyeTrajectory,
                    alignmentId: alignId,
                    polylineId: _selectedPolylineId,
                    laneOffset: laneOffset);

                string extraMsg = drawEyeTrajectory ? "\nĐã vẽ Polyline quỹ đạo mắt người lái xe (Layer: C3D_QUY_DAO_MAT_LAI_XE - Màu Cyan)" : "";
                MessageBox.Show($"Đã vẽ thành công {count} tia ngắm lên bản vẽ CAD!\nLayer: C3D_TAM_NHIN_DAT (Xanh) & C3D_TAM_NHIN_KHUAT (Đỏ){extraMsg}", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Lỗi vẽ tia ngắm: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnDrawMarks_Click(object sender, EventArgs e)
        {
            SaveCurrentSettings();
            if (_analysisResults == null || _analysisResults.Count == 0)
            {
                MessageBox.Show("Chưa có dữ liệu phân tích!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var failOrWarn = _analysisResults.Where(i => i.Status != SightCheckStatus.Pass).ToList();
            if (failOrWarn.Count == 0)
            {
                MessageBox.Show("Tất cả vị trí đều Đạt yêu cầu! Không có lỗi để đánh dấu.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                var db = doc.Database;
                int count = 0;

                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                    string layerMark = "C3D_TAM_NHIN_CANH_BAO";
                    SightDistanceEngine.GetOrCreateLayer(db, tr, layerMark, 1);

                    foreach (var item in failOrWarn)
                    {
                        short colorIdx = (item.Status == SightCheckStatus.Fail) ? (short)1 : (short)2; // 1 = Đỏ, 2 = Vàng

                        var circle = new Circle
                        {
                            Center = new Point3d(item.X, item.Y, 0),
                            Radius = 15.0,
                            Layer = layerMark,
                            ColorIndex = colorIdx
                        };
                        btr.AppendEntity(circle);
                        tr.AddNewlyCreatedDBObject(circle, true);

                        var l1 = new Line(new Point3d(item.X - 20, item.Y, 0), new Point3d(item.X + 20, item.Y, 0)) { Layer = layerMark, ColorIndex = colorIdx };
                        var l2 = new Line(new Point3d(item.X, item.Y - 20, 0), new Point3d(item.X, item.Y + 20, 0)) { Layer = layerMark, ColorIndex = colorIdx };
                        btr.AppendEntity(l1);
                        btr.AppendEntity(l2);
                        tr.AddNewlyCreatedDBObject(l1, true);
                        tr.AddNewlyCreatedDBObject(l2, true);

                        var txt = new DBText
                        {
                            Position = new Point3d(item.X + 18, item.Y + 18, 0),
                            Height = 5.0,
                            TextString = $"[{item.StationFormatted}] {item.Detail}",
                            Layer = layerMark,
                            ColorIndex = colorIdx
                        };
                        btr.AppendEntity(txt);
                        tr.AddNewlyCreatedDBObject(txt, true);

                        count++;
                    }

                    tr.Commit();
                }

                MessageBox.Show($"Đã đánh dấu {count} vị trí cảnh báo / không đạt lên mặt bằng tuyến CAD!\nLayer: C3D_TAM_NHIN_CANH_BAO", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Lỗi đánh dấu CAD: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}

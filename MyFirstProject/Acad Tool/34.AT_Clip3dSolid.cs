// (C) Copyright 2026 by T27
// Lệnh cắt / clip đối tượng 3D Solid bằng Polyline khép kín trên mặt bằng (Plan view)
//
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using MyFirstProject.Acad_Tool;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Exception = System.Exception;
using Region = Autodesk.AutoCAD.DatabaseServices.Region;

[assembly: CommandClass(typeof(Civil3DCsharp.AT_Clip3dSolidCmd))]

namespace Civil3DCsharp
{
    /// <summary>
    /// Class chứa lệnh cắt 3D Solid theo Polyline khép kín trên mặt bằng
    /// Các lệnh: CLIP_SOLID, CLIPSOLID, CLIP3DSOLID, CSOLID, AT_Clip3dSolid
    /// </summary>
    public class AT_Clip3dSolidCmd
    {
        [CommandMethod("CLIP_SOLID")]
        [CommandMethod("CLIPSOLID")]
        [CommandMethod("CLIP3DSOLID")]
        [CommandMethod("CSOLID")]
        [CommandMethod("AT_Clip3dSolid")]
        public static void Clip3dSolid()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            Database db = doc.Database;
            Editor ed = doc.Editor;

            ed.WriteMessage("\n═══════════════════════════════════════════════════════════════");
            ed.WriteMessage("\n  CLIP_SOLID - CẮT 3D SOLID THEO POLYLINE KHÉP KÍN MẶT BẰNG");
            ed.WriteMessage("\n═══════════════════════════════════════════════════════════════\n");

            using (Clip3dSolidForm form = new Clip3dSolidForm())
            {
                DialogResult dr = Application.ShowModalDialog(form);
                if (dr != DialogResult.OK || !form.IsConfirmed)
                {
                    ed.WriteMessage("\n[Hủy bỏ] Thao tác cắt 3D Solid đã được hủy.");
                    return;
                }

                // Thực thi phép cắt trong transaction
                ExecuteClipping(doc, form);
            }
        }

        private static void ExecuteClipping(Document doc, Clip3dSolidForm form)
        {
            Database db = doc.Database;
            Editor ed = doc.Editor;

            using (DocumentLock docLock = doc.LockDocument())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    try
                    {
                        // 1. Kiểm tra đối tượng ranh giới
                        Entity boundaryEnt = tr.GetObject(form.BoundaryId, OpenMode.ForRead) as Entity;
                        if (boundaryEnt == null)
                        {
                            ed.WriteMessage("\n[Lỗi] Không thể mở đối tượng ranh giới.");
                            return;
                        }

                        // 2. Tạo Region từ đường ranh giới
                        Region region = CreateRegionFromEntity(boundaryEnt, tr, ed);
                        if (region == null)
                        {
                            ed.WriteMessage("\n[Lỗi] Không thể tạo Region từ đường ranh giới được chọn.");
                            ed.WriteMessage("\n👉 Vui lòng kiểm tra lại: Đường Polyline có thể bị tự cắt nhau (giao cắt chính nó dạng số 8) hoặc có các đoạn gấp khúc trùng lặp.");
                            return;
                        }

                        // 3. Thu thập các khối Solid3d hợp lệ và tính toán Z min/max
                        List<Solid3d> targetSolids = new List<Solid3d>();
                        double minZ = double.MaxValue;
                        double maxZ = double.MinValue;

                        foreach (ObjectId solidId in form.SelectedSolidIds)
                        {
                            if (solidId.IsNull || !solidId.IsValid || solidId.IsErased) continue;

                            Solid3d s = tr.GetObject(solidId, OpenMode.ForRead) as Solid3d;
                            if (s != null && !s.IsErased)
                            {
                                targetSolids.Add(s);
                                try
                                {
                                    Extents3d ext = s.GeometricExtents;
                                    if (!double.IsNaN(ext.MinPoint.Z) && !double.IsInfinity(ext.MinPoint.Z) &&
                                        !double.IsNaN(ext.MaxPoint.Z) && !double.IsInfinity(ext.MaxPoint.Z))
                                    {
                                        if (ext.MinPoint.Z < minZ) minZ = ext.MinPoint.Z;
                                        if (ext.MaxPoint.Z > maxZ) maxZ = ext.MaxPoint.Z;
                                    }
                                }
                                catch { }
                            }
                        }

                        if (targetSolids.Count == 0)
                        {
                            ed.WriteMessage("\n[Lỗi] Không có khối 3D Solid hợp lệ nào để cắt.");
                            region.Dispose();
                            return;
                        }

                        if (minZ >= maxZ || double.IsInfinity(minZ) || double.IsInfinity(maxZ))
                        {
                            minZ = -100.0;
                            maxZ = 100.0;
                        }

                        // 4. Tính toán chiều cao đùn & tạo khối dao cắt (baseCutter)
                        double buffer = form.BufferHeight > 0.001 ? form.BufferHeight : 1.0;
                        double targetMinZ = minZ - buffer;
                        double targetMaxZ = maxZ + buffer;
                        double extrusionHeight = targetMaxZ - targetMinZ;
                        if (extrusionHeight < 0.1) extrusionHeight = 10.0;

                        Solid3d baseCutter = new Solid3d();
                        baseCutter.RecordHistory = false;

                        try
                        {
                            baseCutter.Extrude(region, extrusionHeight, 0.0);
                        }
                        catch (Exception ex)
                        {
                            ed.WriteMessage($"\n[Lỗi khi tạo khối đùn dao cắt]: {ex.Message}");
                            region.Dispose();
                            baseCutter.Dispose();
                            return;
                        }
                        region.Dispose();

                        // Định vị khối cắt theo phương Z sao cho đáy của nó nằm tại targetMinZ
                        try
                        {
                            baseCutter.TransformBy(Matrix3d.Displacement(new Vector3d(0, 0, targetMinZ)));
                        }
                        catch { }

                        // 5. Chuẩn bị Layer nếu người dùng yêu cầu
                        if (form.AssignLayer)
                        {
                            if (form.ClipMode == ClipSolidMode.KeepInside || form.ClipMode == ClipSolidMode.SplitBoth)
                            {
                                EnsureLayerExists(db, tr, form.LayerInside, 3); // Xanh lá cây
                            }
                            if (form.ClipMode == ClipSolidMode.KeepOutside || form.ClipMode == ClipSolidMode.SplitBoth)
                            {
                                EnsureLayerExists(db, tr, form.LayerOutside, 1); // Đỏ
                            }
                        }

                        // 6. Thực hiện phép toán Boolean cho từng Solid
                        int totalProcessed = 0;
                        int createdInsideCount = 0;
                        int createdOutsideCount = 0;
                        int erasedOriginalCount = 0;
                        int failedCount = 0;

                        foreach (Solid3d originalSolid in targetSolids)
                        {
                            totalProcessed++;
                            bool anyCreated = false;

                            // Không gian chứa solid hiện tại (ModelSpace hoặc PaperSpace)
                            BlockTableRecord targetSpace = (BlockTableRecord)tr.GetObject(originalSolid.BlockId, OpenMode.ForWrite);

                            // Mở khóa layer nếu layer đang bị khóa và cần xóa đối tượng gốc
                            if (form.DeleteOriginal)
                            {
                                UnlockLayerIfLocked(db, tr, originalSolid.LayerId);
                            }

                            if (form.ClipMode == ClipSolidMode.KeepInside)
                            {
                                Solid3d insideClone = (Solid3d)originalSolid.Clone();
                                insideClone.RecordHistory = false;

                                Solid3d cutterClone = (Solid3d)baseCutter.Clone();
                                cutterClone.RecordHistory = false;

                                // BẮT BUỘC: Cả 2 Solid phải được thêm vào Database trước khi gọi BooleanOperation
                                targetSpace.AppendEntity(insideClone);
                                tr.AddNewlyCreatedDBObject(insideClone, true);

                                targetSpace.AppendEntity(cutterClone);
                                tr.AddNewlyCreatedDBObject(cutterClone, true);

                                try
                                {
                                    insideClone.BooleanOperation(BooleanOperationType.BoolIntersect, cutterClone);
                                    if (IsValidSolid(insideClone))
                                    {
                                        if (form.AssignLayer) insideClone.Layer = form.LayerInside;
                                        createdInsideCount++;
                                        anyCreated = true;
                                    }
                                    else
                                    {
                                        insideClone.Erase(true);
                                        failedCount++;
                                    }
                                }
                                catch
                                {
                                    if (!insideClone.IsErased) insideClone.Erase(true);
                                    if (!cutterClone.IsErased) cutterClone.Erase(true);
                                    failedCount++;
                                }
                            }
                            else if (form.ClipMode == ClipSolidMode.KeepOutside)
                            {
                                Solid3d outsideClone = (Solid3d)originalSolid.Clone();
                                outsideClone.RecordHistory = false;

                                Solid3d cutterClone = (Solid3d)baseCutter.Clone();
                                cutterClone.RecordHistory = false;

                                targetSpace.AppendEntity(outsideClone);
                                tr.AddNewlyCreatedDBObject(outsideClone, true);

                                targetSpace.AppendEntity(cutterClone);
                                tr.AddNewlyCreatedDBObject(cutterClone, true);

                                try
                                {
                                    outsideClone.BooleanOperation(BooleanOperationType.BoolSubtract, cutterClone);
                                    if (IsValidSolid(outsideClone))
                                    {
                                        if (form.AssignLayer) outsideClone.Layer = form.LayerOutside;
                                        createdOutsideCount++;
                                        anyCreated = true;
                                    }
                                    else
                                    {
                                        outsideClone.Erase(true);
                                        failedCount++;
                                    }
                                }
                                catch
                                {
                                    if (!outsideClone.IsErased) outsideClone.Erase(true);
                                    if (!cutterClone.IsErased) cutterClone.Erase(true);
                                    failedCount++;
                                }
                            }
                            else // SplitBoth
                            {
                                // 1. Phần bên trong (Intersect)
                                Solid3d insideClone = (Solid3d)originalSolid.Clone();
                                insideClone.RecordHistory = false;
                                Solid3d cutterIn = (Solid3d)baseCutter.Clone();
                                cutterIn.RecordHistory = false;

                                targetSpace.AppendEntity(insideClone);
                                tr.AddNewlyCreatedDBObject(insideClone, true);
                                targetSpace.AppendEntity(cutterIn);
                                tr.AddNewlyCreatedDBObject(cutterIn, true);

                                try
                                {
                                    insideClone.BooleanOperation(BooleanOperationType.BoolIntersect, cutterIn);
                                    if (IsValidSolid(insideClone))
                                    {
                                        if (form.AssignLayer) insideClone.Layer = form.LayerInside;
                                        createdInsideCount++;
                                        anyCreated = true;
                                    }
                                    else
                                    {
                                        insideClone.Erase(true);
                                    }
                                }
                                catch
                                {
                                    if (!insideClone.IsErased) insideClone.Erase(true);
                                    if (!cutterIn.IsErased) cutterIn.Erase(true);
                                }

                                // 2. Phần bên ngoài (Subtract)
                                Solid3d outsideClone = (Solid3d)originalSolid.Clone();
                                outsideClone.RecordHistory = false;
                                Solid3d cutterOut = (Solid3d)baseCutter.Clone();
                                cutterOut.RecordHistory = false;

                                targetSpace.AppendEntity(outsideClone);
                                tr.AddNewlyCreatedDBObject(outsideClone, true);
                                targetSpace.AppendEntity(cutterOut);
                                tr.AddNewlyCreatedDBObject(cutterOut, true);

                                try
                                {
                                    outsideClone.BooleanOperation(BooleanOperationType.BoolSubtract, cutterOut);
                                    if (IsValidSolid(outsideClone))
                                    {
                                        if (form.AssignLayer) outsideClone.Layer = form.LayerOutside;
                                        createdOutsideCount++;
                                        anyCreated = true;
                                    }
                                    else
                                    {
                                        outsideClone.Erase(true);
                                    }
                                }
                                catch
                                {
                                    if (!outsideClone.IsErased) outsideClone.Erase(true);
                                    if (!cutterOut.IsErased) cutterOut.Erase(true);
                                }
                            }

                            // Xóa đối tượng gốc nếu được yêu cầu và đã tạo ra kết quả
                            if (anyCreated && form.DeleteOriginal)
                            {
                                originalSolid.UpgradeOpen();
                                originalSolid.Erase(true);
                                erasedOriginalCount++;
                            }
                        }

                        baseCutter.Dispose();
                        tr.Commit();

                        // 7. Báo cáo thống kê chi tiết
                        ed.WriteMessage("\n=======================================================");
                        ed.WriteMessage("\n🎉 HOÀN THÀNH CẮT 3D SOLID THEO POLYLINE MẶT BẰNG!");
                        ed.WriteMessage($"\n  • Tổng 3D Solid xử lý:          {totalProcessed}");
                        if (createdInsideCount > 0)
                            ed.WriteMessage($"\n  • Số khối tạo ra bên trong:    {createdInsideCount}" + (form.AssignLayer ? $" (Layer: {form.LayerInside})" : ""));
                        if (createdOutsideCount > 0)
                            ed.WriteMessage($"\n  • Số khối tạo ra bên ngoài:    {createdOutsideCount}" + (form.AssignLayer ? $" (Layer: {form.LayerOutside})" : ""));
                        if (erasedOriginalCount > 0)
                            ed.WriteMessage($"\n  • Số 3D Solid gốc đã xóa:      {erasedOriginalCount}");
                        if (failedCount > 0)
                            ed.WriteMessage($"\n  • Số đối tượng không cắt được: {failedCount} (do không giao cắt ranh giới)");
                        ed.WriteMessage("\n=======================================================\n");
                    }
                    catch (Exception ex)
                    {
                        tr.Abort();
                        ed.WriteMessage($"\n[Lỗi ngoại lệ khi cắt 3D Solid]: {ex.Message}");
                    }
                }
            }
        }

        #region Helper Geometry Methods
        private static Region CreateRegionFromEntity(Entity ent, Transaction tr, Editor ed)
        {
            if (ent == null) return null;

            ed.WriteMessage($"\n[Kiểm tra ranh giới]: Loại đối tượng = {ent.GetType().Name}, Layer = {ent.Layer}");

            // ---------------------------------------------------------------
            // CHIẾN LƯỢC 1: Polyline chuẩn hóa phẳng (Elevation = 0, Normal = ZAxis)
            // ---------------------------------------------------------------
            if (ent is Polyline pl)
            {
                try
                {
                    Polyline plClone = (Polyline)pl.Clone();
                    plClone.Elevation = 0.0;
                    if (!plClone.Closed) plClone.Closed = true;

                    // Loại bỏ đỉnh cuối nếu trùng với đỉnh đầu
                    int n = plClone.NumberOfVertices;
                    if (n > 2)
                    {
                        Point2d pFirst = plClone.GetPoint2dAt(0);
                        Point2d pLast = plClone.GetPoint2dAt(n - 1);
                        if (pFirst.GetDistanceTo(pLast) < 1e-4)
                        {
                            plClone.RemoveVertexAt(n - 1);
                        }
                    }

                    using (DBObjectCollection curves = new DBObjectCollection())
                    {
                        curves.Add(plClone);
                        using (DBObjectCollection regions = Region.CreateFromCurves(curves))
                        {
                            if (regions != null && regions.Count > 0)
                            {
                                Region reg = (Region)regions[0];
                                for (int i = 1; i < regions.Count; i++) regions[i].Dispose();
                                plClone.Dispose();
                                return reg;
                            }
                        }
                    }
                    plClone.Dispose();
                }
                catch (Exception ex)
                {
                    ed.WriteMessage($"\n  • Chiến lược 1 (Polyline chuẩn hóa): {ex.Message}");
                }
            }

            // ---------------------------------------------------------------
            // CHIẾN LƯỢC 2: Rã thành các đoạn Line & Arc phẳng (Explode to 2D)
            // ---------------------------------------------------------------
            if (ent is Curve curve)
            {
                try
                {
                    DBObjectCollection exploded = new DBObjectCollection();
                    curve.Explode(exploded);

                    if (exploded.Count > 0)
                    {
                        DBObjectCollection flatCurves = new DBObjectCollection();
                        foreach (DBObject obj in exploded)
                        {
                            if (obj is Line line)
                            {
                                Line flatLine = new Line(
                                    new Point3d(line.StartPoint.X, line.StartPoint.Y, 0.0),
                                    new Point3d(line.EndPoint.X, line.EndPoint.Y, 0.0)
                                );
                                if (flatLine.Length > 1e-5) flatCurves.Add(flatLine);
                                line.Dispose();
                            }
                            else if (obj is Arc arc)
                            {
                                Arc flatArc = new Arc(
                                    new Point3d(arc.Center.X, arc.Center.Y, 0.0),
                                    Vector3d.ZAxis,
                                    arc.Radius,
                                    arc.StartAngle,
                                    arc.EndAngle
                                );
                                flatCurves.Add(flatArc);
                                arc.Dispose();
                            }
                            else if (obj is Curve otherCurve)
                            {
                                flatCurves.Add(otherCurve);
                            }
                            else
                            {
                                obj.Dispose();
                            }
                        }

                        // Nếu 2 đầu chưa khép kín, bổ sung đoạn thẳng nối
                        Point3d startPt = curve.StartPoint;
                        Point3d endPt = curve.EndPoint;
                        Point3d flatStart = new Point3d(startPt.X, startPt.Y, 0.0);
                        Point3d flatEnd = new Point3d(endPt.X, endPt.Y, 0.0);
                        if (flatStart.DistanceTo(flatEnd) > 1e-4)
                        {
                            flatCurves.Add(new Line(flatEnd, flatStart));
                        }

                        using (DBObjectCollection regions = Region.CreateFromCurves(flatCurves))
                        {
                            foreach (DBObject c in flatCurves) c.Dispose();

                            if (regions != null && regions.Count > 0)
                            {
                                Region reg = (Region)regions[0];
                                for (int i = 1; i < regions.Count; i++) regions[i].Dispose();
                                return reg;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ed.WriteMessage($"\n  • Chiến lược 2 (Explode phân đoạn 2D): {ex.Message}");
                }
            }

            // ---------------------------------------------------------------
            // CHIẾN LƯỢC 3: Tái tạo đa giác phẳng WCS (WCS Planar Polygon)
            // ---------------------------------------------------------------
            try
            {
                List<Point2d> wcsPoints = new List<Point2d>();

                if (ent is Polyline plEnt)
                {
                    for (int i = 0; i < plEnt.NumberOfVertices; i++)
                    {
                        Point3d p = plEnt.GetPoint3dAt(i);
                        Point2d pt2 = new Point2d(p.X, p.Y);
                        if (wcsPoints.Count == 0 || wcsPoints[wcsPoints.Count - 1].GetDistanceTo(pt2) > 1e-4)
                            wcsPoints.Add(pt2);
                    }
                }
                else if (ent is Polyline2d p2d)
                {
                    foreach (ObjectId vId in p2d)
                    {
                        Vertex2d v = tr.GetObject(vId, OpenMode.ForRead) as Vertex2d;
                        if (v != null)
                        {
                            Point2d pt2 = new Point2d(v.Position.X, v.Position.Y);
                            if (wcsPoints.Count == 0 || wcsPoints[wcsPoints.Count - 1].GetDistanceTo(pt2) > 1e-4)
                                wcsPoints.Add(pt2);
                        }
                    }
                }
                else if (ent is Polyline3d p3d)
                {
                    foreach (ObjectId vId in p3d)
                    {
                        PolylineVertex3d v = tr.GetObject(vId, OpenMode.ForRead) as PolylineVertex3d;
                        if (v != null)
                        {
                            Point2d pt2 = new Point2d(v.Position.X, v.Position.Y);
                            if (wcsPoints.Count == 0 || wcsPoints[wcsPoints.Count - 1].GetDistanceTo(pt2) > 1e-4)
                                wcsPoints.Add(pt2);
                        }
                    }
                }
                else if (ent is Circle c)
                {
                    Circle flatC = new Circle(new Point3d(c.Center.X, c.Center.Y, 0.0), Vector3d.ZAxis, c.Radius);
                    using (DBObjectCollection curves = new DBObjectCollection())
                    {
                        curves.Add(flatC);
                        using (DBObjectCollection regions = Region.CreateFromCurves(curves))
                        {
                            if (regions != null && regions.Count > 0)
                            {
                                Region reg = (Region)regions[0];
                                for (int i = 1; i < regions.Count; i++) regions[i].Dispose();
                                flatC.Dispose();
                                return reg;
                            }
                        }
                    }
                    flatC.Dispose();
                }

                // Loại bỏ điểm cuối nếu trùng điểm đầu
                if (wcsPoints.Count > 2 && wcsPoints[wcsPoints.Count - 1].GetDistanceTo(wcsPoints[0]) < 1e-4)
                {
                    wcsPoints.RemoveAt(wcsPoints.Count - 1);
                }

                if (wcsPoints.Count >= 3)
                {
                    Polyline polyWcs = new Polyline();
                    for (int i = 0; i < wcsPoints.Count; i++)
                    {
                        polyWcs.AddVertexAt(i, wcsPoints[i], 0.0, 0.0, 0.0);
                    }
                    polyWcs.Closed = true;
                    polyWcs.Elevation = 0.0;
                    polyWcs.Normal = Vector3d.ZAxis;

                    using (DBObjectCollection curves = new DBObjectCollection())
                    {
                        curves.Add(polyWcs);
                        using (DBObjectCollection regions = Region.CreateFromCurves(curves))
                        {
                            if (regions != null && regions.Count > 0)
                            {
                                Region reg = (Region)regions[0];
                                for (int i = 1; i < regions.Count; i++) regions[i].Dispose();
                                polyWcs.Dispose();
                                return reg;
                            }
                        }
                    }
                    polyWcs.Dispose();
                }
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n  • Chiến lược 3 (Tái tạo đa giác WCS): {ex.Message}");
            }

            // ---------------------------------------------------------------
            // CHIẾN LƯỢC 4: Trực tiếp từ bản sao ent (Direct Clone)
            // ---------------------------------------------------------------
            if (ent is Curve directCurve)
            {
                try
                {
                    using (DBObjectCollection curves = new DBObjectCollection())
                    {
                        Curve cloned = (Curve)directCurve.Clone();
                        if (cloned is Polyline cPl && !cPl.Closed) cPl.Closed = true;
                        curves.Add(cloned);
                        using (DBObjectCollection regions = Region.CreateFromCurves(curves))
                        {
                            if (regions != null && regions.Count > 0)
                            {
                                Region reg = (Region)regions[0];
                                for (int i = 1; i < regions.Count; i++) regions[i].Dispose();
                                cloned.Dispose();
                                return reg;
                            }
                        }
                        cloned.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    ed.WriteMessage($"\n  • Chiến lược 4 (Trực tiếp từ clone): {ex.Message}");
                }
            }

            return null;
        }


        private static bool IsValidSolid(Solid3d solid)
        {
            if (solid == null || solid.IsDisposed) return false;
            try
            {
                var mp = solid.MassProperties;
                if (mp.Volume > 1e-7) return true;
            }
            catch { }
            return false;
        }

        private static void UnlockLayerIfLocked(Database db, Transaction tr, ObjectId layerId)
        {
            try
            {
                LayerTableRecord ltr = tr.GetObject(layerId, OpenMode.ForRead) as LayerTableRecord;
                if (ltr != null && ltr.IsLocked)
                {
                    ltr.UpgradeOpen();
                    ltr.IsLocked = false;
                }
            }
            catch { }
        }

        private static void EnsureLayerExists(Database db, Transaction tr, string layerName, short colorIndex = 7)
        {
            if (string.IsNullOrWhiteSpace(layerName)) return;

            LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (!lt.Has(layerName))
            {
                lt.UpgradeOpen();
                LayerTableRecord ltr = new LayerTableRecord
                {
                    Name = layerName,
                    Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, colorIndex)
                };
                lt.Add(ltr);
                tr.AddNewlyCreatedDBObject(ltr, true);
            }
        }
        #endregion
    }
}

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
    /// Lệnh: AT_Clip_3d_Solid
    /// </summary>
    public class AT_Clip3dSolidCmd
    {
        [CommandMethod("AT_Clip_3d_Solid")]
        public static void Clip3dSolid()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            Database db = doc.Database;
            Editor ed = doc.Editor;

            ed.WriteMessage("\n═══════════════════════════════════════════════════════════════");
            ed.WriteMessage("\n  AT_Clip_3d_Solid - CẮT 3D SOLID THEO POLYLINE KHÉP KÍN MẶT BẰNG");
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
                        // 1. Thu thập các khối Solid3d hợp lệ và tính toán Z min/max
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
                            return;
                        }

                        if (minZ >= maxZ || double.IsInfinity(minZ) || double.IsInfinity(maxZ))
                        {
                            minZ = -100.0;
                            maxZ = 100.0;
                        }

                        // 2. Tính toán chiều cao đùn & tạo danh sách khối dao cắt (baseCutters)
                        double buffer = form.BufferHeight > 0.001 ? form.BufferHeight : 1.0;
                        double targetMinZ = minZ - buffer;
                        double targetMaxZ = maxZ + buffer;
                        double extrusionHeight = targetMaxZ - targetMinZ;
                        if (extrusionHeight < 0.1) extrusionHeight = 10.0;

                        List<Solid3d> baseCutters = new List<Solid3d>();

                        foreach (ObjectId bId in form.SelectedBoundaryIds)
                        {
                            if (bId.IsNull || !bId.IsValid || bId.IsErased) continue;
                            Entity boundaryEnt = tr.GetObject(bId, OpenMode.ForRead) as Entity;
                            if (boundaryEnt == null) continue;

                            Region region = CreateRegionFromEntity(boundaryEnt, tr, ed);
                            if (region != null)
                            {
                                try
                                {
                                    Solid3d cutter = new Solid3d();
                                    cutter.RecordHistory = false;
                                    cutter.Extrude(region, extrusionHeight, 0.0);
                                    cutter.TransformBy(Matrix3d.Displacement(new Vector3d(0, 0, targetMinZ)));
                                    baseCutters.Add(cutter);
                                }
                                catch (Exception ex)
                                {
                                    ed.WriteMessage($"\n[Lỗi khi tạo khối đùn dao cắt]: {ex.Message}");
                                }
                                finally
                                {
                                    region.Dispose();
                                }
                            }
                        }

                        if (baseCutters.Count == 0)
                        {
                            ed.WriteMessage("\n[Lỗi] Không thể tạo khối dao cắt từ bất kỳ ranh giới Polyline nào được chọn.");
                            return;
                        }

                        // 3. Chuẩn bị Layer nếu người dùng yêu cầu
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

                        // 4. Thực hiện phép toán Boolean cho từng Solid
                        int totalProcessed = 0;
                        int createdInsideCount = 0;
                        int createdOutsideCount = 0;
                        int erasedOriginalCount = 0;
                        int completelyInsideErasedCount = 0;
                        int unaffectedCount = 0;
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
                                foreach (Solid3d baseCutter in baseCutters)
                                {
                                    Solid3d insideClone = (Solid3d)originalSolid.Clone();
                                    insideClone.RecordHistory = false;

                                    Solid3d cutterClone = (Solid3d)baseCutter.Clone();
                                    cutterClone.RecordHistory = false;

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
                                        }
                                    }
                                    catch
                                    {
                                        if (!insideClone.IsErased) insideClone.Erase(true);
                                        if (!cutterClone.IsErased) cutterClone.Erase(true);
                                    }
                                }
                                if (!anyCreated)
                                {
                                    unaffectedCount++;
                                }
                            }
                            else if (form.ClipMode == ClipSolidMode.KeepOutside)
                            {
                                double origVolume = 0.0;
                                try { origVolume = originalSolid.MassProperties.Volume; } catch { }

                                Solid3d outsideClone = (Solid3d)originalSolid.Clone();
                                outsideClone.RecordHistory = false;

                                targetSpace.AppendEntity(outsideClone);
                                tr.AddNewlyCreatedDBObject(outsideClone, true);

                                bool isCompletelyInside = false;

                                foreach (Solid3d baseCutter in baseCutters)
                                {
                                    if (!IsValidSolid(outsideClone)) break;

                                    Solid3d cutterClone = (Solid3d)baseCutter.Clone();
                                    cutterClone.RecordHistory = false;

                                    targetSpace.AppendEntity(cutterClone);
                                    tr.AddNewlyCreatedDBObject(cutterClone, true);

                                    try
                                    {
                                        outsideClone.BooleanOperation(BooleanOperationType.BoolSubtract, cutterClone);
                                    }
                                    catch
                                    {
                                        if (!cutterClone.IsErased) cutterClone.Erase(true);
                                    }
                                }

                                if (IsValidSolid(outsideClone))
                                {
                                    double remainingVol = 0.0;
                                    try { remainingVol = outsideClone.MassProperties.Volume; } catch { }

                                    // Nếu thể tích còn lại gần bằng 0 -> solid bị cutter trừ sạch hoàn toàn
                                    if (origVolume > 1e-6 && remainingVol <= 1e-6)
                                    {
                                        isCompletelyInside = true;
                                        outsideClone.Erase(true);
                                    }
                                    // Nếu thể tích không đổi so với ban đầu -> solid này nằm hoàn toàn ở ngoài ranh giới
                                    else if (origVolume > 1e-6 && Math.Abs(remainingVol - origVolume) < 1e-5)
                                    {
                                        outsideClone.Erase(true);
                                        unaffectedCount++;
                                    }
                                    else
                                    {
                                        if (form.AssignLayer) outsideClone.Layer = form.LayerOutside;
                                        createdOutsideCount++;
                                        anyCreated = true;
                                    }
                                }
                                else
                                {
                                    outsideClone.Erase(true);
                                    // Phép Subtract làm solid biến mất -> kiểm tra xem có phải nằm trọn bên trong ranh giới
                                    isCompletelyInside = CheckIfSolidIntersectsCutters(originalSolid, baseCutters, targetSpace, tr);
                                    if (!isCompletelyInside)
                                    {
                                        unaffectedCount++;
                                    }
                                }

                                // Nếu solid nằm trọn bên trong ranh giới và người dùng chọn xóa đối tượng gốc
                                if (isCompletelyInside)
                                {
                                    if (form.DeleteOriginal && !originalSolid.IsErased)
                                    {
                                        originalSolid.UpgradeOpen();
                                        originalSolid.Erase(true);
                                        erasedOriginalCount++;
                                        completelyInsideErasedCount++;
                                    }
                                }
                            }
                            else // SplitBoth
                            {
                                // 1. Các phần bên trong (Intersect với từng cutter)
                                foreach (Solid3d baseCutter in baseCutters)
                                {
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
                                }

                                // 2. Phần bên ngoài (Subtract tuần tự tất cả cutters)
                                Solid3d outsideClone = (Solid3d)originalSolid.Clone();
                                outsideClone.RecordHistory = false;
                                targetSpace.AppendEntity(outsideClone);
                                tr.AddNewlyCreatedDBObject(outsideClone, true);

                                foreach (Solid3d baseCutter in baseCutters)
                                {
                                    if (!IsValidSolid(outsideClone)) break;

                                    Solid3d cutterOut = (Solid3d)baseCutter.Clone();
                                    cutterOut.RecordHistory = false;
                                    targetSpace.AppendEntity(cutterOut);
                                    tr.AddNewlyCreatedDBObject(cutterOut, true);

                                    try
                                    {
                                        outsideClone.BooleanOperation(BooleanOperationType.BoolSubtract, cutterOut);
                                    }
                                    catch
                                    {
                                        if (!cutterOut.IsErased) cutterOut.Erase(true);
                                    }
                                }

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

                            // Xóa đối tượng gốc nếu được yêu cầu và đã tạo ra kết quả
                            if (!originalSolid.IsErased && anyCreated && form.DeleteOriginal)
                            {
                                originalSolid.UpgradeOpen();
                                originalSolid.Erase(true);
                                erasedOriginalCount++;
                            }
                        }

                        foreach (Solid3d cutter in baseCutters)
                        {
                            cutter.Dispose();
                        }
                        tr.Commit();

                        // 5. Báo cáo thống kê chi tiết
                        ed.WriteMessage("\n=======================================================");
                        ed.WriteMessage("\n🎉 HOÀN THÀNH CẮT / XÓA 3D SOLID THEO POLYLINE MẶT BẰNG!");
                        ed.WriteMessage($"\n  • Số đường ranh giới Polyline áp dụng:    {baseCutters.Count}");
                        ed.WriteMessage($"\n  • Tổng 3D Solid xử lý:                    {totalProcessed}");
                        if (createdInsideCount > 0)
                            ed.WriteMessage($"\n  • Số khối tạo ra bên trong:              {createdInsideCount}" + (form.AssignLayer ? $" (Layer: {form.LayerInside})" : ""));
                        if (createdOutsideCount > 0)
                            ed.WriteMessage($"\n  • Số khối cắt còn lại bên ngoài:          {createdOutsideCount}" + (form.AssignLayer ? $" (Layer: {form.LayerOutside})" : ""));
                        if (completelyInsideErasedCount > 0)
                            ed.WriteMessage($"\n  • Số 3D Solid nằm trọn bên trong đã xóa:  {completelyInsideErasedCount}");
                        if (erasedOriginalCount > 0)
                            ed.WriteMessage($"\n  • Tổng số 3D Solid gốc đã xóa:           {erasedOriginalCount}");
                        if (unaffectedCount > 0)
                            ed.WriteMessage($"\n  • Số 3D Solid ngoài ranh giới (giữ nguyên): {unaffectedCount}");
                        if (failedCount > 0)
                            ed.WriteMessage($"\n  • Số đối tượng lỗi kỹ thuật:              {failedCount}");
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

        private static bool CheckIfSolidIntersectsCutter(Solid3d solid, Solid3d cutter, BlockTableRecord space, Transaction tr, out double intersectVolume)
        {
            intersectVolume = 0.0;
            Solid3d testInside = null;
            Solid3d testCutter = null;
            try
            {
                testInside = (Solid3d)solid.Clone();
                testInside.RecordHistory = false;
                testCutter = (Solid3d)cutter.Clone();
                testCutter.RecordHistory = false;

                space.AppendEntity(testInside);
                tr.AddNewlyCreatedDBObject(testInside, true);

                space.AppendEntity(testCutter);
                tr.AddNewlyCreatedDBObject(testCutter, true);

                testInside.BooleanOperation(BooleanOperationType.BoolIntersect, testCutter);
                if (IsValidSolid(testInside))
                {
                    try
                    {
                        intersectVolume = testInside.MassProperties.Volume;
                    }
                    catch { }
                    return intersectVolume > 1e-6;
                }
            }
            catch
            {
                return false;
            }
            finally
            {
                if (testInside != null && !testInside.IsDisposed && !testInside.IsErased)
                {
                    try { testInside.Erase(true); } catch { }
                }
                if (testCutter != null && !testCutter.IsDisposed && !testCutter.IsErased)
                {
                    try { testCutter.Erase(true); } catch { }
                }
            }
            return false;
        }

        private static bool CheckIfSolidIntersectsCutters(Solid3d solid, List<Solid3d> cutters, BlockTableRecord space, Transaction tr)
        {
            foreach (Solid3d cutter in cutters)
            {
                if (CheckIfSolidIntersectsCutter(solid, cutter, space, tr, out double intersectVolume) && intersectVolume > 1e-6)
                {
                    return true;
                }
            }
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

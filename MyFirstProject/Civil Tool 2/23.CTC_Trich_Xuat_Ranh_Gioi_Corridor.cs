using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Colors;
using Autodesk.Civil.DatabaseServices;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using CadEntity = Autodesk.AutoCAD.DatabaseServices.Entity;
using CadColor = Autodesk.AutoCAD.Colors.Color;
using AcRegion = Autodesk.AutoCAD.DatabaseServices.Region;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;
using Exception = System.Exception;

[assembly: CommandClass(typeof(Civil3DCsharp.CTC_Trich_Xuat_Ranh_Gioi_Corridor_Command))]

namespace Civil3DCsharp
{
    public class CTC_Trich_Xuat_Ranh_Gioi_Corridor_Command
    {
        [CommandMethod("CTC_Trich_Xuat_Ranh_Gioi_Corridor")]
        public static void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var db = doc.Database;
            var ed = doc.Editor;

            ed.WriteMessage("\n=======================================================");
            ed.WriteMessage("\n  [CTC] TRÍCH XUẤT ĐƯỜNG RANH GIỚI CORRIDOR (BOUNDARIES)");
            ed.WriteMessage("\n=======================================================\n");

            using (var form = new MyFirstProject.Civil_Tool_2.TrichXuatRanhGioiCorridorForm(db, ed))
            {
                var dialogResult = Application.ShowModalDialog(form);

                if (dialogResult != DialogResult.OK || !form.FormAccepted)
                {
                    ed.WriteMessage("\n[Hủy bỏ] Thao tác trích xuất ranh giới đã được hủy.");
                    return;
                }

                if (form.SelectedRegions.Count == 0)
                {
                    ed.WriteMessage("\n⚠️ Không có Region nào được chọn để trích xuất.");
                    return;
                }

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    try
                    {
                        var bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                        if (bt == null) return;
                        var btr = tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite) as BlockTableRecord;
                        if (btr == null) return;

                        // Đảm bảo Layer tồn tại
                        EnsureLayer(db, tr, form.TargetLayerName, form.SelectedColorIndex);

                        int mode = form.ExtractionMode;
                        bool is2D = form.Is2D;
                        bool isClosed = form.IsClosed;
                        string layerName = form.TargetLayerName;
                        short colorIdx = form.SelectedColorIndex;

                        ed.WriteMessage($"\n🔄 Bắt đầu xử lý {form.SelectedRegions.Count} Region...");
                        ed.WriteMessage($"\n  • Chế độ xuất: {(mode == 0 ? "Ranh giới từng Region (Region Boundaries)" : mode == 1 ? "Hợp nhất ranh giới ngoài tổng thể (Corridor Outer Boundary)" : "Xuất 2 đường mép ngoài (Left & Right Lines)")}");
                        ed.WriteMessage($"\n  • Định dạng:   {(is2D ? "2D Polyline (Z = 0)" : "3D Polyline (Z thực tế)")}");
                        ed.WriteMessage($"\n  • Layer:       '{layerName}' (Màu ACI: {colorIdx})");

                        // Gom nhóm các Region theo Corridor và Baseline
                        var groupedRegions = form.SelectedRegions
                            .GroupBy(r => new { r.CorridorId, r.BaselineIndex })
                            .ToList();

                        int totalEntitiesCreated = 0;
                        int totalHatchCreated = 0;
                        int totalLabelsCreated = 0;
                        double totalCalculatedArea = 0.0;

                        List<Polyline> created2DPolylinesForUnion = new List<Polyline>();

                        foreach (var group in groupedRegions)
                        {
                            var corridor = tr.GetObject(group.Key.CorridorId, OpenMode.ForRead) as Corridor;
                            if (corridor == null) continue;

                            if (group.Key.BaselineIndex < 0 || group.Key.BaselineIndex >= corridor.Baselines.Count) continue;
                            var baseline = corridor.Baselines[group.Key.BaselineIndex];

                            Alignment? alignment = null;
                            if (!baseline.IsFeatureLineBased() && !baseline.AlignmentId.IsNull && baseline.AlignmentId.IsValid)
                            {
                                alignment = tr.GetObject(baseline.AlignmentId, OpenMode.ForRead) as Alignment;
                            }

                            foreach (var regItem in group)
                            {
                                if (regItem.RegionIndex < 0 || regItem.RegionIndex >= baseline.BaselineRegions.Count) continue;
                                var baselineRegion = baseline.BaselineRegions[regItem.RegionIndex];

                                // Trích xuất các điểm mép trái và mép phải của Region
                                var boundaryPoints = ExtractRegionBoundaryPoints(baselineRegion, alignment);
                                if (boundaryPoints == null || boundaryPoints.LeftPoints.Count < 2 || boundaryPoints.RightPoints.Count < 2)
                                {
                                    ed.WriteMessage($"\n  ⚠️ Bỏ qua Region '{regItem.RegionName}': Không đủ điểm mặt cắt.");
                                    continue;
                                }

                                if (mode == 2)
                                {
                                    // ── CHẾ ĐỘ 2: XUẤT 2 ĐƯỜNG MÉP NGOÀI (LEFT & RIGHT LINES) ──
                                    ObjectId leftId = CreatePolylineEntity(btr, tr, boundaryPoints.LeftPoints, is2D, false, layerName, colorIdx);
                                    ObjectId rightId = CreatePolylineEntity(btr, tr, boundaryPoints.RightPoints, is2D, false, layerName, colorIdx);

                                    if (leftId.IsValid) totalEntitiesCreated++;
                                    if (rightId.IsValid) totalEntitiesCreated++;

                                    ed.WriteMessage($"\n  ✅ Region '{regItem.RegionName}': Đã xuất 2 đường mép ({boundaryPoints.LeftPoints.Count} đỉnh mỗi bên).");
                                }
                                else
                                {
                                    // ── TẠO DANH SÁCH ĐIỂM CHU VI KHÉP KÍN CỦA REGION ──
                                    // Mép phải từ đầu đến cuối -> Mép trái từ cuối quay về đầu
                                    var perimeterPoints = new List<Point3d>();

                                    for (int i = 0; i < boundaryPoints.RightPoints.Count; i++)
                                    {
                                        perimeterPoints.Add(boundaryPoints.RightPoints[i]);
                                    }

                                    for (int i = boundaryPoints.LeftPoints.Count - 1; i >= 0; i--)
                                    {
                                        perimeterPoints.Add(boundaryPoints.LeftPoints[i]);
                                    }

                                    if (mode == 0)
                                    {
                                        // ── CHẾ ĐỘ 0: XUẤT TỪNG REGION BOUNDARY ──
                                        ObjectId polyId = CreatePolylineEntity(btr, tr, perimeterPoints, is2D, isClosed, layerName, colorIdx);
                                        if (polyId.IsValid)
                                        {
                                            totalEntitiesCreated++;

                                            double area = 0.0;
                                            var ent = tr.GetObject(polyId, OpenMode.ForRead) as CadEntity;
                                            if (ent is Polyline pl2d)
                                            {
                                                try { area = pl2d.Area; } catch { }
                                            }

                                            totalCalculatedArea += area;

                                            // Tạo Hatch nếu được chọn
                                            if (form.CreateHatch && is2D && isClosed)
                                            {
                                                ObjectId hatchId = CreateHatchForPolyline(btr, tr, polyId, form.HatchPattern, form.HatchScale, layerName, colorIdx);
                                                if (hatchId.IsValid) totalHatchCreated++;
                                            }

                                            // Tạo MText nhãn nếu được chọn
                                            if (form.CreateAnnotation && ent != null)
                                            {
                                                ObjectId textId = CreateAnnotationText(btr, tr, ent, regItem.RegionName, area, form.TextHeight, layerName, colorIdx);
                                                if (textId.IsValid) totalLabelsCreated++;
                                            }

                                            ed.WriteMessage($"\n  ✅ Region '{regItem.RegionName}': Đã xuất ranh giới ({perimeterPoints.Count} đỉnh, S = {area:N2} m²).");
                                        }
                                    }
                                    else if (mode == 1)
                                    {
                                        // ── CHẾ ĐỘ 1: THU THẬP ĐỂ HỢP NHẤT TOÀN TUYẾN ──
                                        Polyline tempPoly = Build2DPolyline(perimeterPoints, true, layerName, colorIdx);
                                        created2DPolylinesForUnion.Add(tempPoly);
                                    }
                                }
                            }
                        }

                        // ── XỬ LÝ HỢP NHẤT (MODE 1: OUTER BOUNDARY UNION) ──
                        if (mode == 1 && created2DPolylinesForUnion.Count > 0)
                        {
                            ed.WriteMessage($"\n🔄 Đang thực hiện hợp nhất (Boolean Union) {created2DPolylinesForUnion.Count} vùng Region...");

                            bool unionSuccess = false;
                            List<AcRegion> acRegions = new List<AcRegion>();

                            foreach (var pl in created2DPolylinesForUnion)
                            {
                                var curves = new DBObjectCollection { pl };
                                var regions = AcRegion.CreateFromCurves(curves);
                                foreach (DBObject obj in regions)
                                {
                                    if (obj is AcRegion reg)
                                    {
                                        acRegions.Add(reg);
                                    }
                                }
                            }

                            if (acRegions.Count > 0)
                            {
                                AcRegion unionReg = acRegions[0];
                                for (int i = 1; i < acRegions.Count; i++)
                                {
                                    try
                                    {
                                        unionReg.BooleanOperation(BooleanOperationType.BoolUnite, acRegions[i]);
                                    }
                                    catch
                                    {
                                        // Nếu không giao hoặc lỗi, tiếp tục
                                    }
                                }

                                var explodedCurves = new DBObjectCollection();
                                unionReg.Explode(explodedCurves);

                                if (explodedCurves.Count > 0)
                                {
                                    unionSuccess = true;
                                    double unionArea = 0.0;
                                    try { unionArea = unionReg.Area; } catch { }
                                    totalCalculatedArea = unionArea;

                                    foreach (DBObject obj in explodedCurves)
                                    {
                                        if (obj is CadEntity expEnt)
                                        {
                                            expEnt.Layer = layerName;
                                            if (colorIdx == 256) expEnt.ColorIndex = 256;
                                            else expEnt.Color = CadColor.FromColorIndex(ColorMethod.ByAci, colorIdx);

                                            btr.AppendEntity(expEnt);
                                            tr.AddNewlyCreatedDBObject(expEnt, true);
                                            totalEntitiesCreated++;

                                            // Tạo Hatch cho đối tượng hợp nhất nếu là Polyline kín
                                            if (form.CreateHatch && expEnt is Polyline expPl && expPl.Closed)
                                            {
                                                ObjectId hId = CreateHatchForPolyline(btr, tr, expEnt.ObjectId, form.HatchPattern, form.HatchScale, layerName, colorIdx);
                                                if (hId.IsValid) totalHatchCreated++;
                                            }
                                        }
                                    }

                                    // Tạo nhãn tổng thể
                                    if (form.CreateAnnotation && explodedCurves[0] is CadEntity firstEnt)
                                    {
                                        CreateAnnotationText(btr, tr, firstEnt, "Corridor Boundary", unionArea, form.TextHeight, layerName, colorIdx);
                                        totalLabelsCreated++;
                                    }

                                    ed.WriteMessage($"\n  ✅ Hợp nhất thành công: Tạo được {explodedCurves.Count} đối tượng đường bao ngoài (S = {unionArea:N2} m²).");
                                }

                                // Dọn dẹp Region tạm
                                foreach (var reg in acRegions)
                                {
                                    reg.Dispose();
                                }
                            }

                            // Fallback nếu Boolean Union không thành công (ví dụ Region bị hở)
                            if (!unionSuccess)
                            {
                                ed.WriteMessage("\n  ⚠️ Các Region không tiếp giáp hoặc có khe hở, tự động chuyển sang vẽ từng đường ranh riêng biệt.");
                                foreach (var pl in created2DPolylinesForUnion)
                                {
                                    btr.AppendEntity(pl);
                                    tr.AddNewlyCreatedDBObject(pl, true);
                                    totalEntitiesCreated++;
                                }
                            }
                            else
                            {
                                // Hợp nhất thành công thì giải phóng các polyline tạm
                                foreach (var pl in created2DPolylinesForUnion)
                                {
                                    pl.Dispose();
                                }
                            }
                        }

                        tr.Commit();

                        ed.WriteMessage("\n-------------------------------------------------------");
                        ed.WriteMessage($"\n🎉 HOÀN TẤT TRÍCH XUẤT:");
                        ed.WriteMessage($"\n  • Tổng số đối tượng ranh giới đã tạo: {totalEntitiesCreated}");
                        if (totalHatchCreated > 0) ed.WriteMessage($"\n  • Tổng số Hatch đã tạo:                {totalHatchCreated}");
                        if (totalLabelsCreated > 0) ed.WriteMessage($"\n  • Tổng số Nhãn MText đã tạo:           {totalLabelsCreated}");
                        if (totalCalculatedArea > 0) ed.WriteMessage($"\n  • Tổng diện tích ranh giới:            {totalCalculatedArea:N2} m²");
                        ed.WriteMessage("\n=======================================================\n");
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception aEx)
                    {
                        ed.WriteMessage($"\n❌ Lỗi AutoCAD: {aEx.Message}");
                        tr.Abort();
                    }
                    catch (Exception ex)
                    {
                        ed.WriteMessage($"\n❌ Lỗi hệ thống: {ex.Message}");
                        tr.Abort();
                    }
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  HELPER: TRÍCH XUẤT ĐIỂM MÉP CỦA BASELINE REGION
        // ═══════════════════════════════════════════════════════════════
        public class ExtractedRegionBoundary
        {
            public List<Point3d> LeftPoints { get; set; } = new List<Point3d>();
            public List<Point3d> RightPoints { get; set; } = new List<Point3d>();
        }

        private static ExtractedRegionBoundary? ExtractRegionBoundaryPoints(BaselineRegion region, Alignment? alignment)
        {
            var appliedAssemblies = region.AppliedAssemblies;
            int count = appliedAssemblies.Count;
            if (count < 2) return null;

            var result = new ExtractedRegionBoundary();

            for (int i = 0; i < count; i++)
            {
                var aa = appliedAssemblies[i];
                var subs = aa.GetAppliedSubassemblies();

                Point3d? minPt = null;
                Point3d? maxPt = null;
                double minOffset = double.MaxValue;
                double maxOffset = double.MinValue;

                foreach (AppliedSubassembly sub in subs)
                {
                    var pts = sub.Points;
                    if (pts == null || pts.Count == 0) continue;

                    foreach (CalculatedPoint cp in pts)
                    {
                        Point3d wPt = cp.XYZ;
                        double off = 0.0;

                        try
                        {
                            // Điểm tọa độ tương đối với tim tuyến (X = Station, Y = Offset, Z = Elev)
                            Point3d bPt = cp.StationOffsetElevationToBaseline;
                            off = bPt.Y;
                        }
                        catch
                        {
                            if (alignment != null)
                            {
                                double st = 0.0;
                                alignment.StationOffset(wPt.X, wPt.Y, ref st, ref off);
                            }
                            else
                            {
                                off = 0.0;
                            }
                        }

                        if (off < minOffset)
                        {
                            minOffset = off;
                            minPt = wPt;
                        }
                        if (off > maxOffset)
                        {
                            maxOffset = off;
                            maxPt = wPt;
                        }
                    }
                }

                if (minPt.HasValue && maxPt.HasValue)
                {
                    // Lọc bớt các điểm trùng lặp liên tiếp sát nhau (< 1mm)
                    if (result.LeftPoints.Count == 0 || result.LeftPoints.Last().DistanceTo(minPt.Value) > 0.001)
                    {
                        result.LeftPoints.Add(minPt.Value);
                    }

                    if (result.RightPoints.Count == 0 || result.RightPoints.Last().DistanceTo(maxPt.Value) > 0.001)
                    {
                        result.RightPoints.Add(maxPt.Value);
                    }
                }
            }

            return result;
        }

        // ═══════════════════════════════════════════════════════════════
        //  HELPER: TẠO ĐỐI TƯỢNG POLYLINE (2D HOẶC 3D)
        // ═══════════════════════════════════════════════════════════════
        private static ObjectId CreatePolylineEntity(BlockTableRecord btr, Transaction tr, List<Point3d> points, bool is2D, bool isClosed, string layerName, short colorIdx)
        {
            if (points == null || points.Count < 2) return ObjectId.Null;

            CadEntity ent;

            if (is2D)
            {
                ent = Build2DPolyline(points, isClosed, layerName, colorIdx);
            }
            else
            {
                var ptCol = new Point3dCollection();
                foreach (var pt in points)
                {
                    ptCol.Add(pt);
                }

                var pl3d = new Polyline3d(Poly3dType.SimplePoly, ptCol, isClosed);
                pl3d.Layer = layerName;
                if (colorIdx == 256) pl3d.ColorIndex = 256;
                else pl3d.Color = CadColor.FromColorIndex(ColorMethod.ByAci, colorIdx);
                ent = pl3d;
            }

            ObjectId id = btr.AppendEntity(ent);
            tr.AddNewlyCreatedDBObject(ent, true);
            return id;
        }

        private static Polyline Build2DPolyline(List<Point3d> points, bool isClosed, string layerName, short colorIdx)
        {
            var pl2d = new Polyline();
            for (int i = 0; i < points.Count; i++)
            {
                pl2d.AddVertexAt(i, new Point2d(points[i].X, points[i].Y), 0, 0, 0);
            }
            pl2d.Closed = isClosed;
            pl2d.Layer = layerName;
            if (colorIdx == 256) pl2d.ColorIndex = 256;
            else pl2d.Color = CadColor.FromColorIndex(ColorMethod.ByAci, colorIdx);

            return pl2d;
        }

        // ═══════════════════════════════════════════════════════════════
        //  HELPER: TẠO HATCH CHO POLYLINE
        // ═══════════════════════════════════════════════════════════════
        private static ObjectId CreateHatchForPolyline(BlockTableRecord btr, Transaction tr, ObjectId polyId, string patternName, double scale, string layerName, short colorIdx)
        {
            try
            {
                using (var hatch = new Hatch())
                {
                    hatch.SetDatabaseDefaults();
                    hatch.Layer = layerName;
                    if (colorIdx == 256) hatch.ColorIndex = 256;
                    else hatch.Color = CadColor.FromColorIndex(ColorMethod.ByAci, colorIdx);

                    hatch.SetHatchPattern(HatchPatternType.PreDefined, string.IsNullOrEmpty(patternName) ? "ANSI31" : patternName);
                    hatch.PatternScale = scale > 0 ? scale : 1.0;

                    ObjectId hatchId = btr.AppendEntity(hatch);
                    tr.AddNewlyCreatedDBObject(hatch, true);

                    var loopIds = new ObjectIdCollection { polyId };
                    hatch.AppendLoop(HatchLoopTypes.Default, loopIds);
                    hatch.EvaluateHatch(true);

                    return hatchId;
                }
            }
            catch
            {
                return ObjectId.Null;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  HELPER: TẠO NHÃN MTEXT THÔNG TIN REGION
        // ═══════════════════════════════════════════════════════════════
        private static ObjectId CreateAnnotationText(BlockTableRecord btr, Transaction tr, CadEntity boundaryEnt, string regionName, double area, double textHeight, string layerName, short colorIdx)
        {
            try
            {
                Extents3d ext = boundaryEnt.GeometricExtents;
                Point3d center = new Point3d(
                    (ext.MinPoint.X + ext.MaxPoint.X) / 2.0,
                    (ext.MinPoint.Y + ext.MaxPoint.Y) / 2.0,
                    0
                );

                using (var mtext = new MText())
                {
                    mtext.SetDatabaseDefaults();
                    mtext.Layer = layerName;
                    if (colorIdx == 256) mtext.ColorIndex = 256;
                    else mtext.Color = CadColor.FromColorIndex(ColorMethod.ByAci, colorIdx);

                    mtext.TextHeight = textHeight > 0 ? textHeight : 2.0;
                    mtext.Attachment = AttachmentPoint.MiddleCenter;
                    mtext.Location = center;

                    string content = $"{regionName}";
                    if (area > 0)
                    {
                        content += $"\\P\\C2;S = {area:N2} m²";
                    }
                    mtext.Contents = content;

                    ObjectId id = btr.AppendEntity(mtext);
                    tr.AddNewlyCreatedDBObject(mtext, true);
                    return id;
                }
            }
            catch
            {
                return ObjectId.Null;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  HELPER: ĐẢM BẢO LAYER TỒN TẠI
        // ═══════════════════════════════════════════════════════════════
        public static void EnsureLayer(Database db, Transaction tr, string layerName, short colorIndex)
        {
            if (string.IsNullOrWhiteSpace(layerName)) layerName = "C-CORR-BOUNDARY";
            var lt = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
            if (lt != null && !lt.Has(layerName))
            {
                lt.UpgradeOpen();
                var ltr = new LayerTableRecord { Name = layerName };
                if (colorIndex > 0 && colorIndex <= 255)
                {
                    ltr.Color = CadColor.FromColorIndex(ColorMethod.ByAci, colorIndex);
                }
                lt.Add(ltr);
                tr.AddNewlyCreatedDBObject(ltr, true);
            }
        }
    }
}

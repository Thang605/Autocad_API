// (C) Copyright 2015 by  
//
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Linq;
using System.Drawing;
using System.IO;

using Autodesk.AutoCAD.Runtime;
using Acad = Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using MyFirstProject.Extensions;
using WinFormsLabel = System.Windows.Forms.Label;
using AcadColor = Autodesk.AutoCAD.Colors.Color;
using AcadRegion = Autodesk.AutoCAD.DatabaseServices.Region;

// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3DCsharp.AT_TextToSolid_Commands))]

namespace Civil3DCsharp
{
    public class AT_TextToSolid_Commands
    {
        // Thông tin lưu trữ mỗi Text gốc được chọn trước khi TXTEXP
        public class TextSourceInfo
        {
            public ObjectId Id { get; set; }
            public Extents3d Extents { get; set; }
            public Point3d Center { get; set; }
            public string Layer { get; set; } = "0";
            public AcadColor Color { get; set; } = AcadColor.FromColorIndex(ColorMethod.ByAci, 7);
            public double TextHeight { get; set; } = 1.0;
        }

        // Biến lưu trữ kết quả TXTEXP và cấu hình
        private static HashSet<long> _beforeHandles = new HashSet<long>();
        private static List<TextSourceInfo> _textSourceInfos = new List<TextSourceInfo>();
        private static bool _useCustomColor = false;
        private static AcadColor? _solidColor = null;
        private static bool _create3DSolid = false;
        private static double _extrusionHeight = 1.0;

        /// <summary>
        /// Lệnh chính: Chuyển Text (DBText/MText) thành Solid Hatch hoặc 3D Solid
        /// Quy trình 2 bước: Step 1 chạy TXTEXP, Step 2 tạo hatch/solid cho từng Text
        /// </summary>
        [CommandMethod("AT_TextToSolid")]
        public static void AT_TextToSolid()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            ed.WriteMessage("\n═══════════════════════════════════════════════════════════════");
            ed.WriteMessage("\n  AT_TextToSolid - Chuyển Text thành Solid Hatch / 3D Solid");
            ed.WriteMessage("\n═══════════════════════════════════════════════════════════════");

            // Chọn các đối tượng Text
            List<ObjectId> textIds = SelectTexts(ed);
            if (textIds.Count == 0)
            {
                ed.WriteMessage("\nKhông có text nào được chọn. Hủy lệnh.");
                return;
            }

            // Hiển thị form để cấu hình
            using (var form = new TextToSolidForm())
            {
                form.SelectedTextCount = textIds.Count;

                if (form.ShowDialog() != DialogResult.OK)
                {
                    ed.WriteMessage("\nĐã hủy lệnh.");
                    return;
                }

                // Lưu cấu hình
                _useCustomColor = form.UseCustomColor;
                _solidColor = form.SolidColor;
                _create3DSolid = form.Create3DSolid;
                _extrusionHeight = form.ExtrusionHeight;
                _textSourceInfos.Clear();

                // Lấy thông tin TẤT CẢ text gốc TRƯỚC KHI chạy TXTEXP
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId textId in textIds)
                    {
                        try
                        {
                            Entity textEnt = (Entity)tr.GetObject(textId, OpenMode.ForRead);
                            var info = new TextSourceInfo
                            {
                                Id = textId,
                                Layer = textEnt.Layer,
                                Color = textEnt.Color,
                                Extents = textEnt.GeometricExtents
                            };

                            info.Center = new Point3d(
                                (info.Extents.MinPoint.X + info.Extents.MaxPoint.X) / 2.0,
                                (info.Extents.MinPoint.Y + info.Extents.MaxPoint.Y) / 2.0,
                                (info.Extents.MinPoint.Z + info.Extents.MaxPoint.Z) / 2.0
                            );

                            if (textEnt is DBText dbText)
                            {
                                info.TextHeight = dbText.Height;
                            }
                            else if (textEnt is MText mText)
                            {
                                info.TextHeight = mText.TextHeight;
                            }

                            _textSourceInfos.Add(info);
                        }
                        catch { }
                    }

                    tr.Commit();
                }

                if (_textSourceInfos.Count == 0)
                {
                    ed.WriteMessage("\nKhông lấy được thông tin text. Hủy lệnh.");
                    return;
                }

                // Lấy handles trước khi chạy TXTEXP
                _beforeHandles = GetAllHandles(db);

                ed.WriteMessage($"\n\n→ Đang chạy TXTEXP cho {_textSourceInfos.Count} đối tượng Text...");

                // Chọn tất cả text cần xử lý
                ed.SetImpliedSelection(textIds.ToArray());

                // Chạy TXTEXP rồi tự động gọi Step2
                doc.SendStringToExecute("_.TXTEXP\n_AT_TextToSolid_Step2\n", true, false, false);
            }
        }

        /// <summary>
        /// Bước 2: Tạo hatch / 3D solid từ polylines đã được tạo bởi TXTEXP
        /// Xử lý độc lập theo từng nhóm Text để hỗ trợ chọn nhiều Text cùng lúc
        /// </summary>
        [CommandMethod("AT_TextToSolid_Step2")]
        public static void AT_TextToSolid_Step2()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            ed.WriteMessage("\n\n→ Đang xử lý các đối tượng sinh ra từ TXTEXP...");

            if (_beforeHandles.Count == 0 || _textSourceInfos.Count == 0)
            {
                ed.WriteMessage("\n✗ Lỗi: Không có dữ liệu từ Bước 1. Vui lòng chạy lại AT_TextToSolid.");
                ResetState();
                return;
            }

            // Tìm các objects mới được tạo bởi TXTEXP
            List<ObjectId> newCurveIds = new List<ObjectId>();

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                foreach (ObjectId objId in btr)
                {
                    if (!_beforeHandles.Contains(objId.Handle.Value))
                    {
                        try
                        {
                            Entity ent = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                            if (ent != null && !ent.IsErased)
                            {
                                if (ent is Polyline || ent is Polyline2d || ent is Line ||
                                    ent is Arc || ent is Spline || ent is Circle || ent is Ellipse)
                                {
                                    newCurveIds.Add(objId);
                                }
                            }
                        }
                        catch { }
                    }
                }
                tr.Commit();
            }

            ed.WriteMessage($"\n  Tìm thấy {newCurveIds.Count} đường nét từ TXTEXP.");

            if (newCurveIds.Count == 0)
            {
                ed.WriteMessage("\n✗ Không tìm thấy polylines. TXTEXP có thể đã thất bại hoặc không có đối tượng nào được explode.");
                ResetState();
                return;
            }

            // Nhóm các curves theo từng Text ban đầu dựa vào vị trí hình học
            Dictionary<TextSourceInfo, List<ObjectId>> textToCurvesMap = new Dictionary<TextSourceInfo, List<ObjectId>>();
            foreach (var textInfo in _textSourceInfos)
            {
                textToCurvesMap[textInfo] = new List<ObjectId>();
            }

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                foreach (ObjectId curveId in newCurveIds)
                {
                    try
                    {
                        Entity ent = (Entity)tr.GetObject(curveId, OpenMode.ForRead);
                        Extents3d curveExt = ent.GeometricExtents;
                        Point3d curveCenter = new Point3d(
                            (curveExt.MinPoint.X + curveExt.MaxPoint.X) / 2.0,
                            (curveExt.MinPoint.Y + curveExt.MaxPoint.Y) / 2.0,
                            (curveExt.MinPoint.Z + curveExt.MaxPoint.Z) / 2.0
                        );

                        // Tìm text info phù hợp nhất
                        TextSourceInfo bestMatch = null;
                        double minDistance = double.MaxValue;

                        foreach (var textInfo in _textSourceInfos)
                        {
                            // Mở rộng bounding box của text 30% để bao trọn curve
                            double marginX = Math.Max(0.5, (textInfo.Extents.MaxPoint.X - textInfo.Extents.MinPoint.X) * 0.3);
                            double marginY = Math.Max(0.5, (textInfo.Extents.MaxPoint.Y - textInfo.Extents.MinPoint.Y) * 0.3);

                            bool isInside = (curveCenter.X >= textInfo.Extents.MinPoint.X - marginX &&
                                             curveCenter.X <= textInfo.Extents.MaxPoint.X + marginX &&
                                             curveCenter.Y >= textInfo.Extents.MinPoint.Y - marginY &&
                                             curveCenter.Y <= textInfo.Extents.MaxPoint.Y + marginY);

                            double dist = curveCenter.DistanceTo(textInfo.Center);

                            if (isInside)
                            {
                                if (bestMatch == null || dist < minDistance)
                                {
                                    bestMatch = textInfo;
                                    minDistance = dist;
                                }
                            }
                            else if (bestMatch == null && dist < minDistance)
                            {
                                minDistance = dist;
                            }
                        }

                        // Nếu không nằm trọn trong bbox nào, gán cho text gần nhất
                        if (bestMatch == null)
                        {
                            bestMatch = _textSourceInfos.OrderBy(t => curveCenter.DistanceTo(t.Center)).FirstOrDefault();
                        }

                        if (bestMatch != null)
                        {
                            textToCurvesMap[bestMatch].Add(curveId);
                        }
                    }
                    catch { }
                }
                tr.Commit();
            }

            int totalCreatedCount = 0;
            int processedTextCount = 0;

            // Xử lý tạo Hatch / 3D Solid cho từng nhóm Text
            foreach (var kvp in textToCurvesMap)
            {
                TextSourceInfo textInfo = kvp.Key;
                List<ObjectId> curveIds = kvp.Value;

                if (curveIds.Count == 0)
                    continue;

                bool success = false;
                int count = 0;

                if (_create3DSolid)
                {
                    success = Create3DSolidFromCurves(curveIds, db, ed, textInfo.Layer,
                        _useCustomColor, _solidColor, textInfo.Color, _extrusionHeight, out count);
                }
                else
                {
                    success = CreateHatchFromCurves(curveIds, db, ed, textInfo.Layer,
                        _useCustomColor, _solidColor, textInfo.Color, out count);
                }

                if (success)
                {
                    totalCreatedCount += count;
                    processedTextCount++;
                }
            }

            // Xóa các curves tạm do TXTEXP sinh ra sau khi đã tạo Hatch/Solid
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                foreach (ObjectId id in newCurveIds)
                {
                    try
                    {
                        Entity ent = (Entity)tr.GetObject(id, OpenMode.ForWrite);
                        if (ent != null && !ent.IsErased)
                            ent.Erase();
                    }
                    catch { }
                }
                tr.Commit();
            }

            if (_create3DSolid)
            {
                ed.WriteMessage($"\n\n✓ Hoàn tất: Đã tạo {totalCreatedCount} 3D Solid từ {processedTextCount}/{_textSourceInfos.Count} đối tượng Text (chiều cao: {_extrusionHeight})!");
            }
            else
            {
                ed.WriteMessage($"\n\n✓ Hoàn tất: Đã tạo {totalCreatedCount} Solid Hatch từ {processedTextCount}/{_textSourceInfos.Count} đối tượng Text!");
            }

            ResetState();
        }

        /// <summary>
        /// Xử lý chữ có lỗ rỗng (ví dụ: O, A, B, D, P, R, 0, 8, ...)
        /// Bằng cách trừ (BoolSubtract) các Region con nằm trong Region mẹ
        /// </summary>
        private static List<AcadRegion> ProcessRegionsWithHoles(DBObjectCollection rawRegions)
        {
            List<AcadRegion> regionList = new List<AcadRegion>();
            foreach (DBObject obj in rawRegions)
            {
                if (obj is AcadRegion r)
                    regionList.Add(r);
            }

            if (regionList.Count <= 1)
                return regionList;

            // Sắp xếp các region theo diện tích BoundingBox giảm dần (vùng mẹ lớn nhất đứng trước)
            regionList.Sort((a, b) =>
            {
                double areaA = GetRegionExtentsArea(a);
                double areaB = GetRegionExtentsArea(b);
                return areaB.CompareTo(areaA);
            });

            List<AcadRegion> finalRegions = new List<AcadRegion>();
            HashSet<AcadRegion> innerRegions = new HashSet<AcadRegion>();

            for (int i = 0; i < regionList.Count; i++)
            {
                AcadRegion outer = regionList[i];
                if (innerRegions.Contains(outer)) continue;

                Extents3d outerExt = outer.GeometricExtents;

                for (int j = i + 1; j < regionList.Count; j++)
                {
                    AcadRegion inner = regionList[j];
                    if (innerRegions.Contains(inner)) continue;

                    Extents3d innerExt = inner.GeometricExtents;

                    // Nếu inner nằm hoàn toàn bên trong outer
                    if (IsExtentsInside(innerExt, outerExt))
                    {
                        try
                        {
                            outer.BooleanOperation(BooleanOperationType.BoolSubtract, inner);
                            innerRegions.Add(inner);
                        }
                        catch
                        {
                            // Nếu lỗi boolean thì bỏ qua
                        }
                    }
                }

                finalRegions.Add(outer);
            }

            // Dispose các inner regions đã bị trừ vào outer
            foreach (AcadRegion inReg in innerRegions)
            {
                try { inReg.Dispose(); } catch { }
            }

            return finalRegions;
        }

        private static double GetRegionExtentsArea(AcadRegion r)
        {
            try
            {
                Extents3d ext = r.GeometricExtents;
                return Math.Abs((ext.MaxPoint.X - ext.MinPoint.X) * (ext.MaxPoint.Y - ext.MinPoint.Y));
            }
            catch
            {
                return 0;
            }
        }

        private static bool IsExtentsInside(Extents3d inner, Extents3d outer)
        {
            double tol = 1e-4;
            return inner.MinPoint.X >= outer.MinPoint.X - tol &&
                   inner.MaxPoint.X <= outer.MaxPoint.X + tol &&
                   inner.MinPoint.Y >= outer.MinPoint.Y - tol &&
                   inner.MaxPoint.Y <= outer.MaxPoint.Y + tol;
        }

        /// <summary>
        /// Tạo Hatch từ các curves (Line, Arc, Polyline...)
        /// </summary>
        private static bool CreateHatchFromCurves(List<ObjectId> curveIds, Database db, Editor ed,
            string layer, bool useCustomColor, AcadColor? solidColor, AcadColor originalColor, out int hatchCount)
        {
            hatchCount = 0;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    // Thu thập tất cả các curves
                    DBObjectCollection curves = new DBObjectCollection();
                    foreach (ObjectId id in curveIds)
                    {
                        try
                        {
                            Entity ent = (Entity)tr.GetObject(id, OpenMode.ForRead);
                            if (ent is Curve curve)
                            {
                                curves.Add(curve);
                            }
                        }
                        catch { }
                    }

                    if (curves.Count == 0)
                    {
                        tr.Abort();
                        return false;
                    }

                    // Thử tạo Region từ các curves
                    DBObjectCollection rawRegions = new DBObjectCollection();
                    try
                    {
                        rawRegions = AcadRegion.CreateFromCurves(curves);
                    }
                    catch { }

                    if (rawRegions.Count > 0)
                    {
                        // Xử lý trừ lỗ rỗng
                        List<AcadRegion> processedRegions = ProcessRegionsWithHoles(rawRegions);

                        foreach (AcadRegion region in processedRegions)
                        {
                            try
                            {
                                region.SetDatabaseDefaults();
                                ObjectId regionId = btr.AppendEntity(region);
                                tr.AddNewlyCreatedDBObject(region, true);

                                // Tạo Hatch
                                Hatch hatch = new Hatch();
                                hatch.SetDatabaseDefaults();
                                hatch.Layer = layer;

                                if (useCustomColor && solidColor != null)
                                    hatch.Color = solidColor;
                                else
                                    hatch.Color = originalColor;

                                ObjectId hatchId = btr.AppendEntity(hatch);
                                tr.AddNewlyCreatedDBObject(hatch, true);

                                hatch.PatternScale = 1.0;
                                hatch.SetHatchPattern(HatchPatternType.PreDefined, "SOLID");
                                hatch.Associative = false;

                                ObjectIdCollection boundaries = new ObjectIdCollection();
                                boundaries.Add(regionId);

                                hatch.AppendLoop(HatchLoopTypes.Default, boundaries);
                                hatch.EvaluateHatch(true);
                                hatchCount++;

                                // Xóa region sau khi tạo hatch
                                region.Erase();
                            }
                            catch
                            {
                                try { region.Erase(); } catch { }
                            }
                        }

                        tr.Commit();
                        return hatchCount > 0;
                    }
                    else
                    {
                        // Fallback: Tìm closed polylines/curves và tạo hatch trực tiếp
                        List<ObjectId> closedPolyIds = new List<ObjectId>();
                        foreach (ObjectId id in curveIds)
                        {
                            try
                            {
                                Entity ent = (Entity)tr.GetObject(id, OpenMode.ForRead);
                                if (ent is Curve curve && IsCurveClosed(curve))
                                {
                                    closedPolyIds.Add(id);
                                }
                            }
                            catch { }
                        }

                        if (closedPolyIds.Count > 0)
                        {
                            foreach (ObjectId polyId in closedPolyIds)
                            {
                                try
                                {
                                    Hatch hatch = new Hatch();
                                    hatch.SetDatabaseDefaults();
                                    hatch.Layer = layer;

                                    if (useCustomColor && solidColor != null)
                                        hatch.Color = solidColor;
                                    else
                                        hatch.Color = originalColor;

                                    ObjectId hatchId = btr.AppendEntity(hatch);
                                    tr.AddNewlyCreatedDBObject(hatch, true);

                                    hatch.PatternScale = 1.0;
                                    hatch.SetHatchPattern(HatchPatternType.PreDefined, "SOLID");
                                    hatch.Associative = false;

                                    ObjectIdCollection boundaries = new ObjectIdCollection();
                                    boundaries.Add(polyId);
                                    hatch.AppendLoop(HatchLoopTypes.Default, boundaries);
                                    hatch.EvaluateHatch(true);
                                    hatchCount++;
                                }
                                catch { }
                            }

                            tr.Commit();
                            return hatchCount > 0;
                        }

                        tr.Abort();
                        return false;
                    }
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage($"\n  Lỗi tạo hatch: {ex.Message}");
                    tr.Abort();
                    return false;
                }
            }
        }

        /// <summary>
        /// Tạo 3D Solid từ các curves bằng cách extruding Region
        /// </summary>
        private static bool Create3DSolidFromCurves(List<ObjectId> curveIds, Database db, Editor ed,
            string layer, bool useCustomColor, AcadColor? solidColor, AcadColor originalColor, double extrusionHeight, out int solidCount)
        {
            solidCount = 0;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    // Thu thập tất cả các curves
                    DBObjectCollection curves = new DBObjectCollection();
                    foreach (ObjectId id in curveIds)
                    {
                        try
                        {
                            Entity ent = (Entity)tr.GetObject(id, OpenMode.ForRead);
                            if (ent is Curve curve)
                            {
                                curves.Add(curve);
                            }
                        }
                        catch { }
                    }

                    if (curves.Count == 0)
                    {
                        tr.Abort();
                        return false;
                    }

                    // Tạo Region từ các curves
                    DBObjectCollection rawRegions = new DBObjectCollection();
                    try
                    {
                        rawRegions = AcadRegion.CreateFromCurves(curves);
                    }
                    catch { }

                    if (rawRegions.Count > 0)
                    {
                        // Xử lý trừ lỗ rỗng
                        List<AcadRegion> processedRegions = ProcessRegionsWithHoles(rawRegions);

                        foreach (AcadRegion region in processedRegions)
                        {
                            try
                            {
                                Solid3d solid = new Solid3d();
                                solid.SetDatabaseDefaults();
                                solid.Layer = layer;

                                if (useCustomColor && solidColor != null)
                                    solid.Color = solidColor;
                                else
                                    solid.Color = originalColor;

                                // Extrude region theo chiều cao
                                solid.Extrude(region, extrusionHeight, 0.0);

                                btr.AppendEntity(solid);
                                tr.AddNewlyCreatedDBObject(solid, true);

                                solidCount++;
                            }
                            catch (System.Exception ex)
                            {
                                ed.WriteMessage($"\n  Lỗi extrude: {ex.Message}");
                            }
                            finally
                            {
                                try { region.Dispose(); } catch { }
                            }
                        }

                        tr.Commit();
                        return solidCount > 0;
                    }
                    else
                    {
                        // Fallback: Tìm closed polylines
                        foreach (ObjectId id in curveIds)
                        {
                            try
                            {
                                Entity ent = (Entity)tr.GetObject(id, OpenMode.ForRead);
                                Curve closedCurve = null;

                                if (ent is Polyline pline && pline.Closed)
                                    closedCurve = pline;
                                else if (ent is Circle || ent is Ellipse)
                                    closedCurve = ent as Curve;

                                if (closedCurve != null)
                                {
                                    DBObjectCollection singleCurve = new DBObjectCollection();
                                    singleCurve.Add(closedCurve);

                                    DBObjectCollection singleRegion = AcadRegion.CreateFromCurves(singleCurve);

                                    if (singleRegion.Count > 0 && singleRegion[0] is AcadRegion reg)
                                    {
                                        Solid3d solid = new Solid3d();
                                        solid.SetDatabaseDefaults();
                                        solid.Layer = layer;

                                        if (useCustomColor && solidColor != null)
                                            solid.Color = solidColor;
                                        else
                                            solid.Color = originalColor;

                                        solid.Extrude(reg, extrusionHeight, 0.0);

                                        btr.AppendEntity(solid);
                                        tr.AddNewlyCreatedDBObject(solid, true);

                                        reg.Dispose();
                                        solidCount++;
                                    }
                                }
                            }
                            catch { }
                        }

                        if (solidCount > 0)
                        {
                            tr.Commit();
                            return true;
                        }

                        tr.Abort();
                        return false;
                    }
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage($"\n  Lỗi tạo 3D Solid: {ex.Message}");
                    tr.Abort();
                    return false;
                }
            }
        }

        #region ================= AT_PolysToSolid High-Performance Engine =================

        /// <summary>
        /// Thông tin bao đóng của một Curve/Polyline phục vụ xử lý Solid/Hatch hàng loạt
        /// </summary>
        private class PolyBoundaryNode
        {
            public ObjectId Id { get; set; }
            public string Layer { get; set; } = "0";
            public AcadColor Color { get; set; } = AcadColor.FromColorIndex(ColorMethod.ByAci, 7);
            public Extents3d Extents { get; set; }
            public double Area { get; set; }
            public Point2d SamplePoint { get; set; }
            public List<Point2d> Polygon { get; set; } = new List<Point2d>();
            public Vector3d Normal { get; set; } = Vector3d.ZAxis;
            public double Elevation { get; set; } = 0.0;
            public int Depth { get; set; } = 0;
            public PolyBoundaryNode? Parent { get; set; }
            public List<PolyBoundaryNode> Holes { get; } = new List<PolyBoundaryNode>();
        }

        /// <summary>
        /// Lấy mẫu tọa độ 2D của đường cong để phục vụ thuật toán Point-in-Polygon
        /// </summary>
        private static List<Point2d> SampleCurveToPolygon(Curve curve)
        {
            List<Point2d> pts = new List<Point2d>();
            if (curve == null) return pts;

            try
            {
                if (curve is Polyline pline)
                {
                    int n = pline.NumberOfVertices;
                    for (int i = 0; i < n; i++)
                    {
                        Point2d p0 = pline.GetPoint2dAt(i);
                        pts.Add(p0);

                        double bulge = pline.GetBulgeAt(i);
                        if (Math.Abs(bulge) > 1e-4)
                        {
                            int nextIdx = (i + 1) % n;
                            double d = pline.GetDistanceAtParameter(i);
                            double dNext = (nextIdx == 0 && !pline.Closed) ? pline.Length : pline.GetDistanceAtParameter(i + 1);
                            double step = (dNext - d) / 5.0;
                            for (int s = 1; s <= 4; s++)
                            {
                                try
                                {
                                    Point3d mid3d = pline.GetPointAtDist(d + s * step);
                                    pts.Add(new Point2d(mid3d.X, mid3d.Y));
                                }
                                catch { }
                            }
                        }
                    }
                }
                else if (curve is Circle circle)
                {
                    Point3d center = circle.Center;
                    double r = circle.Radius;
                    int numSegments = 32;
                    for (int i = 0; i < numSegments; i++)
                    {
                        double angle = i * 2.0 * Math.PI / numSegments;
                        pts.Add(new Point2d(center.X + r * Math.Cos(angle), center.Y + r * Math.Sin(angle)));
                    }
                }
                else if (curve is Ellipse ellipse)
                {
                    double length = ellipse.GetDistanceAtParameter(ellipse.EndParam);
                    int numSegments = 32;
                    double step = length / numSegments;
                    for (int i = 0; i < numSegments; i++)
                    {
                        try
                        {
                            Point3d p = ellipse.GetPointAtDist(i * step);
                            pts.Add(new Point2d(p.X, p.Y));
                        }
                        catch { }
                    }
                }
                else
                {
                    double length = curve.GetDistanceAtParameter(curve.EndParam);
                    if (length > 1e-6)
                    {
                        int numSegments = Math.Min(64, Math.Max(16, (int)(length / 2.0)));
                        double step = length / numSegments;
                        for (int i = 0; i < numSegments; i++)
                        {
                            try
                            {
                                Point3d p = curve.GetPointAtDist(i * step);
                                pts.Add(new Point2d(p.X, p.Y));
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }

            return pts;
        }

        /// <summary>
        /// Tính diện tích hình 2D theo công thức Shoelace
        /// </summary>
        private static double CalculatePolygonArea(List<Point2d> polygon)
        {
            if (polygon == null || polygon.Count < 3) return 0.0;
            double area = 0.0;
            int count = polygon.Count;
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                area += (polygon[j].X + polygon[i].X) * (polygon[j].Y - polygon[i].Y);
            }
            return Math.Abs(area * 0.5);
        }

        /// <summary>
        /// Thuật toán Ray-Casting kiểm tra 1 điểm có nằm trong đa giác 2D hay không
        /// </summary>
        private static bool IsPointInPolygon(Point2d pt, List<Point2d> polygon)
        {
            if (polygon == null || polygon.Count < 3) return false;
            bool inside = false;
            int count = polygon.Count;
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                if (((polygon[i].Y > pt.Y) != (polygon[j].Y > pt.Y)) &&
                    (pt.X < (polygon[j].X - polygon[i].X) * (pt.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) + polygon[i].X))
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        /// <summary>
        /// Kiểm tra hình inner có thực sự nằm lọt bên trong hình outer hay không
        /// </summary>
        private static bool IsNodeInside(PolyBoundaryNode inner, PolyBoundaryNode outer)
        {
            // 1. Lọc thô bằng Bounding Box
            if (!IsExtentsInside(inner.Extents, outer.Extents))
                return false;

            // 2. Lọc thô bằng diện tích
            if (inner.Area >= outer.Area)
                return false;

            // 3. Lọc tinh bằng thuật toán Point-in-Polygon
            if (outer.Polygon.Count < 3)
                return false;

            if (IsPointInPolygon(inner.SamplePoint, outer.Polygon))
                return true;

            if (inner.Polygon.Count > 0 && IsPointInPolygon(inner.Polygon[0], outer.Polygon))
                return true;

            return false;
        }

        /// <summary>
        /// Kiểm tra đường cong/polyline có khép kín hay không
        /// </summary>
        private static bool IsCurveClosed(Curve curve)
        {
            if (curve == null) return false;
            try
            {
                if (curve.Closed) return true;

                if (curve is Polyline pline)
                {
                    return pline.Closed || (pline.NumberOfVertices >= 3 && pline.StartPoint.DistanceTo(pline.EndPoint) < 1e-4);
                }
                if (curve is Polyline2d pline2d)
                {
                    return pline2d.Closed || pline2d.StartPoint.DistanceTo(pline2d.EndPoint) < 1e-4;
                }
                if (curve is Polyline3d pline3d)
                {
                    return pline3d.Closed || pline3d.StartPoint.DistanceTo(pline3d.EndPoint) < 1e-4;
                }
                if (curve is Circle) return true;
                if (curve is Ellipse ellipse) return ellipse.Closed;
                if (curve is Spline spline)
                {
                    return spline.Closed || spline.StartPoint.DistanceTo(spline.EndPoint) < 1e-4;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Xây dựng cây phân cấp Outer/Inner (Holes) chuẩn hình học Even-Odd kết hợp Spatial Grid Index
        /// </summary>
        private static List<PolyBoundaryNode> BuildBoundaryHierarchy(List<PolyBoundaryNode> closedNodes)
        {
            if (closedNodes == null || closedNodes.Count == 0)
                return new List<PolyBoundaryNode>();

            if (closedNodes.Count == 1)
            {
                closedNodes[0].Depth = 0;
                return new List<PolyBoundaryNode>(closedNodes);
            }

            // Sắp xếp các boundary theo diện tích giảm dần (lớn nhất trước)
            List<PolyBoundaryNode> sorted = closedNodes.OrderByDescending(n => n.Area).ToList();

            // Tính toán bounds toàn cục cho spatial grid
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;

            foreach (var node in sorted)
            {
                if (node.Extents.MinPoint.X < minX) minX = node.Extents.MinPoint.X;
                if (node.Extents.MinPoint.Y < minY) minY = node.Extents.MinPoint.Y;
                if (node.Extents.MaxPoint.X > maxX) maxX = node.Extents.MaxPoint.X;
                if (node.Extents.MaxPoint.Y > maxY) maxY = node.Extents.MaxPoint.Y;
            }

            double width = Math.Max(1.0, maxX - minX);
            double height = Math.Max(1.0, maxY - minY);
            int gridResolution = Math.Min(64, Math.Max(8, (int)Math.Sqrt(sorted.Count * 2)));
            double cellSizeX = width / gridResolution;
            double cellSizeY = height / gridResolution;

            Dictionary<int, List<PolyBoundaryNode>> grid = new Dictionary<int, List<PolyBoundaryNode>>();
            int GetCellKey(int cx, int cy) => (cx << 16) ^ cy;

            List<int> GetOverlappingCellKeys(Extents3d ext)
            {
                int minCx = Math.Max(0, Math.Min(gridResolution - 1, (int)((ext.MinPoint.X - minX) / cellSizeX)));
                int maxCx = Math.Max(0, Math.Min(gridResolution - 1, (int)((ext.MaxPoint.X - minX) / cellSizeX)));
                int minCy = Math.Max(0, Math.Min(gridResolution - 1, (int)((ext.MinPoint.Y - minY) / cellSizeY)));
                int maxCy = Math.Max(0, Math.Min(gridResolution - 1, (int)((ext.MaxPoint.Y - minY) / cellSizeY)));

                List<int> keys = new List<int>((maxCx - minCx + 1) * (maxCy - minCy + 1));
                for (int cx = minCx; cx <= maxCx; cx++)
                {
                    for (int cy = minCy; cy <= maxCy; cy++)
                    {
                        keys.Add(GetCellKey(cx, cy));
                    }
                }
                return keys;
            }

            void InsertIntoGrid(PolyBoundaryNode node)
            {
                var cellKeys = GetOverlappingCellKeys(node.Extents);
                foreach (int key in cellKeys)
                {
                    if (!grid.TryGetValue(key, out var list))
                    {
                        list = new List<PolyBoundaryNode>();
                        grid[key] = list;
                    }
                    list.Add(node);
                }
            }

            List<PolyBoundaryNode> outerRoots = new List<PolyBoundaryNode>();

            for (int i = 0; i < sorted.Count; i++)
            {
                PolyBoundaryNode current = sorted[i];
                var cellKeys = GetOverlappingCellKeys(current.Extents);

                // Tìm tập hợp ứng viên duy nhất từ các ô lưới đã duyệt
                HashSet<PolyBoundaryNode> candidates = new HashSet<PolyBoundaryNode>();
                foreach (int key in cellKeys)
                {
                    if (grid.TryGetValue(key, out var list))
                    {
                        foreach (var cand in list)
                        {
                            candidates.Add(cand);
                        }
                    }
                }

                // Tìm container trực tiếp nhỏ nhất (diện tích nhỏ nhất trong các container chứa current)
                PolyBoundaryNode? bestParent = null;
                double minParentArea = double.MaxValue;

                foreach (var cand in candidates)
                {
                    if (cand.Area > current.Area && cand.Area < minParentArea)
                    {
                        if (IsNodeInside(current, cand))
                        {
                            bestParent = cand;
                            minParentArea = cand.Area;
                        }
                    }
                }

                if (bestParent != null)
                {
                    current.Parent = bestParent;
                    current.Depth = bestParent.Depth + 1;

                    if (current.Depth % 2 == 1)
                    {
                        // Cấp lẻ = Lỗ rỗng của bestParent
                        bestParent.Holes.Add(current);
                    }
                    else
                    {
                        // Cấp chẵn = Đảo nổi độc lập (New Outer Root)
                        outerRoots.Add(current);
                    }
                }
                else
                {
                    // Không nằm trong đối tượng nào = Outer Root cấp 0
                    current.Depth = 0;
                    outerRoots.Add(current);
                }

                // Thêm current vào grid để làm ứng viên cho các đối tượng nhỏ hơn sau đó
                InsertIntoGrid(current);
            }

            return outerRoots;
        }

        /// <summary>
        /// Xử lý tạo Solid Hatch hàng loạt cho danh sách Curves/Polylines với hiệu năng cao và độ tin cậy tuyệt đối
        /// </summary>
        private static int ProcessPolylinesToHatchBatch(List<ObjectId> curveIds, Database db, Editor ed,
            bool useCustomColor, AcadColor? customColor, List<ObjectId> processedCurveIds)
        {
            int totalCreated = 0;
            List<PolyBoundaryNode> closedNodes = new List<PolyBoundaryNode>();
            List<ObjectId> unclosedCurveIds = new List<ObjectId>();

            // Bước 1: Quét và phân loại đối tượng, trích xuất hình học
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                foreach (ObjectId id in curveIds)
                {
                    try
                    {
                        Entity ent = (Entity)tr.GetObject(id, OpenMode.ForRead);
                        if (ent is Curve curve)
                        {
                            // Đảm bảo polyline khép kín nếu đỉnh đầu trùng đỉnh cuối
                            if (curve is Polyline pline)
                            {
                                if (!pline.Closed && pline.NumberOfVertices >= 3 && pline.StartPoint.DistanceTo(pline.EndPoint) < 1e-4)
                                {
                                    pline.UpgradeOpen();
                                    pline.Closed = true;
                                }
                            }

                            if (IsCurveClosed(curve))
                            {
                                Extents3d ext = curve.GeometricExtents;
                                List<Point2d> poly = SampleCurveToPolygon(curve);
                                double area = CalculatePolygonArea(poly);
                                if (area <= 1e-6)
                                {
                                    area = Math.Abs((ext.MaxPoint.X - ext.MinPoint.X) * (ext.MaxPoint.Y - ext.MinPoint.Y));
                                }

                                Point2d samplePt = poly.Count > 0 ? poly[0] : new Point2d(ext.MinPoint.X, ext.MinPoint.Y);
                                if (poly.Count >= 3)
                                {
                                    double cx = 0, cy = 0;
                                    foreach (var p in poly) { cx += p.X; cy += p.Y; }
                                    Point2d centroid = new Point2d(cx / poly.Count, cy / poly.Count);
                                    if (IsPointInPolygon(centroid, poly))
                                        samplePt = centroid;
                                }

                                Vector3d normal = Vector3d.ZAxis;
                                double elev = 0.0;
                                try
                                {
                                    if (curve is Polyline pl) { normal = pl.Normal; elev = pl.Elevation; }
                                    else if (curve is Polyline2d p2) { normal = p2.Normal; elev = p2.Elevation; }
                                    else if (curve is Circle c) { normal = c.Normal; }
                                }
                                catch { }

                                closedNodes.Add(new PolyBoundaryNode
                                {
                                    Id = id,
                                    Layer = ent.Layer,
                                    Color = ent.Color,
                                    Extents = ext,
                                    Area = area,
                                    Polygon = poly,
                                    SamplePoint = samplePt,
                                    Normal = normal,
                                    Elevation = elev,
                                    Depth = 0
                                });
                            }
                            else
                            {
                                unclosedCurveIds.Add(id);
                            }
                        }
                    }
                    catch { }
                }
                tr.Commit();
            }

            // Bước 2: Xây dựng cấu trúc lồng nhau (islands/holes) bằng Point-in-Polygon & Spatial Grid
            List<PolyBoundaryNode> rootNodes = BuildBoundaryHierarchy(closedNodes);

            // Bước 3: Tạo Hatch hàng loạt theo batch 200
            ProgressMeter? pm = null;
            try
            {
                pm = new ProgressMeter();
                pm.Start("Đang tạo Solid Hatch...");
                pm.SetLimit(rootNodes.Count);
            }
            catch { pm = null; }

            int batchSize = 200;
            for (int i = 0; i < rootNodes.Count; i += batchSize)
            {
                int currentBatchCount = Math.Min(batchSize, rootNodes.Count - i);
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                    for (int j = i; j < i + currentBatchCount; j++)
                    {
                        pm?.MeterProgress();
                        PolyBoundaryNode root = rootNodes[j];
                        bool created = false;

                        // Tầng 1: Thử tạo Hatch phức hợp (Root + Holes)
                        if (root.Holes.Count > 0)
                        {
                            try
                            {
                                Hatch hatch = new Hatch();
                                hatch.SetDatabaseDefaults();
                                hatch.Layer = root.Layer;
                                hatch.Color = (useCustomColor && customColor != null) ? customColor : root.Color;
                                hatch.Normal = root.Normal;
                                hatch.Elevation = root.Elevation;

                                btr.AppendEntity(hatch);
                                tr.AddNewlyCreatedDBObject(hatch, true);

                                hatch.SetHatchPattern(HatchPatternType.PreDefined, "SOLID");
                                hatch.Associative = false;

                                ObjectIdCollection outerLoop = new ObjectIdCollection { root.Id };
                                hatch.AppendLoop(HatchLoopTypes.External, outerLoop);

                                foreach (var hole in root.Holes)
                                {
                                    ObjectIdCollection holeLoop = new ObjectIdCollection { hole.Id };
                                    hatch.AppendLoop(HatchLoopTypes.Default, holeLoop);
                                }

                                hatch.EvaluateHatch(true);
                                totalCreated++;
                                created = true;

                                processedCurveIds.Add(root.Id);
                                foreach (var hole in root.Holes)
                                {
                                    processedCurveIds.Add(hole.Id);
                                }
                            }
                            catch { }
                        }

                        // Tầng 2: Fallback tạo hatch đơn lẻ cho root nếu không có holes hoặc hatch phức hợp lỗi
                        if (!created)
                        {
                            try
                            {
                                Hatch singleHatch = new Hatch();
                                singleHatch.SetDatabaseDefaults();
                                singleHatch.Layer = root.Layer;
                                singleHatch.Color = (useCustomColor && customColor != null) ? customColor : root.Color;
                                singleHatch.Normal = root.Normal;
                                singleHatch.Elevation = root.Elevation;

                                btr.AppendEntity(singleHatch);
                                tr.AddNewlyCreatedDBObject(singleHatch, true);

                                singleHatch.SetHatchPattern(HatchPatternType.PreDefined, "SOLID");
                                singleHatch.Associative = false;

                                ObjectIdCollection loop = new ObjectIdCollection { root.Id };
                                singleHatch.AppendLoop(HatchLoopTypes.Default, loop);
                                singleHatch.EvaluateHatch(true);

                                totalCreated++;
                                processedCurveIds.Add(root.Id);
                                created = true;
                            }
                            catch
                            {
                                // Tầng 3: Fallback qua Region nếu AppendLoop với entity thất bại
                                try
                                {
                                    Entity ent = (Entity)tr.GetObject(root.Id, OpenMode.ForRead);
                                    DBObjectCollection coll = new DBObjectCollection { ent };
                                    DBObjectCollection regColl = AcadRegion.CreateFromCurves(coll);
                                    if (regColl.Count > 0 && regColl[0] is AcadRegion reg)
                                    {
                                        ObjectId regId = btr.AppendEntity(reg);
                                        tr.AddNewlyCreatedDBObject(reg, true);

                                        Hatch regHatch = new Hatch();
                                        regHatch.SetDatabaseDefaults();
                                        regHatch.Layer = root.Layer;
                                        regHatch.Color = (useCustomColor && customColor != null) ? customColor : root.Color;

                                        btr.AppendEntity(regHatch);
                                        tr.AddNewlyCreatedDBObject(regHatch, true);

                                        regHatch.SetHatchPattern(HatchPatternType.PreDefined, "SOLID");
                                        regHatch.Associative = false;

                                        ObjectIdCollection regLoop = new ObjectIdCollection { regId };
                                        regHatch.AppendLoop(HatchLoopTypes.Default, regLoop);
                                        regHatch.EvaluateHatch(true);

                                        reg.Erase();
                                        totalCreated++;
                                        processedCurveIds.Add(root.Id);
                                        created = true;
                                    }
                                }
                                catch { }
                            }

                            // Nếu hatch phức hợp thất bại, xử lý từng lỗ con độc lập để KHÔNG BAO GIỜ làm mất đối tượng
                            if (root.Holes.Count > 0)
                            {
                                foreach (var hole in root.Holes)
                                {
                                    try
                                    {
                                        Hatch holeHatch = new Hatch();
                                        holeHatch.SetDatabaseDefaults();
                                        holeHatch.Layer = hole.Layer;
                                        holeHatch.Color = (useCustomColor && customColor != null) ? customColor : hole.Color;
                                        holeHatch.Normal = hole.Normal;
                                        holeHatch.Elevation = hole.Elevation;

                                        btr.AppendEntity(holeHatch);
                                        tr.AddNewlyCreatedDBObject(holeHatch, true);

                                        holeHatch.SetHatchPattern(HatchPatternType.PreDefined, "SOLID");
                                        holeHatch.Associative = false;

                                        ObjectIdCollection loop = new ObjectIdCollection { hole.Id };
                                        holeHatch.AppendLoop(HatchLoopTypes.Default, loop);
                                        holeHatch.EvaluateHatch(true);

                                        totalCreated++;
                                        processedCurveIds.Add(hole.Id);
                                    }
                                    catch { }
                                }
                            }
                        }
                    }

                    tr.Commit();
                }
            }

            try { pm?.Stop(); } catch { }

            // Bước 4: Xử lý các đường hở (Lines, Arcs...) tạo thành chu trình khép kín
            if (unclosedCurveIds.Count > 0)
            {
                int unclosedCreated = ProcessUnclosedCurvesToHatch(unclosedCurveIds, db, ed, useCustomColor, customColor, processedCurveIds);
                totalCreated += unclosedCreated;
            }

            return totalCreated;
        }

        /// <summary>
        /// Xử lý tạo 3D Solid hàng loạt cho danh sách Curves/Polylines bằng extrude Region
        /// </summary>
        private static int ProcessPolylinesTo3DSolidBatch(List<ObjectId> curveIds, Database db, Editor ed,
            double extrusionHeight, bool useCustomColor, AcadColor? customColor, List<ObjectId> processedCurveIds)
        {
            int totalCreated = 0;
            List<PolyBoundaryNode> closedNodes = new List<PolyBoundaryNode>();
            List<ObjectId> unclosedCurveIds = new List<ObjectId>();

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                foreach (ObjectId id in curveIds)
                {
                    try
                    {
                        Entity ent = (Entity)tr.GetObject(id, OpenMode.ForRead);
                        if (ent is Curve curve)
                        {
                            if (curve is Polyline pline)
                            {
                                if (!pline.Closed && pline.NumberOfVertices >= 3 && pline.StartPoint.DistanceTo(pline.EndPoint) < 1e-4)
                                {
                                    pline.UpgradeOpen();
                                    pline.Closed = true;
                                }
                            }

                            if (IsCurveClosed(curve))
                            {
                                Extents3d ext = curve.GeometricExtents;
                                List<Point2d> poly = SampleCurveToPolygon(curve);
                                double area = CalculatePolygonArea(poly);
                                if (area <= 1e-6)
                                {
                                    area = Math.Abs((ext.MaxPoint.X - ext.MinPoint.X) * (ext.MaxPoint.Y - ext.MinPoint.Y));
                                }

                                Point2d samplePt = poly.Count > 0 ? poly[0] : new Point2d(ext.MinPoint.X, ext.MinPoint.Y);
                                if (poly.Count >= 3)
                                {
                                    double cx = 0, cy = 0;
                                    foreach (var p in poly) { cx += p.X; cy += p.Y; }
                                    Point2d centroid = new Point2d(cx / poly.Count, cy / poly.Count);
                                    if (IsPointInPolygon(centroid, poly))
                                        samplePt = centroid;
                                }

                                Vector3d normal = Vector3d.ZAxis;
                                double elev = 0.0;
                                try
                                {
                                    if (curve is Polyline pl) { normal = pl.Normal; elev = pl.Elevation; }
                                    else if (curve is Polyline2d p2) { normal = p2.Normal; elev = p2.Elevation; }
                                    else if (curve is Circle c) { normal = c.Normal; }
                                }
                                catch { }

                                closedNodes.Add(new PolyBoundaryNode
                                {
                                    Id = id,
                                    Layer = ent.Layer,
                                    Color = ent.Color,
                                    Extents = ext,
                                    Area = area,
                                    Polygon = poly,
                                    SamplePoint = samplePt,
                                    Normal = normal,
                                    Elevation = elev,
                                    Depth = 0
                                });
                            }
                            else
                            {
                                unclosedCurveIds.Add(id);
                            }
                        }
                    }
                    catch { }
                }
                tr.Commit();
            }

            List<PolyBoundaryNode> rootNodes = BuildBoundaryHierarchy(closedNodes);

            ProgressMeter? pm = null;
            try
            {
                pm = new ProgressMeter();
                pm.Start("Đang tạo 3D Solid...");
                pm.SetLimit(rootNodes.Count);
            }
            catch { pm = null; }

            int batchSize = 100;
            for (int i = 0; i < rootNodes.Count; i += batchSize)
            {
                int currentBatchCount = Math.Min(batchSize, rootNodes.Count - i);
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                    for (int j = i; j < i + currentBatchCount; j++)
                    {
                        pm?.MeterProgress();
                        PolyBoundaryNode root = rootNodes[j];
                        try
                        {
                            Entity rootEnt = (Entity)tr.GetObject(root.Id, OpenMode.ForRead);
                            DBObjectCollection rootCurveColl = new DBObjectCollection { rootEnt };
                            DBObjectCollection rootRegColl = AcadRegion.CreateFromCurves(rootCurveColl);

                            if (rootRegColl.Count > 0 && rootRegColl[0] is AcadRegion rootRegion)
                            {
                                // Xử lý trừ các lỗ con
                                foreach (var hole in root.Holes)
                                {
                                    try
                                    {
                                        Entity holeEnt = (Entity)tr.GetObject(hole.Id, OpenMode.ForRead);
                                        DBObjectCollection holeCurveColl = new DBObjectCollection { holeEnt };
                                        DBObjectCollection holeRegColl = AcadRegion.CreateFromCurves(holeCurveColl);
                                        if (holeRegColl.Count > 0 && holeRegColl[0] is AcadRegion holeRegion)
                                        {
                                            rootRegion.BooleanOperation(BooleanOperationType.BoolSubtract, holeRegion);
                                            holeRegion.Dispose();
                                            processedCurveIds.Add(hole.Id);
                                        }
                                    }
                                    catch { }
                                }

                                Solid3d solid = new Solid3d();
                                solid.SetDatabaseDefaults();
                                solid.Layer = root.Layer;
                                solid.Color = (useCustomColor && customColor != null) ? customColor : root.Color;

                                solid.Extrude(rootRegion, extrusionHeight, 0.0);
                                btr.AppendEntity(solid);
                                tr.AddNewlyCreatedDBObject(solid, true);

                                rootRegion.Dispose();
                                totalCreated++;
                                processedCurveIds.Add(root.Id);
                            }
                        }
                        catch { }
                    }

                    tr.Commit();
                }
            }

            try { pm?.Stop(); } catch { }

            // Xử lý các đường unclosed thành 3D Solid nếu có
            if (unclosedCurveIds.Count > 0)
            {
                int unclosedCreated = ProcessUnclosedCurvesTo3DSolid(unclosedCurveIds, db, ed, extrusionHeight, useCustomColor, customColor, processedCurveIds);
                totalCreated += unclosedCreated;
            }

            return totalCreated;
        }

        /// <summary>
        /// Xử lý các đường nét rời (Lines, Arcs...) để tạo Region và Hatch
        /// </summary>
        private static int ProcessUnclosedCurvesToHatch(List<ObjectId> unclosedCurveIds, Database db, Editor ed,
            bool useCustomColor, AcadColor? customColor, List<ObjectId> processedCurveIds)
        {
            int count = 0;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                    DBObjectCollection curves = new DBObjectCollection();
                    foreach (ObjectId id in unclosedCurveIds)
                    {
                        try
                        {
                            Entity ent = (Entity)tr.GetObject(id, OpenMode.ForRead);
                            if (ent is Curve c) curves.Add(c);
                        }
                        catch { }
                    }

                    if (curves.Count > 0)
                    {
                        DBObjectCollection regions = new DBObjectCollection();
                        try
                        {
                            regions = AcadRegion.CreateFromCurves(curves);
                        }
                        catch { }

                        foreach (DBObject obj in regions)
                        {
                            if (obj is AcadRegion region)
                            {
                                try
                                {
                                    Hatch hatch = new Hatch();
                                    hatch.SetDatabaseDefaults();
                                    hatch.Layer = region.Layer;
                                    if (useCustomColor && customColor != null)
                                        hatch.Color = customColor;

                                    ObjectId regId = btr.AppendEntity(region);
                                    tr.AddNewlyCreatedDBObject(region, true);

                                    ObjectId hatchId = btr.AppendEntity(hatch);
                                    tr.AddNewlyCreatedDBObject(hatch, true);

                                    hatch.PatternScale = 1.0;
                                    hatch.SetHatchPattern(HatchPatternType.PreDefined, "SOLID");
                                    hatch.Associative = false;

                                    ObjectIdCollection boundaries = new ObjectIdCollection { regId };
                                    hatch.AppendLoop(HatchLoopTypes.Default, boundaries);
                                    hatch.EvaluateHatch(true);
                                    count++;

                                    region.Erase();
                                }
                                catch
                                {
                                    try { region.Dispose(); } catch { }
                                }
                            }
                        }

                        if (count > 0)
                        {
                            processedCurveIds.AddRange(unclosedCurveIds);
                        }
                    }
                    tr.Commit();
                }
                catch
                {
                    tr.Abort();
                }
            }
            return count;
        }

        /// <summary>
        /// Xử lý các đường nét rời (Lines, Arcs...) để tạo Region và Extrude thành 3D Solid
        /// </summary>
        private static int ProcessUnclosedCurvesTo3DSolid(List<ObjectId> unclosedCurveIds, Database db, Editor ed,
            double extrusionHeight, bool useCustomColor, AcadColor? customColor, List<ObjectId> processedCurveIds)
        {
            int count = 0;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                    DBObjectCollection curves = new DBObjectCollection();
                    foreach (ObjectId id in unclosedCurveIds)
                    {
                        try
                        {
                            Entity ent = (Entity)tr.GetObject(id, OpenMode.ForRead);
                            if (ent is Curve c) curves.Add(c);
                        }
                        catch { }
                    }

                    if (curves.Count > 0)
                    {
                        DBObjectCollection regions = new DBObjectCollection();
                        try
                        {
                            regions = AcadRegion.CreateFromCurves(curves);
                        }
                        catch { }

                        foreach (DBObject obj in regions)
                        {
                            if (obj is AcadRegion region)
                            {
                                try
                                {
                                    Solid3d solid = new Solid3d();
                                    solid.SetDatabaseDefaults();
                                    solid.Layer = region.Layer;
                                    if (useCustomColor && customColor != null)
                                        solid.Color = customColor;

                                    solid.Extrude(region, extrusionHeight, 0.0);
                                    btr.AppendEntity(solid);
                                    tr.AddNewlyCreatedDBObject(solid, true);
                                    count++;
                                }
                                catch { }
                                finally
                                {
                                    try { region.Dispose(); } catch { }
                                }
                            }
                        }

                        if (count > 0)
                        {
                            processedCurveIds.AddRange(unclosedCurveIds);
                        }
                    }
                    tr.Commit();
                }
                catch
                {
                    tr.Abort();
                }
            }
            return count;
        }

        /// <summary>
        /// Lệnh tối ưu: Chuyển đổi hàng loạt Polyline/Curves thành Solid Hatch hoặc 3D Solid
        /// Xử lý mượt mà hàng nghìn đối tượng, tự động đục lỗ lồng nhau và giữ nguyên Layer/Color
        /// </summary>
        [CommandMethod("AT_PolysToSolid")]
        public static void AT_PolysToSolid()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            // 1. Chọn các đối tượng đường nét
            TypedValue[] filterList = new TypedValue[]
            {
                new TypedValue((int)DxfCode.Operator, "<OR"),
                new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),
                new TypedValue((int)DxfCode.Start, "POLYLINE"),
                new TypedValue((int)DxfCode.Start, "LINE"),
                new TypedValue((int)DxfCode.Start, "ARC"),
                new TypedValue((int)DxfCode.Start, "CIRCLE"),
                new TypedValue((int)DxfCode.Start, "ELLIPSE"),
                new TypedValue((int)DxfCode.Start, "SPLINE"),
                new TypedValue((int)DxfCode.Operator, "OR>")
            };
            SelectionFilter filter = new SelectionFilter(filterList);

            PromptSelectionOptions opts = new PromptSelectionOptions();
            opts.MessageForAdding = "\nChọn các Polylines/Curves để chuyển thành Solid: ";

            PromptSelectionResult result = ed.GetSelection(opts, filter);
            if (result.Status != PromptStatus.OK || result.Value.Count == 0)
            {
                ed.WriteMessage("\nKhông có đối tượng nào được chọn.");
                return;
            }

            List<ObjectId> curveIds = result.Value.GetObjectIds().Distinct().ToList();
            ed.WriteMessage($"\nĐã chọn {curveIds.Count} đối tượng.");

            // 2. Hỏi kiểu đầu ra: Solid Hatch (2D) hay 3D Solid
            PromptKeywordOptions modeOpts = new PromptKeywordOptions("\nChọn kiểu Solid cần tạo [Hatch-2D/3DSolid] <Hatch>: ");
            modeOpts.Keywords.Add("Hatch");
            modeOpts.Keywords.Add("3DSolid");
            modeOpts.Keywords.Default = "Hatch";
            PromptResult modeResult = ed.GetKeywords(modeOpts);
            if (modeResult.Status != PromptStatus.OK) return;

            bool is3DSolid = modeResult.StringResult == "3DSolid";
            double extrusionHeight = 1.0;

            if (is3DSolid)
            {
                PromptDistanceOptions distOpts = new PromptDistanceOptions("\nNhập chiều cao extrude 3D <1.0>: ");
                distOpts.DefaultValue = 1.0;
                distOpts.AllowZero = false;
                distOpts.AllowNegative = false;
                PromptDoubleResult distRes = ed.GetDistance(distOpts);
                if (distRes.Status == PromptStatus.OK && distRes.Value > 0)
                {
                    extrusionHeight = distRes.Value;
                }
            }

            // 3. Hỏi màu sắc
            PromptKeywordOptions colorOpts = new PromptKeywordOptions("\nSử dụng màu tùy chỉnh? [Yes/No] <No>: ");
            colorOpts.Keywords.Add("Yes");
            colorOpts.Keywords.Add("No");
            colorOpts.Keywords.Default = "No";
            PromptResult colorResult = ed.GetKeywords(colorOpts);

            bool useCustomColor = colorResult.StringResult == "Yes";
            AcadColor? customColor = null;

            if (useCustomColor)
            {
                PromptIntegerOptions colorIndexOpts = new PromptIntegerOptions("\nNhập mã màu ACI color index (1-255) <1>: ");
                colorIndexOpts.LowerLimit = 1;
                colorIndexOpts.UpperLimit = 255;
                colorIndexOpts.DefaultValue = 1;
                PromptIntegerResult colorIndexResult = ed.GetInteger(colorIndexOpts);
                if (colorIndexResult.Status == PromptStatus.OK)
                {
                    customColor = AcadColor.FromColorIndex(ColorMethod.ByAci, (short)colorIndexResult.Value);
                }
            }

            // 4. Thực hiện xử lý hàng loạt bằng engine tối ưu
            ed.WriteMessage($"\n→ Đang phân tích và xử lý {curveIds.Count} đối tượng...");
            int createdCount = 0;
            List<ObjectId> successfullyProcessedCurveIds = new List<ObjectId>();

            if (!is3DSolid)
            {
                createdCount = ProcessPolylinesToHatchBatch(curveIds, db, ed, useCustomColor, customColor, successfullyProcessedCurveIds);
            }
            else
            {
                createdCount = ProcessPolylinesTo3DSolidBatch(curveIds, db, ed, extrusionHeight, useCustomColor, customColor, successfullyProcessedCurveIds);
            }

            if (createdCount > 0)
            {
                string typeName = is3DSolid ? "3D Solid" : "Solid Hatch";
                ed.WriteMessage($"\n✓ Thành công: Đã tạo {createdCount} {typeName} từ {successfullyProcessedCurveIds.Count}/{curveIds.Count} đối tượng!");

                // 5. Hỏi xóa đối tượng gốc
                PromptKeywordOptions deleteOpts = new PromptKeywordOptions("\nXóa các curves/polylines gốc đã xử lý? [Yes/No] <Yes>: ");
                deleteOpts.Keywords.Add("Yes");
                deleteOpts.Keywords.Add("No");
                deleteOpts.Keywords.Default = "Yes";
                PromptResult deleteResult = ed.GetKeywords(deleteOpts);

                if (deleteResult.StringResult == "Yes")
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        foreach (ObjectId id in successfullyProcessedCurveIds)
                        {
                            try
                            {
                                Entity ent = (Entity)tr.GetObject(id, OpenMode.ForWrite);
                                if (ent != null && !ent.IsErased)
                                    ent.Erase();
                            }
                            catch { }
                        }
                        tr.Commit();
                    }
                    ed.WriteMessage($"\n✓ Đã xóa {successfullyProcessedCurveIds.Count} đường nét gốc.");
                }
            }
            else
            {
                ed.WriteMessage("\n✗ Không tạo được đối tượng Solid nào từ các đường nét đã chọn.");
            }
        }

        #endregion

        private static void ResetState()
        {
            _beforeHandles.Clear();
            _textSourceInfos.Clear();
            _useCustomColor = false;
            _solidColor = null;
            _create3DSolid = false;
            _extrusionHeight = 1.0;
        }

        /// <summary>
        /// Chọn các đối tượng Text (DBText hoặc MText)
        /// </summary>
        private static List<ObjectId> SelectTexts(Editor ed)
        {
            List<ObjectId> textIds = new List<ObjectId>();

            TypedValue[] filterList = new TypedValue[]
            {
                new TypedValue((int)DxfCode.Operator, "<OR"),
                new TypedValue((int)DxfCode.Start, "TEXT"),
                new TypedValue((int)DxfCode.Start, "MTEXT"),
                new TypedValue((int)DxfCode.Operator, "OR>")
            };
            SelectionFilter filter = new SelectionFilter(filterList);

            PromptSelectionOptions opts = new PromptSelectionOptions();
            opts.MessageForAdding = "\nChọn các Text hoặc MText để chuyển thành Solid: ";
            opts.AllowDuplicates = false;

            PromptSelectionResult result = ed.GetSelection(opts, filter);

            if (result.Status == PromptStatus.OK)
            {
                textIds.AddRange(result.Value.GetObjectIds());
                ed.WriteMessage($"\nĐã chọn {textIds.Count} text.");
            }

            return textIds;
        }

        /// <summary>
        /// Lấy tất cả handles trong ModelSpace
        /// </summary>
        private static HashSet<long> GetAllHandles(Database db)
        {
            HashSet<long> handles = new HashSet<long>();
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                foreach (ObjectId objId in btr)
                {
                    handles.Add(objId.Handle.Value);
                }
                tr.Commit();
            }
            return handles;
        }
    }

    /// <summary>
    /// Form cấu hình cho lệnh AT_TextToSolid
    /// </summary>
    public class TextToSolidForm : Form
    {
        private WinFormsLabel? lblInfo;
        private WinFormsLabel? lblNote;
        private WinFormsLabel? lblOutputType;
        private RadioButton? rbHatch;
        private RadioButton? rb3DSolid;
        private WinFormsLabel? lblExtrusionHeight;
        private TextBox? txtExtrusionHeight;
        private CheckBox? chkUseCustomColor;
        private Button? btnSelectColor;
        private Panel? pnlColorPreview;
        private Button? btnOK;
        private Button? btnCancel;

        public int SelectedTextCount { get; set; } = 0;
        public bool DeleteOriginalText => true; // TXTEXP always replaces
        public bool UseCustomColor => chkUseCustomColor?.Checked ?? false;
        public AcadColor? SolidColor { get; private set; }
        public bool Create3DSolid => rb3DSolid?.Checked ?? false;
        public double ExtrusionHeight 
        { 
            get 
            { 
                if (double.TryParse(txtExtrusionHeight?.Text, out double h) && h > 0)
                    return h;
                return 1.0;
            } 
        }

        private System.Drawing.Color _selectedColor = System.Drawing.Color.Red;

        public TextToSolidForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "AT_TextToSolid - Chuyển Text thành Solid";
            this.Size = new System.Drawing.Size(420, 360);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = System.Drawing.Color.FromArgb(45, 45, 48);
            this.ForeColor = System.Drawing.Color.White;

            lblInfo = new WinFormsLabel()
            {
                Text = "Cấu hình chuyển đổi Text thành Solid",
                Location = new System.Drawing.Point(20, 20),
                Size = new System.Drawing.Size(380, 25),
                Font = new System.Drawing.Font("Segoe UI", 10, System.Drawing.FontStyle.Bold),
                ForeColor = System.Drawing.Color.LightSkyBlue
            };

            lblNote = new WinFormsLabel()
            {
                Text = "✓ Hỗ trợ chọn và xử lý hàng loạt nhiều Text/MText cùng lúc\n" +
                       "✓ Tự động xử lý chữ rỗng ruột (O, A, B, 8...) và giữ nguyên Layer/Color",
                Location = new System.Drawing.Point(20, 50),
                Size = new System.Drawing.Size(380, 40),
                Font = new System.Drawing.Font("Segoe UI", 8),
                ForeColor = System.Drawing.Color.LightGreen
            };

            // Output type selection
            lblOutputType = new WinFormsLabel()
            {
                Text = "Loại output:",
                Location = new System.Drawing.Point(20, 100),
                Size = new System.Drawing.Size(100, 25),
                Font = new System.Drawing.Font("Segoe UI", 9, System.Drawing.FontStyle.Bold),
                ForeColor = System.Drawing.Color.White
            };

            rbHatch = new RadioButton()
            {
                Text = "Solid Hatch (2D)",
                Location = new System.Drawing.Point(130, 98),
                Size = new System.Drawing.Size(130, 25),
                Checked = true,
                ForeColor = System.Drawing.Color.White,
                Font = new System.Drawing.Font("Segoe UI", 9)
            };

            rb3DSolid = new RadioButton()
            {
                Text = "3D Solid",
                Location = new System.Drawing.Point(270, 98),
                Size = new System.Drawing.Size(100, 25),
                Checked = false,
                ForeColor = System.Drawing.Color.White,
                Font = new System.Drawing.Font("Segoe UI", 9)
            };
            rb3DSolid.CheckedChanged += Rb3DSolid_CheckedChanged;

            // Extrusion height
            lblExtrusionHeight = new WinFormsLabel()
            {
                Text = "Chiều cao extrude:",
                Location = new System.Drawing.Point(40, 130),
                Size = new System.Drawing.Size(130, 25),
                Font = new System.Drawing.Font("Segoe UI", 9),
                ForeColor = System.Drawing.Color.Gray,
                Enabled = false
            };

            txtExtrusionHeight = new TextBox()
            {
                Text = "1.0",
                Location = new System.Drawing.Point(180, 128),
                Size = new System.Drawing.Size(80, 25),
                BackColor = System.Drawing.Color.FromArgb(62, 62, 66),
                ForeColor = System.Drawing.Color.Gray,
                Font = new System.Drawing.Font("Segoe UI", 9),
                BorderStyle = BorderStyle.FixedSingle,
                Enabled = false
            };

            // Color options
            chkUseCustomColor = new CheckBox()
            {
                Text = "Sử dụng màu tùy chỉnh (mặc định: giữ màu text)",
                Location = new System.Drawing.Point(20, 170),
                Size = new System.Drawing.Size(350, 25),
                Checked = false,
                ForeColor = System.Drawing.Color.White,
                Font = new System.Drawing.Font("Segoe UI", 9)
            };
            chkUseCustomColor.CheckedChanged += ChkUseCustomColor_CheckedChanged;

            btnSelectColor = new Button()
            {
                Text = "Chọn màu...",
                Location = new System.Drawing.Point(40, 200),
                Size = new System.Drawing.Size(120, 30),
                Enabled = false,
                BackColor = System.Drawing.Color.FromArgb(62, 62, 66),
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnSelectColor.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(100, 100, 100);
            btnSelectColor.Click += BtnSelectColor_Click;

            pnlColorPreview = new Panel()
            {
                Location = new System.Drawing.Point(170, 200),
                Size = new System.Drawing.Size(80, 30),
                BackColor = _selectedColor,
                BorderStyle = BorderStyle.FixedSingle
            };

            btnOK = new Button()
            {
                Text = "Bắt đầu",
                Location = new System.Drawing.Point(200, 260),
                Size = new System.Drawing.Size(90, 35),
                DialogResult = DialogResult.OK,
                BackColor = System.Drawing.Color.FromArgb(0, 122, 204),
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new System.Drawing.Font("Segoe UI", 9, System.Drawing.FontStyle.Bold)
            };
            btnOK.FlatAppearance.BorderSize = 0;

            btnCancel = new Button()
            {
                Text = "Hủy",
                Location = new System.Drawing.Point(300, 260),
                Size = new System.Drawing.Size(90, 35),
                DialogResult = DialogResult.Cancel,
                BackColor = System.Drawing.Color.FromArgb(62, 62, 66),
                ForeColor = System.Drawing.Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new System.Drawing.Font("Segoe UI", 9)
            };
            btnCancel.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(100, 100, 100);

            this.Controls.AddRange(new Control[]
            {
                lblInfo, lblNote, 
                lblOutputType, rbHatch, rb3DSolid,
                lblExtrusionHeight, txtExtrusionHeight,
                chkUseCustomColor, btnSelectColor, pnlColorPreview,
                btnOK, btnCancel
            });

            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (lblInfo != null)
            {
                lblInfo.Text = $"Đã chọn {SelectedTextCount} text để chuyển đổi";
            }
        }

        private void Rb3DSolid_CheckedChanged(object? sender, EventArgs e)
        {
            bool enabled = rb3DSolid?.Checked ?? false;
            if (lblExtrusionHeight != null)
            {
                lblExtrusionHeight.Enabled = enabled;
                lblExtrusionHeight.ForeColor = enabled ? System.Drawing.Color.White : System.Drawing.Color.Gray;
            }
            if (txtExtrusionHeight != null)
            {
                txtExtrusionHeight.Enabled = enabled;
                txtExtrusionHeight.ForeColor = enabled ? System.Drawing.Color.White : System.Drawing.Color.Gray;
            }
        }

        private void ChkUseCustomColor_CheckedChanged(object? sender, EventArgs e)
        {
            bool enabled = chkUseCustomColor?.Checked ?? false;
            if (btnSelectColor != null)
                btnSelectColor.Enabled = enabled;
        }

        private void BtnSelectColor_Click(object? sender, EventArgs e)
        {
            using (ColorDialog colorDialog = new ColorDialog())
            {
                colorDialog.Color = _selectedColor;
                colorDialog.FullOpen = true;

                if (colorDialog.ShowDialog() == DialogResult.OK)
                {
                    _selectedColor = colorDialog.Color;
                    if (pnlColorPreview != null)
                        pnlColorPreview.BackColor = _selectedColor;

                    SolidColor = AcadColor.FromRgb(_selectedColor.R, _selectedColor.G, _selectedColor.B);
                }
            }
        }
    }
}

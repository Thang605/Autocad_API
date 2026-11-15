using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using ClosedXML.Excel;
using MyFirstProject.Extensions;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using CivSurface = Autodesk.Civil.DatabaseServices.TinSurface;

namespace Civil3DCsharp
{
    public static partial class SightDistanceEngine
    {
        /// <summary>
        /// Tạo Layer nếu chưa có
        /// </summary>
        public static ObjectId GetOrCreateLayer(Database db, Transaction tr, string layerName, short colorIndex)
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (lt.Has(layerName)) return lt[layerName];

            lt.UpgradeOpen();
            var ltr = new LayerTableRecord
            {
                Name = layerName,
                Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(ColorMethod.ByAci, colorIndex)
            };
            ObjectId layerId = lt.Add(ltr);
            tr.AddNewlyCreatedDBObject(ltr, true);
            return layerId;
        }

        /// <summary>
        /// Vẽ đường Polyline biểu diễn quỹ đạo di chuyển của mắt người lái xe (Offset từ tim tuyến / polyline một khoảng bằng laneOffset)
        /// </summary>
        public static ObjectId DrawDriverEyeTrajectory(
            Database db,
            Transaction tr,
            BlockTableRecord btr,
            Alignment? alignment,
            ObjectId polylineId,
            double laneOffset,
            double startStation,
            double endStation)
        {
            string layerEyePath = "C3D_QUY_DAO_MAT_LAI_XE";
            GetOrCreateLayer(db, tr, layerEyePath, 4); // Màu 4 = Cyan (Xanh lơ nổi bật)

            var eyePathPline = new Polyline
            {
                Layer = layerEyePath,
                ColorIndex = 4,
                LineWeight = LineWeight.LineWeight030
            };
            int idx = 0;

            if (alignment != null)
            {
                double step = 2.0;
                double curSt = startStation;
                while (curSt < endStation)
                {
                    double x = 0, y = 0;
                    alignment.PointLocation(curSt, laneOffset, ref x, ref y);
                    eyePathPline.AddVertexAt(idx++, new Point2d(x, y), 0, 0, 0);
                    curSt += step;
                }
                double ex = 0, ey = 0;
                alignment.PointLocation(endStation, laneOffset, ref ex, ref ey);
                eyePathPline.AddVertexAt(idx++, new Point2d(ex, ey), 0, 0, 0);
            }
            else if (!polylineId.IsNull && polylineId.IsValid)
            {
                var curve = tr.GetObject(polylineId, OpenMode.ForRead) as Curve;
                if (curve != null)
                {
                    double totalLen = curve.GetDistanceAtParameter(curve.EndParam);
                    double step = 2.0;
                    double s = 0.0;
                    while (s < totalLen)
                    {
                        Point3d pt = curve.GetPointAtDist(s);
                        Vector3d tangent = curve.GetFirstDerivative(curve.GetParameterAtDistance(s));
                        double tLen = Math.Sqrt(tangent.X * tangent.X + tangent.Y * tangent.Y);
                        if (tLen > 1e-6)
                        {
                            Vector3d normal2d = new Vector3d(-tangent.Y / tLen, tangent.X / tLen, 0);
                            Point3d ptEye = pt + normal2d * laneOffset;
                            eyePathPline.AddVertexAt(idx++, new Point2d(ptEye.X, ptEye.Y), 0, 0, 0);
                        }
                        s += step;
                    }
                    Point3d ptEnd = curve.GetPointAtDist(totalLen);
                    Vector3d endTangent = curve.GetFirstDerivative(curve.GetParameterAtDistance(totalLen));
                    double endTLen = Math.Sqrt(endTangent.X * endTangent.X + endTangent.Y * endTangent.Y);
                    if (endTLen > 1e-6)
                    {
                        Vector3d normal2d = new Vector3d(-endTangent.Y / endTLen, endTangent.X / endTLen, 0);
                        Point3d ptEye = ptEnd + normal2d * laneOffset;
                        eyePathPline.AddVertexAt(idx++, new Point2d(ptEye.X, ptEye.Y), 0, 0, 0);
                    }
                }
            }

            if (eyePathPline.NumberOfVertices > 1)
            {
                btr.AppendEntity(eyePathPline);
                tr.AddNewlyCreatedDBObject(eyePathPline, true);
                return eyePathPline.ObjectId;
            }
            return ObjectId.Null;
        }

        /// <summary>
        /// Vẽ tia ngắm và vùng dạt tầm nhìn lên CAD
        /// </summary>
        public static int DrawSightRaysToCAD(
            List<SightCheckItem> items,
            bool drawOnlyNonPass,
            bool drawEyeTrajectory = true,
            ObjectId alignmentId = default,
            ObjectId polylineId = default,
            double laneOffset = 1.75,
            double startStation = 0,
            double endStation = 0,
            Alignment? alignment = null)
        {
            int count = 0;
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                string layerClear = "C3D_TAM_NHIN_DAT";
                string layerBlocked = "C3D_TAM_NHIN_KHUAT";
                GetOrCreateLayer(db, tr, layerClear, 3);   // Xanh lá
                GetOrCreateLayer(db, tr, layerBlocked, 1); // Đỏ

                // 1. Vẽ đường Polyline biểu diễn quỹ đạo di chuyển của mắt người lái xe (Offset làn)
                if (drawEyeTrajectory)
                {
                    Alignment? alignToUse = alignment;
                    if (alignToUse == null && !alignmentId.IsNull && alignmentId.IsValid)
                    {
                        try { alignToUse = tr.GetObject(alignmentId, OpenMode.ForRead) as Alignment; } catch { }
                    }
                    if (alignToUse != null)
                    {
                        if (startStation == 0 && endStation == 0)
                        {
                            startStation = alignToUse.StartingStation;
                            endStation = alignToUse.EndingStation;
                        }
                    }
                    DrawDriverEyeTrajectory(db, tr, btr, alignToUse, polylineId, laneOffset, startStation, endStation);
                }

                foreach (var item in items)
                {
                    if (drawOnlyNonPass && item.Status == SightCheckStatus.Pass) continue;

                    string targetLayer = (item.Status == SightCheckStatus.Pass) ? layerClear : layerBlocked;
                    short colorIdx = (item.Status == SightCheckStatus.Pass) ? (short)3 : (item.Status == SightCheckStatus.Warning ? (short)2 : (short)1);

                    // 2. Vẽ tia ngắm phẳng trên mặt bằng CAD với cao độ Z = 0
                    var ptStart2D = new Point3d(item.EyePoint.X, item.EyePoint.Y, 0.0);
                    var ptEnd2D = new Point3d(item.TargetPoint.X, item.TargetPoint.Y, 0.0);

                    var line = new Line(ptStart2D, ptEnd2D)
                    {
                        Layer = targetLayer,
                        ColorIndex = colorIdx
                    };
                    btr.AppendEntity(line);
                    tr.AddNewlyCreatedDBObject(line, true);
                    count++;

                    // 3. Nếu bị chắn, vẽ điểm va chạm với cao độ Z = 0
                    if (item.ObstructionPoint.HasValue)
                    {
                        var circle = new Circle
                        {
                            Center = new Point3d(item.ObstructionPoint.Value.X, item.ObstructionPoint.Value.Y, 0.0),
                            Radius = 5.0,
                            Layer = layerBlocked,
                            ColorIndex = 1
                        };
                        btr.AppendEntity(circle);
                        tr.AddNewlyCreatedDBObject(circle, true);
                    }
                }

                tr.Commit();
            }

            return count;
        }

        /// <summary>
        /// Đánh dấu các vị trí không đạt / cảnh báo lên mặt bằng tuyến CAD
        /// </summary>
        public static int DrawWarningMarksToCAD(List<SightCheckItem> items, Alignment align)
        {
            int count = 0;
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                string layerMark = "C3D_TAM_NHIN_CANH_BAO";
                GetOrCreateLayer(db, tr, layerMark, 1);

                foreach (var item in items.Where(i => i.Status != SightCheckStatus.Pass))
                {
                    short colorIdx = (item.Status == SightCheckStatus.Fail) ? (short)1 : (short)2; // 1 = Đỏ, 2 = Vàng

                    // Vòng tròn tại tim tuyến
                    var circle = new Circle
                    {
                        Center = new Point3d(item.X, item.Y, 0),
                        Radius = 15.0,
                        Layer = layerMark,
                        ColorIndex = colorIdx
                    };
                    btr.AppendEntity(circle);
                    tr.AddNewlyCreatedDBObject(circle, true);

                    // Crosshair chữ thập
                    var l1 = new Line(new Point3d(item.X - 20, item.Y, 0), new Point3d(item.X + 20, item.Y, 0)) { Layer = layerMark, ColorIndex = colorIdx };
                    var l2 = new Line(new Point3d(item.X, item.Y - 20, 0), new Point3d(item.X, item.Y + 20, 0)) { Layer = layerMark, ColorIndex = colorIdx };
                    btr.AppendEntity(l1);
                    btr.AppendEntity(l2);
                    tr.AddNewlyCreatedDBObject(l1, true);
                    tr.AddNewlyCreatedDBObject(l2, true);

                    // Text ghi chú vi phạm
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

            return count;
        }
    }
}

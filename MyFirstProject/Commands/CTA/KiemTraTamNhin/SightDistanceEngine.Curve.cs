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
        /// Chuyển đổi một đường Polyline 2D thành Polyline3d bằng cách nội suy cao độ từ Surface
        /// </summary>
        public static ObjectId ConvertPolylineToPolyline3d(Polyline pline, CivSurface? surface, double sampleStep = 5.0)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            ObjectId poly3dId = ObjectId.Null;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                string layerName = "C3D_TIM_TUYEN_3D_POLYLINE";
                GetOrCreateLayer(db, tr, layerName, 4); // Màu Cyan

                double totalLength = pline.Length;
                var distSet = new SortedSet<double>();

                // 1. Thêm khoảng cách tại các đỉnh hiện tại của Polyline
                for (int i = 0; i < pline.NumberOfVertices; i++)
                {
                    double dist = pline.GetDistanceAtParameter(i);
                    distSet.Add(dist);
                }

                // 2. Thêm các điểm chia mẫu đều theo sampleStep
                for (double d = 0; d < totalLength; d += sampleStep)
                {
                    distSet.Add(d);
                }
                distSet.Add(totalLength);

                // 3. Tạo Point3dCollection với cao độ từ Surface (hoặc pline.Elevation)
                var ptColl = new Point3dCollection();
                foreach (double d in distSet)
                {
                    Point3d pt2d = pline.GetPointAtDist(d);
                    double z = pline.Elevation;
                    if (surface != null)
                    {
                        try { z = surface.FindElevationAtXY(pt2d.X, pt2d.Y); } catch { }
                    }
                    ptColl.Add(new Point3d(pt2d.X, pt2d.Y, z));
                }

                if (ptColl.Count > 1)
                {
                    var poly3d = new Polyline3d(Poly3dType.SimplePoly, ptColl, pline.Closed)
                    {
                        Layer = layerName,
                        ColorIndex = 4
                    };
                    poly3dId = btr.AppendEntity(poly3d);
                    tr.AddNewlyCreatedDBObject(poly3d, true);
                }

                tr.Commit();
            }

            return poly3dId;
        }

        /// <summary>
        /// Phân tích toàn diện tầm nhìn cho đối tượng Polyline hoặc Polyline3d
        /// </summary>
        public static List<SightCheckItem> AnalyzeSightDistanceForCurve(
            Curve curve,
            CivSurface? surface,
            SightStandardDefinition standard,
            int speed,
            SightType sightType,
            double startDist,
            double endDist,
            double interval,
            double laneOffset,
            double customEyeHeight,
            double customTargetHeight,
            bool checkGradeEffect,
            bool checkHorizontalClearance,
            bool check3DSurface,
            double customRequiredSight = 0.0)
        {
            var results = new List<SightCheckItem>();
            var entry = standard.GetEntry(speed);
            if (entry == null) return results;

            double baseSight = (customRequiredSight > 0)
                ? customRequiredSight
                : ((sightType == SightType.Stopping) ? entry.StoppingSightDist : entry.PassingSightDist);
            double eyeH = (customEyeHeight > 0) ? customEyeHeight : standard.EyeHeight;
            double targetH = (customTargetHeight > 0) ? customTargetHeight : ((sightType == SightType.Stopping) ? standard.TargetHeightStopping : standard.TargetHeightPassing);

            double totalLength = curve.GetDistanceAtParameter(curve.EndParam);
            if (endDist <= 0 || endDist > totalLength) endDist = totalLength;

            for (double s = startDist; s <= endDist + 0.001; s += interval)
            {
                if (s > totalLength) s = totalLength;

                var item = new SightCheckItem
                {
                    Station = s,
                    RequiredSight = baseSight,
                    ActualSight = baseSight,
                    Status = SightCheckStatus.Pass
                };

                // 1. Lấy tọa độ tim đường
                Point3d ptCenter = curve.GetPointAtDist(s);
                item.X = ptCenter.X;
                item.Y = ptCenter.Y;
                item.Z = ptCenter.Z;

                Vector3d normal2d = PopulateCurvePositionAndGrade(curve, s, totalLength, ptCenter,
                    sightType, checkGradeEffect, speed, entry, item);

                double hRadius = FindCurveHorizontalRadius(curve, s, totalLength, ptCenter);
                item.HorizontalRadius = hRadius;

                // 5. Tính khoảng dạt bụng cong M
                ApplyHorizontalClearance(item, hRadius, checkHorizontalClearance);

                PopulateCurveSightPoints(curve, s, totalLength, ptCenter, normal2d,
                    laneOffset, eyeH, targetH, item);

                // 7. Kiểm tra tia ngắm 3D xuyên qua Surface
                ApplySurfaceSightCheck(surface, check3DSurface, item);

                if (string.IsNullOrEmpty(item.Detail))
                {
                    item.Detail = (item.Status == SightCheckStatus.Pass) ? "Đảm bảo tầm nhìn xe chạy an toàn" : "Cần lưu ý bố trí mặt cắt";
                }

                results.Add(item);
                if (s >= totalLength) break;
            }

            return results;
        }
    }
}

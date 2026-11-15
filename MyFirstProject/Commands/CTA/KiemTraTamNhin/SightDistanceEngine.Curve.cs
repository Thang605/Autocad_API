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

                // 2. Lấy vector tiếp tuyến và pháp tuyến ngang
                Vector3d tangent = curve.GetFirstDerivative(curve.GetParameterAtDistance(s));
                double tLen = Math.Sqrt(tangent.X * tangent.X + tangent.Y * tangent.Y);
                Vector3d normal2d = new Vector3d(0, 1, 0);
                if (tLen > 1e-6)
                {
                    double ux = tangent.X / tLen;
                    double uy = tangent.Y / tLen;
                    normal2d = new Vector3d(-uy, ux, 0); // Quay 90 độ sang trái/phải
                }

                // 3. Xác định độ dốc dọc i (%)
                double sNext = Math.Min(s + 1.0, totalLength);
                if (sNext > s)
                {
                    Point3d ptNext = curve.GetPointAtDist(sNext);
                    item.GradePercent = Math.Round(((ptNext.Z - ptCenter.Z) / (sNext - s)) * 100.0, 2);
                }

                if (sightType == SightType.Stopping && checkGradeEffect)
                {
                    item.RequiredSight = CalculateStoppingSightDistance(speed, item.GradePercent, entry.FrictionFactor);
                }

                // 4. Tính bán kính cong nằm tại lý trình
                double hRadius = 0;
                if (curve is Polyline pl)
                {
                    try
                    {
                        int seg = (int)pl.GetParameterAtDistance(s);
                        if (Math.Abs(pl.GetBulgeAt(seg)) > 1e-6)
                        {
                            var arc = pl.GetArcSegment2dAt(seg);
                            hRadius = Math.Round(arc.Radius, 2);
                        }
                    }
                    catch { }
                }

                if (hRadius <= 0)
                {
                    // Lấy bán kính qua 3 điểm (s - 5m, s, s + 5m)
                    try
                    {
                        double sA = Math.Max(0, s - 5.0);
                        double sC = Math.Min(totalLength, s + 5.0);
                        if (sC - sA >= 2.0)
                        {
                            Point3d pA = curve.GetPointAtDist(sA);
                            Point3d pB = ptCenter;
                            Point3d pC = curve.GetPointAtDist(sC);

                            double da = Math.Sqrt(Math.Pow(pC.X - pB.X, 2) + Math.Pow(pC.Y - pB.Y, 2));
                            double db = Math.Sqrt(Math.Pow(pC.X - pA.X, 2) + Math.Pow(pC.Y - pA.Y, 2));
                            double dc = Math.Sqrt(Math.Pow(pB.X - pA.X, 2) + Math.Pow(pB.Y - pA.Y, 2));
                            double area2 = Math.Abs((pB.X - pA.X) * (pC.Y - pA.Y) - (pC.X - pA.X) * (pB.Y - pA.Y));
                            if (area2 > 1e-3)
                            {
                                double rad = (da * db * dc) / (2.0 * area2);
                                if (rad < 5000) hRadius = Math.Round(rad, 1);
                            }
                        }
                    }
                    catch { }
                }
                item.HorizontalRadius = hRadius;

                // 5. Tính khoảng dạt bụng cong M
                if (hRadius > 0 && checkHorizontalClearance)
                {
                    item.ClearanceWidthM = CalculateHorizontalClearance(hRadius, item.RequiredSight);
                    if (item.ClearanceWidthM > 2.0)
                    {
                        item.Status = SightCheckStatus.Warning;
                        item.Detail = $"Bụng đường cong R={hRadius:F0}m cần dạt chướng ngại vật M={item.ClearanceWidthM:F2}m";
                    }
                }

                // 6. Điểm mắt tài xế & mục tiêu 3D
                Point3d eyeBase = ptCenter + normal2d * laneOffset;
                item.EyePoint = new Point3d(eyeBase.X, eyeBase.Y, ptCenter.Z + eyeH);

                double sTgt = Math.Min(s + item.RequiredSight, totalLength);
                Point3d ptTgtCenter = curve.GetPointAtDist(sTgt);
                Vector3d tgtTangent = curve.GetFirstDerivative(curve.GetParameterAtDistance(sTgt));
                double tgtTLen = Math.Sqrt(tgtTangent.X * tgtTangent.X + tgtTangent.Y * tgtTangent.Y);
                Vector3d tgtNormal = normal2d;
                if (tgtTLen > 1e-6)
                {
                    tgtNormal = new Vector3d(-tgtTangent.Y / tgtTLen, tgtTangent.X / tgtTLen, 0);
                }

                Point3d tgtBase = ptTgtCenter + tgtNormal * laneOffset;
                item.TargetPoint = new Point3d(tgtBase.X, tgtBase.Y, ptTgtCenter.Z + targetH);

                // 7. Kiểm tra tia ngắm 3D xuyên qua Surface
                if (surface != null && check3DSurface)
                {
                    Point3d? obstruction;
                    bool isClear = Check3DSightRay(surface, item.EyePoint, item.TargetPoint, out obstruction);
                    if (!isClear && obstruction.HasValue)
                    {
                        item.Status = SightCheckStatus.Fail;
                        item.ObstructionPoint = obstruction;

                        double distToObs = item.EyePoint.DistanceTo(obstruction.Value);
                        item.ActualSight = Math.Round(distToObs, 1);
                        item.Detail = $"Tia ngắm bị chắn bởi địa hình tại cách {item.ActualSight:F1}m (Yêu cầu: {item.RequiredSight:F1}m)";
                    }
                }

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

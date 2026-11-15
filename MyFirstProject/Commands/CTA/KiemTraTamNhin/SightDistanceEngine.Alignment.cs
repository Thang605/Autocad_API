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
        /// Phân tích toàn diện tầm nhìn cho Tuyến
        /// </summary>
        public static List<SightCheckItem> AnalyzeSightDistance(
            Alignment alignment,
            Profile? profile,
            CivSurface? surface,
            SightStandardDefinition standard,
            int speed,
            SightType sightType,
            double startStation,
            double endStation,
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

            // Thu thập thông tin các đường cong nằm trên Alignment
            var horizontalArcs = new List<dynamic>();
            try
            {
                foreach (AlignmentEntity ent in alignment.Entities)
                {
                    if (ent.EntityType == AlignmentEntityType.Arc)
                    {
                        horizontalArcs.Add(ent);
                    }
                }
            }
            catch { }

            // Duyệt từng vị trí lý trình
            for (double st = startStation; st <= endStation + 0.001; st += interval)
            {
                if (st > alignment.EndingStation) st = alignment.EndingStation;

                var item = new SightCheckItem
                {
                    Station = st,
                    RequiredSight = baseSight,
                    ActualSight = baseSight,
                    Status = SightCheckStatus.Pass
                };

                // 1. Lấy tọa độ tim đường và cao độ
                double cx = 0, cy = 0;
                alignment.PointLocation(st, 0, ref cx, ref cy);
                item.X = cx;
                item.Y = cy;

                double centerZ = 0;
                if (profile != null)
                {
                    try { centerZ = profile.ElevationAt(st); }
                    catch
                    {
                        if (surface != null)
                            try { centerZ = surface.FindElevationAtXY(cx, cy); } catch { }
                    }
                }
                else if (surface != null)
                {
                    try { centerZ = surface.FindElevationAtXY(cx, cy); } catch { }
                }
                item.Z = centerZ;

                // 2. Xác định độ dốc dọc tại lý trình
                double grade = 0;
                if (profile != null)
                {
                    try
                    {
                        double stNext = Math.Min(st + 1.0, alignment.EndingStation);
                        double z1 = profile.ElevationAt(st);
                        double z2 = profile.ElevationAt(stNext);
                        grade = ((z2 - z1) / (stNext - st)) * 100.0;
                    }
                    catch { }
                }
                item.GradePercent = Math.Round(grade, 2);

                // Nếu kiểm tra dừng xe và có xét độ dốc
                if (sightType == SightType.Stopping && checkGradeEffect)
                {
                    item.RequiredSight = CalculateStoppingSightDistance(speed, item.GradePercent, entry.FrictionFactor);
                }

                // 3. Xác định bán kính cong nằm tại lý trình
                double hRadius = 0;
                foreach (var arc in horizontalArcs)
                {
                    try
                    {
                        if (st >= arc.StartStation && st <= arc.EndStation)
                        {
                            hRadius = Math.Round((double)arc.Radius, 2);
                            break;
                        }
                    }
                    catch { }
                }
                item.HorizontalRadius = hRadius;

                // 4. Tính khoảng dạt tầm nhìn bụng đường cong M
                if (hRadius > 0 && checkHorizontalClearance)
                {
                    item.ClearanceWidthM = CalculateHorizontalClearance(hRadius, item.RequiredSight);
                    if (item.ClearanceWidthM > 2.0)
                    {
                        item.Status = SightCheckStatus.Warning;
                        item.Detail = $"Bụng đường cong R={hRadius:F0}m cần dạt chướng ngại vật M={item.ClearanceWidthM:F2}m";
                    }
                }

                // 5. Kiểm tra đường cong đứng (nếu có Profile)
                if (profile != null)
                {
                    try
                    {
                        if (profile.Entities != null)
                        {
                            foreach (dynamic ent in profile.Entities)
                            {
                                try
                                {
                                    string entType = ent.GetType().Name;
                                    if (entType.Contains("Tangent")) continue;

                                    double startSt = Convert.ToDouble(ent.StartStation);
                                    double endSt = Convert.ToDouble(ent.EndStation);

                                    if (st >= startSt - 0.5 && st <= endSt + 0.5)
                                    {
                                        double vR = 0;
                                        try { vR = Convert.ToDouble(ent.Radius); } catch { }
                                        if (vR <= 0)
                                        {
                                            // Ngoại suy R từ K và chiều dài
                                            try
                                            {
                                                double len = Convert.ToDouble(ent.Length);
                                                double kVal = Convert.ToDouble(ent.K);
                                                if (kVal > 0) vR = kVal * 100.0;
                                            }
                                            catch { }
                                        }

                                        if (vR > 0)
                                        {
                                            item.VerticalRadius = Math.Round(vR, 0);
                                            // Kiểm tra cong lồi hay lõm qua GradeIn / GradeOut hoặc EntType
                                            bool isConvex = true;
                                            try
                                            {
                                                double gIn = Convert.ToDouble(ent.GradeIn);
                                                double gOut = Convert.ToDouble(ent.GradeOut);
                                                isConvex = (gIn > gOut);
                                            }
                                            catch { }

                                            item.IsConvexVertical = isConvex;

                                            if (isConvex && vR < entry.MinConvexRadius)
                                            {
                                                item.Status = SightCheckStatus.Fail;
                                                item.Detail = $"Bán kính cong lồi R={vR:F0}m < {entry.MinConvexRadius:F0}m (Yêu cầu SSD)";
                                            }
                                            else if (!isConvex && vR < entry.MinConcaveRadius)
                                            {
                                                item.Status = SightCheckStatus.Warning;
                                                item.Detail = $"Bán kính cong lõm R={vR:F0}m < {entry.MinConcaveRadius:F0}m (Yêu cầu đèn pha)";
                                            }
                                            break;
                                        }
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                    catch { }
                }

                // 6. Tính toán điểm Mắt và Điểm Mục tiêu 3D
                double eyeX = 0, eyeY = 0;
                alignment.PointLocation(st, laneOffset, ref eyeX, ref eyeY);
                double eyeZ = item.Z + eyeH;
                item.EyePoint = new Point3d(eyeX, eyeY, eyeZ);

                double targetSt = Math.Min(st + item.RequiredSight, alignment.EndingStation);
                double targetX = 0, targetY = 0;
                alignment.PointLocation(targetSt, laneOffset, ref targetX, ref targetY);

                double targetGroundZ = item.Z;
                if (profile != null)
                {
                    try { targetGroundZ = profile.ElevationAt(targetSt); } catch { }
                }
                double targetZ = targetGroundZ + targetH;
                item.TargetPoint = new Point3d(targetX, targetY, targetZ);

                // 7. Kiểm tra tia ngắm 3D xuyên qua Surface (nếu có chọn Surface)
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
                if (st >= alignment.EndingStation) break;
            }

            return results;
        }
    }
}

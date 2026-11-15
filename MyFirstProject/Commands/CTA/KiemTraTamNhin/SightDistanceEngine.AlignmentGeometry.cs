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
        /// <summary>Populates alignment coordinates, elevation fallback and grade at the station.</summary>
        private static void PopulateAlignmentPositionAndGrade(Alignment alignment, Profile? profile, CivSurface? surface, double st, SightCheckItem item)
        {
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
        }

        /// <summary>Returns the first applicable horizontal arc radius using the existing fallback behavior.</summary>
        private static double FindAlignmentHorizontalRadius(List<dynamic> horizontalArcs, double st)
        {
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
            return hRadius;
        }

        /// <summary>Applies the vertical-curve radius checks without changing their exception boundaries.</summary>
        private static void ApplyAlignmentVerticalCheck(Profile? profile, double st, SightStandardEntry entry, SightCheckItem item)
        {
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
        }

        /// <summary>Populates offset eye and target points with the existing profile elevation fallback.</summary>
        private static void PopulateAlignmentSightPoints(Alignment alignment, Profile? profile, double st,
            double laneOffset, double eyeH, double targetH, SightCheckItem item)
        {
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
        }
    }
}

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
        /// <summary>Populates curve grade and required sight, returning the horizontal normal at the supplied center.</summary>
        private static Vector3d PopulateCurvePositionAndGrade(Curve curve, double s, double totalLength, Point3d ptCenter,
            SightType sightType, bool checkGradeEffect, int speed, SightStandardEntry entry, SightCheckItem item)
        {
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
            return normal2d;
        }

        /// <summary>Returns the bulge radius or the existing three-point estimate at the supplied center.</summary>
        private static double FindCurveHorizontalRadius(Curve curve, double s, double totalLength, Point3d ptCenter)
        {
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
            return hRadius;
        }

        /// <summary>Populates curve eye and target points while retaining the target-normal fallback.</summary>
        private static void PopulateCurveSightPoints(Curve curve, double s, double totalLength, Point3d ptCenter,
            Vector3d normal2d, double laneOffset, double eyeH, double targetH, SightCheckItem item)
        {
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
        }
    }
}

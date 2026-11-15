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
        /// Tính Tầm nhìn dừng xe (Stopping Sight Distance - SSD) có xét đến ảnh hưởng của độ dốc dọc i (%)
        /// Sd = (V * t / 3.6) + V^2 / [254 * (f +- i/100)]
        /// </summary>
        public static double CalculateStoppingSightDistance(double speedKmH, double gradePercent, double frictionFactor, double reactionTimeSec = 2.5)
        {
            double v = speedKmH;
            double t = reactionTimeSec;
            double f = frictionFactor;
            double gradeDecimal = gradePercent / 100.0;

            // Quãng đường phản ứng của tài xế
            double dReaction = (v * t) / 3.6;

            // Quãng đường phanh dừng (mẫu số tránh âm hoặc chia cho 0)
            double denominator = 254.0 * (f + gradeDecimal);
            if (denominator < 10.0) denominator = 10.0;

            double dBraking = (v * v) / denominator;
            double sd = dReaction + dBraking;

            return Math.Round(sd, 2);
        }

        /// <summary>
        /// Tính khoảng cách dạt tầm nhìn bụng đường cong nằm (Middle Ordinate M / Clearance):
        /// M = R * [1 - cos(28.65° * S / R)]
        /// </summary>
        public static double CalculateHorizontalClearance(double radius, double sightDistance)
        {
            if (radius <= 0 || sightDistance <= 0) return 0.0;
            if (radius > 10000) return 0.0; // Bán kính quá lớn xem như thẳng

            // Góc ở tâm nửa cung alpha/2 tính bằng radian = S / (2 * R)
            double halfAngleRad = sightDistance / (2.0 * radius);
            if (halfAngleRad > Math.PI / 2.0) halfAngleRad = Math.PI / 2.0;

            double m = radius * (1.0 - Math.Cos(halfAngleRad));
            return Math.Round(m, 2);
        }

        /// <summary>
        /// Kiểm tra tia ngắm 3D từ mắt tài xế đến mục tiêu xem có bị cắt bởi bề mặt địa hình TIN hay không
        /// </summary>
        public static bool Check3DSightRay(CivSurface? surface, Point3d eyePt, Point3d targetPt, out Point3d? obstructionPoint)
        {
            obstructionPoint = null;
            if (surface == null) return true; // Không chọn bề mặt thì coi như tia ngắm thông

            // Lấy 25 điểm mẫu dọc theo tia ngắm giữa mắt và mục tiêu
            int numSamples = 25;
            double dx = (targetPt.X - eyePt.X) / numSamples;
            double dy = (targetPt.Y - eyePt.Y) / numSamples;
            double dz = (targetPt.Z - eyePt.Z) / numSamples;

            for (int i = 1; i < numSamples; i++)
            {
                double px = eyePt.X + i * dx;
                double py = eyePt.Y + i * dy;
                double rayZ = eyePt.Z + i * dz;

                try
                {
                    double surfaceZ = surface.FindElevationAtXY(px, py);
                    // Nếu địa hình cao hơn tia ngắm + 0.05m thì bị chắn
                    if (surfaceZ > rayZ + 0.05)
                    {
                        obstructionPoint = new Point3d(px, py, surfaceZ);
                        return false; // Bị che khuất
                    }
                }
                catch
                {
                    // Điểm nằm ngoài boundary của surface -> bỏ qua
                }
            }

            return true; // Tia ngắm thông thoáng
        }
    }
}

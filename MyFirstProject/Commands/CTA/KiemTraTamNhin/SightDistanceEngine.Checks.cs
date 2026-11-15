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
        /// <summary>Applies the shared horizontal-clearance warning and detail.</summary>
        private static void ApplyHorizontalClearance(SightCheckItem item, double hRadius, bool checkHorizontalClearance)
        {
            if (hRadius > 0 && checkHorizontalClearance)
            {
                item.ClearanceWidthM = CalculateHorizontalClearance(hRadius, item.RequiredSight);
                if (item.ClearanceWidthM > 2.0)
                {
                    item.Status = SightCheckStatus.Warning;
                    item.Detail = $"Bụng đường cong R={hRadius:F0}m cần dạt chướng ngại vật M={item.ClearanceWidthM:F2}m";
                }
            }
        }

        /// <summary>Applies the shared surface obstruction check and sight-distance detail.</summary>
        private static void ApplySurfaceSightCheck(CivSurface? surface, bool check3DSurface, SightCheckItem item)
        {
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
        }
    }
}

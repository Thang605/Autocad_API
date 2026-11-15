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
    /// <summary>
    /// Kết quả kiểm tra tại từng vị trí lý trình
    /// </summary>
    public class SightCheckItem
    {
        public double Station { get; set; }
        public string StationFormatted => $"Km{Math.Floor(Station / 1000):0}+{Station % 1000:000.00}";
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double GradePercent { get; set; }             // Độ dốc dọc %
        public double HorizontalRadius { get; set; }        // Bán kính cong nằm (0 nếu đoạn thẳng)
        public double VerticalRadius { get; set; }          // Bán kính cong đứng (0 nếu không thuộc cong đứng)
        public bool IsConvexVertical { get; set; }          // True: cong lồi, False: cong lõm
        public double RequiredSight { get; set; }           // Tầm nhìn yêu cầu (m)
        public double ActualSight { get; set; }             // Tầm nhìn thực tế khả kiến (m)
        public double ClearanceWidthM { get; set; }         // Bề rộng dạt tầm nhìn bụng cong M (m)
        public SightCheckStatus Status { get; set; }
        public string Detail { get; set; } = "";
        public Point3d EyePoint { get; set; }
        public Point3d TargetPoint { get; set; }
        public Point3d? ObstructionPoint { get; set; }      // Điểm bị che khuất (nếu có)
    }
}

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
    /// Thông số tiêu chuẩn tầm nhìn theo từng cấp vận tốc
    /// </summary>
    public class SightStandardEntry
    {
        public int Speed { get; set; }                          // km/h
        public double StoppingSightDist { get; set; }           // m (Tầm nhìn dừng xe tối thiểu S_d)
        public double PassingSightDist { get; set; }            // m (Tầm nhìn vượt xe tối thiểu S_v)
        public double MinConvexRadius { get; set; }             // m (R cong lồi thông thường)
        public double MinConvexRadiusLimit { get; set; }        // m (R cong lồi giới hạn)
        public double MinConcaveRadius { get; set; }            // m (R cong lõm thông thường)
        public double MinConcaveRadiusLimit { get; set; }       // m (R cong lõm giới hạn)
        public double MinHorizontalRadius { get; set; }         // m (R cong nằm giới hạn)
        public double MinHorizontalRadiusNormal { get; set; }   // m (R cong nằm thông thường)
        public double MaxGrade { get; set; }                    // % Độ dốc dọc tối đa
        public double LaneWidth { get; set; } = 3.50;           // m Bề rộng làn xe
        public double DefaultLaneOffset => Math.Round(LaneWidth / 2.0, 2); // m Khoảng cách từ tim đến tâm làn xe
        public double FrictionFactor { get; set; }              // Hệ số ma sát bám dọc f
    }
}

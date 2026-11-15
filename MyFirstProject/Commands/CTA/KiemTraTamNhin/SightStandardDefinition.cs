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
    /// Tiêu chuẩn tầm nhìn thiết kế đường
    /// </summary>
    public class SightStandardDefinition
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public double EyeHeight { get; set; } = 1.25;           // Chiều cao mắt h1 (m)
        public double TargetHeightStopping { get; set; } = 0.10; // Chiều cao chướng ngại vật dừng xe h2 (m)
        public double TargetHeightPassing { get; set; } = 1.25;  // Chiều cao xe ngược chiều khi vượt h2 (m)
        public List<SightStandardEntry> Entries { get; set; } = new List<SightStandardEntry>();

        public SightStandardEntry? GetEntry(int speed)
        {
            if (Entries == null || Entries.Count == 0) return null;

            var match = Entries.FirstOrDefault(e => e.Speed == speed);
            if (match != null) return match;

            var sorted = Entries.OrderBy(e => e.Speed).ToList();
            if (speed <= sorted.First().Speed)
            {
                var first = sorted.First();
                return new SightStandardEntry
                {
                    Speed = speed,
                    StoppingSightDist = first.StoppingSightDist,
                    PassingSightDist = first.PassingSightDist,
                    MinConvexRadius = first.MinConvexRadius,
                    MinConvexRadiusLimit = first.MinConvexRadiusLimit,
                    MinConcaveRadius = first.MinConcaveRadius,
                    MinConcaveRadiusLimit = first.MinConcaveRadiusLimit,
                    MinHorizontalRadius = first.MinHorizontalRadius,
                    MinHorizontalRadiusNormal = first.MinHorizontalRadiusNormal,
                    MaxGrade = first.MaxGrade,
                    LaneWidth = first.LaneWidth,
                    FrictionFactor = first.FrictionFactor
                };
            }

            if (speed >= sorted.Last().Speed)
            {
                var last = sorted.Last();
                return new SightStandardEntry
                {
                    Speed = speed,
                    StoppingSightDist = last.StoppingSightDist,
                    PassingSightDist = last.PassingSightDist,
                    MinConvexRadius = last.MinConvexRadius,
                    MinConvexRadiusLimit = last.MinConvexRadiusLimit,
                    MinConcaveRadius = last.MinConcaveRadius,
                    MinConcaveRadiusLimit = last.MinConcaveRadiusLimit,
                    MinHorizontalRadius = last.MinHorizontalRadius,
                    MinHorizontalRadiusNormal = last.MinHorizontalRadiusNormal,
                    MaxGrade = last.MaxGrade,
                    LaneWidth = last.LaneWidth,
                    FrictionFactor = last.FrictionFactor
                };
            }

            // Nội suy tuyến tính giữa 2 mốc kề nhau
            var eLower = sorted.Last(e => e.Speed < speed);
            var eUpper = sorted.First(e => e.Speed > speed);

            double factor = (double)(speed - eLower.Speed) / (eUpper.Speed - eLower.Speed);

            return new SightStandardEntry
            {
                Speed = speed,
                StoppingSightDist = Math.Round(eLower.StoppingSightDist + factor * (eUpper.StoppingSightDist - eLower.StoppingSightDist), 1),
                PassingSightDist = (eLower.PassingSightDist > 0 && eUpper.PassingSightDist > 0)
                    ? Math.Round(eLower.PassingSightDist + factor * (eUpper.PassingSightDist - eLower.PassingSightDist), 1)
                    : Math.Max(eLower.PassingSightDist, eUpper.PassingSightDist),
                MinConvexRadius = Math.Round(eLower.MinConvexRadius + factor * (eUpper.MinConvexRadius - eLower.MinConvexRadius), 0),
                MinConvexRadiusLimit = Math.Round(eLower.MinConvexRadiusLimit + factor * (eUpper.MinConvexRadiusLimit - eLower.MinConvexRadiusLimit), 0),
                MinConcaveRadius = Math.Round(eLower.MinConcaveRadius + factor * (eUpper.MinConcaveRadius - eLower.MinConcaveRadius), 0),
                MinConcaveRadiusLimit = Math.Round(eLower.MinConcaveRadiusLimit + factor * (eUpper.MinConcaveRadiusLimit - eLower.MinConcaveRadiusLimit), 0),
                MinHorizontalRadius = Math.Round(eLower.MinHorizontalRadius + factor * (eUpper.MinHorizontalRadius - eLower.MinHorizontalRadius), 0),
                MinHorizontalRadiusNormal = Math.Round(eLower.MinHorizontalRadiusNormal + factor * (eUpper.MinHorizontalRadiusNormal - eLower.MinHorizontalRadiusNormal), 0),
                MaxGrade = Math.Round(eLower.MaxGrade + factor * (eUpper.MaxGrade - eLower.MaxGrade), 1),
                LaneWidth = Math.Round(eLower.LaneWidth + factor * (eUpper.LaneWidth - eLower.LaneWidth), 2),
                FrictionFactor = Math.Round(eLower.FrictionFactor + factor * (eUpper.FrictionFactor - eLower.FrictionFactor), 2)
            };
        }
    }
}

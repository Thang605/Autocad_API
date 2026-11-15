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
    public static class SightStandardRepo
    {
        private static readonly List<SightStandardDefinition> _standards = new List<SightStandardDefinition>();

        static SightStandardRepo()
        {
            // 1. TCVN 4054:2005 - Đường ô tô thiết kế
            var tcvn4054 = new SightStandardDefinition
            {
                Name = "TCVN 4054:2005",
                Description = "TCVN 4054:2005 - Đường Ô Tô (Bảng 11, 15, 17, 19)",
                EyeHeight = 1.25,
                TargetHeightStopping = 0.10,
                TargetHeightPassing = 1.25,
                Entries = new List<SightStandardEntry>
                {
                    new SightStandardEntry { Speed = 20, StoppingSightDist = 20, PassingSightDist = 60, MinConvexRadius = 200, MinConvexRadiusLimit = 200, MinConcaveRadius = 200, MinConcaveRadiusLimit = 100, MinHorizontalRadius = 15, MinHorizontalRadiusNormal = 30, MaxGrade = 10.0, LaneWidth = 3.0, FrictionFactor = 0.42 },
                    new SightStandardEntry { Speed = 30, StoppingSightDist = 30, PassingSightDist = 100, MinConvexRadius = 600, MinConvexRadiusLimit = 400, MinConcaveRadius = 400, MinConcaveRadiusLimit = 250, MinHorizontalRadius = 30, MinHorizontalRadiusNormal = 60, MaxGrade = 9.0, LaneWidth = 3.0, FrictionFactor = 0.40 },
                    new SightStandardEntry { Speed = 40, StoppingSightDist = 40, PassingSightDist = 150, MinConvexRadius = 1000, MinConvexRadiusLimit = 700, MinConcaveRadius = 700, MinConcaveRadiusLimit = 450, MinHorizontalRadius = 60, MinHorizontalRadiusNormal = 125, MaxGrade = 8.0, LaneWidth = 3.25, FrictionFactor = 0.38 },
                    new SightStandardEntry { Speed = 60, StoppingSightDist = 75, PassingSightDist = 300, MinConvexRadius = 4000, MinConvexRadiusLimit = 2500, MinConcaveRadius = 1500, MinConcaveRadiusLimit = 1000, MinHorizontalRadius = 125, MinHorizontalRadiusNormal = 250, MaxGrade = 6.0, LaneWidth = 3.50, FrictionFactor = 0.33 },
                    new SightStandardEntry { Speed = 80, StoppingSightDist = 100, PassingSightDist = 450, MinConvexRadius = 5000, MinConvexRadiusLimit = 4000, MinConcaveRadius = 3000, MinConcaveRadiusLimit = 2000, MinHorizontalRadius = 250, MinHorizontalRadiusNormal = 500, MaxGrade = 5.0, LaneWidth = 3.50, FrictionFactor = 0.30 },
                    new SightStandardEntry { Speed = 100, StoppingSightDist = 150, PassingSightDist = 600, MinConvexRadius = 10000, MinConvexRadiusLimit = 6000, MinConcaveRadius = 5000, MinConcaveRadiusLimit = 3000, MinHorizontalRadius = 400, MinHorizontalRadiusNormal = 1000, MaxGrade = 4.0, LaneWidth = 3.75, FrictionFactor = 0.29 },
                    new SightStandardEntry { Speed = 120, StoppingSightDist = 210, PassingSightDist = 750, MinConvexRadius = 17000, MinConvexRadiusLimit = 11000, MinConcaveRadius = 6000, MinConcaveRadiusLimit = 4000, MinHorizontalRadius = 600, MinHorizontalRadiusNormal = 1500, MaxGrade = 3.0, LaneWidth = 3.75, FrictionFactor = 0.28 }
                }
            };

            // 2. TCVN 13592:2022 - Đường Đô thị
            var tcvn13592 = new SightStandardDefinition
            {
                Name = "TCVN 13592:2022",
                Description = "TCVN 13592:2022 - Đường Đô Thị (Bảng 16, 23, 26)",
                EyeHeight = 1.00,
                TargetHeightStopping = 0.10,
                TargetHeightPassing = 1.20,
                Entries = new List<SightStandardEntry>
                {
                    new SightStandardEntry { Speed = 30, StoppingSightDist = 30, PassingSightDist = 100, MinConvexRadius = 600, MinConvexRadiusLimit = 400, MinConcaveRadius = 400, MinConcaveRadiusLimit = 250, MinHorizontalRadius = 30, MinHorizontalRadiusNormal = 60, MaxGrade = 9.0, LaneWidth = 3.00, FrictionFactor = 0.40 },
                    new SightStandardEntry { Speed = 40, StoppingSightDist = 45, PassingSightDist = 150, MinConvexRadius = 1000, MinConvexRadiusLimit = 700, MinConcaveRadius = 700, MinConcaveRadiusLimit = 450, MinHorizontalRadius = 60, MinHorizontalRadiusNormal = 125, MaxGrade = 8.0, LaneWidth = 3.25, FrictionFactor = 0.38 },
                    new SightStandardEntry { Speed = 50, StoppingSightDist = 60, PassingSightDist = 220, MinConvexRadius = 2500, MinConvexRadiusLimit = 1500, MinConcaveRadius = 1000, MinConcaveRadiusLimit = 700, MinHorizontalRadius = 100, MinHorizontalRadiusNormal = 150, MaxGrade = 7.0, LaneWidth = 3.50, FrictionFactor = 0.35 },
                    new SightStandardEntry { Speed = 60, StoppingSightDist = 75, PassingSightDist = 300, MinConvexRadius = 4000, MinConvexRadiusLimit = 2500, MinConcaveRadius = 1500, MinConcaveRadiusLimit = 1000, MinHorizontalRadius = 125, MinHorizontalRadiusNormal = 250, MaxGrade = 6.0, LaneWidth = 3.50, FrictionFactor = 0.33 },
                    new SightStandardEntry { Speed = 80, StoppingSightDist = 110, PassingSightDist = 450, MinConvexRadius = 5000, MinConvexRadiusLimit = 4000, MinConcaveRadius = 3000, MinConcaveRadiusLimit = 2000, MinHorizontalRadius = 250, MinHorizontalRadiusNormal = 500, MaxGrade = 5.0, LaneWidth = 3.75, FrictionFactor = 0.30 },
                    new SightStandardEntry { Speed = 100, StoppingSightDist = 160, PassingSightDist = 600, MinConvexRadius = 10000, MinConvexRadiusLimit = 6000, MinConcaveRadius = 5000, MinConcaveRadiusLimit = 3000, MinHorizontalRadius = 400, MinHorizontalRadiusNormal = 1000, MaxGrade = 4.0, LaneWidth = 3.75, FrictionFactor = 0.29 }
                }
            };

            // 3. TCVN 5729:2012 - Đường Cao tốc
            var tcvn5729 = new SightStandardDefinition
            {
                Name = "TCVN 5729:2012",
                Description = "TCVN 5729:2012 - Đường Cao Tốc (Bảng 4 & Bảng 6)",
                EyeHeight = 1.25,
                TargetHeightStopping = 0.10,
                TargetHeightPassing = 1.25,
                Entries = new List<SightStandardEntry>
                {
                    new SightStandardEntry { Speed = 60, StoppingSightDist = 75, PassingSightDist = 300, MinConvexRadius = 2000, MinConvexRadiusLimit = 1500, MinConcaveRadius = 2000, MinConcaveRadiusLimit = 1000, MinHorizontalRadius = 200, MinHorizontalRadiusNormal = 400, MaxGrade = 6.0, LaneWidth = 3.75, FrictionFactor = 0.33 },
                    new SightStandardEntry { Speed = 80, StoppingSightDist = 110, PassingSightDist = 450, MinConvexRadius = 4500, MinConvexRadiusLimit = 3000, MinConcaveRadius = 3000, MinConcaveRadiusLimit = 2000, MinHorizontalRadius = 400, MinHorizontalRadiusNormal = 700, MaxGrade = 6.0, LaneWidth = 3.75, FrictionFactor = 0.30 },
                    new SightStandardEntry { Speed = 100, StoppingSightDist = 160, PassingSightDist = 600, MinConvexRadius = 10000, MinConvexRadiusLimit = 6000, MinConcaveRadius = 4500, MinConcaveRadiusLimit = 3000, MinHorizontalRadius = 700, MinHorizontalRadiusNormal = 1200, MaxGrade = 5.0, LaneWidth = 3.75, FrictionFactor = 0.29 },
                    new SightStandardEntry { Speed = 120, StoppingSightDist = 210, PassingSightDist = 750, MinConvexRadius = 17000, MinConvexRadiusLimit = 12000, MinConcaveRadius = 6000, MinConcaveRadiusLimit = 5000, MinHorizontalRadius = 1000, MinHorizontalRadiusNormal = 2000, MaxGrade = 4.0, LaneWidth = 3.75, FrictionFactor = 0.28 }
                }
            };

            // 4. TCVN 10380:2014 - Đường Giao thông nông thôn
            var tcvn10380 = new SightStandardDefinition
            {
                Name = "TCVN 10380:2014",
                Description = "TCVN 10380:2014 - Đường GTNT (Bảng 7, 9)",
                EyeHeight = 1.25,
                TargetHeightStopping = 0.10,
                TargetHeightPassing = 1.25,
                Entries = new List<SightStandardEntry>
                {
                    new SightStandardEntry { Speed = 15, StoppingSightDist = 15, PassingSightDist = 45, MinConvexRadius = 150, MinConvexRadiusLimit = 100, MinConcaveRadius = 150, MinConcaveRadiusLimit = 100, MinHorizontalRadius = 15, MinHorizontalRadiusNormal = 15, MaxGrade = 12.0, LaneWidth = 2.50, FrictionFactor = 0.45 },
                    new SightStandardEntry { Speed = 20, StoppingSightDist = 20, PassingSightDist = 60, MinConvexRadius = 200, MinConvexRadiusLimit = 150, MinConcaveRadius = 200, MinConcaveRadiusLimit = 100, MinHorizontalRadius = 15, MinHorizontalRadiusNormal = 30, MaxGrade = 11.0, LaneWidth = 2.75, FrictionFactor = 0.42 },
                    new SightStandardEntry { Speed = 30, StoppingSightDist = 30, PassingSightDist = 100, MinConvexRadius = 600, MinConvexRadiusLimit = 400, MinConcaveRadius = 400, MinConcaveRadiusLimit = 250, MinHorizontalRadius = 30, MinHorizontalRadiusNormal = 60, MaxGrade = 10.0, LaneWidth = 3.00, FrictionFactor = 0.40 },
                    new SightStandardEntry { Speed = 40, StoppingSightDist = 40, PassingSightDist = 150, MinConvexRadius = 1000, MinConvexRadiusLimit = 700, MinConcaveRadius = 700, MinConcaveRadiusLimit = 450, MinHorizontalRadius = 60, MinHorizontalRadiusNormal = 125, MaxGrade = 9.0, LaneWidth = 3.00, FrictionFactor = 0.38 }
                }
            };

            // 5. AASHTO Green Book
            var aashto = new SightStandardDefinition
            {
                Name = "AASHTO Green Book",
                Description = "AASHTO - A Policy on Geometric Design of Highways and Streets",
                EyeHeight = 1.08,
                TargetHeightStopping = 0.60,
                TargetHeightPassing = 1.08,
                Entries = new List<SightStandardEntry>
                {
                    new SightStandardEntry { Speed = 30, StoppingSightDist = 35, PassingSightDist = 120, MinConvexRadius = 600, MinConvexRadiusLimit = 400, MinConcaveRadius = 400, MinConcaveRadiusLimit = 250, MinHorizontalRadius = 30, MinHorizontalRadiusNormal = 50, MaxGrade = 9.0, LaneWidth = 3.00, FrictionFactor = 0.40 },
                    new SightStandardEntry { Speed = 40, StoppingSightDist = 50, PassingSightDist = 170, MinConvexRadius = 1000, MinConvexRadiusLimit = 700, MinConcaveRadius = 700, MinConcaveRadiusLimit = 450, MinHorizontalRadius = 50, MinHorizontalRadiusNormal = 100, MaxGrade = 8.0, LaneWidth = 3.30, FrictionFactor = 0.38 },
                    new SightStandardEntry { Speed = 60, StoppingSightDist = 85, PassingSightDist = 345, MinConvexRadius = 4000, MinConvexRadiusLimit = 2500, MinConcaveRadius = 1500, MinConcaveRadiusLimit = 1000, MinHorizontalRadius = 110, MinHorizontalRadiusNormal = 220, MaxGrade = 6.0, LaneWidth = 3.50, FrictionFactor = 0.33 },
                    new SightStandardEntry { Speed = 80, StoppingSightDist = 130, PassingSightDist = 540, MinConvexRadius = 5000, MinConvexRadiusLimit = 4000, MinConcaveRadius = 3000, MinConcaveRadiusLimit = 2000, MinHorizontalRadius = 210, MinHorizontalRadiusNormal = 450, MaxGrade = 5.0, LaneWidth = 3.60, FrictionFactor = 0.30 },
                    new SightStandardEntry { Speed = 100, StoppingSightDist = 185, PassingSightDist = 670, MinConvexRadius = 10000, MinConvexRadiusLimit = 6000, MinConcaveRadius = 5000, MinConcaveRadiusLimit = 3000, MinHorizontalRadius = 370, MinHorizontalRadiusNormal = 900, MaxGrade = 4.0, LaneWidth = 3.60, FrictionFactor = 0.29 },
                    new SightStandardEntry { Speed = 120, StoppingSightDist = 250, PassingSightDist = 790, MinConvexRadius = 17000, MinConvexRadiusLimit = 12000, MinConcaveRadius = 6000, MinConcaveRadiusLimit = 5000, MinHorizontalRadius = 540, MinHorizontalRadiusNormal = 1400, MaxGrade = 3.5, LaneWidth = 3.60, FrictionFactor = 0.28 }
                }
            };

            _standards.Add(tcvn4054);
            _standards.Add(tcvn13592);
            _standards.Add(tcvn5729);
            _standards.Add(tcvn10380);
            _standards.Add(aashto);
        }

        public static List<SightStandardDefinition> GetAllStandards() => _standards;
    }
}

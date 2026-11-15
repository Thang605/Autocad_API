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

                PopulateAlignmentPositionAndGrade(alignment, profile, surface, st, item);

                // Nếu kiểm tra dừng xe và có xét độ dốc
                if (sightType == SightType.Stopping && checkGradeEffect)
                {
                    item.RequiredSight = CalculateStoppingSightDistance(speed, item.GradePercent, entry.FrictionFactor);
                }

                double hRadius = FindAlignmentHorizontalRadius(horizontalArcs, st);
                item.HorizontalRadius = hRadius;

                // 4. Tính khoảng dạt tầm nhìn bụng đường cong M
                ApplyHorizontalClearance(item, hRadius, checkHorizontalClearance);

                ApplyAlignmentVerticalCheck(profile, st, entry, item);

                PopulateAlignmentSightPoints(alignment, profile, st, laneOffset, eyeH, targetH, item);

                // 7. Kiểm tra tia ngắm 3D xuyên qua Surface (nếu có chọn Surface)
                ApplySurfaceSightCheck(surface, check3DSurface, item);

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

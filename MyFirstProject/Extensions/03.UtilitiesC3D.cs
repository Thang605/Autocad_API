using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using Autodesk.Civil.Runtime;
using Autodesk.Civil.Settings;
// (C) Copyright 2016 by
//
using System;
using System.Linq;
using System.Windows.Markup;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using System.Collections.Generic;

namespace MyFirstProject.Extensions
{
    public class UtilitiesC3D
    {
        public static void CreateCogoPointFromPoint3D(Point3d point3D, string derciption)
        {
            // create cogo point from location
            CogoPointCollection cogoPointColl = A.Cdoc.CogoPoints;
            _ = cogoPointColl.Add(point3D, derciption, true);

        }
        public static void GStationsFromSamplelineGroup(out double[] station, out SampleLine sampleLineOut)
        {
            Transaction tr = A.Db.TransactionManager.StartTransaction();
            {
                _ = new UserInput();

                //select sampleline
                ObjectId samplelineId = UserInput.GSampleLineId("\n Chọn 1 nhóm cọc: \n");
                SampleLine? sampleLine = tr.GetObject(samplelineId, OpenMode.ForRead) as SampleLine;
#pragma warning disable CS8601 // Possible null reference assignment.
                sampleLineOut = sampleLine;
#pragma warning restore CS8601 // Possible null reference assignment.
                //get samplelineGroup
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                ObjectId sampleLineGroupId = sampleLine.GroupId;
#pragma warning restore CS8602 // Dereference of apossibly null reference.
                SampleLineGroup? sampleLineGroup = tr.GetObject(sampleLineGroupId, OpenMode.ForRead) as SampleLineGroup;

                //get number of station in samplelineGroup
                int i = 0;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                foreach (ObjectId sampleLineId in sampleLineGroup.GetSampleLineIds())
                {
                    i++;
                }
#pragma warning restore CS8602 // Dereference of apossibly null reference.

                // get station
                station = new double[i];
                int j = 0;
                foreach (ObjectId sampleLineId in sampleLineGroup.GetSampleLineIds())
                {
                    SampleLine? sampleline = tr.GetObject(sampleLineId, OpenMode.ForRead) as SampleLine;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    station[j] = sampleline.Station;
#pragma warning restore CS8602 // Dereference of apossibly null reference.
                    j++;
                }
                tr.Commit();
            }
        }
        public static void SetFrequencySection(BaselineRegion baselineRegion, int frequencyDistance)
        {
            baselineRegion.AppliedAssemblySetting.FrequencyAlongCurves = frequencyDistance;
            baselineRegion.AppliedAssemblySetting.FrequencyAlongProfileCurves = frequencyDistance;
            baselineRegion.AppliedAssemblySetting.FrequencyAlongSpirals = frequencyDistance;
            baselineRegion.AppliedAssemblySetting.FrequencyAlongTangents = frequencyDistance;
            baselineRegion.AppliedAssemblySetting.FrequencyAlongTargetCurves = frequencyDistance;
            baselineRegion.AppliedAssemblySetting.AppliedAdjacentToOffsetTargetStartEnd = true;
            baselineRegion.AppliedAssemblySetting.AppliedAtHorizontalGeometryPoints = true;
            baselineRegion.AppliedAssemblySetting.AppliedAtOffsetTargetGeometryPoints = true;
            baselineRegion.AppliedAssemblySetting.AppliedAtProfileGeometryPoints = true;
            baselineRegion.AppliedAssemblySetting.AppliedAtProfileHighLowPoints = true;
            baselineRegion.AppliedAssemblySetting.AppliedAtSuperelevationCriticalPoints = true;
        }
        public static void MergelectionSet()
        {
            //UserInput UI = new UserInput();
            // Request for objects to be selected in the drawing area
            PromptSelectionResult acSSPrompt;
            acSSPrompt = A.Ed.GetSelection();

            SelectionSet acSSet1;
            ObjectIdCollection acObjIdColl = [];

            // If the prompt status is OK, objects were selected
            if (acSSPrompt.Status == PromptStatus.OK)
            {
                // Get the selected objects
                acSSet1 = acSSPrompt.Value;

                // Append the selected objects to the ObjectIdCollection
                acObjIdColl = [.. acSSet1.GetObjectIds()];
            }

            // Request for objects to be selected in the drawing area
            acSSPrompt = A.Ed.GetSelection();

            SelectionSet acSSet2;

            // If the prompt status is OK, objects were selected
            if (acSSPrompt.Status == PromptStatus.OK)
            {
                acSSet2 = acSSPrompt.Value;

                // Check the size of the ObjectIdCollection, if zero, then initialize it
                if (acObjIdColl.Count == 0)
                {
                    acObjIdColl = [.. acSSet2.GetObjectIds()];
                }
                else
                {
                    // Step through the second selection set
                    foreach (ObjectId acObjId in acSSet2.GetObjectIds())
                    {
                        // Add each object id to the ObjectIdCollection
                        acObjIdColl.Add(acObjId);
                    }
                }
            }

            Autodesk.AutoCAD.ApplicationServices.Core.Application.ShowAlertDialog("Number of objects selected: " + acObjIdColl.Count.ToString());
        }
        public static void CheckUDPforCogoPoint(string ClassificationName)
        {
            // Check if classification already exists. If not, create it.
            UDPClassification? sampleClassification;
            if (A.Cdoc.PointUDPClassifications.Contains(ClassificationName))
            {
                sampleClassification = A.Cdoc.PointUDPClassifications[ClassificationName];
                // sampleClassification = _civildoc.PointUDPClassifications["Inexistent"]; // Throws exception.
            }
            else
            {
                sampleClassification = A.Cdoc.PointUDPClassifications.Add(ClassificationName);
                // sampleClassification = _civildoc.PointUDPClassifications.Add("Existent"); // Throws exception.
            }

            // Create new UDP//
            AttributeTypeInfoInt typeInfoInt = new("Sample_Int_UDP")
            {
                Description = "Sample integer User Defined Property",
                DefaultValue = 15,
                LowerBoundValue = 0,
                UpperBoundValue = 100,
                LowerBoundInclusive = true,
                UpperBoundInclusive = false
            };
            _ = sampleClassification.CreateUDP(typeInfoInt);
        }

        public static void TaoCooridorDuongDoThiWithSurfaceTargets(Alignment alignment, Corridor corridor,
            Alignment alignment1, Alignment alignment2, Polyline polyline, ObjectId assemblyId,
            string assemblyName, ObjectIdCollection surfaceTargets)
        {
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                // Get station from alignment
                double[] station = new double[alignment.Entities.Count];
                for (int i = 0; i < alignment.Entities.Count; i++)
                {
                    AlignmentEntity alignmentEntity = alignment.Entities.GetEntityByOrder(i);
                    switch (alignmentEntity.EntityType)
                    {
                        case AlignmentEntityType.Line:
                            {
                                AlignmentLine? alignmentLine = alignmentEntity as AlignmentLine;
#pragma warning disable CS8602
                                station[i] = alignmentLine.Length;
#pragma warning restore CS8602
                                break;
                            }
                        case AlignmentEntityType.Arc:
                            {
                                AlignmentArc? alignmentArc = alignmentEntity as AlignmentArc;
#pragma warning disable CS8602
                                station[i] = alignmentArc.Length;
#pragma warning restore CS8602
                                break;
                            }
                    }
                }
                double startStation = station[0];
                double endStation = station[0] + station[1];

                // Set start and end point for corridor region
                if (alignment.GetProfileIds().Count == 0)
                {
                    A.Ed.WriteMessage($"\nLỗi: Alignment '{alignment.Name}' không có profile.");
                    return;
                }

                ObjectId profileId = alignment.GetProfileIds()[0];
                Profile? profile = tr.GetObject(profileId, OpenMode.ForRead) as Profile;

                // Check baseline exist
#pragma warning disable CS8602
                string baselineName = "BL-" + alignment.Name + "-" + profile.Name;
#pragma warning restore CS8602

                foreach (Baseline BL in corridor.Baselines)
                {
                    if (BL.Name == baselineName)
                    {
                        corridor.Baselines.Remove(corridor.Baselines[baselineName]);
                        break;
                    }
                }

                // Add baseline
                Baseline baselineAdd = corridor.Baselines.Add(baselineName, alignment.Id, profileId);

                A.Ed.WriteMessage($"\n  ℹ️ Sử dụng assembly: {assemblyName}");

                string regionName = "RG-" + alignment.Name + "-" + startStation.ToString("F2") + "-" + endStation.ToString("F2");
                BaselineRegion baselineRegion = baselineAdd.BaselineRegions.Add(regionName, assemblyId, startStation, endStation);

                // Set frequency for assembly
                SetFrequencySection(baselineRegion, 2);

                // Verify target alignments have profiles
                ObjectId profileId_1 = alignment1.GetProfileIds().Count > 0 ? alignment1.GetProfileIds()[0] : ObjectId.Null;
                ObjectId profileId_2 = alignment2.GetProfileIds().Count > 0 ? alignment2.GetProfileIds()[0] : ObjectId.Null;

                // Prepare target collections
                ObjectIdCollection alignmentTargets = new ObjectIdCollection();
                alignmentTargets.Add(alignment1.Id);
                alignmentTargets.Add(alignment2.Id);

                ObjectIdCollection profileTargets = new ObjectIdCollection();
                if (profileId_1 != ObjectId.Null) profileTargets.Add(profileId_1);
                if (profileId_2 != ObjectId.Null) profileTargets.Add(profileId_2);

                ObjectIdCollection polylineTargets = new ObjectIdCollection();
                polylineTargets.Add(polyline.Id);

                A.Ed.WriteMessage($"\n  ℹ️ Targets khả dụng:");
                A.Ed.WriteMessage($"\n    • Alignments: {alignmentTargets.Count}");
                A.Ed.WriteMessage($"\n    • Profiles: {profileTargets.Count}");
                A.Ed.WriteMessage($"\n    • Surfaces: {surfaceTargets.Count}");
                A.Ed.WriteMessage($"\n    • Polylines: {polylineTargets.Count}");

                // Apply intelligent target configuration
                A.Ed.WriteMessage($"\n  ℹ️ Áp dụng cấu hình targets...");
                ApplyIntelligentTargetConfiguration(baselineRegion, alignmentTargets, profileTargets,
                    surfaceTargets, polylineTargets);

                A.Ed.WriteMessage($"\n  ✅ Đã tạo corridor region '{regionName}'");
                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage($"\n  ❌ Lỗi AutoCAD: {e.Message}");
                tr.Abort();
                throw;
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n  ❌ Lỗi: {ex.Message}");
                tr.Abort();
                throw;
            }
        }

        /// <summary>
        /// Gets available surfaces in the current drawing
        /// </summary>
        private static ObjectIdCollection GetAvailableSurfaces()
        {
            ObjectIdCollection surfaceIds = new ObjectIdCollection();

            try
            {
                using (Transaction tr = A.Db.TransactionManager.StartTransaction())
                {
                    // Get all surface IDs from Civil 3D document
                    var allSurfaceIds = A.Cdoc.GetSurfaceIds();

                    foreach (ObjectId surfaceId in allSurfaceIds)
                    {
                        try
                        {
                            CivSurface? surface = tr.GetObject(surfaceId, OpenMode.ForRead) as CivSurface;
                            if (surface != null && !surface.IsErased)
                            {
                                surfaceIds.Add(surfaceId);
                            }
                        }
                        catch (System.Exception ex)
                        {
                            A.Ed.WriteMessage($"\nLỗi khi đọc surface {surfaceId}: {ex.Message}");
                        }
                    }

                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi khi lấy danh sách surfaces: {ex.Message}");
            }

            return surfaceIds;
        }

        /// <summary>
        /// Applies minimal target configuration as last resort
        /// Ensures each SubassemblyTargetInfo gets exactly 2 targets (duplicated if necessary)
        /// </summary>
        private static void ApplyMinimalTargetConfiguration(BaselineRegion baselineRegion,
            ObjectIdCollection TagetIds_0, ObjectIdCollection TagetIds_1, ObjectIdCollection TagetIds_2, ObjectIdCollection TagetIds_3)
        {
            try
            {
                var targetInfoCollection = baselineRegion.GetTargets();
                A.Ed.WriteMessage($"\n=== Phương pháp Fallback - Cấu hình tối thiểu ===");

                // Build a master list of all available targets (combining all groups)
                List<ObjectId> allAvailableTargets = new List<ObjectId>();

                // Add all targets from all groups in priority order
                foreach (ObjectId oid in TagetIds_1) allAvailableTargets.Add(oid); // Profiles first
                foreach (ObjectId oid in TagetIds_0) allAvailableTargets.Add(oid); // Then Alignments
                foreach (ObjectId oid in TagetIds_2) allAvailableTargets.Add(oid); // Then Surfaces
                foreach (ObjectId oid in TagetIds_3) allAvailableTargets.Add(oid); // Finally Others

                if (allAvailableTargets.Count == 0)
                {
                    A.Ed.WriteMessage($"\nℹ️ Không có target nào khả dụng. Bỏ qua việc thiết lập targets.");
                    return;
                }

                A.Ed.WriteMessage($"Fallback với {allAvailableTargets.Count} targets khả dụng");

                int successfulTargets = 0;

                for (int i = 0; i < targetInfoCollection.Count; i++)
                {
                    var targetInfo = targetInfoCollection[i];

                    try
                    {
                        A.Ed.WriteMessage($"\n--- Fallback Target {i} ---");

                        // Clear and add targets FIRST
                        targetInfo.TargetIds.Clear();

                        if (allAvailableTargets.Count >= 2)
                        {
                            // Use round-robin to distribute targets
                            int firstIndex = i % allAvailableTargets.Count;
                            int secondIndex = (i + 1) % allAvailableTargets.Count;

                            // Ensure we use different targets if possible
                            if (firstIndex == secondIndex && allAvailableTargets.Count > 1)
                            {
                                secondIndex = (firstIndex + 1) % allAvailableTargets.Count;
                            }

                            targetInfo.TargetIds.Add(allAvailableTargets[firstIndex]);
                            targetInfo.TargetIds.Add(allAvailableTargets[secondIndex]);
                            A.Ed.WriteMessage($"  ✅ Đã thêm 2 targets (chỉ số {firstIndex}, {secondIndex})");
                        }
                        else
                        {
                            // Only one target available - duplicate it
                            ObjectId singleTarget = allAvailableTargets[0];
                            targetInfo.TargetIds.Add(singleTarget);
                            targetInfo.TargetIds.Add(singleTarget); // Duplicate
                            A.Ed.WriteMessage($"  ✅ Đã thêm 1 target (nhân bản)");
                        }

                        // Set properties AFTER adding targets
                        try
                        {
                            targetInfo.TargetToOption = SubassemblyTargetToOption.Nearest;
                        }
                        catch
                        {
                            // Ignore property setting errors in fallback
                        }

                        successfulTargets++;
                    }
                    catch (System.Exception targetEx)
                    {
                        A.Ed.WriteMessage($"\n  ❌ Fallback Target {i}: {targetEx.Message}");
                    }
                }

                // Try to apply the minimal configuration
                baselineRegion.SetTargets(targetInfoCollection);
                A.Ed.WriteMessage($"\n✅ Đã áp dụng cấu hình tối thiểu: {successfulTargets}/{targetInfoCollection.Count} targets.");
            }
            catch (System.Exception minimalException)
            {
                A.Ed.WriteMessage($"\n❌ Cấu hình tối thiểu thất bại: {minimalException.Message}");
                A.Ed.WriteMessage($"\nℹ️ Tiếp tục mà không có target assignments (corridor vẫn được tạo).");
            }
        }

        // Add missing methods that are used by other files
        public static void SetDefaultPointSetting(string stylePoint, string stylelabel)
        {
            Transaction tr = A.Db.TransactionManager.StartTransaction();
            {
                _ = new UserInput();
                _ = new UtilitiesCAD();
                _ = new UtilitiesC3D();
                //start here

                // get the SettingsPoint object
                SettingsPoint pointSettings = A.Cdoc.Settings.GetSettings<SettingsPoint>() as SettingsPoint;

                // now set the value for default Style and Label Style
                // make sure the values exists in DWG file before you try to set them
                pointSettings.Styles.Point.Value = stylePoint;
                pointSettings.Styles.PointLabel.Value = stylelabel;

                tr.Commit();
            }
        }

        public static void AddSectionBand(SectionView sectionView, string SectionViewSectionDataBandStyles, int orderBand, ObjectId Section1Id, ObjectId Section2Id, double Gap, double Weeding)
        {
            SectionViewBandItemCollection bottomBandItems = sectionView.Bands.GetBottomBandItems();
            ObjectId bandStyleId = A.Cdoc.Styles.BandStyles.SectionViewSectionDataBandStyles[SectionViewSectionDataBandStyles];
            bottomBandItems.Add(bandStyleId);
            bottomBandItems[orderBand].Gap = Gap;
            bottomBandItems[orderBand].Section1Id = Section1Id;
            bottomBandItems[orderBand].Section2Id = Section2Id;
            bottomBandItems[orderBand].Weeding = Weeding;
            sectionView.Bands.SetBottomBandItems(bottomBandItems);
        }

        public static void RemoveSectionBand(SectionView sectionView, string SectionViewSectionDataBandStyles)
        {
            SectionViewBandItemCollection bottomBandItems = sectionView.Bands.GetBottomBandItems();
            ObjectId bandStyleId = A.Cdoc.Styles.BandStyles.SectionViewSectionDataBandStyles[SectionViewSectionDataBandStyles];
            for (int i = 0; i < bottomBandItems.Count; i++)
            {
                if (bottomBandItems[i].BandStyleId == bandStyleId)
                {
                    bottomBandItems.RemoveAt(i);
                }
            }
            sectionView.Bands.SetBottomBandItems(bottomBandItems);
        }

        public static void GCoordinatePointFromAlignment(Alignment alignment, int sampleLineGroupIndex, out string[] samplelineName, out string[] eastings, out string[] northings, out string[] stations)
        {
            Transaction tr = A.Db.TransactionManager.StartTransaction();

            //get samplelineGroup
            ObjectId sampleLineGroupId = alignment.GetSampleLineGroupIds()[sampleLineGroupIndex];
            SampleLineGroup? sampleLineGroup = tr.GetObject(sampleLineGroupId, OpenMode.ForRead) as SampleLineGroup;

            // get coordinate and name of point
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            int i = sampleLineGroup.GetSampleLineIds().Count;
#pragma warning restore CS8602 // Dereference of apossibly null reference.
            stations = new string[i];
            _ = new string[i];
            samplelineName = new string[i];
            eastings = new string[i];
            northings = new string[i];
            int SamplelineIndex = 0;
            foreach (ObjectId sampleLineId in sampleLineGroup.GetSampleLineIds())
            {
                SampleLine? sampleline = tr.GetObject(sampleLineId, OpenMode.ForWrite) as SampleLine;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                double station = sampleline.Station;
#pragma warning restore CS8602 // Dereference of apossibly null reference.

                double easting = 0;
                double northing = 0;
                alignment.PointLocation(station, 0, ref easting, ref northing);
                samplelineName[SamplelineIndex] = sampleline.Name;
                eastings[SamplelineIndex] = Convert.ToString(Math.Round(easting, 2));
                northings[SamplelineIndex] = Convert.ToString(Math.Round(northing, 2));
                stations[SamplelineIndex] = Convert.ToString(Math.Round(station, 2));
                SamplelineIndex++;
            }
            tr.Commit();
        }

        public static double FindY(SectionPointCollection sectionPoints, double x, double X, double Y, double Z)
        {

            double y = 0;
            for (int i = 0; i < sectionPoints.Count - 1; i++)
            {
                double x1 = sectionPoints[i].Location.X + X;
                double x2 = sectionPoints[i + 1].Location.X + X;
                double y1 = sectionPoints[i].Location.Y + Y - Z;
                double y2 = sectionPoints[i + 1].Location.Y + Y - Z;

                if (x1 <= x & x <= x2)
                {
                    double at = (y2 - y1) / (x2 - x1);
                    double b = -x1 * (y2 - y1) / (x2 - x1) + y1;
                    y = at * x + b;
                }
            }
            return y;
        }

        public static PointGroup CPointGroupWithDecription(string nameGroup, string Decription)
        {
            Transaction tr = A.Db.TransactionManager.StartTransaction();
            {
                _ = new UserInput();
                _ = new UtilitiesCAD();
                _ = new UtilitiesC3D();
                //start here

                //create point group Name
                PointGroup? pointGroup_Out = null;
                PointGroupCollection pointGroupColl = A.Cdoc.PointGroups;
                if (!pointGroupColl.Contains(nameGroup))
                {
                    pointGroupColl.Add(nameGroup);
                }

                //set query
                StandardPointGroupQuery query = new()
                {
                    IncludeRawDescriptions = Decription
                };

                //set decription for point group
                foreach (ObjectId pointGroupId in pointGroupColl)
                {
                    PointGroup? pointGroup = tr.GetObject(pointGroupId, OpenMode.ForWrite) as PointGroup;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    if (pointGroup.Name == nameGroup)
                    {
                        pointGroup.SetQuery(query);
                        pointGroup_Out = pointGroup;
                    }
#pragma warning restore CS8602 // Dereference of apossibly null reference.
                }

                tr.Commit();
#pragma warning disable CS8603 // Possible null reference return.
                return pointGroup_Out;
#pragma warning restore CS8603 // Possible null reference return
            }

        }

        public static ObjectId CreateSampleline(string sampleLineName, ObjectId sampleLineGroupId, Alignment alignment, double station)
        {
            // Kiểm tra station có nằm trong phạm vi của alignment không
            if (station < alignment.StartingStation || station > alignment.EndingStation)
            {
                A.Ed.WriteMessage($"\n⚠️ Station {station:F2} nằm ngoài phạm vi alignment ({alignment.StartingStation:F2} - {alignment.EndingStation:F2}). Bỏ qua.");
                return ObjectId.Null;
            }

            using (Transaction tr = A.Db.TransactionManager.StartTransaction())
            {
                try
                {
                    // Lấy khoảng dịch 2 bên
                    Point2dCollection point2Ds = [];
                    double easting = new();
                    double northing = new();
                    alignment.PointLocation(station, -10, ref easting, ref northing);
                    Point2d point2D = new(easting, northing);
                    point2Ds.Add(point2D);
                    alignment.PointLocation(station, 10, ref easting, ref northing);
                    Point2d point2D1 = new(easting, northing);
                    point2Ds.Add(point2D1);

                    // Tạo sampleline
                    ObjectId sampleLineId = SampleLine.Create(sampleLineName, sampleLineGroupId, point2Ds);

                    tr.Commit();
                    return sampleLineId;
                }
                catch (Autodesk.Civil.PointNotOnEntityException)
                {
                    A.Ed.WriteMessage($"\n⚠️ Không thể tạo sampleline '{sampleLineName}' tại station {station:F2}. Bỏ qua.");
                    return ObjectId.Null;
                }
            }
        }

        public static void UpdateAllPointGroup()
        {
            PointGroupCollection objectIds = A.Cdoc.PointGroups;
            objectIds.UpdateAllPointGroups();
        }

        public static ObjectIdCollection GPointIdsFromPointGroup(ObjectId pointGroupId)
        {
            ObjectIdCollection pointIds = new ObjectIdCollection();
            if (!pointGroupId.IsValid || pointGroupId.IsNull) return pointIds;

            try
            {
                PointGroup? pointGroup = pointGroupId.GetObject(OpenMode.ForWrite) as PointGroup;
                if (pointGroup == null) return pointIds;

                try
                {
                    pointGroup.Update();
                }
                catch { }

                uint[] numberPoint = pointGroup.GetPointNumbers();
                CogoPointCollection cogoPointCollection = A.Cdoc.CogoPoints;
                for (int i = 0; i < numberPoint.Length; i++)
                {
                    try
                    {
                        ObjectId ptId = cogoPointCollection.GetPointByPointNumber(numberPoint[i]);
                        if (ptId.IsValid && !ptId.IsNull)
                        {
                            pointIds.Add(ptId);
                        }
                    }
                    catch { }
                }
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n Lỗi truy vấn PointGroup: {ex.Message}");
            }
            return pointIds;
        }

        public static ObjectIdCollection GetAllCogoPointIds()
        {
            ObjectIdCollection pointIds = new ObjectIdCollection();
            try
            {
                CogoPointCollection cogoPointCollection = A.Cdoc.CogoPoints;
                foreach (ObjectId ptId in cogoPointCollection)
                {
                    if (ptId.IsValid && !ptId.IsNull)
                    {
                        pointIds.Add(ptId);
                    }
                }
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n Lỗi lấy tất cả CogoPoints: {ex.Message}");
            }
            return pointIds;
        }

        /// <summary>
        /// Gets target type as string for debugging
        /// </summary>
        private static string GetTargetTypeString(SubassemblyTargetInfo targetInfo)
        {
            try
            {
                return targetInfo.TargetType.ToString();
            }
            catch
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// Show form to select surfaces for target taluy
        /// </summary>
        public static (List<ObjectId> surfaceIds, List<string> surfaceNames) SelectSurfacesForTarget()
        {
            List<ObjectId> selectedSurfaceIds = new List<ObjectId>();
            List<string> selectedSurfaceNames = new List<string>();

            try
            {
                A.Ed.WriteMessage("\n=== CHỌN SURFACES CHO TARGET TALUY ===");

                using (Transaction tr = A.Db.TransactionManager.StartTransaction())
                {
                    var surfaceIds = A.Cdoc.GetSurfaceIds();

                    if (surfaceIds.Count == 0)
                    {
                        A.Ed.WriteMessage("\nℹ️ Không tìm thấy surface nào trong bản vẽ.");
                        return (selectedSurfaceIds, selectedSurfaceNames);
                    }

                    A.Ed.WriteMessage($"\nCó {surfaceIds.Count} surfaces trong bản vẽ:");

                    List<CivSurface> availableSurfaces = new List<CivSurface>();
                    int index = 1;

                    foreach (ObjectId surfaceId in surfaceIds)
                    {
                        CivSurface? surface = tr.GetObject(surfaceId, OpenMode.ForRead) as CivSurface;
                        if (surface != null && !surface.IsErased)
                        {
                            A.Ed.WriteMessage($"\n  {index}. {surface.Name}");
                            availableSurfaces.Add(surface);
                            index++;
                        }
                    }

                    if (availableSurfaces.Count == 0)
                    {
                        A.Ed.WriteMessage("\nℹ️ Không có surface hợp lệ.");
                        return (selectedSurfaceIds, selectedSurfaceNames);
                    }

                    // Ask user to select surfaces
                    A.Ed.WriteMessage("\n\nChọn surfaces (nhập số, cách nhau bằng dấu phẩy hoặc khoảng trắng):");
                    A.Ed.WriteMessage("\nVí dụ: 1,2,3 hoặc 1 2 3");
                    A.Ed.WriteMessage("\nNhập 'all' để chọn tất cả, hoặc Enter để bỏ qua: ");

                    string? input = UserInput.GString("\nLựa chọn của bạn");

                    if (string.IsNullOrWhiteSpace(input))
                    {
                        A.Ed.WriteMessage("\nℹ️ Không chọn surface nào.");
                        return (selectedSurfaceIds, selectedSurfaceNames);
                    }

                    if (input.ToLower() == "all")
                    {
                        // Select all surfaces
                        foreach (var surface in availableSurfaces)
                        {
                            selectedSurfaceIds.Add(surface.ObjectId);
                            selectedSurfaceNames.Add(surface.Name);
                        }
                        A.Ed.WriteMessage($"\n✅ Đã chọn tất cả {selectedSurfaceIds.Count} surfaces.");
                    }
                    else
                    {
                        // Parse user input
                        string[] parts = input.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);

                        foreach (string part in parts)
                        {
                            if (int.TryParse(part.Trim(), out int selectedIndex))
                            {
                                if (selectedIndex >= 1 && selectedIndex <= availableSurfaces.Count)
                                {
                                    var surface = availableSurfaces[selectedIndex - 1];
                                    if (!selectedSurfaceIds.Contains(surface.ObjectId))
                                    {
                                        selectedSurfaceIds.Add(surface.ObjectId);
                                        selectedSurfaceNames.Add(surface.Name);
                                        A.Ed.WriteMessage($"\n  ✅ Đã chọn: {surface.Name}");
                                    }
                                }
                                else
                                {
                                    A.Ed.WriteMessage($"\n  ⚠️ Số {selectedIndex} không hợp lệ (phải từ 1 đến {availableSurfaces.Count})");
                                }
                            }
                        }
                    }

                    if (selectedSurfaceIds.Count > 0)
                    {
                        A.Ed.WriteMessage($"\n\n✅ Tổng cộng đã chọn {selectedSurfaceIds.Count} surface(s):");
                        foreach (string name in selectedSurfaceNames)
                        {
                            A.Ed.WriteMessage($"\n  - {name}");
                        }
                    }
                    else
                    {
                        A.Ed.WriteMessage("\nℹ️ Không có surface nào được chọn.");
                    }

                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n❌ Lỗi khi chọn surfaces: {ex.Message}");
            }

            return (selectedSurfaceIds, selectedSurfaceNames);
        }

        /// <summary>
        /// Configure targets for subassemblies with detailed settings
        /// </summary>
        public static void ConfigureSubassemblyTargets(BaselineRegion baselineRegion,
            ObjectIdCollection alignmentTargets,
            ObjectIdCollection profileTargets,
            ObjectIdCollection surfaceTargets,
            ObjectIdCollection polylineTargets)
        {
            try
            {
                var targetInfoCollection = baselineRegion.GetTargets();

                if (targetInfoCollection.Count == 0)
                {
                    A.Ed.WriteMessage("\nℹ️ Không có subassembly targets để cấu hình.");
                    return;
                }

                A.Ed.WriteMessage($"\n=== CẤU HÌNH CHI TIẾT TARGETS CHO SUBASSEMBLIES ===");
                A.Ed.WriteMessage($"\nSố lượng subassemblies: {targetInfoCollection.Count}");

                A.Ed.WriteMessage($"\nTargets khả dụng:");
                A.Ed.WriteMessage($"  - Alignments: {alignmentTargets.Count}");
                A.Ed.WriteMessage($"  - Profiles: {profileTargets.Count}");
                A.Ed.WriteMessage($"  - Surfaces: {surfaceTargets.Count}");
                A.Ed.WriteMessage($"  - Polylines/Other: {polylineTargets.Count}");

                // Display all subassemblies with their target types
                A.Ed.WriteMessage($"\n\n--- DANH SÁCH SUBASSEMBLIES ---");

                using (Transaction tr = A.Db.TransactionManager.StartTransaction())
                {
                    for (int i = 0; i < targetInfoCollection.Count; i++)
                    {
                        var targetInfo = targetInfoCollection[i];

                        try
                        {
                            string targetType = targetInfo.TargetType.ToString();
                            A.Ed.WriteMessage($"\n{i + 1}. Target Type: {targetType}");
                            A.Ed.WriteMessage($"    Current targets: {targetInfo.TargetIds.Count}");

                            // Suggest appropriate target type
                            string suggestion = "";
                            if (targetType.Contains("Elevation"))
                            {
                                suggestion = "💡 Nên dùng: Profiles";
                            }
                            else if (targetType.Contains("Offset"))
                            {
                                suggestion = "💡 Nên dùng: Alignments";
                            }
                            else if (targetType.Contains("Surface"))
                            {
                                suggestion = "💡 Nên dùng: Surfaces";
                            }

                            if (!string.IsNullOrEmpty(suggestion))
                            {
                                A.Ed.WriteMessage($"    {suggestion}");
                            }
                        }
                        catch (System.Exception ex)
                        {
                            A.Ed.WriteMessage($"\n{i + 1}. [Không xác định được loại target: {ex.Message}]");
                        }
                    }

                    tr.Commit();
                }

                // Ask if user wants auto-configuration or manual
                A.Ed.WriteMessage($"\n\n--- TÙY CHỌN CẤU HÌNH ---");
                A.Ed.WriteMessage($"\n1. Tự động (Auto): Tự động gán targets phù hợp");
                A.Ed.WriteMessage($"\n2. Thủ công (Manual): Chọn từng target cho từng subassembly");

                string? configChoice = UserInput.GString("\nChọn phương thức (1/2, mặc định=1)");

                bool useAutoConfig = string.IsNullOrWhiteSpace(configChoice) || configChoice == "1";

                if (useAutoConfig)
                {
                    A.Ed.WriteMessage($"\nℹ️ Sử dụng cấu hình tự động...");
                    ApplyIntelligentTargetConfiguration(baselineRegion, alignmentTargets, profileTargets,
                        surfaceTargets, polylineTargets);
                }
                else
                {
                    A.Ed.WriteMessage($"\nℹ️ Cấu hình thủ công...");
                    ConfigureTargetsManually(targetInfoCollection, alignmentTargets, profileTargets,
                        surfaceTargets, polylineTargets);

                    // Apply configuration
                    baselineRegion.SetTargets(targetInfoCollection);
                    A.Ed.WriteMessage($"\n✅ Đã áp dụng cấu hình thủ công.");
                }
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n❌ Lỗi khi cấu hình targets: {ex.Message}");
            }
        }

        /// <summary>
        /// Apply intelligent target configuration based on subassembly types
        /// </summary>
        public static void ApplyIntelligentTargetConfiguration(BaselineRegion baselineRegion,
            ObjectIdCollection alignmentTargets,
            ObjectIdCollection profileTargets,
            ObjectIdCollection surfaceTargets,
            ObjectIdCollection polylineTargets)
        {
            try
            {
                var targetInfoCollection = baselineRegion.GetTargets();
                int successCount = 0;
                int skipCount = 0;

                for (int i = 0; i < targetInfoCollection.Count; i++)
                {
                    var targetInfo = targetInfoCollection[i];

                    try
                    {
                        string targetType = targetInfo.TargetType.ToString();
                        ObjectIdCollection appropriateTargets = null;
                        string targetDescription = "";

                        // Match target type to appropriate collection
                        if (targetType.Contains("Elevation"))
                        {
                            appropriateTargets = profileTargets;
                            targetDescription = "Profiles";
                        }
                        else if (targetType.Contains("Offset"))
                        {
                            appropriateTargets = alignmentTargets;
                            targetDescription = "Alignments";
                        }
                        else if (targetType.Contains("Surface"))
                        {
                            appropriateTargets = surfaceTargets;
                            targetDescription = "Surfaces";
                        }
                        else
                        {
                            // Fallback priority: polylines > alignments > profiles > surfaces
                            if (polylineTargets.Count > 0)
                            {
                                appropriateTargets = polylineTargets;
                                targetDescription = "Polylines";
                            }
                            else if (alignmentTargets.Count > 0)
                            {
                                appropriateTargets = alignmentTargets;
                                targetDescription = "Alignments (fallback)";
                            }
                            else if (profileTargets.Count > 0)
                            {
                                appropriateTargets = profileTargets;
                                targetDescription = "Profiles (fallback)";
                            }
                            else if (surfaceTargets.Count > 0)
                            {
                                appropriateTargets = surfaceTargets;
                                targetDescription = "Surfaces (fallback)";
                            }
                        }

                        if (appropriateTargets == null || appropriateTargets.Count == 0)
                        {
                            A.Ed.WriteMessage($"\n  ⚠️ Subassembly {i + 1}: Không có targets phù hợp");
                            skipCount++;
                            continue;
                        }

                        // Assign targets
                        targetInfo.TargetIds.Clear();

                        if (appropriateTargets.Count >= 2)
                        {
                            targetInfo.TargetIds.Add(appropriateTargets[0]);
                            targetInfo.TargetIds.Add(appropriateTargets[1]);
                        }
                        else
                        {
                            targetInfo.TargetIds.Add(appropriateTargets[0]);
                            targetInfo.TargetIds.Add(appropriateTargets[0]); // Duplicate
                        }

                        targetInfo.TargetToOption = SubassemblyTargetToOption.Nearest;

                        A.Ed.WriteMessage($"\n  ℹ️ Subassembly {i + 1}: {targetDescription} ({targetInfo.TargetIds.Count} targets)");
                        successCount++;
                    }
                    catch (System.Exception ex)
                    {
                        A.Ed.WriteMessage($"\n  ❌ Subassembly {i + 1}: {ex.Message}");
                        skipCount++;
                    }
                }

                // Apply configuration
                baselineRegion.SetTargets(targetInfoCollection);

                A.Ed.WriteMessage($"\n\n=== KẾT QUẢ CẤU HÌNH ===");
                A.Ed.WriteMessage($"✅ Thành công: {successCount}/{targetInfoCollection.Count}");
                if (skipCount > 0)
                {
                    A.Ed.WriteMessage($"⚠️ Bỏ qua: {skipCount}/{targetInfoCollection.Count}");
                }
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n❌ Lỗi: {ex.Message}");
            }
        }

        /// <summary>
        /// Configure targets manually for each subassembly
        /// </summary>
        private static void ConfigureTargetsManually(SubassemblyTargetInfoCollection targetInfoCollection,
            ObjectIdCollection alignmentTargets,
            ObjectIdCollection profileTargets,
            ObjectIdCollection surfaceTargets,
            ObjectIdCollection polylineTargets)
        {
            A.Ed.WriteMessage($"\n\n=== CẤU HÌNH THỦ CÔNG ===");
            A.Ed.WriteMessage($"\nHướng dẫn:");
            A.Ed.WriteMessage($"\n  - Nhập 'A' hoặc '0' để dùng Alignment targets");
            A.Ed.WriteMessage($"\n  - Nhập 'P' hoặc '1' để dùng Profile targets");
            A.Ed.WriteMessage($"\n  - Nhập 'S' hoặc '2' để dùng Surface targets");
            A.Ed.WriteMessage($"\n  - Nhập 'L' hoặc '3' để dùng Polyline targets");
            A.Ed.WriteMessage($"\n  - Nhập 'skip' để bỏ qua subassembly này");
            A.Ed.WriteMessage($"\n  - Nhập 'auto' để để hệ thống tự động chọn\n");

            for (int i = 0; i < targetInfoCollection.Count; i++)
            {
                var targetInfo = targetInfoCollection[i];

                try
                {
                    string targetType = "";
                    try
                    {
                        targetType = targetInfo.TargetType.ToString();
                    }
                    catch
                    {
                        targetType = "Unknown";
                    }

                    A.Ed.WriteMessage($"\n--- Subassembly {i + 1}/{targetInfoCollection.Count} ---");
                    A.Ed.WriteMessage($"\nTarget Type: {targetType}");

                    string? choice = UserInput.GString($"\nChọn loại target cho subassembly {i + 1}");

                    if (string.IsNullOrWhiteSpace(choice) || choice.ToLower() == "skip")
                    {
                        A.Ed.WriteMessage($"  ℹ️ Bỏ qua");
                        continue;
                    }

                    ObjectIdCollection selectedTargets = null;
                    string description = "";

                    choice = choice.ToUpper();

                    if (choice == "A" || choice == "0")
                    {
                        selectedTargets = alignmentTargets;
                        description = "Alignments";
                    }
                    else if (choice == "P" || choice == "1")
                    {
                        selectedTargets = profileTargets;
                        description = "Profiles";
                    }
                    else if (choice == "S" || choice == "2")
                    {
                        selectedTargets = surfaceTargets;
                        description = "Surfaces";
                    }
                    else if (choice == "L" || choice == "3")
                    {
                        selectedTargets = polylineTargets;
                        description = "Polylines";
                    }
                    else if (choice == "AUTO")
                    {
                        // Auto-select based on type
                        if (targetType.Contains("Elevation"))
                        {
                            selectedTargets = profileTargets;
                            description = "Profiles (auto)";
                        }
                        else if (targetType.Contains("Offset"))
                        {
                            selectedTargets = alignmentTargets;
                            description = "Alignments (auto)";
                        }
                        else if (targetType.Contains("Surface"))
                        {
                            selectedTargets = surfaceTargets;
                            description = "Surfaces (auto)";
                        }
                    }

                    if (selectedTargets == null || selectedTargets.Count == 0)
                    {
                        A.Ed.WriteMessage($"  ⚠️ Không có targets khả dụng cho lựa chọn này");
                        continue;
                    }

                    // Assign targets
                    targetInfo.TargetIds.Clear();

                    if (selectedTargets.Count >= 2)
                    {
                        targetInfo.TargetIds.Add(selectedTargets[0]);
                        targetInfo.TargetIds.Add(selectedTargets[1]);
                    }
                    else
                    {
                        targetInfo.TargetIds.Add(selectedTargets[0]);
                        targetInfo.TargetIds.Add(selectedTargets[0]);
                    }

                    targetInfo.TargetToOption = SubassemblyTargetToOption.Nearest;

                    A.Ed.WriteMessage($"  ✅ Đã gán {description}: {targetInfo.TargetIds.Count} targets");
                }
                catch (System.Exception ex)
                {
                    A.Ed.WriteMessage($"  ❌ Lỗi: {ex.Message}");
                }
            }
        }
    }
}

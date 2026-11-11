using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Acad = Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;

using Civil = Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using Autodesk.Civil.Runtime;
using Autodesk.Civil.Settings;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.Civil.ApplicationServices;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using MyFirstProject.Extensions;
// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3DCsharp.Corridors))]

namespace Civil3DCsharp
{
    class Corridors
    {
        [CommandMethod("CTC_Add_All_Section")]
        public static void CVC_AddAllSection()
        {
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                // Get corridor from user
                ObjectId corridorId = UserInput.GCorridorId("\n Select corridor model to add sections:\n");
                if (corridorId.IsNull)
                {
                    A.Ed.WriteMessage("\nNo corridor selected. Command cancelled.");
                    return;
                }

                var corridor = tr.GetObject(corridorId, OpenMode.ForWrite) as Corridor;
                if (corridor == null)
                {
                    A.Ed.WriteMessage("\nInvalid corridor selected. Command cancelled.");
                    return;
                }

                ProcessCorridorBaselines(corridor, tr);

                corridor.Rebuild();
                tr.Commit();
                A.Ed.WriteMessage("\nSections added successfully to corridor.");
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nError occurred: {ex.Message}");
                tr.Abort();
            }
        }

        private static void ProcessCorridorBaselines(Corridor corridor, Transaction tr)
        {
            foreach (Baseline baseline in corridor.Baselines)
            {
                var alignment = tr.GetObject(baseline.AlignmentId, OpenMode.ForRead) as Alignment;
                if (alignment == null) continue;

                var sampleLineGroupIds = alignment.GetSampleLineGroupIds();
                A.Ok($"Number of sample line groups on alignment: {sampleLineGroupIds.Count}");

                if (sampleLineGroupIds.Count == 0)
                {
                    A.Ed.WriteMessage("\nNo sample line groups found on this alignment.");
                    continue;
                }

                int selectedGroupIndex = SelectSampleLineGroup(sampleLineGroupIds, alignment, tr);
                if (selectedGroupIndex < 0) continue;

                var sampleLineStations = GetSampleLineStations(sampleLineGroupIds[selectedGroupIndex], tr);
                if (sampleLineStations.Length == 0)
                {
                    A.Ed.WriteMessage("\nNo sample lines found in the selected group.");
                    continue;
                }

                AddStationsToBaselineRegions(baseline, sampleLineStations);
            }
        }

        private static int SelectSampleLineGroup(ObjectIdCollection sampleLineGroupIds, Alignment alignment, Transaction tr)
        {
            // Display available sample line groups
            for (int i = 0; i < sampleLineGroupIds.Count; i++)
            {
                var sampleLineGroup = tr.GetObject(sampleLineGroupIds[i], OpenMode.ForRead) as SampleLineGroup;
                if (sampleLineGroup != null)
                {
                    A.Ok($"Sample line group {i}: {sampleLineGroup.Name}");
                }
            }

            // If only one group, select it automatically
            if (sampleLineGroupIds.Count == 1)
            {
                return 0;
            }

            // Get user selection for multiple groups
            int selectedIndex = UserInput.GInt("Enter the index of the sample line group to add sections:");

            if (selectedIndex < 0 || selectedIndex >= sampleLineGroupIds.Count)
            {
                A.Ed.WriteMessage("\nInvalid group index selected.");
                return -1;
            }

            return selectedIndex;
        }

        private static double[] GetSampleLineStations(ObjectId sampleLineGroupId, Transaction tr)
        {
            var sampleLineGroup = tr.GetObject(sampleLineGroupId, OpenMode.ForRead) as SampleLineGroup;
            if (sampleLineGroup == null) return new double[0];

            var sampleLineIds = sampleLineGroup.GetSampleLineIds();
            var stations = new List<double>();

            foreach (ObjectId sampleLineId in sampleLineIds)
            {
                var sampleLine = tr.GetObject(sampleLineId, OpenMode.ForRead) as SampleLine;
                if (sampleLine != null)
                {
                    stations.Add(sampleLine.Station);
                }
            }

            return stations.OrderBy(s => s).ToArray();
        }

        private static void AddStationsToBaselineRegions(Baseline baseline, double[] newStations)
        {
            foreach (BaselineRegion baselineRegion in baseline.BaselineRegions)
            {

                // Get existing stations in the region
                var existingStations = new HashSet<double>(baselineRegion.SortedStations());

                // Add new stations that fall within the region and don't already exist
                foreach (double station in newStations)
                {
                    if (IsStationInRegion(station, baselineRegion) && !existingStations.Contains(station))
                    {
                        try
                        {
                            baselineRegion.AddStation(station, "AddSection");
                            A.Ed.WriteMessage($"\nAdded station {station:F3} to region {baselineRegion.StartStation:F3}-{baselineRegion.EndStation:F3}");
                        }
                        catch (System.Exception ex)
                        {
                            A.Ed.WriteMessage($"\nFailed to add station {station:F3}: {ex.Message}");
                        }
                    }
                }
            }
        }

        private static bool IsStationInRegion(double station, BaselineRegion region)
        {
            return station >= region.StartStation && station <= region.EndStation;
        }
    }
}


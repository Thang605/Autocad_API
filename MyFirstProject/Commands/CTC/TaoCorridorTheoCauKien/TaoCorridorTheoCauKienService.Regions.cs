using System;
using System.Collections.Generic;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.Civil.DatabaseServices;

using Exception = System.Exception;

namespace MyFirstProject.Commands.CTC.TaoCorridorTheoCauKien
{
    internal static partial class TaoCorridorTheoCauKienService
    {
        private static void CreateAndConfigureRegion(
            Editor ed,
            TaoCorridorTheoCauKienOptions options,
            CorridorComponentRowItem row,
            Corridor targetCorridor,
            Baseline baseline,
            string entityDisplayName)
        {
            double blStart = baseline.StartStation;
            double blEnd = baseline.EndStation;
            double regStart = row.StartStation > blStart && row.StartStation < blEnd ? row.StartStation : blStart;
            double regEnd = row.EndStation > regStart && row.EndStation <= blEnd + 0.05
                ? Math.Min(blEnd, row.EndStation)
                : blEnd;

            string regName = GetUniqueRegionName(baseline, $"RG_{row.ComponentName}_{entityDisplayName}_{regStart:F1}_{regEnd:F1}");
            BaselineRegion region = baseline.BaselineRegions.Add(regName, row.AssemblyId, regStart, regEnd);

            ApplyRegionFrequency(
                region,
                row.FrequencyTangent > 0 ? row.FrequencyTangent : options.DefaultFreqTangent,
                row.FrequencyCurve > 0 ? row.FrequencyCurve : options.DefaultFreqCurve);

            ObjectId surfaceTargetId = !row.TargetSurfaceId.IsNull ? row.TargetSurfaceId : options.DefaultTargetSurfaceId;
            ApplyComponentTargets(region, surfaceTargetId, row.OffsetElevationTargetIds, ed);

            ed.WriteMessage($"\n  ✓ Đã gộp vào Corridor '{targetCorridor.Name}': Baseline '{baseline.Name}' & Region '{region.Name}' ({regStart:F2}m → {regEnd:F2}m)");
        }

        private static void UpdateExistingRegionSettings(
            Editor ed,
            TaoCorridorTheoCauKienOptions options,
            CorridorComponentRowItem row,
            Baseline matchedBl,
            BaselineRegion matchedRg)
        {
            ApplyRegionFrequency(
                matchedRg,
                row.FrequencyTangent > 0 ? row.FrequencyTangent : options.DefaultFreqTangent,
                row.FrequencyCurve > 0 ? row.FrequencyCurve : options.DefaultFreqCurve);

            ObjectId surfTargetId = !row.TargetSurfaceId.IsNull ? row.TargetSurfaceId : options.DefaultTargetSurfaceId;
            ApplyComponentTargets(matchedRg, surfTargetId, row.OffsetElevationTargetIds, ed);
            ed.WriteMessage($"\n  ✓ Đã cập nhật Region hiện có '{matchedRg.Name}' trên Baseline '{matchedBl.Name}'");
        }

        private static bool TryFindExistingMatchingRegion(
            Corridor corridor,
            bool isFeatureLine,
            ObjectId primaryId,
            ObjectId profileId,
            ObjectId assemblyId,
            double regStart,
            double regEnd,
            out Baseline? matchedBaseline,
            out BaselineRegion? matchedRegion)
        {
            matchedBaseline = null;
            matchedRegion = null;

            foreach (Baseline bl in corridor.Baselines)
            {
                bool blIsFl = bl.IsFeatureLineBased();
                if (blIsFl != isFeatureLine) continue;

                if (isFeatureLine)
                {
                    if (bl.FeatureLineId != primaryId) continue;
                }
                else
                {
                    if (bl.AlignmentId != primaryId || bl.ProfileId != profileId) continue;
                }

                foreach (BaselineRegion rg in bl.BaselineRegions)
                {
                    if (rg.AssemblyId == assemblyId &&
                        Math.Abs(rg.StartStation - regStart) < 0.05 &&
                        Math.Abs(rg.EndStation - regEnd) < 0.05)
                    {
                        matchedBaseline = bl;
                        matchedRegion = rg;
                        return true;
                    }
                }
            }

            return false;
        }

        private static Baseline? FindReusableBaseline(
            Corridor corridor,
            bool isFeatureLine,
            ObjectId primaryId,
            ObjectId profileId,
            double reqStart,
            double reqEnd)
        {
            foreach (Baseline bl in corridor.Baselines)
            {
                bool blIsFl = bl.IsFeatureLineBased();
                if (blIsFl != isFeatureLine) continue;

                if (isFeatureLine)
                {
                    if (bl.FeatureLineId != primaryId) continue;
                }
                else
                {
                    if (bl.AlignmentId != primaryId || bl.ProfileId != profileId) continue;
                }

                bool overlaps = false;
                foreach (BaselineRegion rg in bl.BaselineRegions)
                {
                    if (reqStart < rg.EndStation - 1e-3 && reqEnd > rg.StartStation + 1e-3)
                    {
                        overlaps = true;
                        break;
                    }
                }

                if (!overlaps)
                    return bl;
            }

            return null;
        }

        private static string GetUniqueRegionName(Baseline baseline, string baseName)
        {
            var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (BaselineRegion rg in baseline.BaselineRegions)
                existingNames.Add(rg.Name);

            if (!existingNames.Contains(baseName))
                return baseName;

            int index = 2;
            while (existingNames.Contains($"{baseName}_{index}"))
                index++;

            return $"{baseName}_{index}";
        }

        private static void ApplyRegionFrequency(BaselineRegion region, double tangentFreq, double curveFreq)
        {
            try
            {
                var setting = region.AppliedAssemblySetting;
                if (setting == null) return;

                setting.FrequencyAlongTangents = Math.Max(0.5, tangentFreq);
                setting.FrequencyAlongCurves = Math.Max(0.5, curveFreq);
                setting.FrequencyAlongSpirals = Math.Max(0.5, curveFreq);
                setting.FrequencyAlongProfileCurves = Math.Max(1.0, curveFreq);
                setting.FrequencyAlongTargetCurves = Math.Max(1.0, curveFreq);
                setting.AppliedAtHorizontalGeometryPoints = true;
                setting.AppliedAtProfileGeometryPoints = true;
                setting.AppliedAtSuperelevationCriticalPoints = true;
                setting.AppliedAtOffsetTargetGeometryPoints = true;
            }
            catch { }
        }

        private static void ApplyComponentTargets(
            BaselineRegion region,
            ObjectId surfaceId,
            List<ObjectId> offsetElevationIds,
            Editor ed)
        {
            bool hasSurface = !surfaceId.IsNull && surfaceId.IsValid && !surfaceId.IsErased;
            bool hasOffsets = offsetElevationIds != null && offsetElevationIds.Count > 0;
            if (!hasSurface && !hasOffsets) return;

            try
            {
                SubassemblyTargetInfoCollection targets = region.GetTargets();
                if (targets == null || targets.Count == 0) return;

                bool modified = false;

                foreach (SubassemblyTargetInfo ti in targets)
                {
                    string targetTypeStr = ti.TargetType.ToString();

                    if (hasSurface && targetTypeStr.Contains("Surface", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            ti.TargetIds = new ObjectIdCollection { surfaceId };
                            ti.TargetToOption = SubassemblyTargetToOption.Nearest;
                            modified = true;
                        }
                        catch { }
                    }
                    else if (hasOffsets && offsetElevationIds != null &&
                             (targetTypeStr.Contains("Offset", StringComparison.OrdinalIgnoreCase) ||
                              targetTypeStr.Contains("Width", StringComparison.OrdinalIgnoreCase) ||
                              targetTypeStr.Contains("Elevation", StringComparison.OrdinalIgnoreCase)))
                    {
                        try
                        {
                            var col = new ObjectIdCollection();
                            foreach (ObjectId id in offsetElevationIds)
                            {
                                if (!id.IsNull && id.IsValid && !id.IsErased)
                                    col.Add(id);
                            }
                            if (col.Count > 0)
                            {
                                ti.TargetIds = col;
                                ti.TargetToOption = SubassemblyTargetToOption.Nearest;
                                modified = true;
                            }
                        }
                        catch { }
                    }
                }

                if (modified)
                {
                    region.SetTargets(targets);
                }
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n    ⚠ Lưu ý khi gán Target cho Region '{region.Name}': {ex.Message}");
            }
        }
    }
}

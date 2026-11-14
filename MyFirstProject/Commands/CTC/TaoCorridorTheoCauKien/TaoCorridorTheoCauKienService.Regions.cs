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

            string regName = GetUniqueRegionName(baseline, $"RG_{entityDisplayName}_{regStart:F1}_{regEnd:F1}");
            BaselineRegion region = baseline.BaselineRegions.Add(regName, row.AssemblyId, regStart, regEnd);

            ApplyRegionFrequency(
                region,
                row.FrequencyTangent > 0 ? row.FrequencyTangent : options.DefaultFreqTangent,
                row.FrequencyCurve > 0 ? row.FrequencyCurve : options.DefaultFreqCurve);

            ObjectId surfaceTargetId = ResolveDesiredSurfaceId(options, row);
            ApplyComponentTargets(region, surfaceTargetId, row.OffsetElevationTargetIds, ed);

            ed.WriteMessage($"\n  [+] Mới thêm '{baseline.Name}' → Region '{region.Name}' ({regStart:F2}m → {regEnd:F2}m) [Assembly: {row.AssemblyName}]");
        }

        /// <summary>
        /// Cập nhật Region đã có sẵn trên Corridor tổng: TUYỆT ĐỐI KHÔNG chỉnh sửa StartStation / EndStation.
        /// Chỉ cập nhật Assembly / Target / Bước cọc khi người dùng chủ động thay đổi trên bảng.
        /// </summary>
        private static void UpdatePreExistingBaselineRegions(
            Editor ed,
            TaoCorridorTheoCauKienOptions options,
            CorridorComponentRowItem row,
            List<Baseline> existingBaselines,
            ref int keptExistingRegions)
        {
            bool anyUserEdit = row.AssemblyModifiedByUser ||
                               row.SurfaceTargetModifiedByUser ||
                               row.OffsetTargetsModifiedByUser ||
                               row.FrequencyModifiedByUser;

            foreach (Baseline bl in existingBaselines)
            {
                foreach (BaselineRegion rg in bl.BaselineRegions)
                {
                    keptExistingRegions++;
                    if (!anyUserEdit)
                    {
                        ed.WriteMessage($"\n  [=] Giữ nguyên hoàn toàn Region '{rg.Name}' ({rg.StartStation:F2}m → {rg.EndStation:F2}m).");
                        continue;
                    }

                    if (row.AssemblyModifiedByUser && bl.BaselineRegions.Count == 1 &&
                        !row.AssemblyId.IsNull && rg.AssemblyId != row.AssemblyId)
                    {
                        try
                        {
                            rg.AssemblyId = row.AssemblyId;
                            ed.WriteMessage($"\n  [~] Đổi Mặt cắt Region '{rg.Name}' → '{row.AssemblyName}' (giữ nguyên lý trình {rg.StartStation:F2}→{rg.EndStation:F2}m).");
                        }
                        catch (Exception exAsm)
                        {
                            ed.WriteMessage($"\n  ⚠ Không đổi được Assembly cho '{rg.Name}': {exAsm.Message}");
                        }
                    }

                    if (row.FrequencyModifiedByUser)
                    {
                        ApplyRegionFrequency(
                            rg,
                            row.FrequencyTangent > 0 ? row.FrequencyTangent : options.DefaultFreqTangent,
                            row.FrequencyCurve > 0 ? row.FrequencyCurve : options.DefaultFreqCurve);
                    }

                    if (row.SurfaceTargetModifiedByUser || row.OffsetTargetsModifiedByUser)
                    {
                        ApplySelectiveTargetsOnExistingRegion(rg, options, row, ed);
                    }

                    ed.WriteMessage($"\n  [=] Giữ nguyên lý trình Region '{rg.Name}' ({rg.StartStation:F2}m → {rg.EndStation:F2}m).");
                }
            }
        }

        private static ObjectId ResolveDesiredSurfaceId(TaoCorridorTheoCauKienOptions options, CorridorComponentRowItem row)
        {
            if (row.DisableSurfaceTarget)
                return ObjectId.Null;
            if (row.UseDefaultSurface)
                return options.DefaultTargetSurfaceId;
            return !row.TargetSurfaceId.IsNull ? row.TargetSurfaceId : options.DefaultTargetSurfaceId;
        }

        private static Baseline? FindNonOverlappingOrEmptyBaseline(
            Corridor corridor,
            bool isFeatureLine,
            ObjectId primaryId,
            ObjectId profileId,
            double reqStart,
            double reqEnd)
        {
            foreach (Baseline bl in corridor.Baselines)
            {
                if (bl.IsFeatureLineBased() != isFeatureLine) continue;

                if (isFeatureLine)
                {
                    if (bl.FeatureLineId != primaryId) continue;
                }
                else
                {
                    if (bl.AlignmentId != primaryId || bl.ProfileId != profileId) continue;
                }

                if (bl.BaselineRegions.Count == 0)
                    return bl;

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

            if (!existingNames.Contains(baseName)) return baseName;
            int index = 2;
            while (existingNames.Contains($"{baseName}_{index}")) index++;
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

            ApplyTargetsCore(region, updateSurface: hasSurface, surfaceId: surfaceId, updateOffsets: hasOffsets, offsetElevationIds: offsetElevationIds, ed: ed);
        }

        private static void ApplySelectiveTargetsOnExistingRegion(
            BaselineRegion region,
            TaoCorridorTheoCauKienOptions options,
            CorridorComponentRowItem row,
            Editor ed)
        {
            ObjectId desiredSurfaceId = ResolveDesiredSurfaceId(options, row);
            ApplyTargetsCore(
                region,
                updateSurface: row.SurfaceTargetModifiedByUser,
                surfaceId: desiredSurfaceId,
                updateOffsets: row.OffsetTargetsModifiedByUser,
                offsetElevationIds: row.OffsetElevationTargetIds,
                ed: ed);
        }

        private static void ApplyTargetsCore(
            BaselineRegion region,
            bool updateSurface,
            ObjectId surfaceId,
            bool updateOffsets,
            List<ObjectId>? offsetElevationIds,
            Editor ed)
        {
            try
            {
                SubassemblyTargetInfoCollection targets = region.GetTargets();
                if (targets == null || targets.Count == 0) return;

                bool modified = false;
                foreach (SubassemblyTargetInfo ti in targets)
                {
                    string targetTypeStr = ti.TargetType.ToString();

                    if (updateSurface && targetTypeStr.Contains("Surface", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            bool validSurf = !surfaceId.IsNull && surfaceId.IsValid && !surfaceId.IsErased;
                            ti.TargetIds = validSurf ? new ObjectIdCollection { surfaceId } : new ObjectIdCollection();
                            ti.TargetToOption = SubassemblyTargetToOption.Nearest;
                            modified = true;
                        }
                        catch { }
                    }
                    else if (updateOffsets &&
                             (targetTypeStr.Contains("Offset", StringComparison.OrdinalIgnoreCase) ||
                              targetTypeStr.Contains("Width", StringComparison.OrdinalIgnoreCase) ||
                              targetTypeStr.Contains("Elevation", StringComparison.OrdinalIgnoreCase)))
                    {
                        try
                        {
                            var col = new ObjectIdCollection();
                            if (offsetElevationIds != null)
                            {
                                foreach (ObjectId id in offsetElevationIds)
                                {
                                    if (!id.IsNull && id.IsValid && !id.IsErased)
                                        col.Add(id);
                                }
                            }
                            ti.TargetIds = col;
                            ti.TargetToOption = SubassemblyTargetToOption.Nearest;
                            modified = true;
                        }
                        catch { }
                    }
                }

                if (modified)
                    region.SetTargets(targets);
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n    ⚠ Lưu ý khi gán Target cho Region '{region.Name}': {ex.Message}");
            }
        }
    }
}

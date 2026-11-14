using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;

using MyFirstProject.Shared.UI;

namespace MyFirstProject.Commands.CTC.TaoCorridorTheoCauKien
{
    public partial class TaoCorridorTheoCauKienForm
    {
        private SiteCatalogItem? GetSelectedSiteItem()
        {
            int idx = cmbSite.SelectedIndex - 1;
            return idx >= 0 && idx < _siteList.Count ? _siteList[idx] : null;
        }

        private ObjectId GetSelectedTargetCorridorId()
        {
            int idx = cmbExistingCorridor.SelectedIndex - 1;
            return idx >= 0 && idx < _corridorList.Count ? _corridorList[idx].Id : ObjectId.Null;
        }

        private static string SafeGetStyleName(FeatureLine fl)
        {
            try { return fl.StyleName ?? ""; }
            catch { return ""; }
        }

        private void SuggestNewCorridorNameForSite(SiteCatalogItem site)
        {
            string current = txtCorridorName.Text.Trim();
            if (string.IsNullOrEmpty(current) || current.StartsWith("COR_", StringComparison.OrdinalIgnoreCase))
            {
                string raw = site.IsSiteLess ? "KhongSite" : site.SiteName.Trim();
                foreach (char bad in new[] { '<', '>', '/', '\\', ':', '"', '|', '?', '*' })
                    raw = raw.Replace(bad, '_');
                txtCorridorName.Text = $"COR_{raw}";
            }
        }

        private bool SelectSiteByRecipe(SiteCorridorRecipe recipe)
        {
            int matchIdx = -1;
            if (recipe.IsSiteLess)
            {
                matchIdx = _siteList.FindIndex(s => s.IsSiteLess);
            }
            else
            {
                if (TaoCorridorTheoCauKienService.TryResolveHandle(_db, recipe.SiteHandle, out ObjectId siteId))
                    matchIdx = _siteList.FindIndex(s => s.SiteId == siteId);
                if (matchIdx < 0 && !string.IsNullOrWhiteSpace(recipe.SiteName))
                    matchIdx = _siteList.FindIndex(s => s.SiteName.Equals(recipe.SiteName, StringComparison.OrdinalIgnoreCase));
            }

            if (matchIdx < 0) return false;

            _suppressSelectorEvents = true;
            try { cmbSite.SelectedIndex = matchIdx + 1; }
            finally { _suppressSelectorEvents = false; }
            return true;
        }

        private void ApplyRecipeDefaultsToForm(SiteCorridorRecipe recipe)
        {
            _suppressSelectorEvents = true;
            try
            {
                if (TaoCorridorTheoCauKienService.TryResolveHandle(_db, recipe.DefaultAssemblyHandle, out ObjectId defAsmId))
                {
                    int idx = _assemblyList.FindIndex(a => a.Id == defAsmId);
                    if (idx >= 0) cmbDefaultAssembly.SelectedIndex = idx;
                }

                if (string.IsNullOrEmpty(recipe.DefaultSurfaceHandle))
                {
                    if (cmbDefaultSurface.Items.Count > 0) cmbDefaultSurface.SelectedIndex = 0;
                }
                else if (TaoCorridorTheoCauKienService.TryResolveHandle(_db, recipe.DefaultSurfaceHandle, out ObjectId defSurfId))
                {
                    int idx = _surfaceList.FindIndex(s => s.Id == defSurfId);
                    if (idx >= 0) cmbDefaultSurface.SelectedIndex = idx + 1;
                }

                if (TaoCorridorTheoCauKienService.TryResolveHandle(_db, recipe.CodeSetStyleHandle, out ObjectId cssId))
                {
                    int idx = _codeSetStyleList.FindIndex(c => c.Id == cssId);
                    if (idx >= 0) cmbCodeSetStyle.SelectedIndex = idx;
                }

                if (recipe.DefaultFreqTangent >= 1.0)
                    PersistentState.SetValueSafe(numFreqTangent, (decimal)recipe.DefaultFreqTangent);
                if (recipe.DefaultFreqCurve >= 0.5)
                    PersistentState.SetValueSafe(numFreqCurve, (decimal)recipe.DefaultFreqCurve);

                chkRemoveDeletedBaselines.Checked = recipe.RemoveDeletedBaselines;
            }
            finally
            {
                _suppressSelectorEvents = false;
            }
        }

        private Dictionary<ObjectId, List<Baseline>> BuildCorridorFlBaselineLookup(
            Corridor corridor, SiteCorridorRecipe? recipe)
        {
            var lookup = new Dictionary<ObjectId, List<Baseline>>();
            var clonedToSource = new Dictionary<ObjectId, ObjectId>();

            if (recipe?.ClonedFeatureLineMap != null)
            {
                foreach (var kv in recipe.ClonedFeatureLineMap)
                {
                    if (TaoCorridorTheoCauKienService.TryResolveHandle(_db, kv.Key, out ObjectId srcId) &&
                        TaoCorridorTheoCauKienService.TryResolveHandle(_db, kv.Value, out ObjectId clnId))
                    {
                        clonedToSource[clnId] = srcId;
                    }
                }
            }

            foreach (Baseline bl in corridor.Baselines)
            {
                if (!bl.IsFeatureLineBased() || bl.BaselineRegions.Count == 0) continue;
                ObjectId flId = bl.FeatureLineId;
                if (flId.IsNull || !flId.IsValid || flId.IsErased) continue;

                AddBaselineEntry(lookup, flId, bl);
                if (clonedToSource.TryGetValue(flId, out ObjectId srcId))
                    AddBaselineEntry(lookup, srcId, bl);
            }
            return lookup;
        }

        private static void AddBaselineEntry(Dictionary<ObjectId, List<Baseline>> map, ObjectId id, Baseline bl)
        {
            if (!map.TryGetValue(id, out var list))
            {
                list = new List<Baseline>();
                map[id] = list;
            }
            list.Add(bl);
        }

        private int CountOrphanDeletedBaselines(
            Transaction tr, Corridor corridor, SiteCatalogItem site, SiteCorridorRecipe? recipe)
        {
            var currentSiteFlIds = new HashSet<ObjectId>(site.FeatureLineIds);
            var managedAndClonedIds = new HashSet<ObjectId>();

            if (recipe != null)
            {
                foreach (string h in recipe.ManagedFeatureLineHandles)
                {
                    if (TaoCorridorTheoCauKienService.TryResolveHandle(_db, h, out ObjectId id))
                        managedAndClonedIds.Add(id);
                    if (recipe.ClonedFeatureLineMap.TryGetValue(h, out string? clnHex) &&
                        TaoCorridorTheoCauKienService.TryResolveHandle(_db, clnHex, out ObjectId clnId))
                    {
                        managedAndClonedIds.Add(clnId);
                    }
                }
            }

            foreach (ObjectId flId in site.FeatureLineIds)
            {
                string h = TaoCorridorTheoCauKienService.GetHandleHex(flId);
                if (recipe != null && !string.IsNullOrEmpty(h) &&
                    recipe.ClonedFeatureLineMap.TryGetValue(h, out string? clnHex) &&
                    TaoCorridorTheoCauKienService.TryResolveHandle(_db, clnHex, out ObjectId clnId))
                {
                    currentSiteFlIds.Add(clnId);
                }
            }

            int orphanCount = 0;
            foreach (Baseline bl in corridor.Baselines)
            {
                if (!bl.IsFeatureLineBased()) continue;
                ObjectId flId = bl.FeatureLineId;
                if (flId.IsNull || !flId.IsValid || flId.IsErased)
                {
                    orphanCount++;
                    continue;
                }

                if (!currentSiteFlIds.Contains(flId))
                {
                    bool inSameSite = tr.GetObject(flId, OpenMode.ForRead) is FeatureLine fl &&
                        (site.IsSiteLess ? fl.SiteId.IsNull : fl.SiteId == site.SiteId);
                    if (inSameSite || managedAndClonedIds.Contains(flId))
                        orphanCount++;
                }
            }
            return orphanCount;
        }

        private void PopulateRowFromExistingBaselines(
            Transaction tr, CorridorComponentRowItem item, List<Baseline> baselines)
        {
            BaselineRegion? firstRg = null;
            int regionCount = 0;
            foreach (Baseline bl in baselines)
            {
                foreach (BaselineRegion rg in bl.BaselineRegions)
                {
                    if (firstRg == null) firstRg = rg;
                    regionCount++;
                }
            }
            if (firstRg == null) return;

            item.HasExistingCorridorRegion = true;
            item.ExistingRegionCount = regionCount;
            item.StartStation = firstRg.StartStation;
            item.EndStation = firstRg.EndStation;
            item.ExistingStationSummary = regionCount == 1
                ? $"Đã có 1 Region: {firstRg.StartStation:F2} → {firstRg.EndStation:F2}m (Giữ nguyên lý trình)"
                : $"Đã có {regionCount} Regions (đầu tiên: {firstRg.StartStation:F2} → {firstRg.EndStation:F2}m, giữ nguyên tất cả)";

            var asmTuple = _assemblyList.FirstOrDefault(a => a.Id == firstRg.AssemblyId);
            item.AssemblyId = !asmTuple.Id.IsNull ? asmTuple.Id : firstRg.AssemblyId;
            item.AssemblyName = !asmTuple.Id.IsNull ? asmTuple.Name : "";

            try
            {
                if (firstRg.AppliedAssemblySetting != null)
                {
                    item.FrequencyTangent = Math.Max(0.5, firstRg.AppliedAssemblySetting.FrequencyAlongTangents);
                    item.FrequencyCurve = Math.Max(0.5, firstRg.AppliedAssemblySetting.FrequencyAlongCurves);
                }
            }
            catch { }

            ExtractRegionTargets(firstRg, out ObjectId surfId, out List<ObjectId> offsetIds);
            var surfTuple = _surfaceList.FirstOrDefault(s => s.Id == surfId);
            if (!surfTuple.Id.IsNull)
            {
                item.UseDefaultSurface = false;
                item.DisableSurfaceTarget = false;
                item.TargetSurfaceId = surfTuple.Id;
                item.TargetSurfaceName = surfTuple.Name;
            }
            else
            {
                item.UseDefaultSurface = false;
                item.DisableSurfaceTarget = true;
                item.TargetSurfaceId = ObjectId.Null;
                item.TargetSurfaceName = SurfaceOptionNone;
            }

            item.OffsetElevationTargetIds = offsetIds;
            item.OffsetTargetsDisplay = offsetIds.Count > 0 ? $"[{offsetIds.Count} đối tượng]" : "(Không)";
        }

        private static Dictionary<string, StyleLayerRuleConfig> BuildLearnedRulesLookup(SiteCorridorRecipe? recipe)
        {
            var dict = new Dictionary<string, StyleLayerRuleConfig>(StringComparer.OrdinalIgnoreCase);
            if (recipe?.StyleLayerRules == null) return dict;
            foreach (var r in recipe.StyleLayerRules)
                dict[TaoCorridorTheoCauKienService.MakeStyleLayerKey(r.StyleName, r.LayerName)] = r;
            return dict;
        }

        private static void SeedRulesFromExistingRows(
            Dictionary<string, StyleLayerRuleConfig> learnedRules, List<CorridorComponentRowItem> existingRows)
        {
            foreach (var ex in existingRows)
            {
                if (ex.AssemblyId.IsNull) continue;
                string slKey = TaoCorridorTheoCauKienService.MakeStyleLayerKey(ex.FeatureLineStyleName, ex.FeatureLineLayer);
                if (learnedRules.ContainsKey(slKey)) continue;
                learnedRules[slKey] = new StyleLayerRuleConfig
                {
                    StyleName = ex.FeatureLineStyleName,
                    LayerName = ex.FeatureLineLayer,
                    AssemblyHandle = TaoCorridorTheoCauKienService.GetHandleHex(ex.AssemblyId),
                    UseDefaultSurface = ex.UseDefaultSurface,
                    DisableSurfaceTarget = ex.DisableSurfaceTarget,
                    TargetSurfaceHandle = TaoCorridorTheoCauKienService.GetHandleHex(ex.TargetSurfaceId),
                    OffsetTargetHandles = ex.OffsetElevationTargetIds.Select(TaoCorridorTheoCauKienService.GetHandleHex).Where(h => !string.IsNullOrEmpty(h)).ToList(),
                    FreqTangent = ex.FrequencyTangent,
                    FreqCurve = ex.FrequencyCurve
                };
            }
        }

        private static Dictionary<string, SavedFeatureLineConfig> BuildPerFlLookup(SiteCorridorRecipe? recipe)
        {
            var dict = new Dictionary<string, SavedFeatureLineConfig>(StringComparer.OrdinalIgnoreCase);
            if (recipe?.PerFeatureLineConfigs == null) return dict;
            foreach (var cfg in recipe.PerFeatureLineConfigs)
                if (!string.IsNullOrEmpty(cfg.FlHandle)) dict[cfg.FlHandle] = cfg;
            return dict;
        }

        private void ApplySuggestedConfigForNewFl(
            CorridorComponentRowItem item,
            string flName,
            string flHandle,
            string styleName,
            string layerName,
            Dictionary<string, SavedFeatureLineConfig> perFlMap,
            Dictionary<string, StyleLayerRuleConfig> learnedRules)
        {
            item.HasExistingCorridorRegion = false;
            item.StartStation = 0.0;
            item.EndStation = 0.0;
            item.UseDefaultSurface = true;
            item.TargetSurfaceName = SurfaceOptionUseDefault;

            if (perFlMap.TryGetValue(flHandle, out SavedFeatureLineConfig? savedCfg))
            {
                ApplySavedRuleValues(item, savedCfg.IsEnabled, savedCfg.AssemblyHandle, savedCfg.UseDefaultSurface,
                    savedCfg.DisableSurfaceTarget, savedCfg.TargetSurfaceHandle, savedCfg.OffsetTargetHandles, savedCfg.FreqTangent, savedCfg.FreqCurve);
                return;
            }

            string slKey = TaoCorridorTheoCauKienService.MakeStyleLayerKey(styleName, layerName);
            if (learnedRules.TryGetValue(slKey, out StyleLayerRuleConfig? rule) ||
                (!string.IsNullOrEmpty(styleName) && (rule = learnedRules.Values.FirstOrDefault(r => r.StyleName.Equals(styleName, StringComparison.OrdinalIgnoreCase))) != null))
            {
                ApplySavedRuleValues(item, true, rule.AssemblyHandle, rule.UseDefaultSurface,
                    rule.DisableSurfaceTarget, rule.TargetSurfaceHandle, rule.OffsetTargetHandles, rule.FreqTangent, rule.FreqCurve);
                return;
            }

            var matchedAsm = TryMatchAssemblyByKeywords(styleName, layerName, flName);
            if (!matchedAsm.Id.IsNull) { item.AssemblyId = matchedAsm.Id; item.AssemblyName = matchedAsm.Name; }
            else if (cmbDefaultAssembly.SelectedIndex >= 0 && cmbDefaultAssembly.SelectedIndex < _assemblyList.Count)
            {
                item.AssemblyId = _assemblyList[cmbDefaultAssembly.SelectedIndex].Id;
                item.AssemblyName = _assemblyList[cmbDefaultAssembly.SelectedIndex].Name;
            }
        }

        private void ApplySavedRuleValues(
            CorridorComponentRowItem item, bool isEnabled, string asmHandle, bool useDefSurf,
            bool disableSurf, string surfHandle, List<string> offsetHandles, double freqT, double freqC)
        {
            item.IsEnabled = isEnabled;

            if (TaoCorridorTheoCauKienService.TryResolveHandle(_db, asmHandle, out ObjectId asmId))
            {
                var asmTuple = _assemblyList.FirstOrDefault(a => a.Id == asmId);
                if (!asmTuple.Id.IsNull) { item.AssemblyId = asmTuple.Id; item.AssemblyName = asmTuple.Name; }
            }

            item.UseDefaultSurface = useDefSurf;
            item.DisableSurfaceTarget = disableSurf;
            if (disableSurf) { item.TargetSurfaceId = ObjectId.Null; item.TargetSurfaceName = SurfaceOptionNone; }
            else if (!useDefSurf && TaoCorridorTheoCauKienService.TryResolveHandle(_db, surfHandle, out ObjectId sId))
            {
                var sTuple = _surfaceList.FirstOrDefault(s => s.Id == sId);
                if (!sTuple.Id.IsNull) { item.TargetSurfaceId = sTuple.Id; item.TargetSurfaceName = sTuple.Name; }
            }
            else { item.UseDefaultSurface = true; item.TargetSurfaceName = SurfaceOptionUseDefault; }

            var validOffsets = new List<ObjectId>();
            foreach (string oh in offsetHandles)
                if (TaoCorridorTheoCauKienService.TryResolveHandle(_db, oh, out ObjectId oId) && !validOffsets.Contains(oId)) validOffsets.Add(oId);
            item.OffsetElevationTargetIds = validOffsets;
            item.OffsetTargetsDisplay = validOffsets.Count > 0 ? $"[{validOffsets.Count} đối tượng]" : "(Không)";

            if (freqT >= 0.5) item.FrequencyTangent = freqT;
            if (freqC >= 0.5) item.FrequencyCurve = freqC;
        }

        private (string Name, ObjectId Id) TryMatchAssemblyByKeywords(string styleName, string layerName, string flName)
        {
            string[] candidates = new[] { styleName, layerName, flName };
            foreach (string cand in candidates)
            {
                if (string.IsNullOrWhiteSpace(cand)) continue;
                var exact = _assemblyList.FirstOrDefault(a => a.Name.Equals(cand.Trim(), StringComparison.OrdinalIgnoreCase));
                if (!exact.Id.IsNull) return exact;
            }
            foreach (string cand in candidates)
            {
                if (string.IsNullOrWhiteSpace(cand) || cand.Trim().Length < 3) continue;
                var partial = _assemblyList.FirstOrDefault(a =>
                    a.Name.Contains(cand.Trim(), StringComparison.OrdinalIgnoreCase) ||
                    cand.Trim().Contains(a.Name, StringComparison.OrdinalIgnoreCase));
                if (!partial.Id.IsNull) return partial;
            }
            return default;
        }
    }
}

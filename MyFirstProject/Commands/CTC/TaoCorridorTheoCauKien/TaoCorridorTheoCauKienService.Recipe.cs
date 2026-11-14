using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;

namespace MyFirstProject.Commands.CTC.TaoCorridorTheoCauKien
{
    internal static partial class TaoCorridorTheoCauKienService
    {
        public const string RecipeDictKey = "CTC_SITE_CORRIDOR_RECIPE_V1";

        internal static string GetHandleHex(ObjectId id)
        {
            if (id.IsNull || !id.IsValid || id.IsErased)
                return "";
            return id.Handle.Value.ToString("X");
        }

        internal static bool TryResolveHandle(Database db, string? handleHex, out ObjectId id)
        {
            id = ObjectId.Null;
            if (db == null || string.IsNullOrWhiteSpace(handleHex))
                return false;

            try
            {
                if (!long.TryParse(handleHex.Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long val) || val <= 0)
                    return false;

                if (db.TryGetObjectId(new Handle(val), out ObjectId resolved) &&
                    !resolved.IsNull && resolved.IsValid && !resolved.IsErased)
                {
                    id = resolved;
                    return true;
                }
            }
            catch { }

            return false;
        }

        internal static bool TryReadCorridorRecipe(Corridor corridor, Transaction tr, out SiteCorridorRecipe? recipe)
        {
            recipe = null;
            if (corridor == null || corridor.IsErased)
                return false;

            try
            {
                ObjectId extDictId = corridor.ExtensionDictionary;
                if (extDictId.IsNull || !extDictId.IsValid || extDictId.IsErased)
                    return false;

                if (tr.GetObject(extDictId, OpenMode.ForRead) is not DBDictionary extDict)
                    return false;

                if (!extDict.Contains(RecipeDictKey))
                    return false;

                ObjectId recId = extDict.GetAt(RecipeDictKey);
                if (recId.IsNull || !recId.IsValid || recId.IsErased)
                    return false;

                if (tr.GetObject(recId, OpenMode.ForRead) is not Xrecord xrec || xrec.Data == null)
                    return false;

                var sb = new StringBuilder();
                foreach (TypedValue tv in xrec.Data)
                {
                    if (tv.TypeCode == (int)DxfCode.Text && tv.Value is string chunk)
                        sb.Append(chunk);
                }

                string json = sb.ToString();
                if (string.IsNullOrWhiteSpace(json))
                    return false;

                recipe = JsonSerializer.Deserialize<SiteCorridorRecipe>(json);
                return recipe != null;
            }
            catch
            {
                recipe = null;
                return false;
            }
        }

        internal static void SaveCorridorRecipe(Corridor corridor, Transaction tr, SiteCorridorRecipe recipe)
        {
            if (corridor == null || corridor.IsErased || recipe == null)
                return;

            try
            {
                if (!corridor.IsWriteEnabled)
                    corridor.UpgradeOpen();

                ObjectId extDictId = corridor.ExtensionDictionary;
                if (extDictId.IsNull)
                {
                    corridor.CreateExtensionDictionary();
                    extDictId = corridor.ExtensionDictionary;
                }
                if (extDictId.IsNull) return;

                if (tr.GetObject(extDictId, OpenMode.ForWrite) is not DBDictionary extDict)
                    return;

                string json = JsonSerializer.Serialize(recipe);
                using (var rb = new ResultBuffer())
                {
                    const int chunkLen = 200;
                    for (int offset = 0; offset < json.Length; offset += chunkLen)
                    {
                        int len = Math.Min(chunkLen, json.Length - offset);
                        rb.Add(new TypedValue((int)DxfCode.Text, json.Substring(offset, len)));
                    }

                    if (extDict.Contains(RecipeDictKey))
                    {
                        ObjectId recId = extDict.GetAt(RecipeDictKey);
                        if (!recId.IsNull && recId.IsValid && !recId.IsErased &&
                            tr.GetObject(recId, OpenMode.ForWrite) is Xrecord existingXrec)
                        {
                            existingXrec.Data = rb;
                            return;
                        }
                    }

                    using (var xrec = new Xrecord())
                    {
                        xrec.Data = rb;
                        extDict.SetAt(RecipeDictKey, xrec);
                        tr.AddNewlyCreatedDBObject(xrec, true);
                    }
                }
            }
            catch { }
        }

        internal static SiteCorridorRecipe BuildUpdatedRecipe(
            TaoCorridorTheoCauKienOptions options,
            SiteCorridorRecipe? previousRecipe,
            Dictionary<string, string> runtimeClonedMap)
        {
            var recipe = new SiteCorridorRecipe
            {
                Version = 1,
                HasSiteBinding = options.HasSiteSelection || (previousRecipe?.HasSiteBinding ?? false),
                SiteHandle = options.HasSiteSelection
                    ? (options.IsSiteLessMode ? "" : GetHandleHex(options.SelectedSiteId))
                    : (previousRecipe?.SiteHandle ?? ""),
                SiteName = options.HasSiteSelection
                    ? options.SelectedSiteName
                    : (previousRecipe?.SiteName ?? ""),
                IsSiteLess = options.HasSiteSelection
                    ? options.IsSiteLessMode
                    : (previousRecipe?.IsSiteLess ?? false),
                DefaultAssemblyHandle = GetHandleHex(options.DefaultAssemblyId),
                DefaultSurfaceHandle = GetHandleHex(options.DefaultTargetSurfaceId),
                CodeSetStyleHandle = GetHandleHex(options.SelectedCodeSetStyleId),
                DefaultFreqTangent = options.DefaultFreqTangent,
                DefaultFreqCurve = options.DefaultFreqCurve,
                RemoveDeletedBaselines = options.RemoveDeletedBaselines
            };

            if (previousRecipe?.ClonedFeatureLineMap != null)
            {
                foreach (var kv in previousRecipe.ClonedFeatureLineMap)
                    recipe.ClonedFeatureLineMap[kv.Key] = kv.Value;
            }
            foreach (var kv in runtimeClonedMap)
                recipe.ClonedFeatureLineMap[kv.Key] = kv.Value;

            // Giữ các quy tắc Style/Layer cũ rồi cập nhật theo dòng hiện tại
            var ruleByKey = new Dictionary<string, StyleLayerRuleConfig>(StringComparer.OrdinalIgnoreCase);
            if (previousRecipe?.StyleLayerRules != null)
            {
                foreach (var oldRule in previousRecipe.StyleLayerRules)
                {
                    string key = MakeStyleLayerKey(oldRule.StyleName, oldRule.LayerName);
                    ruleByKey[key] = oldRule;
                }
            }

            var managedFlHandles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in options.ComponentRows)
            {
                if (row.SourceType != ComponentBaselineSourceType.FeatureLine)
                    continue;

                foreach (ObjectId flId in row.BaselineEntityIds)
                {
                    string flHandle = GetHandleHex(flId);
                    if (string.IsNullOrEmpty(flHandle))
                        continue;

                    if (row.IsEnabled)
                        managedFlHandles.Add(flHandle);

                    recipe.PerFeatureLineConfigs.Add(new SavedFeatureLineConfig
                    {
                        FlHandle = flHandle,
                        IsEnabled = row.IsEnabled,
                        AssemblyHandle = GetHandleHex(row.AssemblyId),
                        UseDefaultSurface = row.UseDefaultSurface,
                        DisableSurfaceTarget = row.DisableSurfaceTarget,
                        TargetSurfaceHandle = GetHandleHex(row.TargetSurfaceId),
                        OffsetTargetHandles = row.OffsetElevationTargetIds
                            .Select(GetHandleHex)
                            .Where(h => !string.IsNullOrEmpty(h))
                            .ToList(),
                        FreqTangent = row.FrequencyTangent,
                        FreqCurve = row.FrequencyCurve
                    });
                }

                if (row.IsEnabled && !row.AssemblyId.IsNull)
                {
                    string slKey = MakeStyleLayerKey(row.FeatureLineStyleName, row.FeatureLineLayer);
                    ruleByKey[slKey] = new StyleLayerRuleConfig
                    {
                        StyleName = row.FeatureLineStyleName ?? "",
                        LayerName = row.FeatureLineLayer ?? "",
                        AssemblyHandle = GetHandleHex(row.AssemblyId),
                        UseDefaultSurface = row.UseDefaultSurface,
                        DisableSurfaceTarget = row.DisableSurfaceTarget,
                        TargetSurfaceHandle = GetHandleHex(row.TargetSurfaceId),
                        OffsetTargetHandles = row.OffsetElevationTargetIds
                            .Select(GetHandleHex)
                            .Where(h => !string.IsNullOrEmpty(h))
                            .ToList(),
                        FreqTangent = row.FrequencyTangent,
                        FreqCurve = row.FrequencyCurve
                    };
                }
            }

            recipe.ManagedFeatureLineHandles = managedFlHandles.ToList();
            recipe.StyleLayerRules = ruleByKey.Values.ToList();
            return recipe;
        }

        internal static string MakeStyleLayerKey(string? styleName, string? layerName)
            => $"{(styleName ?? "").Trim()}||{(layerName ?? "").Trim()}";
    }
}

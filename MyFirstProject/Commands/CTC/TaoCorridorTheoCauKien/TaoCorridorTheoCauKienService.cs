using System;
using System.Collections.Generic;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;

using Exception = System.Exception;

namespace MyFirstProject.Commands.CTC.TaoCorridorTheoCauKien
{
    /// <summary>
    /// Xử lý đồng bộ danh sách FeatureLine theo Site vào 1 Corridor tổng,
    /// giữ nguyên tuyệt đối lý trình của các Region đã có sẵn.
    /// </summary>
    internal static partial class TaoCorridorTheoCauKienService
    {
        public static void Execute(Database db, Editor ed, TaoCorridorTheoCauKienOptions options)
        {
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    CivilDocument cdoc = CivilDocument.GetCivilDocument(db);
                    Corridor? targetCorridor = ResolveTargetCorridor(tr, cdoc, options, ed);
                    if (targetCorridor == null)
                    {
                        ed.WriteMessage("\n❌ Không thể khởi tạo hoặc mở Corridor tổng đích.");
                        tr.Abort();
                        return;
                    }

                    TryReadCorridorRecipe(targetCorridor, tr, out SiteCorridorRecipe? previousRecipe);
                    var runtimeClonedMap = CloneMapFromRecipe(previousRecipe);

                    int removedBaselines = 0;
                    if (options.RemoveDeletedBaselines)
                    {
                        removedBaselines = RemoveStaleManagedBaselines(
                            tr, db, ed, targetCorridor, options, previousRecipe, runtimeClonedMap);
                    }

                    var snapshot = CaptureExistingBaselineSnapshot(db, targetCorridor, runtimeClonedMap);

                    int addedBaselines = 0;
                    int addedRegions = 0;
                    int keptExistingRegions = 0;
                    var handledPreExisting = new HashSet<ObjectId>();

                    foreach (CorridorComponentRowItem row in options.ComponentRows)
                    {
                        ProcessComponentRow(
                            tr, db, ed, options, row, targetCorridor,
                            snapshot, handledPreExisting, runtimeClonedMap,
                            ref addedBaselines, ref addedRegions, ref keptExistingRegions);
                    }

                    DeleteMergedSourceCorridors(tr, ed, options, targetCorridor, addedRegions + keptExistingRegions);

                    var updatedRecipe = BuildUpdatedRecipe(options, previousRecipe, runtimeClonedMap);
                    SaveCorridorRecipe(targetCorridor, tr, updatedRecipe);

                    RebuildCorridorIfNeeded(ed, options, targetCorridor);
                    tr.Commit();

                    WriteCompletionSummary(
                        ed, targetCorridor.Name, addedBaselines, addedRegions, keptExistingRegions, removedBaselines);
                }
                catch (Autodesk.AutoCAD.Runtime.Exception aEx)
                {
                    ed.WriteMessage($"\n❌ Lỗi AutoCAD: {aEx.Message}");
                    tr.Abort();
                }
                catch (Exception ex)
                {
                    ed.WriteMessage($"\n❌ Lỗi hệ thống: {ex.Message}");
                    tr.Abort();
                }
            }
        }

        private static Corridor? ResolveTargetCorridor(
            Transaction tr, CivilDocument cdoc, TaoCorridorTheoCauKienOptions options, Editor ed)
        {
            if (!options.ExistingCorridorId.IsNull && options.ExistingCorridorId.IsValid && !options.ExistingCorridorId.IsErased)
                return tr.GetObject(options.ExistingCorridorId, OpenMode.ForWrite) as Corridor;

            string desiredName = string.IsNullOrWhiteSpace(options.CorridorName) ? "COR_Tong" : options.CorridorName.Trim();

            foreach (ObjectId id in cdoc.CorridorCollection)
            {
                if (tr.GetObject(id, OpenMode.ForRead) is Corridor existing &&
                    existing.Name.Equals(desiredName, StringComparison.OrdinalIgnoreCase))
                {
                    existing.UpgradeOpen();
                    return existing;
                }
            }

            ObjectId newId = cdoc.CorridorCollection.Add(desiredName);
            var newCorridor = tr.GetObject(newId, OpenMode.ForWrite) as Corridor;
            if (newCorridor != null && !options.SelectedCodeSetStyleId.IsNull && options.SelectedCodeSetStyleId.IsValid)
            {
                try { newCorridor.CodeSetStyleId = options.SelectedCodeSetStyleId; } catch { }
            }

            ed.WriteMessage($"\n[+] Đã khởi tạo Corridor tổng mới: '{desiredName}'");
            return newCorridor;
        }

        private static Dictionary<string, string> CloneMapFromRecipe(SiteCorridorRecipe? recipe)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (recipe?.ClonedFeatureLineMap == null) return map;
            foreach (var kv in recipe.ClonedFeatureLineMap)
                map[kv.Key] = kv.Value;
            return map;
        }

        private static int RemoveStaleManagedBaselines(
            Transaction tr,
            Database db,
            Editor ed,
            Corridor targetCorridor,
            TaoCorridorTheoCauKienOptions options,
            SiteCorridorRecipe? previousRecipe,
            Dictionary<string, string> clonedMap)
        {
            var activeFlIds = CollectActiveFeatureLineIds(db, options, clonedMap);
            var managedIds = CollectManagedFeatureLineIds(db, previousRecipe, clonedMap);

            int removedCount = 0;
            for (int i = targetCorridor.Baselines.Count - 1; i >= 0; i--)
            {
                Baseline bl = targetCorridor.Baselines[i];
                if (!bl.IsFeatureLineBased()) continue;

                ObjectId flId = bl.FeatureLineId;
                bool isErased = flId.IsNull || !flId.IsValid || flId.IsErased;
                bool shouldRemove = isErased;

                if (!shouldRemove && !activeFlIds.Contains(flId))
                {
                    if (managedIds.Contains(flId) || BelongsToSelectedSite(tr, flId, options))
                        shouldRemove = true;
                }

                if (!shouldRemove) continue;

                string blName = bl.Name;
                try
                {
                    targetCorridor.Baselines.Remove(bl);
                    removedCount++;
                    string reason = isErased ? "FL đã bị xóa khỏi bản vẽ" : "không còn trong danh sách bật của Site";
                    ed.WriteMessage($"\n  [-] Đã gỡ Baseline '{blName}' ({reason}).");
                }
                catch (Exception ex)
                {
                    ed.WriteMessage($"\n  ⚠ Không thể gỡ Baseline '{blName}': {ex.Message}");
                }
            }

            return removedCount;
        }

        private static HashSet<ObjectId> CollectActiveFeatureLineIds(
            Database db, TaoCorridorTheoCauKienOptions options, Dictionary<string, string> clonedMap)
        {
            var set = new HashSet<ObjectId>();
            foreach (var row in options.ComponentRows)
            {
                if (!row.IsEnabled || row.SourceType != ComponentBaselineSourceType.FeatureLine) continue;
                foreach (ObjectId flId in row.BaselineEntityIds)
                {
                    if (flId.IsNull || !flId.IsValid || flId.IsErased) continue;
                    set.Add(flId);

                    string hex = GetHandleHex(flId);
                    if (!string.IsNullOrEmpty(hex) && clonedMap.TryGetValue(hex, out string? clonedHex) &&
                        TryResolveHandle(db, clonedHex, out ObjectId clonedId))
                    {
                        set.Add(clonedId);
                    }
                }
            }
            return set;
        }

        private static HashSet<ObjectId> CollectManagedFeatureLineIds(
            Database db, SiteCorridorRecipe? recipe, Dictionary<string, string> clonedMap)
        {
            var set = new HashSet<ObjectId>();
            if (recipe?.ManagedFeatureLineHandles == null) return set;

            foreach (string handleHex in recipe.ManagedFeatureLineHandles)
            {
                if (TryResolveHandle(db, handleHex, out ObjectId flId))
                    set.Add(flId);

                if (clonedMap.TryGetValue(handleHex, out string? clonedHex) &&
                    TryResolveHandle(db, clonedHex, out ObjectId clonedId))
                {
                    set.Add(clonedId);
                }
            }
            return set;
        }

        private static bool BelongsToSelectedSite(Transaction tr, ObjectId flId, TaoCorridorTheoCauKienOptions options)
        {
            if (!options.HasSiteSelection) return false;
            if (tr.GetObject(flId, OpenMode.ForRead) is not FeatureLine fl) return false;
            return options.IsSiteLessMode ? fl.SiteId.IsNull : fl.SiteId == options.SelectedSiteId;
        }

        private static void DeleteMergedSourceCorridors(
            Transaction tr,
            Editor ed,
            TaoCorridorTheoCauKienOptions options,
            Corridor targetCorridor,
            int totalProcessed)
        {
            if (!options.DeleteMergedSourceCorridors || options.SourceCorridorsToMerge.Count == 0 || totalProcessed <= 0)
                return;

            foreach (ObjectId srcCorrId in options.SourceCorridorsToMerge)
            {
                if (srcCorrId.IsNull || !srcCorrId.IsValid || srcCorrId.IsErased || srcCorrId == targetCorridor.ObjectId)
                    continue;

                try
                {
                    if (tr.GetObject(srcCorrId, OpenMode.ForWrite) is Corridor srcCorr)
                    {
                        string oldName = srcCorr.Name;
                        srcCorr.Erase();
                        ed.WriteMessage($"\n  🗑 Đã xóa Corridor nguồn '{oldName}' sau khi gộp vào '{targetCorridor.Name}'.");
                    }
                }
                catch (Exception ex)
                {
                    ed.WriteMessage($"\n  ⚠ Không thể xóa Corridor nguồn ({srcCorrId.Handle}): {ex.Message}");
                }
            }
        }

        private static void RebuildCorridorIfNeeded(Editor ed, TaoCorridorTheoCauKienOptions options, Corridor corr)
        {
            if (!options.AutoRebuild) return;
            ed.WriteMessage($"\n\n🔄 Đang Rebuild Corridor '{corr.Name}'...");
            try
            {
                corr.Rebuild();
                if (corr.IsOutOfDate)
                    corr.Rebuild();
                ed.WriteMessage($"\n  ✓ Đã Rebuild Corridor '{corr.Name}' thành công.");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n  ⚠ Cảnh báo khi Rebuild '{corr.Name}': {ex.Message}");
            }
        }

        private static void WriteCompletionSummary(
            Editor ed,
            string corridorName,
            int addedBaselines,
            int addedRegions,
            int keptExistingRegions,
            int removedBaselines)
        {
            ed.WriteMessage("\n=========================================================================");
            ed.WriteMessage($"\n✅ HOÀN TẤT CẬP NHẬT CORRIDOR TỔNG '{corridorName}':");
            ed.WriteMessage($"\n   • Baseline mới thêm                : +{addedBaselines}");
            ed.WriteMessage($"\n   • Region mới thêm                  : +{addedRegions}");
            ed.WriteMessage($"\n   • Region đã có (Giữ nguyên LT)     : ={keptExistingRegions}");
            ed.WriteMessage($"\n   • Baseline cũ đã gỡ (FL bị xóa)    : -{removedBaselines}");
            ed.WriteMessage("\n=========================================================================\n");
        }
    }
}

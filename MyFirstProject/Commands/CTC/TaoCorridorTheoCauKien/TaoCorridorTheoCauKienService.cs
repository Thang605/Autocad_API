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
    /// Xử lý CAD / Civil 3D cho lệnh CTC_Tao_Corridor_Theo_Cau_Kien.
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
                    var touchedCorridors = new HashSet<ObjectId>();

                    int totalComponentsProcessed = 0;
                    int totalBaselinesAdded = 0;
                    int totalRegionsAdded = 0;

                    Corridor? sharedCorridor = null;
                    if (options.BuildMode == CorridorComponentBuildMode.SingleOrExistingCorridor)
                    {
                        sharedCorridor = ResolveSharedCorridor(tr, cdoc, options, ed);
                        if (sharedCorridor == null)
                        {
                            ed.WriteMessage("\n❌ Không thể khởi tạo hoặc mở Corridor đích để gộp.");
                            tr.Abort();
                            return;
                        }
                        touchedCorridors.Add(sharedCorridor.ObjectId);
                    }

                    foreach (CorridorComponentRowItem row in options.ComponentRows)
                    {
                        int compRegions = ProcessComponentRow(
                            tr,
                            db,
                            cdoc,
                            ed,
                            options,
                            row,
                            sharedCorridor,
                            touchedCorridors,
                            ref totalBaselinesAdded,
                            ref totalRegionsAdded);

                        if (compRegions > 0)
                            totalComponentsProcessed++;
                    }

                    DeleteMergedSourceCorridors(tr, ed, options, sharedCorridor, totalComponentsProcessed);
                    RebuildTouchedCorridors(tr, ed, options, touchedCorridors);

                    tr.Commit();

                    WriteCompletionSummary(
                        ed,
                        totalComponentsProcessed,
                        touchedCorridors.Count,
                        totalBaselinesAdded,
                        totalRegionsAdded);
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

        private static Corridor? ResolveSharedCorridor(
            Transaction tr,
            CivilDocument cdoc,
            TaoCorridorTheoCauKienOptions options,
            Editor ed)
        {
            string prefix = string.IsNullOrWhiteSpace(options.CorridorNamePrefix)
                ? "COR_"
                : options.CorridorNamePrefix.Trim();
            string mergedName = prefix.EndsWith("_") ? $"{prefix}Gop_CauKien" : prefix;

            return GetOrCreateCorridor(
                tr,
                cdoc,
                options.ExistingCorridorId,
                mergedName,
                options.SelectedCodeSetStyleId,
                ed);
        }

        private static int ProcessComponentRow(
            Transaction tr,
            Database db,
            CivilDocument cdoc,
            Editor ed,
            TaoCorridorTheoCauKienOptions options,
            CorridorComponentRowItem row,
            Corridor? sharedCorridor,
            HashSet<ObjectId> touchedCorridors,
            ref int totalBaselinesAdded,
            ref int totalRegionsAdded)
        {
            if (!row.IsEnabled || row.BaselineEntityIds.Count == 0 || row.AssemblyId.IsNull)
                return 0;

            Corridor? targetCorridor = sharedCorridor;
            if (options.BuildMode == CorridorComponentBuildMode.SeparateCorridorPerComponent)
            {
                string compCorrName = BuildSafeCorridorName(options.CorridorNamePrefix, row.ComponentName);
                targetCorridor = GetOrCreateCorridor(
                    tr,
                    cdoc,
                    ObjectId.Null,
                    compCorrName,
                    options.SelectedCodeSetStyleId,
                    ed);

                if (targetCorridor == null)
                    return 0;

                touchedCorridors.Add(targetCorridor.ObjectId);
            }

            if (targetCorridor == null)
                return 0;

            ed.WriteMessage($"\n▶ Cấu kiện [{row.ComponentCategory}] '{row.ComponentName}' → Corridor '{targetCorridor.Name}' (Assembly: '{row.AssemblyName}'):");

            int compRegions = 0;
            foreach (ObjectId entityId in row.BaselineEntityIds)
            {
                if (ProcessSingleBaselineEntity(
                    tr,
                    db,
                    ed,
                    options,
                    row,
                    targetCorridor,
                    entityId,
                    ref totalBaselinesAdded,
                    ref totalRegionsAdded))
                {
                    compRegions++;
                }
            }

            return compRegions;
        }

        private static Corridor? GetOrCreateCorridor(
            Transaction tr,
            CivilDocument cdoc,
            ObjectId existingCorridorId,
            string desiredName,
            ObjectId codeSetStyleId,
            Editor ed)
        {
            if (!existingCorridorId.IsNull && existingCorridorId.IsValid && !existingCorridorId.IsErased)
            {
                return tr.GetObject(existingCorridorId, OpenMode.ForWrite) as Corridor;
            }

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
            if (newCorridor != null && !codeSetStyleId.IsNull && codeSetStyleId.IsValid)
            {
                try { newCorridor.CodeSetStyleId = codeSetStyleId; } catch { }
            }

            ed.WriteMessage($"\n[+] Đã khởi tạo Corridor mới: '{desiredName}'");
            return newCorridor;
        }

        private static string BuildSafeCorridorName(string prefix, string componentName)
        {
            string cleanPrefix = string.IsNullOrWhiteSpace(prefix) ? "COR_" : prefix.Trim();
            string cleanComp = string.IsNullOrWhiteSpace(componentName) ? "CauKien" : componentName.Trim();
            if (cleanComp.StartsWith(cleanPrefix, StringComparison.OrdinalIgnoreCase))
                return cleanComp;
            return $"{cleanPrefix}{cleanComp}";
        }

        private static void DeleteMergedSourceCorridors(
            Transaction tr,
            Editor ed,
            TaoCorridorTheoCauKienOptions options,
            Corridor? sharedCorridor,
            int totalComponentsProcessed)
        {
            if (options.BuildMode != CorridorComponentBuildMode.SingleOrExistingCorridor ||
                !options.DeleteMergedSourceCorridors ||
                sharedCorridor == null ||
                options.SourceCorridorsToMerge.Count == 0 ||
                totalComponentsProcessed <= 0)
            {
                return;
            }

            foreach (ObjectId srcCorrId in options.SourceCorridorsToMerge)
            {
                if (srcCorrId.IsNull || !srcCorrId.IsValid || srcCorrId.IsErased || srcCorrId == sharedCorridor.ObjectId)
                    continue;

                try
                {
                    if (tr.GetObject(srcCorrId, OpenMode.ForWrite) is Corridor srcCorr)
                    {
                        string oldName = srcCorr.Name;
                        srcCorr.Erase();
                        ed.WriteMessage($"\n  🗑 Đã xóa Corridor nguồn '{oldName}' sau khi gộp vào '{sharedCorridor.Name}'.");
                    }
                }
                catch (Exception exDel)
                {
                    ed.WriteMessage($"\n  ⚠ Không thể xóa Corridor nguồn ({srcCorrId.Handle}): {exDel.Message}");
                }
            }
        }

        private static void RebuildTouchedCorridors(
            Transaction tr,
            Editor ed,
            TaoCorridorTheoCauKienOptions options,
            HashSet<ObjectId> touchedCorridors)
        {
            if (!options.AutoRebuild || touchedCorridors.Count == 0)
                return;

            ed.WriteMessage("\n\n🔄 Đang Rebuild các Corridor...");
            foreach (ObjectId corrId in touchedCorridors)
            {
                if (corrId.IsNull || corrId.IsErased) continue;
                if (tr.GetObject(corrId, OpenMode.ForWrite) is not Corridor corr) continue;

                try
                {
                    corr.Rebuild();
                    if (corr.IsOutOfDate)
                        corr.Rebuild();
                    ed.WriteMessage($"\n  ✓ Đã Rebuild Corridor '{corr.Name}' thành công.");
                }
                catch (Exception exReb)
                {
                    ed.WriteMessage($"\n  ⚠ Cảnh báo khi Rebuild '{corr.Name}': {exReb.Message}");
                }
            }
        }

        private static void WriteCompletionSummary(
            Editor ed,
            int totalComponentsProcessed,
            int touchedCorridorCount,
            int totalBaselinesAdded,
            int totalRegionsAdded)
        {
            ed.WriteMessage("\n==================================================================");
            ed.WriteMessage($"\n✅ HOÀN TẤT XÂY DỰNG & GỘP CORRIDOR THEO CẤU KIỆN:");
            ed.WriteMessage($"\n   • Số cấu kiện đã xử lý    : {totalComponentsProcessed}");
            ed.WriteMessage($"\n   • Số Corridor tạo/cập nhật: {touchedCorridorCount}");
            ed.WriteMessage($"\n   • Số Baseline mới thêm    : {totalBaselinesAdded}");
            ed.WriteMessage($"\n   • Số Region mới thêm      : {totalRegionsAdded}");
            ed.WriteMessage("\n==================================================================\n");
        }
    }
}

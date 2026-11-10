using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Exception = System.Exception;
using MyFirstProject.Civil_Tool_2;

[assembly: CommandClass(typeof(Civil3DCsharp.CTC_Tao_Corridor_Theo_Cau_Kien_Commands))]

namespace Civil3DCsharp
{
    public class CTC_Tao_Corridor_Theo_Cau_Kien_Commands
    {
        [CommandMethod("CTC_Tao_Corridor_Theo_Cau_Kien")]
        public static void CTC_Tao_Corridor_Theo_Cau_Kien()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var db = doc.Database;
            var ed = doc.Editor;

            ed.WriteMessage("\n==================================================================");
            ed.WriteMessage("\n  CTC_Tao_Corridor_Theo_Cau_Kien: XÂY DỰNG CORRIDOR THEO CẤU KIỆN");
            ed.WriteMessage("\n==================================================================\n");

            using (var form = new TaoCorridorTheoCauKienForm(db, ed))
            {
                DialogResult result = Application.ShowModalDialog(form);
                if (result != DialogResult.OK || !form.FormAccepted)
                {
                    ed.WriteMessage("\n[Hủy bỏ] Đã hủy thao tác xây dựng Corridor theo cấu kiện.");
                    return;
                }

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
                        if (form.BuildMode == CorridorComponentBuildMode.SingleOrExistingCorridor)
                        {
                            sharedCorridor = GetOrCreateCorridor(
                                tr,
                                cdoc,
                                form.ExistingCorridorId,
                                $"{form.CorridorNamePrefix}Modular",
                                form.SelectedCodeSetStyleId,
                                ed);

                            if (sharedCorridor == null)
                            {
                                ed.WriteMessage("\n❌ Không thể khởi tạo hoặc mở Corridor đích.");
                                tr.Abort();
                                return;
                            }
                            touchedCorridors.Add(sharedCorridor.ObjectId);
                        }

                        foreach (CorridorComponentRowItem row in form.ComponentRows)
                        {
                            if (!row.IsEnabled || row.BaselineEntityIds.Count == 0 || row.AssemblyId.IsNull)
                                continue;

                            Corridor? targetCorridor = sharedCorridor;
                            if (form.BuildMode == CorridorComponentBuildMode.SeparateCorridorPerComponent)
                            {
                                string compCorrName = BuildSafeCorridorName(form.CorridorNamePrefix, row.ComponentName);
                                targetCorridor = GetOrCreateCorridor(
                                    tr,
                                    cdoc,
                                    ObjectId.Null,
                                    compCorrName,
                                    form.SelectedCodeSetStyleId,
                                    ed);

                                if (targetCorridor == null)
                                    continue;

                                touchedCorridors.Add(targetCorridor.ObjectId);
                            }

                            if (targetCorridor == null) continue;

                            ed.WriteMessage($"\n▶ Cấu kiện [{row.ComponentCategory}] '{row.ComponentName}' → Corridor '{targetCorridor.Name}' (Assembly: '{row.AssemblyName}'):");

                            int compBaselines = 0;
                            int compRegions = 0;

                            foreach (ObjectId entityId in row.BaselineEntityIds)
                            {
                                if (entityId.IsNull || !entityId.IsValid || entityId.IsErased)
                                    continue;

                                try
                                {
                                    Baseline? baseline = null;
                                    string entityDisplayName = "";

                                    if (row.SourceType == ComponentBaselineSourceType.FeatureLine)
                                    {
                                        if (tr.GetObject(entityId, OpenMode.ForRead) is not FeatureLine fl)
                                            continue;

                                        entityDisplayName = string.IsNullOrWhiteSpace(fl.Name) ? $"FL_{fl.Handle}" : fl.Name;
                                        string blName = GetUniqueBaselineName(targetCorridor, $"BL_{row.ComponentName}_{entityDisplayName}");
                                        baseline = targetCorridor.Baselines.Add(blName, fl.ObjectId);
                                    }
                                    else
                                    {
                                        if (tr.GetObject(entityId, OpenMode.ForRead) is not Alignment align)
                                            continue;

                                        ObjectId profileId = SelectDesignProfileId(tr, align, row.ExplicitProfileId);
                                        if (profileId.IsNull)
                                        {
                                            ed.WriteMessage($"\n  ⚠ Tuyến '{align.Name}' chưa có Profile (Trắc dọc). Bỏ qua tuyến này.");
                                            continue;
                                        }

                                        var prof = tr.GetObject(profileId, OpenMode.ForRead) as Profile;
                                        entityDisplayName = align.Name;
                                        string blName = GetUniqueBaselineName(targetCorridor, $"BL_{row.ComponentName}_{align.Name}_{prof?.Name}");
                                        baseline = targetCorridor.Baselines.Add(blName, align.ObjectId, profileId);
                                    }

                                    if (baseline == null) continue;
                                    compBaselines++;
                                    totalBaselinesAdded++;

                                    double blStart = baseline.StartStation;
                                    double blEnd = baseline.EndStation;
                                    double regStart = row.StartStation > blStart && row.StartStation < blEnd ? row.StartStation : blStart;
                                    double regEnd = row.EndStation > regStart && row.EndStation <= blEnd ? row.EndStation : blEnd;

                                    string regName = GetUniqueRegionName(baseline, $"RG_{row.ComponentName}_{entityDisplayName}_{regStart:F1}_{regEnd:F1}");
                                    BaselineRegion region = baseline.BaselineRegions.Add(regName, row.AssemblyId, regStart, regEnd);
                                    compRegions++;
                                    totalRegionsAdded++;

                                    // Thiết lập tần suất bước cọc (Frequency)
                                    ApplyRegionFrequency(
                                        region,
                                        row.FrequencyTangent > 0 ? row.FrequencyTangent : form.DefaultFreqTangent,
                                        row.FrequencyCurve > 0 ? row.FrequencyCurve : form.DefaultFreqCurve);

                                    // Gán Target Bề mặt & Target Mở rộng / Cao độ
                                    ObjectId surfaceTargetId = !row.TargetSurfaceId.IsNull ? row.TargetSurfaceId : form.DefaultTargetSurfaceId;
                                    ApplyComponentTargets(region, surfaceTargetId, row.OffsetElevationTargetIds, ed);

                                    ed.WriteMessage($"\n  ✓ Đã thêm Baseline '{baseline.Name}' & Region '{region.Name}' ({regStart:F2}m → {regEnd:F2}m)");
                                }
                                catch (Exception exBl)
                                {
                                    ed.WriteMessage($"\n  ⚠ Lỗi khi thêm đường dẫn ({entityId.Handle}): {exBl.Message}");
                                }
                            }

                            if (compBaselines > 0)
                                totalComponentsProcessed++;
                        }

                        // Tự động Rebuild các Corridor vừa tạo/cập nhật
                        if (form.AutoRebuild && touchedCorridors.Count > 0)
                        {
                            ed.WriteMessage("\n\n🔄 Đang Rebuild các Corridor cấu kiện...");
                            foreach (ObjectId corrId in touchedCorridors)
                            {
                                if (tr.GetObject(corrId, OpenMode.ForWrite) is Corridor corr)
                                {
                                    try
                                    {
                                        corr.Rebuild();
                                        ed.WriteMessage($"\n  ✓ Đã Rebuild Corridor '{corr.Name}' thành công.");
                                    }
                                    catch (Exception exReb)
                                    {
                                        ed.WriteMessage($"\n  ⚠ Cảnh báo khi Rebuild '{corr.Name}': {exReb.Message}");
                                    }
                                }
                            }
                        }

                        tr.Commit();

                        ed.WriteMessage("\n==================================================================");
                        ed.WriteMessage($"\n✅ HOÀN TẤT XÂY DỰNG CORRIDOR THEO CẤU KIỆN:");
                        ed.WriteMessage($"\n   • Số cấu kiện đã lắp ghép : {totalComponentsProcessed}");
                        ed.WriteMessage($"\n   • Số Corridor tạo/cập nhật: {touchedCorridors.Count}");
                        ed.WriteMessage($"\n   • Tổng số Baseline mới    : {totalBaselinesAdded}");
                        ed.WriteMessage($"\n   • Tổng số Region mới      : {totalRegionsAdded}");
                        ed.WriteMessage("\n==================================================================\n");
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

        /// <summary>
        /// Ưu tiên chọn Đường đỏ thiết kế (Layout Profile / tên bắt đầu bằng DD- hoặc TK-) thay vì đường tự nhiên (EG/TN)
        /// </summary>
        private static ObjectId SelectDesignProfileId(Transaction tr, Alignment align, ObjectId explicitProfileId)
        {
            if (!explicitProfileId.IsNull && explicitProfileId.IsValid && !explicitProfileId.IsErased)
                return explicitProfileId;

            ObjectIdCollection profileIds = align.GetProfileIds();
            if (profileIds == null || profileIds.Count == 0)
                return ObjectId.Null;

            ObjectId layoutCandidate = ObjectId.Null;
            ObjectId firstValid = ObjectId.Null;

            foreach (ObjectId pid in profileIds)
            {
                if (tr.GetObject(pid, OpenMode.ForRead) is not Profile prof) continue;
                if (firstValid.IsNull) firstValid = pid;

                if (prof.Name.StartsWith("DD", StringComparison.OrdinalIgnoreCase) ||
                    prof.Name.Contains("TK", StringComparison.OrdinalIgnoreCase) ||
                    prof.Name.Contains("Connected", StringComparison.OrdinalIgnoreCase))
                {
                    return pid;
                }

                if (prof.ProfileType == ProfileType.FG && layoutCandidate.IsNull)
                {
                    layoutCandidate = pid;
                }
            }

            return !layoutCandidate.IsNull ? layoutCandidate : firstValid;
        }

        private static string GetUniqueBaselineName(Corridor corridor, string baseName)
        {
            var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Baseline bl in corridor.Baselines)
                existingNames.Add(bl.Name);

            if (!existingNames.Contains(baseName))
                return baseName;

            int index = 2;
            while (existingNames.Contains($"{baseName}_{index}"))
                index++;

            return $"{baseName}_{index}";
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
                            var col = new ObjectIdCollection { surfaceId };
                            ti.TargetIds = col;
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

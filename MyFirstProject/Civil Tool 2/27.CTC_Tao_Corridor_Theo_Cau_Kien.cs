using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
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
            ed.WriteMessage("\n  CTC_Tao_Corridor_Theo_Cau_Kien: XÂY DỰNG & GỘP CORRIDOR THEO CẤU KIỆN");
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
                            string prefix = string.IsNullOrWhiteSpace(form.CorridorNamePrefix) ? "COR_" : form.CorridorNamePrefix.Trim();
                            string mergedName = prefix.EndsWith("_") ? $"{prefix}Gop_CauKien" : prefix;

                            sharedCorridor = GetOrCreateCorridor(
                                tr,
                                cdoc,
                                form.ExistingCorridorId,
                                mergedName,
                                form.SelectedCodeSetStyleId,
                                ed);

                            if (sharedCorridor == null)
                            {
                                ed.WriteMessage("\n❌ Không thể khởi tạo hoặc mở Corridor đích để gộp.");
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

                            int compRegions = 0;

                            foreach (ObjectId entityId in row.BaselineEntityIds)
                            {
                                if (entityId.IsNull || !entityId.IsValid || entityId.IsErased)
                                    continue;

                                try
                                {
                                    Baseline? baseline = null;
                                    string entityDisplayName = "";
                                    ObjectId resolvedFlId = ObjectId.Null;
                                    ObjectId resolvedAlignId = ObjectId.Null;
                                    ObjectId resolvedProfId = ObjectId.Null;

                                    if (row.SourceType == ComponentBaselineSourceType.FeatureLine)
                                    {
                                        if (tr.GetObject(entityId, OpenMode.ForRead) is not FeatureLine fl)
                                            continue;

                                        EnsureFeatureLineHasName(tr, fl);
                                        entityDisplayName = fl.Name;
                                        resolvedFlId = fl.ObjectId;

                                        double flStart = 0.0;
                                        double flEnd = fl.Length2D;
                                        double reqStart = row.StartStation > flStart && row.StartStation < flEnd ? row.StartStation : flStart;
                                        double reqEnd = row.EndStation > reqStart && row.EndStation <= flEnd + 0.05
                                            ? Math.Min(flEnd, row.EndStation)
                                            : flEnd;

                                        // Kiểm tra nếu đã tồn tại Region trùng khớp hoàn toàn trong Corridor đích
                                        if (TryFindExistingMatchingRegion(targetCorridor, true, resolvedFlId, ObjectId.Null, row.AssemblyId, reqStart, reqEnd, out Baseline? matchedBl, out BaselineRegion? matchedRg) &&
                                            matchedBl != null && matchedRg != null)
                                        {
                                            ApplyRegionFrequency(
                                                matchedRg,
                                                row.FrequencyTangent > 0 ? row.FrequencyTangent : form.DefaultFreqTangent,
                                                row.FrequencyCurve > 0 ? row.FrequencyCurve : form.DefaultFreqCurve);
                                            ObjectId surfTargetId = !row.TargetSurfaceId.IsNull ? row.TargetSurfaceId : form.DefaultTargetSurfaceId;
                                            ApplyComponentTargets(matchedRg, surfTargetId, row.OffsetElevationTargetIds, ed);
                                            compRegions++;
                                            ed.WriteMessage($"\n  ✓ Đã cập nhật Region hiện có '{matchedRg.Name}' trên Baseline '{matchedBl.Name}'");
                                            continue;
                                        }

                                        // Tìm Baseline cùng FeatureLine chưa bị giao cắt khoảng lý trình, nếu không có thì thêm Baseline mới
                                        baseline = FindReusableBaseline(targetCorridor, true, resolvedFlId, ObjectId.Null, reqStart, reqEnd);
                                        if (baseline == null)
                                        {
                                            string blName = GetUniqueBaselineName(targetCorridor, $"BL_{row.ComponentName}_{entityDisplayName}");
                                            try
                                            {
                                                baseline = targetCorridor.Baselines.Add(blName, resolvedFlId);
                                            }
                                            catch
                                            {
                                                // Trường hợp FeatureLine là AutoCorridorFeatureLine thuộc chính Corridor đích (tự tham chiếu)
                                                // → Tạo bản sao FeatureLine độc lập từ tọa độ 3D để cho phép gộp chung vào 1 Corridor
                                                ObjectId clonedFlId = CloneFeatureLineAsIndependent(tr, db, fl, $"FL_{row.ComponentName}_{fl.Handle}");
                                                if (!clonedFlId.IsNull)
                                                {
                                                    resolvedFlId = clonedFlId;
                                                    baseline = targetCorridor.Baselines.Add(blName, resolvedFlId);
                                                    ed.WriteMessage($"\n  ℹ Đã chuyển đổi FeatureLine phụ thuộc '{entityDisplayName}' thành FeatureLine độc lập để gộp vào Corridor '{targetCorridor.Name}'.");
                                                }
                                                else
                                                {
                                                    throw;
                                                }
                                            }
                                            totalBaselinesAdded++;
                                        }
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
                                        resolvedAlignId = align.ObjectId;
                                        resolvedProfId = profileId;

                                        double alStart = align.StartingStation;
                                        double alEnd = align.EndingStation;
                                        double reqStart = row.StartStation > alStart && row.StartStation < alEnd ? row.StartStation : alStart;
                                        double reqEnd = row.EndStation > reqStart && row.EndStation <= alEnd + 0.05
                                            ? Math.Min(alEnd, row.EndStation)
                                            : alEnd;

                                        if (TryFindExistingMatchingRegion(targetCorridor, false, resolvedAlignId, resolvedProfId, row.AssemblyId, reqStart, reqEnd, out Baseline? matchedBl, out BaselineRegion? matchedRg) &&
                                            matchedBl != null && matchedRg != null)
                                        {
                                            ApplyRegionFrequency(
                                                matchedRg,
                                                row.FrequencyTangent > 0 ? row.FrequencyTangent : form.DefaultFreqTangent,
                                                row.FrequencyCurve > 0 ? row.FrequencyCurve : form.DefaultFreqCurve);
                                            ObjectId surfTargetId = !row.TargetSurfaceId.IsNull ? row.TargetSurfaceId : form.DefaultTargetSurfaceId;
                                            ApplyComponentTargets(matchedRg, surfTargetId, row.OffsetElevationTargetIds, ed);
                                            compRegions++;
                                            ed.WriteMessage($"\n  ✓ Đã cập nhật Region hiện có '{matchedRg.Name}' trên Baseline '{matchedBl.Name}'");
                                            continue;
                                        }

                                        baseline = FindReusableBaseline(targetCorridor, false, resolvedAlignId, resolvedProfId, reqStart, reqEnd);
                                        if (baseline == null)
                                        {
                                            string blName = GetUniqueBaselineName(targetCorridor, $"BL_{row.ComponentName}_{align.Name}_{prof?.Name}");
                                            baseline = targetCorridor.Baselines.Add(blName, resolvedAlignId, resolvedProfId);
                                            totalBaselinesAdded++;
                                        }
                                    }

                                    if (baseline == null) continue;

                                    double blStart = baseline.StartStation;
                                    double blEnd = baseline.EndStation;
                                    double regStart = row.StartStation > blStart && row.StartStation < blEnd ? row.StartStation : blStart;
                                    double regEnd = row.EndStation > regStart && row.EndStation <= blEnd + 0.05
                                        ? Math.Min(blEnd, row.EndStation)
                                        : blEnd;

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

                                    ed.WriteMessage($"\n  ✓ Đã gộp vào Corridor '{targetCorridor.Name}': Baseline '{baseline.Name}' & Region '{region.Name}' ({regStart:F2}m → {regEnd:F2}m)");
                                }
                                catch (Exception exBl)
                                {
                                    ed.WriteMessage($"\n  ⚠ Lỗi khi thêm đường dẫn ({entityId.Handle}): {exBl.Message}");
                                }
                            }

                            if (compRegions > 0)
                                totalComponentsProcessed++;
                        }

                        // Xóa các Corridor nguồn đã gộp vào Corridor đích (nếu người dùng dùng chức năng Gộp các Corridor có sẵn)
                        if (form.BuildMode == CorridorComponentBuildMode.SingleOrExistingCorridor &&
                            form.DeleteMergedSourceCorridors &&
                            sharedCorridor != null &&
                            form.SourceCorridorsToMerge.Count > 0 &&
                            totalComponentsProcessed > 0)
                        {
                            foreach (ObjectId srcCorrId in form.SourceCorridorsToMerge)
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

                        // Tự động Rebuild các Corridor vừa tạo/cập nhật
                        if (form.AutoRebuild && touchedCorridors.Count > 0)
                        {
                            ed.WriteMessage("\n\n🔄 Đang Rebuild các Corridor...");
                            foreach (ObjectId corrId in touchedCorridors)
                            {
                                if (corrId.IsNull || corrId.IsErased) continue;
                                if (tr.GetObject(corrId, OpenMode.ForWrite) is Corridor corr)
                                {
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
                        }

                        tr.Commit();

                        ed.WriteMessage("\n==================================================================");
                        ed.WriteMessage($"\n✅ HOÀN TẤT XÂY DỰNG & GỘP CORRIDOR THEO CẤU KIỆN:");
                        ed.WriteMessage($"\n   • Số cấu kiện đã xử lý    : {totalComponentsProcessed}");
                        ed.WriteMessage($"\n   • Số Corridor tạo/cập nhật: {touchedCorridors.Count}");
                        ed.WriteMessage($"\n   • Số Baseline mới thêm    : {totalBaselinesAdded}");
                        ed.WriteMessage($"\n   • Số Region mới thêm      : {totalRegionsAdded}");
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

        private static void EnsureFeatureLineHasName(Transaction tr, FeatureLine fl)
        {
            if (!string.IsNullOrWhiteSpace(fl.Name))
                return;

            try
            {
                fl.UpgradeOpen();
                fl.Name = $"FL_{fl.Handle}";
            }
            catch { }
        }

        private static ObjectId CloneFeatureLineAsIndependent(Transaction tr, Database db, FeatureLine srcFl, string desiredName)
        {
            try
            {
                Point3dCollection pts = srcFl.GetPoints(Autodesk.Civil.FeatureLinePointType.AllPoints);
                if (pts == null || pts.Count < 2)
                    return ObjectId.Null;

                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                ObjectId poly3dId;
                using (var poly3d = new Polyline3d(Poly3dType.SimplePoly, pts, false))
                {
                    poly3d.Layer = srcFl.Layer;
                    poly3dId = ms.AppendEntity(poly3d);
                    tr.AddNewlyCreatedDBObject(poly3d, true);
                }

                ObjectId newFlId = FeatureLine.Create(desiredName, poly3dId);
                if (!poly3dId.IsNull && !poly3dId.IsErased)
                {
                    var tmpPoly = tr.GetObject(poly3dId, OpenMode.ForWrite, false);
                    tmpPoly?.Erase();
                }

                if (!newFlId.IsNull && tr.GetObject(newFlId, OpenMode.ForWrite) is FeatureLine newFl)
                {
                    newFl.Layer = srcFl.Layer;
                    EnsureFeatureLineHasName(tr, newFl);
                }

                return newFlId;
            }
            catch
            {
                return ObjectId.Null;
            }
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

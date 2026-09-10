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

[assembly: CommandClass(typeof(Civil3DCsharp.CTC_NhanBan_Corridor_Commands))]

namespace Civil3DCsharp
{
    public class CTC_NhanBan_Corridor_Commands
    {
        [CommandMethod("CTC_NhanBan_Corridor")]
        public static void CTC_NhanBan_Corridor()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var db = doc.Database;
            var ed = doc.Editor;

            ed.WriteMessage("\n=======================================================");
            ed.WriteMessage("\n  CTC_NhanBan_Corridor: NHÂN BẢN CORRIDOR CIVIL 3D");
            ed.WriteMessage("\n=======================================================\n");

            using (var form = new MyFirstProject.Civil_Tool_2.NhanBanCorridorForm(db))
            {
                var dialogResult = Application.ShowModalDialog(form);
                if (dialogResult != DialogResult.OK || !form.FormAccepted || form.SelectedCorridorId.IsNull)
                {
                    ed.WriteMessage("\n[Hủy bỏ] Thao tác nhân bản Corridor đã được hủy.");
                    return;
                }

                ObjectId sourceCorridorId = form.SelectedCorridorId;
                string newCorridorName = form.NewCorridorName;
                string suffix = form.Suffix;
                ObjectId codeSetStyleId = form.SelectedCodeSetStyleId;
                bool copyTargets = form.CopyTargets;
                bool copyFrequencies = form.CopyFrequencies;
                bool copySurfaces = form.CopySurfaces;
                bool autoRebuild = form.AutoRebuild;

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    try
                    {
                        var sourceCorridor = tr.GetObject(sourceCorridorId, OpenMode.ForRead) as Corridor;
                        if (sourceCorridor == null)
                        {
                            ed.WriteMessage("\n❌ Lỗi: Không thể truy xuất Corridor nguồn.");
                            tr.Abort();
                            return;
                        }

                        CivilDocument cdoc = CivilDocument.GetCivilDocument(db);

                        // 1. Kiểm tra trùng tên Corridor trong Database
                        foreach (ObjectId existingId in cdoc.CorridorCollection)
                        {
                            var existingCorridor = tr.GetObject(existingId, OpenMode.ForRead) as Corridor;
                            if (existingCorridor != null && existingCorridor.Name.Equals(newCorridorName, StringComparison.OrdinalIgnoreCase))
                            {
                                ed.WriteMessage($"\n❌ Lỗi: Corridor có tên '{newCorridorName}' đã tồn tại trong bản vẽ.");
                                tr.Abort();
                                return;
                            }
                        }

                        ed.WriteMessage($"\n🔄 Bắt đầu nhân bản Corridor '{sourceCorridor.Name}' → '{newCorridorName}'...");

                        // 2. Tạo đối tượng Corridor mới
                        ObjectId newCorridorId = cdoc.CorridorCollection.Add(newCorridorName);
                        var newCorridor = tr.GetObject(newCorridorId, OpenMode.ForWrite) as Corridor;
                        if (newCorridor == null)
                        {
                            ed.WriteMessage("\n❌ Lỗi: Không thể khởi tạo đối tượng Corridor mới.");
                            tr.Abort();
                            return;
                        }

                        // Gán Code Set Style & Description
                        if (!codeSetStyleId.IsNull && codeSetStyleId.IsValid)
                        {
                            newCorridor.CodeSetStyleId = codeSetStyleId;
                        }
                        else if (!sourceCorridor.CodeSetStyleId.IsNull && sourceCorridor.CodeSetStyleId.IsValid)
                        {
                            newCorridor.CodeSetStyleId = sourceCorridor.CodeSetStyleId;
                        }

                        newCorridor.Description = sourceCorridor.Description;

                        int expectedBaselines = sourceCorridor.Baselines.Count;
                        int expectedRegions = 0;
                        int totalBaselinesCloned = 0;
                        int totalRegionsCloned = 0;
                        int totalTargetsExpected = 0;
                        int totalTargetsCloned = 0;
                        var warningAndErrorList = new List<string>();

                        // 3. Nhân bản từng Baseline & Region
                        for (int bi = 0; bi < sourceCorridor.Baselines.Count; bi++)
                        {
                            var srcBaseline = sourceCorridor.Baselines[bi];
                            expectedRegions += srcBaseline.BaselineRegions.Count;
                            Baseline? newBaseline = null;

                            try
                            {
                                if (!srcBaseline.IsFeatureLineBased())
                                {
                                    if (srcBaseline.AlignmentId.IsNull || srcBaseline.ProfileId.IsNull)
                                    {
                                        string msg = $"Baseline '{srcBaseline.Name}' thiếu Alignment hoặc Profile hợp lệ.";
                                        warningAndErrorList.Add(msg);
                                        ed.WriteMessage($"\n  ⚠ Cảnh báo: {msg} Bỏ qua.");
                                        continue;
                                    }

                                    newBaseline = newCorridor.Baselines.Add(srcBaseline.Name, srcBaseline.AlignmentId, srcBaseline.ProfileId);
                                }
                                else
                                {
                                    if (srcBaseline.FeatureLineId.IsNull)
                                    {
                                        string msg = $"Baseline FeatureLine '{srcBaseline.Name}' có FeatureLineId không hợp lệ.";
                                        warningAndErrorList.Add(msg);
                                        ed.WriteMessage($"\n  ⚠ Cảnh báo: {msg} Bỏ qua.");
                                        continue;
                                    }

                                    newBaseline = newCorridor.Baselines.Add(srcBaseline.Name, srcBaseline.FeatureLineId);
                                }
                            }
                            catch (Exception exBaseline)
                            {
                                string msg = $"Không thể tạo Baseline '{srcBaseline.Name}': {exBaseline.Message}";
                                warningAndErrorList.Add(msg);
                                ed.WriteMessage($"\n  ❌ Lỗi: {msg}");
                                continue;
                            }

                            if (newBaseline == null) continue;
                            totalBaselinesCloned++;
                            ed.WriteMessage($"\n  ✓ Baseline [{totalBaselinesCloned}/{expectedBaselines}]: '{newBaseline.Name}'");

                            // Nhân bản các Region của Baseline
                            for (int ri = 0; ri < srcBaseline.BaselineRegions.Count; ri++)
                            {
                                var srcRegion = srcBaseline.BaselineRegions[ri];
                                BaselineRegion? newRegion = null;

                                try
                                {
                                    newRegion = newBaseline.BaselineRegions.Add(
                                        srcRegion.Name,
                                        srcRegion.AssemblyId,
                                        srcRegion.StartStation,
                                        srcRegion.EndStation
                                    );
                                }
                                catch (Exception exRegion)
                                {
                                    string msg = $"Không thể tạo Region '{srcRegion.Name}': {exRegion.Message}";
                                    warningAndErrorList.Add(msg);
                                    ed.WriteMessage($"\n    ❌ Lỗi: {msg}");
                                    continue;
                                }

                                if (newRegion == null) continue;
                                totalRegionsCloned++;
                                ed.WriteMessage($"\n    ✓ Phân đoạn [{ri + 1}]: '{newRegion.Name}' ({newRegion.StartStation:F2}m – {newRegion.EndStation:F2}m)");

                                // 3.1. Sao chép Tần suất phân đoạn (Frequencies)
                                if (copyFrequencies && srcRegion.AppliedAssemblySetting != null && newRegion.AppliedAssemblySetting != null)
                                {
                                    try
                                    {
                                        var srcSetting = srcRegion.AppliedAssemblySetting;
                                        var newSetting = newRegion.AppliedAssemblySetting;

                                        newSetting.FrequencyAlongTangents = srcSetting.FrequencyAlongTangents;
                                        newSetting.FrequencyAlongCurves = srcSetting.FrequencyAlongCurves;
                                        newSetting.FrequencyAlongSpirals = srcSetting.FrequencyAlongSpirals;
                                        newSetting.FrequencyAlongProfileCurves = srcSetting.FrequencyAlongProfileCurves;
                                        newSetting.FrequencyAlongTargetCurves = srcSetting.FrequencyAlongTargetCurves;
                                        newSetting.AppliedAtHorizontalGeometryPoints = srcSetting.AppliedAtHorizontalGeometryPoints;
                                        newSetting.AppliedAtSuperelevationCriticalPoints = srcSetting.AppliedAtSuperelevationCriticalPoints;
                                        newSetting.AppliedAtProfileGeometryPoints = srcSetting.AppliedAtProfileGeometryPoints;
                                        newSetting.AppliedAtProfileHighLowPoints = srcSetting.AppliedAtProfileHighLowPoints;
                                        newSetting.AppliedAtOffsetTargetGeometryPoints = srcSetting.AppliedAtOffsetTargetGeometryPoints;
                                        newSetting.AppliedAdjacentToOffsetTargetStartEnd = srcSetting.AppliedAdjacentToOffsetTargetStartEnd;

                                        // Sao chép các cọc bổ sung (Additional Stations) nếu có
                                        double[] addStations = srcRegion.AdditionalStations();
                                        if (addStations != null && addStations.Length > 0)
                                        {
                                            int addedStCount = 0;
                                            foreach (double st in addStations)
                                            {
                                                try
                                                {
                                                    newRegion.AddStation(st, "Copy");
                                                    addedStCount++;
                                                }
                                                catch { }
                                            }
                                            if (addedStCount > 0)
                                            {
                                                ed.WriteMessage($"      • Đã sao chép {addedStCount} cọc bổ sung.");
                                            }
                                        }

                                        ed.WriteMessage($"      • Đã sao chép tần suất cọc (Tangents: {newSetting.FrequencyAlongTangents:F1}m, Curves: {newSetting.FrequencyAlongCurves:F1}m)");
                                    }
                                    catch (Exception exFreq)
                                    {
                                        warningAndErrorList.Add($"Region '{newRegion.Name}': Lỗi sao chép tần suất cọc ({exFreq.Message})");
                                        ed.WriteMessage($"      ⚠ Tần suất cọc: {exFreq.Message}");
                                    }
                                }

                                // 3.2. Sao chép Mục tiêu thiết kế (Targets) với đối chiếu xác minh
                                if (copyTargets)
                                {
                                    try
                                    {
                                        SubassemblyTargetInfoCollection srcTargets = srcRegion.GetTargets();
                                        if (srcTargets != null && srcTargets.Count > 0)
                                        {
                                            int expectedTargetCount = 0;
                                            foreach (SubassemblyTargetInfo sti in srcTargets)
                                            {
                                                if (sti.TargetIds != null && sti.TargetIds.Count > 0)
                                                {
                                                    expectedTargetCount += sti.TargetIds.Count;
                                                }
                                            }

                                            totalTargetsExpected += expectedTargetCount;

                                            if (expectedTargetCount > 0)
                                            {
                                                bool applied = false;

                                                // Phương thức 1: Gán trực tiếp srcTargets vào newRegion
                                                try
                                                {
                                                    newRegion.SetTargets(srcTargets);
                                                    applied = true;
                                                }
                                                catch (Exception exDirect)
                                                {
                                                    ed.WriteMessage($"      ⚠ SetTargets trực tiếp không thành công ({exDirect.Message}), chuyển sang phương thức gán từng phần tử...");
                                                }

                                                // Phương thức 2: Fallback gán từng phần tử nếu Phương thức 1 gặp lỗi
                                                if (!applied)
                                                {
                                                    try
                                                    {
                                                        SubassemblyTargetInfoCollection newTargets = newRegion.GetTargets();
                                                        for (int i = 0; i < Math.Min(srcTargets.Count, newTargets.Count); i++)
                                                        {
                                                            var srcTgt = srcTargets[i];
                                                            var dstTgt = newTargets[i];

                                                            if (srcTgt.TargetIds != null && srcTgt.TargetIds.Count > 0)
                                                            {
                                                                var validIds = new ObjectIdCollection();
                                                                foreach (ObjectId tid in srcTgt.TargetIds)
                                                                {
                                                                    if (!tid.IsNull && tid.IsValid && !tid.IsErased)
                                                                    {
                                                                        validIds.Add(tid);
                                                                    }
                                                                }

                                                                if (validIds.Count > 0)
                                                                {
                                                                    dstTgt.TargetIds = validIds;
                                                                    try { dstTgt.TargetToOption = srcTgt.TargetToOption; } catch { }
                                                                }
                                                            }
                                                        }
                                                        newRegion.SetTargets(newTargets);
                                                        applied = true;
                                                    }
                                                    catch (Exception exFallback)
                                                    {
                                                        warningAndErrorList.Add($"Region '{newRegion.Name}': Lỗi gán Targets ({exFallback.Message})");
                                                        ed.WriteMessage($"      ❌ Lỗi fallback gán Targets: {exFallback.Message}");
                                                    }
                                                }

                                                // Đối chiếu xác minh kết quả gán Targets thực tế
                                                int verifiedTargetCount = 0;
                                                try
                                                {
                                                    var verifiedTargets = newRegion.GetTargets();
                                                    if (verifiedTargets != null)
                                                    {
                                                        foreach (SubassemblyTargetInfo vt in verifiedTargets)
                                                        {
                                                            if (vt.TargetIds != null && vt.TargetIds.Count > 0)
                                                            {
                                                                verifiedTargetCount += vt.TargetIds.Count;
                                                            }
                                                        }
                                                    }
                                                }
                                                catch { }

                                                totalTargetsCloned += verifiedTargetCount;

                                                if (verifiedTargetCount >= expectedTargetCount)
                                                {
                                                    ed.WriteMessage($"      • Đã sao chép & xác minh {verifiedTargetCount}/{expectedTargetCount} mục tiêu thiết kế (Targets) thành công.");
                                                }
                                                else
                                                {
                                                    string msg = $"Region '{newRegion.Name}': Chỉ sao chép được {verifiedTargetCount}/{expectedTargetCount} targets.";
                                                    warningAndErrorList.Add(msg);
                                                    ed.WriteMessage($"      ⚠ Cảnh báo: {msg}");
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception exTargets)
                                    {
                                        warningAndErrorList.Add($"Region '{newRegion.Name}': Lỗi xử lý Targets ({exTargets.Message})");
                                        ed.WriteMessage($"      ⚠ Mục tiêu thiết kế: {exTargets.Message}");
                                    }
                                }
                            }
                        }

                        // Kiểm tra tính toàn vẹn cấu trúc cơ bản: Phải tạo được ít nhất 1 Baseline và 1 Region
                        if (totalBaselinesCloned == 0 || totalRegionsCloned == 0)
                        {
                            ed.WriteMessage("\n\n❌ THẤT BẠI: Không thể tạo bất kỳ Baseline hoặc Region hợp lệ nào cho Corridor mới. Đang Rollback toàn bộ giao dịch...");
                            tr.Abort();
                            return;
                        }

                        // 4. Sao chép Bề mặt Corridor (Corridor Surfaces & Boundaries)
                        int surfacesCloned = 0;
                        if (copySurfaces && sourceCorridor.CorridorSurfaces.Count > 0)
                        {
                            ed.WriteMessage($"\n  🌐 Đang sao chép {sourceCorridor.CorridorSurfaces.Count} bề mặt Corridor...");

                            // Thu thập toàn bộ tên surface hiện có 1 lần duy nhất để tối ưu hiệu năng
                            var existingSurfaceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            try
                            {
                                foreach (ObjectId sId in cdoc.GetSurfaceIds())
                                {
                                    if (sId.IsNull || !sId.IsValid || sId.IsErased) continue;
                                    var s = tr.GetObject(sId, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Surface;
                                    if (s != null && !string.IsNullOrEmpty(s.Name))
                                    {
                                        existingSurfaceNames.Add(s.Name);
                                    }
                                }
                            }
                            catch { }

                            foreach (CorridorSurface srcSurf in sourceCorridor.CorridorSurfaces)
                            {
                                try
                                {
                                    string surfName = GenerateUniqueSurfaceName(existingSurfaceNames, srcSurf.Name, sourceCorridor.Name, newCorridorName, suffix);
                                    CorridorSurface newSurf = newCorridor.CorridorSurfaces.Add(surfName);
                                    existingSurfaceNames.Add(surfName);

                                    if (!srcSurf.SurfaceStyleId.IsNull && srcSurf.SurfaceStyleId.IsValid)
                                    {
                                        newSurf.SurfaceStyleId = srcSurf.SurfaceStyleId;
                                    }

                                    // Sao chép Overhang Correction
                                    try
                                    {
                                        newSurf.OverhangCorrection = srcSurf.OverhangCorrection;
                                    }
                                    catch (Exception exOh)
                                    {
                                        ed.WriteMessage($"    ⚠ Overhang correction '{surfName}': {exOh.Message}");
                                    }

                                    // Sao chép Link Codes với đúng trạng thái BreakLine
                                    string[] linkCodes = srcSurf.LinkCodes();
                                    if (linkCodes != null && linkCodes.Length > 0)
                                    {
                                        int addedCodes = 0;
                                        foreach (string code in linkCodes)
                                        {
                                            try
                                            {
                                                bool isBreakline = true;
                                                try
                                                {
                                                    isBreakline = srcSurf.IsLinkCodeAsBreakLine(code);
                                                }
                                                catch { }

                                                newSurf.AddLinkCode(code, isBreakline);
                                                addedCodes++;
                                            }
                                            catch (Exception exCode)
                                            {
                                                ed.WriteMessage($"      ⚠ Thêm link code '{code}': {exCode.Message}");
                                            }
                                        }
                                        ed.WriteMessage($"    ✓ Bề mặt: '{newSurf.Name}' (đã gán {addedCodes}/{linkCodes.Length} link codes)");
                                    }
                                    else
                                    {
                                        ed.WriteMessage($"    ✓ Bề mặt: '{newSurf.Name}' (không có link code)");
                                    }

                                    // Sao chép Feature Line Codes nếu có
                                    try
                                    {
                                        string[] flCodes = srcSurf.PointCodes();
                                        if (flCodes != null && flCodes.Length > 0)
                                        {
                                            int addedFl = 0;
                                            foreach (string code in flCodes)
                                            {
                                                try
                                                {
                                                    newSurf.AddFeatureLineCode(code);
                                                    addedFl++;
                                                }
                                                catch { }
                                            }
                                            if (addedFl > 0)
                                            {
                                                ed.WriteMessage($"      • Đã sao chép {addedFl} feature line codes.");
                                            }
                                        }
                                    }
                                    catch { }

                                    // Sao chép Boundaries với phân loại chính xác
                                    if (srcSurf.Boundaries != null && srcSurf.Boundaries.Count > 0)
                                    {
                                        int addedBnd = 0;
                                        foreach (CorridorSurfaceBoundary srcBnd in srcSurf.Boundaries)
                                        {
                                            try
                                            {
                                                string bndName = $"{srcBnd.Name}_{suffix.TrimStart('_')}";
                                                // Automatic Corridor Extents Outer boundary
                                                newSurf.Boundaries.AddCorridorExtentsBoundary(bndName);
                                                addedBnd++;
                                            }
                                            catch (Exception exBnd)
                                            {
                                                string bndWarn = $"Boundary '{srcBnd.Name}' của surface '{srcSurf.Name}': Không thể sao chép tự động ({exBnd.Message}). Vui lòng cấu hình trong Corridor Properties > Surfaces > Boundaries.";
                                                warningAndErrorList.Add(bndWarn);
                                                ed.WriteMessage($"      ⚠ {bndWarn}");
                                            }
                                        }
                                        if (addedBnd > 0)
                                        {
                                            ed.WriteMessage($"      • Đã gán {addedBnd} đường bao (Boundaries).");
                                        }
                                    }

                                    surfacesCloned++;
                                }
                                catch (Exception exSurf)
                                {
                                    string surfWarn = $"Không thể sao chép bề mặt '{srcSurf.Name}': {exSurf.Message}";
                                    warningAndErrorList.Add(surfWarn);
                                    ed.WriteMessage($"    ⚠ {surfWarn}");
                                }
                            }
                            ed.WriteMessage($"  ✓ Đã sao chép thành công {surfacesCloned}/{sourceCorridor.CorridorSurfaces.Count} bề mặt Corridor.");
                        }

                        // 5. Rebuild Corridor
                        bool rebuildSucceeded = false;
                        if (autoRebuild && totalBaselinesCloned > 0)
                        {
                            try
                            {
                                newCorridor.Rebuild();
                                rebuildSucceeded = true;
                                ed.WriteMessage($"\n\n✅ Đã Rebuild Corridor '{newCorridorName}' thành công!");
                            }
                            catch (Exception exRebuild)
                            {
                                warningAndErrorList.Add($"Rebuild Corridor: {exRebuild.Message}");
                                ed.WriteMessage($"\n⚠ Cảnh báo Rebuild: {exRebuild.Message}");
                            }
                        }

                        tr.Commit();

                        ed.WriteMessage("\n=======================================================");
                        if (warningAndErrorList.Count == 0 && (!autoRebuild || rebuildSucceeded))
                        {
                            ed.WriteMessage($"\n🎉 NHÂN BẢN CORRIDOR HOÀN TẤT TOÀN DIỆN (100%)!");
                        }
                        else
                        {
                            ed.WriteMessage($"\n⚠️ NHÂN BẢN CORRIDOR HOÀN TẤT MỘT PHẦN ({warningAndErrorList.Count} cảnh báo)");
                            foreach (string warn in warningAndErrorList)
                            {
                                ed.WriteMessage($"\n  - ⚠ {warn}");
                            }
                        }

                        ed.WriteMessage($"\n  • Tên mới:        {newCorridorName}");
                        ed.WriteMessage($"\n  • Số Baselines:   {totalBaselinesCloned}/{expectedBaselines}");
                        ed.WriteMessage($"\n  • Số Phân đoạn:   {totalRegionsCloned}/{expectedRegions}");
                        ed.WriteMessage($"\n  • Số Targets:     {totalTargetsCloned}/{totalTargetsExpected}");
                        ed.WriteMessage($"\n  • Số Bề mặt:      {surfacesCloned}/{sourceCorridor.CorridorSurfaces.Count}");
                        ed.WriteMessage("\n=======================================================\n");
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception eCad)
                    {
                        ed.WriteMessage($"\n❌ Lỗi AutoCAD: {eCad.Message}");
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

        private static string GenerateUniqueSurfaceName(HashSet<string> existingNames, string srcSurfName, string srcCorridorName, string newCorridorName, string suffix)
        {
            string candidate;
            if (!string.IsNullOrEmpty(srcCorridorName) && srcSurfName.IndexOf(srcCorridorName, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                int index = srcSurfName.IndexOf(srcCorridorName, StringComparison.OrdinalIgnoreCase);
                candidate = srcSurfName.Substring(0, index) + newCorridorName + srcSurfName.Substring(index + srcCorridorName.Length);
            }
            else if (!string.IsNullOrEmpty(suffix) && !srcSurfName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                candidate = $"{srcSurfName}{suffix}";
            }
            else
            {
                candidate = $"{srcSurfName}_Copy";
            }

            string finalName = candidate;
            int counter = 1;
            while (existingNames.Contains(finalName))
            {
                finalName = $"{candidate}_{counter}";
                counter++;
            }

            return finalName;
        }
    }
}

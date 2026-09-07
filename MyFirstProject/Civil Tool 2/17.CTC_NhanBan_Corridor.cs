using System;
using System.Collections.Generic;
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
        [CommandMethod("CTC_NhanBanCorridor")]
        [CommandMethod("NHANBANCORRIDOR")]
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
                ObjectId codeSetStyleId = form.SelectedCodeSetStyleId;
                bool copyTargets = form.CopyTargets;
                bool copyFrequencies = form.CopyFrequencies;
                bool copySurfaces = form.CopySurfaces;
                bool autoRebuild = form.AutoRebuild;

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    try
                    {
                        var sourceCorridor = tr.GetObject(sourceCorridorId, OpenMode.ForWrite) as Corridor;
                        if (sourceCorridor == null)
                        {
                            ed.WriteMessage("\n❌ Lỗi: Không thể truy xuất Corridor nguồn.");
                            tr.Abort();
                            return;
                        }

                        CivilDocument cdoc = CivilDocument.GetCivilDocument(db);

                        // 1. Kiểm tra trùng tên Corridor
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

                        ed.WriteMessage($"\n🔄 Đang nhân bản Corridor '{sourceCorridor.Name}' → '{newCorridorName}'...");

                        // 2. Tạo Corridor mới
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

                        int totalBaselinesCloned = 0;
                        int totalRegionsCloned = 0;
                        int totalTargetsCloned = 0;

                        // 3. Nhân bản từng Baseline & Region
                        for (int bi = 0; bi < sourceCorridor.Baselines.Count; bi++)
                        {
                            var srcBaseline = sourceCorridor.Baselines[bi];
                            Baseline? newBaseline = null;

                            try
                            {
                                if (!srcBaseline.IsFeatureLineBased())
                                {
                                    if (srcBaseline.AlignmentId.IsNull || srcBaseline.ProfileId.IsNull)
                                    {
                                        ed.WriteMessage($"\n  ⚠ Cảnh báo: Baseline '{srcBaseline.Name}' thiếu Alignment hoặc Profile. Bỏ qua.");
                                        continue;
                                    }

                                    newBaseline = newCorridor.Baselines.Add(srcBaseline.Name, srcBaseline.AlignmentId, srcBaseline.ProfileId);
                                }
                                else
                                {
                                    if (srcBaseline.FeatureLineId.IsNull)
                                    {
                                        ed.WriteMessage($"\n  ⚠ Cảnh báo: Baseline FeatureLine '{srcBaseline.Name}' không hợp lệ. Bỏ qua.");
                                        continue;
                                    }

                                    newBaseline = newCorridor.Baselines.Add(srcBaseline.Name, srcBaseline.FeatureLineId);
                                }
                            }
                            catch (Exception exBaseline)
                            {
                                ed.WriteMessage($"\n  ❌ Lỗi tạo Baseline '{srcBaseline.Name}': {exBaseline.Message}");
                                continue;
                            }

                            if (newBaseline == null) continue;
                            totalBaselinesCloned++;
                            ed.WriteMessage($"\n  ✓ Đã tạo Baseline [{totalBaselinesCloned}]: '{newBaseline.Name}'");

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
                                    ed.WriteMessage($"\n    ❌ Lỗi tạo Region '{srcRegion.Name}': {exRegion.Message}");
                                    continue;
                                }

                                if (newRegion == null) continue;
                                totalRegionsCloned++;
                                ed.WriteMessage($"\n    ✓ Phân đoạn: '{newRegion.Name}' ({newRegion.StartStation:F2}m – {newRegion.EndStation:F2}m)");

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
                                            foreach (double st in addStations)
                                            {
                                                try { newRegion.AddStation(st, "Copy"); } catch { }
                                            }
                                        }

                                        ed.WriteMessage($"      • Đã sao chép tần suất cọc (Tangents: {newSetting.FrequencyAlongTangents:F1}m, Curves: {newSetting.FrequencyAlongCurves:F1}m)");
                                    }
                                    catch (Exception exFreq)
                                    {
                                        ed.WriteMessage($"      ⚠ Tần suất cọc: {exFreq.Message}");
                                    }
                                }

                                // 3.2. Sao chép Mục tiêu thiết kế (Targets)
                                if (copyTargets)
                                {
                                    try
                                    {
                                        SubassemblyTargetInfoCollection srcTargets = srcRegion.GetTargets();
                                        SubassemblyTargetInfoCollection newTargets = newRegion.GetTargets();

                                        if (srcTargets != null && srcTargets.Count > 0 && newTargets != null && newTargets.Count > 0)
                                        {
                                            int copiedTargetCount = 0;

                                            foreach (SubassemblyTargetInfo srcTgt in srcTargets)
                                            {
                                                if (srcTgt.TargetIds == null || srcTgt.TargetIds.Count == 0) continue;

                                                foreach (SubassemblyTargetInfo newTgt in newTargets)
                                                {
                                                    if (string.Equals(newTgt.SubassemblyName, srcTgt.SubassemblyName, StringComparison.OrdinalIgnoreCase) &&
                                                        newTgt.TargetType == srcTgt.TargetType)
                                                    {
                                                        var targetIds = new ObjectIdCollection();
                                                        foreach (ObjectId tid in srcTgt.TargetIds)
                                                        {
                                                            if (!tid.IsNull && tid.IsValid && !tid.IsErased)
                                                            {
                                                                targetIds.Add(tid);
                                                            }
                                                        }

                                                        if (targetIds.Count > 0)
                                                        {
                                                            newTgt.TargetIds = targetIds;
                                                            newTgt.TargetToOption = srcTgt.TargetToOption;
                                                            copiedTargetCount++;
                                                        }
                                                        break;
                                                    }
                                                }
                                            }

                                            if (copiedTargetCount > 0)
                                            {
                                                newRegion.SetTargets(newTargets);
                                                totalTargetsCloned += copiedTargetCount;
                                                ed.WriteMessage($"      • Đã sao chép {copiedTargetCount} mục tiêu thiết kế (Targets).");
                                            }
                                        }
                                    }
                                    catch (Exception exTargets)
                                    {
                                        ed.WriteMessage($"      ⚠ Mục tiêu thiết kế: {exTargets.Message}");
                                    }
                                }
                            }
                        }

                        // 4. Sao chép Bề mặt Corridor (Corridor Surfaces & Boundaries)
                        if (copySurfaces && sourceCorridor.CorridorSurfaces.Count > 0)
                        {
                            ed.WriteMessage($"\n  🌐 Đang sao chép {sourceCorridor.CorridorSurfaces.Count} bề mặt Corridor...");
                            int surfacesCloned = 0;

                            foreach (CorridorSurface srcSurf in sourceCorridor.CorridorSurfaces)
                            {
                                try
                                {
                                    string surfName = $"{srcSurf.Name}_Copy";
                                    CorridorSurface newSurf = newCorridor.CorridorSurfaces.Add(surfName);

                                    if (!srcSurf.SurfaceStyleId.IsNull && srcSurf.SurfaceStyleId.IsValid)
                                    {
                                        newSurf.SurfaceStyleId = srcSurf.SurfaceStyleId;
                                    }

                                    // Sao chép Link Codes
                                    string[] linkCodes = srcSurf.LinkCodes();
                                    if (linkCodes != null)
                                    {
                                        foreach (string code in linkCodes)
                                        {
                                            try { newSurf.AddLinkCode(code, true); } catch { }
                                        }
                                    }

                                    surfacesCloned++;
                                    ed.WriteMessage($"    ✓ Bề mặt: '{newSurf.Name}'");
                                }
                                catch (Exception exSurf)
                                {
                                    ed.WriteMessage($"    ⚠ Bề mặt '{srcSurf.Name}': {exSurf.Message}");
                                }
                            }
                        }

                        // 5. Rebuild Corridor
                        if (autoRebuild && totalBaselinesCloned > 0)
                        {
                            try
                            {
                                newCorridor.Rebuild();
                                ed.WriteMessage($"\n\n✅ Đã Rebuild Corridor '{newCorridorName}' thành công!");
                            }
                            catch (Exception exRebuild)
                            {
                                ed.WriteMessage($"\n⚠ Cảnh báo Rebuild: {exRebuild.Message}");
                            }
                        }

                        tr.Commit();

                        ed.WriteMessage("\n=======================================================");
                        ed.WriteMessage($"\n🎉 NHÂN BẢN CORRIDOR HOÀN TẤT THÀNH CÔNG!");
                        ed.WriteMessage($"\n  • Tên mới:        {newCorridorName}");
                        ed.WriteMessage($"\n  • Số Baselines:   {totalBaselinesCloned}");
                        ed.WriteMessage($"\n  • Số Phân đoạn:   {totalRegionsCloned}");
                        ed.WriteMessage($"\n  • Số Targets:     {totalTargetsCloned}");
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
    }
}

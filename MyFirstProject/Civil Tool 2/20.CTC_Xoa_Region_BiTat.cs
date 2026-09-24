using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.Civil.DatabaseServices;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Exception = System.Exception;

[assembly: CommandClass(typeof(Civil3DCsharp.CTC_Xoa_Region_BiTat_Command))]

namespace Civil3DCsharp
{
    public class CTC_Xoa_Region_BiTat_Command
    {
        [CommandMethod("CTC_Xoa_Region_BiTat")]
        [CommandMethod("CTC_XoaRegionBiTat")]
        public static void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var db = doc.Database;
            var ed = doc.Editor;

            ed.WriteMessage("\n=======================================================");
            ed.WriteMessage("\n [CTC] QUẢN LÝ & XÓA CÁC CORRIDOR REGION ĐANG BỊ TẮT");
            ed.WriteMessage("\n=======================================================\n");

            using (var form = new MyFirstProject.Civil_Tool_2.XoaRegionBiTatForm(db, ed))
            {
                var dialogResult = Application.ShowModalDialog(form);

                if (dialogResult != DialogResult.OK || !form.FormAccepted)
                {
                    ed.WriteMessage("\nĐã hủy lệnh.");
                    return;
                }

                var regionsToDelete = form.SelectedRegionsToDelete;
                if (regionsToDelete == null || regionsToDelete.Count == 0)
                {
                    ed.WriteMessage("\nKhông có Region nào được chọn để xóa.");
                    return;
                }

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    try
                    {
                        int totalDeletedRegions = 0;
                        int totalDeletedBaselines = 0;
                        int totalAffectedCorridors = 0;

                        // Nhóm các Region cần xóa theo Corridor
                        var corridorGroups = regionsToDelete.GroupBy(r => r.CorridorId);

                        foreach (var corrGroup in corridorGroups)
                        {
                            var corrId = corrGroup.Key;
                            if (corrId.IsNull || !corrId.IsValid || corrId.IsErased) continue;

                            var corridor = tr.GetObject(corrId, OpenMode.ForWrite) as Corridor;
                            if (corridor == null) continue;

                            ed.WriteMessage($"\n--- Xử lý Corridor: '{corridor.Name}' ---");

                            int deletedInCorr = 0;
                            var emptyBaselinesToRemove = new List<Baseline>();

                            // Duyệt qua từng Baseline của Corridor (duyệt ngược để an toàn)
                            for (int bIdx = corridor.Baselines.Count - 1; bIdx >= 0; bIdx--)
                            {
                                var baseline = corridor.Baselines[bIdx];

                                // Lấy danh sách region cần xóa thuộc baseline này
                                var toDeleteInThisBaseline = corrGroup
                                    .Where(r => r.BaselineIndex == bIdx || r.BaselineName.StartsWith(baseline.Name, StringComparison.OrdinalIgnoreCase))
                                    .ToList();

                                if (toDeleteInThisBaseline.Count == 0) continue;

                                // Duyệt ngược các BaselineRegions để xóa
                                for (int rIdx = baseline.BaselineRegions.Count - 1; rIdx >= 0; rIdx--)
                                {
                                    var region = baseline.BaselineRegions[rIdx];

                                    // Khớp theo tên region hoặc lý trình
                                    var match = toDeleteInThisBaseline.FirstOrDefault(target =>
                                        target.RegionName.Equals(region.Name, StringComparison.OrdinalIgnoreCase) &&
                                        Math.Abs(target.StartStation - region.StartStation) < 0.005 &&
                                        Math.Abs(target.EndStation - region.EndStation) < 0.005);

                                    // Nếu không khớp tuyệt đối lý trình, kiểm tra khớp theo tên Region và trạng thái NeedsProcessing == false
                                    if (match == null)
                                    {
                                        match = toDeleteInThisBaseline.FirstOrDefault(target =>
                                            target.RegionName.Equals(region.Name, StringComparison.OrdinalIgnoreCase) &&
                                            !region.NeedsProcessing);
                                    }

                                    if (match != null)
                                    {
                                        string regName = region.Name;
                                        double stStart = region.StartStation;
                                        double stEnd = region.EndStation;

                                        baseline.BaselineRegions.Remove(region);
                                        deletedInCorr++;
                                        totalDeletedRegions++;

                                        ed.WriteMessage($"\n  [-] Đã xóa Region: '{regName}' ({stStart:F2} -> {stEnd:F2})");
                                    }
                                }

                                // Kiểm tra nếu baseline trở thành rỗng
                                if (form.DeleteEmptyBaseline && baseline.BaselineRegions.Count == 0)
                                {
                                    emptyBaselinesToRemove.Add(baseline);
                                }
                            }

                            // Xóa các baseline rỗng nếu người dùng bật tùy chọn
                            if (emptyBaselinesToRemove.Count > 0)
                            {
                                foreach (var bl in emptyBaselinesToRemove)
                                {
                                    string blName = bl.Name;
                                    corridor.Baselines.Remove(bl);
                                    totalDeletedBaselines++;
                                    ed.WriteMessage($"\n  [x] Đã xóa Baseline rỗng: '{blName}'");
                                }
                            }

                            // Rebuild Corridor nếu được chọn
                            if (form.AutoRebuild && deletedInCorr > 0)
                            {
                                ed.WriteMessage($"\n🔄 Đang Rebuild Corridor '{corridor.Name}'...");
                                try
                                {
                                    corridor.Rebuild();
                                    ed.WriteMessage($"\n✅ Đã Rebuild Corridor '{corridor.Name}' thành công!");
                                }
                                catch (Exception exRebuild)
                                {
                                    ed.WriteMessage($"\n⚠ Rebuild cảnh báo: {exRebuild.Message}");
                                }
                            }

                            if (deletedInCorr > 0)
                            {
                                totalAffectedCorridors++;
                            }
                        }

                        tr.Commit();

                        ed.WriteMessage("\n-------------------------------------------------------");
                        ed.WriteMessage($"\n✅ HOÀN TẤT: Đã xóa thành công {totalDeletedRegions} Region bị tắt trong {totalAffectedCorridors} Corridor.");
                        if (totalDeletedBaselines > 0)
                        {
                            ed.WriteMessage($"\n   (Đồng thời xóa {totalDeletedBaselines} Baseline rỗng)");
                        }
                        ed.WriteMessage("\n=======================================================\n");
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
    }
}

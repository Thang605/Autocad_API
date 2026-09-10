using System;
using System.Linq;
using System.Windows.Forms;

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.Civil.DatabaseServices;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Exception = System.Exception;

[assembly: CommandClass(typeof(Civil3DCsharp.CTC_BatTat_CorridorRegion_Polyline_Command))]

namespace Civil3DCsharp
{
    public class CTC_BatTat_CorridorRegion_Polyline_Command
    {
        [CommandMethod("CTC_BatTat_CorridorRegion_Polyline")]
        public static void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var db = doc.Database;
            var ed = doc.Editor;

            ed.WriteMessage("\n=======================================================");
            ed.WriteMessage("\n [CTC] CHIA NHỎ & BẬT TẮT CORRIDOR REGION THEO POLYLINE");
            ed.WriteMessage("\n=======================================================\n");

            using (var form = new MyFirstProject.Civil_Tool_2.BatTatCorridorRegionPolylineForm(db, ed))
            {
                var dialogResult = Application.ShowModalDialog(form);

                if (dialogResult != DialogResult.OK || !form.FormAccepted)
                {
                    ed.WriteMessage("\nĐã hủy lệnh.");
                    return;
                }

                if (form.SelectedCorridorId.IsNull || !form.SelectedCorridorId.IsValid)
                {
                    ed.WriteMessage("\nLỗi: Không có Corridor nào được chọn.");
                    return;
                }

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    try
                    {
                        var corridor = tr.GetObject(form.SelectedCorridorId, OpenMode.ForWrite) as Corridor;
                        if (corridor == null)
                        {
                            ed.WriteMessage("\nLỗi: Không tìm thấy Corridor trong bản vẽ.");
                            return;
                        }

                        int changedCount = 0;
                        int disabledCount = 0;
                        int enabledCount = 0;

                        // Cập nhật trạng thái NeedsProcessing của từng Region
                        foreach (var item in form.RegionItems)
                        {
                            if (item.BaselineIndex < 0 || item.BaselineIndex >= corridor.Baselines.Count) continue;
                            var baseline = corridor.Baselines[item.BaselineIndex];

                            if (item.RegionIndex < 0 || item.RegionIndex >= baseline.BaselineRegions.Count) continue;
                            var region = baseline.BaselineRegions[item.RegionIndex];

                            if (region.NeedsProcessing != item.IsEnabled)
                            {
                                region.NeedsProcessing = item.IsEnabled;
                                changedCount++;

                                if (item.IsEnabled)
                                {
                                    enabledCount++;
                                    ed.WriteMessage($"\n  [+] BẬT Region: '{region.Name}' ({region.StartStation:F2} - {region.EndStation:F2})");
                                }
                                else
                                {
                                    disabledCount++;
                                    ed.WriteMessage($"\n  [-] TẮT (Bypass) Region: '{region.Name}' ({region.StartStation:F2} - {region.EndStation:F2})");
                                }
                            }
                        }

                        // Rebuild Corridor nếu được chọn
                        if (form.AutoRebuild)
                        {
                            ed.WriteMessage($"\n\n🔄 Đang Rebuild Corridor '{corridor.Name}'...");
                            try
                            {
                                corridor.Rebuild();
                                ed.WriteMessage("\n✅ Rebuild Corridor thành công!");
                            }
                            catch (Exception exRebuild)
                            {
                                ed.WriteMessage($"\n⚠ Rebuild cảnh báo: {exRebuild.Message}");
                            }
                        }

                        tr.Commit();

                        ed.WriteMessage("\n-------------------------------------------------------");
                        ed.WriteMessage($"\n✅ Hoàn tất: Đã cập nhật {changedCount} Region ({disabledCount} tắt, {enabledCount} bật).");
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

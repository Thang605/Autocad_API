using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.Civil.DatabaseServices;
using MyFirstProject.Civil_Tool_2;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(Civil3DCsharp.CTC_ThemSection_TatCaRegion_Commands))]

namespace Civil3DCsharp
{
    /// <summary>
    /// Lệnh thêm Section / Cập nhật Tần suất lấy mẫu cho tất cả các Region của Corridor theo con số input
    /// </summary>
    public class CTC_ThemSection_TatCaRegion_Commands
    {
        [CommandMethod("CTC_ThemSection_TatCaRegion")]
        [CommandMethod("CTC_ThemSection")]
        [CommandMethod("CTC_AddSection_AllRegions")]
        public static void CTC_ThemSection_TatCaRegion()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var db = doc.Database;
            var ed = doc.Editor;

            ed.WriteMessage("\n=======================================================");
            ed.WriteMessage("\n  LỆNH THÊM SECTION / THIẾT LẬP TẦN SUẤT CHO CORRIDOR  ");
            ed.WriteMessage("\n=======================================================\n");

            using (var form = new ThemSectionCorridorForm())
            {
                var dialogResult = Application.ShowModalDialog(form);
                if (dialogResult != DialogResult.OK || !form.FormAccepted || form.SelectedRegions.Count == 0)
                {
                    ed.WriteMessage("\n[Thông báo] Đã hủy lệnh hoặc không có phân đoạn nào được chọn.");
                    return;
                }

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    try
                    {
                        var corridor = tr.GetObject(form.SelectedCorridorId, OpenMode.ForWrite) as Corridor;
                        if (corridor == null)
                        {
                            ed.WriteMessage("\n[Lỗi] Không tìm thấy hoặc không mở được đối tượng Corridor để ghi.");
                            return;
                        }

                        int updatedRegions = 0;
                        int addedExplicitStationsCount = 0;
                        double interval = form.SectionInterval;

                        ed.WriteMessage($"\nĐang xử lý Corridor: \"{corridor.Name}\" với khoảng cách Section: {interval:F2}m...");

                        foreach (var regInfo in form.SelectedRegions)
                        {
                            if (regInfo.BaselineIndex < 0 || regInfo.BaselineIndex >= corridor.Baselines.Count)
                                continue;

                            var baseline = corridor.Baselines[regInfo.BaselineIndex];
                            if (regInfo.RegionIndex < 0 || regInfo.RegionIndex >= baseline.BaselineRegions.Count)
                                continue;

                            var region = baseline.BaselineRegions[regInfo.RegionIndex];

                            // 1. Cập nhật Tần suất lấy mẫu (Frequency Settings)
                            if (form.UpdateFrequencySettings)
                            {
                                region.AppliedAssemblySetting.FrequencyAlongTangents = interval;
                                region.AppliedAssemblySetting.FrequencyAlongCurves = interval;
                                region.AppliedAssemblySetting.FrequencyAlongSpirals = interval;
                                region.AppliedAssemblySetting.FrequencyAlongProfileCurves = interval;
                                region.AppliedAssemblySetting.FrequencyAlongTargetCurves = interval;

                                region.AppliedAssemblySetting.AppliedAtHorizontalGeometryPoints = form.ApplyHorizGeom;
                                region.AppliedAssemblySetting.AppliedAtProfileGeometryPoints = form.ApplyProfileGeom;
                                region.AppliedAssemblySetting.AppliedAtProfileHighLowPoints = form.ApplyProfileHighLow;
                                region.AppliedAssemblySetting.AppliedAtSuperelevationCriticalPoints = form.ApplySuperelevation;
                                region.AppliedAssemblySetting.AppliedAtOffsetTargetGeometryPoints = form.ApplyOffsetTarget;
                            }

                            // 2. Chèn các Station bổ sung cố định (Add Stations)
                            if (form.AddExplicitStations)
                            {
                                var existingStations = new HashSet<double>(region.SortedStations());

                                double currentStation = region.StartStation;
                                while (currentStation <= region.EndStation + 0.0001)
                                {
                                    bool exists = existingStations.Any(s => Math.Abs(s - currentStation) < 0.001);
                                    if (!exists)
                                    {
                                        try
                                        {
                                            region.AddStation(currentStation, $"Sec_{interval:F1}m");
                                            addedExplicitStationsCount++;
                                            existingStations.Add(currentStation);
                                        }
                                        catch
                                        {
                                            // Bỏ qua lỗi nếu trạm không hợp lệ với geometry
                                        }
                                    }

                                    currentStation += interval;
                                }
                            }

                            updatedRegions++;
                            ed.WriteMessage($"\n  ✓ Đã cập nhật phân đoạn \"{region.Name}\" (Lý trình: {region.StartStation:F2} -> {region.EndStation:F2})");
                        }

                        // 3. Rebuild Corridor nếu được yêu cầu
                        if (form.RebuildAfterExecution)
                        {
                            ed.WriteMessage("\nĐang Rebuild Corridor...");
                            corridor.Rebuild();
                            ed.WriteMessage(" ✓ Hoàn tất Rebuild Corridor.");
                        }

                        tr.Commit();

                        ed.WriteMessage("\n-------------------------------------------------------");
                        ed.WriteMessage($"\n[Thành công] Đã xử lý {updatedRegions} phân đoạn của Corridor '{corridor.Name}'.");
                        if (form.UpdateFrequencySettings)
                        {
                            ed.WriteMessage($"\n - Tần suất lấy mẫu: {interval:F2} m");
                        }
                        if (form.AddExplicitStations)
                        {
                            ed.WriteMessage($"\n - Số Section bổ sung đã chèn: {addedExplicitStationsCount}");
                        }
                        ed.WriteMessage("\n=======================================================\n");
                    }
                    catch (System.Exception ex)
                    {
                        tr.Abort();
                        ed.WriteMessage($"\n[Lỗi ngoại lệ] Không thể hoàn thành lệnh: {ex.Message}");
                    }
                }
            }
        }
    }
}

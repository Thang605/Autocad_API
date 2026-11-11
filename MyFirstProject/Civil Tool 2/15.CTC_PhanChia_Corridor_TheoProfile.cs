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

[assembly: CommandClass(typeof(Civil3DCsharp.CTC_PhanChia_Corridor_TheoProfile_Command))]

namespace Civil3DCsharp
{
    public class CTC_PhanChia_Corridor_TheoProfile_Command
    {
        [CommandMethod("CTC_Phan_Chia_Corridor_Theo_Profile")]
        public static void CTC_PhanChia_Corridor_TheoProfile()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            ed.WriteMessage("\n=== Lệnh: Chia Corridor thành nhiều đoạn theo điểm pick trên Trắc dọc ===");

            using (var form = new MyFirstProject.Civil_Tool_2.PhanChiaCorridorForm(db, ed))
            {
                var dialogResult = Application.ShowModalDialog(form);

                if (dialogResult != DialogResult.OK || !form.FormAccepted)
                {
                    ed.WriteMessage("\nĐã hủy lệnh.");
                    return;
                }

                ObjectId corridorId = form.SelectedCorridorId;
                List<double> splitStations = form.SplitStations;

                // Sắp xếp các lý trình chia từ nhỏ đến lớn
                splitStations.Sort();

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    try
                    {
                        var corridor = tr.GetObject(corridorId, OpenMode.ForWrite) as Corridor;
                        if (corridor == null)
                        {
                            ed.WriteMessage("\nLỗi: Không tìm thấy Corridor.");
                            return;
                        }

                        // Ở đây ta đơn giản áp dụng chia trên tất cả các Baseline
                        // Trong thực tế Corridor có thể có nhiều Baseline, ta sẽ check từng Baseline
                        bool anySplit = false;

                        foreach (Baseline baseline in corridor.Baselines)
                        {
                            foreach (double splitStation in splitStations)
                            {
                                // Tìm Region chứa lý trình cắt này
                                BaselineRegion regionToSplit = null;

                                // Lấy các Region của Baseline hiện tại
                                // Lưu ý: Do ta có thể vừa xoá/thêm Region trong collection, ta cần duyệt cẩn thận.
                                // Dùng list phụ để duyệt.
                                var regions = new List<BaselineRegion>();
                                foreach (BaselineRegion r in baseline.BaselineRegions)
                                {
                                    regions.Add(r);
                                }

                                foreach (var region in regions)
                                {
                                    // Kiểm tra xem splitStation có nằm hẳn bên trong Region không (tránh trùng điểm đầu/cuối)
                                    if (splitStation > region.StartStation + 0.001 && splitStation < region.EndStation - 0.001)
                                    {
                                        regionToSplit = region;
                                        break;
                                    }
                                }

                                if (regionToSplit != null)
                                {
                                    ed.WriteMessage($"\nĐang chia Region '{regionToSplit.Name}' tại lý trình {splitStation:F3}...");
                                    
                                    double originalEndStation = regionToSplit.EndStation;
                                    ObjectId assemblyId = regionToSplit.AssemblyId;
                                    string originalName = regionToSplit.Name;

                                    // 1. Cập nhật lý trình kết thúc của Region hiện tại thành điểm chia
                                    regionToSplit.EndStation = splitStation;

                                    // 2. Tạo Region mới từ điểm chia đến lý trình kết thúc ban đầu
                                    string newRegionName = $"{originalName}_Split_{Math.Round(splitStation)}";
                                    
                                    // Đảm bảo tên không bị trùng
                                    int counter = 1;
                                    string tempName = newRegionName;
                                    while (true)
                                    {
                                        bool isExist = false;
                                        foreach (BaselineRegion r in baseline.BaselineRegions)
                                        {
                                            if (r.Name.Equals(tempName, StringComparison.OrdinalIgnoreCase))
                                            {
                                                isExist = true;
                                                break;
                                            }
                                        }
                                        if (!isExist) break;
                                        
                                        tempName = $"{newRegionName}_{counter}";
                                        counter++;
                                    }
                                    newRegionName = tempName;

                                    try
                                    {
                                        baseline.BaselineRegions.Add(newRegionName, assemblyId, splitStation, originalEndStation);
                                        anySplit = true;
                                        ed.WriteMessage($"\n  -> Đã tạo Region mới: '{newRegionName}' ({splitStation:F3} đến {originalEndStation:F3})");
                                    }
                                    catch (Exception ex)
                                    {
                                        ed.WriteMessage($"\n  -> Lỗi khi tạo Region mới: {ex.Message}");
                                    }
                                }
                            }
                        }

                        if (anySplit)
                        {
                            corridor.Rebuild();
                            ed.WriteMessage("\n✅ Đã chia Corridor thành công!");
                        }
                        else
                        {
                            ed.WriteMessage("\n⚠ Không có đoạn nào được chia (Lý trình nằm ngoài phạm vi các Region).");
                        }

                        tr.Commit();
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception e)
                    {
                        ed.WriteMessage($"\nLỗi AutoCAD: {e.Message}");
                        tr.Abort();
                    }
                    catch (Exception ex)
                    {
                        ed.WriteMessage($"\nLỗi hệ thống: {ex.Message}");
                        tr.Abort();
                    }
                }
            }
        }
    }
}

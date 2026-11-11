// (C) Copyright 2024 by  
//
// CTC_DieuChinh_PhanDoan Command for Civil 3D
// This file contains the CTC_DieuChinh_PhanDoan command for adjusting corridor region start and end points
// 
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using System;
using System.Collections.Generic;

// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3DCsharp.CTC_DieuChinh_PhanDoan_Commands))]

namespace Civil3DCsharp
{
    /// <summary>
    /// Class containing CTC_DieuChinh_PhanDoan command for Civil 3D
    /// </summary>
    public class CTC_DieuChinh_PhanDoan_Commands
    {
        // Lệnh điều chỉnh phân đoạn corridor
        [CommandMethod("CTC_Dieu_Chinh_Phan_Doan")]
        public static void CTC_DieuChinh_PhanDoan()
        {
            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            using Transaction tr = db.TransactionManager.StartTransaction();
            try
            {
                // 1. Chọn Corridor
                ed.WriteMessage("\nChọn Corridor để điều chỉnh region:");
                var corridorOptions = new PromptEntityOptions("\nChọn Corridor: ");
                corridorOptions.SetRejectMessage("\nĐối tượng không phải là Corridor.");
                corridorOptions.AddAllowedClass(typeof(Corridor), true);

                var corridorResult = ed.GetEntity(corridorOptions);
                if (corridorResult.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\nKhông có Corridor nào được chọn!");
                    return;
                }

                Corridor corridor = (Corridor)tr.GetObject(corridorResult.ObjectId, OpenMode.ForWrite);
                
                // Hiển thị danh sách baselines và regions
                ed.WriteMessage($"\nCorridor: {corridor.Name}");
                ed.WriteMessage("\nDanh sách các Baseline và Region:");
                
                if (corridor.Baselines.Count == 0)
                {
                    ed.WriteMessage("\nCorridor không có baseline nào!");
                    return;
                }

                var allRegions = new List<(Baseline baseline, BaselineRegion region, int baselineIndex, int regionIndex)>();
                int displayIndex = 1;

                for (int baselineIndex = 0; baselineIndex < corridor.Baselines.Count; baselineIndex++)
                {
                    var baseline = corridor.Baselines[baselineIndex];
                    ed.WriteMessage($"\n  Baseline: {baseline.Name}");
                    
                    for (int regionIndex = 0; regionIndex < baseline.BaselineRegions.Count; regionIndex++)
                    {
                        var region = baseline.BaselineRegions[regionIndex];
                        ed.WriteMessage($"\n    {displayIndex}. {region.Name} (Station: {region.StartStation:F3} - {region.EndStation:F3})");
                        allRegions.Add((baseline, region, baselineIndex, regionIndex));
                        displayIndex++;
                    }
                }

                if (allRegions.Count == 0)
                {
                    ed.WriteMessage("\nCorridor không có region nào!");
                    return;
                }

                // 2. Chọn region để điều chỉnh
                var regionOptions = new PromptIntegerOptions($"\nNhập số thứ tự region muốn điều chỉnh (1-{allRegions.Count}): ")
                {
                    AllowNegative = false,
                    AllowZero = false,
                    LowerLimit = 1,
                    UpperLimit = allRegions.Count
                };

                var regionResult = ed.GetInteger(regionOptions);
                if (regionResult.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\nLựa chọn region không hợp lệ!");
                    return;
                }

                var selectedRegionInfo = allRegions[regionResult.Value - 1];
                var selectedRegion = selectedRegionInfo.region;
                var selectedBaseline = selectedRegionInfo.baseline;
                ed.WriteMessage($"\nĐã chọn region: {selectedRegion.Name} (Baseline: {selectedBaseline.Name})");

                // Lấy alignment reference của baseline
                Alignment baselineAlignment = (Alignment)tr.GetObject(selectedBaseline.AlignmentId, OpenMode.ForRead);

                // Hiển thị thông tin chi tiết về phạm vi
                ed.WriteMessage($"\n=== THÔNG TIN ALIGNMENT ===");
                ed.WriteMessage($"\nAlignment: {baselineAlignment.Name}");
                ed.WriteMessage($"\nPhạm vi alignment: {baselineAlignment.StartingStation:F3} - {baselineAlignment.EndingStation:F3}");
                ed.WriteMessage($"\nRegion hiện tại: {selectedRegion.StartStation:F3} - {selectedRegion.EndStation:F3}");
                
                // Hiển thị phạm vi của tất cả regions trong baseline
                ed.WriteMessage($"\n=== TẤT CẢ REGIONS TRONG BASELINE ===");
                for (int i = 0; i < selectedBaseline.BaselineRegions.Count; i++)
                {
                    var region = selectedBaseline.BaselineRegions[i];
                    ed.WriteMessage($"\n  Region {i+1}: {region.StartStation:F3} - {region.EndStation:F3}");
                }
                ed.WriteMessage($"\n========================");
                
                // 3. Chọn điểm đầu mới
                ed.WriteMessage("\nChọn điểm đầu mới cho region:");
                var startPointOptions = new PromptPointOptions("\nChọn điểm đầu: ");
                var startPointResult = ed.GetPoint(startPointOptions);
                if (startPointResult.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\nKhông có điểm đầu nào được chọn!");
                    return;
                }

                Point3d startPoint = startPointResult.Value;
                double newStartStation = 0, startOffset = 0;
                baselineAlignment.StationOffset(startPoint.X, startPoint.Y, ref newStartStation, ref startOffset);
                ed.WriteMessage($"\nStation điểm đầu: {newStartStation:F3} m");

                // 4. Chọn điểm cuối mới
                ed.WriteMessage("\nChọn điểm cuối mới cho region:");
                var endPointOptions = new PromptPointOptions("\nChọn điểm cuối: ");
                var endPointResult = ed.GetPoint(endPointOptions);
                if (endPointResult.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\nKhông có điểm cuối nào được chọn!");
                    return;
                }

                Point3d endPoint = endPointResult.Value;
                double newEndStation = 0, endOffset = 0;
                baselineAlignment.StationOffset(endPoint.X, endPoint.Y, ref newEndStation, ref endOffset);
                ed.WriteMessage($"\nStation điểm cuối: {newEndStation:F3} m");

                // Kiểm tra cơ bản
                if (newStartStation >= newEndStation)
                {
                    ed.WriteMessage("\nLỗi: Station đầu phải nhỏ hơn station cuối!");
                    return;
                }

                ed.WriteMessage($"\nStation yêu cầu: {newStartStation:F3} - {newEndStation:F3}");

                // 5. Cập nhật ngay lập tức mà không cần xác nhận
                ed.WriteMessage($"\nĐang cập nhật region '{selectedRegion.Name}'...");
                ed.WriteMessage($"\n  Station cũ: {selectedRegion.StartStation:F3} - {selectedRegion.EndStation:F3}");
                ed.WriteMessage($"\n  Station mới: {newStartStation:F3} - {newEndStation:F3}");

                // 6. Thực hiện thay đổi region với phương pháp 2 bước
                try
                {
                    // Kiểm tra xem có cần phương pháp 2 bước không
                    bool needsTwoStepUpdate = newStartStation < selectedRegion.StartStation || 
                                             newEndStation > selectedRegion.EndStation ||
                                             newStartStation < baselineAlignment.StartingStation || 
                                             newEndStation > baselineAlignment.EndingStation;
                    
                    if (needsTwoStepUpdate)
                    {
                        ed.WriteMessage("\n🔄 Sử dụng phương pháp cập nhật 2 bước...");
                        ed.WriteMessage($"\n  Region hiện tại: {selectedRegion.StartStation:F3} - {selectedRegion.EndStation:F3}");
                        ed.WriteMessage($"\n  Mục tiêu cuối cùng: {newStartStation:F3} - {newEndStation:F3}");
                        
                        // Bước 1: Tạo phạm vi tạm thời
                        double tempStartStation = Math.Min(selectedRegion.StartStation, newStartStation);
                        double tempEndStation = Math.Max(selectedRegion.EndStation, newEndStation);
                        
                        // Đảm bảo phạm vi tạm thời nằm trong alignment (nếu có)
                        tempStartStation = Math.Max(tempStartStation, baselineAlignment.StartingStation);
                        tempEndStation = Math.Min(tempEndStation, baselineAlignment.EndingStation);
                        
                        ed.WriteMessage($"\n  Bước 1 - Phạm vi tạm thời: {tempStartStation:F3} - {tempEndStation:F3}");
                        
                        try
                        {
                            // Cập nhật lần 1: Mở rộng về phạm vi tạm thời
                            selectedRegion.StartStation = tempStartStation;
                            selectedRegion.EndStation = tempEndStation;
                            
                            ed.WriteMessage("\n  ✓ Bước 1 thành công: Đã mở rộng region tạm thời");
                            
                            // Thử rebuild sau bước 1
                            try
                            {
                                corridor.Rebuild();
                                ed.WriteMessage("\n  ✓ Rebuild sau bước 1 thành công");
                            }
                            catch (System.Exception rebuildEx1)
                            {
                                ed.WriteMessage($"\n  ⚠ Rebuild sau bước 1 thất bại: {rebuildEx1.Message}");
                                ed.WriteMessage("\n  ⚠ Tiếp tục với bước 2...");
                            }
                            
                            // Bước 2: Cập nhật về phạm vi cuối cùng
                            ed.WriteMessage($"\n  Bước 2 - Cập nhật về phạm vi cuối: {newStartStation:F3} - {newEndStation:F3}");
                            
                            selectedRegion.StartStation = newStartStation;
                            selectedRegion.EndStation = newEndStation;
                            
                            ed.WriteMessage("\n  ✓ Bước 2 thành công: Đã cập nhật về phạm vi cuối cùng");
                            
                            // Rebuild cuối cùng
                            try
                            {
                                corridor.Rebuild();
                                ed.WriteMessage("\n✅ Hoàn thành cập nhật 2 bước và rebuild thành công!");
                            }
                            catch (System.Exception rebuildEx2)
                            {
                                ed.WriteMessage($"\n⚠ Rebuild cuối cùng thất bại: {rebuildEx2.Message}");
                                ed.WriteMessage("\n✓ Region đã được cập nhật, có thể rebuild thủ công sau.");
                            }
                        }
                        catch (System.Exception step1Ex)
                        {
                            ed.WriteMessage($"\n✗ Bước 1 thất bại: {step1Ex.Message}");
                            ed.WriteMessage("\n🔄 Thử phương pháp tạo region mới...");
                            
                            // Fallback: Tạo region mới
                            try
                            {
                                var newRegion = CreateExtendedRegion(selectedBaseline, selectedRegion, newStartStation, newEndStation, ed);
                                selectedBaseline.BaselineRegions.Remove(selectedRegion);
                                
                                corridor.Rebuild();
                                ed.WriteMessage("\n✅ Đã tạo region mới thành công!");
                                tr.Commit();
                                return;
                            }
                            catch (System.Exception createEx)
                            {
                                ed.WriteMessage($"\n✗ Tạo region mới cũng thất bại: {createEx.Message}");
                                throw;
                            }
                        }
                    }
                    else
                    {
                        // Cập nhật trực tiếp cho trường hợp đơn giản
                        ed.WriteMessage("\n🔄 Cập nhật trực tiếp...");
                        
                        selectedRegion.StartStation = newStartStation;
                        selectedRegion.EndStation = newEndStation;
                        
                        ed.WriteMessage($"\n✓ Đã cập nhật station cho region '{selectedRegion.Name}'!");
                        
                        // Rebuild
                        try
                        {
                            corridor.Rebuild();
                            ed.WriteMessage("\n✅ Corridor đã được rebuild thành công!");
                        }
                        catch (System.Exception rebuildEx)
                        {
                            ed.WriteMessage($"\n⚠ Rebuild thất bại: {rebuildEx.Message}");
                            ed.WriteMessage("\n✓ Region đã được cập nhật, có thể rebuild thủ công sau.");
                        }
                    }
                    
                    ed.WriteMessage($"\n✅ Station cuối cùng: {newStartStation:F3} - {newEndStation:F3}");
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage($"\n✗ Lỗi khi cập nhật region: {ex.Message}");
                    
                    // Phương pháp cuối cùng: Force update với nhiều bước nhỏ
                    try
                    {
                        ed.WriteMessage("\n🔄 Thử phương pháp force update từng bước nhỏ...");
                        
                        // Chia nhỏ việc cập nhật thành nhiều bước
                        double currentStart = selectedRegion.StartStation;
                        double currentEnd = selectedRegion.EndStation;
                        
                        // Tính toán các bước trung gian
                        int steps = 3; // Chia thành 3 bước
                        for (int i = 1; i <= steps; i++)
                        {
                            double progress = (double)i / steps;
                            double intermediateStart = currentStart + (newStartStation - currentStart) * progress;
                            double intermediateEnd = currentEnd + (newEndStation - currentEnd) * progress;
                            
                            ed.WriteMessage($"\n  Bước {i}/{steps}: {intermediateStart:F3} - {intermediateEnd:F3}");
                            
                            try
                            {
                                selectedRegion.StartStation = intermediateStart;
                                selectedRegion.EndStation = intermediateEnd;
                                
                                // Chỉ rebuild ở bước cuối
                                if (i == steps)
                                {
                                    corridor.Rebuild();
                                }
                                
                                ed.WriteMessage($"\n    ✓ Bước {i} thành công");
                            }
                            catch (System.Exception stepEx)
                            {
                                ed.WriteMessage($"\n    ✗ Bước {i} thất bại: {stepEx.Message}");
                                if (i == steps)
                                {
                                    throw; // Chỉ throw ở bước cuối
                                }
                            }
                        }
                        
                        ed.WriteMessage("\n✅ Force update từng bước thành công!");
                    }
                    catch (System.Exception forceEx)
                    {
                        ed.WriteMessage($"\n✗ Tất cả phương pháp đều thất bại: {forceEx.Message}");
                        ed.WriteMessage("\n💡 Gợi ý giải pháp:");
                        ed.WriteMessage("1. Kiểm tra và mở rộng alignment thủ công");
                        ed.WriteMessage("2. Tạo alignment mới với phạm vi đủ rộng");
                        ed.WriteMessage("3. Chia nhỏ việc điều chỉnh thành nhiều lần");
                        ed.WriteMessage("4. Sử dụng Civil 3D interface để điều chỉnh");
                        return;
                    }
                }

                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                ed.WriteMessage($"\nLỗi: {e.Message}");
                tr.Abort();
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\nLỗi hệ thống: {ex.Message}");
                tr.Abort();
            }
        }

        /// <summary>
        /// Helper method to extend alignment range if needed
        /// </summary>
        private static bool TryExtendAlignment(Alignment alignment, double requiredStartStation, double requiredEndStation, Editor ed)
        {
            try
            {
                ed.WriteMessage($"\n🔧 Đang thử mở rộng alignment...");
                ed.WriteMessage($"\n  Hiện tại: {alignment.StartingStation:F3} - {alignment.EndingStation:F3}");
                ed.WriteMessage($"\n  Yêu cầu: {requiredStartStation:F3} - {requiredEndStation:F3}");
                
                // Hiện tại chỉ thông báo, việc mở rộng alignment phức tạp và cần nhiều logic
                // Trong thực tế, có thể cần sử dụng alignment editing APIs khác
                ed.WriteMessage("\n⚠ Mở rộng alignment tự động chưa được implement đầy đủ");
                ed.WriteMessage("\n⚠ Sẽ sử dụng phương pháp khác...");
                
                return false;
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n✗ Lỗi khi mở rộng alignment: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Alternative method to create a new region when current region cannot be extended
        /// </summary>
        private static BaselineRegion CreateExtendedRegion(Baseline baseline, BaselineRegion originalRegion, 
            double newStartStation, double newEndStation, Editor ed)
        {
            try
            {
                string newRegionName = originalRegion.Name + "_Extended_" + DateTime.Now.ToString("HHmmss");
                ObjectId assemblyId = originalRegion.AssemblyId;
                
                ed.WriteMessage($"\n🔧 Đang tạo region mới: {newRegionName}");
                
                // Tạo region mới với phương pháp 2 bước nếu cần
                BaselineRegion newRegion;
                
                // Lấy alignment để kiểm tra phạm vi (sử dụng database từ alignment object)
                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    var alignment = (Alignment)tr.GetObject(baseline.AlignmentId, OpenMode.ForRead);
                    
                    // Kiểm tra xem có cần 2 bước không
                    bool needsTwoStep = newStartStation < alignment.StartingStation || 
                                       newEndStation > alignment.EndingStation;
                    
                    if (needsTwoStep)
                    {
                        ed.WriteMessage("\n  🔄 Sử dụng phương pháp 2 bước cho region mới...");
                        
                        // Bước 1: Tạo với phạm vi tạm thời
                        double tempStart = Math.Max(newStartStation, alignment.StartingStation);
                        double tempEnd = Math.Min(newEndStation, alignment.EndingStation);
                        
                        // Đảm bảo phạm vi hợp lệ
                        if (tempStart >= tempEnd)
                        {
                            tempStart = Math.Min(originalRegion.StartStation, newStartStation);
                            tempEnd = Math.Max(originalRegion.EndStation, newEndStation);
                        }
                        
                        ed.WriteMessage($"\n    Bước 1: Tạo region tạm thời {tempStart:F3} - {tempEnd:F3}");
                        newRegion = baseline.BaselineRegions.Add(newRegionName, assemblyId, tempStart, tempEnd);
                        
                        // Bước 2: Cập nhật về phạm vi cuối cùng
                        ed.WriteMessage($"\n    Bước 2: Cập nhật về phạm vi cuối {newStartStation:F3} - {newEndStation:F3}");
                        newRegion.StartStation = newStartStation;
                        newRegion.EndStation = newEndStation;
                    }
                    else
                    {
                        // Tạo trực tiếp
                        ed.WriteMessage("\n  🔄 Tạo region trực tiếp...");
                        newRegion = baseline.BaselineRegions.Add(newRegionName, assemblyId, newStartStation, newEndStation);
                    }
                    
                    tr.Commit();
                }
                
                // Copy các target nếu có (sử dụng transaction hiện tại)
                try
                {
                    var originalTargets = originalRegion.GetTargets();
                    if (originalTargets.Count > 0)
                    {
                        newRegion.SetTargets(originalTargets);
                        ed.WriteMessage("\n  ✓ Đã copy targets từ region cũ");
                    }
                }
                catch (System.Exception targetEx)
                {
                    ed.WriteMessage($"\n  ⚠ Không thể copy targets: {targetEx.Message}");
                }
                
                ed.WriteMessage($"\n✓ Đã tạo region mới thành công: {newRegionName}");
                return newRegion;
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n✗ Lỗi tạo region mới: {ex.Message}");
                throw;
            }
        }
    }
}

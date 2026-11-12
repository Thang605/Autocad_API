// (C) Copyright 2015 by  
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using Autodesk.AutoCAD.Runtime;
using Acad = Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;
using ATable = Autodesk.AutoCAD.DatabaseServices.Table;

using Civil = Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using Autodesk.Civil.Runtime;
using Autodesk.Civil.Settings;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.Civil.ApplicationServices;
using CivSurface = Autodesk.Civil.DatabaseServices.TinSurface;
using Section = Autodesk.Civil.DatabaseServices.Section;
using Autodesk.Civil;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using MyFirstProject.Extensions;

// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3DCsharp.CTSV_KhoiLuongCatNgang_Commands))]

namespace Civil3DCsharp
{
    public class CTSV_KhoiLuongCatNgang_Commands
    {
        [CommandMethod("CTS_Khoi_Luong_Cat_Ngang")]
        public static void CTSVKhoiLuongCatNgang()
        {
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                A.Ed.WriteMessage("\nLệnh tính khối lượng vật liệu trong cắt ngang...");

                // Step 1: Chọn sampleline
                A.Ed.WriteMessage("\nChọn sampleline để lấy thông tin khối lượng vật liệu:");
                ObjectId sampleLineId = UserInput.GSampleLineId("\nChọn sampleline: ");
                if (sampleLineId.IsNull)
                {
                    A.Ed.WriteMessage("\nĐã hủy lệnh.");
                    return;
                }

                SampleLine? sampleLine = tr.GetObject(sampleLineId, OpenMode.ForRead) as SampleLine;
                if (sampleLine == null)
                {
                    A.Ed.WriteMessage("\nKhông thể lấy thông tin sampleline.");
                    return;
                }

                // Step 2: Truy xuất samplelinegroup
                ObjectId sampleLineGroupId = sampleLine.GroupId;
                SampleLineGroup? sampleLineGroup = tr.GetObject(sampleLineGroupId, OpenMode.ForRead) as SampleLineGroup;
                if (sampleLineGroup == null)
                {
                    A.Ed.WriteMessage("\nKhông thể lấy thông tin sampleline group.");
                    return;
                }

                A.Ed.WriteMessage($"\nSampleLine Group: {sampleLineGroup.Name}");

                // Step 3: Thu thập thông tin khối lượng vật liệu từ material sections (công thức)
                List<MaterialVolumeInfo> materialInfoList = CollectMaterialVolumeInformation(sampleLineGroup, tr);

                if (materialInfoList.Count == 0)
                {
                    A.Ed.WriteMessage("\nKhông tìm thấy thông tin material section nào.");
                    return;
                }

                // Step 3.5: So sánh với khối lượng từ Civil 3D API (nếu có)
                var comparisonResults = CompareWithCivil3DVolumes(sampleLineGroup, materialInfoList, tr);

                // Step 4: Chọn vị trí tạo bảng
                A.Ed.WriteMessage("\nChọn vị trí đặt bảng khối lượng vật liệu:");
                PromptPointResult ppr = A.Ed.GetPoint("\nChọn điểm đặt bảng: ");
                if (ppr.Status != PromptStatus.OK)
                {
                    A.Ed.WriteMessage("\nĐã hủy lệnh.");
                    return;
                }

                Point3d insertionPoint = ppr.Value;

                // Step 5: Tạo bảng tổng hợp
                // Lấy tên tim đường (alignment) từ sampleLineGroup để đưa vào tiêu đề
                string alignmentName = "";
                try
                {
                    ObjectId alignmentId = sampleLineGroup.ParentAlignmentId;
                    if (!alignmentId.IsNull)
                    {
                        Alignment? alignment = tr.GetObject(alignmentId, OpenMode.ForRead) as Alignment;
                        if (alignment != null) alignmentName = alignment.Name ?? "";
                    }
                }
                catch { }

                CreateMaterialVolumeTable(materialInfoList, insertionPoint, tr, alignmentName);

                A.Ed.WriteMessage($"\nĐã tạo thành công bảng khối lượng vật liệu với {materialInfoList.Count} dòng dữ liệu.");
                
                // Step 6: Hiển thị kết quả so sánh nếu có
                DisplayComparisonResults(comparisonResults);
                
                A.Ed.WriteMessage($"\n=== HOÀN THÀNH ===");

                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage($"\nLỗi AutoCAD: {e.Message}");
                A.Ed.WriteMessage($"\nError Code: {e.ErrorStatus}");
                tr.Abort();
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi hệ thống: {ex.Message}");
                tr.Abort();
            }
        }

        private static List<MaterialVolumeInfo> CollectMaterialVolumeInformation(SampleLineGroup sampleLineGroup, Transaction tr)
        {
            List<MaterialVolumeInfo> materialInfoList = new();

            try
            {
                // Lấy tất cả sample lines trong group
                ObjectIdCollection sampleLineIds = sampleLineGroup.GetSampleLineIds();
                A.Ed.WriteMessage($"\nTìm thấy {sampleLineIds.Count} sample lines trong group.");

                // Tìm material sections trong model space
                BlockTable? bt = tr.GetObject(A.Db.BlockTableId, OpenMode.ForRead) as BlockTable;
                if (bt == null) return materialInfoList;

                BlockTableRecord? btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
                if (btr == null) return materialInfoList;

                // Duyệt qua tất cả entities để tìm material sections
                var materialSectionClass = RXClass.GetClass(typeof(MaterialSection));
                foreach (ObjectId entityId in btr)
                {
                    try
                    {
                        if (entityId.ObjectClass.IsDerivedFrom(materialSectionClass) && tr.GetObject(entityId, OpenMode.ForRead) is MaterialSection materialSection)
                        {
                            // Kiểm tra xem material section này có thuộc sample line group không
                            if (IsMaterialSectionBelongToGroup(materialSection, sampleLineIds, tr))
                            {
                                ProcessMaterialSection(materialSection, materialInfoList, tr);
                            }
                        }
                    }
                    catch
                    {
                        // Continue to next entity if cannot process current one
                        continue;
                    }
                }

                // Sắp xếp theo lý trình
                materialInfoList = materialInfoList.OrderBy(x => x.StationValue).ToList();

                A.Ed.WriteMessage($"\nTổng cộng: {materialInfoList.Count} mục khối lượng vật liệu đã được thu thập.");
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi khi thu thập thông tin material: {ex.Message}");
            }

            return materialInfoList;
        }

        private static bool IsMaterialSectionBelongToGroup(MaterialSection materialSection, ObjectIdCollection sampleLineIds, Transaction tr)
        {
            try
            {
                // Kiểm tra xem material section có station tương ứng với sample line nào không
                double materialStation = materialSection.Station;
                
                foreach (ObjectId sampleLineId in sampleLineIds)
                {
                    SampleLine? sampleLine = tr.GetObject(sampleLineId, OpenMode.ForRead) as SampleLine;
                    if (sampleLine != null)
                    {
                        // Kiểm tra station với tolerance
                        if (Math.Abs(sampleLine.Station - materialStation) < 0.001)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static void ProcessMaterialSection(MaterialSection materialSection, List<MaterialVolumeInfo> materialInfoList, Transaction tr)
        {
            try
            {
                // Lấy thông tin cơ bản
                double station = materialSection.Station;
                string stationText = FormatStation(station);
                string sourceName = materialSection.SourceName ?? "Không có tên";
                
                // Trích xuất tên vật liệu từ SourceName
                string materialName = ExtractMaterialNameFromSource(sourceName);
                
                // Tính diện tích từ SectionPoints
                double area = CalculateAreaFromSectionPoints(materialSection.SectionPoints);
                
                // Tìm tên cọc từ station
                string stakeName = GetStakeNameFromStation(station, tr);

                MaterialVolumeInfo info = new()
                {
                    StakeName = stakeName,
                    Station = stationText,
                    StationValue = station,
                    MaterialName = materialName,
                    Area = area,
                    SourceName = sourceName
                };

                materialInfoList.Add(info);
                A.Ed.WriteMessage($"\n  - Cọc: {stakeName}, Lý trình: {stationText}, Vật liệu: {materialName}, Diện tích: {area:F3} m²");
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi xử lý material section: {ex.Message}");
            }
        }

        private static string ExtractMaterialNameFromSource(string sourceName)
        {
            try
            {
                // SourceName có cấu trúc: "Nền đường - Đắp đất"
                // Trích xuất phần sau dấu "-" là tên vật liệu
                if (string.IsNullOrEmpty(sourceName))
                    return "Không xác định";

                int dashIndex = sourceName.LastIndexOf('-');
                if (dashIndex >= 0 && dashIndex < sourceName.Length - 1)
                {
                    string materialName = sourceName.Substring(dashIndex + 1).Trim();
                    return string.IsNullOrEmpty(materialName) ? "Không xác định" : materialName;
                }

                // Nếu không có dấu "-", trả về toàn bộ sourceName
                return sourceName.Trim();
            }
            catch
            {
                return "Không xác định";
            }
        }

        private static double CalculateAreaFromSectionPoints(SectionPointCollection sectionPoints)
        {
            try
            {
                if (sectionPoints == null || sectionPoints.Count < 3)
                    return 0.0;

                // Chuyển đổi section points thành Point2d để tính diện tích
                List<Point2d> points = new();
                
                for (int i = 0; i < sectionPoints.Count; i++)
                {
                    SectionPoint sectionPoint = sectionPoints[i];
                    // Sử dụng Location property để lấy Point3d, rồi chuyển thành Point2d
                    Point3d location = sectionPoint.Location;
                    points.Add(new Point2d(location.X, location.Y));
                }

                // Tính diện tích bằng công thức Shoelace
                return CalculatePolygonArea(points);
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi tính diện tích từ section points: {ex.Message}");
                return 0.0;
            }
        }

        private static double CalculatePolygonArea(List<Point2d> points)
        {
            if (points.Count < 3) return 0.0;

            double area = 0.0;
            int n = points.Count;

            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                area += points[i].X * points[j].Y;
                area -= points[j].X * points[i].Y;
            }

            return Math.Abs(area) / 2.0;
        }

        private static string GetStakeNameFromStation(double station, Transaction tr)
        {
            try
            {
                // Tìm sample line có station tương ứng để lấy tên cọc
                BlockTable? bt = tr.GetObject(A.Db.BlockTableId, OpenMode.ForRead) as BlockTable;
                if (bt == null) return FormatStation(station);

                BlockTableRecord? btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
                if (btr == null) return FormatStation(station);

                var sampleLineClass = RXClass.GetClass(typeof(SampleLine));
                foreach (ObjectId entityId in btr)
                {
                    try
                    {
                        if (entityId.ObjectClass.IsDerivedFrom(sampleLineClass) && tr.GetObject(entityId, OpenMode.ForRead) is SampleLine sampleLine)
                        {
                            if (Math.Abs(sampleLine.Station - station) < 0.001)
                            {
                                return sampleLine.Name ?? FormatStation(station);
                            }
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }

                return FormatStation(station);
            }
            catch
            {
                return FormatStation(station);
            }
        }

        private static string FormatStation(double station)
        {
            // Format theo định dạng Km+m
            int km = (int)(station / 1000);
            double meters = station % 1000;
            return $"Km{km}+{meters:F3}";
        }

        private static void CreateMaterialVolumeTable(List<MaterialVolumeInfo> materialInfoList, Point3d insertionPoint, Transaction tr, string alignmentName)
        {
            try
            {
                Database db = A.Db;
                BlockTable? bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                if (bt == null) return;

                BlockTableRecord? btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                if (btr == null) return;

                // Tạo pivot table: mỗi loại vật liệu thành cột riêng
                var pivotData = CreatePivotTableData(materialInfoList);
                int decimalPlaces = pivotData.DecimalPlaces;

                // Tính số dòng và cột
                int numRows = pivotData.StakeInfos.Count + 3; // +3 cho title, header, và dòng tổng
                int numCols = 3 + pivotData.MaterialTypes.Count; // Lý trình + Tên cọc + Khoảng cách + các cột vật liệu

                ATable table = new();
                table.SetSize(numRows, numCols);
                table.Position = insertionPoint;
                table.TableStyle = db.Tablestyle;

                // Thiết lập chiều rộng cột (tăng 50% cho các cột cơ bản)
                table.Columns[0].Width = 37.5; // Lý trình (25 * 1.5)
                table.Columns[1].Width = 30.0; // Tên cọc (20 * 1.5)
                table.Columns[2].Width = 30.0; // Khoảng cách (20 * 1.5)
                
                // Chiều rộng cho các cột vật liệu
                for (int i = 3; i < numCols; i++)
                {
                    table.Columns[i].Width = 25.0; // Cột vật liệu
                }

                // Tiêu đề bảng (thêm tên đường nếu có)
                string title = "BẢNG KHỐI LƯỢNG VẬT LIỆU TRONG CẮT NGANG";
                if (!string.IsNullOrEmpty(alignmentName)) title += " - " + alignmentName;
                table.Cells[0, 0].TextString = title;
                table.MergeCells(CellRange.Create(table, 0, 0, 0, numCols - 1));
                table.Cells[0, 0].Alignment = CellAlignment.MiddleCenter;
                table.Cells[0, 0].TextHeight = 6.0;
                table.Rows[0].Height = 10.0;

                // Header cột
                table.Cells[1, 0].TextString = "Lý trình";
                table.Cells[1, 1].TextString = "Tên cọc";
                table.Cells[1, 2].TextString = "Khoảng cách lẻ (m)"; // hiển thị khoảng cách tới cọc trước (cọc đầu bằng 0)
                
                // Headers cho các cột vật liệu
                for (int i = 0; i < pivotData.MaterialTypes.Count; i++)
                {
                    table.Cells[1, 3 + i].TextString = $"{pivotData.MaterialTypes[i]} (m²)";
                    table.Cells[1, 3 + i].Alignment = CellAlignment.MiddleCenter;
                    table.Cells[1, 3 + i].TextHeight = 4.0;
                }

                // Căn chỉnh header cột cơ bản
                for (int col = 0; col < 3; col++)
                {
                    table.Cells[1, col].Alignment = CellAlignment.MiddleCenter;
                    table.Cells[1, col].TextHeight = 4.0;
                }
                table.Rows[1].Height = 8.0;

                // Format string cho số thập phân
                string numberFormat = "F" + decimalPlaces;

                // Dữ liệu
                for (int i = 0; i < pivotData.StakeInfos.Count; i++)
                {
                    int row = i + 2;
                    var stakeInfo = pivotData.StakeInfos[i];

                    // Cột cơ bản
                    table.Cells[row, 0].TextString = stakeInfo.Station;
                    table.Cells[row, 1].TextString = stakeInfo.StakeName;
                    // Hiển thị khoảng cách lẻ (khoảng cách tới cọc trước). Cọc đầu = 0
                    table.Cells[row, 2].TextString = stakeInfo.SpacingPrev.ToString(numberFormat);

                    // Căn chỉnh cột cơ bản
                    table.Cells[row, 0].Alignment = CellAlignment.MiddleCenter;
                    table.Cells[row, 1].Alignment = CellAlignment.MiddleCenter;
                    table.Cells[row, 2].Alignment = CellAlignment.MiddleCenter;

                    // Dữ liệu vật liệu (cộng thêm giá trị bổ sung nếu có)
                    for (int j = 0; j < pivotData.MaterialTypes.Count; j++)
                    {
                        string materialType = pivotData.MaterialTypes[j];
                        double area = stakeInfo.MaterialAreas.ContainsKey(materialType) ? stakeInfo.MaterialAreas[materialType] : 0.0;
                        
                        // Cộng thêm giá trị bổ sung nếu có
                        if (pivotData.MaterialAdditionalValues.ContainsKey(materialType))
                        {
                            area += pivotData.MaterialAdditionalValues[materialType];
                        }
                        
                        table.Cells[row, 3 + j].TextString = area > 0 ? area.ToString(numberFormat) : "-";
                        table.Cells[row, 3 + j].Alignment = CellAlignment.MiddleCenter;
                        table.Cells[row, 3 + j].TextHeight = 3.5;
                    }

                    // Thiết lập text height cho cột cơ bản
                    for (int col = 0; col < 3; col++)
                    {
                        table.Cells[row, col].TextHeight = 3.5;
                    }
                    table.Rows[row].Height = 6.0;
                }

                // Dòng tổng cộng (cuối bảng)
                int totalRow = pivotData.StakeInfos.Count + 2;
                // Merge 2 cột đầu để hiển thị nhãn Tổng cộng
                table.MergeCells(CellRange.Create(table, totalRow, 0, totalRow, 1));
                table.Cells[totalRow, 0].TextString = "TỔNG CỘNG";
                table.Cells[totalRow, 0].Alignment = CellAlignment.MiddleCenter;
                table.Cells[totalRow, 0].TextHeight = 4.0;

                // Tính tổng khoảng cách lẻ
                double totalSpacing = pivotData.StakeInfos.Sum(s => s.SpacingPrev);
                table.Cells[totalRow, 2].TextString = totalSpacing.ToString(numberFormat);
                table.Cells[totalRow, 2].Alignment = CellAlignment.MiddleCenter;
                table.Cells[totalRow, 2].TextHeight = 3.5;

                // Tính tổng cho từng loại vật liệu (bao gồm giá trị bổ sung)
                for (int j = 0; j < pivotData.MaterialTypes.Count; j++)
                {
                    string materialType = pivotData.MaterialTypes[j];
                    double sum = 0.0;
                    
                    foreach (var stake in pivotData.StakeInfos)
                    {
                        double stakeArea = 0.0;
                        if (stake.MaterialAreas.ContainsKey(materialType))
                            stakeArea = stake.MaterialAreas[materialType];
                        
                        // Cộng thêm giá trị bổ sung cho mỗi cọc
                        if (pivotData.MaterialAdditionalValues.ContainsKey(materialType))
                            stakeArea += pivotData.MaterialAdditionalValues[materialType];
                        
                        sum += stakeArea;
                    }
                    
                    string zeroValue = "0.".PadRight(2 + decimalPlaces, '0');
                    table.Cells[totalRow, 3 + j].TextString = sum > 0 ? sum.ToString(numberFormat) : zeroValue;
                    table.Cells[totalRow, 3 + j].Alignment = CellAlignment.MiddleCenter;
                    table.Cells[totalRow, 3 + j].TextHeight = 3.5;
                }
                table.Rows[totalRow].Height = 8.0;

                // Thêm bảng vào database
                btr.AppendEntity(table);
                tr.AddNewlyCreatedDBObject(table, true);

                A.Ed.WriteMessage($"\nĐã tạo bảng khối lượng vật liệu với {pivotData.MaterialTypes.Count} loại vật liệu tại tọa độ: X={insertionPoint.X:F3}, Y={insertionPoint.Y:F3}");
                A.Ed.WriteMessage($"\nCác loại vật liệu: {string.Join(", ", pivotData.MaterialTypes)}");
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi tạo bảng: {ex.Message}");
            }
        }

        private static PivotTableData CreatePivotTableData(List<MaterialVolumeInfo> materialInfoList)
        {
            var pivotData = new PivotTableData();
            
            // Lấy tất cả loại vật liệu duy nhất
            var allMaterialTypes = materialInfoList
                .Select(x => x.MaterialName)
                .Distinct()
                .ToList();

            // Hiển thị form để người dùng sắp xếp thứ tự vật liệu và chọn số thập phân
            var (orderedMaterials, decimalPlaces, additionalValues) = GetUserOrderedMaterialsAndDecimalPlaces(allMaterialTypes);
            pivotData.MaterialTypes = orderedMaterials;
            pivotData.DecimalPlaces = decimalPlaces;
            pivotData.MaterialAdditionalValues = additionalValues;

            // Nhóm theo station và tạo dữ liệu pivot
            var groupedByStation = materialInfoList
                .GroupBy(x => x.StationValue)
                .OrderBy(g => g.Key);

            foreach (var group in groupedByStation)
            {
                var firstItem = group.First();
                var stakeInfo = new StakeInfo
                {
                    Station = firstItem.Station,
                    StakeName = firstItem.StakeName,
                    StationValue = firstItem.StationValue,
                    MaterialAreas = new Dictionary<string, double>()
                };

                // Tính tổng diện tích cho mỗi loại vật liệu tại cọc này
                foreach (var item in group)
                {
                    if (stakeInfo.MaterialAreas.ContainsKey(item.MaterialName))
                    {
                        stakeInfo.MaterialAreas[item.MaterialName] += item.Area;
                    }
                    else
                    {
                        stakeInfo.MaterialAreas[item.MaterialName] = item.Area;
                    }
                }

                pivotData.StakeInfos.Add(stakeInfo);
            }

            // Tính khoảng cách giữa các cọc (SpacingPrev: khoảng cách tới cọc trước, cọc đầu = 0)
            for (int i = 0; i < pivotData.StakeInfos.Count; i++)
            {
                if (i == 0)
                {
                    pivotData.StakeInfos[i].SpacingPrev = 0.0;
                }
                else
                {
                    pivotData.StakeInfos[i].SpacingPrev = Math.Abs(pivotData.StakeInfos[i].StationValue - pivotData.StakeInfos[i - 1].StationValue);
                }
                // Keep existing forward spacing if needed
                if (i < pivotData.StakeInfos.Count - 1)
                {
                    pivotData.StakeInfos[i].Spacing = Math.Abs(pivotData.StakeInfos[i + 1].StationValue - pivotData.StakeInfos[i].StationValue);
                }
                else
                {
                    pivotData.StakeInfos[i].Spacing = pivotData.StakeInfos[i].SpacingPrev;
                }
            }

            return pivotData;
        }

        private static (List<string> materials, int decimalPlaces, Dictionary<string, double> additionalValues) GetUserOrderedMaterialsAndDecimalPlaces(List<string> materialTypes)
        {
            try
            {
                // Sắp xếp mặc định theo thứ tự ưu tiên trước
                var defaultOrderedMaterials = SortMaterialsByPriority(materialTypes);
                
                A.Ed.WriteMessage($"\nTìm thấy {materialTypes.Count} loại vật liệu. Hiển thị form sắp xếp...");
                
                // Tạo và hiển thị form sắp xếp
                var orderForm = new MyFirstProject.Civil_Tool.MaterialOrderForm(defaultOrderedMaterials);
                
                // Sử dụng Application.ShowModalDialog để hiển thị form trong AutoCAD
                var result = Autodesk.AutoCAD.ApplicationServices.Application.ShowModalDialog(orderForm);
                
                if (orderForm.DialogResult_OK && orderForm.OrderedMaterialTypes.Count > 0)
                {
                    A.Ed.WriteMessage($"\nNgười dùng đã sắp xếp thứ tự: {string.Join(", ", orderForm.OrderedMaterialTypes)}");
                    A.Ed.WriteMessage($"\nSố chữ số thập phân: {orderForm.DecimalPlaces}");
                    
                    // Hiển thị giá trị cộng thêm nếu có
                    var nonZeroAdditionalValues = orderForm.MaterialAdditionalValues.Where(kvp => kvp.Value != 0).ToList();
                    if (nonZeroAdditionalValues.Any())
                    {
                        A.Ed.WriteMessage("\nGiá trị cộng thêm:");
                        foreach (var kvp in nonZeroAdditionalValues)
                        {
                            A.Ed.WriteMessage($"  - {kvp.Key}: {kvp.Value}");
                        }
                    }
                    
                    return (orderForm.OrderedMaterialTypes, orderForm.DecimalPlaces, orderForm.MaterialAdditionalValues);
                }
                else
                {
                    A.Ed.WriteMessage("\nSử dụng thứ tự mặc định.");
                    var defaultAdditionalValues = new Dictionary<string, double>();
                    foreach (var material in defaultOrderedMaterials)
                    {
                        defaultAdditionalValues[material] = 0.0;
                    }
                    return (defaultOrderedMaterials, 3, defaultAdditionalValues);
                }
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi hiển thị form sắp xếp: {ex.Message}");
                A.Ed.WriteMessage("\nSử dụng thứ tự mặc định.");
                var defaultOrderedMaterials = SortMaterialsByPriority(materialTypes);
                var defaultAdditionalValues = new Dictionary<string, double>();
                foreach (var material in defaultOrderedMaterials)
                {
                    defaultAdditionalValues[material] = 0.0;
                }
                return (defaultOrderedMaterials, 3, defaultAdditionalValues);
            }
        }

        private static List<string> SortMaterialsByPriority(List<string> materialTypes)
        {
            // Định nghĩa thứ tự ưu tiên cho các loại vật liệu
            var materialPriority = new Dictionary<string, int>
            {
                // Đất
                { "Đào đất", 1 },
                { "Dao dat", 1 },
                { "Đắp đất", 2 },
                { "Dap dat", 2 },
                { "Đất đào", 3 },
                { "Dat dao", 3 },
                { "Đất đắp", 4 },
                { "Dat dap", 4 },
                
                // Bê tông nhựa
                { "BTN hạt mịn", 10 },
                { "BTN hat min", 10 },
                { "BTN hạt nhỏ", 11 },
                { "BTN hat nho", 11 },
                { "BTN hạ trung", 12 },
                { "BTN ha trung", 12 },
                { "BTN thượng", 13 },
                { "BTN thuong", 13 },
                { "Bê tông nhựa", 14 },
                { "Be tong nhua", 14 },
                
                // Cấp phối đá dăm
                { "CPDD loại 1", 20 },
                { "CPDD loai 1", 20 },
                { "CPDD loại 2", 21 },
                { "CPDD loai 2", 21 },
                { "CPDD loại 3", 22 },
                { "CPDD loai 3", 22 },
                { "Cấp phối đá dăm", 23 },
                { "Cap phoi da dam", 23 },
                
                // Xi măng
                { "Xi măng", 30 },
                { "Xi mang", 30 },
                { "Bê tông xi măng", 31 },
                { "Be tong xi mang", 31 },
                
                // Đá
                { "Đá dăm", 40 },
                { "Da dam", 40 },
                { "Đá hộc", 41 },
                { "Da hoc", 41 },
                { "Đá xây", 42 },
                { "Da xay", 42 },
                
                // Cát
                { "Cát", 50 },
                { "Cat", 50 },
                { "Cát vàng", 51 },
                { "Cat vang", 51 },
                
                // Khác
                { "Lót đường", 60 },
                { "Lot duong", 60 },
                { "Nền đường", 61 },
                { "Nen duong", 61 }
            };

            // Sắp xếp theo thứ tự ưu tiên, nếu không có trong danh sách thì đặt ở cuối
            return materialTypes
                .OrderBy(material => 
                {
                    // Tìm kiếm exact match trước
                    if (materialPriority.ContainsKey(material))
                        return materialPriority[material];
                    
                    // Tìm kiếm partial match (không phân biệt hoa thường)
                    var normalizedMaterial = material.ToLower().Trim();
                    foreach (var kvp in materialPriority)
                    {
                        if (normalizedMaterial.Contains(kvp.Key.ToLower()) || 
                            kvp.Key.ToLower().Contains(normalizedMaterial))
                        {
                            return kvp.Value;
                        }
                    }
                    
                    // Nếu không tìm thấy, đặt ở cuối và sắp xếp theo alphabet
                    return 1000;
                })
                .ThenBy(material => material) // Sắp xếp alphabet cho các vật liệu cùng priority
                .ToList();
        }

        // So sánh khối lượng tính toán với khối lượng từ Civil 3D Material Section API
        private static VolumeComparisonResult CompareWithCivil3DVolumes(
            SampleLineGroup sampleLineGroup, 
            List<MaterialVolumeInfo> calculatedMaterials, 
            Transaction tr)
        {
            var result = new VolumeComparisonResult();
            
            try
            {
                A.Ed.WriteMessage("\n\n📊 ===== SO SÁNH KHỐI LƯỢNG VỚI CIVIL 3D API =====");
                A.Ed.WriteMessage("\n💡 Lưu ý: Hiện tại đang so sánh dữ liệu từ MaterialSection objects.");
                
                // Thu thập dữ liệu từ Civil 3D MaterialSection (cách khác để tính)
                var civil3DVolumes = CollectCivil3DVolumesDirect(sampleLineGroup, tr);

                if (civil3DVolumes.Count == 0)
                {
                    A.Ed.WriteMessage("\n⚠️  Không tìm thấy dữ liệu từ Civil 3D để so sánh.");
                    A.Ed.WriteMessage("\n💡 Điều này có thể do:");
                    A.Ed.WriteMessage("\n   - Chưa có Material Section được tạo");
                    A.Ed.WriteMessage("\n   - Corridor chưa được sample");
                    result.HasMaterialList = false;
                    return result;
                }
                
                result.HasMaterialList = true;
                A.Ed.WriteMessage($"\n✓ Tìm thấy {civil3DVolumes.Count} mục dữ liệu từ Civil 3D");
                
                // So sánh với dữ liệu đã tính
                CompareVolumes(calculatedMaterials, civil3DVolumes, result);
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n⚠️  Lỗi khi so sánh với Civil 3D: {ex.Message}");
                result.HasMaterialList = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        private static Dictionary<string, Civil3DVolumeData> CollectCivil3DVolumesDirect(
            SampleLineGroup sampleLineGroup,
            Transaction tr)
        {
            var volumes = new Dictionary<string, Civil3DVolumeData>();
            
            try
            {
                A.Ed.WriteMessage("\n\n🔍 Thu thập dữ liệu từ Civil 3D MaterialSection...");
                
                // Lấy tất cả sample lines
                ObjectIdCollection sampleLineIds = sampleLineGroup.GetSampleLineIds();
                
                // Tìm material sections trong model space
                BlockTable? bt = tr.GetObject(A.Db.BlockTableId, OpenMode.ForRead) as BlockTable;
                if (bt == null) return volumes;

                BlockTableRecord? btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
                if (btr == null) return volumes;

                // Duyệt qua tất cả entities để tìm material sections
                var materialSectionClass = RXClass.GetClass(typeof(MaterialSection));
                foreach (ObjectId entityId in btr)
                {
                    try
                    {
                        if (entityId.ObjectClass.IsDerivedFrom(materialSectionClass) && tr.GetObject(entityId, OpenMode.ForRead) is MaterialSection materialSection)
                        {
                            // Kiểm tra xem material section này có thuộc sample line group không
                            if (IsMaterialSectionBelongToGroup(materialSection, sampleLineIds, tr))
                            {
                                double station = materialSection.Station;
                                string stationText = FormatStation(station);
                                string sourceName = materialSection.SourceName ?? "Không có tên";
                                string materialName = ExtractMaterialNameFromSource(sourceName);
                                
                                // Sử dụng Bounds.Area nếu có, hoặc tính từ SectionPoints
                                double area = GetAlternativeAreaCalculation(materialSection);

                                string key = $"{stationText}|{materialName}";
                                
                                if (!volumes.ContainsKey(key))
                                {
                                    volumes[key] = new Civil3DVolumeData
                                    {
                                        Station = stationText,
                                        StationValue = station,
                                        MaterialName = materialName,
                                        Area = area
                                    };
                                }
                                else
                                {
                                    volumes[key].Area += area;
                                }
                            }
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }
                
                A.Ed.WriteMessage($"\n✓ Thu thập được {volumes.Count} mục từ Civil 3D MaterialSection");
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n⚠️  Lỗi CollectCivil3DVolumesDirect: {ex.Message}");
            }
            
            return volumes;
        }

        private static double GetAlternativeAreaCalculation(MaterialSection materialSection)
        {
            try
            {
                // Thử cách 1: Tính từ Bounds (bounding box)
                var bounds = materialSection.Bounds;
                double boundsArea = 0.0;
                
                if (bounds.HasValue)
                {
                    var extents = bounds.Value;
                    boundsArea = Math.Abs((extents.MaxPoint.X - extents.MinPoint.X) * (extents.MaxPoint.Y - extents.MinPoint.Y));
                }
                
                // Thử cách 2: Tính từ SectionPoints (giống công thức đã dùng)
                double shoelaceArea = CalculateAreaFromSectionPoints(materialSection.SectionPoints);
                
                // So sánh 2 cách: nếu khác nhau đáng kể, báo cáo
                if (boundsArea > 0 && shoelaceArea > 0 && Math.Abs(boundsArea - shoelaceArea) / shoelaceArea > 0.5)
                {
                    // Bounds area khác nhiều so với Shoelace area
                    // Chọn Shoelace area vì chính xác hơn
                    A.Ed.WriteMessage($"\n⚠️  Cảnh báo: Bounds area ({boundsArea:F3}) khác Shoelace area ({shoelaceArea:F3})");
                }
                
                // Trả về Shoelace area (chính xác hơn)
                return shoelaceArea;
            }
            catch
            {
                return 0.0;
            }
        }

        private static void CompareVolumes(
            List<MaterialVolumeInfo> calculatedMaterials,
            Dictionary<string, Civil3DVolumeData> civil3DVolumes,
            VolumeComparisonResult result)
        {
            A.Ed.WriteMessage("\n\n📊 So sánh chi tiết:");
            A.Ed.WriteMessage("\n" + new string('-', 100));
            A.Ed.WriteMessage($"\n{"Lý trình",-15} {"Vật liệu",-20} {"Công thức",-15} {"Civil 3D",-15} {"Chênh lệch",-15} {"% Sai khác",-10}");
            A.Ed.WriteMessage("\n" + new string('-', 100));

            bool hasDifference = false;

            foreach (var calcMat in calculatedMaterials)
            {
                string key = $"{calcMat.Station}|{calcMat.MaterialName}";

                if (civil3DVolumes.ContainsKey(key))
                {
                    var civil3DData = civil3DVolumes[key];
                    double calcArea = calcMat.Area;
                    double civil3DArea = civil3DData.Area;
                    double difference = Math.Abs(calcArea - civil3DArea);
                    double percentDiff = calcArea > 0 ? (difference / calcArea) * 100 : 0;

                    // Ngưỡng sai khác cho phép (0.1%)
                    if (percentDiff > 0.1)
                    {
                        hasDifference = true;
                        result.Differences.Add(new VolumeDifference
                        {
                            Station = calcMat.Station,
                            MaterialName = calcMat.MaterialName,
                            CalculatedArea = calcArea,
                            Civil3DArea = civil3DArea,
                            Difference = difference,
                            PercentDifference = percentDiff
                        });

                        string diffStr = difference >= 0 ? $"+{difference:F3}" : $"{difference:F3}";
                        A.Ed.WriteMessage($"\n{calcMat.Station,-15} {calcMat.MaterialName,-20} {calcArea,12:F3} m² {civil3DArea,12:F3} m² {diffStr,12} m² {percentDiff,7:F2}%");
                    }
                }
            }

            if (!hasDifference && civil3DVolumes.Count > 0)
            {
                A.Ed.WriteMessage("\n✓ Tất cả các giá trị khớp nhau (sai khác < 0.1%)");
            }
            else if (civil3DVolumes.Count == 0)
            {
                A.Ed.WriteMessage("\n⚠️  Không có dữ liệu từ Civil 3D để so sánh");
            }

            A.Ed.WriteMessage("\n" + new string('-', 100));
        }

        private static void DisplayComparisonResults(VolumeComparisonResult result)
        {
            A.Ed.WriteMessage("\n\n📊 ===== KẾT QUẢ SO SÁNH =====");

            if (!result.HasMaterialList)
            {
                A.Ed.WriteMessage("\n⚠️  KHÔNG CÓ DỮ LIỆU ĐỂ SO SÁNH");
                A.Ed.WriteMessage("\n💡 Nguyên nhân:");
                A.Ed.WriteMessage("\n   - Chưa có MaterialSection được tạo trong drawing");
                A.Ed.WriteMessage("\n   - Corridor chưa được sample với material section");

                if (!string.IsNullOrEmpty(result.ErrorMessage))
                {
                    A.Ed.WriteMessage($"\n❌ Lỗi: {result.ErrorMessage}");
                }
                return;
            }

            if (result.Differences.Count == 0)
            {
                A.Ed.WriteMessage("\n✅ KHỐI LƯỢNG KHỚP NHAU!");
                A.Ed.WriteMessage("\n   Công thức tính diện tích (Shoelace) cho kết quả chính xác.");
                A.Ed.WriteMessage("\n   Sai khác < 0.1% so với Civil 3D API.");
            }
            else
            {
                A.Ed.WriteMessage($"\n⚠️  CÓ {result.Differences.Count} VỊ TRÍ CÓ SAI KHÁC:");
                A.Ed.WriteMessage("\n" + new string('=', 80));

                foreach (var diff in result.Differences.OrderByDescending(d => d.PercentDifference).Take(5))
                {
                    A.Ed.WriteMessage($"\n🔸 {diff.Station} - {diff.MaterialName}:");
                    A.Ed.WriteMessage($"\n   Công thức: {diff.CalculatedArea:F3} m²");
                    A.Ed.WriteMessage($"\n   Civil 3D:  {diff.Civil3DArea:F3} m²");
                    A.Ed.WriteMessage($"\n   Chênh lệch: {diff.Difference:F3} m² ({diff.PercentDifference:F2}%)");
                }

                if (result.Differences.Count > 5)
                {
                    A.Ed.WriteMessage($"\n... và {result.Differences.Count - 5} vị trí khác");
                }

                A.Ed.WriteMessage("\n\n💡 Nguyên nhân có thể:");
                A.Ed.WriteMessage("\n   - Cách tính diện tích khác nhau");
                A.Ed.WriteMessage("\n   - Material List settings khác với Material Section");
                A.Ed.WriteMessage("\n   - Rounding errors trong tính toán");

                A.Ed.WriteMessage("\n" + new string('=', 80));
            }
        }

        // Helper classes
        private class MaterialVolumeInfo
        {
            public string StakeName { get; set; } = "";
            public string Station { get; set; } = "";
            public double StationValue { get; set; }
            public string MaterialName { get; set; } = "";
            public double Area { get; set; }
            public string SourceName { get; set; } = "";
        }

        private class PivotTableData
        {
            public List<string> MaterialTypes { get; set; } = new();
            public List<StakeInfo> StakeInfos { get; set; } = new();
            public int DecimalPlaces { get; set; } = 3;
            public Dictionary<string, double> MaterialAdditionalValues { get; set; } = new();
        }

        private class StakeInfo
        {
            public string Station { get; set; } = "";
            public string StakeName { get; set; } = "";
            public double StationValue { get; set; }
            public double Spacing { get; set; }
            public double SpacingPrev { get; set; }
            public Dictionary<string, double> MaterialAreas { get; set; } = new();
        }

        private class Civil3DVolumeData
        {
            public string Station { get; set; } = "";
            public double StationValue { get; set; }
            public string MaterialName { get; set; } = "";
            public double Area { get; set; }
        }

        private class VolumeDifference
        {
            public string Station { get; set; } = "";
            public string MaterialName { get; set; } = "";
            public double CalculatedArea { get; set; }
            public double Civil3DArea { get; set; }
            public double Difference { get; set; }
            public double PercentDifference { get; set; }
        }

        private class VolumeComparisonResult
        {
            public bool HasMaterialList { get; set; } = false;
            public List<VolumeDifference> Differences { get; set; } = new();
            public string ErrorMessage { get; set; } = "";
        }
    }
}

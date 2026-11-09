// (C) Copyright 2024 by  
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;
using MyFirstProject.Extensions;
using ClosedXML.Excel;
using MyFirstProject.Civil_Tool;

// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3DCsharp.CTSV_XuatKhoiLuongRaExcel_Commands))]

namespace Civil3DCsharp
{
    public class CTSV_XuatKhoiLuongRaExcel_Commands
    {
        private static string? _lastExportDirectory;
        private static List<string> _currentQTOMaterialOrder = new();

        [CommandMethod("CTSV_XuatKhoiLuongRaExcel")]
        public static void CTSVXuatKhoiLuongRaExcel()
        {
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                A.Ed.WriteMessage("\n📊 Lệnh xuất bảng khối lượng vật liệu ra Excel...");

                // Step 1: Lấy tất cả Alignments có SampleLineGroup
                A.Ed.WriteMessage("\n\n🎯 BƯỚC 1: Tìm tất cả Alignment có SampleLineGroup");
                List<AlignmentInfo> alignmentsWithSLG = GetAllAlignmentsWithSampleLineGroups(tr);

                if (alignmentsWithSLG.Count == 0)
                {
                    A.Ed.WriteMessage("\n❌ Không tìm thấy Alignment nào có SampleLineGroup.");
                    tr.Abort();
                    return;
                }

                A.Ed.WriteMessage($"\n✓ Tìm thấy {alignmentsWithSLG.Count} alignment(s) có SampleLineGroup");

                // Step 2: Hiển thị form để chọn alignments
                A.Ed.WriteMessage("\n\n🎯 BƯỚC 2: Chọn Alignment để xuất khối lượng");
                var selectionForm = new AlignmentSelectionForm(alignmentsWithSLG);
                Autodesk.AutoCAD.ApplicationServices.Application.ShowModalDialog(selectionForm);

                if (!selectionForm.DialogResult_OK || selectionForm.SelectedAlignments.Count == 0)
                {
                    A.Ed.WriteMessage("\n❌ Đã hủy lệnh.");
                    tr.Abort();
                    return;
                }

                A.Ed.WriteMessage($"\n✓ Đã chọn {selectionForm.SelectedAlignments.Count} alignment(s)");
                bool useTextMethod = selectionForm.UseTextMethod;

                // Step 3: Thu thập thông tin từ tất cả các SampleLineGroups
                A.Ed.WriteMessage("\n\n🎯 BƯỚC 3: Thu thập thông tin khối lượng vật liệu");
                A.Ed.WriteMessage($"\n  📐 Phương pháp: {(useTextMethod ? "Text (QTO Table + Text vàng)" : "MaterialSection (Explode → Hatch.Area)")}");
                List<SheetData> allSheetData = new();

                foreach (var alignmentInfo in selectionForm.SelectedAlignments)
                {
                    A.Ed.WriteMessage($"\n\n📍 Xử lý Alignment: {alignmentInfo.AlignmentName}");

                    foreach (var slgInfo in alignmentInfo.SampleLineGroups)
                    {
                        A.Ed.WriteMessage($"\n  - SampleLineGroup: {slgInfo.SampleLineGroupName}");

                        SampleLineGroup? sampleLineGroup = tr.GetObject(slgInfo.SampleLineGroupId, OpenMode.ForRead) as SampleLineGroup;
                        if (sampleLineGroup == null) continue;

                        List<MaterialVolumeInfo> materialInfoList;
                        if (useTextMethod)
                        {
                            materialInfoList = CollectMaterialVolumeFromText(sampleLineGroup, tr, selectionForm.OffsetX, selectionForm.OffsetY, selectionForm.DrawRangePolyline);
                        }
                        else
                        {
                            materialInfoList = CollectMaterialVolumeInformation(sampleLineGroup, tr);
                        }

                        if (materialInfoList.Count > 0)
                        {
                            var sheetData = new SheetData
                            {
                                SheetName = GenerateSheetName(alignmentInfo.AlignmentName, slgInfo.SampleLineGroupName),
                                AlignmentName = alignmentInfo.AlignmentName,
                                SampleLineGroupName = slgInfo.SampleLineGroupName,
                                SampleLineGroupCount = alignmentInfo.SampleLineGroupCount,
                                MaterialInfoList = materialInfoList,
                                QTOMaterialOrder = new List<string>(_currentQTOMaterialOrder) // Lưu copy của order hiện tại
                            };
                            allSheetData.Add(sheetData);
                            A.Ed.WriteMessage($"\n  ✓ Thu thập được {materialInfoList.Count} mục khối lượng");
                        }
                    }
                }

                if (allSheetData.Count == 0)
                {
                    A.Ed.WriteMessage("\n❌ Không tìm thấy thông tin khối lượng vật liệu nào.");
                    tr.Abort();
                    return;
                }

                A.Ed.WriteMessage($"\n\n✓ Tổng cộng: {allSheetData.Count} sheet sẽ được tạo");

                // Step 4: Chọn vị trí lưu file Excel
                A.Ed.WriteMessage("\n\n🎯 BƯỚC 4: Chọn vị trí lưu file Excel");
                string suggestedName = $"KhoiLuong_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                string initialDir = _lastExportDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                using SaveFileDialog sfd = new()
                {
                    Title = "Chọn nơi lưu file Excel",
                    Filter = "Excel Files (*.xlsx)|*.xlsx",
                    FileName = suggestedName,
                    InitialDirectory = initialDir,
                    AddExtension = true,
                    DefaultExt = "xlsx",
                    OverwritePrompt = true
                };

                if (sfd.ShowDialog() != DialogResult.OK)
                {
                    A.Ed.WriteMessage("\n❌ Đã hủy lưu file.");
                    tr.Abort();
                    return;
                }

                string exportPath = sfd.FileName;
                _lastExportDirectory = Path.GetDirectoryName(exportPath);

                // Step 5: Xuất ra Excel với nhiều sheet
                A.Ed.WriteMessage("\n\n🎯 BƯỚC 5: Xuất dữ liệu ra Excel");
                ExportMultipleSheetsToExcel(allSheetData, exportPath, selectionForm.UseDefaultSorting, selectionForm.DecimalPlaces);

                A.Ed.WriteMessage($"\n\n✅ ===== HOÀN THÀNH =====");
                A.Ed.WriteMessage($"\n📁 File đã được lưu tại: {exportPath}");
                A.Ed.WriteMessage($"\n📊 Đã tạo {allSheetData.Count} sheet(s) trong file Excel");

                // Tự động mở file Excel
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exportPath,
                    UseShellExecute = true
                });

                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage($"\n❌ Lỗi AutoCAD: {e.Message}");
                A.Ed.WriteMessage($"\nError Code: {e.ErrorStatus}");
                tr.Abort();
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n❌ Lỗi hệ thống: {ex.Message}");
                A.Ed.WriteMessage($"\nStack Trace: {ex.StackTrace}");
                tr.Abort();
            }
        }

        private static List<AlignmentInfo> GetAllAlignmentsWithSampleLineGroups(Transaction tr)
        {
            List<AlignmentInfo> result = new();

            try
            {
                BlockTable? bt = tr.GetObject(A.Db.BlockTableId, OpenMode.ForRead) as BlockTable;
                if (bt == null) return result;

                BlockTableRecord? btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
                if (btr == null) return result;

                RXClass rxAlignment = RXClass.GetClass(typeof(Alignment));
                foreach (ObjectId entityId in btr)
                {
                    try
                    {
                        if (!entityId.ObjectClass.IsDerivedFrom(rxAlignment)) continue;
                        if (tr.GetObject(entityId, OpenMode.ForRead) is Alignment alignment)
                        {
                            ObjectIdCollection sampleLineGroupIds = alignment.GetSampleLineGroupIds();

                            if (sampleLineGroupIds.Count > 0)
                            {
                                var alignmentInfo = new AlignmentInfo
                                {
                                    AlignmentId = entityId,
                                    AlignmentName = alignment.Name ?? "Unknown",
                                    SampleLineGroupCount = sampleLineGroupIds.Count
                                };

                                // Lấy thông tin từng SampleLineGroup
                                foreach (ObjectId slgId in sampleLineGroupIds)
                                {
                                    try
                                    {
                                        SampleLineGroup? slg = tr.GetObject(slgId, OpenMode.ForRead) as SampleLineGroup;
                                        if (slg != null)
                                        {
                                            alignmentInfo.SampleLineGroups.Add(new SampleLineGroupInfo
                                            {
                                                SampleLineGroupId = slgId,
                                                SampleLineGroupName = slg.Name ?? "Unknown",
                                                AlignmentName = alignmentInfo.AlignmentName
                                            });
                                        }
                                    }
                                    catch
                                    {
                                        continue;
                                    }
                                }

                                result.Add(alignmentInfo);
                                A.Ed.WriteMessage($"\n  - {alignmentInfo.AlignmentName}: {sampleLineGroupIds.Count} SampleLineGroup(s)");
                            }
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n⚠️  Lỗi khi tìm alignment: {ex.Message}");
            }

            return result;
        }

        private static string GenerateSheetName(string alignmentName, string sampleLineGroupName)
        {
            // Excel sheet name có giới hạn 31 ký tự
            string sheetName = $"{alignmentName}_{sampleLineGroupName}";

            // Loại bỏ các ký tự không hợp lệ
            char[] invalidChars = { '\\', '/', '*', '?', ':', '[', ']' };
            foreach (char c in invalidChars)
            {
                sheetName = sheetName.Replace(c, '_');
            }

            // Cắt ngắn nếu quá dài
            if (sheetName.Length > 31)
            {
                sheetName = sheetName.Substring(0, 31);
            }

            return sheetName;
        }

        private static void ExportMultipleSheetsToExcel(List<SheetData> allSheetData, string filePath, bool useDefaultSorting, int decimalPlaces)
        {
            try
            {
                using var workbook = new XLWorkbook();

                // Dictionary để lưu tổng khối lượng của từng đường/sheet
                // Key: SheetName, Value: Dictionary<MaterialType, TotalVolume>
                var summaryData = new List<SummaryRowData>();

                // Lấy danh sách tất cả các loại vật liệu từ tất cả các sheet
                var allMaterialTypes = new HashSet<string>();

                foreach (var sheetData in allSheetData)
                {
                    A.Ed.WriteMessage($"\n  📄 Tạo sheet: {sheetData.SheetName}");

                    // Xử lý dữ liệu cho sheet này - truyền thêm thông tin alignment, SampleLineGroup và số lượng SLG
                    var pivotData = CreatePivotTableData(sheetData.MaterialInfoList, sheetData.AlignmentName, sheetData.SampleLineGroupName, sheetData.SampleLineGroupCount, sheetData.QTOMaterialOrder, useDefaultSorting);

                    // Tạo worksheet
                    var worksheet = workbook.Worksheets.Add(sheetData.SheetName);

                    // Xuất dữ liệu vào sheet (truyền hasSummarySheet = true nếu có > 1 sheet)
                    bool hasSummarySheet = allSheetData.Count > 1;
                    ExportSheetData(worksheet, pivotData, sheetData.AlignmentName, sheetData.SampleLineGroupName, decimalPlaces, hasSummarySheet);

                    A.Ed.WriteMessage($"\n  ✓ Sheet '{sheetData.SheetName}': {pivotData.StakeInfos.Count} cọc, {pivotData.MaterialTypes.Count} vật liệu");

                    // Thu thập dữ liệu tổng hợp cho sheet TỔNG HỢP
                    // Dòng tổng cộng trong sheet chi tiết = 2 dòng thông tin (Dự án, Địa điểm) + Title row + Header row + số cọc + 1
                    int totalRowInSheet = 4 + pivotData.StakeInfos.Count + 1;
                    int volumeStartColInSheet = 3 + pivotData.MaterialTypes.Count;

                    var rowData = new SummaryRowData
                    {
                        AlignmentName = sheetData.AlignmentName,
                        SampleLineGroupName = sheetData.SampleLineGroupName,
                        SheetName = sheetData.SheetName,
                        TotalRowNumber = totalRowInSheet,
                        MaterialColumnMapping = new Dictionary<string, int>()
                    };

                    // Lưu vị trí cột của từng loại vật liệu (cột khối lượng)
                    for (int i = 0; i < pivotData.MaterialTypes.Count; i++)
                    {
                        string materialType = pivotData.MaterialTypes[i];
                        allMaterialTypes.Add(materialType);
                        // Cột khối lượng = volumeStartCol + i
                        rowData.MaterialColumnMapping[materialType] = volumeStartColInSheet + i;
                    }

                    summaryData.Add(rowData);
                }

                // Tạo sheet TỔNG HỢP và đặt ở đầu
                if (summaryData.Count > 1)
                {
                    A.Ed.WriteMessage($"\n  📄 Tạo sheet: TỔNG HỢP");
                    var summarySheet = workbook.Worksheets.Add("TỔNG HỢP");
                    ExportSummarySheet(summarySheet, summaryData, allMaterialTypes.ToList(), decimalPlaces);

                    // Di chuyển sheet TỔNG HỢP lên đầu
                    summarySheet.Position = 1;

                    A.Ed.WriteMessage($"\n  ✓ Sheet 'TỔNG HỢP': {summaryData.Count} đường");
                }

                // Save workbook
                workbook.SaveAs(filePath);
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n❌ Lỗi xuất Excel: {ex.Message}");
                throw;
            }
        }

        private static void ExportSummarySheet(IXLWorksheet worksheet, List<SummaryRowData> summaryData, List<string> materialTypes, int decimalPlaces)
        {
            try
            {
                int currentRow = 1;

                // Sắp xếp vật liệu theo thứ tự từ Material List (QTO)
                var orderedMaterials = SortMaterialsByPriority(materialTypes, _currentQTOMaterialOrder);
                int materialCount = orderedMaterials.Count;
                int totalCols = 1 + materialCount + 1; // Tên đường + các cột khối lượng + Ghi chú

                // ===== THÔNG TIN DỰ ÁN =====
                worksheet.Cell(currentRow, 1).Value = "Dự án:";
                worksheet.Cell(currentRow, 1).Style.Font.Italic = true;
                currentRow++;

                worksheet.Cell(currentRow, 1).Value = "Địa điểm:";
                worksheet.Cell(currentRow, 1).Style.Font.Italic = true;
                currentRow++;

                // ===== TITLE =====
                worksheet.Cell(currentRow, 1).Value = "BẢNG KHỐI LƯỢNG TẤT CẢ CÁC ĐƯỜNG";
                worksheet.Range(currentRow, 1, currentRow, totalCols).Merge();
                var titleCell = worksheet.Cell(currentRow, 1);
                titleCell.Style.Font.Bold = true;
                titleCell.Style.Font.FontSize = 14;
                titleCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                titleCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                titleCell.Style.Fill.BackgroundColor = XLColor.LightGreen;
                worksheet.Row(currentRow).Height = 30;
                currentRow++;

                // ===== HEADER =====
                worksheet.Cell(currentRow, 1).Value = "Tên đường";

                for (int i = 0; i < materialCount; i++)
                {
                    worksheet.Cell(currentRow, 2 + i).Value = $"{orderedMaterials[i]} (m³)";
                }

                // Cột Ghi chú
                worksheet.Cell(currentRow, totalCols).Value = "Ghi chú";

                // Style cho header
                for (int col = 1; col <= totalCols; col++)
                {
                    var headerCell = worksheet.Cell(currentRow, col);
                    headerCell.Style.Font.Bold = true;
                    headerCell.Style.Font.FontSize = 11;
                    headerCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    headerCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    headerCell.Style.Fill.BackgroundColor = XLColor.LightGray;
                    headerCell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }
                worksheet.Row(currentRow).Height = 20;
                currentRow++;

                // ===== DATA ROWS - Sử dụng công thức tham chiếu đến các sheet khác =====
                int dataStartRow = currentRow;
                foreach (var rowData in summaryData)
                {
                    // Tên đường (Alignment - SampleLineGroup)
                    string displayName = rowData.AlignmentName;
                    worksheet.Cell(currentRow, 1).Value = displayName;

                    // Khối lượng từng loại vật liệu - sử dụng công thức tham chiếu
                    for (int i = 0; i < materialCount; i++)
                    {
                        string materialType = orderedMaterials[i];

                        // Kiểm tra xem sheet có chứa loại vật liệu này không
                        if (rowData.MaterialColumnMapping.ContainsKey(materialType))
                        {
                            int colInSheet = rowData.MaterialColumnMapping[materialType];
                            string columnLetter = GetExcelColumnLetter(colInSheet);

                            // Tạo công thức tham chiếu: ='SheetName'!CellRef
                            // Cần escape tên sheet nếu chứa ký tự đặc biệt
                            string escapedSheetName = rowData.SheetName.Contains(" ") || rowData.SheetName.Contains("-")
                                ? $"'{rowData.SheetName}'"
                                : rowData.SheetName;
                            string formula = $"={escapedSheetName}!{columnLetter}{rowData.TotalRowNumber}";

                            worksheet.Cell(currentRow, 2 + i).FormulaA1 = formula;
                        }
                        else
                        {
                            // Nếu sheet không có loại vật liệu này, đặt giá trị 0
                            worksheet.Cell(currentRow, 2 + i).Value = 0;
                        }
                        worksheet.Cell(currentRow, 2 + i).Style.NumberFormat.Format = "0." + new string('0', decimalPlaces);
                    }

                    // Style cho data rows
                    for (int col = 1; col <= totalCols; col++)
                    {
                        var dataCell = worksheet.Cell(currentRow, col);
                        dataCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        dataCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                        dataCell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    }
                    // Căn trái cho cột tên đường
                    worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

                    currentRow++;
                }
                int dataEndRow = currentRow - 1;

                // ===== TOTAL ROW - Sử dụng công thức SUM =====
                worksheet.Cell(currentRow, 1).Value = "TỔNG CỘNG";

                for (int i = 0; i < materialCount; i++)
                {
                    // Sử dụng công thức SUM để tính tổng các ô phía trên
                    string columnLetter = GetExcelColumnLetter(2 + i);
                    string sumFormula = $"=SUM({columnLetter}{dataStartRow}:{columnLetter}{dataEndRow})";
                    worksheet.Cell(currentRow, 2 + i).FormulaA1 = sumFormula;
                    worksheet.Cell(currentRow, 2 + i).Style.NumberFormat.Format = "0." + new string('0', decimalPlaces);
                }

                // Style cho total row
                for (int col = 1; col <= totalCols; col++)
                {
                    var totalCell = worksheet.Cell(currentRow, col);
                    totalCell.Style.Font.Bold = true;
                    totalCell.Style.Font.FontSize = 11;
                    totalCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    totalCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    totalCell.Style.Fill.BackgroundColor = XLColor.LightYellow;
                    totalCell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }
                worksheet.Row(currentRow).Height = 22;

                // ===== COLUMN WIDTHS =====
                worksheet.Column(1).Width = 20;
                for (int i = 2; i <= totalCols - 1; i++)
                {
                    worksheet.Column(i).Width = 15;
                }
                worksheet.Column(totalCols).Width = 20; // Cột Ghi chú
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n❌ Lỗi tạo sheet tổng hợp: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Chuyển đổi số cột thành chữ cái cột Excel (1=A, 2=B, ..., 27=AA, ...)
        /// </summary>
        private static string GetExcelColumnLetter(int columnNumber)
        {
            string columnLetter = "";
            while (columnNumber > 0)
            {
                int modulo = (columnNumber - 1) % 26;
                columnLetter = Convert.ToChar('A' + modulo) + columnLetter;
                columnNumber = (columnNumber - modulo) / 26;
            }
            return columnLetter;
        }

        // Helper class for summary data
        private class SummaryRowData
        {
            public string AlignmentName { get; set; } = "";
            public string SampleLineGroupName { get; set; } = "";
            public string SheetName { get; set; } = "";
            public int TotalRowNumber { get; set; }
            public Dictionary<string, int> MaterialColumnMapping { get; set; } = new();
        }

        private static void ExportSheetData(IXLWorksheet worksheet, PivotTableData pivotData, string alignmentName, string sampleLineGroupName, int decimalPlaces, bool hasSummarySheet)
        {
            try
            {
                int currentRow = 1;
                string numberFormat = "0." + new string('0', decimalPlaces);
                int materialCount = pivotData.MaterialTypes.Count;

                // ===== THÔNG TIN DỰ ÁN =====
                // Tổng cột = 2 (Tên cọc, Khoảng cách) + số vật liệu (diện tích) + số vật liệu (khối lượng) + 1 (Ghi chú)
                int totalCols = 2 + materialCount + materialCount + 1;

                // Tham chiếu sheet TỔNG HỢP qua công thức nếu có nhiều sheet
                if (hasSummarySheet)
                {
                    worksheet.Cell(currentRow, 1).FormulaA1 = "='TỔNG HỢP'!A1";
                }
                else
                {
                    worksheet.Cell(currentRow, 1).Value = "Dự án:";
                }
                worksheet.Cell(currentRow, 1).Style.Font.Italic = true;
                currentRow++;

                if (hasSummarySheet)
                {
                    worksheet.Cell(currentRow, 1).FormulaA1 = "='TỔNG HỢP'!A2";
                }
                else
                {
                    worksheet.Cell(currentRow, 1).Value = "Địa điểm:";
                }
                worksheet.Cell(currentRow, 1).Style.Font.Italic = true;
                currentRow++;

                // ===== TITLE =====
                string title = $"BẢNG KHỐI LƯỢNG VẬT LIỆU - {alignmentName}";

                worksheet.Cell(currentRow, 1).Value = title;
                worksheet.Range(currentRow, 1, currentRow, totalCols).Merge();
                var titleCell = worksheet.Cell(currentRow, 1);
                titleCell.Style.Font.Bold = true;
                titleCell.Style.Font.FontSize = 14;
                titleCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                titleCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                titleCell.Style.Fill.BackgroundColor = XLColor.LightBlue;
                worksheet.Row(currentRow).Height = 30;
                currentRow++;

                // Bỏ dòng Subtitle (SampleLineGroup)

                // ===== HEADER =====
                int headerRow = currentRow;
                worksheet.Cell(currentRow, 1).Value = "Tên cọc";
                worksheet.Cell(currentRow, 2).Value = "Khoảng cách lẻ (m)";

                // Cột diện tích (m²)
                for (int i = 0; i < materialCount; i++)
                {
                    worksheet.Cell(currentRow, 3 + i).Value = $"{pivotData.MaterialTypes[i]} (m²)";
                }

                // Cột khối lượng (m³)
                int volumeStartCol = 3 + materialCount;
                for (int i = 0; i < materialCount; i++)
                {
                    worksheet.Cell(currentRow, volumeStartCol + i).Value = $"{pivotData.MaterialTypes[i]} (m³)";
                }

                // Cột Ghi chú
                worksheet.Cell(currentRow, totalCols).Value = "Ghi chú";

                // Style cho header
                for (int col = 1; col <= totalCols; col++)
                {
                    var headerCell = worksheet.Cell(currentRow, col);
                    headerCell.Style.Font.Bold = true;
                    headerCell.Style.Font.FontSize = 11;
                    headerCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    headerCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    headerCell.Style.Fill.BackgroundColor = XLColor.LightGray;
                    headerCell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }
                worksheet.Row(currentRow).Height = 20;
                currentRow++;

                // ===== DATA ROWS =====
                int dataStartRow = currentRow;

                // Xử lý từng dòng dữ liệu
                for (int rowIdx = 0; rowIdx < pivotData.StakeInfos.Count; rowIdx++)
                {
                    var stakeInfo = pivotData.StakeInfos[rowIdx];

                    worksheet.Cell(currentRow, 1).Value = stakeInfo.StakeName;

                    // Làm tròn khoảng cách đến số chữ số thập phân đã chọn
                    double spacingRounded = Math.Round(stakeInfo.SpacingPrev, decimalPlaces);
                    worksheet.Cell(currentRow, 2).Value = spacingRounded;
                    worksheet.Cell(currentRow, 2).Style.NumberFormat.Format = numberFormat;

                    // Xuất cột diện tích (m²) - giữ giá trị trực tiếp vì đây là dữ liệu gốc
                    // MaterialAdditionalValues chỉ cộng vào dòng ĐẦU TIÊN (rowIdx == 0) để tránh cộng trùng
                    for (int i = 0; i < materialCount; i++)
                    {
                        string materialType = pivotData.MaterialTypes[i];
                        double area = stakeInfo.MaterialAreas.ContainsKey(materialType) ? stakeInfo.MaterialAreas[materialType] : 0.0;

                        if (rowIdx == 0 && pivotData.MaterialAdditionalValues.ContainsKey(materialType))
                        {
                            area += pivotData.MaterialAdditionalValues[materialType];
                        }

                        // Làm tròn đến số chữ số thập phân đã chọn
                        double areaRounded = Math.Round(area, decimalPlaces);

                        // Luôn hiển thị giá trị số (0 thay vì "-")
                        worksheet.Cell(currentRow, 3 + i).Value = areaRounded;
                        worksheet.Cell(currentRow, 3 + i).Style.NumberFormat.Format = numberFormat;
                    }

                    // Xuất cột khối lượng (m³) - SỬ DỤNG CÔNG THỨC EXCEL
                    // Công thức: =(Diện tích trước + Diện tích sau) / 2 * Khoảng cách
                    for (int i = 0; i < materialCount; i++)
                    {
                        int areaCol = 3 + i; // Cột diện tích tương ứng
                        string areaColLetter = GetExcelColumnLetter(areaCol);
                        string spacingColLetter = GetExcelColumnLetter(2); // Cột B - khoảng cách

                        if (rowIdx == 0)
                        {
                            // Dòng đầu tiên: không có diện tích trước, khối lượng = 0
                            // Công thức: =(0 + D3)/2*C3 = 0 (vì C3 = 0 cho dòng đầu)
                            string formula = $"=({areaColLetter}{currentRow}+0)/2*{spacingColLetter}{currentRow}";
                            worksheet.Cell(currentRow, volumeStartCol + i).FormulaA1 = formula;
                        }
                        else
                        {
                            // Các dòng sau: có diện tích trước
                            // Công thức: =(D_prev + D_curr)/2*C_curr
                            int prevRow = currentRow - 1;
                            string formula = $"=({areaColLetter}{prevRow}+{areaColLetter}{currentRow})/2*{spacingColLetter}{currentRow}";
                            worksheet.Cell(currentRow, volumeStartCol + i).FormulaA1 = formula;
                        }
                        worksheet.Cell(currentRow, volumeStartCol + i).Style.NumberFormat.Format = numberFormat;
                    }

                    // Style cho data rows
                    for (int col = 1; col <= totalCols; col++)
                    {
                        var dataCell = worksheet.Cell(currentRow, col);
                        dataCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        dataCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                        dataCell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    }

                    currentRow++;
                }
                int dataEndRow = currentRow - 1;

                // ===== TOTAL ROW - SỬ DỤNG CÔNG THỨC SUM =====
                worksheet.Cell(currentRow, 1).Value = "TỔNG CỘNG";

                // Tổng khoảng cách - sử dụng công thức SUM
                string spacingCol = GetExcelColumnLetter(2);
                string sumSpacingFormula = $"=SUM({spacingCol}{dataStartRow}:{spacingCol}{dataEndRow})";
                worksheet.Cell(currentRow, 2).FormulaA1 = sumSpacingFormula;
                worksheet.Cell(currentRow, 2).Style.NumberFormat.Format = numberFormat;

                // Tổng cột diện tích (m²) - sử dụng công thức SUM
                for (int i = 0; i < materialCount; i++)
                {
                    string colLetter = GetExcelColumnLetter(3 + i);
                    string sumFormula = $"=SUM({colLetter}{dataStartRow}:{colLetter}{dataEndRow})";
                    worksheet.Cell(currentRow, 3 + i).FormulaA1 = sumFormula;
                    worksheet.Cell(currentRow, 3 + i).Style.NumberFormat.Format = numberFormat;
                }

                // Tổng cột khối lượng (m³) - sử dụng công thức SUM
                for (int i = 0; i < materialCount; i++)
                {
                    string colLetter = GetExcelColumnLetter(volumeStartCol + i);
                    string sumFormula = $"=SUM({colLetter}{dataStartRow}:{colLetter}{dataEndRow})";
                    worksheet.Cell(currentRow, volumeStartCol + i).FormulaA1 = sumFormula;
                    worksheet.Cell(currentRow, volumeStartCol + i).Style.NumberFormat.Format = numberFormat;
                }

                // Style cho total row - Đổi từ Medium sang Thin
                for (int col = 1; col <= totalCols; col++)
                {
                    var totalCell = worksheet.Cell(currentRow, col);
                    totalCell.Style.Font.Bold = true;
                    totalCell.Style.Font.FontSize = 11;
                    totalCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    totalCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    totalCell.Style.Fill.BackgroundColor = XLColor.LightYellow;
                    totalCell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }
                worksheet.Row(currentRow).Height = 22;

                // ===== COLUMN WIDTHS =====
                worksheet.Column(1).Width = 12;
                worksheet.Column(2).Width = 15;
                for (int i = 3; i <= totalCols - 1; i++)
                {
                    worksheet.Column(i).Width = 15;
                }
                worksheet.Column(totalCols).Width = 20; // Cột Ghi chú

                // ===== BỎ AUTO FILTER =====
                // Đã bỏ dòng: worksheet.Range(2, 1, dataEndRow, totalCols).SetAutoFilter();
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n❌ Lỗi xuất sheet: {ex.Message}");
                throw;
            }
        }

        private static List<MaterialVolumeInfo> CollectMaterialVolumeInformation(SampleLineGroup sampleLineGroup, Transaction tr)
        {
            List<MaterialVolumeInfo> materialInfoList = new();

            try
            {
                // ===== Lấy thứ tự materials từ QTOMaterialList =====
                _currentQTOMaterialOrder = GetMaterialOrderFromQTO(sampleLineGroup);
                if (_currentQTOMaterialOrder.Count > 0)
                {
                    A.Ed.WriteMessage($"\n  📋 Thứ tự vật liệu từ Material List: {string.Join(", ", _currentQTOMaterialOrder)}");
                }
                // ===================================


                // Lấy tất cả sample lines trong group
                ObjectIdCollection sampleLineIds = sampleLineGroup.GetSampleLineIds();
                A.Ed.WriteMessage($"\n  - Tìm thấy {sampleLineIds.Count} sample lines trong group.");

                // Duyệt qua từng SampleLine
                foreach (ObjectId sampleLineId in sampleLineIds)
                {
                    try
                    {
                        SampleLine? sampleLine = tr.GetObject(sampleLineId, OpenMode.ForRead) as SampleLine;
                        if (sampleLine == null) continue;

                        double station = sampleLine.Station;
                        string stakeName = sampleLine.Name ?? FormatStation(station);

                        A.Ed.WriteMessage($"\n  📍 Xử lý cọc: {stakeName} (Station: {FormatStation(station)})");

                        // Lấy tất cả Section IDs từ SampleLine
                        ObjectIdCollection sectionIds = sampleLine.GetSectionIds();
                        A.Ed.WriteMessage($"\n     - Tìm thấy {sectionIds.Count} sections");

                        // Dictionary để lưu MaterialSection duy nhất cho mỗi material name
                        Dictionary<string, (double Area, string SourceName)> uniqueMaterials = new();

                        // Duyệt qua từng Section để tìm MaterialSection
                        foreach (ObjectId sectionId in sectionIds)
                        {
                            try
                            {
                                Autodesk.AutoCAD.DatabaseServices.DBObject dbObj = tr.GetObject(sectionId, OpenMode.ForRead);

                                // Kiểm tra nếu là MaterialSection
                                if (dbObj is MaterialSection materialSection)
                                {
                                    string sourceName = materialSection.SourceName ?? "Không có tên";
                                    string materialName = ExtractMaterialNameFromSource(sourceName);
                                    double area = GetMaterialSectionArea(materialSection, useShoelace: true);

                                    // Chỉ thêm hoặc cập nhật nếu chưa có hoặc diện tích lớn hơn
                                    if (!uniqueMaterials.ContainsKey(materialName))
                                    {
                                        uniqueMaterials[materialName] = (area, sourceName);
                                        A.Ed.WriteMessage($"\n     ✓ {materialName}: {area:F3} m²");
                                    }
                                    else if (area > uniqueMaterials[materialName].Area)
                                    {
                                        A.Ed.WriteMessage($"\n     ⚠️  Cập nhật {materialName}: {uniqueMaterials[materialName].Area:F3} → {area:F3} m²");
                                        uniqueMaterials[materialName] = (area, sourceName);
                                    }
                                    else
                                    {
                                        A.Ed.WriteMessage($"\n     ℹ️  Bỏ qua duplicate {materialName}: {area:F3} m² (đã có {uniqueMaterials[materialName].Area:F3} m²)");
                                    }
                                }
                            }
                            catch (System.Exception ex)
                            {
                                A.Ed.WriteMessage($"\n     ⚠️  Lỗi xử lý section: {ex.Message}");
                                continue;
                            }
                        }

                        // Thêm tất cả material duy nhất vào list
                        foreach (var kvp in uniqueMaterials)
                        {
                            MaterialVolumeInfo info = new()
                            {
                                StakeName = stakeName,
                                Station = FormatStation(station),
                                StationValue = station,
                                MaterialName = kvp.Key,
                                Area = kvp.Value.Area,
                                SourceName = kvp.Value.SourceName
                            };
                            materialInfoList.Add(info);
                        }

                        A.Ed.WriteMessage($"\n     → Tổng: {uniqueMaterials.Count} loại vật liệu duy nhất");
                    }
                    catch (System.Exception ex)
                    {
                        A.Ed.WriteMessage($"\n  ❌ Lỗi xử lý sample line: {ex.Message}");
                        continue;
                    }
                }

                // Sắp xếp theo lý trình
                materialInfoList = materialInfoList.OrderBy(x => x.StationValue).ToList();

                A.Ed.WriteMessage($"\n\n  ✅ Tổng cộng: {materialInfoList.Count} mục khối lượng vật liệu đã được thu thập.");

                // Hiển thị thống kê
                var materialStats = materialInfoList
                    .GroupBy(x => x.MaterialName)
                    .Select(g => new { Material = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count);

                A.Ed.WriteMessage("\n  📊 Thống kê vật liệu:");
                foreach (var stat in materialStats)
                {
                    A.Ed.WriteMessage($"\n     - {stat.Material}: {stat.Count} cọc");
                }
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n❌ Lỗi khi thu thập thông tin material: {ex.Message}");
            }

            return materialInfoList;
        }

        private static string ExtractMaterialNameFromSource(string sourceName)
        {
            try
            {
                if (string.IsNullOrEmpty(sourceName))
                    return "Không xác định";

                int dashIndex = sourceName.LastIndexOf('-');
                if (dashIndex >= 0 && dashIndex < sourceName.Length - 1)
                {
                    string materialName = sourceName.Substring(dashIndex + 1).Trim();
                    return string.IsNullOrEmpty(materialName) ? "Không xác định" : materialName;
                }

                return sourceName.Trim();
            }
            catch
            {
                return "Không xác định";
            }
        }


        #region ===== TEXT-BASED QUANTITY COLLECTION =====

        /// <summary>
        /// Thu thập khối lượng vật liệu từ Text (Dạng 1: QTO Table explode + Dạng 2: Text vàng)
        /// </summary>
        private static List<MaterialVolumeInfo> CollectMaterialVolumeFromText(SampleLineGroup sampleLineGroup, Transaction tr, double offsetXPercent = 5, double offsetYPercent = 15, bool drawRangePolyline = false)
        {
            List<MaterialVolumeInfo> materialInfoList = new();

            try
            {
                // Lấy thứ tự materials từ QTOMaterialList
                _currentQTOMaterialOrder = GetMaterialOrderFromQTO(sampleLineGroup);
                if (_currentQTOMaterialOrder.Count > 0)
                {
                    A.Ed.WriteMessage($"\n  📋 Thứ tự vật liệu từ Material List: {string.Join(", ", _currentQTOMaterialOrder)}");
                }

                // ===== Build SectionView map từ SectionViewGroup API (chính xác cho multi-group) =====
                var sectionViewMap = new Dictionary<ObjectId, SectionView>();

                SectionViewGroupCollection sectionViewGroupCollection = sampleLineGroup.SectionViewGroups;
                A.Ed.WriteMessage($"\n  📊 Tìm thấy {sectionViewGroupCollection.Count} SectionViewGroup(s)");

                // Duyệt tất cả SectionViewGroups để tìm SectionViews
                foreach (SectionViewGroup svGroup in sectionViewGroupCollection)
                {
                    ObjectIdCollection sectionViewIds = svGroup.GetSectionViewIds();
                    foreach (ObjectId svId in sectionViewIds)
                    {
                        try
                        {
                            SectionView? sv = tr.GetObject(svId, OpenMode.ForRead) as SectionView;
                            if (sv != null)
                            {
                                // Nếu đã có SectionView cho SampleLine này, giữ cái đầu tiên
                                if (!sectionViewMap.ContainsKey(sv.SampleLineId))
                                {
                                    sectionViewMap[sv.SampleLineId] = sv;
                                }
                            }
                        }
                        catch { }
                    }
                }

                A.Ed.WriteMessage($"\n  📊 Mapped {sectionViewMap.Count} SectionViews cho {sampleLineGroup.GetSampleLineIds().Count} sample lines");

                // ===== Thu thập AECC_TABLE entities và text vàng từ ModelSpace =====
                BlockTable? bt = tr.GetObject(A.Db.BlockTableId, OpenMode.ForRead) as BlockTable;
                if (bt == null) return materialInfoList;
                BlockTableRecord? btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
                if (btr == null) return materialInfoList;

                var allAeccTables = new List<Autodesk.AutoCAD.DatabaseServices.Entity>();
                var allYellowTexts = new List<(string Text, double X, double Y)>();

                RXClass rxText = RXClass.GetClass(typeof(DBText));
                RXClass rxMText = RXClass.GetClass(typeof(MText));
                foreach (ObjectId entityId in btr)
                {
                    try
                    {
                        string dxfName = entityId.ObjectClass.DxfName;
                        bool isTable = dxfName.Contains("AECC") && dxfName.Contains("TABLE");
                        bool isText = entityId.ObjectClass.IsDerivedFrom(rxText) || entityId.ObjectClass.IsDerivedFrom(rxMText);
                        if (!isTable && !isText) continue;
                        var dbObj = tr.GetObject(entityId, OpenMode.ForRead);
                        if (isTable && dbObj is Autodesk.AutoCAD.DatabaseServices.Entity ent)
                        {
                            allAeccTables.Add(ent);
                        }
                        // Thu thập text vàng (ColorIndex = 2)
                        else if (dbObj is DBText dbText && dbText.ColorIndex == 2)
                        {
                            allYellowTexts.Add((dbText.TextString ?? "", dbText.Position.X, dbText.Position.Y));
                        }
                        else if (dbObj is MText mText && mText.ColorIndex == 2)
                        {
                            string cleanText = CleanMTextFormatting(mText.Contents ?? "");
                            allYellowTexts.Add((cleanText, mText.Location.X, mText.Location.Y));
                        }
                    }
                    catch { }
                }

                A.Ed.WriteMessage($"\n  📊 ModelSpace: {allAeccTables.Count} AECC Tables, {allYellowTexts.Count} text vàng");

                // ===== DẠNG 1: Flatten-explode tất cả QTO Tables → thu thập TẤT CẢ texts + tọa độ =====
                var allTextsFromTables = new List<(string Text, double X, double Y)>();

                foreach (var tableEntity in allAeccTables)
                {
                    try
                    {
                        FlattenExplodeToTexts(tableEntity, allTextsFromTables);
                    }
                    catch (System.Exception ex)
                    {
                        A.Ed.WriteMessage($"\n  ⚠️ Lỗi explode QTO Table: {ex.Message}");
                    }
                }

                A.Ed.WriteMessage($"\n  📦 Flatten explode: {allTextsFromTables.Count} texts từ {allAeccTables.Count} QTO Tables");

                // ===== Duyệt từng SampleLine =====
                ObjectIdCollection sampleLineIds = sampleLineGroup.GetSampleLineIds();
                A.Ed.WriteMessage($"\n  - Tìm thấy {sampleLineIds.Count} sample lines trong group.");

                foreach (ObjectId sampleLineId in sampleLineIds)
                {
                    try
                    {
                        SampleLine? sampleLine = tr.GetObject(sampleLineId, OpenMode.ForRead) as SampleLine;
                        if (sampleLine == null) continue;

                        double station = sampleLine.Station;
                        string stakeName = sampleLine.Name ?? FormatStation(station);

                        A.Ed.WriteMessage($"\n  📍 Xử lý cọc: {stakeName} (Station: {FormatStation(station)})");

                        // Tìm SectionView tương ứng từ map đã build từ SectionViewGroup
                        if (!sectionViewMap.TryGetValue(sampleLineId, out SectionView? sectionView) || sectionView == null)
                        {
                            A.Ed.WriteMessage($"\n     ⚠️  Không tìm thấy SectionView cho cọc này → bỏ qua");
                            continue;
                        }

                        // Lấy bounding box của SectionView (mở rộng theo tham số)
                        var svBounds = sectionView.GeometricExtents;
                        double svWidth = svBounds.MaxPoint.X - svBounds.MinPoint.X;
                        double svHeight = svBounds.MaxPoint.Y - svBounds.MinPoint.Y;
                        double marginX = svWidth * (offsetXPercent / 100.0);
                        double marginY = svHeight * (offsetYPercent / 100.0);
                        double svMinX = svBounds.MinPoint.X - marginX;
                        double svMinY = svBounds.MinPoint.Y - marginY;
                        double svMaxX = svBounds.MaxPoint.X + marginX;
                        double svMaxY = svBounds.MaxPoint.Y + marginY;

                        // Vẽ Polyline thể hiện phạm vi lọc text (nếu được bật)
                        if (drawRangePolyline)
                        {
                            try
                            {
                                BlockTable? btPoly = tr.GetObject(A.Db.BlockTableId, OpenMode.ForRead) as BlockTable;
                                if (btPoly != null)
                                {
                                    BlockTableRecord? btrPoly = tr.GetObject(btPoly[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                                    if (btrPoly != null)
                                    {
                                        Polyline rangePoly = new Polyline();
                                        rangePoly.AddVertexAt(0, new Point2d(svMinX, svMinY), 0, 0, 0);
                                        rangePoly.AddVertexAt(1, new Point2d(svMaxX, svMinY), 0, 0, 0);
                                        rangePoly.AddVertexAt(2, new Point2d(svMaxX, svMaxY), 0, 0, 0);
                                        rangePoly.AddVertexAt(3, new Point2d(svMinX, svMaxY), 0, 0, 0);
                                        rangePoly.Closed = true;
                                        rangePoly.ColorIndex = 4; // Cyan
                                        rangePoly.Layer = "0";
                                        btrPoly.AppendEntity(rangePoly);
                                        tr.AddNewlyCreatedDBObject(rangePoly, true);
                                    }
                                }
                            }
                            catch (System.Exception ex)
                            {
                                A.Ed.WriteMessage($"\n     ⚠️  Lỗi vẽ polyline phạm vi: {ex.Message}");
                            }
                        }

                        // Ordered list để giữ thứ tự theo Y (trên → dưới)
                        var materialAreas = new Dictionary<string, double>();
                        var materialOrder = new List<string>(); // Thứ tự vật liệu theo Y (trên → dưới)
                        var materialSourceType = new Dictionary<string, int>(); // 1 = Dạng 1 (QTO Table), 2 = Dạng 2 (Text vàng)

                        // ===== DẠNG 1: Coordinate-based matching =====
                        // Lọc texts từ QTO Tables nằm trong SectionView
                        int qtoCount = 0;
                        var textsInSV = allTextsFromTables
                            .Where(t => t.X >= svMinX && t.X <= svMaxX && t.Y >= svMinY && t.Y <= svMaxY)
                            .ToList();

                        if (textsInSV.Count > 0)
                        {
                            A.Ed.WriteMessage($"\n     🔍 Tìm thấy {textsInSV.Count} texts từ QTO Table trong SectionView");

                            // === BƯỚC 0: Loại bỏ text trùng lặp ===
                            // FlattenExplodeToTexts explode 2 cấp có thể tạo ra text trùng (cùng nội dung, cùng vị trí)
                            int beforeDedup = textsInSV.Count;
                            textsInSV = textsInSV
                                .GroupBy(t => (t.Text, XRound: Math.Round(t.X, 0), YRound: Math.Round(t.Y, 0)))
                                .Select(g => g.First())
                                .ToList();
                            if (textsInSV.Count < beforeDedup)
                            {
                                A.Ed.WriteMessage($"\n     🔄 Loại bỏ {beforeDedup - textsInSV.Count} text trùng lặp → còn {textsInSV.Count}");
                            }

                            // === BƯỚC 1: Phân loại texts ===
                            var nameTexts = new List<(string Name, double X, double Y)>();
                            var valueTexts = new List<(double Value, string Unit, double X, double Y)>();

                            foreach (var (text, x, y) in textsInSV)
                            {
                                string trimmed = text.Trim();

                                // Kiểm tra nếu text kết thúc bằng ":" → name text
                                if (trimmed.EndsWith(":"))
                                {
                                    string cleanName = trimmed.TrimEnd(':', ' ');
                                    if (!string.IsNullOrWhiteSpace(cleanName))
                                    {
                                        nameTexts.Add((cleanName, x, y));
                                    }
                                    continue;
                                }

                                // Kiểm tra nếu text bắt đầu bằng số → value text (bao gồm cả giá trị 0.00)
                                if (Regex.IsMatch(trimmed, @"^\d"))
                                {
                                    var (numVal, unit) = ExtractNumberAndUnit(trimmed);
                                    valueTexts.Add((numVal, unit, x, y));
                                    continue;
                                }

                                // Text không phải name cũng không phải value → bỏ qua
                            }

                            A.Ed.WriteMessage($"\n     📊 Phân loại: {nameTexts.Count} tên, {valueTexts.Count} giá trị");

                            // === BƯỚC 2: Loại bỏ nameTexts trùng tên (giữ entry đầu tiên theo Y cao nhất) ===
                            var uniqueNameTexts = nameTexts
                                .OrderByDescending(n => n.Y)
                                .GroupBy(n => n.Name)
                                .Select(g => g.First())
                                .OrderByDescending(n => n.Y)
                                .ToList();
                            if (uniqueNameTexts.Count < nameTexts.Count)
                            {
                                A.Ed.WriteMessage($"\n     🔄 Loại bỏ {nameTexts.Count - uniqueNameTexts.Count} tên trùng lặp → còn {uniqueNameTexts.Count}");
                            }

                            // === BƯỚC 3: Ghép cặp name-value theo cùng Y (coordinate-based) ===
                            double yTolerance = 1.0;
                            var usedValueIndices = new HashSet<int>();

                            foreach (var (name, nameX, nameY) in uniqueNameTexts)
                            {
                                // Tìm value text có cùng Y (tolerance nhỏ), chưa được sử dụng
                                int bestIdx = -1;
                                double bestYDist = double.MaxValue;

                                for (int vi = 0; vi < valueTexts.Count; vi++)
                                {
                                    if (usedValueIndices.Contains(vi)) continue;
                                    double yDist = Math.Abs(valueTexts[vi].Y - nameY);
                                    if (yDist < yTolerance && yDist < bestYDist)
                                    {
                                        bestYDist = yDist;
                                        bestIdx = vi;
                                    }
                                }

                                if (bestIdx >= 0)
                                {
                                    usedValueIndices.Add(bestIdx);
                                    double numValue = valueTexts[bestIdx].Value;
                                    string unit = valueTexts[bestIdx].Unit;

                                    if (!materialAreas.ContainsKey(name))
                                    {
                                        materialOrder.Add(name);
                                        materialSourceType[name] = 1;
                                    }
                                    materialAreas[name] = numValue;
                                    qtoCount++;
                                    A.Ed.WriteMessage($"\n     📝 [{name}] = {numValue} {unit} (Y gap={bestYDist:F2})");
                                }
                                else
                                {
                                    A.Ed.WriteMessage($"\n     ⚠️  Không tìm thấy giá trị cho [{name}] (Y={nameY:F1})");
                                }
                            }
                        }

                        if (qtoCount > 0)
                        {
                            A.Ed.WriteMessage($"\n     ✓ Dạng 1 (QTO Table): {qtoCount} giá trị từ {materialAreas.Count} vật liệu");
                        }
                        else
                        {
                            A.Ed.WriteMessage($"\n     ℹ️  Không tìm thấy QTO Table block trong SectionView này");
                        }

                        // ===== DẠNG 2: Text vàng trong SectionView (bổ sung) =====
                        // Dùng coordinate-based matching tương tự Dạng 1:
                        // - Name texts: kết thúc bằng ":"
                        // - Value texts: bắt đầu bằng số
                        // - Single texts: chứa cả tên + giá trị (ví dụ "Rọ đá: 21.00 m2")
                        int yellowCount = 0;

                        // Thu thập yellow texts trong bounds (đã mở rộng 10%)
                        var yellowTextsInSV = new List<(string Text, double X, double Y)>();
                        foreach (var (text, x, y) in allYellowTexts)
                        {
                            if (string.IsNullOrWhiteSpace(text)) continue;
                            if (x < svMinX || x > svMaxX) continue;
                            if (y < svMinY || y > svMaxY) continue;
                            yellowTextsInSV.Add((text, x, y));
                        }

                        if (yellowTextsInSV.Count > 0)
                        {
                            A.Ed.WriteMessage($"\n     🔍 Tìm thấy {yellowTextsInSV.Count} text vàng trong SectionView");

                            // Phân loại texts
                            var yellowNameTexts = new List<(string Name, double X, double Y, int Index)>();
                            var yellowValueTexts = new List<(double Value, string Unit, double X, double Y, int Index)>();
                            var yellowSingleResults = new List<(string Name, double Value, double Y)>();

                            for (int i = 0; i < yellowTextsInSV.Count; i++)
                            {
                                string trimmed = yellowTextsInSV[i].Text.Trim();

                                // Thử parse dạng "Tên: giá_trị đơn_vị" (single text)
                                var parsedSingle = TryParseYellowText(trimmed);
                                if (parsedSingle.HasValue && parsedSingle.Value.Name != "Text vàng")
                                {
                                    yellowSingleResults.Add((parsedSingle.Value.Name, parsedSingle.Value.Value, yellowTextsInSV[i].Y));
                                    A.Ed.WriteMessage($"\n     📗 [{parsedSingle.Value.Name}] = {parsedSingle.Value.Value} (single text vàng)");
                                    continue;
                                }

                                // Name text: kết thúc bằng ":"
                                if (trimmed.EndsWith(":"))
                                {
                                    string cleanName = trimmed.TrimEnd(':', ' ');
                                    if (!string.IsNullOrWhiteSpace(cleanName))
                                    {
                                        yellowNameTexts.Add((cleanName, yellowTextsInSV[i].X, yellowTextsInSV[i].Y, i));
                                    }
                                    continue;
                                }

                                // Value text: bắt đầu bằng số (bao gồm cả giá trị 0.00)
                                if (Regex.IsMatch(trimmed, @"^\d"))
                                {
                                    var (numVal, unit) = ExtractNumberAndUnit(trimmed);
                                    yellowValueTexts.Add((numVal, unit, yellowTextsInSV[i].X, yellowTextsInSV[i].Y, i));
                                    continue;
                                }

                                // Text không có ":" và không phải số → name text không có ":"
                                string fallbackName = trimmed.TrimEnd(':', '=', ' ');
                                if (!string.IsNullOrWhiteSpace(fallbackName))
                                {
                                    yellowNameTexts.Add((fallbackName, yellowTextsInSV[i].X, yellowTextsInSV[i].Y, i));
                                }
                            }

                            A.Ed.WriteMessage($"\n     📊 Phân loại text vàng: {yellowNameTexts.Count} tên, {yellowValueTexts.Count} giá trị, {yellowSingleResults.Count} text hỗn hợp");

                            // Ghép cặp name-value theo cùng Y (coordinate-based)
                            double yellowYTolerance = 1.0;
                            var usedYellowValueIndices = new HashSet<int>();
                            var yellowPairResults = new List<(string Name, double Value, double Y)>();

                            // Sắp xếp name texts theo Y giảm dần (trên → dưới)
                            var sortedYellowNames = yellowNameTexts.OrderByDescending(n => n.Y).ToList();

                            foreach (var (name, nameX, nameY, nameIdx) in sortedYellowNames)
                            {
                                int bestIdx = -1;
                                double bestYDist = double.MaxValue;

                                for (int vi = 0; vi < yellowValueTexts.Count; vi++)
                                {
                                    if (usedYellowValueIndices.Contains(vi)) continue;
                                    double yDist = Math.Abs(yellowValueTexts[vi].Y - nameY);
                                    if (yDist < yellowYTolerance && yDist < bestYDist)
                                    {
                                        bestYDist = yDist;
                                        bestIdx = vi;
                                    }
                                }

                                if (bestIdx >= 0)
                                {
                                    usedYellowValueIndices.Add(bestIdx);
                                    yellowPairResults.Add((name, yellowValueTexts[bestIdx].Value, nameY));
                                    A.Ed.WriteMessage($"\n     📘 [{name}] = {yellowValueTexts[bestIdx].Value} {yellowValueTexts[bestIdx].Unit} (ghép cặp, Y gap={bestYDist:F2})");
                                }
                            }

                            // Value texts chưa ghép được → dùng tên "Text vàng"
                            for (int vi = 0; vi < yellowValueTexts.Count; vi++)
                            {
                                if (usedYellowValueIndices.Contains(vi)) continue;
                                yellowPairResults.Add(("Text vàng", yellowValueTexts[vi].Value, yellowValueTexts[vi].Y));
                                A.Ed.WriteMessage($"\n     📙 [Text vàng] = {yellowValueTexts[vi].Value} (không tìm thấy tên)");
                            }

                            // Gộp tất cả kết quả (single + paired), sắp xếp theo Y giảm dần
                            var allYellowResults = yellowSingleResults.Concat(yellowPairResults)
                                .OrderByDescending(r => r.Y)
                                .ToList();

                            foreach (var (matName, matValue, _) in allYellowResults)
                            {
                                // Chỉ thêm nếu Dạng 1 chưa có vật liệu này
                                if (!materialAreas.ContainsKey(matName))
                                {
                                    materialAreas[matName] = matValue;
                                    materialOrder.Add(matName);
                                    materialSourceType[matName] = 2; // Dạng 2: Text vàng
                                    yellowCount++;
                                    A.Ed.WriteMessage($"\n     🟡 [{matName}] = {matValue} (text vàng)");
                                }
                                else
                                {
                                    A.Ed.WriteMessage($"\n     ℹ️  Bỏ qua [{matName}] = {matValue} (đã có từ Dạng 1)");
                                }
                            }
                        }

                        if (yellowCount > 0)
                        {
                            A.Ed.WriteMessage($"\n     ✓ Dạng 2 (Text vàng): {yellowCount} giá trị bổ sung");
                        }

                        // Chỉ thêm vào kết quả nếu có dữ liệu
                        if (materialAreas.Count > 0)
                        {
                            // Thêm theo thứ tự materialOrder (trên → dưới)
                            foreach (var matName in materialOrder)
                            {
                                if (materialAreas.TryGetValue(matName, out double area))
                                {
                                    // Xác định SourceType: 1 = QTO Table (Dạng 1), 2 = Text vàng (Dạng 2)
                                    int sourceType = materialSourceType.ContainsKey(matName) ? materialSourceType[matName] : 1;
                                    materialInfoList.Add(new MaterialVolumeInfo
                                    {
                                        StakeName = stakeName,
                                        Station = FormatStation(station),
                                        StationValue = station,
                                        MaterialName = matName,
                                        Area = area,
                                        SourceName = "Text",
                                        SourceType = sourceType
                                    });
                                }
                            }
                            A.Ed.WriteMessage($"\n     → Tổng: {materialAreas.Count} loại vật liệu");
                        }
                        else
                        {
                            A.Ed.WriteMessage($"\n     ⚠️  Không có dữ liệu khối lượng cho cọc này (không có QTO Table hoặc text vàng)");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        A.Ed.WriteMessage($"\n  ❌ Lỗi xử lý sample line: {ex.Message}");
                        continue;
                    }
                }

                // Không cần cleanup vì texts là value types

                // Sắp xếp theo lý trình
                materialInfoList = materialInfoList.OrderBy(x => x.StationValue).ToList();

                A.Ed.WriteMessage($"\n\n  ✅ Tổng cộng: {materialInfoList.Count} mục khối lượng vật liệu đã được thu thập (phương pháp Text).");

                // Hiển thị thống kê
                var materialStats = materialInfoList
                    .GroupBy(x => x.MaterialName)
                    .Select(g => new { Material = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count);

                A.Ed.WriteMessage("\n  📊 Thống kê vật liệu:");
                foreach (var stat in materialStats)
                {
                    A.Ed.WriteMessage($"\n     - {stat.Material}: {stat.Count} cọc");
                }
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n❌ Lỗi khi thu thập thông tin material từ text: {ex.Message}");
            }

            return materialInfoList;
        }


        /// <summary>
        /// Flatten-explode entity (AECC_TABLE) qua 2 cấp để thu thập TẤT CẢ texts + tọa độ
        /// Không phụ thuộc vào khoảng cách giữa các dòng
        /// </summary>
        private static void FlattenExplodeToTexts(Autodesk.AutoCAD.DatabaseServices.Entity entity, List<(string Text, double X, double Y)> result)
        {
            try
            {
                DBObjectCollection firstExplode = new();
                entity.Explode(firstExplode);

                foreach (Autodesk.AutoCAD.DatabaseServices.DBObject obj1 in firstExplode)
                {
                    try
                    {
                        if (obj1 is MText mtext)
                        {
                            string text = CleanMTextFormatting(mtext.Contents ?? "");
                            if (!string.IsNullOrWhiteSpace(text))
                                result.Add((text.Trim(), mtext.Location.X, mtext.Location.Y));
                        }
                        else if (obj1 is DBText dbtext)
                        {
                            string text = dbtext.TextString ?? "";
                            if (!string.IsNullOrWhiteSpace(text))
                                result.Add((text.Trim(), dbtext.Position.X, dbtext.Position.Y));
                        }
                        else if (obj1 is Autodesk.AutoCAD.DatabaseServices.Entity subEntity)
                        {
                            // Explode lần 2 cho sub-entities (BlockReference, etc.)
                            DBObjectCollection secondExplode = new();
                            subEntity.Explode(secondExplode);

                            foreach (Autodesk.AutoCAD.DatabaseServices.DBObject obj2 in secondExplode)
                            {
                                try
                                {
                                    if (obj2 is MText mtext2)
                                    {
                                        string text = CleanMTextFormatting(mtext2.Contents ?? "");
                                        if (!string.IsNullOrWhiteSpace(text))
                                            result.Add((text.Trim(), mtext2.Location.X, mtext2.Location.Y));
                                    }
                                    else if (obj2 is DBText dbtext2)
                                    {
                                        string text = dbtext2.TextString ?? "";
                                        if (!string.IsNullOrWhiteSpace(text))
                                            result.Add((text.Trim(), dbtext2.Position.X, dbtext2.Position.Y));
                                    }
                                }
                                catch { }
                            }

                            foreach (var obj in secondExplode)
                                ((Autodesk.AutoCAD.DatabaseServices.DBObject)obj).Dispose();
                        }
                    }
                    catch { }
                }

                foreach (var obj in firstExplode)
                    ((Autodesk.AutoCAD.DatabaseServices.DBObject)obj).Dispose();
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n     ⚠️  Lỗi flatten explode: {ex.Message}");
            }
        }

        /// <summary>
        /// Thử parse text vàng: hỗ trợ các format:
        /// "Rọ đá: 21.00 m2", "Vải địa: 11.00 m", "5.23 m2"
        /// </summary>
        private static (string Name, double Value)? TryParseYellowText(string text)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text)) return null;

                text = text.Trim();

                // Pattern 1: "Tên: giá_trị đơn_vị" hoặc "Tên = giá_trị đơn_vị"
                // Ví dụ: "Rọ đá: 21.00 m2", "Vải địa: 11.00 m", "Đào đất = 5.23 m²"
                var match = Regex.Match(text, @"^(.+?)\s*[:=]\s*([\d.,]+)\s*(m[²2]|m)?\s*$", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    string name = match.Groups[1].Value.Trim();
                    string numStr = match.Groups[2].Value.Replace(",", ".");
                    if (double.TryParse(numStr,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double val) && val >= 0)
                    {
                        return (name, val);
                    }
                }

                // Pattern 2: "giá_trị đơn_vị" (chỉ số, không có tên)
                // Ví dụ: "21.00 m2", "11.00 m"
                var match2 = Regex.Match(text, @"^([\d.,]+)\s*(m[²2]|m)?\s*$", RegexOptions.IgnoreCase);
                if (match2.Success)
                {
                    string numStr = match2.Groups[1].Value.Replace(",", ".");
                    if (double.TryParse(numStr,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double val) && val >= 0)
                    {
                        return ("Text vàng", val);
                    }
                }

                // Pattern 3: "Tên vật_liệu\ngiá_trị m2" (multiline)
                var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length >= 2)
                {
                    string namePart = lines[0].Trim();
                    var (numVal, _) = ExtractNumberAndUnit(lines[lines.Length - 1].Trim());
                    if (numVal > 0)
                    {
                        return (namePart, numVal);
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Trích xuất số và đơn vị từ text (ví dụ: "5.230 m²" → (5.23, "m²"))
        /// </summary>
        private static (double Value, string Unit) ExtractNumberAndUnit(string text)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text)) return (0, "");

                // Tìm số trong text
                var match = Regex.Match(text, @"([\d.,]+)\s*(m[²2]?|m|sq\.m)?", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    string numStr = match.Groups[1].Value.Replace(",", ".");
                    string unit = match.Groups[2].Value;

                    // Normalize unit
                    if (unit.Contains("2") || unit.Contains("²")) unit = "m²";
                    else if (unit.Equals("m", StringComparison.OrdinalIgnoreCase)) unit = "m";
                    else unit = "m²"; // Default

                    if (double.TryParse(numStr,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out double val))
                    {
                        return (val, unit);
                    }
                }

                return (0, "");
            }
            catch
            {
                return (0, "");
            }
        }

        /// <summary>
        /// Clean MText formatting codes
        /// </summary>
        private static string CleanMTextFormatting(string content)
        {
            if (string.IsNullOrEmpty(content)) return "";

            // Loại bỏ formatting codes MText phổ biến
            string cleaned = content;
            // Remove font/style codes: {\fArial|...; text}
            cleaned = Regex.Replace(cleaned, @"\{\\[fFpPqQwWaAcChHoOlLkK][^;]*;", "");
            // Remove \P (paragraph break) → space
            cleaned = cleaned.Replace("\\P", " ");
            // Remove closing braces
            cleaned = cleaned.Replace("}", "");
            // Remove other formatting: \S, \U+, etc.
            cleaned = Regex.Replace(cleaned, @"\\[A-Za-z][^;\\]*[;]?", "");
            // Remove Unicode: \U+XXXX
            cleaned = Regex.Replace(cleaned, @"\\U\+[0-9A-Fa-f]{4}", "");
            // Clean multiple spaces
            cleaned = Regex.Replace(cleaned, @"\s+", " ");

            return cleaned.Trim();
        }

        #endregion

        /// <summary>
        /// Lấy diện tích từ MaterialSection bằng Double Explode → Block → Hatch → Hatch.Area
        /// Nếu thất bại và useShoelace=true thì dùng công thức Shoelace với SectionPoints
        /// </summary>
        private static double GetMaterialSectionArea(MaterialSection materialSection, bool useShoelace)
        {
            try
            {
                // Double Explode: MaterialSection → Block → Hatch
                DBObjectCollection firstExplode = new DBObjectCollection();
                materialSection.Explode(firstExplode);

                foreach (Autodesk.AutoCAD.DatabaseServices.DBObject obj1 in firstExplode)
                {
                    try
                    {
                        // Nếu là BlockReference, explode tiếp để lấy Hatch
                        if (obj1 is BlockReference blockRef)
                        {
                            DBObjectCollection secondExplode = new DBObjectCollection();
                            blockRef.Explode(secondExplode);

                            foreach (Autodesk.AutoCAD.DatabaseServices.DBObject obj2 in secondExplode)
                            {
                                try
                                {
                                    if (obj2 is Hatch hatch)
                                    {
                                        double hatchArea = hatch.Area;
                                        if (hatchArea > 0.001)
                                        {
                                            // Dispose tất cả objects
                                            foreach (var remaining in secondExplode)
                                                ((Autodesk.AutoCAD.DatabaseServices.DBObject)remaining).Dispose();
                                            foreach (var remaining in firstExplode)
                                                ((Autodesk.AutoCAD.DatabaseServices.DBObject)remaining).Dispose();

                                            return hatchArea;
                                        }
                                    }
                                }
                                catch { }
                            }

                            // Dispose secondExplode nếu không tìm thấy Hatch
                            foreach (var obj in secondExplode)
                                ((Autodesk.AutoCAD.DatabaseServices.DBObject)obj).Dispose();
                        }
                        // Nếu trực tiếp là Hatch
                        else if (obj1 is Hatch hatchDirect)
                        {
                            double hatchArea = hatchDirect.Area;
                            if (hatchArea > 0.001)
                            {
                                foreach (var remaining in firstExplode)
                                    ((Autodesk.AutoCAD.DatabaseServices.DBObject)remaining).Dispose();
                                return hatchArea;
                            }
                        }
                    }
                    catch { }
                }

                // Dispose firstExplode nếu không tìm thấy
                foreach (var obj in firstExplode)
                    ((Autodesk.AutoCAD.DatabaseServices.DBObject)obj).Dispose();

                if (useShoelace)
                {
                    return CalculateShoelaceArea(materialSection);
                }

                return 0.0;
            }
            catch
            {
                return 0.0;
            }

        }

        private static double CalculateShoelaceArea(MaterialSection materialSection)
        {
            try
            {
                // Thử lấy SectionPoints
                // Note: Cần kiểm tra kỹ xem SectionPoints có phải là Offset/Elevation không.
                // Đối với SectionView, các exploded entities có tọa độ bản vẽ. 
                // Đối với SectionPoints của MaterialSection, thường là Offset/Elevation.

                var points = materialSection.SectionPoints;
                if (points == null || points.Count < 3) return 0.0;

                double area = 0.0;
                int n = points.Count;

                for (int i = 0; i < n; i++)
                {
                    var p1 = points[i].Location;
                    var p2 = points[(i + 1) % n].Location;

                    // Giả sử Location.X là Offset và Location.Y là Elevation
                    area += (p1.X * p2.Y) - (p2.X * p1.Y);
                }

                return Math.Abs(area) / 2.0;
            }
            catch
            {
                return 0.0;
            }
        }

        private static string FormatStation(double station)
        {
            int km = (int)(station / 1000);
            double meters = station % 1000;
            return $"Km{km}+{meters:F3}";
        }

        private static PivotTableData CreatePivotTableData(List<MaterialVolumeInfo> materialInfoList, string alignmentName = "", string sampleLineGroupName = "", int sampleLineGroupCount = 0, List<string>? qtoOrder = null, bool useDefaultSorting = false)
        {
            var pivotData = new PivotTableData();

            var allMaterialTypes = materialInfoList
                .Select(x => x.MaterialName)
                .Distinct()
                .ToList();

            // Build sourceType map: material name -> SourceType (1 = Dạng 1, 2 = Dạng 2)
            var sourceTypeMap = new Dictionary<string, int>();
            foreach (var item in materialInfoList)
            {
                if (!sourceTypeMap.ContainsKey(item.MaterialName) || item.SourceType < sourceTypeMap[item.MaterialName])
                {
                    sourceTypeMap[item.MaterialName] = item.SourceType;
                }
            }

            // Sử dụng qtoOrder được truyền vào (của riêng sheet này), nếu null thì fallback về static (để an toàn)
            var orderToUse = qtoOrder ?? _currentQTOMaterialOrder;

            var (orderedMaterials, decimalPlaces, additionalValues) = GetUserOrderedMaterialsAndDecimalPlaces(allMaterialTypes, alignmentName, sampleLineGroupName, sampleLineGroupCount, orderToUse, useDefaultSorting, sourceTypeMap);
            pivotData.MaterialTypes = orderedMaterials;
            pivotData.DecimalPlaces = decimalPlaces;
            pivotData.MaterialAdditionalValues = additionalValues;

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
            }

            return pivotData;
        }

        private static (List<string> materials, int decimalPlaces, Dictionary<string, double> additionalValues) GetUserOrderedMaterialsAndDecimalPlaces(List<string> materialTypes, string alignmentName = "", string sampleLineGroupName = "", int sampleLineGroupCount = 0, List<string>? qtoOrder = null, bool useDefaultSorting = false, Dictionary<string, int>? sourceTypeMap = null)
        {
            try
            {
                var defaultOrderedMaterials = SortMaterialsByPriority(materialTypes, qtoOrder, sourceTypeMap);

                // Nếu người dùng chọn dùng default sorting, bỏ qua form
                if (useDefaultSorting)
                {
                    A.Ed.WriteMessage("\n  - Sử dụng tùy chọn Mặc định: Bỏ qua sắp xếp thủ công.");
                    var defaultAdditionalValues = new Dictionary<string, double>();
                    foreach (var material in defaultOrderedMaterials)
                    {
                        defaultAdditionalValues[material] = 0.0;
                    }
                    return (defaultOrderedMaterials, 2, defaultAdditionalValues);
                }

                A.Ed.WriteMessage($"\n  - Tìm thấy {materialTypes.Count} loại vật liệu. Hiển thị form sắp xếp...");

                // Tạo form với thông tin alignment, SampleLineGroup và số lượng SLG
                MaterialOrderForm orderForm;
                if (!string.IsNullOrEmpty(alignmentName) || !string.IsNullOrEmpty(sampleLineGroupName))
                {
                    orderForm = new MaterialOrderForm(defaultOrderedMaterials, alignmentName, sampleLineGroupName, sampleLineGroupCount);
                }
                else
                {
                    orderForm = new MaterialOrderForm(defaultOrderedMaterials);
                }

                Autodesk.AutoCAD.ApplicationServices.Application.ShowModalDialog(orderForm);

                if (orderForm.DialogResult_OK && orderForm.OrderedMaterialTypes.Count > 0)
                {
                    A.Ed.WriteMessage($"\n  - Người dùng đã sắp xếp thứ tự: {string.Join(", ", orderForm.OrderedMaterialTypes)}");
                    A.Ed.WriteMessage($"\n  - Số chữ số thập phân: {orderForm.DecimalPlaces}");

                    return (orderForm.OrderedMaterialTypes, orderForm.DecimalPlaces, orderForm.MaterialAdditionalValues);
                }
                else
                {
                    A.Ed.WriteMessage("\n  - Sử dụng thứ tự mặc định.");
                    var defaultAdditionalValues = new Dictionary<string, double>();
                    foreach (var material in defaultOrderedMaterials)
                    {
                        defaultAdditionalValues[material] = 0.0;
                    }
                    return (defaultOrderedMaterials, 2, defaultAdditionalValues);
                }
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n⚠️  Lỗi hiển thị form sắp xếp: {ex.Message}");
                var defaultOrderedMaterials = SortMaterialsByPriority(materialTypes, _currentQTOMaterialOrder, sourceTypeMap);
                var defaultAdditionalValues = new Dictionary<string, double>();
                foreach (var material in defaultOrderedMaterials)
                {
                    defaultAdditionalValues[material] = 0.0;
                }
                return (defaultOrderedMaterials, 2, defaultAdditionalValues);
            }
        }

        /// <summary>
        /// Sắp xếp materials: Dạng 1 trước, Dạng 2 sau, giữ nguyên thứ tự từ trên xuống dưới (Y position)
        /// </summary>
        private static List<string> SortMaterialsByPriority(List<string> materialTypes, List<string>? materialOrderFromQTO = null, Dictionary<string, int>? sourceTypeMap = null)
        {
            // Sắp xếp theo: SourceType (Dạng 1 trước, Dạng 2 sau) → giữ nguyên thứ tự gốc (visual Y position)
            // materialTypes đã có thứ tự đúng từ trên xuống dưới khi thu thập từ SectionView
            return materialTypes
                .OrderBy(material =>
                {
                    // Dạng 1 (QTO Table) trước, Dạng 2 (Text vàng) sau
                    if (sourceTypeMap != null && sourceTypeMap.ContainsKey(material))
                        return sourceTypeMap[material];
                    return 1; // Mặc định là Dạng 1
                })
                .ThenBy(material => materialTypes.IndexOf(material)) // Giữ thứ tự gốc từ trên xuống dưới
                .ToList();
        }

        /// <summary>
        /// Lấy thứ tự materials từ QTOMaterialList của SampleLineGroup
        /// </summary>
        private static List<string> GetMaterialOrderFromQTO(SampleLineGroup sampleLineGroup)
        {
            List<string> materialOrder = new();

            try
            {
                var materialLists = sampleLineGroup.MaterialLists;
                if (materialLists == null || materialLists.Count == 0)
                    return materialOrder;

                // Lấy Material List đầu tiên (thường là list chính)
                foreach (Autodesk.Civil.DatabaseServices.QTOMaterialList qtoList in materialLists)
                {
                    try
                    {
                        // Duyệt qua từng material trong list theo thứ tự
                        for (int i = 0; i < qtoList.Count; i++)
                        {
                            var material = qtoList[i];
                            if (material != null && !string.IsNullOrEmpty(material.Name))
                            {
                                materialOrder.Add(material.Name);
                            }
                        }

                        // Chỉ lấy từ list đầu tiên
                        if (materialOrder.Count > 0)
                            break;
                    }
                    catch { }
                }
            }
            catch { }

            return materialOrder;
        }

        // Helper classes
        private class SheetData
        {
            public string SheetName { get; set; } = "";
            public string AlignmentName { get; set; } = "";
            public string SampleLineGroupName { get; set; } = "";
            public int SampleLineGroupCount { get; set; } = 0;
            public List<MaterialVolumeInfo> MaterialInfoList { get; set; } = new();
            public List<string> QTOMaterialOrder { get; set; } = new();
        }

        private class MaterialVolumeInfo
        {
            public string StakeName { get; set; } = "";
            public string Station { get; set; } = "";
            public double StationValue { get; set; }
            public string MaterialName { get; set; } = "";
            public double Area { get; set; }
            public string SourceName { get; set; } = "";
            public int SourceType { get; set; } = 1; // 1 = Dạng 1 (QTO Table), 2 = Dạng 2 (Text vàng)
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
            public double SpacingPrev { get; set; }
            public Dictionary<string, double> MaterialAreas { get; set; } = new();
        }
    }
}

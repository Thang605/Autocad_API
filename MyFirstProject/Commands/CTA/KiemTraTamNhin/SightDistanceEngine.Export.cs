using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using ClosedXML.Excel;
using MyFirstProject.Extensions;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using CivSurface = Autodesk.Civil.DatabaseServices.TinSurface;

namespace Civil3DCsharp
{
    public static partial class SightDistanceEngine
    {
        /// <summary>
        /// Xuất báo cáo kiểm tra tầm nhìn ra file Excel định dạng chuẩn
        /// </summary>
        public static void ExportToExcel(List<SightCheckItem> items, string filePath, string alignName, string standardName, int speed, string sightTypeName)
        {
            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("Báo cáo Tầm Nhìn");

                // Tiêu đề báo cáo
                ws.Cell(1, 1).Value = "BÁO CÁO KIỂM TRA & ĐÁNH GIÁ TẦM NHÌN XE CHẠY (SIGHT DISTANCE ANALYSIS)";
                ws.Range(1, 1, 1, 10).Merge().Style
                    .Font.SetBold(true)
                    .Font.SetFontSize(15)
                    .Font.SetFontColor(XLColor.Navy)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

                // Thông tin chung
                ws.Cell(3, 1).Value = "Tuyến đường:";
                ws.Cell(3, 2).Value = alignName;
                ws.Cell(3, 4).Value = "Tiêu chuẩn áp dụng:";
                ws.Cell(3, 5).Value = standardName;

                ws.Cell(4, 1).Value = "Vận tốc thiết kế:";
                ws.Cell(4, 2).Value = $"{speed} km/h";
                ws.Cell(4, 4).Value = "Loại tầm nhìn:";
                ws.Cell(4, 5).Value = sightTypeName;

                ws.Cell(5, 1).Value = "Ngày lập:";
                ws.Cell(5, 2).Value = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
                ws.Cell(5, 4).Value = "Tổng số vị trí kiểm tra:";
                ws.Cell(5, 5).Value = items.Count;

                int passCount = items.Count(i => i.Status == SightCheckStatus.Pass);
                int warnCount = items.Count(i => i.Status == SightCheckStatus.Warning);
                int failCount = items.Count(i => i.Status == SightCheckStatus.Fail);

                ws.Cell(6, 1).Value = "Kết quả tổng hợp:";
                ws.Cell(6, 2).Value = $"Đạt: {passCount} | Cảnh báo: {warnCount} | Không đạt: {failCount}";
                ws.Range(3, 1, 6, 5).Style.Font.SetFontSize(10);

                // Header bảng dữ liệu
                string[] headers = {
                    "STT", "Lý trình", "Tọa độ X", "Tọa độ Y", "Độ dốc dọc i (%)",
                    "Bán kính R (m)", "Tầm nhìn yc S_yc (m)", "Tầm nhìn thực S_tt (m)",
                    "Dạt bụng cong M (m)", "Đánh giá", "Chi tiết / Kiến nghị"
                };

                int rowStart = 8;
                for (int c = 0; c < headers.Length; c++)
                {
                    var cell = ws.Cell(rowStart, c + 1);
                    cell.Value = headers[c];
                    cell.Style
                        .Font.SetBold(true)
                        .Font.SetFontColor(XLColor.White)
                        .Fill.SetBackgroundColor(XLColor.SteelBlue)
                        .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                        .Alignment.SetVertical(XLAlignmentVerticalValues.Center);
                }

                // Điền dữ liệu
                int currentRow = rowStart + 1;
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    ws.Cell(currentRow, 1).Value = i + 1;
                    ws.Cell(currentRow, 2).Value = item.StationFormatted;
                    ws.Cell(currentRow, 3).Value = Math.Round(item.X, 3);
                    ws.Cell(currentRow, 4).Value = Math.Round(item.Y, 3);
                    ws.Cell(currentRow, 5).Value = item.GradePercent;

                    string rStr = item.HorizontalRadius > 0 ? $"R_nam={item.HorizontalRadius:F0}" : (item.VerticalRadius > 0 ? $"R_dung={item.VerticalRadius:F0}" : "-");
                    ws.Cell(currentRow, 6).Value = rStr;
                    ws.Cell(currentRow, 7).Value = item.RequiredSight;
                    ws.Cell(currentRow, 8).Value = item.ActualSight;
                    ws.Cell(currentRow, 9).Value = item.ClearanceWidthM > 0 ? item.ClearanceWidthM : 0;

                    var statusCell = ws.Cell(currentRow, 10);
                    if (item.Status == SightCheckStatus.Pass)
                    {
                        statusCell.Value = "Đạt";
                        statusCell.Style.Font.SetFontColor(XLColor.DarkGreen).Font.SetBold(true);
                    }
                    else if (item.Status == SightCheckStatus.Warning)
                    {
                        statusCell.Value = "Cảnh báo";
                        statusCell.Style.Font.SetFontColor(XLColor.DarkOrange).Font.SetBold(true);
                    }
                    else
                    {
                        statusCell.Value = "Không đạt";
                        statusCell.Style.Font.SetFontColor(XLColor.Crimson).Font.SetBold(true);
                    }

                    ws.Cell(currentRow, 11).Value = item.Detail;

                    currentRow++;
                }

                // Kẻ viền bảng & căn chỉnh cột
                var dataRange = ws.Range(rowStart, 1, currentRow - 1, headers.Length);
                dataRange.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
                dataRange.Style.Border.SetInsideBorder(XLBorderStyleValues.Thin);
                ws.Columns().AdjustToContents();

                workbook.SaveAs(filePath);
            }
        }
    }
}

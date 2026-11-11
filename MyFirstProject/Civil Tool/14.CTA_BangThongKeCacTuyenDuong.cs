// (C) Copyright 2015 by  
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
[assembly: CommandClass(typeof(MyFirstProject.CTA_BangThongKeCacTuyenDuong_Commands))]

namespace MyFirstProject
{
    public class CTA_BangThongKeCacTuyenDuong_Commands
    {
        [CommandMethod("CTA_Bang_Thong_Ke_Cac_Tuyen_Duong")]

        public static void CTABangThongKeCacTuyenDuong()
        {
            // start transaction
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();

                A.Ed.WriteMessage("\nTạo bảng thống kê các tuyến đường...");

                // Step 1: Select multiple alignments
                A.Ed.WriteMessage("\nChọn các tuyến đường cần xuất thông tin:");

                PromptSelectionOptions pso = new()
                {
                    MessageForAdding = "\nChọn các alignment (tuyến đường): ",
                    AllowDuplicates = false
                };

                // Create selection filter for alignments only
                TypedValue[] filterList =
                [
                    new((int)DxfCode.Start, "AECC_ALIGNMENT")
                ];
                SelectionFilter filter = new(filterList);

                PromptSelectionResult psr = A.Ed.GetSelection(pso, filter);
                if (psr.Status != PromptStatus.OK)
                {
                    A.Ed.WriteMessage("\nĐã hủy lệnh hoặc không chọn được alignment.");
                    return;
                }

                ObjectId[] selectedIds = psr.Value.GetObjectIds();
                if (selectedIds.Length == 0)
                {
                    A.Ed.WriteMessage("\nKhông có alignment nào được chọn.");
                    return;
                }

                A.Ed.WriteMessage($"\nĐã chọn {selectedIds.Length} tuyến đường.");

                // Step 2: Collect alignment information
                List<AlignmentInfo> alignmentData = [];

                foreach (ObjectId alignmentId in selectedIds)
                {
                    try
                    {
                        Alignment? alignment = tr.GetObject(alignmentId, OpenMode.ForRead) as Alignment;
                        if (alignment != null)
                        {
                            // Chỉ thống kê alignment loại Centerline
                            if (alignment.AlignmentType != AlignmentType.Centerline)
                            {
                                A.Ed.WriteMessage($"\nBỏ qua '{alignment.Name}' - Loại: {alignment.AlignmentType}");
                                continue;
                            }

                            AlignmentInfo info = new()
                            {
                                TenDuong = alignment.Name ?? "Không có tên",
                                MoTa = alignment.Description ?? "Không có mô tả",
                                ChieuDaiTuyen = Math.Round(alignment.Length, 3),
                                CocDau = Math.Round(alignment.StartingStation, 3),
                                CocCuoi = Math.Round(alignment.EndingStation, 3)
                            };

                            // Lấy tên Style
                            try
                            {
                                if (alignment.StyleId != ObjectId.Null)
                                {
                                    AlignmentStyle? style = tr.GetObject(alignment.StyleId, OpenMode.ForRead) as AlignmentStyle;
                                    info.TenStyle = style?.Name ?? "Không có";
                                }
                            }
                            catch { info.TenStyle = "Không xác định"; }

                            // Lấy tên Site
                            try
                            {
                                if (alignment.SiteId != ObjectId.Null)
                                {
                                    Site? site = tr.GetObject(alignment.SiteId, OpenMode.ForRead) as Site;
                                    info.TenSite = site?.Name ?? "Không thuộc Site";
                                }
                                else
                                {
                                    info.TenSite = "Không thuộc Site";
                                }
                            }
                            catch { info.TenSite = "Không xác định"; }

                            // Phân tích các entities để lấy thông tin hình học
                            int tangentCount = 0;
                            int curveCount = 0;
                            double minRadius = double.MaxValue;
                            double maxRadius = 0;

                            AlignmentEntityCollection entities = alignment.Entities;
                            foreach (AlignmentEntity entity in entities)
                            {
                                switch (entity.EntityType)
                                {
                                    case AlignmentEntityType.Line:
                                        tangentCount++;
                                        break;
                                    case AlignmentEntityType.Arc:
                                        curveCount++;
                                        if (entity is AlignmentArc arc)
                                        {
                                            if (arc.Radius < minRadius) minRadius = arc.Radius;
                                            if (arc.Radius > maxRadius) maxRadius = arc.Radius;
                                        }
                                        break;
                                    case AlignmentEntityType.Spiral:
                                    case AlignmentEntityType.SpiralCurve:
                                    case AlignmentEntityType.SpiralCurveSpiral:
                                    case AlignmentEntityType.SpiralLine:
                                    case AlignmentEntityType.SpiralLineSpiral:
                                    case AlignmentEntityType.SpiralSpiral:
                                    case AlignmentEntityType.SpiralSpiralCurveSpiralSpiral:
                                    case AlignmentEntityType.MultipleSegments:
                                        curveCount++;
                                        // Cố gắng lấy Radius từ các subentities nếu có
                                        if (entity is AlignmentSCS scs)
                                        {
                                            if (scs.Arc.Radius < minRadius) minRadius = scs.Arc.Radius;
                                            if (scs.Arc.Radius > maxRadius) maxRadius = scs.Arc.Radius;
                                        }
                                        else if (entity is AlignmentSCSCS scscs)
                                        {
                                            if (scscs.Arc1.Radius < minRadius) minRadius = scscs.Arc1.Radius;
                                            if (scscs.Arc1.Radius > maxRadius) maxRadius = scscs.Arc1.Radius;
                                            if (scscs.Arc2.Radius < minRadius) minRadius = scscs.Arc2.Radius;
                                            if (scscs.Arc2.Radius > maxRadius) maxRadius = scscs.Arc2.Radius;
                                        }
                                        break;
                                }
                            }

                            info.SoDoanThang = tangentCount;
                            info.SoDuongCong = curveCount;
                            info.BanKinhMin = (minRadius == double.MaxValue) ? 0 : Math.Round(minRadius, 2);
                            info.BanKinhMax = Math.Round(maxRadius, 2);

                            alignmentData.Add(info);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        A.Ed.WriteMessage($"\nLỗi đọc alignment: {ex.Message}");
                        continue;
                    }
                }

                if (alignmentData.Count == 0)
                {
                    A.Ed.WriteMessage("\nKhông có dữ liệu alignment hợp lệ để tạo bảng.");
                    return;
                }

                // Step 2.5: Sort by road name (alphabetical order) and assign sequential numbers
                alignmentData = [.. alignmentData.OrderBy(x => x.TenDuong)];

                // Assign sequential numbers after sorting
                for (int i = 0; i < alignmentData.Count; i++)
                {
                    alignmentData[i].SoThuTu = i + 1;
                    A.Ed.WriteMessage($"\nTuyến {alignmentData[i].SoThuTu}: {alignmentData[i].TenDuong} - Dài: {alignmentData[i].ChieuDaiTuyen:F3}m");
                }

                // Step 3: Create table
                A.Ed.WriteMessage("\nChọn vị trí đặt bảng thống kê:");
                PromptPointResult ppr = A.Ed.GetPoint("\nChọn điểm đặt bảng: ");
                if (ppr.Status != PromptStatus.OK)
                {
                    A.Ed.WriteMessage("\nĐã hủy lệnh.");
                    return;
                }

                Point3d insertionPoint = ppr.Value;

                // Create table with data
#pragma warning disable CS0612 // Type or member is obsolete
                CreateAlignmentTable(alignmentData, insertionPoint, tr);
#pragma warning restore CS0612 // Type or member is obsolete

                A.Ed.WriteMessage($"\nĐã tạo thành công bảng thống kê {alignmentData.Count} tuyến đường.");

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

        [Obsolete]
        private static void CreateAlignmentTable(List<AlignmentInfo> alignmentData, Point3d insertionPoint, Transaction tr)
        {
            try
            {
                // Get current database
                Database db = A.Db;

                // Get ModelSpace for writing
                BlockTable? bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                if (bt == null) return;

                BlockTableRecord? btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                if (btr == null) return;

                // Create table - expanded with more columns
                int numRows = alignmentData.Count + 2; // Data rows + header + title
                int numCols = 10; // STT, Tên đường, Mô tả, Cọc đầu, Cọc cuối, Chiều dài, Đoạn thẳng, Đường cong, R min, R max

                ATable table = new();
                table.SetSize(numRows, numCols);
                table.Position = insertionPoint;

                // Set table style properties
                table.TableStyle = db.Tablestyle; // Use current table style

                // Set column widths
                table.SetColumnWidth(0, 12.0);  // STT
                table.SetColumnWidth(1, 50.0);  // Tên đường  
                table.SetColumnWidth(2, 65.0);  // Mô tả
                table.SetColumnWidth(3, 25.0);  // Cọc đầu
                table.SetColumnWidth(4, 25.0);  // Cọc cuối
                table.SetColumnWidth(5, 25.0);  // Chiều dài
                table.SetColumnWidth(6, 18.0);  // Số đoạn thẳng
                table.SetColumnWidth(7, 18.0);  // Số đường cong
                table.SetColumnWidth(8, 25.0);  // Bán kính min
                table.SetColumnWidth(9, 25.0);  // Bán kính max

                // Set row heights
                for (int i = 0; i < numRows; i++)
                {
                    table.SetRowHeight(i, 8.0);
                }

                // Title row (merge all columns)
                table.MergeCells(CellRange.Create(table, 0, 0, 0, numCols - 1));
                table.Cells[0, 0].TextString = "BẢNG THỐNG KÊ CÁC TUYẾN ĐƯỜNG";
                table.Cells[0, 0].Alignment = CellAlignment.MiddleCenter;
                table.Cells[0, 0].TextHeight = 6.0;

                // Header row
                string[] headers = ["STT", "TÊN ĐƯỜNG", "MÔ TẢ", "CỌC ĐẦU (m)", "CỌC CUỐI (m)", "CHIỀU DÀI (m)", "SỐ ĐOẠN THẲNG", "SỐ ĐƯỜNG CONG", "R MIN (m)", "R MAX (m)"];
                for (int col = 0; col < numCols; col++)
                {
                    table.Cells[1, col].TextString = headers[col];
                    table.Cells[1, col].Alignment = CellAlignment.MiddleCenter;
                    table.Cells[1, col].TextHeight = 3.5;
                }

                // Data rows
                for (int i = 0; i < alignmentData.Count; i++)
                {
                    int row = i + 2; // Skip title and header rows
                    AlignmentInfo info = alignmentData[i];

                    // STT
                    table.Cells[row, 0].TextString = info.SoThuTu.ToString();
                    table.Cells[row, 0].Alignment = CellAlignment.MiddleCenter;
                    table.Cells[row, 0].TextHeight = 3.5;

                    // Tên đường
                    table.Cells[row, 1].TextString = info.TenDuong;
                    table.Cells[row, 1].Alignment = CellAlignment.MiddleLeft;
                    table.Cells[row, 1].TextHeight = 3.5;

                    // Mô tả
                    table.Cells[row, 2].TextString = info.MoTa;
                    table.Cells[row, 2].Alignment = CellAlignment.MiddleLeft;
                    table.Cells[row, 2].TextHeight = 3.5;

                    // Cọc đầu
                    table.Cells[row, 3].TextString = info.CocDau.ToString("F3");
                    table.Cells[row, 3].Alignment = CellAlignment.MiddleCenter;
                    table.Cells[row, 3].TextHeight = 3.5;

                    // Cọc cuối
                    table.Cells[row, 4].TextString = info.CocCuoi.ToString("F3");
                    table.Cells[row, 4].Alignment = CellAlignment.MiddleCenter;
                    table.Cells[row, 4].TextHeight = 3.5;

                    // Chiều dài
                    table.Cells[row, 5].TextString = info.ChieuDaiTuyen.ToString("F3");
                    table.Cells[row, 5].Alignment = CellAlignment.MiddleCenter;
                    table.Cells[row, 5].TextHeight = 3.5;

                    // Số đoạn thẳng
                    table.Cells[row, 6].TextString = info.SoDoanThang.ToString();
                    table.Cells[row, 6].Alignment = CellAlignment.MiddleCenter;
                    table.Cells[row, 6].TextHeight = 3.5;

                    // Số đường cong
                    table.Cells[row, 7].TextString = info.SoDuongCong.ToString();
                    table.Cells[row, 7].Alignment = CellAlignment.MiddleCenter;
                    table.Cells[row, 7].TextHeight = 3.5;

                    // Bán kính min
                    string rMinStr = info.BanKinhMin > 0 ? info.BanKinhMin.ToString("F2") : "-";
                    table.Cells[row, 8].TextString = rMinStr;
                    table.Cells[row, 8].Alignment = CellAlignment.MiddleCenter;
                    table.Cells[row, 8].TextHeight = 3.5;

                    // Bán kính max
                    string rMaxStr = info.BanKinhMax > 0 ? info.BanKinhMax.ToString("F2") : "-";
                    table.Cells[row, 9].TextString = rMaxStr;
                    table.Cells[row, 9].Alignment = CellAlignment.MiddleCenter;
                    table.Cells[row, 9].TextHeight = 3.5;
                }

                // Add table to database
                btr.AppendEntity(table);
                tr.AddNewlyCreatedDBObject(table, true);

                A.Ed.WriteMessage($"\nĐã tạo bảng tại tọa độ: X={insertionPoint.X:F3}, Y={insertionPoint.Y:F3}");
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi tạo bảng: {ex.Message}");
            }
        }

        // Helper class to store alignment information
        private class AlignmentInfo
        {
            public int SoThuTu { get; set; }
            public string TenDuong { get; set; } = "";
            public string MoTa { get; set; } = "";
            public double ChieuDaiTuyen { get; set; }

            // Thông tin bổ sung từ Alignment
            public double CocDau { get; set; }        // StartStation
            public double CocCuoi { get; set; }       // EndStation
            public string TenStyle { get; set; } = "";     // Style Name
            public string TenSite { get; set; } = "";      // Site Name
            public int SoDoanThang { get; set; }      // Number of tangent segments
            public int SoDuongCong { get; set; }      // Number of curve segments
            public double BanKinhMin { get; set; }    // Minimum radius
            public double BanKinhMax { get; set; }    // Maximum radius
        }
    }
}

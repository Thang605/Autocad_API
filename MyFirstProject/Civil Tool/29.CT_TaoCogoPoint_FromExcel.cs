// (C) Copyright 2024 by T27 Co.
// Lệnh tạo COGO Point từ file Excel
// File Excel cần có các cột: X, Y, Z (Elevation), Description (tùy chọn)

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;
using MyFirstProject.Extensions;
using ClosedXML.Excel;

// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3DCsharp.CT_TaoCogoPoint_FromExcel_Commands))]

namespace Civil3DCsharp
{
    public class CT_TaoCogoPoint_FromExcel_Commands
    {

        /// <summary>
        /// Lệnh chính: Tạo COGO Point từ file Excel
        /// File Excel cần có các cột: X, Y, Z, Description (tùy chọn)
        /// </summary>
        [CommandMethod("CT_Tao_Cogo_Point_Tu_Excel")]
        public static void CreateCogoPointFromExcel()
        {
            try
            {
                A.Ed.WriteMessage("\n📊 Lệnh tạo COGO Point từ file Excel...");

                // Step 1: Hiển thị Form Input
                using var form = new ImportCogoPointExcelForm();
                if (Autodesk.AutoCAD.ApplicationServices.Application.ShowModalDialog(form) != DialogResult.OK)
                {
                    A.Ed.WriteMessage("\n❌ Đã hủy lệnh.");
                    return;
                }

                string excelFilePath = form.FilePath;
                A.Ed.WriteMessage($"\n✓ Bắt đầu xử lý file: {Path.GetFileName(excelFilePath)}");

                // Step 2: Đọc dữ liệu từ file Excel dựa trên mapping của form
                A.Ed.WriteMessage("\n🎯 Đang đọc dữ liệu từ file Excel...");
                List<PointData> pointDataList = ReadPointDataFromExcelWithMapping(form);

                if (pointDataList.Count == 0)
                {
                    A.Ed.WriteMessage("\n❌ Không tìm thấy dữ liệu điểm hợp lệ.");
                    return;
                }

                A.Ed.WriteMessage($"\n✓ Đọc được {pointDataList.Count} điểm.");

                // Step 3: Tạo COGO Points
                int createdCount = 0;
                int errorCount = 0;

                using (Transaction tr = A.Db.TransactionManager.StartTransaction())
                {
                    try
                    {
                        CogoPointCollection cogoPointColl = A.Cdoc.CogoPoints;
                        ObjectIdCollection newPointIds = new ObjectIdCollection();

                        foreach (var pointData in pointDataList)
                        {
                            try
                            {
                                Point3d point3D = new(pointData.X, pointData.Y, pointData.Z);
                                ObjectId pointId = cogoPointColl.Add(point3D, pointData.Description, true);

                                if (!string.IsNullOrEmpty(pointData.Name))
                                {
                                    CogoPoint? cogoPoint = tr.GetObject(pointId, OpenMode.ForWrite) as CogoPoint;
                                    if (cogoPoint != null) cogoPoint.PointName = pointData.Name;
                                }

                                newPointIds.Add(pointId);
                                createdCount++;

                                if (createdCount % 50 == 0)
                                    A.Ed.WriteMessage($"\n  - Đã tạo {createdCount}/{pointDataList.Count} điểm...");
                            }
                            catch (System.Exception ex)
                            {
                                errorCount++;
                                A.Ed.WriteMessage($"\n  ⚠️ Lỗi tại ({pointData.X:F3}, {pointData.Y:F3}): {ex.Message}");
                            }
                        }

                        // Xử lý Point Group nếu được chọn
                        if (form.AddToPointGroup && !string.IsNullOrEmpty(form.PointGroupName) && newPointIds.Count > 0)
                        {
                            A.Ed.WriteMessage($"\n🎯 Đang tạo Point Group: {form.PointGroupName}...");
                            _ = UtilitiesC3D.CPointGroupWithDecription(form.PointGroupName, form.PointGroupName);
                        }

                        tr.Commit();
                        A.Ed.WriteMessage("\n✓ Lưu thay đổi thành công.");
                    }
                    catch (System.Exception ex)
                    {
                        A.Ed.WriteMessage($"\n❌ Lỗi trong quá trình tạo điểm: {ex.Message}");
                        tr.Abort();
                        return;
                    }
                }

                A.Ed.WriteMessage($"\n\n✅ HOÀN THÀNH: Đã tạo {createdCount} điểm.");
                if (errorCount > 0) A.Ed.WriteMessage($"\n⚠️ Số điểm lỗi: {errorCount}");
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n❌ Lỗi hệ thống: {ex.Message}");
            }
        }

        private static List<PointData> ReadPointDataFromExcelWithMapping(ImportCogoPointExcelForm form)
        {
            List<PointData> data = new List<PointData>();
            try
            {
                using var workbook = new XLWorkbook(form.FilePath);
                var worksheet = workbook.Worksheet(1);
                var rows = worksheet.RowsUsed().Skip(1); // Bỏ qua header (hàng 1)

                foreach (var row in rows)
                {
                    try
                    {
                        double x = 0, y = 0, z = 0;
                        string name = "", desc = form.DefaultDescription;

                        // Đọc X, Y (bắt buộc)
                        if (!TryGetDoubleValue(row.Cell(form.ColXIndex), out x)) continue;
                        if (!TryGetDoubleValue(row.Cell(form.ColYIndex), out y)) continue;

                        // Đọc các trường tùy chọn
                        if (form.ColZIndex > 0) TryGetDoubleValue(row.Cell(form.ColZIndex), out z);
                        if (form.ColNameIndex > 0) name = row.Cell(form.ColNameIndex).GetString().Trim();
                        if (form.ColDescIndex > 0)
                        {
                            string d = row.Cell(form.ColDescIndex).GetString().Trim();
                            if (!string.IsNullOrEmpty(d)) desc = d;
                        }

                        data.Add(new PointData { X = x, Y = y, Z = z, Name = name, Description = desc });
                    }
                    catch { continue; }
                }
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n❌ Lỗi đọc Excel: {ex.Message}");
            }
            return data;
        }


        /// <summary>
        /// Thử chuyển đổi giá trị ô thành số thực
        /// </summary>
        private static bool TryGetDoubleValue(IXLCell cell, out double value)
        {
            value = 0;

            if (cell == null || cell.IsEmpty())
                return false;

            // Thử lấy giá trị số trực tiếp
            if (cell.DataType == XLDataType.Number)
            {
                value = cell.GetDouble();
                return true;
            }

            // Thử parse từ chuỗi
            string stringValue = cell.GetString()?.Trim() ?? "";
            if (string.IsNullOrEmpty(stringValue))
                return false;

            // Thay thế dấu phẩy bằng dấu chấm (cho định dạng số Việt Nam)
            stringValue = stringValue.Replace(",", ".");

            return double.TryParse(stringValue, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// Cấu trúc dữ liệu điểm
        /// </summary>
        private class PointData
        {
            public string Name { get; set; } = "";
            public double X { get; set; }
            public double Y { get; set; }
            public double Z { get; set; }
            public string Description { get; set; } = "EG";
        }
    }
}

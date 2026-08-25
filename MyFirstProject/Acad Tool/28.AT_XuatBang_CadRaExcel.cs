// (C) Copyright 2026 by T27
// Lệnh trích xuất và xuất bảng AutoCAD & Civil 3D sang Excel (.xlsx / .csv)
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using ClosedXML.Excel;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using ATable = Autodesk.AutoCAD.DatabaseServices.Table;
using AcadDBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;

[assembly: CommandClass(typeof(Civil3DCsharp.AT_XuatBang_CadRaExcel_Commands))]

namespace Civil3DCsharp
{
    #region Dữ Liệu Bảng Đã Trích Xuất

    /// <summary>
    /// Định nghĩa vùng ô gộp
    /// </summary>
    public class TableMergeRange
    {
        public int TopRow { get; set; }
        public int LeftColumn { get; set; }
        public int BottomRow { get; set; }
        public int RightColumn { get; set; }
    }

    /// <summary>
    /// Dữ liệu của 1 ô trong bảng
    /// </summary>
    public class ExtractedCell
    {
        public int Row { get; set; }
        public int Column { get; set; }
        public string RawText { get; set; } = string.Empty;
        public string CleanText { get; set; } = string.Empty;
        public double? NumericValue { get; set; }
        public CellAlignment Alignment { get; set; } = CellAlignment.MiddleCenter;
        public bool IsHeaderOrTitle { get; set; } = false;
        public bool IsMergedMaster { get; set; } = false;
        public bool IsMergedSlave { get; set; } = false;
    }

    /// <summary>
    /// Dữ liệu toàn bộ bảng đã trích xuất hoàn chỉnh
    /// </summary>
    public class ExtractedTable
    {
        public string TableTitle { get; set; } = string.Empty;
        public string TableType { get; set; } = "AutoCAD Table";
        public string Handle { get; set; } = string.Empty;
        public string SpaceName { get; set; } = "Model";
        public int RowCount { get; set; }
        public int ColumnCount { get; set; }
        public List<ExtractedCell> Cells { get; set; } = new List<ExtractedCell>();
        public List<TableMergeRange> MergedRanges { get; set; } = new List<TableMergeRange>();
    }

    #endregion

    /// <summary>
    /// Các lệnh xuất bảng CAD ra file Excel
    /// </summary>
    public class AT_XuatBang_CadRaExcel_Commands
    {
        private static List<ObjectId> _lastSelectedTableIds = new List<ObjectId>();

        /// <summary>
        /// Lệnh chính xuất bảng CAD sang file Excel với Form giao diện
        /// </summary>
        [CommandMethod("AT_XuatBangCadRaExcel")]
        [CommandMethod("AT_XuatBangExcel")]
        [CommandMethod("XUATBANGEXCEL")]
        [CommandMethod("BANG2EXCEL")]
        [CommandMethod("TB2EXCEL")]
        public static void RunXuatBangCadRaExcel()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            Editor ed = doc.Editor;
            Database db = doc.Database;

            try
            {
                ed.WriteMessage("\n--- Khởi động lệnh Xuất Bảng AutoCAD Ra Excel (T27) ---");

                // 1. Quét sơ bộ các bảng có sẵn trong Model Space
                List<CadTableInfoItem> initialTables = new List<CadTableInfoItem>();
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    initialTables = ScanTables(db, tr, scanAllSpaces: false);
                    tr.Commit();
                }

                // 2. Khởi tạo Form giao diện
                using (XuatBangCadRaExcelForm form = new XuatBangCadRaExcelForm(initialTables))
                {
                    // Callback 1: Chọn bảng trên màn hình (Pick)
                    form.OnPickTablesOnScreen = () =>
                    {
                        List<CadTableInfoItem> pickedList = new List<CadTableInfoItem>();

                        using (var interaction = ed.StartUserInteraction(form))
                        {
                            PromptSelectionOptions pso = new PromptSelectionOptions
                            {
                                MessageForAdding = "\nChọn các Bảng (Table) trên bản vẽ (quét chọn hoặc click): ",
                                AllowDuplicates = false
                            };

                            // Lọc các đối tượng Bảng AutoCAD và Bảng Civil 3D
                            TypedValue[] filterList = new TypedValue[]
                            {
                                new TypedValue((int)DxfCode.Operator, "<OR"),
                                new TypedValue((int)DxfCode.Start, "ACAD_TABLE"),
                                new TypedValue((int)DxfCode.Start, "AECC_*_TABLE"),
                                new TypedValue((int)DxfCode.Start, "INSERT"),
                                new TypedValue((int)DxfCode.Operator, "OR>")
                            };
                            SelectionFilter filter = new SelectionFilter(filterList);

                            PromptSelectionResult psr = ed.GetSelection(pso, filter);
                            if (psr.Status == PromptStatus.OK && psr.Value != null)
                            {
                                using Transaction trPick = db.TransactionManager.StartTransaction();
                                foreach (SelectedObject so in psr.Value)
                                {
                                    if (so != null && !so.ObjectId.IsNull)
                                    {
                                        CadTableInfoItem? item = TryCreateCadTableInfoItem(so.ObjectId, trPick);
                                        if (item != null)
                                        {
                                            pickedList.Add(item);
                                        }
                                    }
                                }
                                trPick.Commit();
                            }
                        }

                        ed.WriteMessage($"\nĐã chọn được {pickedList.Count} bảng.");
                        return pickedList;
                    };

                    // Callback 2: Quét tất cả bảng trong Model Space
                    form.OnScanModelTables = () =>
                    {
                        using Transaction tr = db.TransactionManager.StartTransaction();
                        var list = ScanTables(db, tr, scanAllSpaces: false);
                        tr.Commit();
                        return list;
                    };

                    // Callback 3: Quét tất cả bảng trong toàn bộ bản vẽ (Model + Layouts)
                    form.OnScanAllDrawingTables = () =>
                    {
                        using Transaction tr = db.TransactionManager.StartTransaction();
                        var list = ScanTables(db, tr, scanAllSpaces: true);
                        tr.Commit();
                        return list;
                    };

                    // Callback 4: Thực thi xuất Excel
                    form.OnExecuteExport = (config, progressCallback) =>
                    {
                        ExecuteExportTablesToExcel(db, config, progressCallback);
                    };

                    // Hiển thị Modal Dialog
                    Application.ShowModalDialog(form);
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\nLỗi thực thi lệnh xuất bảng: {ex.Message}");
            }
        }

        #region Logic Quét và Nhận Diện Bảng

        /// <summary>
        /// Quét danh sách các bảng trong bản vẽ
        /// </summary>
        private static List<CadTableInfoItem> ScanTables(Database db, Transaction tr, bool scanAllSpaces)
        {
            List<CadTableInfoItem> results = new List<CadTableInfoItem>();

            try
            {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

                // Danh sách các BlockTableRecord cần duyệt
                List<ObjectId> btrIds = new List<ObjectId> { bt[BlockTableRecord.ModelSpace] };

                if (scanAllSpaces)
                {
                    // Lấy thêm tất cả Paper Space Layouts
                    DBDictionary layoutDict = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
                    foreach (DBDictionaryEntry entry in layoutDict)
                    {
                        Layout layout = (Layout)tr.GetObject(entry.Value, OpenMode.ForRead);
                        if (!layout.ModelType)
                        {
                            btrIds.Add(layout.BlockTableRecordId);
                        }
                    }
                }

                foreach (ObjectId btrId in btrIds)
                {
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
                    string spaceName = btr.IsLayout ? btr.Name : "Model";
                    if (spaceName.StartsWith("*Paper_Space", StringComparison.OrdinalIgnoreCase))
                    {
                        // Tìm tên Layout hiển thị
                        spaceName = "Layout";
                    }

                    foreach (ObjectId objId in btr)
                    {
                        if (objId.IsNull || objId.IsErased) continue;

                        CadTableInfoItem? item = TryCreateCadTableInfoItem(objId, tr, spaceName);
                        if (item != null)
                        {
                            results.Add(item);
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine("Lỗi quét bảng: " + ex.Message);
            }

            return results;
        }

        /// <summary>
        /// Phân tích đối tượng để tạo CadTableInfoItem nếu là Bảng
        /// </summary>
        private static CadTableInfoItem? TryCreateCadTableInfoItem(ObjectId objId, Transaction tr, string? defaultSpace = null)
        {
            try
            {
                AcadDBObject dbo = tr.GetObject(objId, OpenMode.ForRead);
                string dxfName = dbo.GetRXClass().DxfName;

                // 1. Trường hợp AutoCAD Table chuẩn (ACAD_TABLE)
                if (dbo is ATable acadTable)
                {
                    string space = defaultSpace ?? GetSpaceNameOfObject(acadTable, tr);
                    string styleName = string.Empty;
                    try { styleName = acadTable.TableStyleName; } catch { }

                    string title = string.Empty;
                    try
                    {
                        if (acadTable.Rows.Count > 0 && acadTable.Columns.Count > 0)
                        {
                            title = CleanMTextString(acadTable.Cells[0, 0]?.TextString ?? "");
                            if (string.IsNullOrWhiteSpace(title) && acadTable.Rows.Count > 1)
                            {
                                title = CleanMTextString(acadTable.Cells[1, 0]?.TextString ?? "");
                            }
                        }
                    }
                    catch { }

                    return new CadTableInfoItem
                    {
                        ObjectId = objId,
                        Handle = acadTable.Handle.ToString(),
                        TableType = "AutoCAD Table",
                        SpaceName = space,
                        RowCount = acadTable.Rows.Count,
                        ColumnCount = acadTable.Columns.Count,
                        TableStyleName = styleName,
                        Title = string.IsNullOrWhiteSpace(title) ? $"(Bảng {acadTable.Handle})" : title,
                        IsSelected = true
                    };
                }

                // 2. Trường hợp Bảng Civil 3D (AECC_PARCEL_TABLE, AECC_ALIGNMENT_TABLE, v.v.)
                if (dxfName.StartsWith("AECC_", StringComparison.OrdinalIgnoreCase) &&
                    dxfName.Contains("TABLE", StringComparison.OrdinalIgnoreCase))
                {
                    Entity ent = (Entity)dbo;
                    string space = defaultSpace ?? GetSpaceNameOfObject(ent, tr);

                    return new CadTableInfoItem
                    {
                        ObjectId = objId,
                        Handle = ent.Handle.ToString(),
                        TableType = $"Civil 3D ({dxfName})",
                        SpaceName = space,
                        RowCount = 0,
                        ColumnCount = 0,
                        TableStyleName = "Civil 3D Style",
                        Title = $"Bảng Civil 3D ({ent.Handle})",
                        IsSelected = true
                    };
                }
            }
            catch { }

            return null;
        }

        private static string GetSpaceNameOfObject(Entity ent, Transaction tr)
        {
            try
            {
                BlockTableRecord ownerBtr = (BlockTableRecord)tr.GetObject(ent.OwnerId, OpenMode.ForRead);
                return ownerBtr.Name.Equals(BlockTableRecord.ModelSpace, StringComparison.OrdinalIgnoreCase) ? "Model" : "Layout";
            }
            catch
            {
                return "Model";
            }
        }

        #endregion

        #region Logic Trích Xuất Dữ Liệu Chi Tiết Bảng

        /// <summary>
        /// Trích xuất toàn bộ dữ liệu từ danh sách bảng đã chọn
        /// </summary>
        private static ExtractedTable? ExtractTableData(ObjectId tableId, Transaction tr, TableExportExcelConfig config)
        {
            try
            {
                AcadDBObject dbo = tr.GetObject(tableId, OpenMode.ForRead);

                if (dbo is ATable acadTable)
                {
                    return ExtractFromAcadTable(acadTable, tr, config);
                }
                else if (dbo is Entity ent)
                {
                    return ExtractFromCivil3DOrExplodedTable(ent, tr, config);
                }
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"Lỗi trích xuất bảng {tableId}: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Trích xuất từ AutoCAD Table chuẩn
        /// </summary>
        private static ExtractedTable ExtractFromAcadTable(ATable table, Transaction tr, TableExportExcelConfig config)
        {
            ExtractedTable result = new ExtractedTable
            {
                TableType = "AutoCAD Table",
                Handle = table.Handle.ToString(),
                SpaceName = GetSpaceNameOfObject(table, tr),
                RowCount = table.Rows.Count,
                ColumnCount = table.Columns.Count
            };

            int rowCount = table.Rows.Count;
            int colCount = table.Columns.Count;

            // Ma trận đánh dấu ô đã thuộc merge
            bool[,] mergedVisited = new bool[rowCount, colCount];

            // 1. Quét các vùng ô gộp (Merged Ranges)
            if (config.KeepMergedCells)
            {
                for (int r = 0; r < rowCount; r++)
                {
                    for (int c = 0; c < colCount; c++)
                    {
                        if (mergedVisited[r, c]) continue;

                        try
                        {
                            var cell = table.Cells[r, c];
                            if (cell.IsMerged == true)
                            {
                                var range = cell.GetMergeRange();
                                int top = range.TopRow;
                                int left = range.LeftColumn;
                                int bottom = range.BottomRow;
                                int right = range.RightColumn;

                                if (top >= 0 && left >= 0 && bottom < rowCount && right < colCount)
                                {
                                    if (bottom > top || right > left)
                                    {
                                        result.MergedRanges.Add(new TableMergeRange
                                        {
                                            TopRow = top,
                                            LeftColumn = left,
                                            BottomRow = bottom,
                                            RightColumn = right
                                        });

                                        for (int mr = top; mr <= bottom; mr++)
                                        {
                                            for (int mc = left; mc <= right; mc++)
                                            {
                                                mergedVisited[mr, mc] = true;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }
            }

            // 2. Trích xuất nội dung từng Cell
            for (int r = 0; r < rowCount; r++)
            {
                for (int c = 0; c < colCount; c++)
                {
                    ExtractedCell cellData = new ExtractedCell
                    {
                        Row = r,
                        Column = c
                    };

                    try
                    {
                        var acadCell = table.Cells[r, c];
                        string raw = acadCell?.TextString ?? string.Empty;
                        cellData.RawText = raw;
                        cellData.CleanText = config.CleanMTextFormatting ? CleanMTextString(raw) : raw;

                        // Alignment
                        try { cellData.Alignment = acadCell?.Alignment ?? CellAlignment.MiddleCenter; } catch { }

                        // Header / Title detection
                        try
                        {
                            var cellType = acadCell?.DataType;
                            if (r == 0 || (r == 1 && rowCount > 2 && table.Rows[r].Height > table.Rows[r + 1].Height))
                            {
                                cellData.IsHeaderOrTitle = true;
                            }
                        }
                        catch { }

                        // Parse Numeric
                        if (config.AutoDetectNumericValues && !string.IsNullOrWhiteSpace(cellData.CleanText))
                        {
                            cellData.NumericValue = TryParseNumericValue(cellData.CleanText);
                        }
                    }
                    catch
                    {
                        cellData.CleanText = string.Empty;
                    }

                    result.Cells.Add(cellData);
                }
            }

            // Lấy tiêu đề bảng từ cell đầu tiên
            if (result.Cells.Count > 0)
            {
                result.TableTitle = result.Cells[0].CleanText;
            }

            return result;
        }

        /// <summary>
        /// Trích xuất từ bảng Civil 3D hoặc bảng dạng Block bằng thuật toán gom cụm tọa độ
        /// </summary>
        private static ExtractedTable ExtractFromCivil3DOrExplodedTable(Entity entity, Transaction tr, TableExportExcelConfig config)
        {
            ExtractedTable result = new ExtractedTable
            {
                TableType = entity.GetRXClass().DxfName,
                Handle = entity.Handle.ToString(),
                SpaceName = GetSpaceNameOfObject(entity, tr)
            };

            // Explode đệ quy tìm tất cả Text/MText
            var textItems = ExplodeToFindTexts(entity, 0);

            if (textItems.Count == 0)
            {
                result.RowCount = 1;
                result.ColumnCount = 1;
                result.Cells.Add(new ExtractedCell { Row = 0, Column = 0, CleanText = $"Bảng {entity.Handle} (Không có dữ liệu text)" });
                return result;
            }

            // Nhóm theo tọa độ Y (Hàng) và X (Cột) với sai số tolerance
            double tolerance = 1.2;
            var rowGroups = GroupByTolerance(textItems.Select(t => t.Y).Distinct().ToList(), tolerance);
            rowGroups = rowGroups.OrderByDescending(g => g.Average()).ToList(); // Y giảm dần từ trên xuống

            var rowMap = new Dictionary<double, int>();
            for (int r = 0; r < rowGroups.Count; r++)
                foreach (var y in rowGroups[r])
                    rowMap[y] = r;

            // Lấy danh sách X của các cột
            var colGroups = GroupByTolerance(textItems.Select(t => t.X).Distinct().ToList(), tolerance);
            colGroups = colGroups.OrderBy(g => g.Average()).ToList(); // X tăng dần từ trái sang phải

            var colMap = new Dictionary<double, int>();
            for (int c = 0; c < colGroups.Count; c++)
                foreach (var x in colGroups[c])
                    colMap[x] = c;

            result.RowCount = rowGroups.Count;
            result.ColumnCount = colGroups.Count;

            string[,] matrix = new string[result.RowCount, result.ColumnCount];

            foreach (var item in textItems)
            {
                int r = 0, c = 0;
                // Tìm row index gần nhất
                double bestDistY = double.MaxValue;
                for (int ri = 0; ri < rowGroups.Count; ri++)
                {
                    double avgY = rowGroups[ri].Average();
                    if (Math.Abs(item.Y - avgY) < bestDistY)
                    {
                        bestDistY = Math.Abs(item.Y - avgY);
                        r = ri;
                    }
                }

                // Tìm col index gần nhất
                double bestDistX = double.MaxValue;
                for (int ci = 0; ci < colGroups.Count; ci++)
                {
                    double avgX = colGroups[ci].Average();
                    if (Math.Abs(item.X - avgX) < bestDistX)
                    {
                        bestDistX = Math.Abs(item.X - avgX);
                        c = ci;
                    }
                }

                string clean = config.CleanMTextFormatting ? CleanMTextString(item.Text) : item.Text;
                if (string.IsNullOrEmpty(matrix[r, c]))
                    matrix[r, c] = clean;
                else
                    matrix[r, c] += " " + clean;
            }

            for (int r = 0; r < result.RowCount; r++)
            {
                for (int c = 0; c < result.ColumnCount; c++)
                {
                    string txt = matrix[r, c] ?? string.Empty;
                    ExtractedCell cell = new ExtractedCell
                    {
                        Row = r,
                        Column = c,
                        CleanText = txt,
                        IsHeaderOrTitle = (r == 0)
                    };
                    if (config.AutoDetectNumericValues && !string.IsNullOrWhiteSpace(txt))
                    {
                        cell.NumericValue = TryParseNumericValue(txt);
                    }
                    result.Cells.Add(cell);
                }
            }

            if (result.Cells.Count > 0) result.TableTitle = result.Cells[0].CleanText;
            return result;
        }

        private class TextItemInfo
        {
            public double X { get; set; }
            public double Y { get; set; }
            public string Text { get; set; } = string.Empty;
        }

        private static List<TextItemInfo> ExplodeToFindTexts(Entity ent, int depth)
        {
            List<TextItemInfo> results = new List<TextItemInfo>();
            if (depth > 6) return results;

            if (ent is DBText dbText)
            {
                results.Add(new TextItemInfo { X = dbText.Position.X, Y = dbText.Position.Y, Text = dbText.TextString });
                return results;
            }
            if (ent is MText mText)
            {
                results.Add(new TextItemInfo { X = mText.Location.X, Y = mText.Location.Y, Text = mText.Contents });
                return results;
            }

            DBObjectCollection objs = new DBObjectCollection();
            try
            {
                ent.Explode(objs);
                foreach (DBObject obj in objs)
                {
                    if (obj is Entity childEnt)
                    {
                        results.AddRange(ExplodeToFindTexts(childEnt, depth + 1));
                        childEnt.Dispose();
                    }
                    else
                    {
                        obj.Dispose();
                    }
                }
            }
            catch { }

            return results;
        }

        private static List<List<double>> GroupByTolerance(List<double> values, double tolerance)
        {
            var groups = new List<List<double>>();
            var sorted = values.OrderBy(v => v).ToList();

            foreach (var val in sorted)
            {
                bool added = false;
                foreach (var g in groups)
                {
                    if (Math.Abs(g.Average() - val) <= tolerance)
                    {
                        g.Add(val);
                        added = true;
                        break;
                    }
                }
                if (!added)
                {
                    groups.Add(new List<double> { val });
                }
            }
            return groups;
        }

        #endregion

        #region Logic Ghi Excel Bằng ClosedXML & CSV

        /// <summary>
        /// Thực thi trích xuất và ghi ra file Excel / CSV
        /// </summary>
        private static void ExecuteExportTablesToExcel(Database db, TableExportExcelConfig config, Action<string, int> progressCallback)
        {
            List<ExtractedTable> extractedList = new List<ExtractedTable>();

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                int current = 0;
                int total = config.SelectedTables.Count;

                foreach (var tableItem in config.SelectedTables)
                {
                    current++;
                    int percent = (int)((current / (double)total) * 40);
                    progressCallback($"Đang đọc bảng {current}/{total}: {tableItem.Title}...", percent);

                    if (tableItem.ObjectId.IsNull || tableItem.ObjectId.IsErased) continue;

                    var tableData = ExtractTableData(tableItem.ObjectId, tr, config);
                    if (tableData != null)
                    {
                        extractedList.Add(tableData);
                    }
                }
                tr.Commit();
            }

            if (extractedList.Count == 0)
            {
                throw new System.Exception("Không thể trích xuất dữ liệu từ các bảng đã chọn!");
            }

            progressCallback("Đang định dạng và ghi dữ liệu ra file...", 50);

            if (config.IsXlsxFormat)
            {
                WriteToExcelClosedXml(extractedList, config, progressCallback);
            }
            else
            {
                WriteToCsvFile(extractedList, config, progressCallback);
            }

            progressCallback("Hoàn tất xuất dữ liệu thành công!", 100);

            // Tự động mở file nếu được bật
            if (config.OpenFileAfterExport && File.Exists(config.OutputFilePath))
            {
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = config.OutputFilePath,
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }
                catch (System.Exception ex)
                {
                    Debug.WriteLine("Không thể mở file: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Ghi dữ liệu ra file Excel .xlsx với ClosedXML
        /// </summary>
        private static void WriteToExcelClosedXml(List<ExtractedTable> tables, TableExportExcelConfig config, Action<string, int> progressCallback)
        {
            using (var workbook = new XLWorkbook())
            {
                HashSet<string> usedSheetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                if (config.ExportEachTableToSeparateSheet)
                {
                    // Chế độ 1: Mỗi bảng 1 Sheet riêng biệt
                    int tableIndex = 1;
                    foreach (var table in tables)
                    {
                        progressCallback($"Đang xuất sheet cho bảng {tableIndex}/{tables.Count}...", 50 + (int)((tableIndex / (double)tables.Count) * 40));

                        string rawSheetName = CleanSheetName(string.IsNullOrWhiteSpace(table.TableTitle) ? $"Bang_{tableIndex}" : table.TableTitle);
                        string uniqueSheetName = GetUniqueSheetName(rawSheetName, usedSheetNames);
                        usedSheetNames.Add(uniqueSheetName);

                        var ws = workbook.Worksheets.Add(uniqueSheetName);
                        WriteSingleTableToWorksheet(ws, table, startRow: 1, startCol: 1, config);
                        tableIndex++;
                    }
                }
                else
                {
                    // Chế độ 2: Gộp tất cả bảng vào 1 Sheet chung
                    progressCallback("Đang gộp tất cả bảng vào 1 sheet...", 70);
                    var ws = workbook.Worksheets.Add("TongHop_Bang_CAD");

                    int currentRow = 1;
                    foreach (var table in tables)
                    {
                        WriteSingleTableToWorksheet(ws, table, startRow: currentRow, startCol: 1, config);
                        currentRow += table.RowCount + config.BlankRowsBetweenTables;
                    }
                }

                // Lưu file Excel
                progressCallback("Đang lưu file Excel...", 95);
                workbook.SaveAs(config.OutputFilePath);
            }
        }

        /// <summary>
        /// Ghi 1 bảng cụ thể vào Worksheet ClosedXML với đầy đủ định dạng
        /// </summary>
        private static void WriteSingleTableToWorksheet(IXLWorksheet ws, ExtractedTable table, int startRow, int startCol, TableExportExcelConfig config)
        {
            int rowCount = table.RowCount;
            int colCount = table.ColumnCount;
            if (rowCount == 0 || colCount == 0) return;

            // 1. Ghi giá trị từng Cell
            foreach (var cell in table.Cells)
            {
                int r = startRow + cell.Row;
                int c = startCol + cell.Column;
                var xlCell = ws.Cell(r, c);

                if (config.AutoDetectNumericValues && cell.NumericValue.HasValue)
                {
                    xlCell.Value = cell.NumericValue.Value;
                    xlCell.Style.NumberFormat.Format = "#,##0.00;(#,##0.00);\"-\"";
                }
                else
                {
                    xlCell.Value = cell.CleanText;
                }

                // Căn lề ngang
                xlCell.Style.Alignment.Horizontal = MapAlignment(cell.Alignment, cell.NumericValue.HasValue);
                xlCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                // Format Header / Title
                if (config.FormatHeaderRows && cell.IsHeaderOrTitle)
                {
                    xlCell.Style.Font.Bold = true;
                    xlCell.Style.Fill.BackgroundColor = (cell.Row == 0)
                        ? XLColor.FromArgb(217, 225, 242) // Xanh dương pastel dịu mắt cho Title
                        : XLColor.FromArgb(235, 241, 222); // Xanh lá pastel cho Header
                    xlCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }
            }

            // 2. Gộp ô (Merge Cells)
            if (config.KeepMergedCells && table.MergedRanges != null)
            {
                foreach (var m in table.MergedRanges)
                {
                    try
                    {
                        int top = startRow + m.TopRow;
                        int left = startCol + m.LeftColumn;
                        int bottom = startRow + m.BottomRow;
                        int right = startCol + m.RightColumn;

                        if (bottom >= top && right >= left)
                        {
                            ws.Range(top, left, bottom, right).Merge();
                        }
                    }
                    catch { }
                }
            }

            // 3. Kẻ khung viền (Borders)
            if (config.AddCellBorders)
            {
                var tableRange = ws.Range(startRow, startCol, startRow + rowCount - 1, startCol + colCount - 1);
                tableRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                tableRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                tableRange.Style.Border.OutsideBorderColor = XLColor.FromArgb(140, 150, 160);
                tableRange.Style.Border.InsideBorderColor = XLColor.FromArgb(200, 205, 210);
            }

            // 4. Tự động căn chỉnh độ rộng cột (Auto-fit Columns)
            if (config.AutoFitColumns)
            {
                ws.Columns(startCol, startCol + colCount - 1).AdjustToContents();
                // Giới hạn độ rộng hợp lý
                for (int c = startCol; c < startCol + colCount; c++)
                {
                    var col = ws.Column(c);
                    if (col.Width < 9) col.Width = 9;
                    if (col.Width > 50) col.Width = 50;
                }
            }
        }

        /// <summary>
        /// Ghi ra file CSV (UTF-8 BOM chuẩn hiển thị tiếng Việt trên Excel)
        /// </summary>
        private static void WriteToCsvFile(List<ExtractedTable> tables, TableExportExcelConfig config, Action<string, int> progressCallback)
        {
            StringBuilder sb = new StringBuilder();

            int tableIndex = 0;
            foreach (var table in tables)
            {
                if (tableIndex > 0)
                {
                    for (int i = 0; i < config.BlankRowsBetweenTables; i++)
                    {
                        sb.AppendLine();
                    }
                }

                sb.AppendLine($"# Bảng: {table.TableTitle} (Loại: {table.TableType}, Handle: {table.Handle})");

                int rowCount = table.RowCount;
                int colCount = table.ColumnCount;
                string[,] matrix = new string[rowCount, colCount];

                foreach (var cell in table.Cells)
                {
                    if (cell.Row < rowCount && cell.Column < colCount)
                    {
                        matrix[cell.Row, cell.Column] = cell.CleanText;
                    }
                }

                for (int r = 0; r < rowCount; r++)
                {
                    List<string> rowCells = new List<string>();
                    for (int c = 0; c < colCount; c++)
                    {
                        rowCells.Add(CsvEscape(matrix[r, c] ?? string.Empty));
                    }
                    sb.AppendLine(string.Join(",", rowCells));
                }

                tableIndex++;
            }

            File.WriteAllText(config.OutputFilePath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        }

        #endregion

        #region Helper Functions (String Cleaner, Math, MText)

        /// <summary>
        /// Làm sạch chuỗi MText trong AutoCAD
        /// </summary>
        public static string CleanMTextString(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            string text = input;

            // Xóa font format \F...; hoặc \f...;
            text = Regex.Replace(text, @"\\[Ff][^;]*;", "", RegexOptions.IgnoreCase);
            // Xóa color format \C...; hoặc \c...;
            text = Regex.Replace(text, @"\\[Cc][0-9]+;", "");
            // Xóa height format \H...;
            text = Regex.Replace(text, @"\\[Hh][0-9\.]+x?;", "", RegexOptions.IgnoreCase);
            // Xóa width format \W...;
            text = Regex.Replace(text, @"\\[Ww][0-9\.]+x?;", "", RegexOptions.IgnoreCase);
            // Xóa oblique angle \Q...;
            text = Regex.Replace(text, @"\\[Qq][0-9\.\-]+;", "", RegexOptions.IgnoreCase);
            // Xóa underline, overline, strikethrough \L, \l, \O, \o, \K, \k
            text = Regex.Replace(text, @"\\[LlOoKk]", "");
            // Xóa alignment tag \A...;
            text = Regex.Replace(text, @"\\A[0-2];", "");
            // Xóa stack format \S...^...;
            text = Regex.Replace(text, @"\\S([^;]*?)([\^/#])([^;]*?);", "$1/$3");
            // Xóa ngoặc nhóm { }
            text = Regex.Replace(text, @"[{}]", "");
            // Thay \P và \X thành khoảng trắng hoặc xuống dòng
            text = Regex.Replace(text, @"\\[PXpx]", " ");
            // Thay \~ (non-breaking space) thành space thường
            text = text.Replace("\\~", " ");
            // Thay \r, \n dư thừa
            text = text.Replace("\r", " ").Replace("\n", " ");

            return text.Trim();
        }

        /// <summary>
        /// Thử parse chuỗi thành kiểu số hợp lệ (hỗ trợ cả dấu chấm và phẩy)
        /// </summary>
        public static double? TryParseNumericValue(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            string s = text.Trim();

            // Nếu chuỗi bắt đầu bằng '0' và có nhiều chữ số mà không có dấu chấm/phẩy (ví dụ mã số "01", "007") -> Giữ String
            if (s.Length > 1 && s.StartsWith("0") && char.IsDigit(s[1]) && !s.Contains(".") && !s.Contains(","))
            {
                return null;
            }

            // Chuẩn hóa dấu phân cách
            string normalized = s.Replace(" ", "").Replace(",", ".");
            if (double.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out double result))
            {
                return result;
            }

            return null;
        }

        private static XLAlignmentHorizontalValues MapAlignment(CellAlignment align, bool isNumber)
        {
            switch (align)
            {
                case CellAlignment.TopLeft:
                case CellAlignment.MiddleLeft:
                case CellAlignment.BottomLeft:
                    return XLAlignmentHorizontalValues.Left;

                case CellAlignment.TopRight:
                case CellAlignment.MiddleRight:
                case CellAlignment.BottomRight:
                    return XLAlignmentHorizontalValues.Right;

                case CellAlignment.TopCenter:
                case CellAlignment.MiddleCenter:
                case CellAlignment.BottomCenter:
                    return XLAlignmentHorizontalValues.Center;

                default:
                    return isNumber ? XLAlignmentHorizontalValues.Right : XLAlignmentHorizontalValues.Left;
            }
        }

        private static string CleanSheetName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Sheet1";
            // Ký tự cấm trong tên Sheet Excel: \ / ? * [ ] :
            string invalidChars = @"[\\/\?\*\[\]:]";
            string clean = Regex.Replace(name, invalidChars, "_").Trim();
            if (clean.Length > 31) clean = clean.Substring(0, 31);
            return string.IsNullOrEmpty(clean) ? "Sheet1" : clean;
        }

        private static string GetUniqueSheetName(string baseName, HashSet<string> existingNames)
        {
            if (!existingNames.Contains(baseName)) return baseName;

            int index = 1;
            while (true)
            {
                string suffix = $"_{index}";
                string candidate = baseName;
                if (candidate.Length + suffix.Length > 31)
                {
                    candidate = candidate.Substring(0, 31 - suffix.Length);
                }
                candidate += suffix;

                if (!existingNames.Contains(candidate)) return candidate;
                index++;
            }
        }

        private static string CsvEscape(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            string s = input.Replace("\r", " ").Replace("\n", " ").Replace("\"", "\"\"");
            if (s.Contains(',') || s.Contains('"') || s.StartsWith(" ") || s.EndsWith(" "))
            {
                s = "\"" + s + "\"";
            }
            return s;
        }

        #endregion
    }
}

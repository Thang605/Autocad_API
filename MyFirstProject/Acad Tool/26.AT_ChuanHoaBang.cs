// (C) Copyright 2026 by T27
// Lệnh chuẩn hoá đối tượng bản vẽ - Bắt đầu với đối tượng BẢNG (Table & TableStyle t27)
//
using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Colors;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using ATable = Autodesk.AutoCAD.DatabaseServices.Table;
using AcadDBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;

[assembly: CommandClass(typeof(Civil3DCsharp.AT_ChuanHoaBang_Commands))]

namespace Civil3DCsharp
{
    /// <summary>
    /// Các lệnh Chuẩn Hoá Đối Tượng Bản Vẽ - Chuẩn Hoá Bảng (Table)
    /// </summary>
    public class AT_ChuanHoaBang_Commands
    {
        // Ghi nhớ danh sách ObjectId các bảng đã chọn
        private static List<ObjectId> _lastSelectedTables = new List<ObjectId>();

        /// <summary>
        /// Lệnh chính: Chuẩn hoá đối tượng bảng theo phong cách và tiêu chuẩn T27
        /// </summary>
        [CommandMethod("AT_ChuanHoaBang")]
        [CommandMethod("AT_ChuanHoaDoiTuong")]
        [CommandMethod("AT_ChuanHoaTable")]
        [CommandMethod("CH_BANG")]
        [CommandMethod("CHUANHOABANG")]
        public static void RunChuanHoaBang()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            Editor ed = doc.Editor;
            Database db = doc.Database;

            try
            {
                ed.WriteMessage("\n--- Khởi động lệnh Chuẩn Hoá Đối Tượng Bảng (T27) ---");

                // 1. Quét sơ bộ danh sách Text Styles và số lượng Table trong bản vẽ
                List<string> textStyles = new List<string>();
                int initialTableCount = 0;

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    textStyles = GetAvailableTextStyleNames(db, tr);
                    var allTables = CollectAllTables(db, tr);
                    initialTableCount = allTables.Count;
                    tr.Commit();
                }

                // 2. Khởi tạo Form
                using (ChuanHoaBangForm form = new ChuanHoaBangForm(textStyles, initialTableCount))
                {
                    // Callback 1: Lấy danh sách Text Styles khi Refresh
                    form.OnGetAvailableTextStyles = () =>
                    {
                        using Transaction tr = db.TransactionManager.StartTransaction();
                        var list = GetAvailableTextStyleNames(db, tr);
                        tr.Commit();
                        return list;
                    };

                    // Callback 2: Quét lại số lượng bảng
                    form.OnScanTablesCount = () =>
                    {
                        using Transaction tr = db.TransactionManager.StartTransaction();
                        var list = CollectAllTables(db, tr);
                        tr.Commit();
                        return list.Count;
                    };

                    // Callback 3: Chọn bảng trên màn hình (User Interaction)
                    form.OnSelectTablesOnScreen = () =>
                    {
                        List<ObjectId> pickedIds = new List<ObjectId>();

                        using (var interaction = ed.StartUserInteraction(form))
                        {
                            PromptSelectionOptions pso = new PromptSelectionOptions
                            {
                                MessageForAdding = "\nChọn các đối tượng Bảng (Table) trên bản vẽ (quét chọn hoặc click từng bảng): ",
                                AllowDuplicates = false
                            };

                            TypedValue[] filterList = new TypedValue[]
                            {
                                new TypedValue((int)DxfCode.Start, "ACAD_TABLE")
                            };
                            SelectionFilter filter = new SelectionFilter(filterList);

                            PromptSelectionResult psr = ed.GetSelection(pso, filter);
                            interaction.End();

                            if (psr.Status == PromptStatus.OK && psr.Value != null)
                            {
                                ObjectId[] ids = psr.Value.GetObjectIds();
                                using Transaction tr = db.TransactionManager.StartTransaction();
                                foreach (ObjectId id in ids)
                                {
                                    if (id.IsValid && !id.IsErased)
                                    {
                                        var obj = tr.GetObject(id, OpenMode.ForRead);
                                        if (obj is ATable)
                                        {
                                            pickedIds.Add(id);
                                        }
                                    }
                                }
                                tr.Commit();

                                ed.WriteMessage($"\nĐã chọn được {pickedIds.Count} đối tượng Bảng (Table).");
                            }
                            else
                            {
                                ed.WriteMessage("\nKhông có đối tượng Bảng nào được chọn.");
                            }
                        }

                        return pickedIds;
                    };

                    // Callback 4: Thực thi Chuẩn hoá
                    form.OnExecuteStandardize = (config, selectedIds, logAction) =>
                    {
                        ExecuteStandardizeProcess(db, ed, config, selectedIds, logAction);
                    };

                    // Hiển thị Modal Dialog
                    Application.ShowModalDialog(form);
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n❌ Lỗi khởi chạy lệnh: {ex.Message}");
            }
        }

        #region Core Standardization Logic

        /// <summary>
        /// Quy trình thực hiện chuẩn hoá
        /// </summary>
        private static void ExecuteStandardizeProcess(
            Database db,
            Editor ed,
            TableStandardizeConfig config,
            List<ObjectId> selectedIds,
            Action<string> log)
        {
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    // 1. Tìm hoặc gán TextStyleId
                    ObjectId textStyleId = GetOrCreateTextStyleId(db, tr, config.TextStyleName, log);

                    // 2. Tạo mới hoặc Cập nhật TableStyle
                    ObjectId tableStyleId = ObjectId.Null;
                    if (config.CreateOrUpdateStyle)
                    {
                        tableStyleId = CreateOrUpdateTableStyle(db, tr, config, textStyleId, log);
                    }
                    else
                    {
                        tableStyleId = FindTableStyleId(db, tr, config.TableStyleName);
                        if (tableStyleId.IsNull)
                        {
                            log($"⚠ Không tìm thấy TableStyle '{config.TableStyleName}', tiến hành tạo mới...");
                            tableStyleId = CreateOrUpdateTableStyle(db, tr, config, textStyleId, log);
                        }
                    }

                    // 3. Đặt làm Style hiện hành nếu được chọn
                    if (config.SetCurrentStyle && !tableStyleId.IsNull)
                    {
                        db.Tablestyle = tableStyleId;
                        log($"⭐ Đã đặt '{config.TableStyleName}' làm Table Style mặc định hiện hành.");
                    }

                    // 4. Xác định danh sách Bảng cần chuẩn hoá
                    List<ObjectId> targetTableIds = new List<ObjectId>();
                    if (config.ApplyToAll)
                    {
                        targetTableIds = CollectAllTables(db, tr);
                        log($"📋 Chuẩn hoá TOÀN BỘ {targetTableIds.Count} bảng trong bản vẽ...");
                    }
                    else
                    {
                        targetTableIds = selectedIds.Where(id => id.IsValid && !id.IsErased).ToList();
                        log($"📋 Chuẩn hoá {targetTableIds.Count} bảng đã chọn...");
                    }

                    if (targetTableIds.Count == 0)
                    {
                        log("⚠ Không có bảng nào để chuẩn hoá.");
                        tr.Commit();
                        return;
                    }

                    // 5. Chuẩn hoá từng đối tượng Bảng
                    int successTableCount = 0;
                    int totalCellsCount = 0;

                    for (int i = 0; i < targetTableIds.Count; i++)
                    {
                        ObjectId tblId = targetTableIds[i];
                        try
                        {
                            ATable? table = tr.GetObject(tblId, OpenMode.ForWrite) as ATable;
                            if (table == null) continue;

                            int cellCountInTable = 0;
                            StandardizeSingleTable(table, tableStyleId, textStyleId, config, ref cellCountInTable);

                            successTableCount++;
                            totalCellsCount += cellCountInTable;
                            log($"  [Bảng {i + 1}/{targetTableIds.Count}] Vị trí ({table.Position.X:F1}, {table.Position.Y:F1}): {table.Rows.Count} hàng x {table.Columns.Count} cột ({cellCountInTable} cells) - OK");
                        }
                        catch (System.Exception ex)
                        {
                            log($"  ❌ [Bảng {i + 1}] Lỗi: {ex.Message}");
                        }
                    }

                    // Commit Transaction
                    tr.Commit();

                    // Cập nhật lại cache ID
                    _lastSelectedTables = new List<ObjectId>(targetTableIds);

                    log("==================================================");
                    log($"🎉 HOÀN THÀNH CHUẨN HOÁ {successTableCount}/{targetTableIds.Count} BẢNG ({totalCellsCount} CELLS)!");
                    ed.WriteMessage($"\n✅ Đã chuẩn hoá thành công {successTableCount} bảng trong bản vẽ.");
                }
                catch (System.Exception ex)
                {
                    tr.Abort();
                    log($"❌ LỖI TRONG QUÁ TRÌNH CHUẨN HOÁ: {ex.Message}");
                    ed.WriteMessage($"\n❌ Lỗi chuẩn hoá bảng: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Chuẩn hoá một đối tượng Bảng cụ thể
        /// </summary>
        private static void StandardizeSingleTable(
            ATable table,
            ObjectId tableStyleId,
            ObjectId textStyleId,
            TableStandardizeConfig config,
            ref int cellsProcessed)
        {
            // 1. Gán TableStyle
            if (!tableStyleId.IsNull)
            {
                table.TableStyle = tableStyleId;
            }

            int rowCount = table.Rows.Count;
            int colCount = table.Columns.Count;

            // 2. Chuẩn hoá từng Cell nếu có yêu cầu
            if (config.StandardizeCells)
            {
                for (int r = 0; r < rowCount; r++)
                {
                    // Xác định vai trò của hàng: 0 = Title, 1 = Header, >=2 = Data
                    bool isTitleRow = (r == 0);
                    bool isHeaderRow = (r == 1);
                    double targetHeight = isTitleRow ? config.TitleHeight : (isHeaderRow ? config.HeaderHeight : config.DataHeight);
                    CellAlignment targetAlign = (isTitleRow || isHeaderRow) ? config.HeaderAlignment : config.DataAlignment;

                    for (int c = 0; c < colCount; c++)
                    {
                        var cell = table.Cells[r, c];
                        if (cell == null) continue;

                        // Gán Text Style & Chiều cao chữ
                        if (!textStyleId.IsNull)
                        {
                            cell.TextStyleId = textStyleId;
                        }
                        cell.TextHeight = targetHeight;
                        cell.Alignment = targetAlign;

                        // Gán khoảng căn lề ô (Margin) chuẩn xác qua Borders
                        try
                        {
                            cell.Borders.Top.Margin = config.CellMargin;
                            cell.Borders.Bottom.Margin = config.CellMargin;
                            cell.Borders.Left.Margin = config.CellMargin;
                            cell.Borders.Right.Margin = config.CellMargin;
                        }
                        catch { }

                        cellsProcessed++;
                    }

                    // Tự động tối ưu chiều cao hàng
                    if (config.RecalculateRowHeight)
                    {
                        double minRequiredHeight = targetHeight + (2.0 * config.CellMargin) + 1.5;
                        if (table.Rows[r].Height < minRequiredHeight)
                        {
                            table.Rows[r].Height = minRequiredHeight;
                        }
                    }
                }
            }

            // 3. Tái tạo bố cục bảng
            table.RecomputeTableBlock(true);
            table.GenerateLayout();
        }

        /// <summary>
        /// Tạo mới hoặc cập nhật TableStyle trong Database
        /// </summary>
        private static ObjectId CreateOrUpdateTableStyle(
            Database db,
            Transaction tr,
            TableStandardizeConfig config,
            ObjectId textStyleId,
            Action<string> log)
        {
            DBDictionary dict = (DBDictionary)tr.GetObject(db.TableStyleDictionaryId, OpenMode.ForWrite);
            TableStyle ts;
            ObjectId tsId;

            if (dict.Contains(config.TableStyleName))
            {
                tsId = dict.GetAt(config.TableStyleName);
                ts = (TableStyle)tr.GetObject(tsId, OpenMode.ForWrite);
                log($"🔄 Đang cập nhật thuộc tính TableStyle '{config.TableStyleName}'...");
            }
            else
            {
                ts = new TableStyle();
                tsId = dict.SetAt(config.TableStyleName, ts);
                tr.AddNewlyCreatedDBObject(ts, true);
                log($"✨ Đã tạo mới TableStyle '{config.TableStyleName}'.");
            }

            // Cấu hình Title Row (Tên bảng: Height = 4, TextStyle, Margin)
            if (!textStyleId.IsNull)
            {
                ts.SetTextStyle(textStyleId, (int)RowType.TitleRow);
                ts.SetTextStyle(textStyleId, (int)RowType.HeaderRow);
                ts.SetTextStyle(textStyleId, (int)RowType.DataRow);
            }

            ts.SetTextHeight(config.TitleHeight, (int)RowType.TitleRow);
            ts.SetAlignment(config.HeaderAlignment, (int)RowType.TitleRow);

            // Cấu hình Header Row (Tiêu đề cột: Height = 3, Margin)
            ts.SetTextHeight(config.HeaderHeight, (int)RowType.HeaderRow);
            ts.SetAlignment(config.HeaderAlignment, (int)RowType.HeaderRow);

            // Cấu hình Data Row (Dữ liệu: Height = 2, Margin)
            ts.SetTextHeight(config.DataHeight, (int)RowType.DataRow);
            ts.SetAlignment(config.DataAlignment, (int)RowType.DataRow);

            // Cấu hình Cell Margins
            ts.HorizontalCellMargin = config.CellMargin;
            ts.VerticalCellMargin = config.CellMargin;

            // Đặt màu chữ ByBlock / ByLayer chuẩn
            ts.SetColor(Autodesk.AutoCAD.Colors.Color.FromColorIndex(ColorMethod.ByAci, 0), (int)RowType.TitleRow | (int)RowType.HeaderRow | (int)RowType.DataRow);

            log($"✅ Cấu hình TableStyle '{config.TableStyleName}' thành công (Title={config.TitleHeight}, Header={config.HeaderHeight}, Data={config.DataHeight}, Margin={config.CellMargin}).");

            return tsId;
        }

        /// <summary>
        /// Tìm ID của TableStyle theo tên
        /// </summary>
        private static ObjectId FindTableStyleId(Database db, Transaction tr, string styleName)
        {
            DBDictionary dict = (DBDictionary)tr.GetObject(db.TableStyleDictionaryId, OpenMode.ForRead);
            if (dict.Contains(styleName))
            {
                return dict.GetAt(styleName);
            }
            return ObjectId.Null;
        }

        /// <summary>
        /// Lấy hoặc kiểm tra TextStyleId theo tên
        /// </summary>
        private static ObjectId GetOrCreateTextStyleId(Database db, Transaction tr, string textStyleName, Action<string> log)
        {
            TextStyleTable textStyleTable = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
            if (textStyleTable.Has(textStyleName))
            {
                return textStyleTable[textStyleName];
            }

            // Fallback sang TextStyle Standard hoặc TextStyle hiện hành
            if (textStyleTable.Has("Standard"))
            {
                log($"ℹ TextStyle '{textStyleName}' không tìm thấy, sử dụng TextStyle 'Standard'.");
                return textStyleTable["Standard"];
            }

            log($"ℹ Sử dụng TextStyle hiện hành của bản vẽ.");
            return db.Textstyle;
        }

        /// <summary>
        /// Lấy danh sách tên tất cả TextStyle trong bản vẽ
        /// </summary>
        private static List<string> GetAvailableTextStyleNames(Database db, Transaction tr)
        {
            List<string> list = new List<string>();
            TextStyleTable textStyleTable = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
            foreach (ObjectId id in textStyleTable)
            {
                if (id.IsValid && !id.IsErased)
                {
                    TextStyleTableRecord? record = tr.GetObject(id, OpenMode.ForRead) as TextStyleTableRecord;
                    if (record != null && !string.IsNullOrEmpty(record.Name) && !record.Name.StartsWith("*"))
                    {
                        list.Add(record.Name);
                    }
                }
            }
            return list.OrderBy(n => n).ToList();
        }

        /// <summary>
        /// Tìm tất cả các đối tượng Bảng (Table) trong bản vẽ (ModelSpace & Layout Blocks)
        /// </summary>
        private static List<ObjectId> CollectAllTables(Database db, Transaction tr)
        {
            List<ObjectId> tables = new List<ObjectId>();
            BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

            foreach (ObjectId btrId in bt)
            {
                if (!btrId.IsValid || btrId.IsErased) continue;

                BlockTableRecord? btr = tr.GetObject(btrId, OpenMode.ForRead) as BlockTableRecord;
                if (btr == null) continue;

                // Chỉ quét ModelSpace hoặc các Layouts (PaperSpace)
                if (btr.IsLayout || btr.Name.Equals(BlockTableRecord.ModelSpace, StringComparison.OrdinalIgnoreCase))
                {
                    foreach (ObjectId entId in btr)
                    {
                        if (!entId.IsValid || entId.IsErased) continue;
                        if (entId.ObjectClass.DxfName.Equals("ACAD_TABLE", StringComparison.OrdinalIgnoreCase) ||
                            entId.ObjectClass.Name.Equals("AcDbTable", StringComparison.OrdinalIgnoreCase))
                        {
                            tables.Add(entId);
                        }
                    }
                }
            }

            return tables;
        }

        #endregion
    }
}

using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(Civil3DCsharp.CTA_DeXuatCatNgang_Commands))]

namespace Civil3DCsharp
{
    public class CTA_DeXuatCatNgang_Commands
    {
        [CommandMethod("CTA_De_Xuat_Cat_Ngang")]
        public void CTSV_DeXuatCatNgang()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            try
            {
                using (DeXuatCatNgangForm form = new DeXuatCatNgangForm())
                {
                    DialogResult result = Application.ShowModalDialog(form);

                    if (result == DialogResult.OK && form.RequestDrawCadTable)
                    {
                        DrawCrossSectionTableToCad(doc, db, ed, form.CurrentInputParameters, form.CurrentEvaluationResults);
                    }
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n[LỖI] Không thể chạy lệnh Đề xuất cắt ngang: {ex.Message}");
            }
        }

        private void DrawCrossSectionTableToCad(Document doc, Database db, Editor ed, CrossSectionInputParameters inputParams, List<CrossSectionCheckItem> results)
        {
            PromptPointOptions ppo = new PromptPointOptions("\nChọn điểm chèn Bảng đề xuất cắt ngang trên bản vẽ: ");
            PromptPointResult ppr = ed.GetPoint(ppo);
            if (ppr.Status != PromptStatus.OK) return;

            Point3d insertPt = ppr.Value;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                Table tb = new Table();
                tb.TableStyle = db.Tablestyle;
                tb.Position = insertPt;

                int totalRows = 5 + results.Count; // Title + Subtitle + Header + Items + Summary
                int totalCols = 6;
                tb.SetSize(totalRows, totalCols);

                // Setup Row Heights & Column Widths
                tb.Columns[0].Width = 12.0; // STT
                tb.Columns[1].Width = 50.0; // Element
                tb.Columns[2].Width = 35.0; // Proposed
                tb.Columns[3].Width = 45.0; // Standard
                tb.Columns[4].Width = 30.0; // Status
                tb.Columns[5].Width = 60.0; // Note

                // Row 0: Main Title
                tb.Cells[0, 0].TextString = "BẢNG ĐỀ XUẤT MẶT CẮT NGANG ĐƯỜNG VÀ KIỂM TRA TIÊU CHUẨN";
                tb.Cells[0, 0].TextHeight = 3.5;
                tb.Cells[0, 0].Alignment = CellAlignment.MiddleCenter;

                // Row 1: Subtitle Context
                tb.Cells[1, 0].TextString = $"Tiêu chuẩn: {inputParams.StandardName} | Cấp đường: {inputParams.RoadType} | Vtk = {inputParams.DesignSpeed} km/h";
                tb.Cells[1, 0].TextHeight = 2.5;
                tb.Cells[1, 0].Alignment = CellAlignment.MiddleCenter;

                // Row 2: Header
                string[] headers = new string[] { "STT", "Yếu tố Cắt ngang", "Giá trị Đề xuất", "Yêu cầu Tiêu chuẩn", "Đánh giá", "Ghi chú Đánh giá" };
                for (int c = 0; c < totalCols; c++)
                {
                    tb.Cells[2, c].TextString = headers[c];
                    tb.Cells[2, c].TextHeight = 2.5;
                    tb.Cells[2, c].Alignment = CellAlignment.MiddleCenter;
                }

                // Rows 3 -> 3+N: Items
                int failCount = 0;
                for (int i = 0; i < results.Count; i++)
                {
                    var item = results[i];
                    int row = 3 + i;

                    tb.Cells[row, 0].TextString = (i + 1).ToString();
                    tb.Cells[row, 1].TextString = item.ElementName;
                    tb.Cells[row, 2].TextString = item.ProposedValue;
                    tb.Cells[row, 3].TextString = item.StandardRequirement;
                    tb.Cells[row, 4].TextString = item.Status == CheckStatus.Pass ? "ĐẠT" : (item.Status == CheckStatus.Fail ? "KHÔNG ĐẠT" : "CẢNH BÁO");
                    tb.Cells[row, 5].TextString = item.Note;

                    for (int c = 0; c < totalCols; c++)
                    {
                        tb.Cells[row, c].TextHeight = 2.0;
                        if (c == 0 || c == 4) tb.Cells[row, c].Alignment = CellAlignment.MiddleCenter;
                        else if (c == 2 || c == 3) tb.Cells[row, c].Alignment = CellAlignment.MiddleRight;
                        else tb.Cells[row, c].Alignment = CellAlignment.MiddleLeft;
                    }

                    if (item.Status == CheckStatus.Fail) failCount++;
                }

                // Final Row: Summary
                int lastRow = totalRows - 1;
                string summaryText = failCount == 0 
                    ? $"KẾT LUẬN: ĐỀ XUẤT MẶT CẮT NGANG ĐẠT QUY CHUẨN TIÊU CHUẨN 100% ({results.Count}/{results.Count} TIÊU CHÍ)"
                    : $"KẾT LUẬN: CÓ {failCount} TIÊU CHÍ CHƯA ĐẠT QUY CHUẨN - CẦN ĐIỀU CHỈNH LẠI";

                tb.Cells[lastRow, 0].TextString = summaryText;
                tb.Cells[lastRow, 0].TextHeight = 2.5;
                tb.Cells[lastRow, 0].Alignment = CellAlignment.MiddleCenter;

                btr.AppendEntity(tb);
                tr.AddNewlyCreatedDBObject(tb, true);
                tr.Commit();

                ed.WriteMessage($"\n[THÀNH CÔNG] Đã vẽ Bảng đề xuất cắt ngang và kiểm tra tiêu chuẩn vào bản vẽ AutoCAD!");
            }
        }
    }
}

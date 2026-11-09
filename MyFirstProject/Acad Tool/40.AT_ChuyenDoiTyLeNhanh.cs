// (C) Copyright 2026 by T27
// Lệnh AT_Chuyen_Doi_Ty_Le_Nhanh: Thiết lập nhanh môi trường tỷ lệ vẽ (1:10 -> 1:10000)
// và quy đổi đồng bộ kích thước Dim, Text, MText, Block ký hiệu, Hatch trên bản vẽ
//
using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;

using MyFirstProject.Acad_Tool;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(Civil3DCsharp.AT_ChuyenDoiTyLeNhanhCmd))]

namespace Civil3DCsharp
{
    /// <summary>
    /// Class chứa lệnh AT_Chuyen_Doi_Ty_Le_Nhanh: Chuyển đổi tỷ lệ nhanh cho bản vẽ & cụm đối tượng
    /// </summary>
    public class AT_ChuyenDoiTyLeNhanhCmd
    {
        [CommandMethod("AT_Chuyen_Doi_Ty_Le_Nhanh", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void AT_Chuyen_Doi_Ty_Le_Nhanh()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            Editor ed = doc.Editor;
            ed.WriteMessage("\n═══════════════════════════════════════════════════════════════");
            ed.WriteMessage("\n  📐 AT_Chuyen_Doi_Ty_Le_Nhanh - CHUYỂN ĐỔI TỶ LỆ NHANH BẢN VẼ");
            ed.WriteMessage("\n═══════════════════════════════════════════════════════════════\n");

            try
            {
                var preSelected = new List<ObjectId>();
                PromptSelectionResult implied = ed.SelectImplied();
                if (implied.Status == PromptStatus.OK && implied.Value != null)
                {
                    foreach (SelectedObject so in implied.Value)
                    {
                        if (so != null && !so.ObjectId.IsNull && so.ObjectId.IsValid && !so.ObjectId.IsErased)
                        {
                            preSelected.Add(so.ObjectId);
                        }
                    }
                }

                using (var form = new ChuyenDoiTyLeNhanhForm(doc, preSelected))
                {
                    Application.ShowModalDialog(form);
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n❌ [Lỗi thực thi AT_Chuyen_Doi_Ty_Le_Nhanh]: {ex.Message}");
            }
        }
    }
}

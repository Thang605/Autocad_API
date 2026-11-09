// (C) Copyright 2026 by T27
// Lệnh AT_Gan_Lenh_Layer: Mở bảng Form gán lệnh tắt (số thứ tự 0..15 hoặc tên lệnh tùy chỉnh) với Layer tương ứng
//
using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;

using MyFirstProject.Acad_Tool;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(Civil3DCsharp.AT_GanLenhLayerCmd))]

namespace Civil3DCsharp
{
    /// <summary>
    /// Class chứa lệnh AT_Gan_Lenh_Layer: Bảng gán lệnh tắt (0..15) với Layer tương ứng
    /// </summary>
    public class AT_GanLenhLayerCmd
    {
        [CommandMethod("AT_Gan_Lenh_Layer", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public void AT_Gan_Lenh_Layer()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            Editor ed = doc.Editor;
            ed.WriteMessage("\n═══════════════════════════════════════════════════════════════");
            ed.WriteMessage("\n  ⚡ AT_Gan_Lenh_Layer - BẢNG GÁN LỆNH TẮT VỚI LAYER TƯƠNG ỨNG");
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

                using (var form = new GanLenhLayerForm(doc, preSelected))
                {
                    Application.ShowModalDialog(form);
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n❌ [Lỗi thực thi AT_Gan_Lenh_Layer]: {ex.Message}");
            }
        }
    }
}

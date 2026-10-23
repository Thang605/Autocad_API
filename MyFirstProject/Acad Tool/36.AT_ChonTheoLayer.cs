// (C) Copyright 2026 by T27
// Lệnh AT_Chon_Theo_Layer: Chọn tất cả đối tượng thuộc Layer đã chọn
//
using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;

using MyFirstProject.Acad_Tool;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(Civil3DCsharp.AT_ChonTheoLayerCmd))]

namespace Civil3DCsharp
{
    /// <summary>
    /// Class chứa lệnh AT_Chon_Theo_Layer: Chọn tất cả đối tượng thuộc Layer đã chọn
    /// </summary>
    public class AT_ChonTheoLayerCmd
    {
        [CommandMethod("AT_Chon_Theo_Layer")]
        public void AT_Chon_Theo_Layer()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            Editor ed = doc.Editor;
            ed.WriteMessage("\n═══════════════════════════════════════════════════════════════");
            ed.WriteMessage("\n  🎯 AT_Chon_Theo_Layer - CHỌN TẤT CẢ ĐỐI TƯỢNG THEO LAYER");
            ed.WriteMessage("\n═══════════════════════════════════════════════════════════════\n");

            try
            {
                using (var form = new ChonTheoLayerForm(doc))
                {
                    Application.ShowModalDialog(form);
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n❌ [Lỗi thực thi AT_Chon_Theo_Layer]: {ex.Message}");
            }
        }
    }
}

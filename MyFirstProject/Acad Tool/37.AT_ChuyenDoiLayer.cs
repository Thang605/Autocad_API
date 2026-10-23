// (C) Copyright 2026 by T27
// Lệnh AT_Chuyen_Doi_Layer: Chuyển tất cả đối tượng từ Layer cũ sang Layer mới theo bảng ánh xạ
//
using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;

using MyFirstProject.Acad_Tool;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(Civil3DCsharp.AT_ChuyenDoiLayerCmd))]

namespace Civil3DCsharp
{
    /// <summary>
    /// Class chứa lệnh AT_Chuyen_Doi_Layer: Chuyển tất cả đối tượng thuộc Layer sang Layer khác
    /// </summary>
    public class AT_ChuyenDoiLayerCmd
    {
        [CommandMethod("AT_Chuyen_Doi_Layer")]
        public void AT_Chuyen_Doi_Layer()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            Editor ed = doc.Editor;
            ed.WriteMessage("\n═══════════════════════════════════════════════════════════════");
            ed.WriteMessage("\n  🔄 AT_Chuyen_Doi_Layer - CHUYỂN ĐỔI ĐỐI TƯỢNG SANG LAYER KHÁC");
            ed.WriteMessage("\n═══════════════════════════════════════════════════════════════\n");

            try
            {
                using (var form = new ChuyenDoiLayerForm(doc))
                {
                    Application.ShowModalDialog(form);
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n❌ [Lỗi thực thi AT_Chuyen_Doi_Layer]: {ex.Message}");
            }
        }
    }
}

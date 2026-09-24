// (C) Copyright 2026 by T27
// Lệnh: CTS_DoiTenCoc_QuyTac (Đổi tên cọc theo quy tắc tham khảo CTS_PHATSINHCOC)
// Cho phép đổi tên cọc theo cọc đặc biệt (DT, CT, TD, P, TC, ND, NC), cọc Km, cọc H và cọc chi tiết

using System;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.Civil.DatabaseServices;
using MyFirstProject.Civil_Tool;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(Civil3DCsharp.CTS_DoiTenCoc_QuyTac_Commands))]

namespace Civil3DCsharp
{
    public class CTS_DoiTenCoc_QuyTac_Commands
    {
        private static ObjectId _lastSelectedAlignmentId = ObjectId.Null;

        [CommandMethod("CTS_DoiTenCoc_QuyTac")]
        [CommandMethod("CTS_DTCQ")]
        public static void CTSDoiTenCocQuyTac()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            Editor ed = doc.Editor;

            try
            {
                ed.WriteMessage("\n=== ĐỔI TÊN CỌC THEO QUY TẮC CHUẨN (Civil 3D) ===");

                ObjectId alignmentId = ObjectId.Null;

                // Kiểm tra xem Alignment lần trước có còn tồn tại trong bản vẽ hiện tại không
                if (!_lastSelectedAlignmentId.IsNull && _lastSelectedAlignmentId.IsValid && !_lastSelectedAlignmentId.IsErased)
                {
                    alignmentId = _lastSelectedAlignmentId;
                }
                else
                {
                    // Cho phép người dùng pick tim tuyến trước nếu muốn, hoặc Enter để mở Form chọn sau
                    PromptEntityOptions peo = new PromptEntityOptions("\nChọn tim tuyến Alignment cần đổi tên cọc [Hoặc Enter để mở Form]: ");
                    peo.SetRejectMessage("\nĐối tượng chọn phải là Alignment!");
                    peo.AddAllowedClass(typeof(Alignment), exactMatch: true);
                    peo.AllowNone = true;

                    PromptEntityResult per = ed.GetEntity(peo);
                    if (per.Status == PromptStatus.OK)
                    {
                        alignmentId = per.ObjectId;
                        _lastSelectedAlignmentId = alignmentId;
                    }
                    else if (per.Status != PromptStatus.None && per.Status != PromptStatus.Cancel)
                    {
                        return;
                    }
                }

                // Mở Form giao diện chuẩn chuyên nghiệp
                using (var form = new DoiTenCocQuyTacForm(alignmentId))
                {
                    Application.ShowModalDialog(form);
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n❌ Lỗi thực thi lệnh CTS_DoiTenCoc_QuyTac: {ex.Message}\n");
            }
        }
    }
}

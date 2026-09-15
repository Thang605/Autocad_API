// Lệnh: CTS_PhatSinhCoc_TheoBang (Phiên bản cải tiến toàn diện)
// Phát sinh cọc (SampleLine) theo bảng tọa độ / lý trình AutoCAD
// Hỗ trợ tự động nhận diện cột Tên cọc & Lý trình (Km1+785.75), xem trước trực quan và tạo SampleLine
//
using System;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;

using MyFirstProject.Civil_Tool;
using MyFirstProject.Extensions;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using ATable = Autodesk.AutoCAD.DatabaseServices.Table;

[assembly: CommandClass(typeof(Civil3DCsharp.CTS_PhatSinhCoc_TheoBang_Commands))]

namespace Civil3DCsharp
{
    public class CTS_PhatSinhCoc_TheoBang_Commands
    {
        private static ObjectId _lastSelectedTableId = ObjectId.Null;

        [CommandMethod("CTS_PhatSinhCoc_TheoBang")]
        public static void CTSPhatSinhCocTheoBang()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            Editor ed = doc.Editor;
            Database db = doc.Database;

            try
            {
                ed.WriteMessage("\n=== PHÁT SINH CỌC THEO BẢNG TỌA ĐỘ / LÝ TRÌNH (Civil 3D) ===");

                ObjectId tableId = ObjectId.Null;

                // Kiểm tra xem bảng lần trước có còn tồn tại trong bản vẽ hiện tại không
                if (!_lastSelectedTableId.IsNull && _lastSelectedTableId.IsValid && !_lastSelectedTableId.IsErased)
                {
                    tableId = _lastSelectedTableId;
                }
                else
                {
                    // Cho phép người dùng pick bảng trước nếu muốn, hoặc Enter để mở form chọn sau
                    PromptEntityOptions peo = new PromptEntityOptions("\nChọn bảng tọa độ cọc có lý trình (Table) [Hoặc Enter để mở Form]: ");
                    peo.SetRejectMessage("\nĐối tượng chọn phải là bảng AutoCAD Table!");
                    peo.AddAllowedClass(typeof(ATable), exactMatch: true);
                    peo.AllowNone = true;

                    PromptEntityResult per = ed.GetEntity(peo);
                    if (per.Status == PromptStatus.OK)
                    {
                        tableId = per.ObjectId;
                        _lastSelectedTableId = tableId;
                    }
                    else if (per.Status != PromptStatus.None && per.Status != PromptStatus.Cancel)
                    {
                        return;
                    }
                }

                // Mở giao diện Form chuyên nghiệp
                using (var form = new PhatSinhCocTheoBangForm(tableId))
                {
                    Application.ShowModalDialog(form);
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n❌ Lỗi thực thi lệnh CTS_PhatSinhCoc_TheoBang: {ex.Message}");
            }
        }
    }
}

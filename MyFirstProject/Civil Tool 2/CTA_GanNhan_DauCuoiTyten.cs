using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

// AutoCAD
using Autodesk.AutoCAD.Runtime;
using Acad = Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;

// Civil 3D
using Civil = Autodesk.Civil.ApplicationServices;
using Autodesk.Civil;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using Autodesk.Civil.Runtime;
using Autodesk.Civil.Settings;

// Aliases để tránh xung đột
using ATable = Autodesk.AutoCAD.DatabaseServices.Table;
using CivSurface = Autodesk.Civil.DatabaseServices.TinSurface;
using Section = Autodesk.Civil.DatabaseServices.Section;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

// Extensions của project
using MyFirstProject.Extensions;

[assembly: CommandClass(typeof(Civil3DCsharp.CTA_GanNhan_DauCuoiTyten))]

namespace Civil3DCsharp
{
    public class CTA_GanNhan_DauCuoiTyten
    {
        /// <summary>
        /// Lệnh gắn nhãn điểm đầu và điểm cuối tuyến (Alignment)
        /// Sử dụng Alignment.ImportLabelSet để import Label Set vào nhiều Alignment
        /// </summary>
        [CommandMethod("CTA_Gan_Nhan_Dau_Cuoi_Tuyen")]
        public static void GanNhanDauCuoiTyten()
        {
            // 0. Cho phép chọn Alignment trên màn hình
            List<ObjectId> preSelectedAlignmentIds = new List<ObjectId>();

            PromptSelectionOptions pso = new PromptSelectionOptions();
            pso.MessageForAdding = "\nChọn các tuyến đường (Alignment) cần gắn nhãn: ";

            // Filter chỉ chọn Alignment
            SelectionFilter filter = new SelectionFilter(new TypedValue[] {
                new TypedValue((int)DxfCode.Start, "AECC_ALIGNMENT")
            });

            PromptSelectionResult psr = A.Ed.GetSelection(pso, filter);
            if (psr.Status == PromptStatus.OK)
            {
                preSelectedAlignmentIds.AddRange(psr.Value.GetObjectIds());
            }

            // 1. Hiển thị form chọn Label Set Style và Alignments
            var form = new MyFirstProject.DauCuoiTuyenForm(preSelectedAlignmentIds);
            var result = Application.ShowModalDialog(form);

            if (result != System.Windows.Forms.DialogResult.OK || !form.FormAccepted)
            {
                A.Ed.WriteMessage("\n Đã hủy lệnh.");
                return;
            }

            if (form.SelectedLabelSetStyleId == ObjectId.Null)
            {
                A.Ed.WriteMessage("\n Chưa chọn Label Set Style.");
                return;
            }

            if (form.SelectedAlignmentIds.Count == 0)
            {
                A.Ed.WriteMessage("\n Chưa chọn tuyến nào.");
                return;
            }

            // 2. Bắt đầu transaction
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                int successCount = 0;

                // 3. Lặp qua từng Alignment được chọn
                foreach (ObjectId alignmentId in form.SelectedAlignmentIds)
                {
                    try
                    {
                        Alignment? alignment = tr.GetObject(alignmentId, OpenMode.ForWrite) as Alignment;
                        if (alignment == null) continue;

                        // Import Label Set vào Alignment
                        alignment.ImportLabelSet(form.SelectedLabelSetStyleId);

                        double startStation = alignment.StartingStation;
                        double endStation = alignment.EndingStation;

                        A.Ed.WriteMessage($"\n  ✓ {alignment.Name}: Km{startStation / 1000:F3} - Km{endStation / 1000:F3}");
                        successCount++;
                    }
                    catch (System.Exception ex)
                    {
                        A.Ed.WriteMessage($"\n  ✗ Lỗi: {ex.Message}");
                    }
                }

                // 4. Thông báo kết quả
                A.Ed.WriteMessage($"\n\n Hoàn thành! Đã import Label Set cho {successCount}/{form.SelectedAlignmentIds.Count} tuyến.");

                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage($"\n Lỗi: {e.Message}");
            }
        }
    }
}

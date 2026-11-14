using System.Windows.Forms;

using Autodesk.AutoCAD.Runtime;

using MyFirstProject.Shared.Cad;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(MyFirstProject.Commands.CTC.TaoCorridorTheoCauKien.CTC_Tao_Corridor_Theo_Cau_Kien_Command))]

namespace MyFirstProject.Commands.CTC.TaoCorridorTheoCauKien
{
    /// <summary>
    /// Quét danh sách FeatureLine theo Site để xây dựng và cập nhật 1 Corridor tổng (giữ nguyên lý trình Region đã có).
    /// </summary>
    public class CTC_Tao_Corridor_Theo_Cau_Kien_Command
    {
        [CommandMethod("CTC_Tao_Corridor_Theo_Cau_Kien")]
        public static void Execute()
        {
            CommandGuard.Run("CTC_Tao_Corridor_Theo_Cau_Kien", (doc, db, ed) =>
            {
                ed.WriteMessage("\n=========================================================================");
                ed.WriteMessage("\n  CTC_Tao_Corridor_Theo_Cau_Kien: QUÉT FEATURELINE THEO SITE → CORRIDOR TỔNG");
                ed.WriteMessage("\n=========================================================================\n");

                TaoCorridorTheoCauKienOptions options;
                using (var form = new TaoCorridorTheoCauKienForm(db, ed))
                {
                    DialogResult result = Application.ShowModalDialog(form);
                    if (result != DialogResult.OK || !form.FormAccepted)
                    {
                        ed.WriteMessage("\n[Hủy bỏ] Đã hủy thao tác tạo / cập nhật Corridor tổng.");
                        return;
                    }
                    options = form.Options;
                }

                TaoCorridorTheoCauKienService.Execute(db, ed, options);
            });
        }
    }
}

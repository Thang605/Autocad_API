using System.Windows.Forms;

using Autodesk.AutoCAD.Runtime;

using MyFirstProject.Shared.Cad;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(MyFirstProject.Commands.CTC.TaoCorridorTheoCauKien.CTC_Tao_Corridor_Theo_Cau_Kien_Command))]

namespace MyFirstProject.Commands.CTC.TaoCorridorTheoCauKien
{
    /// <summary>
    /// Xây dựng và gộp Corridor theo từng cấu kiện.
    /// File lệnh chỉ mở Form và gọi Service qua CommandGuard.Run.
    /// </summary>
    public class CTC_Tao_Corridor_Theo_Cau_Kien_Command
    {
        [CommandMethod("CTC_Tao_Corridor_Theo_Cau_Kien")]
        public static void Execute()
        {
            CommandGuard.Run("CTC_Tao_Corridor_Theo_Cau_Kien", (doc, db, ed) =>
            {
                ed.WriteMessage("\n==================================================================");
                ed.WriteMessage("\n  CTC_Tao_Corridor_Theo_Cau_Kien: XÂY DỰNG & GỘP CORRIDOR THEO CẤU KIỆN");
                ed.WriteMessage("\n==================================================================\n");

                TaoCorridorTheoCauKienOptions options;
                using (var form = new TaoCorridorTheoCauKienForm(db, ed))
                {
                    DialogResult result = Application.ShowModalDialog(form);
                    if (result != DialogResult.OK || !form.FormAccepted)
                    {
                        ed.WriteMessage("\n[Hủy bỏ] Đã hủy thao tác xây dựng Corridor theo cấu kiện.");
                        return;
                    }
                    options = form.Options;
                }

                TaoCorridorTheoCauKienService.Execute(db, ed, options);
            });
        }
    }
}

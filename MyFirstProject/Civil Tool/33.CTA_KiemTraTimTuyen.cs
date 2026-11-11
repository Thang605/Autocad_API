using System;
using Autodesk.AutoCAD.Runtime;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using MyFirstProject.Menu_form;
using MyFirstProject.Extensions;

[assembly: CommandClass(typeof(Civil3DCsharp.CTA_KiemTraTimTuyen_Cmd))]

namespace Civil3DCsharp
{
    public class CTA_KiemTraTimTuyen_Cmd
    {
        [CommandMethod("CTA_Kiem_Tra_Tim_Tuyen")]
        public void KiemTraTimTuyen()
        {
            try
            {
                var form = new KiemTraTimTuyenForm();
                Application.ShowModalDialog(form);
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi lệnh CTA_KiemTraTimTuyen: {ex.Message}");
            }
        }
    }
}

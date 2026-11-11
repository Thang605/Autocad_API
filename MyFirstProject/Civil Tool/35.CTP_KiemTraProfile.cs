using System;
using Autodesk.AutoCAD.Runtime;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using MyFirstProject.Menu_form;
using MyFirstProject.Extensions;

[assembly: CommandClass(typeof(Civil3DCsharp.CTP_KiemTraProfile_Cmd))]

namespace Civil3DCsharp
{
    /// <summary>
    /// Lệnh kiểm tra Profile (Trắc dọc / Đường đỏ) theo tiêu chuẩn thiết kế đường (TCVN 4054:2005, TCVN 13592:2022, TCVN 5729:2012, TCVN 10380:2014)
    /// </summary>
    public class CTP_KiemTraProfile_Cmd
    {
        [CommandMethod("CTP_Kiem_Tra_Profile")]
        public void KiemTraProfile()
        {
            try
            {
                var form = new KiemTraProfileForm();
                Application.ShowModalDialog(form);
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi lệnh CTP_KiemTraProfile: {ex.Message}");
            }
        }
    }
}

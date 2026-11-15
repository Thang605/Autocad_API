using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using ClosedXML.Excel;
using MyFirstProject.Extensions;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using CivSurface = Autodesk.Civil.DatabaseServices.TinSurface;

[assembly: CommandClass(typeof(Civil3DCsharp.CTA_KiemTraTamNhin_Cmd))]

namespace Civil3DCsharp
{
    public class CTA_KiemTraTamNhin_Cmd
    {
        [CommandMethod("CTA_Kiem_Tra_Tam_Nhin")]
        public static void KiemTraTamNhin()
        {
            try
            {
                var form = new MyFirstProject.Civil_Tool.KiemTraTamNhinForm();
                Application.ShowModalDialog(form);
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi lệnh CTA_KiemTraTamNhin: {ex.Message}");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using CadEntity = Autodesk.AutoCAD.DatabaseServices.Entity;
using CivSurface = Autodesk.Civil.DatabaseServices.TinSurface;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;
using DocumentCollectionEventArgs = Autodesk.AutoCAD.ApplicationServices.DocumentCollectionEventArgs;

[assembly: CommandClass(typeof(MyFirstProject.Civil_Tool_2.CTT_Tao_Mat_Phang_Tu_Featureline_Command))]

namespace MyFirstProject.Civil_Tool_2
{
    public partial class CTT_Tao_Mat_Phang_Tu_Featureline_Command
    {
        [CommandMethod("CTT_Tao_Mat_Phang_Tu_Feature_Line")]
        public static void TaoMatPhangTuFeatureline()
        {
            FeatureLineDynamicSyncManager.EnsureStarted();
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            Editor ed = doc.Editor;
            Database db = doc.Database;

            try
            {
                using (var form = new TaoMatPhangTuFeaturelineForm(db, ed))
                {
                    Application.ShowModalDialog(form);
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n[LỖI] Không thể mở giao diện tạo mặt phẳng từ Feature Line: {ex.Message}");
            }
        }
    }
}

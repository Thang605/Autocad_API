// (C) Copyright 2015 by  
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Autodesk.AutoCAD.Runtime;
using Acad = Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;

using Civil = Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using Autodesk.Civil.Runtime;
using Autodesk.Civil.Settings;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.Civil.ApplicationServices;
using Section = Autodesk.Civil.DatabaseServices.Section;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using MyFirstProject.Extensions;

// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3DCsharp.CTSV_HieuChinh))]

namespace Civil3DCsharp
{
    public class CTSV_HieuChinh
    {

        [CommandMethod("CTS_Hieu_Chinh_Section")]
        public static void CTSVHieuChinhSectionStatic()
        {
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                //start here
                ObjectIdCollection sectionViewIdColl = UserInput.GSelectionSetWithType("Chọn các section cần chuyển sang Static: \n", "AECC_SECTION");

                if (sectionViewIdColl == null || sectionViewIdColl.Count == 0)
                {
                    A.Ed.WriteMessage("\nKhông có section nào được chọn.");
                    return;
                }

                foreach (ObjectId sectionId in sectionViewIdColl)
                {
                    Section? section = tr.GetObject(sectionId, OpenMode.ForWrite) as Section;
                    if (section == null)
                    {
                        A.Ed.WriteMessage($"\nCảnh báo: Không thể đọc Section (ObjectId: {sectionId}). Bỏ qua...");
                        continue;
                    }
                    section.UpdateMode = SectionUpdateType.Static;
                }

                tr.Commit();
                A.Ed.Regen();
                A.Ed.WriteMessage($"\nĐã chuyển {sectionViewIdColl.Count} section sang Static thành công.");
            }
            catch (System.Exception e)
            {
                A.Ed.WriteMessage($"\nLỗi: {e.Message}");
            }
        }

        [CommandMethod("CTS_Hieu_Chinh_Section_Dynamic")]
        public static void CTSVHieuChinhSectionDynamic()
        {
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                //start here
                ObjectIdCollection sectionViewIdColl = UserInput.GSelectionSetWithType("Chọn các section cần chuyển sang Dynamic: \n", "AECC_SECTION");

                if (sectionViewIdColl == null || sectionViewIdColl.Count == 0)
                {
                    A.Ed.WriteMessage("\nKhông có section nào được chọn.");
                    return;
                }

                foreach (ObjectId sectionId in sectionViewIdColl)
                {
                    Section? section = tr.GetObject(sectionId, OpenMode.ForWrite) as Section;
                    if (section == null)
                    {
                        A.Ed.WriteMessage($"\nCảnh báo: Không thể đọc Section (ObjectId: {sectionId}). Bỏ qua...");
                        continue;
                    }
                    section.UpdateMode = SectionUpdateType.Dynamic;
                }

                tr.Commit();
                A.Ed.Regen();
                A.Ed.WriteMessage($"\nĐã chuyển {sectionViewIdColl.Count} section sang Dynamic thành công.");
            }
            catch (System.Exception e)
            {
                A.Ed.WriteMessage($"\nLỗi: {e.Message}");
            }
        }

    }
}

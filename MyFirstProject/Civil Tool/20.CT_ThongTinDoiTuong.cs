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
using ATable = Autodesk.AutoCAD.DatabaseServices.Table;

using Civil = Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using Autodesk.Civil.Runtime;
using Autodesk.Civil.Settings;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.Civil.ApplicationServices;
using CivSurface = Autodesk.Civil.DatabaseServices.TinSurface;
using Section = Autodesk.Civil.DatabaseServices.Section;
using Autodesk.Civil;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using MyFirstProject.Extensions;

// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3DCsharp.CT_ThongTinDoiTuong_Commands))]

namespace Civil3DCsharp
{
    public class CT_ThongTinDoiTuong_Commands
    {
        [CommandMethod("CT_Thong_Tin_Doi_Tuong")]
        public static void CTThongTinDoiTuong()
        {
            // start transaction
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();

                A.Ed.WriteMessage("\nLệnh truy xuất thông tin Material Section...");

                // Step 1: Choose material section using proper DXF filter
                A.Ed.WriteMessage("\nChọn material section để xem thông tin:");
                
                // Define filter for material section objects
                TypedValue[] filter = new TypedValue[]
                {
                    new TypedValue((int)DxfCode.Start, "AECC_QUANTITY_TAKEOFF_MATERIAL_SECTION")
                };
                SelectionFilter selFilter = new SelectionFilter(filter);

                // Prompt for selection with filter
                PromptSelectionOptions pso = new()
                {
                    MessageForAdding = "\nChọn material section: ",
                    AllowDuplicates = false,
                    SingleOnly = true
                };

                PromptSelectionResult psr = A.Ed.GetSelection(pso, selFilter);
                if (psr.Status != PromptStatus.OK)
                {
                    A.Ed.WriteMessage("\nĐã hủy lệnh hoặc không chọn được material section.");
                    return;
                }

                ObjectId[] selectedIds = psr.Value.GetObjectIds();
                if (selectedIds.Length == 0)
                {
                    A.Ed.WriteMessage("\nKhông có material section nào được chọn.");
                    return;
                }

                ObjectId materialSectionId = selectedIds[0];
                A.Ed.WriteMessage($"\nĐã chọn material section với ObjectId: {materialSectionId}");

                //material section open for read
                MaterialSection? materialSection = tr.GetObject(materialSectionId, OpenMode.ForRead) as MaterialSection;
                if (materialSection == null)
                {
                    A.Ed.WriteMessage("\nKhông thể mở material section.");
                    return;
                }
                // Step 2: Extract and display information
                A.Ed.WriteMessage($"\nThông tin Material Section:");
                A.Ed.WriteMessage($"\n- Name: {materialSection.Name}");
                A.Ed.WriteMessage($"\n- Description: {materialSection.Station}");
                A.Ed.WriteMessage($"\n- Style: {materialSection.StyleName}");
                A.Ed.WriteMessage($"\n- SectionPoints: {materialSection.SectionPoints}");
                A.Ed.WriteMessage($"\n- Area: {materialSection.Area}");
                A.Ed.WriteMessage($"\n- SourceName: {materialSection.SourceName}");
                A.Ed.WriteMessage($"\n- Material Name: {materialSection.Material}");
                A.Ed.WriteMessage($"\n- MaterialMapper: {materialSection.MaterialMapper}");

                ObjectId materialId = materialSection.MaterialId;
                if (materialId != ObjectId.Null)
                {
                    Material? material = tr.GetObject(materialId, OpenMode.ForRead) as Material;
                    if (material != null)
                    {
                        A.Ed.WriteMessage($"\n- Material Name: {material.Name}");
                        A.Ed.WriteMessage($"\n- Material Description: {material.Description}");
                    }
                    else
                    {
                        A.Ed.WriteMessage("\n- Không thể mở vật liệu liên kết.");
                    }
                }
                else
                {
                    A.Ed.WriteMessage("\n- Không có vật liệu liên kết.");
                }





                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage($"\nLỗi AutoCAD: {e.Message}");
                A.Ed.WriteMessage($"\nError Code: {e.ErrorStatus}");
                tr.Abort();
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi hệ thống: {ex.Message}");
                tr.Abort();
            }
        }
    }
}

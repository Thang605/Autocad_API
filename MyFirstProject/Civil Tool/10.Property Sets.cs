// (C) Copyright 2015 by  
//
using Autodesk.Aec.PropertyData.DatabaseServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using MyFirstProject.Extensions;

// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3D_Csharp.PropertySets))]

namespace Civil3D_Csharp
{
    public class PropertySets
    {
        [CommandMethod("AT_Solid_Set_PropertySet")]
        public static void SetSolidPropertySet()
        {
            _ = new UserInput();
            _ = new PropertySetUtils();
            var selectedIds = UserInput.GSelectionSetWithType("\nChọn các đối tượng 3D Solid cần thiết lập Property Set: ", "3DSOLID");

            using var tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                foreach (ObjectId objectId in selectedIds)
                {
                    var solid = tr.GetObject(objectId, OpenMode.ForWrite) as Solid3d;
                    if (solid == null) continue;

                    PropertySetUtils.SetupSolidWithCalculatedProperties(tr, solid);
                }

                tr.Commit();
                A.Ed.WriteMessage($"\nĐã xử lý {selectedIds.Count} đối tượng 3D Solid.");
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi: {ex.Message}");
                tr.Abort();
            }
        }

        [CommandMethod("AT_Solid_Show_Info")]
        public static void ShowSolidInfo()
        {
            _ = new PropertySetUtils();
            var selectedId = UserInput.GSelectionAnObject("\nChọn một đối tượng 3D Solid để xem thông tin: ");

            using var tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                var solid = tr.GetObject(selectedId, OpenMode.ForRead) as Solid3d;
                if (solid == null)
                {
                    A.Ed.WriteMessage("\nĐối tượng được chọn không phải là 3D Solid.");
                    return;
                }

                PropertySetUtils.ShowSolidPropertySetInfo(tr, solid);
                tr.Commit();
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi: {ex.Message}");
                tr.Abort();
            }
        }

        [CommandMethod("AT_Solid_Update_PropertySet")]
        public static void UpdateSolidPropertySet()
        {
            try
            {
                // 1. Quét ban đầu các 3D Solid và Body theo Layer
                List<LayerPropertyMapping> mappings;
                using (var tr = A.Db.TransactionManager.StartTransaction())
                {
                    mappings = PropertySetUtils.ScanSolidsAndBodies(tr);
                    tr.Commit();
                }

                if (mappings.Count == 0)
                {
                    A.Ed.WriteMessage("\nKhông tìm thấy đối tượng 3D Solid hoặc 3D Body nào trong ModelSpace.");
                }

                // 2. Tạo và mở Form
                var form = new UpdatePropertySetForm(mappings);

                // Callback quét lại từ CAD
                form.OnRescanCad = () =>
                {
                    using var trRescan = A.Db.TransactionManager.StartTransaction();
                    var list = PropertySetUtils.ScanSolidsAndBodies(trRescan);
                    trRescan.Commit();
                    return list;
                };

                // Callback thực thi Apply Property Set
                form.OnApplyPropertySet = (propSetName, selectedMappings) =>
                {
                    using var trApply = A.Db.TransactionManager.StartTransaction();
                    try
                    {
                        var timer = System.Diagnostics.Stopwatch.StartNew();
                        int updatedCount = PropertySetUtils.ApplyPropertySetsByMapping(trApply, propSetName, selectedMappings);
                        trApply.Commit();
                        timer.Stop();

                        A.Ed.WriteMessage($"\n[AT_Solid_Update_PropertySet] Đã cập nhật Property Set '{propSetName}' thành công cho {updatedCount} đối tượng 3D Solid / Body trong {timer.ElapsedMilliseconds} ms ({timer.Elapsed.TotalSeconds:F2}s).");
                        System.Windows.Forms.MessageBox.Show(
                            $"Đã cập nhật Property Set '{propSetName}' thành công cho {updatedCount} đối tượng!",
                            "Thành công",
                            System.Windows.Forms.MessageBoxButtons.OK,
                            System.Windows.Forms.MessageBoxIcon.Information);
                    }
                    catch (System.Exception ex)
                    {
                        trApply.Abort();
                        A.Ed.WriteMessage($"\nLỗi khi cập nhật Property Set: {ex.Message}");
                        throw;
                    }
                };

                Application.ShowModalDialog(form);
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi khi chạy lệnh AT_Solid_Update_PropertySet: {ex.Message}");
            }
        }
    }
}



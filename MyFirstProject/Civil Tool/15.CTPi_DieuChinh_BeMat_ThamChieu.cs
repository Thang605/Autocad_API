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
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using MyFirstProject.Extensions;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using AcadEntity = Autodesk.AutoCAD.DatabaseServices.Entity;

// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3DCsharp.CTPi_DieuChinh_BeMat_ThamChieu_Commands))]

namespace Civil3DCsharp
{
    public class CTPi_DieuChinh_BeMat_ThamChieu_Commands
    {
        [CommandMethod("CT_Dieu_Chinh_Be_Mat_Tham_Chieu")]
        public static void CTPiDieuChinhBeMatThamChieu()
        {
            // start transaction
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();

                A.Ed.WriteMessage("\nĐiều chỉnh bề mặt tham chiếu cho pipe và structure...");
                A.Ed.WriteMessage("\nLệnh này sẽ thay đổi bề mặt tham chiếu (reference surface) cho các đối tượng được chọn.\n");

                // Step 1: Select pipes and structures
                A.Ed.WriteMessage("\nChọn các pipe và structure cần điều chỉnh bề mặt tham chiếu:");
                
                PromptSelectionOptions pso = new PromptSelectionOptions
                {
                    MessageForAdding = "\nChọn pipe và structure: ",
                    AllowDuplicates = false
                };

                // Create selection filter for pipes and structures
                TypedValue[] filterList = new TypedValue[]
                {
                    new TypedValue((int)DxfCode.Operator, "<OR"),
                    new TypedValue((int)DxfCode.Start, "AECC_PIPE"),
                    new TypedValue((int)DxfCode.Start, "AECC_STRUCTURE"),
                    new TypedValue((int)DxfCode.Operator, "OR>")
                };
                SelectionFilter filter = new SelectionFilter(filterList);

                PromptSelectionResult psr = A.Ed.GetSelection(pso, filter);
                if (psr.Status != PromptStatus.OK)
                {
                    A.Ed.WriteMessage("\nĐã hủy lệnh hoặc không chọn được đối tượng.");
                    return;
                }

                ObjectId[] selectedIds = psr.Value.GetObjectIds();
                if (selectedIds.Length == 0)
                {
                    A.Ed.WriteMessage("\nKhông có pipe hoặc structure nào được chọn.");
                    return;
                }

                // Step 2: Analyze selected objects
                List<PipeStructureInfo> objectInfos = new List<PipeStructureInfo>();

                foreach (ObjectId objId in selectedIds)
                {
                    try
                    {
                        AcadEntity? entity = tr.GetObject(objId, OpenMode.ForRead) as AcadEntity;
                        if (entity != null)
                        {
                            string objectType = "";
                            string objectName = "";
                            ObjectId currentRefSurfaceId = ObjectId.Null;

                            if (entity is Pipe pipe)
                            {
                                objectType = "Pipe";
                                objectName = pipe.Name ?? "Unnamed Pipe";
                                currentRefSurfaceId = pipe.RefSurfaceId;
                            }
                            else if (entity is Structure structure)
                            {
                                objectType = "Structure";
                                objectName = structure.Name ?? "Unnamed Structure";
                                currentRefSurfaceId = structure.RefSurfaceId;
                            }

                            if (!string.IsNullOrEmpty(objectType))
                            {
                                var info = new PipeStructureInfo
                                {
                                    ObjectId = objId,
                                    ObjectType = objectType,
                                    Name = objectName,
                                    CurrentRefSurfaceId = currentRefSurfaceId
                                };
                                objectInfos.Add(info);
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        A.Ed.WriteMessage($"\nLỗi đọc đối tượng: {ex.Message}");
                        continue;
                    }
                }

                if (objectInfos.Count == 0)
                {
                    A.Ed.WriteMessage("\nKhông có pipe hoặc structure nào hợp lệ được chọn.");
                    return;
                }

                A.Ed.WriteMessage($"\nĐã chọn {objectInfos.Count} đối tượng:");
                for (int i = 0; i < objectInfos.Count; i++)
                {
                    var info = objectInfos[i];
                    string currentSurfaceName = "Không có";
                    
                    if (info.CurrentRefSurfaceId != ObjectId.Null)
                    {
                        try
                        {
                            var surface = tr.GetObject(info.CurrentRefSurfaceId, OpenMode.ForRead) as CivSurface;
                            currentSurfaceName = surface?.Name ?? "Không xác định";
                        }
                        catch
                        {
                            currentSurfaceName = "Lỗi đọc surface";
                        }
                    }
                    
                    A.Ed.WriteMessage($"\n  {i + 1}. {info.ObjectType}: {info.Name} (Surface hiện tại: {currentSurfaceName})");
                }

                // Step 3: Select new reference surface
                A.Ed.WriteMessage("\nChọn bề mặt tham chiếu mới:");
                ObjectId newRefSurfaceId = UserInput.GSurfaceId("Chọn surface để làm bề mặt tham chiếu: ");
                
                if (newRefSurfaceId == ObjectId.Null)
                {
                    A.Ed.WriteMessage("\nKhông có surface nào được chọn. Đã hủy lệnh.");
                    return;
                }

                // Get surface name for confirmation
                string newSurfaceName = "Không xác định";
                try
                {
                    var newSurface = tr.GetObject(newRefSurfaceId, OpenMode.ForRead) as CivSurface;
                    newSurfaceName = newSurface?.Name ?? "Không xác định";
                }
                catch (System.Exception ex)
                {
                    A.Ed.WriteMessage($"\nLỗi đọc surface: {ex.Message}");
                    return;
                }

                A.Ed.WriteMessage($"\nBề mặt tham chiếu mới: {newSurfaceName}");

                // Step 4: Confirm before proceeding
                PromptKeywordOptions pko = new PromptKeywordOptions($"\nXác nhận thay đổi bề mặt tham chiếu cho {objectInfos.Count} đối tượng thành '{newSurfaceName}'?")
                {
                    Message = "\nBạn có chắc chắn muốn thay đổi? [Yes/No]: "
                };
                pko.Keywords.Add("Yes");
                pko.Keywords.Add("No");
                pko.Keywords.Default = "Yes";

                PromptResult pkr = A.Ed.GetKeywords(pko);
                if (pkr.Status != PromptStatus.OK || pkr.StringResult.Equals("No", StringComparison.OrdinalIgnoreCase))
                {
                    A.Ed.WriteMessage("\nĐã hủy lệnh.");
                    return;
                }

                // Step 5: Process each object
                int successCount = 0;
                int failureCount = 0;

                foreach (var objectInfo in objectInfos)
                {
                    try
                    {
                        if (UpdateReferenceSurface(objectInfo, newRefSurfaceId, tr))
                        {
                            successCount++;
                        }
                        else
                        {
                            failureCount++;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        A.Ed.WriteMessage($"\nLỗi cập nhật {objectInfo.ObjectType} '{objectInfo.Name}': {ex.Message}");
                        failureCount++;
                    }
                }

                A.Ed.WriteMessage($"\n\nKết quả thay đổi bề mặt tham chiếu:");
                A.Ed.WriteMessage($"\n- Thành công: {successCount} đối tượng");
                A.Ed.WriteMessage($"\n- Thất bại: {failureCount} đối tượng");
                A.Ed.WriteMessage($"\nĐã hoàn thành điều chỉnh bề mặt tham chiếu thành '{newSurfaceName}'.");

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

        private static bool UpdateReferenceSurface(PipeStructureInfo objectInfo, ObjectId newRefSurfaceId, Transaction tr)
        {
            try
            {
                AcadEntity? entity = tr.GetObject(objectInfo.ObjectId, OpenMode.ForWrite) as AcadEntity;
                if (entity == null)
                {
                    A.Ed.WriteMessage($"\nKhông thể mở {objectInfo.ObjectType} '{objectInfo.Name}' để chỉnh sửa.");
                    return false;
                }

                bool updateSuccess = false;
                string oldSurfaceName = "Không có";
                
                // Get old surface name for logging
                if (objectInfo.CurrentRefSurfaceId != ObjectId.Null)
                {
                    try
                    {
                        var oldSurface = tr.GetObject(objectInfo.CurrentRefSurfaceId, OpenMode.ForRead) as CivSurface;
                        oldSurfaceName = oldSurface?.Name ?? "Không xác định";
                    }
                    catch
                    {
                        oldSurfaceName = "Lỗi đọc";
                    }
                }

                if (entity is Pipe pipe)
                {
                    pipe.RefSurfaceId = newRefSurfaceId;
                    updateSuccess = true;
                }
                else if (entity is Structure structure)
                {
                    structure.RefSurfaceId = newRefSurfaceId;
                    updateSuccess = true;
                }

                if (updateSuccess)
                {
                    A.Ed.WriteMessage($"\n- {objectInfo.ObjectType}: '{objectInfo.Name}'");
                    A.Ed.WriteMessage($"  + Bề mặt cũ: {oldSurfaceName}");
                    A.Ed.WriteMessage($"  + Đã cập nhật bề mặt tham chiếu mới");
                    return true;
                }
                else
                {
                    A.Ed.WriteMessage($"\n- Lỗi: Không thể cập nhật {objectInfo.ObjectType} '{objectInfo.Name}' - Loại đối tượng không được hỗ trợ");
                    return false;
                }
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi trong quá trình xử lý '{objectInfo.Name}': {ex.Message}");
                return false;
            }
        }

        // Helper class to store pipe and structure information
        private class PipeStructureInfo
        {
            public ObjectId ObjectId { get; set; }
            public string ObjectType { get; set; } = "";
            public string Name { get; set; } = "";
            public ObjectId CurrentRefSurfaceId { get; set; }
        }
    }
}

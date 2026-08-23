using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.Civil.DatabaseServices;
using MyFirstProject.Extensions;
using MyFirstProject.Civil_Tool;

// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3DCsharp.CTPO_ThemMoTa_CogoPoint_Commands))]

namespace Civil3DCsharp
{
    class CTPO_ThemMoTa_CogoPoint_Commands
    {
        [CommandMethod("CTPO_ThemMoTa_CogoPoint")]
        public static void CTPO_ThemMoTa_CogoPoint()
        {
            // PHASE 1: Show description form
            using (ThemMoTaCogoPointForm form = new ThemMoTaCogoPointForm())
            {
                DialogResult result = Autodesk.AutoCAD.ApplicationServices.Application
                    .ShowModalDialog(form);

                if (result != DialogResult.OK || !form.FormAccepted)
                {
                    A.Ed.WriteMessage("\nĐã hủy lệnh thêm mô tả CogoPoint.");
                    return;
                }

                string description = form.GenerateDescription();
                bool usePointNameAsMiddle = string.IsNullOrEmpty(form.PointName);

                A.Ed.WriteMessage($"\nMô tả sẽ gán: {description}");
                if (usePointNameAsMiddle)
                    A.Ed.WriteMessage("\n(Sẽ dùng PointName hiện có của mỗi cọc làm phần tên cọc)");

                A.Ed.WriteMessage("\nChọn các CogoPoint cần thêm mô tả (quét chọn nhiều điểm)...");

                // PHASE 2: Select CogoPoints
                PromptSelectionOptions pso = new PromptSelectionOptions();
                pso.MessageForAdding = "\n Chọn các CogoPoint: ";
                pso.AllowDuplicates = false;

                // Filter only CogoPoint objects
                TypedValue[] filterList = new TypedValue[]
                {
                    new TypedValue((int)DxfCode.Start, "AECC_COGO_POINT")
                };
                SelectionFilter filter = new SelectionFilter(filterList);

                PromptSelectionResult selResult = A.Ed.GetSelection(pso, filter);

                if (selResult.Status != PromptStatus.OK || selResult.Value == null)
                {
                    A.Ed.WriteMessage("\nKhông có CogoPoint nào được chọn.");
                    return;
                }

                ObjectId[] selectedIds = selResult.Value.GetObjectIds();

                if (selectedIds.Length == 0)
                {
                    A.Ed.WriteMessage("\nKhông có CogoPoint nào được chọn.");
                    return;
                }

                A.Ed.WriteMessage($"\nĐã chọn {selectedIds.Length} CogoPoint. Đang xử lý...");

                // PHASE 3: Apply description
                int processedCount = 0;

                using (Transaction tr = A.Db.TransactionManager.StartTransaction())
                {
                    try
                    {
                        // Sort by PointNumber for consistent ordering
                        var sortedPoints = new List<(ObjectId Id, uint PointNumber)>();
                        foreach (ObjectId id in selectedIds)
                        {
                            CogoPoint? cp = tr.GetObject(id, OpenMode.ForWrite) as CogoPoint;
                            if (cp != null)
                            {
                                sortedPoints.Add((id, cp.PointNumber));
                            }
                        }
                        sortedPoints = sortedPoints.OrderBy(p => p.PointNumber).ToList();

                        foreach (var item in sortedPoints)
                        {
                            CogoPoint? cogoPoint = tr.GetObject(item.Id, OpenMode.ForWrite) as CogoPoint;
                            if (cogoPoint != null)
                            {
                                string newDescription;

                                if (usePointNameAsMiddle)
                                {
                                    // Use existing PointName as the middle part
                                    newDescription = form.GenerateDescriptionWithCustomName(cogoPoint.PointName);
                                }
                                else
                                {
                                    // Use the fixed description from form
                                    newDescription = description;
                                }

                                string oldDescription = cogoPoint.RawDescription ?? "";

                                if (form.OverwriteExisting)
                                {
                                    // Overwrite mode
                                    cogoPoint.RawDescription = newDescription;
                                }
                                else
                                {
                                    // Append mode
                                    if (string.IsNullOrEmpty(oldDescription))
                                    {
                                        cogoPoint.RawDescription = newDescription;
                                    }
                                    else
                                    {
                                        cogoPoint.RawDescription = oldDescription + " " + newDescription;
                                    }
                                }

                                A.Ed.WriteMessage($"\n  Point #{cogoPoint.PointNumber} [{cogoPoint.PointName}]: \"{oldDescription}\" → \"{cogoPoint.RawDescription}\"");
                                processedCount++;
                            }
                        }

                        tr.Commit();
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception ex)
                    {
                        A.Ed.WriteMessage($"\nLỗi: {ex.Message}");
                        tr.Abort();
                    }
                    catch (System.Exception ex)
                    {
                        A.Ed.WriteMessage($"\nLỗi không xác định: {ex.Message}");
                        tr.Abort();
                    }
                }

                // PHASE 4: Update point groups and summary
                if (processedCount > 0)
                {
                    using (Transaction tr = A.Db.TransactionManager.StartTransaction())
                    {
                        try
                        {
                            UtilitiesC3D.UpdateAllPointGroup();
                            tr.Commit();
                        }
                        catch
                        {
                            tr.Abort();
                        }
                    }

                    A.Ed.Regen();
                    A.Ed.WriteMessage($"\n\nHoàn thành! Đã thêm mô tả cho {processedCount} CogoPoint.");
                }
                else
                {
                    A.Ed.WriteMessage("\nKhông có CogoPoint nào được cập nhật mô tả.");
                }
            }
        }
    }
}

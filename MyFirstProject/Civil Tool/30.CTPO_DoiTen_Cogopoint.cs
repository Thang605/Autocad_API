using Autodesk.AutoCAD.Runtime;
using System;
using System.Windows.Forms;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.Civil.DatabaseServices;
using MyFirstProject.Extensions;
using MyFirstProject.Civil_Tool;

// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3DCsharp.CTPO_DoiTen_Cogopoint_Commands))]

namespace Civil3DCsharp
{
    class CTPO_DoiTen_Cogopoint_Commands
    {
        [CommandMethod("CT_Doi_Ten_Cogo_Point")]
        public static void CTPO_DoiTen_Cogopoint()
        {
            // Show the Name Template form
            using (DoiTenCogopointForm form = new DoiTenCogopointForm())
            {
                // Show dialog
                DialogResult result = Autodesk.AutoCAD.ApplicationServices.Application
                    .ShowModalDialog(form);

                if (result != DialogResult.OK || !form.FormAccepted)
                {
                    A.Ed.WriteMessage("\nĐã hủy lệnh đổi tên CogoPoint.");
                    return;
                }

                // Get template settings
                int counter = form.StartingNumber;
                int increment = form.IncrementValue;
                int processedCount = 0;

                A.Ed.WriteMessage($"\nTemplate: {form.NameTemplate}");
                A.Ed.WriteMessage("\nChọn từng CogoPoint để đổi tên (ESC để kết thúc)...");

                // Loop to pick points one by one
                while (true)
                {
                    // Prompt to select a single CogoPoint
                    PromptEntityOptions peo = new PromptEntityOptions("\n Chọn CogoPoint cần đổi tên (ESC để kết thúc): ");
                    peo.SetRejectMessage("\n- Bạn phải chọn đúng đối tượng CogoPoint!");
                    peo.AddAllowedClass(typeof(CogoPoint), true);
                    peo.AllowNone = true;

                    PromptEntityResult per = A.Ed.GetEntity(peo);

                    // Check if user pressed ESC or cancelled
                    if (per.Status == PromptStatus.Cancel || per.Status == PromptStatus.None)
                    {
                        break;
                    }

                    if (per.Status != PromptStatus.OK)
                    {
                        continue;
                    }

                    // Process the selected CogoPoint
                    using (Transaction tr = A.Db.TransactionManager.StartTransaction())
                    {
                        try
                        {
                            CogoPoint? cogoPoint = tr.GetObject(per.ObjectId, OpenMode.ForWrite) as CogoPoint;
                            if (cogoPoint != null)
                            {
                                // Generate new name based on template
                                string newName = form.GenerateName(
                                    counter,
                                    cogoPoint.PointNumber,
                                    cogoPoint.PointName,
                                    cogoPoint.RawDescription ?? "",
                                    cogoPoint.Easting,
                                    cogoPoint.Northing,
                                    cogoPoint.Elevation
                                );

                                string oldName = cogoPoint.PointName;

                                // Set new name
                                cogoPoint.PointName = newName;

                                // Commit immediately so user can see the change
                                tr.Commit();

                                // Show feedback
                                A.Ed.WriteMessage($"\n  [{oldName}] → [{newName}]");

                                counter += increment;
                                processedCount++;

                                // Regenerate display to show updated name
                                A.Ed.Regen();
                            }
                            else
                            {
                                tr.Abort();
                            }
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
                }

                // Final summary
                if (processedCount > 0)
                {
                    // Update all point groups at the end
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

                    A.Ed.WriteMessage($"\n\nHoàn thành! Đã đổi tên {processedCount} CogoPoint.");
                }
                else
                {
                    A.Ed.WriteMessage("\nKhông có CogoPoint nào được đổi tên.");
                }
            }
        }
    }
}

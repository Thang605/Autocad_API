using Autodesk.AutoCAD.Runtime;
using System;
using System.Windows.Forms;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.Civil.DatabaseServices;
using MyFirstProject.Extensions;
using MyFirstProject.Civil_Tool_2;
// Alias để tránh xung đột với class Civil3DCsharp.Parcel
using CivilParcel = Autodesk.Civil.DatabaseServices.Parcel;

[assembly: CommandClass(typeof(Civil3DCsharp.CTPA_DoiTen_Parcel_Commands))]

namespace Civil3DCsharp
{
    class CTPA_DoiTen_Parcel_Commands
    {
        /// <summary>
        /// Lệnh đổi tên Parcel theo mẫu prefix + số thứ tự
        /// Cho phép chọn từng Parcel và xem tên thay đổi ngay lập tức
        /// </summary>
        [CommandMethod("CT_Doi_Ten_Parcel")]
        public static void CTPA_DoiTen_Parcel()
        {
            // Show the Name Template form
            using (DoiTenParcelForm form = new DoiTenParcelForm())
            {
                // Show dialog
                DialogResult result = Autodesk.AutoCAD.ApplicationServices.Application
                    .ShowModalDialog(form);

                if (result != DialogResult.OK || !form.FormAccepted)
                {
                    A.Ed.WriteMessage("\nĐã hủy lệnh đổi tên Parcel.");
                    return;
                }

                // Get template settings
                int counter = form.StartingNumber;
                int increment = form.IncrementValue;
                int processedCount = 0;

                A.Ed.WriteMessage($"\nMẫu tên: {form.NameTemplate}");
                A.Ed.WriteMessage("\nChọn từng Parcel để đổi tên (ESC để kết thúc)...");

                // Loop to pick Parcels one by one
                while (true)
                {
                    // Prompt to select a single Parcel
                    PromptEntityOptions peo = new PromptEntityOptions("\nChọn Parcel cần đổi tên (ESC để kết thúc): ");
                    peo.SetRejectMessage("\n- Bạn phải chọn đúng đối tượng Parcel!");
                    peo.AddAllowedClass(typeof(CivilParcel), true);
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

                    // Process the selected Parcel
                    using (Transaction tr = A.Db.TransactionManager.StartTransaction())
                    {
                        try
                        {
                            CivilParcel? parcel = tr.GetObject(per.ObjectId, OpenMode.ForWrite) as CivilParcel;
                            if (parcel != null)
                            {
                                // Get current parcel properties
                                string oldName = parcel.Name;
                                int parcelNumber = (int)parcel.Number;
                                double area = parcel.Area;

                                // Generate new name based on template
                                string newName = form.GenerateName(
                                    counter,
                                    oldName,
                                    parcelNumber,
                                    area
                                );

                                // Set new name
                                parcel.Name = newName;

                                // Commit immediately so user can see the change
                                tr.Commit();

                                // Show feedback
                                A.Ed.WriteMessage($"\n  [{oldName}] → [{newName}]");
                                A.Ed.WriteMessage($"    (Diện tích: {area:F2} m²)");


                                counter += increment;
                                processedCount++;
                            }
                            else
                            {
                                tr.Abort();
                            }
                        }
                        catch (Autodesk.AutoCAD.Runtime.Exception ex)
                        {
                            A.Ed.WriteMessage($"\nLỗi AutoCAD: {ex.Message}");
                            tr.Abort();
                        }
                        catch (Autodesk.Civil.CivilException ex)
                        {
                            A.Ed.WriteMessage($"\nLỗi Civil 3D: {ex.Message}");
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
                    A.Ed.WriteMessage($"\n\n======================================");
                    A.Ed.WriteMessage($"\nHoàn thành! Đã đổi tên {processedCount} Parcel.");
                    A.Ed.WriteMessage($"\n======================================");
                }
                else
                {
                    A.Ed.WriteMessage("\nKhông có Parcel nào được đổi tên.");
                }
            }
        }

    }
}

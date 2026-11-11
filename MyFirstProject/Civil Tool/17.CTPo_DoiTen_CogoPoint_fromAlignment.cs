using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using System.Windows.Forms;
using MyFirstProject.Extensions;

// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3DCsharp.CTPo_DoiTen_CogoPoint_fromAlignment_Commands))]

namespace Civil3DCsharp
{
    class CTPo_DoiTen_CogoPoint_fromAlignment_Commands
    {
        [CommandMethod("CT_Doi_Ten_Cogo_Point_Tu_Alignment")]
        public static void CTPo_DoiTen_CogoPoint_fromAlignment()
        {
            // Start transaction
            using (Transaction tr = A.Db.TransactionManager.StartTransaction())
            {
                try
                {
                    UserInput UI = new();
                    UtilitiesCAD CAD = new();
                    UtilitiesC3D C3D = new();

                    // Get first alignment
                    ObjectId alignment1Id = UserInput.GAlignmentId("\n Chọn alignment thứ nhất:");
                    if (alignment1Id.IsNull)
                    {
                        A.Ed.WriteMessage("\nKhông có alignment nào được chọn!");
                        return;
                    }

                    // Get second alignment
                    ObjectId alignment2Id = UserInput.GAlignmentId("\n Chọn alignment thứ hai:");
                    if (alignment2Id.IsNull)
                    {
                        A.Ed.WriteMessage("\nKhông có alignment nào được chọn!");
                        return;
                    }

                    // Get alignment objects
                    Alignment? alignment1 = tr.GetObject(alignment1Id, OpenMode.ForRead) as Alignment;
                    Alignment? alignment2 = tr.GetObject(alignment2Id, OpenMode.ForRead) as Alignment;

                    if (alignment1 == null || alignment2 == null)
                    {
                        A.Ed.WriteMessage("\nLỗi: Không thể lấy thông tin alignment!");
                        return;
                    }

                    // Get cogo points to rename
                    ObjectIdCollection cogoPointIds = UserInput.GSelectionSetWithType("\n Chọn các cogo point cần đổi tên:", "AECC_COGO_POINT");
                    
                    if (cogoPointIds.Count == 0)
                    {
                        A.Ed.WriteMessage("\nKhông có cogo point nào được chọn!");
                        return;
                    }

                    // Create new name format: alignment1-alignment2
                    string newNameFormat = $"{alignment1.Name}-{alignment2.Name}";

                    int processedCount = 0;
                    // Process each cogo point
                    foreach (ObjectId cogoPointId in cogoPointIds)
                    {
                        CogoPoint? cogoPoint = tr.GetObject(cogoPointId, OpenMode.ForWrite) as CogoPoint;
                        if (cogoPoint != null)
                        {
                            // Set new description using the alignment names
                            cogoPoint.PointName = newNameFormat;
                            processedCount++;
                        }
                    }

                    // Update all point groups to reflect changes
                    UtilitiesC3D.UpdateAllPointGroup();

                    tr.Commit();
                    A.Ed.WriteMessage($"\nĐã đổi tên thành công {processedCount} cogo point với tên: {newNameFormat}");
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
    }
}

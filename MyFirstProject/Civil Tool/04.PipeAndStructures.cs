using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using System.Windows;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using MyFirstProject.Extensions;
using System.Windows.Forms;
// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3DCsharp.PipeAndStructures))]

namespace Civil3DCsharp
{
    public class PipeAndStructures
    {

        [CommandMethod("CT_Thay_Doi_Duong_Kinh_Cong")]
        public static void CTPIThayDoiDuongKinhCong()
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here
                ObjectIdCollection pipeIdColl = UserInput.GSelectionSet("Chọn ống cống theo thứ tự từ thượng lưu xuống hạ lưu: \n");
                //get partsize of pipe
                Pipe? pipe = tr.GetObject(pipeIdColl[0], OpenMode.ForWrite) as Pipe;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                ObjectId networkId = pipe.NetworkId;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                Network? network = tr.GetObject(networkId, OpenMode.ForWrite) as Network;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                ObjectId partListId = network.PartsListId;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                PartsList? partsList = tr.GetObject(partListId, OpenMode.ForWrite) as PartsList;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                ObjectIdCollection pipeFamilyIdColl = partsList.GetPartFamilyIdsByDomain(DomainType.Pipe);
#pragma warning restore CS8602 // Dereference of a possibly null reference.

                PartFamily? pipeFamily = tr.GetObject(pipeFamilyIdColl[0], OpenMode.ForWrite) as PartFamily;

                // Get current pipe size information
                string currentPipeSizeName = "";
                double currentPipeDiameter = 0;
                if (pipe != null)
                {
                    // Get the current diameter from the pipe itself
                    currentPipeDiameter = pipe.InnerDiameterOrWidth * 1000; // Convert to mm
                    
                    // Try to find the matching part size name by comparing with available sizes
                    PartFamily? currentPipeFamily = tr.GetObject(pipeFamilyIdColl[0], OpenMode.ForWrite) as PartFamily;
                    if (currentPipeFamily != null)
                    {
                        for (int i = 0; i < currentPipeFamily.PartSizeCount; i++)
                        {
                            PartSize? partSize = tr.GetObject(currentPipeFamily[i], OpenMode.ForRead) as PartSize;
                            if (partSize != null)
                            {
                                // For now, just use the first available size name as current
                                if (string.IsNullOrEmpty(currentPipeSizeName))
                                {
                                    currentPipeSizeName = partSize.Name;
                                }
                            }
                        }
                    }
                    
                    if (string.IsNullOrEmpty(currentPipeSizeName))
                    {
                        currentPipeSizeName = "Không xác định";
                    }
                }

                // Create and show the pipe diameter selection form
                using (var form = new MyFirstProject.PipeDiameterSelectionForm())
                {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    form.LoadPipeSizes(pipeFamily, tr);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                    
                    // Set current pipe information
                    form.SetCurrentPipeInfo(currentPipeSizeName, currentPipeDiameter, pipeIdColl.Count);
                    
                    DialogResult result = Application.ShowModalDialog(form);
                    
                    if (result != DialogResult.OK)
                    {
                        A.Ed.WriteMessage("\nThao tác bị hủy bỏ.");
                        return;
                    }

                    ObjectId pipeSizeId = form.SelectedPipeSizeId;

                    //swap pipe size
                    foreach (ObjectId pipeId in pipeIdColl)
                    {
                        Pipe? pipe1 = tr.GetObject(pipeId, OpenMode.ForWrite) as Pipe;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                        pipe1.SwapPartFamilyAndSize(pipeFamily.Id, pipeSizeId);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                    }

                    A.Ed.WriteMessage($"\nĐã thay đổi đường kính cống cho {pipeIdColl.Count} ống cống.");
                }

                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }

        [CommandMethod("CT_Thay_Doi_Do_Doc_Cong")]
        public static void CTPIThayDoiDoanDocCong()
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here
                ObjectIdCollection pipeIdColl = UserInput.GSelectionSet("Chọn ống cống theo thứ tự từ thượng lưu xuống hạ lưu: \n");
                Pipe? pipeStart = tr.GetObject(pipeIdColl[0], OpenMode.ForWrite) as Pipe;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                double caoDoStart = pipeStart.StartPoint.Z - pipeStart.InnerDiameterOrWidth / 2;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                Pipe? pipeEnd = tr.GetObject(pipeIdColl[^1], OpenMode.ForWrite) as Pipe;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                double caoDoEnd = pipeEnd.EndPoint.Z - pipeEnd.InnerDiameterOrWidth / 2;
#pragma warning restore CS8602 // Dereference of a possibly null reference.

                //tự nhập cao độ điểm đầu, cuối                    
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                String ans = UserInput.GYesNo2("Bạn muốn tự nhập cao độ đáy cống của điểm ĐẦU đoạn cống: (Y/N) \n");
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
                if (ans == "Y" | ans == "y")
                {
                    caoDoStart = UserInput.GDouble("Nhập cao độ đáy cống của điểm ĐẦU đoạn cống: \n");
                }
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                ans = UserInput.GYesNo2("Bạn muốn tự nhập cao độ đáy cống của điểm CUỐI đoạn cống: (Y/N) \n");
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
                if (ans == "Y" | ans == "y")
                {
                    caoDoEnd = UserInput.GDouble("Nhập cao độ đáy cống của điểm CUỐI đoạn cống: \n");
                }


                //tính dốc cống cho cả đoạn
                double pipeLong = 0;
                foreach (ObjectId pipeId in pipeIdColl)
                {
                    Pipe? pipe = tr.GetObject(pipeId, OpenMode.ForWrite) as Pipe;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    pipe.FlowDirectionMethod = FlowDirectionMethodType.StartToEnd;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                    pipeLong += pipe.Length3DCenterToCenter;
                    A.Ed.WriteMessage(pipeLong.ToString());
                }
                double pipeSlope = (caoDoEnd - caoDoStart) / pipeLong;

                //set cao độ điểm đầu đoạn
                double x = pipeStart.StartPoint.X;
                double y = pipeStart.StartPoint.Y;
                double z = caoDoStart + (pipeStart.InnerDiameterOrWidth / 2);
                Point3d startPointPipe = new(x, y, z);

                //set dốc cho từng đoạn cống

                foreach (ObjectId pipeId in pipeIdColl)
                {
                    Pipe? pipe = tr.GetObject(pipeId, OpenMode.ForWrite) as Pipe;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    pipe.StartPoint = startPointPipe;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                    pipe.SetSlopeHoldStart(pipeSlope);
                    startPointPipe = pipe.EndPoint;
                }


                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }


        [CommandMethod("CT_Bang_Cao_Do_Tu_Nhien_Ho_Thu")]
        public static void CTPIBangCaoDoTuNhienHoTho()
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();

                //start here
                ObjectId surfaceId = UserInput.GSurfaceId("Chọn mặt phẳng tự nhiên cần lấy cao độ:");
                CivSurface? civSurface = tr.GetObject(surfaceId, OpenMode.ForWrite) as CivSurface;
                ObjectId surfaceId2 = UserInput.GSurfaceId("Chọn mặt phẳng đáy khuôn cần lấy cao độ:");
                CivSurface? civSurface2 = tr.GetObject(surfaceId2, OpenMode.ForWrite) as CivSurface;
                ObjectIdCollection structureIds = UserInput.GSelectionSet("Chọn các hố thu cần lấy cao độ tự nhiên (cẩn thận chọn chính xác đối tượng để tránh lỗi:\n");
                int soLuongHo = structureIds.Count;
                String[] structureName = new String[soLuongHo];
                String[] elevation = new string[soLuongHo];
                String[] elevation2 = new string[soLuongHo];
                Point3d point3D;

                //truy xuất hố thu
                for (int i = 0; i < soLuongHo; i++)
                {
                    Structure? structure = tr.GetObject(structureIds[i], OpenMode.ForRead) as Structure;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    structureName[i] = structure.Name;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                    point3D = structure.Location;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    elevation[i] = civSurface.FindElevationAtXY(point3D.X, point3D.Y).ToString("0.##");
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    elevation2[i] = civSurface2.FindElevationAtXY(point3D.X, point3D.Y).ToString("0.##");
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                }

                UtilitiesCAD.TaoBangHoTHu(structureName.Count(), 3, "hố thu", structureName, elevation, elevation2);

                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }

        [CommandMethod("CT_Xoay_Ho_Thu_Theo_2_Diem")]
        public static void CTPIXoayHoThuTheo2diem()
        {
            // Lấy đối tượng cần thiết
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;
            _ = new UserInput();
            _ = new UtilitiesCAD();
            _ = new UtilitiesC3D();

            // Bắt đầu transaction
            using (Transaction trans = db.TransactionManager.StartTransaction())
            {
                // Tạo bộ lọc để chọn hố thu
                PromptSelectionOptions pso = new()
                {
                    MessageForAdding = "\nChọn các hố thu cần chỉnh góc xoay:"
                };
                SelectionFilter filter = new(
                [
                    new((int)DxfCode.Start, "AECC_STRUCTURE")
                ]);

                PromptSelectionResult psr = ed.GetSelection(pso, filter);

                if (psr.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\nKhông có hố thu nào được chọn.");
                    trans.Abort();
                    return;
                }

                SelectionSet ss = psr.Value;


                // get current viewport
                _ = A.Ed.GetCurrentView();
                //Double angleAcView = acView.ViewTwist;

                Point3d point1 = UserInput.GPoint("Chọn vị trí điểm ĐẦU làm căn cứ xoay đối tượng:");
                Point3d point2 = UserInput.GPoint("Chọn vị trí điểm CUỐI làm căn cứ xoay đối tượng:");

                // góc giữa 2 điểm
                Double angleAcView = Math.Atan2(point1.Y - point2.Y, point2.X - point1.X);

                // Duyệt qua các hố thu được chọn và thay đổi góc xoay
                foreach (SelectedObject selObj in ss)
                {
                    if (selObj != null)
                    {
                        Structure? hoThu = trans.GetObject(selObj.ObjectId, OpenMode.ForWrite) as Structure;
                        if (hoThu != null)
                        {
                            hoThu.Rotation = 3.14159 - angleAcView;
                        }
                    }
                }

                // Lưu thay đổi
                trans.Commit();
            }

            ed.WriteMessage("\nĐã hiệu chỉnh góc xoay cho các hố thu được chọn.");
        }


















    }
}

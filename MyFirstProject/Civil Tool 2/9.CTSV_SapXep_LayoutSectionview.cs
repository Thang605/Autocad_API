// (C) Copyright 2026 by  
// Lệnh bố trí vị trí Section View vào khung in theo lưới hàng/cột
//
using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.AutoCAD.Runtime;
using Acad = Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;

using Civil = Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.Civil.ApplicationServices;
using Section = Autodesk.Civil.DatabaseServices.Section;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using MyFirstProject.Extensions;
using MyFirstProject.Civil_Tool;
using AcEntity = Autodesk.AutoCAD.DatabaseServices.Entity;

// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3DCsharp.CTSV_SapXepLayout_Commands))]

namespace Civil3DCsharp
{
    public class CTSV_SapXepLayout_Commands
    {
        /// <summary>
        /// Lệnh bố trí Section View vào khung in theo lưới hàng × cột.
        /// Khung in mặc định 76×49. Người dùng chọn điểm gốc (góc trái trên),
        /// sau đó chọn các section view cần sắp xếp.
        /// Section view sẽ được di chuyển (Location) vào vị trí trung tâm mỗi ô trong lưới.
        /// Vẽ khung in bằng Polyline trên layer Defpoints cho từng trang.
        /// </summary>
        [CommandMethod("CTS_Sap_Xep_Layout")]
        public static void CTSVSapXepLayout()
        {
            // 1. Hiển thị form nhập thông số
            SapXepLayoutForm form = new();
            var dialogResult = form.ShowDialog();

            if (dialogResult != System.Windows.Forms.DialogResult.OK || !form.FormAccepted)
            {
                A.Ed.WriteMessage("\n Đã hủy lệnh.");
                return;
            }

            // 2. Lấy thông số từ form
            double chieuRong = form.ChieuRongKhung;   // B = 76
            double chieuCao = form.ChieuCaoKhung;     // H = 49
            int soCot = form.SoCot;
            int soHang = form.SoHang;
            double kcTrang = form.KhoangCachTrang;
            double dichDung = form.KhoangDichDung;
            int soSVPerPage = form.SoSVPerPage;

            // 3. Chọn điểm gốc (góc trái trên của khung in đầu tiên)
            Point3d basePoint = UserInput.GPoint("\n Chọn điểm gốc (góc trái trên khung in đầu tiên):");

            // 4. Chọn các section view (và polyline Defpoints cũ nếu có)
            ObjectIdCollection allSelectedIds = UserInput.GSelectionSet(
                "Chọn các Section View (và khung in cũ nếu có) cần bố trí:\n");

            if (allSelectedIds == null || allSelectedIds.Count == 0)
            {
                A.Ed.WriteMessage("\n Không có đối tượng nào được chọn.");
                return;
            }

            // 5. Phân loại: section view vs polyline Defpoints
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                ObjectIdCollection sectionViewIds = new();
                List<ObjectId> defpointPolylineIds = new();

                foreach (ObjectId objId in allSelectedIds)
                {
                    if (!objId.IsValid) continue;
                    var ent = tr.GetObject(objId, OpenMode.ForWrite) as AcEntity;
                    if (ent == null) continue;

                    if (objId.ObjectClass.DxfName == "AECC_GRAPH_SECTION_VIEW")
                    {
                        sectionViewIds.Add(objId);
                    }
                    else if (ent is Polyline pline && pline.Layer.Equals("Defpoints", StringComparison.OrdinalIgnoreCase))
                    {
                        defpointPolylineIds.Add(objId);
                    }
                }

                // Xóa các polyline Defpoints cũ (khung in cũ)
                if (defpointPolylineIds.Count > 0)
                {
                    foreach (ObjectId plId in defpointPolylineIds)
                    {
                        var entDel = tr.GetObject(plId, OpenMode.ForWrite) as AcEntity;
                        entDel?.Erase();
                    }
                    A.Ed.WriteMessage($"\n Đã xóa {defpointPolylineIds.Count} khung in cũ (polyline Defpoints).");
                }

                if (sectionViewIds.Count == 0)
                {
                    A.Ed.WriteMessage("\n Không có section view nào được chọn.");
                    tr.Commit();
                    return;
                }

                A.Ed.WriteMessage($"\n Đã chọn {sectionViewIds.Count} section view.");
                A.Ed.WriteMessage($"\n Khung in {chieuRong}×{chieuCao}, lưới {soCot}×{soHang} = {soSVPerPage} SV/trang.");

                // 6. Thu thập section view kèm station để sắp xếp
                List<(ObjectId Id, double Station)> svList = new();

                foreach (ObjectId svId in sectionViewIds)
                {
                    if (!svId.IsValid) continue;
                    SectionView? sv = tr.GetObject(svId, OpenMode.ForWrite) as SectionView;
                    if (sv == null) continue;

                    double station = 0;
                    try
                    {
                        ObjectId slId = sv.SampleLineId;
                        SampleLine? sl = tr.GetObject(slId, OpenMode.ForWrite) as SampleLine;
                        station = sl?.Station ?? 0;
                    }
                    catch
                    {
                        station = 0;
                    }

                    svList.Add((svId, station));
                }

                // Sắp xếp theo station tăng dần
                svList = svList.OrderBy(x => x.Station).ToList();

                // 6. Tính kích thước mỗi ô trong lưới
                double cellWidth = chieuRong / soCot;
                double cellHeight = chieuCao / soHang;

                // 7. Tính số trang cần thiết
                int totalPages = (int)Math.Ceiling((double)svList.Count / soSVPerPage);
                A.Ed.WriteMessage($"\n Cần {totalPages} trang để bố trí {svList.Count} section view.");

                // 8. Đảm bảo layer Defpoints tồn tại và lấy LayerId
                ObjectId defpointsLayerId = EnsureDefpointsLayer(tr);

                // 9. Vẽ khung in (polyline) cho từng trang trên layer Defpoints
                BlockTableRecord? btr = tr.GetObject(A.Db.CurrentSpaceId, OpenMode.ForWrite) as BlockTableRecord;
                if (btr == null)
                {
                    A.Ed.WriteMessage("\n Không thể mở BlockTableRecord.");
                    return;
                }

                for (int page = 0; page < totalPages; page++)
                {
                    double pageOffsetX = page * (chieuRong + kcTrang);
                    double x0 = basePoint.X + pageOffsetX;
                    double y0 = basePoint.Y;

                    // Vẽ hình chữ nhật khung in bằng Polyline
                    Polyline pline = new();
                    pline.AddVertexAt(0, new Point2d(x0, y0), 0, 0, 0);                          // Góc trái trên
                    pline.AddVertexAt(1, new Point2d(x0 + chieuRong, y0), 0, 0, 0);              // Góc phải trên
                    pline.AddVertexAt(2, new Point2d(x0 + chieuRong, y0 - chieuCao), 0, 0, 0);   // Góc phải dưới
                    pline.AddVertexAt(3, new Point2d(x0, y0 - chieuCao), 0, 0, 0);               // Góc trái dưới
                    pline.Closed = true;
                    pline.LayerId = defpointsLayerId;

                    btr.AppendEntity(pline);
                    tr.AddNewlyCreatedDBObject(pline, true);

                    A.Ed.WriteMessage($"\n  Đã vẽ khung in trang {page + 1} tại ({x0:F1}, {y0:F1})");
                }

                // 10. Thu thập AECC_TABLE entities (takeoff tables) từ ModelSpace
                List<AcEntity> allAeccTables = new();
                foreach (ObjectId entityId in btr)
                {
                    try
                    {
                        var dbObj = tr.GetObject(entityId, OpenMode.ForRead);
                        if (dbObj is AcEntity ent)
                        {
                            string dxfName = ent.GetRXClass().DxfName;
                            if (dxfName.Contains("AECC") && dxfName.Contains("TABLE"))
                            {
                                allAeccTables.Add(ent);
                            }
                        }
                    }
                    catch { }
                }

                A.Ed.WriteMessage($"\n Tìm thấy {allAeccTables.Count} AECC Tables (takeoff tables) trong bản vẽ.");

                // 11. Xây dựng map: mỗi section view → danh sách AECC_TABLE nằm trong vùng bounds
                //     Dùng GeometricExtents của SectionView (mở rộng 20% dự phòng) để tìm tables liên kết
                var svTableMap = new Dictionary<int, List<AcEntity>>();

                for (int i = 0; i < svList.Count; i++)
                {
                    SectionView? svCheck = tr.GetObject(svList[i].Id, OpenMode.ForRead) as SectionView;
                    if (svCheck == null) continue;

                    Extents3d svBounds;
                    try { svBounds = svCheck.GeometricExtents; }
                    catch { continue; }

                    double svWidth = svBounds.MaxPoint.X - svBounds.MinPoint.X;
                    double svHeight = svBounds.MaxPoint.Y - svBounds.MinPoint.Y;
                    double marginX = svWidth * 0.20;
                    double marginY = svHeight * 0.50; // Mở rộng Y nhiều hơn vì table thường nằm dưới SV

                    double svMinX = svBounds.MinPoint.X - marginX;
                    double svMinY = svBounds.MinPoint.Y - marginY;
                    double svMaxX = svBounds.MaxPoint.X + marginX;
                    double svMaxY = svBounds.MaxPoint.Y + marginY;

                    List<AcEntity> associatedTables = new();
                    foreach (var table in allAeccTables)
                    {
                        try
                        {
                            Extents3d tableBounds = table.GeometricExtents;
                            // Kiểm tra table center nằm trong vùng bounds mở rộng
                            double tableCenterX = (tableBounds.MinPoint.X + tableBounds.MaxPoint.X) / 2.0;
                            double tableCenterY = (tableBounds.MinPoint.Y + tableBounds.MaxPoint.Y) / 2.0;

                            if (tableCenterX >= svMinX && tableCenterX <= svMaxX &&
                                tableCenterY >= svMinY && tableCenterY <= svMaxY)
                            {
                                associatedTables.Add(table);
                            }
                        }
                        catch { }
                    }

                    svTableMap[i] = associatedTables;

                    if (associatedTables.Count > 0)
                    {
                        A.Ed.WriteMessage($"\n  SV[{i + 1}] có {associatedTables.Count} takeoff table(s) liên kết.");
                    }
                }

                // 12. Di chuyển từng section view (và takeoff tables) vào vị trí trong lưới
                for (int i = 0; i < svList.Count; i++)
                {
                    int pageIndex = i / soSVPerPage;
                    int indexInPage = i % soSVPerPage;

                    int col = indexInPage % soCot;
                    int row = indexInPage / soCot;

                    double pageOffsetX = pageIndex * (chieuRong + kcTrang);

                    double newX = basePoint.X + pageOffsetX + col * cellWidth + cellWidth / 2.0;
                    double newY = basePoint.Y - row * cellHeight - cellHeight / 2.0 + dichDung;

                    SectionView? sv = tr.GetObject(svList[i].Id, OpenMode.ForWrite) as SectionView;
                    if (sv == null) continue;

                    // Tính displacement vector từ vị trí cũ sang vị trí mới
                    Point3d oldLocation = sv.Location;
                    Vector3d displacement = new Point3d(newX, newY, 0) - oldLocation;

                    // Di chuyển section view
                    sv.Location = new Point3d(newX, newY, 0);

                    // Di chuyển các takeoff tables liên kết theo cùng displacement
                    if (svTableMap.TryGetValue(i, out List<AcEntity>? tables) && tables.Count > 0)
                    {
                        Matrix3d moveMatrix = Matrix3d.Displacement(displacement);
                        foreach (var table in tables)
                        {
                            try
                            {
                                var tableForWrite = tr.GetObject(table.ObjectId, OpenMode.ForWrite) as AcEntity;
                                tableForWrite?.TransformBy(moveMatrix);
                            }
                            catch (System.Exception ex)
                            {
                                A.Ed.WriteMessage($"\n  ⚠️ Lỗi di chuyển table: {ex.Message}");
                            }
                        }
                        A.Ed.WriteMessage($"\n  [{i + 1}/{svList.Count}] Trang {pageIndex + 1}, Hàng {row + 1} Cột {col + 1} → ({newX:F1}, {newY:F1}) + {tables.Count} table(s)");
                    }
                    else
                    {
                        A.Ed.WriteMessage($"\n  [{i + 1}/{svList.Count}] Trang {pageIndex + 1}, Hàng {row + 1} Cột {col + 1} → ({newX:F1}, {newY:F1})");
                    }
                }

                A.Ed.WriteMessage($"\n\n Hoàn thành! Đã bố trí {svList.Count} section view vào {totalPages} trang khung in.");

                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage("\n Lỗi AutoCAD: " + e.Message);
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage("\n Lỗi hệ thống: " + ex.Message);
            }
        }

        /// <summary>
        /// Đảm bảo layer Defpoints tồn tại, trả về ObjectId của layer
        /// </summary>
        private static ObjectId EnsureDefpointsLayer(Transaction tr)
        {
            LayerTable? lt = tr.GetObject(A.Db.LayerTableId, OpenMode.ForWrite) as LayerTable;
            if (lt == null) return A.Db.Clayer;

            if (lt.Has("Defpoints"))
            {
                return lt["Defpoints"];
            }

            // Tạo layer Defpoints nếu chưa có
            LayerTableRecord ltr = new();
            ltr.Name = "Defpoints";
            ltr.IsPlottable = false; // Layer Defpoints không in
            ObjectId layerId = lt.Add(ltr);
            tr.AddNewlyCreatedDBObject(ltr, true);
            return layerId;
        }
    }
}

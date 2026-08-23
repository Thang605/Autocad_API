using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;

using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using ATable = Autodesk.AutoCAD.DatabaseServices.Table;
using CivilParcel = Autodesk.Civil.DatabaseServices.Parcel;
using AcadDBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;
using MyFirstProject.Extensions;
using MyFirstProject.Civil_Tool_2;

[assembly: CommandClass(typeof(Civil3DCsharp.CTPA_BangThongKeParcel_Commands))]

namespace Civil3DCsharp
{
    public class CTPA_BangThongKeParcel_Commands
    {
        // Ghi nhớ đối tượng / vị trí gần nhất
        private static Point3d _lastTablePosition = Point3d.Origin;

        /// <summary>
        /// Lệnh mở giao diện Thống kê thuộc tính Parcel & Xuất bảng AutoCAD Table
        /// </summary>
        [CommandMethod("CTPA_BangThongKeParcel")]
        public static void CTPA_BangThongKeParcel()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            Editor ed = doc.Editor;
            Database db = doc.Database;

            Civil3DToolsExtensionApplication.EnsureResolverAttached();

            try
            {
                ed.WriteMessage("\n--- Khởi động lệnh Thống kê thuộc tính Parcel ---");

                // 1. Quét sơ bộ các Site và Parcel hiện có trong bản vẽ
                Dictionary<string, ObjectId> siteMap = new Dictionary<string, ObjectId>();
                List<ParcelInfo> initialParcels = new List<ParcelInfo>();

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    siteMap = GetSiteMap(tr);
                    initialParcels = CollectAllParcels(db, tr);
                    tr.Commit();
                }

                ed.WriteMessage($"\nTìm thấy {initialParcels.Count} Parcel trong {siteMap.Count} Phân khu (Site).");

                // 2. Khởi tạo Form
                using (BangThongKeParcelForm form = new BangThongKeParcelForm(initialParcels, siteMap))
                {
                    // Callback 1: Nạp lại toàn bộ Parcel
                    form.OnReloadAllParcels = () =>
                    {
                        using Transaction tr = db.TransactionManager.StartTransaction();
                        var list = CollectAllParcels(db, tr);
                        tr.Commit();
                        return list;
                    };

                    // Callback 2: Lọc theo Site
                    form.OnFilterBySite = (siteId) =>
                    {
                        using Transaction tr = db.TransactionManager.StartTransaction();
                        var list = CollectParcelsBySite(db, tr, siteId);
                        tr.Commit();
                        return list;
                    };

                    // Callback 3: Chọn trên màn hình bản vẽ
                    form.OnSelectOnScreen = () =>
                    {
                        List<ParcelInfo> pickedList = new List<ParcelInfo>();

                        using (var interaction = ed.StartUserInteraction(form))
                        {
                            PromptSelectionOptions pso = new PromptSelectionOptions
                            {
                                MessageForAdding = "\nChọn các Parcel trên bản vẽ (quét chọn hoặc click từng đối tượng): ",
                                AllowDuplicates = false
                            };

                            TypedValue[] filterList = new TypedValue[]
                            {
                                new TypedValue((int)DxfCode.Start, "AECC_PARCEL")
                            };
                            SelectionFilter filter = new SelectionFilter(filterList);

                            PromptSelectionResult psr = ed.GetSelection(pso, filter);
                            interaction.End();

                            if (psr.Status == PromptStatus.OK && psr.Value != null)
                            {
                                ObjectId[] ids = psr.Value.GetObjectIds();
                                using Transaction tr = db.TransactionManager.StartTransaction();
                                pickedList = CollectParcelsFromIds(db, tr, ids);
                                tr.Commit();

                                ed.WriteMessage($"\nĐã chọn {pickedList.Count} Parcel từ màn hình.");
                            }
                            else
                            {
                                ed.WriteMessage("\nKhông có Parcel nào được chọn thêm.");
                            }
                        }

                        return pickedList;
                    };

                    // Callback 4: Zoom và Highlight Parcel
                    form.OnZoomToParcel = (parcelId, bounds) =>
                    {
                        if (parcelId == ObjectId.Null || parcelId.IsErased) return;

                        using Transaction tr = db.TransactionManager.StartTransaction();
                        try
                        {
                            CivilParcel? parcel = tr.GetObject(parcelId, OpenMode.ForWrite) as CivilParcel;
                            if (parcel != null)
                            {
                                Extents3d ext = bounds ?? parcel.GeometricExtents;
                                ZoomToExtents(ed, ext);
                                parcel.Highlight();
                            }
                        }
                        catch (System.Exception ex)
                        {
                            ed.WriteMessage($"\nKhông thể zoom đến Parcel: {ex.Message}");
                        }
                        tr.Commit();
                    };

                    // Callback 5: Vẽ bảng AutoCAD Table vào bản vẽ
                    form.OnDrawTable = (parcels, cols, title, textHeight, titleHeight, rowHeight, includeTotal) =>
                    {
                        Point3d insertPt = Point3d.Origin;

                        using (var interaction = ed.StartUserInteraction(form))
                        {
                            PromptPointOptions ppo = new PromptPointOptions("\nChọn điểm đặt góc trên bên trái của Bảng thống kê: ")
                            {
                                AllowNone = false
                            };

                            PromptPointResult ppr = ed.GetPoint(ppo);
                            interaction.End();

                            if (ppr.Status != PromptStatus.OK)
                            {
                                ed.WriteMessage("\nĐã hủy chọn vị trí vẽ bảng.");
                                return;
                            }

                            insertPt = ppr.Value;
                        }

                        // Thực hiện tạo Table trong transaction
                        using (Transaction tr = db.TransactionManager.StartTransaction())
                        {
                            try
                            {
                                CreateParcelTable(db, tr, insertPt, parcels, cols, title, textHeight, titleHeight, rowHeight, includeTotal);
                                tr.Commit();

                                _lastTablePosition = insertPt;
                                ed.WriteMessage($"\n✅ Đã tạo thành công Bảng AutoCAD Table ({parcels.Count} Parcel) tại tọa độ ({insertPt.X:F2}, {insertPt.Y:F2}).");

                                MessageBox.Show("Đã vẽ Bảng thống kê Parcel vào bản vẽ AutoCAD thành công!", "Hoàn thành", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                            catch (System.Exception ex)
                            {
                                tr.Abort();
                                ed.WriteMessage($"\n❌ Lỗi khi vẽ bảng: {ex.Message}");
                                MessageBox.Show($"Lỗi khi vẽ bảng:\n{ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    };

                    // Hiển thị Form Modal Dialog
                    Application.ShowModalDialog(form);
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\nLỗi thực thi lệnh: {ex.Message}");
            }
        }

        #region Helper: Quét và Trích Xuất Dữ Liệu Parcel

        /// <summary>
        /// Lấy danh sách tên Site và ObjectId tương ứng
        /// </summary>
        private static Dictionary<string, ObjectId> GetSiteMap(Transaction tr)
        {
            var siteMap = new Dictionary<string, ObjectId>(StringComparer.OrdinalIgnoreCase);

            try
            {
                CivilDocument cdoc = CivilApplication.ActiveDocument;
                ObjectIdCollection siteIds = cdoc.GetSiteIds();

                foreach (ObjectId sId in siteIds)
                {
                    if (sId.IsNull || sId.IsErased) continue;
                    try
                    {
                        Site? site = tr.GetObject(sId, OpenMode.ForWrite) as Site;
                        if (site != null && !string.IsNullOrEmpty(site.Name))
                        {
                            siteMap[site.Name] = sId;
                        }
                    }
                    catch { }
                }
            }
            catch { }

            return siteMap;
        }

        /// <summary>
        /// Thu thập toàn bộ Parcel trong tất cả Site của bản vẽ
        /// </summary>
        private static List<ParcelInfo> CollectAllParcels(Database db, Transaction tr)
        {
            var list = new List<ParcelInfo>();
            var seenIds = new HashSet<ObjectId>();

            try
            {
                CivilDocument cdoc = CivilApplication.ActiveDocument;
                ObjectIdCollection siteIds = cdoc.GetSiteIds();

                foreach (ObjectId siteId in siteIds)
                {
                    if (siteId.IsNull || siteId.IsErased) continue;
                    try
                    {
                        Site? site = tr.GetObject(siteId, OpenMode.ForWrite) as Site;
                        if (site == null) continue;

                        ObjectIdCollection parcelIds = site.GetParcelIds();
                        foreach (ObjectId pId in parcelIds)
                        {
                            if (pId.IsNull || pId.IsErased || seenIds.Contains(pId)) continue;
                            seenIds.Add(pId);

                            var info = ExtractParcelInfo(tr, pId, site.Name);
                            if (info != null)
                            {
                                list.Add(info);
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }

            // Sắp xếp theo Số thửa hoặc Tên
            list = list.OrderBy(x => x.SoThua).ThenBy(x => x.TenThua).ToList();
            for (int i = 0; i < list.Count; i++) list[i].SoThuTu = i + 1;

            return list;
        }

        /// <summary>
        /// Thu thập Parcel theo một Site cụ thể
        /// </summary>
        private static List<ParcelInfo> CollectParcelsBySite(Database db, Transaction tr, ObjectId siteId)
        {
            var list = new List<ParcelInfo>();
            if (siteId.IsNull || siteId.IsErased) return list;

            try
            {
                Site? site = tr.GetObject(siteId, OpenMode.ForWrite) as Site;
                if (site != null)
                {
                    ObjectIdCollection parcelIds = site.GetParcelIds();
                    foreach (ObjectId pId in parcelIds)
                    {
                        if (pId.IsNull || pId.IsErased) continue;
                        var info = ExtractParcelInfo(tr, pId, site.Name);
                        if (info != null)
                        {
                            list.Add(info);
                        }
                    }
                }
            }
            catch { }

            list = list.OrderBy(x => x.SoThua).ThenBy(x => x.TenThua).ToList();
            for (int i = 0; i < list.Count; i++) list[i].SoThuTu = i + 1;

            return list;
        }

        /// <summary>
        /// Thu thập Parcel từ danh sách ObjectId được chọn
        /// </summary>
        private static List<ParcelInfo> CollectParcelsFromIds(Database db, Transaction tr, ObjectId[] ids)
        {
            var list = new List<ParcelInfo>();
            var seenIds = new HashSet<ObjectId>();

            foreach (ObjectId id in ids)
            {
                if (id.IsNull || id.IsErased || seenIds.Contains(id)) continue;
                seenIds.Add(id);

                var info = ExtractParcelInfo(tr, id, null);
                if (info != null)
                {
                    list.Add(info);
                }
            }

            list = list.OrderBy(x => x.SoThua).ThenBy(x => x.TenThua).ToList();
            for (int i = 0; i < list.Count; i++) list[i].SoThuTu = i + 1;

            return list;
        }

        /// <summary>
        /// Trích xuất chi tiết tất cả thuộc tính của 1 Parcel
        /// </summary>
        private static ParcelInfo? ExtractParcelInfo(Transaction tr, ObjectId parcelId, string? defaultSiteName)
        {
            try
            {
                CivilParcel? parcel = tr.GetObject(parcelId, OpenMode.ForWrite) as CivilParcel;
                if (parcel == null) return null;

                var info = new ParcelInfo
                {
                    ParcelId = parcelId,
                    SoThua = (int)parcel.Number,
                    TenThua = parcel.Name ?? $"Thửa {parcel.Number}",
                    DienTich = Math.Round(parcel.Area, 2),
                    MoTa = parcel.Description ?? "",
                    MaThue = parcel.TaxId.ToString()
                };

                // Lấy Site Name
                if (!string.IsNullOrEmpty(defaultSiteName))
                {
                    info.PhanKhu = defaultSiteName;
                }
                else
                {
                    info.PhanKhu = "Mặc định";
                }

                // Lấy Style Name
                try
                {
                    info.KieuStyle = parcel.StyleName ?? "Mặc định";
                }
                catch
                {
                    info.KieuStyle = "Mặc định";
                }

                // Tính toán Bounding Box và Tọa độ trọng tâm (Centroid X, Y)
                try
                {
                    Extents3d ext = parcel.GeometricExtents;
                    info.Bounds = ext;
                    info.ToaDoX = Math.Round((ext.MinPoint.X + ext.MaxPoint.X) / 2.0, 3);
                    info.ToaDoY = Math.Round((ext.MinPoint.Y + ext.MaxPoint.Y) / 2.0, 3);
                }
                catch
                {
                    info.ToaDoX = 0;
                    info.ToaDoY = 0;
                }

                return info;
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Helper: Zoom Extents trong AutoCAD

        /// <summary>
        /// Phóng to màn hình AutoCAD vào vị trí của Parcel
        /// </summary>
        private static void ZoomToExtents(Editor ed, Extents3d extents)
        {
            try
            {
                using (ViewTableRecord view = ed.GetCurrentView())
                {
                    double width = Math.Abs(extents.MaxPoint.X - extents.MinPoint.X);
                    double height = Math.Abs(extents.MaxPoint.Y - extents.MinPoint.Y);

                    // Tránh trường hợp kích thước bằng 0
                    if (width < 1.0) width = 10.0;
                    if (height < 1.0) height = 10.0;

                    Point2d center = new Point2d(
                        (extents.MinPoint.X + extents.MaxPoint.X) / 2.0,
                        (extents.MinPoint.Y + extents.MaxPoint.Y) / 2.0
                    );

                    double margin = 1.5;
                    view.CenterPoint = center;
                    view.Height = Math.Max(height, width) * margin;
                    view.Width = Math.Max(height, width) * margin;

                    ed.SetCurrentView(view);
                }
            }
            catch { }
        }

        #endregion

        #region Helper: Tạo Bảng AutoCAD Table

        /// <summary>
        /// Tạo bảng AutoCAD Table chuẩn, có Title, Header, Data rows và dòng Tổng cộng
        /// </summary>
        private static void CreateParcelTable(
            Database db,
            Transaction tr,
            Point3d insertionPoint,
            List<ParcelInfo> data,
            List<ColumnExportConfig> cols,
            string title,
            double textHeight,
            double titleHeight,
            double rowHeight,
            bool includeTotal)
        {
            BlockTable? bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
            if (bt == null) return;

            BlockTableRecord? btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
            if (btr == null) return;

            int numCols = cols.Count;
            int numRows = data.Count + 2 + (includeTotal ? 1 : 0); // Title + Header + Data (+ Total)

            ATable table = new ATable();
            table.TableStyle = db.Tablestyle;
            table.SetSize(numRows, numCols);
            table.Position = insertionPoint;

            // 1. Cấu hình chiều cao dòng & độ rộng cột
            for (int r = 0; r < numRows; r++)
            {
                table.Rows[r].Height = rowHeight;
            }
            table.Rows[0].Height = titleHeight * 2.0; // Dòng Title cao hơn

            for (int c = 0; c < numCols; c++)
            {
                table.Columns[c].Width = cols[c].DefaultWidth;
            }

            // 2. Title Row (Merge toàn bộ cột dòng 0)
            table.MergeCells(CellRange.Create(table, 0, 0, 0, numCols - 1));
            var titleCell = table.Cells[0, 0];
            titleCell.TextString = title;
            titleCell.Alignment = CellAlignment.MiddleCenter;
            titleCell.TextHeight = titleHeight;

            // 3. Header Row (Dòng 1)
            for (int c = 0; c < numCols; c++)
            {
                var hCell = table.Cells[1, c];
                hCell.TextString = cols[c].HeaderName;
                hCell.Alignment = CellAlignment.MiddleCenter;
                hCell.TextHeight = textHeight;
            }

            // 4. Data Rows (Từ dòng 2)
            for (int i = 0; i < data.Count; i++)
            {
                int r = i + 2;
                var parcel = data[i];

                for (int c = 0; c < numCols; c++)
                {
                    var cell = table.Cells[r, c];
                    cell.TextString = cols[c].GetValue(parcel);
                    cell.Alignment = cols[c].Alignment;
                    cell.TextHeight = textHeight;
                }
            }

            // 5. Total Row (Dòng cuối cùng nếu có)
            if (includeTotal)
            {
                int totalRow = numRows - 1;
                
                for (int c = 0; c < numCols; c++)
                {
                    var cell = table.Cells[totalRow, c];
                    cell.TextHeight = textHeight;

                    if (c == 0)
                    {
                        cell.TextString = "TỔNG CỘNG";
                        cell.Alignment = CellAlignment.MiddleCenter;
                    }
                    else if (cols[c].GetTotalValue != null)
                    {
                        cell.TextString = cols[c].GetTotalValue!(data);
                        cell.Alignment = cols[c].Alignment;
                    }
                    else
                    {
                        cell.TextString = "";
                        cell.Alignment = CellAlignment.MiddleCenter;
                    }
                }
            }

            // Thêm Table vào Database
            btr.AppendEntity(table);
            tr.AddNewlyCreatedDBObject(table, true);
        }

        #endregion

        [CommandMethod("CTPA_ThongKeParcel")]
        public static void CTPA_ThongKeParcel() => CTPA_BangThongKeParcel();

        [CommandMethod("CTPA_BangParcel")]
        public static void CTPA_BangParcel() => CTPA_BangThongKeParcel();
    }
}

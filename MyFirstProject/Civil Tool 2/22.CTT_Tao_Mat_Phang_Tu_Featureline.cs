using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using CadEntity = Autodesk.AutoCAD.DatabaseServices.Entity;
using CivSurface = Autodesk.Civil.DatabaseServices.TinSurface;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;

[assembly: CommandClass(typeof(MyFirstProject.Civil_Tool_2.CTT_Tao_Mat_Phang_Tu_Featureline_Command))]

namespace MyFirstProject.Civil_Tool_2
{
    public class SurfaceCreationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public ObjectId MasterSurfaceId { get; set; } = ObjectId.Null;
        public int SubSurfaceCount { get; set; }
    }

    public class CTT_Tao_Mat_Phang_Tu_Featureline_Command
    {
        [CommandMethod("CTT_Tao_Mat_Phang_Tu_Feature_Line")]
        public static void TaoMatPhangTuFeatureline()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            Editor ed = doc.Editor;
            Database db = doc.Database;

            try
            {
                using (var form = new TaoMatPhangTuFeaturelineForm(db, ed))
                {
                    Application.ShowModalDialog(form);
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n[LỖI] Không thể mở giao diện tạo mặt phẳng từ Feature Line: {ex.Message}");
            }
        }

        /// <summary>
        /// Logic xử lý chính: Tạo TIN Surface từ danh sách Feature Lines / Polylines khép kín.
        /// Áp dụng phương pháp gộp từng mặt phẳng nhỏ (Sub-surfaces) để đảm bảo phạm vi
        /// chỉ nằm gọn trong từng đường, không tạo tam giác nối giữa các vùng rời rạc.
        /// </summary>
        public static SurfaceCreationResult ThucHienTaoMatPhang(
            Database db,
            Editor ed,
            string masterSurfaceName,
            string masterStyleName,
            string subStyleName,
            List<ObjectId> entityIds,
            bool isMergeMode,
            bool createSnapshot,
            bool deleteSubSurfaces,
            double midOrdDistance,
            bool autoClose,
            bool rebuildAuto)
        {
            if (entityIds == null || entityIds.Count == 0)
            {
                return new SurfaceCreationResult { Success = false, Message = "Danh sách đối tượng rỗng." };
            }

            CivilDocument civDoc = CivilApplication.ActiveDocument;
            if (civDoc == null)
            {
                return new SurfaceCreationResult { Success = false, Message = "Không thể truy cập CivilDocument." };
            }

            try
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    // 1. Xác định Style cho Mặt phẳng tổng hợp (Master Surface)
                    ObjectId masterStyleId = FindSurfaceStyleId(civDoc, tr, masterStyleName);
                    if (masterStyleId == ObjectId.Null)
                    {
                        return new SurfaceCreationResult { Success = false, Message = "Bản vẽ không có Surface Style hợp lệ cho mặt phẳng tổng." };
                    }

                    // 2. Xác định Style cho các mặt phẳng con (Sub-surfaces)
                    ObjectId subStyleId = FindSurfaceStyleId(civDoc, tr, subStyleName);
                    if (subStyleId == ObjectId.Null)
                    {
                        subStyleId = GetOrCreateNoDisplayStyleId(civDoc, tr, masterStyleId);
                    }

                    // 3. Kiểm tra và đảm bảo tên Master Surface là duy nhất
                    string finalMasterName = EnsureUniqueSurfaceName(civDoc, tr, masterSurfaceName);

                    // 4. Lọc các ObjectId còn tồn tại và hợp lệ
                    List<ObjectId> validIds = new List<ObjectId>();
                    foreach (ObjectId id in entityIds)
                    {
                        if (!id.IsNull && id.IsValid && !id.IsErased)
                        {
                            validIds.Add(id);
                        }
                    }

                    if (validIds.Count == 0)
                    {
                        return new SurfaceCreationResult { Success = false, Message = "Không có đối tượng nào còn tồn tại trên bản vẽ." };
                    }

                    // ═══════════════════════════════════════════════════════════════
                    //  CHẾ ĐỘ 1: GỘP TỪNG MẶT PHẲNG NHỎ (THEO YÊU CẦU NGƯỜI DÙNG)
                    // ═══════════════════════════════════════════════════════════════
                    if (isMergeMode)
                    {
                        ed.WriteMessage($"\n--- BẮT ĐẦU TẠO MẶT PHẲNG GỘP TỪ {validIds.Count} ĐƯỜNG KHÉP KÍN ---");

                        // 4.1. Tạo Master TIN Surface
                        ObjectId masterSurfaceId = TinSurface.Create(finalMasterName, masterStyleId);
                        if (masterSurfaceId.IsNull || !masterSurfaceId.IsValid)
                        {
                            return new SurfaceCreationResult { Success = false, Message = "Hàm TinSurface.Create không trả về ObjectId hợp lệ cho mặt phẳng tổng." };
                        }

                        TinSurface? masterSurface = tr.GetObject(masterSurfaceId, OpenMode.ForWrite) as TinSurface;
                        if (masterSurface == null)
                        {
                            return new SurfaceCreationResult { Success = false, Message = "Không thể mở Master TinSurface ở chế độ ghi." };
                        }

                        List<ObjectId> createdSubSurfaceIds = new List<ObjectId>();
                        int successCount = 0;

                        // 4.2. Duyệt qua từng Feature Line, tạo từng mặt phẳng con có Outer Boundary
                        for (int i = 0; i < validIds.Count; i++)
                        {
                            ObjectId entId = validIds[i];
                            string subName = EnsureUniqueSurfaceName(civDoc, tr, $"{finalMasterName}_Sub_{i + 1}");

                            try
                            {
                                ObjectId subSurfId = TinSurface.Create(subName, subStyleId);
                                if (subSurfId.IsNull || !subSurfId.IsValid) continue;

                                TinSurface? subSurf = tr.GetObject(subSurfId, OpenMode.ForWrite) as TinSurface;
                                if (subSurf == null) continue;

                                // A. Thêm đối tượng vào làm Standard Breakline
                                ObjectIdCollection singleColl = new ObjectIdCollection { entId };
                                try
                                {
                                    subSurf.BreaklinesDefinition.AddStandardBreaklines(
                                        singleColl,
                                        midOrdDistance,
                                        100.0,
                                        1.0,
                                        0.0
                                    );
                                }
                                catch (System.Exception exBrk)
                                {
                                    ed.WriteMessage($"\n⚠️ Cảnh báo thêm breakline cho đối tượng {i + 1}: {exBrk.Message}");
                                }

                                // B. Thêm đối tượng vào làm Outer Boundary
                                bool bndAdded = false;
                                try
                                {
                                    subSurf.BoundariesDefinition.AddBoundaries(
                                        singleColl,
                                        midOrdDistance,
                                        SurfaceBoundaryType.Outer,
                                        false
                                    );
                                    bndAdded = true;
                                }
                                catch
                                {
                                    // Fallback: Nếu không nhận trực tiếp ObjectIdCollection, trích xuất Point3dCollection
                                }

                                if (!bndAdded)
                                {
                                    var pts = ExtractBoundaryPoints(entId, tr, midOrdDistance, autoClose);
                                    if (pts != null && pts.Count >= 3)
                                    {
                                        Point3dCollection ptsColl = new Point3dCollection();
                                        foreach (var p in pts) ptsColl.Add(p);

                                        // Đảm bảo khép kín
                                        if (ptsColl[0].DistanceTo(ptsColl[ptsColl.Count - 1]) > 0.001)
                                        {
                                            ptsColl.Add(ptsColl[0]);
                                        }

                                        subSurf.BoundariesDefinition.AddBoundaries(
                                            ptsColl,
                                            midOrdDistance,
                                            SurfaceBoundaryType.Outer,
                                            false
                                        );
                                        bndAdded = true;
                                    }
                                }

                                // C. Rebuild mặt phẳng con để tính toán lưới tam giác hoàn chỉnh
                                subSurf.Rebuild();

                                // D. Gộp (Paste) mặt phẳng con vào Mặt phẳng tổng hợp
                                masterSurface.PasteSurface(subSurfId);

                                createdSubSurfaceIds.Add(subSurfId);
                                successCount++;
                            }
                            catch (System.Exception exSub)
                            {
                                ed.WriteMessage($"\n⚠️ Lỗi khi xử lý đường {i + 1}: {exSub.Message}");
                            }
                        }

                        if (successCount == 0)
                        {
                            return new SurfaceCreationResult
                            {
                                Success = false,
                                Message = "Không tạo được mặt phẳng con nào từ các đối tượng đã chọn."
                            };
                        }

                        // 4.3. Rebuild Master Surface để cập nhật toàn bộ các mặt phẳng đã gộp
                        masterSurface.Rebuild();

                        // 4.4. Tạo Snapshot nếu được chọn (Đóng băng lưới tam giác)
                        if (createSnapshot)
                        {
                            try
                            {
                                masterSurface.CreateSnapshot();
                                ed.WriteMessage("\n✅ Đã tạo Surface Snapshot để đóng băng dữ liệu cho mặt phẳng tổng.");
                            }
                            catch (System.Exception exSnap)
                            {
                                ed.WriteMessage($"\n⚠️ Không thể tạo Snapshot: {exSnap.Message}");
                            }
                        }

                        // 4.5. Xử lý các mặt phẳng con
                        if (deleteSubSurfaces && createSnapshot)
                        {
                            foreach (ObjectId subId in createdSubSurfaceIds)
                            {
                                try
                                {
                                    if (tr.GetObject(subId, OpenMode.ForWrite) is TinSurface sub)
                                    {
                                        sub.Erase(true);
                                    }
                                }
                                catch { }
                            }
                            ed.WriteMessage($"\n🗑️ Đã xóa {createdSubSurfaceIds.Count} mặt phẳng con tạm thời (dữ liệu đã lưu trong Snapshot).");
                        }
                        else
                        {
                            ed.WriteMessage($"\n📁 Đã tạo {createdSubSurfaceIds.Count} mặt phẳng con (gán style '{subStyleName}').");
                        }

                        // 4.6. Cấu hình AutoRebuild
                        try
                        {
                            masterSurface.AutoRebuild = rebuildAuto;
                        }
                        catch { }

                        tr.Commit();

                        ed.WriteMessage($"\n🎉 Đã tạo thành công mặt phẳng '{finalMasterName}' từ {successCount}/{validIds.Count} Feature Lines.");

                        return new SurfaceCreationResult
                        {
                            Success = true,
                            Message = $"Tạo thành công mặt phẳng '{finalMasterName}'!",
                            MasterSurfaceId = masterSurfaceId,
                            SubSurfaceCount = successCount
                        };
                    }
                    // ═══════════════════════════════════════════════════════════════
                    //  CHẾ ĐỘ 2: TẠO 1 MẶT PHẲNG TRỰC TIẾP (DÀNH CHO 1 ĐƯỜNG ĐƠN LẺ)
                    // ═══════════════════════════════════════════════════════════════
                    else
                    {
                        ObjectId newSurfaceId = TinSurface.Create(finalMasterName, masterStyleId);
                        TinSurface? surface = tr.GetObject(newSurfaceId, OpenMode.ForWrite) as TinSurface;
                        if (surface == null)
                        {
                            return new SurfaceCreationResult { Success = false, Message = "Không thể mở TinSurface ở chế độ ghi." };
                        }

                        ObjectIdCollection validColl = new ObjectIdCollection();
                        foreach (ObjectId id in validIds) validColl.Add(id);

                        // Thêm Breaklines
                        try
                        {
                            surface.BreaklinesDefinition.AddStandardBreaklines(
                                validColl,
                                midOrdDistance,
                                100.0,
                                1.0,
                                0.0
                            );
                        }
                        catch (System.Exception exBrk)
                        {
                            ed.WriteMessage($"\n⚠️ Cảnh báo khi thêm Breaklines: {exBrk.Message}");
                        }

                        // Thêm Boundaries
                        foreach (ObjectId id in validIds)
                        {
                            var pts = ExtractBoundaryPoints(id, tr, midOrdDistance, autoClose);
                            if (pts.Count >= 3)
                            {
                                Point3dCollection ptsColl = new Point3dCollection();
                                foreach (Point3d p in pts) ptsColl.Add(p);
                                if (ptsColl[0].DistanceTo(ptsColl[ptsColl.Count - 1]) > 0.001)
                                    ptsColl.Add(ptsColl[0]);

                                try
                                {
                                    surface.BoundariesDefinition.AddBoundaries(
                                        ptsColl,
                                        midOrdDistance,
                                        SurfaceBoundaryType.Outer,
                                        false
                                    );
                                }
                                catch { }
                            }
                        }

                        surface.Rebuild();

                        try
                        {
                            surface.AutoRebuild = rebuildAuto;
                        }
                        catch { }

                        tr.Commit();

                        return new SurfaceCreationResult
                        {
                            Success = true,
                            Message = $"Tạo thành công mặt phẳng '{finalMasterName}'!",
                            MasterSurfaceId = newSurfaceId,
                            SubSurfaceCount = 1
                        };
                    }
                }
            }
            catch (System.Exception ex)
            {
                return new SurfaceCreationResult
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }

        #region HELPER METHODS

        private static ObjectId FindSurfaceStyleId(CivilDocument civDoc, Transaction tr, string styleName)
        {
            if (string.IsNullOrWhiteSpace(styleName)) return ObjectId.Null;

            SurfaceStyleCollection styles = civDoc.Styles.SurfaceStyles;
            if (styles.Contains(styleName))
            {
                return styles[styleName];
            }

            foreach (ObjectId sId in styles)
            {
                if (tr.GetObject(sId, OpenMode.ForRead) is SurfaceStyle st)
                {
                    if (string.Equals(st.Name, styleName, StringComparison.OrdinalIgnoreCase))
                    {
                        return sId;
                    }
                }
            }

            return ObjectId.Null;
        }

        private static ObjectId GetOrCreateNoDisplayStyleId(CivilDocument civDoc, Transaction tr, ObjectId fallbackId)
        {
            SurfaceStyleCollection styles = civDoc.Styles.SurfaceStyles;

            string[] candidateNames = { "_No Display", "No Display", "Border Only", "_Khong Hien Thi" };
            foreach (string name in candidateNames)
            {
                if (styles.Contains(name))
                {
                    return styles[name];
                }
            }

            foreach (ObjectId sId in styles)
            {
                if (tr.GetObject(sId, OpenMode.ForRead) is SurfaceStyle st)
                {
                    if (st.Name.IndexOf("No Display", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        st.Name.IndexOf("Border Only", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return sId;
                    }
                }
            }

            return fallbackId;
        }

        private static string EnsureUniqueSurfaceName(CivilDocument civDoc, Transaction tr, string baseName)
        {
            var existingIds = civDoc.GetSurfaceIds();
            HashSet<string> existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (ObjectId sId in existingIds)
            {
                if (tr.GetObject(sId, OpenMode.ForRead) is CivSurface s && !string.IsNullOrEmpty(s.Name))
                {
                    existingNames.Add(s.Name);
                }
            }

            if (!existingNames.Contains(baseName))
            {
                return baseName;
            }

            int index = 1;
            string testName;
            do
            {
                testName = $"{baseName}_{index}";
                index++;
            }
            while (existingNames.Contains(testName));

            return testName;
        }

        /// <summary>
        /// Trích xuất danh sách điểm 3D từ đối tượng (FeatureLine, Polyline, Polyline3d)
        /// </summary>
        public static List<Point3d> ExtractBoundaryPoints(ObjectId entId, Transaction tr, double curveSampling, bool autoClose)
        {
            return CT_Tao_3d_Solid_Tu_Feature_Line_Command.ExtractBoundaryPoints(
                entId,
                tr,
                curveSampling,
                autoClose,
                out _,
                out _,
                out _,
                out _,
                out _
            );
        }

        #endregion
    }
}

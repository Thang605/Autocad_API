// CTSU_DieuChinh_EG_TK — 2 lệnh:
//   Lệnh 1: CTSU_DieuChinh_EG_TK       → chọn section, tạo polyline, xóa điểm cũ
//   Lệnh 2: CTSU_DieuChinh_EG_TK_Apply → đọc polyline đã chỉnh, add điểm mới vào surface
//   ★ Hỗ trợ nhiều section: chạy Lệnh 1 nhiều lần, rồi Apply 1 lần duy nhất
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Autodesk.AutoCAD.Runtime;
using Acad = Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;

using Civil = Autodesk.Civil.ApplicationServices;
using Autodesk.Civil;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using Autodesk.Civil.Runtime;
using Autodesk.Civil.Settings;
using CivSurface = Autodesk.Civil.DatabaseServices.TinSurface;
using Section = Autodesk.Civil.DatabaseServices.Section;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Label = Autodesk.Civil.DatabaseServices.Label;
using MyFirstProject.Extensions;
using MyFirstProject;

[assembly: CommandClass(typeof(Civil3DCsharp.CTSU_DieuChinh_EG_TK_Commands))]

namespace Civil3DCsharp
{
    /// <summary>
    /// Dữ liệu cho mỗi section edit (1 polyline = 1 bộ dữ liệu)
    /// </summary>
    internal class SectionEditData
    {
        public ObjectId PolylineId { get; set; }
        public ObjectId SurfaceId { get; set; }
        public ObjectId AlignmentId { get; set; }
        public ObjectId SectionViewId { get; set; }
        public double Station { get; set; }
        public List<Point3d> OldWorldPoints { get; set; } = new();
    }

    public class CTSU_DieuChinh_EG_TK_Commands
    {
        // ★ Lưu danh sách nhiều section edit thay vì chỉ 1
        private static List<SectionEditData> _pendingEdits = new();

        // ═══════════════════════════════════════════════════════════════
        // LỆNH 1: Chọn section → tạo polyline → lưu vào queue
        // Có thể chạy nhiều lần → mỗi lần thêm 1 polyline vào queue
        // Khi xong tất cả, chạy lệnh CTSU_DieuChinh_EG_TK_Apply
        // ═══════════════════════════════════════════════════════════════
        [CommandMethod("CTSU_DieuChinh_EG_TK")]
        public static void CTSUDieuChinhEGTK()
        {
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                CivilDocument civDoc = CivilApplication.ActiveDocument;

                // Step 1: Chọn section
                PromptEntityOptions peo = new("\nChọn section trên trắc ngang: ");
                peo.SetRejectMessage("\nĐối tượng không phải section.");
                peo.AddAllowedClass(typeof(Section), true);
                PromptEntityResult per = A.Ed.GetEntity(peo);
                if (per.Status != PromptStatus.OK) { A.Ed.WriteMessage("\nĐã hủy."); return; }

                ObjectId sectionId = per.ObjectId;
                Section? section = tr.GetObject(sectionId, OpenMode.ForWrite) as Section;
                if (section == null) { A.Ed.WriteMessage("\nKhông lấy được section."); return; }

                // Step 2: Tìm hierarchy (Alignment → SLG → SL → Surface)
                SampleLineGroup? sampleLineGroup = null;
                SampleLine? currentSampleLine = null;
                Alignment? alignment = null;
                ObjectId sourceId = ObjectId.Null;
                SectionView? sectionView = null;
                bool found = false;

                foreach (ObjectId alId in civDoc.GetAlignmentIds())
                {
                    if (found) break;
                    Alignment? al = tr.GetObject(alId, OpenMode.ForWrite) as Alignment;
                    if (al == null) continue;
                    foreach (ObjectId slgId in al.GetSampleLineGroupIds())
                    {
                        if (found) break;
                        SampleLineGroup? slg = tr.GetObject(slgId, OpenMode.ForWrite) as SampleLineGroup;
                        if (slg == null) continue;
                        SectionSourceCollection sources = slg.GetSectionSources();
                        foreach (ObjectId slId in slg.GetSampleLineIds())
                        {
                            if (found) break;
                            SampleLine? sl = tr.GetObject(slId, OpenMode.ForWrite) as SampleLine;
                            if (sl == null) continue;
                            foreach (SectionSource src in sources)
                            {
                                try
                                {
                                    if (sl.GetSectionId(src.SourceId) == sectionId)
                                    {
                                        currentSampleLine = sl;
                                        sampleLineGroup = slg;
                                        alignment = al;
                                        sourceId = src.SourceId;
                                        found = true;
                                        break;
                                    }
                                }
                                catch { continue; }
                            }
                        }
                    }
                }

                if (currentSampleLine == null || sampleLineGroup == null || alignment == null)
                { A.Ed.WriteMessage("\nKhông tìm thấy thông tin section."); return; }

                // Tìm section view
                foreach (SectionViewGroup svGroup in sampleLineGroup.SectionViewGroups)
                {
                    if (sectionView != null) break;
                    foreach (ObjectId svId in svGroup.GetSectionViewIds())
                    {
                        SectionView? sv = tr.GetObject(svId, OpenMode.ForWrite) as SectionView;
                        if (sv != null && sv.SampleLineId == currentSampleLine.ObjectId)
                        { sectionView = sv; break; }
                    }
                }
                if (sectionView == null) { A.Ed.WriteMessage("\nKhông tìm thấy section view."); return; }

                CivSurface? surface = tr.GetObject(sourceId, OpenMode.ForWrite) as CivSurface;
                if (surface == null) { A.Ed.WriteMessage("\nKhông tìm thấy surface nguồn."); return; }

                A.Ed.WriteMessage($"\n📋 Surface: '{surface.Name}' | Station: {currentSampleLine.Station:F3}");

                // Step 3: Tạo polyline từ section + tính world coords
                SectionPointCollection sectionPoints = section.SectionPoints;
                Point3d svLoc = sectionView.Location;
                bool wasAutoElev = sectionView.IsElevationRangeAutomatic;
                sectionView.IsElevationRangeAutomatic = false;
                double elevDatum = sectionView.ElevationMin;

                Polyline polyline = new();
                int vIdx = 0;
                List<Point3d> oldWorldPoints = new();

                foreach (SectionPoint sp in sectionPoints)
                {
                    Point3d loc = sp.Location;
                    double offset = loc.X;
                    double elevation = loc.Y;

                    polyline.AddVertexAt(vIdx++, new Point2d(
                        svLoc.X + offset,
                        svLoc.Y + (elevation - elevDatum)), 0, 0, 0);

                    double easting = 0, northing = 0;
                    alignment.PointLocation(currentSampleLine.Station, offset, ref easting, ref northing);
                    oldWorldPoints.Add(new Point3d(easting, northing, elevation));
                }

                sectionView.IsElevationRangeAutomatic = wasAutoElev;

                // Add polyline to model space (màu đỏ để dễ nhận biết)
                polyline.ColorIndex = 1;
                polyline.Layer = "0";
                BlockTable? bt = tr.GetObject(A.Db.BlockTableId, OpenMode.ForWrite) as BlockTable;
                BlockTableRecord? ms = tr.GetObject(bt![BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                ObjectId newPolyId = ms!.AppendEntity(polyline);
                tr.AddNewlyCreatedDBObject(polyline, true);

                A.Ed.WriteMessage($"\n✅ Đã tạo polyline ({vIdx} điểm, màu đỏ).");

                // ★ Lưu vào danh sách pending (thay vì ghi đè static cũ)
                _pendingEdits.Add(new SectionEditData
                {
                    PolylineId = newPolyId,
                    SurfaceId = sourceId,
                    AlignmentId = alignment.ObjectId,
                    SectionViewId = sectionView.ObjectId,
                    Station = currentSampleLine.Station,
                    OldWorldPoints = oldWorldPoints
                });

                tr.Commit();

                A.Ed.WriteMessage("\n\n══════════════════════════════════════");
                A.Ed.WriteMessage($"\n✏️  Polyline #{_pendingEdits.Count} đã tạo (tổng: {_pendingEdits.Count} đang chờ).");
                A.Ed.WriteMessage("\n   → Tiếp tục chọn section khác nếu muốn chỉnh thêm.");
                A.Ed.WriteMessage("\n   → Khi xong hết, chạy lệnh: CTSU_DieuChinh_EG_TK_Apply");
                A.Ed.WriteMessage("\n══════════════════════════════════════");
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage($"\nLỗi AutoCAD: {e.Message}");
                tr.Abort();
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi: {ex.Message}\n{ex.StackTrace}");
                tr.Abort();
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // LỆNH 2: Apply TẤT CẢ polyline đã chỉnh sửa vào surface
        // ═══════════════════════════════════════════════════════════════
        [CommandMethod("CTSU_DieuChinh_EG_TK_Apply")]
        public static void CTSUDieuChinhEGTKApply()
        {
            if (_pendingEdits.Count == 0)
            {
                A.Ed.WriteMessage("\n⚠ Chưa có polyline nào cần apply.");
                A.Ed.WriteMessage("\n   Hãy chạy lệnh CTSU_DieuChinh_EG_TK trước.");
                return;
            }

            A.Ed.WriteMessage($"\n📦 Bắt đầu apply {_pendingEdits.Count} polyline...");

            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                int totalRemoved = 0;
                int totalAdded = 0;
                int successCount = 0;
                int failCount = 0;

                // Gom điểm theo surface để Rebuild 1 lần duy nhất cho mỗi surface
                Dictionary<ObjectId, List<Point3d>> surfaceNewPoints = new();
                Dictionary<ObjectId, int> surfaceRemovedCount = new();

                foreach (SectionEditData edit in _pendingEdits)
                {
                    try
                    {
                        Polyline? editedPoly = tr.GetObject(edit.PolylineId, OpenMode.ForWrite) as Polyline;
                        SectionView? sv = tr.GetObject(edit.SectionViewId, OpenMode.ForWrite) as SectionView;
                        Alignment? al = tr.GetObject(edit.AlignmentId, OpenMode.ForWrite) as Alignment;
                        CivSurface? surface = tr.GetObject(edit.SurfaceId, OpenMode.ForWrite) as CivSurface;

                        if (editedPoly == null || sv == null || al == null || surface == null)
                        {
                            A.Ed.WriteMessage($"\n⚠ Bỏ qua station {edit.Station:F3} — đối tượng đã bị xóa.");
                            failCount++;
                            continue;
                        }

                        // Transform polyline vertices → world coordinates
                        List<Point3d> newWorldPoints = TransformPolylineVertices(sv, editedPoly, edit.Station, al);

                        if (newWorldPoints.Count < 2)
                        {
                            A.Ed.WriteMessage($"\n⚠ Bỏ qua station {edit.Station:F3} — không đủ điểm (cần ≥ 2).");
                            failCount++;
                            continue;
                        }

                        // Remove điểm cũ
                        int removedCount = 0;
                        foreach (Point3d pt in edit.OldWorldPoints)
                        {
                            try
                            {
                                TinSurfaceVertex vertex = surface.FindVertexAtXY(pt.X, pt.Y);
                                if (vertex != null)
                                {
                                    surface.DeleteVertex(vertex);
                                    removedCount++;
                                }
                            }
                            catch { }
                        }

                        // Gom điểm mới theo surface
                        if (!surfaceNewPoints.ContainsKey(edit.SurfaceId))
                        {
                            surfaceNewPoints[edit.SurfaceId] = new List<Point3d>();
                            surfaceRemovedCount[edit.SurfaceId] = 0;
                        }
                        surfaceNewPoints[edit.SurfaceId].AddRange(newWorldPoints);
                        surfaceRemovedCount[edit.SurfaceId] += removedCount;

                        totalRemoved += removedCount;
                        totalAdded += newWorldPoints.Count;

                        // Xóa polyline tạm
                        editedPoly.Erase();
                        successCount++;

                        A.Ed.WriteMessage($"\n   ✓ Station {edit.Station:F3}: xóa {removedCount} cũ, thêm {newWorldPoints.Count} mới");
                    }
                    catch (System.Exception ex)
                    {
                        A.Ed.WriteMessage($"\n⚠ Lỗi tại station {edit.Station:F3}: {ex.Message}");
                        failCount++;
                    }
                }

                // Add tất cả điểm mới và Rebuild từng surface 1 lần
                foreach (var kvp in surfaceNewPoints)
                {
                    CivSurface? surface = tr.GetObject(kvp.Key, OpenMode.ForWrite) as CivSurface;
                    if (surface == null) continue;

                    Point3dCollection pts = new();
                    foreach (Point3d pt in kvp.Value) pts.Add(pt);

                    surface.AddVertices(pts);
                    surface.Rebuild();

                    A.Ed.WriteMessage($"\n📐 Surface '{surface.Name}': xóa {surfaceRemovedCount[kvp.Key]} cũ, thêm {kvp.Value.Count} mới → Rebuild ✓");
                }

                // Reset trạng thái
                _pendingEdits.Clear();

                tr.Commit();

                A.Ed.WriteMessage("\n\n══════════════════════════════════════");
                A.Ed.WriteMessage($"\n✅ Hoàn thành! {successCount} section thành công, {failCount} thất bại.");
                A.Ed.WriteMessage($"\n   Tổng: xóa {totalRemoved} điểm cũ, thêm {totalAdded} điểm mới.");
                A.Ed.WriteMessage("\n══════════════════════════════════════");
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage($"\nLỗi AutoCAD: {e.Message}");
                tr.Abort();
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi: {ex.Message}\n{ex.StackTrace}");
                tr.Abort();
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // LỆNH 3: Xem danh sách pending / Hủy tất cả
        // ═══════════════════════════════════════════════════════════════
        [CommandMethod("CTSU_DieuChinh_EG_TK_Status")]
        public static void CTSUDieuChinhEGTKStatus()
        {
            if (_pendingEdits.Count == 0)
            {
                A.Ed.WriteMessage("\n📭 Không có polyline nào đang chờ apply.");
                return;
            }

            A.Ed.WriteMessage($"\n\n📋 Danh sách {_pendingEdits.Count} polyline đang chờ apply:");
            A.Ed.WriteMessage("\n──────────────────────────────────");
            for (int i = 0; i < _pendingEdits.Count; i++)
            {
                var edit = _pendingEdits[i];
                A.Ed.WriteMessage($"\n   #{i + 1}: Station {edit.Station:F3} | {edit.OldWorldPoints.Count} điểm");
            }
            A.Ed.WriteMessage("\n──────────────────────────────────");
            A.Ed.WriteMessage("\n   Chạy CTSU_DieuChinh_EG_TK_Apply để apply tất cả.");
            A.Ed.WriteMessage("\n   Chạy CTSU_DieuChinh_EG_TK_Cancel để hủy tất cả.");
        }

        // ═══════════════════════════════════════════════════════════════
        // LỆNH 4: Hủy tất cả pending edits + xóa polyline tạm
        // ═══════════════════════════════════════════════════════════════
        [CommandMethod("CTSU_DieuChinh_EG_TK_Cancel")]
        public static void CTSUDieuChinhEGTKCancel()
        {
            if (_pendingEdits.Count == 0)
            {
                A.Ed.WriteMessage("\n📭 Không có gì để hủy.");
                return;
            }

            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                int deleted = 0;
                foreach (SectionEditData edit in _pendingEdits)
                {
                    try
                    {
                        Polyline? poly = tr.GetObject(edit.PolylineId, OpenMode.ForWrite) as Polyline;
                        if (poly != null)
                        {
                            poly.Erase();
                            deleted++;
                        }
                    }
                    catch { }
                }

                _pendingEdits.Clear();
                tr.Commit();

                A.Ed.WriteMessage($"\n🗑️ Đã hủy và xóa {deleted} polyline tạm.");
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi: {ex.Message}");
                tr.Abort();
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // Helper: Transform polyline vertices (section view space → world)
        // ═══════════════════════════════════════════════════════════════
        private static List<Point3d> TransformPolylineVertices(
            SectionView sectionView, Polyline polyline, double station, Alignment alignment)
        {
            var worldPoints = new List<Point3d>();
            Point3d svLoc = sectionView.Location;
            bool wasAutoElev = sectionView.IsElevationRangeAutomatic;

            try
            {
                sectionView.IsElevationRangeAutomatic = false;
                double elevDatum = sectionView.ElevationMin;

                for (int i = 0; i < polyline.NumberOfVertices; i++)
                {
                    Point2d vertex = polyline.GetPoint2dAt(i);
                    double offset = vertex.X - svLoc.X;
                    double elevation = vertex.Y - svLoc.Y + elevDatum;

                    double easting = 0, northing = 0;
                    alignment.PointLocation(station, offset, ref easting, ref northing);
                    worldPoints.Add(new Point3d(easting, northing, elevation));
                }
            }
            finally
            {
                sectionView.IsElevationRangeAutomatic = wasAutoElev;
            }

            return worldPoints;
        }
    }
}

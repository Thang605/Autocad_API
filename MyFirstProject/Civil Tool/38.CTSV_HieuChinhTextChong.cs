// (C) Copyright 2026 by T27
// Lệnh hiệu chỉnh text chồng lên nhau trong Section Data Band Label Group
// Thao tác trực tiếp trên grip (DraggedOffset) của LabelGroupSubEntity
//
// Cách hoạt động:
// 1. User chọn 1 SectionView → xác định nhóm cắt ngang
// 2. Lệnh truy cập SectionDataBandLabelGroup qua API
// 3. Duyệt các sub-entity, phát hiện chồng lấn qua GeometricExtents
// 4. Dời các label chồng bằng cách set DraggedOffset
//
using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.ApplicationServices;

using Autodesk.Civil.DatabaseServices;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using AcadEntity = Autodesk.AutoCAD.DatabaseServices.Entity;
using CivilLabel = Autodesk.Civil.DatabaseServices.Label;
using MyFirstProject.Extensions;

[assembly: CommandClass(typeof(Civil3DCsharp.CTSV_HieuChinhTextChong_Commands))]

namespace Civil3DCsharp
{
    public class CTSV_HieuChinhTextChong_Commands
    {
        /// <summary>
        /// Thông tin bounding box của 1 sub-entity label trong band
        /// </summary>
        private class LabelSubInfo
        {
            public int Index { get; set; }
            public ObjectId LabelGroupId { get; set; }
            public Point3d LabelLocation { get; set; }
            public bool IsVertical { get; set; }
            public bool HasTextMatch { get; set; }
            public bool IsDragged { get; set; }
            public double MinX { get; set; }
            public double MaxX { get; set; }
            public double MinY { get; set; }
            public double MaxY { get; set; }
        }

        /// <summary>
        /// Lệnh CTSV_HieuChinhTextChong - Hiệu chỉnh text chồng lên nhau
        /// trong Section Data Band Label Group bằng cách điều chỉnh grip (DraggedOffset)
        /// </summary>
        [CommandMethod("CTSV_HieuChinhTextChong")]
        public static void CTSVHieuChinhTextChong()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            var db = doc.Database;

            try
            {
                ed.WriteMessage("\n=== CTSV_HieuChinhTextChong - Hiệu chỉnh label chồng trong Band ===");

                double gap = 0.15;
                string method = "";
                ObjectId selectedSectionViewId = ObjectId.Null;
                HashSet<int> selectedBandIndices = null;

                using (var form = new CTSV_HieuChinhTextChong_Form(ed, db))
                {
                    var res = Application.ShowModalDialog(form);
                    if (res != System.Windows.Forms.DialogResult.OK)
                    {
                        ed.WriteMessage("\nĐã huỷ lệnh.");
                        return;
                    }

                    gap = form.GapValue;
                    method = form.Method;
                    selectedSectionViewId = form.SelectedSectionViewId;
                    selectedBandIndices = form.SelectedBandIndices;
                }

                if (method == "Reset")
                {
                    ResetBandLabels(ed, db);
                }
                else if (method == "ChonDoiTuong")
                {
                    ProcessSelectedBandLabelGroup(ed, db, gap);
                }
                else // SectionView
                {
                    ProcessViaSectionViewDirect(ed, db, gap, selectedSectionViewId, selectedBandIndices);
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n❌ Lỗi: {ex.Message}");
                ed.WriteMessage($"\n   StackTrace: {ex.StackTrace}");
            }
        }

        #region === Phương thức 1: Chọn SectionView → xử lý toàn bộ nhóm ===

        private static void ProcessViaSectionViewDirect(Editor ed, Database db, double gap, ObjectId sectionViewId, HashSet<int> selectedBandIndices)
        {
            if (sectionViewId == ObjectId.Null) return;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                SectionView sv = tr.GetObject(sectionViewId, OpenMode.ForWrite) as SectionView;
                if (sv == null)
                {
                    ed.WriteMessage("\n⚠ Không thể mở SectionView.");
                    return;
                }

                // Lấy SampleLineGroup → SectionViewGroup
                SampleLine sl = tr.GetObject(sv.SampleLineId, OpenMode.ForRead) as SampleLine;
                if (sl == null) return;

                SampleLineGroup slGroup = tr.GetObject(sl.GroupId, OpenMode.ForRead) as SampleLineGroup;
                if (slGroup == null) return;

                // Tìm SectionViewGroup chứa section view đã chọn
                List<ObjectId> allSectionViewIds = new List<ObjectId>();
                SectionViewGroupCollection svGroups = slGroup.SectionViewGroups;
                foreach (SectionViewGroup svGroup in svGroups)
                {
                    ObjectIdCollection svIdColl = svGroup.GetSectionViewIds();
                    if (svIdColl.Contains(sectionViewId))
                    {
                        foreach (ObjectId id in svIdColl)
                        {
                            allSectionViewIds.Add(id);
                        }
                        break;
                    }
                }

                if (allSectionViewIds.Count == 0)
                {
                    ed.WriteMessage("\n⚠ Không tìm thấy nhóm cắt ngang.");
                    tr.Commit();
                    return;
                }

                ed.WriteMessage($"\n📐 Tìm thấy {allSectionViewIds.Count} cắt ngang trong nhóm.");

                int totalAdjusted = 0;
                int totalSV = 0;

                // Bước 2: Xử lý từng SectionView
                foreach (ObjectId svId in allSectionViewIds)
                {
                    try
                    {
                        int adjusted = ProcessSectionViewBandLabels(svId, gap, tr, ed, selectedBandIndices);
                        totalAdjusted += adjusted;
                        if (adjusted > 0) totalSV++;
                    }
                    catch (System.Exception ex)
                    {
                        ed.WriteMessage($"\n⚠ Lỗi SectionView: {ex.Message}");
                    }
                }

                tr.Commit();
                ed.WriteMessage($"\n✅ Đã hiệu chỉnh {totalAdjusted} label trong {totalSV}/{allSectionViewIds.Count} cắt ngang.");
            }
        }

        #endregion

        #region === Phương thức 2: Chọn trực tiếp đối tượng Band Label Group ===

        private static void ProcessSelectedBandLabelGroup(Editor ed, Database db, double gap)
        {
            while (true)
            {
                var selOpts = new PromptSelectionOptions
                {
                    MessageForAdding = "\nChọn đối tượng Section Data Band Label Group (Esc để kết thúc): "
                };
                var selResult = ed.GetSelection(selOpts);

                if (selResult.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n Kết thúc lệnh.");
                    break;
                }

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    int totalAdjusted = 0;

                    foreach (SelectedObject selObj in selResult.Value)
                    {
                        if (selObj == null) continue;

                        try
                        {
                            var dbObj = tr.GetObject(selObj.ObjectId, OpenMode.ForWrite);

                            // Kiểm tra nếu là SectionDataBandLabelGroup
                            if (dbObj is LabelGroup labelGroup)
                            {
                                int adjusted = ProcessSingleLabelGroup(labelGroup, selObj.ObjectId, gap, tr, ed);
                                totalAdjusted += adjusted;
                            }
                            // Nếu chọn SectionView → lấy band label groups từ SectionView
                            else if (dbObj is SectionView svObj)
                            {
                                int adjusted = ProcessSectionViewBandLabels(selObj.ObjectId, gap, tr, ed);
                                totalAdjusted += adjusted;
                            }
                        }
                        catch (System.Exception ex)
                        {
                            ed.WriteMessage($"\n⚠ Lỗi: {ex.Message}");
                        }
                    }

                    tr.Commit();
                    ed.WriteMessage($"\n✅ Đã hiệu chỉnh {totalAdjusted} label.");
                }
            }
        }

        #endregion

        #region === Phương thức 3: Reset tất cả label về vị trí gốc ===

        private static void ResetBandLabels(Editor ed, Database db)
        {
            ObjectId sectionViewId = UserInput.GSectionView("Chọn 1 trắc ngang để reset label:\n");
            if (sectionViewId == ObjectId.Null) return;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                SectionView sv = tr.GetObject(sectionViewId, OpenMode.ForRead) as SectionView;
                if (sv == null) return;

                SampleLine sl = tr.GetObject(sv.SampleLineId, OpenMode.ForRead) as SampleLine;
                if (sl == null) return;

                SampleLineGroup slGroup = tr.GetObject(sl.GroupId, OpenMode.ForRead) as SampleLineGroup;
                if (slGroup == null) return;

                List<ObjectId> allSVIds = new List<ObjectId>();
                foreach (SectionViewGroup svGroup in slGroup.SectionViewGroups)
                {
                    ObjectIdCollection svIdColl = svGroup.GetSectionViewIds();
                    if (svIdColl.Contains(sectionViewId))
                    {
                        foreach (ObjectId id in svIdColl) allSVIds.Add(id);
                        break;
                    }
                }

                int resetCount = 0;
                foreach (ObjectId svId in allSVIds)
                {
                    try
                    {
                        // Tìm band label groups
                        ObjectIdCollection labelGroupIds = SectionDataBandLabelGroup.GetAvailableLabelGroupIds(svId);

                        foreach (ObjectId lgId in labelGroupIds)
                        {
                            LabelGroup labelGroup = tr.GetObject(lgId, OpenMode.ForWrite) as LabelGroup;
                            if (labelGroup == null) continue;

                            labelGroup.ResetAllSubCommonLabelLocations();
                            resetCount++;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        ed.WriteMessage($"\n⚠ Lỗi reset: {ex.Message}");
                    }
                }

                tr.Commit();
                ed.WriteMessage($"\n✅ Đã reset {resetCount} label group trong {allSVIds.Count} cắt ngang.");
            }
        }

        #endregion

        #region === Xử lý Band Label cho 1 SectionView ===

        /// <summary>
        /// Xử lý tất cả SectionDataBandLabelGroup trong 1 SectionView
        /// </summary>
        private static int ProcessSectionViewBandLabels(ObjectId sectionViewId, double gap, Transaction tr, Editor ed, HashSet<int> selectedBandIndices = null)
        {
            int totalAdjusted = 0;

            try
            {
                // Lấy tất cả band label groups
                ObjectIdCollection labelGroupIds = SectionDataBandLabelGroup.GetAvailableLabelGroupIds(sectionViewId);

                if (labelGroupIds.Count == 0)
                {
                    return 0;
                }

                for (int i = 0; i < labelGroupIds.Count; i++)
                {
                    if (selectedBandIndices != null && !selectedBandIndices.Contains(i))
                    {
                        continue;
                    }

                    ObjectId lgId = labelGroupIds[i];

                    try
                    {
                        LabelGroup labelGroup = tr.GetObject(lgId, OpenMode.ForWrite) as LabelGroup;
                        if (labelGroup == null) continue;

                        int adjusted = ProcessSingleLabelGroup(labelGroup, lgId, gap, tr, ed);
                        totalAdjusted += adjusted;
                    }
                    catch (System.Exception ex)
                    {
                        ed.WriteMessage($"\n⚠ Lỗi label group: {ex.Message}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n⚠ Lỗi GetAvailableLabelGroupIds: {ex.Message}");
                // Fallback: tìm trong ModelSpace
                totalAdjusted += ProcessSectionViewBandLabels_Fallback(sectionViewId, gap, tr, ed, selectedBandIndices);
            }

            return totalAdjusted;
        }

        /// <summary>
        /// Fallback: Tìm SectionDataBandLabelGroup bằng cách duyệt ModelSpace
        /// </summary>
        private static int ProcessSectionViewBandLabels_Fallback(ObjectId sectionViewId, double gap, Transaction tr, Editor ed, HashSet<int> selectedBandIndices = null)
        {
            if (selectedBandIndices != null)
            {
                ed.WriteMessage("\n⚠ Chế độ tìm kiếm dự phòng không hỗ trợ phân biệt Band index. Sẽ xử lý tất cả các Band tìm thấy.");
            }
            int totalAdjusted = 0;
            Database db = sectionViewId.Database;

            BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
            if (bt == null) return 0;

            BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
            if (btr == null) return 0;

            // Lấy extents của section view để tìm label group gần đó
            SectionView sv = tr.GetObject(sectionViewId, OpenMode.ForRead) as SectionView;
            if (sv == null) return 0;

            Extents3d svExtents;
            try { svExtents = sv.GeometricExtents; }
            catch { return 0; }

            foreach (ObjectId entId in btr)
            {
                try
                {
                    var dbObj = tr.GetObject(entId, OpenMode.ForRead);
                    AcadEntity ent = dbObj as AcadEntity;
                    if (ent == null) continue;

                    // Kiểm tra DxfName cho label group
                    string dxfName = ent.GetRXClass().DxfName;
                    if (!dxfName.Contains("AECC") || !dxfName.Contains("BAND")) continue;

                    // Kiểm tra nếu label group nằm gần section view
                    try
                    {
                        Extents3d lgExtents = ent.GeometricExtents;
                        bool isNearSV = lgExtents.MinPoint.X < svExtents.MaxPoint.X + 1 &&
                                        lgExtents.MaxPoint.X > svExtents.MinPoint.X - 1 &&
                                        lgExtents.MinPoint.Y < svExtents.MaxPoint.Y + 1 &&
                                        lgExtents.MaxPoint.Y > svExtents.MinPoint.Y - 1;
                        if (!isNearSV) continue;
                    }
                    catch { continue; }

                    if (dbObj is LabelGroup labelGroup)
                    {
                        // Mở ForWrite
                        labelGroup = tr.GetObject(entId, OpenMode.ForWrite) as LabelGroup;
                        if (labelGroup != null)
                        {
                            int adjusted = ProcessSingleLabelGroup(labelGroup, entId, gap, tr, ed);
                            totalAdjusted += adjusted;
                        }
                    }
                }
                catch { }
            }

            return totalAdjusted;
        }

        #endregion

        #region === Xử lý chồng lấn cho 1 LabelGroup ===

        /// <summary>
        /// Xử lý chồng lấn cho 1 LabelGroup:
        /// - Grip xen kẽ: text, anchor, text, anchor... (hoặc ngược lại)
        /// - Text đứng (vertical) → dịch theo phương ngang (X)
        /// - Dùng Explode để xác định bounds thực tế
        /// </summary>
        private static int ProcessSingleLabelGroup(LabelGroup labelGroup, ObjectId labelGroupId, double gap, Transaction tr, Editor ed)
        {
            uint subCount = labelGroup.SubEntityCount;
            if (subCount < 2) return 0;

            // === Bước 1: Thu thập tất cả sub-entity ===
            List<LabelSubInfo> allSubs = new List<LabelSubInfo>();

            for (uint i = 0; i < subCount; i++)
            {
                try
                {
                    LabelGroupSubEntity subEntity = labelGroup.GetAt(i);
                    if (subEntity == null) continue;

                    Point3d labelLoc;
                    try { labelLoc = subEntity.LabelLocation; }
                    catch { continue; }

                    // Thu thập thông tin chẩn đoán
                    bool isDragged = false;
                    try { isDragged = subEntity.Dragged; } catch { }

                    allSubs.Add(new LabelSubInfo
                    {
                        Index = (int)i,
                        LabelGroupId = labelGroupId,
                        LabelLocation = labelLoc,
                        IsVertical = true,
                        HasTextMatch = false,
                        IsDragged = isDragged,
                        MinX = labelLoc.X,
                        MaxX = labelLoc.X,
                        MinY = labelLoc.Y,
                        MaxY = labelLoc.Y
                    });
                }
                catch { }
            }

            if (allSubs.Count < 3) return 0;

            ed.WriteMessage($"\n  📊 Tổng {allSubs.Count} sub-entity trong label group.");

            // === Bước 2: Explode đệ quy (≥2 lần) để lấy text bounds ===
            List<TextBoundsData> textBounds = new List<TextBoundsData>();
            try
            {
                AcadEntity labelGroupEntity = labelGroup as AcadEntity;
                if (labelGroupEntity != null)
                {
                    var allExploded = ExplodeRecursive(labelGroupEntity);

                    foreach (var obj in allExploded)
                    {
                        if (obj is DBText || obj is MText)
                        {
                            AcadEntity textEnt = obj as AcadEntity;
                            if (textEnt == null) continue;

                            try
                            {
                                Extents3d ext = textEnt.GeometricExtents;
                                textBounds.Add(new TextBoundsData
                                {
                                    CenterX = (ext.MinPoint.X + ext.MaxPoint.X) / 2.0,
                                    CenterY = (ext.MinPoint.Y + ext.MaxPoint.Y) / 2.0,
                                    MinX = ext.MinPoint.X,
                                    MaxX = ext.MaxPoint.X,
                                    MinY = ext.MinPoint.Y,
                                    MaxY = ext.MaxPoint.Y
                                });
                            }
                            catch { }
                        }
                    }

                    // Dispose tất cả
                    foreach (var obj in allExploded)
                    {
                        if (obj is IDisposable disposable)
                            disposable.Dispose();
                    }
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n  ⚠ Không thể Explode: {ex.Message}");
            }

            ed.WriteMessage($"\n  📝 Tìm thấy {textBounds.Count} text sau Explode.");

            if (textBounds.Count < 2)
            {
                ed.WriteMessage("\n  ⚠ Không đủ text entity để xử lý (cần Explode ≥2 lần).");
                return 0;
            }

            // === Bước 3: Match-by-proximity — từng text → grip gần nhất ===
            HashSet<int> matchedGripIndices = new HashSet<int>();

            foreach (var text in textBounds)
            {
                double bestDist = double.MaxValue;
                int bestIdx = -1;

                for (int i = 0; i < allSubs.Count; i++)
                {
                    if (matchedGripIndices.Contains(i)) continue; // Đã match rồi

                    double dx = allSubs[i].LabelLocation.X - text.CenterX;
                    double dy = allSubs[i].LabelLocation.Y - text.CenterY;
                    double dist = Math.Sqrt(dx * dx + dy * dy);

                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestIdx = i;
                    }
                }

                if (bestIdx >= 0)
                {
                    matchedGripIndices.Add(bestIdx);
                    allSubs[bestIdx].MinX = text.MinX;
                    allSubs[bestIdx].MaxX = text.MaxX;
                    allSubs[bestIdx].MinY = text.MinY;
                    allSubs[bestIdx].MaxY = text.MaxY;
                    allSubs[bestIdx].HasTextMatch = true;
                }
            }

            var textGrips = allSubs.Where(g => g.HasTextMatch).OrderBy(g => g.LabelLocation.X).ToList();
            ed.WriteMessage($"\n  ✓ Xác định {textGrips.Count}/{allSubs.Count} grip là TEXT (proximity match)");

            if (textGrips.Count < 2) return 0;

            // === Bước 4: Xử lý chồng lấn - text đứng → dịch ngang (X) ===
            return ResolveHorizontalOverlaps(textGrips, gap, labelGroup, tr, ed);
        }

        /// <summary>
        /// Explode đệ quy: nếu gặp BlockReference thì explode tiếp
        /// </summary>
        private static List<Autodesk.AutoCAD.DatabaseServices.DBObject> ExplodeRecursive(AcadEntity entity, int maxDepth = 3)
        {
            var result = new List<Autodesk.AutoCAD.DatabaseServices.DBObject>();
            var queue = new Queue<Tuple<AcadEntity, int>>();
            queue.Enqueue(Tuple.Create(entity, 0));

            while (queue.Count > 0)
            {
                var (ent, depth) = queue.Dequeue();
                if (depth > maxDepth) continue;

                try
                {
                    DBObjectCollection exploded = new DBObjectCollection();
                    ent.Explode(exploded);

                    foreach (Autodesk.AutoCAD.DatabaseServices.DBObject obj in exploded)
                    {
                        if (obj is BlockReference blockRef && depth < maxDepth)
                        {
                            queue.Enqueue(Tuple.Create((AcadEntity)blockRef, depth + 1));
                        }
                        else
                        {
                            result.Add(obj);
                        }
                    }
                }
                catch { }
            }

            return result;
        }

        /// <summary>
        /// Dữ liệu bounds từ text Exploded
        /// </summary>
        private class TextBoundsData
        {
            public double CenterX { get; set; }
            public double CenterY { get; set; }
            public double MinX { get; set; }
            public double MaxX { get; set; }
            public double MinY { get; set; }
            public double MaxY { get; set; }
        }

        /// <summary>
        /// Xử lý chồng lấn cho text đứng: tách thành 2 nhóm trái/phải tim đường
        /// Bên trái → đẩy sang trái, Bên phải → đẩy sang phải
        /// </summary>
        private static int ResolveHorizontalOverlaps(List<LabelSubInfo> labels, double gap,
            LabelGroup labelGroup, Transaction tr, Editor ed)
        {
            int adjustedCount = 0;

            // Xác định tim đường = trung điểm X của tất cả label
            double minLabelX = labels.Min(l => l.LabelLocation.X);
            double maxLabelX = labels.Max(l => l.LabelLocation.X);
            double centerX = (minLabelX + maxLabelX) / 2.0;

            ed.WriteMessage($"\n  📍 Tim đường X ≈ {centerX:F2} (range: {minLabelX:F2} → {maxLabelX:F2})");

            // Tách thành 2 nhóm
            var leftLabels = labels.Where(l => l.LabelLocation.X < centerX).ToList();
            var rightLabels = labels.Where(l => l.LabelLocation.X >= centerX).ToList();

            ed.WriteMessage($"\n  ◀ Trái: {leftLabels.Count} labels, ▶ Phải: {rightLabels.Count} labels");

            // Xử lý nhóm BÊN PHẢI: sắp xếp trái→phải, đẩy sang phải khi chồng
            if (rightLabels.Count >= 2)
            {
                rightLabels.Sort((a, b) => a.MinX.CompareTo(b.MinX));

                for (int i = 1; i < rightLabels.Count; i++)
                {
                    for (int j = 0; j < i; j++)
                    {
                        if (AreOverlapping(rightLabels[j], rightLabels[i], gap))
                        {
                            double targetMinX = rightLabels[j].MaxX + gap;
                            double deltaX = targetMinX - rightLabels[i].MinX;
                            if (deltaX <= 0) continue;

                            if (MoveLabelHorizontal(rightLabels[i], deltaX, labelGroup, ed))
                                adjustedCount++;
                        }
                    }
                }
            }

            // Xử lý nhóm BÊN TRÁI: sắp xếp phải→trái (MaxX giảm dần), đẩy sang trái khi chồng
            if (leftLabels.Count >= 2)
            {
                leftLabels.Sort((a, b) => b.MaxX.CompareTo(a.MaxX)); // Phải → trái

                for (int i = 1; i < leftLabels.Count; i++)
                {
                    for (int j = 0; j < i; j++)
                    {
                        if (AreOverlapping(leftLabels[j], leftLabels[i], gap))
                        {
                            // Đẩy sang trái: MaxX của label i phải < MinX của label j - gap
                            double targetMaxX = leftLabels[j].MinX - gap;
                            double deltaX = targetMaxX - leftLabels[i].MaxX;
                            if (deltaX >= 0) continue; // Đã đủ xa

                            if (MoveLabelHorizontal(leftLabels[i], deltaX, labelGroup, ed))
                                adjustedCount++;
                        }
                    }
                }
            }

            return adjustedCount;
        }

        /// <summary>
        /// Di chuyển 1 label theo phương ngang (X)
        /// </summary>
        private static bool MoveLabelHorizontal(LabelSubInfo label, double deltaX,
            LabelGroup labelGroup, Editor ed)
        {
            try
            {
                LabelGroupSubEntity subEntity = labelGroup.GetAt((uint)label.Index);

                Point3d currentLoc = label.LabelLocation;
                Point3d newLoc = new Point3d(currentLoc.X + deltaX, currentLoc.Y, currentLoc.Z);
                subEntity.LabelLocation = newLoc;
                label.LabelLocation = newLoc;

                // Cập nhật bounds
                label.MinX += deltaX;
                label.MaxX += deltaX;

                string direction = deltaX > 0 ? "phải" : "trái";
                ed.WriteMessage($"\n  → Dời label[{label.Index}] sang {direction} {Math.Abs(deltaX):F2}");
                return true;
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n  ⚠ Không thể dời label {label.Index}: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region === Helper Methods ===

        /// <summary>
        /// Lấy Layer của DBText/MText bên trong LabelGroup thông qua Explode
        /// </summary>
        internal static string GetTextLayerFromLabelGroup(LabelGroup lg)
        {
            string layer = "N/A";
            try
            {
                if (lg.Layer != null) layer = lg.Layer; // Fallback layer của Group

                AcadEntity labelGroupEntity = lg as AcadEntity;
                if (labelGroupEntity != null)
                {
                    var allExploded = ExplodeRecursive(labelGroupEntity, 2);
                    foreach (var obj in allExploded)
                    {
                        if (obj is DBText dbText)
                        {
                            layer = dbText.Layer;
                            break;
                        }
                        else if (obj is MText mText)
                        {
                            layer = mText.Layer;
                            break;
                        }
                    }

                    foreach (var obj in allExploded)
                    {
                        if (obj is IDisposable disposable)
                            disposable.Dispose();
                    }
                }
            }
            catch { }
            return layer;
        }

        /// <summary>
        /// Kiểm tra 2 label có chồng nhau không
        /// </summary>
        private static bool AreOverlapping(LabelSubInfo a, LabelSubInfo b, double gap)
        {
            bool overlapX = (a.MinX - gap) < b.MaxX && (a.MaxX + gap) > b.MinX;
            bool overlapY = (a.MinY - gap) < b.MaxY && (a.MaxY + gap) > b.MinY;
            return overlapX && overlapY;
        }

        #endregion
    }

    public class CTSV_HieuChinhTextChong_Form : System.Windows.Forms.Form
    {
        public System.Windows.Forms.NumericUpDown numGap;
        public System.Windows.Forms.RadioButton rbGroupMode;
        public System.Windows.Forms.RadioButton rbManualMode;
        public System.Windows.Forms.RadioButton rbResetMode;
        public System.Windows.Forms.Button btnPickSectionView;
        public System.Windows.Forms.CheckedListBox clbBands;
        public System.Windows.Forms.Button btnSelectAll;
        public System.Windows.Forms.Button btnDeselectAll;
        public System.Windows.Forms.Button btnOk;
        public System.Windows.Forms.Button btnCancel;

        public double GapValue => (double)numGap.Value;
        public string Method 
        {
            get {
                if (rbGroupMode.Checked) return "SectionView";
                if (rbManualMode.Checked) return "ChonDoiTuong";
                return "Reset";
            }
        }
        
        public HashSet<int> SelectedBandIndices { get; private set; }
        public ObjectId SelectedSectionViewId { get; private set; }

        private Editor _ed;
        private Database _db;

        public CTSV_HieuChinhTextChong_Form(Editor ed, Database db)
        {
            _ed = ed;
            _db = db;
            SelectedSectionViewId = ObjectId.Null;
            SelectedBandIndices = new HashSet<int>();

            InitializeComponents();
        }

        private void InitializeComponents()
        {
            this.Text = "Hiệu chỉnh Text chồng Section View Bands";
            this.Size = new System.Drawing.Size(420, 520);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Group 1: Configuration
            var grpConfig = new System.Windows.Forms.GroupBox();
            grpConfig.Text = "Cài đặt chung";
            grpConfig.Location = new System.Drawing.Point(12, 12);
            grpConfig.Size = new System.Drawing.Size(380, 60);

            var lblGap = new System.Windows.Forms.Label();
            lblGap.Text = "Khoảng cách cách chữ (Gap):";
            lblGap.Location = new System.Drawing.Point(15, 25);
            lblGap.AutoSize = true;

            numGap = new System.Windows.Forms.NumericUpDown();
            numGap.Location = new System.Drawing.Point(200, 23);
            numGap.Size = new System.Drawing.Size(100, 20);
            numGap.DecimalPlaces = 2;
            numGap.Increment = 0.05M;
            numGap.Minimum = 0;
            numGap.Maximum = 100;
            numGap.Value = 0.15M;

            grpConfig.Controls.Add(lblGap);
            grpConfig.Controls.Add(numGap);

            // Group 2: Chế độ hoạt động
            var grpMode = new System.Windows.Forms.GroupBox();
            grpMode.Text = "Chế độ hoạt động";
            grpMode.Location = new System.Drawing.Point(12, 80);
            grpMode.Size = new System.Drawing.Size(380, 100);

            rbGroupMode = new System.Windows.Forms.RadioButton();
            rbGroupMode.Text = "1. Xử lý tự động theo nhóm Section View Group";
            rbGroupMode.Location = new System.Drawing.Point(15, 20);
            rbGroupMode.Size = new System.Drawing.Size(350, 20);
            rbGroupMode.Checked = true;
            rbGroupMode.CheckedChanged += ModeChanged;

            rbManualMode = new System.Windows.Forms.RadioButton();
            rbManualMode.Text = "2. Chọn thủ công đối tượng Section Data Band Label Group";
            rbManualMode.Location = new System.Drawing.Point(15, 45);
            rbManualMode.Size = new System.Drawing.Size(350, 20);

            rbResetMode = new System.Windows.Forms.RadioButton();
            rbResetMode.Text = "3. Khôi phục vị trí gốc của Text (Reset)";
            rbResetMode.Location = new System.Drawing.Point(15, 70);
            rbResetMode.Size = new System.Drawing.Size(350, 20);

            grpMode.Controls.Add(rbGroupMode);
            grpMode.Controls.Add(rbManualMode);
            grpMode.Controls.Add(rbResetMode);

            // Group 3: Chọn Band
            var grpBands = new System.Windows.Forms.GroupBox();
            grpBands.Text = "Lựa chọn Band cần xử lý (Dành cho Chế độ 1)";
            grpBands.Location = new System.Drawing.Point(12, 190);
            grpBands.Size = new System.Drawing.Size(380, 220);

            btnPickSectionView = new System.Windows.Forms.Button();
            btnPickSectionView.Text = "🎯 Chọn 1 trắc ngang mẫu trên bản vẽ...";
            btnPickSectionView.Location = new System.Drawing.Point(15, 20);
            btnPickSectionView.Size = new System.Drawing.Size(350, 30);
            btnPickSectionView.Click += BtnPickSectionView_Click;

            clbBands = new System.Windows.Forms.CheckedListBox();
            clbBands.Location = new System.Drawing.Point(15, 60);
            clbBands.Size = new System.Drawing.Size(350, 110);
            clbBands.CheckOnClick = true;

            btnSelectAll = new System.Windows.Forms.Button();
            btnSelectAll.Text = "Chọn tất cả";
            btnSelectAll.Location = new System.Drawing.Point(15, 180);
            btnSelectAll.Size = new System.Drawing.Size(100, 25);
            btnSelectAll.Click += (s, e) => {
                for (int i = 0; i < clbBands.Items.Count; i++) clbBands.SetItemChecked(i, true);
            };

            btnDeselectAll = new System.Windows.Forms.Button();
            btnDeselectAll.Text = "Bỏ chọn tất cả";
            btnDeselectAll.Location = new System.Drawing.Point(125, 180);
            btnDeselectAll.Size = new System.Drawing.Size(100, 25);
            btnDeselectAll.Click += (s, e) => {
                for (int i = 0; i < clbBands.Items.Count; i++) clbBands.SetItemChecked(i, false);
            };

            grpBands.Controls.Add(btnPickSectionView);
            grpBands.Controls.Add(clbBands);
            grpBands.Controls.Add(btnSelectAll);
            grpBands.Controls.Add(btnDeselectAll);

            // Nút OK Cancel
            btnOk = new System.Windows.Forms.Button();
            btnOk.Text = "THỰC THI";
            btnOk.DialogResult = System.Windows.Forms.DialogResult.OK;
            btnOk.Location = new System.Drawing.Point(216, 430);
            btnOk.Size = new System.Drawing.Size(90, 30);

            btnCancel = new System.Windows.Forms.Button();
            btnCancel.Text = "Huỷ bỏ";
            btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            btnCancel.Location = new System.Drawing.Point(312, 430);
            btnCancel.Size = new System.Drawing.Size(80, 30);

            this.Controls.Add(grpConfig);
            this.Controls.Add(grpMode);
            this.Controls.Add(grpBands);
            this.Controls.Add(btnOk);
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;

            this.FormClosing += CTSV_HieuChinhTextChong_Form_FormClosing;
        }

        private void ModeChanged(object sender, EventArgs e)
        {
            // Enable/disable the Band selection group based on mode
            bool isGroupMode = rbGroupMode.Checked;
            btnPickSectionView.Enabled = isGroupMode;
            clbBands.Enabled = isGroupMode;
            btnSelectAll.Enabled = isGroupMode;
            btnDeselectAll.Enabled = isGroupMode;
        }

        private void BtnPickSectionView_Click(object sender, EventArgs e)
        {
            // Using EditorUserInteraction to hide modal window and prompt on screen
            using (Autodesk.AutoCAD.EditorInput.EditorUserInteraction interaction = _ed.StartUserInteraction(this))
            {
                PromptEntityOptions peo = new PromptEntityOptions("\nChọn 1 trắc ngang mẫu (SectionView): ");
                peo.SetRejectMessage("\nHãy chọn đối tượng SectionView.");
                peo.AddAllowedClass(typeof(SectionView), true);
                
                PromptEntityResult per = _ed.GetEntity(peo);
                if (per.Status == PromptStatus.OK)
                {
                    SelectedSectionViewId = per.ObjectId;
                    LoadBands();
                }
            }
        }

        private void LoadBands()
        {
            if (SelectedSectionViewId == ObjectId.Null) return;
            clbBands.Items.Clear();

            using (Transaction tr = _db.TransactionManager.StartTransaction())
            {
                try
                {
                    SectionView sv = tr.GetObject(SelectedSectionViewId, OpenMode.ForRead) as SectionView;
                    if (sv == null) return;

                    btnPickSectionView.Text = $"✅ Đã chọn (Lý trình: {sv.Name})";

                    ObjectIdCollection labelGroupIds = SectionDataBandLabelGroup.GetAvailableLabelGroupIds(SelectedSectionViewId);
                    if (labelGroupIds == null || labelGroupIds.Count == 0)
                    {
                        System.Windows.Forms.MessageBox.Show("Không tìm thấy Label Group nào trên trắc ngang này.");
                        tr.Commit();
                        return;
                    }

                    for (int i = 0; i < labelGroupIds.Count; i++)
                    {
                        try
                        {
                            LabelGroup lg = tr.GetObject(labelGroupIds[i], OpenMode.ForRead) as LabelGroup;
                            if (lg != null)
                            {
                                string textLayer = CTSV_HieuChinhTextChong_Commands.GetTextLayerFromLabelGroup(lg);
                                clbBands.Items.Add($"Band #{i + 1} (Text layer: {textLayer})", true);
                            }
                            else
                            {
                                clbBands.Items.Add($"Band #{i + 1}", true);
                            }
                        }
                        catch
                        {
                            clbBands.Items.Add($"Band #{i + 1} (Lỗi đọc TT)", false);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    System.Windows.Forms.MessageBox.Show($"Lỗi tải Band: {ex.Message}");
                }
                
                tr.Commit();
            }
        }

        private void CTSV_HieuChinhTextChong_Form_FormClosing(object sender, System.Windows.Forms.FormClosingEventArgs e)
        {
            if (this.DialogResult == System.Windows.Forms.DialogResult.OK && rbGroupMode.Checked)
            {
                if (SelectedSectionViewId == ObjectId.Null)
                {
                    System.Windows.Forms.MessageBox.Show("Vui lòng kích chọn 1 Trắc ngang mẫu trước khi Thực thi ở Chế độ 1.", "Chưa chọn mặt cắt mẫu", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                    e.Cancel = true;
                    return;
                }

                SelectedBandIndices.Clear();
                foreach (int index in clbBands.CheckedIndices)
                {
                    SelectedBandIndices.Add(index);
                }
                
                if (SelectedBandIndices.Count == 0)
                {
                    System.Windows.Forms.MessageBox.Show("Vui lòng tích chọn ít nhất 1 Band Label Group để thực thi.", "Cảnh báo", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                    e.Cancel = true;
                }
            }
        }
    }
}

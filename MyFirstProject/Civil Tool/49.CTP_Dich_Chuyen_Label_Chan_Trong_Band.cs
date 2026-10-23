using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using CadEntity = Autodesk.AutoCAD.DatabaseServices.Entity;

// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3DCsharp.CTP_Dich_Chuyen_Label_Chan_Trong_Band_Commands))]

namespace Civil3DCsharp
{
    public class CTP_Dich_Chuyen_Label_Chan_Trong_Band_Commands
    {
        private class SubLabelInfo
        {
            public uint SubIndex { get; set; }
            public LabelGroupSubEntity Sub { get; set; }
            public Point3d Loc { get; set; }
        }

        [CommandMethod("CTP_Dich_Chuyen_Label_Chan")]
        public static void DichChuyenLabelChanTrongBand()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            Editor ed = doc.Editor;
            Database db = doc.Database;

            ed.WriteMessage("\n=== CTP_Dich_Chuyen_Label_Chan - Dịch chuyển nhãn trong Band (Bảo toàn Label Group) ===");

            // 1. Cho phép người dùng chọn trước trên CAD hoặc mở thẳng Form
            ObjectId[] initialIds = new ObjectId[0];
            PromptSelectionOptions pso = new PromptSelectionOptions
            {
                MessageForAdding = "\nChọn Band Label Group hoặc các nhãn trong Band (nhấn Enter để mở Form): "
            };

            PromptSelectionResult selResult = ed.GetSelection(pso);
            if (selResult.Status == PromptStatus.OK)
            {
                initialIds = selResult.Value.GetObjectIds();
            }

            // 2. Mở Form tương tác
            using (DichChuyenLabelChanTrongBandForm form = new DichChuyenLabelChanTrongBandForm(ed, db))
            {
                if (initialIds.Length > 0)
                {
                    form.SetSelectedIds(initialIds);
                }

                DialogResult result = Application.ShowModalDialog(form);
                if (result != DialogResult.OK)
                {
                    ed.WriteMessage("\nĐã hủy lệnh.");
                    return;
                }

                ObjectId[] selectedIds = form.SelectedIds;
                if (selectedIds == null || selectedIds.Length == 0)
                {
                    ed.WriteMessage("\nChưa có đối tượng nào được chọn để xử lý.");
                    return;
                }

                double offsetY = form.OffsetY;
                bool offsetPositive = form.OffsetPositive;
                double actualOffsetY = offsetPositive ? Math.Abs(offsetY) : -Math.Abs(offsetY);
                double offsetX = form.OffsetX;
                int step = form.StepInterval; // 2 hoặc 3
                int pattern = form.PatternIndex; // Mẫu theo chu kỳ

                // 3. Thực hiện hiệu chỉnh trong Transaction
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    try
                    {
                        int totalProcessedGroups = 0;
                        int totalMovedLabels = 0;

                        // Danh sách text thông thường nếu người dùng chọn text rời
                        List<(CadEntity ent, double x)> looseTexts = new List<(CadEntity, double)>();

                        foreach (ObjectId oid in selectedIds)
                        {
                            if (oid.IsNull || !oid.IsValid) continue;

                            CadEntity ent = tr.GetObject(oid, OpenMode.ForWrite) as CadEntity;
                            if (ent == null) continue;

                            // TRƯỜNG HỢP 1: Civil 3D Label Group (SectionDataBandLabelGroup, ProfileViewBandLabelGroup...)
                            if (ent is LabelGroup labelGroup)
                            {
                                int moved = ProcessCivilLabelGroup(labelGroup, step, pattern, actualOffsetY, offsetX, ed);
                                totalMovedLabels += moved;
                                totalProcessedGroups++;
                            }
                            // TRƯỜNG HỢP 2: Text rời (DBText / MText)
                            else if (ent is DBText || ent is MText)
                            {
                                double x = (ent is DBText dt) ? dt.Position.X : ((MText)ent).Location.X;
                                looseTexts.Add((ent, x));
                            }
                            // TRƯỜNG HỢP 3: Entity khác có GripPoints
                            else
                            {
                                int moved = ProcessGenericEntityGrips(ent, step, pattern, actualOffsetY, offsetX, ed);
                                totalMovedLabels += moved;
                            }
                        }

                        // Xử lý text rời nếu có
                        if (looseTexts.Count > 0)
                        {
                            int looseMoved = ProcessLooseTexts(looseTexts, step, pattern, actualOffsetY, offsetX, ed);
                            totalMovedLabels += looseMoved;
                        }

                        tr.Commit();

                        // Cập nhật lại đồ họa AutoCAD
                        ed.Regen();

                        string stepStr = $"Chu kỳ {step} đối tượng";
                        ed.WriteMessage($"\n\n✓ Hoàn tất! Đã dịch chuyển {totalMovedLabels} nhãn ({stepStr}) trong {totalProcessedGroups} Band Label Group.");
                        ed.WriteMessage($"\n  Độ dời: Y = {actualOffsetY:+0.00;-0.00} đvbv{(Math.Abs(offsetX) > 0.001 ? $", X = {offsetX:+0.00;-0.00} đvbv" : "")}.");
                        ed.WriteMessage("\n  ✓ 100% đối tượng Civil 3D được giữ nguyên vẹn (KHÔNG phân rã / explode).");
                    }
                    catch (System.Exception ex)
                    {
                        ed.WriteMessage($"\n✗ Lỗi trong quá trình xử lý: {ex.Message}\n{ex.StackTrace}");
                        tr.Abort();
                    }
                }
            }
        }

        /// <summary>
        /// Tính toán độ dời Y và X cho từng cột theo chu kỳ (bước N đối tượng tùy chọn) và mẫu đã chọn
        /// </summary>
        private static bool CalculateDeltas(int colIdx, int step, int pattern, double baseDeltaY, double baseDeltaX, out double curDeltaY, out double curDeltaX)
        {
            curDeltaY = 0;
            curDeltaX = 0;

            if (step <= 1)
            {
                // Dịch tất cả đối tượng (mọi cọc)
                curDeltaY = baseDeltaY;
                curDeltaX = baseDeltaX;
                return true;
            }

            if (step == 2)
            {
                // colIdx = 0 là Cột 1 (Lẻ)
                // colIdx = 1 là Cột 2 (Chẵn)
                bool isEven = (colIdx % 2 == 1);

                switch (pattern)
                {
                    case 0: // Vị trí Chẵn (Cột 2, 4, 6...)
                        if (!isEven) return false;
                        curDeltaY = baseDeltaY;
                        curDeltaX = baseDeltaX;
                        return true;

                    case 1: // Vị trí Lẻ (Cột 1, 3, 5...)
                        if (isEven) return false;
                        curDeltaY = baseDeltaY;
                        curDeltaX = baseDeltaX;
                        return true;

                    case 2: // So le 2 chiều (Chẵn Lên, Lẻ Xuống)
                        curDeltaY = isEven ? Math.Abs(baseDeltaY) : -Math.Abs(baseDeltaY);
                        curDeltaX = isEven ? baseDeltaX : -baseDeltaX;
                        return true;

                    default:
                        return false;
                }
            }
            else if (step == 3)
            {
                // colIdx % 3 == 0: Cột 1, 4, 7...
                // colIdx % 3 == 1: Cột 2, 5, 8...
                // colIdx % 3 == 2: Cột 3, 6, 9...
                int rem = colIdx % 3;

                switch (pattern)
                {
                    case 0: // Bậc thang 3 cấp (Cột 1: Giữ nguyên, Cột 2: +Y, Cột 3: +2Y)
                        if (rem == 0) return false;
                        if (rem == 1)
                        {
                            curDeltaY = baseDeltaY;
                            curDeltaX = baseDeltaX;
                        }
                        else
                        {
                            curDeltaY = baseDeltaY * 2.0;
                            curDeltaX = baseDeltaX * 2.0;
                        }
                        return true;

                    case 1: // So le 3 hướng (Cột 1: Giữ nguyên, Cột 2: +Y, Cột 3: -Y)
                        if (rem == 0) return false;
                        if (rem == 1)
                        {
                            curDeltaY = Math.Abs(baseDeltaY);
                            curDeltaX = baseDeltaX;
                        }
                        else
                        {
                            curDeltaY = -Math.Abs(baseDeltaY);
                            curDeltaX = -baseDeltaX;
                        }
                        return true;

                    case 2: // Vị trí thứ 2 (Cột 2, 5, 8...)
                        if (rem != 1) return false;
                        curDeltaY = baseDeltaY;
                        curDeltaX = baseDeltaX;
                        return true;

                    case 3: // Vị trí thứ 3 (Cột 3, 6, 9...)
                        if (rem != 2) return false;
                        curDeltaY = baseDeltaY;
                        curDeltaX = baseDeltaX;
                        return true;

                    case 4: // Vị trí thứ 1 (Cột 1, 4, 7...)
                        if (rem != 0) return false;
                        curDeltaY = baseDeltaY;
                        curDeltaX = baseDeltaX;
                        return true;

                    default:
                        return false;
                }
            }
            else // step >= 4 (hoặc số tùy chọn bất kỳ)
            {
                int rem = colIdx % step;

                switch (pattern)
                {
                    case 0: // Bậc thang N cấp (Cột 1: Giữ nguyên, Cột 2: +Y, ..., Cột N: +(N-1)Y)
                        if (rem == 0) return false;
                        curDeltaY = baseDeltaY * rem;
                        curDeltaX = baseDeltaX * rem;
                        return true;

                    case 1: // So le 2 chiều xen kẽ (Cột chẵn: +Y, Cột lẻ: -Y)
                        bool isEvenCol = (rem % 2 == 1);
                        double sign = baseDeltaY >= 0 ? 1.0 : -1.0;
                        curDeltaY = isEvenCol ? (Math.Abs(baseDeltaY) * sign) : (-Math.Abs(baseDeltaY) * sign);
                        curDeltaX = isEvenCol ? baseDeltaX : -baseDeltaX;
                        return true;

                    case 2: // So le xen kẽ (Cột chẵn: +Y, Cột lẻ: Giữ nguyên)
                        if (rem % 2 == 0) return false;
                        curDeltaY = baseDeltaY;
                        curDeltaX = baseDeltaX;
                        return true;

                    default:
                        // Các vị trí thứ 1, 2, ..., N
                        // pattern 3 -> Cột 1 (rem = 0)
                        // pattern 4 -> Cột 2 (rem = 1)
                        // ...
                        // pattern 3 + targetRem -> targetRem = pattern - 3
                        int targetRem = pattern - 3;
                        if (targetRem >= 0 && targetRem < step)
                        {
                            if (rem != targetRem) return false;
                            curDeltaY = baseDeltaY;
                            curDeltaX = baseDeltaX;
                            return true;
                        }
                        return false;
                }
            }
        }

        /// <summary>
        /// Hiệu chỉnh grip / sub-entity của Civil 3D LabelGroup trực tiếp qua LabelLocation
        /// </summary>
        private static int ProcessCivilLabelGroup(LabelGroup labelGroup, int step, int pattern, double deltaY, double deltaX, Editor ed)
        {
            if (!labelGroup.IsWriteEnabled) labelGroup.UpgradeOpen();

            uint subCount = labelGroup.SubEntityCount;
            if (subCount == 0)
            {
                ed.WriteMessage($"\n  ⚠ Label Group không có sub-entity nào.");
                return 0;
            }

            // Thu thập các sub-entity có tọa độ hợp lệ
            var subList = new List<SubLabelInfo>();
            for (uint i = 0; i < subCount; i++)
            {
                try
                {
                    LabelGroupSubEntity sub = labelGroup.GetAt(i);
                    if (sub == null) continue;

                    Point3d loc = sub.LabelLocation;
                    if (double.IsNaN(loc.X) || double.IsNaN(loc.Y)) continue;

                    subList.Add(new SubLabelInfo
                    {
                        SubIndex = i,
                        Sub = sub,
                        Loc = loc
                    });
                }
                catch { }
            }

            if (subList.Count == 0)
            {
                ed.WriteMessage($"\n  ⚠ Không đọc được tọa độ LabelLocation của các sub-entity.");
                return 0;
            }

            // Nhóm theo cột X (dung sai 0.05 đơn vị bản vẽ) để đồng bộ mọi nhãn tại cùng 1 cọc
            double tol = 0.05;
            var columns = subList
                .GroupBy(s => Math.Round(s.Loc.X / tol) * tol)
                .OrderBy(g => g.Key)
                .ToList();

            ed.WriteMessage($"\n  📊 Nhóm Band Label: tìm thấy {subList.Count} nhãn tại {columns.Count} vị trí cọc (sắp xếp theo X).");

            int movedCount = 0;
            for (int colIdx = 0; colIdx < columns.Count; colIdx++)
            {
                double curDeltaY, curDeltaX;
                if (!CalculateDeltas(colIdx, step, pattern, deltaY, deltaX, out curDeltaY, out curDeltaX))
                    continue;

                foreach (var item in columns[colIdx])
                {
                    try
                    {
                        Point3d curLoc = item.Sub.LabelLocation;
                        Point3d targetLoc = new Point3d(curLoc.X + curDeltaX, curLoc.Y + curDeltaY, curLoc.Z);
                        item.Sub.LabelLocation = targetLoc;
                        movedCount++;
                    }
                    catch (System.Exception ex)
                    {
                        ed.WriteMessage($"\n    ✗ Lỗi gán LabelLocation[{item.SubIndex}]: {ex.Message}");
                    }
                }
            }

            try
            {
                labelGroup.RecordGraphicsModified(true);
            }
            catch { }

            return movedCount;
        }

        /// <summary>
        /// Fallback cho entity thông thường có GripPoints
        /// </summary>
        private static int ProcessGenericEntityGrips(CadEntity ent, int step, int pattern, double deltaY, double deltaX, Editor ed)
        {
            try
            {
                if (!ent.IsWriteEnabled) ent.UpgradeOpen();

                Point3dCollection gripPts = new Point3dCollection();
                IntegerCollection osnaps = new IntegerCollection();
                IntegerCollection geomIds = new IntegerCollection();
                ent.GetGripPoints(gripPts, osnaps, geomIds);

                if (gripPts.Count == 0) return 0;

                // Sắp xếp các grip theo X
                var gripList = new List<(int index, Point3d pt)>();
                for (int i = 0; i < gripPts.Count; i++)
                {
                    gripList.Add((i, gripPts[i]));
                }
                gripList = gripList.OrderBy(g => g.pt.X).ToList();

                int moved = 0;
                for (int i = 0; i < gripList.Count; i++)
                {
                    double curY, curX;
                    if (!CalculateDeltas(i, step, pattern, deltaY, deltaX, out curY, out curX))
                        continue;

                    IntegerCollection moveIdx = new IntegerCollection();
                    moveIdx.Add(gripList[i].index);
                    Vector3d offset = new Vector3d(curX, curY, 0);
                    ent.MoveGripPointsAt(moveIdx, offset);
                    moved++;
                }

                return moved;
            }
            catch { return 0; }
        }

        /// <summary>
        /// Xử lý text rời nếu người dùng chọn các DBText / MText không thuộc LabelGroup
        /// </summary>
        private static int ProcessLooseTexts(List<(CadEntity ent, double x)> texts, int step, int pattern, double deltaY, double deltaX, Editor ed)
        {
            texts = texts.OrderBy(t => t.x).ToList();
            int moved = 0;

            for (int i = 0; i < texts.Count; i++)
            {
                double curY, curX;
                if (!CalculateDeltas(i, step, pattern, deltaY, deltaX, out curY, out curX))
                    continue;

                try
                {
                    CadEntity ent = texts[i].ent;
                    if (!ent.IsWriteEnabled) ent.UpgradeOpen();
                    Vector3d vec = new Vector3d(curX, curY, 0);
                    ent.TransformBy(Matrix3d.Displacement(vec));
                    moved++;
                }
                catch { }
            }

            return moved;
        }
    }
}

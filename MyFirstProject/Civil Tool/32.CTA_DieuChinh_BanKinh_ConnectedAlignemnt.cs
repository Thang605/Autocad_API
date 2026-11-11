using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using MyFirstProject.Civil_Tool;
using MyFirstProject.Extensions;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3DCsharp.CTA_DieuChinhBanKinh_ConnectedAlignment_Commands))]

namespace Civil3DCsharp
{
    /// <summary>
    /// Lệnh điều chỉnh bán kính đường cong trong Connected Alignment
    /// </summary>
    public class CTA_DieuChinhBanKinh_ConnectedAlignment_Commands
    {
        /// <summary>
        /// Lệnh chính - Điều chỉnh bán kính đường cong trong Alignment
        /// </summary>
        [CommandMethod("CTA_Dieu_Chinh_Ban_Kinh_Connected_Alignment")]
        public void CTA_DieuChinhBanKinh_ConnectedAlignment()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            CivilDocument civDoc = CivilApplication.ActiveDocument;

            try
            {
                // Prompt user to select multiple Alignments
                PromptSelectionOptions pso = new PromptSelectionOptions();
                pso.MessageForAdding = "\nChọn các Alignment cần điều chỉnh bán kính: ";

                SelectionFilter filter = new SelectionFilter(new TypedValue[] {
                    new TypedValue((int)DxfCode.Start, "AECC_ALIGNMENT")
                });

                PromptSelectionResult psr = ed.GetSelection(pso, filter);
                if (psr.Status != PromptStatus.OK || psr.Value.Count == 0)
                {
                    ed.WriteMessage("\nĐã hủy lệnh.");
                    return;
                }

                ObjectId[] selectedIds = psr.Value.GetObjectIds();
                ed.WriteMessage($"\n✅ Đã chọn {selectedIds.Length} Alignment.");

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    List<ArcInfo> allArcList = new List<ArcInfo>();

                    foreach (ObjectId alignmentId in selectedIds)
                    {
                        Alignment? alignment = tr.GetObject(alignmentId, OpenMode.ForRead) as Alignment;
                        if (alignment == null) continue;

                        ed.WriteMessage($"\n\n📐 Alignment: {alignment.Name}");

                        // Collect all arcs from this alignment
                        for (int i = 0; i < alignment.Entities.Count; i++)
                        {
                            AlignmentEntity entity = alignment.Entities.GetEntityByOrder(i);

                            if (entity.EntityType == AlignmentEntityType.Arc)
                            {
                                AlignmentArc? arc = entity as AlignmentArc;
                                if (arc != null)
                                {
                                    ArcInfo arcInfo = new ArcInfo
                                    {
                                        EntityId = (int)arc.EntityId,
                                        StartStation = arc.StartStation,
                                        EndStation = arc.EndStation,
                                        CurrentRadius = arc.Radius,
                                        NewRadius = arc.Radius,
                                        AlignmentArcObjectId = alignmentId,
                                        AlignmentName = alignment.Name
                                    };
                                    allArcList.Add(arcInfo);

                                    ed.WriteMessage($"\n  - Arc {arc.EntityId}: Station {arc.StartStation:F2} - {arc.EndStation:F2}, R = {arc.Radius:F2}m");
                                }
                            }
                        }
                    }

                    if (allArcList.Count == 0)
                    {
                        ed.WriteMessage("\n⚠️ Các Alignment được chọn không có đường cong (Arc) nào!");
                        MessageBox.Show("Các Alignment được chọn không có đường cong (Arc) nào!",
                            "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    ed.WriteMessage($"\n📊 Tổng cộng: {allArcList.Count} đường cong từ {selectedIds.Length} Alignment");

                    tr.Commit();

                    // Show form - hiển thị tên nhiều Alignment
                    string displayName = selectedIds.Length == 1
                        ? allArcList[0].AlignmentName
                        : $"{selectedIds.Length} Alignments được chọn";

                    DieuChinhBanKinhForm form = new DieuChinhBanKinhForm(displayName, allArcList);
                    Application.ShowModalDialog(form);

                    if (!form.DialogResult_OK)
                    {
                        ed.WriteMessage("\nĐã hủy lệnh.");
                        return;
                    }

                    // Apply changes - nhóm theo AlignmentId
                    var groupedByAlignment = new Dictionary<ObjectId, List<ArcInfo>>();
                    foreach (var arcInfo in form.ArcList)
                    {
                        if (!groupedByAlignment.ContainsKey(arcInfo.AlignmentArcObjectId))
                            groupedByAlignment[arcInfo.AlignmentArcObjectId] = new List<ArcInfo>();
                        groupedByAlignment[arcInfo.AlignmentArcObjectId].Add(arcInfo);
                    }

                    foreach (var kvp in groupedByAlignment)
                    {
                        ApplyRadiusChanges(kvp.Key, kvp.Value, ed);
                    }
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n❌ Lỗi: {ex.Message}");
                ed.WriteMessage($"\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Áp dụng các thay đổi bán kính cho Alignment
        /// Hỗ trợ cả Alignment thường và Connected Alignment
        /// </summary>
        private void ApplyRadiusChanges(ObjectId alignmentId, List<ArcInfo> arcList, Editor ed)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            int changedCount = 0;

            // Lock document for modification
            using (DocumentLock docLock = doc.LockDocument())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    try
                    {
                        Alignment? alignment = tr.GetObject(alignmentId, OpenMode.ForWrite) as Alignment;
                        if (alignment == null)
                        {
                            ed.WriteMessage("\nKhông thể mở Alignment để chỉnh sửa!");
                            return;
                        }

                        // Check if this is a Connected Alignment
                        ConnectedAlignmentInfo? connectedInfo = alignment.ConnectedAlignmentInfo;
                        bool isConnectedAlignment = connectedInfo != null;

                        ed.WriteMessage($"\n  📋 Loại Alignment: {(isConnectedAlignment ? "Connected Alignment" : "Alignment thường")}");

                        if (isConnectedAlignment && connectedInfo != null)
                        {
                            // For Connected Alignment, use ConnectedAlignmentArcInfo
                            ed.WriteMessage($"\n  ℹ️ Connected Alignment Info Type: {connectedInfo.GetType().Name}");

                            // Get the first arc info (arcList should only have 1 arc for connected alignment)
                            foreach (ArcInfo arcInfo in arcList)
                            {
                                if (Math.Abs(arcInfo.NewRadius - arcInfo.CurrentRadius) > 0.001)
                                {
                                    try
                                    {
                                        // Cast to ConnectedAlignmentArcInfo if it's an arc type
                                        if (connectedInfo is ConnectedAlignmentArcInfo arcConnectedInfo)
                                        {
                                            double oldRadius = arcConnectedInfo.CurveRadius;
                                            ed.WriteMessage($"\n  ℹ️ Connected Arc - CurveRadius hiện tại: {oldRadius:F2}m");

                                            // Set new curve radius directly on the object
                                            arcConnectedInfo.CurveRadius = arcInfo.NewRadius;

                                            changedCount++;
                                            ed.WriteMessage($"\n  ✅ Connected Arc: {oldRadius:F2}m → {arcInfo.NewRadius:F2}m");
                                        }
                                        else
                                        {
                                            ed.WriteMessage($"\n  ⚠️ Connected Alignment không phải loại Arc. Type: {connectedInfo.GetType().Name}");
                                        }
                                    }
                                    catch (System.Exception connEx)
                                    {
                                        ed.WriteMessage($"\n  ❌ Lỗi khi thay đổi Connected Alignment: {connEx.Message}");
                                    }
                                }
                            }
                        }
                        else
                        {
                            // For normal Alignment, use AlignmentArc.Radius
                            foreach (ArcInfo arcInfo in arcList)
                            {
                                if (Math.Abs(arcInfo.NewRadius - arcInfo.CurrentRadius) > 0.001)
                                {
                                    try
                                    {
                                        // Find the arc entity by iterating through entities
                                        AlignmentArc? foundArc = null;
                                        for (int i = 0; i < alignment.Entities.Count; i++)
                                        {
                                            AlignmentEntity entity = alignment.Entities.GetEntityByOrder(i);
                                            if (entity.EntityType == AlignmentEntityType.Arc &&
                                                (int)entity.EntityId == arcInfo.EntityId)
                                            {
                                                foundArc = entity as AlignmentArc;
                                                break;
                                            }
                                        }

                                        if (foundArc != null)
                                        {
                                            double oldRadius = foundArc.Radius;

                                            // Log constraint info
                                            ed.WriteMessage($"\n  ℹ️ Arc {arcInfo.EntityId} Constraint: {foundArc.Constraint1}, {foundArc.Constraint2}");

                                            // Try to set radius
                                            foundArc.Radius = arcInfo.NewRadius;

                                            // Verify change
                                            double newRadiusActual = foundArc.Radius;
                                            if (Math.Abs(newRadiusActual - arcInfo.NewRadius) < 0.01)
                                            {
                                                changedCount++;
                                                ed.WriteMessage($"\n  ✅ Arc {arcInfo.EntityId}: {oldRadius:F2}m → {newRadiusActual:F2}m");
                                            }
                                            else
                                            {
                                                ed.WriteMessage($"\n  ⚠️ Arc {arcInfo.EntityId}: Yêu cầu {arcInfo.NewRadius:F2}m nhưng chỉ đạt {newRadiusActual:F2}m (do ràng buộc hình học)");
                                            }
                                        }
                                    }
                                    catch (Autodesk.AutoCAD.Runtime.Exception arcEx)
                                    {
                                        ed.WriteMessage($"\n  ❌ Lỗi Arc {arcInfo.EntityId}: {arcEx.Message}");
                                    }
                                    catch (System.InvalidOperationException invEx)
                                    {
                                        ed.WriteMessage($"\n  ❌ Lỗi ràng buộc Arc {arcInfo.EntityId}: {invEx.Message}");
                                    }
                                }
                            }
                        }

                        tr.Commit();

                        // Force regen to update display
                        doc.Editor.Regen();

                        if (changedCount > 0)
                        {
                            ed.WriteMessage($"\n\n✅ Hoàn thành! Đã thay đổi bán kính của {changedCount} đường cong.");
                        }
                        else
                        {
                            ed.WriteMessage("\nℹ️ Không có thay đổi nào được thực hiện.");
                            ed.WriteMessage("\n⚠️ Có thể do ràng buộc hình học không cho phép thay đổi bán kính.");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        ed.WriteMessage($"\n❌ Lỗi khi áp dụng thay đổi: {ex.Message}");
                        ed.WriteMessage($"\n{ex.StackTrace}");
                        tr.Abort();
                    }
                }
            }
        }

        /// <summary>
        /// Hiển thị hướng dẫn sử dụng
        /// </summary>
        public void ShowHelp()
        {
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;

            ed.WriteMessage("\n");
            ed.WriteMessage("\n╔════════════════════════════════════════════════════════════════╗");
            ed.WriteMessage("\n║     HƯỚNG DẪN SỬ DỤNG - ĐIỀU CHỈNH BÁN KÍNH CONNECTED ALIGNMENT ║");
            ed.WriteMessage("\n╠════════════════════════════════════════════════════════════════╣");
            ed.WriteMessage("\n║                                                                ║");
            ed.WriteMessage("\n║  Lệnh: CTA_DieuChinhBanKinh_ConnectedAlignment                 ║");
            ed.WriteMessage("\n║                                                                ║");
            ed.WriteMessage("\n║  Mục đích:                                                     ║");
            ed.WriteMessage("\n║  - Thay đổi bán kính đường cong trong Alignment                ║");
            ed.WriteMessage("\n║  - Hỗ trợ Connected Alignment tại các nút giao                 ║");
            ed.WriteMessage("\n║                                                                ║");
            ed.WriteMessage("\n║  Cách sử dụng:                                                 ║");
            ed.WriteMessage("\n║  1. Chạy lệnh CTA_DieuChinhBanKinh_ConnectedAlignment          ║");
            ed.WriteMessage("\n║  2. Chọn Alignment cần thay đổi bán kính                       ║");
            ed.WriteMessage("\n║  3. Form sẽ hiển thị danh sách các đường cong                  ║");
            ed.WriteMessage("\n║  4. Nhập bán kính mới vào cột 'Bán kính mới'                   ║");
            ed.WriteMessage("\n║  5. Nhấn 'Áp dụng' để thay đổi                                 ║");
            ed.WriteMessage("\n║                                                                ║");
            ed.WriteMessage("\n║  Lưu ý:                                                        ║");
            ed.WriteMessage("\n║  - Bán kính phải lớn hơn 0                                     ║");
            ed.WriteMessage("\n║  - Nếu thay đổi gây lỗi hình học, lệnh sẽ thông báo           ║");
            ed.WriteMessage("\n║                                                                ║");
            ed.WriteMessage("\n╚════════════════════════════════════════════════════════════════╝");
            ed.WriteMessage("\n");
        }
    }
}

// (C) Copyright 2026 by  
// Bổ sung cắt ngang vào nhóm cắt ngang hiện có
// Phương án: Tạo SectionView riêng lẻ → Move vào nhóm cắt ngang đích
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
using Autodesk.Civil.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.Civil.ApplicationServices;
using Section = Autodesk.Civil.DatabaseServices.Section;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using MyFirstProject.Extensions;
using MyFirstProject.Civil_Tool;

// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3DCsharp.CTSV_Bosung_CatNgang_Commands))]

namespace Civil3DCsharp
{
    public class CTSV_Bosung_CatNgang_Commands
    {
        /// <summary>
        /// Lệnh bổ sung cắt ngang vào nhóm cắt ngang hiện có.
        /// Phương án: Tạo SectionView riêng lẻ bằng SectionView.Create(),
        ///   sau đó user tự move các cắt ngang vào nhóm đích bằng right-click → Move to Section View Group.
        ///   
        /// Bước 1 (tự động): Tạo SectionView riêng lẻ cho các SampleLine đã chọn, đặt tại vị trí sau nhóm đích.
        /// Bước 2 (hướng dẫn user): Move các SectionView vào nhóm đích qua context menu.
        /// </summary>
        [CommandMethod("CTS_Bo_Sung_Cat_Ngang")]
        public static void CTSV_Bosung_CatNgang()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            try
            {
                ed.WriteMessage("\n=== CTSV_Bosung_CatNgang - Bổ Sung Cắt Ngang ===\n");

                // Khai báo biến
                ObjectId sampleLineGroupId = ObjectId.Null;
                ObjectId alignmentId = ObjectId.Null;
                ObjectId sectionViewStyleId = ObjectId.Null;
                string targetGroupName = "";
                List<(double station, Point3d location, ObjectId sampleLineId)> existingSVInfo
                    = new List<(double, Point3d, ObjectId)>();

                // ===== BƯỚC 1: Chọn 1 SectionView trong nhóm cắt ngang đích =====
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    ObjectId sectionViewId = UserInput.GSectionView("Chọn 1 cắt ngang trong nhóm cắt ngang ĐÍCH: ");
                    if (sectionViewId == ObjectId.Null)
                    {
                        ed.WriteMessage("\nKhông thể chọn SectionView.");
                        return;
                    }

                    SectionView? sectionView = tr.GetObject(sectionViewId, OpenMode.ForWrite) as SectionView;
                    if (sectionView == null)
                    {
                        ed.WriteMessage("\nKhông thể mở SectionView.");
                        return;
                    }

                    sectionViewStyleId = sectionView.StyleId;

                    SampleLine? sampleLine = tr.GetObject(sectionView.SampleLineId, OpenMode.ForWrite) as SampleLine;
                    if (sampleLine == null)
                    {
                        ed.WriteMessage("\nKhông thể lấy SampleLine từ SectionView.");
                        return;
                    }

                    sampleLineGroupId = sampleLine.GroupId;
                    alignmentId = sampleLine.GetParentAlignmentId();

                    SampleLineGroup? sampleLineGroup = tr.GetObject(sampleLineGroupId, OpenMode.ForWrite) as SampleLineGroup;
                    if (sampleLineGroup == null)
                    {
                        ed.WriteMessage("\nKhông thể mở SampleLineGroup.");
                        return;
                    }

                    ed.WriteMessage($"\n✅ SampleLineGroup: {sampleLineGroup.Name}");

                    // Tìm SectionViewGroup chứa SectionView đã chọn
                    SectionViewGroupCollection sectionViewGroupCollection = sampleLineGroup.SectionViewGroups;
                    SectionViewGroup? targetGroup = null;

                    foreach (SectionViewGroup svGroup in sectionViewGroupCollection)
                    {
                        ObjectIdCollection svIds = svGroup.GetSectionViewIds();
                        if (svIds.Contains(sectionViewId))
                        {
                            targetGroup = svGroup;
                            break;
                        }
                    }

                    if (targetGroup == null)
                    {
                        if (sectionViewGroupCollection.Count > 0)
                            targetGroup = sectionViewGroupCollection[0];
                    }

                    if (targetGroup == null)
                    {
                        ed.WriteMessage("\n❌ Không tìm thấy nhóm cắt ngang đích.");
                        return;
                    }

                    targetGroupName = targetGroup.Name;
                    ed.WriteMessage($"\n✅ Nhóm cắt ngang đích: {targetGroupName}");

                    // Lấy thông tin vị trí các SectionView hiện có trong nhóm
                    ObjectIdCollection existingSVIds = targetGroup.GetSectionViewIds();

                    foreach (ObjectId svId in existingSVIds)
                    {
                        SectionView? sv = tr.GetObject(svId, OpenMode.ForWrite) as SectionView;
                        if (sv == null) continue;

                        SampleLine? sl = tr.GetObject(sv.SampleLineId, OpenMode.ForWrite) as SampleLine;
                        if (sl == null) continue;

                        existingSVInfo.Add((sl.Station, sv.Location, sv.SampleLineId));
                    }

                    existingSVInfo.Sort((a, b) => a.station.CompareTo(b.station));

                    ed.WriteMessage($"\n📊 Nhóm đích hiện có {existingSVInfo.Count} cắt ngang.");

                    tr.Commit();
                }

                // ===== BƯỚC 2: Chọn các SampleLine cần bổ sung =====
                List<ObjectId> sampleLineIdsToAdd = new List<ObjectId>();

                ed.WriteMessage("\n\n--- Chọn các SampleLine cần bổ sung cắt ngang ---");
                ed.WriteMessage("\n   (Nhấn ESC để kết thúc chọn)");

                bool continueSelecting = true;
                while (continueSelecting)
                {
                    try
                    {
                        ObjectId selectedSampleLineId = UserInput.GSampleLineId($"\nChọn SampleLine cần bổ sung ({sampleLineIdsToAdd.Count} đã chọn): ");

                        if (selectedSampleLineId == ObjectId.Null)
                        {
                            continueSelecting = false;
                            break;
                        }

                        using (Transaction tr = db.TransactionManager.StartTransaction())
                        {
                            SampleLine? sl = tr.GetObject(selectedSampleLineId, OpenMode.ForWrite) as SampleLine;
                            if (sl == null)
                            {
                                ed.WriteMessage("\n⚠️ Không thể đọc SampleLine, thử lại.");
                                tr.Commit();
                                continue;
                            }

                            if (sl.GroupId != sampleLineGroupId)
                            {
                                ed.WriteMessage($"\n⚠️ SampleLine '{sl.Name}' không thuộc cùng nhóm cọc! Bỏ qua.");
                                tr.Commit();
                                continue;
                            }

                            if (sampleLineIdsToAdd.Contains(selectedSampleLineId))
                            {
                                ed.WriteMessage($"\n⚠️ SampleLine '{sl.Name}' đã được chọn rồi. Bỏ qua.");
                                tr.Commit();
                                continue;
                            }

                            sampleLineIdsToAdd.Add(selectedSampleLineId);
                            ed.WriteMessage($"\n   ✅ Đã thêm: {sl.Name} (Station: {sl.Station:N2})");

                            tr.Commit();
                        }
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception ex)
                    {
                        if (ex.ErrorStatus == ErrorStatus.UserBreak)
                            continueSelecting = false;
                        else
                        {
                            ed.WriteMessage($"\n⚠️ Lỗi: {ex.Message}");
                            continueSelecting = false;
                        }
                    }
                    catch (System.Exception)
                    {
                        continueSelecting = false;
                    }
                }

                if (sampleLineIdsToAdd.Count == 0)
                {
                    ed.WriteMessage("\n❌ Không có SampleLine nào được chọn. Hủy lệnh.");
                    return;
                }

                ed.WriteMessage($"\n\n📋 Tổng cộng {sampleLineIdsToAdd.Count} SampleLine sẽ được bổ sung cắt ngang.");

                // ===== BƯỚC 3: Tạo SectionView riêng lẻ =====
                List<ObjectId> createdSectionViewIds = new List<ObjectId>();

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    Alignment? alignment = tr.GetObject(alignmentId, OpenMode.ForWrite) as Alignment;
                    if (alignment == null)
                    {
                        ed.WriteMessage("\n❌ Không thể mở Alignment.");
                        return;
                    }

                    // Tính spacing từ nhóm đích
                    double spacingX = 0;
                    double spacingY = 0;

                    if (existingSVInfo.Count >= 2)
                    {
                        // Tìm pattern spacing phổ biến nhất
                        List<double> dxList = new List<double>();
                        List<double> dyList = new List<double>();

                        for (int i = 1; i < existingSVInfo.Count; i++)
                        {
                            double dx = existingSVInfo[i].location.X - existingSVInfo[i - 1].location.X;
                            double dy = existingSVInfo[i].location.Y - existingSVInfo[i - 1].location.Y;

                            if (Math.Abs(dx) > 0.1 || Math.Abs(dy) > 0.1)
                            {
                                dxList.Add(dx);
                                dyList.Add(dy);
                            }
                        }

                        if (dxList.Count > 0)
                        {
                            spacingX = dxList.Average();
                            spacingY = dyList.Average();
                        }
                    }

                    if (Math.Abs(spacingX) < 0.1 && Math.Abs(spacingY) < 0.1)
                    {
                        spacingX = 30.0;
                        spacingY = 0;
                    }

                    // Sắp xếp sample lines theo station
                    List<(ObjectId slId, double station, string name)> sampleLinesToAdd
                        = new List<(ObjectId, double, string)>();

                    foreach (ObjectId slId in sampleLineIdsToAdd)
                    {
                        SampleLine? sl = tr.GetObject(slId, OpenMode.ForWrite) as SampleLine;
                        if (sl != null)
                        {
                            sampleLinesToAdd.Add((slId, sl.Station, sl.Name));
                        }
                    }

                    sampleLinesToAdd.Sort((a, b) => a.station.CompareTo(b.station));

                    // Vị trí bắt đầu: sau cắt ngang cuối cùng của nhóm đích
                    Point3d startPos;
                    if (existingSVInfo.Count > 0)
                    {
                        var lastSV = existingSVInfo[existingSVInfo.Count - 1];
                        startPos = new Point3d(lastSV.location.X + spacingX, lastSV.location.Y + spacingY, 0);
                    }
                    else
                    {
                        // Fallback: hỏi user chọn điểm
                        startPos = UserInput.GPoint("\nChọn vị trí đặt cắt ngang bổ sung: ");
                    }

                    ed.WriteMessage($"\n📐 Vị trí bắt đầu: ({startPos.X:F2}, {startPos.Y:F2})");
                    ed.WriteMessage($"\n📐 Khoảng cách: dX={spacingX:F2}, dY={spacingY:F2}");

                    int createdCount = 0;

                    for (int i = 0; i < sampleLinesToAdd.Count; i++)
                    {
                        var (slId, station, name) = sampleLinesToAdd[i];

                        try
                        {
                            // Tính vị trí đặt
                            Point3d location = new Point3d(
                                startPos.X + spacingX * i,
                                startPos.Y + spacingY * i,
                                0);

                            // Tạo SectionView mới
                            ObjectId newSVId = SectionView.Create(name, slId, location);

                            if (newSVId != ObjectId.Null && newSVId.IsValid)
                            {
                                // Áp dụng style
                                SectionView? newSV = tr.GetObject(newSVId, OpenMode.ForWrite) as SectionView;
                                if (newSV != null && sectionViewStyleId != ObjectId.Null && sectionViewStyleId.IsValid)
                                {
                                    try { newSV.StyleId = sectionViewStyleId; }
                                    catch { /* Bỏ qua lỗi style */ }
                                }

                                createdSectionViewIds.Add(newSVId);
                                createdCount++;

                                ed.WriteMessage($"\n  ✅ Đã tạo: {name} (Station: {station:N2})");
                            }
                            else
                            {
                                ed.WriteMessage($"\n  ❌ Không thể tạo: {name}");
                            }
                        }
                        catch (System.Exception ex)
                        {
                            ed.WriteMessage($"\n  ❌ Lỗi khi tạo '{name}': {ex.Message}");
                        }
                    }

                    tr.Commit();

                    ed.WriteMessage($"\n\n📊 Đã tạo {createdCount} cắt ngang riêng lẻ.");
                }

                // ===== BƯỚC 4: Hướng dẫn user move vào nhóm đích =====
                if (createdSectionViewIds.Count > 0)
                {
                    ed.WriteMessage("\n\n╔══════════════════════════════════════════════════╗");
                    ed.WriteMessage("\n║  HƯỚNG DẪN: Move cắt ngang vào nhóm đích        ║");
                    ed.WriteMessage("\n╠══════════════════════════════════════════════════╣");
                    ed.WriteMessage($"\n║  Nhóm đích: {targetGroupName,-36}║");
                    ed.WriteMessage("\n╠══════════════════════════════════════════════════╣");
                    ed.WriteMessage("\n║  1. Chọn (các) cắt ngang vừa tạo                ║");
                    ed.WriteMessage("\n║  2. Right-click → Move to Section View Group     ║");
                    ed.WriteMessage("\n║  3. Chọn nhóm đích                               ║");
                    ed.WriteMessage("\n║  4. Update Group Layout nếu cần                  ║");
                    ed.WriteMessage("\n╚══════════════════════════════════════════════════╝");
                    
                    ed.WriteMessage($"\n\n💡 Số cắt ngang cần move: {createdSectionViewIds.Count}");
                    ed.WriteMessage("\n💡 Sau khi move xong, layout sẽ được cập nhật tự động.");

                    // Thử tự động select các SectionView mới tạo để user dễ thao tác
                    try
                    {
                        // Highlight các SectionView mới tạo
                        ObjectId[] idsArray = createdSectionViewIds.ToArray();
                        ed.SetImpliedSelection(idsArray);
                        ed.WriteMessage("\n\n✅ Đã chọn sẵn các cắt ngang mới tạo. Right-click để move.");
                    }
                    catch (System.Exception)
                    {
                        ed.WriteMessage("\n\n💡 Hãy chọn các cắt ngang mới tạo để move vào nhóm đích.");
                    }
                }

                ed.WriteMessage("\n\n✅ Lệnh CTSV_Bosung_CatNgang hoàn thành!");
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                ed.WriteMessage($"\n❌ Lỗi AutoCAD: {e.Message}");
            }
            catch (System.Exception e)
            {
                ed.WriteMessage($"\n❌ Lỗi: {e.Message}");
                ed.WriteMessage($"\n   Stack: {e.StackTrace}");
            }
        }
    }
}

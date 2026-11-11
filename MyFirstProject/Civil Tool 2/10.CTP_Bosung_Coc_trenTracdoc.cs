// (C) Copyright 2026 by Thang
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

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

using MyFirstProject.Extensions;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3DCsharp.BoSungCocTrenTracDoc))]

namespace Civil3DCsharp
{
    public class BoSungCocTrenTracDoc
    {
        /// <summary>
        /// Lệnh bổ sung cọc bằng cách chọn điểm trên Profile View (trắc dọc).
        /// User chọn ProfileView → Hiện form đặt tên cọc → Chọn điểm liên tục trên ProfileView.
        /// </summary>
        [CommandMethod("CTP_Bo_Sung_Coc_Tren_Trac_Doc")]
        public static void CTPBoSungCocTrenTracDoc()
        {
            // 1. Chọn Profile View
            ObjectId profileViewId = UserInput.GProfileViewId("\n Chọn Profile View (trắc dọc): ");
            if (profileViewId == ObjectId.Null)
            {
                A.Ed.WriteMessage("\n Đã hủy lệnh.");
                return;
            }

            // 2. Lấy AlignmentId từ ProfileView
            ObjectId alignmentId = ObjectId.Null;
            using (Transaction trInit = A.Db.TransactionManager.StartTransaction())
            {
                try
                {
                    ProfileView? pv = trInit.GetObject(profileViewId, OpenMode.ForWrite) as ProfileView;
                    if (pv == null)
                    {
                        A.Ed.WriteMessage("\n Không thể mở Profile View.");
                        return;
                    }
                    alignmentId = pv.AlignmentId;

                    // Kiểm tra/tạo SampleLine Group nếu chưa có
                    Alignment? alignment = trInit.GetObject(alignmentId, OpenMode.ForWrite) as Alignment;
                    if (alignment == null)
                    {
                        A.Ed.WriteMessage("\n Không thể mở Alignment.");
                        return;
                    }

                    if (alignment.GetSampleLineGroupIds().Count == 0)
                    {
                        ObjectId newGroupId = SampleLineGroup.Create(alignment.Name, alignmentId);
                        A.Ed.WriteMessage($"\n Đã tạo nhóm cọc mới: {alignment.Name}");
                    }

                    trInit.Commit();
                }
                catch (System.Exception ex)
                {
                    A.Ed.WriteMessage($"\n Lỗi: {ex.Message}");
                    return;
                }
            }

            // 3. Hiển thị form đặt tên cọc
            var form = new MyFirstProject.Civil_Tool_2.BoSungCocForm(alignmentId);
            var result = Application.ShowModalDialog(form);

            if (result != DialogResult.OK || !form.FormAccepted)
            {
                A.Ed.WriteMessage("\n Đã hủy lệnh.");
                return;
            }

            // 4. Lấy giá trị từ form
            ObjectId sampleLineGroupId = form.SelectedSampleLineGroupId;

            // 5. Vòng lặp chọn điểm và tạo cọc
            A.Ed.WriteMessage("\n --- Bắt đầu chọn điểm trên trắc dọc để tạo cọc ---");
            A.Ed.WriteMessage("\n Nhấn ESC hoặc Enter để kết thúc.");

            bool continueLoop = true;
            int createdCount = 0;

            while (continueLoop)
            {
                using (Transaction tr = A.Db.TransactionManager.StartTransaction())
                {
                    try
                    {
                        Alignment? alignment = tr.GetObject(alignmentId, OpenMode.ForWrite) as Alignment;
                        if (alignment == null)
                        {
                            A.Ed.WriteMessage("\n Không thể mở Alignment.");
                            break;
                        }

                        ProfileView? profileView = tr.GetObject(profileViewId, OpenMode.ForWrite) as ProfileView;
                        if (profileView == null)
                        {
                            A.Ed.WriteMessage("\n Không thể mở Profile View.");
                            break;
                        }

                        string stakeName = form.GetCurrentStakeName();

                        // Prompt user to pick point
                        PromptPointOptions ppo = new PromptPointOptions($"\n Chọn điểm trên trắc dọc để tạo cọc [{stakeName}] (Enter để kết thúc): ");
                        ppo.AllowNone = true;
                        PromptPointResult ppr = A.Ed.GetPoint(ppo);

                        if (ppr.Status == PromptStatus.None || ppr.Status == PromptStatus.Cancel)
                        {
                            continueLoop = false;
                            tr.Commit();
                            continue;
                        }

                        if (ppr.Status != PromptStatus.OK)
                        {
                            continueLoop = false;
                            tr.Commit();
                            continue;
                        }

                        Point3d pickedPoint = ppr.Value;

                        // Find station at picked point on ProfileView
                        double station = 0;
                        double elevation = 0;
                        try
                        {
                            profileView.FindStationAndElevationAtXY(pickedPoint.X, pickedPoint.Y, ref station, ref elevation);
                        }
                        catch
                        {
                            A.Ed.WriteMessage("\n ⚠️ Điểm chọn không nằm trên Profile View. Vui lòng chọn lại.");
                            tr.Commit();
                            continue;
                        }

                        // Kiểm tra station có nằm trong phạm vi alignment
                        if (station < alignment.StartingStation || station > alignment.EndingStation)
                        {
                            A.Ed.WriteMessage($"\n ⚠️ Station {station:F2} nằm ngoài phạm vi alignment ({alignment.StartingStation:F2} - {alignment.EndingStation:F2}). Vui lòng chọn lại.");
                            tr.Commit();
                            continue;
                        }

                        // Tạo sample line với tên tạm (có tiền tố "z") để bypass duplicate check
                        string tempName = "z" + stakeName + "_" + DateTime.Now.Ticks.ToString();

                        ObjectId sampleLineId = ObjectId.Null;
                        try
                        {
                            // Tính tọa độ 2 điểm trái phải
                            Point2dCollection point2Ds = new Point2dCollection();
                            double easting = 0, northing = 0;
                            alignment.PointLocation(station, -10, ref easting, ref northing);
                            point2Ds.Add(new Point2d(easting, northing));
                            alignment.PointLocation(station, 10, ref easting, ref northing);
                            point2Ds.Add(new Point2d(easting, northing));

                            // Tạo sample line với tên tạm
                            sampleLineId = SampleLine.Create(tempName, sampleLineGroupId, point2Ds);
                        }
                        catch (System.Exception createEx)
                        {
                            A.Ed.WriteMessage($"\n ⚠️ Không thể tạo cọc tại station {station:F2}: {createEx.Message}");
                            tr.Commit();
                            continue;
                        }

                        if (sampleLineId != ObjectId.Null)
                        {
                            // Đổi lại tên gốc (cho phép trùng tên)
                            SampleLine? newSampleLine = tr.GetObject(sampleLineId, OpenMode.ForWrite) as SampleLine;
                            if (newSampleLine != null)
                            {
                                newSampleLine.Name = stakeName;
                            }

                            createdCount++;
                            A.Ed.WriteMessage($"\n ✓ Đã tạo cọc '{stakeName}' tại station {station:F2}");

                            // Tự động tăng số
                            form.IncrementNumber();
                        }

                        tr.Commit();
                    }
                    catch (System.Exception ex)
                    {
                        A.Ed.WriteMessage($"\n Lỗi: {ex.Message}");
                        tr.Abort();
                    }
                }
            }

            A.Ed.WriteMessage($"\n\n === Hoàn thành! Đã tạo {createdCount} cọc. ===");
        }
    }
}

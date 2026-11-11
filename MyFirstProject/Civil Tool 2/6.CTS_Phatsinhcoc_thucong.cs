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
[assembly: CommandClass(typeof(Civil3DCsharp.PhatSinhCocThuCong))]

namespace Civil3DCsharp
{
    public class PhatSinhCocThuCong
    {
        /// <summary>
        /// Lệnh phát sinh cọc thủ công - chọn điểm trên ProfileView hoặc bình đồ
        /// </summary>
        [CommandMethod("CTS_Phat_Sinh_Coc_Thu_Cong")]
        public static void CTSPhatSinhCocThuCong()
        {
            // 1. Chọn Alignment trước
            ObjectId alignmentId = UserInput.GAlignmentId("\n Chọn tim đường (Alignment): ");
            if (alignmentId == ObjectId.Null)
            {
                A.Ed.WriteMessage("\n Đã hủy lệnh.");
                return;
            }

            // Check if alignment has sample line groups
            using (Transaction trCheck = A.Db.TransactionManager.StartTransaction())
            {
                try
                {
                    Alignment? alignment = trCheck.GetObject(alignmentId, OpenMode.ForWrite) as Alignment;
                    if (alignment == null)
                    {
                        A.Ed.WriteMessage("\n Không thể mở Alignment.");
                        return;
                    }

                    // Create default group if none exists
                    if (alignment.GetSampleLineGroupIds().Count == 0)
                    {
                        ObjectId newGroupId = SampleLineGroup.Create(alignment.Name, alignmentId);
                        A.Ed.WriteMessage($"\n Đã tạo nhóm cọc mới: {alignment.Name}");
                    }
                    trCheck.Commit();
                }
                catch (System.Exception ex)
                {
                    A.Ed.WriteMessage($"\n Lỗi: {ex.Message}");
                    return;
                }
            }

            // 2. Hiển thị form để cấu hình
            var form = new MyFirstProject.Civil_Tool_2.PhatSinhCocThuCongForm(alignmentId);
            var result = Application.ShowModalDialog(form);

            if (result != DialogResult.OK || !form.FormAccepted)
            {
                A.Ed.WriteMessage("\n Đã hủy lệnh.");
                return;
            }

            // 3. Lấy các giá trị từ form
            ObjectId sampleLineGroupId = form.SelectedSampleLineGroupId;
            bool selectOnProfileView = form.SelectOnProfileView;

            // 4. Nếu chọn trên ProfileView, yêu cầu chọn ProfileView
            ObjectId profileViewId = ObjectId.Null;
            if (selectOnProfileView)
            {
                profileViewId = UserInput.GProfileViewId("\n Chọn Profile View (trắc dọc): ");
                if (profileViewId == ObjectId.Null)
                {
                    A.Ed.WriteMessage("\n Đã hủy lệnh.");
                    return;
                }
            }

            // 5. Vòng lặp chọn điểm và tạo cọc
            A.Ed.WriteMessage("\n --- Bắt đầu chọn điểm để tạo cọc ---");
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

                        double station = 0;
                        string stakeName = form.GetCurrentStakeName();

                        if (selectOnProfileView)
                        {
                            // Chọn trên ProfileView
                            ProfileView? profileView = tr.GetObject(profileViewId, OpenMode.ForWrite) as ProfileView;
                            if (profileView == null)
                            {
                                A.Ed.WriteMessage("\n Không thể mở Profile View.");
                                break;
                            }

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

                            // Find station at picked point
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
                        }
                        else
                        {
                            // Chọn trên bình đồ
                            PromptPointOptions ppo = new PromptPointOptions($"\n Chọn điểm trên bình đồ để tạo cọc [{stakeName}] (Enter để kết thúc): ");
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

                            // Find station and offset from alignment
                            double offset = 0;
                            try
                            {
                                alignment.StationOffset(pickedPoint.X, pickedPoint.Y, ref station, ref offset);
                            }
                            catch
                            {
                                A.Ed.WriteMessage("\n ⚠️ Điểm chọn quá xa tim đường. Vui lòng chọn lại.");
                                tr.Commit();
                                continue;
                            }
                        }

                        // Kiểm tra station có nằm trong phạm vi alignment
                        if (station < alignment.StartingStation || station > alignment.EndingStation)
                        {
                            A.Ed.WriteMessage($"\n ⚠️ Station {station:F2} nằm ngoài phạm vi alignment ({alignment.StartingStation:F2} - {alignment.EndingStation:F2}). Vui lòng chọn lại.");
                            tr.Commit();
                            continue;
                        }

                        // Lấy danh sách tên sampleline đã tồn tại trong group
                        SampleLineGroup? sampleLineGroup = tr.GetObject(sampleLineGroupId, OpenMode.ForRead) as SampleLineGroup;
                        HashSet<string> existingNames = new HashSet<string>();
                        if (sampleLineGroup != null)
                        {
                            foreach (ObjectId slId in sampleLineGroup.GetSampleLineIds())
                            {
                                SampleLine? sl = tr.GetObject(slId, OpenMode.ForRead) as SampleLine;
                                if (sl != null)
                                {
                                    existingNames.Add(sl.Name);
                                }
                            }
                        }

                        // Luôn tạo với tiền tố "z" để bypass duplicate check (như CTS_PHATSINHCOC)
                        // Civil 3D chỉ check trùng tên khi CREATE, không check khi RENAME
                        string tempName = "z" + stakeName + "_" + DateTime.Now.Ticks.ToString();

                        // Tạo sample line trực tiếp trong transaction này
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

                            // Tạo sample line với tên tạm (có tiền tố "z")
                            sampleLineId = SampleLine.Create(tempName, sampleLineGroupId, point2Ds);
                        }
                        catch (System.Exception createEx)
                        {
                            A.Ed.WriteMessage($"\n ⚠️ Không thể tạo sampleline tại station {station:F2}: {createEx.Message}");
                            tr.Commit();
                            continue;
                        }

                        if (sampleLineId != ObjectId.Null)
                        {
                            // Đổi lại tên gốc (giống CTS_PHATSINHCOC - cho phép trùng tên)
                            SampleLine? newSampleLine = tr.GetObject(sampleLineId, OpenMode.ForWrite) as SampleLine;
                            if (newSampleLine != null)
                            {
                                newSampleLine.Name = stakeName;
                            }

                            createdCount++;
                            A.Ed.WriteMessage($"\n ✓ Đã tạo cọc '{stakeName}' tại station {station:F2}");

                            // Tự động tăng số nếu được bật
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

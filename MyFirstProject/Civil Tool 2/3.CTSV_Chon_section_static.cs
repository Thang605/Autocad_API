// (C) Copyright 2026 by  
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Autodesk.AutoCAD.Runtime;
using Acad = Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;

using Civil = Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using Autodesk.Civil.Runtime;
using Autodesk.Civil.Settings;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.Civil.ApplicationServices;
using Section = Autodesk.Civil.DatabaseServices.Section;
using Autodesk.Civil;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using MyFirstProject.Extensions;

// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3DCsharp.CTSV_ChonSectionStatic))]

namespace Civil3DCsharp
{
    public class CTSV_ChonSectionStatic
    {
        /// <summary>
        /// Lệnh quét chọn tất cả các Section có thuộc tính Static Dynamic = "Static" trên cắt ngang
        /// </summary>
        [CommandMethod("CTS_Chon_Section_Static")]
        public static void CTSVChonSectionStatic()
        {
            // start transaction
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                // Lấy Editor để chọn đối tượng
                Editor ed = A.Ed;

                // Yêu cầu người dùng chọn 1 trắc ngang trong nhóm cần quét
                ObjectId sectionViewId = UserInput.GSectionView("Chọn 1 trắc ngang trong nhóm cần quét các Section Static: \n");
                SectionView? sectionView = tr.GetObject(sectionViewId, OpenMode.ForWrite) as SectionView;

                if (sectionView == null)
                {
                    ed.WriteMessage("\n Không thể lấy thông tin SectionView.");
                    return;
                }

                // Lấy SampleLine và SampleLineGroup
                ObjectId sampleLineId = sectionView.SampleLineId;
                SampleLine? sampleLine = tr.GetObject(sampleLineId, OpenMode.ForWrite) as SampleLine;

                if (sampleLine == null)
                {
                    ed.WriteMessage("\n Không thể lấy thông tin SampleLine.");
                    return;
                }

                ObjectId sampleLineGroupId = sampleLine.GroupId;
                SampleLineGroup? sampleLineGroup = tr.GetObject(sampleLineGroupId, OpenMode.ForWrite) as SampleLineGroup;

                if (sampleLineGroup == null)
                {
                    ed.WriteMessage("\n Không thể lấy thông tin SampleLineGroup.");
                    return;
                }

                // Danh sách chứa các ObjectId của Section có thuộc tính Static
                List<ObjectId> staticSectionIds = new List<ObjectId>();
                int totalSections = 0;
                int staticCount = 0;

                // Lấy tất cả SampleLine trong nhóm
                ObjectIdCollection sampleLineIds = sampleLineGroup.GetSampleLineIds();

                // Duyệt qua từng SampleLine
                foreach (ObjectId slId in sampleLineIds)
                {
                    SampleLine? sl = tr.GetObject(slId, OpenMode.ForWrite) as SampleLine;
                    if (sl == null) continue;

                    // Lấy tất cả các Section từ SampleLine này bằng GetSectionIds()
                    ObjectIdCollection sectionIds = sl.GetSectionIds();

                    foreach (ObjectId sectionId in sectionIds)
                    {
                        try
                        {
                            if (sectionId.IsNull || !sectionId.IsValid) continue;

                            Section? section = tr.GetObject(sectionId, OpenMode.ForWrite) as Section;
                            if (section == null) continue;

                            totalSections++;

                            // Kiểm tra thuộc tính UpdateMode của Section
                            // UpdateMode = Static nghĩa là section không cập nhật động
                            if (section.UpdateMode == SectionUpdateType.Static)
                            {
                                staticSectionIds.Add(sectionId);
                                staticCount++;
                            }
                        }
                        catch (System.Exception)
                        {
                            // Bỏ qua nếu không lấy được Section
                            continue;
                        }
                    }
                }

                // Thông báo kết quả
                ed.WriteMessage($"\n Tổng số Section: {totalSections}");
                ed.WriteMessage($"\n Số Section có thuộc tính Static: {staticCount}");

                // Nếu có Section Static, tạo selection set và chọn chúng
                if (staticSectionIds.Count > 0)
                {
                    // Chuyển đổi List thành mảng ObjectId
                    ObjectId[] idsArray = staticSectionIds.ToArray();

                    // Sử dụng SetImpliedSelection để chọn các đối tượng
                    ed.SetImpliedSelection(idsArray);

                    ed.WriteMessage($"\n Đã chọn {staticCount} Section có thuộc tính Static.");
                    ed.WriteMessage("\n Các Section này đã được highlight trong bản vẽ.");
                }
                else
                {
                    ed.WriteMessage("\n Không tìm thấy Section nào có thuộc tính Static trong nhóm này.");
                }

                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage("\n Lỗi: " + e.Message);
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage("\n Lỗi hệ thống: " + ex.Message);
            }
        }

    }
}

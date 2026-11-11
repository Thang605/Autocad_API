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
[assembly: CommandClass(typeof(Civil3DCsharp.CTSV_ThayDoi_MSS))]

namespace Civil3DCsharp
{
    public class CTSV_ThayDoi_MSS
    {
        /// <summary>
        /// Lệnh thay đổi mức so sánh (Datum/ElevationMin) của 1 cắt ngang
        /// Người dùng chọn 1 section view, sau đó pick điểm trên section view
        /// để xác định giá trị ElevationMin (MSS) mới.
        /// </summary>
        [CommandMethod("CTS_Thay_Doi_MSS")]
        public static void CTSVThayDoiMSS()
        {
            // start transaction
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                Editor ed = A.Ed;

                // 1. Chọn 1 section view cần thay đổi MSS
                ObjectId sectionViewId = UserInput.GSectionView("\n Chọn cắt ngang cần thay đổi mức so sánh (MSS): \n");
                SectionView? sectionView = tr.GetObject(sectionViewId, OpenMode.ForWrite) as SectionView;

                if (sectionView == null)
                {
                    ed.WriteMessage("\n Không thể lấy thông tin SectionView.");
                    return;
                }

                // Hiển thị thông tin MSS hiện tại
                ed.WriteMessage($"\n MSS hiện tại: Min = {sectionView.ElevationMin:N2}, Max = {sectionView.ElevationMax:N2}");

                // 2. Pick điểm trên section view để xác định MSS mới
                Point3d point3D_mss = UserInput.GPoint("\n Chọn điểm trên cắt ngang để xác định MSS mới:");

                double offset = 0;
                double newElevationMin = 0;
                sectionView.FindOffsetAndElevationAtXY(point3D_mss.X, point3D_mss.Y, ref offset, ref newElevationMin);

                // Làm tròn MSS mới về số nguyên
                newElevationMin = Math.Round(newElevationMin, 0);

                // 3. Set MSS mới cho section view
                sectionView.IsElevationRangeAutomatic = false;
                sectionView.ElevationMin = newElevationMin;

                ed.WriteMessage($"\n Đã thay đổi MSS thành: Min = {newElevationMin:N2}");

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

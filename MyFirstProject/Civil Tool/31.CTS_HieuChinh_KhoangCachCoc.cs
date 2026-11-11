// (C) Copyright 2015 by  
//
using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

using Autodesk.Civil.DatabaseServices;

using MyFirstProject.Extensions;

// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3DCsharp.HieuChinhKhoangCachCoc))]

namespace Civil3DCsharp
{
    public class HieuChinhKhoangCachCoc
    {
        /// <summary>
        /// Lệnh hiệu chỉnh khoảng cách nhóm cọc.
        /// Chọn cọc đầu và cọc cuối, các cọc ở giữa sẽ được điều chỉnh
        /// sao cho cách đều nhau theo khoảng cách yêu cầu.
        /// Cọc có lý trình nhỏ nhất được giữ nguyên.
        /// </summary>
        [CommandMethod("CTS_Hieu_Chinh_Khoang_Cach_Coc")]
        public static void CTSHieuChinhKhoangCachCoc()
        {
            // Start transaction
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                // Bước 1: Chọn cọc đầu của nhóm
                A.Ed.WriteMessage("\n=== HIỆU CHỈNH KHOẢNG CÁCH NHÓM CỌC ===");
                
                ObjectId sampleLineId1 = UserInput.GSampleLineId("\n Chọn cọc ĐẦU của nhóm:");
                if (sampleLineId1 == ObjectId.Null)
                {
                    A.Ed.WriteMessage("\n Đã hủy lệnh - Không chọn được cọc đầu.");
                    return;
                }

                // Bước 2: Chọn cọc cuối của nhóm
                ObjectId sampleLineId2 = UserInput.GSampleLineId("\n Chọn cọc CUỐI của nhóm:");
                if (sampleLineId2 == ObjectId.Null)
                {
                    A.Ed.WriteMessage("\n Đã hủy lệnh - Không chọn được cọc cuối.");
                    return;
                }

                // Lấy thông tin 2 sample line
                SampleLine? sl1 = tr.GetObject(sampleLineId1, OpenMode.ForRead) as SampleLine;
                SampleLine? sl2 = tr.GetObject(sampleLineId2, OpenMode.ForRead) as SampleLine;

                if (sl1 == null || sl2 == null)
                {
                    A.Ed.WriteMessage("\n Lỗi: Không thể đọc được SampleLine.");
                    return;
                }

                // Kiểm tra 2 sample line có cùng group không
                ObjectId groupId1 = sl1.GroupId;
                ObjectId groupId2 = sl2.GroupId;

                if (groupId1 != groupId2)
                {
                    A.Ed.WriteMessage("\n Lỗi: 2 cọc không thuộc cùng một nhóm SampleLine.");
                    return;
                }

                // Xác định phạm vi lý trình
                double stationMin = Math.Min(sl1.Station, sl2.Station);
                double stationMax = Math.Max(sl1.Station, sl2.Station);

                A.Ed.WriteMessage($"\n Phạm vi lý trình: {stationMin:F3} - {stationMax:F3}");

                // Lấy SampleLineGroup
                SampleLineGroup? slGroup = tr.GetObject(groupId1, OpenMode.ForRead) as SampleLineGroup;
                if (slGroup == null)
                {
                    A.Ed.WriteMessage("\n Lỗi: Không thể đọc được SampleLineGroup.");
                    return;
                }

                // Lấy tất cả sample line trong phạm vi
                List<ObjectId> sampleLinesInRange = new List<ObjectId>();
                ObjectIdCollection allSampleLineIds = slGroup.GetSampleLineIds();

                foreach (ObjectId slId in allSampleLineIds)
                {
                    SampleLine? sl = tr.GetObject(slId, OpenMode.ForRead) as SampleLine;
                    if (sl != null)
                    {
                        // Kiểm tra station có nằm trong phạm vi không (bao gồm cả 2 đầu)
                        if (sl.Station >= stationMin - 0.001 && sl.Station <= stationMax + 0.001)
                        {
                            sampleLinesInRange.Add(slId);
                        }
                    }
                }

                // Sắp xếp theo station tăng dần
                sampleLinesInRange = sampleLinesInRange
                    .OrderBy(id =>
                    {
                        SampleLine? s = tr.GetObject(id, OpenMode.ForRead) as SampleLine;
                        return s?.Station ?? 0;
                    })
                    .ToList();

                int soCoc = sampleLinesInRange.Count;
                A.Ed.WriteMessage($"\n Số cọc trong nhóm: {soCoc}");

                if (soCoc < 2)
                {
                    A.Ed.WriteMessage("\n Không đủ cọc để hiệu chỉnh (cần ít nhất 2 cọc).");
                    return;
                }

                // Bước 3: Hiển thị form để nhập khoảng cách
                var form = new MyFirstProject.Civil_Tool.HieuChinhKhoangCachCocForm();
                form.SoCocTrongNhom = soCoc;
                form.ThongTinLyTrinh = $"{stationMin:F2} → {stationMax:F2}";

                var result = Autodesk.AutoCAD.ApplicationServices.Application.ShowModalDialog(form);

                if (result != System.Windows.Forms.DialogResult.OK || !form.FormAccepted)
                {
                    A.Ed.WriteMessage("\n Đã hủy lệnh.");
                    return;
                }

                double khoangCachYeuCau = form.KhoangCachYeuCau;
                A.Ed.WriteMessage($"\n Khoảng cách yêu cầu: {khoangCachYeuCau}m");

                // Bước 4: Thực hiện hiệu chỉnh
                // Cọc đầu tiên (station nhỏ nhất) giữ nguyên
                SampleLine? firstSl = tr.GetObject(sampleLinesInRange[0], OpenMode.ForRead) as SampleLine;
                if (firstSl == null)
                {
                    A.Ed.WriteMessage("\n Lỗi: Không thể đọc được cọc đầu tiên.");
                    return;
                }

                double stationGoc = firstSl.Station;
                A.Ed.WriteMessage($"\n Cọc gốc (giữ nguyên): Station = {stationGoc:F3}");

                // Điều chỉnh các cọc tiếp theo
                int cocDaDieuChinh = 0;
                for (int i = 1; i < soCoc; i++)
                {
                    SampleLine? sl = tr.GetObject(sampleLinesInRange[i], OpenMode.ForWrite) as SampleLine;
                    if (sl != null)
                    {
                        double stationCu = sl.Station;
                        double stationMoi = stationGoc + (i * khoangCachYeuCau);

                        sl.Station = stationMoi;
                        cocDaDieuChinh++;

                        A.Ed.WriteMessage($"\n   Cọc {i + 1}: {stationCu:F3} → {stationMoi:F3}");
                    }
                }

                tr.Commit();

                A.Ed.WriteMessage($"\n\n=== HOÀN THÀNH ===");
                A.Ed.WriteMessage($"\n Đã hiệu chỉnh {cocDaDieuChinh} cọc với khoảng cách {khoangCachYeuCau}m.");
                A.Ed.WriteMessage($"\n Phạm vi mới: {stationGoc:F3} → {stationGoc + ((soCoc - 1) * khoangCachYeuCau):F3}");
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n Lỗi: {ex.Message}");
                tr.Abort();
            }
        }
    }
}

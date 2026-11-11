// (C) Copyright 2015 by  
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
using ATable = Autodesk.AutoCAD.DatabaseServices.Table;

using Civil = Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using Autodesk.Civil.Runtime;
using Autodesk.Civil.Settings;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.Civil.ApplicationServices;
using CivSurface = Autodesk.Civil.DatabaseServices.TinSurface;
using Section = Autodesk.Civil.DatabaseServices.Section;
using Autodesk.Civil;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using MyFirstProject.Extensions;
using MyFirstProject.Civil_Tool;

// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3DCsharp.CTSV_FitKhungIn))]

namespace Civil3DCsharp
{
    public class CTSV_FitKhungIn
    {
        [CommandMethod("CTS_Fit_Khung_In")]
        public static void CTSVFitKhungIn()
        {
            // ===== BƯỚC 1: Chọn SectionView trước =====
            ObjectIdCollection sectionViewIdColl;
            List<SectionSourceInfo> availableSources;

            using (Transaction trRead = A.Db.TransactionManager.StartTransaction())
            {
                try
                {
                    sectionViewIdColl = UserInput.GSelectionSetWithType("Chọn các SectionView cần fit khung in: \n", "AECC_GRAPH_SECTION_VIEW");
                }
                catch
                {
                    A.Ed.WriteMessage("\nKhông chọn được SectionView. Lệnh đã bị hủy.");
                    trRead.Commit();
                    return;
                }

                // Đọc danh sách section sources từ SectionView đã chọn
                SectionView? sectionView = trRead.GetObject(sectionViewIdColl[0], OpenMode.ForWrite) as SectionView;
                if (sectionView == null)
                {
                    A.Ed.WriteMessage("\nKhông thể đọc SectionView. Lệnh đã bị hủy.");
                    trRead.Commit();
                    return;
                }

                ObjectId sampleLineId = sectionView.SampleLineId;
                SampleLine? sampleLine = trRead.GetObject(sampleLineId, OpenMode.ForWrite) as SampleLine;
                if (sampleLine == null)
                {
                    A.Ed.WriteMessage("\nKhông thể đọc SampleLine. Lệnh đã bị hủy.");
                    trRead.Commit();
                    return;
                }

                ObjectId sampleLineGroupId = sampleLine.GroupId;
                SampleLineGroup? sampleLineGroup = trRead.GetObject(sampleLineGroupId, OpenMode.ForWrite) as SampleLineGroup;
                if (sampleLineGroup == null)
                {
                    A.Ed.WriteMessage("\nKhông thể đọc SampleLineGroup. Lệnh đã bị hủy.");
                    trRead.Commit();
                    return;
                }

                // Lấy tất cả section sources
                SectionSourceCollection sectionSources = sampleLineGroup.GetSectionSources();
                availableSources = new List<SectionSourceInfo>();

                foreach (SectionSource sectionsource in sectionSources)
                {
                    if (sectionsource.IsSampled == true)
                    {
                        try
                        {
                            TinSurface? surface = trRead.GetObject(sectionsource.SourceId, OpenMode.ForRead) as TinSurface;
                            if (surface != null)
                            {
                                availableSources.Add(new SectionSourceInfo
                                {
                                    Name = surface.Name,
                                    SourceType = sectionsource.SourceType.ToString(),
                                    SourceId = sectionsource.SourceId
                                });
                            }
                        }
                        catch { /* skip nếu không đọc được source */ }
                    }
                }

                trRead.Commit();
            }

            // Kiểm tra có source nào không
            if (availableSources.Count == 0)
            {
                A.Ed.WriteMessage("\nKhông tìm thấy section source nào. Vui lòng kiểm tra SectionView.");
                return;
            }

            A.Ed.WriteMessage($"\nTìm thấy {availableSources.Count} section sources.");

            // ===== BƯỚC 2: Hiển thị form với danh sách sources thực tế =====
            FitKhungInForm form = new(availableSources);
            if (form.ShowDialog() != System.Windows.Forms.DialogResult.OK || !form.FormAccepted)
            {
                A.Ed.WriteMessage("\nLệnh đã bị hủy.");
                return;
            }

            // Lấy giá trị từ form
            double moRongKhungDungTren = form.MoRongDungTren;
            double moRongKhungDungDuoi = form.MoRongDungDuoi;
            double moRongKhungNgangTrai = form.MoRongNgangTrai;
            double moRongKhungNgangPhai = form.MoRongNgangPhai;
            List<SectionSourceInfo> selectedSources = form.SelectedSources;
            bool apDungDung = form.ApDungDung;
            bool apDungNgang = form.ApDungNgang;

            A.Ed.WriteMessage($"\nSố section sources được chọn: {selectedSources.Count}");
            foreach (var src in selectedSources)
            {
                A.Ed.WriteMessage($"\n  - {src.Name} ({src.SourceType})");
            }

            // ===== BƯỚC 3: Xử lý fit khung in =====
            List<ObjectId> sourceIds = selectedSources.Select(s => s.SourceId).ToList();

            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                int processedCount = 0;
                int skippedCount = 0;

                foreach (ObjectId sectionviewId in sectionViewIdColl)
                {
                    SectionView? sectionView1 = tr.GetObject(sectionviewId, OpenMode.ForWrite) as SectionView;
#pragma warning disable CS8602
                    ObjectId sampleLine1Id = sectionView1.SampleLineId;
#pragma warning restore CS8602
                    SampleLine? sampleLine1 = tr.GetObject(sampleLine1Id, OpenMode.ForWrite) as SampleLine;

                    // Tính envelope min/max từ tất cả sections được chọn
                    double envelopeYmin = double.MaxValue;
                    double envelopeYmax = double.MinValue;
                    double envelopeXmin = double.MaxValue;
                    double envelopeXmax = double.MinValue;
                    bool hasValidData = false;

                    foreach (ObjectId sourceId in sourceIds)
                    {
                        ObjectId sectionTnId;
                        try
                        {
#pragma warning disable CS8602
                            sectionTnId = sampleLine1.GetSectionId(sourceId);
#pragma warning restore CS8602
                        }
                        catch (System.ArgumentException)
                        {
                            continue;
                        }

                        if (sectionTnId.IsNull || !sectionTnId.IsValid)
                            continue;

                        Section? section = tr.GetObject(sectionTnId, OpenMode.ForWrite) as Section;
                        if (section == null)
                            continue;

                        try
                        {
                            double secYmin = section.MinmumElevation;
                            double secYmax = section.MaximumElevation;
                            double secXmin = section.LeftOffset;
                            double secXmax = section.RightOffset;

                            if (secYmin < envelopeYmin) envelopeYmin = secYmin;
                            if (secYmax > envelopeYmax) envelopeYmax = secYmax;
                            if (secXmin < envelopeXmin) envelopeXmin = secXmin;
                            if (secXmax > envelopeXmax) envelopeXmax = secXmax;

                            hasValidData = true;
                        }
                        catch (System.InvalidOperationException)
                        {
                            continue;
                        }
                    }

                    if (!hasValidData)
                    {
                        A.Ed.WriteMessage($"\nCảnh báo: Không có dữ liệu section cho SectionView tại station {sampleLine1?.Station}. Bỏ qua...");
                        skippedCount++;
                        continue;
                    }

                    // Áp dụng mở rộng khung
                    double ymin = Math.Round(envelopeYmin - moRongKhungDungDuoi, 0);
                    double ymax = Math.Round(envelopeYmax + moRongKhungDungTren, 0);
                    double xmin = Math.Round(envelopeXmin - moRongKhungNgangTrai, 0);
                    double xmax = Math.Round(envelopeXmax + moRongKhungNgangPhai, 0);

                    // Set sectionview
                    if (apDungNgang)
                    {
                        sectionView1.IsOffsetRangeAutomatic = false;
                        sectionView1.OffsetLeft = xmin;
                        sectionView1.OffsetRight = xmax;
                    }
                    if (apDungDung)
                    {
                        sectionView1.IsElevationRangeAutomatic = false;
                        sectionView1.ElevationMin = ymin;
                        sectionView1.ElevationMax = ymax;
                    }

                    processedCount++;
                }

                A.Ed.WriteMessage($"\n\nHoàn thành: {processedCount} SectionView đã được fit khung in.");
                if (skippedCount > 0)
                {
                    A.Ed.WriteMessage($"\n{skippedCount} SectionView bị bỏ qua do không có dữ liệu.");
                }

                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }
    }
}

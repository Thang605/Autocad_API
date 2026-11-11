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

// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3DCsharp.SectionViews))]

namespace Civil3DCsharp
{
    public class SectionViews
    {

        
        [CommandMethod("CTS_Chuyen_Doi_TNTK_TNTN")]
        public static void CTSVChuyenDoiTNTKTNTN()
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here
                ObjectId sectionViewId1 = UserInput.GSectionView("Chọn 1 trắc ngang trong nhóm cần hiệu chỉnh: \n");
                SectionView? sectionview1 = tr.GetObject(sectionViewId1, OpenMode.ForWrite) as SectionView;

#pragma warning disable CS8602 // Dereference of a possibly null reference.
                ObjectId sampleLineId1 = sectionview1.SampleLineId;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                SampleLine? sampleLine1 = tr.GetObject(sampleLineId1, OpenMode.ForWrite) as SampleLine;
#pragma warning disable CS8604 // Possible null reference argument.
                ObjectId alignmentId = sampleLine1.GetParentAlignmentId();
#pragma warning restore CS8604 // Possible null reference argument.
                ObjectId sampleLineGroupId = sampleLine1.GroupId;
                SampleLineGroup? sampleLineGroup = tr.GetObject(sampleLineGroupId, OpenMode.ForWrite) as SampleLineGroup;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                ObjectIdCollection sampleLineIds = sampleLineGroup.GetSampleLineIds();
#pragma warning restore CS8602 // Dereference of a possibly null reference.

                //remove sectionsource
                ObjectIdCollection sectionSourceIdColl = [];
                SectionSourceCollection sectionSources = sampleLineGroup.GetSectionSources();
                foreach (SectionSource sectionsource in sectionSources)
                {


                    if (sectionsource.SourceType == SectionSourceType.TinSurface)
                    {
                        TinSurface? type = tr.GetObject(sectionsource.SourceId, OpenMode.ForRead) as TinSurface;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                        if (type.Name.Contains("TN"))
                        {
                            sectionsource.IsSampled = true;
                            sectionSourceIdColl.Add(sectionsource.SourceId);
                            sectionsource.StyleId = A.Cdoc.Styles.SectionStyles["1.TN Ground"];

                        }
                        else sectionsource.IsSampled = false;
#pragma warning restore CS8602 // Dereference of a possibly null reference.

                    }

                    if (sectionsource.SourceType == SectionSourceType.Corridor)
                    {
                        Corridor? type = tr.GetObject(sectionsource.SourceId, OpenMode.ForRead) as Corridor;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                        if (type.Name.Contains("MoHinh"))
                        {
                            sectionsource.IsSampled = false;
                            //sectionSourceIdColl.Add(sectionsource.SourceId);
                            sectionsource.StyleId = A.Cdoc.Styles.CodeSetStyles["1. All Codes 1-1000"];
                        }
#pragma warning restore CS8602 // Dereference of a possibly null reference.

                    }

                    if (sectionsource.SourceType == SectionSourceType.CorridorSurface)
                    {
                        TinSurface? type = tr.GetObject(sectionsource.SourceId, OpenMode.ForRead) as TinSurface;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                        if (type.Name.Contains("top"))
                        {
                            sectionsource.IsSampled = false;
                            sectionSourceIdColl.Add(sectionsource.SourceId);
                            sectionsource.StyleId = A.Cdoc.Styles.SectionStyles["2.Top Ground"];
                        }
                        else if (type.Name.Contains("datum"))
                        {
                            sectionsource.IsSampled = false;
                            sectionSourceIdColl.Add(sectionsource.SourceId);
                            sectionsource.StyleId = A.Cdoc.Styles.SectionStyles["3.Datum Ground"];
                        }
                        else sectionsource.IsSampled = false;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                    }

                    if (sectionsource.SourceType == SectionSourceType.Material)
                    {
                        sectionsource.IsSampled = false;
                    }

                }

                //remove band
                foreach (ObjectId sampleLineId in sampleLineIds)
                {
                    SampleLine? sampleLine = tr.GetObject(sampleLineId, OpenMode.ForWrite) as SampleLine;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    ObjectIdCollection sectionViewIdColl = sampleLine.GetSectionViewIds();
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                    foreach (ObjectId sectionViewId in sectionViewIdColl)
                    {
                        SectionView? sectionView = tr.GetObject(sectionViewId, OpenMode.ForWrite) as SectionView;
#pragma warning disable CS8604 // Possible null reference argument.
                        UtilitiesC3D.RemoveSectionBand(sectionView, "Cao do thiet ke 1-1000");
#pragma warning restore CS8604 // Possible null reference argument.
                        UtilitiesC3D.RemoveSectionBand(sectionView, "Khoang cach mia TK 1-1000");
                    }
                }

                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }


        [CommandMethod("CTS_Danh_Cap")]
        public static void CTSVDanhCap()
        {
            // start transantion CTSV_DanhCap
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();

                //start here
                ObjectId sectionViewId = UserInput.GSectionView("Chọn 1 bảng cắt ngang " + " trong nhóm cần tính đánh cấp: \n");
                SectionView? sectionView = tr.GetObject(sectionViewId, OpenMode.ForWrite) as SectionView;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                ObjectId sampleLineId = sectionView.SampleLineId;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                SampleLine? sampleLine = tr.GetObject(sampleLineId, OpenMode.ForWrite) as SampleLine;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                ObjectId sampleLineGroupId = sampleLine.GroupId; ;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                SampleLineGroup? sampleLineGroup = tr.GetObject(sampleLineGroupId, OpenMode.ForWrite) as SampleLineGroup;

                // vị trí text đánh cấp
                Double deltaX = 3;
                double deltaY = 7; //UI.G_Double("Nhập vị trí đặt text đánh cấp: (7) \n");
                double deltaX2 = 4; // UI.G_Double("Nhập khoảng giữa tên vật liệu và khối lượng: (4)");

                //số cột trong bảng đánh cấp
                List<String> listLyTrinh = [];
                List<String> listTenCoc = [];
                List<String> listDanhCap = [];

                ObjectId alignmentId = sampleLine.GetParentAlignmentId();
                Alignment? alignment = tr.GetObject(alignmentId, OpenMode.ForWrite) as Alignment;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                String alignmentName = alignment.Name + "_danhCap";
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                LayerTableRecord layer = UtilitiesCAD.CCreateLayer(alignmentName);

                //lấy sectionsource TN và DATUM
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                SectionSourceCollection sectionSources = sampleLineGroup.GetSectionSources();
#pragma warning restore CS8602 // Dereference of a possibly null reference.

                ObjectId sectionSource_TN_Id = new();
                foreach (SectionSource sectionsource in sectionSources)
                {
                    if ((sectionsource.SourceType == SectionSourceType.TinSurface) & (sectionsource.IsSampled == true))
                    {
                        TinSurface? type = tr.GetObject(sectionsource.SourceId, OpenMode.ForRead) as TinSurface;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                        if (type.Name.Contains("TN", StringComparison.CurrentCultureIgnoreCase))
                        {
                            sectionSource_TN_Id = sectionsource.SourceId;
                        }
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                    }
                }
                ObjectId sectionSource_datum_Id = new();
                foreach (SectionSource sectionsource in sectionSources)
                {
                    if ((sectionsource.SourceType == SectionSourceType.CorridorSurface) & (sectionsource.IsSampled == true))
                    {
                        TinSurface? type = tr.GetObject(sectionsource.SourceId, OpenMode.ForRead) as TinSurface;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                        if (type.Name.Contains("DATUM", StringComparison.CurrentCultureIgnoreCase))
                        {
                            sectionSource_datum_Id = sectionsource.SourceId;
                        }
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                    }
                }


                // get sectionViewIdColl
                //SectionViewGroup sectionViewGroup = sectionView.SectionViewGroupObject();

                SectionViewGroupCollection sectionViewGroupCollection = sampleLineGroup.SectionViewGroups;
                SectionViewGroup sectionViewGroup = sectionViewGroupCollection[0];
                if (sectionViewGroupCollection.Count > 1)
                {
                    int num = 0;
                    A.Ed.WriteMessage("Danh sách nhóm cắt ngang:\n");
                    foreach (var item in sectionViewGroupCollection)
                    {
                        A.Ed.WriteMessage(num.ToString() + " " + sectionViewGroupCollection[num].Name.ToString() + "\n");
                        num++;
                    }
                    int numPass = UserInput.GInt("Nhập thứ tự nhóm cắt ngang cần tính đánh cấp:");
                    sectionViewGroup = sectionViewGroupCollection[numPass];
                }


                ObjectIdCollection sectionViewIdColl = sectionViewGroup.GetSectionViewIds();
                double bacCap = 2; // UI.G_Double("\n Nhập bề rộng đánh cấp (2m):");
                double docDanhCap = 0.2; // UI.G_Double("\n Nhập điều kiện đánh cấp (0.2):");

                //vẽ đánh cấp
                // add section Tn và Datum
                foreach (ObjectId sectionviewId in sectionViewIdColl)
                {
                    SectionView? sectionView1 = tr.GetObject(sectionviewId, OpenMode.ForWrite) as SectionView;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    ObjectId sampleLine1Id = sectionView1.SampleLineId;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                    SampleLine? sampleLine1 = tr.GetObject(sampleLine1Id, OpenMode.ForWrite) as SampleLine;

#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    ObjectId sectionTnId = sampleLine1.GetSectionId(sectionSource_TN_Id);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                    Section? section = tr.GetObject(sectionTnId, OpenMode.ForWrite) as Section;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    SectionPointCollection sectionPoints = section.SectionPoints;
#pragma warning restore CS8602 // Dereference of a possibly null reference.

                    //get sectionview location
                    double X = sectionView1.Location.X;
                    double Y = sectionView1.Location.Y;
                    sectionView1.IsElevationRangeAutomatic = false;
                    double Z = sectionView1.ElevationMin;
                    double Z1 = sectionView1.ElevationMax;
                    sectionView1.IsElevationRangeAutomatic = true;

                    //mat datum để kiểm tra đánh cấp hay ko
                    ObjectId sectionDatumId = sampleLine1.GetSectionId(sectionSource_datum_Id);
                    Section? sectionDatum = tr.GetObject(sectionDatumId, OpenMode.ForWrite) as Section;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    SectionPointCollection sectionDatumPoints = sectionDatum.SectionPoints;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                    double X0 = sectionDatum.LeftOffset + X;
                    double Xn = sectionDatum.RightOffset + X;

                    double polyLineArea = new();
                    List<Double> ListArea = [];

                    for (int i = 0; i < sectionPoints.Count - 1; i++)
                    {
                        double x1 = sectionPoints[i].Location.X + X;
                        double x2 = sectionPoints[i + 1].Location.X + X;
                        double y1 = sectionPoints[i].Location.Y + Y - Z;
                        double y2 = sectionPoints[i + 1].Location.Y + Y - Z;
                        double at = (y2 - y1) / (x2 - x1);
                        double b = -x1 * (y2 - y1) / (x2 - x1) + y1;

                        //kiểm tra dk đánh cấp
                        if (Math.Abs(at) >= docDanhCap)
                        {
                            //tìm x1, x2  

                            if (!((x1 < X0 & x2 < X0) | (x1 > Xn & x2 > Xn)))
                            {
                                //kiểm điều kiện đánh cấp theo phương X
                                {
                                    if (x1 < X0 & x2 > X0 & x2 <= Xn)
                                    {
                                        x1 = X0;
                                    }
                                    if (x1 < X0 & x2 >= Xn)
                                    {
                                        x1 = X0;
                                        x2 = Xn;
                                    }
                                    if (x1 >= X0 & x2 <= Xn)
                                    {
                                        //x1 = x1;
                                        //x2 = x2;
                                    }
                                    if (x1 <= Xn & x2 > Xn)
                                    {
                                        x2 = Xn;
                                    }
                                    y1 = at * x1 + b;
                                    y2 = at * x2 + b;
                                }

                                {
                                    //kiểm điều kiện đánh cấp theo phương Y
                                    double yt1 = UtilitiesC3D.FindY(sectionDatumPoints, x1, X, Y, Z);
                                    double yt2 = UtilitiesC3D.FindY(sectionDatumPoints, x2, X, Y, Z);

                                    if (!(yt1 <= y1 & yt2 <= y2))
                                    {

                                        if (yt1 > y1 & yt2 > y2)
                                        {
                                        }

                                        if (yt1 > y1 & yt2 < y2)
                                        {

                                            double x = x1;
                                            double yt11 = UtilitiesC3D.FindY(sectionDatumPoints, x, X, Y, Z);
                                            double y11 = at * x + b;
                                            while (Math.Abs(yt11 - y11) > 0.1)
                                            {
                                                yt11 = UtilitiesC3D.FindY(sectionDatumPoints, x, X, Y, Z);
                                                y11 = at * x + b;
                                                x += 0.1;
                                            }
                                            x2 = x;
                                        }
                                        if (yt1 < y1 & yt2 > y2)
                                        {

                                            double x = x1;
                                            double yt11 = UtilitiesC3D.FindY(sectionDatumPoints, x, X, Y, Z);
                                            double y11 = at * x + b;
                                            while (Math.Abs(yt11 - y11) > 0.1)
                                            {
                                                yt11 = UtilitiesC3D.FindY(sectionDatumPoints, x, X, Y, Z);
                                                y11 = at * x + b;
                                                x += 0.1;
                                            }
                                            x1 = x;
                                        }
                                        y1 = at * x1 + b;
                                        y2 = at * x2 + b;
                                        polyLineArea = UtilitiesCAD.CreatePolylineDanhCap(x1, x2, y1, y2, bacCap, docDanhCap);
                                        ListArea.Add(polyLineArea);
                                    }

                                }

                            }


                        }

                    }

                    //ghi text đánh cấp                        
                    Point3d textPosition = new(X + deltaX, Y + deltaY, 0);
                    UtilitiesCAD.CCreateTextWithOutPut(textPosition, 0.4, "Đánh cấp:", alignmentName, "Standard");
                    Point3d textPosition2 = new(X + deltaX + deltaX2, Y + deltaY, 0);
                    DBText Text = UtilitiesCAD.CCreateText2(textPosition2, 0.4, UtilitiesCAD.CSumList(ListArea).ToString("N2") + " m2", alignmentName, "Standard");
                    String textFile = UtilitiesCAD.ConvertTextToField(Text);

                    //đưa data vào mảng của table
                    listLyTrinh.Add(sampleLine1.Station.ToString("N2"));
                    listTenCoc.Add(sampleLine1.Name.ToString());
                    listDanhCap.Add(textFile);

                }

                // bảng đánh cấp

                UtilitiesCAD.CreateTableKhoiLuong(listLyTrinh.Count, 3, alignment.Name, listLyTrinh, listTenCoc, listDanhCap, alignmentName, "đánh cấp");



                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }

        [CommandMethod("CTS_Danh_Cap_Xoa_Bo")]
        public static void CTSVDanhCapXoaBo()
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            UserInput UI = new();
            UtilitiesCAD CAD = new();
            UtilitiesC3D C3D = new();
            try
            {
                ObjectId polylineId = UserInput.GPolyline("/n Chọn 1 polyline " + " trong nhóm đánh cấp cần xóa: \n");
                Polyline? polyline = tr.GetObject(polylineId, OpenMode.ForWrite) as Polyline;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                String layerPolyline = polyline.Layer;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                UtilitiesCAD.CDelLayerAndObjectOnIt(layerPolyline);

                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }


        [CommandMethod("CTS_Danh_Cap_Ve_Them")]
        public static void CTSVDanhCapVeThem()
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here
                ObjectId sectionViewId = UserInput.GSectionView("\n Chọn 1 bảng cắt ngang " + " trong nhóm cần tính đánh cấp bổ sung: \n");
                SectionView? sectionView = tr.GetObject(sectionViewId, OpenMode.ForWrite) as SectionView;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                ObjectId sampleLineId = sectionView.SampleLineId;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                SampleLine? sampleLine = tr.GetObject(sampleLineId, OpenMode.ForWrite) as SampleLine;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                ObjectId sampleLineGroupId = sampleLine.GroupId; ;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                SampleLineGroup? sampleLineGroup = tr.GetObject(sampleLineGroupId, OpenMode.ForWrite) as SampleLineGroup;

                ObjectId alignmentId = sampleLine.GetParentAlignmentId();
                Alignment? alignment = tr.GetObject(alignmentId, OpenMode.ForWrite) as Alignment;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                String alignmentName = alignment.Name + "_danhCap";
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                Point3d point1 = UserInput.GPoint("\n Chọn vị trí điểm" + "\n Chọn vị trí điểm" + "để xác định điểm ĐẦU đánh cấp bổ sung: \n");
                Point3d point2 = UserInput.GPoint("\n Chọn vị trí điểm" + "\n Chọn vị trí điểm" + "để xác định điểm CUỐI đánh cấp bổ sung: \n");
                UtilitiesCAD.CreatePolylineDanhCap(point1.X, point2.X, point1.Y, point2.Y, 2, 0.2);

                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }

        [CommandMethod("CTS_Danh_Cap_Ve_Them_2")]
        public static void CTSVDanhCapVeThem2()
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                Point3d point1 = UserInput.GPoint("\n Chọn vị trí điểm" + "\n Chọn vị trí điểm" + "để xác định điểm ĐẦU đánh cấp bổ sung: \n");
                Point3d point2 = UserInput.GPoint("\n Chọn vị trí điểm" + "\n Chọn vị trí điểm" + "để xác định điểm CUỐI đánh cấp bổ sung: \n");
                UtilitiesCAD.CreatePolylineDanhCap(point1.X, point2.X, point1.Y, point2.Y, 2, 0.2);

                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }

        [CommandMethod("CTS_Danh_Cap_Ve_Them_1")]
        public static void CTSVDanhCapVeThem1()
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                Point3d point1 = UserInput.GPoint("\n Chọn vị trí điểm" + "\n Chọn vị trí điểm" + "để xác định điểm ĐẦU đánh cấp bổ sung: \n");
                Point3d point2 = UserInput.GPoint("\n Chọn vị trí điểm" + "\n Chọn vị trí điểm" + "để xác định điểm CUỐI đánh cấp bổ sung: \n");
                UtilitiesCAD.CreatePolylineDanhCap(point1.X, point2.X, point1.Y, point2.Y, 1, 0.2);

                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }

        [CommandMethod("CTS_Danh_Cap_Cap_Nhat")]
        public static void CTSVDanhCapCapNhat()
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            UserInput UI = new();
            UtilitiesCAD CAD = new();
            UtilitiesC3D C3D = new();
            try
            {
                ObjectIdCollection polylineIds = UserInput.GSelectionSetWithType("trong nhóm polyline đánh cấp cần bổ sung khối lượng: \n", "LWPOLYLINE");
                List<Double> listPolyLineArea = [];
                foreach (ObjectId polylineId in polylineIds)
                {
                    Polyline? polyline = tr.GetObject(polylineId, OpenMode.ForWrite) as Polyline;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    listPolyLineArea.Add(polyline.Area);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                }
                BlockTable? acBlkTbl;
                acBlkTbl = tr.GetObject(A.Db.BlockTableId, OpenMode.ForRead) as BlockTable;
                // Open the Block table record Model space for write
                BlockTableRecord? acBlkTblRec;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                acBlkTblRec = tr.GetObject(acBlkTbl[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                // Change text contatant
                ObjectId textId = UserInput.GDbText("\n Chọn 1 text  " + " để cập nhật đánh cấp: \n");
                DBText? dBText = tr.GetObject(textId, OpenMode.ForWrite) as DBText;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                dBText.TextString = "Đánh cấp: " + UtilitiesCAD.CSumList(listPolyLineArea).ToString("N2") + " m2";
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                dBText.ColorIndex = 1;


                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }

        [CommandMethod("CTS_Them_Vat_Lieu_Tren_Cat_Ngang")]
        public static void CTSVThemVatLieuTrenCatNgang()
        {
            // start transantion CTSV_DanhCap
            using Transaction tr = A.Db.TransactionManager.StartTransaction();

            UserInput UI = new();
            UtilitiesCAD CAD = new();
            UtilitiesC3D C3D = new();

            SectionViewGroupCreationPlacementOptions sectionViewGroupCreationPlacementOptions = new();
            sectionViewGroupCreationPlacementOptions.UseProductionPlacement("Z:/Z.FORM MAU LAM VIEC/1. BIM/2.MAU C3D/2.THU VIEN C3D/2.LAYOUT C3D/LAYOUT CIVIL 3D.dwt", "A3-TN-1-200");

            //start here
            ObjectId sectionViewId = UserInput.GSectionView("\n Chọn 1 bảng cắt ngang để thêm text khối lượng: ");
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
            String tenVatLieu = UserInput.GString("\n Nhập tên vật liệu cần gắn lên trắc ngang:");
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            tenVatLieu = tenVatLieu.Replace(":", "");
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
            String donViVatLieu = UserInput.GString("\n Nhập tên đơn vị của vật liệu (m or m2):");
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
            SectionView? sectionView = tr.GetObject(sectionViewId, OpenMode.ForWrite) as SectionView;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            ObjectId sampleLineId = sectionView.SampleLineId;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            SampleLine? sampleLine = tr.GetObject(sampleLineId, OpenMode.ForWrite) as SampleLine;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            ObjectId sampleLineGroupId = sampleLine.GroupId;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            SampleLineGroup? sampleLineGroup = tr.GetObject(sampleLineGroupId, OpenMode.ForWrite) as SampleLineGroup;

            //find offset, elevation
            Point3d point3D = UserInput.GPoint("\n Chọn vị trí điểm cần đặt TEXT vật liệu trên trắc ngang: \n ");
            double offset = 1;
            double elevation = 2;
            sectionView.FindOffsetAndElevationAtXY(point3D.X, point3D.Y, ref offset, ref elevation);
            Double deltaX = offset;
            double deltaY = elevation;
            double deltaX2 = tenVatLieu.Length * 0.35;

            //số cột trong bảng đánh cấp
            List<String> listLyTrinh = [];
            List<String> listTenCoc = [];
            List<String> listKhoiLuong = [];
            List<Point3d> listTextPosition = [];
            List<Point3d> listTextPosition2 = [];

            ObjectId alignmentId = sampleLine.GetParentAlignmentId();
            Alignment? alignment = tr.GetObject(alignmentId, OpenMode.ForWrite) as Alignment;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            String alignmentName = alignment.Name + "_" + tenVatLieu;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            LayerTableRecord layer = UtilitiesCAD.CCreateLayer(alignmentName);

            // get sectionViewIdColl
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            SectionViewGroupCollection sectionViewGroupCollection = sampleLineGroup.SectionViewGroups;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            SectionViewGroup sectionViewGroup = sectionViewGroupCollection[0];
            if (sectionViewGroupCollection.Count > 1)
            {
                int num = 0;
                A.Ed.WriteMessage("\n Danh sách nhóm cắt ngang:");
                foreach (var item in sectionViewGroupCollection)
                {
                    A.Ed.WriteMessage(num.ToString() + " " + sectionViewGroupCollection[num].Name.ToString());
                    num++;
                }
                int numPass = UserInput.GInt("\n Đường có nhiều hơn 1 nhóm cắt ngang! \n Nhập thứ tự nhóm cắt ngang cần tính đánh cấp:");
                sectionViewGroup = sectionViewGroupCollection[numPass];
            }

            ObjectIdCollection sectionViewIdColl = sectionViewGroup.GetSectionViewIds();

            // add section Tn và Datum
            foreach (ObjectId sectionviewId in sectionViewIdColl)
            {
                SectionView? sectionView1 = tr.GetObject(sectionviewId, OpenMode.ForWrite) as SectionView;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                ObjectId sampleLine1Id = sectionView1.SampleLineId;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                SampleLine? sampleLine1 = tr.GetObject(sampleLine1Id, OpenMode.ForWrite) as SampleLine;

                //get sectionview location
                double X = sectionView1.Location.X;
                double Y = sectionView1.Location.Y;

                //ghi text đánh cấp                        
                Point3d textPosition = new(X + deltaX, Y + deltaY, 0);
                Point3d textPosition2 = new(X + deltaX + deltaX2, Y + deltaY, 0);

                //đưa data vào mảng của table
                listTextPosition.Add(textPosition);
                listTextPosition2.Add(textPosition2);
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                listLyTrinh.Add(sampleLine1.Station.ToString("N2"));
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                listTenCoc.Add(sampleLine1.Name.ToString());
            }


            tenVatLieu += ":";
            for (int i = 0; i < listLyTrinh.Count; i++)
            {
                UtilitiesCAD.CCreateTextWithOutPut(listTextPosition[i], 0.4, tenVatLieu, alignmentName, "Standard");
                DBText Text = UtilitiesCAD.CCreateText2(listTextPosition2[i], 0.4, "0.00 " + donViVatLieu, alignmentName, "Standard");
                String textFile = UtilitiesCAD.ConvertTextToField(Text);
                listKhoiLuong.Add(textFile);

            }
            UtilitiesCAD.CreateTableKhoiLuong(listLyTrinh.Count, 3, alignment.Name, listLyTrinh, listTenCoc, listKhoiLuong, alignmentName, tenVatLieu);

            tr.Commit();
        }

        [CommandMethod("CTS_Thay_Doi_MSS_Min_Max")]
        public static void CTSVThayDoiMSSMinMax()
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here
                ObjectIdCollection objectIdCollection = UserInput.GSelectionSet("Chọn các cắt ngang cần thay đổi MSS:");
                //get MSS min max
                Double X = 0;
                Double elevationMin = 0;
                Double elevationMax = 0;
                Point3d point3D_min = UserInput.GPoint("\n Chọn MSS min:");
                Point3d point3D_max = UserInput.GPoint("\n Chọn MSS max:");
                //set MSS min max
                SectionView? sectionView0 = tr.GetObject(objectIdCollection[0], OpenMode.ForWrite) as SectionView;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                sectionView0.FindOffsetAndElevationAtXY(point3D_min.X, point3D_min.Y, ref X, ref elevationMin);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                sectionView0.FindOffsetAndElevationAtXY(point3D_max.X, point3D_max.Y, ref X, ref elevationMax);
                //set for all sectionviews
                foreach (ObjectId objectId in objectIdCollection)
                {
                    if (objectId.ObjectClass.DxfName == "AECC_GRAPH_SECTION_VIEW")
                    {
                        SectionView? sectionView = tr.GetObject(objectId, OpenMode.ForWrite) as SectionView;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                        sectionView.IsElevationRangeAutomatic = false;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                        sectionView.ElevationMax = elevationMax;
                        sectionView.ElevationMin = elevationMin;
                    }
                }

                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }

        [CommandMethod("CTS_Thay_Doi_Gioi_Han_Trai_Phai")]
        public static void CTSVThayDoiGioiHanTraiPhai()
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here
                ObjectIdCollection objectIdCollection = UserInput.GSelectionSet("Chọn các cắt ngang cần thay đổi bề rộng trái phải:");
                //get MSS min max
                Double X = 0;
                Double OffsetLeft = 0;
                Double OffsetRight = 0;
                Point3d point3D_left = UserInput.GPoint("\n Chọn điểm giới hạn bên trái:");
                Point3d point3D_right = UserInput.GPoint("\n Chọn điểm giới hạn bên phải:");
                //set MSS min max
                SectionView? sectionView0 = tr.GetObject(objectIdCollection[0], OpenMode.ForWrite) as SectionView;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                sectionView0.FindOffsetAndElevationAtXY(point3D_left.X, point3D_left.Y, ref OffsetLeft, ref X);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                sectionView0.FindOffsetAndElevationAtXY(point3D_right.X, point3D_right.Y, ref OffsetRight, ref X);
                //set for all sectionviews
                foreach (ObjectId objectId in objectIdCollection)
                {
                    if (objectId.ObjectClass.DxfName == "AECC_GRAPH_SECTION_VIEW")
                    {
                        SectionView? sectionView = tr.GetObject(objectId, OpenMode.ForWrite) as SectionView;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                        sectionView.IsOffsetRangeAutomatic = false;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                        sectionView.OffsetLeft = OffsetLeft;
                        sectionView.OffsetRight = OffsetRight;
                    }
                }

                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }


        [CommandMethod("CTS_Thay_Doi_Khung_In")]
        public static void CTSVThayDoiKhungIn()
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here
                ObjectIdCollection objectIdCollection = UserInput.GSelectionSetWithType("Chọn các cắt ngang cần thay đổi khung in \n (Chọn cắt ngang đầu, sau đó chọn các cắt ngang còn lại):", "AECC_GRAPH_SECTION_VIEW");
                //get MSS min max
                Double X = 0;
                Double OffsetLeft = 0;
                Double OffsetRight = 0;
                Point3d point3D_left = UserInput.GPoint("\n Chọn điểm giới hạn bên trái dưới:");
                Point3d point3D_right = UserInput.GPoint("\n Chọn điểm giới hạn bên phải trên:");
                //set MSS min max
                SectionView? sectionView0 = tr.GetObject(objectIdCollection[0], OpenMode.ForWrite) as SectionView;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                sectionView0.FindOffsetAndElevationAtXY(point3D_left.X, point3D_left.Y, ref OffsetLeft, ref X);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                sectionView0.FindOffsetAndElevationAtXY(point3D_right.X, point3D_right.Y, ref OffsetRight, ref X);
                //set for all sectionviews
                foreach (ObjectId objectId in objectIdCollection)
                {
                    if (objectId.ObjectClass.DxfName == "AECC_GRAPH_SECTION_VIEW")
                    {
                        SectionView? sectionView = tr.GetObject(objectId, OpenMode.ForWrite) as SectionView;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                        sectionView.IsOffsetRangeAutomatic = false;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                        sectionView.OffsetLeft = OffsetLeft;
                        sectionView.OffsetRight = OffsetRight;
                    }
                }
                Double elevationMin = 0;
                Double elevationMax = 0;
                //Point3d point3D_min = UI.G_point("\n Chọn MSS min:");
                //Point3d point3D_max = UI.G_point("\n Chọn MSS max:");
                //set MSS min max
                //SectionView sectionView0 = tr.GetObject(objectIdCollection[0], OpenMode.ForWrite) as SectionView;
                sectionView0.FindOffsetAndElevationAtXY(point3D_left.X, point3D_left.Y, ref X, ref elevationMin);
                sectionView0.FindOffsetAndElevationAtXY(point3D_right.X, point3D_right.Y, ref X, ref elevationMax);
                //set for all sectionviews
                foreach (ObjectId objectId in objectIdCollection)
                {
                    if (objectId.ObjectClass.DxfName == "AECC_GRAPH_SECTION_VIEW")
                    {
                        SectionView? sectionView = tr.GetObject(objectId, OpenMode.ForWrite) as SectionView;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                        sectionView.IsElevationRangeAutomatic = false;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                        sectionView.ElevationMax = elevationMax;
                        sectionView.ElevationMin = elevationMin;
                    }
                }
                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }

        [CommandMethod("CTS_Khoa_Cat_Ngang_Add_Point")]
        public static void CTSVKhoaCatNgangAddPoint()
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here
                ObjectId sectionViewId = UserInput.GSectionView("Chọn 1 cắt trong nhóm những cắt ngang");
                SectionView? sectionView = tr.GetObject(sectionViewId, OpenMode.ForWrite) as SectionView;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                sectionView.Description = "check";
#pragma warning restore CS8602 // Dereference of a possibly null reference.

                // chọn mặt phảng để get point
                ObjectId surfaceId = UserInput.GSurfaceId("Chọn mặt phẳng để lấy thông tin khóa cắt ngang");
                CivSurface? civSurface = tr.GetObject(surfaceId, OpenMode.ForWrite) as CivSurface;

                double khoangCachDiemMia = UserInput.GInt("Nhập khoảng cách điểm mia tối thiểu yêu cầu:");

                // chọn mặt phảng để get point
                ObjectId surfaceId2 = UserInput.GSurfaceId("Chọn mặt phẳng để thêm điểm khóa cắt ngang");
                CivSurface? civSurface2 = tr.GetObject(surfaceId2, OpenMode.ForWrite) as CivSurface;


                ObjectId sampleLineId = sectionView.SampleLineId;
                SampleLine? sampleLine = tr.GetObject(sampleLineId, OpenMode.ForWrite) as SampleLine;
#pragma warning disable CS8604 // Possible null reference argument.
                ObjectId alignmentId = sampleLine.GetParentAlignmentId();
#pragma warning restore CS8604 // Possible null reference argument.
                Alignment? alignment = tr.GetObject(alignmentId, OpenMode.ForWrite) as Alignment;

                ObjectId sampleLineGroupId = sampleLine.GroupId;
                SampleLineGroup? sampleLineGroup = tr.GetObject(sampleLineGroupId, OpenMode.ForWrite) as SampleLineGroup;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                SectionViewGroupCollection sectionViewGroups = sampleLineGroup.SectionViewGroups;
#pragma warning restore CS8602 // Dereference of a possibly null reference.

                //tìm sectionViewGroup của cắt ngang được chọn
                /*
                int STT = 0;
                String check ="";
                foreach (SectionViewGroup sectionViewGroup in sectionViewGroups)
                {
                    ObjectIdCollection sectionViewColl = sectionViewGroup.GetSectionViewIds();
                    foreach (ObjectId sectionViewId1 in sectionViewColl)
                    {
                        SectionView sectionView1 = tr.GetObject(sectionViewId1, OpenMode.ForWrite) as SectionView;
                        if (sectionView1.Description == "check")
                        {
                            check = STT.ToString() + "ok";
                            sectionView1.Description = "";
                        }
                        STT++;
                    }
                }
                SectionViewGroup sectionViewGroup1_ok = sectionViewGroups[int.Parse(check.Substring(0, 1))];
                ObjectIdCollection sectionViews = sectionViewGroup1_ok.GetSectionViewIds();
                */
                SectionViewGroup sectionViewGroup1_ok = sectionViewGroups[0];
                ObjectIdCollection sectionViews = sectionViewGroup1_ok.GetSectionViewIds();

                //lấy sectionsource TN
                SectionSourceCollection sectionSources = sampleLineGroup.GetSectionSources();
                ObjectId sectionSource_TN_Id = new();
                foreach (SectionSource sectionsource in sectionSources)
                {
                    if ((sectionsource.SourceType == SectionSourceType.TinSurface) & (sectionsource.IsSampled == true))
                    {
                        TinSurface? type = tr.GetObject(sectionsource.SourceId, OpenMode.ForRead) as TinSurface;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                        if (type.Name.Contains("TN", StringComparison.CurrentCultureIgnoreCase))
                        {
                            sectionSource_TN_Id = sectionsource.SourceId;
                        }
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                    }
                }

                //lấy điểm trên section TN
                foreach (ObjectId sectionViewId_1 in sectionViews)
                {
                    SectionView? sectionView1 = tr.GetObject(sectionViewId_1, OpenMode.ForWrite) as SectionView;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    ObjectId sampleLine1Id = sectionView1.SampleLineId;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                    SampleLine? sampleLine1 = tr.GetObject(sampleLine1Id, OpenMode.ForWrite) as SampleLine;

#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    ObjectId sectionTnId = sampleLine1.GetSectionId(sectionSource_TN_Id);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                    Section? section = tr.GetObject(sectionTnId, OpenMode.ForWrite) as Section;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    SectionPointCollection sectionPoints = section.SectionPoints;
#pragma warning restore CS8602 // Dereference of a possibly null reference.

                    Double easthing = new();
                    Double northing = new();
                    for (int i = 0; i < sectionPoints.Count; i++)
                    {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                        alignment.PointLocation(sampleLine1.Station, sectionPoints[i].Location.X, ref easthing, ref northing);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                        Point3d point3D = new(easthing, northing, sectionPoints[i].Location.Y);
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                        civSurface2.AddVertex(point3D);
#pragma warning restore CS8602 // Dereference of a possibly null reference.

                    }

                    // thêm điểm mia
                    for (int i = 1; i < sectionPoints.Count; i++)
                    {
                        double x1 = sectionPoints[i - 1].Location.X;
                        double x2 = sectionPoints[i].Location.X;
                        double c = Math.Abs(x2 - x1);

                        int j = 0;
                        while (Math.Abs(x2 - x1) > khoangCachDiemMia)
                        {
                            x1 = x1 + khoangCachDiemMia - j * 0.1;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                            alignment.PointLocation(sampleLine1.Station, x1, ref easthing, ref northing);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                            Point3d point3D_1 = new(easthing, northing, civSurface.FindElevationAtXY(easthing, northing));
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                            civSurface2.AddVertex(point3D_1);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                            j++;
                        }

                    }


                }




                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }

        [CommandMethod("CTS_An_Duong_Dia_Chat")]
        public static void CTSVAnDuongDiaChat()
        {
            // start transantion CTSV_DanhCap
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();

                //start here
                ObjectIdCollection sectionViewIdColl = UserInput.GSelectionSetWithType("Chọn các section cần ẩn đi: \n", "AECC_SECTION");

                //
                Document acDoc = Application.DocumentManager.MdiActiveDocument;
                PromptIntegerOptions pIntOpts = new("")
                {
                    Message = "\nNhập tên lớp địa chất hoặc chọn ",

                    // Restrict input to positive and non-negative values
                    AllowZero = false,
                    AllowNegative = false
                };

                // Define the valid keywords and allow Enter
                pIntOpts.Keywords.Add("TN1");
                pIntOpts.Keywords.Add("TN2");
                pIntOpts.Keywords.Add("TN3");
                pIntOpts.Keywords.Add("TN4");
                pIntOpts.Keywords.Add("TN5");
                pIntOpts.Keywords.Add("TN6");
                pIntOpts.Keywords.Default = "TN1";
                pIntOpts.AllowNone = true;

                // Get the value entered by the user
                PromptIntegerResult pIntRes = acDoc.Editor.GetInteger(pIntOpts);
                /*
                if (pIntRes.Status == PromptStatus.Keyword)
                {
                    Application.ShowAlertDialog("Entered keyword: " +
                                                pIntRes.StringResult);
                }
                else
                {
                    Application.ShowAlertDialog("Entered value: " +
                                                pIntRes.Value.ToString());
                }
                */
                foreach (ObjectId sectionId in sectionViewIdColl)
                {
                    Section? section = tr.GetObject(sectionId, OpenMode.ForWrite) as Section;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    if (section.StyleName != pIntRes.StringResult)
                    {
                        if (!section.StyleName.Contains(" -defpoints"))
                        {
                            section.StyleName += " -defpoints";
                        }

                    }
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                    //section.StyleName = pIntRes.StringResult;
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


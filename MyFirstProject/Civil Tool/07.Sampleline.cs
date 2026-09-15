// (C) Copyright 2015 by  
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

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
using System.Security.Cryptography.X509Certificates;
using System.Xml.Schema;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using MyFirstProject.Extensions;
//using Autodesk.Aec.DatabaseServices;
// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3DCsharp.Sampleline))]

namespace Civil3DCsharp
{
    public class Sampleline
    {

        [CommandMethod("CTS_DoiTenCoc")]
        public static void CTSDoiTenCoc()
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput uI = new();

                //start here
                // choose an alignment
                ObjectId alignmentId = UserInput.GAlignmentId("\n Chọn tim đường " + "để đổi tên cọc: \n");

                //get alignment for read
                Alignment? alignment = tr.GetObject(alignmentId, OpenMode.ForRead) as Alignment;
                //get first sampleline group
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                ObjectId samplelineGroup = alignment.GetSampleLineGroupIds()[0];
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                SampleLineGroup? sampleLineGroup = tr.GetObject(samplelineGroup, OpenMode.ForRead) as SampleLineGroup;

                //reset name sampleline
                int i = 0;
                int j = 1000;
                int value;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                foreach (ObjectId sampleLineId in sampleLineGroup.GetSampleLineIds())
                {
                    SampleLine? sampleline = tr.GetObject(sampleLineId, OpenMode.ForWrite) as SampleLine;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    if (int.TryParse(sampleline.Name, out value))
                    {
                        sampleline.Name = Convert.ToString(j);
                        j++;
                    }
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                    i++;
                }
#pragma warning restore CS8602 // Dereference of a possibly null reference.

                //rename sampleline

                j = 1;
                foreach (ObjectId sampleLineId in sampleLineGroup.GetSampleLineIds())
                {
                    SampleLine? sampleline = tr.GetObject(sampleLineId, OpenMode.ForWrite) as SampleLine;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    if (int.TryParse(sampleline.Name, out value))
                    {
                        sampleline.Name = Convert.ToString(j);
                        j++;
                    }
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                    i++;
                }
                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }

        [CommandMethod("CTS_DoiTenCoc3")]
        public static void CTSDoiTenCoc3()
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput uI = new();

                //start here
                // choose an alignment
                ObjectId alignmentId = UserInput.GAlignmentId("\n Chọn tim đường " + "để đổi tên cọc: \n");

                //get alignment for read
                Alignment? alignment = tr.GetObject(alignmentId, OpenMode.ForRead) as Alignment;
                //get first sampleline group
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                ObjectId samplelineGroup = alignment.GetSampleLineGroupIds()[0];
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                SampleLineGroup? sampleLineGroup = tr.GetObject(samplelineGroup, OpenMode.ForRead) as SampleLineGroup;

                //reset name sampleline
                int i = 0;
                int j = 1000;
                int value;
                String lyTrinh;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                foreach (ObjectId sampleLineId in sampleLineGroup.GetSampleLineIds())
                {
                    SampleLine? sampleline = tr.GetObject(sampleLineId, OpenMode.ForWrite) as SampleLine;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    if (int.TryParse(sampleline.Name, out value))
                    {
                        if ((sampleline.Station % 1000) < 100)
                        {
                            lyTrinh = "0" + (sampleline.Station % 1000).ToString();
                        }
                        else lyTrinh = (sampleline.Station % 1000).ToString();
                        sampleline.Name = "Km " + Math.Floor(sampleline.Station / 1000) + "+" + lyTrinh;
                        j++;
                    }
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                    i++;
                }
#pragma warning restore CS8602 // Dereference of a possibly null reference.

                //rename sampleline

                j = 1;
                foreach (ObjectId sampleLineId in sampleLineGroup.GetSampleLineIds())
                {
                    SampleLine? sampleline = tr.GetObject(sampleLineId, OpenMode.ForWrite) as SampleLine;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    if (int.TryParse(sampleline.Name, out value))
                    {
                        sampleline.Name = Convert.ToString(j);
                        j++;
                    }
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                    i++;
                }
                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }


        [CommandMethod("CTS_DoiTenCoc2")]
        public static void CTSDoiTenCoc2()
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput uI = new();

                //start here
                // choose an alignment
                ObjectId alignmentId = UserInput.GAlignmentId("\n Chọn tim đường " + "để đổi tên cọc: \n");

                //get alignment for read
                Alignment? alignment = tr.GetObject(alignmentId, OpenMode.ForRead) as Alignment;
                //get first sampleline group
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                ObjectId samplelineGroup = alignment.GetSampleLineGroupIds()[0];
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                SampleLineGroup? sampleLineGroup = tr.GetObject(samplelineGroup, OpenMode.ForRead) as SampleLineGroup;

                //rename sampleline
                ObjectId sampleLineDauId = UserInput.GSampleLineId("Chọn sampleLine điểm đầu đoạn tuyến cần đổi tên:");
                SampleLine? sampleLineDau = tr.GetObject(sampleLineDauId, OpenMode.ForWrite) as SampleLine;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                int sampleLineNunberDau = sampleLineDau.Number;
#pragma warning restore CS8602 // Dereference of a possibly null reference.

                ObjectId sampleLineCuoiId = UserInput.GSampleLineId("Chọn sampleLine điểm đầu đoạn tuyến cần đổi tên:");
                SampleLine? sampleLineCuoi = tr.GetObject(sampleLineCuoiId, OpenMode.ForWrite) as SampleLine;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                int sampleLineNunberCuoi = sampleLineCuoi.Number;
#pragma warning restore CS8602 // Dereference of a possibly null reference.

#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                String tienTo = UserInput.GString("Nhập tiền tố cho đoạn cọc muốn đổi tên:");
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
                int soTT = UserInput.GInt("Nhập số thứ tự của cọc đầu của tên đoạn muốn đổi tên cọc:");
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                ObjectIdCollection samplePlineIds = sampleLineGroup.GetSampleLineIds();
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                int j = soTT;

                for (int k = sampleLineNunberDau; k < sampleLineNunberCuoi; k++)
                {
                    SampleLine? sampleline = tr.GetObject(samplePlineIds[k], OpenMode.ForWrite) as SampleLine;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    if (int.TryParse(sampleline.Name, out int value))
                    {
                        sampleline.Name = tienTo + Convert.ToString(j);
                        j++;
                    }
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                }



                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }

        [CommandMethod("CTS_TaoBang_ToaDoCoc")]
        public static void CTSTaoBangToaDoCoc()
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput uI = new();
                UtilitiesCAD uti = new();

                //start here

                // get an alignment
                ObjectId alignmentId = UserInput.GAlignmentId("\n Chọn tim đường " + "for export coordinate table");
                Alignment? alignment = tr.GetObject(alignmentId, OpenMode.ForRead) as Alignment;
                //get the first samplelineGroup
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                ObjectId sampleLineGroup = alignment.GetSampleLineGroupIds()[0];
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                SampleLineGroup? samplelineGroup = tr.GetObject(sampleLineGroup, OpenMode.ForRead) as SampleLineGroup;

                //check number of sampleline in samplelinegroup
                int numberSampleline = 1;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                foreach (ObjectId sampleLineId in samplelineGroup.GetSampleLineIds())
                {
                    numberSampleline++;
                }
#pragma warning restore CS8602 // Dereference of a possibly null reference.

                // get coordinate and name of point
                string[] samplelineName = new string[numberSampleline];
                string[] eastings = new string[numberSampleline];
                string[] northings = new string[numberSampleline];
                int orderSampleline = 1;
                foreach (ObjectId sampleLineId in samplelineGroup.GetSampleLineIds())
                {
                    SampleLine? sampleline = tr.GetObject(sampleLineId, OpenMode.ForWrite) as SampleLine;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    Double station = sampleline.Station;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                    double easting = 0;
                    double northing = 0;
                    alignment.PointLocation(station, 0, ref easting, ref northing);
                    samplelineName[orderSampleline] = sampleline.Name.ToUpper();
                    eastings[orderSampleline] = Convert.ToString(Math.Round(easting, 3));
                    northings[orderSampleline] = Convert.ToString(Math.Round(northing, 3));
                    orderSampleline++;
                }

                //create a coordinate table
                UtilitiesCAD.CreateTableCoordinate(numberSampleline, 3, alignment.Name, samplelineName, eastings, northings);

                // draw polyline for check coordinnate
                UtilitiesCAD.CreateOpenPolyline(numberSampleline, eastings, northings);


                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }

        [CommandMethod("CTS_TaoBang_ToaDoCoc2")]
        public static void CTSTaoBangToaDoCoc2()
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput uI = new();
                UtilitiesCAD uti = new();

                //start here

                // get an alignment
                ObjectId alignmentId = UserInput.GAlignmentId("\n Chọn tim đường " + "for export coordinate table");
                Alignment? alignment = tr.GetObject(alignmentId, OpenMode.ForRead) as Alignment;
                //get the first samplelineGroup
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                ObjectId sampleLineGroup = alignment.GetSampleLineGroupIds()[0];
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                SampleLineGroup? samplelineGroup = tr.GetObject(sampleLineGroup, OpenMode.ForRead) as SampleLineGroup;

                //check number of sampleline in samplelinegroup
                int numberSampleline = 1;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                foreach (ObjectId sampleLineId in samplelineGroup.GetSampleLineIds())
                {
                    numberSampleline++;
                }
#pragma warning restore CS8602 // Dereference of a possibly null reference.

                // get coordinate and name of point
                string[] samplelineName = new string[numberSampleline];
                string[] eastings = new string[numberSampleline];
                string[] northings = new string[numberSampleline];
                string[] stations = new string[numberSampleline];
                int orderSampleline = 1;
                foreach (ObjectId sampleLineId in samplelineGroup.GetSampleLineIds())
                {
                    SampleLine? sampleline = tr.GetObject(sampleLineId, OpenMode.ForWrite) as SampleLine;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    Double station = sampleline.Station;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                    double easting = 0;
                    double northing = 0;
                    alignment.PointLocation(station, 0, ref easting, ref northing);
                    samplelineName[orderSampleline] = sampleline.Name.ToUpper();
                    eastings[orderSampleline] = Convert.ToString(Math.Round(easting, 3));
                    northings[orderSampleline] = Convert.ToString(Math.Round(northing, 3));
                    stations[orderSampleline] = Convert.ToString(Math.Round(station, 3));
                    orderSampleline++;
                }

                //create a coordinate table
                UtilitiesCAD.CreateTableCoordinate2(numberSampleline, 4, alignment.Name, samplelineName, eastings, northings, stations);

                // draw polyline for check coordinnate
                UtilitiesCAD.CreateOpenPolyline(numberSampleline, eastings, northings);


                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }

        [CommandMethod("CTS_TaoBang_ToaDoCoc3")]
        public static void CTSTaoBangToaDoCoc3()
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput uI = new();
                UtilitiesCAD uti = new();

                //start here

                // get an alignment
                ObjectId surfaceId = UserInput.GObjId("Chọn mặt phẳng cần lấy cao độ:");
                CivSurface? civSurface = tr.GetObject(surfaceId, OpenMode.ForRead) as CivSurface;
                ObjectId alignmentId = UserInput.GAlignmentId("\n Chọn tim đường " + "for export coordinate table");
                Alignment? alignment = tr.GetObject(alignmentId, OpenMode.ForRead) as Alignment;
                //get the first samplelineGroup
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                ObjectId sampleLineGroup = alignment.GetSampleLineGroupIds()[0];
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                SampleLineGroup? samplelineGroup = tr.GetObject(sampleLineGroup, OpenMode.ForRead) as SampleLineGroup;

                //check number of sampleline in samplelinegroup
                int numberSampleline = 1;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                foreach (ObjectId sampleLineId in samplelineGroup.GetSampleLineIds())
                {
                    numberSampleline++;
                }
#pragma warning restore CS8602 // Dereference of a possibly null reference.

                // get coordinate and name of point
                string[] samplelineName = new string[numberSampleline];
                string[] eastings = new string[numberSampleline];
                string[] northings = new string[numberSampleline];
                string[] elevation = new string[numberSampleline];
                int orderSampleline = 1;
                foreach (ObjectId sampleLineId in samplelineGroup.GetSampleLineIds())
                {
                    SampleLine? sampleline = tr.GetObject(sampleLineId, OpenMode.ForWrite) as SampleLine;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    Double station = sampleline.Station;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                    double easting = 0;
                    double northing = 0;
                    double elevate = 0;
                    alignment.PointLocation(station, 0, ref easting, ref northing);
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    elevate = civSurface.FindElevationAtXY(easting, northing);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                    samplelineName[orderSampleline] = sampleline.Name.ToUpper();
                    eastings[orderSampleline] = Convert.ToString(Math.Round(easting, 3));
                    northings[orderSampleline] = Convert.ToString(Math.Round(northing, 3));
                    elevation[orderSampleline] = Convert.ToString(Math.Round(elevate, 3));
                    orderSampleline++;
                }

                //create a coordinate table
                UtilitiesCAD.CreateTableCoordinate2(numberSampleline, 4, alignment.Name, samplelineName, eastings, northings, elevation);

                // draw polyline for check coordinnate
                UtilitiesCAD.CreateOpenPolyline(numberSampleline, eastings, northings);


                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }

        [CommandMethod("AT_UPdate2Table")]
        public static void ATUPdate2Table()
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here
                ObjectId tableId1 = UserInput.GTable("Chọn 1 bảng " + "for source table:");
                ATable? table1 = tr.GetObject(tableId1, OpenMode.ForWrite) as ATable;
                ObjectId tableId2 = UserInput.GTable("Chọn 1 bảng " + "for destinate table:");
                ATable? table2 = tr.GetObject(tableId2, OpenMode.ForWrite) as ATable;

                //Update table
#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                if (table2.Rows.Count == table1.Rows.Count)
                {
                    for (int i = 2; i < table2.Rows.Count; i++)
                    {
                        table2.Cells[i, 0].TextHeight = 2;
                        table2.Cells[i, 0].TextString = table1.Cells[i, 0].TextString;
                        table2.Cells[i, 0].Alignment = CellAlignment.MiddleCenter;

                        table2.Cells[i, 1].TextHeight = 2;
                        table2.Cells[i, 1].TextString = table1.Cells[i, 1].TextString;
                        table2.Cells[i, 1].Alignment = CellAlignment.MiddleCenter;

                        table2.Cells[i, 2].TextHeight = 2;
                        table2.Cells[i, 2].TextString = table1.Cells[i, 2].TextString;
                        table2.Cells[i, 2].Alignment = CellAlignment.MiddleCenter;
                    }
                    ;
                }
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                ;

                if (table2.Rows.Count < table1.Rows.Count)
                {
                    table2.InsertRows(3, 5, table1.Rows.Count - table2.Rows.Count);
                    for (int i = 2; i < table2.Rows.Count; i++)
                    {
                        table2.Cells[i, 0].TextHeight = 2;
                        table2.Cells[i, 0].TextString = table1.Cells[i, 0].TextString;
                        table2.Cells[i, 0].Alignment = CellAlignment.MiddleCenter;

                        table2.Cells[i, 1].TextHeight = 2;
                        table2.Cells[i, 1].TextString = table1.Cells[i, 1].TextString;
                        table2.Cells[i, 1].Alignment = CellAlignment.MiddleCenter;

                        table2.Cells[i, 2].TextHeight = 2;
                        table2.Cells[i, 2].TextString = table1.Cells[i, 2].TextString;
                        table2.Cells[i, 2].Alignment = CellAlignment.MiddleCenter;

                    }
                    ;
                }
                ;
                if (table2.Rows.Count > table1.Rows.Count)
                {
                    table2.DeleteRows(3, table2.Rows.Count - table1.Rows.Count);
                    for (int i = 2; i < table2.Rows.Count; i++)
                    {
                        table2.Cells[i, 0].TextHeight = 2;
                        table2.Cells[i, 0].TextString = table1.Cells[i, 0].TextString;
                        table2.Cells[i, 0].Alignment = CellAlignment.MiddleCenter;

                        table2.Cells[i, 1].TextHeight = 2;
                        table2.Cells[i, 1].TextString = table1.Cells[i, 1].TextString;
                        table2.Cells[i, 1].Alignment = CellAlignment.MiddleCenter;

                        table2.Cells[i, 2].TextHeight = 2;
                        table2.Cells[i, 2].TextString = table1.Cells[i, 2].TextString;
                        table2.Cells[i, 2].Alignment = CellAlignment.MiddleCenter;

                    }
                    ;
                }
                ;


                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }

        [CommandMethod("CTS_ChenCoc_TrenTracDoc")]
        public static void CTSChenCocTrenTracDoc()
        {
            // start transantion
            _ = new            // start transantion
            UserInput();
            _ = new UtilitiesCAD();
            _ = new UtilitiesC3D();

            ObjectId profileViewId = UserInput.GProfileViewId("\n Chọn 1 bảng trắc dọc " + " để chèn cọc: \n");
            UtilitiesC3D.SetDefaultPointSetting("CDTN", "CDTN");

            String answer = "y"; ;
            while (answer == "y")
            {
                using (Transaction tr = A.Db.TransactionManager.StartTransaction())
                {
                    try
                    {

                        //start here
                        //get profileview

                        ProfileView? profileView = tr.GetObject(profileViewId, OpenMode.ForRead) as ProfileView;

#pragma warning disable CS8602 // Dereference of a possibly null reference.
                        ObjectId alignmentId = profileView.AlignmentId;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                        Alignment? alignment = tr.GetObject(alignmentId, OpenMode.ForRead) as Alignment;

#pragma warning disable CS8602 // Dereference of a possibly null reference.
                        ObjectId sampleLineGroupId = alignment.GetSampleLineGroupIds()[0];
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                        SampleLineGroup? sampleLineGroup = tr.GetObject(sampleLineGroupId, OpenMode.ForWrite) as SampleLineGroup;

                        // get point for add sampleline
                        Point3d point3D = UserInput.GPoint("\n Chọn vị trí điểm" + "để thêm cọc ");
                        double stations = 0, elevations = 0;
                        profileView.FindStationAndElevationAtXY(point3D.X, point3D.Y, ref stations, ref elevations);

                        //get sampleline name
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                        String samplelineName = UserInput.GString("Input sampleline name:");
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.

                        // set station variable
#pragma warning disable CS8604 // Possible null reference argument.
                        ObjectId samplelineId = UtilitiesC3D.CreateSampleline(samplelineName, sampleLineGroupId, alignment, stations);
#pragma warning restore CS8604 // Possible null reference argument.
                        SampleLine? sampleline = tr.GetObject(samplelineId, OpenMode.ForWrite) as SampleLine;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                        sampleline.StyleName = "Road Sample Line";
#pragma warning restore CS8602 // Dereference of a possibly null reference.

                        tr.Commit();
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception e)
                    {
                        A.Ed.WriteMessage(e.Message);
                    }
                }
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                String answer1 = UserInput.GString("Do you want to add more sampleline? (y/n)");
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                answer = answer1;
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
            }

        }

        [CommandMethod("CTS_CHENCOC_TRENTRACNGANG")]
        public static void CTSCHENCOCTRENTRACNGANG()
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here
                ObjectId sectionViewId = UserInput.GSectionView("Chọn 1 bảng cắt ngang " + ": \n");
                SectionView? sectionView = tr.GetObject(sectionViewId, OpenMode.ForRead) as SectionView;
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                string decription = UserInput.GString("Nhập mã mô tả cho điểm sẽ chèn: \n ");
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
                string ans = "Enter";
                while (ans == "Enter")
                {
                    Point3d point3D = UserInput.GPoint("\n Chọn vị trí điểm" + "Chọn điểm cần chèn trên trắc ngang: \n ");

                    //find offset, elevation
                    double offset = 1;
                    double elevation = 2;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    sectionView.FindOffsetAndElevationAtXY(point3D.X, point3D.Y, ref offset, ref elevation);
#pragma warning restore CS8602 // Dereference of a possibly null reference.

                    //find alignment
                    ObjectId samplelineId = sectionView.SampleLineId;
                    SampleLine? sampleLine = tr.GetObject(samplelineId, OpenMode.ForWrite) as SampleLine;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    double station = sampleLine.Station;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                    ObjectId alignmentId = sampleLine.GetParentAlignmentId();
                    Alignment? alignment = tr.GetObject(alignmentId, OpenMode.ForWrite) as Alignment;

                    // create point
                    double x = 1;
                    double y = 1;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    alignment.PointLocation(station, offset, ref x, ref y);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                    Point3d point3D1 = new(x, y, elevation);
#pragma warning disable CS8604 // Possible null reference argument.
                    UtilitiesC3D.CreateCogoPointFromPoint3D(point3D1, decription);
#pragma warning restore CS8604 // Possible null reference argument.
                    ans = UserInput.GStopWithESC();
                }


                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }

        [CommandMethod("CTS_DoiTenCoc_fromCogoPoint")]
        public static void CTSDoiTenCocFromCogoPoint()
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here
                ObjectId cogoPointId = UserInput.GCogoPointId("\n Chọn cogo point " + "để lấy tên cho sampleline:\n");
                CogoPoint? cogoPoint = tr.GetObject(cogoPointId, OpenMode.ForWrite) as CogoPoint;
                ObjectIdCollection sampleLineIds = UserInput.GSelectionSet("Chọn sampleLine cần đổi tên: \n");
                foreach (ObjectId objectId in sampleLineIds)
                {
                    SampleLine? sampleLine = tr.GetObject(objectId, OpenMode.ForWrite) as SampleLine;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    sampleLine.Name = cogoPoint.PointName;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                }
                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }

        [CommandMethod("CTS_PhatSinhCoc")]
        public static void CTSPhatSinhCoc()
        {
            // Show form first
            var form = new MyFirstProject.Civil_Tool.PhatSinhCocForm();
            var result = Autodesk.AutoCAD.ApplicationServices.Application.ShowModalDialog(form);

            if (result != System.Windows.Forms.DialogResult.OK || !form.FormAccepted)
            {
                A.Ed.WriteMessage("\n Đã hủy lệnh.");
                return;
            }

            // Get values from form
            bool phatSinhCocH = form.PhatSinhCocH;
            int Km = form.KmBatDau;
            int khoangCach = form.KhoangCachChiTiet;
            string labelStyleName = form.SampleLineLabelStyleName;

            // start transaction
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                Csharp CS = new();

                //start here CTS_PhatSinhCoc_DiemDacBiet
                ObjectId alignmentId = UserInput.GAlignmentId("\n Chọn tim đường " + "để phát sinh cọc: \n");
                Alignment? alignment = tr.GetObject(alignmentId, OpenMode.ForWrite) as Alignment;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                Station[] station = alignment.GetStationSet(StationTypes.GeometryPoint);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                String[] sampleLineGeo = new string[station.Count()];
                int y = 0;
                for (int i = 0; i < station.Count(); i++)
                {
                    if (station[i].GeometryStationType == AlignmentGeometryPointStationType.BegOfAlign)
                    {
                        sampleLineGeo[i] = "zDT";
                        y++;
                    }
                    if (station[i].GeometryStationType == AlignmentGeometryPointStationType.TanTan)
                    {
                        sampleLineGeo[i] = "zD" + y.ToString();
                        y++;
                    }
                    if (station[i].GeometryStationType == AlignmentGeometryPointStationType.TanCurve)
                    {
                        sampleLineGeo[i] = "zTD" + y.ToString();
                        sampleLineGeo[i + 1] = "zP" + y.ToString();
                        sampleLineGeo[i + 2] = "zTC" + y.ToString();
                        y++;

                    }
                    if (station[i].GeometryStationType == AlignmentGeometryPointStationType.TanSpiral)
                    {
                        sampleLineGeo[i] = "zND" + y.ToString();
                        sampleLineGeo[i + 1] = "zTD" + y.ToString();
                        sampleLineGeo[i + 2] = "zP" + y.ToString();
                        sampleLineGeo[i + 3] = "zTC" + y.ToString();
                        sampleLineGeo[i + 4] = "zNC" + y.ToString();
                        y++;
                    }
                    if (station[i].GeometryStationType == AlignmentGeometryPointStationType.EndOfAlign)
                    {

                        sampleLineGeo[i] = "zCT";
                    }
                }

                //phát sinh cọc đặc biệt
                ObjectId sampleLineGroupId = new();
                if (alignment.GetSampleLineGroupIds().Count == 0)
                {
                    sampleLineGroupId = SampleLineGroup.Create(alignment.Name, alignmentId);
                }
                else
                {
                    sampleLineGroupId = alignment.GetSampleLineGroupIds()[0];
                }

                for (int i = 0; i < station.Count(); i++)
                {
                    ObjectId sampleLineId = UtilitiesC3D.CreateSampleline(sampleLineGeo[i].ToString(), sampleLineGroupId, alignment, station[i].RawStation);
                }

                //phát sinh cọc H (từ form)
                int LyTrinh = Km;
                int H_number = 1;
                int soLuongCocH = 0;
                for (int i = 0; i < alignment.Length / 100 - 1; i++)
                {
                    soLuongCocH++;
                }
                Double[] stationCocH = new double[soLuongCocH];
                if (phatSinhCocH)
                {
                    for (int i = 0; i < alignment.Length / 100 - 1; i++)
                    {
                        stationCocH[i] = 100 + i * 100;
                        if (stationCocH[i] % 1000 == 0)
                        {
                            ObjectId sampleLineId = UtilitiesC3D.CreateSampleline("Km" + ((i + 1) / 10 + Km).ToString(), sampleLineGroupId, alignment, 100 + i * 100);
                            LyTrinh = ((i + 1) / 10 + Km);
                            H_number = 1;
                        }
                        if (stationCocH[i] % 1000 != 0)
                        {
                            ObjectId sampleLineId = UtilitiesC3D.CreateSampleline("H" + H_number + " (Km" + LyTrinh + ")", sampleLineGroupId, alignment, 100 + i * 100);
                            H_number++;
                        }
                    }
                }
                A.Ed.WriteMessage("\n Số cọc H: " + soLuongCocH);

                //cộng 2 mảng đặc biệt và H
                Double[] rawStation = new double[station.Count() + soLuongCocH];
                for (int i = 0; i < station.Count(); i++)
                {
                    rawStation[i] = station[i].RawStation;
                }
                for (int i = 0; i < soLuongCocH; i++)
                {
                    rawStation[i + station.Count()] = stationCocH[i];
                }
                rawStation = Csharp.CSMangSapXepTangDan(rawStation);

                //cọc chi tiết (từ form)
                List<double> stationList = [];
                List<String> sampleLineNameS = [];
                for (int i = 0; i < rawStation.Count() - 1; i++)
                {
                    double stationDetail = rawStation[i];
                    for (int j = 1; j < Math.Ceiling((rawStation[i + 1] - rawStation[i]) / khoangCach); j++)
                    {
                        stationDetail += khoangCach;
                        stationList.Add(stationDetail);
                    }
                }

                int k = 1;
                foreach (var item in stationList)
                {
                    sampleLineNameS.Add("z" + Convert.ToString(k));
                    k++;
                }

                for (int i = 0; i < stationList.Count; i++)
                {
                    ObjectId sampleLineId = UtilitiesC3D.CreateSampleline(sampleLineNameS[i], sampleLineGroupId, alignment, stationList[i]);
                }


                //rename sampleline

                char[] charsToTrim = ['z', ' ', '\''];
                SampleLineGroup? sampleLineGroup = tr.GetObject(sampleLineGroupId, OpenMode.ForWrite) as SampleLineGroup;
                
                // Lấy danh sách tên sampleline đã tồn tại trong group
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                HashSet<string> existingNames = new();
                foreach (ObjectId slId in sampleLineGroup.GetSampleLineIds())
                {
                    SampleLine? sl = tr.GetObject(slId, OpenMode.ForRead) as SampleLine;
                    if (sl != null && !sl.Name.StartsWith("z"))
                    {
                        existingNames.Add(sl.Name);
                    }
                }

                foreach (ObjectId sampleLineId in sampleLineGroup.GetSampleLineIds())
                {
                    SampleLine? sampleline = tr.GetObject(sampleLineId, OpenMode.ForWrite) as SampleLine;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    // Chỉ rename nếu tên bắt đầu bằng 'z' (tên tạm)
                    if (sampleline.Name.StartsWith("z"))
                    {
                        string newName = sampleline.Name.Trim(charsToTrim);
                        
                        // Kiểm tra nếu tên đã tồn tại, thêm suffix
                        if (existingNames.Contains(newName))
                        {
                            int suffix = 1;
                            while (existingNames.Contains($"{newName}_{suffix}"))
                            {
                                suffix++;
                            }
                            newName = $"{newName}_{suffix}";
                        }
                        
                        try
                        {
                            sampleline.Name = newName;
                            existingNames.Add(newName);
                        }
                        catch (System.ArgumentException)
                        {
                            // Tên đã tồn tại, bỏ qua
                            A.Ed.WriteMessage($"\n Bỏ qua sampleline có tên trùng: {newName}");
                        }
                    }
#pragma warning restore CS8602 // Dereference of a possibly null reference.

                }
#pragma warning restore CS8602 // Dereference of a possibly null reference.

                // tạo label (từ form)
                ObjectId labelId = GetLabelStyleId(labelStyleName);
                if (labelId != ObjectId.Null)
                {
                    ObjectId labelSampleLineGroup = SampleLineLabelGroup.Create(sampleLineGroupId, labelId);
                }

                A.Ed.WriteMessage($"\n Đã phát sinh {station.Count()} cọc đặc biệt, {soLuongCocH} cọc H/Km, {stationList.Count} cọc chi tiết.");
                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }

        /// <summary>
        /// Helper method to get SampleLine Label Style ID
        /// </summary>
        private static ObjectId GetLabelStyleId(string styleName)
        {
            try
            {
                return A.Cdoc.Styles.LabelStyles.SampleLineLabelStyles.LabelStyles[styleName];
            }
            catch
            {
                try
                {
                    return A.Cdoc.Styles.LabelStyles.SampleLineLabelStyles.LabelStyles["Tên cọc"];
                }
                catch
                {
                    return ObjectId.Null;
                }
            }
        }

        [CommandMethod("CTS_PhatSinhCoc_theoKhoangDelta")]
        public static void CTSPhatSinhCocTheoKhoangDelta()
        {
            // start transantion
            _ = new            // start transantion
            UserInput();
            _ = new UtilitiesCAD();
            _ = new UtilitiesC3D();
            //start here
            ObjectId alignmentId = UserInput.GAlignmentId("\n Chọn tim đường " + "để phát sinh cọc: \n");
            Double khoangCach = UserInput.GDouble("Nhập khoảng cách cọc sẽ phát sinh:\n");
            double station = new();
            double offset = new();
            int increment = 1000;
            String sampleLineName = Convert.ToString(increment);
            int stationIncrement = 1;

            String i = "Enter";
            while (i == "Enter")
            {
                using (Transaction tr = A.Db.TransactionManager.StartTransaction())
                {
                    try
                    {
                        Alignment? alignment = tr.GetObject(alignmentId, OpenMode.ForWrite) as Alignment;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                        ObjectId sampleLineGroupId = alignment.GetSampleLineGroupIds()[0];
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                        Point3d point3D = UserInput.GPoint("\n Chọn vị trí điểm" + "để phát sinh cọc: \n");

                        // phát sinh cọc
                        alignment.StationOffset(point3D.X, point3D.Y, ref station, ref offset);

                        station += khoangCach * stationIncrement;
                        ObjectId sampleLineId = UtilitiesC3D.CreateSampleline(sampleLineName, sampleLineGroupId, alignment, station);



                        tr.Commit();
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception e)
                    {
                        A.Ed.WriteMessage(e.Message);
                    }
                }
                i = UserInput.GStopWithESC();
                stationIncrement++;
                increment++;
                sampleLineName = Convert.ToString(increment);
            }
        }

        [CommandMethod("CTS_PhatSinhCoc_TuCogoPoint")]
        public static void CTSPhatSinhCocTuCogoPoint()
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here
                //get point group
                ObjectId cogoPointId = UserInput.GCogoPointId("\n Chọn cogo point " + " thuộc nhóm điểm cần phát sinh cọc: \n");
                CogoPoint? cogoPoint = tr.GetObject(cogoPointId, OpenMode.ForWrite) as CogoPoint;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                ObjectId pointGroupId = cogoPoint.PrimaryPointGroupId;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                ObjectIdCollection pointIds = UtilitiesC3D.GPointIdsFromPointGroup(pointGroupId);

                // get alignment
                ObjectId alignmentId = UserInput.GAlignmentId("\n Chọn tim đường cần phát sinh cọc:");
                Alignment? alignment = tr.GetObject(alignmentId, OpenMode.ForWrite) as Alignment;
                double station = new();
                double offset = new();
                foreach (ObjectId pointId in pointIds)
                {
                    CogoPoint? cogoPoint1 = tr.GetObject(pointId, OpenMode.ForRead) as CogoPoint;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    alignment.StationOffset(cogoPoint1.Easting, cogoPoint1.Northing, ref station, ref offset);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning restore CS8602 // Dereference of a possibly null reference.

                    ObjectId sampleLineId = UtilitiesC3D.CreateSampleline(cogoPoint1.PointName, alignment.GetSampleLineGroupIds()[0], alignment, station);
                }


                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }

        [CommandMethod("CTS_DoiTenCoc_TheoThuTu")]
        public static void CTSDoiTenCocTheoThuTu()
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here

                ObjectIdCollection sampleLineIds = UserInput.GSelectionSet("\n Chọn sampleLine cần đổi tên: \n");
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                String tienToCoc = UserInput.GString("\n Nhập tiền tố cho tên cọc:");
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
                int thuTuCoc = UserInput.GInt("\n Nhập số thứ tự bắt đầu (1):"); ;
                foreach (ObjectId objectId in sampleLineIds)
                {
                    SampleLine? sampleLine = tr.GetObject(objectId, OpenMode.ForWrite) as SampleLine;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    sampleLine.Name = tienToCoc + thuTuCoc;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                    thuTuCoc++;
                }
                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }

        [CommandMethod("CTS_DichCoc_TinhTien")]
        public static void CTSDichCocTinhTien()
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here

                ObjectIdCollection sampleLineIds = UserInput.GSelectionSet("\n Chọn sampleLine cần dịch cọc: \n");

                // Cho phép nhập khoảng dịch, chọn 2 điểm, hoặc chọn 2 sampleline
                double delta = 0;
                PromptDoubleOptions pdo = new("\n Nhập khoảng dịch cọc hoặc [Chọn 2 điểm/P] [Chọn 2 sampleLine/S]:");
                pdo.Keywords.Add("P");
                pdo.Keywords.Add("S");
                pdo.AllowNegative = true;
                pdo.AllowZero = true;

                PromptDoubleResult pdr = A.Ed.GetDouble(pdo);

                if (pdr.Status == PromptStatus.Keyword && pdr.StringResult == "P")
                {
                    // Cách 2: Lấy alignment từ sampleLine đầu tiên để tính station
                    if (sampleLineIds.Count == 0)
                    {
                        A.Ed.WriteMessage("\n Không có sampleLine nào được chọn.");
                        return;
                    }

                    SampleLine? firstSampleLine = tr.GetObject(sampleLineIds[0], OpenMode.ForRead) as SampleLine;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    ObjectId alignmentId = firstSampleLine.GetParentAlignmentId();
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                    Alignment? alignment = tr.GetObject(alignmentId, OpenMode.ForRead) as Alignment;

                    // Chọn 2 điểm và tính station trên tuyến
                    Point3d point1 = UserInput.GPoint("\n Chọn điểm thứ nhất trên tuyến:");
                    Point3d point2 = UserInput.GPoint("\n Chọn điểm thứ hai trên tuyến:");

                    double station1 = 0, offset1 = 0;
                    double station2 = 0, offset2 = 0;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    alignment.StationOffset(point1.X, point1.Y, ref station1, ref offset1);
                    alignment.StationOffset(point2.X, point2.Y, ref station2, ref offset2);
#pragma warning restore CS8602 // Dereference of a possibly null reference.

                    delta = station2 - station1;
                    A.Ed.WriteMessage($"\n Station điểm 1: {station1:F3}, Station điểm 2: {station2:F3}");
                    A.Ed.WriteMessage($"\n Khoảng dịch tính được: {delta:F3}");
                }
                else if (pdr.Status == PromptStatus.Keyword && pdr.StringResult == "S")
                {
                    // Cách 3: Chọn 2 sampleline và nhập khoảng cách yêu cầu
                    ObjectId sampleLineId1 = UserInput.GSampleLineId("\n Chọn sampleLine thứ nhất (cọc gốc):");
                    ObjectId sampleLineId2 = UserInput.GSampleLineId("\n Chọn sampleLine thứ hai (cọc cần kiểm tra):");

                    SampleLine? sl1 = tr.GetObject(sampleLineId1, OpenMode.ForRead) as SampleLine;
                    SampleLine? sl2 = tr.GetObject(sampleLineId2, OpenMode.ForRead) as SampleLine;

#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    double stationSL1 = sl1.Station;
                    double stationSL2 = sl2.Station;
#pragma warning restore CS8602 // Dereference of a possibly null reference.

                    double khoangCachHienTai = Math.Abs(stationSL2 - stationSL1);
                    A.Ed.WriteMessage($"\n Khoảng cách hiện tại giữa 2 sampleLine: {khoangCachHienTai:F3}");

                    double khoangCachYeuCau = UserInput.GDouble("\n Nhập khoảng cách yêu cầu giữa 2 sampleLine:");

                    // Tính khoảng dịch = khoảng cách yêu cầu - khoảng cách hiện tại
                    delta = khoangCachYeuCau - khoangCachHienTai;
                    // Xác định hướng dịch: nếu SL2 > SL1 thì dịch về phía cuối tuyến (dương)
                    // delta > 0: dịch ra xa (khoảng cách hiện tại < yêu cầu)
                    // delta < 0: dịch lại gần (khoảng cách hiện tại > yêu cầu)
                    if (stationSL2 < stationSL1)
                    {
                        delta = -delta; // Đảo hướng nếu SL2 nằm trước SL1
                    }
                    A.Ed.WriteMessage($"\n Khoảng dịch tính được: {delta:F3}");
                }
                else if (pdr.Status == PromptStatus.OK)
                {
                    delta = pdr.Value;
                }
                else
                {
                    A.Ed.WriteMessage("\n Đã hủy lệnh.");
                    return;
                }

                foreach (ObjectId objectId in sampleLineIds)
                {
                    SampleLine? sampleLine = tr.GetObject(objectId, OpenMode.ForWrite) as SampleLine;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    sampleLine.Station += delta;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                }


                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }

        [CommandMethod("CTS_Copy_NhomCoc")]
        public static void CTSCopyNhomCoc()
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here

                ObjectId alignmentId = UserInput.GAlignmentId("\n Chọn tim đường có nhóm cọc cần sao chép:");
                Alignment? alignment = tr.GetObject(alignmentId, OpenMode.ForWrite) as Alignment;
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                String tenSamplelineGroup = UserInput.GString("\n Nhập tên nhóm cọc cần tạo:");
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.

                //tạo nhóm cọc mới
                ObjectId sampleLineGroupId = SampleLineGroup.Create(tenSamplelineGroup, alignmentId);

                // lấy thông số từ cọc cũ
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                ObjectId sampleLineGroupId_0 = alignment.GetSampleLineGroupIds()[0];
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                SampleLineGroup? sampleLineGroup_0 = tr.GetObject(sampleLineGroupId_0, OpenMode.ForWrite) as SampleLineGroup;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                ObjectIdCollection sampleLineIds = sampleLineGroup_0.GetSampleLineIds();
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                List<double> stationList = [];
                List<String> sampleLineNameS = [];
                foreach (ObjectId sampleLineId in sampleLineIds)
                {
                    SampleLine? sampleLine = tr.GetObject(sampleLineId, OpenMode.ForWrite) as SampleLine;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    stationList.Add(sampleLine.Station);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                    sampleLineNameS.Add(sampleLine.Name);
                }

                //sampleLineGroup_0.Erase();
                //tạo cọc vào nhóm cọc mới
                for (int i = 0; i < stationList.Count(); i++)
                {
                    A.Ed.WriteMessage(sampleLineNameS[i].ToString() + "-" + stationList[i].ToString());
                    ObjectId sampleLineId = UtilitiesC3D.CreateSampleline(sampleLineNameS[i] + "C", sampleLineGroupId, alignment, stationList[i]);
                    SampleLine? sampleLine = tr.GetObject(sampleLineId, OpenMode.ForWrite) as SampleLine;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    sampleLine.Name = sampleLineNameS[i];
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                }




                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }

        [CommandMethod("CTS_DongBo_2_NhomCoc")]
        public static void CTSDongBo2NhomCoc()
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here

                // lấy thông số từ cọc cũ
                ObjectId sampleLineId_0 = UserInput.GSampleLineId("\n Chọn nhóm cọc NGUỒN để copy: ");
                SampleLine? sampleLine_0 = tr.GetObject(sampleLineId_0, OpenMode.ForWrite) as SampleLine;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                ObjectId sampleLineGroupId = sampleLine_0.GroupId;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                SampleLineGroup? sampleLineGroup = tr.GetObject(sampleLineGroupId, OpenMode.ForWrite) as SampleLineGroup;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                ObjectIdCollection sampleLineIds = sampleLineGroup.GetSampleLineIds();
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                int sampleLIneNumber = sampleLineIds.Count;
                List<double> stationList = [];
                List<String> sampleLineNameS = [];
                foreach (ObjectId sampleLineId in sampleLineIds)
                {
                    SampleLine? sampleLine = tr.GetObject(sampleLineId, OpenMode.ForWrite) as SampleLine;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    stationList.Add(sampleLine.Station);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                    //a.ok(sampleLine.Station.ToString());
                    sampleLineNameS.Add(sampleLine.Name);
                }

                // lấy thông số từ cọc mới
                ObjectId sampleLineId_1 = UserInput.GSampleLineId("\n Chọn nhóm cọc ĐÍCH để copy: ");
                //double lyTrinhBatDau = UI.G_Double(" NHập lý bắt đầu của tuyến: ");
                SampleLine? sampleLine_1 = tr.GetObject(sampleLineId_1, OpenMode.ForWrite) as SampleLine;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                ObjectId sampleLineGroupId_1 = sampleLine_1.GroupId;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                SampleLineGroup? sampleLineGroup_1 = tr.GetObject(sampleLineGroupId_1, OpenMode.ForWrite) as SampleLineGroup;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                ObjectIdCollection sampleLineIds_1 = sampleLineGroup_1.GetSampleLineIds();
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                int sampleLIneNumber_1 = sampleLineIds_1.Count;

                //đồng bộ thông tin
                if (sampleLIneNumber_1 <= sampleLIneNumber)
                {
                    for (int i = 0; i < sampleLIneNumber_1; i++)
                    {
                        SampleLine? sampleLine = tr.GetObject(sampleLineIds[i], OpenMode.ForWrite) as SampleLine;
                        SampleLine? sampleLine_c = tr.GetObject(sampleLineIds_1[i], OpenMode.ForWrite) as SampleLine;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                        sampleLine_c.Station = sampleLine.Station;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                        sampleLine_c.Name = sampleLine.Name + "c";
                    }
                }
                else A.Ok("Số cọc đích nhiều hơn cọc nguồn. Cần xóa bớt cọc đích");


                //rename sampleline
                char[] charsToTrim = ['z', ' ', '\'', 'c'];
                foreach (ObjectId sampleLineId in sampleLineGroup_1.GetSampleLineIds())
                {
                    SampleLine? sampleline = tr.GetObject(sampleLineId, OpenMode.ForWrite) as SampleLine;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    sampleline.Name = sampleline.Name.Trim(charsToTrim);
#pragma warning restore CS8602 // Dereference of a possibly null reference.

                }


                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }

        [CommandMethod("CTS_DongBo_2_NhomCoc_TheoDoan")]
        public static void CTSDongBo2NhomCocTheoDoan()
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here

                // lấy thông số từ cọc cũ
                ObjectId sampleLineId_0 = UserInput.GSampleLineId("\n Chọn cọc ĐẦU TIÊN của đoạn thuộc NGUỒN để copy: ");
                SampleLine? sampleLine_0 = tr.GetObject(sampleLineId_0, OpenMode.ForWrite) as SampleLine;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                ObjectId sampleLineGroupId = sampleLine_0.GroupId;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                SampleLineGroup? sampleLineGroup = tr.GetObject(sampleLineGroupId, OpenMode.ForWrite) as SampleLineGroup;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                ObjectIdCollection sampleLineIds = sampleLineGroup.GetSampleLineIds();
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                int sampleLIneNumber = sampleLineIds.Count;
                int sampleLIneNumberBegin = sampleLine_0.Number;

                List<double> stationList = [];
                List<String> sampleLineNameS = [];

                for (int i = sampleLIneNumberBegin; i < sampleLineIds.Count; i++)
                {
                    ObjectId sampleLineId = sampleLineIds[i];
                    SampleLine? sampleLine = tr.GetObject(sampleLineId, OpenMode.ForWrite) as SampleLine;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    stationList.Add(sampleLine.Station);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                    //a.ok(sampleLine.Station.ToString());
                    sampleLineNameS.Add(sampleLine.Name);
                }
                /*
                foreach (ObjectId sampleLineId in sampleLineIds)
                {
                    SampleLine sampleLine = tr.GetObject(sampleLineId, OpenMode.ForWrite) as SampleLine;
                    stationList.Add(sampleLine.Station);
                    //a.ok(sampleLine.Station.ToString());
                    sampleLineNameS.Add(sampleLine.Name);
                }
                */

                // lấy thông số từ cọc mới
                ObjectId sampleLineId_1 = UserInput.GSampleLineId("\n Chọn nhóm cọc ĐÍCH để copy: ");
                //double lyTrinhBatDau = UI.G_Double(" NHập lý bắt đầu của tuyến: ");
                SampleLine? sampleLine_1 = tr.GetObject(sampleLineId_1, OpenMode.ForWrite) as SampleLine;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                ObjectId sampleLineGroupId_1 = sampleLine_1.GroupId;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                SampleLineGroup? sampleLineGroup_1 = tr.GetObject(sampleLineGroupId_1, OpenMode.ForWrite) as SampleLineGroup;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                ObjectIdCollection sampleLineIds_1 = sampleLineGroup_1.GetSampleLineIds();
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                int sampleLIneNumber_1 = sampleLineIds_1.Count;

                //đồng bộ thông tin
                if (sampleLIneNumber_1 <= sampleLIneNumber)
                {
                    for (int i = 0; i < sampleLIneNumber_1; i++)
                    {
                        SampleLine? sampleLine = tr.GetObject(sampleLineIds[i + sampleLIneNumberBegin - 1], OpenMode.ForWrite) as SampleLine;
                        SampleLine? sampleLine_c = tr.GetObject(sampleLineIds_1[i], OpenMode.ForWrite) as SampleLine;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                        sampleLine_c.Station = sampleLine.Station;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                        sampleLine_c.Name = sampleLine.Name + "c";
                    }
                }
                else A.Ok("Số cọc đích nhiều hơn cọc nguồn. Cần xóa bớt cọc đích");


                //rename sampleline
                char[] charsToTrim = ['z', ' ', '\'', 'c'];
                foreach (ObjectId sampleLineId in sampleLineGroup_1.GetSampleLineIds())
                {
                    SampleLine? sampleline = tr.GetObject(sampleLineId, OpenMode.ForWrite) as SampleLine;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    sampleline.Name = sampleline.Name.Trim(charsToTrim);
#pragma warning restore CS8602 // Dereference of a possibly null reference.

                }


                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }

        [CommandMethod("CTS_DichCoc_TinhTien40")]
        public static void CTSDichCocTinhTien40()
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here

                ObjectId sampleLineId_S = UserInput.GSampleLineId("\n Chọn sampleLine cọc mốc: \n");
                SampleLine? sampleLine_S = tr.GetObject(sampleLineId_S, OpenMode.ForWrite) as SampleLine;
                ObjectId sampleLineId_D = UserInput.GSampleLineId("\n Chọn sampleLine cọc cần dịch: \n");
                SampleLine? sampleLine_D = tr.GetObject(sampleLineId_D, OpenMode.ForWrite) as SampleLine;
                Double khoangDich = 40;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                sampleLine_D.Station = sampleLine_S.Station + khoangDich;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning restore CS8602 // Dereference of a possibly null reference.



                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }

        [CommandMethod("CTS_DichCoc_TinhTien_20")]
        public static void CTSDichCocTinhTien20()
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here

                ObjectId sampleLineId_S = UserInput.GSampleLineId("\n Chọn sampleLine cọc mốc: \n");
                SampleLine? sampleLine_S = tr.GetObject(sampleLineId_S, OpenMode.ForWrite) as SampleLine;
                ObjectId sampleLineId_D = UserInput.GSampleLineId("\n Chọn sampleLine cọc cần dịch: \n");
                SampleLine? sampleLine_D = tr.GetObject(sampleLineId_D, OpenMode.ForWrite) as SampleLine;
                Double khoangDich = 20;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                sampleLine_D.Station = sampleLine_S.Station + khoangDich;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning restore CS8602 // Dereference of a possibly null reference.



                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }

        [CommandMethod("CTS_DoiTenCoc_H")]
        public static void CTSDoiTenCocH()
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here

                ObjectId sampleLineId_S = UserInput.GSampleLineId("\n Chọn sampleLine cọc mốc: \n");
                SampleLine? sampleLine_S = tr.GetObject(sampleLineId_S, OpenMode.ForWrite) as SampleLine;
                ObjectId sampleLineId_D = UserInput.GSampleLineId("\n Chọn sampleLine cọc cần đổi tên: \n");
                SampleLine? sampleLine_D = tr.GetObject(sampleLineId_D, OpenMode.ForWrite) as SampleLine;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                sampleLine_D.Name = sampleLine_S.Name + "A";
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning restore CS8602 // Dereference of a possibly null reference.



                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }

        // [GHI CHÚ]: Lệnh CTS_PhatSinhCoc_TheoBang đã được nâng cấp toàn diện và chuyển sang:
        // Civil Tool/46.CTS_PhatSinhCoc_TheoBang.cs kèm Form PhatSinhCocTheoBangForm.cs


        [CommandMethod("CTS_Copy_BeRong_sampleLine")]
        public static void CTSCopyBeRongSampleLine()
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here

                ObjectId sampleLineId = UserInput.GSampleLineId("Chọn sampleLine GỐC cần sao chép bề rộng:");
                double offsetLeft = new();
                double offsetRight = new();
                Point3d point3dLeft = new();
                Point3d point3dRight = new();
                Point3d point3dCenter = new();
                SampleLine? sampleLine = tr.GetObject(sampleLineId, OpenMode.ForWrite) as SampleLine;
#pragma warning disable CS8604 // Possible null reference argument.
                ObjectId alignmentId = sampleLine.GetParentAlignmentId();
#pragma warning restore CS8604 // Possible null reference argument.
                Alignment? alignment = tr.GetObject(alignmentId, OpenMode.ForWrite) as Alignment;
                if (sampleLine.Vertices.Count > 3)
                {
                    A.Ok("Kiểm tra lại sampleline có nhiều hơn 3 điểm");
                }
                foreach (SampleLineVertex vertex in sampleLine.Vertices)
                {

                    if (vertex.Side == SampleLineVertexSideType.Left)
                    {
                        point3dLeft = vertex.Location;
                    }
                    if (vertex.Side == SampleLineVertexSideType.Right)
                    {
                        point3dRight = vertex.Location;
                    }
                    if (vertex.Side == SampleLineVertexSideType.Center)
                    {
                        point3dCenter = vertex.Location;
                    }
                    offsetLeft = point3dCenter.DistanceTo(point3dLeft);
                    offsetRight = point3dCenter.DistanceTo(point3dRight);
                }
                double station = new();
                double offset = new();
                double easting = new();
                double northing = new();
                ObjectIdCollection sampleLineIds = UserInput.GSelectionSetWithType("Chọn các sample cần copy bề rộng: \n", "AECC_SAMPLE_LINE");
                A.OkHere();
                foreach (ObjectId SLineId in sampleLineIds)
                {
                    SampleLine? sampleLine1 = tr.GetObject(SLineId, OpenMode.ForWrite) as SampleLine;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    if (sampleLine1.Vertices.Count > 3)
                    {
                        A.Ok("Kiểm tra lại sampleline có nhiều hơn 3 điểm");
                    }
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                    foreach (SampleLineVertex vertex in sampleLine1.Vertices)
                    {
                        if (vertex.Side == SampleLineVertexSideType.Center)
                        {
                            point3dCenter = vertex.Location;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                            alignment.StationOffset(point3dCenter.X, point3dCenter.Y, ref station, ref offset);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                        }
                    }
                    foreach (SampleLineVertex vertex in sampleLine1.Vertices)
                    {
                        if (vertex.Side == SampleLineVertexSideType.Left)
                        {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                            alignment.PointLocation(station, -offsetLeft, ref easting, ref northing);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                            vertex.Location = new Point3d(easting, northing, 0);
                        }
                    }
                    foreach (SampleLineVertex vertex in sampleLine1.Vertices)
                    {
                        if (vertex.Side == SampleLineVertexSideType.Right)
                        {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                            alignment.PointLocation(station, offsetRight, ref easting, ref northing);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                            vertex.Location = new Point3d(easting, northing, 0);
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

        [CommandMethod("CTS_Offset_BeRong_sampleLine")]
        public static void CTSOffsetBeRongSampleLine()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var db = doc.Database;
            var ed = doc.Editor;

            try
            {
                var selOpts = new PromptSelectionOptions { MessageForAdding = "\nChọn các sample lines:" };
                var filter = new SelectionFilter([new TypedValue(0, "AECC_SAMPLE_LINE")]);
                var selRes = ed.GetSelection(selOpts, filter);
                if (selRes.Status != PromptStatus.OK) return;

                // Show the new form
                var form = new SampleLineOffsetPolylineForm();
                if (Application.ShowModalDialog(form) != DialogResult.OK) return;

                using var tr = db.TransactionManager.StartTransaction();
                Polyline? plLeft = form.LeftPolylineId != ObjectId.Null ? (Polyline)tr.GetObject(form.LeftPolylineId, OpenMode.ForRead) : null;
                Polyline? plRight = form.RightPolylineId != ObjectId.Null ? (Polyline)tr.GetObject(form.RightPolylineId, OpenMode.ForRead) : null;

                double leftExcess = form.LeftExcess;
                double rightExcess = form.RightExcess;

                int updated = 0, noCenter = 0, noLeft = 0, noRight = 0, missLeft = 0, missRight = 0;

                foreach (ObjectId id in selRes.Value.GetObjectIds())
                {
                    var sl = tr.GetObject(id, OpenMode.ForWrite) as SampleLine;
                    if (sl == null) continue;

                    var centerV = sl.Vertices.Cast<SampleLineVertex>().FirstOrDefault(v => v.Side == SampleLineVertexSideType.Center);
                    if (centerV == null) { noCenter++; continue; }

                    Point3d centerPt = centerV.Location;
                    bool sampleLineUpdated = false;

                    // Xử lý bên trái
                    if (plLeft != null)
                    {
                        var leftV = sl.Vertices.Cast<SampleLineVertex>().FirstOrDefault(v => v.Side == SampleLineVertexSideType.Left);
                        if (leftV != null)
                        {
                            Vector3d dirLeft = (leftV.Location - centerPt);
                            if (dirLeft.Length < 1e-6) dirLeft = new Vector3d(-1, 0, 0);
                            dirLeft = dirLeft.GetNormal();

                            Point3d? hitLeft = IntersectOneDirection(centerPt, dirLeft, plLeft);
                            if (hitLeft != null)
                            {
                                Point3d newLoc = hitLeft.Value + dirLeft * leftExcess;
                                leftV.Location = new(newLoc.X, newLoc.Y, leftV.Location.Z);
                                sampleLineUpdated = true;
                            }
                            else { missLeft++; }
                        }
                        else { noLeft++; }
                    }

                    // Xử lý bên phải
                    if (plRight != null)
                    {
                        var rightV = sl.Vertices.Cast<SampleLineVertex>().FirstOrDefault(v => v.Side == SampleLineVertexSideType.Right);
                        if (rightV != null)
                        {
                            Vector3d dirRight = (rightV.Location - centerPt);
                            if (dirRight.Length < 1e-6) dirRight = new Vector3d(1, 0, 0);
                            dirRight = dirRight.GetNormal();

                            Point3d? hitRight = IntersectOneDirection(centerPt, dirRight, plRight);
                            if (hitRight != null)
                            {
                                Point3d newLoc = hitRight.Value + dirRight * rightExcess;
                                rightV.Location = new(newLoc.X, newLoc.Y, rightV.Location.Z);
                                sampleLineUpdated = true;
                            }
                            else { missRight++; }
                        }
                        else { noRight++; }
                    }

                    if (sampleLineUpdated) updated++;
                }

                tr.Commit();
                ed.WriteMessage($"\nCập nhật xong: {updated} sample lines. Không center: {noCenter}, không left vtx: {noLeft}, không right vtx: {noRight}, không giao trái: {missLeft}, không giao phải: {missRight}.");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\nLỗi: " + ex.Message);
            }
        }

        private static Point3d? IntersectOneDirection(Point3d origin, Vector3d dir, Polyline boundary)
        {
            const double maxLen = 100000.0; // 100 km để đủ dài
            using var ray = new Line(origin, origin + dir * maxLen);
            var pts = new Point3dCollection();
            try { boundary.IntersectWith(ray, Intersect.OnBothOperands, pts, IntPtr.Zero, IntPtr.Zero); }
            catch { return null; }
            if (pts.Count == 0) return null;
            double best = double.MaxValue; Point3d bestPt = Point3d.Origin;
            for (int i = 0; i < pts.Count; i++)
            {
                double d = (pts[i] - origin).DotProduct(dir);
                if (d > 1e-6 && d < best) { best = d; bestPt = pts[i]; }
            }
            return best == double.MaxValue ? null : bestPt;
        }



















































    }

    // Form for selecting polylines and extra offsets
    public class SampleLineOffsetPolylineForm : Form
    {
        public ObjectId LeftPolylineId { get; private set; } = ObjectId.Null;
        public ObjectId RightPolylineId { get; private set; } = ObjectId.Null;
        public double LeftExcess { get; private set; } = 0.0;
        public double RightExcess { get; private set; } = 0.0;

        private System.Windows.Forms.Label lblLeftPl;
        private System.Windows.Forms.Label lblRightPl;
        private TextBox txtLeftExcess;
        private TextBox txtRightExcess;
        private Button btnPickLeft;
        private Button btnPickRight;
        private Button btnOK;
        private Button btnCancel;

        public SampleLineOffsetPolylineForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Offset Sample Line theo Polyline";
            this.Size = new System.Drawing.Size(400, 250);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            var gbLeft = new GroupBox { Text = "Bên Trái", Location = new System.Drawing.Point(10, 10), Size = new System.Drawing.Size(360, 75) };
            lblLeftPl = new System.Windows.Forms.Label { Text = "Chưa chọn Polyline", Location = new System.Drawing.Point(10, 20), Size = new System.Drawing.Size(200, 20) };
            btnPickLeft = new Button { Text = "Chọn Model", Location = new System.Drawing.Point(220, 15), Size = new System.Drawing.Size(130, 25) };
            btnPickLeft.Click += (s, e) => PickPolyline(true);
            var lblL2 = new System.Windows.Forms.Label { Text = "Khoảng vượt:", Location = new System.Drawing.Point(10, 48), Size = new System.Drawing.Size(100, 20) };
            txtLeftExcess = new TextBox { Text = "0.0", Location = new System.Drawing.Point(110, 45), Size = new System.Drawing.Size(100, 20) };

            gbLeft.Controls.AddRange(new Control[] { lblLeftPl, btnPickLeft, lblL2, txtLeftExcess });

            var gbRight = new GroupBox { Text = "Bên Phải", Location = new System.Drawing.Point(10, 90), Size = new System.Drawing.Size(360, 75) };
            lblRightPl = new System.Windows.Forms.Label { Text = "Chưa chọn Polyline", Location = new System.Drawing.Point(10, 20), Size = new System.Drawing.Size(200, 20) };
            btnPickRight = new Button { Text = "Chọn Model", Location = new System.Drawing.Point(220, 15), Size = new System.Drawing.Size(130, 25) };
            btnPickRight.Click += (s, e) => PickPolyline(false);
            var lblR2 = new System.Windows.Forms.Label { Text = "Khoảng vượt:", Location = new System.Drawing.Point(10, 48), Size = new System.Drawing.Size(100, 20) };
            txtRightExcess = new TextBox { Text = "0.0", Location = new System.Drawing.Point(110, 45), Size = new System.Drawing.Size(100, 20) };

            gbRight.Controls.AddRange(new Control[] { lblRightPl, btnPickRight, lblR2, txtRightExcess });

            btnOK = new Button { Text = "OK", Location = new System.Drawing.Point(210, 175), Size = new System.Drawing.Size(75, 25) };
            btnOK.Click += BtnOK_Click;
            btnCancel = new Button { Text = "Hủy", Location = new System.Drawing.Point(295, 175), Size = new System.Drawing.Size(75, 25) };
            btnCancel.DialogResult = DialogResult.Cancel;

            this.Controls.AddRange(new Control[] { gbLeft, gbRight, btnOK, btnCancel });
        }

        private void PickPolyline(bool isLeft)
        {
            var ed = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor;
            this.Visible = false;
            try
            {
                var peo = new PromptEntityOptions($"\nChọn Polyline biên {(isLeft ? "TRÁI" : "PHẢI")}: ");
                peo.SetRejectMessage("\nPhải là Polyline.");
                peo.AddAllowedClass(typeof(Polyline), true);
                var per = ed.GetEntity(peo);
                if (per.Status == PromptStatus.OK)
                {
                    if (isLeft)
                    {
                        LeftPolylineId = per.ObjectId;
                        lblLeftPl.Text = $"Đã chọn: {per.ObjectId.Handle}";
                    }
                    else
                    {
                        RightPolylineId = per.ObjectId;
                        lblRightPl.Text = $"Đã chọn: {per.ObjectId.Handle}";
                    }
                }
            }
            finally
            {
                this.Visible = true;
            }
        }

        private void BtnOK_Click(object? sender, EventArgs e)
        {
            if (double.TryParse(txtLeftExcess.Text, out double l)) LeftExcess = l;
            if (double.TryParse(txtRightExcess.Text, out double r)) RightExcess = r;

            if (LeftPolylineId == ObjectId.Null && RightPolylineId == ObjectId.Null)
            {
                MessageBox.Show("Vui lòng chọn ít nhất một Polyline biên.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}


using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using ATable = Autodesk.AutoCAD.DatabaseServices.Table;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;

using System;
using System.Windows.Forms;
using System.Collections.Generic;
//using Autodesk.AutoCAD.Interop.Common;
using Autodesk.AutoCAD.Colors;
using Color = Autodesk.AutoCAD.Colors.Color;

namespace MyFirstProject.Extensions
{
    public class UtilitiesCAD
    {
        public static Point2d ConvertP3dToP2d(Point3d point3d)
        {
            return new Point2d(point3d.X, point3d.Y);
        }
        public static Point2d Offset2d(Point3d pointOffset, double x, double y)
        {
            return new Point2d(pointOffset.X + x, pointOffset.Y + y);
        }
        public static void CreateTableCoordinate(int numberRow, int numberColumn, string title, string[] samplelineIds, string[] eastings, string[] northings)
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                //start here
                UserInput userInput = new();
                BlockTable bt = (BlockTable)tr.GetObject(A.Doc.Database.BlockTableId, OpenMode.ForRead);
                //Point3d point3D = userInput.g_point("Specify a point for the position of table:");
                TableStyle tableStyle = new();
                ATable table = new();
                table.SetSize(numberRow + 1, numberColumn);
                table.SetRowHeight(5);
                table.SetColumnWidth(18);
                table.Position = UserInput.GPoint("\n Chọn vị trí điểm" + " đặt bảng: \n");

                //input title and header
                //title
                table.Cells[0, 0].TextHeight = 2;
                table.Cells[0, 0].TextString = "Bảng tọa độ cọc: đường " + title;
                table.Cells[0, 0].Alignment = CellAlignment.MiddleCenter;

                // header
                table.Cells[1, 0].TextHeight = 2;
                table.Cells[1, 0].TextString = "Tên cọc";
                table.Cells[1, 0].Alignment = CellAlignment.MiddleCenter;

                table.Cells[1, 1].TextHeight = 2;
                table.Cells[1, 1].TextString = "Tọa độ Y";
                table.Cells[1, 1].Alignment = CellAlignment.MiddleCenter;

                table.Cells[1, 2].TextHeight = 2;
                table.Cells[1, 2].TextString = "Tọa độ X";
                table.Cells[1, 2].Alignment = CellAlignment.MiddleCenter;

                // add data to table
                for (int i = 2; i < numberRow + 1; i++)
                {

                    table.Cells[i, 0].TextHeight = 2;
                    table.Cells[i, 0].TextString = samplelineIds[i - 1];
                    table.Cells[i, 0].Alignment = CellAlignment.MiddleCenter;

                    table.Cells[i, 1].TextHeight = 2;
                    table.Cells[i, 1].TextString = eastings[i - 1];
                    table.Cells[i, 1].Alignment = CellAlignment.MiddleCenter;

                    table.Cells[i, 2].TextHeight = 2;
                    table.Cells[i, 2].TextString = northings[i - 1];
                    table.Cells[i, 2].Alignment = CellAlignment.MiddleCenter;

                }

                //draw table
                table.GenerateLayout();
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                btr.AppendEntity(table);
                tr.AddNewlyCreatedDBObject(table, true);




                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }

        public static void CreateTableCoordinate2(int numberRow, int numberColumn, string title, string[] samplelineIds, string[] eastings, string[] northings, string[] stations)
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                //start here
                UserInput userInput = new();
                BlockTable bt = (BlockTable)tr.GetObject(A.Doc.Database.BlockTableId, OpenMode.ForRead);
                //Point3d point3D = userInput.g_point("Specify a point for the position of table:");
                TableStyle tableStyle = new();
                ATable table = new();
                table.SetSize(numberRow + 1, numberColumn);
                table.SetRowHeight(5);
                table.SetColumnWidth(18);
                table.Position = UserInput.GPoint("\n Chọn vị trí điểm" + " đặt bảng: \n");

                //input title and header
                //title
                table.Cells[0, 0].TextHeight = 2;
                table.Cells[0, 0].TextString = "Bảng tọa độ cọc: đường " + title;
                table.Cells[0, 0].Alignment = CellAlignment.MiddleCenter;

                // header
                table.Cells[1, 0].TextHeight = 2;
                table.Cells[1, 0].TextString = "Tên cọc";
                table.Cells[1, 0].Alignment = CellAlignment.MiddleCenter;

                table.Cells[1, 1].TextHeight = 2;
                table.Cells[1, 1].TextString = "Tọa độ Y";
                table.Cells[1, 1].Alignment = CellAlignment.MiddleCenter;

                table.Cells[1, 2].TextHeight = 2;
                table.Cells[1, 2].TextString = "Tọa độ X";
                table.Cells[1, 2].Alignment = CellAlignment.MiddleCenter;

                table.Cells[1, 3].TextHeight = 2;
                table.Cells[1, 3].TextString = "Lý trình";
                table.Cells[1, 3].Alignment = CellAlignment.MiddleCenter;

                // add data to table
                for (int i = 2; i < numberRow + 1; i++)
                {

                    table.Cells[i, 0].TextHeight = 2;
                    table.Cells[i, 0].TextString = samplelineIds[i - 1];
                    table.Cells[i, 0].Alignment = CellAlignment.MiddleCenter;

                    table.Cells[i, 1].TextHeight = 2;
                    table.Cells[i, 1].TextString = eastings[i - 1];
                    table.Cells[i, 1].Alignment = CellAlignment.MiddleCenter;

                    table.Cells[i, 2].TextHeight = 2;
                    table.Cells[i, 2].TextString = northings[i - 1];
                    table.Cells[i, 2].Alignment = CellAlignment.MiddleCenter;

                    table.Cells[i, 3].TextHeight = 2;
                    table.Cells[i, 3].TextString = stations[i - 1];
                    table.Cells[i, 3].Alignment = CellAlignment.MiddleCenter;

                }

                //draw table
                table.GenerateLayout();
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                btr.AppendEntity(table);
                tr.AddNewlyCreatedDBObject(table, true);

                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }

        public static void TaoBangHoTHu(int numberRow, int numberColumn, string title, string[] samplelineIds, string[] eastings, string[] northings)
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                //start here
                UserInput userInput = new();
                BlockTable bt = (BlockTable)tr.GetObject(A.Doc.Database.BlockTableId, OpenMode.ForRead);
                //Point3d point3D = userInput.g_point("Specify a point for the position of table:");
                TableStyle tableStyle = new();
                ATable table = new();
                table.SetSize(numberRow + 1, numberColumn);
                table.SetRowHeight(5);
                table.SetColumnWidth(18);
                table.Position = UserInput.GPoint("\n Chọn vị trí điểm" + " đặt bảng: \n");

                //input title and header
                //title
                table.Cells[0, 0].TextHeight = 2;
                table.Cells[0, 0].TextString = "Bảng thống kê: " + title;
                table.Cells[0, 0].Alignment = CellAlignment.MiddleCenter;

                // header
                table.Cells[1, 0].TextHeight = 2;
                table.Cells[1, 0].TextString = "Tên hố thu";
                table.Cells[1, 0].Alignment = CellAlignment.MiddleCenter;

                table.Cells[1, 1].TextHeight = 2;
                table.Cells[1, 1].TextString = "Cao độ tự nhiên";
                table.Cells[1, 1].Alignment = CellAlignment.MiddleCenter;

                table.Cells[1, 2].TextHeight = 2;
                table.Cells[1, 2].TextString = "Cao độ khuôn đường";
                table.Cells[1, 2].Alignment = CellAlignment.MiddleCenter;

                // add data to table
                for (int i = 2; i < numberRow + 1; i++)
                {

                    table.Cells[i, 0].TextHeight = 2;
                    table.Cells[i, 0].TextString = samplelineIds[i - 1];
                    table.Cells[i, 0].Alignment = CellAlignment.MiddleCenter;

                    table.Cells[i, 1].TextHeight = 2;
                    table.Cells[i, 1].TextString = eastings[i - 1];
                    table.Cells[i, 1].Alignment = CellAlignment.MiddleCenter;

                    table.Cells[i, 2].TextHeight = 2;
                    table.Cells[i, 2].TextString = northings[i - 1];
                    table.Cells[i, 2].Alignment = CellAlignment.MiddleCenter;

                }

                //draw table
                table.GenerateLayout();
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                btr.AppendEntity(table);
                tr.AddNewlyCreatedDBObject(table, true);




                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }

        public static void CreateTableKhoiLuong(int numberRow, int numberColumn, string tenDuong, List<string> lyTrinh, List<string> tenCoc, List<string> danhCap, string layerTable, string tenVatLieu)
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                //start here
                UserInput userInput = new();
                BlockTable bt = (BlockTable)tr.GetObject(A.Doc.Database.BlockTableId, OpenMode.ForRead);
                //Point3d point3D = userInput.g_point("Specify a point for the position of table:");
                TableStyle tableStyle = new();
                ATable table = new();
                table.SetSize(numberRow + 2, numberColumn);
                table.SetRowHeight(5);
                table.SetColumnWidth(20);
                table.Position = UserInput.GPoint("\n Chọn vị trí điểm" + " đặt bảng: \n");
                table.Layer = layerTable;

                //input title and header
                //title
                table.Cells[0, 0].TextHeight = 2;
                table.Cells[0, 0].TextString = "Bảng khối lượng " + tenVatLieu + " đường " + tenDuong;
                table.Cells[0, 0].Alignment = CellAlignment.MiddleCenter;

                // header
                table.Cells[1, 0].TextHeight = 2;
                table.Cells[1, 0].TextString = "Lý trình";
                table.Cells[1, 0].Alignment = CellAlignment.MiddleCenter;

                table.Cells[1, 1].TextHeight = 2;
                table.Cells[1, 1].TextString = "Tên cọc";
                table.Cells[1, 1].Alignment = CellAlignment.MiddleCenter;

                table.Cells[1, 2].TextHeight = 2;
                table.Cells[1, 2].TextString = "Khối lượng";
                table.Cells[1, 2].Alignment = CellAlignment.MiddleCenter;

                // add data to table
                for (int i = 2; i < numberRow + 2; i++)
                {

                    table.Cells[i, 0].TextHeight = 2;
                    table.Cells[i, 0].TextString = lyTrinh[i - 2];
                    table.Cells[i, 0].Alignment = CellAlignment.MiddleCenter;

                    table.Cells[i, 1].TextHeight = 2;
                    table.Cells[i, 1].TextString = tenCoc[i - 2];
                    table.Cells[i, 1].Alignment = CellAlignment.MiddleCenter;

                    table.Cells[i, 2].TextHeight = 2;
                    table.Cells[i, 2].TextString = danhCap[i - 2];
                    table.Cells[i, 2].Alignment = CellAlignment.MiddleCenter;

                }

                //draw table
                table.GenerateLayout();
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                btr.AppendEntity(table);
                tr.AddNewlyCreatedDBObject(table, true);




                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }
        public static void CreateOpenPolyline(int numberPoint, string[] eastings, string[] northings)
        {
            // Start a transaction
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            // Open the Block table for read
            BlockTable? acBlkTbl = tr.GetObject(A.Db.BlockTableId, OpenMode.ForRead) as BlockTable ?? throw new InvalidOperationException("BlockTable could not be retrieved.");
            // Open the Block table record Model space for write
            BlockTableRecord? acBlkTblRec = tr.GetObject(acBlkTbl[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord ?? throw new InvalidOperationException("BlockTableRecord could not be retrieved.");

            // Create a polyline with 
            Polyline acPoly = new();
            for (int i = 0; i < numberPoint; i++)
            {
                double easting = Convert.ToDouble(eastings[i]);
                double northing = Convert.ToDouble(northings[i]);
                acPoly.AddVertexAt(i, new Point2d(easting, northing), 0, 0, 0);
            }
            acPoly.Closed = false;
            
            // Set layer to Defpoints (không in được)
            acPoly.Layer = "Defpoints";
            
            // Add the new object to the block table record and the transaction
            acBlkTblRec.AppendEntity(acPoly);
            tr.AddNewlyCreatedDBObject(acPoly, true);

            // Save the new object to the database
            tr.Commit();
        }
        public static double CreatePolylineDanhCap(double x1, double x2, double y1, double y2, double bacCap, double docDanhCap)
        {
            if (Math.Abs(x2 - x1) < 1e-9 || bacCap <= 0)
            {
                return 0;
            }

            using (Transaction tr = A.Db.TransactionManager.StartTransaction())
            {
                //UtilitiesCAD CAD = new UtilitiesCAD();
                BlockTable bt = (BlockTable)tr.GetObject(A.Doc.Database.BlockTableId, OpenMode.ForRead);
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                //start here
                double at = (y2 - y1) / (x2 - x1);
                double b = -x1 * (y2 - y1) / (x2 - x1) + y1;
                double xt = x1;
                double yt = at * xt + b;
                List<Point2d> pointList = [];
                int jt = 0;
                do
                {
                    pointList.Add(new Point2d(xt, yt));
                    jt += 1;
                    xt += bacCap;
                    yt = at * xt + b;
                }
                while (xt < x2);

                //vẽ polyline đánh cấp
                //pointList.Reverse();

                Polyline polyline = new();
                int soChan = 0;
                int soLe = 1;
                for (int j = 0; j < pointList.Count; j++)
                {
                    polyline.AddVertexAt(soChan, new Point2d(pointList[j].X, pointList[j].Y), 0, 0, 0);
                    if (pointList[j].X + bacCap < x2)
                    {
                        if (at > docDanhCap)
                        {
                            polyline.AddVertexAt(soLe, new Point2d(pointList[j].X + bacCap, pointList[j].Y), 0, 0, 0);
                        }
                        else
                        {
                            double nextY = (j + 1 < pointList.Count) ? pointList[j + 1].Y : y2;
                            polyline.AddVertexAt(soLe, new Point2d(pointList[j].X, nextY), 0, 0, 0);
                        }

                    }

                    soChan += 2;
                    soLe += 2;
                }

                btr.AppendEntity(polyline);
                tr.AddNewlyCreatedDBObject(polyline, true);
                double polyLineArea = polyline.Area;
                //polyline.Layer = layer1.Name;

                tr.Commit();
                return polyLineArea;
            }

        }
        public static double CSumList(List<double> list)
        {
            double sum = 0;
            foreach (double item in list)
            {
                sum += item;
            }
            return sum;

        }
        public static LayerTableRecord CCreateLayer(string NameLayer)
        {
            using (Transaction tr = A.Db.TransactionManager.StartTransaction())
            {
                LayerTable acLyrTbl = (LayerTable)tr.GetObject(A.Db.LayerTableId, OpenMode.ForRead);
                LayerTableRecord acLyrTblRec;

                if (!acLyrTbl.Has(NameLayer))
                {
                    acLyrTbl.UpgradeOpen();
                    acLyrTblRec = new LayerTableRecord
                    {
                        Name = NameLayer
                    };
                    acLyrTbl.Add(acLyrTblRec);
                    tr.AddNewlyCreatedDBObject(acLyrTblRec, true);
                }
                else
                {
                    acLyrTblRec = (LayerTableRecord)tr.GetObject(acLyrTbl[NameLayer], OpenMode.ForRead);
                }

                tr.Commit();
                return acLyrTblRec;
            }
        }
        public static LayerTableRecord CCreateLayerWithFullOption(string NameLayer, short colorIndex, LineWeight lineweight, string lineTypeName)
        {
            using (Transaction tr = A.Db.TransactionManager.StartTransaction())
            {
                LayerTable acLyrTbl = (LayerTable)tr.GetObject(A.Db.LayerTableId, OpenMode.ForRead);
                LayerTableRecord acLyrTblRec;

                if (!acLyrTbl.Has(NameLayer))
                {
                    acLyrTbl.UpgradeOpen();
                    acLyrTblRec = new LayerTableRecord
                    {
                        Name = NameLayer,
                        Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex),
                        LineWeight = lineweight
                    };

                    //add linetype
                    LinetypeTable? acLinTbl = tr.GetObject(A.Db.LinetypeTableId, OpenMode.ForRead) as LinetypeTable;
                    if (acLinTbl != null && acLinTbl.Has(lineTypeName))
                    {
                        acLyrTblRec.LinetypeObjectId = acLinTbl[lineTypeName];
                    }

                    acLyrTbl.Add(acLyrTblRec);
                    tr.AddNewlyCreatedDBObject(acLyrTblRec, true);
                }
                else
                {
                    acLyrTblRec = (LayerTableRecord)tr.GetObject(acLyrTbl[NameLayer], OpenMode.ForRead);
                }

                tr.Commit();
                return acLyrTblRec;
            }
        }
        public static void CDelLayerAndObjectOnIt(string layerName)
        {
            using (Transaction tr = A.Db.TransactionManager.StartTransaction())
            {
                // Create a Selection Filter which selects all Objects on a given Layer
                TypedValue[] tvs = [new((int)DxfCode.LayerName, layerName)];
                SelectionFilter sf = new(tvs);
                // Select all Objects on Layer
                PromptSelectionResult psr = A.Ed.SelectAll(sf);
                if (psr.Status == PromptStatus.OK && psr.Value != null)
                {
                    SelectionSet ss = psr.Value;
                    // Iterate through objects and delete them
                    foreach (SelectedObject so in ss)
                    {
                        if (so != null && so.ObjectId.IsValid && !so.ObjectId.IsErased)
                        {
                            DBObject ob = tr.GetObject(so.ObjectId, OpenMode.ForWrite);
                            ob.Erase();
                        }
                    }
                }

                tr.Commit();
            }
        }
        public static void CCreateText(Point3d position, double height, string TextString, string layerName, string textStyle)
        {
            using (Transaction tr = A.Db.TransactionManager.StartTransaction())
            {
                _ = new UserInput();
                _ = new UtilitiesCAD();
                _ = new UtilitiesC3D();
                //start here
                // Open the Block table for read
                BlockTable? acBlkTbl;
                acBlkTbl = tr.GetObject(A.Db.BlockTableId,
                                                   OpenMode.ForRead) as BlockTable;
                // Open the Block table record Model space for write
                BlockTableRecord? acBlkTblRec;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                acBlkTblRec = tr.GetObject(acBlkTbl[BlockTableRecord.ModelSpace],
                                                      OpenMode.ForWrite) as BlockTableRecord;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                // Create a single-line text object
                DBText acText = new();
                acText.SetDatabaseDefaults();
                acText.Position = position;
                acText.Height = height;
                acText.TextString = TextString;
                acText.Layer = layerName;

                TextStyleTable? styleTable = tr.GetObject(A.Db.TextStyleTableId, OpenMode.ForRead) as TextStyleTable;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                if (styleTable.Has(textStyle))
                {
                    acText.TextStyleId = styleTable[textStyle];
                }
#pragma warning restore CS8602 // Dereference of a possibly null reference.


#pragma warning disable CS8602 // Dereference of a possibly null reference.
                acBlkTblRec.AppendEntity(acText);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                tr.AddNewlyCreatedDBObject(acText, true);

                A.Ed.Command("_UPDATEFIELD", acText);
                // Save the changes and dispose of the transaction

                tr.Commit();
            }
        }
        public static void CCreateTextWithOutPut(Point3d position, double height, string TextString, string layerName, string textStyle)
        {
            using (Transaction tr = A.Db.TransactionManager.StartTransaction())
            {
                _ = new
                UserInput();
                _ = new UtilitiesCAD();
                _ = new UtilitiesC3D();

                //start here
                // Open the Block table for read
                BlockTable? acBlkTbl;
                acBlkTbl = tr.GetObject(A.Db.BlockTableId, OpenMode.ForRead) as BlockTable;
                // Open the Block table record Model space for write
                BlockTableRecord? acBlkTblRec;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                acBlkTblRec = tr.GetObject(acBlkTbl[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                // Create a single-line text object
                DBText acText = new();
                acText.SetDatabaseDefaults();
                acText.Position = position;
                acText.Height = height;
                acText.TextString = TextString;
                acText.Layer = layerName;

                TextStyleTable? styleTable = tr.GetObject(A.Db.TextStyleTableId, OpenMode.ForRead) as TextStyleTable;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                if (styleTable.Has(textStyle))
                {
                    acText.TextStyleId = styleTable[textStyle];
                }
#pragma warning restore CS8602 // Dereference of a possibly null reference.

#pragma warning disable CS8602 // Dereference of a possibly null reference.
                acBlkTblRec.AppendEntity(acText);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                tr.AddNewlyCreatedDBObject(acText, true);

                // Save the changes and dispose of the transaction

                tr.Commit();

            }
        }
        public static DBText CCreateText2(Point3d position, double height, string TextString, string layerName, string textStyle)
        {
            using (Transaction tr = A.Db.TransactionManager.StartTransaction())
            {
                _ = new UserInput();
                _ = new UtilitiesCAD();
                _ = new UtilitiesC3D();
                //start here
                // Open the Block table for read
                BlockTable? acBlkTbl;
                acBlkTbl = tr.GetObject(A.Db.BlockTableId,
                                                   OpenMode.ForRead) as BlockTable;
                // Open the Block table record Model space for write
                BlockTableRecord? acBlkTblRec;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                acBlkTblRec = tr.GetObject(acBlkTbl[BlockTableRecord.ModelSpace],
                                                      OpenMode.ForWrite) as BlockTableRecord;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                // Create a single-line text object
                DBText acText = new();
                acText.SetDatabaseDefaults();
                acText.Position = position;
                acText.Height = height;
                acText.TextString = TextString;
                acText.Layer = layerName;

                TextStyleTable? styleTable = tr.GetObject(A.Db.TextStyleTableId, OpenMode.ForRead) as TextStyleTable;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                if (styleTable.Has(textStyle))
                {
                    acText.TextStyleId = styleTable[textStyle];
                }
#pragma warning restore CS8602 // Dereference of a possibly null reference.

#pragma warning disable CS8602 // Dereference of a possibly null reference.
                acBlkTblRec.AppendEntity(acText);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                tr.AddNewlyCreatedDBObject(acText, true);

                // Save the changes and dispose of the transaction

                tr.Commit();


                A.Ed.Command("_UPDATEFIELD", acText.Id);
                return acText;
            }
        }
        public static string ConvertTextToField(DBText text)
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();

            //start here
            //tạo field data
            string str1 = "%<\\AcObjProp Object(%<\\_ObjId ";
            string str2 = ">%).TextString>%";
            string format = text.Id.ToString();
            format = format.Replace(")", "");
            format = format.Replace("(", "");
            format = str1 + format + str2;
            tr.Commit();
            return format;
        }
        public static string ConvertMTextToField(ObjectId textId)
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();

            //start here
            //tạo field data
            string str1 = "%<\\AcObjProp Object(%<\\_ObjId ";
            string str2 = ">%).TextString>%";
            string format = textId.ToString();
            format = format.Replace(")", "");
            format = format.Replace("(", "");
            format = str1 + format + str2;
            tr.Commit();
            return format;
        }
        public static ObjectId CreateDimStyle(string DimStyleName)
        {
            Database db = HostApplicationServices.WorkingDatabase;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                DimStyleTable DimTabb = (DimStyleTable)tr.GetObject(db.DimStyleTableId, OpenMode.ForRead);
                ObjectId dimId = ObjectId.Null;

                if (!DimTabb.Has(DimStyleName))
                {
                    DimTabb.UpgradeOpen();
                    DimStyleTableRecord newRecord = new()
                    {
                        Name = DimStyleName
                    };
                    dimId = DimTabb.Add(newRecord);
                    tr.AddNewlyCreatedDBObject(newRecord, true);

                }
                else
                {
                    dimId = DimTabb[DimStyleName];
                }
                DimStyleTableRecord DimTabbRecaord = (DimStyleTableRecord)tr.GetObject(dimId, OpenMode.ForRead);
                if (DimTabbRecaord.ObjectId != db.Dimstyle)
                {
                    db.Dimstyle = DimTabbRecaord.ObjectId;
                    db.SetDimstyleData(DimTabbRecaord);
                }

                tr.Commit();
                return dimId;
            }
        }
        public static string ChangeCaseFirstLetter(string text)
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            if (text.Length == 0)
                Console.WriteLine("Empty String");
            else if (text.Length == 1)
                text = text.ToUpper();
            else
                text = char.ToUpper(text[0]) + text[1..];

            tr.Commit();
            return text;

        }
        public static void CreateDim()
        {
            using (Transaction tr = A.Db.TransactionManager.StartTransaction())
            {
                _ = new UserInput();
                _ = new UtilitiesCAD();
                _ = new UtilitiesC3D();
                BlockTable bt = (BlockTable)tr.GetObject(A.Doc.Database.BlockTableId, OpenMode.ForRead);
                _ = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                //start here


                tr.Commit();
            }
        }
        public static void TextLayout(ObjectIdCollection textIdColl, double textHight)
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here

                //get current sacle annotation
                AnnotationScale annoScaleCurrent = A.Db.Cannoscale;
                A.Ok(annoScaleCurrent.DrawingUnits.ToString() + "\n");
                A.Ok(annoScaleCurrent.PaperUnits.ToString() + "\n");
                CCreateLayer("0.TEXT");
                CCreateLayer("0.TEXT_TO");

                //đổi text                   

                foreach (ObjectId textId in textIdColl)
                {
                    if (textId.ObjectClass.DxfName == "TEXT")
                    {
                        DBText? text = tr.GetObject(textId, OpenMode.ForWrite) as DBText;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                        text.Height = textHight * annoScaleCurrent.DrawingUnits;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                        if (textHight > 2)
                        {
                            text.Layer = "0.TEXT_TO";
                        }
                        else text.Layer = "0.TEXT";
                        text.TextString = ChangeCaseFirstLetter(text.TextString);
                    }

                    else if (textId.ObjectClass.DxfName == "MTEXT")
                    {
                        MText? text = tr.GetObject(textId, OpenMode.ForWrite) as MText;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                        text.TextHeight = textHight * annoScaleCurrent.DrawingUnits;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                        if (textHight > 2)
                        {
                            text.Layer = "0.TEXT_TO";
                        }
                        else text.Layer = "0.TEXT";
                        text.Contents = ChangeCaseFirstLetter(text.Contents);
                    }
                }


                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }
        public static void DimLayout(ObjectIdCollection ObjectIdColl)
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here

                //get current sacle annotation
                AnnotationScale annoScaleCurrent = A.Db.Cannoscale;
                A.Ok(annoScaleCurrent.Name.ToString() + "\n");
                A.Ok(annoScaleCurrent.PaperUnits.ToString() + "\n");
                CCreateLayer("1.DIM");
                CreateDimStyle("1-1000");

                //đổi text
                foreach (ObjectId dimensionId in ObjectIdColl)
                {

                    if (dimensionId.ObjectClass.Name == "AcDbAlignedDimension")
                    {
                        AlignedDimension? dimension = tr.GetObject(dimensionId, OpenMode.ForWrite) as AlignedDimension;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                        dimension.Dimscale = annoScaleCurrent.DrawingUnits;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                        dimension.Layer = "1.DIM";
                        dimension.DimensionStyleName = annoScaleCurrent.Name.ToString();
                    }

                    else if (dimensionId.ObjectClass.Name == "AcDb2LineAngularDimension")
                    {
                        LineAngularDimension2? dimension = tr.GetObject(dimensionId, OpenMode.ForWrite) as LineAngularDimension2;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                        dimension.Dimscale = annoScaleCurrent.DrawingUnits;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                        dimension.Layer = "1.DIM";
                        dimension.DimensionStyleName = annoScaleCurrent.Name.ToString();
                    }
                    else if (dimensionId.ObjectClass.Name == "AcDbRotatedDimension")
                    {
                        RotatedDimension? dimension = tr.GetObject(dimensionId, OpenMode.ForWrite) as RotatedDimension;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                        dimension.Dimscale = annoScaleCurrent.DrawingUnits;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                        dimension.Layer = "1.DIM";
                        dimension.DimensionStyleName = annoScaleCurrent.Name.ToString();
                    }
                    else if (dimensionId.ObjectClass.Name == "AcDbArcDimension")
                    {
                        ArcDimension? dimension = tr.GetObject(dimensionId, OpenMode.ForWrite) as ArcDimension;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                        dimension.Dimscale = annoScaleCurrent.DrawingUnits;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                        dimension.Layer = "1.DIM";
                        dimension.DimensionStyleName = annoScaleCurrent.Name.ToString();
                    }
                    else if (dimensionId.ObjectClass.Name == "AcDbRadialDimension")
                    {
                        RadialDimension? dimension = tr.GetObject(dimensionId, OpenMode.ForWrite) as RadialDimension;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                        dimension.Dimscale = annoScaleCurrent.DrawingUnits;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                        dimension.Layer = "1.DIM";
                        dimension.DimensionStyleName = annoScaleCurrent.Name.ToString();
                    }
                    else if (dimensionId.ObjectClass.Name == "AcDbDiametricDimension")
                    {
                        DiametricDimension? dimension = tr.GetObject(dimensionId, OpenMode.ForWrite) as DiametricDimension;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                        dimension.Dimscale = annoScaleCurrent.DrawingUnits;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                        dimension.Layer = "1.DIM";
                        dimension.DimensionStyleName = annoScaleCurrent.Name.ToString();
                    }
                    else if (dimensionId.ObjectClass.Name == "AcDbLeader")
                    {
                        Leader? dimension = tr.GetObject(dimensionId, OpenMode.ForWrite) as Leader;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                        dimension.Dimscale = annoScaleCurrent.DrawingUnits;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                        dimension.Layer = "1.DIM";
                        dimension.DimensionStyleName = annoScaleCurrent.Name.ToString();
                    }
                }

                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }
        public static void BlockLayout(ObjectIdCollection blockIdColl)
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here

                //get current sacle annotation
                AnnotationScale annoScaleCurrent = A.Db.Cannoscale;


                foreach (ObjectId blockId in blockIdColl)
                {

                    if (blockId.ObjectClass.Name == "AcDbBlockReference")
                    {
                        BlockReference? block = tr.GetObject(blockId, OpenMode.ForWrite) as BlockReference;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                        if (block.Name.Contains("KH"))
                        {
                            Scale3d scale3d = new(annoScaleCurrent.DrawingUnits, annoScaleCurrent.DrawingUnits, 1);
                            block.ScaleFactors = scale3d;
                            block.Layer = "2.BAO BT";
                            A.Ed.Command("ATTSYNC", "Name", block.Name.ToString());
                        }
                        else
                        {
                            block.Layer = "2.BAO BT";
                            A.Ed.Command("ATTSYNC", "Name", block.Name.ToString());
                        }
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                    }

                }


                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }
        public static void LineLayout(ObjectIdCollection lineIdColl)
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here

                //get current sacle annotation
                AnnotationScale annoScaleCurrent = A.Db.Cannoscale;


                foreach (ObjectId lineId in lineIdColl)
                {

                    if (lineId.ObjectClass.Name == "AcDbLine")
                    {
                        Line? line = tr.GetObject(lineId, OpenMode.ForWrite) as Line;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                        line.Layer = "2.BAO BT";
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                    }

                }

                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }
        public static void PolyLineLayout(ObjectIdCollection polyLineIdColl)
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here

                //get current sacle annotation
                AnnotationScale annoScaleCurrent = A.Db.Cannoscale;


                foreach (ObjectId polyLineId in polyLineIdColl)
                {

                    if (polyLineId.ObjectClass.Name == "AcDbPolyline")
                    {
                        Polyline? polyLine = tr.GetObject(polyLineId, OpenMode.ForWrite) as Polyline;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                        polyLine.Layer = "2.BAO BT";
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                    }

                }

                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }
        public static void HatchLayout(ObjectIdCollection hatchIdColl)
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here

                //get current sacle annotation
                AnnotationScale annoScaleCurrent = A.Db.Cannoscale;


                foreach (ObjectId hatchId in hatchIdColl)
                {

                    if (hatchId.ObjectClass.Name == "AcDbHatch")
                    {
                        Hatch? hatch = tr.GetObject(hatchId, OpenMode.ForWrite) as Hatch;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                        hatch.Layer = "7.HATCH";
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                    }

                }

                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }
        public static void LinePolyLineHatchArraytArcSplineLayout(ObjectId objectId)
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here

                //get current sacle annotation
                AnnotationScale annoScaleCurrent = A.Db.Cannoscale;

                if (objectId.ObjectClass.Name == "AcDbHatch")
                {
                    Hatch? hatch = tr.GetObject(objectId, OpenMode.ForWrite) as Hatch;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    hatch.Layer = "7.HATCH";
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                }

                else if (objectId.ObjectClass.Name == "AcDbPolyline")
                {
                    Polyline? polyLine = tr.GetObject(objectId, OpenMode.ForWrite) as Polyline;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    polyLine.Layer = "2.BAO BT";
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                }

                else if (objectId.ObjectClass.Name == "AcDbLine")
                {
                    Line? line = tr.GetObject(objectId, OpenMode.ForWrite) as Line;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    line.Layer = "2.BAO BT";
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                }

                else if (objectId.ObjectClass.Name == "AcDbCircle")
                {
                    Circle? line = tr.GetObject(objectId, OpenMode.ForWrite) as Circle;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    line.Layer = "2.BAO BT";
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                }

                else if (objectId.ObjectClass.Name == "AcDbSpline")
                {
                    Spline? line = tr.GetObject(objectId, OpenMode.ForWrite) as Spline;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    line.Layer = "2.BAO BT";
#pragma warning restore CS8602 // Dereference of a possibly null reference.
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

using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Autodesk.AutoCAD.Runtime;
using Acad = Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;
using ATable = Autodesk.AutoCAD.DatabaseServices.Table;
using AcadDocument = Autodesk.AutoCAD.ApplicationServices.Application;

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
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using Autodesk.AutoCAD.Colors;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using MyFirstProject.Extensions;

// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3DCsharp.AT_TongKichThuoc))]

namespace Civil3DCsharp
{
    public class AT_TongKichThuoc
    {
        [CommandMethod("AT_Tong_Do_Dai_Replace")]
        public static void AT_TongDoDai_Replace()
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here
                ObjectIdCollection dimColl = UserInput.GSelectionSetWithType("Chọn các đối tượng (Dimension) cần tính tổng chiều dài/giá trị đo: \n", "DIMENSION");
                if (dimColl == null || dimColl.Count == 0) return;
                List<String> listMeasurement = [];

                //tạo field data
                string str1 = "%<\\AcObjProp Object(%<\\_ObjId ";
                string str2 = ">%).Measurement \\f \"%lu2\">%";
                string format = "";
                foreach (ObjectId dimId in dimColl)
                {
                    long objIdNum = dimId.OldIdPtr.ToInt64();
                    format = str1 + objIdNum.ToString() + str2;
                    listMeasurement.Add(format);
                }

                //tính tổng data file
                string formatTong = "0";
                string str3 = "%<\\AcExpr (";
                string str4 = ") \\f \" %lu2\">%";
                foreach (String item in listMeasurement)
                {
                    formatTong = formatTong + "+" + item;
                }
                formatTong = str3 + formatTong + str4;
                // vẽ text
                ObjectId textId = UserInput.GTextOrMText("Chọn text/mtext cần nhập nội dung: \n");
                if (textId == ObjectId.Null) return;
                var textObj = tr.GetObject(textId, OpenMode.ForWrite);
                if (textObj is DBText dbText)
                    dbText.TextString = formatTong;
                else if (textObj is MText mText)
                    mText.Contents = formatTong;
                A.Ed.Command("_UPDATEFIELD", textId);

                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }

        [CommandMethod("AT_Tong_Do_Dai_Full")]
        public static void AT_TongDoDai_Full()
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here
                ObjectIdCollection polyLineColl = UserInput.GSelectionSet("Chọn các đối tượng (Polyline/Hatch) cần tính tổng độ dài: \n");
                if (polyLineColl == null || polyLineColl.Count == 0) return;
                List<String> listLengthPolyline = [];

                //tạo field data
                string str1 = "%<\\AcObjProp Object(%<\\_ObjId ";
                string str2 = ">%).Length \\f \"%lu2\">%";
                string format = "";
                foreach (ObjectId polyLineId in polyLineColl)
                {
                    string strId = polyLineId.OldIdPtr.ToString();
                    format = str1 + strId + str2;
                    listLengthPolyline.Add(format);
                }

                //tính tổng data file
                string formatTong = "0";
                string str3 = "%<\\AcExpr (";
                string str4 = ") \\f \" %lu2\">%";
                foreach (String item in listLengthPolyline)
                {
                    formatTong = formatTong + "+" + item;
                }
                String formatTong_Lite = formatTong[2..];
                formatTong = formatTong_Lite + "=" + str3 + formatTong + str4 + "m";
                // vẽ text
                Point3d point = UserInput.GPoint("Chọn vị trí đặt text: \n");
                LayerTableRecord layer = UtilitiesCAD.CCreateLayer("TinhTong");
                UtilitiesCAD.CCreateText2(point, 2, formatTong, "TinhTong", "Standard");
                Clipboard.SetText(formatTong);

                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }

        [CommandMethod("AT_Tong_Do_Dai_Replace_2")]
        public static void AT_TongDoDai_Replace2()
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here
                ObjectIdCollection polyLineColl = UserInput.GSelectionSet("Chọn các đối tượng (Polyline/Hatch) cần tính tổng độ dài: \n");
                if (polyLineColl == null || polyLineColl.Count == 0) return;
                List<String> listLengthPolyline = [];

                //tạo field data
                string str1 = "%<\\AcObjProp Object(%<\\_ObjId ";
                string str2 = ">%).Length \\f \"%lu2\">%";
                string format = "";
                foreach (ObjectId polyLineId in polyLineColl)
                {
                    string strId = polyLineId.OldIdPtr.ToString();
                    format = str1 + strId + str2;
                    listLengthPolyline.Add(format);
                }

                //tính tổng data file
                string formatTong = "0";
                string str3 = "%<\\AcExpr (";
                string str4 = ") \\f \" %lu2\">%";
                foreach (String item in listLengthPolyline)
                {
                    formatTong = formatTong + "+" + item;
                }
                formatTong = str3 + formatTong + str4;
                // vẽ text
                ObjectId textId = UserInput.GTextOrMText("Chọn text/mtext cần nhập nội dung: \n");
                if (textId == ObjectId.Null) return;
                var textObj = tr.GetObject(textId, OpenMode.ForWrite);
                if (textObj is DBText dbText)
                    dbText.TextString = formatTong;
                else if (textObj is MText mText)
                    mText.Contents = formatTong;
                A.Ed.Command("_UPDATEFIELD", textId);

                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }

        [CommandMethod("AT_Tong_Do_Dai_Replace_Cong_Them")]
        public static void AT_TongDoDai_Replace_CongThem()
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here
                ObjectIdCollection polyLineColl = UserInput.GSelectionSet("Chọn các đối tượng (Polyline/Hatch) cần tính tổng độ dài: \n");
                if (polyLineColl == null || polyLineColl.Count == 0) return;
                List<String> listLengthPolyline = [];

                // vẽ text
                ObjectId textId = UserInput.GTextOrMText("Chọn text/mtext cần nhập nội dung: \n");
                if (textId == ObjectId.Null) return;
                var textObj = tr.GetObject(textId, OpenMode.ForWrite);

                //tạo field data
                string str1 = "%<\\AcObjProp Object(%<\\_ObjId ";
                string str2 = ">%).Length \\f \"%lu2\">%";
                string format = "";
                foreach (ObjectId polyLineId in polyLineColl)
                {
                    string strId = polyLineId.OldIdPtr.ToString();
                    format = str1 + strId + str2;
                    listLengthPolyline.Add(format);
                }

                //tính tổng data file
                string existingText = "";
                if (textObj is DBText dbTextRead)
                    existingText = dbTextRead.TextString;
                else if (textObj is MText mTextRead)
                    existingText = mTextRead.Contents;

                string formatTong = existingText;
                string str3 = "%<\\AcExpr (";
                string str4 = ") \\f \" %lu2\">%";
                foreach (String item in listLengthPolyline)
                {
                    formatTong = formatTong + "+" + item;
                }
                formatTong = str3 + formatTong + str4;

                if (textObj is DBText dbTextWrite)
                    dbTextWrite.TextString = formatTong;
                else if (textObj is MText mTextWrite)
                    mTextWrite.Contents = formatTong;
                A.Ed.Command("_UPDATEFIELD", textId);

                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }

        [CommandMethod("AT_Tong_Dien_Tich_Full")]
        public static void ET_TongDienTich_Full()
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here
                ObjectIdCollection polyLineColl = UserInput.GSelectionSet("Chọn các đối tượng (Polyline/Hatch) cần tính tổng diện tích: \n");
                if (polyLineColl == null || polyLineColl.Count == 0) return;
                List<String> listLengthPolyline = [];

                //tạo field data
                string str1 = "%<\\AcObjProp Object(%<\\_ObjId ";
                string str2 = ">%).Area \\f \"%lu2\">%";
                string format = "";
                foreach (ObjectId polyLineId in polyLineColl)
                {
                    string strId = polyLineId.OldIdPtr.ToString();
                    format = str1 + strId + str2;
                    listLengthPolyline.Add(format);
                }

                //tính tổng data file
                string formatTong = "0";
                string str3 = "%<\\AcExpr (";
                string str4 = ") \\f \" %lu2\">%";
                foreach (String item in listLengthPolyline)
                {
                    formatTong = formatTong + "+" + item;
                }
                String formatTong_Lite = formatTong[2..];
                formatTong = formatTong_Lite + "=" + str3 + formatTong + str4 + "m2";
                // vẽ text
                MText text = new();
                Point3d point = UserInput.GPoint("Chọn vị trí đặt text: \n");
                LayerTableRecord layer = UtilitiesCAD.CCreateLayer("TinhTong");
                UtilitiesCAD.CCreateText(point, 2, formatTong, "TinhTong", "Standard");

                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }

        [CommandMethod("AT_Tong_Dien_Tich_Replace")]
        public static void AT_TongDienTich_Replace()
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here
                ObjectIdCollection polyLineColl = UserInput.GSelectionSet("Chọn các đối tượng (Polyline/Hatch) cần tính tổng diện tích: \n");
                if (polyLineColl == null || polyLineColl.Count == 0) return;
                List<String> listLengthPolyline = [];

                //tạo field data
                string str1 = "%<\\AcObjProp Object(%<\\_ObjId ";
                string str2 = ">%).Area \\f \"%lu2\">%";
                string format = "";
                foreach (ObjectId polyLineId in polyLineColl)
                {
                    string strId = polyLineId.OldIdPtr.ToString();
                    format = str1 + strId + str2;
                    listLengthPolyline.Add(format);
                }

                //tính tổng data file
                string formatTong = "0";
                string str3 = "%<\\AcExpr (";
                string str4 = ") \\f \" %lu2\">%";
                foreach (String item in listLengthPolyline)
                {
                    formatTong = formatTong + "+" + item;
                }
                formatTong = str3 + formatTong + str4 + "m2";
                // vẽ text
                ObjectId textId = UserInput.GTextOrMText("Chọn text/mtext cần nhập nội dung: \n");
                if (textId == ObjectId.Null) return;
                var textObj = tr.GetObject(textId, OpenMode.ForWrite);
                if (textObj is DBText dbText)
                    dbText.TextString = formatTong;
                else if (textObj is MText mText)
                    mText.Contents = formatTong;
                A.Ed.Command("_UPDATEFIELD", textId);

                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }

        [CommandMethod("AT_Tong_Dien_Tich_Replace_2")]
        public static void AT_TongDienTich_Replace2()
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here
                ObjectIdCollection polyLineColl = UserInput.GSelectionSet("Chọn các đối tượng (Polyline/Hatch) cần tính tổng diện tích: \n");
                if (polyLineColl == null || polyLineColl.Count == 0) return;
                List<String> listLengthPolyline = [];

                //tạo field data
                string str1 = "%<\\AcObjProp Object(%<\\_ObjId ";
                string str2 = ">%).Area \\f \"%lu2\">%";
                string format = "";
                foreach (ObjectId polyLineId in polyLineColl)
                {
                    string strId = polyLineId.OldIdPtr.ToString();
                    format = str1 + strId + str2;
                    listLengthPolyline.Add(format);
                }

                //tính tổng data file
                string formatTong = "0";
                string str3 = "%<\\AcExpr (";
                string str4 = ") \\f \" %lu2\">%";
                foreach (String item in listLengthPolyline)
                {
                    formatTong = formatTong + "+" + item;
                }
                formatTong = str3 + formatTong + str4;
                // vẽ text
                ObjectId textId = UserInput.GTextOrMText("Chọn text/mtext cần nhập nội dung: \n");
                if (textId == ObjectId.Null) return;
                var textObj = tr.GetObject(textId, OpenMode.ForWrite);
                if (textObj is DBText dbText)
                    dbText.TextString = formatTong;
                else if (textObj is MText mText)
                    mText.Contents = formatTong;
                A.Ed.Command("_UPDATEFIELD", textId);

                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }

        [CommandMethod("AT_Tong_Dien_Tich_Replace_Cong_Them")]
        public static void AT_TongDienTich_Replace_CongThem()
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here
                ObjectIdCollection polyLineColl = UserInput.GSelectionSet("Chọn các đối tượng (Polyline/Hatch) cần tính tổng diện tích: \n");
                if (polyLineColl == null || polyLineColl.Count == 0) return;
                List<String> listLengthPolyline = [];
                // vẽ text
                ObjectId textId = UserInput.GTextOrMText("Chọn text/mtext cần nhập nội dung: \n");
                if (textId == ObjectId.Null) return;
                var textObj = tr.GetObject(textId, OpenMode.ForWrite);

                //tạo field data
                string str1 = "%<\\AcObjProp Object(%<\\_ObjId ";
                string str2 = ">%).Area \\f \"%lu2\">%";
                string format = "";
                foreach (ObjectId polyLineId in polyLineColl)
                {
                    string strId = polyLineId.OldIdPtr.ToString();
                    format = str1 + strId + str2;
                    listLengthPolyline.Add(format);
                }

                //tính tổng data file
                string existingText = "";
                if (textObj is DBText dbTextRead)
                    existingText = dbTextRead.TextString;
                else if (textObj is MText mTextRead)
                    existingText = mTextRead.Contents;

                string formatTong = existingText;
                string str3 = "%<\\AcExpr (";
                string str4 = ") \\f \" %lu2\">%";
                foreach (String item in listLengthPolyline)
                {
                    formatTong = formatTong + "+" + item;
                }
                formatTong = str3 + formatTong + str4;

                if (textObj is DBText dbTextWrite)
                    dbTextWrite.TextString = formatTong;
                else if (textObj is MText mTextWrite)
                    mTextWrite.Contents = formatTong;
                A.Ed.Command("_UPDATEFIELD", textId);

                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }
    }
}

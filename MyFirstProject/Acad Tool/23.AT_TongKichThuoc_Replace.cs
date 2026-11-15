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
            if (!TryReadTotalsInput(doc => new AT_Tong_Do_Dai_ReplaceForm(doc),
                out var document, out var sourceIds, out var targetId, out var selectedPoint)) return;
            // start transantion
            using Transaction tr = document.Database.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here
                ObjectIdCollection dimColl = new(sourceIds);
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
                ObjectId textId = targetId;
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
            if (!TryReadTotalsInput(doc => new AT_Tong_Do_Dai_FullForm(doc),
                out var document, out var sourceIds, out var targetId, out var selectedPoint)) return;
            // start transantion
            using Transaction tr = document.Database.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here
                ObjectIdCollection polyLineColl = new(sourceIds);
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
                Point3d point = selectedPoint;
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
            if (!TryReadTotalsInput(doc => new AT_Tong_Do_Dai_Replace_2Form(doc),
                out var document, out var sourceIds, out var targetId, out var selectedPoint)) return;
            // start transantion
            using Transaction tr = document.Database.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here
                ObjectIdCollection polyLineColl = new(sourceIds);
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
                ObjectId textId = targetId;
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
            if (!TryReadTotalsInput(doc => new AT_Tong_Do_Dai_Replace_Cong_ThemForm(doc),
                out var document, out var sourceIds, out var targetId, out var selectedPoint)) return;
            // start transantion
            using Transaction tr = document.Database.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here
                ObjectIdCollection polyLineColl = new(sourceIds);
                if (polyLineColl == null || polyLineColl.Count == 0) return;
                List<String> listLengthPolyline = [];

                // vẽ text
                ObjectId textId = targetId;
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
            if (!TryReadTotalsInput(doc => new AT_Tong_Dien_Tich_FullForm(doc),
                out var document, out var sourceIds, out var targetId, out var selectedPoint)) return;
            // start transantion
            using Transaction tr = document.Database.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here
                ObjectIdCollection polyLineColl = new(sourceIds);
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
                Point3d point = selectedPoint;
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
            if (!TryReadTotalsInput(doc => new AT_Tong_Dien_Tich_ReplaceForm(doc),
                out var document, out var sourceIds, out var targetId, out var selectedPoint)) return;
            // start transantion
            using Transaction tr = document.Database.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here
                ObjectIdCollection polyLineColl = new(sourceIds);
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
                ObjectId textId = targetId;
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
            if (!TryReadTotalsInput(doc => new AT_Tong_Dien_Tich_Replace_2Form(doc),
                out var document, out var sourceIds, out var targetId, out var selectedPoint)) return;
            // start transantion
            using Transaction tr = document.Database.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here
                ObjectIdCollection polyLineColl = new(sourceIds);
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
                ObjectId textId = targetId;
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
            if (!TryReadTotalsInput(doc => new AT_Tong_Dien_Tich_Replace_Cong_ThemForm(doc),
                out var document, out var sourceIds, out var targetId, out var selectedPoint)) return;
            // start transantion
            using Transaction tr = document.Database.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here
                ObjectIdCollection polyLineColl = new(sourceIds);
                if (polyLineColl == null || polyLineColl.Count == 0) return;
                List<String> listLengthPolyline = [];
                // vẽ text
                ObjectId textId = targetId;
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
        /// <summary>Collects validated modal inputs without extending the calculation catch scope.</summary>
        private static bool TryReadTotalsInput(
            Func<Document, MyFirstProject.Shared.UI.CadSelectionForm> createForm,
            out Document document, out ObjectId[] sourceIds, out ObjectId targetId, out Point3d point)
        {
            document = Application.DocumentManager.MdiActiveDocument;
            sourceIds = [];
            targetId = ObjectId.Null;
            point = default;
            if (document == null) return false;
            try
            {
                using var form = createForm(document);
                if (Application.ShowModalDialog(form) != DialogResult.OK || !form.ValidateInputs()) return false;
                sourceIds = form.SelectedIds;
                targetId = form.TargetId;
                point = form.SelectedPoint;
                return true;
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                document.Editor.WriteMessage(e.Message);
                return false;
            }
        }
    }
}

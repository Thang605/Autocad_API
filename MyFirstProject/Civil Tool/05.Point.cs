using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using System.Windows.Forms;
using MyFirstProject.Extensions;
// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3DCsharp.Points))]

namespace Civil3DCsharp
{
    class Points
    {
        
        [CommandMethod("CT_Tao_Cogo_Point_Cao_Do_Tu_Surface")]
        public static void CTPO_TaoCogoPoint_CaoDo_FromSurface()
        {
            // start transantion
            _ = new            // start transantion
            UserInput();
            _ = new UtilitiesCAD();
            _ = new UtilitiesC3D();

            UtilitiesC3D.SetDefaultPointSetting("CDTN", "CDTN");
            ObjectId civSurface = UserInput.GSurfaceId("\n Chọn mặt phẳng cần lấy cao độ CogoPoint nền nhà:");

            string mota = "TN";
            String i = "Enter";
            while (i == "Enter")
            {
                using (Transaction tr = A.Db.TransactionManager.StartTransaction())
                {
                try
                    {
                        Point3d point3D = UserInput.GPoint("\n Chọn vị trí điểm" + " cần phát sinh điểm CogoPoint :\n");
                        CivSurface? surface = tr.GetObject(civSurface, OpenMode.ForRead) as CivSurface;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                        double elevation = surface.FindElevationAtXY(point3D.X, point3D.Y);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                        Point3d newpoint = new(point3D.X, point3D.Y, elevation);
                        UtilitiesC3D.CreateCogoPointFromPoint3D(newpoint, mota);

                        tr.Commit();
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception e)
                    {
                        A.Ed.WriteMessage(e.Message);
                    }
                }
                i = UserInput.GStopWithESC();
            }
        }
        
        [CommandMethod("CT_Tao_Cogo_Point_Tu_Elevation_Spot")]
        public static void TaoCogoPoint_Elevationspot()
        {
            // start transantion
            _ = new            // start transantion
            UserInput();
            _ = new UtilitiesCAD();
            _ = new UtilitiesC3D();

            UtilitiesC3D.SetDefaultPointSetting("CDTN", "CDTN");

            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                ObjectId civSurface = UserInput.GSurfaceId("\n Chọn mặt phẳng " + " cần lấy cao độ cho CogoPoint:");
                CivSurface? surface = tr.GetObject(civSurface, OpenMode.ForRead) as CivSurface;
                ObjectIdCollection objectIdCollection = SurfaceElevationLabel.GetAvailableSurfaceElevationLabelIds(civSurface);

                // create point group
                string pointGroupName = "TN-BS";
                PointGroup pointGroup = UtilitiesC3D.CPointGroupWithDecription(pointGroupName, pointGroupName);

                //create point
                foreach (ObjectId objectId in objectIdCollection)
                {
                    SurfaceElevationLabel? surfaceElevationLabel = tr.GetObject(objectId, OpenMode.ForWrite) as SurfaceElevationLabel;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    double elevation = surface.FindElevationAtXY(surfaceElevationLabel.Location.X, surfaceElevationLabel.Location.Y);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                    Point3d point3D = new(surfaceElevationLabel.Location.X, surfaceElevationLabel.Location.Y, elevation);
                    UtilitiesC3D.CreateCogoPointFromPoint3D(point3D, pointGroupName);
                    surfaceElevationLabel.Erase();

                }
                pointGroup.Update();
                pointGroup.ApplyDescriptionKeys();
                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }

        }

        [CommandMethod("CT_Update_All_Point_Group")]
        public static void CTP_UpdateAllPointGroup()
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here
                UtilitiesC3D.UpdateAllPointGroup();

                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }

        [CommandMethod("CT_Tao_Cogo_Point_Tu_Text")]
        public static void CTP_CreateCogopointFromText()
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here
                ObjectIdCollection textIdColl = UserInput.GSelectionSetWithType("\n Chọn các đối tượng text cần tạo cogo point: ", "TEXT");
                foreach (ObjectId textId in textIdColl)
                {
                    DBText? text = tr.GetObject(textId, OpenMode.ForWrite) as DBText;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    Point3d alignmentPoint = text.AlignmentPoint;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                    Point3d position = text.Position;

                    //set position và text left
                    if ((text.Justify == AttachmentPoint.BaseLeft) || (text.Justify == AttachmentPoint.BaseAlign) || (text.Justify == AttachmentPoint.BaseFit))
                    {

                        text.Justify = AttachmentPoint.BaseLeft;
                    }

                    else
                    {
                        text.Position = alignmentPoint;
                        text.Justify = AttachmentPoint.BaseLeft;
                    }

                    //lấy tọa độ cogopoint
                    Point3d cogoText = text.Position;
                    if (double.TryParse(text.TextString, out double Z))
                    {
                        Point3d cogoPoint = new(cogoText.X, cogoText.Y, Z);
                        UtilitiesC3D.CreateCogoPointFromPoint3D(cogoPoint, "TN");
                        text.Erase();
                    }


                }

                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }

        [CommandMethod("CT_An_Cogo_Point")]
        public static void CTP_An_CogoPoint()
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                //start here
                ObjectIdCollection pointColl = UserInput.GSelectionSet("\n Chọn các cogo point cần ẩn: ");

                // create point group
                string pointGroupName = "HidePoint";
                PointGroup pointGroup = UtilitiesC3D.CPointGroupWithDecription(pointGroupName, pointGroupName);

                foreach (ObjectId pointId in pointColl)
                {
                    if (pointId.ObjectClass.Name == "AeccDbCogoPoint")
                    {
                        CogoPoint? cogoPoint = tr.GetObject(pointId, OpenMode.ForWrite) as CogoPoint;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                        cogoPoint.RawDescription = pointGroupName;
#pragma warning restore CS8602 // Dereference of a possibly null reference.

                    }

                }
                pointGroup.Update();
                pointGroup.ApplyDescriptionKeys();

                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }









    } 
}

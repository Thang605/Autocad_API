using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using MyFirstProject.Extensions;
// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3DCsharp.Surfaces))]

namespace Civil3DCsharp
{
    public class Surfaces
    {        
        [CommandMethod("CTT_Tao_Spot_Elevation_Tai_Tim")]
        public static void CTSTaoSpotElevationOnSurfaceTaiTim()
        {
            // start transantion
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput uI = new();
                UtilitiesCAD uti = new();
                UtilitiesC3D C3D = new();

                //start here

                // get an alignment
                ObjectId surfaceId = UserInput.GSurfaceId("\n Chọn mặt phẳng " + "để phát sinh cao độ thiết kế trên bình đồ: ");
                //get the  samplelineGroup
                ObjectId sampleLineId = UserInput.GSampleLineId("\n Chọn 1 cọc thuộc nhóm cọc cần phát sinh cao độ thiết kế trên bình đồ:");
                SampleLine? sampleLine = tr.GetObject(sampleLineId, OpenMode.ForWrite) as SampleLine;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                ObjectId sampleLineGroupId = sampleLine.GroupId;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                SampleLineGroup? sampleLineGroup = tr.GetObject(sampleLineGroupId, OpenMode.ForRead) as SampleLineGroup;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                A.Ed.WriteMessage(sampleLineGroup.Name);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                ObjectId alignmentId = A.ParentAlignmentId(sampleLine);
                Alignment? alignment = tr.GetObject(alignmentId, OpenMode.ForWrite) as Alignment;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                A.Ed.WriteMessage(alignment.Name);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                //check number of sampleline in samplelinegroup
                int numberSampleline = 1;
                foreach (ObjectId item in sampleLineGroup.GetSampleLineIds())
                {
                    numberSampleline++;
                }

                // get coordinate and name of point
                double easting = 0;
                double northing = 0;
                ObjectId spotElevationLabelStylesId = A.Cdoc.Styles.LabelStyles.SurfaceLabelStyles.SpotElevationLabelStyles["CĐTK (BĐ)"];
                ObjectId markStyleId = A.Cdoc.Styles.MarkerStyles["_No Markers"];

                foreach (ObjectId item in sampleLineGroup.GetSampleLineIds())
                {
                    SampleLine? sampleline = tr.GetObject(item, OpenMode.ForWrite) as SampleLine;

#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    Double station = sampleline.Station;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                    A.Ed.WriteMessage(station + "\n");
                    alignment.PointLocation(station, 0, ref easting, ref northing);
                    Point2d point2D = new(easting, northing);
                    A.Ed.WriteMessage(point2D.X + "-" + point2D.Y);
                    // create the spot elevation using SurfaceElevationLabel.Create
                    ObjectId surfaceElevnLblId = SurfaceElevationLabel.Create(surfaceId, point2D, spotElevationLabelStylesId, markStyleId);
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

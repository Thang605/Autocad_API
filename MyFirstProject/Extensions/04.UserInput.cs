using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

using ATable = Autodesk.AutoCAD.DatabaseServices.Table;
//system
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Civil.DatabaseServices;
using CivSurface = Autodesk.Civil.DatabaseServices.TinSurface;
using Section = Autodesk.Civil.DatabaseServices.Section;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;

namespace MyFirstProject.Extensions
{
    public class UserInput
    {
        // Get string from user
        public static string? GString(string FromUser)
        {
            PromptStringOptions pso = new(FromUser)
            {
                AllowSpaces = true
            };
            PromptResult Rpso = A.Ed.GetString(pso);
            if (Rpso.Status == PromptStatus.OK)
                return Rpso.StringResult;
            else return null;
        }

        // Get Enter/Cancel from user
        public static string GStopWithESC()
        {
            PromptStringOptions pso = new("\n Nhấn phím bất kì để tiếp tục / Nhấm ESC to dừng lại")
            {
                AllowSpaces = true
            };
            PromptResult Rpso = A.Ed.GetString(pso);
            string st = "Cancel";
            string st2 = "Enter";
            if (Rpso.Status == PromptStatus.Cancel)
                return st;
            else return st2;
        }

        // Get Enter/Cancel from user
        public static string? GYesNo2(string thongbao)
        {
            PromptStringOptions pso = new(thongbao)
            {
                AllowSpaces = true
            };
            PromptResult Rpso = A.Ed.GetString(pso);

            if (Rpso.Status == PromptStatus.OK)
                return Rpso.StringResult;
            else return null;
        }

        // get integer from user
        public static int GInt(string thongbao)
        {
            PromptIntegerOptions pio = new("\n" + thongbao);
            PromptIntegerResult Rpio = A.Ed.GetInteger(pio);
            if (Rpio.Status == PromptStatus.OK)
                return Rpio.Value;

            return Rpio.Value;
        }

        // get integer from user
        public static double GDouble(string thongbao)
        {
            PromptDoubleOptions pio = new(thongbao);
            PromptDoubleResult Rpio = A.Ed.GetDouble(pio);
            if (Rpio.Status == PromptStatus.OK)
                return Rpio.Value;

            return Rpio.Value;
        }

        // get point from user
        public static Point3d GPoint(string thongbao)
        {
            Point3d p3d = new();
            PromptPointOptions ppo = new(thongbao);
            PromptPointResult Rppo = A.Ed.GetPoint(ppo);
            if (Rppo.Status == PromptStatus.OK)
                p3d = Rppo.Value;
            return p3d;
        }

        // get alignment from user
        public static ObjectId GAlignmentId(string thongbao)
        {
            PromptEntityOptions peo = new(thongbao);
            peo.SetRejectMessage("\n- Bạn phải chọn đúng đối tượng Alignment!");
            peo.AddAllowedClass(typeof(Alignment), true);
            PromptEntityResult Rpeo = A.Ed.GetEntity(peo);
            if (Rpeo.Status != PromptStatus.OK)
            {
                return ObjectId.Null;
            }
            return Rpeo.ObjectId;
        }

        //get cogo point
        public static ObjectId GCogoPointId(string thongbao)
        {
            PromptEntityOptions peo = new(thongbao);
            peo.SetRejectMessage("\n- Bạn phải chọn đúng đối tượng CogoPoint!");
            peo.AddAllowedClass(typeof(CogoPoint), true);
            PromptEntityResult Rpeo = A.Ed.GetEntity(peo);
            if (Rpeo.Status != PromptStatus.OK)
            {
                return ObjectId.Null;
            }
            return Rpeo.ObjectId;
        }

        // get sampleline from user
        public static ObjectId GSampleLineId(string thongbao)
        {
            PromptEntityOptions peo = new(thongbao);
            peo.SetRejectMessage("\n- Bạn phải chọn đúng đối tượng SampleLine!");
            peo.AddAllowedClass(typeof(SampleLine), true);
            PromptEntityResult Rpeo = A.Ed.GetEntity(peo);
            if (Rpeo.Status != PromptStatus.OK)
            {
                return ObjectId.Null;
            }
            return Rpeo.ObjectId;
        }

        // get surface from user
        public static ObjectId GSurfaceId(string thongbao)
        {
            PromptEntityOptions peo = new("\n " + thongbao);
            peo.SetRejectMessage("\n Bạn phải chọn đúng đối tượng Surface!");
            peo.AddAllowedClass(typeof(CivSurface), true);
            PromptEntityResult Rpeo = A.Ed.GetEntity(peo);
            if (Rpeo.Status != PromptStatus.OK)
            {
                return ObjectId.Null;
            };
            return Rpeo.ObjectId;
        }

        // get profile from user
        public static ObjectId GProfileId(string thongbao)
        {
            PromptEntityOptions peo = new(thongbao);
            peo.SetRejectMessage("\n- Bạn phải chọn đúng đối tượng Profile!");
            peo.AddAllowedClass(typeof(Profile), true);
            PromptEntityResult Rpeo = A.Ed.GetEntity(peo);
            if (Rpeo.Status != PromptStatus.OK)
            {
                return ObjectId.Null;
            }
            return Rpeo.ObjectId;
        }

        // get corrodor from user
        public static ObjectId GCorridorId(string thongbao)
        {
            PromptEntityOptions peo = new(thongbao);
            peo.SetRejectMessage("\n- Bạn phải chọn đúng đối tượng Corridor!");
            peo.AddAllowedClass(typeof(Corridor), true);
            PromptEntityResult Rpeo = A.Ed.GetEntity(peo);
            if (Rpeo.Status != PromptStatus.OK)
            {
                return ObjectId.Null;
            }
            return Rpeo.ObjectId;
        }
        // get g_Baseline from user
        public static ObjectId GBaselineId(string thongbao)
        {
            PromptEntityOptions peo = new(thongbao);
            peo.SetRejectMessage("\n- Bạn phải chọn đúng đối tượng Baseline!");
            peo.AddAllowedClass(typeof(Baseline), true);
            PromptEntityResult Rpeo = A.Ed.GetEntity(peo);
            if (Rpeo.Status != PromptStatus.OK)
            {
                return ObjectId.Null;
            }

            return Rpeo.ObjectId;
        }
        // get BaselineRegion from user
        public static ObjectId GBaselineRegionId(string thongbao)
        {
            PromptEntityOptions peo = new(thongbao);
            peo.SetRejectMessage("\n- Bạn phải chọn đúng đối tượng BaselineRegion!");
            peo.AddAllowedClass(typeof(BaselineRegion), true);
            PromptEntityResult Rpeo = A.Ed.GetEntity(peo);
            if (Rpeo.Status != PromptStatus.OK)
            {
                return ObjectId.Null;
            }

            return Rpeo.ObjectId;
        }

        // get Polyline from user
        public static ObjectId GPolyline(string thongbao)
        {
            PromptEntityOptions peo = new(thongbao);
            peo.SetRejectMessage("\n- Bạn phải chọn đúng đối tượng Polyline!");
            peo.AddAllowedClass(typeof(Polyline), true);
            PromptEntityResult Rpeo = A.Ed.GetEntity(peo);
            if (Rpeo.Status != PromptStatus.OK)
            {
                return ObjectId.Null;
            }

            return Rpeo.ObjectId;
        }

        // get table from user
        public static ObjectId GTable(string thongbao)
        {
            PromptEntityOptions peo = new(thongbao);
            peo.SetRejectMessage("\n- Bạn phải chọn đúng đối tượng Table!");
            peo.AddAllowedClass(typeof(ATable), true);
            PromptEntityResult Rpeo = A.Ed.GetEntity(peo);
            if (Rpeo.Status != PromptStatus.OK)
            {
                return ObjectId.Null;
            }

            return Rpeo.ObjectId;
        }

        public static ObjectIdCollection GSelectCrossingWindow(Point3d point1, Point3d point2, string typeObject)
        {

            // Create a TypedValue array to define the filter criteria
            TypedValue[] acTypValAr = new TypedValue[1];
            acTypValAr.SetValue(new TypedValue((int)DxfCode.Start, typeObject), 0);
            PromptSelectionResult promptSelectionResult = A.Ed.SelectCrossingWindow(point1, point2, new SelectionFilter(acTypValAr));
            SelectionSet selectionSet = promptSelectionResult.Value;
            ObjectIdCollection objectIdCollection = [.. selectionSet.GetObjectIds()];
            return objectIdCollection;
        }


        public static ObjectIdCollection GSelectionSetWithType(string thongbao, string typeObject)
        {
            // Create a TypedValue array to define the filter criteria

            A.Ok(thongbao);
            TypedValue[] acTypValAr = new TypedValue[1];
            acTypValAr.SetValue(new TypedValue((int)DxfCode.Start, typeObject), 0);
            PromptSelectionResult promptSelectionResult = A.Ed.GetSelection(new SelectionFilter(acTypValAr));
            if (promptSelectionResult.Status != PromptStatus.OK || promptSelectionResult.Value == null)
                return null;
            SelectionSet selectionSet = promptSelectionResult.Value;
            ObjectIdCollection objectIdCollection = [.. selectionSet.GetObjectIds()];
            return objectIdCollection;
        }

        public static ObjectIdCollection GSelectionSet(string thongbao)
        {
            PromptSelectionResult promptSelectionResult = A.Ed.GetSelection();
            A.Ok(thongbao);
            ObjectIdCollection objectIdCollection = [];
            if (promptSelectionResult.Status == PromptStatus.OK)
            {
                for (int i = 0; i < promptSelectionResult.Value.Count; i++)
                {
                    objectIdCollection.Add(promptSelectionResult.Value.GetObjectIds()[i]);
                }
            }
            return objectIdCollection;
        }

        public static ObjectId GProfileViewId(string thongbao)
        {
            PromptEntityOptions peo = new(thongbao);
            peo.SetRejectMessage("\n- Bạn phải chọn đúng đối tượng ProfileView!");
            peo.AddAllowedClass(typeof(ProfileView), true);
            PromptEntityResult Rpeo = A.Ed.GetEntity(peo);
            
            // Return ObjectId.Null if user cancels or selection fails
            if (Rpeo.Status != PromptStatus.OK)
            {
                return ObjectId.Null;
            }

            return Rpeo.ObjectId;
        }

        public static ObjectId GSection(string thongbao)
        {
            PromptEntityOptions peo = new(thongbao);
            peo.SetRejectMessage("\n- Bạn phải chọn đúng đối tượng Section!");
            peo.AddAllowedClass(typeof(Section), true);
            PromptEntityResult Rpeo = A.Ed.GetEntity(peo);
            if (Rpeo.Status != PromptStatus.OK)
            {
                return ObjectId.Null;
            }

            return Rpeo.ObjectId;
        }

        public static ObjectId GSectionView(string thongbao)
        {
            PromptEntityOptions peo = new("\n" + thongbao);
            peo.SetRejectMessage("\n- Bạn phải chọn đúng đối tượng SectionView!");
            peo.AddAllowedClass(typeof(SectionView), true);
            PromptEntityResult Rpeo = A.Ed.GetEntity(peo);
            if (Rpeo.Status != PromptStatus.OK)
            {
                return ObjectId.Null;
            }

            return Rpeo.ObjectId;
        }

        public static ObjectId GProfileProjection(string thongbao)
        {
            PromptEntityOptions peo = new(thongbao);
            peo.SetRejectMessage("\n- Bạn phải chọn đúng đối tượng ProfileProjection!");
            peo.AddAllowedClass(typeof(ProfileProjection), true);
            PromptEntityResult Rpeo = A.Ed.GetEntity(peo);
            if (Rpeo.Status != PromptStatus.OK)
            {
                return ObjectId.Null;
            }

            return Rpeo.ObjectId;
        }

        public static ObjectId[] ConvertObjectIdCollectionToArray(ObjectIdCollection objectIdCollection)
        {
            ObjectId[] objectIds = new ObjectId[objectIdCollection.Count];
            for (int i = 0; i < objectIdCollection.Count; i++)
            {
                objectIds[i] = objectIdCollection[i];
            }
            return objectIds;
        }

        public static ObjectId GSelectionAnObject(string thongbao)
        {
            PromptEntityOptions peo = new(thongbao);
            PromptEntityResult Rpeo = A.Ed.GetEntity(peo);
            if (Rpeo.Status != PromptStatus.OK)
            {
                return ObjectId.Null;
            }

            return Rpeo.ObjectId;
        }

        public static ObjectId GDbText(string thongbao)
        {
            PromptEntityOptions peo = new(thongbao);
            peo.SetRejectMessage("\n- Bạn phải chọn đúng đối tượng DBText!");
            peo.AddAllowedClass(typeof(DBText), true);
            PromptEntityResult Rpeo = A.Ed.GetEntity(peo);
            if (Rpeo.Status != PromptStatus.OK)
            {
                return ObjectId.Null;
            }

            return Rpeo.ObjectId;
        }

        public static ObjectId GTextOrMText(string thongbao)
        {
            PromptEntityOptions peo = new(thongbao);
            peo.SetRejectMessage("\n- Bạn phải chọn đúng đối tượng Text hoặc MText!");
            peo.AddAllowedClass(typeof(DBText), true);
            peo.AddAllowedClass(typeof(MText), true);
            PromptEntityResult Rpeo = A.Ed.GetEntity(peo);
            if (Rpeo.Status != PromptStatus.OK)
            {
                return ObjectId.Null;
            }

            return Rpeo.ObjectId;
        }

        public static ObjectId GObjId(string thongbao)
        {
            PromptEntityOptions peo = new(thongbao);
            peo.SetRejectMessage("\n- Bạn phải chọn đúng đối tượng!");
            //peo.AddAllowedClass(typeof(Polyline), true);
            PromptEntityResult Rpeo = A.Ed.GetEntity(peo);
            if (Rpeo.Status != PromptStatus.OK)
            {
                return ObjectId.Null;
            }

            return Rpeo.ObjectId;
        }


        public static void check()
        {
            using (Transaction tr = A.Db.TransactionManager.StartTransaction())
            {
                _ = new UserInput();
                _ = new UtilitiesCAD();
                _ = new UtilitiesC3D();
                //start here
                _ = GString("\n Code ok ở đây (Stop/Continous)?");

                tr.Commit();
            }
        }



































    }




















}

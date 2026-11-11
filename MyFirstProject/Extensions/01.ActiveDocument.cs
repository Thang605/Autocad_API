using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Autodesk.AutoCAD.Runtime;
using Cad = Autodesk.AutoCAD.ApplicationServices;
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
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MyFirstProject.Extensions
{
    public static class A
    {
        public static Document Doc
        {
            get { return Cad.Core.Application.DocumentManager.MdiActiveDocument; }
        }
        public static Editor Ed
        {
            get { return Doc.Editor; }
        }
        public static Database Db
        {
            get { return Doc.Database; }
        }
        public static CivilDocument Cdoc
        {
            get { return CivilApplication.ActiveDocument; }
        }
        public static ObjectId ParentAlignmentId(this SampleLine sl)
        {
            ObjectId algnId = ObjectId.Null;
            foreach (ObjectId id in CivilApplication.ActiveDocument.GetAlignmentIds())
            {
                Alignment algn = (Alignment)id.GetObject(OpenMode.ForRead);
                foreach (ObjectId slgId in algn.GetSampleLineGroupIds())
                {
                    if (slgId == sl.GroupId)
                    {
                        algnId = id;
                        break;
                    }
                }
                if (algnId != ObjectId.Null)
                    break;
            }
            return algnId;
        }

        public static void Ok(string thongbao)
        {
            Ed.WriteMessage(thongbao + "\n");
        }

        public static void OkHere()
        {
            Ed.WriteMessage("OK HERE \n");
        }

        public static SectionViewGroup SectionViewGroupObject(this SectionView sv)
        {
            using Transaction tr = Db.TransactionManager.StartTransaction();
            SectionViewGroup? result = null;
            ObjectId sampleLineId = sv.SampleLineId;
            if (sampleLineId.IsValid)
            {
                SampleLine? sl = tr.GetObject(sampleLineId, OpenMode.ForRead) as SampleLine;
                SampleLineGroup? slg = sl != null
                    ? tr.GetObject(sl.GroupId, OpenMode.ForRead) as SampleLineGroup
                    : tr.GetObject(sampleLineId, OpenMode.ForRead) as SampleLineGroup;
                if (slg != null)
                {
                    foreach (SectionViewGroup svg in slg.SectionViewGroups)
                    {
                        if (svg.GetSectionViewIds().Contains(sv.ObjectId))
                        {
                            result = svg;
                            break;
                        }
                    }
                }
            }
            tr.Commit();
#pragma warning disable CS8603 // Possible null reference return.
            return result;
#pragma warning restore CS8603 // Possible null reference return.
        }





    }
}

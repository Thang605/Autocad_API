using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using CadEntity = Autodesk.AutoCAD.DatabaseServices.Entity;
using CivSurface = Autodesk.Civil.DatabaseServices.TinSurface;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;
using DocumentCollectionEventArgs = Autodesk.AutoCAD.ApplicationServices.DocumentCollectionEventArgs;

namespace MyFirstProject.Civil_Tool_2
{
    public partial class CTT_Tao_Mat_Phang_Tu_Featureline_Command
    {
        internal static SourceCurveInfo? ExtractSourceCurveInfo(
            ObjectId entId,
            Transaction tr,
            double midOrdDistance,
            double autoCloseTol,
            bool allowAutoClose,
            int curveIndex)
        {
            if (entId.IsNull || !entId.IsValid || entId.IsErased) return null;

            CadEntity? ent = tr.GetObject(entId, OpenMode.ForRead) as CadEntity;
            if (ent == null) return null;

            var info = new SourceCurveInfo
            {
                CurveIndex = curveIndex,
                Id = entId
            };

            double sampleMidOrd = Math.Max(0.008, midOrdDistance * 0.25);
            List<Point3d> pts = new List<Point3d>();

            if (ent is AutoCorridorFeatureLine acfl)
            {
                info.EntityTypeName = "Corridor FL";
                string code = string.Empty;
                string corr = string.Empty;
                try { code = acfl.CorridorFeaturelineCode ?? ""; } catch { }
                try { corr = acfl.CorridorName ?? ""; } catch { }
                info.DisplayName = !string.IsNullOrWhiteSpace(code)
                    ? (!string.IsNullOrWhiteSpace(corr) ? $"{code} ({corr})" : code)
                    : (acfl.Name ?? acfl.Handle.ToString());
                info.IsClosed = SafeGetClosed(acfl);
                try { info.HasTrueArcs = acfl.CurvesCount > 0; } catch { }
                pts = ExtractFeatureLine3DPoints(acfl, sampleMidOrd);
            }
            else if (ent is FeatureLine fl)
            {
                info.EntityTypeName = "FeatureLine";
                info.DisplayName = !string.IsNullOrWhiteSpace(fl.Name) ? fl.Name : $"FL_{fl.Handle}";
                info.IsClosed = SafeGetClosed(fl);
                try { info.HasTrueArcs = fl.CurvesCount > 0; } catch { }
                pts = ExtractFeatureLine3DPoints(fl, sampleMidOrd);
            }
            else if (ent is Polyline pl)
            {
                info.EntityTypeName = "2D Polyline";
                info.DisplayName = $"PL_{pl.Handle}";
                info.IsClosed = pl.Closed;
                info.HasTrueArcs = pl.HasBulges;
                pts = SampleLwPolyline3D(pl, sampleMidOrd);
            }
            else if (ent is Polyline3d p3d)
            {
                info.EntityTypeName = "Polyline3D";
                info.DisplayName = $"3DP_{p3d.Handle}";
                info.IsClosed = p3d.Closed;
                foreach (ObjectId vId in p3d)
                {
                    if (tr.GetObject(vId, OpenMode.ForRead) is PolylineVertex3d v)
                    {
                        pts.Add(v.Position);
                    }
                }
            }
            else if (ent is Polyline2d p2d)
            {
                info.EntityTypeName = "Polyline2D";
                info.DisplayName = $"P2D_{p2d.Handle}";
                info.IsClosed = p2d.Closed;
                foreach (ObjectId vId in p2d)
                {
                    if (tr.GetObject(vId, OpenMode.ForRead) is Vertex2d v)
                    {
                        pts.Add(new Point3d(v.Position.X, v.Position.Y, p2d.Elevation));
                    }
                }
            }
            else if (ent is Line ln)
            {
                info.EntityTypeName = "Line";
                info.DisplayName = $"Line_{ln.Handle}";
                info.IsClosed = false;
                pts.Add(ln.StartPoint);
                pts.Add(ln.EndPoint);
            }
            else if (ent is Arc arc)
            {
                info.EntityTypeName = "Arc";
                info.DisplayName = $"Arc_{arc.Handle}";
                info.IsClosed = false;
                SampleArc3D(arc, midOrdDistance, pts);
            }
            else
            {
                return null;
            }

            // Lọc các đỉnh trùng liên tiếp
            List<Point3d> cleaned = new List<Point3d>(pts.Count);
            for (int i = 0; i < pts.Count; i++)
            {
                if (cleaned.Count == 0 || cleaned[cleaned.Count - 1].DistanceTo(pts[i]) > 1e-5)
                {
                    cleaned.Add(pts[i]);
                }
            }

            if (cleaned.Count < 2) return null;

            double gap2D = Math.Sqrt(
                Math.Pow(cleaned[0].X - cleaned[cleaned.Count - 1].X, 2) +
                Math.Pow(cleaned[0].Y - cleaned[cleaned.Count - 1].Y, 2));

            if (info.IsClosed)
            {
                if (gap2D > 1e-4)
                {
                    cleaned.Add(cleaned[0]);
                }
            }
            else if (cleaned.Count >= 3 && (gap2D <= 0.005 || (allowAutoClose && gap2D <= autoCloseTol)))
            {
                info.IsClosed = true;
                if (gap2D > 1e-4)
                {
                    info.WasAutoClosedGap = true;
                    info.RawEndBeforeAutoClose = cleaned[cleaned.Count - 1];
                    cleaned.Add(cleaned[0]);
                }
            }

            info.Points3D = cleaned;
            return info;
        }

        private static bool SafeGetClosed(Curve c)
        {
            try { return c.Closed; }
            catch { return false; }
        }

        private static List<Point3d> ExtractFeatureLine3DPoints(FeatureLine fl, double midOrdDistance)
        {
            List<Point3d> result = new List<Point3d>();

            try
            {
                Point3dCollection allPts = fl.GetPoints(FeatureLinePointType.AllPoints);
                if (allPts != null && allPts.Count >= 2)
                {
                    bool hasCurves = false;
                    try { hasCurves = fl.CurvesCount > 0; } catch { }

                    if (!hasCurves)
                    {
                        foreach (Point3d p in allPts) result.Add(p);
                        if (fl.Closed && result[0].DistanceTo(result[result.Count - 1]) > 1e-4)
                            result.Add(result[0]);
                        return result;
                    }

                    // Nếu có cung cong, lấy mẫu mịn dọc theo Curve giữa các điểm đặc trưng
                    for (int i = 0; i < allPts.Count - 1; i++)
                    {
                        Point3d pA = allPts[i];
                        Point3d pB = allPts[i + 1];
                        result.Add(pA);

                        try
                        {
                            double dA = fl.GetDistAtPoint(pA);
                            double dB = fl.GetDistAtPoint(pB);
                            double spanLen = dB - dA;
                            double chordLen = pA.DistanceTo(pB);
                            if (spanLen > chordLen + 1e-4)
                            {
                                double step = EstimateArcStepFromSagitta(spanLen, chordLen, midOrdDistance);
                                int nSteps = Math.Max(4, (int)Math.Ceiling(spanLen / step));
                                for (int s = 1; s < nSteps; s++)
                                {
                                    result.Add(fl.GetPointAtDist(dA + spanLen * s / nSteps));
                                }
                            }
                        }
                        catch { }
                    }

                    result.Add(allPts[allPts.Count - 1]);

                    if (fl.Closed && result[0].DistanceTo(result[result.Count - 1]) > 1e-4)
                    {
                        result.Add(result[0]);
                    }
                    return result;
                }
            }
            catch { }

            // Fallback: Explode đối tượng tạm trong bộ nhớ và giải phóng (Dispose) triệt để
            try
            {
                using (DBObjectCollection exploded = new DBObjectCollection())
                {
                    fl.Explode(exploded);
                    foreach (DBObject obj in exploded)
                    {
                        try
                        {
                            if (obj is Arc arc)
                                SampleArc3D(arc, midOrdDistance, result);
                            else if (obj is Polyline pl)
                                result.AddRange(SampleLwPolyline3D(pl, midOrdDistance));
                            else if (obj is Line ln)
                            {
                                result.Add(ln.StartPoint);
                                result.Add(ln.EndPoint);
                            }
                            else if (obj is Curve cv)
                            {
                                result.Add(cv.StartPoint);
                                result.Add(cv.EndPoint);
                            }
                        }
                        finally
                        {
                            obj.Dispose();
                        }
                    }
                }
            }
            catch { }

            return result;
        }

        private static List<Point3d> SampleLwPolyline3D(Polyline pl, double midOrdDistance)
        {
            List<Point3d> pts = new List<Point3d>();
            int vn = pl.NumberOfVertices;
            if (vn == 0) return pts;

            int segCount = pl.Closed ? vn : vn - 1;
            for (int i = 0; i < segCount; i++)
            {
                int next = (i + 1) % vn;
                Point3d p0 = pl.GetPoint3dAt(i);
                Point3d p1 = pl.GetPoint3dAt(next);
                pts.Add(p0);

                double bulge = pl.GetBulgeAt(i);
                if (Math.Abs(bulge) > 1e-6)
                {
                    try
                    {
                        CircularArc2d arc2d = pl.GetArcSegment2dAt(i);
                        double r = arc2d.Radius;
                        double angle = 4.0 * Math.Atan(Math.Abs(bulge));
                        double arcLen = r * angle;
                        double sagStep = 2.0 * Math.Sqrt(Math.Max(1e-4, 2.0 * r * midOrdDistance - midOrdDistance * midOrdDistance));
                        int steps = Math.Max(4, (int)Math.Ceiling(arcLen / Math.Max(0.10, sagStep)));
                        Point2d[] samples = arc2d.GetSamplePoints(steps + 1);
                        for (int s = 1; s < samples.Length - 1; s++)
                        {
                            pts.Add(new Point3d(samples[s].X, samples[s].Y, pl.Elevation));
                        }
                    }
                    catch { }
                }

                if (!pl.Closed && i == segCount - 1)
                {
                    pts.Add(p1);
                }
            }

            if (pl.Closed && pts.Count > 0)
            {
                pts.Add(pts[0]);
            }

            return pts;
        }

        private static void SampleArc3D(Arc arc, double midOrdDistance, List<Point3d> outPts)
        {
            double r = arc.Radius;
            double sagStep = 2.0 * Math.Sqrt(Math.Max(1e-4, 2.0 * r * midOrdDistance - midOrdDistance * midOrdDistance));
            int steps = Math.Max(4, (int)Math.Ceiling(arc.Length / Math.Max(0.10, sagStep)));
            double delta = arc.Length / steps;
            for (int i = 0; i <= steps; i++)
            {
                outPts.Add(arc.GetPointAtDist(Math.Min(arc.Length, i * delta)));
            }
        }

        private static double EstimateArcStepFromSagitta(double arcLen, double chordLen, double midOrd)
        {
            if (arcLen <= chordLen + 1e-5) return Math.Max(0.5, arcLen);
            // Xấp xỉ góc tâm theta từ L/c ~ (theta/2)/sin(theta/2) -> theta ~ sqrt(24*(1 - c/L))
            double theta = Math.Sqrt(Math.Max(1e-4, 24.0 * (1.0 - chordLen / arcLen)));
            double radius = arcLen / theta;
            double step = 2.0 * Math.Sqrt(Math.Max(1e-4, 2.0 * radius * midOrd - midOrd * midOrd));
            return Math.Max(0.15, step);
        }

        /// <summary>
        /// Tương thích với các chỗ gọi cũ nếu có.
        /// </summary>
        public static List<Point3d> ExtractBoundaryPoints(ObjectId entId, Transaction tr, double curveSampling, bool autoClose)
        {
            SourceCurveInfo? info = ExtractSourceCurveInfo(entId, tr, curveSampling, 0.10, autoClose, 0);
            return info != null ? info.Points3D : new List<Point3d>();
        }
    }
}

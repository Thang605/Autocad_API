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
        /// <summary>
        /// Cắt đoạn thẳng 3D (pA -> pB) chỉ giữ phần nằm BÊN NGOÀI vùng regionGrid (khớp cao độ êm tại điểm giao biên).
        /// </summary>
        private static void AppendClippedSegmentsOutsideRegion(
            Point3d pA,
            Point3d pB,
            SpatialTriangleGrid2D regionGrid,
            SpatialBorderSegmentGrid2D regionBorderGrid,
            List<(Point3d A, Point3d B)> outSegments)
        {
            bool aStrictIn = regionGrid.ContainsPoint(pA.X, pA.Y, -1e-4) && !regionBorderGrid.IsNearAnySegment(pA.X, pA.Y, 0.012);
            bool bStrictIn = regionGrid.ContainsPoint(pB.X, pB.Y, -1e-4) && !regionBorderGrid.IsNearAnySegment(pB.X, pB.Y, 0.012);

            if (aStrictIn && bStrictIn)
            {
                return;
            }

            if (!aStrictIn && !bStrictIn)
            {
                double mx = 0.5 * (pA.X + pB.X);
                double my = 0.5 * (pA.Y + pB.Y);
                bool midStrictIn = regionGrid.ContainsPoint(mx, my, -1e-4) && !regionBorderGrid.IsNearAnySegment(mx, my, 0.012);
                if (!midStrictIn)
                {
                    outSegments.Add((pA, pB));
                    return;
                }

                // Đoạn thẳng xuyên qua vùng ở giữa: tách làm 2 đoạn hai đầu ngoài biên
                Point3d midPt = new Point3d(mx, my, 0.5 * (pA.Z + pB.Z));
                Point3d hit1 = BisectRegionBorderCrossing(pA, midPt, regionGrid);
                Point3d hit2 = BisectRegionBorderCrossing(pB, midPt, regionGrid);
                if (pA.DistanceTo(hit1) > 1e-3) outSegments.Add((pA, hit1));
                if (hit2.DistanceTo(pB) > 1e-3) outSegments.Add((hit2, pB));
                return;
            }

            if (!aStrictIn && bStrictIn)
            {
                Point3d hit = BisectRegionBorderCrossing(pA, pB, regionGrid);
                if (pA.DistanceTo(hit) > 1e-3)
                {
                    outSegments.Add((pA, hit));
                }
            }
            else
            {
                Point3d hit = BisectRegionBorderCrossing(pB, pA, regionGrid);
                if (hit.DistanceTo(pB) > 1e-3)
                {
                    outSegments.Add((hit, pB));
                }
            }
        }

        private static Point3d BisectRegionBorderCrossing(Point3d outsidePt, Point3d insidePt, SpatialTriangleGrid2D regionGrid)
        {
            double lo = 0.0;
            double hi = 1.0;
            for (int iter = 0; iter < 11; iter++)
            {
                double mid = 0.5 * (lo + hi);
                double x = outsidePt.X + mid * (insidePt.X - outsidePt.X);
                double y = outsidePt.Y + mid * (insidePt.Y - outsidePt.Y);
                if (regionGrid.ContainsPoint(x, y, 0.0))
                    hi = mid;
                else
                    lo = mid;
            }

            double t = hi;
            double hx = outsidePt.X + t * (insidePt.X - outsidePt.X);
            double hy = outsidePt.Y + t * (insidePt.Y - outsidePt.Y);
            double hz = outsidePt.Z + t * (insidePt.Z - outsidePt.Z);
            if (regionGrid.TryInterpolateZ(hx, hy, out double regionZ, 0.02))
            {
                hz = regionZ;
            }
            return new Point3d(hx, hy, hz);
        }

        private static List<Point3dCollection> ChainSegmentsToPolylines(List<(Point3d A, Point3d B)> segments)
        {
            var result = new List<Point3dCollection>();
            if (segments == null || segments.Count == 0) return result;

            var cleanSegs = new List<(Point3d A, Point3d B, QuantizedPoint2D QA, QuantizedPoint2D QB)>(segments.Count);
            var seenUndirected = new HashSet<(QuantizedPoint2D, QuantizedPoint2D)>();

            for (int i = 0; i < segments.Count; i++)
            {
                Point3d a = segments[i].A;
                Point3d b = segments[i].B;
                QuantizedPoint2D qa = QuantizedPoint2D.From(a.X, a.Y);
                QuantizedPoint2D qb = QuantizedPoint2D.From(b.X, b.Y);
                if (qa.Equals(qb)) continue;

                var key = qa.QX < qb.QX || (qa.QX == qb.QX && qa.QY <= qb.QY) ? (qa, qb) : (qb, qa);
                if (!seenUndirected.Add(key)) continue;

                cleanSegs.Add((a, b, qa, qb));
            }

            if (cleanSegs.Count == 0) return result;

            var adj = new Dictionary<QuantizedPoint2D, List<int>>();
            for (int i = 0; i < cleanSegs.Count; i++)
            {
                var s = cleanSegs[i];
                if (!adj.TryGetValue(s.QA, out var la)) { la = new List<int>(2); adj[s.QA] = la; }
                if (!adj.TryGetValue(s.QB, out var lb)) { lb = new List<int>(2); adj[s.QB] = lb; }
                la.Add(i);
                lb.Add(i);
            }

            bool[] used = new bool[cleanSegs.Count];

            void TraceFrom(int startSegIdx, bool startForward)
            {
                used[startSegIdx] = true;
                var seg0 = cleanSegs[startSegIdx];
                var pts = new Point3dCollection();
                Point3d firstPt = startForward ? seg0.A : seg0.B;
                Point3d curPt = startForward ? seg0.B : seg0.A;
                QuantizedPoint2D curKey = startForward ? seg0.QB : seg0.QA;

                pts.Add(firstPt);
                pts.Add(curPt);

                while (adj.TryGetValue(curKey, out var inc))
                {
                    int nextIdx = -1;
                    for (int k = 0; k < inc.Count; k++)
                    {
                        if (!used[inc[k]])
                        {
                            nextIdx = inc[k];
                            break;
                        }
                    }
                    if (nextIdx < 0) break;

                    used[nextIdx] = true;
                    var nextSeg = cleanSegs[nextIdx];
                    if (nextSeg.QA.Equals(curKey))
                    {
                        curPt = nextSeg.B;
                        curKey = nextSeg.QB;
                    }
                    else
                    {
                        curPt = nextSeg.A;
                        curKey = nextSeg.QA;
                    }
                    pts.Add(curPt);
                }

                if (pts.Count >= 2)
                {
                    result.Add(pts);
                }
            }

            // Ưu tiên lần theo các nút bậc != 2 (đầu mút hở hoặc ngã ba) trước, sau đó mới quét các chu trình kín bậc 2
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < cleanSegs.Count; i++)
                {
                    if (used[i]) continue;
                    var s = cleanSegs[i];
                    if (pass == 0)
                    {
                        int degA = adj[s.QA].Count;
                        int degB = adj[s.QB].Count;
                        if (degA != 2)
                            TraceFrom(i, startForward: true);
                        else if (degB != 2)
                            TraceFrom(i, startForward: false);
                    }
                    else
                    {
                        TraceFrom(i, startForward: true);
                    }
                }
            }

            return result;
        }
    }
}

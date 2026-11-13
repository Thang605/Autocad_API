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
        private static void BuildClosedAndChainedRegions(
            List<SourceCurveInfo> sourceCurves,
            double closeTol,
            bool treatNestedClosedAsHole,
            out List<Polygon2D> outerRegions,
            out List<Polygon2D> holeRegions,
            out HashSet<int> curvesInClosedLoops)
        {
            outerRegions = new List<Polygon2D>();
            holeRegions = new List<Polygon2D>();
            curvesInClosedLoops = new HashSet<int>();

            List<Polygon2D> rawPolys = new List<Polygon2D>();

            // 1. Đường vốn đã khép kín
            for (int i = 0; i < sourceCurves.Count; i++)
            {
                SourceCurveInfo sc = sourceCurves[i];
                if (sc.IsClosed && sc.Points3D.Count >= 3)
                {
                    var poly = new Polygon2D(sc.Points3D);
                    if (poly.Vertices3D.Count >= 3 && poly.AbsArea > 1e-3)
                    {
                        if (!sc.WasAutoClosedGap && !sc.Id.IsNull && sc.Id.IsValid && !sc.Id.IsErased)
                        {
                            poly.AssociativeEntityId = sc.Id;
                        }
                        rawPolys.Add(poly);
                        curvesInClosedLoops.Add(i);
                    }
                }
            }

            // 2. Ghép các đường hở nối tiếp đầu-cuối thành vòng khép kín đơn (bậc 2 tại mỗi nút nối)
            List<int> openIndices = new List<int>();
            for (int i = 0; i < sourceCurves.Count; i++)
            {
                if (!curvesInClosedLoops.Contains(i) && sourceCurves[i].Points3D.Count >= 2)
                    openIndices.Add(i);
            }

            if (openIndices.Count >= 2)
            {
                ChainSimpleClosedCyclesFromOpenCurves(sourceCurves, openIndices, Math.Max(0.02, closeTol), rawPolys, curvesInClosedLoops);
            }

            if (rawPolys.Count == 0) return;

            if (!treatNestedClosedAsHole)
            {
                // Giữ tất cả vùng bao ngoài không lọt bên trong vùng khác; đường kín bên trong đóng vai trò Breakline đồng mức
                for (int i = 0; i < rawPolys.Count; i++)
                {
                    Point3d testPt = rawPolys[i].Vertices3D[0];
                    bool containedInLarger = false;
                    for (int j = 0; j < rawPolys.Count; j++)
                    {
                        if (i == j) continue;
                        if (rawPolys[j].AbsArea > rawPolys[i].AbsArea && rawPolys[j].Contains(testPt.X, testPt.Y))
                        {
                            containedInLarger = true;
                            break;
                        }
                    }
                    if (!containedInLarger)
                    {
                        outerRegions.Add(rawPolys[i]);
                    }
                }
            }
            else
            {
                // Phân bậc chẵn/lẻ: bậc chẵn = Outer, bậc lẻ = Hole (Hide)
                for (int i = 0; i < rawPolys.Count; i++)
                {
                    Point3d testPt = rawPolys[i].Vertices3D[0];
                    int depth = 0;
                    for (int j = 0; j < rawPolys.Count; j++)
                    {
                        if (i == j) continue;
                        if (rawPolys[j].AbsArea > rawPolys[i].AbsArea && rawPolys[j].Contains(testPt.X, testPt.Y))
                        {
                            depth++;
                        }
                    }

                    if (depth % 2 == 0) outerRegions.Add(rawPolys[i]);
                    else holeRegions.Add(rawPolys[i]);
                }
            }
        }

        private static void ChainSimpleClosedCyclesFromOpenCurves(
            List<SourceCurveInfo> curves,
            List<int> openIndices,
            double snapTol,
            List<Polygon2D> outPolys,
            HashSet<int> curvesInClosedLoops)
        {
            double tolSq = snapTol * snapTol;
            List<Point2d> nodes = new List<Point2d>();

            int GetNodeId(Point3d pt)
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    double dx = nodes[i].X - pt.X;
                    double dy = nodes[i].Y - pt.Y;
                    if (dx * dx + dy * dy <= tolSq) return i;
                }
                nodes.Add(new Point2d(pt.X, pt.Y));
                return nodes.Count - 1;
            }

            var curveEnds = new Dictionary<int, (int StartNode, int EndNode)>();
            var adj = new Dictionary<int, List<int>>();

            foreach (int cIdx in openIndices)
            {
                var pts = curves[cIdx].Points3D;
                int nStart = GetNodeId(pts[0]);
                int nEnd = GetNodeId(pts[pts.Count - 1]);
                if (nStart == nEnd) continue;

                curveEnds[cIdx] = (nStart, nEnd);
                if (!adj.TryGetValue(nStart, out var l1)) { l1 = new List<int>(); adj[nStart] = l1; }
                if (!adj.TryGetValue(nEnd, out var l2)) { l2 = new List<int>(); adj[nEnd] = l2; }
                l1.Add(cIdx);
                l2.Add(cIdx);
            }

            HashSet<int> visitedCurves = new HashSet<int>();
            foreach (int startCurve in curveEnds.Keys)
            {
                if (visitedCurves.Contains(startCurve)) continue;

                var (firstNode, curNode) = curveEnds[startCurve];
                if (adj[firstNode].Count != 2 || adj[curNode].Count != 2) continue;

                List<(int CurveIdx, bool Forward)> chain = new List<(int, bool)> { (startCurve, true) };
                HashSet<int> cycleSet = new HashSet<int> { startCurve };
                int prevCurve = startCurve;
                bool closedOk = false;

                while (true)
                {
                    if (curNode == firstNode)
                    {
                        closedOk = true;
                        break;
                    }
                    if (!adj.TryGetValue(curNode, out var inc) || inc.Count != 2)
                        break;

                    int nextCurve = inc[0] == prevCurve ? inc[1] : inc[0];
                    if (cycleSet.Contains(nextCurve))
                        break;

                    var ends = curveEnds[nextCurve];
                    bool forward = ends.StartNode == curNode;
                    curNode = forward ? ends.EndNode : ends.StartNode;
                    chain.Add((nextCurve, forward));
                    cycleSet.Add(nextCurve);
                    prevCurve = nextCurve;
                }

                if (closedOk && chain.Count >= 2)
                {
                    List<Point3d> loopPts = new List<Point3d>();
                    foreach (var (cIdx, fwd) in chain)
                    {
                        visitedCurves.Add(cIdx);
                        var srcPts = curves[cIdx].Points3D;
                        if (fwd)
                        {
                            for (int k = 0; k < srcPts.Count; k++) loopPts.Add(srcPts[k]);
                        }
                        else
                        {
                            for (int k = srcPts.Count - 1; k >= 0; k--) loopPts.Add(srcPts[k]);
                        }
                    }

                    var poly = new Polygon2D(loopPts);
                    if (poly.Vertices3D.Count >= 3 && poly.AbsArea > 1e-3)
                    {
                        outPolys.Add(poly);
                        foreach (var (cIdx, _) in chain) curvesInClosedLoops.Add(cIdx);
                    }
                }
            }
        }

        private static bool AddCurveToSurfaceBreaklines(
            CivSurface surface,
            SourceCurveInfo curve,
            double midOrdDistance,
            double supplementingDistance,
            bool usePointsOnly = false,
            bool allowDirectCurvedEntity = true)
        {
            // Thử liên kết trực tiếp ObjectId cho mọi loại đường hợp lệ để Civil 3D gắn Reactor động theo đối tượng gốc
            bool canTryDirectEntity =
                !usePointsOnly &&
                !curve.Id.IsNull &&
                curve.Id.IsValid &&
                !curve.Id.IsErased &&
                (allowDirectCurvedEntity || !curve.HasTrueArcs);

            if (canTryDirectEntity)
            {
                try
                {
                    var coll = new ObjectIdCollection { curve.Id };
                    surface.BreaklinesDefinition.AddStandardBreaklines(
                        coll,
                        midOrdDistance,
                        supplementingDistance,
                        0.0,
                        0.0);

                    // Nếu đường gần kín được tự động đóng khe hở (<= closeTol), bổ sung thêm đoạn nối khép 2 điểm đầu-cuối
                    // mà vẫn giữ nguyên liên kết động cho toàn bộ đường chính
                    if (curve.WasAutoClosedGap && curve.RawEndBeforeAutoClose.HasValue && curve.Points3D.Count >= 3)
                    {
                        try
                        {
                            var gapColl = new Point3dCollection
                            {
                                curve.RawEndBeforeAutoClose.Value,
                                curve.Points3D[0]
                            };
                            surface.BreaklinesDefinition.AddStandardBreaklines(gapColl, midOrdDistance, supplementingDistance, 0.0, 0.0);
                        }
                        catch { }
                    }

                    return true;
                }
                catch
                {
                    // Tự động chuyển sang nạp Point3dCollection chính xác bên dưới nếu loại đối tượng không hỗ trợ liên kết trực tiếp
                }
            }

            Point3dCollection ptsColl = BuildCleanPoint3dCollection(curve.Points3D, curve.IsClosed);
            if (ptsColl.Count < 2) return false;

            try
            {
                surface.BreaklinesDefinition.AddStandardBreaklines(
                    ptsColl,
                    midOrdDistance,
                    supplementingDistance,
                    0.0,
                    0.0);
                return true;
            }
            catch
            {
                try
                {
                    surface.AddVertices(ptsColl);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        private static Point3dCollection BuildCleanPoint3dCollection(List<Point3d> rawPts, bool ensureClosed)
        {
            var coll = new Point3dCollection();
            for (int i = 0; i < rawPts.Count; i++)
            {
                Point3d p = rawPts[i];
                if (coll.Count > 0)
                {
                    Point3d prev = coll[coll.Count - 1];
                    if (prev.DistanceTo(p) < 1e-5) continue;
                }
                coll.Add(p);
            }

            if (ensureClosed && coll.Count >= 3)
            {
                Point3d first = coll[0];
                Point3d last = coll[coll.Count - 1];
                if (Math.Sqrt(Math.Pow(first.X - last.X, 2) + Math.Pow(first.Y - last.Y, 2)) > 1e-4)
                {
                    coll.Add(first);
                }
            }

            return coll;
        }

        private static void AddNativeBoundaryPolygonOrEntity(
            CivSurface surface,
            Polygon2D poly,
            double midOrdDistance,
            SurfaceBoundaryType bndType,
            bool nonDestructive = false)
        {
            if (!poly.AssociativeEntityId.IsNull && poly.AssociativeEntityId.IsValid && !poly.AssociativeEntityId.IsErased)
            {
                try
                {
                    var bndColl = new ObjectIdCollection { poly.AssociativeEntityId };
                    surface.BoundariesDefinition.AddBoundaries(bndColl, midOrdDistance, bndType, nonDestructive);
                    return;
                }
                catch { }
            }

            AddNativeBoundaryPolygon(surface, poly, midOrdDistance, bndType, nonDestructive);
        }

        private static void AddNativeBoundaryPolygon(
            CivSurface surface,
            Polygon2D poly,
            double midOrdDistance,
            SurfaceBoundaryType bndType,
            bool nonDestructive = false)
        {
            if (poly.Vertices3D.Count < 3) return;
            Point3dCollection pts = BuildCleanPoint3dCollection(poly.Vertices3D, true);
            if (pts.Count < 4) return;

            try
            {
                surface.BoundariesDefinition.AddBoundaries(pts, midOrdDistance, bndType, nonDestructive);
            }
            catch { }
        }

        private static void AddPolygon2DAsNonDestructiveBreakline(
            CivSurface surface,
            Polygon2D poly,
            double midOrdDistance)
        {
            if (poly.Vertices3D.Count < 3) return;
            Point3dCollection pts = BuildCleanPoint3dCollection(poly.Vertices3D, true);
            if (pts.Count < 4) return;

            try
            {
                surface.BreaklinesDefinition.AddNonDestructiveBreaklines(pts, midOrdDistance);
            }
            catch { }
        }

        private static List<Polygon2D> ExtractPolygonsFromBoundaryIds(
            List<ObjectId>? boundaryIds,
            Transaction tr,
            double midOrdDistance)
        {
            var result = new List<Polygon2D>();
            if (boundaryIds == null || boundaryIds.Count == 0) return result;

            foreach (ObjectId id in boundaryIds)
            {
                if (id.IsNull || !id.IsValid || id.IsErased) continue;
                SourceCurveInfo? info = ExtractSourceCurveInfo(id, tr, midOrdDistance, 0.25, true, 0);
                if (info != null && info.Points3D.Count >= 3)
                {
                    var poly = new Polygon2D(info.Points3D);
                    if (poly.Vertices3D.Count >= 3 && poly.AbsArea > 1e-3)
                    {
                        if (!info.WasAutoClosedGap && info.IsClosed)
                        {
                            poly.AssociativeEntityId = id;
                        }
                        result.Add(poly);
                    }
                }
            }

            return result;
        }
    }
}

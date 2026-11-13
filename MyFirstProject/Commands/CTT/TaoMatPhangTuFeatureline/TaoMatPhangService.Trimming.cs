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
        /// Thuật toán cắt lọc tam giác thừa trực tiếp trên 1 TIN Surface duy nhất (không dùng mặt phẳng phụ):
        /// 1) Bảo vệ toàn bộ tam giác nằm trong các vùng khép kín (Closed FeatureLines / Chained Loops) và tam giác cũ (nếu gộp thêm).
        /// 2) Với đường hở (Taluy, Rãnh, Lề, Nút giao, Corridor FL):
        ///    - Ánh xạ các đỉnh TIN lên từng đường nguồn và sắp xếp theo lý trình dọc đường.
        ///    - Loại bỏ tam giác có cạnh không thuộc Breakline vượt quá ngưỡng MaxEdgeLength (chống nối bắc cầu qua lòng đường / nhánh rời).
        ///    - Lan truyền BFS từ biên ngoài (Convex Hull / Void) vào trong để bóc sạch 100% tam giác dây cung ở bụng đường cong
        ///      (cạnh nối tắt 2 đỉnh không liền kề |i - j| >= 2 trên cùng một đường nguồn), dừng chính xác tuyệt đối tại cạnh Breakline!
        /// </summary>
        private static int TrimUnwantedTrianglesOnSingleSurface(
            CivSurface surface,
            List<SourceCurveInfo> sourceCurves,
            HashSet<int> curvesInClosedLoops,
            List<Polygon2D> closedRegions,
            List<Polygon2D> holeRegions,
            bool allCurvesAreClosedRegions,
            HashSet<QuantizedPoint2D>? preExistingVertices,
            double midOrdDistance,
            bool limitMaxEdgeLength,
            double maxEdgeLength,
            Editor ed)
        {
            TinTopologyGraph graph = BuildTinTopologyGraph(surface);
            if (graph.Triangles.Count == 0) return 0;

            int triCount = graph.Triangles.Count;
            bool[] removed = new bool[triCount];
            bool[] protectedInside = new bool[triCount];

            // 1. Đánh dấu bảo vệ tam giác nằm trong các vùng khép kín (và ngoài lỗ khoét)
            for (int t = 0; t < triCount; t++)
            {
                Point2d c = graph.Triangles[t].Centroid2D;

                bool inHole = false;
                for (int h = 0; h < holeRegions.Count; h++)
                {
                    if (holeRegions[h].Contains(c.X, c.Y))
                    {
                        inHole = true;
                        break;
                    }
                }
                if (inHole)
                {
                    removed[t] = true;
                    continue;
                }

                bool inClosed = false;
                for (int r = 0; r < closedRegions.Count; r++)
                {
                    if (closedRegions[r].Contains(c.X, c.Y))
                    {
                        inClosed = true;
                        break;
                    }
                }

                if (inClosed)
                {
                    protectedInside[t] = true;
                    continue;
                }

                // Bảo vệ các tam giác cũ của mặt phẳng hiện hữu (khi chạy chế độ Bổ sung vào mặt phẳng có sẵn)
                if (preExistingVertices != null && preExistingVertices.Count > 0)
                {
                    var tri = graph.Triangles[t];
                    if (preExistingVertices.Contains(graph.Vertices[tri.V0].QKey) &&
                        preExistingVertices.Contains(graph.Vertices[tri.V1].QKey) &&
                        preExistingVertices.Contains(graph.Vertices[tri.V2].QKey))
                    {
                        protectedInside[t] = true;
                        continue;
                    }
                }

                // Nếu TẤT CẢ đối tượng nguồn đều là các vùng khép kín (ví dụ N mặt móng / đảo rời nhau),
                // thì mọi tam giác nằm ngoài hợp của các đa giác kín đều bị loại bỏ!
                if (allCurvesAreClosedRegions)
                {
                    removed[t] = true;
                }
            }

            // 2. Nếu có các đường Feature Line HỞ: chạy thuật toán nhận diện cạnh Breakline + Bóc tách BFS biên dải
            if (!allCurvesAreClosedRegions)
            {
                // Dung sai 2D chặt (12mm) kết hợp kiểm tra độ lệch cao độ Z (35mm) để phân biệt tuyệt đối
                // các đường sát nhau như Đỉnh bó vỉa - Chân bó vỉa (cách 2cm - 5cm) hoặc tường chắn!
                double matchTol2D = Math.Max(0.012, Math.Min(0.025, midOrdDistance * 0.35));
                double matchTolZ = 0.04;
                CurveVertexAttachmentMap attachment = BuildCurveVertexAttachmentMap(sourceCurves, graph.Vertices, matchTol2D, matchTolZ);

                // Đánh dấu cạnh nào của TIN là cạnh Breakline thực sự (IsLocked hoặc 2 đỉnh liền kề trên cùng 1 đường nguồn hoặc trùng đoạn thẳng nguồn)
                bool[] isBreaklineEdge = new bool[graph.Edges.Count];
                for (int eIdx = 0; eIdx < graph.Edges.Count; eIdx++)
                {
                    TinEdgeNode edge = graph.Edges[eIdx];
                    if (edge.IsLocked || attachment.IsConsecutiveCurveSegment(edge.U, edge.V, graph.Vertices))
                    {
                        isBreaklineEdge[eIdx] = true;
                    }
                }

                // Pha 2A: Loại bỏ các tam giác (không được bảo vệ) có cạnh KHÔNG thuộc Breakline dài vượt ngưỡng MaxEdgeLength
                if (limitMaxEdgeLength)
                {
                    double maxLenSq = maxEdgeLength * maxEdgeLength;
                    for (int t = 0; t < triCount; t++)
                    {
                        if (removed[t] || protectedInside[t]) continue;

                        TinTriangleNode tri = graph.Triangles[t];
                        if ((!isBreaklineEdge[tri.E0] && graph.Edges[tri.E0].Length2DSq > maxLenSq) ||
                            (!isBreaklineEdge[tri.E1] && graph.Edges[tri.E1].Length2DSq > maxLenSq) ||
                            (!isBreaklineEdge[tri.E2] && graph.Edges[tri.E2].Length2DSq > maxLenSq))
                        {
                            removed[t] = true;
                        }
                    }
                }

                // Pha 2B: Lan truyền BFS từ toàn bộ các cạnh biên lộ ra ngoài (Convex Hull hoặc sát vùng vừa xóa)
                // để bóc sạch 100% tam giác dây cung ở bụng đường cong (|rankU - rankV| >= 2 trên cùng đường hở)
                Queue<int> exposedEdgeQueue = new Queue<int>();
                bool[] inQueue = new bool[graph.Edges.Count];

                for (int eIdx = 0; eIdx < graph.Edges.Count; eIdx++)
                {
                    if (IsEdgeExposedToVoid(graph.Edges[eIdx], removed, out _))
                    {
                        exposedEdgeQueue.Enqueue(eIdx);
                        inQueue[eIdx] = true;
                    }
                }

                double maxEdgeLenVal = limitMaxEdgeLength ? maxEdgeLength : double.MaxValue;

                while (exposedEdgeQueue.Count > 0)
                {
                    int eIdx = exposedEdgeQueue.Dequeue();
                    inQueue[eIdx] = false;

                    if (isBreaklineEdge[eIdx])
                        continue; // Tuyệt đối không bao giờ bóc xuyên qua cạnh Breakline!

                    if (!IsEdgeExposedToVoid(graph.Edges[eIdx], removed, out int liveTriIdx))
                        continue;

                    if (protectedInside[liveTriIdx])
                        continue;

                    TinEdgeNode edge = graph.Edges[eIdx];
                    double edgeLen = Math.Sqrt(edge.Length2DSq);

                    bool shouldPeel = false;

                    // Điều kiện 1: Cạnh biên lộ ra ngoài vượt quá chiều dài tối đa cho phép
                    if (limitMaxEdgeLength && edgeLen > maxEdgeLenVal)
                    {
                        shouldPeel = true;
                    }
                    // Điều kiện 2: Cạnh biên là dây cung nối tắt qua ít nhất 1 đỉnh trung gian trên CÙNG một đường nguồn
                    else if (attachment.IsShortcutChordOnSameCurve(edge.U, edge.V, edgeLen, curvesInClosedLoops))
                    {
                        shouldPeel = true;
                    }

                    if (shouldPeel)
                    {
                        removed[liveTriIdx] = true;
                        TinTriangleNode peeledTri = graph.Triangles[liveTriIdx];
                        int[] triEdges = { peeledTri.E0, peeledTri.E1, peeledTri.E2 };
                        foreach (int nextEdgeIdx in triEdges)
                        {
                            if (!inQueue[nextEdgeIdx] && !isBreaklineEdge[nextEdgeIdx] &&
                                IsEdgeExposedToVoid(graph.Edges[nextEdgeIdx], removed, out _))
                            {
                                exposedEdgeQueue.Enqueue(nextEdgeIdx);
                                inQueue[nextEdgeIdx] = true;
                            }
                        }
                    }
                }
            }

            return ApplyRemovedTrianglesToSurface(surface, graph, removed, midOrdDistance, ed);
        }

        private static int TrimTrianglesByCustomPolygons(
            CivSurface surface,
            List<Polygon2D> outerPolys,
            List<Polygon2D> hidePolys,
            double midOrdDistance,
            Editor ed)
        {
            TinTopologyGraph graph = BuildTinTopologyGraph(surface);
            if (graph.Triangles.Count == 0) return 0;

            bool[] removed = new bool[graph.Triangles.Count];
            for (int t = 0; t < graph.Triangles.Count; t++)
            {
                Point2d c = graph.Triangles[t].Centroid2D;
                bool insideOuter = false;
                for (int i = 0; i < outerPolys.Count; i++)
                {
                    if (outerPolys[i].Contains(c.X, c.Y))
                    {
                        insideOuter = true;
                        break;
                    }
                }

                if (!insideOuter)
                {
                    removed[t] = true;
                    continue;
                }

                for (int h = 0; h < hidePolys.Count; h++)
                {
                    if (hidePolys[h].Contains(c.X, c.Y))
                    {
                        removed[t] = true;
                        break;
                    }
                }
            }

            return ApplyRemovedTrianglesToSurface(surface, graph, removed, midOrdDistance, ed);
        }

        /// <summary>
        /// Thực thi xóa toàn bộ các tam giác đã đánh dấu removed[] trên 1 TIN Surface duy nhất:
        /// - Với tam giác ốc đảo 3 cạnh đều giáp tam giác giữ lại: dùng Hide Boundary thu nhỏ 1mm vào tâm tam giác.
        /// - Với toàn bộ tam giác biên / cầu nối còn lại: thu thập các cạnh mà cả 2 phía đều không có tam giác giữ lại
        ///   và gọi surface.DeleteLines(edgesToDelete) trong 1 lần duy nhất!
        /// </summary>
        private static int ApplyRemovedTrianglesToSurface(
            CivSurface surface,
            TinTopologyGraph graph,
            bool[] removed,
            double midOrdDistance,
            Editor ed)
        {
            int totalRemovedCount = 0;
            for (int t = 0; t < removed.Length; t++)
            {
                if (removed[t]) totalRemovedCount++;
            }

            if (totalRemovedCount == 0) return 0;

            // Kiểm tra xem có tam giác bị xóa nào bị bao vây cả 3 cạnh bởi tam giác giữ lại không (lỗ 1 tam giác cô lập)
            List<Point3dCollection> isolatedPocketHoles = new List<Point3dCollection>();
            for (int t = 0; t < graph.Triangles.Count; t++)
            {
                if (!removed[t]) continue;

                TinTriangleNode tri = graph.Triangles[t];
                bool e0HasKept = EdgeHasKeptTriangle(graph.Edges[tri.E0], removed);
                bool e1HasKept = EdgeHasKeptTriangle(graph.Edges[tri.E1], removed);
                bool e2HasKept = EdgeHasKeptTriangle(graph.Edges[tri.E2], removed);

                if (e0HasKept && e1HasKept && e2HasKept)
                {
                    Point3d p0 = graph.Vertices[tri.V0].Pos;
                    Point3d p1 = graph.Vertices[tri.V1].Pos;
                    Point3d p2 = graph.Vertices[tri.V2].Pos;
                    double cx = (p0.X + p1.X + p2.X) / 3.0;
                    double cy = (p0.Y + p1.Y + p2.Y) / 3.0;
                    double cz = (p0.Z + p1.Z + p2.Z) / 3.0;

                    // Thu nhẹ 0.5% về tâm để Hide Boundary chỉ ẩn đúng tam giác lỗ cô lập này
                    Point3d Shrink(Point3d p) => new Point3d(
                        cx + (p.X - cx) * 0.995,
                        cy + (p.Y - cy) * 0.995,
                        cz);

                    Point3d s0 = Shrink(p0);
                    Point3d s1 = Shrink(p1);
                    Point3d s2 = Shrink(p2);
                    var pts = new Point3dCollection { s0, s1, s2, s0 };
                    isolatedPocketHoles.Add(pts);
                }
            }

            if (isolatedPocketHoles.Count > 0)
            {
                foreach (Point3dCollection holePts in isolatedPocketHoles)
                {
                    try
                    {
                        surface.BoundariesDefinition.AddBoundaries(holePts, midOrdDistance, SurfaceBoundaryType.Hide, false);
                    }
                    catch { }
                }
                surface.Rebuild();

                // Xây dựng lại đồ thị sau Rebuild để con trỏ TinSurfaceEdge còn nguyên hiệu lực trước khi DeleteLines
                TinTopologyGraph refreshedGraph = BuildTinTopologyGraph(surface);
                bool[] refreshedRemoved = new bool[refreshedGraph.Triangles.Count];

                // Ánh xạ lại trạng thái giữ/xóa theo tọa độ tâm 2D của các tam giác giữ lại
                HashSet<QuantizedPoint2D> keptCentroids = new HashSet<QuantizedPoint2D>();
                for (int t = 0; t < graph.Triangles.Count; t++)
                {
                    if (!removed[t])
                    {
                        Point2d c = graph.Triangles[t].Centroid2D;
                        keptCentroids.Add(QuantizedPoint2D.From(c.X, c.Y));
                    }
                }

                for (int t = 0; t < refreshedGraph.Triangles.Count; t++)
                {
                    Point2d c = refreshedGraph.Triangles[t].Centroid2D;
                    if (!keptCentroids.Contains(QuantizedPoint2D.From(c.X, c.Y)))
                    {
                        refreshedRemoved[t] = true;
                    }
                }

                graph = refreshedGraph;
                removed = refreshedRemoved;
            }

            List<TinSurfaceEdge> edgesToDelete = new List<TinSurfaceEdge>();
            for (int eIdx = 0; eIdx < graph.Edges.Count; eIdx++)
            {
                TinEdgeNode edge = graph.Edges[eIdx];
                bool hasKept = EdgeHasKeptTriangle(edge, removed);
                bool hasRemoved = (edge.TriA >= 0 && removed[edge.TriA]) || (edge.TriB >= 0 && removed[edge.TriB]);

                if (!hasKept && hasRemoved && edge.RawEdge != null && edge.RawEdge.IsValid)
                {
                    edgesToDelete.Add(edge.RawEdge);
                }
            }

            if (edgesToDelete.Count > 0)
            {
                try
                {
                    surface.DeleteLines(edgesToDelete);
                }
                catch
                {
                    // Fallback an toàn từng cạnh nếu có cạnh tự động bị thu hồi kèm tam giác kế cận
                    foreach (TinSurfaceEdge rawEdge in edgesToDelete)
                    {
                        try
                        {
                            if (rawEdge != null && rawEdge.IsValid)
                            {
                                surface.DeleteLine(rawEdge);
                            }
                        }
                        catch { }
                    }
                }
            }

            return totalRemovedCount;
        }

        private static bool EdgeHasKeptTriangle(TinEdgeNode edge, bool[] removed)
        {
            if (edge.TriA >= 0 && !removed[edge.TriA]) return true;
            if (edge.TriB >= 0 && !removed[edge.TriB]) return true;
            return false;
        }

        private static bool IsEdgeExposedToVoid(TinEdgeNode edge, bool[] removed, out int liveTriIdx)
        {
            bool aAlive = edge.TriA >= 0 && !removed[edge.TriA];
            bool bAlive = edge.TriB >= 0 && !removed[edge.TriB];

            if (aAlive && !bAlive)
            {
                liveTriIdx = edge.TriA;
                return true;
            }
            if (bAlive && !aAlive)
            {
                liveTriIdx = edge.TriB;
                return true;
            }

            liveTriIdx = -1;
            return false;
        }
    }
}

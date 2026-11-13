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
        /// Đồng bộ đỉnh & cao độ tại vùng giao chồng sau Rebuild():
        /// - Chế độ Đắp đè (OverwriteOverlapOnExisting = true): Xóa toàn bộ đỉnh cũ nằm lọt trong lòng R_new,
        ///   bổ sung đỉnh R_new bị thiếu (nếu mặt phẳng đích có PasteSurface/Snapshot) và ép khớp chuẩn xác cao độ Z của R_new.
        /// - Chế độ Giữ nguyên mặt cũ (OverwriteOverlapOnExisting = false): Bổ sung đỉnh trong R_new nằm ngoài R_old nếu thiếu,
        ///   và xóa đỉnh mới vô tình lọt vào lòng R_old.
        /// </summary>
        private static void ReconcileMergedVerticesAndElevations(
            CivSurface targetSurface,
            TinTopologyGraph patchGraph,
            SpatialTriangleGrid2D newPatchGrid,
            SpatialBorderSegmentGrid2D newPatchBorderGrid,
            SpatialVertexLookup2D newPatchVertexLookup,
            SpatialTriangleGrid2D oldTriGrid,
            SpatialBorderSegmentGrid2D oldBorderGrid,
            SpatialVertexLookup2D oldVertexLookup,
            bool overwriteOverlap)
        {
            if (overwriteOverlap)
            {
                double minX = newPatchGrid.MinX - 0.10;
                double maxX = newPatchGrid.MaxX + 0.10;
                double minY = newPatchGrid.MinY - 0.10;
                double maxY = newPatchGrid.MaxY + 0.10;

                List<TinSurfaceVertex> staleInteriorVertices = new List<TinSurfaceVertex>();
                try
                {
                    foreach (TinSurfaceVertex v in targetSurface.Vertices)
                    {
                        if (v == null || !v.IsValid) continue;
                        Point3d loc = v.Location;
                        if (loc.X < minX || loc.X > maxX || loc.Y < minY || loc.Y > maxY)
                            continue;

                        if (newPatchVertexLookup.TryFindNearestZ(loc.X, loc.Y, 0.015, out _))
                            continue;

                        if (newPatchGrid.ContainsPoint(loc.X, loc.Y, -1e-4) &&
                            !newPatchBorderGrid.IsNearAnySegment(loc.X, loc.Y, 0.008))
                        {
                            staleInteriorVertices.Add(v);
                        }
                    }
                }
                catch { }

                DeleteVerticesSafe(targetSurface, staleInteriorVertices);

                // Quét lại tập đỉnh hiện hành trong khung bao R_new để kiểm tra đỉnh thiếu (do PasteSurface/Snapshot) & cao độ Z
                var liveVerticesInPatchBox = new Dictionary<QuantizedPoint2D, TinSurfaceVertex>();
                var survivingNonPatchVertices = new List<TinSurfaceVertex>();

                try
                {
                    foreach (TinSurfaceVertex v in targetSurface.Vertices)
                    {
                        if (v == null || !v.IsValid) continue;
                        Point3d loc = v.Location;
                        if (loc.X < minX || loc.X > maxX || loc.Y < minY || loc.Y > maxY)
                            continue;

                        liveVerticesInPatchBox[QuantizedPoint2D.From(loc.X, loc.Y)] = v;

                        if (!newPatchVertexLookup.TryFindNearestZ(loc.X, loc.Y, 0.015, out _) &&
                            newPatchGrid.ContainsPoint(loc.X, loc.Y, 0.005))
                        {
                            survivingNonPatchVertices.Add(v);
                        }
                    }
                }
                catch { }

                Point3dCollection missingPatchPts = new Point3dCollection();
                HashSet<int> missingPatchVertexIndices = new HashSet<int>();

                for (int i = 0; i < patchGraph.Vertices.Count; i++)
                {
                    Point3d pv = patchGraph.Vertices[i].Pos;
                    TinSurfaceVertex? matchedLive = FindLiveVertexNear(liveVerticesInPatchBox, pv.X, pv.Y);
                    if (matchedLive != null)
                    {
                        if (Math.Abs(matchedLive.Location.Z - pv.Z) > 0.002)
                        {
                            try { targetSurface.SetVertexElevation(matchedLive, pv.Z); } catch { }
                        }
                    }
                    else
                    {
                        missingPatchPts.Add(pv);
                        missingPatchVertexIndices.Add(i);
                    }
                }

                // Nếu còn đỉnh giao cắt cũ không xóa được nằm trong R_new -> ép phẳng cao độ Z theo mặt phẳng R_new
                foreach (TinSurfaceVertex sv in survivingNonPatchVertices)
                {
                    try
                    {
                        if (sv.IsValid && newPatchGrid.TryInterpolateZ(sv.Location.X, sv.Location.Y, out double interpZ, 0.01))
                        {
                            if (Math.Abs(sv.Location.Z - interpZ) > 0.003)
                            {
                                targetSurface.SetVertexElevation(sv, interpZ);
                            }
                        }
                    }
                    catch { }
                }

                if (missingPatchPts.Count > 0)
                {
                    try
                    {
                        targetSurface.AddVertices(missingPatchPts);
                    }
                    catch
                    {
                        foreach (Point3d pt in missingPatchPts)
                        {
                            try { targetSurface.AddVertex(pt); } catch { }
                        }
                    }

                    // Đảo cạnh (SwapEdge) nếu cạnh Breakline của R_new trong vùng PasteSurface bị Delaunay quay chéo
                    EnforceMissingPatchLockedEdges(targetSurface, patchGraph, missingPatchVertexIndices);
                }
            }
            else
            {
                double minX = newPatchGrid.MinX - 0.10;
                double maxX = newPatchGrid.MaxX + 0.10;
                double minY = newPatchGrid.MinY - 0.10;
                double maxY = newPatchGrid.MaxY + 0.10;

                var liveVerticesInPatchBox = new Dictionary<QuantizedPoint2D, TinSurfaceVertex>();
                var unwantedIntrudingVertices = new List<TinSurfaceVertex>();

                try
                {
                    foreach (TinSurfaceVertex v in targetSurface.Vertices)
                    {
                        if (v == null || !v.IsValid) continue;
                        Point3d loc = v.Location;
                        if (loc.X < minX || loc.X > maxX || loc.Y < minY || loc.Y > maxY)
                            continue;

                        liveVerticesInPatchBox[QuantizedPoint2D.From(loc.X, loc.Y)] = v;

                        if (!oldVertexLookup.TryFindNearestZ(loc.X, loc.Y, 0.015, out _) &&
                            oldTriGrid.ContainsPoint(loc.X, loc.Y, -1e-4) &&
                            !oldBorderGrid.IsNearAnySegment(loc.X, loc.Y, 0.012))
                        {
                            unwantedIntrudingVertices.Add(v);
                        }
                    }
                }
                catch { }

                DeleteVerticesSafe(targetSurface, unwantedIntrudingVertices);

                Point3dCollection missingExteriorPts = new Point3dCollection();
                for (int i = 0; i < patchGraph.Vertices.Count; i++)
                {
                    Point3d pv = patchGraph.Vertices[i].Pos;
                    if (oldTriGrid.ContainsPoint(pv.X, pv.Y, -1e-4) && !oldBorderGrid.IsNearAnySegment(pv.X, pv.Y, 0.012))
                        continue;

                    if (FindLiveVertexNear(liveVerticesInPatchBox, pv.X, pv.Y) == null)
                    {
                        missingExteriorPts.Add(pv);
                    }
                }

                if (missingExteriorPts.Count > 0)
                {
                    try { targetSurface.AddVertices(missingExteriorPts); } catch { }
                }
            }
        }

        private static TinSurfaceVertex? FindLiveVertexNear(
            Dictionary<QuantizedPoint2D, TinSurfaceVertex> map,
            double x,
            double y)
        {
            long qx = (long)Math.Round(x * 1000.0);
            long qy = (long)Math.Round(y * 1000.0);

            if (map.TryGetValue(new QuantizedPoint2D(qx, qy), out TinSurfaceVertex? exact) && exact.IsValid)
                return exact;

            // Tìm lân cận +/- 15mm phòng sai số lượng tử hóa
            for (int dx = -15; dx <= 15; dx += 5)
            {
                for (int dy = -15; dy <= 15; dy += 5)
                {
                    if (map.TryGetValue(new QuantizedPoint2D(qx + dx, qy + dy), out TinSurfaceVertex? near) && near.IsValid)
                        return near;
                }
            }

            // Fallback quét chính xác nếu cần
            foreach (var kvp in map)
            {
                if (Math.Abs(kvp.Key.QX - qx) <= 15 && Math.Abs(kvp.Key.QY - qy) <= 15 && kvp.Value.IsValid)
                    return kvp.Value;
            }
            return null;
        }

        private static void DeleteVerticesSafe(CivSurface surface, List<TinSurfaceVertex> verticesToDelete)
        {
            if (verticesToDelete == null || verticesToDelete.Count == 0) return;

            try
            {
                surface.DeleteVertices(verticesToDelete);
            }
            catch
            {
                foreach (TinSurfaceVertex v in verticesToDelete)
                {
                    try
                    {
                        if (v != null && v.IsValid)
                            surface.DeleteVertex(v);
                    }
                    catch { }
                }
            }
        }

        private static void EnforceMissingPatchLockedEdges(
            CivSurface surface,
            TinTopologyGraph patchGraph,
            HashSet<int> missingVertexIndices)
        {
            for (int eIdx = 0; eIdx < patchGraph.Edges.Count; eIdx++)
            {
                TinEdgeNode edge = patchGraph.Edges[eIdx];
                bool isBnd = (edge.TriA >= 0) != (edge.TriB >= 0);
                if (!edge.IsLocked && !isBnd) continue;
                if (!missingVertexIndices.Contains(edge.U) && !missingVertexIndices.Contains(edge.V))
                    continue;

                Point3d pu = patchGraph.Vertices[edge.U].Pos;
                Point3d pv = patchGraph.Vertices[edge.V].Pos;
                double mx = 0.5 * (pu.X + pv.X);
                double my = 0.5 * (pu.Y + pv.Y);

                try
                {
                    TinSurfaceEdge? curEdge = surface.FindEdgeAtXY(mx, my);
                    if (curEdge != null && curEdge.IsValid)
                    {
                        Point3d e1 = curEdge.Vertex1.Location;
                        Point3d e2 = curEdge.Vertex2.Location;
                        bool matchesForward = Hypot2D(e1.X - pu.X, e1.Y - pu.Y) < 0.03 && Hypot2D(e2.X - pv.X, e2.Y - pv.Y) < 0.03;
                        bool matchesReverse = Hypot2D(e1.X - pv.X, e1.Y - pv.Y) < 0.03 && Hypot2D(e2.X - pu.X, e2.Y - pu.Y) < 0.03;
                        if (!matchesForward && !matchesReverse)
                        {
                            surface.SwapEdge(curEdge);
                        }
                    }
                }
                catch { }
            }
        }

        private static double Hypot2D(double dx, double dy) => Math.Sqrt(dx * dx + dy * dy);

        public static SurfaceOriginationType GetSurfaceOriginationType(Database db, ObjectId surfaceId)
        {
            if (surfaceId.IsNull || !surfaceId.IsValid || surfaceId.IsErased)
                return SurfaceOriginationType.None;

            try
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    SurfaceOriginationType result = SurfaceOriginationType.None;
                    if (tr.GetObject(surfaceId, OpenMode.ForRead) is CivSurface surf)
                    {
                        result = surf.OriginationType;
                    }
                    tr.Commit();
                    return result;
                }
            }
            catch
            {
                return SurfaceOriginationType.None;
            }
        }
    }
}

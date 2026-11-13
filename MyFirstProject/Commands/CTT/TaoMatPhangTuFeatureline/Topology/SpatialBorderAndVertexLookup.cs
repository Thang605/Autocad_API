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
        private sealed class SpatialBorderSegmentGrid2D
        {
            private readonly List<(double X0, double Y0, double X1, double Y1)> _segs = new List<(double, double, double, double)>();
            private readonly Dictionary<(int GX, int GY), List<int>> _cells = new Dictionary<(int GX, int GY), List<int>>();
            private readonly double _cellSize;

            public SpatialBorderSegmentGrid2D(List<(Point3d A, Point3d B)> borderSegments, double maxQueryRadius = 0.15)
            {
                double sumLen = 0.0;
                for (int i = 0; i < borderSegments.Count; i++)
                {
                    var (a, b) = borderSegments[i];
                    double len = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
                    if (len <= 1e-6) continue;
                    _segs.Add((a.X, a.Y, b.X, b.Y));
                    sumLen += len;
                }

                _cellSize = _segs.Count > 0 ? Math.Max(1.0, (sumLen / _segs.Count) * 2.0) : 5.0;

                for (int idx = 0; idx < _segs.Count; idx++)
                {
                    var s = _segs[idx];
                    int x0 = (int)Math.Floor((Math.Min(s.X0, s.X1) - maxQueryRadius) / _cellSize);
                    int x1 = (int)Math.Floor((Math.Max(s.X0, s.X1) + maxQueryRadius) / _cellSize);
                    int y0 = (int)Math.Floor((Math.Min(s.Y0, s.Y1) - maxQueryRadius) / _cellSize);
                    int y1 = (int)Math.Floor((Math.Max(s.Y0, s.Y1) + maxQueryRadius) / _cellSize);

                    for (int gx = x0; gx <= x1; gx++)
                    {
                        for (int gy = y0; gy <= y1; gy++)
                        {
                            var key = (gx, gy);
                            if (!_cells.TryGetValue(key, out var bucket))
                            {
                                bucket = new List<int>(4);
                                _cells[key] = bucket;
                            }
                            bucket.Add(idx);
                        }
                    }
                }
            }

            public bool IsNearAnySegment(double px, double py, double tol)
            {
                if (_segs.Count == 0) return false;
                int gx = (int)Math.Floor(px / _cellSize);
                int gy = (int)Math.Floor(py / _cellSize);
                if (!_cells.TryGetValue((gx, gy), out var bucket))
                    return false;

                double tolSq = tol * tol;
                for (int i = 0; i < bucket.Count; i++)
                {
                    var s = _segs[bucket[i]];
                    double dx = s.X1 - s.X0;
                    double dy = s.Y1 - s.Y0;
                    double lenSq = dx * dx + dy * dy;
                    double t = lenSq > 1e-12 ? ((px - s.X0) * dx + (py - s.Y0) * dy) / lenSq : 0.0;
                    if (t < 0.0) t = 0.0;
                    else if (t > 1.0) t = 1.0;

                    double rx = px - (s.X0 + t * dx);
                    double ry = py - (s.Y0 + t * dy);
                    if (rx * rx + ry * ry <= tolSq)
                        return true;
                }
                return false;
            }
        }

        private static SpatialBorderSegmentGrid2D BuildBorderSegmentGrid(TinTopologyGraph graph)
        {
            var borderSegs = new List<(Point3d A, Point3d B)>();
            for (int eIdx = 0; eIdx < graph.Edges.Count; eIdx++)
            {
                TinEdgeNode edge = graph.Edges[eIdx];
                if ((edge.TriA >= 0) != (edge.TriB >= 0))
                {
                    borderSegs.Add((graph.Vertices[edge.U].Pos, graph.Vertices[edge.V].Pos));
                }
            }
            return new SpatialBorderSegmentGrid2D(borderSegs);
        }

        private sealed class SpatialVertexLookup2D
        {
            private readonly Dictionary<(int GX, int GY), List<Point3d>> _cells = new Dictionary<(int GX, int GY), List<Point3d>>();
            private const double CellSize = 1.0;

            public SpatialVertexLookup2D(List<TinVertexNode> vertices)
            {
                for (int i = 0; i < vertices.Count; i++)
                {
                    Point3d p = vertices[i].Pos;
                    int gx = (int)Math.Floor(p.X / CellSize);
                    int gy = (int)Math.Floor(p.Y / CellSize);
                    var key = (gx, gy);
                    if (!_cells.TryGetValue(key, out var list))
                    {
                        list = new List<Point3d>(2);
                        _cells[key] = list;
                    }
                    list.Add(p);
                }
            }

            public bool TryFindNearestZ(double x, double y, double tol2D, out double nearestZ)
            {
                nearestZ = 0.0;
                double bestDistSq = tol2D * tol2D;
                bool found = false;

                int gx0 = (int)Math.Floor((x - tol2D) / CellSize);
                int gx1 = (int)Math.Floor((x + tol2D) / CellSize);
                int gy0 = (int)Math.Floor((y - tol2D) / CellSize);
                int gy1 = (int)Math.Floor((y + tol2D) / CellSize);

                for (int gx = gx0; gx <= gx1; gx++)
                {
                    for (int gy = gy0; gy <= gy1; gy++)
                    {
                        if (!_cells.TryGetValue((gx, gy), out var list)) continue;
                        for (int i = 0; i < list.Count; i++)
                        {
                            Point3d p = list[i];
                            double dSq = (p.X - x) * (p.X - x) + (p.Y - y) * (p.Y - y);
                            if (dSq <= bestDistSq)
                            {
                                bestDistSq = dSq;
                                nearestZ = p.Z;
                                found = true;
                            }
                        }
                    }
                }
                return found;
            }
        }
    }
}

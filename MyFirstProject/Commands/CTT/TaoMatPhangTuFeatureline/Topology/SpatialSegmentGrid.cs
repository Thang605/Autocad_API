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
        private sealed class SpatialSegmentGrid
        {
            private readonly struct SegEntry
            {
                public readonly int CurveIdx;
                public readonly double X0, Y0, Z0, X1, Y1, Z1;
                public readonly double StartStation;
                public readonly double SegLen;

                public SegEntry(int curveIdx, double x0, double y0, double z0, double x1, double y1, double z1, double startStation, double segLen)
                {
                    CurveIdx = curveIdx;
                    X0 = x0; Y0 = y0; Z0 = z0;
                    X1 = x1; Y1 = y1; Z1 = z1;
                    StartStation = startStation;
                    SegLen = segLen;
                }
            }

            private readonly Dictionary<(int GX, int GY), List<int>> _cells = new Dictionary<(int GX, int GY), List<int>>();
            private readonly List<SegEntry> _segments = new List<SegEntry>();
            private readonly double _cellSize;
            private readonly int _curveCount;

            public SpatialSegmentGrid(List<SourceCurveInfo> curves, double pad)
            {
                _curveCount = curves.Count;
                double totalLen = 0.0;
                int segCount = 0;

                for (int c = 0; c < curves.Count; c++)
                {
                    var pts = curves[c].Points3D;
                    for (int i = 0; i < pts.Count - 1; i++)
                    {
                        double dx = pts[i + 1].X - pts[i].X;
                        double dy = pts[i + 1].Y - pts[i].Y;
                        double len = Math.Sqrt(dx * dx + dy * dy);
                        if (len > 1e-6)
                        {
                            totalLen += len;
                            segCount++;
                        }
                    }
                }

                _cellSize = segCount > 0 ? Math.Max(2.0, (totalLen / segCount) * 2.5) : 10.0;

                for (int c = 0; c < curves.Count; c++)
                {
                    var pts = curves[c].Points3D;
                    double sta = 0.0;
                    for (int i = 0; i < pts.Count - 1; i++)
                    {
                        double x0 = pts[i].X, y0 = pts[i].Y, z0 = pts[i].Z;
                        double x1 = pts[i + 1].X, y1 = pts[i + 1].Y, z1 = pts[i + 1].Z;
                        double dx = x1 - x0, dy = y1 - y0;
                        double len = Math.Sqrt(dx * dx + dy * dy);
                        if (len <= 1e-6) continue;

                        int sIdx = _segments.Count;
                        _segments.Add(new SegEntry(c, x0, y0, z0, x1, y1, z1, sta, len));
                        sta += len;

                        int minGX = (int)Math.Floor((Math.Min(x0, x1) - pad) / _cellSize);
                        int maxGX = (int)Math.Floor((Math.Max(x0, x1) + pad) / _cellSize);
                        int minGY = (int)Math.Floor((Math.Min(y0, y1) - pad) / _cellSize);
                        int maxGY = (int)Math.Floor((Math.Max(y0, y1) + pad) / _cellSize);

                        for (int gx = minGX; gx <= maxGX; gx++)
                        {
                            for (int gy = minGY; gy <= maxGY; gy++)
                            {
                                var key = (gx, gy);
                                if (!_cells.TryGetValue(key, out var bucket))
                                {
                                    bucket = new List<int>(4);
                                    _cells[key] = bucket;
                                }
                                bucket.Add(sIdx);
                            }
                        }
                    }
                    curves[c].TotalLength2D = sta;
                }
            }

            public void FindMatchingCurvesForPoint(
                double px,
                double py,
                double pz,
                double tol2DSq,
                double tolZ,
                List<(int VertexIdx, double Station)>[] perCurveHits,
                int vertexIdx)
            {
                int gx = (int)Math.Floor(px / _cellSize);
                int gy = (int)Math.Floor(py / _cellSize);
                if (!_cells.TryGetValue((gx, gy), out var bucket)) return;

                Dictionary<int, (double BestDistSq, double BestDz, double BestSta)>? bestPerCurve = null;
                double minOverallDistSq = double.MaxValue;

                for (int i = 0; i < bucket.Count; i++)
                {
                    SegEntry seg = _segments[bucket[i]];
                    double distSq = PointToSegmentDistSq(px, py, seg.X0, seg.Y0, seg.X1, seg.Y1, out double tProj);
                    if (distSq <= tol2DSq)
                    {
                        double interpZ = seg.Z0 + tProj * (seg.Z1 - seg.Z0);
                        double dz = Math.Abs(pz - interpZ);
                        if (dz > tolZ) continue;

                        double sta = seg.StartStation + tProj * seg.SegLen;
                        bestPerCurve ??= new Dictionary<int, (double, double, double)>(2);
                        if (!bestPerCurve.TryGetValue(seg.CurveIdx, out var prev) || distSq < prev.BestDistSq)
                        {
                            bestPerCurve[seg.CurveIdx] = (distSq, dz, sta);
                        }
                        if (distSq < minOverallDistSq)
                        {
                            minOverallDistSq = distSq;
                        }
                    }
                }

                if (bestPerCurve != null)
                {
                    foreach (var kvp in bestPerCurve)
                    {
                        // Nếu đỉnh nằm trùng sát (< 2mm) trên 1 đường nguồn, chỉ gắn thêm đường nguồn thứ 2 nếu đường thứ 2 cũng đi sát đỉnh (< 5mm)
                        if (minOverallDistSq < 4e-6 && kvp.Value.BestDistSq > 2.5e-5)
                            continue;

                        perCurveHits[kvp.Key].Add((vertexIdx, kvp.Value.BestSta));
                    }
                }
            }

            public bool IsSegmentCollinearWithAnySource(
                double ux, double uy,
                double vx, double vy,
                double mx, double my,
                double tol)
            {
                double tolSq = tol * tol;
                int gx = (int)Math.Floor(mx / _cellSize);
                int gy = (int)Math.Floor(my / _cellSize);
                if (!_cells.TryGetValue((gx, gy), out var bucket)) return false;

                for (int i = 0; i < bucket.Count; i++)
                {
                    SegEntry seg = _segments[bucket[i]];
                    if (PointToSegmentDistSq(mx, my, seg.X0, seg.Y0, seg.X1, seg.Y1, out _) <= tolSq &&
                        PointToSegmentDistSq(ux, uy, seg.X0, seg.Y0, seg.X1, seg.Y1, out _) <= tolSq &&
                        PointToSegmentDistSq(vx, vy, seg.X0, seg.Y0, seg.X1, seg.Y1, out _) <= tolSq)
                    {
                        return true;
                    }
                }
                return false;
            }

            private static double PointToSegmentDistSq(
                double px, double py,
                double x0, double y0,
                double x1, double y1,
                out double t)
            {
                double dx = x1 - x0;
                double dy = y1 - y0;
                double lenSq = dx * dx + dy * dy;
                if (lenSq <= 1e-12)
                {
                    t = 0.0;
                    double ex = px - x0, ey = py - y0;
                    return ex * ex + ey * ey;
                }

                t = ((px - x0) * dx + (py - y0) * dy) / lenSq;
                if (t < 0.0) t = 0.0;
                else if (t > 1.0) t = 1.0;

                double projX = x0 + t * dx;
                double projY = y0 + t * dy;
                double rx = px - projX;
                double ry = py - projY;
                return rx * rx + ry * ry;
            }
        }
    }
}

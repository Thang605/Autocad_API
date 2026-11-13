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
        /// Bảng ánh xạ tốc độ cao O(1) bằng lưới ô vuông 2D (Uniform Spatial Grid):
        /// gắn mỗi đỉnh TIN vào các đường FeatureLine/Polyline đi qua nó, đánh thứ tự rank dọc theo lý trình từng đường.
        /// </summary>
        private sealed class CurveVertexAttachmentMap
        {
            private readonly struct VertexCurveEntry
            {
                public readonly int CurveIndex;
                public readonly int OrderRank;
                public readonly double StationAlongCurve;

                public VertexCurveEntry(int curveIndex, int orderRank, double station)
                {
                    CurveIndex = curveIndex;
                    OrderRank = orderRank;
                    StationAlongCurve = station;
                }
            }

            private readonly List<VertexCurveEntry>[] _entriesByVertex;
            private readonly List<SourceCurveInfo> _curves;
            private readonly SpatialSegmentGrid _segmentGrid;
            private readonly double _segMatchTol;

            public CurveVertexAttachmentMap(
                List<SourceCurveInfo> curves,
                List<TinVertexNode> vertices,
                SpatialSegmentGrid segmentGrid,
                double segMatchTol)
            {
                _curves = curves;
                _segmentGrid = segmentGrid;
                _segMatchTol = segMatchTol;
                _entriesByVertex = new List<VertexCurveEntry>[vertices.Count];
            }

            public void AddAttachment(int vertexIdx, int curveIdx, int rank, double station)
            {
                _entriesByVertex[vertexIdx] ??= new List<VertexCurveEntry>(2);
                _entriesByVertex[vertexIdx].Add(new VertexCurveEntry(curveIdx, rank, station));
            }

            public bool IsConsecutiveCurveSegment(int u, int v, List<TinVertexNode> vertices)
            {
                var listU = _entriesByVertex[u];
                var listV = _entriesByVertex[v];
                if (listU != null && listV != null)
                {
                    for (int i = 0; i < listU.Count; i++)
                    {
                        var eu = listU[i];
                        for (int j = 0; j < listV.Count; j++)
                        {
                            var ev = listV[j];
                            if (eu.CurveIndex == ev.CurveIndex)
                            {
                                int diff = Math.Abs(eu.OrderRank - ev.OrderRank);
                                if (diff == 1)
                                    return true;

                                // Nếu đường khép kín, đỉnh đầu (rank 0) và đỉnh cuối nối liền nhau
                                SourceCurveInfo c = _curves[eu.CurveIndex];
                                if (c.IsClosed && diff == c.AttachedTinVertexCount - 1 && c.AttachedTinVertexCount >= 3)
                                    return true;
                            }
                        }
                    }
                }

                // Kiểm tra bổ sung: trung điểm cạnh nằm sát đoạn nguồn (phòng trường hợp giao cắt chèn đỉnh)
                Point3d pu = vertices[u].Pos;
                Point3d pv = vertices[v].Pos;
                double mx = 0.5 * (pu.X + pv.X);
                double my = 0.5 * (pu.Y + pv.Y);
                return _segmentGrid.IsSegmentCollinearWithAnySource(pu.X, pu.Y, pv.X, pv.Y, mx, my, _segMatchTol);
            }

            public bool IsShortcutChordOnSameCurve(int u, int v, double chordLength, HashSet<int> curvesInClosedLoops)
            {
                if (chordLength <= 1e-5) return false;

                var listU = _entriesByVertex[u];
                var listV = _entriesByVertex[v];
                if (listU == null || listV == null) return false;

                for (int i = 0; i < listU.Count; i++)
                {
                    var eu = listU[i];
                    for (int j = 0; j < listV.Count; j++)
                    {
                        var ev = listV[j];
                        if (eu.CurveIndex != ev.CurveIndex) continue;

                        int rankDiff = Math.Abs(eu.OrderRank - ev.OrderRank);
                        if (rankDiff < 2) continue;

                        SourceCurveInfo curve = _curves[eu.CurveIndex];
                        double alongDist = Math.Abs(eu.StationAlongCurve - ev.StationAlongCurve);
                        if (curve.IsClosed && curve.TotalLength2D > 0)
                        {
                            alongDist = Math.Min(alongDist, Math.Max(0.0, curve.TotalLength2D - alongDist));
                        }

                        // Tỷ số chiều dài cung / dây cung:
                        // Mọi cung cong đường bộ kể cả góc quay 180 độ đều có tỷ số <= pi/2 (~1.57) <= 2.4.
                        // Nếu tỷ số > 2.4 trên đường hở thì đó là miệng chữ U sâu, giữ lại để không ăn lẹm lòng chữ U!
                        double ratio = alongDist / chordLength;
                        if (ratio <= 2.45)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
        }

        private static CurveVertexAttachmentMap BuildCurveVertexAttachmentMap(
            List<SourceCurveInfo> sourceCurves,
            List<TinVertexNode> vertices,
            double matchTol2D,
            double matchTolZ)
        {
            var grid = new SpatialSegmentGrid(sourceCurves, matchTol2D);
            var map = new CurveVertexAttachmentMap(sourceCurves, vertices, grid, matchTol2D);

            double tol2DSq = matchTol2D * matchTol2D;
            var perCurveHits = new List<(int VertexIdx, double Station)>[sourceCurves.Count];
            for (int c = 0; c < sourceCurves.Count; c++)
            {
                perCurveHits[c] = new List<(int VertexIdx, double Station)>();
            }

            for (int vIdx = 0; vIdx < vertices.Count; vIdx++)
            {
                Point3d vp = vertices[vIdx].Pos;
                grid.FindMatchingCurvesForPoint(vp.X, vp.Y, vp.Z, tol2DSq, matchTolZ, perCurveHits, vIdx);
            }

            for (int c = 0; c < sourceCurves.Count; c++)
            {
                var hits = perCurveHits[c];
                if (hits.Count == 0) continue;

                hits.Sort((a, b) => a.Station.CompareTo(b.Station));
                sourceCurves[c].AttachedTinVertexCount = hits.Count;

                for (int rank = 0; rank < hits.Count; rank++)
                {
                    map.AddAttachment(hits[rank].VertexIdx, c, rank, hits[rank].Station);
                }
            }

            return map;
        }
    }
}

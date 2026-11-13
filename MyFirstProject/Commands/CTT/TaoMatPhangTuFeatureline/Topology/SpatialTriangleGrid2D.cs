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
        private readonly struct TriangleVertexKey : IEquatable<TriangleVertexKey>
        {
            public readonly QuantizedPoint2D A;
            public readonly QuantizedPoint2D B;
            public readonly QuantizedPoint2D C;

            public TriangleVertexKey(QuantizedPoint2D a, QuantizedPoint2D b, QuantizedPoint2D c)
            {
                // Sắp xếp tăng dần từ điển để không phụ thuộc thứ tự đỉnh (CW / CCW)
                if (Compare(a, b) > 0) (a, b) = (b, a);
                if (Compare(b, c) > 0) (b, c) = (c, b);
                if (Compare(a, b) > 0) (a, b) = (b, a);
                A = a;
                B = b;
                C = c;
            }

            private static int Compare(QuantizedPoint2D p1, QuantizedPoint2D p2)
            {
                int cmp = p1.QX.CompareTo(p2.QX);
                return cmp != 0 ? cmp : p1.QY.CompareTo(p2.QY);
            }

            public static TriangleVertexKey From(TinTopologyGraph graph, TinTriangleNode tri)
            {
                return new TriangleVertexKey(
                    graph.Vertices[tri.V0].QKey,
                    graph.Vertices[tri.V1].QKey,
                    graph.Vertices[tri.V2].QKey);
            }

            public bool Equals(TriangleVertexKey other) => A.Equals(other.A) && B.Equals(other.B) && C.Equals(other.C);
            public override bool Equals(object? obj) => obj is TriangleVertexKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(A, B, C);
        }

        private static HashSet<TriangleVertexKey> BuildTriangleVertexKeySet(TinTopologyGraph graph)
        {
            var set = new HashSet<TriangleVertexKey>(graph.Triangles.Count);
            for (int t = 0; t < graph.Triangles.Count; t++)
            {
                set.Add(TriangleVertexKey.From(graph, graph.Triangles[t]));
            }
            return set;
        }

        /// <summary>
        /// Lưới ô vuông 2D tra cứu tam giác tốc độ O(1) cho mặt phẳng lớn (40,000+ tam giác),
        /// hỗ trợ kiểm tra điểm thuộc vùng và nội suy phẳng cao độ Z chính xác từng milimét.
        /// </summary>
        private sealed class SpatialTriangleGrid2D
        {
            private readonly struct TriRecord
            {
                public readonly double Ax, Ay, Az;
                public readonly double Bx, By, Bz;
                public readonly double Cx, Cy, Cz;
                public readonly double MinX, MinY, MaxX, MaxY;
                public readonly double LenAB, LenBC, LenCA;
                public readonly double OrientSign;
                public readonly double InvAbsTwiceArea;

                public TriRecord(Point3d a, Point3d b, Point3d c, double sign, double absTwiceArea)
                {
                    Ax = a.X; Ay = a.Y; Az = a.Z;
                    Bx = b.X; By = b.Y; Bz = b.Z;
                    Cx = c.X; Cy = c.Y; Cz = c.Z;
                    MinX = Math.Min(Ax, Math.Min(Bx, Cx));
                    MinY = Math.Min(Ay, Math.Min(By, Cy));
                    MaxX = Math.Max(Ax, Math.Max(Bx, Cx));
                    MaxY = Math.Max(Ay, Math.Max(By, Cy));
                    LenAB = Math.Sqrt((Bx - Ax) * (Bx - Ax) + (By - Ay) * (By - Ay));
                    LenBC = Math.Sqrt((Cx - Bx) * (Cx - Bx) + (Cy - By) * (Cy - By));
                    LenCA = Math.Sqrt((Ax - Cx) * (Ax - Cx) + (Ay - Cy) * (Ay - Cy));
                    OrientSign = sign;
                    InvAbsTwiceArea = 1.0 / absTwiceArea;
                }
            }

            private readonly List<TriRecord> _tris;
            private readonly Dictionary<(int GX, int GY), List<int>> _cells = new Dictionary<(int GX, int GY), List<int>>();
            private readonly double _cellSize;
            public readonly double MinX, MinY, MaxX, MaxY;

            public SpatialTriangleGrid2D(TinTopologyGraph graph)
            {
                _tris = new List<TriRecord>(graph.Triangles.Count);
                double gMinX = double.MaxValue, gMinY = double.MaxValue;
                double gMaxX = double.MinValue, gMaxY = double.MinValue;
                double sumSpan = 0.0;

                for (int t = 0; t < graph.Triangles.Count; t++)
                {
                    TinTriangleNode node = graph.Triangles[t];
                    Point3d a = graph.Vertices[node.V0].Pos;
                    Point3d b = graph.Vertices[node.V1].Pos;
                    Point3d c = graph.Vertices[node.V2].Pos;

                    double twiceArea = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
                    double absTwiceArea = Math.Abs(twiceArea);
                    if (absTwiceArea <= 1e-10) continue;

                    var rec = new TriRecord(a, b, c, twiceArea >= 0.0 ? 1.0 : -1.0, absTwiceArea);
                    _tris.Add(rec);

                    if (rec.MinX < gMinX) gMinX = rec.MinX;
                    if (rec.MinY < gMinY) gMinY = rec.MinY;
                    if (rec.MaxX > gMaxX) gMaxX = rec.MaxX;
                    if (rec.MaxY > gMaxY) gMaxY = rec.MaxY;
                    sumSpan += (rec.MaxX - rec.MinX) + (rec.MaxY - rec.MinY);
                }

                if (_tris.Count == 0)
                {
                    MinX = MinY = 0.0;
                    MaxX = MaxY = -1.0;
                    _cellSize = 10.0;
                    return;
                }

                MinX = gMinX; MinY = gMinY;
                MaxX = gMaxX; MaxY = gMaxY;
                _cellSize = Math.Max(2.0, (sumSpan / (2.0 * _tris.Count)) * 2.0);

                const double pad = 0.05;
                for (int idx = 0; idx < _tris.Count; idx++)
                {
                    TriRecord rec = _tris[idx];
                    int x0 = (int)Math.Floor((rec.MinX - pad) / _cellSize);
                    int x1 = (int)Math.Floor((rec.MaxX + pad) / _cellSize);
                    int y0 = (int)Math.Floor((rec.MinY - pad) / _cellSize);
                    int y1 = (int)Math.Floor((rec.MaxY + pad) / _cellSize);

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

            /// <summary>
            /// Kiểm tra điểm (px, py) có nằm trong hợp các tam giác không.
            /// edgeDistTol > 0: nới nhẹ ra ngoài biên; edgeDistTol &lt; 0: yêu cầu nằm sâu bên trong cạnh tam giác.
            /// </summary>
            public bool ContainsPoint(double px, double py, double edgeDistTol)
            {
                if (_tris.Count == 0) return false;
                double boxPad = Math.Max(0.0, edgeDistTol);
                if (px < MinX - boxPad || px > MaxX + boxPad || py < MinY - boxPad || py > MaxY + boxPad)
                    return false;

                int gx = (int)Math.Floor(px / _cellSize);
                int gy = (int)Math.Floor(py / _cellSize);
                if (!_cells.TryGetValue((gx, gy), out var bucket))
                    return false;

                for (int i = 0; i < bucket.Count; i++)
                {
                    TriRecord tri = _tris[bucket[i]];
                    if (px < tri.MinX - boxPad || px > tri.MaxX + boxPad ||
                        py < tri.MinY - boxPad || py > tri.MaxY + boxPad)
                        continue;

                    double c0 = ((tri.Bx - tri.Ax) * (py - tri.Ay) - (tri.By - tri.Ay) * (px - tri.Ax)) * tri.OrientSign;
                    if (c0 < -edgeDistTol * tri.LenAB) continue;

                    double c1 = ((tri.Cx - tri.Bx) * (py - tri.By) - (tri.Cy - tri.By) * (px - tri.Bx)) * tri.OrientSign;
                    if (c1 < -edgeDistTol * tri.LenBC) continue;

                    double c2 = ((tri.Ax - tri.Cx) * (py - tri.Cy) - (tri.Ay - tri.Cy) * (px - tri.Cx)) * tri.OrientSign;
                    if (c2 < -edgeDistTol * tri.LenCA) continue;

                    return true;
                }

                return false;
            }

            public bool TryInterpolateZ(double px, double py, out double z, double edgeDistTol)
            {
                z = 0.0;
                if (_tris.Count == 0) return false;
                double boxPad = Math.Max(0.0, edgeDistTol);
                if (px < MinX - boxPad || px > MaxX + boxPad || py < MinY - boxPad || py > MaxY + boxPad)
                    return false;

                int gx = (int)Math.Floor(px / _cellSize);
                int gy = (int)Math.Floor(py / _cellSize);
                if (!_cells.TryGetValue((gx, gy), out var bucket))
                    return false;

                for (int i = 0; i < bucket.Count; i++)
                {
                    TriRecord tri = _tris[bucket[i]];
                    if (px < tri.MinX - boxPad || px > tri.MaxX + boxPad ||
                        py < tri.MinY - boxPad || py > tri.MaxY + boxPad)
                        continue;

                    double c0 = ((tri.Bx - tri.Ax) * (py - tri.Ay) - (tri.By - tri.Ay) * (px - tri.Ax)) * tri.OrientSign;
                    if (c0 < -edgeDistTol * tri.LenAB) continue;

                    double c1 = ((tri.Cx - tri.Bx) * (py - tri.By) - (tri.Cy - tri.By) * (px - tri.Bx)) * tri.OrientSign;
                    if (c1 < -edgeDistTol * tri.LenBC) continue;

                    double c2 = ((tri.Ax - tri.Cx) * (py - tri.Cy) - (tri.Ay - tri.Cy) * (px - tri.Cx)) * tri.OrientSign;
                    if (c2 < -edgeDistTol * tri.LenCA) continue;

                    double wA = c1 * tri.InvAbsTwiceArea;
                    double wB = c2 * tri.InvAbsTwiceArea;
                    double wC = c0 * tri.InvAbsTwiceArea;
                    z = wA * tri.Az + wB * tri.Bz + wC * tri.Cz;
                    return true;
                }

                return false;
            }
        }
    }
}

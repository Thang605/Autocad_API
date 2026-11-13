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
        private readonly struct QuantizedPoint2D : IEquatable<QuantizedPoint2D>
        {
            public readonly long QX;
            public readonly long QY;

            public QuantizedPoint2D(long qx, long qy)
            {
                QX = qx;
                QY = qy;
            }

            public static QuantizedPoint2D From(double x, double y)
            {
                return new QuantizedPoint2D((long)Math.Round(x * 1000.0), (long)Math.Round(y * 1000.0));
            }

            public bool Equals(QuantizedPoint2D other) => QX == other.QX && QY == other.QY;
            public override bool Equals(object? obj) => obj is QuantizedPoint2D other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(QX, QY);
        }

        private readonly struct UndirectedEdgeKey : IEquatable<UndirectedEdgeKey>
        {
            public readonly int U;
            public readonly int V;

            public UndirectedEdgeKey(int a, int b)
            {
                if (a <= b) { U = a; V = b; }
                else { U = b; V = a; }
            }

            public bool Equals(UndirectedEdgeKey other) => U == other.U && V == other.V;
            public override bool Equals(object? obj) => obj is UndirectedEdgeKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(U, V);
        }

        private sealed class TinVertexNode
        {
            public int Index;
            public Point3d Pos;
            public QuantizedPoint2D QKey;
        }

        private sealed class TinEdgeNode
        {
            public int U;
            public int V;
            public double Length2DSq;
            public bool IsLocked;
            public int TriA = -1;
            public int TriB = -1;
            public TinSurfaceEdge? RawEdge;
        }

        private sealed class TinTriangleNode
        {
            public int V0, V1, V2;
            public int E0, E1, E2;
            public Point2d Centroid2D;
        }

        private sealed class TinTopologyGraph
        {
            public List<TinVertexNode> Vertices = new List<TinVertexNode>();
            public List<TinEdgeNode> Edges = new List<TinEdgeNode>();
            public List<TinTriangleNode> Triangles = new List<TinTriangleNode>();
        }

        private static TinTopologyGraph BuildTinTopologyGraph(CivSurface surface)
        {
            var graph = new TinTopologyGraph();
            var vertexLookup = new Dictionary<QuantizedPoint2D, int>();
            var edgeLookup = new Dictionary<UndirectedEdgeKey, int>();

            int GetOrAddVertex(Point3d pt)
            {
                QuantizedPoint2D qk = QuantizedPoint2D.From(pt.X, pt.Y);
                if (vertexLookup.TryGetValue(qk, out int idx))
                    return idx;

                idx = graph.Vertices.Count;
                graph.Vertices.Add(new TinVertexNode { Index = idx, Pos = pt, QKey = qk });
                vertexLookup[qk] = idx;
                return idx;
            }

            int RegisterTriangleEdge(TinSurfaceEdge? rawEdge, int fallbackU, int fallbackV, int triIndex)
            {
                int u = fallbackU;
                int v = fallbackV;
                bool locked = false;

                if (rawEdge != null && rawEdge.IsValid)
                {
                    try
                    {
                        u = GetOrAddVertex(rawEdge.Vertex1.Location);
                        v = GetOrAddVertex(rawEdge.Vertex2.Location);
                        locked = rawEdge.IsLocked;
                    }
                    catch { }
                }

                var key = new UndirectedEdgeKey(u, v);
                if (!edgeLookup.TryGetValue(key, out int eIdx))
                {
                    Point3d pu = graph.Vertices[key.U].Pos;
                    Point3d pv = graph.Vertices[key.V].Pos;
                    double dx = pu.X - pv.X;
                    double dy = pu.Y - pv.Y;

                    eIdx = graph.Edges.Count;
                    graph.Edges.Add(new TinEdgeNode
                    {
                        U = key.U,
                        V = key.V,
                        Length2DSq = dx * dx + dy * dy,
                        IsLocked = locked,
                        TriA = triIndex,
                        TriB = -1,
                        RawEdge = rawEdge
                    });
                    edgeLookup[key] = eIdx;
                }
                else
                {
                    TinEdgeNode existing = graph.Edges[eIdx];
                    if (locked) existing.IsLocked = true;
                    if (existing.RawEdge == null && rawEdge != null) existing.RawEdge = rawEdge;
                    if (existing.TriA != triIndex && existing.TriB == -1)
                    {
                        existing.TriB = triIndex;
                    }
                }

                return eIdx;
            }

            TinSurfaceTriangleCollection tris = surface.GetTriangles(false);
            foreach (TinSurfaceTriangle tri in tris)
            {
                if (tri == null || !tri.IsVisible) continue;

                Point3d p0, p1, p2;
                try
                {
                    p0 = tri.Vertex1.Location;
                    p1 = tri.Vertex2.Location;
                    p2 = tri.Vertex3.Location;
                }
                catch
                {
                    continue;
                }

                int v0 = GetOrAddVertex(p0);
                int v1 = GetOrAddVertex(p1);
                int v2 = GetOrAddVertex(p2);
                if (v0 == v1 || v1 == v2 || v2 == v0) continue;

                int tIdx = graph.Triangles.Count;
                TinSurfaceEdge? re0 = null, re1 = null, re2 = null;
                try { re0 = tri.Edge1; } catch { }
                try { re1 = tri.Edge2; } catch { }
                try { re2 = tri.Edge3; } catch { }

                int e0 = RegisterTriangleEdge(re0, v0, v1, tIdx);
                int e1 = RegisterTriangleEdge(re1, v1, v2, tIdx);
                int e2 = RegisterTriangleEdge(re2, v2, v0, tIdx);

                graph.Triangles.Add(new TinTriangleNode
                {
                    V0 = v0,
                    V1 = v1,
                    V2 = v2,
                    E0 = e0,
                    E1 = e1,
                    E2 = e2,
                    Centroid2D = new Point2d((p0.X + p1.X + p2.X) / 3.0, (p0.Y + p1.Y + p2.Y) / 3.0)
                });
            }

            return graph;
        }
    }
}

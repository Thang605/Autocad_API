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
        internal sealed class SourceCurveInfo
        {
            public int CurveIndex;
            public ObjectId Id;
            public string EntityTypeName = string.Empty;
            public string DisplayName = string.Empty;
            public List<Point3d> Points3D = new List<Point3d>();
            public bool IsClosed;
            public bool WasAutoClosedGap;
            public Point3d? RawEndBeforeAutoClose;
            public bool HasTrueArcs;
            public double TotalLength2D;
            public int AttachedTinVertexCount;
        }

        internal sealed class Polygon2D
        {
            public readonly List<Point3d> Vertices3D;
            public readonly double MinX, MinY, MaxX, MaxY;
            public readonly double SignedArea;
            public ObjectId AssociativeEntityId { get; set; } = ObjectId.Null;
            public double AbsArea => Math.Abs(SignedArea);

            public Polygon2D(List<Point3d> pts)
            {
                Vertices3D = new List<Point3d>(pts.Count);
                double minX = double.MaxValue, minY = double.MaxValue;
                double maxX = double.MinValue, maxY = double.MinValue;

                for (int i = 0; i < pts.Count; i++)
                {
                    Point3d p = pts[i];
                    if (Vertices3D.Count > 0)
                    {
                        Point3d prev = Vertices3D[Vertices3D.Count - 1];
                        if (Math.Abs(p.X - prev.X) < 1e-5 && Math.Abs(p.Y - prev.Y) < 1e-5)
                            continue;
                    }
                    Vertices3D.Add(p);
                    if (p.X < minX) minX = p.X;
                    if (p.Y < minY) minY = p.Y;
                    if (p.X > maxX) maxX = p.X;
                    if (p.Y > maxY) maxY = p.Y;
                }

                if (Vertices3D.Count > 2)
                {
                    Point3d first = Vertices3D[0];
                    Point3d last = Vertices3D[Vertices3D.Count - 1];
                    if (Math.Abs(first.X - last.X) < 1e-4 && Math.Abs(first.Y - last.Y) < 1e-4)
                    {
                        Vertices3D.RemoveAt(Vertices3D.Count - 1);
                    }
                }

                MinX = minX; MinY = minY; MaxX = maxX; MaxY = maxY;

                double area2 = 0.0;
                int n = Vertices3D.Count;
                for (int i = 0; i < n; i++)
                {
                    Point3d a = Vertices3D[i];
                    Point3d b = Vertices3D[(i + 1) % n];
                    area2 += a.X * b.Y - b.X * a.Y;
                }
                SignedArea = 0.5 * area2;
            }

            public bool Contains(double x, double y)
            {
                if (Vertices3D.Count < 3) return false;
                if (x < MinX - 1e-4 || x > MaxX + 1e-4 || y < MinY - 1e-4 || y > MaxY + 1e-4)
                    return false;

                bool inside = false;
                int n = Vertices3D.Count;
                for (int i = 0, j = n - 1; i < n; j = i++)
                {
                    double xi = Vertices3D[i].X, yi = Vertices3D[i].Y;
                    double xj = Vertices3D[j].X, yj = Vertices3D[j].Y;

                    if (((yi > y) != (yj > y)) &&
                        (x < (xj - xi) * (y - yi) / (yj - yi + 1e-30) + xi))
                    {
                        inside = !inside;
                    }
                }
                return inside;
            }
        }
    }
}

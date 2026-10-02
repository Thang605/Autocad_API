using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;
using CadEntity = Autodesk.AutoCAD.DatabaseServices.Entity;
using CadRegion = Autodesk.AutoCAD.DatabaseServices.Region;

[assembly: CommandClass(typeof(MyFirstProject.Civil_Tool_2.CT_Tao_3d_Solid_Tu_Feature_Line_Command))]

namespace MyFirstProject.Civil_Tool_2
{
    public class CT_Tao_3d_Solid_Tu_Feature_Line_Command
    {
        [CommandMethod("CT_Tao_3d_Solid_Tu_Feature_Line")]
        public static void Tao3dSolidTuFeatureLine()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            Editor ed = doc.Editor;
            Database db = doc.Database;

            try
            {
                using (var form = new Tao3dSolidTuFeatureLineForm(db, ed))
                {
                    Application.ShowModalDialog(form);
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n[LỖI] Không thể mở giao diện tạo 3D Solid: {ex.Message}");
            }
        }

        #region GEOMETRY & SOLID CREATION CORE LOGIC

        /// <summary>
        /// Trích xuất danh sách điểm 3D từ đối tượng (FeatureLine, Polyline, Polyline3d)
        /// </summary>
        public static List<Point3d> ExtractBoundaryPoints(
            ObjectId entId,
            Transaction tr,
            double curveSampling,
            bool autoClose,
            out string entityType,
            out double totalLength,
            out bool isClosed,
            out double minZ,
            out double maxZ)
        {
            entityType = "Unknown";
            totalLength = 0;
            isClosed = false;
            minZ = 0;
            maxZ = 0;

            if (entId.IsNull || !entId.IsValid) return new List<Point3d>();

            CadEntity ent = tr.GetObject(entId, OpenMode.ForRead) as CadEntity;
            if (ent == null) return new List<Point3d>();

            List<Point3d> rawPoints = new List<Point3d>();

            if (ent is FeatureLine fl)
            {
                entityType = "Feature Line";
                isClosed = fl.Closed;

                // Thử explode để lấy cung cong mịn nếu có
                bool explodedOk = false;
                try
                {
                    using (DBObjectCollection objs = new DBObjectCollection())
                    {
                        fl.Explode(objs);
                        if (objs.Count > 0)
                        {
                            List<Point3d> expPts = new List<Point3d>();
                            foreach (DBObject obj in objs)
                            {
                                if (obj is Arc arc)
                                {
                                    totalLength += arc.Length;
                                    int steps = Math.Max(4, (int)Math.Ceiling(arc.Length / Math.Max(0.05, curveSampling)));
                                    double delta = arc.Length / steps;
                                    for (int i = 0; i <= steps; i++)
                                    {
                                        expPts.Add(arc.GetPointAtDist(i * delta));
                                    }
                                }
                                else if (obj is Polyline pl)
                                {
                                    totalLength += pl.Length;
                                    SamplePolyline(pl, curveSampling, expPts);
                                }
                                else if (obj is Line line)
                                {
                                    totalLength += line.Length;
                                    expPts.Add(line.StartPoint);
                                    expPts.Add(line.EndPoint);
                                }
                                else if (obj is Curve c)
                                {
                                    try { totalLength += c.GetDistanceAtParameter(c.EndParam) - c.GetDistanceAtParameter(c.StartParam); } catch { }
                                    expPts.Add(c.StartPoint);
                                    expPts.Add(c.EndPoint);
                                }
                                obj.Dispose();
                            }

                            if (expPts.Count >= 3)
                            {
                                rawPoints = expPts;
                                explodedOk = true;
                            }
                        }
                    }
                }
                catch { }

                // Nếu explode không cho ra kết quả, lấy trực tiếp qua GetPoints
                if (!explodedOk)
                {
                    try
                    {
                        Point3dCollection ptsCol = fl.GetPoints(Autodesk.Civil.FeatureLinePointType.AllPoints);
                        if (ptsCol != null)
                        {
                            for (int i = 0; i < ptsCol.Count; i++)
                            {
                                rawPoints.Add(ptsCol[i]);
                            }
                        }
                    }
                    catch { }
                }
            }
            else if (ent is Polyline pl)
            {
                entityType = "Polyline (2D/LW)";
                totalLength = pl.Length;
                isClosed = pl.Closed;
                SamplePolyline(pl, curveSampling, rawPoints);
            }
            else if (ent is Polyline3d pl3d)
            {
                entityType = "Polyline 3D";
                totalLength = pl3d.Length;
                isClosed = pl3d.Closed;

                foreach (object obj in pl3d)
                {
                    if (obj is ObjectId vertId)
                    {
                        PolylineVertex3d v = tr.GetObject(vertId, OpenMode.ForRead) as PolylineVertex3d;
                        if (v != null) rawPoints.Add(v.Position);
                    }
                    else if (obj is PolylineVertex3d v)
                    {
                        rawPoints.Add(v.Position);
                    }
                }
            }

            var cleanPoints = CleanPolygonPoints(rawPoints, autoClose, out isClosed);

            if (cleanPoints.Count > 0)
            {
                minZ = cleanPoints.Min(p => p.Z);
                maxZ = cleanPoints.Max(p => p.Z);

                // Tính tổng chiều dài chu vi nếu chưa có
                if (totalLength <= 0 && cleanPoints.Count >= 2)
                {
                    for (int i = 0; i < cleanPoints.Count; i++)
                    {
                        Point3d p1 = cleanPoints[i];
                        Point3d p2 = cleanPoints[(i + 1) % cleanPoints.Count];
                        totalLength += p1.DistanceTo(p2);
                    }
                }
            }

            return cleanPoints;
        }

        private static void SamplePolyline(Polyline pl, double curveSampling, List<Point3d> outPts)
        {
            if (HasBulges(pl))
            {
                try
                {
                    double len = pl.Length;
                    int steps = Math.Max(20, (int)Math.Ceiling(len / Math.Max(0.05, curveSampling)));
                    double delta = len / steps;
                    for (int i = 0; i <= steps; i++)
                    {
                        outPts.Add(pl.GetPointAtDist(i * delta));
                    }
                    return;
                }
                catch { }
            }

            for (int i = 0; i < pl.NumberOfVertices; i++)
            {
                outPts.Add(pl.GetPoint3dAt(i));
            }
        }

        private static bool HasBulges(Polyline pl)
        {
            for (int i = 0; i < pl.NumberOfVertices; i++)
            {
                if (Math.Abs(pl.GetBulgeAt(i)) > 1e-6) return true;
            }
            return false;
        }

        private static List<Point3d> CleanPolygonPoints(List<Point3d> pts, bool autoClose, out bool isClosed)
        {
            isClosed = false;
            if (pts == null || pts.Count == 0) return new List<Point3d>();

            // 1. Loại bỏ các điểm trùng nhau liên tiếp
            List<Point3d> filtered = new List<Point3d>();
            for (int i = 0; i < pts.Count; i++)
            {
                if (filtered.Count == 0 || filtered[filtered.Count - 1].DistanceTo(pts[i]) > 1e-4)
                {
                    filtered.Add(pts[i]);
                }
            }

            if (filtered.Count < 3) return filtered;

            // 2. Kiểm tra khép kín: Điểm cuối trùng điểm đầu
            double closeDist = filtered[0].DistanceTo(filtered[filtered.Count - 1]);
            if (closeDist < 1e-4)
            {
                isClosed = true;
                filtered.RemoveAt(filtered.Count - 1); // Bỏ điểm lặp để danh sách có đúng N đỉnh độc nhất
            }
            else if (autoClose)
            {
                isClosed = true; // Người dùng cho phép tự động nối đỉnh cuối về đỉnh đầu
            }

            return filtered;
        }

        /// <summary>
        /// Tạo 3D Solid dạng tấm dày T từ danh sách điểm đường bao kín
        /// </summary>
        public static Solid3d CreateSolidFromPolygon(List<Point3d> inputPoints, double thickness, bool isDownwards)
        {
            if (inputPoints == null || inputPoints.Count < 3)
                throw new ArgumentException("Đường bao cần tối thiểu 3 đỉnh để tạo 3D Solid.");

            if (thickness <= 1e-5)
                throw new ArgumentException("Độ dày cần lớn hơn 0.");

            List<Point3d> pts = new List<Point3d>(inputPoints);
            int n = pts.Count;

            // 1. Đảm bảo đỉnh có thứ tự Ngược chiều kim đồng hồ (CCW) trên mặt phẳng XY
            double signedArea = 0;
            for (int i = 0; i < n; i++)
            {
                Point3d p1 = pts[i];
                Point3d p2 = pts[(i + 1) % n];
                signedArea += (p1.X * p2.Y - p2.X * p1.Y);
            }

            if (signedArea < 0)
            {
                pts.Reverse();
            }

            // Vector đùn theo phương Z
            Vector3d extrudeVec = isDownwards ? new Vector3d(0, 0, -thickness) : new Vector3d(0, 0, thickness);

            // 2. KIỂM TRA ĐỒNG PHẲNG (PLANAR CHECK)
            // Nếu tất cả các đỉnh cùng nằm trên 1 mặt phẳng, có thể dùng trực tiếp Region Extrude
            if (IsCoplanar(pts, out Plane plane))
            {
                try
                {
                    Solid3d planarSolid = CreatePlanarExtrudedSolid(pts, extrudeVec);
                    if (planarSolid != null) return planarSolid;
                }
                catch { }
            }

            // 3. TAM GIÁC HÓA 2D (EAR CLIPPING TRIANGULATION) CHO BỀ MẶT 3D LỒI LÕM
            List<(int, int, int)> triangles = Triangulate2D(pts);
            if (triangles.Count == 0)
                throw new InvalidOperationException("Không thể tam giác hóa bề mặt đa giác từ các đỉnh Feature Line.");

            // 4. DỰNG KHỐI SUBDMESH KÍN WATERTIGHT VÀ CONVERT THÀNH SOLID3D
            try
            {
                Solid3d solidFromMesh = CreateSolidFromSubDMesh(pts, triangles, extrudeVec);
                if (solidFromMesh != null) return solidFromMesh;
            }
            catch { }

            // 5. PHƯƠNG ÁN DỰ PHÒNG: TẠO TẬP HỢP LĂNG TRỤ TAM GIÁC (TRIANGULAR PRISMS) RỒI UNITE
            return CreateSolidFromTriangularPrisms(pts, triangles, extrudeVec);
        }

        private static bool IsCoplanar(List<Point3d> pts, out Plane plane)
        {
            plane = null;
            if (pts.Count < 3) return false;

            // Tính pháp tuyến Newell
            double nx = 0, ny = 0, nz = 0;
            int n = pts.Count;
            for (int i = 0; i < n; i++)
            {
                Point3d current = pts[i];
                Point3d next = pts[(i + 1) % n];
                nx += (current.Y - next.Y) * (current.Z + next.Z);
                ny += (current.Z - next.Z) * (current.X + next.X);
                nz += (current.X - next.X) * (current.Y + next.Y);
            }

            Vector3d normal = new Vector3d(nx, ny, nz);
            if (normal.Length < 1e-8) return false;
            normal = normal.GetNormal();

            Point3d p0 = pts[0];
            plane = new Plane(p0, normal);

            foreach (Point3d p in pts)
            {
                double dist = Math.Abs(normal.DotProduct(p - p0));
                if (dist > 1e-3) return false; // Lệch quá 1mm coi như phi phẳng
            }

            return true;
        }

        private static Solid3d CreatePlanarExtrudedSolid(List<Point3d> pts, Vector3d extrudeVec)
        {
            using (DBObjectCollection curves = new DBObjectCollection())
            {
                int n = pts.Count;
                for (int i = 0; i < n; i++)
                {
                    curves.Add(new Line(pts[i], pts[(i + 1) % n]));
                }

                using (DBObjectCollection regions = CadRegion.CreateFromCurves(curves))
                {
                    if (regions != null && regions.Count > 0 && regions[0] is CadRegion reg)
                    {
                        Solid3d solid = new Solid3d();
                        SweepOptions options = new SweepOptions();
                        solid.CreateExtrudedSolid(reg, extrudeVec, options);
                        return solid;
                    }
                }
            }
            return null;
        }

        private static Solid3d CreateSolidFromSubDMesh(List<Point3d> topPoints, List<(int, int, int)> triangles, Vector3d extrudeVec)
        {
            int n = topPoints.Count;
            Point3dCollection vertices = new Point3dCollection();

            // Đỉnh mặt trên: 0 .. n-1
            for (int i = 0; i < n; i++)
            {
                vertices.Add(topPoints[i]);
            }

            // Đỉnh mặt dưới: n .. 2n-1
            for (int i = 0; i < n; i++)
            {
                vertices.Add(topPoints[i] + extrudeVec);
            }

            Int32Collection faceArray = new Int32Collection();

            // 1. Mặt trên (Top triangles): Ngược chiều kim đồng hồ (CCW) -> Pháp tuyến hướng ra ngoài
            foreach (var (u, v, w) in triangles)
            {
                faceArray.Add(3);
                faceArray.Add(u);
                faceArray.Add(v);
                faceArray.Add(w);
            }

            // 2. Mặt dưới (Bottom triangles): Đảo thứ tự -> Pháp tuyến hướng xuống đáy
            foreach (var (u, v, w) in triangles)
            {
                faceArray.Add(3);
                faceArray.Add(n + u);
                faceArray.Add(n + w);
                faceArray.Add(n + v);
            }

            // 3. Các mặt thành bên (Side Quads): Mỗi cạnh i -> j của biên
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                // Thứ tự đỉnh tứ giác hướng ra ngoài: j -> i -> n+i -> n+j
                faceArray.Add(4);
                faceArray.Add(j);
                faceArray.Add(i);
                faceArray.Add(n + i);
                faceArray.Add(n + j);
            }

            using (SubDMesh mesh = new SubDMesh())
            {
                mesh.SetSubDMesh(vertices, faceArray, 0);
                Solid3d solid = mesh.ConvertToSolid(false, false);
                return solid;
            }
        }

        private static Solid3d CreateSolidFromTriangularPrisms(List<Point3d> topPoints, List<(int, int, int)> triangles, Vector3d extrudeVec)
        {
            Solid3d totalSolid = null;
            SweepOptions options = new SweepOptions();

            foreach (var (u, v, w) in triangles)
            {
                Point3d a = topPoints[u];
                Point3d b = topPoints[v];
                Point3d c = topPoints[w];

                using (DBObjectCollection curves = new DBObjectCollection())
                {
                    curves.Add(new Line(a, b));
                    curves.Add(new Line(b, c));
                    curves.Add(new Line(c, a));

                    using (DBObjectCollection regions = CadRegion.CreateFromCurves(curves))
                    {
                        if (regions != null && regions.Count > 0 && regions[0] is CadRegion reg)
                        {
                            Solid3d prism = new Solid3d();
                            prism.CreateExtrudedSolid(reg, extrudeVec, options);

                            if (totalSolid == null)
                            {
                                totalSolid = prism;
                            }
                            else
                            {
                                try
                                {
                                    totalSolid.BooleanOperation(BooleanOperationType.BoolUnite, prism);
                                    prism.Dispose();
                                }
                                catch
                                {
                                    // Nếu không thể unite lăng trụ này, bỏ qua để giữ khối tổng
                                    prism.Dispose();
                                }
                            }
                        }
                    }
                }
            }

            if (totalSolid == null)
                throw new InvalidOperationException("Không thể tạo các lăng trụ 3D từ các tam giác bề mặt.");

            return totalSolid;
        }

        public static List<(int, int, int)> Triangulate2D(List<Point3d> pts)
        {
            int n = pts.Count;
            var triangles = new List<(int, int, int)>();
            if (n < 3) return triangles;
            if (n == 3)
            {
                triangles.Add((0, 1, 2));
                return triangles;
            }

            List<int> V = new List<int>();
            for (int i = 0; i < n; i++) V.Add(i);

            int safetyCounter = 0;
            int maxIterations = n * n * 2;

            while (V.Count > 3 && safetyCounter++ < maxIterations)
            {
                bool earFound = false;
                int m = V.Count;

                for (int i = 0; i < m; i++)
                {
                    int prevIdx = V[(i - 1 + m) % m];
                    int currIdx = V[i];
                    int nextIdx = V[(i + 1) % m];

                    Point3d a = pts[prevIdx];
                    Point3d b = pts[currIdx];
                    Point3d c = pts[nextIdx];

                    // Kiểm tra góc lồi (Convex check)
                    double cross = (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);
                    if (cross <= 1e-9) continue;

                    // Kiểm tra xem có đỉnh nào khác nằm trong tam giác không
                    bool hasPointInside = false;
                    for (int j = 0; j < m; j++)
                    {
                        if (j == (i - 1 + m) % m || j == i || j == (i + 1) % m) continue;
                        Point3d p = pts[V[j]];
                        if (IsPointInsideTriangle2D(p, a, b, c))
                        {
                            hasPointInside = true;
                            break;
                        }
                    }

                    if (!hasPointInside)
                    {
                        triangles.Add((prevIdx, currIdx, nextIdx));
                        V.RemoveAt(i);
                        earFound = true;
                        break;
                    }
                }

                if (!earFound)
                {
                    // Fallback tránh treo vòng lặp nếu đa giác tự cắt nhẹ
                    triangles.Add((V[0], V[1], V[2]));
                    V.RemoveAt(1);
                }
            }

            if (V.Count == 3)
            {
                triangles.Add((V[0], V[1], V[2]));
            }

            return triangles;
        }

        private static bool IsPointInsideTriangle2D(Point3d p, Point3d a, Point3d b, Point3d c)
        {
            double cp1 = (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
            double cp2 = (c.X - b.X) * (p.Y - b.Y) - (c.Y - b.Y) * (p.X - b.X);
            double cp3 = (a.X - c.X) * (p.Y - c.Y) - (a.Y - c.Y) * (p.X - c.X);

            bool hasNeg = (cp1 < -1e-9) || (cp2 < -1e-9) || (cp3 < -1e-9);
            bool hasPos = (cp1 > 1e-9) || (cp2 > 1e-9) || (cp3 > 1e-9);

            return !(hasNeg && hasPos);
        }

        #endregion
    }
}

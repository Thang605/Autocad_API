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
        internal static int CountVisibleTriangles(CivSurface surface)
        {
            try
            {
                return surface.GetTriangles(false).Count;
            }
            catch
            {
                return 0;
            }
        }

        private static void SetAutoRebuildSafe(CivSurface surface, bool autoRebuild)
        {
            try
            {
                surface.AutoRebuild = autoRebuild;
            }
            catch { }
        }

        private static ObjectId FindSurfaceStyleId(CivilDocument civDoc, Transaction tr, string styleName)
        {
            SurfaceStyleCollection styles = civDoc.Styles.SurfaceStyles;
            if (!string.IsNullOrWhiteSpace(styleName))
            {
                if (styles.Contains(styleName))
                    return styles[styleName];

                foreach (ObjectId sId in styles)
                {
                    if (tr.GetObject(sId, OpenMode.ForWrite) is SurfaceStyle st &&
                        string.Equals(st.Name, styleName, StringComparison.OrdinalIgnoreCase))
                    {
                        return sId;
                    }
                }
            }

            foreach (ObjectId sId in styles)
            {
                return sId;
            }

            return ObjectId.Null;
        }

        internal static ObjectId FindSurfaceByName(CivilDocument civDoc, Transaction tr, string surfaceName)
        {
            if (string.IsNullOrWhiteSpace(surfaceName)) return ObjectId.Null;

            foreach (ObjectId sId in civDoc.GetSurfaceIds())
            {
                if (sId.IsNull || !sId.IsValid || sId.IsErased) continue;
                if (tr.GetObject(sId, OpenMode.ForWrite) is CivSurface s &&
                    string.Equals(s.Name, surfaceName.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return sId;
                }
            }
            return ObjectId.Null;
        }

        private static string EnsureUniqueSurfaceName(CivilDocument civDoc, Transaction tr, string baseName)
        {
            HashSet<string> existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ObjectId sId in civDoc.GetSurfaceIds())
            {
                if (sId.IsNull || !sId.IsValid || sId.IsErased) continue;
                if (tr.GetObject(sId, OpenMode.ForWrite) is CivSurface s && !string.IsNullOrEmpty(s.Name))
                {
                    existingNames.Add(s.Name);
                }
            }

            if (!existingNames.Contains(baseName))
                return baseName;

            int index = 1;
            string candidate;
            do
            {
                candidate = $"{baseName}_{index}";
                index++;
            }
            while (existingNames.Contains(candidate));

            return candidate;
        }

        private static string EnsureUniquePrefixedSubSurfaceName(CivilDocument civDoc, Transaction tr, string parentSurfaceName, string rawSuffixOrName)
        {
            HashSet<string> existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ObjectId sId in civDoc.GetSurfaceIds())
            {
                if (sId.IsNull || !sId.IsValid || sId.IsErased) continue;
                if (tr.GetObject(sId, OpenMode.ForWrite) is CivSurface s && !string.IsNullOrEmpty(s.Name))
                {
                    existingNames.Add(s.Name);
                }
            }

            string desired = BuildPrefixedSubSurfaceName(parentSurfaceName, rawSuffixOrName);
            if (!existingNames.Contains(desired) && !string.Equals(desired, parentSurfaceName, StringComparison.OrdinalIgnoreCase))
            {
                return desired;
            }

            // Nếu đang ở dạng đánh số tự động '<Parent>_Phu_<N>' hoặc kết thúc bằng '_<Số>', tăng thẳng số thứ tự để tên đẹp ('_Phu_2', '_Phu_3'...)
            int lastUnderscore = desired.LastIndexOf('_');
            if (lastUnderscore > 0 && lastUnderscore < desired.Length - 1)
            {
                string tailDigits = desired.Substring(lastUnderscore + 1);
                if (int.TryParse(tailDigits, out int startNum) && startNum >= 1)
                {
                    string stem = desired.Substring(0, lastUnderscore);
                    int nextNum = startNum + 1;
                    while (true)
                    {
                        string numbered = $"{stem}_{nextNum}";
                        if (!existingNames.Contains(numbered) && !string.Equals(numbered, parentSurfaceName, StringComparison.OrdinalIgnoreCase))
                        {
                            return numbered;
                        }
                        nextNum++;
                    }
                }
            }

            int idx = 1;
            while (true)
            {
                string candidate = $"{desired}_{idx}";
                if (!existingNames.Contains(candidate) && !string.Equals(candidate, parentSurfaceName, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
                idx++;
            }
        }

        internal static ObjectId ResolveSubSurfaceStyleId(
            CivilDocument civDoc,
            Transaction tr,
            string requestedSubStyleName,
            ObjectId fallbackStyleId,
            out string resolvedStyleName)
        {
            resolvedStyleName = string.Empty;
            SurfaceStyleCollection styles = civDoc.Styles.SurfaceStyles;

            string[] candidates = !string.IsNullOrWhiteSpace(requestedSubStyleName)
                ? new[] { requestedSubStyleName.Trim(), "_No Display", "No Display", "_Khong Hien Thi", "Border Only" }
                : new[] { "_No Display", "No Display", "_Khong Hien Thi", "Border Only" };

            foreach (string cand in candidates)
            {
                foreach (ObjectId sId in styles)
                {
                    if (sId.IsNull || !sId.IsValid || sId.IsErased) continue;
                    if (tr.GetObject(sId, OpenMode.ForWrite) is SurfaceStyle st &&
                        string.Equals(st.Name, cand, StringComparison.OrdinalIgnoreCase))
                    {
                        resolvedStyleName = st.Name;
                        return sId;
                    }
                }
            }

            if (!fallbackStyleId.IsNull && fallbackStyleId.IsValid && !fallbackStyleId.IsErased)
            {
                if (tr.GetObject(fallbackStyleId, OpenMode.ForWrite) is SurfaceStyle fbSt)
                {
                    resolvedStyleName = fbSt.Name;
                    return fallbackStyleId;
                }
            }

            foreach (ObjectId sId in styles)
            {
                if (sId.IsNull || !sId.IsValid || sId.IsErased) continue;
                if (tr.GetObject(sId, OpenMode.ForWrite) is SurfaceStyle anySt)
                {
                    resolvedStyleName = anySt.Name;
                    return sId;
                }
            }

            return ObjectId.Null;
        }

        private static void ClearSurfaceDefinitionsAndEdits(CivSurface surface)
        {
            try
            {
                for (int i = surface.BoundariesDefinition.Count - 1; i >= 0; i--)
                {
                    try { surface.BoundariesDefinition.RemoveAt(i); } catch { }
                }
            }
            catch { }

            try
            {
                for (int i = surface.BreaklinesDefinition.Count - 1; i >= 0; i--)
                {
                    try { surface.BreaklinesDefinition.RemoveAt(i); } catch { }
                }
            }
            catch { }

            try
            {
                for (int i = surface.Operations.Count - 1; i >= 0; i--)
                {
                    try { surface.Operations.RemoveAt(i); } catch { }
                }
            }
            catch { }

            try { surface.Rebuild(); } catch { }
        }

        private static SurfaceOperationPasteSurface? FindPasteOperationForSubSurface(CivSurface targetSurface, ObjectId subSurfaceId)
        {
            if (subSurfaceId.IsNull || !subSurfaceId.IsValid) return null;
            try
            {
                for (int i = 0; i < targetSurface.Operations.Count; i++)
                {
                    try
                    {
                        if (targetSurface.Operations[i] is SurfaceOperationPasteSurface pasteOp &&
                            pasteOp.SurfaceId == subSurfaceId)
                        {
                            return pasteOp;
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return null;
        }

        internal static bool HasRestrictiveOuterBoundary(CivSurface surface)
        {
            try
            {
                for (int i = 0; i < surface.BoundariesDefinition.Count; i++)
                {
                    try
                    {
                        SurfaceBoundaryType bType = surface.BoundariesDefinition[i].BoundaryType;
                        if (bType == SurfaceBoundaryType.Outer || bType == SurfaceBoundaryType.DataClip)
                            return true;
                    }
                    catch { }
                }
            }
            catch { }
            return false;
        }

        internal static List<Point2d> GetSampleVisibleCentroids(CivSurface surface, int maxSamples = 8)
        {
            var result = new List<Point2d>();
            try
            {
                TinSurfaceTriangleCollection tris = surface.GetTriangles(false);
                int count = tris.Count;
                if (count == 0) return result;

                int step = Math.Max(1, count / Math.Max(1, maxSamples));
                int idx = 0;
                foreach (TinSurfaceTriangle tri in tris)
                {
                    if (tri != null && tri.IsValid && (idx % step == 0))
                    {
                        Point3d p0 = tri.Vertex1.Location;
                        Point3d p1 = tri.Vertex2.Location;
                        Point3d p2 = tri.Vertex3.Location;
                        result.Add(new Point2d((p0.X + p1.X + p2.X) / 3.0, (p0.Y + p1.Y + p2.Y) / 3.0));
                        if (result.Count >= maxSamples) break;
                    }
                    idx++;
                }
            }
            catch { }
            return result;
        }

        internal static bool IsPointVisibleOnSurface(CivSurface surface, double x, double y)
        {
            try
            {
                surface.FindElevationAtXY(x, y);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static List<Point2d> GetSampleBorderPoints(CivSurface surface, int maxSamples = 24)
        {
            var result = new List<Point2d>();
            try
            {
                TinSurfaceTriangleCollection tris = surface.GetTriangles(false);
                foreach (TinSurfaceTriangle tri in tris)
                {
                    if (tri == null || !tri.IsValid) continue;
                    TinSurfaceEdge?[] edges = { tri.Edge1, tri.Edge2, tri.Edge3 };
                    for (int e = 0; e < edges.Length; e++)
                    {
                        TinSurfaceEdge? edge = edges[e];
                        if (edge == null || !edge.IsValid) continue;

                        bool t1 = edge.Triangle1 != null && edge.Triangle1.IsVisible;
                        bool t2 = edge.Triangle2 != null && edge.Triangle2.IsVisible;
                        if (t1 != t2)
                        {
                            Point3d p1 = edge.Vertex1.Location;
                            Point3d p2 = edge.Vertex2.Location;
                            result.Add(new Point2d(0.5 * (p1.X + p2.X), 0.5 * (p1.Y + p2.Y)));
                            if (result.Count >= maxSamples) return result;
                        }
                    }
                }
            }
            catch { }
            return result;
        }
    }
}

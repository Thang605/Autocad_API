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
        private const string FlSurfaceRecipeDictKey = "CTT_FL_SURF_RECIPE_V1";

        internal sealed class FeatureLineSurfaceRecipe
        {
            public int Version { get; set; } = 1;
            public List<string> SourceHandles { get; set; } = new List<string>();
            public List<string> CustomOuterHandles { get; set; } = new List<string>();
            public List<string> CustomHideHandles { get; set; } = new List<string>();
            public int BoundaryMode { get; set; }
            public double MidOrdinateDistance { get; set; } = 0.10;
            public bool UseSupplementing { get; set; } = true;
            public double SupplementingDistance { get; set; } = 2.00;
            public bool LimitMaxEdgeLength { get; set; } = true;
            public double MaxEdgeLength { get; set; } = 25.00;
            public bool AutoCloseNearClosed { get; set; } = true;
            public double AutoCloseTolerance { get; set; } = 0.10;
            public bool TreatNestedClosedAsHole { get; set; } = false;
            public bool AutoRebuild { get; set; } = true;
            public bool OverwriteOverlapOnExisting { get; set; } = true;
            public string ParentSurfaceHandle { get; set; } = string.Empty;
            public ulong LastGeometryHash { get; set; } = 0;
            public int LastExpectedTriCount { get; set; } = -1;

            public TaoMatPhangOptions ToRebuildOptions(Database db)
            {
                return new TaoMatPhangOptions
                {
                    CreateNewSurface = true,
                    SourceEntityIds = ResolveHandleList(db, SourceHandles),
                    CustomOuterBoundaryIds = ResolveHandleList(db, CustomOuterHandles),
                    CustomHideBoundaryIds = ResolveHandleList(db, CustomHideHandles),
                    BoundaryMode = Enum.IsDefined(typeof(FeatureLineBoundaryMode), BoundaryMode)
                        ? (FeatureLineBoundaryMode)BoundaryMode
                        : FeatureLineBoundaryMode.AutoSmart,
                    MidOrdinateDistance = MidOrdinateDistance,
                    UseSupplementing = UseSupplementing,
                    SupplementingDistance = SupplementingDistance,
                    LimitMaxEdgeLength = LimitMaxEdgeLength,
                    MaxEdgeLength = MaxEdgeLength,
                    AutoCloseNearClosed = AutoCloseNearClosed,
                    AutoCloseTolerance = AutoCloseTolerance,
                    TreatNestedClosedAsHole = TreatNestedClosedAsHole,
                    AutoRebuild = AutoRebuild,
                    OverwriteOverlapOnExisting = OverwriteOverlapOnExisting
                };
            }
        }

        internal static FeatureLineSurfaceRecipe CreateRecipeFromOptions(
            Database db,
            TaoMatPhangOptions options,
            ObjectId parentSurfaceId)
        {
            var recipe = new FeatureLineSurfaceRecipe
            {
                Version = 1,
                SourceHandles = ExtractHandlesFromIds(options.SourceEntityIds),
                CustomOuterHandles = ExtractHandlesFromIds(options.CustomOuterBoundaryIds),
                CustomHideHandles = ExtractHandlesFromIds(options.CustomHideBoundaryIds),
                BoundaryMode = (int)options.BoundaryMode,
                MidOrdinateDistance = options.MidOrdinateDistance,
                UseSupplementing = options.UseSupplementing,
                SupplementingDistance = options.SupplementingDistance,
                LimitMaxEdgeLength = options.LimitMaxEdgeLength,
                MaxEdgeLength = options.MaxEdgeLength,
                AutoCloseNearClosed = options.AutoCloseNearClosed,
                AutoCloseTolerance = options.AutoCloseTolerance,
                TreatNestedClosedAsHole = options.TreatNestedClosedAsHole,
                AutoRebuild = options.AutoRebuild,
                OverwriteOverlapOnExisting = options.OverwriteOverlapOnExisting,
                ParentSurfaceHandle = (!parentSurfaceId.IsNull && parentSurfaceId.IsValid && !parentSurfaceId.IsErased)
                    ? parentSurfaceId.Handle.ToString()
                    : string.Empty
            };
            return recipe;
        }

        private static List<string> ExtractHandlesFromIds(IEnumerable<ObjectId>? ids)
        {
            var list = new List<string>();
            if (ids == null) return list;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ObjectId id in ids)
            {
                if (id.IsNull || !id.IsValid || id.IsErased) continue;
                string h = id.Handle.ToString();
                if (!string.IsNullOrEmpty(h) && seen.Add(h))
                {
                    list.Add(h);
                }
            }
            return list;
        }

        internal static ObjectId TryResolveEntityIdByHandle(Database db, string? handleHex)
        {
            if (db == null || string.IsNullOrWhiteSpace(handleHex))
                return ObjectId.Null;

            try
            {
                if (long.TryParse(handleHex.Trim(), System.Globalization.NumberStyles.HexNumber, null, out long val) && val > 0)
                {
                    if (db.TryGetObjectId(new Handle(val), out ObjectId id) &&
                        !id.IsNull && id.IsValid && !id.IsErased)
                    {
                        return id;
                    }
                }
            }
            catch { }

            return ObjectId.Null;
        }

        private static List<ObjectId> ResolveHandleList(Database db, IEnumerable<string>? handles)
        {
            var result = new List<ObjectId>();
            if (handles == null) return result;

            var seen = new HashSet<ObjectId>();
            foreach (string h in handles)
            {
                ObjectId id = TryResolveEntityIdByHandle(db, h);
                if (!id.IsNull && seen.Add(id))
                {
                    result.Add(id);
                }
            }
            return result;
        }

        internal static ulong ComputeRecipeGeometryHash(Database db, Transaction tr, FeatureLineSurfaceRecipe recipe)
        {
            const ulong fnvOffset = 14695981039346656037UL;
            const ulong fnvPrime = 1099511628211UL;
            ulong hash = fnvOffset;

            void AddLong(long v)
            {
                unchecked
                {
                    ulong uv = (ulong)v;
                    for (int b = 0; b < 8; b++)
                    {
                        hash ^= (uv & 0xFFUL);
                        hash *= fnvPrime;
                        uv >>= 8;
                    }
                }
            }

            void AddString(string? s)
            {
                if (string.IsNullOrEmpty(s))
                {
                    AddLong(0);
                    return;
                }
                for (int i = 0; i < s.Length; i++)
                {
                    unchecked
                    {
                        hash ^= s[i];
                        hash *= fnvPrime;
                    }
                }
            }

            AddLong(recipe.BoundaryMode);
            AddLong((long)Math.Round(recipe.MidOrdinateDistance * 1000.0));
            AddLong(recipe.UseSupplementing ? 1 : 0);
            AddLong((long)Math.Round(recipe.SupplementingDistance * 1000.0));
            AddLong(recipe.LimitMaxEdgeLength ? 1 : 0);
            AddLong((long)Math.Round(recipe.MaxEdgeLength * 1000.0));
            AddLong(recipe.AutoCloseNearClosed ? 1 : 0);
            AddLong((long)Math.Round(recipe.AutoCloseTolerance * 1000.0));
            AddLong(recipe.TreatNestedClosedAsHole ? 1 : 0);
            AddLong(recipe.OverwriteOverlapOnExisting ? 1 : 0);

            double midOrd = Math.Max(0.01, recipe.MidOrdinateDistance);
            double closeTol = recipe.AutoCloseNearClosed ? Math.Max(0.005, recipe.AutoCloseTolerance) : 0.005;

            void HashHandles(List<string>? handles, int listTag)
            {
                AddLong(listTag);
                if (handles == null) return;
                for (int i = 0; i < handles.Count; i++)
                {
                    string h = handles[i];
                    AddString(h);
                    ObjectId id = TryResolveEntityIdByHandle(db, h);
                    if (id.IsNull)
                    {
                        AddLong(-1);
                        continue;
                    }
                    SourceCurveInfo? info = ExtractSourceCurveInfo(id, tr, midOrd, closeTol, recipe.AutoCloseNearClosed, i);
                    if (info == null)
                    {
                        AddLong(-2);
                        continue;
                    }
                    AddLong(info.IsClosed ? 1 : 0);
                    AddLong(info.Points3D.Count);
                    for (int p = 0; p < info.Points3D.Count; p++)
                    {
                        Point3d pt = info.Points3D[p];
                        AddLong((long)Math.Round(pt.X * 1000.0));
                        AddLong((long)Math.Round(pt.Y * 1000.0));
                        AddLong((long)Math.Round(pt.Z * 1000.0));
                    }
                }
            }

            HashHandles(recipe.SourceHandles, 101);
            HashHandles(recipe.CustomOuterHandles, 102);
            HashHandles(recipe.CustomHideHandles, 103);

            return hash;
        }

        internal static bool IsSupportedSourceCurveId(ObjectId id)
        {
            if (id.IsNull || !id.IsValid || id.IsErased) return false;
            string dxf = id.ObjectClass?.DxfName ?? "";
            return dxf == "AECC_FEATURE_LINE" ||
                   dxf == "AECC_AUTO_CORRIDOR_FEATURE_LINE" ||
                   dxf == "POLYLINE" ||
                   dxf == "LWPOLYLINE" ||
                   dxf == "LINE" ||
                   dxf == "ARC";
        }

        private static ObjectId TryResolveComEntityId(object? comObj, Database db)
        {
            if (comObj == null || db == null) return ObjectId.Null;

            try
            {
                ObjectId id = DBObject.FromAcadObject(comObj);
                if (!id.IsNull && id.IsValid && !id.IsErased && id.Database == db)
                    return id;
            }
            catch { }

            try
            {
                string? handleHex = Convert.ToString(((dynamic)comObj).Handle);
                if (!string.IsNullOrWhiteSpace(handleHex))
                    return TryResolveEntityIdByHandle(db, handleHex);
            }
            catch { }

            return ObjectId.Null;
        }

        internal static List<ObjectId> ExtractSourceCurveIdsFromSurface(
            CivSurface surf,
            Database db,
            Transaction tr,
            bool allowGeometricFallback,
            out List<double[]> rawPointBreaklines,
            out List<ObjectId> customOuterIds,
            out List<ObjectId> customHideIds)
        {
            var foundIds = new List<ObjectId>();
            var seen = new HashSet<ObjectId>();
            rawPointBreaklines = new List<double[]>();
            customOuterIds = new List<ObjectId>();
            customHideIds = new List<ObjectId>();

            void AddCurveId(ObjectId id)
            {
                if (IsSupportedSourceCurveId(id) && seen.Add(id))
                    foundIds.Add(id);
            }

            try
            {
                dynamic acadSurf = surf.AcadObject;
                foreach (object brObj in (System.Collections.IEnumerable)acadSurf.Breaklines)
                {
                    try
                    {
                        object? ents = ((dynamic)brObj).BreaklineEntities;
                        if (ents is double[] dblArr)
                        {
                            if (dblArr.Length >= 6) rawPointBreaklines.Add(dblArr);
                        }
                        else if (ents is Array arr)
                        {
                            if (arr.Length >= 6 && arr.GetValue(0) is double)
                            {
                                var copy = new double[arr.Length];
                                for (int k = 0; k < arr.Length; k++)
                                    copy[k] = Convert.ToDouble(arr.GetValue(k));
                                rawPointBreaklines.Add(copy);
                            }
                            else
                            {
                                foreach (object? elem in arr)
                                {
                                    AddCurveId(TryResolveComEntityId(elem, db));
                                }
                            }
                        }
                        else if (ents != null)
                        {
                            AddCurveId(TryResolveComEntityId(ents, db));
                        }
                    }
                    catch { }
                }
            }
            catch { }

            try
            {
                dynamic acadSurf = surf.AcadObject;
                int bIdx = 0;
                foreach (object boObj in (System.Collections.IEnumerable)acadSurf.Boundaries)
                {
                    try
                    {
                        ObjectId bndEntId = TryResolveComEntityId(((dynamic)boObj).BoundaryEntity, db);
                        if (IsSupportedSourceCurveId(bndEntId))
                        {
                            SurfaceBoundaryType bType = SurfaceBoundaryType.Outer;
                            try
                            {
                                if (bIdx < surf.BoundariesDefinition.Count)
                                    bType = surf.BoundariesDefinition[bIdx].BoundaryType;
                            }
                            catch { }

                            if (bType == SurfaceBoundaryType.Hide)
                            {
                                if (!customHideIds.Contains(bndEntId)) customHideIds.Add(bndEntId);
                            }
                            else if (bType == SurfaceBoundaryType.Outer)
                            {
                                if (!customOuterIds.Contains(bndEntId)) customOuterIds.Add(bndEntId);
                            }
                        }
                    }
                    catch { }
                    bIdx++;
                }
            }
            catch { }

            // Fallback hình học cho các mặt phẳng lá cũ có Breakline nạp theo dạng Point3dCollection
            int bklDefCount = 0;
            try { bklDefCount = surf.BreaklinesDefinition.Count; } catch { }

            if (foundIds.Count == 0 && allowGeometricFallback && bklDefCount > 0)
            {
                const double cellSize = 2.0;
                var refCells = new Dictionary<(int, int), List<Point3d>>();
                int totalRefPts = 0;

                void AddRefPt(double x, double y, double z)
                {
                    var key = ((int)Math.Floor(x / cellSize), (int)Math.Floor(y / cellSize));
                    if (!refCells.TryGetValue(key, out var bucket))
                    {
                        bucket = new List<Point3d>(4);
                        refCells[key] = bucket;
                    }
                    bucket.Add(new Point3d(x, y, z));
                    totalRefPts++;
                }

                foreach (double[] dbl in rawPointBreaklines)
                {
                    for (int k = 0; k + 2 < dbl.Length; k += 3)
                        AddRefPt(dbl[k], dbl[k + 1], dbl[k + 2]);
                }

                try
                {
                    foreach (TinSurfaceVertex v in surf.Vertices)
                    {
                        if (v != null && v.IsValid)
                        {
                            Point3d p = v.Location;
                            AddRefPt(p.X, p.Y, p.Z);
                        }
                    }
                }
                catch { }

                bool HasNearbyRefPt(Point3d p)
                {
                    int gx = (int)Math.Floor(p.X / cellSize);
                    int gy = (int)Math.Floor(p.Y / cellSize);
                    const double tol2DSq = 0.03 * 0.03;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            if (!refCells.TryGetValue((gx + dx, gy + dy), out var bucket)) continue;
                            for (int i = 0; i < bucket.Count; i++)
                            {
                                Point3d r = bucket[i];
                                double dSq = (r.X - p.X) * (r.X - p.X) + (r.Y - p.Y) * (r.Y - p.Y);
                                if (dSq <= tol2DSq && Math.Abs(r.Z - p.Z) <= 0.05)
                                    return true;
                            }
                        }
                    }
                    return false;
                }

                if (totalRefPts >= 2 &&
                    tr.GetObject(db.BlockTableId, OpenMode.ForRead) is BlockTable bt &&
                    tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) is BlockTableRecord ms)
                {
                    foreach (ObjectId entId in ms)
                    {
                        if (!IsSupportedSourceCurveId(entId)) continue;
                        SourceCurveInfo? info = ExtractSourceCurveInfo(entId, tr, 0.10, 0.10, true, 0);
                        if (info == null || info.Points3D.Count < 2) continue;

                        int matched = 0;
                        for (int i = 0; i < info.Points3D.Count; i++)
                        {
                            if (HasNearbyRefPt(info.Points3D[i]))
                                matched++;
                        }

                        int required = Math.Max(2, (int)Math.Ceiling(info.Points3D.Count * 0.90));
                        if (matched >= required)
                        {
                            if (info.Points3D.Count == 2)
                            {
                                double mx = 0.5 * (info.Points3D[0].X + info.Points3D[1].X);
                                double my = 0.5 * (info.Points3D[0].Y + info.Points3D[1].Y);
                                double mz = 0.5 * (info.Points3D[0].Z + info.Points3D[1].Z);
                                try
                                {
                                    double sz = surf.FindElevationAtXY(mx, my);
                                    if (Math.Abs(sz - mz) > 0.08) continue;
                                }
                                catch { continue; }
                            }
                            AddCurveId(entId);
                        }
                    }
                }
            }

            return foundIds;
        }
    }
}

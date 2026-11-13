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
        internal static void SaveSurfaceRecipe(CivSurface surface, Transaction tr, FeatureLineSurfaceRecipe recipe)
        {
            if (surface == null || surface.IsErased || recipe == null) return;

            try
            {
                if (surface.Database != null)
                {
                    recipe.LastGeometryHash = ComputeRecipeGeometryHash(surface.Database, tr, recipe);
                }
                recipe.LastExpectedTriCount = CountVisibleTriangles(surface);

                if (!surface.IsWriteEnabled)
                    surface.UpgradeOpen();

                ObjectId extDictId = surface.ExtensionDictionary;
                if (extDictId.IsNull)
                {
                    surface.CreateExtensionDictionary();
                    extDictId = surface.ExtensionDictionary;
                }
                if (extDictId.IsNull) return;

                if (tr.GetObject(extDictId, OpenMode.ForWrite) is not DBDictionary extDict)
                    return;

                string json = JsonSerializer.Serialize(recipe);
                using (var rb = new ResultBuffer())
                {
                    const int chunkLen = 200;
                    for (int offset = 0; offset < json.Length; offset += chunkLen)
                    {
                        int len = Math.Min(chunkLen, json.Length - offset);
                        rb.Add(new TypedValue((int)DxfCode.Text, json.Substring(offset, len)));
                    }

                    if (extDict.Contains(FlSurfaceRecipeDictKey))
                    {
                        ObjectId recId = extDict.GetAt(FlSurfaceRecipeDictKey);
                        if (!recId.IsNull && recId.IsValid && !recId.IsErased &&
                            tr.GetObject(recId, OpenMode.ForWrite) is Xrecord existingXrec)
                        {
                            existingXrec.Data = rb;
                            return;
                        }
                    }

                    using (var xrec = new Xrecord())
                    {
                        xrec.Data = rb;
                        extDict.SetAt(FlSurfaceRecipeDictKey, xrec);
                        tr.AddNewlyCreatedDBObject(xrec, true);
                    }
                }
            }
            catch { }
        }

        internal static bool TryReadSurfaceRecipe(CivSurface surface, Transaction tr, out FeatureLineSurfaceRecipe? recipe)
        {
            recipe = null;
            if (surface == null || surface.IsErased) return false;

            try
            {
                ObjectId extDictId = surface.ExtensionDictionary;
                if (extDictId.IsNull || !extDictId.IsValid || extDictId.IsErased)
                    return false;

                if (tr.GetObject(extDictId, OpenMode.ForRead) is not DBDictionary extDict)
                    return false;

                if (!extDict.Contains(FlSurfaceRecipeDictKey))
                    return false;

                ObjectId recId = extDict.GetAt(FlSurfaceRecipeDictKey);
                if (recId.IsNull || !recId.IsValid || recId.IsErased)
                    return false;

                if (tr.GetObject(recId, OpenMode.ForRead) is not Xrecord xrec || xrec.Data == null)
                    return false;

                var sb = new StringBuilder();
                foreach (TypedValue tv in xrec.Data)
                {
                    if (tv.TypeCode == (int)DxfCode.Text && tv.Value is string chunk)
                    {
                        sb.Append(chunk);
                    }
                }

                string json = sb.ToString();
                if (string.IsNullOrWhiteSpace(json)) return false;

                recipe = JsonSerializer.Deserialize<FeatureLineSurfaceRecipe>(json);
                return recipe != null;
            }
            catch
            {
                return false;
            }
        }

        internal static void RemoveSurfaceRecipe(CivSurface surface, Transaction tr)
        {
            if (surface == null || surface.IsErased) return;

            try
            {
                ObjectId extDictId = surface.ExtensionDictionary;
                if (extDictId.IsNull || !extDictId.IsValid || extDictId.IsErased)
                    return;

                if (tr.GetObject(extDictId, OpenMode.ForWrite) is DBDictionary extDict &&
                    extDict.Contains(FlSurfaceRecipeDictKey))
                {
                    ObjectId recId = extDict.GetAt(FlSurfaceRecipeDictKey);
                    extDict.Remove(FlSurfaceRecipeDictKey);
                    if (!recId.IsNull && recId.IsValid && !recId.IsErased &&
                        tr.GetObject(recId, OpenMode.ForWrite) is DBObject obj)
                    {
                        obj.Erase(true);
                    }
                }
            }
            catch { }
        }

        internal static bool RebuildSurfaceFromRecipeInternal(
            CivilDocument civDoc,
            Database db,
            Transaction tr,
            Editor ed,
            CivSurface targetSurface,
            FeatureLineSurfaceRecipe recipe,
            CivSurface? parentSurfaceOverride,
            out int validCurveCount,
            out int newTriCount)
        {
            validCurveCount = 0;
            newTriCount = 0;

            TaoMatPhangOptions rebuildOpts = recipe.ToRebuildOptions(db);
            double midOrd = Math.Max(0.01, rebuildOpts.MidOrdinateDistance);
            double suppDist = rebuildOpts.UseSupplementing ? Math.Max(0.20, rebuildOpts.SupplementingDistance) : 100000.0;
            double closeTol = rebuildOpts.AutoCloseNearClosed ? Math.Max(0.005, rebuildOpts.AutoCloseTolerance) : 0.005;

            var sourceCurves = new List<SourceCurveInfo>();
            var seenIds = new HashSet<ObjectId>();
            foreach (ObjectId rawId in rebuildOpts.SourceEntityIds)
            {
                if (rawId.IsNull || !rawId.IsValid || rawId.IsErased || !seenIds.Add(rawId))
                    continue;

                SourceCurveInfo? info = ExtractSourceCurveInfo(rawId, tr, midOrd, closeTol, rebuildOpts.AutoCloseNearClosed, sourceCurves.Count);
                if (info != null && info.Points3D.Count >= 2)
                {
                    sourceCurves.Add(info);
                }
            }

            validCurveCount = sourceCurves.Count;
            if (sourceCurves.Count == 0)
            {
                return false;
            }

            if (sourceCurves.Count == 1 && !sourceCurves[0].IsClosed && sourceCurves[0].Points3D.Count < 3)
            {
                return false;
            }

            bool wasLocked = false;
            try
            {
                wasLocked = targetSurface.Lock;
                if (wasLocked) targetSurface.Lock = false;
            }
            catch { }

            try
            {
                // Nếu cần giữ nguyên mặt phẳng cũ tại vùng chồng lấn -> tạm tắt Paste của chính mặt con này trên mặt cha để chụp nền cũ
                CivSurface? parentSurf = parentSurfaceOverride;
                if (parentSurf == null && !recipe.OverwriteOverlapOnExisting && !string.IsNullOrWhiteSpace(recipe.ParentSurfaceHandle))
                {
                    ObjectId pId = TryResolveEntityIdByHandle(db, recipe.ParentSurfaceHandle);
                    if (!pId.IsNull && pId != targetSurface.ObjectId)
                    {
                        parentSurf = tr.GetObject(pId, OpenMode.ForWrite) as CivSurface;
                    }
                }

                SpatialTriangleGrid2D? oldTriGrid = null;
                if (!recipe.OverwriteOverlapOnExisting && parentSurf != null)
                {
                    bool wasParentLocked = false;
                    try
                    {
                        wasParentLocked = parentSurf.Lock;
                        if (wasParentLocked) parentSurf.Lock = false;
                    }
                    catch { }

                    SurfaceOperationPasteSurface? pasteOp = FindPasteOperationForSubSurface(parentSurf, targetSurface.ObjectId);
                    bool tempDisabledPaste = false;
                    try
                    {
                        if (pasteOp != null && pasteOp.Enabled)
                        {
                            pasteOp.Enabled = false;
                            parentSurf.Rebuild();
                            tempDisabledPaste = true;
                        }

                        if (CountVisibleTriangles(parentSurf) > 0)
                        {
                            TinTopologyGraph oldGraph = BuildTinTopologyGraph(parentSurf);
                            oldTriGrid = new SpatialTriangleGrid2D(oldGraph);
                        }
                    }
                    catch { }
                    finally
                    {
                        if (tempDisabledPaste && pasteOp != null)
                        {
                            try { pasteOp.Enabled = true; } catch { }
                        }
                        if (wasParentLocked)
                        {
                            try { parentSurf.Lock = true; } catch { }
                        }
                    }
                }

                ClearSurfaceDefinitionsAndEdits(targetSurface);

                bool populatedOk = PopulateCleanFeatureLineSurface(
                    targetSurface,
                    rebuildOpts,
                    tr,
                    ed,
                    sourceCurves,
                    midOrd,
                    suppDist,
                    closeTol,
                    usePointsOnlyBreaklines: false,
                    out int addedBreaklines,
                    out _,
                    out _);

                if (!populatedOk || addedBreaklines == 0)
                {
                    return false;
                }

                if (!recipe.OverwriteOverlapOnExisting && oldTriGrid != null)
                {
                    TinTopologyGraph subGraph = BuildTinTopologyGraph(targetSurface);
                    if (subGraph.Triangles.Count > 0)
                    {
                        bool[] subRemoved = new bool[subGraph.Triangles.Count];
                        bool anyMarked = false;
                        for (int t = 0; t < subGraph.Triangles.Count; t++)
                        {
                            TinTriangleNode tri = subGraph.Triangles[t];
                            Point2d c = tri.Centroid2D;
                            Point3d p0 = subGraph.Vertices[tri.V0].Pos;
                            Point3d p1 = subGraph.Vertices[tri.V1].Pos;
                            Point3d p2 = subGraph.Vertices[tri.V2].Pos;

                            if (oldTriGrid.ContainsPoint(c.X, c.Y, -1e-4) &&
                                oldTriGrid.ContainsPoint(p0.X, p0.Y, 0.01) &&
                                oldTriGrid.ContainsPoint(p1.X, p1.Y, 0.01) &&
                                oldTriGrid.ContainsPoint(p2.X, p2.Y, 0.01))
                            {
                                subRemoved[t] = true;
                                anyMarked = true;
                            }
                        }

                        if (anyMarked)
                        {
                            ApplyRemovedTrianglesToSurface(targetSurface, subGraph, subRemoved, midOrd, ed);
                        }

                        try
                        {
                            foreach (TinSurfaceVertex v in targetSurface.Vertices)
                            {
                                if (v == null || !v.IsValid) continue;
                                Point3d loc = v.Location;
                                if (oldTriGrid.TryInterpolateZ(loc.X, loc.Y, out double oldZ, 0.015) &&
                                    Math.Abs(loc.Z - oldZ) > 0.002)
                                {
                                    try { targetSurface.SetVertexElevation(v, oldZ); } catch { }
                                }
                            }
                        }
                        catch { }
                    }
                }

                SetAutoRebuildSafe(targetSurface, recipe.AutoRebuild);
                newTriCount = CountVisibleTriangles(targetSurface);
                if (newTriCount > 0)
                {
                    SaveSurfaceRecipe(targetSurface, tr, recipe);
                }
                return newTriCount > 0;
            }
            finally
            {
                if (wasLocked)
                {
                    try { targetSurface.Lock = true; } catch { }
                }
            }
        }
    }
}

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
        internal static bool HasAnyPasteSurfaceOperation(CivSurface surface)
        {
            try
            {
                for (int i = 0; i < surface.Operations.Count; i++)
                {
                    if (surface.Operations[i] is SurfaceOperationPasteSurface)
                        return true;
                }
            }
            catch { }
            return false;
        }

        internal static bool IsPurePasteCompositeSurface(CivSurface surface)
        {
            try
            {
                if (!HasAnyPasteSurfaceOperation(surface))
                    return false;

                if (surface.BreaklinesDefinition.Count > 0) return false;
                if (surface.PointGroupsDefinition.Count > 0) return false;
                if (surface.PointFilesDefinition.Count > 0) return false;
                if (surface.ContoursDefinition.Count > 0) return false;
                if (surface.DEMFilesDefinition.Count > 0) return false;
                if (surface.DrawingObjectsDefinition.Count > 0) return false;
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static bool NormalizeFlMasterSurfaceToPurePasteParent(
            CivilDocument civDoc,
            Database db,
            Transaction tr,
            Editor? ed,
            ObjectId targetSurfaceId,
            CivSurface targetSurface,
            ObjectId subStyleId,
            bool autoRebuild,
            out ObjectId createdBaseSubId)
        {
            createdBaseSubId = ObjectId.Null;
            try
            {
                SurfaceOriginationType origType = SurfaceOriginationType.None;
                try { origType = targetSurface.OriginationType; } catch { }
                if (origType != SurfaceOriginationType.None) return false;

                if (targetSurface.PointGroupsDefinition.Count > 0 ||
                    targetSurface.PointFilesDefinition.Count > 0 ||
                    targetSurface.ContoursDefinition.Count > 0 ||
                    targetSurface.DEMFilesDefinition.Count > 0 ||
                    targetSurface.DrawingObjectsDefinition.Count > 0)
                {
                    return false;
                }

                string parentName = targetSurface.Name;
                bool hasPaste = HasAnyPasteSurfaceOperation(targetSurface);

                if (!hasPaste)
                {
                    if (!TryReadSurfaceRecipe(targetSurface, tr, out FeatureLineSurfaceRecipe? baseRecipe) || baseRecipe == null)
                    {
                        if (targetSurface.BreaklinesDefinition.Count == 0)
                            return false;

                        List<ObjectId> legacySrcIds = ExtractSourceCurveIdsFromSurface(
                            targetSurface, db, tr, allowGeometricFallback: true,
                            out _, out List<ObjectId> cOuter, out List<ObjectId> cHide);
                        if (legacySrcIds.Count == 0)
                            return false;

                        baseRecipe = new FeatureLineSurfaceRecipe
                        {
                            Version = 1,
                            SourceHandles = ExtractHandlesFromIds(legacySrcIds),
                            CustomOuterHandles = ExtractHandlesFromIds(cOuter),
                            CustomHideHandles = ExtractHandlesFromIds(cHide),
                            BoundaryMode = (cOuter.Count > 0 || cHide.Count > 0)
                                ? (int)FeatureLineBoundaryMode.CustomBoundaryEntities
                                : (int)FeatureLineBoundaryMode.AutoSmart,
                            AutoRebuild = autoRebuild
                        };
                    }

                    string firstSubName = EnsureUniquePrefixedSubSurfaceName(civDoc, tr, parentName, "Phu_1");
                    ObjectId firstSubId = TinSurface.Create(firstSubName, subStyleId);
                    if (firstSubId.IsNull || !firstSubId.IsValid) return false;

                    if (tr.GetObject(firstSubId, OpenMode.ForWrite) is not CivSurface firstSub)
                        return false;

                    bool ok = RebuildSurfaceFromRecipeInternal(
                        civDoc,
                        db,
                        tr,
                        ed ?? Application.DocumentManager.MdiActiveDocument.Editor,
                        firstSub,
                        baseRecipe,
                        parentSurfaceOverride: null,
                        out _,
                        out _);

                    if (!ok)
                    {
                        try { firstSub.Erase(true); } catch { }
                        return false;
                    }

                    baseRecipe.ParentSurfaceHandle = targetSurface.Handle.ToString();
                    SetAutoRebuildSafe(firstSub, autoRebuild);
                    SaveSurfaceRecipe(firstSub, tr, baseRecipe);

                    RemoveSurfaceRecipe(targetSurface, tr);
                    ClearSurfaceDefinitionsAndEdits(targetSurface);
                    targetSurface.PasteSurface(firstSubId);
                    targetSurface.Rebuild();
                    SetAutoRebuildSafe(targetSurface, autoRebuild);

                    createdBaseSubId = firstSubId;
                    ed?.WriteMessage($"\n  • Chuẩn hóa gốc  : Tự động tách dữ liệu gốc '{parentName}' thành '{firstSub.Name}' để mặt tổng '{parentName}' thuần PasteSurface (không bị kẹp biên cũ).");
                    return true;
                }

                // Nhánh B: Mặt phẳng tổng ĐÃ có PasteSurface nhưng vẫn còn Breakline thừa (biên tĩnh cũ / FL gốc) hoặc Outer Boundary gây kẹp biên
                bool hasBkl = false;
                try { hasBkl = targetSurface.BreaklinesDefinition.Count > 0; } catch { }
                bool hasOuter = HasRestrictiveOuterBoundary(targetSurface);
                if (!hasBkl && !hasOuter)
                    return false;

                var pasteChildren = new List<(ObjectId ChildId, bool Enabled)>();
                try
                {
                    for (int i = 0; i < targetSurface.Operations.Count; i++)
                    {
                        if (targetSurface.Operations[i] is SurfaceOperationPasteSurface pasteOp &&
                            !pasteOp.SurfaceId.IsNull &&
                            pasteOp.SurfaceId.IsValid &&
                            !pasteOp.SurfaceId.IsErased &&
                            pasteOp.SurfaceId != targetSurfaceId)
                        {
                            pasteChildren.Add((pasteOp.SurfaceId, pasteOp.Enabled));
                        }
                    }
                }
                catch { }

                if (pasteChildren.Count == 0)
                    return false;

                var childManagedIds = new HashSet<ObjectId>();
                foreach (var (childId, _) in pasteChildren)
                {
                    if (tr.GetObject(childId, OpenMode.ForRead) is not CivSurface childSurf) continue;
                    if (TryReadSurfaceRecipe(childSurf, tr, out FeatureLineSurfaceRecipe? cRecipe) && cRecipe != null)
                    {
                        foreach (ObjectId sId in ResolveHandleList(db, cRecipe.SourceHandles))
                            childManagedIds.Add(sId);
                    }
                    else
                    {
                        foreach (ObjectId sId in ExtractSourceCurveIdsFromSurface(childSurf, db, tr, allowGeometricFallback: true, out _, out _, out _))
                            childManagedIds.Add(sId);
                    }
                }

                List<ObjectId> directIds = ExtractSourceCurveIdsFromSurface(
                    targetSurface, db, tr, allowGeometricFallback: false,
                    out _, out List<ObjectId> directOuter, out List<ObjectId> directHide);
                var unpromotedDirectIds = directIds.Where(id => !childManagedIds.Contains(id)).ToList();

                if (unpromotedDirectIds.Count > 0)
                {
                    string gocName = EnsureUniquePrefixedSubSurfaceName(civDoc, tr, parentName, "Goc");
                    ObjectId gocId = TinSurface.Create(gocName, subStyleId);
                    if (!gocId.IsNull && gocId.IsValid &&
                        tr.GetObject(gocId, OpenMode.ForWrite) is CivSurface gocSurf)
                    {
                        var gocRecipe = new FeatureLineSurfaceRecipe
                        {
                            Version = 1,
                            SourceHandles = ExtractHandlesFromIds(unpromotedDirectIds),
                            CustomOuterHandles = ExtractHandlesFromIds(directOuter),
                            CustomHideHandles = ExtractHandlesFromIds(directHide),
                            BoundaryMode = (directOuter.Count > 0 || directHide.Count > 0)
                                ? (int)FeatureLineBoundaryMode.CustomBoundaryEntities
                                : (int)FeatureLineBoundaryMode.AutoSmart,
                            AutoRebuild = autoRebuild,
                            ParentSurfaceHandle = targetSurface.Handle.ToString()
                        };

                        bool gocOk = RebuildSurfaceFromRecipeInternal(
                            civDoc,
                            db,
                            tr,
                            ed ?? Application.DocumentManager.MdiActiveDocument.Editor,
                            gocSurf,
                            gocRecipe,
                            parentSurfaceOverride: null,
                            out _,
                            out _);

                        if (gocOk)
                        {
                            SetAutoRebuildSafe(gocSurf, autoRebuild);
                            SaveSurfaceRecipe(gocSurf, tr, gocRecipe);
                            createdBaseSubId = gocId;
                            pasteChildren.Insert(0, (gocId, true));
                        }
                        else
                        {
                            try { gocSurf.Erase(true); } catch { }
                        }
                    }
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
                    RemoveSurfaceRecipe(targetSurface, tr);
                    ClearSurfaceDefinitionsAndEdits(targetSurface);

                    foreach (var (childId, enabled) in pasteChildren)
                    {
                        try
                        {
                            targetSurface.PasteSurface(childId);
                            if (!enabled)
                            {
                                SurfaceOperationPasteSurface? pOp = FindPasteOperationForSubSurface(targetSurface, childId);
                                if (pOp != null) pOp.Enabled = false;
                            }
                        }
                        catch { }
                    }

                    targetSurface.Rebuild();
                    SetAutoRebuildSafe(targetSurface, autoRebuild);
                    ed?.WriteMessage($"\n  • Chuẩn hóa tổng : Đã dọn sạch Breakline/Boundary tĩnh thừa trên '{parentName}' thành mặt phẳng tổng thuần PasteSurface.");
                    return true;
                }
                finally
                {
                    if (wasLocked)
                    {
                        try { targetSurface.Lock = true; } catch { }
                    }
                }
            }
            catch
            {
                return false;
            }
        }
    }
}

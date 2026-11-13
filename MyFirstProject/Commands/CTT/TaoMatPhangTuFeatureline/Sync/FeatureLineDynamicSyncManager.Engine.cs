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
    public static partial class FeatureLineDynamicSyncManager
    {
        /// <summary>
        /// Đồng bộ thủ công toàn bộ các mặt phẳng tạo từ Feature Line (và chuỗi mặt phẳng tổng PasteSurface) trong bản vẽ hiện hành.
        /// </summary>
        public static bool SyncAllInDocument(
            Database db,
            Editor ed,
            bool forceAll,
            out int updatedSubSurfaceCount,
            out int rebuiltParentCount)
        {
            updatedSubSurfaceCount = 0;
            rebuiltParentCount = 0;

            Document? targetDoc = null;
            foreach (Document d in Application.DocumentManager)
            {
                if (d != null && !d.IsDisposed && d.Database == db)
                {
                    targetDoc = d;
                    break;
                }
            }

            if (targetDoc == null)
                targetDoc = Application.DocumentManager.MdiActiveDocument;

            if (targetDoc == null || targetDoc.IsDisposed)
                return false;

            HashSet<string>? queuedHandles = null;
            HashSet<string>? queuedSurfHandles = null;
            IntPtr dbPtr = targetDoc.Database.UnmanagedObject;
            lock (_syncLock)
            {
                _pendingLegacyScanDbPtrs.Remove(dbPtr);
                if (_pendingHandlesByDb.TryGetValue(dbPtr, out var qh) && qh.Count > 0)
                {
                    queuedHandles = new HashSet<string>(qh, StringComparer.OrdinalIgnoreCase);
                    _pendingHandlesByDb.Remove(dbPtr);
                }
                if (_pendingSurfaceHandlesByDb.TryGetValue(dbPtr, out var qs) && qs.Count > 0)
                {
                    queuedSurfHandles = new HashSet<string>(qs, StringComparer.OrdinalIgnoreCase);
                    _pendingSurfaceHandlesByDb.Remove(dbPtr);
                }
            }

            return ProcessSyncForDocument(
                targetDoc,
                runLegacyDiscovery: true,
                modifiedSourceHandles: queuedHandles,
                modifiedSurfaceHandles: queuedSurfHandles,
                forceAll: forceAll,
                out updatedSubSurfaceCount,
                out rebuiltParentCount,
                checkAllRecipeHashes: !forceAll);
        }

        private static bool ProcessSyncForDocument(
            Document doc,
            bool runLegacyDiscovery,
            HashSet<string>? modifiedSourceHandles,
            HashSet<string>? modifiedSurfaceHandles,
            bool forceAll,
            out int updatedSubCount,
            out int rebuiltParentCount,
            bool checkAllRecipeHashes = false)
        {
            updatedSubCount = 0;
            rebuiltParentCount = 0;
            if (doc == null || doc.IsDisposed) return false;

            Database db = doc.Database;
            Editor ed = doc.Editor;
            IntPtr dbPtr = db.UnmanagedObject;

            lock (_syncLock)
            {
                _scannedLegacyDbPtrs.Add(dbPtr);
            }

            _isInternalRebuilding = true;
            try
            {
                using (DocumentLock docLock = doc.LockDocument())
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    CivilDocument? civDoc = CivilDocument.GetCivilDocument(db);
                    if (civDoc == null)
                    {
                        tr.Commit();
                        return false;
                    }

                    // 1. Tự động phát hiện, gắn Recipe cho mặt phẳng FL cũ & chuẩn hóa mặt tổng lai có Breakline/Outer thừa
                    var healSurfaceIds = new HashSet<ObjectId>();
                    var dirtyParentIds = new HashSet<ObjectId>();
                    if (runLegacyDiscovery)
                    {
                        DiscoverAndAttachLegacyFlRecipes(civDoc, db, tr, ed, healSurfaceIds, dirtyParentIds);
                    }

                    // 2. Xác định các mặt phẳng nguồn FL bị ảnh hưởng và tái tạo chính xác lưới + ranh giới
                    var dirtySurfaceIds = new HashSet<ObjectId>();

                    foreach (ObjectId surfId in civDoc.GetSurfaceIds())
                    {
                        if (surfId.IsNull || !surfId.IsValid || surfId.IsErased) continue;
                        if (tr.GetObject(surfId, OpenMode.ForRead) is not CivSurface surf) continue;

                        if (!CTT_Tao_Mat_Phang_Tu_Featureline_Command.TryReadSurfaceRecipe(
                                surf, tr, out CTT_Tao_Mat_Phang_Tu_Featureline_Command.FeatureLineSurfaceRecipe? recipe) ||
                            recipe == null)
                        {
                            continue;
                        }

                        bool shouldRebuild = forceAll || healSurfaceIds.Contains(surfId);
                        if (!shouldRebuild && recipe.AutoRebuild)
                        {
                            bool triggeredBySource = false;
                            if (modifiedSourceHandles != null && modifiedSourceHandles.Count > 0)
                            {
                                for (int i = 0; i < recipe.SourceHandles.Count; i++)
                                {
                                    if (modifiedSourceHandles.Contains(recipe.SourceHandles[i]))
                                    {
                                        triggeredBySource = true;
                                        break;
                                    }
                                }
                                if (!triggeredBySource)
                                {
                                    for (int i = 0; i < recipe.CustomOuterHandles.Count; i++)
                                    {
                                        if (modifiedSourceHandles.Contains(recipe.CustomOuterHandles[i]))
                                        {
                                            triggeredBySource = true;
                                            break;
                                        }
                                    }
                                }
                                if (!triggeredBySource)
                                {
                                    for (int i = 0; i < recipe.CustomHideHandles.Count; i++)
                                    {
                                        if (modifiedSourceHandles.Contains(recipe.CustomHideHandles[i]))
                                        {
                                            triggeredBySource = true;
                                            break;
                                        }
                                    }
                                }
                            }

                            bool triggeredBySurface = modifiedSurfaceHandles != null &&
                                                      modifiedSurfaceHandles.Contains(surf.Handle.ToString());

                            if (checkAllRecipeHashes || triggeredBySource || triggeredBySurface)
                            {
                                ulong curHash = CTT_Tao_Mat_Phang_Tu_Featureline_Command.ComputeRecipeGeometryHash(db, tr, recipe);
                                int curTris = CTT_Tao_Mat_Phang_Tu_Featureline_Command.CountVisibleTriangles(surf);
                                if (curHash != recipe.LastGeometryHash ||
                                    curTris != recipe.LastExpectedTriCount ||
                                    curTris == 0)
                                {
                                    shouldRebuild = true;
                                }
                            }
                        }

                        if (!shouldRebuild) continue;

                        surf.UpgradeOpen();
                        if (CTT_Tao_Mat_Phang_Tu_Featureline_Command.RebuildSurfaceFromRecipeInternal(
                                civDoc, db, tr, ed, surf, recipe, parentSurfaceOverride: null, out _, out _))
                        {
                            updatedSubCount++;
                            dirtySurfaceIds.Add(surfId);
                        }
                    }

                    // 3. Cascade Rebuild toàn bộ chuỗi mặt phẳng tổng (PasteSurface) theo đúng thứ tự phụ thuộc
                    if (dirtySurfaceIds.Count > 0 || dirtyParentIds.Count > 0)
                    {
                        rebuiltParentCount = RebuildDownstreamPasteParentsInOrder(civDoc, tr, ed, dirtySurfaceIds, dirtyParentIds);
                    }

                    tr.Commit();
                }

                if (updatedSubCount > 0 || rebuiltParentCount > 0)
                {
                    try { ed.UpdateScreen(); } catch { }
                    if (!forceAll)
                    {
                        ed.WriteMessage($"\n🔄 [Đồng bộ động FL] Đã cập nhật {updatedSubCount} mặt phẳng phụ & {rebuiltParentCount} mặt phẳng tổng theo ranh giới Feature Line mới.");
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                _isInternalRebuilding = false;
            }
        }

        private static void DiscoverAndAttachLegacyFlRecipes(
            CivilDocument civDoc,
            Database db,
            Transaction tr,
            Editor ed,
            HashSet<ObjectId> healSurfaceIds,
            HashSet<ObjectId> dirtyParentIds)
        {
            try
            {
                var leafCandidateIds = new List<ObjectId>();
                var hybridParentIds = new List<ObjectId>();
                var parentByChild = new Dictionary<ObjectId, ObjectId>();

                foreach (ObjectId sId in civDoc.GetSurfaceIds())
                {
                    if (sId.IsNull || !sId.IsValid || sId.IsErased) continue;
                    if (tr.GetObject(sId, OpenMode.ForRead) is not CivSurface surf) continue;

                    try
                    {
                        for (int i = 0; i < surf.Operations.Count; i++)
                        {
                            if (surf.Operations[i] is SurfaceOperationPasteSurface pasteOp &&
                                pasteOp.Enabled &&
                                !pasteOp.SurfaceId.IsNull &&
                                !parentByChild.ContainsKey(pasteOp.SurfaceId))
                            {
                                parentByChild[pasteOp.SurfaceId] = sId;
                            }
                        }
                    }
                    catch { }

                    SurfaceOriginationType origType = SurfaceOriginationType.None;
                    try { origType = surf.OriginationType; } catch { }
                    if (origType != SurfaceOriginationType.None) continue;

                    if (surf.PointGroupsDefinition.Count > 0 ||
                        surf.PointFilesDefinition.Count > 0 ||
                        surf.DEMFilesDefinition.Count > 0 ||
                        surf.ContoursDefinition.Count > 0 ||
                        surf.DrawingObjectsDefinition.Count > 0)
                    {
                        continue;
                    }

                    bool hasPaste = CTT_Tao_Mat_Phang_Tu_Featureline_Command.HasAnyPasteSurfaceOperation(surf);
                    if (hasPaste)
                    {
                        bool hasBkl = false;
                        try { hasBkl = surf.BreaklinesDefinition.Count > 0; } catch { }
                        if (hasBkl || CTT_Tao_Mat_Phang_Tu_Featureline_Command.HasRestrictiveOuterBoundary(surf))
                        {
                            hybridParentIds.Add(sId);
                        }
                        continue;
                    }

                    if (CTT_Tao_Mat_Phang_Tu_Featureline_Command.TryReadSurfaceRecipe(surf, tr, out _))
                        continue;

                    if (surf.BreaklinesDefinition.Count == 0) continue;
                    leafCandidateIds.Add(sId);
                }

                // Pass 1: Khôi phục & gắn Recipe cho các mặt phẳng lá tạo từ Feature Line (qua COM + fallback hình học)
                foreach (ObjectId sId in leafCandidateIds)
                {
                    if (tr.GetObject(sId, OpenMode.ForRead) is not CivSurface surf) continue;

                    List<ObjectId> sourceIds = CTT_Tao_Mat_Phang_Tu_Featureline_Command.ExtractSourceCurveIdsFromSurface(
                        surf,
                        db,
                        tr,
                        allowGeometricFallback: true,
                        out _,
                        out List<ObjectId> customOuterIds,
                        out List<ObjectId> customHideIds);

                    if (sourceIds.Count == 0) continue;

                    surf.UpgradeOpen();
                    bool customBnd = customOuterIds.Count > 0 || customHideIds.Count > 0;
                    var recipe = new CTT_Tao_Mat_Phang_Tu_Featureline_Command.FeatureLineSurfaceRecipe
                    {
                        Version = 1,
                        SourceHandles = sourceIds.Select(id => id.Handle.ToString()).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                        CustomOuterHandles = customOuterIds.Select(id => id.Handle.ToString()).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                        CustomHideHandles = customHideIds.Select(id => id.Handle.ToString()).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                        BoundaryMode = customBnd
                            ? (int)FeatureLineBoundaryMode.CustomBoundaryEntities
                            : (int)FeatureLineBoundaryMode.AutoSmart,
                        MidOrdinateDistance = 0.10,
                        UseSupplementing = true,
                        SupplementingDistance = 2.00,
                        LimitMaxEdgeLength = true,
                        MaxEdgeLength = 25.00,
                        AutoCloseNearClosed = true,
                        AutoCloseTolerance = 0.10,
                        TreatNestedClosedAsHole = false,
                        AutoRebuild = true,
                        OverwriteOverlapOnExisting = true,
                        ParentSurfaceHandle = parentByChild.TryGetValue(sId, out ObjectId pId) ? pId.Handle.ToString() : string.Empty
                    };

                    try { recipe.AutoRebuild = surf.AutoRebuild; } catch { }

                    CTT_Tao_Mat_Phang_Tu_Featureline_Command.SaveSurfaceRecipe(surf, tr, recipe);

                    int curTris = CTT_Tao_Mat_Phang_Tu_Featureline_Command.CountVisibleTriangles(surf);
                    int bndCount = 0;
                    try { bndCount = surf.BoundariesDefinition.Count; } catch { }
                    if (curTris == 0 || (bndCount > 0 && customOuterIds.Count == 0 && customHideIds.Count == 0))
                    {
                        healSurfaceIds.Add(sId);
                    }
                }

                // Pass 2: Chuẩn hóa các mặt tổng lai (ví dụ 'Taluy' vừa Paste vừa còn Breakline biên tĩnh cũ / Outer Boundary kẹp biên)
                foreach (ObjectId parentId in hybridParentIds)
                {
                    if (tr.GetObject(parentId, OpenMode.ForWrite) is not CivSurface parentSurf) continue;
                    ObjectId subStyleId = CTT_Tao_Mat_Phang_Tu_Featureline_Command.ResolveSubSurfaceStyleId(
                        civDoc, tr, "_No Display", parentSurf.StyleId, out _);

                    if (CTT_Tao_Mat_Phang_Tu_Featureline_Command.NormalizeFlMasterSurfaceToPurePasteParent(
                            civDoc, db, tr, ed, parentId, parentSurf, subStyleId, parentSurf.AutoRebuild, out _))
                    {
                        dirtyParentIds.Add(parentId);
                    }
                }
            }
            catch { }
        }

        private static int RebuildDownstreamPasteParentsInOrder(
            CivilDocument civDoc,
            Transaction tr,
            Editor ed,
            HashSet<ObjectId> seedDirtyIds,
            HashSet<ObjectId>? seedParentIds = null)
        {
            var childrenToParents = new Dictionary<ObjectId, List<ObjectId>>();
            var parentToChildren = new Dictionary<ObjectId, List<ObjectId>>();

            foreach (ObjectId sId in civDoc.GetSurfaceIds())
            {
                if (sId.IsNull || !sId.IsValid || sId.IsErased) continue;
                if (tr.GetObject(sId, OpenMode.ForRead) is not CivSurface surf) continue;

                try
                {
                    for (int i = 0; i < surf.Operations.Count; i++)
                    {
                        if (surf.Operations[i] is SurfaceOperationPasteSurface pasteOp &&
                            pasteOp.Enabled &&
                            !pasteOp.SurfaceId.IsNull &&
                            pasteOp.SurfaceId.IsValid &&
                            !pasteOp.SurfaceId.IsErased &&
                            pasteOp.SurfaceId != sId)
                        {
                            if (!childrenToParents.TryGetValue(pasteOp.SurfaceId, out var pList))
                            {
                                pList = new List<ObjectId>();
                                childrenToParents[pasteOp.SurfaceId] = pList;
                            }
                            if (!pList.Contains(sId)) pList.Add(sId);

                            if (!parentToChildren.TryGetValue(sId, out var cList))
                            {
                                cList = new List<ObjectId>();
                                parentToChildren[sId] = cList;
                            }
                            if (!cList.Contains(pasteOp.SurfaceId)) cList.Add(pasteOp.SurfaceId);
                        }
                    }
                }
                catch { }
            }

            // BFS tìm tất cả các mặt phẳng cha/ông bị ảnh hưởng
            var affectedParents = new HashSet<ObjectId>();
            var bfs = new Queue<ObjectId>(seedDirtyIds);
            if (seedParentIds != null)
            {
                foreach (ObjectId spId in seedParentIds)
                {
                    if (!spId.IsNull && spId.IsValid && !spId.IsErased && affectedParents.Add(spId))
                    {
                        bfs.Enqueue(spId);
                    }
                }
            }

            while (bfs.Count > 0)
            {
                ObjectId childId = bfs.Dequeue();
                if (!childrenToParents.TryGetValue(childId, out var parents)) continue;
                foreach (ObjectId parentId in parents)
                {
                    if (affectedParents.Add(parentId))
                    {
                        bfs.Enqueue(parentId);
                    }
                }
            }

            if (affectedParents.Count == 0) return 0;

            // Sắp xếp tô-pô từ cấp con lên cấp cha
            var inDegree = new Dictionary<ObjectId, int>();
            foreach (ObjectId pId in affectedParents)
            {
                int deg = 0;
                if (parentToChildren.TryGetValue(pId, out var children))
                {
                    foreach (ObjectId cId in children)
                    {
                        if (affectedParents.Contains(cId)) deg++;
                    }
                }
                inDegree[pId] = deg;
            }

            var orderedParents = new List<ObjectId>(affectedParents.Count);
            var topoQ = new Queue<ObjectId>();
            foreach (var kvp in inDegree)
            {
                if (kvp.Value == 0) topoQ.Enqueue(kvp.Key);
            }

            while (topoQ.Count > 0)
            {
                ObjectId cur = topoQ.Dequeue();
                orderedParents.Add(cur);
                if (childrenToParents.TryGetValue(cur, out var nextParents))
                {
                    foreach (ObjectId np in nextParents)
                    {
                        if (!inDegree.ContainsKey(np)) continue;
                        inDegree[np]--;
                        if (inDegree[np] == 0) topoQ.Enqueue(np);
                    }
                }
            }

            if (orderedParents.Count < affectedParents.Count)
            {
                foreach (ObjectId pId in affectedParents)
                {
                    if (!orderedParents.Contains(pId))
                        orderedParents.Add(pId);
                }
            }

            int rebuiltCount = 0;
            foreach (ObjectId parentId in orderedParents)
            {
                if (tr.GetObject(parentId, OpenMode.ForWrite) is not CivSurface parentSurf)
                    continue;

                bool wasLocked = false;
                try
                {
                    wasLocked = parentSurf.Lock;
                    if (wasLocked) parentSurf.Lock = false;
                }
                catch { }

                try
                {
                    parentSurf.Rebuild();

                    SurfaceOriginationType origType = SurfaceOriginationType.None;
                    try { origType = parentSurf.OriginationType; } catch { }
                    if (origType == SurfaceOriginationType.None &&
                        CTT_Tao_Mat_Phang_Tu_Featureline_Command.HasRestrictiveOuterBoundary(parentSurf) &&
                        CTT_Tao_Mat_Phang_Tu_Featureline_Command.IsPurePasteCompositeSurface(parentSurf))
                    {
                        bool anyChildClipped = false;
                        if (parentToChildren.TryGetValue(parentId, out var childIds))
                        {
                            foreach (ObjectId cId in childIds)
                            {
                                if (tr.GetObject(cId, OpenMode.ForRead) is not CivSurface childSurf) continue;
                                var samples = CTT_Tao_Mat_Phang_Tu_Featureline_Command.GetSampleVisibleCentroids(childSurf, 24);
                                for (int s = 0; s < samples.Count; s++)
                                {
                                    if (!CTT_Tao_Mat_Phang_Tu_Featureline_Command.IsPointVisibleOnSurface(parentSurf, samples[s].X, samples[s].Y))
                                    {
                                        anyChildClipped = true;
                                        break;
                                    }
                                }
                                if (anyChildClipped) break;
                            }
                        }

                        if (anyChildClipped)
                        {
                            CTT_Tao_Mat_Phang_Tu_Featureline_Command.RemoveRestrictiveOuterBoundaries(parentSurf);
                            parentSurf.Rebuild();
                        }
                    }

                    rebuiltCount++;
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage($"\n⚠️ Không thể rebuild mặt phẳng tổng '{parentSurf.Name}': {ex.Message}");
                }
                finally
                {
                    if (wasLocked)
                    {
                        try { parentSurf.Lock = true; } catch { }
                    }
                }
            }

            return rebuiltCount;
        }
    }
}

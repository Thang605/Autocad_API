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
        /// Bổ sung mảng mặt phẳng mới tạo bởi Feature Line vào một mặt phẳng TIN ĐÃ CÓ SẴN:
        /// - Chế độ mặc định (UseManagedSubSurfaceForExisting = true): Tạo 1 mặt phẳng phụ sạch có tiền tố '&lt;TênMặtPhẳngCóSẵn&gt;_&lt;HậuTố&gt;'
        ///   (Style mặc định '_No Display') rồi Paste động vào mặt phẳng có sẵn để dễ quản lý/bật tắt/cập nhật trong Prospector.
        /// - Chế độ gộp trực tiếp (UseManagedSubSurfaceForExisting = false): Nạp trực tiếp vào mặt phẳng có sẵn (0 mặt phẳng phụ).
        /// </summary>
        private static SurfaceCreationResult SupplementFeatureLinesIntoExistingSurface(
            CivilDocument civDoc,
            Transaction tr,
            Editor ed,
            TaoMatPhangOptions options,
            List<SourceCurveInfo> sourceCurves,
            int openCount,
            int closedCount,
            double midOrd,
            double suppDist,
            double closeTol)
        {
            ObjectId targetSurfaceId = options.ExistingSurfaceId;
            CivSurface? origSurface = tr.GetObject(targetSurfaceId, OpenMode.ForWrite) as CivSurface;
            if (origSurface == null)
            {
                return new SurfaceCreationResult
                {
                    Success = false,
                    Message = "Không thể mở mặt phẳng TIN có sẵn ở chế độ ghi (OpenMode.ForWrite)."
                };
            }

            // Nếu mặt phẳng có sẵn sinh trực tiếp từ Corridor / Grading và người dùng bật chế độ gộp bảo vệ:
            SurfaceOriginationType origType = SurfaceOriginationType.None;
            try { origType = origSurface.OriginationType; } catch { }

            if (origType != SurfaceOriginationType.None && options.CreateCombinedForCorridorSurface)
            {
                string desiredCombinedName = !string.IsNullOrWhiteSpace(options.CombinedSurfaceName)
                    ? options.CombinedSurfaceName.Trim()
                    : $"{origSurface.Name}_Gop";

                ObjectId existingCombinedId = FindSurfaceByName(civDoc, tr, desiredCombinedName);
                if (!existingCombinedId.IsNull && existingCombinedId.IsValid && !existingCombinedId.IsErased &&
                    existingCombinedId != options.ExistingSurfaceId)
                {
                    targetSurfaceId = existingCombinedId;
                }
                else
                {
                    ObjectId styleId = origSurface.StyleId;
                    if (styleId.IsNull || !styleId.IsValid || styleId.IsErased)
                    {
                        styleId = FindSurfaceStyleId(civDoc, tr, options.SurfaceStyleName);
                    }

                    string uniqueCombinedName = EnsureUniqueSurfaceName(civDoc, tr, desiredCombinedName);
                    targetSurfaceId = TinSurface.Create(uniqueCombinedName, styleId);
                    if (tr.GetObject(targetSurfaceId, OpenMode.ForWrite) is CivSurface newCombinedSurf)
                    {
                        newCombinedSurf.PasteSurface(options.ExistingSurfaceId);
                        newCombinedSurf.Rebuild();
                    }
                }
            }

            CivSurface? targetSurface = tr.GetObject(targetSurfaceId, OpenMode.ForWrite) as CivSurface;
            if (targetSurface == null)
            {
                return new SurfaceCreationResult
                {
                    Success = false,
                    Message = "Không thể truy cập mặt phẳng TIN đích để bổ sung dữ liệu."
                };
            }

            // Nhánh ưu tiên mặc định: Tạo mặt phẳng phụ mang tiền tố '<TênMặtPhẳngCóSẵn>_' & Paste động để người dùng quản lý rõ ràng
            if (options.UseManagedSubSurfaceForExisting)
            {
                return SupplementViaManagedSubSurface(
                    civDoc,
                    tr,
                    ed,
                    options,
                    targetSurfaceId,
                    targetSurface,
                    sourceCurves,
                    openCount,
                    closedCount,
                    midOrd,
                    suppDist,
                    closeTol);
            }

            string finalSurfaceName = targetSurface.Name;
            bool wasLocked = false;
            try
            {
                wasLocked = targetSurface.Lock;
                if (wasLocked) targetSurface.Lock = false;
            }
            catch { }

            try
            {
                int prevTriangleCount = CountVisibleTriangles(targetSurface);

                ed.WriteMessage($"\n══════════════════════════════════════════════════════════════════");
                ed.WriteMessage($"\n➕ BỔ SUNG MẶT PHẲNG TỪ FEATURE LINE VÀO SURFACE CÓ SẴN (0 MẶT PHẲNG PHỤ)");
                ed.WriteMessage($"\n  • Mặt phẳng đích : '{finalSurfaceName}' (Hiện có: {prevTriangleCount:N0} tam giác)");
                ed.WriteMessage($"\n  • Chế độ chồng lấn: {(options.OverwriteOverlapOnExisting ? "Đắp đè cao độ mới tại vùng chồng lấn" : "Giữ nguyên mặt phẳng cũ, chỉ nối mở rộng vùng mới")}");
                ed.WriteMessage($"\n  • Nguồn bổ sung  : {sourceCurves.Count} đối tượng ({openCount} đường hở, {closedCount} đường kín)");

                // Trường hợp 1: Mặt phẳng có sẵn đang rỗng (0 tam giác) -> nạp trực tiếp như mặt phẳng mới
                if (prevTriangleCount == 0)
                {
                    RemoveRestrictiveOuterBoundaries(targetSurface);
                    RemoveDeleteLineOperations(targetSurface);

                    bool emptyOk = PopulateCleanFeatureLineSurface(
                        targetSurface,
                        options,
                        tr,
                        ed,
                        sourceCurves,
                        midOrd,
                        suppDist,
                        closeTol,
                        usePointsOnlyBreaklines: false,
                        out int emptyAddedBkl,
                        out int emptyTrimmed,
                        out _);

                    if (!emptyOk || emptyAddedBkl == 0)
                    {
                        tr.Abort();
                        return new SurfaceCreationResult
                        {
                            Success = false,
                            Message = "Không thể thêm Breakline vào mặt phẳng đích."
                        };
                    }

                    SetAutoRebuildSafe(targetSurface, options.AutoRebuild);
                    int emptyFinalTris = CountVisibleTriangles(targetSurface);
                    SaveSurfaceRecipe(targetSurface, tr, CreateRecipeFromOptions(targetSurface.Database, options, ObjectId.Null));
                    if (wasLocked) { try { targetSurface.Lock = true; } catch { } }
                    tr.Commit();

                    return new SurfaceCreationResult
                    {
                        Success = true,
                        Message = $"Đã bổ sung thành công vào mặt phẳng '{finalSurfaceName}' ({emptyFinalTris:N0} tam giác, 0 mặt phẳng phụ)!",
                        MasterSurfaceId = targetSurfaceId,
                        FinalSurfaceName = finalSurfaceName,
                        IsSupplementedIntoExisting = true,
                        PreviousTriangleCount = 0,
                        SubSurfaceCount = 0,
                        ProcessedCurveCount = emptyAddedBkl,
                        OpenCurveCount = openCount,
                        ClosedCurveCount = closedCount,
                        TrimmedTriangleCount = emptyTrimmed,
                        FinalTriangleCount = emptyFinalTris
                    };
                }

                // Trường hợp 2: Người dùng chủ động chọn "Chỉ thêm Breaklines thuần túy (Không cắt biên tam giác)"
                if (options.BoundaryMode == FeatureLineBoundaryMode.BreaklinesOnly)
                {
                    try
                    {
                        targetSurface.BuildOptions.CrossingBreaklinesElevationOption =
                            options.OverwriteOverlapOnExisting
                                ? CrossingBreaklinesElevationType.UseLast
                                : CrossingBreaklinesElevationType.UseFirst;
                        targetSurface.BuildOptions.UseMaximumTriangleLength = options.LimitMaxEdgeLength;
                        if (options.LimitMaxEdgeLength)
                        {
                            targetSurface.BuildOptions.MaximumTriangleLength = Math.Max(0.5, options.MaxEdgeLength);
                        }
                    }
                    catch { }

                    int rawAddedCount = 0;
                    foreach (SourceCurveInfo curve in sourceCurves)
                    {
                        if (AddCurveToSurfaceBreaklines(targetSurface, curve, midOrd, suppDist))
                            rawAddedCount++;
                    }

                    targetSurface.Rebuild();
                    SetAutoRebuildSafe(targetSurface, options.AutoRebuild);
                    int rawFinalTris = CountVisibleTriangles(targetSurface);
                    if (wasLocked) { try { targetSurface.Lock = true; } catch { } }
                    tr.Commit();

                    return new SurfaceCreationResult
                    {
                        Success = true,
                        Message = $"Đã thêm {rawAddedCount} Breakline vào mặt phẳng '{finalSurfaceName}' ({prevTriangleCount:N0} -> {rawFinalTris:N0} tam giác)!",
                        MasterSurfaceId = targetSurfaceId,
                        FinalSurfaceName = finalSurfaceName,
                        IsSupplementedIntoExisting = true,
                        PreviousTriangleCount = prevTriangleCount,
                        SubSurfaceCount = 0,
                        ProcessedCurveCount = rawAddedCount,
                        OpenCurveCount = openCount,
                        ClosedCurveCount = closedCount,
                        TrimmedTriangleCount = 0,
                        FinalTriangleCount = rawFinalTris
                    };
                }

                // Trường hợp 3: BỔ SUNG THÔNG MINH VÀO MẶT PHẲNG ĐÃ CÓ LƯỚI TAM GIÁC (AutoSmart / CustomBoundaryEntities)
                // Nếu mặt phẳng hiện hữu là mặt phẳng đơn tạo từ Feature Line (có Recipe và chưa Paste), hợp nhất thêm vào Recipe & tái tạo sạch để đồng bộ động 100%
                SurfaceOriginationType targetOrigCheck = SurfaceOriginationType.None;
                try { targetOrigCheck = targetSurface.OriginationType; } catch { }
                if (targetOrigCheck == SurfaceOriginationType.None &&
                    !HasAnyPasteSurfaceOperation(targetSurface) &&
                    TryReadSurfaceRecipe(targetSurface, tr, out FeatureLineSurfaceRecipe? existingStandaloneRecipe) &&
                    existingStandaloneRecipe != null)
                {
                    var mergedHandleSet = new HashSet<string>(existingStandaloneRecipe.SourceHandles, StringComparer.OrdinalIgnoreCase);
                    foreach (string h in ExtractHandlesFromIds(options.SourceEntityIds))
                    {
                        if (mergedHandleSet.Add(h))
                            existingStandaloneRecipe.SourceHandles.Add(h);
                    }

                    if (options.BoundaryMode == FeatureLineBoundaryMode.CustomBoundaryEntities)
                    {
                        existingStandaloneRecipe.BoundaryMode = (int)FeatureLineBoundaryMode.CustomBoundaryEntities;
                        var outerSet = new HashSet<string>(existingStandaloneRecipe.CustomOuterHandles, StringComparer.OrdinalIgnoreCase);
                        foreach (string h in ExtractHandlesFromIds(options.CustomOuterBoundaryIds))
                        {
                            if (outerSet.Add(h))
                                existingStandaloneRecipe.CustomOuterHandles.Add(h);
                        }

                        var hideSet = new HashSet<string>(existingStandaloneRecipe.CustomHideHandles, StringComparer.OrdinalIgnoreCase);
                        foreach (string h in ExtractHandlesFromIds(options.CustomHideBoundaryIds))
                        {
                            if (hideSet.Add(h))
                                existingStandaloneRecipe.CustomHideHandles.Add(h);
                        }
                    }

                    existingStandaloneRecipe.AutoRebuild = options.AutoRebuild;
                    if (RebuildSurfaceFromRecipeInternal(
                            civDoc,
                            targetSurface.Database,
                            tr,
                            ed,
                            targetSurface,
                            existingStandaloneRecipe,
                            parentSurfaceOverride: null,
                            out int mergedValidCurves,
                            out int rebuiltFinalTris))
                    {
                        if (wasLocked)
                        {
                            try { targetSurface.Lock = true; } catch { }
                        }

                        tr.Commit();

                        ed.WriteMessage($"\n  • Hợp nhất Recipe : Tái tạo sạch '{finalSurfaceName}' ({prevTriangleCount:N0} -> {rebuiltFinalTris:N0} tam giác | 0 mặt phẳng phụ).");
                        ed.WriteMessage($"\n🎉 Đã bổ sung & duy trì đồng bộ động cho '{finalSurfaceName}'!");
                        ed.WriteMessage($"\n══════════════════════════════════════════════════════════════════\n");

                        return new SurfaceCreationResult
                        {
                            Success = true,
                            Message = $"Đã bổ sung thành công vào mặt phẳng '{finalSurfaceName}' ({prevTriangleCount:N0} → {rebuiltFinalTris:N0} tam giác, 0 mặt phẳng phụ)!",
                            MasterSurfaceId = targetSurfaceId,
                            FinalSurfaceName = finalSurfaceName,
                            IsSupplementedIntoExisting = true,
                            PreviousTriangleCount = prevTriangleCount,
                            SubSurfaceCount = 0,
                            ProcessedCurveCount = mergedValidCurves,
                            OpenCurveCount = openCount,
                            ClosedCurveCount = closedCount,
                            TrimmedTriangleCount = 0,
                            FinalTriangleCount = rebuiltFinalTris
                        };
                    }
                }

                // Bước 3A: Tính toán chính xác hình học 3D của mảng mặt phẳng mới R_new trên biến tạm và XÓA NGAY trong cùng Transaction (0 mặt phẳng phụ!)
                TinTopologyGraph patchGraph;
                List<Polygon2D> newHolePolys;
                int patchTrimmedCount;

                ObjectId calcStyleId = targetSurface.StyleId;
                if (calcStyleId.IsNull || !calcStyleId.IsValid || calcStyleId.IsErased)
                {
                    calcStyleId = FindSurfaceStyleId(civDoc, tr, options.SurfaceStyleName);
                }

                string tempPatchName = EnsureUniqueSurfaceName(civDoc, tr, $"__TMP_FL_{Guid.NewGuid():N}".Substring(0, 22));
                ObjectId tempSurfId = TinSurface.Create(tempPatchName, calcStyleId);
                CivSurface? tempSurf = tr.GetObject(tempSurfId, OpenMode.ForWrite) as CivSurface;
                if (tempSurf == null)
                {
                    tr.Abort();
                    return new SurfaceCreationResult
                    {
                        Success = false,
                        Message = "Không thể khởi tạo bộ đệm tính toán lưới tam giác tạm thời."
                    };
                }

                try
                {
                    bool patchOk = PopulateCleanFeatureLineSurface(
                        tempSurf,
                        options,
                        tr,
                        ed,
                        sourceCurves,
                        midOrd,
                        suppDist,
                        closeTol,
                        usePointsOnlyBreaklines: true,
                        out _,
                        out patchTrimmedCount,
                        out newHolePolys);

                    if (!patchOk)
                    {
                        tempSurf.Erase(true);
                        tr.Abort();
                        return new SurfaceCreationResult
                        {
                            Success = false,
                            Message = "Không thể tạo lưới tam giác từ các đường Feature Line bổ sung."
                        };
                    }

                    patchGraph = BuildTinTopologyGraph(tempSurf);
                    // Ngắt tham chiếu cạnh COM/Mgd trước khi giải phóng biến tạm
                    for (int i = 0; i < patchGraph.Edges.Count; i++)
                    {
                        patchGraph.Edges[i].RawEdge = null;
                    }
                }
                finally
                {
                    if (!tempSurf.IsErased)
                    {
                        try { tempSurf.Erase(true); } catch { }
                    }
                }

                if (patchGraph.Triangles.Count == 0)
                {
                    tr.Abort();
                    return new SurfaceCreationResult
                    {
                        Success = false,
                        Message = "Mảng mặt phẳng mới tạo từ Feature Line có 0 tam giác hợp lệ sau khi lọc biên (hãy kiểm tra lại chiều dài cạnh tối đa hoặc đường biên)."
                    };
                }

                var newPatchGrid = new SpatialTriangleGrid2D(patchGraph);
                var newExactTriKeys = BuildTriangleVertexKeySet(patchGraph);
                var newPatchBorderGrid = BuildBorderSegmentGrid(patchGraph);
                var newPatchVertexLookup = new SpatialVertexLookup2D(patchGraph.Vertices);

                // Bước 3B: Chụp trạng thái hình học hiện hữu R_old của mặt phẳng có sẵn trước khi can thiệp
                TinTopologyGraph oldGraph = BuildTinTopologyGraph(targetSurface);
                var oldTriGrid = new SpatialTriangleGrid2D(oldGraph);
                var oldExactTriKeys = BuildTriangleVertexKeySet(oldGraph);
                var oldBorderGrid = BuildBorderSegmentGrid(oldGraph);
                var oldVertexLookup = new SpatialVertexLookup2D(oldGraph.Vertices);

                bool patchExtendsOutsideOld = DoesGraphExtendOutsideGrid(patchGraph, oldTriGrid);

                // Bước 3C: Gỡ bỏ Outer/DataClip Boundary cũ nếu vùng bổ sung vươn ra ngoài mặt phẳng cũ,
                // và gỡ các lệnh DeleteLines cũ để tái tạo 1 lệnh cắt biên hợp nhất sạch sẽ ở cuối
                bool removedOuterBoundary = false;
                if (patchExtendsOutsideOld)
                {
                    removedOuterBoundary = RemoveRestrictiveOuterBoundaries(targetSurface);
                }
                RemoveDeleteLineOperations(targetSurface);

                // Bước 3D: Khóa toàn bộ đường biên hợp lệ của mặt phẳng cũ (ngoài vùng bị đắp đè) để bảo vệ tuyệt đối R_old
                List<(Point3d A, Point3d B)> oldBorderSegmentsToLock = new List<(Point3d A, Point3d B)>();
                for (int eIdx = 0; eIdx < oldGraph.Edges.Count; eIdx++)
                {
                    TinEdgeNode edge = oldGraph.Edges[eIdx];
                    bool isBnd = (edge.TriA >= 0) != (edge.TriB >= 0);
                    if (!isBnd) continue;
                    if (!removedOuterBoundary && edge.IsLocked) continue;

                    Point3d pA = oldGraph.Vertices[edge.U].Pos;
                    Point3d pB = oldGraph.Vertices[edge.V].Pos;

                    if (options.OverwriteOverlapOnExisting)
                    {
                        AppendClippedSegmentsOutsideRegion(pA, pB, newPatchGrid, newPatchBorderGrid, oldBorderSegmentsToLock);
                    }
                    else
                    {
                        oldBorderSegmentsToLock.Add((pA, pB));
                    }
                }

                List<Point3dCollection> oldBorderChains = ChainSegmentsToPolylines(oldBorderSegmentsToLock);
                foreach (Point3dCollection chain in oldBorderChains)
                {
                    try
                    {
                        targetSurface.BreaklinesDefinition.AddStandardBreaklines(chain, midOrd, 100000.0, 0.0, 0.0);
                    }
                    catch { }
                }

                // Bước 3E: Cấu hình ưu tiên giao cắt & nạp Breaklines mới + khóa biên R_new
                try
                {
                    targetSurface.BuildOptions.UseMaximumTriangleLength = false;
                    targetSurface.BuildOptions.CrossingBreaklinesElevationOption =
                        options.OverwriteOverlapOnExisting
                            ? CrossingBreaklinesElevationType.UseLast
                            : CrossingBreaklinesElevationType.UseFirst;
                }
                catch { }

                int addedBreaklineCount = 0;
                if (options.OverwriteOverlapOnExisting)
                {
                    foreach (SourceCurveInfo curve in sourceCurves)
                    {
                        if (AddCurveToSurfaceBreaklines(
                            targetSurface,
                            curve,
                            midOrd,
                            suppDist,
                            usePointsOnly: false,
                            allowDirectCurvedEntity: false))
                        {
                            addedBreaklineCount++;
                        }
                    }

                    // Khóa các cạnh biên hở (cap đầu-cuối / cạnh cắt biên tùy chọn) của mảng mới R_new
                    List<(Point3d A, Point3d B)> patchCapSegments = new List<(Point3d A, Point3d B)>();
                    bool lockAllPatchBorders = options.BoundaryMode == FeatureLineBoundaryMode.CustomBoundaryEntities;
                    for (int eIdx = 0; eIdx < patchGraph.Edges.Count; eIdx++)
                    {
                        TinEdgeNode edge = patchGraph.Edges[eIdx];
                        bool isBnd = (edge.TriA >= 0) != (edge.TriB >= 0);
                        if (!isBnd) continue;
                        if (!lockAllPatchBorders && edge.IsLocked) continue;

                        patchCapSegments.Add((patchGraph.Vertices[edge.U].Pos, patchGraph.Vertices[edge.V].Pos));
                    }

                    foreach (Point3dCollection capChain in ChainSegmentsToPolylines(patchCapSegments))
                    {
                        try
                        {
                            targetSurface.BreaklinesDefinition.AddStandardBreaklines(capChain, midOrd, 100000.0, 0.0, 0.0);
                        }
                        catch { }
                    }

                    foreach (Polygon2D hole in newHolePolys)
                    {
                        AddNativeBoundaryPolygon(targetSurface, hole, midOrd, SurfaceBoundaryType.Hide);
                    }
                }
                else
                {
                    // Chế độ giữ nguyên mặt phẳng cũ tại vùng chồng lấn: cắt các đoạn mới dừng êm ngay tại biên R_old
                    List<(Point3d A, Point3d B)> clippedNewSegments = new List<(Point3d A, Point3d B)>();
                    foreach (SourceCurveInfo curve in sourceCurves)
                    {
                        bool anySegAdded = false;
                        for (int i = 0; i < curve.Points3D.Count - 1; i++)
                        {
                            int before = clippedNewSegments.Count;
                            AppendClippedSegmentsOutsideRegion(curve.Points3D[i], curve.Points3D[i + 1], oldTriGrid, oldBorderGrid, clippedNewSegments);
                            if (clippedNewSegments.Count > before) anySegAdded = true;
                        }
                        if (anySegAdded) addedBreaklineCount++;
                    }

                    for (int eIdx = 0; eIdx < patchGraph.Edges.Count; eIdx++)
                    {
                        TinEdgeNode edge = patchGraph.Edges[eIdx];
                        bool isBnd = (edge.TriA >= 0) != (edge.TriB >= 0);
                        if (!isBnd) continue;
                        AppendClippedSegmentsOutsideRegion(
                            patchGraph.Vertices[edge.U].Pos,
                            patchGraph.Vertices[edge.V].Pos,
                            oldTriGrid,
                            oldBorderGrid,
                            clippedNewSegments);
                    }

                    foreach (Point3dCollection extChain in ChainSegmentsToPolylines(clippedNewSegments))
                    {
                        try
                        {
                            targetSurface.BreaklinesDefinition.AddStandardBreaklines(extChain, midOrd, suppDist, 0.0, 0.0);
                        }
                        catch { }
                    }

                    foreach (Polygon2D hole in newHolePolys)
                    {
                        if (hole.Vertices3D.Count > 0 && !oldTriGrid.ContainsPoint(hole.Vertices3D[0].X, hole.Vertices3D[0].Y, 1e-4))
                        {
                            AddNativeBoundaryPolygon(targetSurface, hole, midOrd, SurfaceBoundaryType.Hide);
                        }
                    }

                    if (addedBreaklineCount == 0 && sourceCurves.Count > 0)
                    {
                        addedBreaklineCount = sourceCurves.Count;
                    }
                }

                // Bước 3F: Rebuild & Đồng bộ đỉnh / cao độ vùng chồng lấn (xử lý triệt để cả PointGroup, Contour, PasteSurface, Snapshot)
                targetSurface.Rebuild();

                ReconcileMergedVerticesAndElevations(
                    targetSurface,
                    patchGraph,
                    newPatchGrid,
                    newPatchBorderGrid,
                    newPatchVertexLookup,
                    oldTriGrid,
                    oldBorderGrid,
                    oldVertexLookup,
                    options.OverwriteOverlapOnExisting);

                // Bước 3G: Cắt lọc tam giác hợp nhất (giữ trọn R_old U R_new, xóa sạch 100% tam giác bắc cầu & dây cung thừa)
                TinTopologyGraph mergedGraph = BuildTinTopologyGraph(targetSurface);
                bool[] removed = new bool[mergedGraph.Triangles.Count];

                for (int t = 0; t < mergedGraph.Triangles.Count; t++)
                {
                    TinTriangleNode tri = mergedGraph.Triangles[t];
                    Point2d c = tri.Centroid2D;

                    // Kiểm tra lỗ khoét của phần mới bổ sung
                    bool inNewHole = false;
                    for (int h = 0; h < newHolePolys.Count; h++)
                    {
                        if (newHolePolys[h].Contains(c.X, c.Y))
                        {
                            if (options.OverwriteOverlapOnExisting || !oldTriGrid.ContainsPoint(c.X, c.Y, 1e-5))
                            {
                                inNewHole = true;
                                break;
                            }
                        }
                    }
                    if (inNewHole)
                    {
                        removed[t] = true;
                        continue;
                    }

                    bool keep = newPatchGrid.ContainsPoint(c.X, c.Y, 1e-5) || oldTriGrid.ContainsPoint(c.X, c.Y, 1e-5);
                    if (!keep)
                    {
                        TriangleVertexKey tKey = TriangleVertexKey.From(mergedGraph, tri);
                        if (newExactTriKeys.Contains(tKey))
                        {
                            keep = true;
                        }
                        else if (oldExactTriKeys.Contains(tKey) &&
                                 !(options.OverwriteOverlapOnExisting && newPatchGrid.ContainsPoint(c.X, c.Y, 1e-5)))
                        {
                            keep = true;
                        }
                    }

                    if (!keep)
                    {
                        removed[t] = true;
                    }
                }

                int mergedTrimmedCount = ApplyRemovedTrianglesToSurface(targetSurface, mergedGraph, removed, midOrd, ed);
                int totalTrimmedReport = mergedTrimmedCount + patchTrimmedCount;

                SetAutoRebuildSafe(targetSurface, options.AutoRebuild);
                int finalTriangleCount = CountVisibleTriangles(targetSurface);

                if (wasLocked)
                {
                    try { targetSurface.Lock = true; } catch { }
                }

                tr.Commit();

                ed.WriteMessage($"\n  • Đã cắt lọc     : {mergedTrimmedCount:N0} tam giác bắc cầu/ngoài biên khi ghép (tổng lọc: {totalTrimmedReport:N0})");
                ed.WriteMessage($"\n  • Lưới hoàn thiện: {prevTriangleCount:N0} -> {finalTriangleCount:N0} tam giác hợp lệ | Số mặt phẳng phụ = 0");
                ed.WriteMessage($"\n🎉 Đã bổ sung thành công vào mặt phẳng '{finalSurfaceName}'!");
                ed.WriteMessage($"\n══════════════════════════════════════════════════════════════════\n");

                return new SurfaceCreationResult
                {
                    Success = true,
                    Message = $"Đã bổ sung thành công vào mặt phẳng '{finalSurfaceName}' ({prevTriangleCount:N0} → {finalTriangleCount:N0} tam giác, 0 mặt phẳng phụ)!",
                    MasterSurfaceId = targetSurfaceId,
                    FinalSurfaceName = finalSurfaceName,
                    IsSupplementedIntoExisting = true,
                    PreviousTriangleCount = prevTriangleCount,
                    SubSurfaceCount = 0,
                    ProcessedCurveCount = addedBreaklineCount,
                    OpenCurveCount = openCount,
                    ClosedCurveCount = closedCount,
                    TrimmedTriangleCount = totalTrimmedReport,
                    FinalTriangleCount = finalTriangleCount
                };
            }
            catch
            {
                if (wasLocked)
                {
                    try { targetSurface.Lock = true; } catch { }
                }
                throw;
            }
        }
    }
}

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
        /// Tạo hoặc cập nhật 1 mặt phẳng phụ duy nhất có tiền tố '&lt;TênMặtPhẳngCóSẵn&gt;_&lt;HậuTố&gt;'
        /// rồi Paste động vào mặt phẳng có sẵn để kỹ sư quản lý trực quan ngay dưới mặt phẳng gốc trong Prospector.
        /// </summary>
        private static SurfaceCreationResult SupplementViaManagedSubSurface(
            CivilDocument civDoc,
            Transaction tr,
            Editor ed,
            TaoMatPhangOptions options,
            ObjectId targetSurfaceId,
            CivSurface targetSurface,
            List<SourceCurveInfo> sourceCurves,
            int openCount,
            int closedCount,
            double midOrd,
            double suppDist,
            double closeTol)
        {
            string finalSurfaceName = targetSurface.Name;
            bool wasTargetLocked = false;
            bool wasSubLocked = false;
            CivSurface? subSurface = null;

            try
            {
                wasTargetLocked = targetSurface.Lock;
                if (wasTargetLocked) targetSurface.Lock = false;
            }
            catch { }

            try
            {
                ObjectId subStyleId = ResolveSubSurfaceStyleId(civDoc, tr, options.SubSurfaceStyleName, targetSurface.StyleId, out string resolvedSubStyleName);
                if (subStyleId.IsNull || !subStyleId.IsValid)
                {
                    tr.Abort();
                    return new SurfaceCreationResult
                    {
                        Success = false,
                        Message = "Không tìm thấy Surface Style hợp lệ để tạo mặt phẳng phụ."
                    };
                }

                // Nếu mặt phẳng gốc là mặt phẳng đơn FL hoặc mặt lai có Breakline/Outer cũ chặn biên,
                // tự động chuẩn hóa thành mặt tổng thuần PasteSurface để luôn co giãn chính xác theo từng mặt phẳng phụ.
                NormalizeFlMasterSurfaceToPurePasteParent(
                    civDoc,
                    targetSurface.Database,
                    tr,
                    ed,
                    targetSurfaceId,
                    targetSurface,
                    subStyleId,
                    options.AutoRebuild,
                    out _);

                int prevTriangleCount = CountVisibleTriangles(targetSurface);
                string desiredSubName = BuildPrefixedSubSurfaceName(finalSurfaceName, options.SubSurfaceSuffixOrName);
                ObjectId existingSubId = FindSurfaceByName(civDoc, tr, desiredSubName);
                bool reusingExistingSub = !existingSubId.IsNull &&
                                          existingSubId.IsValid &&
                                          !existingSubId.IsErased &&
                                          existingSubId != targetSurfaceId &&
                                          options.OverwriteExistingSubSurface;

                SurfaceOperationPasteSurface? existingPasteOp = reusingExistingSub
                    ? FindPasteOperationForSubSurface(targetSurface, existingSubId)
                    : null;

                // Nếu chế độ giữ nguyên mặt phẳng cũ tại vùng chồng lấn -> chụp lưới mặt phẳng gốc trước khi Paste/Cập nhật
                TinTopologyGraph? oldGraph = null;
                SpatialTriangleGrid2D? oldTriGrid = null;
                if (!options.OverwriteOverlapOnExisting)
                {
                    bool tempDisabledPriorPaste = false;
                    if (existingPasteOp != null && existingPasteOp.Enabled)
                    {
                        try
                        {
                            existingPasteOp.Enabled = false;
                            targetSurface.Rebuild();
                            tempDisabledPriorPaste = true;
                        }
                        catch { }
                    }

                    int pureOldTris = CountVisibleTriangles(targetSurface);
                    if (pureOldTris > 0)
                    {
                        oldGraph = BuildTinTopologyGraph(targetSurface);
                        oldTriGrid = new SpatialTriangleGrid2D(oldGraph);
                    }

                    if (tempDisabledPriorPaste && existingPasteOp != null)
                    {
                        try { existingPasteOp.Enabled = true; } catch { }
                    }
                }

                ObjectId subSurfaceId;
                string finalSubName;

                if (reusingExistingSub)
                {
                    subSurfaceId = existingSubId;
                    subSurface = tr.GetObject(subSurfaceId, OpenMode.ForWrite) as CivSurface;
                    if (subSurface == null)
                    {
                        tr.Abort();
                        return new SurfaceCreationResult
                        {
                            Success = false,
                            Message = $"Không thể mở mặt phẳng phụ '{desiredSubName}' ở chế độ ghi (OpenMode.ForWrite)."
                        };
                    }

                    try
                    {
                        wasSubLocked = subSurface.Lock;
                        if (wasSubLocked) subSurface.Lock = false;
                    }
                    catch { }

                    ClearSurfaceDefinitionsAndEdits(subSurface);
                    try { subSurface.StyleId = subStyleId; } catch { }
                    finalSubName = subSurface.Name;
                }
                else
                {
                    finalSubName = EnsureUniquePrefixedSubSurfaceName(civDoc, tr, finalSurfaceName, options.SubSurfaceSuffixOrName);
                    subSurfaceId = TinSurface.Create(finalSubName, subStyleId);
                    if (subSurfaceId.IsNull || !subSurfaceId.IsValid)
                    {
                        tr.Abort();
                        return new SurfaceCreationResult
                        {
                            Success = false,
                            Message = $"Không thể khởi tạo mặt phẳng phụ '{finalSubName}'."
                        };
                    }

                    subSurface = tr.GetObject(subSurfaceId, OpenMode.ForWrite) as CivSurface;
                    if (subSurface == null)
                    {
                        tr.Abort();
                        return new SurfaceCreationResult
                        {
                            Success = false,
                            Message = $"Không thể mở mặt phẳng phụ '{finalSubName}' ở chế độ ghi."
                        };
                    }

                    finalSubName = subSurface.Name;
                }

                ed.WriteMessage($"\n══════════════════════════════════════════════════════════════════");
                ed.WriteMessage($"\n➕ BỔ SUNG VÀO MẶT PHẲNG CÓ SẴN QUA MẶT PHẲNG PHỤ CÓ TIỀN TỐ QUẢN LÝ");
                ed.WriteMessage($"\n  • Mặt phẳng gốc  : '{finalSurfaceName}' (Hiện có: {prevTriangleCount:N0} tam giác)");
                ed.WriteMessage($"\n  • Mặt phẳng phụ  : '{finalSubName}' ({(reusingExistingSub ? "Cập nhật tại chỗ" : "Tạo mới")} | Style: '{resolvedSubStyleName}')");
                ed.WriteMessage($"\n  • Nguồn bổ sung  : {sourceCurves.Count} đối tượng ({openCount} đường hở, {closedCount} đường kín)");

                bool populatedOk = PopulateCleanFeatureLineSurface(
                    subSurface,
                    options,
                    tr,
                    ed,
                    sourceCurves,
                    midOrd,
                    suppDist,
                    closeTol,
                    usePointsOnlyBreaklines: false,
                    out int addedBreaklineCount,
                    out int subTrimmedCount,
                    out _);

                if (!populatedOk || addedBreaklineCount == 0)
                {
                    tr.Abort();
                    return new SurfaceCreationResult
                    {
                        Success = false,
                        Message = "Không thể thêm Breakline nào vào mặt phẳng phụ."
                    };
                }

                // Nếu người dùng chọn KHÔNG đắp đè vùng chồng lấn -> cắt bỏ tam giác của mặt phẳng phụ nằm lọt trong mặt phẳng gốc & khớp cao độ mép nối
                if (!options.OverwriteOverlapOnExisting && oldTriGrid != null && oldGraph != null && oldGraph.Triangles.Count > 0)
                {
                    TinTopologyGraph subGraph = BuildTinTopologyGraph(subSurface);
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
                            subTrimmedCount += ApplyRemovedTrianglesToSurface(subSurface, subGraph, subRemoved, midOrd, ed);
                        }

                        try
                        {
                            foreach (TinSurfaceVertex v in subSurface.Vertices)
                            {
                                if (v == null || !v.IsValid) continue;
                                Point3d loc = v.Location;
                                if (oldTriGrid.TryInterpolateZ(loc.X, loc.Y, out double oldZ, 0.015) &&
                                    Math.Abs(loc.Z - oldZ) > 0.002)
                                {
                                    try { subSurface.SetVertexElevation(v, oldZ); } catch { }
                                }
                            }
                        }
                        catch { }
                    }
                }

                int subTriangleCount = CountVisibleTriangles(subSurface);
                if (subTriangleCount == 0)
                {
                    tr.Abort();
                    return new SurfaceCreationResult
                    {
                        Success = false,
                        Message = !options.OverwriteOverlapOnExisting
                            ? "Toàn bộ vùng Feature Line mới nằm trọn trong mặt phẳng cũ trong khi tùy chọn 'Đắp đè vùng chồng lấn' đang tắt (0 tam giác mở rộng mới)."
                            : "Mặt phẳng phụ tạo từ Feature Line có 0 tam giác hợp lệ sau khi lọc biên (hãy kiểm tra lại chiều dài cạnh tối đa hoặc đường biên)."
                    };
                }

                // Liên kết Paste động mặt phẳng phụ vào mặt phẳng có sẵn
                existingPasteOp = FindPasteOperationForSubSurface(targetSurface, subSurfaceId);
                if (existingPasteOp != null)
                {
                    try { if (!existingPasteOp.Enabled) existingPasteOp.Enabled = true; } catch { }
                }
                else
                {
                    targetSurface.PasteSurface(subSurfaceId);
                    existingPasteOp = FindPasteOperationForSubSurface(targetSurface, subSurfaceId);
                }

                targetSurface.Rebuild();

                // Kiểm tra an toàn: nếu mặt phẳng gốc có Outer/DataClip Boundary cũ làm cắt khuất phần mặt phẳng phụ vừa Paste
                if (HasRestrictiveOuterBoundary(targetSurface))
                {
                    List<Point2d> probePoints = GetSampleVisibleCentroids(subSurface, maxSamples: 48);
                    probePoints.AddRange(GetSampleBorderPoints(subSurface, maxSamples: 24));
                    bool anyClippedByOuter = false;
                    for (int i = 0; i < probePoints.Count; i++)
                    {
                        if (!IsPointVisibleOnSurface(targetSurface, probePoints[i].X, probePoints[i].Y))
                        {
                            anyClippedByOuter = true;
                            break;
                        }
                    }

                    if (anyClippedByOuter)
                    {
                        SurfaceOriginationType targetOrigType = SurfaceOriginationType.None;
                        try { targetOrigType = targetSurface.OriginationType; } catch { }

                        if (targetOrigType != SurfaceOriginationType.None)
                        {
                            // Tuyệt đối không xóa Outer Boundary của Corridor / Grading Surface để tránh bung tam giác bao lồi!
                            ed.WriteMessage($"\n  ⚠️ [CẢNH BÁO] '{finalSurfaceName}' là mặt Corridor/Grading có Outer Boundary riêng làm cắt bớt phần mở rộng ngoài biên. Khuyên bật tùy chọn 'Tạo mặt gộp {finalSurfaceName}_Gop'.");
                        }
                        else if (IsPurePasteCompositeSurface(targetSurface))
                        {
                            RemoveRestrictiveOuterBoundaries(targetSurface);
                            targetSurface.Rebuild();
                        }
                        else
                        {
                            if (existingPasteOp != null)
                            {
                                try
                                {
                                    existingPasteOp.Enabled = false;
                                    targetSurface.Rebuild();
                                }
                                catch { }
                            }

                            TinTopologyGraph pureOld = oldGraph ?? BuildTinTopologyGraph(targetSurface);
                            SpatialTriangleGrid2D pureOldGrid = oldTriGrid ?? new SpatialTriangleGrid2D(pureOld);
                            var borderSegs = new List<(Point3d A, Point3d B)>();
                            for (int eIdx = 0; eIdx < pureOld.Edges.Count; eIdx++)
                            {
                                TinEdgeNode edge = pureOld.Edges[eIdx];
                                if ((edge.TriA >= 0) != (edge.TriB >= 0))
                                {
                                    borderSegs.Add((pureOld.Vertices[edge.U].Pos, pureOld.Vertices[edge.V].Pos));
                                }
                            }

                            foreach (Point3dCollection chain in ChainSegmentsToPolylines(borderSegs))
                            {
                                try { targetSurface.BreaklinesDefinition.AddStandardBreaklines(chain, midOrd, 100000.0, 0.0, 0.0); }
                                catch { }
                            }

                            RemoveRestrictiveOuterBoundaries(targetSurface);
                            targetSurface.Rebuild();

                            // Cắt bỏ ngay các tam giác bao lồi phát sinh bên ngoài vùng nền cũ pureOldGrid trước khi bật lại Paste
                            if (pureOldGrid != null && pureOld.Triangles.Count > 0)
                            {
                                TinTopologyGraph rebuiltBaseGraph = BuildTinTopologyGraph(targetSurface);
                                bool[] removeConvexFill = new bool[rebuiltBaseGraph.Triangles.Count];
                                bool anyFill = false;
                                for (int t = 0; t < rebuiltBaseGraph.Triangles.Count; t++)
                                {
                                    Point2d c = rebuiltBaseGraph.Triangles[t].Centroid2D;
                                    if (!pureOldGrid.ContainsPoint(c.X, c.Y, 1e-4))
                                    {
                                        removeConvexFill[t] = true;
                                        anyFill = true;
                                    }
                                }
                                if (anyFill)
                                {
                                    ApplyRemovedTrianglesToSurface(targetSurface, rebuiltBaseGraph, removeConvexFill, midOrd, ed);
                                }
                            }

                            if (existingPasteOp != null)
                            {
                                try { existingPasteOp.Enabled = true; } catch { }
                            }

                            targetSurface.Rebuild();
                        }
                    }
                }

                SetAutoRebuildSafe(subSurface, options.AutoRebuild);
                SetAutoRebuildSafe(targetSurface, options.AutoRebuild);
                SaveSurfaceRecipe(subSurface, tr, CreateRecipeFromOptions(targetSurface.Database, options, targetSurfaceId));

                int finalTriangleCount = CountVisibleTriangles(targetSurface);

                if (wasSubLocked && subSurface != null)
                {
                    try { subSurface.Lock = true; } catch { }
                }
                if (wasTargetLocked)
                {
                    try { targetSurface.Lock = true; } catch { }
                }

                tr.Commit();

                ed.WriteMessage($"\n  • Mặt phẳng phụ  : '{finalSubName}' ({subTriangleCount:N0} tam giác, đã lọc {subTrimmedCount:N0} tam giác thừa)");
                ed.WriteMessage($"\n  • Kết quả Paste  : '{finalSurfaceName}' ({prevTriangleCount:N0} -> {finalTriangleCount:N0} tam giác)");
                ed.WriteMessage($"\n🎉 Đã tạo mặt phẳng phụ '{finalSubName}' và Paste động vào '{finalSurfaceName}' thành công!");
                ed.WriteMessage($"\n══════════════════════════════════════════════════════════════════\n");

                return new SurfaceCreationResult
                {
                    Success = true,
                    Message = $"Đã bổ sung vào '{finalSurfaceName}' qua mặt phẳng phụ '{finalSubName}' ({prevTriangleCount:N0} → {finalTriangleCount:N0} tam giác)!",
                    MasterSurfaceId = targetSurfaceId,
                    FinalSurfaceName = finalSurfaceName,
                    CreatedSubSurfaceId = subSurfaceId,
                    CreatedSubSurfaceName = finalSubName,
                    SubSurfaceTriangleCount = subTriangleCount,
                    IsSupplementedIntoExisting = true,
                    PreviousTriangleCount = prevTriangleCount,
                    SubSurfaceCount = 1,
                    ProcessedCurveCount = addedBreaklineCount,
                    OpenCurveCount = openCount,
                    ClosedCurveCount = closedCount,
                    TrimmedTriangleCount = subTrimmedCount,
                    FinalTriangleCount = finalTriangleCount
                };
            }
            catch
            {
                if (wasSubLocked && subSurface != null)
                {
                    try { subSurface.Lock = true; } catch { }
                }
                if (wasTargetLocked)
                {
                    try { targetSurface.Lock = true; } catch { }
                }
                throw;
            }
        }

        private static bool DoesGraphExtendOutsideGrid(TinTopologyGraph patchGraph, SpatialTriangleGrid2D oldTriGrid)
        {
            for (int i = 0; i < patchGraph.Vertices.Count; i++)
            {
                Point3d p = patchGraph.Vertices[i].Pos;
                if (!oldTriGrid.ContainsPoint(p.X, p.Y, -1e-4))
                    return true;
            }
            for (int t = 0; t < patchGraph.Triangles.Count; t++)
            {
                Point2d c = patchGraph.Triangles[t].Centroid2D;
                if (!oldTriGrid.ContainsPoint(c.X, c.Y, -1e-4))
                    return true;
            }
            return false;
        }

        internal static bool RemoveRestrictiveOuterBoundaries(CivSurface surface)
        {
            bool removedAny = false;
            try
            {
                for (int i = surface.BoundariesDefinition.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        SurfaceBoundaryType bType = surface.BoundariesDefinition[i].BoundaryType;
                        if (bType == SurfaceBoundaryType.Outer || bType == SurfaceBoundaryType.DataClip)
                        {
                            surface.BoundariesDefinition.RemoveAt(i);
                            removedAny = true;
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return removedAny;
        }

        private static bool RemoveDeleteLineOperations(CivSurface surface)
        {
            bool removedAny = false;
            try
            {
                for (int i = surface.Operations.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        SurfaceOperation op = surface.Operations[i];
                        if (op is SurfaceOperationDeleteMultipleLines || op is SurfaceOperationDeleteLine)
                        {
                            surface.Operations.RemoveAt(i);
                            removedAny = true;
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return removedAny;
        }
    }
}

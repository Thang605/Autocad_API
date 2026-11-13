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
        /// Overload tương thích ngược với chữ ký cũ — chuyển tiếp sang cơ chế mới 0 mặt phẳng phụ.
        /// </summary>
        public static SurfaceCreationResult ThucHienTaoMatPhang(
            Database db,
            Editor ed,
            string masterSurfaceName,
            string masterStyleName,
            string subStyleName,
            List<ObjectId> entityIds,
            bool isMergeMode,
            bool createSnapshot,
            bool deleteSubSurfaces,
            double midOrdDistance,
            bool autoClose,
            bool rebuildAuto)
        {
            var options = new TaoMatPhangOptions
            {
                CreateNewSurface = true,
                SurfaceName = masterSurfaceName,
                SurfaceStyleName = masterStyleName,
                SourceEntityIds = entityIds ?? new List<ObjectId>(),
                BoundaryMode = FeatureLineBoundaryMode.AutoSmart,
                MidOrdinateDistance = midOrdDistance,
                UseSupplementing = true,
                SupplementingDistance = 2.0,
                LimitMaxEdgeLength = true,
                MaxEdgeLength = 25.0,
                AutoCloseNearClosed = autoClose,
                AutoRebuild = rebuildAuto
            };
            return ThucHienTaoMatPhang(db, ed, options);
        }

        /// <summary>
        /// Tạo hoặc cập nhật trực tiếp 1 TIN Surface duy nhất (0 mặt phẳng phụ!) từ tập hợp
        /// Feature Line, Auto Corridor Feature Line, 3D Polyline, 2D Polyline, Line, Arc (cả Hở & Khép kín).
        /// Tự động bóc tách tam giác thừa ở bụng đường cong và khoảng hở giữa các dải rời nhau.
        /// </summary>
        public static SurfaceCreationResult ThucHienTaoMatPhang(
            Database db,
            Editor ed,
            TaoMatPhangOptions options)
        {
            FeatureLineDynamicSyncManager.EnsureStarted();

            if (options == null || options.SourceEntityIds == null || options.SourceEntityIds.Count == 0)
            {
                return new SurfaceCreationResult { Success = false, Message = "Danh sách đối tượng nguồn đang trống." };
            }

            CivilDocument civDoc = CivilApplication.ActiveDocument;
            if (civDoc == null)
            {
                return new SurfaceCreationResult { Success = false, Message = "Không thể truy cập CivilDocument hiện hành." };
            }

            double midOrd = Math.Max(0.01, options.MidOrdinateDistance);
            double suppDist = options.UseSupplementing ? Math.Max(0.20, options.SupplementingDistance) : 100000.0;
            double closeTol = options.AutoCloseNearClosed ? Math.Max(0.005, options.AutoCloseTolerance) : 0.005;

            try
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    // 1. Trích xuất dữ liệu hình học 3D chuẩn xác từ tất cả đối tượng nguồn
                    List<SourceCurveInfo> sourceCurves = new List<SourceCurveInfo>();
                    HashSet<ObjectId> seenIds = new HashSet<ObjectId>();

                    foreach (ObjectId rawId in options.SourceEntityIds)
                    {
                        if (rawId.IsNull || !rawId.IsValid || rawId.IsErased || !seenIds.Add(rawId))
                            continue;

                        SourceCurveInfo? info = ExtractSourceCurveInfo(rawId, tr, midOrd, closeTol, options.AutoCloseNearClosed, sourceCurves.Count);
                        if (info != null && info.Points3D.Count >= 2)
                        {
                            sourceCurves.Add(info);
                        }
                    }

                    if (sourceCurves.Count == 0)
                    {
                        return new SurfaceCreationResult
                        {
                            Success = false,
                            Message = "Không trích xuất được dữ liệu hình học hợp lệ từ các đối tượng đã chọn."
                        };
                    }

                    int openCount = sourceCurves.Count(c => !c.IsClosed);
                    int closedCount = sourceCurves.Count(c => c.IsClosed);

                    if (sourceCurves.Count == 1 && !sourceCurves[0].IsClosed && sourceCurves[0].Points3D.Count < 3)
                    {
                        return new SurfaceCreationResult
                        {
                            Success = false,
                            Message = "Chỉ có 1 đoạn thẳng hở duy nhất (2 điểm) nên không đủ dữ liệu tạo lưới tam giác TIN. Hãy chọn ít nhất 2 đường Feature Line hở hoặc 1 đường khép kín."
                        };
                    }

                    // 2. Nhánh BỔ SUNG / GHÉP VÀO MẶT PHẲNG TIN ĐÃ CÓ SẴN (0 mặt phẳng phụ!)
                    if (!options.CreateNewSurface && !options.ExistingSurfaceId.IsNull &&
                        options.ExistingSurfaceId.IsValid && !options.ExistingSurfaceId.IsErased)
                    {
                        return SupplementFeatureLinesIntoExistingSurface(
                            civDoc,
                            tr,
                            ed,
                            options,
                            sourceCurves,
                            openCount,
                            closedCount,
                            midOrd,
                            suppDist,
                            closeTol);
                    }

                    // 3. Nhánh TẠO MỚI TIN SURFACE DUY NHẤT (0 mặt phẳng phụ!)
                    ObjectId styleId = FindSurfaceStyleId(civDoc, tr, options.SurfaceStyleName);
                    if (styleId == ObjectId.Null)
                    {
                        return new SurfaceCreationResult
                        {
                            Success = false,
                            Message = "Bản vẽ không có Surface Style hợp lệ để tạo mặt phẳng."
                        };
                    }

                    string reqName = string.IsNullOrWhiteSpace(options.SurfaceName)
                        ? $"MatPhang_FL_{DateTime.Now:yyyyMMdd_HHmm}"
                        : options.SurfaceName.Trim();

                    string finalSurfaceName;
                    if (options.OverwriteExistingIfNameMatches)
                    {
                        ObjectId existingSameName = FindSurfaceByName(civDoc, tr, reqName);
                        if (!existingSameName.IsNull && existingSameName.IsValid && !existingSameName.IsErased)
                        {
                            if (tr.GetObject(existingSameName, OpenMode.ForWrite) is CivSurface oldSurf)
                            {
                                oldSurf.Erase(true);
                            }
                        }
                        finalSurfaceName = reqName;
                    }
                    else
                    {
                        finalSurfaceName = EnsureUniqueSurfaceName(civDoc, tr, reqName);
                    }

                    ObjectId targetSurfaceId = TinSurface.Create(finalSurfaceName, styleId);
                    if (targetSurfaceId.IsNull || !targetSurfaceId.IsValid)
                    {
                        return new SurfaceCreationResult
                        {
                            Success = false,
                            Message = "Không thể khởi tạo TIN Surface mới."
                        };
                    }

                    CivSurface? targetSurface = tr.GetObject(targetSurfaceId, OpenMode.ForWrite) as CivSurface;
                    if (targetSurface == null)
                    {
                        return new SurfaceCreationResult
                        {
                            Success = false,
                            Message = "Không thể mở TIN Surface mới ở chế độ ghi (OpenMode.ForWrite)."
                        };
                    }

                    finalSurfaceName = targetSurface.Name;

                    ed.WriteMessage($"\n══════════════════════════════════════════════════════════════════");
                    ed.WriteMessage($"\n🚀 TẠO MẶT PHẲNG TIN TỪ FEATURE LINE (0 MẶT PHẲNG PHỤ)");
                    ed.WriteMessage($"\n  • Mặt phẳng đích : '{finalSurfaceName}' (Tạo mới)");
                    ed.WriteMessage($"\n  • Nguồn dữ liệu  : {sourceCurves.Count} đối tượng ({openCount} đường hở, {closedCount} đường kín)");

                    bool populatedOk = PopulateCleanFeatureLineSurface(
                        targetSurface,
                        options,
                        tr,
                        ed,
                        sourceCurves,
                        midOrd,
                        suppDist,
                        closeTol,
                        usePointsOnlyBreaklines: false,
                        out int addedBreaklineCount,
                        out int trimmedTriangles,
                        out _);

                    if (!populatedOk || addedBreaklineCount == 0)
                    {
                        tr.Abort();
                        return new SurfaceCreationResult
                        {
                            Success = false,
                            Message = "Không thể thêm Breakline nào vào mặt phẳng TIN."
                        };
                    }

                    SetAutoRebuildSafe(targetSurface, options.AutoRebuild);
                    int finalTriangleCount = CountVisibleTriangles(targetSurface);
                    SaveSurfaceRecipe(targetSurface, tr, CreateRecipeFromOptions(db, options, ObjectId.Null));

                    tr.Commit();

                    ed.WriteMessage($"\n  • Đã loại bỏ     : {trimmedTriangles} tam giác thừa (ngoài biên / dây cung cong / bắc cầu)");
                    ed.WriteMessage($"\n  • Lưới hoàn thiện: {finalTriangleCount} tam giác hợp lệ | Số mặt phẳng phụ = 0");
                    ed.WriteMessage($"\n🎉 Hoàn tất mặt phẳng '{finalSurfaceName}' thành công!");
                    ed.WriteMessage($"\n══════════════════════════════════════════════════════════════════\n");

                    return new SurfaceCreationResult
                    {
                        Success = true,
                        Message = $"Đã tạo thành công mặt phẳng '{finalSurfaceName}' ({finalTriangleCount:N0} tam giác, 0 mặt phẳng phụ)!",
                        MasterSurfaceId = targetSurfaceId,
                        FinalSurfaceName = finalSurfaceName,
                        IsSupplementedIntoExisting = false,
                        PreviousTriangleCount = 0,
                        SubSurfaceCount = 0,
                        ProcessedCurveCount = addedBreaklineCount,
                        OpenCurveCount = openCount,
                        ClosedCurveCount = closedCount,
                        TrimmedTriangleCount = trimmedTriangles,
                        FinalTriangleCount = finalTriangleCount
                    };
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n❌ [LỖI] Tạo mặt phẳng từ Feature Line thất bại: {ex.Message}");
                return new SurfaceCreationResult
                {
                    Success = false,
                    Message = ex.Message
                };
            }
        }

        /// <summary>
        /// Tạo lưới TIN sạch cho 1 tập Feature Line trên một mặt phẳng trống (dùng chung cho Tạo mới,
        /// Mặt phẳng có sẵn đang rỗng, hoặc tính toán hình học tạm trong bộ nhớ trước khi gộp).
        /// </summary>
        private static bool PopulateCleanFeatureLineSurface(
            CivSurface targetSurface,
            TaoMatPhangOptions options,
            Transaction tr,
            Editor ed,
            List<SourceCurveInfo> sourceCurves,
            double midOrd,
            double suppDist,
            double closeTol,
            bool usePointsOnlyBreaklines,
            out int addedBreaklineCount,
            out int trimmedTriangles,
            out List<Polygon2D> appliedHolePolys)
        {
            addedBreaklineCount = 0;
            trimmedTriangles = 0;
            appliedHolePolys = new List<Polygon2D>();

            try
            {
                targetSurface.BuildOptions.CrossingBreaklinesElevationOption = CrossingBreaklinesElevationType.UseAverage;
                if (options.BoundaryMode == FeatureLineBoundaryMode.BreaklinesOnly)
                {
                    targetSurface.BuildOptions.UseMaximumTriangleLength = options.LimitMaxEdgeLength;
                    if (options.LimitMaxEdgeLength)
                    {
                        targetSurface.BuildOptions.MaximumTriangleLength = Math.Max(0.5, options.MaxEdgeLength);
                    }
                }
                else
                {
                    targetSurface.BuildOptions.UseMaximumTriangleLength = false;
                }
            }
            catch { }

            foreach (SourceCurveInfo curve in sourceCurves)
            {
                if (AddCurveToSurfaceBreaklines(
                    targetSurface,
                    curve,
                    midOrd,
                    suppDist,
                    usePointsOnly: usePointsOnlyBreaklines,
                    allowDirectCurvedEntity: !usePointsOnlyBreaklines))
                {
                    addedBreaklineCount++;
                }
            }

            if (addedBreaklineCount == 0)
            {
                return false;
            }

            List<Polygon2D> customOuterPolys = new List<Polygon2D>();
            List<Polygon2D> customHidePolys = new List<Polygon2D>();

            if (options.BoundaryMode == FeatureLineBoundaryMode.CustomBoundaryEntities)
            {
                customOuterPolys = ExtractPolygonsFromBoundaryIds(options.CustomOuterBoundaryIds, tr, midOrd);
                customHidePolys = ExtractPolygonsFromBoundaryIds(options.CustomHideBoundaryIds, tr, midOrd);
                appliedHolePolys.AddRange(customHidePolys);

                if (customOuterPolys.Count == 1)
                {
                    AddNativeBoundaryPolygonOrEntity(targetSurface, customOuterPolys[0], midOrd, SurfaceBoundaryType.Outer, nonDestructive: true);
                    foreach (Polygon2D hidePoly in customHidePolys)
                    {
                        AddNativeBoundaryPolygonOrEntity(targetSurface, hidePoly, midOrd, SurfaceBoundaryType.Hide, nonDestructive: true);
                    }
                    targetSurface.Rebuild();
                    return true;
                }
                else if (customOuterPolys.Count > 1)
                {
                    foreach (Polygon2D poly in customOuterPolys)
                    {
                        AddPolygon2DAsNonDestructiveBreakline(targetSurface, poly, midOrd);
                    }
                    foreach (Polygon2D hidePoly in customHidePolys)
                    {
                        AddPolygon2DAsNonDestructiveBreakline(targetSurface, hidePoly, midOrd);
                    }
                    foreach (Polygon2D hidePoly in customHidePolys)
                    {
                        AddNativeBoundaryPolygonOrEntity(targetSurface, hidePoly, midOrd, SurfaceBoundaryType.Hide, nonDestructive: true);
                    }
                }
            }

            List<Polygon2D> closedRegions = new List<Polygon2D>();
            List<Polygon2D> holeRegions = new List<Polygon2D>();
            HashSet<int> curvesInClosedLoops = new HashSet<int>();

            if (options.BoundaryMode == FeatureLineBoundaryMode.AutoSmart)
            {
                BuildClosedAndChainedRegions(
                    sourceCurves,
                    closeTol,
                    options.TreatNestedClosedAsHole,
                    out closedRegions,
                    out holeRegions,
                    out curvesInClosedLoops);

                appliedHolePolys.AddRange(holeRegions);
                bool allCurvesFormClosedRegions = curvesInClosedLoops.Count == sourceCurves.Count && closedRegions.Count > 0;

                if (allCurvesFormClosedRegions && closedRegions.Count == 1)
                {
                    AddNativeBoundaryPolygonOrEntity(targetSurface, closedRegions[0], midOrd, SurfaceBoundaryType.Outer, nonDestructive: false);
                    foreach (Polygon2D hole in holeRegions)
                    {
                        AddNativeBoundaryPolygonOrEntity(targetSurface, hole, midOrd, SurfaceBoundaryType.Hide, nonDestructive: false);
                    }
                    targetSurface.Rebuild();
                    return true;
                }

                foreach (Polygon2D hole in holeRegions)
                {
                    AddNativeBoundaryPolygonOrEntity(targetSurface, hole, midOrd, SurfaceBoundaryType.Hide, nonDestructive: false);
                }
            }

            targetSurface.Rebuild();

            if (options.BoundaryMode == FeatureLineBoundaryMode.AutoSmart)
            {
                bool allClosed = curvesInClosedLoops.Count == sourceCurves.Count && closedRegions.Count > 0;
                trimmedTriangles = TrimUnwantedTrianglesOnSingleSurface(
                    targetSurface,
                    sourceCurves,
                    curvesInClosedLoops,
                    closedRegions,
                    holeRegions,
                    allClosed,
                    null,
                    midOrd,
                    options.LimitMaxEdgeLength,
                    Math.Max(0.5, options.MaxEdgeLength),
                    ed);
            }
            else if (options.BoundaryMode == FeatureLineBoundaryMode.CustomBoundaryEntities && customOuterPolys.Count > 0)
            {
                trimmedTriangles = TrimTrianglesByCustomPolygons(
                    targetSurface,
                    customOuterPolys,
                    customHidePolys,
                    midOrd,
                    ed);
            }

            return true;
        }
    }
}

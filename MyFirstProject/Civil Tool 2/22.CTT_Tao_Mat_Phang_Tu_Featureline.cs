using System;
using System.Collections.Generic;
using System.Linq;
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

[assembly: CommandClass(typeof(MyFirstProject.Civil_Tool_2.CTT_Tao_Mat_Phang_Tu_Featureline_Command))]

namespace MyFirstProject.Civil_Tool_2
{
    public enum FeatureLineBoundaryMode
    {
        /// <summary>
        /// Tự động nhận diện đường hở & đường kín, cắt tam giác dây cung đường cong và tam giác bắc cầu mà không tạo mặt phẳng phụ.
        /// </summary>
        AutoSmart = 0,

        /// <summary>
        /// Giới hạn phạm vi theo các đường Biên bao ngoài (Outer) và Lỗ khoét (Hide) do người dùng chọn riêng trên CAD.
        /// </summary>
        CustomBoundaryEntities = 1,

        /// <summary>
        /// Chỉ thêm Breaklines thuần túy (kèm tùy chọn giới hạn Max Triangle Length chuẩn Civil 3D).
        /// </summary>
        BreaklinesOnly = 2
    }

    public class TaoMatPhangOptions
    {
        public bool CreateNewSurface { get; set; } = true;
        public string SurfaceName { get; set; } = string.Empty;
        public ObjectId ExistingSurfaceId { get; set; } = ObjectId.Null;
        public bool OverwriteExistingIfNameMatches { get; set; } = false;
        public string SurfaceStyleName { get; set; } = string.Empty;

        public List<ObjectId> SourceEntityIds { get; set; } = new List<ObjectId>();
        public FeatureLineBoundaryMode BoundaryMode { get; set; } = FeatureLineBoundaryMode.AutoSmart;
        public List<ObjectId> CustomOuterBoundaryIds { get; set; } = new List<ObjectId>();
        public List<ObjectId> CustomHideBoundaryIds { get; set; } = new List<ObjectId>();

        public double MidOrdinateDistance { get; set; } = 0.10;
        public bool UseSupplementing { get; set; } = true;
        public double SupplementingDistance { get; set; } = 2.00;

        public bool LimitMaxEdgeLength { get; set; } = true;
        public double MaxEdgeLength { get; set; } = 25.00;

        public bool AutoCloseNearClosed { get; set; } = true;
        public double AutoCloseTolerance { get; set; } = 0.10;
        public bool TreatNestedClosedAsHole { get; set; } = false;
        public bool AutoRebuild { get; set; } = true;

        /// <summary>
        /// Khi bổ sung vào mặt phẳng TIN có sẵn:
        /// true  = Đắp đè cao độ mới tại vùng chồng lấn (thay thế điểm/lưới cũ bên dưới Feature Line mới, 0 mặt phẳng phụ).
        /// false = Giữ nguyên cao độ mặt phẳng cũ tại vùng chồng lấn, chỉ nối mở rộng thêm phần diện tích mới bên ngoài.
        /// </summary>
        public bool OverwriteOverlapOnExisting { get; set; } = true;

        /// <summary>
        /// Khi mặt phẳng đích có nguồn gốc trực tiếp từ Corridor / Grading (FromCorridor / FromGrading):
        /// Tự động tạo hoặc cập nhật mặt phẳng tổng hợp '&lt;Tên&gt;_Gop' kế thừa động mặt phẳng gốc + ghép phần Feature Line mới,
        /// giúp không bị cắt bởi Corridor Extents và không mất dữ liệu khi Rebuild Corridor.
        /// </summary>
        public bool CreateCombinedForCorridorSurface { get; set; } = true;
        public string CombinedSurfaceName { get; set; } = string.Empty;
    }

    public class SurfaceCreationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public ObjectId MasterSurfaceId { get; set; } = ObjectId.Null;
        public string FinalSurfaceName { get; set; } = string.Empty;
        public bool IsSupplementedIntoExisting { get; set; }
        public int PreviousTriangleCount { get; set; }
        public int SubSurfaceCount { get; set; }
        public int ProcessedCurveCount { get; set; }
        public int OpenCurveCount { get; set; }
        public int ClosedCurveCount { get; set; }
        public int TrimmedTriangleCount { get; set; }
        public int FinalTriangleCount { get; set; }
    }

    public class CTT_Tao_Mat_Phang_Tu_Featureline_Command
    {
        [CommandMethod("CTT_Tao_Mat_Phang_Tu_Feature_Line")]
        public static void TaoMatPhangTuFeatureline()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            Editor ed = doc.Editor;
            Database db = doc.Database;

            try
            {
                using (var form = new TaoMatPhangTuFeaturelineForm(db, ed))
                {
                    Application.ShowModalDialog(form);
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n[LỖI] Không thể mở giao diện tạo mặt phẳng từ Feature Line: {ex.Message}");
            }
        }

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
                    AddNativeBoundaryPolygon(targetSurface, customOuterPolys[0], midOrd, SurfaceBoundaryType.Outer);
                    foreach (Polygon2D hidePoly in customHidePolys)
                    {
                        AddNativeBoundaryPolygon(targetSurface, hidePoly, midOrd, SurfaceBoundaryType.Hide);
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
                        AddNativeBoundaryPolygon(targetSurface, hidePoly, midOrd, SurfaceBoundaryType.Hide);
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
                    AddNativeBoundaryPolygon(targetSurface, closedRegions[0], midOrd, SurfaceBoundaryType.Outer);
                    foreach (Polygon2D hole in holeRegions)
                    {
                        AddNativeBoundaryPolygon(targetSurface, hole, midOrd, SurfaceBoundaryType.Hide);
                    }
                    targetSurface.Rebuild();
                    return true;
                }

                foreach (Polygon2D hole in holeRegions)
                {
                    AddNativeBoundaryPolygon(targetSurface, hole, midOrd, SurfaceBoundaryType.Hide);
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

        #region SUPPLEMENT / MERGE INTO EXISTING SURFACE (ZERO SUB-SURFACES)

        /// <summary>
        /// Bổ sung mảng mặt phẳng mới tạo bởi Feature Line vào một mặt phẳng TIN ĐÃ CÓ SẴN mà không để lại mặt phẳng phụ:
        /// - Tự xử lý mặt phẳng gốc từ Corridor/Grading (gộp an toàn sang '&lt;Tên&gt;_Gop' nếu bật bảo vệ Corridor).
        /// - Gỡ bỏ bẫy Outer Boundary cũ cắt mất vùng mới mở rộng bên ngoài, đồng thời khóa nguyên vẹn đường biên cũ.
        /// - Xử lý chính xác vùng chồng lấn (Đắp đè cao độ mới thay thế điểm cũ, hoặc Giữ nguyên mặt phẳng cũ chỉ nối phần mới).
        /// - Xóa sạch 100% tam giác bắc cầu nối ngang qua khe hở giữa mặt phẳng cũ và mặt phẳng mới.
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

        private static bool RemoveRestrictiveOuterBoundaries(CivSurface surface)
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

        /// <summary>
        /// Cắt đoạn thẳng 3D (pA -> pB) chỉ giữ phần nằm BÊN NGOÀI vùng regionGrid (khớp cao độ êm tại điểm giao biên).
        /// </summary>
        private static void AppendClippedSegmentsOutsideRegion(
            Point3d pA,
            Point3d pB,
            SpatialTriangleGrid2D regionGrid,
            SpatialBorderSegmentGrid2D regionBorderGrid,
            List<(Point3d A, Point3d B)> outSegments)
        {
            bool aStrictIn = regionGrid.ContainsPoint(pA.X, pA.Y, -1e-4) && !regionBorderGrid.IsNearAnySegment(pA.X, pA.Y, 0.012);
            bool bStrictIn = regionGrid.ContainsPoint(pB.X, pB.Y, -1e-4) && !regionBorderGrid.IsNearAnySegment(pB.X, pB.Y, 0.012);

            if (aStrictIn && bStrictIn)
            {
                return;
            }

            if (!aStrictIn && !bStrictIn)
            {
                double mx = 0.5 * (pA.X + pB.X);
                double my = 0.5 * (pA.Y + pB.Y);
                bool midStrictIn = regionGrid.ContainsPoint(mx, my, -1e-4) && !regionBorderGrid.IsNearAnySegment(mx, my, 0.012);
                if (!midStrictIn)
                {
                    outSegments.Add((pA, pB));
                    return;
                }

                // Đoạn thẳng xuyên qua vùng ở giữa: tách làm 2 đoạn hai đầu ngoài biên
                Point3d midPt = new Point3d(mx, my, 0.5 * (pA.Z + pB.Z));
                Point3d hit1 = BisectRegionBorderCrossing(pA, midPt, regionGrid);
                Point3d hit2 = BisectRegionBorderCrossing(pB, midPt, regionGrid);
                if (pA.DistanceTo(hit1) > 1e-3) outSegments.Add((pA, hit1));
                if (hit2.DistanceTo(pB) > 1e-3) outSegments.Add((hit2, pB));
                return;
            }

            if (!aStrictIn && bStrictIn)
            {
                Point3d hit = BisectRegionBorderCrossing(pA, pB, regionGrid);
                if (pA.DistanceTo(hit) > 1e-3)
                {
                    outSegments.Add((pA, hit));
                }
            }
            else
            {
                Point3d hit = BisectRegionBorderCrossing(pB, pA, regionGrid);
                if (hit.DistanceTo(pB) > 1e-3)
                {
                    outSegments.Add((hit, pB));
                }
            }
        }

        private static Point3d BisectRegionBorderCrossing(Point3d outsidePt, Point3d insidePt, SpatialTriangleGrid2D regionGrid)
        {
            double lo = 0.0;
            double hi = 1.0;
            for (int iter = 0; iter < 11; iter++)
            {
                double mid = 0.5 * (lo + hi);
                double x = outsidePt.X + mid * (insidePt.X - outsidePt.X);
                double y = outsidePt.Y + mid * (insidePt.Y - outsidePt.Y);
                if (regionGrid.ContainsPoint(x, y, 0.0))
                    hi = mid;
                else
                    lo = mid;
            }

            double t = hi;
            double hx = outsidePt.X + t * (insidePt.X - outsidePt.X);
            double hy = outsidePt.Y + t * (insidePt.Y - outsidePt.Y);
            double hz = outsidePt.Z + t * (insidePt.Z - outsidePt.Z);
            if (regionGrid.TryInterpolateZ(hx, hy, out double regionZ, 0.02))
            {
                hz = regionZ;
            }
            return new Point3d(hx, hy, hz);
        }

        private static List<Point3dCollection> ChainSegmentsToPolylines(List<(Point3d A, Point3d B)> segments)
        {
            var result = new List<Point3dCollection>();
            if (segments == null || segments.Count == 0) return result;

            var cleanSegs = new List<(Point3d A, Point3d B, QuantizedPoint2D QA, QuantizedPoint2D QB)>(segments.Count);
            var seenUndirected = new HashSet<(QuantizedPoint2D, QuantizedPoint2D)>();

            for (int i = 0; i < segments.Count; i++)
            {
                Point3d a = segments[i].A;
                Point3d b = segments[i].B;
                QuantizedPoint2D qa = QuantizedPoint2D.From(a.X, a.Y);
                QuantizedPoint2D qb = QuantizedPoint2D.From(b.X, b.Y);
                if (qa.Equals(qb)) continue;

                var key = qa.QX < qb.QX || (qa.QX == qb.QX && qa.QY <= qb.QY) ? (qa, qb) : (qb, qa);
                if (!seenUndirected.Add(key)) continue;

                cleanSegs.Add((a, b, qa, qb));
            }

            if (cleanSegs.Count == 0) return result;

            var adj = new Dictionary<QuantizedPoint2D, List<int>>();
            for (int i = 0; i < cleanSegs.Count; i++)
            {
                var s = cleanSegs[i];
                if (!adj.TryGetValue(s.QA, out var la)) { la = new List<int>(2); adj[s.QA] = la; }
                if (!adj.TryGetValue(s.QB, out var lb)) { lb = new List<int>(2); adj[s.QB] = lb; }
                la.Add(i);
                lb.Add(i);
            }

            bool[] used = new bool[cleanSegs.Count];

            void TraceFrom(int startSegIdx, bool startForward)
            {
                used[startSegIdx] = true;
                var seg0 = cleanSegs[startSegIdx];
                var pts = new Point3dCollection();
                Point3d firstPt = startForward ? seg0.A : seg0.B;
                Point3d curPt = startForward ? seg0.B : seg0.A;
                QuantizedPoint2D curKey = startForward ? seg0.QB : seg0.QA;

                pts.Add(firstPt);
                pts.Add(curPt);

                while (adj.TryGetValue(curKey, out var inc))
                {
                    int nextIdx = -1;
                    for (int k = 0; k < inc.Count; k++)
                    {
                        if (!used[inc[k]])
                        {
                            nextIdx = inc[k];
                            break;
                        }
                    }
                    if (nextIdx < 0) break;

                    used[nextIdx] = true;
                    var nextSeg = cleanSegs[nextIdx];
                    if (nextSeg.QA.Equals(curKey))
                    {
                        curPt = nextSeg.B;
                        curKey = nextSeg.QB;
                    }
                    else
                    {
                        curPt = nextSeg.A;
                        curKey = nextSeg.QA;
                    }
                    pts.Add(curPt);
                }

                if (pts.Count >= 2)
                {
                    result.Add(pts);
                }
            }

            // Ưu tiên lần theo các nút bậc != 2 (đầu mút hở hoặc ngã ba) trước, sau đó mới quét các chu trình kín bậc 2
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < cleanSegs.Count; i++)
                {
                    if (used[i]) continue;
                    var s = cleanSegs[i];
                    if (pass == 0)
                    {
                        int degA = adj[s.QA].Count;
                        int degB = adj[s.QB].Count;
                        if (degA != 2)
                            TraceFrom(i, startForward: true);
                        else if (degB != 2)
                            TraceFrom(i, startForward: false);
                    }
                    else
                    {
                        TraceFrom(i, startForward: true);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Đồng bộ đỉnh & cao độ tại vùng giao chồng sau Rebuild():
        /// - Chế độ Đắp đè (OverwriteOverlapOnExisting = true): Xóa toàn bộ đỉnh cũ nằm lọt trong lòng R_new,
        ///   bổ sung đỉnh R_new bị thiếu (nếu mặt phẳng đích có PasteSurface/Snapshot) và ép khớp chuẩn xác cao độ Z của R_new.
        /// - Chế độ Giữ nguyên mặt cũ (OverwriteOverlapOnExisting = false): Bổ sung đỉnh trong R_new nằm ngoài R_old nếu thiếu,
        ///   và xóa đỉnh mới vô tình lọt vào lòng R_old.
        /// </summary>
        private static void ReconcileMergedVerticesAndElevations(
            CivSurface targetSurface,
            TinTopologyGraph patchGraph,
            SpatialTriangleGrid2D newPatchGrid,
            SpatialBorderSegmentGrid2D newPatchBorderGrid,
            SpatialVertexLookup2D newPatchVertexLookup,
            SpatialTriangleGrid2D oldTriGrid,
            SpatialBorderSegmentGrid2D oldBorderGrid,
            SpatialVertexLookup2D oldVertexLookup,
            bool overwriteOverlap)
        {
            if (overwriteOverlap)
            {
                double minX = newPatchGrid.MinX - 0.10;
                double maxX = newPatchGrid.MaxX + 0.10;
                double minY = newPatchGrid.MinY - 0.10;
                double maxY = newPatchGrid.MaxY + 0.10;

                List<TinSurfaceVertex> staleInteriorVertices = new List<TinSurfaceVertex>();
                try
                {
                    foreach (TinSurfaceVertex v in targetSurface.Vertices)
                    {
                        if (v == null || !v.IsValid) continue;
                        Point3d loc = v.Location;
                        if (loc.X < minX || loc.X > maxX || loc.Y < minY || loc.Y > maxY)
                            continue;

                        if (newPatchVertexLookup.TryFindNearestZ(loc.X, loc.Y, 0.015, out _))
                            continue;

                        if (newPatchGrid.ContainsPoint(loc.X, loc.Y, -1e-4) &&
                            !newPatchBorderGrid.IsNearAnySegment(loc.X, loc.Y, 0.008))
                        {
                            staleInteriorVertices.Add(v);
                        }
                    }
                }
                catch { }

                DeleteVerticesSafe(targetSurface, staleInteriorVertices);

                // Quét lại tập đỉnh hiện hành trong khung bao R_new để kiểm tra đỉnh thiếu (do PasteSurface/Snapshot) & cao độ Z
                var liveVerticesInPatchBox = new Dictionary<QuantizedPoint2D, TinSurfaceVertex>();
                var survivingNonPatchVertices = new List<TinSurfaceVertex>();

                try
                {
                    foreach (TinSurfaceVertex v in targetSurface.Vertices)
                    {
                        if (v == null || !v.IsValid) continue;
                        Point3d loc = v.Location;
                        if (loc.X < minX || loc.X > maxX || loc.Y < minY || loc.Y > maxY)
                            continue;

                        liveVerticesInPatchBox[QuantizedPoint2D.From(loc.X, loc.Y)] = v;

                        if (!newPatchVertexLookup.TryFindNearestZ(loc.X, loc.Y, 0.015, out _) &&
                            newPatchGrid.ContainsPoint(loc.X, loc.Y, 0.005))
                        {
                            survivingNonPatchVertices.Add(v);
                        }
                    }
                }
                catch { }

                Point3dCollection missingPatchPts = new Point3dCollection();
                HashSet<int> missingPatchVertexIndices = new HashSet<int>();

                for (int i = 0; i < patchGraph.Vertices.Count; i++)
                {
                    Point3d pv = patchGraph.Vertices[i].Pos;
                    TinSurfaceVertex? matchedLive = FindLiveVertexNear(liveVerticesInPatchBox, pv.X, pv.Y);
                    if (matchedLive != null)
                    {
                        if (Math.Abs(matchedLive.Location.Z - pv.Z) > 0.002)
                        {
                            try { targetSurface.SetVertexElevation(matchedLive, pv.Z); } catch { }
                        }
                    }
                    else
                    {
                        missingPatchPts.Add(pv);
                        missingPatchVertexIndices.Add(i);
                    }
                }

                // Nếu còn đỉnh giao cắt cũ không xóa được nằm trong R_new -> ép phẳng cao độ Z theo mặt phẳng R_new
                foreach (TinSurfaceVertex sv in survivingNonPatchVertices)
                {
                    try
                    {
                        if (sv.IsValid && newPatchGrid.TryInterpolateZ(sv.Location.X, sv.Location.Y, out double interpZ, 0.01))
                        {
                            if (Math.Abs(sv.Location.Z - interpZ) > 0.003)
                            {
                                targetSurface.SetVertexElevation(sv, interpZ);
                            }
                        }
                    }
                    catch { }
                }

                if (missingPatchPts.Count > 0)
                {
                    try
                    {
                        targetSurface.AddVertices(missingPatchPts);
                    }
                    catch
                    {
                        foreach (Point3d pt in missingPatchPts)
                        {
                            try { targetSurface.AddVertex(pt); } catch { }
                        }
                    }

                    // Đảo cạnh (SwapEdge) nếu cạnh Breakline của R_new trong vùng PasteSurface bị Delaunay quay chéo
                    EnforceMissingPatchLockedEdges(targetSurface, patchGraph, missingPatchVertexIndices);
                }
            }
            else
            {
                double minX = newPatchGrid.MinX - 0.10;
                double maxX = newPatchGrid.MaxX + 0.10;
                double minY = newPatchGrid.MinY - 0.10;
                double maxY = newPatchGrid.MaxY + 0.10;

                var liveVerticesInPatchBox = new Dictionary<QuantizedPoint2D, TinSurfaceVertex>();
                var unwantedIntrudingVertices = new List<TinSurfaceVertex>();

                try
                {
                    foreach (TinSurfaceVertex v in targetSurface.Vertices)
                    {
                        if (v == null || !v.IsValid) continue;
                        Point3d loc = v.Location;
                        if (loc.X < minX || loc.X > maxX || loc.Y < minY || loc.Y > maxY)
                            continue;

                        liveVerticesInPatchBox[QuantizedPoint2D.From(loc.X, loc.Y)] = v;

                        if (!oldVertexLookup.TryFindNearestZ(loc.X, loc.Y, 0.015, out _) &&
                            oldTriGrid.ContainsPoint(loc.X, loc.Y, -1e-4) &&
                            !oldBorderGrid.IsNearAnySegment(loc.X, loc.Y, 0.012))
                        {
                            unwantedIntrudingVertices.Add(v);
                        }
                    }
                }
                catch { }

                DeleteVerticesSafe(targetSurface, unwantedIntrudingVertices);

                Point3dCollection missingExteriorPts = new Point3dCollection();
                for (int i = 0; i < patchGraph.Vertices.Count; i++)
                {
                    Point3d pv = patchGraph.Vertices[i].Pos;
                    if (oldTriGrid.ContainsPoint(pv.X, pv.Y, -1e-4) && !oldBorderGrid.IsNearAnySegment(pv.X, pv.Y, 0.012))
                        continue;

                    if (FindLiveVertexNear(liveVerticesInPatchBox, pv.X, pv.Y) == null)
                    {
                        missingExteriorPts.Add(pv);
                    }
                }

                if (missingExteriorPts.Count > 0)
                {
                    try { targetSurface.AddVertices(missingExteriorPts); } catch { }
                }
            }
        }

        private static TinSurfaceVertex? FindLiveVertexNear(
            Dictionary<QuantizedPoint2D, TinSurfaceVertex> map,
            double x,
            double y)
        {
            long qx = (long)Math.Round(x * 1000.0);
            long qy = (long)Math.Round(y * 1000.0);

            if (map.TryGetValue(new QuantizedPoint2D(qx, qy), out TinSurfaceVertex? exact) && exact.IsValid)
                return exact;

            // Tìm lân cận +/- 15mm phòng sai số lượng tử hóa
            for (int dx = -15; dx <= 15; dx += 5)
            {
                for (int dy = -15; dy <= 15; dy += 5)
                {
                    if (map.TryGetValue(new QuantizedPoint2D(qx + dx, qy + dy), out TinSurfaceVertex? near) && near.IsValid)
                        return near;
                }
            }

            // Fallback quét chính xác nếu cần
            foreach (var kvp in map)
            {
                if (Math.Abs(kvp.Key.QX - qx) <= 15 && Math.Abs(kvp.Key.QY - qy) <= 15 && kvp.Value.IsValid)
                    return kvp.Value;
            }
            return null;
        }

        private static void DeleteVerticesSafe(CivSurface surface, List<TinSurfaceVertex> verticesToDelete)
        {
            if (verticesToDelete == null || verticesToDelete.Count == 0) return;

            try
            {
                surface.DeleteVertices(verticesToDelete);
            }
            catch
            {
                foreach (TinSurfaceVertex v in verticesToDelete)
                {
                    try
                    {
                        if (v != null && v.IsValid)
                            surface.DeleteVertex(v);
                    }
                    catch { }
                }
            }
        }

        private static void EnforceMissingPatchLockedEdges(
            CivSurface surface,
            TinTopologyGraph patchGraph,
            HashSet<int> missingVertexIndices)
        {
            for (int eIdx = 0; eIdx < patchGraph.Edges.Count; eIdx++)
            {
                TinEdgeNode edge = patchGraph.Edges[eIdx];
                bool isBnd = (edge.TriA >= 0) != (edge.TriB >= 0);
                if (!edge.IsLocked && !isBnd) continue;
                if (!missingVertexIndices.Contains(edge.U) && !missingVertexIndices.Contains(edge.V))
                    continue;

                Point3d pu = patchGraph.Vertices[edge.U].Pos;
                Point3d pv = patchGraph.Vertices[edge.V].Pos;
                double mx = 0.5 * (pu.X + pv.X);
                double my = 0.5 * (pu.Y + pv.Y);

                try
                {
                    TinSurfaceEdge? curEdge = surface.FindEdgeAtXY(mx, my);
                    if (curEdge != null && curEdge.IsValid)
                    {
                        Point3d e1 = curEdge.Vertex1.Location;
                        Point3d e2 = curEdge.Vertex2.Location;
                        bool matchesForward = Hypot2D(e1.X - pu.X, e1.Y - pu.Y) < 0.03 && Hypot2D(e2.X - pv.X, e2.Y - pv.Y) < 0.03;
                        bool matchesReverse = Hypot2D(e1.X - pv.X, e1.Y - pv.Y) < 0.03 && Hypot2D(e2.X - pu.X, e2.Y - pu.Y) < 0.03;
                        if (!matchesForward && !matchesReverse)
                        {
                            surface.SwapEdge(curEdge);
                        }
                    }
                }
                catch { }
            }
        }

        private static double Hypot2D(double dx, double dy) => Math.Sqrt(dx * dx + dy * dy);

        public static SurfaceOriginationType GetSurfaceOriginationType(Database db, ObjectId surfaceId)
        {
            if (surfaceId.IsNull || !surfaceId.IsValid || surfaceId.IsErased)
                return SurfaceOriginationType.None;

            try
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    SurfaceOriginationType result = SurfaceOriginationType.None;
                    if (tr.GetObject(surfaceId, OpenMode.ForRead) is CivSurface surf)
                    {
                        result = surf.OriginationType;
                    }
                    tr.Commit();
                    return result;
                }
            }
            catch
            {
                return SurfaceOriginationType.None;
            }
        }

        #endregion

        #region CORE ZERO-SUBSURFACE TRIANGLE TRIMMING ENGINE

        /// <summary>
        /// Thuật toán cắt lọc tam giác thừa trực tiếp trên 1 TIN Surface duy nhất (không dùng mặt phẳng phụ):
        /// 1) Bảo vệ toàn bộ tam giác nằm trong các vùng khép kín (Closed FeatureLines / Chained Loops) và tam giác cũ (nếu gộp thêm).
        /// 2) Với đường hở (Taluy, Rãnh, Lề, Nút giao, Corridor FL):
        ///    - Ánh xạ các đỉnh TIN lên từng đường nguồn và sắp xếp theo lý trình dọc đường.
        ///    - Loại bỏ tam giác có cạnh không thuộc Breakline vượt quá ngưỡng MaxEdgeLength (chống nối bắc cầu qua lòng đường / nhánh rời).
        ///    - Lan truyền BFS từ biên ngoài (Convex Hull / Void) vào trong để bóc sạch 100% tam giác dây cung ở bụng đường cong
        ///      (cạnh nối tắt 2 đỉnh không liền kề |i - j| >= 2 trên cùng một đường nguồn), dừng chính xác tuyệt đối tại cạnh Breakline!
        /// </summary>
        private static int TrimUnwantedTrianglesOnSingleSurface(
            CivSurface surface,
            List<SourceCurveInfo> sourceCurves,
            HashSet<int> curvesInClosedLoops,
            List<Polygon2D> closedRegions,
            List<Polygon2D> holeRegions,
            bool allCurvesAreClosedRegions,
            HashSet<QuantizedPoint2D>? preExistingVertices,
            double midOrdDistance,
            bool limitMaxEdgeLength,
            double maxEdgeLength,
            Editor ed)
        {
            TinTopologyGraph graph = BuildTinTopologyGraph(surface);
            if (graph.Triangles.Count == 0) return 0;

            int triCount = graph.Triangles.Count;
            bool[] removed = new bool[triCount];
            bool[] protectedInside = new bool[triCount];

            // 1. Đánh dấu bảo vệ tam giác nằm trong các vùng khép kín (và ngoài lỗ khoét)
            for (int t = 0; t < triCount; t++)
            {
                Point2d c = graph.Triangles[t].Centroid2D;

                bool inHole = false;
                for (int h = 0; h < holeRegions.Count; h++)
                {
                    if (holeRegions[h].Contains(c.X, c.Y))
                    {
                        inHole = true;
                        break;
                    }
                }
                if (inHole)
                {
                    removed[t] = true;
                    continue;
                }

                bool inClosed = false;
                for (int r = 0; r < closedRegions.Count; r++)
                {
                    if (closedRegions[r].Contains(c.X, c.Y))
                    {
                        inClosed = true;
                        break;
                    }
                }

                if (inClosed)
                {
                    protectedInside[t] = true;
                    continue;
                }

                // Bảo vệ các tam giác cũ của mặt phẳng hiện hữu (khi chạy chế độ Bổ sung vào mặt phẳng có sẵn)
                if (preExistingVertices != null && preExistingVertices.Count > 0)
                {
                    var tri = graph.Triangles[t];
                    if (preExistingVertices.Contains(graph.Vertices[tri.V0].QKey) &&
                        preExistingVertices.Contains(graph.Vertices[tri.V1].QKey) &&
                        preExistingVertices.Contains(graph.Vertices[tri.V2].QKey))
                    {
                        protectedInside[t] = true;
                        continue;
                    }
                }

                // Nếu TẤT CẢ đối tượng nguồn đều là các vùng khép kín (ví dụ N mặt móng / đảo rời nhau),
                // thì mọi tam giác nằm ngoài hợp của các đa giác kín đều bị loại bỏ!
                if (allCurvesAreClosedRegions)
                {
                    removed[t] = true;
                }
            }

            // 2. Nếu có các đường Feature Line HỞ: chạy thuật toán nhận diện cạnh Breakline + Bóc tách BFS biên dải
            if (!allCurvesAreClosedRegions)
            {
                // Dung sai 2D chặt (12mm) kết hợp kiểm tra độ lệch cao độ Z (35mm) để phân biệt tuyệt đối
                // các đường sát nhau như Đỉnh bó vỉa - Chân bó vỉa (cách 2cm - 5cm) hoặc tường chắn!
                double matchTol2D = Math.Max(0.012, Math.Min(0.025, midOrdDistance * 0.35));
                double matchTolZ = 0.04;
                CurveVertexAttachmentMap attachment = BuildCurveVertexAttachmentMap(sourceCurves, graph.Vertices, matchTol2D, matchTolZ);

                // Đánh dấu cạnh nào của TIN là cạnh Breakline thực sự (IsLocked hoặc 2 đỉnh liền kề trên cùng 1 đường nguồn hoặc trùng đoạn thẳng nguồn)
                bool[] isBreaklineEdge = new bool[graph.Edges.Count];
                for (int eIdx = 0; eIdx < graph.Edges.Count; eIdx++)
                {
                    TinEdgeNode edge = graph.Edges[eIdx];
                    if (edge.IsLocked || attachment.IsConsecutiveCurveSegment(edge.U, edge.V, graph.Vertices))
                    {
                        isBreaklineEdge[eIdx] = true;
                    }
                }

                // Pha 2A: Loại bỏ các tam giác (không được bảo vệ) có cạnh KHÔNG thuộc Breakline dài vượt ngưỡng MaxEdgeLength
                if (limitMaxEdgeLength)
                {
                    double maxLenSq = maxEdgeLength * maxEdgeLength;
                    for (int t = 0; t < triCount; t++)
                    {
                        if (removed[t] || protectedInside[t]) continue;

                        TinTriangleNode tri = graph.Triangles[t];
                        if ((!isBreaklineEdge[tri.E0] && graph.Edges[tri.E0].Length2DSq > maxLenSq) ||
                            (!isBreaklineEdge[tri.E1] && graph.Edges[tri.E1].Length2DSq > maxLenSq) ||
                            (!isBreaklineEdge[tri.E2] && graph.Edges[tri.E2].Length2DSq > maxLenSq))
                        {
                            removed[t] = true;
                        }
                    }
                }

                // Pha 2B: Lan truyền BFS từ toàn bộ các cạnh biên lộ ra ngoài (Convex Hull hoặc sát vùng vừa xóa)
                // để bóc sạch 100% tam giác dây cung ở bụng đường cong (|rankU - rankV| >= 2 trên cùng đường hở)
                Queue<int> exposedEdgeQueue = new Queue<int>();
                bool[] inQueue = new bool[graph.Edges.Count];

                for (int eIdx = 0; eIdx < graph.Edges.Count; eIdx++)
                {
                    if (IsEdgeExposedToVoid(graph.Edges[eIdx], removed, out _))
                    {
                        exposedEdgeQueue.Enqueue(eIdx);
                        inQueue[eIdx] = true;
                    }
                }

                double maxEdgeLenVal = limitMaxEdgeLength ? maxEdgeLength : double.MaxValue;

                while (exposedEdgeQueue.Count > 0)
                {
                    int eIdx = exposedEdgeQueue.Dequeue();
                    inQueue[eIdx] = false;

                    if (isBreaklineEdge[eIdx])
                        continue; // Tuyệt đối không bao giờ bóc xuyên qua cạnh Breakline!

                    if (!IsEdgeExposedToVoid(graph.Edges[eIdx], removed, out int liveTriIdx))
                        continue;

                    if (protectedInside[liveTriIdx])
                        continue;

                    TinEdgeNode edge = graph.Edges[eIdx];
                    double edgeLen = Math.Sqrt(edge.Length2DSq);

                    bool shouldPeel = false;

                    // Điều kiện 1: Cạnh biên lộ ra ngoài vượt quá chiều dài tối đa cho phép
                    if (limitMaxEdgeLength && edgeLen > maxEdgeLenVal)
                    {
                        shouldPeel = true;
                    }
                    // Điều kiện 2: Cạnh biên là dây cung nối tắt qua ít nhất 1 đỉnh trung gian trên CÙNG một đường nguồn
                    else if (attachment.IsShortcutChordOnSameCurve(edge.U, edge.V, edgeLen, curvesInClosedLoops))
                    {
                        shouldPeel = true;
                    }

                    if (shouldPeel)
                    {
                        removed[liveTriIdx] = true;
                        TinTriangleNode peeledTri = graph.Triangles[liveTriIdx];
                        int[] triEdges = { peeledTri.E0, peeledTri.E1, peeledTri.E2 };
                        foreach (int nextEdgeIdx in triEdges)
                        {
                            if (!inQueue[nextEdgeIdx] && !isBreaklineEdge[nextEdgeIdx] &&
                                IsEdgeExposedToVoid(graph.Edges[nextEdgeIdx], removed, out _))
                            {
                                exposedEdgeQueue.Enqueue(nextEdgeIdx);
                                inQueue[nextEdgeIdx] = true;
                            }
                        }
                    }
                }
            }

            return ApplyRemovedTrianglesToSurface(surface, graph, removed, midOrdDistance, ed);
        }

        private static int TrimTrianglesByCustomPolygons(
            CivSurface surface,
            List<Polygon2D> outerPolys,
            List<Polygon2D> hidePolys,
            double midOrdDistance,
            Editor ed)
        {
            TinTopologyGraph graph = BuildTinTopologyGraph(surface);
            if (graph.Triangles.Count == 0) return 0;

            bool[] removed = new bool[graph.Triangles.Count];
            for (int t = 0; t < graph.Triangles.Count; t++)
            {
                Point2d c = graph.Triangles[t].Centroid2D;
                bool insideOuter = false;
                for (int i = 0; i < outerPolys.Count; i++)
                {
                    if (outerPolys[i].Contains(c.X, c.Y))
                    {
                        insideOuter = true;
                        break;
                    }
                }

                if (!insideOuter)
                {
                    removed[t] = true;
                    continue;
                }

                for (int h = 0; h < hidePolys.Count; h++)
                {
                    if (hidePolys[h].Contains(c.X, c.Y))
                    {
                        removed[t] = true;
                        break;
                    }
                }
            }

            return ApplyRemovedTrianglesToSurface(surface, graph, removed, midOrdDistance, ed);
        }

        /// <summary>
        /// Thực thi xóa toàn bộ các tam giác đã đánh dấu removed[] trên 1 TIN Surface duy nhất:
        /// - Với tam giác ốc đảo 3 cạnh đều giáp tam giác giữ lại: dùng Hide Boundary thu nhỏ 1mm vào tâm tam giác.
        /// - Với toàn bộ tam giác biên / cầu nối còn lại: thu thập các cạnh mà cả 2 phía đều không có tam giác giữ lại
        ///   và gọi surface.DeleteLines(edgesToDelete) trong 1 lần duy nhất!
        /// </summary>
        private static int ApplyRemovedTrianglesToSurface(
            CivSurface surface,
            TinTopologyGraph graph,
            bool[] removed,
            double midOrdDistance,
            Editor ed)
        {
            int totalRemovedCount = 0;
            for (int t = 0; t < removed.Length; t++)
            {
                if (removed[t]) totalRemovedCount++;
            }

            if (totalRemovedCount == 0) return 0;

            // Kiểm tra xem có tam giác bị xóa nào bị bao vây cả 3 cạnh bởi tam giác giữ lại không (lỗ 1 tam giác cô lập)
            List<Point3dCollection> isolatedPocketHoles = new List<Point3dCollection>();
            for (int t = 0; t < graph.Triangles.Count; t++)
            {
                if (!removed[t]) continue;

                TinTriangleNode tri = graph.Triangles[t];
                bool e0HasKept = EdgeHasKeptTriangle(graph.Edges[tri.E0], removed);
                bool e1HasKept = EdgeHasKeptTriangle(graph.Edges[tri.E1], removed);
                bool e2HasKept = EdgeHasKeptTriangle(graph.Edges[tri.E2], removed);

                if (e0HasKept && e1HasKept && e2HasKept)
                {
                    Point3d p0 = graph.Vertices[tri.V0].Pos;
                    Point3d p1 = graph.Vertices[tri.V1].Pos;
                    Point3d p2 = graph.Vertices[tri.V2].Pos;
                    double cx = (p0.X + p1.X + p2.X) / 3.0;
                    double cy = (p0.Y + p1.Y + p2.Y) / 3.0;
                    double cz = (p0.Z + p1.Z + p2.Z) / 3.0;

                    // Thu nhẹ 0.5% về tâm để Hide Boundary chỉ ẩn đúng tam giác lỗ cô lập này
                    Point3d Shrink(Point3d p) => new Point3d(
                        cx + (p.X - cx) * 0.995,
                        cy + (p.Y - cy) * 0.995,
                        cz);

                    Point3d s0 = Shrink(p0);
                    Point3d s1 = Shrink(p1);
                    Point3d s2 = Shrink(p2);
                    var pts = new Point3dCollection { s0, s1, s2, s0 };
                    isolatedPocketHoles.Add(pts);
                }
            }

            if (isolatedPocketHoles.Count > 0)
            {
                foreach (Point3dCollection holePts in isolatedPocketHoles)
                {
                    try
                    {
                        surface.BoundariesDefinition.AddBoundaries(holePts, midOrdDistance, SurfaceBoundaryType.Hide, false);
                    }
                    catch { }
                }
                surface.Rebuild();

                // Xây dựng lại đồ thị sau Rebuild để con trỏ TinSurfaceEdge còn nguyên hiệu lực trước khi DeleteLines
                TinTopologyGraph refreshedGraph = BuildTinTopologyGraph(surface);
                bool[] refreshedRemoved = new bool[refreshedGraph.Triangles.Count];

                // Ánh xạ lại trạng thái giữ/xóa theo tọa độ tâm 2D của các tam giác giữ lại
                HashSet<QuantizedPoint2D> keptCentroids = new HashSet<QuantizedPoint2D>();
                for (int t = 0; t < graph.Triangles.Count; t++)
                {
                    if (!removed[t])
                    {
                        Point2d c = graph.Triangles[t].Centroid2D;
                        keptCentroids.Add(QuantizedPoint2D.From(c.X, c.Y));
                    }
                }

                for (int t = 0; t < refreshedGraph.Triangles.Count; t++)
                {
                    Point2d c = refreshedGraph.Triangles[t].Centroid2D;
                    if (!keptCentroids.Contains(QuantizedPoint2D.From(c.X, c.Y)))
                    {
                        refreshedRemoved[t] = true;
                    }
                }

                graph = refreshedGraph;
                removed = refreshedRemoved;
            }

            List<TinSurfaceEdge> edgesToDelete = new List<TinSurfaceEdge>();
            for (int eIdx = 0; eIdx < graph.Edges.Count; eIdx++)
            {
                TinEdgeNode edge = graph.Edges[eIdx];
                bool hasKept = EdgeHasKeptTriangle(edge, removed);
                bool hasRemoved = (edge.TriA >= 0 && removed[edge.TriA]) || (edge.TriB >= 0 && removed[edge.TriB]);

                if (!hasKept && hasRemoved && edge.RawEdge != null && edge.RawEdge.IsValid)
                {
                    edgesToDelete.Add(edge.RawEdge);
                }
            }

            if (edgesToDelete.Count > 0)
            {
                try
                {
                    surface.DeleteLines(edgesToDelete);
                }
                catch
                {
                    // Fallback an toàn từng cạnh nếu có cạnh tự động bị thu hồi kèm tam giác kế cận
                    foreach (TinSurfaceEdge rawEdge in edgesToDelete)
                    {
                        try
                        {
                            if (rawEdge != null && rawEdge.IsValid)
                            {
                                surface.DeleteLine(rawEdge);
                            }
                        }
                        catch { }
                    }
                }
            }

            return totalRemovedCount;
        }

        private static bool EdgeHasKeptTriangle(TinEdgeNode edge, bool[] removed)
        {
            if (edge.TriA >= 0 && !removed[edge.TriA]) return true;
            if (edge.TriB >= 0 && !removed[edge.TriB]) return true;
            return false;
        }

        private static bool IsEdgeExposedToVoid(TinEdgeNode edge, bool[] removed, out int liveTriIdx)
        {
            bool aAlive = edge.TriA >= 0 && !removed[edge.TriA];
            bool bAlive = edge.TriB >= 0 && !removed[edge.TriB];

            if (aAlive && !bAlive)
            {
                liveTriIdx = edge.TriA;
                return true;
            }
            if (bAlive && !aAlive)
            {
                liveTriIdx = edge.TriB;
                return true;
            }

            liveTriIdx = -1;
            return false;
        }

        #endregion

        #region TIN TOPOLOGY & SPATIAL INDEXING STRUCTURES

        private readonly struct QuantizedPoint2D : IEquatable<QuantizedPoint2D>
        {
            public readonly long QX;
            public readonly long QY;

            public QuantizedPoint2D(long qx, long qy)
            {
                QX = qx;
                QY = qy;
            }

            public static QuantizedPoint2D From(double x, double y)
            {
                return new QuantizedPoint2D((long)Math.Round(x * 1000.0), (long)Math.Round(y * 1000.0));
            }

            public bool Equals(QuantizedPoint2D other) => QX == other.QX && QY == other.QY;
            public override bool Equals(object? obj) => obj is QuantizedPoint2D other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(QX, QY);
        }

        private readonly struct UndirectedEdgeKey : IEquatable<UndirectedEdgeKey>
        {
            public readonly int U;
            public readonly int V;

            public UndirectedEdgeKey(int a, int b)
            {
                if (a <= b) { U = a; V = b; }
                else { U = b; V = a; }
            }

            public bool Equals(UndirectedEdgeKey other) => U == other.U && V == other.V;
            public override bool Equals(object? obj) => obj is UndirectedEdgeKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(U, V);
        }

        private sealed class TinVertexNode
        {
            public int Index;
            public Point3d Pos;
            public QuantizedPoint2D QKey;
        }

        private sealed class TinEdgeNode
        {
            public int U;
            public int V;
            public double Length2DSq;
            public bool IsLocked;
            public int TriA = -1;
            public int TriB = -1;
            public TinSurfaceEdge? RawEdge;
        }

        private sealed class TinTriangleNode
        {
            public int V0, V1, V2;
            public int E0, E1, E2;
            public Point2d Centroid2D;
        }

        private sealed class TinTopologyGraph
        {
            public List<TinVertexNode> Vertices = new List<TinVertexNode>();
            public List<TinEdgeNode> Edges = new List<TinEdgeNode>();
            public List<TinTriangleNode> Triangles = new List<TinTriangleNode>();
        }

        private static TinTopologyGraph BuildTinTopologyGraph(CivSurface surface)
        {
            var graph = new TinTopologyGraph();
            var vertexLookup = new Dictionary<QuantizedPoint2D, int>();
            var edgeLookup = new Dictionary<UndirectedEdgeKey, int>();

            int GetOrAddVertex(Point3d pt)
            {
                QuantizedPoint2D qk = QuantizedPoint2D.From(pt.X, pt.Y);
                if (vertexLookup.TryGetValue(qk, out int idx))
                    return idx;

                idx = graph.Vertices.Count;
                graph.Vertices.Add(new TinVertexNode { Index = idx, Pos = pt, QKey = qk });
                vertexLookup[qk] = idx;
                return idx;
            }

            int RegisterTriangleEdge(TinSurfaceEdge? rawEdge, int fallbackU, int fallbackV, int triIndex)
            {
                int u = fallbackU;
                int v = fallbackV;
                bool locked = false;

                if (rawEdge != null && rawEdge.IsValid)
                {
                    try
                    {
                        u = GetOrAddVertex(rawEdge.Vertex1.Location);
                        v = GetOrAddVertex(rawEdge.Vertex2.Location);
                        locked = rawEdge.IsLocked;
                    }
                    catch { }
                }

                var key = new UndirectedEdgeKey(u, v);
                if (!edgeLookup.TryGetValue(key, out int eIdx))
                {
                    Point3d pu = graph.Vertices[key.U].Pos;
                    Point3d pv = graph.Vertices[key.V].Pos;
                    double dx = pu.X - pv.X;
                    double dy = pu.Y - pv.Y;

                    eIdx = graph.Edges.Count;
                    graph.Edges.Add(new TinEdgeNode
                    {
                        U = key.U,
                        V = key.V,
                        Length2DSq = dx * dx + dy * dy,
                        IsLocked = locked,
                        TriA = triIndex,
                        TriB = -1,
                        RawEdge = rawEdge
                    });
                    edgeLookup[key] = eIdx;
                }
                else
                {
                    TinEdgeNode existing = graph.Edges[eIdx];
                    if (locked) existing.IsLocked = true;
                    if (existing.RawEdge == null && rawEdge != null) existing.RawEdge = rawEdge;
                    if (existing.TriA != triIndex && existing.TriB == -1)
                    {
                        existing.TriB = triIndex;
                    }
                }

                return eIdx;
            }

            TinSurfaceTriangleCollection tris = surface.GetTriangles(false);
            foreach (TinSurfaceTriangle tri in tris)
            {
                if (tri == null || !tri.IsVisible) continue;

                Point3d p0, p1, p2;
                try
                {
                    p0 = tri.Vertex1.Location;
                    p1 = tri.Vertex2.Location;
                    p2 = tri.Vertex3.Location;
                }
                catch
                {
                    continue;
                }

                int v0 = GetOrAddVertex(p0);
                int v1 = GetOrAddVertex(p1);
                int v2 = GetOrAddVertex(p2);
                if (v0 == v1 || v1 == v2 || v2 == v0) continue;

                int tIdx = graph.Triangles.Count;
                TinSurfaceEdge? re0 = null, re1 = null, re2 = null;
                try { re0 = tri.Edge1; } catch { }
                try { re1 = tri.Edge2; } catch { }
                try { re2 = tri.Edge3; } catch { }

                int e0 = RegisterTriangleEdge(re0, v0, v1, tIdx);
                int e1 = RegisterTriangleEdge(re1, v1, v2, tIdx);
                int e2 = RegisterTriangleEdge(re2, v2, v0, tIdx);

                graph.Triangles.Add(new TinTriangleNode
                {
                    V0 = v0,
                    V1 = v1,
                    V2 = v2,
                    E0 = e0,
                    E1 = e1,
                    E2 = e2,
                    Centroid2D = new Point2d((p0.X + p1.X + p2.X) / 3.0, (p0.Y + p1.Y + p2.Y) / 3.0)
                });
            }

            return graph;
        }

        /// <summary>
        /// Bảng ánh xạ tốc độ cao O(1) bằng lưới ô vuông 2D (Uniform Spatial Grid):
        /// gắn mỗi đỉnh TIN vào các đường FeatureLine/Polyline đi qua nó, đánh thứ tự rank dọc theo lý trình từng đường.
        /// </summary>
        private sealed class CurveVertexAttachmentMap
        {
            private readonly struct VertexCurveEntry
            {
                public readonly int CurveIndex;
                public readonly int OrderRank;
                public readonly double StationAlongCurve;

                public VertexCurveEntry(int curveIndex, int orderRank, double station)
                {
                    CurveIndex = curveIndex;
                    OrderRank = orderRank;
                    StationAlongCurve = station;
                }
            }

            private readonly List<VertexCurveEntry>[] _entriesByVertex;
            private readonly List<SourceCurveInfo> _curves;
            private readonly SpatialSegmentGrid _segmentGrid;
            private readonly double _segMatchTol;

            public CurveVertexAttachmentMap(
                List<SourceCurveInfo> curves,
                List<TinVertexNode> vertices,
                SpatialSegmentGrid segmentGrid,
                double segMatchTol)
            {
                _curves = curves;
                _segmentGrid = segmentGrid;
                _segMatchTol = segMatchTol;
                _entriesByVertex = new List<VertexCurveEntry>[vertices.Count];
            }

            public void AddAttachment(int vertexIdx, int curveIdx, int rank, double station)
            {
                _entriesByVertex[vertexIdx] ??= new List<VertexCurveEntry>(2);
                _entriesByVertex[vertexIdx].Add(new VertexCurveEntry(curveIdx, rank, station));
            }

            public bool IsConsecutiveCurveSegment(int u, int v, List<TinVertexNode> vertices)
            {
                var listU = _entriesByVertex[u];
                var listV = _entriesByVertex[v];
                if (listU != null && listV != null)
                {
                    for (int i = 0; i < listU.Count; i++)
                    {
                        var eu = listU[i];
                        for (int j = 0; j < listV.Count; j++)
                        {
                            var ev = listV[j];
                            if (eu.CurveIndex == ev.CurveIndex)
                            {
                                int diff = Math.Abs(eu.OrderRank - ev.OrderRank);
                                if (diff == 1)
                                    return true;

                                // Nếu đường khép kín, đỉnh đầu (rank 0) và đỉnh cuối nối liền nhau
                                SourceCurveInfo c = _curves[eu.CurveIndex];
                                if (c.IsClosed && diff == c.AttachedTinVertexCount - 1 && c.AttachedTinVertexCount >= 3)
                                    return true;
                            }
                        }
                    }
                }

                // Kiểm tra bổ sung: trung điểm cạnh nằm sát đoạn nguồn (phòng trường hợp giao cắt chèn đỉnh)
                Point3d pu = vertices[u].Pos;
                Point3d pv = vertices[v].Pos;
                double mx = 0.5 * (pu.X + pv.X);
                double my = 0.5 * (pu.Y + pv.Y);
                return _segmentGrid.IsSegmentCollinearWithAnySource(pu.X, pu.Y, pv.X, pv.Y, mx, my, _segMatchTol);
            }

            public bool IsShortcutChordOnSameCurve(int u, int v, double chordLength, HashSet<int> curvesInClosedLoops)
            {
                if (chordLength <= 1e-5) return false;

                var listU = _entriesByVertex[u];
                var listV = _entriesByVertex[v];
                if (listU == null || listV == null) return false;

                for (int i = 0; i < listU.Count; i++)
                {
                    var eu = listU[i];
                    for (int j = 0; j < listV.Count; j++)
                    {
                        var ev = listV[j];
                        if (eu.CurveIndex != ev.CurveIndex) continue;

                        int rankDiff = Math.Abs(eu.OrderRank - ev.OrderRank);
                        if (rankDiff < 2) continue;

                        SourceCurveInfo curve = _curves[eu.CurveIndex];
                        double alongDist = Math.Abs(eu.StationAlongCurve - ev.StationAlongCurve);
                        if (curve.IsClosed && curve.TotalLength2D > 0)
                        {
                            alongDist = Math.Min(alongDist, Math.Max(0.0, curve.TotalLength2D - alongDist));
                        }

                        // Tỷ số chiều dài cung / dây cung:
                        // Mọi cung cong đường bộ kể cả góc quay 180 độ đều có tỷ số <= pi/2 (~1.57) <= 2.4.
                        // Nếu tỷ số > 2.4 trên đường hở thì đó là miệng chữ U sâu, giữ lại để không ăn lẹm lòng chữ U!
                        double ratio = alongDist / chordLength;
                        if (ratio <= 2.45)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
        }

        private static CurveVertexAttachmentMap BuildCurveVertexAttachmentMap(
            List<SourceCurveInfo> sourceCurves,
            List<TinVertexNode> vertices,
            double matchTol2D,
            double matchTolZ)
        {
            var grid = new SpatialSegmentGrid(sourceCurves, matchTol2D);
            var map = new CurveVertexAttachmentMap(sourceCurves, vertices, grid, matchTol2D);

            double tol2DSq = matchTol2D * matchTol2D;
            var perCurveHits = new List<(int VertexIdx, double Station)>[sourceCurves.Count];
            for (int c = 0; c < sourceCurves.Count; c++)
            {
                perCurveHits[c] = new List<(int VertexIdx, double Station)>();
            }

            for (int vIdx = 0; vIdx < vertices.Count; vIdx++)
            {
                Point3d vp = vertices[vIdx].Pos;
                grid.FindMatchingCurvesForPoint(vp.X, vp.Y, vp.Z, tol2DSq, matchTolZ, perCurveHits, vIdx);
            }

            for (int c = 0; c < sourceCurves.Count; c++)
            {
                var hits = perCurveHits[c];
                if (hits.Count == 0) continue;

                hits.Sort((a, b) => a.Station.CompareTo(b.Station));
                sourceCurves[c].AttachedTinVertexCount = hits.Count;

                for (int rank = 0; rank < hits.Count; rank++)
                {
                    map.AddAttachment(hits[rank].VertexIdx, c, rank, hits[rank].Station);
                }
            }

            return map;
        }

        private sealed class SpatialSegmentGrid
        {
            private readonly struct SegEntry
            {
                public readonly int CurveIdx;
                public readonly double X0, Y0, Z0, X1, Y1, Z1;
                public readonly double StartStation;
                public readonly double SegLen;

                public SegEntry(int curveIdx, double x0, double y0, double z0, double x1, double y1, double z1, double startStation, double segLen)
                {
                    CurveIdx = curveIdx;
                    X0 = x0; Y0 = y0; Z0 = z0;
                    X1 = x1; Y1 = y1; Z1 = z1;
                    StartStation = startStation;
                    SegLen = segLen;
                }
            }

            private readonly Dictionary<(int GX, int GY), List<int>> _cells = new Dictionary<(int GX, int GY), List<int>>();
            private readonly List<SegEntry> _segments = new List<SegEntry>();
            private readonly double _cellSize;
            private readonly int _curveCount;

            public SpatialSegmentGrid(List<SourceCurveInfo> curves, double pad)
            {
                _curveCount = curves.Count;
                double totalLen = 0.0;
                int segCount = 0;

                for (int c = 0; c < curves.Count; c++)
                {
                    var pts = curves[c].Points3D;
                    for (int i = 0; i < pts.Count - 1; i++)
                    {
                        double dx = pts[i + 1].X - pts[i].X;
                        double dy = pts[i + 1].Y - pts[i].Y;
                        double len = Math.Sqrt(dx * dx + dy * dy);
                        if (len > 1e-6)
                        {
                            totalLen += len;
                            segCount++;
                        }
                    }
                }

                _cellSize = segCount > 0 ? Math.Max(2.0, (totalLen / segCount) * 2.5) : 10.0;

                for (int c = 0; c < curves.Count; c++)
                {
                    var pts = curves[c].Points3D;
                    double sta = 0.0;
                    for (int i = 0; i < pts.Count - 1; i++)
                    {
                        double x0 = pts[i].X, y0 = pts[i].Y, z0 = pts[i].Z;
                        double x1 = pts[i + 1].X, y1 = pts[i + 1].Y, z1 = pts[i + 1].Z;
                        double dx = x1 - x0, dy = y1 - y0;
                        double len = Math.Sqrt(dx * dx + dy * dy);
                        if (len <= 1e-6) continue;

                        int sIdx = _segments.Count;
                        _segments.Add(new SegEntry(c, x0, y0, z0, x1, y1, z1, sta, len));
                        sta += len;

                        int minGX = (int)Math.Floor((Math.Min(x0, x1) - pad) / _cellSize);
                        int maxGX = (int)Math.Floor((Math.Max(x0, x1) + pad) / _cellSize);
                        int minGY = (int)Math.Floor((Math.Min(y0, y1) - pad) / _cellSize);
                        int maxGY = (int)Math.Floor((Math.Max(y0, y1) + pad) / _cellSize);

                        for (int gx = minGX; gx <= maxGX; gx++)
                        {
                            for (int gy = minGY; gy <= maxGY; gy++)
                            {
                                var key = (gx, gy);
                                if (!_cells.TryGetValue(key, out var bucket))
                                {
                                    bucket = new List<int>(4);
                                    _cells[key] = bucket;
                                }
                                bucket.Add(sIdx);
                            }
                        }
                    }
                    curves[c].TotalLength2D = sta;
                }
            }

            public void FindMatchingCurvesForPoint(
                double px,
                double py,
                double pz,
                double tol2DSq,
                double tolZ,
                List<(int VertexIdx, double Station)>[] perCurveHits,
                int vertexIdx)
            {
                int gx = (int)Math.Floor(px / _cellSize);
                int gy = (int)Math.Floor(py / _cellSize);
                if (!_cells.TryGetValue((gx, gy), out var bucket)) return;

                Dictionary<int, (double BestDistSq, double BestDz, double BestSta)>? bestPerCurve = null;
                double minOverallDistSq = double.MaxValue;

                for (int i = 0; i < bucket.Count; i++)
                {
                    SegEntry seg = _segments[bucket[i]];
                    double distSq = PointToSegmentDistSq(px, py, seg.X0, seg.Y0, seg.X1, seg.Y1, out double tProj);
                    if (distSq <= tol2DSq)
                    {
                        double interpZ = seg.Z0 + tProj * (seg.Z1 - seg.Z0);
                        double dz = Math.Abs(pz - interpZ);
                        if (dz > tolZ) continue;

                        double sta = seg.StartStation + tProj * seg.SegLen;
                        bestPerCurve ??= new Dictionary<int, (double, double, double)>(2);
                        if (!bestPerCurve.TryGetValue(seg.CurveIdx, out var prev) || distSq < prev.BestDistSq)
                        {
                            bestPerCurve[seg.CurveIdx] = (distSq, dz, sta);
                        }
                        if (distSq < minOverallDistSq)
                        {
                            minOverallDistSq = distSq;
                        }
                    }
                }

                if (bestPerCurve != null)
                {
                    foreach (var kvp in bestPerCurve)
                    {
                        // Nếu đỉnh nằm trùng sát (< 2mm) trên 1 đường nguồn, chỉ gắn thêm đường nguồn thứ 2 nếu đường thứ 2 cũng đi sát đỉnh (< 5mm)
                        if (minOverallDistSq < 4e-6 && kvp.Value.BestDistSq > 2.5e-5)
                            continue;

                        perCurveHits[kvp.Key].Add((vertexIdx, kvp.Value.BestSta));
                    }
                }
            }

            public bool IsSegmentCollinearWithAnySource(
                double ux, double uy,
                double vx, double vy,
                double mx, double my,
                double tol)
            {
                double tolSq = tol * tol;
                int gx = (int)Math.Floor(mx / _cellSize);
                int gy = (int)Math.Floor(my / _cellSize);
                if (!_cells.TryGetValue((gx, gy), out var bucket)) return false;

                for (int i = 0; i < bucket.Count; i++)
                {
                    SegEntry seg = _segments[bucket[i]];
                    if (PointToSegmentDistSq(mx, my, seg.X0, seg.Y0, seg.X1, seg.Y1, out _) <= tolSq &&
                        PointToSegmentDistSq(ux, uy, seg.X0, seg.Y0, seg.X1, seg.Y1, out _) <= tolSq &&
                        PointToSegmentDistSq(vx, vy, seg.X0, seg.Y0, seg.X1, seg.Y1, out _) <= tolSq)
                    {
                        return true;
                    }
                }
                return false;
            }

            private static double PointToSegmentDistSq(
                double px, double py,
                double x0, double y0,
                double x1, double y1,
                out double t)
            {
                double dx = x1 - x0;
                double dy = y1 - y0;
                double lenSq = dx * dx + dy * dy;
                if (lenSq <= 1e-12)
                {
                    t = 0.0;
                    double ex = px - x0, ey = py - y0;
                    return ex * ex + ey * ey;
                }

                t = ((px - x0) * dx + (py - y0) * dy) / lenSq;
                if (t < 0.0) t = 0.0;
                else if (t > 1.0) t = 1.0;

                double projX = x0 + t * dx;
                double projY = y0 + t * dy;
                double rx = px - projX;
                double ry = py - projY;
                return rx * rx + ry * ry;
            }
        }

        private readonly struct TriangleVertexKey : IEquatable<TriangleVertexKey>
        {
            public readonly QuantizedPoint2D A;
            public readonly QuantizedPoint2D B;
            public readonly QuantizedPoint2D C;

            public TriangleVertexKey(QuantizedPoint2D a, QuantizedPoint2D b, QuantizedPoint2D c)
            {
                // Sắp xếp tăng dần từ điển để không phụ thuộc thứ tự đỉnh (CW / CCW)
                if (Compare(a, b) > 0) (a, b) = (b, a);
                if (Compare(b, c) > 0) (b, c) = (c, b);
                if (Compare(a, b) > 0) (a, b) = (b, a);
                A = a;
                B = b;
                C = c;
            }

            private static int Compare(QuantizedPoint2D p1, QuantizedPoint2D p2)
            {
                int cmp = p1.QX.CompareTo(p2.QX);
                return cmp != 0 ? cmp : p1.QY.CompareTo(p2.QY);
            }

            public static TriangleVertexKey From(TinTopologyGraph graph, TinTriangleNode tri)
            {
                return new TriangleVertexKey(
                    graph.Vertices[tri.V0].QKey,
                    graph.Vertices[tri.V1].QKey,
                    graph.Vertices[tri.V2].QKey);
            }

            public bool Equals(TriangleVertexKey other) => A.Equals(other.A) && B.Equals(other.B) && C.Equals(other.C);
            public override bool Equals(object? obj) => obj is TriangleVertexKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(A, B, C);
        }

        private static HashSet<TriangleVertexKey> BuildTriangleVertexKeySet(TinTopologyGraph graph)
        {
            var set = new HashSet<TriangleVertexKey>(graph.Triangles.Count);
            for (int t = 0; t < graph.Triangles.Count; t++)
            {
                set.Add(TriangleVertexKey.From(graph, graph.Triangles[t]));
            }
            return set;
        }

        /// <summary>
        /// Lưới ô vuông 2D tra cứu tam giác tốc độ O(1) cho mặt phẳng lớn (40,000+ tam giác),
        /// hỗ trợ kiểm tra điểm thuộc vùng và nội suy phẳng cao độ Z chính xác từng milimét.
        /// </summary>
        private sealed class SpatialTriangleGrid2D
        {
            private readonly struct TriRecord
            {
                public readonly double Ax, Ay, Az;
                public readonly double Bx, By, Bz;
                public readonly double Cx, Cy, Cz;
                public readonly double MinX, MinY, MaxX, MaxY;
                public readonly double LenAB, LenBC, LenCA;
                public readonly double OrientSign;
                public readonly double InvAbsTwiceArea;

                public TriRecord(Point3d a, Point3d b, Point3d c, double sign, double absTwiceArea)
                {
                    Ax = a.X; Ay = a.Y; Az = a.Z;
                    Bx = b.X; By = b.Y; Bz = b.Z;
                    Cx = c.X; Cy = c.Y; Cz = c.Z;
                    MinX = Math.Min(Ax, Math.Min(Bx, Cx));
                    MinY = Math.Min(Ay, Math.Min(By, Cy));
                    MaxX = Math.Max(Ax, Math.Max(Bx, Cx));
                    MaxY = Math.Max(Ay, Math.Max(By, Cy));
                    LenAB = Math.Sqrt((Bx - Ax) * (Bx - Ax) + (By - Ay) * (By - Ay));
                    LenBC = Math.Sqrt((Cx - Bx) * (Cx - Bx) + (Cy - By) * (Cy - By));
                    LenCA = Math.Sqrt((Ax - Cx) * (Ax - Cx) + (Ay - Cy) * (Ay - Cy));
                    OrientSign = sign;
                    InvAbsTwiceArea = 1.0 / absTwiceArea;
                }
            }

            private readonly List<TriRecord> _tris;
            private readonly Dictionary<(int GX, int GY), List<int>> _cells = new Dictionary<(int GX, int GY), List<int>>();
            private readonly double _cellSize;
            public readonly double MinX, MinY, MaxX, MaxY;

            public SpatialTriangleGrid2D(TinTopologyGraph graph)
            {
                _tris = new List<TriRecord>(graph.Triangles.Count);
                double gMinX = double.MaxValue, gMinY = double.MaxValue;
                double gMaxX = double.MinValue, gMaxY = double.MinValue;
                double sumSpan = 0.0;

                for (int t = 0; t < graph.Triangles.Count; t++)
                {
                    TinTriangleNode node = graph.Triangles[t];
                    Point3d a = graph.Vertices[node.V0].Pos;
                    Point3d b = graph.Vertices[node.V1].Pos;
                    Point3d c = graph.Vertices[node.V2].Pos;

                    double twiceArea = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
                    double absTwiceArea = Math.Abs(twiceArea);
                    if (absTwiceArea <= 1e-10) continue;

                    var rec = new TriRecord(a, b, c, twiceArea >= 0.0 ? 1.0 : -1.0, absTwiceArea);
                    _tris.Add(rec);

                    if (rec.MinX < gMinX) gMinX = rec.MinX;
                    if (rec.MinY < gMinY) gMinY = rec.MinY;
                    if (rec.MaxX > gMaxX) gMaxX = rec.MaxX;
                    if (rec.MaxY > gMaxY) gMaxY = rec.MaxY;
                    sumSpan += (rec.MaxX - rec.MinX) + (rec.MaxY - rec.MinY);
                }

                if (_tris.Count == 0)
                {
                    MinX = MinY = 0.0;
                    MaxX = MaxY = -1.0;
                    _cellSize = 10.0;
                    return;
                }

                MinX = gMinX; MinY = gMinY;
                MaxX = gMaxX; MaxY = gMaxY;
                _cellSize = Math.Max(2.0, (sumSpan / (2.0 * _tris.Count)) * 2.0);

                const double pad = 0.05;
                for (int idx = 0; idx < _tris.Count; idx++)
                {
                    TriRecord rec = _tris[idx];
                    int x0 = (int)Math.Floor((rec.MinX - pad) / _cellSize);
                    int x1 = (int)Math.Floor((rec.MaxX + pad) / _cellSize);
                    int y0 = (int)Math.Floor((rec.MinY - pad) / _cellSize);
                    int y1 = (int)Math.Floor((rec.MaxY + pad) / _cellSize);

                    for (int gx = x0; gx <= x1; gx++)
                    {
                        for (int gy = y0; gy <= y1; gy++)
                        {
                            var key = (gx, gy);
                            if (!_cells.TryGetValue(key, out var bucket))
                            {
                                bucket = new List<int>(4);
                                _cells[key] = bucket;
                            }
                            bucket.Add(idx);
                        }
                    }
                }
            }

            /// <summary>
            /// Kiểm tra điểm (px, py) có nằm trong hợp các tam giác không.
            /// edgeDistTol > 0: nới nhẹ ra ngoài biên; edgeDistTol &lt; 0: yêu cầu nằm sâu bên trong cạnh tam giác.
            /// </summary>
            public bool ContainsPoint(double px, double py, double edgeDistTol)
            {
                if (_tris.Count == 0) return false;
                double boxPad = Math.Max(0.0, edgeDistTol);
                if (px < MinX - boxPad || px > MaxX + boxPad || py < MinY - boxPad || py > MaxY + boxPad)
                    return false;

                int gx = (int)Math.Floor(px / _cellSize);
                int gy = (int)Math.Floor(py / _cellSize);
                if (!_cells.TryGetValue((gx, gy), out var bucket))
                    return false;

                for (int i = 0; i < bucket.Count; i++)
                {
                    TriRecord tri = _tris[bucket[i]];
                    if (px < tri.MinX - boxPad || px > tri.MaxX + boxPad ||
                        py < tri.MinY - boxPad || py > tri.MaxY + boxPad)
                        continue;

                    double c0 = ((tri.Bx - tri.Ax) * (py - tri.Ay) - (tri.By - tri.Ay) * (px - tri.Ax)) * tri.OrientSign;
                    if (c0 < -edgeDistTol * tri.LenAB) continue;

                    double c1 = ((tri.Cx - tri.Bx) * (py - tri.By) - (tri.Cy - tri.By) * (px - tri.Bx)) * tri.OrientSign;
                    if (c1 < -edgeDistTol * tri.LenBC) continue;

                    double c2 = ((tri.Ax - tri.Cx) * (py - tri.Cy) - (tri.Ay - tri.Cy) * (px - tri.Cx)) * tri.OrientSign;
                    if (c2 < -edgeDistTol * tri.LenCA) continue;

                    return true;
                }

                return false;
            }

            public bool TryInterpolateZ(double px, double py, out double z, double edgeDistTol)
            {
                z = 0.0;
                if (_tris.Count == 0) return false;
                double boxPad = Math.Max(0.0, edgeDistTol);
                if (px < MinX - boxPad || px > MaxX + boxPad || py < MinY - boxPad || py > MaxY + boxPad)
                    return false;

                int gx = (int)Math.Floor(px / _cellSize);
                int gy = (int)Math.Floor(py / _cellSize);
                if (!_cells.TryGetValue((gx, gy), out var bucket))
                    return false;

                for (int i = 0; i < bucket.Count; i++)
                {
                    TriRecord tri = _tris[bucket[i]];
                    if (px < tri.MinX - boxPad || px > tri.MaxX + boxPad ||
                        py < tri.MinY - boxPad || py > tri.MaxY + boxPad)
                        continue;

                    double c0 = ((tri.Bx - tri.Ax) * (py - tri.Ay) - (tri.By - tri.Ay) * (px - tri.Ax)) * tri.OrientSign;
                    if (c0 < -edgeDistTol * tri.LenAB) continue;

                    double c1 = ((tri.Cx - tri.Bx) * (py - tri.By) - (tri.Cy - tri.By) * (px - tri.Bx)) * tri.OrientSign;
                    if (c1 < -edgeDistTol * tri.LenBC) continue;

                    double c2 = ((tri.Ax - tri.Cx) * (py - tri.Cy) - (tri.Ay - tri.Cy) * (px - tri.Cx)) * tri.OrientSign;
                    if (c2 < -edgeDistTol * tri.LenCA) continue;

                    double wA = c1 * tri.InvAbsTwiceArea;
                    double wB = c2 * tri.InvAbsTwiceArea;
                    double wC = c0 * tri.InvAbsTwiceArea;
                    z = wA * tri.Az + wB * tri.Bz + wC * tri.Cz;
                    return true;
                }

                return false;
            }
        }

        private sealed class SpatialBorderSegmentGrid2D
        {
            private readonly List<(double X0, double Y0, double X1, double Y1)> _segs = new List<(double, double, double, double)>();
            private readonly Dictionary<(int GX, int GY), List<int>> _cells = new Dictionary<(int GX, int GY), List<int>>();
            private readonly double _cellSize;

            public SpatialBorderSegmentGrid2D(List<(Point3d A, Point3d B)> borderSegments, double maxQueryRadius = 0.15)
            {
                double sumLen = 0.0;
                for (int i = 0; i < borderSegments.Count; i++)
                {
                    var (a, b) = borderSegments[i];
                    double len = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
                    if (len <= 1e-6) continue;
                    _segs.Add((a.X, a.Y, b.X, b.Y));
                    sumLen += len;
                }

                _cellSize = _segs.Count > 0 ? Math.Max(1.0, (sumLen / _segs.Count) * 2.0) : 5.0;

                for (int idx = 0; idx < _segs.Count; idx++)
                {
                    var s = _segs[idx];
                    int x0 = (int)Math.Floor((Math.Min(s.X0, s.X1) - maxQueryRadius) / _cellSize);
                    int x1 = (int)Math.Floor((Math.Max(s.X0, s.X1) + maxQueryRadius) / _cellSize);
                    int y0 = (int)Math.Floor((Math.Min(s.Y0, s.Y1) - maxQueryRadius) / _cellSize);
                    int y1 = (int)Math.Floor((Math.Max(s.Y0, s.Y1) + maxQueryRadius) / _cellSize);

                    for (int gx = x0; gx <= x1; gx++)
                    {
                        for (int gy = y0; gy <= y1; gy++)
                        {
                            var key = (gx, gy);
                            if (!_cells.TryGetValue(key, out var bucket))
                            {
                                bucket = new List<int>(4);
                                _cells[key] = bucket;
                            }
                            bucket.Add(idx);
                        }
                    }
                }
            }

            public bool IsNearAnySegment(double px, double py, double tol)
            {
                if (_segs.Count == 0) return false;
                int gx = (int)Math.Floor(px / _cellSize);
                int gy = (int)Math.Floor(py / _cellSize);
                if (!_cells.TryGetValue((gx, gy), out var bucket))
                    return false;

                double tolSq = tol * tol;
                for (int i = 0; i < bucket.Count; i++)
                {
                    var s = _segs[bucket[i]];
                    double dx = s.X1 - s.X0;
                    double dy = s.Y1 - s.Y0;
                    double lenSq = dx * dx + dy * dy;
                    double t = lenSq > 1e-12 ? ((px - s.X0) * dx + (py - s.Y0) * dy) / lenSq : 0.0;
                    if (t < 0.0) t = 0.0;
                    else if (t > 1.0) t = 1.0;

                    double rx = px - (s.X0 + t * dx);
                    double ry = py - (s.Y0 + t * dy);
                    if (rx * rx + ry * ry <= tolSq)
                        return true;
                }
                return false;
            }
        }

        private static SpatialBorderSegmentGrid2D BuildBorderSegmentGrid(TinTopologyGraph graph)
        {
            var borderSegs = new List<(Point3d A, Point3d B)>();
            for (int eIdx = 0; eIdx < graph.Edges.Count; eIdx++)
            {
                TinEdgeNode edge = graph.Edges[eIdx];
                if ((edge.TriA >= 0) != (edge.TriB >= 0))
                {
                    borderSegs.Add((graph.Vertices[edge.U].Pos, graph.Vertices[edge.V].Pos));
                }
            }
            return new SpatialBorderSegmentGrid2D(borderSegs);
        }

        private sealed class SpatialVertexLookup2D
        {
            private readonly Dictionary<(int GX, int GY), List<Point3d>> _cells = new Dictionary<(int GX, int GY), List<Point3d>>();
            private const double CellSize = 1.0;

            public SpatialVertexLookup2D(List<TinVertexNode> vertices)
            {
                for (int i = 0; i < vertices.Count; i++)
                {
                    Point3d p = vertices[i].Pos;
                    int gx = (int)Math.Floor(p.X / CellSize);
                    int gy = (int)Math.Floor(p.Y / CellSize);
                    var key = (gx, gy);
                    if (!_cells.TryGetValue(key, out var list))
                    {
                        list = new List<Point3d>(2);
                        _cells[key] = list;
                    }
                    list.Add(p);
                }
            }

            public bool TryFindNearestZ(double x, double y, double tol2D, out double nearestZ)
            {
                nearestZ = 0.0;
                double bestDistSq = tol2D * tol2D;
                bool found = false;

                int gx0 = (int)Math.Floor((x - tol2D) / CellSize);
                int gx1 = (int)Math.Floor((x + tol2D) / CellSize);
                int gy0 = (int)Math.Floor((y - tol2D) / CellSize);
                int gy1 = (int)Math.Floor((y + tol2D) / CellSize);

                for (int gx = gx0; gx <= gx1; gx++)
                {
                    for (int gy = gy0; gy <= gy1; gy++)
                    {
                        if (!_cells.TryGetValue((gx, gy), out var list)) continue;
                        for (int i = 0; i < list.Count; i++)
                        {
                            Point3d p = list[i];
                            double dSq = (p.X - x) * (p.X - x) + (p.Y - y) * (p.Y - y);
                            if (dSq <= bestDistSq)
                            {
                                bestDistSq = dSq;
                                nearestZ = p.Z;
                                found = true;
                            }
                        }
                    }
                }
                return found;
            }
        }

        #endregion

        #region POLYGON & CURVE EXTRACTION HELPERS

        internal sealed class SourceCurveInfo
        {
            public int CurveIndex;
            public ObjectId Id;
            public string EntityTypeName = string.Empty;
            public string DisplayName = string.Empty;
            public List<Point3d> Points3D = new List<Point3d>();
            public bool IsClosed;
            public bool WasAutoClosedGap;
            public bool HasTrueArcs;
            public double TotalLength2D;
            public int AttachedTinVertexCount;
        }

        internal sealed class Polygon2D
        {
            public readonly List<Point3d> Vertices3D;
            public readonly double MinX, MinY, MaxX, MaxY;
            public readonly double SignedArea;
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

        private static void BuildClosedAndChainedRegions(
            List<SourceCurveInfo> sourceCurves,
            double closeTol,
            bool treatNestedClosedAsHole,
            out List<Polygon2D> outerRegions,
            out List<Polygon2D> holeRegions,
            out HashSet<int> curvesInClosedLoops)
        {
            outerRegions = new List<Polygon2D>();
            holeRegions = new List<Polygon2D>();
            curvesInClosedLoops = new HashSet<int>();

            List<Polygon2D> rawPolys = new List<Polygon2D>();

            // 1. Đường vốn đã khép kín
            for (int i = 0; i < sourceCurves.Count; i++)
            {
                SourceCurveInfo sc = sourceCurves[i];
                if (sc.IsClosed && sc.Points3D.Count >= 3)
                {
                    var poly = new Polygon2D(sc.Points3D);
                    if (poly.Vertices3D.Count >= 3 && poly.AbsArea > 1e-3)
                    {
                        rawPolys.Add(poly);
                        curvesInClosedLoops.Add(i);
                    }
                }
            }

            // 2. Ghép các đường hở nối tiếp đầu-cuối thành vòng khép kín đơn (bậc 2 tại mỗi nút nối)
            List<int> openIndices = new List<int>();
            for (int i = 0; i < sourceCurves.Count; i++)
            {
                if (!curvesInClosedLoops.Contains(i) && sourceCurves[i].Points3D.Count >= 2)
                    openIndices.Add(i);
            }

            if (openIndices.Count >= 2)
            {
                ChainSimpleClosedCyclesFromOpenCurves(sourceCurves, openIndices, Math.Max(0.02, closeTol), rawPolys, curvesInClosedLoops);
            }

            if (rawPolys.Count == 0) return;

            if (!treatNestedClosedAsHole)
            {
                // Giữ tất cả vùng bao ngoài không lọt bên trong vùng khác; đường kín bên trong đóng vai trò Breakline đồng mức
                for (int i = 0; i < rawPolys.Count; i++)
                {
                    Point3d testPt = rawPolys[i].Vertices3D[0];
                    bool containedInLarger = false;
                    for (int j = 0; j < rawPolys.Count; j++)
                    {
                        if (i == j) continue;
                        if (rawPolys[j].AbsArea > rawPolys[i].AbsArea && rawPolys[j].Contains(testPt.X, testPt.Y))
                        {
                            containedInLarger = true;
                            break;
                        }
                    }
                    if (!containedInLarger)
                    {
                        outerRegions.Add(rawPolys[i]);
                    }
                }
            }
            else
            {
                // Phân bậc chẵn/lẻ: bậc chẵn = Outer, bậc lẻ = Hole (Hide)
                for (int i = 0; i < rawPolys.Count; i++)
                {
                    Point3d testPt = rawPolys[i].Vertices3D[0];
                    int depth = 0;
                    for (int j = 0; j < rawPolys.Count; j++)
                    {
                        if (i == j) continue;
                        if (rawPolys[j].AbsArea > rawPolys[i].AbsArea && rawPolys[j].Contains(testPt.X, testPt.Y))
                        {
                            depth++;
                        }
                    }

                    if (depth % 2 == 0) outerRegions.Add(rawPolys[i]);
                    else holeRegions.Add(rawPolys[i]);
                }
            }
        }

        private static void ChainSimpleClosedCyclesFromOpenCurves(
            List<SourceCurveInfo> curves,
            List<int> openIndices,
            double snapTol,
            List<Polygon2D> outPolys,
            HashSet<int> curvesInClosedLoops)
        {
            double tolSq = snapTol * snapTol;
            List<Point2d> nodes = new List<Point2d>();

            int GetNodeId(Point3d pt)
            {
                for (int i = 0; i < nodes.Count; i++)
                {
                    double dx = nodes[i].X - pt.X;
                    double dy = nodes[i].Y - pt.Y;
                    if (dx * dx + dy * dy <= tolSq) return i;
                }
                nodes.Add(new Point2d(pt.X, pt.Y));
                return nodes.Count - 1;
            }

            var curveEnds = new Dictionary<int, (int StartNode, int EndNode)>();
            var adj = new Dictionary<int, List<int>>();

            foreach (int cIdx in openIndices)
            {
                var pts = curves[cIdx].Points3D;
                int nStart = GetNodeId(pts[0]);
                int nEnd = GetNodeId(pts[pts.Count - 1]);
                if (nStart == nEnd) continue;

                curveEnds[cIdx] = (nStart, nEnd);
                if (!adj.TryGetValue(nStart, out var l1)) { l1 = new List<int>(); adj[nStart] = l1; }
                if (!adj.TryGetValue(nEnd, out var l2)) { l2 = new List<int>(); adj[nEnd] = l2; }
                l1.Add(cIdx);
                l2.Add(cIdx);
            }

            HashSet<int> visitedCurves = new HashSet<int>();
            foreach (int startCurve in curveEnds.Keys)
            {
                if (visitedCurves.Contains(startCurve)) continue;

                var (firstNode, curNode) = curveEnds[startCurve];
                if (adj[firstNode].Count != 2 || adj[curNode].Count != 2) continue;

                List<(int CurveIdx, bool Forward)> chain = new List<(int, bool)> { (startCurve, true) };
                HashSet<int> cycleSet = new HashSet<int> { startCurve };
                int prevCurve = startCurve;
                bool closedOk = false;

                while (true)
                {
                    if (curNode == firstNode)
                    {
                        closedOk = true;
                        break;
                    }
                    if (!adj.TryGetValue(curNode, out var inc) || inc.Count != 2)
                        break;

                    int nextCurve = inc[0] == prevCurve ? inc[1] : inc[0];
                    if (cycleSet.Contains(nextCurve))
                        break;

                    var ends = curveEnds[nextCurve];
                    bool forward = ends.StartNode == curNode;
                    curNode = forward ? ends.EndNode : ends.StartNode;
                    chain.Add((nextCurve, forward));
                    cycleSet.Add(nextCurve);
                    prevCurve = nextCurve;
                }

                if (closedOk && chain.Count >= 2)
                {
                    List<Point3d> loopPts = new List<Point3d>();
                    foreach (var (cIdx, fwd) in chain)
                    {
                        visitedCurves.Add(cIdx);
                        var srcPts = curves[cIdx].Points3D;
                        if (fwd)
                        {
                            for (int k = 0; k < srcPts.Count; k++) loopPts.Add(srcPts[k]);
                        }
                        else
                        {
                            for (int k = srcPts.Count - 1; k >= 0; k--) loopPts.Add(srcPts[k]);
                        }
                    }

                    var poly = new Polygon2D(loopPts);
                    if (poly.Vertices3D.Count >= 3 && poly.AbsArea > 1e-3)
                    {
                        outPolys.Add(poly);
                        foreach (var (cIdx, _) in chain) curvesInClosedLoops.Add(cIdx);
                    }
                }
            }
        }

        private static bool AddCurveToSurfaceBreaklines(
            CivSurface surface,
            SourceCurveInfo curve,
            double midOrdDistance,
            double supplementingDistance,
            bool usePointsOnly = false,
            bool allowDirectCurvedEntity = true)
        {
            // Thử liên kết trực tiếp ObjectId nếu là FeatureLine chuẩn (không gắn reactor vào surface tạm khi usePointsOnly = true)
            bool canTryDirectEntity =
                !usePointsOnly &&
                !curve.WasAutoClosedGap &&
                (allowDirectCurvedEntity || !curve.HasTrueArcs) &&
                (curve.EntityTypeName == "FeatureLine" || curve.EntityTypeName == "Polyline3D");

            if (canTryDirectEntity)
            {
                try
                {
                    var coll = new ObjectIdCollection { curve.Id };
                    surface.BreaklinesDefinition.AddStandardBreaklines(
                        coll,
                        midOrdDistance,
                        supplementingDistance,
                        0.0,
                        0.0);
                    return true;
                }
                catch
                {
                    // Tự động chuyển sang nạp Point3dCollection chính xác bên dưới
                }
            }

            Point3dCollection ptsColl = BuildCleanPoint3dCollection(curve.Points3D, curve.IsClosed);
            if (ptsColl.Count < 2) return false;

            try
            {
                surface.BreaklinesDefinition.AddStandardBreaklines(
                    ptsColl,
                    midOrdDistance,
                    supplementingDistance,
                    0.0,
                    0.0);
                return true;
            }
            catch
            {
                try
                {
                    surface.AddVertices(ptsColl);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        private static Point3dCollection BuildCleanPoint3dCollection(List<Point3d> rawPts, bool ensureClosed)
        {
            var coll = new Point3dCollection();
            for (int i = 0; i < rawPts.Count; i++)
            {
                Point3d p = rawPts[i];
                if (coll.Count > 0)
                {
                    Point3d prev = coll[coll.Count - 1];
                    if (prev.DistanceTo(p) < 1e-5) continue;
                }
                coll.Add(p);
            }

            if (ensureClosed && coll.Count >= 3)
            {
                Point3d first = coll[0];
                Point3d last = coll[coll.Count - 1];
                if (Math.Sqrt(Math.Pow(first.X - last.X, 2) + Math.Pow(first.Y - last.Y, 2)) > 1e-4)
                {
                    coll.Add(first);
                }
            }

            return coll;
        }

        private static void AddNativeBoundaryPolygon(
            CivSurface surface,
            Polygon2D poly,
            double midOrdDistance,
            SurfaceBoundaryType bndType)
        {
            if (poly.Vertices3D.Count < 3) return;
            Point3dCollection pts = BuildCleanPoint3dCollection(poly.Vertices3D, true);
            if (pts.Count < 4) return;

            try
            {
                surface.BoundariesDefinition.AddBoundaries(pts, midOrdDistance, bndType, false);
            }
            catch { }
        }

        private static void AddPolygon2DAsNonDestructiveBreakline(
            CivSurface surface,
            Polygon2D poly,
            double midOrdDistance)
        {
            if (poly.Vertices3D.Count < 3) return;
            Point3dCollection pts = BuildCleanPoint3dCollection(poly.Vertices3D, true);
            if (pts.Count < 4) return;

            try
            {
                surface.BreaklinesDefinition.AddNonDestructiveBreaklines(pts, midOrdDistance);
            }
            catch { }
        }

        private static List<Polygon2D> ExtractPolygonsFromBoundaryIds(
            List<ObjectId>? boundaryIds,
            Transaction tr,
            double midOrdDistance)
        {
            var result = new List<Polygon2D>();
            if (boundaryIds == null || boundaryIds.Count == 0) return result;

            foreach (ObjectId id in boundaryIds)
            {
                if (id.IsNull || !id.IsValid || id.IsErased) continue;
                SourceCurveInfo? info = ExtractSourceCurveInfo(id, tr, midOrdDistance, 0.25, true, 0);
                if (info != null && info.Points3D.Count >= 3)
                {
                    var poly = new Polygon2D(info.Points3D);
                    if (poly.Vertices3D.Count >= 3 && poly.AbsArea > 1e-3)
                    {
                        result.Add(poly);
                    }
                }
            }

            return result;
        }

        internal static SourceCurveInfo? ExtractSourceCurveInfo(
            ObjectId entId,
            Transaction tr,
            double midOrdDistance,
            double autoCloseTol,
            bool allowAutoClose,
            int curveIndex)
        {
            if (entId.IsNull || !entId.IsValid || entId.IsErased) return null;

            CadEntity? ent = tr.GetObject(entId, OpenMode.ForWrite) as CadEntity;
            if (ent == null) return null;

            var info = new SourceCurveInfo
            {
                CurveIndex = curveIndex,
                Id = entId
            };

            double sampleMidOrd = Math.Max(0.008, midOrdDistance * 0.25);
            List<Point3d> pts = new List<Point3d>();

            if (ent is AutoCorridorFeatureLine acfl)
            {
                info.EntityTypeName = "Corridor FL";
                string code = string.Empty;
                string corr = string.Empty;
                try { code = acfl.CorridorFeaturelineCode ?? ""; } catch { }
                try { corr = acfl.CorridorName ?? ""; } catch { }
                info.DisplayName = !string.IsNullOrWhiteSpace(code)
                    ? (!string.IsNullOrWhiteSpace(corr) ? $"{code} ({corr})" : code)
                    : (acfl.Name ?? acfl.Handle.ToString());
                info.IsClosed = SafeGetClosed(acfl);
                try { info.HasTrueArcs = acfl.CurvesCount > 0; } catch { }
                pts = ExtractFeatureLine3DPoints(acfl, sampleMidOrd);
            }
            else if (ent is FeatureLine fl)
            {
                info.EntityTypeName = "FeatureLine";
                info.DisplayName = !string.IsNullOrWhiteSpace(fl.Name) ? fl.Name : $"FL_{fl.Handle}";
                info.IsClosed = SafeGetClosed(fl);
                try { info.HasTrueArcs = fl.CurvesCount > 0; } catch { }
                pts = ExtractFeatureLine3DPoints(fl, sampleMidOrd);
            }
            else if (ent is Polyline pl)
            {
                info.EntityTypeName = "2D Polyline";
                info.DisplayName = $"PL_{pl.Handle}";
                info.IsClosed = pl.Closed;
                info.HasTrueArcs = pl.HasBulges;
                pts = SampleLwPolyline3D(pl, sampleMidOrd);
            }
            else if (ent is Polyline3d p3d)
            {
                info.EntityTypeName = "Polyline3D";
                info.DisplayName = $"3DP_{p3d.Handle}";
                info.IsClosed = p3d.Closed;
                foreach (ObjectId vId in p3d)
                {
                    if (tr.GetObject(vId, OpenMode.ForWrite) is PolylineVertex3d v)
                    {
                        pts.Add(v.Position);
                    }
                }
            }
            else if (ent is Polyline2d p2d)
            {
                info.EntityTypeName = "Polyline2D";
                info.DisplayName = $"P2D_{p2d.Handle}";
                info.IsClosed = p2d.Closed;
                foreach (ObjectId vId in p2d)
                {
                    if (tr.GetObject(vId, OpenMode.ForWrite) is Vertex2d v)
                    {
                        pts.Add(new Point3d(v.Position.X, v.Position.Y, p2d.Elevation));
                    }
                }
            }
            else if (ent is Line ln)
            {
                info.EntityTypeName = "Line";
                info.DisplayName = $"Line_{ln.Handle}";
                info.IsClosed = false;
                pts.Add(ln.StartPoint);
                pts.Add(ln.EndPoint);
            }
            else if (ent is Arc arc)
            {
                info.EntityTypeName = "Arc";
                info.DisplayName = $"Arc_{arc.Handle}";
                info.IsClosed = false;
                SampleArc3D(arc, midOrdDistance, pts);
            }
            else
            {
                return null;
            }

            // Lọc các đỉnh trùng liên tiếp
            List<Point3d> cleaned = new List<Point3d>(pts.Count);
            for (int i = 0; i < pts.Count; i++)
            {
                if (cleaned.Count == 0 || cleaned[cleaned.Count - 1].DistanceTo(pts[i]) > 1e-5)
                {
                    cleaned.Add(pts[i]);
                }
            }

            if (cleaned.Count < 2) return null;

            double gap2D = Math.Sqrt(
                Math.Pow(cleaned[0].X - cleaned[cleaned.Count - 1].X, 2) +
                Math.Pow(cleaned[0].Y - cleaned[cleaned.Count - 1].Y, 2));

            if (info.IsClosed)
            {
                if (gap2D > 1e-4)
                {
                    cleaned.Add(cleaned[0]);
                }
            }
            else if (cleaned.Count >= 3 && (gap2D <= 0.005 || (allowAutoClose && gap2D <= autoCloseTol)))
            {
                info.IsClosed = true;
                if (gap2D > 1e-4)
                {
                    info.WasAutoClosedGap = true;
                    cleaned.Add(cleaned[0]);
                }
            }

            info.Points3D = cleaned;
            return info;
        }

        private static bool SafeGetClosed(Curve c)
        {
            try { return c.Closed; }
            catch { return false; }
        }

        private static List<Point3d> ExtractFeatureLine3DPoints(FeatureLine fl, double midOrdDistance)
        {
            List<Point3d> result = new List<Point3d>();

            try
            {
                Point3dCollection allPts = fl.GetPoints(FeatureLinePointType.AllPoints);
                if (allPts != null && allPts.Count >= 2)
                {
                    bool hasCurves = false;
                    try { hasCurves = fl.CurvesCount > 0; } catch { }

                    if (!hasCurves)
                    {
                        foreach (Point3d p in allPts) result.Add(p);
                        if (fl.Closed && result[0].DistanceTo(result[result.Count - 1]) > 1e-4)
                            result.Add(result[0]);
                        return result;
                    }

                    // Nếu có cung cong, lấy mẫu mịn dọc theo Curve giữa các điểm đặc trưng
                    for (int i = 0; i < allPts.Count - 1; i++)
                    {
                        Point3d pA = allPts[i];
                        Point3d pB = allPts[i + 1];
                        result.Add(pA);

                        try
                        {
                            double dA = fl.GetDistAtPoint(pA);
                            double dB = fl.GetDistAtPoint(pB);
                            double spanLen = dB - dA;
                            double chordLen = pA.DistanceTo(pB);
                            if (spanLen > chordLen + 1e-4)
                            {
                                double step = EstimateArcStepFromSagitta(spanLen, chordLen, midOrdDistance);
                                int nSteps = Math.Max(4, (int)Math.Ceiling(spanLen / step));
                                for (int s = 1; s < nSteps; s++)
                                {
                                    result.Add(fl.GetPointAtDist(dA + spanLen * s / nSteps));
                                }
                            }
                        }
                        catch { }
                    }

                    result.Add(allPts[allPts.Count - 1]);

                    if (fl.Closed && result[0].DistanceTo(result[result.Count - 1]) > 1e-4)
                    {
                        result.Add(result[0]);
                    }
                    return result;
                }
            }
            catch { }

            // Fallback: Explode đối tượng tạm trong bộ nhớ và giải phóng (Dispose) triệt để
            try
            {
                using (DBObjectCollection exploded = new DBObjectCollection())
                {
                    fl.Explode(exploded);
                    foreach (DBObject obj in exploded)
                    {
                        try
                        {
                            if (obj is Arc arc)
                                SampleArc3D(arc, midOrdDistance, result);
                            else if (obj is Polyline pl)
                                result.AddRange(SampleLwPolyline3D(pl, midOrdDistance));
                            else if (obj is Line ln)
                            {
                                result.Add(ln.StartPoint);
                                result.Add(ln.EndPoint);
                            }
                            else if (obj is Curve cv)
                            {
                                result.Add(cv.StartPoint);
                                result.Add(cv.EndPoint);
                            }
                        }
                        finally
                        {
                            obj.Dispose();
                        }
                    }
                }
            }
            catch { }

            return result;
        }

        private static List<Point3d> SampleLwPolyline3D(Polyline pl, double midOrdDistance)
        {
            List<Point3d> pts = new List<Point3d>();
            int vn = pl.NumberOfVertices;
            if (vn == 0) return pts;

            int segCount = pl.Closed ? vn : vn - 1;
            for (int i = 0; i < segCount; i++)
            {
                int next = (i + 1) % vn;
                Point3d p0 = pl.GetPoint3dAt(i);
                Point3d p1 = pl.GetPoint3dAt(next);
                pts.Add(p0);

                double bulge = pl.GetBulgeAt(i);
                if (Math.Abs(bulge) > 1e-6)
                {
                    try
                    {
                        CircularArc2d arc2d = pl.GetArcSegment2dAt(i);
                        double r = arc2d.Radius;
                        double angle = 4.0 * Math.Atan(Math.Abs(bulge));
                        double arcLen = r * angle;
                        double sagStep = 2.0 * Math.Sqrt(Math.Max(1e-4, 2.0 * r * midOrdDistance - midOrdDistance * midOrdDistance));
                        int steps = Math.Max(4, (int)Math.Ceiling(arcLen / Math.Max(0.10, sagStep)));
                        Point2d[] samples = arc2d.GetSamplePoints(steps + 1);
                        for (int s = 1; s < samples.Length - 1; s++)
                        {
                            pts.Add(new Point3d(samples[s].X, samples[s].Y, pl.Elevation));
                        }
                    }
                    catch { }
                }

                if (!pl.Closed && i == segCount - 1)
                {
                    pts.Add(p1);
                }
            }

            if (pl.Closed && pts.Count > 0)
            {
                pts.Add(pts[0]);
            }

            return pts;
        }

        private static void SampleArc3D(Arc arc, double midOrdDistance, List<Point3d> outPts)
        {
            double r = arc.Radius;
            double sagStep = 2.0 * Math.Sqrt(Math.Max(1e-4, 2.0 * r * midOrdDistance - midOrdDistance * midOrdDistance));
            int steps = Math.Max(4, (int)Math.Ceiling(arc.Length / Math.Max(0.10, sagStep)));
            double delta = arc.Length / steps;
            for (int i = 0; i <= steps; i++)
            {
                outPts.Add(arc.GetPointAtDist(Math.Min(arc.Length, i * delta)));
            }
        }

        private static double EstimateArcStepFromSagitta(double arcLen, double chordLen, double midOrd)
        {
            if (arcLen <= chordLen + 1e-5) return Math.Max(0.5, arcLen);
            // Xấp xỉ góc tâm theta từ L/c ~ (theta/2)/sin(theta/2) -> theta ~ sqrt(24*(1 - c/L))
            double theta = Math.Sqrt(Math.Max(1e-4, 24.0 * (1.0 - chordLen / arcLen)));
            double radius = arcLen / theta;
            double step = 2.0 * Math.Sqrt(Math.Max(1e-4, 2.0 * radius * midOrd - midOrd * midOrd));
            return Math.Max(0.15, step);
        }

        /// <summary>
        /// Tương thích với các chỗ gọi cũ nếu có.
        /// </summary>
        public static List<Point3d> ExtractBoundaryPoints(ObjectId entId, Transaction tr, double curveSampling, bool autoClose)
        {
            SourceCurveInfo? info = ExtractSourceCurveInfo(entId, tr, curveSampling, 0.10, autoClose, 0);
            return info != null ? info.Points3D : new List<Point3d>();
        }

        #endregion

        #region CIVIL 3D SURFACE UTILITY HELPERS

        private static int CountVisibleTriangles(CivSurface surface)
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

        #endregion
    }
}

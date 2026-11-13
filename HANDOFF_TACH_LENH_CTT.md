# HANDOFF — Tách lệnh `CTT_Tao_Mat_Phang_Tu_Feature_Line` thành nhiều file nhỏ

> **Người nhận**: dev / AI agent khác. **Trạng thái**: CHƯA làm. **Ưu tiên**: trung bình.
> Đọc trước: `GEMINI.md` mục 4 (kiến trúc 1 lệnh = 1 thư mục nhiều file nhỏ) và `AGENTS.md` mục 5.

## 1. Mục tiêu

Hai file hiện quá lớn để AI agent đọc/sửa trong 1 lần:

| File | Số dòng |
|---|---|
| `MyFirstProject/Civil Tool 2/22.CTT_Tao_Mat_Phang_Tu_Featureline.cs` (class `CTT_Tao_Mat_Phang_Tu_Featureline_Command`) | 4.447 |
| `MyFirstProject/Civil Tool 2/TaoMatPhangTuFeaturelineForm.cs` (class `TaoMatPhangTuFeaturelineForm`) | 2.389 |

Tách thành nhiều file **≤ 800 dòng** (mục tiêu ≤ 400) bằng `partial class`.

## 2. Ràng buộc BẮT BUỘC (Giai đoạn 1)

- **KHÔNG đổi logic**: chỉ *di chuyển nguyên văn* các khối code. Không đổi tên hàm, không sửa thân hàm, không gộp/tách hàm, không "tiện tay" sửa bug.
- **Giữ nguyên namespace** `MyFirstProject.Civil_Tool_2` và tên class (Form gọi `CTT_Tao_Mat_Phang_Tu_Featureline_Command.Xxx` ở 9 chỗ).
- **Giữ nguyên tên lệnh** `CTT_Tao_Mat_Phang_Tu_Feature_Line` và các dòng menu trong `ClassicMenu.cs` / `ToolPaletteDataReader.cs`.
- Các class lồng `private sealed class` / `private readonly struct` (TIN topology, spatial grid…) **vẫn lồng trong partial class** — chỉ chuyển sang file khác, không đổi `private` thành `internal`.
- Không động vào `.csproj` (project SDK-style tự nhận file `.cs` mới).
- 1 PR riêng, chỉ chứa thao tác tách file.

## 3. Thư mục đích

`MyFirstProject/Commands/CTT/TaoMatPhangTuFeatureline/` (namespace vẫn là `MyFirstProject.Civil_Tool_2` ở giai đoạn 1).

Mỗi file mới có khung:

```csharp
// copy NGUYÊN khối using + alias ở đầu file gốc (dòng 1–17), xóa using thừa sau khi build OK là tùy chọn
namespace MyFirstProject.Civil_Tool_2
{
    public partial class CTT_Tao_Mat_Phang_Tu_Featureline_Command
    {
        // khối code di chuyển nguyên văn
    }
}
```

- Đổi khai báo gốc dòng 125 `public class CTT_Tao_Mat_Phang_Tu_Featureline_Command` → `public partial class …` (tất cả các phần đều phải có `partial`).
- `[assembly: CommandClass(typeof(...))]` (dòng 19) chỉ để **ở đúng 1 file** (`*.Command.cs`).
- `#region` / `#endregion` phải **cân bằng trong từng file**. Region dòng 549–2088 bị chia ra nhiều file → mỗi file tự mở/đóng region của mình.

## 4. Bảng ánh xạ — file lệnh (4.447 dòng)

> Mốc dòng theo commit `07d2df8` (master). Khi cắt, kéo theo cả comment `///` ngay phía trên mỗi hàm.

| Dòng gốc | Nội dung | File mới | ~Dòng |
|---|---|---|---|
| 21–124 | `FeatureLineBoundaryMode`, `TaoMatPhangOptions`, `SurfaceCreationResult` (type cấp namespace, không nằm trong class) | `TaoMatPhangModels.cs` | 105 |
| 19, 125–151 | assembly attribute + `[CommandMethod]` `TaoMatPhangTuFeatureline()` | `CTT_Tao_Mat_Phang_Tu_Feature_Line.Command.cs` | 40 |
| 152–548 | `ThucHienTaoMatPhang` (2 overload), `PopulateCleanFeatureLineSurface` | `TaoMatPhangService.Create.cs` | 400 |
| 549–634 | `ExtractCleanSubSurfaceSuffix`, `BuildPrefixedSubSurfaceName`, `IsDefaultAutoSubSuffix`, `SuggestNextSubSurfaceSuffix` | `TaoMatPhangService.SubSurfaceNaming.cs` | 85 |
| 635–1187 | `SupplementFeatureLinesIntoExistingSurface` (1 hàm ~550 dòng) | `TaoMatPhangService.Supplement.cs` | 555 |
| 1188–1604 | `SupplementViaManagedSubSurface`, `DoesGraphExtendOutsideGrid`, `RemoveRestrictiveOuterBoundaries`, `RemoveDeleteLineOperations` | `TaoMatPhangService.SubSurface.cs` | 420 |
| 1605–1798 | `AppendClippedSegmentsOutsideRegion`, `BisectRegionBorderCrossing`, `ChainSegmentsToPolylines` | `TaoMatPhangService.Clipping.cs` | 195 |
| 1799–2088 | `ReconcileMergedVerticesAndElevations`, `FindLiveVertexNear`, `DeleteVerticesSafe`, `EnforceMissingPatchLockedEdges`, `Hypot2D`, `GetSurfaceOriginationType` | `TaoMatPhangService.Reconcile.cs` | 290 |
| 2090–2487 | Trimming engine: `TrimUnwantedTrianglesOnSingleSurface`, `TrimTrianglesByCustomPolygons`, `ApplyRemovedTrianglesToSurface`, `EdgeHasKeptTriangle`, `IsEdgeExposedToVoid` | `TaoMatPhangService.Trimming.cs` | 400 |
| 2489–2680 | `QuantizedPoint2D`, `UndirectedEdgeKey`, `TinVertexNode`, `TinEdgeNode`, `TinTriangleNode`, `TinTopologyGraph`, `BuildTinTopologyGraph` | `Topology/TinTopologyGraph.cs` | 190 |
| 2681–2834 | `CurveVertexAttachmentMap`, `BuildCurveVertexAttachmentMap` | `Topology/CurveVertexAttachmentMap.cs` | 155 |
| 2835–3025 | `SpatialSegmentGrid` | `Topology/SpatialSegmentGrid.cs` | 190 |
| 3026–3252 | `TriangleVertexKey`, `BuildTriangleVertexKeySet`, `SpatialTriangleGrid2D` | `Topology/SpatialTriangleGrid2D.cs` | 225 |
| 3253–3394 | `SpatialBorderSegmentGrid2D`, `BuildBorderSegmentGrid`, `SpatialVertexLookup2D` | `Topology/SpatialBorderAndVertexLookup.cs` | 140 |
| 3396–3485 | `SourceCurveInfo`, `Polygon2D` | `Geometry/Polygon2D.cs` | 90 |
| 3487–3821 | `BuildClosedAndChainedRegions`, `ChainSimpleClosedCyclesFromOpenCurves`, `AddCurveToSurfaceBreaklines`, `BuildCleanPoint3dCollection`, `AddNativeBoundaryPolygon`, `AddPolygon2DAsNonDestructiveBreakline`, `ExtractPolygonsFromBoundaryIds` | `TaoMatPhangService.Regions.cs` | 335 |
| 3822–4136 | `ExtractSourceCurveInfo`, `SafeGetClosed`, `ExtractFeatureLine3DPoints`, `SampleLwPolyline3D`, `SampleArc3D`, `EstimateArcStepFromSagitta`, `ExtractBoundaryPoints` | `TaoMatPhangService.CurveExtraction.cs` | 315 |
| 4138–4445 | `CountVisibleTriangles`, `SetAutoRebuildSafe`, `FindSurfaceStyleId`, `FindSurfaceByName`, `EnsureUniqueSurfaceName`, `EnsureUniquePrefixedSubSurfaceName`, `ResolveSubSurfaceStyleId`, `ClearSurfaceDefinitionsAndEdits`, `FindPasteOperationForSubSurface`, `HasRestrictiveOuterBoundary`, `GetSampleVisibleCentroids`, `IsPointVisibleOnSurface` | `TaoMatPhangService.SurfaceUtils.cs` | 310 |

> Tên file có chữ `Service` nhưng **class vẫn là** `CTT_Tao_Mat_Phang_Tu_Featureline_Command` (partial). Đổi tên class là việc của giai đoạn 2.

## 5. Bảng ánh xạ — Form (2.389 dòng)

Đổi dòng 45 `public class TaoMatPhangTuFeaturelineForm : Form` → `public partial class TaoMatPhangTuFeaturelineForm : Form`. Khối using/alias dòng 1–27 copy sang mọi file.

| Dòng gốc | Nội dung | File mới | ~Dòng |
|---|---|---|---|
| 31–44 | class `SelectedFeatureLineBoundaryItem` | `SelectedFeatureLineBoundaryItem.cs` | 15 |
| 45–192 | Field, constructor, `Dispose`, `CreateFont` | `TaoMatPhangTuFeaturelineForm.cs` | 150 |
| 193–1136 | `InitializeCustomUI` (1 hàm ~945 dòng) | `TaoMatPhangTuFeaturelineForm.Layout.cs` | 945 ⚠️ |
| 1137–1329 | `CreateActionButton`, `UpdateControlStates`, `OnSelectedExistingSurfaceChanged`, sub-surface name preview, `LooksLikeAuxiliarySubSurface` | `TaoMatPhangTuFeaturelineForm.State.cs` | 195 |
| 1330–1625 | Region SCAN: `ScanDrawingSourcesAndStyles`, `SelectPreferredSubSurfaceStyle`, `PopulateFilterValuesDropdown`, `BtnLoadByFilter_Click`, `ExtractKeyBeforeCount` | `TaoMatPhangTuFeaturelineForm.Scan.cs` | 295 |
| 1626–1831 | Region PERSISTENT: `SaveCurrentSettings`, `RestoreLastSettings`, `ClampDecimal`, `IsIdAlive`, `TryRestorePreviousSelection`, `RestoreLastExecutedItems`… | `TaoMatPhangTuFeaturelineForm.Settings.cs` | 205 |
| 1832–2108 | Region SELECTION: pick nguồn/surface/boundary, `PopulateItems`, ListView, `UpdateSummary` | `TaoMatPhangTuFeaturelineForm.Selection.cs` | 280 |
| 2109–2389 | `BtnSuggestMaxEdge_Click` (thuộc region SELECTION, kết thúc 2169) + region EXECUTION `BtnExecute_Click` | `TaoMatPhangTuFeaturelineForm.Execute.cs` | 280 |

> ⚠️ `Layout.cs` vẫn ~945 dòng > 800. Ở giai đoạn 1 **chấp nhận** (vì tách 1 hàm thành nhiều hàm là đổi cấu trúc). Nếu muốn xử lý luôn: tách `InitializeCustomUI` thành `BuildSourceGroup()`, `BuildOptionsGroup()`… theo từng GroupBox, đặt trong `Layout.<Nhom>.cs` — làm ở **commit riêng**, chú ý biến cục bộ dùng chung giữa các GroupBox phải nâng thành field hoặc truyền tham số.

## 6. Các bước thực hiện

1. Tạo nhánh `refactor/tach-file-ctt-tao-mat-phang` từ `master`.
2. `git mv` file gốc sang file lớn nhất trong thư mục đích (giữ lịch sử blame), ví dụ:
   `git mv "MyFirstProject/Civil Tool 2/22.CTT_Tao_Mat_Phang_Tu_Featureline.cs" "MyFirstProject/Commands/CTT/TaoMatPhangTuFeatureline/TaoMatPhangService.Supplement.cs"` — tương tự Form → `TaoMatPhangTuFeaturelineForm.cs`.
3. Thêm `partial` vào 2 khai báo class.
4. Cắt từng khối theo bảng mục 4 và 5 **từ dưới lên** (để mốc dòng phía trên không bị lệch), dán vào file mới có khung ở mục 3.
5. Cân bằng `#region`/`#endregion` từng file; assembly attribute chỉ ở 1 file.
6. Build: `powershell -ExecutionPolicy Bypass -NoProfile -File "c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\BuildProject.ps1"` → **0 Error(s)**, số warning không tăng.
7. Kiểm chứng không đổi logic (Git Bash/WSL) — chỉ được khác các dòng `using`, `namespace`, `partial class`, `{`, `}`, `#region`, `#endregion`, `[assembly`:
   ```bash
   shopt -s globstar
   git show "master:MyFirstProject/Civil Tool 2/22.CTT_Tao_Mat_Phang_Tu_Featureline.cs" > /tmp/old.cs
   git show "master:MyFirstProject/Civil Tool 2/TaoMatPhangTuFeaturelineForm.cs" >> /tmp/old.cs
   cat MyFirstProject/Commands/CTT/TaoMatPhangTuFeatureline/**/*.cs > /tmp/new.cs
   diff <(sed 's/^\s*//' /tmp/old.cs | grep -v '^$' | sort) \
        <(sed 's/^\s*//' /tmp/new.cs | grep -v '^$' | sort) \
     | grep -vE '^[<>] (using |namespace |public partial class|public class|[{}]$|#region|#endregion|\[assembly)'
   ```
   Kết quả phải rỗng (trừ dòng `---`/số dòng của diff).
8. Test trong Civil 3D 2027 (`NETRELOAD`), chạy `CTT_Tao_Mat_Phang_Tu_Feature_Line`:
   - Tạo surface mới từ FeatureLine (có/không boundary tùy chọn).
   - Bổ sung FeatureLine vào surface có sẵn (merge trực tiếp + qua sub-surface có tiền tố).
   - Gợi ý tên sub-surface, nút "Khôi phục lần trước", gợi ý Max Edge.
   - Đóng/mở lại Form → thông số được nhớ.
9. Commit: `refactor: tach CTT_Tao_Mat_Phang_Tu_Featureline thanh nhieu file partial (khong doi logic)` → push → tạo PR.

## 7. Giai đoạn 2 (PR riêng, SAU khi giai đoạn 1 đã merge)

- Thay helper trùng bằng thư viện chung: `FindSurfaceByName`, `EnsureUniqueSurfaceName`, `FindSurfaceStyleId` → `MyFirstProject.Shared.Civil.SurfaceHelpers` (lưu ý bản cũ mở surface `ForWrite` chỉ để đọc tên — bản Shared mở `ForRead`). Zoom → `Shared.Cad.ZoomHelper`. Form: `IsIdAlive`/`ClampDecimal` → `Shared.UI.PersistentState`.
- Đưa các cấu trúc TIN topology / spatial grid ra `MyFirstProject/Shared/Geometry/` nếu lệnh khác cần dùng (khi đó đổi `private` → `internal`).
- Tách hàm > 120 dòng: `SupplementFeatureLinesIntoExistingSurface` (~550), `SupplementViaManagedSubSurface` (~350), `InitializeCustomUI` (~945), `ThucHienTaoMatPhang` (~200).
- Đổi namespace sang `MyFirstProject.Commands.CTT.TaoMatPhangTuFeatureline` và tách class lệnh thành `…Command` + `TaoMatPhangService`.

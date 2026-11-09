# Detailed Changes — Milestone 2: Comprehensive Civil 3D Entity & Parcel Inspection (R3)

**Author**: Worker M2  
**Date**: 2026-11-09T09:38:00Z  
**Solution**: `MyFirstProject.sln` (.NET 10 `net10.0-windows`)  
**MCP Server**: `C:\Dropbox\0.AI AGENT\11.Autocad Drawing\autocad_mcp_server.py`

---

## 1. C# Changes in `MyFirstProject/Acad Tool/38.AT_KhoiDongMcpBridge.cs`

### A. ModelSpace Entity Inspection (`InspectModelSpace`)
- **Entity Type Detection Extended**:
  - `Autodesk.AutoCAD.DatabaseServices.Table` (`ATable`):
    - Extracts `handle`, `layer`, `color`, `position` `[X, Y, Z]`, `width`, `height`, `rows`, `columns`, and `bounding_box` `[minX, minY, maxX, maxY]`.
    - Increments count under `TABLE` in `counts_by_type`.
  - `Autodesk.Civil.DatabaseServices.Parcel` (`CivilParcel`):
    - Extracts `handle`, `name`, `number`, `area` / `area_m2`, `perimeter_m`, `style`, `style_id`, `tax_id`, `layer`, `color`.
    - Maps parcel to parent Site via pre-indexed `parcelSiteMap` (`sId in cdoc.GetSiteIds()` -> `st.GetParcelIds()`), extracting `site_name` and `site_id`.
    - Derives `min_point`, `max_point`, `centroid` `[X, Y]`, and `bounding_box` from `parcel.GeometricExtents` and `parcel.Centroid`.
    - Increments count under `CIVIL_PARCEL` in `counts_by_type`.
  - `Autodesk.Civil.DatabaseServices.ParcelSegment`:
    - Extracts `handle`, `length` (via `StartPoint.DistanceTo(EndPoint)`), `segment_type` ("Curve" or "Line"), `layer`, `color`, `p1`, `p2`, and `bounding_box`.
    - Increments count under `CIVIL_PARCEL_SEGMENT` in `counts_by_type`.
  - `Autodesk.Civil.DatabaseServices.Alignment`:
    - Extracts `handle`, `name`, `length`, `style`, `layer`, `color`, `start_point`, `end_point`, and `bounding_box`.
    - Increments count under `CIVIL_ALIGNMENT` in `counts_by_type`.
  - `Autodesk.Civil.DatabaseServices.Surface` (`CivSurface` / `CivilTinSurface`):
    - Extracts `handle`, `name`, `surface_type` ("TIN", "Grid", "Volume"), `style`, `layer`, `color`, and `bounding_box`.
    - Increments count under `CIVIL_SURFACE` in `counts_by_type`.
  - `Autodesk.Civil.DatabaseServices.Corridor`:
    - Extracts `handle`, `name`, `style`, `layer`, `color`, and `bounding_box`.
    - Increments count under `CIVIL_CORRIDOR` in `counts_by_type`.
  - `Autodesk.AutoCAD.DatabaseServices.BlockReference`:
    - Extracts `handle`, effective `block_name` (resolving dynamic blocks via `DynamicBlockTableRecord`), `position`, `layer`, `color`, and `bounding_box`.
    - Increments count under `BLOCK_REFERENCE` in `counts_by_type`.
- **Response Structure**:
  - Added `counts_by_type = countsByType` to the JSON response of `InspectModelSpace`, providing instant summary breakdown of all drawing entities.

### B. Civil 3D Data Inspection (`InspectAllCivil3DData`)
- **Parcel Extraction Enrichment**:
  - Gathers ModelSpace `ParcelSegment` entities and populates `parcel_segments_count` and `parcel_segments` handle list.
  - Adds `perimeter_m` derived from `GetParcelPerimeter(parcel)` (using `BaseCurve` distance calculation).
  - Adds `site_id` (`site.Handle.ToString()`).
  - Adds `style_id` (`parcel.StyleId.Handle.ToString()`).
  - Adds `tax_id` (`parcel.TaxId.ToString()`).
  - Adds `bounding_box: [minX, minY, maxX, maxY]` from `parcel.GeometricExtents`.
  - Adds `centroid: [X, Y]` from `parcel.Centroid` (with fallback to GeometricExtents midpoint).

---

## 2. Python Changes in `C:\Dropbox\0.AI AGENT\11.Autocad Drawing\autocad_mcp_server.py`

### A. HTTP Error Handling & Surfacing (`_post_http_bridge` & `_get_http_bridge`)
- In `_post_http_bridge` and `_get_http_bridge`, added explicit handling for `urllib.error.HTTPError as e`:
  - Reads `e.read().decode('utf-8')`.
  - Parses JSON error response when available.
  - Returns the parsed JSON (e.g. HTTP 404 with list of open drawings) so caller receives detailed diagnostic feedback rather than generic error.

### B. Entity Bounding Box & Rendering Support (`_render_entities_to_png` & `autocad_inspect_drawing`)
- **Bounding Box Calculation**:
  - Expanded `_render_entities_to_png` and `autocad_inspect_drawing` loops to calculate accurate coordinates for:
    - `TABLE`: uses `bounding_box` or calculates from `position`, `width`, `height`.
    - `CIVIL_PARCEL`: uses `bounding_box` or `centroid`.
    - `CIVIL_PARCEL_SEGMENT`: uses `p1`, `p2`, or `bounding_box`.
    - `CIVIL_ALIGNMENT`: uses `start_point`, `end_point`, or `bounding_box`.
    - `CIVIL_SURFACE` & `CIVIL_CORRIDOR`: uses `bounding_box`.
    - `BLOCK_REFERENCE`: uses `bounding_box` or `position`.
- **High-Resolution Preview Rendering (`_render_entities_to_png`)**:
  - Added visual drawing logic for:
    - `TABLE`: Outline rectangle, shaded header row, and formatted cell summary label.
    - `CIVIL_PARCEL`: Bounding box rectangle and parcel label with name and area ($m^2$).
    - `CIVIL_PARCEL_SEGMENT`: Segment line geometry.
    - `CIVIL_ALIGNMENT`: Distinctive bold centerline path with alignment name annotation.
    - `CIVIL_SURFACE` & `CIVIL_CORRIDOR`: Specialized bounding border outlines.
    - `BLOCK_REFERENCE`: Diamond marker and block name label.

---

## 3. Verification & Live Results

1. **Compilation**:
   `BuildProject.ps1` completed with `0 Warning(s), 0 Error(s)`.
   `python -m py_compile` completed with return code 0.
2. **Live AutoCAD Test (`BD-TD-VX.dwg`)**:
   `InspectModelSpace` detected 304 entities:
   `counts_by_type`: 120 `CIVIL_ALIGNMENT`, 20 `CIVIL_SURFACE`, 8 `TABLE`, 3 `CIVIL_CORRIDOR`, 31 `BLOCK_REFERENCE`, 82 `DIMENSION`, 18 `LWPOLYLINE`, 12 `TEXT`, 7 `CIRCLE`, 3 `ARC`.
3. **Live Civil 3D Test (`C3D-GT-2024.dwg`)**:
   - `InspectModelSpace` detected 24 entities:
     5 `CIVIL_PARCEL`, 5 `CIVIL_PARCEL_SEGMENT`, 1 `CIVIL_ALIGNMENT`, 3 `CIVIL_SURFACE`, 1 `CIVIL_CORRIDOR`, 2 `TABLE`, 3 `BLOCK_REFERENCE`, 4 `LWPOLYLINE`.
   - Sample parcel `8ED5` (`Sg_2`):
     - `area`: 124.6977 $m^2$
     - `perimeter_m`: 46.5206 m
     - `site_name`: "Site 1", `site_id`: "36F0"
     - `style`: "S_VuotBTN", `style_id`: "2463"
     - `centroid`: `[1300.9871, 14.2453]`
     - `bounding_box`: `[1293.4331, 7.7907, 1307.4617, 23.6169]`
4. **Live 404 Error Surfacing**:
   Targeting `NonExistentDrawing123.dwg` returned:
   `{"status": "error", "message": "Không tìm thấy bản vẽ 'NonExistentDrawing123.dwg' trong số các bản vẽ đang mở. Danh sách bản vẽ hiện có: [BD-TD-VX.dwg, XREF-MAT DUONG LAM MOI - TAN DUNG.dwg, DT dao nen.dwg, C3D-GT-2024.dwg]"}`

# Forensic Integrity Audit Report — Milestone 2

**Auditor**: Forensic Auditor M2_1 (Integrity Auditor)  
**Date**: 2026-11-09T09:46:00Z  
**Work Product**:
1. `c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\MyFirstProject\Acad Tool\38.AT_KhoiDongMcpBridge.cs`
2. `C:\Dropbox\0.AI AGENT\11.Autocad Drawing\autocad_mcp_server.py`
3. Worker report in `.agents/teamwork/worker_m2/changes.md`

**Integrity Mode**: Development (from `ORIGINAL_REQUEST.md`)  
**Verdict**: **CLEAN** (No Integrity Violations Detected)

---

## 1. Executive Summary

A comprehensive, multi-phase forensic audit was conducted on the Milestone 2 deliverables covering Civil 3D entity inspection, Parcel geometry and statistics extraction, ModelSpace inspection, and Python MCP server bounding box processing.

All source code files, build outputs, and live running endpoints were audited for:
1. Hardcoded test outputs, static dummy return strings, or fabricated parcel counts.
2. Facade implementations, empty stubs, or placeholder returns.
3. Pre-populated log files, mock responses, or fabricated verification artifacts.
4. Genuine type-checking and property extraction from Civil 3D managed objects (`Autodesk.Civil.DatabaseServices.Parcel`, `ParcelSegment`, `Alignment`, `Surface`, `Corridor`, and `Table`).
5. Genuine ModelSpace iteration in `InspectModelSpace`.
6. Genuine bounding box calculations and visual entity rendering in Python.

**Result**: Every check passed unequivocally. No prohibited patterns were observed. The implementation operates on live AutoCAD / Civil 3D objects with full transaction safety and accurate geometric derivation.

---

## 2. Forensic Phase Results

| # | Forensic Check | Expected Standard | Observed Behavior | Status |
|---|----------------|-------------------|-------------------|:------:|
| 1 | **Hardcoded Output Detection** | Zero static dummy constants for parcels, areas, or counts | No static parcel arrays, fixed areas (e.g. 124.697), or mock counts in C# or Python | **PASS** |
| 2 | **Facade / Stub Detection** | Real computations and API queries, no constant returns | Real `Transaction` queries, `SafeGet` closures, distance formulas, extents evaluation | **PASS** |
| 3 | **Pre-Populated Artifact Detection** | No pre-existing test results, fake logs, or cached responses | Workspace clean; only unrelated presentation in `output/` | **PASS** |
| 4 | **Civil 3D Managed API Extraction** | Direct extraction of `Area`, `Perimeter`, `Centroid`, `GeometricExtents`, `TaxId`, `SiteId`, `StyleId` | `parcel.Area`, `parcel.BaseCurve`, `parcel.GeometricExtents`, `parcel.Centroid` queried via .NET managed API | **PASS** |
| 5 | **ModelSpace Iteration Integrity** | Genuine traversal of `BlockTableRecord.ModelSpace` | Real `foreach (ObjectId id in ms)` loop opening objects with `OpenMode.ForRead` and type matching | **PASS** |
| 6 | **Python Bounding Box Computation** | Dynamic bounding box aggregation from entity coordinates | `min(xs)`, `max(xs)`, `min(ys)`, `max(ys)` calculated across all entities; no hardcoded dimensions | **PASS** |
| 7 | **Build & Compilation Compliance** | `BuildProject.ps1` compiles with 0 errors on .NET 10 | Compiled `Civil3D_Tools.dll` with 0 warnings, 0 errors in 9.5s; Python compiled cleanly | **PASS** |
| 8 | **Live Endpoint Verification** | Live query against open drawings matches real CAD data | Tested live `/inspect` on `C3D-GT-2024.dwg` (5 parcels) and `DT dao nen.dwg` (13 parcels) with accurate coordinates | **PASS** |
| 9 | **Error Surfacing Integrity** | Proper HTTP 404 and diagnostic JSON returned on invalid doc | Requesting non-existent drawing returns 404 with list of currently open drawings | **PASS** |

---

## 3. Detailed Forensic Evidence

### A. Static Code Analysis: C# In-Process Bridge (`38.AT_KhoiDongMcpBridge.cs`)

#### 1. Civil Parcel Property Extraction (`InspectModelSpace` lines 3086–3163)
```csharp
else if (ent is CivilParcel parcel)
{
    double areaVal = SafeGet(() => Math.Round(parcel.Area, 4), 0.0);
    double perimVal = GetParcelPerimeter(parcel);
    string pName = SafeGet(() => parcel.Name, "");
    int pNum = SafeGet(() => (int)parcel.Number, 0);
    string styleName = SafeGet(() => parcel.StyleName, "");
    string styleIdStr = SafeGet(() => parcel.StyleId.Handle.ToString(), "");
    string taxIdStr = SafeGet(() => parcel.TaxId.ToString(), "");
...
    Extents3d ext = parcel.GeometricExtents;
    minPt = new[] { Math.Round(ext.MinPoint.X, 4), Math.Round(ext.MinPoint.Y, 4), Math.Round(ext.MinPoint.Z, 4) };
    maxPt = new[] { Math.Round(ext.MaxPoint.X, 4), Math.Round(ext.MaxPoint.Y, 4), Math.Round(ext.MaxPoint.Z, 4) };
    centroid = new[] { Math.Round(parcel.Centroid.X, 4), Math.Round(parcel.Centroid.Y, 4) };
    bbox = new[] { minPt[0], minPt[1], maxPt[0], maxPt[1] };
```
- **Evidence**: `parcel.Area` is queried directly from the Civil 3D managed object.
- **Evidence**: `GetParcelPerimeter(parcel)` calculates perimeter via `parcel.BaseCurve.GetDistanceAtParameter(...)` or `GeometricExtents` perimeter fallback.
- **Evidence**: Site information is obtained through dynamic map `parcelSiteMap` pre-indexed from `CivilDocument.GetCivilDocument(db).GetSiteIds()`.
- **Evidence**: No fixed values or hardcoded parcel lists exist.

#### 2. ModelSpace Iteration (`InspectModelSpace` lines 2917–3329)
- Iterates `foreach (ObjectId id in ms)` within an active `Transaction`.
- Handles `Line`, `Polyline`, `Circle`, `Arc`, `RotatedDimension`, `AlignedDimension`, `DBText`, `MText`, `ATable`, `CivilParcel`, `ParcelSegment`, `Alignment`, `CivSurface`, `Corridor`, and `BlockReference`.
- Dynamically accumulates summary counts in `countsByType`.

### B. Static Code Analysis: Python MCP Server (`autocad_mcp_server.py`)

#### 1. Bounding Box Calculation (lines 800–875 & 2120–2190)
- Accurately checks for `bounding_box` in `TABLE`, `CIVIL_PARCEL`, `CIVIL_PARCEL_SEGMENT`, `CIVIL_ALIGNMENT`, `CIVIL_SURFACE`, `CIVIL_CORRIDOR`, and `BLOCK_REFERENCE`.
- Falls back to `position + width/height` or `centroid` or `p1/p2` if `bounding_box` is missing.
- Computes `min_x = min(xs)`, `max_x = max(xs)`, `min_y = min(ys)`, `max_y = max(ys)`.
- No mock extents or static rectangles found.

#### 2. HTTP Error Surfacing (lines 163–170)
```python
except urllib.error.HTTPError as e:
    try:
        err_data = e.read().decode("utf-8")
        if err_data:
            return json.loads(err_data)
    except Exception:
        pass
```
- Catches HTTP error responses and parses the structured error JSON sent by the C# bridge.

---

## 4. Empirical Behavioral Verification

### A. Solution Build Verification
- Command: `powershell -ExecutionPolicy Bypass -NoProfile -File "c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\BuildProject.ps1"`
- Output:
  ```
  Restored C:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\MyFirstProject\MyFirstProject.csproj (in 237 ms).
  MyFirstProject -> C:\CadBuild\Autocad2026_API\MyFirstProject\bin\Debug\Civil3D_Tools_qfcorwfgcw1.dll
  Build succeeded.
      0 Warning(s)
      0 Error(s)
  Time Elapsed 00:00:09.50
  ```
- Result: **0 Warnings, 0 Errors**.

### B. Live AutoCAD Bridge Verification (`C3D-GT-2024.dwg`)
- Query: `http://127.0.0.1:18888/inspect?document_name=C3D-GT-2024.dwg`
- Live Output:
  - `status`: `"success"`
  - `count`: 24 entities
  - `counts_by_type`:
    - `CIVIL_ALIGNMENT`: 1
    - `CIVIL_SURFACE`: 3
    - `CIVIL_CORRIDOR`: 1
    - `CIVIL_PARCEL_SEGMENT`: 5
    - `CIVIL_PARCEL`: 5
    - `BLOCK_REFERENCE`: 3
    - `LWPOLYLINE`: 4
    - `TABLE`: 2
  - Extracted Parcels:
    - `8ED5`: Name `Sg_2`, Area `124.6977 m2`, Perimeter `46.5206 m`, Site `Site 1`, Style `S_VuotBTN`
    - `8F2B`: Name `Svn_2`, Area `297.726 m2`, Perimeter `85.0376 m`, Site `Site 1`, Style `S_VuotBTN`
    - `8F35`: Name `Svn_1`, Area `342.7172 m2`, Perimeter `97.4321 m`, Site `Site 1`, Style `S_VuotBTN`
    - `9723`: Name `S_VuotBTN : 8`, Area `182.991 m2`, Perimeter `64.7902 m`, Site `Site 1`, Style `S_VuotBTN`
    - `9732`: Name `S_VuotBTN : 9`, Area `227.3406 m2`, Perimeter `70.67 m`, Site `Site 1`, Style `S_VuotBTN`

### C. Live AutoCAD Bridge Verification (`DT dao nen.dwg`)
- Query: `http://127.0.0.1:18888/inspect?document_name=DT%20dao%20nen.dwg`
- Live Output:
  - `status`: `"success"`
  - `count`: 213 entities
  - `counts_by_type`: `{'LWPOLYLINE': 20, 'BLOCK_REFERENCE': 2, 'CIVIL_PARCEL_SEGMENT': 14, 'CIVIL_PARCEL': 13, 'LINE': 85, 'TEXT': 79}`
  - 13 Civil Parcels detected and extracted with real coordinates (e.g., `33F1D` area `72.09 m2`, `33F31` area `4458.29 m2`, `344C9` area `1540.12 m2`).

### D. Error Surfacing Verification
- Query: `document_name='DrawingNotExist.dwg'`
- Live Output:
  - HTTP Status: `404`
  - Response: `{"status":"error","message":"Không tìm thấy bản vẽ 'DrawingNotExist.dwg' trong số các bản vẽ đang mở. Danh sách bản vẽ hiện có: [BD-TD-VX.dwg, XREF-MAT DUONG LAM MOI - TAN DUNG.dwg, DT dao nen.dwg, C3D-GT-2024.dwg]"}`

---

## 5. Binary Forensic Verdict

**VERDICT: CLEAN**

The Milestone 2 work product is completely genuine, robust, correctly engineered, and free of any shortcuts, dummy facades, or hardcoded fabrications.

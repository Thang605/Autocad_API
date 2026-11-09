# Handoff Report — Forensic Integrity Audit (Milestone 2)

**Auditor**: Forensic Auditor M2_1  
**Target**: Milestone 2: Comprehensive Civil 3D Entity & Parcel Inspection (R3)  
**Date**: 2026-11-09T09:47:00Z  
**Verdict**: **CLEAN**

---

## 1. Observation

1. **C# Code Inspection (`38.AT_KhoiDongMcpBridge.cs`)**:
   - `InspectModelSpace` (lines 2875–3330):
     - Opens `BlockTableRecord.ModelSpace` via `Transaction tr`.
     - Maps Parcels to Sites dynamically by querying `CivilDocument.GetCivilDocument(db).GetSiteIds()` -> `Site.GetParcelIds()` (lines 2897–2915).
     - Genuinely identifies and extracts properties for:
       - `Autodesk.AutoCAD.DatabaseServices.Table` (lines 3056–3085)
       - `Autodesk.Civil.DatabaseServices.Parcel` (lines 3086–3163): queries `parcel.Area`, `GetParcelPerimeter(parcel)`, `parcel.GeometricExtents`, `parcel.Centroid`, `parcel.Name`, `parcel.Number`, `parcel.TaxId`, `parcel.StyleName`, and `parcel.StyleId`.
       - `Autodesk.Civil.DatabaseServices.ParcelSegment` (lines 3164–3206): extracts `ps.StartPoint.DistanceTo(ps.EndPoint)`, `segment_type`, `GeometricExtents`.
       - `Autodesk.Civil.DatabaseServices.Alignment` (lines 3207–3235): extracts `Length`, `StyleName`, `GeometricExtents`, `start_point`, `end_point`.
       - `Autodesk.Civil.DatabaseServices.Surface` (lines 3236–3259): identifies TIN/Grid/Volume surfaces, `StyleName`, `GeometricExtents`.
       - `Autodesk.Civil.DatabaseServices.Corridor` (lines 3260–3281): extracts `Name`, `StyleName`, `GeometricExtents`.
       - `Autodesk.AutoCAD.DatabaseServices.BlockReference` (lines 3282–3318): resolves dynamic blocks via `DynamicBlockTableRecord`, extracts position and bounds.
     - Aggregates counts per type in `counts_by_type = countsByType` (line 3327).
   - `InspectAllCivil3DData` (lines 1258–1353):
     - Traverses sites and parcels, extracting `parcel.Area`, `GetParcelPerimeter(parcel)`, `TaxId`, `StyleId`, `GeometricExtents`, and `Centroid`.
   - `GetParcelPerimeter` (lines 2244–2262):
     - Calculates perimeter via `parcel.BaseCurve.GetDistanceAtParameter(...)` with fallback to `GeometricExtents` perimeter.
   - Zero hardcoded mock values, fixed counts, or dummy strings found (grep for `124.697|8ED5|Sg_2|S_VuotBTN|46.5206` returned 0 matches in source files).

2. **Python Code Inspection (`autocad_mcp_server.py`)**:
   - `_post_http_bridge` (lines 163–170):
     - Catches `urllib.error.HTTPError as e`, reads `e.read().decode('utf-8')`, and parses JSON error payloads.
   - `_render_entities_to_png` (lines 800–875, 1009–1072) & `autocad_inspect_drawing` (lines 2120–2190):
     - Dynamically extracts bounds from `bounding_box`, `position`, `centroid`, or segment points for all entity types.
     - Computes bounding box using `min(xs)`, `max(xs)`, `min(ys)`, `max(ys)`.
     - Renders visual geometries and text labels for `TABLE`, `CIVIL_PARCEL`, `CIVIL_PARCEL_SEGMENT`, `CIVIL_ALIGNMENT`, `CIVIL_SURFACE`, `CIVIL_CORRIDOR`, and `BLOCK_REFERENCE`.

3. **Compilation Build Verification**:
   - Executed `BuildProject.ps1`:
     `powershell -ExecutionPolicy Bypass -NoProfile -File "c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\BuildProject.ps1"`
     Result: `Build succeeded. 0 Warning(s), 0 Error(s). Time Elapsed 00:00:09.50`.
   - Executed `python -m py_compile "C:\Dropbox\0.AI AGENT\11.Autocad Drawing\autocad_mcp_server.py"`: exit code 0.

4. **Live AutoCAD Empirical Verification**:
   - Live query `http://127.0.0.1:18888/inspect?document_name=C3D-GT-2024.dwg` returned:
     - 24 total entities (`CIVIL_ALIGNMENT`: 1, `CIVIL_SURFACE`: 3, `CIVIL_CORRIDOR`: 1, `CIVIL_PARCEL_SEGMENT`: 5, `CIVIL_PARCEL`: 5, `BLOCK_REFERENCE`: 3, `LWPOLYLINE`: 4, `TABLE`: 2).
     - 5 Parcels extracted with exact live parameters:
       - `8ED5`: `Sg_2`, Area `124.6977 m2`, Perimeter `46.5206 m`, Site `Site 1`, Style `S_VuotBTN`
       - `8F2B`: `Svn_2`, Area `297.726 m2`, Perimeter `85.0376 m`, Site `Site 1`, Style `S_VuotBTN`
       - `8F35`: `Svn_1`, Area `342.7172 m2`, Perimeter `97.4321 m`, Site `Site 1`, Style `S_VuotBTN`
       - `9723`: `S_VuotBTN : 8`, Area `182.991 m2`, Perimeter `64.7902 m`, Site `Site 1`, Style `S_VuotBTN`
       - `9732`: `S_VuotBTN : 9`, Area `227.3406 m2`, Perimeter `70.67 m`, Site `Site 1`, Style `S_VuotBTN`
   - Live query `http://127.0.0.1:18888/inspect?document_name=DT%20dao%20nen.dwg` returned:
     - 213 total entities, 13 Civil Parcels extracted (e.g. `33F1D`, `33F31`, `344C9`, `34602`) with actual VN-2000 coordinates.
   - Non-existent drawing query returned HTTP 404 with structured JSON listing open documents.

---

## 2. Logic Chain

1. **Hypothesis 1: Were test results hardcoded or pre-baked?**
   - Direct grep for sample values (`124.697`, `8ED5`, `Sg_2`, `S_VuotBTN`, `46.5206`) returned 0 occurrences in source files.
   - Testing on two different drawings (`C3D-GT-2024.dwg` with 5 parcels vs `DT dao nen.dwg` with 13 parcels) yielded distinct, dynamically computed results matching the drawings' actual content.
   - Therefore, no hardcoded results exist (Observation 1, 4).

2. **Hypothesis 2: Are entity inspections facades or genuine API calls?**
   - Code inspects `BlockTableRecord.ModelSpace` via `Transaction tr` and calls native Civil 3D managed properties (`parcel.Area`, `parcel.BaseCurve`, `parcel.GeometricExtents`, `parcel.Centroid`, `Site.GetParcelIds()`).
   - Results show exact floating-point geometry and VN-2000 coordinates (`X ~ 605000, Y ~ 1385000`).
   - Therefore, the implementation is genuine and not a facade (Observation 1, 4).

3. **Hypothesis 3: Is Python bounding box calculation authentic?**
   - Python code dynamically accumulates coordinate extrema across all returned entities and computes `min(xs)`, `max(xs)`, `min(ys)`, `max(ys)`.
   - Tool execution returned dynamic bounds `min_x: -156.1538, min_y: -290.003, max_x: 1318.1326, max_y: 370.2187`.
   - Therefore, bounding box calculation is authentic (Observation 2, 4).

4. **Hypothesis 4: Does the solution compile and run without regression?**
   - `BuildProject.ps1` completed with 0 errors and 0 warnings.
   - Live endpoints respond correctly on the AutoCAD UI thread with document lock safety.
   - Therefore, compilation and operational readiness are verified (Observation 3, 4).

---

## 3. Caveats

- Live verification was executed with AutoCAD 2026 / Civil 3D 2026 running locally on Windows. In headless CI environments without AutoCAD installed, build tests require mock Autodesk reference assemblies or `EnableWindowsTargeting=true` as configured in `Directory.Build.props`.
- No caveats regarding code integrity or compliance.

---

## 4. Conclusion

**Verdict**: **CLEAN**

The Milestone 2 implementation satisfies all integrity checks under Development Mode. It contains no hardcoded outputs, dummy facades, or fabricated responses. The C# bridge and Python MCP server correctly query live Civil 3D objects, perform genuine geometric computations, and provide complete diagnostic error surfacing. Milestone 2 is officially verified and accepted.

---

## 5. Verification Method

To independently reproduce this verification:
1. **Compilation Check**:
   ```powershell
   powershell -ExecutionPolicy Bypass -NoProfile -File "c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\BuildProject.ps1"
   ```
   *Expected: Build succeeded. 0 Warning(s), 0 Error(s).*

2. **Python Syntax Check**:
   ```powershell
   python -m py_compile "C:\Dropbox\0.AI AGENT\11.Autocad Drawing\autocad_mcp_server.py"
   ```
   *Expected: Exit code 0.*

3. **Live Entity Inspection Query**:
   ```powershell
   python -c "import urllib.request, json; print(json.loads(urllib.request.urlopen('http://127.0.0.1:18888/inspect?document_name=C3D-GT-2024.dwg').read().decode('utf-8'))['counts_by_type'])"
   ```
   *Expected: `{'CIVIL_ALIGNMENT': 1, 'CIVIL_SURFACE': 3, 'CIVIL_CORRIDOR': 1, 'CIVIL_PARCEL_SEGMENT': 5, 'CIVIL_PARCEL': 5, 'BLOCK_REFERENCE': 3, 'LWPOLYLINE': 4, 'TABLE': 2}`*

4. **Invalidation Conditions**:
   - Any static constant array matching test outputs inserted into C# or Python source.
   - Any failure of `BuildProject.ps1`.
   - Any mock or hardcoded returns from `/inspect` or `/civil`.

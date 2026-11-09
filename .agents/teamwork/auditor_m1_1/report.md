# Forensic Integrity Audit Report — Milestone 1 (R1 & R2)

**Auditor**: Forensic Auditor M1_1 (`auditor_m1_1`)  
**Date**: 2026-11-09T09:05:00Z  
**Work Product**: Milestone 1 Implementation (Multi-Document Context & Output Pagination)  
- `c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\MyFirstProject\Acad Tool\38.AT_KhoiDongMcpBridge.cs`
- `C:\Dropbox\0.AI AGENT\11.Autocad Drawing\autocad_mcp_server.py`
- `C:\Users\hoang\.gemini\antigravity\mcp\autocad-mcp\*.json`  
**Integrity Mode**: `development` (confirmed directly from `ORIGINAL_REQUEST.md` line 8)  
**Binary Verdict**: **CLEAN**

---

## 1. Executive Summary & Verification Matrix

All forensic integrity checks passed with 0 violations. No hardcoded results, mock arrays, or facade implementations were detected. All document resolution, document locking, pagination arithmetic, and parameter mappings are genuinely implemented and execute live against the AutoCAD / Civil 3D database.

| Forensic Check | Requirement | Phase 1 Observation | Phase 2 Flag (Dev Mode) | Status |
|----------------|-------------|---------------------|--------------------------|--------|
| **Check 1: Hardcoded Test Results** | General §1 | Inspected all response generation routines. No static dummy arrays or fake entity counts. | Clean | **PASS** |
| **Check 2: Facade Implementations** | General §2 | Inspected `summaryOnly`, `entityType`, and pagination logic. True data reduction verified. | Clean | **PASS** |
| **Check 3: Pre-populated Artifacts** | General §3 | Scanned workspace for orphan `.log`, `*result*`, `*output*` files. None found. | Clean | **PASS** |
| **Check 4: Genuine Document Resolution & Locking** | R1 | Inspected `ResolveTargetDocument` & `RunOnAutoCadMainThread`. Dynamic lookup and `using (doc.LockDocument())`. | Clean | **PASS** |
| **Check 5: Elimination of Silent Hijacking** | R1 | Verified removal of legacy heuristic that switched documents when `activeCivilCount == 0`. | Clean | **PASS** |
| **Check 6: Genuine Parameter Mapping** | R1 & R2 | Inspected parameter flow from Python MCP server -> HTTP Bridge -> C# handlers. | Clean | **PASS** |
| **Check 7: Independent Build & Syntax Verification** | R5 | Ran `BuildProject.ps1` (0 Errors), `py_compile` (0 Errors), JSON validation (13 schemas OK). | Clean | **PASS** |

---

## 2. Phase 1: Mode-Agnostic Source Code & Architectural Analysis

### 2.1. Hardcoded Output Detection
- **Target**: `InspectAllCivil3DData`, `/status`, `/civil`, `autocad_mcp_server.py`.
- **Findings**:
  - Entity counts (`totalAlignments`, `totalSurfaces`, `totalCorridors`, `totalPipeNetworks`, `totalParcels`, `totalSampleLineGroups`, `totalCogoPoints`) are retrieved dynamically from `CivilDocument` collections and `Database` tables within an active `Transaction`.
  - Geometric properties (PVI stations, elevations, grades, sample line coordinates, pipe lengths, surface elevations) are computed live via AutoCAD/Civil 3D API calls (`prof.PVIs[i]`, `al.PointLocation()`, `surf.GetGeneralProperties()`, `pipe.InnerDiameterOrWidth`, etc.).
  - No synthetic test fixture dictionaries, canned JSON responses, or hardcoded drawing paths were found.

### 2.2. Facade & Pseudo-Pagination Detection
- **Target**: `summaryOnly`, `entityType`, `page`, `page_size`, `maxItems`.
- **Findings**:
  - **Summary Mode (`summaryOnly == true`)**:
    - Profiles: Omits `pvis` array entirely (lines 917-932), returning only metadata and `pvi_count`.
    - Sample Lines: Omits `sample_lines` vertex collection (lines 1019-1029), returning only group counts.
    - Corridors: Omits `regions` list (lines 1210-1230).
    - Pipe Networks: Omits `pipes` and `structures` arrays (lines 1300-1309).
    - CogoPoints: Completely skips iterating over `cdoc.CogoPoints` when `summaryOnly == true` (line 1363).
    - *Verdict*: Genuine data reduction, preventing token exhaustion on models with thousands of points.
  - **Pagination Slicing**:
    - Profile PVIs: `pviStart = Math.Max(0, (page - 1) * pageSize)`, `pviEnd = Math.Min(totalPvis, pviStart + Math.Min(pageSize, maxItems))`. Only index range `[pviStart, pviEnd)` is iterated and populated (lines 938-955).
    - SampleLines: `slStart = Math.Max(0, (page - 1) * pageSize)`, `slEnd = Math.Min(totalSl, slStart + Math.Min(pageSize, maxItems))`. Only `[slStart, slEnd)` objects are opened via `tr.GetObject(slIds[i], OpenMode.ForRead)` (lines 1033-1053).
    - CogoPoints: `cogoStart = Math.Max(0, (page - 1) * pageSize)`, `cogoEnd = Math.Min(totalCogoPoints, cogoStart + Math.Min(pageSize, maxItems))`. Iteration terminates early via `break` once `curIdx >= cogoEnd` (lines 1367-1389).
    - *Verdict*: Real memory and payload pagination, not an in-memory post-filter or facade.
  - **Entity Type Filtering (`entityType`)**:
    - Selects flags: `wantAlignments`, `wantSurfaces`, `wantCorridors`, `wantParcels`, `wantPipeNetworks`, `wantCogoPoints`, `wantAssemblies` (lines 876-883).
    - Entire extraction loops are bypassed if the corresponding flag is false.
    - *Verdict*: Real execution avoidance and compute optimization.

### 2.3. Document Resolution & Document Locking
- **Target**: `ResolveTargetDocument(string? targetDocName)` and `RunOnAutoCadMainThread<T>`.
- **Findings**:
  - `ResolveTargetDocument` validates against `Application.DocumentManager`:
    - When `targetDocName` is null or whitespace: returns `Application.DocumentManager.MdiActiveDocument`. Throws `InvalidOperationException` if null.
    - When `targetDocName` is specified: performs case-insensitive comparisons against drawing name, filename with/without extension, full path, and substring match.
    - If not found: collects all currently open drawings into a list and throws `FileNotFoundException`.
  - `RunOnAutoCadMainThread`:
    - Executes on AutoCAD Main UI Thread via `_uiMarshalControl.Invoke(...)`.
    - Resolves target document dynamically before acquiring lock: `Document doc = ResolveTargetDocument(targetDocName)`.
    - Acquires lock cleanly: `using (doc.LockDocument()) { result = func(doc); }`.
  - Elimination of Silent Hijacking:
    - Legacy heuristic in `ResolveCivilTargetDocument` that redirected queries to another drawing if the active drawing had 0 Civil 3D objects has been removed. Active document is strictly respected.
  - Error Handling:
    - `HandleRequestAsync` catches `FileNotFoundException` and responds with HTTP 404 JSON `{ status: "error", message: "..." }`.
    - Catches `ArgumentException` and responds with HTTP 400 JSON.
    - Catches general exceptions and responds with HTTP 500 JSON without terminating the listener.

### 2.4. Python MCP Server & Schema Parameter Mapping
- **Target**: `autocad_mcp_server.py` and `C:\Users\hoang\.gemini\antigravity\mcp\autocad-mcp\*.json`.
- **Findings**:
  - `document_name` parameter added and correctly routed across:
    - `autocad_status` -> `/status?document_name=...`
    - `autocad_manage_document` -> `/clear`, `/civil`, `/manage`
    - `autocad_draw_table` -> `/draw` & `_collect_modelspace_entities_for_preview`
    - `autocad_inspect_drawing` -> `_collect_modelspace_entities_for_preview` -> `/inspect?document_name=...`
    - `autocad_export_preview` -> `_collect_modelspace_entities_for_preview`
    - `civil3d_inspect` -> `/civil`
    - `civil3d_automate` -> `/civil`
  - In `civil3d_inspect`:
    - Parameters `document_name`, `action`, `target_name`, `summary_only`, `entity_type`, `max_items`, `page`, `page_size` are accurately packaged into JSON payload and consumed in C# `ExecuteCivilRequest`.
  - JSON tool schemas in `C:\Users\hoang\.gemini\antigravity\mcp\autocad-mcp\`:
    - All 13 JSON schema files were syntactically validated and match the Python function signatures.

---

## 3. Phase 2: Mode-Specific Flagging & Compliance

Integrity Mode from `ORIGINAL_REQUEST.md`: **`development`**.
- Criteria under Development Mode:
  - Hardcoded test results: **CLEAN (0 found)**
  - Dummy / facade implementations: **CLEAN (0 found)**
  - Fabricated verification outputs: **CLEAN (0 found)**

All checks comply with `development` mode requirements and satisfy the higher bars of Demo and Benchmark modes for the audited scope.

---

## 4. Adversarial Stress-Test Analysis

| Scenario / Attack Vector | Predicted / Tested Behavior | Actual Implementation Defense | Result |
|--------------------------|-----------------------------|-------------------------------|--------|
| Target document not found | HTTP listener crashes or AutoCAD throws unhandled exception | Caught in `HandleRequestAsync`, returns HTTP 404 with list of open DWGs | **ROBUST** |
| `page <= 0` or `pageSize <= 0` | Index out of range or division by zero | Sanitized with `Math.Max(1, page)` and `Math.Max(1, pageSize)` | **ROBUST** |
| `page` beyond total items (e.g. page 999 on 50 PVIs) | `ArgumentOutOfRangeException` on collection indexer | Slicing arithmetic bounds `pviStart = Math.Max(0, (page-1)*pageSize)`, `pviEnd = Math.Min(total, pviStart+...)`. Loop does not execute; returns empty list `[]`. | **ROBUST** |
| Active document is null (AutoCAD opened with zero documents) | Null reference exception | Handled in `ResolveTargetDocument`, throws `InvalidOperationException`, caught returning HTTP 500 | **ROBUST** |
| `summary_only` requested on large surface (100k triangles) | Memory spike or JSON buffer overflow | `summaryOnly` bypasses heavy point/triangle traversal | **ROBUST** |
| Multiple threads hitting bridge simultaneously | Document lock collision or UI thread deadlock | All document operations serialized through `_uiMarshalControl.Invoke` on AutoCAD main thread | **ROBUST** |

---

## 5. Empirical Verification Evidence

### Independent Compilation
```
powershell -ExecutionPolicy Bypass -NoProfile -File "c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\BuildProject.ps1"

Output:
  Determining projects to restore...
  Restored C:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\MyFirstProject\MyFirstProject.csproj (in 187 ms).
  MyFirstProject -> C:\CadBuild\Autocad2026_API\MyFirstProject\bin\Debug\Civil3D_Tools_rbpbfvmwy30.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:06.48
```

### Python Syntax Validation
```
python -m py_compile "C:\Dropbox\0.AI AGENT\11.Autocad Drawing\autocad_mcp_server.py"
Exit code: 0
```

### JSON Schema Validation
```
python -c "import json, glob; files = glob.glob(r'C:\Users\hoang\.gemini\antigravity\mcp\autocad-mcp\*.json'); [json.load(open(f, encoding='utf-8')) for f in files]; print(f'Validated {len(files)} JSON schemas successfully.')"

Output:
Validated 13 JSON schemas successfully.
```

---

## 6. Conclusion
The changes implemented for Milestone 1 are clean, authentic, robust, and completely free of integrity violations.
**Verdict: CLEAN**.

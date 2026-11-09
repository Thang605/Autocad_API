# Handoff Report — Explorer Survey 1 (C# Bridge Explorer)

**Role**: Explorer Survey 1 (C# Bridge Explorer)  
**Date**: 2026-11-09  
**Type**: Hard Handoff (Investigation Complete)  
**Working Directory**: `c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\.agents\teamwork\explorer_survey_1`  
**Primary Artifact**: `c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\.agents\teamwork\explorer_survey_1\report.md`  

---

## 1. Observation

### 1.1 IPC Architecture
- In `c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\MyFirstProject\Acad Tool\38.AT_KhoiDongMcpBridge.cs`:
  - Lines 48-51: `HttpListener? _listener; Control? _uiMarshalControl; int _activePort = 18888;`
  - Lines 114-118: `_listener = new HttpListener(); _listener.Prefixes.Add($"http://127.0.0.1:{port}/"); _listener.Start();`
  - Lines 107-111: `_uiMarshalControl` is initialized on AutoCAD's Main UI thread.
  - Lines 153-186: `RunOnAutoCadMainThread<T>` calls `_uiMarshalControl.Invoke(...)`, acquires `Document? doc = Application.DocumentManager.MdiActiveDocument`, locks it via `using (doc.LockDocument())`, and runs `func(doc)`.
  - In `C:\Dropbox\0.AI AGENT\11.Autocad Drawing\autocad_mcp_server.py`:
    - Lines 56: `BRIDGE_URL = os.environ.get("AUTOCAD_MCP_BRIDGE_URL", "http://127.0.0.1:18888")`
    - Lines 143-159: `_post_http_bridge` sends HTTP POST requests directly to `http://127.0.0.1:18888`.

### 1.2 Document Context & R1 Multi-Document Handling
- In `38.AT_KhoiDongMcpBridge.cs`:
  - Lines 650-661: `ResolveCivilTargetDocument` searches `Application.DocumentManager` by `document_name`.
  - Lines 665-684: When `document_name` is empty and `activeCivilCount == 0`, it iterates `Application.DocumentManager` and switches away to another open document that has civil entities:
    ```csharp
    if (activeCivilCount == 0)
    {
        foreach (Document d in Application.DocumentManager)
        {
            if (d == activeDoc) continue;
            try
            {
                CivilDocument cdoc = CivilDocument.GetCivilDocument(d.Database);
                int cnt = cdoc.GetAlignmentIds().Count + cdoc.GetSurfaceIds().Count + cdoc.CorridorCollection.Count;
                if (cnt > 0) return d;
            }
            catch { }
        }
    }
    ```
  - Lines 337-342: `/inspect` directly calls `RunOnAutoCadMainThread(doc => InspectModelSpace(doc))` with no document filter.
  - In `autocad_mcp_server.py`: Lines 2076-2080: `civil3d_inspect(action, target_name, max_items)` accepts no `drawing_name` parameter.

### 1.3 Inspection Commands & Missing Entities
- In `38.AT_KhoiDongMcpBridge.cs`:
  - Lines 2569-2730: `InspectModelSpace` only scans for `Line`, `Polyline`, `Circle`, `Arc`, `RotatedDimension`, `AlignedDimension`, `DBText`, `MText`.
  - Native `Table` (`Autodesk.AutoCAD.DatabaseServices.Table`) is NOT scanned.
  - Civil 3D entities (`CivilParcel`, `ParcelSegment`, `Alignment`, `CivSurface`, `Corridor`, `SampleLine`, `CogoPoint`) in ModelSpace are NOT scanned.
  - In `autocad_mcp_server.py`: Line 1958 calls `_collect_modelspace_entities_for_preview()`, which queries `/inspect` (line 666). All Civil 3D entities and Tables return 0 in `autocad_inspect_drawing`.

### 1.4 Civil 3D Entity Queries & Token Consumption
- In `38.AT_KhoiDongMcpBridge.cs`:
  - Lines 785-800: In `InspectAllCivil3DData`, each profile dumps up to 100 PVIs.
  - Lines 867-886: Each SampleLineGroup dumps up to `maxItems` (default 500) sample lines with full coordinates.
  - Lines 1074-1098: Parcels only return `site_name, name, number, handle, layer, style, area_m2, description`.
    - No geometry (no bounding box, centroid, or polygon vertices).
    - `area_m2` uses `parcel.Area` instead of `parcel.Statistics.Area`.
    - `ParcelSegment` entities are not queried at all.
    - No `summary_only` flag or field filtering exists.

### 1.5 Table Drawing (`autocad_draw_table`)
- In `autocad_mcp_server.py`:
  - Lines 1357-1432: `autocad_draw_table` decomposes the table into rectangle shapes, line shapes, and individual `text` annotations.
  - Lines 1420-1430: For blank cells, it creates `annots.append({ "type": "text", "text": "", ... })`.
- In `38.AT_KhoiDongMcpBridge.cs`:
  - Lines 2401-2436: `CreateAnnotationEntity` creates a `DBText` for every `type == "text"`, even when `txtVal == ""` (lines 2404, 2410-2416). It appends empty text entities to ModelSpace.
  - Lines 2048-2125: C# already implements native `ATable` creation in `DrawStakeTableInModelSpace`.
  - In `Civil Tool 2\CTPA_BangThongKeParcel.cs`: Lines 468-547 (`CreateParcelTable`) also implement native `ATable` creation with title merge, column widths, and cell alignment.

### 1.6 Build Verification
- Running `powershell -ExecutionPolicy Bypass -NoProfile -File "c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\BuildProject.ps1"`:
  ```
  Build succeeded.
      0 Warning(s)
      0 Error(s)
  Time Elapsed 00:00:10.77
  ```
  Project compiled cleanly with 0 errors on .NET 10.

---

## 2. Logic Chain

1. **IPC Chain**:
   - `HttpListener` on port 18888 (Obs 1.1) handles JSON over HTTP from Python `autocad_mcp_server.py` (Obs 1.1).
   - Because AutoCAD requires Main UI thread execution, `RunOnAutoCadMainThread` uses `_uiMarshalControl.Invoke` and locks the document before calling transactions (Obs 1.1).
   - Therefore, the IPC architecture is sound and fast (< 5ms), but its document resolution must be made flexible.

2. **Document Context Chain (R1)**:
   - `RunOnAutoCadMainThread` locks `MdiActiveDocument` by default (Obs 1.1).
   - In `/civil`, `ResolveCivilTargetDocument` silently redirects to another open document when `activeCivilCount == 0` (Obs 1.2).
   - This violates user expectation when working in a new or non-alignment drawing.
   - Removing this fallback and resolving document target by parameter across all endpoints (`/civil`, `/inspect`, `/draw`, `/manage`, `/clear`) will fully satisfy R1.

3. **Inspection Chain (R2 & R3)**:
   - `/inspect` only iterates basic 2D CAD entities (Obs 1.3).
   - Adding `ATable`, `CivilParcel`, `ParcelSegment`, `Alignment`, `CivSurface`, `Corridor`, and `SampleLine` to `InspectModelSpace` will allow `autocad_inspect_drawing` and preview bounding box calculation to see all entities in ModelSpace (Obs 1.3).
   - Adding `summary_only` mode and field filtering to `InspectAllCivil3DData` will eliminate massive token dumps (Obs 1.4), satisfying R2.
   - Extracting `parcel.Statistics.Area`, bounding box, and `ParcelSegment` entities will satisfy R3 (Obs 1.4).

4. **Table Creation Chain (R4)**:
   - Python decomposes tables into primitives and emits empty text strings (Obs 1.5).
   - C# appends empty `DBText` entities to ModelSpace (Obs 1.5).
   - C# already contains production-tested native `ATable` creation logic in `DrawStakeTableInModelSpace` and `CreateParcelTable` (Obs 1.5).
   - Introducing a native table drawing handler and guarding `CreateAnnotationEntity` against empty strings will eliminate empty text entities and provide clean `AcDbTable` objects, satisfying R4.

---

## 3. Caveats

1. **AutoCAD Runtime Dependency**: The C# code references proprietary Autodesk DLLs at `C:\Program Files\Autodesk\AutoCAD 2027\`. Builds and tests must only occur on Windows with the local SDK.
2. **CAD_Reloader File Locking**: If AutoCAD is currently open and holding a lock on `Civil3D_Tools.dll`, building via `BuildProject.ps1` handles this using timestamped assembly names (`Civil3D_Tools_<random>.dll`).
3. **Parcel Segment Geometry**: While `parcel.Statistics` and `parcel.GeometricExtents` are straightforward, extracting full parcel loop vertices across complex curve boundaries requires handling `ParcelLoop` segments carefully.

---

## 4. Conclusion

The C# bridge (`AT_McpBridgeServer`) provides a high-performance, robust foundation, but requires targeted enhancements to meet the user's requirements:
1. **R1**: Replace silent document switching with strict active document targeting by default, and allow explicit drawing selection by name across all endpoints.
2. **R2**: Implement a `summary_only` mode, field filtering (`fields`), and pagination for PVIs and sample lines in `InspectAllCivil3DData`.
3. **R3**: Expand `InspectModelSpace` to recognize Tables and Civil 3D entities; enrich Parcel inspection with `Statistics.Area/Perimeter`, centroid, bounding box, and `ParcelSegment` data.
4. **R4**: Implement native `ATable` drawing and guard `CreateAnnotationEntity` against empty `DBText` creation (`string.IsNullOrWhiteSpace(txtVal)`).

---

## 5. Verification Method

1. **Build Verification**:
   Execute:
   ```powershell
   powershell -ExecutionPolicy Bypass -NoProfile -File "c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\BuildProject.ps1"
   ```
   Must output: `0 Error(s), 0 Warning(s)`.
2. **Code Inspection**:
   Inspect `38.AT_KhoiDongMcpBridge.cs` at:
   - `RunOnAutoCadMainThread` (lines 153-186)
   - `ResolveCivilTargetDocument` (lines 648-688)
   - `InspectAllCivil3DData` (lines 755-1227)
   - `CreateAnnotationEntity` (lines 2401-2436)
   - `InspectModelSpace` (lines 2569-2730)
3. **Invalidation Conditions**:
   - If `BuildProject.ps1` fails to compile with 0 errors.
   - If `/inspect` fails to return `TABLE` or Civil 3D entities present in ModelSpace.
   - If empty text entities (`TextString = ""`) are added to ModelSpace during table drawing.

# Comprehensive Investigation Report: AutoCAD & Civil 3D C# Bridge (`AcadMcpBridge`)

**Explorer**: Explorer Survey 1 (C# Bridge Explorer)  
**Date**: 2026-11-09  
**Target Solution**: `c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\MyFirstProject.sln`  
**Primary Source File**: `c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\MyFirstProject\Acad Tool\38.AT_KhoiDongMcpBridge.cs`  
**Supporting Files**:
- `c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\MyFirstProject\Acad Tool\KhoiDongMcpBridgeForm.cs`
- `c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\MyFirstProject\Extensions\Civil3DToolsExtensionApplication.cs`
- `c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\MyFirstProject\Civil Tool 2\CTPA_BangThongKeParcel.cs`
- `C:\Dropbox\0.AI AGENT\11.Autocad Drawing\autocad_mcp_server.py`

---

## Executive Summary

This investigation analyzed the AutoCAD / Civil 3D MCP bridge on the C# side to address the 5 core questions and align the codebase with requirements **R1** (Multi-document context & targeting active by default), **R2** (Token efficiency, pagination & summary modes), **R3** (Comprehensive Civil 3D entity inspection including Parcels, Parcel Segments, Tables, Alignments, Corridors, Surfaces), and **R4** (Native `AcDbTable` creation without extraneous blank text entities).

### Key Takeaways
1. **IPC Mechanism**: An in-process `HttpListener` running inside `acad.exe` at `http://127.0.0.1:18888/` marshals background HTTP requests to AutoCAD's single-threaded UI loop via a hidden WinForms `Control` (`_uiMarshalControl.Invoke`).
2. **Document Context**: Document context currently defaults to `Application.DocumentManager.MdiActiveDocument`, but in `/civil` it contains an anti-pattern: if the active drawing has 0 alignments/surfaces/corridors, it silently hijacks execution and switches to another open document. Moreover, other endpoints (`/inspect`, `/draw`, `/manage`, `/clear`) do not support document targeting at all.
3. **Inspection Tools**: `/inspect` only queries 2D CAD primitives and dimensions, completely ignoring native `Table` entities and all Civil 3D entities. Consequently, `autocad_inspect_drawing` in Python cannot detect or bound Civil 3D objects. Meanwhile, `/civil` dumps entire PVI lists and sample lines without pagination, leading to token exhaustion.
4. **Civil 3D Entity Representation**: Parcels lack geometry (coordinates / bounding box) and area statistics; Parcel Segments and Tables are completely omitted from inspection.
5. **Table Drawing**: `autocad_draw_table` in Python decomposes tables into loose line and rectangle primitives and emits empty text annotations for blank cells. In C#, `CreateAnnotationEntity` creates empty `DBText` entities (`TextString = ""`), cluttering ModelSpace. C# already has proven native `Autodesk.AutoCAD.DatabaseServices.Table` (`ATable`) capability (as seen in `DrawStakeTableInModelSpace` and `CTPA_BangThongKeParcel.cs`), which should be leveraged natively.

---

## 1. IPC & Communication Architecture

### 1.1 In-Process HTTP Server
- **File**: `MyFirstProject/Acad Tool/38.AT_KhoiDongMcpBridge.cs`
- **Class**: `Civil3DCsharp.AT_McpBridgeServer`
- **Listener**: `System.Net.HttpListener` bound to `http://127.0.0.1:18888/` (default port 18888, stored in `KhoiDongMcpBridgeForm.SavedPort`).
- **Lifecycle**:
  - Automatically started during AutoCAD plugin initialization via `Civil3DToolsExtensionApplication.Initialize()` (lines 31 in `Civil3DToolsExtensionApplication.cs`).
  - Can be manually started/stopped/restarted via command `[CommandMethod("AT_Khoi_Dong_Mcp_Bridge")]` (lines 56-75) or LISP expression `(AT_MCP_START)` (lines 77-82).
  - Clean shutdown & AppDomain protection: Uses `AppDomain.CurrentDomain.SetData("Civil3DTools.McpBridge.StopAction", ...)` so reloading DLLs via `CAD_Reloader` (`NETRELOAD`) or `NetReload` (`NRL`) cleanly stops the old HTTP listener before binding the new one, avoiding `HttpListenerException: Failed to listen on prefix...`.

### 1.2 Cross-Thread Synchronization (UI Marshal Control)
- AutoCAD is single-threaded; any modification or reading of `Database`, `TransactionManager`, or `Document` must occur on AutoCAD's Main UI thread.
- `HttpListener.GetContextAsync()` receives requests on ThreadPool threads.
- In `AT_KhoiDongMcpBridge.cs`, lines 107-111 & 153-186:
  ```csharp
  private static Control? _uiMarshalControl;
  ...
  private static T RunOnAutoCadMainThread<T>(Func<Document, T> func)
  {
      if (_uiMarshalControl == null || !_uiMarshalControl.IsHandleCreated)
          throw new InvalidOperationException("UI Marshal Control chưa được khởi tạo trên Main Thread.");

      T result = default!;
      Exception? capturedEx = null;

      _uiMarshalControl.Invoke(new Action(() =>
      {
          try
          {
              Document? doc = Application.DocumentManager.MdiActiveDocument;
              if (doc == null)
                  throw new InvalidOperationException("Không có bản vẽ (MdiActiveDocument) nào đang mở trong AutoCAD.");

              using (doc.LockDocument())
              {
                  result = func(doc);
              }
          }
          catch (Exception ex) { capturedEx = ex; }
      }));

      if (capturedEx != null) throw capturedEx;
      return result;
  }
  ```
- **Strengths**: High performance (< 5ms response time), bypasses UAC restrictions, directly accesses the live AutoCAD .NET object model and Civil 3D runtime.
- **Weaknesses**:
  - Currently hardcoded to `Application.DocumentManager.MdiActiveDocument` inside `RunOnAutoCadMainThread`.
  - Secondary documents require double-locking (see Section 2).

### 1.3 Active Endpoints
| HTTP Method | Route | Purpose | Current Scope |
|---|---|---|---|
| `GET` | `/status` | Connection check, active doc, open docs list, Civil 3D summary, layers | Active doc + list of open docs |
| `POST` | `/civil` | Civil 3D query & automation (inspect, stake_report, rebuild, query_elevation, adjust_profile, adjust_sample_lines) | CivilDocument collections |
| `GET` | `/inspect` | ModelSpace entity dump for preview & inspection | Hardcoded to active doc |
| `POST` | `/draw` | Batch drawing of primitives and annotations | Hardcoded to active doc |
| `POST` | `/clear` | Erases all entities in ModelSpace | Hardcoded to active doc |
| `POST` | `/lisp` | Executes LISP command via `SendStringToExecute` | Hardcoded to active doc |
| `POST` | `/manage` | Document actions (`zoom_extents`, `save`, `save_as`, forwards `civil:*`) | Hardcoded to active doc |
| `POST` | `/reload` | Reloads plugin DLL via `ExtensionLoader.Load` | Process-wide |

---

## 2. Document Context Handling (Active vs. Specific Document) & R1

### 2.1 Current Implementation & Flaws
In `38.AT_KhoiDongMcpBridge.cs`, lines 648-688:
```csharp
private static Document ResolveCivilTargetDocument(Document activeDoc, JsonElement root)
{
    string docFilter = GetStr(root, "document_name", "").Trim();
    if (!string.IsNullOrWhiteSpace(docFilter))
    {
        foreach (Document d in Application.DocumentManager)
        {
            if (d.Name.Contains(docFilter, StringComparison.OrdinalIgnoreCase) ||
                d.Database.Filename.Contains(docFilter, StringComparison.OrdinalIgnoreCase))
            {
                return d;
            }
        }
    }

    try
    {
        CivilDocument activeCdoc = CivilDocument.GetCivilDocument(activeDoc.Database);
        int activeCivilCount = activeCdoc.GetAlignmentIds().Count
                             + activeCdoc.GetSurfaceIds().Count
                             + activeCdoc.CorridorCollection.Count
                             + activeCdoc.GetPipeNetworkIds().Count;
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
    }
    catch { }

    return activeDoc;
}
```

**Critical Issues Identified:**
1. **Silent Document Hijacking**: When `document_name` is NOT provided and the active document has 0 alignments/surfaces/corridors (e.g. user opens a blank drawing, or drawing containing only Parcels, or is inspecting a new file), `ResolveCivilTargetDocument` **silently shifts execution to another open drawing** that happens to have Civil entities. This violates the principle of least astonishment and breaks Requirement R1 ("target active document by default").
2. **Double DocumentLock Anti-Pattern**: Because `RunOnAutoCadMainThread` always locks `MdiActiveDocument`, if `ResolveCivilTargetDocument` switches to `d != activeDoc`, `ExecuteCivilRequest` executes:
   ```csharp
   bool needExtraLock = (doc != activeDoc);
   DocumentLock? extraLock = needExtraLock ? doc.LockDocument() : null;
   ```
   This creates nested/dual document locks across different documents on the main UI thread.
3. **Endpoint Disparity**: `/inspect`, `/draw`, `/manage`, `/clear` do not use `ResolveCivilTargetDocument`. They strictly operate on `MdiActiveDocument`.
4. **Python MCP Server Disconnect**: In `autocad_mcp_server.py`, `civil3d_inspect` definition:
   ```python
   def civil3d_inspect(
       action: str = "inspect_all",
       target_name: Optional[str] = None,
       max_items: int = 500,
   ) -> str:
   ```
   There is no parameter for `drawing_name` or `document_name`!

### 2.2 Architectural Recommendation for R1
1. **Remove Silent Hijacking**:
   When no document filter is supplied, **strictly target `MdiActiveDocument`**. Never redirect to another drawing unless explicitly requested.
2. **Unified Document Resolver**:
   Create a centralized helper `ResolveTargetDocument(string? docNameOrFilter)`:
   - If empty/null: Return `Application.DocumentManager.MdiActiveDocument`.
   - If specified: Search `Application.DocumentManager` by exact filename, filename without extension, partial name, or full file path.
   - If not found: Throw an informative error (`Document '<name>' was not found among open drawings. Open drawings: ...`).
3. **Targeted Document Locking**:
   Refactor `RunOnAutoCadMainThread` to accept `string? targetDocName = null`:
   ```csharp
   private static T RunOnAutoCadMainThread<T>(Func<Document, T> func, string? targetDocName = null)
   {
       ...
       _uiMarshalControl.Invoke(new Action(() =>
       {
           Document doc = ResolveTargetDocument(targetDocName);
           using (doc.LockDocument())
           {
               result = func(doc);
           }
       }));
       ...
   }
   ```
   This provides clean, single-lock targeting for all endpoints.
4. **Support Query Parameters & JSON Body**:
   Accept both `?document_name=...` / `?drawing_name=...` (query string) and `"document_name": "..."` / `"drawing_name": "..."` (JSON body) on `/civil`, `/inspect`, `/draw`, `/manage`, and `/clear`.

---

## 3. Inspection Commands on C# Side & Entity Querying

### 3.1 Existing Inspection Implementations
1. **`GET /status`** (lines 210-287):
   - Computes total entity count in ModelSpace.
   - If `SavedIncludeCivilInStatus` is true, calls `BuildCivil3DSummarySafe` which counts alignments, profiles, profile views, surfaces, corridors, sample line groups, sample lines, section views, sites, parcels, pipe networks, pipes, structures, cogo points, assemblies.
2. **`GET /inspect` -> `InspectModelSpace(Document doc)`** (lines 2569-2730):
   - Scans `BlockTableRecord.ModelSpace` via `Transaction tr`.
   - Recognizes: `Line`, `Polyline` (`LWPOLYLINE`), `Circle`, `Arc`, `RotatedDimension`, `AlignedDimension`, `DBText`, `MText`.
   - Returns `{ status, count, entities: [...] }`.
3. **`POST /civil` -> `InspectAllCivil3DData`** (lines 755-1227):
   - Calls `CivilDocument.GetCivilDocument(db)`.
   - Extracts detailed properties of Alignments, Profiles (with PVIs), Surfaces, Corridors, Parcels, Pipes, CogoPoints, Assemblies.

### 3.2 Gaps in Current Inspection
1. **`InspectModelSpace` is Blind to Tables & Civil 3D Objects**:
   - `InspectModelSpace` does NOT check for `Autodesk.AutoCAD.DatabaseServices.Table` (`ATable`).
   - `InspectModelSpace` does NOT check for any Civil 3D entities (`CivilParcel`, `ParcelSegment`, `Alignment`, `TinSurface`, `Corridor`, `SampleLine`, `CogoPoint`, etc.).
   - In `autocad_mcp_server.py`, `autocad_inspect_drawing` and `_collect_modelspace_entities_for_preview` rely entirely on `/inspect`.
   - Therefore, inspecting a Civil 3D drawing via `autocad_inspect_drawing` returns **0 counts for Civil entities and Tables**, and `autocad_export_preview` cannot calculate the correct bounding box.
2. **ModelSpace Civil 3D Entity Detection**:
   In AutoCAD/Civil 3D, all visible drawing objects in ModelSpace are instances of `Autodesk.AutoCAD.DatabaseServices.Entity`.
   By adding handlers in `InspectModelSpace` for:
   - `ATable table` -> `{ type: "TABLE", layer, color, num_rows, num_cols, width, height, position, title }`
   - `CivilParcel parcel` -> `{ type: "PARCEL", layer, color, name, number, area, bounds }`
   - `Alignment al` -> `{ type: "ALIGNMENT", layer, color, name, length, start_point, end_point }`
   - `CivSurface surf` -> `{ type: "SURFACE", layer, color, name, surface_type, bounds }`
   - `Corridor corr` -> `{ type: "CORRIDOR", layer, color, name }`
   - `SampleLine sl` -> `{ type: "SAMPLE_LINE", layer, color, name, station }`
   The general drawing inspector will immediately recognize all Civil entities and Tables!

---

## 4. Civil 3D Entity Querying & Representation (Parcels, Tables, Alignments, etc.)

### 4.1 Parcels & Parcel Segments Analysis (Requirement R3)
In `38.AT_KhoiDongMcpBridge.cs`, lines 1074-1098:
```csharp
foreach (ObjectId siteId in cdoc.GetSiteIds())
{
    if (siteId.IsNull || siteId.IsErased) continue;
    if (tr.GetObject(siteId, OpenMode.ForWrite) is not Site site) continue;

    foreach (ObjectId pId in site.GetParcelIds())
    {
        if (pId.IsNull || pId.IsErased) continue;
        if (tr.GetObject(pId, OpenMode.ForWrite) is CivilParcel parcel)
        {
            parcels.Add(new
            {
                site_name = site.Name,
                name = parcel.Name,
                number = SafeGet(() => (int)parcel.Number, 0),
                handle = parcel.Handle.ToString(),
                layer = SafeGet(() => parcel.Layer, ""),
                style = SafeGet(() => parcel.StyleName, ""),
                area_m2 = SafeGet(() => Math.Round(parcel.Area, 2), 0.0),
                description = SafeGet(() => parcel.Description, "")
            });
        }
    }
}
```

**Key Gaps & Enhancements**:
1. **Area Extraction via Statistics**:
   - `parcel.Area` gives the 2D projected area.
   - Using `parcel.Statistics` provides both:
     - `parcel.Statistics.Area` (accurate 2D area)
     - `parcel.Statistics.Perimeter` (accurate boundary perimeter)
2. **Parcel Geometry & Centroid**:
   - As implemented in `CTPA_BangThongKeParcel.cs` (lines 383-394), `parcel.GeometricExtents` provides the 3D bounding box:
     - `MinPoint = [minX, minY, minZ]`, `MaxPoint = [maxX, maxY, maxZ]`
     - Centroid: `[(minX + maxX)/2, (minY + maxY)/2]`
   - Furthermore, `parcel.GetParcelLoops()` returns `ParcelLoopCollection` which can be traversed to extract exact polygon boundary coordinates `points: [[x1, y1], [x2, y2], ...]`.
3. **Parcel Segments**:
   - In Civil 3D, parcel boundaries are defined by `ParcelSegment` entities (lines and arcs).
   - Can be obtained via:
     - `site.GetParcelSegmentIds()`
     - Or `parcel.GetSegmentIds()` on each parcel.
   - For each segment: `SegmentType` (Line, Curve), `StartPoint`, `EndPoint`, `Length`, `Radius`, `Delta`, `Layer`, `StyleName`.
4. **Site-less Parcels**:
   - In Civil 3D 2024+, parcels can also exist outside traditional sites. Querying both `site.GetParcelIds()` and scanning ModelSpace for `AECC_PARCEL` ensures 100% parcel capture.

### 4.2 Token Optimization & Pagination (Requirement R2)
In `InspectAllCivil3DData` (lines 755-1227):
- For every alignment, up to 100 PVIs per profile are dumped.
- For every sample line group, up to 500 sample lines are dumped.
- For a corridor with 5 baselines and 200 sample lines, the JSON payload exceeds 500 KB (~100,000 tokens), causing LLM timeouts or context overflow!

**Solution**:
1. **`summary_only: bool` mode** (or `action="summary"`):
   - Returns counts, names, extents, station ranges, and key metadata.
   - Completely omits detailed `pvis` arrays and `sample_lines` arrays.
2. **Pagination & Limits**:
   - Support `pvi_limit` (default 0 or 10 in full mode, 0 in summary mode).
   - Support `sample_line_limit` (default 20, with offset/page).
3. **Field Filtering**:
   - Support `fields: ["alignments", "surfaces"]` (or comma-separated string) so the caller can request only what they need instead of the entire Civil database.

---

## 5. Table Drawing (`autocad_draw_table`) & Native AcDbTable (Requirement R4)

### 5.1 Current Implementation & Flaws
- **In Python (`autocad_mcp_server.py`, lines 1357-1432)**:
  - `autocad_draw_table` manually calculates bounding coordinates and generates:
    - 1 `rectangle` shape for the title box.
    - 1 `text` annotation for the title.
    - 1 `rectangle` shape for the header row.
    - $N-1$ `line` shapes for header vertical dividers.
    - $N$ `text` annotations for header labels.
    - For each row: 1 `rectangle` shape + $N-1$ `line` shapes + $N$ `text` annotations.
  - Cell value handling:
    ```python
    cell_val = str(row[idx]) if idx < len(row) else ""
    annots.append({
        "type": "text",
        "position": [cx + cw / 2.0, (curr_y + next_y) / 2.0 - text_height * 0.35],
        "text": cell_val,
        "height": text_height * 0.92,
        "align": "center",
        "layer": layer,
        "color": 7,
    })
    ```
- **In C# (`38.AT_KhoiDongMcpBridge.cs`, lines 2401-2436)**:
  ```csharp
  if (type == "text")
  {
      ...
      var dbText = new DBText
      {
          Position = pos,
          Height = height,
          TextString = txtVal,
          ...
      };
      ...
      ms.AppendEntity(dbText);
      tr.AddNewlyCreatedDBObject(dbText, true);
  }
  ```
  - **The Problem**: Whenever `cell_val` is `""` (empty string or empty cell), C# still instantiates `DBText`, appends it to ModelSpace, and commits it!
  - In AutoCAD, empty `DBText` objects are invalid "ghost" entities that cannot be seen or easily selected by users, but pollute selection sets, cause DXF export warnings, and fail drawing audits.

### 5.2 Native `AcDbTable` Solution
AutoCAD has native `Autodesk.AutoCAD.DatabaseServices.Table` (`ATable`).
The codebase already has two production-grade examples of native `ATable` creation:
1. `AT_KhoiDongMcpBridge.cs` lines 2048-2125 (`DrawStakeTableInModelSpace`)
2. `CTPA_BangThongKeParcel.cs` lines 468-547 (`CreateParcelTable`)

**Native `ATable` Advantages**:
- A single `Entity` object representing the entire grid, headers, title, and data.
- Built-in cell formatting, text styles, alignment (`MiddleCenter`, `MiddleRight`), row height, column widths.
- Merge title cells natively: `table.MergeCells(CellRange.Create(table, 0, 0, 0, numCols - 1))`.
- Zero empty `DBText` entities created.
- Editing in AutoCAD allows the user to click any cell and edit values directly like an Excel sheet.
- Clean bounding box calculation via `table.GeometricExtents` for tight preview framing.

### 5.3 Immediate Code Guard in `CreateAnnotationEntity`
Even when drawing loose text, `CreateAnnotationEntity` should never create empty text entities:
```csharp
if (type == "text")
{
    string txtVal = GetStr(item, "text", "");
    if (string.IsNullOrWhiteSpace(txtVal)) return false; // Prevent empty DBText
    ...
}
```

---

## 6. Implementation Roadmap for Subsequent Phase

| Priority | Component | Target Files | Key Changes |
|---|---|---|---|
| **P0** | Prevent empty `DBText` | `38.AT_KhoiDongMcpBridge.cs` | Guard `string.IsNullOrWhiteSpace(txtVal)` in `CreateAnnotationEntity` |
| **P0** | Native Table Support | `38.AT_KhoiDongMcpBridge.cs`, `autocad_mcp_server.py` | Add native `draw_table` handler in C# using `ATable`; update Python `autocad_draw_table` to send table JSON directly |
| **P0** | Multi-Document Resolution (R1) | `38.AT_KhoiDongMcpBridge.cs`, `autocad_mcp_server.py` | Remove silent hijacking; support target drawing by name across all endpoints; default to active doc |
| **P1** | ModelSpace Inspection Expansion (R3) | `38.AT_KhoiDongMcpBridge.cs` (`InspectModelSpace`) | Add detection of `ATable`, `CivilParcel`, `ParcelSegment`, `Alignment`, `CivSurface`, `Corridor` |
| **P1** | Token Optimization (R2) | `38.AT_KhoiDongMcpBridge.cs` (`InspectAllCivil3DData`) | Implement `summary_only`, `fields` filtering, and PVI/sampleline pagination |
| **P1** | Parcel Geometry & Statistics (R3) | `38.AT_KhoiDongMcpBridge.cs` | Extract `parcel.Statistics.Area/Perimeter`, `GeometricExtents`, centroid, and boundary points |
| **P2** | Preview Framing (R4) | `autocad_mcp_server.py` | Frame preview tightly around created table extents |

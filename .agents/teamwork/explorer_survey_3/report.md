# Detailed Technical Investigation Report — Explorer Survey 3
**Focus**: Build System Architecture, Civil 3D Parcel Geometry/APIs, Native AcDbTable Creation, Preview Framing, and Non-Destructive Live Verification
**Date**: 2026-11-09
**Author**: Explorer Survey 3 (Civil 3D & Build Explorer)
**Target Project**: `c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API`
**Related MCP Server**: `C:\Dropbox\0.AI AGENT\11.Autocad Drawing\autocad_mcp_server.py`

---

## 1. Executive Summary

This investigation analyzed the complete end-to-end workflow between the Python MCP server (`autocad-mcp`), the AutoCAD/Civil 3D in-process C# HTTP bridge (`AT_McpBridgeServer` in `38.AT_KhoiDongMcpBridge.cs`), the build & hot-reload pipeline, and Civil 3D entity structures.

### Key Discoveries:
1. **Build Compliance & Tooling**:
   - The solution compiles cleanly on **.NET 10** (`TargetFramework: net10.0-windows`) with 0 errors and 0 warnings using `BuildProject.ps1`.
   - The build output is routed outside Dropbox to `C:\CadBuild\Autocad2026_API\` via `Directory.Build.props` to prevent file locking and disk sync lag.
   - **Vulnerability Found**: Concurrent execution of `dotnet build` by parallel agents collides on `obj\Debug\MyFirstProject.AssemblyInfo.cs` (error `MSB3713`). A serial build lock or `/nodeReuse:false` is recommended.
2. **Civil 3D Parcel API & Inspection**:
   - The live AutoCAD 2027 session (PID 13948) has 4 open drawings; the active drawing `C3D-GT-2024.dwg` contains 5 Parcels under "Site 1" (`Svn_2`, `S_VuotBTN : 8`, `Svn_1`, `Sg_2`, `S_VuotBTN : 9`).
   - The `Autodesk.Civil.DatabaseServices.Parcel` class exposes `Area`, `Perimeter2d`, `Number`, `TaxId`, `StyleId`, `StyleName`, `AreaSelectionLabelStyleId`, and `AreaSelectionLabelLocation`.
   - Its 2D/3D boundary extents and centroid can be derived accurately from `parcel.GeometricExtents`.
   - Currently, `autocad_inspect_drawing` completely ignores Civil 3D entities and native Tables in ModelSpace; only raw primitives are detected.
3. **Table Generation & Preview Framing**:
   - `autocad_draw_table` in `autocad_mcp_server.py` currently builds tables out of dozens of primitive lines and rectangles, creating redundant empty `DBText` entities for blank cells. It does NOT generate native AutoCAD `Table` entities (`AcDbTable`).
   - In contrast, the C# codebase already contains production-grade native `AcDbTable` creation logic (`DrawStakeTableInModelSpace` and `CreateParcelTable`).
   - Preview generation (`autocad_export_preview`) fails to frame tables because:
     1. `InspectModelSpace` in C# completely omits `Table` objects from entity collection.
     2. `_render_entities_to_png` in Python has no bounding-box calculation or rendering support for `TABLE`.
     3. When tables are drawn at `[0, 0]` in civil drawings with UTM coordinates (e.g., $X=500,000, Y=1,300,000$), the global zoom bounding box collapses the table into an invisible fraction of a pixel.
4. **Non-Destructive Testing Strategy**:
   - Direct execution on live drawings risks dirtying user production drawings (the active drawing is `Z:\Z.FORM MAU LAM VIEC\...\C3D-GT-2024.dwg`).
   - A non-destructive test suite MUST execute within a dedicated scratch drawing created via `acad.Documents.Add()`, verify commands, and discard the scratch document without saving (`doc.Close(false)`).

---

## 2. Build System Architecture & Tooling Analysis

### 2.1 Build Scripts & Environment
- **`BuildProject.ps1`**:
  - Dynamically detects the .NET SDK at `$env:USERPROFILE\.dotnet\dotnet.exe` (SDK 10.0.400).
  - Configures `$env:DOTNET_ROOT` and `$env:PATH`.
  - Generates a randomized AssemblyName: `Civil3D_Tools_<random>` (e.g., `Civil3D_Tools_cawa2wdqv54`). This allows AutoCAD's .NET runtime to load updated assemblies repeatedly without restarting AutoCAD.
  - Copies the build output to `Civil3D_Tools.dll` and `Civil3D_Tools.pdb`.
  - Prunes historical builds, maintaining the 5 most recent assemblies in `C:\CadBuild\Autocad2026_API\MyFirstProject\bin\Debug`.
  - Writes the absolute forward-slash path to `last_dll.txt` and generates `last_reload.lsp` for immediate AutoLISP `(command "._NETLOAD" ...)` execution.
- **`Directory.Build.props`**:
  ```xml
  <PropertyGroup Condition="'$(OS)' == 'Windows_NT'">
    <CadBuildRoot>C:\CadBuild\Autocad2026_API\</CadBuildRoot>
    <BaseOutputPath>$(CadBuildRoot)$(MSBuildProjectName)\bin\</BaseOutputPath>
    <BaseIntermediateOutputPath>$(CadBuildRoot)$(MSBuildProjectName)\obj\</BaseIntermediateOutputPath>
  </PropertyGroup>
  <PropertyGroup>
    <EnableWindowsTargeting>true</EnableWindowsTargeting>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
  </PropertyGroup>
  ```
  - This keeps all `bin\` and `obj\` artifacts on the local drive outside Dropbox, avoiding hash thrashing, anti-virus locks, and cloud sync stalls.
- **`MyFirstProject.csproj`**:
  - `TargetFramework`: `net10.0-windows`
  - References proprietary Autodesk 2027 assemblies located at `C:\Program Files\Autodesk\AutoCAD 2027\` (`accoremgd`, `acdbmgd`, `acmgd`, `AecBaseMgd`, `AeccDbMgd`, `AdWindows`, `AecPropDataMgd`, etc.) with `<Private>False</Private>`.
  - NuGet dependencies: `ClosedXML` (v0.104.2) and `ExcelNumberFormat` (v1.1.0).

### 2.2 CAD_Reloader vs NetReload
- **`CAD_Reloader`** (Primary):
  - Commands: `NETRELOAD`, `RELOAD`, `CADRELOAD`, `NETRELOADUI`.
  - Mechanism: Shadows DLL to `%TEMP%\AutoCAD_Reloader\<timestamp>\`, copies `.pdb`, registers assembly resolve handler, disables security alerts (`SECURELOAD = 0`), registers `%TEMP%` in `TRUSTEDPATHS`, and executes `ExtensionLoader.Load()`.
  - Includes a Windows Forms UI (`NetReloadForm`) for settings and manual triggering.
- **`NetReload`** (Legacy):
  - Command: `NRL`.
  - Mechanism: Executes `dotnet build` with randomized assembly name and directly calls `Assembly.LoadFrom()`.
- **In-Process HTTP Bridge `/reload` Endpoint**:
  - `38.AT_KhoiDongMcpBridge.cs` (lines 418-449) exposes `/reload`: reads `dll_path` from JSON and calls `ExtensionLoader.Load(dllPath)` on AutoCAD's Main UI thread.

### 2.3 Build Verification Result & Concurrent Lock Assessment
- Serial build verification:
  ```powershell
  powershell -ExecutionPolicy Bypass -NoProfile -File "c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\BuildProject.ps1"
  # Result: Build succeeded. 0 Warning(s), 0 Error(s). Time Elapsed: 00:00:06.20
  ```
- **Observed Failure Mode**: When two processes invoke `dotnet build` simultaneously, MSBuild produces:
  `error MSB3713: The file "C:\CadBuild\Autocad2026_API\MyFirstProject\obj\Debug\MyFirstProject.AssemblyInfo.cs" could not be created... process cannot access the file because it is being used by another process.`
- **Remediation**:
  Ensure all automated build tasks pass `/nr:false` (disable MSBuild node reuse) or ensure serial execution when multi-agent workflows are active.

---

## 3. Civil 3D Parcel Geometry, Statistics, Site Membership & Styles

### 3.1 Live Session Verification
A live query against `http://127.0.0.1:18888/civil` (`action: "inspect_all"`) confirmed the active drawing `C3D-GT-2024.dwg` contains 5 Parcels:

| Parcel Handle | Name | Number | Site Name | Style | Area ($m^2$) | Layer |
|---|---|---|---|---|---|---|
| `8F2B` | `Svn_2` | 5 | Site 1 | `S_VuotBTN` | 297.73 | `0` |
| `9723` | `S_VuotBTN : 8` | 8 | Site 1 | `S_VuotBTN` | 182.99 | `0` |
| `8F35` | `Svn_1` | 6 | Site 1 | `S_VuotBTN` | 342.72 | `0` |
| `8ED5` | `Sg_2` | 2 | Site 1 | `S_VuotBTN` | 124.70 | `0` |
| `9732` | `S_VuotBTN : 9` | 9 | Site 1 | `S_VuotBTN` | 227.34 | `0` |

### 3.2 Autodesk.Civil.DatabaseServices.Parcel APIs
From direct inspection of `AeccDbMgd.dll`, `Autodesk.Civil.DatabaseServices.Parcel` provides the following properties:

1. **Hierarchy & Membership**:
   - `parcel.SiteId`: `ObjectId` of the parent `Site`.
   - `site.GetParcelIds()`: Traversal mechanism from `CivilDocument.GetSiteIds()`.
2. **Area & Geometry Statistics**:
   - `parcel.Area`: 2D planar area in square meters.
   - `parcel.Perimeter2d`: Total boundary length in meters.
   - `parcel.GeometricExtents`: Returns `Extents3d` (`MinPoint` and `MaxPoint`).
   - `Centroid`: Calculated as `Point2d((MinPoint.X + MaxPoint.X)/2.0, (MinPoint.Y + MaxPoint.Y)/2.0)`.
   - `parcel.AreaSelectionLabelLocation`: Placement coordinate (`Point3d`) of the parcel area tag.
3. **Identification & Metadata**:
   - `parcel.Number`: Sequential parcel integer index (uint).
   - `parcel.Name`: User-assigned parcel identifier string.
   - `parcel.Description`: Freeform text description.
   - `parcel.TaxId`: Tax/cadastral assessment string.
4. **Styles & Visual Presentation**:
   - `parcel.StyleId` & `parcel.StyleName`: Boundary line/fill style.
   - `parcel.AreaSelectionLabelStyleId`: Style ID for the area text tag.
5. **Boundary Segments**:
   - `parcel.GetParcelSegmentTableIds()`: References segment boundary tables.
   - Boundary segment entities (`ParcelSegment`) reside in the drawing database and can be queried per Site via `site.GetParcelSegmentIds()`.

### 3.3 Gaps in Current Inspection Tools
1. **`civil3d_inspect`**:
   - Currently returns only `{ site_name, name, number, handle, layer, style, area_m2, description }`.
   - Missing: `perimeter_m`, `tax_id`, `centroid` `[X, Y]`, `bounding_box` `[minX, minY, maxX, maxY]`, `style_id`, and `site_id`.
2. **`autocad_inspect_drawing`**:
   - Loops through ModelSpace but only filters for: `Line`, `Polyline`, `Circle`, `Arc`, `RotatedDimension`, `AlignedDimension`, `DBText`, `MText`.
   - Civil 3D entities (Parcels, Surfaces, Alignments, Corridors) and AutoCAD `Table` entities are completely ignored in the counts and bounding box.

---

## 4. Native AcDbTable Creation & Preview Framing Mechanics

### 4.1 Native AcDbTable vs Current Custom Line/Text Implementation

| Feature | Current `autocad_draw_table` | Native `AcDbTable` (`Autodesk.AutoCAD.DatabaseServices.Table`) |
|---|---|---|
| **Underlying Entity** | Dozens of individual `LINE`, `POLYLINE`, `RECTANGLE` and `DBTEXT` objects | Single unified `AcDbTable` (`Autodesk.AutoCAD.DatabaseServices.Table`) |
| **Empty Cells** | Creates `DBText` entities with `""`, cluttering ModelSpace | Native cell with empty content, zero entity overhead |
| **AutoCAD Editing** | Cannot be edited as a table; user must move/edit individual lines & text | Full AutoCAD Table Editor support, column dragging, cell formulas, Excel export |
| **Formatting** | Hardcoded line coordinates and text positioning | Built-in `TableStyle`, row heights, column widths, margin padding, cell alignment |
| **Performance** | $O(R \times C)$ entities added to database | Exactly 1 entity added to database |
| **C# Reference Implementation** | None (pure primitive drawing) | Available in `38.AT_KhoiDongMcpBridge.cs` (`DrawStakeTableInModelSpace`) & `CTPA_BangThongKeParcel.cs` (`CreateParcelTable`) |

### 4.2 Standard Native AcDbTable Creation Pattern
```csharp
ATable tb = new ATable();
tb.SetDatabaseDefaults(db);
tb.TableStyle = db.Tablestyle;
tb.Layer = layerName;
tb.Position = insertionPoint;
tb.SetSize(numRows, numCols);

// Row height & column widths
for (int r = 0; r < numRows; r++) tb.Rows[r].Height = rowHeight;
tb.Rows[0].Height = titleHeight * 1.8;
for (int c = 0; c < numCols; c++) tb.Columns[c].Width = colWidths[c];

// Title Row (Merged)
tb.MergeCells(CellRange.Create(tb, 0, 0, 0, numCols - 1));
tb.Cells[0, 0].TextString = title;
tb.Cells[0, 0].TextStyleId = tahomaId;
tb.Cells[0, 0].TextHeight = titleHeight;
tb.Cells[0, 0].Alignment = CellAlignment.MiddleCenter;

// Header Row
for (int c = 0; c < numCols; c++)
{
    tb.Cells[1, c].TextString = headers[c];
    tb.Cells[1, c].TextStyleId = tahomaId;
    tb.Cells[1, c].TextHeight = textHeight;
    tb.Cells[1, c].Alignment = CellAlignment.MiddleCenter;
}

// Data Rows
for (int r = 0; r < rowCount; r++)
{
    for (int c = 0; c < numCols; c++)
    {
        string val = data[r][c];
        if (!string.IsNullOrEmpty(val))
        {
            tb.Cells[r + 2, c].TextString = val;
            tb.Cells[r + 2, c].TextStyleId = tahomaId;
            tb.Cells[r + 2, c].TextHeight = textHeight;
            tb.Cells[r + 2, c].Alignment = (c == 0) ? CellAlignment.MiddleCenter : CellAlignment.MiddleLeft;
        }
    }
}

tb.GenerateLayout();
btr.AppendEntity(tb);
tr.AddNewlyCreatedDBObject(tb, true);
```

### 4.3 Why Preview Framing Fails & The Fix

#### Defect Mechanism:
1. In `autocad_mcp_server.py` line 1446, when `export_preview_path` is passed, it calls:
   `entities = _collect_modelspace_entities_for_preview()`
   `preview_saved = _render_entities_to_png(entities, export_preview_path, title_badge=title)`
2. `_collect_modelspace_entities_for_preview` queries `/inspect` (`InspectModelSpace` in C#).
3. `InspectModelSpace` ignores `ATable` entities completely.
4. `_render_entities_to_png` calculates the bounding box using only `LINE`, `LWPOLYLINE`, `CIRCLE`, `ARC`, `DIMENSION`, `TEXT`.
5. If only a table exists, the bounding box falls back to `[0, 0]` to `[100, 100]`.
6. If the drawing contains civil entities at UTM coordinates (e.g. $X=580,000, Y=1,350,000$), the global zoom extends across hundreds of kilometers, reducing the table at `[0, 0]` to an invisible sub-pixel artifact.

#### Solution Architecture:
1. **In C# Bridge (`InspectModelSpace`)**:
   Add support for `ATable`:
   ```csharp
   else if (ent is ATable tb)
   {
       entities.Add(new
       {
           type = "TABLE",
           layer = tb.Layer,
           color = col,
           p = new[] { tb.Position.X, tb.Position.Y },
           width = tb.Width,
           height = tb.Height,
           num_rows = tb.Rows.Count,
           num_cols = tb.Columns.Count
       });
   }
   ```
2. **In Python Preview Renderer (`_render_entities_to_png`)**:
   Add `TABLE` to the bounding box calculation:
   ```python
   elif et == "TABLE":
       px, py = e["p"][0], e["p"][1]
       tw = float(e.get("width", 100.0))
       th = float(e.get("height", 50.0))
       xs.extend([px, px + tw])
       ys.extend([py - th, py])
   ```
3. **Dedicated Target Framing for `autocad_draw_table`**:
   When drawing a table with `export_preview_path`, frame specifically around the table's bounding box:
   `[insert_point[0] - margin, insert_point[1] - total_height - margin, insert_point[0] + total_width + margin, insert_point[1] + margin]`.
   This guarantees crystal-clear, tightly framed previews regardless of the drawing's overall extents.

---

## 5. Non-Destructive Testing Strategy for Live AutoCAD / Civil 3D

### 5.1 The Danger to Live Drawings
In a production workstation, AutoCAD or Civil 3D is frequently open with active user drawings. Our live survey revealed:
- Drawing: `Z:\Z.FORM MAU LAM VIEC\1. BIM\2.MAU C3D\Z0.STYLE C3D\2. THIET KE GIAO THONG\THANG\C3D-GT-2024.dwg`
- Drawing: `W:\1.KS-TK\4.NINH HOA\4. DT652.B -DT652.D\3.HS_TKBVTC\...\BD-TD-VX.dwg` (Contains 120 Alignments, 20 Surfaces)

Executing `clear` or modifying entities directly on these drawings risks irrecoverable project data corruption.

### 5.2 The 4-Pillar Non-Destructive Testing Protocol

```
                  ┌──────────────────────────────────────────────┐
                  │          Non-Destructive Testing              │
                  └──────────────────────┬───────────────────────┘
                                         │
        ┌────────────────────────────────┼────────────────────────────────┐
        │                                │                                │
┌───────▼──────────────┐      ┌──────────▼───────────┐      ┌─────────────▼────────────┐
│ Pillar 1: Scratch    │      │ Pillar 2: Read-Only  │      │ Pillar 3: Multi-Document │
│ Drawing Isolation    │      │ Inspection Safety    │      │ Explicit Targeting       │
│ • Create new drawing │      │ • Use OpenMode.ForRead│     │ • Pass document_name     │
│ • Run test mutations │      │ • Non-committing tr   │     │ • Never mutate active    │
│ • Close without save │      │ • Zero DBMOD dirtied │     │   production drawings    │
└──────────────────────┘      └──────────────────────┘      └──────────────────────────┘
```

#### Pillar 1: Temporary Scratch Drawing Isolation
For all draw, clear, and table creation tests:
1. Connect via COM: `acad = win32com.client.GetActiveObject("AutoCAD.Application.25")`.
2. Add a new scratch drawing: `scratch_doc = acad.Documents.Add()`.
3. Set the scratch document as active, or target it by name (e.g., `Drawing2.dwg`).
4. Execute test commands (`autocad_draw_table`, `autocad_draw_primitives`, `autocad_export_preview`).
5. Verify output (PNG exists, entity count matches).
6. Close the scratch drawing without saving: `scratch_doc.Close(False)`.
7. User drawings are left completely untouched.

#### Pillar 2: Read-Only Inspection Safety
For `civil3d_inspect` and `autocad_inspect_drawing`:
- Change inspection object open mode in C# from `OpenMode.ForWrite` to `OpenMode.ForRead`.
- Use read-only transactions (`tr.Commit()` without modification or `tr.Abort()`).
- Verify that `DBMOD` (Database Modified flag) remains 0 on inspected drawings.

#### Pillar 3: Multi-Document Explicit Targeting (Requirement R1)
- `ResolveCivilTargetDocument` in `38.AT_KhoiDongMcpBridge.cs` already accepts `document_name`.
- **Requirement**: Expose `document_name: Optional[str] = None` in Python tools:
  - `civil3d_inspect(action=..., target_name=..., max_items=..., document_name=...)`
  - `civil3d_automate(action=..., params={..., "document_name": ...})`
  - `autocad_inspect_drawing(document_name=...)`
  - `autocad_draw_table(document_name=...)`
- In C# bridge, propagate `document_name` filtering across `/inspect`, `/draw`, `/status`, and `/manage`.

#### Pillar 4: Token Efficiency & Pagination (Requirement R2)
- Large models with 120 alignments (such as `BD-TD-VX.dwg`) generate multi-megabyte payloads if all PVIs and sample lines are serialized.
- **Requirement**:
  1. `action="summary"` in `civil3d_inspect` must return ONLY `BuildCivil3DSummarySafe` (counts and names), requiring $< 500$ tokens.
  2. Implement `entity_type` filter (`"alignments"`, `"surfaces"`, `"parcels"`, `"corridors"`).
  3. Default `pvi_limit` to 20 instead of 100 unless explicitly requested.
  4. Include `pagination: { page: 1, page_size: 50, total: ... }` for sample lines and points.

---

## 6. Synthesis & Architecture Recommendations

### 6.1 Unified Data Flow Diagram
```
[Antigravity AI Agent]
        │
        ▼ (stdio / MCP)
[autocad_mcp_server.py]
        │
        ├─► [Tool: civil3d_inspect(document_name, action='summary'|'inspect_all', entity_filter)]
        ├─► [Tool: autocad_draw_table(native AcDbTable with proper framing)]
        ├─► [Tool: autocad_inspect_drawing(detects Civil3D entities & Tables)]
        │
        ▼ (HTTP POST / 127.0.0.1:18888)
[38.AT_KhoiDongMcpBridge.cs (In-CAD Main UI Thread)]
        │
        ├─► ResolveTargetDocument(document_name)
        ├─► Transaction (OpenMode.ForRead for inspections)
        ├─► Native AcDbTable generation (ATable)
        └─► Civil 3D Object extraction (Parcels, Surfaces, Alignments, Corridors)
```

### 6.2 Recommended Change Matrix for Implementation

| File | Target Location | Change Description | Impact |
|---|---|---|---|
| `38.AT_KhoiDongMcpBridge.cs` | `InspectAllCivil3DData` | Add `perimeter_m`, `tax_id`, `centroid`, `bounding_box`, `style_id` to Parcel return payload | Resolves R3 (Parcel extraction) |
| `38.AT_KhoiDongMcpBridge.cs` | `ExecuteCivilRequest` | Branch `action == "summary"` to call `BuildCivil3DSummarySafe` directly | Resolves R2 (Token efficiency) |
| `38.AT_KhoiDongMcpBridge.cs` | `InspectModelSpace` | Add recognition of `ATable` and Civil 3D entities in ModelSpace | Resolves R3 (Civil/Table detection) |
| `38.AT_KhoiDongMcpBridge.cs` | `ExecuteDrawBatch` | Add native `table` drawing handler constructing `ATable` | Resolves R4 (Native table) |
| `autocad_mcp_server.py` | `civil3d_inspect` | Expose `document_name`, `entity_filter`, default `summary` mode | Resolves R1, R2 |
| `autocad_mcp_server.py` | `autocad_draw_table` | Emit native table schema; calculate tight bounding box for preview framing | Resolves R4 (Table & preview) |
| `autocad_mcp_server.py` | `_render_entities_to_png` | Add `TABLE` bounding box & rendering support | Resolves R4 (Preview framing) |
| `BuildProject.ps1` | `dotnet build` | Add `/nodeReuse:false` flag to avoid concurrent build locks | Resolves R5 (Build reliability) |

---
*Report completed and verified against live AutoCAD 2027 session and .NET 10 compilation pipeline.*

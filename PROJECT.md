# Project: AutoCAD & Civil 3D MCP Server Optimization

## Architecture
- **In-Process C# HTTP Bridge**: `38.AT_KhoiDongMcpBridge.cs` in `Civil3D_Tools` (`MyFirstProject`). Runs `HttpListener` on `http://127.0.0.1:18888`. Marshals requests to AutoCAD Main UI Thread via `Control.Invoke`, locks documents via `doc.LockDocument()`, and executes database transactions with `OpenMode.ForWrite` or `OpenMode.ForRead`.
- **External Python MCP Server**: `C:\Dropbox\0.AI AGENT\11.Autocad Drawing\autocad_mcp_server.py`. Exposes MCP tools over stdio to Antigravity. Communicates with C# bridge via HTTP POST JSON payloads.
- **Tool Schemas**: `C:\Users\hoang\.gemini\antigravity\mcp\autocad-mcp\*.json`. Defines tool parameters and contracts for Antigravity.
- **Build Infrastructure**: `BuildProject.ps1` using .NET 10 (`net10.0-windows`), compiling output outside Dropbox to `C:\CadBuild\Autocad2026_API\`.

## Feature Inventory
| # | Feature | Description | Milestone | Source |
|---|---------|-------------|-----------|--------|
| 1 | Multi-Document Selection (R1) | All MCP tools target active document by default; accept optional `document_name` to target specific open drawing without silent hijacking | M1 | ORIGINAL_REQUEST §R1 |
| 2 | Output Pagination & Filtering (R2) | `civil3d_inspect` and C# `/civil` support `summary_only`, `entity_type` filtering, PVI/sample line limits, and pagination to save tokens | M1 | ORIGINAL_REQUEST §R2 |
| 3 | ModelSpace Civil 3D & Table Inspection (R3) | `InspectModelSpace` and `autocad_inspect_drawing` detect Civil 3D entities (Parcels, Alignments, Surfaces, Corridors, Tables) in ModelSpace | M2 | ORIGINAL_REQUEST §R3 |
| 4 | Rich Parcel & Segment Extraction (R3) | Extract Parcel `Area`, `Perimeter2d`, `Centroid`, `BoundingBox`, `TaxId`, `SiteId`, `StyleId`, `StyleName`, and `ParcelSegment` info | M2 | ORIGINAL_REQUEST §R3 |
| 5 | Clean Native Table Drawing (R4) | Create native `AcDbTable` entities instead of loose primitives; guard against creating blank `DBText` entities | M3 | ORIGINAL_REQUEST §R4 |
| 6 | Preview Framing & Table Bounds (R4) | Include `TABLE` bounds in `InspectModelSpace` & `_render_entities_to_png`; compute focused bounding box for `export_preview_path` | M3 | ORIGINAL_REQUEST §R4 |
| 7 | Property Set Definition & Value MCP Tools (R6) | List Property Set Definitions (properties, formulas); Read & Attach Property Sets on entities; Set manual values; Copy/import from template | M4 | ORIGINAL_REQUEST §R6 |
| 8 | AEC Schedule Table MCP Tools (R6) | List Schedule Table Styles & existing tables; Insert `AEC_SCHEDULE_TABLE`; Trigger update; Honor `document_name`; Never save template | M4 | ORIGINAL_REQUEST §R6 |
| 9 | .NET 10 Build Compliance (R5) | 100% build pass with 0 errors via `BuildProject.ps1` | M5 | ORIGINAL_REQUEST §R5 |
| 10 | Non-Destructive Live E2E Verification (R5) | Execute automated tests in scratch drawing (`acad.Documents.Add()`) and read-only inspection of live models without corrupting drawings | M5 | ORIGINAL_REQUEST §R5 |

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| M1 | Multi-Doc Context & Output Pagination | R1 & R2: Document resolution refactor in C# & Python, `summary_only`, filtering, pagination | none | DONE |
| M2 | Civil 3D & Parcel Entity Inspection | R3: ModelSpace inspection for Civil 3D objects/Tables, enriched Parcel geometry/statistics | M1 | IN_PROGRESS |
| M3 | Native Table Drawing & Preview Framing | R4: Native `AcDbTable` creation, blank text entity guard, preview framing | M1 | PLANNED |
| M4 | Property Set & AEC Schedule Table Tools | R6: Full Property Set & Schedule Table automation tools in C# and Python | M1, M2 | PLANNED |
| M5 | Build Compliance & Live E2E Verification | R5: Complete build check (0 errors) and automated live CAD test suite execution | M1, M2, M3, M4 | PLANNED |

## Interface Contracts

### 1. Document Targeting & Context Resolution
- Parameter: `document_name: Optional[str] = None`
- JSON Payload: `{"document_name": "...", ...}`
- C# Resolution:
  - If `string.IsNullOrWhiteSpace(document_name)`: Target `Application.DocumentManager.MdiActiveDocument`. Never hijack to another drawing.
  - If specified: Find document in `Application.DocumentManager` matching name (case-insensitive, with or without `.dwg` / path). If not found, return clean error JSON without crashing.

### 2. Civil 3D Inspection Contract (`/civil` & `civil3d_inspect`)
- Parameters:
  - `document_name: Optional[str] = None`
  - `action: str = "inspect_all"` ("inspect_all", "summary", "alignments", "surfaces", "corridors", "parcels", "pipe_networks")
  - `summary_only: bool = False` (When true, returns counts, names, styles, lengths, and basic bounds; omits PVI coordinates and sample line lists)
  - `entity_type: Optional[str] = None` (Filter by specific type, e.g. "parcels", "alignments", "surfaces")
  - `target_name: Optional[str] = None` (Filter by entity name)
  - `max_items: int = 200`
  - `page: int = 1`, `page_size: int = 50`

### 3. ModelSpace Drawing Inspection Contract (`/inspect` & `autocad_inspect_drawing`)
- Added entity types in JSON response:
  - `TABLE`: `{ "handle": "...", "position": [X, Y, Z], "width": W, "height": H, "rows": R, "columns": C }`
  - `CIVIL_PARCEL`: `{ "handle": "...", "name": "...", "number": N, "area_m2": A, "perimeter_m": P, "site": "...", "style": "...", "bounding_box": [minX, minY, maxX, maxY], "centroid": [cX, cY] }`
  - `CIVIL_ALIGNMENT`: `{ "handle": "...", "name": "...", "length": L, "style": "..." }`
  - `CIVIL_SURFACE`: `{ "handle": "...", "name": "...", "type": "TIN", "style": "..." }`
  - `CIVIL_CORRIDOR`: `{ "handle": "...", "name": "...", "style": "..." }`

### 4. Table Drawing Contract (`/table` or `/draw` & `autocad_draw_table`)
- Payload:
  ```json
  {
    "document_name": null,
    "position": [X, Y, Z],
    "title": "Title String",
    "headers": ["Col 1", "Col 2", ...],
    "rows": [["val1", "val2"], ...],
    "column_widths": [W1, W2, ...],
    "row_height": 8.0,
    "text_height": 2.5,
    "table_style": "Standard",
    "layer": "BANG_THONG_KE"
  }
  ```
- C# creates a single `Autodesk.AutoCAD.DatabaseServices.Table` (`ATable`), formats cells, sets cell alignments, and commits transaction.
- Returns `{ "status": "success", "handle": "...", "width": W, "height": H, "bounding_box": [minX, minY, maxX, maxY] }`.

### 5. Property Set & Schedule Table Contract (`/property_sets` & `/schedule_tables`)
- Endpoints in C# and Python tools:
  - `property_set_list(document_name=None)`: Lists all definitions in `AEC_PROPERTY_SET_DEFS`.
  - `property_set_read(handles=None, object_types=None, layer=None, property_set_name=None, document_name=None)`: Reads property values attached to matching entities.
  - `property_set_attach(handles, property_set_name, values={}, document_name=None)`: Attaches property set and writes manual property values.
  - `property_set_import(source_drawing_path, property_set_names=[], schedule_table_styles=[], document_name=None)`: Imports definition objects from source drawing.
  - `schedule_table_list(document_name=None)`: Lists Schedule Table Styles and existing `AEC_SCHEDULE_TABLE` entities.
  - `schedule_table_insert(style_name, position, selection_handles=[], document_name=None)`: Inserts a schedule table entity.
  - `schedule_table_update(handle=None, document_name=None)`: Triggers recalculation/update of schedule tables.

## Code Layout
- `c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\MyFirstProject\Acad Tool\38.AT_KhoiDongMcpBridge.cs`: Core C# bridge endpoints and AutoCAD transactions.
- `C:\Dropbox\0.AI AGENT\11.Autocad Drawing\autocad_mcp_server.py`: Python MCP server tools, request routing, preview rendering.
- `C:\Users\hoang\.gemini\antigravity\mcp\autocad-mcp\`: JSON schemas defining MCP tools.
- `c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\BuildProject.ps1`: Solution compilation and hot-reload packaging.

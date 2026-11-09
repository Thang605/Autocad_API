# Original User Request

## Initial Request — 2026-11-09T08:14:31Z

Research and optimize the AutoCAD/Civil 3D MCP server and C# bridge (`AcadMcpBridge`), enhancing connection efficiency, multi-document handling, and Civil 3D entity inspection and drawing capabilities.

Working directory: c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API
Integrity mode: development

## Requirements

### R1. Multi-Document Context and Selection
Enable MCP tools and endpoints (including `civil3d_inspect`) to target the active document by default and support an optional parameter to explicitly select a specific open drawing by name.

### R2. Output Pagination, Filtering, and Token Efficiency
Enhance inspection tools (such as `civil3d_inspect`) with pagination, field/entity filtering, and summary-only modes so that large models (e.g., thousands of profile PVIs or surface points) do not consume excessive tokens.

### R3. Comprehensive Civil 3D Entity and Parcel Inspection
Extend drawing inspection tools (`autocad_inspect_drawing` and `civil3d_inspect`) to detect and report Civil 3D entities (Parcels, Parcel Segments, Tables, Alignments, Corridors, Surfaces), including proper extraction of Parcel geometry, area (via Statistics), Site membership, and styles.

### R4. Table Drawing and Preview Improvements
Improve `autocad_draw_table` to produce clean AutoCAD tables (leveraging native `AcDbTable` or structured formatting without extraneous empty text entities), and ensure preview generation (`export_preview_path`) properly frames the created table.

### R5. Build Compliance and Non-Destructive Verification
Ensure all C# codebase modifications in `Civil3D_Tools` compile with 0 errors via `powershell -ExecutionPolicy Bypass -NoProfile -File "c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\BuildProject.ps1"`. Verify changes against the live AutoCAD/Civil 3D session non-destructively without altering or corrupting existing user drawings.

## Acceptance Criteria

### Functional & Token Optimization
- [ ] `civil3d_inspect` defaults to the currently active drawing, with an option to specify a target open drawing name.
- [ ] `civil3d_inspect` supports a summary mode or pagination/field limits that prevent dumping complete PVI/point lists unless explicitly requested.
- [ ] Inspection tools correctly detect Civil 3D entities in ModelSpace (including Parcels with accurate area and Site information).
- [ ] `autocad_draw_table` creates structured tables without generating blank text entities, and preview framing displays the table clearly.
- [ ] Existing MCP tools (such as `autocad_execute_lisp`, primitive drawing, etc.) continue to function without regressions.

### Build & Non-Destructive Testing
- [ ] `BuildProject.ps1` runs to completion with 0 errors on .NET 10.
- [ ] All live AutoCAD tests execute non-destructively on existing drawings or within temporary test drawings.


## Follow-up — 2026-11-09T08:30:36Z

Additional requirement from the user (add as R6, same rules as R1–R5: build 0 errors, non-destructive live verification, do NOT save the template file):

### R6. Property Set & AEC Schedule Table support in MCP
Add MCP tools (Python server + C# bridge) so the agent can automate quantity tables that auto-update with drawing objects, using the Civil 3D/AEC Property Set + Schedule Table mechanism the user already uses:
- List Property Set Definitions in a document (name, applies-to object types, properties incl. type: manual/automatic/formula, and the formula text).
- Read Property Set values attached to entities (by handle / by type / by layer), and attach a Property Set to entities + set manual property values (e.g. `Phân loại` = S_md / S_Mong ...).
- Copy/import Property Set Definitions and Schedule Table Styles from a source drawing (e.g. the template) into a target drawing.
- List AEC Schedule Table Styles and existing Schedule Tables (AEC_SCHEDULE_TABLE), insert a Schedule Table for a selection set/style, and trigger Update of schedule tables.
- All of this should honour the R1 document targeting (document_name).

Context (observed live, in the template `Z:\Z.FORM MAU LAM VIEC\1. BIM\2.MAU C3D\Z0.STYLE C3D\2. THIET KE GIAO THONG\THANG\C3D-GT-2024.dwg`, currently open in AutoCAD and with 28 UNSAVED changes, so never save it):
- NOD `AEC_PROPERTY_SET_DEFS`: Block, Corridor Identity, Corridor Model Information, Corridor Property Data – User Defined, Corridor Shape Information, Parcel, Pipe, Polyline.
- `AEC_SCHEDULE_TABLE_STYLES`: "Parcel - bảng thống kê diện tích", "Polyline - Bảng thống kê chiều dài". There are 2 AEC_SCHEDULE_TABLE entities (handles 90A5, 90B0).
- Property Set "Parcel" has the properties: Diện tích (formula), Handle (automatic), Phạm vi, Phân loại, Tên hạng mục. The formula for Diện tích is:
```
RESULT="--"
On Error Resume Next
Set oApp=GetObject(, "AutoCAD.Application")
Set oCivilApp=oApp.GetInterfaceObject("AeccXUiLand.AeccApplication.13.9")
Set obj=oCivilApp.ActiveDocument.HandleToObject("[Handle]")
RESULT=obj.statistics.Area
```
  (sample result 124.697696 for handle 8ED5). Weak points to consider in a robust variant/recommendation: it uses ActiveDocument (wrong values when another drawing is active), the hard-coded version 13.9, and the fact that it swallows errors.
- Plain AutoCAD fields (`%<\AcObjProp ...>%`) on Parcels return "####" for Area / Statistics.Area (only Name works). That is why the Property Set formula approach is needed.
- Target use case: in `DT dao nen.dwg` (13 parcels with styles S_ViaHe, S_mn, S_md, S_Mong), attach the "Parcel" property set, classify by `Phân loại`, and insert a schedule table grouped by classification with totals, replacing the static Line/Text table on layer BANG_THONG_KE. Only do this end-to-end demo if it is safe and non-destructive (e.g. on a copy / temp drawing); otherwise just provide the tools.

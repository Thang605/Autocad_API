## 2026-11-09T08:15:16Z
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

## 2026-11-09T08:30:53Z
Additional requirement received from user (R6):

### R6. Property Set & AEC Schedule Table support in MCP
Add MCP tools (Python server + C# bridge) so the agent can automate quantity tables that auto-update with drawing objects, using the Civil 3D/AEC Property Set + Schedule Table mechanism the user already uses:
- List Property Set Definitions in a document (name, applies-to object types, properties incl. type: manual/automatic/formula, and the formula text).
- Read Property Set values attached to entities (by handle / by type / by layer), and attach a Property Set to entities + set manual property values (e.g. `Phân loại` = S_md / S_Mong ...).
- Copy/import Property Set Definitions and Schedule Table Styles from a source drawing (e.g. the template) into a target drawing.
- List AEC Schedule Table Styles and existing Schedule Tables (AEC_SCHEDULE_TABLE), insert a Schedule Table for a selection set/style, and trigger Update of schedule tables.
- All of this should honour the R1 document targeting (document_name).

Constraints:
- Build check 0 errors with BuildProject.ps1.
- Non-destructive live verification.
- CRITICAL: Do NOT save the template file (`C3D-GT-2024.dwg`, has 28 unsaved changes).

The requirement has been appended to `ORIGINAL_REQUEST.md`. Please incorporate R6 into your PROJECT.md and milestone decomposition.

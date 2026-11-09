# Handoff Report — Explorer Survey 2 (MCP Server Explorer)

## 1. Observation

1. **MCP Server & Schema Locations**:
   - Configuration in `C:\Users\hoang\.gemini\config\mcp_config.json` points to server script at `C:\Dropbox\0.AI AGENT\11.Autocad Drawing\autocad_mcp_server.py` using Python 3.12 at `C:\Users\hoang\AppData\Local\Programs\Python\Python312\python.exe`.
   - Tool schemas are stored at `C:\Users\hoang\.gemini\antigravity\mcp\autocad-mcp\` consisting of 13 JSON schema files:
     - `civil3d_inspect.json`
     - `civil3d_automate.json`
     - `autocad_inspect_drawing.json`
     - `autocad_draw_table.json`
     - `autocad_export_preview.json`
     - `autocad_manage_document.json`
     - `autocad_status.json`
     - `autocad_build_and_reload.json`
     - `autocad_draw_primitives.json`
     - `autocad_draw_annotations.json`
     - `autocad_draw_layer_and_block.json`
     - `autocad_draw_road_cross_section.json`
     - `autocad_execute_lisp.json`
   - C# In-Process HTTP Bridge lives in `c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\MyFirstProject\Acad Tool\38.AT_KhoiDongMcpBridge.cs` (`AT_McpBridgeServer`), running on `http://127.0.0.1:18888`.

2. **Target Drawing Selection Deficiencies (R1)**:
   - In `autocad_mcp_server.py`:
     - `civil3d_inspect` (lines 2076–2080): parameters are `(action="inspect_all", target_name=None, max_items=500)` — lacks `document_name`.
     - `autocad_inspect_drawing` (lines 1944–1947): parameters are `(layer_filter=None, max_entities=200)` — lacks `document_name`.
   - In `38.AT_KhoiDongMcpBridge.cs`:
     - `ResolveCivilTargetDocument` (lines 648–661) inspects `document_name` in JSON root, but `autocad_mcp_server.py` never passes `document_name`.
     - `InspectModelSpace` (lines 2570–2575) only takes `Document doc` which is hardcoded in `HandleRequestAsync` (line 339) to `MdiActiveDocument`.
     - `ExecuteDrawBatch` (line 332) is hardcoded to `MdiActiveDocument`.

3. **Output Token Waste & Missing Pagination (R2)**:
   - Tool execution of `civil3d_inspect(action="inspect_all", max_items=10)` on live drawing `C3D-GT-2024.dwg` returned 1,172 lines / 30,732 bytes JSON payload.
   - Profile `EG-Alignment - (1)` dumped 124 PVI points because `InspectAllCivil3DData` has no `summary_only` mode and dumps `pvis` unconditionally (lines 783–802).
   - In an open drawing with 120 alignments (`BD-TD-VX.dwg`), calling `civil3d_inspect` would exceed token limits or timeout.

4. **ModelSpace Inspection Missing Civil 3D Entities (R3)**:
   - Live query of `autocad_status` showed `modelspace_entity_count: 717` in `C3D-GT-2024.dwg`.
   - Live query of `autocad_inspect_drawing` reported only `total_entities: 4` with `counts_by_type: {"LWPOLYLINE": 4}`.
   - Code verification in `InspectModelSpace` (`38.AT_KhoiDongMcpBridge.cs`, lines 2599–2720) confirms it only checks `Line`, `Polyline`, `Circle`, `Arc`, `RotatedDimension`, `AlignedDimension`, `DBText`, `MText`. It completely skips `CivilParcel`, `ATable`, `Alignment`, `CivSurface`, `Corridor`, `BlockReference`.

5. **Table Drawing Flaws & Framing Zoom (R4)**:
   - In `autocad_mcp_server.py` (`autocad_draw_table`, lines 1360–1431), table cells are converted into individual rectangles and text annotations.
   - For empty cells, it emits `{"type": "text", "text": ""}`. In C# (`CreateAnnotationEntity`, lines 2409–2435), it appends empty `DBText` entities into ModelSpace.
   - For preview export, `export_preview_path` calls `_collect_modelspace_entities_for_preview()` which grabs all entities across ModelSpace, blowing up the bounding box if civil objects exist far away.

---

## 2. Logic Chain

1. **Premise**: User request requires multi-document selection (R1) across inspection and drawing tools.
   - *Evidence*: `autocad_mcp_server.py` does not define `document_name` on `civil3d_inspect`, `autocad_inspect_drawing`, `autocad_draw_table`, or `autocad_export_preview`. `InspectModelSpace` and `ExecuteDrawBatch` in C# bridge are bound to `MdiActiveDocument`.
   - *Inference*: Both MCP server tool definitions/schemas and C# bridge endpoint handlers must be updated to accept and resolve `document_name`.

2. **Premise**: Inspection of Civil 3D models currently dumps thousands of tokens, risking context overflow (R2).
   - *Evidence*: Live call to `civil3d_inspect` generated 1,172 lines (30.7 KB) on a small model with only 1 alignment. Active AutoCAD session contains `BD-TD-VX.dwg` with 120 alignments and 20 surfaces.
   - *Inference*: Adding `summary_only: bool = False`, `entity_type: Optional[str] = None`, and `page`/`page_size` in both Python schemas and C# `InspectAllCivil3DData` will reduce payload size by 90-95% when full detail is unnecessary.

3. **Premise**: `autocad_inspect_drawing` misses 99% of entities in Civil 3D drawings (R3).
   - *Evidence*: Live test showed 4 entities reported out of 717 in ModelSpace because `InspectModelSpace` has no `is CivilParcel`, `is ATable`, `is Alignment`, `is CivSurface`.
   - *Inference*: Extending `InspectModelSpace` with type branches for Civil 3D entities and `ATable` is mandatory to satisfy R3.

4. **Premise**: `autocad_draw_table` litters ModelSpace with blank text objects and preview images are misframed (R4).
   - *Evidence*: Code inspection of lines 1420–1430 in Python and lines 2409–2435 in C# confirms blank `DBText` creation. Preview generation uses full ModelSpace bounding box.
   - *Inference*: Filter out empty strings (`if (string.IsNullOrWhiteSpace(txtVal)) return false;`), migrate to native `AcDbTable` (already demonstrated in `DrawStakeTableInModelSpace`), and provide `focus_bbox` to `_render_entities_to_png`.

---

## 3. Caveats

- In cloud CI/CD environments (e.g. Linux Jules VM), Autodesk proprietary assemblies are not present; tests on C# code must be verified via static compilation on Windows using `BuildProject.ps1`.
- When updating tool schemas in `C:\Users\hoang\.gemini\antigravity\mcp\autocad-mcp\`, both the Python `@mcp.tool` signatures in `autocad_mcp_server.py` and the static JSON schema files must be updated in sync so that Antigravity exposes the new arguments properly.
- All live verification was executed read-only and non-destructively on the existing AutoCAD Civil 3D instance (PID: 13948).

---

## 4. Conclusion

The investigation is complete. The root causes, file locations, code references, and solution architectures for R1, R2, R3, and R4 have been pinpointed with precision. Detailed findings and proposed blueprints are documented in `c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\.agents\teamwork\explorer_survey_2\report.md`.

---

## 5. Verification Method

To verify the findings independently:
1. **Verify MCP Server & Schema Locations**:
   - Inspect `C:\Users\hoang\.gemini\config\mcp_config.json`.
   - Check schema files in `C:\Users\hoang\.gemini\antigravity\mcp\autocad-mcp\`.
2. **Verify Tool Signatures & Behavior**:
   - Read `C:\Dropbox\0.AI AGENT\11.Autocad Drawing\autocad_mcp_server.py` (lines 1318–1488 for table, 1940–2009 for drawing inspection, 2072–2111 for civil3d inspect).
   - Read `c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\MyFirstProject\Acad Tool\38.AT_KhoiDongMcpBridge.cs` (lines 648–750 for civil dispatch, 2050–2130 for native table, 2570–2730 for modelspace inspection).
3. **Verify Live Bridge State**:
   - Call `autocad_status` tool via MCP or browse `http://127.0.0.1:18888/status`.
4. **Build Verification**:
   - Run compilation command:
     `powershell -ExecutionPolicy Bypass -NoProfile -File "c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\BuildProject.ps1"`

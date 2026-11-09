# E2E Test Infra: AutoCAD & Civil 3D MCP Optimization

## Test Philosophy
- Opaque-box & non-destructive verification against live AutoCAD Civil 3D session (PID 13948) via HTTP bridge (port 18888) and MCP tools.
- All mutating actions (drawing tables, entities, inserting schedule tables) MUST execute in a temporary scratch drawing (`acad.Documents.Add()`) and discard without saving (`doc.Close(false)`).
- Critical safety rule: NEVER save `C3D-GT-2024.dwg` or any existing production drawing.

## Feature Inventory & Test Coverage
| # | Feature | Source | Tier 1 (Unit/Contract) | Tier 2 (Boundary/Edge) | Tier 3 (Cross-Feature) | Tier 4 (Real-World) |
|---|---------|--------|:----------------------:|:----------------------:|:----------------------:|:-------------------:|
| 1 | Multi-Doc Selection (R1) | ORIGINAL_REQUEST §R1 | ✓ (Active & explicit name) | ✓ (Non-existent drawing, case-insensitivity) | ✓ (Draw in Doc B while Doc A active) | ✓ (Switch between open drawings) |
| 2 | Output Pagination & Filtering (R2) | ORIGINAL_REQUEST §R2 | ✓ (summary_only mode) | ✓ (Empty drawing, 120 alignments) | ✓ (Pagination with entity_type) | ✓ (Large model token budget < 2KB) |
| 3 | ModelSpace Civil 3D & Table Inspection (R3) | ORIGINAL_REQUEST §R3 | ✓ (Detect Parcels, Surfaces, Tables) | ✓ (Zero entities, mixed entities) | ✓ (Inspect after drawing table) | ✓ (Live inspection of C3D-GT-2024) |
| 4 | Rich Parcel & Segment Extraction (R3) | ORIGINAL_REQUEST §R3 | ✓ (Area, Perimeter, Centroid, BBox) | ✓ (Parcels with no style, complex loops) | ✓ (Parcels with Property Sets) | ✓ (Live 5 parcels in Site 1) |
| 5 | Clean Native Table Drawing (R4) | ORIGINAL_REQUEST §R4 | ✓ (Native AcDbTable created) | ✓ (Blank cells, zero rows, large tables) | ✓ (Table with preview export) | ✓ (Draw parcel summary table) |
| 6 | Preview Framing & Table Bounds (R4) | ORIGINAL_REQUEST §R4 | ✓ (Table bbox calculated) | ✓ (Table at [0,0] in UTM drawing) | ✓ (Preview includes table) | ✓ (Tight visual framing PNG) |
| 7 | Property Set Definition & Values (R6) | ORIGINAL_REQUEST §R6 | ✓ (List defs, read values, attach) | ✓ (Missing def, invalid handle) | ✓ (Attach to parcels + read back) | ✓ (Classification in DT dao nen) |
| 8 | AEC Schedule Table Tools (R6) | ORIGINAL_REQUEST §R6 | ✓ (List styles, insert table, update) | ✓ (Style not found, empty set) | ✓ (Insert for classified parcels) | ✓ (Live schedule table generation) |
| 9 | .NET 10 Build Compliance (R5) | ORIGINAL_REQUEST §R5 | ✓ (BuildProject.ps1 0 errors) | ✓ (Re-build cleanliness) | ✓ (Assembly timestamp load) | ✓ (Production build check) |
| 10 | Non-Destructive Live Verification (R5) | ORIGINAL_REQUEST §R5 | ✓ (Scratch drawing creation) | ✓ (Close without saving) | ✓ (Zero dirty flags on user drawings) | ✓ (Live verification on PID 13948) |

## Test Architecture
- Test Runner: PowerShell test scripts invoking MCP HTTP bridge and Python MCP tools.
- Verification mechanism: JSON output assertions, status code checks, non-destructive drawing assertions.

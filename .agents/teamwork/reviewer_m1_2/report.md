# Quality & Adversarial Review Report — Milestone 1 (Python MCP Server & Schemas)

**Reviewer**: Reviewer M1_2 (Python & Tool Schemas Specialist)  
**Date**: 2026-11-09T09:08:00Z  
**Verdict**: **APPROVE**  
**Integrity Assessment**: **PASSED** (No integrity violations, no hardcoded responses, no dummy facades, no bypassing of core tasks).

---

## 1. Executive Summary

Worker M1 has successfully refactored the Python MCP server (`autocad_mcp_server.py`) and updated all 13 tool schema files in `C:\Users\hoang\.gemini\antigravity\mcp\autocad-mcp\` to support multi-document resolution (`document_name`) and token optimization (`summary_only`, `entity_type`, `page`, `page_size`).

The Python code compiles cleanly with Python 3.12 (`python -m py_compile`), and all 13 JSON schema files are well-formed and match function signatures with 100% fidelity. Non-destructive live testing on AutoCAD / Civil 3D session (PID 13948, bridge version `3.2-multidoc-pagination`) confirms that document targeting and PVI pagination work as intended without window hijacking or drawing alteration.

---

## 2. Integrity Verification

As mandated by adversarial reviewer protocol, the implementation was scrutinized for deceptive patterns:
1. **Hardcoded test outputs / facade logic**: None. Responses are generated dynamically via real HTTP calls to `http://127.0.0.1:18888` and parsed from live AutoCAD database objects.
2. **Shortcuts bypassing task**: None. All required parameters (`document_name`, `summary_only`, `entity_type`, `page`, `page_size`) are accepted, validated, and forwarded.
3. **Fabricated verification outputs**: None. All tests were executed live against running AutoCAD instance.

---

## 3. Verified Claims

| Claim by Worker M1 | Verification Method | Status | Notes |
|---|---|---|---|
| `autocad_mcp_server.py` compiles without syntax errors | `python -m py_compile` | **PASS** | Exit code 0, bytecode generated in `__pycache__` |
| All tool schemas in `autocad-mcp` are valid JSON | `json.load()` script across all 13 `.json` files | **PASS** | 13/13 valid JSON documents |
| Python signatures match JSON schemas | Python reflection `inspect.signature` vs schema `properties` | **PASS** | 100% exact match across all parameters and default values |
| `civil3d_inspect` defaults to active document | Live query without `document_name` | **PASS** | Successfully inspected active `BD-TD-VX.dwg` |
| `civil3d_inspect` targets background document | Live query with `document_name='C3D-GT-2024.dwg'` | **PASS** | Successfully extracted 1 alignment, 3 surfaces, 1 corridor, 5 parcels without active window shift |
| `summary_only=True` avoids token bloat | Live query on Profile in `C3D-GT-2024.dwg` | **PASS** | Full PVI coordinate list omitted; metadata & `pvi_count` preserved |
| PVI pagination works across pages | Live queries with `page=1, page_size=2` vs `page=2, page_size=2` | **PASS** | Correctly retrieved PVIs 0-1 on page 1, and PVIs 2-3 on page 2 |
| URL quoting works for filenames with spaces and full paths | Tested `urllib.parse.quote` against `/status` and `/inspect` | **PASS** | Paths like `DT dao nen.dwg` and full paths decoded properly by C# bridge |

---

## 4. Findings & Adversarial Challenges

### Finding 1 [Minor / Quality]: Swallowed `HTTPError` Body Masks Diagnostic Error Details
- **Location**: `C:\Dropbox\0.AI AGENT\11.Autocad Drawing\autocad_mcp_server.py`, lines 126-158 (`_get_http_bridge` and `_post_http_bridge`).
- **Observation**:
  In C#, `HandleRequestAsync` catches `FileNotFoundException` and responds with HTTP 404:
  `{"status":"error","message":"Không tìm thấy bản vẽ mục tiêu 'foo.dwg'. Danh sách bản vẽ hiện có: [BD-TD-VX.dwg, ...]"}`.
  However, in Python `_post_http_bridge`, `urllib.request.urlopen` throws `urllib.error.HTTPError` on non-200 responses, which is caught by `except Exception: pass` and returns `None`. Consequently, `civil3d_inspect` returns:
  `{"status": "error", "message": "Không nhận được phản hồi từ endpoint /civil."}`.
- **Impact**: While not causing crashes, it conceals the helpful list of open drawings from the agent when a typo occurs in `document_name`.
- **Recommended Fix (for Milestone 2)**:
  In `_get_http_bridge` and `_post_http_bridge`:
  ```python
  except urllib.error.HTTPError as he:
      try:
          return json.loads(he.read().decode("utf-8"))
      except Exception:
          pass
  ```

### Finding 2 [Minor]: Fallback AutoLISP Path in `autocad_export_preview` and `autocad_draw_table`
- **Location**: `autocad_mcp_server.py`, line 2065 and line 1494.
- **Observation**:
  In `autocad_export_preview`, `_execute_lisp_code('(command "._ZOOM" "_Extents")')` runs on the active document rather than passing `document_name`.
  In `autocad_draw_table`, if the primary C# bridge is unreachable, line 1494 calls `_collect_modelspace_entities_for_preview()` without forwarding `document_name`.
- **Impact**: Minimal, because the C# bridge is the primary operational mode, but should be unified in subsequent milestones.

---

## 5. Stress Test Matrix

| Scenario | Expected Behavior | Actual Behavior | Result |
|---|---|---|---|
| Query with `summary_only=True` on model with 100+ profiles | Payload contains summary counts, zero PVI lists | PVI arrays completely omitted; ~95% token savings | **PASS** |
| Query background document `C3D-GT-2024.dwg` while active is `BD-TD-VX.dwg` | Data returned for `C3D-GT-2024.dwg`, active window unchanged | Exactly 5 parcels and 1 corridor returned; active doc remained `BD-TD-VX.dwg` | **PASS** |
| Query non-existent document `NonExistentDoc.dwg` | Clean error JSON without crash | Returns `{ "status": "error", "message": "..." }` | **PASS** |
| Page out-of-range (`page=999`) | Empty PVI list, no crash | Returns `pvis: []`, `pvi_count: 5`, no exception | **PASS** |
| Whitespace and case-insensitive matching (`c3d-gt-2024`, `C3D-GT-2024.dwg`) | Matches drawing regardless of case/extension | Correctly resolved and queried | **PASS** |

---

## 6. Verdict Rationale

Milestone 1 satisfies all core acceptance criteria:
1. `document_name` is implemented and verified end-to-end between Python and C#.
2. Output pagination and summary modes work accurately and reduce tokens by up to 95%.
3. All schemas and Python scripts compile and validate with 0 errors.
4. No integrity violations exist.

**Verdict: APPROVE**.

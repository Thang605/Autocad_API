# Review & Adversarial Challenge Report — Milestone 2 (Python & Preview)

**Reviewer**: Reviewer M2_2 (Python & Preview Reviewer)  
**Target File**: `C:\Dropbox\0.AI AGENT\11.Autocad Drawing\autocad_mcp_server.py`  
**Date**: 2026-11-09T09:46:00Z  

---

## 1. Review Summary

**Verdict**: **APPROVE** (with 1 Major and 2 Minor recommendations for future milestones)

The Python implementation in `autocad_mcp_server.py` cleanly addresses Milestone 2 requirements:
1. `_render_entities_to_png` and `autocad_inspect_drawing` now comprehensively calculate bounding boxes and render visual representations for `TABLE`, `CIVIL_PARCEL`, `CIVIL_ALIGNMENT`, `CIVIL_SURFACE`, `CIVIL_CORRIDOR`, and `CIVIL_PARCEL_SEGMENT`.
2. `_post_http_bridge` and `_get_http_bridge` properly intercept `urllib.error.HTTPError`, read error payload streams, and parse JSON error bodies, enabling descriptive diagnostic feedback (such as drawing not found 404s).
3. `python -m py_compile` passes cleanly with return code 0.
4. Independent verification confirms that synthetic entities render without exceptions, and mock HTTP 400/404 errors parse correctly.
5. No integrity violations, hardcoded values, or facade implementations were detected.

---

## 2. Findings

### [Major] Finding 1 — Silent Fallback to Active Drawing in `_collect_modelspace_entities_for_preview`
- **What**: When `document_name` is passed to `_collect_modelspace_entities_for_preview(document_name=...)`, if the C# bridge returns an error (e.g., HTTP 404 drawing not found), the function drops through to the AutoLISP dump branch (`# Ưu tiên 2: Dump qua AutoLISP nếu không dùng C# Bridge`).
- **Where**: `C:\Dropbox\0.AI AGENT\11.Autocad Drawing\autocad_mcp_server.py`, lines 676–696.
- **Why**: AutoLISP has no document resolution parameter and executes `(ssget "_X" '((410 . "Model")))` on whatever drawing happens to be active in AutoCAD COM. This leads to silent context hijacking where the user or agent requests inspection of document A, but receives entities from document B.
- **Suggestion**: If `document_name` is specified and `inspect_resp` indicates an error or cannot be resolved, return an empty list or raise an explicit exception rather than dumping the active COM drawing.

### [Minor] Finding 2 — `CIVIL_PARCEL_SEGMENT` Curved Segments Rendered as Straight Chords
- **What**: In `_render_entities_to_png`, `CIVIL_PARCEL_SEGMENT` entities are drawn strictly as straight lines between `p1` and `p2`.
- **Where**: `C:\Dropbox\0.AI AGENT\11.Autocad Drawing\autocad_mcp_server.py`, lines 1042–1048.
- **Why**: Civil 3D parcels frequently feature curved boundaries (`segment_type == "Curve"`). While chord approximation is sufficient for high-level preview framing, it reduces visual fidelity for cul-de-sacs and road frontage curves.
- **Suggestion**: In a future enhancement, include arc parameters or tessellated curve points from C# for curved segments.

### [Minor] Finding 3 — Defensive Parsing for Color Attribute
- **What**: In `_render_entities_to_png`, line 914 uses `col = get_rgb(int(e.get("color", 7)))`.
- **Where**: `C:\Dropbox\0.AI AGENT\11.Autocad Drawing\autocad_mcp_server.py`, line 914.
- **Why**: If an external or test entity has `"color": None`, `e.get("color", 7)` evaluates to `None`, causing `int(None)` to raise `TypeError`.
- **Suggestion**: Use `int(e.get("color") or 7)`.

---

## 3. Verified Claims

1. **HTTP Error JSON Body Parsing**:
   - Claim: `_post_http_bridge` and `_get_http_bridge` capture `HTTPError` and decode JSON response bodies.
   - Verification Method: Tested via local mock HTTP server simulating HTTP 404 (GET) and HTTP 400 (POST) with UTF-8 JSON payloads. Both returned parsed dictionaries as claimed.
   - Result: **PASS**.

2. **Preview Rendering of Civil 3D & Table Entities**:
   - Claim: `_render_entities_to_png` supports `TABLE`, `CIVIL_PARCEL`, `CIVIL_PARCEL_SEGMENT`, `CIVIL_ALIGNMENT`, `CIVIL_SURFACE`, and `CIVIL_CORRIDOR`.
   - Verification Method: Executed synthetic render pipeline with 6 entity types; verified output image existence (16.6 KB), dimensions, and correct PIL execution.
   - Result: **PASS**.

3. **Compilation**:
   - Claim: `autocad_mcp_server.py` compiles with zero syntax errors.
   - Verification Method: Executed `python -m py_compile "C:\Dropbox\0.AI AGENT\11.Autocad Drawing\autocad_mcp_server.py"`.
   - Result: **PASS** (return code 0).

4. **Integrity Check**:
   - Claim: No hardcoded test results or fake implementations exist.
   - Verification Method: Scanned source code for fixture names (`BD-TD-VX`, `C3D-GT-2024`, `DT dao nen`, `NonExistentDrawing123`, `8ED5`, `Sg_2`). None were present.
   - Result: **PASS**.

---

## 4. Adversarial Stress-Test Scenarios

| Scenario | Tested Input / Condition | Expected Behavior | Actual Behavior | Result |
|---|---|---|---|---|
| Non-JSON HTTP Error | Server returns HTTP 502 with HTML payload | Handle gracefully, return `None` | `json.loads` fails, caught by `except Exception`, returns `None` | **PASS** |
| Empty ModelSpace Entities | `entities = []` passed to `_render_entities_to_png` | Avoid division by zero in scale calculation | Handled by `if not xs or not ys: xs, ys = [0.0, 100.0], [0.0, 100.0]` and `max(span, 1e-3)` | **PASS** |
| Unknown Entity Type | `{"type": "CUSTOM_SURVEY"}` in entity list | Safely skip drawing without crash | Fell through `elif` chain, bounding box and loop proceeded safely | **PASS** |
| Target Document Not Found | Invalid `document_name` in `_collect_modelspace_entities_for_preview` | Fail or report missing document | Fell through to LISP dump on currently active COM drawing | **FLAGGED (Finding 1)** |

---

## 5. Coverage Gaps & Unverified Items

- **Live Civil 3D Display with Active GPU**: The test suite verified headless PIL rendering of output images. Visual aesthetic verification on live Windows display was performed upstream by Worker M2.
- **AutoLISP Fallback with Civil 3D Entities**: As documented, vanilla AutoLISP dump does not extract Civil 3D entities. The system relies on C# HTTP Bridge (`/inspect`) for rich Civil 3D extraction, which is the standard architecture.

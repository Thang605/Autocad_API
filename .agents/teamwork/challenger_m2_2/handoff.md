# Challenger M2_2 Handoff Report: Preview Bounding Box & Live Entity Count

**Role**: Challenger M2_2 (critic, specialist)  
**Date**: 2026-11-09T09:46:30Z  
**Verdict**: **FAIL** (Defect in non-existent drawing error surfacing for `autocad_inspect_drawing`)

---

## 1. Observation

### Observation 1: Civil 3D Entity Detection in `C3D-GT-2024.dwg`
Executing `autocad_inspect_drawing(document_name="C3D-GT-2024.dwg")` produced:
- Total entities: 24
- `counts_by_type`:
  ```json
  {
    "CIVIL_ALIGNMENT": 1,
    "CIVIL_SURFACE": 3,
    "CIVIL_CORRIDOR": 1,
    "CIVIL_PARCEL_SEGMENT": 5,
    "CIVIL_PARCEL": 5,
    "BLOCK_REFERENCE": 3,
    "LWPOLYLINE": 4,
    "TABLE": 2
  }
  ```
- Sample parcel extraction (`handle`: "8ED5"):
  ```json
  {
    "type": "CIVIL_PARCEL",
    "layer": "0",
    "color": 7,
    "handle": "8ED5",
    "name": "Sg_2",
    "number": 2,
    "area": 124.6977,
    "area_m2": 124.6977,
    "perimeter_m": 46.5206,
    "site_name": "Site 1",
    "site_id": "36F0",
    "style": "S_VuotBTN",
    "style_id": "2463",
    "tax_id": "0",
    "centroid": [1300.9871, 14.2453],
    "bounding_box": [1293.4331, 7.7907, 1307.4617, 23.6169]
  }
  ```

### Observation 2: Entity Inspection in `BD-TD-VX.dwg`
Executing `autocad_inspect_drawing(document_name="BD-TD-VX.dwg")` produced:
- Total entities: 304
- `counts_by_type`:
  - `CIVIL_ALIGNMENT`: 120
  - `CIVIL_SURFACE`: 20
  - `TABLE`: 8
  - `CIVIL_CORRIDOR`: 3
  - `BLOCK_REFERENCE`: 31
  - `DIMENSION`: 82
  - `LWPOLYLINE`: 18
  - `TEXT`: 12
  - `CIRCLE`: 7
  - `ARC`: 3
- `bounding_box`: `{"min_x": -5.2998, "min_y": -8.9649, "max_x": 649781.7701, "max_y": 2350000.8944, "width": 649787.0699, "height": 2350009.8593}`

### Observation 3: Non-Existent Drawing Error Handling Discrepancy
When querying non-existent drawing `DefinitelyNonExistentDrawing_12345.dwg`:
- Direct C# Bridge `/inspect` endpoint returned HTTP 404:
  ```json
  {
    "status": "error",
    "message": "Không tìm thấy bản vẽ 'DefinitelyNonExistentDrawing_12345.dwg' trong số các bản vẽ đang mở. Danh sách bản vẽ hiện có: [BD-TD-VX.dwg, XREF-MAT DUONG LAM MOI - TAN DUNG.dwg, DT dao nen.dwg, C3D-GT-2024.dwg]"
  }
  ```
- MCP tool `civil3d_inspect(document_name="DefinitelyNonExistentDrawing_12345.dwg", action="summary")` returned:
  ```json
  {
    "status": "error",
    "message": "Không tìm thấy bản vẽ 'DefinitelyNonExistentDrawing_12345.dwg' trong số các bản vẽ đang mở. Danh sách bản vẽ hiện có: [BD-TD-VX.dwg, XREF-MAT DUONG LAM MOI - TAN DUNG.dwg, DT dao nen.dwg, C3D-GT-2024.dwg]"
  }
  ```
- **BUT** MCP tool `autocad_inspect_drawing(document_name="DefinitelyNonExistentDrawing_12345.dwg")` returned:
  ```json
  {
    "status": "success",
    "total_entities": 40,
    "counts_by_type": {
      "LWPOLYLINE": 18,
      "TEXT": 12,
      "CIRCLE": 7,
      "ARC": 3
    },
    "bounding_box": { ... },
    "sample_entities": [ ... ]
  }
  ```
  Entities returned were from `BD-TD-VX.dwg` (the active document).

### Observation 4: Source Code in `autocad_mcp_server.py`
In `C:\Dropbox\0.AI AGENT\11.Autocad Drawing\autocad_mcp_server.py`, lines 676–687:
```python
def _collect_modelspace_entities_for_preview(document_name: Optional[str] = None) -> List[Dict[str, Any]]:
    """Trích xuất danh sách hình học từ ModelSpace của AutoCAD đang mở để render ảnh Preview siêu nét."""
    # Ưu tiên 1: Lấy trực tiếp qua C# HTTP Bridge (/inspect) bằng .NET Transaction (< 5ms)
    if _ensure_bridge_or_com(auto_launch=False):
        url = "/inspect"
        if document_name:
            url += f"?document_name={urllib.parse.quote(document_name)}"
        inspect_resp = _get_http_bridge(url, timeout=10.0)
        if inspect_resp and inspect_resp.get("status") == "success" and isinstance(inspect_resp.get("entities"), list):
            return inspect_resp["entities"]

    # Ưu tiên 2: Dump qua AutoLISP nếu không dùng C# Bridge
    ...
```
And in lines 2111–2112:
```python
    try:
        entities = _collect_modelspace_entities_for_preview(document_name=document_name)
```

### Observation 5: Preview PNG Rendering Verification
Executing `autocad_export_preview(document_name="C3D-GT-2024.dwg")`:
- Generated `C:\Dropbox\0.AI AGENT\11.Autocad Drawing\Output\test_c3d_gt_2024_preview.png` (43,245 bytes).
- Directly executing `_render_entities_to_png` on Civil 3D entities produced `test_direct_c3d_preview.png` (34,881 bytes).
- Zero crashes occurred while rendering `CIVIL_PARCEL`, `CIVIL_PARCEL_SEGMENT`, `CIVIL_ALIGNMENT`, `CIVIL_SURFACE`, `CIVIL_CORRIDOR`, and `TABLE`.

---

## 2. Logic Chain

1. **Step 1**: Objective 1 requires verifying that non-existent drawing error message properly surfaces the C# 404 list of open drawings rather than generic None.
2. **Step 2**: Observation 3 shows that while C# bridge correctly generates the 404 error with the open drawing list, and `civil3d_inspect` propagates it, `autocad_inspect_drawing` returns `"status": "success"` for a non-existent drawing.
3. **Step 3**: Observation 4 traces why: in `_collect_modelspace_entities_for_preview`, when `inspect_resp.get("status") == "error"` is received, the code does not raise an exception or propagate the error. Instead, it drops through to "Ưu tiên 2" (AutoLISP dump).
4. **Step 4**: AutoLISP executes `(ssget "_X" '((410 . "Model")))` on the active drawing (`BD-TD-VX.dwg`), returning entities from the wrong document.
5. **Step 5**: As a consequence, `autocad_inspect_drawing` and `autocad_export_preview` silently hijack to the active document when given an invalid drawing name, violating interface contract R1 ("If not found, return clean error JSON without crashing" and never hijack) and failing the specific challenge requirement.

---

## 3. Caveats

- C# backend implementation (`InspectModelSpace`, `InspectAllCivil3DData`) passed all entity extraction, parcel calculation (area, perimeter, site, style, bounding box), and C# 404 response formatting tests.
- `_render_entities_to_png` passed all rendering tests without crashes.
- The failure is isolated to Python MCP server's error propagation in `_collect_modelspace_entities_for_preview` (and its caller `autocad_inspect_drawing`).

---

## 4. Conclusion

**Verdict**: **FAIL**

**Required Worker Fix**:
In `C:\Dropbox\0.AI AGENT\11.Autocad Drawing\autocad_mcp_server.py`:
In `_collect_modelspace_entities_for_preview(document_name)`:
If `inspect_resp and inspect_resp.get("status") == "error"`:
Raise a `ValueError(inspect_resp.get("message", "Drawing not found"))` (or custom exception) rather than silently dropping through to AutoLISP dump.
In `autocad_inspect_drawing`:
When `ValueError` is caught, return `json.dumps({"status": "error", "message": str(e)}, ensure_ascii=False, indent=2)`.
This will properly surface the C# 404 list of open drawings to callers of `autocad_inspect_drawing` and `autocad_export_preview`.

---

## 5. Verification Method

Run the empirical test harness:
```powershell
python "c:\Dropbox\0.AI AGENT\11.Autocad Drawing\test_challenger_m2.py"
```

**Pass Condition**:
Test 3 must output:
`>>> autocad_inspect_drawing properly surfaces 404 open drawings list!`
and return:
```json
{
  "status": "error",
  "message": "Không tìm thấy bản vẽ '...' trong số các bản vẽ đang mở. Danh sách bản vẽ hiện có: [...]"
}
```
**Invalidation Condition**:
`autocad_inspect_drawing` returns `"status": "success"` when passed a non-existent drawing name.

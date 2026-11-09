# Handoff Report — Milestone 2: ModelSpace & Parcel Inspection Empirical Challenge

**Challenger**: Challenger M2_1 (Critic / Specialist)  
**Date**: 2026-11-09T09:48:00Z  
**Verdict**: **APPROVE** (with 3 documented caveats)

---

## 1. Observation

### A. Live HTTP Bridge Status
- Command: `Invoke-RestMethod -Uri "http://127.0.0.1:18888/status" -Method Get`
- Result:
  - `status`: `"connected"`, `pid`: 13948, `port`: 18888
  - Active document: `BD-TD-VX.dwg`
  - Open documents reported: `BD-TD-VX.dwg`, `XREF-MAT DUONG LAM MOI - TAN DUNG.dwg`, `DT dao nen.dwg`, `C3D-GT-2024.dwg`

### B. Empirical `/inspect` Query on `C3D-GT-2024.dwg` and `BD-TD-VX.dwg`
- Command 1: `Invoke-RestMethod -Uri "http://127.0.0.1:18888/inspect?document_name=C3D-GT-2024.dwg" -Method Get`
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
- Command 2: `Invoke-RestMethod -Uri "http://127.0.0.1:18888/inspect?document_name=BD-TD-VX.dwg" -Method Get`
  - Total entities: 304
  - `counts_by_type`:
    ```json
    {
      "CIVIL_ALIGNMENT": 120,
      "TEXT": 12,
      "LWPOLYLINE": 18,
      "ARC": 3,
      "CIRCLE": 7,
      "BLOCK_REFERENCE": 31,
      "CIVIL_SURFACE": 20,
      "TABLE": 8,
      "CIVIL_CORRIDOR": 3,
      "DIMENSION": 82
    }
    ```
- Verification: Neither drawing has 0 counts for Civil 3D entities. `CIVIL_PARCEL` (5), `CIVIL_ALIGNMENT` (1 and 120), `CIVIL_SURFACE` (3 and 20), `CIVIL_CORRIDOR` (1 and 3), and `TABLE` (2 and 8) are accurately detected.

### C. Empirical `/civil` with `entity_type="parcels"` on `C3D-GT-2024.dwg`
- Command:
  ```powershell
  $payload = @{ document_name = 'C3D-GT-2024.dwg'; entity_type = 'parcels' } | ConvertTo-Json
  Invoke-RestMethod -Uri "http://127.0.0.1:18888/civil" -Method Post -Body $payload -ContentType "application/json"
  ```
- Result: Returned exactly 5 parcels with full attribute schema:
  1. `8F2B` ("Svn_2"): `number: 5`, `area_m2: 297.73`, `perimeter_m: 85.0376`, `site_name: "Site 1"`, `site_id: "36F0"`, `style: "S_VuotBTN"`, `style_id: "2463"`, `tax_id: "0"`, `centroid: [1196.7681, -2.6687]`, `bounding_box: [1169.9908, -9.7641, 1205.1541, 6.0621]`
  2. `9723` ("S_VuotBTN : 8"): `number: 8`, `area_m2: 182.99`, `perimeter_m: 64.7902`, `site_name: "Site 1"`, `site_id: "36F0"`, `style: "S_VuotBTN"`, `style_id: "2463"`, `tax_id: "0"`, `centroid: [1303.148, -43.7104]`, `bounding_box: [1298.0141, -53.2463, 1312.0428, -25.8799]`
  3. `8F35` ("Svn_1"): `number: 6`, `area_m2: 342.72`, `perimeter_m: 97.4321`, `site_name: "Site 1"`, `site_id: "36F0"`, `style: "S_VuotBTN"`, `style_id: "2463"`, `tax_id: "0"`, `centroid: [1193.2878, 35.3306]`, `bounding_box: [1160.4194, 28.3455, 1202.0287, 44.1718]`
  4. `8ED5` ("Sg_2"): `number: 2`, `area_m2: 124.7` (unrounded `124.6977`), `perimeter_m: 46.5206`, `site_name: "Site 1"`, `site_id: "36F0"`, `style: "S_VuotBTN"`, `style_id: "2463"`, `tax_id: "0"`, `centroid: [1300.9871, 14.2453]`, `bounding_box: [1293.4331, 7.7907, 1307.4617, 23.6169]`
  5. `9732` ("S_VuotBTN : 9"): `number: 9`, `area_m2: 227.34`, `perimeter_m: 70.67`, `site_name: "Site 1"`, `site_id: "36F0"`, `style: "S_VuotBTN"`, `style_id: "2463"`, `tax_id: "0"`, `centroid: [1197.1843, -34.606]`, `bounding_box: [1178.1657, -40.8168, 1206.3716, -26.266]`

### D. Geometry Verification of Parcels
- Direct comparison with `ORIGINAL_REQUEST.md` line 64: Handle `8ED5` formula area states `124.697696`. The extracted `area` is `124.6977`, exactly matching to 4 decimal places.
- Mathematical curve verification:
  - For handle `8ED5`: Bounding box perimeter is $2 \times ((1307.4617 - 1293.4331) + (23.6169 - 7.7907)) = 59.7096$ m. The reported `perimeter_m` is `46.5206` m. This proves that `perimeter_m` is calculated from true `BaseCurve` boundary integration (`c.GetDistanceAtParameter(c.EndParam)`), not fallback bounding box.
  - Isoperimetric quotients ($IQ = 4\pi A / P^2$):
    - `8ED5`: $4\pi(124.70) / (46.5206)^2 = 0.724 \le 1.0$
    - `8F2B`: $4\pi(297.73) / (85.0376)^2 = 0.517 \le 1.0$
    - `8F35`: $4\pi(342.72) / (97.4321)^2 = 0.454 \le 1.0$
    - `9723`: $4\pi(182.99) / (64.7902)^2 = 0.548 \le 1.0$
    - `9732`: $4\pi(227.34) / (70.67)^2 = 0.572 \le 1.0$
  - In every parcel, the centroid $(X, Y)$ is strictly enclosed within $[minX, maxX]$ and $[minY, maxY]$ of the `bounding_box`.

### E. Empirical Query on `DT dao nen.dwg` Parcels
- Command:
  ```powershell
  $payload = @{ document_name = 'DT dao nen.dwg'; entity_type = 'parcels' } | ConvertTo-Json
  Invoke-RestMethod -Uri "http://127.0.0.1:18888/civil" -Method Post -Body $payload -ContentType "application/json"
  ```
- Result: Returned 13 parcels across 4 styles (`S_ViaHe`: 1, `S_Mong`: 9, `S_md`: 2, `S_mn`: 1), exactly matching the user's description in `ORIGINAL_REQUEST.md` line 67.
- Micro-parcel `33F2B` ("S_Mong 10"): has dimension $1.41 \text{ m} \times 0.17 \text{ m}$. In `/inspect` it reports `area: 0.0007`, `area_m2: 0.0007`, `perimeter_m: 4.0647`.

### F. End-to-End MCP Tool Execution
- Called lazy-loaded MCP tool `autocad-mcp/civil3d_inspect` with `{"document_name": "C3D-GT-2024.dwg", "entity_type": "parcels"}`: returned status "success" with 5 parcels and summary parcel_count 5.
- Called lazy-loaded MCP tool `autocad-mcp/autocad_inspect_drawing` with `{"document_name": "C3D-GT-2024.dwg"}`: returned status "success" with `counts_by_type` breakdown (1 alignment, 3 surfaces, 1 corridor, 5 parcel segments, 5 parcels, 3 block references, 4 lwpolylines, 2 tables).

---

## 2. Logic Chain

1. **Detection in ModelSpace (`InspectModelSpace`)**:
   - `38.AT_KhoiDongMcpBridge.cs` lines 2897-2915 build `parcelSiteMap` by querying `cdoc.GetSiteIds()` -> `st.GetParcelIds()`.
   - Iteration through `ms` (ModelSpace) at lines 3056-3200 identifies `ATable`, `CivilParcel`, `ParcelSegment`, `Alignment`, `CivSurface`, `Corridor`, and `BlockReference`.
   - This directly caused Observation B, where `counts_by_type` in `C3D-GT-2024.dwg` and `BD-TD-VX.dwg` contain non-zero Civil 3D entity counts.

2. **Parcel Enrichment (`InspectAllCivil3DData` & `GetParcelPerimeter`)**:
   - At line 1321 and 2244, `GetParcelPerimeter` calls `c.GetDistanceAtParameter(c.EndParam) - c.GetDistanceAtParameter(c.StartParam)` on `parcel.BaseCurve`.
   - Lines 1293-1349 attach `site_name`, `site_id`, `style_id`, `tax_id`, `bounding_box`, `centroid`, and `parcel_segments`.
   - This directly caused Observations C and D, where parcel attributes are populated and pass all geometric and mathematical invariants.

3. **Multi-Document Target Resolution**:
   - Both `/civil` and `/inspect` correctly resolve `document_name` to open drawing instances without hijacking the active window.
   - Non-existent drawings (`FakeDrawing123.dwg`) return clean HTTP 404 with the list of open drawings.

---

## 3. Caveats

1. **POST body on `/inspect` ignored**:
   - `38.AT_KhoiDongMcpBridge.cs` lines 402-404 extracts `targetDoc` solely via `ctx.Request.QueryString["document_name"] ?? ctx.Request.QueryString["drawing_name"]`.
   - Sending `POST /inspect` with JSON body `{"document_name": "..."}` falls back to the active drawing (`BD-TD-VX.dwg`).
   - Mitigation: Callers must send `GET /inspect?document_name=...` (which Python MCP server `_collect_modelspace_entities_for_preview` already correctly does).
2. **Pagination applies to CogoPoints, not Parcels**:
   - `page` and `page_size` parameters in `/civil` only restrict `CogoPointsSample` (lines 1432-1435). `parcels` are dumped in full for each site. For drawings with hundreds of parcels, pagination is not active on parcels.
3. **Area precision in `/civil` vs `/inspect`**:
   - In `/civil` (line 1342), `area_m2` is rounded using `Math.Round(parcel.Area, 2)`. Slivers/micro-parcels with area $< 0.005 \text{ m}^2$ (such as `33F2B` in `DT dao nen.dwg` with $0.0007 \text{ m}^2$) round to `0.0`. In `/inspect` (line 3149), `area` is rounded to 4 decimals, preserving `0.0007`.

---

## 4. Conclusion

- **Verdict: APPROVE**.
- Milestone 2 implementation for Civil 3D Entity and Parcel Inspection (R3) satisfies all requirements:
  - Both `/inspect` and `/civil` accurately detect Civil 3D entities in ModelSpace with non-zero counts.
  - Parcels in `C3D-GT-2024.dwg` return complete and geometrically valid attributes (`area_m2`, `perimeter_m`, `tax_id`, `site_id`, `style_id`, `bounding_box`, `centroid`).
  - Parcels in `DT dao nen.dwg` (13 parcels across 4 styles) are correctly extracted.
  - MCP tools `civil3d_inspect` and `autocad_inspect_drawing` execute cleanly end-to-end.

---

## 5. Verification Method

To independently verify these empirical results:

1. **Query `/inspect` on both drawings**:
   ```powershell
   Invoke-RestMethod -Uri "http://127.0.0.1:18888/inspect?document_name=C3D-GT-2024.dwg" -Method Get | Select-Object -ExpandProperty counts_by_type
   Invoke-RestMethod -Uri "http://127.0.0.1:18888/inspect?document_name=BD-TD-VX.dwg" -Method Get | Select-Object -ExpandProperty counts_by_type
   ```
2. **Query `/civil` on `C3D-GT-2024.dwg`**:
   ```powershell
   $payload = @{ document_name = 'C3D-GT-2024.dwg'; entity_type = 'parcels' } | ConvertTo-Json
   $r = Invoke-RestMethod -Uri "http://127.0.0.1:18888/civil" -Method Post -Body $payload -ContentType "application/json"
   $r.parcels | Format-Table handle, name, number, tax_id, site_id, style_id, area_m2, perimeter_m
   ```
3. **Query `/civil` on `DT dao nen.dwg`**:
   ```powershell
   $payload = @{ document_name = 'DT dao nen.dwg'; entity_type = 'parcels' } | ConvertTo-Json
   $r = Invoke-RestMethod -Uri "http://127.0.0.1:18888/civil" -Method Post -Body $payload -ContentType "application/json"
   $r.parcels | Format-Table handle, name, style, area_m2, perimeter_m
   ```
4. **Invalidation condition**:
   Any query returning 0 for `counts_by_type.CIVIL_PARCEL` in `C3D-GT-2024.dwg` or `DT dao nen.dwg`, or empty `perimeter_m` / `area_m2` / `centroid` / `bounding_box`.

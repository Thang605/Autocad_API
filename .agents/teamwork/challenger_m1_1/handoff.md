# Handoff Report — Challenger M1_1: Multi-Doc Resolution & Target Switching (R1)

**Agent**: Challenger M1_1 (Critic / Specialist)  
**Date**: 2026-11-09T09:14:00Z  
**Verdict**: **APPROVE** (Requirement R1 is verified and fully functional; 1 Medium non-blocking UX enhancement identified in Python MCP server)

---

## 1. Observation

### Obs 1: Active Document Default Handling
Direct HTTP query to `http://127.0.0.1:18888/status` without parameters:
```json
{
  "status": "connected",
  "service": "autocad-mcp-csharp-bridge",
  "bridge_version": "3.2-multidoc-pagination",
  "active_document": "W:\\1.KS-TK\\4.NINH HOA\\4. DT652.B -DT652.D\\3.HS_TKBVTC\\T3.HS_TKBVTC\\03-USER\\T06-THANG\\1.BV\\1.BD\\1.XLKT lan 1\\BD-TD-VX.dwg",
  "open_documents": [
    { "name": "BD-TD-VX.dwg", "is_active": true, "alignment_count": 120, "surface_count": 20, "corridor_count": 3 },
    { "name": "XREF-MAT DUONG LAM MOI - TAN DUNG.dwg", "is_active": false, "alignment_count": 0, "surface_count": 0, "corridor_count": 0 },
    { "name": "DT dao nen.dwg", "is_active": false, "alignment_count": 0, "surface_count": 0, "corridor_count": 0 },
    { "name": "C3D-GT-2024.dwg", "is_active": false, "alignment_count": 1, "surface_count": 3, "corridor_count": 1 }
  ]
}
```
POST to `http://127.0.0.1:18888/civil` with `{"action":"summary"}` (omitted `document_name`) and `{"action":"summary","document_name":null}`:
- StatusCode: 200 OK
- Targeted document: `BD-TD-VX.dwg` (120 alignments, 20 surfaces, 3 corridors).

### Obs 2: Explicit Target Document Resolution
Direct HTTP queries to `http://127.0.0.1:18888/civil`:
1. `{"action":"summary","document_name":"C3D-GT-2024.dwg"}`:
   - StatusCode: 200 OK
   - Targeted document: `C3D-GT-2024.dwg`
   - Data returned: `alignment_count: 1`, `surface_count: 3`, `corridor_count: 1`, `parcel_count: 5` (including handle `8ED5`, area: `124.7`).
2. `{"action":"summary","document_name":"C3D-GT-2024"}` (without `.dwg`):
   - StatusCode: 200 OK
   - Targeted document: `C3D-GT-2024.dwg`
3. `{"action":"summary","document_name":"c3d-gt-2024.dwg"}` (case-insensitive):
   - StatusCode: 200 OK
   - Targeted document: `C3D-GT-2024.dwg`
4. `{"action":"summary","document_name":"DT dao nen.dwg"}`:
   - StatusCode: 200 OK
   - Targeted document: `DT dao nen.dwg`
   - Data returned: `alignment_count: 0`, `surface_count: 0`, `corridor_count: 0`, `parcel_count: 13`.
5. Subsequent `/status` query:
   - StatusCode: 200 OK
   - Active document remains `BD-TD-VX.dwg` (`is_active: true`). No GUI window switching or focus stealing occurred.

### Obs 3: Non-Existent Drawing Handling
Query to `http://127.0.0.1:18888/status?document_name=NonExistent_Drawing_12345.dwg`:
- HTTP StatusCode: `404 Not Found`
- Content-Type: `application/json; charset=utf-8`
- Verbatim JSON body:
```json
{"status":"error","message":"Không tìm thấy bản vẽ 'NonExistent_Drawing_12345.dwg' trong số các bản vẽ đang mở. Danh sách bản vẽ hiện có: [BD-TD-VX.dwg, XREF-MAT DUONG LAM MOI - TAN DUNG.dwg, DT dao nen.dwg, C3D-GT-2024.dwg]"}
```
Tested across all 7 endpoints (`/status`, `/civil` POST body, `/civil` GET query, `/inspect`, `/draw`, `/lisp`, `/manage`):
- All 7 endpoints consistently return `HTTP 404` with the clean error JSON above.
- Zero crashes, zero unhandled exceptions, and the HTTP bridge remained 100% operational.

### Obs 4: Python MCP Server Error Swallowing
In `C:\Dropbox\0.AI AGENT\11.Autocad Drawing\autocad_mcp_server.py`, lines 153-158:
```python
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            if resp.status == 200:
                return json.loads(resp.read().decode("utf-8"))
    except Exception:
        pass
    return None
```
When `call_mcp_tool` was invoked for `civil3d_inspect` with `document_name="NonExistent_Drawing_12345.dwg"`:
- Result returned: `{"status": "error", "message": "Không nhận được phản hồi từ endpoint /civil."}`
- The rich C# 404 message listing the open drawings is swallowed by `except Exception: pass` in `_post_http_bridge` and `_get_http_bridge`.

### Obs 5: Build Compliance
Executed `powershell -ExecutionPolicy Bypass -NoProfile -File "c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\BuildProject.ps1"`:
- Result: `Build succeeded. 0 Warning(s), 0 Error(s). Time Elapsed 00:00:03.69`
- Target DLL generated: `C:\CadBuild\Autocad2026_API\MyFirstProject\bin\Debug\Civil3D_Tools_xqd5vnxot4s.dll`.

---

## 2. Logic Chain

1. **Active Document Contract (Obs 1)**:
   - When no `document_name` is supplied, `ResolveTargetDocument` returns `Application.DocumentManager.MdiActiveDocument`.
   - Empirically verified: queries without `document_name` target `BD-TD-VX.dwg`, which is the actual active drawing in AutoCAD.

2. **Explicit Multi-Doc Resolution & Anti-Hijacking (Obs 2)**:
   - The user requested targeting open drawings by name. `ResolveTargetDocument` matches exact name, filename without extension, case-insensitively, and full path.
   - In legacy code, `ResolveCivilTargetDocument` switched execution to another document if `activeCivilCount == 0`.
   - Empirically verified: `DT dao nen.dwg` has 0 alignments, 0 surfaces, 0 corridors, but 13 parcels. The new implementation stays on `DT dao nen.dwg` and returns its 13 parcels without hijacking.
   - Furthermore, querying other open drawings (`C3D-GT-2024.dwg`, `DT dao nen.dwg`) does not alter `MdiActiveDocument`, confirming zero GUI disruption.

3. **Robust 404 Error Handling (Obs 3)**:
   - `ResolveTargetDocument` throws `FileNotFoundException` when a requested drawing is not among `Application.DocumentManager`.
   - `HandleRequestAsync` catches `FileNotFoundException` and responds with HTTP 404 and a JSON payload listing all valid open drawing names.
   - Tested across all HTTP endpoints: verified HTTP 404 and complete process stability.

4. **Python MCP Client Finding (Obs 4)**:
   - While C# sends the detailed 404 payload, Python's `_post_http_bridge` catches `urllib.error.HTTPError` as a generic `Exception` and returns `None`, producing the fallback `"Không nhận được phản hồi từ endpoint /civil."`.
   - This does not violate requirement R1 (which specifies C# bridge behavior and document targeting), but is a valuable UX recommendation for Worker M1.

---

## 3. Caveats

1. Tested against 4 concurrently open drawings (`BD-TD-VX.dwg`, `XREF-MAT DUONG LAM MOI - TAN DUNG.dwg`, `DT dao nen.dwg`, `C3D-GT-2024.dwg`).
2. Concurrency stress was performed sequentially across endpoints; extreme high-concurrency race conditions (e.g., 50 parallel requests while a document is being actively closed by a user in AutoCAD) were not tested to protect user session integrity.
3. As per instructions, `C3D-GT-2024.dwg` was tested 100% non-destructively (no save or write operations).

---

## 4. Conclusion & Verdict

**VERDICT: APPROVE**

- **R1.1 Active Document Default**: Fully satisfied.
- **R1.2 Named Document Targeting**: Fully satisfied (exact, case-insensitive, extensionless, full path).
- **R1.3 Non-existent Drawing Error**: Fully satisfied in C# HTTP bridge (HTTP 404, clean JSON with open drawing list, no crashes).
- **R1.4 Zero Document Hijacking**: Fully satisfied (`DT dao nen.dwg` stays intact; `MdiActiveDocument` never switched).

### Recommended Enhancement (for Worker M1)
In `C:\Dropbox\0.AI AGENT\11.Autocad Drawing\autocad_mcp_server.py`:
In `_post_http_bridge` and `_get_http_bridge`, catch `urllib.error.HTTPError as e`, read `e.read().decode("utf-8")`, parse JSON, and return it if status is 400 or 404 so MCP users receive the full list of available open drawings instead of the generic fallback message.

---

## 5. Verification Method

To independently verify all findings, run the following PowerShell command in the workspace:

```powershell
# 1. Verify Active Document Default
Invoke-RestMethod -Uri "http://127.0.0.1:18888/civil" -Method POST -Body '{"action":"summary"}' -ContentType "application/json" | Select-Object document_name

# 2. Verify Explicit Targeting
Invoke-RestMethod -Uri "http://127.0.0.1:18888/civil" -Method POST -Body '{"action":"summary","document_name":"C3D-GT-2024.dwg"}' -ContentType "application/json" | Select-Object document_name, summary

# 3. Verify HTTP 404 Error on Non-existent Drawing
curl.exe -i "http://127.0.0.1:18888/status?document_name=NonExistent_Drawing_12345.dwg"
# Expected: HTTP/1.1 404 Not Found with list of open drawings

# 4. Verify Build
powershell -ExecutionPolicy Bypass -NoProfile -File "c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\BuildProject.ps1"
# Expected: 0 Warning(s), 0 Error(s)
```

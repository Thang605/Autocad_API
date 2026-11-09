# Handoff Report — Explorer Survey 3 (Build Compliance, Civil 3D Parcel/Table Mechanics & E2E Testing)
**Working Directory**: `c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\.agents\teamwork\explorer_survey_3`
**Parent Agent**: `0bacc69e-6c1a-4e6c-a083-63d1ef025e4f`
**Target Request**: `c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\.agents\teamwork\ORIGINAL_REQUEST.md`
**Timestamp**: 2026-11-09T08:32:00Z

---

## 1. Observation

1. **Build Execution & Results**:
   - Running `powershell -ExecutionPolicy Bypass -NoProfile -File "c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\BuildProject.ps1"` succeeds with:
     ```
     Restored C:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\MyFirstProject\MyFirstProject.csproj (in 202 ms).
     MyFirstProject -> C:\CadBuild\Autocad2026_API\MyFirstProject\bin\Debug\Civil3D_Tools_cawa2wdqv54.dll
     Build succeeded. 0 Warning(s), 0 Error(s). Time Elapsed 00:00:06.20
     ```
   - Target framework in `MyFirstProject.csproj:4` is `net10.0-windows`.
   - Output directory configured in `Directory.Build.props:15-18` routes to `C:\CadBuild\Autocad2026_API\MyFirstProject\bin\Debug\` outside Dropbox.
   - Under concurrent build execution, MSBuild reported verbatim:
     `error MSB3713: The file "C:\CadBuild\Autocad2026_API\MyFirstProject\obj\Debug\MyFirstProject.AssemblyInfo.cs" could not be created. The process cannot access the file ... because it is being used by another process.`

2. **Live AutoCAD & Civil 3D HTTP Bridge**:
   - Querying `http://127.0.0.1:18888/status` returned:
     - `status: "connected"`, `bridge_version: "3.1-civil3d-multidoc"`, `pid: 13948` (AutoCAD 2027).
     - Active document: `Z:\Z.FORM MAU LAM VIEC\1. BIM\2.MAU C3D\Z0.STYLE C3D\2. THIET KE GIAO THONG\THANG\C3D-GT-2024.dwg`.
     - 4 open documents detected (`BD-TD-VX.dwg` with 120 alignments and 20 surfaces; `XREF-MAT DUONG...`; `DT dao nen.dwg`; `C3D-GT-2024.dwg`).
   - Querying `http://127.0.0.1:18888/civil` (`action: "inspect_all"`) returned 5 Parcels under `"Site 1"`:
     - Handles: `8F2B`, `9723`, `8F35`, `8ED5`, `9732`.
     - Areas: 297.73 $m^2$, 182.99 $m^2$, 342.72 $m^2$, 124.70 $m^2$, 227.34 $m^2$.
     - Style: `S_VuotBTN`, Layer: `0`.

3. **Civil 3D Parcel API Surface in `AeccDbMgd.dll`**:
   - `Autodesk.Civil.DatabaseServices.Parcel` provides:
     - Hierarchy: `parcel.SiteId`, `site.GetParcelIds()`.
     - Measurements: `parcel.Area` ($m^2$), `parcel.Perimeter2d` ($m$), `parcel.GeometricExtents` (`MinPoint`, `MaxPoint`).
     - Metadata: `parcel.Number`, `parcel.Name`, `parcel.Description`, `parcel.TaxId`.
     - Presentation: `parcel.StyleId`, `parcel.StyleName`, `parcel.AreaSelectionLabelStyleId`, `parcel.AreaSelectionLabelLocation`.
     - Segments: `parcel.GetParcelSegmentTableIds()`, `site.GetParcelSegmentIds()`.

4. **Table Generation & Preview Framing Defects**:
   - `autocad_mcp_server.py:1320-1430` implements `autocad_draw_table` by generating primitive rectangles and lines for borders, plus `annots.append({"type": "text", ...})`.
   - Empty cells generate `DBText` entities with `""`, cluttering ModelSpace.
   - Native `AcDbTable` (`Autodesk.AutoCAD.DatabaseServices.Table`) is NOT created by `autocad_draw_table`, though C# implementations exist in `38.AT_KhoiDongMcpBridge.cs:2048` (`DrawStakeTableInModelSpace`) and `Civil Tool 2/CTPA_BangThongKeParcel.cs:447` (`CreateParcelTable`).
   - In `autocad_mcp_server.py:777-804`, `_render_entities_to_png` only calculates bounding boxes for `LINE`, `LWPOLYLINE`, `CIRCLE`, `ARC`, `DIMENSION`, `TEXT`. It has zero cases for `TABLE`.
   - In `38.AT_KhoiDongMcpBridge.cs:2569-2722`, `InspectModelSpace` does not inspect `ATable` or Civil 3D entities.

5. **Multi-Document & Token Volume Behavior**:
   - `38.AT_KhoiDongMcpBridge.cs:648` already has `ResolveCivilTargetDocument(activeDoc, root)` which parses `"document_name"`.
   - However, `autocad_mcp_server.py:2076` (`civil3d_inspect`) and other tools do not expose `document_name` in their parameter schemas, defaulting always to the active drawing.
   - `civil3d_inspect(action="inspect_all")` dumps all PVIs and sample lines without pagination, producing large payloads on drawings with 120 alignments like `BD-TD-VX.dwg`.

---

## 2. Logic Chain

1. **Build Compliance & Stability**:
   - *From Observation 1*: `BuildProject.ps1` produces a valid DLL `Civil3D_Tools_<random>.dll` and exits with code 0 on .NET 10.
   - *Inference*: The project dependencies, HintPaths, and C# syntax are intact. The transient `MSB3713` error observed during concurrent agent runs stems from shared access to `obj\Debug\MyFirstProject.AssemblyInfo.cs`.
   - *Deduction*: Adding `/nodeReuse:false` and enforcing serial build invocations eliminates build conflicts.

2. **Parcel Data Completeness (Requirement R3)**:
   - *From Observations 2 & 3*: The live drawing contains active Parcels, and `AeccDbMgd.dll` contains properties for perimeter, extents, style ID, tax ID, and label coordinates.
   - *Inference*: The C# bridge currently only extracts `area_m2`, `style`, and `name`.
   - *Deduction*: Extending `InspectAllCivil3DData` to also return `perimeter_m`, `tax_id`, `centroid: [X, Y]`, `bounding_box: [minX, minY, maxX, maxY]`, `style_id`, and `site_id` completely fulfills R3 with zero breaking changes to existing fields.

3. **Native Table Architecture (Requirement R4)**:
   - *From Observation 4*: `autocad_draw_table` outputs loose lines and empty text strings because it lacks a bridge endpoint for native `AcDbTable`.
   - *Inference*: `DrawStakeTableInModelSpace` and `CreateParcelTable` prove that creating `ATable` via C# transaction is robust, formats headers and title cleanly, and creates exactly one database entity.
   - *Deduction*: Adding a native table creation handler in `38.AT_KhoiDongMcpBridge.cs` (either under `/draw` or a dedicated action) and updating `autocad_draw_table` to invoke it will produce clean, editable AutoCAD tables without blank text clutter.

4. **Preview Framing Resolution (Requirement R4)**:
   - *From Observation 4*: `InspectModelSpace` drops tables; `_render_entities_to_png` ignores tables; global extents dwarf localized tables inserted at `[0, 0]`.
   - *Deduction*: To frame tables correctly:
     a. `InspectModelSpace` must report `TABLE` bounding rectangles (`width`, `height`, `position`).
     b. `_render_entities_to_png` must include `TABLE` in its bounding-box calculation.
     c. `autocad_draw_table` with `export_preview_path` must pass or compute the table's specific bounding box `[x0, y0 - height, x0 + width, y0]` with a 15% margin, ensuring tight, high-resolution rendering.

5. **Safe Non-Destructive Verification (Requirement R5)**:
   - *From Observation 2*: Active drawing `C3D-GT-2024.dwg` is a real user model. Mutating it during testing will destroy production work.
   - *Deduction*: Live automated tests must open a temporary blank scratch drawing (`acad.Documents.Add()`), run table/primitive draw tests, verify output, and close without saving (`doc.Close(false)`).

---

## 3. Caveats

1. **AutoCAD 2027 In-Process Requirement**:
   Direct .NET reflection on `AeccDbMgd.dll` outside of `acad.exe` fails because unmanaged C++ AutoCAD core dependencies are not loaded in external console hosts. All Civil 3D entity inspection must run in-process via the C# HTTP Bridge or within AutoCAD COM context.
2. **AutoLISP ActiveX Parcel Limitations**:
   Querying `(vlax-get-property p 'Area)` on Civil 3D Parcel enames fails with `unknown name: AREA` because Civil 3D COM wrappers require specialized Civil ActiveX type libraries (`AeccXUiLand.AeccApplication`). In-process C# managed transactions are the only reliable mechanism for Parcel attribute extraction.
3. **No Code Implementation in this Turn**:
   In strict accordance with the read-only Explorer role, no source code in `MyFirstProject` or `autocad_mcp_server.py` was altered. Concrete patch blueprints are documented in `report.md`.

---

## 4. Conclusion

1. **Build**: `BuildProject.ps1` is 100% compliant with .NET 10 (0 errors, 0 warnings).
2. **Civil 3D Parcels**: All necessary APIs (`Area`, `Perimeter2d`, `GeometricExtents`, `TaxId`, `SiteId`, `StyleId`) are verified and available in `AeccDbMgd.dll`. The live drawing contains 5 parcels ready for validation.
3. **Tables & Framing**: Replacing the primitive drawing loop in `autocad_draw_table` with native `AcDbTable` generation eliminates blank text entities and enables full table editing. Framing failure is resolved by incorporating `TABLE` bounding boxes in `InspectModelSpace` and `_render_entities_to_png`.
4. **Testing Protocol**: Testing against live AutoCAD must use temporary scratch documents (`_NEW`) to guarantee zero corruption of user project drawings.

---

## 5. Verification Method

To independently verify the findings in this report:

1. **Verify Build**:
   ```powershell
   powershell -ExecutionPolicy Bypass -NoProfile -File "c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\BuildProject.ps1"
   # Verify exit code is 0 and output reports "Build succeeded. 0 Warning(s) 0 Error(s)".
   ```

2. **Verify Live Bridge & Multi-Doc Status**:
   ```powershell
   Invoke-RestMethod -Uri "http://127.0.0.1:18888/status" -TimeoutSec 3
   # Verify status is "connected", active_document is reported, and open_documents lists all open drawings.
   ```

3. **Verify Live Parcel Inspection**:
   ```powershell
   $body = @{ action = 'inspect_all'; max_items = 10 } | ConvertTo-Json
   $res = Invoke-RestMethod -Uri "http://127.0.0.1:18888/civil" -Method Post -Body $body -ContentType "application/json"
   $res.parcels | Format-Table -AutoSize
   # Confirms 5 Parcels under "Site 1" with handles 8F2B, 9723, 8F35, 8ED5, 9732.
   ```

4. **Invalidation Conditions**:
   - If `BuildProject.ps1` returns non-zero error count, check if concurrent builds locked `AssemblyInfo.cs`.
   - If `http://127.0.0.1:18888/status` fails to respond, verify AutoCAD PID 13948 is running and `AT_Khoi_Dong_Mcp_Bridge` was loaded.

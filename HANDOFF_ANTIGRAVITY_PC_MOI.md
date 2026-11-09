# 📋 BÀN GIAO CÔNG VIỆC CHO ANTIGRAVITY TRÊN PC MỚI (ĐỒNG BỘ QUA DROPBOX)

**Thời điểm bàn giao:** `2026-11-09 22:42 GMT+7`  
**Thư mục dự án C#:** `c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API` (`MyFirstProject.sln`)  
**Thư mục MCP Server Python:** `c:\Dropbox\0.AI AGENT\11.Autocad Drawing\autocad_mcp_server.py`

---

## 1. Tóm tắt các hạng mục ĐÃ HOÀN THÀNH (R1 → R6)

Toàn bộ yêu cầu nghiên cứu tối ưu MCP và tăng cường kết nối Civil 3D (R1 → R6) đã được code xong, build đạt **0 Warning(s), 0 Error(s)** trên .NET 10 và kiểm chứng trực tiếp trên AutoCAD/Civil 3D:

1. **R1 — Đa bản vẽ (`document_name`) & Chống nhảy sai bản vẽ:**
   - Sửa `ResolveTargetDocument` và `ResolveCivilTargetDocument` trong `MyFirstProject/Acad Tool/38.AT_KhoiDongMcpBridge.cs`: mặc định luôn thao tác trên `MdiActiveDocument`, loại bỏ hoàn toàn hành vi tự nhảy sang bản vẽ khác khi bản vẽ hiện hành có `activeCivilCount == 0`.
   - Mọi endpoint (`/status`, `/inspect`, `/civil`, `/draw`, `/table`, `/propsets`, `/clear`, `/manage`, `/lisp`) và toàn bộ 14 công cụ MCP Python đều hỗ trợ tham số tùy chọn `document_name` (khớp theo tên file hoặc đường dẫn). Nếu truyền tên bản vẽ không mở, trả về HTTP 404 kèm danh sách các bản vẽ đang mở.

2. **R2 — Tối ưu Token & Phân trang (`civil3d_inspect`):**
   - Bổ sung `summary_only`, `entity_type`, `page`, `page_size` vào `InspectAllCivil3DData` (`38.AT_KhoiDongMcpBridge.cs`) và `civil3d_inspect` (`autocad_mcp_server.py`).
   - Khi `summary_only=True`, lược bỏ mảng đỉnh PVI trắc dọc và danh sách cọc chi tiết (giảm **80,4% số dòng** và **76,9% dung lượng** trên bản vẽ `BD-TD-VX.dwg`).

3. **R3 — Nhận diện đầy đủ đối tượng Civil 3D & Parcel (`autocad_inspect_drawing` & `civil3d_inspect`):**
   - Nâng cấp `InspectModelSpace` (`38.AT_KhoiDongMcpBridge.cs`) và `ClassifyOtherEntityInModelSpace` (`38.AT_McpBridge_TableAndPropertySets.cs`): nhận diện đầy đủ `CIVIL_PARCEL`, `CIVIL_PARCEL_SEGMENT`, `AECC_PARCEL_TABLE`, `CIVIL_ALIGNMENT`, `CIVIL_SURFACE`, `CIVIL_CORRIDOR`, `TABLE` (`AcDbTable`), `AEC_SCHEDULE_TABLE`, `POLYLINE3D`, `BLOCK_REFERENCE`.
   - Đọc chính xác diện tích Parcel (`Statistics.Area`), chu vi 2D (`GetParcelPerimeter` qua `BaseCurve`), tên & Handle của `Site` chứa Parcel (`cdoc.GetSiteIds()` → `site.GetParcelIds()`), `StyleName`, `Centroid`, `BoundingBox`.

4. **R4 — Bảng `AcDbTable` gốc & Đóng khung ảnh Preview (`autocad_draw_table`):**
   - Tạo file mới `MyFirstProject/Acad Tool/38.AT_McpBridge_TableAndPropertySets.cs` (`partial class AT_McpBridgeServer`) bổ sung endpoint `POST /table`:
     - Tạo đối tượng `AcDbTable` gốc của AutoCAD với font `TAHOMA_MCP`.
     - Ô trống không sinh nội dung rác; trong `CreateAnnotationEntity` (`38.AT_KhoiDongMcpBridge.cs`) cũng đã chặn tạo `DBText`/`MText` khi chuỗi rỗng.
     - Không tự ý đổi `db.Textstyle` hiện hành của người dùng trong `EnsureTahomaTextStyle`.
     - Hỗ trợ công thức tính toán trực tiếp trong ô bắt đầu bằng dấu `=` (ví dụ `=Sum(C3:C15)`).
   - Trong `autocad_mcp_server.py`, hàm `_render_entities_to_png` hỗ trợ tham số `focus_bbox=[min_x, min_y, max_x, max_y]` giúp ảnh preview của `autocad_draw_table` tự động đóng khung sắc nét ngay vào phạm vi bảng vừa chèn.

5. **R5 — Tuân thủ Build & An toàn bản vẽ:**
   - Đã kiểm tra build bằng `BuildProject.ps1` đạt `0 Warning(s), 0 Error(s)`.

6. **R6 — Quản lý Property Set & Bảng khối lượng động (`AEC_SCHEDULE_TABLE`):**
   - Bổ sung endpoint `POST /propsets` (và tích hợp chung vào `POST /civil`) trong `38.AT_McpBridge_TableAndPropertySets.cs` cùng công cụ MCP mới `civil3d_property_sets` (cũng có thể gọi qua `civil3d_automate(action=...)`):
     - `list_definitions`: Liệt kê các `PropertySetDefinition` trong bản vẽ, phân loại thuộc tính (`manual`, `automatic`, `formula`), đọc nội dung công thức VBScript (`PropertyDefinitionFormula.GetFormulaString()`) và tự động cảnh báo nếu công thức dùng `ActiveDocument.HandleToObject`, ghi cứng phiên bản COM (`AeccApplication.13.9`), hoặc `On Error Resume Next`.
     - `get_values`: Đọc giá trị Property Set gắn trên đối tượng (lọc theo `handles`, `entity_type`, `layer`, `property_set`, có phân trang).
     - `attach` / `set_values`: Gắn Property Set vào đối tượng và gán giá trị thuộc tính thủ công (hỗ trợ `values` chung hoặc `per_handle_values` riêng cho từng Handle, tự động cập nhật lại các bảng Schedule).
     - `import`: Sao chép `PropertySetDefinition` và `ScheduleTableStyle` từ bản vẽ mẫu (`source_document` đang mở hoặc `source_file` trên đĩa) sang bản vẽ đích qua `WblockCloneObjects`.
     - `list_schedule_styles`: Liệt kê các kiểu bảng `ScheduleTableStyle` kèm danh sách cột, thuộc tính liên kết và cờ tính tổng (`total`).
     - `list_schedule_tables`: Đọc danh sách và toàn bộ nội dung từng ô (`CellText`) của các bảng `AEC_SCHEDULE_TABLE` trong bản vẽ.
     - `insert_schedule_table`: Chèn bảng `AEC_SCHEDULE_TABLE` mới liên kết động với tập đối tượng (`handles` hoặc lọc theo `entity_type`/`layer`), bật `AutomaticUpdate`.
     - `update_schedule_tables`: Cập nhật (`RegenerateTable(true)`) toàn bộ hoặc theo danh sách Handle các bảng `AEC_SCHEDULE_TABLE`.

---

## 2. Hướng dẫn khởi chạy nhanh cho Antigravity trên PC mới

Khi mở dự án này trên PC mới sau khi Dropbox đồng bộ xong, Antigravity chỉ cần chạy 1 lệnh PowerShell dưới đây để:
1. Chép 14 file JSON schema đã sao lưu trong Dropbox (`.agent\mcp_schemas\*.json`) vào `%USERPROFILE%\.gemini\antigravity\mcp\autocad-mcp\`.
2. Build dự án C# (`BuildProject.ps1`) ra thư mục `C:\CadBuild\Autocad2026_API\MyFirstProject\bin\Debug\` trên PC mới (vì thư mục `C:\CadBuild` nằm ngoài Dropbox).
3. Nếu AutoCAD đang mở và chạy HTTP Bridge (`:18888`), tự động nạp (`Hot-Reload`) DLL mới vào AutoCAD:

```powershell
# 1. Đồng bộ 14 MCP tool schemas từ Dropbox vào thư mục cấu hình Antigravity của PC mới
$mcpDir = "$env:USERPROFILE\.gemini\antigravity\mcp\autocad-mcp"
New-Item -ItemType Directory -Force -Path $mcpDir | Out-Null
Copy-Item "c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\.agent\mcp_schemas\*.json" -Destination $mcpDir -Force

# 2. Build kiểm tra (0 Warning, 0 Error)
powershell -ExecutionPolicy Bypass -NoProfile -File "c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\BuildProject.ps1"

# 3. Nạp nóng DLL vào AutoCAD nếu cổng 18888 đang mở
$dll = (Get-Content "c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\last_dll.txt" -Raw).Trim()
try {
    Invoke-RestMethod -Uri "http://127.0.0.1:18888/reload" -Method POST -Body (@{dll_path=$dll} | ConvertTo-Json) -ContentType "application/json; charset=utf-8"
} catch {
    Write-Output "AutoCAD chưa mở hoặc chưa chạy lệnh AT_Khoi_Dong_Mcp_Bridge / NETRELOAD."
}
```

---

## 3. Danh sách các file thay đổi trong Git Working Tree (Lưu ý khi Commit)

- **Thuộc phạm vi nâng cấp MCP Civil 3D lần này:**
  - `MyFirstProject/Acad Tool/38.AT_KhoiDongMcpBridge.cs` (sửa)
  - `MyFirstProject/Acad Tool/38.AT_McpBridge_TableAndPropertySets.cs` (file mới — chứa R4 `AcDbTable` và R6 `PropertySet` / `ScheduleTable`)
  - `C:\Dropbox\0.AI AGENT\11.Autocad Drawing\autocad_mcp_server.py` (sửa — repo/thư mục MCP Server)
  - `.agent/mcp_schemas/*.json` và `C:\Dropbox\0.AI AGENT\11.Autocad Drawing\mcp_schemas\*.json` (bản sao lưu 14 file JSON schema của MCP)
  - `PROJECT.md`, `TEST_INFRA.md`, `.agents/` (tài liệu kiến trúc & kiểm thử do nhóm teamwork tạo)
- **KHÔNG thuộc phạm vi MCP (thay đổi cũ từ ngày 24/10/2026 của người dùng — cần giữ riêng khi commit):**
  - `MyFirstProject/Civil Tool 2/19.CTC_Xuat_Solid_Tu_Corridor.cs`

---

## 4. Gợi ý các công việc có thể triển khai tiếp trên PC mới

Nếu người dùng muốn tiếp tục phát triển hoặc ứng dụng ngay bộ công cụ mới trên PC mới:
1. **Ứng dụng thực tế vào bản vẽ khối lượng (`DT dao nen.dwg` hoặc bản vẽ tương tự):**
   - Dùng `civil3d_property_sets(action="import", source_file="...C3D-GT-2024.dwg", property_set_names=["Parcel"], schedule_style_names=["Parcel - bảng thống kê diện tích"])` để chép bộ Property Set `Parcel` và kiểu bảng Schedule từ file mẫu sang.
   - Dùng `civil3d_property_sets(action="attach", property_set="Parcel", per_handle_values={...})` để gắn và điền tự động `Phân loại` theo Style của từng Parcel (`S_ViaHe`, `S_mn`, `S_md`, `S_Mong`).
   - Dùng `civil3d_property_sets(action="insert_schedule_table", style="Parcel - bảng thống kê diện tích", entity_type="AECC_PARCEL", insert_point=[606950, 1389490])` để chèn bảng khối lượng động tự cập nhật khi chỉnh sửa Parcel.
2. **Nâng cấp công thức VBScript trong file mẫu `C3D-GT-2024.dwg` (nếu người dùng yêu cầu):**
   - Công thức `Diện tích` và `Tên hạng mục` trong Property Set `Parcel` của file mẫu hiện dùng `AeccXUiLand.AeccApplication.13.9` và `ActiveDocument.HandleToObject("[Handle]")`. Có thể cập nhật công thức để tự dò phiên bản Civil 3D (`14.0`, `13.9`, `13.8`...) và duyệt qua `oCivilApp.Documents` tìm đúng bản vẽ chứa Handle đó để không bị lỗi `--` khi mở nhiều bản vẽ.
3. **Commit code lên nhánh `master`:**
   - Chỉ stage và commit 2 file C# của MCP Bridge (`38.AT_KhoiDongMcpBridge.cs`, `38.AT_McpBridge_TableAndPropertySets.cs`), giữ nguyên các file không liên quan.

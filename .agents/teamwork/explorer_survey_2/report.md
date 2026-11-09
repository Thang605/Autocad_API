# Báo Cáo Khảo Sát Kỹ Thuật Máy Chủ AutoCAD / Civil 3D MCP Server & C# Bridge

**Ngày khảo sát**: 2026-11-09  
**Người thực hiện**: Explorer Survey 2 (MCP Server Explorer)  
**Phạm vi**: Khảo sát mã nguồn MCP Server, JSON Schemas, cơ chế kết nối đa tầng sang C# In-Process HTTP Bridge, đánh giá hiện trạng và đề xuất giải pháp kỹ thuật đáp ứng các yêu cầu R1 (Multi-Document), R2 (Pagination & Token Optimization), R3 (Civil 3D & Parcel Inspection), R4 (Table & Preview Framing).

---

## 1. Vị Trí Lưu Trữ Mã Nguồn MCP Server & JSON Schemas

### 1.1. Cấu hình Antigravity MCP Server
- **File cấu hình MCP**: `C:\Users\hoang\.gemini\config\mcp_config.json`
- **Tên Server**: `autocad-mcp`
- **Python Runtime**: `C:\Users\hoang\AppData\Local\Programs\Python\Python312\python.exe`
- **File thực thi MCP Server**: `C:\Dropbox\0.AI AGENT\11.Autocad Drawing\autocad_mcp_server.py` (2,167 dòng, 92.5 KB)
- **Biến môi trường**:
  - `PYTHONIOENCODING`: `utf-8`
  - `AUTOCAD_MCP_OUTPUT_DIR`: `C:\Dropbox\0.AI AGENT\11.Autocad Drawing\Output`
  - `AUTOCAD_MCP_CSHARP_DIR`: `c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API`
  - `AUTOCAD_MCP_BRIDGE_URL`: `http://127.0.0.1:18888` (mặc định)

### 1.2. Thư mục Tool Schemas của Antigravity
- **Đường dẫn**: `C:\Users\hoang\.gemini\antigravity\mcp\autocad-mcp\`
- **Tổng cộng 13 file JSON schema**:
  1. `civil3d_inspect.json` (1,514 bytes)
  2. `civil3d_automate.json` (2,413 bytes)
  3. `autocad_inspect_drawing.json` (784 bytes)
  4. `autocad_draw_table.json` (1,708 bytes)
  5. `autocad_export_preview.json` (1,391 bytes)
  6. `autocad_manage_document.json` (962 bytes)
  7. `autocad_status.json` (515 bytes)
  8. `autocad_build_and_reload.json` (638 bytes)
  9. `autocad_draw_primitives.json` (2,122 bytes)
  10. `autocad_draw_annotations.json` (1,943 bytes)
  11. `autocad_draw_layer_and_block.json` (997 bytes)
  12. `autocad_draw_road_cross_section.json` (2,208 bytes)
  13. `autocad_execute_lisp.json` (923 bytes)

### 1.3. C# In-Process HTTP Bridge trong AutoCAD / Civil 3D
- **File mã nguồn chính**: `c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\MyFirstProject\Acad Tool\38.AT_KhoiDongMcpBridge.cs` (2,820 dòng, 124.7 KB)
- **Lớp điều khiển**: `Civil3DCsharp.AT_McpBridgeServer`
- **Giao diện quản lý**: `KhoiDongMcpBridgeForm.cs`
- **Cổng HTTP**: `18888` (chạy ngầm trên tiến trình `acad.exe` qua `HttpListener`)

---

## 2. Phân Tích Chi Tiết 5 Công Cụ Mục Tiêu (Tool Definitions & Schemas)

### 2.1. `civil3d_inspect`
* **Vị trí trong Python**: `autocad_mcp_server.py`, dòng 2072–2111
* **Vị trí trong Schema**: `civil3d_inspect.json`
* **Tham số hiện tại**:
  - `action`: `str = "inspect_all"` (`"inspect_all"`, `"stake_report"`, `"check_surfaces"`)
  - `target_name`: `Optional[str] = None` (lọc tên Tuyến Alignment hoặc Surface)
  - `max_items`: `int = 500` (giới hạn số cọc trong SampleLineGroup)
* **Luồng xử lý**:
  Gửi POST request tới `http://127.0.0.1:18888/civil` kèm payload JSON.
* **Tồn tại kỹ thuật phát hiện được**:
  1. **Thiếu tham số `document_name`**: Không cho phép chỉ định bản vẽ mục tiêu trong số các file đang mở (R1).
  2. **Thiếu chế độ rút gọn `summary_only` / `summary_mode`**: Khi gọi `inspect_all` trên bản vẽ có nhiều tuyến hoặc bề mặt lớn, công cụ trả về toàn bộ mảng đỉnh PVI (lên tới 100 đỉnh mỗi profile), toàn bộ cọc trắc ngang, toàn bộ ống cống và hố thu. Ví dụ: kiểm thử thực tế trên bản vẽ mẫu `C3D-GT-2024.dwg` chỉ có 1 tuyến đã sinh ra **1,172 dòng JSON** (30.7 KB, tương đương ~8,000 tokens). Với các bản vẽ lớn như `BD-TD-VX.dwg` (120 tuyến, 20 bề mặt), lệnh này sẽ làm tràn context window hoặc timeout.
  3. **Thiếu bộ lọc loại đối tượng (`entity_type`)**: Không thể chỉ đọc riêng Parcel, riêng Surface hay riêng Alignment.
  4. **Thiếu phân trang (`page`, `page_size`)**: Không phân đoạn được danh sách dữ liệu lớn.
  5. **Dữ liệu Parcel còn sơ sài**: Chưa trích xuất tọa độ trọng tâm (Centroid X, Y), bounding box, TaxId và chuỗi phân đoạn ranh giới (Parcel Segments).

### 2.2. `autocad_inspect_drawing`
* **Vị trí trong Python**: `autocad_mcp_server.py`, dòng 1940–2009
* **Vị trí trong Schema**: `autocad_inspect_drawing.json`
* **Tham số hiện tại**:
  - `layer_filter`: `Optional[str] = None`
  - `max_entities`: `int = 200`
* **Luồng xử lý**:
  Gọi `_collect_modelspace_entities_for_preview()`, gửi GET request tới `http://127.0.0.1:18888/inspect`.
* **Tồn tại kỹ thuật nghiêm trọng phát hiện được**:
  1. **Bỏ sót hoàn toàn đối tượng Civil 3D và AutoCAD Table**:
     Hàm C# `InspectModelSpace(Document doc)` (dòng 2590–2720) chỉ duyệt các kiểu: `Line`, `Polyline`, `Circle`, `Arc`, `RotatedDimension`, `AlignedDimension`, `DBText`, `MText`. Toàn bộ các đối tượng Civil 3D (`CivilParcel`, `Alignment`, `CivSurface`, `Corridor`) và bảng biểu AutoCAD (`ATable`) đều bị bỏ qua!
     *Minh chứng thực tế*: Trên bản vẽ `C3D-GT-2024.dwg` có **717 đối tượng** ModelSpace, nhưng `autocad_inspect_drawing` chỉ trả về **4 đối tượng** `LWPOLYLINE` (bỏ sót 713 đối tượng còn lại).
  2. **Không có tham số `document_name`**: Luôn cố định vào bản vẽ hiện hành.
  3. **Không có chế độ `summary_only`**: Luôn đọc và truyền danh sách chi tiết các thực thể.

### 2.3. `autocad_draw_table`
* **Vị trí trong Python**: `autocad_mcp_server.py`, dòng 1318–1488
* **Vị trí trong Schema**: `autocad_draw_table.json`
* **Tham số hiện tại**:
  - `title`: `str`
  - `headers`: `List[str]`
  - `rows`: `List[List[str]]`
  - `insert_point`: `List[float] = [0.0, 0.0]`
  - `col_widths`: `Optional[List[float]] = None`
  - `row_height`: `float = 3.5`
  - `text_height`: `float = 1.4`
  - `layer`: `str = "BANG_THONG_KE"`
  - `export_preview_path`: `Optional[str] = None`
* **Tồn tại kỹ thuật phát hiện được**:
  1. **Không tạo bảng AutoCAD Table thực thụ**:
     Hàm Python tự băm nhỏ bảng thành các hình chữ nhật (`rectangle`), đoạn kẻ (`line`) và nhãn chữ (`text`), rồi gửi mảng `shapes` và `annots` tới endpoint `/draw`.
  2. **Tạo ra hàng loạt đối tượng chữ rỗng (`""`) trong bản vẽ** (Nguyên nhân của yêu cầu R4):
     Khi một ô bảng rỗng (`cell_val == ""`), Python vẫn gửi `{ "type": "text", "text": "" }`. Phía C# bridge (`CreateAnnotationEntity`, dòng 2409–2435) tạo ra thực thể `DBText` với chuỗi `""` và thêm vào ModelSpace. Điều này làm rác bản vẽ với hàng chục/hàng trăm đối tượng text vô hình!
  3. **Lỗi phóng to (Framing) khi xuất ảnh Preview**:
     Khi truyền `export_preview_path`, Python gọi `_collect_modelspace_entities_for_preview()`, quét toàn bộ thực thể trong ModelSpace của cả bản vẽ. Nếu bản vẽ có tuyến đường hay địa hình ở tọa độ hàng trăm ngàn mét, bounding box sẽ bao phủ cả bản vẽ, khiến bảng vẽ xong bị thu nhỏ thành 1 điểm ảnh mắt thường không thể đọc được.

### 2.4. `autocad_export_preview`
* **Vị trí trong Python**: `autocad_mcp_server.py`, dòng 2012–2070
* **Vị trí trong Schema**: `autocad_export_preview.json`
* **Tham số hiện tại**:
  - `output_png_path`, `output_dxf_path`, `theme`, `width`, `height`, `badge_title`
* **Tồn tại kỹ thuật phát hiện được**:
  1. Luôn thực hiện `._ZOOM _Extents` toàn bộ bản vẽ, không hỗ trợ zoom/crop tập trung vào vùng đối tượng vừa vẽ hoặc khung tọa độ cụ thể (`focus_bbox` / `target_layer`).
  2. Chỉ render các đối tượng 2D đơn giản (Line, Polyline, Circle, Arc, Dimension, Text), không render bảng hay biên dạng Parcel.

### 2.5. `autocad_manage_document`
* **Vị trí trong Python**: `autocad_mcp_server.py`, dòng 1004–1092
* **Vị trí trong Schema**: `autocad_manage_document.json`
* **Tham số hiện tại**:
  - `action`: `['new', 'open', 'save', 'clear', 'zoom_extents']`
  - `file_path`: `Optional[str] = None`
* **Tồn tại kỹ thuật phát hiện được**:
  1. Chưa có hành động `list_open_documents` để AI biết danh sách các bản vẽ đang mở kèm số lượng đối tượng Civil 3D (dù C# bridge `/status` đã cung cấp sẵn mảng `open_documents`).
  2. Chưa có hành động `set_active_document` để chuyển đổi qua lại giữa các bản vẽ.

---

## 3. Cơ Chế Điều Phối Yêu Cầu (Dispatch Mechanism) Từ MCP Server Sang C# Bridge

Toàn bộ chu trình truyền nhận diễn ra theo kiến trúc **Triple-Engine IPC**:

```
[Antigravity AI Agent]
         │ (JSON-RPC qua Stdio)
         ▼
[autocad_mcp_server.py (FastMCP)]
         │
         ├── 1. Kiểm tra liveness: GET http://127.0.0.1:18888/status
         │      └─ Nếu chưa chạy: Kích hoạt COM ActiveX (win32com) -> NETLOAD DLL -> AT_MCP_START
         │
         ├── 2. Ưu tiên 1 (Tốc độ cao & Giao dịch an toàn):
         │      POST/GET http://127.0.0.1:18888/{endpoint}
         │         │
         │         ▼ (HttpListener trong acad.exe trên background thread)
         │      [AT_McpBridgeServer.ListenLoopAsync]
         │         │
         │         ▼ Marshalling về UI STA Thread của AutoCAD
         │      [_uiMarshalControl.Invoke(...) via Windows Message Loop]
         │         │
         │         ▼ Bắt buộc DocumentLock
         │      [using (doc.LockDocument())]
         │         │
         │         ▼ Giao dịch Database an toàn
         │      [using (Transaction tr = db.TransactionManager.StartTransaction())]
         │         │
         │         ▼
         │      Thao tác AutoCAD .NET & Civil 3D Managed API
         │
         └── 3. Ưu tiên 2 (Dự phòng khi HTTP bận):
                Ghi file AutoLISP tạm vào runtime/ -> doc.SendCommand(...) qua COM
```

### Điểm mấu chốt trong xử lý luồng (Threading & Document Locking):
1. **Windows Message Loop Marshalling**: AutoCAD là ứng dụng Win32/WPF đơn luồng (`STAThread`). Việc gọi các API đồ họa hoặc Civil 3D từ luồng HTTP của .NET Core / .NET 10 sẽ gây lỗi `AccessViolationException` hoặc crash tiến trình `acad.exe`. C# bridge đã giải quyết xuất sắc bằng biến `_uiMarshalControl` (khởi tạo trên Main Thread) và gọi `_uiMarshalControl.Invoke()`.
2. **DocumentLocking**: Mọi thao tác ghi/đọc dữ liệu bản vẽ đều được bao bọc trong `doc.LockDocument()`.
3. **Cơ chế Resolve Document**: Trong C# bridge (`38.AT_KhoiDongMcpBridge.cs`, dòng 648–688), hàm `ResolveCivilTargetDocument` đã hỗ trợ tìm bản vẽ theo `document_name`:
   ```csharp
   private static Document ResolveCivilTargetDocument(Document activeDoc, JsonElement root)
   {
       string docFilter = GetStr(root, "document_name", "").Trim();
       if (!string.IsNullOrWhiteSpace(docFilter))
       {
           foreach (Document d in Application.DocumentManager)
           {
               if (d.Name.Contains(docFilter, StringComparison.OrdinalIgnoreCase) ||
                   d.Database.Filename.Contains(docFilter, StringComparison.OrdinalIgnoreCase))
               {
                   return d;
               }
           }
       }
       ...
   }
   ```
   **Tuy nhiên**: Phía Python MCP Server chưa từng khai báo tham số `document_name` và chưa truyền trường này vào JSON payload!

---

## 4. Đề Xuất Giải Pháp Kỹ Thuật Chi Tiết Cho R1, R2, R3, R4

### 4.1. Giải pháp cho R1: Multi-Document Context & Selection
1. **Nâng cấp Python Tool Signatures & JSON Schemas**:
   Bổ sung tham số `document_name: Optional[str] = None` vào các công cụ:
   - `civil3d_inspect`
   - `autocad_inspect_drawing`
   - `autocad_draw_table`
   - `autocad_draw_primitives`
   - `autocad_export_preview`
   - `civil3d_automate`
2. **Nâng cấp `autocad_manage_document`**:
   Bổ sung 2 action:
   - `list_open_documents`: trả về danh sách tài liệu đang mở kèm thuộc tính `is_active`, `alignment_count`, `surface_count`, `entity_count`.
   - `set_active_document`: gọi API C# để đặt `Application.DocumentManager.MdiActiveDocument = targetDoc`.
3. **Nâng cấp C# Bridge**:
   - Mở rộng hàm trợ giúp `RunOnTargetDocument(string? docName, Func<Document, T> func)` thay cho việc luôn mặc định lấy `MdiActiveDocument`.
   - Trong `/inspect`: đọc query param `document_name` để chọn bản vẽ cần đọc.
   - Trong `/draw`: đọc thuộc tính `document_name` trong root JSON để vẽ vào đúng bản vẽ được chỉ định.

### 4.2. Giải pháp cho R2: Output Pagination, Filtering & Token Efficiency
1. **Bổ sung tham số vào `civil3d_inspect`**:
   - `summary_only: bool = False`: Nếu `True`, chỉ trả về số lượng, tên, chiều dài, diện tích, không xuất mảng PVI, không xuất mảng cọc, không xuất mảng ống chi tiết (tiết kiệm 95% token).
   - `entity_type: Optional[str] = None`: Chỉ định loại đối tượng cần đọc chi tiết (`"alignment"`, `"surface"`, `"corridor"`, `"parcel"`, `"pipe_network"` hoặc `"all"`).
   - `page: int = 1` và `page_size: int = 50`: Phân trang cho các mảng dữ liệu lớn (như SampleLines, CogoPoints, PVIs).
2. **Cập nhật logic `InspectAllCivil3DData` trong C#**:
   - Kiểm tra `summary_only`: nếu bật, bỏ qua vòng lặp đọc `prof.PVIs` và `grp.GetSampleLineIds()`.
   - Kiểm tra `entity_type`: nếu là `"parcel"`, bỏ qua quét Alignments, Surfaces, Corridors để tăng tốc độ phản hồi và tiết kiệm token tối đa.
   - Trả về cấu trúc phân trang chuẩn:
     ```json
     {
       "page": 1,
       "page_size": 50,
       "total_items": 120,
       "total_pages": 3,
       "items": [ ... ]
     }
     ```
3. **Bổ sung tham số vào `autocad_inspect_drawing`**:
   - `summary_only: bool = False`: Nếu `True`, chỉ trả về `counts_by_type` và `bounding_box`, không xuất mảng `sample_entities`.
   - `entity_type_filter: Optional[str] = None`: Lọc theo loại đối tượng (`"PARCEL"`, `"TABLE"`, `"LINE"`...).

### 4.3. Giải pháp cho R3: Trích Xuất Toàn Diện Civil 3D Entities & Parcels
1. **Nâng cấp `InspectModelSpace` trong C# (`38.AT_KhoiDongMcpBridge.cs`)**:
   Mở rộng vòng lặp duyệt `BlockTableRecord.ModelSpace` để nhận diện các kiểu đối tượng:
   - `CivilParcel parcel`: Trích xuất Tên thửa, Phân khu (`Site`), Diện tích (`parcel.Area`), Bounding Box (`parcel.GeometricExtents`), Tâm thửa, Style.
   - `ATable table`: Trích xuất Tiêu đề, Số hàng, Số cột, Bounding Box.
   - `CivSurface surf`: Trích xuất Tên bề mặt, Loại, Bounding Box.
   - `Alignment al`: Trích xuất Tên tuyến, Chiều dài, Bounding Box.
   - `Corridor corr`: Trích xuất Tên corridor, Bounding Box.
   - `BlockReference blk`: Trích xuất Tên block, Điểm chèn, Tọa độ.
2. **Hoàn thiện trích xuất thông tin Parcel trong `InspectAllCivil3DData`**:
   - Kế thừa giải pháp đã chứng minh hiệu quả trong `BangThongKeParcelForm.cs` và `CTPA_BangThongKeParcel.cs`:
     - `DienTich = Math.Round(parcel.Area, 2)` (diện tích tính theo m² và ha)
     - `TaxId = parcel.TaxId`
     - `Bounds = parcel.GeometricExtents`
     - `Centroid = new Point2d((Bounds.MinPoint.X + Bounds.MaxPoint.X) / 2.0, (Bounds.MinPoint.Y + Bounds.MaxPoint.Y) / 2.0)`
     - Trích xuất tọa độ biên dạng thửa đất (loop vertices) để có thể hiển thị trên Preview PNG.

### 4.4. Giải pháp cho R4: Vẽ Bảng Chuẩn AutoCAD Table & Framing Preview
1. **Chuyển đổi `autocad_draw_table` sang sử dụng Native `AcDbTable`**:
   - C# bridge đã có sẵn mẫu chuẩn tạo bảng `var tb = new ATable()` trong hàm `DrawStakeTableInModelSpace` (dòng 2063–2124).
   - Bổ sung cấu trúc `tables` vào endpoint `/draw` (hoặc tạo endpoint riêng `/draw_table`):
     - Tạo đối tượng `Autodesk.AutoCAD.DatabaseServices.Table` trực tiếp trong Database.
     - Thiết lập số hàng, số cột, chiều cao hàng, chiều rộng cột.
     - Gán chữ tiếng Việt font Tahoma chuẩn Unicode qua `tb.Cells[r, c].TextString`.
     - Căn lề giữa/phải tự động.
2. **Loại bỏ hoàn toàn thực thể chữ rỗng**:
   - Trong Python: Thêm điều kiện `if cell_val.strip(): annots.append(...)`.
   - Trong C# `CreateAnnotationEntity`: Thêm kiểm tra `if (string.IsNullOrWhiteSpace(txtVal)) return false;` để triệt tiêu mọi rác `DBText` rỗng trong ModelSpace.
3. **Cải tiến Preview Framing cho Bảng**:
   - Tính toán Bounding Box chính xác của bảng:
     - `min_x = x0 - 5.0`
     - `max_x = x0 + total_w + 5.0`
     - `min_y = y0 - total_h - 5.0`
     - `max_y = y0 + 5.0`
   - Truyền `focus_bbox` vào hàm `_render_entities_to_png` để ảnh preview chỉ cắt và phóng to vùng bảng vẽ, đảm bảo chữ và đường kẻ bảng rõ nét, dễ đọc, không bị ảnh hưởng bởi các đối tượng khác trên bản vẽ.

---

## 5. Kết Quả Kiểm Thử Thực Tế Trên Phiên AutoCAD / Civil 3D Hiện Hành

Tại thời điểm khảo sát, kết nối tới AutoCAD Civil 3D đang mở đã được xác minh thành công 100%:
- **Tiến trình**: `acad.exe` (PID: 13948)
- **C# Bridge Version**: `3.1-civil3d-multidoc`, cổng `18888`
- **Tài liệu hiện hành**: `C3D-GT-2024.dwg`
- **Danh sách 4 bản vẽ đang mở**:
  1. `BD-TD-VX.dwg` (120 Alignments, 20 Surfaces, 3 Corridors) -> Chứng minh tính cấp thiết của R1 & R2
  2. `XREF-MAT DUONG LAM MOI - TAN DUNG.dwg`
  3. `DT dao nen.dwg`
  4. `C3D-GT-2024.dwg` (1 Alignment, 3 Surfaces, 1 Corridor, 51 SampleLines, 5 Parcels, 2 Pipe Networks)
- **Xác nhận lỗi ModelSpace Inspector**:
  - `autocad_status` báo: 717 đối tượng trong ModelSpace.
  - `autocad_inspect_drawing` chỉ tìm thấy: 4 đối tượng `LWPOLYLINE` (bỏ sót 713 đối tượng Civil 3D/Blocks/Tables).
- **Xác nhận nguy cơ quá tải token**:
  - `civil3d_inspect(action="inspect_all", max_items=10)` trả về 1,172 dòng JSON (30.7 KB) do chưa có cơ chế lọc rút gọn `summary_only`.

---
*Báo cáo được lập đầy đủ làm căn cứ để các kỹ sư triển khai thực hiện các thay đổi mã nguồn.*

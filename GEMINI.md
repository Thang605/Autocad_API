# Quy Tắc Phát Triển Lệnh AutoCAD / Civil 3D (BẮT BUỘC)

## 1. 🌟 BẮT BUỘC TẠO FORM GIAO DIỆN (UI) CHO MỌI LỆNH MỚI
- **Mọi lệnh mới được tạo đều PHẢI có giao diện Form** (Windows Forms kế thừa `System.Windows.Forms.Form`).
- File Form đặt tên theo format: `TenLenhForm.cs` (Ví dụ: `BangThongKeParcelForm.cs`, `DieuChinhDuongDoForm.cs`).
- Form được thiết kế chuyên nghiệp, hiện đại (Segoe UI, bố cục GroupBox khoa học, hỗ trợ resize).
- **Tương tác hai chiều với AutoCAD Canvas**:
  - Sử dụng `ed.StartUserInteraction(form)` khi người dùng bấm nút pick điểm / chọn đối tượng trên bản vẽ để tạm ẩn Form.
  - Hiển thị Form bằng `Application.ShowModalDialog(form)` trong phương thức lệnh.

---

## 2. 💾 BẮT BUỘC GHI NHỚ TOÀN BỘ THÔNG SỐ VÀ LỰA CHỌN TRƯỚC ĐÓ (PERSISTENT STATE)
- **Mọi lệnh và Form PHẢI tự động ghi nhớ toàn bộ thông số, đối tượng và tùy chọn người dùng đã chọn ở lần chạy trước**:
  1. **Lưu vết đối tượng & nguồn dữ liệu đã chọn**:
     - Khai báo `private static ObjectId _last... = ObjectId.Null;` hoặc danh sách ID trong Command Class / Form.
     - Khi mở lại Form, kiểm tra đối tượng cũ (`!_lastObjectId.IsNull && _lastObjectId.IsValid && !_lastObjectId.IsErased`) để tự động nạp lại.
  2. **Ghi nhớ toàn bộ giá trị nhập & tùy chọn UI**:
     - Khai báo các biến `private static` trong Form cho tất cả TextBox, ComboBox, NumericUpDown, CheckBox, RadioButton, CheckedListBox...
     - Viết hàm `SaveCurrentSettings()` tự động gọi khi Form đóng (`FormClosing`) hoặc khi nhấn nút thực thi.
     - Viết hàm `RestoreLastSettings()` tự động khôi phục toàn bộ giá trị trong hàm khởi tạo Form.
     - Ghi nhớ kích thước cửa sổ Form nếu người dùng resize (`_lastFormSize`).

---

## 3. ⚙️ QUY CHUẨN KỸ THUẬT, TRANSACTION & BẮT BUỘC BUILD KIỂM TRA
- **Transaction & OpenMode**: Luôn sử dụng `OpenMode.ForWrite` khi sửa đổi đối tượng Civil 3D/AutoCAD trong `Transaction`.
- **Tránh xung đột namespace**: Sử dụng alias chuẩn (`Application = Autodesk.AutoCAD.ApplicationServices.Application`, `ATable = Autodesk.AutoCAD.DatabaseServices.Table`, `WinFormsLabel = System.Windows.Forms.Label`...).
- 🚨 **BẮT BUỘC BUILD KIỂM TRA SAU MỌI LẦN SỬA CODE**:
  - Sau **BẤT KỲ** thao tác chỉnh sửa, tạo mới, sửa lỗi, đổi tên hoặc refactor code nào, AI **BẮT BUỘC** phải tự động chạy lệnh build để xác nhận kết quả `0 Error(s)` trước khi hoàn thành phản hồi:
    `powershell -ExecutionPolicy Bypass -NoProfile -File "c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\BuildProject.ps1"`
  - **Tuyệt đối không bỏ qua bước build hoặc chỉ trả lời bằng lời nói mà chưa kiểm chứng kết quả biên dịch.**

---

## 4. 🎯 QUY TẮC TINH GỌN FILE KHI TẠO LỆNH MỚI
- Khi tạo lệnh mới, **CHỈ tập trung vào các file cốt lõi**:
  1. **File Form UI**: `TenLenhForm.cs` (giao diện, controls, persistent state).
  2. **File Lệnh Logic**: `TenLenh.cs` (command methods, xử lý CAD/Civil 3D transaction).
  3. *(Tùy chọn)*: Thêm 1 dòng vào `ClassicMenu.cs` nếu cần mục trên thanh Menu.
- **KHÔNG** tạo hay chỉnh sửa các file phụ trợ thừa thãi (như chatbot, doc trợ giúp gõ tay...).

---

## 5. 🏗️ CẤU TRÚC HẠ TẦNG BUILD & RELOAD
- **Output build nằm NGOÀI Dropbox**: `C:\CadBuild\Autocad2026_API\{ProjectName}\bin\{Config}\` do `Directory.Build.props` ở gốc solution quyết định. **KHÔNG** dùng đường dẫn Dropbox cho output.
- **Không có thư mục con TFM**: `AppendTargetFrameworkToOutputPath=false` → output là `bin\Debug\` chứ KHÔNG phải `bin\Debug\net10.0-windows\`.
- **dotnet SDK chỉ có tại per-user**: `$env:USERPROFILE\.dotnet\dotnet.exe` (ưu tiên 1). Đường dẫn `$env:ProgramFiles\dotnet\dotnet.exe` chỉ có Runtime, KHÔNG có SDK → `dotnet build` sẽ lỗi "No .NET SDKs were found". Khi gọi dotnet trực tiếp (ngoài BuildProject.ps1), luôn set `$env:DOTNET_ROOT` và thêm vào `$env:PATH`:
  ```powershell
  $env:DOTNET_ROOT = "$env:USERPROFILE\.dotnet"
  $env:PATH = "$env:USERPROFILE\.dotnet;" + $env:PATH
  ```
- **CAD_Reloader.dll bị lock khi AutoCAD đang chạy**: Không thể build ghi đè. Dùng output tạm: `/p:OutputPath="C:\CadBuild\...\Debug_new"`. Thay đổi sẽ có hiệu lực khi khởi động lại AutoCAD.
- **Hệ thống Reload**:
  - **CAD_Reloader** (project chính): Lệnh `NETRELOAD` / `RELOAD` / `NETRELOADUI` — shadow copy → `ExtensionLoader.Load()`, có Form UI, tự xử lý `SECURELOAD` và `TRUSTEDPATHS`.
  - **NetReload** (project phụ/cũ): Lệnh `NRL` — `dotnet build` với random AssemblyName → `Assembly.LoadFrom()`.
  - **KHÔNG** để hai project cùng đăng ký lệnh trùng tên (đã bỏ `RELOAD` khỏi NetReload).
- **PowerShell**: Dùng `;` để nối lệnh, **KHÔNG** dùng `&&` (không hỗ trợ trên hệ thống này).

---

## 6. 📦 GIT WORKFLOW
- Remote: `https://github.com/Thang605/Autocad_API.git`, branch `master`.
- Commit message bằng tiếng Việt không dấu, prefix theo conventional commits (`fix:`, `feat:`, `refactor:`...).
- Chỉ commit các file source code đã sửa, **KHÔNG** commit output build (`C:\CadBuild\`), file tạm (`last_dll.txt`, `last_reload.lsp`).

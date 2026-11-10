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

## 6. 📦 GIT WORKFLOW & GOOGLE JULES TỰ ĐỘNG HẰNG NGÀY
- **Remote**: `https://github.com/Thang605/Autocad_API.git`, branch `master`.
- **Tự động Commit & Push cuối mỗi yêu cầu (Không làm chậm tiến độ)**:
  - Không commit lắt nhắt ở các bước sửa nháp trung gian để tránh làm chậm tiến độ.
  - Ngay sau khi **hoàn tất một yêu cầu/tính năng** và chạy `BuildProject.ps1` đạt `0 Error(s)`, AI **BẮT BUỘC** tự động chạy `git add` + `git commit` (mất `< 0.2s`) và `git push` cho các file mã nguồn đã sửa/tạo mới để thanh **Review** của Antigravity luôn sạch (`0 Uncommitted`) ở mọi phiên làm việc.
- Commit message bằng tiếng Việt không dấu, prefix theo conventional commits (`fix:`, `feat:`, `refactor:`...).
- Chỉ commit các file source code đã sửa, **KHÔNG** commit output build (`C:\CadBuild\`), file tạm (`last_dll.txt`, `last_reload.lsp`).
- **Quy trình Tự động Check Code với Google Jules (`https://jules.google.com/repo/github/Thang605/Autocad_API/overview`):**
  - **File quy tắc chuẩn cho Jules**: `AGENTS.md` ở gốc dự án và `Directory.Build.props` (`EnableWindowsTargeting=true`).
  - **Environment Setup Script chuẩn**: `dotnet --info || true` và `git checkout -- .` (đảm bảo working tree sạch 100% trước khi Jules snapshot).
  - **Lịch chạy 00:00 (12:00 AM GMT+7 hằng ngày)**: 3 tác nhân **Bolt ⚡** (Performance), **Palette 🎨** (UX/WinForms A11y), **Sentinel 🛡️** (Defensive Coding & CAD Transaction Safety) tự động quét nhánh `master` và tạo bản vá.
  - **Lịch duyệt 08:00 (08:00 AM GMT+7 hằng ngày)**: Antigravity (`daily-jules-autocad-review` lúc 08:00 sáng) mở trình duyệt kiểm tra kết quả của Jules, phản hồi phê duyệt các kế hoạch (Plan review) hợp lý bằng Tiếng Anh, kiểm tra diff code C# (bảo toàn `.csproj` HintPaths, `OpenMode.ForWrite`, Persistent State) và nhấn `Publish PR` cho các tác vụ `Ready for review` đạt chuẩn.

---

## 7. 🏷️ QUY TẮC ĐẶT TÊN LỆNH (COMMAND METHOD NAMING CONVENTION) - BẮT BUỘC
- **Chỉ dùng duy nhất 1 tên lệnh** cho mỗi phương thức `[CommandMethod("...")]`. Tuyệt đối không khai báo nhiều alias/tên phụ thừa thãi.
- **Cấu trúc đặt tên lệnh chuẩn**:
  - Tiền tố theo nhóm lệnh:
    - `AT_`: AutoCAD Tool chung
    - `CT_`: Civil 3D Tool chung
    - `CTA_`: Civil 3D Alignment (Tuyến tim)
    - `CTC_`: Civil 3D Corridor (Hành lang tuyến)
    - `CTP_`: Civil 3D Profile (Trắc dọc)
    - `CTS_`: Civil 3D Section (Trắc ngang) / Cọc
    - `CTT_`: Civil 3D TIN Surface (Bề mặt)
  - **Mỗi từ phân tách bằng dấu gạch dưới `_`**, viết hoa chữ cái đầu mỗi từ (`Prefix_Word_Word_Word`).
  - **Ví dụ chuẩn mẫu**:
    - `AT_Cut_Solid_Body_To_Clipboard`
    - `AT_Cap_Nhat_Mau_Va_Property_Set`
    - `AT_Clip_3d_Solid`
    - `AT_Tat_VP_Freeze_Layer`
    - `CTA_Kiem_Tra_Tam_Nhin`
    - `CTC_Xoa_Region_Bi_Tat`
    - `CTS_Doi_Ten_Coc_Quy_Tac`

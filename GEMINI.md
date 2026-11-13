# Quy Tắc Phát Triển Lệnh AutoCAD / Civil 3D (BẮT BUỘC)

## 1. 🌟 BẮT BUỘC TẠO FORM GIAO DIỆN (UI) CHO MỌI LỆNH MỚI
- **Mọi lệnh mới được tạo đều PHẢI có giao diện Form** (Windows Forms kế thừa `System.Windows.Forms.Form`).
- File Form đặt tên theo format: `TenLenhForm.cs` (Ví dụ: `BangThongKeParcelForm.cs`, `DieuChinhDuongDoForm.cs`).
- Form được thiết kế chuyên nghiệp, hiện đại (Segoe UI, bố cục GroupBox khoa học, hỗ trợ resize).
- **Bắt buộc dùng Dropdown (`ComboBox` / `DataGridViewComboBoxColumn`) & Chống cắt chữ High-DPI**:
  - Khi cho phép chọn Layer, TextStyle, DimStyle, Linetype, Block, Tỷ lệ bản vẽ, Surface, Alignment... **TUYỆT ĐỐI KHÔNG** bắt người dùng gõ tay tên đối tượng vào `TextBox` hoặc dàn ma trận nhiều nút bấm chật chội; phải nạp danh sách từ bản vẽ hiện hành vào `ComboBox` (`DropDownList` hoặc `SuggestAppend`) / `DataGridViewComboBoxColumn` kèm nút Pick từ CAD.
  - Luôn đặt `UseMnemonic = false` cho các `Button` / `Label` có chứa ký tự `&` (tránh bị WinForms nuốt dấu `&`) và thiết kế nút bấm đủ rộng (`Height >= 40px`) để không bị cắt chữ ở DPI cao.
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
- **Tránh xung đột namespace**: Sử dụng alias chuẩn (`Application = Autodesk.AutoCAD.ApplicationServices.Application`, `ATable = Autodesk.AutoCAD.DatabaseServices.Table`, `WinFormsLabel = System.Windows.Forms.Label`, `WinFormsComboBox = System.Windows.Forms.ComboBox`, `FlowDirection = System.Windows.Forms.FlowDirection`...).
- **Lưu ý khi gọi `autocad_execute_lisp` qua MCP**: Không viết comment `;` bên trong chuỗi `lisp_code` nhiều dòng vì trình nạp một dòng sẽ biến phần code phía sau dấu `;` thành chú thích.
- 🚨 **BẮT BUỘC BUILD KIỂM TRA SAU MỌI LẦN SỬA CODE**:
  - Sau **BẤT KỲ** thao tác chỉnh sửa, tạo mới, sửa lỗi, đổi tên hoặc refactor code nào, AI **BẮT BUỘC** phải tự động chạy lệnh build để xác nhận kết quả `0 Error(s)` trước khi hoàn thành phản hồi:
    `powershell -ExecutionPolicy Bypass -NoProfile -File "c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\BuildProject.ps1"`
  - **Tuyệt đối không bỏ qua bước build hoặc chỉ trả lời bằng lời nói mà chưa kiểm chứng kết quả biên dịch.**

---

## 4. 🗂️ KIẾN TRÚC 1 LỆNH = 1 THƯ MỤC NHIỀU FILE NHỎ (BẮT BUỘC CHO LỆNH MỚI)
> Mục tiêu: mỗi file đủ nhỏ để AI agent đọc/sửa trọn vẹn trong 1 lần, sửa 1 chỗ không phải tải lại 3000–4000 dòng.

- **Tạo khung lệnh bằng script, KHÔNG viết tay từ đầu**:
  `powershell -ExecutionPolicy Bypass -NoProfile -File "c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\NewCommand.ps1" -Name CTT_Ten_Lenh -Title "Tên hiển thị" -Description "Mô tả" -Register`
  - `-Name` phải đúng chuẩn mục 7 (script tự kiểm tra regex và trùng tên lệnh).
  - `-Register` tự thêm 1 dòng vào `Menu form\ClassicMenu.cs` và `Menu form\ToolPaletteDataReader.cs` (nhóm mặc định theo tiền tố, đổi bằng `-Group C1..C10 / A1..A9`).
  - `-BaseName` (tùy chọn) = tên thư mục/class, mặc định bỏ tiền tố và dấu `_` (`CTT_Tao_Mat_Phang` → `TaoMatPhang`).
- **Cấu trúc thư mục chuẩn** `MyFirstProject\Commands\<Prefix>\<BaseName>\` (namespace `MyFirstProject.Commands.<Prefix>.<BaseName>`):

  | File | Trách nhiệm | Giới hạn |
  |---|---|---|
  | `<Name>.Command.cs` | Chỉ `[CommandMethod]`: mở Form bằng `ShowModalDialog`, gọi Service, bọc `CommandGuard.Run` | ≤ 80 dòng |
  | `<Base>Options.cs` | POCO thông số người dùng (không tham chiếu WinForms) | ≤ 150 dòng |
  | `<Base>Service.cs` (+ `<Base>Service.<Phan>.cs` partial) | Transaction, đọc/ghi CAD/Civil 3D, `OpenMode.ForWrite` khi sửa | ≤ 400 dòng/file |
  | `<Base>Core.cs` / `Core\*.cs` (khi cần) | Thuật toán thuần (hình học, tính toán) – không mở Transaction, dễ test | ≤ 400 dòng/file |
  | `<Base>Form.cs` | Khởi tạo, sự kiện, Pick trên canvas (`StartUserInteraction`) | ≤ 400 dòng |
  | `<Base>Form.Layout.cs` (+ `Form.Layout.<Nhom>.cs`) | Dựng giao diện, mỗi GroupBox 1 hàm `Build…Group()` | ≤ 300 dòng/file |
  | `<Base>Form.Settings.cs` | Persistent state: biến `static _last…`, `SaveCurrentSettings()`, `RestoreLastSettings()` | ≤ 200 dòng |

- **Giới hạn kích thước (bắt buộc)**:
  - File: mục tiêu **≤ 400 dòng**, tối đa **800 dòng** → vượt thì tách thêm `partial class` theo chủ đề (`Service.Trim.cs`, `Form.Layout.Options.cs`...).
  - Phương thức: mục tiêu **≤ 60 dòng**, tối đa **120 dòng** → tách hàm con có tên rõ nghĩa.
  - Class lồng nhau (`private sealed class` hình học, lưới không gian...) dài > 100 dòng phải đưa ra file riêng.
- **Bắt buộc dùng thư viện chung `MyFirstProject\Shared\` trước khi viết helper mới** (không copy-paste helper giữa các lệnh):
  - `Shared\UI\FormBuilder.cs`: `ApplyStandardStyle`, `BuildBody`, `ActionButton` (cao 40px, `UseMnemonic=false`), `ButtonBar`, `Label/CheckBox/Radio/Number`, `CreateToolTip`, `StandardFonts` (Segoe UI dùng chung, không tạo/dispose Font riêng).
  - `Shared\UI\FieldGroup.cs`: GroupBox + lưới 3 cột (nhãn | ô nhập | nút Pick) – `AddRow`, `AddFullRow`.
  - `Shared\UI\DrawingComboBoxes.cs`: nạp Layer/TextStyle/DimStyle/Linetype/Block/Surface/Alignment vào `ComboBox` (`Fill`, `SelectByName`, `GetSelectedName`, `GetSelectedId`).
  - `Shared\UI\PersistentState.cs`: `IsAlive(id[, db])`, `FilterAlive`, `RestoreFormSize`, `CaptureFormSize`, `SetValueSafe`.
  - `Shared\Civil\SurfaceHelpers.cs`: `FindSurfaceByName`, `GetSurfaceNames`, `EnsureUniqueSurfaceName`, `FindSurfaceStyleId`, `FindAlignmentByName` (mở `ForRead`).
  - `Shared\Cad\ZoomHelper.cs`: `ZoomToExtents`, `ZoomToEntities`. `Shared\Cad\CommandGuard.cs`: `Run(...)` bắt lỗi + `LockDocument` tùy chọn.
  - Helper mới dùng được cho ≥ 2 lệnh → thêm vào `Shared\` (đúng thư mục `UI` / `Cad` / `Civil`), không để trong thư mục lệnh.
- **Quy trình làm lệnh mới**: (1) chốt spec thông số → (2) chạy `NewCommand.ps1` → (3) viết `Options` → (4) `Core`/`Service` → (5) `Form.Layout` + `Form` → (6) build `BuildProject.ps1` 0 lỗi → (7) commit.
- **Lệnh cũ** (các thư mục `Acad Tool`, `Civil Tool`, `Civil Tool 2`...): giữ nguyên, chỉ tách theo kiến trúc mới khi cần sửa lớn; khi tách **không đổi logic**, không đổi tên lệnh/menu.
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

---

## 8. 📐 QUY CHUẨN BẢN VẼ MẪU (`MAU CAD-V2.dwt.dwg` - KHÔNG DÙNG ANNOTATIVE) & ĐỘ DÀY NÉT VẼ
- **Không sử dụng Annotative**: Toàn bộ `TextStyle`, `DimStyle`, `MLeaderStyle` đều tắt `Annotative` (`ANNOTATIVEDWG = 0`).
- **Chuẩn gốc `1:1000` (Hệ mét `INSUNITS = 6`, `DIMLFAC = 1.0`, `DIMDEC = 2`)**:
  - Hệ số tỷ lệ cho bản vẽ `1:N` là $k = \frac{N}{1000.0}$.
  - **`TextStyle`**: Duy nhất kiểu `Standard` (`tahoma.ttf`, `WidthFactor = 0.9`, `Height = 0.0`). Chiều cao chữ thường (`01_TEXT`) $= 2.0 \times k$, chữ tiêu đề (`02_TEXT_TO`) $= 3.5 \times k$.
  - **`DimStyle`**: Bộ 15 kiểu chuẩn (`1-10` đến `1-10000`) nhân bản từ `1-1000` (`DIMTXT = 2.0`, `DIMASZ = 1.8`, `DIMSCALE = k`).
  - **`MLeaderStyle`**: Kiểu `T27-LEADER` và `Standard` (`TextStyle = Standard`, `TextHeight = 2.0`, `ArrowSize = 1.8`, `ScaleFactor = k`).
- **Bộ 15 Layer chuẩn & Quản lý độ dày nét theo `Lineweight ByLayer` (BẮT BUỘC)**:
  - `01_TEXT` (Màu 7, 0.18mm), `02_TEXT_TO` (Màu 6, 0.18mm), `03_DIM` (Màu 11, 0.13mm), `04_BAO_BT` (Màu 3, 0.25mm), `05_BAO_COT_THEP` (Màu 4, 0.15mm), `06_THEP` (Màu 2, 0.35mm), `07_TRUC` (Màu 150, `DASHDOT2`, 0.13mm), `08_KHUAT` (Màu 25, `HIDDEN2`, 0.13mm), `09_HATCH` (Màu 144, 0.09mm), `10_RANH_GIOI` (Màu 1, `BORDER2`, 0.70mm), `11_VIEWPORT` (Màu 8, `No Plot`), `12_KY_HIEU_BLOCK` (Màu 4, 0.15mm), `13_MAT_DAT_TN` (Màu 8, `DAT`, 0.13mm), `14_HA_TANG_TALUY` (Màu 30, 0.15mm), `15_KHUNG_BANG` (Màu 7, 0.25mm).
  - **Không gán `Global width` (`ConstantWidth > 0`) cho `Polyline` cốt thép / hình học**: Luôn giữ `Global width = 0.0` để độ dày nét vẽ tuân thủ hoàn toàn **`Lineweight = ByLayer`** của từng Layer (`06_THEP = 0.35mm`, `05_BAO_COT_THEP = 0.15mm`, `04_BAO_BT = 0.25mm`), tránh làm mất tác dụng `Lineweight ByLayer` và tránh biến dạng nét khi đổi tỷ lệ in.
- **Quy tắc xử lý `Hatch`, `LTSCALE` và Mặt cắt BTCT**:
  - Giữ `LTSCALE = 1.0` cố định; đặt `CELTSCALE = k`, `HPSCALE = k`, `MLEADERSCALE = k`. Khi đổi tỷ lệ đối tượng đã vẽ, nhân `Hatch.PatternScale` và `Curve.LinetypeScale` theo tỷ số $\text{ratio} = k_{\text{đích}} / k_{\text{nguồn}}$ (các mẫu Hatch trong bảng mẫu như `AR-CONC = 0.02`, `BT-01,_O = 0.05` được định nghĩa ở tỷ lệ `1:100` ứng với $k = 0.10$).
  - Khi vẽ mặt cắt ngang kết cấu BTCT, không tô Hatch đè lên vùng lõi tiết diện chứa cốt đai và chấm cốt thép chủ; chỉ tô Hatch ở phần cánh sàn giao cắt hai bên.

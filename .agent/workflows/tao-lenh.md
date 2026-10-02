---
description: Quy trình tạo lệnh mới cho AutoCAD/Civil3D plugin
---

# Quy trình tạo lệnh mới cho AutoCAD / Civil 3D

## Bước 1: Quy tắc đặt tên lệnh (BẮT BUỘC)
- **Chỉ dùng DUY NHẤT 1 tên lệnh** cho mỗi phương thức `[CommandMethod("...")]`. Tuyệt đối không đặt nhiều alias/tên phụ trùng lặp.
- **Cấu trúc đặt tên**:
  - Tiền tố theo nhóm:
    - `AT_`: AutoCAD Tool chung
    - `CT_`: Civil 3D Tool chung
    - `CTA_`: Civil 3D Alignment (Tuyến tim)
    - `CTC_`: Civil 3D Corridor (Hành lang tuyến)
    - `CTP_`: Civil 3D Profile (Trắc dọc)
    - `CTS_`: Civil 3D Section (Trắc ngang) / Cọc
    - `CTT_`: Civil 3D TIN Surface (Bề mặt)
  - **Mỗi từ phân tách bằng dấu gạch dưới `_`**, viết hoa chữ cái đầu mỗi từ:
    `Prefix_Word_Word_Word`
  - *Ví dụ mẫu*:
    - `AT_Cut_Solid_Body_To_Clipboard`
    - `AT_Cap_Nhat_Mau_Va_Property_Set`
    - `AT_Clip_3d_Solid`
    - `AT_Tat_VP_Freeze_Layer`
    - `CTA_Kiem_Tra_Tam_Nhin`
    - `CTC_Xoa_Region_Bi_Tat`
    - `CTS_Doi_Ten_Coc_Quy_Tac`

## Bước 2: Tạo file lệnh & Form UI (BẮT BUỘC FORM UI)
- **Mọi lệnh mới đều PHẢI có giao diện Form** (kế thừa `System.Windows.Forms.Form`).
- **File Lệnh**: Tạo file `.cs` mới trong thư mục `Acad Tool` hoặc `Civil Tool`:
  - Format file: `XX.Prefix_TenLenh.cs` (Ví dụ: `31.AT_CapNhatMauVaPropertySet.cs`, `34.AT_Clip3dSolid.cs`).
  - Đặt `[assembly: CommandClass(typeof(Civil3DCsharp.TenClass))]` ở đầu file.
- **File Form UI**:
  - Format file: `TenLenhForm.cs` (Ví dụ: `CapNhatMauVaPropertySetForm.cs`, `Clip3dSolidForm.cs`).
  - Form thiết kế hiện đại (Segoe UI, bố cục GroupBox khoa học, hỗ trợ resize).
  - Tương tác với Canvas: dùng `ed.StartUserInteraction(form)` khi pick điểm/chọn đối tượng trên CAD để tạm ẩn Form.
  - Hiển thị Form bằng `Application.ShowModalDialog(form)` trong phương thức lệnh.

## Bước 3: Ghi nhớ thông số & lựa chọn (Persistent State - BẮT BUỘC)
- **Lưu vết đối tượng đã chọn**:
  - Khai báo `private static ObjectId _lastObjectId = ObjectId.Null;` trong Command Class hoặc Form.
  - Khi mở lại Form, kiểm tra `!_lastObjectId.IsNull && _lastObjectId.IsValid && !_lastObjectId.IsErased` để tự động nạp lại.
- **Ghi nhớ giá trị nhập & tùy chọn UI**:
  - Khai báo các biến `private static` trong Form cho toàn bộ TextBox, ComboBox, CheckBox, NumericUpDown...
  - Viết hàm `SaveCurrentSettings()` (gọi khi `FormClosing` hoặc bấm thực thi).
  - Viết hàm `RestoreLastSettings()` (gọi trong constructor của Form).
  - Ghi nhớ kích thước Form nếu có resize (`_lastFormSize`).

## Bước 4: Kiểm tra xung đột namespace & Transaction
- **Namespace aliases thường dùng**:
  ```csharp
  using Application = Autodesk.AutoCAD.ApplicationServices.Application;
  using Color = Autodesk.AutoCAD.Colors.Color;
  using DrawingColor = System.Drawing.Color;
  using DrawingFont = System.Drawing.Font;
  using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
  using WinFormsLabel = System.Windows.Forms.Label;
  ```
- **Transaction & OpenMode**:
  - Luôn dùng `OpenMode.ForWrite` khi sửa đổi đối tượng.
  - Bọc logic sửa đổi trong `using (Transaction tr = db.TransactionManager.StartTransaction())`.

## Bước 5: Build kiểm tra (BẮT BUỘC 0 ERROR)
- Sau khi viết code, **bắt buộc** chạy lệnh build để xác nhận kết quả:
  ```powershell
  powershell -ExecutionPolicy Bypass -NoProfile -File "c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\BuildProject.ps1"
  ```
- Sửa triệt để tất cả lỗi cho tới khi `0 Warning(s), 0 Error(s)`.

## Bước 6: Thêm mục vào Menu Classic (Tùy chọn)
- Nếu cần mục trên thanh menu, thêm 1 dòng vào `ClassicMenu.cs`:
  ```csharp
  AddMenuItem(subMenu, "Tên hiển thị menu", "TEN_LENH_CHUAN ");
  ```


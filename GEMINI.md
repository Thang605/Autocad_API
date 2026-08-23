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

## 3. ⚙️ QUY CHUẨN KỸ THUẬT & TRANSACTION
- **Transaction & OpenMode**: Luôn sử dụng `OpenMode.ForWrite` khi sửa đổi đối tượng Civil 3D/AutoCAD trong `Transaction`.
- **Tránh xung đột namespace**: Sử dụng alias chuẩn (`Application = Autodesk.AutoCAD.ApplicationServices.Application`, `ATable = Autodesk.AutoCAD.DatabaseServices.Table`, `WinFormsLabel = System.Windows.Forms.Label`...).
- **Build kiểm tra**: Sau khi viết code, PHẢI build kiểm tra thành công `0 Error(s)` bằng:
  `& "C:\Users\thang\.dotnet\dotnet.exe" build "c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\MyFirstProject\MyFirstProject.csproj"`.

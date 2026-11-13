# Civil 3D Tools

Bộ công cụ AutoCAD / Civil 3D bằng C#, chạy trên Windows x64 với .NET 10.

## Các dự án

`MyFirstProject.sln` chứa ba dự án được lưu trong repository:

- `MyFirstProject`: plugin chính, xuất `Civil3D_Tools.dll`.
- `CAD_Reloader`: tiện ích nạp lại chính (`NETRELOAD`, `RELOAD`, `NETRELOADUI`).
- `NetReload`: tiện ích nạp lại phụ (`NRL`).

Solution không yêu cầu mã nguồn công cụ ngoài `SnoopCivil3D`. Bộ kiểm thử WPF nằm riêng tại `tests/PaletteChecks`.

## Build

Cần .NET 10 SDK và các thư viện AutoCAD / Civil 3D 2027 được cài ở đường dẫn khai báo trong các file `.csproj`. Không thay thế hoặc xóa các tham chiếu Autodesk để vượt qua lỗi thiếu thư viện.

Chạy tại thư mục repository:

```powershell
dotnet build MyFirstProject.sln -c Debug
dotnet build MyFirstProject.sln -c Release
```

Theo `Directory.Build.props`, output nằm ngoài Dropbox tại `C:\CadBuild\Autocad2026_API\<tên dự án>\bin\<cấu hình>\`. Cả ba DLL đều hướng tới x64; solution hỗ trợ cấu hình `Any CPU` và `x64`.

Để tạo riêng DLL plugin có tên duy nhất phục vụ nạp lại, chạy:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\BuildProject.ps1
```

Script ghi đường dẫn vào `last_dll.txt`; build thông thường không tự nạp DLL vào AutoCAD. Cảnh báo `NU1900` nghĩa là NuGet không truy cập được nguồn kiểm tra lỗ hổng; cần phân biệt với lỗi biên dịch.

## Kiểm thử

```powershell
dotnet build tests/PaletteChecks/PaletteChecks.csproj -c Release "-p:CadBuildRoot=$env:TEMP\CodexCadOptimization\"
& "$env:TEMP\CodexCadOptimization\PaletteChecks\bin\Release\PaletteChecks.exe"
```

Xem [hướng dẫn kiểm thử Palette](tests/PaletteChecks/README.md) để đo hiệu năng và xuất ảnh giao diện. Các kiểm thử này chạy control WPF thật, không cần mở AutoCAD; chúng không kiểm chứng transaction, hình học Civil 3D hoặc hot-reload. Các phần đó phải được kiểm tra trong bản vẽ thử riêng, không lưu thay đổi vào bản vẽ sản xuất.

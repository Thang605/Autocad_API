---
description: Tách file .cs lớn thành nhiều file nhỏ theo nhóm lệnh - Refactor C# command files
---

# Workflow: Tách file .cs theo nhóm lệnh (/tach-file)

Khi người dùng yêu cầu tách 1 file .cs lớn chứa nhiều lệnh ra các file nhỏ hơn.

## Bước 1: Phân tích file gốc

1. Đọc toàn bộ file .cs cần tách
2. Liệt kê tất cả các lệnh (`[CommandMethod("...")]`) có trong file
3. Phân nhóm các lệnh theo chức năng (dựa vào tên lệnh, logic code, comment)
4. Trình bày bảng phân nhóm cho user xác nhận trước khi tách

## Bước 2: Quy tắc đặt tên file con

- Giữ prefix số thứ tự của file gốc, thêm suffix chữ cái: `07b`, `07c`, `07d`...
- Tên file mô tả nhóm chức năng: `07c.SamplelineRename.cs`, `07d.SamplelineTable.cs`
- Quy ước suffix:
  - `b` → nhóm đầu tiên được tách
  - `c, d, e...` → các nhóm tiếp theo theo thứ tự logic

## Bước 3: Tạo file con

Mỗi file con phải có đầy đủ:

```csharp
// Nhóm lệnh: [Mô tả nhóm]
// Tách từ [tên file gốc]
//
using System;
// ... các using cần thiết (chỉ copy những using thực sự dùng trong file)

using Autodesk.AutoCAD.Runtime;
// ... các using AutoCAD/Civil3D

using MyFirstProject.Extensions;

[assembly: CommandClass(typeof(Civil3DCsharp.[TênClass]))]

namespace Civil3DCsharp
{
    public class [TênClass]
    {
        // Các lệnh CommandMethod
        // Các helper method chỉ dùng trong nhóm này
    }
}
```

### Checklist cho mỗi file con:
- [ ] Có đủ `using` directives cần thiết
- [ ] Có `[assembly: CommandClass(...)]` attribute
- [ ] Cùng namespace `Civil3DCsharp`
- [ ] Tên class khác nhau giữa các file (không trùng tên class)
- [ ] Copy nguyên vẹn code lệnh (không sửa logic)
- [ ] Bao gồm helper methods / inner classes mà nhóm lệnh đó sử dụng
- [ ] Không để helper method bị duplicate giữa các file

## Bước 4: Thay file gốc bằng file index

File gốc `.cs` được thay bằng file comment-only liệt kê cấu trúc:

```csharp
// ============================================================
// [Tên module] - [Mô tả]
// ============================================================
//
// File gốc đã được tách ra thành các file nhỏ hơn:
//
// [tên file con 1]   - [mô tả] ([số lệnh] lệnh)
//   - [tên lệnh 1]
//   - [tên lệnh 2]
//
// [tên file con 2]   - [mô tả] ([số lệnh] lệnh)
//   - [tên lệnh 3]
//   ...
//
// Tổng cộng: [N] lệnh trong [M] file
// ============================================================
```

## Bước 5: Build và kiểm tra

// turbo
1. Build project:
```powershell
dotnet build
```
Cwd: `c:\Onedrive\0.AI AGENT\6.C#\Autocad 2026_API\MyFirstProject`

2. Nếu build lỗi:
   - Kiểm tra thiếu `using` → bổ sung
   - Kiểm tra trùng tên class → đổi tên
   - Kiểm tra thiếu helper method → copy sang file đúng
   - Build lại cho đến khi thành công

## Lưu ý quan trọng

- **KHÔNG sửa logic code** khi tách - chỉ di chuyển nguyên vẹn
- **Helper methods**: nếu 1 helper dùng chung nhiều nhóm → giữ ở file utility chung hoặc duplicate
- **Form/Dialog classes**: tách theo nhóm lệnh sử dụng chúng
- **Thứ tự tách**: tách từ nhóm nhỏ, đơn giản trước → nhóm lớn, phức tạp sau
- **Luôn build sau mỗi lần tách** để phát hiện lỗi sớm

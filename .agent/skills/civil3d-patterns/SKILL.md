---
name: Civil 3D Command Patterns
description: Các pattern và template code chuẩn để phát triển lệnh AutoCAD Civil 3D bằng C#
---

# Civil 3D Command Patterns

Skill này cung cấp các pattern và code template chuẩn để phát triển lệnh Civil 3D cho dự án này.

## 📁 Cấu trúc thư mục

```
MyFirstProject/
├── Acad Tool/          # Các lệnh AutoCAD thuần (sử dụng prefix AT_)
├── Civil Tool/         # Các lệnh Civil 3D chính (sử dụng prefix CT_)
├── Civil Tool 2/       # Các lệnh Civil 3D mở rộng
├── Extensions/         # Các extension methods
├── Help System/        # Hệ thống trợ giúp
└── Menu form/          # Các form menu
```

## 🔤 Quy tắc đặt tên lệnh

| Prefix | Ý nghĩa | Ví dụ |
|--------|---------|-------|
| `AT_` | AutoCAD Tool | `AT_PolylineFromSection` |
| `CT_` | Civil Tool (chung) | `CT_ThongTinDoiTuong` |
| `CTS_` | Civil Tool - Sample Line | `CTS_DoiTenCoc` |
| `CTA_` | Civil Tool - Alignment | `CTA_TaoDuong_ConnectedAlignment_NutGiao` |
| `CTC_` | Civil Tool - Corridor | `CTC_TaoCorridor_ChoTuyenDuong` |
| `CTP_` | Civil Tool - Profile | `CTP_ThayDoi_profile_Band` |
| `CTPV_` | Civil Tool - Profile View | `CTPV_VeTracDoc` |
| `CTSV_` | Civil Tool - Section View | `CTSV_VeTracNgangThietKe` |
| `CTSU_` | Civil Tool - Surface | `CTSU_CaoDoMatPhang_TaiCogopoint` |
| `CTPO_` | Civil Tool - Cogo Point | `CTPO_DoiTen_Cogopoint` |
| `CTPI_` | Civil Tool - Pipe | `CTPI_ThayDoi_CaoDo_DayCong` |

## 🧩 Template Command cơ bản

### Template 1: Command đơn giản với Transaction

```csharp
using System;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.Civil.DatabaseServices;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(Civil3DCsharp.TenClass))]

namespace Civil3DCsharp
{
    public class TenClass
    {
        [CommandMethod("TEN_LENH")]
        public static void TenMethod()
        {
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                // 1. Khởi tạo utilities
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();

                // 2. Lấy input từ user
                ObjectId alignmentId = UserInput.GAlignmentId("\\n Chọn tim đường:");
                Alignment? alignment = tr.GetObject(alignmentId, OpenMode.ForWrite) as Alignment;

                // 3. Xử lý logic
                // ... code logic ở đây

                // 4. Commit transaction
                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }
    }
}
```

### Template 2: Command với Form (Dialog), Tương tác Canvas & Ghi nhớ đối tượng

```csharp
public class TenCommandClass
{
    // Lưu giữ vết đối tượng đã chọn cho lần thực hiện sau
    private static ObjectId _lastSelectedId = ObjectId.Null;

    [CommandMethod("TEN_LENH_FORM")]
    public static void TenMethodForm()
    {
        Document doc = Application.DocumentManager.MdiActiveDocument;
        Editor ed = doc.Editor;
        Database db = doc.Database;

        var form = new MyFirstProject.Civil_Tool.TenForm();

        // 1. Nạp lại đối tượng đã chọn ở lần chạy trước (nếu còn hợp lệ)
        if (!_lastSelectedId.IsNull && _lastSelectedId.IsValid && !_lastSelectedId.IsErased)
        {
            try
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    DBObject obj = tr.GetObject(_lastSelectedId, OpenMode.ForRead);
                    if (obj != null)
                    {
                        form.SelectedObjectId = _lastSelectedId;
                        // form.TxtName.Text = ...;
                    }
                }
            }
            catch { }
        }

        // 2. Sự kiện pick đối tượng / điểm từ Form
        form.BtnPick.Click += (s, e) =>
        {
            using (var interaction = ed.StartUserInteraction(form))
            {
                PromptPointOptions ppo = new PromptPointOptions("\nChọn điểm trên bản vẽ: ");
                PromptPointResult ppr = ed.GetPoint(ppo);
                interaction.End();

                if (ppr.Status == PromptStatus.OK)
                {
                    form.SelectedPoint = ppr.Value;
                }
            }
        };

        // 3. Hiển thị form Modal Dialog
        var result = Application.ShowModalDialog(form);

        if (result == System.Windows.Forms.DialogResult.OK && form.FormAccepted)
        {
            // Lưu lại đối tượng chọn cho lần sau
            _lastSelectedId = form.SelectedObjectId;

            // 4. Bắt đầu transaction và thực thi logic
            using Transaction tr = db.TransactionManager.StartTransaction();
            try
            {
                // ... xử lý logic với giá trị từ form
                tr.Commit();
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n Lỗi: {ex.Message}");
            }
        }
    }
}
```

### Template 3: Command với vòng lặp (nhiều lần chọn)

```csharp
[CommandMethod("TEN_LENH_LOOP")]
public static void TenMethodLoop()
{
    ObjectId profileViewId = UserInput.GProfileViewId("\\n Chọn trắc dọc:");
    string answer = "y";

    while (answer == "y")
    {
        using (Transaction tr = A.Db.TransactionManager.StartTransaction())
        {
            try
            {
                // ... xử lý logic trong mỗi vòng lặp

                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage(e.Message);
            }
        }

        // Hỏi tiếp tục
        answer = UserInput.GString("Tiếp tục? (y/n)");
    }
}
```

## 🔧 Các Helper Class quan trọng

### Class A (Application shortcut)
```csharp
// Truy cập nhanh các đối tượng thường dùng
A.Doc    // Document hiện tại
A.Db     // Database hiện tại
A.Ed     // Editor hiện tại

// Ví dụ sử dụng
A.Ed.WriteMessage("\\n Thông báo: ...");
using Transaction tr = A.Db.TransactionManager.StartTransaction();
```

### Class UserInput
```csharp
// Các method chọn đối tượng
UserInput.GAlignmentId("Prompt:")           // Chọn Alignment
UserInput.GSampleLineId("Prompt:")          // Chọn SampleLine  
UserInput.GProfileViewId("Prompt:")         // Chọn ProfileView
UserInput.GSectionView("Prompt:")           // Chọn SectionView
UserInput.GCogoPointId("Prompt:")           // Chọn CogoPoint
UserInput.GObjId("Prompt:")                 // Chọn Object bất kỳ
UserInput.GTable("Prompt:")                 // Chọn Table

// Input khác
UserInput.GPoint("Prompt:")                 // Chọn điểm
UserInput.GString("Prompt:")                // Nhập chuỗi
UserInput.GInt("Prompt:")                   // Nhập số nguyên
UserInput.GSelectionSet("Prompt:")          // Chọn nhiều đối tượng
UserInput.GStopWithESC()                    // Dừng với phím ESC
```

### Class UtilitiesC3D
```csharp
// Các utility Civil 3D
UtilitiesC3D.CreateSampleline(name, groupId, alignment, station)
UtilitiesC3D.CreateCogoPointFromPoint3D(point, description)
UtilitiesC3D.SetDefaultPointSetting(styleName, labelStyleName)
```

### Class UtilitiesCAD
```csharp
// Các utility AutoCAD
UtilitiesCAD.CreateTableCoordinate(...)
UtilitiesCAD.CreateOpenPolyline(...)
```

## ⚠️ QUAN TRỌNG: Luôn dùng OpenMode.ForWrite

> [!CAUTION]
> **LUÔN sử dụng `OpenMode.ForWrite`** thay vì `OpenMode.ForRead` khi mở đối tượng trong transaction.
> 
> Việc dùng `ForRead` có thể gây **crash AutoCAD** trong một số trường hợp, đặc biệt khi:
> - Đối tượng được tham chiếu bởi đối tượng khác
> - Transaction lồng nhau
> - Đối tượng Civil 3D phức tạp (Corridor, Surface, v.v.)

```csharp
// ❌ TRÁNH - Có thể gây crash
Alignment? alignment = tr.GetObject(alignmentId, OpenMode.ForRead) as Alignment;

// ✅ ĐÚNG - An toàn hơn
Alignment? alignment = tr.GetObject(alignmentId, OpenMode.ForWrite) as Alignment;
```

## ⚠️ Xử lý xung đột Namespace

Các alias thường dùng để tránh xung đột:

```csharp
// AutoCAD vs Windows Forms
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using DrawingFont = System.Drawing.Font;
using WinFormsComboBox = System.Windows.Forms.ComboBox;
using FlowDirection = System.Windows.Forms.FlowDirection;

// AutoCAD vs Civil 3D
using ATable = Autodesk.AutoCAD.DatabaseServices.Table;
using Section = Autodesk.Civil.DatabaseServices.Section;
using CivSurface = Autodesk.Civil.DatabaseServices.TinSurface;

// Namespace aliases
using Acad = Autodesk.AutoCAD.ApplicationServices;
using Civil = Autodesk.Civil.ApplicationServices;
```

## 📋 Danh sách using chuẩn cho file lệnh

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

// AutoCAD
using Autodesk.AutoCAD.Runtime;
using Acad = Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;

// Civil 3D
using Civil = Autodesk.Civil.ApplicationServices;
using Autodesk.Civil;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using Autodesk.Civil.Runtime;
using Autodesk.Civil.Settings;

// Aliases để tránh xung đột
using ATable = Autodesk.AutoCAD.DatabaseServices.Table;
using CivSurface = Autodesk.Civil.DatabaseServices.TinSurface;
using Section = Autodesk.Civil.DatabaseServices.Section;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

// Extensions của project
using MyFirstProject.Extensions;
```

## 🎨 Làm việc với các đối tượng Civil 3D

### Alignment (Tim tuyến)
```csharp
ObjectId alignmentId = UserInput.GAlignmentId("Chọn tim đường:");
Alignment? alignment = tr.GetObject(alignmentId, OpenMode.ForWrite) as Alignment;

// Các thuộc tính
alignment.Name              // Tên
alignment.Length            // Chiều dài
alignment.StartingStation   // Lý trình đầu
alignment.EndingStation     // Lý trình cuối

// Lấy tọa độ tại station
double x = 0, y = 0;
alignment.PointLocation(station, offset, ref x, ref y);

// Lấy các điểm hình học
Station[] stations = alignment.GetStationSet(StationTypes.GeometryPoint);
```

### SampleLine Group & SampleLine (Hệ cọc)
```csharp
// Lấy sample line group đầu tiên của alignment
ObjectId sampleLineGroupId = alignment.GetSampleLineGroupIds()[0];
SampleLineGroup? group = tr.GetObject(sampleLineGroupId, OpenMode.ForWrite) as SampleLineGroup;

// Tạo mới sample line group
ObjectId newGroupId = SampleLineGroup.Create(groupName, alignmentId);

// Duyệt qua các sample line
foreach (ObjectId slId in group.GetSampleLineIds())
{
    SampleLine? sl = tr.GetObject(slId, OpenMode.ForWrite) as SampleLine;
    double station = sl.Station;
    string name = sl.Name;
    int number = sl.Number;
}
```

### ProfileView (Trắc dọc)
```csharp
ObjectId pvId = UserInput.GProfileViewId("Chọn trắc dọc:");
ProfileView? pv = tr.GetObject(pvId, OpenMode.ForWrite) as ProfileView;

// Tìm station & elevation tại điểm click
double station = 0, elevation = 0;
pv.FindStationAndElevationAtXY(point.X, point.Y, ref station, ref elevation);

// Lấy alignment của profile view
ObjectId alignmentId = pv.AlignmentId;
```

### SectionView (Trắc ngang)
```csharp
ObjectId svId = UserInput.GSectionView("Chọn trắc ngang:");
SectionView? sv = tr.GetObject(svId, OpenMode.ForWrite) as SectionView;

// Tìm offset & elevation
double offset = 0, elevation = 0;
sv.FindOffsetAndElevationAtXY(point.X, point.Y, ref offset, ref elevation);

// Lấy sample line
ObjectId sampleLineId = sv.SampleLineId;
```

### Surface (Mặt phẳng địa hình)
```csharp
ObjectId surfaceId = UserInput.GObjId("Chọn mặt phẳng:");
CivSurface? surface = tr.GetObject(surfaceId, OpenMode.ForWrite) as CivSurface;

// Tìm cao độ tại tọa độ
double elevation = surface.FindElevationAtXY(x, y);
```

### CogoPoint (Điểm khảo sát)
```csharp
ObjectId pointId = UserInput.GCogoPointId("Chọn điểm:");
CogoPoint? point = tr.GetObject(pointId, OpenMode.ForWrite) as CogoPoint;

// Thuộc tính
string name = point.PointName;
double x = point.Easting;
double y = point.Northing;
double z = point.Elevation;
string description = point.RawDescription;
```

## ✅ Checklist khi tạo lệnh mới

1. [ ] Tạo file với prefix và đặt tên phù hợp (VD: `35.CTS_TenLenh.cs`)
2. [ ] Thêm `[assembly: CommandClass(...)]` ở đầu file
3. [ ] Sử dụng namespace `Civil3DCsharp`
4. [ ] Thêm các using cần thiết và alias tránh xung đột (`Application`, `ATable`, `FlowDirection`, `WinFormsComboBox`...)
5. [ ] Đặt tên method phù hợp với tên lệnh (VD: `CTSTenMethod`)
6. [ ] Bọc code trong `using Transaction` và `try-catch`
7. [ ] Sử dụng `A.Ed.WriteMessage()` để thông báo lỗi
8. [ ] Build để kiểm tra lỗi: `BuildProject.ps1` và tự động `git commit` + `git push` khi hoàn tất yêu cầu

## 📐 Các Pattern Xử Lý Style, Độ Dày Nét (`Lineweight ByLayer`) & Quy Đổi Tỷ Lệ AutoCAD

1. **Giữ `Global width = 0.0` cho `Polyline` cốt thép & hình học**:
   - Tuyệt đối không gán `ConstantWidth > 0` (`Global width`) cho các `Polyline` cốt thép (`06_THEP`, `05_BAO_COT_THEP`) hoặc đường bao kết cấu (`04_BAO_BT`), vì `ConstantWidth > 0` sẽ làm AutoCAD bỏ qua `Lineweight ByLayer` của Layer (`06_THEP = 0.35mm`, `05_BAO_COT_THEP = 0.15mm`) và làm biến dạng nét khi đổi tỷ lệ.
2. **Bảo toàn vị trí căn lề (`AlignmentPoint`) khi đổi chiều cao `DBText` / `AttributeReference`**:
   - Lưu `Position` và `AlignmentPoint` trước khi gán `Height`, sau đó gán lại `AlignmentPoint` và gọi `dbText.AdjustAlignment(db)`.
3. **Xóa triệt để `TextStyle` bị kẹt trong `ATTRIB` và thay thế `DimStyle Standard` gốc**:
   - Duyệt thêm `blkRef.AttributeCollection` để đổi `StyleName` của mọi `AttributeReference` trước khi Purge.
   - Để loại bỏ `DimStyle` `Standard` mặc định không cho xóa trực tiếp: chép dữ liệu của kiểu chuẩn (`1-1000`) đè lên bản ghi `Standard`, xóa bản ghi `1-1000` phụ và đổi tên bản ghi `Standard` thành `"1-1000"`.

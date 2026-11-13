using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using CadEntity = Autodesk.AutoCAD.DatabaseServices.Entity;
using CivSurface = Autodesk.Civil.DatabaseServices.TinSurface;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;
using DocumentCollectionEventArgs = Autodesk.AutoCAD.ApplicationServices.DocumentCollectionEventArgs;

namespace MyFirstProject.Civil_Tool_2
{
    public enum FeatureLineBoundaryMode
    {
        /// <summary>
        /// Tự động nhận diện đường hở & đường kín, cắt tam giác dây cung đường cong và tam giác bắc cầu mà không tạo mặt phẳng phụ.
        /// </summary>
        AutoSmart = 0,

        /// <summary>
        /// Giới hạn phạm vi theo các đường Biên bao ngoài (Outer) và Lỗ khoét (Hide) do người dùng chọn riêng trên CAD.
        /// </summary>
        CustomBoundaryEntities = 1,

        /// <summary>
        /// Chỉ thêm Breaklines thuần túy (kèm tùy chọn giới hạn Max Triangle Length chuẩn Civil 3D).
        /// </summary>
        BreaklinesOnly = 2
    }

    public class TaoMatPhangOptions
    {
        public bool CreateNewSurface { get; set; } = true;
        public string SurfaceName { get; set; } = string.Empty;
        public ObjectId ExistingSurfaceId { get; set; } = ObjectId.Null;
        public bool OverwriteExistingIfNameMatches { get; set; } = false;
        public string SurfaceStyleName { get; set; } = string.Empty;

        public List<ObjectId> SourceEntityIds { get; set; } = new List<ObjectId>();
        public FeatureLineBoundaryMode BoundaryMode { get; set; } = FeatureLineBoundaryMode.AutoSmart;
        public List<ObjectId> CustomOuterBoundaryIds { get; set; } = new List<ObjectId>();
        public List<ObjectId> CustomHideBoundaryIds { get; set; } = new List<ObjectId>();

        public double MidOrdinateDistance { get; set; } = 0.10;
        public bool UseSupplementing { get; set; } = true;
        public double SupplementingDistance { get; set; } = 2.00;

        public bool LimitMaxEdgeLength { get; set; } = true;
        public double MaxEdgeLength { get; set; } = 25.00;

        public bool AutoCloseNearClosed { get; set; } = true;
        public double AutoCloseTolerance { get; set; } = 0.10;
        public bool TreatNestedClosedAsHole { get; set; } = false;
        public bool AutoRebuild { get; set; } = true;

        /// <summary>
        /// Khi bổ sung vào mặt phẳng TIN có sẵn:
        /// true  = Tạo 1 mặt phẳng phụ mang tiền tố '&lt;TênMặtPhẳngCóSẵn&gt;_&lt;HậuTố&gt;' rồi Paste động vào mặt phẳng có sẵn để dễ quản lý trong Prospector.
        /// false = Gộp trực tiếp Breaklines/lưới vào mặt phẳng có sẵn (0 mặt phẳng phụ).
        /// </summary>
        public bool UseManagedSubSurfaceForExisting { get; set; } = true;

        /// <summary>
        /// Hậu tố (hoặc tên) của mặt phẳng phụ khi bổ sung vào mặt phẳng có sẵn.
        /// Hệ thống luôn đảm bảo tên mặt phẳng phụ bắt đầu bằng tiền tố '&lt;TênMặtPhẳngCóSẵn&gt;_'.
        /// </summary>
        public string SubSurfaceSuffixOrName { get; set; } = string.Empty;

        /// <summary>
        /// Surface Style áp dụng cho mặt phẳng phụ (mặc định ưu tiên '_No Display' để ẩn trên màn hình CAD nhưng vẫn quản lý rõ trong Prospector).
        /// </summary>
        public string SubSurfaceStyleName { get; set; } = "_No Display";

        /// <summary>
        /// Nếu mặt phẳng phụ cùng tên '&lt;TênMặtPhẳngCóSẵn&gt;_&lt;HậuTố&gt;' đã tồn tại:
        /// true = Cập nhật/ghi đè nội dung mặt phẳng phụ đó tại chỗ (giữ nguyên ObjectId &amp; liên kết PasteSurface).
        /// false = Tự động tăng số thứ tự để tạo mặt phẳng phụ mới.
        /// </summary>
        public bool OverwriteExistingSubSurface { get; set; } = false;

        /// <summary>
        /// Khi bổ sung vào mặt phẳng TIN có sẵn:
        /// true  = Đắp đè cao độ mới tại vùng chồng lấn (thay thế điểm/lưới cũ bên dưới Feature Line mới).
        /// false = Giữ nguyên cao độ mặt phẳng cũ tại vùng chồng lấn, chỉ nối mở rộng thêm phần diện tích mới bên ngoài.
        /// </summary>
        public bool OverwriteOverlapOnExisting { get; set; } = true;

        /// <summary>
        /// Khi mặt phẳng đích có nguồn gốc trực tiếp từ Corridor / Grading (FromCorridor / FromGrading):
        /// Tự động tạo hoặc cập nhật mặt phẳng tổng hợp '&lt;Tên&gt;_Gop' kế thừa động mặt phẳng gốc + ghép phần Feature Line mới.
        /// </summary>
        public bool CreateCombinedForCorridorSurface { get; set; } = false;
        public string CombinedSurfaceName { get; set; } = string.Empty;
    }

    public class SurfaceCreationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public ObjectId MasterSurfaceId { get; set; } = ObjectId.Null;
        public string FinalSurfaceName { get; set; } = string.Empty;
        public ObjectId CreatedSubSurfaceId { get; set; } = ObjectId.Null;
        public string CreatedSubSurfaceName { get; set; } = string.Empty;
        public int SubSurfaceTriangleCount { get; set; }
        public bool IsSupplementedIntoExisting { get; set; }
        public int PreviousTriangleCount { get; set; }
        public int SubSurfaceCount { get; set; }
        public int ProcessedCurveCount { get; set; }
        public int OpenCurveCount { get; set; }
        public int ClosedCurveCount { get; set; }
        public int TrimmedTriangleCount { get; set; }
        public int FinalTriangleCount { get; set; }
    }
}

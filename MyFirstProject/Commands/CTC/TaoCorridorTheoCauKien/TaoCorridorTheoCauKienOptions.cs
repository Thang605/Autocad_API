using System.Collections.Generic;

using Autodesk.AutoCAD.DatabaseServices;

namespace MyFirstProject.Commands.CTC.TaoCorridorTheoCauKien
{
    public enum ComponentBaselineSourceType
    {
        AlignmentAndProfile = 0,
        FeatureLine = 1
    }

    public sealed class CorridorComponentRowItem
    {
        public bool IsEnabled { get; set; } = true;
        public ComponentBaselineSourceType SourceType { get; set; } = ComponentBaselineSourceType.FeatureLine;
        public List<ObjectId> BaselineEntityIds { get; set; } = new List<ObjectId>();
        public string BaselineDisplayText { get; set; } = "(Chưa chọn)";
        public string FeatureLineStyleName { get; set; } = "";
        public string FeatureLineLayer { get; set; } = "";

        public ObjectId ExplicitProfileId { get; set; } = ObjectId.Null;
        public string ProfileName { get; set; } = "(Tự động lấy Đường đỏ)";

        public ObjectId AssemblyId { get; set; } = ObjectId.Null;
        public string AssemblyName { get; set; } = "";

        // Lý trình chỉ dùng khi tạo Region mới; Region đã có sẵn được giữ nguyên tuyệt đối
        public double StartStation { get; set; } = 0.0;
        public double EndStation { get; set; } = 0.0;
        public bool HasExistingCorridorRegion { get; set; } = false;
        public int ExistingRegionCount { get; set; } = 0;
        public string ExistingStationSummary { get; set; } = "";

        public double FrequencyTangent { get; set; } = 10.0;
        public double FrequencyCurve { get; set; } = 5.0;

        public bool UseDefaultSurface { get; set; } = true;
        public bool DisableSurfaceTarget { get; set; } = false;
        public ObjectId TargetSurfaceId { get; set; } = ObjectId.Null;
        public string TargetSurfaceName { get; set; } = "(Theo mặc định)";

        public List<ObjectId> OffsetElevationTargetIds { get; set; } = new List<ObjectId>();
        public string OffsetTargetsDisplay { get; set; } = "(Không)";
        public ObjectId SourceCorridorId { get; set; } = ObjectId.Null;

        // Cờ theo dõi chỉnh sửa trên Region đã tồn tại để giữ nguyên thiết lập cũ khi chỉ cập nhật FL
        public bool AssemblyModifiedByUser { get; set; } = false;
        public bool SurfaceTargetModifiedByUser { get; set; } = false;
        public bool OffsetTargetsModifiedByUser { get; set; } = false;
        public bool FrequencyModifiedByUser { get; set; } = false;
    }

    /// <summary>
    /// Thông số quét FeatureLine theo Site để xây dựng &amp; cập nhật 1 Corridor tổng.
    /// </summary>
    public sealed class TaoCorridorTheoCauKienOptions
    {
        public ObjectId ExistingCorridorId { get; set; } = ObjectId.Null;
        public string CorridorName { get; set; } = "COR_Tong";

        public bool HasSiteSelection { get; set; } = false;
        public ObjectId SelectedSiteId { get; set; } = ObjectId.Null;
        public string SelectedSiteName { get; set; } = "";
        public bool IsSiteLessMode { get; set; } = false;

        public ObjectId DefaultAssemblyId { get; set; } = ObjectId.Null;
        public ObjectId SelectedCodeSetStyleId { get; set; } = ObjectId.Null;
        public ObjectId DefaultTargetSurfaceId { get; set; } = ObjectId.Null;
        public double DefaultFreqTangent { get; set; } = 10.0;
        public double DefaultFreqCurve { get; set; } = 5.0;

        public bool AutoRebuild { get; set; } = true;
        public bool RemoveDeletedBaselines { get; set; } = true;
        public bool DeleteMergedSourceCorridors { get; set; } = true;

        public List<CorridorComponentRowItem> ComponentRows { get; set; } = new List<CorridorComponentRowItem>();
        public List<ObjectId> SourceCorridorsToMerge { get; set; } = new List<ObjectId>();
    }
}

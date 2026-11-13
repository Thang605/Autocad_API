using System.Collections.Generic;

using Autodesk.AutoCAD.DatabaseServices;

namespace MyFirstProject.Commands.CTC.TaoCorridorTheoCauKien
{
    public enum CorridorComponentBuildMode
    {
        SingleOrExistingCorridor = 0,
        SeparateCorridorPerComponent = 1
    }

    public enum ComponentBaselineSourceType
    {
        AlignmentAndProfile = 0,
        FeatureLine = 1
    }

    public sealed class CorridorComponentRowItem
    {
        public bool IsEnabled { get; set; } = true;
        public string ComponentCategory { get; set; } = "Lòng đường chính";
        public string ComponentName { get; set; } = "CauKien_1";
        public ComponentBaselineSourceType SourceType { get; set; } = ComponentBaselineSourceType.AlignmentAndProfile;
        public List<ObjectId> BaselineEntityIds { get; set; } = new List<ObjectId>();
        public string BaselineDisplayText { get; set; } = "(Chưa chọn)";
        public ObjectId ExplicitProfileId { get; set; } = ObjectId.Null;
        public string ProfileName { get; set; } = "(Tự động lấy Đường đỏ)";
        public ObjectId AssemblyId { get; set; } = ObjectId.Null;
        public string AssemblyName { get; set; } = "";
        public double StartStation { get; set; } = 0.0;
        public double EndStation { get; set; } = 0.0; // <= 0 nghĩa là lấy toàn bộ chiều dài
        public double FrequencyTangent { get; set; } = 10.0;
        public double FrequencyCurve { get; set; } = 5.0;
        public ObjectId TargetSurfaceId { get; set; } = ObjectId.Null;
        public string TargetSurfaceName { get; set; } = "(Theo mặc định)";
        public List<ObjectId> OffsetElevationTargetIds { get; set; } = new List<ObjectId>();
        public string OffsetTargetsDisplay { get; set; } = "(Không)";
        public ObjectId SourceCorridorId { get; set; } = ObjectId.Null;
    }

    /// <summary>
    /// Thông số xây dựng & gộp Corridor theo cấu kiện truyền từ Form sang Service.
    /// </summary>
    public sealed class TaoCorridorTheoCauKienOptions
    {
        public CorridorComponentBuildMode BuildMode { get; set; } = CorridorComponentBuildMode.SingleOrExistingCorridor;
        public ObjectId ExistingCorridorId { get; set; } = ObjectId.Null;
        public string CorridorNamePrefix { get; set; } = "COR_";
        public ObjectId SelectedCodeSetStyleId { get; set; } = ObjectId.Null;
        public ObjectId DefaultTargetSurfaceId { get; set; } = ObjectId.Null;
        public double DefaultFreqTangent { get; set; } = 10.0;
        public double DefaultFreqCurve { get; set; } = 5.0;
        public bool AutoRebuild { get; set; } = true;
        public bool DeleteMergedSourceCorridors { get; set; } = true;
        public List<CorridorComponentRowItem> ComponentRows { get; set; } = new List<CorridorComponentRowItem>();
        public List<ObjectId> SourceCorridorsToMerge { get; set; } = new List<ObjectId>();
    }
}

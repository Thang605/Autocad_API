using System.Collections.Generic;

namespace MyFirstProject.Commands.CTC.TaoCorridorTheoCauKien
{
    /// <summary>
    /// Công thức đồng bộ giữa Site FeatureLine và 1 Corridor tổng, lưu bền vững vào ExtensionDictionary của Corridor.
    /// </summary>
    public sealed class SiteCorridorRecipe
    {
        public int Version { get; set; } = 1;
        public bool HasSiteBinding { get; set; } = true;
        public string SiteHandle { get; set; } = "";
        public string SiteName { get; set; } = "";
        public bool IsSiteLess { get; set; } = false;

        public string DefaultAssemblyHandle { get; set; } = "";
        public string DefaultSurfaceHandle { get; set; } = "";
        public string CodeSetStyleHandle { get; set; } = "";
        public double DefaultFreqTangent { get; set; } = 10.0;
        public double DefaultFreqCurve { get; set; } = 5.0;
        public bool RemoveDeletedBaselines { get; set; } = true;

        /// <summary>
        /// Danh sách Handle của các FeatureLine đang được Corridor tổng quản lý từ Site (dùng để nhận diện FL đã xóa).
        /// </summary>
        public List<string> ManagedFeatureLineHandles { get; set; } = new List<string>();

        /// <summary>
        /// Ánh xạ Handle FL nguồn (AutoCorridorFeatureLine) → Handle FL bản sao độc lập (nếu có).
        /// </summary>
        public Dictionary<string, string> ClonedFeatureLineMap { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Cấu hình chi tiết theo từng FeatureLine Handle.
        /// </summary>
        public List<SavedFeatureLineConfig> PerFeatureLineConfigs { get; set; } = new List<SavedFeatureLineConfig>();

        /// <summary>
        /// Quy tắc tự động gán Mặt cắt &amp; Target theo (StyleName, Layer) cho FeatureLine mới thêm vào Site.
        /// </summary>
        public List<StyleLayerRuleConfig> StyleLayerRules { get; set; } = new List<StyleLayerRuleConfig>();
    }

    public sealed class SavedFeatureLineConfig
    {
        public string FlHandle { get; set; } = "";
        public bool IsEnabled { get; set; } = true;
        public string AssemblyHandle { get; set; } = "";
        public bool UseDefaultSurface { get; set; } = true;
        public bool DisableSurfaceTarget { get; set; } = false;
        public string TargetSurfaceHandle { get; set; } = "";
        public List<string> OffsetTargetHandles { get; set; } = new List<string>();
        public double FreqTangent { get; set; } = 10.0;
        public double FreqCurve { get; set; } = 5.0;
    }

    public sealed class StyleLayerRuleConfig
    {
        public string StyleName { get; set; } = "";
        public string LayerName { get; set; } = "";
        public string AssemblyHandle { get; set; } = "";
        public bool UseDefaultSurface { get; set; } = true;
        public bool DisableSurfaceTarget { get; set; } = false;
        public string TargetSurfaceHandle { get; set; } = "";
        public List<string> OffsetTargetHandles { get; set; } = new List<string>();
        public double FreqTangent { get; set; } = 10.0;
        public double FreqCurve { get; set; } = 5.0;
    }
}

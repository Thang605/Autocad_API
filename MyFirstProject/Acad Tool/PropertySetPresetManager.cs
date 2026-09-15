// (C) Copyright 2026 by T27
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MyFirstProject.Extensions;

namespace Civil3DCsharp
{
    /// <summary>
    /// Mẫu cấu hình Property Set hoàn chỉnh (bao gồm Tên và Danh sách trường thuộc tính)
    /// </summary>
    public class PropertySetTemplate
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public bool IsSelectedForExport { get; set; } = true;
        public List<PropertyDefinitionItem> Definitions { get; set; } = new();

        public PropertySetTemplate Clone()
        {
            return new PropertySetTemplate
            {
                Name = this.Name,
                Description = this.Description,
                IsSelectedForExport = this.IsSelectedForExport,
                Definitions = this.Definitions.Select(d => d.Clone()).ToList()
            };
        }
    }

    /// <summary>
    /// Quản lý danh mục các mẫu Property Set, lưu trữ lâu dài trong JSON tại %APPDATA%\Civil3D_Tools\PropertySetTemplates.json
    /// </summary>
    public static class PropertySetPresetManager
    {
        private static readonly string SettingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Civil3D_Tools"
        );

        private static readonly string TemplatesFilePath = Path.Combine(SettingsDirectory, "PropertySetTemplates.json");

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public static string FilePath => TemplatesFilePath;

        /// <summary>
        /// Nạp toàn bộ danh mục mẫu Property Set
        /// </summary>
        public static List<PropertySetTemplate> LoadTemplates()
        {
            try
            {
                if (File.Exists(TemplatesFilePath))
                {
                    string json = File.ReadAllText(TemplatesFilePath);
                    var list = JsonSerializer.Deserialize<List<PropertySetTemplate>>(json, JsonOptions);
                    if (list != null && list.Count > 0)
                    {
                        return list;
                    }
                }
            }
            catch { }

            var defaults = GetDefaultTemplates();
            try
            {
                SaveTemplates(defaults);
            }
            catch { }
            return defaults;
        }

        /// <summary>
        /// Lưu danh mục mẫu Property Set ra file JSON
        /// </summary>
        public static void SaveTemplates(IEnumerable<PropertySetTemplate> templates)
        {
            try
            {
                if (!Directory.Exists(SettingsDirectory))
                {
                    Directory.CreateDirectory(SettingsDirectory);
                }

                var list = templates.ToList();
                string json = JsonSerializer.Serialize(list, JsonOptions);
                File.WriteAllText(TemplatesFilePath, json);
            }
            catch { }
        }

        /// <summary>
        /// Danh mục các Property Set chuẩn BIM Việt Nam / Quốc tế mặc định
        /// </summary>
        public static List<PropertySetTemplate> GetDefaultTemplates()
        {
            var defaultDefs = PropertySetUtils.GetDefaultPropertyDefinitions();

            return new List<PropertySetTemplate>
            {
                new PropertySetTemplate
                {
                    Name = "IFC ĐƯỜNG GIAO THÔNG2",
                    Description = "Thuộc tính chuẩn BIM cho Hệ thống đường giao thông (bản 2)",
                    Definitions = defaultDefs.Select(d => d.Clone()).ToList()
                },
                new PropertySetTemplate
                {
                    Name = "IFC ĐƯỜNG GIAO THÔNG",
                    Description = "Thuộc tính chuẩn BIM cho Hệ thống đường giao thông",
                    Definitions = defaultDefs.Select(d => d.Clone()).ToList()
                },
                new PropertySetTemplate
                {
                    Name = "IFC THOÁT NƯỚC MƯA",
                    Description = "Thuộc tính chuẩn BIM cho Hệ thống thoát nước mưa",
                    Definitions = defaultDefs.Select(d => d.Clone()).ToList()
                },
                new PropertySetTemplate
                {
                    Name = "IFC THOÁT NƯỚC THẢI",
                    Description = "Thuộc tính chuẩn BIM cho Hệ thống thoát nước thải",
                    Definitions = defaultDefs.Select(d => d.Clone()).ToList()
                },
                new PropertySetTemplate
                {
                    Name = "IFC CẤP NƯỚC",
                    Description = "Thuộc tính chuẩn BIM cho Hệ thống cấp nước",
                    Definitions = defaultDefs.Select(d => d.Clone()).ToList()
                },
                new PropertySetTemplate
                {
                    Name = "IFC CÂY XANH",
                    Description = "Thuộc tính chuẩn BIM cho Hệ thống cây xanh & cảnh quan",
                    Definitions = defaultDefs.Select(d => d.Clone()).ToList()
                },
                new PropertySetTemplate
                {
                    Name = "IFC CHIẾU SÁNG & ĐIỆN",
                    Description = "Thuộc tính chuẩn BIM cho Hệ thống chiếu sáng và cấp điện",
                    Definitions = defaultDefs.Select(d => d.Clone()).ToList()
                },
                new PropertySetTemplate
                {
                    Name = "IFC HÀO KỸ THUẬT",
                    Description = "Thuộc tính chuẩn BIM cho Hào và Tuynen kỹ thuật",
                    Definitions = defaultDefs.Select(d => d.Clone()).ToList()
                },
                new PropertySetTemplate
                {
                    Name = "IFC KẾT CẤU & CẦU",
                    Description = "Thuộc tính chuẩn BIM cho Cầu và Kết cấu bê tông",
                    Definitions = defaultDefs.Select(d => d.Clone()).ToList()
                },
                new PropertySetTemplate
                {
                    Name = "BIM_THUOC_TINH_CHUNG",
                    Description = "Thuộc tính BIM dùng chung",
                    Definitions = defaultDefs.Select(d => d.Clone()).ToList()
                }
            };
        }
    }
}

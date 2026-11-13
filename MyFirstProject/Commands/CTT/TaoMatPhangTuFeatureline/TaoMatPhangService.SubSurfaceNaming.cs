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
    public partial class CTT_Tao_Mat_Phang_Tu_Featureline_Command
    {
        /// <summary>
        /// Chuẩn hóa hậu tố do người dùng nhập (tự động khử lặp tiền tố nếu người dùng lỡ gõ cả tên mặt phẳng gốc vào ô hậu tố).
        /// </summary>
        public static string ExtractCleanSubSurfaceSuffix(string parentSurfaceName, string rawSuffixOrName)
        {
            string parent = (parentSurfaceName ?? string.Empty).Trim();
            string suffix = (rawSuffixOrName ?? string.Empty).Trim();

            if (!string.IsNullOrEmpty(parent))
            {
                while (suffix.StartsWith(parent + "_", StringComparison.OrdinalIgnoreCase))
                {
                    suffix = suffix.Substring(parent.Length + 1).TrimStart('_').Trim();
                }

                if (string.Equals(suffix, parent, StringComparison.OrdinalIgnoreCase))
                {
                    suffix = string.Empty;
                }
            }

            return suffix.TrimStart('_').Trim();
        }

        /// <summary>
        /// Tạo tên mặt phẳng phụ luôn có tiền tố bắt buộc là '&lt;TênMặtPhẳngCóSẵn&gt;_' để quản lý tập trung ngay dưới mặt phẳng gốc trong Prospector.
        /// Ví dụ: 'Doan 1_Top' + 'Phu_1' -&gt; 'Doan 1_Top_Phu_1'; 'Doan 1_Top' + 'ViaHe1' -&gt; 'Doan 1_Top_ViaHe1'.
        /// </summary>
        public static string BuildPrefixedSubSurfaceName(string parentSurfaceName, string rawSuffixOrName, string fallbackSuffix = "Phu_1")
        {
            string parent = (parentSurfaceName ?? string.Empty).Trim();
            string cleanSuffix = ExtractCleanSubSurfaceSuffix(parent, rawSuffixOrName);
            if (string.IsNullOrWhiteSpace(cleanSuffix))
            {
                cleanSuffix = string.IsNullOrWhiteSpace(fallbackSuffix) ? "Phu_1" : fallbackSuffix.Trim().TrimStart('_');
            }

            return string.IsNullOrEmpty(parent) ? cleanSuffix : $"{parent}_{cleanSuffix}";
        }

        /// <summary>
        /// Kiểm tra xem hậu tố đang là mẫu đánh số tự động mặc định ('Phu_1', 'Phu_2'...) hay hậu tố tùy chỉnh của người dùng.
        /// </summary>
        public static bool IsDefaultAutoSubSuffix(string parentSurfaceName, string rawSuffixOrName, string baseTag = "Phu")
        {
            string clean = ExtractCleanSubSurfaceSuffix(parentSurfaceName, rawSuffixOrName);
            if (string.IsNullOrWhiteSpace(clean)) return true;

            string prefix = (string.IsNullOrWhiteSpace(baseTag) ? "Phu" : baseTag.Trim()) + "_";
            if (!clean.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;

            string numPart = clean.Substring(prefix.Length);
            return int.TryParse(numPart, out int n) && n >= 1;
        }

        /// <summary>
        /// Gợi ý hậu tố tự động tiếp theo chưa trùng ('Phu_1', 'Phu_2'...) theo tên mặt phẳng có sẵn.
        /// </summary>
        public static string SuggestNextSubSurfaceSuffix(IEnumerable<string> existingSurfaceNames, string parentSurfaceName, string baseTag = "Phu")
        {
            string tag = string.IsNullOrWhiteSpace(baseTag) ? "Phu" : baseTag.Trim();
            string parent = (parentSurfaceName ?? string.Empty).Trim();
            var existingSet = new HashSet<string>(existingSurfaceNames ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);

            int index = 1;
            while (true)
            {
                string candidateSuffix = $"{tag}_{index}";
                string fullCandidate = string.IsNullOrEmpty(parent) ? candidateSuffix : $"{parent}_{candidateSuffix}";
                if (!existingSet.Contains(fullCandidate))
                {
                    return candidateSuffix;
                }
                index++;
            }
        }
    }
}

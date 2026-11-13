using System;
using System.Collections.Generic;

using Autodesk.AutoCAD.DatabaseServices;

using CivAlignment = Autodesk.Civil.DatabaseServices.Alignment;
using CivilDocument = Autodesk.Civil.ApplicationServices.CivilDocument;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using SurfaceStyle = Autodesk.Civil.DatabaseServices.Styles.SurfaceStyle;

namespace MyFirstProject.Shared.Civil
{
    /// <summary>
    /// Helper dùng chung cho Civil 3D Surface / Alignment.
    /// Mọi hàm chỉ ĐỌC (OpenMode.ForRead) và dùng Transaction do nơi gọi truyền vào.
    /// Trước khi viết hàm tìm Surface/Alignment theo tên trong lệnh mới, hãy dùng các hàm ở đây.
    /// </summary>
    public static class SurfaceHelpers
    {
        /// <summary>Tìm Surface theo tên (không phân biệt hoa thường). Không thấy → ObjectId.Null.</summary>
        public static ObjectId FindSurfaceByName(Database db, Transaction tr, string? surfaceName)
        {
            if (string.IsNullOrWhiteSpace(surfaceName)) return ObjectId.Null;
            CivilDocument? civDoc = CivilDocument.GetCivilDocument(db);
            if (civDoc == null) return ObjectId.Null;

            string target = surfaceName.Trim();
            foreach (ObjectId id in civDoc.GetSurfaceIds())
            {
                if (id.IsNull || id.IsErased) continue;
                if (tr.GetObject(id, OpenMode.ForRead) is CivSurface s &&
                    string.Equals(s.Name, target, StringComparison.OrdinalIgnoreCase))
                {
                    return id;
                }
            }
            return ObjectId.Null;
        }

        /// <summary>Tập tên toàn bộ Surface trong bản vẽ (không phân biệt hoa thường).</summary>
        public static HashSet<string> GetSurfaceNames(Database db, Transaction tr)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CivilDocument? civDoc = CivilDocument.GetCivilDocument(db);
            if (civDoc == null) return names;

            foreach (ObjectId id in civDoc.GetSurfaceIds())
            {
                if (id.IsNull || id.IsErased) continue;
                if (tr.GetObject(id, OpenMode.ForRead) is CivSurface s && !string.IsNullOrEmpty(s.Name))
                {
                    names.Add(s.Name);
                }
            }
            return names;
        }

        /// <summary>
        /// Trả về tên Surface chưa bị trùng: giữ nguyên <paramref name="baseName"/> nếu chưa có,
        /// ngược lại thêm hậu tố <c>_1</c>, <c>_2</c>...
        /// </summary>
        public static string EnsureUniqueSurfaceName(Database db, Transaction tr, string baseName)
        {
            HashSet<string> existing = GetSurfaceNames(db, tr);
            if (!existing.Contains(baseName)) return baseName;

            int index = 1;
            string candidate;
            do
            {
                candidate = $"{baseName}_{index}";
                index++;
            }
            while (existing.Contains(candidate));

            return candidate;
        }

        /// <summary>
        /// Tìm Surface Style theo tên. Không thấy và <paramref name="fallbackToFirst"/> = true → trả style đầu tiên.
        /// </summary>
        public static ObjectId FindSurfaceStyleId(Database db, Transaction tr, string? styleName, bool fallbackToFirst = true)
        {
            CivilDocument? civDoc = CivilDocument.GetCivilDocument(db);
            if (civDoc == null) return ObjectId.Null;

            var styles = civDoc.Styles.SurfaceStyles;
            ObjectId first = ObjectId.Null;

            foreach (ObjectId id in styles)
            {
                if (id.IsNull || id.IsErased) continue;
                if (first.IsNull) first = id;
                if (string.IsNullOrWhiteSpace(styleName)) break;

                if (tr.GetObject(id, OpenMode.ForRead) is SurfaceStyle st &&
                    string.Equals(st.Name, styleName.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return id;
                }
            }
            return fallbackToFirst ? first : ObjectId.Null;
        }

        /// <summary>Tìm Alignment theo tên (không phân biệt hoa thường). Không thấy → ObjectId.Null.</summary>
        public static ObjectId FindAlignmentByName(Database db, Transaction tr, string? alignmentName)
        {
            if (string.IsNullOrWhiteSpace(alignmentName)) return ObjectId.Null;
            CivilDocument? civDoc = CivilDocument.GetCivilDocument(db);
            if (civDoc == null) return ObjectId.Null;

            string target = alignmentName.Trim();
            foreach (ObjectId id in civDoc.GetAlignmentIds())
            {
                if (id.IsNull || id.IsErased) continue;
                if (tr.GetObject(id, OpenMode.ForRead) is CivAlignment a &&
                    string.Equals(a.Name, target, StringComparison.OrdinalIgnoreCase))
                {
                    return id;
                }
            }
            return ObjectId.Null;
        }
    }
}

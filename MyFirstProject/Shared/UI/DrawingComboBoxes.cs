using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

using Autodesk.AutoCAD.DatabaseServices;

using CivAlignment = Autodesk.Civil.DatabaseServices.Alignment;
using CivilDocument = Autodesk.Civil.ApplicationServices.CivilDocument;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using WinFormsComboBox = System.Windows.Forms.ComboBox;

namespace MyFirstProject.Shared.UI
{
    /// <summary>Phần tử ComboBox mang cả tên hiển thị và ObjectId (Surface, Alignment...).</summary>
    public sealed class NamedObjectItem
    {
        public NamedObjectItem(string name, ObjectId id)
        {
            Name = name;
            Id = id;
        }

        public string Name { get; }
        public ObjectId Id { get; }

        public override string ToString() => Name;
    }

    /// <summary>
    /// Nạp danh sách đối tượng từ bản vẽ vào ComboBox (quy tắc GEMINI.md mục 1:
    /// KHÔNG bắt người dùng gõ tay tên Layer / TextStyle / DimStyle / Surface / Alignment...).
    /// Mọi hàm Get... chỉ đọc (OpenMode.ForRead) trong OpenCloseTransaction riêng, an toàn khi gọi từ Form.
    /// </summary>
    public static class DrawingComboBoxes
    {
        /// <summary>ComboBox dạng DropDownList chuẩn.</summary>
        public static WinFormsComboBox CreateDropDown(int width = 240)
        {
            return new WinFormsComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = width,
                MaxDropDownItems = 20,
                IntegralHeight = false,
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };
        }

        #region Đọc danh sách tên từ bản vẽ

        public static List<string> GetLayerNames(Database db, bool includeXrefLayers = false) =>
            ReadSymbolTable<LayerTableRecord>(db, db.LayerTableId,
                r => includeXrefLayers || !r.IsDependent);

        public static List<string> GetTextStyleNames(Database db) =>
            ReadSymbolTable<TextStyleTableRecord>(db, db.TextStyleTableId,
                r => !r.IsShapeFile && !r.IsDependent && !string.IsNullOrEmpty(r.Name));

        public static List<string> GetDimStyleNames(Database db) =>
            ReadSymbolTable<DimStyleTableRecord>(db, db.DimStyleTableId, r => !r.IsDependent);

        public static List<string> GetLinetypeNames(Database db) =>
            ReadSymbolTable<LinetypeTableRecord>(db, db.LinetypeTableId, r => !r.IsDependent);

        /// <summary>Block do người dùng định nghĩa (bỏ Layout, block ẩn danh, Xref).</summary>
        public static List<string> GetBlockNames(Database db) =>
            ReadSymbolTable<BlockTableRecord>(db, db.BlockTableId,
                r => !r.IsLayout && !r.IsAnonymous && !r.IsFromExternalReference && !r.IsDependent);

        /// <summary>Danh sách TIN/Grid Surface trong bản vẽ (sắp xếp theo tên).</summary>
        public static List<NamedObjectItem> GetSurfaces(Database db) =>
            ReadCivilObjects(db, civDoc => civDoc.GetSurfaceIds(), obj => (obj as CivSurface)?.Name);

        /// <summary>Danh sách Alignment trong bản vẽ (sắp xếp theo tên).</summary>
        public static List<NamedObjectItem> GetAlignments(Database db) =>
            ReadCivilObjects(db, civDoc => civDoc.GetAlignmentIds(), obj => (obj as CivAlignment)?.Name);

        #endregion

        #region Nạp / chọn trên ComboBox

        /// <summary>Nạp danh sách tên, chọn <paramref name="preferred"/> nếu có, ngược lại chọn mục đầu tiên.</summary>
        public static void Fill(WinFormsComboBox cbo, IEnumerable<string> names, string? preferred = null)
        {
            cbo.BeginUpdate();
            try
            {
                cbo.Items.Clear();
                foreach (string name in names)
                {
                    cbo.Items.Add(name);
                }
            }
            finally
            {
                cbo.EndUpdate();
            }
            SelectByName(cbo, preferred);
        }

        /// <summary>Nạp danh sách đối tượng có ObjectId, chọn theo tên ưu tiên nếu có.</summary>
        public static void Fill(WinFormsComboBox cbo, IEnumerable<NamedObjectItem> items, string? preferredName = null)
        {
            cbo.BeginUpdate();
            try
            {
                cbo.Items.Clear();
                foreach (NamedObjectItem item in items)
                {
                    cbo.Items.Add(item);
                }
            }
            finally
            {
                cbo.EndUpdate();
            }
            SelectByName(cbo, preferredName);
        }

        /// <summary>
        /// Chọn mục theo tên (không phân biệt hoa thường). Không tìm thấy → chọn mục đầu tiên.
        /// Trả về true nếu tìm đúng tên.
        /// </summary>
        public static bool SelectByName(WinFormsComboBox cbo, string? name)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                for (int i = 0; i < cbo.Items.Count; i++)
                {
                    if (string.Equals(cbo.Items[i]?.ToString(), name, StringComparison.OrdinalIgnoreCase))
                    {
                        cbo.SelectedIndex = i;
                        return true;
                    }
                }
            }

            if (cbo.Items.Count > 0 && cbo.SelectedIndex < 0)
            {
                cbo.SelectedIndex = 0;
            }
            return false;
        }

        /// <summary>Chọn mục theo ObjectId (dùng khi người dùng bấm nút Pick trên bản vẽ).</summary>
        public static bool SelectById(WinFormsComboBox cbo, ObjectId id)
        {
            for (int i = 0; i < cbo.Items.Count; i++)
            {
                if (cbo.Items[i] is NamedObjectItem item && item.Id == id)
                {
                    cbo.SelectedIndex = i;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Tên đang chọn (chuỗi rỗng nếu chưa chọn).</summary>
        public static string GetSelectedName(WinFormsComboBox cbo) => cbo.SelectedItem?.ToString() ?? string.Empty;

        /// <summary>ObjectId đang chọn (ObjectId.Null nếu chưa chọn hoặc ComboBox chứa chuỗi).</summary>
        public static ObjectId GetSelectedId(WinFormsComboBox cbo) =>
            cbo.SelectedItem is NamedObjectItem item ? item.Id : ObjectId.Null;

        #endregion

        #region Helpers nội bộ

        private static List<string> ReadSymbolTable<TRecord>(Database db, ObjectId tableId, Func<TRecord, bool> filter)
            where TRecord : SymbolTableRecord
        {
            var names = new List<string>();
            using (Transaction tr = db.TransactionManager.StartOpenCloseTransaction())
            {
                if (tr.GetObject(tableId, OpenMode.ForRead) is SymbolTable table)
                {
                    foreach (ObjectId id in table)
                    {
                        if (id.IsErased) continue;
                        if (tr.GetObject(id, OpenMode.ForRead) is TRecord record && filter(record))
                        {
                            names.Add(record.Name);
                        }
                    }
                }
                tr.Commit();
            }
            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }

        private static List<NamedObjectItem> ReadCivilObjects(
            Database db,
            Func<CivilDocument, ObjectIdCollection> getIds,
            Func<DBObject, string?> getName)
        {
            var items = new List<NamedObjectItem>();
            CivilDocument? civDoc = CivilDocument.GetCivilDocument(db);
            if (civDoc == null) return items;

            using (Transaction tr = db.TransactionManager.StartOpenCloseTransaction())
            {
                foreach (ObjectId id in getIds(civDoc))
                {
                    if (id.IsNull || id.IsErased) continue;
                    string? name = getName(tr.GetObject(id, OpenMode.ForRead));
                    if (!string.IsNullOrEmpty(name))
                    {
                        items.Add(new NamedObjectItem(name, id));
                    }
                }
                tr.Commit();
            }
            return items.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        #endregion
    }
}

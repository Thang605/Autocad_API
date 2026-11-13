using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

using Autodesk.AutoCAD.DatabaseServices;

namespace MyFirstProject.Shared.UI
{
    /// <summary>
    /// Tiện ích ghi nhớ thông số giữa các lần chạy lệnh (quy tắc GEMINI.md mục 2).
    /// <para>Mẫu khuyến nghị: dùng chính class <c>XxxOptions</c> làm mô hình lưu trữ:</para>
    /// <code>
    /// private static XxxOptions _last = new();
    /// private static Size _lastFormSize = Size.Empty;
    /// private void SaveCurrentSettings() { _last = ReadOptionsFromControls(); _lastFormSize = PersistentState.CaptureFormSize(this, _lastFormSize); }
    /// private void RestoreLastSettings() { PersistentState.RestoreFormSize(this, _lastFormSize); ... }
    /// </code>
    /// </summary>
    public static class PersistentState
    {
        /// <summary>ObjectId còn dùng được: không Null, hợp lệ, chưa bị xóa.</summary>
        public static bool IsAlive(ObjectId id) => !id.IsNull && id.IsValid && !id.IsErased;

        /// <summary>
        /// Như <see cref="IsAlive(ObjectId)"/> và thuộc đúng bản vẽ <paramref name="db"/>
        /// (biến static sống suốt phiên, người dùng có thể đã chuyển sang bản vẽ khác).
        /// </summary>
        public static bool IsAlive(ObjectId id, Database? db) => IsAlive(id) && (db == null || id.Database == db);

        /// <summary>Lọc danh sách ObjectId đã lưu, chỉ giữ các đối tượng còn sống (và thuộc <paramref name="db"/> nếu truyền vào).</summary>
        public static List<ObjectId> FilterAlive(IEnumerable<ObjectId>? ids, Database? db = null)
        {
            var result = new List<ObjectId>();
            if (ids == null) return result;

            var seen = new HashSet<ObjectId>();
            foreach (ObjectId id in ids)
            {
                if (IsAlive(id, db) && seen.Add(id))
                {
                    result.Add(id);
                }
            }
            return result;
        }

        /// <summary>Khôi phục kích thước Form đã lưu nếu hợp lệ (không nhỏ hơn MinimumSize, không vượt màn hình).</summary>
        public static void RestoreFormSize(Form form, Size lastSize)
        {
            if (lastSize.IsEmpty) return;

            Rectangle area = Screen.FromControl(form).WorkingArea;
            int width = Math.Min(Math.Max(lastSize.Width, form.MinimumSize.Width), area.Width);
            int height = Math.Min(Math.Max(lastSize.Height, form.MinimumSize.Height), area.Height);
            form.Size = new Size(width, height);
        }

        /// <summary>Lấy kích thước Form để lưu (bỏ qua khi đang Maximize/Minimize để không ghi đè kích thước thường).</summary>
        public static Size CaptureFormSize(Form form, Size previous) =>
            form.WindowState == FormWindowState.Normal ? form.Size : previous;

        /// <summary>Gán giá trị cho NumericUpDown, tự kẹp trong [Minimum, Maximum] để không ném ngoại lệ.</summary>
        public static void SetValueSafe(NumericUpDown nud, decimal value)
        {
            nud.Value = Math.Min(nud.Maximum, Math.Max(nud.Minimum, value));
        }
    }
}

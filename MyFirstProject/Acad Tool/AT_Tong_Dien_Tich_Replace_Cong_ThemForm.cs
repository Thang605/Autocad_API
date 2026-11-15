using Autodesk.AutoCAD.ApplicationServices;
using MyFirstProject.Shared.UI;

namespace Civil3DCsharp
{
    /// <summary>Dedicated input dialog for AT_Tong_Dien_Tich_Replace_Cong_Them.</summary>
    public sealed class AT_Tong_Dien_Tich_Replace_Cong_ThemForm : CadSelectionForm
    {
        private static readonly CadSelectionState LastSettings = new();

        /// <summary>Restores this command's independent session choices.</summary>
        public AT_Tong_Dien_Tich_Replace_Cong_ThemForm(Document document)
            : base(document, "AT_Tong_Dien_Tich_Replace_Cong_Them", "Tính tổng diện tích (Area). Ghi kết quả vào Text/MText đã chọn. Nội dung hiện có được cộng vào biểu thức. Chọn đối tượng hỗ trợ thuộc tính tương ứng.",
                "Chọn các đối tượng (Polyline/Hatch) cần tính tổng diện tích: \n", null, CadSelectionDestination.Text,
                "Chọn text/mtext cần nhập nội dung: \n", LastSettings)
        {
        }
    }
}
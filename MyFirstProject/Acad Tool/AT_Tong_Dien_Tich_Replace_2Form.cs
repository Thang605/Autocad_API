using Autodesk.AutoCAD.ApplicationServices;
using MyFirstProject.Shared.UI;

namespace Civil3DCsharp
{
    /// <summary>Dedicated input dialog for AT_Tong_Dien_Tich_Replace_2.</summary>
    public sealed class AT_Tong_Dien_Tich_Replace_2Form : CadSelectionForm
    {
        private static readonly CadSelectionState LastSettings = new();

        /// <summary>Restores this command's independent session choices.</summary>
        public AT_Tong_Dien_Tich_Replace_2Form(Document document)
            : base(document, "AT_Tong_Dien_Tich_Replace_2", "Tính tổng diện tích (Area). Ghi kết quả vào Text/MText đã chọn. Chọn đối tượng hỗ trợ thuộc tính tương ứng.",
                "Chọn các đối tượng (Polyline/Hatch) cần tính tổng diện tích: \n", null, CadSelectionDestination.Text,
                "Chọn text/mtext cần nhập nội dung: \n", LastSettings)
        {
        }
    }
}
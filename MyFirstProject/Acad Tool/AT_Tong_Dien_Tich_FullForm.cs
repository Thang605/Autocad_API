using Autodesk.AutoCAD.ApplicationServices;
using MyFirstProject.Shared.UI;

namespace Civil3DCsharp
{
    /// <summary>Dedicated input dialog for AT_Tong_Dien_Tich_Full.</summary>
    public sealed class AT_Tong_Dien_Tich_FullForm : CadSelectionForm
    {
        private static readonly CadSelectionState LastSettings = new();

        /// <summary>Restores this command's independent session choices.</summary>
        public AT_Tong_Dien_Tich_FullForm(Document document)
            : base(document, "AT_Tong_Dien_Tich_Full", "Tính tổng diện tích (Area). Tạo Text trên layer TinhTong, cao 2, kiểu Standard. Chọn đối tượng hỗ trợ thuộc tính tương ứng.",
                "Chọn các đối tượng (Polyline/Hatch) cần tính tổng diện tích: \n", null, CadSelectionDestination.Point,
                "Chọn vị trí đặt text: \n", LastSettings)
        {
        }
    }
}
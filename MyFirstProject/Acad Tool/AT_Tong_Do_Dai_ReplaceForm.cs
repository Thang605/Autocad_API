using Autodesk.AutoCAD.ApplicationServices;
using MyFirstProject.Shared.UI;

namespace Civil3DCsharp
{
    /// <summary>Dedicated input dialog for AT_Tong_Do_Dai_Replace.</summary>
    public sealed class AT_Tong_Do_Dai_ReplaceForm : CadSelectionForm
    {
        private static readonly CadSelectionState LastSettings = new();

        /// <summary>Restores this command's independent session choices.</summary>
        public AT_Tong_Do_Dai_ReplaceForm(Document document)
            : base(document, "AT_Tong_Do_Dai_Replace", "Tính tổng giá trị đo Dimension. Ghi kết quả vào Text/MText đã chọn.",
                "Chọn các đối tượng (Dimension) cần tính tổng chiều dài/giá trị đo: \n", "DIMENSION", CadSelectionDestination.Text,
                "Chọn text/mtext cần nhập nội dung: \n", LastSettings)
        {
        }
    }
}
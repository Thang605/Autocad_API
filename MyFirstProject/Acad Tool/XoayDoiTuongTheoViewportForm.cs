using System;
using Autodesk.AutoCAD.ApplicationServices;
using MyFirstProject.Shared.UI;

namespace Civil3DCsharp
{
    /// <summary>Dedicated modal inputs for XoayDoiTuongTheoViewportForm.</summary>
    internal sealed class XoayDoiTuongTheoViewportForm : CadSelectionForm
    {
        private static readonly CadSelectionState LastState = new CadSelectionState();

        /// <summary>Shows the captured viewport angle without changing it during canvas picks.</summary>
        public XoayDoiTuongTheoViewportForm(Document document, double angleViewport)
            : base(document, "Xoay đối tượng theo viewport",
                $"Đặt góc theo loại đối tượng; một số nhãn dùng phép xoay dự phòng. Góc viewport khi mở lệnh: {angleViewport * 180.0 / Math.PI:F2}°.",
                "\n Chọn các đối tượng cần xoay theo viewport:", null, CadSelectionDestination.None, "", LastState)
        {
        }
    }
}
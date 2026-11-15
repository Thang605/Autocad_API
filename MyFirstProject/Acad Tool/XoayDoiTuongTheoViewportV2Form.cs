using System;
using Autodesk.AutoCAD.ApplicationServices;
using MyFirstProject.Shared.UI;

namespace Civil3DCsharp
{
    /// <summary>Dedicated modal inputs for XoayDoiTuongTheoViewportV2Form.</summary>
    internal sealed class XoayDoiTuongTheoViewportV2Form : CadSelectionForm
    {
        private static readonly CadSelectionState LastState = new CadSelectionState();

        /// <summary>Shows the captured viewport angle without changing it during canvas picks.</summary>
        public XoayDoiTuongTheoViewportV2Form(Document document, double angleViewport)
            : base(document, "Xoay đối tượng quanh điểm theo viewport",
                $"Xoay tương đối các đối tượng quanh điểm cơ sở đã chọn. Góc viewport khi mở lệnh: {angleViewport * 180.0 / Math.PI:F2}°.",
                "\n Chọn các đối tượng cần xoay:", null, CadSelectionDestination.Point, "\n Chọn điểm cơ sở xoay:", LastState)
        {
        }
    }
}
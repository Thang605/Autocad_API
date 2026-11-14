using System.Drawing;

using Autodesk.AutoCAD.DatabaseServices;

using MyFirstProject.Shared.UI;

namespace MyFirstProject.Commands.CTC.TaoCorridorTheoCauKien
{
    public partial class TaoCorridorTheoCauKienForm
    {
        private static string _lastSelectedSiteKey = "";
        private static ObjectId _lastExistingCorridorId = ObjectId.Null;
        private static string _lastCorridorName = "COR_Tong";
        private static ObjectId _lastDefaultAssemblyId = ObjectId.Null;
        private static ObjectId _lastCodeSetStyleId = ObjectId.Null;
        private static ObjectId _lastDefaultSurfaceId = ObjectId.Null;
        private static decimal _lastDefaultFreqTangent = 10m;
        private static decimal _lastDefaultFreqCurve = 5m;
        private static bool _lastAutoRebuild = true;
        private static bool _lastRemoveDeletedBaselines = true;
        private static bool _lastDeleteMergedSourceCorridors = true;
        private static Size _lastFormSize = new Size(1280, 740);

        private void SaveCurrentSettings()
        {
            _lastSelectedSiteKey = GetSelectedSiteItem()?.Key ?? "";
            _lastExistingCorridorId = GetSelectedTargetCorridorId();
            _lastCorridorName = txtCorridorName.Text.Trim();

            _lastDefaultAssemblyId = cmbDefaultAssembly.SelectedIndex >= 0 && cmbDefaultAssembly.SelectedIndex < _assemblyList.Count
                ? _assemblyList[cmbDefaultAssembly.SelectedIndex].Id
                : ObjectId.Null;

            if (cmbCodeSetStyle.SelectedIndex >= 0 && cmbCodeSetStyle.SelectedIndex < _codeSetStyleList.Count)
                _lastCodeSetStyleId = _codeSetStyleList[cmbCodeSetStyle.SelectedIndex].Id;

            if (cmbDefaultSurface.SelectedIndex > 0 && cmbDefaultSurface.SelectedIndex - 1 < _surfaceList.Count)
                _lastDefaultSurfaceId = _surfaceList[cmbDefaultSurface.SelectedIndex - 1].Id;
            else
                _lastDefaultSurfaceId = ObjectId.Null;

            _lastDefaultFreqTangent = numFreqTangent.Value;
            _lastDefaultFreqCurve = numFreqCurve.Value;
            _lastAutoRebuild = chkAutoRebuild.Checked;
            _lastRemoveDeletedBaselines = chkRemoveDeletedBaselines.Checked;
            _lastDeleteMergedSourceCorridors = chkDeleteMergedSourceCorridors.Checked;
            _lastFormSize = PersistentState.CaptureFormSize(this, _lastFormSize);
        }

        private void RestoreLastSettings()
        {
            _suppressSelectorEvents = true;
            try
            {
                if (!string.IsNullOrWhiteSpace(_lastCorridorName))
                    txtCorridorName.Text = _lastCorridorName;

                if (PersistentState.IsAlive(_lastDefaultAssemblyId, _db))
                {
                    int asmIdx = _assemblyList.FindIndex(a => a.Id == _lastDefaultAssemblyId);
                    if (asmIdx >= 0) cmbDefaultAssembly.SelectedIndex = asmIdx;
                }

                if (PersistentState.IsAlive(_lastCodeSetStyleId, _db))
                {
                    int cssIdx = _codeSetStyleList.FindIndex(c => c.Id == _lastCodeSetStyleId);
                    if (cssIdx >= 0) cmbCodeSetStyle.SelectedIndex = cssIdx;
                }

                if (PersistentState.IsAlive(_lastDefaultSurfaceId, _db))
                {
                    int sIdx = _surfaceList.FindIndex(s => s.Id == _lastDefaultSurfaceId);
                    if (sIdx >= 0) cmbDefaultSurface.SelectedIndex = sIdx + 1;
                }

                PersistentState.SetValueSafe(numFreqTangent, _lastDefaultFreqTangent);
                PersistentState.SetValueSafe(numFreqCurve, _lastDefaultFreqCurve);
                chkAutoRebuild.Checked = _lastAutoRebuild;
                chkRemoveDeletedBaselines.Checked = _lastRemoveDeletedBaselines;
                chkDeleteMergedSourceCorridors.Checked = _lastDeleteMergedSourceCorridors;

                dgvComponents.Rows.Clear();

                // Ưu tiên 1: Khôi phục Corridor tổng ở lần chạy trước trong phiên
                if (PersistentState.IsAlive(_lastExistingCorridorId, _db))
                {
                    int cIdx = _corridorList.FindIndex(c => c.Id == _lastExistingCorridorId);
                    if (cIdx >= 0) cmbExistingCorridor.SelectedIndex = cIdx + 1;
                }

                // Ưu tiên 2: Nếu phiên mới mở .dwg lần đầu, tự nhận diện Corridor đã gắn SiteRecipe (nếu có)
                if (cmbExistingCorridor.SelectedIndex <= 0 && _corridorRecipes.Count > 0)
                {
                    foreach (var kv in _corridorRecipes)
                    {
                        if (kv.Value.HasSiteBinding)
                        {
                            int cIdx = _corridorList.FindIndex(c => c.Id == kv.Key);
                            if (cIdx >= 0)
                            {
                                cmbExistingCorridor.SelectedIndex = cIdx + 1;
                                break;
                            }
                        }
                    }
                }

                // Ưu tiên 3: Khôi phục Site nguồn của phiên trước nếu chưa chọn Corridor gắn Recipe
                if (cmbExistingCorridor.SelectedIndex <= 0 && !string.IsNullOrEmpty(_lastSelectedSiteKey))
                {
                    int sIdx = _siteList.FindIndex(s => s.Key == _lastSelectedSiteKey);
                    if (sIdx >= 0) cmbSite.SelectedIndex = sIdx + 1;
                }
            }
            finally
            {
                _suppressSelectorEvents = false;
            }

            if (cmbExistingCorridor.SelectedIndex > 0)
                OnTargetCorridorSelectionChanged();
            else if (cmbSite.SelectedIndex > 0)
                OnSiteSelectionChanged();
        }
    }
}

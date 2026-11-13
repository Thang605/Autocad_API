using System;
using System.Collections.Generic;
using System.Drawing;

using Autodesk.AutoCAD.DatabaseServices;

using MyFirstProject.Shared.UI;

namespace MyFirstProject.Commands.CTC.TaoCorridorTheoCauKien
{
    public partial class TaoCorridorTheoCauKienForm
    {
        private static CorridorComponentBuildMode _lastBuildMode = CorridorComponentBuildMode.SingleOrExistingCorridor;
        private static ObjectId _lastExistingCorridorId = ObjectId.Null;
        private static string _lastCorridorPrefix = "COR_";
        private static ObjectId _lastCodeSetStyleId = ObjectId.Null;
        private static ObjectId _lastDefaultSurfaceId = ObjectId.Null;
        private static decimal _lastDefaultFreqTangent = 10m;
        private static decimal _lastDefaultFreqCurve = 5m;
        private static bool _lastAutoRebuild = true;
        private static bool _lastDeleteMergedSourceCorridors = true;
        private static Size _lastFormSize = new Size(1240, 720);
        private static List<CorridorComponentRowItem> _savedRows = new List<CorridorComponentRowItem>();

        private void SaveCurrentSettings()
        {
            _lastBuildMode = radSingleOrExistingCorridor.Checked
                ? CorridorComponentBuildMode.SingleOrExistingCorridor
                : CorridorComponentBuildMode.SeparateCorridorPerComponent;

            if (cmbExistingCorridor.SelectedIndex > 0 && cmbExistingCorridor.SelectedIndex - 1 < _corridorList.Count)
                _lastExistingCorridorId = _corridorList[cmbExistingCorridor.SelectedIndex - 1].Id;
            else
                _lastExistingCorridorId = ObjectId.Null;

            _lastCorridorPrefix = txtCorridorPrefix.Text.Trim();

            if (cmbCodeSetStyle.SelectedIndex >= 0 && cmbCodeSetStyle.SelectedIndex < _codeSetStyleList.Count)
                _lastCodeSetStyleId = _codeSetStyleList[cmbCodeSetStyle.SelectedIndex].Id;

            if (cmbDefaultSurface.SelectedIndex > 0 && cmbDefaultSurface.SelectedIndex - 1 < _surfaceList.Count)
                _lastDefaultSurfaceId = _surfaceList[cmbDefaultSurface.SelectedIndex - 1].Id;
            else
                _lastDefaultSurfaceId = ObjectId.Null;

            _lastDefaultFreqTangent = numFreqTangent.Value;
            _lastDefaultFreqCurve = numFreqCurve.Value;
            _lastAutoRebuild = chkAutoRebuild.Checked;
            _lastDeleteMergedSourceCorridors = chkDeleteMergedSourceCorridors.Checked;
            _lastFormSize = PersistentState.CaptureFormSize(this, _lastFormSize);

            if (dgvComponents.Rows.Count > 0)
            {
                _savedRows.Clear();
                for (int i = 0; i < dgvComponents.Rows.Count; i++)
                {
                    SyncGridRowToModel(i);
                    if (dgvComponents.Rows[i].Tag is CorridorComponentRowItem item && item.BaselineEntityIds.Count > 0)
                    {
                        _savedRows.Add(item);
                    }
                }
            }
        }

        private void RestoreLastSettings()
        {
            if (PersistentState.IsAlive(_lastExistingCorridorId, _db))
            {
                int cIdx = _corridorList.FindIndex(c => c.Id == _lastExistingCorridorId);
                if (cIdx >= 0) cmbExistingCorridor.SelectedIndex = cIdx + 1;
            }

            SetBuildModeUI(_lastBuildMode);

            if (!string.IsNullOrWhiteSpace(_lastCorridorPrefix))
                txtCorridorPrefix.Text = _lastCorridorPrefix;

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
            chkDeleteMergedSourceCorridors.Checked = _lastDeleteMergedSourceCorridors;

            // Bảng danh sách cấu kiện khởi đầu trống — chỉ hiển thị khi người dùng chọn Alignment hoặc FeatureLine
            dgvComponents.Rows.Clear();
        }

        private void BtnRestoreSavedRows_Click(object? sender, EventArgs e)
        {
            dgvComponents.Rows.Clear();
            foreach (var saved in _savedRows)
            {
                var validBaselines = PersistentState.FilterAlive(saved.BaselineEntityIds, _db);
                if (validBaselines.Count == 0)
                    continue;

                saved.BaselineEntityIds = validBaselines;
                saved.OffsetElevationTargetIds = PersistentState.FilterAlive(saved.OffsetElevationTargetIds, _db);
                AddRowToGrid(saved);
            }

            UpdateComponentListVisibility();
            UpdateSummaryLabel();
        }
    }
}

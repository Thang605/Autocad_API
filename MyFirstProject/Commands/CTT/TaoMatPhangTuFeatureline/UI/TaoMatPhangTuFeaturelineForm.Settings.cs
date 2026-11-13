using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using CadEntity = Autodesk.AutoCAD.DatabaseServices.Entity;
using CivSurface = Autodesk.Civil.DatabaseServices.TinSurface;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsButton = System.Windows.Forms.Button;
using WinFormsTextBox = System.Windows.Forms.TextBox;
using WinFormsComboBox = System.Windows.Forms.ComboBox;
using FlowDirection = System.Windows.Forms.FlowDirection;
using Font = System.Drawing.Font;
using WinFormsColor = System.Drawing.Color;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace MyFirstProject.Civil_Tool_2
{
    public partial class TaoMatPhangTuFeaturelineForm
    {
        private void SaveCurrentSettings()
        {
            _lastFormSize = this.Size;
            _lastCreateNewSurface = radNewSurface.Checked;
            _lastSurfaceName = txtSurfaceName.Text.Trim();

            if (cboExistingSurfaces.SelectedItem != null)
            {
                string surfName = cboExistingSurfaces.SelectedItem.ToString() ?? "";
                _lastExistingSurfaceName = surfName;
                if (_existingSurfaceMap.TryGetValue(surfName, out ObjectId sId))
                    _lastExistingSurfaceId = sId;
            }

            _lastUseManagedSubSurface = chkCreateManagedSubSurface.Checked;
            _lastSubSurfaceSuffix = txtSubSurfaceSuffix.Text.Trim();
            if (cboSubSurfaceStyle.SelectedItem != null)
                _lastSubSurfaceStyle = cboSubSurfaceStyle.SelectedItem.ToString() ?? "_No Display";

            if (cboMasterStyles.SelectedItem != null)
                _lastMasterSurfaceStyle = cboMasterStyles.SelectedItem.ToString() ?? "";

            _lastFilterSourceIdx = Math.Max(0, cboFilterSource.SelectedIndex);
            _lastFilterValue = cboFilterValue.SelectedItem?.ToString() ?? "";

            if (radBoundaryAuto.Checked) _lastBoundaryMode = FeatureLineBoundaryMode.AutoSmart;
            else if (radBoundaryCustom.Checked) _lastBoundaryMode = FeatureLineBoundaryMode.CustomBoundaryEntities;
            else _lastBoundaryMode = FeatureLineBoundaryMode.BreaklinesOnly;

            _lastMidOrdDistance = (double)numMidOrd.Value;
            _lastUseSupplementing = chkUseSupplementing.Checked;
            _lastSupplementingDistance = (double)numSupplementing.Value;
            _lastLimitMaxEdge = chkLimitMaxEdge.Checked;
            _lastMaxEdgeLength = (double)numMaxEdgeLength.Value;
            _lastAutoClose = chkAutoClose.Checked;
            _lastTreatNestedAsHole = chkNestedClosedAsHole.Checked;
            _lastRebuildAuto = chkRebuildAuto.Checked;
            _lastOverwriteOverlap = chkOverwriteOverlap.Checked;
            _lastSafeCorridorCombine = chkSafeCorridorCombine.Checked;

            _lastSelectedIds = _items.Select(x => x.Id).Where(IsIdAlive).ToList();
            _lastCustomOuterIds = _customOuterIds.Where(IsIdAlive).ToList();
            _lastCustomHideIds = _customHideIds.Where(IsIdAlive).ToList();
        }

        private void RestoreLastSettings()
        {
            if (string.IsNullOrWhiteSpace(_lastSurfaceName))
                GenerateDefaultSurfaceName();
            else
                txtSurfaceName.Text = _lastSurfaceName;

            if (!_lastCreateNewSurface && cboExistingSurfaces.Items.Count > 0)
                radExistingSurface.Checked = true;
            else
                radNewSurface.Checked = true;

            if (!string.IsNullOrEmpty(_lastExistingSurfaceName) && cboExistingSurfaces.Items.Contains(_lastExistingSurfaceName))
                cboExistingSurfaces.SelectedItem = _lastExistingSurfaceName;

            chkCreateManagedSubSurface.Checked = _lastUseManagedSubSurface;
            SelectPreferredSubSurfaceStyle(_lastSubSurfaceStyle);

            if (!string.IsNullOrEmpty(_lastMasterSurfaceStyle) && cboMasterStyles.Items.Contains(_lastMasterSurfaceStyle))
                cboMasterStyles.SelectedItem = _lastMasterSurfaceStyle;

            if (_lastFilterSourceIdx >= 0 && _lastFilterSourceIdx < cboFilterSource.Items.Count)
                cboFilterSource.SelectedIndex = _lastFilterSourceIdx;
            else
                cboFilterSource.SelectedIndex = 0;

            PopulateFilterValuesDropdown();
            if (!string.IsNullOrEmpty(_lastFilterValue) && cboFilterValue.Items.Contains(_lastFilterValue))
                cboFilterValue.SelectedItem = _lastFilterValue;

            radBoundaryAuto.Checked = _lastBoundaryMode == FeatureLineBoundaryMode.AutoSmart;
            radBoundaryCustom.Checked = _lastBoundaryMode == FeatureLineBoundaryMode.CustomBoundaryEntities;
            radBoundaryNone.Checked = _lastBoundaryMode == FeatureLineBoundaryMode.BreaklinesOnly;

            numMidOrd.Value = ClampDecimal((decimal)_lastMidOrdDistance, numMidOrd.Minimum, numMidOrd.Maximum);
            chkUseSupplementing.Checked = _lastUseSupplementing;
            numSupplementing.Value = ClampDecimal((decimal)_lastSupplementingDistance, numSupplementing.Minimum, numSupplementing.Maximum);
            chkLimitMaxEdge.Checked = _lastLimitMaxEdge;
            numMaxEdgeLength.Value = ClampDecimal((decimal)_lastMaxEdgeLength, numMaxEdgeLength.Minimum, numMaxEdgeLength.Maximum);

            chkAutoClose.Checked = _lastAutoClose;
            chkNestedClosedAsHole.Checked = _lastTreatNestedAsHole;
            chkRebuildAuto.Checked = _lastRebuildAuto;
            chkOverwriteOverlap.Checked = _lastOverwriteOverlap;
            chkSafeCorridorCombine.Checked = _lastSafeCorridorCombine;

            // Khôi phục hậu tố mặt phẳng phụ: nếu hậu tố tự động cũ đã tồn tại thì tự tăng sang số mới (Phu_2, Phu_3...)
            string effectiveParent = GetEffectiveParentSurfaceName();
            if (string.IsNullOrWhiteSpace(_lastSubSurfaceSuffix) ||
                CTT_Tao_Mat_Phang_Tu_Featureline_Command.IsDefaultAutoSubSuffix(effectiveParent, _lastSubSurfaceSuffix))
            {
                _userCustomizedSubSuffix = false;
                AssignNextAvailableSubSuffix();
            }
            else
            {
                _suppressSubSuffixTracking = true;
                try
                {
                    txtSubSurfaceSuffix.Text = CTT_Tao_Mat_Phang_Tu_Featureline_Command.ExtractCleanSubSurfaceSuffix(effectiveParent, _lastSubSurfaceSuffix);
                }
                finally
                {
                    _suppressSubSuffixTracking = false;
                }
                _userCustomizedSubSuffix = true;
                UpdateSubSurfaceNamePreview();
            }

            _customOuterIds.Clear();
            _customOuterIds.AddRange(_lastCustomOuterIds.Where(IsIdAlive));
            _customHideIds.Clear();
            _customHideIds.AddRange(_lastCustomHideIds.Where(IsIdAlive));
            UpdateCustomBoundaryLabel();
        }

        private static decimal ClampDecimal(decimal val, decimal min, decimal max)
        {
            if (val < min) return min;
            if (val > max) return max;
            return val;
        }

        private static bool IsIdAlive(ObjectId id) => !id.IsNull && id.IsValid && !id.IsErased;

        private void GenerateDefaultSurfaceName()
        {
            txtSurfaceName.Text = $"MatPhang_FL_{DateTime.Now:yyyyMMdd_HHmm}";
        }

        private void TryRestorePreviousSelection()
        {
            UpdateRestoreLastButtonState();

            if (_lastSelectedIds != null && _lastSelectedIds.Count > 0)
            {
                List<ObjectId> validPending = _lastSelectedIds.Where(IsIdAlive).ToList();
                if (validPending.Count > 0)
                {
                    PopulateItems(validPending);
                    lblStatus.Text = $"Đã khôi phục {validPending.Count} đối tượng đang chọn dở từ lần mở trước.";
                    return;
                }
            }

            int lastExecCount = _lastExecutedIds != null ? _lastExecutedIds.Count(IsIdAlive) : 0;
            if (lastExecCount > 0)
            {
                string targetLabel = string.IsNullOrWhiteSpace(_lastExecutedTargetDesc) ? "lần trước" : $"'{_lastExecutedTargetDesc}'";
                lblStatus.Text = $"Danh sách đã làm trống để chọn vùng mới (tránh trùng lặp). Nhấn '↺ Nạp lại cũ' nếu cần gọi lại {lastExecCount} đường của {targetLabel}.";
            }
        }

        private void UpdateRestoreLastButtonState()
        {
            int validExecCount = _lastExecutedIds != null ? _lastExecutedIds.Count(IsIdAlive) : 0;
            btnRestoreLast.Enabled = validExecCount > 0;

            if (validExecCount > 0)
            {
                string targetLabel = string.IsNullOrWhiteSpace(_lastExecutedTargetDesc) ? "lần chạy trước" : $"'{_lastExecutedTargetDesc}'";
                _toolTip.SetToolTip(
                    btnRestoreLast,
                    $"Nạp lại {validExecCount} đối tượng vừa dùng tạo {targetLabel} (dùng khi muốn chỉnh lại thông số của vùng cũ)");
            }
            else
            {
                _toolTip.SetToolTip(btnRestoreLast, "Chưa có bộ đối tượng nào từ lần thực thi thành công trước đó trong phiên này");
            }
        }

        private void RestoreLastExecutedItems()
        {
            List<ObjectId> validExec = _lastExecutedIds != null
                ? _lastExecutedIds.Where(IsIdAlive).ToList()
                : new List<ObjectId>();

            if (validExec.Count == 0)
            {
                UpdateRestoreLastButtonState();
                return;
            }

            PopulateItems(validExec);

            if (_customOuterIds.Count == 0 && _lastExecutedOuterIds.Count > 0)
            {
                _customOuterIds.AddRange(_lastExecutedOuterIds.Where(IsIdAlive));
                _customHideIds.Clear();
                _customHideIds.AddRange(_lastExecutedHideIds.Where(IsIdAlive));
                UpdateCustomBoundaryLabel();
            }

            string targetLabel = string.IsNullOrWhiteSpace(_lastExecutedTargetDesc) ? "lần chạy trước" : $"'{_lastExecutedTargetDesc}'";
            lblStatus.Text = $"↺ Đã nạp lại {_items.Count} đối tượng từ {targetLabel}.";
        }
    }
}

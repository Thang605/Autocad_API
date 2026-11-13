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
        private WinFormsButton CreateActionButton(string text, int width, int height, WinFormsColor backColor, WinFormsColor foreColor, bool bold)
        {
            var btn = new WinFormsButton
            {
                Text = text,
                Width = width,
                Height = height,
                BackColor = backColor,
                ForeColor = foreColor,
                FlatStyle = FlatStyle.Flat,
                Font = CreateFont(8.75F, bold ? FontStyle.Bold : FontStyle.Regular),
                UseMnemonic = false,
                Margin = new Padding(0, 0, 6, 4)
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private void UpdateControlStates()
        {
            bool createNew = radNewSurface.Checked;
            radNewSurface.Font = createNew ? _fontBold9 : _fontRegular9;
            radExistingSurface.Font = !createNew ? _fontBold9 : _fontRegular9;

            txtSurfaceName.Enabled = createNew;
            btnGenName.Enabled = createNew;
            cboMasterStyles.Enabled = createNew;

            cboExistingSurfaces.Enabled = !createNew;
            btnPickExistingSurface.Enabled = !createNew;
            pnlExistingOptions.Enabled = !createNew;
            pnlSubSurfaceConfig.Enabled = !createNew && chkCreateManagedSubSurface.Checked;

            if (createNew)
            {
                btnExecute.Text = "⚡ Thực Hiện Tạo Mặt Phẳng";
                btnExecute.BackColor = WinFormsColor.FromArgb(13, 110, 253);
            }
            else
            {
                btnExecute.Text = "➕ Bổ Sung Vào Mặt Phẳng Có Sẵn";
                btnExecute.BackColor = WinFormsColor.FromArgb(25, 135, 84);
            }

            UpdateExistingSurfaceMetadataUI();
            UpdateSubSurfaceNamePreview();

            chkNestedClosedAsHole.Enabled = radBoundaryAuto.Checked;
            pnlCustomBoundary.Enabled = radBoundaryCustom.Checked;

            numSupplementing.Enabled = chkUseSupplementing.Checked;
            numMaxEdgeLength.Enabled = chkLimitMaxEdge.Checked;
            btnSuggestMaxEdge.Enabled = chkLimitMaxEdge.Checked;
        }

        private void OnSelectedExistingSurfaceChanged()
        {
            UpdateExistingSurfaceMetadataUI();

            if (!_userCustomizedSubSuffix)
            {
                AssignNextAvailableSubSuffix();
            }
            else
            {
                UpdateSubSurfaceNamePreview();
            }
        }

        private string GetEffectiveParentSurfaceName()
        {
            string selectedSurf = cboExistingSurfaces.SelectedItem?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(selectedSurf)) return "";

            if (chkSafeCorridorCombine.Visible && chkSafeCorridorCombine.Checked)
            {
                return $"{selectedSurf}_Gop";
            }

            return selectedSurf;
        }

        private void AssignNextAvailableSubSuffix()
        {
            string parentName = GetEffectiveParentSurfaceName();
            string nextSuffix = CTT_Tao_Mat_Phang_Tu_Featureline_Command.SuggestNextSubSurfaceSuffix(
                _existingSurfaceMap.Keys,
                parentName,
                "Phu");

            _suppressSubSuffixTracking = true;
            try
            {
                txtSubSurfaceSuffix.Text = nextSuffix;
            }
            finally
            {
                _suppressSubSuffixTracking = false;
            }

            UpdateSubSurfaceNamePreview();
        }

        private void UpdateSubSurfaceNamePreview()
        {
            string parentName = GetEffectiveParentSurfaceName();
            string prefixDisplay = string.IsNullOrWhiteSpace(parentName) ? "<TênGốc>_" : $"{parentName}_";
            lblSubPrefix.Text = prefixDisplay;
            _toolTip.SetToolTip(lblSubPrefix, $"Tiền tố bắt buộc theo mặt phẳng có sẵn: {prefixDisplay}");

            if (!chkCreateManagedSubSurface.Checked)
            {
                lblSubFullNamePreview.Text = "ℹ️ Chế độ gộp trực tiếp (0 mặt phẳng phụ — nạp thẳng vào mặt phẳng đích)";
                lblSubFullNamePreview.ForeColor = WinFormsColor.FromArgb(85, 98, 115);
                return;
            }

            if (string.IsNullOrWhiteSpace(parentName))
            {
                lblSubFullNamePreview.Text = "📌 Tên mặt phẳng phụ: (Chưa chọn mặt phẳng có sẵn)";
                lblSubFullNamePreview.ForeColor = WinFormsColor.FromArgb(85, 98, 115);
                return;
            }

            string fullSubName = CTT_Tao_Mat_Phang_Tu_Featureline_Command.BuildPrefixedSubSurfaceName(
                parentName,
                txtSubSurfaceSuffix.Text);

            if (_existingSurfaceMap.TryGetValue(fullSubName, out ObjectId dupId) && IsIdAlive(dupId))
            {
                lblSubFullNamePreview.Text = $"⚠️ Tên mặt phẳng phụ: '{fullSubName}' (đã có — sẽ hỏi Cập nhật / Đánh số mới)";
                lblSubFullNamePreview.ForeColor = WinFormsColor.FromArgb(155, 80, 0);
            }
            else
            {
                lblSubFullNamePreview.Text = $"📌 Tên mặt phẳng phụ: '{fullSubName}' (xếp ngay dưới '{parentName}')";
                lblSubFullNamePreview.ForeColor = WinFormsColor.FromArgb(18, 92, 52);
            }
        }

        private void UpdateExistingSurfaceMetadataUI()
        {
            string selectedSurf = cboExistingSurfaces.SelectedItem?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(selectedSurf) || !_existingSurfaceMap.ContainsKey(selectedSurf))
            {
                lblExistingSurfInfo.Text = "📊 Mặt phẳng đích: (Chưa có mặt phẳng TIN trong bản vẽ)";
                chkSafeCorridorCombine.Visible = false;
                return;
            }

            int triCount = _surfaceTriCountMap.TryGetValue(selectedSurf, out int tc) ? tc : 0;
            SurfaceOriginationType origType = _surfaceOriginationMap.TryGetValue(selectedSurf, out var ot)
                ? ot
                : SurfaceOriginationType.None;

            bool isSpecialOrig = origType != SurfaceOriginationType.None;
            chkSafeCorridorCombine.Visible = isSpecialOrig;

            if (isSpecialOrig)
            {
                string origLabel = origType == SurfaceOriginationType.FromCorridor ? "Corridor Surface" : "Grading Surface";
                lblExistingSurfInfo.Text = $"⚠️ '{selectedSurf}' ({origLabel}, {triCount:N0} tam giác hiện có)";
                lblExistingSurfInfo.ForeColor = WinFormsColor.FromArgb(150, 75, 0);
                chkSafeCorridorCombine.Text = $"🛡️ Tạo mặt gộp '{selectedSurf}_Gop' thay vì Paste trực tiếp vào Corridor";
            }
            else
            {
                lblExistingSurfInfo.Text = $"📊 Đang chọn: '{selectedSurf}' — Mặt phẳng TIN ({triCount:N0} tam giác hiện có)";
                lblExistingSurfInfo.ForeColor = WinFormsColor.FromArgb(24, 95, 60);
            }
        }

        private static bool LooksLikeAuxiliarySubSurface(string surfName, string styleName)
        {
            if (string.IsNullOrWhiteSpace(surfName)) return false;

            int idxSub = surfName.LastIndexOf("_Sub_", StringComparison.OrdinalIgnoreCase);
            if (idxSub > 0 && int.TryParse(surfName.Substring(idxSub + 5), out _))
                return true;

            int idxPhu = surfName.LastIndexOf("_Phu_", StringComparison.OrdinalIgnoreCase);
            if (idxPhu > 0 && int.TryParse(surfName.Substring(idxPhu + 5), out _))
                return true;

            if (styleName.IndexOf("No Display", StringComparison.OrdinalIgnoreCase) >= 0 &&
                surfName.Contains('_'))
            {
                return true;
            }

            return false;
        }
    }
}

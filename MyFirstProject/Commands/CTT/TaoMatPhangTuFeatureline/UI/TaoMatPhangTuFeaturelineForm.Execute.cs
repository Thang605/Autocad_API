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
        private void BtnSyncDynamic_Click(object? sender, EventArgs e)
        {
            this.UseWaitCursor = true;
            progressBar.Visible = true;
            progressBar.Style = ProgressBarStyle.Marquee;
            btnSyncDynamic.Enabled = false;
            btnExecute.Enabled = false;
            lblStatus.Text = "Đang quét & đồng bộ lại toàn bộ ranh giới mặt phẳng Feature Line và mặt phẳng tổng...";
            System.Windows.Forms.Application.DoEvents();

            try
            {
                string prevExistingSurf = cboExistingSurfaces.SelectedItem?.ToString() ?? "";
                string prevMasterStyle = cboMasterStyles.SelectedItem?.ToString() ?? "";
                string prevSubStyle = cboSubSurfaceStyle.SelectedItem?.ToString() ?? "_No Display";

                FeatureLineDynamicSyncManager.EnsureStarted();
                FeatureLineDynamicSyncManager.SyncAllInDocument(
                    _db,
                    _ed,
                    forceAll: true,
                    out int updatedSubCount,
                    out int rebuiltParentCount);

                ScanDrawingSourcesAndStyles();
                if (!string.IsNullOrEmpty(prevExistingSurf) && cboExistingSurfaces.Items.Contains(prevExistingSurf))
                    cboExistingSurfaces.SelectedItem = prevExistingSurf;
                if (!string.IsNullOrEmpty(prevMasterStyle) && cboMasterStyles.Items.Contains(prevMasterStyle))
                    cboMasterStyles.SelectedItem = prevMasterStyle;
                SelectPreferredSubSurfaceStyle(prevSubStyle);
                UpdateControlStates();

                lblStatus.Text = $"🔄 Đã đồng bộ {updatedSubCount} mặt phẳng từ Feature Line & {rebuiltParentCount} mặt phẳng tổng.";
                MessageBox.Show(
                    $"Hoàn tất đồng bộ động theo hình học & ranh giới Feature Line mới nhất:\n\n" +
                    $"• Mặt phẳng phụ / FL đã cập nhật : {updatedSubCount}\n" +
                    $"• Mặt phẳng tổng đã Rebuild lại : {rebuiltParentCount}",
                    "Đồng Bộ Biên Động Thành Công",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (System.Exception ex)
            {
                lblStatus.Text = $"❌ Lỗi đồng bộ biên động: {ex.Message}";
                MessageBox.Show($"Không thể đồng bộ:\n{ex.Message}", "Lỗi đồng bộ biên động", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.UseWaitCursor = false;
                progressBar.Visible = false;
                btnSyncDynamic.Enabled = true;
                btnExecute.Enabled = true;
            }
        }

        private void BtnExecute_Click(object? sender, EventArgs e)
        {
            if (_items.Count == 0)
            {
                MessageBox.Show(
                    "Vui lòng chọn ít nhất một đường Feature Line / Corridor FL / Polyline nguồn!",
                    "Chưa chọn đối tượng nguồn",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            bool createNew = radNewSurface.Checked;
            string surfaceName = txtSurfaceName.Text.Trim();
            ObjectId existingSurfaceId = ObjectId.Null;
            bool overwriteSameName = false;
            bool useManagedSub = !createNew && chkCreateManagedSubSurface.Checked;
            bool overwriteExistingSub = false;
            string cleanSubSuffix = txtSubSurfaceSuffix.Text.Trim();

            if (createNew)
            {
                if (string.IsNullOrWhiteSpace(surfaceName))
                {
                    MessageBox.Show("Vui lòng nhập tên cho Mặt phẳng TIN mới!", "Thiếu tên Mặt phẳng", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtSurfaceName.Focus();
                    return;
                }

                // Kiểm tra trùng tên mặt phẳng hiện có
                if (_existingSurfaceMap.TryGetValue(surfaceName, out ObjectId dupSurfId) && IsIdAlive(dupSurfId))
                {
                    DialogResult dupChoice = MessageBox.Show(
                        $"Mặt phẳng tên '{surfaceName}' đã tồn tại trong bản vẽ!\n\n" +
                        $"• Nhấn [Yes]  : Ghi đè (Xóa mặt phẳng '{surfaceName}' cũ và tạo mới lại)\n" +
                        $"• Nhấn [No]   : Tự động đánh số tên mới ('{surfaceName}_1')\n" +
                        $"• Nhấn [Cancel]: Hủy để đổi tên khác",
                        "Trùng tên Mặt phẳng TIN",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Question);

                    if (dupChoice == DialogResult.Cancel) return;
                    overwriteSameName = dupChoice == DialogResult.Yes;
                }
            }
            else
            {
                string selectedSurf = cboExistingSurfaces.SelectedItem?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(selectedSurf) || !_existingSurfaceMap.TryGetValue(selectedSurf, out existingSurfaceId) || !IsIdAlive(existingSurfaceId))
                {
                    MessageBox.Show("Vui lòng chọn một mặt phẳng TIN hiện có hợp lệ!", "Chưa chọn mặt phẳng đích", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                surfaceName = selectedSurf;

                if (useManagedSub)
                {
                    string effectiveParent = (chkSafeCorridorCombine.Visible && chkSafeCorridorCombine.Checked)
                        ? $"{surfaceName}_Gop"
                        : surfaceName;

                    string desiredSubName = CTT_Tao_Mat_Phang_Tu_Featureline_Command.BuildPrefixedSubSurfaceName(
                        effectiveParent,
                        cleanSubSuffix);

                    if (_existingSurfaceMap.TryGetValue(desiredSubName, out ObjectId existingSubId) &&
                        IsIdAlive(existingSubId) &&
                        existingSubId != existingSurfaceId)
                    {
                        DialogResult subDupChoice = MessageBox.Show(
                            $"Mặt phẳng phụ '{desiredSubName}' đã tồn tại trong bản vẽ!\n\n" +
                            $"• Nhấn [Yes]  : Cập nhật / Ghi đè mặt phẳng phụ '{desiredSubName}' hiện có (giữ nguyên liên kết Paste vào '{effectiveParent}')\n" +
                            $"• Nhấn [No]   : Tự động đánh số tên mặt phẳng phụ mới\n" +
                            $"• Nhấn [Cancel]: Hủy để nhập hậu tố khác",
                            "Trùng tên Mặt phẳng phụ",
                            MessageBoxButtons.YesNoCancel,
                            MessageBoxIcon.Question);

                        if (subDupChoice == DialogResult.Cancel) return;
                        overwriteExistingSub = subDupChoice == DialogResult.Yes;
                    }
                }
            }

            FeatureLineBoundaryMode bndMode = FeatureLineBoundaryMode.AutoSmart;
            if (radBoundaryCustom.Checked)
            {
                bndMode = FeatureLineBoundaryMode.CustomBoundaryEntities;
                if (_customOuterIds.Count == 0)
                {
                    MessageBox.Show(
                        "Bạn đang chọn chế độ 'Giới hạn theo đường Biên bao chọn riêng trên CAD' nhưng chưa chọn đường Biên ngoài (Outer) nào!",
                        "Thiếu đường Biên ngoài",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }
            }
            else if (radBoundaryNone.Checked)
            {
                bndMode = FeatureLineBoundaryMode.BreaklinesOnly;
            }

            var options = new TaoMatPhangOptions
            {
                CreateNewSurface = createNew,
                SurfaceName = surfaceName,
                ExistingSurfaceId = existingSurfaceId,
                OverwriteExistingIfNameMatches = overwriteSameName,
                SurfaceStyleName = cboMasterStyles.SelectedItem?.ToString() ?? "",
                UseManagedSubSurfaceForExisting = useManagedSub,
                SubSurfaceSuffixOrName = cleanSubSuffix,
                SubSurfaceStyleName = cboSubSurfaceStyle.SelectedItem?.ToString() ?? "_No Display",
                OverwriteExistingSubSurface = overwriteExistingSub,
                SourceEntityIds = _items.Select(x => x.Id).ToList(),
                BoundaryMode = bndMode,
                CustomOuterBoundaryIds = _customOuterIds.Where(IsIdAlive).ToList(),
                CustomHideBoundaryIds = _customHideIds.Where(IsIdAlive).ToList(),
                MidOrdinateDistance = (double)numMidOrd.Value,
                UseSupplementing = chkUseSupplementing.Checked,
                SupplementingDistance = (double)numSupplementing.Value,
                LimitMaxEdgeLength = chkLimitMaxEdge.Checked,
                MaxEdgeLength = (double)numMaxEdgeLength.Value,
                AutoCloseNearClosed = chkAutoClose.Checked,
                AutoCloseTolerance = 0.10,
                TreatNestedClosedAsHole = chkNestedClosedAsHole.Checked,
                AutoRebuild = chkRebuildAuto.Checked,
                OverwriteOverlapOnExisting = chkOverwriteOverlap.Checked,
                CreateCombinedForCorridorSurface = chkSafeCorridorCombine.Visible && chkSafeCorridorCombine.Checked,
                CombinedSurfaceName = $"{surfaceName}_Gop"
            };

            this.UseWaitCursor = true;
            progressBar.Visible = true;
            progressBar.Style = ProgressBarStyle.Marquee;
            btnExecute.Enabled = false;
            lblStatus.Text = createNew
                ? "Đang xây dựng lưới tam giác TIN mới và cắt lọc biên trực tiếp (0 mặt phẳng phụ)..."
                : (useManagedSub
                    ? $"Đang tạo mặt phẳng phụ có tiền tố '{surfaceName}_' và Paste động vào '{surfaceName}'..."
                    : $"Đang gộp trực tiếp mảng Feature Line vào mặt phẳng '{surfaceName}'...");
            System.Windows.Forms.Application.DoEvents();

            try
            {
                SurfaceCreationResult result = CTT_Tao_Mat_Phang_Tu_Featureline_Command.ThucHienTaoMatPhang(_db, _ed, options);

                if (result.Success)
                {
                    lblStatus.Text = $"✅ {result.Message}";
                    string triInfoLine = result.IsSupplementedIntoExisting
                        ? $"• Lưới mặt gốc  : {result.PreviousTriangleCount:N0} → {result.FinalTriangleCount:N0} tam giác\n"
                        : $"• Tam giác hợp lệ: {result.FinalTriangleCount:N0} (đã cắt {result.TrimmedTriangleCount:N0} tam giác thừa)\n";

                    string subInfoLine = (result.SubSurfaceCount > 0 && !string.IsNullOrWhiteSpace(result.CreatedSubSurfaceName))
                        ? $"• Mặt phẳng phụ : '{result.CreatedSubSurfaceName}' ({result.SubSurfaceTriangleCount:N0} tam giác — đã Paste động)"
                        : "• Mặt phẳng phụ : 0 (Bản vẽ sạch 100%)";

                    MessageBox.Show(
                        $"{result.Message}\n\n" +
                        $"• Mặt phẳng gốc : '{result.FinalSurfaceName}'\n" +
                        $"• Đường đã xử lý: {result.ProcessedCurveCount} ({result.OpenCurveCount} Hở, {result.ClosedCurveCount} Kín)\n" +
                        triInfoLine +
                        subInfoLine,
                        result.IsSupplementedIntoExisting ? "Bổ Sung Mặt Phẳng Thành Công" : "Tạo Mặt Phẳng Thành Công",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);

                    // Ghi nhớ riêng bộ đối tượng vừa thực thi vào nút '↺ Nạp lại cũ', đồng thời làm trống danh sách chờ cho lần chạy vùng tiếp theo
                    _lastExecutedIds = _items.Select(x => x.Id).Where(IsIdAlive).ToList();
                    _lastExecutedOuterIds = _customOuterIds.Where(IsIdAlive).ToList();
                    _lastExecutedHideIds = _customHideIds.Where(IsIdAlive).ToList();
                    _lastExecutedTargetDesc = !string.IsNullOrWhiteSpace(result.CreatedSubSurfaceName)
                        ? result.CreatedSubSurfaceName
                        : result.FinalSurfaceName;

                    SaveCurrentSettings();
                    _completedSuccessfully = true;
                    _lastSelectedIds.Clear();
                    _lastCustomOuterIds.Clear();
                    _lastCustomHideIds.Clear();

                    if (!string.IsNullOrWhiteSpace(result.FinalSurfaceName) && IsIdAlive(result.MasterSurfaceId))
                    {
                        _lastExistingSurfaceName = result.FinalSurfaceName;
                        _lastExistingSurfaceId = result.MasterSurfaceId;
                    }
                    // Nếu đang dùng hậu tố tự động (Phu_1, Phu_2...), đặt lại để lần mở sau tự động gợi ý số tiếp theo
                    if (useManagedSub && CTT_Tao_Mat_Phang_Tu_Featureline_Command.IsDefaultAutoSubSuffix(result.FinalSurfaceName, cleanSubSuffix))
                    {
                        _lastSubSurfaceSuffix = "";
                    }
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else
                {
                    lblStatus.Text = $"❌ Lỗi: {result.Message}";
                    MessageBox.Show($"Không thể tạo mặt phẳng:\n{result.Message}", "Lỗi thực thi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (System.Exception ex)
            {
                lblStatus.Text = $"❌ Lỗi ngoại lệ: {ex.Message}";
                MessageBox.Show($"Xảy ra lỗi ngoại lệ:\n{ex.Message}", "Lỗi ngoại lệ", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.UseWaitCursor = false;
                progressBar.Visible = false;
                btnExecute.Enabled = true;
            }
        }
    }
}

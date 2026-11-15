using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Civil3DCsharp;
using MyFirstProject.Extensions;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using CivSurface = Autodesk.Civil.DatabaseServices.TinSurface;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using WinFormsColor = System.Drawing.Color;
using WinFormsFont = System.Drawing.Font;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsPoint = System.Drawing.Point;

namespace MyFirstProject.Civil_Tool
{
    public partial class KiemTraTamNhinForm : Form
    {
        private int GetSelectedSpeed()
        {
            if (int.TryParse(cbbSpeed.Text.Trim(), out int spd) && spd > 0)
                return spd;
            if (cbbSpeed.SelectedItem != null && int.TryParse(cbbSpeed.SelectedItem.ToString(), out int itemSpd) && itemSpd > 0)
                return itemSpd;
            return 60;
        }

        private void CbbSpeed_TextChanged(object? sender, EventArgs e)
        {
            if (_isLoading || _isUpdatingSpeed) return;
            string text = cbbSpeed.Text.Trim();
            if (int.TryParse(text, out int spd) && spd >= 10 && spd <= 200)
            {
                ApplyStandardLookup(updateControls: true);
            }
        }

        private void CbbStandard_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_isLoading) return;
            if (cbbStandard.SelectedItem is not SightStandardDefinition std) return;

            _isUpdatingSpeed = true;
            int curSpeed = GetSelectedSpeed();
            cbbSpeed.Items.Clear();
            foreach (var entry in std.Entries)
            {
                cbbSpeed.Items.Add(entry.Speed);
            }

            int idx = cbbSpeed.Items.IndexOf(curSpeed);
            if (idx >= 0)
            {
                cbbSpeed.SelectedIndex = idx;
            }
            else
            {
                // Chọn cấp tốc độ gần nhất trong tiêu chuẩn
                var closest = std.Entries.OrderBy(x => Math.Abs(x.Speed - curSpeed)).FirstOrDefault();
                if (closest != null)
                {
                    int cIdx = cbbSpeed.Items.IndexOf(closest.Speed);
                    if (cIdx >= 0) cbbSpeed.SelectedIndex = cIdx;
                    else cbbSpeed.Text = closest.Speed.ToString();
                }
                else
                {
                    cbbSpeed.Text = curSpeed.ToString();
                }
            }
            _isUpdatingSpeed = false;

            ApplyStandardLookup(updateControls: true);
        }

        private void CbbSpeed_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_isLoading || _isUpdatingSpeed) return;
            ApplyStandardLookup(updateControls: true);
        }

        private void BtnLookupTable_Click(object? sender, EventArgs e)
        {
            string curStd = cbbStandard.Text;
            int curSpeed = GetSelectedSpeed();

            using (var dlg = new BangTraTieuChuanTamNhinForm(curStd, curSpeed))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && dlg.SelectedStandard != null && dlg.SelectedEntry != null)
                {
                    // Chọn lại tiêu chuẩn
                    for (int i = 0; i < cbbStandard.Items.Count; i++)
                    {
                        if (cbbStandard.Items[i] is SightStandardDefinition s && s.Name == dlg.SelectedStandard.Name)
                        {
                            cbbStandard.SelectedIndex = i;
                            break;
                        }
                    }

                    // Chọn lại vận tốc
                    _isUpdatingSpeed = true;
                    int newSpeed = dlg.SelectedEntry.Speed;
                    int spdIdx = cbbSpeed.Items.IndexOf(newSpeed);
                    if (spdIdx >= 0) cbbSpeed.SelectedIndex = spdIdx;
                    else cbbSpeed.Text = newSpeed.ToString();
                    _isUpdatingSpeed = false;

                    ApplyStandardLookup(updateControls: true);
                }
            }
        }

        private void BtnResetStandard_Click(object? sender, EventArgs e)
        {
            ApplyStandardLookup(updateControls: true);
            MessageBox.Show("Đã nạp lại toàn bộ thông số chuẩn (Tầm nhìn yêu cầu S, Mắt h1, Vật cản h2, Offset làn, Bước kiểm tra) theo Tiêu chuẩn & Vận tốc đang chọn!", "Tra Lại Chuẩn", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ApplyStandardSightTypeChanged()
        {
            if (_isLoading) return;
            if (cbbStandard.SelectedItem is not SightStandardDefinition std) return;
            int speed = GetSelectedSpeed();
            var entry = std.GetEntry(speed);
            if (entry == null) return;

            bool isStopping = rbStoppingSight.Checked;
            double stdSight = isStopping ? entry.StoppingSightDist : (entry.PassingSightDist > 0 ? entry.PassingSightDist : entry.StoppingSightDist);
            double stdTargetH = isStopping ? std.TargetHeightStopping : std.TargetHeightPassing;

            if (numReqSight != null)
            {
                decimal sightVal = (decimal)Math.Round(stdSight, 1);
                if (sightVal >= numReqSight.Minimum && sightVal <= numReqSight.Maximum)
                    numReqSight.Value = sightVal;
            }

            if (numTargetHeight != null)
            {
                decimal tgtVal = (decimal)stdTargetH;
                if (tgtVal >= numTargetHeight.Minimum && tgtVal <= numTargetHeight.Maximum)
                    numTargetHeight.Value = tgtVal;
            }
        }

        private void ApplyStandardLookup(bool updateControls)
        {
            if (cbbStandard.SelectedItem is not SightStandardDefinition std) return;

            int speed = GetSelectedSpeed();
            var entry = std.GetEntry(speed);
            if (entry == null) return;

            bool isStopping = rbStoppingSight.Checked;
            double stdSight = isStopping ? entry.StoppingSightDist : (entry.PassingSightDist > 0 ? entry.PassingSightDist : entry.StoppingSightDist);
            double stdTargetH = isStopping ? std.TargetHeightStopping : std.TargetHeightPassing;

            if (updateControls)
            {
                if (numReqSight != null)
                {
                    decimal sightVal = (decimal)Math.Round(stdSight, 1);
                    if (sightVal >= numReqSight.Minimum && sightVal <= numReqSight.Maximum)
                        numReqSight.Value = sightVal;
                }

                if (numEyeHeight != null)
                {
                    decimal eyeVal = (decimal)std.EyeHeight;
                    if (eyeVal >= numEyeHeight.Minimum && eyeVal <= numEyeHeight.Maximum)
                        numEyeHeight.Value = eyeVal;
                }

                if (numTargetHeight != null)
                {
                    decimal tgtVal = (decimal)stdTargetH;
                    if (tgtVal >= numTargetHeight.Minimum && tgtVal <= numTargetHeight.Maximum)
                        numTargetHeight.Value = tgtVal;
                }

                if (numLaneOffset != null)
                {
                    decimal laneVal = (decimal)entry.DefaultLaneOffset;
                    if (laneVal >= numLaneOffset.Minimum && laneVal <= numLaneOffset.Maximum)
                        numLaneOffset.Value = laneVal;
                }

                if (numInterval != null)
                {
                    decimal intervalVal = (entry.Speed >= 80) ? 20m : ((entry.Speed >= 40) ? 10m : 5m);
                    if (intervalVal >= numInterval.Minimum && intervalVal <= numInterval.Maximum)
                        numInterval.Value = intervalVal;
                }
            }

            // Luôn cập nhật thẻ tóm tắt tra cứu đầy đủ, rõ ràng
            if (lblStdSummaryTitle != null && lblStdSight != null && lblStdVertical != null && lblStdHorizontal != null)
            {
                bool isExactMatch = std.Entries.Any(e => e.Speed == speed);
                string tagStr = isExactMatch ? "Chính xác theo bảng" : "Nội suy tuyến tính";
                lblStdSummaryTitle.Text = $"💡 Thông số tra chuẩn {std.Name} (V = {speed} km/h - {tagStr}):";
                string passStr = entry.PassingSightDist > 0 ? $"{entry.PassingSightDist:F0} m" : "Không áp dụng";

                lblStdSight.Text = $"• Tầm nhìn: S_dừng ≥ {entry.StoppingSightDist:F0} m | S_vượt ≥ {passStr} | B_làn = {entry.LaneWidth:F2} m (Offset tim = {entry.DefaultLaneOffset:F2} m)";
                lblStdVertical.Text = $"• Cong đứng: R_lồi ≥ {entry.MinConvexRadiusLimit:F0} m (tt {entry.MinConvexRadius:F0} m) | R_lõm ≥ {entry.MinConcaveRadiusLimit:F0} m (tt {entry.MinConcaveRadius:F0} m)";
                lblStdHorizontal.Text = $"• Cong nằm & Dốc: R_nằm min ≥ {entry.MinHorizontalRadius:F0} m (tt {entry.MinHorizontalRadiusNormal:F0} m) | i_max ≤ {entry.MaxGrade:F1}% | f = {entry.FrictionFactor:F2}";
            }
        }
    }
}

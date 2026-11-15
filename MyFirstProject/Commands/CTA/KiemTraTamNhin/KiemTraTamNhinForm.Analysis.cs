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
        private void BtnAnalyze_Click(object sender, EventArgs e)
        {
            SaveCurrentSettings();
            if (cbbStandard.SelectedItem is not SightStandardDefinition std)
            {
                MessageBox.Show("Vui lòng chọn Tiêu chuẩn thiết kế!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int speed = GetSelectedSpeed();
            if (speed <= 0)
            {
                MessageBox.Show("Vui lòng nhập vận tốc thiết kế hợp lệ (> 0 km/h)!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            SightType sightType = rbStoppingSight.Checked ? SightType.Stopping : SightType.Passing;

            ObjectId surfId = (cbbSurface.SelectedItem is ObjectIdItem sItem) ? sItem.Id : ObjectId.Null;

            double reqSight = (numReqSight != null) ? (double)numReqSight.Value : 0.0;
            double interval = (double)numInterval.Value;
            double laneOffset = (double)numLaneOffset.Value;
            double eyeH = (double)numEyeHeight.Value;
            double targetH = (double)numTargetHeight.Value;

            bool checkGrade = chkCheckGrade.Checked;
            bool checkClearance = chkCheckClearance.Checked;
            bool check3D = chkCheck3DSurface.Checked;

            var doc = Application.DocumentManager.MdiActiveDocument;

            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                CivSurface? surface = null;
                if (!surfId.IsNull)
                {
                    try { surface = tr.GetObject(surfId, OpenMode.ForRead) as CivSurface; } catch { }
                }

                // 1. Phân tích theo Alignment
                if (rbSourceAlignment.Checked)
                {
                    if (cbbAlignment.SelectedItem is not ObjectIdItem alignItem || alignItem.Id.IsNull)
                    {
                        MessageBox.Show("Vui lòng chọn Alignment cần kiểm tra!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    var align = tr.GetObject(alignItem.Id, OpenMode.ForRead) as Alignment;
                    if (align == null)
                    {
                        MessageBox.Show("Không thể mở Alignment!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    ObjectId profId = (cbbProfile.SelectedItem is ObjectIdItem pItem) ? pItem.Id : ObjectId.Null;
                    Profile? profile = null;
                    if (!profId.IsNull)
                    {
                        try { profile = tr.GetObject(profId, OpenMode.ForRead) as Profile; } catch { }
                    }

                    AnalyzeAlignment(align, profile, surface, std, speed, sightType, interval, laneOffset, eyeH, targetH, checkGrade, checkClearance, check3D, reqSight);
                }
                // 2. Phân tích theo Polyline / 3D Polyline
                else
                {
                    if (_selectedPolylineId.IsNull || !_selectedPolylineId.IsValid)
                    {
                        MessageBox.Show("Vui lòng Pick chọn đường Polyline cần kiểm tra!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    var ent = tr.GetObject(_selectedPolylineId, OpenMode.ForRead);
                    Curve? curve = ent as Curve;

                    // Nếu là Polyline 2D và có surface mà chưa chuyển 3D, tự động chuyển 3D Polyline để có trắc dọc
                    if (ent is Polyline pline && surface != null)
                    {
                        ObjectId poly3dId = SightDistanceEngine.ConvertPolylineToPolyline3d(pline, surface, 5.0);
                        if (!poly3dId.IsNull)
                        {
                            curve = tr.GetObject(poly3dId, OpenMode.ForRead) as Curve;
                            _selectedPolylineId = poly3dId;
                            UpdatePolylineDisplayInfo(_selectedPolylineId);
                        }
                    }

                    if (curve == null)
                    {
                        MessageBox.Show("Không thể đọc đường hình học của Polyline!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    double totalLength = curve.GetDistanceAtParameter(curve.EndParam);

                    AnalyzeCurve(curve, surface, std, speed, sightType, totalLength, interval, laneOffset, eyeH, targetH, checkGrade, checkClearance, check3D, reqSight);
                }

                tr.Commit();
            }

            FilterAndDisplayResults();
        }

        private void FilterAndDisplayResults()
        {
            dgvResults.Rows.Clear();

            if (_analysisResults == null || _analysisResults.Count == 0)
            {
                lblSummary.Text = "Không có dữ liệu phân tích.";
                return;
            }

            int passCount = 0;
            int warnCount = 0;
            int failCount = 0;

            int rowStt = 1;
            foreach (var item in _analysisResults)
            {
                if (item.Status == SightCheckStatus.Pass) passCount++;
                else if (item.Status == SightCheckStatus.Warning) warnCount++;
                else if (item.Status == SightCheckStatus.Fail) failCount++;

                if (chkFilterOnlyErrors.Checked && item.Status == SightCheckStatus.Pass)
                    continue;

                string statusText = (item.Status == SightCheckStatus.Pass) ? "Đạt" : (item.Status == SightCheckStatus.Warning ? "Cảnh báo" : "Không đạt");
                string rStr = item.HorizontalRadius > 0 ? $"R_nam={item.HorizontalRadius:F0}" : (item.VerticalRadius > 0 ? $"R_dung={item.VerticalRadius:F0}" : "-");
                string clearStr = item.ClearanceWidthM > 0 ? $"{item.ClearanceWidthM:F2}" : "-";

                int rowIdx = dgvResults.Rows.Add(
                    rowStt++,
                    statusText,
                    item.StationFormatted,
                    $"{item.GradePercent:F2}%",
                    rStr,
                    $"{item.RequiredSight:F1}",
                    $"{item.ActualSight:F1}",
                    clearStr,
                    item.Detail
                );

                var row = dgvResults.Rows[rowIdx];
                row.Tag = item;

                if (item.Status == SightCheckStatus.Pass)
                {
                    row.Cells["colStatus"].Style.ForeColor = WinFormsColor.DarkGreen;
                    row.Cells["colStatus"].Style.Font = new WinFormsFont("Segoe UI", 9.0f, FontStyle.Bold);
                }
                else if (item.Status == SightCheckStatus.Warning)
                {
                    row.Cells["colStatus"].Style.ForeColor = WinFormsColor.DarkOrange;
                    row.Cells["colStatus"].Style.Font = new WinFormsFont("Segoe UI", 9.0f, FontStyle.Bold);
                    row.DefaultCellStyle.BackColor = WinFormsColor.FromArgb(255, 252, 240);
                }
                else
                {
                    row.Cells["colStatus"].Style.ForeColor = WinFormsColor.Crimson;
                    row.Cells["colStatus"].Style.Font = new WinFormsFont("Segoe UI", 9.0f, FontStyle.Bold);
                    row.DefaultCellStyle.BackColor = WinFormsColor.FromArgb(255, 240, 240);
                }
            }

            lblSummary.Text = $"Tổng số: {_analysisResults.Count} vị trí | Đạt: {passCount} | Cảnh báo: {warnCount} | Không đạt: {failCount}";
            if (failCount > 0) lblSummary.ForeColor = WinFormsColor.Crimson;
            else if (warnCount > 0) lblSummary.ForeColor = WinFormsColor.DarkOrange;
            else lblSummary.ForeColor = WinFormsColor.DarkGreen;
        }

        /// <summary>Dispatches the captured alignment inputs without changing transaction ownership.</summary>
        private void AnalyzeAlignment(Alignment align, Profile? profile, CivSurface? surface,
            SightStandardDefinition std, int speed, SightType sightType, double interval,
            double laneOffset, double eyeH, double targetH, bool checkGrade,
            bool checkClearance, bool check3D, double reqSight)
        {
            _analysisResults = SightDistanceEngine.AnalyzeSightDistance(
                align,
                profile,
                surface,
                std,
                speed,
                sightType,
                align.StartingStation,
                align.EndingStation,
                interval,
                laneOffset,
                eyeH,
                targetH,
                checkGrade,
                checkClearance,
                check3D,
                reqSight
            );
        }

        /// <summary>Dispatches the captured curve inputs without changing transaction ownership.</summary>
        private void AnalyzeCurve(Curve curve, CivSurface? surface, SightStandardDefinition std,
            int speed, SightType sightType, double totalLength, double interval,
            double laneOffset, double eyeH, double targetH, bool checkGrade,
            bool checkClearance, bool check3D, double reqSight)
        {
            _analysisResults = SightDistanceEngine.AnalyzeSightDistanceForCurve(
                curve,
                surface,
                std,
                speed,
                sightType,
                0.0,
                totalLength,
                interval,
                laneOffset,
                eyeH,
                targetH,
                checkGrade,
                checkClearance,
                check3D,
                reqSight
            );
        }
    }
}

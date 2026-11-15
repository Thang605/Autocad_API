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
        /// <summary>Builds standard rows with their original vertical offsets.</summary>
        private void BuildStandardGroup(TableLayoutPanel topTable)
        {
            // ==========================================
            // GROUP 2: TIÊU CHUẨN & THÔNG SỐ (BỐ CỤC LƯỚI RÕ RÀNG)
            // ==========================================
            var grpStandard = new GroupBox
            {
                Text = "2. Tiêu chuẩn, Vận tốc & Tra cứu",
                Dock = DockStyle.Fill,
                Font = new WinFormsFont("Segoe UI", 9.0f, FontStyle.Bold)
            };
            topTable.Controls.Add(grpStandard, 1, 0);

            var pnlGrp2 = new Panel { Dock = DockStyle.Fill, Font = new WinFormsFont("Segoe UI", 8.75f, FontStyle.Regular), Padding = new Padding(6) };
            grpStandard.Controls.Add(pnlGrp2);
            int y = 2;
            BuildStandardSelectionRow(pnlGrp2, y);
            y += 46;
            BuildSightTypeRow(pnlGrp2, y);
            y += 46;
            BuildGeometryRow(pnlGrp2, y);
            y += 44;
            BuildStandardSummary(pnlGrp2, y);
        }

        /// <summary>Builds the original standard and speed selection row.</summary>
        private void BuildStandardSelectionRow(Panel pnlGrp2, int y)
        {
            // Hàng 1: Tiêu chuẩn, Vận tốc, Bảng tra, Tra lại
            var lblStd = new WinFormsLabel { Text = "Tiêu chuẩn:", Location = new WinFormsPoint(6, y), AutoSize = true };
            pnlGrp2.Controls.Add(lblStd);

            cbbStandard = new ComboBox { Location = new WinFormsPoint(6, y + 18), Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
            cbbStandard.SelectedIndexChanged += CbbStandard_SelectedIndexChanged;
            pnlGrp2.Controls.Add(cbbStandard);

            var lblSpd = new WinFormsLabel { Text = "Vận tốc V:", Location = new WinFormsPoint(172, y), AutoSize = true };
            pnlGrp2.Controls.Add(lblSpd);

            cbbSpeed = new ComboBox { Location = new WinFormsPoint(172, y + 18), Width = 72, DropDownStyle = ComboBoxStyle.DropDown };
            cbbSpeed.SelectedIndexChanged += CbbSpeed_SelectedIndexChanged;
            cbbSpeed.TextChanged += CbbSpeed_TextChanged;
            cbbSpeed.Leave += (s, e) => ApplyStandardLookup(updateControls: true);
            cbbSpeed.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    ApplyStandardLookup(updateControls: true);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };
            pnlGrp2.Controls.Add(cbbSpeed);

            btnLookupTable = new Button
            {
                Text = "📋 Bảng tra...",
                Location = new WinFormsPoint(250, y + 17),
                Width = 92,
                Height = 26,
                BackColor = WinFormsColor.FromArgb(235, 245, 255),
                ForeColor = WinFormsColor.FromArgb(10, 60, 130)
            };
            btnLookupTable.Click += BtnLookupTable_Click;
            pnlGrp2.Controls.Add(btnLookupTable);

            btnResetStandard = new Button
            {
                Text = "🔄 Tra lại",
                Location = new WinFormsPoint(348, y + 17),
                Width = 82,
                Height = 26
            };
            btnResetStandard.Click += BtnResetStandard_Click;
            pnlGrp2.Controls.Add(btnResetStandard);
        }

        /// <summary>Builds the original sight-type row.</summary>
        private void BuildSightTypeRow(Panel pnlGrp2, int y)
        {
            // Hàng 2: Loại tầm nhìn (Dừng xe / Vượt xe) & Ô nhập Tầm nhìn yêu cầu S (m)
            var lblType = new WinFormsLabel { Text = "Loại tầm nhìn:", Location = new WinFormsPoint(6, y), AutoSize = true };
            pnlGrp2.Controls.Add(lblType);

            rbStoppingSight = new RadioButton { Text = "Dừng xe (SSD)", Location = new WinFormsPoint(6, y + 18), Width = 108, Checked = true };
            rbStoppingSight.CheckedChanged += (s, e) =>
            {
                if (rbStoppingSight.Checked) ApplyStandardSightTypeChanged();
            };
            pnlGrp2.Controls.Add(rbStoppingSight);

            rbPassingSight = new RadioButton { Text = "Vượt xe (PSD)", Location = new WinFormsPoint(118, y + 18), Width = 108 };
            rbPassingSight.CheckedChanged += (s, e) =>
            {
                if (rbPassingSight.Checked) ApplyStandardSightTypeChanged();
            };
            pnlGrp2.Controls.Add(rbPassingSight);

            lblReqSight = new WinFormsLabel
            {
                Text = "Tầm nhìn yêu cầu S (m):",
                Location = new WinFormsPoint(232, y),
                AutoSize = true,
                Font = new WinFormsFont("Segoe UI", 9.0f, FontStyle.Bold),
                ForeColor = WinFormsColor.FromArgb(15, 60, 140)
            };
            pnlGrp2.Controls.Add(lblReqSight);

            numReqSight = new NumericUpDown
            {
                Location = new WinFormsPoint(232, y + 18),
                Width = 98,
                DecimalPlaces = 1,
                Increment = 5m,
                Minimum = 5m,
                Maximum = 2000m,
                Value = 75m,
                Font = new WinFormsFont("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = WinFormsColor.FromArgb(10, 50, 130),
                BackColor = WinFormsColor.FromArgb(240, 248, 255)
            };
            pnlGrp2.Controls.Add(numReqSight);

            var lblReqSightHint = new WinFormsLabel
            {
                Text = "(Tra chuẩn / Sửa)",
                Location = new WinFormsPoint(335, y + 21),
                AutoSize = true,
                Font = new WinFormsFont("Segoe UI", 7.75f, FontStyle.Regular),
                ForeColor = WinFormsColor.Gray
            };
            pnlGrp2.Controls.Add(lblReqSightHint);
        }

        /// <summary>Builds the original geometry input row.</summary>
        private void BuildGeometryRow(Panel pnlGrp2, int y)
        {
            // Hàng 3: 4 thông số hình học xe chạy: Mắt h1, Vật cản h2, Offset làn, Bước kiểm tra
            var lblEye = new WinFormsLabel { Text = "Mắt h1 (m):", Location = new WinFormsPoint(6, y), AutoSize = true };
            pnlGrp2.Controls.Add(lblEye);

            numEyeHeight = new NumericUpDown { Location = new WinFormsPoint(6, y + 18), Width = 80, DecimalPlaces = 2, Increment = 0.05m, Minimum = 0.5m, Maximum = 3.0m, Value = 1.25m };
            pnlGrp2.Controls.Add(numEyeHeight);

            var lblTgt = new WinFormsLabel { Text = "Vật cản h2 (m):", Location = new WinFormsPoint(92, y), AutoSize = true };
            pnlGrp2.Controls.Add(lblTgt);

            numTargetHeight = new NumericUpDown { Location = new WinFormsPoint(92, y + 18), Width = 80, DecimalPlaces = 2, Increment = 0.05m, Minimum = 0.05m, Maximum = 3.0m, Value = 0.10m };
            pnlGrp2.Controls.Add(numTargetHeight);

            var lblLane = new WinFormsLabel { Text = "Offset làn (m):", Location = new WinFormsPoint(178, y), AutoSize = true };
            pnlGrp2.Controls.Add(lblLane);

            numLaneOffset = new NumericUpDown { Location = new WinFormsPoint(178, y + 18), Width = 82, DecimalPlaces = 2, Increment = 0.25m, Minimum = -20m, Maximum = 20m, Value = 1.75m };
            pnlGrp2.Controls.Add(numLaneOffset);

            var lblStep = new WinFormsLabel { Text = "Bước kt (m):", Location = new WinFormsPoint(266, y), AutoSize = true };
            pnlGrp2.Controls.Add(lblStep);

            numInterval = new NumericUpDown { Location = new WinFormsPoint(266, y + 18), Width = 72, DecimalPlaces = 0, Increment = 5m, Minimum = 5m, Maximum = 200m, Value = 20m };
            pnlGrp2.Controls.Add(numInterval);
        }

        /// <summary>Builds the original standard summary card.</summary>
        private void BuildStandardSummary(Panel pnlGrp2, int y)
        {
            // Hàng 4: Thẻ tóm tắt thông số tra cứu chuẩn (4 dòng độc lập, không che khuất)
            pnlStdSummary = new Panel
            {
                Location = new WinFormsPoint(6, y),
                Size = new Size(430, 88),
                BackColor = WinFormsColor.FromArgb(240, 247, 255),
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(6, 4, 6, 4)
            };
            pnlGrp2.Controls.Add(pnlStdSummary);

            lblStdSummaryTitle = new WinFormsLabel
            {
                Location = new WinFormsPoint(6, 4),
                Size = new Size(416, 18),
                Font = new WinFormsFont("Segoe UI", 8.25f, FontStyle.Bold),
                ForeColor = WinFormsColor.FromArgb(20, 70, 140),
                Text = "💡 Thông số tra cứu chuẩn:"
            };
            pnlStdSummary.Controls.Add(lblStdSummaryTitle);

            lblStdSight = new WinFormsLabel
            {
                Location = new WinFormsPoint(6, 23),
                Size = new Size(416, 18),
                Font = new WinFormsFont("Segoe UI", 8.0f, FontStyle.Regular),
                ForeColor = WinFormsColor.FromArgb(20, 30, 45),
                Text = "• Tầm nhìn: Đang nạp dữ liệu..."
            };
            pnlStdSummary.Controls.Add(lblStdSight);

            lblStdVertical = new WinFormsLabel
            {
                Location = new WinFormsPoint(6, 42),
                Size = new Size(416, 18),
                Font = new WinFormsFont("Segoe UI", 8.0f, FontStyle.Regular),
                ForeColor = WinFormsColor.FromArgb(20, 30, 45),
                Text = "• Cong đứng: Đang nạp dữ liệu..."
            };
            pnlStdSummary.Controls.Add(lblStdVertical);

            lblStdHorizontal = new WinFormsLabel
            {
                Location = new WinFormsPoint(6, 61),
                Size = new Size(416, 18),
                Font = new WinFormsFont("Segoe UI", 8.0f, FontStyle.Regular),
                ForeColor = WinFormsColor.FromArgb(20, 30, 45),
                Text = "• Cong nằm & Dốc: Đang nạp dữ liệu..."
            };
            pnlStdSummary.Controls.Add(lblStdHorizontal);
        }
    }
}

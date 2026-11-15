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
        /// <summary>Builds the original source-selection group.</summary>
        private void BuildSourceGroup(TableLayoutPanel topTable)
        {
            // ==========================================
            // GROUP 1: NGUỒN ĐƯỜNG KIỂM TRA (ALIGNMENT HOẶC POLYLINE)
            // ==========================================
            var grpSource = new GroupBox
            {
                Text = "1. Đối tượng đường kiểm tra",
                Dock = DockStyle.Fill,
                Font = new WinFormsFont("Segoe UI", 9.0f, FontStyle.Bold)
            };
            topTable.Controls.Add(grpSource, 0, 0);

            var pnlGrp1 = new Panel { Dock = DockStyle.Fill, Font = new WinFormsFont("Segoe UI", 8.75f, FontStyle.Regular), Padding = new Padding(6) };
            grpSource.Controls.Add(pnlGrp1);

            int y = 4;
            rbSourceAlignment = new RadioButton { Text = "Tuyến Civil 3D", Location = new WinFormsPoint(6, y), AutoSize = true, Checked = true };
            rbSourceAlignment.CheckedChanged += RbSourceMode_CheckedChanged;
            pnlGrp1.Controls.Add(rbSourceAlignment);

            rbSourcePolyline = new RadioButton { Text = "Đường Polyline (CAD 2D/3D)", Location = new WinFormsPoint(125, y), AutoSize = true };
            rbSourcePolyline.CheckedChanged += RbSourceMode_CheckedChanged;
            pnlGrp1.Controls.Add(rbSourcePolyline);

            // Panel con cho Alignment
            y += 26;
            pnlAlignmentControls = new Panel { Location = new WinFormsPoint(2, y), Size = new Size(310, 112) };
            pnlGrp1.Controls.Add(pnlAlignmentControls);

            int subY = 0;
            var lblAlign = new WinFormsLabel { Text = "Tim tuyến (Alignment):", Location = new WinFormsPoint(4, subY), AutoSize = true };
            pnlAlignmentControls.Controls.Add(lblAlign);

            cbbAlignment = new ComboBox { Location = new WinFormsPoint(4, subY + 19), Width = 232, DropDownStyle = ComboBoxStyle.DropDownList };
            cbbAlignment.SelectedIndexChanged += CbbAlignment_SelectedIndexChanged;
            pnlAlignmentControls.Controls.Add(cbbAlignment);

            btnPickAlignment = new Button { Text = "Pick", Location = new WinFormsPoint(240, subY + 18), Width = 52, Height = 25 };
            btnPickAlignment.Click += BtnPickAlignment_Click;
            pnlAlignmentControls.Controls.Add(btnPickAlignment);

            subY += 46;
            var lblProf = new WinFormsLabel { Text = "Trắc dọc thiết kế (Profile):", Location = new WinFormsPoint(4, subY), AutoSize = true };
            pnlAlignmentControls.Controls.Add(lblProf);

            cbbProfile = new ComboBox { Location = new WinFormsPoint(4, subY + 19), Width = 288, DropDownStyle = ComboBoxStyle.DropDownList };
            pnlAlignmentControls.Controls.Add(cbbProfile);

            // Panel con cho Polyline
            pnlPolylineControls = new Panel { Location = new WinFormsPoint(2, y), Size = new Size(310, 112), Visible = false };
            pnlGrp1.Controls.Add(pnlPolylineControls);

            subY = 0;
            var lblPoly = new WinFormsLabel { Text = "Đường Polyline đã chọn:", Location = new WinFormsPoint(4, subY), AutoSize = true };
            pnlPolylineControls.Controls.Add(lblPoly);

            txtPolylineInfo = new TextBox { Location = new WinFormsPoint(4, subY + 19), Width = 232, ReadOnly = true, BackColor = WinFormsColor.WhiteSmoke, Text = "(Chưa chọn Polyline)" };
            pnlPolylineControls.Controls.Add(txtPolylineInfo);

            btnPickPolyline = new Button { Text = "Pick", Location = new WinFormsPoint(240, subY + 18), Width = 52, Height = 25 };
            btnPickPolyline.Click += BtnPickPolyline_Click;
            pnlPolylineControls.Controls.Add(btnPickPolyline);

            subY += 48;
            btnConvertTo3D = new Button
            {
                Text = "⚡ Chuyển Polyline 2D sang 3D theo Surface",
                Location = new WinFormsPoint(4, subY),
                Width = 288,
                Height = 28,
                BackColor = WinFormsColor.FromArgb(235, 245, 255),
                ForeColor = WinFormsColor.FromArgb(10, 60, 130)
            };
            btnConvertTo3D.Click += BtnConvertTo3D_Click;
            pnlPolylineControls.Controls.Add(btnConvertTo3D);

            // Surface nằm cố định bên dưới Group 1
            y += 114;
            var lblSurf = new WinFormsLabel { Text = "Bề mặt địa hình (Surface - kiểm tra 3D Ray):", Location = new WinFormsPoint(6, y), AutoSize = true };
            pnlGrp1.Controls.Add(lblSurf);

            cbbSurface = new ComboBox { Location = new WinFormsPoint(6, y + 20), Width = 232, DropDownStyle = ComboBoxStyle.DropDownList };
            pnlGrp1.Controls.Add(cbbSurface);

            btnPickSurface = new Button { Text = "Pick", Location = new WinFormsPoint(242, y + 19), Width = 52, Height = 25 };
            btnPickSurface.Click += BtnPickSurface_Click;
            pnlGrp1.Controls.Add(btnPickSurface);
        }

        /// <summary>Builds the original analysis options group.</summary>
        private void BuildOptionsGroup(TableLayoutPanel topTable)
        {
            // ==========================================
            // GROUP 3: PHẠM VI & TÙY CHỌN PHÂN TÍCH
            // ==========================================
            var grpOptions = new GroupBox
            {
                Text = "3. Phạm vi & Tùy chọn",
                Dock = DockStyle.Fill,
                Font = new WinFormsFont("Segoe UI", 9.0f, FontStyle.Bold)
            };
            topTable.Controls.Add(grpOptions, 2, 0);

            var pnlGrp3 = new Panel { Dock = DockStyle.Fill, Font = new WinFormsFont("Segoe UI", 8.75f, FontStyle.Regular), Padding = new Padding(6) };
            grpOptions.Controls.Add(pnlGrp3);

            int y = 4;
            var lblStRange = new WinFormsLabel { Text = "Lý trình kiểm tra (Từ - Đến):", Location = new WinFormsPoint(6, y), AutoSize = true };
            pnlGrp3.Controls.Add(lblStRange);

            txtStartStation = new TextBox { Location = new WinFormsPoint(6, y + 20), Width = 120, ReadOnly = true, BackColor = WinFormsColor.WhiteSmoke };
            pnlGrp3.Controls.Add(txtStartStation);

            txtEndStation = new TextBox { Location = new WinFormsPoint(135, y + 20), Width = 120, ReadOnly = true, BackColor = WinFormsColor.WhiteSmoke };
            pnlGrp3.Controls.Add(txtEndStation);

            y += 48;
            chkCheckGrade = new CheckBox { Text = "Xét độ dốc dọc i (%) đến tầm nhìn SSD", Location = new WinFormsPoint(6, y), AutoSize = true, Checked = true };
            pnlGrp3.Controls.Add(chkCheckGrade);

            y += 24;
            chkCheckClearance = new CheckBox { Text = "Tính khoảng dạt tầm nhìn bụng cong M", Location = new WinFormsPoint(6, y), AutoSize = true, Checked = true };
            pnlGrp3.Controls.Add(chkCheckClearance);

            y += 24;
            chkCheck3DSurface = new CheckBox { Text = "Kiểm tra tia ngắm 3D cắt địa hình (Surface)", Location = new WinFormsPoint(6, y), AutoSize = true, Checked = true };
            pnlGrp3.Controls.Add(chkCheck3DSurface);

            y += 26;
            chkFilterOnlyErrors = new CheckBox { Text = "Chỉ lọc các vị trí Cảnh báo / Không đạt", Location = new WinFormsPoint(6, y), AutoSize = true, Checked = false };
            chkFilterOnlyErrors.CheckedChanged += (s, e) => FilterAndDisplayResults();
            pnlGrp3.Controls.Add(chkFilterOnlyErrors);

            y += 24;
            chkDrawEyePath = new CheckBox { Text = "Vẽ quỹ đạo mắt lái xe (Polyline offset làn)", Location = new WinFormsPoint(6, y), AutoSize = true, Checked = true };
            pnlGrp3.Controls.Add(chkDrawEyePath);
        }
    }
}

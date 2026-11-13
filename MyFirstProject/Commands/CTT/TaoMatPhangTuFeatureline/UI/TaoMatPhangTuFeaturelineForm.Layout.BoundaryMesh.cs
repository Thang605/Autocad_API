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
        private GroupBox BuildRightBoundaryGroup()
        {
                // ═══ GROUP 3: KIỂM SOÁT BIÊN BAO (CHỐNG PHÓNG TAM GIÁC THỪA) ═══
                GroupBox grpBoundary = new GroupBox
                {
                    Text = "3. Kiểm soát Biên bao && Chống tam giác thừa",
                    Font = CreateFont(9.5F, FontStyle.Bold),
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    Padding = new Padding(12, 10, 12, 10),
                    Margin = new Padding(0, 0, 0, 8)
                };

                TableLayoutPanel tlpBoundary = new TableLayoutPanel
                {
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    ColumnCount = 1,
                    RowCount = 6
                };
                tlpBoundary.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(SizeType.Percent, 100F));

                radBoundaryAuto = new RadioButton
                {
                    Text = "Tự động thông minh (Khuyên dùng cho cả dải đường Hở & đường Kín)",
                    Font = CreateFont(9F, FontStyle.Bold),
                    AutoSize = true,
                    Checked = true,
                    UseMnemonic = false,
                    Margin = new Padding(0, 2, 0, 2)
                };
                radBoundaryAuto.CheckedChanged += (s, e) => UpdateControlStates();

                WinFormsLabel lblAutoHint = new WinFormsLabel
                {
                    Text = "💡 Tự nhận diện đường khép kín & dải đường hở (taluy, lề, rãnh, nút giao), cắt 100% tam giác dây cung qua bụng đường cong và khoảng trống mà không sinh mặt phẳng phụ.",
                    Font = CreateFont(8.25F, FontStyle.Italic),
                    ForeColor = WinFormsColor.FromArgb(30, 105, 55),
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    MaximumSize = new Size(410, 0),
                    Padding = new Padding(20, 0, 0, 6),
                    UseMnemonic = false
                };

                chkNestedClosedAsHole = new CheckBox
                {
                    Text = "Coi đường khép kín nằm lọt trong đường kín khác là Lỗ khoét (Hide)",
                    Font = CreateFont(8.75F, FontStyle.Regular),
                    AutoSize = true,
                    Checked = _lastTreatNestedAsHole,
                    UseMnemonic = false,
                    Margin = new Padding(22, 0, 0, 6)
                };
                _toolTip.SetToolTip(chkNestedClosedAsHole, "Bỏ tích: đường kín bên trong đóng vai trò đường đồng mức/mặt bệ. Tích chọn: khoét rỗng đảo bên trong.");

                radBoundaryCustom = new RadioButton
                {
                    Text = "Giới hạn theo đường Biên bao chọn riêng trên CAD (ví dụ layer 2.BAO):",
                    Font = CreateFont(9F, FontStyle.Regular),
                    AutoSize = true,
                    UseMnemonic = false,
                    Margin = new Padding(0, 4, 0, 4)
                };
                radBoundaryCustom.CheckedChanged += (s, e) => UpdateControlStates();

                pnlCustomBoundary = new Panel
                {
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    Padding = new Padding(18, 2, 0, 6)
                };

                TableLayoutPanel tlpCustomBnd = new TableLayoutPanel
                {
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    ColumnCount = 1,
                    RowCount = 2
                };
                tlpCustomBnd.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(SizeType.Percent, 100F));

                FlowLayoutPanel flpCustomBtns = new FlowLayoutPanel
                {
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    WrapContents = true
                };

                btnPickOuterBnd = CreateActionButton("📐 Chọn Biên ngoài", 142, 40, WinFormsColor.FromArgb(20, 72, 118), WinFormsColor.White, false);
                btnPickOuterBnd.Click += (s, e) => PickCustomBoundaryEntities(isOuter: true);

                btnPickHideBnd = CreateActionButton("🕳️ Chọn Lỗ khoét", 132, 40, WinFormsColor.FromArgb(108, 117, 125), WinFormsColor.White, false);
                btnPickHideBnd.Click += (s, e) => PickCustomBoundaryEntities(isOuter: false);

                btnClearBnd = CreateActionButton("Xóa biên", 85, 40, WinFormsColor.FromArgb(232, 236, 241), WinFormsColor.FromArgb(55, 65, 80), false);
                btnClearBnd.Click += (s, e) =>
                {
                    _customOuterIds.Clear();
                    _customHideIds.Clear();
                    UpdateCustomBoundaryLabel();
                };

                flpCustomBtns.Controls.Add(btnPickOuterBnd);
                flpCustomBtns.Controls.Add(btnPickHideBnd);
                flpCustomBtns.Controls.Add(btnClearBnd);

                lblCustomBndInfo = new WinFormsLabel
                {
                    Text = "Biên riêng đã chọn: 0 Outer | 0 Hide",
                    Font = CreateFont(8.5F),
                    ForeColor = WinFormsColor.FromArgb(70, 80, 95),
                    AutoSize = true,
                    Padding = new Padding(2, 4, 0, 0),
                    UseMnemonic = false
                };

                tlpCustomBnd.Controls.Add(flpCustomBtns, 0, 0);
                tlpCustomBnd.Controls.Add(lblCustomBndInfo, 0, 1);
                pnlCustomBoundary.Controls.Add(tlpCustomBnd);

                radBoundaryNone = new RadioButton
                {
                    Text = "Chỉ thêm Breaklines thuần túy (Không cắt biên tam giác)",
                    Font = CreateFont(9F, FontStyle.Regular),
                    AutoSize = true,
                    UseMnemonic = false,
                    Margin = new Padding(0, 2, 0, 0)
                };
                radBoundaryNone.CheckedChanged += (s, e) => UpdateControlStates();

                tlpBoundary.Controls.Add(radBoundaryAuto, 0, 0);
                tlpBoundary.Controls.Add(lblAutoHint, 0, 1);
                tlpBoundary.Controls.Add(chkNestedClosedAsHole, 0, 2);
                tlpBoundary.Controls.Add(radBoundaryCustom, 0, 3);
                tlpBoundary.Controls.Add(pnlCustomBoundary, 0, 4);
                tlpBoundary.Controls.Add(radBoundaryNone, 0, 5);
                grpBoundary.Controls.Add(tlpBoundary);
            return grpBoundary;
        }

        private GroupBox BuildRightMeshParamsGroup()
        {
                // ═══ GROUP 4: THÔNG SỐ CHIA LƯỚI TAM GIÁC & BỀ RỘNG DẢI ═══
                GroupBox grpMeshParams = new GroupBox
                {
                    Text = "4. Thông số độ mịn Breakline && Giới hạn cạnh bắc cầu",
                    Font = CreateFont(9.5F, FontStyle.Bold),
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    Padding = new Padding(12, 10, 12, 10),
                    Margin = new Padding(0, 0, 0, 4)
                };

                TableLayoutPanel tlpMesh = new TableLayoutPanel
                {
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    ColumnCount = 3,
                    RowCount = 3
                };
                tlpMesh.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(SizeType.Percent, 100F));
                tlpMesh.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(SizeType.Absolute, 105F));
                tlpMesh.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(SizeType.AutoSize));

                // Dòng 1: Mid-ordinate
                WinFormsLabel lblMidOrd = new WinFormsLabel
                {
                    Text = "Độ mịn cung cong (Mid-ordinate, m):",
                    Font = CreateFont(8.75F, FontStyle.Regular),
                    AutoSize = true,
                    Anchor = AnchorStyles.Left,
                    UseMnemonic = false,
                    Margin = new Padding(0, 6, 6, 6)
                };

                numMidOrd = new NumericUpDown
                {
                    Dock = DockStyle.Fill,
                    DecimalPlaces = 2,
                    Minimum = 0.01M,
                    Maximum = 10.00M,
                    Increment = 0.05M,
                    Value = 0.10M,
                    Font = CreateFont(9F),
                    Margin = new Padding(0, 4, 6, 6)
                };

                // Dòng 2: Supplementing Distance
                chkUseSupplementing = new CheckBox
                {
                    Text = "Chèn thêm đỉnh đoạn thẳng (Supplementing, m):",
                    Font = CreateFont(8.75F, FontStyle.Regular),
                    AutoSize = true,
                    Anchor = AnchorStyles.Left,
                    Checked = true,
                    UseMnemonic = false,
                    Margin = new Padding(0, 6, 6, 6)
                };
                chkUseSupplementing.CheckedChanged += (s, e) => numSupplementing.Enabled = chkUseSupplementing.Checked;
                _toolTip.SetToolTip(chkUseSupplementing, "Chia mịn các đoạn Feature Line thẳng dài để lưới tam giác bám đều theo mặt cắt ngang dải đường/taluy");

                numSupplementing = new NumericUpDown
                {
                    Dock = DockStyle.Fill,
                    DecimalPlaces = 2,
                    Minimum = 0.20M,
                    Maximum = 500.00M,
                    Increment = 0.50M,
                    Value = 2.00M,
                    Font = CreateFont(9F),
                    Margin = new Padding(0, 4, 6, 6)
                };

                // Dòng 3: Max Edge Length + Nút Gợi ý
                chkLimitMaxEdge = new CheckBox
                {
                    Text = "Cắt cạnh tam giác bắc cầu dài quá (m):",
                    Font = CreateFont(8.75F, FontStyle.Regular),
                    AutoSize = true,
                    Anchor = AnchorStyles.Left,
                    Checked = true,
                    UseMnemonic = false,
                    Margin = new Padding(0, 8, 6, 4)
                };
                chkLimitMaxEdge.CheckedChanged += (s, e) =>
                {
                    numMaxEdgeLength.Enabled = chkLimitMaxEdge.Checked;
                    btnSuggestMaxEdge.Enabled = chkLimitMaxEdge.Checked;
                };
                _toolTip.SetToolTip(chkLimitMaxEdge, "Ngăn các dải Feature Line rời nhau (ví dụ Taluy trái & Taluy phải cách nhau qua lòng đường) nối tam giác sang nhau");

                numMaxEdgeLength = new NumericUpDown
                {
                    Dock = DockStyle.Fill,
                    DecimalPlaces = 2,
                    Minimum = 0.50M,
                    Maximum = 5000.00M,
                    Increment = 1.00M,
                    Value = 25.00M,
                    Font = CreateFont(9F),
                    Margin = new Padding(0, 8, 6, 4)
                };

                btnSuggestMaxEdge = CreateActionButton("⚡ Gợi ý", 84, 40, WinFormsColor.FromArgb(255, 243, 205), WinFormsColor.FromArgb(120, 80, 0), true);
                btnSuggestMaxEdge.Margin = new Padding(0, 2, 0, 2);
                btnSuggestMaxEdge.Click += BtnSuggestMaxEdge_Click;
                _toolTip.SetToolTip(btnSuggestMaxEdge, "Tự động đo khoảng cách ngang giữa các đường Feature Line đang chọn để gợi ý chiều dài cạnh tối đa hợp lý");

                tlpMesh.Controls.Add(lblMidOrd, 0, 0);
                tlpMesh.Controls.Add(numMidOrd, 1, 0);

                tlpMesh.Controls.Add(chkUseSupplementing, 0, 1);
                tlpMesh.Controls.Add(numSupplementing, 1, 1);

                tlpMesh.Controls.Add(chkLimitMaxEdge, 0, 2);
                tlpMesh.Controls.Add(numMaxEdgeLength, 1, 2);
                tlpMesh.Controls.Add(btnSuggestMaxEdge, 2, 2);

                grpMeshParams.Controls.Add(tlpMesh);
            return grpMeshParams;
        }
    }
}

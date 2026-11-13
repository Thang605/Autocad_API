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
        private void InitializeCustomUI()
        {
            _toolTip = new ToolTip { AutoPopDelay = 10000, InitialDelay = 350, ReshowDelay = 200 };
            _fontBold9 = CreateFont(9F, FontStyle.Bold);
            _fontRegular9 = CreateFont(9F, FontStyle.Regular);

            this.Text = "Tạo & Bổ Sung Mặt Phẳng TIN Từ Feature Line";
            this.Size = _lastFormSize;
            this.MinimumSize = new Size(980, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = _fontRegular9;
            this.BackColor = WinFormsColor.FromArgb(245, 247, 250);
            this.KeyPreview = true;

            // ═══════════════════════════════════════════════════════════════
            //  1. HEADER BANNER
            // ═══════════════════════════════════════════════════════════════
            Panel pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 68,
                BackColor = WinFormsColor.FromArgb(20, 72, 118),
                Padding = new Padding(16, 10, 16, 8)
            };

            WinFormsLabel lblHeaderTitle = new WinFormsLabel
            {
                Text = "TẠO MỚI & BỔ SUNG MẶT PHẲNG TIN TỪ FEATURE LINE (HỞ & KÍN)",
                Font = CreateFont(11.5F, FontStyle.Bold),
                ForeColor = WinFormsColor.White,
                Dock = DockStyle.Top,
                Height = 26,
                UseMnemonic = false
            };

            WinFormsLabel lblHeaderSub = new WinFormsLabel
            {
                Text = "Tạo mới 1 TIN Surface sạch hoặc Bổ sung vào mặt phẳng có sẵn qua Mặt phẳng phụ mang tiền tố tên gốc để dễ quản lý",
                Font = CreateFont(8.5F, FontStyle.Regular),
                ForeColor = WinFormsColor.FromArgb(212, 232, 252),
                Dock = DockStyle.Fill,
                UseMnemonic = false
            };

            pnlHeader.Controls.Add(lblHeaderSub);
            pnlHeader.Controls.Add(lblHeaderTitle);

            // ═══════════════════════════════════════════════════════════════
            //  2. BOTTOM FOOTER (EXECUTION BAR)
            // ═══════════════════════════════════════════════════════════════
            Panel pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 92,
                BackColor = WinFormsColor.White,
                Padding = new Padding(16, 8, 16, 10)
            };

            progressBar = new ProgressBar
            {
                Dock = DockStyle.Top,
                Height = 8,
                Visible = false
            };

            lblStatus = new WinFormsLabel
            {
                Text = "Sẵn sàng. Hãy chọn các đường Feature Line / Corridor FL / Polyline (hở hoặc kín) trên bản vẽ...",
                Font = CreateFont(8.5F, FontStyle.Italic),
                ForeColor = WinFormsColor.FromArgb(85, 98, 115),
                Dock = DockStyle.Top,
                Height = 22,
                UseMnemonic = false
            };

            FlowLayoutPanel flpBottomButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 46,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 2, 0, 0)
            };

            btnClose = new WinFormsButton
            {
                Text = "Đóng",
                Width = 110,
                Height = 42,
                BackColor = WinFormsColor.FromArgb(232, 236, 241),
                ForeColor = WinFormsColor.FromArgb(45, 55, 70),
                FlatStyle = FlatStyle.Flat,
                Font = CreateFont(9.5F, FontStyle.Regular),
                UseMnemonic = false,
                Margin = new Padding(8, 0, 0, 0)
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => this.Close();

            btnExecute = new WinFormsButton
            {
                Text = "⚡ Thực Hiện Tạo Mặt Phẳng",
                Width = 270,
                Height = 42,
                BackColor = WinFormsColor.FromArgb(13, 110, 253),
                ForeColor = WinFormsColor.White,
                FlatStyle = FlatStyle.Flat,
                Font = CreateFont(9.5F, FontStyle.Bold),
                UseMnemonic = false,
                Margin = new Padding(8, 0, 0, 0)
            };
            btnExecute.FlatAppearance.BorderSize = 0;
            btnExecute.Click += BtnExecute_Click;

            btnSyncDynamic = new WinFormsButton
            {
                Text = "🔄 Đồng Bộ Biên Động",
                Width = 184,
                Height = 42,
                BackColor = WinFormsColor.FromArgb(224, 236, 252),
                ForeColor = WinFormsColor.FromArgb(14, 62, 118),
                FlatStyle = FlatStyle.Flat,
                Font = CreateFont(9F, FontStyle.Bold),
                UseMnemonic = false,
                Margin = new Padding(0)
            };
            btnSyncDynamic.FlatAppearance.BorderSize = 0;
            btnSyncDynamic.Click += BtnSyncDynamic_Click;
            _toolTip.SetToolTip(
                btnSyncDynamic,
                "Quét và cập nhật lại toàn bộ mặt phẳng phụ tạo từ Feature Line + các mặt phẳng tổng đang PasteSurface theo hình học & ranh giới mới nhất");

            flpBottomButtons.Controls.Add(btnClose);
            flpBottomButtons.Controls.Add(btnExecute);
            flpBottomButtons.Controls.Add(btnSyncDynamic);

            pnlBottom.Controls.Add(flpBottomButtons);
            pnlBottom.Controls.Add(lblStatus);
            pnlBottom.Controls.Add(progressBar);

            // ═══════════════════════════════════════════════════════════════
            //  3. MAIN TWO-COLUMN BODY
            // ═══════════════════════════════════════════════════════════════
            TableLayoutPanel tlpMain = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(12, 10, 12, 8),
                BackColor = WinFormsColor.Transparent
            };
            tlpMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(SizeType.Percent, 53F));
            tlpMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(SizeType.Percent, 47F));

            // ───────────────────────────────────────────────────────────────
            //  CỘT TRÁI: ĐỐI TƯỢNG NGUỒN & BỘ LỌC NẠP NHANH
            // ───────────────────────────────────────────────────────────────
            GroupBox grpSource = new GroupBox
            {
                Text = "1. Đối tượng nguồn (Feature Line / Corridor FL / 3D && 2D Polyline)",
                Font = CreateFont(9.5F, FontStyle.Bold),
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            TableLayoutPanel tlpLeft = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5
            };
            tlpLeft.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Hàng 1: Nút chọn trên CAD
            tlpLeft.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Hàng 2: Lọc nhanh theo Dropdown
            tlpLeft.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // Hàng 3: ListView
            tlpLeft.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Hàng 4: Tổng kết
            tlpLeft.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Hàng 5: AutoClose

            // Hàng 1: Thanh nút chọn trên CAD (Height = 40px chuẩn chống cắt chữ High-DPI)
            FlowLayoutPanel flpPickBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                WrapContents = true,
                Margin = new Padding(0, 0, 0, 6)
            };

            btnSelectNew = CreateActionButton("🎯 Chọn trên CAD", 136, 40, WinFormsColor.FromArgb(20, 72, 118), WinFormsColor.White, true);
            btnSelectNew.Click += (s, e) => PickSourceEntitiesOnCad(append: false);
            _toolTip.SetToolTip(btnSelectNew, "Quét chọn mới các đường Feature Line, Auto Corridor FL, Polyline trên bản vẽ");

            btnSelectAdd = CreateActionButton("➕ Chọn thêm", 110, 40, WinFormsColor.FromArgb(25, 135, 84), WinFormsColor.White, true);
            btnSelectAdd.Click += (s, e) => PickSourceEntitiesOnCad(append: true);
            _toolTip.SetToolTip(btnSelectAdd, "Chọn bổ sung thêm đối tượng trên CAD vào danh sách hiện tại");

            btnRestoreLast = CreateActionButton("↺ Nạp lại cũ", 104, 40, WinFormsColor.FromArgb(255, 243, 205), WinFormsColor.FromArgb(125, 78, 0), false);
            btnRestoreLast.Click += (s, e) => RestoreLastExecutedItems();

            btnRemoveSelected = CreateActionButton("🗑️ Xóa dòng", 98, 40, WinFormsColor.FromArgb(232, 236, 241), WinFormsColor.FromArgb(55, 65, 80), false);
            btnRemoveSelected.Click += (s, e) => RemoveSelectedItemsInList();
            _toolTip.SetToolTip(btnRemoveSelected, "Loại bỏ các dòng đang bôi đen trong bảng (hoặc nhấn phím Delete)");

            btnClearAll = CreateActionButton("🧹 Xóa hết", 90, 40, WinFormsColor.FromArgb(248, 225, 228), WinFormsColor.FromArgb(155, 30, 45), false);
            btnClearAll.Click += (s, e) => ClearAllSourceItems();

            flpPickBar.Controls.Add(btnSelectNew);
            flpPickBar.Controls.Add(btnSelectAdd);
            flpPickBar.Controls.Add(btnRestoreLast);
            flpPickBar.Controls.Add(btnRemoveSelected);
            flpPickBar.Controls.Add(btnClearAll);

            // Hàng 2: Lọc & nạp nhanh theo Dropdown (Site / Layer / Corridor)
            TableLayoutPanel tlpQuickFilter = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 3,
                RowCount = 1,
                Margin = new Padding(0, 0, 0, 6)
            };
            tlpQuickFilter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(SizeType.Percent, 42F));
            tlpQuickFilter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(SizeType.Percent, 58F));
            tlpQuickFilter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(SizeType.AutoSize));

            cboFilterSource = new WinFormsComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = CreateFont(9F),
                Margin = new Padding(0, 6, 6, 0)
            };
            cboFilterSource.Items.AddRange(new object[]
            {
                "Lọc: Tất cả FL bản vẽ",
                "Lọc theo Site",
                "Lọc theo Layer",
                "Lọc theo Corridor"
            });
            cboFilterSource.SelectedIndexChanged += (s, e) => PopulateFilterValuesDropdown();

            cboFilterValue = new WinFormsComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = CreateFont(9F),
                Margin = new Padding(0, 6, 6, 0)
            };

            btnLoadByFilter = CreateActionButton("📥 Nạp lọc", 104, 40, WinFormsColor.FromArgb(224, 236, 252), WinFormsColor.FromArgb(15, 68, 130), true);
            btnLoadByFilter.Margin = new Padding(0);
            btnLoadByFilter.Click += BtnLoadByFilter_Click;
            _toolTip.SetToolTip(btnLoadByFilter, "Thêm nhanh tất cả đối tượng khớp với bộ lọc vào bảng danh sách");

            tlpQuickFilter.Controls.Add(cboFilterSource, 0, 0);
            tlpQuickFilter.Controls.Add(cboFilterValue, 1, 0);
            tlpQuickFilter.Controls.Add(btnLoadByFilter, 2, 0);

            // Hàng 3: Bảng ListView hiển thị chi tiết
            lvObjects = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                Font = CreateFont(8.75F),
                MultiSelect = true,
                HideSelection = false
            };
            lvObjects.Columns.Add("STT", 42);
            lvObjects.Columns.Add("Tên / Mã Code", 130);
            lvObjects.Columns.Add("Loại", 92);
            lvObjects.Columns.Add("Dạng", 62);
            lvObjects.Columns.Add("Dài 2D (m)", 78);
            lvObjects.Columns.Add("Min Z", 66);
            lvObjects.Columns.Add("Max Z", 66);
            lvObjects.Columns.Add("Layer / Nguồn", 125);
            lvObjects.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Delete)
                {
                    RemoveSelectedItemsInList();
                    e.Handled = true;
                }
            };

            // Hàng 4: Tổng kết
            lblSummary = new WinFormsLabel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                Padding = new Padding(0, 6, 0, 2),
                Font = CreateFont(8.75F, FontStyle.Regular),
                ForeColor = WinFormsColor.FromArgb(40, 60, 85),
                Text = "Đã chọn: 0 đối tượng (0 đường hở, 0 đường kín) | Tổng chiều dài: 0.00 m",
                UseMnemonic = false
            };

            // Hàng 5: Checkbox AutoClose
            chkAutoClose = new CheckBox
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                Padding = new Padding(0, 2, 0, 0),
                Text = "Tự động coi đường gần kín (khoảng hở đầu-cuối ≤ 0.10 m) là đường khép kín",
                Font = CreateFont(8.75F),
                Checked = _lastAutoClose,
                UseMnemonic = false
            };
            chkAutoClose.CheckedChanged += (s, e) => RefreshItemsClosedState();

            tlpLeft.Controls.Add(flpPickBar, 0, 0);
            tlpLeft.Controls.Add(tlpQuickFilter, 0, 1);
            tlpLeft.Controls.Add(lvObjects, 0, 2);
            tlpLeft.Controls.Add(lblSummary, 0, 3);
            tlpLeft.Controls.Add(chkAutoClose, 0, 4);

            grpSource.Controls.Add(tlpLeft);
            tlpMain.Controls.Add(grpSource, 0, 0);

            // ───────────────────────────────────────────────────────────────
            //  CỘT PHẢI: CẤU HÌNH MẶT PHẲNG ĐÍCH, BIÊN BAO & LƯỚI TAM GIÁC
            // ───────────────────────────────────────────────────────────────
            Panel pnlRightScroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(4, 0, 4, 0)
            };

            TableLayoutPanel tlpRightStack = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 1,
                RowCount = 3
            };
            tlpRightStack.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(SizeType.Percent, 100F));
            tlpRightStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tlpRightStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tlpRightStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            GroupBox grpTarget = BuildRightTargetGroup();
            GroupBox grpBoundary = BuildRightBoundaryGroup();
            GroupBox grpMeshParams = BuildRightMeshParamsGroup();

            tlpRightStack.Controls.Add(grpTarget, 0, 0);
            tlpRightStack.Controls.Add(grpBoundary, 0, 1);
            tlpRightStack.Controls.Add(grpMeshParams, 0, 2);

            pnlRightScroll.Controls.Add(tlpRightStack);
            tlpMain.Controls.Add(pnlRightScroll, 1, 0);

            this.Controls.Add(tlpMain);
            this.Controls.Add(pnlBottom);
            this.Controls.Add(pnlHeader);

            this.AcceptButton = btnExecute;
            this.CancelButton = btnClose;

            this.FormClosing += (s, e) =>
            {
                if (!_completedSuccessfully)
                {
                    SaveCurrentSettings();
                }
            };
            this.ResizeEnd += (s, e) => _lastFormSize = this.Size;
        }
    }
}

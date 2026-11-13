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
        private GroupBox BuildRightTargetGroup()
        {
                // ═══ GROUP 2: MẶT PHẲNG TIN ĐÍCH ═══
                GroupBox grpTarget = new GroupBox
                {
                    Text = "2. Mặt phẳng TIN đích (Tạo mới hoặc Bổ sung vào mặt có sẵn)",
                    Font = CreateFont(9.5F, FontStyle.Bold),
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    Padding = new Padding(12, 10, 12, 10),
                    Margin = new Padding(0, 0, 0, 8)
                };

                TableLayoutPanel tlpTarget = new TableLayoutPanel
                {
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    ColumnCount = 2,
                    RowCount = 7
                };
                tlpTarget.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(SizeType.Percent, 100F));
                tlpTarget.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(SizeType.AutoSize));

                radNewSurface = new RadioButton
                {
                    Text = "🆕 Tạo mặt phẳng TIN mới (1 Surface độc lập):",
                    Font = _fontBold9,
                    AutoSize = true,
                    Checked = true,
                    UseMnemonic = false,
                    Margin = new Padding(0, 2, 0, 4)
                };
                radNewSurface.CheckedChanged += (s, e) => UpdateControlStates();

                txtSurfaceName = new WinFormsTextBox
                {
                    Dock = DockStyle.Fill,
                    Font = CreateFont(9.25F),
                    Margin = new Padding(0, 4, 8, 6)
                };

                btnGenName = CreateActionButton("🔄 Tạo tên", 98, 40, WinFormsColor.FromArgb(232, 236, 241), WinFormsColor.FromArgb(45, 55, 70), false);
                btnGenName.Margin = new Padding(0, 0, 0, 4);
                btnGenName.Click += (s, e) => GenerateDefaultSurfaceName();

                radExistingSurface = new RadioButton
                {
                    Text = "➕ Bổ sung mặt phẳng từ Feature Line vào Mặt phẳng có sẵn:",
                    Font = _fontRegular9,
                    AutoSize = true,
                    UseMnemonic = false,
                    Margin = new Padding(0, 4, 0, 4)
                };
                radExistingSurface.CheckedChanged += (s, e) => UpdateControlStates();

                cboExistingSurfaces = new WinFormsComboBox
                {
                    Dock = DockStyle.Fill,
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Font = _fontRegular9,
                    Margin = new Padding(0, 6, 8, 4)
                };
                cboExistingSurfaces.SelectedIndexChanged += (s, e) => OnSelectedExistingSurfaceChanged();

                btnPickExistingSurface = CreateActionButton("🎯 Chọn", 98, 40, WinFormsColor.FromArgb(224, 236, 252), WinFormsColor.FromArgb(15, 68, 130), false);
                btnPickExistingSurface.Margin = new Padding(0, 0, 0, 2);
                btnPickExistingSurface.Click += BtnPickExistingSurface_Click;
                _toolTip.SetToolTip(btnPickExistingSurface, "Chọn trực tiếp một TIN Surface có sẵn trên màn hình CAD để bổ sung thêm vùng mặt phẳng mới");

                // Khung tùy chọn chuyên sâu khi Bổ sung vào mặt phẳng có sẵn
                pnlExistingOptions = new Panel
                {
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    BackColor = WinFormsColor.FromArgb(236, 245, 240),
                    Padding = new Padding(10, 6, 8, 6),
                    Margin = new Padding(0, 2, 0, 6)
                };

                TableLayoutPanel tlpExistingOpts = new TableLayoutPanel
                {
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    ColumnCount = 1,
                    RowCount = 5
                };
                tlpExistingOpts.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(SizeType.Percent, 100F));

                lblExistingSurfInfo = new WinFormsLabel
                {
                    Text = "📊 Mặt phẳng đích: (Chưa chọn)",
                    Font = CreateFont(8.25F, FontStyle.Italic),
                    ForeColor = WinFormsColor.FromArgb(24, 95, 60),
                    AutoSize = true,
                    MaximumSize = new Size(415, 0),
                    UseMnemonic = false,
                    Margin = new Padding(0, 0, 0, 4)
                };

                chkCreateManagedSubSurface = new CheckBox
                {
                    Text = "Tạo mặt phẳng phụ có tiền tố tên mặt phẳng gốc & Paste động (dễ quản lý)",
                    Font = CreateFont(8.5F, FontStyle.Bold),
                    ForeColor = WinFormsColor.FromArgb(15, 68, 130),
                    AutoSize = true,
                    Checked = _lastUseManagedSubSurface,
                    UseMnemonic = false,
                    Margin = new Padding(0, 0, 0, 4)
                };
                chkCreateManagedSubSurface.CheckedChanged += (s, e) =>
                {
                    pnlSubSurfaceConfig.Enabled = chkCreateManagedSubSurface.Checked;
                    UpdateSubSurfaceNamePreview();
                };
                _toolTip.SetToolTip(
                    chkCreateManagedSubSurface,
                    "• Tích chọn (Khuyên dùng): Tạo 1 mặt phẳng phụ mang tiền tố '<TênMặtPhẳngCóSẵn>_' rồi Paste động vào mặt phẳng có sẵn.\n" +
                    "  Trong Toolspace -> Prospector -> Surfaces, các mặt phẳng phụ nằm ngay bên dưới mặt phẳng gốc giúp bật/tắt, sửa hoặc xóa từng vùng cực dễ.\n" +
                    "• Bỏ tích: Gộp trực tiếp vào mặt phẳng có sẵn (0 mặt phẳng phụ).");

                pnlSubSurfaceConfig = new Panel
                {
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    BackColor = WinFormsColor.FromArgb(246, 250, 255),
                    Padding = new Padding(8, 6, 6, 6),
                    Margin = new Padding(0, 0, 0, 5)
                };

                TableLayoutPanel tlpSubConfig = new TableLayoutPanel
                {
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    ColumnCount = 1,
                    RowCount = 3
                };
                tlpSubConfig.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(SizeType.Percent, 100F));

                // Dòng A: Nhãn tiền tố '<TênMặtPhẳngCóSẵn>_' + Ô nhập hậu tố + Nút gợi ý số thứ tự mới
                TableLayoutPanel tlpSubNameRow = new TableLayoutPanel
                {
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    ColumnCount = 3,
                    RowCount = 1,
                    Margin = new Padding(0, 0, 0, 2)
                };
                tlpSubNameRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(SizeType.AutoSize));
                tlpSubNameRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(SizeType.Percent, 100F));
                tlpSubNameRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(SizeType.AutoSize));

                lblSubPrefix = new WinFormsLabel
                {
                    Text = "<TênGốc>_",
                    Font = CreateFont(8.75F, FontStyle.Bold),
                    BackColor = WinFormsColor.FromArgb(216, 232, 252),
                    ForeColor = WinFormsColor.FromArgb(14, 62, 118),
                    AutoSize = true,
                    MaximumSize = new Size(175, 28),
                    AutoEllipsis = true,
                    Padding = new Padding(6, 5, 6, 5),
                    Margin = new Padding(0, 5, 4, 2),
                    UseMnemonic = false
                };

                txtSubSurfaceSuffix = new WinFormsTextBox
                {
                    Dock = DockStyle.Fill,
                    Font = CreateFont(9F),
                    Text = "Phu_1",
                    Margin = new Padding(0, 6, 6, 2)
                };
                txtSubSurfaceSuffix.TextChanged += (s, e) =>
                {
                    if (!_suppressSubSuffixTracking)
                    {
                        string parent = GetEffectiveParentSurfaceName();
                        _userCustomizedSubSuffix = !CTT_Tao_Mat_Phang_Tu_Featureline_Command.IsDefaultAutoSubSuffix(parent, txtSubSurfaceSuffix.Text);
                    }
                    UpdateSubSurfaceNamePreview();
                };
                _toolTip.SetToolTip(
                    txtSubSurfaceSuffix,
                    "Nhập hậu tố mô tả vùng bổ sung (ví dụ: Phu_1, ViaHe_Trai, NutGiao_Km2, Taluy1...).\n" +
                    "Hệ thống tự động gắn tiền tố '<TênMặtPhẳngCóSẵn>_' ở phía trước để nhóm gọn dưới mặt phẳng gốc.");

                btnNextSubSuffix = CreateActionButton("🔄 Số mới", 90, 40, WinFormsColor.FromArgb(224, 236, 252), WinFormsColor.FromArgb(15, 68, 130), false);
                btnNextSubSuffix.Margin = new Padding(0);
                btnNextSubSuffix.Click += (s, e) =>
                {
                    _userCustomizedSubSuffix = false;
                    AssignNextAvailableSubSuffix();
                };
                _toolTip.SetToolTip(btnNextSubSuffix, "Tự động đặt hậu tố tiếp theo chưa tồn tại (Phu_1, Phu_2, Phu_3...) cho mặt phẳng có sẵn đang chọn");

                tlpSubNameRow.Controls.Add(lblSubPrefix, 0, 0);
                tlpSubNameRow.Controls.Add(txtSubSurfaceSuffix, 1, 0);
                tlpSubNameRow.Controls.Add(btnNextSubSuffix, 2, 0);

                // Dòng B: Xem trước trực tiếp tên đầy đủ của mặt phẳng phụ
                lblSubFullNamePreview = new WinFormsLabel
                {
                    Text = "📌 Tên mặt phẳng phụ: ''",
                    Font = CreateFont(8.25F, FontStyle.Bold),
                    ForeColor = WinFormsColor.FromArgb(18, 92, 52),
                    AutoSize = true,
                    MaximumSize = new Size(405, 0),
                    UseMnemonic = false,
                    Margin = new Padding(0, 2, 0, 4)
                };

                // Dòng C: Chọn Surface Style của mặt phẳng phụ (mặc định '_No Display')
                TableLayoutPanel tlpSubStyleRow = new TableLayoutPanel
                {
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    ColumnCount = 2,
                    RowCount = 1,
                    Margin = new Padding(0)
                };
                tlpSubStyleRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(SizeType.AutoSize));
                tlpSubStyleRow.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(SizeType.Percent, 100F));

                WinFormsLabel lblSubStyle = new WinFormsLabel
                {
                    Text = "Style mặt phẳng phụ:",
                    Font = CreateFont(8.5F, FontStyle.Regular),
                    AutoSize = true,
                    Anchor = AnchorStyles.Left,
                    UseMnemonic = false,
                    Margin = new Padding(0, 4, 8, 0)
                };

                cboSubSurfaceStyle = new WinFormsComboBox
                {
                    Dock = DockStyle.Fill,
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Font = CreateFont(8.75F),
                    Margin = new Padding(0, 2, 0, 0)
                };
                _toolTip.SetToolTip(
                    cboSubSurfaceStyle,
                    "Khuyên dùng '_No Display' để mặt phẳng phụ ẩn trên màn hình CAD (tránh vẽ trùng đường đồng mức với mặt phẳng chính) nhưng vẫn quản lý đầy đủ trong Prospector.");

                tlpSubStyleRow.Controls.Add(lblSubStyle, 0, 0);
                tlpSubStyleRow.Controls.Add(cboSubSurfaceStyle, 1, 0);

                tlpSubConfig.Controls.Add(tlpSubNameRow, 0, 0);
                tlpSubConfig.Controls.Add(lblSubFullNamePreview, 0, 1);
                tlpSubConfig.Controls.Add(tlpSubStyleRow, 0, 2);
                pnlSubSurfaceConfig.Controls.Add(tlpSubConfig);

                chkOverwriteOverlap = new CheckBox
                {
                    Text = "Đắp đè cao độ mới tại vùng chồng lấn (bỏ tích = giữ mặt cũ)",
                    Font = CreateFont(8.5F, FontStyle.Regular),
                    AutoSize = true,
                    Checked = _lastOverwriteOverlap,
                    UseMnemonic = false,
                    Margin = new Padding(0, 0, 0, 2)
                };
                _toolTip.SetToolTip(
                    chkOverwriteOverlap,
                    "• Tích chọn (Khuyên dùng): Tại vùng giao chồng, cao độ của mảng Feature Line mới sẽ đắp lên mặt phẳng cũ.\n" +
                    "• Bỏ tích: Giữ nguyên 100% cao độ mặt phẳng cũ tại nơi giao chồng, chỉ nối mở rộng thêm phần diện tích mới nằm ngoài mặt phẳng cũ.");

                chkSafeCorridorCombine = new CheckBox
                {
                    Text = "🛡️ Tạo riêng mặt gộp '<Tên>_Gop' thay vì Paste trực tiếp vào Corridor Surface",
                    Font = CreateFont(8.5F, FontStyle.Bold),
                    ForeColor = WinFormsColor.FromArgb(145, 72, 0),
                    AutoSize = true,
                    Checked = _lastSafeCorridorCombine,
                    Visible = false,
                    UseMnemonic = false,
                    Margin = new Padding(0, 2, 0, 0)
                };
                chkSafeCorridorCombine.CheckedChanged += (s, e) => OnSelectedExistingSurfaceChanged();
                _toolTip.SetToolTip(
                    chkSafeCorridorCombine,
                    "• Bỏ tích (Mặc định): Tạo mặt phẳng phụ '<Tên>_Phu_1' và Paste trực tiếp vào mặt phẳng đang chọn (tiện khi SampleLine đang cắt trực tiếp mặt này).\n" +
                    "• Tích chọn: Tạo/cập nhật mặt phẳng tổng hợp '<Tên>_Gop' kế thừa động mặt Corridor gốc + Paste mặt phẳng phụ vào đó.");

                tlpExistingOpts.Controls.Add(lblExistingSurfInfo, 0, 0);
                tlpExistingOpts.Controls.Add(chkCreateManagedSubSurface, 0, 1);
                tlpExistingOpts.Controls.Add(pnlSubSurfaceConfig, 0, 2);
                tlpExistingOpts.Controls.Add(chkOverwriteOverlap, 0, 3);
                tlpExistingOpts.Controls.Add(chkSafeCorridorCombine, 0, 4);
                pnlExistingOptions.Controls.Add(tlpExistingOpts);

                WinFormsLabel lblStyle = new WinFormsLabel
                {
                    Text = "Surface Style hiển thị (khi tạo mới):",
                    Font = _fontRegular9,
                    AutoSize = true,
                    UseMnemonic = false,
                    Margin = new Padding(0, 4, 0, 4)
                };

                cboMasterStyles = new WinFormsComboBox
                {
                    Dock = DockStyle.Fill,
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Font = _fontRegular9,
                    Margin = new Padding(0, 0, 0, 6)
                };

                chkRebuildAuto = new CheckBox
                {
                    Text = "Tự động cập nhật lưới & ranh giới mặt phẳng tổng khi chỉnh Feature Line (Auto-Rebuild)",
                    Font = CreateFont(8.5F),
                    AutoSize = true,
                    Checked = _lastRebuildAuto,
                    UseMnemonic = false,
                    Margin = new Padding(0, 2, 0, 0)
                };

                tlpTarget.Controls.Add(radNewSurface, 0, 0);
                tlpTarget.SetColumnSpan(radNewSurface, 2);
                tlpTarget.Controls.Add(txtSurfaceName, 0, 1);
                tlpTarget.Controls.Add(btnGenName, 1, 1);

                tlpTarget.Controls.Add(radExistingSurface, 0, 2);
                tlpTarget.SetColumnSpan(radExistingSurface, 2);
                tlpTarget.Controls.Add(cboExistingSurfaces, 0, 3);
                tlpTarget.Controls.Add(btnPickExistingSurface, 1, 3);
                tlpTarget.Controls.Add(pnlExistingOptions, 0, 4);
                tlpTarget.SetColumnSpan(pnlExistingOptions, 2);

                tlpTarget.Controls.Add(lblStyle, 0, 5);
                tlpTarget.SetColumnSpan(lblStyle, 2);
                tlpTarget.Controls.Add(cboMasterStyles, 0, 6);
                tlpTarget.SetColumnSpan(cboMasterStyles, 2);

                TableLayoutPanel tlpTargetOuter = new TableLayoutPanel
                {
                    Dock = DockStyle.Top,
                    AutoSize = true,
                    ColumnCount = 1,
                    RowCount = 2
                };
                tlpTargetOuter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(SizeType.Percent, 100F));
                tlpTargetOuter.Controls.Add(tlpTarget, 0, 0);
                tlpTargetOuter.Controls.Add(chkRebuildAuto, 0, 1);
                grpTarget.Controls.Add(tlpTargetOuter);
            return grpTarget;
        }
    }
}

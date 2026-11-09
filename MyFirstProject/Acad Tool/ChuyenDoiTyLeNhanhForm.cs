// (C) Copyright 2026 by T27
// Giao diện Form Chuyển Đổi Tỷ Lệ Nhanh (1:10 -> 1:10000) cho bản vẽ AutoCAD / Civil 3D
//
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Color = System.Drawing.Color;
using FlowDirection = System.Windows.Forms.FlowDirection;
using Font = System.Drawing.Font;
using WinFormsButton = System.Windows.Forms.Button;
using WinFormsCheckBox = System.Windows.Forms.CheckBox;
using WinFormsComboBox = System.Windows.Forms.ComboBox;
using WinFormsLabel = System.Windows.Forms.Label;

namespace MyFirstProject.Acad_Tool
{
    public class ChuyenDoiTyLeNhanhForm : Form
    {
        // ─── Danh sách 15 tỷ lệ tiêu chuẩn ───
        private static readonly int[] StandardScales = new[]
        {
            10, 20, 25, 50, 75,
            100, 200, 250, 500, 750,
            1000, 2000, 2500, 5000, 10000
        };

        // ─── Persistent State (Ghi nhớ thông số giữa các lần chạy) ───
        private static int _lastTargetScale = 1000;
        private static int _lastSourceScaleIndex = 0; // 0 = Tự động nhận diện
        private static decimal _lastBaseTextHeight = 2.0m;
        private static bool _lastSetDimStyle = true;
        private static bool _lastSetTextSize = true;
        private static bool _lastSetLtScale = true;
        private static bool _lastSetMLeaderScale = true;
        private static bool _lastConvertDim = true;
        private static bool _lastConvertText = true;
        private static bool _lastConvertBlock = true;
        private static bool _lastConvertHatchAndLt = true;
        private static List<ObjectId> _lastSelectedIds = new();
        private static Size _lastFormSize = new(780, 520);

        private readonly Document _doc;
        private readonly List<ObjectId> _currentSelectedIds = new();

        // ─── Fonts (GDI+ resources) ───
        private Font _fontTitle = null!;
        private Font _fontBold = null!;
        private Font _fontRegular = null!;
        private Font _fontCombo = null!;
        private ToolTip _toolTip = null!;

        // ─── Controls ───
        private WinFormsComboBox _cboTargetScale = null!;
        private NumericUpDown _numBaseTextHeight = null!;
        private WinFormsComboBox _cboSourceScale = null!;
        private WinFormsLabel _lblComputedInfo = null!;
        private WinFormsLabel _lblSelectionInfo = null!;

        private WinFormsCheckBox _chkSetDimStyle = null!;
        private WinFormsCheckBox _chkSetTextSize = null!;
        private WinFormsCheckBox _chkSetLtScale = null!;
        private WinFormsCheckBox _chkSetMLeaderScale = null!;

        private WinFormsCheckBox _chkConvertDim = null!;
        private WinFormsCheckBox _chkConvertText = null!;
        private WinFormsCheckBox _chkConvertBlock = null!;
        private WinFormsCheckBox _chkConvertHatch = null!;

        private WinFormsButton _btnPickObjects = null!;
        private WinFormsButton _btnClearSelection = null!;
        private WinFormsButton _btnApplyEnvOnly = null!;
        private WinFormsButton _btnApplyAll = null!;
        private WinFormsButton _btnClose = null!;

        public ChuyenDoiTyLeNhanhForm(Document doc, IEnumerable<ObjectId>? preSelectedIds = null)
        {
            _doc = doc;
            InitializeComponent();
            RestoreLastSettings(preSelectedIds);
            UpdateComputedPreview();
            UpdateSelectionLabel();
        }

        private void InitializeComponent()
        {
            _fontTitle = new Font("Segoe UI", 11F, FontStyle.Bold);
            _fontBold = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            _fontRegular = new Font("Segoe UI", 9.5F, FontStyle.Regular);
            _fontCombo = new Font("Segoe UI", 10.5F, FontStyle.Bold);
            _toolTip = new ToolTip { AutoPopDelay = 8000, InitialDelay = 300, ReshowDelay = 200 };

            if (_lastFormSize.Width < 760 || _lastFormSize.Height < 500 || _lastFormSize.Height > 640)
            {
                _lastFormSize = new Size(780, 520);
            }

            Text = "AT_Chuyen_Doi_Ty_Le_Nhanh - Chuyển Đổi Tỷ Lệ Nhanh Bản Vẽ (1:10 → 1:10000)";
            Size = _lastFormSize;
            MinimumSize = new Size(760, 500);
            StartPosition = FormStartPosition.CenterScreen;
            Font = _fontRegular;
            BackColor = Color.FromArgb(245, 247, 250);
            KeyPreview = true;

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(12)
            };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));  // Row 0: Header
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 112F)); // Row 1: Group 1 (Chọn tỷ lệ bằng danh sách sổ xuống)
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 112F)); // Row 2: Group 2 (Đồng bộ môi trường vẽ)
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));  // Row 3: Group 3 (Quy đổi đối tượng)
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));  // Row 4: Thanh nút lệnh dưới cùng

            // ─── 1. Header Banner ───
            var pnlHeader = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(25, 55, 109),
                Padding = new Padding(12, 6, 12, 6),
                Margin = new Padding(0, 0, 0, 6)
            };
            var lblHeader = new WinFormsLabel
            {
                Text = "📐 CHUYỂN ĐỔI TỶ LỆ NHANH BẢN VẼ & ĐỐI TƯỢNG (CHUẨN GỐC 1:1000)",
                ForeColor = Color.White,
                Font = _fontTitle,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            pnlHeader.Controls.Add(lblHeader);
            mainLayout.Controls.Add(pnlHeader, 0, 0);

            // ─── 2. GroupBox Chọn Tỷ Lệ Đích Bằng Danh Sách Sổ Xuống (ComboBox) ───
            var grpScale = new GroupBox
            {
                Text = "1. Chọn Tỷ Lệ Bản Vẽ Đích",
                Font = _fontBold,
                Dock = DockStyle.Fill,
                Padding = new Padding(12, 8, 12, 6),
                Margin = new Padding(0, 0, 0, 6)
            };

            var grpScaleLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            grpScaleLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            grpScaleLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F)); // Hàng chọn ComboBox tỷ lệ & cỡ chữ gốc
            grpScaleLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // Dòng hiển thị kết quả tính toán

            var flpScaleInputs = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 2, 0, 0),
                Margin = new Padding(0)
            };

            var lblTarget = new WinFormsLabel
            {
                Text = "Tỷ lệ bản vẽ đích:",
                Font = _fontBold,
                AutoSize = true,
                Margin = new Padding(0, 7, 8, 0)
            };

            _cboTargetScale = new WinFormsComboBox
            {
                Font = _fontCombo,
                Width = 220,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 2, 32, 0)
            };
            foreach (int sc in StandardScales)
            {
                _cboTargetScale.Items.Add($"1 : {sc}   (DimStyle: 1-{sc})");
            }
            _cboTargetScale.SelectedIndex = Array.IndexOf(StandardScales, 1000);
            _cboTargetScale.SelectedIndexChanged += (s, e) =>
            {
                UpdateComputedPreview();
                UpdateSelectionLabel();
            };
            _toolTip.SetToolTip(_cboTargetScale, "Chọn tỷ lệ bản vẽ từ danh sách 15 tỷ lệ tiêu chuẩn (1:10 đến 1:10000)");

            var lblBaseTxt = new WinFormsLabel
            {
                Text = "Cỡ chữ chuẩn ở 1:1000 (mm):",
                Font = _fontRegular,
                AutoSize = true,
                Margin = new Padding(0, 7, 8, 0)
            };
            _numBaseTextHeight = new NumericUpDown
            {
                Font = _fontBold,
                Width = 85,
                Minimum = 0.5m,
                Maximum = 20.0m,
                DecimalPlaces = 1,
                Increment = 0.5m,
                Value = 2.0m,
                Margin = new Padding(0, 4, 0, 0)
            };
            _numBaseTextHeight.ValueChanged += (s, e) => UpdateComputedPreview();

            flpScaleInputs.Controls.Add(lblTarget);
            flpScaleInputs.Controls.Add(_cboTargetScale);
            flpScaleInputs.Controls.Add(lblBaseTxt);
            flpScaleInputs.Controls.Add(_numBaseTextHeight);

            _lblComputedInfo = new WinFormsLabel
            {
                Text = "➜ DimStyle: [1-1000] | Hệ số k = 1.000 | Chữ thường = 2.00 | Chữ tiêu đề = 3.50 | CELTSCALE / HPSCALE = 1.000",
                Font = _fontBold,
                ForeColor = Color.FromArgb(0, 102, 51),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(2, 2, 2, 0)
            };

            grpScaleLayout.Controls.Add(flpScaleInputs, 0, 0);
            grpScaleLayout.Controls.Add(_lblComputedInfo, 0, 1);
            grpScale.Controls.Add(grpScaleLayout);
            mainLayout.Controls.Add(grpScale, 0, 1);

            // ─── 3. GroupBox Thiết Lập Môi Trường Vẽ Hiện Hành ───
            var grpEnv = new GroupBox
            {
                Text = "2. Đồng Bộ Môi Trường Vẽ Mới Trên Bản Vẽ",
                Font = _fontBold,
                Dock = DockStyle.Fill,
                Padding = new Padding(12, 6, 12, 6),
                Margin = new Padding(0, 0, 0, 6)
            };

            var envGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2
            };
            envGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54F));
            envGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46F));
            envGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            envGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

            _chkSetDimStyle = new WinFormsCheckBox
            {
                Text = "Đặt DimStyle hiện hành = 1-N",
                Font = _fontRegular,
                Dock = DockStyle.Fill,
                Checked = true
            };
            _chkSetLtScale = new WinFormsCheckBox
            {
                Text = "Đồng bộ nét đứt & Hatch mới (CELTSCALE & HPSCALE = k)",
                Font = _fontRegular,
                Dock = DockStyle.Fill,
                Checked = true
            };
            _chkSetTextSize = new WinFormsCheckBox
            {
                Text = "Đặt chiều cao chữ mặc định (TEXTSIZE = Cỡ chữ × k)",
                Font = _fontRegular,
                Dock = DockStyle.Fill,
                Checked = true
            };
            _chkSetMLeaderScale = new WinFormsCheckBox
            {
                Text = "Đồng bộ tỷ lệ MLeader (MLEADERSCALE & Style = k)",
                Font = _fontRegular,
                Dock = DockStyle.Fill,
                Checked = true
            };

            envGrid.Controls.Add(_chkSetDimStyle, 0, 0);
            envGrid.Controls.Add(_chkSetLtScale, 1, 0);
            envGrid.Controls.Add(_chkSetTextSize, 0, 1);
            envGrid.Controls.Add(_chkSetMLeaderScale, 1, 1);
            grpEnv.Controls.Add(envGrid);
            mainLayout.Controls.Add(grpEnv, 0, 2);

            // ─── 4. GroupBox Quy Đổi Vùng Đối Tượng Đã Vẽ ───
            var grpConvert = new GroupBox
            {
                Text = "3. Quy Đổi Tỷ Lệ Cho Các Đối Tượng Đã Chọn (Giữ nguyên kích thước hình học 1:1)",
                Font = _fontBold,
                Dock = DockStyle.Fill,
                Padding = new Padding(12, 8, 12, 8),
                Margin = new Padding(0, 0, 0, 6)
            };

            var convertLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3
            };
            convertLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            convertLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            convertLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            convertLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var flpPickBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Margin = new Padding(0)
            };

            _btnPickObjects = new WinFormsButton
            {
                Text = "🔍 Chọn đối tượng trên bản vẽ...",
                Font = _fontBold,
                Size = new Size(250, 34),
                BackColor = Color.FromArgb(228, 238, 252),
                FlatStyle = FlatStyle.Flat,
                UseMnemonic = false,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 2, 8, 0)
            };
            _btnPickObjects.Click += BtnPickObjects_Click;

            _btnClearSelection = new WinFormsButton
            {
                Text = "✖ Bỏ chọn",
                Font = _fontRegular,
                Size = new Size(95, 34),
                FlatStyle = FlatStyle.Flat,
                UseMnemonic = false,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 2, 18, 0)
            };
            _btnClearSelection.Click += (s, e) =>
            {
                _currentSelectedIds.Clear();
                UpdateSelectionLabel();
            };

            var lblSourceScale = new WinFormsLabel
            {
                Text = "Tỷ lệ nguồn:",
                Font = _fontRegular,
                AutoSize = true,
                Margin = new Padding(0, 9, 6, 0)
            };
            _cboSourceScale = new WinFormsComboBox
            {
                Font = _fontRegular,
                Width = 245,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 5, 0, 0)
            };
            _cboSourceScale.Items.Add("Tự động nhận diện từ đối tượng");
            foreach (int sc in StandardScales)
            {
                _cboSourceScale.Items.Add($"Từ tỷ lệ 1 : {sc}");
            }
            _cboSourceScale.SelectedIndex = 0;

            flpPickBar.Controls.Add(_btnPickObjects);
            flpPickBar.Controls.Add(_btnClearSelection);
            flpPickBar.Controls.Add(lblSourceScale);
            flpPickBar.Controls.Add(_cboSourceScale);

            _lblSelectionInfo = new WinFormsLabel
            {
                Text = "Chưa chọn đối tượng nào (Nhấn nút 'Chọn đối tượng' hoặc chỉ áp dụng môi trường vẽ ở Mục 2).",
                Font = _fontRegular,
                ForeColor = Color.FromArgb(90, 90, 90),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var chkConvertGrid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 2
            };
            chkConvertGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54F));
            chkConvertGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46F));
            chkConvertGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            chkConvertGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

            _chkConvertDim = new WinFormsCheckBox
            {
                Text = "Chuyển Dimension / Leader / MLeader sang [1-N]",
                Font = _fontRegular,
                Dock = DockStyle.Fill,
                Checked = true
            };
            _chkConvertBlock = new WinFormsCheckBox
            {
                Text = "Quy đổi Scale các Block ký hiệu & chữ ATTRIB",
                Font = _fontRegular,
                Dock = DockStyle.Fill,
                Checked = true
            };
            _chkConvertText = new WinFormsCheckBox
            {
                Text = "Quy đổi chiều cao Text / MText (giữ vị trí căn lề)",
                Font = _fontRegular,
                Dock = DockStyle.Fill,
                Checked = true
            };
            _chkConvertHatch = new WinFormsCheckBox
            {
                Text = "Nhân tỷ lệ mẫu Hatch & nét đứt (PatternScale / LinetypeScale)",
                Font = _fontRegular,
                Dock = DockStyle.Fill,
                Checked = true
            };

            chkConvertGrid.Controls.Add(_chkConvertDim, 0, 0);
            chkConvertGrid.Controls.Add(_chkConvertBlock, 1, 0);
            chkConvertGrid.Controls.Add(_chkConvertText, 0, 1);
            chkConvertGrid.Controls.Add(_chkConvertHatch, 1, 1);

            convertLayout.Controls.Add(flpPickBar, 0, 0);
            convertLayout.Controls.Add(_lblSelectionInfo, 0, 1);
            convertLayout.Controls.Add(chkConvertGrid, 0, 2);
            grpConvert.Controls.Add(convertLayout);
            mainLayout.Controls.Add(grpConvert, 0, 3);

            // ─── 5. Bottom Action Bar ───
            var bottomLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Margin = new Padding(0)
            };
            bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            bottomLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _btnApplyEnvOnly = new WinFormsButton
            {
                Text = "⚡ Chỉ Đặt Môi Trường Vẽ",
                Font = _fontBold,
                Size = new Size(225, 42),
                BackColor = Color.FromArgb(45, 110, 180),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                UseMnemonic = false,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 4, 12, 0)
            };
            _btnApplyEnvOnly.Click += (s, e) => ExecuteScaleConversion(convertObjects: false);

            _btnApplyAll = new WinFormsButton
            {
                Text = "✔ Áp Dụng & Quy Đổi Đối Tượng",
                Font = _fontBold,
                Size = new Size(320, 42),
                BackColor = Color.FromArgb(22, 130, 65),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                UseMnemonic = false,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 4, 0, 0)
            };
            _btnApplyAll.Click += (s, e) => ExecuteScaleConversion(convertObjects: true);

            _btnClose = new WinFormsButton
            {
                Text = "Đóng (ESC)",
                Font = _fontRegular,
                Size = new Size(125, 42),
                FlatStyle = FlatStyle.Flat,
                UseMnemonic = false,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 4, 0, 0)
            };
            _btnClose.Click += (s, e) => Close();

            AcceptButton = _btnApplyAll;
            CancelButton = _btnClose;

            bottomLayout.Controls.Add(_btnApplyEnvOnly, 0, 0);
            bottomLayout.Controls.Add(_btnApplyAll, 1, 0);
            bottomLayout.Controls.Add(_btnClose, 2, 0);
            mainLayout.Controls.Add(bottomLayout, 0, 4);

            Controls.Add(mainLayout);
        }

        private int GetSelectedTargetScale()
        {
            int idx = _cboTargetScale.SelectedIndex;
            if (idx >= 0 && idx < StandardScales.Length)
                return StandardScales[idx];
            return 1000;
        }

        private void SetSelectedTargetScale(int scaleVal)
        {
            int idx = Array.IndexOf(StandardScales, scaleVal);
            if (idx < 0)
                idx = Array.IndexOf(StandardScales, 1000);
            _cboTargetScale.SelectedIndex = idx;
        }

        private void UpdateComputedPreview()
        {
            int scaleN = GetSelectedTargetScale();
            double k = scaleN / 1000.0;
            double baseH = (double)_numBaseTextHeight.Value;
            double normalTxt = baseH * k;
            double titleTxt = (baseH * 1.75) * k;

            _lblComputedInfo.Text = string.Format(
                CultureInfo.InvariantCulture,
                "➜ DimStyle: [1-{0}] | Hệ số k = {1:0.###} | Chữ thường = {2:0.###} | Chữ tiêu đề = {3:0.###} | CELTSCALE / HPSCALE = {1:0.###}",
                scaleN, k, normalTxt, titleTxt);
        }

        private void UpdateSelectionLabel()
        {
            int validCount = 0;
            foreach (ObjectId id in _currentSelectedIds)
            {
                if (!id.IsNull && id.IsValid && !id.IsErased)
                    validCount++;
            }

            if (validCount > 0)
            {
                _lblSelectionInfo.Text = $"✔ Đã chọn {validCount} đối tượng hợp lệ sẵn sàng quy đổi sang tỷ lệ 1:{GetSelectedTargetScale()}.";
                _lblSelectionInfo.ForeColor = Color.FromArgb(0, 110, 50);
            }
            else
            {
                _lblSelectionInfo.Text = "Chưa chọn đối tượng nào (Nhấn nút 'Chọn đối tượng' hoặc chỉ áp dụng môi trường vẽ ở Mục 2).";
                _lblSelectionInfo.ForeColor = Color.FromArgb(100, 100, 100);
            }
        }

        private void BtnPickObjects_Click(object? sender, EventArgs e)
        {
            Editor ed = _doc.Editor;
            try
            {
                using (EditorUserInteraction userInteraction = ed.StartUserInteraction(this))
                {
                    var pso = new PromptSelectionOptions
                    {
                        MessageForAdding = "\nChọn các đối tượng (Dim, Leader, MLeader, Text, MText, Block, Hatch, Nét đứt) cần quy đổi tỷ lệ: "
                    };
                    PromptSelectionResult psr = ed.GetSelection(pso);
                    if (psr.Status == PromptStatus.OK && psr.Value != null)
                    {
                        _currentSelectedIds.Clear();
                        foreach (SelectedObject so in psr.Value)
                        {
                            if (so != null && !so.ObjectId.IsNull && so.ObjectId.IsValid && !so.ObjectId.IsErased)
                            {
                                _currentSelectedIds.Add(so.ObjectId);
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n⚠️ Lỗi chọn đối tượng: {ex.Message}");
            }
            UpdateSelectionLabel();
        }

        private void RestoreLastSettings(IEnumerable<ObjectId>? preSelectedIds)
        {
            SetSelectedTargetScale(_lastTargetScale);
            _numBaseTextHeight.Value = Math.Clamp(_lastBaseTextHeight, 0.5m, 20.0m);
            if (_lastSourceScaleIndex >= 0 && _lastSourceScaleIndex < _cboSourceScale.Items.Count)
                _cboSourceScale.SelectedIndex = _lastSourceScaleIndex;

            _chkSetDimStyle.Checked = _lastSetDimStyle;
            _chkSetTextSize.Checked = _lastSetTextSize;
            _chkSetLtScale.Checked = _lastSetLtScale;
            _chkSetMLeaderScale.Checked = _lastSetMLeaderScale;

            _chkConvertDim.Checked = _lastConvertDim;
            _chkConvertText.Checked = _lastConvertText;
            _chkConvertBlock.Checked = _lastConvertBlock;
            _chkConvertHatch.Checked = _lastConvertHatchAndLt;

            _currentSelectedIds.Clear();
            if (preSelectedIds != null)
            {
                foreach (ObjectId id in preSelectedIds)
                {
                    if (!id.IsNull && id.IsValid && !id.IsErased)
                        _currentSelectedIds.Add(id);
                }
            }

            if (_currentSelectedIds.Count == 0 && _lastSelectedIds.Count > 0)
            {
                foreach (ObjectId id in _lastSelectedIds)
                {
                    if (!id.IsNull && id.IsValid && !id.IsErased && id.Database == _doc.Database)
                        _currentSelectedIds.Add(id);
                }
            }
        }

        private void SaveCurrentSettings()
        {
            _lastTargetScale = GetSelectedTargetScale();
            _lastBaseTextHeight = _numBaseTextHeight.Value;
            _lastSourceScaleIndex = _cboSourceScale.SelectedIndex;

            _lastSetDimStyle = _chkSetDimStyle.Checked;
            _lastSetTextSize = _chkSetTextSize.Checked;
            _lastSetLtScale = _chkSetLtScale.Checked;
            _lastSetMLeaderScale = _chkSetMLeaderScale.Checked;

            _lastConvertDim = _chkConvertDim.Checked;
            _lastConvertText = _chkConvertText.Checked;
            _lastConvertBlock = _chkConvertBlock.Checked;
            _lastConvertHatchAndLt = _chkConvertHatch.Checked;

            _lastSelectedIds = new List<ObjectId>(_currentSelectedIds);
            if (WindowState == FormWindowState.Normal)
                _lastFormSize = Size;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveCurrentSettings();
            _toolTip?.Dispose();
            _fontTitle?.Dispose();
            _fontBold?.Dispose();
            _fontRegular?.Dispose();
            _fontCombo?.Dispose();
            base.OnFormClosing(e);
        }

        private void ExecuteScaleConversion(bool convertObjects)
        {
            SaveCurrentSettings();

            int targetScale = GetSelectedTargetScale();
            double targetFactor = targetScale / 1000.0;
            double baseTextHeight = (double)_numBaseTextHeight.Value;
            string targetDimStyleName = $"1-{targetScale}";

            Database db = _doc.Database;
            Editor ed = _doc.Editor;

            int dimCount = 0;
            int textCount = 0;
            int blockCount = 0;
            int hatchAndLtCount = 0;

            try
            {
                UseWaitCursor = true;
                using (DocumentLock docLock = _doc.LockDocument())
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    // ─── 1. Đảm bảo DimStyle "1-{targetScale}" tồn tại (nhân bản từ "1-1000" nếu chưa có) ───
                    DimStyleTable dst = (DimStyleTable)tr.GetObject(db.DimStyleTableId, OpenMode.ForRead);
                    ObjectId targetDimStyleId = ObjectId.Null;

                    if (dst.Has(targetDimStyleName))
                    {
                        targetDimStyleId = dst[targetDimStyleName];
                        if (tr.GetObject(targetDimStyleId, OpenMode.ForWrite) is DimStyleTableRecord existingDstr)
                        {
                            existingDstr.Dimscale = targetFactor;
                        }
                    }
                    else
                    {
                        dst.UpgradeOpen();
                        DimStyleTableRecord newDstr = new DimStyleTableRecord();
                        if (dst.Has("1-1000") && tr.GetObject(dst["1-1000"], OpenMode.ForRead) is DimStyleTableRecord templateDstr)
                        {
                            newDstr.CopyFrom(templateDstr);
                        }
                        newDstr.Name = targetDimStyleName;
                        newDstr.Dimscale = targetFactor;
                        targetDimStyleId = dst.Add(newDstr);
                        tr.AddNewlyCreatedDBObject(newDstr, true);
                    }

                    // ─── 2. Thiết lập môi trường vẽ mới ───
                    if (_chkSetDimStyle.Checked && !targetDimStyleId.IsNull)
                    {
                        db.Dimstyle = targetDimStyleId;
                        if (tr.GetObject(targetDimStyleId, OpenMode.ForRead) is DimStyleTableRecord activeDstr)
                        {
                            db.SetDimstyleData(activeDstr);
                        }
                    }

                    if (_chkSetTextSize.Checked)
                    {
                        db.Textsize = Math.Max(0.001, baseTextHeight * targetFactor);
                    }

                    if (_chkSetLtScale.Checked)
                    {
                        db.Celtscale = Math.Max(0.001, targetFactor);
                        try
                        {
                            Application.SetSystemVariable("HPSCALE", Math.Max(0.001, targetFactor));
                        }
                        catch { /* Bỏ qua nếu biến không hỗ trợ */ }
                    }

                    if (_chkSetMLeaderScale.Checked)
                    {
                        try
                        {
                            Application.SetSystemVariable("MLEADERSCALE", Math.Max(0.001, targetFactor));
                            if (!db.MLeaderStyleDictionaryId.IsNull &&
                                tr.GetObject(db.MLeaderStyleDictionaryId, OpenMode.ForRead) is DBDictionary mlsDict)
                            {
                                foreach (DBDictionaryEntry entry in mlsDict)
                                {
                                    if (tr.GetObject(entry.Value, OpenMode.ForWrite, false) is MLeaderStyle mls &&
                                        mls.Annotative != AnnotativeStates.True)
                                    {
                                        mls.Scale = Math.Max(0.001, targetFactor);
                                    }
                                }
                            }
                        }
                        catch { /* Bỏ qua nếu biến không hỗ trợ */ }
                    }

                    // ─── 3. Quy đổi các đối tượng đã chọn (nếu bật convertObjects) ───
                    if (convertObjects && _currentSelectedIds.Count > 0)
                    {
                        double? explicitSourceFactor = null;
                        if (_cboSourceScale.SelectedIndex > 0 && _cboSourceScale.SelectedIndex <= StandardScales.Length)
                        {
                            int srcScale = StandardScales[_cboSourceScale.SelectedIndex - 1];
                            explicitSourceFactor = srcScale / 1000.0;
                        }

                        // Tự động phát hiện hệ số tỷ lệ nguồn từ các đối tượng được chọn nếu đang để chế độ Auto
                        double detectedSourceFactor = explicitSourceFactor ?? DetectSourceFactor(tr, _currentSelectedIds, baseTextHeight);
                        double ratio = detectedSourceFactor > 1e-6 ? (targetFactor / detectedSourceFactor) : 1.0;

                        foreach (ObjectId objId in _currentSelectedIds)
                        {
                            if (objId.IsNull || !objId.IsValid || objId.IsErased) continue;
                            if (tr.GetObject(objId, OpenMode.ForWrite, false) is not Entity ent) continue;

                            // 3a. Dimension
                            if (_chkConvertDim.Checked && ent is Dimension dim)
                            {
                                if (!targetDimStyleId.IsNull)
                                {
                                    dim.DimensionStyle = targetDimStyleId;
                                }
                                dim.Dimscale = targetFactor;
                                dimCount++;
                            }
                            // 3b. Leader / MLeader
                            else if (_chkConvertDim.Checked && ent is Leader leader)
                            {
                                if (!targetDimStyleId.IsNull)
                                {
                                    leader.DimensionStyle = targetDimStyleId;
                                }
                                leader.Dimscale = targetFactor;
                                dimCount++;
                            }
                            else if (_chkConvertDim.Checked && ent is MLeader mleader)
                            {
                                mleader.Scale = targetFactor;
                                dimCount++;
                            }
                            // 3c. DBText (giữ nguyên điểm căn lề AlignmentPoint / Position)
                            else if (_chkConvertText.Checked && ent is DBText dbText)
                            {
                                Point3d savedPos = dbText.Position;
                                Point3d savedAlign = dbText.AlignmentPoint;
                                bool hasAlign = dbText.Justify != AttachmentPoint.BaseLeft ||
                                                dbText.HorizontalMode != TextHorizontalMode.TextLeft ||
                                                dbText.VerticalMode != TextVerticalMode.TextBase;

                                if (explicitSourceFactor.HasValue)
                                {
                                    dbText.Height = Math.Max(0.001, dbText.Height * ratio);
                                }
                                else if (string.Equals(dbText.Layer, "02_TEXT_TO", StringComparison.OrdinalIgnoreCase))
                                {
                                    dbText.Height = Math.Max(0.001, baseTextHeight * 1.75 * targetFactor);
                                }
                                else if (string.Equals(dbText.Layer, "01_TEXT", StringComparison.OrdinalIgnoreCase))
                                {
                                    dbText.Height = Math.Max(0.001, baseTextHeight * targetFactor);
                                }
                                else
                                {
                                    dbText.Height = Math.Max(0.001, dbText.Height * ratio);
                                }

                                if (hasAlign && savedAlign != Point3d.Origin)
                                {
                                    dbText.AlignmentPoint = savedAlign;
                                    dbText.AdjustAlignment(db);
                                }
                                else
                                {
                                    dbText.Position = savedPos;
                                }
                                textCount++;
                            }
                            // 3d. MText
                            else if (_chkConvertText.Checked && ent is MText mText)
                            {
                                if (explicitSourceFactor.HasValue)
                                {
                                    mText.TextHeight = Math.Max(0.001, mText.TextHeight * ratio);
                                }
                                else if (string.Equals(mText.Layer, "02_TEXT_TO", StringComparison.OrdinalIgnoreCase))
                                {
                                    mText.TextHeight = Math.Max(0.001, baseTextHeight * 1.75 * targetFactor);
                                }
                                else
                                {
                                    mText.TextHeight = Math.Max(0.001, baseTextHeight * targetFactor);
                                }
                                textCount++;
                            }
                            // 3e. BlockReference & AttributeReference
                            else if (_chkConvertBlock.Checked && ent is BlockReference blkRef)
                            {
                                double signX = blkRef.ScaleFactors.X < 0 ? -1.0 : 1.0;
                                double signY = blkRef.ScaleFactors.Y < 0 ? -1.0 : 1.0;
                                double signZ = blkRef.ScaleFactors.Z < 0 ? -1.0 : 1.0;

                                double newScaleMag = explicitSourceFactor.HasValue
                                    ? Math.Abs(blkRef.ScaleFactors.X) * ratio
                                    : targetFactor;

                                Point3d insPt = blkRef.Position;
                                blkRef.ScaleFactors = new Scale3d(
                                    signX * newScaleMag,
                                    signY * newScaleMag,
                                    signZ * newScaleMag);

                                foreach (ObjectId attId in blkRef.AttributeCollection)
                                {
                                    if (tr.GetObject(attId, OpenMode.ForWrite, false) is AttributeReference attRef)
                                    {
                                        Point3d attPos = attRef.Position;
                                        Point3d attAlign = attRef.AlignmentPoint;
                                        bool attHasAlign = attRef.Justify != AttachmentPoint.BaseLeft ||
                                                           attRef.HorizontalMode != TextHorizontalMode.TextLeft ||
                                                           attRef.VerticalMode != TextVerticalMode.TextBase;

                                        double attFactor = explicitSourceFactor.HasValue
                                            ? ratio
                                            : (targetFactor / Math.Max(1e-6, detectedSourceFactor));

                                        attRef.Height = Math.Max(0.001, attRef.Height * attFactor);
                                        attRef.Position = insPt + (attPos - insPt) * attFactor;
                                        if (attHasAlign && attAlign != Point3d.Origin)
                                        {
                                            attRef.AlignmentPoint = insPt + (attAlign - insPt) * attFactor;
                                            attRef.AdjustAlignment(db);
                                        }
                                    }
                                }
                                blockCount++;
                            }
                            // 3f. Hatch (Nhân PatternScale theo hệ số tỷ lệ ratio để bảo toàn mật độ mẫu gốc như AR-CONC, GRASS...)
                            else if (_chkConvertHatch.Checked && ent is Hatch hatch)
                            {
                                if (!string.Equals(hatch.PatternName, "SOLID", StringComparison.OrdinalIgnoreCase) &&
                                    Math.Abs(ratio - 1.0) > 1e-6)
                                {
                                    double newPatScale = Math.Max(0.0001, hatch.PatternScale * ratio);
                                    hatch.PatternScale = newPatScale;
                                    hatch.EvaluateHatch(true);
                                    hatchAndLtCount++;
                                }
                            }
                            // 3g. Nét đứt của các đường hình học (Line, Polyline, Arc, Circle, Spline...)
                            else if (_chkConvertHatch.Checked && ent is Curve && Math.Abs(ratio - 1.0) > 1e-6)
                            {
                                ent.LinetypeScale = Math.Max(0.0001, ent.LinetypeScale * ratio);
                                hatchAndLtCount++;
                            }
                        }
                    }

                    tr.Commit();
                }

                ed.Regen();
                ed.WriteMessage($"\n✅ Đã thiết lập tỷ lệ 1:{targetScale} (DimStyle = {targetDimStyleName}, k = {targetFactor:0.###}, TEXTSIZE = {(baseTextHeight * targetFactor):0.###}).");
                if (convertObjects && (dimCount + textCount + blockCount + hatchAndLtCount) > 0)
                {
                    ed.WriteMessage($"\n   ➜ Đã quy đổi: {dimCount} Dim/Leader/MLeader, {textCount} Text/MText, {blockCount} Block ký hiệu, {hatchAndLtCount} Hatch/Nét đứt.");
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n❌ Lỗi khi chuyển đổi tỷ lệ: {ex.Message}");
                MessageBox.Show($"Lỗi khi chuyển đổi tỷ lệ:\n{ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                UseWaitCursor = false;
            }
        }

        private static double DetectSourceFactor(Transaction tr, List<ObjectId> ids, double baseTextHeight)
        {
            foreach (ObjectId id in ids)
            {
                if (id.IsNull || !id.IsValid || id.IsErased) continue;
                if (tr.GetObject(id, OpenMode.ForRead) is Dimension dim)
                {
                    if (dim.Dimscale > 1e-6) return dim.Dimscale;
                    if (!dim.DimensionStyle.IsNull &&
                        tr.GetObject(dim.DimensionStyle, OpenMode.ForRead) is DimStyleTableRecord dstr &&
                        dstr.Dimscale > 1e-6)
                    {
                        return dstr.Dimscale;
                    }
                }
                if (tr.GetObject(id, OpenMode.ForRead) is MLeader ml && ml.Scale > 1e-6)
                {
                    return ml.Scale;
                }
            }

            foreach (ObjectId id in ids)
            {
                if (id.IsNull || !id.IsValid || id.IsErased) continue;
                if (tr.GetObject(id, OpenMode.ForRead) is DBText txt && txt.Height > 1e-6)
                {
                    if (string.Equals(txt.Layer, "02_TEXT_TO", StringComparison.OrdinalIgnoreCase))
                        return txt.Height / Math.Max(0.1, baseTextHeight * 1.75);
                    return txt.Height / Math.Max(0.1, baseTextHeight);
                }
                if (tr.GetObject(id, OpenMode.ForRead) is BlockReference blk && Math.Abs(blk.ScaleFactors.X) > 1e-6)
                {
                    return Math.Abs(blk.ScaleFactors.X);
                }
            }

            return 1.0;
        }
    }
}

using System;
using System.Drawing;
using System.Windows.Forms;

using MyFirstProject.Shared.UI;

using ColumnStyle = System.Windows.Forms.ColumnStyle;
using FlowDirection = System.Windows.Forms.FlowDirection;
using Font = System.Drawing.Font;
using Size = System.Drawing.Size;
using WinFormsButton = System.Windows.Forms.Button;
using WinFormsComboBox = System.Windows.Forms.ComboBox;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsTextBox = System.Windows.Forms.TextBox;

namespace MyFirstProject.Commands.CTC.TaoCorridorTheoCauKien
{
    public partial class TaoCorridorTheoCauKienForm
    {
        private static readonly Font EmptyNoticeFont = new Font("Segoe UI", 9.5F, FontStyle.Italic);
        private static readonly Font FooterBoldFont = new Font("Segoe UI", 9.5F, FontStyle.Bold);

        private void BuildLayout()
        {
            Text = "CTC_Tao_Corridor_Theo_Cau_Kien — Xây Dựng & Gộp Corridor Theo Cấu Kiện (Modular BIM)";
            Font = StandardFonts.Regular;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(1050, 620);
            PersistentState.RestoreFormSize(this, _lastFormSize);

            FormClosing += (s, e) => SaveCurrentSettings();

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(12, 8, 12, 10)
            };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            mainLayout.Controls.Add(BuildHeaderBanner(), 0, 0);
            mainLayout.Controls.Add(BuildGeneralGroup(), 0, 1);
            mainLayout.Controls.Add(BuildComponentsGroup(), 0, 2);
            mainLayout.Controls.Add(BuildFooterBar(), 0, 3);

            AcceptButton = btnExecute;
            CancelButton = btnCancel;
            Controls.Add(mainLayout);
        }

        private Control BuildHeaderBanner()
        {
            return new WinFormsLabel
            {
                Text = "XÂY DỰNG & GỘP CORRIDOR THEO TỪNG CẤU KIỆN (LÒNG ĐƯỜNG, DẢI PHÂN CÁCH, VỈA HÈ, RÃNH, TALUY)",
                AutoSize = true,
                Font = StandardFonts.Title,
                ForeColor = Color.FromArgb(0, 70, 150),
                UseMnemonic = false,
                Margin = new Padding(2, 0, 2, 6)
            };
        }

        private GroupBox BuildGeneralGroup()
        {
            var grpGeneral = new GroupBox
            {
                Text = "1. Phương thức tổ chức / Gộp Corridor && Thông số mặc định",
                Dock = DockStyle.Fill,
                AutoSize = true,
                Padding = new Padding(10, 6, 10, 8),
                Margin = new Padding(0, 0, 0, 6)
            };

            var grp1Layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                AutoSize = true
            };
            grp1Layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            grp1Layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grp1Layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grp1Layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            radSeparateCorridor = new RadioButton
            {
                Text = "Tách riêng mỗi Cấu kiện thành 1 Corridor độc lập (Mỗi dòng cấu kiện tạo 1 Corridor riêng)",
                AutoSize = true,
                Checked = false,
                AutoCheck = false,
                UseMnemonic = false,
                Margin = new Padding(3, 2, 3, 2)
            };
            radSeparateCorridor.Click += (s, e) => SetBuildModeUI(CorridorComponentBuildMode.SeparateCorridorPerComponent);

            grp1Layout.Controls.Add(radSeparateCorridor, 0, 0);
            grp1Layout.Controls.Add(BuildMergeModeRow(), 0, 1);
            grp1Layout.Controls.Add(BuildDefaultParamsRow(), 0, 2);

            grpGeneral.Controls.Add(grp1Layout);
            return grpGeneral;
        }

        private Control BuildMergeModeRow()
        {
            var pnlSingleRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoSize = true,
                Margin = new Padding(0, 2, 0, 4)
            };

            radSingleOrExistingCorridor = new RadioButton
            {
                Text = "Gộp tất cả cấu kiện vào 1 Corridor (Tạo mới hoặc thêm vào Corridor có sẵn):",
                AutoSize = true,
                Checked = true,
                AutoCheck = false,
                Font = StandardFonts.Bold,
                UseMnemonic = false,
                Margin = new Padding(3, 6, 8, 2)
            };
            radSingleOrExistingCorridor.Click += (s, e) => SetBuildModeUI(CorridorComponentBuildMode.SingleOrExistingCorridor);

            cmbExistingCorridor = new WinFormsComboBox
            {
                Width = 240,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 3, 8, 2)
            };
            cmbExistingCorridor.Items.Add("(Tạo 1 Corridor gộp mới từ Tiền tố)");
            foreach (var c in _corridorList)
                cmbExistingCorridor.Items.Add(c.Name);
            cmbExistingCorridor.SelectedIndex = 0;
            cmbExistingCorridor.SelectedIndexChanged += (s, e) =>
            {
                if (cmbExistingCorridor.SelectedIndex > 0)
                    SetBuildModeUI(CorridorComponentBuildMode.SingleOrExistingCorridor);
                else
                    UpdateSummaryLabel();
            };

            btnPickExistingCorridor = new WinFormsButton
            {
                Text = "Pick Corridor đích...",
                Size = new Size(145, 34),
                UseMnemonic = false,
                Margin = new Padding(0, 0, 8, 2)
            };
            btnPickExistingCorridor.Click += BtnPickExistingCorridor_Click;

            btnMergeExistingCorridors = new WinFormsButton
            {
                Text = "Gộp các Corridor có sẵn...",
                Size = new Size(185, 34),
                BackColor = Color.FromArgb(255, 244, 214),
                UseMnemonic = false,
                Margin = new Padding(0, 0, 8, 2)
            };
            btnMergeExistingCorridors.Click += BtnMergeExistingCorridors_Click;

            chkDeleteMergedSourceCorridors = new CheckBox
            {
                Text = "Xóa Corridor nguồn sau khi gộp",
                AutoSize = true,
                Checked = true,
                UseMnemonic = false,
                Margin = new Padding(2, 7, 0, 2)
            };

            pnlSingleRow.Controls.AddRange(new Control[]
            {
                radSingleOrExistingCorridor,
                cmbExistingCorridor,
                btnPickExistingCorridor,
                btnMergeExistingCorridors,
                chkDeleteMergedSourceCorridors
            });
            return pnlSingleRow;
        }

        private Control BuildDefaultParamsRow()
        {
            var pnlParamsRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoSize = true,
                Margin = new Padding(0, 2, 0, 0)
            };

            var lblPrefix = new WinFormsLabel { Text = "Tên / Tiền tố Corridor:", AutoSize = true, UseMnemonic = false, Margin = new Padding(3, 6, 4, 2) };
            txtCorridorPrefix = new WinFormsTextBox { Text = "COR_", Width = 105, Margin = new Padding(0, 3, 14, 2) };
            txtCorridorPrefix.TextChanged += (s, e) => UpdateSummaryLabel();

            var lblCodeSet = new WinFormsLabel { Text = "Code Set Style:", AutoSize = true, UseMnemonic = false, Margin = new Padding(0, 6, 4, 2) };
            cmbCodeSetStyle = new WinFormsComboBox { Width = 175, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 3, 14, 2) };
            foreach (var css in _codeSetStyleList)
                cmbCodeSetStyle.Items.Add(css.Name);
            if (cmbCodeSetStyle.Items.Count > 0)
                cmbCodeSetStyle.SelectedIndex = 0;

            var lblDefSurface = new WinFormsLabel { Text = "Bề mặt tự nhiên (EG):", AutoSize = true, UseMnemonic = false, Margin = new Padding(0, 6, 4, 2) };
            cmbDefaultSurface = new WinFormsComboBox { Width = 155, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 3, 14, 2) };
            cmbDefaultSurface.Items.Add("(Không gán Surface)");
            foreach (var s in _surfaceList)
                cmbDefaultSurface.Items.Add(s.Name);
            int egIdx = _surfaceList.FindIndex(x => x.Name.Contains("EG", StringComparison.OrdinalIgnoreCase) || x.Name.Contains("TN", StringComparison.OrdinalIgnoreCase));
            cmbDefaultSurface.SelectedIndex = egIdx >= 0 ? egIdx + 1 : (_surfaceList.Count > 0 ? 1 : 0);

            var lblFreqT = new WinFormsLabel { Text = "Bước cọc Thẳng / Cong (m):", AutoSize = true, UseMnemonic = false, Margin = new Padding(0, 6, 4, 2) };
            numFreqTangent = new NumericUpDown { Width = 60, Minimum = 1m, Maximum = 200m, DecimalPlaces = 1, Value = 10m, Margin = new Padding(0, 3, 4, 2) };
            numFreqCurve = new NumericUpDown { Width = 60, Minimum = 0.5m, Maximum = 200m, DecimalPlaces = 1, Value = 5m, Margin = new Padding(0, 3, 0, 2) };

            pnlParamsRow.Controls.AddRange(new Control[]
            {
                lblPrefix, txtCorridorPrefix,
                lblCodeSet, cmbCodeSetStyle,
                lblDefSurface, cmbDefaultSurface,
                lblFreqT, numFreqTangent, numFreqCurve
            });
            return pnlParamsRow;
        }

        private Control BuildFooterBar()
        {
            var pnlFooter = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                AutoSize = true,
                Margin = new Padding(0, 2, 0, 0)
            };
            pnlFooter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            pnlFooter.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pnlFooter.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            lblSummary = new WinFormsLabel
            {
                Text = "Sẵn sàng.",
                AutoSize = true,
                Font = FooterBoldFont,
                ForeColor = Color.FromArgb(0, 100, 60),
                UseMnemonic = false,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(2, 8, 8, 0)
            };

            btnExecute = new WinFormsButton
            {
                Text = "Xây Dựng / Gộp Corridor",
                Size = new Size(255, 42),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = FooterBoldFont,
                UseMnemonic = false,
                Margin = new Padding(0, 0, 8, 0)
            };
            btnExecute.Click += BtnExecute_Click;

            btnCancel = new WinFormsButton
            {
                Text = "Đóng",
                Size = new Size(110, 42),
                UseMnemonic = false,
                Margin = new Padding(0)
            };
            btnCancel.Click += (s, e) =>
            {
                FormAccepted = false;
                DialogResult = DialogResult.Cancel;
                Close();
            };

            pnlFooter.Controls.Add(lblSummary, 0, 0);
            pnlFooter.Controls.Add(btnExecute, 1, 0);
            pnlFooter.Controls.Add(btnCancel, 2, 0);
            return pnlFooter;
        }
    }
}

using System;
using System.Drawing;
using System.Windows.Forms;

using MyFirstProject.Shared.UI;

using ColumnStyle = System.Windows.Forms.ColumnStyle;
using FlowDirection = System.Windows.Forms.FlowDirection;
using Size = System.Drawing.Size;
using WinFormsButton = System.Windows.Forms.Button;
using WinFormsComboBox = System.Windows.Forms.ComboBox;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsTextBox = System.Windows.Forms.TextBox;

namespace MyFirstProject.Commands.CTC.TaoCorridorTheoCauKien
{
    public partial class TaoCorridorTheoCauKienForm
    {
        private void BuildLayout()
        {
            SuspendLayout();
            Text = "CTC_Tao_Corridor_Theo_Cau_Kien — Quét FeatureLine Theo Site & Cập Nhật Corridor Tổng";
            Font = StandardFonts.Regular;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false;
            MaximizeBox = true;
            MinimumSize = new Size(1120, 680);
            _toolTip = FormBuilder.CreateToolTip(this);

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

            Controls.Add(mainLayout);
            AcceptButton = btnExecute;
            CancelButton = btnCancel;
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ResumeLayout(false);
            PerformLayout();

            PersistentState.RestoreFormSize(this, _lastFormSize);
        }

        private Control BuildHeaderBanner()
        {
            return new WinFormsLabel
            {
                Text = "QUÉT FEATURELINE THEO SITE — TẠO & CẬP NHẬT 1 CORRIDOR TỔNG (GIỮ NGUYÊN LÝ TRÌNH REGION ĐÃ CÓ)",
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
                Text = "1. Nguồn Site FeatureLine && Corridor Tổng Đích",
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

            grp1Layout.Controls.Add(BuildSiteSourceRow(), 0, 0);
            grp1Layout.Controls.Add(BuildTargetCorridorRow(), 0, 1);
            grp1Layout.Controls.Add(BuildDefaultParamsRow(), 0, 2);

            grpGeneral.Controls.Add(grp1Layout);
            return grpGeneral;
        }

        private Control BuildSiteSourceRow()
        {
            var pnl = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, AutoSize = true, Margin = new Padding(0, 0, 0, 4) };

            var lblSite = FormBuilder.Label("Site nguồn FeatureLine:", bold: true);
            cmbSite = new WinFormsComboBox { Width = 300, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 6, 8, 2) };
            PopulateSiteComboBox();
            cmbSite.SelectedIndexChanged += (s, e) => OnSiteSelectionChanged();

            btnPickSiteFromFl = FormBuilder.ActionButton("Pick FL lấy Site...", ButtonKind.Normal, 145);
            btnPickSiteFromFl.Margin = new Padding(0, 0, 6, 2);
            btnPickSiteFromFl.Click += BtnPickSiteFromFl_Click;

            btnRescanSite = FormBuilder.ActionButton("🔄 Quét lại Site", ButtonKind.Normal, 130);
            btnRescanSite.Margin = new Padding(0, 0, 12, 2);
            btnRescanSite.Click += BtnRescanSite_Click;

            chkRemoveDeletedBaselines = FormBuilder.CheckBox("Tự gỡ FL đã xóa khỏi Site khi Cập nhật", isChecked: true);
            chkRemoveDeletedBaselines.Margin = new Padding(2, 10, 0, 2);
            chkRemoveDeletedBaselines.CheckedChanged += (s, e) => UpdateSummaryLabel();

            pnl.Controls.AddRange(new Control[] { lblSite, cmbSite, btnPickSiteFromFl, btnRescanSite, chkRemoveDeletedBaselines });
            return pnl;
        }

        private Control BuildTargetCorridorRow()
        {
            var pnl = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, AutoSize = true, Margin = new Padding(0, 2, 0, 4) };

            var lblTarget = FormBuilder.Label("Corridor tổng đích:", bold: true);
            cmbExistingCorridor = new WinFormsComboBox { Width = 250, DropDownStyle = ComboBoxStyle.DropDownList, Margin = new Padding(0, 6, 8, 2) };
            PopulateCorridorComboBox();
            cmbExistingCorridor.SelectedIndexChanged += (s, e) => OnTargetCorridorSelectionChanged();

            var lblName = FormBuilder.Label("Tên mới:");
            lblName.Margin = new Padding(0, 9, 4, 2);
            txtCorridorName = new WinFormsTextBox { Text = "COR_Tong", Width = 155, Margin = new Padding(0, 6, 8, 2) };
            txtCorridorName.TextChanged += (s, e) => UpdateSummaryLabel();

            btnPickExistingCorridor = FormBuilder.ActionButton("Pick Corridor...", ButtonKind.Normal, 130);
            btnPickExistingCorridor.Margin = new Padding(0, 0, 6, 2);
            btnPickExistingCorridor.Click += BtnPickExistingCorridor_Click;

            btnMergeExistingCorridors = FormBuilder.ActionButton("Gộp thêm Corridor...", ButtonKind.Normal, 155);
            btnMergeExistingCorridors.BackColor = Color.FromArgb(255, 244, 214);
            btnMergeExistingCorridors.UseVisualStyleBackColor = false;
            btnMergeExistingCorridors.Margin = new Padding(0, 0, 8, 2);
            btnMergeExistingCorridors.Click += BtnMergeExistingCorridors_Click;

            chkDeleteMergedSourceCorridors = FormBuilder.CheckBox("Xóa Corridor nguồn khi gộp", isChecked: true);
            chkDeleteMergedSourceCorridors.Margin = new Padding(2, 10, 0, 2);

            pnl.Controls.AddRange(new Control[] { lblTarget, cmbExistingCorridor, lblName, txtCorridorName, btnPickExistingCorridor, btnMergeExistingCorridors, chkDeleteMergedSourceCorridors });
            return pnl;
        }

        private Control BuildDefaultParamsRow()
        {
            var pnl = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoSize = true,
                Margin = new Padding(0, 2, 0, 0)
            };

            var lblDefAsm = FormBuilder.Label("Mặt cắt mặc định (FL mới):");
            cmbDefaultAssembly = new WinFormsComboBox
            {
                Width = 190,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 4, 12, 2)
            };
            foreach (var asm in _assemblyList)
                cmbDefaultAssembly.Items.Add(asm.Name);
            if (cmbDefaultAssembly.Items.Count > 0)
                cmbDefaultAssembly.SelectedIndex = 0;

            var lblDefSurface = FormBuilder.Label("Bề mặt EG mặc định:");
            cmbDefaultSurface = new WinFormsComboBox
            {
                Width = 155,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 4, 12, 2)
            };
            cmbDefaultSurface.Items.Add("(Không gán Surface)");
            foreach (var s in _surfaceList)
                cmbDefaultSurface.Items.Add(s.Name);
            int egIdx = _surfaceList.FindIndex(x =>
                x.Name.Contains("EG", StringComparison.OrdinalIgnoreCase) ||
                x.Name.Contains("TN", StringComparison.OrdinalIgnoreCase));
            cmbDefaultSurface.SelectedIndex = egIdx >= 0 ? egIdx + 1 : (_surfaceList.Count > 0 ? 1 : 0);

            var lblCodeSet = FormBuilder.Label("Code Set:");
            cmbCodeSetStyle = new WinFormsComboBox
            {
                Width = 140,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 4, 12, 2)
            };
            foreach (var css in _codeSetStyleList)
                cmbCodeSetStyle.Items.Add(css.Name);
            if (cmbCodeSetStyle.Items.Count > 0)
                cmbCodeSetStyle.SelectedIndex = 0;

            var lblFreqT = FormBuilder.Label("Bước cọc Thẳng/Cong (m):");
            numFreqTangent = FormBuilder.Number(1m, 200m, 10m, 1, 1m);
            numFreqTangent.Width = 58;
            numFreqTangent.Margin = new Padding(0, 4, 4, 2);

            numFreqCurve = FormBuilder.Number(0.5m, 200m, 5m, 1, 0.5m);
            numFreqCurve.Width = 58;
            numFreqCurve.Margin = new Padding(0, 4, 0, 2);

            pnl.Controls.AddRange(new Control[]
            {
                lblDefAsm, cmbDefaultAssembly,
                lblDefSurface, cmbDefaultSurface,
                lblCodeSet, cmbCodeSetStyle,
                lblFreqT, numFreqTangent, numFreqCurve
            });
            return pnl;
        }

        private Control BuildFooterBar()
        {
            var pnlFooter = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                AutoSize = true,
                Margin = new Padding(0, 4, 0, 0)
            };
            pnlFooter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            pnlFooter.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pnlFooter.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            lblSummary = new WinFormsLabel
            {
                Text = "Sẵn sàng.",
                AutoSize = true,
                Font = StandardFonts.Bold,
                ForeColor = Color.FromArgb(0, 100, 60),
                UseMnemonic = false,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(2, 10, 8, 0)
            };

            btnExecute = FormBuilder.ActionButton("🏗 Tạo Corridor Tổng", ButtonKind.Primary, 235);
            btnExecute.Margin = new Padding(0, 0, 8, 0);
            btnExecute.Click += BtnExecute_Click;

            btnCancel = FormBuilder.ActionButton("Đóng", ButtonKind.Normal, 110);
            btnCancel.Margin = new Padding(0);
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

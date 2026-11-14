using System;
using System.Drawing;
using System.Windows.Forms;

using MyFirstProject.Shared.UI;

using ColumnStyle = System.Windows.Forms.ColumnStyle;
using FlowDirection = System.Windows.Forms.FlowDirection;
using Size = System.Drawing.Size;
using WinFormsComboBox = System.Windows.Forms.ComboBox;
using WinFormsLabel = System.Windows.Forms.Label;

namespace MyFirstProject.Commands.CTC.TaoCorridorTheoCauKien
{
    public partial class TaoCorridorTheoCauKienForm
    {
        private GroupBox BuildComponentsGroup()
        {
            var grpComponents = new GroupBox
            {
                Text = "2. Danh Sách FeatureLine Trong Site — Gán Mặt Cắt (Assembly) && Target",
                Dock = DockStyle.Fill,
                Padding = new Padding(10, 6, 10, 8),
                Margin = new Padding(0, 0, 0, 6)
            };

            var grp2Layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4
            };
            grp2Layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            grp2Layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grp2Layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            grp2Layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            grp2Layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            grp2Layout.Controls.Add(BuildBatchAssignToolbar(), 0, 0);
            grp2Layout.Controls.Add(BuildEmptyListNotice(), 0, 1);
            grp2Layout.Controls.Add(BuildComponentsGrid(), 0, 2);
            grp2Layout.Controls.Add(BuildSubActionsBar(), 0, 3);

            grpComponents.Controls.Add(grp2Layout);
            return grpComponents;
        }

        private Control BuildBatchAssignToolbar()
        {
            pnlBatchToolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoSize = true,
                Margin = new Padding(0, 2, 0, 6)
            };

            var lblFilter = FormBuilder.Label("Lọc nhanh Style/Layer:");
            cmbFilterStyleLayer = new WinFormsComboBox
            {
                Width = 175,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 7, 4, 2)
            };

            btnSelectByGroup = FormBuilder.ActionButton("Chọn nhóm", ButtonKind.Normal, 95);
            btnSelectByGroup.Margin = new Padding(0, 0, 10, 2);
            btnSelectByGroup.Click += BtnSelectByGroup_Click;

            var lblAsm = FormBuilder.Label("Mặt cắt:");
            cmbBatchAssembly = new WinFormsComboBox
            {
                Width = 165,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 7, 4, 2)
            };
            foreach (var asm in _assemblyList)
                cmbBatchAssembly.Items.Add(asm.Name);
            if (cmbBatchAssembly.Items.Count > 0)
                cmbBatchAssembly.SelectedIndex = 0;

            btnApplyBatchAssembly = FormBuilder.ActionButton("Gán Mặt cắt", ButtonKind.Normal, 105);
            btnApplyBatchAssembly.BackColor = Color.FromArgb(228, 242, 255);
            btnApplyBatchAssembly.UseVisualStyleBackColor = false;
            btnApplyBatchAssembly.Margin = new Padding(0, 0, 4, 2);
            btnApplyBatchAssembly.Click += BtnApplyBatchAssembly_Click;

            btnPickAssemblyForRow = FormBuilder.ActionButton("Pick Mặt cắt CAD", ButtonKind.Normal, 130);
            btnPickAssemblyForRow.Margin = new Padding(0, 0, 10, 2);
            btnPickAssemblyForRow.Click += BtnPickAssemblyForRow_Click;

            var lblSurf = FormBuilder.Label("Surface:");
            cmbBatchSurface = new WinFormsComboBox
            {
                Width = 140,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Margin = new Padding(0, 7, 4, 2)
            };
            cmbBatchSurface.Items.Add(SurfaceOptionUseDefault);
            cmbBatchSurface.Items.Add(SurfaceOptionNone);
            foreach (var s in _surfaceList)
                cmbBatchSurface.Items.Add(s.Name);
            cmbBatchSurface.SelectedIndex = 0;

            btnApplyBatchSurface = FormBuilder.ActionButton("Gán Surface", ButtonKind.Normal, 100);
            btnApplyBatchSurface.Margin = new Padding(0, 0, 8, 2);
            btnApplyBatchSurface.Click += BtnApplyBatchSurface_Click;

            btnZoomToRow = FormBuilder.ActionButton("🔍 Zoom ĐT", ButtonKind.Normal, 95);
            btnZoomToRow.Margin = new Padding(0, 0, 0, 2);
            btnZoomToRow.Click += BtnZoomToRow_Click;

            pnlBatchToolbar.Controls.AddRange(new Control[]
            {
                lblFilter, cmbFilterStyleLayer, btnSelectByGroup,
                lblAsm, cmbBatchAssembly, btnApplyBatchAssembly, btnPickAssemblyForRow,
                lblSurf, cmbBatchSurface, btnApplyBatchSurface, btnZoomToRow
            });
            return pnlBatchToolbar;
        }

        private Control BuildEmptyListNotice()
        {
            lblEmptyListNotice = new WinFormsLabel
            {
                Text = "👉 Danh sách chỉ hiển thị sau khi bạn chọn một [Site nguồn FeatureLine], [Pick FL lấy Site], hoặc chọn Corridor tổng / Alignment.\r\n" +
                       "Sau khi chọn Site, toàn bộ FeatureLine trong Site sẽ được liệt kê tại đây; lý trình các Region đã có trên Corridor được giữ nguyên tuyệt đối.",
                Dock = DockStyle.Fill,
                AutoSize = true,
                Padding = new Padding(14, 18, 14, 18),
                BackColor = Color.FromArgb(248, 250, 252),
                ForeColor = Color.FromArgb(60, 80, 110),
                BorderStyle = BorderStyle.FixedSingle,
                Font = StandardFonts.Regular,
                UseMnemonic = false,
                Margin = new Padding(0, 4, 0, 6)
            };
            return lblEmptyListNotice;
        }

        private Control BuildComponentsGrid()
        {
            dgvComponents = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                RowHeadersWidth = 26,
                RowTemplate = { Height = 28 },
                ColumnHeadersHeight = 32,
                BackgroundColor = Color.White,
                Margin = new Padding(0, 0, 0, 6)
            };
            SetupDataGridViewColumns();
            dgvComponents.CellValueChanged += DgvComponents_CellValueChanged;
            dgvComponents.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (dgvComponents.IsCurrentCellDirty)
                    dgvComponents.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            dgvComponents.DataError += (s, e) => { e.ThrowException = false; };
            return dgvComponents;
        }

        private void SetupDataGridViewColumns()
        {
            dgvComponents.Columns.Clear();

            var colEnable = new DataGridViewCheckBoxColumn { Name = "colEnable", HeaderText = "Bật", FillWeight = 36, MinimumWidth = 40 };
            var colStatus = new DataGridViewTextBoxColumn { Name = "colStatus", HeaderText = "Trạng Thái", FillWeight = 105, MinimumWidth = 105, ReadOnly = true };
            var colBaseline = new DataGridViewTextBoxColumn { Name = "colBaseline", HeaderText = "Đường Dẫn (FeatureLine / Tuyến)", FillWeight = 175, MinimumWidth = 155, ReadOnly = true };
            var colStyleLayer = new DataGridViewTextBoxColumn { Name = "colStyleLayer", HeaderText = "Style | Layer", FillWeight = 135, MinimumWidth = 120, ReadOnly = true };

            var colAssembly = new DataGridViewComboBoxColumn { Name = "colAssembly", HeaderText = "Mặt Cắt (Assembly)", FillWeight = 145, MinimumWidth = 130, FlatStyle = FlatStyle.Flat };
            foreach (var asm in _assemblyList)
                colAssembly.Items.Add(asm.Name);

            var colTargetSurface = new DataGridViewComboBoxColumn { Name = "colTargetSurface", HeaderText = "Target Bề Mặt", FillWeight = 125, MinimumWidth = 115, FlatStyle = FlatStyle.Flat };
            colTargetSurface.Items.Add(SurfaceOptionUseDefault);
            colTargetSurface.Items.Add(SurfaceOptionNone);
            foreach (var surf in _surfaceList)
                colTargetSurface.Items.Add(surf.Name);

            var colTargets = new DataGridViewTextBoxColumn { Name = "colTargets", HeaderText = "Target Mở Rộng / Cao Độ", FillWeight = 120, MinimumWidth = 110, ReadOnly = true };
            var colStartSta = new DataGridViewTextBoxColumn { Name = "colStartSta", HeaderText = "LT Đầu", FillWeight = 70, MinimumWidth = 68 };
            var colEndSta = new DataGridViewTextBoxColumn { Name = "colEndSta", HeaderText = "LT Cuối (0=Hết)", FillWeight = 82, MinimumWidth = 78 };
            var colFreq = new DataGridViewTextBoxColumn { Name = "colFreq", HeaderText = "Bước Cọc (m)", FillWeight = 66, MinimumWidth = 64 };

            dgvComponents.Columns.AddRange(new DataGridViewColumn[]
            {
                colEnable, colStatus, colBaseline, colStyleLayer,
                colAssembly, colTargetSurface, colTargets,
                colStartSta, colEndSta, colFreq
            });
        }

        private Control BuildSubActionsBar()
        {
            var pnlSubActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoSize = true,
                Margin = new Padding(0)
            };

            btnPickOffsetTargetsForRow = FormBuilder.ActionButton("Gán Target Mở rộng/Cao độ (Pick CAD)...", ButtonKind.Normal, 290);
            btnPickOffsetTargetsForRow.Margin = new Padding(0, 0, 6, 2);
            btnPickOffsetTargetsForRow.Click += BtnPickOffsetTargetsForRow_Click;

            btnClearOffsetTargetsForRow = FormBuilder.ActionButton("Xóa Target ĐT", ButtonKind.Normal, 110);
            btnClearOffsetTargetsForRow.Margin = new Padding(0, 0, 10, 2);
            btnClearOffsetTargetsForRow.Click += BtnClearOffsetTargetsForRow_Click;

            btnAddFeatureLineComponent = FormBuilder.ActionButton("+ Pick thêm FL lẻ", ButtonKind.Normal, 135);
            btnAddFeatureLineComponent.Margin = new Padding(0, 0, 6, 2);
            btnAddFeatureLineComponent.Click += BtnAddFeatureLineComponent_Click;

            btnAddAlignmentComponent = FormBuilder.ActionButton("+ Thêm Alignment", ButtonKind.Normal, 135);
            btnAddAlignmentComponent.Margin = new Padding(0, 0, 12, 2);
            btnAddAlignmentComponent.Click += BtnAddAlignmentComponent_Click;

            btnRemoveSelectedRow = FormBuilder.ActionButton("Bỏ dòng", ButtonKind.Normal, 85);
            btnRemoveSelectedRow.ForeColor = Color.DarkRed;
            btnRemoveSelectedRow.Margin = new Padding(0, 0, 12, 2);
            btnRemoveSelectedRow.Click += BtnRemoveSelectedRow_Click;

            chkAutoRebuild = FormBuilder.CheckBox("Tự Rebuild Corridor sau khi cập nhật", isChecked: true);
            chkAutoRebuild.Font = StandardFonts.Bold;
            chkAutoRebuild.Margin = new Padding(2, 10, 0, 2);

            pnlSubActions.Controls.AddRange(new Control[]
            {
                btnPickOffsetTargetsForRow,
                btnClearOffsetTargetsForRow,
                btnAddFeatureLineComponent,
                btnAddAlignmentComponent,
                btnRemoveSelectedRow,
                chkAutoRebuild
            });
            return pnlSubActions;
        }
    }
}

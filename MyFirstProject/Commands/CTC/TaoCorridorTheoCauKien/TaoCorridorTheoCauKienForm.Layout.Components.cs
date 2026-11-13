using System.Drawing;
using System.Windows.Forms;

using MyFirstProject.Shared.UI;

using ColumnStyle = System.Windows.Forms.ColumnStyle;
using FlowDirection = System.Windows.Forms.FlowDirection;
using Size = System.Drawing.Size;
using WinFormsButton = System.Windows.Forms.Button;
using WinFormsLabel = System.Windows.Forms.Label;

namespace MyFirstProject.Commands.CTC.TaoCorridorTheoCauKien
{
    public partial class TaoCorridorTheoCauKienForm
    {
        private GroupBox BuildComponentsGroup()
        {
            var grpComponents = new GroupBox
            {
                Text = "2. Chọn Đường dẫn (Alignment / FeatureLine) && Danh sách Cấu kiện Corridor",
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

            grp2Layout.Controls.Add(BuildComponentsToolbar(), 0, 0);
            grp2Layout.Controls.Add(BuildEmptyListNotice(), 0, 1);
            grp2Layout.Controls.Add(BuildComponentsGrid(), 0, 2);
            grp2Layout.Controls.Add(BuildSubActionsBar(), 0, 3);

            grpComponents.Controls.Add(grp2Layout);
            return grpComponents;
        }

        private Control BuildComponentsToolbar()
        {
            var pnlToolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoSize = true,
                Margin = new Padding(0, 2, 0, 6)
            };

            btnAddAlignmentComponent = new WinFormsButton
            {
                Text = "+ Chọn Alignment (Tuyến)",
                Size = new Size(200, 40),
                BackColor = Color.FromArgb(228, 242, 255),
                Font = StandardFonts.Bold,
                UseMnemonic = false,
                Margin = new Padding(0, 0, 6, 2)
            };
            btnAddAlignmentComponent.Click += BtnAddAlignmentComponent_Click;

            btnAddFeatureLineComponent = new WinFormsButton
            {
                Text = "+ Chọn FeatureLine",
                Size = new Size(180, 40),
                BackColor = Color.FromArgb(228, 242, 255),
                Font = StandardFonts.Bold,
                UseMnemonic = false,
                Margin = new Padding(0, 0, 6, 2)
            };
            btnAddFeatureLineComponent.Click += BtnAddFeatureLineComponent_Click;

            btnRestoreSavedRows = new WinFormsButton
            {
                Text = "Nạp lại ĐT lần trước",
                Size = new Size(170, 40),
                UseMnemonic = false,
                Visible = false,
                Margin = new Padding(0, 0, 6, 2)
            };
            btnRestoreSavedRows.Click += BtnRestoreSavedRows_Click;

            btnPickBaselineForRow = new WinFormsButton
            {
                Text = "Đổi Đường dẫn dòng chọn",
                Size = new Size(190, 40),
                UseMnemonic = false,
                Margin = new Padding(0, 0, 6, 2)
            };
            btnPickBaselineForRow.Click += BtnPickBaselineForRow_Click;

            btnPickAssemblyForRow = new WinFormsButton
            {
                Text = "Pick Assembly trên CAD",
                Size = new Size(175, 40),
                UseMnemonic = false,
                Margin = new Padding(0, 0, 6, 2)
            };
            btnPickAssemblyForRow.Click += BtnPickAssemblyForRow_Click;

            btnRemoveSelectedRow = new WinFormsButton
            {
                Text = "Xóa dòng",
                Size = new Size(85, 40),
                ForeColor = Color.DarkRed,
                UseMnemonic = false,
                Margin = new Padding(0, 0, 6, 2)
            };
            btnRemoveSelectedRow.Click += BtnRemoveSelectedRow_Click;

            btnClearAllRows = new WinFormsButton
            {
                Text = "Xóa tất cả",
                Size = new Size(95, 40),
                ForeColor = Color.DarkRed,
                UseMnemonic = false,
                Margin = new Padding(0, 0, 0, 2)
            };
            btnClearAllRows.Click += BtnClearAllRows_Click;

            pnlToolbar.Controls.AddRange(new Control[]
            {
                btnAddAlignmentComponent,
                btnAddFeatureLineComponent,
                btnRestoreSavedRows,
                btnPickBaselineForRow,
                btnPickAssemblyForRow,
                btnRemoveSelectedRow,
                btnClearAllRows
            });
            return pnlToolbar;
        }

        private Control BuildEmptyListNotice()
        {
            lblEmptyListNotice = new WinFormsLabel
            {
                Text = "👉 Danh sách cấu kiện chỉ hiển thị sau khi bạn chọn Alignment hoặc FeatureLine.\r\n" +
                       "Hãy bấm nút [+ Chọn Alignment (Tuyến)] hoặc [+ Chọn FeatureLine] ở phía trên để chọn đối tượng trên bản vẽ.",
                Dock = DockStyle.Fill,
                AutoSize = true,
                Padding = new Padding(14, 18, 14, 18),
                BackColor = Color.FromArgb(248, 250, 252),
                ForeColor = Color.FromArgb(60, 80, 110),
                BorderStyle = BorderStyle.FixedSingle,
                Font = EmptyNoticeFont,
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
                MultiSelect = false,
                RowHeadersWidth = 26,
                RowTemplate = { Height = 28 },
                ColumnHeadersHeight = 32,
                BackgroundColor = Color.White,
                Margin = new Padding(0, 0, 0, 6)
            };
            SetupDataGridViewColumns();
            dgvComponents.CellValueChanged += (s, e) =>
            {
                if (e.RowIndex >= 0) SyncGridRowToModel(e.RowIndex);
                UpdateSummaryLabel();
            };
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

            var colEnable = new DataGridViewCheckBoxColumn { Name = "colEnable", HeaderText = "Bật", FillWeight = 38, MinimumWidth = 42 };
            var colCategory = new DataGridViewComboBoxColumn { Name = "colCategory", HeaderText = "Nhóm Cấu Kiện", FillWeight = 135, MinimumWidth = 125, FlatStyle = FlatStyle.Flat };
            colCategory.Items.AddRange(ComponentCategories);

            var colName = new DataGridViewTextBoxColumn { Name = "colName", HeaderText = "Tên Cấu Kiện / Corridor", FillWeight = 130, MinimumWidth = 120 };
            var colSourceType = new DataGridViewComboBoxColumn { Name = "colSourceType", HeaderText = "Loại Đường Dẫn", FillWeight = 115, MinimumWidth = 110, FlatStyle = FlatStyle.Flat };
            colSourceType.Items.AddRange(SourceTypeNames);

            var colBaseline = new DataGridViewTextBoxColumn { Name = "colBaseline", HeaderText = "Đường Dẫn (Alignment / FeatureLine)", FillWeight = 195, MinimumWidth = 160, ReadOnly = true };
            var colAssembly = new DataGridViewComboBoxColumn { Name = "colAssembly", HeaderText = "Mặt Cắt Cấu Kiện (Assembly)", FillWeight = 155, MinimumWidth = 140, FlatStyle = FlatStyle.Flat };
            foreach (var asm in _assemblyList)
                colAssembly.Items.Add(asm.Name);

            var colStartSta = new DataGridViewTextBoxColumn { Name = "colStartSta", HeaderText = "Lý Trình Đầu", FillWeight = 75, MinimumWidth = 75 };
            var colEndSta = new DataGridViewTextBoxColumn { Name = "colEndSta", HeaderText = "Lý Trình Cuối (0=Hết)", FillWeight = 95, MinimumWidth = 90 };
            var colFreq = new DataGridViewTextBoxColumn { Name = "colFreq", HeaderText = "Bước Cọc (m)", FillWeight = 72, MinimumWidth = 70 };
            var colTargets = new DataGridViewTextBoxColumn { Name = "colTargets", HeaderText = "Target Mở Rộng / Cao Độ", FillWeight = 115, MinimumWidth = 105, ReadOnly = true };

            dgvComponents.Columns.AddRange(new DataGridViewColumn[]
            {
                colEnable, colCategory, colName, colSourceType,
                colBaseline, colAssembly, colStartSta, colEndSta, colFreq, colTargets
            });
        }

        private Control BuildSubActionsBar()
        {
            pnlSubActions = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                AutoSize = true,
                Margin = new Padding(0)
            };
            pnlSubActions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pnlSubActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            btnPickOffsetTargetsForRow = new WinFormsButton
            {
                Text = "Gán Target Mở rộng / Cao độ (Alignment, FeatureLine, Polyline) cho dòng chọn...",
                Size = new Size(540, 40),
                UseMnemonic = false,
                Margin = new Padding(0, 0, 16, 0)
            };
            btnPickOffsetTargetsForRow.Click += BtnPickOffsetTargetsForRow_Click;

            chkAutoRebuild = new CheckBox
            {
                Text = "Tự động Rebuild Corridor ngay sau khi lắp ghép các cấu kiện",
                AutoSize = true,
                Checked = true,
                UseMnemonic = false,
                Font = StandardFonts.Bold,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(4, 8, 0, 0)
            };

            pnlSubActions.Controls.Add(btnPickOffsetTargetsForRow, 0, 0);
            pnlSubActions.Controls.Add(chkAutoRebuild, 1, 0);
            return pnlSubActions;
        }
    }
}

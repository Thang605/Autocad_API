using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ClosedXML.Excel;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using CadEntity = Autodesk.AutoCAD.DatabaseServices.Entity;
using CivSurface = Autodesk.Civil.DatabaseServices.TinSurface;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsFont = System.Drawing.Font;
using WinFormsColor = System.Drawing.Color;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace MyFirstProject.Civil_Tool_2
{
    public enum PavementSolidStackMode
    {
        StackDownFromTopSurface = 0,
        DirectFromSurfaceNoOffset = 1,
        StackUpFromBottomSurface = 2
    }

    public class PavementLayerItem
    {
        public bool IsSelected { get; set; } = true;
        public string StructureName { get; set; } = "ĐƯỜNG BTN";
        public string MaterialName { get; set; } = string.Empty;
        public double Thickness { get; set; } = 0.06;
        public ushort AciColor { get; set; } = 1;

        public PavementLayerItem Clone()
        {
            return new PavementLayerItem
            {
                IsSelected = this.IsSelected,
                StructureName = this.StructureName,
                MaterialName = this.MaterialName,
                Thickness = this.Thickness,
                AciColor = this.AciColor
            };
        }
    }

    public class SurfaceDropdownItem
    {
        public List<ObjectId> SurfaceIds { get; set; } = new List<ObjectId>();
        public string DisplayText { get; set; } = string.Empty;
        public override string ToString() => DisplayText;
    }

    public class TrichXuatSolidKetCauAoDuongForm : Form
    {
        // ═══════════════════════════════════════════════════════════════
        //  PERSISTENT STATE (BẮT BUỘC THEO QUY TẮC RULE 2)
        // ═══════════════════════════════════════════════════════════════
        private static Size _lastFormSize = new Size(1040, 740);
        private static List<ObjectId> _lastSelectedSurfaceIds = new List<ObjectId>();
        private static string _lastStructureName = "ĐƯỜNG BTN";
        private static List<PavementLayerItem> _lastLayers = new List<PavementLayerItem>();
        private static PavementSolidStackMode _lastStackMode = PavementSolidStackMode.StackDownFromTopSurface;
        private static bool _lastKeepTrailingZero = false;
        private static bool _lastUpdateExistingLayerColor = true;
        private static string _lastExcelPath = string.Empty;

        // ═══════════════════════════════════════════════════════════════
        //  FIELDS
        // ═══════════════════════════════════════════════════════════════
        private readonly Database _db;
        private readonly Editor _ed;
        private List<ObjectId> _selectedSurfaceIds = new List<ObjectId>();
        private bool _isUpdatingGrid = false;

        // UI Controls
        private ComboBox cboSurfaces = null!;
        private Button btnPickSurface = null!;
        private WinFormsLabel lblSurfaceInfo = null!;
        private TextBox txtStructureName = null!;
        private Button btnApplyStructureAll = null!;

        private DataGridView dgvLayers = null!;
        private Button btnAddRow = null!;
        private Button btnRemoveRow = null!;
        private Button btnMoveUp = null!;
        private Button btnMoveDown = null!;
        private Button btnDefault5Layers = null!;
        private Button btnImportExcel = null!;
        private Button btnExportExcel = null!;
        private WinFormsLabel lblLayerSummary = null!;

        private RadioButton radStackDown = null!;
        private RadioButton radDirectNoOffset = null!;
        private RadioButton radStackUp = null!;
        private CheckBox chkKeepTrailingZero = null!;
        private CheckBox chkUpdateExistingLayerColor = null!;

        private ProgressBar progressBar = null!;
        private WinFormsLabel lblStatus = null!;
        private Button btnExecute = null!;
        private Button btnClose = null!;

        public TrichXuatSolidKetCauAoDuongForm(Database db, Editor ed)
        {
            _db = db;
            _ed = ed;

            InitializeCustomUI();
            RestoreLastSettings();
            LoadSurfacesList();
            RefreshGridCalculatedColumns();
        }

        private void InitializeCustomUI()
        {
            this.Text = "Trích Xuất 3D Solid Kết Cấu Áo Đường Từ Mặt Phẳng (AT_Xuat_Solid_KCAD_Tu_MatPhang)";
            this.Size = _lastFormSize;
            this.MinimumSize = new Size(960, 670);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.BackColor = WinFormsColor.FromArgb(245, 247, 250);

            // 1. Header Panel
            Panel pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 66,
                BackColor = WinFormsColor.FromArgb(24, 76, 120),
                Padding = new Padding(16, 10, 16, 10)
            };

            WinFormsLabel lblHeaderTitle = new WinFormsLabel
            {
                Text = "TRÍCH XUẤT 3D SOLID KẾT CẤU ÁO ĐƯỜNG (AT_XUAT_SOLID_KCAD_TU_MATPHANG)",
                Font = new WinFormsFont("Segoe UI", 11.5F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = WinFormsColor.White,
                Dock = DockStyle.Top,
                Height = 25
            };

            WinFormsLabel lblHeaderSub = new WinFormsLabel
            {
                Text = "Tự động chiết xuất các lớp 3D Solid và gắn vào Layer IN HOA theo cú pháp: TÊN CẤU KIỆN_TÊN VẬT LIỆU_ĐỘ DÀY (VD: ĐƯỜNG BTN_BTN C16_0.06)",
                Font = new WinFormsFont("Segoe UI", 8.5F, FontStyle.Regular, GraphicsUnit.Point),
                ForeColor = WinFormsColor.FromArgb(215, 232, 250),
                Dock = DockStyle.Fill
            };

            pnlHeader.Controls.Add(lblHeaderSub);
            pnlHeader.Controls.Add(lblHeaderTitle);
            this.Controls.Add(pnlHeader);

            // 2. Bottom Panel
            Panel pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 82,
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
                Text = "Sẵn sàng. Hãy chọn mặt phẳng trên cùng của mặt đường, nhập tên cấu kiện và kiểm tra danh sách lớp kết cấu áo đường.",
                Font = new WinFormsFont("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = WinFormsColor.FromArgb(90, 100, 115),
                Dock = DockStyle.Top,
                Height = 22
            };

            Panel pnlButtons = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 36
            };

            btnExecute = new Button
            {
                Text = "⚡ Trích Xuất 3D Solid",
                Width = 180,
                Height = 34,
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
                Left = pnlButtons.Width - 285,
                Top = 1,
                BackColor = WinFormsColor.FromArgb(13, 110, 253),
                ForeColor = WinFormsColor.White,
                FlatStyle = FlatStyle.Flat,
                Font = new WinFormsFont("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnExecute.FlatAppearance.BorderSize = 0;
            btnExecute.Click += BtnExecute_Click;

            btnClose = new Button
            {
                Text = "Đóng",
                Width = 95,
                Height = 34,
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
                Left = pnlButtons.Width - 95,
                Top = 1,
                BackColor = WinFormsColor.FromArgb(230, 234, 240),
                ForeColor = WinFormsColor.FromArgb(50, 60, 75),
                FlatStyle = FlatStyle.Flat,
                Font = new WinFormsFont("Segoe UI", 9F),
                Cursor = Cursors.Hand
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => this.Close();

            pnlButtons.Controls.Add(btnExecute);
            pnlButtons.Controls.Add(btnClose);

            pnlBottom.Controls.Add(pnlButtons);
            pnlBottom.Controls.Add(lblStatus);
            pnlBottom.Controls.Add(progressBar);
            this.Controls.Add(pnlBottom);

            // 3. Main Content Panel
            Panel pnlMain = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(14, 10, 14, 10)
            };
            this.Controls.Add(pnlMain);
            pnlMain.BringToFront();

            // ═══════════════════════════════════════════════════════════════
            //  GROUP 1: MẶT PHẲNG NGUỒN (TIN SURFACE) & TÊN CẤU KIỆN
            // ═══════════════════════════════════════════════════════════════
            GroupBox grpSurface = new GroupBox
            {
                Text = "1. Mặt phẳng nguồn (TIN Surface) & Tên cấu kiện",
                Font = new WinFormsFont("Segoe UI", 9.5F, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 118,
                Padding = new Padding(12, 8, 12, 8)
            };

            WinFormsLabel lblSurf = new WinFormsLabel
            {
                Text = "Mặt phẳng TIN Surface:",
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Regular),
                Left = 14,
                Top = 27,
                Width = 145
            };

            cboSurfaces = new ComboBox
            {
                Left = 162,
                Top = 24,
                Width = 580,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new WinFormsFont("Segoe UI", 9F),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            cboSurfaces.SelectedIndexChanged += CboSurfaces_SelectedIndexChanged;

            btnPickSurface = new Button
            {
                Text = "🎯 Chọn trên CAD",
                Left = 752,
                Top = 22,
                Width = 180,
                Height = 29,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = WinFormsColor.FromArgb(24, 76, 120),
                ForeColor = WinFormsColor.White,
                FlatStyle = FlatStyle.Flat,
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnPickSurface.FlatAppearance.BorderSize = 0;
            btnPickSurface.Click += BtnPickSurface_Click;

            lblSurfaceInfo = new WinFormsLabel
            {
                Text = "Chưa chọn mặt phẳng.",
                Font = new WinFormsFont("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = WinFormsColor.FromArgb(40, 95, 145),
                Left = 162,
                Top = 54,
                Width = 770,
                Height = 20,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            WinFormsLabel lblStructure = new WinFormsLabel
            {
                Text = "Tên cấu kiện:",
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = WinFormsColor.FromArgb(24, 76, 120),
                Left = 14,
                Top = 83,
                Width = 145
            };

            txtStructureName = new TextBox
            {
                Left = 162,
                Top = 80,
                Width = 320,
                CharacterCasing = CharacterCasing.Upper,
                Font = new WinFormsFont("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = WinFormsColor.FromArgb(10, 88, 202),
                Text = "ĐƯỜNG BTN"
            };
            txtStructureName.TextChanged += TxtStructureName_TextChanged;

            btnApplyStructureAll = new Button
            {
                Text = "⬇️ Áp dụng tên cấu kiện cho tất cả lớp",
                Left = 492,
                Top = 79,
                Width = 250,
                Height = 27,
                BackColor = WinFormsColor.FromArgb(230, 238, 248),
                ForeColor = WinFormsColor.FromArgb(24, 76, 120),
                FlatStyle = FlatStyle.Flat,
                Font = new WinFormsFont("Segoe UI", 8.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnApplyStructureAll.FlatAppearance.BorderSize = 0;
            btnApplyStructureAll.Click += (s, e) => ApplyStructureNameToAllRows();

            grpSurface.Controls.Add(lblSurf);
            grpSurface.Controls.Add(cboSurfaces);
            grpSurface.Controls.Add(btnPickSurface);
            grpSurface.Controls.Add(lblSurfaceInfo);
            grpSurface.Controls.Add(lblStructure);
            grpSurface.Controls.Add(txtStructureName);
            grpSurface.Controls.Add(btnApplyStructureAll);

            // ═══════════════════════════════════════════════════════════════
            //  GROUP 3: TÙY CHỌN TRÍCH XUẤT & ĐỊNH DẠNG LAYER (ĐẶT DƯỚI CÙNG)
            // ═══════════════════════════════════════════════════════════════
            GroupBox grpOptions = new GroupBox
            {
                Text = "3. Phương thức xếp lớp 3D Solid & Quy tắc đặt tên Layer",
                Font = new WinFormsFont("Segoe UI", 9.5F, FontStyle.Bold),
                Dock = DockStyle.Bottom,
                Height = 132,
                Padding = new Padding(12, 8, 12, 8)
            };

            radStackDown = new RadioButton
            {
                Text = "Xếp chồng liên tiếp từ trên xuống dưới (Lớp 1 sát mặt phẳng đỉnh, các lớp dưới nối tiếp đáy lớp trên - Chuẩn KCAĐ)",
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = WinFormsColor.FromArgb(20, 65, 110),
                Left = 16,
                Top = 24,
                Width = 900,
                Checked = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            radStackDown.CheckedChanged += (s, e) => RefreshGridCalculatedColumns();

            radDirectNoOffset = new RadioButton
            {
                Text = "Đùn trực tiếp từ mặt phẳng gốc (Tất cả các lớp đều lấy mặt đỉnh tại Surface gốc, không dịch chồng lớp)",
                Font = new WinFormsFont("Segoe UI", 8.5F, FontStyle.Regular),
                Left = 16,
                Top = 49,
                Width = 900,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            radDirectNoOffset.CheckedChanged += (s, e) => RefreshGridCalculatedColumns();

            radStackUp = new RadioButton
            {
                Text = "Xếp chồng liên tiếp từ dưới lên trên (Khi mặt phẳng chọn là mặt đáy lớp dưới cùng)",
                Font = new WinFormsFont("Segoe UI", 8.5F, FontStyle.Regular),
                Left = 16,
                Top = 73,
                Width = 900,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            radStackUp.CheckedChanged += (s, e) => RefreshGridCalculatedColumns();

            chkKeepTrailingZero = new CheckBox
            {
                Text = "Luôn giữ tối thiểu 2 chữ số thập phân ở độ dày trong tên Layer (VD: 0.30 thay vì 0.3)",
                Font = new WinFormsFont("Segoe UI", 8.5F, FontStyle.Regular),
                Left = 16,
                Top = 100,
                Width = 510
            };
            chkKeepTrailingZero.CheckedChanged += (s, e) => RefreshGridCalculatedColumns();

            chkUpdateExistingLayerColor = new CheckBox
            {
                Text = "Cập nhật màu Layer theo bảng nếu Layer đã tồn tại",
                Font = new WinFormsFont("Segoe UI", 8.5F, FontStyle.Regular),
                Left = 540,
                Top = 100,
                Width = 380,
                Checked = true
            };

            grpOptions.Controls.Add(radStackDown);
            grpOptions.Controls.Add(radDirectNoOffset);
            grpOptions.Controls.Add(radStackUp);
            grpOptions.Controls.Add(chkKeepTrailingZero);
            grpOptions.Controls.Add(chkUpdateExistingLayerColor);

            // ═══════════════════════════════════════════════════════════════
            //  GROUP 2: BẢNG CÁC LỚP KẾT CẤU ÁO ĐƯỜNG (GIỮA - FILL)
            // ═══════════════════════════════════════════════════════════════
            GroupBox grpLayers = new GroupBox
            {
                Text = "2. Thông số các lớp kết cấu áo đường (Khai báo từ lớp trên cùng xuống lớp dưới cùng)",
                Font = new WinFormsFont("Segoe UI", 9.5F, FontStyle.Bold),
                Dock = DockStyle.Fill,
                Padding = new Padding(12, 8, 12, 8)
            };

            Panel pnlToolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 38
            };

            btnAddRow = CreateToolbarButton("➕ Thêm lớp", 0, 105, WinFormsColor.FromArgb(25, 135, 84), WinFormsColor.White);
            btnAddRow.Click += BtnAddRow_Click;

            btnRemoveRow = CreateToolbarButton("➖ Xóa lớp", 111, 95, WinFormsColor.FromArgb(220, 53, 69), WinFormsColor.White);
            btnRemoveRow.Click += BtnRemoveRow_Click;

            btnMoveUp = CreateToolbarButton("⬆️ Lên", 212, 75, WinFormsColor.FromArgb(230, 234, 240), WinFormsColor.FromArgb(40, 50, 65));
            btnMoveUp.Click += (s, e) => MoveSelectedRow(-1);

            btnMoveDown = CreateToolbarButton("⬇️ Xuống", 293, 85, WinFormsColor.FromArgb(230, 234, 240), WinFormsColor.FromArgb(40, 50, 65));
            btnMoveDown.Click += (s, e) => MoveSelectedRow(1);

            btnDefault5Layers = CreateToolbarButton("🔄 Mẫu 5 lớp KCAĐ", 384, 145, WinFormsColor.FromArgb(230, 234, 240), WinFormsColor.FromArgb(24, 76, 120));
            btnDefault5Layers.Click += (s, e) =>
            {
                string currentStruct = GetCurrentDefaultStructureName();
                PopulateGridFromList(GetDefaultFiveLayers(currentStruct));
                lblStatus.Text = "Đã nạp mẫu 5 lớp kết cấu áo đường tiêu chuẩn.";
            };

            btnExportExcel = new Button
            {
                Text = "📥 Xuất Excel",
                Width = 120,
                Height = 30,
                Top = 3,
                Left = pnlToolbar.Width - 120,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = WinFormsColor.FromArgb(25, 135, 84),
                ForeColor = WinFormsColor.White,
                FlatStyle = FlatStyle.Flat,
                Font = new WinFormsFont("Segoe UI", 8.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnExportExcel.FlatAppearance.BorderSize = 0;
            btnExportExcel.Click += BtnExportExcel_Click;

            btnImportExcel = new Button
            {
                Text = "📤 Nhập Excel",
                Width = 120,
                Height = 30,
                Top = 3,
                Left = pnlToolbar.Width - 246,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = WinFormsColor.FromArgb(24, 76, 120),
                ForeColor = WinFormsColor.White,
                FlatStyle = FlatStyle.Flat,
                Font = new WinFormsFont("Segoe UI", 8.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnImportExcel.FlatAppearance.BorderSize = 0;
            btnImportExcel.Click += BtnImportExcel_Click;

            pnlToolbar.Controls.Add(btnAddRow);
            pnlToolbar.Controls.Add(btnRemoveRow);
            pnlToolbar.Controls.Add(btnMoveUp);
            pnlToolbar.Controls.Add(btnMoveDown);
            pnlToolbar.Controls.Add(btnDefault5Layers);
            pnlToolbar.Controls.Add(btnImportExcel);
            pnlToolbar.Controls.Add(btnExportExcel);

            dgvLayers = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = WinFormsColor.White,
                BorderStyle = BorderStyle.FixedSingle,
                GridColor = WinFormsColor.FromArgb(220, 224, 230),
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Regular),
                RowTemplate = { Height = 30 },
                ColumnHeadersHeight = 34,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                EnableHeadersVisualStyles = false
            };

            dgvLayers.ColumnHeadersDefaultCellStyle.BackColor = WinFormsColor.FromArgb(235, 240, 248);
            dgvLayers.ColumnHeadersDefaultCellStyle.ForeColor = WinFormsColor.FromArgb(24, 55, 90);
            dgvLayers.ColumnHeadersDefaultCellStyle.Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold);
            dgvLayers.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            // Columns
            var colSelect = new DataGridViewCheckBoxColumn
            {
                Name = "colSelect",
                HeaderText = "Xuất",
                Width = 48,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };

            var colOrder = new DataGridViewTextBoxColumn
            {
                Name = "colOrder",
                HeaderText = "Thứ tự",
                Width = 60,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    BackColor = WinFormsColor.FromArgb(248, 249, 252),
                    ForeColor = WinFormsColor.FromArgb(90, 100, 115)
                }
            };

            var colStructure = new DataGridViewTextBoxColumn
            {
                Name = "colStructure",
                HeaderText = "Tên cấu kiện",
                Width = 140,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold),
                    ForeColor = WinFormsColor.FromArgb(24, 76, 120)
                }
            };

            var colMaterial = new DataGridViewTextBoxColumn
            {
                Name = "colMaterial",
                HeaderText = "Tên vật liệu / Lớp KCAĐ",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 160,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };

            var colThickness = new DataGridViewTextBoxColumn
            {
                Name = "colThickness",
                HeaderText = "Độ dày (m)",
                Width = 95,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold)
                }
            };

            var colColor = new DataGridViewTextBoxColumn
            {
                Name = "colColor",
                HeaderText = "Màu ACI (Click)",
                Width = 100,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    Font = new WinFormsFont("Segoe UI", 8.5F, FontStyle.Bold)
                }
            };

            var colLayerName = new DataGridViewTextBoxColumn
            {
                Name = "colLayerName",
                HeaderText = "Tên Layer (TÊN CẤU KIỆN_TÊN VẬT LIỆU_ĐỘ DÀY)",
                Width = 285,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = WinFormsColor.FromArgb(242, 248, 255),
                    ForeColor = WinFormsColor.FromArgb(10, 88, 202),
                    Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold)
                }
            };

            var colElevRange = new DataGridViewTextBoxColumn
            {
                Name = "colElevRange",
                HeaderText = "Cao độ tương đối (m)",
                Width = 135,
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    BackColor = WinFormsColor.FromArgb(248, 249, 252),
                    ForeColor = WinFormsColor.FromArgb(70, 80, 95)
                }
            };

            dgvLayers.Columns.AddRange(colSelect, colOrder, colStructure, colMaterial, colThickness, colColor, colLayerName, colElevRange);

            dgvLayers.CellValueChanged += DgvLayers_CellValueChanged;
            dgvLayers.EditingControlShowing += (s, e) =>
            {
                if (e.Control is TextBox tb)
                {
                    string colName = dgvLayers.CurrentCell != null
                        ? dgvLayers.Columns[dgvLayers.CurrentCell.ColumnIndex].Name
                        : string.Empty;

                    if (colName == "colMaterial" || colName == "colStructure")
                    {
                        tb.CharacterCasing = CharacterCasing.Upper;
                    }
                    else
                    {
                        tb.CharacterCasing = CharacterCasing.Normal;
                    }
                }
            };
            dgvLayers.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (dgvLayers.IsCurrentCellDirty && dgvLayers.CurrentCell is DataGridViewCheckBoxCell)
                {
                    dgvLayers.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };
            dgvLayers.CellClick += DgvLayers_CellClick;

            lblLayerSummary = new WinFormsLabel
            {
                Dock = DockStyle.Bottom,
                Height = 26,
                Text = "Tổng số lớp xuất: 0 lớp | Tổng chiều dày kết cấu áo đường: 0.000 m",
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = WinFormsColor.FromArgb(24, 76, 120),
                TextAlign = ContentAlignment.BottomLeft
            };

            grpLayers.Controls.Add(dgvLayers);
            grpLayers.Controls.Add(lblLayerSummary);
            grpLayers.Controls.Add(pnlToolbar);

            // Add groups to pnlMain in correct dock order
            pnlMain.Controls.Add(grpLayers);
            pnlMain.Controls.Add(grpOptions);
            pnlMain.Controls.Add(grpSurface);

            // Form Closing & Resize Events (RULE 2)
            this.FormClosing += (s, e) => SaveCurrentSettings();
            this.ResizeEnd += (s, e) => _lastFormSize = this.Size;
        }

        private Button CreateToolbarButton(string text, int left, int width, WinFormsColor backColor, WinFormsColor foreColor)
        {
            var btn = new Button
            {
                Text = text,
                Left = left,
                Top = 3,
                Width = width,
                Height = 30,
                BackColor = backColor,
                ForeColor = foreColor,
                FlatStyle = FlatStyle.Flat,
                Font = new WinFormsFont("Segoe UI", 8.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        private string GetCurrentDefaultStructureName()
        {
            string s = (txtStructureName?.Text ?? string.Empty).Trim().ToUpperInvariant();
            return string.IsNullOrWhiteSpace(s) ? "ĐƯỜNG BTN" : s;
        }

        private void TxtStructureName_TextChanged(object? sender, EventArgs e)
        {
            if (_isUpdatingGrid || dgvLayers == null) return;
            ApplyStructureNameToAllRows();
        }

        private void ApplyStructureNameToAllRows()
        {
            if (dgvLayers == null) return;
            string structName = (txtStructureName.Text ?? string.Empty).Trim().ToUpperInvariant();

            _isUpdatingGrid = true;
            try
            {
                foreach (DataGridViewRow row in dgvLayers.Rows)
                {
                    row.Cells["colStructure"].Value = structName;
                }
            }
            finally
            {
                _isUpdatingGrid = false;
            }

            RefreshGridCalculatedColumns();
        }

        #region PERSISTENT STATE & DEFAULT DATA (RULE 2)

        public static List<PavementLayerItem> GetDefaultFiveLayers(string structureName = "ĐƯỜNG BTN")
        {
            string cleanStruct = string.IsNullOrWhiteSpace(structureName) ? "ĐƯỜNG BTN" : structureName.Trim().ToUpperInvariant();
            return new List<PavementLayerItem>
            {
                new PavementLayerItem { IsSelected = true, StructureName = cleanStruct, MaterialName = "BTN C16", Thickness = 0.06, AciColor = 1 },
                new PavementLayerItem { IsSelected = true, StructureName = cleanStruct, MaterialName = "BTN C19", Thickness = 0.07, AciColor = 30 },
                new PavementLayerItem { IsSelected = true, StructureName = cleanStruct, MaterialName = "CẤP PHỐI ĐÁ DĂM LOẠI 1", Thickness = 0.16, AciColor = 2 },
                new PavementLayerItem { IsSelected = true, StructureName = cleanStruct, MaterialName = "CẤP PHỐI ĐÁ DĂM LOẠI 2", Thickness = 0.32, AciColor = 3 },
                new PavementLayerItem { IsSelected = true, StructureName = cleanStruct, MaterialName = "ĐẮP ĐẤT K98", Thickness = 0.30, AciColor = 40 }
            };
        }

        private void RestoreLastSettings()
        {
            _isUpdatingGrid = true;
            try
            {
                txtStructureName.Text = string.IsNullOrWhiteSpace(_lastStructureName)
                    ? "ĐƯỜNG BTN"
                    : _lastStructureName.Trim().ToUpperInvariant();
            }
            finally
            {
                _isUpdatingGrid = false;
            }

            chkKeepTrailingZero.Checked = _lastKeepTrailingZero;
            chkUpdateExistingLayerColor.Checked = _lastUpdateExistingLayerColor;

            switch (_lastStackMode)
            {
                case PavementSolidStackMode.DirectFromSurfaceNoOffset:
                    radDirectNoOffset.Checked = true;
                    break;
                case PavementSolidStackMode.StackUpFromBottomSurface:
                    radStackUp.Checked = true;
                    break;
                default:
                    radStackDown.Checked = true;
                    break;
            }

            string defaultStruct = GetCurrentDefaultStructureName();
            if (_lastLayers == null || _lastLayers.Count == 0)
            {
                _lastLayers = GetDefaultFiveLayers(defaultStruct);
            }
            else
            {
                foreach (var item in _lastLayers)
                {
                    item.StructureName = string.IsNullOrWhiteSpace(item.StructureName)
                        ? defaultStruct
                        : item.StructureName.Trim().ToUpperInvariant();
                    item.MaterialName = (item.MaterialName ?? string.Empty).Trim().ToUpperInvariant();
                }
            }

            PopulateGridFromList(_lastLayers);
        }

        private void SaveCurrentSettings()
        {
            _lastFormSize = this.Size;
            _lastStructureName = (txtStructureName?.Text ?? "ĐƯỜNG BTN").Trim().ToUpperInvariant();
            _lastKeepTrailingZero = chkKeepTrailingZero.Checked;
            _lastUpdateExistingLayerColor = chkUpdateExistingLayerColor.Checked;
            _lastStackMode = GetSelectedStackMode();
            _lastLayers = ReadLayersFromGrid();

            if (_selectedSurfaceIds != null && _selectedSurfaceIds.Count > 0)
            {
                _lastSelectedSurfaceIds = _selectedSurfaceIds
                    .Where(id => !id.IsNull && id.IsValid && !id.IsErased)
                    .ToList();
            }
        }

        private PavementSolidStackMode GetSelectedStackMode()
        {
            if (radDirectNoOffset.Checked) return PavementSolidStackMode.DirectFromSurfaceNoOffset;
            if (radStackUp.Checked) return PavementSolidStackMode.StackUpFromBottomSurface;
            return PavementSolidStackMode.StackDownFromTopSurface;
        }

        #endregion

        #region SURFACE LOADING & PICKING

        private void LoadSurfacesList()
        {
            cboSurfaces.Items.Clear();
            List<ObjectId> validSavedIds = new List<ObjectId>();

            try
            {
                CivilDocument civDoc = CivilApplication.ActiveDocument;
                if (civDoc != null)
                {
                    using (Transaction tr = _db.TransactionManager.StartTransaction())
                    {
                        // Kiểm tra các mặt phẳng đã lưu từ lần trước
                        if (_lastSelectedSurfaceIds != null)
                        {
                            foreach (ObjectId id in _lastSelectedSurfaceIds)
                            {
                                if (!id.IsNull && id.IsValid && !id.IsErased && tr.GetObject(id, OpenMode.ForWrite) is CivSurface)
                                {
                                    validSavedIds.Add(id);
                                }
                            }
                        }

                        // Nếu lần trước chọn nhiều mặt phẳng (> 1), thêm mục tổng hợp lên đầu
                        if (validSavedIds.Count > 1)
                        {
                            cboSurfaces.Items.Add(new SurfaceDropdownItem
                            {
                                SurfaceIds = new List<ObjectId>(validSavedIds),
                                DisplayText = $"[Đã chọn {validSavedIds.Count} mặt phẳng TIN Surface trên bản vẽ]"
                            });
                        }

                        ObjectIdCollection surfIds = civDoc.GetSurfaceIds();
                        foreach (ObjectId sId in surfIds)
                        {
                            if (tr.GetObject(sId, OpenMode.ForWrite) is CivSurface tinSurf)
                            {
                                cboSurfaces.Items.Add(new SurfaceDropdownItem
                                {
                                    SurfaceIds = new List<ObjectId> { sId },
                                    DisplayText = tinSurf.Name
                                });
                            }
                        }

                        tr.Commit();
                    }
                }
            }
            catch { }

            if (cboSurfaces.Items.Count > 0)
            {
                int selectIdx = 0;
                if (validSavedIds.Count == 1)
                {
                    for (int i = 0; i < cboSurfaces.Items.Count; i++)
                    {
                        if (cboSurfaces.Items[i] is SurfaceDropdownItem item &&
                            item.SurfaceIds.Count == 1 &&
                            item.SurfaceIds[0] == validSavedIds[0])
                        {
                            selectIdx = i;
                            break;
                        }
                    }
                }
                cboSurfaces.SelectedIndex = selectIdx;
            }
            else
            {
                _selectedSurfaceIds.Clear();
                lblSurfaceInfo.Text = "⚠️ Bản vẽ hiện tại chưa có TIN Surface nào.";
            }
        }

        private void CboSurfaces_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (cboSurfaces.SelectedItem is SurfaceDropdownItem item)
            {
                _selectedSurfaceIds = new List<ObjectId>(item.SurfaceIds);
                UpdateSurfaceInfoLabel();
            }
        }

        private void BtnPickSurface_Click(object? sender, EventArgs e)
        {
            List<ObjectId> pickedIds = new List<ObjectId>();

            using (EditorUserInteraction interaction = _ed.StartUserInteraction(this))
            {
                PromptEntityOptions peo = new PromptEntityOptions("\nChọn mặt phẳng TIN Surface trên cùng của mặt đường (hoặc nhấn Enter để quét chọn nhiều mặt phẳng): ");
                peo.SetRejectMessage("\nĐối tượng chọn phải là TIN Surface!");
                peo.AddAllowedClass(typeof(CivSurface), true);
                peo.AllowNone = true;

                PromptEntityResult per = _ed.GetEntity(peo);
                if (per.Status == PromptStatus.OK && !per.ObjectId.IsNull)
                {
                    pickedIds.Add(per.ObjectId);
                }
                else if (per.Status == PromptStatus.None)
                {
                    PromptSelectionOptions pso = new PromptSelectionOptions
                    {
                        MessageForAdding = "\nQuét chọn các mặt phẳng TIN Surface: "
                    };
                    SelectionFilter filter = new SelectionFilter(new TypedValue[]
                    {
                        new TypedValue((int)DxfCode.Start, "AECC_TIN_SURFACE")
                    });
                    PromptSelectionResult psr = _ed.GetSelection(pso, filter);
                    if (psr.Status == PromptStatus.OK && psr.Value != null)
                    {
                        pickedIds.AddRange(psr.Value.GetObjectIds());
                    }
                }
            }

            if (pickedIds.Count == 0) return;

            _selectedSurfaceIds = pickedIds;
            _lastSelectedSurfaceIds = new List<ObjectId>(pickedIds);

            if (pickedIds.Count == 1)
            {
                for (int i = 0; i < cboSurfaces.Items.Count; i++)
                {
                    if (cboSurfaces.Items[i] is SurfaceDropdownItem item &&
                        item.SurfaceIds.Count == 1 &&
                        item.SurfaceIds[0] == pickedIds[0])
                    {
                        cboSurfaces.SelectedIndex = i;
                        return;
                    }
                }
            }

            // Nếu chọn nhiều mặt phẳng
            LoadSurfacesList();
        }

        private void UpdateSurfaceInfoLabel()
        {
            if (_selectedSurfaceIds == null || _selectedSurfaceIds.Count == 0)
            {
                lblSurfaceInfo.Text = "Chưa chọn mặt phẳng.";
                return;
            }

            try
            {
                using (Transaction tr = _db.TransactionManager.StartTransaction())
                {
                    if (_selectedSurfaceIds.Count == 1)
                    {
                        if (tr.GetObject(_selectedSurfaceIds[0], OpenMode.ForWrite) is CivSurface surf)
                        {
                            var genProps = surf.GetGeneralProperties();
                            var tinProps = surf.GetTinProperties();
                            var terrainProps = surf.GetTerrainProperties();
                            lblSurfaceInfo.Text = $"✅ Mặt phẳng: {surf.Name} | Tam giác: {tinProps.NumberOfTriangles:N0} | Diện tích 2D: {terrainProps.SurfaceArea2D:N2} m² | Cao độ Z: {genProps.MinimumElevation:F2}m → {genProps.MaximumElevation:F2}m";
                        }
                    }
                    else
                    {
                        List<string> names = new List<string>();
                        double totalArea2D = 0;
                        foreach (ObjectId id in _selectedSurfaceIds)
                        {
                            if (tr.GetObject(id, OpenMode.ForWrite) is CivSurface surf)
                            {
                                names.Add(surf.Name);
                                try { totalArea2D += surf.GetTerrainProperties().SurfaceArea2D; } catch { }
                            }
                        }
                        lblSurfaceInfo.Text = $"✅ Đã chọn {_selectedSurfaceIds.Count} mặt phẳng ({string.Join(", ", names)}) | Tổng diện tích 2D: {totalArea2D:N2} m²";
                    }
                    tr.Commit();
                }
            }
            catch
            {
                lblSurfaceInfo.Text = $"Đã chọn {_selectedSurfaceIds.Count} mặt phẳng TIN Surface.";
            }
        }

        #endregion

        #region DATAGRIDVIEW MANAGEMENT & LAYER NAME FORMATTING

        private void PopulateGridFromList(List<PavementLayerItem> items)
        {
            _isUpdatingGrid = true;
            try
            {
                string defaultStruct = GetCurrentDefaultStructureName();
                dgvLayers.Rows.Clear();
                foreach (var item in items)
                {
                    int rowIdx = dgvLayers.Rows.Add();
                    DataGridViewRow row = dgvLayers.Rows[rowIdx];
                    row.Cells["colSelect"].Value = item.IsSelected;
                    string structVal = string.IsNullOrWhiteSpace(item.StructureName)
                        ? defaultStruct
                        : item.StructureName.Trim().ToUpperInvariant();
                    row.Cells["colStructure"].Value = structVal;
                    row.Cells["colMaterial"].Value = (item.MaterialName ?? string.Empty).Trim().ToUpperInvariant();
                    row.Cells["colThickness"].Value = FormatThicknessValue(item.Thickness, chkKeepTrailingZero.Checked);
                    row.Cells["colColor"].Tag = item.AciColor;
                    ApplyColorCellStyle(row.Cells["colColor"], item.AciColor);
                }
            }
            finally
            {
                _isUpdatingGrid = false;
            }

            RefreshGridCalculatedColumns();
        }

        public static string FormatThicknessValue(double thickness, bool keepTwoDecimals)
        {
            string format = keepTwoDecimals ? "0.00#" : "0.###";
            return Math.Abs(thickness).ToString(format, CultureInfo.InvariantCulture);
        }

        public static string BuildLayerName(string structureName, string materialName, double thickness, bool keepTwoDecimals)
        {
            string cleanStruct = SanitizeLayerPart(structureName).ToUpperInvariant();
            string cleanMat = SanitizeLayerPart(materialName).ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(cleanMat))
            {
                cleanMat = "LOP KET CAU";
            }
            string thkStr = FormatThicknessValue(thickness, keepTwoDecimals);

            if (!string.IsNullOrWhiteSpace(cleanStruct))
            {
                return $"{cleanStruct}_{cleanMat}_{thkStr}";
            }
            return $"{cleanMat}_{thkStr}";
        }

        /// <summary>
        /// Loại bỏ các ký tự bị cấm trong tên Layer của AutoCAD (< > / \ " : ; ? * | , = `)
        /// </summary>
        public static string SanitizeLayerPart(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            string s = input.Trim().ToUpperInvariant();
            char[] invalidChars = new char[] { '<', '>', '/', '\\', '"', ':', ';', '?', '*', '|', ',', '=', '`' };
            foreach (char c in invalidChars)
            {
                s = s.Replace(c, '-');
            }
            while (s.Contains("  "))
            {
                s = s.Replace("  ", " ");
            }
            return s.Trim();
        }

        private void ApplyColorCellStyle(DataGridViewCell cell, ushort aciColor)
        {
            if (aciColor < 1 || aciColor > 255) aciColor = 1;
            cell.Tag = aciColor;
            cell.Value = $"Màu {aciColor}";

            WinFormsColor bg = GetPreviewColorFromAci(aciColor);
            cell.Style.BackColor = bg;
            cell.Style.SelectionBackColor = bg;

            double luminance = (0.299 * bg.R + 0.587 * bg.G + 0.114 * bg.B);
            WinFormsColor fg = luminance > 145 ? WinFormsColor.Black : WinFormsColor.White;
            cell.Style.ForeColor = fg;
            cell.Style.SelectionForeColor = fg;
        }

        private static WinFormsColor GetPreviewColorFromAci(ushort aci)
        {
            try
            {
                var acadCol = Autodesk.AutoCAD.Colors.Color.FromColorIndex(ColorMethod.ByAci, (short)aci);
                System.Drawing.Color c = acadCol.ColorValue;
                if (c.A > 0) return c;
            }
            catch { }

            return aci switch
            {
                1 => WinFormsColor.FromArgb(220, 40, 40),
                2 => WinFormsColor.FromArgb(235, 215, 30),
                3 => WinFormsColor.FromArgb(40, 185, 60),
                4 => WinFormsColor.FromArgb(40, 200, 215),
                5 => WinFormsColor.FromArgb(45, 95, 220),
                6 => WinFormsColor.FromArgb(205, 50, 195),
                7 => WinFormsColor.FromArgb(210, 210, 210),
                30 => WinFormsColor.FromArgb(240, 130, 30),
                40 => WinFormsColor.FromArgb(215, 165, 55),
                _ => WinFormsColor.FromArgb(120, 150, 190)
            };
        }

        private void RefreshGridCalculatedColumns()
        {
            if (_isUpdatingGrid || dgvLayers == null) return;
            _isUpdatingGrid = true;

            try
            {
                bool keepTwoDecimals = chkKeepTrailingZero.Checked;
                PavementSolidStackMode stackMode = GetSelectedStackMode();
                string defaultStruct = (txtStructureName?.Text ?? string.Empty).Trim().ToUpperInvariant();

                double cumulativeOffset = 0.0;
                int activeCount = 0;

                for (int i = 0; i < dgvLayers.Rows.Count; i++)
                {
                    DataGridViewRow row = dgvLayers.Rows[i];
                    row.Cells["colOrder"].Value = $"Lớp {i + 1}";

                    bool isSelected = row.Cells["colSelect"].Value is bool b && b;
                    string structName = (Convert.ToString(row.Cells["colStructure"].Value)?.Trim() ?? "").ToUpperInvariant();
                    if (string.IsNullOrWhiteSpace(structName))
                    {
                        structName = defaultStruct;
                    }
                    row.Cells["colStructure"].Value = structName;

                    string matName = (Convert.ToString(row.Cells["colMaterial"].Value)?.Trim() ?? "").ToUpperInvariant();
                    row.Cells["colMaterial"].Value = matName;
                    string thkRaw = Convert.ToString(row.Cells["colThickness"].Value)?.Trim() ?? "0";

                    if (!TryParsePositiveThickness(thkRaw, out double thk))
                    {
                        thk = 0.0;
                    }

                    string layerName = BuildLayerName(structName, matName, thk, keepTwoDecimals);
                    row.Cells["colLayerName"].Value = layerName;

                    if (isSelected && thk > 1e-5)
                    {
                        activeCount++;
                        if (stackMode == PavementSolidStackMode.StackDownFromTopSurface)
                        {
                            double topZ = -cumulativeOffset;
                            double botZ = -(cumulativeOffset + thk);
                            row.Cells["colElevRange"].Value = $"{topZ:F3} → {botZ:F3}";
                        }
                        else if (stackMode == PavementSolidStackMode.StackUpFromBottomSurface)
                        {
                            double botZ = cumulativeOffset;
                            double topZ = cumulativeOffset + thk;
                            row.Cells["colElevRange"].Value = $"+{botZ:F3} → +{topZ:F3}";
                        }
                        else
                        {
                            row.Cells["colElevRange"].Value = $"0.000 → -{thk:F3}";
                        }

                        cumulativeOffset += thk;
                    }
                    else
                    {
                        row.Cells["colElevRange"].Value = "(Bỏ qua)";
                    }
                }

                lblLayerSummary.Text = $"Tổng số lớp xuất: {activeCount} lớp | Tổng chiều dày kết cấu áo đường: {cumulativeOffset:F3} m ({cumulativeOffset * 100.0:F1} cm)";
            }
            finally
            {
                _isUpdatingGrid = false;
            }
        }

        public static bool TryParsePositiveThickness(string text, out double thickness)
        {
            thickness = 0.0;
            if (string.IsNullOrWhiteSpace(text)) return false;

            string s = text.Trim().ToLowerInvariant();
            bool isCm = false;
            bool isMm = false;

            if (s.EndsWith("cm"))
            {
                isCm = true;
                s = s.Substring(0, s.Length - 2).Trim();
            }
            else if (s.EndsWith("mm"))
            {
                isMm = true;
                s = s.Substring(0, s.Length - 2).Trim();
            }
            else if (s.EndsWith("m"))
            {
                s = s.Substring(0, s.Length - 1).Trim();
            }

            s = s.Replace(',', '.');
            if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
            {
                val = Math.Abs(val);
                if (isCm) val /= 100.0;
                else if (isMm) val /= 1000.0;
                thickness = Math.Round(val, 4);
                return thickness > 0;
            }
            return false;
        }

        private void DgvLayers_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (_isUpdatingGrid || e.RowIndex < 0) return;
            RefreshGridCalculatedColumns();
        }

        private void DgvLayers_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            if (dgvLayers.Columns[e.ColumnIndex].Name == "colColor")
            {
                DataGridViewCell cell = dgvLayers.Rows[e.RowIndex].Cells["colColor"];
                ushort currentAci = cell.Tag is ushort u ? u : (ushort)1;

                var dlg = new Autodesk.AutoCAD.Windows.ColorDialog
                {
                    Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(ColorMethod.ByAci, (short)currentAci),
                    IncludeByBlockByLayer = false
                };

                if (dlg.ShowDialog() == DialogResult.OK && dlg.Color != null)
                {
                    short idx = dlg.Color.ColorIndex;
                    if (idx < 1 || idx > 255) idx = 1;
                    ApplyColorCellStyle(cell, (ushort)idx);
                }
            }
        }

        private void BtnAddRow_Click(object? sender, EventArgs e)
        {
            ushort[] defaultColors = { 1, 30, 2, 3, 40, 4, 5, 6, 140, 200 };
            ushort nextColor = defaultColors[dgvLayers.Rows.Count % defaultColors.Length];

            int idx = dgvLayers.Rows.Add();
            DataGridViewRow row = dgvLayers.Rows[idx];
            row.Cells["colSelect"].Value = true;
            row.Cells["colStructure"].Value = GetCurrentDefaultStructureName();
            row.Cells["colMaterial"].Value = $"LỚP VẬT LIỆU {idx + 1}";
            row.Cells["colThickness"].Value = "0.10";
            ApplyColorCellStyle(row.Cells["colColor"], nextColor);

            RefreshGridCalculatedColumns();
            dgvLayers.ClearSelection();
            row.Selected = true;
        }

        private void BtnRemoveRow_Click(object? sender, EventArgs e)
        {
            if (dgvLayers.SelectedRows.Count == 0) return;
            int idx = dgvLayers.SelectedRows[0].Index;
            dgvLayers.Rows.RemoveAt(idx);
            RefreshGridCalculatedColumns();
        }

        private void MoveSelectedRow(int direction)
        {
            if (dgvLayers.SelectedRows.Count == 0) return;
            int idx = dgvLayers.SelectedRows[0].Index;
            int newIdx = idx + direction;
            if (newIdx < 0 || newIdx >= dgvLayers.Rows.Count) return;

            var items = ReadLayersFromGrid();
            var temp = items[idx];
            items.RemoveAt(idx);
            items.Insert(newIdx, temp);

            PopulateGridFromList(items);
            dgvLayers.ClearSelection();
            dgvLayers.Rows[newIdx].Selected = true;
        }

        private List<PavementLayerItem> ReadLayersFromGrid()
        {
            try
            {
                if (dgvLayers != null && dgvLayers.IsCurrentCellInEditMode)
                {
                    dgvLayers.EndEdit();
                }
            }
            catch { }

            List<PavementLayerItem> list = new List<PavementLayerItem>();
            if (dgvLayers == null) return list;

            string defaultStruct = (txtStructureName?.Text ?? string.Empty).Trim().ToUpperInvariant();

            foreach (DataGridViewRow row in dgvLayers.Rows)
            {
                bool isSel = row.Cells["colSelect"].Value is bool b && b;
                string structName = (Convert.ToString(row.Cells["colStructure"].Value)?.Trim() ?? "").ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(structName))
                {
                    structName = defaultStruct;
                }
                string mat = (Convert.ToString(row.Cells["colMaterial"].Value)?.Trim() ?? "").ToUpperInvariant();
                string thkStr = Convert.ToString(row.Cells["colThickness"].Value)?.Trim() ?? "0";
                TryParsePositiveThickness(thkStr, out double thk);
                ushort aci = row.Cells["colColor"].Tag is ushort u ? u : (ushort)1;

                list.Add(new PavementLayerItem
                {
                    IsSelected = isSel,
                    StructureName = structName,
                    MaterialName = mat,
                    Thickness = thk,
                    AciColor = aci
                });
            }
            return list;
        }

        #endregion

        #region EXCEL IMPORT & EXPORT

        private void BtnImportExcel_Click(object? sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "Chọn file Excel thông số các lớp kết cấu áo đường";
                ofd.Filter = "Excel Workbook (*.xlsx;*.xlsm)|*.xlsx;*.xlsm|All Files (*.*)|*.*";
                if (!string.IsNullOrEmpty(_lastExcelPath) && File.Exists(_lastExcelPath))
                {
                    ofd.InitialDirectory = Path.GetDirectoryName(_lastExcelPath);
                    ofd.FileName = Path.GetFileName(_lastExcelPath);
                }

                if (ofd.ShowDialog(this) != DialogResult.OK) return;
                _lastExcelPath = ofd.FileName;

                try
                {
                    List<PavementLayerItem> imported = new List<PavementLayerItem>();
                    ushort[] defaultColors = { 1, 30, 2, 3, 40, 4, 5, 6, 140, 200 };
                    string fallbackStruct = GetCurrentDefaultStructureName();

                    using (var fs = new FileStream(ofd.FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var wb = new XLWorkbook(fs))
                    {
                        var ws = wb.Worksheets.FirstOrDefault();
                        if (ws == null)
                        {
                            MessageBox.Show(this, "File Excel không có sheet dữ liệu nào!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        var rangeUsed = ws.RangeUsed();
                        if (rangeUsed == null)
                        {
                            MessageBox.Show(this, "Sheet Excel trống!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        int firstRow = rangeUsed.FirstRow().RowNumber();
                        int lastRow = rangeUsed.LastRow().RowNumber();
                        int firstCol = rangeUsed.FirstColumn().ColumnNumber();
                        int lastCol = rangeUsed.LastColumn().ColumnNumber();

                        int colStruct = -1;
                        int colMat = -1;
                        int colThk = -1;
                        int colColor = -1;
                        int startDataRow = firstRow;

                        // Tự động nhận diện dòng tiêu đề (Header)
                        for (int r = firstRow; r <= Math.Min(firstRow + 4, lastRow); r++)
                        {
                            for (int c = firstCol; c <= lastCol; c++)
                            {
                                string cellText = ws.Cell(r, c).GetString().Trim().ToLowerInvariant();
                                if (string.IsNullOrEmpty(cellText) || cellText.Contains("layer")) continue;

                                if (colStruct == -1 && (cellText.Contains("cấu kiện") || cellText.Contains("cau kien") || cellText.Contains("hạng mục") || cellText.Contains("hang muc") || cellText == "structure"))
                                {
                                    colStruct = c;
                                }
                                else if (colMat == -1 && (cellText.Contains("vật liệu") || cellText.Contains("vat lieu") || cellText.Contains("tên lớp") || cellText.Contains("ten lop") || cellText.Contains("kết cấu") || cellText == "tên" || cellText == "material"))
                                {
                                    colMat = c;
                                }
                                else if (colThk == -1 && (cellText.Contains("độ dày") || cellText.Contains("do day") || cellText.Contains("bề dày") || cellText.Contains("be day") || cellText.Contains("chiều dày") || cellText.Contains("dày") || cellText.Contains("thickness")))
                                {
                                    colThk = c;
                                }
                                else if (colColor == -1 && (cellText.Contains("màu") || cellText.Contains("mau") || cellText.Contains("aci") || cellText.Contains("color")))
                                {
                                    colColor = c;
                                }
                            }

                            if (colMat != -1 && colThk != -1)
                            {
                                startDataRow = r + 1;
                                break;
                            }
                        }

                        // Nếu không có header chuẩn, tự động suy luận cột
                        if (colMat == -1 || colThk == -1)
                        {
                            string c2 = ws.Cell(firstRow, firstCol + 1).GetString().Trim();
                            if (!TryParsePositiveThickness(c2, out _) && firstRow < lastRow)
                            {
                                startDataRow = firstRow + 1;
                            }

                            if (lastCol - firstCol + 1 >= 4 &&
                                TryParsePositiveThickness(ws.Cell(startDataRow, firstCol + 3).GetString(), out _))
                            {
                                // STT - Tên cấu kiện - Tên vật liệu - Độ dày
                                colStruct = firstCol + 1;
                                colMat = firstCol + 2;
                                colThk = firstCol + 3;
                                if (lastCol >= firstCol + 4) colColor = firstCol + 4;
                            }
                            else if (lastCol - firstCol + 1 >= 3 &&
                                TryParsePositiveThickness(ws.Cell(startDataRow, firstCol + 2).GetString(), out _))
                            {
                                // STT (hoặc Tên cấu kiện) - Tên vật liệu - Độ dày
                                string firstCellVal = ws.Cell(startDataRow, firstCol).GetString().Trim();
                                if (!int.TryParse(firstCellVal, out _))
                                {
                                    colStruct = firstCol;
                                }
                                colMat = firstCol + 1;
                                colThk = firstCol + 2;
                                if (lastCol >= firstCol + 3) colColor = firstCol + 3;
                            }
                            else
                            {
                                colMat = firstCol;
                                colThk = firstCol + 1;
                                if (lastCol >= firstCol + 2) colColor = firstCol + 2;
                            }
                        }

                        for (int r = startDataRow; r <= lastRow; r++)
                        {
                            string structName = colStruct != -1
                                ? ws.Cell(r, colStruct).GetString().Trim().ToUpperInvariant()
                                : fallbackStruct;
                            if (string.IsNullOrWhiteSpace(structName))
                            {
                                structName = fallbackStruct;
                            }

                            string matName = ws.Cell(r, colMat).GetString().Trim().ToUpperInvariant();
                            string thkStr = ws.Cell(r, colThk).GetString().Trim();

                            if (string.IsNullOrWhiteSpace(matName) && string.IsNullOrWhiteSpace(thkStr))
                                continue;

                            if (!TryParsePositiveThickness(thkStr, out double thk))
                            {
                                if (ws.Cell(r, colThk).TryGetValue<double>(out double dblVal) && dblVal > 0)
                                {
                                    thk = Math.Round(dblVal, 4);
                                }
                            }

                            if (string.IsNullOrWhiteSpace(matName) || thk <= 0)
                                continue;

                            ushort aci = defaultColors[imported.Count % defaultColors.Length];
                            if (colColor != -1)
                            {
                                string colStr = ws.Cell(r, colColor).GetString().Trim();
                                if (ushort.TryParse(colStr, out ushort parsedAci) && parsedAci >= 1 && parsedAci <= 255)
                                {
                                    aci = parsedAci;
                                }
                            }

                            imported.Add(new PavementLayerItem
                            {
                                IsSelected = true,
                                StructureName = structName,
                                MaterialName = matName,
                                Thickness = thk,
                                AciColor = aci
                            });
                        }
                    }

                    if (imported.Count == 0)
                    {
                        MessageBox.Show(this,
                            "Không tìm thấy dòng dữ liệu hợp lệ nào trong file Excel!\n\n" +
                            "Cấu trúc file Excel cần có cột 'Tên cấu kiện' (tùy chọn), 'Tên vật liệu' và 'Độ dày (m)'.",
                            "Không có dữ liệu",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        return;
                    }

                    if (!string.IsNullOrWhiteSpace(imported[0].StructureName))
                    {
                        _isUpdatingGrid = true;
                        try
                        {
                            txtStructureName.Text = imported[0].StructureName;
                        }
                        finally
                        {
                            _isUpdatingGrid = false;
                        }
                    }

                    PopulateGridFromList(imported);
                    lblStatus.Text = $"✅ Đã nhập thành công {imported.Count} lớp kết cấu áo đường từ file '{Path.GetFileName(ofd.FileName)}'.";
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show(this, $"Lỗi khi đọc file Excel:\n{ex.Message}", "Lỗi Nhập Excel", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnExportExcel_Click(object? sender, EventArgs e)
        {
            var items = ReadLayersFromGrid();
            if (items.Count == 0)
            {
                MessageBox.Show(this, "Bảng thông số đang trống, không có dữ liệu để xuất!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Title = "Xuất thông số kết cấu áo đường ra file Excel";
                sfd.Filter = "Excel Workbook (*.xlsx)|*.xlsx";
                sfd.FileName = !string.IsNullOrEmpty(_lastExcelPath)
                    ? Path.GetFileName(_lastExcelPath)
                    : "ThongSo_KetCauAoDuong.xlsx";

                if (!string.IsNullOrEmpty(_lastExcelPath))
                {
                    string? dir = Path.GetDirectoryName(_lastExcelPath);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                        sfd.InitialDirectory = dir;
                }

                if (sfd.ShowDialog(this) != DialogResult.OK) return;
                _lastExcelPath = sfd.FileName;

                try
                {
                    bool keepTwoDecimals = chkKeepTrailingZero.Checked;
                    using (var wb = new XLWorkbook())
                    {
                        var ws = wb.Worksheets.Add("KetCauAoDuong");

                        // Header
                        ws.Cell(1, 1).Value = "STT";
                        ws.Cell(1, 2).Value = "Tên cấu kiện";
                        ws.Cell(1, 3).Value = "Tên vật liệu";
                        ws.Cell(1, 4).Value = "Độ dày (m)";
                        ws.Cell(1, 5).Value = "Màu ACI";
                        ws.Cell(1, 6).Value = "Tên Layer (TÊN CẤU KIỆN_TÊN VẬT LIỆU_ĐỘ DÀY)";

                        var headerRange = ws.Range(1, 1, 1, 6);
                        headerRange.Style.Font.Bold = true;
                        headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(24, 76, 120);
                        headerRange.Style.Font.FontColor = XLColor.White;
                        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                        for (int i = 0; i < items.Count; i++)
                        {
                            var item = items[i];
                            int r = i + 2;
                            ws.Cell(r, 1).Value = i + 1;
                            ws.Cell(r, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                            ws.Cell(r, 2).Value = item.StructureName;
                            ws.Cell(r, 3).Value = item.MaterialName;

                            ws.Cell(r, 4).Value = item.Thickness;
                            ws.Cell(r, 4).Style.NumberFormat.Format = keepTwoDecimals ? "0.00#" : "0.###";

                            ws.Cell(r, 5).Value = item.AciColor;
                            ws.Cell(r, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                            ws.Cell(r, 6).Value = BuildLayerName(item.StructureName, item.MaterialName, item.Thickness, keepTwoDecimals);
                        }

                        var fullRange = ws.Range(1, 1, items.Count + 1, 6);
                        fullRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        fullRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                        ws.Columns().AdjustToContents();
                        wb.SaveAs(sfd.FileName);
                    }

                    lblStatus.Text = $"✅ Đã xuất {items.Count} lớp kết cấu áo đường ra file '{Path.GetFileName(sfd.FileName)}'.";

                    var askOpen = MessageBox.Show(
                        this,
                        $"Đã xuất file Excel thành công:\n{sfd.FileName}\n\nBạn có muốn mở file Excel này ngay không?",
                        "Xuất Excel Thành Công",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information);

                    if (askOpen == DialogResult.Yes)
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(sfd.FileName)
                        {
                            UseShellExecute = true
                        });
                    }
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show(this, $"Lỗi khi xuất file Excel:\n{ex.Message}", "Lỗi Xuất Excel", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        #endregion

        #region EXECUTION

        private void BtnExecute_Click(object? sender, EventArgs e)
        {
            if (_selectedSurfaceIds == null || _selectedSurfaceIds.Count == 0)
            {
                MessageBox.Show(this, "Vui lòng chọn ít nhất 1 mặt phẳng TIN Surface làm mặt trên cùng của mặt đường!", "Thiếu Mặt Phẳng Nguồn", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var allLayers = ReadLayersFromGrid();
            var activeLayers = allLayers.Where(x => x.IsSelected && !string.IsNullOrWhiteSpace(x.MaterialName) && x.Thickness > 1e-5).ToList();

            if (activeLayers.Count == 0)
            {
                MessageBox.Show(this, "Vui lòng tích chọn ít nhất 1 lớp kết cấu áo đường có tên vật liệu và độ dày > 0!", "Thiếu Thông Số Lớp", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SaveCurrentSettings();

            progressBar.Visible = true;
            progressBar.Style = ProgressBarStyle.Continuous;
            progressBar.Minimum = 0;
            progressBar.Maximum = Math.Max(1, _selectedSurfaceIds.Count * activeLayers.Count);
            progressBar.Value = 0;
            btnExecute.Enabled = false;
            this.Cursor = Cursors.WaitCursor;

            try
            {
                var result = CTT_Trich_Xuat_Solid_Ket_Cau_Ao_Duong_Command.ThucHienTrichXuatSolid(
                    _db,
                    _ed,
                    _selectedSurfaceIds,
                    activeLayers,
                    GetSelectedStackMode(),
                    chkKeepTrailingZero.Checked,
                    chkUpdateExistingLayerColor.Checked,
                    (step, statusText) =>
                    {
                        if (step <= progressBar.Maximum) progressBar.Value = step;
                        lblStatus.Text = statusText;
                        System.Windows.Forms.Application.DoEvents();
                    });

                if (result.Success)
                {
                    lblStatus.Text = $"🎉 Hoàn tất! Đã tạo {result.TotalSolidsCreated} đối tượng 3D Solid cho {activeLayers.Count} lớp kết cấu áo đường.";
                    MessageBox.Show(
                        this,
                        result.Message,
                        "Trích Xuất 3D Solid Thành Công",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                else
                {
                    lblStatus.Text = $"❌ Lỗi: {result.Message}";
                    MessageBox.Show(this, result.Message, "Lỗi Trích Xuất", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            finally
            {
                this.Cursor = Cursors.Default;
                btnExecute.Enabled = true;
                progressBar.Visible = false;
            }
        }

        #endregion
    }
}

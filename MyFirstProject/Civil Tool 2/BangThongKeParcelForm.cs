using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using ATable = Autodesk.AutoCAD.DatabaseServices.Table;
using CivilParcel = Autodesk.Civil.DatabaseServices.Parcel;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsFont = System.Drawing.Font;
using WinFormsPoint = System.Drawing.Point;
using DrawingFont = System.Drawing.Font;
using DrawingColor = System.Drawing.Color;
using ClosedXML.Excel;
using MyFirstProject.Extensions;

namespace MyFirstProject.Civil_Tool_2
{
    /// <summary>
    /// Model chứa toàn bộ thông tin thuộc tính của Parcel (thửa đất)
    /// </summary>
    public class ParcelInfo
    {
        public ObjectId ParcelId { get; set; }
        public int SoThuTu { get; set; }
        public int SoThua { get; set; }
        public string TenThua { get; set; } = "";
        public string PhanKhu { get; set; } = "";
        public double DienTich { get; set; }
        public double DienTichHa => DienTich / 10000.0;
        public double ToaDoX { get; set; }
        public double ToaDoY { get; set; }
        public string KieuStyle { get; set; } = "";
        public string MaThue { get; set; } = "";
        public string MoTa { get; set; } = "";
        public Extents3d? Bounds { get; set; }
    }

    /// <summary>
    /// Định nghĩa cấu hình cột xuất
    /// </summary>
    public class ColumnExportConfig
    {
        public string Key { get; set; } = "";
        public string HeaderName { get; set; } = "";
        public double DefaultWidth { get; set; } = 30.0;
        public CellAlignment Alignment { get; set; } = CellAlignment.MiddleCenter;
        public XLAlignmentHorizontalValues ExcelAlign { get; set; } = XLAlignmentHorizontalValues.Center;
        public bool IsDefaultChecked { get; set; } = true;
        public Func<ParcelInfo, string> GetValue { get; set; } = null!;
        public Action<IXLCell, ParcelInfo> SetExcelValue { get; set; } = null!;
        public bool IsNumericSummary { get; set; } = false;
        public Func<List<ParcelInfo>, string>? GetTotalValue { get; set; }
    }

    /// <summary>
    /// Giao diện thống kê và xuất bảng thuộc tính Parcel
    /// Hỗ trợ:
    /// - Ưu tiên chọn: STT, Số thửa, Tên thửa đất, Diện tích
    /// - Di chuyển thứ tự CỘT (Lên / Xuống)
    /// - Di chuyển thứ tự HÀNG (Lên / Xuống) & Sắp xếp tự động theo cột
    /// - Ghi nhớ toàn bộ thông số và tùy chọn đã thiết lập trước đó
    /// </summary>
    public class BangThongKeParcelForm : Form
    {
        // ================= GHI NHỚ THÔNG SỐ (STATIC VARIABLES) =================
        private static string _lastTitle = "BẢNG THỐNG KÊ THUỘC TÍNH PARCEL (THỬA ĐẤT)";
        private static decimal _lastTextHeight = 2.5m;
        private static decimal _lastTitleHeight = 4.0m;
        private static decimal _lastRowHeight = 8.0m;
        private static bool _lastIncludeTotal = true;
        private static bool _lastAutoRenumber = true;
        private static string _lastExportDir = "";
        private static int _lastSourceType = 0; // 0: All, 1: Site, 2: Screen
        private static string _lastSelectedSiteName = "";
        private static HashSet<string>? _lastCheckedColumnKeys = null;
        private static List<string>? _lastColumnOrderKeys = null;
        private static Size? _lastFormSize = null;
        private static List<ParcelInfo>? _lastScreenParcels = null;

        // Dữ liệu
        private List<ParcelInfo> _allParcels = new List<ParcelInfo>();
        private List<ParcelInfo> _filteredParcels = new List<ParcelInfo>();
        private List<ColumnExportConfig> _availableColumns = new List<ColumnExportConfig>();
        private Dictionary<string, ObjectId> _siteMap = new Dictionary<string, ObjectId>();

        private string _currentSortKey = "";
        private bool _isSortAscending = true;

        // Event callbacks cho tương tác với AutoCAD canvas
        public Func<List<ParcelInfo>>? OnReloadAllParcels { get; set; }
        public Func<ObjectId, List<ParcelInfo>>? OnFilterBySite { get; set; }
        public Func<List<ParcelInfo>>? OnSelectOnScreen { get; set; }
        public Action<ObjectId, Extents3d?>? OnZoomToParcel { get; set; }
        public Action<List<ParcelInfo>, List<ColumnExportConfig>, string, double, double, double, bool>? OnDrawTable { get; set; }

        // Controls
        private GroupBox grpSource = null!;
        private RadioButton radAll = null!;
        private RadioButton radSite = null!;
        private ComboBox cmbSites = null!;
        private Button btnSelectScreen = null!;
        private Button btnReload = null!;
        private TextBox txtSearch = null!;
        private WinFormsLabel lblSearch = null!;

        private GroupBox grpGrid = null!;
        private DataGridView dgvData = null!;
        private Button btnZoom = null!;
        private Button btnMoveRowUp = null!;
        private Button btnMoveRowDown = null!;
        private WinFormsLabel lblSort = null!;
        private ComboBox cmbSort = null!;
        private CheckBox chkAutoRenumber = null!;

        private GroupBox grpSummary = null!;
        private WinFormsLabel lblTotalCount = null!;
        private WinFormsLabel lblTotalArea = null!;
        private WinFormsLabel lblAvgArea = null!;
        private WinFormsLabel lblMinMaxArea = null!;

        private GroupBox grpColumns = null!;
        private CheckedListBox chkListColumns = null!;
        private Button btnMoveColUp = null!;
        private Button btnMoveColDown = null!;
        private Button btnPresetEssential = null!;
        private Button btnSelectAllCols = null!;
        private Button btnDeselectAllCols = null!;

        private GroupBox grpTableSettings = null!;
        private WinFormsLabel lblTitle = null!;
        private TextBox txtTitle = null!;
        private WinFormsLabel lblTextHeight = null!;
        private NumericUpDown numTextHeight = null!;
        private WinFormsLabel lblTitleHeight = null!;
        private NumericUpDown numTitleHeight = null!;
        private WinFormsLabel lblRowHeight = null!;
        private NumericUpDown numRowHeight = null!;
        private CheckBox chkIncludeTotal = null!;

        private Button btnDrawTable = null!;
        private Button btnExportExcel = null!;
        private Button btnExportCsv = null!;
        private Button btnHelp = null!;
        private Button btnClose = null!;

        public BangThongKeParcelForm(List<ParcelInfo> initialParcels, Dictionary<string, ObjectId> siteMap)
        {
            _allParcels = initialParcels ?? new List<ParcelInfo>();
            _filteredParcels = new List<ParcelInfo>(_allParcels);
            _siteMap = siteMap ?? new Dictionary<string, ObjectId>();

            InitColumnDefinitions();
            InitializeComponent();
            PopulateSites();
            RestoreLastSettings();
            BindGridData();
            UpdateSummary();

            this.FormClosing += (s, e) => SaveCurrentSettings();
        }

        private void InitColumnDefinitions()
        {
            var defs = new List<ColumnExportConfig>
            {
                new ColumnExportConfig
                {
                    Key = "STT",
                    HeaderName = "STT",
                    DefaultWidth = 12.0,
                    Alignment = CellAlignment.MiddleCenter,
                    ExcelAlign = XLAlignmentHorizontalValues.Center,
                    IsDefaultChecked = true, // Ưu tiên
                    GetValue = p => p.SoThuTu.ToString(),
                    SetExcelValue = (cell, p) => cell.SetValue(p.SoThuTu)
                },
                new ColumnExportConfig
                {
                    Key = "SoThua",
                    HeaderName = "SỐ THỬA",
                    DefaultWidth = 18.0,
                    Alignment = CellAlignment.MiddleCenter,
                    ExcelAlign = XLAlignmentHorizontalValues.Center,
                    IsDefaultChecked = true, // Ưu tiên
                    GetValue = p => p.SoThua.ToString(),
                    SetExcelValue = (cell, p) => cell.SetValue(p.SoThua)
                },
                new ColumnExportConfig
                {
                    Key = "TenThua",
                    HeaderName = "TÊN THỬA ĐẤT",
                    DefaultWidth = 35.0,
                    Alignment = CellAlignment.MiddleLeft,
                    ExcelAlign = XLAlignmentHorizontalValues.Left,
                    IsDefaultChecked = true, // Ưu tiên
                    GetValue = p => p.TenThua,
                    SetExcelValue = (cell, p) => cell.SetValue(p.TenThua)
                },
                new ColumnExportConfig
                {
                    Key = "DienTich",
                    HeaderName = "DIỆN TÍCH (m²)",
                    DefaultWidth = 25.0,
                    Alignment = CellAlignment.MiddleRight,
                    ExcelAlign = XLAlignmentHorizontalValues.Right,
                    IsDefaultChecked = true, // Ưu tiên
                    IsNumericSummary = true,
                    GetValue = p => p.DienTich.ToString("N2"),
                    SetExcelValue = (cell, p) => {
                        cell.SetValue(p.DienTich);
                        cell.Style.NumberFormat.Format = "#,##0.00";
                    },
                    GetTotalValue = list => list.Sum(x => x.DienTich).ToString("N2")
                },
                new ColumnExportConfig
                {
                    Key = "PhanKhu",
                    HeaderName = "PHÂN KHU (SITE)",
                    DefaultWidth = 28.0,
                    Alignment = CellAlignment.MiddleLeft,
                    ExcelAlign = XLAlignmentHorizontalValues.Left,
                    IsDefaultChecked = false,
                    GetValue = p => p.PhanKhu,
                    SetExcelValue = (cell, p) => cell.SetValue(p.PhanKhu)
                },
                new ColumnExportConfig
                {
                    Key = "DienTichHa",
                    HeaderName = "DIỆN TÍCH (ha)",
                    DefaultWidth = 22.0,
                    Alignment = CellAlignment.MiddleRight,
                    ExcelAlign = XLAlignmentHorizontalValues.Right,
                    IsDefaultChecked = false,
                    IsNumericSummary = true,
                    GetValue = p => p.DienTichHa.ToString("N4"),
                    SetExcelValue = (cell, p) => {
                        cell.SetValue(p.DienTichHa);
                        cell.Style.NumberFormat.Format = "#,##0.0000";
                    },
                    GetTotalValue = list => (list.Sum(x => x.DienTich) / 10000.0).ToString("N4")
                },
                new ColumnExportConfig
                {
                    Key = "ToaDoX",
                    HeaderName = "TỌA ĐỘ X (m)",
                    DefaultWidth = 25.0,
                    Alignment = CellAlignment.MiddleRight,
                    ExcelAlign = XLAlignmentHorizontalValues.Right,
                    IsDefaultChecked = false,
                    GetValue = p => p.ToaDoX.ToString("N3"),
                    SetExcelValue = (cell, p) => {
                        cell.SetValue(p.ToaDoX);
                        cell.Style.NumberFormat.Format = "#,##0.000";
                    }
                },
                new ColumnExportConfig
                {
                    Key = "ToaDoY",
                    HeaderName = "TỌA ĐỘ Y (m)",
                    DefaultWidth = 25.0,
                    Alignment = CellAlignment.MiddleRight,
                    ExcelAlign = XLAlignmentHorizontalValues.Right,
                    IsDefaultChecked = false,
                    GetValue = p => p.ToaDoY.ToString("N3"),
                    SetExcelValue = (cell, p) => {
                        cell.SetValue(p.ToaDoY);
                        cell.Style.NumberFormat.Format = "#,##0.000";
                    }
                },
                new ColumnExportConfig
                {
                    Key = "KieuStyle",
                    HeaderName = "KIỂU STYLE",
                    DefaultWidth = 25.0,
                    Alignment = CellAlignment.MiddleLeft,
                    ExcelAlign = XLAlignmentHorizontalValues.Left,
                    IsDefaultChecked = false,
                    GetValue = p => p.KieuStyle,
                    SetExcelValue = (cell, p) => cell.SetValue(p.KieuStyle)
                },
                new ColumnExportConfig
                {
                    Key = "MaThue",
                    HeaderName = "MÃ THUẾ / TAX ID",
                    DefaultWidth = 22.0,
                    Alignment = CellAlignment.MiddleCenter,
                    ExcelAlign = XLAlignmentHorizontalValues.Center,
                    IsDefaultChecked = false,
                    GetValue = p => !string.IsNullOrEmpty(p.MaThue) && p.MaThue != "0" ? p.MaThue : "-",
                    SetExcelValue = (cell, p) => cell.SetValue(p.MaThue)
                },
                new ColumnExportConfig
                {
                    Key = "MoTa",
                    HeaderName = "MÔ TẢ",
                    DefaultWidth = 35.0,
                    Alignment = CellAlignment.MiddleLeft,
                    ExcelAlign = XLAlignmentHorizontalValues.Left,
                    IsDefaultChecked = false,
                    GetValue = p => p.MoTa,
                    SetExcelValue = (cell, p) => cell.SetValue(p.MoTa)
                }
            };

            // Phục hồi thứ tự cột đã lưu nếu có
            if (_lastColumnOrderKeys != null && _lastColumnOrderKeys.Count > 0)
            {
                var dict = defs.ToDictionary(x => x.Key, x => x);
                var ordered = new List<ColumnExportConfig>();
                foreach (var key in _lastColumnOrderKeys)
                {
                    if (dict.TryGetValue(key, out var col))
                    {
                        ordered.Add(col);
                        dict.Remove(key);
                    }
                }
                ordered.AddRange(dict.Values);
                _availableColumns = ordered;
            }
            else
            {
                _availableColumns = defs;
            }
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Form Properties
            this.Text = "Thống Kê Thuộc Tính Parcel (Thửa Đất) - AutoCAD Table";
            this.Size = _lastFormSize ?? new Size(1100, 740);
            this.MinimumSize = new Size(980, 680);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Regular);
            this.BackColor = DrawingColor.FromArgb(245, 247, 250);

            // ================= 1. Source Group =================
            grpSource = new GroupBox
            {
                Text = "📌 Nguồn Dữ Liệu & Bộ Lọc",
                Location = new WinFormsPoint(12, 10),
                Size = new Size(this.ClientSize.Width - 24, 68),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            radAll = new RadioButton
            {
                Text = "Toàn bộ bản vẽ",
                Location = new WinFormsPoint(15, 26),
                AutoSize = true,
                Checked = (_lastSourceType == 0)
            };
            radAll.CheckedChanged += RadAll_CheckedChanged;

            radSite = new RadioButton
            {
                Text = "Theo Phân khu (Site):",
                Location = new WinFormsPoint(145, 26),
                AutoSize = true,
                Checked = (_lastSourceType == 1)
            };
            radSite.CheckedChanged += RadSite_CheckedChanged;

            cmbSites = new ComboBox
            {
                Location = new WinFormsPoint(300, 24),
                Size = new Size(160, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Enabled = radSite.Checked
            };
            cmbSites.SelectedIndexChanged += CmbSites_SelectedIndexChanged;

            btnSelectScreen = new Button
            {
                Text = "🎯 Chọn trên màn hình",
                Location = new WinFormsPoint(475, 22),
                Size = new Size(160, 28),
                BackColor = DrawingColor.FromArgb(235, 245, 255),
                FlatStyle = FlatStyle.Flat
            };
            btnSelectScreen.FlatAppearance.BorderColor = DrawingColor.FromArgb(100, 160, 220);
            btnSelectScreen.Click += BtnSelectScreen_Click;

            btnReload = new Button
            {
                Text = "🔄 Nạp lại",
                Location = new WinFormsPoint(645, 22),
                Size = new Size(85, 28),
                FlatStyle = FlatStyle.Flat
            };
            btnReload.Click += BtnReload_Click;

            lblSearch = new WinFormsLabel
            {
                Text = "Tìm kiếm:",
                Location = new WinFormsPoint(745, 27),
                AutoSize = true
            };

            txtSearch = new TextBox
            {
                Location = new WinFormsPoint(815, 24),
                Size = new Size(grpSource.Width - 830, 25),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                PlaceholderText = "Nhập tên hoặc số thửa..."
            };
            txtSearch.TextChanged += TxtSearch_TextChanged;

            grpSource.Controls.AddRange(new Control[] {
                radAll, radSite, cmbSites, btnSelectScreen, btnReload, lblSearch, txtSearch
            });

            // ================= 2. Grid Group =================
            grpGrid = new GroupBox
            {
                Text = "📋 Danh Sách Thuộc Tính Parcel",
                Location = new WinFormsPoint(12, 85),
                Size = new Size(this.ClientSize.Width - 320, this.ClientSize.Height - 85 - 250),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            dgvData = new DataGridView
            {
                Location = new WinFormsPoint(12, 24),
                Size = new Size(grpGrid.Width - 24, grpGrid.Height - 65),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                BackgroundColor = DrawingColor.White,
                BorderStyle = BorderStyle.Fixed3D,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None
            };
            dgvData.DefaultCellStyle.SelectionBackColor = DrawingColor.FromArgb(210, 230, 255);
            dgvData.DefaultCellStyle.SelectionForeColor = DrawingColor.Black;
            dgvData.AlternatingRowsDefaultCellStyle.BackColor = DrawingColor.FromArgb(248, 250, 252);
            dgvData.DoubleClick += (s, e) => BtnZoom_Click(s, e);
            dgvData.ColumnHeaderMouseClick += DgvData_ColumnHeaderMouseClick;

            btnZoom = new Button
            {
                Text = "🔍 Zoom",
                Location = new WinFormsPoint(12, grpGrid.Height - 35),
                Size = new Size(80, 28),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                BackColor = DrawingColor.FromArgb(240, 245, 255),
                FlatStyle = FlatStyle.Flat,
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold)
            };
            btnZoom.Click += BtnZoom_Click;

            btnMoveRowUp = new Button
            {
                Text = "⬆️ Hàng Lên",
                Location = new WinFormsPoint(98, grpGrid.Height - 35),
                Size = new Size(95, 28),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                BackColor = DrawingColor.FromArgb(245, 250, 255),
                FlatStyle = FlatStyle.Flat
            };
            btnMoveRowUp.Click += (s, e) => MoveRow(-1);

            btnMoveRowDown = new Button
            {
                Text = "⬇️ Hàng Xuống",
                Location = new WinFormsPoint(198, grpGrid.Height - 35),
                Size = new Size(105, 28),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                BackColor = DrawingColor.FromArgb(245, 250, 255),
                FlatStyle = FlatStyle.Flat
            };
            btnMoveRowDown.Click += (s, e) => MoveRow(1);

            lblSort = new WinFormsLabel
            {
                Text = "Sắp xếp:",
                Location = new WinFormsPoint(315, grpGrid.Height - 30),
                AutoSize = true,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };

            cmbSort = new ComboBox
            {
                Location = new WinFormsPoint(375, grpGrid.Height - 33),
                Size = new Size(200, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            cmbSort.Items.AddRange(new object[] {
                "Số thửa tăng dần (1, 2, 3...)",
                "Số thửa giảm dần",
                "Tên thửa đất (A ➔ Z)",
                "Tên thửa đất (Z ➔ A)",
                "Diện tích (Nhỏ ➔ Lớn)",
                "Diện tích (Lớn ➔ Nhỏ)",
                "Phân khu (Site)",
                "Tọa độ Y (Bắc ➔ Nam)",
                "Tọa độ X (Tây ➔ Đông)"
            });
            cmbSort.SelectedIndexChanged += CmbSort_SelectedIndexChanged;

            chkAutoRenumber = new CheckBox
            {
                Text = "Tự đánh lại STT (1, 2, 3...)",
                Location = new WinFormsPoint(585, grpGrid.Height - 30),
                AutoSize = true,
                Checked = _lastAutoRenumber,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };

            grpGrid.Controls.AddRange(new Control[] {
                dgvData, btnZoom, btnMoveRowUp, btnMoveRowDown, lblSort, cmbSort, chkAutoRenumber
            });

            // ================= 3. Column Selection Group =================
            grpColumns = new GroupBox
            {
                Text = "☑️ Cột Xuất Bảng CAD",
                Location = new WinFormsPoint(this.ClientSize.Width - 296, 85),
                Size = new Size(284, this.ClientSize.Height - 85 - 250),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right
            };

            chkListColumns = new CheckedListBox
            {
                Location = new WinFormsPoint(10, 24),
                Size = new Size(264, grpColumns.Height - 130),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                CheckOnClick = true
            };
            PopulateColumnsCheckList();

            btnMoveColUp = new Button
            {
                Text = "⬆️ Cột lên",
                Location = new WinFormsPoint(10, grpColumns.Height - 100),
                Size = new Size(128, 28),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                BackColor = DrawingColor.FromArgb(240, 245, 255),
                FlatStyle = FlatStyle.Flat
            };
            btnMoveColUp.Click += (s, e) => MoveColumn(-1);

            btnMoveColDown = new Button
            {
                Text = "⬇️ Cột xuống",
                Location = new WinFormsPoint(146, grpColumns.Height - 100),
                Size = new Size(128, 28),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                BackColor = DrawingColor.FromArgb(240, 245, 255),
                FlatStyle = FlatStyle.Flat
            };
            btnMoveColDown.Click += (s, e) => MoveColumn(1);

            btnPresetEssential = new Button
            {
                Text = "⭐ Cơ bản (STT, Tên, Diện tích)",
                Location = new WinFormsPoint(10, grpColumns.Height - 68),
                Size = new Size(264, 28),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = DrawingColor.FromArgb(255, 250, 235),
                FlatStyle = FlatStyle.Flat,
                Font = new WinFormsFont("Segoe UI", 8.5F, FontStyle.Bold)
            };
            btnPresetEssential.FlatAppearance.BorderColor = DrawingColor.FromArgb(230, 180, 80);
            btnPresetEssential.Click += (s, e) => SetPresetEssentialColumns();

            btnSelectAllCols = new Button
            {
                Text = "Chọn hết",
                Location = new WinFormsPoint(10, grpColumns.Height - 35),
                Size = new Size(128, 28),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                FlatStyle = FlatStyle.Flat
            };
            btnSelectAllCols.Click += (s, e) => {
                for (int i = 0; i < chkListColumns.Items.Count; i++)
                    chkListColumns.SetItemChecked(i, true);
            };

            btnDeselectAllCols = new Button
            {
                Text = "Bỏ chọn",
                Location = new WinFormsPoint(146, grpColumns.Height - 35),
                Size = new Size(128, 28),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat
            };
            btnDeselectAllCols.Click += (s, e) => {
                for (int i = 0; i < chkListColumns.Items.Count; i++)
                    chkListColumns.SetItemChecked(i, false);
            };

            grpColumns.Controls.AddRange(new Control[] {
                chkListColumns, btnMoveColUp, btnMoveColDown, btnPresetEssential, btnSelectAllCols, btnDeselectAllCols
            });

            // ================= 4. Summary Group =================
            grpSummary = new GroupBox
            {
                Text = "📊 Thống Kê Tổng Hợp",
                Location = new WinFormsPoint(12, this.ClientSize.Height - 245),
                Size = new Size(this.ClientSize.Width - 536, 175),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            lblTotalCount = new WinFormsLabel
            {
                Text = "Tổng số thửa: 0 thửa",
                Location = new WinFormsPoint(15, 28),
                AutoSize = true,
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold)
            };

            lblTotalArea = new WinFormsLabel
            {
                Text = "Tổng diện tích: 0.00 m² (0.0000 ha)",
                Location = new WinFormsPoint(15, 58),
                AutoSize = true,
                ForeColor = DrawingColor.FromArgb(0, 100, 180),
                Font = new WinFormsFont("Segoe UI", 9.5F, FontStyle.Bold)
            };

            lblAvgArea = new WinFormsLabel
            {
                Text = "Diện tích trung bình: 0.00 m²",
                Location = new WinFormsPoint(15, 88),
                AutoSize = true
            };

            lblMinMaxArea = new WinFormsLabel
            {
                Text = "Nhỏ nhất: 0.00 m² | Lớn nhất: 0.00 m²",
                Location = new WinFormsPoint(15, 118),
                AutoSize = true
            };

            grpSummary.Controls.AddRange(new Control[] {
                lblTotalCount, lblTotalArea, lblAvgArea, lblMinMaxArea
            });

            // ================= 5. Table Settings Group =================
            grpTableSettings = new GroupBox
            {
                Text = "⚙️ Cài Đặt Bảng AutoCAD Table",
                Location = new WinFormsPoint(this.ClientSize.Width - 516, this.ClientSize.Height - 245),
                Size = new Size(504, 175),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };

            lblTitle = new WinFormsLabel
            {
                Text = "Tiêu đề bảng:",
                Location = new WinFormsPoint(12, 28),
                AutoSize = true
            };

            txtTitle = new TextBox
            {
                Text = _lastTitle,
                Location = new WinFormsPoint(100, 25),
                Size = new Size(390, 25)
            };

            lblTextHeight = new WinFormsLabel
            {
                Text = "Cao chữ ô:",
                Location = new WinFormsPoint(12, 68),
                AutoSize = true
            };

            numTextHeight = new NumericUpDown
            {
                Location = new WinFormsPoint(85, 66),
                Size = new Size(60, 25),
                DecimalPlaces = 1,
                Increment = 0.5m,
                Minimum = 0.5m,
                Maximum = 50.0m,
                Value = _lastTextHeight
            };

            lblTitleHeight = new WinFormsLabel
            {
                Text = "Cao tiêu đề:",
                Location = new WinFormsPoint(160, 68),
                AutoSize = true
            };

            numTitleHeight = new NumericUpDown
            {
                Location = new WinFormsPoint(245, 66),
                Size = new Size(60, 25),
                DecimalPlaces = 1,
                Increment = 0.5m,
                Minimum = 1.0m,
                Maximum = 100.0m,
                Value = _lastTitleHeight
            };

            lblRowHeight = new WinFormsLabel
            {
                Text = "Cao hàng:",
                Location = new WinFormsPoint(325, 68),
                AutoSize = true
            };

            numRowHeight = new NumericUpDown
            {
                Location = new WinFormsPoint(400, 66),
                Size = new Size(60, 25),
                DecimalPlaces = 1,
                Increment = 1.0m,
                Minimum = 2.0m,
                Maximum = 100.0m,
                Value = _lastRowHeight
            };

            chkIncludeTotal = new CheckBox
            {
                Text = "Thêm dòng Tổng cộng (SUM) ở cuối bảng",
                Location = new WinFormsPoint(12, 115),
                AutoSize = true,
                Checked = _lastIncludeTotal,
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold)
            };

            grpTableSettings.Controls.AddRange(new Control[] {
                lblTitle, txtTitle,
                lblTextHeight, numTextHeight,
                lblTitleHeight, numTitleHeight,
                lblRowHeight, numRowHeight,
                chkIncludeTotal
            });

            // ================= 6. Action Buttons =================
            btnDrawTable = new Button
            {
                Text = "📋 VẼ BẢNG VÀO AUTOCAD",
                Location = new WinFormsPoint(12, this.ClientSize.Height - 55),
                Size = new Size(250, 44),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                BackColor = DrawingColor.FromArgb(0, 122, 204),
                ForeColor = DrawingColor.White,
                FlatStyle = FlatStyle.Flat,
                Font = new WinFormsFont("Segoe UI", 10F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnDrawTable.FlatAppearance.BorderSize = 0;
            btnDrawTable.Click += BtnDrawTable_Click;

            btnExportExcel = new Button
            {
                Text = "📊 Xuất File Excel (.xlsx)",
                Location = new WinFormsPoint(272, this.ClientSize.Height - 55),
                Size = new Size(180, 44),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                BackColor = DrawingColor.FromArgb(33, 115, 70),
                ForeColor = DrawingColor.White,
                FlatStyle = FlatStyle.Flat,
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnExportExcel.FlatAppearance.BorderSize = 0;
            btnExportExcel.Click += BtnExportExcel_Click;

            btnExportCsv = new Button
            {
                Text = "📄 Xuất CSV",
                Location = new WinFormsPoint(462, this.ClientSize.Height - 55),
                Size = new Size(110, 44),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                BackColor = DrawingColor.FromArgb(235, 240, 245),
                FlatStyle = FlatStyle.Flat,
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Regular),
                Cursor = Cursors.Hand
            };
            btnExportCsv.Click += BtnExportCsv_Click;

            btnHelp = new Button
            {
                Text = "❓ Trợ giúp",
                Location = new WinFormsPoint(this.ClientSize.Width - 215, this.ClientSize.Height - 55),
                Size = new Size(95, 44),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat
            };
            btnHelp.Click += BtnHelp_Click;

            btnClose = new Button
            {
                Text = "❌ Đóng",
                Location = new WinFormsPoint(this.ClientSize.Width - 110, this.ClientSize.Height - 55),
                Size = new Size(98, 44),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat
            };
            btnClose.Click += (s, e) => this.Close();

            // Add all groups and buttons
            this.Controls.AddRange(new Control[] {
                grpSource,
                grpGrid,
                grpColumns,
                grpSummary,
                grpTableSettings,
                btnDrawTable,
                btnExportExcel,
                btnExportCsv,
                btnHelp,
                btnClose
            });

            this.ResumeLayout(false);
        }

        private void PopulateColumnsCheckList()
        {
            chkListColumns.Items.Clear();
            foreach (var col in _availableColumns)
            {
                bool isChecked = (_lastCheckedColumnKeys != null)
                    ? _lastCheckedColumnKeys.Contains(col.Key)
                    : col.IsDefaultChecked;

                chkListColumns.Items.Add(col.HeaderName, isChecked);
            }
        }

        private void SetPresetEssentialColumns()
        {
            for (int i = 0; i < _availableColumns.Count; i++)
            {
                string key = _availableColumns[i].Key;
                // Ưu tiên chọn: STT, Số thửa, Tên thửa đất, Diện tích (m²)
                bool isEssential = (key == "STT" || key == "SoThua" || key == "TenThua" || key == "DienTich");
                chkListColumns.SetItemChecked(i, isEssential);
            }
        }

        private void MoveColumn(int direction)
        {
            int index = chkListColumns.SelectedIndex;
            if (index < 0)
            {
                MessageBox.Show("Vui lòng chọn một cột trong danh sách để di chuyển!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int newIndex = index + direction;
            if (newIndex < 0 || newIndex >= _availableColumns.Count)
                return;

            // Lưu lại danh sách các key đang checked
            var checkedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _availableColumns.Count; i++)
            {
                if (chkListColumns.GetItemChecked(i))
                    checkedKeys.Add(_availableColumns[i].Key);
            }

            // Hoán đổi vị trí trong _availableColumns
            var colItem = _availableColumns[index];
            _availableColumns.RemoveAt(index);
            _availableColumns.Insert(newIndex, colItem);

            // Nạp lại CheckedListBox
            chkListColumns.Items.Clear();
            for (int i = 0; i < _availableColumns.Count; i++)
            {
                bool isChecked = checkedKeys.Contains(_availableColumns[i].Key);
                chkListColumns.Items.Add(_availableColumns[i].HeaderName, isChecked);
            }

            chkListColumns.SelectedIndex = newIndex;

            // Cập nhật lại Grid View
            BindGridData();
        }

        private void MoveRow(int direction)
        {
            if (dgvData.SelectedRows.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn 1 hàng Parcel trong bảng để di chuyển!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int index = dgvData.SelectedRows[0].Index;
            int newIndex = index + direction;

            if (newIndex < 0 || newIndex >= _filteredParcels.Count)
                return;

            // Hoán đổi vị trí trong _filteredParcels
            var item = _filteredParcels[index];
            _filteredParcels.RemoveAt(index);
            _filteredParcels.Insert(newIndex, item);

            // Tự đánh lại STT nếu được chọn
            if (chkAutoRenumber.Checked)
            {
                for (int i = 0; i < _filteredParcels.Count; i++)
                {
                    _filteredParcels[i].SoThuTu = i + 1;
                }
            }

            BindGridData();

            // Chọn lại hàng vừa di chuyển
            if (newIndex >= 0 && newIndex < dgvData.Rows.Count)
            {
                dgvData.ClearSelection();
                dgvData.Rows[newIndex].Selected = true;
                dgvData.CurrentCell = dgvData.Rows[newIndex].Cells[0];
            }
        }

        private void DgvData_ColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.ColumnIndex < 0 || e.ColumnIndex >= _availableColumns.Count) return;

            var col = _availableColumns[e.ColumnIndex];
            string key = col.Key;

            if (_currentSortKey == key)
                _isSortAscending = !_isSortAscending;
            else
            {
                _currentSortKey = key;
                _isSortAscending = true;
            }

            SortParcelsByKey(key, _isSortAscending);
        }

        private void CmbSort_SelectedIndexChanged(object? sender, EventArgs e)
        {
            switch (cmbSort.SelectedIndex)
            {
                case 0: // Số thửa tăng dần
                    SortParcelsByKey("SoThua", true);
                    break;
                case 1: // Số thửa giảm dần
                    SortParcelsByKey("SoThua", false);
                    break;
                case 2: // Tên thửa đất (A ➔ Z)
                    SortParcelsByKey("TenThua", true);
                    break;
                case 3: // Tên thửa đất (Z ➔ A)
                    SortParcelsByKey("TenThua", false);
                    break;
                case 4: // Diện tích (Nhỏ ➔ Lớn)
                    SortParcelsByKey("DienTich", true);
                    break;
                case 5: // Diện tích (Lớn ➔ Nhỏ)
                    SortParcelsByKey("DienTich", false);
                    break;
                case 6: // Phân khu (Site)
                    SortParcelsByKey("PhanKhu", true);
                    break;
                case 7: // Tọa độ Y (Bắc ➔ Nam)
                    SortParcelsByKey("ToaDoY", false);
                    break;
                case 8: // Tọa độ X (Tây ➔ Đông)
                    SortParcelsByKey("ToaDoX", true);
                    break;
            }
        }

        private void SortParcelsByKey(string key, bool ascending)
        {
            switch (key)
            {
                case "STT":
                    _filteredParcels = ascending ? _filteredParcels.OrderBy(p => p.SoThuTu).ToList() : _filteredParcels.OrderByDescending(p => p.SoThuTu).ToList();
                    break;
                case "SoThua":
                    _filteredParcels = ascending ? _filteredParcels.OrderBy(p => p.SoThua).ToList() : _filteredParcels.OrderByDescending(p => p.SoThua).ToList();
                    break;
                case "TenThua":
                    _filteredParcels = ascending ? _filteredParcels.OrderBy(p => p.TenThua).ToList() : _filteredParcels.OrderByDescending(p => p.TenThua).ToList();
                    break;
                case "PhanKhu":
                    _filteredParcels = ascending ? _filteredParcels.OrderBy(p => p.PhanKhu).ThenBy(p => p.SoThua).ToList() : _filteredParcels.OrderByDescending(p => p.PhanKhu).ThenBy(p => p.SoThua).ToList();
                    break;
                case "DienTich":
                case "DienTichHa":
                    _filteredParcels = ascending ? _filteredParcels.OrderBy(p => p.DienTich).ToList() : _filteredParcels.OrderByDescending(p => p.DienTich).ToList();
                    break;
                case "ToaDoX":
                    _filteredParcels = ascending ? _filteredParcels.OrderBy(p => p.ToaDoX).ToList() : _filteredParcels.OrderByDescending(p => p.ToaDoX).ToList();
                    break;
                case "ToaDoY":
                    _filteredParcels = ascending ? _filteredParcels.OrderBy(p => p.ToaDoY).ToList() : _filteredParcels.OrderByDescending(p => p.ToaDoY).ToList();
                    break;
                default:
                    var colDef = _availableColumns.FirstOrDefault(c => c.Key == key);
                    if (colDef != null)
                        _filteredParcels = ascending ? _filteredParcels.OrderBy(p => colDef.GetValue(p)).ToList() : _filteredParcels.OrderByDescending(p => colDef.GetValue(p)).ToList();
                    break;
            }

            if (chkAutoRenumber.Checked)
            {
                for (int i = 0; i < _filteredParcels.Count; i++)
                {
                    _filteredParcels[i].SoThuTu = i + 1;
                }
            }

            BindGridData();
            UpdateSummary();
        }

        private void PopulateSites()
        {
            cmbSites.Items.Clear();
            foreach (var siteName in _siteMap.Keys)
            {
                cmbSites.Items.Add(siteName);
            }
            if (cmbSites.Items.Count > 0)
            {
                if (!string.IsNullOrEmpty(_lastSelectedSiteName) && cmbSites.Items.Contains(_lastSelectedSiteName))
                    cmbSites.SelectedItem = _lastSelectedSiteName;
                else
                    cmbSites.SelectedIndex = 0;
            }
        }

        private void RestoreLastSettings()
        {
            txtTitle.Text = _lastTitle;
            numTextHeight.Value = _lastTextHeight;
            numTitleHeight.Value = _lastTitleHeight;
            numRowHeight.Value = _lastRowHeight;
            chkIncludeTotal.Checked = _lastIncludeTotal;
            chkAutoRenumber.Checked = _lastAutoRenumber;

            // Phục hồi nguồn dữ liệu trước đó
            if (_lastSourceType == 1 && !string.IsNullOrEmpty(_lastSelectedSiteName) && _siteMap.ContainsKey(_lastSelectedSiteName))
            {
                radSite.Checked = true;
                cmbSites.SelectedItem = _lastSelectedSiteName;
                FilterBySelectedSite();
            }
            else if (_lastSourceType == 2 && _lastScreenParcels != null && _lastScreenParcels.Count > 0)
            {
                _allParcels = _lastScreenParcels;
                _filteredParcels = new List<ParcelInfo>(_allParcels);
            }
            else
            {
                radAll.Checked = true;
            }

            // Phục hồi các cột đã chọn
            if (_lastCheckedColumnKeys != null && _lastCheckedColumnKeys.Count > 0)
            {
                for (int i = 0; i < _availableColumns.Count; i++)
                {
                    bool isChecked = _lastCheckedColumnKeys.Contains(_availableColumns[i].Key);
                    chkListColumns.SetItemChecked(i, isChecked);
                }
            }
            else
            {
                // Mặc định ưu tiên STT, Số thửa, Tên thửa đất, Diện tích
                SetPresetEssentialColumns();
            }
        }

        private void SaveCurrentSettings()
        {
            _lastTitle = txtTitle.Text;
            _lastTextHeight = numTextHeight.Value;
            _lastTitleHeight = numTitleHeight.Value;
            _lastRowHeight = numRowHeight.Value;
            _lastIncludeTotal = chkIncludeTotal.Checked;
            _lastAutoRenumber = chkAutoRenumber.Checked;
            _lastFormSize = this.Size;

            if (radSite.Checked)
            {
                _lastSourceType = 1;
                _lastSelectedSiteName = cmbSites.SelectedItem?.ToString() ?? "";
            }
            else
            {
                _lastSourceType = 0;
            }

            _lastCheckedColumnKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < chkListColumns.Items.Count; i++)
            {
                if (chkListColumns.GetItemChecked(i))
                {
                    _lastCheckedColumnKeys.Add(_availableColumns[i].Key);
                }
            }

            _lastColumnOrderKeys = _availableColumns.Select(c => c.Key).ToList();
        }

        private void BindGridData()
        {
            dgvData.Columns.Clear();

            // Create Grid Columns
            foreach (var col in _availableColumns)
            {
                var gridCol = new DataGridViewTextBoxColumn
                {
                    Name = col.Key,
                    HeaderText = col.HeaderName,
                    Width = (int)(col.DefaultWidth * 3.8),
                    SortMode = DataGridViewColumnSortMode.Programmatic
                };

                if (col.Alignment == CellAlignment.MiddleRight)
                    gridCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                else if (col.Alignment == CellAlignment.MiddleLeft)
                    gridCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
                else
                    gridCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

                dgvData.Columns.Add(gridCol);
            }

            // Populate rows
            dgvData.Rows.Clear();
            for (int i = 0; i < _filteredParcels.Count; i++)
            {
                var p = _filteredParcels[i];
                if (chkAutoRenumber.Checked)
                {
                    p.SoThuTu = i + 1;
                }

                var rowValues = new object[_availableColumns.Count];
                for (int c = 0; c < _availableColumns.Count; c++)
                {
                    rowValues[c] = _availableColumns[c].GetValue(p);
                }
                int rowIndex = dgvData.Rows.Add(rowValues);
                dgvData.Rows[rowIndex].Tag = p;
            }
        }

        private void UpdateSummary()
        {
            int count = _filteredParcels.Count;
            if (count == 0)
            {
                lblTotalCount.Text = "Tổng số thửa: 0 thửa";
                lblTotalArea.Text = "Tổng diện tích: 0.00 m² (0.0000 ha)";
                lblAvgArea.Text = "Diện tích trung bình: 0.00 m²";
                lblMinMaxArea.Text = "Nhỏ nhất: 0.00 m² | Lớn nhất: 0.00 m²";
                return;
            }

            double totalArea = _filteredParcels.Sum(x => x.DienTich);
            double avgArea = totalArea / count;
            double minArea = _filteredParcels.Min(x => x.DienTich);
            double maxArea = _filteredParcels.Max(x => x.DienTich);

            var minParcel = _filteredParcels.FirstOrDefault(x => Math.Abs(x.DienTich - minArea) < 0.001);
            var maxParcel = _filteredParcels.FirstOrDefault(x => Math.Abs(x.DienTich - maxArea) < 0.001);

            string minName = minParcel != null ? $" ({minParcel.TenThua})" : "";
            string maxName = maxParcel != null ? $" ({maxParcel.TenThua})" : "";

            lblTotalCount.Text = $"Tổng số thửa: {count:N0} thửa";
            lblTotalArea.Text = $"Tổng diện tích: {totalArea:N2} m² ({(totalArea / 10000.0):N4} ha)";
            lblAvgArea.Text = $"Diện tích trung bình: {avgArea:N2} m²";
            lblMinMaxArea.Text = $"Nhỏ nhất: {minArea:N2} m²{minName} | Lớn nhất: {maxArea:N2} m²{maxName}";
        }

        private void ApplyFilter()
        {
            string keyword = txtSearch.Text.Trim().ToLower();

            if (string.IsNullOrEmpty(keyword))
            {
                _filteredParcels = new List<ParcelInfo>(_allParcels);
            }
            else
            {
                _filteredParcels = _allParcels.Where(p =>
                    p.TenThua.ToLower().Contains(keyword) ||
                    p.SoThua.ToString().Contains(keyword) ||
                    p.PhanKhu.ToLower().Contains(keyword) ||
                    p.MoTa.ToLower().Contains(keyword)
                ).ToList();
            }

            BindGridData();
            UpdateSummary();
        }

        private void RadAll_CheckedChanged(object? sender, EventArgs e)
        {
            if (radAll.Checked)
            {
                cmbSites.Enabled = false;
                _lastSourceType = 0;
                if (OnReloadAllParcels != null)
                {
                    _allParcels = OnReloadAllParcels();
                    ApplyFilter();
                }
            }
        }

        private void RadSite_CheckedChanged(object? sender, EventArgs e)
        {
            cmbSites.Enabled = radSite.Checked;
            if (radSite.Checked)
            {
                _lastSourceType = 1;
                if (cmbSites.SelectedItem != null)
                {
                    FilterBySelectedSite();
                }
            }
        }

        private void CmbSites_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (radSite.Checked)
            {
                FilterBySelectedSite();
            }
        }

        private void FilterBySelectedSite()
        {
            string? siteName = cmbSites.SelectedItem?.ToString();
            if (!string.IsNullOrEmpty(siteName) && _siteMap.TryGetValue(siteName, out ObjectId siteId))
            {
                _lastSelectedSiteName = siteName;
                if (OnFilterBySite != null)
                {
                    _allParcels = OnFilterBySite(siteId);
                    ApplyFilter();
                }
            }
        }

        private void BtnSelectScreen_Click(object? sender, EventArgs e)
        {
            if (OnSelectOnScreen != null)
            {
                var selected = OnSelectOnScreen();
                if (selected != null && selected.Count > 0)
                {
                    _allParcels = selected;
                    _lastScreenParcels = selected;
                    _lastSourceType = 2;
                    ApplyFilter();
                }
            }
        }

        private void BtnReload_Click(object? sender, EventArgs e)
        {
            if (radSite.Checked)
                FilterBySelectedSite();
            else if (OnReloadAllParcels != null)
            {
                _allParcels = OnReloadAllParcels();
                ApplyFilter();
            }
        }

        private void TxtSearch_TextChanged(object? sender, EventArgs e)
        {
            ApplyFilter();
        }

        private void BtnZoom_Click(object? sender, EventArgs e)
        {
            if (dgvData.SelectedRows.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn 1 thửa đất trong bảng danh sách!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var row = dgvData.SelectedRows[0];
            if (row.Tag is ParcelInfo parcel)
            {
                OnZoomToParcel?.Invoke(parcel.ParcelId, parcel.Bounds);
            }
        }

        public List<ColumnExportConfig> GetSelectedColumns()
        {
            var selected = new List<ColumnExportConfig>();
            for (int i = 0; i < chkListColumns.Items.Count; i++)
            {
                if (chkListColumns.GetItemChecked(i))
                {
                    selected.Add(_availableColumns[i]);
                }
            }
            return selected;
        }

        private void BtnDrawTable_Click(object? sender, EventArgs e)
        {
            if (_filteredParcels.Count == 0)
            {
                MessageBox.Show("Không có dữ liệu Parcel nào để vẽ bảng!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selectedCols = GetSelectedColumns();
            if (selectedCols.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn ít nhất 1 cột cần xuất!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SaveCurrentSettings();

            string title = string.IsNullOrWhiteSpace(txtTitle.Text) ? "BẢNG THỐNG KÊ THUỘC TÍNH PARCEL" : txtTitle.Text.Trim();
            double textHeight = (double)numTextHeight.Value;
            double titleHeight = (double)numTitleHeight.Value;
            double rowHeight = (double)numRowHeight.Value;
            bool includeTotal = chkIncludeTotal.Checked;

            OnDrawTable?.Invoke(_filteredParcels, selectedCols, title, textHeight, titleHeight, rowHeight, includeTotal);
        }

        private void BtnExportExcel_Click(object? sender, EventArgs e)
        {
            if (_filteredParcels.Count == 0)
            {
                MessageBox.Show("Không có dữ liệu Parcel nào để xuất Excel!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selectedCols = GetSelectedColumns();
            if (selectedCols.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn ít nhất 1 cột cần xuất!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SaveCurrentSettings();

            string defaultFileName = $"ThongKe_Parcel_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            string initialDir = !string.IsNullOrEmpty(_lastExportDir) ? _lastExportDir : Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            using (SaveFileDialog sfd = new SaveFileDialog
            {
                Title = "Xuất dữ liệu Parcel ra File Excel",
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                FileName = defaultFileName,
                InitialDirectory = initialDir,
                DefaultExt = "xlsx",
                OverwritePrompt = true
            })
            {
                if (sfd.ShowDialog() != DialogResult.OK)
                    return;

                string filePath = sfd.FileName;
                _lastExportDir = Path.GetDirectoryName(filePath) ?? "";

                try
                {
                    ExportToExcel(filePath, _filteredParcels, selectedCols, txtTitle.Text, chkIncludeTotal.Checked);
                    
                    var dlgResult = MessageBox.Show(
                        $"Xuất file Excel thành công!\n\nĐường dẫn: {filePath}\n\nBạn có muốn mở file ngay không?",
                        "Hoàn thành",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information);

                    if (dlgResult == DialogResult.Yes)
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = filePath,
                            UseShellExecute = true
                        });
                    }
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show($"Lỗi khi xuất file Excel:\n{ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ExportToExcel(string filePath, List<ParcelInfo> data, List<ColumnExportConfig> cols, string title, bool includeTotal)
        {
            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("ThongKe_Parcel");
                int numCols = cols.Count;

                // 1. Tiêu đề
                ws.Cell(1, 1).Value = string.IsNullOrWhiteSpace(title) ? "BẢNG THỐNG KÊ THUỘC TÍNH PARCEL" : title.Trim();
                var titleRange = ws.Range(1, 1, 1, numCols);
                titleRange.Merge();
                titleRange.Style.Font.Bold = true;
                titleRange.Style.Font.FontSize = 14;
                titleRange.Style.Font.FontColor = XLColor.White;
                titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                titleRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                titleRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E79");
                ws.Row(1).Height = 32;

                // 2. Headers
                for (int c = 0; c < numCols; c++)
                {
                    var cell = ws.Cell(2, c + 1);
                    cell.Value = cols[c].HeaderName;
                    cell.Style.Font.Bold = true;
                    cell.Style.Font.FontSize = 10;
                    cell.Style.Font.FontColor = XLColor.White;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2E75B6");
                    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#BDD7EE");
                }
                ws.Row(2).Height = 24;

                // 3. Data Rows
                for (int i = 0; i < data.Count; i++)
                {
                    int row = i + 3;
                    var p = data[i];
                    for (int c = 0; c < numCols; c++)
                    {
                        var cell = ws.Cell(row, c + 1);
                        cols[c].SetExcelValue(cell, p);
                        cell.Style.Alignment.Horizontal = cols[c].ExcelAlign;
                        cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                        cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#D9D9D9");

                        if (i % 2 == 1)
                        {
                            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F5F9");
                        }
                    }
                    ws.Row(row).Height = 20;
                }

                // 4. Total Row (Dòng tổng cộng)
                if (includeTotal)
                {
                    int totalRow = data.Count + 3;
                    ws.Cell(totalRow, 1).Value = "TỔNG CỘNG";
                    ws.Cell(totalRow, 1).Style.Font.Bold = true;
                    ws.Cell(totalRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    ws.Cell(totalRow, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#D9E1F2");
                    ws.Cell(totalRow, 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                    for (int c = 0; c < numCols; c++)
                    {
                        var cell = ws.Cell(totalRow, c + 1);
                        cell.Style.Font.Bold = true;
                        cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9E1F2");
                        cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        cell.Style.Border.TopBorder = XLBorderStyleValues.Double;

                        if (cols[c].Key == "DienTich")
                        {
                            string colLetter = ws.Column(c + 1).ColumnLetter();
                            cell.FormulaA1 = $"SUM({colLetter}3:{colLetter}{totalRow - 1})";
                            cell.Style.NumberFormat.Format = "#,##0.00";
                            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                        }
                        else if (cols[c].Key == "DienTichHa")
                        {
                            string colLetter = ws.Column(c + 1).ColumnLetter();
                            cell.FormulaA1 = $"SUM({colLetter}3:{colLetter}{totalRow - 1})";
                            cell.Style.NumberFormat.Format = "#,##0.0000";
                            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                        }
                    }
                    ws.Row(totalRow).Height = 22;
                }

                ws.Columns().AdjustToContents();
                workbook.SaveAs(filePath);
            }
        }

        private void BtnExportCsv_Click(object? sender, EventArgs e)
        {
            if (_filteredParcels.Count == 0)
            {
                MessageBox.Show("Không có dữ liệu Parcel nào để xuất CSV!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selectedCols = GetSelectedColumns();
            if (selectedCols.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn ít nhất 1 cột cần xuất!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SaveCurrentSettings();

            string defaultFileName = $"ThongKe_Parcel_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            string initialDir = !string.IsNullOrEmpty(_lastExportDir) ? _lastExportDir : Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            using (SaveFileDialog sfd = new SaveFileDialog
            {
                Title = "Xuất dữ liệu Parcel ra File CSV",
                Filter = "CSV File (Comma delimited) (*.csv)|*.csv",
                FileName = defaultFileName,
                InitialDirectory = initialDir,
                DefaultExt = "csv",
                OverwritePrompt = true
            })
            {
                if (sfd.ShowDialog() != DialogResult.OK)
                    return;

                string filePath = sfd.FileName;
                _lastExportDir = Path.GetDirectoryName(filePath) ?? "";

                try
                {
                    var sb = new StringBuilder();

                    // Tiêu đề bảng
                    sb.AppendLine($"\"{txtTitle.Text.Replace("\"", "\"\"")}\"");
                    sb.AppendLine();

                    // Header Row
                    sb.AppendLine(string.Join(",", selectedCols.Select(c => $"\"{c.HeaderName.Replace("\"", "\"\"")}\"")));

                    // Data Rows
                    foreach (var p in _filteredParcels)
                    {
                        var rowValues = selectedCols.Select(c => $"\"{c.GetValue(p).Replace("\"", "\"\"")}\"");
                        sb.AppendLine(string.Join(",", rowValues));
                    }

                    // Total Row
                    if (chkIncludeTotal.Checked)
                    {
                        var totalValues = new List<string>();
                        for (int c = 0; c < selectedCols.Count; c++)
                        {
                            if (c == 0)
                                totalValues.Add("\"TỔNG CỘNG\"");
                            else if (selectedCols[c].GetTotalValue != null)
                                totalValues.Add($"\"{selectedCols[c].GetTotalValue!(_filteredParcels)}\"");
                            else
                                totalValues.Add("\"\"");
                        }
                        sb.AppendLine(string.Join(",", totalValues));
                    }

                    File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);

                    var dlgResult = MessageBox.Show(
                        $"Xuất file CSV thành công!\n\nĐường dẫn: {filePath}\n\nBạn có muốn mở file ngay không?",
                        "Hoàn thành",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information);

                    if (dlgResult == DialogResult.Yes)
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = filePath,
                            UseShellExecute = true
                        });
                    }
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show($"Lỗi khi xuất file CSV:\n{ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnHelp_Click(object? sender, EventArgs e)
        {
            string msg = "HƯỚNG DẪN SỬ DỤNG BẢNG THỐNG KÊ PARCEL:\n\n" +
                         "1. NGUỒN DỮ LIỆU:\n" +
                         "   • Toàn bộ bản vẽ: Lấy tất cả thửa đất trong mọi Phân khu (Site).\n" +
                         "   • Theo Phân khu: Lọc theo từng Site cụ thể.\n" +
                         "   • Chọn trên màn hình: Quét chọn trực tiếp các Parcel trong bản vẽ.\n\n" +
                         "2. TÙY BIẾN THỨ TỰ CỘT & HÀNG:\n" +
                         "   • Đổi thứ tự Cột: Chọn cột bên phải rồi bấm [⬆️ Cột lên] / [⬇️ Cột xuống].\n" +
                         "   • Đổi thứ tự Hàng: Chọn hàng trong bảng rồi bấm [⬆️ Hàng lên] / [⬇️ Hàng xuống].\n" +
                         "   • Sắp xếp nhanh: Bấm vào tiêu đề cột hoặc chọn trong menu 'Sắp xếp'.\n" +
                         "   • Nút [⭐ Cơ bản]: Nhanh chóng chọn 4 cột ưu tiên (STT, Số thửa, Tên, Diện tích).\n\n" +
                         "3. XUẤT DỮ LIỆU:\n" +
                         "   • Vẽ bảng vào AutoCAD: Tạo bảng AutoCAD Table chuẩn tại vị trí pick điểm.\n" +
                         "   • Xuất Excel (.xlsx): Xuất bảng biểu chuyên nghiệp có công thức SUM và màu sắc.\n" +
                         "   • Xuất CSV: Xuất định dạng văn bản ngăn cách dấu phẩy.\n\n" +
                         "4. TƯƠNG TÁC CANVAS:\n" +
                         "   • Click đúp hàng hoặc bấm [Zoom] để phóng to và highlight thửa đất trên CAD.";

            MessageBox.Show(msg, "Hướng Dẫn Sử Dụng", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}

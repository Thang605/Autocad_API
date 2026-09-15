using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using ATable = Autodesk.AutoCAD.DatabaseServices.Table;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsFont = System.Drawing.Font;
using WinFormsPoint = System.Drawing.Point;

namespace MyFirstProject.Civil_Tool
{
    #region Data Item Models

    /// <summary>
    /// Model lưu thông tin 1 hàng cọc đọc từ bảng AutoCAD
    /// </summary>
    public class CocItemModel
    {
        public bool IsSelected { get; set; } = true;
        public int Stt { get; set; } = 1;
        public string TenCoc { get; set; } = "";
        public string RawStationText { get; set; } = "";
        public double Station { get; set; } = 0.0;
        public bool IsValidStation { get; set; } = false;
        public bool IsOutOfRange { get; set; } = false;
        public bool IsDuplicateStation { get; set; } = false;
        public string StatusText { get; set; } = "";
        public Color StatusColor { get; set; } = Color.DarkGreen;
        public int TableRowIndex { get; set; } = 0;
    }

    /// <summary>
    /// Model hỗ trợ ComboBox hiển thị Name kèm ObjectId
    /// </summary>
    public class ObjectIdComboBoxItem
    {
        public string Text { get; set; } = "";
        public ObjectId Id { get; set; } = ObjectId.Null;
        public object? Tag { get; set; }

        public override string ToString() => Text;
    }

    #endregion

    #region Station Parser

    /// <summary>
    /// Bộ phân tích chuỗi Lý trình đa định dạng cho công trình giao thông Việt Nam
    /// Hỗ trợ: Km1+785.75, Km 1+785.75, 1+785.75, +785.75, 0+785.75, 1785.75, Km1+785,75, lý trình âm...
    /// </summary>
    public static class StationParser
    {
        public static string CleanMText(string? input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            string text = input;
            text = text.Replace("{", "").Replace("}", "");
            text = Regex.Replace(text, @"\\[PpXxLlOoKk]", " ");
            text = Regex.Replace(text, @"\\f[^;]+;", " ");
            text = Regex.Replace(text, @"\\A[0-9];", " ");
            text = Regex.Replace(text, @"\\H[0-9\.]+x?;", " ");
            text = Regex.Replace(text, @"\\W[0-9\.]+x?;", " ");
            text = Regex.Replace(text, @"\\Q[0-9\.-]+;", " ");
            text = Regex.Replace(text, @"\\T[0-9\.]+;", " ");
            text = Regex.Replace(text, @"\\S[^;]+;", " ");
            text = Regex.Replace(text, @"\\[A-Za-z0-9]+(?:;|\s*)", " ");
            text = text.Replace("%%u", "").Replace("%%U", "").Replace("%%o", "").Replace("%%O", "");
            return text.Trim();
        }

        public static bool TryParseStation(string? input, out double station)
        {
            station = 0.0;
            if (string.IsNullOrWhiteSpace(input)) return false;

            string text = CleanMText(input);

            // Chuẩn hóa dấu phẩy và chấm thập phân
            if (text.Contains(',') && text.Contains('.'))
            {
                int dotIdx = text.IndexOf('.');
                int commaIdx = text.IndexOf(',');
                if (dotIdx < commaIdx)
                {
                    text = text.Replace(".", "").Replace(',', '.');
                }
                else
                {
                    text = text.Replace(",", "");
                }
            }
            else if (text.Contains(','))
            {
                text = text.Replace(',', '.');
            }

            text = Regex.Replace(text, @"\s*\+\s*", "+");
            text = Regex.Replace(text, @"\s*-\s*", "-");

            // Trường hợp 1: Có dấu '+' (Km1+785.75, 1+785.75, +785.75, 0+123.45, -0+050.00...)
            var matchPlus = Regex.Match(text, @"^(?:[Kk][Mm]\s*)?([+-]?\d*)\+(\d+(?:\.\d+)?)$");
            if (matchPlus.Success)
            {
                string partKm = matchPlus.Groups[1].Value;
                string partM = matchPlus.Groups[2].Value;

                double km = 0;
                bool isNegative = false;

                if (!string.IsNullOrEmpty(partKm))
                {
                    if (partKm == "-")
                    {
                        isNegative = true;
                        km = 0;
                    }
                    else if (partKm == "+")
                    {
                        km = 0;
                    }
                    else if (double.TryParse(partKm, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedKm))
                    {
                        if (parsedKm < 0)
                        {
                            isNegative = true;
                            km = Math.Abs(parsedKm);
                        }
                        else
                        {
                            km = parsedKm;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }

                if (double.TryParse(partM, NumberStyles.Float, CultureInfo.InvariantCulture, out double m))
                {
                    station = km * 1000.0 + m;
                    if (isNegative) station = -station;
                    return true;
                }
            }

            // Trường hợp 2: Số thuần hoặc có chữ Km (Km 1785.75, 1785.75, -50.00)
            string textWithoutKm = Regex.Replace(text, @"^[Kk][Mm]\s*", "").Trim();
            if (double.TryParse(textWithoutKm, NumberStyles.Float, CultureInfo.InvariantCulture, out double directVal))
            {
                station = directVal;
                return true;
            }

            return false;
        }
    }

    #endregion

    /// <summary>
    /// Giao diện phát sinh cọc theo bảng AutoCAD chuyên nghiệp
    /// </summary>
    public class PhatSinhCocTheoBangForm : Form
    {
        #region Persistent State (Ghi nhớ cấu hình phiên làm việc)

        private static ObjectId _lastTableId = ObjectId.Null;
        private static ObjectId _lastAlignmentId = ObjectId.Null;
        private static ObjectId _lastSampleLineGroupId = ObjectId.Null;

        private static int _lastColTenCoc = 1;
        private static int _lastColLyTrinh = 2;
        private static int _lastStartRow = 2;

        private static decimal _lastLeftWidth = 15.0m;
        private static decimal _lastRightWidth = 15.0m;

        private static string _lastSampleLineStyle = "";
        private static string _lastSampleLineLabelStyle = "Tên cọc";
        private static int _lastDuplicateMode = 0;
        private static Size _lastFormSize = new Size(1060, 750);

        #endregion

        #region Form Fields & State

        private ObjectId _selectedTableId = ObjectId.Null;
        private ObjectId _selectedAlignmentId = ObjectId.Null;
        private ObjectId _selectedGroupId = ObjectId.Null;

        private List<CocItemModel> _currentCocItems = new List<CocItemModel>();
        private bool _isPopulatingUI = false;

        public bool FormAccepted { get; private set; } = false;

        #endregion

        #region UI Controls

        // Banner
        private WinFormsLabel lblTitle = null!;
        private WinFormsLabel lblSubtitle = null!;

        // Group 1: Table Source
        private GroupBox grpTable = null!;
        private WinFormsLabel lblTableStatus = null!;
        private Button btnPickTable = null!;
        private WinFormsLabel lblColName = null!;
        private ComboBox cmbColName = null!;
        private WinFormsLabel lblColStation = null!;
        private ComboBox cmbColStation = null!;
        private WinFormsLabel lblStartRow = null!;
        private NumericUpDown numStartRow = null!;
        private WinFormsLabel lblEndRow = null!;
        private NumericUpDown numEndRow = null!;
        private Button btnReloadTable = null!;

        // Group 2: Alignment & SampleLineGroup
        private GroupBox grpAlignment = null!;
        private WinFormsLabel lblAlignment = null!;
        private ComboBox cmbAlignment = null!;
        private Button btnPickAlignment = null!;
        private WinFormsLabel lblGroup = null!;
        private ComboBox cmbGroup = null!;
        private WinFormsLabel lblNewGroupName = null!;
        private TextBox txtNewGroupName = null!;

        private WinFormsLabel lblLeftWidth = null!;
        private NumericUpDown numLeftWidth = null!;
        private WinFormsLabel lblRightWidth = null!;
        private NumericUpDown numRightWidth = null!;
        private Button btnPickSampleLine = null!;

        private WinFormsLabel lblSLStyle = null!;
        private ComboBox cmbSLStyle = null!;
        private WinFormsLabel lblLabelStyle = null!;
        private ComboBox cmbLabelStyle = null!;

        // Group 3: Options
        private GroupBox grpOptions = null!;
        private RadioButton rbSkipDuplicate = null!;
        private RadioButton rbRenameDuplicate = null!;
        private RadioButton rbCreateDuplicate = null!;

        // Group 4: Preview DataGridView
        private GroupBox grpPreview = null!;
        private Panel pnlToolbar = null!;
        private Button btnSelectAll = null!;
        private Button btnDeselectAll = null!;
        private Button btnSelectValid = null!;
        private WinFormsLabel lblSummary = null!;
        private DataGridView dgvPreview = null!;

        // Bottom Actions
        private ProgressBar progressBar = null!;
        private WinFormsLabel lblStatusMsg = null!;
        private Button btnExecute = null!;
        private Button btnCancel = null!;

        #endregion

        public PhatSinhCocTheoBangForm(ObjectId initialTableId)
        {
            _selectedTableId = initialTableId;
            InitializeComponent();
            RestoreLastSettings();

            LoadAlignments();
            LoadSampleLineStyles();
            LoadLabelStyles();

            if (!_selectedTableId.IsNull && _selectedTableId.IsValid && !_selectedTableId.IsErased)
            {
                LoadTableStructureAndData(_selectedTableId);
            }
            else if (!_lastTableId.IsNull && _lastTableId.IsValid && !_lastTableId.IsErased)
            {
                _selectedTableId = _lastTableId;
                LoadTableStructureAndData(_selectedTableId);
            }
            else
            {
                lblTableStatus.Text = "Chưa chọn bảng AutoCAD nào. Vui lòng bấm [Chọn Bảng Trên CAD].";
                lblTableStatus.ForeColor = Color.DarkOrange;
            }

            this.FormClosing += (s, e) => SaveCurrentSettings();
        }

        #region UI Initialization

        private void InitializeComponent()
        {
            var standardFont = new WinFormsFont("Segoe UI", 9.5F, FontStyle.Regular);
            var boldFont = new WinFormsFont("Segoe UI", 9.5F, FontStyle.Bold);
            var titleFont = new WinFormsFont("Segoe UI", 12.5F, FontStyle.Bold);
            var headerFont = new WinFormsFont("Segoe UI", 9.5F, FontStyle.Bold);
            var smallFont = new WinFormsFont("Segoe UI", 9F, FontStyle.Regular);

            this.SuspendLayout();

            this.Text = "Phát Sinh Cọc Theo Bảng Tọa Độ / Lý Trình - Civil 3D";
            this.Size = (_lastFormSize.Width >= 1060 && _lastFormSize.Height >= 750) ? _lastFormSize : new Size(1060, 750);
            this.MinimumSize = new Size(1040, 720);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = standardFont;
            this.BackColor = Color.FromArgb(248, 249, 250);
            this.AutoScaleMode = AutoScaleMode.None;

            // -------------------------------------------------------------
            // Banner Title
            // -------------------------------------------------------------
            this.lblTitle = new WinFormsLabel
            {
                Text = "PHÁT SINH CỌC THEO BẢNG TỌA ĐỘ / LÝ TRÌNH",
                Font = titleFont,
                ForeColor = Color.FromArgb(24, 43, 73),
                Location = new WinFormsPoint(18, 12),
                AutoSize = true
            };

            this.lblSubtitle = new WinFormsLabel
            {
                Text = "Tự động nhận diện cột Tên cọc và Lý trình (hỗ trợ Km1+785.75), xem trước trực quan và tạo SampleLine chính xác",
                Font = smallFont,
                ForeColor = Color.FromArgb(108, 117, 125),
                Location = new WinFormsPoint(19, 36),
                AutoSize = true
            };

            // -------------------------------------------------------------
            // Group 1: Table Source (Top Left)
            // -------------------------------------------------------------
            this.grpTable = new GroupBox
            {
                Text = "1. Bảng Dữ Liệu Nguồn (AutoCAD Table)",
                Font = headerFont,
                ForeColor = Color.FromArgb(33, 37, 41),
                Location = new WinFormsPoint(16, 62),
                Size = new Size(495, 178)
            };

            this.lblTableStatus = new WinFormsLabel
            {
                Text = "Bảng: Chưa chọn",
                Font = standardFont,
                ForeColor = Color.FromArgb(73, 80, 87),
                Location = new WinFormsPoint(12, 22),
                Size = new Size(310, 36),
                AutoEllipsis = true
            };

            this.btnPickTable = new Button
            {
                Text = "Chọn Bảng Trên CAD",
                Font = boldFont,
                BackColor = Color.FromArgb(222, 226, 230),
                Location = new WinFormsPoint(330, 22),
                Size = new Size(150, 32),
                UseVisualStyleBackColor = false
            };
            this.btnPickTable.Click += btnPickTable_Click;

            this.lblColName = new WinFormsLabel
            {
                Text = "Cột Tên cọc:",
                Font = standardFont,
                Location = new WinFormsPoint(12, 66),
                Size = new Size(85, 22)
            };

            this.cmbColName = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = standardFont,
                Location = new WinFormsPoint(100, 63),
                Size = new Size(220, 26)
            };
            this.cmbColName.SelectedIndexChanged += (s, e) => { if (!_isPopulatingUI) RefreshPreviewData(); };

            this.lblColStation = new WinFormsLabel
            {
                Text = "Cột Lý trình:",
                Font = standardFont,
                Location = new WinFormsPoint(12, 100),
                Size = new Size(85, 22)
            };

            this.cmbColStation = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = standardFont,
                Location = new WinFormsPoint(100, 97),
                Size = new Size(220, 26)
            };
            this.cmbColStation.SelectedIndexChanged += (s, e) => { if (!_isPopulatingUI) RefreshPreviewData(); };

            this.lblStartRow = new WinFormsLabel
            {
                Text = "Hàng bắt đầu:",
                Font = standardFont,
                Location = new WinFormsPoint(12, 134),
                Size = new Size(85, 22)
            };

            this.numStartRow = new NumericUpDown
            {
                Font = standardFont,
                Location = new WinFormsPoint(100, 131),
                Size = new Size(65, 25),
                Minimum = 1,
                Maximum = 9999,
                Value = 3
            };
            this.numStartRow.ValueChanged += (s, e) => { if (!_isPopulatingUI) RefreshPreviewData(); };

            this.lblEndRow = new WinFormsLabel
            {
                Text = "đến:",
                Font = standardFont,
                Location = new WinFormsPoint(172, 134),
                Size = new Size(35, 22)
            };

            this.numEndRow = new NumericUpDown
            {
                Font = standardFont,
                Location = new WinFormsPoint(210, 131),
                Size = new Size(65, 25),
                Minimum = 1,
                Maximum = 9999,
                Value = 10
            };
            this.numEndRow.ValueChanged += (s, e) => { if (!_isPopulatingUI) RefreshPreviewData(); };

            this.btnReloadTable = new Button
            {
                Text = "Cập Nhật\nPreview",
                Font = boldFont,
                BackColor = Color.FromArgb(233, 236, 239),
                Location = new WinFormsPoint(330, 63),
                Size = new Size(150, 93),
                UseVisualStyleBackColor = false
            };
            this.btnReloadTable.Click += (s, e) => RefreshPreviewData();

            grpTable.Controls.AddRange(new Control[] {
                lblTableStatus, btnPickTable,
                lblColName, cmbColName,
                lblColStation, cmbColStation,
                lblStartRow, numStartRow, lblEndRow, numEndRow,
                btnReloadTable
            });

            // -------------------------------------------------------------
            // Group 2: Alignment & SampleLineGroup (Top Right)
            // -------------------------------------------------------------
            this.grpAlignment = new GroupBox
            {
                Text = "2. Tim Tuyến và Nhóm Cọc (Civil 3D)",
                Font = headerFont,
                ForeColor = Color.FromArgb(33, 37, 41),
                Location = new WinFormsPoint(522, 62),
                Size = new Size(508, 178)
            };

            // Row 1: Tim tuyến & Chọn tuyến
            this.lblAlignment = new WinFormsLabel
            {
                Text = "Tim tuyến:",
                Font = standardFont,
                Location = new WinFormsPoint(12, 25),
                Size = new Size(70, 22)
            };

            this.cmbAlignment = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = standardFont,
                Location = new WinFormsPoint(82, 22),
                Size = new Size(280, 26)
            };
            this.cmbAlignment.SelectedIndexChanged += cmbAlignment_SelectedIndexChanged;

            this.btnPickAlignment = new Button
            {
                Text = "Chọn Tuyến",
                Font = standardFont,
                BackColor = Color.FromArgb(222, 226, 230),
                Location = new WinFormsPoint(368, 21),
                Size = new Size(126, 28),
                UseVisualStyleBackColor = false
            };
            this.btnPickAlignment.Click += btnPickAlignment_Click;

            // Row 2: Nhóm cọc & Tên nhóm mới
            this.lblGroup = new WinFormsLabel
            {
                Text = "Nhóm cọc:",
                Font = standardFont,
                Location = new WinFormsPoint(12, 59),
                Size = new Size(70, 22)
            };

            this.cmbGroup = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = standardFont,
                Location = new WinFormsPoint(82, 56),
                Size = new Size(165, 26)
            };
            this.cmbGroup.SelectedIndexChanged += cmbGroup_SelectedIndexChanged;

            this.lblNewGroupName = new WinFormsLabel
            {
                Text = "Tên nhóm:",
                Font = standardFont,
                Location = new WinFormsPoint(254, 59),
                Size = new Size(70, 22)
            };

            this.txtNewGroupName = new TextBox
            {
                Font = standardFont,
                Location = new WinFormsPoint(326, 56),
                Size = new Size(168, 25)
            };

            // Row 3: Bề rộng Trái/Phải & Lấy theo cọc mẫu
            this.lblLeftWidth = new WinFormsLabel
            {
                Text = "Rộng trái:",
                Font = standardFont,
                Location = new WinFormsPoint(12, 95),
                Size = new Size(65, 22)
            };

            this.numLeftWidth = new NumericUpDown
            {
                Font = standardFont,
                Location = new WinFormsPoint(80, 92),
                Size = new Size(65, 25),
                DecimalPlaces = 1,
                Minimum = 0.5m,
                Maximum = 1000m,
                Value = 15.0m
            };

            this.lblRightWidth = new WinFormsLabel
            {
                Text = "Rộng phải:",
                Font = standardFont,
                Location = new WinFormsPoint(152, 95),
                Size = new Size(65, 22)
            };

            this.numRightWidth = new NumericUpDown
            {
                Font = standardFont,
                Location = new WinFormsPoint(220, 92),
                Size = new Size(65, 25),
                DecimalPlaces = 1,
                Minimum = 0.5m,
                Maximum = 1000m,
                Value = 15.0m
            };

            this.btnPickSampleLine = new Button
            {
                Text = "Lấy Theo Cọc Mẫu",
                Font = smallFont,
                BackColor = Color.FromArgb(233, 236, 239),
                Location = new WinFormsPoint(294, 91),
                Size = new Size(200, 28),
                UseVisualStyleBackColor = false
            };
            this.btnPickSampleLine.Click += btnPickSampleLine_Click;

            // Row 4: Style cọc & Nhãn cọc
            this.lblSLStyle = new WinFormsLabel
            {
                Text = "Style cọc:",
                Font = standardFont,
                Location = new WinFormsPoint(12, 131),
                Size = new Size(65, 22)
            };

            this.cmbSLStyle = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = standardFont,
                Location = new WinFormsPoint(80, 128),
                Size = new Size(165, 26)
            };

            this.lblLabelStyle = new WinFormsLabel
            {
                Text = "Nhãn cọc:",
                Font = standardFont,
                Location = new WinFormsPoint(252, 131),
                Size = new Size(68, 22)
            };

            this.cmbLabelStyle = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = standardFont,
                Location = new WinFormsPoint(324, 128),
                Size = new Size(170, 26)
            };

            grpAlignment.Controls.AddRange(new Control[] {
                lblAlignment, cmbAlignment, btnPickAlignment,
                lblGroup, cmbGroup, lblNewGroupName, txtNewGroupName,
                lblLeftWidth, numLeftWidth, lblRightWidth, numRightWidth, btnPickSampleLine,
                lblSLStyle, cmbSLStyle, lblLabelStyle, cmbLabelStyle
            });

            // -------------------------------------------------------------
            // Group 3: Options (Handling Duplicate Stations)
            // -------------------------------------------------------------
            this.grpOptions = new GroupBox
            {
                Text = "3. Tùy Chọn Xử Lý Cọc Trùng Lý Trình (Dung Sai ±0.01m)",
                Font = headerFont,
                ForeColor = Color.FromArgb(33, 37, 41),
                Location = new WinFormsPoint(16, 248),
                Size = new Size(1014, 55),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            this.rbSkipDuplicate = new RadioButton
            {
                Text = "Bỏ qua nếu đã có cọc cùng lý trình",
                Font = standardFont,
                Location = new WinFormsPoint(16, 22),
                AutoSize = true,
                Checked = true
            };
            this.rbSkipDuplicate.CheckedChanged += (s, e) => { if (rbSkipDuplicate.Checked) RefreshPreviewData(); };

            this.rbRenameDuplicate = new RadioButton
            {
                Text = "Đổi tên cọc cũ thành tên trong bảng",
                Font = standardFont,
                Location = new WinFormsPoint(320, 22),
                AutoSize = true
            };
            this.rbRenameDuplicate.CheckedChanged += (s, e) => { if (rbRenameDuplicate.Checked) RefreshPreviewData(); };

            this.rbCreateDuplicate = new RadioButton
            {
                Text = "Vẫn tạo cọc mới (cho phép cùng lý trình)",
                Font = standardFont,
                Location = new WinFormsPoint(630, 22),
                AutoSize = true
            };
            this.rbCreateDuplicate.CheckedChanged += (s, e) => { if (rbCreateDuplicate.Checked) RefreshPreviewData(); };

            grpOptions.Controls.AddRange(new Control[] {
                rbSkipDuplicate, rbRenameDuplicate, rbCreateDuplicate
            });

            // -------------------------------------------------------------
            // Group 4: Live Data Preview DataGridView
            // -------------------------------------------------------------
            this.grpPreview = new GroupBox
            {
                Text = "4. Xem Trước Dữ Liệu và Danh Sách Cọc Sẽ Tạo",
                Font = headerFont,
                ForeColor = Color.FromArgb(33, 37, 41),
                Location = new WinFormsPoint(16, 308),
                Size = new Size(1014, 340),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            this.pnlToolbar = new Panel
            {
                Location = new WinFormsPoint(10, 22),
                Size = new Size(994, 34),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            this.btnSelectAll = new Button
            {
                Text = "Chọn Tất Cả",
                Font = smallFont,
                Location = new WinFormsPoint(0, 2),
                Size = new Size(100, 28)
            };
            this.btnSelectAll.Click += (s, e) => SetAllCheckState(true);

            this.btnDeselectAll = new Button
            {
                Text = "Bỏ Chọn",
                Font = smallFont,
                Location = new WinFormsPoint(106, 2),
                Size = new Size(85, 28)
            };
            this.btnDeselectAll.Click += (s, e) => SetAllCheckState(false);

            this.btnSelectValid = new Button
            {
                Text = "Chỉ Chọn Cọc Hợp Lệ",
                Font = smallFont,
                Location = new WinFormsPoint(197, 2),
                Size = new Size(160, 28)
            };
            this.btnSelectValid.Click += (s, e) => SetOnlyValidCheckState();

            this.lblSummary = new WinFormsLabel
            {
                Text = "Tổng: 0 cọc",
                Font = boldFont,
                ForeColor = Color.FromArgb(13, 110, 253),
                TextAlign = ContentAlignment.MiddleRight,
                Location = new WinFormsPoint(365, 4),
                Size = new Size(625, 24),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };

            pnlToolbar.Controls.AddRange(new Control[] {
                btnSelectAll, btnDeselectAll, btnSelectValid, lblSummary
            });

            this.dgvPreview = new DataGridView
            {
                Location = new WinFormsPoint(10, 58),
                Size = new Size(994, 270),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Font = standardFont,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.Fixed3D,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None
            };

            var colCheck = new DataGridViewCheckBoxColumn
            {
                HeaderText = "Tạo",
                Name = "colCheck",
                Width = 50,
                Resizable = DataGridViewTriState.False
            };
            var colStt = new DataGridViewTextBoxColumn
            {
                HeaderText = "STT",
                Name = "colStt",
                Width = 55,
                ReadOnly = true
            };
            var colName = new DataGridViewTextBoxColumn
            {
                HeaderText = "Tên Cọc",
                Name = "colName",
                Width = 140,
                ReadOnly = false
            };
            var colRawStation = new DataGridViewTextBoxColumn
            {
                HeaderText = "Lý Trình (Bảng)",
                Name = "colRawStation",
                Width = 160,
                ReadOnly = true
            };
            var colStation = new DataGridViewTextBoxColumn
            {
                HeaderText = "Lý Trình (m)",
                Name = "colStation",
                Width = 130,
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
            };
            var colStatus = new DataGridViewTextBoxColumn
            {
                HeaderText = "Trạng Thái Kiểm Tra",
                Name = "colStatus",
                Width = 360,
                ReadOnly = true,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            };

            dgvPreview.Columns.AddRange(new DataGridViewColumn[] {
                colCheck, colStt, colName, colRawStation, colStation, colStatus
            });

            dgvPreview.CellContentClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex == 0)
                {
                    dgvPreview.CommitEdit(DataGridViewDataErrorContexts.Commit);
                    UpdateSummaryLabels();
                }
            };

            dgvPreview.CellValueChanged += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex == 2)
                {
                    string newName = dgvPreview.Rows[e.RowIndex].Cells[2].Value?.ToString() ?? "";
                    if (e.RowIndex < _currentCocItems.Count)
                    {
                        _currentCocItems[e.RowIndex].TenCoc = newName;
                    }
                }
            };

            grpPreview.Controls.AddRange(new Control[] {
                pnlToolbar, dgvPreview
            });

            // -------------------------------------------------------------
            // Bottom Footer
            // -------------------------------------------------------------
            this.progressBar = new ProgressBar
            {
                Location = new WinFormsPoint(16, 658),
                Size = new Size(620, 20),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Visible = false
            };

            this.lblStatusMsg = new WinFormsLabel
            {
                Text = "Sẵn sàng phát sinh cọc.",
                Font = standardFont,
                ForeColor = Color.FromArgb(73, 80, 87),
                Location = new WinFormsPoint(16, 682),
                Size = new Size(620, 22),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            this.btnExecute = new Button
            {
                Text = "Thực Hiện Tạo Cọc",
                Font = boldFont,
                BackColor = Color.FromArgb(25, 135, 84),
                ForeColor = Color.White,
                Location = new WinFormsPoint(655, 658),
                Size = new Size(225, 44),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                UseVisualStyleBackColor = false,
                Cursor = Cursors.Hand
            };
            this.btnExecute.Click += btnExecute_Click;

            this.btnCancel = new Button
            {
                Text = "Đóng",
                Font = standardFont,
                BackColor = Color.FromArgb(241, 243, 245),
                Location = new WinFormsPoint(890, 658),
                Size = new Size(140, 44),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                UseVisualStyleBackColor = false
            };
            this.btnCancel.Click += (s, e) => this.Close();

            this.Controls.AddRange(new Control[] {
                lblTitle, lblSubtitle,
                grpTable, grpAlignment, grpOptions, grpPreview,
                progressBar, lblStatusMsg, btnExecute, btnCancel
            });

            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        #region Persistent State Logic

        private void RestoreLastSettings()
        {
            try
            {
                numLeftWidth.Value = Math.Max(numLeftWidth.Minimum, Math.Min(numLeftWidth.Maximum, _lastLeftWidth));
                numRightWidth.Value = Math.Max(numRightWidth.Minimum, Math.Min(numRightWidth.Maximum, _lastRightWidth));

                if (_lastDuplicateMode == 0) rbSkipDuplicate.Checked = true;
                else if (_lastDuplicateMode == 1) rbRenameDuplicate.Checked = true;
                else if (_lastDuplicateMode == 2) rbCreateDuplicate.Checked = true;
            }
            catch { }
        }

        private void SaveCurrentSettings()
        {
            try
            {
                _lastTableId = _selectedTableId;
                _lastAlignmentId = _selectedAlignmentId;
                _lastSampleLineGroupId = _selectedGroupId;

                _lastColTenCoc = cmbColName.SelectedIndex >= 0 ? cmbColName.SelectedIndex : _lastColTenCoc;
                _lastColLyTrinh = cmbColStation.SelectedIndex >= 0 ? cmbColStation.SelectedIndex : _lastColLyTrinh;
                _lastStartRow = (int)numStartRow.Value - 1;

                _lastLeftWidth = numLeftWidth.Value;
                _lastRightWidth = numRightWidth.Value;

                _lastSampleLineStyle = cmbSLStyle.Text;
                _lastSampleLineLabelStyle = cmbLabelStyle.Text;

                if (rbSkipDuplicate.Checked) _lastDuplicateMode = 0;
                else if (rbRenameDuplicate.Checked) _lastDuplicateMode = 1;
                else _lastDuplicateMode = 2;

                _lastFormSize = this.Size;
            }
            catch { }
        }

        #endregion

        #region Civil 3D Data Loading

        private void LoadAlignments()
        {
            try
            {
                _isPopulatingUI = true;
                cmbAlignment.Items.Clear();

                CivilDocument civilDoc = CivilApplication.ActiveDocument;
                Database db = Application.DocumentManager.MdiActiveDocument.Database;

                using Transaction tr = db.TransactionManager.StartTransaction();
                ObjectIdCollection alignIds = civilDoc.GetAlignmentIds();

                int selectIdx = -1;
                for (int i = 0; i < alignIds.Count; i++)
                {
                    ObjectId aId = alignIds[i];
                    Alignment? align = tr.GetObject(aId, OpenMode.ForRead) as Alignment;
                    if (align != null)
                    {
                        string displayName = $"{align.Name} (Km{align.StartingStation / 1000.0:F3} - Km{align.EndingStation / 1000.0:F3})";
                        var item = new ObjectIdComboBoxItem { Text = displayName, Id = aId, Tag = align.Name };
                        cmbAlignment.Items.Add(item);

                        if (!_lastAlignmentId.IsNull && aId == _lastAlignmentId)
                        {
                            selectIdx = cmbAlignment.Items.Count - 1;
                        }
                    }
                }
                tr.Commit();

                if (cmbAlignment.Items.Count > 0)
                {
                    cmbAlignment.SelectedIndex = selectIdx >= 0 ? selectIdx : 0;
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Lỗi nạp danh sách tim tuyến: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _isPopulatingUI = false;
            }
        }

        private void LoadSampleLineStyles()
        {
            try
            {
                cmbSLStyle.Items.Clear();
                CivilDocument civilDoc = CivilApplication.ActiveDocument;
                Database db = Application.DocumentManager.MdiActiveDocument.Database;

                using Transaction tr = db.TransactionManager.StartTransaction();
                foreach (ObjectId styleId in civilDoc.Styles.SampleLineStyles)
                {
                    SampleLineStyle? style = tr.GetObject(styleId, OpenMode.ForRead) as SampleLineStyle;
                    if (style != null)
                    {
                        cmbSLStyle.Items.Add(new ObjectIdComboBoxItem { Text = style.Name, Id = styleId });
                    }
                }
                tr.Commit();

                SelectMatchingStyle(cmbSLStyle, _lastSampleLineStyle, "Road Sample Line");
            }
            catch { }
        }

        private void LoadLabelStyles()
        {
            try
            {
                cmbLabelStyle.Items.Clear();
                CivilDocument civilDoc = CivilApplication.ActiveDocument;
                Database db = Application.DocumentManager.MdiActiveDocument.Database;

                using Transaction tr = db.TransactionManager.StartTransaction();
                foreach (ObjectId styleId in civilDoc.Styles.LabelStyles.SampleLineLabelStyles.LabelStyles)
                {
                    LabelStyle? style = tr.GetObject(styleId, OpenMode.ForRead) as LabelStyle;
                    if (style != null)
                    {
                        cmbLabelStyle.Items.Add(new ObjectIdComboBoxItem { Text = style.Name, Id = styleId });
                    }
                }
                tr.Commit();

                SelectMatchingStyle(cmbLabelStyle, _lastSampleLineLabelStyle, "Tên cọc");
            }
            catch { }
        }

        private void SelectMatchingStyle(ComboBox cmb, string preferredName, string fallbackKeyword)
        {
            if (cmb.Items.Count == 0) return;

            if (!string.IsNullOrEmpty(preferredName))
            {
                for (int i = 0; i < cmb.Items.Count; i++)
                {
                    if (cmb.Items[i].ToString()?.Equals(preferredName, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        cmb.SelectedIndex = i;
                        return;
                    }
                }
            }

            for (int i = 0; i < cmb.Items.Count; i++)
            {
                if (cmb.Items[i].ToString()?.IndexOf(fallbackKeyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    cmb.SelectedIndex = i;
                    return;
                }
            }

            cmb.SelectedIndex = 0;
        }

        private void cmbAlignment_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (cmbAlignment.SelectedItem is ObjectIdComboBoxItem item)
            {
                _selectedAlignmentId = item.Id;
                txtNewGroupName.Text = item.Tag?.ToString() ?? "SLG_New";
                LoadSampleLineGroups(_selectedAlignmentId);
                RefreshPreviewData();
            }
        }

        private void LoadSampleLineGroups(ObjectId alignId)
        {
            try
            {
                cmbGroup.Items.Clear();
                cmbGroup.Items.Add(new ObjectIdComboBoxItem { Text = "[+ Tạo nhóm mới...]", Id = ObjectId.Null });

                if (alignId.IsNull || !alignId.IsValid) return;

                Database db = Application.DocumentManager.MdiActiveDocument.Database;
                using Transaction tr = db.TransactionManager.StartTransaction();

                Alignment? align = tr.GetObject(alignId, OpenMode.ForRead) as Alignment;
                if (align != null)
                {
                    ObjectIdCollection grpIds = align.GetSampleLineGroupIds();
                    int selectIdx = -1;

                    for (int i = 0; i < grpIds.Count; i++)
                    {
                        ObjectId gId = grpIds[i];
                        SampleLineGroup? grp = tr.GetObject(gId, OpenMode.ForRead) as SampleLineGroup;
                        if (grp != null)
                        {
                            var comboItem = new ObjectIdComboBoxItem { Text = grp.Name, Id = gId };
                            cmbGroup.Items.Add(comboItem);

                            if (!_lastSampleLineGroupId.IsNull && gId == _lastSampleLineGroupId)
                            {
                                selectIdx = cmbGroup.Items.Count - 1;
                            }
                        }
                    }

                    if (selectIdx >= 0)
                        cmbGroup.SelectedIndex = selectIdx;
                    else if (cmbGroup.Items.Count > 1)
                        cmbGroup.SelectedIndex = 1;
                    else
                        cmbGroup.SelectedIndex = 0;
                }
                tr.Commit();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Lỗi nạp nhóm cọc: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void cmbGroup_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (cmbGroup.SelectedItem is ObjectIdComboBoxItem item)
            {
                _selectedGroupId = item.Id;
                bool isNew = (_selectedGroupId == ObjectId.Null);
                txtNewGroupName.Enabled = isNew;

                if (!isNew)
                {
                    TryReadWidthFromGroup(_selectedGroupId);
                }

                RefreshPreviewData();
            }
        }

        private void TryReadWidthFromGroup(ObjectId groupId)
        {
            try
            {
                Database db = Application.DocumentManager.MdiActiveDocument.Database;
                using Transaction tr = db.TransactionManager.StartTransaction();
                SampleLineGroup? grp = tr.GetObject(groupId, OpenMode.ForRead) as SampleLineGroup;
                if (grp != null)
                {
                    ObjectIdCollection slIds = grp.GetSampleLineIds();
                    if (slIds.Count > 0)
                    {
                        SampleLine? sl = tr.GetObject(slIds[0], OpenMode.ForRead) as SampleLine;
                        if (sl != null && sl.Vertices.Count >= 3)
                        {
                            Point3d center = Point3d.Origin, left = Point3d.Origin, right = Point3d.Origin;
                            foreach (SampleLineVertex v in sl.Vertices)
                            {
                                if (v.Side == SampleLineVertexSideType.Center) center = v.Location;
                                else if (v.Side == SampleLineVertexSideType.Left) left = v.Location;
                                else if (v.Side == SampleLineVertexSideType.Right) right = v.Location;
                            }
                            double l = center.DistanceTo(left);
                            double r = center.DistanceTo(right);
                            if (l > 0.5 && r > 0.5)
                            {
                                numLeftWidth.Value = (decimal)Math.Round(l, 1);
                                numRightWidth.Value = (decimal)Math.Round(r, 1);
                            }
                        }
                    }
                }
                tr.Commit();
            }
            catch { }
        }

        #endregion

        #region Table Loading & Auto-detection

        public void LoadTableStructureAndData(ObjectId tableId)
        {
            try
            {
                _isPopulatingUI = true;
                _selectedTableId = tableId;

                Database db = Application.DocumentManager.MdiActiveDocument.Database;
                using Transaction tr = db.TransactionManager.StartTransaction();

                ATable? table = tr.GetObject(tableId, OpenMode.ForRead) as ATable;
                if (table == null)
                {
                    lblTableStatus.Text = "Đối tượng không phải là AutoCAD Table hợp lệ!";
                    lblTableStatus.ForeColor = Color.Red;
                    return;
                }

                int totalRows = table.Rows.Count;
                int totalCols = table.Columns.Count;

                if (totalRows < 2 || totalCols < 2)
                {
                    lblTableStatus.Text = $"Bảng quá nhỏ ({totalRows} hàng x {totalCols} cột). Cần tối thiểu 2 hàng, 2 cột!";
                    lblTableStatus.ForeColor = Color.DarkOrange;
                    return;
                }

                string title = GetTableTitle(table);
                lblTableStatus.Text = $"Đã chọn: Bảng ({totalRows} hàng x {totalCols} cột){(string.IsNullOrEmpty(title) ? "" : " - " + title)}";
                lblTableStatus.ForeColor = Color.FromArgb(25, 135, 84);

                cmbColName.Items.Clear();
                cmbColStation.Items.Clear();

                int detectedNameCol = -1;
                int detectedStationCol = -1;
                int detectedHeaderRow = -1;

                for (int r = 0; r < Math.Min(4, totalRows); r++)
                {
                    int foundNameInRow = -1;
                    int foundStationInRow = -1;

                    for (int c = 0; c < totalCols; c++)
                    {
                        string cellText = StationParser.CleanMText(table.Cells[r, c].GetTextString(FormatOption.IgnoreMtextFormat)).ToLower();

                        if (foundNameInRow == -1 && (cellText.Contains("tên cọc") || cellText.Contains("ten coc") || cellText.Contains("tên") || cellText == "cọc" || cellText.Contains("point") || cellText.Contains("name")))
                        {
                            foundNameInRow = c;
                        }

                        if (foundStationInRow == -1 && (cellText.Contains("lý trình") || cellText.Contains("ly trinh") || cellText.Contains("station") || cellText.Contains("km") || cellText.Contains("chainage")))
                        {
                            foundStationInRow = c;
                        }
                    }

                    if (foundNameInRow != -1 || foundStationInRow != -1)
                    {
                        detectedHeaderRow = r;
                        if (foundNameInRow != -1) detectedNameCol = foundNameInRow;
                        if (foundStationInRow != -1) detectedStationCol = foundStationInRow;
                        break;
                    }
                }

                int headerRowToUse = detectedHeaderRow >= 0 ? detectedHeaderRow : 0;
                for (int c = 0; c < totalCols; c++)
                {
                    string colHeader = StationParser.CleanMText(table.Cells[headerRowToUse, c].GetTextString(FormatOption.IgnoreMtextFormat));
                    if (string.IsNullOrWhiteSpace(colHeader)) colHeader = $"Cột {c + 1}";
                    string itemDisplay = $"{c + 1}. {colHeader}";

                    cmbColName.Items.Add(itemDisplay);
                    cmbColStation.Items.Add(itemDisplay);
                }

                if (detectedNameCol >= 0 && detectedNameCol < totalCols)
                    cmbColName.SelectedIndex = detectedNameCol;
                else if (_lastColTenCoc >= 0 && _lastColTenCoc < totalCols)
                    cmbColName.SelectedIndex = _lastColTenCoc;
                else
                    cmbColName.SelectedIndex = Math.Min(1, totalCols - 1);

                if (detectedStationCol >= 0 && detectedStationCol < totalCols)
                    cmbColStation.SelectedIndex = detectedStationCol;
                else if (_lastColLyTrinh >= 0 && _lastColLyTrinh < totalCols)
                    cmbColStation.SelectedIndex = _lastColLyTrinh;
                else
                    cmbColStation.SelectedIndex = Math.Min(2, totalCols - 1);

                int startRow = detectedHeaderRow >= 0 ? detectedHeaderRow + 2 : 2;
                numStartRow.Maximum = totalRows;
                numEndRow.Maximum = totalRows;

                numStartRow.Value = Math.Min(startRow, totalRows);
                numEndRow.Value = totalRows;

                tr.Commit();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Lỗi nạp cấu trúc bảng: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _isPopulatingUI = false;
                RefreshPreviewData();
            }
        }

        private string GetTableTitle(ATable table)
        {
            try
            {
                string t = StationParser.CleanMText(table.Cells[0, 0].GetTextString(FormatOption.IgnoreMtextFormat));
                if (t.Length > 40) t = t.Substring(0, 37) + "...";
                return t;
            }
            catch
            {
                return "";
            }
        }

        #endregion

        #region Live Preview Logic

        private void RefreshPreviewData()
        {
            if (_selectedTableId.IsNull || !_selectedTableId.IsValid || _selectedTableId.IsErased)
            {
                dgvPreview.Rows.Clear();
                _currentCocItems.Clear();
                UpdateSummaryLabels();
                return;
            }

            int colNameIdx = cmbColName.SelectedIndex;
            int colStationIdx = cmbColStation.SelectedIndex;
            int startRow1Based = (int)numStartRow.Value;
            int endRow1Based = (int)numEndRow.Value;

            if (colNameIdx < 0 || colStationIdx < 0 || startRow1Based > endRow1Based)
            {
                return;
            }

            try
            {
                Database db = Application.DocumentManager.MdiActiveDocument.Database;
                using Transaction tr = db.TransactionManager.StartTransaction();

                ATable? table = tr.GetObject(_selectedTableId, OpenMode.ForRead) as ATable;
                if (table == null) return;

                Alignment? align = null;
                if (!_selectedAlignmentId.IsNull && _selectedAlignmentId.IsValid)
                {
                    align = tr.GetObject(_selectedAlignmentId, OpenMode.ForRead) as Alignment;
                }

                HashSet<double> existingStations = new HashSet<double>();
                if (!_selectedGroupId.IsNull && _selectedGroupId.IsValid)
                {
                    SampleLineGroup? grp = tr.GetObject(_selectedGroupId, OpenMode.ForRead) as SampleLineGroup;
                    if (grp != null)
                    {
                        foreach (ObjectId slId in grp.GetSampleLineIds())
                        {
                            SampleLine? sl = tr.GetObject(slId, OpenMode.ForRead) as SampleLine;
                            if (sl != null)
                            {
                                existingStations.Add(Math.Round(sl.Station, 2));
                            }
                        }
                    }
                }

                _currentCocItems.Clear();
                dgvPreview.Rows.Clear();

                int sttCounter = 1;
                int startRow0Based = startRow1Based - 1;
                int endRow0Based = Math.Min(endRow1Based - 1, table.Rows.Count - 1);

                for (int r = startRow0Based; r <= endRow0Based; r++)
                {
                    string rawName = StationParser.CleanMText(table.Cells[r, colNameIdx].GetTextString(FormatOption.IgnoreMtextFormat));
                    string rawStation = StationParser.CleanMText(table.Cells[r, colStationIdx].GetTextString(FormatOption.IgnoreMtextFormat));

                    if (string.IsNullOrWhiteSpace(rawName) && string.IsNullOrWhiteSpace(rawStation))
                        continue;

                    var item = new CocItemModel
                    {
                        Stt = sttCounter++,
                        TenCoc = string.IsNullOrWhiteSpace(rawName) ? $"Coc_{sttCounter - 1}" : rawName,
                        RawStationText = rawStation,
                        TableRowIndex = r
                    };

                    if (StationParser.TryParseStation(rawStation, out double parsedStation))
                    {
                        item.Station = parsedStation;
                        item.IsValidStation = true;

                        if (align != null)
                        {
                            if (parsedStation < align.StartingStation - 0.01 || parsedStation > align.EndingStation + 0.01)
                            {
                                item.IsOutOfRange = true;
                                item.StatusText = $"Ngoài tuyến ({align.StartingStation:F1}m - {align.EndingStation:F1}m)";
                                item.StatusColor = Color.FromArgb(220, 53, 69);
                                item.IsSelected = false;
                            }
                        }

                        if (!item.IsOutOfRange && existingStations.Contains(Math.Round(parsedStation, 2)))
                        {
                            item.IsDuplicateStation = true;
                            if (rbSkipDuplicate.Checked)
                            {
                                item.StatusText = "Trùng cọc cũ (Bỏ qua)";
                                item.StatusColor = Color.FromArgb(253, 126, 20);
                                item.IsSelected = false;
                            }
                            else if (rbRenameDuplicate.Checked)
                            {
                                item.StatusText = "Trùng cọc cũ (Đổi tên)";
                                item.StatusColor = Color.FromArgb(13, 110, 253);
                                item.IsSelected = true;
                            }
                            else
                            {
                                item.StatusText = "Trùng cọc cũ (Tạo mới)";
                                item.StatusColor = Color.FromArgb(108, 117, 125);
                                item.IsSelected = true;
                            }
                        }

                        if (!item.IsOutOfRange && !item.IsDuplicateStation)
                        {
                            item.StatusText = "Hợp lệ";
                            item.StatusColor = Color.FromArgb(25, 135, 84);
                            item.IsSelected = true;
                        }
                    }
                    else
                    {
                        item.IsValidStation = false;
                        item.StatusText = "Lỗi định dạng lý trình";
                        item.StatusColor = Color.FromArgb(220, 53, 69);
                        item.IsSelected = false;
                    }

                    _currentCocItems.Add(item);

                    int rowIdx = dgvPreview.Rows.Add(
                        item.IsSelected,
                        item.Stt,
                        item.TenCoc,
                        item.RawStationText,
                        item.IsValidStation ? item.Station.ToString("F2") : "--",
                        item.StatusText
                    );

                    var row = dgvPreview.Rows[rowIdx];
                    row.Cells[5].Style.ForeColor = item.StatusColor;
                    row.Cells[5].Style.Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold);

                    if (!item.IsValidStation || item.IsOutOfRange)
                    {
                        row.DefaultCellStyle.BackColor = Color.FromArgb(255, 245, 245);
                    }
                    else if (item.IsDuplicateStation)
                    {
                        row.DefaultCellStyle.BackColor = Color.FromArgb(255, 253, 235);
                    }
                }

                tr.Commit();
                UpdateSummaryLabels();
            }
            catch (System.Exception ex)
            {
                lblSummary.Text = $"Lỗi preview: {ex.Message}";
                lblSummary.ForeColor = Color.Red;
            }
        }

        private void UpdateSummaryLabels()
        {
            int total = dgvPreview.Rows.Count;
            int selectedCount = 0;
            int validCount = 0;
            int errorCount = 0;
            int outRangeCount = 0;
            int duplicateCount = 0;

            for (int i = 0; i < dgvPreview.Rows.Count; i++)
            {
                var row = dgvPreview.Rows[i];
                bool isChecked = Convert.ToBoolean(row.Cells[0].Value);
                if (isChecked) selectedCount++;

                if (i < _currentCocItems.Count)
                {
                    var item = _currentCocItems[i];
                    if (!item.IsValidStation) errorCount++;
                    else if (item.IsOutOfRange) outRangeCount++;
                    else if (item.IsDuplicateStation) duplicateCount++;
                    else validCount++;
                }
            }

            lblSummary.Text = $"Tổng: {total} cọc | Đã chọn: {selectedCount} | Hợp lệ: {validCount} | Ngoài tuyến: {outRangeCount} | Trùng: {duplicateCount} | Lỗi: {errorCount}";
            btnExecute.Enabled = (selectedCount > 0);
        }

        private void SetAllCheckState(bool isChecked)
        {
            for (int i = 0; i < dgvPreview.Rows.Count; i++)
            {
                dgvPreview.Rows[i].Cells[0].Value = isChecked;
                if (i < _currentCocItems.Count) _currentCocItems[i].IsSelected = isChecked;
            }
            UpdateSummaryLabels();
        }

        private void SetOnlyValidCheckState()
        {
            for (int i = 0; i < dgvPreview.Rows.Count; i++)
            {
                if (i < _currentCocItems.Count)
                {
                    var item = _currentCocItems[i];
                    bool shouldCheck = item.IsValidStation && !item.IsOutOfRange && (!item.IsDuplicateStation || !rbSkipDuplicate.Checked);
                    dgvPreview.Rows[i].Cells[0].Value = shouldCheck;
                    item.IsSelected = shouldCheck;
                }
            }
            UpdateSummaryLabels();
        }

        #endregion

        #region Canvas Interaction Events

        private void btnPickTable_Click(object? sender, EventArgs e)
        {
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            using (EditorUserInteraction ui = ed.StartUserInteraction(this))
            {
                PromptEntityOptions peo = new PromptEntityOptions("\nChọn bảng tọa độ cọc có lý trình (AutoCAD Table): ");
                peo.SetRejectMessage("\nĐối tượng chọn phải là bảng AutoCAD Table!");
                peo.AddAllowedClass(typeof(ATable), exactMatch: true);

                PromptEntityResult per = ed.GetEntity(peo);
                if (per.Status == PromptStatus.OK)
                {
                    _selectedTableId = per.ObjectId;
                    LoadTableStructureAndData(_selectedTableId);
                }
            }
        }

        private void btnPickAlignment_Click(object? sender, EventArgs e)
        {
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            using (EditorUserInteraction ui = ed.StartUserInteraction(this))
            {
                PromptEntityOptions peo = new PromptEntityOptions("\nChọn tim tuyến cần bổ sung cọc (Civil 3D Alignment): ");
                peo.SetRejectMessage("\nĐối tượng chọn phải là Civil 3D Alignment!");
                peo.AddAllowedClass(typeof(Alignment), exactMatch: true);

                PromptEntityResult per = ed.GetEntity(peo);
                if (per.Status == PromptStatus.OK)
                {
                    SelectAlignmentInCombo(per.ObjectId);
                }
            }
        }

        private void SelectAlignmentInCombo(ObjectId alignId)
        {
            for (int i = 0; i < cmbAlignment.Items.Count; i++)
            {
                if (cmbAlignment.Items[i] is ObjectIdComboBoxItem item && item.Id == alignId)
                {
                    cmbAlignment.SelectedIndex = i;
                    return;
                }
            }
        }

        private void btnPickSampleLine_Click(object? sender, EventArgs e)
        {
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            using (EditorUserInteraction ui = ed.StartUserInteraction(this))
            {
                PromptEntityOptions peo = new PromptEntityOptions("\nChọn một cọc mẫu để lấy bề rộng trái/phải (Sample Line): ");
                peo.SetRejectMessage("\nĐối tượng chọn phải là Civil 3D Sample Line!");
                peo.AddAllowedClass(typeof(SampleLine), exactMatch: true);

                PromptEntityResult per = ed.GetEntity(peo);
                if (per.Status == PromptStatus.OK)
                {
                    Database db = Application.DocumentManager.MdiActiveDocument.Database;
                    using Transaction tr = db.TransactionManager.StartTransaction();
                    SampleLine? sl = tr.GetObject(per.ObjectId, OpenMode.ForRead) as SampleLine;
                    if (sl != null && sl.Vertices.Count >= 3)
                    {
                        Point3d center = Point3d.Origin, left = Point3d.Origin, right = Point3d.Origin;
                        foreach (SampleLineVertex v in sl.Vertices)
                        {
                            if (v.Side == SampleLineVertexSideType.Center) center = v.Location;
                            else if (v.Side == SampleLineVertexSideType.Left) left = v.Location;
                            else if (v.Side == SampleLineVertexSideType.Right) right = v.Location;
                        }
                        double l = center.DistanceTo(left);
                        double r = center.DistanceTo(right);
                        if (l > 0.5 && r > 0.5)
                        {
                            numLeftWidth.Value = (decimal)Math.Round(l, 1);
                            numRightWidth.Value = (decimal)Math.Round(r, 1);
                            ed.WriteMessage($"\nĐã lấy bề rộng từ cọc '{sl.Name}': Trái = {l:F2}m, Phải = {r:F2}m.");
                        }
                    }
                    tr.Commit();
                }
            }
        }

        #endregion

        #region Execution Engine

        private void btnExecute_Click(object? sender, EventArgs e)
        {
            if (_selectedAlignmentId.IsNull || !_selectedAlignmentId.IsValid)
            {
                MessageBox.Show("Vui lòng chọn tim tuyến (Alignment) trước khi thực hiện!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            List<CocItemModel> itemsToCreate = new List<CocItemModel>();
            for (int i = 0; i < dgvPreview.Rows.Count; i++)
            {
                if (Convert.ToBoolean(dgvPreview.Rows[i].Cells[0].Value))
                {
                    if (i < _currentCocItems.Count)
                    {
                        var item = _currentCocItems[i];
                        item.TenCoc = dgvPreview.Rows[i].Cells[2].Value?.ToString() ?? item.TenCoc;
                        itemsToCreate.Add(item);
                    }
                }
            }

            if (itemsToCreate.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn ít nhất một cọc trong bảng để tạo!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SaveCurrentSettings();

            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            progressBar.Visible = true;
            progressBar.Minimum = 0;
            progressBar.Maximum = itemsToCreate.Count;
            progressBar.Value = 0;
            btnExecute.Enabled = false;

            int createdCount = 0;
            int renamedCount = 0;
            int skippedCount = 0;
            int errorCount = 0;

            double leftWidth = (double)numLeftWidth.Value;
            double rightWidth = (double)numRightWidth.Value;
            int duplicateMode = rbSkipDuplicate.Checked ? 0 : (rbRenameDuplicate.Checked ? 1 : 2);

            ObjectId slStyleId = (_cmbSLStyleSelected() != ObjectId.Null) ? _cmbSLStyleSelected() : ObjectId.Null;
            ObjectId labelStyleId = (_cmbLabelStyleSelected() != ObjectId.Null) ? _cmbLabelStyleSelected() : ObjectId.Null;

            string newGroupName = txtNewGroupName.Text.Trim();
            bool isNewGroup = (_selectedGroupId == ObjectId.Null);

            try
            {
                using (DocumentLock docLock = doc.LockDocument())
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    Alignment? alignment = tr.GetObject(_selectedAlignmentId, OpenMode.ForWrite) as Alignment;
                    if (alignment == null)
                    {
                        MessageBox.Show("Không thể truy cập Alignment!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    ObjectId slgId = _selectedGroupId;
                    if (isNewGroup || slgId == ObjectId.Null)
                    {
                        string grpName = string.IsNullOrWhiteSpace(newGroupName) ? alignment.Name : newGroupName;
                        slgId = SampleLineGroup.Create(grpName, alignment.ObjectId);
                    }

                    SampleLineGroup? slg = tr.GetObject(slgId, OpenMode.ForWrite) as SampleLineGroup;
                    if (slg == null)
                    {
                        MessageBox.Show("Không thể truy cập SampleLineGroup!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    Dictionary<double, ObjectId> existingStationMap = new Dictionary<double, ObjectId>();
                    foreach (ObjectId slId in slg.GetSampleLineIds())
                    {
                        SampleLine? sl = tr.GetObject(slId, OpenMode.ForRead) as SampleLine;
                        if (sl != null)
                        {
                            double rSt = Math.Round(sl.Station, 2);
                            if (!existingStationMap.ContainsKey(rSt))
                                existingStationMap[rSt] = slId;
                        }
                    }

                    for (int i = 0; i < itemsToCreate.Count; i++)
                    {
                        var item = itemsToCreate[i];
                        double station = item.Station;

                        if (station < alignment.StartingStation || station > alignment.EndingStation)
                        {
                            ed.WriteMessage($"\nCọc '{item.TenCoc}' tại lý trình {station:F2} nằm ngoài phạm vi ({alignment.StartingStation:F2} - {alignment.EndingStation:F2}). Bỏ qua.");
                            skippedCount++;
                            progressBar.Value = i + 1;
                            continue;
                        }

                        if (station >= alignment.EndingStation - 0.001)
                            station = alignment.EndingStation - 0.001;
                        if (station <= alignment.StartingStation + 0.001)
                            station = alignment.StartingStation + 0.001;

                        double rSt = Math.Round(station, 2);

                        if (existingStationMap.TryGetValue(rSt, out ObjectId existingSlId))
                        {
                            if (duplicateMode == 0)
                            {
                                ed.WriteMessage($"\nLý trình {station:F2} đã có cọc. Bỏ qua '{item.TenCoc}'.");
                                skippedCount++;
                                progressBar.Value = i + 1;
                                continue;
                            }
                            else if (duplicateMode == 1)
                            {
                                SampleLine? existingSl = tr.GetObject(existingSlId, OpenMode.ForWrite) as SampleLine;
                                if (existingSl != null)
                                {
                                    string oldName = existingSl.Name;
                                    existingSl.Name = item.TenCoc;
                                    if (slStyleId != ObjectId.Null) existingSl.StyleId = slStyleId;
                                    ed.WriteMessage($"\nĐổi tên cọc cũ '{oldName}' thành '{item.TenCoc}' tại lý trình {station:F2}.");
                                    renamedCount++;
                                    progressBar.Value = i + 1;
                                    continue;
                                }
                            }
                        }

                        try
                        {
                            Point2dCollection pts = new Point2dCollection();
                            double easting = 0, northing = 0;

                            alignment.PointLocation(station, -leftWidth, ref easting, ref northing);
                            pts.Add(new Point2d(easting, northing));

                            alignment.PointLocation(station, rightWidth, ref easting, ref northing);
                            pts.Add(new Point2d(easting, northing));

                            string tempName = "z_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                            ObjectId newSlId = SampleLine.Create(tempName, slgId, pts);

                            if (newSlId != ObjectId.Null)
                            {
                                SampleLine? newSL = tr.GetObject(newSlId, OpenMode.ForWrite) as SampleLine;
                                if (newSL != null)
                                {
                                    try
                                    {
                                        newSL.Name = item.TenCoc;
                                    }
                                    catch
                                    {
                                        newSL.Name = item.TenCoc + "_" + (i + 1);
                                    }

                                    if (slStyleId != ObjectId.Null)
                                    {
                                        try { newSL.StyleId = slStyleId; } catch { }
                                    }

                                    createdCount++;
                                    existingStationMap[rSt] = newSlId;
                                    ed.WriteMessage($"\nĐã tạo cọc '{newSL.Name}' tại lý trình {station:F2} (Trái: {leftWidth}m, Phải: {rightWidth}m).");
                                }
                            }
                        }
                        catch (System.Exception ex)
                        {
                            ed.WriteMessage($"\nKhông thể tạo cọc '{item.TenCoc}' tại lý trình {station:F2}: {ex.Message}");
                            errorCount++;
                        }

                        progressBar.Value = i + 1;
                    }

                    if (labelStyleId != ObjectId.Null)
                    {
                        try
                        {
                            SampleLineLabelGroup.Create(slgId, labelStyleId);
                        }
                        catch { }
                    }

                    tr.Commit();
                }

                FormAccepted = true;
                string resultSummary = $"Hoàn tất phát sinh cọc theo bảng!\n" +
                                       $"• Tạo mới thành công: {createdCount} cọc\n" +
                                       $"• Cập nhật đổi tên: {renamedCount} cọc\n" +
                                       $"• Bỏ qua: {skippedCount} cọc\n" +
                                       (errorCount > 0 ? $"• Lỗi: {errorCount} cọc\n" : "");

                lblStatusMsg.Text = $"Hoàn tất: Đã tạo {createdCount} cọc, đổi tên {renamedCount} cọc.";
                lblStatusMsg.ForeColor = Color.FromArgb(25, 135, 84);

                MessageBox.Show(resultSummary, "Kết Quả Phát Sinh Cọc", MessageBoxButtons.OK, MessageBoxIcon.Information);

                RefreshPreviewData();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Lỗi thực thi: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                progressBar.Visible = false;
                btnExecute.Enabled = true;
            }
        }

        private ObjectId _cmbSLStyleSelected()
        {
            if (cmbSLStyle.SelectedItem is ObjectIdComboBoxItem item) return item.Id;
            return ObjectId.Null;
        }

        private ObjectId _cmbLabelStyleSelected()
        {
            if (cmbLabelStyle.SelectedItem is ObjectIdComboBoxItem item) return item.Id;
            return ObjectId.Null;
        }

        #endregion
    }
}

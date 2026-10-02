// (C) Copyright 2026 by T27
// Form giao diện in bản vẽ PDF theo Block - Tự động nhận diện khổ giấy và chiều ngang/đứng
// Plotter: DWG To PDF.pc3
// Cấu trúc tên file: [tên layout hiện hành]_[số thứ tự].pdf
//
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.PlottingServices;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsTextBox = System.Windows.Forms.TextBox;
using WinFormsButton = System.Windows.Forms.Button;
using WinFormsComboBox = System.Windows.Forms.ComboBox;
using DrawingFont = System.Drawing.Font;

namespace MyFirstProject.Acad_Tool
{
    /// <summary>
    /// Thứ tự sắp xếp các khung in
    /// </summary>
    public enum PrintSortOrder
    {
        LeftToRight = 0,    // Trái sang phải, trên xuống dưới
        TopToBottom = 1,    // Trên xuống dưới, trái sang phải
        Original = 2        // Giữ nguyên thứ tự chọn
    }

    /// <summary>
    /// Thông tin tiêu chuẩn khổ giấy ISO A-Series
    /// </summary>
    public class PaperSizeInfo
    {
        public string Name { get; set; }           // "A3", "A4", "A2", "A1", "A0"
        public string CanonicalName { get; set; }  // "ISO_full_bleed_A3_(420.00_x_297.00_MM)"
        public double LongSideMm { get; set; }
        public double ShortSideMm { get; set; }

        public PaperSizeInfo(string name, string canonical, double longSide, double shortSide)
        {
            Name = name;
            CanonicalName = canonical;
            LongSideMm = longSide;
            ShortSideMm = shortSide;
        }
    }

    /// <summary>
    /// Đại diện cho 1 Block khung bản vẽ được phát hiện
    /// </summary>
    public class BlockPrintItem
    {
        public int Index { get; set; }
        public ObjectId BlockId { get; set; }
        public string BlockName { get; set; } = "";
        public string LayoutName { get; set; } = "Model";
        public Point3d MinPoint { get; set; }
        public Point3d MaxPoint { get; set; }
        public double Width => MaxPoint.X - MinPoint.X;
        public double Height => MaxPoint.Y - MinPoint.Y;

        // Thuộc tính nhận diện tự động
        public bool IsDetected { get; set; } = false;
        public string PaperName { get; set; } = "Chưa rõ";
        public string CanonicalMediaName { get; set; } = "";
        public bool IsLandscape { get; set; } = true;
        public string OrientationText => IsLandscape ? "Ngang" : "Đứng";
        public string DetectedScaleText { get; set; } = "1:1";
        public string StatusText => IsDetected ? $"✅ {PaperName} ({OrientationText})" : "⚠️ Không xác định";
        public string ErrorMessage { get; set; } = "";

        public BlockPrintItem(int index, ObjectId blockId, string blockName, Point3d min, Point3d max, string layoutName = "Model")
        {
            Index = index;
            BlockId = blockId;
            BlockName = blockName;
            LayoutName = layoutName;

            double minX = Math.Min(min.X, max.X);
            double minY = Math.Min(min.Y, max.Y);
            double maxX = Math.Max(min.X, max.X);
            double maxY = Math.Max(min.Y, max.Y);

            MinPoint = new Point3d(minX, minY, 0);
            MaxPoint = new Point3d(maxX, maxY, 0);
        }
    }

    /// <summary>
    /// Form giao diện điều khiển lệnh In Bản Vẽ PDF (DWG To PDF.pc3)
    /// </summary>
    public class InDwgToPdfForm : Form
    {
        #region Persistent State (Ghi nhớ toàn bộ thông số giữa các lần chạy)
        private static string _lastBlockName = "";
        private static ObjectId _lastSampleBlockId = ObjectId.Null;
        private ObjectId _pickedSampleBlockId = ObjectId.Null;
        private static string _lastCtb = "monochrome.ctb";
        private static bool _lastCenterPlot = true;
        private static bool _lastPlotStyles = true;
        private static bool _lastLineweights = true;
        private static bool _lastFastMode = true;
        private static PrintSortOrder _lastSortOrder = PrintSortOrder.LeftToRight;
        private static string _lastOutputFolder = "";
        private static int _lastStartIndex = 1;
        private static int _lastDigits = 2;
        private static bool _lastOpenFolder = true;
        private static Size _lastFormSize = new Size(720, 770);
        #endregion

        #region Public Properties (Truy xuất từ Command logic)
        public List<BlockPrintItem> SelectedBlocks { get; private set; } = new();
        public string SelectedDeviceName { get; } = "DWG To PDF.pc3";
        public string SelectedPlotStyle { get; private set; } = "monochrome.ctb";
        public bool CenterPlot { get; private set; } = true;
        public bool PlotWithPlotStyles { get; private set; } = true;
        public bool PlotWithLineweights { get; private set; } = true;
        public bool FastMode { get; private set; } = true;
        public PrintSortOrder SortOrder { get; private set; } = PrintSortOrder.LeftToRight;
        public string OutputFolder { get; private set; } = "";
        public int StartIndex { get; private set; } = 1;
        public int Digits { get; private set; } = 2;
        public bool OpenFolderAfterPlot { get; private set; } = true;
        public bool IsConfirmed { get; private set; } = false;
        #endregion

        #region Standard ISO Papers Database
        private static readonly List<PaperSizeInfo> StandardPapers = new()
        {
            new PaperSizeInfo("A4", "ISO_full_bleed_A4_(297.00_x_210.00_MM)", 297.0, 210.0),
            new PaperSizeInfo("A3", "ISO_full_bleed_A3_(420.00_x_297.00_MM)", 420.0, 297.0),
            new PaperSizeInfo("A2", "ISO_full_bleed_A2_(594.00_x_420.00_MM)", 594.0, 420.0),
            new PaperSizeInfo("A1", "ISO_full_bleed_A1_(841.00_x_594.00_MM)", 841.0, 594.0),
            new PaperSizeInfo("A0", "ISO_full_bleed_A0_(1189.00_x_841.00_MM)", 1189.0, 841.0)
        };
        #endregion

        #region Controls UI
        private GroupBox grpBlockSelection = null!;
        private WinFormsButton btnPickBlock = null!;
        private WinFormsComboBox cboBlockName = null!;
        private WinFormsButton btnSelectArea = null!;
        private WinFormsButton btnScanBlocks = null!;
        private DataGridView dgvBlocks = null!;
        private WinFormsButton btnClearBlocks = null!;
        private WinFormsComboBox cboSortOrder = null!;
        private WinFormsLabel lblSummary = null!;

        private GroupBox grpSettings = null!;
        private WinFormsTextBox txtPlotter = null!;
        private WinFormsComboBox cboPlotStyle = null!;
        private CheckBox chkCenterPlot = null!;
        private CheckBox chkPlotStyles = null!;
        private CheckBox chkLineweights = null!;
        private CheckBox chkFastMode = null!;

        private GroupBox grpOutput = null!;
        private WinFormsTextBox txtOutputFolder = null!;
        private WinFormsButton btnBrowseFolder = null!;
        private WinFormsTextBox txtCurrentLayout = null!;
        private NumericUpDown nudStartIndex = null!;
        private NumericUpDown nudDigits = null!;
        private WinFormsLabel lblNamingRule = null!;
        private WinFormsLabel lblPreview = null!;
        private CheckBox chkOpenFolder = null!;

        private WinFormsButton btnExecute = null!;
        private WinFormsButton btnClose = null!;
        #endregion

        private readonly Editor _ed;
        private readonly Database _db;

        public string CurrentLayoutName => _db.TileMode ? "Model" : LayoutManager.Current.CurrentLayout;

        public InDwgToPdfForm(Editor ed, Database db)
        {
            _ed = ed;
            _db = db;

            InitializeComponent();
            LoadCtbStyles();
            LoadDrawingBlockNames();
            RestoreLastSettings();
            UpdateFilePreview();
        }

        private void InitializeComponent()
        {
            this.Text = "🖨️ In Bản Vẽ PDF Theo Block (DWG To PDF.pc3)";
            this.Font = new DrawingFont("Segoe UI", 9.25F, FontStyle.Regular, GraphicsUnit.Point);
            this.Size = _lastFormSize.Width > 0 ? _lastFormSize : new Size(720, 770);
            this.MinimumSize = new Size(680, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.ShowIcon = true;
            this.BackColor = SystemColors.Control;

            int padding = 12;
            int currentY = 10;

            // ==================== 1. GROUPBOX: CHỌN BLOCK KHUNG BẢN VẼ ====================
            grpBlockSelection = new GroupBox
            {
                Text = "📦 1. Chọn Block Khung Bản Vẽ (Tự động nhận diện khổ giấy & hướng)",
                Location = new Point(padding, currentY),
                Size = new Size(this.ClientSize.Width - padding * 2, 335),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };

            btnPickBlock = new WinFormsButton
            {
                Text = "📍 Pick Block mẫu",
                Location = new Point(15, 25),
                Size = new Size(130, 30),
                BackColor = Color.FromArgb(235, 245, 255),
                FlatStyle = FlatStyle.System
            };
            btnPickBlock.Click += BtnPickBlock_Click;

            var lblBlock = new WinFormsLabel { Text = "Tên Block:", Location = new Point(155, 31), AutoSize = true };
            cboBlockName = new WinFormsComboBox
            {
                Location = new Point(220, 27),
                Size = new Size(165, 25),
                DropDownStyle = ComboBoxStyle.DropDown
            };

            btnSelectArea = new WinFormsButton
            {
                Text = "📐 Chọn theo vùng",
                Location = new Point(395, 25),
                Size = new Size(135, 30),
                FlatStyle = FlatStyle.System
            };
            btnSelectArea.Click += BtnSelectArea_Click;

            btnScanBlocks = new WinFormsButton
            {
                Text = "🔍 Quét toàn bộ",
                Location = new Point(538, 25),
                Size = new Size(125, 30),
                FlatStyle = FlatStyle.System
            };
            btnScanBlocks.Click += BtnScanBlocks_Click;

            // DataGridView hiển thị danh sách các khung bản vẽ
            dgvBlocks = new DataGridView
            {
                Location = new Point(15, 66),
                Size = new Size(grpBlockSelection.Width - 30, 200),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.Fixed3D
            };

            dgvBlocks.Columns.Add("colIndex", "STT");
            dgvBlocks.Columns["colIndex"].Width = 42;
            dgvBlocks.Columns.Add("colName", "Tên Block");
            dgvBlocks.Columns["colName"].Width = 125;
            dgvBlocks.Columns.Add("colLayout", "Không gian");
            dgvBlocks.Columns["colLayout"].Width = 85;
            dgvBlocks.Columns.Add("colSize", "Kích thước (W x H)");
            dgvBlocks.Columns["colSize"].Width = 135;
            dgvBlocks.Columns.Add("colPaper", "Khổ giấy nhận diện");
            dgvBlocks.Columns["colPaper"].Width = 145;
            dgvBlocks.Columns.Add("colStatus", "Trạng thái");
            dgvBlocks.Columns["colStatus"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            // Dòng điều khiển dưới bảng
            var lblSort = new WinFormsLabel
            {
                Text = "Sắp xếp thứ tự:",
                Location = new Point(15, 273),
                AutoSize = true,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };

            cboSortOrder = new WinFormsComboBox
            {
                Location = new Point(110, 270),
                Size = new Size(240, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            cboSortOrder.Items.AddRange(new object[] {
                "Từ trái sang phải, trên xuống dưới",
                "Từ trên xuống dưới, trái sang phải",
                "Theo thứ tự tìm thấy trên bản vẽ"
            });
            cboSortOrder.SelectedIndex = 0;
            cboSortOrder.SelectedIndexChanged += CboSortOrder_SelectedIndexChanged;

            btnClearBlocks = new WinFormsButton
            {
                Text = "🗑️ Xóa danh sách",
                Location = new Point(360, 268),
                Size = new Size(120, 28),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                FlatStyle = FlatStyle.System
            };
            btnClearBlocks.Click += (s, e) => { SelectedBlocks.Clear(); RefreshGrid(); };

            lblSummary = new WinFormsLabel
            {
                Text = "Chưa có khung bản vẽ nào được chọn.",
                Location = new Point(15, 305),
                AutoSize = true,
                ForeColor = Color.DarkRed,
                Font = new DrawingFont(this.Font, FontStyle.Bold),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };

            grpBlockSelection.Controls.AddRange(new Control[] {
                btnPickBlock, lblBlock, cboBlockName, btnSelectArea, btnScanBlocks,
                dgvBlocks, lblSort, cboSortOrder, btnClearBlocks,
                lblSummary
            });

            currentY += grpBlockSelection.Height + 10;

            // ==================== 2. GROUPBOX: CẤU HÌNH IN ====================
            grpSettings = new GroupBox
            {
                Text = "⚙️ 2. Cấu Hình Plotter & Tối Ưu Tốc Độ In",
                Location = new Point(padding, currentY),
                Size = new Size(this.ClientSize.Width - padding * 2, 115),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            var lblPlotter = new WinFormsLabel { Text = "Plotter:", Location = new Point(15, 25), AutoSize = true };
            txtPlotter = new WinFormsTextBox
            {
                Text = "DWG To PDF.pc3 (Cố định chuẩn)",
                Location = new Point(68, 22),
                Size = new Size(220, 25),
                ReadOnly = true,
                BackColor = SystemColors.Info
            };

            var lblCtb = new WinFormsLabel { Text = "Nét in (CTB):", Location = new Point(305, 25), AutoSize = true };
            cboPlotStyle = new WinFormsComboBox
            {
                Location = new Point(385, 22),
                Size = new Size(210, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            chkCenterPlot = new CheckBox { Text = "Căn giữa trang (Center plot)", Location = new Point(68, 54), AutoSize = true, Checked = true };
            chkPlotStyles = new CheckBox { Text = "In theo nét CTB", Location = new Point(275, 54), AutoSize = true, Checked = true };
            chkLineweights = new CheckBox { Text = "In độ dày nét (Lineweights)", Location = new Point(410, 54), AutoSize = true, Checked = true };

            chkFastMode = new CheckBox
            {
                Text = "⚡ Tối ưu tốc độ in: Tắt PDFSHX Comments & Transparency (In nhanh gấp 3-5 lần, file cực nhẹ)",
                Location = new Point(68, 83),
                AutoSize = true,
                Checked = true,
                ForeColor = Color.FromArgb(0, 100, 180),
                Font = new DrawingFont(this.Font, FontStyle.Bold)
            };

            grpSettings.Controls.AddRange(new Control[] {
                lblPlotter, txtPlotter, lblCtb, cboPlotStyle,
                chkCenterPlot, chkPlotStyles, chkLineweights,
                chkFastMode
            });

            currentY += grpSettings.Height + 10;

            // ==================== 3. GROUPBOX: XUẤT FILE PDF ====================
            grpOutput = new GroupBox
            {
                Text = "💾 3. Xuất Từng File PDF (Cấu trúc: [Tên Layout hiện hành]_[Số thứ tự].pdf)",
                Location = new Point(padding, currentY),
                Size = new Size(this.ClientSize.Width - padding * 2, 125),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            var lblFolder = new WinFormsLabel { Text = "Thư mục lưu:", Location = new Point(15, 24), AutoSize = true };
            txtOutputFolder = new WinFormsTextBox
            {
                Location = new Point(95, 21),
                Size = new Size(grpOutput.Width - 215, 25),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            btnBrowseFolder = new WinFormsButton
            {
                Text = "📁 Duyệt...",
                Location = new Point(grpOutput.Width - 110, 19),
                Size = new Size(95, 28),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlatStyle = FlatStyle.System
            };
            btnBrowseFolder.Click += BtnBrowseFolder_Click;

            var lblLayout = new WinFormsLabel { Text = "Layout hiện hành:", Location = new Point(15, 56), AutoSize = true };
            txtCurrentLayout = new WinFormsTextBox
            {
                Text = CurrentLayoutName,
                Location = new Point(125, 53),
                Size = new Size(130, 25),
                ReadOnly = true,
                BackColor = Color.FromArgb(240, 248, 255),
                Font = new DrawingFont(this.Font, FontStyle.Bold)
            };

            var lblStart = new WinFormsLabel { Text = "STT bắt đầu:", Location = new Point(275, 56), AutoSize = true };
            nudStartIndex = new NumericUpDown
            {
                Location = new Point(355, 53),
                Size = new Size(55, 25),
                Minimum = 1,
                Maximum = 9999,
                Value = 1
            };
            nudStartIndex.ValueChanged += (s, e) => UpdateFilePreview();

            var lblDigits = new WinFormsLabel { Text = "Độ dài số:", Location = new Point(425, 56), AutoSize = true };
            nudDigits = new NumericUpDown
            {
                Location = new Point(490, 53),
                Size = new Size(45, 25),
                Minimum = 1,
                Maximum = 4,
                Value = 2
            };
            nudDigits.ValueChanged += (s, e) => UpdateFilePreview();

            chkOpenFolder = new CheckBox
            {
                Text = "Mở thư mục khi xong",
                Location = new Point(grpOutput.Width - 170, 55),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                AutoSize = true,
                Checked = true
            };

            lblNamingRule = new WinFormsLabel
            {
                Text = $"📌 Tiền tố cố định: [{CurrentLayoutName}_]",
                Location = new Point(15, 88),
                AutoSize = true,
                Font = new DrawingFont(this.Font, FontStyle.Bold),
                ForeColor = Color.DarkSlateBlue
            };

            lblPreview = new WinFormsLabel
            {
                Text = "📄 Xem trước: Đang nạp...",
                Location = new Point(220, 88),
                AutoSize = true,
                ForeColor = Color.FromArgb(0, 110, 50),
                Font = new DrawingFont(this.Font, FontStyle.Italic)
            };

            grpOutput.Controls.AddRange(new Control[] {
                lblFolder, txtOutputFolder, btnBrowseFolder,
                lblLayout, txtCurrentLayout,
                lblStart, nudStartIndex, lblDigits, nudDigits,
                chkOpenFolder, lblNamingRule, lblPreview
            });

            currentY += grpOutput.Height + 12;

            // ==================== 4. FOOTER BUTTONS ====================
            btnExecute = new WinFormsButton
            {
                Text = "🖨️ In Các Bản Vẽ PDF",
                Location = new Point(this.ClientSize.Width - padding - 290, currentY),
                Size = new Size(190, 36),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                Font = new DrawingFont(this.Font, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat
            };
            btnExecute.FlatAppearance.BorderSize = 0;
            btnExecute.Click += BtnExecute_Click;

            btnClose = new WinFormsButton
            {
                Text = "Đóng",
                Location = new Point(this.ClientSize.Width - padding - 90, currentY),
                Size = new Size(90, 36),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                FlatStyle = FlatStyle.System
            };
            btnClose.Click += (s, e) => this.Close();

            this.Controls.AddRange(new Control[] {
                grpBlockSelection, grpSettings, grpOutput,
                btnExecute, btnClose
            });

            this.FormClosing += InDwgToPdfForm_FormClosing;
        }

        private void UpdateFilePreview()
        {
            try
            {
                string layout = CurrentLayoutName;
                int start = (int)nudStartIndex.Value;
                int digits = (int)nudDigits.Value;
                string fmt = $"D{digits}";

                string f1 = $"{layout}_{start.ToString(fmt)}.pdf";
                string f2 = $"{layout}_{(start + 1).ToString(fmt)}.pdf";
                string f3 = $"{layout}_{(start + 2).ToString(fmt)}.pdf";

                lblNamingRule.Text = $"📌 Tiền tố cố định: [{layout}_]";
                lblPreview.Text = $"📄 Tên file mẫu: {f1}, {f2}, {f3}...";
            }
            catch { }
        }

        #region Nạp danh sách CTB & Tên Block
        private void LoadCtbStyles()
        {
            cboPlotStyle.Items.Clear();
            try
            {
                PlotSettingsValidator psv = PlotSettingsValidator.Current;
                var ctbList = psv.GetPlotStyleSheetList();
                if (ctbList != null && ctbList.Count > 0)
                {
                    foreach (string ctb in ctbList)
                    {
                        cboPlotStyle.Items.Add(ctb);
                    }
                }
            }
            catch { }

            if (cboPlotStyle.Items.Count == 0)
            {
                cboPlotStyle.Items.AddRange(new object[] { "monochrome.ctb", "acad.ctb", "grayscale.ctb", "None" });
            }

            int monoIdx = cboPlotStyle.FindStringExact("monochrome.ctb");
            cboPlotStyle.SelectedIndex = monoIdx >= 0 ? monoIdx : 0;
        }

        private void LoadDrawingBlockNames()
        {
            try
            {
                cboBlockName.Items.Clear();
                using (Transaction tr = _db.TransactionManager.StartTransaction())
                {
                    BlockTable bt = (BlockTable)tr.GetObject(_db.BlockTableId, OpenMode.ForRead);
                    foreach (ObjectId btrId in bt)
                    {
                        BlockTableRecord btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
                        if (!btr.IsLayout && !btr.IsAnonymous && !btr.Name.StartsWith("*"))
                        {
                            cboBlockName.Items.Add(btr.Name);
                        }
                    }
                    tr.Commit();
                }
            }
            catch { }
        }
        #endregion

        #region Helper Block Names & Extents
        /// <summary>
        /// Lấy tên thực tế của Block (xử lý chính xác Dynamic Block với tên ẩn *U...)
        /// </summary>
        public static string GetEffectiveBlockName(BlockReference blk, Transaction tr)
        {
            try
            {
                if (blk.IsDynamicBlock)
                {
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(blk.DynamicBlockTableRecord, OpenMode.ForRead);
                    return btr.Name;
                }
                else
                {
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(blk.BlockTableRecord, OpenMode.ForRead);
                    return btr.Name;
                }
            }
            catch
            {
                return "";
            }
        }

        /// <summary>
        /// Lấy Extents an toàn cho BlockReference (tránh crash khi block động rỗng hoặc chưa tính bounding box)
        /// </summary>
        public static Extents3d GetBlockExtentsSafe(BlockReference blk, Transaction tr)
        {
            try
            {
                return blk.GeometricExtents;
            }
            catch
            {
                try
                {
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(blk.BlockTableRecord, OpenMode.ForRead);
                    var ext = new Extents3d();
                    bool hasEntity = false;
                    foreach (ObjectId entId in btr)
                    {
                        Entity ent = (Entity)tr.GetObject(entId, OpenMode.ForRead);
                        if (ent.Visible)
                        {
                            try
                            {
                                ext.AddExtents(ent.GeometricExtents);
                                hasEntity = true;
                            }
                            catch { }
                        }
                    }
                    if (hasEntity)
                    {
                        ext.TransformBy(blk.BlockTransform);
                        return ext;
                    }
                }
                catch { }

                double scale = Math.Max(0.001, blk.ScaleFactors.X);
                Point3d pos = blk.Position;
                return new Extents3d(pos, new Point3d(pos.X + 420.0 * scale, pos.Y + 297.0 * scale, pos.Z));
            }
        }
        #endregion

        #region Thuật toán Tự động Nhận diện Khổ Giấy & Hướng (Paper Size Detection)
        /// <summary>
        /// Căn cứ vào kích thước block để tự động xác định khổ giấy và hướng in
        /// </summary>
        public static bool DetectPaperSize(double width, double height, double blockScale,
            out string paperCode, out string canonicalMediaName, out bool isLandscape, out string note)
        {
            paperCode = "A3";
            canonicalMediaName = "ISO_full_bleed_A3_(420.00_x_297.00_MM)";
            note = "";

            isLandscape = width >= height;
            double L = Math.Max(width, height);
            double S = Math.Min(width, height);

            if (S <= 0.01)
            {
                note = "Kích thước rỗng";
                return false;
            }

            // Danh sách các hệ số tỷ lệ thử nghiệm phong phú cho ngành hạ tầng / giao thông
            var testScales = new List<double>();
            if (blockScale > 0.0001) testScales.Add(blockScale);
            testScales.AddRange(new double[] {
                1.0, 1000.0, 500.0, 200.0, 250.0, 100.0, 50.0, 25.0, 20.0, 10.0, 5.0, 2.0,
                2000.0, 2500.0, 3000.0, 4000.0, 5000.0, 10000.0, 1500.0, 400.0, 75.0, 150.0, 300.0,
                0.001, 0.002, 0.005, 0.01, 0.02, 0.05, 0.1, 0.2, 0.5
            });

            // 1. Kiểm tra từng tỷ lệ theo kích thước bảng tiêu chuẩn
            foreach (double sc in testScales)
            {
                double normL = L / sc;
                double normS = S / sc;

                // Nếu đơn vị là mét (ví dụ 0.420 x 0.297), quy đổi sang mm
                if (normL < 3.0 && normL > 0.1)
                {
                    normL *= 1000.0;
                    normS *= 1000.0;
                }

                foreach (var paper in StandardPapers)
                {
                    double errL = Math.Abs(normL - paper.LongSideMm) / paper.LongSideMm;
                    double errS = Math.Abs(normS - paper.ShortSideMm) / paper.ShortSideMm;

                    // Cho phép sai số biên khung vẽ trong phạm vi 12% (bao quát cả lề gáy đóng sổ 25mm hoặc viền mép)
                    if (errL < 0.12 && errS < 0.12)
                    {
                        paperCode = paper.Name;

                        if (paper.Name == "A4" && !isLandscape)
                        {
                            canonicalMediaName = "ISO_full_bleed_A4_(210.00_x_297.00_MM)";
                        }
                        else
                        {
                            canonicalMediaName = paper.CanonicalName;
                        }

                        note = $"Tỷ lệ ~1:{sc:F0}";
                        return true;
                    }
                }
            }

            // 2. Nhận diện theo tỷ lệ khung hình chuẩn ISO A-Series (L / S ~ 1.414)
            double ratio = L / S;
            if (ratio >= 1.25 && ratio <= 1.60)
            {
                if (L <= 320.0)
                {
                    paperCode = "A4";
                    canonicalMediaName = isLandscape ? "ISO_full_bleed_A4_(297.00_x_210.00_MM)" : "ISO_full_bleed_A4_(210.00_x_297.00_MM)";
                }
                else
                {
                    paperCode = "A3";
                    canonicalMediaName = isLandscape ? "ISO_full_bleed_A3_(420.00_x_297.00_MM)" : "ISO_full_bleed_A3_(297.00_x_420.00_MM)";
                }
                note = $"Khung tỷ lệ A-Series ({ratio:F2})";
                return true;
            }

            // 3. Fallback an toàn cho mọi block hợp lệ để KHÔNG BAO GIỜ bị bỏ sót khi in
            paperCode = "A3";
            canonicalMediaName = isLandscape ? "ISO_full_bleed_A3_(420.00_x_297.00_MM)" : "ISO_full_bleed_A3_(297.00_x_420.00_MM)";
            note = $"Tự động Fit A3 (W={width:F0}, H={height:F0})";
            return true;
        }

        private void AutoDetectBlockItem(BlockPrintItem item, double blockScale)
        {
            if (DetectPaperSize(item.Width, item.Height, blockScale,
                out string paperCode, out string canonical, out bool isLandscape, out string note))
            {
                item.IsDetected = true;
                item.PaperName = paperCode;
                item.CanonicalMediaName = canonical;
                item.IsLandscape = isLandscape;
                item.DetectedScaleText = note;
            }
            else
            {
                item.IsDetected = true;
                item.PaperName = "A3";
                item.CanonicalMediaName = item.Width >= item.Height
                    ? "ISO_full_bleed_A3_(420.00_x_297.00_MM)"
                    : "ISO_full_bleed_A3_(297.00_x_420.00_MM)";
                item.IsLandscape = item.Width >= item.Height;
                item.DetectedScaleText = "Tự động Fit A3";
                item.ErrorMessage = note;
            }
        }
        #endregion

        #region Tương tác AutoCAD Canvas: Pick Block, Chọn Vùng & Quét Toàn Bộ

        /// <summary>
        /// Kiểm tra xem một BlockReference có khớp với tên đích không.
        /// So sánh cả tên effective (dynamic block gốc) LẪN tên anonymous (*U...) để không bao giờ bỏ sót.
        /// </summary>
        private static bool IsBlockNameMatch(BlockReference blk, Transaction tr, string targetName)
        {
            if (string.IsNullOrEmpty(targetName)) return true;

            // 1. So sánh effective name (tên gốc của dynamic block)
            string effectiveName = GetEffectiveBlockName(blk, tr);
            if (!string.IsNullOrEmpty(effectiveName) && string.Equals(effectiveName, targetName, StringComparison.OrdinalIgnoreCase))
                return true;

            // 2. So sánh anonymous name (tên *U... hoặc tên block thường)
            try
            {
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(blk.BlockTableRecord, OpenMode.ForRead);
                if (string.Equals(btr.Name, targetName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            catch { }

            return false;
        }

        /// <summary>
        /// Lấy tên hiển thị tốt nhất cho block (ưu tiên effective name, fallback anonymous name)
        /// </summary>
        private static string GetBestDisplayName(BlockReference blk, Transaction tr)
        {
            string name = GetEffectiveBlockName(blk, tr);
            if (!string.IsNullOrEmpty(name)) return name;

            try
            {
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(blk.BlockTableRecord, OpenMode.ForRead);
                return btr.Name;
            }
            catch { return ""; }
        }

        private void BtnPickBlock_Click(object? sender, EventArgs e)
        {
            using (_ed.StartUserInteraction(this))
            {
                var peo = new PromptEntityOptions("\nChọn một Block khung tên trên bản vẽ: ");
                peo.SetRejectMessage("\nĐối tượng phải là Block Reference.");
                peo.AddAllowedClass(typeof(BlockReference), exactMatch: false);

                var per = _ed.GetEntity(peo);
                if (per.Status != PromptStatus.OK) return;

                using (Transaction tr = _db.TransactionManager.StartTransaction())
                {
                    BlockReference blk = (BlockReference)tr.GetObject(per.ObjectId, OpenMode.ForRead);
                    string blockName = GetBestDisplayName(blk, tr);

                    _pickedSampleBlockId = blk.ObjectId;
                    _lastSampleBlockId = blk.ObjectId;

                    if (!string.IsNullOrEmpty(blockName))
                    {
                        if (!cboBlockName.Items.Contains(blockName))
                        {
                            cboBlockName.Items.Add(blockName);
                        }
                        cboBlockName.Text = blockName;
                    }

                    try
                    {
                        Extents3d ext = GetBlockExtentsSafe(blk, tr);

                        // Tránh nhân đôi nếu block mẫu đã có trong danh sách
                        SelectedBlocks.RemoveAll(b => b.BlockId == blk.ObjectId);

                        var item = new BlockPrintItem(SelectedBlocks.Count + 1, blk.ObjectId, blockName, ext.MinPoint, ext.MaxPoint, CurrentLayoutName);
                        AutoDetectBlockItem(item, blk.ScaleFactors.X);
                        SelectedBlocks.Add(item);

                        SortBlocks();
                        RefreshGrid();
                    }
                    catch (System.Exception ex)
                    {
                        _ed.WriteMessage($"\n❌ Không lấy được kích thước Block: {ex.Message}");
                    }

                    tr.Commit();
                    _ed.WriteMessage($"\n✅ Đã chọn Block mẫu: '{blockName}'. Bấm [Quét toàn bộ] hoặc [Chọn theo vùng] để nạp tất cả bản vẽ.");
                }
            }
        }

        private void BtnSelectArea_Click(object? sender, EventArgs e)
        {
            using (_ed.StartUserInteraction(this))
            {
                string targetName = cboBlockName.Text.Trim();
                var filterList = new List<TypedValue>
                {
                    new TypedValue((int)DxfCode.Start, "INSERT")
                };

                var filter = new SelectionFilter(filterList.ToArray());
                var pso = new PromptSelectionOptions
                {
                    MessageForAdding = string.IsNullOrEmpty(targetName)
                        ? "\nQuét chọn các khung block trên bản vẽ: "
                        : $"\nQuét chọn các khung block '{targetName}' trên bản vẽ: "
                };

                var psr = _ed.GetSelection(pso, filter);
                if (psr.Status != PromptStatus.OK || psr.Value == null) return;

                int added = 0;
                using (Transaction tr = _db.TransactionManager.StartTransaction())
                {
                    foreach (SelectedObject so in psr.Value)
                    {
                        if (so == null) continue;
                        if (SelectedBlocks.Any(b => b.BlockId == so.ObjectId)) continue;

                        BlockReference blk = (BlockReference)tr.GetObject(so.ObjectId, OpenMode.ForRead);

                        if (!string.IsNullOrEmpty(targetName) && !IsBlockNameMatch(blk, tr, targetName))
                        {
                            continue;
                        }

                        try
                        {
                            string bName = GetBestDisplayName(blk, tr);
                            Extents3d ext = GetBlockExtentsSafe(blk, tr);
                            var item = new BlockPrintItem(SelectedBlocks.Count + 1, blk.ObjectId, bName, ext.MinPoint, ext.MaxPoint, CurrentLayoutName);
                            AutoDetectBlockItem(item, blk.ScaleFactors.X);
                            SelectedBlocks.Add(item);
                            added++;
                        }
                        catch { }
                    }
                    tr.Commit();
                }

                SortBlocks();
                RefreshGrid();
                _ed.WriteMessage($"\n✅ Đã thêm {added} khung bản vẽ từ vùng chọn.");
            }
        }

        private void BtnScanBlocks_Click(object? sender, EventArgs e)
        {
            string targetName = cboBlockName.Text.Trim();
            if (string.IsNullOrEmpty(targetName))
            {
                MessageBox.Show("Vui lòng chọn hoặc nhập tên Block khung bản vẽ.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (Transaction tr = _db.TransactionManager.StartTransaction())
            {
                BlockTableRecord currentSpace;
                if (_db.TileMode)
                {
                    BlockTable bt = (BlockTable)tr.GetObject(_db.BlockTableId, OpenMode.ForRead);
                    currentSpace = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                }
                else
                {
                    LayoutManager lm = LayoutManager.Current;
                    DBDictionary layoutDict = (DBDictionary)tr.GetObject(_db.LayoutDictionaryId, OpenMode.ForRead);
                    Layout layout = (Layout)tr.GetObject(layoutDict.GetAt(lm.CurrentLayout), OpenMode.ForRead);
                    currentSpace = (BlockTableRecord)tr.GetObject(layout.BlockTableRecordId, OpenMode.ForRead);
                }

                SelectedBlocks.Clear();

                foreach (ObjectId id in currentSpace)
                {
                    if (id.ObjectClass.DxfName.Equals("INSERT", StringComparison.OrdinalIgnoreCase))
                    {
                        BlockReference blk = (BlockReference)tr.GetObject(id, OpenMode.ForRead);

                        // So sánh CẢ effective name LẪN anonymous name để không bao giờ bỏ sót
                        if (IsBlockNameMatch(blk, tr, targetName))
                        {
                            try
                            {
                                string displayName = GetBestDisplayName(blk, tr);
                                Extents3d ext = GetBlockExtentsSafe(blk, tr);
                                var item = new BlockPrintItem(SelectedBlocks.Count + 1, blk.ObjectId, displayName, ext.MinPoint, ext.MaxPoint, CurrentLayoutName);
                                AutoDetectBlockItem(item, blk.ScaleFactors.X);
                                SelectedBlocks.Add(item);
                            }
                            catch { }
                        }
                    }
                }
                tr.Commit();
            }

            SortBlocks();
            RefreshGrid();

            MessageBox.Show($"Đã quét và nhận diện {SelectedBlocks.Count} khung bản vẽ từ Block '{targetName}'.",
                "Hoàn tất quét", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void CboSortOrder_SelectedIndexChanged(object? sender, EventArgs e)
        {
            SortBlocks();
            RefreshGrid();
        }

        private void SortBlocks()
        {
            if (SelectedBlocks.Count <= 1) return;

            PrintSortOrder order = (PrintSortOrder)cboSortOrder.SelectedIndex;
            switch (order)
            {
                case PrintSortOrder.LeftToRight:
                    SelectedBlocks = SelectedBlocks.OrderBy(b => b.MinPoint.X).ThenByDescending(b => b.MaxPoint.Y).ToList();
                    break;
                case PrintSortOrder.TopToBottom:
                    SelectedBlocks = SelectedBlocks.OrderByDescending(b => b.MaxPoint.Y).ThenBy(b => b.MinPoint.X).ToList();
                    break;
                case PrintSortOrder.Original:
                    break;
            }

            for (int i = 0; i < SelectedBlocks.Count; i++)
            {
                SelectedBlocks[i].Index = i + 1;
            }
        }

        private void RefreshGrid()
        {
            dgvBlocks.Rows.Clear();

            int validCount = 0;
            foreach (var b in SelectedBlocks)
            {
                int rowIdx = dgvBlocks.Rows.Add(
                    b.Index,
                    b.BlockName,
                    b.LayoutName,
                    $"{b.Width:F0} x {b.Height:F0}",
                    b.IsDetected ? $"{b.PaperName} ({b.OrientationText})" : "Không rõ",
                    b.StatusText
                );

                if (b.IsDetected)
                {
                    validCount++;
                    dgvBlocks.Rows[rowIdx].DefaultCellStyle.ForeColor = Color.DarkGreen;
                }
                else
                {
                    dgvBlocks.Rows[rowIdx].DefaultCellStyle.ForeColor = Color.DarkRed;
                    dgvBlocks.Rows[rowIdx].DefaultCellStyle.BackColor = Color.FromArgb(255, 245, 235);
                }
            }

            if (SelectedBlocks.Count == 0)
            {
                lblSummary.Text = "Chưa có khung bản vẽ nào được chọn.";
                lblSummary.ForeColor = Color.DarkRed;
            }
            else
            {
                int invalidCount = SelectedBlocks.Count - validCount;
                if (invalidCount == 0)
                {
                    lblSummary.Text = $"✅ Tổng cộng: {SelectedBlocks.Count} bản vẽ - Tất cả đều nhận diện chuẩn.";
                    lblSummary.ForeColor = Color.DarkGreen;
                }
                else
                {
                    lblSummary.Text = $"⚠️ Tổng cộng: {SelectedBlocks.Count} bản vẽ ({validCount} hợp lệ, {invalidCount} không rõ khổ giấy).";
                    lblSummary.ForeColor = Color.DarkOrange;
                }
            }
        }

        private void BtnBrowseFolder_Click(object? sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Chọn thư mục lưu các file PDF xuất ra:";
                if (!string.IsNullOrEmpty(txtOutputFolder.Text) && Directory.Exists(txtOutputFolder.Text))
                {
                    fbd.SelectedPath = txtOutputFolder.Text;
                }

                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    txtOutputFolder.Text = fbd.SelectedPath;
                }
            }
        }
        #endregion

        #region Persistent State: Save & Restore
        private void RestoreLastSettings()
        {
            txtCurrentLayout.Text = CurrentLayoutName;

            if (_lastSampleBlockId != ObjectId.Null && _lastSampleBlockId.IsValid && !_lastSampleBlockId.IsErased)
            {
                _pickedSampleBlockId = _lastSampleBlockId;
                try
                {
                    using (Transaction tr = _db.TransactionManager.StartTransaction())
                    {
                        BlockReference sampleBlk = (BlockReference)tr.GetObject(_lastSampleBlockId, OpenMode.ForRead);
                        string sName = GetBestDisplayName(sampleBlk, tr);
                        if (!string.IsNullOrEmpty(sName))
                        {
                            if (!cboBlockName.Items.Contains(sName))
                            {
                                cboBlockName.Items.Add(sName);
                            }
                            cboBlockName.Text = sName;
                        }
                        tr.Commit();
                    }
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(_lastBlockName) && string.IsNullOrEmpty(cboBlockName.Text))
            {
                cboBlockName.Text = _lastBlockName;
            }

            int foundCtb = cboPlotStyle.FindStringExact(_lastCtb);
            if (foundCtb >= 0) cboPlotStyle.SelectedIndex = foundCtb;

            chkCenterPlot.Checked = _lastCenterPlot;
            chkPlotStyles.Checked = _lastPlotStyles;
            chkLineweights.Checked = _lastLineweights;
            chkFastMode.Checked = _lastFastMode;
            cboSortOrder.SelectedIndex = Math.Min((int)_lastSortOrder, cboSortOrder.Items.Count - 1);
            nudStartIndex.Value = Math.Max(1, _lastStartIndex);
            nudDigits.Value = Math.Max(1, Math.Min(4, _lastDigits));

            Document doc = Application.DocumentManager.MdiActiveDocument;
            string defaultFolder = "";
            if (doc != null && !string.IsNullOrEmpty(doc.Name))
            {
                try
                {
                    defaultFolder = Path.GetDirectoryName(doc.Name) ?? "";
                }
                catch { }
            }

            txtOutputFolder.Text = !string.IsNullOrEmpty(_lastOutputFolder) && Directory.Exists(_lastOutputFolder)
                ? _lastOutputFolder
                : defaultFolder;

            chkOpenFolder.Checked = _lastOpenFolder;
        }

        private void SaveCurrentSettings()
        {
            _lastBlockName = cboBlockName.Text.Trim();
            if (cboPlotStyle.SelectedItem != null)
                _lastCtb = cboPlotStyle.SelectedItem.ToString() ?? "monochrome.ctb";

            _lastCenterPlot = chkCenterPlot.Checked;
            _lastPlotStyles = chkPlotStyles.Checked;
            _lastLineweights = chkLineweights.Checked;
            _lastFastMode = chkFastMode.Checked;
            _lastSortOrder = (PrintSortOrder)cboSortOrder.SelectedIndex;
            _lastOutputFolder = txtOutputFolder.Text.Trim();
            _lastStartIndex = (int)nudStartIndex.Value;
            _lastDigits = (int)nudDigits.Value;
            _lastOpenFolder = chkOpenFolder.Checked;

            if (this.WindowState == FormWindowState.Normal)
            {
                _lastFormSize = this.Size;
            }
        }

        private void InDwgToPdfForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            SaveCurrentSettings();
        }
        #endregion

        #region Thực thi in
        private void BtnExecute_Click(object? sender, EventArgs e)
        {
            if (SelectedBlocks.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn ít nhất 1 Block khung bản vẽ trước khi in.", "Chưa có bản vẽ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string folder = txtOutputFolder.Text.Trim();
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                MessageBox.Show("Thư mục lưu PDF không hợp lệ hoặc chưa tồn tại. Vui lòng chọn lại.", "Lỗi thư mục", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Loại bỏ trùng lặp ObjectId (nếu có) để tránh in thừa
            var distinctBlocks = new List<BlockPrintItem>();
            var seenIds = new HashSet<long>();
            foreach (var b in SelectedBlocks)
            {
                long key = b.BlockId.Handle.Value;
                if (seenIds.Add(key))
                {
                    distinctBlocks.Add(b);
                }
            }
            SelectedBlocks = distinctBlocks;

            // Chuẩn hóa và fallback tất cả các block để KHÔNG BAO GIỜ bị bỏ sót khi in
            foreach (var b in SelectedBlocks)
            {
                if (string.IsNullOrEmpty(b.PaperName) || b.PaperName == "Chưa rõ" || string.IsNullOrEmpty(b.CanonicalMediaName))
                {
                    b.PaperName = "A3";
                    b.CanonicalMediaName = b.IsLandscape
                        ? "ISO_full_bleed_A3_(420.00_x_297.00_MM)"
                        : "ISO_full_bleed_A3_(297.00_x_420.00_MM)";
                    b.IsDetected = true;
                }
            }

            SelectedPlotStyle = cboPlotStyle.SelectedItem?.ToString() ?? "monochrome.ctb";
            CenterPlot = chkCenterPlot.Checked;
            PlotWithPlotStyles = chkPlotStyles.Checked;
            PlotWithLineweights = chkLineweights.Checked;
            FastMode = chkFastMode.Checked;
            SortOrder = (PrintSortOrder)cboSortOrder.SelectedIndex;
            OutputFolder = folder;
            StartIndex = (int)nudStartIndex.Value;
            Digits = (int)nudDigits.Value;
            OpenFolderAfterPlot = chkOpenFolder.Checked;

            SaveCurrentSettings();
            IsConfirmed = true;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
        #endregion
    }
}

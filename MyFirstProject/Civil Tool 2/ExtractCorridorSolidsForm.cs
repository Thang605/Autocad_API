using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsFont = System.Drawing.Font;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace MyFirstProject.Civil_Tool_2
{
    public enum OutputObjectType
    {
        SolidsBasedOnCorridorSampling = 0,
        SolidsSweptSolids = 1,
        Bodies = 2
    }

    public enum OutputDestination
    {
        InsertIntoCurrentDrawing = 0,
        AddToExistingDrawing = 1,
        AddToNewDrawing = 2
    }

    public class ExtractCorridorSolidsForm : Form
    {
        // ═══════════════════════════════════════════════════════════════
        //  PERSISTENT SETTINGS (Ghi nhớ cấu hình lần chạy trước)
        // ═══════════════════════════════════════════════════════════════
        private static ObjectId _lastCorridorId = ObjectId.Null;
        private static OutputObjectType _lastObjectType = OutputObjectType.SolidsBasedOnCorridorSampling;
        private static OutputDestination _lastDestination = OutputDestination.AddToNewDrawing;
        private static bool _lastDynamicLink = false;

        private static bool _lastExportShapes = true;
        private static bool _lastExportLinks = true;
        private static bool _lastFilterCodes = false;
        private static HashSet<string> _lastSelectedShapeCodes = new(StringComparer.OrdinalIgnoreCase);
        private static HashSet<string> _lastSelectedLinkCodes = new(StringComparer.OrdinalIgnoreCase);

        private static bool _lastAttachXData = true;

        private static string _lastFilePath = string.Empty;
        private static string _lastExportFolder = string.Empty;
        private static bool _lastOverwrite = true;
        private static bool _lastOpenAfterExport = false;
        private static bool _lastOpenFolderAfterExport = false;
        private static Size _lastFormSize = new Size(860, 580);

        private readonly Database _db;

        // ═══════════════════════════════════════════════════════════════
        //  OUTPUT PROPERTIES
        // ═══════════════════════════════════════════════════════════════
        public ObjectId SelectedCorridorId { get; private set; } = ObjectId.Null;
        public OutputObjectType ObjectType { get; private set; } = OutputObjectType.SolidsBasedOnCorridorSampling;
        public OutputDestination Destination { get; private set; } = OutputDestination.AddToNewDrawing;
        public bool DynamicLink => chkDynamicLink.Checked;

        public bool ExportShapes => chkExportShapes.Checked;
        public bool ExportLinks => chkExportLinks.Checked;
        public bool CreateSolidForShape => ObjectType != OutputObjectType.Bodies;
        public bool SweepSolidForShape => ObjectType == OutputObjectType.SolidsSweptSolids;
        public bool FilterCodes => radSelectedCodes.Checked;
        public List<string> IncludedCodes { get; private set; } = new();

        public bool AttachXData => chkAttachXData.Checked;

        public string OutputFilePath => txtFilePath.Text.Trim();
        public bool OverwriteIfExists => chkOverwrite.Checked;
        public bool OpenAfterExport => chkOpenAfterExport.Checked;
        public bool OpenFolderAfterExport => chkOpenFolderAfterExport.Checked;
        public bool FormAccepted { get; private set; } = false;

        // ═══════════════════════════════════════════════════════════════
        //  UI CONTROLS
        // ═══════════════════════════════════════════════════════════════
        private Panel pnlSidebar = null!;
        private ListBox lstSteps = null!;
        private Panel pnlMainContainer = null!;

        // Step Panels
        private Panel pnlStepCodes = null!;
        private Panel pnlStepProperty = null!;
        private Panel pnlStepOutput = null!;

        // Step 1: Codes to Extract
        private ComboBox cmbCorridor = null!;
        private Button btnPickCorridor = null!;
        private WinFormsLabel lblCorridorInfo = null!;
        private TextBox txtStep1FilePath = null!;
        private Button btnStep1Browse = null!;

        private CheckBox chkExportShapes = null!;
        private CheckBox chkExportLinks = null!;
        private RadioButton radAllCodes = null!;
        private RadioButton radSelectedCodes = null!;
        private TabControl tabCodes = null!;
        private CheckedListBox chkListShapes = null!;
        private CheckedListBox chkListLinks = null!;
        private Button btnSelectAllShapes = null!;
        private Button btnDeselectAllShapes = null!;
        private Button btnSelectAllLinks = null!;
        private Button btnDeselectAllLinks = null!;

        // Step 2: Property Data
        private CheckBox chkAttachXData = null!;

        // Step 3: Output Options (Khớp ảnh gốc Civil 3D)
        private TableLayoutPanel tblDestPath = null!;
        private ComboBox cmbObjectType = null!;
        private RadioButton radInsertCurrent = null!;
        private RadioButton radAddExisting = null!;
        private RadioButton radAddNew = null!;
        private TextBox txtFilePath = null!;
        private Button btnBrowse = null!;
        private CheckBox chkOverwrite = null!;
        private CheckBox chkOpenAfterExport = null!;
        private CheckBox chkOpenFolderAfterExport = null!;
        private CheckBox chkDynamicLink = null!;

        // Navigation bottom bar
        private WinFormsLabel lblStatus = null!;
        private Button btnPrevious = null!;
        private Button btnNext = null!;
        private Button btnExtract = null!;
        private Button btnCancel = null!;
        private Button btnHelp = null!;

        public ExtractCorridorSolidsForm(Database db)
        {
            _db = db;
            InitializeComponent();
            LoadData();
            RestoreLastSettings();
        }

        private void InitializeComponent()
        {
            this.Text = "Xuất Solid Từ Corridor — Extract Corridor Solids";
            this.Size = _lastFormSize;
            this.MinimumSize = new Size(840, 560);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.ShowIcon = false;
            this.Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Regular);
            this.BackColor = Color.FromArgb(245, 245, 248);

            // ═══════════════════════════════════════════════════════════════
            //  SIDEBAR PANEL (Bên trái: Các bước Wizard)
            // ═══════════════════════════════════════════════════════════════
            pnlSidebar = new Panel
            {
                Dock = DockStyle.Left,
                Width = 185,
                BackColor = Color.White,
                Padding = new Padding(12, 16, 8, 16)
            };

            var lblWizardTitle = new WinFormsLabel
            {
                Text = "Xuất Solid Corridor",
                Font = new WinFormsFont("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 40, 40),
                Dock = DockStyle.Top,
                Height = 32
            };

            lstSteps = new ListBox
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.None,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 34,
                Font = new WinFormsFont("Segoe UI", 9F),
                Cursor = Cursors.Hand
            };
            lstSteps.Items.Add("Codes to Extract");
            lstSteps.Items.Add("Property Data");
            lstSteps.Items.Add("Output Options");
            lstSteps.DrawItem += LstSteps_DrawItem;
            lstSteps.SelectedIndexChanged += (s, e) => SwitchStep(lstSteps.SelectedIndex);

            pnlSidebar.Controls.Add(lstSteps);
            pnlSidebar.Controls.Add(lblWizardTitle);

            // ═══════════════════════════════════════════════════════════════
            //  BOTTOM BAR (Thanh điều hướng dưới cùng)
            // ═══════════════════════════════════════════════════════════════
            var pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 54,
                BackColor = Color.FromArgb(240, 240, 244),
                Padding = new Padding(15, 8, 15, 10)
            };

            lblStatus = new WinFormsLabel
            {
                Text = "Sẵn sàng",
                Location = new Point(15, 14),
                Size = new Size(340, 26),
                ForeColor = Color.DarkSlateGray,
                Font = new WinFormsFont("Segoe UI", 8.5F, FontStyle.Italic),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            btnPrevious = new Button
            {
                Text = "< Previous",
                Location = new Point(365, 10),
                Size = new Size(82, 32),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            btnPrevious.Click += (s, e) => SwitchStep(lstSteps.SelectedIndex - 1);

            btnNext = new Button
            {
                Text = "Next >",
                Location = new Point(453, 10),
                Size = new Size(82, 32),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            btnNext.Click += (s, e) => SwitchStep(lstSteps.SelectedIndex + 1);

            btnExtract = new Button
            {
                Text = "Extract Solids",
                Location = new Point(542, 9),
                Size = new Size(120, 34),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            btnExtract.Click += BtnExtract_Click;

            btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(668, 10),
                Size = new Size(72, 32),
                DialogResult = DialogResult.Cancel,
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };

            btnHelp = new Button
            {
                Text = "Help",
                Location = new Point(746, 10),
                Size = new Size(65, 32),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            btnHelp.Click += (s, e) => ShowHelpDialog();

            pnlBottom.Controls.AddRange(new Control[] {
                lblStatus, btnPrevious, btnNext, btnExtract, btnCancel, btnHelp
            });

            // ═══════════════════════════════════════════════════════════════
            //  MAIN CONTENT CONTAINER
            // ═══════════════════════════════════════════════════════════════
            pnlMainContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(248, 248, 250),
                Padding = new Padding(15, 12, 15, 12)
            };

            InitializeStep1_Codes();
            InitializeStep2_Property();
            InitializeStep3_Output();

            pnlMainContainer.Controls.AddRange(new Control[] {
                pnlStepCodes, pnlStepProperty, pnlStepOutput
            });

            this.Controls.Add(pnlMainContainer);
            this.Controls.Add(pnlSidebar);
            this.Controls.Add(pnlBottom);

            this.AcceptButton = btnExtract;
            this.CancelButton = btnCancel;
            this.FormClosing += (s, e) => SaveCurrentSettings();
        }

        // ═══════════════════════════════════════════════════════════════
        //  STEP 1: CODES TO EXTRACT
        // ═══════════════════════════════════════════════════════════════
        private void InitializeStep1_Codes()
        {
            pnlStepCodes = new Panel
            {
                Dock = DockStyle.Fill,
                Visible = true
            };

            var grpCorridor = new GroupBox
            {
                Text = "Corridor nguồn & Vị trí xuất",
                Location = new Point(0, 0),
                Size = new Size(620, 118),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var lblSel = new WinFormsLabel { Text = "Corridor:", Location = new Point(12, 22), Size = new Size(65, 22) };
            cmbCorridor = new ComboBox
            {
                Location = new Point(80, 20),
                Size = new Size(385, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            cmbCorridor.SelectedIndexChanged += (s, e) => OnCorridorChanged();

            btnPickCorridor = new Button
            {
                Text = "🎯 Pick trên bản vẽ",
                Location = new Point(475, 18),
                Size = new Size(132, 26),
                Cursor = Cursors.Hand,
                Font = new WinFormsFont("Segoe UI", 8.5F, FontStyle.Bold),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnPickCorridor.Click += BtnPickCorridor_Click;

            lblCorridorInfo = new WinFormsLabel
            {
                Text = "Thông tin: Chưa chọn Corridor",
                Location = new Point(12, 48),
                Size = new Size(595, 24),
                ForeColor = Color.DarkSlateGray,
                Font = new WinFormsFont("Segoe UI", 8.5F, FontStyle.Italic),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var lblQuickFile = new WinFormsLabel
            {
                Text = "File xuất:",
                Location = new Point(12, 78),
                Size = new Size(65, 22),
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold)
            };

            txtStep1FilePath = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = new WinFormsFont("Segoe UI", 9F)
            };
            txtStep1FilePath.TextChanged += (s, e) =>
            {
                if (txtFilePath != null && txtFilePath.Text != txtStep1FilePath.Text)
                {
                    txtFilePath.Text = txtStep1FilePath.Text;
                }
                ValidateForm();
            };

            btnStep1Browse = new Button
            {
                Text = "...",
                Dock = DockStyle.Fill,
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                BackColor = Color.FromArgb(240, 240, 245),
                FlatStyle = FlatStyle.Standard
            };
            btnStep1Browse.Click += BtnBrowse_Click;

            // TableLayoutPanel tự động quản lý: cột 1 fill, cột 2 cố định 36px cho nút [...]
            var tblStep1Path = new TableLayoutPanel
            {
                Location = new Point(80, 75),
                Size = new Size(527, 28),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                ColumnCount = 2,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            tblStep1Path.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tblStep1Path.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36F));
            tblStep1Path.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tblStep1Path.Controls.Add(txtStep1FilePath, 0, 0);
            tblStep1Path.Controls.Add(btnStep1Browse, 1, 0);

            grpCorridor.Controls.AddRange(new Control[] {
                lblSel, cmbCorridor, btnPickCorridor, lblCorridorInfo,
                lblQuickFile, tblStep1Path
            });

            var grpCodes = new GroupBox
            {
                Text = "Lọc mã Code (Codes to Extract)",
                Location = new Point(0, 126),
                Size = new Size(620, 321),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            chkExportShapes = new CheckBox
            {
                Text = "Trích xuất Shapes (Khối kết cấu áo đường, vỉa hè, bó vỉa)",
                Location = new Point(15, 20),
                Size = new Size(380, 22),
                Checked = true,
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold)
            };
            chkExportShapes.CheckedChanged += (s, e) => ValidateForm();

            chkExportLinks = new CheckBox
            {
                Text = "Trích xuất Links (Bề mặt liên kết, taluy, khuôn đường)",
                Location = new Point(15, 42),
                Size = new Size(380, 22),
                Checked = true,
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold)
            };
            chkExportLinks.CheckedChanged += (s, e) => ValidateForm();

            radAllCodes = new RadioButton
            {
                Text = "Trích xuất toàn bộ mã Code trong Corridor",
                Location = new Point(15, 68),
                Size = new Size(270, 22),
                Checked = true
            };
            radAllCodes.CheckedChanged += (s, e) => UpdateCodeFilterState();

            radSelectedCodes = new RadioButton
            {
                Text = "Chỉ trích xuất các mã Code được đánh dấu bên dưới:",
                Location = new Point(290, 68),
                Size = new Size(315, 22)
            };
            radSelectedCodes.CheckedChanged += (s, e) => UpdateCodeFilterState();

            tabCodes = new TabControl
            {
                Location = new Point(15, 94),
                Size = new Size(592, 216),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Enabled = false
            };

            var tabShapes = new TabPage("Shape Codes");
            chkListShapes = new CheckedListBox
            {
                Location = new Point(8, 8),
                Size = new Size(450, 171),
                CheckOnClick = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            chkListShapes.ItemCheck += (s, e) => this.BeginInvoke(new Action(ValidateForm));

            btnSelectAllShapes = new Button { Text = "Chọn tất cả", Location = new Point(465, 8), Size = new Size(110, 26), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            btnSelectAllShapes.Click += (s, e) => SetAllChecked(chkListShapes, true);

            btnDeselectAllShapes = new Button { Text = "Bỏ chọn tất cả", Location = new Point(465, 38), Size = new Size(110, 26), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            btnDeselectAllShapes.Click += (s, e) => SetAllChecked(chkListShapes, false);

            tabShapes.Controls.AddRange(new Control[] { chkListShapes, btnSelectAllShapes, btnDeselectAllShapes });

            var tabLinks = new TabPage("Link Codes");
            chkListLinks = new CheckedListBox
            {
                Location = new Point(8, 8),
                Size = new Size(450, 171),
                CheckOnClick = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            chkListLinks.ItemCheck += (s, e) => this.BeginInvoke(new Action(ValidateForm));

            btnSelectAllLinks = new Button { Text = "Chọn tất cả", Location = new Point(465, 8), Size = new Size(110, 26), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            btnSelectAllLinks.Click += (s, e) => SetAllChecked(chkListLinks, true);

            btnDeselectAllLinks = new Button { Text = "Bỏ chọn tất cả", Location = new Point(465, 38), Size = new Size(110, 26), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            btnDeselectAllLinks.Click += (s, e) => SetAllChecked(chkListLinks, false);

            tabLinks.Controls.AddRange(new Control[] { chkListLinks, btnSelectAllLinks, btnDeselectAllLinks });

            tabCodes.TabPages.Add(tabShapes);
            tabCodes.TabPages.Add(tabLinks);

            grpCodes.Controls.AddRange(new Control[] {
                chkExportShapes, chkExportLinks, radAllCodes, radSelectedCodes, tabCodes
            });

            pnlStepCodes.Controls.AddRange(new Control[] { grpCorridor, grpCodes });
        }

        // ═══════════════════════════════════════════════════════════════
        //  STEP 2: PROPERTY DATA
        // ═══════════════════════════════════════════════════════════════
        private void InitializeStep2_Property()
        {
            pnlStepProperty = new Panel
            {
                Dock = DockStyle.Fill,
                Visible = false
            };

            var grpBim = new GroupBox
            {
                Text = "Dữ liệu thuộc tính BIM (Extended Entity Data / XData)",
                Location = new Point(0, 0),
                Size = new Size(620, 160),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            chkAttachXData = new CheckBox
            {
                Text = "Đính kèm metadata (Corridor Name, Shape/Link Code, Baseline, Region) vào XData của Solid",
                Location = new Point(15, 30),
                Size = new Size(585, 36),
                Checked = true,
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold)
            };

            var lblBimDesc = new WinFormsLabel
            {
                Text = "Giúp các mô hình 3D Solid giữ nguyên thông tin cấu kiện khi xuất sang Navisworks, Revit hoặc phần mềm BIM/IFC khác.",
                Location = new Point(35, 75),
                Size = new Size(565, 60),
                ForeColor = Color.DarkSlateGray
            };

            grpBim.Controls.AddRange(new Control[] { chkAttachXData, lblBimDesc });

            pnlStepProperty.Controls.Add(grpBim);
        }

        // ═══════════════════════════════════════════════════════════════
        //  STEP 3: OUTPUT OPTIONS (Giao diện chuẩn trong ảnh Civil 3D)
        // ═══════════════════════════════════════════════════════════════
        private void InitializeStep3_Output()
        {
            pnlStepOutput = new Panel
            {
                Dock = DockStyle.Fill,
                Visible = false
            };

            // Group 1: Output object type
            var grpObjType = new GroupBox
            {
                Text = "Output object type",
                Location = new Point(0, 0),
                Size = new Size(620, 75),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            cmbObjectType = new ComboBox
            {
                Location = new Point(15, 26),
                Size = new Size(590, 24),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            cmbObjectType.Items.Add("AutoCAD 3D Solids (based on corridor sampling)");
            cmbObjectType.Items.Add("AutoCAD 3D Solids (swept solids)");
            cmbObjectType.Items.Add("AutoCAD 3D Bodies");
            cmbObjectType.SelectedIndex = 0;
            cmbObjectType.SelectedIndexChanged += (s, e) => ValidateForm();

            grpObjType.Controls.Add(cmbObjectType);

            // Group 2: Output destination options (Chuẩn theo ảnh + Nút chọn đường dẫn nổi bật)
            var grpDestination = new GroupBox
            {
                Text = "Output destination options",
                Location = new Point(0, 85),
                Size = new Size(620, 230),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            radInsertCurrent = new RadioButton
            {
                Text = "Insert into current drawing",
                Location = new Point(15, 24),
                Size = new Size(380, 22),
                Checked = false
            };
            radInsertCurrent.CheckedChanged += (s, e) => UpdateDestinationState();

            radAddExisting = new RadioButton
            {
                Text = "Add to an existing drawing",
                Location = new Point(15, 48),
                Size = new Size(380, 22),
                Checked = false
            };
            radAddExisting.CheckedChanged += (s, e) => UpdateDestinationState();

            radAddNew = new RadioButton
            {
                Text = "Add to a new drawing",
                Location = new Point(15, 72),
                Size = new Size(380, 22),
                Checked = true
            };
            radAddNew.CheckedChanged += (s, e) => UpdateDestinationState();

            // TableLayoutPanel tự động quản lý: cột 1 fill, cột 2 cố định 36px cho nút [...]
            txtFilePath = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = new WinFormsFont("Segoe UI", 9F)
            };
            txtFilePath.TextChanged += (s, e) =>
            {
                if (txtStep1FilePath != null && txtStep1FilePath.Text != txtFilePath.Text)
                {
                    txtStep1FilePath.Text = txtFilePath.Text;
                }
                ValidateForm();
            };

            btnBrowse = new Button
            {
                Text = "...",
                Dock = DockStyle.Fill,
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                BackColor = Color.FromArgb(240, 240, 245),
                FlatStyle = FlatStyle.Standard
            };
            btnBrowse.Click += BtnBrowse_Click;

            tblDestPath = new TableLayoutPanel
            {
                Location = new Point(32, 98),
                Size = new Size(575, 28),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                ColumnCount = 2,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            tblDestPath.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tblDestPath.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36F));
            tblDestPath.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tblDestPath.Controls.Add(txtFilePath, 0, 0);
            tblDestPath.Controls.Add(btnBrowse, 1, 0);

            // Dynamic link to corridor (hiển thị chuẩn theo ảnh Civil 3D)
            chkDynamicLink = new CheckBox
            {
                Text = "Dynamic link to corridor",
                Location = new Point(15, 130),
                Size = new Size(300, 22),
                Checked = false,
                Enabled = false
            };

            chkOverwrite = new CheckBox
            {
                Text = "Ghi đè file nếu đã tồn tại",
                Location = new Point(15, 160),
                Size = new Size(180, 22),
                Checked = true
            };

            chkOpenAfterExport = new CheckBox
            {
                Text = "Mở file sau khi xuất",
                Location = new Point(205, 160),
                Size = new Size(160, 22),
                Checked = false
            };

            chkOpenFolderAfterExport = new CheckBox
            {
                Text = "Mở thư mục chứa file",
                Location = new Point(375, 160),
                Size = new Size(170, 22),
                Checked = false
            };

            grpDestination.Controls.AddRange(new Control[] {
                radInsertCurrent, radAddExisting, radAddNew,
                tblDestPath,
                chkDynamicLink,
                chkOverwrite, chkOpenAfterExport, chkOpenFolderAfterExport
            });

            pnlStepOutput.Controls.AddRange(new Control[] {
                grpObjType, grpDestination
            });
        }

        // ═══════════════════════════════════════════════════════════════
        //  STEP NAVIGATION & UI RENDERING
        // ═══════════════════════════════════════════════════════════════
        private void LstSteps_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= lstSteps.Items.Count) return;

            bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Color backColor = isSelected ? Color.FromArgb(235, 243, 252) : Color.White;
            Color textColor = isSelected ? Color.FromArgb(0, 120, 215) : Color.FromArgb(0, 102, 204);

            using (var b = new SolidBrush(backColor))
            {
                e.Graphics.FillRectangle(b, e.Bounds);
            }

            string text = lstSteps.Items[e.Index].ToString() ?? "";
            using (var font = new WinFormsFont("Segoe UI", 9.5F, isSelected ? FontStyle.Bold : FontStyle.Regular))
            using (var bText = new SolidBrush(textColor))
            {
                if (isSelected)
                {
                    using (var arrowBrush = new SolidBrush(Color.FromArgb(0, 120, 215)))
                    {
                        var arrowPoint = new PointF(e.Bounds.Left + 4, e.Bounds.Top + (e.Bounds.Height / 2f));
                        PointF[] points = {
                            new PointF(arrowPoint.X, arrowPoint.Y - 5),
                            new PointF(arrowPoint.X + 6, arrowPoint.Y),
                            new PointF(arrowPoint.X, arrowPoint.Y + 5)
                        };
                        e.Graphics.FillPolygon(arrowBrush, points);
                    }
                    e.Graphics.DrawString(text, font, bText, e.Bounds.Left + 16, e.Bounds.Top + 6);
                }
                else
                {
                    e.Graphics.DrawString(text, font, bText, e.Bounds.Left + 16, e.Bounds.Top + 6);
                }
            }

            e.DrawFocusRectangle();
        }

        private void SwitchStep(int index)
        {
            if (index < 0) index = 0;
            if (index > 2) index = 2;

            if (lstSteps.SelectedIndex != index)
            {
                lstSteps.SelectedIndex = index;
            }

            pnlStepCodes.Visible = (index == 0);
            pnlStepProperty.Visible = (index == 1);
            pnlStepOutput.Visible = (index == 2);

            btnPrevious.Enabled = (index > 0);
            btnNext.Enabled = (index < 2);

            lstSteps.Invalidate();
            ValidateForm();
        }

        private void UpdateDestinationState()
        {
            if (radInsertCurrent.Checked)
            {
                radAddExisting.Location = new Point(15, 48);
                radAddNew.Location = new Point(15, 72);
                tblDestPath.Visible = false;
                chkDynamicLink.Location = new Point(15, 104);
                chkOverwrite.Location = new Point(15, 134);
                chkOpenAfterExport.Location = new Point(205, 134);
                chkOpenFolderAfterExport.Location = new Point(375, 134);

                chkOverwrite.Enabled = false;
                chkOpenAfterExport.Enabled = false;
                chkOpenFolderAfterExport.Enabled = false;

                lblStatus.Text = "ℹ 3D Solids sẽ được chèn trực tiếp vào ModelSpace của bản vẽ hiện tại.";
                lblStatus.ForeColor = Color.DarkSlateGray;

                if (txtStep1FilePath != null) txtStep1FilePath.Enabled = false;
                if (btnStep1Browse != null) btnStep1Browse.Enabled = false;
            }
            else if (radAddExisting.Checked)
            {
                radAddExisting.Location = new Point(15, 48);
                tblDestPath.Location = new Point(32, 74);
                tblDestPath.Visible = true;
                radAddNew.Location = new Point(15, 106);
                chkDynamicLink.Location = new Point(15, 136);
                chkOverwrite.Location = new Point(15, 166);
                chkOpenAfterExport.Location = new Point(205, 166);
                chkOpenFolderAfterExport.Location = new Point(375, 166);

                txtFilePath.Enabled = true;
                btnBrowse.Enabled = true;
                chkOverwrite.Enabled = false;
                chkOpenAfterExport.Enabled = true;
                chkOpenFolderAfterExport.Enabled = true;

                if (txtStep1FilePath != null) txtStep1FilePath.Enabled = true;
                if (btnStep1Browse != null) btnStep1Browse.Enabled = true;

                lblStatus.Text = "ℹ 3D Solids sẽ được thêm vào file DWG đã có sẵn được chỉ định.";
                lblStatus.ForeColor = Color.DarkSlateGray;
            }
            else // radAddNew.Checked
            {
                radAddExisting.Location = new Point(15, 48);
                radAddNew.Location = new Point(15, 72);
                tblDestPath.Location = new Point(32, 98);
                tblDestPath.Visible = true;
                chkDynamicLink.Location = new Point(15, 130);
                chkOverwrite.Location = new Point(15, 160);
                chkOpenAfterExport.Location = new Point(205, 160);
                chkOpenFolderAfterExport.Location = new Point(375, 160);

                txtFilePath.Enabled = true;
                btnBrowse.Enabled = true;
                chkOverwrite.Enabled = true;
                chkOpenAfterExport.Enabled = true;
                chkOpenFolderAfterExport.Enabled = true;

                if (txtStep1FilePath != null) txtStep1FilePath.Enabled = true;
                if (btnStep1Browse != null) btnStep1Browse.Enabled = true;

                lblStatus.Text = "ℹ 3D Solids sẽ được lưu vào một file bản vẽ DWG mới.";
                lblStatus.ForeColor = Color.DarkSlateGray;
            }

            ValidateForm();
        }

        private void UpdateCodeFilterState()
        {
            tabCodes.Enabled = radSelectedCodes.Checked;
            ValidateForm();
        }

        private void SetAllChecked(CheckedListBox list, bool check)
        {
            for (int i = 0; i < list.Items.Count; i++)
            {
                list.SetItemChecked(i, check);
            }
            ValidateForm();
        }

        // ═══════════════════════════════════════════════════════════════
        //  CORRIDOR SELECTION & DATA LOADING
        // ═══════════════════════════════════════════════════════════════
        private void LoadData()
        {
            cmbCorridor.Items.Clear();
            try
            {
                using (var tr = _db.TransactionManager.StartTransaction())
                {
                    CivilDocument cdoc = CivilDocument.GetCivilDocument(_db);
                    if (cdoc != null)
                    {
                        foreach (ObjectId id in cdoc.CorridorCollection)
                        {
                            var c = tr.GetObject(id, OpenMode.ForRead) as Corridor;
                            if (c != null && !string.IsNullOrEmpty(c.Name))
                            {
                                cmbCorridor.Items.Add(new ExtractCorridorComboItem(c.Name, id));
                            }
                        }
                    }
                    tr.Commit();
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Lỗi nạp danh sách Corridor: {ex.Message}";
            }
        }

        private void OnCorridorChanged()
        {
            chkListShapes.Items.Clear();
            chkListLinks.Items.Clear();

            var item = cmbCorridor.SelectedItem as ExtractCorridorComboItem;
            if (item == null || item.Id.IsNull)
            {
                lblCorridorInfo.Text = "Thông tin: Chưa chọn Corridor";
                txtFilePath.Text = string.Empty;
                if (txtStep1FilePath != null) txtStep1FilePath.Text = string.Empty;
                btnExtract.Enabled = false;
                return;
            }

            try
            {
                using (var tr = _db.TransactionManager.StartTransaction())
                {
                    var corridor = tr.GetObject(item.Id, OpenMode.ForRead) as Corridor;
                    if (corridor != null)
                    {
                        int totalBaselines = corridor.Baselines.Count;
                        int totalRegions = 0;
                        foreach (Baseline bl in corridor.Baselines)
                        {
                            totalRegions += bl.BaselineRegions.Count;
                        }

                        string[] shapeCodes = corridor.GetShapeCodes();
                        if (shapeCodes != null)
                        {
                            foreach (string sc in shapeCodes.OrderBy(x => x))
                            {
                                if (!string.IsNullOrEmpty(sc))
                                {
                                    bool isChecked = _lastSelectedShapeCodes.Count == 0 || _lastSelectedShapeCodes.Contains(sc);
                                    chkListShapes.Items.Add(sc, isChecked);
                                }
                            }
                        }

                        string[] linkCodes = corridor.GetLinkCodes();
                        if (linkCodes != null)
                        {
                            foreach (string lc in linkCodes.OrderBy(x => x))
                            {
                                if (!string.IsNullOrEmpty(lc))
                                {
                                    bool isChecked = _lastSelectedLinkCodes.Count == 0 || _lastSelectedLinkCodes.Contains(lc);
                                    chkListLinks.Items.Add(lc, isChecked);
                                }
                            }
                        }

                        lblCorridorInfo.Text = $"Thông tin: {totalBaselines} Baseline, {totalRegions} Phân đoạn (Region). {chkListShapes.Items.Count} mã Shape, {chkListLinks.Items.Count} mã Link.";

                        GenerateDefaultFilePath(corridor.Name);
                    }
                    tr.Commit();
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Lỗi đọc thông tin Corridor: {ex.Message}";
            }

            ValidateForm();
        }

        private void GenerateDefaultFilePath(string corridorName)
        {
            string targetDir = _lastExportFolder;
            if (string.IsNullOrEmpty(targetDir) || !Directory.Exists(targetDir))
            {
                try
                {
                    string currentDrawingPath = _db.Filename;
                    if (!string.IsNullOrEmpty(currentDrawingPath) && File.Exists(currentDrawingPath))
                    {
                        targetDir = Path.GetDirectoryName(currentDrawingPath)!;
                    }
                }
                catch { }

                if (string.IsNullOrEmpty(targetDir) || !Directory.Exists(targetDir))
                {
                    targetDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                }
            }

            string drawingBaseName = "Drawing";
            try
            {
                if (!string.IsNullOrEmpty(_db.Filename))
                {
                    drawingBaseName = Path.GetFileNameWithoutExtension(_db.Filename);
                }
            }
            catch { }

            string safeCorridorName = string.Join("_", corridorName.Split(Path.GetInvalidFileNameChars()));
            string fileName = $"{drawingBaseName}_{safeCorridorName}_Solids.dwg";
            string fullPath = Path.Combine(targetDir, fileName);

            txtFilePath.Text = fullPath;
            if (txtStep1FilePath != null) txtStep1FilePath.Text = fullPath;
        }

        private void BtnPickCorridor_Click(object? sender, EventArgs e)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            using (var interaction = ed.StartUserInteraction(this))
            {
                var peo = new PromptEntityOptions("\nChọn Corridor trên bản vẽ: ");
                peo.SetRejectMessage("\nĐối tượng chọn phải là Corridor Civil 3D.");
                peo.AddAllowedClass(typeof(Corridor), exactMatch: false);

                var per = ed.GetEntity(peo);
                interaction.End();

                if (per.Status == PromptStatus.OK)
                {
                    SelectCorridorById(per.ObjectId);
                }
            }
        }

        public void SelectCorridorById(ObjectId id)
        {
            if (id.IsNull || !id.IsValid || id.IsErased) return;

            for (int i = 0; i < cmbCorridor.Items.Count; i++)
            {
                var item = cmbCorridor.Items[i] as ExtractCorridorComboItem;
                if (item != null && item.Id == id)
                {
                    cmbCorridor.SelectedIndex = i;
                    return;
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  FILE & FOLDER BROWSER DIALOGS (Chỉ đường dẫn xuất file DWG)
        // ═══════════════════════════════════════════════════════════════
        private void BtnBrowse_Click(object? sender, EventArgs e)
        {
            if (radAddExisting.Checked)
            {
                using (var ofd = new OpenFileDialog())
                {
                    ofd.Title = "Chọn file bản vẽ AutoCAD DWG có sẵn để thêm Solids";
                    ofd.Filter = "AutoCAD Drawing (*.dwg)|*.dwg|All files (*.*)|*.*";
                    if (File.Exists(txtFilePath.Text))
                    {
                        try { ofd.InitialDirectory = Path.GetDirectoryName(txtFilePath.Text); } catch { }
                    }
                    if (ofd.ShowDialog(this) == DialogResult.OK)
                    {
                        txtFilePath.Text = ofd.FileName;
                        if (txtStep1FilePath != null) txtStep1FilePath.Text = ofd.FileName;
                    }
                }
            }
            else
            {
                using (var sfd = new SaveFileDialog())
                {
                    sfd.Title = "Chọn vị trí lưu file AutoCAD 3D Solids DWG mới";
                    sfd.Filter = "AutoCAD Drawing (*.dwg)|*.dwg|All files (*.*)|*.*";
                    sfd.DefaultExt = "dwg";
                    if (!string.IsNullOrEmpty(txtFilePath.Text))
                    {
                        try
                        {
                            string dir = Path.GetDirectoryName(txtFilePath.Text)!;
                            if (Directory.Exists(dir)) sfd.InitialDirectory = dir;
                            sfd.FileName = Path.GetFileName(txtFilePath.Text);
                        }
                        catch { }
                    }
                    if (sfd.ShowDialog(this) == DialogResult.OK)
                    {
                        txtFilePath.Text = sfd.FileName;
                        if (txtStep1FilePath != null) txtStep1FilePath.Text = sfd.FileName;
                    }
                }
            }
        }

        private void BtnBrowseFolder_Click(object? sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Chọn thư mục lưu file AutoCAD 3D Solids DWG";
                string currentPath = txtFilePath.Text.Trim();
                if (!string.IsNullOrEmpty(currentPath))
                {
                    try
                    {
                        string dir = Path.GetDirectoryName(currentPath)!;
                        if (Directory.Exists(dir)) fbd.SelectedPath = dir;
                    }
                    catch { }
                }

                if (fbd.ShowDialog(this) == DialogResult.OK)
                {
                    string fileName = "Corridor_Solids.dwg";
                    if (!string.IsNullOrEmpty(currentPath))
                    {
                        try
                        {
                            string fn = Path.GetFileName(currentPath);
                            if (!string.IsNullOrEmpty(fn)) fileName = fn;
                        }
                        catch { }
                    }

                    string newFullPath = Path.Combine(fbd.SelectedPath, fileName);
                    txtFilePath.Text = newFullPath;
                    if (txtStep1FilePath != null) txtStep1FilePath.Text = newFullPath;
                    ValidateForm();
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  VALIDATION & EXECUTION
        // ═══════════════════════════════════════════════════════════════
        private void ValidateForm()
        {
            var item = cmbCorridor.SelectedItem as ExtractCorridorComboItem;
            if (item == null || item.Id.IsNull)
            {
                lblStatus.Text = "⚠ Vui lòng chọn Corridor cần trích xuất solid.";
                lblStatus.ForeColor = Color.Red;
                btnExtract.Enabled = false;
                return;
            }

            if (!chkExportShapes.Checked && !chkExportLinks.Checked)
            {
                lblStatus.Text = "⚠ Vui lòng tích chọn ít nhất Shapes hoặc Links.";
                lblStatus.ForeColor = Color.Red;
                btnExtract.Enabled = false;
                return;
            }

            if (radSelectedCodes.Checked)
            {
                int selectedCount = 0;
                if (chkExportShapes.Checked) selectedCount += chkListShapes.CheckedItems.Count;
                if (chkExportLinks.Checked) selectedCount += chkListLinks.CheckedItems.Count;

                if (selectedCount == 0)
                {
                    lblStatus.Text = "⚠ Bạn đang chọn lọc mã code nhưng chưa đánh dấu mã nào.";
                    lblStatus.ForeColor = Color.Red;
                    btnExtract.Enabled = false;
                    return;
                }
            }

            if (!radInsertCurrent.Checked)
            {
                string path = txtFilePath.Text.Trim();
                if (string.IsNullOrWhiteSpace(path))
                {
                    lblStatus.Text = "⚠ Vui lòng chỉ định đường dẫn file DWG (dùng nút 📂 Chọn file).";
                    lblStatus.ForeColor = Color.Red;
                    btnExtract.Enabled = false;
                    return;
                }

                if (radAddExisting.Checked && !File.Exists(path))
                {
                    lblStatus.Text = "⚠ File bản vẽ có sẵn được chỉ định không tồn tại.";
                    lblStatus.ForeColor = Color.Red;
                    btnExtract.Enabled = false;
                    return;
                }

                try
                {
                    string dir = Path.GetDirectoryName(path)!;
                    if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                    {
                        lblStatus.Text = "⚠ Thư mục lưu file không tồn tại.";
                        lblStatus.ForeColor = Color.Red;
                        btnExtract.Enabled = false;
                        return;
                    }
                }
                catch
                {
                    lblStatus.Text = "⚠ Đường dẫn file không hợp lệ.";
                    lblStatus.ForeColor = Color.Red;
                    btnExtract.Enabled = false;
                    return;
                }
            }

            lblStatus.Text = "✓ Sẵn sàng trích xuất 3D Solids từ Corridor.";
            lblStatus.ForeColor = Color.DarkGreen;
            btnExtract.Enabled = true;
        }

        private void BtnExtract_Click(object? sender, EventArgs e)
        {
            var item = cmbCorridor.SelectedItem as ExtractCorridorComboItem;
            if (item == null || item.Id.IsNull) return;

            SelectedCorridorId = item.Id;
            ObjectType = (OutputObjectType)cmbObjectType.SelectedIndex;

            if (radInsertCurrent.Checked) Destination = OutputDestination.InsertIntoCurrentDrawing;
            else if (radAddExisting.Checked) Destination = OutputDestination.AddToExistingDrawing;
            else Destination = OutputDestination.AddToNewDrawing;

            if (Destination == OutputDestination.AddToNewDrawing && File.Exists(OutputFilePath) && !chkOverwrite.Checked)
            {
                var confirm = MessageBox.Show(
                    $"File '{Path.GetFileName(OutputFilePath)}' đã tồn tại trong thư mục.\nBạn có muốn ghi đè lên file này không?",
                    "Xác nhận ghi đè file",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (confirm != DialogResult.Yes) return;
            }

            IncludedCodes.Clear();
            if (radSelectedCodes.Checked)
            {
                if (chkExportShapes.Checked)
                {
                    foreach (var sc in chkListShapes.CheckedItems)
                    {
                        if (sc != null) IncludedCodes.Add(sc.ToString()!);
                    }
                }
                if (chkExportLinks.Checked)
                {
                    foreach (var lc in chkListLinks.CheckedItems)
                    {
                        if (lc != null) IncludedCodes.Add(lc.ToString()!);
                    }
                }
            }

            SaveCurrentSettings();
            FormAccepted = true;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void ShowHelpDialog()
        {
            MessageBox.Show(
                "LỆNH TRÍCH XUẤT 3D SOLID TỪ CORRIDOR (CTC_Xuat_Solid_Tu_Corridor):\n\n" +
                "1. Codes to Extract:\n" +
                "   • Chọn Corridor nguồn cần trích xuất khối.\n" +
                "   • Chọn đường dẫn file DWG xuất hoặc thư mục lưu ngay tại bước này.\n" +
                "   • Tùy chọn trích xuất Shapes (khối áo đường, bó vỉa, taluy...) và Links (bề mặt liên kết).\n" +
                "   • Hỗ trợ lọc mã Code linh hoạt theo từng lớp kết cấu.\n\n" +
                "2. Property Data:\n" +
                "   • Tự động tạo Layer theo tên mã Code với tiền tố chỉ định.\n" +
                "   • Tô màu phân biệt trực quan cho từng khối kết cấu.\n" +
                "   • Đính kèm metadata BIM vào XData để liên kết sang Navisworks/Revit.\n\n" +
                "3. Output Options:\n" +
                "   • Chọn kiểu thực thể: 3D Solids (Sampling), 3D Solids (Swept) hoặc Bodies.\n" +
                "   • Chọn đích đến:\n" +
                "     + Insert into current drawing: Chèn trực tiếp vào bản vẽ hiện hành.\n" +
                "     + Add to an existing drawing: Dùng nút '📂 Chọn file...' để chọn file DWG có sẵn.\n" +
                "     + Add to a new drawing: Dùng nút '📂 Chọn file...' hoặc '📁 Thư mục...' để chỉ vị trí lưu.",
                "Trợ giúp — Xuất Solid Từ Corridor",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        // ═══════════════════════════════════════════════════════════════
        //  PERSISTENT STATE: SAVE & RESTORE
        // ═══════════════════════════════════════════════════════════════
        public void SaveCurrentSettings()
        {
            var item = cmbCorridor.SelectedItem as ExtractCorridorComboItem;
            if (item != null && !item.Id.IsNull)
            {
                _lastCorridorId = item.Id;
            }

            _lastObjectType = (OutputObjectType)cmbObjectType.SelectedIndex;

            if (radInsertCurrent.Checked) _lastDestination = OutputDestination.InsertIntoCurrentDrawing;
            else if (radAddExisting.Checked) _lastDestination = OutputDestination.AddToExistingDrawing;
            else _lastDestination = OutputDestination.AddToNewDrawing;

            _lastExportShapes = chkExportShapes.Checked;
            _lastExportLinks = chkExportLinks.Checked;
            _lastFilterCodes = radSelectedCodes.Checked;

            _lastSelectedShapeCodes.Clear();
            foreach (var sc in chkListShapes.CheckedItems)
            {
                if (sc != null) _lastSelectedShapeCodes.Add(sc.ToString()!);
            }

            _lastSelectedLinkCodes.Clear();
            foreach (var lc in chkListLinks.CheckedItems)
            {
                if (lc != null) _lastSelectedLinkCodes.Add(lc.ToString()!);
            }

            _lastAttachXData = chkAttachXData.Checked;
            _lastDynamicLink = chkDynamicLink.Checked;

            string filePath = txtFilePath.Text.Trim();
            if (!string.IsNullOrEmpty(filePath))
            {
                _lastFilePath = filePath;
                try
                {
                    string dir = Path.GetDirectoryName(filePath)!;
                    if (Directory.Exists(dir)) _lastExportFolder = dir;
                }
                catch { }
            }

            _lastOverwrite = chkOverwrite.Checked;
            _lastOpenAfterExport = chkOpenAfterExport.Checked;
            _lastOpenFolderAfterExport = chkOpenFolderAfterExport.Checked;
            _lastFormSize = this.Size;
        }

        public void RestoreLastSettings()
        {
            chkExportShapes.Checked = _lastExportShapes;
            chkExportLinks.Checked = _lastExportLinks;
            radSelectedCodes.Checked = _lastFilterCodes;
            radAllCodes.Checked = !_lastFilterCodes;

            chkAttachXData.Checked = _lastAttachXData;
            chkDynamicLink.Checked = _lastDynamicLink;

            cmbObjectType.SelectedIndex = (int)_lastObjectType;

            radInsertCurrent.Checked = (_lastDestination == OutputDestination.InsertIntoCurrentDrawing);
            radAddExisting.Checked = (_lastDestination == OutputDestination.AddToExistingDrawing);
            radAddNew.Checked = (_lastDestination == OutputDestination.AddToNewDrawing);

            chkOverwrite.Checked = _lastOverwrite;
            chkOpenAfterExport.Checked = _lastOpenAfterExport;
            chkOpenFolderAfterExport.Checked = _lastOpenFolderAfterExport;

            UpdateDestinationState();
            UpdateCodeFilterState();

            if (!_lastCorridorId.IsNull && _lastCorridorId.IsValid && !_lastCorridorId.IsErased)
            {
                SelectCorridorById(_lastCorridorId);
            }
            else if (cmbCorridor.Items.Count > 0)
            {
                cmbCorridor.SelectedIndex = 0;
            }

            // Luôn mở tab đầu tiên: Codes to Extract khi khởi chạy Form
            SwitchStep(0);
        }
    }

    internal class ExtractCorridorComboItem
    {
        public string Name { get; }
        public ObjectId Id { get; }

        public ExtractCorridorComboItem(string name, ObjectId id)
        {
            Name = name;
            Id = id;
        }

        public override string ToString() => Name;
    }
}

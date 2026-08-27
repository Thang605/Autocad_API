// (C) Copyright 2026 by T27
//
using System;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.ApplicationServices;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using DrawingFont = System.Drawing.Font;

namespace Civil3DCsharp
{
    /// <summary>
    /// Form giao diện cho lệnh Chuẩn Hóa Bản Vẽ (AT_ChuanHoaBanVe / CHBV)
    /// Hỗ trợ nạp bộ chuẩn T27 tích hợp sẵn hoặc đồng bộ từ file mẫu DWG/DWT
    /// </summary>
    public class ChuanHoaBanVeForm : Form
    {
        #region Persistent State (Ghi nhớ thông số giữa các lần chạy)
        private static string _lastTemplatePath = "";
        private static int _lastSelectedTab = 0;
        private static bool _lastApplyTextStyles = true;
        private static bool _lastApplyDimStyles = true;
        private static bool _lastApplyMLeaderStyles = true;
        private static bool _lastApplyLinetypes = true;
        private static bool _lastSetCurrent = true;
        private static bool _lastPurgeUnused = false;
        private static bool _lastOverwriteExisting = true;
        private static Size _lastFormSize = new Size(620, 680);
        #endregion

        #region UI Controls
        private Label lblTitle = null!;
        private TabControl tabMain = null!;
        private TabPage tabT27Standard = null!;
        private TabPage tabTemplateSync = null!;
        private TabPage tabOptions = null!;

        // Tab 1: Bộ chuẩn T27
        private GroupBox grpT27Layers = null!;
        private CheckedListBox chkListLayers = null!;
        private Button btnSelectAllLayers = null!;
        private Button btnDeselectAllLayers = null!;

        private GroupBox grpT27Styles = null!;
        private CheckBox chkTextStyles = null!;
        private CheckBox chkDimStyles = null!;
        private CheckBox chkMLeaderStyles = null!;
        private CheckBox chkLinetypes = null!;

        // Tab 2: Import từ Template
        private Label lblTemplateFile = null!;
        private TextBox txtTemplatePath = null!;
        private Button btnBrowseTemplate = null!;
        private GroupBox grpImportItems = null!;
        private CheckBox chkImportLayers = null!;
        private CheckBox chkImportTextStyles = null!;
        private CheckBox chkImportDimStyles = null!;
        private CheckBox chkImportMLeaderStyles = null!;
        private CheckBox chkImportBlocks = null!;
        private RadioButton radOverwrite = null!;
        private RadioButton radSkipExisting = null!;

        // Tab 3: Tùy chọn nâng cao
        private GroupBox grpGeneralOptions = null!;
        private CheckBox chkSetCurrentStyles = null!;
        private CheckBox chkPurge = null!;
        private CheckBox chkApplyByLayer = null!;

        // Log Box
        private GroupBox grpLog = null!;
        private TextBox txtLog = null!;

        // Buttons
        private Button btnExecute = null!;
        private Button btnClose = null!;
        #endregion

        public ChuanHoaBanVeForm()
        {
            InitializeComponent();
            RestoreLastSettings();
        }

        private void InitializeComponent()
        {
            var standardFont = new DrawingFont("Segoe UI", 9.5F, FontStyle.Regular);
            var boldFont = new DrawingFont("Segoe UI", 9.5F, FontStyle.Bold);
            var titleFont = new DrawingFont("Segoe UI", 13.5F, FontStyle.Bold);

            this.SuspendLayout();

            // Form settings
            this.Text = "Chuẩn Hóa Bản Vẽ - Tiêu Chuẩn T27";
            this.ClientSize = _lastFormSize;
            this.MinimumSize = new Size(580, 620);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = standardFont;
            this.Icon = SystemIcons.Application;

            // Title
            lblTitle = new Label
            {
                Text = "CHUẨN HÓA ĐỊNH DẠNG BẢN VẼ (T27 STANDARD)",
                Font = titleFont,
                Location = new Point(15, 12),
                Size = new Size(590, 28),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(0, 102, 204),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            // Main TabControl
            tabMain = new TabControl
            {
                Location = new Point(15, 45),
                Size = new Size(590, 380),
                Font = boldFont,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            tabT27Standard = new TabPage("1. Bộ Chuẩn T27 (Mặc Định)");
            tabTemplateSync = new TabPage("2. Đồng Bộ Từ File Mẫu (.dwg/.dwt)");
            tabOptions = new TabPage("3. Tùy Chọn & Tinh Chỉnh");

            tabT27Standard.Font = standardFont;
            tabTemplateSync.Font = standardFont;
            tabOptions.Font = standardFont;

            #region Setup Tab 1: Bộ Chuẩn T27
            // GroupBox Layers
            grpT27Layers = new GroupBox
            {
                Text = "Danh sách Layer chuẩn T27",
                Font = boldFont,
                Location = new Point(10, 10),
                Size = new Size(320, 325),
                ForeColor = Color.DarkSlateGray
            };

            chkListLayers = new CheckedListBox
            {
                Location = new Point(10, 25),
                Size = new Size(300, 255),
                Font = standardFont,
                CheckOnClick = true,
                IntegralHeight = false
            };

            // Thêm các Layer chuẩn T27
            string[] defaultLayers = new string[]
            {
                "T27_NET_THAY (Màu 7 - Trắng, 0.35mm)",
                "T27_NET_KHUAT (Màu 8 - Xám, HIDDEN, 0.15mm)",
                "T27_NET_TRUC (Màu 1 - Đỏ, CENTER, 0.15mm)",
                "T27_NET_MANH (Màu 9 - Xám nhạt, 0.13mm)",
                "T27_DIM (Màu 3 - Xanh lá, 0.15mm)",
                "T27_TEXT (Màu 2 - Vàng, 0.20mm)",
                "T27_TEXT_TIEUDE (Màu 4 - Cyan, 0.35mm)",
                "T27_HATCH (Màu 8 - Xám, 0.09mm)",
                "T27_KHUNG_TEN (Màu 4 - Cyan, 0.50mm)",
                "T27_TIM_DUONG (Màu 1 - Đỏ, CENTER2, 0.35mm)",
                "T27_MEP_DUONG (Màu 7 - Trắng, 0.35mm)",
                "T27_VIA_HE (Màu 3 - Xanh lá, 0.25mm)",
                "T27_TALUY (Màu 8 - Xám, 0.15mm)",
                "T27_THOAT_NUOC (Màu 5 - Xanh dương, 0.30mm)",
                "Defpoints (Không in)"
            };

            foreach (var lay in defaultLayers)
            {
                chkListLayers.Items.Add(lay, true);
            }

            btnSelectAllLayers = new Button
            {
                Text = "Chọn tất cả",
                Location = new Point(10, 288),
                Size = new Size(145, 26),
                Font = standardFont
            };
            btnSelectAllLayers.Click += (s, e) => {
                for (int i = 0; i < chkListLayers.Items.Count; i++) chkListLayers.SetItemChecked(i, true);
            };

            btnDeselectAllLayers = new Button
            {
                Text = "Bỏ chọn hết",
                Location = new Point(165, 288),
                Size = new Size(145, 26),
                Font = standardFont
            };
            btnDeselectAllLayers.Click += (s, e) => {
                for (int i = 0; i < chkListLayers.Items.Count; i++) chkListLayers.SetItemChecked(i, false);
            };

            grpT27Layers.Controls.AddRange(new Control[] { chkListLayers, btnSelectAllLayers, btnDeselectAllLayers });

            // GroupBox Styles & Linetypes
            grpT27Styles = new GroupBox
            {
                Text = "Kiểu chữ, Dim, Leader & Nét",
                Font = boldFont,
                Location = new Point(340, 10),
                Size = new Size(230, 325),
                ForeColor = Color.DarkSlateGray
            };

            chkTextStyles = new CheckBox
            {
                Text = "Text Styles chuẩn T27\n(T27_RomanS, T27_Arial, T27_Times...)",
                Location = new Point(15, 25),
                Size = new Size(205, 55),
                Font = standardFont,
                Checked = true
            };

            chkDimStyles = new CheckBox
            {
                Text = "Dimension Styles T27\n(T27_1-100, 1-50, 1-200, 1-500, Annotative...)",
                Location = new Point(15, 85),
                Size = new Size(205, 60),
                Font = standardFont,
                Checked = true
            };

            chkMLeaderStyles = new CheckBox
            {
                Text = "MLeader Styles T27\n(T27_MLeader chuẩn)",
                Location = new Point(15, 150),
                Size = new Size(205, 45),
                Font = standardFont,
                Checked = true
            };

            chkLinetypes = new CheckBox
            {
                Text = "Nạp Đường Nét chuẩn\n(CENTER, HIDDEN, DASHED...)",
                Location = new Point(15, 205),
                Size = new Size(205, 45),
                Font = standardFont,
                Checked = true
            };

            grpT27Styles.Controls.AddRange(new Control[] { chkTextStyles, chkDimStyles, chkMLeaderStyles, chkLinetypes });

            tabT27Standard.Controls.AddRange(new Control[] { grpT27Layers, grpT27Styles });
            #endregion

            #region Setup Tab 2: Import từ File Mẫu
            lblTemplateFile = new Label
            {
                Text = "Đường dẫn file AutoCAD mẫu (.dwg, .dwt):",
                Font = boldFont,
                Location = new Point(15, 15),
                Size = new Size(350, 20)
            };

            txtTemplatePath = new TextBox
            {
                Location = new Point(15, 38),
                Size = new Size(440, 24),
                Font = standardFont
            };

            btnBrowseTemplate = new Button
            {
                Text = "Duyệt file...",
                Location = new Point(465, 36),
                Size = new Size(105, 28),
                Font = standardFont
            };
            btnBrowseTemplate.Click += BtnBrowseTemplate_Click;

            grpImportItems = new GroupBox
            {
                Text = "Đối tượng nạp từ file mẫu",
                Font = boldFont,
                Location = new Point(15, 75),
                Size = new Size(555, 180),
                ForeColor = Color.DarkSlateGray
            };

            chkImportLayers = new CheckBox { Text = "Layers (Lớp bản vẽ)", Location = new Point(20, 30), Size = new Size(220, 24), Font = standardFont, Checked = true };
            chkImportTextStyles = new CheckBox { Text = "Text Styles (Kiểu chữ)", Location = new Point(20, 65), Size = new Size(220, 24), Font = standardFont, Checked = true };
            chkImportDimStyles = new CheckBox { Text = "Dimension Styles (Kích thước)", Location = new Point(20, 100), Size = new Size(220, 24), Font = standardFont, Checked = true };
            chkImportMLeaderStyles = new CheckBox { Text = "Multileader Styles (Ghi chú)", Location = new Point(280, 30), Size = new Size(240, 24), Font = standardFont, Checked = true };
            chkImportBlocks = new CheckBox { Text = "Block Definitions (Khối khung tên, ký hiệu)", Location = new Point(280, 65), Size = new Size(260, 24), Font = standardFont, Checked = true };

            radOverwrite = new RadioButton { Text = "Ghi đè/Cập nhật Style đã có", Location = new Point(20, 140), Size = new Size(220, 24), Font = boldFont, Checked = true };
            radSkipExisting = new RadioButton { Text = "Bỏ qua nếu đã tồn tại", Location = new Point(280, 140), Size = new Size(220, 24), Font = standardFont };

            grpImportItems.Controls.AddRange(new Control[] {
                chkImportLayers, chkImportTextStyles, chkImportDimStyles,
                chkImportMLeaderStyles, chkImportBlocks, radOverwrite, radSkipExisting
            });

            tabTemplateSync.Controls.AddRange(new Control[] {
                lblTemplateFile, txtTemplatePath, btnBrowseTemplate, grpImportItems
            });
            #endregion

            #region Setup Tab 3: Tùy Chọn
            grpGeneralOptions = new GroupBox
            {
                Text = "Tùy chọn thiết lập sau khi chuẩn hóa",
                Font = boldFont,
                Location = new Point(15, 15),
                Size = new Size(555, 180),
                ForeColor = Color.DarkSlateGray
            };

            chkSetCurrentStyles = new CheckBox
            {
                Text = "Đặt Layer (T27_NET_THAY), TextStyle (T27_RomanS) & DimStyle (T27_1-100) làm HIỆN HÀNH",
                Location = new Point(20, 35),
                Size = new Size(520, 30),
                Font = standardFont,
                Checked = true
            };

            chkPurge = new CheckBox
            {
                Text = "Tự động dọn dẹp các đối tượng rác không sử dụng (Purge Unused)",
                Location = new Point(20, 75),
                Size = new Size(520, 30),
                Font = standardFont,
                Checked = false
            };

            chkApplyByLayer = new CheckBox
            {
                Text = "Thiết lập biến hệ thống CAD chuẩn (LTSCALE = 1, CELTSCALE = 1, DIMASSOC = 2)",
                Location = new Point(20, 115),
                Size = new Size(520, 30),
                Font = standardFont,
                Checked = true
            };

            grpGeneralOptions.Controls.AddRange(new Control[] { chkSetCurrentStyles, chkPurge, chkApplyByLayer });
            tabOptions.Controls.Add(grpGeneralOptions);
            #endregion

            tabMain.TabPages.AddRange(new TabPage[] { tabT27Standard, tabTemplateSync, tabOptions });

            // Log GroupBox
            grpLog = new GroupBox
            {
                Text = "Nhật ký thực hiện",
                Font = boldFont,
                Location = new Point(15, 435),
                Size = new Size(590, 140),
                ForeColor = Color.DarkSlateGray,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            txtLog = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Location = new Point(15, 22),
                Size = new Size(560, 105),
                Font = new DrawingFont("Consolas", 9F, FontStyle.Regular),
                BackColor = Color.White,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            grpLog.Controls.Add(txtLog);

            // Action Buttons
            btnExecute = new Button
            {
                Text = "🚀 CHUẨN HÓA BẢN VẼ",
                Location = new Point(290, 590),
                Size = new Size(185, 38),
                Font = boldFont,
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            btnExecute.Click += BtnExecute_Click;

            btnClose = new Button
            {
                Text = "Đóng",
                Location = new Point(485, 590),
                Size = new Size(120, 38),
                Font = standardFont,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right
            };
            btnClose.Click += (s, e) => this.Close();

            this.Controls.AddRange(new Control[] {
                lblTitle, tabMain, grpLog, btnExecute, btnClose
            });

            this.FormClosing += (s, e) => SaveCurrentSettings();
            this.ResumeLayout(false);
        }

        private void BtnBrowseTemplate_Click(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Chọn file bản vẽ mẫu AutoCAD (.dwg, .dwt)",
                Filter = "AutoCAD Drawing / Template (*.dwg;*.dwt)|*.dwg;*.dwt|All Files (*.*)|*.*",
                CheckFileExists = true
            };

            if (!string.IsNullOrEmpty(txtTemplatePath.Text) && Directory.Exists(Path.GetDirectoryName(txtTemplatePath.Text)))
            {
                ofd.InitialDirectory = Path.GetDirectoryName(txtTemplatePath.Text);
            }

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                txtTemplatePath.Text = ofd.FileName;
            }
        }

        private void BtnExecute_Click(object? sender, EventArgs e)
        {
            SaveCurrentSettings();
            txtLog.Clear();
            AppendLog("Bắt đầu chuẩn hóa bản vẽ...");

            btnExecute.Enabled = false;
            try
            {
                if (tabMain.SelectedIndex == 0) // Chế độ 1: Chuẩn T27 Built-in
                {
                    var selectedLayers = new List<string>();
                    for (int i = 0; i < chkListLayers.Items.Count; i++)
                    {
                        if (chkListLayers.GetItemChecked(i))
                        {
                            string itemText = chkListLayers.Items[i].ToString() ?? "";
                            string layerName = itemText.Split(' ')[0];
                            selectedLayers.Add(layerName);
                        }
                    }

                    ChuanHoaBanVeCmd.ExecuteT27Standards(
                        selectedLayers,
                        chkTextStyles.Checked,
                        chkDimStyles.Checked,
                        chkMLeaderStyles.Checked,
                        chkLinetypes.Checked,
                        chkSetCurrentStyles.Checked,
                        chkPurge.Checked,
                        chkApplyByLayer.Checked,
                        AppendLog
                    );
                }
                else if (tabMain.SelectedIndex == 1) // Chế độ 2: Import từ Template
                {
                    string templatePath = txtTemplatePath.Text.Trim();
                    if (string.IsNullOrEmpty(templatePath) || !File.Exists(templatePath))
                    {
                        MessageBox.Show("Vui lòng chọn file mẫu .dwg hoặc .dwt hợp lệ!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    ChuanHoaBanVeCmd.ExecuteImportFromTemplate(
                        templatePath,
                        chkImportLayers.Checked,
                        chkImportTextStyles.Checked,
                        chkImportDimStyles.Checked,
                        chkImportMLeaderStyles.Checked,
                        chkImportBlocks.Checked,
                        radOverwrite.Checked,
                        chkSetCurrentStyles.Checked,
                        chkPurge.Checked,
                        chkApplyByLayer.Checked,
                        AppendLog
                    );
                }
                else // Tab 3
                {
                    ChuanHoaBanVeCmd.ExecuteSystemSettings(chkSetCurrentStyles.Checked, chkPurge.Checked, chkApplyByLayer.Checked, AppendLog);
                }

                AppendLog("✅ HOÀN TẤT CHUẨN HÓA BẢN VẼ THÀNH CÔNG!");
            }
            catch (System.Exception ex)
            {
                AppendLog("❌ Lỗi: " + ex.Message);
                MessageBox.Show("Lỗi trong quá trình chuẩn hóa: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnExecute.Enabled = true;
            }
        }

        private void AppendLog(string message)
        {
            if (txtLog.InvokeRequired)
            {
                txtLog.Invoke(new Action<string>(AppendLog), message);
                return;
            }
            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\r\n");
        }

        private void SaveCurrentSettings()
        {
            _lastSelectedTab = tabMain.SelectedIndex;
            _lastTemplatePath = txtTemplatePath.Text.Trim();
            _lastApplyTextStyles = chkTextStyles.Checked;
            _lastApplyDimStyles = chkDimStyles.Checked;
            _lastApplyMLeaderStyles = chkMLeaderStyles.Checked;
            _lastApplyLinetypes = chkLinetypes.Checked;
            _lastSetCurrent = chkSetCurrentStyles.Checked;
            _lastPurgeUnused = chkPurge.Checked;
            _lastOverwriteExisting = radOverwrite.Checked;
            _lastFormSize = this.Size;
        }

        private void RestoreLastSettings()
        {
            if (_lastSelectedTab >= 0 && _lastSelectedTab < tabMain.TabCount)
                tabMain.SelectedIndex = _lastSelectedTab;

            txtTemplatePath.Text = _lastTemplatePath;
            chkTextStyles.Checked = _lastApplyTextStyles;
            chkDimStyles.Checked = _lastApplyDimStyles;
            chkMLeaderStyles.Checked = _lastApplyMLeaderStyles;
            chkLinetypes.Checked = _lastApplyLinetypes;
            chkSetCurrentStyles.Checked = _lastSetCurrent;
            chkPurge.Checked = _lastPurgeUnused;
            radOverwrite.Checked = _lastOverwriteExisting;
            radSkipExisting.Checked = !_lastOverwriteExisting;
        }
    }
}

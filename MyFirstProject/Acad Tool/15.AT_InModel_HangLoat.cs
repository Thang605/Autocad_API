// (C) Copyright 2024 by T27
// Lệnh in hàng loạt Model space - Tham khảo từ LISP INMODEL_TNC3D
//
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Linq;
using System.IO;

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.ApplicationServices;

// Alias để tránh xung đột namespace
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsTextBox = System.Windows.Forms.TextBox;
using WinFormsButton = System.Windows.Forms.Button;
using WinFormsComboBox = System.Windows.Forms.ComboBox;
using DrawingFont = System.Drawing.Font;

// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3DCsharp.AT_InModel_HangLoat_Commands))]

namespace Civil3DCsharp
{
    /// <summary>
    /// Class lưu thông tin điểm in
    /// </summary>
    public class PrintPointInfo
    {
        public int Index { get; set; }
        public Point3d StartPoint { get; set; }
        public int Quantity { get; set; }

        public PrintPointInfo(int index, Point3d startPoint, int quantity)
        {
            Index = index;
            StartPoint = startPoint;
            Quantity = quantity;
        }
    }

    /// <summary>
    /// Form cấu hình in hàng loạt
    /// </summary>
    public class BatchPlotSettingsForm : Form
    {
        // Properties trả về kết quả
        public List<PrintPointInfo> PrintPoints { get; private set; } = new();
        public string SelectedPrinter { get; private set; } = "PDF reDirect v2";
        public string SelectedPaperSize { get; private set; } = "A3";
        public double FrameWidth { get; private set; } = 84.0;
        public double FrameHeight { get; private set; } = 59.4;
        public double Spacing { get; private set; } = 421.0;
        public string SelectedScale { get; private set; } = "5:1";
        public string SelectedCtb { get; private set; } = "monochrome.ctb";
        public bool IsLandscape { get; private set; } = true;

        // Controls
        private DataGridView dgvPrintPoints = null!;
        private WinFormsComboBox cmbPrinter = null!;
        private WinFormsComboBox cmbPaperSize = null!;
        private WinFormsTextBox txtFrameWidth = null!;
        private WinFormsTextBox txtFrameHeight = null!;
        private WinFormsTextBox txtSpacing = null!;
        private WinFormsComboBox cmbScale = null!;
        private WinFormsComboBox cmbCtb = null!;
        private RadioButton rbLandscape = null!;
        private RadioButton rbPortrait = null!;
        private WinFormsButton btnAddPoint = null!;
        private WinFormsButton btnRemovePoint = null!;
        private WinFormsButton btnImport = null!;
        private WinFormsButton btnExport = null!;
        private WinFormsButton btnOK = null!;
        private WinFormsButton btnCancel = null!;
        private WinFormsLabel lblTotalPrints = null!;

        // Editor reference for point picking
        private Editor _editor;

        // Static để lưu giá trị giữa các phiên
        private static string _lastPrinter = "PDF reDirect v2";  // Mặc định như LISP
        private static string _lastPaperSize = "A3";             // Mặc định như LISP
        private static double _lastFrameWidth = 84.0;
        private static double _lastFrameHeight = 59.4;
        private static double _lastSpacing = 421.0;
        private static string _lastScale = "5:1";
        private static string _lastCtb = "monochrome.ctb";
        private static bool _lastIsLandscape = true;

        // Paper sizes cho từng loại máy in
        private static readonly Dictionary<string, string[]> PrinterPaperSizes = new()
        {
            { "PDF reDirect v2", new[] { "A0", "A1", "A2", "A3", "A4", "A5", "Letter", "Legal", "Tabloid" } },
            { "Microsoft Print to PDF", new[] { "A3", "A4", "Letter", "Legal", "Tabloid" } },
            { "AutoCAD PDF (General Documentation).pc3", new[] { 
                "ISO full bleed A0 (1189.00 x 841.00 MM)",
                "ISO full bleed A1 (841.00 x 594.00 MM)", 
                "ISO full bleed A2 (594.00 x 420.00 MM)",
                "ISO full bleed A3 (420.00 x 297.00 MM)",
                "ISO full bleed A4 (297.00 x 210.00 MM)"
            }}
        };

        public BatchPlotSettingsForm(Editor editor)
        {
            _editor = editor;
            InitializeComponent();
            LoadDefaultValues();
        }

        private void InitializeComponent()
        {
            this.Text = "🖨️ In Hàng Loạt Model - AT_InModel_HangLoat";
            this.Size = new Size(750, 620);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.White;

            int y = 15;

            // ========== Group: Danh sách điểm in ==========
            var grpPoints = new GroupBox
            {
                Text = "📍 Danh sách điểm in",
                Location = new Point(15, y),
                Size = new Size(705, 200),
                ForeColor = Color.FromArgb(0, 120, 215)
            };

            dgvPrintPoints = new DataGridView
            {
                Location = new Point(10, 25),
                Size = new Size(480, 160),
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            dgvPrintPoints.Columns.Add("STT", "STT");
            dgvPrintPoints.Columns.Add("X", "X");
            dgvPrintPoints.Columns.Add("Y", "Y");
            dgvPrintPoints.Columns.Add("Quantity", "Số lượng");
            dgvPrintPoints.Columns["STT"].Width = 50;
            dgvPrintPoints.Columns["STT"].ReadOnly = true;
            dgvPrintPoints.Columns["X"].Width = 130;
            dgvPrintPoints.Columns["X"].ReadOnly = true;
            dgvPrintPoints.Columns["Y"].Width = 130;
            dgvPrintPoints.Columns["Y"].ReadOnly = true;
            dgvPrintPoints.Columns["Quantity"].Width = 80;
            dgvPrintPoints.CellEndEdit += DgvPrintPoints_CellEndEdit;

            int btnX = 505;
            int btnY = 25;
            int btnHeight = 32;
            int btnSpacing = 38;

            btnAddPoint = new WinFormsButton
            {
                Text = "➕ Thêm điểm",
                Location = new Point(btnX, btnY),
                Size = new Size(95, btnHeight),
                BackColor = Color.FromArgb(40, 167, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnAddPoint.FlatAppearance.BorderSize = 0;
            btnAddPoint.Click += BtnAddPoint_Click;

            btnRemovePoint = new WinFormsButton
            {
                Text = "➖ Xóa điểm",
                Location = new Point(btnX, btnY + btnSpacing),
                Size = new Size(95, btnHeight),
                BackColor = Color.FromArgb(220, 53, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnRemovePoint.FlatAppearance.BorderSize = 0;
            btnRemovePoint.Click += BtnRemovePoint_Click;

            btnImport = new WinFormsButton
            {
                Text = "📥 Nhập CSV",
                Location = new Point(btnX, btnY + btnSpacing * 2),
                Size = new Size(95, btnHeight),
                BackColor = Color.FromArgb(23, 162, 184),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnImport.FlatAppearance.BorderSize = 0;
            btnImport.Click += BtnImport_Click;

            btnExport = new WinFormsButton
            {
                Text = "📤 Xuất CSV",
                Location = new Point(btnX, btnY + btnSpacing * 3),
                Size = new Size(95, btnHeight),
                BackColor = Color.FromArgb(255, 193, 7),
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnExport.FlatAppearance.BorderSize = 0;
            btnExport.Click += BtnExport_Click;

            lblTotalPrints = new WinFormsLabel
            {
                Text = "Tổng: 0 bản",
                Location = new Point(btnX + 100, btnY),
                Size = new Size(90, 60),
                Font = new DrawingFont("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 120, 215),
                TextAlign = ContentAlignment.TopLeft
            };

            grpPoints.Controls.Add(dgvPrintPoints);
            grpPoints.Controls.Add(btnAddPoint);
            grpPoints.Controls.Add(btnRemovePoint);
            grpPoints.Controls.Add(btnImport);
            grpPoints.Controls.Add(btnExport);
            grpPoints.Controls.Add(lblTotalPrints);

            y += 210;

            // ========== Group: Cài đặt in ==========
            var grpSettings = new GroupBox
            {
                Text = "⚙️ Cài đặt in",
                Location = new Point(15, y),
                Size = new Size(350, 300),
                ForeColor = Color.FromArgb(0, 120, 215)
            };

            int settingY = 30;
            int labelX = 15;
            int controlX = 130;
            int controlWidth = 200;

            // Máy in
            var lblPrinter = new WinFormsLabel
            {
                Text = "Máy in:",
                Location = new Point(labelX, settingY + 3),
                Size = new Size(100, 23),
                ForeColor = Color.Black
            };
            cmbPrinter = new WinFormsComboBox
            {
                Location = new Point(controlX, settingY),
                Size = new Size(controlWidth, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbPrinter.Items.AddRange(new object[] {
                "PDF reDirect v2",  // Mặc định đầu tiên như LISP
                "Microsoft Print to PDF",
                "AutoCAD PDF (General Documentation).pc3"
            });
            cmbPrinter.SelectedIndexChanged += CmbPrinter_SelectedIndexChanged;
            grpSettings.Controls.Add(lblPrinter);
            grpSettings.Controls.Add(cmbPrinter);

            settingY += 40;

            // Kích thước giấy
            var lblPaperSize = new WinFormsLabel
            {
                Text = "Kích thước giấy:",
                Location = new Point(labelX, settingY + 3),
                Size = new Size(110, 23),
                ForeColor = Color.Black
            };
            cmbPaperSize = new WinFormsComboBox
            {
                Location = new Point(controlX, settingY),
                Size = new Size(controlWidth, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            grpSettings.Controls.Add(lblPaperSize);
            grpSettings.Controls.Add(cmbPaperSize);

            settingY += 40;

            // Tỷ lệ
            var lblScale = new WinFormsLabel
            {
                Text = "Tỷ lệ in:",
                Location = new Point(labelX, settingY + 3),
                Size = new Size(100, 23),
                ForeColor = Color.Black
            };
            cmbScale = new WinFormsComboBox
            {
                Location = new Point(controlX, settingY),
                Size = new Size(controlWidth, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbScale.Items.AddRange(new object[] {
                "1:1", "2:1", "5:1", "10:1", "20:1", "50:1", "100:1",
                "1:2", "1:5", "1:10", "1:20", "1:50", "1:100", "1:200", "1:500", "1:1000"
            });
            grpSettings.Controls.Add(lblScale);
            grpSettings.Controls.Add(cmbScale);

            settingY += 40;

            // CTB file
            var lblCtb = new WinFormsLabel
            {
                Text = "CTB file:",
                Location = new Point(labelX, settingY + 3),
                Size = new Size(100, 23),
                ForeColor = Color.Black
            };
            cmbCtb = new WinFormsComboBox
            {
                Location = new Point(controlX, settingY),
                Size = new Size(controlWidth, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbCtb.Items.AddRange(new object[] {
                "monochrome.ctb",
                "acad.ctb",
                "grayscale.ctb",
                "Screening 100%.ctb",
                "Screening 75%.ctb",
                "Screening 50%.ctb",
                "Screening 25%.ctb"
            });
            grpSettings.Controls.Add(lblCtb);
            grpSettings.Controls.Add(cmbCtb);

            settingY += 40;

            // Hướng giấy
            var lblOrientation = new WinFormsLabel
            {
                Text = "Hướng giấy:",
                Location = new Point(labelX, settingY + 3),
                Size = new Size(100, 23),
                ForeColor = Color.Black
            };
            rbLandscape = new RadioButton
            {
                Text = "Ngang",
                Location = new Point(controlX, settingY),
                Size = new Size(70, 23),
                Checked = true
            };
            rbPortrait = new RadioButton
            {
                Text = "Dọc",
                Location = new Point(controlX + 80, settingY),
                Size = new Size(60, 23)
            };
            grpSettings.Controls.Add(lblOrientation);
            grpSettings.Controls.Add(rbLandscape);
            grpSettings.Controls.Add(rbPortrait);

            // ========== Group: Kích thước khung ==========
            var grpFrame = new GroupBox
            {
                Text = "📐 Kích thước khung in",
                Location = new Point(380, y),
                Size = new Size(340, 300),
                ForeColor = Color.FromArgb(0, 120, 215)
            };

            int frameY = 30;
            int frameLabelX = 15;
            int frameControlX = 150;
            int frameControlWidth = 120;

            // Chiều rộng khung
            var lblFrameWidth = new WinFormsLabel
            {
                Text = "Chiều rộng (mm):",
                Location = new Point(frameLabelX, frameY + 3),
                Size = new Size(130, 23),
                ForeColor = Color.Black
            };
            txtFrameWidth = new WinFormsTextBox
            {
                Location = new Point(frameControlX, frameY),
                Size = new Size(frameControlWidth, 25),
                Text = "84"
            };
            grpFrame.Controls.Add(lblFrameWidth);
            grpFrame.Controls.Add(txtFrameWidth);

            frameY += 40;

            // Chiều cao khung
            var lblFrameHeight = new WinFormsLabel
            {
                Text = "Chiều cao (mm):",
                Location = new Point(frameLabelX, frameY + 3),
                Size = new Size(130, 23),
                ForeColor = Color.Black
            };
            txtFrameHeight = new WinFormsTextBox
            {
                Location = new Point(frameControlX, frameY),
                Size = new Size(frameControlWidth, 25),
                Text = "59.4"
            };
            grpFrame.Controls.Add(lblFrameHeight);
            grpFrame.Controls.Add(txtFrameHeight);

            frameY += 40;

            // Khoảng cách
            var lblSpacing = new WinFormsLabel
            {
                Text = "Khoảng cách (mm):",
                Location = new Point(frameLabelX, frameY + 3),
                Size = new Size(130, 23),
                ForeColor = Color.Black
            };
            txtSpacing = new WinFormsTextBox
            {
                Location = new Point(frameControlX, frameY),
                Size = new Size(frameControlWidth, 25),
                Text = "421"
            };
            grpFrame.Controls.Add(lblSpacing);
            grpFrame.Controls.Add(txtSpacing);

            frameY += 50;

            // Ghi chú
            var lblNote = new WinFormsLabel
            {
                Text = "💡 Ghi chú:\n" +
                       "- Điểm chọn là góc trái dưới\n" +
                       "- Các bản vẽ in theo hướng X+\n" +
                       "- Khoảng cách tính từ điểm đầu\n" +
                       "  của bản vẽ này đến bản kế tiếp",
                Location = new Point(frameLabelX, frameY),
                Size = new Size(300, 100),
                ForeColor = Color.Gray,
                Font = new DrawingFont("Segoe UI", 8.5f)
            };
            grpFrame.Controls.Add(lblNote);

            y += 310;

            // ========== Buttons OK/Cancel ==========
            btnOK = new WinFormsButton
            {
                Text = "✅ In",
                Location = new Point(280, y),
                Size = new Size(100, 40),
                BackColor = Color.FromArgb(0, 123, 255),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new DrawingFont("Segoe UI", 10, FontStyle.Bold)
            };
            btnOK.FlatAppearance.BorderSize = 0;
            btnOK.Click += BtnOK_Click;

            btnCancel = new WinFormsButton
            {
                Text = "❌ Hủy",
                Location = new Point(400, y),
                Size = new Size(100, 40),
                BackColor = Color.FromArgb(108, 117, 125),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new DrawingFont("Segoe UI", 10, FontStyle.Bold)
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };

            // Add controls
            this.Controls.Add(grpPoints);
            this.Controls.Add(grpSettings);
            this.Controls.Add(grpFrame);
            this.Controls.Add(btnOK);
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;
        }

        private void CmbPrinter_SelectedIndexChanged(object? sender, EventArgs e)
        {
            UpdatePaperSizeList();
        }

        private void UpdatePaperSizeList()
        {
            string selectedPrinter = cmbPrinter.SelectedItem?.ToString() ?? "PDF reDirect v2";
            cmbPaperSize.Items.Clear();

            if (PrinterPaperSizes.TryGetValue(selectedPrinter, out string[]? paperSizes))
            {
                cmbPaperSize.Items.AddRange(paperSizes);
            }
            else
            {
                // Default paper sizes nếu không tìm thấy máy in
                cmbPaperSize.Items.AddRange(new object[] { "A0", "A1", "A2", "A3", "A4" });
            }

            // Chọn A3 nếu có, hoặc item đầu tiên
            int a3Index = -1;
            for (int i = 0; i < cmbPaperSize.Items.Count; i++)
            {
                if (cmbPaperSize.Items[i]?.ToString()?.Contains("A3") == true)
                {
                    a3Index = i;
                    break;
                }
            }
            
            if (a3Index >= 0)
                cmbPaperSize.SelectedIndex = a3Index;
            else if (cmbPaperSize.Items.Count > 0)
                cmbPaperSize.SelectedIndex = 0;
        }

        private void LoadDefaultValues()
        {
            // Kiểm tra và reset về mặc định nếu cần
            // Đảm bảo mặc định là "PDF reDirect v2" như LISP
            bool printerFound = false;
            for (int i = 0; i < cmbPrinter.Items.Count; i++)
            {
                if (cmbPrinter.Items[i]?.ToString() == _lastPrinter)
                {
                    cmbPrinter.SelectedIndex = i;
                    printerFound = true;
                    break;
                }
            }
            
            // Nếu không tìm thấy, chọn PDF reDirect v2 (item đầu tiên)
            if (!printerFound && cmbPrinter.Items.Count > 0)
            {
                cmbPrinter.SelectedIndex = 0; // PDF reDirect v2 là item đầu tiên
                _lastPrinter = cmbPrinter.Items[0]?.ToString() ?? "PDF reDirect v2";
            }
            
            UpdatePaperSizeList();  // Cập nhật paper size list
            SelectComboItemExact(cmbPaperSize, _lastPaperSize);
            
            txtFrameWidth.Text = _lastFrameWidth.ToString("F1");
            txtFrameHeight.Text = _lastFrameHeight.ToString("F1");
            txtSpacing.Text = _lastSpacing.ToString("F1");
            SelectComboItemExact(cmbScale, _lastScale);
            SelectComboItemExact(cmbCtb, _lastCtb);
            rbLandscape.Checked = _lastIsLandscape;
            rbPortrait.Checked = !_lastIsLandscape;
        }

        private void SelectComboItemExact(WinFormsComboBox combo, string value)
        {
            // Ưu tiên exact match trước
            for (int i = 0; i < combo.Items.Count; i++)
            {
                if (combo.Items[i]?.ToString() == value)
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
            
            // Nếu không có exact match, tìm contains
            for (int i = 0; i < combo.Items.Count; i++)
            {
                string? item = combo.Items[i]?.ToString();
                if (item != null && item.Contains(value))
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
            
            // Mặc định chọn item đầu
            if (combo.Items.Count > 0)
                combo.SelectedIndex = 0;
        }

        private void BtnAddPoint_Click(object? sender, EventArgs e)
        {
            // Hide form to allow point picking
            this.Hide();
            
            try
            {
                // Prompt for point
                PromptPointOptions ppo = new PromptPointOptions("\n📍 Chọn điểm góc trái dưới của khung in (hoặc Esc để hủy):");
                ppo.AllowNone = true;
                PromptPointResult ppr = _editor.GetPoint(ppo);

                if (ppr.Status == PromptStatus.OK)
                {
                    // Prompt for quantity
                    PromptIntegerOptions pio = new PromptIntegerOptions("\n🔢 Nhập số lượng bản vẽ:");
                    pio.DefaultValue = 1;
                    pio.LowerLimit = 1;
                    pio.UpperLimit = 1000;
                    PromptIntegerResult pir = _editor.GetInteger(pio);

                    int quantity = pir.Status == PromptStatus.OK ? pir.Value : 1;

                    // Add to grid
                    int index = dgvPrintPoints.Rows.Count + 1;
                    dgvPrintPoints.Rows.Add(index, ppr.Value.X.ToString("F2"), ppr.Value.Y.ToString("F2"), quantity);

                    // Add to list
                    PrintPoints.Add(new PrintPointInfo(index, ppr.Value, quantity));

                    UpdateTotalPrints();

                    _editor.WriteMessage($"\n✅ Đã thêm điểm {index}: ({ppr.Value.X:F2}, {ppr.Value.Y:F2}) - Số lượng: {quantity}");
                }
            }
            catch (System.Exception ex)
            {
                _editor.WriteMessage($"\n❌ Lỗi: {ex.Message}");
            }
            finally
            {
                // Show form again
                this.Show();
                this.BringToFront();
                this.Focus();
            }
        }

        private void BtnRemovePoint_Click(object? sender, EventArgs e)
        {
            if (dgvPrintPoints.SelectedRows.Count > 0)
            {
                int selectedIndex = dgvPrintPoints.SelectedRows[0].Index;
                dgvPrintPoints.Rows.RemoveAt(selectedIndex);
                
                if (selectedIndex < PrintPoints.Count)
                    PrintPoints.RemoveAt(selectedIndex);

                // Renumber
                for (int i = 0; i < dgvPrintPoints.Rows.Count; i++)
                {
                    dgvPrintPoints.Rows[i].Cells["STT"].Value = i + 1;
                    if (i < PrintPoints.Count)
                        PrintPoints[i].Index = i + 1;
                }

                UpdateTotalPrints();
            }
        }

        private void BtnImport_Click(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Nhập danh sách điểm in",
                Filter = "CSV files (*.csv)|*.csv|Text files (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt = "csv"
            };

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var lines = File.ReadAllLines(ofd.FileName);
                    int imported = 0;

                    // Clear existing
                    dgvPrintPoints.Rows.Clear();
                    PrintPoints.Clear();

                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        if (line.StartsWith("STT") || line.StartsWith("#")) continue; // Skip header

                        var parts = line.Split(new[] { ',', '\t', ';' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 3)
                        {
                            // Format: X, Y, Quantity (or STT, X, Y, Quantity)
                            int startIdx = parts.Length >= 4 ? 1 : 0;
                            
                            if (double.TryParse(parts[startIdx], out double x) &&
                                double.TryParse(parts[startIdx + 1], out double y) &&
                                int.TryParse(parts[startIdx + 2], out int qty))
                            {
                                int index = PrintPoints.Count + 1;
                                dgvPrintPoints.Rows.Add(index, x.ToString("F2"), y.ToString("F2"), qty);
                                PrintPoints.Add(new PrintPointInfo(index, new Point3d(x, y, 0), qty));
                                imported++;
                            }
                        }
                    }

                    UpdateTotalPrints();
                    MessageBox.Show($"Đã nhập {imported} điểm thành công!", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show($"Lỗi nhập file: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnExport_Click(object? sender, EventArgs e)
        {
            if (PrintPoints.Count == 0)
            {
                MessageBox.Show("Không có điểm nào để xuất!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Title = "Xuất danh sách điểm in",
                Filter = "CSV files (*.csv)|*.csv|Text files (*.txt)|*.txt",
                DefaultExt = "csv",
                FileName = "PrintPoints_" + DateTime.Now.ToString("yyyyMMdd_HHmmss")
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    using var sw = new StreamWriter(sfd.FileName);
                    sw.WriteLine("STT,X,Y,SoLuong");
                    
                    foreach (var point in PrintPoints)
                    {
                        sw.WriteLine($"{point.Index},{point.StartPoint.X:F2},{point.StartPoint.Y:F2},{point.Quantity}");
                    }

                    MessageBox.Show($"Đã xuất {PrintPoints.Count} điểm thành công!\n{sfd.FileName}", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show($"Lỗi xuất file: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void DgvPrintPoints_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == 3 && e.RowIndex >= 0 && e.RowIndex < PrintPoints.Count) // Quantity column
            {
                if (int.TryParse(dgvPrintPoints.Rows[e.RowIndex].Cells["Quantity"].Value?.ToString(), out int qty))
                {
                    PrintPoints[e.RowIndex].Quantity = qty;
                    UpdateTotalPrints();
                }
            }
        }

        private void UpdateTotalPrints()
        {
            int total = PrintPoints.Sum(p => p.Quantity);
            lblTotalPrints.Text = $"Tổng:\n{total} bản";
        }

        private void BtnOK_Click(object? sender, EventArgs e)
        {
            // Validate
            if (PrintPoints.Count == 0)
            {
                MessageBox.Show("Vui lòng thêm ít nhất một điểm in!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!double.TryParse(txtFrameWidth.Text, out double frameWidth) || frameWidth <= 0)
            {
                MessageBox.Show("Chiều rộng khung phải là số dương!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                txtFrameWidth.Focus();
                return;
            }

            if (!double.TryParse(txtFrameHeight.Text, out double frameHeight) || frameHeight <= 0)
            {
                MessageBox.Show("Chiều cao khung phải là số dương!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                txtFrameHeight.Focus();
                return;
            }

            if (!double.TryParse(txtSpacing.Text, out double spacing) || spacing <= 0)
            {
                MessageBox.Show("Khoảng cách phải là số dương!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                txtSpacing.Focus();
                return;
            }

            // Save values
            SelectedPrinter = cmbPrinter.SelectedItem?.ToString() ?? "PDF reDirect v2";
            SelectedPaperSize = cmbPaperSize.SelectedItem?.ToString() ?? "A3";
            FrameWidth = frameWidth;
            FrameHeight = frameHeight;
            Spacing = spacing;
            SelectedScale = cmbScale.SelectedItem?.ToString() ?? "5:1";
            SelectedCtb = cmbCtb.SelectedItem?.ToString() ?? "monochrome.ctb";
            IsLandscape = rbLandscape.Checked;

            // Save for next session
            _lastPrinter = SelectedPrinter;
            _lastPaperSize = SelectedPaperSize;
            _lastFrameWidth = FrameWidth;
            _lastFrameHeight = FrameHeight;
            _lastSpacing = Spacing;
            _lastScale = SelectedScale;
            _lastCtb = SelectedCtb;
            _lastIsLandscape = IsLandscape;

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }

    /// <summary>
    /// Class chứa lệnh in hàng loạt
    /// </summary>
    public class AT_InModel_HangLoat_Commands
    {
        [CommandMethod("AT_In_Model_Hang_Loat")]
        public static void AT_InModel_HangLoat()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            try
            {
                ed.WriteMessage("\n=== 🖨️ IN HÀNG LOẠT MODEL - AT_InModel_HangLoat ===");

                // Kiểm tra xem đang ở Model space không
                if (db.TileMode == false)
                {
                    ed.WriteMessage("\n⚠️ Bạn đang ở Paper space. Vui lòng chuyển sang Model space.");
                    ed.WriteMessage("\n   Gõ lệnh MODEL hoặc nhấn phím Tab để chuyển.");
                    return;
                }

                // Hiển thị form
                List<PrintPointInfo> printPoints;
                string printer, paperSize, scale, ctb, orientation;
                double frameWidth, frameHeight, spacing;

                using (var form = new BatchPlotSettingsForm(ed))
                {
                    if (form.ShowDialog() != DialogResult.OK)
                    {
                        ed.WriteMessage("\n❌ Đã hủy lệnh.");
                        return;
                    }

                    // Lấy thông tin từ form
                    printPoints = new List<PrintPointInfo>(form.PrintPoints); // Clone list
                    printer = form.SelectedPrinter;
                    paperSize = form.SelectedPaperSize;
                    frameWidth = form.FrameWidth;
                    frameHeight = form.FrameHeight;
                    spacing = form.Spacing;
                    scale = form.SelectedScale;
                    ctb = form.SelectedCtb;
                    orientation = form.IsLandscape ? "Landscape" : "Portrait";
                }

                ed.WriteMessage($"\n\n📋 Thông tin in:");
                ed.WriteMessage($"\n   - Máy in: {printer}");
                ed.WriteMessage($"\n   - Kích thước giấy: {paperSize}");
                ed.WriteMessage($"\n   - Kích thước khung: {frameWidth} x {frameHeight} mm");
                ed.WriteMessage($"\n   - Khoảng cách: {spacing} mm");
                ed.WriteMessage($"\n   - Tỷ lệ: {scale}");
                ed.WriteMessage($"\n   - CTB: {ctb}");
                ed.WriteMessage($"\n   - Hướng: {orientation}");
                ed.WriteMessage($"\n   - Số điểm in: {printPoints.Count}");
                ed.WriteMessage($"\n   - Tổng số bản in: {printPoints.Sum(p => p.Quantity)}");

                // Xây dựng tất cả lệnh in thành một chuỗi LISP duy nhất
                // Sử dụng (progn ...) để đảm bảo tất cả lệnh thực thi liên tiếp
                var allCommands = new System.Text.StringBuilder();
                int totalPrinted = 0;

                // Bắt đầu block progn
                allCommands.AppendLine("(progn ");

                foreach (var pointInfo in printPoints)
                {
                    ed.WriteMessage($"\n\n📍 Chuẩn bị in từ điểm {pointInfo.Index}: ({pointInfo.StartPoint.X:F2}, {pointInfo.StartPoint.Y:F2}) - Số lượng: {pointInfo.Quantity}");

                    for (int i = 0; i < pointInfo.Quantity; i++)
                    {
                        // Tính điểm p3, p4 cho mỗi bản vẽ (giống LISP)
                        double offsetX = spacing * i;
                        double p3X = pointInfo.StartPoint.X + offsetX;
                        double p3Y = pointInfo.StartPoint.Y;
                        double p4X = p3X + frameWidth;
                        double p4Y = p3Y + frameHeight;

                        // Thêm lệnh -PLOT cho bản vẽ này
                        AppendPlotCommand(allCommands, 
                            printer, paperSize, orientation,
                            p3X, p3Y, p4X, p4Y,
                            scale, ctb);

                        totalPrinted++;
                        ed.WriteMessage($"\n   📄 Bản vẽ {i + 1}/{pointInfo.Quantity}: Window ({p3X:F0},{p3Y:F0}) to ({p4X:F0},{p4Y:F0})");
                    }
                }

                // Đóng block progn
                allCommands.AppendLine("(princ)) ");

                // Gửi tất cả lệnh một lần
                ed.WriteMessage($"\n\n🚀 Đang gửi {totalPrinted} lệnh in...");
                doc.SendStringToExecute(allCommands.ToString(), true, false, false);

                ed.WriteMessage($"\n\n🎉 Đã gửi lệnh in cho {totalPrinted} bản vẽ!");
                ed.WriteMessage("\n💡 Vui lòng đợi quá trình in hoàn tất.");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n❌ Lỗi: {ex.Message}");
                ed.WriteMessage($"\n   Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Thêm lệnh -PLOT vào StringBuilder - sử dụng LISP (command ...) syntax
        /// Giống hệt cách LISP gốc hoạt động
        /// </summary>
        private static void AppendPlotCommand(System.Text.StringBuilder sb,
            string printer, string paperSize, string orientation,
            double p3X, double p3Y, double p4X, double p4Y,
            string scale, string ctb)
        {
            // Sử dụng LISP (command ...) syntax để đảm bảo đồng bộ như LISP gốc
            // Format: (command "-PLOT" "Y" "" "printer" "papersize" "Millimeters" ...)
            
            sb.Append("(command \"-PLOT\" ");
            sb.Append("\"Y\" ");           // Detailed plot configuration? Yes
            sb.Append("\"\" ");            // Layout name (empty = current/Model)
            sb.Append($"\"{printer}\" ");  // Printer/plotter name
            sb.Append($"\"{paperSize}\" ");// Paper size
            sb.Append("\"Millimeters\" "); // Drawing units
            sb.Append($"\"{orientation}\" ");// Landscape/Portrait
            sb.Append("\"No\" ");          // Plot upside-down? No
            sb.Append("\"Window\" ");      // Plot area: Window
            sb.Append($"(list {p3X:F4} {p3Y:F4}) ");  // First corner as LISP list
            sb.Append($"(list {p4X:F4} {p4Y:F4}) ");  // Second corner as LISP list
            sb.Append($"\"{scale}\" ");    // Plot scale
            sb.Append("\"Center\" ");      // Plot offset: Center
            sb.Append("\"Yes\" ");         // Plot with plot styles? Yes
            sb.Append($"\"{ctb}\" ");      // Plot style table (CTB)
            sb.Append("\"Yes\" ");         // Plot with lineweights? Yes
            sb.Append("\"As displayed\" ");// Shade plot: As displayed
            sb.Append("\"No\" ");          // Save changes to layout? No
            sb.Append("\"No\" ");          // Plot to file? No
            sb.Append("\"Yes\"");          // Proceed with plot? Yes
            sb.AppendLine(") ");           // Close LISP command
        }
    }
}

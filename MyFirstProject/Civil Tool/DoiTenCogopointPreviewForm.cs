using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ClosedXML.Excel;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsFont = System.Drawing.Font;
using WinFormsPoint = System.Drawing.Point;

namespace MyFirstProject.Civil_Tool
{
    /// <summary>
    /// Data class chứa thông tin trước/sau đổi tên cho mỗi CogoPoint
    /// </summary>
    public class CogoPointRenameInfo
    {
        public uint PointNumber { get; set; }
        public string OldName { get; set; } = "";
        public string NewName { get; set; } = "";
        public string Description { get; set; } = "";
        public double Easting { get; set; }
        public double Northing { get; set; }
        public double Elevation { get; set; }
    }

    /// <summary>
    /// Form xem trước kết quả đổi tên CogoPoint (trước / sau)
    /// Hỗ trợ Export/Import Excel để chỉnh sửa tên trước khi áp dụng
    /// </summary>
    public class DoiTenCogopointPreviewForm : Form
    {
        public bool FormAccepted { get; private set; } = false;

        // UI Controls
        private DataGridView dgvPreview = null!;
        private WinFormsLabel lblSummary = null!;
        private Button btnExportExcel = null!;
        private Button btnImportExcel = null!;
        private Button btnOK = null!;
        private Button btnCancel = null!;
        private Panel pnlBottom = null!;
        private ToolTip toolTip = null!;

        private readonly List<CogoPointRenameInfo> _renameList;
        private string _lastExportPath = "";

        public DoiTenCogopointPreviewForm(List<CogoPointRenameInfo> renameList)
        {
            _renameList = renameList;
            InitializeComponent();
            LoadData();
        }

        /// <summary>
        /// Trả về danh sách rename đã cập nhật (có thể đã import từ Excel)
        /// </summary>
        public List<CogoPointRenameInfo> GetUpdatedRenameList()
        {
            return _renameList;
        }

        private void InitializeComponent()
        {
            this.dgvPreview = new DataGridView();
            this.lblSummary = new WinFormsLabel();
            this.btnExportExcel = new Button();
            this.btnImportExcel = new Button();
            this.btnOK = new Button();
            this.btnCancel = new Button();
            this.pnlBottom = new Panel();
            this.toolTip = new ToolTip();

            this.SuspendLayout();

            // === Form ===
            this.Text = "Xem trước kết quả đổi tên CogoPoint";
            this.Size = new Size(880, 520);
            this.MinimumSize = new Size(700, 380);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MaximizeBox = true;
            this.MinimizeBox = false;
            this.ShowIcon = true;

            // === DataGridView ===
            this.dgvPreview.Dock = DockStyle.Fill;
            this.dgvPreview.ReadOnly = true;
            this.dgvPreview.AllowUserToAddRows = false;
            this.dgvPreview.AllowUserToDeleteRows = false;
            this.dgvPreview.AllowUserToResizeRows = false;
            this.dgvPreview.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            this.dgvPreview.MultiSelect = false;
            this.dgvPreview.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvPreview.RowHeadersVisible = false;
            this.dgvPreview.BackgroundColor = Color.White;
            this.dgvPreview.BorderStyle = BorderStyle.None;
            this.dgvPreview.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(245, 249, 255)
            };
            this.dgvPreview.DefaultCellStyle = new DataGridViewCellStyle
            {
                Font = new WinFormsFont("Segoe UI", 9f),
                SelectionBackColor = Color.FromArgb(200, 220, 255),
                SelectionForeColor = Color.Black
            };
            this.dgvPreview.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                Font = new WinFormsFont("Segoe UI", 9f, FontStyle.Bold),
                BackColor = Color.FromArgb(60, 90, 130),
                ForeColor = Color.White,
                Alignment = DataGridViewContentAlignment.MiddleCenter
            };
            this.dgvPreview.EnableHeadersVisualStyles = false;
            this.dgvPreview.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;

            // Add columns
            this.dgvPreview.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colSTT",
                HeaderText = "STT",
                Width = 45,
                FillWeight = 7,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            this.dgvPreview.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colPointNumber",
                HeaderText = "Point #",
                Width = 65,
                FillWeight = 9,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            this.dgvPreview.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colOldName",
                HeaderText = "Tên cũ",
                Width = 150,
                FillWeight = 22
            });
            this.dgvPreview.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colArrow",
                HeaderText = "",
                Width = 35,
                FillWeight = 4,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    Font = new WinFormsFont("Segoe UI", 10f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(0, 120, 60)
                }
            });
            this.dgvPreview.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colNewName",
                HeaderText = "Tên mới",
                Width = 150,
                FillWeight = 22,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    ForeColor = Color.FromArgb(0, 100, 180),
                    Font = new WinFormsFont("Segoe UI", 9f, FontStyle.Bold)
                }
            });
            this.dgvPreview.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colDescription",
                HeaderText = "Mô tả",
                Width = 100,
                FillWeight = 14
            });
            this.dgvPreview.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colEasting",
                HeaderText = "Easting (X)",
                Width = 90,
                FillWeight = 11,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    Format = "F3"
                }
            });
            this.dgvPreview.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "colNorthing",
                HeaderText = "Northing (Y)",
                Width = 90,
                FillWeight = 11,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    Format = "F3"
                }
            });

            // === Bottom Panel ===
            this.pnlBottom.Dock = DockStyle.Bottom;
            this.pnlBottom.Height = 55;
            this.pnlBottom.Padding = new Padding(10);
            this.pnlBottom.BackColor = Color.FromArgb(245, 245, 248);

            // Summary label
            this.lblSummary.AutoSize = true;
            this.lblSummary.Location = new WinFormsPoint(12, 18);
            this.lblSummary.Font = new WinFormsFont("Segoe UI", 9f);
            this.lblSummary.ForeColor = Color.FromArgb(80, 80, 80);

            // --- Export Excel Button ---
            this.btnExportExcel.Text = "📤 Xuất Excel";
            this.btnExportExcel.Size = new Size(105, 32);
            this.btnExportExcel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            this.btnExportExcel.BackColor = Color.FromArgb(33, 115, 70);
            this.btnExportExcel.ForeColor = Color.White;
            this.btnExportExcel.FlatStyle = FlatStyle.Flat;
            this.btnExportExcel.FlatAppearance.BorderSize = 0;
            this.btnExportExcel.Font = new WinFormsFont("Segoe UI", 9f, FontStyle.Bold);
            this.btnExportExcel.Cursor = Cursors.Hand;
            this.btnExportExcel.Click += BtnExportExcel_Click;
            this.toolTip.SetToolTip(this.btnExportExcel, "Xuất danh sách ra file Excel để chỉnh sửa tên mới");

            // --- Import Excel Button ---
            this.btnImportExcel.Text = "📥 Nhập Excel";
            this.btnImportExcel.Size = new Size(105, 32);
            this.btnImportExcel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            this.btnImportExcel.BackColor = Color.FromArgb(180, 100, 20);
            this.btnImportExcel.ForeColor = Color.White;
            this.btnImportExcel.FlatStyle = FlatStyle.Flat;
            this.btnImportExcel.FlatAppearance.BorderSize = 0;
            this.btnImportExcel.Font = new WinFormsFont("Segoe UI", 9f, FontStyle.Bold);
            this.btnImportExcel.Cursor = Cursors.Hand;
            this.btnImportExcel.Click += BtnImportExcel_Click;
            this.toolTip.SetToolTip(this.btnImportExcel, "Nhập lại file Excel đã chỉnh sửa cột 'Tên mới'");

            // --- OK Button ---
            this.btnOK.Text = "✅ Đổi tên";
            this.btnOK.Size = new Size(95, 32);
            this.btnOK.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            this.btnOK.BackColor = Color.FromArgb(0, 120, 215);
            this.btnOK.ForeColor = Color.White;
            this.btnOK.FlatStyle = FlatStyle.Flat;
            this.btnOK.FlatAppearance.BorderSize = 0;
            this.btnOK.Font = new WinFormsFont("Segoe UI", 9f, FontStyle.Bold);
            this.btnOK.Cursor = Cursors.Hand;
            this.btnOK.Click += BtnOK_Click;

            // --- Cancel Button ---
            this.btnCancel.Text = "Hủy";
            this.btnCancel.Size = new Size(75, 32);
            this.btnCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            this.btnCancel.FlatStyle = FlatStyle.Flat;
            this.btnCancel.FlatAppearance.BorderColor = Color.FromArgb(180, 180, 180);
            this.btnCancel.Font = new WinFormsFont("Segoe UI", 9f);
            this.btnCancel.Cursor = Cursors.Hand;
            this.btnCancel.Click += BtnCancel_Click;

            // Position buttons
            this.Resize += Form_Resize;

            // Add to bottom panel
            this.pnlBottom.Controls.AddRange(new Control[]
            {
                lblSummary, btnExportExcel, btnImportExcel, btnOK, btnCancel
            });

            // Add to form
            this.Controls.Add(this.dgvPreview);
            this.Controls.Add(this.pnlBottom);

            this.ResumeLayout(false);

            // Initial button positioning
            PositionButtons();
        }

        private void LoadData()
        {
            dgvPreview.Rows.Clear();

            for (int i = 0; i < _renameList.Count; i++)
            {
                var info = _renameList[i];
                dgvPreview.Rows.Add(
                    (i + 1).ToString(),
                    info.PointNumber.ToString(),
                    info.OldName,
                    "→",
                    info.NewName,
                    info.Description,
                    info.Easting,
                    info.Northing
                );
            }

            lblSummary.Text = $"Tổng cộng: {_renameList.Count} CogoPoint sẽ được đổi tên";
        }

        private void PositionButtons()
        {
            int rightMargin = this.ClientSize.Width - 15;
            btnCancel.Location = new WinFormsPoint(rightMargin - btnCancel.Width, 12);
            btnOK.Location = new WinFormsPoint(btnCancel.Left - btnOK.Width - 8, 12);
            btnImportExcel.Location = new WinFormsPoint(btnOK.Left - btnImportExcel.Width - 16, 12);
            btnExportExcel.Location = new WinFormsPoint(btnImportExcel.Left - btnExportExcel.Width - 6, 12);
        }

        private void Form_Resize(object? sender, EventArgs e)
        {
            PositionButtons();
        }

        // ============================
        // EXPORT TO EXCEL
        // ============================
        private void BtnExportExcel_Click(object? sender, EventArgs e)
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Title = "Xuất danh sách CogoPoint ra Excel";
                sfd.Filter = "Excel Files (*.xlsx)|*.xlsx";
                sfd.FileName = $"DoiTen_CogoPoint_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

                if (sfd.ShowDialog() != DialogResult.OK)
                    return;

                try
                {
                    using (var workbook = new XLWorkbook())
                    {
                        var ws = workbook.Worksheets.Add("CogoPoint_Rename");

                        // === Header row ===
                        string[] headers = { "STT", "Point #", "Tên cũ", "Tên mới", "Mô tả", "Easting (X)", "Northing (Y)", "Elevation (Z)" };
                        for (int col = 0; col < headers.Length; col++)
                        {
                            var cell = ws.Cell(1, col + 1);
                            cell.Value = headers[col];
                            cell.Style.Font.Bold = true;
                            cell.Style.Font.FontColor = XLColor.White;
                            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(60, 90, 130);
                            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        }

                        // === Data rows ===
                        for (int i = 0; i < _renameList.Count; i++)
                        {
                            var info = _renameList[i];
                            int row = i + 2;

                            ws.Cell(row, 1).Value = i + 1;                        // STT
                            ws.Cell(row, 2).Value = (int)info.PointNumber;         // Point #
                            ws.Cell(row, 3).Value = info.OldName;                  // Tên cũ
                            ws.Cell(row, 4).Value = info.NewName;                  // Tên mới (editable)
                            ws.Cell(row, 5).Value = info.Description;              // Mô tả
                            ws.Cell(row, 6).Value = info.Easting;                  // Easting
                            ws.Cell(row, 7).Value = info.Northing;                 // Northing
                            ws.Cell(row, 8).Value = info.Elevation;                // Elevation

                            // Highlight "Tên mới" column with editable color
                            ws.Cell(row, 4).Style.Fill.BackgroundColor = XLColor.FromArgb(255, 255, 210);
                            ws.Cell(row, 4).Style.Font.Bold = true;
                            ws.Cell(row, 4).Style.Font.FontColor = XLColor.FromArgb(0, 100, 180);

                            // Alternate row color
                            if (i % 2 == 1)
                            {
                                for (int col = 1; col <= headers.Length; col++)
                                {
                                    if (col != 4) // skip "Tên mới" column
                                        ws.Cell(row, col).Style.Fill.BackgroundColor = XLColor.FromArgb(245, 249, 255);
                                }
                            }

                            // Border for all cells
                            for (int col = 1; col <= headers.Length; col++)
                            {
                                ws.Cell(row, col).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                                ws.Cell(row, col).Style.Border.OutsideBorderColor = XLColor.FromArgb(200, 200, 200);
                            }
                        }

                        // Column widths
                        ws.Column(1).Width = 6;    // STT
                        ws.Column(2).Width = 10;   // Point #
                        ws.Column(3).Width = 25;   // Tên cũ
                        ws.Column(4).Width = 25;   // Tên mới
                        ws.Column(5).Width = 18;   // Mô tả
                        ws.Column(6).Width = 15;   // Easting
                        ws.Column(7).Width = 15;   // Northing
                        ws.Column(8).Width = 12;   // Elevation

                        // Format number columns
                        ws.Column(6).Style.NumberFormat.Format = "0.000";
                        ws.Column(7).Style.NumberFormat.Format = "0.000";
                        ws.Column(8).Style.NumberFormat.Format = "0.000";

                        // Add note at bottom
                        int noteRow = _renameList.Count + 3;
                        ws.Cell(noteRow, 1).Value = "📝 Hướng dẫn: Chỉ chỉnh sửa cột \"Tên mới\" (cột D - highlight vàng). Sau đó lưu file và nhấn nút \"Nhập Excel\" trong form.";
                        ws.Range(noteRow, 1, noteRow, 8).Merge();
                        ws.Cell(noteRow, 1).Style.Font.Italic = true;
                        ws.Cell(noteRow, 1).Style.Font.FontColor = XLColor.FromArgb(120, 120, 120);

                        // Freeze header row
                        ws.SheetView.FreezeRows(1);

                        // Protect all columns except "Tên mới" (column 4)
                        ws.Protect();
                        ws.Column(4).Style.Protection.SetLocked(false);
                        // Also unlock header
                        ws.Cell(1, 4).Style.Protection.SetLocked(true);

                        workbook.SaveAs(sfd.FileName);
                    }

                    _lastExportPath = sfd.FileName;

                    MessageBox.Show(
                        $"Đã xuất thành công!\n\n" +
                        $"File: {sfd.FileName}\n\n" +
                        $"Hướng dẫn:\n" +
                        $"1. Mở file Excel\n" +
                        $"2. Chỉnh sửa cột \"Tên mới\" (cột D - highlight vàng)\n" +
                        $"3. Lưu file\n" +
                        $"4. Nhấn nút \"Nhập Excel\" để cập nhật lại",
                        "Xuất Excel thành công",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show(
                        $"Lỗi khi xuất Excel:\n{ex.Message}",
                        "Lỗi",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                }
            }
        }

        // ============================
        // IMPORT FROM EXCEL
        // ============================
        private void BtnImportExcel_Click(object? sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "Nhập file Excel đã chỉnh sửa";
                ofd.Filter = "Excel Files (*.xlsx)|*.xlsx";

                // Default to last export path
                if (!string.IsNullOrEmpty(_lastExportPath) && File.Exists(_lastExportPath))
                {
                    ofd.InitialDirectory = Path.GetDirectoryName(_lastExportPath);
                    ofd.FileName = Path.GetFileName(_lastExportPath);
                }

                if (ofd.ShowDialog() != DialogResult.OK)
                    return;

                try
                {
                    using (var workbook = new XLWorkbook(ofd.FileName))
                    {
                        var ws = workbook.Worksheet(1); // First sheet

                        // Detect data range
                        int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
                        int updatedCount = 0;
                        int mismatchCount = 0;
                        var changes = new List<string>();

                        for (int row = 2; row <= lastRow; row++)
                        {
                            // Read Point # from column 2
                            var pointNumCell = ws.Cell(row, 2);
                            if (pointNumCell.IsEmpty()) continue;

                            uint pointNumber;
                            if (!uint.TryParse(pointNumCell.GetString(), out pointNumber))
                                continue;

                            // Read new name from column 4
                            string newName = ws.Cell(row, 4).GetString().Trim();
                            if (string.IsNullOrEmpty(newName)) continue;

                            // Find matching item in renameList by PointNumber
                            var match = _renameList.FirstOrDefault(r => r.PointNumber == pointNumber);
                            if (match != null)
                            {
                                if (match.NewName != newName)
                                {
                                    changes.Add($"  Point #{pointNumber}: [{match.NewName}] → [{newName}]");
                                    match.NewName = newName;
                                    updatedCount++;
                                }
                            }
                            else
                            {
                                mismatchCount++;
                            }
                        }

                        // Refresh grid
                        LoadData();

                        // Summary message
                        string msg = $"Nhập Excel hoàn tất!\n\n" +
                                     $"• Đã cập nhật: {updatedCount} tên mới\n" +
                                     $"• Không thay đổi: {_renameList.Count - updatedCount} điểm\n";

                        if (mismatchCount > 0)
                            msg += $"• Không khớp Point#: {mismatchCount} dòng (bỏ qua)\n";

                        if (changes.Count > 0 && changes.Count <= 15)
                        {
                            msg += $"\nChi tiết thay đổi:\n{string.Join("\n", changes)}";
                        }
                        else if (changes.Count > 15)
                        {
                            msg += $"\nChi tiết thay đổi (15 đầu tiên):\n{string.Join("\n", changes.Take(15))}\n  ... và {changes.Count - 15} thay đổi khác";
                        }

                        MessageBox.Show(msg, "Nhập Excel", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (IOException)
                {
                    MessageBox.Show(
                        "Không thể đọc file Excel!\n\nHãy đóng file trong Excel trước rồi thử lại.",
                        "Lỗi",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show(
                        $"Lỗi khi nhập Excel:\n{ex.Message}",
                        "Lỗi",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                }
            }
        }

        private void BtnOK_Click(object? sender, EventArgs e)
        {
            FormAccepted = true;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void BtnCancel_Click(object? sender, EventArgs e)
        {
            FormAccepted = false;
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}

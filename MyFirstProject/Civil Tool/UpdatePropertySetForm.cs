using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ClosedXML.Excel;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsFont = System.Drawing.Font;
using WinFormsPoint = System.Drawing.Point;
using MyFirstProject.Extensions;

namespace Civil3D_Csharp
{
    /// <summary>
    /// Giao diện quản lý, ánh xạ và cập nhật Property Set cho 3D Solid và Body theo Layer.
    /// Hỗ trợ:
    /// - Quét tự động 3D Solid và Body trong ModelSpace
    /// - Nhập/Xuất cấu hình mapping Layer <-> Cấu kiện, Vật liệu ra Excel (.xlsx)
    /// - Ghi nhớ toàn bộ thông số và giá trị cấu hình lần chạy trước
    /// </summary>
    public class UpdatePropertySetForm : Form
    {
        // ================= GHI NHỚ THÔNG SỐ (PERSISTENT STATE) =================
        private static string _lastPropertySetName = PropertySetUtils.DefaultPropertySetName;
        private static string _lastExcelPath = "";
        private static Dictionary<string, (string CauKien, string VatLieu)> _lastMappings = new(StringComparer.OrdinalIgnoreCase);
        private static Size? _lastFormSize = null;

        // Dữ liệu nội bộ
        private List<LayerPropertyMapping> _allMappings = new();
        private List<LayerPropertyMapping> _displayedMappings = new();

        // Controls
        private GroupBox grpTop = null!;
        private WinFormsLabel lblPropSetName = null!;
        private TextBox txtPropSetName = null!;
        private WinFormsLabel lblSearch = null!;
        private TextBox txtSearch = null!;
        private WinFormsLabel lblStats = null!;
        private Button btnSelectAll = null!;
        private Button btnDeselectAll = null!;
        private Button btnRefresh = null!;

        private GroupBox grpGrid = null!;
        private DataGridView dgvLayers = null!;

        private Panel pnlBottom = null!;
        private Button btnImportExcel = null!;
        private Button btnExportExcel = null!;
        private Button btnApply = null!;
        private Button btnClose = null!;

        // Callback thực thi cập nhật
        public Func<List<LayerPropertyMapping>>? OnRescanCad { get; set; }
        public Action<string, List<LayerPropertyMapping>>? OnApplyPropertySet { get; set; }

        public UpdatePropertySetForm(List<LayerPropertyMapping> initialMappings)
        {
            _allMappings = initialMappings ?? new List<LayerPropertyMapping>();

            InitializeComponent();
            RestoreLastSettings();
            RefreshGrid();

            this.FormClosing += (s, e) => SaveCurrentSettings();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Cấu hình Form chính
            this.Text = "Cập Nhật Thông Tin Property Set (3D Solid & 3D Body Theo Layer)";
            this.Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            this.Size = _lastFormSize ?? new Size(920, 600);
            this.MinimumSize = new Size(800, 480);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ShowIcon = false;

            // ================= 1. TOP GROUPBOX: THIẾT LẬP & TÌM KIẾM =================
            grpTop = new GroupBox
            {
                Text = "Cấu hình Property Set & Bộ lọc",
                Dock = DockStyle.Top,
                Height = 95,
                Padding = new Padding(10, 8, 10, 8)
            };

            lblPropSetName = new WinFormsLabel
            {
                Text = "Tên Property Set:",
                Location = new WinFormsPoint(15, 25),
                AutoSize = true
            };

            txtPropSetName = new TextBox
            {
                Location = new WinFormsPoint(125, 22),
                Size = new Size(230, 23),
                Text = _lastPropertySetName
            };

            lblSearch = new WinFormsLabel
            {
                Text = "Tìm kiếm Layer:",
                Location = new WinFormsPoint(375, 25),
                AutoSize = true
            };

            txtSearch = new TextBox
            {
                Location = new WinFormsPoint(475, 22),
                Size = new Size(180, 23)
            };
            txtSearch.TextChanged += (s, e) => FilterData();

            btnSelectAll = new Button
            {
                Text = "☑ Chọn tất cả",
                Location = new WinFormsPoint(670, 20),
                Size = new Size(100, 27),
                Cursor = Cursors.Hand
            };
            btnSelectAll.Click += (s, e) => SetAllSelection(true);

            btnDeselectAll = new Button
            {
                Text = "☐ Bỏ chọn",
                Location = new WinFormsPoint(775, 20),
                Size = new Size(90, 27),
                Cursor = Cursors.Hand
            };
            btnDeselectAll.Click += (s, e) => SetAllSelection(false);

            lblStats = new WinFormsLabel
            {
                Text = "Đang thống kê...",
                Location = new WinFormsPoint(15, 60),
                AutoSize = true,
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = Color.DarkBlue
            };

            btnRefresh = new Button
            {
                Text = "🔄 Quét lại từ CAD",
                Location = new WinFormsPoint(670, 55),
                Size = new Size(195, 28),
                Cursor = Cursors.Hand
            };
            btnRefresh.Click += BtnRefresh_Click;

            grpTop.Controls.AddRange(new Control[]
            {
                lblPropSetName, txtPropSetName,
                lblSearch, txtSearch,
                btnSelectAll, btnDeselectAll,
                lblStats, btnRefresh
            });

            // ================= 2. CENTER GROUPBOX: DATAGRIDVIEW =================
            grpGrid = new GroupBox
            {
                Text = "Danh sách Layer và Ánh xạ Thuộc tính",
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            dgvLayers = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.Fixed3D,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                MultiSelect = false,
                AutoGenerateColumns = false,
                EnableHeadersVisualStyles = false
            };

            dgvLayers.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(240, 244, 250);
            dgvLayers.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(20, 35, 60);
            dgvLayers.ColumnHeadersDefaultCellStyle.Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
            dgvLayers.ColumnHeadersHeight = 32;

            // Định nghĩa cột
            var colCheck = new DataGridViewCheckBoxColumn
            {
                Name = "colCheck",
                HeaderText = "Chọn",
                Width = 50,
                DataPropertyName = "IsSelected"
            };

            var colLayer = new DataGridViewTextBoxColumn
            {
                Name = "colLayer",
                HeaderText = "Tên Layer",
                Width = 180,
                ReadOnly = true,
                DataPropertyName = "LayerName"
            };

            var colSolid = new DataGridViewTextBoxColumn
            {
                Name = "colSolid",
                HeaderText = "Solid 3D",
                Width = 80,
                ReadOnly = true,
                DataPropertyName = "SolidCount",
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
            };

            var colBody = new DataGridViewTextBoxColumn
            {
                Name = "colBody",
                HeaderText = "3D Body",
                Width = 80,
                ReadOnly = true,
                DataPropertyName = "BodyCount",
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
            };

            var colTotal = new DataGridViewTextBoxColumn
            {
                Name = "colTotal",
                HeaderText = "Tổng số",
                Width = 80,
                ReadOnly = true,
                DataPropertyName = "TotalCount",
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point) }
            };

            var colCauKien = new DataGridViewTextBoxColumn
            {
                Name = "colCauKien",
                HeaderText = "Cấu kiện (Property Set)",
                Width = 200,
                DataPropertyName = "CauKien"
            };

            var colVatLieu = new DataGridViewTextBoxColumn
            {
                Name = "colVatLieu",
                HeaderText = "Vật liệu (Property Set)",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 180,
                DataPropertyName = "VatLieu"
            };

            dgvLayers.Columns.AddRange(new DataGridViewColumn[]
            {
                colCheck, colLayer, colSolid, colBody, colTotal, colCauKien, colVatLieu
            });

            // Khi người dùng sửa cell trên grid, đồng bộ lại vào model
            dgvLayers.CellValueChanged += DgvLayers_CellValueChanged;
            dgvLayers.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (dgvLayers.IsCurrentCellDirty)
                {
                    dgvLayers.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };

            grpGrid.Controls.Add(dgvLayers);

            // ================= 3. BOTTOM PANEL: ACTIONS =================
            pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                Padding = new Padding(10, 8, 10, 8)
            };

            btnImportExcel = new Button
            {
                Text = "📥 Nhập từ Excel (.xlsx)",
                Size = new Size(160, 34),
                Location = new WinFormsPoint(10, 8),
                BackColor = Color.FromArgb(235, 245, 255),
                Cursor = Cursors.Hand
            };
            btnImportExcel.Click += BtnImportExcel_Click;

            btnExportExcel = new Button
            {
                Text = "📤 Xuất ra Excel (.xlsx)",
                Size = new Size(160, 34),
                Location = new WinFormsPoint(180, 8),
                BackColor = Color.FromArgb(235, 245, 255),
                Cursor = Cursors.Hand
            };
            btnExportExcel.Click += BtnExportExcel_Click;

            btnApply = new Button
            {
                Text = "✅ Cập nhật Property Set",
                Size = new Size(190, 34),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new WinFormsPoint(pnlBottom.Width - 300, 8),
                BackColor = Color.FromArgb(40, 140, 60),
                ForeColor = Color.White,
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point),
                Cursor = Cursors.Hand
            };
            btnApply.Click += BtnApply_Click;

            btnClose = new Button
            {
                Text = "❌ Đóng",
                Size = new Size(90, 34),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new WinFormsPoint(pnlBottom.Width - 100, 8),
                Cursor = Cursors.Hand
            };
            btnClose.Click += (s, e) => this.Close();

            pnlBottom.Controls.AddRange(new Control[]
            {
                btnImportExcel, btnExportExcel, btnApply, btnClose
            });

            // Gắn vào Form
            this.Controls.Add(grpGrid);
            this.Controls.Add(pnlBottom);
            this.Controls.Add(grpTop);

            this.ResumeLayout(false);
        }

        // ================= PERSISTENT SETTINGS =================

        private void RestoreLastSettings()
        {
            if (!string.IsNullOrWhiteSpace(_lastPropertySetName))
            {
                txtPropSetName.Text = _lastPropertySetName;
            }

            // Phục hồi lại Cấu kiện & Vật liệu đã lưu từ lần trước
            if (_lastMappings != null && _lastMappings.Count > 0)
            {
                foreach (var item in _allMappings)
                {
                    if (_lastMappings.TryGetValue(item.LayerName, out var saved))
                    {
                        if (!string.IsNullOrEmpty(saved.CauKien)) item.CauKien = saved.CauKien;
                        if (!string.IsNullOrEmpty(saved.VatLieu)) item.VatLieu = saved.VatLieu;
                    }
                }
            }
        }

        private void SaveCurrentSettings()
        {
            _lastFormSize = this.Size;
            _lastPropertySetName = txtPropSetName.Text.Trim();

            // Lưu mapping của từng layer
            _lastMappings.Clear();
            foreach (var item in _allMappings)
            {
                if (!string.IsNullOrWhiteSpace(item.LayerName))
                {
                    _lastMappings[item.LayerName] = (item.CauKien ?? "", item.VatLieu ?? "");
                }
            }
        }

        // ================= DATA BINDING & FILTERING =================

        private void RefreshGrid()
        {
            FilterData();
            UpdateStats();
        }

        private void FilterData()
        {
            string keyword = txtSearch.Text.Trim().ToLower();

            if (string.IsNullOrEmpty(keyword))
            {
                _displayedMappings = new List<LayerPropertyMapping>(_allMappings);
            }
            else
            {
                _displayedMappings = _allMappings
                    .Where(m => (m.LayerName != null && m.LayerName.ToLower().Contains(keyword)) ||
                                (m.CauKien != null && m.CauKien.ToLower().Contains(keyword)) ||
                                (m.VatLieu != null && m.VatLieu.ToLower().Contains(keyword)))
                    .ToList();
            }

            dgvLayers.DataSource = null;
            dgvLayers.DataSource = _displayedMappings;
        }

        private void UpdateStats()
        {
            int totalLayers = _allMappings.Count;
            int totalSolids = _allMappings.Sum(x => x.SolidCount);
            int totalBodies = _allMappings.Sum(x => x.BodyCount);
            int grandTotal = totalSolids + totalBodies;
            int selectedLayers = _allMappings.Count(x => x.IsSelected);

            lblStats.Text = $"Tổng số: {totalLayers} Layer ({selectedLayers} đang chọn) | Solid 3D: {totalSolids:N0} | Body: {totalBodies:N0} | Tổng đối tượng: {grandTotal:N0}";
        }

        private void SetAllSelection(bool isSelected)
        {
            foreach (var item in _displayedMappings)
            {
                item.IsSelected = isSelected;
            }
            dgvLayers.Refresh();
            UpdateStats();
        }

        private void DgvLayers_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _displayedMappings.Count) return;

            var currentItem = _displayedMappings[e.RowIndex];
            // Đồng bộ với danh sách gốc nếu item được sửa
            var originItem = _allMappings.FirstOrDefault(x => string.Equals(x.LayerName, currentItem.LayerName, StringComparison.OrdinalIgnoreCase));
            if (originItem != null && originItem != currentItem)
            {
                originItem.IsSelected = currentItem.IsSelected;
                originItem.CauKien = currentItem.CauKien;
                originItem.VatLieu = currentItem.VatLieu;
            }

            UpdateStats();
        }

        // ================= EVENT HANDLERS =================

        private void BtnRefresh_Click(object? sender, EventArgs e)
        {
            if (OnRescanCad != null)
            {
                try
                {
                    var newMappings = OnRescanCad();
                    if (newMappings != null)
                    {
                        // Giữ lại các giá trị Cấu kiện, Vật liệu đang có
                        var currentDict = _allMappings.ToDictionary(x => x.LayerName, x => (x.CauKien, x.VatLieu, x.IsSelected), StringComparer.OrdinalIgnoreCase);

                        foreach (var item in newMappings)
                        {
                            if (currentDict.TryGetValue(item.LayerName, out var saved))
                            {
                                item.CauKien = saved.CauKien;
                                item.VatLieu = saved.VatLieu;
                                item.IsSelected = saved.IsSelected;
                            }
                            else if (_lastMappings.TryGetValue(item.LayerName, out var last))
                            {
                                item.CauKien = last.CauKien;
                                item.VatLieu = last.VatLieu;
                            }
                        }

                        _allMappings = newMappings;
                        RefreshGrid();
                        MessageBox.Show($"Đã quét lại thành công! Tìm thấy {_allMappings.Count} Layer chứa đối tượng 3D Solid / Body.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show($"Lỗi khi quét bản vẽ: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void BtnExportExcel_Click(object? sender, EventArgs e)
        {
            if (_allMappings.Count == 0)
            {
                MessageBox.Show("Không có dữ liệu Layer để xuất ra Excel!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                Title = "Xuất cấu hình ánh xạ Property Set ra Excel",
                FileName = $"PropertySet_Mapping_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                InitialDirectory = !string.IsNullOrEmpty(_lastExcelPath) ? Path.GetDirectoryName(_lastExcelPath) : ""
            };

            if (sfd.ShowDialog() != DialogResult.OK) return;

            try
            {
                _lastExcelPath = sfd.FileName;

                using var workbook = new XLWorkbook();
                var ws = workbook.Worksheets.Add("PropertySet_Mapping");

                // Tiêu đề cột
                string[] headers = { "STT", "Tên Layer", "Số lượng Solid", "Số lượng Body", "Tổng số lượng", "Cấu kiện", "Vật liệu" };
                for (int c = 0; c < headers.Length; c++)
                {
                    var cell = ws.Cell(1, c + 1);
                    cell.Value = headers[c];
                    cell.Style.Font.Bold = true;
                    cell.Style.Font.FontColor = XLColor.White;
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1B365D");
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                }
                ws.Row(1).Height = 26;

                // Ghi dữ liệu
                int row = 2;
                int stt = 1;
                foreach (var item in _allMappings)
                {
                    ws.Cell(row, 1).SetValue(stt++);
                    ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    ws.Cell(row, 2).SetValue(item.LayerName);
                    ws.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

                    ws.Cell(row, 3).SetValue(item.SolidCount);
                    ws.Cell(row, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                    ws.Cell(row, 4).SetValue(item.BodyCount);
                    ws.Cell(row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                    ws.Cell(row, 5).SetValue(item.TotalCount);
                    ws.Cell(row, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    ws.Cell(row, 5).Style.Font.Bold = true;

                    ws.Cell(row, 6).SetValue(item.CauKien ?? "");
                    ws.Cell(row, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

                    ws.Cell(row, 7).SetValue(item.VatLieu ?? "");
                    ws.Cell(row, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

                    row++;
                }

                // Kẻ viền bảng và tự co giãn cột
                var range = ws.Range(1, 1, row - 1, headers.Length);
                range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                ws.Columns().AdjustToContents(10, 45);

                workbook.SaveAs(sfd.FileName);

                var dr = MessageBox.Show($"Xuất Excel thành công:\n{sfd.FileName}\n\nBạn có muốn mở file ngay không?", "Thành công", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (dr == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = sfd.FileName,
                        UseShellExecute = true
                    });
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Lỗi khi xuất file Excel: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnImportExcel_Click(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                Title = "Chọn file Excel cấu hình ánh xạ Property Set",
                InitialDirectory = !string.IsNullOrEmpty(_lastExcelPath) ? Path.GetDirectoryName(_lastExcelPath) : ""
            };

            if (ofd.ShowDialog() != DialogResult.OK) return;

            try
            {
                _lastExcelPath = ofd.FileName;

                using var workbook = new XLWorkbook(ofd.FileName);
                var ws = workbook.Worksheets.FirstOrDefault();
                if (ws == null)
                {
                    MessageBox.Show("File Excel không chứa bất kỳ Sheet nào!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var rangeUsed = ws.RangeUsed();
                if (rangeUsed == null)
                {
                    MessageBox.Show("File Excel không có dữ liệu!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                int firstRow = rangeUsed.FirstRow().RowNumber();
                int lastRow = rangeUsed.LastRow().RowNumber();
                int firstCol = rangeUsed.FirstColumn().ColumnNumber();
                int lastCol = rangeUsed.LastColumn().ColumnNumber();

                // Tìm hàng tiêu đề (quét tối đa 5 hàng đầu trong vùng dữ liệu)
                int headerRow = firstRow;
                int colLayerIdx = -1;
                int colCauKienIdx = -1;
                int colVatLieuIdx = -1;

                int maxHeaderScanRow = Math.Min(firstRow + 4, lastRow);
                for (int r = firstRow; r <= maxHeaderScanRow; r++)
                {
                    for (int c = firstCol; c <= lastCol; c++)
                    {
                        string val = ws.Cell(r, c).GetString().Trim().ToLower();
                        if (val.Contains("layer") || val == "tên layer") colLayerIdx = c;
                        else if (val.Contains("cấu kiện") || val.Contains("cau kien")) colCauKienIdx = c;
                        else if (val.Contains("vật liệu") || val.Contains("vat lieu")) colVatLieuIdx = c;
                    }

                    if (colLayerIdx > 0 && (colCauKienIdx > 0 || colVatLieuIdx > 0))
                    {
                        headerRow = r;
                        break;
                    }
                }

                if (colLayerIdx <= 0)
                {
                    // Fallback theo vị trí mặc định trong vùng dữ liệu
                    colLayerIdx = Math.Min(firstCol + 1, lastCol);
                    colCauKienIdx = Math.Min(firstCol + 5, lastCol);
                    colVatLieuIdx = Math.Min(firstCol + 6, lastCol);
                }

                int importedCount = 0;
                var layerMap = _allMappings.ToDictionary(x => x.LayerName, x => x, StringComparer.OrdinalIgnoreCase);

                for (int r = headerRow + 1; r <= lastRow; r++)
                {
                    string layerName = ws.Cell(r, colLayerIdx).GetString().Trim();
                    if (string.IsNullOrEmpty(layerName)) continue;

                    string cauKien = colCauKienIdx > 0 ? ws.Cell(r, colCauKienIdx).GetString().Trim() : "";
                    string vatLieu = colVatLieuIdx > 0 ? ws.Cell(r, colVatLieuIdx).GetString().Trim() : "";

                    if (layerMap.TryGetValue(layerName, out var existing))
                    {
                        if (!string.IsNullOrEmpty(cauKien)) existing.CauKien = cauKien;
                        if (!string.IsNullOrEmpty(vatLieu)) existing.VatLieu = vatLieu;
                        existing.IsSelected = true;
                        importedCount++;
                    }
                    else
                    {
                        // Nếu layer trong Excel chưa có trong bản vẽ, thêm mới để lưu mapping
                        var newItem = new LayerPropertyMapping
                        {
                            LayerName = layerName,
                            CauKien = cauKien,
                            VatLieu = vatLieu,
                            SolidCount = 0,
                            BodyCount = 0,
                            IsSelected = true
                        };
                        _allMappings.Add(newItem);
                        layerMap[layerName] = newItem;
                        importedCount++;
                    }
                }

                RefreshGrid();
                MessageBox.Show($"Đã nạp thành công cấu hình cho {importedCount} Layer từ file Excel!", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Lỗi khi nhập dữ liệu từ Excel: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnApply_Click(object? sender, EventArgs e)
        {
            string propSetName = txtPropSetName.Text.Trim();
            if (string.IsNullOrEmpty(propSetName))
            {
                MessageBox.Show("Vui lòng nhập Tên Property Set!", "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtPropSetName.Focus();
                return;
            }

            var selectedMappings = _allMappings.Where(x => x.IsSelected).ToList();
            if (selectedMappings.Count == 0)
            {
                MessageBox.Show("Bạn chưa chọn Layer nào để cập nhật Property Set!", "Cảnh báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SaveCurrentSettings();

            if (OnApplyPropertySet != null)
            {
                try
                {
                    OnApplyPropertySet(propSetName, selectedMappings);
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show($"Lỗi khi thực thi cập nhật Property Set: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}

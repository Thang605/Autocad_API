// (C) Copyright 2026 by T27
// Giao diện Form gán lệnh tắt (số thứ tự / tên lệnh) với Layer tương ứng trong AutoCAD
//
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Color = System.Drawing.Color;
using FlowDirection = System.Windows.Forms.FlowDirection;
using Font = System.Drawing.Font;
using WinFormsButton = System.Windows.Forms.Button;
using WinFormsCheckBox = System.Windows.Forms.CheckBox;
using WinFormsComboBox = System.Windows.Forms.ComboBox;
using WinFormsLabel = System.Windows.Forms.Label;

namespace MyFirstProject.Acad_Tool
{
    public class LayerCommandMappingItem
    {
        public string CommandName { get; set; } = "";
        public string LayerName { get; set; } = "";
        public int ColorIndex { get; set; } = 7;
        public string Linetype { get; set; } = "Continuous";
        public int Lineweight { get; set; } = 18;
    }

    public class GanLenhLayerForm : Form
    {
        // ─── Persistent State (Ghi nhớ thông số giữa các lần chạy) ───
        private static List<LayerCommandMappingItem> _savedMappings = new();
        private static List<ObjectId> _lastSelectedIds = new();
        private static bool _lastSetClayer = true;
        private static bool _lastResetColorByLayer = true;
        private static bool _lastCreateMissingLayers = true;
        private static Size _lastFormSize = new(900, 640);

        private static readonly string ConfigFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Civil3D_Tools",
            "GanLenhLayerConfig.json");

        private const string DefaultLspOutputPath = @"Z:\Z.FORM MAU LAM VIEC\1. BIM\1.MAU AUTOCAD\DoiLayerNhanh_1_15.lsp";

        private readonly Document _doc;
        private readonly List<ObjectId> _currentSelectedIds = new();
        private readonly List<string> _drawingLayers = new();
        private readonly Dictionary<string, (int ColorIndex, string Linetype, int Lineweight)> _drawingLayerProps =
            new(StringComparer.OrdinalIgnoreCase);

        // ─── Controls ───
        private DataGridView _dgv = null!;
        private DataGridViewComboBoxColumn _colLayerCombo = null!;
        private WinFormsLabel _lblSelectionStatus = null!;
        private WinFormsCheckBox _chkSetClayer = null!;
        private WinFormsCheckBox _chkResetColorByLayer = null!;
        private WinFormsCheckBox _chkCreateMissingLayers = null!;
        private ToolTip _toolTip = null!;
        private bool _isPopulatingGrid = false;

        public GanLenhLayerForm(Document doc, IEnumerable<ObjectId>? preSelectedIds = null)
        {
            _doc = doc;
            LoadDrawingLayers();
            InitializeComponent();
            RestoreLastSettings(preSelectedIds);
        }

        private void LoadDrawingLayers()
        {
            _drawingLayers.Clear();
            _drawingLayerProps.Clear();
            try
            {
                Database db = _doc.Database;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    if (tr.GetObject(db.LayerTableId, OpenMode.ForRead) is LayerTable lt)
                    {
                        foreach (ObjectId lid in lt)
                        {
                            if (tr.GetObject(lid, OpenMode.ForRead) is LayerTableRecord ltr && !ltr.IsErased)
                            {
                                _drawingLayers.Add(ltr.Name);
                                string ltypeName = "Continuous";
                                if (!ltr.LinetypeObjectId.IsNull &&
                                    tr.GetObject(ltr.LinetypeObjectId, OpenMode.ForRead) is LinetypeTableRecord ltRec)
                                {
                                    ltypeName = ltRec.Name;
                                }
                                int colIdx = Math.Max(1, Math.Min(255, (int)ltr.Color.ColorIndex));
                                int lw = (int)ltr.LineWeight;
                                _drawingLayerProps[ltr.Name] = (colIdx, ltypeName, lw);
                            }
                        }
                    }
                    tr.Commit();
                }
                _drawingLayers.Sort(StringComparer.OrdinalIgnoreCase);
            }
            catch { }
        }

        private void EnsureLayerOptionExists(string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName) || _colLayerCombo == null) return;
            foreach (object? item in _colLayerCombo.Items)
            {
                if (string.Equals(Convert.ToString(item), layerName, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
            _colLayerCombo.Items.Add(layerName);
        }

        private void RefreshComboColumnItems(IEnumerable<string>? extraNames = null)
        {
            if (_colLayerCombo == null) return;
            var allNames = new List<string>(_drawingLayers);
            if (extraNames != null)
            {
                foreach (string name in extraNames)
                {
                    if (!string.IsNullOrWhiteSpace(name) &&
                        !allNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                    {
                        allNames.Add(name);
                    }
                }
            }
            allNames.Sort(StringComparer.OrdinalIgnoreCase);

            _colLayerCombo.Items.Clear();
            foreach (string name in allNames)
            {
                _colLayerCombo.Items.Add(name);
            }
        }

        private void InitializeComponent()
        {
            _toolTip = new ToolTip();
            Text = "AT_Gan_Lenh_Layer — Bảng Gán Lệnh Tắt (Số Thứ Tự) Với Layer";
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            MinimumSize = new Size(820, 540);
            Size = _lastFormSize;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(245, 247, 250);

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(12)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 74f));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 86f));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48f));

            // ─── 1. Nhóm Chọn Đối Tượng Trên Bản Vẽ ───
            var grpSelect = new GroupBox
            {
                Text = "1. Đối tượng trên bản vẽ (Tùy chọn — dùng để đổi nhanh ngay trong Form)",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(25, 55, 109)
            };

            var btnPickObjects = new WinFormsButton
            {
                Text = "🎯 Chọn đối tượng trên CAD",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Location = new Point(14, 26),
                Size = new Size(210, 34),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnPickObjects.FlatAppearance.BorderSize = 0;
            btnPickObjects.Click += BtnPickObjects_Click;
            _toolTip.SetToolTip(btnPickObjects, "Tạm ẩn hộp thoại để quét chọn các đối tượng trên bản vẽ AutoCAD");

            _lblSelectionStatus = new WinFormsLabel
            {
                Text = "Chưa chọn đối tượng nào (có thể gõ lệnh trực tiếp trên CAD).",
                Font = new Font("Segoe UI", 9.2f, FontStyle.Regular),
                ForeColor = Color.FromArgb(60, 60, 60),
                Location = new Point(236, 33),
                AutoSize = true
            };

            grpSelect.Controls.Add(btnPickObjects);
            grpSelect.Controls.Add(_lblSelectionStatus);

            // ─── 2. Bảng Gán Lệnh Tắt ↔ Layer ───
            var grpGrid = new GroupBox
            {
                Text = "2. Bảng gán Tên Lệnh Tắt / Số Thứ Tự ↔ Chọn Layer trong bản vẽ (Dropdown ComboBox)",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(25, 55, 109)
            };

            var gridLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(6)
            };
            gridLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38f));
            gridLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var toolbarPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };

            var btnLoadDefault15 = CreateToolbarButton("📋 Nạp 15 Layer Chuẩn (0..15)", Color.FromArgb(16, 124, 65));
            btnLoadDefault15.Width = 210;
            btnLoadDefault15.Click += (s, e) => PopulateMappings(GetDefault15LayerMappings());
            _toolTip.SetToolTip(btnLoadDefault15, "Nạp bộ 15 Layer chuẩn hóa từ 01_TEXT đến 15_KHUNG_BANG");

            var btnSyncFromDwg = CreateToolbarButton("🔄 Lấy tất cả Layer bản vẽ", Color.FromArgb(70, 90, 120));
            btnSyncFromDwg.Width = 185;
            btnSyncFromDwg.Click += BtnSyncFromDwg_Click;
            _toolTip.SetToolTip(btnSyncFromDwg, "Đọc tất cả Layer đang có trong bản vẽ hiện hành và tự động đánh số lệnh tắt");

            var btnPickLayerFromEnt = CreateToolbarButton("👆 Pick Layer từ đối tượng", Color.FromArgb(130, 80, 160));
            btnPickLayerFromEnt.Width = 185;
            btnPickLayerFromEnt.Click += BtnPickLayerFromEnt_Click;
            _toolTip.SetToolTip(btnPickLayerFromEnt, "Click chọn 1 đối tượng trên bản vẽ để gán Layer của nó vào dòng đang chọn");

            var btnAddRow = CreateToolbarButton("➕ Thêm dòng", Color.FromArgb(55, 110, 165));
            btnAddRow.Width = 110;
            btnAddRow.Click += (s, e) =>
            {
                int nextIdx = _dgv.Rows.Count;
                string defaultLayer = _drawingLayers.FirstOrDefault() ?? "0";
                EnsureLayerOptionExists(defaultLayer);
                int colIdx = 7;
                string ltype = "Continuous";
                int lw = 18;
                if (_drawingLayerProps.TryGetValue(defaultLayer, out var props))
                {
                    colIdx = props.ColorIndex;
                    ltype = props.Linetype;
                    lw = props.Lineweight;
                }
                _dgv.Rows.Add(nextIdx.ToString(), defaultLayer, colIdx.ToString(), ltype, lw.ToString(), "⚡ Đổi ngay");
            };

            var btnDeleteRow = CreateToolbarButton("➖ Xóa dòng", Color.FromArgb(185, 55, 55));
            btnDeleteRow.Width = 105;
            btnDeleteRow.Click += (s, e) =>
            {
                if (_dgv.CurrentRow != null && !_dgv.CurrentRow.IsNewRow)
                {
                    _dgv.Rows.Remove(_dgv.CurrentRow);
                }
            };

            toolbarPanel.Controls.AddRange(new Control[]
            {
                btnLoadDefault15,
                btnSyncFromDwg,
                btnPickLayerFromEnt,
                btnAddRow,
                btnDeleteRow
            });

            _dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                RowHeadersWidth = 28,
                EditMode = DataGridViewEditMode.EditOnEnter,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                Font = new Font("Segoe UI", 9.2f, FontStyle.Regular)
            };
            _dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.2f, FontStyle.Bold);
            _dgv.ColumnHeadersHeight = 32;
            _dgv.RowTemplate.Height = 28;

            var colCmd = new DataGridViewTextBoxColumn
            {
                Name = "ColCommand",
                HeaderText = "Lệnh tắt (Gõ trên CAD)",
                FillWeight = 20f
            };

            // Cột Chọn Layer bằng Dropdown ComboBox chứa toàn bộ Layer trong bản vẽ
            _colLayerCombo = new DataGridViewComboBoxColumn
            {
                Name = "ColLayer",
                HeaderText = "Chọn Layer trong bản vẽ (Dropdown)",
                DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox,
                FlatStyle = FlatStyle.Standard,
                FillWeight = 40f
            };
            RefreshComboColumnItems();

            var colColor = new DataGridViewTextBoxColumn
            {
                Name = "ColColor",
                HeaderText = "Mã màu ACI",
                FillWeight = 14f
            };
            var colLtype = new DataGridViewTextBoxColumn
            {
                Name = "ColLinetype",
                HeaderText = "Kiểu nét (Linetype)",
                FillWeight = 18f
            };
            var colLw = new DataGridViewTextBoxColumn
            {
                Name = "ColLineweight",
                HeaderText = "Độ dày (1/100mm)",
                FillWeight = 16f
            };
            var colAction = new DataGridViewButtonColumn
            {
                Name = "ColApplyNow",
                HeaderText = "Thao tác nhanh",
                Text = "⚡ Đổi ngay",
                UseColumnTextForButtonValue = true,
                FillWeight = 18f
            };

            _dgv.Columns.AddRange(colCmd, _colLayerCombo, colColor, colLtype, colLw, colAction);
            _dgv.DataError += (s, e) => { e.ThrowException = false; };
            _dgv.EditingControlShowing += Dgv_EditingControlShowing;
            _dgv.CellValidating += Dgv_CellValidating;
            _dgv.CellValueChanged += Dgv_CellValueChanged;
            _dgv.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (_dgv.IsCurrentCellDirty && _dgv.CurrentCell is DataGridViewComboBoxCell)
                {
                    _dgv.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };
            _dgv.CellContentClick += Dgv_CellContentClick;

            gridLayout.Controls.Add(toolbarPanel, 0, 0);
            gridLayout.Controls.Add(_dgv, 0, 1);
            grpGrid.Controls.Add(gridLayout);

            // ─── 3. Tùy Chọn Thực Thi ───
            var grpOptions = new GroupBox
            {
                Text = "3. Tùy chọn khi gõ lệnh tắt hoặc bấm Đổi ngay",
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(25, 55, 109)
            };

            _chkSetClayer = new WinFormsCheckBox
            {
                Text = "Đặt làm Layer hiện hành (CLAYER) khi chưa chọn đối tượng",
                Font = new Font("Segoe UI", 9.2f, FontStyle.Regular),
                Location = new Point(16, 26),
                AutoSize = true,
                Checked = true
            };

            _chkResetColorByLayer = new WinFormsCheckBox
            {
                Text = "Đưa màu sắc đối tượng về ByLayer khi chuyển Layer",
                Font = new Font("Segoe UI", 9.2f, FontStyle.Regular),
                Location = new Point(430, 26),
                AutoSize = true,
                Checked = true
            };

            _chkCreateMissingLayers = new WinFormsCheckBox
            {
                Text = "Tự động tạo Layer (đúng màu, kiểu nét, độ dày) nếu bản vẽ chưa có",
                Font = new Font("Segoe UI", 9.2f, FontStyle.Regular),
                Location = new Point(16, 52),
                AutoSize = true,
                Checked = true
            };

            grpOptions.Controls.AddRange(new Control[] { _chkSetClayer, _chkResetColorByLayer, _chkCreateMissingLayers });

            // ─── 4. Thanh Nút Bấm Cuối Form ───
            var bottomPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0, 4, 0, 0)
            };

            var btnClose = new WinFormsButton
            {
                Text = "Đóng",
                Size = new Size(105, 36),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
                DialogResult = DialogResult.Cancel,
                Cursor = Cursors.Hand
            };

            var btnSaveAndRegister = new WinFormsButton
            {
                Text = "✅ Lưu & Nạp Lệnh Tắt Vào AutoCAD",
                Size = new Size(275, 36),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnSaveAndRegister.FlatAppearance.BorderSize = 0;
            btnSaveAndRegister.Click += BtnSaveAndRegister_Click;

            bottomPanel.Controls.Add(btnClose);
            bottomPanel.Controls.Add(btnSaveAndRegister);

            AcceptButton = btnSaveAndRegister;
            CancelButton = btnClose;

            mainLayout.Controls.Add(grpSelect, 0, 0);
            mainLayout.Controls.Add(grpGrid, 0, 1);
            mainLayout.Controls.Add(grpOptions, 0, 2);
            mainLayout.Controls.Add(bottomPanel, 0, 3);

            Controls.Add(mainLayout);

            FormClosing += (s, e) => SaveCurrentSettings();
        }

        private void Dgv_EditingControlShowing(object? sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (_dgv.CurrentCell?.ColumnIndex == _dgv.Columns["ColLayer"]?.Index && e.Control is WinFormsComboBox cb)
            {
                // Cho phép vừa xổ danh sách tất cả Layer trong bản vẽ vừa gõ tìm nhanh (AutoComplete) hoặc nhập tên mới
                cb.DropDownStyle = ComboBoxStyle.DropDown;
                cb.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                cb.AutoCompleteSource = AutoCompleteSource.ListItems;
            }
        }

        private void Dgv_CellValidating(object? sender, DataGridViewCellValidatingEventArgs e)
        {
            if (e.ColumnIndex == _dgv.Columns["ColLayer"]?.Index)
            {
                string formattedVal = Convert.ToString(e.FormattedValue)?.Trim() ?? "";
                if (!string.IsNullOrEmpty(formattedVal))
                {
                    EnsureLayerOptionExists(formattedVal);
                }
            }
        }

        private void Dgv_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (_isPopulatingGrid || e.RowIndex < 0) return;

            if (e.ColumnIndex == _dgv.Columns["ColLayer"]?.Index)
            {
                var row = _dgv.Rows[e.RowIndex];
                string selectedLayer = Convert.ToString(row.Cells["ColLayer"].Value)?.Trim() ?? "";
                if (!string.IsNullOrEmpty(selectedLayer) &&
                    _drawingLayerProps.TryGetValue(selectedLayer, out var props))
                {
                    row.Cells["ColColor"].Value = props.ColorIndex.ToString();
                    row.Cells["ColLinetype"].Value = props.Linetype;
                    row.Cells["ColLineweight"].Value = props.Lineweight.ToString();
                }
            }
        }

        private void BtnPickLayerFromEnt_Click(object? sender, EventArgs e)
        {
            Editor ed = _doc.Editor;
            string pickedLayer = "";
            using (EditorUserInteraction ui = ed.StartUserInteraction(this))
            {
                PromptEntityOptions peo = new("\nClick chọn một đối tượng trên bản vẽ để lấy Layer: ");
                PromptEntityResult per = ed.GetEntity(peo);
                if (per.Status == PromptStatus.OK && !per.ObjectId.IsNull)
                {
                    using (Transaction tr = _doc.Database.TransactionManager.StartTransaction())
                    {
                        if (tr.GetObject(per.ObjectId, OpenMode.ForRead) is Entity ent)
                        {
                            pickedLayer = ent.Layer;
                        }
                        tr.Commit();
                    }
                }
                ui.End();
            }

            if (!string.IsNullOrEmpty(pickedLayer))
            {
                EnsureLayerOptionExists(pickedLayer);
                if (_dgv.CurrentRow != null && !_dgv.CurrentRow.IsNewRow)
                {
                    _dgv.CurrentRow.Cells["ColLayer"].Value = pickedLayer;
                }
                else
                {
                    int nextIdx = _dgv.Rows.Count;
                    int colIdx = 7;
                    string ltype = "Continuous";
                    int lw = 18;
                    if (_drawingLayerProps.TryGetValue(pickedLayer, out var props))
                    {
                        colIdx = props.ColorIndex;
                        ltype = props.Linetype;
                        lw = props.Lineweight;
                    }
                    _dgv.Rows.Add(nextIdx.ToString(), pickedLayer, colIdx.ToString(), ltype, lw.ToString(), "⚡ Đổi ngay");
                }
            }
        }

        private static WinFormsButton CreateToolbarButton(string text, Color backColor)
        {
            var btn = new WinFormsButton
            {
                Text = text,
                Height = 30,
                Font = new Font("Segoe UI", 8.8f, FontStyle.Bold),
                BackColor = backColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 8, 0)
            };
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        public static List<LayerCommandMappingItem> GetDefault15LayerMappings()
        {
            return new List<LayerCommandMappingItem>
            {
                new() { CommandName = "0",  LayerName = "0",                ColorIndex = 7,   Linetype = "Continuous", Lineweight = -3 },
                new() { CommandName = "1",  LayerName = "01_TEXT",          ColorIndex = 7,   Linetype = "Continuous", Lineweight = 18 },
                new() { CommandName = "2",  LayerName = "02_TEXT_TO",       ColorIndex = 6,   Linetype = "Continuous", Lineweight = 18 },
                new() { CommandName = "3",  LayerName = "03_DIM",           ColorIndex = 11,  Linetype = "Continuous", Lineweight = 13 },
                new() { CommandName = "4",  LayerName = "04_BAO_BT",        ColorIndex = 3,   Linetype = "Continuous", Lineweight = 25 },
                new() { CommandName = "5",  LayerName = "05_BAO_COT_THEP",  ColorIndex = 4,   Linetype = "Continuous", Lineweight = 15 },
                new() { CommandName = "6",  LayerName = "06_THEP",          ColorIndex = 2,   Linetype = "Continuous", Lineweight = 35 },
                new() { CommandName = "7",  LayerName = "07_TRUC",          ColorIndex = 150, Linetype = "DASHDOT2",   Lineweight = 13 },
                new() { CommandName = "8",  LayerName = "08_KHUAT",         ColorIndex = 25,  Linetype = "HIDDEN2",    Lineweight = 13 },
                new() { CommandName = "9",  LayerName = "09_HATCH",         ColorIndex = 144, Linetype = "Continuous", Lineweight = 9  },
                new() { CommandName = "10", LayerName = "10_RANH_GIOI",     ColorIndex = 1,   Linetype = "BORDER2",    Lineweight = 70 },
                new() { CommandName = "11", LayerName = "11_VIEWPORT",      ColorIndex = 8,   Linetype = "Continuous", Lineweight = -3 },
                new() { CommandName = "12", LayerName = "12_KY_HIEU_BLOCK", ColorIndex = 4,   Linetype = "Continuous", Lineweight = 15 },
                new() { CommandName = "13", LayerName = "13_MAT_DAT_TN",    ColorIndex = 8,   Linetype = "DAT",        Lineweight = 13 },
                new() { CommandName = "14", LayerName = "14_HA_TANG_TALUY", ColorIndex = 30,  Linetype = "Continuous", Lineweight = 15 },
                new() { CommandName = "15", LayerName = "15_KHUNG_BANG",    ColorIndex = 7,   Linetype = "Continuous", Lineweight = 25 }
            };
        }

        private void PopulateMappings(List<LayerCommandMappingItem> items)
        {
            _isPopulatingGrid = true;
            try
            {
                LoadDrawingLayers();
                RefreshComboColumnItems(items.Select(x => x.LayerName));
                _dgv.Rows.Clear();
                foreach (var item in items)
                {
                    EnsureLayerOptionExists(item.LayerName);
                    _dgv.Rows.Add(
                        item.CommandName,
                        item.LayerName,
                        item.ColorIndex.ToString(),
                        item.Linetype,
                        item.Lineweight.ToString(),
                        "⚡ Đổi ngay");
                }
            }
            finally
            {
                _isPopulatingGrid = false;
            }
        }

        private List<LayerCommandMappingItem> ReadCurrentMappingsFromGrid()
        {
            var list = new List<LayerCommandMappingItem>();
            foreach (DataGridViewRow row in _dgv.Rows)
            {
                if (row.IsNewRow) continue;
                string cmd = Convert.ToString(row.Cells["ColCommand"].Value)?.Trim() ?? "";
                string lyr = Convert.ToString(row.Cells["ColLayer"].Value)?.Trim() ?? "";
                if (string.IsNullOrEmpty(cmd) || string.IsNullOrEmpty(lyr)) continue;

                int col = int.TryParse(Convert.ToString(row.Cells["ColColor"].Value), out int c) ? c : 7;
                string ltype = Convert.ToString(row.Cells["ColLinetype"].Value)?.Trim() ?? "Continuous";
                int lw = int.TryParse(Convert.ToString(row.Cells["ColLineweight"].Value), out int w) ? w : 18;

                list.Add(new LayerCommandMappingItem
                {
                    CommandName = cmd,
                    LayerName = lyr,
                    ColorIndex = col,
                    Linetype = string.IsNullOrEmpty(ltype) ? "Continuous" : ltype,
                    Lineweight = lw
                });
            }
            return list;
        }

        private void RestoreLastSettings(IEnumerable<ObjectId>? preSelectedIds)
        {
            _chkSetClayer.Checked = _lastSetClayer;
            _chkResetColorByLayer.Checked = _lastResetColorByLayer;
            _chkCreateMissingLayers.Checked = _lastCreateMissingLayers;

            // Khôi phục danh sách đối tượng đã chọn
            _currentSelectedIds.Clear();
            var sourceIds = preSelectedIds != null && preSelectedIds.Any() ? preSelectedIds : _lastSelectedIds;
            foreach (ObjectId id in sourceIds)
            {
                if (!id.IsNull && id.IsValid && !id.IsErased)
                {
                    _currentSelectedIds.Add(id);
                }
            }
            UpdateSelectionStatusLabel();

            // Khôi phục bảng mapping từ bộ nhớ phiên hoặc file JSON hoặc mặc định 15 layer
            if (_savedMappings.Count == 0 && File.Exists(ConfigFilePath))
            {
                try
                {
                    string json = File.ReadAllText(ConfigFilePath, Encoding.UTF8);
                    var loaded = JsonSerializer.Deserialize<List<LayerCommandMappingItem>>(json);
                    if (loaded != null && loaded.Count > 0)
                    {
                        _savedMappings = loaded;
                    }
                }
                catch { }
            }

            if (_savedMappings.Count == 0)
            {
                _savedMappings = GetDefault15LayerMappings();
            }

            PopulateMappings(_savedMappings);
        }

        private void SaveCurrentSettings()
        {
            _savedMappings = ReadCurrentMappingsFromGrid();
            _lastSelectedIds = new List<ObjectId>(_currentSelectedIds);
            _lastSetClayer = _chkSetClayer.Checked;
            _lastResetColorByLayer = _chkResetColorByLayer.Checked;
            _lastCreateMissingLayers = _chkCreateMissingLayers.Checked;
            if (WindowState == FormWindowState.Normal)
            {
                _lastFormSize = Size;
            }

            try
            {
                string? dir = Path.GetDirectoryName(ConfigFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                string json = JsonSerializer.Serialize(_savedMappings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigFilePath, json, Encoding.UTF8);
            }
            catch { }
        }

        private void UpdateSelectionStatusLabel()
        {
            if (_currentSelectedIds.Count > 0)
            {
                _lblSelectionStatus.Text = $"✅ Đã chọn {_currentSelectedIds.Count} đối tượng (bấm '⚡ Đổi ngay' ở dòng bất kỳ để chuyển Layer).";
                _lblSelectionStatus.ForeColor = Color.FromArgb(16, 124, 65);
            }
            else
            {
                _lblSelectionStatus.Text = "Chưa chọn đối tượng (bấm '⚡ Đổi ngay' sẽ đặt Layer đó làm Layer hiện hành CLAYER).";
                _lblSelectionStatus.ForeColor = Color.FromArgb(80, 80, 80);
            }
        }

        private void BtnPickObjects_Click(object? sender, EventArgs e)
        {
            Editor ed = _doc.Editor;
            using (EditorUserInteraction ui = ed.StartUserInteraction(this))
            {
                PromptSelectionOptions pso = new()
                {
                    MessageForAdding = "\nChọn các đối tượng cần đổi Layer nhanh: "
                };
                PromptSelectionResult psr = ed.GetSelection(pso);
                if (psr.Status == PromptStatus.OK && psr.Value != null)
                {
                    _currentSelectedIds.Clear();
                    foreach (SelectedObject so in psr.Value)
                    {
                        if (so != null && !so.ObjectId.IsNull && so.ObjectId.IsValid && !so.ObjectId.IsErased)
                        {
                            _currentSelectedIds.Add(so.ObjectId);
                        }
                    }
                }
                ui.End();
            }
            UpdateSelectionStatusLabel();
        }

        private void BtnSyncFromDwg_Click(object? sender, EventArgs e)
        {
            var items = new List<LayerCommandMappingItem>();
            try
            {
                LoadDrawingLayers();
                Database db = _doc.Database;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    if (tr.GetObject(db.LayerTableId, OpenMode.ForRead) is LayerTable lt)
                    {
                        var records = new List<LayerTableRecord>();
                        foreach (ObjectId lid in lt)
                        {
                            if (tr.GetObject(lid, OpenMode.ForRead) is LayerTableRecord ltr && !ltr.IsErased)
                            {
                                if (ltr.Name.Equals("Defpoints", StringComparison.OrdinalIgnoreCase)) continue;
                                records.Add(ltr);
                            }
                        }
                        records.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

                        int autoIdx = 1;
                        foreach (var ltr in records)
                        {
                            string cmd = ltr.Name == "0" ? "0" : ExtractLeadingNumberOrNext(ltr.Name, ref autoIdx);
                            string ltypeName = "Continuous";
                            if (!ltr.LinetypeObjectId.IsNull && tr.GetObject(ltr.LinetypeObjectId, OpenMode.ForRead) is LinetypeTableRecord ltRec)
                            {
                                ltypeName = ltRec.Name;
                            }
                            items.Add(new LayerCommandMappingItem
                            {
                                CommandName = cmd,
                                LayerName = ltr.Name,
                                ColorIndex = Math.Max(1, Math.Min(255, (int)ltr.Color.ColorIndex)),
                                Linetype = ltypeName,
                                Lineweight = (int)ltr.LineWeight
                            });
                        }
                    }
                    tr.Commit();
                }
                if (items.Count > 0)
                {
                    PopulateMappings(items);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi đọc Layer bản vẽ: {ex.Message}", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static string ExtractLeadingNumberOrNext(string layerName, ref int fallbackIndex)
        {
            int i = 0;
            while (i < layerName.Length && char.IsDigit(layerName[i]))
            {
                i++;
            }
            if (i > 0 && int.TryParse(layerName.Substring(0, i), out int parsed))
            {
                if (parsed >= fallbackIndex) fallbackIndex = parsed + 1;
                return parsed.ToString();
            }
            return (fallbackIndex++).ToString();
        }

        private void Dgv_CellContentClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != _dgv.Columns["ColApplyNow"]?.Index) return;

            var row = _dgv.Rows[e.RowIndex];
            string lyrName = Convert.ToString(row.Cells["ColLayer"].Value)?.Trim() ?? "";
            if (string.IsNullOrEmpty(lyrName)) return;

            int colorIdx = int.TryParse(Convert.ToString(row.Cells["ColColor"].Value), out int c) ? c : 7;
            string ltype = Convert.ToString(row.Cells["ColLinetype"].Value)?.Trim() ?? "Continuous";
            int lw = int.TryParse(Convert.ToString(row.Cells["ColLineweight"].Value), out int w) ? w : 18;

            ApplyLayerDirectly(lyrName, colorIdx, ltype, lw);
        }

        private void ApplyLayerDirectly(string layerName, int colorIdx, string linetype, int lineweight)
        {
            try
            {
                using (DocumentLock docLock = _doc.LockDocument())
                {
                    Database db = _doc.Database;
                    int changedCount = 0;

                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                        ObjectId targetLayerId = ObjectId.Null;

                        if (lt.Has(layerName))
                        {
                            targetLayerId = lt[layerName];
                        }
                        else if (_chkCreateMissingLayers.Checked)
                        {
                            lt.UpgradeOpen();
                            var newLtr = new LayerTableRecord
                            {
                                Name = layerName,
                                Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(ColorMethod.ByAci, (short)Math.Max(1, Math.Min(255, colorIdx)))
                            };
                            if (Enum.IsDefined(typeof(LineWeight), lineweight))
                            {
                                newLtr.LineWeight = (LineWeight)lineweight;
                            }
                            var ltt = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
                            if (ltt.Has(linetype))
                            {
                                newLtr.LinetypeObjectId = ltt[linetype];
                            }
                            if (layerName.Equals("11_VIEWPORT", StringComparison.OrdinalIgnoreCase))
                            {
                                newLtr.IsPlottable = false;
                            }
                            targetLayerId = lt.Add(newLtr);
                            tr.AddNewlyCreatedDBObject(newLtr, true);
                        }

                        if (!targetLayerId.IsNull)
                        {
                            if (_currentSelectedIds.Count > 0)
                            {
                                foreach (ObjectId id in _currentSelectedIds)
                                {
                                    if (id.IsNull || !id.IsValid || id.IsErased) continue;
                                    if (tr.GetObject(id, OpenMode.ForWrite) is Entity ent)
                                    {
                                        ent.LayerId = targetLayerId;
                                        if (_chkResetColorByLayer.Checked)
                                        {
                                            ent.ColorIndex = 256; // ByLayer
                                        }
                                        changedCount++;
                                    }
                                }
                            }
                            if (_chkSetClayer.Checked || changedCount == 0)
                            {
                                db.Clayer = targetLayerId;
                            }
                        }

                        tr.Commit();
                    }

                    _doc.Editor.Regen();
                    if (changedCount > 0)
                    {
                        _doc.Editor.WriteMessage($"\n✅ [AT_Gan_Lenh_Layer] Đã chuyển {changedCount} đối tượng sang Layer '{layerName}'.");
                        _lblSelectionStatus.Text = $"✅ Đã chuyển {changedCount} đối tượng sang Layer '{layerName}' (CLAYER = {layerName}).";
                    }
                    else
                    {
                        _doc.Editor.WriteMessage($"\n✅ [AT_Gan_Lenh_Layer] Đã đặt Layer hiện hành (CLAYER) = '{layerName}'.");
                        _lblSelectionStatus.Text = $"✅ Đã đặt Layer hiện hành (CLAYER) = '{layerName}'.";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi áp dụng Layer '{layerName}': {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnSaveAndRegister_Click(object? sender, EventArgs e)
        {
            SaveCurrentSettings();
            var mappings = _savedMappings;
            if (mappings.Count == 0)
            {
                MessageBox.Show("Bảng gán lệnh đang trống. Vui lòng thêm ít nhất 1 dòng.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                if (_chkCreateMissingLayers.Checked)
                {
                    using (DocumentLock docLock = _doc.LockDocument())
                    {
                        Database db = _doc.Database;
                        using (Transaction tr = db.TransactionManager.StartTransaction())
                        {
                            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForWrite);
                            var ltt = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);

                            foreach (var m in mappings)
                            {
                                if (string.IsNullOrWhiteSpace(m.LayerName)) continue;
                                if (!lt.Has(m.LayerName))
                                {
                                    var ltr = new LayerTableRecord
                                    {
                                        Name = m.LayerName,
                                        Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(ColorMethod.ByAci, (short)Math.Max(1, Math.Min(255, m.ColorIndex)))
                                    };
                                    if (Enum.IsDefined(typeof(LineWeight), m.Lineweight))
                                    {
                                        ltr.LineWeight = (LineWeight)m.Lineweight;
                                    }
                                    if (ltt.Has(m.Linetype))
                                    {
                                        ltr.LinetypeObjectId = ltt[m.Linetype];
                                    }
                                    if (m.LayerName.Equals("11_VIEWPORT", StringComparison.OrdinalIgnoreCase))
                                    {
                                        ltr.IsPlottable = false;
                                    }
                                    lt.Add(ltr);
                                    tr.AddNewlyCreatedDBObject(ltr, true);
                                }
                            }
                            tr.Commit();
                        }
                    }
                }

                string lispSingleLine = BuildSingleLineLisp(mappings);
                string lispFormatted = BuildFormattedLispFile(mappings);

                try
                {
                    string? lspDir = Path.GetDirectoryName(DefaultLspOutputPath);
                    if (!string.IsNullOrEmpty(lspDir) && Directory.Exists(lspDir))
                    {
                        File.WriteAllText(DefaultLspOutputPath, lispFormatted, Encoding.UTF8);
                    }
                }
                catch { }

                _doc.SendStringToExecute(lispSingleLine + "\n", true, false, false);
                _doc.Editor.WriteMessage($"\n✅ [AT_Gan_Lenh_Layer] Đã lưu và nạp {mappings.Count} lệnh tắt đổi Layer nhanh vào AutoCAD!");

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi đăng ký lệnh tắt: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public static string BuildSingleLineLisp(List<LayerCommandMappingItem> mappings)
        {
            var sb = new StringBuilder();
            sb.Append("(progn (vl-load-com) ");
            sb.Append("(defun _SetOrChangeLayer (lyrName lyrColor lyrLtype lyrLw / doc layers lobj ss i vobj) ");
            sb.Append("(setq doc (vla-get-ActiveDocument (vlax-get-acad-object))) ");
            sb.Append("(setq layers (vla-get-Layers doc)) ");
            sb.Append("(if (vl-catch-all-error-p (setq lobj (vl-catch-all-apply 'vla-Item (list layers lyrName)))) ");
            sb.Append("(progn (setq lobj (vla-Add layers lyrName)) (vla-put-Color lobj lyrColor) (vla-put-Lineweight lobj lyrLw) ");
            sb.Append("(if (not (vl-catch-all-error-p (vl-catch-all-apply 'vla-Item (list (vla-get-Linetypes doc) lyrLtype)))) (vla-put-Linetype lobj lyrLtype)) ");
            sb.Append("(if (= lyrName \"11_VIEWPORT\") (vla-put-Plottable lobj :vlax-false)))) ");
            sb.Append("(setq ss (ssget \"_I\")) ");
            sb.Append("(if ss (progn (setq i 0) (while (< i (sslength ss)) (setq vobj (vlax-ename->vla-object (ssname ss i))) (vl-catch-all-apply 'vla-put-Layer (list vobj lyrName)) (vl-catch-all-apply 'vla-put-Color (list vobj 256)) (setq i (1+ i))) (sssetfirst nil nil) (princ (strcat \"\\n[Layer Nhanh] Da chuyen \" (itoa (sslength ss)) \" doi tuong sang layer: \" lyrName))) ");
            sb.Append("(progn (setvar \"CLAYER\" lyrName) (princ (strcat \"\\n[Layer Nhanh] CLAYER -> \" lyrName \". Chon doi tuong can doi (Enter de bo qua): \")) (setq ss (ssget)) (if ss (progn (setq i 0) (while (< i (sslength ss)) (setq vobj (vlax-ename->vla-object (ssname ss i))) (vl-catch-all-apply 'vla-put-Layer (list vobj lyrName)) (vl-catch-all-apply 'vla-put-Color (list vobj 256)) (setq i (1+ i))) (princ (strcat \"\\n[Layer Nhanh] Da chuyen \" (itoa (sslength ss)) \" doi tuong sang layer: \" lyrName)))))) (princ)) ");

            foreach (var m in mappings)
            {
                string cleanCmd = m.CommandName.Trim();
                string cleanLyr = m.LayerName.Replace("\"", "");
                string cleanLtype = m.Linetype.Replace("\"", "");
                sb.Append($"(defun c:{cleanCmd} () (_SetOrChangeLayer \"{cleanLyr}\" {m.ColorIndex} \"{cleanLtype}\" {m.Lineweight})) ");
                if (int.TryParse(cleanCmd, out int num) && num >= 1 && num <= 9 && cleanCmd.Length == 1)
                {
                    sb.Append($"(defun c:0{num} () (_SetOrChangeLayer \"{cleanLyr}\" {m.ColorIndex} \"{cleanLtype}\" {m.Lineweight})) ");
                }
            }

            sb.Append("(princ \"\\n[AT_Gan_Lenh_Layer] Da cap nhat bang lenh tat Layer thanh cong!\\n\") (princ))");
            return sb.ToString();
        }

        private static string BuildFormattedLispFile(List<LayerCommandMappingItem> mappings)
        {
            var sb = new StringBuilder();
            sb.AppendLine(";; File sinh tự động bởi lệnh AT_Gan_Lenh_Layer");
            sb.AppendLine("(vl-load-com)");
            sb.AppendLine(BuildSingleLineLisp(mappings));
            return sb.ToString();
        }
    }
}

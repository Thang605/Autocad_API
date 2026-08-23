// (C) Copyright 2024-2026 by T27
// Lệnh đánh số thứ tự cho Block theo vị trí
// Hỗ trợ cấu hình số bắt đầu, tổng số bản vẽ, tiền tố, ký tự phân cách
//
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

// AutoCAD
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;

// Aliases
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsTextBox = System.Windows.Forms.TextBox;
using WinFormsButton = System.Windows.Forms.Button;
using WinFormsComboBox = System.Windows.Forms.ComboBox;
using WinFormsCheckBox = System.Windows.Forms.CheckBox;
using WinFormsNumericUpDown = System.Windows.Forms.NumericUpDown;
using DrawingFont = System.Drawing.Font;

[assembly: CommandClass(typeof(Civil3DCsharp.AT_DanhSoThuTu_ChoBlock))]

namespace Civil3DCsharp
{
    /// <summary>
    /// Thứ tự sắp xếp block
    /// </summary>
    public enum BlockSortOrder
    {
        TopToBottom_LeftToRight,    // Trên→Dưới, Trái→Phải (mặc định)
        LeftToRight_TopToBottom,    // Trái→Phải, Trên→Dưới
        BottomToTop_LeftToRight,    // Dưới→Trên, Trái→Phải
        LeftToRight_BottomToTop     // Trái→Phải, Dưới→Trên
    }

    /// <summary>
    /// Class chứa thông tin Block để sắp xếp
    /// </summary>
    public class BlockInfo
    {
        public ObjectId ObjectId { get; set; }
        public Point3d Position { get; set; }
        public string BlockName { get; set; }
        public int Index { get; set; }

        public BlockInfo(ObjectId objId, Point3d pos, string name)
        {
            ObjectId = objId;
            Position = pos;
            BlockName = name;
            Index = 0;
        }
    }

    /// <summary>
    /// Form cấu hình đánh số thứ tự cho Block
    /// </summary>
    public class DanhSoBlockForm : Form
    {
        // Controls
        private WinFormsLabel lblBlockName;
        private WinFormsTextBox txtBlockName;
        private WinFormsButton btnPickBlock;
        private WinFormsLabel lblBlockCount;
        private WinFormsButton btnSelectBlocks;

        private WinFormsLabel lblAttributeTag;
        private WinFormsTextBox txtAttributeTag;

        private WinFormsLabel lblSortOrder;
        private WinFormsComboBox cmbSortOrder;

        private WinFormsLabel lblStartNumber;
        private WinFormsNumericUpDown numStartNumber;
        private WinFormsLabel lblTotalCount;
        private WinFormsNumericUpDown numTotalCount;
        private WinFormsCheckBox chkAutoTotal;

        private WinFormsLabel lblPrefix;
        private WinFormsTextBox txtPrefix;
        private WinFormsLabel lblSeparator;
        private WinFormsTextBox txtSeparator;
        private WinFormsCheckBox chkShowTotal;
        private WinFormsLabel lblPreview;

        private WinFormsButton btnOK;
        private WinFormsButton btnCancel;

        // Properties
        public string BlockName { get; set; } = "";
        public string AttributeTag { get; set; } = "NUMBER";
        public BlockSortOrder SortOrder { get; set; } = BlockSortOrder.TopToBottom_LeftToRight;
        public int StartNumber { get; set; } = 1;
        public int TotalCount { get; set; } = 1;
        public bool AutoTotal { get; set; } = true;
        public string Prefix { get; set; } = "";
        public string Separator { get; set; } = "/";
        public bool ShowTotal { get; set; } = true;
        public List<ObjectId> SelectedBlockIds { get; set; } = new List<ObjectId>();
        public bool FormAccepted { get; private set; } = false;

        // Static để lưu giá trị giữa các lần gọi
        public static string LastBlockName { get; set; } = "";
        private static string _lastAttributeTag = "NUMBER";
        private static BlockSortOrder _lastSortOrder = BlockSortOrder.TopToBottom_LeftToRight;
        private static int _lastStartNumber = 1;
        private static int _lastTotalCount = 1;
        private static bool _lastAutoTotal = true;
        private static string _lastPrefix = "";
        private static string _lastSeparator = "/";
        private static bool _lastShowTotal = true;

        private Editor _editor;

        public DanhSoBlockForm(Editor editor, string blockName)
        {
            _editor = editor;
            BlockName = blockName;
            InitializeComponent();
            LoadLastValues();
            UpdatePreview();
        }

        private void InitializeComponent()
        {
            this.Text = "🔢 Đánh Số Thứ Tự Block";
            this.Size = new Size(430, 460);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            int y = 15;
            int labelWidth = 110;
            int controlLeft = 125;

            // 1. Block Name
            lblBlockName = new WinFormsLabel
            {
                Text = "Tên Block:",
                Location = new Point(15, y + 3),
                Size = new Size(labelWidth, 20)
            };

            txtBlockName = new WinFormsTextBox
            {
                Location = new Point(controlLeft, y),
                Size = new Size(160, 23),
                Text = BlockName,
                ReadOnly = true,
                BackColor = Color.LightGray
            };

            // Button Đổi Block (pick block mới) - nằm cạnh textbox
            btnPickBlock = new WinFormsButton
            {
                Text = "...",
                Location = new Point(290, y),
                Size = new Size(35, 23)
            };
            btnPickBlock.Click += BtnPickBlock_Click;

            lblBlockCount = new WinFormsLabel
            {
                Text = "Số block: 0",
                Location = new Point(330, y + 3),
                Size = new Size(80, 20),
                ForeColor = Color.Blue
            };

            this.Controls.Add(lblBlockName);
            this.Controls.Add(txtBlockName);
            this.Controls.Add(btnPickBlock);
            this.Controls.Add(lblBlockCount);

            y += 35;

            // 2. Button Select Blocks
            btnSelectBlocks = new WinFormsButton
            {
                Text = "📍 Quét chọn các Block",
                Location = new Point(controlLeft, y),
                Size = new Size(160, 28)
            };
            btnSelectBlocks.Click += BtnSelectBlocks_Click;
            this.Controls.Add(btnSelectBlocks);

            y += 40;

            // 3. Attribute Tag
            lblAttributeTag = new WinFormsLabel
            {
                Text = "Tên Attribute:",
                Location = new Point(15, y + 3),
                Size = new Size(labelWidth, 20)
            };

            txtAttributeTag = new WinFormsTextBox
            {
                Location = new Point(controlLeft, y),
                Size = new Size(160, 23),
                Text = "NUMBER"
            };
            txtAttributeTag.TextChanged += (s, e) => UpdatePreview();

            this.Controls.Add(lblAttributeTag);
            this.Controls.Add(txtAttributeTag);

            y += 35;

            // 4. Sort Order
            lblSortOrder = new WinFormsLabel
            {
                Text = "Thứ tự sắp xếp:",
                Location = new Point(15, y + 3),
                Size = new Size(labelWidth, 20)
            };

            cmbSortOrder = new WinFormsComboBox
            {
                Location = new Point(controlLeft, y),
                Size = new Size(270, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbSortOrder.Items.Add("Trên→Dưới, Trái→Phải");
            cmbSortOrder.Items.Add("Trái→Phải, Trên→Dưới");
            cmbSortOrder.Items.Add("Dưới→Trên, Trái→Phải");
            cmbSortOrder.Items.Add("Trái→Phải, Dưới→Trên");
            cmbSortOrder.SelectedIndex = 0;

            this.Controls.Add(lblSortOrder);
            this.Controls.Add(cmbSortOrder);

            y += 40;

            // 5. Format & Numbering GroupBox
            var grpFormat = new GroupBox
            {
                Text = "📝 Cấu hình số thứ tự & Định dạng",
                Location = new Point(15, y),
                Size = new Size(385, 165)
            };

            // Row 1: Số bắt đầu & Tổng số bản vẽ
            lblStartNumber = new WinFormsLabel
            {
                Text = "Số bắt đầu:",
                Location = new Point(10, 25),
                Size = new Size(75, 20)
            };

            numStartNumber = new WinFormsNumericUpDown
            {
                Location = new Point(88, 22),
                Size = new Size(60, 23),
                Minimum = 1,
                Maximum = 999999,
                Value = 1
            };
            numStartNumber.ValueChanged += (s, e) =>
            {
                RecalculateAutoTotal();
                UpdatePreview();
            };

            lblTotalCount = new WinFormsLabel
            {
                Text = "Tổng số BV:",
                Location = new Point(160, 25),
                Size = new Size(75, 20)
            };

            numTotalCount = new WinFormsNumericUpDown
            {
                Location = new Point(238, 22),
                Size = new Size(60, 23),
                Minimum = 1,
                Maximum = 999999,
                Value = 1
            };
            numTotalCount.ValueChanged += (s, e) => UpdatePreview();

            chkAutoTotal = new WinFormsCheckBox
            {
                Text = "Tự động",
                Location = new Point(305, 23),
                Size = new Size(75, 20),
                Checked = true
            };
            chkAutoTotal.CheckedChanged += (s, e) =>
            {
                numTotalCount.Enabled = !chkAutoTotal.Checked && chkShowTotal.Checked;
                RecalculateAutoTotal();
                UpdatePreview();
            };

            grpFormat.Controls.Add(lblStartNumber);
            grpFormat.Controls.Add(numStartNumber);
            grpFormat.Controls.Add(lblTotalCount);
            grpFormat.Controls.Add(numTotalCount);
            grpFormat.Controls.Add(chkAutoTotal);

            // Row 2: Tiền tố & Ký tự ngăn
            lblPrefix = new WinFormsLabel
            {
                Text = "Tiền tố:",
                Location = new Point(10, 58),
                Size = new Size(75, 20)
            };

            txtPrefix = new WinFormsTextBox
            {
                Location = new Point(88, 55),
                Size = new Size(60, 23),
                Text = ""
            };
            txtPrefix.TextChanged += (s, e) => UpdatePreview();

            lblSeparator = new WinFormsLabel
            {
                Text = "Ký tự ngăn:",
                Location = new Point(160, 58),
                Size = new Size(75, 20)
            };

            txtSeparator = new WinFormsTextBox
            {
                Location = new Point(238, 55),
                Size = new Size(60, 23),
                Text = "/",
                TextAlign = HorizontalAlignment.Center
            };
            txtSeparator.TextChanged += (s, e) => UpdatePreview();

            grpFormat.Controls.Add(lblPrefix);
            grpFormat.Controls.Add(txtPrefix);
            grpFormat.Controls.Add(lblSeparator);
            grpFormat.Controls.Add(txtSeparator);

            // Row 3: Checkbox Hiển thị tổng số
            chkShowTotal = new WinFormsCheckBox
            {
                Text = "Hiển thị tổng số (VD: 1/10)",
                Location = new Point(10, 92),
                Size = new Size(200, 20),
                Checked = true
            };
            chkShowTotal.CheckedChanged += (s, e) =>
            {
                bool show = chkShowTotal.Checked;
                lblTotalCount.Enabled = show;
                chkAutoTotal.Enabled = show;
                numTotalCount.Enabled = show && !chkAutoTotal.Checked;
                lblSeparator.Enabled = show;
                txtSeparator.Enabled = show;
                UpdatePreview();
            };
            grpFormat.Controls.Add(chkShowTotal);

            // Row 4: Preview
            lblPreview = new WinFormsLabel
            {
                Text = "Xem trước: 1/10",
                Location = new Point(10, 125),
                Size = new Size(365, 25),
                ForeColor = Color.DarkGreen,
                Font = new DrawingFont("Segoe UI", 9.5f, FontStyle.Bold)
            };
            grpFormat.Controls.Add(lblPreview);

            this.Controls.Add(grpFormat);

            y += 180;

            // 6. OK / Cancel Buttons
            btnOK = new WinFormsButton
            {
                Text = "✅ Đánh số",
                Location = new Point(110, y),
                Size = new Size(100, 32),
                DialogResult = DialogResult.OK
            };
            btnOK.Click += BtnOK_Click;

            btnCancel = new WinFormsButton
            {
                Text = "❌ Hủy",
                Location = new Point(220, y),
                Size = new Size(95, 32),
                DialogResult = DialogResult.Cancel
            };

            this.Controls.Add(btnOK);
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;
        }

        private void RecalculateAutoTotal()
        {
            if (chkAutoTotal.Checked)
            {
                int start = (int)numStartNumber.Value;
                int count = SelectedBlockIds.Count;
                int total = count > 0 ? (start + count - 1) : start;
                if (total < 1) total = 1;
                numTotalCount.Value = Math.Min(999999, Math.Max(1, total));
            }
        }

        private void LoadLastValues()
        {
            txtAttributeTag.Text = _lastAttributeTag;
            cmbSortOrder.SelectedIndex = Math.Min(Math.Max(0, (int)_lastSortOrder), cmbSortOrder.Items.Count - 1);
            numStartNumber.Value = Math.Max(1, _lastStartNumber);
            chkAutoTotal.Checked = _lastAutoTotal;
            numTotalCount.Value = Math.Max(1, _lastTotalCount);
            txtPrefix.Text = _lastPrefix;
            txtSeparator.Text = _lastSeparator;
            chkShowTotal.Checked = _lastShowTotal;

            bool show = chkShowTotal.Checked;
            lblTotalCount.Enabled = show;
            chkAutoTotal.Enabled = show;
            numTotalCount.Enabled = show && !_lastAutoTotal;
            lblSeparator.Enabled = show;
            txtSeparator.Enabled = show;
        }

        private void UpdatePreview()
        {
            int startNum = (int)numStartNumber.Value;
            int totalNum = (int)numTotalCount.Value;
            string prefix = txtPrefix.Text;
            string separator = txtSeparator.Text;
            bool showTotal = chkShowTotal.Checked;

            string preview;
            if (showTotal)
            {
                preview = $"{prefix}{startNum}{separator}{totalNum}";
            }
            else
            {
                preview = $"{prefix}{startNum}";
            }

            lblPreview.Text = $"Xem trước: {preview}";
        }

        private void BtnSelectBlocks_Click(object sender, EventArgs e)
        {
            this.Hide();

            try
            {
                Document doc = Application.DocumentManager.MdiActiveDocument;
                Database db = doc.Database;
                Editor ed = doc.Editor;

                // Tạo filter để chọn block theo tên
                TypedValue[] filterList = new TypedValue[]
                {
                    new TypedValue((int)DxfCode.Start, "INSERT"),
                    new TypedValue((int)DxfCode.BlockName, BlockName)
                };
                SelectionFilter filter = new SelectionFilter(filterList);

                PromptSelectionOptions pso = new PromptSelectionOptions();
                pso.MessageForAdding = $"\n📍 Quét chọn các Block '{BlockName}' cần đánh số:";
                pso.AllowDuplicates = false;

                PromptSelectionResult psr = ed.GetSelection(pso, filter);

                if (psr.Status == PromptStatus.OK)
                {
                    SelectionSet ss = psr.Value;
                    SelectedBlockIds.Clear();

                    foreach (SelectedObject so in ss)
                    {
                        if (so != null)
                        {
                            SelectedBlockIds.Add(so.ObjectId);
                        }
                    }

                    lblBlockCount.Text = $"Số block: {SelectedBlockIds.Count}";
                    RecalculateAutoTotal();
                    UpdatePreview();
                    ed.WriteMessage($"\n✅ Đã chọn {SelectedBlockIds.Count} block '{BlockName}'");
                }
            }
            catch (System.Exception ex)
            {
                _editor.WriteMessage($"\n❌ Lỗi: {ex.Message}");
            }
            finally
            {
                this.Show();
                this.BringToFront();
                this.Focus();
            }
        }

        private void BtnPickBlock_Click(object sender, EventArgs e)
        {
            this.Hide();

            try
            {
                Document doc = Application.DocumentManager.MdiActiveDocument;
                Database db = doc.Database;
                Editor ed = doc.Editor;

                PromptEntityOptions peo = new PromptEntityOptions("\n📍 Chọn Block mẫu mới:");
                peo.SetRejectMessage("\n⚠️ Vui lòng chọn Block!");
                peo.AddAllowedClass(typeof(BlockReference), true);

                PromptEntityResult per = ed.GetEntity(peo);

                if (per.Status == PromptStatus.OK)
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        BlockReference blkRef = tr.GetObject(per.ObjectId, OpenMode.ForRead) as BlockReference;
                        if (blkRef != null)
                        {
                            BlockTableRecord btr = tr.GetObject(blkRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                            if (btr != null)
                            {
                                BlockName = btr.Name;
                                txtBlockName.Text = BlockName;
                                LastBlockName = BlockName;  // Cập nhật block đã nhớ
                                SelectedBlockIds.Clear();   // Reset danh sách block đã chọn
                                lblBlockCount.Text = "Số block: 0";
                                RecalculateAutoTotal();
                                UpdatePreview();
                                ed.WriteMessage($"\n✅ Đã đổi Block mẫu: {BlockName}");
                            }
                        }
                        tr.Commit();
                    }
                }
            }
            catch (System.Exception ex)
            {
                _editor.WriteMessage($"\n❌ Lỗi: {ex.Message}");
            }
            finally
            {
                this.Show();
                this.BringToFront();
                this.Focus();
            }
        }

        private void BtnOK_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(BlockName))
            {
                MessageBox.Show("Vui lòng chọn Block mẫu bằng nút ...!", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (SelectedBlockIds.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn các block cần đánh số!", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtAttributeTag.Text))
            {
                MessageBox.Show("Vui lòng nhập tên Attribute!", "Thông báo",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtAttributeTag.Focus();
                return;
            }

            StartNumber = (int)numStartNumber.Value;
            TotalCount = (int)numTotalCount.Value;
            AutoTotal = chkAutoTotal.Checked;
            AttributeTag = txtAttributeTag.Text.Trim();
            SortOrder = (BlockSortOrder)cmbSortOrder.SelectedIndex;
            Prefix = txtPrefix.Text;
            Separator = txtSeparator.Text;
            ShowTotal = chkShowTotal.Checked;

            // Kiểm tra cảnh báo nếu tổng số nhỏ hơn số thứ tự kết thúc
            int lastIndex = StartNumber + SelectedBlockIds.Count - 1;
            if (ShowTotal && TotalCount < lastIndex)
            {
                var confirm = MessageBox.Show(
                    $"Tổng số bản vẽ ({TotalCount}) nhỏ hơn số bản vẽ cuối cùng ({lastIndex}).\nBạn có chắc chắn muốn tiếp tục?",
                    "Cảnh báo số lượng",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (confirm != DialogResult.Yes)
                {
                    return;
                }
            }

            // Save for next session
            _lastStartNumber = StartNumber;
            _lastTotalCount = TotalCount;
            _lastAutoTotal = AutoTotal;
            _lastAttributeTag = AttributeTag;
            _lastSortOrder = SortOrder;
            _lastPrefix = Prefix;
            _lastSeparator = Separator;
            _lastShowTotal = ShowTotal;

            FormAccepted = true;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }

    /// <summary>
    /// Command class đánh số thứ tự cho Block
    /// </summary>
    public class AT_DanhSoThuTu_ChoBlock
    {
        /// <summary>
        /// Lệnh đánh số thứ tự cho các Block cùng tên
        /// Số thứ tự được ghi vào attribute với format tùy chỉnh
        /// </summary>
        [CommandMethod("AT_DanhSoThuTu_ChoBlock")]
        public static void DanhSoThuTuChoBlock()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            try
            {
                ed.WriteMessage("\n=== 🔢 ĐÁNH SỐ THỨ TỰ CHO BLOCK - AT_DanhSoThuTu_ChoBlock ===");

                // Sử dụng block đã nhớ (nếu có), hoặc để trống cho người dùng chọn trong form
                string blockName = DanhSoBlockForm.LastBlockName ?? "";
                
                if (!string.IsNullOrEmpty(blockName))
                {
                    ed.WriteMessage($"\n✅ Block mẫu: {blockName}");
                }
                else
                {
                    ed.WriteMessage("\n📍 Chọn block mẫu trong form...");
                }

                // Mở form trực tiếp
                using (var form = new DanhSoBlockForm(ed, blockName))
                {
                    var result = Application.ShowModalDialog(form);

                    if (result != DialogResult.OK || !form.FormAccepted)
                    {
                        ed.WriteMessage("\n❌ Đã hủy lệnh.");
                        return;
                    }

                    // 3. Thu thập thông tin các block đã chọn
                    List<BlockInfo> blockInfos = new List<BlockInfo>();

                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        foreach (ObjectId objId in form.SelectedBlockIds)
                        {
                            BlockReference blkRef = tr.GetObject(objId, OpenMode.ForRead) as BlockReference;
                            if (blkRef != null)
                            {
                                blockInfos.Add(new BlockInfo(objId, blkRef.Position, blockName));
                            }
                        }
                        tr.Commit();
                    }

                    if (blockInfos.Count == 0)
                    {
                        ed.WriteMessage("\n❌ Không tìm thấy block hợp lệ nào!");
                        return;
                    }

                    // 4. Sắp xếp theo thứ tự đã chọn
                    blockInfos = SortBlocks(blockInfos, form.SortOrder);

                    // 5. Đánh số thứ tự theo số bắt đầu được chỉ định
                    int startNumber = form.StartNumber;
                    int totalDrawings = form.TotalCount;

                    for (int i = 0; i < blockInfos.Count; i++)
                    {
                        blockInfos[i].Index = startNumber + i;
                    }

                    ed.WriteMessage($"\n📊 Thứ tự sắp xếp ({GetSortOrderName(form.SortOrder)}):");
                    foreach (var info in blockInfos)
                    {
                        if (form.ShowTotal)
                            ed.WriteMessage($"\n   {info.Index}/{totalDrawings}: X={info.Position.X:F2}, Y={info.Position.Y:F2}");
                        else
                            ed.WriteMessage($"\n   {info.Index}: X={info.Position.X:F2}, Y={info.Position.Y:F2}");
                    }

                    // 6. Cập nhật attribute cho từng block
                    int successCount = 0;
                    int failCount = 0;

                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        foreach (var info in blockInfos)
                        {
                            try
                            {
                                BlockReference blkRef = tr.GetObject(info.ObjectId, OpenMode.ForWrite) as BlockReference;
                                if (blkRef != null)
                                {
                                    bool foundAttribute = false;

                                    // Duyệt qua các attribute của block
                                    foreach (ObjectId attId in blkRef.AttributeCollection)
                                    {
                                        AttributeReference attRef = tr.GetObject(attId, OpenMode.ForWrite) as AttributeReference;
                                        if (attRef != null && attRef.Tag.Equals(form.AttributeTag, StringComparison.OrdinalIgnoreCase))
                                        {
                                            // Tạo giá trị theo format đã chọn
                                            string newValue = FormatNumber(info.Index, totalDrawings, form.Prefix, form.Separator, form.ShowTotal);
                                            attRef.TextString = newValue;
                                            foundAttribute = true;
                                            successCount++;
                                            break;
                                        }
                                    }

                                    if (!foundAttribute)
                                    {
                                        ed.WriteMessage($"\n⚠️ Block tại ({info.Position.X:F2}, {info.Position.Y:F2}) không có attribute '{form.AttributeTag}'");
                                        failCount++;
                                    }
                                }
                            }
                            catch (System.Exception ex)
                            {
                                ed.WriteMessage($"\n⚠️ Lỗi cập nhật block {info.Index}: {ex.Message}");
                                failCount++;
                            }
                        }

                        tr.Commit();
                    }

                    // 7. Thông báo kết quả
                    ed.WriteMessage($"\n\n🎉 Hoàn thành!");
                    ed.WriteMessage($"\n   ✅ Cập nhật thành công: {successCount} block");
                    if (failCount > 0)
                    {
                        ed.WriteMessage($"\n   ⚠️ Không cập nhật được: {failCount} block");
                    }
                    string exampleFormat = FormatNumber(startNumber, totalDrawings, form.Prefix, form.Separator, form.ShowTotal);
                    ed.WriteMessage($"\n   📝 Format số thứ tự: {exampleFormat}");
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n❌ Lỗi: {ex.Message}");
                ed.WriteMessage($"\n   Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Sắp xếp danh sách block theo thứ tự đã chọn
        /// </summary>
        private static List<BlockInfo> SortBlocks(List<BlockInfo> blocks, BlockSortOrder sortOrder)
        {
            switch (sortOrder)
            {
                case BlockSortOrder.TopToBottom_LeftToRight:
                    // Trên→Dưới, Trái→Phải (Y giảm, X tăng)
                    return blocks
                        .OrderByDescending(b => Math.Round(b.Position.Y, 2))
                        .ThenBy(b => Math.Round(b.Position.X, 2))
                        .ToList();

                case BlockSortOrder.LeftToRight_TopToBottom:
                    // Trái→Phải, Trên→Dưới (X tăng, Y giảm)
                    return blocks
                        .OrderBy(b => Math.Round(b.Position.X, 2))
                        .ThenByDescending(b => Math.Round(b.Position.Y, 2))
                        .ToList();

                case BlockSortOrder.BottomToTop_LeftToRight:
                    // Dưới→Trên, Trái→Phải (Y tăng, X tăng)
                    return blocks
                        .OrderBy(b => Math.Round(b.Position.Y, 2))
                        .ThenBy(b => Math.Round(b.Position.X, 2))
                        .ToList();

                case BlockSortOrder.LeftToRight_BottomToTop:
                    // Trái→Phải, Dưới→Trên (X tăng, Y tăng)
                    return blocks
                        .OrderBy(b => Math.Round(b.Position.X, 2))
                        .ThenBy(b => Math.Round(b.Position.Y, 2))
                        .ToList();

                default:
                    return blocks;
            }
        }

        /// <summary>
        /// Lấy tên thứ tự sắp xếp
        /// </summary>
        private static string GetSortOrderName(BlockSortOrder sortOrder)
        {
            switch (sortOrder)
            {
                case BlockSortOrder.TopToBottom_LeftToRight:
                    return "Trên→Dưới, Trái→Phải";
                case BlockSortOrder.LeftToRight_TopToBottom:
                    return "Trái→Phải, Trên→Dưới";
                case BlockSortOrder.BottomToTop_LeftToRight:
                    return "Dưới→Trên, Trái→Phải";
                case BlockSortOrder.LeftToRight_BottomToTop:
                    return "Trái→Phải, Dưới→Trên";
                default:
                    return "";
            }
        }

        /// <summary>
        /// Format số thứ tự theo cấu hình
        /// </summary>
        private static string FormatNumber(int index, int total, string prefix, string separator, bool showTotal)
        {
            if (showTotal)
            {
                return $"{prefix}{index}{separator}{total}";
            }
            else
            {
                return $"{prefix}{index}";
            }
        }
    }
}

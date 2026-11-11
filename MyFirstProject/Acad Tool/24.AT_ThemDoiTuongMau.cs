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

[assembly: CommandClass(typeof(Civil3DCsharp.ThemDoiTuongMauCmd))]

namespace Civil3DCsharp
{
    /// <summary>
    /// Form giao diện cho lệnh ThemDoiTuongMau
    /// </summary>
    public class ThemDoiTuongMauForm : Form
    {
        // Nhớ đường dẫn file mẫu đã chọn giữa các lần chạy
        private static string _lastTemplatePath = "";

        // UI Controls
        private Label lblTitle = null!;
        private Label lblTemplatePath = null!;
        private TextBox txtTemplatePath = null!;
        private Button btnBrowse = null!;
        
        private GroupBox grpCategories = null!;
        private CheckBox chkLayers = null!;
        private CheckBox chkTextStyles = null!;
        private CheckBox chkDimStyles = null!;
        private CheckBox chkMLeaderStyles = null!;
        private CheckBox chkTableStyles = null!;
        private CheckBox chkLinetypes = null!;
        private CheckBox chkBlocks = null!;

        private GroupBox grpClash = null!;
        private RadioButton radReplace = null!;
        private RadioButton radIgnore = null!;

        private GroupBox grpLog = null!;
        private TextBox txtLog = null!;

        private Button btnExecute = null!;
        private Button btnClose = null!;

        public ThemDoiTuongMauForm()
        {
            InitializeComponent();
            RestoreLastValues();
        }

        private void InitializeComponent()
        {
            var standardFont = new DrawingFont("Segoe UI", 9.5F, FontStyle.Regular);
            var boldFont = new DrawingFont("Segoe UI", 9.5F, FontStyle.Bold);
            var titleFont = new DrawingFont("Segoe UI", 13.5F, FontStyle.Bold);

            this.SuspendLayout();

            // Form settings
            this.Text = "Thêm Đối Tượng từ Bản Vẽ Mẫu";
            this.ClientSize = new Size(540, 560);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Font = standardFont;

            // Title
            lblTitle = new Label
            {
                Text = "THÊM ĐỐI TƯỢNG TỪ BẢN VẼ MẪU",
                Font = titleFont,
                Location = new Point(15, 15),
                Size = new Size(510, 28),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(0, 102, 204)
            };

            // File path label & textbox & button
            lblTemplatePath = new Label
            {
                Text = "File bản vẽ mẫu (.dwg):",
                Font = boldFont,
                Location = new Point(20, 55),
                Size = new Size(180, 20)
            };

            txtTemplatePath = new TextBox
            {
                Location = new Point(20, 78),
                Size = new Size(390, 24),
                ReadOnly = false
            };

            btnBrowse = new Button
            {
                Text = "Chọn file...",
                Location = new Point(420, 77),
                Size = new Size(100, 26),
                Font = standardFont
            };
            btnBrowse.Click += BtnBrowse_Click;

            // Categories Group Box
            grpCategories = new GroupBox
            {
                Text = "Các đối tượng muốn import",
                Font = boldFont,
                Location = new Point(20, 115),
                Size = new Size(500, 130),
                ForeColor = Color.DarkSlateGray
            };

            chkLayers = new CheckBox
            {
                Text = "Layers (Lớp)",
                Location = new Point(20, 25),
                Size = new Size(140, 22),
                Font = standardFont,
                Checked = true
            };

            chkTextStyles = new CheckBox
            {
                Text = "Text Styles (Kiểu chữ)",
                Location = new Point(170, 25),
                Size = new Size(150, 22),
                Font = standardFont,
                Checked = true
            };

            chkDimStyles = new CheckBox
            {
                Text = "Dim Styles (Kiểu kích thước)",
                Location = new Point(330, 25),
                Size = new Size(160, 22),
                Font = standardFont,
                Checked = true
            };

            chkMLeaderStyles = new CheckBox
            {
                Text = "MLeader Styles (Ghi chú)",
                Location = new Point(20, 60),
                Size = new Size(140, 22),
                Font = standardFont,
                Checked = true
            };

            chkTableStyles = new CheckBox
            {
                Text = "Table Styles (Kiểu bảng)",
                Location = new Point(170, 60),
                Size = new Size(150, 22),
                Font = standardFont,
                Checked = true
            };

            chkLinetypes = new CheckBox
            {
                Text = "Linetypes (Đường nét)",
                Location = new Point(330, 60),
                Size = new Size(160, 22),
                Font = standardFont,
                Checked = true
            };

            chkBlocks = new CheckBox
            {
                Text = "Blocks (Định nghĩa khối)",
                Location = new Point(20, 95),
                Size = new Size(200, 22),
                Font = standardFont,
                Checked = true
            };

            grpCategories.Controls.AddRange(new Control[] {
                chkLayers, chkTextStyles, chkDimStyles,
                chkMLeaderStyles, chkTableStyles, chkLinetypes,
                chkBlocks
            });

            // Clash options Group Box
            grpClash = new GroupBox
            {
                Text = "Tùy chọn trùng tên",
                Font = boldFont,
                Location = new Point(20, 255),
                Size = new Size(500, 65),
                ForeColor = Color.DarkSlateGray
            };

            radReplace = new RadioButton
            {
                Text = "Ghi đè / Cập nhật (Replace)",
                Location = new Point(20, 25),
                Size = new Size(210, 22),
                Font = standardFont,
                Checked = true
            };

            radIgnore = new RadioButton
            {
                Text = "Bỏ qua đối tượng đã có (Ignore)",
                Location = new Point(250, 25),
                Size = new Size(230, 22),
                Font = standardFont
            };

            grpClash.Controls.AddRange(new Control[] { radReplace, radIgnore });

            // Log Group Box
            grpLog = new GroupBox
            {
                Text = "Nhật ký thực hiện",
                Font = boldFont,
                Location = new Point(20, 330),
                Size = new Size(500, 160),
                ForeColor = Color.DarkSlateGray
            };

            txtLog = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Location = new Point(15, 25),
                Size = new Size(470, 120),
                Font = new DrawingFont("Consolas", 9F, FontStyle.Regular),
                BackColor = Color.White
            };
            grpLog.Controls.Add(txtLog);

            // Execute Button
            btnExecute = new Button
            {
                Text = "Thực hiện",
                Location = new Point(310, 505),
                Size = new Size(100, 35),
                Font = boldFont,
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnExecute.Click += BtnExecute_Click;

            // Close Button
            btnClose = new Button
            {
                Text = "Đóng",
                Location = new Point(420, 505),
                Size = new Size(100, 35),
                Font = standardFont
            };
            btnClose.Click += BtnClose_Click;

            // Add all to form
            this.Controls.AddRange(new Control[] {
                lblTitle, lblTemplatePath, txtTemplatePath, btnBrowse,
                grpCategories, grpClash, grpLog, btnExecute, btnClose
            });

            this.AcceptButton = btnExecute;
            this.CancelButton = btnClose;

            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private void RestoreLastValues()
        {
            if (!string.IsNullOrEmpty(_lastTemplatePath) && File.Exists(_lastTemplatePath))
            {
                txtTemplatePath.Text = _lastTemplatePath;
            }
            else
            {
                // Tìm thư mục mặc định từ tài liệu hiện tại hoặc thư viện
                try
                {
                    Document doc = Application.DocumentManager.MdiActiveDocument;
                    if (doc != null && !string.IsNullOrEmpty(doc.Database.Filename))
                    {
                        string currentDir = Path.GetDirectoryName(doc.Database.Filename);
                        if (!string.IsNullOrEmpty(currentDir))
                        {
                            // Xem xét thư mục X:\0.ThuVienTK hoặc đường dẫn tương tự nếu tồn tại
                            string defaultLib = @"X:\0.ThuVienTK\0.ThuVienCty";
                            if (Directory.Exists(defaultLib))
                            {
                                txtTemplatePath.Text = defaultLib;
                            }
                            else
                            {
                                txtTemplatePath.Text = currentDir;
                            }
                        }
                    }
                }
                catch { }
            }
        }

        private void BtnBrowse_Click(object? sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "Chọn file AutoCAD chứa đối tượng mẫu";
                ofd.Filter = "AutoCAD Drawing (*.dwg)|*.dwg|Template AutoCAD (*.dwt)|*.dwt";
                ofd.FilterIndex = 1;
                ofd.RestoreDirectory = true;

                if (!string.IsNullOrEmpty(txtTemplatePath.Text))
                {
                    if (File.Exists(txtTemplatePath.Text))
                    {
                        ofd.InitialDirectory = Path.GetDirectoryName(txtTemplatePath.Text);
                        ofd.FileName = Path.GetFileName(txtTemplatePath.Text);
                    }
                    else if (Directory.Exists(txtTemplatePath.Text))
                    {
                        ofd.InitialDirectory = txtTemplatePath.Text;
                    }
                }

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    txtTemplatePath.Text = ofd.FileName;
                    _lastTemplatePath = ofd.FileName;
                }
            }
        }

        private void Log(string message)
        {
            txtLog.AppendText(message + Environment.NewLine);
        }

        private void BtnExecute_Click(object? sender, EventArgs e)
        {
            string templatePath = txtTemplatePath.Text.Trim();
            if (string.IsNullOrEmpty(templatePath) || !File.Exists(templatePath))
            {
                MessageBox.Show("Vui lòng chọn một file bản vẽ mẫu (.dwg/.dwt) hợp lệ!", "Lỗi",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnBrowse.Focus();
                return;
            }

            // Lưu lại đường dẫn thành công
            _lastTemplatePath = templatePath;

            txtLog.Clear();
            Log($"=== BẮT ĐẦU IMPORT TỪ: {Path.GetFileName(templatePath)} ===");

            int successCount = 0;
            int skipCount = 0;
            List<string> detailLogs = new List<string>();

            // Lấy tùy chọn trùng lặp
            DuplicateRecordCloning duplicateBehavior = radReplace.Checked 
                ? DuplicateRecordCloning.Replace 
                : DuplicateRecordCloning.Ignore;

            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database destDb = doc.Database;
            Editor ed = doc.Editor;

            try
            {
                using (DocumentLock docLock = doc.LockDocument())
                {
                    using (Database sourceDb = new Database(false, true))
                    {
                        // Đọc file bản vẽ mẫu
                        sourceDb.ReadDwgFile(templatePath, FileShare.Read, true, "");

                        // 1. Import Layers
                        if (chkLayers.Checked)
                        {
                            ImportTableRecords<LayerTable, LayerTableRecord>(
                                sourceDb, destDb, db => db.LayerTableId, 
                                duplicateBehavior, "Layer", ref successCount, ref skipCount, detailLogs);
                        }

                        // 2. Import Text Styles
                        if (chkTextStyles.Checked)
                        {
                            ImportTableRecords<TextStyleTable, TextStyleTableRecord>(
                                sourceDb, destDb, db => db.TextStyleTableId, 
                                duplicateBehavior, "TextStyle", ref successCount, ref skipCount, detailLogs);
                        }

                        // 3. Import Dim Styles
                        if (chkDimStyles.Checked)
                        {
                            ImportTableRecords<DimStyleTable, DimStyleTableRecord>(
                                sourceDb, destDb, db => db.DimStyleTableId, 
                                duplicateBehavior, "DimStyle", ref successCount, ref skipCount, detailLogs);
                        }

                        // 4. Import Linetypes
                        if (chkLinetypes.Checked)
                        {
                            ImportTableRecords<LinetypeTable, LinetypeTableRecord>(
                                sourceDb, destDb, db => db.LinetypeTableId, 
                                duplicateBehavior, "Linetype", ref successCount, ref skipCount, detailLogs);
                        }

                        // 5. Import Blocks
                        if (chkBlocks.Checked)
                        {
                            ImportTableRecords<BlockTable, BlockTableRecord>(
                                sourceDb, destDb, db => db.BlockTableId, 
                                duplicateBehavior, "Block", ref successCount, ref skipCount, detailLogs);
                        }

                        // 6. Import MLeader Styles
                        if (chkMLeaderStyles.Checked)
                        {
                            ImportDictionaryEntries(
                                sourceDb, destDb, "ACAD_MLEADERSTYLE", 
                                duplicateBehavior, ref successCount, ref skipCount, detailLogs);
                        }

                        // 7. Import Table Styles
                        if (chkTableStyles.Checked)
                        {
                            ImportDictionaryEntries(
                                sourceDb, destDb, "ACAD_TABLESTYLE", 
                                duplicateBehavior, ref successCount, ref skipCount, detailLogs);
                        }
                    }
                }

                // Xuất chi tiết nhật ký vào TextBox
                foreach (string logMsg in detailLogs)
                {
                    Log(logMsg);
                }

                Log("");
                Log("=== KẾT THÚC QUÁ TRÌNH ===");
                Log($"Import thành công/cập nhật: {successCount} đối tượng.");
                if (skipCount > 0)
                {
                    Log($"Bỏ qua: {skipCount} đối tượng trùng tên.");
                }

                ed.Regen();
                
                MessageBox.Show($"Hoàn thành import đối tượng từ file mẫu!\n- Thành công/Cập nhật: {successCount}\n- Bỏ qua: {skipCount}", 
                    "Kết quả", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (System.Exception ex)
            {
                Log($"LỖI hệ thống: {ex.Message}");
                MessageBox.Show($"Có lỗi xảy ra: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void ImportTableRecords<TTable, TRecord>(
            Database sourceDb, 
            Database destDb, 
            Func<Database, ObjectId> getTableIdFunc, 
            DuplicateRecordCloning duplicateBehavior,
            string categoryName,
            ref int successCount, 
            ref int skipCount, 
            List<string> logs) 
            where TTable : SymbolTable 
            where TRecord : SymbolTableRecord
        {
            try
            {
                using (Transaction trSource = sourceDb.TransactionManager.StartTransaction())
                {
                    ObjectId sourceTableId = getTableIdFunc(sourceDb);
                    TTable sourceTable = trSource.GetObject(sourceTableId, OpenMode.ForRead) as TTable;
                    if (sourceTable == null) return;

                    using (Transaction trDest = destDb.TransactionManager.StartTransaction())
                    {
                        ObjectId destTableId = getTableIdFunc(destDb);
                        TTable destTable = trDest.GetObject(destTableId, OpenMode.ForRead) as TTable;
                        if (destTable == null) return;

                        ObjectIdCollection idsToClone = new ObjectIdCollection();
                        foreach (ObjectId recordId in sourceTable)
                        {
                            TRecord record = trSource.GetObject(recordId, OpenMode.ForRead) as TRecord;
                            if (record == null) continue;

                            string name = record.Name;

                            // Bỏ qua các đối tượng tiêu chuẩn hệ thống
                            if (IsStandardRecord(name, categoryName)) continue;

                            // Đối với Block, lọc bỏ ModelSpace, PaperSpace, Layout Blocks
                            if (categoryName == "Block")
                            {
                                BlockTableRecord? btr = record as BlockTableRecord;
                                if (btr != null && (btr.IsLayout || name.StartsWith("*")))
                                    continue;
                            }

                            if (destTable.Has(name))
                            {
                                if (duplicateBehavior == DuplicateRecordCloning.Ignore)
                                {
                                    skipCount++;
                                    logs.Add($"[Bỏ qua] {categoryName}: '{name}' đã có trong bản vẽ.");
                                    continue;
                                }
                                else
                                {
                                    logs.Add($"[Cập nhật] {categoryName}: '{name}'");
                                }
                            }
                            else
                            {
                                logs.Add($"[Thêm mới] {categoryName}: '{name}'");
                            }
                            idsToClone.Add(recordId);
                        }

                        if (idsToClone.Count > 0)
                        {
                            IdMapping idMap = new IdMapping();
                            sourceDb.WblockCloneObjects(idsToClone, destTableId, idMap, duplicateBehavior, false);
                            successCount += idsToClone.Count;
                        }

                        trDest.Commit();
                    }
                    trSource.Commit();
                }
            }
            catch (System.Exception ex)
            {
                logs.Add($"[Lỗi] Import {categoryName}: {ex.Message}");
            }
        }

        private static void ImportDictionaryEntries(
            Database sourceDb, 
            Database destDb, 
            string dictName, 
            DuplicateRecordCloning duplicateBehavior, 
            ref int successCount, 
            ref int skipCount, 
            List<string> logs)
        {
            string categoryLabel = dictName == "ACAD_MLEADERSTYLE" ? "MLeaderStyle" : "TableStyle";
            try
            {
                using (Transaction trSource = sourceDb.TransactionManager.StartTransaction())
                {
                    DBDictionary nodSource = trSource.GetObject(sourceDb.NamedObjectsDictionaryId, OpenMode.ForRead) as DBDictionary;
                    if (nodSource == null || !nodSource.Contains(dictName)) return;

                    ObjectId dictSourceId = nodSource.GetAt(dictName);
                    DBDictionary dictSource = trSource.GetObject(dictSourceId, OpenMode.ForRead) as DBDictionary;
                    if (dictSource == null) return;

                    using (Transaction trDest = destDb.TransactionManager.StartTransaction())
                    {
                        DBDictionary nodDest = trDest.GetObject(destDb.NamedObjectsDictionaryId, OpenMode.ForRead) as DBDictionary;
                        if (nodDest == null) return;

                        ObjectId dictDestId;
                        if (!nodDest.Contains(dictName))
                        {
                            nodDest.UpgradeOpen();
                            DBDictionary newDict = new DBDictionary();
                            dictDestId = nodDest.SetAt(dictName, newDict);
                            trDest.AddNewlyCreatedDBObject(newDict, true);
                        }
                        else
                        {
                            dictDestId = nodDest.GetAt(dictName);
                        }

                        DBDictionary dictDest = trDest.GetObject(dictDestId, OpenMode.ForRead) as DBDictionary;
                        if (dictDest == null) return;

                        ObjectIdCollection idsToClone = new ObjectIdCollection();
                        foreach (DBDictionaryEntry entry in dictSource)
                        {
                            string name = entry.Key;
                            if (name.Equals("Standard", StringComparison.OrdinalIgnoreCase)) continue;

                            if (dictDest.Contains(name))
                            {
                                if (duplicateBehavior == DuplicateRecordCloning.Ignore)
                                {
                                    skipCount++;
                                    logs.Add($"[Bỏ qua] {categoryLabel}: '{name}' đã có trong bản vẽ.");
                                    continue;
                                }
                                else
                                {
                                    logs.Add($"[Cập nhật] {categoryLabel}: '{name}'");
                                }
                            }
                            else
                            {
                                logs.Add($"[Thêm mới] {categoryLabel}: '{name}'");
                            }
                            idsToClone.Add(entry.Value);
                        }

                        if (idsToClone.Count > 0)
                        {
                            IdMapping idMap = new IdMapping();
                            sourceDb.WblockCloneObjects(idsToClone, dictDestId, idMap, duplicateBehavior, false);
                            successCount += idsToClone.Count;
                        }

                        trDest.Commit();
                    }
                    trSource.Commit();
                }
            }
            catch (System.Exception ex)
            {
                logs.Add($"[Lỗi] Import {categoryLabel}: {ex.Message}");
            }
        }

        private static bool IsStandardRecord(string name, string categoryName)
        {
            if (categoryName == "Layer")
            {
                return name.Equals("0", StringComparison.OrdinalIgnoreCase) || 
                       name.Equals("Defpoints", StringComparison.OrdinalIgnoreCase);
            }
            if (categoryName == "TextStyle")
            {
                return name.Equals("Standard", StringComparison.OrdinalIgnoreCase);
            }
            if (categoryName == "DimStyle")
            {
                return name.Equals("Standard", StringComparison.OrdinalIgnoreCase) || 
                       name.Equals("Annotative", StringComparison.OrdinalIgnoreCase);
            }
            if (categoryName == "Linetype")
            {
                return name.Equals("Continuous", StringComparison.OrdinalIgnoreCase) || 
                       name.Equals("ByLayer", StringComparison.OrdinalIgnoreCase) || 
                       name.Equals("ByBlock", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        private void BtnClose_Click(object? sender, EventArgs e)
        {
            this.Close();
        }
    }

    /// <summary>
    /// Định nghĩa lệnh trong AutoCAD
    /// </summary>
    public class ThemDoiTuongMauCmd
    {
        [CommandMethod("AT_Them_Doi_Tuong_Mau")]
        public static void AT_ThemDoiTuongMau()
        {
            try
            {
                using (var form = new ThemDoiTuongMauForm())
                {
                    Application.ShowModalDialog(form);
                }
            }
            catch (System.Exception ex)
            {
                Document doc = Application.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    doc.Editor.WriteMessage($"\nLỗi khi khởi chạy giao diện: {ex.Message}");
                }
            }
        }
    }
}

// (C) Copyright 2025 by T27
//
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.ApplicationServices;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(Civil3DCsharp.XrefAllFiles))]

namespace Civil3DCsharp
{
    /// <summary>
    /// Lệnh tạo Xref cho tất cả các file DWG trong thư mục và thư mục con
    /// </summary>
    public class XrefAllFiles
    {
        // Lưu trữ thư mục đã chọn lần trước
        private static string _lastSelectedFolder = "";

        /// <summary>
        /// Lệnh AT_XrefAll - Tạo xref cho tất cả file DWG trong thư mục được chọn (bao gồm thư mục con)
        /// </summary>
        [CommandMethod("AT_Xref_All")]
        public static void AT_XrefAll()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            try
            {
                // Mở hộp thoại chọn thư mục
                string selectedFolder = SelectFolder();
                if (string.IsNullOrEmpty(selectedFolder))
                {
                    ed.WriteMessage("\nĐã hủy lệnh.");
                    return;
                }

                // Tìm tất cả file DWG trong thư mục và thư mục con
                List<string> dwgFiles = GetAllDwgFiles(selectedFolder);

                if (dwgFiles.Count == 0)
                {
                    ed.WriteMessage($"\nKhông tìm thấy file DWG nào trong thư mục: {selectedFolder}");
                    return;
                }

                // Loại bỏ file hiện tại (nếu có) khỏi danh sách
                string currentFilePath = db.Filename;
                dwgFiles = dwgFiles.Where(f => !f.Equals(currentFilePath, StringComparison.OrdinalIgnoreCase)).ToList();

                ed.WriteMessage($"\nTìm thấy {dwgFiles.Count} file DWG.");

                // Hỏi người dùng chọn điểm chèn
                PromptPointResult ppr = ed.GetPoint("\nChọn điểm chèn Xref: ");
                if (ppr.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\nĐã hủy lệnh.");
                    return;
                }

                Point3d insertPoint = ppr.Value;

                // Hỏi người dùng về khoảng cách giữa các xref
                PromptDoubleOptions spacingOptions = new PromptDoubleOptions("\nNhập khoảng cách giữa các Xref (0 = chèn tất cả tại cùng một vị trí): ")
                {
                    DefaultValue = 0,
                    AllowNegative = false,
                    AllowZero = true
                };
                PromptDoubleResult spacingResult = ed.GetDouble(spacingOptions);
                double spacing = spacingResult.Status == PromptStatus.OK ? spacingResult.Value : 0;

                // Tạo xref cho từng file
                int successCount = 0;
                int failCount = 0;

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord modelSpace = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                    for (int i = 0; i < dwgFiles.Count; i++)
                    {
                        string dwgFile = dwgFiles[i];
                        try
                        {
                            // Tạo tên xref từ tên file (không có đường dẫn và extension)
                            string xrefName = Path.GetFileNameWithoutExtension(dwgFile);

                            // Kiểm tra xem xref đã tồn tại chưa
                            xrefName = GetUniqueXrefName(bt, xrefName);

                            // Attach xref
                            ObjectId xrefId = db.AttachXref(dwgFile, xrefName);

                            if (xrefId.IsValid)
                            {
                                // Tính toán điểm chèn
                                Point3d currentInsertPoint = new Point3d(
                                    insertPoint.X,
                                    insertPoint.Y - (i * spacing),
                                    insertPoint.Z
                                );

                                // Tạo BlockReference cho xref
                                using (BlockReference xrefRef = new BlockReference(currentInsertPoint, xrefId))
                                {
                                    modelSpace.AppendEntity(xrefRef);
                                    tr.AddNewlyCreatedDBObject(xrefRef, true);
                                }

                                successCount++;
                                ed.WriteMessage($"\n  + Đã tạo Xref: {xrefName}");
                            }
                            else
                            {
                                failCount++;
                                ed.WriteMessage($"\n  ! Không thể tạo Xref cho: {Path.GetFileName(dwgFile)}");
                            }
                        }
                        catch (System.Exception ex)
                        {
                            failCount++;
                            ed.WriteMessage($"\n  ! Lỗi khi tạo Xref cho {Path.GetFileName(dwgFile)}: {ex.Message}");
                        }
                    }

                    tr.Commit();
                }

                // Thông báo kết quả
                ed.WriteMessage($"\n\n=== KẾT QUẢ ===");
                ed.WriteMessage($"\nThành công: {successCount} file");
                if (failCount > 0)
                    ed.WriteMessage($"\nThất bại: {failCount} file");
                ed.WriteMessage($"\nTổng cộng: {dwgFiles.Count} file");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\nLỗi: {ex.Message}");
            }
        }

        /// <summary>
        /// Lệnh AT_XrefAllOverlay - Tạo xref overlay cho tất cả file DWG trong thư mục được chọn
        /// </summary>
        [CommandMethod("AT_Xref_All_Overlay")]
        public static void AT_XrefAllOverlay()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            try
            {
                // Mở hộp thoại chọn thư mục
                string selectedFolder = SelectFolder();
                if (string.IsNullOrEmpty(selectedFolder))
                {
                    ed.WriteMessage("\nĐã hủy lệnh.");
                    return;
                }

                // Tìm tất cả file DWG trong thư mục và thư mục con
                List<string> dwgFiles = GetAllDwgFiles(selectedFolder);

                if (dwgFiles.Count == 0)
                {
                    ed.WriteMessage($"\nKhông tìm thấy file DWG nào trong thư mục: {selectedFolder}");
                    return;
                }

                // Loại bỏ file hiện tại (nếu có) khỏi danh sách
                string currentFilePath = db.Filename;
                dwgFiles = dwgFiles.Where(f => !f.Equals(currentFilePath, StringComparison.OrdinalIgnoreCase)).ToList();

                ed.WriteMessage($"\nTìm thấy {dwgFiles.Count} file DWG.");

                // Hỏi người dùng chọn điểm chèn
                PromptPointResult ppr = ed.GetPoint("\nChọn điểm chèn Xref: ");
                if (ppr.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\nĐã hủy lệnh.");
                    return;
                }

                Point3d insertPoint = ppr.Value;

                // Tạo xref overlay cho từng file
                int successCount = 0;
                int failCount = 0;

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord modelSpace = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                    foreach (string dwgFile in dwgFiles)
                    {
                        try
                        {
                            // Tạo tên xref từ tên file
                            string xrefName = Path.GetFileNameWithoutExtension(dwgFile);
                            xrefName = GetUniqueXrefName(bt, xrefName);

                            // Overlay xref (thay vì Attach)
                            ObjectId xrefId = db.OverlayXref(dwgFile, xrefName);

                            if (xrefId.IsValid)
                            {
                                // Tạo BlockReference cho xref
                                using (BlockReference xrefRef = new BlockReference(insertPoint, xrefId))
                                {
                                    modelSpace.AppendEntity(xrefRef);
                                    tr.AddNewlyCreatedDBObject(xrefRef, true);
                                }

                                successCount++;
                                ed.WriteMessage($"\n  + Đã tạo Xref Overlay: {xrefName}");
                            }
                            else
                            {
                                failCount++;
                                ed.WriteMessage($"\n  ! Không thể tạo Xref Overlay cho: {Path.GetFileName(dwgFile)}");
                            }
                        }
                        catch (System.Exception ex)
                        {
                            failCount++;
                            ed.WriteMessage($"\n  ! Lỗi khi tạo Xref Overlay cho {Path.GetFileName(dwgFile)}: {ex.Message}");
                        }
                    }

                    tr.Commit();
                }

                // Thông báo kết quả
                ed.WriteMessage($"\n\n=== KẾT QUẢ ===");
                ed.WriteMessage($"\nThành công: {successCount} file");
                if (failCount > 0)
                    ed.WriteMessage($"\nThất bại: {failCount} file");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\nLỗi: {ex.Message}");
            }
        }

        /// <summary>
        /// Mở hộp thoại chọn thư mục
        /// </summary>
        private static string SelectFolder()
        {
            using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Chọn thư mục chứa các file DWG cần tạo Xref";
                folderDialog.ShowNewFolderButton = false;

                // Sử dụng thư mục đã chọn lần trước nếu có
                if (!string.IsNullOrEmpty(_lastSelectedFolder) && Directory.Exists(_lastSelectedFolder))
                {
                    folderDialog.SelectedPath = _lastSelectedFolder;
                }
                else
                {
                    // Mặc định là thư mục của file hiện tại
                    Document doc = Application.DocumentManager.MdiActiveDocument;
                    if (doc != null && !string.IsNullOrEmpty(doc.Database.Filename))
                    {
                        string currentDir = Path.GetDirectoryName(doc.Database.Filename);
                        if (!string.IsNullOrEmpty(currentDir))
                            folderDialog.SelectedPath = currentDir;
                    }
                }

                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    _lastSelectedFolder = folderDialog.SelectedPath;
                    return folderDialog.SelectedPath;
                }
            }
            return string.Empty;
        }

        /// <summary>
        /// Lấy tất cả file DWG trong thư mục và thư mục con
        /// </summary>
        private static List<string> GetAllDwgFiles(string folderPath)
        {
            List<string> dwgFiles = new List<string>();
            try
            {
                // Tìm file DWG trong thư mục hiện tại
                dwgFiles.AddRange(Directory.GetFiles(folderPath, "*.dwg", SearchOption.AllDirectories));

                // Sắp xếp theo tên file
                dwgFiles.Sort((a, b) => string.Compare(
                    Path.GetFileName(a),
                    Path.GetFileName(b),
                    StringComparison.OrdinalIgnoreCase));
            }
            catch (System.Exception ex)
            {
                Application.DocumentManager.MdiActiveDocument?.Editor.WriteMessage(
                    $"\nLỗi khi đọc thư mục: {ex.Message}");
            }
            return dwgFiles;
        }

        /// <summary>
        /// Tạo tên xref duy nhất (thêm số nếu trùng)
        /// </summary>
        private static string GetUniqueXrefName(BlockTable bt, string baseName)
        {
            string name = baseName;
            int counter = 1;

            while (bt.Has(name))
            {
                name = $"{baseName}_{counter}";
                counter++;
            }

            return name;
        }

        /// <summary>
        /// Lệnh AT_XrefAttachToOverlay - Chuyển đổi tất cả xref từ Attach sang Overlay trong file hiện tại
        /// </summary>
        [CommandMethod("AT_Xref_Attach_To_Overlay")]
        public static void AT_XrefAttachToOverlay()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            try
            {
                int convertedCount = 0;
                int alreadyOverlayCount = 0;
                int failCount = 0;

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;

                    foreach (ObjectId btrId in bt)
                    {
                        try
                        {
                            BlockTableRecord btr = tr.GetObject(btrId, OpenMode.ForRead) as BlockTableRecord;
                            
                            // Kiểm tra xem có phải là xref không
                            if (btr != null && btr.IsFromExternalReference)
                            {
                                if (btr.IsFromOverlayReference)
                                {
                                    // Đã là overlay
                                    alreadyOverlayCount++;
                                    ed.WriteMessage($"\n  • {btr.Name}: Đã là Overlay");
                                }
                                else
                                {
                                    // Là attach, chuyển sang overlay
                                    btr.UpgradeOpen();
                                    btr.IsFromOverlayReference = true;
                                    convertedCount++;
                                    ed.WriteMessage($"\n  ✓ {btr.Name}: Đã chuyển từ Attach -> Overlay");
                                }
                            }
                        }
                        catch (System.Exception ex)
                        {
                            failCount++;
                            ed.WriteMessage($"\n  ! Lỗi: {ex.Message}");
                        }
                    }

                    tr.Commit();
                }

                // Thông báo kết quả
                ed.WriteMessage($"\n\n=== KẾT QUẢ ===");
                ed.WriteMessage($"\nĐã chuyển đổi: {convertedCount} xref");
                ed.WriteMessage($"\nĐã là Overlay: {alreadyOverlayCount} xref");
                if (failCount > 0)
                    ed.WriteMessage($"\nThất bại: {failCount} xref");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\nLỗi: {ex.Message}");
            }
        }

        /// <summary>
        /// Lệnh AT_XrefAttachToOverlayFile - Chuyển đổi tất cả xref từ Attach sang Overlay trong file được chỉ định
        /// </summary>
        [CommandMethod("AT_Xref_Attach_To_Overlay_File")]
        public static void AT_XrefAttachToOverlayFile()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            try
            {
                // Mở hộp thoại chọn file DWG
                string selectedFile = SelectDwgFile();
                if (string.IsNullOrEmpty(selectedFile))
                {
                    ed.WriteMessage("\nĐã hủy lệnh.");
                    return;
                }

                ed.WriteMessage($"\nĐang xử lý file: {Path.GetFileName(selectedFile)}");

                int convertedCount = 0;
                int alreadyOverlayCount = 0;
                int failCount = 0;

                // Tạo file tạm trong thư mục TEMP của hệ thống (tránh sync Dropbox/OneDrive)
                string tempFile = Path.Combine(Path.GetTempPath(), 
                    "AutoCAD_XrefConvert_" + Path.GetFileNameWithoutExtension(selectedFile) + "_" + DateTime.Now.Ticks + ".dwg");

                // Mở file database riêng biệt (không phải file hiện tại)
                using (Database targetDb = new Database(false, true))
                {
                    targetDb.ReadDwgFile(selectedFile, FileOpenMode.OpenForReadAndAllShare, true, null);

                    // Kiểm tra đơn vị bản vẽ - UnitsValue.Meters = 6
                    if (targetDb.Insunits != UnitsValue.Meters)
                    {
                        string unitName = targetDb.Insunits.ToString();
                        ed.WriteMessage($"\n⚠ CẢNH BÁO: Đơn vị bản vẽ là '{unitName}', không phải 'Meters'!");
                        ed.WriteMessage($"\n  Đơn vị hiện tại: {unitName} (InsUnits = {(int)targetDb.Insunits})");
                    }
                    else
                    {
                        ed.WriteMessage($"\n✓ Đơn vị bản vẽ: Meters");
                    }

                    using (Transaction tr = targetDb.TransactionManager.StartTransaction())
                    {
                        BlockTable bt = tr.GetObject(targetDb.BlockTableId, OpenMode.ForRead) as BlockTable;

                        foreach (ObjectId btrId in bt)
                        {
                            try
                            {
                                BlockTableRecord btr = tr.GetObject(btrId, OpenMode.ForRead) as BlockTableRecord;
                                
                                // Kiểm tra xem có phải là xref không
                                if (btr != null && btr.IsFromExternalReference)
                                {
                                    if (btr.IsFromOverlayReference)
                                    {
                                        // Đã là overlay
                                        alreadyOverlayCount++;
                                        ed.WriteMessage($"\n  • {btr.Name}: Đã là Overlay");
                                    }
                                    else
                                    {
                                        // Là attach, chuyển sang overlay
                                        btr.UpgradeOpen();
                                        btr.IsFromOverlayReference = true;
                                        convertedCount++;
                                        ed.WriteMessage($"\n  ✓ {btr.Name}: Đã chuyển từ Attach -> Overlay");
                                    }
                                }
                            }
                            catch (System.Exception ex)
                            {
                                failCount++;
                                ed.WriteMessage($"\n  ! Lỗi: {ex.Message}");
                            }
                        }

                        tr.Commit();
                    }

                    // Đóng input stream trước khi lưu
                    targetDb.CloseInput(true);

                    // Lưu file tạm vào thư mục TEMP
                    ed.WriteMessage($"\n\nĐang lưu file...");
                    targetDb.SaveAs(tempFile, DwgVersion.Current);
                    ed.WriteMessage($"\n  ✓ Đã lưu vào file tạm");
                }

                // Sau khi Database đã dispose, copy file tạm đè lên file gốc
                ed.WriteMessage($"\n  Đang copy về vị trí gốc...");
                bool copySuccess = false;
                int maxRetries = 3;
                
                for (int retry = 0; retry < maxRetries && !copySuccess; retry++)
                {
                    try
                    {
                        if (retry > 0)
                        {
                            ed.WriteMessage($"\n  Thử lại lần {retry + 1}...");
                            System.Threading.Thread.Sleep(1000); // Đợi 1 giây trước khi thử lại
                        }
                        
                        // Sử dụng File.Copy thay vì File.Move (hoạt động tốt hơn giữa các ổ đĩa)
                        File.Copy(tempFile, selectedFile, true);
                        copySuccess = true;
                        
                        // Xóa file tạm sau khi copy thành công
                        try { File.Delete(tempFile); } catch { }
                    }
                    catch (System.Exception)
                    {
                        if (retry == maxRetries - 1)
                        {
                            // Nếu không thể đè file gốc, giữ lại file tạm và thông báo
                            ed.WriteMessage($"\n\n⚠ Không thể ghi đè file gốc (file có thể đang mở hoặc bị khóa)");
                            ed.WriteMessage($"\nFile đã được lưu tại: {tempFile}");
                            ed.WriteMessage($"\nBạn có thể copy thủ công file này để thay thế file gốc.");
                            
                            // Thông báo kết quả
                            ed.WriteMessage($"\n\n=== KẾT QUẢ ===");
                            ed.WriteMessage($"\nĐã chuyển đổi: {convertedCount} xref");
                            ed.WriteMessage($"\nĐã là Overlay: {alreadyOverlayCount} xref");
                            if (failCount > 0)
                                ed.WriteMessage($"\nThất bại: {failCount} xref");
                            return;
                        }
                    }
                }

                // Thông báo kết quả
                ed.WriteMessage($"\n\n=== KẾT QUẢ ===");
                ed.WriteMessage($"\nFile: {Path.GetFileName(selectedFile)}");
                ed.WriteMessage($"\nĐã chuyển đổi: {convertedCount} xref");
                ed.WriteMessage($"\nĐã là Overlay: {alreadyOverlayCount} xref");
                if (failCount > 0)
                    ed.WriteMessage($"\nThất bại: {failCount} xref");
                ed.WriteMessage($"\nĐã lưu file thành công!");

                // Xref file đã xử lý vào bản vẽ hiện hành
                ed.WriteMessage($"\n\nĐang tạo Xref Overlay vào bản vẽ hiện hành...");
                try
                {
                    Database db = doc.Database;
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                        BlockTableRecord modelSpace = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                        // Tạo tên xref từ tên file
                        string xrefName = Path.GetFileNameWithoutExtension(selectedFile);
                        xrefName = GetUniqueXrefName(bt, xrefName);

                        // Tạo Xref Overlay
                        ObjectId xrefId = db.OverlayXref(selectedFile, xrefName);

                        if (xrefId.IsValid)
                        {
                            // Tạo BlockReference tại điểm 0,0,0
                            using (BlockReference xrefRef = new BlockReference(Point3d.Origin, xrefId))
                            {
                                modelSpace.AppendEntity(xrefRef);
                                tr.AddNewlyCreatedDBObject(xrefRef, true);
                            }
                            ed.WriteMessage($"\n  ✓ Đã tạo Xref Overlay: {xrefName}");
                        }
                        else
                        {
                            ed.WriteMessage($"\n  ! Không thể tạo Xref cho file đã xử lý");
                        }

                        tr.Commit();
                    }
                }
                catch (System.Exception xrefEx)
                {
                    ed.WriteMessage($"\n  ! Lỗi khi tạo Xref: {xrefEx.Message}");
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\nLỗi: {ex.Message}");
            }
        }

        /// <summary>
        /// Mở hộp thoại chọn file DWG
        /// </summary>
        private static string SelectDwgFile()
        {
            using (OpenFileDialog openDialog = new OpenFileDialog())
            {
                openDialog.Title = "Chọn file DWG cần chuyển đổi Xref";
                openDialog.Filter = "AutoCAD Drawing (*.dwg)|*.dwg";
                openDialog.FilterIndex = 1;
                openDialog.RestoreDirectory = true;

                // Mặc định là thư mục của file hiện tại
                Document doc = Application.DocumentManager.MdiActiveDocument;
                if (doc != null && !string.IsNullOrEmpty(doc.Database.Filename))
                {
                    string currentDir = Path.GetDirectoryName(doc.Database.Filename);
                    if (!string.IsNullOrEmpty(currentDir))
                        openDialog.InitialDirectory = currentDir;
                }

                if (openDialog.ShowDialog() == DialogResult.OK)
                {
                    return openDialog.FileName;
                }
            }
            return string.Empty;
        }
    }
}

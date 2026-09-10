using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.Civil.DatabaseServices;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;
using Exception = System.Exception;

[assembly: CommandClass(typeof(Civil3DCsharp.CTC_XuatSolidCorridor_Commands))]

namespace Civil3DCsharp
{
    public class CTC_XuatSolidCorridor_Commands
    {
        [CommandMethod("CTC_XuatSolidCorridor")]
        [CommandMethod("CTC_XUATSOLID_CORRIDOR")]
        [CommandMethod("CTC_ExportCorridorSolids")]
        public static void CTC_XuatSolidCorridor()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var db = doc.Database;
            var ed = doc.Editor;

            ed.WriteMessage("\n=======================================================");
            ed.WriteMessage("\n  CTC_XuatSolidCorridor: XUẤT 3D SOLID & BODY TỪ CORRIDOR");
            ed.WriteMessage("\n=======================================================\n");

            using (var form = new MyFirstProject.Civil_Tool_2.XuatSolidCorridorForm(db))
            {
                var dialogResult = Application.ShowModalDialog(form);
                if (dialogResult != DialogResult.OK || !form.FormAccepted || form.SelectedCorridorId.IsNull)
                {
                    ed.WriteMessage("\n[Hủy bỏ] Thao tác xuất 3D Solid / Body từ Corridor đã được hủy.");
                    return;
                }

                ObjectId sourceCorridorId = form.SelectedCorridorId;
                bool exportShapes = form.ExportShapes;
                bool createSolidForShape = form.CreateSolidForShape;
                bool sweepSolidForShape = form.SweepSolidForShape;
                bool exportLinks = form.ExportLinks;
                bool filterCodes = form.FilterCodes;
                List<string> includedCodes = form.IncludedCodes;
                string outputFilePath = form.OutputFilePath;
                bool overwriteIfExists = form.OverwriteIfExists;
                bool openAfterExport = form.OpenAfterExport;
                bool openFolderAfterExport = form.OpenFolderAfterExport;

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    try
                    {
                        var sourceCorridor = tr.GetObject(sourceCorridorId, OpenMode.ForWrite) as Corridor;
                        if (sourceCorridor == null)
                        {
                            ed.WriteMessage("\n❌ Lỗi: Không thể truy xuất đối tượng Corridor đã chọn.");
                            tr.Abort();
                            return;
                        }

                        ed.WriteMessage($"\n🔄 Đang trích xuất 3D Solid / Body từ Corridor '{sourceCorridor.Name}'...");

                        // 1. Cấu hình tham số xuất ExportCorridorSolidsParams
                        var solidsParams = new ExportCorridorSolidsParams
                        {
                            ExportShapes = exportShapes,
                            CreateSolidForShape = createSolidForShape,
                            SweepSolidForShape = sweepSolidForShape,
                            ExportLinks = exportLinks,
                            ExcludedCodes = Array.Empty<string>()
                        };

                        if (filterCodes && includedCodes != null && includedCodes.Count > 0)
                        {
                            solidsParams.IncludedCodes = includedCodes.ToArray();
                            ed.WriteMessage($"\n  • Áp dụng bộ lọc mã ({includedCodes.Count} mã code): {string.Join(", ", includedCodes)}");
                        }
                        else
                        {
                            solidsParams.IncludedCodes = Array.Empty<string>();
                            ed.WriteMessage("\n  • Xuất toàn bộ mã code trong Corridor (không lọc).");
                        }

                        // 2. Chuẩn bị thư mục đích
                        string? targetDir = Path.GetDirectoryName(outputFilePath);
                        if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                        {
                            Directory.CreateDirectory(targetDir);
                        }

                        // 3. Khởi tạo Database đích & thực thi trích xuất ExportSolids
                        int solidCount = 0;
                        int bodyCount = 0;
                        int totalEntities = 0;

                        using (Database targetDb = new Database(true, true))
                        {
                            sourceCorridor.ExportSolids(solidsParams, targetDb);

                            // 4. Thống kê các đối tượng đã được tạo trong ModelSpace của targetDb
                            using (Transaction targetTr = targetDb.TransactionManager.StartTransaction())
                            {
                                BlockTable? bt = targetTr.GetObject(targetDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                                if (bt != null)
                                {
                                    BlockTableRecord? ms = targetTr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
                                    if (ms != null)
                                    {
                                        foreach (ObjectId entId in ms)
                                        {
                                            DBObject? ent = targetTr.GetObject(entId, OpenMode.ForRead);
                                            if (ent is Solid3d) solidCount++;
                                            else if (ent is Body) bodyCount++;
                                            totalEntities++;
                                        }
                                    }
                                }
                                targetTr.Commit();
                            }

                            // 5. Lưu target database ra file DWG
                            targetDb.SaveAs(outputFilePath, DwgVersion.Current);
                        }

                        tr.Commit();

                        long fileSizeBytes = 0;
                        try
                        {
                            if (File.Exists(outputFilePath))
                            {
                                fileSizeBytes = new FileInfo(outputFilePath).Length;
                            }
                        }
                        catch { }

                        double fileSizeKb = fileSizeBytes / 1024.0;

                        ed.WriteMessage("\n=======================================================");
                        ed.WriteMessage("\n🎉 XUẤT 3D SOLID / BODY TỪ CORRIDOR THÀNH CÔNG!");
                        ed.WriteMessage($"\n  • Corridor nguồn:     {sourceCorridor.Name}");
                        ed.WriteMessage($"\n  • File DWG xuất:      {outputFilePath}");
                        ed.WriteMessage($"\n  • Dung lượng file:    {fileSizeKb:F1} KB");
                        ed.WriteMessage($"\n  • Số lượng 3D Solid:  {solidCount}");
                        ed.WriteMessage($"\n  • Số lượng 3D Body:   {bodyCount}");
                        ed.WriteMessage($"\n  • Tổng đối tượng:     {totalEntities}");
                        ed.WriteMessage("\n=======================================================\n");

                        // 6. Mở file bản vẽ nếu được yêu cầu
                        if (openAfterExport && File.Exists(outputFilePath))
                        {
                            try
                            {
                                Application.DocumentManager.Open(outputFilePath, false);
                                ed.WriteMessage($"\n📖 Đã mở file '{Path.GetFileName(outputFilePath)}' trong AutoCAD.");
                            }
                            catch (Exception exOpen)
                            {
                                ed.WriteMessage($"\n⚠ Không thể mở file trực tiếp: {exOpen.Message}");
                            }
                        }

                        // 7. Mở thư mục chứa file nếu được yêu cầu
                        if (openFolderAfterExport && File.Exists(outputFilePath))
                        {
                            try
                            {
                                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{outputFilePath}\"")
                                {
                                    UseShellExecute = true
                                });
                            }
                            catch (Exception exFolder)
                            {
                                ed.WriteMessage($"\n⚠ Không thể mở thư mục: {exFolder.Message}");
                            }
                        }
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception eCad)
                    {
                        ed.WriteMessage($"\n❌ Lỗi AutoCAD: {eCad.Message}\n{eCad.StackTrace}");
                        tr.Abort();
                    }
                    catch (Exception ex)
                    {
                        ed.WriteMessage($"\n❌ Lỗi hệ thống: {ex.Message}\n{ex.StackTrace}");
                        tr.Abort();
                    }
                }
            }
        }
    }
}

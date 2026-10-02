using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Colors;
using Autodesk.Civil.DatabaseServices;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;
using CadEntity = Autodesk.AutoCAD.DatabaseServices.Entity;
using CadColor = Autodesk.AutoCAD.Colors.Color;
using Exception = System.Exception;

[assembly: CommandClass(typeof(Civil3DCsharp.CTC_Xuat_Solid_Tu_Corridor_Commands))]

namespace Civil3DCsharp
{
    public class CTC_Xuat_Solid_Tu_Corridor_Commands
    {
        private const string REG_APP_NAME = "CORRIDOR_EXTRACT_SOLIDS";

        [CommandMethod("CTC_Xuat_Solid_Tu_Corridor")]
        public static void CTC_Xuat_Solid_Tu_Corridor()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var db = doc.Database;
            var ed = doc.Editor;

            ed.WriteMessage("\n=======================================================");
            ed.WriteMessage("\n  CTC_Xuat_Solid_Tu_Corridor: XUẤT SOLID TỪ CORRIDOR");
            ed.WriteMessage("\n=======================================================\n");

            using (var form = new MyFirstProject.Civil_Tool_2.ExtractCorridorSolidsForm(db))
            {
                var dialogResult = Application.ShowModalDialog(form);
                if (dialogResult != DialogResult.OK || !form.FormAccepted || form.SelectedCorridorId.IsNull)
                {
                    ed.WriteMessage("\n[Hủy bỏ] Thao tác xuất 3D Solid từ Corridor đã được hủy.");
                    return;
                }

                ObjectId sourceCorridorId = form.SelectedCorridorId;
                var objectType = form.ObjectType;
                var destination = form.Destination;
                bool exportShapes = form.ExportShapes;
                bool createSolidForShape = form.CreateSolidForShape;
                bool sweepSolidForShape = form.SweepSolidForShape;
                bool exportLinks = form.ExportLinks;
                bool filterCodes = form.FilterCodes;
                List<string> includedCodes = form.IncludedCodes;
                string outputFilePath = form.OutputFilePath;
                bool attachXData = form.AttachXData;
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

                        ed.WriteMessage($"\n🔄 Đang xuất 3D Solid từ Corridor '{sourceCorridor.Name}'...");
                        ed.WriteMessage($"\n  • Kiểu đối tượng: {GetObjTypeDescription(objectType)}");
                        ed.WriteMessage($"\n  • Đích đến:       {GetDestinationDescription(destination)}");

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
                            ed.WriteMessage($"\n  • Áp dụng bộ lọc ({includedCodes.Count} mã code): {string.Join(", ", includedCodes)}");
                        }
                        else
                        {
                            solidsParams.IncludedCodes = Array.Empty<string>();
                            ed.WriteMessage("\n  • Xuất toàn bộ mã code trong Corridor (không lọc).");
                        }

                        int solidCount = 0;
                        int bodyCount = 0;
                        int totalEntities = 0;

                        // 2. Thực thi theo từng chế độ đích đến
                        if (destination == MyFirstProject.Civil_Tool_2.OutputDestination.InsertIntoCurrentDrawing)
                        {
                            // ── CHẾ ĐỘ 1: INSERT VÀO BẢN VẼ HIỆN TẠI ──
                            EnsureRegApp(db, tr);

                            using (Database tempDb = new Database(true, true))
                            {
                                sourceCorridor.ExportSolids(solidsParams, tempDb);

                                var tempIds = CollectEntitiesInModelSpace(tempDb);
                                if (tempIds.Count > 0)
                                {
                                    var idMap = new IdMapping();
                                    tempDb.WblockCloneObjects(tempIds, db.CurrentSpaceId, idMap, DuplicateRecordCloning.Ignore, false);

                                    // Duyệt các đối tượng vừa clone vào db hiện tại để cập nhật Layer & XData
                                    foreach (ObjectId tempId in tempIds)
                                    {
                                        if (idMap.Contains(tempId))
                                        {
                                            IdPair pair = idMap[tempId];
                                            if (pair.IsCloned && !pair.Value.IsNull)
                                            {
                                                var ent = tr.GetObject(pair.Value, OpenMode.ForWrite) as CadEntity;
                                                if (ent != null)
                                                {
                                                    if (ent is Solid3d) solidCount++;
                                                    else if (ent is Body) bodyCount++;
                                                    totalEntities++;

                                                    ApplyMetadata(ent, sourceCorridor.Name, attachXData);
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            tr.Commit();

                            ed.WriteMessage("\n=======================================================");
                            ed.WriteMessage("\n🎉 XUẤT 3D SOLIDS VÀO BẢN VẼ HIỆN HÀNH THÀNH CÔNG!");
                            ed.WriteMessage($"\n  • Corridor nguồn:     {sourceCorridor.Name}");
                            ed.WriteMessage($"\n  • Số lượng 3D Solid:  {solidCount}");
                            ed.WriteMessage($"\n  • Số lượng 3D Body:   {bodyCount}");
                            ed.WriteMessage($"\n  • Tổng đối tượng:     {totalEntities}");
                            ed.WriteMessage("\n=======================================================\n");
                        }
                        else if (destination == MyFirstProject.Civil_Tool_2.OutputDestination.AddToExistingDrawing)
                        {
                            // ── CHẾ ĐỘ 2: THÊM VÀO FILE DWG CÓ SẴN ──
                            if (!File.Exists(outputFilePath))
                            {
                                ed.WriteMessage($"\n❌ Lỗi: File đích '{outputFilePath}' không tồn tại.");
                                tr.Abort();
                                return;
                            }

                            using (Database targetDb = new Database(false, true))
                            {
                                targetDb.ReadDwgFile(outputFilePath, FileShare.ReadWrite, false, "");
                                targetDb.CloseInput(true);

                                using (Database tempDb = new Database(true, true))
                                {
                                    sourceCorridor.ExportSolids(solidsParams, tempDb);

                                    var tempIds = CollectEntitiesInModelSpace(tempDb);
                                    if (tempIds.Count > 0)
                                    {
                                        var idMap = new IdMapping();
                                        tempDb.WblockCloneObjects(tempIds, targetDb.CurrentSpaceId, idMap, DuplicateRecordCloning.Ignore, false);

                                        using (Transaction targetTr = targetDb.TransactionManager.StartTransaction())
                                        {
                                            EnsureRegApp(targetDb, targetTr);

                                            foreach (ObjectId tempId in tempIds)
                                            {
                                                if (idMap.Contains(tempId))
                                                {
                                                    IdPair pair = idMap[tempId];
                                                    if (pair.IsCloned && !pair.Value.IsNull)
                                                    {
                                                        var ent = targetTr.GetObject(pair.Value, OpenMode.ForWrite) as CadEntity;
                                                        if (ent != null)
                                                        {
                                                            if (ent is Solid3d) solidCount++;
                                                            else if (ent is Body) bodyCount++;
                                                            totalEntities++;

                                                            ApplyMetadata(ent, sourceCorridor.Name, attachXData);
                                                        }
                                                    }
                                                }
                                            }
                                            targetTr.Commit();
                                        }
                                    }
                                }

                                targetDb.SaveAs(outputFilePath, DwgVersion.Current);
                            }

                            tr.Commit();

                            ShowExportResult(ed, sourceCorridor.Name, outputFilePath, solidCount, bodyCount, totalEntities, openAfterExport, openFolderAfterExport);
                        }
                        else
                        {
                            // ── CHẾ ĐỘ 3: XUẤT RA FILE DWG MỚI ──
                            string? targetDir = Path.GetDirectoryName(outputFilePath);
                            if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
                            {
                                Directory.CreateDirectory(targetDir);
                            }

                            using (Database targetDb = new Database(true, true))
                            {
                                sourceCorridor.ExportSolids(solidsParams, targetDb);

                                using (Transaction targetTr = targetDb.TransactionManager.StartTransaction())
                                {
                                    EnsureRegApp(targetDb, targetTr);

                                    BlockTable? bt = targetTr.GetObject(targetDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                                    if (bt != null)
                                    {
                                        BlockTableRecord? ms = targetTr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
                                        if (ms != null)
                                        {
                                            foreach (ObjectId entId in ms)
                                            {
                                                var ent = targetTr.GetObject(entId, OpenMode.ForWrite) as CadEntity;
                                                if (ent != null)
                                                {
                                                    if (ent is Solid3d) solidCount++;
                                                    else if (ent is Body) bodyCount++;
                                                    totalEntities++;

                                                    ApplyMetadata(ent, sourceCorridor.Name, attachXData);
                                                }
                                            }
                                        }
                                    }
                                    targetTr.Commit();
                                }

                                targetDb.SaveAs(outputFilePath, DwgVersion.Current);
                            }

                            tr.Commit();

                            ShowExportResult(ed, sourceCorridor.Name, outputFilePath, solidCount, bodyCount, totalEntities, openAfterExport, openFolderAfterExport);
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

        // ═══════════════════════════════════════════════════════════════
        //  HELPER METHODS
        // ═══════════════════════════════════════════════════════════════
        private static ObjectIdCollection CollectEntitiesInModelSpace(Database db)
        {
            var ids = new ObjectIdCollection();
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                foreach (ObjectId id in ms)
                {
                    var ent = tr.GetObject(id, OpenMode.ForRead);
                    if (ent is Solid3d || ent is Body)
                    {
                        ids.Add(id);
                    }
                }
                tr.Commit();
            }
            return ids;
        }

        private static void EnsureRegApp(Database db, Transaction tr)
        {
            var regTable = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
            if (!regTable.Has(REG_APP_NAME))
            {
                regTable.UpgradeOpen();
                var regRecord = new RegAppTableRecord { Name = REG_APP_NAME };
                regTable.Add(regRecord);
                tr.AddNewlyCreatedDBObject(regRecord, true);
            }
        }

        private static void ApplyMetadata(CadEntity ent, string corridorName, bool attachXData)
        {
            if (attachXData)
            {
                string currentLayer = ent.Layer;
                if (string.IsNullOrEmpty(currentLayer)) currentLayer = "0";

                using (var rb = new ResultBuffer())
                {
                    rb.Add(new TypedValue((int)DxfCode.ExtendedDataRegAppName, REG_APP_NAME));
                    rb.Add(new TypedValue((int)DxfCode.ExtendedDataAsciiString, $"Corridor: {corridorName}"));
                    rb.Add(new TypedValue((int)DxfCode.ExtendedDataAsciiString, $"Code: {currentLayer}"));
                    rb.Add(new TypedValue((int)DxfCode.ExtendedDataAsciiString, $"EntityType: {ent.GetType().Name}"));
                    rb.Add(new TypedValue((int)DxfCode.ExtendedDataAsciiString, $"ExportTime: {DateTime.Now:yyyy-MM-dd HH:mm:ss}"));
                    ent.XData = rb;
                }
            }
        }

        private static void ShowExportResult(Editor ed, string corridorName, string outputFilePath, int solidCount, int bodyCount, int totalEntities, bool openAfterExport, bool openFolderAfterExport)
        {
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
            ed.WriteMessage("\n🎉 XUẤT 3D SOLIDS RA FILE DWG THÀNH CÔNG!");
            ed.WriteMessage($"\n  • Corridor nguồn:     {corridorName}");
            ed.WriteMessage($"\n  • File DWG xuất:      {outputFilePath}");
            ed.WriteMessage($"\n  • Dung lượng file:    {fileSizeKb:F1} KB");
            ed.WriteMessage($"\n  • Số lượng 3D Solid:  {solidCount}");
            ed.WriteMessage($"\n  • Số lượng 3D Body:   {bodyCount}");
            ed.WriteMessage($"\n  • Tổng đối tượng:     {totalEntities}");
            ed.WriteMessage("\n=======================================================\n");

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

        private static string GetObjTypeDescription(MyFirstProject.Civil_Tool_2.OutputObjectType type)
        {
            return type switch
            {
                MyFirstProject.Civil_Tool_2.OutputObjectType.SolidsBasedOnCorridorSampling => "AutoCAD 3D Solids (based on corridor sampling)",
                MyFirstProject.Civil_Tool_2.OutputObjectType.SolidsSweptSolids => "AutoCAD 3D Solids (swept solids)",
                MyFirstProject.Civil_Tool_2.OutputObjectType.Bodies => "AutoCAD 3D Bodies",
                _ => type.ToString()
            };
        }

        private static string GetDestinationDescription(MyFirstProject.Civil_Tool_2.OutputDestination dest)
        {
            return dest switch
            {
                MyFirstProject.Civil_Tool_2.OutputDestination.InsertIntoCurrentDrawing => "Insert into current drawing (Bản vẽ hiện tại)",
                MyFirstProject.Civil_Tool_2.OutputDestination.AddToExistingDrawing => "Add to an existing drawing (Thêm vào file có sẵn)",
                MyFirstProject.Civil_Tool_2.OutputDestination.AddToNewDrawing => "Add to a new drawing (Xuất ra file mới)",
                _ => dest.ToString()
            };
        }
    }
}

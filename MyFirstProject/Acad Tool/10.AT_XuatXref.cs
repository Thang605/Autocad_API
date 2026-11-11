// (C) Copyright 2025 by T27
//
using System;
using System.IO;
using System.Collections.Generic;

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.ApplicationServices;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(Civil3DCsharp.XrefToBlockConverter))]

namespace Civil3DCsharp
{
    /// <summary>
    /// Lệnh chuyển đổi Xref thành Block
    /// </summary>
    public class XrefToBlockConverter
    {
        /// <summary>
        /// Lệnh AT_XrefToBlock - Chuyển đổi Xref đã loaded thành Block (bỏ qua xref chưa load)
        /// Sử dụng Database.BindXrefs() để bind trực tiếp
        /// </summary>
        [CommandMethod("AT_Xref_To_Block")]
        public static void AT_XrefToBlock()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            try
            {
                using (DocumentLock docLock = doc.LockDocument())
                {
                    ed.WriteMessage("\n=== CHUYỂN ĐỔI XREF THÀNH BLOCK ===");
                    ed.WriteMessage("\n(Chỉ xử lý Xref đã loaded, bỏ qua các Xref chưa load)\n");

                    // Thu thập ObjectId của các xref đã loaded
                    ObjectIdCollection loadedXrefIds = new ObjectIdCollection();
                    List<string> loadedXrefNames = new List<string>();
                    List<string> skippedXrefs = new List<string>();

                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;

                        foreach (ObjectId btrId in bt)
                        {
                            BlockTableRecord btr = tr.GetObject(btrId, OpenMode.ForRead) as BlockTableRecord;

                            if (btr != null && btr.IsFromExternalReference)
                            {
                                XrefStatus status = btr.XrefStatus;

                                if (status == XrefStatus.Resolved)
                                {
                                    // Xref đã loaded - thêm vào danh sách bind
                                    loadedXrefIds.Add(btrId);
                                    loadedXrefNames.Add(btr.Name);
                                    ed.WriteMessage($"\n  ✓ Loaded: {btr.Name}");
                                }
                                else
                                {
                                    // Xref chưa load - bỏ qua
                                    skippedXrefs.Add($"{btr.Name} ({status})");
                                    ed.WriteMessage($"\n  ○ Bỏ qua ({status}): {btr.Name}");
                                }
                            }
                        }

                        tr.Commit();
                    }

                    if (loadedXrefIds.Count == 0)
                    {
                        ed.WriteMessage("\n\nKhông có Xref đã loaded nào để xử lý.");
                        return;
                    }

                    ed.WriteMessage($"\n\nTìm thấy: {loadedXrefIds.Count} Xref đã loaded");
                    if (skippedXrefs.Count > 0)
                        ed.WriteMessage($", {skippedXrefs.Count} Xref bỏ qua");

                    // Xử lý từng Xref bằng cách Detach rồi Insert
                    ed.WriteMessage("\n\nĐang Detach và Insert từng Xref một...");
                    int successCount = 0;
                    
                    for (int i = 0; i < loadedXrefIds.Count; i++)
                    {
                        try
                        {
                            string xrefName = "";
                            string xrefPath = "";
                            var blockRefDataList = new List<Tuple<Point3d, Scale3d, double, string, ObjectId>>();

                            using (Transaction trCheck = db.TransactionManager.StartTransaction())
                            {
                                BlockTableRecord btrCheck = trCheck.GetObject(loadedXrefIds[i], OpenMode.ForRead) as BlockTableRecord;
                                if (btrCheck == null || !btrCheck.IsFromExternalReference)
                                {
                                    ed.WriteMessage($"\n  ✓ {loadedXrefNames[i]} (đã được xử lý qua xref khác)");
                                    successCount++;
                                    trCheck.Commit();
                                    continue;
                                }

                                xrefName = btrCheck.Name;
                                xrefPath = btrCheck.PathName;

                                // Lấy thông tin tất cả các reference đang chèn trong bản vẽ
                                ObjectIdCollection refIds = btrCheck.GetBlockReferenceIds(true, true);
                                foreach (ObjectId refId in refIds)
                                {
                                    BlockReference br = trCheck.GetObject(refId, OpenMode.ForRead) as BlockReference;
                                    if (br != null)
                                    {
                                        blockRefDataList.Add(Tuple.Create(br.Position, br.ScaleFactors, br.Rotation, br.Layer, br.OwnerId));
                                    }
                                }
                                trCheck.Commit();
                            }

                            // Cố gắng phân giải đường dẫn đầy đủ
                            string resolvedPath = xrefPath;
                            try {
                                resolvedPath = HostApplicationServices.Current.FindFile(xrefPath, db, FindFileHint.Default);
                            } catch { }

                            if (!System.IO.File.Exists(resolvedPath))
                            {
                                ed.WriteMessage($"\n  ! {xrefName}: Không tìm thấy file gốc ({resolvedPath})");
                                continue;
                            }

                            // Detach Xref (Hành động này sẽ xóa tất cả BlockReference cũ)
                            db.DetachXref(loadedXrefIds[i]);

                            // Insert file DWG như một block mới
                            ObjectId newBlockId;
                            using (Database sideDb = new Database(false, true))
                            {
                                sideDb.ReadDwgFile(resolvedPath, FileShare.Read, true, "");
                                newBlockId = db.Insert(xrefName, sideDb, true);
                            }

                            // Tạo lại các BlockReference tại vị trí cũ
                            using (Transaction trInsert = db.TransactionManager.StartTransaction())
                            {
                                foreach (var data in blockRefDataList)
                                {
                                    BlockTableRecord owner = (BlockTableRecord)trInsert.GetObject(data.Item5, OpenMode.ForWrite);
                                    BlockReference newBr = new BlockReference(data.Item1, newBlockId);
                                    newBr.ScaleFactors = data.Item2;
                                    newBr.Rotation = data.Item3;
                                    newBr.Layer = data.Item4;
                                    
                                    owner.AppendEntity(newBr);
                                    trInsert.AddNewlyCreatedDBObject(newBr, true);
                                }
                                trInsert.Commit();
                            }

                            ed.WriteMessage($"\n  ✓ {xrefName} (Đã Detach & Insert)");
                            successCount++;
                        }
                        catch (System.Exception ex)
                        {
                            ed.WriteMessage($"\n  ! {loadedXrefNames[i]}: {ex.Message}");
                        }
                    }
                    
                    ed.WriteMessage($"\n\nKết quả: {successCount}/{loadedXrefIds.Count} Xref đã xử lý thành công.");

                    ed.WriteMessage("\n");
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\nLỗi: {ex.Message}");
            }
        }
    }
}

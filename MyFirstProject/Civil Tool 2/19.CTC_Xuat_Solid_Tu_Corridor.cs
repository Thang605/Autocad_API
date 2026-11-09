using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Colors;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Aec.PropertyData.DatabaseServices;

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
                        ed.WriteMessage($"\n  • Thuộc tính:     {(attachXData ? "Đính kèm Property Sets & XData" : "3D Solid sạch (Loại bỏ Property Sets mặc định)")}");

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
                            if (attachXData)
                            {
                                EnsureRegApp(db, tr);
                            }

                            using (Database tempDb = new Database(true, true))
                            {
                                SetDatabaseUnitsToMeters(tempDb);
                                sourceCorridor.ExportSolids(solidsParams, tempDb);
                                SetDatabaseUnitsToMeters(tempDb);

                                // Đọc thông tin cấu kiện (SubassemblyName) từ PropertySet trước khi dọn
                                var entityLayerMap = ReadComponentLayerInfo(tempDb);

                                // Nếu không muốn mang Property Set mặc định, dọn sạch ngay trong tempDb trước khi clone
                                if (!attachXData)
                                {
                                    CleanAllPropertySetsInDatabase(tempDb, true);
                                }

                                var tempIds = CollectEntitiesInModelSpace(tempDb);
                                if (tempIds.Count > 0)
                                {
                                    var idMap = new IdMapping();
                                    tempDb.WblockCloneObjects(tempIds, db.CurrentSpaceId, idMap, DuplicateRecordCloning.Ignore, false);

                                    // Duyệt các đối tượng vừa clone vào db hiện tại để cập nhật Layer, XData hoặc làm sạch
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

                                                    // Gán layer theo format: ComponentName_ShapeCode
                                                    if (entityLayerMap.TryGetValue(tempId, out string? newLayerName) && !string.IsNullOrEmpty(newLayerName))
                                                    {
                                                        EnsureLayerExists(db, tr, newLayerName);
                                                        ent.Layer = newLayerName;
                                                    }

                                                    if (attachXData)
                                                    {
                                                        ApplyMetadata(ent, sourceCorridor.Name, true);
                                                    }
                                                    else
                                                    {
                                                        RemoveAllPropertySetsFromEntity(ent, tr);
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    if (!attachXData)
                                    {
                                        PurgeCorridorPropertySetDefinitions(db, tr);
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
                            ed.WriteMessage($"\n  • Trạng thái PSet:    {(attachXData ? "Đã đính kèm" : "Đã loại bỏ toàn bộ Property Sets mặc định")}");
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
                                    SetDatabaseUnitsToMeters(tempDb);
                                    sourceCorridor.ExportSolids(solidsParams, tempDb);
                                    SetDatabaseUnitsToMeters(tempDb);

                                    // Đọc thông tin cấu kiện (SubassemblyName) từ PropertySet trước khi dọn
                                    var entityLayerMap = ReadComponentLayerInfo(tempDb);

                                    // Nếu không muốn mang Property Set mặc định, dọn sạch ngay trong tempDb trước khi clone
                                    if (!attachXData)
                                    {
                                        CleanAllPropertySetsInDatabase(tempDb, true);
                                    }

                                    var tempIds = CollectEntitiesInModelSpace(tempDb);
                                    if (tempIds.Count > 0)
                                    {
                                        var idMap = new IdMapping();
                                        tempDb.WblockCloneObjects(tempIds, targetDb.CurrentSpaceId, idMap, DuplicateRecordCloning.Ignore, false);

                                        using (Transaction targetTr = targetDb.TransactionManager.StartTransaction())
                                        {
                                            if (attachXData)
                                            {
                                                EnsureRegApp(targetDb, targetTr);
                                            }

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

                                                            // Gán layer theo format: ComponentName_ShapeCode
                                                            if (entityLayerMap.TryGetValue(tempId, out string? newLayerName) && !string.IsNullOrEmpty(newLayerName))
                                                            {
                                                                EnsureLayerExists(targetDb, targetTr, newLayerName);
                                                                ent.Layer = newLayerName;
                                                            }

                                                            if (attachXData)
                                                            {
                                                                ApplyMetadata(ent, sourceCorridor.Name, true);
                                                            }
                                                            else
                                                            {
                                                                RemoveAllPropertySetsFromEntity(ent, targetTr);
                                                            }
                                                        }
                                                    }
                                                }
                                            }

                                            if (!attachXData)
                                            {
                                                PurgeCorridorPropertySetDefinitions(targetDb, targetTr);
                                            }

                                            targetTr.Commit();
                                        }
                                    }
                                }

                                SetDatabaseUnitsToMeters(targetDb);
                                targetDb.SaveAs(outputFilePath, DwgVersion.Current);
                            }

                            tr.Commit();

                            ShowExportResult(ed, sourceCorridor.Name, outputFilePath, solidCount, bodyCount, totalEntities, attachXData, openAfterExport, openFolderAfterExport);
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
                                SetDatabaseUnitsToMeters(targetDb);
                                sourceCorridor.ExportSolids(solidsParams, targetDb);
                                SetDatabaseUnitsToMeters(targetDb);

                                // Đọc thông tin cấu kiện (SubassemblyName) từ PropertySet trước khi dọn
                                var entityLayerMap = ReadComponentLayerInfo(targetDb);

                                using (Transaction targetTr = targetDb.TransactionManager.StartTransaction())
                                {
                                    if (attachXData)
                                    {
                                        EnsureRegApp(targetDb, targetTr);
                                    }

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

                                                    // Gán layer theo format: ComponentName_ShapeCode
                                                    if (entityLayerMap.TryGetValue(entId, out string? newLayerName) && !string.IsNullOrEmpty(newLayerName))
                                                    {
                                                        EnsureLayerExists(targetDb, targetTr, newLayerName);
                                                        ent.Layer = newLayerName;
                                                    }

                                                    if (attachXData)
                                                    {
                                                        ApplyMetadata(ent, sourceCorridor.Name, true);
                                                    }
                                                    else
                                                    {
                                                        RemoveAllPropertySetsFromEntity(ent, targetTr);
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    // Nếu không đính kèm Property Data, xóa sạch toàn bộ Property Set Definitions trong file mới
                                    if (!attachXData)
                                    {
                                        PurgeCorridorPropertySetDefinitions(targetDb, targetTr);
                                    }

                                    targetTr.Commit();
                                }

                                SetDatabaseUnitsToMeters(targetDb);
                                targetDb.SaveAs(outputFilePath, DwgVersion.Current);
                            }

                            tr.Commit();

                            ShowExportResult(ed, sourceCorridor.Name, outputFilePath, solidCount, bodyCount, totalEntities, attachXData, openAfterExport, openFolderAfterExport);
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
        //  HELPER METHODS - DRAWING UNITS & PROPERTY SETS CLEANUP
        // ═══════════════════════════════════════════════════════════════
        /// <summary>
        /// Thiết lập đơn vị bản vẽ đích sang hệ Mét (Meters) cho cả AutoCAD (INSUNITS, MEASUREMENT),
        /// AEC Drawing Setup (-DWGUNITS / AecDbVarsDwgSetup) và Civil 3D UnitZoneSettings.
        /// </summary>
        private static void SetDatabaseUnitsToMeters(Database targetDb)
        {
            if (targetDb == null) return;

            // 1. Thiết lập biến hệ thống AutoCAD Database (INSUNITS = Meters, MEASUREMENT = Metric)
            try
            {
                targetDb.Insunits = UnitsValue.Meters;
                targetDb.Measurement = MeasurementValue.Metric;
                targetDb.Lunits = 2; // Decimal
            }
            catch { }

            // 2. Thiết lập AEC Drawing Setup Variables (-DWGUNITS = 6 [Meters])
            try
            {
                using (Transaction trUnit = targetDb.TransactionManager.StartTransaction())
                {
                    ObjectId setupId = Autodesk.Aec.ApplicationServices.DrawingSetupVariables.GetInstance(targetDb, true);
                    if (!setupId.IsNull && setupId.IsValid)
                    {
                        if (trUnit.GetObject(setupId, OpenMode.ForWrite) is Autodesk.Aec.ApplicationServices.DrawingSetupVariables setupVars)
                        {
                            setupVars.LinearUnit = Autodesk.Aec.BuiltInUnit.Meter;
                            setupVars.AreaDisplayUnit = Autodesk.Aec.BuiltInUnit.SquareMeters;
                            setupVars.VolumeDisplayUnit = Autodesk.Aec.BuiltInUnit.CubicMeter;
                            setupVars.CurrentDimUnit = Autodesk.Aec.BuiltInUnit.Meter;
                        }
                    }
                    trUnit.Commit();
                }
            }
            catch { }

            // 3. Thiết lập Civil 3D Drawing Settings (UnitZoneSettings.DrawingUnits = Meters)
            try
            {
                var civilDoc = Autodesk.Civil.ApplicationServices.CivilDocument.GetCivilDocument(targetDb);
                if (civilDoc != null)
                {
                    civilDoc.Settings.DrawingSettings.UnitZoneSettings.DrawingUnits = Autodesk.Civil.Settings.DrawingUnitType.Meters;
                }
            }
            catch { }
        }

        /// <summary>
        /// Xóa sạch toàn bộ Property Sets mặc định và định nghĩa PropertySet trong Database
        /// </summary>
        private static void CleanAllPropertySetsInDatabase(Database db, bool isNewOrTempDb)
        {
            try
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTable? bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    if (bt != null)
                    {
                        BlockTableRecord? ms = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
                        if (ms != null)
                        {
                            foreach (ObjectId entId in ms)
                            {
                                if (entId.IsValid && !entId.IsErased)
                                {
                                    var ent = tr.GetObject(entId, OpenMode.ForWrite) as CadEntity;
                                    if (ent != null)
                                    {
                                        RemoveAllPropertySetsFromEntity(ent, tr);
                                    }
                                }
                            }
                        }
                    }

                    if (isNewOrTempDb)
                    {
                        PurgeCorridorPropertySetDefinitions(db, tr);
                    }

                    tr.Commit();
                }
            }
            catch { }
        }

        /// <summary>
        /// Xóa toàn bộ Property Sets gắn vào đối tượng Solid/Body và làm sạch ExtensionDictionary
        /// </summary>
        private static void RemoveAllPropertySetsFromEntity(CadEntity ent, Transaction tr)
        {
            if (ent == null || ent.IsErased) return;

            // 1. Gỡ và Erase các PropertySet gắn với đối tượng qua AEC PropertyDataServices
            try
            {
                ObjectIdCollection psetIds = PropertyDataServices.GetPropertySets(ent);
                if (psetIds != null && psetIds.Count > 0)
                {
                    foreach (ObjectId psId in psetIds)
                    {
                        if (psId.IsValid && !psId.IsErased)
                        {
                            try
                            {
                                var pset = tr.GetObject(psId, OpenMode.ForWrite) as DBObject;
                                if (pset != null && !pset.IsErased)
                                {
                                    pset.Erase(true);
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }

            // 2. Dọn sạch ExtensionDictionary của Entity (chứa liên kết AEC_PROPERTYSETS...)
            try
            {
                ObjectId extDictId = ent.ExtensionDictionary;
                if (extDictId.IsValid && !extDictId.IsErased)
                {
                    var extDict = tr.GetObject(extDictId, OpenMode.ForWrite) as DBDictionary;
                    if (extDict != null && !extDict.IsErased)
                    {
                        var keysToRemove = new List<string>();
                        foreach (DBDictionaryEntry entry in extDict)
                        {
                            string keyUpper = entry.Key.ToUpperInvariant();
                            if (keyUpper.Contains("PROPERTY") || keyUpper.Contains("AEC") || keyUpper.Contains("CORRIDOR"))
                            {
                                keysToRemove.Add(entry.Key);
                            }
                        }

                        foreach (string key in keysToRemove)
                        {
                            try
                            {
                                ObjectId entryId = extDict.GetAt(key);
                                if (entryId.IsValid && !entryId.IsErased)
                                {
                                    var entryObj = tr.GetObject(entryId, OpenMode.ForWrite);
                                    if (entryObj != null && !entryObj.IsErased)
                                    {
                                        entryObj.Erase(true);
                                    }
                                }
                                extDict.Remove(key);
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }

            // 3. Xóa XData mở rộng nếu có
            try
            {
                if (ent.XData != null)
                {
                    using (var rb = new ResultBuffer())
                    {
                        rb.Add(new TypedValue((int)DxfCode.ExtendedDataRegAppName, REG_APP_NAME));
                        ent.XData = rb;
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Xóa sạch các bản ghi PropertySetDefinition sinh ra từ Corridor khỏi Database
        /// </summary>
        private static void PurgeCorridorPropertySetDefinitions(Database db, Transaction tr)
        {
            try
            {
                DictionaryPropertySetDefinitions propSetDefs = new DictionaryPropertySetDefinitions(db);
                ObjectIdCollection recs = propSetDefs.Records;
                if (recs != null && recs.Count > 0)
                {
                    var idsToErase = new List<ObjectId>();
                    foreach (ObjectId id in recs)
                    {
                        if (id.IsValid && !id.IsErased)
                        {
                            try
                            {
                                if (tr.GetObject(id, OpenMode.ForRead) is PropertySetDefinition def && !def.IsErased)
                                {
                                    string defName = def.Name ?? "";
                                    // Xóa các PropertySet mặc định của Corridor
                                    if (defName.IndexOf("Corridor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        defName.IndexOf("Shape", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        defName.IndexOf("Model", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        defName.IndexOf("Civil", StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        idsToErase.Add(id);
                                    }
                                }
                            }
                            catch { }
                        }
                    }

                    foreach (ObjectId id in idsToErase)
                    {
                        try
                        {
                            var def = tr.GetObject(id, OpenMode.ForWrite) as DBObject;
                            if (def != null && !def.IsErased)
                            {
                                def.Erase(true);
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Đọc PropertySet "Corridor Shape Information" từ mỗi entity trong tempDb/targetDb
        /// để trích xuất SubassemblyName (tên cấu kiện) + CodeName (tên shape/link)
        /// và tạo tên layer theo format: SubassemblyName_CodeName
        /// Phải gọi TRƯỚC khi dọn PropertySets.
        /// </summary>
        private static Dictionary<ObjectId, string> ReadComponentLayerInfo(Database db)
        {
            var result = new Dictionary<ObjectId, string>();
            try
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                    foreach (ObjectId entId in ms)
                    {
                        if (!entId.IsValid || entId.IsErased) continue;
                        var ent = tr.GetObject(entId, OpenMode.ForRead) as CadEntity;
                        if (ent == null || !(ent is Solid3d || ent is Body)) continue;

                        string componentName = "";
                        string codeName = "";

                        try
                        {
                            ObjectIdCollection psetIds = PropertyDataServices.GetPropertySets(ent);
                            if (psetIds != null && psetIds.Count > 0)
                            {
                                foreach (ObjectId psId in psetIds)
                                {
                                    if (!psId.IsValid || psId.IsErased) continue;
                                    var pset = tr.GetObject(psId, OpenMode.ForRead) as PropertySet;
                                    if (pset == null) continue;

                                    // Tìm PropertySet có chứa thông tin Corridor Shape/Link
                                    PropertySetDefinition? psDef = null;
                                    try
                                    {
                                        psDef = tr.GetObject(pset.PropertySetDefinition, OpenMode.ForRead) as PropertySetDefinition;
                                    }
                                    catch { continue; }

                                    if (psDef == null) continue;

                                    // Duyệt tất cả property definitions trong PropertySet
                                    for (int i = 0; i < psDef.Definitions.Count; i++)
                                    {
                                        try
                                        {
                                            var propDef = psDef.Definitions[i];
                                            string propName = propDef.Name ?? "";
                                            string propNameUpper = propName.ToUpperInvariant();

                                            object? val = null;
                                            try { val = pset.GetAt(pset.PropertyNameToId(propName)); }
                                            catch { continue; }

                                            string valStr = val?.ToString()?.Trim() ?? "";
                                            if (string.IsNullOrEmpty(valStr)) continue;

                                            // Chỉ lấy tên cấu kiện, không lấy SubassemblyHandle/ComponentHandle.
                                            // Các handle này cũng chứa chuỗi "SUBASSEMBLY"/"COMPONENT"
                                            // nhưng không phải tên dùng để đặt layer.
                                            if (propNameUpper == "SUBASSEMBLYNAME" ||
                                                propNameUpper == "COMPONENTNAME")
                                            {
                                                componentName = valStr;
                                            }

                                            // Chỉ lấy CodeName/ShapeCode/LinkCode làm phần thứ hai của layer.
                                            if (propNameUpper == "CODENAME" ||
                                                propNameUpper == "SHAPECODE" ||
                                                propNameUpper == "LINKCODE")
                                            {
                                                codeName = valStr;
                                            }
                                        }
                                        catch { }
                                    }
                                }
                            }
                        }
                        catch { }

                        // Nếu không đọc được componentName từ PropertySet, dùng layer hiện tại (chính là Code)
                        if (string.IsNullOrEmpty(codeName))
                        {
                            codeName = ent.Layer;
                        }

                        // Xây dựng tên layer: ComponentName_ShapeCode
                        string layerName;
                        if (!string.IsNullOrEmpty(componentName))
                        {
                            // Loại bỏ ký tự không hợp lệ cho tên layer
                            layerName = SanitizeLayerName($"{componentName}_{codeName}");
                        }
                        else
                        {
                            // Fallback: giữ nguyên layer gốc (Code name)
                            layerName = SanitizeLayerName(codeName);
                        }

                        if (!string.IsNullOrEmpty(layerName))
                        {
                            result[entId] = layerName;
                        }
                    }

                    tr.Commit();
                }
            }
            catch { }
            return result;
        }

        /// <summary>
        /// Loại bỏ các ký tự không hợp lệ cho tên layer trong AutoCAD
        /// </summary>
        private static string SanitizeLayerName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "0";

            // AutoCAD không chấp nhận các ký tự này trong tên layer.
            // Đồng thời loại bỏ ký tự điều khiển có thể xuất hiện trong Property Set.
            char[] invalid = { '<', '>', '/', '\\', '"', ':', ';', '?', '*', '|', '=', '`', ',' };
            var builder = new System.Text.StringBuilder(name.Length);
            foreach (char c in name)
            {
                if (char.IsControl(c) || invalid.Contains(c))
                    builder.Append('_');
                else
                    builder.Append(c);
            }

            string sanitized = builder.ToString().Trim();
            if (string.IsNullOrWhiteSpace(sanitized)) return "0";

            // Tên layer AutoCAD tối đa 255 ký tự.
            return sanitized.Length > 255 ? sanitized.Substring(0, 255).TrimEnd() : sanitized;
        }

        /// <summary>
        /// Đảm bảo layer tồn tại trong database, tạo mới nếu chưa có
        /// </summary>
        private static void EnsureLayerExists(Database db, Transaction tr, string layerName)
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (!lt.Has(layerName))
            {
                lt.UpgradeOpen();
                var ltr = new LayerTableRecord
                {
                    Name = layerName
                };
                lt.Add(ltr);
                tr.AddNewlyCreatedDBObject(ltr, true);
            }
        }

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

        private static void ShowExportResult(Editor ed, string corridorName, string outputFilePath, int solidCount, int bodyCount, int totalEntities, bool attachXData, bool openAfterExport, bool openFolderAfterExport)
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
            ed.WriteMessage($"\n  • Trạng thái PSet:    {(attachXData ? "Đã đính kèm Property Sets & XData" : "Đã loại bỏ toàn bộ Property Sets mặc định")}");
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

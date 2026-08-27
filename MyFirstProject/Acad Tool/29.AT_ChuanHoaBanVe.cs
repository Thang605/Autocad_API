// (C) Copyright 2026 by T27
//
using System;
using System.IO;
using System.Collections.Generic;

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.GraphicsInterface;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Color = Autodesk.AutoCAD.Colors.Color;
using Exception = System.Exception;

[assembly: CommandClass(typeof(Civil3DCsharp.ChuanHoaBanVeCmd))]

namespace Civil3DCsharp
{
    public class ChuanHoaBanVeCmd
    {
        [CommandMethod("AT_ChuanHoaBanVe")]
        [CommandMethod("CHBV")]
        [CommandMethod("AT_StandardizeDrawing")]
        public void ChuanHoaBanVeCommand()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            using (var form = new ChuanHoaBanVeForm())
            {
                Application.ShowModalDialog(form);
            }
        }

        #region Logic Thực Thi Chuẩn Hóa Theo Tiêu Chuẩn T27
        public static void ExecuteT27Standards(
            List<string> selectedLayers,
            bool applyTextStyles,
            bool applyDimStyles,
            bool applyMLeaderStyles,
            bool applyLinetypes,
            bool setCurrent,
            bool purgeUnused,
            bool applySysVars,
            Action<string> log)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var db = doc.Database;
            var ed = doc.Editor;

            using (var docLock = doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                // 1. Nạp Linetypes
                if (applyLinetypes)
                {
                    log("Đang nạp các đường nét chuẩn (Linetypes)...");
                    EnsureLinetypes(db, tr, log);
                }

                // 2. Tạo / Cập nhật Layers
                if (selectedLayers != null && selectedLayers.Count > 0)
                {
                    log($"Đang thiết lập {selectedLayers.Count} Layer chuẩn T27...");
                    CreateT27Layers(db, tr, selectedLayers, log);
                }

                // 3. Tạo Text Styles
                ObjectId romanStyleId = ObjectId.Null;
                ObjectId arialStyleId = ObjectId.Null;
                if (applyTextStyles)
                {
                    log("Đang thiết lập các Text Style chuẩn T27...");
                    CreateT27TextStyles(db, tr, out romanStyleId, out arialStyleId, log);
                }
                else
                {
                    romanStyleId = db.Textstyle;
                }

                // 4. Tạo Dim Styles
                ObjectId defaultDimStyleId = ObjectId.Null;
                if (applyDimStyles)
                {
                    log("Đang thiết lập các Dimension Style chuẩn T27...");
                    ObjectId baseTextStyleId = !romanStyleId.IsNull ? romanStyleId : db.Textstyle;
                    defaultDimStyleId = CreateT27DimStyles(db, tr, baseTextStyleId, log);
                }

                // 5. Tạo MLeader Style
                if (applyMLeaderStyles)
                {
                    log("Đang thiết lập Multileader Style chuẩn T27...");
                    ObjectId baseTextStyleId = !romanStyleId.IsNull ? romanStyleId : db.Textstyle;
                    CreateT27MLeaderStyle(db, tr, baseTextStyleId, log);
                }

                // 6. Đặt Hiện Hành (Set Current)
                if (setCurrent)
                {
                    log("Đang thiết lập Layer, TextStyle, DimStyle hiện hành...");
                    SetCurrentStyles(db, tr, "T27_NET_THAY", romanStyleId, defaultDimStyleId, log);
                }

                tr.Commit();
            }

            // 7. Thiết lập biến hệ thống
            if (applySysVars)
            {
                log("Đang tối ưu các biến hệ thống CAD (LTSCALE, CELTSCALE, DIMASSOC)...");
                ApplySystemVariables(log);
            }

            // 8. Purge rác
            if (purgeUnused)
            {
                log("Đang dọn dẹp các đối tượng rác không dùng (Purge)...");
                PurgeDrawing(db, log);
            }

            ed.Regen();
        }

        private static void EnsureLinetypes(Database db, Transaction tr, Action<string> log)
        {
            var ltTable = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForWrite);
            string[] lineTypes = new string[] { "CENTER", "CENTER2", "HIDDEN", "HIDDEN2", "DASHED", "PHANTOM", "PHANTOM2" };

            foreach (var ltName in lineTypes)
            {
                if (!ltTable.Has(ltName))
                {
                    try
                    {
                        db.LoadLineTypeFile(ltName, "acad.lin");
                        log($"  + Đã nạp Linetype: {ltName}");
                    }
                    catch
                    {
                        try
                        {
                            db.LoadLineTypeFile(ltName, "acadiso.lin");
                            log($"  + Đã nạp Linetype: {ltName} (từ acadiso.lin)");
                        }
                        catch
                        {
                            // Ignore if linetype file not found
                        }
                    }
                }
            }
        }

        private static void CreateT27Layers(Database db, Transaction tr, List<string> layerNames, Action<string> log)
        {
            var ltTable = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
            var layTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForWrite);

            var layerDefs = new Dictionary<string, (short colorIndex, string linetype, LineWeight weight, string desc, bool plottable)>
            {
                ["T27_NET_THAY"] = (7, "Continuous", LineWeight.LineWeight035, "Nét thấy chính công trình", true),
                ["T27_NET_KHUAT"] = (8, "HIDDEN", LineWeight.LineWeight015, "Nét khuất", true),
                ["T27_NET_TRUC"] = (1, "CENTER", LineWeight.LineWeight015, "Nét trục tâm", true),
                ["T27_NET_MANH"] = (9, "Continuous", LineWeight.LineWeight013, "Nét mảnh, nét gióng", true),
                ["T27_DIM"] = (3, "Continuous", LineWeight.LineWeight015, "Kích thước Dimension", true),
                ["T27_TEXT"] = (2, "Continuous", LineWeight.LineWeight020, "Văn bản chữ ghi chú", true),
                ["T27_TEXT_TIEUDE"] = (4, "Continuous", LineWeight.LineWeight035, "Tiêu đề bản vẽ", true),
                ["T27_HATCH"] = (8, "Continuous", LineWeight.LineWeight009, "Mặt cắt Hatch", true),
                ["T27_KHUNG_TEN"] = (4, "Continuous", LineWeight.LineWeight050, "Khung bản vẽ & Khung tên", true),
                ["T27_TIM_DUONG"] = (1, "CENTER2", LineWeight.LineWeight035, "Tim tuyến đường", true),
                ["T27_MEP_DUONG"] = (7, "Continuous", LineWeight.LineWeight035, "Mép mặt đường", true),
                ["T27_VIA_HE"] = (3, "Continuous", LineWeight.LineWeight025, "Vỉa hè, bó vỉa", true),
                ["T27_TALUY"] = (8, "Continuous", LineWeight.LineWeight015, "Đường chân/đỉnh taluy", true),
                ["T27_THOAT_NUOC"] = (5, "Continuous", LineWeight.LineWeight030, "Cống & rãnh thoát nước", true),
                ["Defpoints"] = (7, "Continuous", LineWeight.LineWeight000, "Layer không in", false)
            };

            foreach (var name in layerNames)
            {
                if (!layerDefs.TryGetValue(name, out var def))
                {
                    def = (7, "Continuous", LineWeight.LineWeight025, "Layer T27", true);
                }

                ObjectId linetypeId = db.ContinuousLinetype;
                if (ltTable.Has(def.linetype))
                {
                    linetypeId = ltTable[def.linetype];
                }

                LayerTableRecord ltr;
                if (layTable.Has(name))
                {
                    ltr = (LayerTableRecord)tr.GetObject(layTable[name], OpenMode.ForWrite);
                    ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, def.colorIndex);
                    ltr.LinetypeObjectId = linetypeId;
                    ltr.LineWeight = def.weight;
                    ltr.Description = def.desc;
                    ltr.IsPlottable = def.plottable;
                    log($"  ~ Cập nhật Layer: {name} (Color {def.colorIndex}, {def.linetype}, {def.weight})");
                }
                else
                {
                    ltr = new LayerTableRecord
                    {
                        Name = name,
                        Color = Color.FromColorIndex(ColorMethod.ByAci, def.colorIndex),
                        LinetypeObjectId = linetypeId,
                        LineWeight = def.weight,
                        Description = def.desc,
                        IsPlottable = def.plottable
                    };
                    layTable.Add(ltr);
                    tr.AddNewlyCreatedDBObject(ltr, true);
                    log($"  + Tạo mới Layer: {name} (Color {def.colorIndex}, {def.linetype}, {def.weight})");
                }
            }
        }

        private static void CreateT27TextStyles(Database db, Transaction tr, out ObjectId romanStyleId, out ObjectId arialStyleId, Action<string> log)
        {
            var txtTable = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForWrite);
            romanStyleId = ObjectId.Null;
            arialStyleId = ObjectId.Null;

            // 1. T27_RomanS
            string nameRomans = "T27_RomanS";
            TextStyleTableRecord tsRomans;
            if (txtTable.Has(nameRomans))
            {
                tsRomans = (TextStyleTableRecord)tr.GetObject(txtTable[nameRomans], OpenMode.ForWrite);
                tsRomans.FileName = "romans.shx";
                tsRomans.BigFontFileName = "vncomplex.shx";
                tsRomans.XScale = 0.85;
                tsRomans.TextSize = 0.0;
                log($"  ~ Cập nhật TextStyle: {nameRomans} (romans.shx + vncomplex.shx, Width 0.85)");
            }
            else
            {
                tsRomans = new TextStyleTableRecord
                {
                    Name = nameRomans,
                    FileName = "romans.shx",
                    BigFontFileName = "vncomplex.shx",
                    XScale = 0.85,
                    TextSize = 0.0
                };
                txtTable.Add(tsRomans);
                tr.AddNewlyCreatedDBObject(tsRomans, true);
                log($"  + Tạo mới TextStyle: {nameRomans} (romans.shx + vncomplex.shx, Width 0.85)");
            }
            romanStyleId = tsRomans.ObjectId;

            // 2. T27_Arial
            string nameArial = "T27_Arial";
            TextStyleTableRecord tsArial;
            if (txtTable.Has(nameArial))
            {
                tsArial = (TextStyleTableRecord)tr.GetObject(txtTable[nameArial], OpenMode.ForWrite);
                tsArial.Font = new FontDescriptor("Arial", false, false, 0, 0);
                tsArial.XScale = 1.0;
                tsArial.TextSize = 0.0;
                log($"  ~ Cập nhật TextStyle: {nameArial} (Arial Unicode, Width 1.0)");
            }
            else
            {
                tsArial = new TextStyleTableRecord
                {
                    Name = nameArial,
                    Font = new FontDescriptor("Arial", false, false, 0, 0),
                    XScale = 1.0,
                    TextSize = 0.0
                };
                txtTable.Add(tsArial);
                tr.AddNewlyCreatedDBObject(tsArial, true);
                log($"  + Tạo mới TextStyle: {nameArial} (Arial Unicode, Width 1.0)");
            }
            arialStyleId = tsArial.ObjectId;

            // 3. T27_TimesNewRoman
            string nameTimes = "T27_TimesNewRoman";
            if (txtTable.Has(nameTimes))
            {
                var ts = (TextStyleTableRecord)tr.GetObject(txtTable[nameTimes], OpenMode.ForWrite);
                ts.Font = new FontDescriptor("Times New Roman", false, false, 0, 0);
                ts.XScale = 1.0;
            }
            else
            {
                var ts = new TextStyleTableRecord
                {
                    Name = nameTimes,
                    Font = new FontDescriptor("Times New Roman", false, false, 0, 0),
                    XScale = 1.0,
                    TextSize = 0.0
                };
                txtTable.Add(ts);
                tr.AddNewlyCreatedDBObject(ts, true);
                log($"  + Tạo mới TextStyle: {nameTimes}");
            }

            // 4. T27_TieuDe
            string nameTieuDe = "T27_TieuDe";
            if (txtTable.Has(nameTieuDe))
            {
                var ts = (TextStyleTableRecord)tr.GetObject(txtTable[nameTieuDe], OpenMode.ForWrite);
                ts.Font = new FontDescriptor("Arial", true, false, 0, 0);
                ts.XScale = 0.85;
            }
            else
            {
                var ts = new TextStyleTableRecord
                {
                    Name = nameTieuDe,
                    Font = new FontDescriptor("Arial", true, false, 0, 0),
                    XScale = 0.85,
                    TextSize = 0.0
                };
                txtTable.Add(ts);
                tr.AddNewlyCreatedDBObject(ts, true);
                log($"  + Tạo mới TextStyle: {nameTieuDe}");
            }
        }

        private static ObjectId CreateT27DimStyles(Database db, Transaction tr, ObjectId textStyleId, Action<string> log)
        {
            var dimTable = (DimStyleTable)tr.GetObject(db.DimStyleTableId, OpenMode.ForWrite);
            ObjectId defaultDimId = ObjectId.Null;

            var scales = new Dictionary<string, double>
            {
                ["T27_1-100"] = 100.0,
                ["T27_1-50"] = 50.0,
                ["T27_1-200"] = 200.0,
                ["T27_1-500"] = 500.0,
                ["T27_1-1000"] = 1000.0,
                ["T27_1-25"] = 25.0,
                ["T27_1-20"] = 20.0,
                ["T27_1-10"] = 10.0,
                ["T27_Annotative"] = 1.0
            };

            foreach (var kv in scales)
            {
                string dimName = kv.Key;
                double scale = kv.Value;

                DimStyleTableRecord dstr;
                if (dimTable.Has(dimName))
                {
                    dstr = (DimStyleTableRecord)tr.GetObject(dimTable[dimName], OpenMode.ForWrite);
                    ApplyDimStyleProperties(dstr, textStyleId, scale);
                    log($"  ~ Cập nhật DimStyle: {dimName} (Scale {scale})");
                }
                else
                {
                    dstr = new DimStyleTableRecord
                    {
                        Name = dimName
                    };
                    ApplyDimStyleProperties(dstr, textStyleId, scale);
                    dimTable.Add(dstr);
                    tr.AddNewlyCreatedDBObject(dstr, true);
                    log($"  + Tạo mới DimStyle: {dimName} (Scale {scale})");
                }

                if (dimName == "T27_1-100")
                {
                    defaultDimId = dstr.ObjectId;
                }
            }

            return defaultDimId;
        }

        private static void ApplyDimStyleProperties(DimStyleTableRecord dstr, ObjectId textStyleId, double scale)
        {
            dstr.Dimtxt = 2.5;                       // Text height = 2.5mm
            dstr.Dimasz = 2.0;                       // Arrow size = 2.0mm
            dstr.Dimexe = 1.5;                       // Extension line extension = 1.5mm
            dstr.Dimexo = 1.0;                       // Extension line offset = 1.0mm
            dstr.Dimgap = 1.0;                       // Gap from dimension line to text = 1.0mm
            dstr.Dimscale = scale;                   // Scale factor
            dstr.Dimtad = 1;                         // Place text above dimension line
            dstr.Dimtih = false;                     // Align text with dimension line inside
            dstr.Dimtoh = false;                     // Align text with dimension line outside
            dstr.Dimtxsty = textStyleId;             // Text Style
            dstr.Dimclrd = Color.FromColorIndex(ColorMethod.ByAci, 3); // Green dimension line
            dstr.Dimclre = Color.FromColorIndex(ColorMethod.ByAci, 3); // Green extension line
            dstr.Dimclrt = Color.FromColorIndex(ColorMethod.ByAci, 2); // Yellow text
            dstr.Dimlfac = 1.0;                      // Linear measurement factor
            dstr.Dimdec = 2;                         // Decimal places
            dstr.Dimdsep = '.';                      // Decimal separator
            dstr.Dimzin = 8;                         // Suppress trailing zeros
            dstr.Dimtmove = 1;                       // Add leader when text is moved
        }

        private static void CreateT27MLeaderStyle(Database db, Transaction tr, ObjectId textStyleId, Action<string> log)
        {
            try
            {
                var mlDict = (DBDictionary)tr.GetObject(db.MLeaderStyleDictionaryId, OpenMode.ForWrite);
                string styleName = "T27_MLeader";

                MLeaderStyle mlStyle;
                if (mlDict.Contains(styleName))
                {
                    mlStyle = (MLeaderStyle)tr.GetObject(mlDict.GetAt(styleName), OpenMode.ForWrite);
                    ConfigureMLeaderStyle(mlStyle, textStyleId);
                    log($"  ~ Cập nhật MLeaderStyle: {styleName}");
                }
                else
                {
                    mlStyle = new MLeaderStyle();
                    ConfigureMLeaderStyle(mlStyle, textStyleId);
                    mlDict.SetAt(styleName, mlStyle);
                    tr.AddNewlyCreatedDBObject(mlStyle, true);
                    log($"  + Tạo mới MLeaderStyle: {styleName}");
                }
            }
            catch (Exception ex)
            {
                log($"  ! Lưu ý MLeaderStyle: {ex.Message}");
            }
        }

        private static void ConfigureMLeaderStyle(MLeaderStyle mlStyle, ObjectId textStyleId)
        {
            mlStyle.TextStyleId = textStyleId;
            mlStyle.TextHeight = 2.5;
            mlStyle.ArrowSize = 2.0;
            mlStyle.LandingGap = 1.5;
            mlStyle.EnableLanding = true;
            mlStyle.EnableDogleg = true;
            mlStyle.DoglegLength = 3.0;
            mlStyle.LeaderLineColor = Color.FromColorIndex(ColorMethod.ByAci, 3);
            mlStyle.TextColor = Color.FromColorIndex(ColorMethod.ByAci, 2);
        }

        private static void SetCurrentStyles(Database db, Transaction tr, string layerName, ObjectId textStyleId, ObjectId dimStyleId, Action<string> log)
        {
            var layTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (layTable.Has(layerName))
            {
                db.Clayer = layTable[layerName];
                log($"  -> Đã đặt Layer hiện hành: {layerName}");
            }

            if (!textStyleId.IsNull && textStyleId.IsValid)
            {
                db.Textstyle = textStyleId;
                log("  -> Đã đặt TextStyle hiện hành: T27_RomanS");
            }

            if (!dimStyleId.IsNull && dimStyleId.IsValid)
            {
                db.Dimstyle = dimStyleId;
                db.SetDimstyleData(new DimStyleTableRecord());
                log("  -> Đã đặt DimStyle hiện hành: T27_1-100");
            }
        }
        #endregion

        #region Logic Import Từ File Mẫu DWG / DWT
        public static void ExecuteImportFromTemplate(
            string templatePath,
            bool importLayers,
            bool importTextStyles,
            bool importDimStyles,
            bool importMLeaderStyles,
            bool importBlocks,
            bool overwrite,
            bool setCurrent,
            bool purgeUnused,
            bool applySysVars,
            Action<string> log)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var destDb = doc.Database;
            var ed = doc.Editor;

            log($"Mở file bản vẽ mẫu: {Path.GetFileName(templatePath)}...");
            int successCount = 0;
            int skipCount = 0;
            var duplicateBehavior = overwrite ? DuplicateRecordCloning.Replace : DuplicateRecordCloning.Ignore;

            using (var sourceDb = new Database(false, true))
            {
                sourceDb.ReadDwgFile(templatePath, FileShare.Read, true, "");

                using (var docLock = doc.LockDocument())
                {
                    // 1. Layers
                    if (importLayers)
                    {
                        ImportTableRecords<LayerTable, LayerTableRecord>(
                            sourceDb, destDb, db => db.LayerTableId,
                            duplicateBehavior, "Layer", ref successCount, ref skipCount, log);
                    }

                    // 2. TextStyles
                    if (importTextStyles)
                    {
                        ImportTableRecords<TextStyleTable, TextStyleTableRecord>(
                            sourceDb, destDb, db => db.TextStyleTableId,
                            duplicateBehavior, "TextStyle", ref successCount, ref skipCount, log);
                    }

                    // 3. DimStyles
                    if (importDimStyles)
                    {
                        ImportTableRecords<DimStyleTable, DimStyleTableRecord>(
                            sourceDb, destDb, db => db.DimStyleTableId,
                            duplicateBehavior, "DimStyle", ref successCount, ref skipCount, log);
                    }

                    // 4. Blocks
                    if (importBlocks)
                    {
                        ImportTableRecords<BlockTable, BlockTableRecord>(
                            sourceDb, destDb, db => db.BlockTableId,
                            duplicateBehavior, "Block", ref successCount, ref skipCount, log);
                    }

                    // 5. MLeader Styles
                    if (importMLeaderStyles)
                    {
                        ImportDictionaryEntries(
                            sourceDb, destDb, "ACAD_MLEADERSTYLE",
                            duplicateBehavior, ref successCount, ref skipCount, log);
                    }
                }
            }

            log($"  ✅ Tổng cộng: Đồng bộ {successCount} đối tượng, Bỏ qua {skipCount} đối tượng.");

            if (applySysVars)
            {
                ApplySystemVariables(log);
            }

            if (purgeUnused)
            {
                PurgeDrawing(destDb, log);
            }

            ed.Regen();
        }

        private static void ImportTableRecords<TTable, TRecord>(
            Database sourceDb,
            Database destDb,
            Func<Database, ObjectId> getTableIdFunc,
            DuplicateRecordCloning duplicateBehavior,
            string categoryName,
            ref int successCount,
            ref int skipCount,
            Action<string> log)
            where TTable : SymbolTable
            where TRecord : SymbolTableRecord
        {
            try
            {
                using (var trSource = sourceDb.TransactionManager.StartTransaction())
                {
                    ObjectId sourceTableId = getTableIdFunc(sourceDb);
                    var sourceTable = trSource.GetObject(sourceTableId, OpenMode.ForRead) as TTable;
                    if (sourceTable == null) return;

                    using (var trDest = destDb.TransactionManager.StartTransaction())
                    {
                        ObjectId destTableId = getTableIdFunc(destDb);
                        var destTable = trDest.GetObject(destTableId, OpenMode.ForRead) as TTable;
                        if (destTable == null) return;

                        var idsToClone = new ObjectIdCollection();
                        foreach (ObjectId recordId in sourceTable)
                        {
                            var record = trSource.GetObject(recordId, OpenMode.ForRead) as TRecord;
                            if (record == null) continue;

                            string name = record.Name;
                            if (IsStandardRecord(name, categoryName)) continue;

                            if (categoryName == "Block")
                            {
                                var btr = record as BlockTableRecord;
                                if (btr != null && (btr.IsLayout || name.StartsWith("*")))
                                    continue;
                            }

                            if (destTable.Has(name))
                            {
                                if (duplicateBehavior == DuplicateRecordCloning.Ignore)
                                {
                                    skipCount++;
                                    log($"  [Bỏ qua] {categoryName}: '{name}' đã có");
                                    continue;
                                }
                                else
                                {
                                    log($"  [Cập nhật] {categoryName}: '{name}'");
                                }
                            }
                            else
                            {
                                log($"  [Thêm mới] {categoryName}: '{name}'");
                            }
                            idsToClone.Add(recordId);
                        }

                        if (idsToClone.Count > 0)
                        {
                            var idMap = new IdMapping();
                            sourceDb.WblockCloneObjects(idsToClone, destTableId, idMap, duplicateBehavior, false);
                            successCount += idsToClone.Count;
                        }

                        trDest.Commit();
                    }
                    trSource.Commit();
                }
            }
            catch (Exception ex)
            {
                log($"  ! Lỗi Import {categoryName}: {ex.Message}");
            }
        }

        private static void ImportDictionaryEntries(
            Database sourceDb,
            Database destDb,
            string dictName,
            DuplicateRecordCloning duplicateBehavior,
            ref int successCount,
            ref int skipCount,
            Action<string> log)
        {
            try
            {
                using (var trSource = sourceDb.TransactionManager.StartTransaction())
                {
                    var sourceNamedDict = (DBDictionary)trSource.GetObject(sourceDb.NamedObjectsDictionaryId, OpenMode.ForRead);
                    if (!sourceNamedDict.Contains(dictName)) return;

                    var sourceDict = (DBDictionary)trSource.GetObject(sourceNamedDict.GetAt(dictName), OpenMode.ForRead);

                    using (var trDest = destDb.TransactionManager.StartTransaction())
                    {
                        var destNamedDict = (DBDictionary)trDest.GetObject(destDb.NamedObjectsDictionaryId, OpenMode.ForWrite);
                        DBDictionary destDict;
                        if (destNamedDict.Contains(dictName))
                        {
                            destDict = (DBDictionary)trDest.GetObject(destNamedDict.GetAt(dictName), OpenMode.ForWrite);
                        }
                        else
                        {
                            destDict = new DBDictionary();
                            destNamedDict.SetAt(dictName, destDict);
                            trDest.AddNewlyCreatedDBObject(destDict, true);
                        }

                        var idsToClone = new ObjectIdCollection();
                        foreach (DBDictionaryEntry entry in sourceDict)
                        {
                            string entryKey = entry.Key;
                            if (destDict.Contains(entryKey))
                            {
                                if (duplicateBehavior == DuplicateRecordCloning.Ignore)
                                {
                                    skipCount++;
                                    continue;
                                }
                            }
                            idsToClone.Add(entry.Value);
                        }

                        if (idsToClone.Count > 0)
                        {
                            var idMap = new IdMapping();
                            sourceDb.WblockCloneObjects(idsToClone, destDict.ObjectId, idMap, duplicateBehavior, false);
                            successCount += idsToClone.Count;
                            log($"  [Đồng bộ] Dictionary '{dictName}': {idsToClone.Count} mục.");
                        }

                        trDest.Commit();
                    }
                    trSource.Commit();
                }
            }
            catch (Exception ex)
            {
                log($"  ! Lỗi Import Dictionary {dictName}: {ex.Message}");
            }
        }

        private static bool IsStandardRecord(string name, string category)
        {
            if (category == "Layer")
                return name == "0" || name == "Defpoints";
            if (category == "TextStyle")
                return name.Equals("Standard", StringComparison.OrdinalIgnoreCase);
            if (category == "DimStyle")
                return name.Equals("Standard", StringComparison.OrdinalIgnoreCase);
            if (category == "Linetype")
                return name.Equals("ByBlock", StringComparison.OrdinalIgnoreCase) ||
                       name.Equals("ByLayer", StringComparison.OrdinalIgnoreCase) ||
                       name.Equals("Continuous", StringComparison.OrdinalIgnoreCase);
            return false;
        }
        #endregion

        #region Helper Functions
        public static void ExecuteSystemSettings(bool setCurrent, bool purgeUnused, bool applySysVars, Action<string> log)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var db = doc.Database;

            if (applySysVars) ApplySystemVariables(log);
            if (purgeUnused) PurgeDrawing(db, log);
        }

        private static void ApplySystemVariables(Action<string> log)
        {
            try
            {
                Application.SetSystemVariable("LTSCALE", 1.0);
                Application.SetSystemVariable("CELTSCALE", 1.0);
                Application.SetSystemVariable("DIMASSOC", 2);
                Application.SetSystemVariable("MEASUREMENT", 1); // Metric
                log("  -> Đã thiết lập: LTSCALE=1, CELTSCALE=1, DIMASSOC=2, MEASUREMENT=1 (Metric)");
            }
            catch (Exception ex)
            {
                log($"  ! Cảnh báo System Variables: {ex.Message}");
            }
        }

        private static void PurgeDrawing(Database db, Action<string> log)
        {
            try
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var idCol = new ObjectIdCollection();
                    var layTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    foreach (ObjectId id in layTable) idCol.Add(id);

                    var blkTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    foreach (ObjectId id in blkTable) idCol.Add(id);

                    var txtTable = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                    foreach (ObjectId id in txtTable) idCol.Add(id);

                    var dimTable = (DimStyleTable)tr.GetObject(db.DimStyleTableId, OpenMode.ForRead);
                    foreach (ObjectId id in dimTable) idCol.Add(id);

                    db.Purge(idCol);
                    log($"  -> Đã lọc và dọn dẹp các đối tượng rác thành công.");
                    tr.Commit();
                }
            }
            catch (Exception ex)
            {
                log($"  ! Cảnh báo Purge: {ex.Message}");
            }
        }
        #endregion
    }
}

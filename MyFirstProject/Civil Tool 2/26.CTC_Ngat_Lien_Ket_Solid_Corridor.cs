using System;
using System.Collections.Generic;
using System.Windows.Forms;

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.Aec.PropertyData.DatabaseServices;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using CadEntity = Autodesk.AutoCAD.DatabaseServices.Entity;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;
using Exception = System.Exception;

[assembly: CommandClass(typeof(Civil3DCsharp.CTC_Ngat_Lien_Ket_Solid_Corridor_Command))]

namespace Civil3DCsharp
{
    public class CTC_Ngat_Lien_Ket_Solid_Corridor_Command
    {
        [CommandMethod("CTC_Ngat_Lien_Ket_Solid_Corridor")]
        public static void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var db = doc.Database;
            var ed = doc.Editor;

            ed.WriteMessage("\n=======================================================");
            ed.WriteMessage("\n [CTC] NGẮT LIÊN KẾT ĐỘNG & DỌN DẸP 3D SOLID / BODY CORRIDOR");
            ed.WriteMessage("\n=======================================================\n");

            using (var form = new MyFirstProject.Civil_Tool_2.NgatLienKetSolidCorridorForm(db, ed))
            {
                var dialogResult = Application.ShowModalDialog(form);
                if (dialogResult != DialogResult.OK || !form.FormAccepted)
                {
                    ed.WriteMessage("\nĐã hủy lệnh.");
                    return;
                }

                bool breakDynamicLink = form.BreakDynamicLink;
                bool deleteEntities = form.DeleteSelectedEntities;
                bool cleanPropertySets = form.CleanPropertySets;
                bool purgePropertySetDefs = form.PurgePropertySetDefs;
                bool autoRebuild = form.AutoRebuild;
                bool scanAllCorridors = form.ScanAllCorridors;
                ObjectId selectedCorridorId = form.SelectedCorridorId;
                List<ObjectId> targetEntityIds = form.SelectedEntityIdsToProcess;

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    try
                    {
                        int removedMgrCount = 0;
                        int deletedSolidCount = 0;
                        int deletedBodyCount = 0;
                        int cleanedEntityCount = 0;
                        int purgedDefCount = 0;
                        int rebuiltCorridorCount = 0;

                        // 1. Ngắt liên kết động (Xóa AeccDbCorridorSolidMgr trong Named Objects Dictionary)
                        if (breakDynamicLink)
                        {
                            removedMgrCount = RemoveCorridorSolidDynamicLinkManager(db, tr, ed);
                        }

                        // 2. Xóa hoặc làm sạch Property Sets trên các đối tượng 3DSOLID / BODY đã chọn
                        if (targetEntityIds != null && targetEntityIds.Count > 0)
                        {
                            foreach (ObjectId entId in targetEntityIds)
                            {
                                if (entId.IsNull || !entId.IsValid || entId.IsErased) continue;

                                var ent = tr.GetObject(entId, OpenMode.ForWrite) as CadEntity;
                                if (ent == null || ent.IsErased) continue;

                                if (deleteEntities)
                                {
                                    bool isSolid = ent is Solid3d;
                                    bool isBody = ent is Body;

                                    RemoveCorridorPropertySetsFromEntity(ent, tr);
                                    ent.Erase(true);

                                    if (isSolid) deletedSolidCount++;
                                    else if (isBody) deletedBodyCount++;
                                }
                                else if (cleanPropertySets)
                                {
                                    if (RemoveCorridorPropertySetsFromEntity(ent, tr))
                                    {
                                        cleanedEntityCount++;
                                    }
                                }
                            }
                        }

                        // 3. Xóa các định nghĩa PropertySetDefinition của Corridor nếu được chọn
                        if (purgePropertySetDefs)
                        {
                            purgedDefCount = PurgeUnusedCorridorPropertySetDefinitions(db, tr);
                        }

                        // 4. Tự động Rebuild Corridor nếu được chọn
                        if (autoRebuild)
                        {
                            var civilDoc = CivilDocument.GetCivilDocument(db);
                            if (scanAllCorridors || selectedCorridorId.IsNull)
                            {
                                foreach (ObjectId corrId in civilDoc.CorridorCollection)
                                {
                                    if (!corrId.IsValid || corrId.IsErased) continue;
                                    if (tr.GetObject(corrId, OpenMode.ForWrite) is Corridor corr)
                                    {
                                        try
                                        {
                                            ed.WriteMessage($"\n🔄 Đang Rebuild Corridor '{corr.Name}'...");
                                            corr.Rebuild();
                                            rebuiltCorridorCount++;
                                        }
                                        catch (Exception exReb)
                                        {
                                            ed.WriteMessage($"\n⚠ Cảnh báo Rebuild Corridor '{corr.Name}': {exReb.Message}");
                                        }
                                    }
                                }
                            }
                            else if (selectedCorridorId.IsValid && !selectedCorridorId.IsErased)
                            {
                                if (tr.GetObject(selectedCorridorId, OpenMode.ForWrite) is Corridor corr)
                                {
                                    try
                                    {
                                        ed.WriteMessage($"\n🔄 Đang Rebuild Corridor '{corr.Name}'...");
                                        corr.Rebuild();
                                        rebuiltCorridorCount++;
                                    }
                                    catch (Exception exReb)
                                    {
                                        ed.WriteMessage($"\n⚠ Cảnh báo Rebuild Corridor '{corr.Name}': {exReb.Message}");
                                    }
                                }
                            }
                        }

                        tr.Commit();

                        ed.WriteMessage("\n-------------------------------------------------------");
                        if (breakDynamicLink)
                        {
                            if (removedMgrCount > 0)
                                ed.WriteMessage($"\n✅ Đã ngắt liên kết động và xóa {removedMgrCount} bộ quản lý AeccDbCorridorSolidMgr.");
                            else
                                ed.WriteMessage("\nℹ Bản vẽ không còn bộ quản lý liên kết động AeccDbCorridorSolidMgr.");
                        }

                        if (deleteEntities && (deletedSolidCount > 0 || deletedBodyCount > 0))
                        {
                            ed.WriteMessage($"\n🗑 Đã xóa: {deletedSolidCount} đối tượng 3DSOLID + {deletedBodyCount} đối tượng BODY (Tổng: {deletedSolidCount + deletedBodyCount}).");
                        }
                        else if (cleanPropertySets && cleanedEntityCount > 0)
                        {
                            ed.WriteMessage($"\n🧹 Đã gỡ bỏ Property Sets của Corridor khỏi {cleanedEntityCount} đối tượng.");
                        }

                        if (purgedDefCount > 0)
                        {
                            ed.WriteMessage($"\n🧹 Đã dọn sạch {purgedDefCount} định nghĩa Property Set Corridor.");
                        }

                        if (rebuiltCorridorCount > 0)
                        {
                            ed.WriteMessage($"\n🔄 Đã Rebuild kiểm chứng {rebuiltCorridorCount} Corridor thành công.");
                        }
                        ed.WriteMessage("\n=======================================================\n");
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception aEx)
                    {
                        ed.WriteMessage($"\n❌ Lỗi AutoCAD: {aEx.Message}");
                        tr.Abort();
                    }
                    catch (Exception ex)
                    {
                        ed.WriteMessage($"\n❌ Lỗi hệ thống: {ex.Message}");
                        tr.Abort();
                    }
                }
            }
        }

        /// <summary>
        /// Tìm và xóa đối tượng quản lý liên kết động AeccDbCorridorSolidMgr trong Named Objects Dictionary
        /// </summary>
        private static int RemoveCorridorSolidDynamicLinkManager(Database db, Transaction tr, Editor ed)
        {
            int removedCount = 0;
            try
            {
                if (!db.NamedObjectsDictionaryId.IsValid || db.NamedObjectsDictionaryId.IsErased)
                    return 0;

                if (tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForWrite) is not DBDictionary nod)
                    return 0;

                var keysToRemove = new List<(string Key, ObjectId ObjId)>();
                foreach (DBDictionaryEntry entry in nod)
                {
                    if (entry.Key.Equals("AeccDbCorridorSolidMgr", StringComparison.OrdinalIgnoreCase) ||
                        entry.Key.IndexOf("CorridorSolidMgr", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        keysToRemove.Add((entry.Key, entry.Value));
                    }
                }

                foreach (var (key, objId) in keysToRemove)
                {
                    try
                    {
                        string handleStr = objId.Handle.ToString();

                        if (!objId.IsNull && objId.IsValid && !objId.IsErased)
                        {
                            var mgrObj = tr.GetObject(objId, OpenMode.ForWrite);
                            if (mgrObj != null && !mgrObj.IsErased)
                            {
                                try
                                {
                                    mgrObj.Erase(true);
                                }
                                catch
                                {
                                    try
                                    {
                                        dynamic comObj = mgrObj.AcadObject;
                                        comObj.Delete();
                                    }
                                    catch { }
                                }
                            }
                        }

                        if (nod.Contains(key))
                        {
                            nod.Remove(key);
                        }

                        removedCount++;
                        ed.WriteMessage($"\n  [x] Đã gỡ bỏ '{key}' (Handle: {handleStr}) khỏi Named Objects Dictionary.");
                    }
                    catch (Exception exItem)
                    {
                        ed.WriteMessage($"\n  ⚠ Không thể gỡ '{key}': {exItem.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n⚠ Cảnh báo khi truy cập Named Objects Dictionary: {ex.Message}");
            }

            return removedCount;
        }

        /// <summary>
        /// Gỡ bỏ Property Sets của Corridor khỏi đối tượng Solid / Body
        /// </summary>
        private static bool RemoveCorridorPropertySetsFromEntity(CadEntity ent, Transaction tr)
        {
            if (ent == null || ent.IsErased) return false;
            bool anyRemoved = false;

            try
            {
                ObjectIdCollection psetIds = PropertyDataServices.GetPropertySets(ent);
                if (psetIds != null && psetIds.Count > 0)
                {
                    foreach (ObjectId psId in psetIds)
                    {
                        if (!psId.IsValid || psId.IsErased) continue;
                        try
                        {
                            var pset = tr.GetObject(psId, OpenMode.ForWrite);
                            if (pset != null && !pset.IsErased)
                            {
                                pset.Erase(true);
                                anyRemoved = true;
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }

            try
            {
                ObjectId extDictId = ent.ExtensionDictionary;
                if (extDictId.IsValid && !extDictId.IsErased)
                {
                    if (tr.GetObject(extDictId, OpenMode.ForWrite) is DBDictionary extDict && !extDict.IsErased)
                    {
                        var keysToRemove = new List<string>();
                        foreach (DBDictionaryEntry entry in extDict)
                        {
                            string kUpper = entry.Key.ToUpperInvariant();
                            if (kUpper.Contains("PROPERTY") || kUpper.Contains("AEC") || kUpper.Contains("CORRIDOR"))
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
                                anyRemoved = true;
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }

            return anyRemoved;
        }

        /// <summary>
        /// Xóa các định nghĩa PropertySetDefinition mặc định của Corridor
        /// </summary>
        private static int PurgeUnusedCorridorPropertySetDefinitions(Database db, Transaction tr)
        {
            int count = 0;
            try
            {
                var propSetDefs = new DictionaryPropertySetDefinitions(db);
                ObjectIdCollection recs = propSetDefs.Records;
                if (recs != null && recs.Count > 0)
                {
                    var idsToErase = new List<ObjectId>();
                    foreach (ObjectId id in recs)
                    {
                        if (!id.IsValid || id.IsErased) continue;
                        try
                        {
                            if (tr.GetObject(id, OpenMode.ForRead) is PropertySetDefinition def && !def.IsErased)
                            {
                                string defName = def.Name ?? string.Empty;
                                if (defName.IndexOf("Corridor", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    idsToErase.Add(id);
                                }
                            }
                        }
                        catch { }
                    }

                    foreach (ObjectId id in idsToErase)
                    {
                        try
                        {
                            var def = tr.GetObject(id, OpenMode.ForWrite);
                            if (def != null && !def.IsErased)
                            {
                                def.Erase(true);
                                count++;
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }

            return count;
        }
    }
}

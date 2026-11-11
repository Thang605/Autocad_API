// (C) Copyright 2015 by  
// Thêm Material List cho SampleLineGroup - Tạo Đào Đắp
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Autodesk.AutoCAD.Runtime;
using Acad = Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;

using Civil = Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using Autodesk.Civil.Runtime;
using Autodesk.Civil.Settings;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.Civil.ApplicationServices;
using CivSurface = Autodesk.Civil.DatabaseServices.TinSurface;
using Section = Autodesk.Civil.DatabaseServices.Section;
using Autodesk.Civil;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using MyFirstProject.Extensions;
using MyFirstProject.Civil_Tool;

// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3DCsharp.CTS_MaterialList_Commands))]

namespace Civil3DCsharp
{
    public class CTS_MaterialList_Commands
    {
        /// <summary>
        /// Lệnh tạo Material List cho SampleLineGroup với cấu trúc Đào/Đắp
        /// Kết quả: Material List với 2 materials: Đào đất (Cut) và Đắp đất (Fill)
        /// </summary>
        [CommandMethod("CTS_Them_Material_List")]
        public static void CTS_Them_MaterialList()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            try
            {
                ed.WriteMessage("\n=== CTS_Them_MaterialList - Tạo Material List (Đào/Đắp) ===\n");

                // Khai báo các biến cần thiết
                ObjectId sampleLineGroupId = ObjectId.Null;
                string sampleLineGroupName = "";
                List<KeyValuePair<string, ObjectId>> surfaceList = [];

                // 1. Chọn SampleLineGroup và lấy thông tin - dùng OpenCloseTransaction
                using (Transaction tr = db.TransactionManager.StartOpenCloseTransaction())
                {
                    ObjectId sectionViewId = UserInput.GSectionView("Chọn 1 trắc ngang trong nhóm cần tạo Material List: ");
                    if (sectionViewId == ObjectId.Null)
                    {
                        ed.WriteMessage("\nKhông thể chọn SectionView.");
                        return;
                    }

                    SectionView? sectionView = tr.GetObject(sectionViewId, OpenMode.ForWrite) as SectionView;
                    if (sectionView == null)
                    {
                        ed.WriteMessage("\nKhông thể mở SectionView.");
                        return;
                    }

                    ObjectId sampleLineId = sectionView.SampleLineId;
                    SampleLine? sampleLine = tr.GetObject(sampleLineId, OpenMode.ForWrite) as SampleLine;
                    if (sampleLine == null)
                    {
                        ed.WriteMessage("\nKhông thể lấy SampleLine từ SectionView.");
                        return;
                    }

                    sampleLineGroupId = sampleLine.GroupId;
                    SampleLineGroup? sampleLineGroup = tr.GetObject(sampleLineGroupId, OpenMode.ForWrite) as SampleLineGroup;
                    if (sampleLineGroup == null)
                    {
                        ed.WriteMessage("\nKhông thể mở SampleLineGroup.");
                        return;
                    }

                    sampleLineGroupName = sampleLineGroup.Name;
                    ed.WriteMessage($"\n✅ Đã chọn SampleLineGroup: {sampleLineGroupName}");

                    // Lấy danh sách surfaces
                    SectionSourceCollection sectionSources = sampleLineGroup.GetSectionSources();
                    foreach (SectionSource sectionSource in sectionSources)
                    {
                        if (sectionSource.SourceType == SectionSourceType.TinSurface ||
                            sectionSource.SourceType == SectionSourceType.CorridorSurface)
                        {
                            try
                            {
                                var entity = tr.GetObject(sectionSource.SourceId, OpenMode.ForWrite);
                                string surfaceName = "";

                                if (entity is TinSurface tinSurface)
                                {
                                    surfaceName = tinSurface.Name;
                                }
                                else
                                {
                                    var nameProperty = entity.GetType().GetProperty("Name");
                                    if (nameProperty != null)
                                    {
                                        surfaceName = nameProperty.GetValue(entity)?.ToString() ?? "";
                                    }
                                }

                                if (!string.IsNullOrEmpty(surfaceName))
                                {
                                    surfaceList.Add(new KeyValuePair<string, ObjectId>(surfaceName, sectionSource.SourceId));
                                }
                            }
                            catch { /* Ignore */ }
                        }
                    }

                    tr.Commit();
                }

                if (surfaceList.Count < 2)
                {
                    ed.WriteMessage("\n❌ Cần ít nhất 2 surfaces (EG và Datum) để tạo Material List.");
                    return;
                }

                // Load Shape Styles
                List<KeyValuePair<string, ObjectId>> shapeStyleList = [];
                try
                {
                    CivilDocument civDoc = CivilApplication.ActiveDocument;
                    var shapeStyles = civDoc.Styles.ShapeStyles;
                    ed.WriteMessage($"\n📐 Tìm thấy {shapeStyles.Count} Shape Styles trong document.");
                    
                    using (Transaction trStyles = db.TransactionManager.StartOpenCloseTransaction())
                    {
                        foreach (ObjectId styleId in shapeStyles)
                        {
                            try
                            {
                                // Cast đúng kiểu ShapeStyle
                                var style = trStyles.GetObject(styleId, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Styles.ShapeStyle;
                                if (style != null)
                                {
                                    string styleName = style.Name ?? "";
                                    if (!string.IsNullOrEmpty(styleName))
                                    {
                                        shapeStyleList.Add(new KeyValuePair<string, ObjectId>(styleName, styleId));
                                    }
                                }
                            }
                            catch (System.Exception ex)
                            { 
                                ed.WriteMessage($"\n   ⚠️ Lỗi đọc style: {ex.Message}");
                            }
                        }
                        trStyles.Commit();
                    }
                    ed.WriteMessage($"\n   ✅ Đã load {shapeStyleList.Count} Shape Styles.");
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage($"\n⚠️ Không thể load Shape Styles: {ex.Message}");
                }

                // 2. Hiển thị Form để chọn Surfaces và Shape Styles
                MaterialListFormSimple form = new MaterialListFormSimple(sampleLineGroupName, surfaceList, shapeStyleList);
                var dialogResult = form.ShowDialog();

                if (dialogResult != System.Windows.Forms.DialogResult.OK || !form.FormAccepted)
                {
                    ed.WriteMessage("\nLệnh đã bị hủy.");
                    return;
                }

                // 3. Tạo Material List với DocumentLock
                using (DocumentLock docLock = doc.LockDocument())
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        try
                        {
                            // Mở SampleLineGroup và surfaces ForWrite
                            SampleLineGroup? sampleLineGroup = tr.GetObject(sampleLineGroupId, OpenMode.ForWrite) as SampleLineGroup;
                            if (sampleLineGroup == null)
                            {
                                ed.WriteMessage("\nKhông thể mở lại SampleLineGroup.");
                                tr.Abort();
                                return;
                            }

                            // Mở surfaces ForWrite
                            var egSurface = tr.GetObject(form.EgSurfaceId, OpenMode.ForWrite);
                            var datumSurface = tr.GetObject(form.DatumSurfaceId, OpenMode.ForWrite);
                            
                            if (egSurface == null || datumSurface == null)
                            {
                                ed.WriteMessage("\n❌ Không thể mở surfaces.");
                                tr.Abort();
                                return;
                            }

                            ed.WriteMessage($"\n🔄 Đang tạo Material List: {form.MaterialListName}...");
                            
                            // Tạo Material List
                            QTOMaterialListCollection materialLists = sampleLineGroup.MaterialLists;
                            QTOMaterialList newMaterialList = materialLists.Add(form.MaterialListName);
                            
                            if (newMaterialList != null)
                            {
                                ed.WriteMessage($"\n   ✅ Đã tạo Material List: {newMaterialList.Name}");
                                
                                // Tạo material Đào đất (Cut)
                                ed.WriteMessage($"\n\n🔄 Đang tạo material '{form.CutMaterialName}'...");
                                QTOMaterial cutMaterial = newMaterialList.Add(form.CutMaterialName);
                                if (cutMaterial != null)
                                {
                                    cutMaterial.QuantityType = MaterialQuantityType.Cut;
                                    
                                    // Set Shape Style cho Cut (từ form)
                                    if (form.CutShapeStyleId != ObjectId.Null)
                                    {
                                        cutMaterial.ShapeStyleId = form.CutShapeStyleId;
                                        ed.WriteMessage($"\n   - Set Cut Shape Style ✅");
                                    }
                                    
                                    QTOMaterialItem egItemCut = cutMaterial.Add(form.EgSurfaceId);
                                    egItemCut.Condition = MaterialConditionType.Below;
                                    ed.WriteMessage($"\n   - EG Surface: Condition = Below ✅");
                                    
                                    QTOMaterialItem datumItemCut = cutMaterial.Add(form.DatumSurfaceId);
                                    datumItemCut.Condition = MaterialConditionType.Above;
                                    ed.WriteMessage($"\n   - Datum Surface: Condition = Above ✅");
                                    
                                    ed.WriteMessage($"\n   ✅ Đã tạo material '{form.CutMaterialName}' (Cut)");
                                }
                                
                                // Tạo material Đắp đất (Fill)
                                ed.WriteMessage($"\n\n🔄 Đang tạo material '{form.FillMaterialName}'...");
                                QTOMaterial fillMaterial = newMaterialList.Add(form.FillMaterialName);
                                if (fillMaterial != null)
                                {
                                    fillMaterial.QuantityType = MaterialQuantityType.Fill;
                                    
                                    // Set Shape Style cho Fill (từ form)
                                    if (form.FillShapeStyleId != ObjectId.Null)
                                    {
                                        fillMaterial.ShapeStyleId = form.FillShapeStyleId;
                                        ed.WriteMessage($"\n   - Set Fill Shape Style ✅");
                                    }
                                    
                                    QTOMaterialItem egItemFill = fillMaterial.Add(form.EgSurfaceId);
                                    egItemFill.Condition = MaterialConditionType.Above;
                                    ed.WriteMessage($"\n   - EG Surface: Condition = Above ✅");
                                    
                                    QTOMaterialItem datumItemFill = fillMaterial.Add(form.DatumSurfaceId);
                                    datumItemFill.Condition = MaterialConditionType.Below;
                                    ed.WriteMessage($"\n   - Datum Surface: Condition = Below ✅");
                                    
                                    ed.WriteMessage($"\n   ✅ Đã tạo material '{form.FillMaterialName}' (Fill)");
                                }
                                
                                ed.WriteMessage($"\n\n📋 Kết quả Material List:");
                                ed.WriteMessage($"\n   📁 {newMaterialList.Name}");
                                ed.WriteMessage($"\n      ├── 🔴 {form.CutMaterialName} (Cut)");
                                ed.WriteMessage($"\n      │      ├── EG (Below)");
                                ed.WriteMessage($"\n      │      └── Datum (Above)");
                                ed.WriteMessage($"\n      └── 🟢 {form.FillMaterialName} (Fill)");
                                ed.WriteMessage($"\n             ├── EG (Above)");
                                ed.WriteMessage($"\n             └── Datum (Below)");
                            }
                            else
                            {
                                ed.WriteMessage($"\n⚠️ Không thể tạo Material List.");
                            }

                            tr.Commit();
                            ed.WriteMessage("\n\n✅ Lệnh CTS_Them_MaterialList hoàn thành thành công!");
                        }
                        catch (System.Exception ex)
                        {
                            ed.WriteMessage($"\n❌ Lỗi khi tạo Material List: {ex.Message}");
                            ed.WriteMessage($"\n   Stack: {ex.StackTrace}");
                            tr.Abort();
                        }
                    }
                }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                ed.WriteMessage($"\n❌ Lỗi AutoCAD: {e.Message}");
            }
            catch (System.Exception e)
            {
                ed.WriteMessage($"\n❌ Lỗi: {e.Message}");
            }
        }

        /// <summary>
        /// Lệnh hiển thị thông tin Material List hiện có của SampleLineGroup
        /// </summary>
        [CommandMethod("CTS_Xem_Material_List")]
        public static void CTS_Xem_MaterialList()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            try
            {
                ed.WriteMessage("\n=== CTS_Xem_MaterialList - Xem Material List của SampleLineGroup ===\n");

                using (DocumentLock docLock = doc.LockDocument())
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        ObjectId sectionViewId = UserInput.GSectionView("Chọn 1 trắc ngang để xem Material List: ");
                        if (sectionViewId == ObjectId.Null)
                        {
                            ed.WriteMessage("\nKhông thể chọn SectionView.");
                            return;
                        }

                        SectionView? sectionView = tr.GetObject(sectionViewId, OpenMode.ForWrite) as SectionView;
                        if (sectionView == null)
                        {
                            ed.WriteMessage("\nKhông thể mở SectionView.");
                            return;
                        }

                        ObjectId sampleLineId = sectionView.SampleLineId;
                        SampleLine? sampleLine = tr.GetObject(sampleLineId, OpenMode.ForWrite) as SampleLine;
                        if (sampleLine == null)
                        {
                            ed.WriteMessage("\nKhông thể lấy SampleLine từ SectionView.");
                            return;
                        }

                        ObjectId sampleLineGroupId = sampleLine.GroupId;
                        SampleLineGroup? sampleLineGroup = tr.GetObject(sampleLineGroupId, OpenMode.ForWrite) as SampleLineGroup;
                        if (sampleLineGroup == null)
                        {
                            ed.WriteMessage("\nKhông thể mở SampleLineGroup.");
                            return;
                        }

                        ed.WriteMessage($"\n📋 SampleLineGroup: {sampleLineGroup.Name}");

                        try
                        {
                            QTOMaterialListCollection materialLists = sampleLineGroup.MaterialLists;
                            
                            if (materialLists.Count == 0)
                            {
                                ed.WriteMessage("\n   ⚠️ Không có Material List nào.");
                            }
                            else
                            {
                                ed.WriteMessage($"\n   ✅ Số lượng Material List: {materialLists.Count}");
                                
                                int idx = 0;
                                foreach (QTOMaterialList materialList in materialLists)
                                {
                                    try
                                    {
                                        if (materialList != null)
                                        {
                                            ed.WriteMessage($"\n\n   📁 [{idx}] {materialList.Name}");
                                            
                                            try
                                            {
                                                for (int i = 0; i < materialList.Count; i++)
                                                {
                                                    QTOMaterial material = materialList[i];
                                                    string typeIcon = material.QuantityType == MaterialQuantityType.Cut ? "🔴" : 
                                                                      material.QuantityType == MaterialQuantityType.Fill ? "🟢" : "⚪";
                                                    ed.WriteMessage($"\n      {typeIcon} {material.Name} ({material.QuantityType})");
                                                    
                                                    for (int j = 0; j < material.Count; j++)
                                                    {
                                                        QTOMaterialItem item = material[j];
                                                        ed.WriteMessage($"\n         └── Item {j}: {item.Condition}");
                                                    }
                                                }
                                            }
                                            catch { /* Ignore */ }
                                            
                                            idx++;
                                        }
                                    }
                                    catch (System.Exception ex)
                                    {
                                        ed.WriteMessage($"\n      {idx}: (Lỗi đọc Material List: {ex.Message})");
                                        idx++;
                                    }
                                }
                            }
                        }
                        catch (System.Exception ex)
                        {
                            ed.WriteMessage($"\n   ❌ Lỗi khi lấy Material List: {ex.Message}");
                        }

                        tr.Commit();
                    }
                }
            }
            catch (System.Exception e)
            {
                ed.WriteMessage($"\n❌ Lỗi: {e.Message}");
            }
        }

        /// <summary>
        /// Lệnh xóa tất cả Material List của SampleLineGroup
        /// </summary>
        [CommandMethod("CTS_Xoa_Material_List")]
        public static void CTS_Xoa_MaterialList()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            try
            {
                ed.WriteMessage("\n=== CTS_Xoa_MaterialList - Xóa Material List của SampleLineGroup ===\n");

                using (DocumentLock docLock = doc.LockDocument())
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        ObjectId sectionViewId = UserInput.GSectionView("Chọn 1 trắc ngang trong nhóm cần xóa Material List: ");
                        if (sectionViewId == ObjectId.Null)
                        {
                            ed.WriteMessage("\nKhông thể chọn SectionView.");
                            return;
                        }

                        SectionView? sectionView = tr.GetObject(sectionViewId, OpenMode.ForWrite) as SectionView;
                        if (sectionView == null)
                        {
                            ed.WriteMessage("\nKhông thể mở SectionView.");
                            return;
                        }

                        ObjectId sampleLineId = sectionView.SampleLineId;
                        SampleLine? sampleLine = tr.GetObject(sampleLineId, OpenMode.ForWrite) as SampleLine;
                        if (sampleLine == null)
                        {
                            ed.WriteMessage("\nKhông thể lấy SampleLine từ SectionView.");
                            return;
                        }

                        ObjectId sampleLineGroupId = sampleLine.GroupId;
                        SampleLineGroup? sampleLineGroup = tr.GetObject(sampleLineGroupId, OpenMode.ForWrite) as SampleLineGroup;
                        if (sampleLineGroup == null)
                        {
                            ed.WriteMessage("\nKhông thể mở SampleLineGroup.");
                            return;
                        }

                        ed.WriteMessage($"\n📋 SampleLineGroup: {sampleLineGroup.Name}");

                        try
                        {
                            QTOMaterialListCollection materialLists = sampleLineGroup.MaterialLists;
                            
                            if (materialLists.Count == 0)
                            {
                                ed.WriteMessage("\n   ⚠️ Không có Material List nào để xóa.");
                            }
                            else
                            {
                                int count = materialLists.Count;
                                
                                List<string> namesToRemove = [];
                                foreach (QTOMaterialList materialList in materialLists)
                                {
                                    namesToRemove.Add(materialList.Name);
                                }
                                
                                foreach (string name in namesToRemove)
                                {
                                    try
                                    {
                                        materialLists.Remove(name);
                                        ed.WriteMessage($"\n      ✅ Đã xóa: {name}");
                                    }
                                    catch (System.Exception ex)
                                    {
                                        ed.WriteMessage($"\n      ⚠️ Lỗi xóa '{name}': {ex.Message}");
                                    }
                                }
                                
                                ed.WriteMessage($"\n   ✅ Đã xóa {count} Material List.");
                            }
                        }
                        catch (System.Exception ex)
                        {
                            ed.WriteMessage($"\n   ❌ Lỗi khi xóa Material List: {ex.Message}");
                        }

                        tr.Commit();
                        ed.WriteMessage("\n\n✅ Lệnh CTS_Xoa_MaterialList hoàn thành!");
                    }
                }
            }
            catch (System.Exception e)
            {
                ed.WriteMessage($"\n❌ Lỗi: {e.Message}");
            }
        }

        /// <summary>
        /// Helper method to get surface name from ObjectId
        /// </summary>
        private static string GetSurfaceName(ObjectId surfaceId, Transaction tr)
        {
            try
            {
                if (surfaceId == ObjectId.Null || !surfaceId.IsValid)
                    return "Unknown";

                var entity = tr.GetObject(surfaceId, OpenMode.ForWrite);
                
                if (entity is TinSurface tinSurface)
                    return tinSurface.Name ?? "Unnamed Surface";
                
                var nameProperty = entity.GetType().GetProperty("Name");
                if (nameProperty != null)
                {
                    return nameProperty.GetValue(entity)?.ToString() ?? "Unknown";
                }
                
                return entity.GetType().Name;
            }
            catch
            {
                return "Error";
            }
        }
    }
}

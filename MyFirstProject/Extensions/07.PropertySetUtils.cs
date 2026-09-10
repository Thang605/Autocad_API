using Autodesk.Aec.PropertyData.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using MyFirstProject.Extensions;

namespace MyFirstProject.Extensions
{
    /// <summary>
    /// Model chứa thông tin ánh xạ Layer với thuộc tính Property Set
    /// </summary>
    public class LayerPropertyMapping
    {
        public bool IsSelected { get; set; } = true;
        public string LayerName { get; set; } = "";
        public int SolidCount { get; set; }
        public int BodyCount { get; set; }
        public int TotalCount => SolidCount + BodyCount;
        public string CauKien { get; set; } = "";
        public string VatLieu { get; set; } = "";
    }

    public class PropertySetUtils
    {
        /// <summary>
        /// Tên Property Set mặc định cho Solid và Body
        /// </summary>
        public const string DefaultPropertySetName = "IFC ĐƯỜNG GIAO THÔNG2";

        /// <summary>
        /// Thiết lập Property Set cho 3D Solid với các thuộc tính được tính toán
        /// </summary>
        /// <param name="tr">Transaction</param>
        /// <param name="solid">3D Solid object</param>
        public static void SetupSolidWithCalculatedProperties(Transaction tr, Solid3d solid)
        {
            if (solid == null) return;

            try
            {
                // Lấy thông tin cơ bản của solid
                string layerName = solid.Layer;
                double volume = solid.MassProperties.Volume;
                Point3d centroid = solid.MassProperties.Centroid;

                // Sử dụng tên Property Set cố định
                string propertySetName = DefaultPropertySetName;

                // Kiểm tra và tạo Property Set Definition nếu chưa có
                ObjectId propertySetDefId = GetOrCreatePropertySetDefinition(tr, propertySetName);

                if (propertySetDefId.IsNull) return;

                // Attach Property Set vào solid
                AttachPropertySetToObject(tr, solid, propertySetDefId);

                // Set các giá trị properties
                SetPropertyValues(tr, solid, propertySetDefId, new Dictionary<string, object>
                {
                    { "Cấu kiện", "" },
                    { "Vật liệu", "" },
                    { "Thể tích (m3)", Math.Round(volume, 3) }
                });
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi khi thiết lập Property Set: {ex.Message}");
            }
        }

        /// <summary>
        /// Lấy hoặc tạo Property Set Definition (hỗ trợ cả AcDb3dSolid và AcDbBody)
        /// </summary>
        public static ObjectId GetOrCreatePropertySetDefinition(Transaction tr, string propertySetName)
        {
            try
            {
                Database db = A.Db;
                DictionaryPropertySetDefinitions propSetDefs = new(db);

                // Kiểm tra xem Property Set Definition đã tồn tại chưa
                if (propSetDefs.Has(propertySetName, tr))
                {
                    ObjectId existingId = propSetDefs.GetAt(propertySetName);
                    try
                    {
                        var existingDef = tr.GetObject(existingId, OpenMode.ForWrite) as PropertySetDefinition;
                        if (existingDef != null)
                        {
                            var filter = existingDef.AppliesToFilter;
                            bool modified = false;
                            if (!filter.Contains("AcDb3dSolid")) { filter.Add("AcDb3dSolid"); modified = true; }
                            if (!filter.Contains("AcDbBody")) { filter.Add("AcDbBody"); modified = true; }
                            if (modified)
                            {
                                existingDef.SetAppliesToFilter(filter, false);
                            }

                            // Xóa bớt các thuộc tính không dùng (chỉ giữ Cấu kiện, Vật liệu, Thể tích (m3))
                            var allowedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                            {
                                "Cấu kiện", "Vật liệu", "Thể tích (m3)"
                            };
                            for (int i = existingDef.Definitions.Count - 1; i >= 0; i--)
                            {
                                if (!allowedNames.Contains(existingDef.Definitions[i].Name))
                                {
                                    try
                                    {
                                        existingDef.Definitions.RemoveAt(i);
                                    }
                                    catch { }
                                }
                            }

                            // Đảm bảo có đủ 3 thuộc tính
                            var currentNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            for (int i = 0; i < existingDef.Definitions.Count; i++)
                            {
                                currentNames.Add(existingDef.Definitions[i].Name);
                            }

                            if (!currentNames.Contains("Cấu kiện"))
                            {
                                existingDef.Definitions.Add(new PropertyDefinition
                                {
                                    Name = "Cấu kiện",
                                    Description = "Loại cấu kiện",
                                    DataType = Autodesk.Aec.PropertyData.DataType.Text,
                                    DefaultData = ""
                                });
                            }
                            if (!currentNames.Contains("Vật liệu"))
                            {
                                existingDef.Definitions.Add(new PropertyDefinition
                                {
                                    Name = "Vật liệu",
                                    Description = "Loại vật liệu",
                                    DataType = Autodesk.Aec.PropertyData.DataType.Text,
                                    DefaultData = ""
                                });
                            }
                            if (!currentNames.Contains("Thể tích (m3)"))
                            {
                                existingDef.Definitions.Add(new PropertyDefinition
                                {
                                    Name = "Thể tích (m3)",
                                    Description = "Thể tích của đối tượng (m³)",
                                    DataType = Autodesk.Aec.PropertyData.DataType.Real,
                                    DefaultData = 0.0
                                });
                            }
                        }
                    }
                    catch { }
                    return existingId;
                }

                // Tạo mới Property Set Definition
                PropertySetDefinition propSetDef = new();
                propSetDef.SubSetDatabaseDefaults(db);
                propSetDef.Description = "Property Set cho đường giao thông IFC";

                // Thêm các Property Definitions
                AddPropertyDefinitions(propSetDef);

                // Thiết lập Applies To (cả 3D Solid và Body)
                StringCollection appliesToFilter = ["AcDb3dSolid", "AcDbBody"];
                propSetDef.SetAppliesToFilter(appliesToFilter, false);

                propSetDefs.AddNewRecord(propertySetName, propSetDef);
                tr.AddNewlyCreatedDBObject(propSetDef, true);

                return propSetDef.ObjectId;
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi khi tạo Property Set Definition: {ex.Message}");
                return ObjectId.Null;
            }
        }

        /// <summary>
        /// Thêm các Property Definitions vào Property Set Definition (chỉ giữ Cấu kiện, Vật liệu, Thể tích (m3))
        /// </summary>
        private static void AddPropertyDefinitions(PropertySetDefinition propSetDef)
        {
            // Property: Cấu kiện
            PropertyDefinition cauKienProp = new()
            {
                Name = "Cấu kiện",
                Description = "Loại cấu kiện",
                DataType = Autodesk.Aec.PropertyData.DataType.Text,
                DefaultData = ""
            };
            propSetDef.Definitions.Add(cauKienProp);

            // Property: Vật liệu
            PropertyDefinition vatLieuProp = new()
            {
                Name = "Vật liệu",
                Description = "Loại vật liệu",
                DataType = Autodesk.Aec.PropertyData.DataType.Text,
                DefaultData = ""
            };
            propSetDef.Definitions.Add(vatLieuProp);

            // Property: Thể tích (m3)
            PropertyDefinition theTichProp = new()
            {
                Name = "Thể tích (m3)",
                Description = "Thể tích của đối tượng (m³)",
                DataType = Autodesk.Aec.PropertyData.DataType.Real,
                DefaultData = 0.0
            };
            propSetDef.Definitions.Add(theTichProp);
        }

        /// <summary>
        /// Attach Property Set vào object (bỏ qua nếu đã gắn)
        /// </summary>
        private static void AttachPropertySetToObject(Transaction tr, DBObject dbObject, ObjectId propertySetDefId)
        {
            try
            {
                ObjectIdCollection existingSets = PropertyDataServices.GetPropertySets(dbObject);
                foreach (ObjectId propSetId in existingSets)
                {
                    if (tr.GetObject(propSetId, OpenMode.ForRead) is PropertySet propSet &&
                        propSet.PropertySetDefinition == propertySetDefId)
                    {
                        return; // Đã gắn Property Set này rồi
                    }
                }

                PropertyDataServices.AddPropertySet(dbObject, propertySetDefId);
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi khi attach Property Set: {ex.Message}");
            }
        }

        /// <summary>
        /// Set giá trị cho các properties
        /// </summary>
        private static void SetPropertyValues(Transaction tr, DBObject dbObject, ObjectId propertySetDefId, Dictionary<string, object> values)
        {
            try
            {
                ObjectIdCollection propertySetIds = PropertyDataServices.GetPropertySets(dbObject);
                
                foreach (ObjectId propSetId in propertySetIds)
                {
                    PropertySet? propSet = tr.GetObject(propSetId, OpenMode.ForWrite) as PropertySet;
                    if (propSet?.PropertySetDefinition == propertySetDefId)
                    {
                        PropertySetDefinition? propSetDef = tr.GetObject(propertySetDefId, OpenMode.ForRead) as PropertySetDefinition;
                        
                        foreach (var kvp in values)
                        {
                            try
                            {
                                // Tìm index của property theo tên
                                int propertyIndex = -1;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                                for (int i = 0; i < propSetDef.Definitions.Count; i++)
                                {
                                    if (propSetDef.Definitions[i].Name == kvp.Key)
                                    {
                                        propertyIndex = i;
                                        break;
                                    }
                                }
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                                
                                if (propertyIndex >= 0)
                                {
                                    propSet.SetAt(propertyIndex, kvp.Value);
                                }
                            }
                            catch (System.Exception ex)
                            {
                                A.Ed.WriteMessage($"\nLỗi khi set property {kvp.Key}: {ex.Message}");
                            }
                        }

                        // Set giá trị cho "Thể tích (m3)" từ Volume
                        if (values.ContainsKey("Volume"))
                        {
                            try
                            {
                                int theTichIndex = -1;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                                for (int i = 0; i < propSetDef.Definitions.Count; i++)
                                {
                                    if (propSetDef.Definitions[i].Name == "Thể tích (m3)")
                                    {
                                        theTichIndex = i;
                                        break;
                                    }
                                }
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                                
                                if (theTichIndex >= 0)
                                {
                                    propSet.SetAt(theTichIndex, values["Volume"]);
                                }
                            }
                            catch (System.Exception ex)
                            {
                                A.Ed.WriteMessage($"\nLỗi khi set property Thể tích (m3): {ex.Message}");
                            }
                        }
                        break;
                    }
                }
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi khi set property values: {ex.Message}");
            }
        }

        /// <summary>
        /// Hiển thị thông tin Property Set của 3D Solid
        /// </summary>
        public static void ShowSolidPropertySetInfo(Transaction tr, Solid3d solid)
        {
            try
            {
                A.Ed.WriteMessage($"\n=== THÔNG TIN 3D SOLID ===");
                A.Ed.WriteMessage($"\nLayer: {solid.Layer}");
                A.Ed.WriteMessage($"\nHandle: {solid.Handle}");
                
                var massProps = solid.MassProperties;
                A.Ed.WriteMessage($"\nThể tích: {massProps.Volume:F3} m³");
                A.Ed.WriteMessage($"\nTrọng tâm: X={massProps.Centroid.X:F3}, Y={massProps.Centroid.Y:F3}, Z={massProps.Centroid.Z:F3}");

                // Hiển thị Property Sets
                ObjectIdCollection propertySetIds = PropertyDataServices.GetPropertySets(solid);
                if (propertySetIds.Count > 0)
                {
                    A.Ed.WriteMessage($"\n\n=== PROPERTY SETS ({propertySetIds.Count}) ===");
                    
                    foreach (ObjectId propSetId in propertySetIds)
                    {
                        PropertySet? propSet = tr.GetObject(propSetId, OpenMode.ForRead) as PropertySet;
                        if (propSet != null)
                        {
                            PropertySetDefinition? propSetDef = tr.GetObject(propSet.PropertySetDefinition, OpenMode.ForRead) as PropertySetDefinition;
                            A.Ed.WriteMessage($"\n\nProperty Set: {propSetDef?.Name}");
                            A.Ed.WriteMessage($"Mô tả: {propSetDef?.Description}");
                            
                            if (propSetDef != null)
                            {
                                for (int i = 0; i < propSetDef.Definitions.Count; i++)
                                {
                                    PropertyDefinition propDef = propSetDef.Definitions[i];
                                    try
                                    {
                                        object value = propSet.GetAt(i);
                                        A.Ed.WriteMessage($"\n  - {propDef.Name}: {value} ({propDef.Description})");
                                    }
                                    catch
                                    {
                                        A.Ed.WriteMessage($"\n  - {propDef.Name}: <không có giá trị> ({propDef.Description})");
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    A.Ed.WriteMessage($"\n\nKhông có Property Set nào được gắn vào đối tượng này.");
                }
                
                A.Ed.WriteMessage($"\n================================\n");
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi khi hiển thị thông tin: {ex.Message}");
            }
        }

        /// <summary>
        /// Quét tất cả các đối tượng Solid3d và Body trong ModelSpace theo Layer
        /// </summary>
        public static List<LayerPropertyMapping> ScanSolidsAndBodies(Transaction tr)
        {
            var layerMap = new Dictionary<string, LayerPropertyMapping>(StringComparer.OrdinalIgnoreCase);
            Database db = A.Db;
            BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            var solidRx = RXClass.GetClass(typeof(Solid3d));
            var bodyRx = RXClass.GetClass(typeof(Body));

            foreach (ObjectId id in btr)
            {
                if (id.IsErased || !id.IsValid) continue;

                if (id.ObjectClass.IsDerivedFrom(solidRx))
                {
                    var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent == null) continue;
                    string layer = ent.Layer ?? "0";
                    if (!layerMap.TryGetValue(layer, out var item))
                    {
                        item = new LayerPropertyMapping { LayerName = layer, IsSelected = true };
                        layerMap[layer] = item;
                    }
                    item.SolidCount++;
                }
                else if (id.ObjectClass.IsDerivedFrom(bodyRx))
                {
                    var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent == null) continue;
                    string layer = ent.Layer ?? "0";
                    if (!layerMap.TryGetValue(layer, out var item))
                    {
                        item = new LayerPropertyMapping { LayerName = layer, IsSelected = true };
                        layerMap[layer] = item;
                    }
                    item.BodyCount++;
                }
            }

            return layerMap.Values.OrderBy(x => x.LayerName).ToList();
        }

        private static PropertySet? FindPropertySet(Transaction tr, DBObject entity, ObjectId definitionId)
        {
            foreach (ObjectId id in PropertyDataServices.GetPropertySets(entity))
            {
                if (tr.GetObject(id, OpenMode.ForRead) is PropertySet propertySet &&
                    propertySet.PropertySetDefinition == definitionId)
                    return propertySet;
            }
            return null;
        }

        private static void SetPropertyIfChanged(PropertySet propertySet, int propertyId, object value)
        {
            if (propertyId < 0) return;
            try
            {
                object currentVal = propertySet.GetAt(propertyId);
                if (currentVal != null)
                {
                    if (value is double dNew && (currentVal is double || double.TryParse(currentVal.ToString(), out _)))
                    {
                        double dOld = Convert.ToDouble(currentVal);
                        if (Math.Abs(dNew - dOld) < 1e-4) return;
                    }
                    else if (string.Equals(currentVal.ToString()?.Trim(), value?.ToString()?.Trim() ?? "", StringComparison.Ordinal))
                    {
                        return;
                    }
                }
                else if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
                {
                    return;
                }

                if (!propertySet.IsWriteEnabled) propertySet.UpgradeOpen();
                propertySet.SetAt(propertyId, value);
            }
            catch { }
        }
        /// <summary>
        /// Gán / Cập nhật Property Set cho các 3D Solid và Body theo danh sách mapping Layer
        /// </summary>
        public static int ApplyPropertySetsByMapping(Transaction tr, string propertySetName, List<LayerPropertyMapping> mappings)
        {
            if (mappings == null || mappings.Count == 0) return 0;

            var mappingDict = mappings
                .Where(m => m.IsSelected)
                .ToDictionary(m => m.LayerName, m => m, StringComparer.OrdinalIgnoreCase);

            if (mappingDict.Count == 0) return 0;

            ObjectId propertySetDefId = GetOrCreatePropertySetDefinition(tr, propertySetName);
            if (propertySetDefId.IsNull) return 0;

            Database db = A.Db;
            BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            var solidRx = RXClass.GetClass(typeof(Solid3d));
            var bodyRx = RXClass.GetClass(typeof(Body));
            // Resolve layer names once, instead of querying the layer name for every entity.
            var selectedLayers = new Dictionary<ObjectId, LayerPropertyMapping>();
            var layers = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            foreach (var mapping in mappingDict.Values)
            {
                if (layers.Has(mapping.LayerName))
                    selectedLayers[layers[mapping.LayerName]] = mapping;
            }

            int count = 0;
            int[]? propertyIds = null;
            foreach (ObjectId id in btr)
            {
                if (!id.IsValid || id.IsErased) continue;
                var objectClass = id.ObjectClass;
                if (!objectClass.IsDerivedFrom(solidRx) && !objectClass.IsDerivedFrom(bodyRx)) continue;

                var entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (entity == null || !selectedLayers.TryGetValue(entity.LayerId, out var map)) continue;

                PropertySet? propertySet = FindPropertySet(tr, entity, propertySetDefId);
                if (propertySet == null)
                {
                    entity.UpgradeOpen();
                    PropertyDataServices.AddPropertySet(entity, propertySetDefId);
                    propertySet = FindPropertySet(tr, entity, propertySetDefId)
                        ?? throw new InvalidOperationException("Không tìm thấy Property Set vừa gắn.");
                }

                // AEC property IDs are not necessarily collection indexes after schema edits.
                // All sets in this batch share the same definition; resolve IDs only once.
                propertyIds ??= new[]
                {
                    propertySet.PropertyNameToId("Cấu kiện"),
                    propertySet.PropertyNameToId("Vật liệu"),
                    propertySet.PropertyNameToId("Thể tích (m3)")
                };

                double volume = 0.0;
                if (entity is Solid3d solid)
                {
                    try { volume = solid.MassProperties.Volume; }
                    catch { } // Preserve the existing fallback for invalid solid geometry.
                }

                SetPropertyIfChanged(propertySet, propertyIds[0], map.CauKien ?? "");
                SetPropertyIfChanged(propertySet, propertyIds[1], map.VatLieu ?? "");
                SetPropertyIfChanged(propertySet, propertyIds[2], Math.Round(volume, 3));
                count++;
            }
            return count;
        }
    }
}

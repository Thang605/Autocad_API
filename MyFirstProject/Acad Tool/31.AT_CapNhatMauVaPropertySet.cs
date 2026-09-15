// (C) Copyright 2026 by T27
//
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using Autodesk.Aec.PropertyData.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.Geometry;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Color = Autodesk.AutoCAD.Colors.Color;
using Exception = System.Exception;

[assembly: CommandClass(typeof(Civil3DCsharp.CapNhatMauVaPropertySetCmd))]

namespace Civil3DCsharp
{
    /// <summary>
    /// Lệnh Cập Nhật Màu Layer và Thuộc tính Property Set cho 3D Solid theo Chuẩn BIM / EIR (BEP T27)
    /// Lệnh: AT_CapNhatMauVaPropertySet / T_CAPNHATMAUVAPROPERTYSET / T_CAPNHATMAU / CAPNHATMAUVAPROPERTYSET / CNMPS / AT_MauVaPropertySet / AT_LayerBimStandard
    /// </summary>
    public class CapNhatMauVaPropertySetCmd
    {
        [CommandMethod("AT_CapNhatMauVaPropertySet")]
        [CommandMethod("T_CAPNHATMAUVAPROPERTYSET")]
        [CommandMethod("T_CAPNHATMAU")]
        [CommandMethod("CAPNHATMAUVAPROPERTYSET")]
        [CommandMethod("CNMPS")]
        [CommandMethod("AT_MauVaPropertySet")]
        [CommandMethod("AT_LayerBimStandard")]
        public void CapNhatMauVaPropertySetCommand()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            using (var form = new CapNhatMauVaPropertySetForm())
            {
                Application.ShowModalDialog(form);
            }
        }

        #region Standard Property Set Definitions & Schema
        public class PropertyFieldDef
        {
            public string Name { get; set; } = "";
            public Autodesk.Aec.PropertyData.DataType DataType { get; set; } = Autodesk.Aec.PropertyData.DataType.Text;
            public string DefaultValue { get; set; } = "";
            public string Description { get; set; } = "";
        }

        /// <summary>
        /// Danh mục 4 Property Set chuẩn BIM theo hồ sơ thiết kế hạ tầng kỹ thuật / đường giao thông
        /// </summary>
        public static readonly Dictionary<string, List<PropertyFieldDef>> StandardPropertySets = new(StringComparer.OrdinalIgnoreCase)
        {
            {
                "1. Thông tin dự án",
                new List<PropertyFieldDef>
                {
                    new() { Name = "Tên công trình", DataType = Autodesk.Aec.PropertyData.DataType.Text, DefaultValue = "", Description = "Tên dự án / công trình xây dựng" },
                    new() { Name = "Vị trí", DataType = Autodesk.Aec.PropertyData.DataType.Text, DefaultValue = "", Description = "Vị trí địa lý / địa bàn công trình" },
                    new() { Name = "Nhóm cấu kiện", DataType = Autodesk.Aec.PropertyData.DataType.Text, DefaultValue = "Hạ tầng kỹ thuật", Description = "Nhóm cấu kiện phân loại BIM" }
                }
            },
            {
                "2. Đặc trưng",
                new List<PropertyFieldDef>
                {
                    new() { Name = "Tên cấu kiện", DataType = Autodesk.Aec.PropertyData.DataType.Text, DefaultValue = "", Description = "Tên cấu kiện / bộ phận công trình" },
                    new() { Name = "Hạng mục", DataType = Autodesk.Aec.PropertyData.DataType.Text, DefaultValue = "Đường giao thông", Description = "Hạng mục công trình" },
                    new() { Name = "Loại vật liệu", DataType = Autodesk.Aec.PropertyData.DataType.Text, DefaultValue = "", Description = "Chủng loại vật liệu kết cấu" },
                    new() { Name = "Độ chặt", DataType = Autodesk.Aec.PropertyData.DataType.Text, DefaultValue = "", Description = "Hệ số đầm chặt (K95, K98...)" }
                }
            },
            {
                "3. Hình học",
                new List<PropertyFieldDef>
                {
                    new() { Name = "Bề dày", DataType = Autodesk.Aec.PropertyData.DataType.Real, DefaultValue = "0.0", Description = "Bề dày kết cấu (m)" },
                    new() { Name = "Diện tích", DataType = Autodesk.Aec.PropertyData.DataType.Real, DefaultValue = "0.0", Description = "Diện tích bề mặt trải kết cấu (m²)" }
                }
            },
            {
                "4. Khối lượng",
                new List<PropertyFieldDef>
                {
                    new() { Name = "Khối lượng", DataType = Autodesk.Aec.PropertyData.DataType.Real, DefaultValue = "0.0", Description = "Thể tích khối lượng 3D Solid (m³)" }
                }
            }
        };

        /// <summary>
        /// Lấy hoặc tạo Property Set Definition an toàn (KHÔNG xóa bất kỳ thuộc tính nào có sẵn của người dùng)
        /// </summary>
        public static ObjectId GetOrCreatePropertySetDefinition(Transaction tr, Database db, string psetName, List<PropertyFieldDef> desiredDefs)
        {
            try
            {
                DictionaryPropertySetDefinitions propSetDefs = new(db);
                if (propSetDefs.Has(psetName, tr))
                {
                    ObjectId existingId = propSetDefs.GetAt(psetName);
                    try
                    {
                        if (tr.GetObject(existingId, OpenMode.ForWrite) is PropertySetDefinition existingDef)
                        {
                            var filter = existingDef.AppliesToFilter;
                            bool filterModified = false;
                            if (!filter.Contains("AcDb3dSolid")) { filter.Add("AcDb3dSolid"); filterModified = true; }
                            if (!filter.Contains("AcDbBody")) { filter.Add("AcDbBody"); filterModified = true; }
                            if (filterModified) existingDef.SetAppliesToFilter(filter, false);

                            var currentNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            for (int i = 0; i < existingDef.Definitions.Count; i++)
                            {
                                currentNames.Add(existingDef.Definitions[i].Name);
                            }

                            // Chỉ bổ sung thuộc tính còn thiếu, KHÔNG xóa thuộc tính cũ
                            foreach (var item in desiredDefs)
                            {
                                if (!currentNames.Contains(item.Name))
                                {
                                    object defData = ConvertDefaultData(item.DataType, item.DefaultValue);
                                    existingDef.Definitions.Add(new PropertyDefinition
                                    {
                                        Name = item.Name,
                                        Description = item.Description ?? "",
                                        DataType = item.DataType,
                                        DefaultData = defData
                                    });
                                    currentNames.Add(item.Name);
                                }
                            }
                        }
                    }
                    catch { }
                    return existingId;
                }

                // Tạo mới Property Set Definition
                PropertySetDefinition propSetDef = new();
                propSetDef.SubSetDatabaseDefaults(db);
                propSetDef.Description = $"Property Set BIM: {psetName}";

                foreach (var item in desiredDefs)
                {
                    object defData = ConvertDefaultData(item.DataType, item.DefaultValue);
                    propSetDef.Definitions.Add(new PropertyDefinition
                    {
                        Name = item.Name,
                        Description = item.Description ?? "",
                        DataType = item.DataType,
                        DefaultData = defData
                    });
                }

                StringCollection appliesTo = ["AcDb3dSolid", "AcDbBody"];
                propSetDef.SetAppliesToFilter(appliesTo, false);

                propSetDefs.AddNewRecord(psetName, propSetDef);
                tr.AddNewlyCreatedDBObject(propSetDef, true);
                return propSetDef.ObjectId;
            }
            catch
            {
                return ObjectId.Null;
            }
        }

        private static object ConvertDefaultData(Autodesk.Aec.PropertyData.DataType dataType, string textVal)
        {
            switch (dataType)
            {
                case Autodesk.Aec.PropertyData.DataType.Real:
                    if (double.TryParse(textVal, NumberStyles.Any, CultureInfo.InvariantCulture, out double dVal)) return dVal;
                    return 0.0;
                case Autodesk.Aec.PropertyData.DataType.Integer:
                    if (int.TryParse(textVal, out int iVal)) return iVal;
                    return 0;
                case Autodesk.Aec.PropertyData.DataType.TrueFalse:
                    if (bool.TryParse(textVal, out bool bVal)) return bVal;
                    return false;
                default:
                    return textVal ?? "";
            }
        }
        #endregion

        #region Helper Clean / Normalization
        /// <summary>
        /// Chuẩn hóa tên trường thuộc tính để so khớp linh hoạt không phân biệt dấu, khoảng trắng, viết hoa/thường
        /// </summary>
        public static string NormalizePropertyName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            string s = name.Trim().ToLowerInvariant();
            s = s.Replace(" ", "").Replace("_", "").Replace("-", "").Replace(":", "").Replace(".", "").Replace("(", "").Replace(")", "");
            s = s.Replace("đ", "d").Replace("á", "a").Replace("à", "a").Replace("ạ", "a").Replace("ả", "a").Replace("ã", "a")
                 .Replace("â", "a").Replace("ấ", "a").Replace("ầ", "a").Replace("ậ", "a").Replace("ẩ", "a").Replace("ẫ", "a")
                 .Replace("ă", "a").Replace("ắ", "a").Replace("ằ", "a").Replace("ặ", "a").Replace("ẳ", "a").Replace("ẵ", "a")
                 .Replace("é", "e").Replace("è", "e").Replace("ẹ", "e").Replace("ẻ", "e").Replace("ẽ", "e")
                 .Replace("ê", "e").Replace("ế", "e").Replace("ề", "e").Replace("ệ", "e").Replace("ể", "e").Replace("ễ", "e")
                 .Replace("í", "i").Replace("ì", "i").Replace("ị", "i").Replace("ỉ", "i").Replace("ĩ", "i")
                 .Replace("ó", "o").Replace("ò", "o").Replace("ọ", "o").Replace("ỏ", "o").Replace("õ", "o")
                 .Replace("ô", "o").Replace("ố", "o").Replace("ồ", "o").Replace("ộ", "o").Replace("ổ", "o").Replace("ỗ", "o")
                 .Replace("ơ", "o").Replace("ớ", "o").Replace("ờ", "o").Replace("ợ", "o").Replace("ở", "o").Replace("ỡ", "o")
                 .Replace("ú", "u").Replace("ù", "u").Replace("ụ", "u").Replace("ủ", "u").Replace("ũ", "u")
                 .Replace("ư", "u").Replace("ứ", "u").Replace("ừ", "u").Replace("ự", "u").Replace("ử", "u").Replace("ữ", "u")
                 .Replace("ý", "y").Replace("ỳ", "y").Replace("ỵ", "y").Replace("ỷ", "y").Replace("ỹ", "y");
            return s;
        }

        /// <summary>
        /// Kiểm tra xem giá trị thuộc tính hiện tại có đang bị để trống / rỗng / 0 hay không
        /// </summary>
        public static bool IsPropertyEmpty(object? val)
        {
            if (val == null) return true;
            if (val is string s)
            {
                string trimmed = s.Trim();
                return string.IsNullOrEmpty(trimmed) || trimmed == "---" || trimmed == "(0)" || trimmed == "0" || trimmed == "0.0" || trimmed == "0.00" || trimmed == "0.000";
            }
            if (val is double d)
            {
                return Math.Abs(d) < 1e-6;
            }
            if (val is int i)
            {
                return i == 0;
            }
            return false;
        }

        /// <summary>
        /// Ép kiểu giá trị theo kiểu dữ liệu DataType của PropertyDefinition
        /// </summary>
        private static object ConvertValueForProperty(PropertyDefinition propDef, object rawValue)
        {
            if (rawValue == null) return "";
            switch (propDef.DataType)
            {
                case Autodesk.Aec.PropertyData.DataType.Real:
                    if (rawValue is double dVal) return dVal;
                    if (double.TryParse(rawValue.ToString()?.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double pd)) return pd;
                    return 0.0;
                case Autodesk.Aec.PropertyData.DataType.Integer:
                    if (rawValue is int iVal) return iVal;
                    if (int.TryParse(rawValue.ToString(), out int pi)) return pi;
                    return 0;
                case Autodesk.Aec.PropertyData.DataType.TrueFalse:
                    if (rawValue is bool bVal) return bVal;
                    string s = rawValue.ToString()?.Trim().ToLowerInvariant() ?? "";
                    return s == "true" || s == "1" || s == "yes" || s == "có";
                default:
                    if (rawValue is double dNum)
                    {
                        return dNum.ToString("0.###", CultureInfo.InvariantCulture);
                    }
                    return rawValue.ToString() ?? "";
            }
        }
        #endregion

        #region Main Execution: Update Layers & Property Sets
        /// <summary>
        /// Thực thi cập nhật Màu Layer, Mô tả Layer, chuyển ByLayer và Cập nhật Property Set cho 3D Solid
        /// </summary>
        public static (int updatedLayers, int updatedSolids, int byLayerCount) ExecuteUpdateAll(
            List<LayerBimRowModel> rows,
            bool updateColor,
            bool updateDescription,
            bool applyByLayer,
            bool unlockLayers,
            bool updatePropertySet,
            bool onlyMissingProperties,
            string projectTitle,
            string projectLocation,
            string projectComponentGroup,
            string defaultCategory,
            Action<string> log)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) throw new Exception("Không tìm thấy bản vẽ hiện hành!");

            var db = doc.Database;
            var ed = doc.Editor;
            var timer = Stopwatch.StartNew();

            int updatedLayersCount = 0;
            int updatedSolidsCount = 0;
            int byLayerCount = 0;

            var updatedLayerMap = rows.ToDictionary(x => x.LayerName, x => x, StringComparer.OrdinalIgnoreCase);
            var updatedLayerNames = new HashSet<string>(updatedLayerMap.Keys, StringComparer.OrdinalIgnoreCase);

            using (var docLock = doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var layTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForWrite);

                log("🚀 Bắt đầu quá trình cập nhật Màu sắc, Mô tả Layer & Property Set cho 3D Solid...");

                // ================= 1. CẬP NHẬT MÀU VÀ MÔ TẢ TRÊN LAYERTABLE =================
                foreach (var item in rows)
                {
                    string layName = item.LayerName;
                    if (string.IsNullOrWhiteSpace(layName)) continue;

                    if (layTable.Has(layName))
                    {
                        var ltr = (LayerTableRecord)tr.GetObject(layTable[layName], OpenMode.ForWrite);

                        if (ltr.IsLocked && unlockLayers)
                        {
                            ltr.IsLocked = false;
                            log($"  🔓 Mở khóa Layer: [{layName}]");
                        }

                        // Cập nhật TrueColor
                        if (updateColor && item.NewColor.HasValue)
                        {
                            ltr.Color = Color.FromRgb(item.NewR!.Value, item.NewG!.Value, item.NewB!.Value);
                        }

                        // Cập nhật Mô tả Description
                        if (updateDescription)
                        {
                            string desc = "";
                            if (!string.IsNullOrWhiteSpace(item.CauKien) && !string.IsNullOrWhiteSpace(item.VatLieu))
                            {
                                desc = $"{item.CauKien} | {item.VatLieu}";
                            }
                            else if (!string.IsNullOrWhiteSpace(item.VatLieu))
                            {
                                desc = item.VatLieu;
                            }
                            else if (!string.IsNullOrWhiteSpace(item.CauKien))
                            {
                                desc = item.CauKien;
                            }

                            if (!string.IsNullOrEmpty(desc))
                            {
                                ltr.Description = desc;
                            }
                        }

                        updatedLayersCount++;
                        string colorInfo = item.NewColor.HasValue ? $"RGB({item.NewR},{item.NewG},{item.NewB})" : "giữ màu cũ";
                        log($"  ✅ Layer [{layName}] -> {colorInfo} | {item.CauKien} | {item.VatLieu}");
                    }
                    else
                    {
                        log($"  ⚠️ Bỏ qua Layer [{layName}] (không tồn tại trong LayerTable)");
                    }
                }

                // ================= 2. CẬP NHẬT PROPERTY SET CHO 3D SOLID =================
                if (updatePropertySet && updatedLayerNames.Count > 0)
                {
                    log("📦 Đang chuẩn bị các Property Set Definitions chuẩn...");

                    // Đảm bảo tồn tại 4 Property Set Definitions chuẩn
                    var psetDefIds = new Dictionary<string, ObjectId>(StringComparer.OrdinalIgnoreCase);
                    foreach (var kvp in StandardPropertySets)
                    {
                        string psetName = kvp.Key;
                        ObjectId defId = GetOrCreatePropertySetDefinition(tr, db, psetName, kvp.Value);
                        if (!defId.IsNull)
                        {
                            psetDefIds[psetName] = defId;
                        }
                    }

                    log("🔍 Đang quét và cập nhật các đối tượng 3D Solid / Body thuộc các Layer đã chọn...");

                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                    var solidRx = RXClass.GetClass(typeof(Solid3d));
                    var bodyRx = RXClass.GetClass(typeof(Body));
                    var warnedLayersWithoutThickness = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (ObjectId entId in btr)
                    {
                        if (!entId.IsValid || entId.IsErased) continue;
                        var rxClass = entId.ObjectClass;
                        bool isSolid = rxClass.IsDerivedFrom(solidRx);
                        bool isBody = rxClass.IsDerivedFrom(bodyRx);
                        if (!isSolid && !isBody) continue;

                        var ent = tr.GetObject(entId, OpenMode.ForRead) as Entity;
                        if (ent == null || !updatedLayerMap.TryGetValue(ent.Layer, out var rowModel)) continue;

                        // Thu thập thông tin hình học của 3D Solid (Thể tích V)
                        double volume = 0.0;
                        if (ent is Solid3d solid)
                        {
                            try
                            {
                                volume = solid.MassProperties.Volume;
                            }
                            catch { }
                        }

                        // Xác định Bề dày kết cấu h:
                        // Bắt buộc lấy trực tiếp từ Bề dày do người dùng nhập theo Layer (rowModel.BeDay)
                        double thickness = 0.0;
                        if (rowModel.BeDay.HasValue && rowModel.BeDay.Value > 0)
                        {
                            thickness = rowModel.BeDay.Value;
                        }

                        // Tính diện tích = Khối lượng (Thể tích V) / Bề dày kết cấu (h)
                        // Công thức: S = V / h (m²). Nếu không có Bề dày nhập vào thì bỏ qua điền Diện tích
                        double area = (thickness > 0.00001 && volume > 0.0) ? Math.Round(volume / thickness, 2) : 0.0;

                        if (thickness <= 0.00001 && volume > 0.0 && warnedLayersWithoutThickness.Add(ent.Layer))
                        {
                            log($"  ℹ️ Layer [{ent.Layer}]: Không có Bề dày kết cấu nhập vào -> Bỏ qua điền Diện tích (Khối lượng V = {volume:F3} m³).");
                        }

                        // Độ chặt
                        string doChat = !string.IsNullOrWhiteSpace(rowModel.DoChat)
                            ? rowModel.DoChat
                            : CapNhatMauVaPropertySetForm.ExtractDoChat(rowModel.LayerName);

                        // Cấu kiện & Vật liệu & Hạng mục
                        string cauKien = !string.IsNullOrWhiteSpace(rowModel.CauKien) ? rowModel.CauKien : rowModel.LayerName;
                        string vatLieu = !string.IsNullOrWhiteSpace(rowModel.VatLieu) ? rowModel.VatLieu : cauKien;
                        string hangMuc = !string.IsNullOrWhiteSpace(rowModel.HangMuc) ? rowModel.HangMuc : (!string.IsNullOrWhiteSpace(defaultCategory) ? defaultCategory : "Đường giao thông");

                        // Nâng quyền ForWrite cho thực thể
                        ent.UpgradeOpen();

                        // Đảm bảo gắn đủ 4 Property Set vào đối tượng
                        foreach (var defId in psetDefIds.Values)
                        {
                            bool hasPset = false;
                            var existingSets = PropertyDataServices.GetPropertySets(ent);
                            foreach (ObjectId psId in existingSets)
                            {
                                if (tr.GetObject(psId, OpenMode.ForRead) is PropertySet ps && ps.PropertySetDefinition == defId)
                                {
                                    hasPset = true;
                                    break;
                                }
                            }
                            if (!hasPset)
                            {
                                try
                                {
                                    PropertyDataServices.AddPropertySet(ent, defId);
                                }
                                catch { }
                            }
                        }

                        // Quét và cập nhật từng trường thuộc tính trong toàn bộ Property Sets gắn vào đối tượng
                        var currentPsetIds = PropertyDataServices.GetPropertySets(ent);
                        foreach (ObjectId psId in currentPsetIds)
                        {
                            if (tr.GetObject(psId, OpenMode.ForWrite) is PropertySet propSet)
                            {
                                if (tr.GetObject(propSet.PropertySetDefinition, OpenMode.ForRead) is PropertySetDefinition pDef)
                                {
                                    for (int i = 0; i < pDef.Definitions.Count; i++)
                                    {
                                        var propDef = pDef.Definitions[i];
                                        string rawPropName = propDef.Name;
                                        int propId = propSet.PropertyNameToId(rawPropName);
                                        if (propId < 0) continue;

                                        // Kiểm tra giá trị hiện tại
                                        object? currentVal = null;
                                        try { currentVal = propSet.GetAt(propId); } catch { }

                                        // Nếu bật tùy chọn "Chỉ điền các trường còn thiếu" và trường này đã có dữ liệu -> BẢO TOÀN, không ghi đè
                                        if (onlyMissingProperties && !IsPropertyEmpty(currentVal))
                                        {
                                            continue;
                                        }

                                        // So khớp tên thuộc tính
                                        string norm = NormalizePropertyName(rawPropName);
                                        object? newVal = null;

                                        if (norm == "tencaukien" || norm == "caukien")
                                        {
                                            newVal = cauKien;
                                        }
                                        else if (norm == "loaivatlieu" || norm == "vatlieu")
                                        {
                                            newVal = vatLieu;
                                        }
                                        else if (norm == "dochat")
                                        {
                                            newVal = doChat;
                                        }
                                        else if (norm == "hangmuc")
                                        {
                                            newVal = hangMuc;
                                        }
                                        else if (norm == "beday" || norm == "chieuday" || norm == "thickness")
                                        {
                                            if (thickness > 0.00001)
                                            {
                                                newVal = thickness;
                                            }
                                        }
                                        else if (norm == "dientich" || norm == "area")
                                        {
                                            // Trường hợp không có độ dày nhập vào thì bỏ qua việc điền diện tích
                                            if (thickness > 0.00001 && volume > 0.0)
                                            {
                                                newVal = area;
                                            }
                                        }
                                        else if (norm == "khoiluong" || norm == "thetich" || norm == "volume" || norm.Contains("khoiluong") || norm.Contains("thetich"))
                                        {
                                            newVal = Math.Round(volume, 3);
                                        }
                                        else if (norm == "tencongtrinh" || norm == "congtrinh" || norm == "tenduan")
                                        {
                                            if (!string.IsNullOrWhiteSpace(projectTitle)) newVal = projectTitle;
                                        }
                                        else if (norm == "vitri" || norm == "diadiem")
                                        {
                                            if (!string.IsNullOrWhiteSpace(projectLocation)) newVal = projectLocation;
                                        }
                                        else if (norm == "nhomcaukien")
                                        {
                                            if (!string.IsNullOrWhiteSpace(projectComponentGroup)) newVal = projectComponentGroup;
                                        }

                                        if (newVal != null)
                                        {
                                            object converted = ConvertValueForProperty(propDef, newVal);
                                            try
                                            {
                                                propSet.SetAt(propId, converted);
                                            }
                                            catch { }
                                        }
                                    }
                                }
                            }
                        }

                        updatedSolidsCount++;
                        if (updatedSolidsCount <= 10 || updatedSolidsCount % 50 == 0)
                        {
                            log($"  🔹 Solid [{ent.Handle}] (Layer: {ent.Layer}) -> Cấu kiện: '{cauKien}' | Vật liệu: '{vatLieu}' | Dày: {thickness:0.###}m | Diện tích: {area:0.##}m² | Khối lượng: {volume:0.###}m³");
                        }
                    }

                    log($"  ✨ Đã cập nhật xong Property Set cho {updatedSolidsCount} đối tượng 3D Solid / Body.");
                }

                // ================= 3. CHUYỂN MÀU ĐỐI TƯỢNG VỀ BYLAYER =================
                if (applyByLayer && updatedLayerNames.Count > 0)
                {
                    log("Đang quét và chuyển màu các đối tượng trong Layer về ByLayer...");

                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    foreach (ObjectId btrId in bt)
                    {
                        var btrObj = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
                        if (btrObj.IsFromExternalReference) continue;

                        foreach (ObjectId entId in btrObj)
                        {
                            var ent = tr.GetObject(entId, OpenMode.ForRead) as Entity;
                            if (ent != null && updatedLayerNames.Contains(ent.Layer))
                            {
                                if (ent.Color.ColorMethod != ColorMethod.ByLayer)
                                {
                                    ent.UpgradeOpen();
                                    ent.Color = Color.FromColorIndex(ColorMethod.ByLayer, 256);
                                    byLayerCount++;
                                }
                            }
                        }
                    }

                    if (byLayerCount > 0)
                    {
                        log($"  ✨ Đã chuyển màu {byLayerCount} đối tượng về ByLayer.");
                    }
                }

                tr.Commit();
            }

            ed.Regen();
            timer.Stop();

            log($"🎉 Hoàn tất: {updatedLayersCount} Layer, {updatedSolidsCount} 3D Solid / Body, {byLayerCount} đối tượng ByLayer trong {timer.ElapsedMilliseconds} ms ({timer.Elapsed.TotalSeconds:F2}s)!");
            ed.WriteMessage($"\n[AT_CAPNHATMAUVAPROPERTYSET] Đã cập nhật thành công {updatedLayersCount} Layer và {updatedSolidsCount} đối tượng 3D Solid trong {timer.Elapsed.TotalSeconds:F2}s.\n");

            return (updatedLayersCount, updatedSolidsCount, byLayerCount);
        }

        /// <summary>
        /// Quá tải tương thích ngược cho các lời gọi cũ
        /// </summary>
        public static int ExecuteUpdateAll(
            List<LayerBimRowModel> rows,
            bool updateColor,
            bool updateDescription,
            bool applyByLayer,
            bool unlockLayers,
            Action<string> log)
        {
            var res = ExecuteUpdateAll(rows, updateColor, updateDescription, applyByLayer, unlockLayers, true, true, "", "", "", "", log);
            return res.updatedLayers;
        }
        #endregion
    }
}

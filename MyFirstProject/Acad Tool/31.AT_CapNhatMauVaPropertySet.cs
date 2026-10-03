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

using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using AcadSurface = Autodesk.AutoCAD.DatabaseServices.Surface;
using Body = Autodesk.AutoCAD.DatabaseServices.Body;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Color = Autodesk.AutoCAD.Colors.Color;
using Exception = System.Exception;

[assembly: CommandClass(typeof(Civil3DCsharp.CapNhatMauVaPropertySetCmd))]

namespace Civil3DCsharp
{



    /// <summary>
    /// Lệnh Cập Nhật Màu Layer và Thuộc tính Property Set cho 3D Solid theo Chuẩn BIM / EIR (BEP T27)
    /// Lệnh: AT_Cap_Nhat_Mau_Va_Property_Set
    /// </summary>
    public class CapNhatMauVaPropertySetCmd
    {
        [CommandMethod("AT_Cap_Nhat_Mau_Va_Property_Set")]
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
                    new() { Name = "Bề dày (m)", DataType = Autodesk.Aec.PropertyData.DataType.Real, DefaultValue = "0.0", Description = "Bề dày kết cấu (m)" },
                    new() { Name = "Diện tích (m2)", DataType = Autodesk.Aec.PropertyData.DataType.Real, DefaultValue = "0.0", Description = "Diện tích bề mặt trải kết cấu (m2)" }
                }
            },
            {
                "4. Khối lượng",
                new List<PropertyFieldDef>
                {
                    new() { Name = "Khối lượng (m3)", DataType = Autodesk.Aec.PropertyData.DataType.Real, DefaultValue = "0.0", Description = "Thể tích khối lượng 3D Solid (m3)" }
                }
            }
        };

        public static readonly string[] SupportedAppliesToClasses = new string[]
        {
            "AcDb3dSolid",
            "AcDbBody",
            "AeccDbTinSurface",
            "AeccDbGridSurface",
            "AeccDbTinVolumeSurface",
            "AeccDbGridVolumeSurface",
            "AeccDbSurface",
            "AcDbSurface",
            "AcDbPlaneSurface",
            "AcDbExtrudedSurface",
            "AcDbLoftedSurface",
            "AcDbRevolvedSurface",
            "AcDbSweptSurface"
        };

        /// <summary>
        /// Đảm bảo định dạng số thực Property Data Format (BIM / Standard) luôn hiển thị đầy đủ số 0 ở đầu (LeadingZeros = true)
        /// Tránh bị AutoCAD triệt tiêu số 0 ở đầu (ví dụ hiển thị 0.03 thay vì .03)
        /// </summary>
        public static ObjectId EnsureBimPropertyDataFormat(Transaction tr, Database db)
        {
            try
            {
                DictionaryPropertyDataFormat formatDict = new(db);
                string bimFormatName = "BIM_Real_Format";
                ObjectId targetFormatId = ObjectId.Null;

                // 1. Cập nhật định dạng Standard có sẵn trong bản vẽ để luôn hiển thị số 0 ở đầu
                try
                {
                    ObjectId stdId = DictionaryPropertyDataFormat.GetStandardFormat(db);
                    if (!stdId.IsNull && stdId.IsValid)
                    {
                        if (tr.GetObject(stdId, OpenMode.ForWrite) is PropertyDataFormat stdPdf)
                        {
                            stdPdf.LeadingZeros = true;
                            stdPdf.TrailingZeros = false;
                            if (stdPdf.Precision < 3) stdPdf.Precision = 3;
                            targetFormatId = stdId;
                        }
                    }
                }
                catch { }

                // 2. Duyệt qua tất cả các Record định dạng hiện có trong Dictionary để bật LeadingZeros = true
                try
                {
                    ObjectIdCollection formatRecs = formatDict.Records;
                    foreach (ObjectId fId in formatRecs)
                    {
                        if (!fId.IsNull && fId.IsValid && !fId.IsErased)
                        {
                            try
                            {
                                if (tr.GetObject(fId, OpenMode.ForWrite) is PropertyDataFormat pdf)
                                {
                                    pdf.LeadingZeros = true;
                                    pdf.TrailingZeros = false;
                                    if (pdf.Precision < 3) pdf.Precision = 3;
                                    if (targetFormatId.IsNull) targetFormatId = fId;
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }

                // 3. Lấy hoặc tạo định dạng chuẩn chuyên dụng BIM_Real_Format
                try
                {
                    if (formatDict.Has(bimFormatName, tr))
                    {
                        ObjectId existingBimId = formatDict.GetAt(bimFormatName);
                        if (tr.GetObject(existingBimId, OpenMode.ForWrite) is PropertyDataFormat existingPdf)
                        {
                            existingPdf.LeadingZeros = true;
                            existingPdf.TrailingZeros = false;
                            existingPdf.Precision = 3;
                        }
                        targetFormatId = existingBimId;
                    }
                    else
                    {
                        PropertyDataFormat newFormat = new();
                        newFormat.SubSetDatabaseDefaults(db);
                        newFormat.Description = "Định dạng số thực BIM (hiển thị số 0 ở đầu: 0.03 thay vì .03)";
                        newFormat.LeadingZeros = true;
                        newFormat.TrailingZeros = false;
                        newFormat.Precision = 3;

                        formatDict.AddNewRecord(bimFormatName, newFormat);
                        tr.AddNewlyCreatedDBObject(newFormat, true);
                        targetFormatId = newFormat.ObjectId;
                    }
                }
                catch { }

                // 4. Đảm bảo biến hệ thống DIMZIN trong Database không triệt tiêu số 0 ở đầu
                try
                {
                    if (db.Dimzin == 0 || db.Dimzin == 2 || db.Dimzin == 4 || db.Dimzin == 6)
                    {
                        db.Dimzin = 8;
                    }
                }
                catch { }

                return targetFormatId;
            }
            catch
            {
                return ObjectId.Null;
            }
        }

        /// <summary>
        /// Lấy hoặc tạo Property Set Definition an toàn (KHÔNG xóa bất kỳ thuộc tính nào có sẵn của người dùng)
        /// Hỗ trợ cả 3D Solid, Body và các loại Surface (Civil 3D & AutoCAD Surface)
        /// </summary>
        public static ObjectId GetOrCreatePropertySetDefinition(Transaction tr, Database db, string psetName, List<PropertyFieldDef> desiredDefs)
        {
            try
            {
                ObjectId bimFormatId = EnsureBimPropertyDataFormat(tr, db);

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
                            foreach (var cls in SupportedAppliesToClasses)
                            {
                                if (!filter.Contains(cls))
                                {
                                    filter.Add(cls);
                                    filterModified = true;
                                }
                            }
                            if (filterModified)
                            {
                                try { existingDef.SetAppliesToFilter(filter, false); } catch { }
                            }

                            var currentNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            for (int i = 0; i < existingDef.Definitions.Count; i++)
                            {
                                var propDef = existingDef.Definitions[i];
                                currentNames.Add(propDef.Name);

                                // Cập nhật FormatId cho các thuộc tính số thực đã có để luôn hiển thị số 0 ở đầu
                                if (propDef.DataType == Autodesk.Aec.PropertyData.DataType.Real && !bimFormatId.IsNull)
                                {
                                    try { propDef.FormatId = bimFormatId; } catch { }
                                }
                            }

                            // Tự động nâng cấp / đổi tên thuộc tính cũ sang tên mới có đơn vị nếu cần
                            for (int i = 0; i < existingDef.Definitions.Count; i++)
                            {
                                var propDef = existingDef.Definitions[i];
                                if (propDef.Name.Equals("Bề dày", StringComparison.OrdinalIgnoreCase) && !currentNames.Contains("Bề dày (m)"))
                                {
                                    try { propDef.Name = "Bề dày (m)"; currentNames.Remove("Bề dày"); currentNames.Add("Bề dày (m)"); } catch { }
                                }
                                else if (propDef.Name.Equals("Diện tích", StringComparison.OrdinalIgnoreCase) && !currentNames.Contains("Diện tích (m2)"))
                                {
                                    try { propDef.Name = "Diện tích (m2)"; currentNames.Remove("Diện tích"); currentNames.Add("Diện tích (m2)"); } catch { }
                                }
                                else if (propDef.Name.Equals("Khối lượng", StringComparison.OrdinalIgnoreCase) && !currentNames.Contains("Khối lượng (m3)"))
                                {
                                    try { propDef.Name = "Khối lượng (m3)"; currentNames.Remove("Khối lượng"); currentNames.Add("Khối lượng (m3)"); } catch { }
                                }
                            }

                            // Chỉ bổ sung thuộc tính còn thiếu, KHÔNG xóa thuộc tính cũ
                            foreach (var item in desiredDefs)
                            {
                                if (!currentNames.Contains(item.Name))
                                {
                                    object defData = ConvertDefaultData(item.DataType, item.DefaultValue);
                                    var propDef = new PropertyDefinition
                                    {
                                        Name = item.Name,
                                        Description = item.Description ?? "",
                                        DataType = item.DataType,
                                        DefaultData = defData
                                    };
                                    if (item.DataType == Autodesk.Aec.PropertyData.DataType.Real && !bimFormatId.IsNull)
                                    {
                                        propDef.FormatId = bimFormatId;
                                    }
                                    existingDef.Definitions.Add(propDef);
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
                    var propDef = new PropertyDefinition
                    {
                        Name = item.Name,
                        Description = item.Description ?? "",
                        DataType = item.DataType,
                        DefaultData = defData
                    };
                    if (item.DataType == Autodesk.Aec.PropertyData.DataType.Real && !bimFormatId.IsNull)
                    {
                        propDef.FormatId = bimFormatId;
                    }
                    propSetDef.Definitions.Add(propDef);
                }

                StringCollection appliesTo = new StringCollection();
                foreach (var cls in SupportedAppliesToClasses)
                {
                    appliesTo.Add(cls);
                }
                try { propSetDef.SetAppliesToFilter(appliesTo, false); } catch { }

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
            s = s.Replace("²", "2").Replace("³", "3");
            s = s.Replace(" ", "").Replace("_", "").Replace("-", "").Replace(":", "").Replace(".", "").Replace("(", "").Replace(")", "").Replace("[", "").Replace("]", "");
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
                    string str = rawValue.ToString() ?? "";
                    if (str.StartsWith(".") || str.StartsWith("-."))
                    {
                        if (double.TryParse(str.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double dv))
                        {
                            return dv.ToString("0.###", CultureInfo.InvariantCulture);
                        }
                    }
                    return str;
            }
        }
        #endregion

        #region Main Execution: Update Layers & Property Sets
        /// <summary>
        /// Đảm bảo Layer được mở khóa tạm thời để thao tác Entity (tạo mới Surface, xóa Body cũ, gắn Property Sets)
        /// </summary>
        private static void EnsureLayerUnlocked(Transaction tr, ObjectId layerId)
        {
            try
            {
                if (!layerId.IsNull && layerId.IsValid)
                {
                    var ltr = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForWrite);
                    if (ltr.IsLocked)
                    {
                        ltr.IsLocked = false;
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Thực thi cập nhật Màu Layer, Mô tả Layer, chuyển đổi Body -> Surface, chuyển ByLayer và Cập nhật Property Set cho 3D Solid & Surface
        /// </summary>
        public static (int updatedLayers, int updatedSolids, int updatedSurfaces, int byLayerCount) ExecuteUpdateAll(
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
            Action<string> log,
            System.Windows.Forms.IWin32Window? owner = null)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) throw new Exception("Không tìm thấy bản vẽ hiện hành!");

            var db = doc.Database;
            var ed = doc.Editor;
            var timer = Stopwatch.StartNew();

            int updatedLayersCount = 0;
            int updatedSolidsCount = 0;
            int updatedSurfacesCount = 0;
            int byLayerCount = 0;

            var updatedLayerMap = rows.ToDictionary(x => x.LayerName, x => x, StringComparer.OrdinalIgnoreCase);
            var updatedLayerNames = new HashSet<string>(updatedLayerMap.Keys, StringComparer.OrdinalIgnoreCase);

            using (var docLock = doc.LockDocument())
            {
                log("🚀 Bắt đầu quá trình cập nhật Màu sắc, Mô tả Layer & Property Set cho 3D Solid & Surface...");

                // ================= 1. CẬP NHẬT MÀU VÀ MÔ TẢ TRÊN LAYERTABLE =================
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var layTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForWrite);

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

                    tr.Commit();
                }

                // ================= 1.5. CHUYỂN ĐỔI TOÀN BỘ BODY THÀNH SURFACE (CONVTOSURFACE) & XÓA BODY CŨ =================
                if (updatedLayerNames.Count > 0)
                {
                    int convertedBodyCount = 0;
                    var bodyRx = RXClass.GetClass(typeof(Body));
                    var remainingBodies = new List<ObjectId>();

                    using (var trConvert = db.TransactionManager.StartTransaction())
                    {
                        var bt = (BlockTable)trConvert.GetObject(db.BlockTableId, OpenMode.ForRead);
                        var ms = (BlockTableRecord)trConvert.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                        foreach (ObjectId entId in ms)
                        {
                            if (!entId.IsValid || entId.IsErased) continue;
                            if (!entId.ObjectClass.IsDerivedFrom(bodyRx)) continue;

                            var b = trConvert.GetObject(entId, OpenMode.ForWrite) as Body;
                            if (b == null || b.IsErased) continue;
                            if (!updatedLayerNames.Contains(b.Layer)) continue;

                            EnsureLayerUnlocked(trConvert, b.LayerId);

                            bool converted = false;
                            try
                            {
                                AcadSurface newSurf = AcadSurface.CreateFrom(b);
                                if (newSurf != null)
                                {
                                    newSurf.SetDatabaseDefaults(db);
                                    newSurf.LayerId = b.LayerId;
                                    newSurf.Color = b.Color;
                                    newSurf.LinetypeId = b.LinetypeId;
                                    newSurf.LineWeight = b.LineWeight;
                                    newSurf.Transparency = b.Transparency;

                                    ms.AppendEntity(newSurf);
                                    trConvert.AddNewlyCreatedDBObject(newSurf, true);

                                    b.Erase(true);
                                    converted = true;
                                    convertedBodyCount++;
                                }
                            }
                            catch
                            {
                                converted = false;
                            }

                            if (!converted)
                            {
                                remainingBodies.Add(entId);
                            }
                        }

                        trConvert.Commit();
                    }

                    // Với các Body chưa chuyển đổi được qua Native API -> Dùng lệnh CONVTOSURFACE của AutoCAD
                    if (remainingBodies.Count > 0)
                    {
                        log($"  🔄 Đang chuyển đổi {remainingBodies.Count} đối tượng Body bằng lệnh CONVTOSURFACE...");
                        var newAppendedIds = new List<ObjectId>();
                        void OnAppended(object sender, ObjectEventArgs args)
                        {
                            if (args.DBObject is AcadSurface)
                            {
                                newAppendedIds.Add(args.DBObject.ObjectId);
                            }
                        }

                        db.ObjectAppended += OnAppended;
                        try
                        {
                            if (owner != null)
                            {
                                using (var interaction = ed.StartUserInteraction(owner.Handle))
                                {
                                    SelectionSet ss = SelectionSet.FromObjectIds(remainingBodies.ToArray());
                                    ed.Command("._CONVTOSURFACE", ss, "");
                                    interaction.End();
                                }
                            }
                            else
                            {
                                SelectionSet ss = SelectionSet.FromObjectIds(remainingBodies.ToArray());
                                ed.Command("._CONVTOSURFACE", ss, "");
                            }
                        }
                        catch (Exception ex)
                        {
                            log($"  ⚠️ Cảnh báo CONVTOSURFACE: {ex.Message}");
                        }
                        finally
                        {
                            db.ObjectAppended -= OnAppended;
                        }

                        convertedBodyCount += newAppendedIds.Count;

                        // Bắt buộc xóa sạch các Body gốc nếu AutoCAD chưa tự xóa
                        using (var trClean = db.TransactionManager.StartTransaction())
                        {
                            foreach (ObjectId bId in remainingBodies)
                            {
                                if (bId.IsNull || !bId.IsValid || bId.IsErased) continue;
                                try
                                {
                                    var obj = trClean.GetObject(bId, OpenMode.ForWrite);
                                    if (!obj.IsErased)
                                    {
                                        obj.Erase(true);
                                    }
                                }
                                catch { }
                            }
                            trClean.Commit();
                        }
                    }

                    if (convertedBodyCount > 0)
                    {
                        log($"  ✨ Đã chuyển đổi {convertedBodyCount} đối tượng Body thành Surface (CONVTOSURFACE) và xóa sạch Body gốc.");
                    }
                }

                // ================= 2. CẬP NHẬT PROPERTY SET CHO 3D SOLID & SURFACE =================
                if (updatePropertySet && updatedLayerNames.Count > 0)
                {
                    using (var tr = db.TransactionManager.StartTransaction())
                    {
                        log("📦 Đang chuẩn bị các Property Set Definitions chuẩn (hỗ trợ Solid & Surface)...");
                        ObjectId bimFormatId = EnsureBimPropertyDataFormat(tr, db);

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

                        log("🔍 Đang quét và cập nhật các đối tượng 3D Solid & Surface thuộc các Layer đã chọn...");

                        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                        var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                        var solidRx = RXClass.GetClass(typeof(Solid3d));
                        var civSurfRx = RXClass.GetClass(typeof(CivSurface));
                        var acadSurfRx = RXClass.GetClass(typeof(AcadSurface));
                        var warnedLayersWithoutThickness = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                        foreach (ObjectId entId in btr)
                        {
                            if (!entId.IsValid || entId.IsErased) continue;
                            var rxClass = entId.ObjectClass;
                            bool isSolid = rxClass.IsDerivedFrom(solidRx);
                            bool isCivSurf = rxClass.IsDerivedFrom(civSurfRx);
                            bool isAcadSurf = rxClass.IsDerivedFrom(acadSurfRx);

                            if (!isSolid && !isCivSurf && !isAcadSurf) continue;

                            var ent = tr.GetObject(entId, OpenMode.ForRead) as Entity;
                            if (ent == null || !updatedLayerMap.TryGetValue(ent.Layer, out var rowModel)) continue;

                            // Phân tích thông tin theo cú pháp tên Layer [Cấu kiện_Vật liệu_Bề dày (m)]
                            var parsedInfo = CapNhatMauVaPropertySetForm.ParseLayerName(ent.Layer);

                            // Xác định Bề dày kết cấu h: ưu tiên rowModel.BeDay nếu người dùng đã chỉnh sửa trên Form, fallback theo parsedInfo
                            double thickness = 0.0;
                            if (rowModel.BeDay.HasValue)
                            {
                                thickness = Math.Max(0.0, rowModel.BeDay.Value);
                            }
                            else if (parsedInfo.HasThicknessDeclared && parsedInfo.BeDay > 0)
                            {
                                thickness = parsedInfo.BeDay;
                            }

                            double volume = 0.0;
                            double area = 0.0;
                            string objectKind = "Solid";
                            string extraLog = "";

                            if (ent is Solid3d solid)
                            {
                                objectKind = "Solid";
                                // 1. Thể tích lấy từ thông tin hình học đối tượng
                                try { volume = solid.MassProperties.Volume; } catch { }

                                // 2. Diện tích:
                                if (thickness > 0.00001)
                                {
                                    // Khi bề dày được khai báo: diện tích bằng khối lượng chia cho bề dày
                                    if (volume > 0.0)
                                    {
                                        area = Math.Round(volume / thickness, 2);
                                    }
                                    else
                                    {
                                        try { area = Math.Round(solid.Area, 2); } catch { }
                                    }
                                }
                                else
                                {
                                    // Khi bề dày không được khai báo trong tên layer: đặt bề dày là 0, diện tích lấy từ thông tin hình học của đối tượng
                                    thickness = 0.0;
                                    try { area = Math.Round(solid.Area, 2); } catch { }
                                }
                            }
                            else if (ent is Autodesk.Civil.DatabaseServices.TinVolumeSurface tinVol)
                            {
                                objectKind = "Civil 3D Volume Surface";
                                try
                                {
                                    var volProps = tinVol.GetVolumeProperties();
                                    double net = Math.Abs(volProps.AdjustedNetVolume) > 0.0001 ? volProps.AdjustedNetVolume : volProps.UnadjustedNetVolume;
                                    double cut = Math.Abs(volProps.AdjustedCutVolume) > 0.0001 ? volProps.AdjustedCutVolume : volProps.UnadjustedCutVolume;
                                    double fill = Math.Abs(volProps.AdjustedFillVolume) > 0.0001 ? volProps.AdjustedFillVolume : volProps.UnadjustedFillVolume;

                                    volume = Math.Round(Math.Abs(net) > 0.0001 ? net : (cut + fill), 3);
                                    if (thickness > 0.00001 && volume > 0.0)
                                    {
                                        area = Math.Round(volume / thickness, 2);
                                    }
                                    else
                                    {
                                        thickness = 0.0;
                                    }
                                    extraLog = $" (Net: {net:0.###} m³, Đào: {cut:0.###} m³, Đắp: {fill:0.###} m³)";
                                }
                                catch { }
                            }
                            else if (ent is Autodesk.Civil.DatabaseServices.TinSurface tinSurf)
                            {
                                objectKind = "Civil 3D TIN Surface";
                                try
                                {
                                    var terrainProps = tinSurf.GetTerrainProperties();
                                    area = terrainProps.SurfaceArea2D > 0.0001 ? terrainProps.SurfaceArea2D : terrainProps.SurfaceArea3D;
                                    area = Math.Round(area, 2);
                                    if (thickness > 0.00001 && area > 0.0)
                                    {
                                        volume = Math.Round(area * thickness, 3);
                                    }
                                    else
                                    {
                                        thickness = 0.0;
                                        volume = 0.0;
                                    }
                                }
                                catch { }
                            }
                            else if (ent is Autodesk.Civil.DatabaseServices.GridSurface gridSurf)
                            {
                                objectKind = "Civil 3D Grid Surface";
                                try
                                {
                                    var terrainProps = gridSurf.GetTerrainProperties();
                                    area = terrainProps.SurfaceArea2D > 0.0001 ? terrainProps.SurfaceArea2D : terrainProps.SurfaceArea3D;
                                    area = Math.Round(area, 2);
                                    if (thickness > 0.00001 && area > 0.0)
                                    {
                                        volume = Math.Round(area * thickness, 3);
                                    }
                                    else
                                    {
                                        thickness = 0.0;
                                        volume = 0.0;
                                    }
                                }
                                catch { }
                            }
                            else if (ent is CivSurface)
                            {
                                objectKind = "Civil 3D Surface";
                            }
                            else if (ent is AcadSurface acadSurf)
                            {
                                objectKind = "AutoCAD Surface";
                                try
                                {
                                    area = Math.Round(acadSurf.GetArea(), 2);
                                    if (thickness > 0.00001 && area > 0.0)
                                    {
                                        volume = Math.Round(area * thickness, 3);
                                    }
                                    else
                                    {
                                        thickness = 0.0;
                                        volume = 0.0;
                                    }
                                }
                                catch { }
                            }

                            if (isSolid && thickness <= 0.00001 && warnedLayersWithoutThickness.Add(ent.Layer))
                            {
                                log($"  ℹ️ Layer [{ent.Layer}]: Bề dày = 0m -> Diện tích hình học Solid3d.Area S = {area:F2} m², Khối lượng V = {volume:F3} m³.");
                            }
                            else if ((isCivSurf || isAcadSurf) && thickness <= 0.00001 && warnedLayersWithoutThickness.Add(ent.Layer))
                            {
                                log($"  ℹ️ Layer [{ent.Layer}]: Bề dày = 0m -> Diện tích hình học S = {area:F2} m².");
                            }

                            // Cấu kiện & Vật liệu & Hạng mục
                            string defaultName = (ent is CivSurface cSurf && !string.IsNullOrWhiteSpace(cSurf.Name)) ? cSurf.Name : rowModel.LayerName;
                            string cauKien = !string.IsNullOrWhiteSpace(rowModel.CauKien)
                                ? rowModel.CauKien
                                : (!string.IsNullOrWhiteSpace(parsedInfo.CauKien) ? parsedInfo.CauKien : defaultName);
                            string vatLieu = !string.IsNullOrWhiteSpace(rowModel.VatLieu)
                                ? rowModel.VatLieu
                                : (!string.IsNullOrWhiteSpace(parsedInfo.VatLieu) ? parsedInfo.VatLieu : cauKien);
                            string hangMuc = !string.IsNullOrWhiteSpace(rowModel.HangMuc)
                                ? rowModel.HangMuc
                                : (!string.IsNullOrWhiteSpace(defaultCategory) ? defaultCategory : "Đường giao thông");
                            string doChat = !string.IsNullOrWhiteSpace(rowModel.DoChat)
                                ? rowModel.DoChat
                                : CapNhatMauVaPropertySetForm.ExtractDoChat(rowModel.LayerName);

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

                                            // Đảm bảo các trường kiểu số thực luôn hiển thị đủ số 0 ở đầu
                                            if (propDef.DataType == Autodesk.Aec.PropertyData.DataType.Real && !bimFormatId.IsNull && (propDef.FormatId.IsNull || propDef.FormatId != bimFormatId))
                                            {
                                                try
                                                {
                                                    pDef.UpgradeOpen();
                                                    propDef.FormatId = bimFormatId;
                                                }
                                                catch { }
                                            }

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
                                            else if (norm == "beday" || norm == "bedaym" || norm.StartsWith("beday")
                                                  || norm == "chieuday" || norm == "chieudaym" || norm.StartsWith("chieuday")
                                                  || norm == "thickness" || norm == "thicknessm")
                                            {
                                                // Quy định: Nếu bề dày không khai báo thì đặt là 0, ngược lại gán giá trị bề dày (m)
                                                newVal = (thickness > 0.00001) ? thickness : 0.0;
                                            }
                                            else if (norm == "dientich" || norm == "dientichm2" || norm.StartsWith("dientich")
                                                  || norm == "area" || norm == "aream2" || norm.StartsWith("area"))
                                            {
                                                // Quy định: Diện tích = Khối lượng / Bề dày (nếu có bề dày), nếu bề dày = 0 thì lấy từ thông tin hình học
                                                newVal = Math.Round(area, 2);
                                            }
                                            else if (norm.Contains("khoiluong") || norm.Contains("thetich") || norm.Contains("volume"))
                                            {
                                                // Quy định: Thể tích lấy từ thông tin hình học đối tượng
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

                            if (isCivSurf || isAcadSurf)
                            {
                                updatedSurfacesCount++;
                                if (updatedSurfacesCount <= 10 || updatedSurfacesCount % 20 == 0)
                                {
                                    string surfName = (ent is CivSurface cs && !string.IsNullOrWhiteSpace(cs.Name)) ? $" '{cs.Name}'" : "";
                                    string thickStr = thickness > 0.00001 ? $"{thickness:0.###}m" : "0m (hình học)";
                                    log($"  🔹 {objectKind}{surfName} [{ent.Handle}] (Layer: {ent.Layer}) -> Cấu kiện: '{cauKien}' | Dày: {thickStr} | Diện tích: {area:0.##}m² | Khối lượng: {volume:0.###}m³{extraLog}");
                                }
                            }
                            else
                            {
                                updatedSolidsCount++;
                                if (updatedSolidsCount <= 10 || updatedSolidsCount % 50 == 0)
                                {
                                    string thickStr = thickness > 0.00001 ? $"{thickness:0.###}m" : "0m (hình học)";
                                    string areaMethod = thickness > 0.00001 ? "V/h" : "hình học";
                                    log($"  🔹 {objectKind} [{ent.Handle}] (Layer: {ent.Layer}) -> Cấu kiện: '{cauKien}' | Vật liệu: '{vatLieu}' | Dày: {thickStr} | Diện tích ({areaMethod}): {area:0.##}m² | Khối lượng: {volume:0.###}m³");
                                }
                            }
                        }

                        log($"  ✨ Đã cập nhật xong Property Set cho {updatedSolidsCount} đối tượng 3D Solid và {updatedSurfacesCount} Surface.");
                        tr.Commit();
                    }
                }

                // ================= 3. CHUYỂN MÀU ĐỐI TƯỢNG VỀ BYLAYER =================
                if (applyByLayer && updatedLayerNames.Count > 0)
                {
                    using (var tr = db.TransactionManager.StartTransaction())
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
                                    bool modified = false;

                                    // 1. Reset màu Entity về ByLayer
                                    if (ent.Color.ColorMethod != ColorMethod.ByLayer)
                                    {
                                        ent.UpgradeOpen();
                                        ent.Color = Color.FromColorIndex(ColorMethod.ByLayer, 256);
                                        modified = true;
                                    }

                                    // 2. Reset Vật liệu (Material) về ByLayer để không bị đè màu render
                                    try
                                    {
                                        if (!string.IsNullOrEmpty(ent.Material) && !string.Equals(ent.Material, "ByLayer", StringComparison.OrdinalIgnoreCase))
                                        {
                                            ent.UpgradeOpen();
                                            ent.Material = "ByLayer";
                                            modified = true;
                                        }
                                    }
                                    catch { }

                                    // 3. Reset Độ trong suốt (Transparency) về ByLayer
                                    try
                                    {
                                        if (!ent.Transparency.IsByLayer)
                                        {
                                            ent.UpgradeOpen();
                                            ent.Transparency = new Transparency(TransparencyMethod.ByLayer);
                                            modified = true;
                                        }
                                    }
                                    catch { }

                                    // 4. Làm sạch màu từng mặt (Face Color) cho 3D Solid
                                    if (ent is Solid3d solid)
                                    {
                                        try
                                        {
                                            solid.UpgradeOpen();
                                            solid.CleanBody();
                                            modified = true;
                                        }
                                        catch { }
                                    }

                                    if (modified)
                                    {
                                        byLayerCount++;
                                    }
                                }
                            }
                        }

                        if (byLayerCount > 0)
                        {
                            log($"  ✨ Đã chuyển {byLayerCount} đối tượng về ByLayer (làm sạch Material, Transparency và màu mặt Face Color của 3D Solid).");
                        }

                        tr.Commit();
                    }
                }
            }

            ed.Regen();
            timer.Stop();

            log($"🎉 Hoàn tất: {updatedLayersCount} Layer, {updatedSolidsCount} 3D Solid, {updatedSurfacesCount} Surface, {byLayerCount} đối tượng ByLayer trong {timer.ElapsedMilliseconds} ms ({timer.Elapsed.TotalSeconds:F2}s)!");
            ed.WriteMessage($"\n[AT_CAPNHATMAUVAPROPERTYSET] Đã cập nhật thành công {updatedLayersCount} Layer, {updatedSolidsCount} 3D Solid và {updatedSurfacesCount} Surface trong {timer.Elapsed.TotalSeconds:F2}s.\n");

            return (updatedLayersCount, updatedSolidsCount, updatedSurfacesCount, byLayerCount);
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

// (C) Copyright 2026 by T27
// MCP Bridge — Mô-đun mở rộng: Bảng AutoCAD gốc (AcDbTable) & Property Set / AEC Schedule Table
// - POST /table    : Tạo bảng AcDbTable gốc (không sinh Text rỗng, hỗ trợ ô công thức "=Sum(C3:C6)")
// - POST /propsets : Đọc / gán Property Set, sao chép định nghĩa & kiểu bảng Schedule giữa các bản vẽ,
//                    liệt kê / chèn / cập nhật bảng Schedule (AEC_SCHEDULE_TABLE) tự cập nhật theo đối tượng
//
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Autodesk.Aec.PropertyData.DatabaseServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using AecDataType = Autodesk.Aec.PropertyData.DataType;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using ATable = Autodesk.AutoCAD.DatabaseServices.Table;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;

namespace Civil3DCsharp
{
    public partial class AT_McpBridgeServer
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // 1. BẢNG AUTOCAD GỐC (AcDbTable)
        // ═══════════════════════════════════════════════════════════════════════════

        private static object CreateNativeTable(Document doc, string body)
        {
            Database db = doc.Database;
            using JsonDocument jd = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            JsonElement root = jd.RootElement;

            string title = GetStr(root, "title", "");
            List<string> headers = ReadStringList(root, "headers");
            List<List<string>> rows = ReadStringMatrix(root, "rows");
            List<double> colWidths = ReadDoubleList(root, "col_widths");
            double rowH = Math.Max(0.1, GetDbl(root, "row_height", 3.5));
            double txtH = Math.Max(0.05, GetDbl(root, "text_height", 1.4));
            string layer = GetStr(root, "layer", "BANG_THONG_KE");
            Point3d ins = GetPt3d(root, "insert_point", Point3d.Origin);
            bool zoomToTable = GetBool(root, "zoom_to_table", false);
            CellAlignment dataAlign = ParseCellAlignment(GetStr(root, "data_alignment", "middle_center"));

            bool hasTitle = !string.IsNullOrWhiteSpace(title);
            bool hasHeader = headers.Count > 0;
            int nCols = Math.Max(1, Math.Max(headers.Count, rows.Count == 0 ? 0 : rows.Max(r => r.Count)));
            int nRows = (hasTitle ? 1 : 0) + (hasHeader ? 1 : 0) + rows.Count;
            if (nRows == 0)
                throw new ArgumentException("Bảng không có dữ liệu (cần ít nhất title, headers hoặc rows).");

            int formulaCells = 0;
            var formulaErrors = new List<string>();
            string handle;
            Extents3d ext;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                if (!lt.Has(layer))
                {
                    lt.UpgradeOpen();
                    EnsureLayerInTr(tr, db, lt, layer, 7);
                }
                ObjectId styleId = EnsureTahomaTextStyle(tr, db);

                var tb = new ATable();
                tb.SetDatabaseDefaults(db);
                if (!db.Tablestyle.IsNull) tb.TableStyle = db.Tablestyle;
                tb.Position = ins;
                tb.SetSize(nRows, nCols);
                tb.Layer = layer;

                for (int c = 0; c < nCols; c++)
                {
                    double w = (c < colWidths.Count && colWidths[c] > 0)
                        ? colWidths[c]
                        : AutoColumnWidth(c, headers, rows, txtH);
                    tb.Columns[c].Width = w;
                }

                int r = 0;
                if (hasTitle)
                {
                    tb.Rows[r].Style = "_TITLE";
                    tb.Rows[r].Height = rowH * 1.35;
                    if (nCols > 1) tb.MergeCells(CellRange.Create(tb, r, 0, r, nCols - 1));
                    SetNativeCell(tb, r, 0, title, txtH * 1.25, styleId, CellAlignment.MiddleCenter, ref formulaCells, formulaErrors);
                    r++;
                }
                if (hasHeader)
                {
                    tb.Rows[r].Style = "_HEADER";
                    tb.Rows[r].Height = rowH * 1.15;
                    for (int c = 0; c < nCols; c++)
                    {
                        string h = c < headers.Count ? headers[c] : "";
                        SetNativeCell(tb, r, c, h, txtH, styleId, CellAlignment.MiddleCenter, ref formulaCells, formulaErrors);
                    }
                    r++;
                }
                foreach (List<string> row in rows)
                {
                    tb.Rows[r].Style = "_DATA";
                    tb.Rows[r].Height = rowH;
                    for (int c = 0; c < nCols; c++)
                    {
                        string v = c < row.Count ? row[c] : "";
                        SetNativeCell(tb, r, c, v, txtH * 0.92, styleId, dataAlign, ref formulaCells, formulaErrors);
                    }
                    r++;
                }

                tb.GenerateLayout();
                ms.AppendEntity(tb);
                tr.AddNewlyCreatedDBObject(tb, true);
                try { tb.RecomputeTableBlock(true); } catch { }

                handle = tb.Handle.ToString();
                ext = SafeGet(() => tb.GeometricExtents,
                    new Extents3d(new Point3d(ins.X, ins.Y - tb.Height, 0), new Point3d(ins.X + tb.Width, ins.Y, 0)));
                tr.Commit();
            }

            if (zoomToTable)
            {
                try
                {
                    double padX = (ext.MaxPoint.X - ext.MinPoint.X) * 0.05;
                    double padY = (ext.MaxPoint.Y - ext.MinPoint.Y) * 0.05;
                    string p1 = string.Format(CultureInfo.InvariantCulture, "{0},{1}", ext.MinPoint.X - padX, ext.MinPoint.Y - padY);
                    string p2 = string.Format(CultureInfo.InvariantCulture, "{0},{1}", ext.MaxPoint.X + padX, ext.MaxPoint.Y + padY);
                    doc.SendStringToExecute($"._ZOOM _W {p1} {p2}\n", true, false, false);
                    PostMessageW(Application.MainWindow.Handle, WM_NULL, IntPtr.Zero, IntPtr.Zero);
                }
                catch { }
            }

            return new Dictionary<string, object?>
            {
                ["status"] = "success",
                ["engine"] = $"C# .NET AcDbTable (PID {Environment.ProcessId})",
                ["document"] = doc.Name,
                ["handle"] = handle,
                ["rows"] = nRows,
                ["columns"] = nCols,
                ["formula_cells"] = formulaCells,
                ["formula_errors"] = formulaErrors,
                ["bounding_box"] = new[] { R4(ext.MinPoint.X), R4(ext.MinPoint.Y), R4(ext.MaxPoint.X), R4(ext.MaxPoint.Y) },
                ["width"] = R4(ext.MaxPoint.X - ext.MinPoint.X),
                ["height"] = R4(ext.MaxPoint.Y - ext.MinPoint.Y),
            };
        }

        private static void SetNativeCell(ATable tb, int row, int col, string text, double height, ObjectId styleId,
            CellAlignment align, ref int formulaCells, List<string> formulaErrors)
        {
            Cell cell = tb.Cells[row, col];
            cell.TextStyleId = styleId;
            cell.TextHeight = height;
            cell.Alignment = align;
            if (string.IsNullOrEmpty(text)) return; // Ô trống: không tạo nội dung

            if (text.Length > 1 && text[0] == '=')
            {
                try
                {
                    if (cell.Contents.Count == 0) cell.Contents.Add();
                    cell.Contents[0].Formula = text;
                    formulaCells++;
                    return;
                }
                catch (System.Exception ex)
                {
                    formulaErrors.Add($"R{row + 1}C{col + 1} '{text}': {ex.Message}");
                }
            }
            cell.TextString = text;
        }

        private static double AutoColumnWidth(int c, List<string> headers, List<List<string>> rows, double txtH)
        {
            int maxLen = c < headers.Count ? headers[c].Length : 0;
            foreach (List<string> r in rows)
                if (c < r.Count && r[c] != null && !r[c].StartsWith("=")) maxLen = Math.Max(maxLen, r[c].Length);
            return Math.Max(txtH * 6.0, maxLen * txtH * 0.85 + txtH * 2.5);
        }

        private static CellAlignment ParseCellAlignment(string s)
        {
            switch ((s ?? "").Trim().ToLowerInvariant())
            {
                case "left": case "middle_left": return CellAlignment.MiddleLeft;
                case "right": case "middle_right": return CellAlignment.MiddleRight;
                default: return CellAlignment.MiddleCenter;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // 2. PROPERTY SET & AEC SCHEDULE TABLE
        // ═══════════════════════════════════════════════════════════════════════════

        private static object ExecutePropertySetRequest(Document doc, string body)
        {
            using JsonDocument jd = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            JsonElement root = jd.RootElement;
            string action = GetStr(root, "action", "list_definitions").Trim().ToLowerInvariant();

            object result = action switch
            {
                "list_definitions" => PsListDefinitions(doc, root),
                "get_values" => PsGetValues(doc, root),
                "attach" or "set_values" => PsAttachAndSet(doc, root),
                "import" => PsImport(doc, root),
                "list_schedule_styles" => PsListScheduleStyles(doc, root),
                "list_schedule_tables" => PsListScheduleTables(doc, root),
                "insert_schedule_table" => PsInsertScheduleTable(doc, root),
                "update_schedule_tables" => PsUpdateScheduleTables(doc, root),
                _ => throw new ArgumentException(
                    $"Action không hợp lệ: '{action}'. Hỗ trợ: list_definitions, get_values, attach, set_values, import, " +
                    "list_schedule_styles, list_schedule_tables, insert_schedule_table, update_schedule_tables."),
            };
            return result;
        }

        // ---------- 2.1 Liệt kê định nghĩa Property Set ----------
        private static object PsListDefinitions(Document doc, JsonElement root)
        {
            Database db = doc.Database;
            string nameFilter = GetStr(root, "name", GetStr(root, "property_set", ""));
            bool includeUsage = GetBool(root, "include_usage", true);
            var defs = new List<object>();

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var dict = new DictionaryPropertySetDefinitions(db);
                foreach (ObjectId id in dict.Records)
                {
                    if (!id.IsValid || id.IsErased) continue;
                    if (tr.GetObject(id, OpenMode.ForRead) is not PropertySetDefinition def) continue;
                    if (!string.IsNullOrWhiteSpace(nameFilter) &&
                        def.Name.IndexOf(nameFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;

                    var props = new List<object>();
                    var warnings = new List<string>();
                    for (int i = 0; i < def.Definitions.Count; i++)
                    {
                        PropertyDefinition pd = def.Definitions[i];
                        string kind = pd is PropertyDefinitionFormula ? "formula" : (SafeGet(() => pd.Automatic, false) ? "automatic" : "manual");
                        var p = new Dictionary<string, object?>
                        {
                            ["name"] = pd.Name,
                            ["id"] = SafeGet(() => pd.Id, -1),
                            ["kind"] = kind,
                            ["data_type"] = SafeGet(() => pd.DataType.ToString(), "?"),
                            ["read_only"] = SafeGet(() => pd.IsReadOnly, false),
                            ["visible"] = SafeGet(() => pd.IsVisible, true),
                            ["default"] = JsonSafe(SafeGet<object?>(() => pd.DefaultData, null)),
                        };
                        if (pd is PropertyDefinitionFormula pf)
                        {
                            string formula = SafeGet(() => pf.GetFormulaString(), SafeGet(() => pf.FormulaString, ""));
                            p["formula"] = formula;
                            warnings.AddRange(AnalyzeFormula(pd.Name, formula));
                        }
                        else if (kind == "automatic")
                        {
                            p["automatic_sources"] = SafeGet(() => pd.GetAutomaticData().Select(DescribeObject).ToList(), new List<Dictionary<string, string>>());
                        }
                        props.Add(p);
                    }

                    var d = new Dictionary<string, object?>
                    {
                        ["name"] = def.Name,
                        ["handle"] = def.Handle.ToString(),
                        ["description"] = SafeGet(() => def.Description, ""),
                        ["applies_to_all"] = SafeGet(() => def.AppliesToAll, false),
                        ["applies_to"] = SafeGet(() => def.AppliesToFilter.Cast<string>().ToList(), new List<string>()),
                        ["is_style_based"] = SafeGet(() => def.IsStyleBased, false),
                        ["property_count"] = def.Definitions.Count,
                        ["properties"] = props,
                        ["warnings"] = warnings,
                    };
                    if (includeUsage)
                        d["attached_count"] = SafeGet(() => PropertyDataServices.GetAllPropertySetsUsingDefinition(id, false).Count, -1);
                    defs.Add(d);
                }
                tr.Commit();
            }

            return new { status = "success", action = "list_definitions", document = doc.Name, count = defs.Count, definitions = defs };
        }

        /// <summary>Phân tích công thức VBScript của thuộc tính Formula để cảnh báo các điểm dễ lỗi.</summary>
        private static List<string> AnalyzeFormula(string propName, string formula)
        {
            var w = new List<string>();
            if (string.IsNullOrWhiteSpace(formula)) return w;
            if (formula.IndexOf("ActiveDocument", StringComparison.OrdinalIgnoreCase) >= 0 &&
                formula.IndexOf("HandleToObject", StringComparison.OrdinalIgnoreCase) >= 0)
                w.Add($"[{propName}] Dùng ActiveDocument.HandleToObject: khi mở nhiều bản vẽ, giá trị sẽ sai/'--' nếu bản vẽ khác đang active lúc cập nhật.");
            Match m = Regex.Match(formula, @"AeccApplication\.(\d+\.\d+)", RegexOptions.IgnoreCase);
            if (m.Success)
                w.Add($"[{propName}] Ghi cứng phiên bản COM 'AeccApplication.{m.Groups[1].Value}': sẽ trả '--' khi nâng cấp Civil 3D.");
            if (formula.IndexOf("On Error Resume Next", StringComparison.OrdinalIgnoreCase) >= 0)
                w.Add($"[{propName}] 'On Error Resume Next' che mọi lỗi: chỉ thấy giá trị mặc định, không biết nguyên nhân.");
            return w;
        }

        // ---------- 2.2 Đọc giá trị Property Set của đối tượng ----------
        private static object PsGetValues(Document doc, JsonElement root)
        {
            Database db = doc.Database;
            string psFilter = GetStr(root, "property_set", "");
            int page = Math.Max(1, GetInt(root, "page", 1));
            int pageSize = Math.Clamp(GetInt(root, "page_size", 50), 1, 500);
            bool onlyWith = GetBool(root, "only_with_property_sets", true);
            var items = new List<object>();
            int total = 0;
            List<string> notFound;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                List<ObjectId> ids = PsResolveTargets(tr, db, root, out notFound, defaultAll: true);
                var defCache = new Dictionary<ObjectId, PropertySetDefinition?>();
                int skip = (page - 1) * pageSize;

                foreach (ObjectId id in ids)
                {
                    if (tr.GetObject(id, OpenMode.ForRead) is not DBObject obj) continue;
                    var sets = new List<object>();
                    foreach (ObjectId psId in SafeGet(() => PropertyDataServices.GetPropertySets(obj), new ObjectIdCollection()))
                    {
                        if (tr.GetObject(psId, OpenMode.ForRead) is not PropertySet ps) continue;
                        PropertySetDefinition? def = GetCachedDef(tr, defCache, ps.PropertySetDefinition);
                        if (def == null) continue;
                        if (!string.IsNullOrWhiteSpace(psFilter) && !def.Name.Equals(psFilter, StringComparison.OrdinalIgnoreCase)) continue;
                        sets.Add(new { name = def.Name, values = ReadPropertySetValues(def, ps, obj) });
                    }
                    if (onlyWith && sets.Count == 0) continue;

                    total++;
                    if (total <= skip || items.Count >= pageSize) continue;
                    items.Add(new
                    {
                        handle = obj.Handle.ToString(),
                        dxf_name = id.ObjectClass.DxfName,
                        layer = (obj as Entity)?.Layer ?? "",
                        name = SafeGet(() => (string?)obj.GetType().GetProperty("Name")?.GetValue(obj), null),
                        property_sets = sets,
                    });
                }
                tr.Commit();
            }

            return new
            {
                status = "success",
                action = "get_values",
                document = doc.Name,
                total_matched = total,
                page,
                page_size = pageSize,
                total_pages = (int)Math.Ceiling(total / (double)pageSize),
                not_found_handles = notFound,
                entities = items,
            };
        }

        private static Dictionary<string, object?> ReadPropertySetValues(PropertySetDefinition def, PropertySet ps, DBObject obj)
        {
            var values = new Dictionary<string, object?>();
            var emptyPath = new ObjectIdCollection();
            for (int i = 0; i < def.Definitions.Count; i++)
            {
                PropertyDefinition pd = def.Definitions[i];
                int pid = SafeGet(() => ps.PropertyNameToId(pd.Name), -1);
                object? v = null;
                if (pid >= 0)
                {
                    v = SafeGet<object?>(() => def.GetValue(pid, obj, emptyPath), null);
                    if (v == null) v = SafeGet<object?>(() => ps.GetAt(pid), null);
                }
                values[pd.Name] = JsonSafe(v);
            }
            return values;
        }

        // ---------- 2.3 Gắn Property Set & gán giá trị ----------
        private static object PsAttachAndSet(Document doc, JsonElement root)
        {
            Database db = doc.Database;
            string psName = GetStr(root, "property_set", "");
            if (string.IsNullOrWhiteSpace(psName))
                throw new ArgumentException("Thiếu 'property_set' (tên định nghĩa Property Set cần gắn).");
            bool dryRun = GetBool(root, "dry_run", false);
            bool updateSchedules = GetBool(root, "update_schedules", true);

            var common = ReadValueMap(root, "values");
            var perHandle = new Dictionary<string, Dictionary<string, JsonElement>>(StringComparer.OrdinalIgnoreCase);
            if (root.TryGetProperty("per_handle_values", out JsonElement phEl) && phEl.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty hp in phEl.EnumerateObject())
                {
                    var m = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
                    if (hp.Value.ValueKind == JsonValueKind.Object)
                        foreach (JsonProperty vp in hp.Value.EnumerateObject()) m[vp.Name] = vp.Value.Clone();
                    perHandle[hp.Name.Trim().ToUpperInvariant()] = m;
                }
            }

            int attached = 0, alreadyAttached = 0, valuesSet = 0, scheduleUpdated = 0;
            var errors = new List<string>();
            var touched = new List<string>();
            List<string> notFound;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var dict = new DictionaryPropertySetDefinitions(db);
                if (!dict.Has(psName, tr))
                    throw new ArgumentException($"Bản vẽ không có định nghĩa Property Set '{psName}'. Dùng action 'import' để chép từ bản vẽ mẫu trước.");
                ObjectId defId = dict.GetAt(psName);
                var def = (PropertySetDefinition)tr.GetObject(defId, OpenMode.ForRead);

                List<ObjectId> ids = PsResolveTargets(tr, db, root, out notFound);
                // Với per_handle_values, tự bổ sung các handle đó vào danh sách đích
                foreach (string h in perHandle.Keys)
                {
                    if (TryGetIdFromHandle(db, h, out ObjectId hid) && !ids.Contains(hid)) ids.Add(hid);
                    else if (!TryGetIdFromHandle(db, h, out _)) notFound.Add(h);
                }
                if (ids.Count == 0)
                    throw new ArgumentException("Không có đối tượng đích: cần 'handles', 'per_handle_values' hoặc bộ lọc 'entity_type'/'layer'.");

                foreach (ObjectId id in ids)
                {
                    string hStr = SafeGet(() => id.Handle.ToString(), "?");
                    try
                    {
                        var obj = (DBObject)tr.GetObject(id, OpenMode.ForRead);
                        ObjectId psId = SafeGet(() => PropertyDataServices.GetPropertySet(obj, defId), ObjectId.Null);
                        if (psId.IsNull)
                        {
                            if (dryRun) { attached++; touched.Add(hStr); continue; }
                            obj.UpgradeOpen();
                            PropertyDataServices.AddPropertySet(obj, defId);
                            psId = PropertyDataServices.GetPropertySet(obj, defId);
                            if (psId.IsNull) { errors.Add($"{hStr}: không gắn được Property Set (kiểm tra 'Applies To' của định nghĩa)."); continue; }
                            attached++;
                        }
                        else alreadyAttached++;

                        var vals = new Dictionary<string, JsonElement>(common, StringComparer.OrdinalIgnoreCase);
                        if (perHandle.TryGetValue(hStr.ToUpperInvariant(), out var hv))
                            foreach (var kv in hv) vals[kv.Key] = kv.Value;
                        if (vals.Count == 0) { touched.Add(hStr); continue; }
                        if (dryRun) { touched.Add(hStr); continue; }

                        var ps = (PropertySet)tr.GetObject(psId, OpenMode.ForWrite);
                        foreach (var kv in vals)
                        {
                            int idx = def.Definitions.IndexOf(kv.Key);
                            if (idx < 0) { errors.Add($"{hStr}: Property Set '{psName}' không có thuộc tính '{kv.Key}'."); continue; }
                            PropertyDefinition pd = def.Definitions[idx];
                            if (pd is PropertyDefinitionFormula || SafeGet(() => pd.Automatic, false))
                            {
                                errors.Add($"{hStr}: '{kv.Key}' là thuộc tính tự động/công thức, không gán tay được.");
                                continue;
                            }
                            int pid = ps.PropertyNameToId(pd.Name);
                            ps.SetAt(pid, ConvertJsonToAec(kv.Value, pd.DataType));
                            valuesSet++;
                        }
                        touched.Add(hStr);
                    }
                    catch (System.Exception ex)
                    {
                        errors.Add($"{hStr}: {ex.Message}");
                    }
                }

                if (!dryRun && updateSchedules)
                    scheduleUpdated = RegenerateAllScheduleTables(tr, db, null, errors);
                tr.Commit();
            }

            return new
            {
                status = errors.Count == 0 ? "success" : "partial",
                action = "attach",
                document = doc.Name,
                property_set = psName,
                dry_run = dryRun,
                targets = touched.Count,
                attached,
                already_attached = alreadyAttached,
                values_set = valuesSet,
                schedule_tables_updated = scheduleUpdated,
                not_found_handles = notFound,
                errors,
                handles = touched.Take(200).ToList(),
            };
        }

        // ---------- 2.4 Chép định nghĩa Property Set & kiểu bảng Schedule từ bản vẽ nguồn ----------
        private static object PsImport(Document doc, JsonElement root)
        {
            Database targetDb = doc.Database;
            string srcName = GetStr(root, "source_document", "");
            string srcFile = GetStr(root, "source_file", "");
            List<string> psNames = ReadStringList(root, "property_set_names");
            List<string> styleNames = ReadStringList(root, "schedule_style_names");
            bool overwrite = GetBool(root, "overwrite", false);
            if (psNames.Count == 0 && styleNames.Count == 0)
                throw new ArgumentException("Cần 'property_set_names' và/hoặc 'schedule_style_names' để chép.");

            Document? srcDoc = null;
            Database? srcDb = null;
            bool ownsSrcDb = false;
            if (!string.IsNullOrWhiteSpace(srcName))
            {
                srcDoc = ResolveTargetDocument(srcName);
                if (srcDoc == doc) throw new ArgumentException("Bản vẽ nguồn trùng với bản vẽ đích.");
                srcDb = srcDoc.Database;
            }
            else if (!string.IsNullOrWhiteSpace(srcFile))
            {
                if (!File.Exists(srcFile)) throw new FileNotFoundException($"Không tìm thấy file nguồn: {srcFile}");
                srcDb = new Database(false, true);
                srcDb.ReadDwgFile(srcFile, FileOpenMode.OpenForReadAndAllShare, true, "");
                srcDb.CloseInput(true);
                ownsSrcDb = true;
            }
            else throw new ArgumentException("Cần 'source_document' (tên bản vẽ đang mở) hoặc 'source_file' (đường dẫn .dwg).");

            var imported = new List<string>();
            var skipped = new List<string>();
            var missing = new List<string>();
            try
            {
                using DocumentLock? srcLock = srcDoc?.LockDocument();
                var psIds = new ObjectIdCollection();
                var styIds = new ObjectIdCollection();

                using (Transaction str = srcDb.TransactionManager.StartTransaction())
                {
                    var srcPs = new DictionaryPropertySetDefinitions(srcDb);
                    foreach (string n in psNames)
                    {
                        if (srcPs.Has(n, str)) psIds.Add(srcPs.GetAt(n)); else missing.Add($"PropertySet:{n}");
                    }
                    var srcSty = new DictionaryScheduleTableStyle(srcDb);
                    foreach (string n in styleNames)
                    {
                        if (srcSty.Has(n, str)) styIds.Add(srcSty.GetAt(n)); else missing.Add($"ScheduleStyle:{n}");
                    }
                    str.Commit();
                }

                var tgtPs = new DictionaryPropertySetDefinitions(targetDb);
                var tgtSty = new DictionaryScheduleTableStyle(targetDb);
                var existedBefore = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (Transaction ttr = targetDb.TransactionManager.StartTransaction())
                {
                    foreach (string n in psNames) if (tgtPs.Has(n, ttr)) existedBefore.Add($"PropertySet:{n}");
                    foreach (string n in styleNames) if (tgtSty.Has(n, ttr)) existedBefore.Add($"ScheduleStyle:{n}");
                    ttr.Commit();
                }

                DuplicateRecordCloning drc = overwrite ? DuplicateRecordCloning.Replace : DuplicateRecordCloning.Ignore;
                if (psIds.Count > 0)
                {
                    ObjectId owner = EnsureAecDictionaryId(targetDb, true);
                    srcDb.WblockCloneObjects(psIds, owner, new IdMapping(), drc, false);
                }
                if (styIds.Count > 0)
                {
                    ObjectId owner = EnsureAecDictionaryId(targetDb, false);
                    srcDb.WblockCloneObjects(styIds, owner, new IdMapping(), drc, false);
                }

                using (Transaction ttr = targetDb.TransactionManager.StartTransaction())
                {
                    var tgtPs2 = new DictionaryPropertySetDefinitions(targetDb);
                    var tgtSty2 = new DictionaryScheduleTableStyle(targetDb);
                    foreach (string n in psNames)
                    {
                        string key = $"PropertySet:{n}";
                        if (missing.Contains(key)) continue;
                        if (!tgtPs2.Has(n, ttr)) { missing.Add(key + " (chép thất bại)"); continue; }
                        if (existedBefore.Contains(key) && !overwrite) skipped.Add(key); else imported.Add(key);
                    }
                    foreach (string n in styleNames)
                    {
                        string key = $"ScheduleStyle:{n}";
                        if (missing.Contains(key)) continue;
                        if (!tgtSty2.Has(n, ttr)) { missing.Add(key + " (chép thất bại)"); continue; }
                        if (existedBefore.Contains(key) && !overwrite) skipped.Add(key); else imported.Add(key);
                    }
                    ttr.Commit();
                }
            }
            finally
            {
                if (ownsSrcDb) srcDb.Dispose();
            }

            return new
            {
                status = missing.Count == 0 ? "success" : "partial",
                action = "import",
                document = doc.Name,
                source = srcDoc?.Name ?? srcFile,
                overwrite,
                imported,
                skipped_existing = skipped,
                missing,
            };
        }

        /// <summary>Lấy ObjectId của từ điển AEC (Property Set Definitions hoặc Schedule Table Styles), tạo nếu chưa có.</summary>
        private static ObjectId EnsureAecDictionaryId(Database db, bool propertySetDefs)
        {
            ObjectId id = propertySetDefs
                ? new DictionaryPropertySetDefinitions(db).DictionaryId
                : new DictionaryScheduleTableStyle(db).DictionaryId;
            if (!id.IsNull) return id;

            // Từ điển chưa tồn tại: thêm một bản ghi tạm để AEC tự tạo từ điển, sau đó xóa bản ghi tạm.
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                const string tmpName = "__MCP_TMP_INIT__";
                if (propertySetDefs)
                {
                    var dict = new DictionaryPropertySetDefinitions(db);
                    var tmp = new PropertySetDefinition();
                    tmp.SubSetDatabaseDefaults(db);
                    dict.AddNewRecord(tmpName, tmp);
                    tr.AddNewlyCreatedDBObject(tmp, true);
                    tmp.Erase();
                }
                else
                {
                    var dict = new DictionaryScheduleTableStyle(db);
                    var tmp = new ScheduleTableStyle();
                    tmp.SubSetDatabaseDefaults(db);
                    dict.AddNewRecord(tmpName, tmp);
                    tr.AddNewlyCreatedDBObject(tmp, true);
                    tmp.Erase();
                }
                tr.Commit();
            }
            id = propertySetDefs
                ? new DictionaryPropertySetDefinitions(db).DictionaryId
                : new DictionaryScheduleTableStyle(db).DictionaryId;
            if (id.IsNull) throw new InvalidOperationException("Không tạo được từ điển AEC trong bản vẽ đích.");
            return id;
        }

        // ---------- 2.5 Liệt kê kiểu bảng Schedule ----------
        private static object PsListScheduleStyles(Document doc, JsonElement root)
        {
            Database db = doc.Database;
            string nameFilter = GetStr(root, "name", "");
            var styles = new List<object>();
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var dict = new DictionaryScheduleTableStyle(db);
                foreach (ObjectId id in dict.Records)
                {
                    if (!id.IsValid || id.IsErased) continue;
                    if (tr.GetObject(id, OpenMode.ForRead) is not ScheduleTableStyle st) continue;
                    if (!string.IsNullOrWhiteSpace(nameFilter) && st.Name.IndexOf(nameFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;

                    var cols = new List<object>();
                    int n = SafeGet(() => st.Columns.Count, 0);
                    for (int i = 0; i < n; i++)
                    {
                        ScheduleTableStyleColumn? col = SafeGet<ScheduleTableStyleColumn?>(() => st.Columns[i], null);
                        if (col == null) continue;
                        cols.Add(new Dictionary<string, object?>
                        {
                            ["heading"] = SafeGet(() => col.Heading, ""),
                            ["property_set"] = SafeGet(() => col.PropertySetDefinitionName, ""),
                            ["property"] = SafeGet(() => col.PropertyName, ""),
                            ["column_type"] = SafeGet(() => col.ColumnType.ToString(), ""),
                            ["total"] = SafeGet(() => col.Total, false),
                            ["hidden"] = SafeGet(() => col.HideColumn, false),
                            ["formula"] = SafeGet(() => col.Formula?.GetFormulaString(), null),
                            ["valid"] = SafeGet(() => col.IsPropertyValid, true),
                        });
                    }
                    styles.Add(new Dictionary<string, object?>
                    {
                        ["name"] = st.Name,
                        ["handle"] = st.Handle.ToString(),
                        ["title"] = SafeGet(() => st.Title, ""),
                        ["applies_to_all"] = SafeGet(() => st.AppliesToAll, false),
                        ["applies_to"] = SafeGet(() => st.AppliesToFilter.Cast<string>().ToList(), new List<string>()),
                        ["sorting_count"] = SafeGet(() => st.Sortings.Count, 0),
                        ["columns"] = cols,
                    });
                }
                tr.Commit();
            }
            return new { status = "success", action = "list_schedule_styles", document = doc.Name, count = styles.Count, styles };
        }

        // ---------- 2.6 Liệt kê bảng Schedule trong bản vẽ ----------
        private static object PsListScheduleTables(Document doc, JsonElement root)
        {
            Database db = doc.Database;
            bool includeCells = GetBool(root, "include_cells", true);
            int maxRows = Math.Clamp(GetInt(root, "max_rows", 50), 0, 1000);
            var handleFilter = new HashSet<string>(ReadStringList(root, "handles").Select(h => h.Trim().ToUpperInvariant()));
            var tables = new List<object>();
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                foreach (ObjectId id in EnumerateScheduleTables(tr, db))
                {
                    if (handleFilter.Count > 0 && !handleFilter.Contains(id.Handle.ToString().ToUpperInvariant())) continue;
                    var st = (ScheduleTable)tr.GetObject(id, OpenMode.ForRead);
                    tables.Add(DescribeScheduleTable(tr, st, includeCells, maxRows));
                }
                tr.Commit();
            }
            return new { status = "success", action = "list_schedule_tables", document = doc.Name, count = tables.Count, tables };
        }

        // ---------- 2.7 Chèn bảng Schedule ----------
        private static object PsInsertScheduleTable(Document doc, JsonElement root)
        {
            Database db = doc.Database;
            string styleName = GetStr(root, "style", GetStr(root, "schedule_style", ""));
            if (string.IsNullOrWhiteSpace(styleName)) throw new ArgumentException("Thiếu 'style' (tên kiểu bảng Schedule).");
            Point3d ins = GetPt3d(root, "insert_point", Point3d.Origin);
            string title = GetStr(root, "title", "");
            double scale = GetDbl(root, "scale", 0);
            string layer = GetStr(root, "layer", "");
            bool auto = GetBool(root, "automatic_update", true);
            bool addNew = GetBool(root, "add_new_automatically", false);
            bool includeCells = GetBool(root, "include_cells", true);
            List<string> notFound;
            object desc;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var sdict = new DictionaryScheduleTableStyle(db);
                if (!sdict.Has(styleName, tr))
                    throw new ArgumentException($"Bản vẽ không có kiểu bảng Schedule '{styleName}'. Dùng action 'import' để chép từ bản vẽ mẫu trước.");
                ObjectId styleId = sdict.GetAt(styleName);

                List<ObjectId> ids = PsResolveTargets(tr, db, root, out notFound);
                if (ids.Count == 0)
                    throw new ArgumentException("Không có đối tượng đưa vào bảng: cần 'handles' hoặc bộ lọc 'entity_type'/'layer'/'property_set'.");

                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                var st = new ScheduleTable();
                st.SetDatabaseDefaults(db);
                try { st.SetToStandard(db); } catch { }
                st.StyleId = styleId;
                st.Location = ins;
                if (!string.IsNullOrWhiteSpace(title)) st.Title = title;
                if (scale > 0) st.Scale = scale;
                if (!string.IsNullOrWhiteSpace(layer))
                {
                    var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    if (!lt.Has(layer)) { lt.UpgradeOpen(); EnsureLayerInTr(tr, db, lt, layer, 7); }
                    st.Layer = layer;
                }
                ms.AppendEntity(st);
                tr.AddNewlyCreatedDBObject(st, true);

                var sel = new ObjectIdCollection();
                foreach (ObjectId id in ids) sel.Add(id);
                st.SetSelectionSet(sel, new ObjectIdCollection());
                st.AutomaticUpdate = auto;
                st.AddNewEntriesAutomatically = addNew;
                st.RegenerateTable(true);

                desc = DescribeScheduleTable(tr, st, includeCells, 200);
                tr.Commit();
            }
            return new { status = "success", action = "insert_schedule_table", document = doc.Name, not_found_handles = notFound, table = desc };
        }

        // ---------- 2.8 Cập nhật bảng Schedule ----------
        private static object PsUpdateScheduleTables(Document doc, JsonElement root)
        {
            Database db = doc.Database;
            var handles = ReadStringList(root, "handles");
            bool includeCells = GetBool(root, "include_cells", false);
            var errors = new List<string>();
            var tables = new List<object>();
            int updated;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                HashSet<string>? filter = handles.Count > 0
                    ? new HashSet<string>(handles.Select(h => h.Trim().ToUpperInvariant()))
                    : null;
                updated = RegenerateAllScheduleTables(tr, db, filter, errors);
                foreach (ObjectId id in EnumerateScheduleTables(tr, db))
                {
                    if (filter != null && !filter.Contains(id.Handle.ToString().ToUpperInvariant())) continue;
                    var st = (ScheduleTable)tr.GetObject(id, OpenMode.ForRead);
                    tables.Add(DescribeScheduleTable(tr, st, includeCells, 200));
                }
                tr.Commit();
            }
            return new { status = errors.Count == 0 ? "success" : "partial", action = "update_schedule_tables", document = doc.Name, updated, errors, tables };
        }

        // ---------- Tiện ích Schedule ----------
        private static IEnumerable<ObjectId> EnumerateScheduleTables(Transaction tr, Database db)
        {
            RXClass rx = RXObject.GetClass(typeof(ScheduleTable));
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            foreach (ObjectId btrId in bt)
            {
                var btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
                if (!btr.IsLayout) continue;
                foreach (ObjectId id in btr)
                {
                    if (!id.IsErased && id.ObjectClass.IsDerivedFrom(rx)) yield return id;
                }
            }
        }

        private static int RegenerateAllScheduleTables(Transaction tr, Database db, HashSet<string>? handleFilter, List<string> errors)
        {
            int count = 0;
            foreach (ObjectId id in EnumerateScheduleTables(tr, db).ToList())
            {
                if (handleFilter != null && !handleFilter.Contains(id.Handle.ToString().ToUpperInvariant())) continue;
                try
                {
                    var st = (ScheduleTable)tr.GetObject(id, OpenMode.ForWrite);
                    st.RegenerateTable(true);
                    count++;
                }
                catch (System.Exception ex)
                {
                    errors.Add($"Schedule {id.Handle}: {ex.Message}");
                }
            }
            return count;
        }

        private static Dictionary<string, object?> DescribeScheduleTable(Transaction tr, ScheduleTable st, bool includeCells, int maxRows)
        {
            string styleName = SafeGet(() => (tr.GetObject(st.StyleId, OpenMode.ForRead) as ScheduleTableStyle)?.Name ?? "", "");
            int rows = SafeGet(() => st.RowCount, 0);
            int cols = SafeGet(() => st.ColumnCount, 0);
            Extents3d? ext = SafeGet<Extents3d?>(() => st.GeometricExtents, null);
            var d = new Dictionary<string, object?>
            {
                ["handle"] = st.Handle.ToString(),
                ["style"] = styleName,
                ["title"] = SafeGet(() => st.Title, ""),
                ["layer"] = st.Layer,
                ["rows"] = rows,
                ["columns"] = cols,
                ["automatic_update"] = SafeGet(() => st.AutomaticUpdate, false),
                ["add_new_automatically"] = SafeGet(() => st.AddNewEntriesAutomatically, false),
                ["out_of_date"] = SafeGet(() => st.IsOutOfDate, false),
                ["selection_count"] = SafeGet(() => st.SelectionSet.Count, 0),
                ["location"] = SafeGet(() => new[] { R4(st.Location.X), R4(st.Location.Y) }, Array.Empty<double>()),
                ["bounding_box"] = ext.HasValue
                    ? new[] { R4(ext.Value.MinPoint.X), R4(ext.Value.MinPoint.Y), R4(ext.Value.MaxPoint.X), R4(ext.Value.MaxPoint.Y) }
                    : null,
            };
            if (includeCells)
            {
                var cells = new List<List<string>>();
                for (int r = 0; r < Math.Min(rows, maxRows); r++)
                {
                    var line = new List<string>();
                    for (int c = 0; c < cols; c++) line.Add(SafeGet(() => st.CellText(r, c) ?? "", ""));
                    cells.Add(line);
                }
                d["cells"] = cells;
                d["cells_truncated"] = rows > maxRows;
            }
            return d;
        }

        // ---------- Tiện ích chọn đối tượng đích ----------
        /// <summary>
        /// Xác định danh sách đối tượng đích: theo 'handles' (ưu tiên) hoặc quét ModelSpace với bộ lọc
        /// 'entity_type' (DXF name / tên lớp, hỗ trợ *), 'layer' (hỗ trợ *), 'property_set' (đã gắn PS này).
        /// </summary>
        private static List<ObjectId> PsResolveTargets(Transaction tr, Database db, JsonElement root, out List<string> notFound, bool defaultAll = false)
        {
            notFound = new List<string>();
            var result = new List<ObjectId>();
            List<string> handles = ReadStringList(root, "handles");
            if (handles.Count > 0)
            {
                foreach (string h in handles)
                {
                    if (TryGetIdFromHandle(db, h, out ObjectId id) && !id.IsErased) result.Add(id);
                    else notFound.Add(h);
                }
                return result;
            }

            string typeFilter = GetStr(root, "entity_type", "");
            string layerFilter = GetStr(root, "layer", "");
            string psFilter = GetStr(root, "filter_property_set", "");
            bool hasFilter = defaultAll || !string.IsNullOrWhiteSpace(typeFilter) || !string.IsNullOrWhiteSpace(layerFilter) ||
                             !string.IsNullOrWhiteSpace(psFilter) || GetBool(root, "all_modelspace", false);
            if (!hasFilter) return result;

            Regex? typeRx = string.IsNullOrWhiteSpace(typeFilter) ? null : WildcardToRegex(typeFilter);
            Regex? layerRx = string.IsNullOrWhiteSpace(layerFilter) ? null : WildcardToRegex(layerFilter);
            ObjectId psDefId = ObjectId.Null;
            if (!string.IsNullOrWhiteSpace(psFilter))
            {
                var dict = new DictionaryPropertySetDefinitions(db);
                if (!dict.Has(psFilter, tr)) return result;
                psDefId = dict.GetAt(psFilter);
            }

            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            foreach (ObjectId id in ms)
            {
                if (id.IsErased) continue;
                if (typeRx != null)
                {
                    string dxf = id.ObjectClass.DxfName ?? "";
                    string cls = id.ObjectClass.Name ?? "";
                    if (!typeRx.IsMatch(dxf) && !typeRx.IsMatch(cls)) continue;
                }
                if (layerRx != null || !psDefId.IsNull)
                {
                    if (tr.GetObject(id, OpenMode.ForRead) is not Entity ent) continue;
                    if (layerRx != null && !layerRx.IsMatch(ent.Layer ?? "")) continue;
                    if (!psDefId.IsNull && SafeGet(() => PropertyDataServices.GetPropertySet(ent, psDefId), ObjectId.Null).IsNull) continue;
                }
                result.Add(id);
            }
            return result;
        }

        private static bool TryGetIdFromHandle(Database db, string handleHex, out ObjectId id)
        {
            id = ObjectId.Null;
            try
            {
                long v = Convert.ToInt64(handleHex.Trim(), 16);
                return db.TryGetObjectId(new Handle(v), out id) && id.IsValid;
            }
            catch { return false; }
        }

        private static Regex WildcardToRegex(string pattern)
        {
            string rx = "^" + Regex.Escape(pattern.Trim()).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            return new Regex(rx, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        private static PropertySetDefinition? GetCachedDef(Transaction tr, Dictionary<ObjectId, PropertySetDefinition?> cache, ObjectId defId)
        {
            if (!cache.TryGetValue(defId, out PropertySetDefinition? def))
            {
                def = SafeGet(() => tr.GetObject(defId, OpenMode.ForRead) as PropertySetDefinition, null);
                cache[defId] = def;
            }
            return def;
        }

        // ---------- Tiện ích chuyển đổi dữ liệu ----------
        private static object ConvertJsonToAec(JsonElement v, AecDataType dataType)
        {
            string s = v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "") : v.GetRawText();
            switch (dataType)
            {
                case AecDataType.Real:
                    if (v.ValueKind == JsonValueKind.Number) return v.GetDouble();
                    return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double d) ? d : 0.0;
                case AecDataType.Integer:
                case AecDataType.AutoIncrement:
                    if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int iv)) return iv;
                    return int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out int i2) ? i2 : 0;
                case AecDataType.TrueFalse:
                    if (v.ValueKind == JsonValueKind.True) return true;
                    if (v.ValueKind == JsonValueKind.False) return false;
                    return s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "1";
                default:
                    return s;
            }
        }

        private static object? JsonSafe(object? v)
        {
            switch (v)
            {
                case null: return null;
                case double d: return double.IsNaN(d) || double.IsInfinity(d) ? null : Math.Round(d, 6);
                case float f: return float.IsNaN(f) || float.IsInfinity(f) ? null : Math.Round(f, 6);
                case int or long or short or bool or string: return v;
                case ObjectId oid: return oid.IsNull ? null : oid.Handle.ToString();
                default: return v.ToString();
            }
        }

        private static Dictionary<string, string> DescribeObject(object o)
        {
            var d = new Dictionary<string, string>();
            if (o == null) return d;
            foreach (var p in o.GetType().GetProperties())
            {
                if (!p.CanRead || p.GetIndexParameters().Length > 0) continue;
                d[p.Name] = SafeGet(() => p.GetValue(o)?.ToString() ?? "", "");
            }
            return d;
        }

        private static Dictionary<string, JsonElement> ReadValueMap(JsonElement root, string prop)
        {
            var m = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            if (root.TryGetProperty(prop, out JsonElement el) && el.ValueKind == JsonValueKind.Object)
                foreach (JsonProperty p in el.EnumerateObject()) m[p.Name] = p.Value.Clone();
            return m;
        }

        private static List<string> ReadStringList(JsonElement root, string prop)
        {
            var list = new List<string>();
            if (root.TryGetProperty(prop, out JsonElement el) && el.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement e in el.EnumerateArray())
                {
                    if (e.ValueKind == JsonValueKind.String) list.Add(e.GetString() ?? "");
                    else if (e.ValueKind != JsonValueKind.Null) list.Add(e.GetRawText());
                }
            }
            return list;
        }

        private static List<List<string>> ReadStringMatrix(JsonElement root, string prop)
        {
            var rows = new List<List<string>>();
            if (root.TryGetProperty(prop, out JsonElement el) && el.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement r in el.EnumerateArray())
                {
                    var row = new List<string>();
                    if (r.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement c in r.EnumerateArray())
                        {
                            if (c.ValueKind == JsonValueKind.String) row.Add(c.GetString() ?? "");
                            else if (c.ValueKind == JsonValueKind.Null) row.Add("");
                            else row.Add(c.GetRawText());
                        }
                    }
                    rows.Add(row);
                }
            }
            return rows;
        }

        private static List<double> ReadDoubleList(JsonElement root, string prop)
        {
            var list = new List<double>();
            if (root.TryGetProperty(prop, out JsonElement el) && el.ValueKind == JsonValueKind.Array)
                foreach (JsonElement e in el.EnumerateArray())
                    if (e.ValueKind == JsonValueKind.Number && e.TryGetDouble(out double d)) list.Add(d);
            return list;
        }

        private static double R4(double v) => double.IsNaN(v) || double.IsInfinity(v) ? 0.0 : Math.Round(v, 4);

        /// <summary>Chuyển tiếp các action Property Set / Schedule Table gọi từ endpoint /civil.</summary>
        private static object ExecutePropertySetFromCivil(Document doc, JsonElement root, string civilAction)
        {
            string psAction = civilAction switch
            {
                "list_property_sets" => "list_definitions",
                "get_property_sets" => "get_values",
                "attach_property_set" => "attach",
                "set_property_values" => "set_values",
                "import_property_sets" => "import",
                _ => civilAction,
            };
            return psAction switch
            {
                "list_definitions" => PsListDefinitions(doc, root),
                "get_values" => PsGetValues(doc, root),
                "attach" or "set_values" => PsAttachAndSet(doc, root),
                "import" => PsImport(doc, root),
                "list_schedule_styles" => PsListScheduleStyles(doc, root),
                "list_schedule_tables" => PsListScheduleTables(doc, root),
                "insert_schedule_table" => PsInsertScheduleTable(doc, root),
                "update_schedule_tables" => PsUpdateScheduleTables(doc, root),
                _ => throw new ArgumentException($"Action không hợp lệ: {civilAction}"),
            };
        }

        /// <summary>Phân loại các đối tượng còn lại trên ModelSpace (Schedule Table, 3D Polyline, bảng Civil 3D...).</summary>
        private static void ClassifyOtherEntityInModelSpace(Transaction tr, Entity ent, int col, List<object> entities, Action<string> addCount)
        {
            if (ent is ScheduleTable st)
            {
                string styleName = SafeGet(() => (tr.GetObject(st.StyleId, OpenMode.ForRead) as ScheduleTableStyle)?.Name ?? "", "");
                double[] bbox = SafeGet(() =>
                {
                    Extents3d ext = st.GeometricExtents;
                    return new[] { R4(ext.MinPoint.X), R4(ext.MinPoint.Y), R4(ext.MaxPoint.X), R4(ext.MaxPoint.Y) };
                }, Array.Empty<double>());
                entities.Add(new
                {
                    type = "AEC_SCHEDULE_TABLE",
                    layer = ent.Layer,
                    color = col,
                    handle = ent.Handle.ToString(),
                    style = styleName,
                    title = SafeGet(() => st.Title, ""),
                    rows = SafeGet(() => st.RowCount, 0),
                    columns = SafeGet(() => st.ColumnCount, 0),
                    bounding_box = bbox,
                });
                addCount("AEC_SCHEDULE_TABLE");
                return;
            }
            if (ent is Polyline3d p3)
            {
                var pts = new List<double[]>();
                try
                {
                    foreach (ObjectId vId in p3)
                        if (tr.GetObject(vId, OpenMode.ForRead) is PolylineVertex3d v)
                            pts.Add(new[] { R4(v.Position.X), R4(v.Position.Y) });
                }
                catch { }
                double[] bbox = SafeGet(() =>
                {
                    Extents3d ext = p3.GeometricExtents;
                    return new[] { R4(ext.MinPoint.X), R4(ext.MinPoint.Y), R4(ext.MaxPoint.X), R4(ext.MaxPoint.Y) };
                }, Array.Empty<double>());
                entities.Add(new
                {
                    type = "POLYLINE3D",
                    layer = p3.Layer,
                    color = col,
                    handle = p3.Handle.ToString(),
                    closed = p3.Closed ? 1 : 0,
                    length = SafeGet(() => R4(p3.Length), 0.0),
                    pts = pts.Take(200).ToList(),
                    bounding_box = bbox,
                });
                addCount("POLYLINE3D");
                return;
            }

            string dxf = SafeGet(() => ent.Id.ObjectClass.DxfName, "");
            if (string.IsNullOrWhiteSpace(dxf)) dxf = ent.GetType().Name.ToUpperInvariant();
            double[] obbox = SafeGet(() =>
            {
                Extents3d ext = ent.GeometricExtents;
                return new[] { R4(ext.MinPoint.X), R4(ext.MinPoint.Y), R4(ext.MaxPoint.X), R4(ext.MaxPoint.Y) };
            }, Array.Empty<double>());
            entities.Add(new
            {
                type = dxf,
                layer = ent.Layer,
                color = col,
                handle = ent.Handle.ToString(),
                name = SafeGet(() => (string?)ent.GetType().GetProperty("Name")?.GetValue(ent), null),
                bounding_box = obbox,
            });
            addCount(dxf);
        }
    }
}

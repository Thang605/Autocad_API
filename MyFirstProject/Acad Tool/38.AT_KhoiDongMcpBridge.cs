// (C) Copyright 2026 by T27
// Lệnh AT_Khoi_Dong_Mcp_Bridge & Máy chủ HTTP Bridge nội bộ giúp Antigravity MCP điều khiển AutoCAD/Civil 3D trực tiếp
// Hỗ trợ vượt rào cản quyền Admin (UAC), vẽ trực tiếp qua .NET Database Transaction và đọc/tự động hóa đối tượng Civil 3D
//
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using MyFirstProject.Acad_Tool;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using ATable = Autodesk.AutoCAD.DatabaseServices.Table;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using CivilTinSurface = Autodesk.Civil.DatabaseServices.TinSurface;
using CivilGridSurface = Autodesk.Civil.DatabaseServices.GridSurface;
using CivilVolumeSurface = Autodesk.Civil.DatabaseServices.TinVolumeSurface;
using CivilParcel = Autodesk.Civil.DatabaseServices.Parcel;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;

[assembly: CommandClass(typeof(Civil3DCsharp.AT_McpBridgeServer))]

namespace Civil3DCsharp
{
    public class AT_McpBridgeServer
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessageW(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        private const uint WM_NULL = 0x0000;

        private const string StopActionDomainKey = "Civil3DTools.McpBridge.StopAction";
        private static HttpListener? _listener;
        private static CancellationTokenSource? _cts;
        private static Control? _uiMarshalControl;
        private static int _activePort = 18888;

        public static bool IsRunning => _listener != null && _listener.IsListening;
        public static int ActivePort => _activePort;

        [CommandMethod("AT_Khoi_Dong_Mcp_Bridge")]
        public void AT_Khoi_Dong_Mcp_Bridge()
        {
            Document? doc = Application.DocumentManager.MdiActiveDocument;
            Editor? ed = doc?.Editor;

            EnsureStarted(KhoiDongMcpBridgeForm.SavedPort);

            try
            {
                using (var form = new KhoiDongMcpBridgeForm())
                {
                    Application.ShowModalDialog(form);
                }
            }
            catch (System.Exception ex)
            {
                ed?.WriteMessage($"\n❌ [Lỗi AT_Khoi_Dong_Mcp_Bridge]: {ex.Message}");
            }
        }

        [LispFunction("AT_MCP_START")]
        public static ResultBuffer? LispStartMcpBridge(ResultBuffer? args)
        {
            EnsureStarted(KhoiDongMcpBridgeForm.SavedPort);
            return null;
        }

        [LispFunction("fboundp")]
        public static ResultBuffer? LispFboundpCompat(ResultBuffer? args)
        {
            EnsureStarted(KhoiDongMcpBridgeForm.SavedPort);
            return null;
        }

        public static void EnsureStarted(int port = 18888)
        {
            try
            {
                AppDomain domain = AppDomain.CurrentDomain;
                lock (domain)
                {
                    if (domain.GetData(StopActionDomainKey) is Action oldStop)
                    {
                        try { oldStop(); } catch { }
                        domain.SetData(StopActionDomainKey, null);
                    }

                    StopBridge();

                    // Khởi tạo Control trên Main UI Thread của AutoCAD để marshal mọi lệnh từ HTTP Thread về Main Thread
                    if (_uiMarshalControl == null || _uiMarshalControl.IsDisposed)
                    {
                        _uiMarshalControl = new Control();
                        IntPtr _ = _uiMarshalControl.Handle;
                    }

                    _activePort = port;
                    _cts = new CancellationTokenSource();
                    _listener = new HttpListener();
                    _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                    _listener.Start();

                    domain.SetData(StopActionDomainKey, new Action(StopBridge));

                    CancellationToken token = _cts.Token;
                    Task.Run(() => ListenLoopAsync(_listener, token), token);
                }
            }
            catch
            {
                // Không làm gián đoạn quá trình khởi tạo AutoCAD nếu cổng đang bận
            }
        }

        public static void StopBridge()
        {
            try
            {
                _cts?.Cancel();
                if (_listener != null)
                {
                    if (_listener.IsListening) _listener.Stop();
                    _listener.Close();
                }
            }
            catch { }
            finally
            {
                _listener = null;
                _cts = null;
            }
        }

        /// <summary>
        /// Thực thi một hàm đồng bộ ngay trên Main UI Thread của AutoCAD với DocumentLock an toàn.
        /// </summary>
        private static T RunOnAutoCadMainThread<T>(Func<Document, T> func)
        {
            if (_uiMarshalControl == null || !_uiMarshalControl.IsHandleCreated)
            {
                throw new InvalidOperationException("UI Marshal Control chưa được khởi tạo trên Main Thread.");
            }

            T result = default!;
            System.Exception? capturedEx = null;

            _uiMarshalControl.Invoke(new Action(() =>
            {
                try
                {
                    Document? doc = Application.DocumentManager.MdiActiveDocument;
                    if (doc == null)
                        throw new InvalidOperationException("Không có bản vẽ (MdiActiveDocument) nào đang mở trong AutoCAD.");

                    using (doc.LockDocument())
                    {
                        result = func(doc);
                    }
                }
                catch (System.Exception ex)
                {
                    capturedEx = ex;
                }
            }));

            if (capturedEx != null)
                throw capturedEx;

            return result;
        }

        private static async Task ListenLoopAsync(HttpListener listener, CancellationToken token)
        {
            while (!token.IsCancellationRequested && listener.IsListening)
            {
                try
                {
                    HttpListenerContext ctx = await listener.GetContextAsync().ConfigureAwait(false);
                    _ = Task.Run(() => HandleRequestAsync(ctx), token);
                }
                catch
                {
                    if (token.IsCancellationRequested || !listener.IsListening)
                        break;
                }
            }
        }

        private static async Task HandleRequestAsync(HttpListenerContext ctx)
        {
            string path = ctx.Request.Url?.AbsolutePath?.ToLowerInvariant() ?? "/";
            try
            {
                if (path == "/status")
                {
                    var info = RunOnAutoCadMainThread(doc =>
                    {
                        Database db = doc.Database;
                        int entityCount = 0;
                        var layers = new List<string>();
                        object? civilSummary = null;

                        using (Transaction tr = db.TransactionManager.StartTransaction())
                        {
                            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                            foreach (ObjectId lid in lt)
                            {
                                if (tr.GetObject(lid, OpenMode.ForRead) is LayerTableRecord ltr)
                                {
                                    layers.Add(ltr.Name);
                                }
                            }

                            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                            foreach (ObjectId _ in ms)
                            {
                                entityCount++;
                            }

                            if (KhoiDongMcpBridgeForm.SavedIncludeCivilInStatus)
                            {
                                civilSummary = BuildCivil3DSummarySafe(db, tr);
                            }

                            tr.Commit();
                        }

                        var openDocs = new List<object>();
                        try
                        {
                            foreach (Document d in Application.DocumentManager)
                            {
                                try
                                {
                                    Database ddb = d.Database;
                                    CivilDocument cdoc = CivilDocument.GetCivilDocument(ddb);
                                    openDocs.Add(new
                                    {
                                        name = Path.GetFileName(d.Name),
                                        full_path = ddb.Filename,
                                        is_active = (d == doc),
                                        alignment_count = cdoc.GetAlignmentIds().Count,
                                        surface_count = cdoc.GetSurfaceIds().Count,
                                        corridor_count = cdoc.CorridorCollection.Count
                                    });
                                }
                                catch { }
                            }
                        }
                        catch { }

                        return new
                        {
                            status = "connected",
                            service = "autocad-mcp-csharp-bridge",
                            bridge_version = "3.1-civil3d-multidoc",
                            pid = Environment.ProcessId,
                            port = _activePort,
                            active_document = doc.Name,
                            full_path = doc.Database.Filename,
                            modelspace_entity_count = entityCount,
                            open_documents = openDocs,
                            civil3d_summary = civilSummary,
                            layers = layers
                        };
                    });

                    await WriteJsonResponseAsync(ctx.Response, 200, info).ConfigureAwait(false);
                    return;
                }

                if (path == "/civil")
                {
                    string body = ctx.Request.HttpMethod == "POST"
                        ? await ReadBodyAsync(ctx.Request).ConfigureAwait(false)
                        : "{}";
                    string queryAction = ctx.Request.QueryString["action"] ?? "";
                    string queryTarget = ctx.Request.QueryString["target"] ?? "";
                    var civilRes = RunOnAutoCadMainThread(doc => ExecuteCivilRequest(doc, body, queryAction, queryTarget));
                    await WriteJsonResponseAsync(ctx.Response, 200, civilRes).ConfigureAwait(false);
                    return;
                }

                if (path == "/clear" && ctx.Request.HttpMethod == "POST")
                {
                    int deleted = RunOnAutoCadMainThread(doc =>
                    {
                        Database db = doc.Database;
                        int count = 0;
                        using (Transaction tr = db.TransactionManager.StartTransaction())
                        {
                            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                            foreach (ObjectId id in ms)
                            {
                                if (tr.GetObject(id, OpenMode.ForWrite, false, true) is Entity ent)
                                {
                                    ent.Erase();
                                    count++;
                                }
                            }
                            tr.Commit();
                        }
                        doc.Editor.Regen();
                        return count;
                    });

                    await WriteJsonResponseAsync(ctx.Response, 200, new { status = "success", deleted_count = deleted }).ConfigureAwait(false);
                    return;
                }

                if (path == "/draw" && ctx.Request.HttpMethod == "POST")
                {
                    string body = await ReadBodyAsync(ctx.Request).ConfigureAwait(false);
                    var drawRes = RunOnAutoCadMainThread(doc => ExecuteDrawBatch(doc, body));
                    await WriteJsonResponseAsync(ctx.Response, 200, drawRes).ConfigureAwait(false);
                    return;
                }

                if (path == "/inspect")
                {
                    var inspectRes = RunOnAutoCadMainThread(doc => InspectModelSpace(doc));
                    await WriteJsonResponseAsync(ctx.Response, 200, inspectRes).ConfigureAwait(false);
                    return;
                }

                if (path == "/lisp" && ctx.Request.HttpMethod == "POST")
                {
                    string body = await ReadBodyAsync(ctx.Request).ConfigureAwait(false);
                    string cmdToSend = "";
                    using (JsonDocument jdoc = JsonDocument.Parse(body))
                    {
                        if (jdoc.RootElement.TryGetProperty("command", out JsonElement cmdEl))
                        {
                            cmdToSend = cmdEl.GetString() ?? "";
                        }
                    }

                    bool sent = RunOnAutoCadMainThread(doc =>
                    {
                        if (string.IsNullOrWhiteSpace(cmdToSend)) return false;
                        if (!cmdToSend.EndsWith("\n")) cmdToSend += "\n";
                        doc.SendStringToExecute(cmdToSend, true, false, false);
                        try
                        {
                            PostMessageW(Application.MainWindow.Handle, WM_NULL, IntPtr.Zero, IntPtr.Zero);
                        }
                        catch { }
                        return true;
                    });

                    await WriteJsonResponseAsync(ctx.Response, 200, new { status = "ok", dispatched = sent }).ConfigureAwait(false);
                    return;
                }

                if (path == "/manage" && ctx.Request.HttpMethod == "POST")
                {
                    string body = await ReadBodyAsync(ctx.Request).ConfigureAwait(false);
                    var manageRes = RunOnAutoCadMainThread<object>(doc =>
                    {
                        using JsonDocument jdoc = JsonDocument.Parse(body);
                        string act = GetStr(jdoc.RootElement, "action", "").ToLowerInvariant();
                        string fpath = GetStr(jdoc.RootElement, "file_path", "");

                        if (act.StartsWith("civil"))
                        {
                            string subAction = act.Contains(':') ? act.Substring(act.IndexOf(':') + 1) : act.Replace("civil_", "").Replace("civil", "");
                            if (string.IsNullOrWhiteSpace(subAction)) subAction = "inspect";
                            string extraJson = (!string.IsNullOrWhiteSpace(fpath) && fpath.TrimStart().StartsWith("{")) ? fpath : body;
                            return ExecuteCivilRequest(doc, extraJson, subAction, fpath);
                        }

                        if (act == "zoom_extents")
                        {
                            doc.SendStringToExecute("._ZOOM _Extents\n", true, false, false);
                            try { PostMessageW(Application.MainWindow.Handle, WM_NULL, IntPtr.Zero, IntPtr.Zero); } catch { }
                            return new { status = "success", action = "zoom_extents" };
                        }
                        else if (act == "save")
                        {
                            if (!string.IsNullOrWhiteSpace(fpath))
                            {
                                string abs = Path.GetFullPath(fpath);
                                Directory.CreateDirectory(Path.GetDirectoryName(abs) ?? "");
                                doc.Database.SaveAs(abs, DwgVersion.Current);
                                return new { status = "success", action = "save_as", saved_path = abs };
                            }
                            else
                            {
                                doc.SendStringToExecute("._QSAVE\n", true, false, false);
                                return new { status = "success", action = "save", document = doc.Name };
                            }
                        }
                        return new { status = "error", message = $"Action không hỗ trợ qua HTTP: {act}" };
                    });

                    await WriteJsonResponseAsync(ctx.Response, 200, manageRes).ConfigureAwait(false);
                    return;
                }

                if (path == "/reload" && ctx.Request.HttpMethod == "POST")
                {
                    string body = await ReadBodyAsync(ctx.Request).ConfigureAwait(false);
                    string dllPath = "";
                    using (JsonDocument jdoc = JsonDocument.Parse(body))
                    {
                        dllPath = GetStr(jdoc.RootElement, "dll_path", "").Trim().Trim('\uFEFF');
                    }

                    if (string.IsNullOrWhiteSpace(dllPath) || !File.Exists(dllPath))
                    {
                        await WriteJsonResponseAsync(ctx.Response, 400, new { status = "error", message = $"Không tìm thấy DLL: {dllPath}" }).ConfigureAwait(false);
                        return;
                    }

                    await WriteJsonResponseAsync(ctx.Response, 200, new { status = "success", loaded_dll = dllPath }).ConfigureAwait(false);

                    RunOnAutoCadMainThread<object>(doc =>
                    {
                        try { Application.SetSystemVariable("SECURELOAD", (short)0); } catch { }
                        var asm = ExtensionLoader.Load(dllPath);
                        try
                        {
                            asm?.GetType("Civil3DCsharp.AT_McpBridgeServer")
                                ?.GetMethod("EnsureStarted")
                                ?.Invoke(null, new object[] { _activePort });
                        }
                        catch { }
                        return true;
                    });
                    return;
                }

                await WriteJsonResponseAsync(ctx.Response, 404, new { status = "not_found" }).ConfigureAwait(false);
            }
            catch (System.Exception ex)
            {
                try
                {
                    await WriteJsonResponseAsync(ctx.Response, 500, new { status = "error", message = ex.Message }).ConfigureAwait(false);
                }
                catch { }
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // MODULE MỞ RỘNG DỮ LIỆU & TỰ ĐỘNG HÓA CIVIL 3D (MCP BRIDGE v3.0)
        // ═══════════════════════════════════════════════════════════════════════════

        public static string GetQuickCivilSummaryText()
        {
            try
            {
                Document? doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return "Chưa mở bản vẽ.";
                Database db = doc.Database;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    CivilDocument cdoc = CivilDocument.GetCivilDocument(db);
                    int aligns = cdoc.GetAlignmentIds().Count;
                    int surfs = cdoc.GetSurfaceIds().Count;
                    int corrs = cdoc.CorridorCollection.Count;
                    int sites = cdoc.GetSiteIds().Count;
                    int nets = cdoc.GetPipeNetworkIds().Count;
                    int cogoCount = 0;
                    try { cogoCount = (int)cdoc.CogoPoints.Count; } catch { }

                    int profiles = 0, slGroups = 0, sampleLines = 0, sectionViews = 0;
                    foreach (ObjectId aId in cdoc.GetAlignmentIds())
                    {
                        if (tr.GetObject(aId, OpenMode.ForWrite) is Alignment al)
                        {
                            profiles += al.GetProfileIds().Count;
                            foreach (ObjectId gId in al.GetSampleLineGroupIds())
                            {
                                slGroups++;
                                if (tr.GetObject(gId, OpenMode.ForWrite) is SampleLineGroup grp)
                                {
                                    sampleLines += grp.GetSampleLineIds().Count;
                                    foreach (SectionViewGroup svg in grp.SectionViewGroups)
                                    {
                                        sectionViews += svg.GetSectionViewIds().Count;
                                    }
                                }
                            }
                        }
                    }

                    int parcels = 0;
                    foreach (ObjectId sId in cdoc.GetSiteIds())
                    {
                        if (tr.GetObject(sId, OpenMode.ForWrite) is Site site)
                        {
                            parcels += site.GetParcelIds().Count;
                        }
                    }

                    tr.Commit();
                    return $"Tuyến (Alignment): {aligns} | Trắc dọc (Profile): {profiles} | Bề mặt (Surface): {surfs} | Corridor: {corrs}\n"
                         + $"Nhóm cọc (SLGroup): {slGroups} ({sampleLines} cọc) | Trắc ngang (SectionView): {sectionViews} | Parcel: {parcels} (Site: {sites}) | Mạng cống: {nets} | CogoPoint: {cogoCount}";
                }
            }
            catch (System.Exception ex)
            {
                return $"Không đọc được dữ liệu Civil 3D: {ex.Message}";
            }
        }

        private static object? BuildCivil3DSummarySafe(Database db, Transaction tr)
        {
            try
            {
                CivilDocument cdoc = CivilDocument.GetCivilDocument(db);
                var alignmentNames = new List<string>();
                var surfaceNames = new List<string>();
                var corridorNames = new List<string>();
                var pipeNetworkNames = new List<string>();

                int profileCount = 0;
                int profileViewCount = 0;
                int sampleLineGroupCount = 0;
                int sampleLineCount = 0;
                int sectionViewGroupCount = 0;
                int sectionViewCount = 0;

                foreach (ObjectId aId in cdoc.GetAlignmentIds())
                {
                    if (aId.IsNull || aId.IsErased) continue;
                    if (tr.GetObject(aId, OpenMode.ForWrite) is Alignment al)
                    {
                        alignmentNames.Add(al.Name);
                        profileCount += al.GetProfileIds().Count;
                        profileViewCount += al.GetProfileViewIds().Count;

                        foreach (ObjectId gId in al.GetSampleLineGroupIds())
                        {
                            if (gId.IsNull || gId.IsErased) continue;
                            sampleLineGroupCount++;
                            if (tr.GetObject(gId, OpenMode.ForWrite) is SampleLineGroup grp)
                            {
                                sampleLineCount += grp.GetSampleLineIds().Count;
                                sectionViewGroupCount += grp.SectionViewGroups.Count;
                                foreach (SectionViewGroup svg in grp.SectionViewGroups)
                                {
                                    sectionViewCount += svg.GetSectionViewIds().Count;
                                }
                            }
                        }
                    }
                }

                foreach (ObjectId sId in cdoc.GetSurfaceIds())
                {
                    if (sId.IsNull || sId.IsErased) continue;
                    if (tr.GetObject(sId, OpenMode.ForWrite) is CivSurface surf)
                    {
                        surfaceNames.Add(surf.Name);
                    }
                }

                foreach (ObjectId cId in cdoc.CorridorCollection)
                {
                    if (cId.IsNull || cId.IsErased) continue;
                    if (tr.GetObject(cId, OpenMode.ForWrite) is Corridor corr)
                    {
                        corridorNames.Add(corr.Name);
                    }
                }

                int siteCount = 0;
                int parcelCount = 0;
                foreach (ObjectId siteId in cdoc.GetSiteIds())
                {
                    if (siteId.IsNull || siteId.IsErased) continue;
                    siteCount++;
                    if (tr.GetObject(siteId, OpenMode.ForWrite) is Site site)
                    {
                        parcelCount += site.GetParcelIds().Count;
                    }
                }

                int pipeCount = 0;
                int structureCount = 0;
                foreach (ObjectId netId in cdoc.GetPipeNetworkIds())
                {
                    if (netId.IsNull || netId.IsErased) continue;
                    if (tr.GetObject(netId, OpenMode.ForWrite) is Network net)
                    {
                        pipeNetworkNames.Add(net.Name);
                        pipeCount += net.GetPipeIds().Count;
                        structureCount += net.GetStructureIds().Count;
                    }
                }

                int cogoPointCount = 0;
                try { cogoPointCount = (int)cdoc.CogoPoints.Count; } catch { }

                int assemblyCount = 0;
                try { assemblyCount = cdoc.AssemblyCollection.Count; } catch { }

                return new
                {
                    alignment_count = alignmentNames.Count,
                    alignments = alignmentNames,
                    profile_count = profileCount,
                    profile_view_count = profileViewCount,
                    surface_count = surfaceNames.Count,
                    surfaces = surfaceNames,
                    corridor_count = corridorNames.Count,
                    corridors = corridorNames,
                    sample_line_group_count = sampleLineGroupCount,
                    sample_line_count = sampleLineCount,
                    section_view_group_count = sectionViewGroupCount,
                    section_view_count = sectionViewCount,
                    site_count = siteCount,
                    parcel_count = parcelCount,
                    pipe_network_count = pipeNetworkNames.Count,
                    pipe_networks = pipeNetworkNames,
                    pipe_count = pipeCount,
                    structure_count = structureCount,
                    cogo_point_count = cogoPointCount,
                    assembly_count = assemblyCount
                };
            }
            catch (System.Exception ex)
            {
                return new { error = ex.Message };
            }
        }

        private static Document ResolveCivilTargetDocument(Document activeDoc, JsonElement root)
        {
            string docFilter = GetStr(root, "document_name", "").Trim();
            if (!string.IsNullOrWhiteSpace(docFilter))
            {
                foreach (Document d in Application.DocumentManager)
                {
                    if (d.Name.Contains(docFilter, StringComparison.OrdinalIgnoreCase) ||
                        d.Database.Filename.Contains(docFilter, StringComparison.OrdinalIgnoreCase))
                    {
                        return d;
                    }
                }
            }

            try
            {
                CivilDocument activeCdoc = CivilDocument.GetCivilDocument(activeDoc.Database);
                int activeCivilCount = activeCdoc.GetAlignmentIds().Count
                                     + activeCdoc.GetSurfaceIds().Count
                                     + activeCdoc.CorridorCollection.Count
                                     + activeCdoc.GetPipeNetworkIds().Count;
                if (activeCivilCount == 0)
                {
                    foreach (Document d in Application.DocumentManager)
                    {
                        if (d == activeDoc) continue;
                        try
                        {
                            CivilDocument cdoc = CivilDocument.GetCivilDocument(d.Database);
                            int cnt = cdoc.GetAlignmentIds().Count + cdoc.GetSurfaceIds().Count + cdoc.CorridorCollection.Count;
                            if (cnt > 0) return d;
                        }
                        catch { }
                    }
                }
            }
            catch { }

            return activeDoc;
        }

        private static object ExecuteCivilRequest(Document activeDoc, string bodyJson, string queryAction, string queryTarget)
        {
            using JsonDocument jdoc = JsonDocument.Parse(string.IsNullOrWhiteSpace(bodyJson) ? "{}" : bodyJson);
            JsonElement root = jdoc.RootElement;

            Document doc = ResolveCivilTargetDocument(activeDoc, root);
            bool needExtraLock = (doc != activeDoc);
            DocumentLock? extraLock = needExtraLock ? doc.LockDocument() : null;
            try
            {
                Database db = doc.Database;
                string action = GetStr(root, "action", queryAction).Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(action)) action = "inspect";

                string targetName = GetStr(root, "target_name", GetStr(root, "alignment_name", queryTarget)).Trim();
                int maxItems = Math.Max(10, GetInt(root, "max_items", 500));

                switch (action)
                {
                    case "inspect":
                    case "summary":
                    case "all":
                    case "inspect_all":
                        return InspectAllCivil3DData(db, root, targetName, maxItems);

                    case "stake_report":
                    case "alignment_report":
                    case "export_stakes":
                    case "stations":
                        return GenerateAlignmentAndStakeReport(doc, root, targetName);

                    case "check_surfaces":
                    case "rebuild":
                    case "rebuild_all":
                        return CheckAndRebuildCivilObjects(doc, root, targetName);

                    case "query_elevation":
                    case "surface_elevation":
                    case "query_surface_elevation":
                        return QuerySurfaceAndProfileElevations(db, root, targetName);

                    case "adjust_profile":
                    case "update_profile":
                    case "dieu_chinh_trac_doc":
                        return AdjustProfileElevations(doc, root, targetName);

                    case "adjust_sample_lines":
                    case "update_sample_lines":
                    case "dieu_chinh_be_rong_coc":
                        return AdjustSampleLineWidths(doc, root, targetName);

                    default:
                        return InspectAllCivil3DData(db, root, targetName, maxItems);
                }
            }
            finally
            {
                extraLock?.Dispose();
            }
        }

        /// <summary>
        /// 1. Đọc & liệt kê chi tiết toàn bộ đối tượng Civil 3D trong bản vẽ hiện hành.
        /// </summary>
        private static object InspectAllCivil3DData(Database db, JsonElement root, string filterName, int maxItems)
        {
            using Transaction tr = db.TransactionManager.StartTransaction();
            CivilDocument cdoc = CivilDocument.GetCivilDocument(db);

            var alignments = new List<object>();
            var surfaces = new List<object>();
            var corridors = new List<object>();
            var sampleLineGroups = new List<object>();
            var parcels = new List<object>();
            var pipeNetworks = new List<object>();
            var cogoPointsSample = new List<object>();
            var assemblies = new List<object>();

            // 1. Alignments + Profiles + ProfileViews + SampleLineGroups
            foreach (ObjectId aId in cdoc.GetAlignmentIds())
            {
                if (aId.IsNull || aId.IsErased) continue;
                if (tr.GetObject(aId, OpenMode.ForWrite) is not Alignment al) continue;
                if (!string.IsNullOrWhiteSpace(filterName) && !al.Name.Contains(filterName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var profList = new List<object>();
                foreach (ObjectId pId in al.GetProfileIds())
                {
                    if (pId.IsNull || pId.IsErased) continue;
                    if (tr.GetObject(pId, OpenMode.ForWrite) is Profile prof)
                    {
                        var pvis = new List<object>();
                        try
                        {
                            int pviLimit = Math.Min(prof.PVIs.Count, 100);
                            for (int i = 0; i < pviLimit; i++)
                            {
                                ProfilePVI pvi = prof.PVIs[i];
                                double gIn = 0, gOut = 0;
                                try { gIn = Math.Round(pvi.GradeIn * 100.0, 3); } catch { }
                                try { gOut = Math.Round(pvi.GradeOut * 100.0, 3); } catch { }
                                pvis.Add(new
                                {
                                    index = i,
                                    station = Math.Round(pvi.RawStation, 3),
                                    elevation = Math.Round(pvi.Elevation, 3),
                                    grade_in_pct = gIn,
                                    grade_out_pct = gOut
                                });
                            }
                        }
                        catch { }

                        double minElev = 0, maxElev = 0, startSta = 0, endSta = 0;
                        try { minElev = Math.Round(prof.ElevationMin, 3); } catch { }
                        try { maxElev = Math.Round(prof.ElevationMax, 3); } catch { }
                        try { startSta = Math.Round(prof.StartingStation, 3); } catch { }
                        try { endSta = Math.Round(prof.EndingStation, 3); } catch { }

                        profList.Add(new
                        {
                            name = prof.Name,
                            handle = prof.Handle.ToString(),
                            profile_type = prof.ProfileType.ToString(),
                            style = SafeGet(() => prof.StyleName, ""),
                            layer = SafeGet(() => prof.Layer, ""),
                            starting_station = startSta,
                            ending_station = endSta,
                            elevation_min = minElev,
                            elevation_max = maxElev,
                            pvi_count = SafeGet(() => prof.PVIs.Count, 0),
                            pvis = pvis
                        });
                    }
                }

                var pvList = new List<object>();
                foreach (ObjectId pvId in al.GetProfileViewIds())
                {
                    if (pvId.IsNull || pvId.IsErased) continue;
                    if (tr.GetObject(pvId, OpenMode.ForWrite) is ProfileView pv)
                    {
                        pvList.Add(new
                        {
                            name = pv.Name,
                            handle = pv.Handle.ToString(),
                            layer = SafeGet(() => pv.Layer, ""),
                            station_start = SafeGet(() => Math.Round(pv.StationStart, 3), 0.0),
                            station_end = SafeGet(() => Math.Round(pv.StationEnd, 3), 0.0),
                            elevation_min = SafeGet(() => Math.Round(pv.ElevationMin, 3), 0.0),
                            elevation_max = SafeGet(() => Math.Round(pv.ElevationMax, 3), 0.0),
                            location = new[] { Math.Round(pv.Location.X, 3), Math.Round(pv.Location.Y, 3) }
                        });
                    }
                }

                var slgNames = new List<string>();
                foreach (ObjectId gId in al.GetSampleLineGroupIds())
                {
                    if (gId.IsNull || gId.IsErased) continue;
                    if (tr.GetObject(gId, OpenMode.ForWrite) is SampleLineGroup grp)
                    {
                        slgNames.Add(grp.Name);

                        var svgList = new List<object>();
                        foreach (SectionViewGroup svg in grp.SectionViewGroups)
                        {
                            svgList.Add(new
                            {
                                name = svg.Name,
                                section_view_count = svg.GetSectionViewIds().Count
                            });
                        }

                        var slItems = new List<object>();
                        ObjectIdCollection slIds = grp.GetSampleLineIds();
                        int slLimit = Math.Min(slIds.Count, maxItems);
                        for (int i = 0; i < slLimit; i++)
                        {
                            if (tr.GetObject(slIds[i], OpenMode.ForWrite) is SampleLine sl)
                            {
                                double cx = 0, cy = 0;
                                try { al.PointLocation(sl.Station, 0, ref cx, ref cy); } catch { }
                                GetSampleLineOffsets(sl, al, out double leftW, out double rightW);
                                slItems.Add(new
                                {
                                    name = sl.Name,
                                    number = sl.Number,
                                    station = Math.Round(sl.Station, 3),
                                    easting = Math.Round(cx, 3),
                                    northing = Math.Round(cy, 3),
                                    left_width = Math.Round(leftW, 2),
                                    right_width = Math.Round(rightW, 2)
                                });
                            }
                        }

                        sampleLineGroups.Add(new
                        {
                            alignment_name = al.Name,
                            group_name = grp.Name,
                            handle = grp.Handle.ToString(),
                            sample_line_count = slIds.Count,
                            section_view_groups = svgList,
                            sample_lines = slItems
                        });
                    }
                }

                double startX = 0, startY = 0, endX = 0, endY = 0;
                try { al.PointLocation(al.StartingStation, 0, ref startX, ref startY); } catch { }
                try { al.PointLocation(al.EndingStation, 0, ref endX, ref endY); } catch { }

                alignments.Add(new
                {
                    name = al.Name,
                    handle = al.Handle.ToString(),
                    alignment_type = al.AlignmentType.ToString(),
                    layer = SafeGet(() => al.Layer, ""),
                    style = SafeGet(() => al.StyleName, ""),
                    site_name = SafeGet(() => al.SiteName, ""),
                    starting_station = Math.Round(al.StartingStation, 3),
                    ending_station = Math.Round(al.EndingStation, 3),
                    length = Math.Round(al.Length, 3),
                    start_point = new[] { Math.Round(startX, 3), Math.Round(startY, 3) },
                    end_point = new[] { Math.Round(endX, 3), Math.Round(endY, 3) },
                    profile_count = profList.Count,
                    profiles = profList,
                    profile_view_count = pvList.Count,
                    profile_views = pvList,
                    sample_line_groups = slgNames
                });
            }

            // 2. Surfaces (TIN, Grid, Volume)
            foreach (ObjectId sId in cdoc.GetSurfaceIds())
            {
                if (sId.IsNull || sId.IsErased) continue;
                if (tr.GetObject(sId, OpenMode.ForWrite) is not CivSurface surf) continue;

                double minZ = 0, maxZ = 0, meanZ = 0, area2d = 0, area3d = 0;
                int numPts = 0, numTris = 0;
                double cutVol = 0, fillVol = 0, netVol = 0;

                try
                {
                    var gen = surf.GetGeneralProperties();
                    minZ = Math.Round(gen.MinimumElevation, 3);
                    maxZ = Math.Round(gen.MaximumElevation, 3);
                    meanZ = Math.Round(gen.MeanElevation, 3);
                    numPts = gen.NumberOfPoints;
                }
                catch { }

                if (surf is CivilTinSurface tinSurf)
                {
                    try
                    {
                        var tp = tinSurf.GetTerrainProperties();
                        area2d = Math.Round(tp.SurfaceArea2D, 2);
                        area3d = Math.Round(tp.SurfaceArea3D, 2);
                    }
                    catch { }
                    try
                    {
                        numTris = tinSurf.GetTinProperties().NumberOfTriangles;
                    }
                    catch { }
                }
                else if (surf is CivilGridSurface gridSurf)
                {
                    try
                    {
                        var tp = gridSurf.GetTerrainProperties();
                        area2d = Math.Round(tp.SurfaceArea2D, 2);
                        area3d = Math.Round(tp.SurfaceArea3D, 2);
                    }
                    catch { }
                }
                else if (surf is CivilVolumeSurface volSurf)
                {
                    try
                    {
                        var vp = volSurf.GetVolumeProperties();
                        cutVol = Math.Round(vp.AdjustedCutVolume, 2);
                        fillVol = Math.Round(vp.AdjustedFillVolume, 2);
                        netVol = Math.Round(vp.AdjustedNetVolume, 2);
                    }
                    catch { }
                }

                surfaces.Add(new
                {
                    name = surf.Name,
                    handle = surf.Handle.ToString(),
                    type = surf.GetType().Name,
                    layer = SafeGet(() => surf.Layer, ""),
                    style = SafeGet(() => surf.StyleName, ""),
                    is_out_of_date = SafeGet(() => surf.IsOutOfDate, false),
                    min_elevation = minZ,
                    max_elevation = maxZ,
                    mean_elevation = meanZ,
                    number_of_points = numPts,
                    number_of_triangles = numTris,
                    area_2d_m2 = area2d,
                    area_3d_m2 = area3d,
                    volume_cut_m3 = cutVol,
                    volume_fill_m3 = fillVol,
                    volume_net_m3 = netVol
                });
            }

            // 3. Corridors
            foreach (ObjectId cId in cdoc.CorridorCollection)
            {
                if (cId.IsNull || cId.IsErased) continue;
                if (tr.GetObject(cId, OpenMode.ForWrite) is not Corridor corr) continue;

                var baselines = new List<object>();
                try
                {
                    foreach (Baseline bl in corr.Baselines)
                    {
                        string alignName = "";
                        string profName = "";
                        try
                        {
                            if (!bl.AlignmentId.IsNull && tr.GetObject(bl.AlignmentId, OpenMode.ForWrite) is Alignment blAl)
                                alignName = blAl.Name;
                        }
                        catch { }
                        try
                        {
                            if (!bl.ProfileId.IsNull && tr.GetObject(bl.ProfileId, OpenMode.ForWrite) is Profile blPr)
                                profName = blPr.Name;
                        }
                        catch { }

                        var regions = new List<object>();
                        foreach (BaselineRegion reg in bl.BaselineRegions)
                        {
                            string asmName = "";
                            try
                            {
                                if (!reg.AssemblyId.IsNull && tr.GetObject(reg.AssemblyId, OpenMode.ForWrite) is Assembly asm)
                                    asmName = asm.Name;
                            }
                            catch { }

                            regions.Add(new
                            {
                                name = reg.Name,
                                assembly_name = asmName,
                                start_station = Math.Round(reg.StartStation, 3),
                                end_station = Math.Round(reg.EndStation, 3)
                            });
                        }

                        baselines.Add(new
                        {
                            name = bl.Name,
                            alignment_name = alignName,
                            profile_name = profName,
                            start_station = SafeGet(() => Math.Round(bl.StartStation, 3), 0.0),
                            end_station = SafeGet(() => Math.Round(bl.EndStation, 3), 0.0),
                            region_count = regions.Count,
                            regions = regions
                        });
                    }
                }
                catch { }

                corridors.Add(new
                {
                    name = corr.Name,
                    handle = corr.Handle.ToString(),
                    layer = SafeGet(() => corr.Layer, ""),
                    is_out_of_date = SafeGet(() => corr.IsOutOfDate, false),
                    baseline_count = baselines.Count,
                    baselines = baselines
                });
            }

            // 4. Parcels (grouped by Site)
            foreach (ObjectId siteId in cdoc.GetSiteIds())
            {
                if (siteId.IsNull || siteId.IsErased) continue;
                if (tr.GetObject(siteId, OpenMode.ForWrite) is not Site site) continue;

                foreach (ObjectId pId in site.GetParcelIds())
                {
                    if (pId.IsNull || pId.IsErased) continue;
                    if (tr.GetObject(pId, OpenMode.ForWrite) is CivilParcel parcel)
                    {
                        parcels.Add(new
                        {
                            site_name = site.Name,
                            name = parcel.Name,
                            number = SafeGet(() => (int)parcel.Number, 0),
                            handle = parcel.Handle.ToString(),
                            layer = SafeGet(() => parcel.Layer, ""),
                            style = SafeGet(() => parcel.StyleName, ""),
                            area_m2 = SafeGet(() => Math.Round(parcel.Area, 2), 0.0),
                            description = SafeGet(() => parcel.Description, "")
                        });
                    }
                }
            }

            // 5. Pipe Networks
            foreach (ObjectId netId in cdoc.GetPipeNetworkIds())
            {
                if (netId.IsNull || netId.IsErased) continue;
                if (tr.GetObject(netId, OpenMode.ForWrite) is not Network net) continue;

                var pipes = new List<object>();
                foreach (ObjectId pipeId in net.GetPipeIds())
                {
                    if (pipeId.IsNull || pipeId.IsErased) continue;
                    if (tr.GetObject(pipeId, OpenMode.ForWrite) is Pipe pipe)
                    {
                        pipes.Add(new
                        {
                            name = pipe.Name,
                            handle = pipe.Handle.ToString(),
                            inner_diameter_mm = SafeGet(() => Math.Round(pipe.InnerDiameterOrWidth * 1000.0, 1), 0.0),
                            length_2d = SafeGet(() => Math.Round(pipe.Length2DCenterToCenter, 3), 0.0),
                            length_3d = SafeGet(() => Math.Round(pipe.Length3DCenterToCenter, 3), 0.0),
                            slope_pct = SafeGet(() => Math.Round(pipe.Slope * 100.0, 3), 0.0),
                            start_point = new[] { Math.Round(pipe.StartPoint.X, 3), Math.Round(pipe.StartPoint.Y, 3), Math.Round(pipe.StartPoint.Z, 3) },
                            end_point = new[] { Math.Round(pipe.EndPoint.X, 3), Math.Round(pipe.EndPoint.Y, 3), Math.Round(pipe.EndPoint.Z, 3) }
                        });
                    }
                }

                var structures = new List<object>();
                foreach (ObjectId stId in net.GetStructureIds())
                {
                    if (stId.IsNull || stId.IsErased) continue;
                    if (tr.GetObject(stId, OpenMode.ForWrite) is Structure st)
                    {
                        structures.Add(new
                        {
                            name = st.Name,
                            handle = st.Handle.ToString(),
                            location = new[] { Math.Round(st.Location.X, 3), Math.Round(st.Location.Y, 3), Math.Round(st.Location.Z, 3) },
                            rim_elevation = SafeGet(() => Math.Round(st.RimElevation, 3), 0.0),
                            sump_elevation = SafeGet(() => Math.Round(st.SumpElevation, 3), 0.0)
                        });
                    }
                }

                pipeNetworks.Add(new
                {
                    name = net.Name,
                    handle = net.Handle.ToString(),
                    pipe_count = pipes.Count,
                    structure_count = structures.Count,
                    pipes = pipes,
                    structures = structures
                });
            }

            // 6. CogoPoints (Lấy mẫu tối đa 100 điểm để tránh quá tải JSON)
            int totalCogoPoints = 0;
            try
            {
                totalCogoPoints = (int)cdoc.CogoPoints.Count;
                int cogoIdx = 0;
                foreach (ObjectId ptId in cdoc.CogoPoints)
                {
                    if (cogoIdx >= Math.Min(maxItems, 100)) break;
                    if (ptId.IsNull || ptId.IsErased) continue;
                    if (tr.GetObject(ptId, OpenMode.ForWrite) is CogoPoint cp)
                    {
                        cogoPointsSample.Add(new
                        {
                            point_number = SafeGet(() => (int)cp.PointNumber, 0),
                            point_name = SafeGet(() => cp.PointName, ""),
                            easting = SafeGet(() => Math.Round(cp.Easting, 3), 0.0),
                            northing = SafeGet(() => Math.Round(cp.Northing, 3), 0.0),
                            elevation = SafeGet(() => Math.Round(cp.Elevation, 3), 0.0),
                            description = SafeGet(() => cp.RawDescription, "")
                        });
                        cogoIdx++;
                    }
                }
            }
            catch { }

            // 7. Assemblies (Mẫu mặt cắt ngang thiết kế)
            try
            {
                foreach (ObjectId asmId in cdoc.AssemblyCollection)
                {
                    if (asmId.IsNull || asmId.IsErased) continue;
                    if (tr.GetObject(asmId, OpenMode.ForWrite) is Assembly asm)
                    {
                        assemblies.Add(new
                        {
                            name = asm.Name,
                            handle = asm.Handle.ToString(),
                            layer = SafeGet(() => asm.Layer, "")
                        });
                    }
                }
            }
            catch { }

            tr.Commit();

            return new
            {
                status = "success",
                action = "inspect_all",
                document = db.Filename,
                summary = new
                {
                    alignment_count = alignments.Count,
                    surface_count = surfaces.Count,
                    corridor_count = corridors.Count,
                    sample_line_group_count = sampleLineGroups.Count,
                    parcel_count = parcels.Count,
                    pipe_network_count = pipeNetworks.Count,
                    cogo_point_count = totalCogoPoints,
                    assembly_count = assemblies.Count
                },
                alignments = alignments,
                surfaces = surfaces,
                corridors = corridors,
                sample_line_groups = sampleLineGroups,
                parcels = parcels,
                pipe_networks = pipeNetworks,
                cogo_points_sample = cogoPointsSample,
                assemblies = assemblies
            };
        }

        /// <summary>
        /// 2. Kiểm tra thông số tuyến & Xuất thống kê tọa độ/cao độ cọc (ra JSON, file CSV hoặc vẽ bảng AutoCAD Table).
        /// </summary>
        private static object GenerateAlignmentAndStakeReport(Document doc, JsonElement root, string targetAlignmentName)
        {
            Database db = doc.Database;
            string groupName = GetStr(root, "group_name", "").Trim();
            string surfaceName = GetStr(root, "surface_name", "").Trim();
            string profileName = GetStr(root, "profile_name", "").Trim();
            double interval = GetDbl(root, "interval", 0.0);
            bool drawAcadTable = GetBool(root, "draw_acad_table", false);
            string exportCsvPath = GetStr(root, "export_csv_path", "").Trim();
            Point3d insertPt = GetPt3d(root, "insert_point", Point3d.Origin);

            using Transaction tr = db.TransactionManager.StartTransaction();
            CivilDocument cdoc = CivilDocument.GetCivilDocument(db);

            Alignment? selectedAlign = FindAlignment(cdoc, tr, targetAlignmentName, requireSampleLines: string.IsNullOrWhiteSpace(targetAlignmentName));
            if (selectedAlign == null)
            {
                selectedAlign = FindAlignment(cdoc, tr, targetAlignmentName, requireSampleLines: false);
            }
            if (selectedAlign == null)
            {
                tr.Commit();
                return new { status = "error", message = "Không tìm thấy tuyến Alignment nào trong bản vẽ." };
            }

            // Tìm bề mặt tự nhiên (EG Surface)
            CivSurface? egSurface = FindSurface(cdoc, tr, surfaceName);

            // Tìm Profile tự nhiên (EG) và Profile thiết kế (FG)
            Profile? egProfile = null;
            Profile? fgProfile = null;
            foreach (ObjectId pId in selectedAlign.GetProfileIds())
            {
                if (tr.GetObject(pId, OpenMode.ForWrite) is Profile pr)
                {
                    if (!string.IsNullOrWhiteSpace(profileName) && pr.Name.Equals(profileName, StringComparison.OrdinalIgnoreCase))
                    {
                        fgProfile = pr;
                    }
                    else if (pr.ProfileType == ProfileType.FG && fgProfile == null)
                    {
                        fgProfile = pr;
                    }
                    else if (pr.ProfileType == ProfileType.EG && egProfile == null)
                    {
                        egProfile = pr;
                    }
                }
            }
            if (fgProfile == null && selectedAlign.GetProfileIds().Count > 0)
            {
                fgProfile = tr.GetObject(selectedAlign.GetProfileIds()[0], OpenMode.ForWrite) as Profile;
            }

            // Tìm SampleLineGroup
            SampleLineGroup? selectedGroup = null;
            foreach (ObjectId gId in selectedAlign.GetSampleLineGroupIds())
            {
                if (tr.GetObject(gId, OpenMode.ForWrite) is SampleLineGroup grp)
                {
                    if (string.IsNullOrWhiteSpace(groupName) || grp.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase))
                    {
                        selectedGroup = grp;
                        break;
                    }
                }
            }

            var stakeRows = new List<StakeRowItem>();

            if (selectedGroup != null && interval <= 0.001)
            {
                int idx = 1;
                foreach (ObjectId slId in selectedGroup.GetSampleLineIds())
                {
                    if (tr.GetObject(slId, OpenMode.ForWrite) is not SampleLine sl) continue;
                    double sta = sl.Station;
                    double x = 0, y = 0;
                    try { selectedAlign.PointLocation(sta, 0, ref x, ref y); } catch { }

                    GetSampleLineOffsets(sl, selectedAlign, out double leftW, out double rightW);
                    double? egZ = TryGetElevation(egProfile, egSurface, sta, x, y);
                    double? fgZ = TryGetProfileElevation(fgProfile, sta);
                    double? diff = (fgZ.HasValue && egZ.HasValue) ? Math.Round(fgZ.Value - egZ.Value, 3) : null;

                    stakeRows.Add(new StakeRowItem
                    {
                        Index = idx++,
                        StakeName = sl.Name,
                        Station = Math.Round(sta, 3),
                        StationFormatted = FormatStationKm(sta),
                        Easting = Math.Round(x, 3),
                        Northing = Math.Round(y, 3),
                        LeftWidth = Math.Round(leftW, 2),
                        RightWidth = Math.Round(rightW, 2),
                        NaturalElevation = egZ,
                        DesignElevation = fgZ,
                        CutFill = diff
                    });
                }
            }
            else
            {
                double step = interval > 0.001 ? interval : 20.0;
                int idx = 1;
                for (double sta = selectedAlign.StartingStation; sta <= selectedAlign.EndingStation + 0.0001; sta += step)
                {
                    double curSta = Math.Min(sta, selectedAlign.EndingStation);
                    double x = 0, y = 0;
                    try { selectedAlign.PointLocation(curSta, 0, ref x, ref y); } catch { }

                    double? egZ = TryGetElevation(egProfile, egSurface, curSta, x, y);
                    double? fgZ = TryGetProfileElevation(fgProfile, curSta);
                    double? diff = (fgZ.HasValue && egZ.HasValue) ? Math.Round(fgZ.Value - egZ.Value, 3) : null;

                    stakeRows.Add(new StakeRowItem
                    {
                        Index = idx,
                        StakeName = $"C{idx}",
                        Station = Math.Round(curSta, 3),
                        StationFormatted = FormatStationKm(curSta),
                        Easting = Math.Round(x, 3),
                        Northing = Math.Round(y, 3),
                        LeftWidth = 0,
                        RightWidth = 0,
                        NaturalElevation = egZ,
                        DesignElevation = fgZ,
                        CutFill = diff
                    });
                    idx++;
                    if (Math.Abs(curSta - selectedAlign.EndingStation) < 0.0001) break;
                }
            }

            // Xuất file CSV nếu được yêu cầu
            string? savedCsv = null;
            if (!string.IsNullOrWhiteSpace(exportCsvPath))
            {
                try
                {
                    string absCsv = Path.GetFullPath(exportCsvPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(absCsv) ?? "");
                    var sb = new StringBuilder();
                    sb.AppendLine("STT,TenCoc,LyTrinh,LyTrinhKm,ToaDo_X_Easting,ToaDo_Y_Northing,BeRongTrai,BeRongPhai,CaoDoTuNhien,CaoDoThietKe,DaoDap");
                    foreach (var r in stakeRows)
                    {
                        sb.AppendLine(string.Format(
                            CultureInfo.InvariantCulture,
                            "{0},\"{1}\",{2:F3},\"{3}\",{4:F3},{5:F3},{6:F2},{7:F2},{8},{9},{10}",
                            r.Index,
                            r.StakeName,
                            r.Station,
                            r.StationFormatted,
                            r.Easting,
                            r.Northing,
                            r.LeftWidth,
                            r.RightWidth,
                            r.NaturalElevation?.ToString("F3", CultureInfo.InvariantCulture) ?? "",
                            r.DesignElevation?.ToString("F3", CultureInfo.InvariantCulture) ?? "",
                            r.CutFill?.ToString("F3", CultureInfo.InvariantCulture) ?? ""
                        ));
                    }
                    File.WriteAllText(absCsv, sb.ToString(), new UTF8Encoding(true));
                    savedCsv = absCsv;
                }
                catch { }
            }

            // Vẽ bảng AutoCAD Table trực tiếp lên ModelSpace nếu draw_acad_table = true
            string? createdTableHandle = null;
            if (drawAcadTable && stakeRows.Count > 0)
            {
                createdTableHandle = DrawStakeTableInModelSpace(tr, db, selectedAlign.Name, stakeRows, insertPt);
            }

            tr.Commit();
            if (drawAcadTable)
            {
                try { doc.Editor.Regen(); } catch { }
            }

            return new
            {
                status = "success",
                action = "stake_report",
                alignment_name = selectedAlign.Name,
                starting_station = Math.Round(selectedAlign.StartingStation, 3),
                ending_station = Math.Round(selectedAlign.EndingStation, 3),
                length = Math.Round(selectedAlign.Length, 3),
                sample_line_group = selectedGroup?.Name ?? "(Theo khoảng cách)",
                eg_surface = egSurface?.Name ?? "",
                eg_profile = egProfile?.Name ?? "",
                fg_profile = fgProfile?.Name ?? "",
                stake_count = stakeRows.Count,
                exported_csv_path = savedCsv,
                acad_table_handle = createdTableHandle,
                stakes = stakeRows.Select(r => new
                {
                    stt = r.Index,
                    stake_name = r.StakeName,
                    station = r.Station,
                    station_km = r.StationFormatted,
                    easting_x = r.Easting,
                    northing_y = r.Northing,
                    left_width = r.LeftWidth,
                    right_width = r.RightWidth,
                    natural_elevation = r.NaturalElevation,
                    design_elevation = r.DesignElevation,
                    cut_fill = r.CutFill
                }).ToList()
            };
        }

        /// <summary>
        /// 3. Kiểm tra trạng thái & Tự động Rebuild Surface / Corridor.
        /// </summary>
        private static object CheckAndRebuildCivilObjects(Document doc, JsonElement root, string targetName)
        {
            Database db = doc.Database;
            bool doRebuild = GetBool(root, "rebuild", false);

            using Transaction tr = db.TransactionManager.StartTransaction();
            CivilDocument cdoc = CivilDocument.GetCivilDocument(db);

            var surfaceReports = new List<object>();
            var corridorReports = new List<object>();
            int rebuiltSurfaces = 0;
            int rebuiltCorridors = 0;

            foreach (ObjectId sId in cdoc.GetSurfaceIds())
            {
                if (sId.IsNull || sId.IsErased) continue;
                if (tr.GetObject(sId, OpenMode.ForWrite) is not CivSurface surf) continue;
                if (!string.IsNullOrWhiteSpace(targetName) && !surf.Name.Contains(targetName, StringComparison.OrdinalIgnoreCase))
                    continue;

                bool wasOutOfDate = SafeGet(() => surf.IsOutOfDate, false);
                bool rebuilt = false;
                string? rebuildErr = null;

                if (doRebuild)
                {
                    try
                    {
                        surf.Rebuild();
                        rebuilt = true;
                        rebuiltSurfaces++;
                    }
                    catch (System.Exception ex)
                    {
                        rebuildErr = ex.Message;
                    }
                }

                double minZ = 0, maxZ = 0;
                int numPts = 0;
                try
                {
                    var gp = surf.GetGeneralProperties();
                    minZ = Math.Round(gp.MinimumElevation, 3);
                    maxZ = Math.Round(gp.MaximumElevation, 3);
                    numPts = gp.NumberOfPoints;
                }
                catch { }

                surfaceReports.Add(new
                {
                    name = surf.Name,
                    type = surf.GetType().Name,
                    was_out_of_date = wasOutOfDate,
                    is_out_of_date_now = SafeGet(() => surf.IsOutOfDate, false),
                    rebuilt = rebuilt,
                    min_elevation = minZ,
                    max_elevation = maxZ,
                    number_of_points = numPts,
                    error = rebuildErr
                });
            }

            foreach (ObjectId cId in cdoc.CorridorCollection)
            {
                if (cId.IsNull || cId.IsErased) continue;
                if (tr.GetObject(cId, OpenMode.ForWrite) is not Corridor corr) continue;
                if (!string.IsNullOrWhiteSpace(targetName) && !corr.Name.Contains(targetName, StringComparison.OrdinalIgnoreCase))
                    continue;

                bool wasOutOfDate = SafeGet(() => corr.IsOutOfDate, false);
                bool rebuilt = false;
                string? rebuildErr = null;

                if (doRebuild)
                {
                    try
                    {
                        corr.Rebuild();
                        rebuilt = true;
                        rebuiltCorridors++;
                    }
                    catch (System.Exception ex)
                    {
                        rebuildErr = ex.Message;
                    }
                }

                corridorReports.Add(new
                {
                    name = corr.Name,
                    was_out_of_date = wasOutOfDate,
                    is_out_of_date_now = SafeGet(() => corr.IsOutOfDate, false),
                    rebuilt = rebuilt,
                    baseline_count = SafeGet(() => corr.Baselines.Count, 0),
                    error = rebuildErr
                });
            }

            tr.Commit();
            if (rebuiltSurfaces > 0 || rebuiltCorridors > 0)
            {
                try { doc.Editor.Regen(); } catch { }
            }

            return new
            {
                status = "success",
                action = doRebuild ? "rebuild_all" : "check_surfaces",
                rebuilt_surfaces_count = rebuiltSurfaces,
                rebuilt_corridors_count = rebuiltCorridors,
                surfaces = surfaceReports,
                corridors = corridorReports
            };
        }

        /// <summary>
        /// 4. Truy vấn cao độ Bề mặt (Surface) và Trắc dọc (Profile) theo danh sách tọa độ [X, Y] hoặc [Station, Offset].
        /// </summary>
        private static object QuerySurfaceAndProfileElevations(Database db, JsonElement root, string targetSurfaceName)
        {
            string surfaceName = GetStr(root, "surface_name", targetSurfaceName).Trim();
            string alignmentName = GetStr(root, "alignment_name", "").Trim();
            string profileName = GetStr(root, "profile_name", "").Trim();

            using Transaction tr = db.TransactionManager.StartTransaction();
            CivilDocument cdoc = CivilDocument.GetCivilDocument(db);

            CivSurface? surf = FindSurface(cdoc, tr, surfaceName);
            Alignment? align = !string.IsNullOrWhiteSpace(alignmentName)
                ? FindAlignment(cdoc, tr, alignmentName, requireSampleLines: false)
                : FindAlignment(cdoc, tr, "", requireSampleLines: false);

            Profile? fgProf = null;
            if (align != null)
            {
                foreach (ObjectId pId in align.GetProfileIds())
                {
                    if (tr.GetObject(pId, OpenMode.ForWrite) is Profile pr)
                    {
                        if (!string.IsNullOrWhiteSpace(profileName) && pr.Name.Equals(profileName, StringComparison.OrdinalIgnoreCase))
                        {
                            fgProf = pr;
                            break;
                        }
                        if (pr.ProfileType == ProfileType.FG && fgProf == null)
                            fgProf = pr;
                    }
                }
            }

            var results = new List<object>();

            // Trường hợp 1: Truyền danh sách tọa độ points: [[x, y], ...]
            if (root.TryGetProperty("points", out JsonElement ptsEl) && ptsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement pt in ptsEl.EnumerateArray())
                {
                    if (pt.ValueKind == JsonValueKind.Array)
                    {
                        var coords = new List<double>();
                        foreach (JsonElement c in pt.EnumerateArray())
                        {
                            if (c.TryGetDouble(out double d)) coords.Add(d);
                        }
                        if (coords.Count >= 2)
                        {
                            double x = coords[0], y = coords[1];
                            double? surfZ = null;
                            if (surf != null)
                            {
                                try { surfZ = Math.Round(surf.FindElevationAtXY(x, y), 3); } catch { }
                            }
                            double? sta = null, off = null, profZ = null;
                            if (align != null)
                            {
                                try
                                {
                                    double s = 0, o = 0;
                                    align.StationOffset(x, y, ref s, ref o);
                                    sta = Math.Round(s, 3);
                                    off = Math.Round(o, 3);
                                    if (fgProf != null) profZ = TryGetProfileElevation(fgProf, s);
                                }
                                catch { }
                            }

                            results.Add(new
                            {
                                x = Math.Round(x, 3),
                                y = Math.Round(y, 3),
                                surface_elevation = surfZ,
                                station = sta,
                                offset = off,
                                profile_elevation = profZ
                            });
                        }
                    }
                }
            }

            // Trường hợp 2: Truyền danh sách stations: [0, 20, 40] hoặc station_offsets: [{"station": 50, "offset": 0}]
            if (align != null && root.TryGetProperty("stations", out JsonElement stasEl) && stasEl.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement sEl in stasEl.EnumerateArray())
                {
                    double sta = 0, off = 0;
                    if (sEl.ValueKind == JsonValueKind.Number && sEl.TryGetDouble(out double sVal))
                    {
                        sta = sVal;
                    }
                    else if (sEl.ValueKind == JsonValueKind.Object)
                    {
                        sta = GetDbl(sEl, "station", 0.0);
                        off = GetDbl(sEl, "offset", 0.0);
                    }
                    else continue;

                    double x = 0, y = 0;
                    try { align.PointLocation(sta, off, ref x, ref y); } catch { }
                    double? surfZ = null;
                    if (surf != null)
                    {
                        try { surfZ = Math.Round(surf.FindElevationAtXY(x, y), 3); } catch { }
                    }
                    double? profZ = TryGetProfileElevation(fgProf, sta);

                    results.Add(new
                    {
                        station = Math.Round(sta, 3),
                        offset = Math.Round(off, 3),
                        x = Math.Round(x, 3),
                        y = Math.Round(y, 3),
                        surface_elevation = surfZ,
                        profile_elevation = profZ
                    });
                }
            }

            tr.Commit();
            return new
            {
                status = "success",
                action = "query_elevation",
                surface_name = surf?.Name ?? "",
                alignment_name = align?.Name ?? "",
                profile_name = fgProf?.Name ?? "",
                count = results.Count,
                results = results
            };
        }

        /// <summary>
        /// 5. Điều chỉnh trực tiếp đỉnh PVI trên Trắc dọc thiết kế (Profile) và tự động Rebuild Corridor.
        /// </summary>
        private static object AdjustProfileElevations(Document doc, JsonElement root, string targetAlignmentName)
        {
            Database db = doc.Database;
            string alignName = GetStr(root, "alignment_name", targetAlignmentName).Trim();
            string profName = GetStr(root, "profile_name", "").Trim();
            double deltaZ = GetDbl(root, "delta_elevation", 0.0);
            double minSta = GetDbl(root, "start_station", double.MinValue);
            double maxSta = GetDbl(root, "end_station", double.MaxValue);
            bool rebuildCorridors = GetBool(root, "rebuild_corridors", true);

            using Transaction tr = db.TransactionManager.StartTransaction();
            CivilDocument cdoc = CivilDocument.GetCivilDocument(db);

            Alignment? align = FindAlignment(cdoc, tr, alignName, requireSampleLines: false);
            if (align == null)
            {
                tr.Commit();
                return new { status = "error", message = "Không tìm thấy Alignment để điều chỉnh Profile." };
            }

            Profile? targetProfile = null;
            foreach (ObjectId pId in align.GetProfileIds())
            {
                if (tr.GetObject(pId, OpenMode.ForWrite) is Profile pr)
                {
                    if (!string.IsNullOrWhiteSpace(profName) && pr.Name.Equals(profName, StringComparison.OrdinalIgnoreCase))
                    {
                        targetProfile = pr;
                        break;
                    }
                    if (pr.ProfileType == ProfileType.FG && targetProfile == null)
                        targetProfile = pr;
                }
            }

            if (targetProfile == null)
            {
                tr.Commit();
                return new { status = "error", message = $"Không tìm thấy Profile thiết kế trên tuyến '{align.Name}'." };
            }

            int updatedCount = 0;
            int addedCount = 0;

            // 1. Cộng/trừ delta_elevation cho các PVI trong khoảng [start_station, end_station]
            if (Math.Abs(deltaZ) > 0.00001)
            {
                for (int i = 0; i < targetProfile.PVIs.Count; i++)
                {
                    ProfilePVI pvi = targetProfile.PVIs[i];
                    if (pvi.RawStation >= minSta - 0.001 && pvi.RawStation <= maxSta + 0.001)
                    {
                        pvi.Elevation += deltaZ;
                        updatedCount++;
                    }
                }
            }

            // 2. Cập nhật cụ thể từng PVI theo pvi_updates: [{"index": 1, "station": 100, "elevation": 12.5}]
            if (root.TryGetProperty("pvi_updates", out JsonElement updEl) && updEl.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in updEl.EnumerateArray())
                {
                    int idx = GetInt(item, "index", -1);
                    double targetSta = GetDbl(item, "station", double.NaN);
                    double newElev = GetDbl(item, "elevation", double.NaN);
                    if (double.IsNaN(newElev)) continue;

                    ProfilePVI? matchedPvi = null;
                    if (idx >= 0 && idx < targetProfile.PVIs.Count)
                    {
                        matchedPvi = targetProfile.PVIs[idx];
                    }
                    else if (!double.IsNaN(targetSta))
                    {
                        double minDiff = double.MaxValue;
                        for (int i = 0; i < targetProfile.PVIs.Count; i++)
                        {
                            double diff = Math.Abs(targetProfile.PVIs[i].RawStation - targetSta);
                            if (diff < minDiff && diff <= 1.0)
                            {
                                minDiff = diff;
                                matchedPvi = targetProfile.PVIs[i];
                            }
                        }
                    }

                    if (matchedPvi != null)
                    {
                        matchedPvi.Elevation = newElev;
                        updatedCount++;
                    }
                }
            }

            // 3. Thêm PVI mới nếu có add_pvis: [{"station": 150.0, "elevation": 14.2}]
            if (root.TryGetProperty("add_pvis", out JsonElement addEl) && addEl.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in addEl.EnumerateArray())
                {
                    double sta = GetDbl(item, "station", double.NaN);
                    double elev = GetDbl(item, "elevation", double.NaN);
                    if (!double.IsNaN(sta) && !double.IsNaN(elev))
                    {
                        try
                        {
                            targetProfile.PVIs.AddPVI(sta, elev);
                            addedCount++;
                        }
                        catch { }
                    }
                }
            }

            int rebuiltCorridors = 0;
            if (rebuildCorridors && (updatedCount > 0 || addedCount > 0))
            {
                foreach (ObjectId cId in cdoc.CorridorCollection)
                {
                    if (tr.GetObject(cId, OpenMode.ForWrite) is Corridor corr)
                    {
                        try
                        {
                            corr.Rebuild();
                            rebuiltCorridors++;
                        }
                        catch { }
                    }
                }
            }

            var currentPvis = new List<object>();
            for (int i = 0; i < targetProfile.PVIs.Count; i++)
            {
                ProfilePVI pvi = targetProfile.PVIs[i];
                currentPvis.Add(new
                {
                    index = i,
                    station = Math.Round(pvi.RawStation, 3),
                    elevation = Math.Round(pvi.Elevation, 3)
                });
            }

            tr.Commit();
            try { doc.Editor.Regen(); } catch { }

            return new
            {
                status = "success",
                action = "adjust_profile",
                alignment_name = align.Name,
                profile_name = targetProfile.Name,
                updated_pvi_count = updatedCount,
                added_pvi_count = addedCount,
                rebuilt_corridors_count = rebuiltCorridors,
                pvis = currentPvis
            };
        }

        /// <summary>
        /// 6. Điều chỉnh trực tiếp bề rộng Trái/Phải của hệ thống cọc SampleLine trên tuyến.
        /// </summary>
        private static object AdjustSampleLineWidths(Document doc, JsonElement root, string targetAlignmentName)
        {
            Database db = doc.Database;
            string alignName = GetStr(root, "alignment_name", targetAlignmentName).Trim();
            string groupName = GetStr(root, "group_name", "").Trim();
            double leftWidth = Math.Abs(GetDbl(root, "left_width", 20.0));
            double rightWidth = Math.Abs(GetDbl(root, "right_width", 20.0));
            double minSta = GetDbl(root, "start_station", double.MinValue);
            double maxSta = GetDbl(root, "end_station", double.MaxValue);

            using Transaction tr = db.TransactionManager.StartTransaction();
            CivilDocument cdoc = CivilDocument.GetCivilDocument(db);

            Alignment? align = FindAlignment(cdoc, tr, alignName, requireSampleLines: true);
            if (align == null)
            {
                tr.Commit();
                return new { status = "error", message = "Không tìm thấy Alignment có chứa SampleLineGroup." };
            }

            int updatedSampleLines = 0;
            foreach (ObjectId gId in align.GetSampleLineGroupIds())
            {
                if (tr.GetObject(gId, OpenMode.ForWrite) is not SampleLineGroup grp) continue;
                if (!string.IsNullOrWhiteSpace(groupName) && !grp.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (ObjectId slId in grp.GetSampleLineIds())
                {
                    if (tr.GetObject(slId, OpenMode.ForWrite) is not SampleLine sl) continue;
                    if (sl.Station < minSta - 0.001 || sl.Station > maxSta + 0.001) continue;

                    try
                    {
                        double sta = sl.Station;
                        foreach (SampleLineVertex v in sl.Vertices)
                        {
                            if (v.Side == SampleLineVertexSideType.Left)
                            {
                                double ex = 0, ny = 0;
                                align.PointLocation(sta, -leftWidth, ref ex, ref ny);
                                v.Location = new Point3d(ex, ny, v.Location.Z);
                            }
                            else if (v.Side == SampleLineVertexSideType.Right)
                            {
                                double ex = 0, ny = 0;
                                align.PointLocation(sta, rightWidth, ref ex, ref ny);
                                v.Location = new Point3d(ex, ny, v.Location.Z);
                            }
                        }
                        updatedSampleLines++;
                    }
                    catch { }
                }
            }

            tr.Commit();
            try { doc.Editor.Regen(); } catch { }

            return new
            {
                status = "success",
                action = "adjust_sample_lines",
                alignment_name = align.Name,
                left_width = leftWidth,
                right_width = rightWidth,
                updated_sample_lines_count = updatedSampleLines
            };
        }

        // ─── Các hàm phụ trợ cho Civil 3D ─────────────────────────────────────────

        private class StakeRowItem
        {
            public int Index { get; set; }
            public string StakeName { get; set; } = "";
            public double Station { get; set; }
            public string StationFormatted { get; set; } = "";
            public double Easting { get; set; }
            public double Northing { get; set; }
            public double LeftWidth { get; set; }
            public double RightWidth { get; set; }
            public double? NaturalElevation { get; set; }
            public double? DesignElevation { get; set; }
            public double? CutFill { get; set; }
        }

        private static T SafeGet<T>(Func<T> getter, T fallback)
        {
            try { return getter(); }
            catch { return fallback; }
        }

        private static Alignment? FindAlignment(CivilDocument cdoc, Transaction tr, string nameFilter, bool requireSampleLines)
        {
            foreach (ObjectId aId in cdoc.GetAlignmentIds())
            {
                if (aId.IsNull || aId.IsErased) continue;
                if (tr.GetObject(aId, OpenMode.ForWrite) is Alignment al)
                {
                    if (!string.IsNullOrWhiteSpace(nameFilter))
                    {
                        if (al.Name.Equals(nameFilter, StringComparison.OrdinalIgnoreCase) ||
                            al.Name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase))
                        {
                            return al;
                        }
                    }
                    else if (!requireSampleLines || al.GetSampleLineGroupIds().Count > 0)
                    {
                        return al;
                    }
                }
            }
            return null;
        }

        private static CivSurface? FindSurface(CivilDocument cdoc, Transaction tr, string nameFilter)
        {
            foreach (ObjectId sId in cdoc.GetSurfaceIds())
            {
                if (sId.IsNull || sId.IsErased) continue;
                if (tr.GetObject(sId, OpenMode.ForWrite) is CivSurface surf)
                {
                    if (string.IsNullOrWhiteSpace(nameFilter) ||
                        surf.Name.Equals(nameFilter, StringComparison.OrdinalIgnoreCase) ||
                        surf.Name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase))
                    {
                        return surf;
                    }
                }
            }
            return null;
        }

        private static void GetSampleLineOffsets(SampleLine sl, Alignment al, out double leftWidth, out double rightWidth)
        {
            leftWidth = 0;
            rightWidth = 0;
            try
            {
                foreach (SampleLineVertex v in sl.Vertices)
                {
                    double s = 0, off = 0;
                    al.StationOffset(v.Location.X, v.Location.Y, ref s, ref off);
                    if (v.Side == SampleLineVertexSideType.Left)
                        leftWidth = Math.Max(leftWidth, Math.Abs(off));
                    else if (v.Side == SampleLineVertexSideType.Right)
                        rightWidth = Math.Max(rightWidth, Math.Abs(off));
                }
            }
            catch { }
        }

        private static double? TryGetProfileElevation(Profile? prof, double station)
        {
            if (prof == null) return null;
            try { return Math.Round(prof.ElevationAt(station), 3); }
            catch { return null; }
        }

        private static double? TryGetElevation(Profile? egProf, CivSurface? egSurf, double station, double x, double y)
        {
            if (egProf != null)
            {
                try { return Math.Round(egProf.ElevationAt(station), 3); }
                catch { }
            }
            if (egSurf != null)
            {
                try { return Math.Round(egSurf.FindElevationAtXY(x, y), 3); }
                catch { }
            }
            return null;
        }

        private static string FormatStationKm(double station)
        {
            int km = (int)Math.Floor(station / 1000.0);
            double rem = station - km * 1000.0;
            return string.Format(CultureInfo.InvariantCulture, "Km{0}+{1:000.00}", km, rem);
        }

        private static string? DrawStakeTableInModelSpace(Transaction tr, Database db, string alignName, List<StakeRowItem> rows, Point3d insertPt)
        {
            try
            {
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForWrite);
                ObjectId tahomaId = EnsureTahomaTextStyle(tr, db);

                const string layerName = "C3D_BANG_TOA_DO_COC";
                EnsureLayerInTr(tr, db, lt, layerName, 3);

                int numRows = rows.Count + 2;
                const int numCols = 8;

                var tb = new ATable();
                tb.SetDatabaseDefaults(db);
                tb.TableStyle = db.Tablestyle;
                tb.Layer = layerName;
                tb.Position = insertPt;
                tb.SetSize(numRows, numCols);
                tb.SetRowHeight(3.5);

                double[] colWidths = { 10.0, 18.0, 26.0, 28.0, 28.0, 20.0, 20.0, 18.0 };
                for (int c = 0; c < numCols; c++)
                {
                    tb.Columns[c].Width = colWidths[c];
                }

                // Dòng Tiêu đề bảng
                tb.Cells[0, 0].TextString = $"BẢNG THỐNG KÊ TỌA ĐỘ & CAO ĐỘ CỌC - TUYẾN: {alignName.ToUpperInvariant()}";
                tb.Cells[0, 0].TextStyleId = tahomaId;
                tb.Cells[0, 0].TextHeight = 2.2;
                tb.Cells[0, 0].Alignment = CellAlignment.MiddleCenter;
                tb.Rows[0].Height = 6.0;

                // Dòng Header cột
                string[] headers = { "STT", "TÊN CỌC", "LÝ TRÌNH", "X (EASTING)", "Y (NORTHING)", "CAO ĐỘ TN", "CAO ĐỘ TK", "ĐÀO/ĐẮP" };
                tb.Rows[1].Height = 4.5;
                for (int c = 0; c < numCols; c++)
                {
                    tb.Cells[1, c].TextString = headers[c];
                    tb.Cells[1, c].TextStyleId = tahomaId;
                    tb.Cells[1, c].TextHeight = 1.6;
                    tb.Cells[1, c].Alignment = CellAlignment.MiddleCenter;
                }

                // Các dòng dữ liệu cọc
                for (int i = 0; i < rows.Count; i++)
                {
                    int rIdx = i + 2;
                    var item = rows[i];
                    string[] vals =
                    {
                        item.Index.ToString(CultureInfo.InvariantCulture),
                        item.StakeName,
                        item.StationFormatted,
                        item.Easting.ToString("F3", CultureInfo.InvariantCulture),
                        item.Northing.ToString("F3", CultureInfo.InvariantCulture),
                        item.NaturalElevation?.ToString("F3", CultureInfo.InvariantCulture) ?? "-",
                        item.DesignElevation?.ToString("F3", CultureInfo.InvariantCulture) ?? "-",
                        item.CutFill?.ToString("+0.000;-0.000;0.000", CultureInfo.InvariantCulture) ?? "-"
                    };

                    for (int c = 0; c < numCols; c++)
                    {
                        tb.Cells[rIdx, c].TextString = vals[c];
                        tb.Cells[rIdx, c].TextStyleId = tahomaId;
                        tb.Cells[rIdx, c].TextHeight = 1.4;
                        tb.Cells[rIdx, c].Alignment = c <= 2 ? CellAlignment.MiddleCenter : CellAlignment.MiddleRight;
                    }
                }

                tb.GenerateLayout();
                ms.AppendEntity(tb);
                tr.AddNewlyCreatedDBObject(tb, true);
                return tb.Handle.ToString();
            }
            catch
            {
                return null;
            }
        }

        private static object ExecuteDrawBatch(Document doc, string jsonBody)
        {
            Database db = doc.Database;
            int drawnCount = 0;
            bool clearBefore = false;
            bool zoomExtents = true;

            using JsonDocument jdoc = JsonDocument.Parse(jsonBody);
            JsonElement root = jdoc.RootElement;

            if (root.TryGetProperty("clear_before_draw", out JsonElement cbEl) && cbEl.ValueKind == JsonValueKind.True)
                clearBefore = true;
            if (root.TryGetProperty("zoom_extents", out JsonElement zeEl) && zeEl.ValueKind == JsonValueKind.False)
                zoomExtents = false;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForWrite);

                if (clearBefore)
                {
                    foreach (ObjectId id in ms)
                    {
                        if (tr.GetObject(id, OpenMode.ForWrite, false, true) is Entity ent)
                            ent.Erase();
                    }
                }

                // 1. Tạo Layers nếu có
                if (root.TryGetProperty("layers", out JsonElement layersEl) && layersEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement lyEl in layersEl.EnumerateArray())
                    {
                        string lyName = GetStr(lyEl, "name", "0");
                        int lyColor = GetInt(lyEl, "color", 7);
                        EnsureLayerInTr(tr, db, lt, lyName, (short)lyColor);
                    }
                }

                // 2. Vẽ Shapes (Primitives)
                if (root.TryGetProperty("shapes", out JsonElement shapesEl) && shapesEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement item in shapesEl.EnumerateArray())
                    {
                        if (CreatePrimitiveEntity(tr, db, lt, ms, item))
                            drawnCount++;
                    }
                }

                // 3. Vẽ Annotations (Text, MText, Dimensions, Leaders, Hatches)
                if (root.TryGetProperty("annotations", out JsonElement annotsEl) && annotsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement item in annotsEl.EnumerateArray())
                    {
                        if (CreateAnnotationEntity(tr, db, lt, ms, item))
                            drawnCount++;
                    }
                }

                tr.Commit();
            }

            doc.Editor.Regen();
            if (zoomExtents)
            {
                try
                {
                    doc.SendStringToExecute("._ZOOM _Extents\n", true, false, false);
                    PostMessageW(Application.MainWindow.Handle, WM_NULL, IntPtr.Zero, IntPtr.Zero);
                }
                catch { }
            }

            return new
            {
                status = "success",
                drawn_count = drawnCount,
                engine = $"C# .NET Transaction (PID {Environment.ProcessId})"
            };
        }

        private static void EnsureLayerInTr(Transaction tr, Database db, LayerTable lt, string layerName, short colorIndex)
        {
            if (string.IsNullOrWhiteSpace(layerName) || layerName == "0") return;
            try
            {
                if (!lt.Has(layerName))
                {
                    var ltr = new LayerTableRecord
                    {
                        Name = layerName,
                        Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(ColorMethod.ByAci, Math.Max((short)1, Math.Min((short)255, colorIndex)))
                    };
                    lt.Add(ltr);
                    tr.AddNewlyCreatedDBObject(ltr, true);
                }
            }
            catch { }
        }

        private static void ApplyEntityProps(Transaction tr, Database db, LayerTable lt, Entity ent, JsonElement item)
        {
            string layer = GetStr(item, "layer", "0");
            int color = GetInt(item, "color", -1);
            EnsureLayerInTr(tr, db, lt, layer, color > 0 ? (short)color : (short)7);
            try { ent.Layer = layer; } catch { }
            if (color >= 1 && color <= 255)
            {
                ent.ColorIndex = color;
            }
        }

        private static bool CreatePrimitiveEntity(Transaction tr, Database db, LayerTable lt, BlockTableRecord ms, JsonElement item)
        {
            string type = GetStr(item, "type", "line").ToLowerInvariant();
            Entity? created = null;

            switch (type)
            {
                case "line":
                {
                    Point3d p1 = GetPt3d(item, "start", Point3d.Origin);
                    Point3d p2 = GetPt3d(item, "end", new Point3d(100, 100, 0));
                    created = new Line(p1, p2);
                    break;
                }
                case "polyline":
                case "lwpolyline":
                {
                    var pts = GetPts2d(item, "points");
                    if (pts.Count >= 2)
                    {
                        double width = GetDbl(item, "width", 0.0);
                        var pl = new Polyline(pts.Count);
                        for (int i = 0; i < pts.Count; i++)
                        {
                            pl.AddVertexAt(i, pts[i], 0.0, width, width);
                        }
                        pl.Closed = GetBool(item, "closed", false);
                        created = pl;
                    }
                    break;
                }
                case "rectangle":
                case "rect":
                {
                    Point3d c1 = GetPt3d(item, "corner1", GetPt3d(item, "start", Point3d.Origin));
                    Point3d c2 = GetPt3d(item, "corner2", GetPt3d(item, "end", new Point3d(100, 50, 0)));
                    double w = GetDbl(item, "width", 0.0);
                    var pl = new Polyline(4);
                    pl.AddVertexAt(0, new Point2d(c1.X, c1.Y), 0, w, w);
                    pl.AddVertexAt(1, new Point2d(c2.X, c1.Y), 0, w, w);
                    pl.AddVertexAt(2, new Point2d(c2.X, c2.Y), 0, w, w);
                    pl.AddVertexAt(3, new Point2d(c1.X, c2.Y), 0, w, w);
                    pl.Closed = true;
                    created = pl;
                    break;
                }
                case "polygon":
                {
                    var pts = GetPts2d(item, "points");
                    if (pts.Count == 0)
                    {
                        Point3d center = GetPt3d(item, "center", Point3d.Origin);
                        double r = GetDbl(item, "radius", 50.0);
                        int sides = Math.Max(3, GetInt(item, "sides", 6));
                        double rotDeg = GetDbl(item, "rotation", 0.0);
                        for (int i = 0; i < sides; i++)
                        {
                            double ang = (rotDeg + i * 360.0 / sides) * Math.PI / 180.0;
                            pts.Add(new Point2d(center.X + r * Math.Cos(ang), center.Y + r * Math.Sin(ang)));
                        }
                    }
                    if (pts.Count >= 3)
                    {
                        var pl = new Polyline(pts.Count);
                        for (int i = 0; i < pts.Count; i++)
                            pl.AddVertexAt(i, pts[i], 0, 0, 0);
                        pl.Closed = true;
                        created = pl;
                    }
                    break;
                }
                case "circle":
                {
                    Point3d c = GetPt3d(item, "center", Point3d.Origin);
                    double r = Math.Max(0.001, GetDbl(item, "radius", 10.0));
                    created = new Circle(c, Vector3d.ZAxis, r);
                    break;
                }
                case "arc":
                {
                    Point3d c = GetPt3d(item, "center", Point3d.Origin);
                    double r = Math.Max(0.001, GetDbl(item, "radius", 10.0));
                    double sa = GetDbl(item, "start_angle", 0.0) * Math.PI / 180.0;
                    double ea = GetDbl(item, "end_angle", 90.0) * Math.PI / 180.0;
                    created = new Arc(c, r, sa, ea);
                    break;
                }
                case "ellipse":
                {
                    Point3d c = GetPt3d(item, "center", Point3d.Origin);
                    double maj = Math.Max(0.01, GetDbl(item, "major_radius", 20.0));
                    double min = Math.Max(0.01, GetDbl(item, "minor_radius", 10.0));
                    double rot = GetDbl(item, "rotation", 0.0) * Math.PI / 180.0;
                    double ratio = Math.Max(0.01, Math.Min(1.0, min / maj));
                    var majVec = new Vector3d(maj * Math.Cos(rot), maj * Math.Sin(rot), 0);
                    created = new Ellipse(c, Vector3d.ZAxis, majVec, ratio, 0.0, 2 * Math.PI);
                    break;
                }
                case "point":
                {
                    Point3d p = GetPt3d(item, "position", GetPt3d(item, "center", Point3d.Origin));
                    created = new DBPoint(p);
                    break;
                }
            }

            if (created != null)
            {
                ApplyEntityProps(tr, db, lt, created, item);
                ms.AppendEntity(created);
                tr.AddNewlyCreatedDBObject(created, true);
                return true;
            }
            return false;
        }

        private static ObjectId EnsureTahomaTextStyle(Transaction tr, Database db)
        {
            const string styleName = "TAHOMA_MCP";
            try
            {
                var tst = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                if (tst.Has(styleName))
                {
                    ObjectId existingId = tst[styleName];
                    if (tr.GetObject(existingId, OpenMode.ForWrite) is TextStyleTableRecord existingRec)
                    {
                        existingRec.Font = new Autodesk.AutoCAD.GraphicsInterface.FontDescriptor("Tahoma", false, false, 0, 0);
                    }
                    db.Textstyle = existingId;
                    return existingId;
                }

                tst.UpgradeOpen();
                var tstr = new TextStyleTableRecord
                {
                    Name = styleName,
                    Font = new Autodesk.AutoCAD.GraphicsInterface.FontDescriptor("Tahoma", false, false, 0, 0)
                };
                ObjectId newId = tst.Add(tstr);
                tr.AddNewlyCreatedDBObject(tstr, true);
                db.Textstyle = newId;
                return newId;
            }
            catch
            {
                return db.Textstyle;
            }
        }

        private static bool CreateAnnotationEntity(Transaction tr, Database db, LayerTable lt, BlockTableRecord ms, JsonElement item)
        {
            string type = GetStr(item, "type", "text").ToLowerInvariant();
            ObjectId tahomaStyleId = EnsureTahomaTextStyle(tr, db);

            if (type == "text")
            {
                Point3d pos = GetPt3d(item, "position", Point3d.Origin);
                string txtVal = GetStr(item, "text", "");
                double height = Math.Max(0.05, GetDbl(item, "height", 2.5));
                double rot = GetDbl(item, "rotation", 0.0) * Math.PI / 180.0;
                string align = GetStr(item, "align", "left").ToLowerInvariant();

                var dbText = new DBText
                {
                    Position = pos,
                    Height = height,
                    TextString = txtVal,
                    Rotation = rot,
                    TextStyleId = tahomaStyleId
                };

                if (align == "center")
                {
                    dbText.HorizontalMode = TextHorizontalMode.TextCenter;
                    dbText.VerticalMode = TextVerticalMode.TextVerticalMid;
                    dbText.AlignmentPoint = pos;
                }
                else if (align == "right")
                {
                    dbText.HorizontalMode = TextHorizontalMode.TextRight;
                    dbText.VerticalMode = TextVerticalMode.TextVerticalMid;
                    dbText.AlignmentPoint = pos;
                }

                ApplyEntityProps(tr, db, lt, dbText, item);
                ms.AppendEntity(dbText);
                tr.AddNewlyCreatedDBObject(dbText, true);
                try { dbText.AdjustAlignment(db); } catch { }
                return true;
            }
            else if (type == "mtext")
            {
                Point3d pos = GetPt3d(item, "position", Point3d.Origin);
                string txtVal = GetStr(item, "text", "");
                double height = Math.Max(0.05, GetDbl(item, "height", 2.5));
                double width = GetDbl(item, "width", 50.0);

                var mtext = new MText
                {
                    Location = pos,
                    TextHeight = height,
                    Width = width,
                    Contents = txtVal,
                    TextStyleId = tahomaStyleId
                };

                ApplyEntityProps(tr, db, lt, mtext, item);
                ms.AppendEntity(mtext);
                tr.AddNewlyCreatedDBObject(mtext, true);
                return true;
            }
            else if (type == "dim_aligned" || type == "dim_linear" || type == "dimension")
            {
                Point3d p1 = GetPt3d(item, "point1", GetPt3d(item, "start", Point3d.Origin));
                Point3d p2 = GetPt3d(item, "point2", GetPt3d(item, "end", new Point3d(10, 0, 0)));
                Point3d dimPt = GetPt3d(item, "dim_line_point", new Point3d((p1.X + p2.X) / 2.0, Math.Min(p1.Y, p2.Y) - 1.5, 0));
                string txtOverride = GetStr(item, "text", "");
                double h = Math.Max(0.1, GetDbl(item, "height", 0.35));
                double asz = Math.Max(0.08, GetDbl(item, "arrow_size", h * 0.75));
                double rotDeg = GetDbl(item, "rotation", 0.0);

                Dimension dim;
                if (type == "dim_aligned")
                {
                    dim = new AlignedDimension(p1, p2, dimPt, txtOverride, db.Dimstyle);
                }
                else
                {
                    double rotRad = rotDeg * Math.PI / 180.0;
                    dim = new RotatedDimension(rotRad, p1, p2, dimPt, txtOverride, db.Dimstyle);
                }

                // Thiết lập thuộc tính Dimension chuẩn kỹ thuật TCVN & Font Tahoma
                dim.TextStyleId = tahomaStyleId;
                dim.Dimtxt = h;
                dim.Dimasz = asz;
                dim.Dimexe = h * 0.45;
                dim.Dimexo = h * 0.25;
                dim.Dimgap = h * 0.22;
                dim.Dimtad = 1;       // Đặt chữ phía trên đường kích thước
                dim.Dimtih = false;   // Chữ song song với đường kích thước
                dim.Dimtoh = false;
                dim.Dimtix = true;    // Luôn giữ chữ nằm giữa 2 đường gióng
                dim.Dimtofl = true;   // Luôn vẽ đường kích thước giữa 2 đường gióng
                dim.Dimdec = 2;
                dim.Dimdsep = ',';    // Dấu phẩy thập phân chuẩn Việt Nam
                dim.Dimclrd = Autodesk.AutoCAD.Colors.Color.FromColorIndex(ColorMethod.ByAci, 3); // Đường dim màu xanh lá
                dim.Dimclre = Autodesk.AutoCAD.Colors.Color.FromColorIndex(ColorMethod.ByAci, 8); // Đường gióng màu xám
                dim.Dimclrt = Autodesk.AutoCAD.Colors.Color.FromColorIndex(ColorMethod.ByAci, 2); // Chữ dim màu vàng

                ApplyEntityProps(tr, db, lt, dim, item);
                ms.AppendEntity(dim);
                tr.AddNewlyCreatedDBObject(dim, true);
                try { dim.RecomputeDimensionBlock(true); } catch { }
                return true;
            }
            else if (type == "leader")
            {
                var pts = GetPts2d(item, "points");
                string txtVal = GetStr(item, "text", "");
                double h = Math.Max(0.1, GetDbl(item, "height", 0.35));
                if (pts.Count >= 2)
                {
                    var pl = new Polyline(pts.Count);
                    for (int i = 0; i < pts.Count; i++)
                        pl.AddVertexAt(i, pts[i], 0, 0, 0);
                    ApplyEntityProps(tr, db, lt, pl, item);
                    ms.AppendEntity(pl);
                    tr.AddNewlyCreatedDBObject(pl, true);

                    if (!string.IsNullOrEmpty(txtVal))
                    {
                        Point2d last = pts[pts.Count - 1];
                        var lbl = new DBText
                        {
                            Position = new Point3d(last.X + h * 0.4, last.Y, 0),
                            Height = h,
                            TextString = txtVal,
                            TextStyleId = tahomaStyleId
                        };
                        ApplyEntityProps(tr, db, lt, lbl, item);
                        ms.AppendEntity(lbl);
                        tr.AddNewlyCreatedDBObject(lbl, true);
                    }
                    return true;
                }
            }
            else if (type == "hatch")
            {
                var pts = GetPts2d(item, "points");
                if (pts.Count >= 3)
                {
                    var pl = new Polyline(pts.Count);
                    for (int i = 0; i < pts.Count; i++)
                        pl.AddVertexAt(i, pts[i], 0, 0, 0);
                    pl.Closed = true;
                    ApplyEntityProps(tr, db, lt, pl, item);
                    ObjectId polyId = ms.AppendEntity(pl);
                    tr.AddNewlyCreatedDBObject(pl, true);

                    try
                    {
                        string pat = GetStr(item, "pattern", "ANSI31").ToUpperInvariant();
                        double scale = Math.Max(0.01, GetDbl(item, "scale", 1.0));
                        var hatch = new Hatch();
                        ApplyEntityProps(tr, db, lt, hatch, item);
                        ms.AppendEntity(hatch);
                        tr.AddNewlyCreatedDBObject(hatch, true);
                        hatch.SetHatchPattern(HatchPatternType.PreDefined, pat);
                        hatch.PatternScale = scale;
                        var ids = new ObjectIdCollection { polyId };
                        hatch.AppendLoop(HatchLoopTypes.Outermost, ids);
                        hatch.EvaluateHatch(true);
                    }
                    catch { }
                    return true;
                }
            }

            return false;
        }

        private static object InspectModelSpace(Document doc)
        {
            Database db = doc.Database;
            var entities = new List<object>();

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                var layerColors = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (ObjectId lid in lt)
                {
                    if (tr.GetObject(lid, OpenMode.ForRead) is LayerTableRecord ltr)
                    {
                        layerColors[ltr.Name] = Math.Abs(ltr.Color.ColorIndex);
                    }
                }

                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                foreach (ObjectId id in ms)
                {
                    if (tr.GetObject(id, OpenMode.ForRead) is not Entity ent) continue;

                    int col = ent.ColorIndex;
                    if (col == 256 || col <= 0)
                    {
                        col = layerColors.TryGetValue(ent.Layer, out int lc) ? lc : 7;
                    }

                    if (ent is Line ln)
                    {
                        entities.Add(new
                        {
                            type = "LINE",
                            layer = ln.Layer,
                            color = col,
                            p1 = new[] { Math.Round(ln.StartPoint.X, 4), Math.Round(ln.StartPoint.Y, 4) },
                            p2 = new[] { Math.Round(ln.EndPoint.X, 4), Math.Round(ln.EndPoint.Y, 4) }
                        });
                    }
                    else if (ent is Polyline pl)
                    {
                        var pts = new List<double[]>();
                        for (int i = 0; i < pl.NumberOfVertices; i++)
                        {
                            Point2d pt = pl.GetPoint2dAt(i);
                            pts.Add(new[] { Math.Round(pt.X, 4), Math.Round(pt.Y, 4) });
                        }
                        entities.Add(new
                        {
                            type = "LWPOLYLINE",
                            layer = pl.Layer,
                            color = col,
                            closed = pl.Closed ? 1 : 0,
                            pts = pts
                        });
                    }
                    else if (ent is Circle cir)
                    {
                        entities.Add(new
                        {
                            type = "CIRCLE",
                            layer = cir.Layer,
                            color = col,
                            c = new[] { Math.Round(cir.Center.X, 4), Math.Round(cir.Center.Y, 4) },
                            r = Math.Round(cir.Radius, 4)
                        });
                    }
                    else if (ent is Arc arc)
                    {
                        entities.Add(new
                        {
                            type = "ARC",
                            layer = arc.Layer,
                            color = col,
                            c = new[] { Math.Round(arc.Center.X, 4), Math.Round(arc.Center.Y, 4) },
                            r = Math.Round(arc.Radius, 4),
                            sa = Math.Round(arc.StartAngle, 4),
                            ea = Math.Round(arc.EndAngle, 4)
                        });
                    }
                    else if (ent is RotatedDimension rdim)
                    {
                        string dtxt = !string.IsNullOrWhiteSpace(rdim.DimensionText) && rdim.DimensionText != "<>"
                            ? rdim.DimensionText
                            : rdim.Measurement.ToString("0.##", new CultureInfo("vi-VN")) + "m";
                        entities.Add(new
                        {
                            type = "DIMENSION",
                            dim_type = "RotatedDimension",
                            layer = rdim.Layer,
                            color = col,
                            p1 = new[] { Math.Round(rdim.XLine1Point.X, 4), Math.Round(rdim.XLine1Point.Y, 4) },
                            p2 = new[] { Math.Round(rdim.XLine2Point.X, 4), Math.Round(rdim.XLine2Point.Y, 4) },
                            dim_pt = new[] { Math.Round(rdim.DimLinePoint.X, 4), Math.Round(rdim.DimLinePoint.Y, 4) },
                            h = Math.Round(rdim.Dimtxt > 0 ? rdim.Dimtxt : 0.35, 4),
                            asz = Math.Round(rdim.Dimasz > 0 ? rdim.Dimasz : 0.25, 4),
                            text = dtxt
                        });
                    }
                    else if (ent is AlignedDimension adim)
                    {
                        string dtxt = !string.IsNullOrWhiteSpace(adim.DimensionText) && adim.DimensionText != "<>"
                            ? adim.DimensionText
                            : adim.Measurement.ToString("0.##", new CultureInfo("vi-VN")) + "m";
                        entities.Add(new
                        {
                            type = "DIMENSION",
                            dim_type = "AlignedDimension",
                            layer = adim.Layer,
                            color = col,
                            p1 = new[] { Math.Round(adim.XLine1Point.X, 4), Math.Round(adim.XLine1Point.Y, 4) },
                            p2 = new[] { Math.Round(adim.XLine2Point.X, 4), Math.Round(adim.XLine2Point.Y, 4) },
                            dim_pt = new[] { Math.Round(adim.DimLinePoint.X, 4), Math.Round(adim.DimLinePoint.Y, 4) },
                            h = Math.Round(adim.Dimtxt > 0 ? adim.Dimtxt : 0.35, 4),
                            asz = Math.Round(adim.Dimasz > 0 ? adim.Dimasz : 0.25, 4),
                            text = dtxt
                        });
                    }
                    else if (ent is DBText txt)
                    {
                        Point3d p = (txt.HorizontalMode != TextHorizontalMode.TextLeft && txt.AlignmentPoint != Point3d.Origin)
                            ? txt.AlignmentPoint
                            : txt.Position;
                        int align = txt.HorizontalMode == TextHorizontalMode.TextCenter ? 1 :
                                    txt.HorizontalMode == TextHorizontalMode.TextRight ? 2 : 0;
                        entities.Add(new
                        {
                            type = "TEXT",
                            layer = txt.Layer,
                            color = col,
                            p = new[] { Math.Round(p.X, 4), Math.Round(p.Y, 4) },
                            h = Math.Round(txt.Height, 4),
                            align = align,
                            text = txt.TextString
                        });
                    }
                    else if (ent is MText mtxt)
                    {
                        entities.Add(new
                        {
                            type = "TEXT",
                            layer = mtxt.Layer,
                            color = col,
                            p = new[] { Math.Round(mtxt.Location.X, 4), Math.Round(mtxt.Location.Y, 4) },
                            h = Math.Round(mtxt.TextHeight, 4),
                            align = 0,
                            text = mtxt.Text
                        });
                    }
                }
                tr.Commit();
            }

            return new
            {
                status = "success",
                count = entities.Count,
                entities = entities
            };
        }

        private static string GetStr(JsonElement el, string prop, string defVal)
        {
            if (el.TryGetProperty(prop, out JsonElement v) && v.ValueKind == JsonValueKind.String)
                return v.GetString() ?? defVal;
            return defVal;
        }

        private static int GetInt(JsonElement el, string prop, int defVal)
        {
            if (el.TryGetProperty(prop, out JsonElement v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int res))
                return res;
            return defVal;
        }

        private static double GetDbl(JsonElement el, string prop, double defVal)
        {
            if (el.TryGetProperty(prop, out JsonElement v) && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out double res))
                return res;
            return defVal;
        }

        private static bool GetBool(JsonElement el, string prop, bool defVal)
        {
            if (el.TryGetProperty(prop, out JsonElement v))
            {
                if (v.ValueKind == JsonValueKind.True) return true;
                if (v.ValueKind == JsonValueKind.False) return false;
            }
            return defVal;
        }

        private static Point3d GetPt3d(JsonElement el, string prop, Point3d defVal)
        {
            if (el.TryGetProperty(prop, out JsonElement v) && v.ValueKind == JsonValueKind.Array)
            {
                var coords = new List<double>();
                foreach (JsonElement c in v.EnumerateArray())
                {
                    if (c.TryGetDouble(out double d)) coords.Add(d);
                }
                if (coords.Count >= 2)
                {
                    return new Point3d(coords[0], coords[1], coords.Count >= 3 ? coords[2] : 0.0);
                }
            }
            return defVal;
        }

        private static List<Point2d> GetPts2d(JsonElement el, string prop)
        {
            var res = new List<Point2d>();
            if (el.TryGetProperty(prop, out JsonElement v) && v.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement ptEl in v.EnumerateArray())
                {
                    if (ptEl.ValueKind == JsonValueKind.Array)
                    {
                        var coords = new List<double>();
                        foreach (JsonElement c in ptEl.EnumerateArray())
                        {
                            if (c.TryGetDouble(out double d)) coords.Add(d);
                        }
                        if (coords.Count >= 2)
                            res.Add(new Point2d(coords[0], coords[1]));
                    }
                }
            }
            return res;
        }

        private static async Task<string> ReadBodyAsync(HttpListenerRequest req)
        {
            using var reader = new StreamReader(req.InputStream, Encoding.UTF8);
            return await reader.ReadToEndAsync().ConfigureAwait(false);
        }

        private static async Task WriteJsonResponseAsync(HttpListenerResponse response, int statusCode, object payload)
        {
            string json = JsonSerializer.Serialize(payload);
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            response.StatusCode = statusCode;
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
            response.OutputStream.Close();
        }
    }
}

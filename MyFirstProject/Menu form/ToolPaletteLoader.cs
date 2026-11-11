using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Windows;
using System.Windows.Forms.Integration;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(MyFirstProject.ToolPaletteLoader))]

namespace MyFirstProject
{
    /// <summary>
    /// Quản lý PaletteSet chứa Tool Palette — hệ thống bảng lệnh neo (dockable) cho AutoCAD / Civil 3D.
    /// 
    /// Các lệnh chuẩn:
    ///   AT_Show_Palette   (hoặc SHOW_PALETTE)   — Mở/hiện Tool Palette
    ///   AT_Hide_Palette   (hoặc HIDE_PALETTE)   — Ẩn Tool Palette
    ///   AT_Reload_Palette (hoặc RELOAD_PALETTE) — Reload danh sách lệnh từ JSON / Excel / Mặc định
    ///   AT_Palette_Config (hoặc PALETTE_CONFIG) — Chọn file cấu hình hoặc xuất file JSON chuẩn
    /// </summary>
    public class ToolPaletteLoader
    {
        private static PaletteSet? _paletteSet;
        private static ToolPaletteControl? _control;
        private static readonly Guid PaletteGuid = new("B7E3F1A2-C4D6-4E8F-9A0B-1C2D3E4F5678");
        private static bool _idleSubscribed;

        private static readonly string SharedNetworkFolder =
            @"Z:\Z.FORM MAU LAM VIEC\1. BIM\2.MAU C3D\1.LISP\0.CIVIL TOOL\Excel file";

        private static readonly string LocalSourceJsonPath =
            @"C:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\MyFirstProject\Menu form\ToolPalette.json";

        private static string? _customFilePath;

        /// <summary>
        /// Tự động mở và nạp dữ liệu Tool Palette khi AutoCAD khởi động xong hoặc sau khi NETLOAD / RELOAD.
        /// </summary>
        public static void EnsurePaletteLoaded()
        {
            if (_idleSubscribed) return;
            _idleSubscribed = true;
            AcadApp.Idle += OnApplicationIdleLoadPalette;
        }

        private static void OnApplicationIdleLoadPalette(object? sender, EventArgs e)
        {
            AcadApp.Idle -= OnApplicationIdleLoadPalette;
            _idleSubscribed = false;

            try
            {
                if (AcadApp.DocumentManager.MdiActiveDocument != null)
                {
                    ShowPaletteInternal(silent: true);
                }
            }
            catch { }
        }

        // ═══════════════════════════════════════════════════════════════
        //  LỆNH CHÍNH (CHUẨN HÓA & TƯƠNG THÍCH)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Mở Tool Palette (Lệnh chuẩn hóa)
        /// </summary>
        [CommandMethod("AT_Show_Palette")]
        public static void AT_Show_Palette()
        {
            ShowPaletteInternal(silent: false);
        }

        /// <summary>
        /// Mở Tool Palette (Lệnh tương thích)
        /// </summary>
        [CommandMethod("SHOW_PALETTE")]
        public static void ShowPalette()
        {
            ShowPaletteInternal(silent: false);
        }

        private static void ShowPaletteInternal(bool silent)
        {
            var ed = AcadApp.DocumentManager.MdiActiveDocument?.Editor;

            try
            {
                if (_paletteSet == null)
                {
                    CreatePaletteSet();
                }

                LoadCommandsData(ed, silent);
                _paletteSet!.Visible = true;

                if (!silent)
                {
                    ed?.WriteMessage("\n✅ Tool Palette đã mở.");
                }
            }
            catch (System.Exception ex)
            {
                ed?.WriteMessage($"\n❌ Lỗi mở Palette: {ex.Message}");
            }
        }

        /// <summary>
        /// Ẩn Tool Palette (Lệnh chuẩn hóa)
        /// </summary>
        [CommandMethod("AT_Hide_Palette")]
        public static void AT_Hide_Palette()
        {
            HidePaletteInternal();
        }

        /// <summary>
        /// Ẩn Tool Palette (Lệnh tương thích)
        /// </summary>
        [CommandMethod("HIDE_PALETTE")]
        public static void HidePalette()
        {
            HidePaletteInternal();
        }

        private static void HidePaletteInternal()
        {
            var ed = AcadApp.DocumentManager.MdiActiveDocument?.Editor;

            if (_paletteSet != null)
            {
                _paletteSet.Visible = false;
                ed?.WriteMessage("\n✅ Tool Palette đã ẩn.");
            }
            else
            {
                ed?.WriteMessage("\n⚠️ Palette chưa được tạo.");
            }
        }

        /// <summary>
        /// Reload danh sách lệnh từ file cấu hình (Lệnh chuẩn hóa)
        /// </summary>
        [CommandMethod("AT_Reload_Palette")]
        public static void AT_Reload_Palette()
        {
            ReloadPaletteInternal();
        }

        /// <summary>
        /// Reload danh sách lệnh từ file cấu hình (Lệnh tương thích)
        /// </summary>
        [CommandMethod("RELOAD_PALETTE")]
        public static void ReloadPalette()
        {
            ReloadPaletteInternal();
        }

        private static void ReloadPaletteInternal()
        {
            var ed = AcadApp.DocumentManager.MdiActiveDocument?.Editor;

            if (_paletteSet == null || _control == null)
            {
                ShowPaletteInternal(silent: false);
                return;
            }

            LoadCommandsData(ed, silent: false);
            ed?.WriteMessage("\n🔄 Đã nạp lại Tool Palette.");
        }

        /// <summary>
        /// Cấu hình file nguồn cho Tool Palette (Lệnh chuẩn hóa)
        /// </summary>
        [CommandMethod("AT_Palette_Config")]
        public static void AT_Palette_Config()
        {
            PaletteConfigInternal();
        }

        /// <summary>
        /// Cấu hình file nguồn cho Tool Palette (Lệnh tương thích)
        /// </summary>
        [CommandMethod("PALETTE_CONFIG")]
        public static void PaletteConfig()
        {
            PaletteConfigInternal();
        }

        private static void PaletteConfigInternal()
        {
            var ed = AcadApp.DocumentManager.MdiActiveDocument?.Editor;
            if (ed == null) return;

            string activeFile = FindToolPaletteJson() ?? FindToolPaletteExcel() ?? "(Danh sách chuẩn tích hợp sẵn)";
            ed.WriteMessage("\n═══════════════════════════════════════════════════════════");
            ed.WriteMessage($"\n📂 Nguồn dữ liệu Palette hiện tại: {activeFile}");
            ed.WriteMessage("\n═══════════════════════════════════════════════════════════");

            var options = new PromptKeywordOptions("\nChọn hành động [Browse/Reset/Reload/ExportJson]:");
            options.Keywords.Add("Browse", "Browse", "Browse - Chọn file JSON/Excel");
            options.Keywords.Add("Reset", "Reset", "Reset - Về mặc định");
            options.Keywords.Add("Reload", "Reload", "Reload - Nạp lại lệnh");
            options.Keywords.Add("ExportJson", "ExportJson", "ExportJson - Xuất danh sách chuẩn ra ToolPalette.json");
            options.AllowNone = true;

            var result = ed.GetKeywords(options);
            if (result.Status != PromptStatus.OK) return;

            switch (result.StringResult)
            {
                case "Browse":
                    BrowseForFile(ed);
                    break;

                case "Reset":
                    _customFilePath = null;
                    ed.WriteMessage("\n✅ Đã khôi phục nguồn dữ liệu về mặc định.");
                    if (_control != null) LoadCommandsData(ed, silent: false);
                    break;

                case "Reload":
                    ReloadPaletteInternal();
                    break;

                case "ExportJson":
                    ExportDefaultJsonFiles(ed);
                    if (_control != null) LoadCommandsData(ed, silent: false);
                    break;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  TẠO PALETTE SET
        // ═══════════════════════════════════════════════════════════════

        private static void CreatePaletteSet()
        {
            _paletteSet = new PaletteSet("Civil 3D & AutoCAD Tool Palette", PaletteGuid)
            {
                Style =
                    PaletteSetStyles.ShowAutoHideButton |
                    PaletteSetStyles.ShowCloseButton |
                    PaletteSetStyles.Snappable |
                    PaletteSetStyles.UsePaletteNameAsTitleForSingle,
                MinimumSize = new System.Drawing.Size(260, 320),
                Size = new System.Drawing.Size(360, 650),
                DockEnabled = DockSides.Left | DockSides.Right
            };

            _control = new ToolPaletteControl(ExecuteCommand, ReloadPaletteInternal);

            var host = new ElementHost
            {
                Dock = System.Windows.Forms.DockStyle.Fill,
                Child = _control
            };

            _paletteSet.Add("Commands", host);
        }

        // ═══════════════════════════════════════════════════════════════
        //  NẠP DỮ LIỆU LỆNH (JSON → EXCEL → BUILT-IN DEFAULT)
        // ═══════════════════════════════════════════════════════════════

        private static void LoadCommandsData(Editor? ed, bool silent)
        {
            if (_control == null) return;

            // 1. Thử đọc từ file JSON
            string? jsonPath = FindToolPaletteJson();
            if (!string.IsNullOrEmpty(jsonPath))
            {
                try
                {
                    var commands = ToolPaletteDataReader.ReadFromJson(jsonPath);
                    if (commands.Count > 0)
                    {
                        _control.Dispatcher.Invoke(() => _control.LoadCommands(commands));
                        if (!silent)
                            ed?.WriteMessage($"\n📋 Đã nạp {commands.Count} lệnh từ {Path.GetFileName(jsonPath)} (JSON).");
                        return;
                    }
                }
                catch (System.Exception ex)
                {
                    ed?.WriteMessage($"\n⚠️ Lỗi đọc JSON ({ex.Message}), chuyển sang nguồn dự phòng...");
                }
            }

            // 2. Thử đọc từ file Excel (.xlsx)
            string? excelPath = FindToolPaletteExcel();
            if (!string.IsNullOrEmpty(excelPath))
            {
                try
                {
                    var commands = ToolPaletteDataReader.ReadFromExcel(excelPath);
                    if (commands.Count > 0)
                    {
                        _control.Dispatcher.Invoke(() => _control.LoadCommands(commands));
                        if (!silent)
                            ed?.WriteMessage($"\n📋 Đã nạp {commands.Count} lệnh từ {Path.GetFileName(excelPath)} (Excel).");
                        return;
                    }
                }
                catch (System.Exception ex)
                {
                    ed?.WriteMessage($"\n⚠️ Lỗi đọc Excel ({ex.Message}), chuyển sang danh sách chuẩn tích hợp sẵn...");
                }
            }

            // 3. Fallback: Danh sách 206 lệnh chuẩn tích hợp sẵn trong DLL
            var defaultCommands = ToolPaletteDataReader.GetDefaultCommands();
            _control.Dispatcher.Invoke(() => _control.LoadCommands(defaultCommands));
            if (!silent)
                ed?.WriteMessage($"\n📋 Đã nạp {defaultCommands.Count} lệnh từ danh sách chuẩn tích hợp sẵn.");
        }

        // ═══════════════════════════════════════════════════════════════
        //  TÌM FILE CẤU HÌNH (JSON / EXCEL)
        // ═══════════════════════════════════════════════════════════════

        private static string? FindToolPaletteJson()
        {
            // 1. Đường dẫn người dùng chọn thủ công
            if (!string.IsNullOrEmpty(_customFilePath) &&
                _customFilePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase) &&
                File.Exists(_customFilePath))
            {
                return _customFilePath;
            }

            // 2. File ToolPalette.json trong thư mục Menu form của project
            if (File.Exists(LocalSourceJsonPath))
                return LocalSourceJsonPath;

            // 3. Cùng thư mục DLL
            string dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            if (!string.IsNullOrEmpty(dllDir))
            {
                string[] localPaths =
                {
                    Path.Combine(dllDir, "ToolPalette.json"),
                    Path.Combine(dllDir, "Menu form", "ToolPalette.json"),
                    Path.Combine(dllDir, "..", "Menu form", "ToolPalette.json")
                };
                foreach (var p in localPaths)
                {
                    if (File.Exists(p)) return Path.GetFullPath(p);
                }
            }

            // 4. Thư mục mạng chia sẻ Z:\
            if (Directory.Exists(SharedNetworkFolder))
            {
                string sharedJson = Path.Combine(SharedNetworkFolder, "ToolPalette.json");
                if (File.Exists(sharedJson)) return sharedJson;
            }

            return null;
        }

        private static string? FindToolPaletteExcel()
        {
            if (!string.IsNullOrEmpty(_customFilePath) &&
                _customFilePath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) &&
                File.Exists(_customFilePath))
            {
                return _customFilePath;
            }

            if (Directory.Exists(SharedNetworkFolder))
            {
                string sharedXlsx = Path.Combine(SharedNetworkFolder, "ToolPalette.xlsx");
                if (File.Exists(sharedXlsx)) return sharedXlsx;
            }

            string dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            if (!string.IsNullOrEmpty(dllDir))
            {
                string[] localPaths =
                {
                    Path.Combine(dllDir, "ToolPalette.xlsx"),
                    Path.Combine(dllDir, "Menu form", "ToolPalette.xlsx"),
                    Path.Combine(dllDir, "..", "Menu form", "ToolPalette.xlsx")
                };
                foreach (var p in localPaths)
                {
                    if (File.Exists(p)) return Path.GetFullPath(p);
                }
            }

            return null;
        }

        private static void ExportDefaultJsonFiles(Editor? ed)
        {
            var defaults = ToolPaletteDataReader.GetDefaultCommands();

            try
            {
                ToolPaletteDataReader.WriteToJson(LocalSourceJsonPath, defaults);
                ed?.WriteMessage($"\n✅ Đã xuất {defaults.Count} lệnh ra: {LocalSourceJsonPath}");
            }
            catch (System.Exception ex)
            {
                ed?.WriteMessage($"\n⚠️ Không thể ghi file local: {ex.Message}");
            }

            try
            {
                if (Directory.Exists(SharedNetworkFolder))
                {
                    string sharedJson = Path.Combine(SharedNetworkFolder, "ToolPalette.json");
                    ToolPaletteDataReader.WriteToJson(sharedJson, defaults);
                    ed?.WriteMessage($"\n✅ Đã đồng bộ {defaults.Count} lệnh lên ổ mạng: {sharedJson}");
                }
            }
            catch (System.Exception ex)
            {
                ed?.WriteMessage($"\n⚠️ Không thể ghi file ổ mạng Z:\\: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  BROWSE / EXECUTE
        // ═══════════════════════════════════════════════════════════════

        private static void BrowseForFile(Editor ed)
        {
            var dialog = new System.Windows.Forms.OpenFileDialog
            {
                Title = "Chọn file danh sách lệnh (JSON hoặc Excel)",
                Filter = "JSON files (*.json)|*.json|Excel files (*.xlsx)|*.xlsx|All files (*.*)|*.*",
                InitialDirectory = Directory.Exists(SharedNetworkFolder) ? SharedNetworkFolder : @"C:\"
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _customFilePath = dialog.FileName;
                ed.WriteMessage($"\n✅ Đã chọn: {_customFilePath}");

                if (_control != null)
                    LoadCommandsData(ed, silent: false);
            }
        }

        /// <summary>
        /// Gửi lệnh AutoCAD khi user click vào mục lệnh trong Tool Palette
        /// </summary>
        private static void ExecuteCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return;
            string cmd = command.Trim();

            try
            {
                var doc = AcadApp.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    doc.SendStringToExecute(cmd + "\n", true, false, true);
                    return;
                }
            }
            catch { }

            try
            {
                dynamic acadApp = AcadApp.AcadApplication;
                acadApp.ActiveDocument.SendCommand(cmd + "\n");
            }
            catch { }
        }
    }
}

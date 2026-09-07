using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Exception = System.Exception;

[assembly: CommandClass(typeof(CAD_Reloader.NetReloadCommands))]

namespace CAD_Reloader
{
    public class NetReloadCommands
    {
        /// <summary>
        /// Lệnh nạp lại nhanh trực tiếp từ Command Line.
        /// </summary>
        [CommandMethod("NETRELOAD")]
        [CommandMethod("RELOAD")]
        [CommandMethod("CADRELOAD")]
        public static void FastReload()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc?.Editor;

            string targetPath = NetReloadForm.LastDllPath;

            if (!File.Exists(targetPath))
            {
                if (ed != null)
                {
                    ed.WriteMessage($"\n[NETRELOAD] Chưa tìm thấy file DLL tại: '{targetPath}'");
                    ed.WriteMessage("\n[NETRELOAD] Đang mở giao diện cấu hình...");
                }
                ShowReloadUI();
                return;
            }

            if (ed != null)
            {
                ed.WriteMessage("\n=======================================================");
                ed.WriteMessage("\n⚡ [NETRELOAD] ĐANG TẢI LẠI ASSEMBLY KHÔNG KHÓA FILE...");
                ed.WriteMessage($"\n  • Nguồn: {targetPath}");
            }

            bool ok = ReloaderEngine.ReloadAssembly(
                targetPath,
                NetReloadForm.LastCopyPdb,
                NetReloadForm.LastListCommands,
                msg => ed?.WriteMessage($"\n  {msg}")
            );

            if (ed != null)
            {
                if (ok)
                {
                    ed.WriteMessage("\n✅ TẢI LẠI THÀNH CÔNG! Code mới đã có hiệu lực.");
                }
                else
                {
                    ed.WriteMessage("\n❌ TẢI LẠI THẤT BẠI. Kiểm tra lại lỗi ở trên hoặc gõ NETRELOADUI để cấu hình.");
                }
                ed.WriteMessage("\n=======================================================\n");
            }
        }

        /// <summary>
        /// Mở giao diện Form cấu hình và nạp lại.
        /// </summary>
        [CommandMethod("NETRELOADUI")]
        [CommandMethod("RELOADUI")]
        [CommandMethod("CADRELOADUI")]
        public static void ShowReloadUI()
        {
            using (var form = new NetReloadForm())
            {
                Application.ShowModalDialog(form);
            }
        }
    }

    public static class ReloaderEngine
    {
        private static bool _resolverRegistered = false;
        private static string _currentSourceDir = string.Empty;

        public static bool ReloadAssembly(
            string sourceDllPath,
            bool copyPdb,
            bool listCommands,
            Action<string> logAction)
        {
            if (string.IsNullOrWhiteSpace(sourceDllPath) || !File.Exists(sourceDllPath))
            {
                logAction?.Invoke($"❌ Tệp không tồn tại: {sourceDllPath}");
                return false;
            }

            try
            {
                string sourceDir = Path.GetDirectoryName(sourceDllPath) ?? string.Empty;
                string fileName = Path.GetFileName(sourceDllPath);

                // Đăng ký bộ phân giải thư viện phụ thuộc từ thư mục nguồn
                RegisterAssemblyResolver(sourceDir);

                // Tạo thư mục Shadow Copy trong %TEMP%
                string shadowDir = Path.Combine(
                    Path.GetTempPath(),
                    "AutoCAD_Reloader",
                    DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")
                );
                Directory.CreateDirectory(shadowDir);

                string shadowDll = Path.Combine(shadowDir, fileName);

                // Copy DLL
                File.Copy(sourceDllPath, shadowDll, overwrite: true);
                logAction?.Invoke($"✓ Đã sao chép DLL sang bộ nhớ đệm: {shadowDll}");

                // Copy PDB (nếu có)
                if (copyPdb)
                {
                    string sourcePdb = Path.ChangeExtension(sourceDllPath, ".pdb");
                    if (File.Exists(sourcePdb))
                    {
                        string shadowPdb = Path.Combine(shadowDir, Path.GetFileName(sourcePdb));
                        File.Copy(sourcePdb, shadowPdb, overwrite: true);
                        logAction?.Invoke("✓ Đã sao chép biểu tượng gỡ lỗi (.pdb)");
                    }
                }

                // Đảm bảo thư mục đệm nằm trong TRUSTEDPATHS của AutoCAD
                EnsureTrustedPath();

                // Nạp Assembly thông qua AutoCAD ExtensionLoader (Tạm thời bỏ qua cảnh báo bảo mật SECURELOAD)
                logAction?.Invoke("🔄 Đang nạp Assembly qua ExtensionLoader.Load()...");

                // Tự động tắt cảnh báo bảo mật SECURELOAD = 0 theo Cách 2
                try
                {
                    object? curSecure = Application.GetSystemVariable("SECURELOAD");
                    if (curSecure != null && Convert.ToInt16(curSecure) != 0)
                    {
                        Application.SetSystemVariable("SECURELOAD", (short)0);
                        logAction?.Invoke("✓ Đã tự động thiết lập SECURELOAD = 0 (tắt cảnh báo bảo mật).");
                    }
                }
                catch { }

                Assembly loadedAssembly = ExtensionLoader.Load(shadowDll);

                if (loadedAssembly == null)
                {
                    logAction?.Invoke("❌ ExtensionLoader.Load trả về null.");
                    return false;
                }

                logAction?.Invoke($"✓ Nạp thành công Assembly: {loadedAssembly.FullName}");

                // Quét danh sách lệnh có trong Assembly
                if (listCommands)
                {
                    var commands = ExtractCommands(loadedAssembly);
                    if (commands.Count > 0)
                    {
                        logAction?.Invoke($"🎯 Tìm thấy {commands.Count} lệnh [CommandMethod]:");
                        foreach (var cmd in commands)
                        {
                            logAction?.Invoke($"    • {cmd}");
                        }
                    }
                    else
                    {
                        logAction?.Invoke("ℹ Không tìm thấy thuộc tính [CommandMethod] nào trong Assembly này.");
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"❌ Lỗi trong quá trình nạp: {ex.Message}");
                if (ex.InnerException != null)
                {
                    logAction?.Invoke($"   Chi tiết: {ex.InnerException.Message}");
                }
                return false;
            }
        }

        private static void RegisterAssemblyResolver(string sourceDir)
        {
            _currentSourceDir = sourceDir;
            if (_resolverRegistered) return;

            // Phân giải cho AppDomain
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                if (string.IsNullOrEmpty(_currentSourceDir)) return null;
                try
                {
                    string simpleName = new AssemblyName(args.Name).Name + ".dll";
                    string candidate = Path.Combine(_currentSourceDir, simpleName);
                    if (File.Exists(candidate))
                    {
                        return Assembly.LoadFrom(candidate);
                    }
                }
                catch { }
                return null;
            };

            // Phân giải cho AssemblyLoadContext (.NET Core / .NET 8 / .NET 10)
            try
            {
                AssemblyLoadContext.Default.Resolving += (context, assemblyName) =>
                {
                    if (string.IsNullOrEmpty(_currentSourceDir)) return null;
                    try
                    {
                        string candidate = Path.Combine(_currentSourceDir, assemblyName.Name + ".dll");
                        if (File.Exists(candidate))
                        {
                            return context.LoadFromAssemblyPath(candidate);
                        }
                    }
                    catch { }
                    return null;
                };
            }
            catch { }

            _resolverRegistered = true;
        }

        private static List<string> ExtractCommands(Assembly assembly)
        {
            var result = new List<string>();
            try
            {
                foreach (Type t in assembly.GetExportedTypes())
                {
                    foreach (MethodInfo m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
                    {
                        var attrs = m.GetCustomAttributes(typeof(CommandMethodAttribute), inherit: false);
                        foreach (CommandMethodAttribute cmdAttr in attrs)
                        {
                            if (!string.IsNullOrEmpty(cmdAttr.GlobalName) && !result.Contains(cmdAttr.GlobalName))
                            {
                                result.Add(cmdAttr.GlobalName);
                            }
                        }
                    }
                }
            }
            catch { }
            return result;
        }

        private static void EnsureTrustedPath()
        {
            try
            {
                // Thư mục gốc chứa các bản sao tạm của Reloader: %TEMP%\AutoCAD_Reloader\...
                // Hậu tố "\..." có ý nghĩa đệ quy mọi thư mục con theo quy định của AutoCAD
                string baseDir = Path.Combine(Path.GetTempPath(), "AutoCAD_Reloader") + @"\...";
                string currentPaths = Application.GetSystemVariable("TRUSTEDPATHS") as string ?? string.Empty;

                bool alreadyPresent = false;
                foreach (string p in currentPaths.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string clean = p.Trim();
                    if (string.Equals(clean, baseDir, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(clean.TrimEnd('\\', '.'), baseDir.TrimEnd('\\', '.'), StringComparison.OrdinalIgnoreCase))
                    {
                        alreadyPresent = true;
                        break;
                    }
                }

                if (!alreadyPresent)
                {
                    string updated = string.IsNullOrEmpty(currentPaths) ? baseDir : $"{currentPaths};{baseDir}";
                    Application.SetSystemVariable("TRUSTEDPATHS", updated);
                }
            }
            catch { }
        }
    }
}

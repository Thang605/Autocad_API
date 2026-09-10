using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.RegularExpressions;
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
        /// Lệnh nạp lại nhanh trực tiếp từ Command Line (Tự động Build & Nạp lại).
        /// </summary>
        [CommandMethod("NETRELOAD")]
        [CommandMethod("RELOAD")]
        [CommandMethod("CADRELOAD")]
        public static void FastReload()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc?.Editor;

            if (NetReloadForm.AutoBuildOnFastReload)
            {
                // Quy trình 1: Tự động Biên dịch (Build) và Nạp lại Assembly mới nhất
                if (ed != null)
                {
                    ed.WriteMessage("\n=======================================================");
                    ed.WriteMessage("\n⚡ [RELOAD] ĐANG TỰ ĐỘNG BIÊN DỊCH VÀ NẠP LẠI CODE MỚI...");
                }

                var sw = Stopwatch.StartNew();
                bool buildOk = ProjectBuildEngine.BuildProject(
                    msg => ed?.WriteMessage($"\n  {msg}"),
                    out string newDllPath
                );
                sw.Stop();

                if (!buildOk || string.IsNullOrEmpty(newDllPath) || !File.Exists(newDllPath))
                {
                    if (ed != null)
                    {
                        ed.WriteMessage($"\n❌ [RELOAD] BIÊN DỊCH THẤT BẠI ({sw.ElapsedMilliseconds} ms). Kiểm tra lại lỗi ở trên hoặc gõ RELOADUI.");
                        ed.WriteMessage("\n=======================================================\n");
                    }
                    return;
                }

                if (ed != null)
                {
                    ed.WriteMessage($"\n✓ Biên dịch thành công trong {sw.ElapsedMilliseconds} ms!");
                    ed.WriteMessage($"\n⚡ Đang nạp Assembly: {Path.GetFileName(newDllPath)} ...");
                }

                NetReloadForm.LastDllPath = newDllPath;

                bool reloadOk = ReloaderEngine.ReloadAssembly(
                    newDllPath,
                    NetReloadForm.LastCopyPdb,
                    NetReloadForm.LastListCommands,
                    msg => ed?.WriteMessage($"\n  {msg}")
                );

                if (ed != null)
                {
                    if (reloadOk)
                    {
                        ed.WriteMessage("\n✅ TẢI LẠI THÀNH CÔNG! Code mới đã có hiệu lực ngay lập tức.");
                    }
                    else
                    {
                        ed.WriteMessage("\n❌ TẢI LẠI THẤT BẠI. Kiểm tra lại lỗi ở trên hoặc gõ RELOADUI.");
                    }
                    ed.WriteMessage("\n=======================================================");
                    ed.WriteMessage("\n💡 Gợi ý: Gõ RELOAD để tự động Build & Nạp lại | Gõ RELOADUI để mở bảng điều khiển.\n");
                }
            }
            else
            {
                // Quy trình 2: Nạp file DLL mới nhất có sẵn mà không build lại
                FastReloadFileOnly();
            }
        }

        /// <summary>
        /// Nạp lại file DLL mới nhất có sẵn trên đĩa mà không chạy build.
        /// </summary>
        [CommandMethod("NETRELOADFILE")]
        [CommandMethod("RELOADFILE")]
        [CommandMethod("CADRELOADFILE")]
        public static void FastReloadFileOnly()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc?.Editor;

            string targetPath = ResolveLatestTargetDll();

            if (!File.Exists(targetPath))
            {
                if (ed != null)
                {
                    ed.WriteMessage($"\n[RELOADFILE] Chưa tìm thấy file DLL tại: '{targetPath}'");
                    ed.WriteMessage("\n[RELOADFILE] Đang mở giao diện cấu hình...");
                }
                ShowReloadUI();
                return;
            }

            NetReloadForm.LastDllPath = targetPath;

            if (ed != null)
            {
                ed.WriteMessage("\n=======================================================");
                ed.WriteMessage("\n⚡ [RELOADFILE] ĐANG TẢI LẠI ASSEMBLY KHÔNG KHÓA FILE...");
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
                    ed.WriteMessage("\n❌ TẢI LẠI THẤT BẠI. Kiểm tra lại lỗi ở trên hoặc gõ RELOADUI để cấu hình.");
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

        /// <summary>
        /// Tìm kiếm đường dẫn file DLL mới nhất (ưu tiên file dynamic có random suffix và timestamp mới nhất).
        /// </summary>
        public static string ResolveLatestTargetDll()
        {
            string buildDir = @"C:\CadBuild\Autocad2026_API\MyFirstProject\bin\Debug";

            // 1. Kiểm tra file last_dll.txt từ thư mục Repo hiện tại
            string repoRoot = ProjectBuildEngine.ResolveRepoRootDir();
            string lastDllTextFile = Path.Combine(repoRoot, "last_dll.txt");

            if (File.Exists(lastDllTextFile))
            {
                try
                {
                    string txt = File.ReadAllText(lastDllTextFile).Trim();
                    if (!string.IsNullOrEmpty(txt) && File.Exists(txt))
                    {
                        return txt;
                    }
                }
                catch { }
            }

            // 2. Tìm file DLL mới nhất trong thư mục build (ưu tiên file dynamic Civil3D_Tools_*.dll)
            if (Directory.Exists(buildDir))
            {
                try
                {
                    var dynamicFiles = new DirectoryInfo(buildDir).GetFiles("Civil3D_Tools_*.dll")
                        .Where(f => !f.Name.EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(f => f.LastWriteTimeUtc)
                        .ToList();

                    if (dynamicFiles.Count > 0)
                    {
                        return dynamicFiles[0].FullName;
                    }

                    var staticDll = Path.Combine(buildDir, "Civil3D_Tools.dll");
                    if (File.Exists(staticDll))
                    {
                        return staticDll;
                    }
                }
                catch { }
            }

            // 3. Fallback theo cấu hình tĩnh
            return NetReloadForm.LastDllPath;
        }
    }

    /// <summary>
    /// Engine thực thi biên dịch (dotnet build) với unique AssemblyName và tự động dò đường dẫn Dropbox trên mọi máy.
    /// </summary>
    public static class ProjectBuildEngine
    {
        private const string BuildRoot = @"C:\CadBuild\Autocad2026_API";
        private const string Configuration = "Debug";
        private const int KeepRecentBuilds = 5;

        /// <summary>
        /// Tự động phát hiện thư mục gốc của Repo dự án trên bất kỳ máy tính nào (kể cả Dropbox đặt ở D:\, E:\ hoặc UserProfile).
        /// </summary>
        public static string ResolveRepoRootDir()
        {
            // 1. Kiểm tra nếu người dùng đã lưu thủ công đường dẫn hợp lệ
            if (!string.IsNullOrEmpty(NetReloadForm.CustomRepoPath) && 
                File.Exists(Path.Combine(NetReloadForm.CustomRepoPath, "MyFirstProject", "MyFirstProject.csproj")))
            {
                return NetReloadForm.CustomRepoPath;
            }

            // 2. Kiểm tra đường dẫn mặc định
            string defaultPath = @"C:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API";
            if (File.Exists(Path.Combine(defaultPath, "MyFirstProject", "MyFirstProject.csproj")))
            {
                return defaultPath;
            }

            // 3. Đọc cấu hình Dropbox info.json trong %LOCALAPPDATA% hoặc %APPDATA%
            try
            {
                string[] jsonPaths = new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Dropbox", "info.json"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Dropbox", "info.json")
                };

                foreach (var jsonPath in jsonPaths)
                {
                    if (File.Exists(jsonPath))
                    {
                        string json = File.ReadAllText(jsonPath);
                        var matches = Regex.Matches(json, @"""path""\s*:\s*""([^""]+)""");
                        foreach (Match match in matches)
                        {
                            if (match.Success)
                            {
                                string dbPath = match.Groups[1].Value.Replace(@"\\", @"\");
                                string candidate = Path.Combine(dbPath, @"0.AI AGENT\6.C#\Autocad 2026_API");
                                if (File.Exists(Path.Combine(candidate, "MyFirstProject", "MyFirstProject.csproj")))
                                {
                                    return candidate;
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            // 4. Kiểm tra trong %USERPROFILE%\Dropbox...
            try
            {
                string userProfileDb = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @"Dropbox\0.AI AGENT\6.C#\Autocad 2026_API");
                if (File.Exists(Path.Combine(userProfileDb, "MyFirstProject", "MyFirstProject.csproj")))
                {
                    return userProfileDb;
                }
            }
            catch { }

            // 5. Quét tất cả các ổ đĩa trên máy tính
            try
            {
                foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && (d.DriveType == DriveType.Fixed || d.DriveType == DriveType.Removable)))
                {
                    string candidate = Path.Combine(drive.RootDirectory.FullName, @"Dropbox\0.AI AGENT\6.C#\Autocad 2026_API");
                    if (File.Exists(Path.Combine(candidate, "MyFirstProject", "MyFirstProject.csproj")))
                    {
                        return candidate;
                    }
                }
            }
            catch { }

            return defaultPath;
        }

        public static string GetDotNetExePath()
        {
            string userDotnet = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet", "dotnet.exe");
            if (File.Exists(userDotnet)) return userDotnet;

            string pfDotnet = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe");
            if (File.Exists(pfDotnet)) return pfDotnet;

            return "dotnet";
        }

        public static bool BuildProject(Action<string> logAction, out string outputDllPath)
        {
            outputDllPath = string.Empty;

            string repoRoot = ResolveRepoRootDir();
            string projectDir = Path.Combine(repoRoot, "MyFirstProject");
            string csprojPath = Path.Combine(projectDir, "MyFirstProject.csproj");

            if (!File.Exists(csprojPath))
            {
                logAction?.Invoke($"❌ Không tìm thấy file dự án tại: {csprojPath}");
                logAction?.Invoke("💡 Vui lòng gõ RELOADUI để kiểm tra hoặc chọn lại thư mục dự án.");
                return false;
            }

            string dotnetExe = GetDotNetExePath();
            string dotnetDir = Path.GetDirectoryName(dotnetExe) ?? string.Empty;

            // Tạo unique AssemblyName để .NET Runtime luôn nạp code mới vào bộ nhớ
            string randomName = Path.GetRandomFileName().Replace(".", "");
            string uniqueAssemblyName = $"Civil3D_Tools_{randomName}";

            logAction?.Invoke($"📁 Thư mục dự án: {projectDir}");
            logAction?.Invoke($"🔨 Đang biên dịch: {Path.GetFileName(csprojPath)} (Assembly: {uniqueAssemblyName})...");

            var psi = new ProcessStartInfo
            {
                FileName = dotnetExe,
                Arguments = $"build \"{csprojPath}\" -c {Configuration} /p:AssemblyName={uniqueAssemblyName} /p:Clean=false /nowarn:MSB3061,NU1510",
                WorkingDirectory = projectDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            if (!string.IsNullOrEmpty(dotnetDir) && Directory.Exists(dotnetDir))
            {
                psi.EnvironmentVariables["DOTNET_ROOT"] = dotnetDir;
                string currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                psi.EnvironmentVariables["PATH"] = $"{dotnetDir};{currentPath}";
            }

            try
            {
                using var process = Process.Start(psi);
                if (process == null)
                {
                    logAction?.Invoke("❌ Không thể khởi động tiến trình dotnet build.");
                    return false;
                }

                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                process.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        outputBuilder.AppendLine(e.Data);
                        if (e.Data.Contains("error", StringComparison.OrdinalIgnoreCase))
                        {
                            logAction?.Invoke($"  [LỖI] {e.Data.Trim()}");
                        }
                    }
                };

                process.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        errorBuilder.AppendLine(e.Data);
                        logAction?.Invoke($"  [STDERR] {e.Data.Trim()}");
                    }
                };

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    logAction?.Invoke($"❌ Biên dịch thất bại với mã thoát: {process.ExitCode}");
                    string errors = errorBuilder.ToString().Trim();
                    if (!string.IsNullOrEmpty(errors))
                    {
                        logAction?.Invoke($"Chi tiết lỗi:\n{errors}");
                    }
                    return false;
                }

                // Xác định đường dẫn file DLL kết quả
                string binDir = Path.Combine(BuildRoot, "MyFirstProject", "bin", Configuration);
                string expectedDll = Path.Combine(binDir, $"{uniqueAssemblyName}.dll");

                if (!File.Exists(expectedDll))
                {
                    logAction?.Invoke($"❌ Không tìm thấy file DLL sau khi build: {expectedDll}");
                    return false;
                }

                outputDllPath = expectedDll;

                // Đồng bộ sang Civil3D_Tools.dll (nếu có thể)
                try
                {
                    string staticDll = Path.Combine(binDir, "Civil3D_Tools.dll");
                    File.Copy(expectedDll, staticDll, overwrite: true);

                    string expectedPdb = Path.ChangeExtension(expectedDll, ".pdb");
                    if (File.Exists(expectedPdb))
                    {
                        File.Copy(expectedPdb, Path.Combine(binDir, "Civil3D_Tools.pdb"), overwrite: true);
                    }
                }
                catch { }

                // Cập nhật last_dll.txt và last_reload.lsp trong thư mục Repo
                try
                {
                    string fwdPath = expectedDll.Replace("\\", "/");
                    File.WriteAllText(Path.Combine(repoRoot, "last_dll.txt"), fwdPath, Encoding.UTF8);

                    string lspContent = $"(vl-load-com)\n(vl-cmdf \"._NETLOAD\" \"{fwdPath}\")\n(princ \"\\n=======================================================\\n\")\n(princ \"\\n[NETLOAD] DA NAP THANH CONG: {uniqueAssemblyName}.dll\\n\")\n(princ \"\\n=======================================================\\n\")\n(princ)";
                    File.WriteAllText(Path.Combine(repoRoot, "last_reload.lsp"), lspContent, Encoding.UTF8);
                }
                catch { }

                // Dọn dẹp các bản build cũ để không làm đầy ổ cứng
                PruneOldBuilds(binDir, expectedDll, "Civil3D_Tools", KeepRecentBuilds);

                return true;
            }
            catch (Exception ex)
            {
                logAction?.Invoke($"❌ Lỗi trong quá trình build: {ex.Message}");
                return false;
            }
        }

        private static void PruneOldBuilds(string binDir, string currentDllPath, string prefix, int keepCount)
        {
            try
            {
                if (!Directory.Exists(binDir)) return;

                var staleFiles = Directory.GetFiles(binDir, $"{prefix}_*.dll")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .Skip(keepCount)
                    .Where(f => !string.Equals(f.FullName, currentDllPath, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var file in staleFiles)
                {
                    try
                    {
                        string pdb = Path.ChangeExtension(file.FullName, ".pdb");
                        string deps = Path.ChangeExtension(file.FullName, ".deps.json");

                        file.Delete();
                        if (File.Exists(pdb)) File.Delete(pdb);
                        if (File.Exists(deps)) File.Delete(deps);
                    }
                    catch { }
                }
            }
            catch { }
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

                // Sao chép các dependency sang thư mục shadow copy nếu cần
                try
                {
                    foreach (var dep in Directory.GetFiles(sourceDir, "*.dll"))
                    {
                        string depName = Path.GetFileName(dep);
                        if (depName.StartsWith("Civil3D_Tools", StringComparison.OrdinalIgnoreCase) ||
                            depName.StartsWith("NRL_", StringComparison.OrdinalIgnoreCase))
                            continue;

                        string targetDep = Path.Combine(shadowDir, depName);
                        if (!File.Exists(targetDep))
                        {
                            File.Copy(dep, targetDep, overwrite: true);
                        }
                    }
                }
                catch { }

                // Đảm bảo thư mục đệm nằm trong TRUSTEDPATHS của AutoCAD
                EnsureTrustedPath();

                // Nạp Assembly thông qua AutoCAD ExtensionLoader (Tạm thời bỏ qua cảnh báo bảo mật SECURELOAD)
                logAction?.Invoke("🔄 Đang nạp Assembly qua ExtensionLoader.Load()...");

                // Tự động tắt cảnh báo bảo mật SECURELOAD = 0
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

                Assembly? loadedAssembly = null;
                try
                {
                    loadedAssembly = ExtensionLoader.Load(shadowDll);
                }
                catch (Exception exLoad)
                {
                    logAction?.Invoke($"⚠ ExtensionLoader.Load ném ngoại lệ: {exLoad.Message}. Đang thử fallback Assembly.LoadFrom...");
                    loadedAssembly = Assembly.LoadFrom(shadowDll);
                }

                if (loadedAssembly == null)
                {
                    logAction?.Invoke("❌ Không thể nạp Assembly vào bộ nhớ.");
                    return false;
                }

                logAction?.Invoke($"✓ Nạp thành công Assembly: {loadedAssembly.GetName().Name}");

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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;

[assembly: CommandClass(typeof(NetReload.Commands))]

namespace NetReload
{

	public class Commands
	{
		// Thư mục output nằm NGOÀI Dropbox (xem Directory.Build.props ở gốc solution).
		// Nếu để trong C:\Dropbox thì mỗi lần reload Dropbox phải hash + upload DLL ~1.7 MB
		// và Kaspersky phải quét lại -> lag cả máy theo chu kỳ vài giây.
		private const string BuildRoot = @"C:\CadBuild\Autocad2026_API";

		// Release: bản Debug không tối ưu, chạy chậm hơn rõ rệt trong Civil 3D.
		// Đổi lại "Debug" nếu cần đặt breakpoint.
		private const string Configuration = "Release";

		// Giữ lại tối đa N bản DLL gần nhất trong thư mục NetReload, xoá các bản cũ hơn.
		private const int KeepRecentBuilds = 5;

		// NRL command - Short alias for RELOAD
		[CommandMethod("NRL")]
		public static void Nrl()
		{
			Reload();
		}

		// RELOAD command - Hot reload project without Visual Studio.
		[CommandMethod("RELOAD")]
		public static void Reload()
		{
			Autodesk.AutoCAD.ApplicationServices.Document doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
			Editor ed = doc.Editor;

			try
			{
				// 1. Determine Project Path
				// Since this is a standalone reloader, we point it to the main project we want to reload.
				string projectDir = @"C:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\MyFirstProject";

				if (string.IsNullOrEmpty(projectDir) || !Directory.Exists(projectDir))
				{
					ed.WriteMessage($"\nCould not find project directory: {projectDir}. *Cancel*");
					return;
				}

				string csprojFile = Directory.GetFiles(projectDir, "*.csproj").FirstOrDefault();
				if (string.IsNullOrEmpty(csprojFile))
				{
					ed.WriteMessage("\nCould not find .csproj file. *Cancel*");
					return;
				}

				string projectName = Path.GetFileNameWithoutExtension(csprojFile);

				// Generate unique assembly name to avoid "Assembly already loaded" error
				string randomName = Path.GetRandomFileName().Replace(".", "");
				string uniqueAssemblyName = $"{projectName}_{randomName}";

				ed.WriteMessage($"\nReloading project: {projectName} (New Assembly: {uniqueAssemblyName})...");

				// 2. Run dotnet build with unique AssemblyName
				string configuration = Configuration;
				string dotnetExe = GetDotNetPath();
				string dotnetDir = Path.GetDirectoryName(dotnetExe) ?? "";

				ProcessStartInfo psi = new ProcessStartInfo
				{
					FileName = dotnetExe,
					Arguments = $"build \"{csprojFile}\" -c {configuration} /p:AssemblyName={uniqueAssemblyName}",
					WorkingDirectory = projectDir,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					UseShellExecute = false,
					CreateNoWindow = true
				};

				if (!string.IsNullOrEmpty(dotnetDir) && Directory.Exists(dotnetDir))
				{
					psi.EnvironmentVariables["DOTNET_ROOT"] = dotnetDir;
					string currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";
					psi.EnvironmentVariables["PATH"] = $"{dotnetDir};{currentPath}";
				}

				using (Process process = Process.Start(psi))
				{
					StringBuilder outputBuilder = new StringBuilder();
					StringBuilder errorBuilder = new StringBuilder();

					process.OutputDataReceived += (sender, args) =>
					{
						if (args.Data != null) outputBuilder.AppendLine(args.Data);
					};
					process.ErrorDataReceived += (sender, args) =>
					{
						if (args.Data != null) errorBuilder.AppendLine(args.Data);
					};

					process.BeginOutputReadLine();
					process.BeginErrorReadLine();

					process.WaitForExit();

					string output = outputBuilder.ToString();
					string error = errorBuilder.ToString();

					if (process.ExitCode != 0)
					{
						ed.WriteMessage($"\nBuild Failed:\n{output}\n{error}");
						return;
					}
				}

				// 3. Locate Output DLL (output nằm ngoài Dropbox)
				string binDir = Path.Combine(BuildRoot, projectName, "bin", configuration);
				if (!Directory.Exists(binDir))
				{
					ed.WriteMessage($"\nOutput directory not found: {binDir}. *Cancel*");
					return;
				}

				var dllFiles = Directory.GetFiles(binDir, $"{uniqueAssemblyName}.dll", SearchOption.AllDirectories)
					.Select(f => new FileInfo(f))
					.OrderByDescending(f => f.LastWriteTime)
					.ToList();

				if (dllFiles.Count == 0)
				{
					ed.WriteMessage($"\nCould not find built DLL ({uniqueAssemblyName}.dll). *Cancel*");
					return;
				}

				FileInfo sourceDll = dllFiles.First();

				// 4. Copy to reload staging directory (ngoài Dropbox)
				// Dùng tên "_reload" để không lẫn với output của chính project NetReload
				// (C:\CadBuild\Autocad2026_API\NetReload\bin\...).
				string netReloadDir = Path.Combine(BuildRoot, "_reload");
				if (!Directory.Exists(netReloadDir))
				{
					Directory.CreateDirectory(netReloadDir);
				}

				string destDllPath = Path.Combine(netReloadDir, sourceDll.Name);
				string destPdbPath = Path.ChangeExtension(destDllPath, "pdb");

				File.Copy(sourceDll.FullName, destDllPath, true);

				string sourcePdb = Path.ChangeExtension(sourceDll.FullName, "pdb");
				if (File.Exists(sourcePdb))
				{
					File.Copy(sourcePdb, destPdbPath, true);
				}

				// Clean up the artifact from bin (kể cả .deps.json, trước đây bị bỏ sót và tích tụ)
				try
				{
					File.Delete(sourceDll.FullName);
					if (File.Exists(sourcePdb)) File.Delete(sourcePdb);

					string sourceDeps = Path.ChangeExtension(sourceDll.FullName, "deps.json");
					if (File.Exists(sourceDeps)) File.Delete(sourceDeps);
				}
				catch { /* Ignore cleanup errors */ }

				// 5. Đồng bộ DLL phụ thuộc (ClosedXML, OpenXml, RBush...) vào _reload
				// để thư mục này tự chứa đủ; AssemblyResolve handler chỉ probe thư mục
				// của assembly được nạp.
				SyncDependencies(binDir, netReloadDir);

				// 6. Load the new DLL
				Assembly.LoadFrom(destDllPath);

				// 7. Xoá các bản build cũ để thư mục không phình lên (trước đây tích tụ 326 file / 292 MB)
				int pruned = PruneOldBuilds(netReloadDir, destDllPath, projectName);

				ed.WriteMessage($"\nNETRELOAD complete. Loaded: {uniqueAssemblyName}.dll ({configuration})");
				if (pruned > 0) ed.WriteMessage($"\nCleaned {pruned} old build file(s).");

			}
			catch (System.Exception ex)
			{
				ed.WriteMessage($"\nError: {ex.Message}");
			}
		}

		/// <summary>
		/// Copy các DLL phụ thuộc từ thư mục build sang thư mục staging, bỏ qua file đã có
		/// và cùng thời điểm ghi (tránh copy lại ~10 MB mỗi lần reload).
		/// Bỏ qua mọi DLL là output của chính project: chỉ output mới có file .deps.json
		/// đi kèm, còn ClosedXML/OpenXml/RBush... thì không.
		/// </summary>
		private static void SyncDependencies(string binDir, string stageDir)
		{
			try
			{
				foreach (string source in Directory.GetFiles(binDir, "*.dll"))
				{
					if (File.Exists(Path.ChangeExtension(source, "deps.json"))) continue;

					string target = Path.Combine(stageDir, Path.GetFileName(source));
					if (File.Exists(target) &&
						File.GetLastWriteTimeUtc(target) == File.GetLastWriteTimeUtc(source)) continue;

					try { File.Copy(source, target, true); }
					catch { /* DLL đang bị lock -> bản cũ vẫn dùng được */ }
				}
			}
			catch { /* Ignore */ }
		}

		/// <summary>
		/// Giữ lại <see cref="KeepRecentBuilds"/> bản DLL mới nhất, xoá phần còn lại.
		/// CHỈ xét các bản build của plugin ("{projectName}_*.dll") — không được đụng vào
		/// DLL phụ thuộc (ClosedXML, OpenXml...) vì chúng có timestamp cũ và sẽ bị xoá oan.
		/// DLL đang được nạp không thể xoá (bị lock) nên bỏ qua lỗi.
		/// </summary>
		private static int PruneOldBuilds(string netReloadDir, string currentDllPath, string projectName)
		{
			int deleted = 0;
			try
			{
				var stale = Directory.GetFiles(netReloadDir, $"{projectName}_*.dll")
					.Select(f => new FileInfo(f))
					.OrderByDescending(f => f.LastWriteTimeUtc)
					.Skip(KeepRecentBuilds)
					.Where(f => !string.Equals(f.FullName, currentDllPath, StringComparison.OrdinalIgnoreCase));


				foreach (FileInfo file in stale)
				{
					try
					{
						string pdb = Path.ChangeExtension(file.FullName, "pdb");
						file.Delete();
						deleted++;
						if (File.Exists(pdb)) { File.Delete(pdb); deleted++; }
					}
					catch { /* DLL đang bị process lock -> để lần sau */ }
				}
			}
			catch { /* Ignore */ }
			return deleted;
		}

		private static string GetDotNetPath()
		{
			string userDotnet = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet", "dotnet.exe");
			if (File.Exists(userDotnet))
				return userDotnet;

			string programFilesDotnet = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe");
			if (File.Exists(programFilesDotnet))
				return programFilesDotnet;

			return "dotnet";
		}
	}
}

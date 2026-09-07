using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using Autodesk.AutoCAD.Runtime;

[assembly: ExtensionApplication(typeof(Civil3DCsharp.Civil3DToolsExtensionApplication))]

namespace Civil3DCsharp
{
    /// <summary>
    /// Extension Application khởi tạo các cấu hình hệ thống, đặc biệt là tự động tìm nạp DLL phụ thuộc (ClosedXML, OpenXml...)
    /// </summary>
    public class Civil3DToolsExtensionApplication : IExtensionApplication
    {
        // Trạng thái phải nằm trong AppDomain, KHÔNG phải static field.
        // static là per-assembly: mỗi lần NRL nạp thêm một bản Civil3D_Tools_xxx.dll thì
        // cờ static reset về false và handler bị đăng ký thêm một lần nữa. Sau N lần reload
        // có N handler cùng chạy File.Exists() cho mọi lần .NET dò assembly trượt.
        // AppDomain data dùng chung giữa mọi bản assembly (chỉ chứa kiểu của framework
        // để định danh kiểu khớp nhau giữa các assembly).
        private const string AttachedKey  = "Civil3DTools.Resolver.Attached";
        private const string ProbeDirsKey = "Civil3DTools.Resolver.ProbeDirs";
        private const string NegCacheKey  = "Civil3DTools.Resolver.NegativeCache";

        private static bool _attachedByThisAssembly;

        public void Initialize()
        {
            EnsureResolverAttached();
        }

        public void Terminate()
        {
            if (!_attachedByThisAssembly) return;
            AppDomain.CurrentDomain.AssemblyResolve -= OnAssemblyResolve;
            AppDomain.CurrentDomain.SetData(AttachedKey, null);
            _attachedByThisAssembly = false;
        }

        public static void EnsureResolverAttached()
        {
            AppDomain domain = AppDomain.CurrentDomain;

            // Locking trên chính AppDomain: đối tượng này có cùng định danh ở mọi bản
            // assembly đã nạp, nên nó khoá được cả các bản nạp lại qua NRL.
            lock (domain)
            {
                ConcurrentDictionary<string, byte> probeDirs =
                    domain.GetData(ProbeDirsKey) as ConcurrentDictionary<string, byte>;
                if (probeDirs == null)
                {
                    probeDirs = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
                    domain.SetData(ProbeDirsKey, probeDirs);
                }

                // Mỗi bản assembly góp thư mục của nó vào danh sách probe dùng chung,
                // để bản nạp từ folder khác vẫn tìm được ClosedXML/OpenXml.
                string folder = GetAssemblyFolder();
                if (folder.Length > 0) probeDirs.TryAdd(folder, 0);

                if (domain.GetData(NegCacheKey) is not ConcurrentDictionary<string, byte>)
                {
                    domain.SetData(NegCacheKey,
                        new ConcurrentDictionary<string, byte>(StringComparer.Ordinal));
                }

                if (domain.GetData(AttachedKey) is bool attached && attached) return;

                domain.AssemblyResolve += OnAssemblyResolve;
                domain.SetData(AttachedKey, true);
                _attachedByThisAssembly = true;
            }
        }

        private static string GetAssemblyFolder()
        {
            try
            {
                string location = typeof(Civil3DToolsExtensionApplication).Assembly.Location;
                if (string.IsNullOrEmpty(location)) return "";
                return Path.GetDirectoryName(location) ?? "";
            }
            catch { return ""; }
        }

        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            try
            {
                string requested = args.Name;
                if (string.IsNullOrEmpty(requested)) return null;

                AppDomain domain = AppDomain.CurrentDomain;
                var negCache = domain.GetData(NegCacheKey) as ConcurrentDictionary<string, byte>;

                // Đã dò trượt tên này rồi -> không đụng đĩa lần nữa.
                if (negCache != null && negCache.ContainsKey(requested)) return null;

                string simpleName;
                try { simpleName = new AssemblyName(requested).Name ?? ""; }
                catch { return null; }
                if (simpleName.Length == 0) return null;

                // Satellite resource assembly: WinForms/WPF dò liên tục và ta không bao giờ
                // có sẵn. Đây là nguồn gọi handler nhiều nhất -> chặn trước khi đọc đĩa.
                if (simpleName.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
                {
                    negCache?.TryAdd(requested, 0);
                    return null;
                }

                // Đã nạp trong process thì trả về luôn, không đọc đĩa.
                foreach (Assembly loaded in domain.GetAssemblies())
                {
                    if (string.Equals(loaded.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase))
                        return loaded;
                }

                var probeDirs = domain.GetData(ProbeDirsKey) as ConcurrentDictionary<string, byte>;
                if (probeDirs != null)
                {
                    foreach (string dir in probeDirs.Keys)
                    {
                        try
                        {
                            string candidate = Path.Combine(dir, simpleName + ".dll");
                            if (File.Exists(candidate)) return Assembly.LoadFrom(candidate);
                        }
                        catch { }
                    }
                }

                negCache?.TryAdd(requested, 0);
                return null;
            }
            catch { return null; }
        }
    }
}

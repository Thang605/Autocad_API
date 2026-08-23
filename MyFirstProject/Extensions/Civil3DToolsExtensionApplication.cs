using System;
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
        private static bool _isResolverAttached = false;

        public void Initialize()
        {
            EnsureResolverAttached();
        }

        public void Terminate()
        {
        }

        public static void EnsureResolverAttached()
        {
            if (_isResolverAttached) return;
            _isResolverAttached = true;

            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                try
                {
                    string assemblyName = new AssemblyName(args.Name).Name ?? "";
                    if (string.IsNullOrEmpty(assemblyName)) return null;

                    string pluginFolder = Path.GetDirectoryName(typeof(Civil3DToolsExtensionApplication).Assembly.Location) ?? "";
                    if (string.IsNullOrEmpty(pluginFolder)) return null;

                    string targetFile = Path.Combine(pluginFolder, assemblyName + ".dll");
                    if (File.Exists(targetFile))
                    {
                        return Assembly.LoadFrom(targetFile);
                    }
                }
                catch { }
                return null;
            };
        }
    }
}

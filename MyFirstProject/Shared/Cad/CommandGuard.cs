using System;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace MyFirstProject.Shared.Cad
{
    /// <summary>
    /// Khung chạy chuẩn cho thân lệnh <c>[CommandMethod]</c> (quy tắc AGENTS.md - Sentinel):
    /// kiểm tra Document null, bắt mọi ngoại lệ và báo ra command line thay vì làm sập AutoCAD.
    /// <code>
    /// [CommandMethod("CTT_Ten_Lenh")]
    /// public static void Execute() => CommandGuard.Run("CTT_Ten_Lenh", (doc, db, ed) => { ... });
    /// </code>
    /// </summary>
    public static class CommandGuard
    {
        /// <param name="commandName">Tên lệnh, dùng làm tiền tố thông báo.</param>
        /// <param name="body">Thân lệnh.</param>
        /// <param name="lockDocument">
        /// true khi gọi từ ngữ cảnh modeless (PaletteSet, Form không modal, sự kiện) để tránh eLockViolation.
        /// Lệnh chạy từ command line đã được AutoCAD tự khóa document.
        /// </param>
        public static void Run(string commandName, Action<Document, Database, Editor> body, bool lockDocument = false)
        {
            Document? doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            Editor ed = doc.Editor;
            try
            {
                using (DocumentLock? docLock = lockDocument ? doc.LockDocument() : null)
                {
                    body(doc, doc.Database, ed);
                }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                ed.WriteMessage($"\n[{commandName}] Lỗi AutoCAD ({ex.ErrorStatus}): {ex.Message}");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n[{commandName}] Lỗi: {ex.Message}");
            }
        }
    }
}

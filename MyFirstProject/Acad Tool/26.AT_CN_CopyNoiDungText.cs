// Lệnh CN: Copy nội dung text và truyền vào text khác
// Hỗ trợ 2 chế độ:
//   1) Copy nội dung (không link) - copy text thuần túy
//   2) Link nội dung (Field liên kết) - text đích liên kết với text nguồn bằng Field

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(Civil3DCsharp.AT_CN_CopyNoiDungText))]

namespace Civil3DCsharp
{
    public class AT_CN_CopyNoiDungText
    {
        /// <summary>
        /// Strip formatting codes từ MText để lấy text thuần túy
        /// </summary>
        private static string StripMTextFormatting(string mTextContent)
        {
            if (string.IsNullOrEmpty(mTextContent))
                return string.Empty;

            string cleaned = mTextContent;

            // Loại bỏ color codes
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\\C\d+;", "");
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\{\\C\d+;([^}]*)\}", "$1");

            // Loại bỏ font codes
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\{\\[Ff][^;]*;([^}]*)\}", "$1");

            // Loại bỏ height codes
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\{\\H[\d.]+x?;([^}]*)\}", "$1");

            // Loại bỏ width codes
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\{\\W[\d.]+;([^}]*)\}", "$1");

            // Loại bỏ underline, overline, strikethrough
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\{\\[LOKlok]([^}]*)\}", "$1");

            // Paragraph breaks
            cleaned = cleaned.Replace("\\P", "\n");
            cleaned = cleaned.Replace("\\p", "\n");

            // Stacking
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\\S([^^;]+)\^([^;]*);", "$1/$2");

            // Ngoặc nhọn thừa
            cleaned = cleaned.Replace("{", "").Replace("}", "");

            // Backslash đơn lẻ
            cleaned = cleaned.Replace("\\", "");

            return cleaned.Trim();
        }

        /// <summary>
        /// Lệnh CN: Copy nội dung text và truyền vào text khác.
        /// Hiện form để chọn Link hoặc Không link.
        /// </summary>
        [CommandMethod("CN")]
        public static void CopyNoiDung()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;

            try
            {
                // === Bước 1: Hiện form tuỳ chọn ===
                var form = new MyFirstProject.Acad_Tool.CopyNoiDungForm();
                var dlgResult = Application.ShowModalDialog(form);

                if (dlgResult != System.Windows.Forms.DialogResult.OK || !form.FormAccepted)
                {
                    ed.WriteMessage("\n Đã hủy lệnh CN.");
                    return;
                }

                bool isLinked = form.IsLinked;

                // === Bước 2: Chọn text nguồn ===
                PromptEntityOptions sourcePrompt = new PromptEntityOptions("\nChọn text nguồn để copy nội dung: ");
                sourcePrompt.SetRejectMessage("\nĐối tượng phải là Text hoặc MText!");
                sourcePrompt.AddAllowedClass(typeof(DBText), true);
                sourcePrompt.AddAllowedClass(typeof(MText), true);

                PromptEntityResult sourceResult = ed.GetEntity(sourcePrompt);
                if (sourceResult.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\nĐã hủy chọn text nguồn.");
                    return;
                }

                ObjectId sourceId = sourceResult.ObjectId;
                string textContent = string.Empty;

                // Lấy nội dung text nguồn
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    Entity sourceEntity = tr.GetObject(sourceId, OpenMode.ForWrite) as Entity;

                    if (sourceEntity is DBText dbText)
                    {
                        textContent = dbText.TextString;
                    }
                    else if (sourceEntity is MText mText)
                    {
                        textContent = mText.Text;
                        if (string.IsNullOrEmpty(textContent))
                        {
                            textContent = StripMTextFormatting(mText.Contents);
                        }
                    }

                    tr.Commit();
                }

                if (string.IsNullOrEmpty(textContent))
                {
                    ed.WriteMessage("\nText nguồn không có nội dung!");
                    return;
                }

                string modeText = isLinked ? "LINK (Field liên kết)" : "COPY (không link)";
                ed.WriteMessage($"\nNội dung: \"{textContent}\"");
                ed.WriteMessage($"\nChế độ: {modeText}");
                ed.WriteMessage("\n" + new string('-', 50));

                // === Bước 3: Chọn text đích ===
                PromptSelectionOptions selOptions = new PromptSelectionOptions
                {
                    MessageForAdding = "\nChọn các text đích cần cập nhật (hoặc Enter để kết thúc): ",
                    AllowDuplicates = false
                };

                TypedValue[] filterList = new TypedValue[]
                {
                    new TypedValue((int)DxfCode.Start, "TEXT,MTEXT")
                };
                SelectionFilter filter = new SelectionFilter(filterList);

                PromptSelectionResult selResult = ed.GetSelection(selOptions, filter);
                if (selResult.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\nĐã hủy chọn text đích.");
                    return;
                }

                SelectionSet selSet = selResult.Value;
                int successCount = 0;
                int totalCount = selSet.Count;

                // === Bước 4: Cập nhật text đích ===
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    foreach (SelectedObject selObj in selSet)
                    {
                        if (selObj == null) continue;

                        Entity entity = tr.GetObject(selObj.ObjectId, OpenMode.ForWrite) as Entity;
                        if (entity == null) continue;

                        if (isLinked)
                        {
                            // === Chế độ LINK: Dùng Field liên kết đến text nguồn ===
                            ApplyFieldLink(entity, sourceId, tr);
                            successCount++;
                        }
                        else
                        {
                            // === Chế độ COPY: Copy nội dung text thuần túy ===
                            if (entity is DBText targetDbText)
                            {
                                targetDbText.TextString = textContent;
                                successCount++;
                            }
                            else if (entity is MText targetMText)
                            {
                                targetMText.Contents = textContent;
                                successCount++;
                            }
                        }
                    }

                    tr.Commit();
                }

                // Thông báo kết quả
                ed.WriteMessage("\n" + new string('=', 50));
                ed.WriteMessage($"\nHoàn thành: {successCount}/{totalCount} text đã được cập nhật.");
                ed.WriteMessage($"\nChế độ: {modeText}");
                ed.WriteMessage($"\nNội dung: \"{textContent}\"");
                if (isLinked)
                {
                    ed.WriteMessage("\n⚡ Các text đích đã được liên kết với text nguồn.");
                    ed.WriteMessage("\n   Khi text nguồn thay đổi → gõ REGEN để cập nhật.");
                }
                ed.WriteMessage("\n" + new string('=', 50));
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\nLỗi: {ex.Message}");
            }
        }

        /// <summary>
        /// Tạo Field liên kết từ entity đích đến text nguồn.
        /// Field sẽ tự động cập nhật nội dung khi text nguồn thay đổi.
        /// Hỗ trợ: DBText→DBText, DBText→MText, MText→DBText, MText→MText
        /// </summary>
        private static void ApplyFieldLink(Entity targetEntity, ObjectId sourceId, Transaction tr)
        {
            Database db = targetEntity.Database;
            DBObject sourceObj = tr.GetObject(sourceId, OpenMode.ForWrite);

            // Lấy ObjectId dạng decimal (AutoCAD Field _ObjId yêu cầu ObjectId pointer, không phải Handle)
            long objIdValue = sourceId.OldIdPtr.ToInt64();

            // Xác định property name dựa trên loại text nguồn:
            // - DBText: dùng "TextString" 
            // - MText: dùng "Contents" (để giữ formatting)
            string propName = (sourceObj is MText) ? "Contents" : "TextString";

            // Tạo Field expression
            // Format: %<\AcObjProp Object(%<\_ObjId HANDLE_DECIMAL>%).PropertyName>%
            string fieldCode = $"%<\\AcObjProp Object(%<\\_ObjId {objIdValue}>%).{propName}>%";

            if (targetEntity is MText targetMText)
            {
                // MText hỗ trợ nhúng field code trực tiếp vào Contents
                targetMText.Contents = fieldCode;
            }
            else if (targetEntity is DBText targetDbText)
            {
                // DBText: tạo Field object và gán qua SetField
                Field field = new Field(fieldCode);
                field.EvaluationOption = FieldEvaluationOptions.Automatic;

                // Đăng ký Field vào database
                ObjectId fieldId = db.AddDBObject(field);
                tr.AddNewlyCreatedDBObject(field, true);

                // Gán field cho DBText
                targetDbText.SetField(field);

                // Evaluate field để hiển thị giá trị ngay
                field.Evaluate((int)FieldEvaluationOptions.Automatic, db);
            }
        }
    }
}

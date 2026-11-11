// CA - Copy và Dịch Text sang Tiếng Anh
// Lệnh này cho phép chọn text trong AutoCAD, copy ra vị trí mới và dịch nội dung sang tiếng Anh
// Có tính năng preview text trước khi đặt
//
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(Civil3DCsharp.CA_CopyVaDichTiengAnh))]

namespace Civil3DCsharp
{
    /// <summary>
    /// Jig để preview MText khi di chuyển chuột
    /// </summary>
    public class MTextJig : EntityJig
    {
        private Point3d _position;
        private MText _mText;

        public MTextJig(MText mText) : base(mText)
        {
            _mText = mText;
            _position = mText.Location;
        }

        protected override SamplerStatus Sampler(JigPrompts prompts)
        {
            JigPromptPointOptions opts = new JigPromptPointOptions("\nChọn vị trí đặt text (preview): ");
            opts.UserInputControls = UserInputControls.Accept3dCoordinates;

            PromptPointResult result = prompts.AcquirePoint(opts);

            if (result.Status == PromptStatus.OK)
            {
                if (_position.DistanceTo(result.Value) < 0.001)
                    return SamplerStatus.NoChange;

                _position = result.Value;
                return SamplerStatus.OK;
            }

            return SamplerStatus.Cancel;
        }

        protected override bool Update()
        {
            _mText.Location = _position;
            return true;
        }

        public Point3d GetPosition()
        {
            return _position;
        }
    }

    /// <summary>
    /// Jig để preview DBText khi di chuyển chuột
    /// </summary>
    public class DBTextJig : EntityJig
    {
        private Point3d _position;
        private DBText _dbText;

        public DBTextJig(DBText dbText) : base(dbText)
        {
            _dbText = dbText;
            _position = dbText.Position;
        }

        protected override SamplerStatus Sampler(JigPrompts prompts)
        {
            JigPromptPointOptions opts = new JigPromptPointOptions("\nChọn vị trí đặt text (preview): ");
            opts.UserInputControls = UserInputControls.Accept3dCoordinates;

            PromptPointResult result = prompts.AcquirePoint(opts);

            if (result.Status == PromptStatus.OK)
            {
                if (_position.DistanceTo(result.Value) < 0.001)
                    return SamplerStatus.NoChange;

                _position = result.Value;
                return SamplerStatus.OK;
            }

            return SamplerStatus.Cancel;
        }

        protected override bool Update()
        {
            _dbText.Position = _position;
            return true;
        }

        public Point3d GetPosition()
        {
            return _position;
        }
    }

    public class CA_CopyVaDichTiengAnh
    {
        private static readonly HttpClient httpClient = new HttpClient();

        /// <summary>
        /// Strip formatting codes từ MText để lấy text thuần túy
        /// </summary>
        private static string StripMTextFormatting(string mTextContent)
        {
            if (string.IsNullOrEmpty(mTextContent))
                return string.Empty;

            string cleaned = mTextContent;

            // Loại bỏ color codes: {\C#;...} hoặc \C#;
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\\C\d+;", "");
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\{\\C\d+;([^}]*)\}", "$1");

            // Loại bỏ font codes: {\F...|...}
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\{\\[Ff][^;]*;([^}]*)\}", "$1");

            // Loại bỏ height codes: {\H...x;...}
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\{\\H[\d.]+x?;([^}]*)\}", "$1");

            // Loại bỏ width codes: {\W...;...}
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\{\\W[\d.]+;([^}]*)\}", "$1");

            // Loại bỏ underline, overline, strikethrough: {\L...}, {\O...}, {\K...}
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\{\\[LOKlok]([^}]*)\}", "$1");

            // Loại bỏ paragraph breaks: \P
            cleaned = cleaned.Replace("\\P", "\n");
            cleaned = cleaned.Replace("\\p", "\n");

            // Loại bỏ stacking: \S...^...;
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\\S([^^;]+)\^([^;]*);", "$1/$2");

            // Loại bỏ các dấu ngoặc nhọn thừa
            cleaned = cleaned.Replace("{", "").Replace("}", "");

            // Loại bỏ backslash đơn lẻ
            cleaned = cleaned.Replace("\\", "");

            return cleaned.Trim();
        }

        /// <summary>
        /// Dịch text sang tiếng Anh sử dụng Google Translate API (miễn phí)
        /// </summary>
        private static string TranslateToEnglish(string text)
        {
            try
            {
                // Sử dụng Google Translate API miễn phí
                string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=vi&tl=en&dt=t&q={Uri.EscapeDataString(text)}";

                var task = httpClient.GetStringAsync(url);
                task.Wait();
                string response = task.Result;

                // Parse JSON response để lấy kết quả dịch
                // Response format: [[["translated text","original text",null,null,x],...],null,"vi",...]
                StringBuilder translatedText = new StringBuilder();

                // Tìm vị trí bắt đầu của các phần dịch
                int startIndex = 0;
                while (true)
                {
                    // Tìm pattern [["text
                    int openBracket = response.IndexOf("[[\"", startIndex);
                    if (openBracket == -1 && startIndex == 0)
                    {
                        openBracket = response.IndexOf("[\"", startIndex);
                    }

                    if (openBracket == -1)
                        break;

                    int quoteStart = response.IndexOf("\"", openBracket) + 1;
                    int quoteEnd = response.IndexOf("\"", quoteStart);

                    if (quoteStart > 0 && quoteEnd > quoteStart)
                    {
                        string part = response.Substring(quoteStart, quoteEnd - quoteStart);
                        // Unescape JSON string
                        part = part.Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\");
                        translatedText.Append(part);
                    }

                    startIndex = quoteEnd + 1;

                    // Tìm đến phần dịch tiếp theo nếu có
                    int nextSection = response.IndexOf("],[\"", startIndex);
                    if (nextSection == -1)
                        break;
                    startIndex = nextSection;
                }

                string result = translatedText.ToString();
                return string.IsNullOrEmpty(result) ? text : result;
            }
            catch (System.Exception ex)
            {
                // Nếu không dịch được, trả về text gốc
                System.Diagnostics.Debug.WriteLine($"Translation error: {ex.Message}");
                return text;
            }
        }

        [CommandMethod("AT_Copy_Va_Dich_Tieng_Anh")]
        public static void CA()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;

            try
            {
                // Bước 1: Chọn text nguồn
                PromptEntityOptions sourcePrompt = new PromptEntityOptions("\nChọn text cần copy và dịch sang tiếng Anh: ");
                sourcePrompt.SetRejectMessage("\nĐối tượng phải là Text hoặc MText!");
                sourcePrompt.AddAllowedClass(typeof(DBText), true);
                sourcePrompt.AddAllowedClass(typeof(MText), true);

                PromptEntityResult sourceResult = ed.GetEntity(sourcePrompt);
                if (sourceResult.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\nĐã hủy lệnh.");
                    return;
                }

                // Lấy thông tin từ text nguồn
                string originalText = string.Empty;
                Point3d sourcePosition = Point3d.Origin;
                double textHeight = 2.5;
                double rotation = 0;
                ObjectId textStyleId = ObjectId.Null;
                bool isDBText = false;
                double widthFactor = 1.0;
                AttachmentPoint attachment = AttachmentPoint.MiddleLeft;
                double mTextWidth = 0;

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    Entity sourceEntity = tr.GetObject(sourceResult.ObjectId, OpenMode.ForRead) as Entity;

                    if (sourceEntity is DBText dbText)
                    {
                        isDBText = true;
                        originalText = dbText.TextString;
                        sourcePosition = dbText.Position;
                        textHeight = dbText.Height;
                        rotation = dbText.Rotation;
                        textStyleId = dbText.TextStyleId;
                        widthFactor = dbText.WidthFactor;
                    }
                    else if (sourceEntity is MText mText)
                    {
                        isDBText = false;
                        originalText = mText.Text;
                        if (string.IsNullOrEmpty(originalText))
                        {
                            originalText = StripMTextFormatting(mText.Contents);
                        }
                        sourcePosition = mText.Location;
                        textHeight = mText.TextHeight;
                        rotation = mText.Rotation;
                        textStyleId = mText.TextStyleId;
                        attachment = mText.Attachment;
                        mTextWidth = mText.Width;
                    }

                    tr.Commit();
                }

                if (string.IsNullOrEmpty(originalText))
                {
                    ed.WriteMessage("\nText không có nội dung!");
                    return;
                }

                ed.WriteMessage($"\nNội dung gốc: \"{originalText}\"");

                // Bước 2: Dịch sang tiếng Anh
                ed.WriteMessage("\nĐang dịch sang tiếng Anh...");
                string translatedText = TranslateToEnglish(originalText);
                ed.WriteMessage($"\nNội dung đã dịch: \"{translatedText}\"");

                // Bước 3: Sử dụng Jig để preview và chọn vị trí
                Point3d newPosition = Point3d.Origin;
                PromptResult jigResult;

                if (isDBText)
                {
                    // Tạo DBText preview
                    DBText previewText = new DBText();
                    previewText.Position = sourcePosition;
                    previewText.Height = textHeight;
                    previewText.Rotation = rotation;
                    previewText.TextString = translatedText;
                    previewText.WidthFactor = widthFactor;
                    if (!textStyleId.IsNull)
                    {
                        previewText.TextStyleId = textStyleId;
                    }

                    DBTextJig jig = new DBTextJig(previewText);
                    jigResult = ed.Drag(jig);

                    if (jigResult.Status == PromptStatus.OK)
                    {
                        newPosition = jig.GetPosition();

                        // Thêm text vào bản vẽ
                        using (Transaction tr = db.TransactionManager.StartTransaction())
                        {
                            BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                            BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                            previewText.Position = newPosition;
                            btr.AppendEntity(previewText);
                            tr.AddNewlyCreatedDBObject(previewText, true);

                            tr.Commit();
                        }
                    }
                    else
                    {
                        previewText.Dispose();
                        ed.WriteMessage("\nĐã hủy lệnh.");
                        return;
                    }
                }
                else
                {
                    // Tạo MText preview
                    MText previewText = new MText();
                    previewText.Location = sourcePosition;
                    previewText.TextHeight = textHeight;
                    previewText.Rotation = rotation;
                    previewText.Contents = translatedText;
                    previewText.Attachment = attachment;
                    if (mTextWidth > 0)
                    {
                        previewText.Width = mTextWidth;
                    }
                    if (!textStyleId.IsNull)
                    {
                        previewText.TextStyleId = textStyleId;
                    }

                    MTextJig jig = new MTextJig(previewText);
                    jigResult = ed.Drag(jig);

                    if (jigResult.Status == PromptStatus.OK)
                    {
                        newPosition = jig.GetPosition();

                        // Thêm text vào bản vẽ
                        using (Transaction tr = db.TransactionManager.StartTransaction())
                        {
                            BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                            BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                            previewText.Location = newPosition;
                            btr.AppendEntity(previewText);
                            tr.AddNewlyCreatedDBObject(previewText, true);

                            tr.Commit();
                        }
                    }
                    else
                    {
                        previewText.Dispose();
                        ed.WriteMessage("\nĐã hủy lệnh.");
                        return;
                    }
                }

                ed.WriteMessage("\n" + new string('=', 50));
                ed.WriteMessage($"\nHoàn thành! Text đã được copy và dịch sang tiếng Anh.");
                ed.WriteMessage($"\n- Nội dung gốc: \"{originalText}\"");
                ed.WriteMessage($"\n- Nội dung dịch: \"{translatedText}\"");
                ed.WriteMessage("\n" + new string('=', 50));
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\nLỗi: {ex.Message}");
            }
        }
    }
}

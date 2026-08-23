// Lệnh: CTS_BoSung_Coc_DataShortcut
// Bổ sung các cọc (SampleLine) mới phát sinh từ file nguồn Data Shortcut
// vào file tham chiếu hiện tại.
//
// Cơ chế hoạt động:
// 1. Tìm file nguồn DWG từ thư mục _Shortcuts của dự án Data Shortcut
// 2. Mở file nguồn dưới dạng side database (không cần mở bản vẽ)
// 3. So sánh danh sách cọc giữa file nguồn và file hiện tại (theo station)
// 4. Hiển thị form cho phép chọn cọc cần bổ sung
// 5. Tạo các cọc đã chọn vào nhóm cọc tương ứng
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Linq;

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.ApplicationServices;

using MyFirstProject.Extensions;
using MyFirstProject.Civil_Tool;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(Civil3DCsharp.CTS_BoSung_Coc_DataShortcut_Commands))]

namespace Civil3DCsharp
{
    public class CTS_BoSung_Coc_DataShortcut_Commands
    {
        // Tolerance khi so sánh station (mét) - 1cm
        private const double STATION_TOLERANCE = 0.01;

        [CommandMethod("CTS_BoSung_Coc_DataShortcut")]
        public static void CTSBoSungCocDataShortcut()
        {
            try
            {
                A.Ed.WriteMessage("\n=== CTS_BoSung_Coc_DataShortcut - Bổ Sung Cọc Từ Data Shortcut ===\n");

                // 1. Tìm file nguồn DWG
                string sourcePath = FindSourceDwgFile();
                if (string.IsNullOrEmpty(sourcePath))
                {
                    A.Ed.WriteMessage("\n❌ Không tìm được file nguồn. Hủy lệnh.");
                    return;
                }

                A.Ed.WriteMessage($"\n📁 File nguồn: {sourcePath}");

                // 2. Đọc dữ liệu cọc từ file nguồn
                A.Ed.WriteMessage("\n📖 Đang đọc dữ liệu cọc từ file nguồn...");
                var sourceData = ReadSourceSampleLines(sourcePath);
                if (sourceData.Count == 0)
                {
                    A.Ed.WriteMessage("\n⚠️ File nguồn không có alignment nào có cọc. Hủy lệnh.");
                    return;
                }

                A.Ed.WriteMessage($"\n   Tìm thấy {sourceData.Count} alignment(s) trong file nguồn.");

                // 3. Đọc dữ liệu cọc từ file hiện tại
                A.Ed.WriteMessage("\n📖 Đang đọc dữ liệu cọc từ file hiện tại...");
                var localData = GetLocalAlignmentData();
                if (localData.Count == 0)
                {
                    A.Ed.WriteMessage("\n⚠️ File hiện tại không có alignment nào có nhóm cọc. Hủy lệnh.");
                    return;
                }

                A.Ed.WriteMessage($"\n   Tìm thấy {localData.Count} alignment(s) trong file hiện tại.");

                // 4. So sánh và tìm cọc thiếu
                A.Ed.WriteMessage("\n🔍 Đang so sánh...");
                var compareResults = CompareAlignments(sourceData, localData);

                int totalMissing = compareResults.Sum(x => x.MissingSampleLines.Count);
                if (totalMissing == 0)
                {
                    A.Ed.WriteMessage("\n✅ Tất cả các cọc đã đồng bộ. Không có cọc nào cần bổ sung!");
                    A.Ok("✅ Tất cả các cọc đã đồng bộ. Không có cọc nào cần bổ sung!");
                    return;
                }

                A.Ed.WriteMessage($"\n   Tìm thấy {totalMissing} cọc cần bổ sung.");

                // 5. Hiển thị form
                var form = new BoSungCocDataShortcutForm(compareResults, sourcePath);
                var result = Application.ShowModalDialog(form);

                if (result != System.Windows.Forms.DialogResult.OK || !form.FormAccepted)
                {
                    A.Ed.WriteMessage("\n Đã hủy lệnh.");
                    return;
                }

                // 6. Tạo các cọc đã chọn
                var selectedData = form.GetSelectedData();
                CreateMissingSampleLines(selectedData);
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                A.Ed.WriteMessage($"\n❌ Lỗi AutoCAD: {ex.Message}");
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n❌ Lỗi: {ex.Message}");
                A.Ed.WriteMessage($"\n   Stack: {ex.StackTrace}");
            }
        }

        // ===================================================================
        // TÌM FILE NGUỒN DWG
        // ===================================================================

        /// <summary>
        /// Tìm file nguồn DWG từ thư mục _Shortcuts hoặc hỏi user chọn file.
        /// </summary>
        private static string FindSourceDwgFile()
        {
            // Phương án 1: Tìm từ thư mục _Shortcuts
            string autoDetected = TryAutoDetectSourceFromShortcuts();
            if (!string.IsNullOrEmpty(autoDetected) && File.Exists(autoDetected))
            {
                A.Ed.WriteMessage($"\n✅ Tự động phát hiện file nguồn từ Data Shortcuts.");
                return autoDetected;
            }

            // Phương án 2: Hỏi user chọn file
            A.Ed.WriteMessage("\n💡 Không thể tự động phát hiện file nguồn. Vui lòng chọn file DWG nguồn.");

            using (var openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Title = "Chọn file DWG nguồn (chứa cọc gốc)";
                openFileDialog.Filter = "AutoCAD Drawing (*.dwg)|*.dwg";
                openFileDialog.FilterIndex = 1;
                openFileDialog.RestoreDirectory = true;

                // Mặc định mở thư mục của bản vẽ hiện tại
                string currentDir = Path.GetDirectoryName(A.Db.Filename) ?? "";
                if (!string.IsNullOrEmpty(currentDir))
                {
                    openFileDialog.InitialDirectory = currentDir;
                }

                if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    return openFileDialog.FileName;
                }
            }

            return "";
        }

        /// <summary>
        /// Tìm file DWG nguồn từ thư mục _Shortcuts của dự án Civil 3D.
        /// Duyệt ngược từ thư mục chứa bản vẽ hiện tại lên trên để tìm _Shortcuts.
        /// </summary>
        private static string TryAutoDetectSourceFromShortcuts()
        {
            try
            {
                string drawingPath = A.Db.Filename;
                if (string.IsNullOrEmpty(drawingPath)) return "";

                string? currentDir = Path.GetDirectoryName(drawingPath);

                // Duyệt ngược lên trên tối đa 5 cấp thư mục để tìm _Shortcuts
                for (int depth = 0; depth < 5 && !string.IsNullOrEmpty(currentDir); depth++)
                {
                    string shortcutsFolder = Path.Combine(currentDir, "_Shortcuts");
                    if (Directory.Exists(shortcutsFolder))
                    {
                        A.Ed.WriteMessage($"\n   Tìm thấy thư mục _Shortcuts: {shortcutsFolder}");
                        return ParseShortcutXmlsForSource(shortcutsFolder, currentDir);
                    }
                    currentDir = Path.GetDirectoryName(currentDir);
                }
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n⚠️ Lỗi khi tìm _Shortcuts: {ex.Message}");
            }

            return "";
        }

        /// <summary>
        /// Phân tích các file XML trong _Shortcuts/Alignments để tìm đường dẫn file DWG nguồn.
        /// </summary>
        private static string ParseShortcutXmlsForSource(string shortcutsFolder, string projectFolder)
        {
            try
            {
                string alignmentsFolder = Path.Combine(shortcutsFolder, "Alignments");
                if (!Directory.Exists(alignmentsFolder))
                {
                    A.Ed.WriteMessage("\n   Không tìm thấy thư mục Alignments trong _Shortcuts.");
                    return "";
                }

                string[] xmlFiles = Directory.GetFiles(alignmentsFolder, "*.xml");
                if (xmlFiles.Length == 0)
                {
                    A.Ed.WriteMessage("\n   Không tìm thấy file XML nào trong _Shortcuts/Alignments.");
                    return "";
                }

                // Tìm SourceFile/SourceDrawing trong XML
                HashSet<string> sourcePaths = new(StringComparer.OrdinalIgnoreCase);

                foreach (string xmlFile in xmlFiles)
                {
                    try
                    {
                        XDocument doc = XDocument.Load(xmlFile);

                        // Tìm các element/attribute chứa đường dẫn file nguồn
                        // Hỗ trợ nhiều format XML khác nhau của Civil 3D
                        var sourceElements = doc.Descendants()
                            .Where(e => e.Name.LocalName.Equals("SourceFile", StringComparison.OrdinalIgnoreCase)
                                     || e.Name.LocalName.Equals("SourceDrawing", StringComparison.OrdinalIgnoreCase)
                                     || e.Name.LocalName.Equals("DwgPath", StringComparison.OrdinalIgnoreCase));

                        foreach (var elem in sourceElements)
                        {
                            string value = elem.Value?.Trim() ?? "";
                            if (!string.IsNullOrEmpty(value))
                            {
                                sourcePaths.Add(value);
                            }
                        }

                        // Cũng tìm trong attributes
                        var allElements = doc.Descendants();
                        foreach (var elem in allElements)
                        {
                            var attr = elem.Attribute("SourceFile")
                                    ?? elem.Attribute("SourceDrawing")
                                    ?? elem.Attribute("DwgPath");
                            if (attr != null && !string.IsNullOrWhiteSpace(attr.Value))
                            {
                                sourcePaths.Add(attr.Value.Trim());
                            }
                        }
                    }
                    catch
                    {
                        // Bỏ qua file XML lỗi
                    }
                }

                // Giải quyết đường dẫn tương đối
                foreach (string path in sourcePaths)
                {
                    string resolvedPath = path;

                    // Nếu là đường dẫn tương đối, giải quyết từ project folder
                    if (!Path.IsPathRooted(resolvedPath))
                    {
                        resolvedPath = Path.GetFullPath(Path.Combine(projectFolder, resolvedPath));
                    }

                    if (File.Exists(resolvedPath) && resolvedPath.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase))
                    {
                        A.Ed.WriteMessage($"\n   Phát hiện file nguồn: {resolvedPath}");
                        return resolvedPath;
                    }
                }
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n⚠️ Lỗi khi phân tích XML: {ex.Message}");
            }

            return "";
        }

        // ===================================================================
        // ĐỌC DỮ LIỆU CỌC TỪ FILE NGUỒN (SIDE DATABASE)
        // ===================================================================

        /// <summary>
        /// Đọc tất cả alignment và sample lines từ file DWG nguồn bằng side database.
        /// Returns: Dictionary (AlignmentName → List of (SampleLineName, Station))
        /// </summary>
        private static Dictionary<string, List<(string name, double station)>> ReadSourceSampleLines(string sourcePath)
        {
            var result = new Dictionary<string, List<(string name, double station)>>(StringComparer.OrdinalIgnoreCase);

            try
            {
                // Mở file nguồn dưới dạng side database (chỉ đọc, không khóa file)
                using (Database sourceDb = new Database(false, false))
                {
                    sourceDb.ReadDwgFile(sourcePath, FileOpenMode.OpenForReadAndAllShare, true, "");

                    using (Transaction tr = sourceDb.TransactionManager.StartTransaction())
                    {
                        // Lấy CivilDocument từ side database
                        CivilDocument civDoc = CivilDocument.GetCivilDocument(sourceDb);
                        ObjectIdCollection alignmentIds = civDoc.GetAlignmentIds();

                        foreach (ObjectId alignId in alignmentIds)
                        {
                            try
                            {
                                Alignment? alignment = tr.GetObject(alignId, OpenMode.ForRead) as Alignment;
                                if (alignment == null) continue;

                                string alignName = alignment.Name;
                                var sampleLines = new List<(string name, double station)>();

                                // Duyệt qua tất cả Sample Line Groups
                                ObjectIdCollection groupIds = alignment.GetSampleLineGroupIds();
                                foreach (ObjectId groupId in groupIds)
                                {
                                    SampleLineGroup? group = tr.GetObject(groupId, OpenMode.ForRead) as SampleLineGroup;
                                    if (group == null) continue;

                                    ObjectIdCollection slIds = group.GetSampleLineIds();
                                    foreach (ObjectId slId in slIds)
                                    {
                                        SampleLine? sl = tr.GetObject(slId, OpenMode.ForRead) as SampleLine;
                                        if (sl == null) continue;

                                        sampleLines.Add((sl.Name, sl.Station));
                                    }
                                }

                                if (sampleLines.Count > 0)
                                {
                                    // Loại bỏ trùng lặp (nếu có nhiều group cùng tên SL)
                                    var uniqueSL = sampleLines
                                        .GroupBy(x => Math.Round(x.station, 2))
                                        .Select(g => g.First())
                                        .OrderBy(x => x.station)
                                        .ToList();

                                    result[alignName] = uniqueSL;
                                    A.Ed.WriteMessage($"\n   📋 {alignName}: {uniqueSL.Count} cọc");
                                }
                            }
                            catch (System.Exception ex)
                            {
                                A.Ed.WriteMessage($"\n   ⚠️ Lỗi khi đọc alignment: {ex.Message}");
                            }
                        }

                        tr.Commit();
                    }
                }
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n❌ Lỗi khi đọc file nguồn: {ex.Message}");
                A.Ed.WriteMessage($"\n   Đường dẫn: {sourcePath}");
            }

            return result;
        }

        // ===================================================================
        // ĐỌC DỮ LIỆU CỌC TỪ BẢN VẼ HIỆN TẠI
        // ===================================================================

        /// <summary>
        /// Đọc tất cả alignment và sample lines từ bản vẽ hiện tại.
        /// Returns: Dictionary (AlignmentName → (AlignmentId, GroupId, GroupName, SampleLines))
        /// </summary>
        private static Dictionary<string, LocalAlignmentInfo> GetLocalAlignmentData()
        {
            var result = new Dictionary<string, LocalAlignmentInfo>(StringComparer.OrdinalIgnoreCase);

            using (Transaction tr = A.Db.TransactionManager.StartTransaction())
            {
                try
                {
                    ObjectIdCollection alignmentIds = A.Cdoc.GetAlignmentIds();

                    foreach (ObjectId alignId in alignmentIds)
                    {
                        Alignment? alignment = tr.GetObject(alignId, OpenMode.ForWrite) as Alignment;
                        if (alignment == null) continue;

                        string alignName = alignment.Name;
                        ObjectIdCollection groupIds = alignment.GetSampleLineGroupIds();

                        if (groupIds.Count == 0) continue;

                        // Lấy nhóm cọc đầu tiên làm nhóm đích (target)
                        ObjectId targetGroupId = groupIds[0];
                        SampleLineGroup? targetGroup = tr.GetObject(targetGroupId, OpenMode.ForWrite) as SampleLineGroup;
                        if (targetGroup == null) continue;

                        // Đọc tất cả cọc từ TẤT CẢ groups (để so sánh đầy đủ)
                        var localSampleLines = new List<(string name, double station)>();
                        foreach (ObjectId gid in groupIds)
                        {
                            SampleLineGroup? grp = tr.GetObject(gid, OpenMode.ForWrite) as SampleLineGroup;
                            if (grp == null) continue;

                            foreach (ObjectId slId in grp.GetSampleLineIds())
                            {
                                SampleLine? sl = tr.GetObject(slId, OpenMode.ForWrite) as SampleLine;
                                if (sl == null) continue;

                                localSampleLines.Add((sl.Name, sl.Station));
                            }
                        }

                        // Loại bỏ trùng lặp theo station
                        var uniqueStations = new HashSet<double>();
                        var uniqueSL = new List<(string name, double station)>();
                        foreach (var sl in localSampleLines.OrderBy(x => x.station))
                        {
                            double rounded = Math.Round(sl.station, 2);
                            if (uniqueStations.Add(rounded))
                            {
                                uniqueSL.Add(sl);
                            }
                        }

                        result[alignName] = new LocalAlignmentInfo
                        {
                            AlignmentId = alignId,
                            TargetGroupId = targetGroupId,
                            TargetGroupName = targetGroup.Name,
                            SampleLines = uniqueSL,
                            StationSet = uniqueStations
                        };

                        A.Ed.WriteMessage($"\n   📋 {alignName}: {uniqueSL.Count} cọc (nhóm: {targetGroup.Name})");
                    }

                    tr.Commit();
                }
                catch (System.Exception ex)
                {
                    A.Ed.WriteMessage($"\n❌ Lỗi khi đọc file hiện tại: {ex.Message}");
                }
            }

            return result;
        }

        // ===================================================================
        // SO SÁNH VÀ TÌM CỌC THIẾU
        // ===================================================================

        /// <summary>
        /// So sánh cọc giữa file nguồn và file hiện tại.
        /// Tìm các cọc có trong nguồn nhưng không có trong file hiện tại (theo station).
        /// </summary>
        private static List<AlignmentCompareData> CompareAlignments(
            Dictionary<string, List<(string name, double station)>> sourceData,
            Dictionary<string, LocalAlignmentInfo> localData)
        {
            var results = new List<AlignmentCompareData>();

            foreach (var sourceKvp in sourceData)
            {
                string alignName = sourceKvp.Key;
                var sourceSL = sourceKvp.Value;

                // Tìm alignment tương ứng trong file hiện tại
                if (!localData.TryGetValue(alignName, out var localInfo))
                {
                    A.Ed.WriteMessage($"\n   ⚠️ Alignment '{alignName}' không tồn tại trong file hiện tại. Bỏ qua.");
                    continue;
                }

                // Tìm cọc có trong nguồn nhưng KHÔNG có trong local (so sánh theo station)
                var missingSL = new List<SampleLineToAdd>();
                foreach (var (name, station) in sourceSL)
                {
                    bool existsLocally = localInfo.StationSet
                        .Any(localStation => Math.Abs(localStation - Math.Round(station, 2)) < STATION_TOLERANCE);

                    if (!existsLocally)
                    {
                        missingSL.Add(new SampleLineToAdd
                        {
                            Name = name,
                            Station = station,
                            IsSelected = true
                        });
                    }
                }

                if (missingSL.Count > 0)
                {
                    results.Add(new AlignmentCompareData
                    {
                        AlignmentName = alignName,
                        AlignmentId = localInfo.AlignmentId,
                        TargetGroupId = localInfo.TargetGroupId,
                        TargetGroupName = localInfo.TargetGroupName,
                        MissingSampleLines = missingSL.OrderBy(x => x.Station).ToList()
                    });

                    A.Ed.WriteMessage($"\n   🔸 {alignName}: {missingSL.Count} cọc cần bổ sung");
                }
                else
                {
                    A.Ed.WriteMessage($"\n   ✅ {alignName}: đã đồng bộ");
                }
            }

            return results;
        }

        // ===================================================================
        // TẠO CỌC MỚI
        // ===================================================================

        /// <summary>
        /// Tạo các cọc (SampleLine) đã chọn vào nhóm cọc tương ứng.
        /// </summary>
        private static void CreateMissingSampleLines(List<AlignmentCompareData> selectedData)
        {
            int totalCreated = 0;
            int totalFailed = 0;

            A.Ed.WriteMessage("\n\n=== ĐANG TẠO CỌC BỔ SUNG ===");

            using (Transaction tr = A.Db.TransactionManager.StartTransaction())
            {
                try
                {
                    foreach (var alignData in selectedData)
                    {
                        Alignment? alignment = tr.GetObject(alignData.AlignmentId, OpenMode.ForWrite) as Alignment;
                        if (alignment == null)
                        {
                            A.Ed.WriteMessage($"\n❌ Không thể mở alignment '{alignData.AlignmentName}'");
                            continue;
                        }

                        // Lấy danh sách tên đã tồn tại để tránh trùng
                        SampleLineGroup? targetGroup = tr.GetObject(alignData.TargetGroupId, OpenMode.ForWrite) as SampleLineGroup;
                        HashSet<string> existingNames = new(StringComparer.OrdinalIgnoreCase);
                        if (targetGroup != null)
                        {
                            foreach (ObjectId slId in targetGroup.GetSampleLineIds())
                            {
                                SampleLine? existingSL = tr.GetObject(slId, OpenMode.ForRead) as SampleLine;
                                if (existingSL != null)
                                {
                                    existingNames.Add(existingSL.Name);
                                }
                            }
                        }

                        A.Ed.WriteMessage($"\n\n🛣️ {alignData.AlignmentName} (nhóm: {alignData.TargetGroupName}):");

                        foreach (var sl in alignData.MissingSampleLines)
                        {
                            try
                            {
                                // Kiểm tra station hợp lệ
                                if (sl.Station < alignment.StartingStation || sl.Station > alignment.EndingStation)
                                {
                                    A.Ed.WriteMessage($"\n   ⚠️ {sl.Name}: Station {sl.Station:F2} nằm ngoài phạm vi. Bỏ qua.");
                                    totalFailed++;
                                    continue;
                                }

                                // Xử lý tên trùng: thêm suffix nếu cần
                                string finalName = sl.Name;
                                if (existingNames.Contains(finalName))
                                {
                                    // Tên đã tồn tại → dùng tên tạm, sau đó sẽ rename
                                    finalName = $"{sl.Name}_ds";
                                    int suffix = 1;
                                    while (existingNames.Contains(finalName))
                                    {
                                        finalName = $"{sl.Name}_ds{suffix}";
                                        suffix++;
                                    }
                                }

                                // Tạo SampleLine với tên tạm (tránh lỗi trùng tên API)
                                string tempName = $"z_{Guid.NewGuid():N}";
                                ObjectId newSLId = UtilitiesC3D.CreateSampleline(tempName, alignData.TargetGroupId, alignment, sl.Station);

                                if (newSLId != ObjectId.Null && newSLId.IsValid)
                                {
                                    // Rename sang tên thật
                                    using (Transaction trRename = A.Db.TransactionManager.StartTransaction())
                                    {
                                        SampleLine? newSL = trRename.GetObject(newSLId, OpenMode.ForWrite) as SampleLine;
                                        if (newSL != null)
                                        {
                                            try
                                            {
                                                newSL.Name = finalName;
                                            }
                                            catch
                                            {
                                                // Nếu rename thất bại, giữ tên tạm
                                                A.Ed.WriteMessage($"\n   ⚠️ Không thể đặt tên '{finalName}', giữ tên tạm.");
                                            }
                                        }
                                        trRename.Commit();
                                    }

                                    existingNames.Add(finalName);
                                    totalCreated++;
                                    A.Ed.WriteMessage($"\n   ✅ {finalName} tại Km{sl.Station / 1000:F3} (Station: {sl.Station:F2})");
                                }
                                else
                                {
                                    totalFailed++;
                                    A.Ed.WriteMessage($"\n   ❌ Không thể tạo: {sl.Name}");
                                }
                            }
                            catch (System.Exception ex)
                            {
                                totalFailed++;
                                A.Ed.WriteMessage($"\n   ❌ Lỗi khi tạo '{sl.Name}': {ex.Message}");
                            }
                        }
                    }

                    tr.Commit();
                }
                catch (System.Exception ex)
                {
                    A.Ed.WriteMessage($"\n❌ Lỗi: {ex.Message}");
                }
            }

            // Thông báo kết quả
            A.Ed.WriteMessage("\n\n╔══════════════════════════════════════════════════╗");
            A.Ed.WriteMessage("\n║             KẾT QUẢ BỔ SUNG CỌC                ║");
            A.Ed.WriteMessage("\n╠══════════════════════════════════════════════════╣");
            A.Ed.WriteMessage($"\n║  ✅ Thành công: {totalCreated,-32}║");
            if (totalFailed > 0)
                A.Ed.WriteMessage($"\n║  ❌ Thất bại:   {totalFailed,-32}║");
            A.Ed.WriteMessage("\n╚══════════════════════════════════════════════════╝");

            A.Ed.WriteMessage($"\n\n✅ Lệnh CTS_BoSung_Coc_DataShortcut hoàn thành!");
        }

        // ===================================================================
        // DATA CLASS
        // ===================================================================

        /// <summary>
        /// Thông tin alignment trong file hiện tại (local)
        /// </summary>
        private class LocalAlignmentInfo
        {
            public ObjectId AlignmentId { get; set; }
            public ObjectId TargetGroupId { get; set; }
            public string TargetGroupName { get; set; } = "";
            public List<(string name, double station)> SampleLines { get; set; } = new();
            public HashSet<double> StationSet { get; set; } = new();
        }
    }
}

// Lệnh: CTS_BoSung_Coc_DataShortcut_Ref (Phương án A)
// Bổ sung cọc tham chiếu thiếu trong Sample Line Groups đã tồn tại
//
// Vấn đề: SLG đã được tham chiếu qua Data Shortcuts, nhưng khi file gốc
// thêm cọc mới vào SLG, file tham chiếu không tự cập nhật.
//
// Giải pháp: Gọi DataShortcuts.CreateReference() lại cho SLG để cập nhật
// danh sách cọc (tương tự thao tác thủ công: Data Shortcuts → SLG → 
// Create Reference → chọn tất cả cọc → OK)
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
using Autodesk.Civil.DataShortcuts;

using MyFirstProject.Extensions;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(Civil3DCsharp.CTS_BoSung_Coc_DataShortcut_Ref_Commands))]

namespace Civil3DCsharp
{
    public class CTS_BoSung_Coc_DataShortcut_Ref_Commands
    {
        private const double STATION_TOLERANCE = 0.01;

        [CommandMethod("CTS_BoSung_Coc_DataShortcut_Ref")]
        public static void CTSBoSungCocDataShortcutRef()
        {
            try
            {
                A.Ed.WriteMessage("\n=== CTS_BoSung_Coc_DataShortcut_Ref ===");
                A.Ed.WriteMessage("\n📌 Phương án A: Cập nhật tham chiếu cọc trong SLG đã tồn tại");

                // 1. Tìm file nguồn DWG
                string sourcePath = FindSourceDwgFile();
                if (string.IsNullOrEmpty(sourcePath))
                {
                    A.Ed.WriteMessage("\n❌ Không tìm được file nguồn. Hủy lệnh.");
                    return;
                }

                A.Ed.WriteMessage($"\n📁 File nguồn: {Path.GetFileName(sourcePath)}");

                // 2. Đọc cọc từ file nguồn (theo từng SLG)
                A.Ed.WriteMessage("\n📖 Đang đọc dữ liệu từ file nguồn...");
                var sourceData = ReadSourceSLGDetails(sourcePath);
                if (sourceData.Count == 0)
                {
                    A.Ed.WriteMessage("\n⚠️ File nguồn không có SLG nào. Hủy lệnh.");
                    return;
                }

                // 3. Đọc cọc từ file hiện tại (theo từng SLG)
                A.Ed.WriteMessage("\n📖 Đang đọc dữ liệu từ file hiện tại...");
                var localData = GetLocalSLGDetails();
                if (localData.Count == 0)
                {
                    A.Ed.WriteMessage("\n⚠️ File hiện tại không có SLG nào. Hủy lệnh.");
                    return;
                }

                // 4. So sánh cọc trong từng SLG
                A.Ed.WriteMessage("\n🔍 Đang so sánh cọc trong từng nhóm...\n");
                var comparison = CompareSLGDetails(sourceData, localData);

                if (comparison.Count == 0)
                {
                    A.Ed.WriteMessage("\n✅ Tất cả các cọc đã đồng bộ! Không cần bổ sung.");
                    MessageBox.Show("Tất cả các cọc trong tất cả SLG đã đồng bộ.\nKhông cần bổ sung!",
                        "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                int totalMissing = comparison.Sum(x => x.MissingSampleLines.Count);
                A.Ed.WriteMessage($"\n   🔸 Tổng cộng: {totalMissing} cọc thiếu trong {comparison.Count} nhóm");

                // 5. Hiển thị kết quả gọn gàng và hỏi user
                string msg = $"Tìm thấy {totalMissing} cọc thiếu trong {comparison.Count} nhóm:\n\n";
                foreach (var c in comparison)
                {
                    msg += $"  • {c.AlignmentName} → {c.GroupName}: +{c.MissingSampleLines.Count} cọc\n";
                }
                msg += $"\nBổ sung tất cả?";

                var dialogResult = MessageBox.Show(msg, "BỔ SUNG CỌC THAM CHIẾU",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (dialogResult != System.Windows.Forms.DialogResult.Yes)
                {
                    A.Ed.WriteMessage("\n Đã hủy lệnh.");
                    return;
                }

                // 6. Gọi CreateReference cho từng SLG cần cập nhật
                int successCount = 0;
                int failCount = 0;

                foreach (var comp in comparison)
                {
                    try
                    {
                        A.Ed.WriteMessage($"\n   🔄 Cập nhật: {comp.AlignmentName} → {comp.GroupName} ({comp.MissingSampleLines.Count} cọc thiếu)...");

                        ObjectIdCollection refIds = Autodesk.Civil.DataShortcuts.DataShortcuts.CreateReference(
                            A.Db,
                            comp.SourceDwgPath,
                            comp.GroupName,
                            DataShortcutEntityType.SampleLineGroup
                        );

                        if (refIds != null && refIds.Count > 0)
                        {
                            successCount++;
                            A.Ed.WriteMessage($" ✅ OK ({refIds.Count} đối tượng)");
                        }
                        else
                        {
                            failCount++;
                            A.Ed.WriteMessage($" ⚠️ Không có đối tượng mới");
                        }
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception ex) when (ex.Message.Contains("already") || ex.Message.Contains("exist"))
                    {
                        // SLG reference đã tồn tại - thử cách khác
                        A.Ed.WriteMessage($"\n   ⚠️ Reference đã tồn tại. Thử bổ sung cọc local...");
                        int added = FallbackAddLocalSampleLines(comp);
                        if (added > 0)
                        {
                            successCount++;
                            A.Ed.WriteMessage($" ✅ Đã bổ sung {added} cọc local");
                        }
                        else
                        {
                            failCount++;
                            A.Ed.WriteMessage($" ❌ Không thể bổ sung");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        // Fallback: tạo cọc local (Phương án B)
                        A.Ed.WriteMessage($"\n   ⚠️ CreateReference lỗi: {ex.Message}");
                        A.Ed.WriteMessage($"\n   🔄 Chuyển sang bổ sung cọc local (Phương án B)...");
                        int added = FallbackAddLocalSampleLines(comp);
                        if (added > 0)
                        {
                            successCount++;
                            A.Ed.WriteMessage($" ✅ Đã bổ sung {added} cọc local");
                        }
                        else
                        {
                            failCount++;
                            A.Ed.WriteMessage($" ❌ Không thể bổ sung: {ex.Message}");
                        }
                    }
                }

                // 7. Kết quả
                A.Ed.WriteMessage("\n\n╔══════════════════════════════════════════════════╗");
                A.Ed.WriteMessage("\n║        KẾT QUẢ BỔ SUNG CỌC THAM CHIẾU          ║");
                A.Ed.WriteMessage("\n╠══════════════════════════════════════════════════╣");
                A.Ed.WriteMessage($"\n║  ✅ Thành công: {successCount,-32}║");
                if (failCount > 0)
                    A.Ed.WriteMessage($"\n║  ❌ Thất bại:   {failCount,-32}║");
                A.Ed.WriteMessage("\n╚══════════════════════════════════════════════════╝");
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n❌ Lỗi: {ex.Message}");
                A.Ed.WriteMessage($"\n   Stack: {ex.StackTrace}");
            }
        }

        // ===================================================================
        // DATA CLASSES
        // ===================================================================

        private class SLGDetail
        {
            public string AlignmentName { get; set; } = "";
            public string GroupName { get; set; } = "";
            public string SourceDwgPath { get; set; } = "";
            public ObjectId AlignmentId { get; set; } = ObjectId.Null;
            public ObjectId GroupId { get; set; } = ObjectId.Null;
            public List<SLInfo> SampleLines { get; set; } = new();
        }

        private class SLInfo
        {
            public string Name { get; set; } = "";
            public double Station { get; set; }
        }

        private class SLGCompareResult
        {
            public string AlignmentName { get; set; } = "";
            public string GroupName { get; set; } = "";
            public string SourceDwgPath { get; set; } = "";
            public ObjectId AlignmentId { get; set; } = ObjectId.Null;
            public ObjectId GroupId { get; set; } = ObjectId.Null;
            public List<SLInfo> MissingSampleLines { get; set; } = new();
        }

        // ===================================================================
        // TÌM FILE NGUỒN
        // ===================================================================

        private static string FindSourceDwgFile()
        {
            // Thử auto-detect từ _Shortcuts
            string? currentDir = Path.GetDirectoryName(A.Db.Filename);
            for (int depth = 0; depth < 5 && !string.IsNullOrEmpty(currentDir); depth++)
            {
                string shortcutsFolder = Path.Combine(currentDir, "_Shortcuts");
                if (Directory.Exists(shortcutsFolder))
                {
                    string detected = TryParseSourceFromXml(shortcutsFolder, currentDir);
                    if (!string.IsNullOrEmpty(detected) && File.Exists(detected))
                    {
                        A.Ed.WriteMessage($"\n✅ Tự động phát hiện file nguồn từ _Shortcuts.");
                        return detected;
                    }
                }
                currentDir = Path.GetDirectoryName(currentDir);
            }

            // Fallback: hỏi user chọn file
            A.Ed.WriteMessage("\n💡 Không thể tự động phát hiện. Chọn file DWG nguồn.");
            using (var ofd = new OpenFileDialog())
            {
                ofd.Title = "Chọn file DWG nguồn (chứa Sample Line Groups gốc)";
                ofd.Filter = "AutoCAD Drawing (*.dwg)|*.dwg";
                ofd.InitialDirectory = Path.GetDirectoryName(A.Db.Filename) ?? "";
                if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    return ofd.FileName;
            }
            return "";
        }

        private static string TryParseSourceFromXml(string shortcutsFolder, string projectFolder)
        {
            try
            {
                // Tìm XML files chứa reference đến file DWG
                string[] xmlFiles = Directory.GetFiles(shortcutsFolder, "*.xml", SearchOption.AllDirectories);
                foreach (string xmlFile in xmlFiles)
                {
                    try
                    {
                        XDocument doc = XDocument.Load(xmlFile);
                        foreach (var elem in doc.Descendants())
                        {
                            // Tìm SourceFile element/attribute
                            string? sourcePath = null;
                            if (elem.Name.LocalName.Equals("SourceFile", StringComparison.OrdinalIgnoreCase) ||
                                elem.Name.LocalName.Equals("SourceDrawing", StringComparison.OrdinalIgnoreCase) ||
                                elem.Name.LocalName.Equals("DwgPath", StringComparison.OrdinalIgnoreCase))
                            {
                                sourcePath = elem.Value?.Trim();
                            }
                            var attr = elem.Attribute("SourceFile") ?? elem.Attribute("SourceDrawing") ?? elem.Attribute("DwgPath");
                            if (attr != null) sourcePath = attr.Value?.Trim();

                            if (!string.IsNullOrEmpty(sourcePath) && sourcePath.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase))
                            {
                                if (!Path.IsPathRooted(sourcePath))
                                    sourcePath = Path.GetFullPath(Path.Combine(projectFolder, sourcePath));
                                if (File.Exists(sourcePath)) return sourcePath;
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return "";
        }

        // ===================================================================
        // ĐỌC CHI TIẾT CỌC TỪ FILE NGUỒN
        // ===================================================================

        /// <summary>
        /// Đọc tất cả SLG và cọc bên trong từ file DWG nguồn
        /// Key = "AlignmentName|GroupName"
        /// </summary>
        private static Dictionary<string, SLGDetail> ReadSourceSLGDetails(string sourcePath)
        {
            var result = new Dictionary<string, SLGDetail>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using (Database sourceDb = new Database(false, false))
                {
                    sourceDb.ReadDwgFile(sourcePath, FileOpenMode.OpenForReadAndAllShare, true, "");

                    using (Transaction tr = sourceDb.TransactionManager.StartTransaction())
                    {
                        CivilDocument civDoc = CivilDocument.GetCivilDocument(sourceDb);
                        ObjectIdCollection alignmentIds = civDoc.GetAlignmentIds();

                        foreach (ObjectId alignId in alignmentIds)
                        {
                            try
                            {
                                Alignment? alignment = tr.GetObject(alignId, OpenMode.ForRead) as Alignment;
                                if (alignment == null) continue;

                                foreach (ObjectId groupId in alignment.GetSampleLineGroupIds())
                                {
                                    SampleLineGroup? group = tr.GetObject(groupId, OpenMode.ForRead) as SampleLineGroup;
                                    if (group == null) continue;

                                    var detail = new SLGDetail
                                    {
                                        AlignmentName = alignment.Name,
                                        GroupName = group.Name,
                                        SourceDwgPath = sourcePath
                                    };

                                    foreach (ObjectId slId in group.GetSampleLineIds())
                                    {
                                        SampleLine? sl = tr.GetObject(slId, OpenMode.ForRead) as SampleLine;
                                        if (sl == null) continue;
                                        detail.SampleLines.Add(new SLInfo { Name = sl.Name, Station = sl.Station });
                                    }

                                    string key = $"{alignment.Name}|{group.Name}";
                                    result[key] = detail;

                                    A.Ed.WriteMessage($"\n   📋 [Nguồn] {alignment.Name} → {group.Name}: {detail.SampleLines.Count} cọc");
                                }
                            }
                            catch { }
                        }

                        tr.Commit();
                    }
                }
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n❌ Lỗi đọc file nguồn: {ex.Message}");
            }

            return result;
        }

        // ===================================================================
        // ĐỌC CHI TIẾT CỌC TỪ FILE HIỆN TẠI
        // ===================================================================

        private static Dictionary<string, SLGDetail> GetLocalSLGDetails()
        {
            var result = new Dictionary<string, SLGDetail>(StringComparer.OrdinalIgnoreCase);

            using (Transaction tr = A.Db.TransactionManager.StartTransaction())
            {
                try
                {
                    foreach (ObjectId alignId in A.Cdoc.GetAlignmentIds())
                    {
                        Alignment? alignment = tr.GetObject(alignId, OpenMode.ForRead) as Alignment;
                        if (alignment == null) continue;

                        foreach (ObjectId groupId in alignment.GetSampleLineGroupIds())
                        {
                            SampleLineGroup? group = tr.GetObject(groupId, OpenMode.ForRead) as SampleLineGroup;
                            if (group == null) continue;

                            var detail = new SLGDetail
                            {
                                AlignmentName = alignment.Name,
                                GroupName = group.Name,
                                AlignmentId = alignId,
                                GroupId = groupId
                            };

                            foreach (ObjectId slId in group.GetSampleLineIds())
                            {
                                SampleLine? sl = tr.GetObject(slId, OpenMode.ForRead) as SampleLine;
                                if (sl == null) continue;
                                detail.SampleLines.Add(new SLInfo { Name = sl.Name, Station = sl.Station });
                            }

                            string key = $"{alignment.Name}|{group.Name}";
                            result[key] = detail;

                            A.Ed.WriteMessage($"\n   📋 [Local] {alignment.Name} → {group.Name}: {detail.SampleLines.Count} cọc");
                        }
                    }
                }
                catch { }
                tr.Commit();
            }

            return result;
        }

        // ===================================================================
        // SO SÁNH CỌC TRONG TỪNG SLG
        // ===================================================================

        private static List<SLGCompareResult> CompareSLGDetails(
            Dictionary<string, SLGDetail> sourceData,
            Dictionary<string, SLGDetail> localData)
        {
            var results = new List<SLGCompareResult>();

            foreach (var kvp in sourceData)
            {
                string key = kvp.Key;
                var source = kvp.Value;

                if (!localData.TryGetValue(key, out var local))
                {
                    A.Ed.WriteMessage($"\n   ⚠️ SLG '{source.GroupName}' (Alignment: {source.AlignmentName}) không tồn tại trong file hiện tại");
                    continue;
                }

                // So sánh cọc theo station
                var localStations = new HashSet<double>(
                    local.SampleLines.Select(s => Math.Round(s.Station, 2)));

                var missing = new List<SLInfo>();
                foreach (var sl in source.SampleLines)
                {
                    double rounded = Math.Round(sl.Station, 2);
                    bool existsLocally = localStations.Any(ls => Math.Abs(ls - rounded) < STATION_TOLERANCE);
                    if (!existsLocally)
                    {
                        missing.Add(sl);
                    }
                }

                if (missing.Count > 0)
                {
                    A.Ed.WriteMessage($"\n   🔸 {source.AlignmentName} → {source.GroupName}: nguồn={source.SampleLines.Count}, local={local.SampleLines.Count}, thiếu={missing.Count}");

                    results.Add(new SLGCompareResult
                    {
                        AlignmentName = source.AlignmentName,
                        GroupName = source.GroupName,
                        SourceDwgPath = source.SourceDwgPath,
                        AlignmentId = local.AlignmentId,
                        GroupId = local.GroupId,
                        MissingSampleLines = missing.OrderBy(x => x.Station).ToList()
                    });
                }
                else
                {
                    A.Ed.WriteMessage($"\n   ✅ {source.AlignmentName} → {source.GroupName}: đồng bộ ({local.SampleLines.Count} cọc)");
                }
            }

            return results;
        }

        // ===================================================================
        // FALLBACK: TẠO CỌC LOCAL (PHƯƠNG ÁN B)
        // ===================================================================

        /// <summary>
        /// Nếu CreateReference thất bại, fallback sang tạo cọc local
        /// bằng UtilitiesC3D.CreateSampleline()
        /// </summary>
        private static int FallbackAddLocalSampleLines(SLGCompareResult comp)
        {
            int added = 0;

            try
            {
                using (Transaction tr = A.Db.TransactionManager.StartTransaction())
                {
                    Alignment? alignment = tr.GetObject(comp.AlignmentId, OpenMode.ForWrite) as Alignment;
                    if (alignment == null) return 0;

                    // Lấy tên đã tồn tại để tránh trùng
                    SampleLineGroup? group = tr.GetObject(comp.GroupId, OpenMode.ForWrite) as SampleLineGroup;
                    HashSet<string> existingNames = new(StringComparer.OrdinalIgnoreCase);
                    if (group != null)
                    {
                        foreach (ObjectId slId in group.GetSampleLineIds())
                        {
                            SampleLine? existingSL = tr.GetObject(slId, OpenMode.ForRead) as SampleLine;
                            if (existingSL != null)
                                existingNames.Add(existingSL.Name);
                        }
                    }

                    foreach (var sl in comp.MissingSampleLines)
                    {
                        try
                        {
                            if (sl.Station < alignment.StartingStation || sl.Station > alignment.EndingStation)
                                continue;

                            string finalName = sl.Name;
                            if (existingNames.Contains(finalName))
                            {
                                finalName = $"{sl.Name}_ds";
                                int suffix = 1;
                                while (existingNames.Contains(finalName))
                                {
                                    finalName = $"{sl.Name}_ds{suffix++}";
                                }
                            }

                            string tempName = $"z_{Guid.NewGuid():N}";
                            ObjectId newSLId = UtilitiesC3D.CreateSampleline(tempName, comp.GroupId, alignment, sl.Station);

                            if (newSLId != ObjectId.Null && newSLId.IsValid)
                            {
                                using (Transaction trRename = A.Db.TransactionManager.StartTransaction())
                                {
                                    SampleLine? newSL = trRename.GetObject(newSLId, OpenMode.ForWrite) as SampleLine;
                                    if (newSL != null)
                                    {
                                        try { newSL.Name = finalName; } catch { }
                                    }
                                    trRename.Commit();
                                }
                                existingNames.Add(finalName);
                                added++;
                            }
                        }
                        catch { }
                    }

                    tr.Commit();
                }
            }
            catch { }

            return added;
        }
    }
}

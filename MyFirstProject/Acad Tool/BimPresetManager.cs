// (C) Copyright 2026 by T27
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ClosedXML.Excel;

namespace Civil3DCsharp
{
    /// <summary>
    /// Trình quản lý lưu trữ, nạp, xuất/nhập Excel và khôi phục Bảng Mẫu Màu Chuẩn BIM / EIR (BEP T27)
    /// Dữ liệu được lưu trữ lâu dài dạng JSON trong %APPDATA%\Civil3D_Tools\BimStandardPresets.json
    /// </summary>
    public static class BimPresetManager
    {
        private static readonly string SettingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Civil3D_Tools"
        );

        private static readonly string PresetsFilePath = Path.Combine(SettingsDirectory, "BimStandardPresets.json");

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Đường dẫn file cấu hình JSON
        /// </summary>
        public static string FilePath => PresetsFilePath;

        /// <summary>
        /// Nạp danh sách mẫu màu chuẩn (nếu đã có file JSON thì nạp từ JSON, ngược lại nạp mặc định)
        /// </summary>
        public static List<BimStandardPreset> LoadPresets()
        {
            try
            {
                if (File.Exists(PresetsFilePath))
                {
                    string json = File.ReadAllText(PresetsFilePath);
                    var loaded = JsonSerializer.Deserialize<List<BimStandardPreset>>(json, JsonOptions);
                    if (loaded != null && loaded.Count > 0)
                    {
                        // Kiểm tra xem dữ liệu JSON đã có các mục chuẩn mới (ví dụ "Tường chắn" và "Đất đắp nền K90") chưa
                        bool hasNewStandard = loaded.Any(p => p.MaterialName.Contains("Tường chắn", StringComparison.OrdinalIgnoreCase)) &&
                                              loaded.Any(p => p.MaterialName.Contains("K90", StringComparison.OrdinalIgnoreCase));
                        if (hasNewStandard)
                        {
                            return loaded;
                        }
                    }
                }
            }
            catch
            {
                // Fallback nếu có lỗi đọc file JSON
            }

            var defaultPresets = CapNhatMauLayerForm.GetDefaultPresets();
            try
            {
                SavePresets(defaultPresets);
            }
            catch { }

            return defaultPresets;
        }

        /// <summary>
        /// Lưu danh sách mẫu màu vào file JSON cấu hình
        /// </summary>
        public static void SavePresets(IEnumerable<BimStandardPreset> presets)
        {
            try
            {
                if (!Directory.Exists(SettingsDirectory))
                {
                    Directory.CreateDirectory(SettingsDirectory);
                }

                var list = presets.ToList();
                string json = JsonSerializer.Serialize(list, JsonOptions);
                File.WriteAllText(PresetsFilePath, json);
            }
            catch (Exception ex)
            {
                throw new Exception($"Không thể lưu file cấu hình mẫu màu: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Khôi phục về danh sách mẫu màu chuẩn ban đầu (BEP T27) và lưu đè file JSON
        /// </summary>
        public static List<BimStandardPreset> ResetToDefaultPresets()
        {
            var defaultPresets = CapNhatMauLayerForm.GetDefaultPresets();
            SavePresets(defaultPresets);
            return defaultPresets;
        }

        /// <summary>
        /// Xuất danh sách bảng mẫu màu ra file Excel (.xlsx)
        /// </summary>
        public static void ExportPresetsToExcel(string filePath, IEnumerable<BimStandardPreset> presets)
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("BIM_Color_Presets");

            string[] headers = new[]
            {
                "STT",
                "Mã Mẫu",
                "Hạng Mục / Cấu Kiện",
                "Vật Liệu / Loại Kết Cấu",
                "R",
                "G",
                "B",
                "Mã HEX",
                "Từ Khóa Nhận Diện Tự Động"
            };

            // Tiêu đề bảng
            for (int c = 0; c < headers.Length; c++)
            {
                var cell = ws.Cell(1, c + 1);
                cell.Value = headers[c];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1B365D");
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            }
            ws.Row(1).Height = 26;

            int row = 2;
            int stt = 1;
            foreach (var p in presets)
            {
                ws.Cell(row, 1).SetValue(stt++);
                ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                ws.Cell(row, 2).SetValue(p.Code ?? "");
                ws.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                ws.Cell(row, 3).SetValue(p.GroupName ?? "");
                ws.Cell(row, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

                ws.Cell(row, 4).SetValue(p.MaterialName ?? "");
                ws.Cell(row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

                ws.Cell(row, 5).SetValue(p.R);
                ws.Cell(row, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                ws.Cell(row, 6).SetValue(p.G);
                ws.Cell(row, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                ws.Cell(row, 7).SetValue(p.B);
                ws.Cell(row, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                ws.Cell(row, 8).SetValue(p.HexCode);
                ws.Cell(row, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Tô màu nền trực quan cho ô HEX
                try
                {
                    ws.Cell(row, 8).Style.Fill.BackgroundColor = XLColor.FromArgb(p.R, p.G, p.B);
                    double lum = (0.299 * p.R + 0.587 * p.G + 0.114 * p.B);
                    ws.Cell(row, 8).Style.Font.FontColor = lum < 140 ? XLColor.White : XLColor.Black;
                    ws.Cell(row, 8).Style.Font.Bold = true;
                }
                catch { }

                string keywords = p.Keywords != null ? string.Join(", ", p.Keywords) : "";
                ws.Cell(row, 9).SetValue(keywords);
                ws.Cell(row, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

                row++;
            }

            var range = ws.Range(1, 1, Math.Max(row - 1, 1), headers.Length);
            range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            ws.Columns().AdjustToContents(10, 50);

            workbook.SaveAs(filePath);
        }

        /// <summary>
        /// Nhập danh sách bảng mẫu màu từ file Excel (.xlsx)
        /// </summary>
        public static List<BimStandardPreset> ImportPresetsFromExcel(string filePath)
        {
            using var workbook = new XLWorkbook(filePath);
            var ws = workbook.Worksheets.FirstOrDefault();
            if (ws == null)
            {
                throw new Exception("File Excel không chứa bất kỳ Sheet nào!");
            }

            var rangeUsed = ws.RangeUsed();
            if (rangeUsed == null)
            {
                throw new Exception("File Excel không có dữ liệu!");
            }

            int firstRow = rangeUsed.FirstRow().RowNumber();
            int lastRow = rangeUsed.LastRow().RowNumber();
            int firstCol = rangeUsed.FirstColumn().ColumnNumber();
            int lastCol = rangeUsed.LastColumn().ColumnNumber();

            // Tìm hàng tiêu đề và chỉ mục các cột
            int headerRow = firstRow;
            int colCodeIdx = -1;
            int colGroupIdx = -1;
            int colMaterialIdx = -1;
            int colRIdx = -1;
            int colGIdx = -1;
            int colBIdx = -1;
            int colRgbIdx = -1;
            int colHexIdx = -1;
            int colKeywordsIdx = -1;

            int maxHeaderScan = Math.Min(firstRow + 5, lastRow);
            for (int r = firstRow; r <= maxHeaderScan; r++)
            {
                for (int c = firstCol; c <= lastCol; c++)
                {
                    string val = ws.Cell(r, c).GetString().Trim().ToLower();
                    if (val == "mã" || val == "mã mẫu" || val.Contains("code")) colCodeIdx = c;
                    else if (val.Contains("hạng mục") || val.Contains("cấu kiện") || val.Contains("group")) colGroupIdx = c;
                    else if (val.Contains("vật liệu") || val.Contains("loại kết cấu") || val.Contains("material")) colMaterialIdx = c;
                    else if (val == "r" || val == "red" || val == "màu r") colRIdx = c;
                    else if (val == "g" || val == "green" || val == "màu g") colGIdx = c;
                    else if (val == "b" || val == "blue" || val == "màu b") colBIdx = c;
                    else if (val.Contains("rgb") || val == "màu") colRgbIdx = c;
                    else if (val.Contains("hex")) colHexIdx = c;
                    else if (val.Contains("từ khóa") || val.Contains("keyword")) colKeywordsIdx = c;
                }

                if (colMaterialIdx > 0 && (colCodeIdx > 0 || colGroupIdx > 0 || colRgbIdx > 0 || colRIdx > 0))
                {
                    headerRow = r;
                    break;
                }
            }

            var result = new List<BimStandardPreset>();

            for (int r = headerRow + 1; r <= lastRow; r++)
            {
                string code = colCodeIdx > 0 ? ws.Cell(r, colCodeIdx).GetString().Trim() : "";
                string group = colGroupIdx > 0 ? ws.Cell(r, colGroupIdx).GetString().Trim() : "";
                string material = colMaterialIdx > 0 ? ws.Cell(r, colMaterialIdx).GetString().Trim() : "";

                if (string.IsNullOrEmpty(material) && string.IsNullOrEmpty(group) && string.IsNullOrEmpty(code))
                {
                    continue;
                }

                byte rVal = 128, gVal = 128, bVal = 128;
                bool hasColor = false;

                // 1. Thử lấy R, G, B riêng biệt
                if (colRIdx > 0 && colGIdx > 0 && colBIdx > 0)
                {
                    if (byte.TryParse(ws.Cell(r, colRIdx).GetString().Trim(), out byte rP) &&
                        byte.TryParse(ws.Cell(r, colGIdx).GetString().Trim(), out byte gP) &&
                        byte.TryParse(ws.Cell(r, colBIdx).GetString().Trim(), out byte bP))
                    {
                        rVal = rP; gVal = gP; bVal = bP;
                        hasColor = true;
                    }
                }

                // 2. Thử lấy từ chuỗi RGB
                if (!hasColor && colRgbIdx > 0)
                {
                    string rgbStr = ws.Cell(r, colRgbIdx).GetString().Trim();
                    if (!string.IsNullOrEmpty(rgbStr))
                    {
                        var parts = rgbStr.Replace("(", "").Replace(")", "").Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 3 &&
                            byte.TryParse(parts[0], out byte rP) &&
                            byte.TryParse(parts[1], out byte gP) &&
                            byte.TryParse(parts[2], out byte bP))
                        {
                            rVal = rP; gVal = gP; bVal = bP;
                            hasColor = true;
                        }
                    }
                }

                // 3. Thử lấy từ chuỗi HEX
                if (!hasColor && colHexIdx > 0)
                {
                    string hexStr = ws.Cell(r, colHexIdx).GetString().Trim().TrimStart('#');
                    if (hexStr.Length == 6)
                    {
                        try
                        {
                            rVal = Convert.ToByte(hexStr.Substring(0, 2), 16);
                            gVal = Convert.ToByte(hexStr.Substring(2, 2), 16);
                            bVal = Convert.ToByte(hexStr.Substring(4, 2), 16);
                            hasColor = true;
                        }
                        catch { }
                    }
                }

                // Từ khóa
                string kwStr = colKeywordsIdx > 0 ? ws.Cell(r, colKeywordsIdx).GetString().Trim() : "";
                string[] keywords = !string.IsNullOrEmpty(kwStr)
                    ? kwStr.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                           .Select(k => k.Trim())
                           .Where(k => !string.IsNullOrEmpty(k))
                           .ToArray()
                    : Array.Empty<string>();

                if (string.IsNullOrEmpty(code))
                {
                    code = (result.Count + 1).ToString();
                }

                result.Add(new BimStandardPreset
                {
                    Code = code,
                    GroupName = group,
                    MaterialName = material,
                    R = rVal,
                    G = gVal,
                    B = bVal,
                    Keywords = keywords
                });
            }

            if (result.Count == 0)
            {
                throw new Exception("Không tìm thấy dòng mẫu màu hợp lệ nào trong file Excel!");
            }

            return result;
        }
    }
}

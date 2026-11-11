using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using ClosedXML.Excel;

namespace MyFirstProject
{
    /// <summary>
    /// Dữ liệu 1 lệnh trong Tool Palette
    /// </summary>
    public class PaletteCommandInfo
    {
        [JsonPropertyName("category")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string Category { get; set; } = "";

        [JsonPropertyName("label")]
        public string Label { get; set; } = "";

        [JsonPropertyName("command")]
        public string Command { get; set; } = "";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("videoUrl")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? VideoUrl { get; set; }
    }

    public class JsonPaletteCategory
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("commands")]
        public List<PaletteCommandInfo> Commands { get; set; } = new();
    }

    public class JsonPaletteRoot
    {
        [JsonPropertyName("categories")]
        public List<JsonPaletteCategory> Categories { get; set; } = new();
    }

    /// <summary>
    /// Đọc/ghi dữ liệu danh sách lệnh cho Tool Palette từ JSON, Excel hoặc danh sách chuẩn tích hợp sẵn.
    /// </summary>
    public static class ToolPaletteDataReader
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };

        /// <summary>
        /// Đọc danh sách lệnh từ file ToolPalette.json
        /// </summary>
        public static List<PaletteCommandInfo> ReadFromJson(string jsonPath)
        {
            string json = File.ReadAllText(jsonPath);
            var root = JsonSerializer.Deserialize<JsonPaletteRoot>(json, JsonOptions);
            var result = new List<PaletteCommandInfo>();

            if (root?.Categories == null)
                return result;

            foreach (var cat in root.Categories)
            {
                string catName = (cat.Name ?? "").Trim();
                if (string.IsNullOrEmpty(catName) || cat.Commands == null)
                    continue;

                foreach (var cmd in cat.Commands)
                {
                    string commandName = (cmd.Command ?? "").Trim();
                    string label = (cmd.Label ?? "").Trim();
                    if (string.IsNullOrEmpty(label) && string.IsNullOrEmpty(commandName))
                        continue;

                    result.Add(new PaletteCommandInfo
                    {
                        Category = catName,
                        Label = string.IsNullOrEmpty(label) ? commandName : label,
                        Command = commandName,
                        Description = (cmd.Description ?? "").Trim(),
                        VideoUrl = string.IsNullOrWhiteSpace(cmd.VideoUrl) ? null : cmd.VideoUrl.Trim()
                    });
                }
            }

            return result;
        }

        /// <summary>
        /// Ghi danh sách lệnh ra file ToolPalette.json
        /// </summary>
        public static void WriteToJson(string jsonPath, List<PaletteCommandInfo> commands)
        {
            var root = new JsonPaletteRoot();
            foreach (var group in commands.GroupBy(c => c.Category))
            {
                var cat = new JsonPaletteCategory
                {
                    Name = group.Key,
                    Commands = group.Select(c => new PaletteCommandInfo
                    {
                        Label = c.Label,
                        Command = c.Command,
                        Description = c.Description,
                        VideoUrl = string.IsNullOrWhiteSpace(c.VideoUrl) ? null : c.VideoUrl
                    }).ToList()
                };
                root.Categories.Add(cat);
            }

            string? dir = Path.GetDirectoryName(jsonPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonSerializer.Serialize(root, JsonOptions);
            File.WriteAllText(jsonPath, json, System.Text.Encoding.UTF8);
        }

        /// <summary>
        /// Đọc file Excel ToolPalette.xlsx → danh sách lệnh
        /// </summary>
        public static List<PaletteCommandInfo> ReadFromExcel(string excelPath)
        {
            using var workbook = new XLWorkbook(excelPath);

            if (workbook.Worksheets.TryGetWorksheet("Commands", out var commandsSheet))
                return ReadSingleSheet(commandsSheet);

            return ReadMultiSheet(workbook);
        }

        private static List<PaletteCommandInfo> ReadMultiSheet(XLWorkbook workbook)
        {
            var result = new List<PaletteCommandInfo>();

            foreach (var ws in workbook.Worksheets)
            {
                string category = ws.Name.Trim();
                if (string.IsNullOrEmpty(category) || category.StartsWith("_"))
                    continue;

                int lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
                for (int r = 2; r <= lastRow; r++)
                {
                    var label = ws.Cell(r, 1).GetString().Trim();
                    var command = ws.Cell(r, 2).GetString().Trim();
                    var desc = ws.Cell(r, 3).GetString().Trim();
                    var video = ws.Cell(r, 4).GetString().Trim();

                    if (string.IsNullOrEmpty(label) && string.IsNullOrEmpty(command))
                        continue;

                    result.Add(new PaletteCommandInfo
                    {
                        Category = category,
                        Label = string.IsNullOrEmpty(label) ? command : label,
                        Command = command,
                        Description = desc,
                        VideoUrl = string.IsNullOrWhiteSpace(video) ? null : video
                    });
                }
            }

            return result;
        }

        private static List<PaletteCommandInfo> ReadSingleSheet(IXLWorksheet ws)
        {
            var result = new List<PaletteCommandInfo>();
            int lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
            string currentCategory = "";

            for (int r = 2; r <= lastRow; r++)
            {
                var cat = ws.Cell(r, 1).GetString().Trim();
                var label = ws.Cell(r, 2).GetString().Trim();
                var command = ws.Cell(r, 3).GetString().Trim();
                var desc = ws.Cell(r, 4).GetString().Trim();
                var video = ws.Cell(r, 5).GetString().Trim();

                if (!string.IsNullOrEmpty(cat))
                    currentCategory = cat;

                if (string.IsNullOrEmpty(label) && string.IsNullOrEmpty(command))
                    continue;

                result.Add(new PaletteCommandInfo
                {
                    Category = currentCategory,
                    Label = string.IsNullOrEmpty(label) ? command : label,
                    Command = command,
                    Description = desc,
                    VideoUrl = string.IsNullOrWhiteSpace(video) ? null : video
                });
            }

            return result;
        }

        /// <summary>
        /// Danh sách toàn bộ các lệnh chuẩn hóa của hệ thống (dùng để khởi tạo/đồng bộ JSON và làm dữ liệu dự phòng)
        /// </summary>
        public static List<PaletteCommandInfo> GetDefaultCommands()
        {
            var list = new List<PaletteCommandInfo>();

            void Add(string cat, string label, string cmd, string desc, string? video = null)
            {
                list.Add(new PaletteCommandInfo
                {
                    Category = cat,
                    Label = label,
                    Command = cmd,
                    Description = desc,
                    VideoUrl = video
                });
            }

            // ═══════════════════════════════════════════════════════════════════
            //  PHẦN I: CIVIL 3D TOOLS (10 NHÓM)
            // ═══════════════════════════════════════════════════════════════════

            const string C1 = "Civil — 1. Tuyến (Alignment)";
            Add(C1, "Bố trí đường cong nằm tự động", "CTA_Bo_Tri_Cong_Nam", "Tự động bố trí bán kính đường cong nằm và chuyển tiếp tại các đỉnh PI");
            Add(C1, "Điều chỉnh bán kính Connected Alignment", "CTA_Dieu_Chinh_Ban_Kinh_Connected_Alignment", "Thay đổi bán kính cong cho các tuyến nối nút giao");
            Add(C1, "Tạo đường Offset từ Alignment", "CTA_Offset_Alignment", "Tạo các đường Offset Alignment hai bên tuyến chính");
            Add(C1, "Đề xuất mặt cắt ngang theo tuyến", "CTA_De_Xuat_Cat_Ngang", "Phân tích và đề xuất quy mô mặt cắt ngang cho tuyến");
            Add(C1, "Kiểm tra tiêu chuẩn bình đồ tuyến", "CTA_Kiem_Tra_Tim_Tuyen", "Kiểm tra các yếu tố hình học bình đồ tuyến theo TCVN");
            Add(C1, "Kiểm tra tầm nhìn (Sight Distance)", "CTA_Kiem_Tra_Tam_Nhin", "Phân tích và kiểm tra tầm nhìn dừng xe / vượt xe trên tuyến");
            Add(C1, "Gán nhãn đầu tuyến & cuối tuyến", "CTA_Gan_Nhan_Dau_Cuoi_Tuyen", "Tự động gắn nhãn lý trình điểm đầu và điểm cuối tuyến");
            Add(C1, "Bảng thống kê các tuyến đường", "CTA_Bang_Thong_Ke_Cac_Tuyen_Duong", "Tạo bảng tổng hợp thông số kỹ thuật các tuyến đường");

            const string C2 = "Civil — 2. Trắc dọc (Profile)";
            Add(C2, "Vẽ trắc dọc tự nhiên (1 tuyến)", "CTP_Ve_Trac_Doc_Tu_Nhien", "Tạo đường trắc dọc tự nhiên và khung ProfileView cho 1 tuyến");
            Add(C2, "Vẽ trắc dọc tự nhiên (Tất cả tuyến)", "CTP_Ve_Trac_Doc_Tu_Nhien_Tat_Ca_Tuyen", "Tạo trắc dọc tự nhiên hàng loạt cho tất cả tuyến trong bản vẽ");
            Add(C2, "Điều chỉnh đường đỏ thiết kế", "CTP_Dieu_Chinh_Duong_Do", "Công cụ tinh chỉnh cao độ và độ dốc đường đỏ thiết kế");
            Add(C2, "Bố trí đường cong đứng tự động", "CTP_Bo_Tri_Cong_Dung", "Tự động bố trí bán kính cong đứng lồi/lõm tại các đỉnh đổi dốc PVI");
            Add(C2, "Kiểm tra tiêu chuẩn trắc dọc", "CTP_Kiem_Tra_Profile", "Kiểm tra độ dốc dọc, chiều dài đoạn dốc và bán kính cong đứng theo TCVN");
            Add(C2, "Sửa đường tự nhiên theo cọc", "CTP_Fix_Duong_Tu_Nhien_Theo_Coc", "Lọc đỉnh đường tự nhiên trên trắc dọc trùng với vị trí cọc SampleLine");
            Add(C2, "Bổ sung cọc từ trắc dọc", "CTP_Bo_Sung_Coc_Tren_Trac_Doc", "Phát sinh thêm cọc SampleLine tại các vị trí chọn trên trắc dọc");
            Add(C2, "Gắn nhãn nút giao lên trắc dọc", "CTP_Gan_Nhan_Nut_Giao_Len_Trac_Doc", "Tự động tìm giao điểm các tuyến và gắn nhãn lý trình lên trắc dọc");
            Add(C2, "Tạo CogoPoint từ đỉnh PVI", "CTP_Tao_Cogo_Point_Tu_PVI", "Trích xuất tọa độ và cao độ các đỉnh PVI trên trắc dọc thành CogoPoint");
            Add(C2, "Chuyển Polyline thành Profile", "CTP_Polyline_To_Profile", "Chuyển đổi đường Polyline trên trắc dọc thành Profile Civil 3D");
            Add(C2, "Xuất Profile thành Polyline", "CTP_Profile_To_Polyline", "Xuất đường Profile trên ProfileView thành đối tượng Polyline");
            Add(C2, "Điều chỉnh Profile theo Polyline", "CTP_Adjust_Profile_By_Polyline", "Cập nhật cao độ đỉnh PVI của Profile bám theo đường Polyline");
            Add(C2, "Thay đổi dữ liệu Profile trong Band", "CTP_Thay_Doi_Profile_Band", "Gán đồng loạt Profile tự nhiên và thiết kế vào các dòng Band trắc dọc");
            Add(C2, "Dịch chuyển nhãn chẵn trong Band", "CTP_Dich_Chuyen_Label_Chan", "Dịch chuyển vị trí nhãn tại các lý trình chẵn trong Band trắc dọc");

            const string C3 = "Civil — 3. Hành lang (Corridor)";
            Add(C3, "Tạo Corridor cho tuyến đường", "CTC_Tao_Corridor_Cho_Tuyen_Duong", "Tự động xây dựng Corridor, Region và gán Target cho tuyến đường");
            Add(C3, "Tạo Corridor theo từng cấu kiện", "CTC_Tao_Corridor_Theo_Cau_Kien", "Tách và xây dựng Corridor riêng theo từng nhóm cấu kiện");
            Add(C3, "Tạo Corridor đường đô thị rẽ phải", "CTC_Tao_Corridor_Duong_Do_Thi_Re_Phai", "Tạo Corridor cho góc nút giao rẽ phải đường đô thị");
            Add(C3, "Thêm Section cho tất cả Region", "CTC_Them_Section_Tat_Ca_Region", "Bổ sung mặt cắt cọc vào tất cả các phân đoạn Region của Corridor");
            Add(C3, "Thêm tất cả Section (SampleLine)", "CTC_Add_All_Section", "Thêm toàn bộ vị trí cọc SampleLine làm mặt cắt tính toán trong Corridor");
            Add(C3, "Cấu hình Target cho Subassembly", "CTC_Cau_Hinh_Target_Corridor", "Thiết lập tự động các mục tiêu (Width/Elevation/Surface Target) cho Subassembly");
            Add(C3, "Xem thông tin Target của Corridor", "CTC_Thong_Tin_Target_Corridor", "Liệt kê chi tiết các đối tượng Target đang gán trong Corridor");
            Add(C3, "Điều chỉnh lý trình phân đoạn Region", "CTC_Dieu_Chinh_Phan_Doan", "Tinh chỉnh lý trình đầu/cuối của các Region trong Corridor");
            Add(C3, "Chọn phân đoạn Region trên Corridor", "CTC_Chon_Doan_Corridor", "Chọn và quản lý các phân đoạn Region trên bình đồ");
            Add(C3, "Phân chia Corridor theo Profile", "CTC_Phan_Chia_Corridor_Theo_Profile", "Tự động cắt chia Region của Corridor theo các đoạn của trắc dọc");
            Add(C3, "Nhân bản Corridor (Copy)", "CTC_Nhan_Ban_Corridor", "Sao chép cấu trúc Baseline và Region sang một Corridor mới");
            Add(C3, "Bật/Tắt & chia nhỏ Region theo Polyline", "CTC_Bat_Tat_Corridor_Region_Polyline", "Tự động bật hoặc tắt các Region nằm trong vùng Polyline kín");
            Add(C3, "Xóa các Region đang bị tắt", "CTC_Xoa_Region_Bi_Tat", "Dọn dẹp và xóa vĩnh viễn các Region đã tắt trong Corridor");
            Add(C3, "Tạo Corridor Surface (Hàng loạt / Form)", "CTC_Tao_Corridor_Surface", "Tự động tạo bề mặt Top/Datum cho nhiều Corridor cùng lúc");
            Add(C3, "Tạo Corridor Surface (Chọn đơn)", "CTC_Tao_Corridor_Surface_Don", "Tạo nhanh bề mặt thiết kế cho 1 Corridor được chọn");
            Add(C3, "Xuất 3D Solid từ Corridor", "CTC_Xuat_Solid_Tu_Corridor", "Trích xuất hàng loạt khối 3D Solid kết cấu từ Corridor");
            Add(C3, "Ngắt liên kết & xóa Solid/Body Corridor", "CTC_Ngat_Lien_Ket_Solid_Corridor", "Ngắt liên kết động của 3D Solid với Corridor và dọn dẹp Body");
            Add(C3, "Trích xuất ranh giới Corridor", "CTC_Trich_Xuat_Ranh_Gioi_Corridor", "Xuất đường bao ranh giới chân taluy / phạm vi chiếm dụng của Corridor");
            Add(C3, "Quản lý & tạo Subassembly .NET", "CTC_Quan_Ly_Subassembly_DotNet", "Quản lý, cấu hình và chèn các Subassembly .NET tùy chỉnh");

            const string C4 = "Civil — 4. Cọc & Lý trình (Sample Line)";
            Add(C4, "Phát sinh cọc tự động trên tuyến", "CTS_Phat_Sinh_Coc", "Phát sinh cọc lý trình Km, H, cọc chi tiết và cọc đặc biệt trên tuyến");
            Add(C4, "Phát sinh cọc thủ công", "CTS_Phat_Sinh_Coc_Thu_Cong", "Chọn vị trí trực tiếp trên bình đồ để chèn cọc và đặt tên tự động");
            Add(C4, "Phát sinh cọc theo bảng cấu hình", "CTS_Phat_Sinh_Coc_Theo_Bang", "Phát sinh cọc hàng loạt theo bảng phân đoạn khoảng cách");
            Add(C4, "Phát sinh cọc theo khoảng Delta", "CTS_Phat_Sinh_Coc_Theo_Khoang_Delta", "Rải cọc đều theo khoảng cách Delta trong phạm vi chọn");
            Add(C4, "Phát sinh cọc từ CogoPoint", "CTS_Phat_Sinh_Coc_Tu_Cogo_Point", "Tạo cọc SampleLine chiếu vuông góc từ các điểm CogoPoint");
            Add(C4, "Chèn cọc trên trắc dọc", "CTS_Chen_Coc_Tren_Trac_Doc", "Hiển thị ký hiệu và tên cọc SampleLine lên khung trắc dọc");
            Add(C4, "Chèn cọc trên trắc ngang", "CTS_Chen_Coc_Tren_Trac_Ngang", "Chèn bổ sung cọc mới từ vị trí mặt cắt ngang");
            Add(C4, "Bổ sung cọc từ Data Shortcut (Local)", "CTS_Bo_Sung_Coc_Data_Shortcut", "Đọc và sao chép cọc SampleLine từ file nguồn DataShortcut vào bản vẽ");
            Add(C4, "Bổ sung cọc từ Data Shortcut (Ref)", "CTS_Bo_Sung_Coc_Data_Shortcut_Ref", "Tạo tham chiếu DataShortcut Reference cho tuyến và cọc");
            Add(C4, "Đổi tên cọc theo quy tắc (Form)", "CTS_Doi_Ten_Coc_Quy_Tac", "Đổi tên cọc thông minh theo quy tắc tiền tố, hậu tố và lý trình");
            Add(C4, "Đổi tên cọc cơ bản", "CTS_Doi_Ten_Coc", "Đánh lại số thứ tự tên cọc liên tục từ đầu đến cuối tuyến");
            Add(C4, "Đổi tên cọc theo đoạn", "CTS_Doi_Ten_Coc_2", "Đổi tên cọc trong phạm vi đoạn lý trình chỉ định");
            Add(C4, "Đổi tên cọc theo lý trình Km", "CTS_Doi_Ten_Coc_3", "Tự động đặt tên các cọc Km và cọc H theo lý trình");
            Add(C4, "Đổi tên cọc H (Hectomet)", "CTS_Doi_Ten_Coc_H", "Chuẩn hóa tên gọi cho các cọc Hectomet (H1..H9)");
            Add(C4, "Đổi tên cọc theo thứ tự chọn", "CTS_Doi_Ten_Coc_Theo_Thu_Tu", "Đổi tên các cọc theo thứ tự người dùng chọn trên bản vẽ");
            Add(C4, "Đổi tên cọc từ CogoPoint", "CTS_Doi_Ten_Coc_Tu_Cogo_Point", "Lấy tên/mô tả của CogoPoint gán sang tên cọc SampleLine tương ứng");
            Add(C4, "Dịch cọc tịnh tiến (Tùy chọn)", "CTS_Dich_Coc_Tinh_Tien", "Dịch chuyển vị trí cọc dọc theo tuyến một khoảng Delta nhập vào");
            Add(C4, "Dịch cọc tịnh tiến 20m", "CTS_Dich_Coc_Tinh_Tien_20", "Dịch chuyển nhanh cọc theo bước 20m");
            Add(C4, "Dịch cọc tịnh tiến 40m", "CTS_Dich_Coc_Tinh_Tien_40", "Dịch chuyển nhanh cọc theo bước 40m");
            Add(C4, "Sao chép nhóm cọc (Copy Group)", "CTS_Copy_Nhom_Coc", "Sao chép các cọc SampleLine sang một nhóm cọc hoặc tuyến khác");
            Add(C4, "Đồng bộ 2 nhóm cọc", "CTS_Dong_Bo_2_Nhom_Coc", "Đồng bộ tên và lý trình giữa 2 nhóm cọc SampleLineGroup");
            Add(C4, "Đồng bộ 2 nhóm cọc theo đoạn", "CTS_Dong_Bo_2_Nhom_Coc_Theo_Doan", "Đồng bộ tên và lý trình giữa 2 nhóm cọc trong đoạn chỉ định");
            Add(C4, "Thay đổi bề rộng Sample Line", "CTS_Thay_Doi_Be_Rong_Sample_Line", "Thay đổi bề rộng sang trái và sang phải của các cọc SampleLine");
            Add(C4, "Sao chép bề rộng Sample Line", "CTS_Copy_Be_Rong_Sample_Line", "Sao chép bề rộng lấy mẫu từ cọc mẫu sang các cọc khác");
            Add(C4, "Offset bề rộng Sample Line theo Polyline", "CTS_Offset_Be_Rong_Sample_Line", "Cắt hoặc kéo dài bề rộng SampleLine bám theo đường biên Polyline trái/phải");
            Add(C4, "Hiệu chỉnh khoảng cách cọc (Làm tròn)", "CTS_Hieu_Chinh_Khoang_Cach_Coc", "Làm tròn lý trình các cọc lẻ về số chẵn theo bước làm tròn");
            Add(C4, "Điều chỉnh cọc vuông góc tuyến (Orthogonal)", "CTS_Dieu_Chinh_Orthogonal", "Xoay các vạch cọc SampleLine bị xiên về đúng phương vuông góc 90 độ với tim");
            Add(C4, "Xuất bảng tọa độ cọc (Form đầy đủ)", "CTS_Xuat_Toa_Do_Coc", "Xuất bảng tọa độ X, Y, lý trình và khoảng cách lẻ của hệ thống cọc");
            Add(C4, "Tạo bảng tọa độ cọc (Mẫu 1)", "CTS_Tao_Bang_Toa_Do_Coc", "Lập bảng tọa độ cọc trực tiếp trên bản vẽ theo mẫu 1");
            Add(C4, "Tạo bảng tọa độ cọc (Mẫu 2 - Lý trình)", "CTS_Tao_Bang_Toa_Do_Coc_2", "Lập bảng tọa độ cọc kèm lý trình theo mẫu 2");
            Add(C4, "Tạo bảng tọa độ cọc (Mẫu 3 - Cao độ)", "CTS_Tao_Bang_Toa_Do_Coc_3", "Lập bảng tọa độ cọc kèm cao độ theo mẫu 3");
            Add(C4, "Cập nhật 2 bảng tọa độ cọc", "CTS_Cap_Nhat_2_Bang_Toa_Do_Coc", "Cập nhật dữ liệu đồng thời cho 2 bảng thống kê tọa độ cọc");

            const string C5 = "Civil — 5. Trắc ngang (Section View)";
            Add(C5, "Vẽ trắc ngang thiết kế", "CTS_Ve_Trac_Ngang_Thiet_Ke", "Tạo hàng loạt khung trắc ngang SectionView kèm bảng khối lượng");
            Add(C5, "Bổ sung Section trên trắc ngang", "CTS_Bo_Sung_Cat_Ngang", "Thêm nguồn dữ liệu Surface / Corridor mới vào tất cả SectionView");
            Add(C5, "Tính toán & vẽ đánh cấp tự động", "CTS_Danh_Cap", "Tính toán và vẽ bậc đánh cấp vét hữu cơ / đắp nền trên trắc ngang");
            Add(C5, "Cập nhật lại đánh cấp", "CTS_Danh_Cap_Cap_Nhat", "Tính toán và cập nhật lại chiều dài đánh cấp sau khi chỉnh sửa");
            Add(C5, "Vẽ thêm đánh cấp tùy chọn", "CTS_Danh_Cap_Ve_Them", "Vẽ bổ sung bậc đánh cấp tại vị trí chỉ định trên trắc ngang");
            Add(C5, "Vẽ thêm đánh cấp 1m", "CTS_Danh_Cap_Ve_Them_1", "Vẽ nhanh bậc đánh cấp rộng 1m trên trắc ngang");
            Add(C5, "Vẽ thêm đánh cấp 2m", "CTS_Danh_Cap_Ve_Them_2", "Vẽ nhanh bậc đánh cấp rộng 2m trên trắc ngang");
            Add(C5, "Xóa bỏ đánh cấp", "CTS_Danh_Cap_Xoa_Bo", "Xóa toàn bộ đường đánh cấp và dữ liệu đánh cấp trên trắc ngang");
            Add(C5, "Hiệu chỉnh Section (Static)", "CTS_Hieu_Chinh_Section", "Chuyển các đường cắt ngang được chọn sang chế độ tĩnh (Static)");
            Add(C5, "Hiệu chỉnh Section (Dynamic)", "CTS_Hieu_Chinh_Section_Dynamic", "Chuyển các đường cắt ngang được chọn về chế độ động (Dynamic)");
            Add(C5, "Chọn nhanh Section Static", "CTS_Chon_Section_Static", "Quét và chọn các mặt cắt ngang đang ở trạng thái Static");
            Add(C5, "Điều chỉnh đường tự nhiên trên trắc ngang", "CTS_Dieu_Chinh_Duong_Tu_Nhien", "Chỉnh sửa cao độ đường tự nhiên EG trên các mặt cắt ngang");
            Add(C5, "Chuyển đổi nhãn TN-TK sang TN-TN", "CTS_Chuyen_Doi_TNTK_TNTN", "Chuyển đổi kiểu hiển thị nhãn chênh cao trên Band trắc ngang");
            Add(C5, "Khóa cắt ngang & thêm điểm", "CTS_Khoa_Cat_Ngang_Add_Point", "Khóa đường cắt ngang và chèn thêm điểm gãy");
            Add(C5, "Ẩn đường địa chất trên trắc ngang", "CTS_An_Duong_Dia_Chat", "Ẩn nhanh các đường địa chất nằm ngoài phạm vi đào/đắp");
            Add(C5, "Hiệu chỉnh nhãn Text bị chồng đè", "CTS_Hieu_Chinh_Text_Chong", "Tự động rải và dịch chuyển các nhãn cao độ/khoảng cách bị đè nhau trên trắc ngang", @"Y:\8. HDSD PHAN MEM - FORM MAU\6.CIVIL 3D\1.civil tool\CTSV_HIEUCHINHTEXTCHONG.mkv");
            Add(C5, "Tạo Polyline từ Section", "CTS_Polyline_Tu_Section", "Trích xuất hình học đường mặt cắt trên SectionView ra Polyline");
            Add(C5, "Sắp xếp Layout Section View", "CTS_Sap_Xep_Layout", "Sắp xếp vị trí các trắc ngang theo hàng/cột gọn gàng");
            Add(C5, "Fit khung in trắc ngang", "CTS_Fit_Khung_In", "Tự động căn chỉnh vùng hiển thị SectionView vừa khít khung in");
            Add(C5, "Thay đổi khung in trắc ngang", "CTS_Thay_Doi_Khung_In", "Đổi mẫu Group Plot Style và Plot Area cho nhóm trắc ngang");
            Add(C5, "Thay đổi cao độ MSS (Form)", "CTS_Thay_Doi_MSS", "Mở giao diện cấu hình chi tiết cao độ và bề rộng cho nhóm SectionView");
            Add(C5, "Thay đổi giới hạn cao độ MSS Min/Max", "CTS_Thay_Doi_MSS_Min_Max", "Điều chỉnh khoảng cao độ hiển thị tối thiểu/tối đa cho các SectionView");
            Add(C5, "Thay đổi giới hạn bề rộng Trái/Phải", "CTS_Thay_Doi_Gioi_Han_Trai_Phai", "Điều chỉnh phạm vi offset trái/phải hiển thị trên SectionView");
            Add(C5, "Thêm & cấu hình Material List", "CTS_Them_Material_List", "Tạo danh mục tính khối lượng đào đắp (Material List) cho nhóm cọc");
            Add(C5, "Xem thông tin Material List", "CTS_Xem_Material_List", "Kiểm tra danh sách các loại vật liệu trong Material List hiện có");
            Add(C5, "Xóa Material List", "CTS_Xoa_Material_List", "Xóa các Material List trong SampleLineGroup");
            Add(C5, "Thêm vật liệu trên cắt ngang", "CTS_Them_Vat_Lieu_Tren_Cat_Ngang", "Gán thêm bề mặt vật liệu vào danh mục tính khối lượng");
            Add(C5, "Thêm bảng khối lượng cắt ngang", "CTS_Them_Bang_KL_Cat_Ngang", "Chèn bảng khối lượng đào/đắp (Volume Table) vào từng SectionView");
            Add(C5, "Tính tổng chiều dài trên cắt ngang", "CTS_Tong_Chieu_Dai_Cat_Ngang", "Đo và thống kê chiều dài các mã Link (Link Code) của Corridor trên trắc ngang");
            Add(C5, "Tính khối lượng cắt ngang", "CTS_Khoi_Luong_Cat_Ngang", "Tổng hợp diện tích và khối lượng các lớp vật liệu trên mặt cắt ngang");
            Add(C5, "Xuất khối lượng trắc ngang ra Excel", "CTS_Xuat_Khoi_Luong_Ra_Excel", "Xuất toàn bộ bảng khối lượng đào đắp và kết cấu của các tuyến ra file Excel");

            const string C6 = "Civil — 6. Bề mặt & Feature Line (Surface)";
            Add(C6, "Tạo mặt phẳng ghép EG - TK", "CTT_Tao_Mat_Phang_EG_TK", "Tạo bề mặt kết hợp giữa địa hình tự nhiên (EG) và mặt phẳng thiết kế (TK)");
            Add(C6, "Điều chỉnh mặt phẳng EG - TK (Tạo vùng sửa)", "CTT_Dieu_Chinh_EG_TK", "Hiệu chỉnh cao độ bề mặt tự nhiên theo bề mặt thiết kế");
            Add(C6, "Áp dụng chỉnh sửa mặt phẳng EG - TK", "CTT_Dieu_Chinh_EG_TK_Apply", "Thực thi cập nhật cao độ bề mặt EG theo vùng chỉnh sửa");
            Add(C6, "Xem trạng thái chờ sửa EG - TK", "CTT_Dieu_Chinh_EG_TK_Status", "Kiểm tra trạng thái phiên hiệu chỉnh bề mặt EG - TK hiện tại");
            Add(C6, "Hủy các Polyline tạm EG - TK", "CTT_Dieu_Chinh_EG_TK_Cancel", "Hủy bỏ phiên hiệu chỉnh bề mặt EG - TK đang chờ");
            Add(C6, "Lấy cao độ mặt phẳng tại CogoPoint", "CTT_Cao_Do_Mat_Phang_Tai_Cogo_Point", "Nội suy cao độ từ bề mặt TIN Surface và gán cho các điểm CogoPoint");
            Add(C6, "Tạo Spot Elevation trên Surface tại tim", "CTT_Tao_Spot_Elevation_Tai_Tim", "Tạo nhãn cao độ Spot Elevation dọc theo tim tuyến trên bề mặt");
            Add(C6, "Trích xuất 3D Solid KCAĐ từ mặt phẳng", "CTT_Xuat_Solid_KCAD_Tu_Mat_Phang", "Tạo các lớp 3D Solid kết cấu áo đường từ bề mặt TIN Surface và vùng bao");
            Add(C6, "Tạo mặt phẳng (Surface) từ Feature Line", "CTT_Tao_Mat_Phang_Tu_Feature_Line", "Tạo bề mặt TIN Surface từ các đường FeatureLine chọn trên bản vẽ");
            Add(C6, "Tạo khối 3D Solid từ Feature Line", "CT_Tao_3d_Solid_Tu_Feature_Line", "Quét tiết diện hoặc đùn khối 3D Solid dọc theo đường FeatureLine");

            const string C7 = "Civil — 7. Điểm (CogoPoint)";
            Add(C7, "Tạo CogoPoint đa nguồn (Form tổng hợp)", "CT_Tao_Cogo_Point_Tong_Hop", "Giao diện tổng hợp tạo CogoPoint từ Text, Circle, Point, Table, Excel...");
            Add(C7, "Tạo CogoPoint lấy cao độ từ Surface", "CT_Tao_Cogo_Point_Cao_Do_Tu_Surface", "Rải điểm CogoPoint và tự động nội suy cao độ Z từ bề mặt TIN Surface");
            Add(C7, "Tạo CogoPoint từ Spot Elevation", "CT_Tao_Cogo_Point_Tu_Elevation_Spot", "Chuyển đổi các nhãn cao độ Spot Elevation thành điểm CogoPoint");
            Add(C7, "Tạo CogoPoint từ Text cao độ", "CT_Tao_Cogo_Point_Tu_Text", "Đọc giá trị số của Text cao độ hiện trạng để tạo điểm CogoPoint đúng vị trí");
            Add(C7, "Tạo CogoPoint từ file Excel", "CT_Tao_Cogo_Point_Tu_Excel", "Nhập danh sách tọa độ điểm X, Y, Z, Mô tả từ file Excel vào bản vẽ");
            Add(C7, "Đổi tên CogoPoint theo mẫu (Template)", "CT_Doi_Ten_Cogo_Point", "Đổi tên điểm CogoPoint hàng loạt kết hợp xuất/nhập Excel");
            Add(C7, "Đổi tên CogoPoint theo tuyến Alignment", "CT_Doi_Ten_Cogo_Point_Tu_Alignment", "Đổi tên CogoPoint theo thứ tự lý trình dọc theo tuyến Alignment");
            Add(C7, "Thêm mô tả (Description) cho CogoPoint", "CT_Them_Mo_Ta_Cogo_Point", "Gán hoặc cập nhật mã mô tả Raw Description hàng loạt cho CogoPoint");
            Add(C7, "Cập nhật tất cả Point Group", "CT_Update_All_Point_Group", "Cập nhật (Update) lại toàn bộ các nhóm điểm PointGroup trong bản vẽ");
            Add(C7, "Ẩn CogoPoint được chọn", "CT_An_Cogo_Point", "Chuyển Point Style và Label Style của các CogoPoint được chọn về None (ẩn)");

            const string C8 = "Civil — 8. Thoát nước (Pipe Network)";
            Add(C8, "Thay đổi đường kính cống hàng loạt", "CT_Thay_Doi_Duong_Kinh_Cong", "Đổi kích thước đường kính trong Part Family cho nhiều đoạn cống");
            Add(C8, "Thay đổi độ dốc cống", "CT_Thay_Doi_Do_Doc_Cong", "Cập nhật độ dốc dọc cho các đoạn cống được chọn");
            Add(C8, "Thay đổi cao độ đáy cống (Invert)", "CT_Thay_Doi_Cao_Do_Day_Cong", "Tinh chỉnh cao độ đáy cống đầu/cuối hoặc theo độ dốc thiết kế");
            Add(C8, "Điều chỉnh bề mặt tham chiếu cống/hố thu", "CT_Dieu_Chinh_Be_Mat_Tham_Chieu", "Gán đồng loạt Reference Surface và Reference Alignment cho cống & hố ga");
            Add(C8, "Lập bảng cao độ tự nhiên hố thu", "CT_Bang_Cao_Do_Tu_Nhien_Ho_Thu", "Tạo bảng tổng hợp tọa độ và cao độ mặt đất tự nhiên tại các hố thu");
            Add(C8, "Xoay hố thu theo 2 điểm", "CT_Xoay_Ho_Thu_Theo_2_Diem", "Xoay góc hướng của hố ga/cửa thu nước theo hướng 2 điểm chọn trên bản vẽ");

            const string C9 = "Civil — 9. Thửa đất (Parcel)";
            Add(C9, "Thống kê Parcel ra bảng CAD / Excel", "CT_Bang_Thong_Ke_Parcel", "Tổng hợp diện tích các lô đất Parcel và xuất bảng thống kê chuẩn");
            Add(C9, "Đổi tên Parcel theo mẫu (Template)", "CT_Doi_Ten_Parcel", "Đặt lại tên các lô đất Parcel theo mẫu tiền tố và số thứ tự");

            const string C10 = "Civil — 10. BIM Property Sets & Thông tin";
            Add(C10, "Cập nhật màu & PropertySet BIM (Solid & Surface)", "AT_Cap_Nhat_Mau_Va_Property_Set", "Đồng bộ màu sắc Layer, đặt ByLayer cho 3D Solid và nhập/xuất Property Set qua Excel");
            Add(C10, "Cập nhật PropertySet 3D Solid theo Layer", "AT_Solid_Update_Property_Set", "Cập nhật nhanh thông tin Property Set của 3D Solid dựa theo tên Layer");
            Add(C10, "Gán PropertySet cho 3D Solid", "AT_Solid_Set_Property_Set", "Khởi tạo bộ thuộc tính BIM Property Set và gán cho các khối 3D Solid");
            Add(C10, "Xem thông tin khối 3D Solid", "AT_Solid_Show_Info", "Đọc và hiển thị thông tin thể tích, diện tích và thuộc tính của 3D Solid");
            Add(C10, "Tra cứu thông tin đối tượng Civil 3D", "CT_Thong_Tin_Doi_Tuong", "Tra cứu nhanh thông số hình học và thuộc tính đối tượng Civil 3D");

            // ═══════════════════════════════════════════════════════════════════
            //  PHẦN II: AUTOCAD TOOLS (9 NHÓM)
            // ═══════════════════════════════════════════════════════════════════

            const string A1 = "AutoCAD — 1. Layer, Tỷ lệ & Chuẩn hóa";
            Add(A1, "Chuẩn hóa bản vẽ theo tiêu chuẩn T27", "AT_Chuan_Hoa_Ban_Ve", "Thiết lập trọn bộ 15 Layer chuẩn, TextStyle, DimStyle, MLeaderStyle theo tỷ lệ 1:N");
            Add(A1, "Chuyển đổi tỷ lệ nhanh bản vẽ (1:N)", "AT_Chuyen_Doi_Ty_Le_Nhanh", "Quy đổi chiều cao chữ, DimScale, MLeaderScale, LinetypeScale và Hatch giữa các tỷ lệ");
            Add(A1, "Giữ lại duy nhất Annotative Scale hiện hành", "AT_Annotative_Scale_Current_Only", "Dọn bỏ các tỷ lệ Annotative thừa, chỉ giữ lại tỷ lệ hiện hành");
            Add(A1, "Gán lệnh tắt số thứ tự cho Layer", "AT_Gan_Lenh_Layer", "Chuyển nhanh các đối tượng đang chọn về đúng Layer quy chuẩn");
            Add(A1, "Chọn đối tượng theo Layer", "AT_Chon_Theo_Layer", "Chọn tất cả các đối tượng trong bản vẽ thuộc các Layer được chỉ định");
            Add(A1, "Chuyển đổi đối tượng sang Layer khác", "AT_Chuyen_Doi_Layer", "Chuyển nhanh các đối tượng từ Layer nguồn sang Layer đích");
            Add(A1, "Xóa các đối tượng cùng Layer", "AT_Xoa_Doi_Tuong_Cung_Layer", "Chọn đối tượng mẫu để xóa toàn bộ các đối tượng nằm trên cùng Layer đó");
            Add(A1, "Cập nhật màu Layer theo chuẩn BIM EIR", "AT_Cap_Nhat_Mau_Layer", "Đồng bộ bảng màu Layer theo quy chuẩn BIM EIR");

            const string A2 = "AutoCAD — 2. Đo Độ dài, Diện tích & Độ dốc";
            Add(A2, "Tính tổng độ dài (Tạo Text mới)", "AT_Tong_Do_Dai_Full", "Cộng tổng chiều dài các đường Line/Polyline/Arc/Spline và đặt Text kết quả mới");
            Add(A2, "Tính tổng độ dài (Ghi đè Text)", "AT_Tong_Do_Dai_Replace", "Cộng tổng chiều dài các đường được chọn và thay số vào Text có sẵn");
            Add(A2, "Tính tổng độ dài (Ghi đè nhiều lần)", "AT_Tong_Do_Dai_Replace_2", "Lặp lại thao tác cộng tổng chiều dài và cập nhật vào từng Text liên tiếp");
            Add(A2, "Tính tổng độ dài (Cộng thêm vào Text)", "AT_Tong_Do_Dai_Replace_Cong_Them", "Cộng tổng chiều dài nhân hệ số hoặc cộng thêm giá trị bù rồi ghi vào Text");
            Add(A2, "Tính tổng diện tích (Tạo Text mới)", "AT_Tong_Dien_Tich_Full", "Cộng tổng diện tích các vùng kín Polyline/Hatch/Region và đặt Text kết quả mới");
            Add(A2, "Tính tổng diện tích (Ghi đè Text)", "AT_Tong_Dien_Tich_Replace", "Cộng tổng diện tích các hình được chọn và thay số vào Text có sẵn");
            Add(A2, "Tính tổng diện tích (Ghi đè nhiều lần)", "AT_Tong_Dien_Tich_Replace_2", "Lặp lại thao tác cộng tổng diện tích và cập nhật vào từng Text liên tiếp");
            Add(A2, "Tính tổng diện tích (Cộng thêm vào Text)", "AT_Tong_Dien_Tich_Replace_Cong_Them", "Cộng tổng diện tích nhân hệ số quy đổi rồi ghi vào Text");
            Add(A2, "Đo độ dốc giữa 2 điểm", "AT_Do_Doc", "Tính và ghi nhãn độ dốc (%) hoặc hệ số mái dốc (1:m) giữa 2 điểm có cao độ");
            Add(A2, "Đo độ dốc đơn giản", "AT_Do_Doc_Simple", "Tính nhanh độ dốc phần trăm giữa 2 điểm chọn trên bản vẽ");
            Add(A2, "Đo độ dốc theo đối tượng", "AT_Do_Doc_Object", "Đo và ghi nhãn độ dốc trực tiếp trên đoạn thẳng hoặc Polyline");

            const string A3 = "AutoCAD — 3. Biên tập Text & Đánh số";
            Add(A3, "Căn lề cho các đối tượng Text", "AT_Can_Le_Cho_Text", "Sắp xếp và căn lề Trái / Giữa / Phải thẳng hàng cho nhóm Text/MText");
            Add(A3, "Copy nhanh nội dung Text", "AT_Copy_Noi_Dung_Text", "Sao chép nội dung giữa các Text/Attribute kèm tính toán cộng trừ nhân chia");
            Add(A3, "Copy & dịch Text sang Tiếng Anh", "AT_Copy_Va_Dich_Tieng_Anh", "Sao chép văn bản thuyết minh trên bản vẽ và dịch tự động sang Tiếng Anh");
            Add(A3, "Liên kết nội dung giữa các Text", "AT_Text_Link", "Gán nội dung từ Text nguồn sang các Text đích được chọn");
            Add(A3, "Đánh số thứ tự cho Text (Form)", "AT_Danh_So_Thu_Tu_Text", "Đánh số thứ tự tăng dần cho các đối tượng Text/MText với tiền tố và hậu tố");
            Add(A3, "Đánh số thứ tự cho Text (Chọn từng Text)", "AT_Danh_So_Thu_Tu_Legacy", "Đánh số thứ tự nhanh cho từng Text được chọn");
            Add(A3, "Đồng bộ Text giữa Model & Layout", "AT_Text_Layout", "Xoay các Text/MText có sẵn nằm ngang chuẩn theo góc khung nhìn Layout");
            Add(A3, "Tạo mới Text Layout", "AT_Tao_Moi_Text_Layout", "Tạo chữ mới tự động xoay góc và căn chỉnh theo khung nhìn Layout");
            Add(A3, "Tạo nhãn cao độ từ Text", "AT_Label_From_Text", "Chuyển đổi đối tượng Text thường thành nhãn Label có liên kết");
            Add(A3, "Phá khối Text thành nét vẽ (Explode Text)", "AT_Explode_Text", "Phá vỡ phông chữ Text thành các đường Polyline hình học");

            const string A4 = "AutoCAD — 4. 3D Solid & Hình học";
            Add(A4, "Tạo 3D Solid từ các Polyline", "AT_Polys_To_Solid", "Extrude hàng loạt các đường Polyline kín thành khối 3D Solid theo chiều cao");
            Add(A4, "Chuyển đổi Text thành 3D Solid", "AT_Text_To_Solid", "Đùn chữ Text/MText thành chữ nổi 3D Solid với chiều dày tùy chọn");
            Add(A4, "Tạo bề mặt Surface từ Polyline đóng", "AT_Surface_From_Polyline", "Chuyển các đường Polyline khép kín thành mặt phẳng 3D");
            Add(A4, "Cắt 3D Solid theo Polyline mặt bằng", "AT_Clip_3d_Solid", "Cắt xén (Boolean Intersect/Subtract) các khối 3D Solid theo đường biên Polyline 2D");
            Add(A4, "Cắt 3D Solid / Body vào Clipboard", "AT_Cut_Solid_Body_To_Clipboard", "Cắt phần giao 3D Solid/Body trong vùng chọn và sao chép vào Clipboard");
            Add(A4, "Xóa nhanh các đối tượng 3D Solid / Body", "AT_Xoa_Doi_Tuong_3d_Solid_Body", "Lọc và xóa nhanh các khối 3D Solid và Body trong vùng chọn");
            Add(A4, "Tạo đường bao ngoài (Outline)", "AT_Tao_Outline", "Tự động tạo đường biên khép kín bao quanh các đối tượng được chọn");
            Add(A4, "Offset đối tượng sang 2 bên", "AT_Offset_2_Ben", "Offset một đường tim sang cả 2 phía trái và phải với khoảng cách chỉ định");
            Add(A4, "Sao chép dãy đối tượng (Copy Array)", "AT_Copy_Array", "Sao chép và rải đều đối tượng theo khoảng cách hoặc số lượng");
            Add(A4, "Ghi kích thước đường cong (Dim Arc/Curve)", "AT_Dim_Duong_Cong", "Đo và ghi kích thước chiều dài cung cong cho Arc hoặc phân đoạn Polyline");

            const string A5 = "AutoCAD — 5. Block & Thư viện mẫu";
            Add(A5, "Tạo Block riêng cho từng đối tượng", "AT_Tao_Block_Tung_Doi_Tuong", "Đóng gói từng đối tượng rời rạc thành một Block độc lập tại tâm của nó");
            Add(A5, "Đánh số thứ tự Attribute cho Block", "AT_Danh_So_Thu_Tu_Cho_Block", "Tự động đánh số thứ tự vào thuộc tính Attribute của các Block theo hàng/cột");
            Add(A5, "Chèn & quản lý thư viện đối tượng mẫu", "AT_Them_Doi_Tuong_Mau", "Chèn bảng mẫu quy chuẩn Layer, nét vẽ, chữ, kích thước và mẫu Hatch");

            const string A6 = "AutoCAD — 6. Viewport, Layout & Xoay";
            Add(A6, "Bố trí Viewport theo hình chữ nhật", "AT_Bo_Tri_Viewport_Theo_Hinh", "Tự động tạo các Layout và Viewport bám theo dãy khung in chữ nhật bên Model");
            Add(A6, "Bố trí Viewport theo 2 điểm chọn", "AT_Bo_Tri_Viewport_Theo_2_Diem", "Tạo Viewport trên Layout và tự động xoay hướng nhìn theo 2 điểm chọn bên Model");
            Add(A6, "Xoay Viewport hiện hành theo 2 điểm", "AT_Xoay_Viewport_Theo_2_Diem", "Xoay góc nhìn DVIEW/Twist của Viewport dựa theo 2 điểm chuẩn");
            Add(A6, "Tắt VP Freeze Layer theo đối tượng chọn", "AT_Tat_VP_Freeze_Layer", "Chọn đối tượng để đóng băng Layer tương ứng trong Viewport hiện hành hoặc hàng loạt");
            Add(A6, "Xoay đối tượng theo góc Viewport", "AT_Xoay_Doi_Tuong_Theo_Viewport", "Xoay Block/Text bên Model cho thẳng đứng với góc xoay của Viewport");
            Add(A6, "Xoay đối tượng theo góc Viewport (V2)", "AT_Xoay_Doi_Tuong_Theo_Viewport_V2", "Đồng bộ góc xoay nhiều đối tượng theo hệ tọa độ của Viewport");
            Add(A6, "Xoay đối tượng theo 2 điểm", "AT_Xoay_Doi_Tuong_Theo_2_Diem", "Xoay các đối tượng được chọn theo hướng vector xác định bởi 2 điểm");
            Add(A6, "Chuyển đổi kích thước Dim sang Layout", "AT_Dim_Layout", "Ghi kích thước chuẩn theo hệ trục của khung nhìn Layout");
            Add(A6, "Chuyển đổi kích thước Dim sang Layout (V2)", "AT_Dim_Layout_2", "Ghi kích thước liên hoàn theo khung nhìn Layout");
            Add(A6, "Chuyển đổi Block sang Layout", "AT_Block_Layout", "Chèn và căn chỉnh Block khung tên đồng loạt trên các Layout");
            Add(A6, "Cập nhật khung nhìn Layout", "AT_Update_Layout", "Làm mới và đồng bộ thông tin trên tất cả các tab Layout");

            const string A7 = "AutoCAD — 7. Quản lý Xref";
            Add(A7, "Gắn Xref tất cả file trong thư mục (Attach)", "AT_Xref_All", "Đính kèm toàn bộ các bản vẽ trong thư mục vào file hiện hành kiểu Attach");
            Add(A7, "Gắn Xref tất cả file trong thư mục (Overlay)", "AT_Xref_All_Overlay", "Đính kèm toàn bộ các bản vẽ trong thư mục vào file hiện hành kiểu Overlay");
            Add(A7, "Chuyển đổi các Xref đã nạp thành Block (Bind)", "AT_Xref_To_Block", "Bind/Insert các bản vẽ tham chiếu Xref thành Block nằm trực tiếp trong bản vẽ");
            Add(A7, "Chuyển Xref từ Attach sang Overlay (Hiện hành)", "AT_Xref_Attach_To_Overlay", "Chuyển toàn bộ các Xref đang ở kiểu Attach sang kiểu Overlay để tránh vòng lặp");
            Add(A7, "Chuyển Xref từ Attach sang Overlay (Hàng loạt file)", "AT_Xref_Attach_To_Overlay_File", "Xử lý hàng loạt nhiều file DWG để đổi kiểu Xref từ Attach sang Overlay");

            const string A8 = "AutoCAD — 8. Bảng biểu, Xuất dữ liệu & In ấn";
            Add(A8, "Chuẩn hóa định dạng bảng (Table)", "AT_Chuan_Hoa_Bang", "Định dạng lại phông chữ, chiều cao chữ, căn lề và đường viền bảng CAD theo chuẩn");
            Add(A8, "Xuất bảng CAD ra file Excel", "AT_Xuat_Bang_Cad_Ra_Excel", "Nhận diện cả bảng AutoCAD Table lẫn bảng kẻ bằng Line/Text để xuất sang Excel");
            Add(A8, "Xuất bảng tọa độ đỉnh Polyline", "AT_Xuat_Bang_Toa_Do_Polyline", "Lập bảng thống kê tọa độ X, Y và chiều dài các đỉnh của đường Polyline");
            Add(A8, "Xuất đối tượng sang Google Earth (KML/KMZ)", "AT_Export_Kml_Custom", "Xuất bình đồ tuyến và đối tượng CAD sang file KML/KMZ");
            Add(A8, "In hàng loạt bản vẽ từ Model", "AT_In_Model_Hang_Loat", "Tự động nhận diện khung in bên Model, sắp xếp thứ tự và xuất PDF hàng loạt");
            Add(A8, "In hàng loạt bản vẽ theo Block khung tên", "AT_In_Ban_Ve_Theo_Block", "Quét các Block khung tên, đọc thuộc tính số hiệu bản vẽ và in PDF hàng loạt");

            const string A9 = "AutoCAD — 9. Hệ thống, Palette & Nạp lại Code";
            Add(A9, "Mở bảng lệnh Tool Palette", "AT_Show_Palette", "Mở hoặc hiển thị lại bảng lệnh Tool Palette (SHOW_PALETTE)");
            Add(A9, "Nạp lại dữ liệu Tool Palette", "AT_Reload_Palette", "Đọc lại danh sách lệnh từ file ToolPalette.json / mặc định (RELOAD_PALETTE)");
            Add(A9, "Cấu hình nguồn dữ liệu Tool Palette", "AT_Palette_Config", "Chọn file JSON/Excel cấu hình hoặc khôi phục mặc định (PALETTE_CONFIG)");
            Add(A9, "Ẩn bảng lệnh Tool Palette", "AT_Hide_Palette", "Tạm ẩn bảng lệnh Tool Palette (HIDE_PALETTE)");
            Add(A9, "Khởi động máy chủ kết nối AI (MCP Bridge)", "AT_Khoi_Dong_Mcp_Bridge", "Khởi động lại kết nối MCP Bridge giữa AutoCAD/Civil 3D và AI Agent");
            Add(A9, "Làm mới thanh Menu lệnh", "AT_Show_Menu", "Tạo lại thanh menu Civil tool & Acad tool trên thanh Menu Bar");
            Add(A9, "Tự động Build & nạp lại Code mới", "RELOAD", "Tải lại bản build Civil3D_Tools.dll mới nhất không cần tắt AutoCAD (NETRELOAD)");
            Add(A9, "Bảng điều khiển nạp lại DLL", "RELOADUI", "Mở hộp thoại chọn đường dẫn và cấu hình tự động nạp lại DLL (NETRELOADUI)");

            return list;
        }
    }
}

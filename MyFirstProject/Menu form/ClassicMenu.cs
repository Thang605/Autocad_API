using System;
using Autodesk.AutoCAD.Runtime;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(MyFirstProject.ClassicMenu))]

namespace MyFirstProject
{
    public class ClassicMenu
    {
        private static bool _idleSubscribed;

        /// <summary>
        /// Tự động đăng ký nạp thanh Menu ngay khi AutoCAD khởi tạo xong hoặc sau khi RELOAD DLL.
        /// </summary>
        public static void EnsureMenuLoaded()
        {
            try
            {
                if (_idleSubscribed) return;
                _idleSubscribed = true;
                AcadApp.Idle += OnIdleLoadMenu;
            }
            catch
            {
                _idleSubscribed = false;
            }
        }

        private static void OnIdleLoadMenu(object? sender, EventArgs e)
        {
            try
            {
                if (AcadApp.DocumentManager.MdiActiveDocument == null) return;
                AcadApp.Idle -= OnIdleLoadMenu;
                _idleSubscribed = false;
                ShowMenu();
            }
            catch
            {
                AcadApp.Idle -= OnIdleLoadMenu;
                _idleSubscribed = false;
            }
        }

        [CommandMethod("AT_Show_Menu")]
        public static void ShowMenu()
        {
            try
            {
                object? menuBarVar = AcadApp.GetSystemVariable("MENUBAR");
                if (menuBarVar != null && Convert.ToInt16(menuBarVar) == 0)
                {
                    AcadApp.SetSystemVariable("MENUBAR", (short)1);
                }
            }
            catch { }

            CreateMenuGeneric("Civil tool", BuildCivilToolStructure);
            CreateMenuGeneric("Acad tool", BuildAcadToolStructure);
        }

        private static void CreateMenuGeneric(string menuName, Action<dynamic> buildAction)
        {
            try
            {
                dynamic acadApp = AcadApp.AcadApplication;
                dynamic menuBar = acadApp.MenuBar;
                dynamic popupMenus = acadApp.MenuGroups.Item(0).Menus;

                // 1. Gỡ menu cũ trên thanh MenuBar nếu đã tồn tại
                try
                {
                    for (int i = menuBar.Count - 1; i >= 0; i--)
                    {
                        if (menuBar.Item(i).Name == menuName)
                        {
                            menuBar.Item(i).RemoveFromMenuBar();
                        }
                    }
                }
                catch { }

                // 2. Tìm hoặc tạo mới PopupMenu trong MenuGroup chính
                dynamic? targetMenu = null;
                bool exists = false;
                foreach (dynamic menu in popupMenus)
                {
                    if (menu.Name == menuName)
                    {
                        targetMenu = menu;
                        exists = true;
                        while (targetMenu.Count > 0)
                        {
                            targetMenu.Item(0).Delete();
                        }
                        break;
                    }
                }

                if (!exists || targetMenu == null)
                {
                    targetMenu = popupMenus.Add(menuName);
                }

                // 3. Xây dựng cấu trúc cây Menu
                buildAction(targetMenu);

                // 4. Đưa lên thanh MenuBar
                targetMenu.InsertInMenuBar(menuBar.Count + 1);

                AcadApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage($"\n✓ Đã cập nhật thanh menu '{menuName}' thành công.");
            }
            catch (System.Exception ex)
            {
                AcadApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage($"\n❌ Lỗi khi tạo menu '{menuName}': {ex.Message}");
            }
        }

        private static void BuildCivilToolStructure(dynamic menu)
        {
            // 1. Alignment (Tuyến đường)
            dynamic subMenuAlignment = menu.AddSubMenu(menu.Count + 1, "1. Alignment (Tuyến đường)");
            AddHeader(subMenuAlignment, "--- Thiết kế & Bố trí tuyến ---");
            AddMenuItem(subMenuAlignment, "Bố Trí Đường Cong Nằm", "CTA_Bo_Tri_Cong_Nam");
            AddMenuItem(subMenuAlignment, "Điều Chỉnh Bán Kính Connected Alignment", "CTA_Dieu_Chinh_Ban_Kinh_Connected_Alignment");
            AddMenuItem(subMenuAlignment, "Tạo Tuyến Offset (Offset Alignment)", "CTA_Offset_Alignment");
            AddMenuItem(subMenuAlignment, "Đề Xuất Cắt Ngang Tuyến", "CTA_De_Xuat_Cat_Ngang");
            AddHeader(subMenuAlignment, "--- Kiểm tra tiêu chuẩn & Tầm nhìn ---");
            AddMenuItem(subMenuAlignment, "Kiểm Tra Tim Tuyến Theo Tiêu Chuẩn", "CTA_Kiem_Tra_Tim_Tuyen");
            AddMenuItem(subMenuAlignment, "Kiểm Tra Tầm Nhìn (Sight Distance)", "CTA_Kiem_Tra_Tam_Nhin");
            AddHeader(subMenuAlignment, "--- Gắn nhãn & Thống kê ---");
            AddMenuItem(subMenuAlignment, "Gắn Nhãn Đầu Cuối Tuyến", "CTA_Gan_Nhan_Dau_Cuoi_Tuyen");
            AddMenuItem(subMenuAlignment, "Bảng Thống Kê Các Tuyến Đường", "CTA_Bang_Thong_Ke_Cac_Tuyen_Duong");

            // 2. Profile (Trắc dọc)
            dynamic subMenuProfile = menu.AddSubMenu(menu.Count + 1, "2. Profile (Trắc dọc)");
            AddHeader(subMenuProfile, "--- Vẽ trắc dọc tự nhiên ---");
            AddMenuItem(subMenuProfile, "Vẽ Trắc Dọc Tự Nhiên (1 Tuyến)", "CTP_Ve_Trac_Doc_Tu_Nhien");
            AddMenuItem(subMenuProfile, "Vẽ Trắc Dọc Tự Nhiên (Tất Cả Tuyến)", "CTP_Ve_Trac_Doc_Tu_Nhien_Tat_Ca_Tuyen");
            AddHeader(subMenuProfile, "--- Thiết kế Đường đỏ & Cong đứng ---");
            AddMenuItem(subMenuProfile, "Điều Chỉnh Đường Đỏ Thiết Kế", "CTP_Dieu_Chinh_Duong_Do");
            AddMenuItem(subMenuProfile, "Bố Trí Đường Cong Đứng", "CTP_Bo_Tri_Cong_Dung");
            AddMenuItem(subMenuProfile, "Kiểm Tra Profile Theo Tiêu Chuẩn", "CTP_Kiem_Tra_Profile");
            AddHeader(subMenuProfile, "--- Hiệu chỉnh & Gắn nhãn trên Trắc dọc ---");
            AddMenuItem(subMenuProfile, "Sửa Đường Tự Nhiên Theo Cọc", "CTP_Fix_Duong_Tu_Nhien_Theo_Coc");
            AddMenuItem(subMenuProfile, "Bổ Sung Cọc Trên Trắc Dọc", "CTP_Bo_Sung_Coc_Tren_Trac_Doc");
            AddMenuItem(subMenuProfile, "Gắn Nhãn Nút Giao Lên Trắc Dọc", "CTP_Gan_Nhan_Nut_Giao_Len_Trac_Doc");
            AddMenuItem(subMenuProfile, "Tạo CogoPoint Từ Đỉnh PVI", "CTP_Tao_Cogo_Point_Tu_PVI");
            AddHeader(subMenuProfile, "--- Chuyển đổi Polyline & Profile ---");
            AddMenuItem(subMenuProfile, "Chuyển Polyline Thành Profile", "CTP_Polyline_To_Profile");
            AddMenuItem(subMenuProfile, "Xuất Profile Thành Polyline", "CTP_Profile_To_Polyline");
            AddMenuItem(subMenuProfile, "Điều Chỉnh Profile Theo Polyline", "CTP_Adjust_Profile_By_Polyline");
            AddHeader(subMenuProfile, "--- Profile Band ---");
            AddMenuItem(subMenuProfile, "Thay Đổi Profile Trong Band", "CTP_Thay_Doi_Profile_Band");
            AddMenuItem(subMenuProfile, "Dịch Chuyển Nhãn Chẵn Trong Band", "CTP_Dich_Chuyen_Label_Chan");

            // 3. Corridor (Hành lang tuyến)
            dynamic subMenuCorridor = menu.AddSubMenu(menu.Count + 1, "3. Corridor (Hành lang tuyến)");
            AddHeader(subMenuCorridor, "--- Khởi tạo & Xây dựng Corridor ---");
            AddMenuItem(subMenuCorridor, "Tạo Corridor Cho Tuyến Đường", "CTC_Tao_Corridor_Cho_Tuyen_Duong");
            AddMenuItem(subMenuCorridor, "Tạo Corridor Theo Từng Cấu Kiện (Modular)", "CTC_Tao_Corridor_Theo_Cau_Kien");
            AddMenuItem(subMenuCorridor, "Tạo Corridor Đường Đô Thị Rẽ Phải", "CTC_Tao_Corridor_Duong_Do_Thi_Re_Phai");
            AddHeader(subMenuCorridor, "--- Thiết lập Section & Target ---");
            AddMenuItem(subMenuCorridor, "Thêm Section Cho Tất Cả Region", "CTC_Them_Section_Tat_Ca_Region");
            AddMenuItem(subMenuCorridor, "Thêm Tất Cả Section (SampleLine)", "CTC_Add_All_Section");
            AddMenuItem(subMenuCorridor, "Cấu Hình Target Cho Subassembly Corridor", "CTC_Cau_Hinh_Target_Corridor");
            AddMenuItem(subMenuCorridor, "Xem Thông Tin Target Corridor", "CTC_Thong_Tin_Target_Corridor");
            AddHeader(subMenuCorridor, "--- Phân đoạn & Hiệu chỉnh Region ---");
            AddMenuItem(subMenuCorridor, "Điều Chỉnh Phân Đoạn Corridor", "CTC_Dieu_Chinh_Phan_Doan");
            AddMenuItem(subMenuCorridor, "Chọn Đoạn Corridor (Multi-Region)", "CTC_Chon_Doan_Corridor");
            AddMenuItem(subMenuCorridor, "Phân Chia Corridor Theo Profile", "CTC_Phan_Chia_Corridor_Theo_Profile");
            AddMenuItem(subMenuCorridor, "Nhân Bản Corridor (Copy)", "CTC_Nhan_Ban_Corridor");
            AddMenuItem(subMenuCorridor, "Bật/Tắt & Chia Nhỏ Region Theo Polyline", "CTC_Bat_Tat_Corridor_Region_Polyline");
            AddMenuItem(subMenuCorridor, "Xóa Các Region Đang Bị Tắt", "CTC_Xoa_Region_Bi_Tat");
            AddHeader(subMenuCorridor, "--- Corridor Surface ---");
            AddMenuItem(subMenuCorridor, "Tạo Corridor Surface (Hàng Loạt / Form)", "CTC_Tao_Corridor_Surface");
            AddMenuItem(subMenuCorridor, "Tạo Corridor Surface (Chọn Đơn)", "CTC_Tao_Corridor_Surface_Don");
            AddHeader(subMenuCorridor, "--- Xuất 3D Solid / Ranh giới & Subassembly .NET ---");
            AddMenuItem(subMenuCorridor, "Xuất 3D Solid Từ Corridor", "CTC_Xuat_Solid_Tu_Corridor");
            AddMenuItem(subMenuCorridor, "Ngắt Liên Kết & Xóa Solid/Body Corridor", "CTC_Ngat_Lien_Ket_Solid_Corridor");
            AddMenuItem(subMenuCorridor, "Trích Xuất Ranh Giới Corridor", "CTC_Trich_Xuat_Ranh_Gioi_Corridor");
            AddMenuItem(subMenuCorridor, "Quản Lý & Tạo Subassembly .NET (C# / Stock)", "CTC_Quan_Ly_Subassembly_DotNet");

            // 4. Sample Line (Cọc & Lý trình)
            dynamic subMenuSample = menu.AddSubMenu(menu.Count + 1, "4. Sample Line (Cọc & Lý trình)");
            AddHeader(subMenuSample, "--- Phát sinh & Chèn cọc ---");
            AddMenuItem(subMenuSample, "Phát Sinh Cọc Tự Động Trên Tuyến", "CTS_Phat_Sinh_Coc");
            AddMenuItem(subMenuSample, "Phát Sinh Cọc Thủ Công", "CTS_Phat_Sinh_Coc_Thu_Cong");
            AddMenuItem(subMenuSample, "Phát Sinh Cọc Theo Bảng", "CTS_Phat_Sinh_Coc_Theo_Bang");
            AddMenuItem(subMenuSample, "Phát Sinh Cọc Theo Khoảng Delta", "CTS_Phat_Sinh_Coc_Theo_Khoang_Delta");
            AddMenuItem(subMenuSample, "Phát Sinh Cọc Từ CogoPoint", "CTS_Phat_Sinh_Coc_Tu_Cogo_Point");
            AddMenuItem(subMenuSample, "Chèn Cọc Trên Trắc Dọc", "CTS_Chen_Coc_Tren_Trac_Doc");
            AddMenuItem(subMenuSample, "Chèn Cọc Trên Trắc Ngang", "CTS_Chen_Coc_Tren_Trac_Ngang");
            AddMenuItem(subMenuSample, "Bổ Sung Cọc Từ Data Shortcut", "CTS_Bo_Sung_Coc_Data_Shortcut");
            AddMenuItem(subMenuSample, "Bổ Sung Cọc Từ Data Shortcut (Ref)", "CTS_Bo_Sung_Coc_Data_Shortcut_Ref");
            AddHeader(subMenuSample, "--- Đổi tên cọc ---");
            AddMenuItem(subMenuSample, "Đổi Tên Cọc Theo Quy Tắc (Form)", "CTS_Doi_Ten_Coc_Quy_Tac");
            AddMenuItem(subMenuSample, "Đổi Tên Cọc Cơ Bản", "CTS_Doi_Ten_Coc");
            AddMenuItem(subMenuSample, "Đổi Tên Cọc Theo Đoạn", "CTS_Doi_Ten_Coc_2");
            AddMenuItem(subMenuSample, "Đổi Tên Cọc Theo Lý Trình Km", "CTS_Doi_Ten_Coc_3");
            AddMenuItem(subMenuSample, "Đổi Tên Cọc H (Hectomet)", "CTS_Doi_Ten_Coc_H");
            AddMenuItem(subMenuSample, "Đổi Tên Cọc Theo Thứ Tự", "CTS_Doi_Ten_Coc_Theo_Thu_Tu");
            AddMenuItem(subMenuSample, "Đổi Tên Cọc Từ CogoPoint", "CTS_Doi_Ten_Coc_Tu_Cogo_Point");
            AddHeader(subMenuSample, "--- Dịch chuyển, Sao chép & Đồng bộ cọc ---");
            AddMenuItem(subMenuSample, "Dịch Cọc Tịnh Tiến (Tùy Chọn)", "CTS_Dich_Coc_Tinh_Tien");
            AddMenuItem(subMenuSample, "Dịch Cọc Tịnh Tiến 20m", "CTS_Dich_Coc_Tinh_Tien_20");
            AddMenuItem(subMenuSample, "Dịch Cọc Tịnh Tiến 40m", "CTS_Dich_Coc_Tinh_Tien_40");
            AddMenuItem(subMenuSample, "Sao Chép Nhóm Cọc (Copy Group)", "CTS_Copy_Nhom_Coc");
            AddMenuItem(subMenuSample, "Đồng Bộ 2 Nhóm Cọc", "CTS_Dong_Bo_2_Nhom_Coc");
            AddMenuItem(subMenuSample, "Đồng Bộ 2 Nhóm Cọc Theo Đoạn", "CTS_Dong_Bo_2_Nhom_Coc_Theo_Doan");
            AddHeader(subMenuSample, "--- Bề rộng & Hiệu chỉnh hình học cọc ---");
            AddMenuItem(subMenuSample, "Thay Đổi Bề Rộng Sample Line", "CTS_Thay_Doi_Be_Rong_Sample_Line");
            AddMenuItem(subMenuSample, "Sao Chép Bề Rộng Sample Line", "CTS_Copy_Be_Rong_Sample_Line");
            AddMenuItem(subMenuSample, "Offset Bề Rộng Sample Line", "CTS_Offset_Be_Rong_Sample_Line");
            AddMenuItem(subMenuSample, "Hiệu Chỉnh Khoảng Cách Cọc", "CTS_Hieu_Chinh_Khoang_Cach_Coc");
            AddMenuItem(subMenuSample, "Điều Chỉnh Cọc Vuông Góc Tuyến (Orthogonal)", "CTS_Dieu_Chinh_Orthogonal");
            AddHeader(subMenuSample, "--- Bảng tọa độ cọc & Xuất dữ liệu ---");
            AddMenuItem(subMenuSample, "Xuất Bảng Tọa Độ Cọc (Form Đầy Đủ)", "CTS_Xuat_Toa_Do_Coc");
            AddMenuItem(subMenuSample, "Tạo Bảng Tọa Độ Cọc (Mẫu 1)", "CTS_Tao_Bang_Toa_Do_Coc");
            AddMenuItem(subMenuSample, "Tạo Bảng Tọa Độ Cọc (Mẫu 2 - Lý Trình)", "CTS_Tao_Bang_Toa_Do_Coc_2");
            AddMenuItem(subMenuSample, "Tạo Bảng Tọa Độ Cọc (Mẫu 3 - Cao Độ)", "CTS_Tao_Bang_Toa_Do_Coc_3");
            AddMenuItem(subMenuSample, "Cập Nhật 2 Bảng Tọa Độ Cọc", "CTS_Cap_Nhat_2_Bang_Toa_Do_Coc");

            // 5. Section View (Trắc ngang & Khối lượng)
            dynamic subMenuSection = menu.AddSubMenu(menu.Count + 1, "5. Section View (Trắc ngang)");
            AddHeader(subMenuSection, "--- Vẽ & Bổ sung cắt ngang ---");
            AddMenuItem(subMenuSection, "Vẽ Trắc Ngang Thiết Kế", "CTS_Ve_Trac_Ngang_Thiet_Ke");
            AddMenuItem(subMenuSection, "Bổ Sung Section Trên Trắc Ngang", "CTS_Bo_Sung_Cat_Ngang");
            AddHeader(subMenuSection, "--- Tính toán & Vẽ đánh cấp ---");
            AddMenuItem(subMenuSection, "Tính Toán & Vẽ Đánh Cấp Tự Động", "CTS_Danh_Cap");
            AddMenuItem(subMenuSection, "Cập Nhật Đánh Cấp", "CTS_Danh_Cap_Cap_Nhat");
            AddMenuItem(subMenuSection, "Vẽ Thêm Đánh Cấp (Tùy Chọn)", "CTS_Danh_Cap_Ve_Them");
            AddMenuItem(subMenuSection, "Vẽ Thêm Đánh Cấp 1m", "CTS_Danh_Cap_Ve_Them_1");
            AddMenuItem(subMenuSection, "Vẽ Thêm Đánh Cấp 2m", "CTS_Danh_Cap_Ve_Them_2");
            AddMenuItem(subMenuSection, "Xóa Bỏ Đánh Cấp", "CTS_Danh_Cap_Xoa_Bo");
            AddHeader(subMenuSection, "--- Hiệu chỉnh Section & Hiển thị ---");
            AddMenuItem(subMenuSection, "Hiệu Chỉnh Section (Static)", "CTS_Hieu_Chinh_Section");
            AddMenuItem(subMenuSection, "Hiệu Chỉnh Section (Dynamic)", "CTS_Hieu_Chinh_Section_Dynamic");
            AddMenuItem(subMenuSection, "Chọn Nhanh Section Static", "CTS_Chon_Section_Static");
            AddMenuItem(subMenuSection, "Điều Chỉnh Đường Tự Nhiên Trên Trắc Ngang", "CTS_Dieu_Chinh_Duong_Tu_Nhien");
            AddMenuItem(subMenuSection, "Chuyển Đổi TN-TK Sang TN-TN", "CTS_Chuyen_Doi_TNTK_TNTN");
            AddMenuItem(subMenuSection, "Khóa Cắt Ngang & Thêm Điểm", "CTS_Khoa_Cat_Ngang_Add_Point");
            AddMenuItem(subMenuSection, "Ẩn Đường Địa Chất Trên Trắc Ngang", "CTS_An_Duong_Dia_Chat");
            AddMenuItem(subMenuSection, "Hiệu Chỉnh Nhãn Text Chồng Đè", "CTS_Hieu_Chinh_Text_Chong");
            AddMenuItem(subMenuSection, "Tạo Polyline Từ Section", "CTS_Polyline_Tu_Section");
            AddHeader(subMenuSection, "--- Thiết lập Khung in, MSS & Giới hạn ---");
            AddMenuItem(subMenuSection, "Sắp Xếp Layout Section View", "CTS_Sap_Xep_Layout");
            AddMenuItem(subMenuSection, "Fit Khung In Trắc Ngang", "CTS_Fit_Khung_In");
            AddMenuItem(subMenuSection, "Thay Đổi Khung In Trắc Ngang", "CTS_Thay_Doi_Khung_In");
            AddMenuItem(subMenuSection, "Thay Đổi Cao Độ MSS", "CTS_Thay_Doi_MSS");
            AddMenuItem(subMenuSection, "Thay Đổi MSS Min/Max", "CTS_Thay_Doi_MSS_Min_Max");
            AddMenuItem(subMenuSection, "Thay Đổi Giới Hạn Trái/Phải", "CTS_Thay_Doi_Gioi_Han_Trai_Phai");
            AddHeader(subMenuSection, "--- Vật liệu (Material List) & Khối lượng ---");
            AddMenuItem(subMenuSection, "Thêm & Cấu Hình Material List", "CTS_Them_Material_List");
            AddMenuItem(subMenuSection, "Xem Thông Tin Material List", "CTS_Xem_Material_List");
            AddMenuItem(subMenuSection, "Xóa Material List", "CTS_Xoa_Material_List");
            AddMenuItem(subMenuSection, "Thêm Vật Liệu Trên Cắt Ngang", "CTS_Them_Vat_Lieu_Tren_Cat_Ngang");
            AddMenuItem(subMenuSection, "Thêm Bảng Khối Lượng Cắt Ngang", "CTS_Them_Bang_KL_Cat_Ngang");
            AddMenuItem(subMenuSection, "Tính Tổng Chiều Dài Trên Cắt Ngang", "CTS_Tong_Chieu_Dai_Cat_Ngang");
            AddMenuItem(subMenuSection, "Tính Khối Lượng Cắt Ngang", "CTS_Khoi_Luong_Cat_Ngang");
            AddMenuItem(subMenuSection, "Xuất Khối Lượng Trắc Ngang Ra Excel", "CTS_Xuat_Khoi_Luong_Ra_Excel");

            // 6. Surface & Feature Line (Bề mặt & Đường đặc trưng)
            dynamic subMenuSurface = menu.AddSubMenu(menu.Count + 1, "6. Surface & Feature Line");
            AddHeader(subMenuSurface, "--- Bề mặt TIN Surface ---");
            AddMenuItem(subMenuSurface, "Tạo Mặt Phẳng Ghép EG - TK", "CTT_Tao_Mat_Phang_EG_TK");
            AddMenuItem(subMenuSurface, "Điều Chỉnh Mặt Phẳng EG - TK (Tạo Polyline Sửa)", "CTT_Dieu_Chinh_EG_TK");
            AddMenuItem(subMenuSurface, "Áp Dụng Chỉnh Sửa Mặt Phẳng EG - TK", "CTT_Dieu_Chinh_EG_TK_Apply");
            AddMenuItem(subMenuSurface, "Xem Trạng Thái Chờ Sửa EG - TK", "CTT_Dieu_Chinh_EG_TK_Status");
            AddMenuItem(subMenuSurface, "Hủy Các Polyline Tạm EG - TK", "CTT_Dieu_Chinh_EG_TK_Cancel");
            AddMenuItem(subMenuSurface, "Lấy Cao Độ Mặt Phẳng Tại CogoPoint", "CTT_Cao_Do_Mat_Phang_Tai_Cogo_Point");
            AddMenuItem(subMenuSurface, "Tạo Spot Elevation Trên Surface Tại Tim", "CTT_Tao_Spot_Elevation_Tai_Tim");
            AddMenuItem(subMenuSurface, "Trích Xuất 3D Solid Kết Cấu Áo Đường Từ Mặt Phẳng", "CTT_Xuat_Solid_KCAD_Tu_Mat_Phang");
            AddHeader(subMenuSurface, "--- Feature Line ---");
            AddMenuItem(subMenuSurface, "Tạo Mặt Phẳng (Surface) Từ Feature Line", "CTT_Tao_Mat_Phang_Tu_Feature_Line");
            AddMenuItem(subMenuSurface, "Tạo 3D Solid Từ Feature Line", "CT_Tao_3d_Solid_Tu_Feature_Line");

            // 7. Point (Điểm CogoPoint)
            dynamic subMenuPoint = menu.AddSubMenu(menu.Count + 1, "7. Point (CogoPoint)");
            AddHeader(subMenuPoint, "--- Tạo CogoPoint ---");
            AddMenuItem(subMenuPoint, "Tạo CogoPoint Đa Nguồn (Text/Circle/Point/Table/Excel)", "CT_Tao_Cogo_Point_Tong_Hop");
            AddMenuItem(subMenuPoint, "Tạo CogoPoint Lấy Cao Độ Từ Surface", "CT_Tao_Cogo_Point_Cao_Do_Tu_Surface");
            AddMenuItem(subMenuPoint, "Tạo CogoPoint Từ Spot Elevation", "CT_Tao_Cogo_Point_Tu_Elevation_Spot");
            AddMenuItem(subMenuPoint, "Tạo CogoPoint Từ Text Cao Độ", "CT_Tao_Cogo_Point_Tu_Text");
            AddMenuItem(subMenuPoint, "Tạo CogoPoint Từ File Excel", "CT_Tao_Cogo_Point_Tu_Excel");
            AddHeader(subMenuPoint, "--- Đổi tên, Mô tả & Quản lý Point Group ---");
            AddMenuItem(subMenuPoint, "Đổi Tên CogoPoint Theo Mẫu (Template)", "CT_Doi_Ten_Cogo_Point");
            AddMenuItem(subMenuPoint, "Đổi Tên CogoPoint Theo Tuyến Alignment", "CT_Doi_Ten_Cogo_Point_Tu_Alignment");
            AddMenuItem(subMenuPoint, "Thêm Mô Tả (Description) Cho CogoPoint", "CT_Them_Mo_Ta_Cogo_Point");
            AddMenuItem(subMenuPoint, "Cập Nhật Tất Cả Point Group", "CT_Update_All_Point_Group");
            AddMenuItem(subMenuPoint, "Ẩn CogoPoint Được Chọn", "CT_An_Cogo_Point");

            // 8. Pipe Network (Mạng lưới thoát nước)
            dynamic subMenuPipe = menu.AddSubMenu(menu.Count + 1, "8. Pipe Network (Thoát nước)");
            AddHeader(subMenuPipe, "--- Thay đổi thông số Cống & Hố thu ---");
            AddMenuItem(subMenuPipe, "Thay Đổi Đường Kính Cống", "CT_Thay_Doi_Duong_Kinh_Cong");
            AddMenuItem(subMenuPipe, "Thay Đổi Độ Dốc Cống", "CT_Thay_Doi_Do_Doc_Cong");
            AddMenuItem(subMenuPipe, "Thay Đổi Cao Độ Đáy Cống", "CT_Thay_Doi_Cao_Do_Day_Cong");
            AddMenuItem(subMenuPipe, "Điều Chỉnh Bề Mặt Tham Chiếu Cống/Hố Thu", "CT_Dieu_Chinh_Be_Mat_Tham_Chieu");
            AddHeader(subMenuPipe, "--- Bảng thống kê & Xoay Hố thu ---");
            AddMenuItem(subMenuPipe, "Lập Bảng Cao Độ Tự Nhiên Hố Thu", "CT_Bang_Cao_Do_Tu_Nhien_Ho_Thu");
            AddMenuItem(subMenuPipe, "Xoay Hố Thu Theo 2 Điểm", "CT_Xoay_Ho_Thu_Theo_2_Diem");

            // 9. Parcel (Thửa đất / GPMB)
            dynamic subMenuParcel = menu.AddSubMenu(menu.Count + 1, "9. Parcel (Thửa đất)");
            AddHeader(subMenuParcel, "--- Quản lý & Thống kê Parcel ---");
            AddMenuItem(subMenuParcel, "Thống Kê Parcel Ra Bảng CAD / Excel", "CT_Bang_Thong_Ke_Parcel");
            AddMenuItem(subMenuParcel, "Đổi Tên Parcel Theo Mẫu (Template)", "CT_Doi_Ten_Parcel");

            // 10. BIM Property Sets & Thông tin đối tượng
            dynamic subMenuProp = menu.AddSubMenu(menu.Count + 1, "10. BIM Property Sets & Thông tin");
            AddHeader(subMenuProp, "--- Gán & Cập nhật Property Set BIM ---");
            AddMenuItem(subMenuProp, "Cập Nhật Màu & PropertySet BIM (Solid & Surface)", "AT_Cap_Nhat_Mau_Va_Property_Set");
            AddMenuItem(subMenuProp, "Cập Nhật PropertySet 3D Solid Theo Layer", "AT_Solid_Update_Property_Set");
            AddMenuItem(subMenuProp, "Gán PropertySet Cho 3D Solid", "AT_Solid_Set_Property_Set");
            AddMenuItem(subMenuProp, "Xem Thông Tin Khối 3D Solid", "AT_Solid_Show_Info");
            AddHeader(subMenuProp, "--- Tra cứu đối tượng Civil 3D ---");
            AddMenuItem(subMenuProp, "Tra Cứu Thông Tin Đối Tượng Civil 3D", "CT_Thong_Tin_Doi_Tuong");
        }

        private static void BuildAcadToolStructure(dynamic menu)
        {
            // 1. Chuẩn hóa Bản vẽ, Layer & Tỷ lệ
            dynamic subMenuLayerScale = menu.AddSubMenu(menu.Count + 1, "1. Layer, Tỷ lệ & Chuẩn hóa");
            AddHeader(subMenuLayerScale, "--- Chuẩn hóa & Tỷ lệ bản vẽ ---");
            AddMenuItem(subMenuLayerScale, "Chuẩn Hóa Bản Vẽ Theo Tiêu Chuẩn T27", "AT_Chuan_Hoa_Ban_Ve");
            AddMenuItem(subMenuLayerScale, "Chuyển Đổi Tỷ Lệ Nhanh Bản Vẽ (1:N)", "AT_Chuyen_Doi_Ty_Le_Nhanh");
            AddMenuItem(subMenuLayerScale, "Giữ Lại Duy Nhất Annotative Scale Hiện Hành", "AT_Annotative_Scale_Current_Only");
            AddHeader(subMenuLayerScale, "--- Quản lý Layer & Màu BIM ---");
            AddMenuItem(subMenuLayerScale, "Gán Lệnh Tắt Số Thứ Tự Cho Layer", "AT_Gan_Lenh_Layer");
            AddMenuItem(subMenuLayerScale, "Chọn Đối Tượng Theo Layer", "AT_Chon_Theo_Layer");
            AddMenuItem(subMenuLayerScale, "Chuyển Đổi Đối Tượng Sang Layer Khác", "AT_Chuyen_Doi_Layer");
            AddMenuItem(subMenuLayerScale, "Xóa Các Đối Tượng Cùng Layer", "AT_Xoa_Doi_Tuong_Cung_Layer");
            AddMenuItem(subMenuLayerScale, "Cập Nhật Màu Layer Theo Chuẩn BIM EIR", "AT_Cap_Nhat_Mau_Layer");
            AddMenuItem(subMenuLayerScale, "Cập Nhật Màu & PropertySet BIM", "AT_Cap_Nhat_Mau_Va_Property_Set");

            // 2. Tính Tổng Độ Dài, Diện Tích & Độ Dốc
            dynamic subMenuMeasure = menu.AddSubMenu(menu.Count + 1, "2. Đo Độ dài, Diện tích & Độ dốc");
            AddHeader(subMenuMeasure, "--- Tổng độ dài ---");
            AddMenuItem(subMenuMeasure, "Tính Tổng Độ Dài (Tạo Text Mới)", "AT_Tong_Do_Dai_Full");
            AddMenuItem(subMenuMeasure, "Tính Tổng Độ Dài (Ghi Đè Text)", "AT_Tong_Do_Dai_Replace");
            AddMenuItem(subMenuMeasure, "Tính Tổng Độ Dài (Ghi Đè Nhiều Lần)", "AT_Tong_Do_Dai_Replace_2");
            AddMenuItem(subMenuMeasure, "Tính Tổng Độ Dài (Cộng Thêm Vào Text)", "AT_Tong_Do_Dai_Replace_Cong_Them");
            AddHeader(subMenuMeasure, "--- Tổng diện tích ---");
            AddMenuItem(subMenuMeasure, "Tính Tổng Diện Tích (Tạo Text Mới)", "AT_Tong_Dien_Tich_Full");
            AddMenuItem(subMenuMeasure, "Tính Tổng Diện Tích (Ghi Đè Text)", "AT_Tong_Dien_Tich_Replace");
            AddMenuItem(subMenuMeasure, "Tính Tổng Diện Tích (Ghi Đè Nhiều Lần)", "AT_Tong_Dien_Tich_Replace_2");
            AddMenuItem(subMenuMeasure, "Tính Tổng Diện Tích (Cộng Thêm Vào Text)", "AT_Tong_Dien_Tich_Replace_Cong_Them");
            AddHeader(subMenuMeasure, "--- Đo độ dốc ---");
            AddMenuItem(subMenuMeasure, "Đo Độ Dốc Giữa 2 Điểm", "AT_Do_Doc");
            AddMenuItem(subMenuMeasure, "Đo Độ Dốc Đơn Giản", "AT_Do_Doc_Simple");
            AddMenuItem(subMenuMeasure, "Đo Độ Dốc Theo Đối Tượng", "AT_Do_Doc_Object");

            // 3. Biên tập Text & Đánh số thứ tự
            dynamic subMenuText = menu.AddSubMenu(menu.Count + 1, "3. Biên tập Text & Đánh số");
            AddHeader(subMenuText, "--- Căn lề, Sao chép & Dịch thuật ---");
            AddMenuItem(subMenuText, "Căn Lề Cho Các Đối Tượng Text", "AT_Can_Le_Cho_Text");
            AddMenuItem(subMenuText, "Copy Nhanh Nội Dung Text", "AT_Copy_Noi_Dung_Text");
            AddMenuItem(subMenuText, "Copy & Dịch Text Sang Tiếng Anh", "AT_Copy_Va_Dich_Tieng_Anh");
            AddMenuItem(subMenuText, "Liên Kết Nội Dung Giữa Các Text", "AT_Text_Link");
            AddHeader(subMenuText, "--- Đánh số thứ tự ---");
            AddMenuItem(subMenuText, "Đánh Số Thứ Tự Cho Text (Form)", "AT_Danh_So_Thu_Tu_Text");
            AddMenuItem(subMenuText, "Đánh Số Thứ Tự Cho Text (Chọn Từng Text)", "AT_Danh_So_Thu_Tu_Legacy");
            AddHeader(subMenuText, "--- Text Layout, Label & Explode ---");
            AddMenuItem(subMenuText, "Đồng Bộ Text Giữa Model & Layout", "AT_Text_Layout");
            AddMenuItem(subMenuText, "Tạo Mới Text Layout", "AT_Tao_Moi_Text_Layout");
            AddMenuItem(subMenuText, "Tạo Nhãn Cao Độ Từ Text", "AT_Label_From_Text");
            AddMenuItem(subMenuText, "Phá Khối Text Thành Nét Vẽ (Explode Text)", "AT_Explode_Text");

            // 4. 3D Solid & Hình học Polyline
            dynamic subMenu3D = menu.AddSubMenu(menu.Count + 1, "4. 3D Solid & Hình học");
            AddHeader(subMenu3D, "--- Tạo & Cắt khối 3D Solid ---");
            AddMenuItem(subMenu3D, "Tạo 3D Solid Từ Các Polyline", "AT_Polys_To_Solid");
            AddMenuItem(subMenu3D, "Chuyển Đổi Text Thành 3D Solid", "AT_Text_To_Solid");
            AddMenuItem(subMenu3D, "Chuyển Đổi Text Thành 3D Solid (Bước 2)", "AT_Text_To_Solid_Step2");
            AddMenuItem(subMenu3D, "Tạo Bề Mặt Surface Từ Polyline Đóng", "AT_Surface_From_Polyline");
            AddMenuItem(subMenu3D, "Cắt 3D Solid Theo Polyline Mặt Bằng", "AT_Clip_3d_Solid");
            AddMenuItem(subMenu3D, "Cắt 3D Solid / Body Vào Clipboard", "AT_Cut_Solid_Body_To_Clipboard");
            AddMenuItem(subMenu3D, "Xóa Nhanh Các Đối Tượng 3D Solid / Body", "AT_Xoa_Doi_Tuong_3d_Solid_Body");
            AddHeader(subMenu3D, "--- Hình học, Mảng & Kích thước ---");
            AddMenuItem(subMenu3D, "Tạo Đường Bao Ngoài (Outline)", "AT_Tao_Outline");
            AddMenuItem(subMenu3D, "Offset Đối Tượng Sang 2 Bên", "AT_Offset_2_Ben");
            AddMenuItem(subMenu3D, "Sao Chép Dãy Đối Tượng (Copy Array)", "AT_Copy_Array");
            AddMenuItem(subMenu3D, "Ghi Kích Thước Đường Cong (Dim Arc/Curve)", "AT_Dim_Duong_Cong");

            // 5. Block & Thư viện mẫu
            dynamic subMenuBlock = menu.AddSubMenu(menu.Count + 1, "5. Block & Thư viện mẫu");
            AddHeader(subMenuBlock, "--- Quản lý Block & Thư viện ---");
            AddMenuItem(subMenuBlock, "Tạo Block Riêng Cho Từng Đối Tượng", "AT_Tao_Block_Tung_Doi_Tuong");
            AddMenuItem(subMenuBlock, "Đánh Số Thứ Tự Attribute Cho Block", "AT_Danh_So_Thu_Tu_Cho_Block");
            AddMenuItem(subMenuBlock, "Chèn & Quản Lý Thư Viện Đối Tượng Mẫu", "AT_Them_Doi_Tuong_Mau");

            // 6. Viewport, Layout & Xoay đối tượng
            dynamic subMenuViewport = menu.AddSubMenu(menu.Count + 1, "6. Viewport, Layout & Xoay");
            AddHeader(subMenuViewport, "--- Quản lý Viewport ---");
            AddMenuItem(subMenuViewport, "Bố Trí Viewport Theo Hình Chữ Nhật", "AT_Bo_Tri_Viewport_Theo_Hinh");
            AddMenuItem(subMenuViewport, "Bố Trí Viewport Theo 2 Điểm Chọn", "AT_Bo_Tri_Viewport_Theo_2_Diem");
            AddMenuItem(subMenuViewport, "Xoay Viewport Hiện Hành Theo 2 Điểm", "AT_Xoay_Viewport_Theo_2_Diem");
            AddMenuItem(subMenuViewport, "Tắt VP Freeze Layer Theo Đối Tượng Chọn", "AT_Tat_VP_Freeze_Layer");
            AddHeader(subMenuViewport, "--- Xoay đối tượng ---");
            AddMenuItem(subMenuViewport, "Xoay Đối Tượng Theo Góc Viewport", "AT_Xoay_Doi_Tuong_Theo_Viewport");
            AddMenuItem(subMenuViewport, "Xoay Đối Tượng Theo Góc Viewport (V2)", "AT_Xoay_Doi_Tuong_Theo_Viewport_V2");
            AddMenuItem(subMenuViewport, "Xoay Đối Tượng Theo 2 Điểm", "AT_Xoay_Doi_Tuong_Theo_2_Diem");
            AddHeader(subMenuViewport, "--- Trình bày Layout ---");
            AddMenuItem(subMenuViewport, "Chuyển Đổi Kích Thước Dim Sang Layout", "AT_Dim_Layout");
            AddMenuItem(subMenuViewport, "Chuyển Đổi Kích Thước Dim Sang Layout (V2)", "AT_Dim_Layout_2");
            AddMenuItem(subMenuViewport, "Chuyển Đổi Block Sang Layout", "AT_Block_Layout");
            AddMenuItem(subMenuViewport, "Cập Nhật Khung Nhìn Layout", "AT_Update_Layout");

            // 7. Quản lý Xref
            dynamic subMenuXref = menu.AddSubMenu(menu.Count + 1, "7. Quản lý Xref");
            AddHeader(subMenuXref, "--- Gắn & Chuyển đổi Xref ---");
            AddMenuItem(subMenuXref, "Gắn Xref Tất Cả File Trong Thư Mục (Attach)", "AT_Xref_All");
            AddMenuItem(subMenuXref, "Gắn Xref Tất Cả File Trong Thư Mục (Overlay)", "AT_Xref_All_Overlay");
            AddMenuItem(subMenuXref, "Chuyển Đổi Các Xref Đã Nạp Thành Block (Bind)", "AT_Xref_To_Block");
            AddMenuItem(subMenuXref, "Chuyển Xref Từ Attach Sang Overlay (Hiện Hành)", "AT_Xref_Attach_To_Overlay");
            AddMenuItem(subMenuXref, "Chuyển Xref Từ Attach Sang Overlay (Hàng Loạt File)", "AT_Xref_Attach_To_Overlay_File");

            // 8. Bảng biểu, Xuất Excel, KML & In ấn
            dynamic subMenuExportPrint = menu.AddSubMenu(menu.Count + 1, "8. Bảng biểu, Xuất dữ liệu & In ấn");
            AddHeader(subMenuExportPrint, "--- Bảng biểu & Xuất Excel / KML ---");
            AddMenuItem(subMenuExportPrint, "Chuẩn Hóa Định Dạng Bảng (Table)", "AT_Chuan_Hoa_Bang");
            AddMenuItem(subMenuExportPrint, "Xuất Bảng CAD Ra File Excel", "AT_Xuat_Bang_Cad_Ra_Excel");
            AddMenuItem(subMenuExportPrint, "Xuất Bảng Tọa Độ Đỉnh Polyline", "AT_Xuat_Bang_Toa_Do_Polyline");
            AddMenuItem(subMenuExportPrint, "Xuất Đối Tượng Sang Google Earth (KML/KMZ)", "AT_Export_Kml_Custom");
            AddHeader(subMenuExportPrint, "--- In ấn tự động ---");
            AddMenuItem(subMenuExportPrint, "In Hàng Loạt Bản Vẽ Từ Model", "AT_In_Model_Hang_Loat");
            AddMenuItem(subMenuExportPrint, "In Hàng Loạt Bản Vẽ Theo Block Khung Tên", "AT_In_Ban_Ve_Theo_Block");

            // 9. Hệ thống, MCP Bridge & Nạp lại Code
            dynamic subMenuSystem = menu.AddSubMenu(menu.Count + 1, "9. Hệ thống, Palette & Nạp lại Code");
            AddHeader(subMenuSystem, "--- Bảng lệnh Tool Palette ---");
            AddMenuItem(subMenuSystem, "Mở Bảng Lệnh Tool Palette", "AT_Show_Palette");
            AddMenuItem(subMenuSystem, "Nạp Lại Dữ Liệu Tool Palette", "AT_Reload_Palette");
            AddMenuItem(subMenuSystem, "Cấu Hình Nguồn Dữ Liệu Tool Palette", "AT_Palette_Config");
            AddMenuItem(subMenuSystem, "Ẩn Bảng Lệnh Tool Palette", "AT_Hide_Palette");
            AddHeader(subMenuSystem, "--- Kết nối AI & Thanh Menu ---");
            AddMenuItem(subMenuSystem, "Khởi Động Máy Chủ Kết Nối AI (MCP Bridge)", "AT_Khoi_Dong_Mcp_Bridge");
            AddMenuItem(subMenuSystem, "Làm Mới Thanh Menu Lệnh", "AT_Show_Menu");
            AddHeader(subMenuSystem, "--- Biên dịch & Nạp lại DLL (Hot Reload) ---");
            AddMenuItem(subMenuSystem, "Tự Động Build & Nạp Lại Code Mới", "RELOAD");
            AddMenuItem(subMenuSystem, "Bảng Điều Khiển Nạp Lại DLL", "RELOADUI");
        }

        private static void AddMenuItem(dynamic menu, string label, string command)
        {
            string cmd = command.Trim();
            string safeLabel = $"{label.Replace("&", "&&")}  [{cmd}]";
            string macro = $"{cmd} ";
            menu.AddMenuItem(menu.Count + 1, safeLabel, macro);
        }

        private static void AddHeader(dynamic menu, string label)
        {
            if (menu.Count > 0)
            {
                try { menu.AddSeparator(menu.Count + 1); } catch { }
            }
            string safeLabel = label.Replace("&", "&&");
            var item = menu.AddMenuItem(menu.Count + 1, safeLabel, "^C^C");
            item.Enable = false;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Autodesk.AutoCAD.Runtime;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(MyFirstProject.ClassicMenu))]

namespace MyFirstProject
{
    public class ClassicMenu
    {
        [CommandMethod("SHOW_MENU")]
        public static void ShowMenu()
        {
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

                // 1. Remove existing menu if it exists
                try
                {
                    for (int i = 0; i < menuBar.Count; i++)
                    {
                        if (menuBar.Item(i).Name == menuName)
                        {
                            menuBar.Item(i).RemoveFromMenuBar();
                            break;
                        }
                    }
                }
                catch { }

                dynamic targetMenu = null;
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

                if (!exists)
                {
                    targetMenu = popupMenus.Add(menuName);
                }

                // 2. Build Structure
                buildAction(targetMenu);

                // 3. Add to MenuBar
                targetMenu.InsertInMenuBar(menuBar.Count + 1);

                AcadApp.DocumentManager.MdiActiveDocument.Editor.WriteMessage($"\nMenu '{menuName}' created successfully.");
            }
            catch (System.Exception ex)
            {
                AcadApp.DocumentManager.MdiActiveDocument.Editor.WriteMessage($"\nError creating menu '{menuName}': {ex.Message}");
            }
        }



        private static void BuildCivilToolStructure(dynamic menu)
        {
            // Alignment
            dynamic subMenuAlignment = menu.AddSubMenu(menu.Count + 1, "Alignment");
            AddMenuItem(subMenuAlignment, "Offset from Alignment", "AT_OffsetAlignment ");
            AddMenuItem(subMenuAlignment, "Bảng Thống Kê Tuyến Đường", "CTA_BangThongKeCacTuyenDuong ");
            AddMenuItem(subMenuAlignment, "Gắn Nhãn Đầu Cuối Tỷ Lệ", "CTA_GanNhan_DauCuoiTyten ");
            AddHeader(subMenuAlignment, "--- Điều chỉnh bán kính ---");
            AddMenuItem(subMenuAlignment, "Điều Chỉnh Bán Kính Connected", "CTA_DieuChinhBanKinh_ConnectedAlignment ");

            // Corridor
            dynamic subMenuCorridor = menu.AddSubMenu(menu.Count + 1, "Corridor");
            AddHeader(subMenuCorridor, "--- Tạo Corridor ---");
            AddMenuItem(subMenuCorridor, "Tạo Corridor Cho Tuyến Đường", "CTC_TaoCorridor_ChoTuyenDuong ");
            AddMenuItem(subMenuCorridor, "Tạo Corridor Đường Đô Thị Rẽ Phải", "CAC_TaoCooridor_DuongDoThi_RePhai ");
            AddHeader(subMenuCorridor, "--- Thiết lập ---");
            AddMenuItem(subMenuCorridor, "Add All Section", "CTC_AddAllSection ");
            AddHeader(subMenuCorridor, "--- Điều chỉnh ---");
            AddMenuItem(subMenuCorridor, "Điều Chỉnh Phân Đoạn", "CTC_DieuChinh_PhanDoan ");
            AddHeader(subMenuCorridor, "--- Corridor Surface ---");
            AddMenuItem(subMenuCorridor, "Tạo Corridor Surface", "CTSV_TaoCorridorSurface ");

            // Parcel
            dynamic subMenuParcel = menu.AddSubMenu(menu.Count + 1, "Parcel");
            AddMenuItem(subMenuParcel, "Thống Kê Parcel Ra Bảng CAD/Excel", "CTPA_BangThongKeParcel ");
            AddMenuItem(subMenuParcel, "Đổi Tên Parcel (Template)", "CTPA_DoiTen_Parcel ");

            dynamic subMenuPipe = menu.AddSubMenu(menu.Count + 1, "Pipe Network");
            AddHeader(subMenuPipe, "--- Thay đổi thông số ---");
            AddMenuItem(subMenuPipe, "Thay Đổi Đường Kính Cống", "CTPI_ThayDoi_DuongKinhCong ");
            AddMenuItem(subMenuPipe, "Thay Đổi Mặt Phẳng Ref Cống", "CTPI_ThayDoi_MatPhangRef_Cong ");
            AddMenuItem(subMenuPipe, "Thay Đổi Độ Dốc Cống", "CTPI_ThayDoi_DoanDocCong ");
            AddMenuItem(subMenuPipe, "Thay Đổi Cao Độ Đáy Cống", "CTPi_ThayDoi_CaoDo_DayCong ");
            AddHeader(subMenuPipe, "--- Bảng/Xoay ---");
            AddMenuItem(subMenuPipe, "Bảng Cao Độ Hố Thu", "CTPI_BangCaoDo_TuNhienHoThu ");
            AddMenuItem(subMenuPipe, "Xoay Hố Thu Theo 2 Điểm", "CTPI_XoayHoThu_Theo2diem ");
            AddHeader(subMenuPipe, "--- Bề mặt tham chiếu ---");
            AddMenuItem(subMenuPipe, "Điều Chỉnh Bề Mặt Tham Chiếu", "CTPi_DieuChinh_BeMat_ThamChieu ");

            // Point
            dynamic subMenuPoint = menu.AddSubMenu(menu.Count + 1, "Point");
            AddHeader(subMenuPoint, "--- Tạo CogoPoint ---");
            AddMenuItem(subMenuPoint, "Tạo CogoPoint Từ Surface", "CTPO_TaoCogoPoint_CaoDo_FromSurface ");
            AddMenuItem(subMenuPoint, "Tạo CogoPoint Từ Elevation Spot", "CTPO_TaoCogoPoint_CaoDo_Elevationspot ");
            AddMenuItem(subMenuPoint, "Tạo CogoPoint Từ Text", "CTPO_CreateCogopointFromText ");
            AddHeader(subMenuPoint, "--- Quản lý/Ẩn ---");
            AddMenuItem(subMenuPoint, "Update All Point Group", "CTPO_UpdateAllPointGroup ");
            AddMenuItem(subMenuPoint, "Ẩn CogoPoint", "CTPO_An_CogoPoint ");
            AddHeader(subMenuPoint, "--- Tạo từ nguồn khác ---");
            AddMenuItem(subMenuPoint, "Tạo CogoPoint Từ Excel", "CTPO_TaoCogoPoint_FromExcel ");
            AddHeader(subMenuPoint, "--- Đổi tên ---");
            AddMenuItem(subMenuPoint, "Đổi Tên CogoPoint (Template)", "CTPO_DoiTen_Cogopoint ");
            AddMenuItem(subMenuPoint, "Đổi Tên CogoPoint Theo Alignment", "CTPo_DoiTen_CogoPoint_fromAlignment ");

            // Profile
            dynamic subMenuProfile = menu.AddSubMenu(menu.Count + 1, "Profile");
            AddHeader(subMenuProfile, "--- Vẽ trắc dọc ---");
            AddMenuItem(subMenuProfile, "Vẽ Trắc Dọc Tự Nhiên", "CTP_VeTracDoc_TuNhien ");
            AddMenuItem(subMenuProfile, "Vẽ Trắc Dọc Tất Cả Tuyến", "CTP_VeTracDoc_TuNhien_TatCaTuyen ");
            AddHeader(subMenuProfile, "--- Kiểm tra tiêu chuẩn ---");
            AddMenuItem(subMenuProfile, "Kiểm Tra Profile Theo Tiêu Chuẩn", "CTP_KiemTraProfile ");
            AddHeader(subMenuProfile, "--- Sửa/Gắn nhãn ---");
            AddMenuItem(subMenuProfile, "Sửa Đường Tự Nhiên Theo Cọc", "CTP_Fix_DuongTuNhien_TheoCoc ");
            AddMenuItem(subMenuProfile, "Gắn Nhãn Nút Giao Lên Trắc Dọc", "CTP_GanNhanNutGiao_LenTracDoc ");
            AddHeader(subMenuProfile, "--- Chuyển đổi ---");
            AddMenuItem(subMenuProfile, "Polyline To Profile", "CTP_Polyline_To_Profile ");
            AddMenuItem(subMenuProfile, "Profile To Polyline", "CTP_Profile_To_Polyline ");
            AddMenuItem(subMenuProfile, "Điều Chỉnh Profile Theo Polyline", "CTP_Adjust_Profile_By_Polyline ");
            AddHeader(subMenuProfile, "--- Tạo điểm ---");
            AddMenuItem(subMenuProfile, "Tạo CogoPoint Từ PVI", "CTP_TaoCogoPointTuPVI ");
            AddHeader(subMenuProfile, "--- Band Profile ---");
            AddMenuItem(subMenuProfile, "Thay Đổi Profile trong Band", "CTP_ThayDoi_profile_Band ");

            // Surface
            dynamic subMenuSurface = menu.AddSubMenu(menu.Count + 1, "Surface");
            AddMenuItem(subMenuSurface, "Cao Độ Mặt Phẳng Tại CogoPoint", "CTSU_CaoDoMatPhang_TaiCogopoint ");
            AddMenuItem(subMenuSurface, "Tạo Spot Elevation Tại Tim", "CTS_TaoSpotElevation_OnSurface_TaiTim ");

            // Sampleline
            dynamic subMenuSample = menu.AddSubMenu(menu.Count + 1, "Sampleline");
            AddHeader(subMenuSample, "--- Đổi tên cọc ---");
            AddMenuItem(subMenuSample, "Đổi Tên Cọc", "CTS_DoiTenCoc ");
            AddMenuItem(subMenuSample, "Đổi Tên Cọc Km", "CTS_DoiTenCoc3 ");
            AddMenuItem(subMenuSample, "Đổi Tên Cọc Theo Đoạn", "CTS_DoiTenCoc2 ");
            AddMenuItem(subMenuSample, "Đổi Tên Cọc Từ CogoPoint", "CTS_DoiTenCoc_fromCogoPoint ");
            AddMenuItem(subMenuSample, "Đổi Tên Cọc Theo Thứ Tự", "CTS_DoiTenCoc_TheoThuTu ");
            AddMenuItem(subMenuSample, "Đổi Tên Cọc H", "CTS_DoiTenCoc_H ");
            AddHeader(subMenuSample, "--- Bảng tọa độ/Update ---");
            AddMenuItem(subMenuSample, "Tạo Bảng Tọa Độ Cọc", "CTS_TaoBang_ToaDoCoc ");
            AddMenuItem(subMenuSample, "Tạo Bảng Tọa Độ Cọc (Lý Trình)", "CTS_TaoBang_ToaDoCoc2 ");
            AddMenuItem(subMenuSample, "Tạo Bảng Tọa Độ Cọc (Cao Độ)", "CTS_TaoBang_ToaDoCoc3 ");
            AddMenuItem(subMenuSample, "Cập Nhật 2 Table", "AT_UPdate2Table ");
            AddHeader(subMenuSample, "--- Chèn/Phát sinh cọc ---");
            AddMenuItem(subMenuSample, "Chèn Cọc Trên Trắc Dọc", "CTS_ChenCoc_TrenTracDoc ");
            AddMenuItem(subMenuSample, "Chèn Cọc Trên Trắc Ngang", "CTS_CHENCOC_TRENTRACNGANG ");
            AddMenuItem(subMenuSample, "Phát Sinh Cọc", "CTS_PhatSinhCoc ");
            AddMenuItem(subMenuSample, "Phát Sinh Cọc Theo Delta", "CTS_PhatSinhCoc_theoKhoangDelta ");
            AddMenuItem(subMenuSample, "Phát Sinh Cọc Từ CogoPoint", "CTS_PhatSinhCoc_TuCogoPoint ");
            AddMenuItem(subMenuSample, "Phát Sinh Cọc Theo Bảng", "CTS_PhatSinhCoc_TheoBang ");
            AddMenuItem(subMenuSample, "Phát Sinh Cọc Thủ Công", "CTS_PhatSinhCoc_ThuCong ");
            AddHeader(subMenuSample, "--- Dịch/Copy/Đồng bộ ---");
            AddMenuItem(subMenuSample, "Dịch Cọc Tịnh Tiến", "CTS_DichCoc_TinhTien ");
            AddMenuItem(subMenuSample, "Copy Nhóm Cọc", "CTS_Copy_NhomCoc ");
            AddMenuItem(subMenuSample, "Đồng Bộ 2 Nhóm Cọc", "CTS_DongBo_2_NhomCoc ");
            AddMenuItem(subMenuSample, "Đồng Bộ 2 Nhóm Cọc Theo Đoạn", "CTS_DongBo_2_NhomCoc_TheoDoan ");
            AddMenuItem(subMenuSample, "Dịch Cọc 40m", "CTS_DichCoc_TinhTien40 ");
            AddMenuItem(subMenuSample, "Dịch Cọc 20m", "CTS_DichCoc_TinhTien_20 ");
            AddHeader(subMenuSample, "--- Bề rộng/Hiệu chỉnh ---");
            AddMenuItem(subMenuSample, "Copy Bề Rộng Sample Line", "CTS_Copy_BeRong_sampleLine ");
            AddMenuItem(subMenuSample, "Thay Đổi Bề Rộng Sample Line", "CTS_ThayDoi_BeRong_Sampleline ");
            AddMenuItem(subMenuSample, "Offset Bề Rộng Sample Line", "CTS_Offset_BeRong_sampleLine ");
            AddMenuItem(subMenuSample, "Hiệu Chỉnh Khoảng Cách Cọc", "CTS_HieuChinh_KhoangCachCoc ");

            // Section View
            dynamic subMenuSection = menu.AddSubMenu(menu.Count + 1, "Section View");
            AddHeader(subMenuSection, "--- Vẽ trắc ngang ---");
            AddMenuItem(subMenuSection, "Vẽ Trắc Ngang Thiết Kế", "CTSV_VeTracNgangThietKe ");
            AddMenuItem(subMenuSection, "Vẽ Tất Cả Trắc Ngang", "CVSV_VeTatCa_TracNgangThietKe ");
            AddHeader(subMenuSection, "--- Đánh cấp ---");
            AddMenuItem(subMenuSection, "Tính Đánh Cấp", "CTSV_DanhCap ");
            AddMenuItem(subMenuSection, "Xóa Bỏ Đánh Cấp", "CTSV_DanhCap_XoaBo ");
            AddMenuItem(subMenuSection, "Vẽ Thêm Đánh Cấp", "CTSV_DanhCap_VeThem ");
            AddMenuItem(subMenuSection, "Vẽ Thêm Đánh Cấp 2m", "CTSV_DanhCap_VeThem2 ");
            AddMenuItem(subMenuSection, "Vẽ Thêm Đánh Cấp 1m", "CTSV_DanhCap_VeThem1 ");
            AddMenuItem(subMenuSection, "Cập Nhật Đánh Cấp", "CTSV_DanhCap_CapNhat ");
            AddHeader(subMenuSection, "--- Thiết lập/giới hạn ---");
            AddMenuItem(subMenuSection, "Thay Đổi MSS Min Max", "CTSV_ThayDoi_MSS_Min_Max ");
            AddMenuItem(subMenuSection, "Thay Đổi Giới Hạn Trái Phải", "CTSV_ThayDoi_GioiHan_traiPhai ");
            AddHeader(subMenuSection, "--- Khung in ---");
            AddMenuItem(subMenuSection, "Thay Đổi Khung In", "CTSV_ThayDoi_KhungIn ");
            AddMenuItem(subMenuSection, "Fit Khung In", "CTSV_fit_KhungIn ");
            AddMenuItem(subMenuSection, "Fit Khung In 5x5", "CTSV_fit_KhungIn_5_5_top ");
            AddMenuItem(subMenuSection, "Fit Khung In 5x10", "CTSV_fit_KhungIn_5_10_top ");
            AddHeader(subMenuSection, "--- Khóa/ẩn ---");
            AddMenuItem(subMenuSection, "Khóa Cắt Ngang Add Point", "CTSV_KhoaCatNgang_AddPoint ");
            AddMenuItem(subMenuSection, "Ẩn Đường Địa Chất", "CTSV_An_DuongDiaChat ");
            AddHeader(subMenuSection, "--- Hiệu chỉnh ---");
            AddMenuItem(subMenuSection, "Hiệu Chỉnh Section Static", "CTSV_HieuChinh_Section ");
            AddMenuItem(subMenuSection, "Hiệu Chỉnh Section Dynamic", "CTSV_HieuChinh_Section_Dynamic ");
            AddMenuItem(subMenuSection, "Điều Chỉnh Đường Tự Nhiên", "CTSV_DieuChinh_DuongTuNhien ");
            AddMenuItem(subMenuSection, "Chọn Section Static", "CTSV_ChonSection_Static ");
            AddHeader(subMenuSection, "--- Khác ---");
            AddMenuItem(subMenuSection, "Chuyển Đổi TN-TK sang TN-TN", "CTSV_ChuyenDoi_TNTK_TNTN ");
            AddMenuItem(subMenuSection, "Thêm Vật Liệu Trên Cắt Ngang", "CTSV_ThemVatLieu_TrenCatNgang ");
            AddMenuItem(subMenuSection, "Thêm Bảng KL Cắt Ngang", "CTSV_Them_BangKL_CatNgang ");
            AddMenuItem(subMenuSection, "Xuất Thông Tin Material Section", "CTSV_MaterialSection ");
            AddMenuItem(subMenuSection, "Tạo Polyline Từ Section", "AT_PolylineFromSection ");
            AddHeader(subMenuSection, "--- Xuất/Khối lượng ---");
            AddMenuItem(subMenuSection, "Xuất Khối Lượng ra Excel", "CTSV_XuatKhoiLuongRaExcel ");
            AddMenuItem(subMenuSection, "Khối Lượng Cắt Ngang", "CTSV_KhoiLuongCatNgang ");
            AddMenuItem(subMenuSection, "Xuất Bảng Sang Excel", "AT_XuatBangCadRaExcel ");
            AddHeader(subMenuSection, "--- Material ---");
            AddMenuItem(subMenuSection, "Thêm Material List", "CTS_Them_MaterialList ");
            AddMenuItem(subMenuSection, "Xem Material List", "CTS_Xem_MaterialList ");
            AddMenuItem(subMenuSection, "Xóa Material List", "CTS_Xoa_MaterialList ");

            // Property Sets
            dynamic subMenuProp = menu.AddSubMenu(menu.Count + 1, "Property Sets");
            AddHeader(subMenuProp, "--- 3D Solid ---");
            AddMenuItem(subMenuProp, "Set PropertySet 3D Solid", "AT_Solid_Set_PropertySet ");
            AddMenuItem(subMenuProp, "Show 3D Solid Info", "AT_Solid_Show_Info ");

            // Thông tin
            dynamic subMenuInfo = menu.AddSubMenu(menu.Count + 1, "Thông tin");
            AddMenuItem(subMenuInfo, "Thông Tin Đối Tượng", "CT_ThongTinDoiTuong ");
        }

        private static void BuildAcadToolStructure(dynamic menu)
        {
            // Tổng độ dài
            dynamic subMenuLen = menu.AddSubMenu(menu.Count + 1, "Tổng độ dài");
            AddMenuItem(subMenuLen, "Tổng Độ Dài (Full)", "AT_TongDoDai_Full ");
            AddMenuItem(subMenuLen, "Tổng Độ Dài (Replace)", "AT_TongDoDai_Replace ");
            AddMenuItem(subMenuLen, "Tổng Độ Dài (Replace2)", "AT_TongDoDai_Replace2 ");
            AddMenuItem(subMenuLen, "Tổng Độ Dài (Cộng Thêm)", "AT_TongDoDai_Replace_CongThem ");

            // Tổng diện tích
            dynamic subMenuArea = menu.AddSubMenu(menu.Count + 1, "Tổng diện tích");
            AddMenuItem(subMenuArea, "Tổng Diện Tích (Full)", "AT_TongDienTich_Full ");
            AddMenuItem(subMenuArea, "Tổng Diện Tích (Replace)", "AT_TongDienTich_Replace ");
            AddMenuItem(subMenuArea, "Tổng Diện Tích (Replace2)", "AT_TongDienTich_Replace2 ");
            AddMenuItem(subMenuArea, "Tổng Diện Tích (Cộng Thêm)", "AT_TongDienTich_Replace_CongThem ");

            // Đo độ dốc
            dynamic subMenuSlope = menu.AddSubMenu(menu.Count + 1, "Đo độ dốc");
            AddMenuItem(subMenuSlope, "Tính Độ Dốc (2 Điểm)", "AT_DoDoc ");
            AddMenuItem(subMenuSlope, "Tính Độ Dốc (Simple)", "AT_DoDoc_Simple ");
            AddMenuItem(subMenuSlope, "Tính Độ Dốc (Object)", "AT_DoDoc_Object ");

            // Biên tập Text
            dynamic subMenuText = menu.AddSubMenu(menu.Count + 1, "Biên tập Text");
            AddMenuItem(subMenuText, "Text Link", "AT_TextLink ");
            AddMenuItem(subMenuText, "Text Layout", "AT_TextLayout ");
            AddMenuItem(subMenuText, "Tạo Mới Text Layout", "AT_TaoMoi_TextLayout ");
            AddMenuItem(subMenuText, "Label From Text", "AT_Label_FromText ");
            AddMenuItem(subMenuText, "Đánh Số Thứ Tự", "AT_DanhSoThuTu ");
            AddMenuItem(subMenuText, "Copy Nội Dung Text", "CN ");
            AddMenuItem(subMenuText, "Copy và Dịch Tiếng Anh", "CA ");

            // In ấn
            dynamic subMenuPrint = menu.AddSubMenu(menu.Count + 1, "In ấn");
            AddMenuItem(subMenuPrint, "In Model Hàng Loạt", "AT_InModel_HangLoat ");
            AddMenuItem(subMenuPrint, "In Bản Vẽ Theo Block", "AT_InBanVe_TheoBlock ");

            // 3D Solid
            dynamic subMenu3D = menu.AddSubMenu(menu.Count + 1, "3D Solid");
            AddMenuItem(subMenu3D, "Tạo Solid từ Polyline", "AT_Solid_frompolyline ");
            AddMenuItem(subMenu3D, "Tạo Surface từ Polyline", "AT_Surface_frompolyline ");
            AddMenuItem(subMenu3D, "Text To Solid", "AT_TextToSolid ");
            AddMenuItem(subMenu3D, "Text To Solid (Step 2)", "AT_TextToSolid_Step2 ");
            AddMenuItem(subMenu3D, "Polys To Solid", "AT_PolysToSolid ");

            // Block
            dynamic subMenuBlock = menu.AddSubMenu(menu.Count + 1, "Block");
            AddMenuItem(subMenuBlock, "Tạo Block Từng Đối Tượng", "AT_TAOBLOCK_TUNGDOITUONG ");
            AddMenuItem(subMenuBlock, "Đánh Số Thứ Tự Cho Block", "AT_DanhSoThuTu_ChoBlock ");

            // Xoay đối tượng
            dynamic subMenuRotate = menu.AddSubMenu(menu.Count + 1, "Xoay đối tượng");
            AddMenuItem(subMenuRotate, "Xoay Theo Viewport", "AT_XoayDoiTuong_TheoViewport ");
            AddMenuItem(subMenuRotate, "Xoay Theo Viewport (V2)", "AT_XoayDoiTuong_TheoViewport_V2 ");
            AddMenuItem(subMenuRotate, "Xoay Theo 2 Điểm", "AT_XoayDoiTuong_Theo2Diem ");
            AddMenuItem(subMenuRotate, "Điều Chỉnh Map Rotation", "AdjustMapRotation ");

            // Viewport
            dynamic subMenuViewport = menu.AddSubMenu(menu.Count + 1, "Viewport");
            AddMenuItem(subMenuViewport, "Bố Trí ViewPort Theo Hình", "AT_BoTri_ViewPort_TheoHinh ");
            AddMenuItem(subMenuViewport, "Bố Trí ViewPort Theo 2 Điểm", "AT_BoTri_ViewPort_Theo2Diem ");
            AddMenuItem(subMenuViewport, "Xoay ViewPort Theo 2 Điểm", "AT_Xoay_ViewPort_Theo2Diem ");
            AddMenuItem(subMenuViewport, "Xoay VP Hiện Hành (2 Điểm)", "AT_Xoay_ViewPortHienHanh_Theo2Diem ");
            AddMenuItem(subMenuViewport, "Xoay VP Hiện Hành (Góc)", "AT_Xoay_ViewPortHienHanh_TheoGoc ");
            AddMenuItem(subMenuViewport, "Reset Góc Xoay VP", "AT_Xoay_ViewPortHienHanh_Reset ");

            // Layout
            dynamic subMenuLayout = menu.AddSubMenu(menu.Count + 1, "Layout");
            AddMenuItem(subMenuLayout, "Dim Layout 2", "AT_DimLayout2 ");
            AddMenuItem(subMenuLayout, "Dim Layout", "AT_DimLayout ");
            AddMenuItem(subMenuLayout, "Block Layout", "AT_BlockLayout ");
            AddMenuItem(subMenuLayout, "Update Layout", "AT_UpdateLayout ");

            // Xref
            dynamic subMenuXref = menu.AddSubMenu(menu.Count + 1, "Xref");
            AddMenuItem(subMenuXref, "Xref All", "AT_XrefAll ");
            AddMenuItem(subMenuXref, "Xref All Overlay", "AT_XrefAllOverlay ");
            AddMenuItem(subMenuXref, "Xref To Block", "AT_XrefToBlock ");
            AddMenuItem(subMenuXref, "Attach To Overlay", "AT_XrefAttachToOverlay ");
            AddMenuItem(subMenuXref, "Attach To Overlay (File)", "AT_XrefAttachToOverlayFile ");

            // Xuất bảng
            dynamic subMenuExport = menu.AddSubMenu(menu.Count + 1, "Xuất bảng");
            AddMenuItem(subMenuExport, "Xuất Bảng CAD ra Excel", "AT_XuatBangCadRaExcel ");
            AddMenuItem(subMenuExport, "Xuất Bảng Tọa Độ Polyline", "XUATBANG_ToaDoPolyline ");

            // Khác
            dynamic subMenuOther = menu.AddSubMenu(menu.Count + 1, "Khác");
            AddMenuItem(subMenuOther, "Tạo Outline", "AT_TaoOutline ");
            AddMenuItem(subMenuOther, "Xóa Đối Tượng Cùng Layer", "AT_XoaDoiTuong_CungLayer ");
            AddMenuItem(subMenuOther, "Xóa 3DSolid/Body", "AT_XoaDoiTuong_3DSolid_Body ");
            AddMenuItem(subMenuOther, "Offset 2 Bên", "AT_Offset_2Ben ");
            AddMenuItem(subMenuOther, "Annotative Scale Current Only", "AT_annotive_scale_currentOnly ");
            AddMenuItem(subMenuOther, "Explode Text", "AT_TXTEXP ");
            AddMenuItem(subMenuOther, "Dim Đường Cong", "AT_DIM_DUONGCONG ");
        }

        private static void AddMenuItem(dynamic menu, string label, string macro)
        {
            // AddMenuItem(Index, Label, Macro)
            // Chỉ sử dụng lệnh trực tiếp, không cần ^C^C
            menu.AddMenuItem(menu.Count + 1, label, macro);
        }

        private static void AddHeader(dynamic menu, string label)
        {
            // Add a disabled item to act as a header
            // Use a valid macro even if disabled
            var item = menu.AddMenuItem(menu.Count + 1, label, "^C^C");
            item.Enable = false;
        }
    }
}

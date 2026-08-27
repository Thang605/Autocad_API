# THONG KE LENH COMMAND METHOD TRONG DU AN

## Tong Quan
- **Tong so file .cs co lenh:** 65 file
- **Tong so lenh CommandMethod:** 162 lenh
- **Ngay cap nhat:** 2026-02-21

---

## Menu form (1 lenh)

### ClassicMenu.cs
| STT | Lenh | Mo ta |
|-----|------|-------|
| 1 | `SHOW_MENU` | Hien thi menu classic |

---

## Help System (5 lenh)

### HelpSystem.cs
| STT | Lenh | Mo ta |
|-----|------|-------|
| 1 | `AT_HelpList` | Danh sach tat ca lenh |
| 2 | `AT_Help` | Tra cuu help cho lenh |
| 3 | `AT_HelpSearch` | Tim kiem lenh |
| 4 | `AT_HelpForm` | Hien thi form help |
| 5 | `SHORTCUT_MANAGER` | Quan ly phim tat |

---

## Acad Tool (49 lenh)

### 01.CAD.cs (22 lenh)
| STT | Lenh | Mo ta |
|-----|------|-------|
| 1 | `AT_annotive_scale_currentOnly` | Chi giu annotative scale hien tai |
| 2 | `AT_BlockLayout` | Block layout |
| 3 | `AT_DanhSoThuTu` | Danh so thu tu |
| 4 | `AT_DimLayout` | Dim layout |
| 5 | `AT_DimLayout2` | Dim layout phien ban 2 |
| 6 | `AT_Label_FromText` | Tao label tu text |
| 7 | `AT_Offset_2Ben` | Offset 2 ben |
| 8 | `AT_TaoMoi_TextLayout` | Tao moi text layout |
| 9 | `AT_TextLayout` | Text layout |
| 10 | `AT_TextLink` | Lien ket text |
| 11 | `AT_TongDienTich_Full` | Tong dien tich (full) |
| 12 | `AT_TongDienTich_Replace` | Tong dien tich (replace) |
| 13 | `AT_TongDienTich_Replace_CongThem` | Tong dien tich (replace cong them) |
| 14 | `AT_TongDienTich_Replace2` | Tong dien tich (replace 2) |
| 15 | `AT_TongDoDai_Full` | Tong do dai (full) |
| 16 | `AT_TongDoDai_Replace` | Tong do dai (replace) |
| 17 | `AT_TongDoDai_Replace_CongThem` | Tong do dai (replace cong them) |
| 18 | `AT_TongDoDai_Replace2` | Tong do dai (replace 2) |
| 19 | `AT_UpdateLayout` | Cap nhat layout |
| 20 | `AT_XoaDoiTuong_3DSolid_Body` | Xoa doi tuong 3D Solid/Body |
| 21 | `AT_XoaDoiTuong_CungLayer` | Xoa doi tuong cung layer |
| 22 | `AT_XoayDoiTuong_Theo2Diem` | Xoay doi tuong theo 2 diem |

### 02.AT_Solid_frompolyline.cs
| STT | Lenh | Mo ta |
|-----|------|-------|
| 23 | `AT_Surface_frompolyline` | Tao surface tu polyline |

### 03.Command_XUATBANG_ToaDoPolyline.cs
| STT | Lenh | Mo ta |
|-----|------|-------|
| 24 | `XUATBANG_ToaDoPolyline` | Xuat bang toa do polyline |

### 04.AT_XuatBang_Civil3D_ToExcel.cs
| STT | Lenh | Mo ta |
|-----|------|-------|
| 25 | `AT_XuatBang_Civil3D_ToExcel` | Xuat bang Civil 3D sang Excel |

### 05.AT_TaoOutline.cs
| STT | Lenh | Mo ta |
|-----|------|-------|
| 26 | `AT_TaoOutline` | Tao outline |

### 06.CT_Copy_NoiDung_Text.cs
| STT | Lenh | Mo ta |
|-----|------|-------|
| 27 | `CT_Copy_NoiDung_Text` | Copy noi dung text |

### 07.CA_CopyVaDichTiengAnh.cs
| STT | Lenh | Mo ta |
|-----|------|-------|
| 28 | `CA` | Copy va dich tieng Anh |

### 08.AT_DocNgang.cs (3 lenh)
| STT | Lenh | Mo ta |
|-----|------|-------|
| 29 | `AT_DoDoc` | Do do doc |
| 30 | `AT_DoDoc_Object` | Do do doc theo object |
| 31 | `AT_DoDoc_Simple` | Do do doc don gian |

### 09.AT_Xref_all_file.cs (4 lenh)
| STT | Lenh | Mo ta |
|-----|------|-------|
| 32 | `AT_XrefAll` | Xref tat ca file |
| 33 | `AT_XrefAllOverlay` | Xref tat ca file (overlay) |
| 34 | `AT_XrefAttachToOverlay` | Chuyen xref attach sang overlay |
| 35 | `AT_XrefAttachToOverlayFile` | Chuyen xref attach sang overlay (file) |

### 10.AT_XuatXref.cs
| STT | Lenh | Mo ta |
|-----|------|-------|
| 36 | `AT_XrefToBlock` | Chuyen xref sang block |

### 11.AT_XoayDoiTuong_TheoViewport.cs (2 lenh)
| STT | Lenh | Mo ta |
|-----|------|-------|
| 37 | `AT_XoayDoiTuong_TheoViewport` | Xoay doi tuong theo viewport |
| 38 | `AT_XoayDoiTuong_TheoViewport_V2` | Xoay doi tuong theo viewport V2 |

### 12.AT_BoTri_ViewPort_TheoHinh.cs
| STT | Lenh | Mo ta |
|-----|------|-------|
| 39 | `AT_BoTri_ViewPort_TheoHinh` | Bo tri viewport theo hinh |

### 13.AT_Xoay_ViewPort_Theo2Diem.cs
| STT | Lenh | Mo ta |
|-----|------|-------|
| 40 | `AT_Xoay_ViewPort_Theo2Diem` | Xoay viewport theo 2 diem |

### 14.AT_TaoBlock_TungDoiTuong.cs
| STT | Lenh | Mo ta |
|-----|------|-------|
| 41 | `AT_TAOBLOCK_TUNGDOITUONG` | Tao block tung doi tuong |

### 15.AT_InModel_HangLoat.cs
| STT | Lenh | Mo ta |
|-----|------|-------|
| 42 | `AT_InModel_HangLoat` | In model hang loat |

### 16.AT_TextToSolid.cs (3 lenh)
| STT | Lenh | Mo ta |
|-----|------|-------|
| 43 | `AT_TextToSolid` | Chuyen text thanh solid |
| 44 | `AT_TextToSolid_Step2` | Chuyen text thanh solid buoc 2 |
| 45 | `AT_PolysToSolid` | Chuyen polyline thanh solid |

### 17.AT_InBanVe_TheoBlock.cs
| STT | Lenh | Mo ta |
|-----|------|-------|
| 46 | `AT_InBanVe_TheoBlock` | In ban ve theo block |

### 18.AT_Danh_SoThuTu_ChoBlock.cs
| STT | Lenh | Mo ta |
|-----|------|-------|
| 47 | `AT_DanhSoThuTu_ChoBlock` | Danh so thu tu cho block |

### 19.AT_DIM_DUONGCONG.CS
| STT | Lenh | Mo ta |
|-----|------|-------|
| 48 | `AT_DIM_DUONGCONG` | Dim duong cong |

### 20.AT_TXTEXP.cs
| STT | Lenh | Mo ta |
|-----|------|-------|
| 49 | `AT_TXTEXP` | Explode text (no text) |

### 29.AT_ChuanHoaBanVe.cs (3 lenh)
| STT | Lenh | Mo ta |
|-----|------|-------|
| 50 | `AT_ChuanHoaBanVe` | Chuẩn hóa định dạng bản vẽ theo tiêu chuẩn T27 (Layer, TextStyle, DimStyle, MLeader, Purge) |
| 51 | `CHBV` | Lệnh tắt chuẩn hóa bản vẽ T27 |
| 52 | `AT_StandardizeDrawing` | Standardize drawing to T27 standard |

---

## Civil Tool (94 lenh)

### 01.Corridor.cs
| STT | Lenh | Mo ta |
|-----|------|-------|
| 1 | `CTC_AddAllSection` | Them tat ca section vao corridor |

### 02.Parcel.cs
| STT | Lenh | Mo ta |
|-----|------|-------|
| 2 | `CTPA_TaoParcel_CacLoaiNha` | Tao parcel cac loai nha |

### 04.PipeAndStructures.cs (4 lenh)
| STT | Lenh | Mo ta |
|-----|------|-------|
| 3 | `CTPI_ThayDoi_DuongKinhCong` | Thay doi duong kinh cong |
| 4 | `CTPI_ThayDoi_DoanDocCong` | Thay doi do doc cong |
| 5 | `CTPI_BangCaoDo_TuNhienHoThu` | Bang cao do tu nhien ho thu |
| 6 | `CTPI_XoayHoThu_Theo2diem` | Xoay ho thu theo 2 diem |

### 05.Point.cs (5 lenh)
| STT | Lenh | Mo ta |
|-----|------|-------|
| 7 | `CTPO_TaoCogoPoint_CaoDo_FromSurface` | Tao CogoPoint cao do tu Surface |
| 8 | `CTPO_TaoCogoPoint_CaoDo_Elevationspot` | Tao CogoPoint tu Elevation spot |
| 9 | `CTPO_UpdateAllPointGroup` | Update tat ca Point Group |
| 10 | `CTPO_CreateCogopointFromText` | Tao CogoPoint tu Text |
| 11 | `CTPO_An_CogoPoint` | An CogoPoint |

### 06.ProfileAndProfileView.cs (5 lenh)
| STT | Lenh | Mo ta |
|-----|------|-------|
| 12 | `CTP_VeTracDoc_TuNhien` | Ve trac doc tu nhien |
| 13 | `CTP_VeTracDoc_TuNhien_TatCaTuyen` | Ve trac doc tu nhien tat ca tuyen |
| 14 | `CTP_Fix_DuongTuNhien_TheoCoc` | Fix duong tu nhien theo coc |
| 15 | `CTP_GanNhanNutGiao_LenTracDoc` | Gan nhan nut giao len trac doc |
| 16 | `CTP_TaoCogoPointTuPVI` | Tao CogoPoint tu PVI |

### 07.Sampleline.cs (24 lenh)
| STT | Lenh | Mo ta |
|-----|------|-------|
| 17 | `CTS_PhatSinhCoc` | Phat sinh coc |
| 18 | `CTS_PhatSinhCoc_TheoBang` | Phat sinh coc theo bang |
| 19 | `CTS_PhatSinhCoc_theoKhoangDelta` | Phat sinh coc theo khoang delta |
| 20 | `CTS_PhatSinhCoc_TuCogoPoint` | Phat sinh coc tu CogoPoint |
| 21 | `CTS_DoiTenCoc` | Doi ten coc |
| 22 | `CTS_DoiTenCoc2` | Doi ten coc 2 |
| 23 | `CTS_DoiTenCoc3` | Doi ten coc 3 |
| 24 | `CTS_DoiTenCoc_H` | Doi ten coc H |
| 25 | `CTS_DoiTenCoc_TheoThuTu` | Doi ten coc theo thu tu |
| 26 | `CTS_DoiTenCoc_fromCogoPoint` | Doi ten coc tu CogoPoint |
| 27 | `CTS_DichCoc_TinhTien` | Dich coc tinh tien |
| 28 | `CTS_DichCoc_TinhTien_20` | Dich coc tinh tien 20m |
| 29 | `CTS_DichCoc_TinhTien40` | Dich coc tinh tien 40m |
| 30 | `CTS_Copy_NhomCoc` | Copy nhom coc |
| 31 | `CTS_Copy_BeRong_sampleLine` | Copy be rong sample line |
| 32 | `CTS_Offset_BeRong_sampleLine` | Offset be rong sample line |
| 33 | `CTS_DongBo_2_NhomCoc` | Dong bo 2 nhom coc |
| 34 | `CTS_DongBo_2_NhomCoc_TheoDoan` | Dong bo 2 nhom coc theo doan |
| 35 | `CTS_ChenCoc_TrenTracDoc` | Chen coc tren trac doc |
| 36 | `CTS_CHENCOC_TRENTRACNGANG` | Chen coc tren trac ngang |
| 37 | `CTS_TaoBang_ToaDoCoc` | Tao bang toa do coc |
| 38 | `CTS_TaoBang_ToaDoCoc2` | Tao bang toa do coc 2 |
| 39 | `CTS_TaoBang_ToaDoCoc3` | Tao bang toa do coc 3 |
| 40 | `AT_UPdate2Table` | Update 2 bang |

### 08.Sectionview.cs (18 lenh)
| STT | Lenh | Mo ta |
|-----|------|-------|
| 41 | `CTSV_DanhCap` | Danh cap |
| 42 | `CTSV_DanhCap_CapNhat` | Danh cap - cap nhat |
| 43 | `CTSV_DanhCap_VeThem` | Danh cap - ve them |
| 44 | `CTSV_DanhCap_VeThem1` | Danh cap - ve them 1 |
| 45 | `CTSV_DanhCap_VeThem2` | Danh cap - ve them 2 |
| 46 | `CTSV_DanhCap_XoaBo` | Danh cap - xoa bo |
| 47 | `CTSV_HieuChinh_Section` | Hieu chinh section |
| 48 | `CTSV_HieuChinh_Section_Dynamic` | Hieu chinh section (dynamic) |
| 49 | `CTSV_ThayDoi_GioiHan_traiPhai` | Thay doi gioi han trai/phai |
| 50 | `CTSV_ThayDoi_KhungIn` | Thay doi khung in |
| 51 | `CTSV_ThayDoi_MSS_Min_Max` | Thay doi MSS min/max |
| 52 | `CTSV_fit_KhungIn` | Fit khung in |
| 53 | `CTSV_fit_KhungIn_5_10_top` | Fit khung in 5-10 top |
| 54 | `CTSV_fit_KhungIn_5_5_top` | Fit khung in 5-5 top |
| 55 | `CTSV_An_DuongDiaChat` | An duong dia chat |
| 56 | `CTSV_ChuyenDoi_TNTK_TNTN` | Chuyen doi TNTK/TNTN |
| 57 | `CTSV_KhoaCatNgang_AddPoint` | Khoa cat ngang - them diem |
| 58 | `CTSV_ThemVatLieu_TrenCatNgang` | Them vat lieu tren cat ngang |

### 09.Surfaces.cs
| STT | Lenh | Mo ta |
|-----|------|-------|
| 59 | `CTS_TaoSpotElevation_OnSurface_TaiTim` | Tao Spot Elevation tren Surface tai tim |

### 10.Property Sets.cs (2 lenh)
| STT | Lenh | Mo ta |
|-----|------|-------|
| 60 | `AT_Solid_Set_PropertySet` | Set property set cho solid |
| 61 | `AT_Solid_Show_Info` | Hien thi thong tin solid |

### 11.OffsetAlignment.cs
| STT | Lenh | Mo ta |
|-----|------|-------|
| 62 | `AT_OffsetAlignment` | Offset alignment |

### 12.CTC_DieuChinh_PhanDoan.cs
| STT | Lenh | Mo ta |
|-----|------|-------|
| 63 | `CTC_DieuChinh_PhanDoan` | Dieu chinh phan doan corridor |

### 13.CTSV_DieuChinh_DuongTuNhien.cs
| STT | Lenh | Mo ta |
|-----|------|-------|
| 64 | `CTSV_DieuChinh_DuongTuNhien` | Dieu chinh duong tu nhien |

### 14.CTA_BangThongKeCacTuyenDuong.cs
| STT | Lenh | Mo ta |
|-----|------|-------|
| 65 | `CTA_BangThongKeCacTuyenDuong` | Bang thong ke cac tuyen duong |

### 15.CTPi_DieuChinh_BeMat_ThamChieu.cs
| STT | Lenh | Mo ta |
|-----|------|-------|
| 66 | `CTPi_DieuChinh_BeMat_ThamChieu` | Dieu chinh be mat tham chieu |

### 16.CTSV_KhoiLuongCatNgang.cs
| STT | Lenh | Mo ta |
|-----|------|-------|
| 67 | `CTSV_KhoiLuongCatNgang` | Khoi luong cat ngang |

### 17.CTPo_DoiTen_CogoPoint_fromAlignment.cs
| STT | Lenh | Mo ta |
|-----|------|-------|
| 68 | `CTPo_DoiTen_CogoPoint_fromAlignment` | Doi ten CogoPoint tu alignment |

### 17.CTSV_XuatKhoiLuongRaExcel.cs
| STT | Lenh | Mo ta |
|-----|------|-------|
| 69 | `CTSV_XuatKhoiLuongRaExcel` | Xuat khoi luong ra Excel |

### 19.AT_PolylineFromSection.cs
| STT | Lenh | Mo ta |
|-----|------|-------|
| 70 | `AT_PolylineFromSection` | Tao polyline tu section |

### 20.CT_ThongTinDoiTuong.cs
| STT | Lenh | Mo ta |
|-----|------|-------|
| 71 | `CT_ThongTinDoiTuong` | Thong tin doi tuong Civil 3D |

### 21.CTSU_CaoDoMatPhang_TaiCogopoint.cs
| STT | Lenh | Mo ta |
|-----|------|-------|
| 72 | `CTSU_CaoDoMatPhang_TaiCogopoint` | Cao do mat phang tai CogoPoint |

### 22.CTP_ThayDoi_profile_Band.cs
| STT | Lenh | Mo ta |
|-----|------|-------|
| 73 | `CTP_ThayDoi_profile_Band` | Thay doi profile band |

### 23.CTSV_VeTracNgangThietKe.cs
| STT | Lenh | Mo ta |
|-----|------|-------|
| 74 | `CTSV_VeTracNgangThietKe` | Ve trac ngang thiet ke |

### 24.CTS_ThayDoi_BeRong_Sampleline.cs
| STT | Lenh | Mo ta |
|-----|------|-------|
| 75 | `CTS_ThayDoi_BeRong_Sampleline` | Thay doi be rong sample line |

### 24.CTSV_TaoCorridorSurface.cs (3 lenh)
| STT | Lenh | Mo ta |
|-----|------|-------|
| 76 | `CTSV_TaoCorridorSurface` | Tao corridor surface |

### 25.CTPI_ThayDoi_CaoDo_DayCong.cs
| STT | Lenh | Mo ta |
|-----|------|-------|
| 79 | `CTPi_ThayDoi_CaoDo_DayCong` | Thay doi cao do day cong |

### 26.CTC_TaoCorridor_ChoTuyenDuong.cs
| STT | Lenh | Mo ta |
|-----|------|-------|
| 80 | `CTC_TaoCorridor_ChoTuyenDuong` | Tao corridor cho tuyen duong |

### 26.CTPI_Corridor_SetTargets.cs
| STT | Lenh | Mo ta |
|-----|------|-------|
| 81 | `CTPI_Corridor_SetTargets` | Corridor set targets |

### 28.CTC_TaoCooridor_DuongDoThi_RePhai.cs
| STT | Lenh | Mo ta |
|-----|------|-------|
| 87 | `CAC_TaoCooridor_DuongDoThi_RePhai` | Tao corridor duong do thi re phai |

### 29.CT_TaoCogoPoint_FromExcel.cs
| STT | Lenh | Mo ta |
|-----|------|-------|
| 88 | `CTPO_TaoCogoPoint_FromExcel` | Tao CogoPoint tu Excel |

### 30.CTPO_DoiTen_Cogopoint.cs
| STT | Lenh | Mo ta |
|-----|------|-------|
| 89 | `CTPO_DoiTen_Cogopoint` | Doi ten CogoPoint |

### 31.CTS_HieuChinh_KhoangCachCoc.cs
| STT | Lenh | Mo ta |
|-----|------|-------|
| 90 | `CTS_HieuChinh_KhoangCachCoc` | Hieu chinh khoang cach coc |

### 32.CTA_DieuChinh_BanKinh_ConnectedAlignemnt.cs (2 lenh)
| STT | Lenh | Mo ta |
|-----|------|-------|
| 91 | `CTA_DieuChinhBanKinh_ConnectedAlignment` | Dieu chinh ban kinh connected alignment |
| 92 | `CTA_DieuChinhBanKinh_Help` | Dieu chinh ban kinh - help |

### TestSubassemblyTargetConfigForm.cs (2 lenh)
| STT | Lenh | Mo ta |
|-----|------|-------|
| 93 | `TestTargetConfigForm` | Test target config form |
| 94 | `TestTargetConfigFormDebug` | Test target config form (debug) |

---

## Civil Tool 2 (13 lenh)

### 1.CTS_Them_MaterialList.cs (3 lenh)
| STT | Lenh | Mo ta |
|-----|------|-------|
| 1 | `CTS_Them_MaterialList` | Them material list |
| 2 | `CTS_Xem_MaterialList` | Xem material list |
| 3 | `CTS_Xoa_MaterialList` | Xoa material list |

### 2.CTSV_Them_BangKL_CatNgang.cs
| STT | Lenh | Mo ta |
|-----|------|-------|
| 4 | `CTSV_Them_BangKL_CatNgang` | Them bang khoi luong cat ngang |

### 3.CTSV_Chon_section_static.cs
| STT | Lenh | Mo ta |
|-----|------|-------|
| 5 | `CTSV_ChonSection_Static` | Chon section static |

### 4.AT_Xuatbang_civil_sang_excel.cs
| STT | Lenh | Mo ta |
|-----|------|-------|
| 6 | `AT_XuatBang_SangExcel` | Xuat bang sang Excel |

### 5.AT_Botri_Viewport_theo2diem.cs
| STT | Lenh | Mo ta |
|-----|------|-------|
| 7 | `AT_BoTri_ViewPort_Theo2Diem` | Bo tri viewport theo 2 diem |

### 6.CTS_Phatsinhcoc_thucong.cs
| STT | Lenh | Mo ta |
|-----|------|-------|
| 8 | `CTS_PhatSinhCoc_ThuCong` | Phat sinh coc thu cong |

### 7.CTP_Polyline_To_Profile.cs (2 lenh)
| STT | Lenh | Mo ta |
|-----|------|-------|
| 9 | `CTP_Polyline_To_Profile` | Chuyen polyline thanh profile |
| 10 | `CTP_Adjust_Profile_By_Polyline` | Dieu chinh profile theo polyline |

### CTA_GanNhan_DauCuoiTyten.cs
| STT | Lenh | Mo ta |
|-----|------|-------|
| 11 | `CTA_GanNhan_DauCuoiTyten` | Gan nhan dau cuoi ty le |

### CTPA_DoiTen_Parcel.cs (2 lenh)
| STT | Lenh | Mo ta |
|-----|------|-------|
| 12 | `CTPA_DoiTen_Parcel` | Doi ten parcel |
| 13 | `CTPA_DoiTen_Parcel_Nhanh` | Doi ten parcel nhanh |

---

## Thong ke theo nhom chuc nang

| Nhom | Tien to | So lenh |
|------|---------|---------|
| Acad Tool | `AT_` | 49 |
| Sample Line (Coc) | `CTS_` | 28 |
| Section View (Cat ngang) | `CTSV_` | 26 |
| Alignment (Tuyen) | `CTA_` | 9 |
| Profile (Trac doc) | `CTP_` | 9 |
| Point (Diem) | `CTPO_` | 8 |
| Corridor (Hanh lang) | `CTC_` | 4 |
| Pipe (Cong) | `CTPI_` | 6 |
| Parcel (Thua dat) | `CTPA_` | 3 |
| Surface (Be mat) | `CTSU_`/`CTS_` | 2 |
| Chung | `CT_` | 3 |
| Menu and Help | - | 6 |
| Test | `Test` | 2 |
| Khac | - | 7 |

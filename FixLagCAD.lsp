;;; ==========================================================================
;;; Tên file : FixLagCAD.lsp
;;; Mô tả    : Khôi phục biến hệ thống và tắt các tính năng Hover / Preview nặng
;;;            giúp AutoCAD / Civil 3D chạy mượt tuyệt đối, hết giật lag.
;;; Lệnh gọi : FIXCAD / TOIUU / MUOT / LAG
;;; ==========================================================================

(vl-load-com)

(defun c:FixLagCAD ( / setSafeVar)
  ;; Hàm gán biến an toàn (không báo lỗi nếu biến không tồn tại ở một số đời CAD)
  (defun setSafeVar (var val / res)
    (setq res (vl-catch-all-apply '(lambda () (setvar var val))))
    (if (vl-catch-all-error-p res)
      nil
      t
    )
  )

  (princ "\n========================================================")
  (princ "\n🚀 ĐANG TỐI ƯU HỆ THỐNG & KHÔI PHỤC BIẾN CAD...")

  ;; 1. Khôi phục phản hồi dòng lệnh và bảo mật nạp file
  (setSafeVar "NOMUTT" 0)          ; 0: Bật lại đầy đủ phản hồi dòng lệnh (tránh bị câm/đơ)
  (setSafeVar "CMDECHO" 1)         ; 1: Hiện phản hồi thực thi lệnh bình thường
  (setSafeVar "SECURELOAD" 0)      ; 0: Tắt cảnh báo bảo mật khi nạp Lisp/DLL
  (setSafeVar "FILEDIA" 1)         ; 1: Đảm bảo luôn hiện hộp thoại chọn file
  (setSafeVar "CMDDIA" 1)          ; 1: Hiện hộp thoại cho các lệnh có Dialog

  ;; 2. Tắt các chế độ Hover / Preview tự động gây giật lag chuột
  (setSafeVar "SELECTIONPREVIEW" 0) ; 0: Tắt sáng viền đối tượng khi rê chuột qua (Cực kỳ nhẹ máy)
  (setSafeVar "ROLLOVERTIPS" 0)     ; 0: Tắt bảng Tooltip thông tin khi rê chuột qua đối tượng
  (setSafeVar "SELECTIONCYCLING" 0) ; 0: Tắt bảng chọn trùng đối tượng khi rê chuột
  (setSafeVar "HPQUICKPREVIEW" "OFF") ; Tắt tự động tính toán xem trước Hatch
  (setSafeVar "HPQUICKPREVIEW" 0)     ; Dự phòng cho các đời CAD nhận giá trị số 0
  (setSafeVar "COMMANDPREVIEW" 0)   ; 0: Tắt xem trước kết quả các lệnh Trim, Extend, Fillet...
  (setSafeVar "PROPERTYPREVIEW" 0)  ; 0: Tắt xem trước thay đổi thuộc tính trong bảng Properties
  (setSafeVar "QPMODE" 0)           ; 0: Tắt popup Quick Properties khi click chọn đối tượng

  ;; 3. Tối ưu tốc độ Zoom / Pan và hiển thị đối tượng
  (setSafeVar "VTENABLE" 0)         ; 0: Tắt hiệu ứng trượt khung nhìn (Zoom/Pan tức thì, không bị trễ)
  (setSafeVar "HIGHLIGHT" 1)        ; 1: Đảm bảo đối tượng vẫn sáng rõ khi ĐÃ CHỌN
  (setSafeVar "DRAWORDERCTL" 3)     ; 3: Tối ưu thứ tự hiển thị đối tượng
  (setSafeVar "CURSORBADGE" 1)      ; 1: Tắt các biểu tượng phù hiệu nhỏ bám theo con trỏ chuột
  (setSafeVar "CACHEMAXFILES" 256)  ; Tăng cache đồ họa

  (princ "\n--------------------------------------------------------")
  (princ "\n✅ ĐÃ TỐI ƯU TOÀN DIỆN:")
  (princ "\n   - NOMUTT = 0 | CMDECHO = 1 | SECURELOAD = 0")
  (princ "\n   - Đã TẮT SELECTIONPREVIEW, ROLLOVERTIPS, HPQUICKPREVIEW (Hết lag chuột)")
  (princ "\n   - Đã TẮT COMMANDPREVIEW, VTENABLE (Zoom/Pan mượt tức thì)")
  (princ "\n========================================================\n")
  (princ)
)

;; Các lệnh tắt thuận tiện cho người dùng
(defun c:FIXCAD () (c:FixLagCAD))
(defun c:TOIUU  () (c:FixLagCAD))
(defun c:MUOT   () (c:FixLagCAD))
(defun c:LAG    () (c:FixLagCAD))

;; Tự động chạy ngay 1 lần khi vừa Load file Lisp vào CAD
(c:FixLagCAD)

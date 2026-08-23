;;; ==========================================================================
;;; LISP: Convert_Text_To_Line (Chuyen doi Text / MText thanh Line/Polyline)
;;; Phien ban toi uu: xu ly hang loat on dinh, chong crash, khong mat net
;;; Lenh su dung trong AutoCAD: CONVERT_TEXT_TO_LINE hoac T2L
;;; ==========================================================================
(vl-load-com)

(defun c:Convert_Text_To_Line (/ *error* acadObj doc ss i hnd obj mn mx p1 p2
                                  oldCmdecho oldPickfirst oldOsm oldViewCtr oldViewSize ss1)
  
  ;; Hàm xử lý bẫy lỗi và khôi phục các biến hệ thống
  (defun *error* (msg)
    (if oldPickfirst (setvar 'PICKFIRST oldPickfirst))
    (if oldOsm       (setvar 'OSMODE oldOsm))
    (if oldCmdecho   (setvar 'CMDECHO oldCmdecho))
    (if (and oldViewCtr oldViewSize)
      (command-s "_.zoom" "_C" oldViewCtr oldViewSize)
    )
    (if (and doc (= 1 (logand 1 (getvar 'UNDOCTL))))
      (vla-EndUndoMark doc)
    )
    (if (and msg (not (wcmatch (strcase msg t) "*break*,*cancel*,*exit*")))
      (princ (strcat "\n[Convert_Text_To_Line] Loi: " msg))
    )
    (princ)
  )

  ;; Kiểm tra Express Tools (TXTEXP)
  (if (not (or (vl-bb-ref 'c:txtexp) (eval 'c:txtexp)))
    (progn
      (load "txtexp.lsp" nil)
      (if (not (eval 'c:txtexp))
        (progn
          (alert "Loi: AutoCAD Express Tools (TXTEXP) chua duoc load hoac chua cai dat!")
          (exit)
        )
      )
    )
  )

  ;; Cho phép chọn cả TEXT và MTEXT (bỏ qua các đối tượng trên Layer bị khóa)
  (prompt "\nChon cac doi tuong Text / MText can chuyen doi sang Line: ")
  (if (setq ss (ssget ":L" '((0 . "TEXT,MTEXT"))))
    (progn
      (setq acadObj      (vlax-get-acad-object)
            doc          (vla-get-ActiveDocument acadObj)
            oldCmdecho   (getvar 'CMDECHO)
            oldPickfirst (getvar 'PICKFIRST)
            oldOsm       (getvar 'OSMODE)
            oldViewCtr   (getvar 'VIEWCTR)
            oldViewSize  (getvar 'VIEWSIZE)
      )

      ;; Đặt nhóm Undo và tắt bớt hiển thị để tăng tốc độ
      (vla-StartUndoMark doc)
      (setvar 'CMDECHO 0)
      (setvar 'OSMODE 0)
      (setvar 'PICKFIRST 1)

      (prompt (strcat "\nDang xu ly " (itoa (sslength ss)) " doi tuong Text..."))

      ;; Duyệt từng Text trong tập chọn
      (repeat (setq i (sslength ss))
        (setq hnd (ssname ss (setq i (1- i))))
        
        ;; 1. KIỂM TRA ĐỐI TƯỢNG CÒN TỒN TẠI (Tránh lỗi Crash nếu đã bị explode trước đó)
        (if (and hnd (entget hnd))
          (progn
            (setq obj (vlax-ename->vla-object hnd))
            
            ;; 2. TÍNH BOUNDING BOX VÀ TẠO KHOẢNG ĐỆM MARGIN 30% ĐỂ ZOOM
            (vla-getboundingbox obj 'mn 'mx)
            (setq p1 (vlax-safearray->list mn)
                  p2 (vlax-safearray->list mx)
            )
            
            ;; Zoom rộng hơn 30% để đảm bảo Text nằm trọn 100% trong Viewport (tránh mất nét WMFOUT)
            (command "_.zoom" "_W"
                     (list (- (car p1) (* 0.3 (- (car p2) (car p1))))
                           (- (cadr p1) (* 0.3 (- (cadr p2) (cadr p1))))
                           0.0)
                     (list (+ (car p2) (* 0.3 (- (car p2) (car p1))))
                           (+ (cadr p2) (* 0.3 (- (cadr p2) (cadr p1))))
                           0.0)
            )

            ;; 3. TẠO TẬP CHỌN CHỈ CHỨA DUY NHẤT 1 ĐỐI TƯỢNG HIỆN TẠI
            ;; Tuyệt đối không dùng (ssget "_C") vì sẽ dính các Text lân cận
            (setq ss1 (ssadd hnd (ssadd)))
            (sssetfirst nil ss1)
            (c:txtexp)
          )
        )
      )

      ;; Khôi phục lại góc nhìn màn hình ban đầu của bạn
      (command "_.zoom" "_C" oldViewCtr oldViewSize)

      ;; Kết thúc Undo group & khôi phục biến hệ thống
      (vla-EndUndoMark doc)
      (setvar 'PICKFIRST oldPickfirst)
      (setvar 'OSMODE oldOsm)
      (setvar 'CMDECHO oldCmdecho)
      (prompt "\n==> Hoan thanh chuyen doi Text sang Line thanh cong!")
    )
    (prompt "\nKhong co doi tuong Text nao duoc chon.")
  )
  (princ)
)

;; Lệnh rút gọn (Alias) để gõ nhanh
(defun c:T2L ()
  (c:Convert_Text_To_Line)
)

(princ "\n=========================================================")
(princ "\n  Da load LISP: Convert_Text_To_Line")
(princ "\n  Go lenh: CONVERT_TEXT_TO_LINE hoac T2L de su dung")
(princ "\n=========================================================")
(princ)

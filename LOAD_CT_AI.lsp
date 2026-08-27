;;; ==========================================================================
;;; Ten file: LOAD_CT_AI.lsp
;;; Mo ta: Lisp doc lap tu dong copy file DLL sang file tam (Temp)
;;;        roi moi NETLOAD, giup file goc KHONG BI KHOA khi can cap nhat.
;;; ==========================================================================

(vl-load-com)

(defun c:LOAD_CT_AI ( / srcDll tempDll tempDir oldCmdEcho oldSecure copyRes netRes)
  (setq srcDll "Y:/5.SOFT T27/1. FOR WORK/1. THIET KE DUONG/2.CIVIL 3D/2026/AutoCAD Civil 3D 2026 Win x64/x64/c3d/2461254.392280.dll")
  
  (setq oldCmdEcho (getvar "CMDECHO")
        oldSecure  (getvar "SECURELOAD"))
  
  (setvar "CMDECHO" 0)
  
  ;; Tam tat canh bao bao mat neu dang bat de nap muot ma
  (vl-catch-all-apply '(lambda () (setvar "SECURELOAD" 0)))

  ;; 1. Kiem tra file goc ton tai
  (if (findfile srcDll)
    (progn
      ;; 2. Tao duong dan file tam voi ten ngau nhien trong thu muc Temp
      ;; Co che Shadow Copy giup file goc tren o Y: KHONG BI KHOA boi AutoCAD
      (setq tempDir (getvar "TEMPPREFIX"))
      (setq tempDll (vl-filename-mktemp "C3D_2461254_" tempDir ".dll"))
      
      (setq copyRes (vl-file-copy srcDll tempDll))
      
      (if copyRes
        (progn
          ;; 3. Thuc hien NETLOAD voi file tam
          (setq netRes (vl-catch-all-apply '(lambda () (command "._NETLOAD" tempDll))))
          
          (if (vl-catch-all-error-p netRes)
            (princ (strcat "\n[AutoLoader] [LOI] Khong the NETLOAD DLL: " (vl-catch-all-error-message netRes)))
          )
        )
        (princ "\n[AutoLoader] [LOI] Khong the sao chep file sang thu muc Temp!")
      )
    )
    (progn
      (princ "\n[AutoLoader] [LOI] KHONG TIM THAY FILE DLL GOC!")
      (princ (strcat "\n   Vui long kiem tra ket noi o Y: hoac duong dan:\n   " srcDll))
    )
  )

  ;; Khoi phuc bien he thong
  (vl-catch-all-apply '(lambda () (setvar "SECURELOAD" oldSecure)))
  (setvar "CMDECHO" oldCmdEcho)
  (princ)
)

;; Lenh rut gon
(defun c:LDLL () (c:LOAD_CT_AI))
(defun c:L246 () (c:LOAD_CT_AI))
(defun c:RL   () (c:LOAD_CT_AI))

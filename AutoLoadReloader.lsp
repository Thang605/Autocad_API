;;; ==========================================================================
;;; Tên file : AutoLoadReloader.lsp
;;; Mô tả    : Tự động nạp CAD_Reloader.dll vào AutoCAD / Civil 3D.
;;;            Sau khi load file này, gõ RELOAD hoặc NETRELOAD để nạp lại
;;;            code mới của MyFirstProject mà không cần khởi động lại CAD.
;;; Cách dùng: Trong AutoCAD gõ:
;;;   (load "C:/Dropbox/0.AI AGENT/6.C#/Autocad 2026_API/AutoLoadReloader.lsp")
;;; ==========================================================================

(defun C:LOADRELOADER ( / srcDll srcDllNew tempDir tempDll cdateStr)
  (vl-load-com)
  (setq srcDll "C:/CadBuild/Autocad2026_API/CAD_Reloader/bin/Debug/CAD_Reloader.dll")
  (setq srcDllNew "C:/CadBuild/Autocad2026_API/CAD_Reloader/bin/Debug_new/CAD_Reloader.dll")

  ;; Uu tien file trong Debug_new neu co ban build moi hon
  (if (findfile srcDllNew)
    (setq srcDll srcDllNew)
  )

  (if (findfile srcDll)
    (progn
      (setq tempDir (getvar "TEMPPREFIX"))
      (setq cdateStr (rtos (getvar "CDATE") 2 6))
      (setq tempDll (strcat tempDir "CAD_Reloader_" cdateStr ".dll"))
      
      ;; Sao chep file DLL sang thu muc temp de khong bi khoa file goc
      (vl-file-copy srcDll tempDll)
      
      ;; Tat canh bao bao mat va nap DLL
      (setvar "SECURELOAD" 0)
      (command "._NETLOAD" tempDll)
      (princ "\n=======================================================")
      (princ (strcat "\n⚡ Da nap CAD_Reloader.dll tu: " srcDll))
      (princ "\n   Go RELOAD  / NETRELOAD    — Tu dong Bien dich & Nap lai code moi")
      (princ "\n   Go RELOADUI / NETRELOADUI — Mo Form giao dien")
      (princ "\n=======================================================\n")
    )
    (progn
      (princ (strcat "\n❌ Khong tim thay file: " srcDll))
      (princ "\n   Hay build project CAD_Reloader truoc!")
    )
  )
  (princ)
)

;; Tu dong chay ngay khi load file LISP
(C:LOADRELOADER)

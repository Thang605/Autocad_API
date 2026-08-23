;;; LoadNetReload.lsp
;;; Tự động load NetReload.dll để sử dụng lệnh NRL và RELOAD
;;; Đặt file này vào thư mục Support của AutoCAD hoặc load thủ công bằng APPLOAD

(defun C:LOADNRL ()
  (if (not *netreload-loaded*)
    (progn
      (princ "\nLoading NetReload.dll...")
      (command "._NETLOAD" "C:\\Dropbox\\0.AI AGENT\\6.C#\\Autocad 2026_API\\NetReload\\bin\\Debug\\NetReload.dll")
      (setq *netreload-loaded* T)
      (princ "\nNetReload.dll loaded successfully!")
      (princ "\nAvailable commands: NRL, RELOAD")
    )
    (princ "\nNetReload.dll already loaded. Use NRL or RELOAD command.")
  )
  (princ)
)

;;; Tự động load khi file LISP được load
(defun s::startup ()
  (C:LOADNRL)
)

(defun C:RL ()
  (princ "\n--- Đang Build MyFirstProject va Reload DLL ---")
  (startapp "powershell.exe" "-ExecutionPolicy Bypass -NoProfile -File \"C:\\Dropbox\\0.AI AGENT\\6.C#\\Autocad 2026_API\\BuildAndReload.ps1\"")
  (princ "\nĐang build trong nền... Sau 3 giay, go RL2 de hoan tat nap DLL vao AutoCAD.")
  (princ)
)

(defun C:RL2 ()
  (setq *lspPath* "C:/Dropbox/0.AI AGENT/6.C#/Autocad 2026_API/last_reload.lsp")
  (if (findfile *lspPath*)
    (progn
      (load *lspPath*)
      (princ "\n[Thanh cong] Da nap phien ban DLL moi nhat vao AutoCAD!")
    )
    (princ "\n[Loi] Chua tim thay last_reload.lsp, hay go RL truoc.")
  )
  (princ)
)

(princ "\n*** LoadNetReload.lsp loaded ***")
(princ "\n*** Go RL (hoac RL2) de build va reload Civil3D_Tools ***")
(princ)

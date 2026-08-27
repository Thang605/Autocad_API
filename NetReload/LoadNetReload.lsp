;;; LoadNetReload.lsp
;;; Tự động chuyển tiếp đến LoadNetReload-onNAS.lsp để nạp lệnh NRL & RL

(vl-load-com)

(setq _nasLspPath "C:/Dropbox/0.AI AGENT/6.C#/Autocad 2026_API/LoadNetReload-onNAS.lsp")
(if (findfile _nasLspPath)
  (load _nasLspPath)
  (princ "\n[Lỗi] Không tìm thấy LoadNetReload-onNAS.lsp!")
)
(princ)

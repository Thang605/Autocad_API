;;; LoadNetReload-onNAS.lsp
;;; Tự động Build C# (Unique Assembly) và load DLL

(vl-load-com)

(defun C:LOAD_CT_AI (/ wsh psCmd buildScript buildRes lspPath txtPath dllPath f _oldCmdEcho _oldNoMutt _oldSecure)
  (setq _oldCmdEcho (getvar "CMDECHO")
        _oldNoMutt  (getvar "NOMUTT")
        _oldSecure  (getvar "SECURELOAD"))
  (setvar "CMDECHO" 0)
  (setvar "NOMUTT" 1)
  (setvar "SECURELOAD" 0)

  (princ "\n[Civil3D Tools] 🔨 Đang tiến hành Build C# với Assembly mới...")
  
  (setq buildScript "C:/Dropbox/0.AI AGENT/6.C#/Autocad 2026_API/BuildProject.ps1"
        lspPath "C:/Dropbox/0.AI AGENT/6.C#/Autocad 2026_API/last_reload.lsp"
        txtPath "C:/Dropbox/0.AI AGENT/6.C#/Autocad 2026_API/last_dll.txt")

  (if (findfile lspPath) (vl-file-delete lspPath))
  (if (findfile txtPath) (vl-file-delete txtPath))

  (setq psCmd (strcat "powershell.exe -ExecutionPolicy Bypass -NoProfile -File \"" buildScript "\""))
  
  (setq wsh (vlax-create-object "WScript.Shell"))
  (if wsh
    (progn
      (setq buildRes (vlax-invoke-method wsh 'Run psCmd 0 1))
      (vlax-release-object wsh)
      
      (if (and (= buildRes 0) (findfile lspPath))
        (progn
          (load lspPath)
          (if (not *nrl-load-count*) (setq *nrl-load-count* 0))
          (setq *nrl-load-count* (1+ *nrl-load-count*))
          (princ (strcat "\n✅ [Civil3D Tools] Build & Nạp thành công lần " (itoa *nrl-load-count*) "!"))
          (if (findfile txtPath)
            (progn
              (setq f (open txtPath "r"))
              (if f
                (progn
                  (setq dllPath (read-line f))
                  (close f)
                  (princ (strcat "\n   • Assembly đã nạp: " (vl-filename-base dllPath) ".dll"))
                )
              )
            )
          )
          (princ "\n   • Lệnh khả dụng: CTPA_BangThongKeParcel, RL, NRL, CLEANNRL...")
        )
        (princ (strcat "\n❌ [Civil3D Tools] Build C# THẤT BẠI! (Mã lỗi: " (itoa buildRes) ")."))
      )
    )
    (princ "\n❌ [Civil3D Tools] Không thể khởi tạo WScript.Shell!")
  )

  (setvar "SECURELOAD" _oldSecure)
  (setvar "NOMUTT" _oldNoMutt)
  (setvar "CMDECHO" _oldCmdEcho)
  (princ)
)

(defun C:RL () (C:LOAD_CT_AI))
(defun C:NRL () (C:LOAD_CT_AI))
(defun C:RELOAD () (C:LOAD_CT_AI))

(defun C:LOADC3D (/ lastLsp localDebug localRelease dllPath)
  (setq lastLsp "C:/Dropbox/0.AI AGENT/6.C#/Autocad 2026_API/last_reload.lsp"
        localDebug "C:/Dropbox/0.AI AGENT/6.C#/Autocad 2026_API/MyFirstProject/bin/Debug/Civil3D_Tools.dll"
        localRelease "C:/Dropbox/0.AI AGENT/6.C#/Autocad 2026_API/MyFirstProject/bin/Release/Civil3D_Tools.dll"
        dllPath "Y:/5.SOFT T27/1. FOR WORK/1. THIET KE DUONG/2.CIVIL 3D/2026/AutoCAD Civil 3D 2026 Win x64/x64/c3d/Civil3D2026.dll")

  (cond
    ((findfile lastLsp)
     (load lastLsp)
     (princ "\n[C3D] ✅ Nạp thành công bản build gần nhất!"))
    ((findfile localDebug)
     (command "._NETLOAD" localDebug)
     (princ "\n[C3D] ✅ Nạp thành công Civil3D_Tools.dll (Debug)!"))
    ((findfile localRelease)
     (command "._NETLOAD" localRelease)
     (princ "\n[C3D] ✅ Nạp thành công Civil3D_Tools.dll (Release)!"))
    ((findfile dllPath)
     (command "._NETLOAD" dllPath)
     (princ "\n[C3D] ✅ Nạp thành công Civil3D tool từ ổ mạng!"))
    (t
     (C:LOAD_CT_AI))
  )
  (princ)
)

(defun C:CLS ()
  (textscr)
  (graphscr)
  (princ "\n--- Command Line Cleared ---")
  (princ)
)

(defun C:CLEANNRL ( / d files f fullPath delCount)
  (setq d "C:/Dropbox/0.AI AGENT/6.C#/Autocad 2026_API/MyFirstProject/bin/Debug"
        delCount 0)
  
  (if (vl-file-directory-p d)
    (progn
      (setq files (append 
                    (vl-directory-files d "NRL_*.dll" 1)
                    (vl-directory-files d "Civil3D_Tools_*.dll" 1)))
      (if files
        (foreach f files
          (setq fullPath (strcat d "/" f))
          (if (vl-file-delete fullPath)
            (setq delCount (1+ delCount))
          )
        )
      )
    )
  )
  (princ (strcat "\n*** Đã dọn dẹp " (itoa delCount) " file build tạm cũ. ***"))
  (princ)
)

(C:LOADC3D)

(princ "\n*** LoadNetReload-onNAS.lsp loaded (Unique Assembly Mode) ***")
(princ "\n*** Go RL hoac NRL de Auto-Build & Reload ***")
(princ)

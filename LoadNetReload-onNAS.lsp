;;; LoadNetReload-onNAS.lsp
;;; Tu dong Build C# (Unique Assembly) va load DLL

(vl-load-com)

(defun C:LOAD_CT_AI (/ wsh psCmd buildScript buildRes lspPath txtPath dllPath f _oldCmdEcho _oldNoMutt _oldSecure)
  (setq _oldCmdEcho (getvar "CMDECHO")
        _oldNoMutt  (getvar "NOMUTT")
        _oldSecure  (getvar "SECURELOAD"))
  (setvar "CMDECHO" 0)
  (setvar "NOMUTT" 1)
  (setvar "SECURELOAD" 0)

  (princ "\n[Civil3D Tools] >> Dang tien hanh Build C# voi Assembly moi...")
  
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
          (princ (strcat "\n[Civil3D Tools] [OK] Build & Nap thanh cong lan " (itoa *nrl-load-count*) "!"))
        )
        (princ (strcat "\n[Civil3D Tools] [LOI] Build C# THAT BAI! (Ma loi: " (itoa buildRes) ")."))
      )
    )
    (princ "\n[Civil3D Tools] [LOI] Khong the khoi tao WScript.Shell!")
  )

  (setvar "SECURELOAD" _oldSecure)
  (setvar "NOMUTT" _oldNoMutt)
  (setvar "CMDECHO" _oldCmdEcho)
  (princ)
)

(defun C:RL () (C:LOAD_CT_AI))
(defun C:NRL () (C:LOAD_CT_AI))
(defun C:RELOAD () (C:LOAD_CT_AI))

(defun C:LOADC3D (/ lastLsp localDebug localRelease dllPath _oldCmdEcho _oldNoMutt _oldSecure)
  (setq _oldCmdEcho (getvar "CMDECHO")
        _oldNoMutt  (getvar "NOMUTT")
        _oldSecure  (getvar "SECURELOAD"))
  (setvar "CMDECHO" 0)
  (setvar "NOMUTT" 1)
  (setvar "SECURELOAD" 0)

  (setq lastLsp "C:/Dropbox/0.AI AGENT/6.C#/Autocad 2026_API/last_reload.lsp"
        localDebug "C:/Dropbox/0.AI AGENT/6.C#/Autocad 2026_API/MyFirstProject/bin/Debug/Civil3D_Tools.dll"
        localRelease "C:/Dropbox/0.AI AGENT/6.C#/Autocad 2026_API/MyFirstProject/bin/Release/Civil3D_Tools.dll"
        dllPath "Y:/5.SOFT T27/1. FOR WORK/1. THIET KE DUONG/2.CIVIL 3D/2026/AutoCAD Civil 3D 2026 Win x64/x64/c3d/2461254.392280.dll")

  (cond
    ((findfile lastLsp)
     (load lastLsp)
     (setvar "NOMUTT" _oldNoMutt)
     (princ "\n[C3D] [OK] Nap thanh cong ban build gan nhat!"))
    ((findfile localDebug)
     (command "._NETLOAD" localDebug)
     (setvar "NOMUTT" _oldNoMutt)
     (princ "\n[C3D] [OK] Nap thanh cong Civil3D_Tools.dll (Debug)!"))
    ((findfile localRelease)
     (command "._NETLOAD" localRelease)
     (setvar "NOMUTT" _oldNoMutt)
     (princ "\n[C3D] [OK] Nap thanh cong Civil3D_Tools.dll (Release)!"))
    ((findfile dllPath)
     (command "._NETLOAD" dllPath)
     (setvar "NOMUTT" _oldNoMutt)
     (princ "\n[C3D] [OK] Nap thanh cong Civil3D tool tu o mang!"))
    (t
     (setvar "NOMUTT" _oldNoMutt)
     (C:LOAD_CT_AI))
  )

  (setvar "SECURELOAD" _oldSecure)
  (setvar "NOMUTT" _oldNoMutt)
  (setvar "CMDECHO" _oldCmdEcho)
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
  (princ (strcat "\n*** Da don dep " (itoa delCount) " file build tam cu. ***"))
  (princ)
)

;; Undefine C# NRL/RELOAD command trong che do an (khong echo ra dong lenh)
(setq _oldCE (getvar "CMDECHO") _oldNM (getvar "NOMUTT"))
(setvar "CMDECHO" 0)
(setvar "NOMUTT" 1)
(vl-catch-all-apply '(lambda () (command "._UNDEFINE" "NRL")))
(vl-catch-all-apply '(lambda () (command "._UNDEFINE" "RELOAD")))
(setvar "NOMUTT" _oldNM)
(setvar "CMDECHO" _oldCE)

(princ "\n[Civil3D Tools] [OK] LoadNetReload-onNAS.lsp da san sang. (Go NRL hoac RL de Reload)")
(princ)


;;; ==========================================================================
;;; Tên file : AutoLoadReloader.lsp
;;; Mô tả    : Tự động quản lý biên dịch và nạp lại file DLL mới nhất
;;;            cho AutoCAD / Civil 3D (.NET 10).
;;;
;;; DUY NHẤT 1 LỆNH:
;;;   RELOAD  : Tự động nạp file DLL mới nhất.
;;;             Nếu bản DLL hiện tại đã nạp rồi -> Tự động Build lại rồi nạp!
;;;
;;; CÁCH DÙNG: Kéo thả file này vào CAD hoặc gõ:
;;;   (load "C:/Dropbox/0.AI AGENT/6.C#/Autocad 2026_API/AutoLoadReloader.lsp")
;;; ==========================================================================

(vl-load-com)

;; Biến toàn cục lưu trữ file DLL đã nạp gần nhất trong phiên CAD này
(if (null *LAST_LOADED_CAD_DLL*)
  (setq *LAST_LOADED_CAD_DLL* "")
)

;; --------------------------------------------------------------------------
;; HÀM NỘI BỘ: Lấy thư mục gốc của Repo dự án
;; --------------------------------------------------------------------------
(defun ALR:GetRepoDir ( / lspFile dir)
  (setq lspFile (findfile "AutoLoadReloader.lsp"))
  (if lspFile
    (setq dir (vl-string-translate "\\" "/" (vl-filename-directory lspFile)))
    (setq dir "C:/Dropbox/0.AI AGENT/6.C#/Autocad 2026_API")
  )
  dir
)

;; --------------------------------------------------------------------------
;; HÀM NỘI BỘ: Đọc một dòng text từ file
;; --------------------------------------------------------------------------
(defun ALR:ReadFileText (filePath / fn line content)
  (setq content nil)
  (if (and filePath (findfile filePath))
    (progn
      (setq fn (open filePath "r"))
      (if fn
        (progn
          (setq line (read-line fn))
          (close fn)
          (if line
            (setq content (vl-string-trim " \t\r\n\"" line))
          )
        )
      )
    )
  )
  content
)

;; --------------------------------------------------------------------------
;; HÀM NỘI BỘ: Chuyển vl-file-systime sang chuỗi YYYYMMDDHHMMSS để so sánh
;; --------------------------------------------------------------------------
(defun ALR:SysTimeToStr (st / pad)
  (defun pad (n) (if (< n 10) (strcat "0" (itoa n)) (itoa n)))
  (if (and st (>= (length st) 7))
    (strcat (itoa (nth 0 st)) (pad (nth 1 st)) (pad (nth 3 st)) (pad (nth 4 st)) (pad (nth 5 st)) (pad (nth 6 st)))
    ""
  )
)

;; --------------------------------------------------------------------------
;; HÀM NỘI BỘ: Chuyển vl-file-systime sang chuỗi ngày giờ dễ đọc
;; --------------------------------------------------------------------------
(defun ALR:SysTimeToReadable (st / pad)
  (defun pad (n) (if (< n 10) (strcat "0" (itoa n)) (itoa n)))
  (if (and st (>= (length st) 7))
    (strcat (itoa (nth 0 st)) "-" (pad (nth 1 st)) "-" (pad (nth 3 st)) " " (pad (nth 4 st)) ":" (pad (nth 5 st)) ":" (pad (nth 6 st)))
    "Không rõ"
  )
)

;; --------------------------------------------------------------------------
;; HÀM NỘI BỘ: Đảm bảo thư mục nạp nằm trong TRUSTEDPATHS
;; --------------------------------------------------------------------------
(defun ALR:EnsureTrustedPath (targetDir / curPaths baseDir)
  (setq baseDir "C:\\CadBuild\\Autocad2026_API\\...")
  (setq curPaths (getvar "TRUSTEDPATHS"))
  (if (null curPaths) (setq curPaths ""))
  (if (not (vl-string-search "CadBuild" curPaths))
    (progn
      (if (= curPaths "")
        (setvar "TRUSTEDPATHS" baseDir)
        (setvar "TRUSTEDPATHS" (strcat curPaths ";" baseDir))
      )
    )
  )
)

;; --------------------------------------------------------------------------
;; HÀM NỘI BỘ: Tìm file DLL mới nhất của MyFirstProject
;; --------------------------------------------------------------------------
(defun ALR:FindLatestDll (binDir repoDir / lastDllTxt dllFromTxt files latestFile latestTime file fullPath st timeStr)
  (setq latestFile nil)
  (setq latestTime "")

  ;; 1. Ưu tiên kiểm tra file last_dll.txt do BuildProject.ps1 ghi ra
  (setq lastDllTxt (strcat repoDir "/last_dll.txt"))
  (setq dllFromTxt (ALR:ReadFileText lastDllTxt))
  (if (and dllFromTxt (findfile dllFromTxt))
    (progn
      (setq st (vl-file-systime dllFromTxt))
      (setq latestFile (vl-string-translate "\\" "/" dllFromTxt))
      (setq latestTime (ALR:SysTimeToStr st))
    )
  )

  ;; 2. Quét thư mục binDir tìm các file Civil3D_Tools_*.dll xem có file nào mới hơn không
  (if (vl-file-directory-p binDir)
    (progn
      (setq files (vl-directory-files binDir "Civil3D_Tools_*.dll" 1))
      (if files
        (foreach f files
          (if (not (vl-string-search ".deps.json" (strcase f t)))
            (progn
              (setq fullPath (vl-string-translate "\\" "/" (strcat binDir "/" f)))
              (setq st (vl-file-systime fullPath))
              (setq timeStr (ALR:SysTimeToStr st))
              (if (> timeStr latestTime)
                (progn
                  (setq latestFile fullPath)
                  (setq latestTime timeStr)
                )
              )
            )
          )
        )
      )
      ;; 3. Fallback tìm file tĩnh Civil3D_Tools.dll nếu chưa tìm được file dynamic nào
      (if (null latestFile)
        (progn
          (setq fullPath (vl-string-translate "\\" "/" (strcat binDir "/Civil3D_Tools.dll")))
          (if (findfile fullPath)
            (setq latestFile fullPath)
          )
        )
      )
    )
  )
  latestFile
)

;; --------------------------------------------------------------------------
;; HÀM NỘI BỘ: Chạy PowerShell để biên dịch dự án đồng bộ (chờ build xong)
;; --------------------------------------------------------------------------
(defun ALR:RunBuild (ps1Path / wsh cmd exitCode ok)
  (setq ok nil)
  (if (and ps1Path (findfile ps1Path))
    (progn
      (princ "\n🔨 [RELOAD] Đang tự động biên dịch lại mã nguồn C# (.NET 10)...")
      (princ "\n   Vui lòng chờ trong giây lát...")
      (setq wsh (vlax-create-object "WScript.Shell"))
      (if wsh
        (progn
          (setq cmd (strcat "powershell.exe -ExecutionPolicy Bypass -NoProfile -WindowStyle Hidden -File \"" ps1Path "\""))
          ;; Tham số 0 = Ẩn cửa sổ; :vlax-true = Chờ tiến trình kết thúc
          (setq exitCode (vlax-invoke-method wsh 'Run cmd 0 :vlax-true))
          (vlax-release-object wsh)
          (if (= exitCode 0)
            (setq ok t)
          )
        )
      )
    )
    (princ (strcat "\n❌ Không tìm thấy script build tại: " ps1Path))
  )
  ok
)

;; --------------------------------------------------------------------------
;; HÀM NỘI BỘ: Nạp một file DLL vào AutoCAD
;; --------------------------------------------------------------------------
(defun ALR:LoadDll (dllPath / fileName st timeReadable)
  (if (and dllPath (findfile dllPath))
    (progn
      (setvar "SECURELOAD" 0)
      (ALR:EnsureTrustedPath (vl-filename-directory dllPath))
      
      (setq fileName (vl-filename-base dllPath))
      (setq st (vl-file-systime dllPath))
      (setq timeReadable (ALR:SysTimeToReadable st))

      (princ "\n=======================================================")
      (princ (strcat "\n⚡ Đang nạp Assembly: " fileName ".dll"))
      (princ (strcat "\n🕒 Thời gian build  : " timeReadable))
      (princ (strcat "\n📁 Đường dẫn        : " dllPath))

      ;; Thực thi lệnh NETLOAD
      (vl-cmdf "._NETLOAD" dllPath)

      ;; Lưu lại file DLL đã nạp
      (setq *LAST_LOADED_CAD_DLL* dllPath)

      (princ "\n✅ [RELOAD THÀNH CÔNG] Code mới nhất đã có hiệu lực ngay lập tức!")
      (princ "\n=======================================================\n")
      t
    )
    (progn
      (princ (strcat "\n❌ Không tìm thấy file DLL: " (if dllPath dllPath "null")))
      nil
    )
  )
)

;; ==========================================================================
;; LỆNH DUY NHẤT: RELOAD
;; Nạp file DLL mới nhất. Nếu file hiện tại đã nạp rồi -> Tự động Build mới rồi nạp!
;; ==========================================================================
(defun C:RELOAD ( / repoDir binDir ps1Path targetDll needBuild)
  (vl-load-com)
  (setvar "SECURELOAD" 0)
  (setq repoDir (ALR:GetRepoDir))
  (setq binDir "C:/CadBuild/Autocad2026_API/MyFirstProject/bin/Debug")
  (setq ps1Path (strcat repoDir "/BuildProject.ps1"))

  ;; Tìm file DLL mới nhất trên đĩa
  (setq targetDll (ALR:FindLatestDll binDir repoDir))

  ;; Kiểm tra xem file DLL này đã được nạp trong phiên CAD hiện tại chưa
  (if (and targetDll (equal (strcase targetDll) (strcase *LAST_LOADED_CAD_DLL*)))
    (setq needBuild t)
    (setq needBuild nil)
  )

  ;; Nếu file hiện tại chưa có hoặc đã nạp rồi -> Tự động build bản mới
  (if (or (null targetDll) needBuild)
    (progn
      (if (ALR:RunBuild ps1Path)
        (progn
          (princ "\n✓ Biên dịch thành công!")
          (setq targetDll (ALR:FindLatestDll binDir repoDir))
        )
        (progn
          (princ "\n❌ Biên dịch thất bại! Vui lòng kiểm tra lại mã nguồn C#.")
        )
      )
    )
  )

  ;; Nạp file DLL kết quả
  (if targetDll
    (ALR:LoadDll targetDll)
    (princ "\n❌ Không tìm thấy file DLL để nạp.")
  )
  (princ)
)

;; ==========================================================================
;; TỰ ĐỘNG THỰC THI KHI LOAD FILE LISP:
;; Tắt cảnh báo bảo mật và nạp ngay DLL mới nhất của dự án
;; ==========================================================================
(progn
  (setvar "SECURELOAD" 0)

  ;; Tự động nạp file DLL MyFirstProject mới nhất hiện có
  (setq repoDir (ALR:GetRepoDir))
  (setq binDir "C:/CadBuild/Autocad2026_API/MyFirstProject/bin/Debug")
  (setq targetDll (ALR:FindLatestDll binDir repoDir))
  (if targetDll
    (ALR:LoadDll targetDll)
  )

  (princ "\n=======================================================")
  (princ "\n⚡ [AUTOLOAD RELOADER] ĐÃ SẴN SÀNG!")
  (princ "\n   👉 Chỉ cần gõ lệnh: RELOAD")
  (princ "\n      (Tự động nạp bản mới nhất; Tự động Build lại nếu vừa sửa code)")
  (princ "\n=======================================================\n")
  (princ)
)

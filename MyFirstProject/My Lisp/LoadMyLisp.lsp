;;; ==========================================================================
;;; CHUONG TRINH : LoadMyLisp.lsp
;;; MO TA        : Tu dong quet va nap toan bo cac file AutoLISP (.lsp, .fas, .vlx)
;;;                trong thu muc nay va toan bo cac thu muc con (C3D, CAD, IN, hay dung...).
;;; LENH GO      : LOADMYLISP hoac LML
;;; ==========================================================================

(vl-load-com)

(defun c:LOADMYLISP ( / Get-RootDir Add-To-TrustedPaths GetAllLispFiles rootDir lispFiles count success errCount res f fname)
  (princ "\n==================================================")
  (princ "\n [LoadMyLisp] Dang quet va nap toan bo file Lisp...")

  ;; 1. Xac dinh thu muc goc chua file LoadMyLisp.lsp
  (defun Get-RootDir ( / fPath )
    (setq fPath (findfile "LoadMyLisp.lsp"))
    (if fPath
      (vl-string-translate "\\" "/" (vl-filename-directory fPath))
      "C:/Dropbox/0.AI AGENT/6.C#/Autocad 2026_API/MyFirstProject/My Lisp"
    )
  )

  ;; 2. Tu dong them thu muc vao TRUSTEDPATHS (bao gom ca cac thu muc con /...) de tranh thong bao bao mat
  (defun Add-To-TrustedPaths (dirPath / curPaths winPath searchPattern)
    (setq winPath (vl-string-translate "/" "\\" dirPath))
    (setq searchPattern (strcat winPath "\\..."))
    (setq curPaths (getvar "TRUSTEDPATHS"))
    (if (not curPaths) (setq curPaths ""))
    (if (not (vl-string-search (strcase winPath) (strcase curPaths)))
      (vl-catch-all-apply
        '(lambda ()
           (if (> (strlen curPaths) 0)
             (setvar "TRUSTEDPATHS" (strcat curPaths ";" searchPattern))
             (setvar "TRUSTEDPATHS" searchPattern)
           )
           (princ (strcat "\n [LoadMyLisp] Da them thu muc vao TRUSTEDPATHS."))
         )
      )
    )
  )

  ;; 3. Ham de quy quet toan bo file .lsp, .fas, .vlx trong thu muc va cac thu muc con
  (defun GetAllLispFiles (dir / result ext)
    (setq dir (vl-string-translate "\\" "/" dir))
    (if (not (wcmatch dir "*/"))
      (setq dir (strcat dir "/"))
    )
    (setq result nil)
    
    ;; Quet cac file trong thu muc hien tai
    (foreach f (vl-directory-files dir "*.*" 1)
      (setq ext (strcase (vl-filename-extension f)))
      (if (member ext '(".LSP" ".FAS" ".VLX"))
        ;; Bo qua chinh file LoadMyLisp.lsp
        (if (not (equal (strcase f) "LOADMYLISP.LSP"))
          (setq result (cons (strcat dir f) result))
        )
      )
    )
    
    ;; De quy vao tat ca cac thu muc con
    (foreach d (vl-directory-files dir "*.*" -1)
      (if (and (/= d ".") (/= d ".."))
        (setq result (append result (GetAllLispFiles (strcat dir d))))
      )
    )
    result
  )

  ;; Thuc thi
  (setq rootDir (Get-RootDir))
  (if (vl-file-directory-p rootDir)
    (progn
      (Add-To-TrustedPaths rootDir)
      (princ (strcat "\n Thu muc goc: " rootDir))
      
      (setq lispFiles (GetAllLispFiles rootDir))
      (setq count (length lispFiles))
      (setq success 0)
      (setq errCount 0)

      (if (> count 0)
        (progn
          (princ (strcat "\n Tim thay tong cong " (itoa count) " file Lisp. Bat dau nap..."))
          (foreach f lispFiles
            (setq fname (strcat (vl-filename-base f) (vl-filename-extension f)))
            ;; Su dung vl-catch-all-apply de tranh loi lam dung chuong trinh
            (setq res (vl-catch-all-apply 'load (list f)))
            (if (vl-catch-all-error-p res)
              (progn
                (setq errCount (1+ errCount))
                (princ (strcat "\n  [-] [LOI] Khong the nap: " fname " (" (vl-catch-all-error-message res) ")"))
              )
              (setq success (1+ success))
            )
          )
          (princ "\n--------------------------------------------------")
          (princ (strcat "\n KET QUA: Da nap thanh cong " (itoa success) "/" (itoa count) " file Lisp."))
          (if (> errCount 0)
            (princ (strcat "\n Canh bao: Co " (itoa errCount) " file gap loi khi nap."))
          )
        )
        (princ "\n Khong tim thay file Lisp nao trong thu muc va cac thu muc con.")
      )
    )
    (princ (strcat "\n [LOI] Khong tim thay thu muc: " rootDir))
  )
  (princ "\n==================================================\n")
  (princ)
)

;; Dinh nghia lenh tat
(defun c:LML () (c:LOADMYLISP))

;; Tu dong thuc thi ngay khi file duoc nap vao AutoCAD
(c:LOADMYLISP)

(princ "\n Da nap xong 'LoadMyLisp.lsp'.")
(princ "\n Su dung lenh LOADMYLISP hoac LML de nap lai toan bo Lisp bat cu luc nao.\n")
(princ)

;; free lisp from cadviet.com
;;; this lisp was downloaded from http://www.cadviet.com/forum/topic/1259-lisp-save-file-hien-tai-vao-1-cho-de-luu/

(vl-load-com)
(defun c:BKSAVE (/	   curpath    oldpath	 curfname   fname
		bfpattern  fpattern   tapso	 somax	    fnamelist
		strso	   nfname
	       )  
  (setq
    curpath   (getvar "DWGPREFIX")
    oldpath   (strcat curpath "OLD")
    curfname  (getvar "DWGNAME")
    fname     (substr curfname 1 (- (strlen curfname) 4))
    Bfpattern (strcat "Z-" fname)
    fpattern  (strcat Bfpattern "*.dwg")
    tapso     nil
    somax     0
  )
  (if (not (vl-file-directory-p oldpath))
    (if	(not (vl-mkdir oldpath))
      (progn
	(alert "\nError when create Old directory")	
	(exit)
      )
    )
  )
  (setq fnamelist (vl-directory-files oldpath fpattern))
  (foreach fname fnamelist
    (setq daiso	(- (strlen fname) (strlen Bfpattern) 4)
	  strso	(substr fname (- (strlen fname) daiso 3) daiso)
	  so	(atoi strso)
	  somax	(max somax so)
    )
  )
  (setq	somax (1+ somax)
	strso (itoa somax)
  )
  (while (< (strlen strso) 3)
    (setq strso (strcat "0" strso))
  )
  (setq nfname (strcat oldpath "\\" Bfpattern strso ".dwg"))
  (command ".save" nfname)
  (command "_qsave")
  (princ)
)


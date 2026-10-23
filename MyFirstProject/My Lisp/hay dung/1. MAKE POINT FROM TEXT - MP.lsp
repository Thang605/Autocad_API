;; free lisp from cadviet.com
;;; this lisp was downloaded from http://www.cadviet.com/forum/topic/2275-lisp-tao-cac-point-3d-tu-file-binh-do/
;;;------------------------------------------------------------------------
(defun emk1p(e / d x y z)
(setq
	d (entget e)
	x (car (cdr (assoc 10 d)))
	y (cadr (cdr (assoc 10 d)))
	z (atof (cdr (assoc 1 d)))
)
(entmake (list (cons 0 "POINT") (cons 10 (list x y z))))
)
;;;=======================================
(defun C:MP( / ss e)
(setq ss (ssget '((0 . "TEXT"))))
(while (setq e (ssname ss 0))
	(emk1p e)
	(ssdel e ss)
)
(princ)
)
;;;=======================================
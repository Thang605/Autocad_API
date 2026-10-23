;; free lisp from cadviet.com
;;; this lisp was downloaded from http://www.cadviet.com/forum/index.php?showtopic=52738
[codebox](defun c:exptxt()
(setq	ss (ssget '((0 . "TEXT")))
fn (getfiled "Ten file: " "" "txt" 1)
f (open fn "w")
lst (ss2ent ss)
)
(foreach e lst
(setq tt (entget e)
p (cdr (assoc 10 tt))
x (rtos (car p))
y (rtos (cadr p))
nd (cdr (assoc 1 tt))
)
(write-line (strcat x "\t" y "\t" nd ) f)
)
(close f)
(princ)
)

(defun ss2ent (ss / sodt index lstent)
(setq sodt (if ss (sslength ss) 0 )
index 0
)
(repeat sodt
(setq ent	 (ssname ss index)
index	 (1+ index)
lstent (cons ent lstent)
)
)
(reverse lstent)
)[/codebox]


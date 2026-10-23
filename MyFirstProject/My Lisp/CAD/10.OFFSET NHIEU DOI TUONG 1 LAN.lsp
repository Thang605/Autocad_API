; thanks to Lee-mac for this defun
(defun csv->lst ( str / pos )
(if (setq pos (vl-string-position 44 str))
    (cons (substr str 1 pos) (csv->lst (substr str (+ pos 2))))
    (list str)
    )
)
; by alanh Feb 2022
(defun c:muloff ( / offs lst val off)
(setq offs (getstring "\Enter offsets 1,2,3 "))
(setq lst (csv->lst offs))
(foreach val lst
(setq off (atof val))
(command "offset" off (entsel "\nSelect object ") (getpoint "\npick side ") "")
)
(princ)
)
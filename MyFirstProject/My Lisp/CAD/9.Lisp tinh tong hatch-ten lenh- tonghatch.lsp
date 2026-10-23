[font=Courier New];; free lisp from cadviet.com @ gia_bach
(defun c:TongHatch (/ cnt tot ss obj )
(if (> (atof (substr (getvar "ACADVER") 1 4)) 16.1);;;AutoCAD 2006 16.2
(progn
(vl-load-com)
(setq cnt 0 tot 0 )
(princ "\nChon doi tuong HATCH : ")
(if (setq ss (ssget '((0 . "HATCH"))))
(progn
(foreach e (mapcar 'vlax-ename->vla-Object (vl-remove-if 'listp (mapcar 'cadr (ssnamex ss))))
(if (vlax-property-available-p e 'Area)
(setq cnt (1+ cnt)
tot (+ tot (vla-get-Area e))
)
)
)
(princ (strcat "\nTong dien tich cua "(itoa cnt) " Hatch la : " (rtos tot) ) )
(setq obj (entsel "\nChon text de ghi ket qua hay Enter de ket thuc."))
(if (and
obj
(setq obj (vlax-ename->vla-object (car obj)))
(eq (vlax-get obj 'ObjectName) "AcDbText")
)
(vla-put-TextString obj (rtos tot))
)
(princ)
)
(princ "\nKhong co doi tuong HATCH nao duoc chon." )
)
)
(alert "\nPhien ban AutoCad cua ban khong ho tro tinh dien tich Hatch ")
)
)[/font]
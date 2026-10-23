(Defun c:trongtam(/ ob ob1)

(vl-load-com)

(setvar "cmdecho" 0) (setvar "osmode" 0)

(setq ob1 (car (entsel "\nChon Chon 1 duong khep kin")))

(command "region" ob1 "")

(setq ob1 (entlast)

ob (vlax-ename->vla-object ob1)

tamO (vlax-safearray->list (vlax-variant-value (vla-get-Centroid ob)))

)

(setvar "osmode" 0)

(command "line" tamO (list 0 0) "")

(entdel ob1)

(setvar "osmode" 33)

(princ)

)
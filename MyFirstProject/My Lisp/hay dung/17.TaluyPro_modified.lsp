;;;;;;;;;;;;;;;;;;;;;;;;;;;;; Edit by Giaptc
(defun c:TLP (/ int refent reply s p cn en p0 pt1 pt2 pt3 pt4 xent xdist)
  (setvar "CMDECHO" 0)
  (setq osmode (getvar "osmode"))
  (setvar "osmode" 0)
  (setvar "unitmode" 0)
  (setvar "dimzin" 0)
  (setvar "aunits" 0)
  (setvar "angbase" (/ pi 2))
  (setvar "angdir" 1)

  (if (not (tblsearch "layer" "maingan"))
    (command "_.layer" "n" "maingan" "color" "4" "maingan" "s" "maingan" \n)
    (command "_.layer" "s" "maingan" \n)
    )
    
   (if (not (tblsearch "layer" "maidai"))
    (command "_.layer" "n" "maidai" "color" "5" "maidai" "s" "maidai" \n)
    (command "_.layer" "s" "maidai" \n)
    )
  (if (not lint)
    (setq lint 1.5)
    )
  (setq int (getdist (strcat "\nNhap khoang cach chia taluy <" (rtos lint 2 3) ">: ")))
  (if int
    (setq lint int)
    (setq int lint)
    )
  (command "_.line" (list 0.0 0.0) (list 0.0 0.0001) "")
  (if (tblsearch "block" "tadtick")
    (command "_.block" "tadtick" "y" (list 0.0 0.0) (entlast) "")
    (command "_.block" "tadtick" (list 0.0 0.0) (entlast) "")
    );if
  (while (setq refent (entsel "\nChon doi tuong can rai taluy: "))
    (command "_.undo" "group")
    (redraw (car refent) 3)
    (initget 1 "daO daP")
    (setq reply (getkword "\nChon kieu taluy Nen Da[O] hay Nen Da[P]: "))
    (prompt "\nChon doi tuong giao tuyen voi mat tu nhien: ")
    (setq s (ssget))
    (command "_.measure" refent "b" "tadtick" "y" int)
    (setq p (ssget "p")
     cn 0
     )
    (if s
      (progn
   (while (< cn (sslength p))
     (setq en (entget (ssname p cn))
      p0 (cdr (assoc 10 en))
      pt1 p0
      pt2 nil
      b (cdr (assoc 50 en))
      )
     (entdel (ssname p cn))
     (setq p1 (polar p0 (+ (/ pi 2) b ) 0.0001))
     (command "_.line" p0 p1 "")
     (command "_.extend" s "" (list (entlast) p1) "")
     (setq xent (entget (entlast)))
     (setq xdist (distance (cdr (assoc 10 xent)) (cdr (assoc 11 xent))))
     (if (not (equal xdist 0.0001 0.0001))
       (setq pt2 (cdr (assoc 11 xent)))
       (progn
         (command "_.extend" s "" (list (entlast) p0) "")
         (setq xent (entget (entlast)))
         (setq xdist (distance (cdr (assoc 10 xent))
                (cdr (assoc 11 xent))
                )
          );setq
         (if (not (equal xdist 0.0001 0.0001))
      (setq pt2 (cdr (assoc 10 xent)))
      );if
         );progn
       );progn


     (entdel (entlast))
     (if pt2
       (if (= reply "daP")
         (if (= (rem cn 2) 0)
      (progn
	(command "_.layer" "s" "maidai" \n)
        (command "_.line" pt1 pt2 "")
	(command "_.layer" "s" "maingan" \n)
        )
      (progn
        (setq pt3 (polar pt1 (angle pt1 pt2) (/ (distance pt1 pt2) 2)))
        (command "_.line" pt1 pt3 "")
        )
      )
         (if (= (rem cn 2) 0)
      (progn
	(command "_.layer" "s" "maidai" \n)
        (command "_.line" pt2 pt1 "")
	(command "_.layer" "s" "maingan" \n)
        );progn
      (progn
        (setq pt3 (polar pt2 (angle pt2 pt1) (/ (distance pt2 pt1) 2)))
        (command "_.line" pt2 pt3 "" )
        );progn
      )
         )
       )
     (setq cn (1+ cn))
     );while
   );progn
      );if
   (command "_.undo" "en")
   );while
 ;; (setvar "osmode" 4133)
 (setvar "dimzin" 8)
 (setvar "osmode" osmode)
 (princ)
)
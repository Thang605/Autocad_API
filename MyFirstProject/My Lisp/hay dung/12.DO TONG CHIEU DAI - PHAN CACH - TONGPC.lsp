;; free lisp from cadviet.com

;;;--------------------------------------------------------------------
(defun Length1(e) (vlax-curve-getDistAtParam e (vlax-curve-getEndParam e)))
;;;--------------------------------------------------------------------
(defun C:TONGPC( / ss L e)
(setq
    ss (ssget  (list (cons 0 "LINE,ARC,CIRCLE,POLYLINE,LWPOLYLINE,ELLIPSE,SPLINE")))
    L 0.0
)
(vl-load-com)
(while (setq e (ssname ss 0))
    (setq L (+ L (length1 e)))
    (ssdel e ss)
)
   (setq ktext (car (entsel "chi vµo text c?n ghi: ")))
   (setq ktratext (entget ktext))
   (setq ktratext1 (cdr (assoc 0 ktratext)))
   (if (= ktratext1 "TEXT")
       (progn
               (setq ktextcu (assoc 1 ktratext))
               (setq textdt (strcat "L pc= " (rtos (- L 0) 2 2) " m"))
               (setq ktextmoi (cons 1 textdt))
               (setq ktratext (subst ktextmoi ktextcu ktratext))
               (entmod ktratext)
               (setq color 50)
               (setq newcolor (cons 62 color))
                  (if (assoc 62 ktratext)
                      (progn
                           (setq oldcolor (assoc 62 ktratext))
                           (setq ktratext (subst newcolor oldcolor ktratext))
                           (entmod ktratext)
                      )
                      (entmod (append ktratext (list (cons 62 color))))
                   )
        )
        (alert "¤i tr?i ¬i, ch?n nh?m r?i, ®? kh«ng ph¶i lµ tetx!")
   )
  (textpage)
  (graphscr)
)
;;;--------------------------------------------------------------------

;| Lisp to reorder existing points using new point numbers, in the order in which they are selected.
   by Jeff Mishler, January 2012;
   For use in C3D 2010 - 2012
 |;

(defun c:reorderpoints (/ *ACAD* C3D C3DDOC ENT GETNUM NEXTPOINT POINTS PROD PRODSTR PTOBJ VERSTR)
  (vl-load-com)
  (setq prod (vlax-product-key))
  (setq	verstr (cond ((vl-string-search "\\R18.0\\" prod)
		      "7.0"
		     )
		     ;;2010
		     ((vl-string-search "\\R18.1\\" prod)
		      "8.0"
		     )
		     ;;2011
		     ((vl-string-search "\\R18.2\\" prod)
		      "9.0"
		     )
		     ;;2012
	       )
  )
  (setq prodStr (strcat "AeccXUiLand.AeccApplication." verstr))

  (if (and (setq *acad* (vlax-get-acad-object))
	   (setq C3D (vla-getinterfaceobject *acad* prodStr))
	   (setq C3Ddoc (vla-get-activedocument C3D))
      )
    (progn
      (setq points nil)
      (vlax-for	point (vlax-get-property c3ddoc 'points)
	(setq points (cons (vlax-get point 'number) points))
      )
      (setq points (vl-sort points '>))
      (setq nextpoint (car points))
      (while (<= nextpoint (car points))
	(setq
	  getnum
	   (getint
	     (strcat "\nPoint number to start the reordering at ["
		     (itoa (1+ nextpoint))
		     "]: "
	     )
	   )
	)
	(if (and getnum
		 (> getnum nextpoint)
	    )
	  (setq nextpoint getnum)
	  (setq nextpoint (1+ nextpoint))
	)
      )
      (while (setq ent (entsel (strcat "\nSelect point to change to #["
				       (itoa nextpoint)
				       "]: "
			       )
		       )
	     )
	(if (eq (cdr (assoc 0 (entget (car ent)))) "AECC_COGO_POINT")
	  (progn
	    (setq ptobj (vlax-ename->vla-object (car ent)))
	    (vlax-put ptobj 'number nextpoint)
	    (setq nextpoint (1+ nextpoint))
	  )
	  (princ "...not a point object, try again!")
	)
      )      
    )
  )
  (princ)
)


(defun C:REBAS ()
(vl-load-com)
  (if (or (ssget "I" '((0 . "AECC_*SURFACE")))
	  (ssget "x" '((0 . "AECC_*SURFACE")))
	  )
    (vlax-for ent (vla-get-activeselectionset
		    (vla-get-activedocument
		      (vlax-get-acad-object)))
      (vlax-invoke ent 'rebuild)
      )
    )
  )

(defun c:RAS () (rebuild_all_surfaces) (princ))
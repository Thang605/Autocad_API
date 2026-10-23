(defun C:UHAddMultipleParcelSegmentlabels ()
; Add Segment Labels to multiple Parcels at once
; Udo Huebner - 01.10.21 for Autodesk Civil 3D Forum
  (Prompt "\nselect Parcels")
  (if (setq ss (ssget (list (cons 0 "AECC_PARCEL"))))
    (progn
      (setq cnt (sslength ss))
      (while (>= (setq cnt (1- cnt)) 0)
        (command "ADDPARCELSEGMENTLABELS" (list (ssname ss cnt)(list 0.0 0.0 0.0)) "" "")
      )
    )
  )
  (princ)
)	 
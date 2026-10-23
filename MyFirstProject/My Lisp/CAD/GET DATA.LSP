(defun c:getData ()
  (setq ss (ssget ":s"))
  (if ss
    (princ (entget (ssname ss 0))))
  (princ)
  )
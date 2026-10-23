(defun C:T2M (/ ss i elist)
(prompt "\nSelect Text objects to convert to MTexts: ")
(setq ss (ssget (list (cons 0 "TEXT"))))
(setq i -1)
(if ss
(repeat (sslength ss)
(setq elist (cdr (assoc -1 (entget (ssname ss (setq i (1+ i)))))))
(command "TXT2MTXT" elist ""); Express Tools command
)
)
)
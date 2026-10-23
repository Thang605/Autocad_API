
(defun c:cache_all_layouts (/ savCurrlayout Layouts^ Layout)

 (setq savCurrLayout (getvar "ctab"))

 (setq Layouts^ (cons "Model" (layoutlist)))
  
 (foreach Layout Layouts^ 
  (setvar "ctab" layout)
 )

 (setvar "ctab" savCurrLayout)

 (princ)
)


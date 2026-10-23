(defun c:INLAYOUT_FIX_MAU ()
  	(setq p1 (getpoint "Chọn điểm trái dưới của khung in:"))
	(setq p2 (list (+ (car p1) 420) (+ (cadr p1) 297)))
  	(setq distance1 420)
	(setq number (getint "Nhập số lượng bản vẽ:"))
	(setq number0 0)
(repeat number	
	(setq distance2 (* distance1 number0))
	(setq p3 (list (+ (car p1) distance2) (+ (cadr p1) 0)))
	(setq p4 (list (+ (car p2) distance2) (+ (cadr p2) 0)))
	(setq number0 (1+ number0 ))
	(COMMAND "-PLOT"
		"Y"
		""
		"PDF reDirect v2" ; PDF reDirect v2
		"A3"
		"Millimeters"
		"Landscape"
		"No"
		"Window"
		p3
		p4
		"1"
		"Center"
		"Yes"		
		"acad.ctb" ;
		"Yes"
		"Yes"
		"Yes"
		"No"
		"No"
		"No"
		"Yes"



  	)  

)
	



)
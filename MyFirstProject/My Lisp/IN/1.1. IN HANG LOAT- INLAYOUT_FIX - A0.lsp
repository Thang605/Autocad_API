(defun c:INLAYOUT_FIX_A0 ()
  	(setq p1 (getpoint "Chọn điểm trái dưới của khung in:"))
	(setq p2 (list (+ (car p1) 1189) (+ (cadr p1) 841)))
  	(setq distance1 1189)
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
		"PDF reDirect v2"
		"A0"
		"Millimeters"
		"Landscape"
		"No"
		"Window"
		p3
		p4
		"1"
		"Center"
		"Yes"		
		"monochrome.ctb"
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
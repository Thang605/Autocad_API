(defun c:INMODEL_TNC3D ()
  	(setq p1 (getpoint "Chọn điểm trái dưới của khung in:"))
	(setq p2 (list (+ (car p1) 84) (+ (cadr p1) 59.4)))
  	(setq distance1 421)
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
		"A3"
		"Millimeters"
		"Landscape"
		"No"
		"Window"
		p3
		p4
		"5:1"
		"Center"
		"Yes"		
		"monochrome.ctb"
		"Yes"
		"As displayed"
		"No"
		"No"
		"Yes"
  	)  

)
	



)
(defun c:ConvexHull (/ _lwpline ss i lst)

 (defun _lwpline (lst)
   (if (> (length lst) 1)
     (entmakex (append
                 (list '(0 . "LWPOLYLINE")
                       '(100 . "AcDbEntity")
                       '(100 . "AcDbPolyline")
                       (cons 90 (length lst))
                       '(70 . 1)
                 )
                 (mapcar (function (lambda (p) (list 10 (car p) (cadr p)))) lst)
               )
     )
   )
 )

 (if (and (setq ss (ssget '((0 . "POINT")))) (>= (setq i (sslength ss)) 3))
   (progn (repeat i (setq lst (cons (cdr (assoc 10 (entget (ssname ss (setq i (1- i)))))) lst)))
          (_lwpline (MeGetConvexHull lst))
   )
 )
 (princ)
)


; == Function MeGetConvexHull
; Calculates the convex hull from a 'cloud' of points.
; Arguments [Type]:
;   Lst = Point list 
[list]
; Return [Type]:
;   > Hull points 
[list]
; Notes:
;   - Algorithm by Graham
;
(defun MeGetConvexHull (Lst / FstCnt LstLen MinCnt NxtCnt TmpLst)
 (setq LstLen (length Lst)
       MinCnt 0
       FstCnt 1
 )
 (while (< FstCnt LstLen)
   (if (< (cadr (nth FstCnt Lst)) (cadr (nth MinCnt Lst)))
     (setq MinCnt FstCnt)
   )
   (setq FstCnt (1+ FstCnt))
 )
 (setq FstCnt 0)
 (while (< FstCnt LstLen)
   (if (and
         (equal (cadr (nth FstCnt Lst)) (cadr (nth MinCnt Lst)) 1E-
         (> (car (nth FstCnt Lst)) (car (nth MinCnt Lst)))
       )
     (setq MinCnt FstCnt)
   )
   (setq FstCnt (1+ FstCnt))
 )
 (setq TmpLst (MeSwapList Lst 0 MinCnt)
       TmpLst (vl-sort
                TmpLst
                (function
                  (lambda (e1 e2)
                    (<
                      (MeCalcTheta (nth 0 TmpLst) e1)
                      (MeCalcTheta (nth 0 TmpLst) e2)
                    )
                  )
                )
              )
       TmpLst (cons (last TmpLst) TmpLst)
       NxtCnt 3
       FstCnt 4
 )
 (while (<= FstCnt LstLen)
   (while (>=
            (MeGetCcw
              (nth NxtCnt TmpLst)
              (nth (1- NxtCnt) TmpLst)
              (nth FstCnt TmpLst)
            )
            0
          )
     (setq NxtCnt (1- NxtCnt))
   )
   (setq NxtCnt (1+ NxtCnt)
         TmpLst (MeSwapList TmpLst FstCnt NxtCnt)
         FstCnt (1+ FstCnt)
   )
 )
 (mapcar '(lambda (l) (nth l TmpLst)) (MeRepeatedList 0 (1- NxtCnt) 1))
)
;
; == Function MeSwapList
; Swaps 2 atoms in a list.
; Arguments [Type]:
;   Lst = List to swap 
[list]
;   Fst = Source position [iNT]
;   Nxt = Target position [iNT]
; Return [Type]:
;   > Swapped list 
[list]
; Notes:
;   None
;
(defun MeSwapList (Lst Fst Nxt / FstVal NxtVal TmpLst)
 (setq FstVal (nth Fst Lst)
       NxtVal (nth Nxt Lst)
       TmpLst (MeEditList 0 Fst NxtVal Lst)
 )
 (MeEditList 0 Nxt FstVal TmpLst)
)
;
; == Function MeCalcTheta
; Calculates a ordinal number between 2 points.
; Arguments [Type]:
;   Pt1 = First point 
[list]
;   Pt2 = Second point 
[list]
; Return [Type]:
;   > A value between 0 and 360 [REAL]
; Notes:
;   None
;
(defun MeCalcTheta (Pt1 Pt2 / X__Abs Y__Abs X__Dif Y__Dif TheVal)
 (setq X__Dif (- (car Pt2) (car Pt1))
       Y__Dif (- (cadr Pt2) (cadr Pt1))
       X__Abs (abs X__Dif)
       Y__Abs (abs Y__Dif)
       TheVal (if (equal (+ X__Abs Y__Abs) 0 1E-5)
                0
                (/ Y__Dif (+ X__Abs Y__Abs))
              )
 )
 (if (< X__Dif 0)
   (setq TheVal (- 2.0 TheVal))
   (if (< Y__Dif 0)
     (setq TheVal (+ 4.0 TheVal))
   )
 )
 (* 90.0 TheVal)
)
;
; == Function MeGetCcw
; Determines the direction of 3 points.
; Arguments [Type]:
;   Pt1 = First point 
[list]
;   Pt1 = Second point 
[list]
;   Pt3 = Third point 
[list]
; Return [Type]:
;   >  1 = ccw [iNT]
;   > -1 = cw [iNT]
;   >  0 = Colinear [iNT]
; Notes:
;   None
;
(defun MeGetCcw (Pt0 Pt1 Pt2 / X1_Dif X1_Sqr X2_Dif X2_Sqr Y1_Dif Y1_Sqr Y2_Dif Y2_Sqr
               )
 (setq X1_Dif (- (car Pt1) (car Pt0))
       Y1_Dif (- (cadr Pt1) (cadr Pt0))
       X2_Dif (- (car Pt2) (car Pt0))
       Y2_Dif (- (cadr Pt2) (cadr Pt0))
       X1_Sqr (* X1_Dif X1_Dif)
       Y1_Sqr (* Y1_Dif Y1_Dif)
       X2_Sqr (* X2_Dif X2_Dif)
       Y2_Sqr (* Y2_Dif Y2_Dif)
 )
 (cond
   ((> (* X1_Dif Y2_Dif) (* Y1_Dif X2_Dif)) 1)
   ((< (* X1_Dif Y2_Dif) (* Y1_Dif X2_Dif)) -1)
   ((or (< (* X1_Dif X2_Dif) 0) (< (* Y1_Dif Y2_Dif) 0)) -1)
   ((< (+ X1_Sqr Y1_Sqr) (+ X2_Sqr Y2_Sqr)) 1)
   (0)
 )
)
;
; == Function MeRepeatedList
; Fills a list with numbers from Srt to End, using increment of Inc.
; Arguments [Type]:
;   Srt = Start number [iNT] or [REAL]
;   End = End number [iNT] or [REAL]
;   Inc = Increment to use [iNT] or [REAL]
; Return [Type]:
;   > List containting all numbers between and including Srt and End
; Notes:
;   If End is not a repeated of End - Srt, End will not be included
;
(defun MeRepeatedList (Srt End Inc / TmpVal RetVal)
 (setq TmpVal (- Srt Inc))
 (while (< TmpVal End)
   (setq RetVal (append
                  RetVal
                  (list (setq TmpVal (+ TmpVal Inc)))
                )
   )
 )
 RetVal
)
;
; == Function MeEditList
; Delete, replace or append a list.
; Arguments [Type]:
;   Mde = Mode  1 append  [iNT]
;         Mode  0 replace [iNT]
;         Mode -1 delete  [iNT]
;   Pos = Position in list [iNT]
;   Val = New value [iNT/REAL/STR/LIST]
;   Lst = List 
[list]
; Return [Type]:
;   > Modified list 
[list]
; Notes:
;   - Delete and replace, require the position (Pos) in list.
;   - Append and replace, require the new value (Val).
;   - :vlax-null items are not allowed in the list argument
;
(defun MeEditList (Mde Pos Val Lst / Countr TmpLst TmpVal)
 (if (= Mde 1)
   (reverse (cons Val (reverse Lst)))
   (progn
     (setq Countr -1
           TmpVal (if (= Mde -1)
                    :vlax-null
                    Val
                  )
           TmpLst (mapcar
                    '(lambda (l)
                       (if (= Pos (setq Countr (1+ Countr)))
                         TmpVal
                         l
                       )
                     )
                    Lst
                  )
     )
     (vl-remove :vlax-null TmpLst)
   )
 )
)
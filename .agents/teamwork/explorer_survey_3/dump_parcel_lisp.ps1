$lisp = '(vl-load-com) (setq p (vlax-ename->vla-object (handent "8F2B"))) (vl-princ-to-string (list (vlax-get-property p ''Area) (vlax-get-property p ''Perimeter) (vlax-get-property p ''SiteName) (vlax-get-property p ''StyleName) (vlax-get-property p ''Number)))'
$body = @{ command = $lisp } | ConvertTo-Json
$res = Invoke-RestMethod -Uri 'http://127.0.0.1:18888/lisp' -Method Post -Body $body -ContentType 'application/json'
$res | Format-Table -AutoSize

$dllPath = "C:\Program Files\Autodesk\AutoCAD 2027\C3D\AeccDbMgd.dll"
$bytes = [System.IO.File]::ReadAllBytes($dllPath)
$text = [System.Text.Encoding]::ASCII.GetString($bytes)

# Tìm các chuỗi kiểu Parcel::
$matches = [System.Text.RegularExpressions.Regex]::Matches($text, "Parcel\.[A-Za-z0-9_]+")
$matches | ForEach-Object { $_.Value } | Select-Object -Unique | Sort-Object | Format-Table -AutoSize

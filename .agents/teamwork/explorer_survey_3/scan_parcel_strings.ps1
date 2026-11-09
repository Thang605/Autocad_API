$dllPath = "C:\Program Files\Autodesk\AutoCAD 2027\C3D\AeccDbMgd.dll"
$bytes = [System.IO.File]::ReadAllBytes($dllPath)
$text = [System.Text.Encoding]::ASCII.GetString($bytes)

# Tìm các chuỗi liên quan đến Parcel
$matches = [System.Text.RegularExpressions.Regex]::Matches($text, "get_[A-Za-z0-9_]*Parcel[A-Za-z0-9_]*|get_[A-Za-z0-9_]*Area[A-Za-z0-9_]*|get_[A-Za-z0-9_]*Perimeter[A-Za-z0-9_]*|Get[A-Za-z0-9_]*Segment[A-Za-z0-9_]*")
$unique = $matches | ForEach-Object { $_.Value } | Select-Object -Unique | Sort-Object
$unique | Format-Table -AutoSize

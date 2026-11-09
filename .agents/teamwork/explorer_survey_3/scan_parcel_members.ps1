$dllPath = "C:\Program Files\Autodesk\AutoCAD 2027\C3D\AeccDbMgd.dll"
$bytes = [System.IO.File]::ReadAllBytes($dllPath)
$text = [System.Text.Encoding]::ASCII.GetString($bytes)

# Tìm các methods bắt đầu với Autodesk.Civil.DatabaseServices.Parcel
$matches = [System.Text.RegularExpressions.Regex]::Matches($text, "Autodesk\.Civil\.DatabaseServices\.Parcel\0[A-Za-z0-9_]+")
$matches | ForEach-Object { $_.Value } | Select-Object -Unique | Sort-Object | Format-Table -AutoSize

# Tìm tất cả ParcelLoop, ParcelSegment, ParcelVertex
$matches2 = [System.Text.RegularExpressions.Regex]::Matches($text, "ParcelLoop[A-Za-z0-9_]*|ParcelSegment[A-Za-z0-9_]*")
$matches2 | ForEach-Object { $_.Value } | Select-Object -Unique | Sort-Object | Format-Table -AutoSize

$c3dPath = "C:\Program Files\Autodesk\AutoCAD 2027\C3D\AeccDbMgd.dll"
$corePath = "C:\Program Files\Autodesk\AutoCAD 2027\accoremgd.dll"
$dbPath = "C:\Program Files\Autodesk\AutoCAD 2027\acdbmgd.dll"
$mgdPath = "C:\Program Files\Autodesk\AutoCAD 2027\acmgd.dll"
$aecPath = "C:\Program Files\Autodesk\AutoCAD 2027\ACA\AecBaseMgd.dll"

[System.Reflection.Assembly]::LoadFrom($corePath) | Out-Null
[System.Reflection.Assembly]::LoadFrom($dbPath) | Out-Null
[System.Reflection.Assembly]::LoadFrom($mgdPath) | Out-Null
[System.Reflection.Assembly]::LoadFrom($aecPath) | Out-Null
$asm = [System.Reflection.Assembly]::LoadFrom($c3dPath)

$t = $asm.GetType("Autodesk.Civil.DatabaseServices.Parcel")
Write-Host "=== PROPERTIES OF PARCEL ==="
$t.GetProperties() | Select-Object Name, PropertyType | Format-Table -AutoSize

Write-Host "=== METHODS OF PARCEL ==="
$t.GetMethods() | Where-Object { $_.DeclaringType.Name -eq "Parcel" } | Select-Object Name, ReturnType | Format-Table -AutoSize

$statsType = $asm.GetType("Autodesk.Civil.DatabaseServices.ParcelProperties")
if (-not $statsType) {
    $statsType = $asm.GetTypes() | Where-Object { $_.Name -match "Parcel.*Stat" }
}
Write-Host "=== PARCEL STATS TYPE ==="
$statsType | Select-Object FullName
if ($statsType) {
    $statsType.GetProperties() | Select-Object Name, PropertyType | Format-Table -AutoSize
}

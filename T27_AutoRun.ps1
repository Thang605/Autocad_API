$ErrorActionPreference = "Stop"

# Detect dotnet executable & directory dynamically
$dotnetExe = ""
$dotnetDir = ""

$userDotnet = "$env:USERPROFILE\.dotnet\dotnet.exe"
$pfDotnet = "$env:ProgramFiles\dotnet\dotnet.exe"

if (Test-Path $userDotnet) {
    $dotnetExe = $userDotnet
    $dotnetDir = "$env:USERPROFILE\.dotnet"
} elseif (Test-Path $pfDotnet) {
    $dotnetExe = $pfDotnet
    $dotnetDir = "$env:ProgramFiles\dotnet"
} else {
    $dotnetExe = "dotnet"
}

if ($dotnetDir) {
    $env:DOTNET_ROOT = $dotnetDir
    $env:PATH = "$dotnetDir;" + $env:PATH
}

Write-Host "Using dotnet: $dotnetExe"

$projectDir = "c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\MyFirstProject"
$csproj = "$projectDir\MyFirstProject.csproj"

# Build Release
& $dotnetExe build $csproj -c Release /nowarn:MSB3061,NU1510

if ($LASTEXITCODE -eq 0) {
    $src = "$projectDir\bin\Release\Civil3D_Tools.dll"
    $dest = "Y:\5.SOFT T27\1. FOR WORK\1. THIET KE DUONG\2.CIVIL 3D\2026\AutoCAD Civil 3D 2026 Win x64\x64\c3d\2461254.392280.dll"
    
    $destDir = Split-Path -Path $dest -Parent
    if (!(Test-Path $destDir)) {
        New-Item -ItemType Directory -Path $destDir -Force | Out-Null
    }

    Copy-Item -Path $src -Destination $dest -Force
    Write-Host "=========================================="
    Write-Host "PHAT HANH THANH CONG!"
    Write-Host "Source: $src"
    Write-Host "Destination: $dest"
    Write-Host "=========================================="
    exit 0
} else {
    Write-Error "Build Release Failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

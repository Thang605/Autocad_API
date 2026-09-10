# BuildAndReload.ps1
$ErrorActionPreference = "Continue"
$projectDir = "C:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\MyFirstProject"
$csprojFile = "$projectDir\MyFirstProject.csproj"
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

# Generate unique assembly name
$randomName = [System.IO.Path]::GetRandomFileName().Replace(".", "")
$uniqueAssemblyName = "Civil3D_Tools_$randomName"

# Execute build
& $dotnetExe build $csprojFile -c Debug /p:AssemblyName=$uniqueAssemblyName

if ($LASTEXITCODE -eq 0) {
    $dllPath = "C:\CadBuild\Autocad2026_API\MyFirstProject\bin\Debug\$uniqueAssemblyName.dll"
    $escapedDllPath = $dllPath.Replace("\", "\\")
    
    # Generate auto-load LISP file
    $lspContent = "(command `"._NETLOAD` `"$escapedDllPath`")`n(princ `"\n[Reload Success] Da load $uniqueAssemblyName.dll vao AutoCAD!\n`")`n(princ)"
    $lspFile = "C:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\last_reload.lsp"
    [System.IO.File]::WriteAllText($lspFile, $lspContent, [System.Text.Encoding]::UTF8)

    Write-Host "`nBuild Succeeded!" -ForegroundColor Green
    Write-Host "DLL: $dllPath" -ForegroundColor Yellow
    Write-Host "LISP: (load `"$($lspFile.Replace('\', '/'))`")" -ForegroundColor Cyan
} else {
    Write-Host "`nBuild Failed!" -ForegroundColor Red
}

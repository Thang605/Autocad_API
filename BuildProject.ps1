# BuildProject.ps1
$ErrorActionPreference = "Stop"
$dotnetExe = "C:\Users\thang\.dotnet\dotnet.exe"
if (-not (Test-Path $dotnetExe)) { $dotnetExe = "dotnet" }
$env:DOTNET_ROOT = "C:\Users\thang\.dotnet"
$env:PATH = "C:\Users\thang\.dotnet;" + $env:PATH

$projectDir = "C:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\MyFirstProject"
$csproj = "$projectDir\MyFirstProject.csproj"

# Generate unique assembly name
$randomName = [System.IO.Path]::GetRandomFileName().Replace(".", "")
$uniqueAssemblyName = "Civil3D_Tools_$randomName"

& $dotnetExe build $csproj -c Debug /p:AssemblyName=$uniqueAssemblyName /p:Clean=false /nowarn:MSB3061,NU1510 --no-restore

if ($LASTEXITCODE -eq 0) {
    $dllPath = "$projectDir\bin\Debug\$uniqueAssemblyName.dll"
    $forwardSlashPath = $dllPath.Replace("\", "/")

    # Output path for AutoLISP to read directly
    [System.IO.File]::WriteAllText("C:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\last_dll.txt", $forwardSlashPath, [System.Text.Encoding]::UTF8)
    
    # Write cleanly formatted AutoLISP command with forward slashes
    $lspContent = "(command `"._NETLOAD`" `"$forwardSlashPath`")"
    [System.IO.File]::WriteAllText("C:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\last_reload.lsp", $lspContent, [System.Text.Encoding]::UTF8)

    exit 0
} else {
    exit $LASTEXITCODE
}

# BuildProject.ps1
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

$repoRoot = $PSScriptRoot
$projectDir = "$repoRoot\MyFirstProject"
$csproj = "$projectDir\MyFirstProject.csproj"

# Generate unique assembly name
$randomName = [System.IO.Path]::GetRandomFileName().Replace(".", "")
$uniqueAssemblyName = "Civil3D_Tools_$randomName"

& $dotnetExe build $csproj -c Debug /p:AssemblyName=$uniqueAssemblyName /p:Clean=false /nowarn:MSB3061,NU1510

if ($LASTEXITCODE -eq 0) {
    $binDebugDir = "C:\CadBuild\Autocad2026_API\MyFirstProject\bin\Debug"
    $dllPath = "$binDebugDir\$uniqueAssemblyName.dll"
    $forwardSlashPath = $dllPath.Replace("\", "/")

    # Đồng bộ sang Civil3D_Tools.dll để lệnh RELOAD / NETRELOAD luôn nhận bản mới nhất
    try {
        Copy-Item $dllPath "$binDebugDir\Civil3D_Tools.dll" -Force
        $pdbPath = "$binDebugDir\$uniqueAssemblyName.pdb"
        if (Test-Path $pdbPath) {
            Copy-Item $pdbPath "$binDebugDir\Civil3D_Tools.pdb" -Force
        }
    } catch { }

    # Dọn dẹp các bản build cũ, giữ lại 5 bản gần nhất
    try {
        $staleDlls = Get-ChildItem -Path $binDebugDir -Filter "Civil3D_Tools_*.dll" |
            Sort-Object LastWriteTimeUtc -Descending |
            Select-Object -Skip 5
        foreach ($f in $staleDlls) {
            Remove-Item $f.FullName -Force -ErrorAction SilentlyContinue
            $p = [System.IO.Path]::ChangeExtension($f.FullName, ".pdb")
            if (Test-Path $p) { Remove-Item $p -Force -ErrorAction SilentlyContinue }
            $d = [System.IO.Path]::ChangeExtension($f.FullName, ".deps.json")
            if (Test-Path $d) { Remove-Item $d -Force -ErrorAction SilentlyContinue }
        }
    } catch { }

    # Output path for AutoLISP to read directly
    [System.IO.File]::WriteAllText("$repoRoot\last_dll.txt", $forwardSlashPath, [System.Text.Encoding]::UTF8)
    
    # Write cleanly formatted AutoLISP command
    $lspContent = "(vl-load-com)`n(vl-cmdf `"._NETLOAD`" `"$forwardSlashPath`")`n(princ `"\n=======================================================\n`")`n(princ `"\n[NETLOAD] DA NAP THANH CONG: $uniqueAssemblyName.dll\n`")`n(princ `"\n👉 Go lenh: AT_Solid_Update_PropertySet de mo Form!\n`")`n(princ `"\n=======================================================\n`")`n(princ)"
    [System.IO.File]::WriteAllText("$repoRoot\last_reload.lsp", $lspContent, [System.Text.Encoding]::UTF8)

    exit 0
} else {
    exit $LASTEXITCODE
}

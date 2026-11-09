$tempDir = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), 'ReflectParcel')
if (Test-Path $tempDir) { Remove-Item $tempDir -Recurse -Force }
New-Item -ItemType Directory -Path $tempDir | Out-Null

$csproj = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <EnableWindowsTargeting>true</EnableWindowsTargeting>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="accoremgd">
      <HintPath>C:\Program Files\Autodesk\AutoCAD 2027\accoremgd.dll</HintPath>
      <Private>False</Private>
    </Reference>
    <Reference Include="Acdbmgd">
      <HintPath>C:\Program Files\Autodesk\AutoCAD 2027\acdbmgd.dll</HintPath>
      <Private>False</Private>
    </Reference>
    <Reference Include="Acmgd">
      <HintPath>C:\Program Files\Autodesk\AutoCAD 2027\acmgd.dll</HintPath>
      <Private>False</Private>
    </Reference>
    <Reference Include="AecBaseMgd">
      <HintPath>C:\Program Files\Autodesk\AutoCAD 2027\ACA\AecBaseMgd.dll</HintPath>
      <Private>False</Private>
    </Reference>
    <Reference Include="AeccDbMgd">
      <HintPath>C:\Program Files\Autodesk\AutoCAD 2027\C3D\AeccDbMgd.dll</HintPath>
      <Private>False</Private>
    </Reference>
  </ItemGroup>
</Project>
'@

$prog = @'
using System;
using System.Linq;
using System.Reflection;

try
{
    var asm = Assembly.LoadFrom(@"C:\Program Files\Autodesk\AutoCAD 2027\C3D\AeccDbMgd.dll");
    var pType = asm.GetType("Autodesk.Civil.DatabaseServices.Parcel");
    Console.WriteLine("=== PARCEL PROPERTIES ===");
    foreach (var p in pType.GetProperties().OrderBy(x => x.Name))
    {
        Console.WriteLine($"{p.Name} : {p.PropertyType.Name}");
    }

    Console.WriteLine("\n=== PARCEL METHODS ===");
    foreach (var m in pType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).OrderBy(x => x.Name))
    {
        Console.WriteLine($"{m.Name} -> {m.ReturnType.Name}");
    }

    Console.WriteLine("\n=== PARCEL RELATED TYPES ===");
    foreach (var t in asm.GetTypes().Where(t => t.Name.Contains("Parcel")).OrderBy(t => t.FullName))
    {
        Console.WriteLine(t.FullName);
    }
}
catch (Exception ex)
{
    Console.WriteLine("ERROR: " + ex);
}
'@

[System.IO.File]::WriteAllText("$tempDir\ReflectParcel.csproj", $csproj)
[System.IO.File]::WriteAllText("$tempDir\Program.cs", $prog)

$env:PATH = "C:\Program Files\Autodesk\AutoCAD 2027;C:\Program Files\Autodesk\AutoCAD 2027\C3D;C:\Program Files\Autodesk\AutoCAD 2027\ACA;" + $env:PATH
$env:DOTNET_ROOT = "$env:USERPROFILE\.dotnet"
$env:PATH = "$env:USERPROFILE\.dotnet;" + $env:PATH
Set-Location $tempDir
& "$env:USERPROFILE\.dotnet\dotnet.exe" run

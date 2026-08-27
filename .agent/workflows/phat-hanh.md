---
description: Phát hành Civil3D_Tools - Build Release và copy đến thư mục phát hành
---

# Workflow: Phát hành (Release)

Khi người dùng nói "phát hành", thực hiện các bước sau:

// turbo-all

1. Build project ở chế độ Release:
```powershell
& "C:\Users\thang\.dotnet\dotnet.exe" build "c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\MyFirstProject\MyFirstProject.csproj" -c Release
```

2. Copy file DLL đến đường dẫn phát hành:
```powershell
Copy-Item -Path "c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API\MyFirstProject\bin\Release\Civil3D_Tools.dll" -Destination "Y:\5.SOFT T27\1. FOR WORK\1. THIET KE DUONG\2.CIVIL 3D\2026\AutoCAD Civil 3D 2026 Win x64\x64\c3d\2461254.392280.dll" -Force
```

3. Thông báo cho người dùng biết file đã được phát hành tại:
`Y:\5.SOFT T27\1. FOR WORK\1. THIET KE DUONG\2.CIVIL 3D\2026\AutoCAD Civil 3D 2026 Win x64\x64\c3d\2461254.392280.dll`


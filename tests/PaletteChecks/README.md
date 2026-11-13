# Palette regression checks

Windows/.NET 10 console runner using the production WPF control and palette data reader. No AutoCAD process or Autodesk DLLs are needed. It exercises Vietnamese search, category ordering, scope filters, collapse/expand, command callbacks, reload invalidation, duplicate entries and empty data.

Run from the repository root in PowerShell:

```powershell
dotnet build tests/PaletteChecks/PaletteChecks.csproj -c Release "-p:CadBuildRoot=$env:TEMP\CodexCadOptimization\"
& "$env:TEMP\CodexCadOptimization\PaletteChecks\bin\Release\PaletteChecks.exe"
& "$env:TEMP\CodexCadOptimization\PaletteChecks\bin\Release\PaletteChecks.exe" --benchmark
& "$env:TEMP\CodexCadOptimization\PaletteChecks\bin\Release\PaletteChecks.exe" --snapshot "$env:TEMP\palette.png"
```

To compare with another revision, save its `ToolPaletteControl.cs` outside the production source tree and build with `-p:PaletteControlSource=<absolute file path>` and a different `CadBuildRoot`. Run both benchmarks sequentially to avoid CPU contention. The snapshot command renders the control after searching and clearing the search; it prints a pixel hash for visual regression comparison.

## Measurement on 2026-11-12

209 built-in commands, Release build, STA thread, median of five runs after warm-up:

| Scenario | Before (ms/filter) | After (ms/filter) | Before (bytes/filter) | After (bytes/filter) |
| --- | ---: | ---: | ---: | ---: |
| Search without matches | 0.538 | 0.034 | 277,767 | 2,345 |
| Alternate all commands / `CT` matches | 15.614 | 5.075 | 3,253,833 | 495,681 |

These measurements cover filtering and WPF control-tree updates, excluding screen layout/painting and the initial index/control creation. Cached rows trade retained memory for fewer allocations during subsequent searches. Reloading clears the cache. Timings vary by machine; they are not test pass/fail thresholds.

Validation: 26 regression checks passed. The 720 × 920 pixel snapshots before and after the optimization matched exactly after searching and clearing the search (SHA256 `95B5827A5D1458664A287F47E6315764BEA2649964ACA4FA159127BBE7748B7C`). The main plugin built successfully in Debug and Release against the installed AutoCAD/Civil 3D 2027 assemblies. NuGet reported NU1900 because its vulnerability feed was unreachable; the same warning occurred before the changes.

The main plugin also requires a build against installed Autodesk assemblies. These WPF tests do not validate Civil 3D transactions or solid mass-property behavior. Those require a separate scratch drawing in Civil 3D; never use a production drawing for mutating tests.

# AGENTS.md — Coding Rules & Architecture Guide for AI Agents (Google Jules / Antigravity)

## Project Overview
- **Solution**: `MyFirstProject.sln` (`c:\Dropbox\0.AI AGENT\6.C#\Autocad 2026_API`)
- **Repository**: `https://github.com/Thang605/Autocad_API.git` (default branch: `master`)
- **Target Framework**: `net10.0-windows` (`x64`, Windows Forms enabled)
- **Projects**:
  1. **`MyFirstProject`** (`AssemblyName: Civil3D_Tools`): Main AutoCAD / Civil 3D plugin containing commands, Windows Forms UI, Civil 3D tools (Alignment, Corridor, Profile, Section, Surface, Parcel, Pipe/Pressure Networks), AutoCAD tools, and MCP Bridge (`AcadMcpBridge.cs`).
  2. **`CAD_Reloader`**: Primary hot-reload utility (`NETRELOAD`, `RELOAD`, `NETRELOADUI`) using shadow copy + `ExtensionLoader.Load()`.
  3. **`NetReload`**: Secondary reload utility (`NRL`).

---

## Important Note for Cloud Linux Environments (Google Jules VM)
- This project references proprietary AutoCAD / Civil 3D Managed .NET Assemblies (`accoremgd.dll`, `acdbmgd.dll`, `acmgd.dll`, `AecBaseMgd.dll`, `AeccDbMgd.dll`, etc.) installed locally on Windows at `C:\Program Files\Autodesk\AutoCAD 2027\`.
- Because these proprietary Autodesk DLLs are not present in Linux containers:
  - **Do NOT remove or alter `<Reference>` / `<HintPath>` entries** in `.csproj` files.
  - **Do NOT commit build artifacts, temporary files, or lockfiles**.
  - When performing code checks, reviews, bug fixes, performance optimizations (**Bolt**), security/reliability hardening (**Sentinel**), or WinForms UI/UX improvements (**Palette**), verify C# syntax, type safety, `Transaction` lifecycle, `IDisposable` cleanup, and adherence to the mandatory rules below through careful static analysis.

---

## Mandatory Development & Code Review Rules

### 1. Mandatory Windows Forms UI for Every Command
- Every command must have a dedicated Windows Forms UI (`System.Windows.Forms.Form`), named `<CommandName>Form.cs` (e.g., `BangThongKeParcelForm.cs`, `DieuChinhDuongDoForm.cs`).
- Forms must use modern layout (`Segoe UI` font, logical `GroupBox` sections, proper tab order, accessibility labels/tooltips, and resize support).
- Prefer clean dropdown lists (`ComboBox` / `DataGridViewComboBoxColumn` populated from drawing tables such as `LayerTable`, `TextStyleTable`, `DimStyleTable`) over manual text entry or crowded multi-button grids; set `UseMnemonic = false` on buttons/labels containing `&`, and ensure generous button dimensions (`Height >= 40px`) so text never clips on high-DPI displays.
- **Two-way Canvas Interaction**:
  - Always use `ed.StartUserInteraction(form)` when picking points or selecting entities on the AutoCAD canvas so the modal form hides cleanly.
  - Display forms via `Application.ShowModalDialog(form)`.

### 2. Mandatory Persistent State (Remembering Previous Settings)
- Every command and Form must remember all parameters, selected objects, and UI options from the previous execution within the session:
  1. **Selected Object IDs**:
     - Store `private static ObjectId _last... = ObjectId.Null;` (or `List<ObjectId>`).
     - On re-open, validate `!_lastObjectId.IsNull && _lastObjectId.IsValid && !_lastObjectId.IsErased` before pre-loading.
  2. **UI Control Values**:
     - Store `private static` fields for all `TextBox`, `ComboBox`, `NumericUpDown`, `CheckBox`, `RadioButton`, `CheckedListBox`, and `_lastFormSize`.
     - Implement `SaveCurrentSettings()` (called on `FormClosing` or execution) and `RestoreLastSettings()` (called in Form constructor/load).

### 3. CAD / Civil 3D Transaction Safety & Resource Management
- **Transactions**:
  - Always wrap database reads/writes in `using (Transaction tr = db.TransactionManager.StartTransaction())` and ensure `tr.Commit()` is called on success.
  - Always use `OpenMode.ForWrite` (or `tr.GetObject(id, OpenMode.ForWrite)`) before modifying any AutoCAD or Civil 3D `DBObject` / `Entity`.
  - Properly `Dispose()` newly created in-memory CAD objects (`Solid3d`, `Polyline`, `DBObject`, `Font`, `Pen`, `Brush`) if they are not appended to the Database/Transaction.
- **Namespace Aliases**:
  - Avoid ambiguous references between WinForms and AutoCAD/Civil 3D by using explicit aliases:
    - `using Application = Autodesk.AutoCAD.ApplicationServices.Application;`
    - `using ATable = Autodesk.AutoCAD.DatabaseServices.Table;`
    - `using WinFormsLabel = System.Windows.Forms.Label;`
    - `using WinFormsButton = System.Windows.Forms.Button;`
    - `using WinFormsTextBox = System.Windows.Forms.TextBox;`
    - `using WinFormsComboBox = System.Windows.Forms.ComboBox;`
    - `using FlowDirection = System.Windows.Forms.FlowDirection;`
    - `using Font = System.Drawing.Font;`

### 4. Command Method Naming Convention (Strict)
- **Single `[CommandMethod("...")]` per command**: Never declare multiple redundant command aliases on the same method.
- **Standard Prefix + Pascal_Snake_Case (`Prefix_Word_Word_Word`)**:
  - `AT_`: General AutoCAD Tools (e.g., `AT_Cut_Solid_Body_To_Clipboard`, `AT_Clip_3d_Solid`)
  - `CT_`: General Civil 3D Tools
  - `CTA_`: Civil 3D Alignment Tools (e.g., `CTA_Kiem_Tra_Tam_Nhin`)
  - `CTC_`: Civil 3D Corridor Tools (e.g., `CTC_Xoa_Region_Bi_Tat`)
  - `CTP_`: Civil 3D Profile Tools
  - `CTS_`: Civil 3D Section / SampleLine Tools (e.g., `CTS_Doi_Ten_Coc_Quy_Tac`)
  - `CTT_`: Civil 3D TIN Surface Tools

### 5. Command Folder Architecture (1 Command = 1 Folder of Small Files)
- **Goal**: every file must be small enough for an AI agent to read and edit in one pass.
- **Scaffold new commands with `NewCommand.ps1`** (repo root), never hand-write from scratch:
  `powershell -ExecutionPolicy Bypass -NoProfile -File .\NewCommand.ps1 -Name CTT_Ten_Lenh -Title "Display title" -Description "Tooltip" -Register`
  - Validates the name (rule 4) and duplicate `[CommandMethod]` names; `-Register` adds one line to `Menu form/ClassicMenu.cs` and `Menu form/ToolPaletteDataReader.cs` (group defaults by prefix, override with `-Group C1..C10 / A1..A9`).
- **Folder layout** `MyFirstProject/Commands/<Prefix>/<BaseName>/`, namespace `MyFirstProject.Commands.<Prefix>.<BaseName>`:
  | File | Responsibility | Limit |
  |---|---|---|
  | `<Name>.Command.cs` | Only `[CommandMethod]`: show Form via `ShowModalDialog`, call Service, wrap with `CommandGuard.Run` | ≤ 80 lines |
  | `<Base>Options.cs` | Plain parameter object (no WinForms references) | ≤ 150 lines |
  | `<Base>Service.cs` (+ `<Base>Service.<Topic>.cs` partials) | Transactions, CAD/Civil 3D reads/writes (`OpenMode.ForWrite` when modifying) | ≤ 400 lines/file |
  | `<Base>Core.cs` / `Core/*.cs` (optional) | Pure algorithms (geometry, math), no Transactions | ≤ 400 lines/file |
  | `<Base>Form.cs` | Constructor, events, canvas picking (`StartUserInteraction`) | ≤ 400 lines |
  | `<Base>Form.Layout.cs` (+ `Form.Layout.<Group>.cs`) | UI construction, one `Build…Group()` method per `GroupBox` | ≤ 300 lines/file |
  | `<Base>Form.Settings.cs` | Persistent state: `static _last…` fields, `SaveCurrentSettings()`, `RestoreLastSettings()` | ≤ 200 lines |
- **Size limits**: files target ≤ 400 lines, hard max 800 (split further with `partial class` by topic); methods target ≤ 60 lines, hard max 120; nested helper classes > 100 lines go to their own file.
- **Use the shared library `MyFirstProject/Shared/` before writing any new helper** (no copy-pasting helpers between commands):
  - `Shared/UI/FormBuilder.cs` (`ApplyStandardStyle`, `BuildBody`, `ActionButton` 40px + `UseMnemonic=false`, `ButtonBar`, `Label/CheckBox/Radio/Number`, `CreateToolTip`, shared `StandardFonts` — never dispose these), `Shared/UI/FieldGroup.cs` (GroupBox + 3-column label|field|pick grid), `Shared/UI/DrawingComboBoxes.cs` (Layer/TextStyle/DimStyle/Linetype/Block/Surface/Alignment dropdowns), `Shared/UI/PersistentState.cs` (`IsAlive`, `FilterAlive`, `RestoreFormSize`, `CaptureFormSize`, `SetValueSafe`).
  - `Shared/Civil/SurfaceHelpers.cs` (`FindSurfaceByName`, `GetSurfaceNames`, `EnsureUniqueSurfaceName`, `FindSurfaceStyleId`, `FindAlignmentByName`, all `ForRead`), `Shared/Cad/ZoomHelper.cs`, `Shared/Cad/CommandGuard.cs`.
  - A helper useful to ≥ 2 commands belongs in `Shared/` (`UI` / `Cad` / `Civil`), not inside a command folder.
- **Legacy commands** (`Acad Tool`, `Civil Tool`, `Civil Tool 2`, ...) stay where they are; split them into the new layout only when doing substantial work, with **zero logic changes** and unchanged command names / menu entries.

### 6. Focus Areas for Automated Code Checks (Jules Agents)
- **🛡️ Sentinel (Defensive Coding & Reliability)**:
  - Check for unhandled exceptions in `[CommandMethod]` entry points (`try-catch` logging to `ed.WriteMessage`), null dereferences on `tr.GetObject(...)`, invalid/erased `ObjectId` access, `eLockViolation` (missing `doc.LockDocument()` in modeless contexts), and unclosed streams/files when reading/writing Excel/CSV/JSON.
- **⚡ Bolt (Performance Optimization)**:
  - Identify $O(N^2)$ entity lookups or repeated `ModelSpace` scans inside loops; replace with `Dictionary`/`HashSet` lookups.
  - Avoid opening the same `ObjectId` repeatedly inside nested loops; cache retrieved data or pre-filter with `SelectionFilter`.
  - Optimize geometry/solid boolean operations and batch `Table` updates (`SuppressRegenerateTable(true)`).
- **🎨 Palette (WinForms UX & Accessibility)**:
  - Improve WinForms usability: keyboard shortcuts (`AcceptButton`, `CancelButton`), input validation feedback, clear Vietnamese labels/tooltips, `UseWaitCursor` / progress indication during heavy CAD operations, and proper `Dispose()` of GDI+ resources (`Font`, `Color`, `Pen`) — except the shared `StandardFonts`, which live for the whole session.
- **📐 Structure (all agents)**:
  - In new code, flag files > 800 lines, methods > 120 lines, and helpers duplicated instead of using `MyFirstProject/Shared/`. Do not mix structural moves with logic changes in the same PR.

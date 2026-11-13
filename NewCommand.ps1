<#
.SYNOPSIS
    Sinh khung (scaffold) cho 1 lệnh AutoCAD / Civil 3D mới theo kiến trúc "1 lệnh = 1 thư mục nhiều file nhỏ".

.DESCRIPTION
    Tạo thư mục MyFirstProject\Commands\<Prefix>\<BaseName>\ gồm 6 file:
      <Name>.Command.cs          - [CommandMethod] mỏng: mở Form → gọi Service
      <BaseName>Options.cs       - Thông số người dùng chọn (dùng luôn làm mô hình ghi nhớ)
      <BaseName>Service.cs       - Xử lý CAD/Civil 3D trong Transaction (OpenMode.ForWrite)
      <BaseName>Form.cs          - Sự kiện & logic UI (pick trên bản vẽ, validate)
      <BaseName>Form.Layout.cs   - Dựng control bằng Shared/UI (FormBuilder, FieldGroup, DrawingComboBoxes)
      <BaseName>Form.Settings.cs - SaveCurrentSettings / RestoreLastSettings
    Khung sinh ra BUILD ĐƯỢC NGAY và chạy được (lệnh mẫu: chọn đối tượng → đổi Layer).
    Thay các chỗ đánh dấu "TODO" bằng logic thật của lệnh.

    Kèm tùy chọn -Register: tự chèn 1 dòng vào ClassicMenu.cs và ToolPaletteDataReader.cs.

.PARAMETER Name
    Tên lệnh theo chuẩn Prefix_Word_Word (AT_, CT_, CTA_, CTC_, CTP_, CTS_, CTT_). Ví dụ: CTT_Tao_Mat_Phang_Tu_Featureline

.PARAMETER Title
    Tiêu đề tiếng Việt hiển thị trên Form, Menu, Tool Palette. Ví dụ: "Tạo mặt phẳng từ Feature Line"

.PARAMETER Description
    Mô tả ngắn hiển thị trên Tool Palette (mặc định = Title).

.PARAMETER BaseName
    Tên gốc cho class/thư mục (mặc định: bỏ tiền tố và dấu gạch dưới của Name, ví dụ TaoMatPhangTuFeatureline).

.PARAMETER Group
    Nhóm menu/palette: C1..C10 (Civil), A1..A9 (AutoCAD). Mặc định suy ra từ tiền tố:
    CTA→C1, CTP→C2, CTC→C3, CTS→C4, CTT→C6. Tiền tố AT_/CT_ bắt buộc chỉ định khi dùng -Register.

.PARAMETER Register
    Tự chèn dòng đăng ký vào ClassicMenu.cs và ToolPaletteDataReader.cs.

.PARAMETER Force
    Cho phép ghi đè thư mục lệnh đã tồn tại.

.EXAMPLE
    .\NewCommand.ps1 -Name CTT_Tao_Mat_Phang_Tu_Featureline -Title "Tạo mặt phẳng từ Feature Line" -Register

.EXAMPLE
    .\NewCommand.ps1 -Name AT_Danh_So_Text -Title "Đánh số Text tự động" -Group A3 -Register
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Name,
    [Parameter(Mandatory = $true)][string]$Title,
    [string]$Description = "",
    [string]$BaseName = "",
    [ValidateSet("", "C1", "C2", "C3", "C4", "C5", "C6", "C7", "C8", "C9", "C10",
                 "A1", "A2", "A3", "A4", "A5", "A6", "A7", "A8", "A9")]
    [string]$Group = "",
    [switch]$Register,
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------
# 1. Kiểm tra đầu vào
# ---------------------------------------------------------------------------
if ($Name -cnotmatch '^(AT|CT|CTA|CTC|CTP|CTS|CTT)(_[A-Z0-9][A-Za-z0-9]*)+$') {
    throw "Tên lệnh '$Name' sai chuẩn. Dùng Prefix_Word_Word với Prefix thuộc AT, CT, CTA, CTC, CTP, CTS, CTT và mỗi từ viết hoa chữ đầu (ví dụ CTT_Tao_Mat_Phang)."
}

$prefix = $Name.Split('_')[0]
if ([string]::IsNullOrWhiteSpace($BaseName)) {
    $BaseName = ($Name.Split('_') | Select-Object -Skip 1) -join ''
}
if ($BaseName -cnotmatch '^[A-Z][A-Za-z0-9]*$') {
    throw "BaseName '$BaseName' không hợp lệ (phải là định danh C# PascalCase)."
}
if ([string]::IsNullOrWhiteSpace($Description)) { $Description = $Title }

$repoRoot    = $PSScriptRoot
$projectDir  = Join-Path $repoRoot "MyFirstProject"
$commandDir  = Join-Path (Join-Path (Join-Path $projectDir "Commands") $prefix) $BaseName
$namespace   = "MyFirstProject.Commands.$prefix.$BaseName"

if (-not (Test-Path $projectDir)) { throw "Không tìm thấy thư mục project: $projectDir" }

# Tên lệnh không được trùng với lệnh đã có (bỏ qua thư mục đích khi -Force).
$pattern = 'CommandMethod\("' + [regex]::Escape($Name) + '"'
$dupes = Get-ChildItem -Path $repoRoot -Recurse -Filter *.cs -File |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and -not $_.FullName.StartsWith($commandDir) } |
    Select-String -Pattern $pattern -List
if ($dupes) {
    throw "Lệnh '$Name' đã tồn tại tại: $($dupes[0].Path)"
}

if ((Test-Path $commandDir) -and -not $Force) {
    throw "Thư mục đã tồn tại: $commandDir (dùng -Force để ghi đè)."
}

# ---------------------------------------------------------------------------
# 2. Template (placeholder: __NAME__ __BASE__ __NS__ __TITLE__ __TITLECS__)
# ---------------------------------------------------------------------------
function ConvertTo-CSharpString([string]$s) { return $s.Replace('\', '\\').Replace('"', '\"') }

$templates = [ordered]@{}

$templates["$Name.Command.cs"] = @'
using Autodesk.AutoCAD.Runtime;

using MyFirstProject.Shared.Cad;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(__NS__.__NAME___Command))]

namespace __NS__
{
    /// <summary>
    /// __TITLE__.
    /// File lệnh CHỈ mở Form và gọi Service - không viết logic xử lý ở đây.
    /// </summary>
    public class __NAME___Command
    {
        [CommandMethod("__NAME__")]
        public static void Execute()
        {
            CommandGuard.Run("__NAME__", (doc, db, ed) =>
            {
                __BASE__Options options;
                using (var form = new __BASE__Form(db, ed))
                {
                    if (Application.ShowModalDialog(form) != System.Windows.Forms.DialogResult.OK)
                    {
                        ed.WriteMessage("\n[__NAME__] Đã hủy lệnh.");
                        return;
                    }
                    options = form.Options;
                }

                int processed = __BASE__Service.Execute(db, ed, options);
                ed.WriteMessage($"\n[__NAME__] Hoàn thành: {processed} đối tượng.");
            });
        }
    }
}
'@

$templates["${BaseName}Options.cs"] = @'
using System.Collections.Generic;

using Autodesk.AutoCAD.DatabaseServices;

namespace __NS__
{
    /// <summary>
    /// Thông số người dùng chọn trên Form, truyền từ Form sang Service.
    /// Dùng luôn làm mô hình GHI NHỚ giữa các lần chạy (xem __BASE__Form.Settings.cs).
    /// Chỉ chứa dữ liệu - không chứa code UI hay code ghi bản vẽ.
    /// </summary>
    public sealed class __BASE__Options
    {
        /// <summary>Đối tượng nguồn đã chọn trên bản vẽ.</summary>
        public List<ObjectId> SourceIds { get; set; } = new List<ObjectId>();

        // TODO: thay/bổ sung thông số thật của lệnh.
        public string LayerName { get; set; } = "0";

        public bool ZoomAfterRun { get; set; } = true;
    }
}
'@

$templates["${BaseName}Service.cs"] = @'
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

using MyFirstProject.Shared.Cad;
using MyFirstProject.Shared.UI;

namespace __NS__
{
    /// <summary>
    /// Xử lý CAD / Civil 3D của lệnh __NAME__ (Transaction, OpenMode.ForWrite).
    /// Không chứa code WinForms. Khi file vượt ~400 dòng: tách thành partial class
    /// (__BASE__Service.Xxx.cs) hoặc đưa thuật toán thuần C# sang thư mục Core/.
    /// </summary>
    internal static class __BASE__Service
    {
        /// <summary>Thực thi lệnh. Trả về số đối tượng đã xử lý.</summary>
        public static int Execute(Database db, Editor ed, __BASE__Options options)
        {
            int processed = 0;
            var aliveIds = PersistentState.FilterAlive(options.SourceIds, db);

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                foreach (ObjectId id in aliveIds)
                {
                    if (tr.GetObject(id, OpenMode.ForWrite) is not Entity ent) continue;

                    // TODO: thay bằng logic thật của lệnh.
                    ent.Layer = options.LayerName;
                    processed++;
                }
                tr.Commit();
            }

            if (options.ZoomAfterRun && processed > 0)
            {
                ZoomHelper.ZoomToEntities(ed, aliveIds);
            }
            return processed;
        }
    }
}
'@

$templates["${BaseName}Form.cs"] = @'
using System;
using System.Collections.Generic;
using System.Windows.Forms;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

using MyFirstProject.Shared.UI;

namespace __NS__
{
    /// <summary>
    /// Form lệnh __NAME__ - phần SỰ KIỆN và LOGIC UI.
    /// Bố cục control: __BASE__Form.Layout.cs | Ghi nhớ thông số: __BASE__Form.Settings.cs.
    /// Form KHÔNG ghi bản vẽ - chỉ thu thập thông số vào <see cref="Options"/>.
    /// </summary>
    public partial class __BASE__Form : Form
    {
        private readonly Database _db;
        private readonly Editor _ed;
        private readonly List<ObjectId> _sourceIds = new List<ObjectId>();

        /// <summary>Thông số đã xác nhận - đọc sau khi ShowModalDialog trả về OK.</summary>
        public __BASE__Options Options { get; private set; } = new __BASE__Options();

        public __BASE__Form(Database db, Editor ed)
        {
            _db = db;
            _ed = ed;

            BuildLayout();
            LoadDrawingData();
            RestoreLastSettings();
            FormClosing += (s, e) => SaveCurrentSettings();
        }

        /// <summary>Nạp danh sách từ bản vẽ vào các ComboBox (không bắt người dùng gõ tay).</summary>
        private void LoadDrawingData()
        {
            DrawingComboBoxes.Fill(cboLayer, DrawingComboBoxes.GetLayerNames(_db), "0");
        }

        private void BtnPickSource_Click(object? sender, EventArgs e)
        {
            using (_ed.StartUserInteraction(this))
            {
                PromptSelectionResult psr = _ed.GetSelection();
                if (psr.Status != PromptStatus.OK) return;

                _sourceIds.Clear();
                _sourceIds.AddRange(psr.Value.GetObjectIds());
            }
            UpdateSourceSummary();
        }

        private void BtnExecute_Click(object? sender, EventArgs e)
        {
            if (!ValidateInput()) return;

            Options = ReadOptionsFromControls();
            DialogResult = DialogResult.OK;
            Close();
        }

        private bool ValidateInput()
        {
            if (_sourceIds.Count == 0)
            {
                MessageBox.Show(this, "Chưa chọn đối tượng nào trên bản vẽ.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                btnPickSource.Focus();
                return false;
            }
            if (cboLayer.SelectedIndex < 0)
            {
                MessageBox.Show(this, "Chưa chọn Layer đích.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                cboLayer.Focus();
                return false;
            }
            return true;
        }

        private void UpdateSourceSummary()
        {
            lblSourceSummary.Text = $"Đã chọn: {_sourceIds.Count} đối tượng";
        }
    }
}
'@

$templates["${BaseName}Form.Layout.cs"] = @'
using System.Drawing;
using System.Windows.Forms;

using MyFirstProject.Shared.UI;

using WinFormsButton = System.Windows.Forms.Button;
using WinFormsComboBox = System.Windows.Forms.ComboBox;
using WinFormsLabel = System.Windows.Forms.Label;

namespace __NS__
{
    /// <summary>
    /// Form lệnh __NAME__ - phần BỐ CỤC. Mỗi GroupBox = 1 method Build...Group().
    /// Khi file vượt ~300 dòng: tách thêm __BASE__Form.Layout.Xxx.cs.
    /// </summary>
    public partial class __BASE__Form
    {
        private WinFormsLabel lblSourceSummary = null!;
        private WinFormsButton btnPickSource = null!;
        private WinFormsComboBox cboLayer = null!;
        private CheckBox chkZoomAfterRun = null!;
        private WinFormsButton btnExecute = null!;
        private WinFormsButton btnCancel = null!;

        private void BuildLayout()
        {
            FormBuilder.ApplyStandardStyle(this, "__TITLECS__", new Size(560, 380));
            ToolTip toolTip = FormBuilder.CreateToolTip(this);

            FieldGroup grpSource = BuildSourceGroup(toolTip);
            FieldGroup grpOptions = BuildOptionsGroup(toolTip);

            btnExecute = FormBuilder.ActionButton("Thực hiện", ButtonKind.Primary);
            btnExecute.Click += BtnExecute_Click;
            btnCancel = FormBuilder.ActionButton("Hủy");
            btnCancel.DialogResult = DialogResult.Cancel;
            AcceptButton = btnExecute;
            CancelButton = btnCancel;

            FormBuilder.BuildBody(this, FormBuilder.ButtonBar(btnExecute, btnCancel), grpSource.Box, grpOptions.Box);
        }

        private FieldGroup BuildSourceGroup(ToolTip toolTip)
        {
            lblSourceSummary = FormBuilder.Label("Đã chọn: 0 đối tượng");
            btnPickSource = FormBuilder.ActionButton("Chọn trên bản vẽ...", ButtonKind.Normal, 170);
            btnPickSource.Click += BtnPickSource_Click;

            return new FieldGroup("1. Đối tượng nguồn", toolTip)
                .AddRow("Đối tượng:", lblSourceSummary, btnPickSource, "Bấm để chọn đối tượng trên bản vẽ (Form tạm ẩn)");
        }

        private FieldGroup BuildOptionsGroup(ToolTip toolTip)
        {
            // TODO: thay/bổ sung control cho thông số thật của lệnh.
            cboLayer = DrawingComboBoxes.CreateDropDown();
            chkZoomAfterRun = FormBuilder.CheckBox("Zoom tới đối tượng sau khi chạy", true);

            return new FieldGroup("2. Tùy chọn", toolTip)
                .AddRow("Layer đích:", cboLayer, null, "Layer gán cho đối tượng sau khi xử lý")
                .AddFullRow(chkZoomAfterRun);
        }
    }
}
'@

$templates["${BaseName}Form.Settings.cs"] = @'
using System.Collections.Generic;
using System.Drawing;

using Autodesk.AutoCAD.DatabaseServices;

using MyFirstProject.Shared.UI;

namespace __NS__
{
    /// <summary>
    /// Form lệnh __NAME__ - phần GHI NHỚ THÔNG SỐ (quy tắc GEMINI.md mục 2).
    /// Thêm thông số mới: (1) thêm thuộc tính vào __BASE__Options, (2) đọc trong ReadOptionsFromControls,
    /// (3) gán lại trong RestoreLastSettings.
    /// </summary>
    public partial class __BASE__Form
    {
        private static __BASE__Options _last = new __BASE__Options();
        private static Size _lastFormSize = Size.Empty;

        private __BASE__Options ReadOptionsFromControls()
        {
            return new __BASE__Options
            {
                SourceIds = new List<ObjectId>(_sourceIds),
                LayerName = DrawingComboBoxes.GetSelectedName(cboLayer),
                ZoomAfterRun = chkZoomAfterRun.Checked
            };
        }

        private void RestoreLastSettings()
        {
            PersistentState.RestoreFormSize(this, _lastFormSize);

            _sourceIds.Clear();
            _sourceIds.AddRange(PersistentState.FilterAlive(_last.SourceIds, _db));
            DrawingComboBoxes.SelectByName(cboLayer, _last.LayerName);
            chkZoomAfterRun.Checked = _last.ZoomAfterRun;

            UpdateSourceSummary();
        }

        private void SaveCurrentSettings()
        {
            _last = ReadOptionsFromControls();
            _lastFormSize = PersistentState.CaptureFormSize(this, _lastFormSize);
        }
    }
}
'@

# ---------------------------------------------------------------------------
# 3. Ghi file
# ---------------------------------------------------------------------------
$utf8Bom = New-Object System.Text.UTF8Encoding($true)
New-Item -ItemType Directory -Path $commandDir -Force | Out-Null

$titleCs = ConvertTo-CSharpString $Title
foreach ($fileName in $templates.Keys) {
    $content = $templates[$fileName].
        Replace('__NAME__', $Name).
        Replace('__BASE__', $BaseName).
        Replace('__NS__', $namespace).
        Replace('__TITLECS__', $titleCs).
        Replace('__TITLE__', $Title)
    $content = ($content -replace "`r?`n", [Environment]::NewLine) + [Environment]::NewLine
    [System.IO.File]::WriteAllText((Join-Path $commandDir $fileName), $content, $utf8Bom)
    Write-Host "  + $fileName"
}
Write-Host "Đã tạo khung lệnh tại: $commandDir" -ForegroundColor Green

# ---------------------------------------------------------------------------
# 4. Đăng ký Menu + Tool Palette
# ---------------------------------------------------------------------------
$groupMap = @{
    "C1" = "subMenuAlignment"; "C2" = "subMenuProfile"; "C3" = "subMenuCorridor"; "C4" = "subMenuSample"
    "C5" = "subMenuSection";   "C6" = "subMenuSurface"; "C7" = "subMenuPoint";    "C8" = "subMenuPipe"
    "C9" = "subMenuParcel";    "C10" = "subMenuProp"
    "A1" = "subMenuLayerScale"; "A2" = "subMenuMeasure"; "A3" = "subMenuText";     "A4" = "subMenu3D"
    "A5" = "subMenuBlock";      "A6" = "subMenuViewport"; "A7" = "subMenuXref";    "A8" = "subMenuExportPrint"
    "A9" = "subMenuSystem"
}
$prefixDefault = @{ "CTA" = "C1"; "CTP" = "C2"; "CTC" = "C3"; "CTS" = "C4"; "CTT" = "C6" }
if ([string]::IsNullOrWhiteSpace($Group) -and $prefixDefault.ContainsKey($prefix)) { $Group = $prefixDefault[$prefix] }

$titleArg = ConvertTo-CSharpString $Title
$descArg  = ConvertTo-CSharpString $Description

function Add-LineAfterLast([string]$path, [string]$matchRegex, [string]$newLine) {
    $bytes   = [System.IO.File]::ReadAllBytes($path)
    $hasBom  = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $text    = (New-Object System.Text.UTF8Encoding($false)).GetString($bytes)
    if ($hasBom) { $text = $text.Substring(1) }
    $eol     = if ($text.Contains("`r`n")) { "`r`n" } else { "`n" }
    $lines   = [System.Collections.Generic.List[string]]::new([string[]]($text -split "`r?`n"))

    $last = -1
    for ($i = 0; $i -lt $lines.Count; $i++) { if ($lines[$i] -match $matchRegex) { $last = $i } }
    if ($last -lt 0) { return $false }

    $indent = ([regex]::Match($lines[$last], '^\s*')).Value
    $lines.Insert($last + 1, $indent + $newLine)
    [System.IO.File]::WriteAllText($path, ($lines -join $eol), (New-Object System.Text.UTF8Encoding($hasBom)))
    return $true
}

$menuFile    = Join-Path (Join-Path $projectDir "Menu form") "ClassicMenu.cs"
$paletteFile = Join-Path (Join-Path $projectDir "Menu form") "ToolPaletteDataReader.cs"
$menuLine    = "AddMenuItem(<subMenu>, `"$titleArg`", `"$Name`");"
$paletteLine = "Add(<Nhom>, `"$titleArg`", `"$Name`", `"$descArg`");"

if ($Register -and -not [string]::IsNullOrWhiteSpace($Group)) {
    $subMenu = $groupMap[$Group]
    $okMenu = Add-LineAfterLast $menuFile ('AddMenuItem\(\s*' + $subMenu + '\s*,') $menuLine.Replace('<subMenu>', $subMenu)
    $okPal  = Add-LineAfterLast $paletteFile ('^\s*Add\(\s*' + $Group + '\s*,') $paletteLine.Replace('<Nhom>', $Group)
    if ($okMenu) { Write-Host "  + ClassicMenu.cs ($subMenu)" } else { Write-Warning "Không tìm thấy nhóm $subMenu trong ClassicMenu.cs - hãy thêm tay." }
    if ($okPal)  { Write-Host "  + ToolPaletteDataReader.cs ($Group)" } else { Write-Warning "Không tìm thấy nhóm $Group trong ToolPaletteDataReader.cs - hãy thêm tay." }
}
else {
    if ($Register) { Write-Warning "Tiền tố $prefix chưa có nhóm mặc định - chạy lại với -Group (C1..C10 / A1..A9) hoặc thêm tay:" }
    else { Write-Host "Chưa đăng ký Menu/Palette (thêm -Register để tự chèn). Dòng cần thêm:" -ForegroundColor Yellow }
    Write-Host "  ClassicMenu.cs           : $menuLine"
    Write-Host "  ToolPaletteDataReader.cs : $paletteLine"
}

Write-Host ""
Write-Host "Bước tiếp theo:" -ForegroundColor Cyan
Write-Host "  1. Thay các chỗ TODO trong Options / Service / Form.Layout."
Write-Host "  2. Build: powershell -ExecutionPolicy Bypass -NoProfile -File `"$(Join-Path $repoRoot 'BuildProject.ps1')`""

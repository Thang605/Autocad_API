using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

using MyFirstProject.Shared.UI;

namespace MyFirstProject.Commands.CTC.TaoCorridorTheoCauKien
{
    /// <summary>
    /// Form lệnh CTC_Tao_Corridor_Theo_Cau_Kien — Quét FeatureLine theo Site &amp; Cập nhật 1 Corridor tổng.
    /// </summary>
    public partial class TaoCorridorTheoCauKienForm : Form
    {
        private const string SurfaceOptionUseDefault = "(Theo mặc định)";
        private const string SurfaceOptionNone = "(Không gán)";
        private const string StatusNewText = "✨ Mới thêm";
        private const string StatusExistingText = "✓ Đã có (Giữ LT)";

        private readonly Database _db;
        private readonly Editor _ed;
        private bool _suppressGridEvents = false;
        private bool _suppressSelectorEvents = false;
        private int _orphanDeletedBaselinesCount = 0;

        private readonly List<(string Name, ObjectId Id)> _corridorList = new List<(string, ObjectId)>();
        private readonly Dictionary<ObjectId, SiteCorridorRecipe> _corridorRecipes = new Dictionary<ObjectId, SiteCorridorRecipe>();
        private readonly HashSet<ObjectId> _clonedFlTargetIds = new HashSet<ObjectId>();
        private readonly List<(string Name, ObjectId Id)> _assemblyList = new List<(string, ObjectId)>();
        private readonly List<(string Name, ObjectId Id)> _surfaceList = new List<(string, ObjectId)>();
        private readonly List<(string Name, ObjectId Id)> _codeSetStyleList = new List<(string, ObjectId)>();
        private readonly List<SiteCatalogItem> _siteList = new List<SiteCatalogItem>();
        private readonly List<ObjectId> _sourceCorridorsToMerge = new List<ObjectId>();

        public bool FormAccepted { get; private set; } = false;
        public TaoCorridorTheoCauKienOptions Options { get; private set; } = new TaoCorridorTheoCauKienOptions();

        // Controls (khởi tạo trong *.Layout*.cs)
        private ToolTip _toolTip = null!;
        private System.Windows.Forms.ComboBox cmbSite = null!;
        private System.Windows.Forms.Button btnPickSiteFromFl = null!;
        private System.Windows.Forms.Button btnRescanSite = null!;
        private CheckBox chkRemoveDeletedBaselines = null!;

        private System.Windows.Forms.ComboBox cmbExistingCorridor = null!;
        private System.Windows.Forms.TextBox txtCorridorName = null!;
        private System.Windows.Forms.Button btnPickExistingCorridor = null!;
        private System.Windows.Forms.Button btnMergeExistingCorridors = null!;
        private CheckBox chkDeleteMergedSourceCorridors = null!;

        private System.Windows.Forms.ComboBox cmbDefaultAssembly = null!;
        private System.Windows.Forms.ComboBox cmbDefaultSurface = null!;
        private System.Windows.Forms.ComboBox cmbCodeSetStyle = null!;
        private NumericUpDown numFreqTangent = null!;
        private NumericUpDown numFreqCurve = null!;

        private FlowLayoutPanel pnlBatchToolbar = null!;
        private System.Windows.Forms.ComboBox cmbFilterStyleLayer = null!;
        private System.Windows.Forms.Button btnSelectByGroup = null!;
        private System.Windows.Forms.ComboBox cmbBatchAssembly = null!;
        private System.Windows.Forms.Button btnApplyBatchAssembly = null!;
        private System.Windows.Forms.Button btnPickAssemblyForRow = null!;
        private System.Windows.Forms.ComboBox cmbBatchSurface = null!;
        private System.Windows.Forms.Button btnApplyBatchSurface = null!;
        private System.Windows.Forms.Button btnZoomToRow = null!;

        private DataGridView dgvComponents = null!;
        private System.Windows.Forms.Label lblEmptyListNotice = null!;
        private System.Windows.Forms.Button btnPickOffsetTargetsForRow = null!;
        private System.Windows.Forms.Button btnClearOffsetTargetsForRow = null!;
        private System.Windows.Forms.Button btnAddFeatureLineComponent = null!;
        private System.Windows.Forms.Button btnAddAlignmentComponent = null!;
        private System.Windows.Forms.Button btnRemoveSelectedRow = null!;
        private CheckBox chkAutoRebuild = null!;

        private System.Windows.Forms.Label lblSummary = null!;
        private System.Windows.Forms.Button btnExecute = null!;
        private System.Windows.Forms.Button btnCancel = null!;

        public TaoCorridorTheoCauKienForm(Database db, Editor ed)
        {
            _db = db;
            _ed = ed;
            LoadDrawingCatalogs();
            BuildLayout();
            RestoreLastSettings();
            UpdateComponentListVisibility();
            UpdateSummaryLabel();
        }

        private void UpdateComponentListVisibility()
        {
            bool hasRows = dgvComponents.Rows.Count > 0;
            dgvComponents.Visible = hasRows;
            lblEmptyListNotice.Visible = !hasRows;

            pnlBatchToolbar.Visible = hasRows;
            btnPickOffsetTargetsForRow.Visible = hasRows;
            btnClearOffsetTargetsForRow.Visible = hasRows;
            btnRemoveSelectedRow.Visible = hasRows;
        }

        private void AddRowToGrid(CorridorComponentRowItem item)
        {
            if (item.BaselineEntityIds == null || item.BaselineEntityIds.Count == 0)
                return;

            string asmName = item.AssemblyName;
            if (string.IsNullOrEmpty(asmName) || !_assemblyList.Any(a => a.Name.Equals(asmName, StringComparison.OrdinalIgnoreCase)))
            {
                if (cmbDefaultAssembly.SelectedIndex >= 0 && cmbDefaultAssembly.SelectedIndex < _assemblyList.Count)
                {
                    asmName = _assemblyList[cmbDefaultAssembly.SelectedIndex].Name;
                    item.AssemblyId = _assemblyList[cmbDefaultAssembly.SelectedIndex].Id;
                }
                else if (_assemblyList.Count > 0)
                {
                    asmName = _assemblyList[0].Name;
                    item.AssemblyId = _assemblyList[0].Id;
                }
                item.AssemblyName = asmName;
            }
            else
            {
                item.AssemblyId = _assemblyList.First(a => a.Name.Equals(asmName, StringComparison.OrdinalIgnoreCase)).Id;
            }

            string surfName = SurfaceOptionUseDefault;
            if (item.DisableSurfaceTarget)
                surfName = SurfaceOptionNone;
            else if (!item.UseDefaultSurface && !item.TargetSurfaceId.IsNull)
            {
                var foundSurf = _surfaceList.FirstOrDefault(s => s.Id == item.TargetSurfaceId);
                if (!foundSurf.Id.IsNull) surfName = foundSurf.Name;
            }
            item.TargetSurfaceName = surfName;

            string statusText = item.HasExistingCorridorRegion ? StatusExistingText : StatusNewText;
            string styleLayerText = !string.IsNullOrEmpty(item.FeatureLineStyleName)
                ? $"{item.FeatureLineStyleName} | {item.FeatureLineLayer}"
                : (!string.IsNullOrEmpty(item.FeatureLineLayer) ? item.FeatureLineLayer : (item.SourceType == ComponentBaselineSourceType.FeatureLine ? "FeatureLine" : "Alignment"));

            int rowIdx = dgvComponents.Rows.Add(
                item.IsEnabled,
                statusText,
                item.BaselineDisplayText,
                styleLayerText,
                asmName,
                surfName,
                item.OffsetTargetsDisplay,
                item.StartStation.ToString("F2"),
                item.EndStation.ToString("F2"),
                item.FrequencyTangent.ToString("F1"));

            DataGridViewRow gridRow = dgvComponents.Rows[rowIdx];
            gridRow.Tag = item;

            if (item.HasExistingCorridorRegion)
            {
                gridRow.Cells["colStatus"].Style.BackColor = Color.FromArgb(230, 247, 236);
                gridRow.Cells["colStatus"].Style.ForeColor = Color.FromArgb(16, 124, 65);
                LockExistingRegionStationCells(gridRow, item);
            }
            else
            {
                gridRow.Cells["colStatus"].Style.BackColor = Color.FromArgb(255, 248, 225);
                gridRow.Cells["colStatus"].Style.ForeColor = Color.FromArgb(145, 85, 0);
            }
        }

        private static void LockExistingRegionStationCells(DataGridViewRow gridRow, CorridorComponentRowItem item)
        {
            Color lockBg = Color.FromArgb(242, 244, 247);
            Color lockFg = Color.FromArgb(90, 95, 105);
            string tip = string.IsNullOrEmpty(item.ExistingStationSummary)
                ? "Region đã có sẵn trên Corridor — Lý trình được giữ nguyên tuyệt đối khi cập nhật."
                : item.ExistingStationSummary;

            foreach (string colName in new[] { "colStartSta", "colEndSta" })
            {
                DataGridViewCell cell = gridRow.Cells[colName];
                cell.ReadOnly = true;
                cell.Style.BackColor = lockBg;
                cell.Style.ForeColor = lockFg;
                cell.ToolTipText = tip;
            }
            gridRow.Cells["colStatus"].ToolTipText = tip;
        }

        private void DgvComponents_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (_suppressGridEvents || e.RowIndex < 0) return;

            SyncGridRowToModel(e.RowIndex);
            if (dgvComponents.Rows[e.RowIndex].Tag is CorridorComponentRowItem item && item.HasExistingCorridorRegion)
            {
                string col = dgvComponents.Columns[e.ColumnIndex].Name;
                if (col == "colAssembly") item.AssemblyModifiedByUser = true;
                else if (col == "colTargetSurface") item.SurfaceTargetModifiedByUser = true;
                else if (col == "colFreq") item.FrequencyModifiedByUser = true;
            }
            UpdateSummaryLabel();
        }

        private void SyncGridRowToModel(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= dgvComponents.Rows.Count) return;
            var row = dgvComponents.Rows[rowIndex];
            if (row.Tag is not CorridorComponentRowItem item) return;

            item.IsEnabled = Convert.ToBoolean(row.Cells["colEnable"].Value ?? true);

            string asmName = Convert.ToString(row.Cells["colAssembly"].Value) ?? "";
            item.AssemblyName = asmName;
            var foundAsm = _assemblyList.FirstOrDefault(a => a.Name.Equals(asmName, StringComparison.OrdinalIgnoreCase));
            if (!foundAsm.Id.IsNull) item.AssemblyId = foundAsm.Id;

            string surfName = Convert.ToString(row.Cells["colTargetSurface"].Value) ?? SurfaceOptionUseDefault;
            item.TargetSurfaceName = surfName;
            if (surfName == SurfaceOptionUseDefault)
            {
                item.UseDefaultSurface = true;
                item.DisableSurfaceTarget = false;
                item.TargetSurfaceId = ObjectId.Null;
            }
            else if (surfName == SurfaceOptionNone)
            {
                item.UseDefaultSurface = false;
                item.DisableSurfaceTarget = true;
                item.TargetSurfaceId = ObjectId.Null;
            }
            else
            {
                item.UseDefaultSurface = false;
                item.DisableSurfaceTarget = false;
                var foundSurf = _surfaceList.FirstOrDefault(s => s.Name.Equals(surfName, StringComparison.OrdinalIgnoreCase));
                item.TargetSurfaceId = foundSurf.Id;
            }

            if (!item.HasExistingCorridorRegion)
            {
                if (double.TryParse(Convert.ToString(row.Cells["colStartSta"].Value), out double sSta))
                    item.StartStation = Math.Max(0.0, sSta);
                if (double.TryParse(Convert.ToString(row.Cells["colEndSta"].Value), out double eSta))
                    item.EndStation = Math.Max(0.0, eSta);
            }

            if (double.TryParse(Convert.ToString(row.Cells["colFreq"].Value), out double freq) && freq >= 0.5)
                item.FrequencyTangent = freq;
        }

        private void BtnRemoveSelectedRow_Click(object? sender, EventArgs e)
        {
            var targets = GetTargetRowIndices();
            if (targets.Count == 0) return;

            _suppressGridEvents = true;
            try
            {
                foreach (int rIdx in targets.OrderByDescending(x => x))
                    dgvComponents.Rows.RemoveAt(rIdx);
            }
            finally { _suppressGridEvents = false; }

            UpdateComponentListVisibility();
            UpdateSummaryLabel();
        }

        private void UpdateSummaryLabel()
        {
            int newEnabled = 0, existingEnabled = 0, disabledCount = 0;
            for (int i = 0; i < dgvComponents.Rows.Count; i++)
            {
                if (dgvComponents.Rows[i].Tag is not CorridorComponentRowItem item) continue;
                bool enabled = Convert.ToBoolean(dgvComponents.Rows[i].Cells["colEnable"].Value ?? true);
                if (!enabled) disabledCount++;
                else if (item.HasExistingCorridorRegion) existingEnabled++;
                else newEnabled++;
            }

            bool isUpdateMode = cmbExistingCorridor.SelectedIndex > 0;
            btnExecute.Text = isUpdateMode ? "🔄 Cập Nhật Corridor Tổng" : "🏗 Tạo Corridor Tổng";
            btnExecute.BackColor = isUpdateMode ? Color.FromArgb(16, 124, 65) : Color.FromArgb(0, 120, 215);

            string targetName = isUpdateMode ? Convert.ToString(cmbExistingCorridor.SelectedItem) ?? "" : txtCorridorName.Text.Trim();
            if (dgvComponents.Rows.Count == 0 && _orphanDeletedBaselinesCount == 0)
            {
                lblSummary.Text = $"Chưa có danh sách FL — Đích: 1 Corridor tổng '{targetName}'";
                return;
            }

            int removeCount = chkRemoveDeletedBaselines.Checked ? _orphanDeletedBaselinesCount + (isUpdateMode ? disabledCount : 0) : 0;
            lblSummary.Text = $"Corridor '{targetName}': +{newEnabled} FL mới | ={existingEnabled} Region cũ (giữ nguyên LT) | -{removeCount} gỡ bỏ";
        }

        private void BtnExecute_Click(object? sender, EventArgs e)
        {
            SaveCurrentSettings();

            var allRows = new List<CorridorComponentRowItem>();
            int enabledCount = 0;
            for (int i = 0; i < dgvComponents.Rows.Count; i++)
            {
                SyncGridRowToModel(i);
                if (dgvComponents.Rows[i].Tag is CorridorComponentRowItem item &&
                    item.BaselineEntityIds.Count > 0 &&
                    (!item.IsEnabled || !item.AssemblyId.IsNull))
                {
                    allRows.Add(item);
                    if (item.IsEnabled) enabledCount++;
                }
            }

            bool canRemoveStale = cmbExistingCorridor.SelectedIndex > 0 && chkRemoveDeletedBaselines.Checked && _orphanDeletedBaselinesCount > 0;
            if (enabledCount == 0 && !canRemoveStale)
            {
                MessageBox.Show(
                    "Vui lòng chọn Site FeatureLine hoặc ít nhất 1 Alignment / FeatureLine có bật áp mặt cắt.",
                    "Chưa có dữ liệu thực thi",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            SiteCatalogItem? site = GetSelectedSiteItem();
            ObjectId targetCorrId = GetSelectedTargetCorridorId();
            string corrName = targetCorrId.IsNull
                ? (string.IsNullOrWhiteSpace(txtCorridorName.Text) ? "COR_Tong" : txtCorridorName.Text.Trim())
                : _corridorList.FirstOrDefault(c => c.Id == targetCorrId).Name;

            Options = new TaoCorridorTheoCauKienOptions
            {
                ExistingCorridorId = targetCorrId,
                CorridorName = corrName,
                HasSiteSelection = site != null,
                SelectedSiteId = site?.SiteId ?? ObjectId.Null,
                SelectedSiteName = site?.SiteName ?? "",
                IsSiteLessMode = site?.IsSiteLess ?? false,
                DefaultAssemblyId = _lastDefaultAssemblyId,
                SelectedCodeSetStyleId = _lastCodeSetStyleId,
                DefaultTargetSurfaceId = _lastDefaultSurfaceId,
                DefaultFreqTangent = (double)numFreqTangent.Value,
                DefaultFreqCurve = (double)numFreqCurve.Value,
                AutoRebuild = chkAutoRebuild.Checked,
                RemoveDeletedBaselines = chkRemoveDeletedBaselines.Checked,
                DeleteMergedSourceCorridors = chkDeleteMergedSourceCorridors.Checked,
                ComponentRows = allRows,
                SourceCorridorsToMerge = new List<ObjectId>(_sourceCorridorsToMerge)
            };

            FormAccepted = true;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}

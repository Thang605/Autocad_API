using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;

using MyFirstProject.Shared.UI;

using CivSurface = Autodesk.Civil.DatabaseServices.Surface;

namespace MyFirstProject.Commands.CTC.TaoCorridorTheoCauKien
{
    /// <summary>
    /// Form lệnh CTC_Tao_Corridor_Theo_Cau_Kien - phần SỰ KIỆN và LOGIC UI.
    /// </summary>
    public partial class TaoCorridorTheoCauKienForm : Form
    {
        private readonly Database _db;
        private readonly Editor _ed;
        private bool _updatingRadioMode = false;
        private readonly List<ObjectId> _sourceCorridorsToMerge = new List<ObjectId>();

        // Danh mục dữ liệu từ bản vẽ
        private readonly List<(string Name, ObjectId Id)> _corridorList = new List<(string, ObjectId)>();
        private readonly List<(string Name, ObjectId Id)> _assemblyList = new List<(string, ObjectId)>();
        private readonly List<(string Name, ObjectId Id)> _surfaceList = new List<(string, ObjectId)>();
        private readonly List<(string Name, ObjectId Id)> _codeSetStyleList = new List<(string, ObjectId)>();
        private readonly List<(string Name, ObjectId Id)> _alignmentList = new List<(string, ObjectId)>();

        public static readonly string[] ComponentCategories = new[]
        {
            "Lòng đường chính",
            "Dải phân cách giữa",
            "Vỉa hè & Bó vỉa Trái",
            "Vỉa hè & Bó vỉa Phải",
            "Rãnh biên / Tường chắn",
            "Nhánh nút giao rẽ phải",
            "Taluy Đào / Đắp",
            "Cấu kiện tùy chỉnh"
        };

        public static readonly string[] SourceTypeNames = new[]
        {
            "Alignment + Profile",
            "Feature Line"
        };

        public bool FormAccepted { get; private set; } = false;
        public TaoCorridorTheoCauKienOptions Options { get; private set; } = new TaoCorridorTheoCauKienOptions();

        // UI Controls
        private RadioButton radSeparateCorridor = null!;
        private RadioButton radSingleOrExistingCorridor = null!;
        private System.Windows.Forms.ComboBox cmbExistingCorridor = null!;
        private System.Windows.Forms.Button btnPickExistingCorridor = null!;
        private System.Windows.Forms.Button btnMergeExistingCorridors = null!;
        private CheckBox chkDeleteMergedSourceCorridors = null!;
        private System.Windows.Forms.TextBox txtCorridorPrefix = null!;
        private System.Windows.Forms.ComboBox cmbCodeSetStyle = null!;
        private System.Windows.Forms.ComboBox cmbDefaultSurface = null!;
        private NumericUpDown numFreqTangent = null!;
        private NumericUpDown numFreqCurve = null!;
        private CheckBox chkAutoRebuild = null!;

        private DataGridView dgvComponents = null!;
        private System.Windows.Forms.Label lblEmptyListNotice = null!;
        private TableLayoutPanel pnlSubActions = null!;
        private System.Windows.Forms.Button btnAddAlignmentComponent = null!;
        private System.Windows.Forms.Button btnAddFeatureLineComponent = null!;
        private System.Windows.Forms.Button btnRestoreSavedRows = null!;
        private System.Windows.Forms.Button btnPickBaselineForRow = null!;
        private System.Windows.Forms.Button btnPickAssemblyForRow = null!;
        private System.Windows.Forms.Button btnPickOffsetTargetsForRow = null!;
        private System.Windows.Forms.Button btnRemoveSelectedRow = null!;
        private System.Windows.Forms.Button btnClearAllRows = null!;

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

        private void LoadDrawingCatalogs()
        {
            _corridorList.Clear();
            _assemblyList.Clear();
            _surfaceList.Clear();
            _codeSetStyleList.Clear();
            _alignmentList.Clear();

            using (Transaction tr = _db.TransactionManager.StartTransaction())
            {
                CivilDocument cdoc = CivilDocument.GetCivilDocument(_db);

                foreach (ObjectId id in cdoc.CorridorCollection)
                {
                    if (tr.GetObject(id, OpenMode.ForRead) is Corridor corr)
                        _corridorList.Add((corr.Name, id));
                }

                foreach (ObjectId id in cdoc.AssemblyCollection)
                {
                    if (tr.GetObject(id, OpenMode.ForRead) is Assembly asm)
                        _assemblyList.Add((asm.Name, id));
                }

                foreach (ObjectId id in cdoc.GetSurfaceIds())
                {
                    if (tr.GetObject(id, OpenMode.ForRead) is CivSurface surf)
                        _surfaceList.Add((surf.Name, id));
                }

                foreach (ObjectId id in cdoc.Styles.CodeSetStyles)
                {
                    if (tr.GetObject(id, OpenMode.ForRead) is CodeSetStyle css)
                        _codeSetStyleList.Add((css.Name, id));
                }

                foreach (ObjectId id in cdoc.GetAlignmentIds())
                {
                    if (tr.GetObject(id, OpenMode.ForRead) is Alignment al)
                        _alignmentList.Add((al.Name, id));
                }

                tr.Commit();
            }
        }

        private void SetBuildModeUI(CorridorComponentBuildMode mode)
        {
            if (_updatingRadioMode) return;
            _updatingRadioMode = true;
            try
            {
                bool singleMode = mode == CorridorComponentBuildMode.SingleOrExistingCorridor;
                radSingleOrExistingCorridor.Checked = singleMode;
                radSeparateCorridor.Checked = !singleMode;
                radSingleOrExistingCorridor.Font = singleMode ? StandardFonts.Bold : StandardFonts.Regular;
                radSeparateCorridor.Font = !singleMode ? StandardFonts.Bold : StandardFonts.Regular;

                cmbExistingCorridor.Enabled = singleMode;
                btnPickExistingCorridor.Enabled = singleMode;
                btnMergeExistingCorridors.Enabled = singleMode;
                chkDeleteMergedSourceCorridors.Enabled = singleMode;
            }
            finally
            {
                _updatingRadioMode = false;
            }
            UpdateSummaryLabel();
        }

        /// <summary>
        /// Chỉ hiển thị bảng danh sách cấu kiện khi đã chọn ít nhất 1 Alignment hoặc FeatureLine.
        /// </summary>
        private void UpdateComponentListVisibility()
        {
            bool hasComponents = dgvComponents.Rows.Count > 0;
            dgvComponents.Visible = hasComponents;
            lblEmptyListNotice.Visible = !hasComponents;

            btnPickBaselineForRow.Visible = hasComponents;
            btnPickAssemblyForRow.Visible = hasComponents;
            btnRemoveSelectedRow.Visible = hasComponents;
            btnClearAllRows.Visible = hasComponents;
            btnPickOffsetTargetsForRow.Visible = hasComponents;

            int validSavedCount = _savedRows.Count(r =>
                r.BaselineEntityIds != null &&
                r.BaselineEntityIds.Any(id => PersistentState.IsAlive(id, _db)));
            btnRestoreSavedRows.Visible = !hasComponents && validSavedCount > 0;
            if (btnRestoreSavedRows.Visible)
            {
                btnRestoreSavedRows.Text = $"Nạp lại ĐT lần trước ({validSavedCount})";
            }
        }

        private void AddRowToGrid(CorridorComponentRowItem item)
        {
            if (item.BaselineEntityIds == null || item.BaselineEntityIds.Count == 0)
                return;

            string asmName = item.AssemblyName;
            if (string.IsNullOrEmpty(asmName) || !_assemblyList.Any(a => a.Name.Equals(asmName, StringComparison.OrdinalIgnoreCase)))
            {
                asmName = _assemblyList.Count > 0 ? _assemblyList[0].Name : "";
                item.AssemblyName = asmName;
                if (_assemblyList.Count > 0) item.AssemblyId = _assemblyList[0].Id;
            }
            else
            {
                item.AssemblyId = _assemblyList.First(a => a.Name.Equals(asmName, StringComparison.OrdinalIgnoreCase)).Id;
            }

            string category = ComponentCategories.Contains(item.ComponentCategory)
                ? item.ComponentCategory
                : ComponentCategories[0];

            string srcTypeStr = item.SourceType == ComponentBaselineSourceType.FeatureLine
                ? SourceTypeNames[1]
                : SourceTypeNames[0];

            int rowIdx = dgvComponents.Rows.Add(
                item.IsEnabled,
                category,
                item.ComponentName,
                srcTypeStr,
                item.BaselineDisplayText,
                asmName,
                item.StartStation.ToString("F2"),
                item.EndStation.ToString("F2"),
                item.FrequencyTangent.ToString("F1"),
                item.OffsetTargetsDisplay
            );
            dgvComponents.Rows[rowIdx].Tag = item;
        }

        private void SyncGridRowToModel(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= dgvComponents.Rows.Count) return;
            var row = dgvComponents.Rows[rowIndex];
            if (row.Tag is not CorridorComponentRowItem item) return;

            item.IsEnabled = Convert.ToBoolean(row.Cells["colEnable"].Value ?? true);
            item.ComponentCategory = Convert.ToString(row.Cells["colCategory"].Value) ?? ComponentCategories[0];
            item.ComponentName = (Convert.ToString(row.Cells["colName"].Value) ?? "CauKien").Trim();

            string srcStr = Convert.ToString(row.Cells["colSourceType"].Value) ?? SourceTypeNames[0];
            item.SourceType = srcStr == SourceTypeNames[1]
                ? ComponentBaselineSourceType.FeatureLine
                : ComponentBaselineSourceType.AlignmentAndProfile;

            string asmName = Convert.ToString(row.Cells["colAssembly"].Value) ?? "";
            item.AssemblyName = asmName;
            var foundAsm = _assemblyList.FirstOrDefault(a => a.Name.Equals(asmName, StringComparison.OrdinalIgnoreCase));
            if (!foundAsm.Id.IsNull) item.AssemblyId = foundAsm.Id;

            if (double.TryParse(Convert.ToString(row.Cells["colStartSta"].Value), out double sSta))
                item.StartStation = Math.Max(0.0, sSta);
            if (double.TryParse(Convert.ToString(row.Cells["colEndSta"].Value), out double eSta))
                item.EndStation = Math.Max(0.0, eSta);
            if (double.TryParse(Convert.ToString(row.Cells["colFreq"].Value), out double freq) && freq >= 0.5)
                item.FrequencyTangent = freq;
        }

        private void UpdateSummaryLabel()
        {
            int total = dgvComponents.Rows.Count;
            int enabledCount = 0;
            int totalBaselines = 0;

            for (int i = 0; i < total; i++)
            {
                if (dgvComponents.Rows[i].Tag is CorridorComponentRowItem item)
                {
                    bool en = Convert.ToBoolean(dgvComponents.Rows[i].Cells["colEnable"].Value ?? true);
                    if (en)
                    {
                        enabledCount++;
                        totalBaselines += item.BaselineEntityIds.Count;
                    }
                }
            }

            bool isSingleMode = radSingleOrExistingCorridor.Checked;
            string prefix = string.IsNullOrWhiteSpace(txtCorridorPrefix?.Text) ? "COR_" : txtCorridorPrefix.Text.Trim();
            string newMergedName = prefix.EndsWith("_") ? $"{prefix}Gop_CauKien" : prefix;

            string modeText = !isSingleMode
                ? $"Tách riêng {enabledCount} Corridor theo từng cấu kiện"
                : (cmbExistingCorridor.SelectedIndex > 0
                    ? $"Gộp tất cả vào Corridor '{cmbExistingCorridor.SelectedItem}'"
                    : $"Gộp tất cả vào 1 Corridor '{newMergedName}'");

            lblSummary.Text = total == 0
                ? $"Chưa chọn Alignment / FeatureLine — Chế độ: {modeText}"
                : $"Tổng hợp: {enabledCount}/{total} cấu kiện ({totalBaselines} đường dẫn Baseline) — Chế độ: {modeText}";
        }

        private void BtnRemoveSelectedRow_Click(object? sender, EventArgs e)
        {
            if (dgvComponents.CurrentRow == null || dgvComponents.CurrentRow.Index < 0) return;
            dgvComponents.Rows.RemoveAt(dgvComponents.CurrentRow.Index);
            UpdateComponentListVisibility();
            UpdateSummaryLabel();
        }

        private void BtnClearAllRows_Click(object? sender, EventArgs e)
        {
            dgvComponents.Rows.Clear();
            _sourceCorridorsToMerge.Clear();
            UpdateComponentListVisibility();
            UpdateSummaryLabel();
        }

        private void BtnExecute_Click(object? sender, EventArgs e)
        {
            SaveCurrentSettings();

            var currentRows = new List<CorridorComponentRowItem>();
            for (int i = 0; i < dgvComponents.Rows.Count; i++)
            {
                SyncGridRowToModel(i);
                if (dgvComponents.Rows[i].Tag is CorridorComponentRowItem item &&
                    item.IsEnabled &&
                    item.BaselineEntityIds.Count > 0 &&
                    !item.AssemblyId.IsNull)
                {
                    currentRows.Add(item);
                }
            }

            if (currentRows.Count == 0)
            {
                MessageBox.Show(
                    "Vui lòng chọn ít nhất 1 Alignment hoặc Feature Line để hiển thị và lắp ghép cấu kiện.",
                    "Chưa chọn Alignment / FeatureLine",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            Options = new TaoCorridorTheoCauKienOptions
            {
                BuildMode = _lastBuildMode,
                ExistingCorridorId = _lastExistingCorridorId,
                CorridorNamePrefix = string.IsNullOrWhiteSpace(_lastCorridorPrefix) ? "COR_" : _lastCorridorPrefix,
                SelectedCodeSetStyleId = _lastCodeSetStyleId,
                DefaultTargetSurfaceId = _lastDefaultSurfaceId,
                DefaultFreqTangent = (double)numFreqTangent.Value,
                DefaultFreqCurve = (double)numFreqCurve.Value,
                AutoRebuild = chkAutoRebuild.Checked,
                DeleteMergedSourceCorridors = chkDeleteMergedSourceCorridors.Checked,
                ComponentRows = currentRows,
                SourceCorridorsToMerge = new List<ObjectId>(_sourceCorridorsToMerge)
            };

            FormAccepted = true;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace MyFirstProject.Commands.CTC.TaoCorridorTheoCauKien
{
    public partial class TaoCorridorTheoCauKienForm
    {
        private void RefreshStyleLayerFilterComboBox()
        {
            cmbFilterStyleLayer.Items.Clear();
            cmbFilterStyleLayer.Items.Add("-- Chọn theo Style / Layer --");

            var groups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < dgvComponents.Rows.Count; i++)
            {
                if (dgvComponents.Rows[i].Tag is not CorridorComponentRowItem item) continue;
                if (!string.IsNullOrWhiteSpace(item.FeatureLineStyleName))
                    groups.Add($"[Style] {item.FeatureLineStyleName}");
                if (!string.IsNullOrWhiteSpace(item.FeatureLineLayer))
                    groups.Add($"[Layer] {item.FeatureLineLayer}");
            }

            foreach (string g in groups.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                cmbFilterStyleLayer.Items.Add(g);
            cmbFilterStyleLayer.SelectedIndex = 0;
        }

        private void BtnSelectByGroup_Click(object? sender, EventArgs e)
        {
            if (cmbFilterStyleLayer.SelectedIndex <= 0) return;
            string token = Convert.ToString(cmbFilterStyleLayer.SelectedItem) ?? "";
            bool byStyle = token.StartsWith("[Style] ");
            string value = token.Length > 8 ? token.Substring(8) : "";

            dgvComponents.ClearSelection();
            foreach (DataGridViewRow row in dgvComponents.Rows)
            {
                if (row.Tag is not CorridorComponentRowItem item) continue;
                row.Selected = byStyle
                    ? item.FeatureLineStyleName.Equals(value, StringComparison.OrdinalIgnoreCase)
                    : item.FeatureLineLayer.Equals(value, StringComparison.OrdinalIgnoreCase);
            }
        }

        private List<int> GetTargetRowIndices()
        {
            var indices = new List<int>();
            foreach (DataGridViewRow row in dgvComponents.SelectedRows)
                if (row.Index >= 0) indices.Add(row.Index);

            if (indices.Count == 0 && dgvComponents.CurrentRow != null && dgvComponents.CurrentRow.Index >= 0)
                indices.Add(dgvComponents.CurrentRow.Index);

            return indices.OrderBy(x => x).ToList();
        }

        private void BtnApplyBatchAssembly_Click(object? sender, EventArgs e)
        {
            int asmIdx = cmbBatchAssembly.SelectedIndex;
            if (asmIdx < 0 || asmIdx >= _assemblyList.Count) return;

            var targetRows = GetTargetRowIndices();
            if (targetRows.Count == 0)
            {
                MessageBox.Show("Hãy bôi đen ít nhất 1 dòng FeatureLine trong bảng.", "Chưa chọn dòng", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var asm = _assemblyList[asmIdx];
            _suppressGridEvents = true;
            try
            {
                foreach (int rIdx in targetRows)
                {
                    if (dgvComponents.Rows[rIdx].Tag is not CorridorComponentRowItem item) continue;
                    item.AssemblyId = asm.Id;
                    item.AssemblyName = asm.Name;
                    if (item.HasExistingCorridorRegion) item.AssemblyModifiedByUser = true;
                    dgvComponents.Rows[rIdx].Cells["colAssembly"].Value = asm.Name;
                }
            }
            finally { _suppressGridEvents = false; }
            UpdateSummaryLabel();
        }

        private void BtnApplyBatchSurface_Click(object? sender, EventArgs e)
        {
            if (cmbBatchSurface.SelectedIndex < 0) return;
            var targetRows = GetTargetRowIndices();
            if (targetRows.Count == 0) return;

            string surfName = Convert.ToString(cmbBatchSurface.SelectedItem) ?? SurfaceOptionUseDefault;
            _suppressGridEvents = true;
            try
            {
                foreach (int rIdx in targetRows)
                {
                    dgvComponents.Rows[rIdx].Cells["colTargetSurface"].Value = surfName;
                    SyncGridRowToModel(rIdx);
                    if (dgvComponents.Rows[rIdx].Tag is CorridorComponentRowItem item && item.HasExistingCorridorRegion)
                        item.SurfaceTargetModifiedByUser = true;
                }
            }
            finally { _suppressGridEvents = false; }
            UpdateSummaryLabel();
        }
    }
}

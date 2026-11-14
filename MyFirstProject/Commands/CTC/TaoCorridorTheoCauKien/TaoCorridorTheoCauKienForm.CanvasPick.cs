using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.Civil.DatabaseServices;

using MyFirstProject.Shared.Cad;

namespace MyFirstProject.Commands.CTC.TaoCorridorTheoCauKien
{
    public partial class TaoCorridorTheoCauKienForm
    {
        private void BtnPickSiteFromFl_Click(object? sender, EventArgs e)
        {
            using (EditorUserInteraction ui = _ed.StartUserInteraction(this))
            {
                var peo = new PromptEntityOptions("\nChọn 1 FeatureLine mẫu trên bản vẽ để lấy Site của nó: ");
                peo.SetRejectMessage("\nVui lòng chọn đúng đối tượng FeatureLine.");
                peo.AddAllowedClass(typeof(FeatureLine), true);

                PromptEntityResult per = _ed.GetEntity(peo);
                if (per.Status != PromptStatus.OK || per.ObjectId.IsNull) return;

                ObjectId targetSiteId = ObjectId.Null;
                bool isSiteLess = false;
                bool isAutoCorrFl = false;

                using (Transaction tr = _db.TransactionManager.StartTransaction())
                {
                    if (tr.GetObject(per.ObjectId, OpenMode.ForRead) is AutoCorridorFeatureLine)
                    {
                        isAutoCorrFl = true;
                    }
                    else if (tr.GetObject(per.ObjectId, OpenMode.ForRead) is FeatureLine fl)
                    {
                        targetSiteId = fl.SiteId;
                        isSiteLess = targetSiteId.IsNull;
                    }
                    tr.Commit();
                }

                if (isAutoCorrFl)
                {
                    MessageBox.Show(
                        "Đối tượng vừa chọn là FeatureLine phụ thuộc Corridor (AutoCorridorFeatureLine), không thuộc Site nguồn.",
                        "Không lấy được Site",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                int matchIdx = isSiteLess
                    ? _siteList.FindIndex(s => s.IsSiteLess)
                    : _siteList.FindIndex(s => !s.IsSiteLess && s.SiteId == targetSiteId);

                if (matchIdx >= 0)
                {
                    cmbSite.SelectedIndex = matchIdx + 1;
                }
            }
        }

        private void BtnRescanSite_Click(object? sender, EventArgs e)
        {
            string? selectedSiteKey = GetSelectedSiteItem()?.Key;
            ObjectId selectedCorrId = GetSelectedTargetCorridorId();

            _suppressSelectorEvents = true;
            try
            {
                LoadDrawingCatalogs();
                PopulateSiteComboBox();
                PopulateCorridorComboBox();

                if (!selectedCorrId.IsNull)
                {
                    int cIdx = _corridorList.FindIndex(c => c.Id == selectedCorrId);
                    if (cIdx >= 0) cmbExistingCorridor.SelectedIndex = cIdx + 1;
                }

                if (!string.IsNullOrEmpty(selectedSiteKey))
                {
                    int sIdx = _siteList.FindIndex(s => s.Key == selectedSiteKey);
                    if (sIdx >= 0) cmbSite.SelectedIndex = sIdx + 1;
                }
            }
            finally
            {
                _suppressSelectorEvents = false;
            }

            if (GetSelectedSiteItem() != null)
                ScanSiteAndPopulateGrid();
            else if (!GetSelectedTargetCorridorId().IsNull)
                LoadExistingCorridorOnlyToGrid(GetSelectedTargetCorridorId());
            else
            {
                dgvComponents.Rows.Clear();
                UpdateComponentListVisibility();
                UpdateSummaryLabel();
            }
        }

        private void BtnAddAlignmentComponent_Click(object? sender, EventArgs e)
        {
            var pickedIds = PickEntitiesOnCanvas("\nChọn một hoặc nhiều Alignment (Tuyến): ", typeof(Alignment));
            if (pickedIds.Count == 0) return;

            var existingIds = CollectGridBaselineEntityIds();
            var defaultAsm = cmbDefaultAssembly.SelectedIndex >= 0 && cmbDefaultAssembly.SelectedIndex < _assemblyList.Count
                ? _assemblyList[cmbDefaultAssembly.SelectedIndex]
                : _assemblyList.FirstOrDefault();

            using (Transaction tr = _db.TransactionManager.StartTransaction())
            {
                _suppressGridEvents = true;
                try
                {
                    foreach (ObjectId alId in pickedIds)
                    {
                        if (existingIds.Contains(alId)) continue;
                        if (tr.GetObject(alId, OpenMode.ForRead) is not Alignment al) continue;

                        AddRowToGrid(new CorridorComponentRowItem
                        {
                            IsEnabled = true,
                            SourceType = ComponentBaselineSourceType.AlignmentAndProfile,
                            BaselineEntityIds = new List<ObjectId> { alId },
                            BaselineDisplayText = $"[Tuyến] {al.Name} ({al.StartingStation:F1}→{al.EndingStation:F1}m)",
                            FeatureLineLayer = al.Layer,
                            AssemblyId = defaultAsm.Id,
                            AssemblyName = defaultAsm.Name ?? "",
                            FrequencyTangent = (double)numFreqTangent.Value,
                            FrequencyCurve = (double)numFreqCurve.Value
                        });
                    }
                }
                finally { _suppressGridEvents = false; }
                tr.Commit();
            }

            RefreshStyleLayerFilterComboBox();
            UpdateComponentListVisibility();
            UpdateSummaryLabel();
        }

        private void BtnAddFeatureLineComponent_Click(object? sender, EventArgs e)
        {
            var pickedIds = PickEntitiesOnCanvas("\nChọn thêm FeatureLine: ", typeof(FeatureLine));
            if (pickedIds.Count == 0) return;

            var existingIds = CollectGridBaselineEntityIds();
            var defaultAsm = cmbDefaultAssembly.SelectedIndex >= 0 && cmbDefaultAssembly.SelectedIndex < _assemblyList.Count
                ? _assemblyList[cmbDefaultAssembly.SelectedIndex]
                : _assemblyList.FirstOrDefault();

            using (Transaction tr = _db.TransactionManager.StartTransaction())
            {
                _suppressGridEvents = true;
                try
                {
                    foreach (ObjectId flId in pickedIds)
                    {
                        if (existingIds.Contains(flId)) continue;
                        if (tr.GetObject(flId, OpenMode.ForRead) is not FeatureLine fl) continue;

                        string flName = string.IsNullOrWhiteSpace(fl.Name) ? $"FL#{fl.Handle}" : fl.Name;
                        AddRowToGrid(new CorridorComponentRowItem
                        {
                            IsEnabled = true,
                            SourceType = ComponentBaselineSourceType.FeatureLine,
                            BaselineEntityIds = new List<ObjectId> { flId },
                            BaselineDisplayText = $"{flName} (L={fl.Length2D:F2}m)",
                            FeatureLineStyleName = SafeGetStyleName(fl),
                            FeatureLineLayer = string.IsNullOrWhiteSpace(fl.Layer) ? "0" : fl.Layer,
                            AssemblyId = defaultAsm.Id,
                            AssemblyName = defaultAsm.Name ?? "",
                            FrequencyTangent = (double)numFreqTangent.Value,
                            FrequencyCurve = (double)numFreqCurve.Value
                        });
                    }
                }
                finally { _suppressGridEvents = false; }
                tr.Commit();
            }

            RefreshStyleLayerFilterComboBox();
            UpdateComponentListVisibility();
            UpdateSummaryLabel();
        }

        private void BtnPickAssemblyForRow_Click(object? sender, EventArgs e)
        {
            var targetRows = GetTargetRowIndices();
            if (targetRows.Count == 0)
            {
                MessageBox.Show("Hãy chọn ít nhất 1 dòng trong bảng trước.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (EditorUserInteraction ui = _ed.StartUserInteraction(this))
            {
                var peo = new PromptEntityOptions("\nChọn Assembly mẫu trên bản vẽ để gán cho các dòng đã chọn: ");
                peo.SetRejectMessage("\nVui lòng chọn đúng đối tượng Assembly.");
                peo.AddAllowedClass(typeof(Assembly), true);

                PromptEntityResult per = _ed.GetEntity(peo);
                if (per.Status != PromptStatus.OK || per.ObjectId.IsNull) return;

                int foundIdx = _assemblyList.FindIndex(a => a.Id == per.ObjectId);
                if (foundIdx < 0) return;

                var asm = _assemblyList[foundIdx];
                cmbBatchAssembly.SelectedIndex = foundIdx;

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
            }
            UpdateSummaryLabel();
        }

        private void BtnPickOffsetTargetsForRow_Click(object? sender, EventArgs e)
        {
            var targetRows = GetTargetRowIndices();
            if (targetRows.Count == 0)
            {
                MessageBox.Show("Hãy chọn ít nhất 1 dòng trong bảng trước.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (EditorUserInteraction ui = _ed.StartUserInteraction(this))
            {
                var pso = new PromptSelectionOptions
                {
                    MessageForAdding = "\nChọn các Alignment, FeatureLine hoặc Polyline làm Target Mở rộng / Cao độ: "
                };

                PromptSelectionResult psr = _ed.GetSelection(pso);
                if (psr.Status != PromptStatus.OK || psr.Value == null) return;

                var validTargetIds = new List<ObjectId>();
                using (Transaction tr = _db.TransactionManager.StartTransaction())
                {
                    foreach (SelectedObject so in psr.Value)
                    {
                        if (so == null || so.ObjectId.IsNull) continue;
                        var ent = tr.GetObject(so.ObjectId, OpenMode.ForRead);
                        if ((ent is Alignment || ent is FeatureLine || ent is Polyline || ent is Polyline2d || ent is Polyline3d) &&
                            !validTargetIds.Contains(so.ObjectId))
                        {
                            validTargetIds.Add(so.ObjectId);
                        }
                    }
                    tr.Commit();
                }

                string display = validTargetIds.Count > 0 ? $"[{validTargetIds.Count} đối tượng]" : "(Không)";
                _suppressGridEvents = true;
                try
                {
                    foreach (int rIdx in targetRows)
                    {
                        if (dgvComponents.Rows[rIdx].Tag is not CorridorComponentRowItem item) continue;
                        item.OffsetElevationTargetIds = new List<ObjectId>(validTargetIds);
                        item.OffsetTargetsDisplay = display;
                        if (item.HasExistingCorridorRegion) item.OffsetTargetsModifiedByUser = true;
                        dgvComponents.Rows[rIdx].Cells["colTargets"].Value = display;
                    }
                }
                finally { _suppressGridEvents = false; }
            }
        }

        private void BtnClearOffsetTargetsForRow_Click(object? sender, EventArgs e)
        {
            var targetRows = GetTargetRowIndices();
            if (targetRows.Count == 0) return;

            _suppressGridEvents = true;
            try
            {
                foreach (int rIdx in targetRows)
                {
                    if (dgvComponents.Rows[rIdx].Tag is not CorridorComponentRowItem item) continue;
                    item.OffsetElevationTargetIds.Clear();
                    item.OffsetTargetsDisplay = "(Không)";
                    if (item.HasExistingCorridorRegion) item.OffsetTargetsModifiedByUser = true;
                    dgvComponents.Rows[rIdx].Cells["colTargets"].Value = "(Không)";
                }
            }
            finally { _suppressGridEvents = false; }
        }

        private void BtnZoomToRow_Click(object? sender, EventArgs e)
        {
            var ids = new List<ObjectId>();
            foreach (int rIdx in GetTargetRowIndices())
            {
                if (dgvComponents.Rows[rIdx].Tag is CorridorComponentRowItem item)
                    ids.AddRange(item.BaselineEntityIds);
            }
            if (ids.Count == 0) return;

            try
            {
                if (ZoomHelper.ZoomToEntities(_ed, ids, 1.4))
                    _ed.UpdateScreen();
            }
            catch { }
        }

        private HashSet<ObjectId> CollectGridBaselineEntityIds()
        {
            var set = new HashSet<ObjectId>();
            for (int i = 0; i < dgvComponents.Rows.Count; i++)
            {
                if (dgvComponents.Rows[i].Tag is CorridorComponentRowItem item)
                {
                    foreach (ObjectId id in item.BaselineEntityIds)
                        set.Add(id);
                }
            }
            return set;
        }

        private List<ObjectId> PickEntitiesOnCanvas(string promptMessage, Type allowedClass)
        {
            var result = new List<ObjectId>();
            using (EditorUserInteraction ui = _ed.StartUserInteraction(this))
            {
                _ed.WriteMessage(promptMessage);
                var pso = new PromptSelectionOptions { MessageForAdding = promptMessage };

                PromptSelectionResult psr = _ed.GetSelection(pso);
                if (psr.Status == PromptStatus.OK && psr.Value != null)
                {
                    using (Transaction tr = _db.TransactionManager.StartTransaction())
                    {
                        foreach (SelectedObject so in psr.Value)
                        {
                            if (so == null || so.ObjectId.IsNull) continue;
                            var obj = tr.GetObject(so.ObjectId, OpenMode.ForRead);
                            if (obj != null && allowedClass.IsInstanceOfType(obj) && !result.Contains(so.ObjectId))
                                result.Add(so.ObjectId);
                        }
                        tr.Commit();
                    }
                }
            }
            return result;
        }

        private static string DescribeSingleBaselineEntity(Transaction tr, ObjectId id, out string styleName, out string layerName)
        {
            styleName = "";
            layerName = "";
            var obj = tr.GetObject(id, OpenMode.ForRead);
            if (obj is Alignment al)
            {
                layerName = al.Layer;
                return $"[Tuyến] {al.Name} ({al.StartingStation:F1}→{al.EndingStation:F1}m)";
            }
            if (obj is FeatureLine fl)
            {
                styleName = SafeGetStyleName(fl);
                layerName = string.IsNullOrWhiteSpace(fl.Layer) ? "0" : fl.Layer;
                string flName = string.IsNullOrWhiteSpace(fl.Name) ? $"FL#{fl.Handle}" : fl.Name;
                return $"{flName} (L={fl.Length2D:F2}m)";
            }
            return "(Đã chọn)";
        }
    }
}

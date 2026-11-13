using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.Civil.DatabaseServices;

namespace MyFirstProject.Commands.CTC.TaoCorridorTheoCauKien
{
    public partial class TaoCorridorTheoCauKienForm
    {
        private void BtnAddAlignmentComponent_Click(object? sender, EventArgs e)
        {
            var pickedIds = PickEntitiesOnCanvas(
                "\nChọn một hoặc nhiều Alignment (Tuyến) cho cấu kiện: ",
                typeof(Alignment));

            if (pickedIds.Count == 0) return;

            var defaultAsm = _assemblyList.FirstOrDefault();

            using (Transaction tr = _db.TransactionManager.StartTransaction())
            {
                foreach (ObjectId alId in pickedIds)
                {
                    if (tr.GetObject(alId, OpenMode.ForRead) is not Alignment al)
                        continue;

                    int nextIdx = dgvComponents.Rows.Count + 1;
                    string compName = string.IsNullOrWhiteSpace(al.Name)
                        ? $"CauKien_Tuyen_{nextIdx}"
                        : al.Name.Trim();

                    var newItem = new CorridorComponentRowItem
                    {
                        IsEnabled = true,
                        ComponentCategory = al.Name.Contains("Connected", StringComparison.OrdinalIgnoreCase)
                            ? "Nhánh nút giao rẽ phải"
                            : "Lòng đường chính",
                        ComponentName = compName,
                        SourceType = ComponentBaselineSourceType.AlignmentAndProfile,
                        BaselineEntityIds = new List<ObjectId> { alId },
                        BaselineDisplayText = $"[Tuyến] {al.Name} ({al.StartingStation:F1}→{al.EndingStation:F1}m)",
                        AssemblyId = defaultAsm.Id,
                        AssemblyName = defaultAsm.Name ?? "",
                        StartStation = 0.0,
                        EndStation = 0.0,
                        FrequencyTangent = (double)numFreqTangent.Value,
                        FrequencyCurve = (double)numFreqCurve.Value
                    };

                    AddRowToGrid(newItem);
                }
                tr.Commit();
            }

            UpdateComponentListVisibility();
            UpdateSummaryLabel();
        }

        private void BtnAddFeatureLineComponent_Click(object? sender, EventArgs e)
        {
            var pickedIds = PickEntitiesOnCanvas(
                "\nChọn một hoặc nhiều Feature Line cho cấu kiện (VD: Bó lề, Dải phân cách, Vỉa hè, Taluy): ",
                typeof(FeatureLine));

            if (pickedIds.Count == 0) return;

            var defaultAsm = _assemblyList.FirstOrDefault(a =>
                a.Name.Contains("bo le", StringComparison.OrdinalIgnoreCase) ||
                a.Name.Contains("taluy", StringComparison.OrdinalIgnoreCase) ||
                a.Name.Contains("Phân cách", StringComparison.OrdinalIgnoreCase) ||
                a.Name.Contains("via he", StringComparison.OrdinalIgnoreCase));
            if (defaultAsm.Id.IsNull && _assemblyList.Count > 0)
                defaultAsm = _assemblyList[0];

            using (Transaction tr = _db.TransactionManager.StartTransaction())
            {
                foreach (ObjectId flId in pickedIds)
                {
                    if (tr.GetObject(flId, OpenMode.ForRead) is not FeatureLine fl)
                        continue;

                    int nextIdx = dgvComponents.Rows.Count + 1;
                    string flLabel = string.IsNullOrWhiteSpace(fl.Name) ? $"FL#{fl.Handle}" : fl.Name;

                    var newItem = new CorridorComponentRowItem
                    {
                        IsEnabled = true,
                        ComponentCategory = "Vỉa hè & Bó vỉa Phải",
                        ComponentName = $"CauKien_FL_{nextIdx}",
                        SourceType = ComponentBaselineSourceType.FeatureLine,
                        BaselineEntityIds = new List<ObjectId> { flId },
                        BaselineDisplayText = $"[FeatureLine] {flLabel} (L={fl.Length2D:F2}m)",
                        AssemblyId = defaultAsm.Id,
                        AssemblyName = defaultAsm.Name ?? "",
                        StartStation = 0.0,
                        EndStation = 0.0,
                        FrequencyTangent = (double)numFreqTangent.Value,
                        FrequencyCurve = (double)numFreqCurve.Value
                    };

                    AddRowToGrid(newItem);
                }
                tr.Commit();
            }

            UpdateComponentListVisibility();
            UpdateSummaryLabel();
        }

        private void BtnPickBaselineForRow_Click(object? sender, EventArgs e)
        {
            if (dgvComponents.CurrentRow == null || dgvComponents.CurrentRow.Index < 0)
            {
                MessageBox.Show("Vui lòng chọn một dòng cấu kiện trong bảng trước.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int rowIdx = dgvComponents.CurrentRow.Index;
            SyncGridRowToModel(rowIdx);
            if (dgvComponents.Rows[rowIdx].Tag is not CorridorComponentRowItem item) return;

            bool isFl = item.SourceType == ComponentBaselineSourceType.FeatureLine;
            string prompt = isFl
                ? "\nChọn Feature Line làm đường dẫn cho cấu kiện: "
                : "\nChọn Alignment làm đường dẫn cho cấu kiện: ";
            Type targetType = isFl ? typeof(FeatureLine) : typeof(Alignment);

            var ids = PickEntitiesOnCanvas(prompt, targetType);
            if (ids.Count > 0)
            {
                item.BaselineEntityIds = ids;
                item.BaselineDisplayText = DescribePickedEntities(ids, isFl ? "FeatureLine" : "Tuyến");
                dgvComponents.Rows[rowIdx].Cells["colBaseline"].Value = item.BaselineDisplayText;
            }

            UpdateSummaryLabel();
        }

        private void BtnPickAssemblyForRow_Click(object? sender, EventArgs e)
        {
            if (dgvComponents.CurrentRow == null || dgvComponents.CurrentRow.Index < 0)
            {
                MessageBox.Show("Vui lòng chọn một dòng cấu kiện trong bảng trước.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int rowIdx = dgvComponents.CurrentRow.Index;
            SyncGridRowToModel(rowIdx);
            if (dgvComponents.Rows[rowIdx].Tag is not CorridorComponentRowItem item) return;

            using (EditorUserInteraction ui = _ed.StartUserInteraction(this))
            {
                var peo = new PromptEntityOptions("\nChọn Assembly trên bản vẽ cho cấu kiện: ");
                peo.SetRejectMessage("\nVui lòng chọn đúng đối tượng Assembly.");
                peo.AddAllowedClass(typeof(Assembly), true);

                var per = _ed.GetEntity(peo);
                if (per.Status == PromptStatus.OK && !per.ObjectId.IsNull)
                {
                    var found = _assemblyList.FirstOrDefault(a => a.Id == per.ObjectId);
                    if (!found.Id.IsNull)
                    {
                        item.AssemblyId = found.Id;
                        item.AssemblyName = found.Name;
                        dgvComponents.Rows[rowIdx].Cells["colAssembly"].Value = found.Name;
                    }
                }
            }
        }

        private void BtnPickOffsetTargetsForRow_Click(object? sender, EventArgs e)
        {
            if (dgvComponents.CurrentRow == null || dgvComponents.CurrentRow.Index < 0)
            {
                MessageBox.Show("Vui lòng chọn một dòng cấu kiện trong bảng trước.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int rowIdx = dgvComponents.CurrentRow.Index;
            SyncGridRowToModel(rowIdx);
            if (dgvComponents.Rows[rowIdx].Tag is not CorridorComponentRowItem item) return;

            using (EditorUserInteraction ui = _ed.StartUserInteraction(this))
            {
                _ed.WriteMessage("\nChọn các đối tượng Target Mở rộng / Cao độ (Alignment, FeatureLine, Polyline): ");
                var pso = new PromptSelectionOptions
                {
                    MessageForAdding = "\nChọn Alignment, FeatureLine hoặc Polyline làm Target: "
                };

                var psr = _ed.GetSelection(pso);
                if (psr.Status == PromptStatus.OK && psr.Value != null)
                {
                    var validTargetIds = new List<ObjectId>();
                    using (Transaction tr = _db.TransactionManager.StartTransaction())
                    {
                        foreach (SelectedObject so in psr.Value)
                        {
                            if (so == null || so.ObjectId.IsNull) continue;
                            var ent = tr.GetObject(so.ObjectId, OpenMode.ForRead);
                            if (ent is Alignment || ent is FeatureLine || ent is Polyline || ent is Polyline2d || ent is Polyline3d)
                            {
                                validTargetIds.Add(so.ObjectId);
                            }
                        }
                        tr.Commit();
                    }

                    item.OffsetElevationTargetIds = validTargetIds;
                    item.OffsetTargetsDisplay = validTargetIds.Count > 0
                        ? $"[{validTargetIds.Count} đối tượng]"
                        : "(Không)";
                    dgvComponents.Rows[rowIdx].Cells["colTargets"].Value = item.OffsetTargetsDisplay;
                }
            }
        }

        private List<ObjectId> PickEntitiesOnCanvas(string promptMessage, Type allowedClass)
        {
            var result = new List<ObjectId>();
            using (EditorUserInteraction ui = _ed.StartUserInteraction(this))
            {
                _ed.WriteMessage(promptMessage);
                var pso = new PromptSelectionOptions { MessageForAdding = promptMessage };

                var psr = _ed.GetSelection(pso);
                if (psr.Status == PromptStatus.OK && psr.Value != null)
                {
                    using (Transaction tr = _db.TransactionManager.StartTransaction())
                    {
                        foreach (SelectedObject so in psr.Value)
                        {
                            if (so == null || so.ObjectId.IsNull) continue;
                            var obj = tr.GetObject(so.ObjectId, OpenMode.ForRead);
                            if (obj != null && allowedClass.IsInstanceOfType(obj) && !result.Contains(so.ObjectId))
                            {
                                result.Add(so.ObjectId);
                            }
                        }
                        tr.Commit();
                    }
                }
            }
            return result;
        }

        private string DescribeSingleBaselineEntity(Transaction tr, ObjectId id)
        {
            var obj = tr.GetObject(id, OpenMode.ForRead);
            if (obj is Alignment al)
                return $"[Tuyến] {al.Name} ({al.StartingStation:F1}→{al.EndingStation:F1}m)";
            if (obj is FeatureLine fl)
            {
                string flName = string.IsNullOrWhiteSpace(fl.Name) ? $"FL#{fl.Handle}" : fl.Name;
                return $"[FeatureLine] {flName} (L={fl.Length2D:F2}m)";
            }
            return "(Đã chọn)";
        }

        private string DescribePickedEntities(List<ObjectId> ids, string typeLabel)
        {
            if (ids == null || ids.Count == 0) return "(Chưa chọn)";
            using (Transaction tr = _db.TransactionManager.StartTransaction())
            {
                var names = new List<string>();
                foreach (ObjectId id in ids.Take(2))
                {
                    var obj = tr.GetObject(id, OpenMode.ForRead);
                    if (obj is Alignment al) names.Add(al.Name);
                    else if (obj is FeatureLine fl) names.Add(string.IsNullOrWhiteSpace(fl.Name) ? $"FL#{fl.Handle}" : fl.Name);
                }
                tr.Commit();
                string joined = string.Join(", ", names);
                return ids.Count > 2
                    ? $"[{ids.Count} {typeLabel}] {joined}..."
                    : $"[{ids.Count} {typeLabel}] {joined}";
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.Civil.DatabaseServices;

namespace MyFirstProject.Commands.CTC.TaoCorridorTheoCauKien
{
    public partial class TaoCorridorTheoCauKienForm
    {
        private void BtnPickExistingCorridor_Click(object? sender, EventArgs e)
        {
            using (EditorUserInteraction ui = _ed.StartUserInteraction(this))
            {
                var peo = new PromptEntityOptions("\nChọn Corridor đích hiện có trên bản vẽ để gộp vào: ");
                peo.SetRejectMessage("\nĐối tượng được chọn phải là Corridor.");
                peo.AddAllowedClass(typeof(Corridor), true);

                PromptEntityResult per = _ed.GetEntity(peo);
                if (per.Status == PromptStatus.OK && !per.ObjectId.IsNull)
                {
                    int idx = _corridorList.FindIndex(c => c.Id == per.ObjectId);
                    if (idx >= 0)
                    {
                        cmbExistingCorridor.SelectedIndex = idx + 1;
                        SetBuildModeUI(CorridorComponentBuildMode.SingleOrExistingCorridor);
                    }
                }
            }
        }

        private void BtnMergeExistingCorridors_Click(object? sender, EventArgs e)
        {
            var pickedCorridorIds = PickEntitiesOnCanvas(
                "\nChọn các Corridor trên bản vẽ cần gộp chung: ",
                typeof(Corridor));

            if (pickedCorridorIds.Count == 0) return;

            SetBuildModeUI(CorridorComponentBuildMode.SingleOrExistingCorridor);

            // Nếu chưa chọn Corridor đích trong ComboBox thì lấy Corridor đầu tiên được chọn làm Corridor đích
            if (cmbExistingCorridor.SelectedIndex == 0 && pickedCorridorIds.Count > 0)
            {
                int firstIdx = _corridorList.FindIndex(c => c.Id == pickedCorridorIds[0]);
                if (firstIdx >= 0)
                    cmbExistingCorridor.SelectedIndex = firstIdx + 1;
            }

            ObjectId targetCorrId = cmbExistingCorridor.SelectedIndex > 0 && cmbExistingCorridor.SelectedIndex - 1 < _corridorList.Count
                ? _corridorList[cmbExistingCorridor.SelectedIndex - 1].Id
                : ObjectId.Null;

            using (Transaction tr = _db.TransactionManager.StartTransaction())
            {
                foreach (ObjectId corrId in pickedCorridorIds)
                {
                    if (corrId == targetCorrId)
                        continue;

                    if (tr.GetObject(corrId, OpenMode.ForRead) is not Corridor corr)
                        continue;

                    if (!_sourceCorridorsToMerge.Contains(corrId))
                        _sourceCorridorsToMerge.Add(corrId);

                    AppendCorridorRegionsToGrid(tr, corr, corrId);
                }
                tr.Commit();
            }

            UpdateComponentListVisibility();
            UpdateSummaryLabel();
        }

        private void AppendCorridorRegionsToGrid(Transaction tr, Corridor corr, ObjectId corrId)
        {
            foreach (Baseline bl in corr.Baselines)
            {
                bool isFl = bl.IsFeatureLineBased();
                ObjectId baseEntId = isFl ? bl.FeatureLineId : bl.AlignmentId;
                if (baseEntId.IsNull || !baseEntId.IsValid || baseEntId.IsErased)
                    continue;

                string entDisplay = DescribeSingleBaselineEntity(tr, baseEntId);

                foreach (BaselineRegion rg in bl.BaselineRegions)
                {
                    var asmTuple = _assemblyList.FirstOrDefault(a => a.Id == rg.AssemblyId);
                    string asmName = !asmTuple.Id.IsNull ? asmTuple.Name : (_assemblyList.FirstOrDefault().Name ?? "");

                    double freqT = (double)numFreqTangent.Value;
                    double freqC = (double)numFreqCurve.Value;
                    try
                    {
                        if (rg.AppliedAssemblySetting != null)
                        {
                            freqT = rg.AppliedAssemblySetting.FrequencyAlongTangents;
                            freqC = rg.AppliedAssemblySetting.FrequencyAlongCurves;
                        }
                    }
                    catch { }

                    ExtractRegionTargets(rg, out ObjectId surfTargetId, out List<ObjectId> offsetTargetIds);

                    var rowItem = new CorridorComponentRowItem
                    {
                        IsEnabled = true,
                        ComponentCategory = isFl ? "Taluy Đào / Đắp" : "Lòng đường chính",
                        ComponentName = $"{corr.Name}",
                        SourceType = isFl ? ComponentBaselineSourceType.FeatureLine : ComponentBaselineSourceType.AlignmentAndProfile,
                        BaselineEntityIds = new List<ObjectId> { baseEntId },
                        BaselineDisplayText = entDisplay,
                        ExplicitProfileId = isFl ? ObjectId.Null : bl.ProfileId,
                        AssemblyId = rg.AssemblyId,
                        AssemblyName = asmName,
                        StartStation = rg.StartStation,
                        EndStation = rg.EndStation,
                        FrequencyTangent = freqT,
                        FrequencyCurve = freqC,
                        TargetSurfaceId = surfTargetId,
                        OffsetElevationTargetIds = offsetTargetIds,
                        OffsetTargetsDisplay = offsetTargetIds.Count > 0 ? $"[{offsetTargetIds.Count} đối tượng]" : "(Không)",
                        SourceCorridorId = corrId
                    };

                    AddRowToGrid(rowItem);
                }
            }
        }

        private static void ExtractRegionTargets(BaselineRegion rg, out ObjectId surfTargetId, out List<ObjectId> offsetTargetIds)
        {
            surfTargetId = ObjectId.Null;
            offsetTargetIds = new List<ObjectId>();
            try
            {
                var targets = rg.GetTargets();
                if (targets == null) return;
                foreach (SubassemblyTargetInfo ti in targets)
                {
                    if (ti.TargetIds == null || ti.TargetIds.Count == 0) continue;
                    string tType = ti.TargetType.ToString();
                    if (tType.Contains("Surface", StringComparison.OrdinalIgnoreCase))
                    {
                        if (surfTargetId.IsNull && !ti.TargetIds[0].IsNull)
                            surfTargetId = ti.TargetIds[0];
                    }
                    else
                    {
                        foreach (ObjectId id in ti.TargetIds)
                        {
                            if (!id.IsNull && !offsetTargetIds.Contains(id))
                                offsetTargetIds.Add(id);
                        }
                    }
                }
            }
            catch { }
        }
    }
}

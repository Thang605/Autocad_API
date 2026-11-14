using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;

using CivSurface = Autodesk.Civil.DatabaseServices.Surface;

namespace MyFirstProject.Commands.CTC.TaoCorridorTheoCauKien
{
    internal sealed class SiteCatalogItem
    {
        public string Key { get; set; } = "";
        public string DisplayText { get; set; } = "";
        public ObjectId SiteId { get; set; } = ObjectId.Null;
        public string SiteName { get; set; } = "";
        public bool IsSiteLess { get; set; } = false;
        public List<ObjectId> FeatureLineIds { get; set; } = new List<ObjectId>();
    }

    public partial class TaoCorridorTheoCauKienForm
    {
        private void LoadDrawingCatalogs()
        {
            _corridorList.Clear();
            _corridorRecipes.Clear();
            _clonedFlTargetIds.Clear();
            _assemblyList.Clear();
            _surfaceList.Clear();
            _codeSetStyleList.Clear();
            _siteList.Clear();

            using (Transaction tr = _db.TransactionManager.StartTransaction())
            {
                CivilDocument cdoc = CivilDocument.GetCivilDocument(_db);
                LoadCorridorsAndRecipes(tr, cdoc);

                foreach (ObjectId id in cdoc.AssemblyCollection)
                    if (tr.GetObject(id, OpenMode.ForRead) is Assembly asm) _assemblyList.Add((asm.Name, id));

                foreach (ObjectId id in cdoc.GetSurfaceIds())
                    if (tr.GetObject(id, OpenMode.ForRead) is CivSurface surf) _surfaceList.Add((surf.Name, id));

                foreach (ObjectId id in cdoc.Styles.CodeSetStyles)
                    if (tr.GetObject(id, OpenMode.ForRead) is CodeSetStyle css) _codeSetStyleList.Add((css.Name, id));

                ScanSitesAndFeatureLines(tr, cdoc);
                tr.Commit();
            }
        }

        private void LoadCorridorsAndRecipes(Transaction tr, CivilDocument cdoc)
        {
            foreach (ObjectId id in cdoc.CorridorCollection)
            {
                if (tr.GetObject(id, OpenMode.ForRead) is not Corridor corr) continue;
                _corridorList.Add((corr.Name, id));

                if (TaoCorridorTheoCauKienService.TryReadCorridorRecipe(corr, tr, out SiteCorridorRecipe? recipe) && recipe != null)
                {
                    _corridorRecipes[id] = recipe;
                    foreach (var kv in recipe.ClonedFeatureLineMap)
                    {
                        if (TaoCorridorTheoCauKienService.TryResolveHandle(_db, kv.Value, out ObjectId clnId))
                            _clonedFlTargetIds.Add(clnId);
                    }
                }
            }
        }

        private void ScanSitesAndFeatureLines(Transaction tr, CivilDocument cdoc)
        {
            var bySiteId = new Dictionary<ObjectId, SiteCatalogItem>();
            foreach (ObjectId siteId in cdoc.GetSiteIds())
            {
                if (tr.GetObject(siteId, OpenMode.ForRead) is not Site site) continue;
                bySiteId[siteId] = new SiteCatalogItem
                {
                    Key = siteId.Handle.Value.ToString("X"),
                    SiteId = siteId,
                    SiteName = site.Name,
                    IsSiteLess = false
                };
            }

            var siteLessItem = new SiteCatalogItem
            {
                Key = "<NONE>",
                SiteId = ObjectId.Null,
                SiteName = "<None> — Không thuộc Site",
                IsSiteLess = true
            };

            if (tr.GetObject(_db.BlockTableId, OpenMode.ForRead) is BlockTable bt &&
                tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) is BlockTableRecord ms)
            {
                foreach (ObjectId entId in ms)
                {
                    if (entId.IsNull || !entId.IsValid || entId.IsErased || _clonedFlTargetIds.Contains(entId))
                        continue;

                    string dxf = entId.ObjectClass?.DxfName ?? "";
                    if (dxf != "AECC_FEATURE_LINE") continue;

                    if (tr.GetObject(entId, OpenMode.ForRead) is not FeatureLine fl || fl is AutoCorridorFeatureLine)
                        continue;

                    if (!fl.SiteId.IsNull && bySiteId.TryGetValue(fl.SiteId, out SiteCatalogItem? item))
                        item.FeatureLineIds.Add(entId);
                    else
                        siteLessItem.FeatureLineIds.Add(entId);
                }
            }

            foreach (var s in bySiteId.Values.OrderBy(x => x.SiteName, StringComparer.OrdinalIgnoreCase))
            {
                s.DisplayText = $"{s.SiteName} ({s.FeatureLineIds.Count} FL)";
                _siteList.Add(s);
            }

            if (siteLessItem.FeatureLineIds.Count > 0)
            {
                siteLessItem.DisplayText = $"{siteLessItem.SiteName} ({siteLessItem.FeatureLineIds.Count} FL)";
                _siteList.Add(siteLessItem);
            }
        }

        private void PopulateSiteComboBox()
        {
            cmbSite.Items.Clear();
            cmbSite.Items.Add("-- Chọn Site nguồn để quét FeatureLine --");
            foreach (var s in _siteList)
                cmbSite.Items.Add(s.DisplayText);
            cmbSite.SelectedIndex = 0;
        }

        private void PopulateCorridorComboBox()
        {
            cmbExistingCorridor.Items.Clear();
            cmbExistingCorridor.Items.Add("(Tạo 1 Corridor tổng mới)");
            foreach (var c in _corridorList)
            {
                string label = _corridorRecipes.TryGetValue(c.Id, out SiteCorridorRecipe? rec) && rec.HasSiteBinding
                    ? $"{c.Name} [Site: {rec.SiteName}]"
                    : c.Name;
                cmbExistingCorridor.Items.Add(label);
            }
            cmbExistingCorridor.SelectedIndex = 0;
        }

        private void OnTargetCorridorSelectionChanged()
        {
            if (_suppressSelectorEvents) return;

            ObjectId corrId = GetSelectedTargetCorridorId();
            bool hasExisting = !corrId.IsNull;
            txtCorridorName.ReadOnly = hasExisting;

            if (hasExisting)
            {
                var corrTuple = _corridorList.FirstOrDefault(c => c.Id == corrId);
                if (!string.IsNullOrEmpty(corrTuple.Name))
                    txtCorridorName.Text = corrTuple.Name;

                if (_corridorRecipes.TryGetValue(corrId, out SiteCorridorRecipe? recipe))
                {
                    ApplyRecipeDefaultsToForm(recipe);
                    if (recipe.HasSiteBinding && SelectSiteByRecipe(recipe))
                    {
                        ScanSiteAndPopulateGrid();
                        return;
                    }
                }

                if (GetSelectedSiteItem() != null)
                    ScanSiteAndPopulateGrid();
                else
                    LoadExistingCorridorOnlyToGrid(corrId);
            }
            else if (GetSelectedSiteItem() != null)
            {
                SuggestNewCorridorNameForSite(GetSelectedSiteItem()!);
                ScanSiteAndPopulateGrid();
            }
            else
            {
                dgvComponents.Rows.Clear();
                _orphanDeletedBaselinesCount = 0;
                UpdateComponentListVisibility();
                UpdateSummaryLabel();
            }
        }

        private void OnSiteSelectionChanged()
        {
            if (_suppressSelectorEvents) return;

            SiteCatalogItem? site = GetSelectedSiteItem();
            if (site == null)
            {
                dgvComponents.Rows.Clear();
                _orphanDeletedBaselinesCount = 0;
                ObjectId corrId = GetSelectedTargetCorridorId();
                if (!corrId.IsNull)
                    LoadExistingCorridorOnlyToGrid(corrId);
                else
                {
                    UpdateComponentListVisibility();
                    UpdateSummaryLabel();
                }
                return;
            }

            if (cmbExistingCorridor.SelectedIndex == 0)
                SuggestNewCorridorNameForSite(site);

            ScanSiteAndPopulateGrid();
        }

        private void ScanSiteAndPopulateGrid()
        {
            SiteCatalogItem? site = GetSelectedSiteItem();
            if (site == null) return;

            ObjectId targetCorrId = GetSelectedTargetCorridorId();
            _corridorRecipes.TryGetValue(targetCorrId, out SiteCorridorRecipe? recipe);

            var newPending = new List<(CorridorComponentRowItem Item, string FlName, string FlHandle, string Style, string Layer)>();
            var existingRows = new List<CorridorComponentRowItem>();
            var learnedRules = BuildLearnedRulesLookup(recipe);
            var perFlMap = BuildPerFlLookup(recipe);

            using (Transaction tr = _db.TransactionManager.StartTransaction())
            {
                Corridor? targetCorr = !targetCorrId.IsNull ? tr.GetObject(targetCorrId, OpenMode.ForRead) as Corridor : null;
                var flBaselines = targetCorr != null
                    ? BuildCorridorFlBaselineLookup(targetCorr, recipe)
                    : new Dictionary<ObjectId, List<Baseline>>();

                _orphanDeletedBaselinesCount = targetCorr != null
                    ? CountOrphanDeletedBaselines(tr, targetCorr, site, recipe)
                    : 0;

                foreach (ObjectId flId in site.FeatureLineIds)
                {
                    if (tr.GetObject(flId, OpenMode.ForRead) is not FeatureLine fl) continue;

                    string flName = string.IsNullOrWhiteSpace(fl.Name) ? $"FL#{fl.Handle}" : fl.Name;
                    string styleName = SafeGetStyleName(fl);
                    string layerName = string.IsNullOrWhiteSpace(fl.Layer) ? "0" : fl.Layer;
                    string flHandle = TaoCorridorTheoCauKienService.GetHandleHex(flId);

                    var item = new CorridorComponentRowItem
                    {
                        IsEnabled = true,
                        SourceType = ComponentBaselineSourceType.FeatureLine,
                        BaselineEntityIds = new List<ObjectId> { flId },
                        BaselineDisplayText = $"{flName} (L={fl.Length2D:F2}m)",
                        FeatureLineStyleName = styleName,
                        FeatureLineLayer = layerName,
                        FrequencyTangent = (double)numFreqTangent.Value,
                        FrequencyCurve = (double)numFreqCurve.Value
                    };

                    if (flBaselines.TryGetValue(flId, out var matchedBls) && matchedBls.Count > 0)
                    {
                        PopulateRowFromExistingBaselines(tr, item, matchedBls);
                        existingRows.Add(item);
                    }
                    else
                    {
                        newPending.Add((item, flName, flHandle, styleName, layerName));
                    }
                }
                tr.Commit();
            }

            SeedRulesFromExistingRows(learnedRules, existingRows);
            foreach (var p in newPending)
                ApplySuggestedConfigForNewFl(p.Item, p.FlName, p.FlHandle, p.Style, p.Layer, perFlMap, learnedRules);

            _suppressGridEvents = true;
            try
            {
                dgvComponents.Rows.Clear();
                foreach (var p in newPending) AddRowToGrid(p.Item);
                foreach (var r in existingRows) AddRowToGrid(r);
            }
            finally
            {
                _suppressGridEvents = false;
            }

            RefreshStyleLayerFilterComboBox();
            UpdateComponentListVisibility();
            UpdateSummaryLabel();
        }

        private void LoadExistingCorridorOnlyToGrid(ObjectId corrId)
        {
            if (corrId.IsNull) return;
            _orphanDeletedBaselinesCount = 0;

            _suppressGridEvents = true;
            try
            {
                dgvComponents.Rows.Clear();
                using (Transaction tr = _db.TransactionManager.StartTransaction())
                {
                    if (tr.GetObject(corrId, OpenMode.ForRead) is Corridor corr)
                    {
                        foreach (Baseline bl in corr.Baselines)
                        {
                            if (bl.BaselineRegions.Count == 0) continue;
                            bool isFl = bl.IsFeatureLineBased();
                            ObjectId baseId = isFl ? bl.FeatureLineId : bl.AlignmentId;
                            if (baseId.IsNull || !baseId.IsValid || baseId.IsErased) continue;

                            var item = new CorridorComponentRowItem
                            {
                                IsEnabled = true,
                                SourceType = isFl ? ComponentBaselineSourceType.FeatureLine : ComponentBaselineSourceType.AlignmentAndProfile,
                                BaselineEntityIds = new List<ObjectId> { baseId },
                                BaselineDisplayText = DescribeSingleBaselineEntity(tr, baseId, out string styleName, out string layerName),
                                FeatureLineStyleName = styleName,
                                FeatureLineLayer = layerName,
                                ExplicitProfileId = isFl ? ObjectId.Null : bl.ProfileId
                            };
                            PopulateRowFromExistingBaselines(tr, item, new List<Baseline> { bl });
                            AddRowToGrid(item);
                        }
                    }
                    tr.Commit();
                }
            }
            finally
            {
                _suppressGridEvents = false;
            }

            RefreshStyleLayerFilterComboBox();
            UpdateComponentListVisibility();
            UpdateSummaryLabel();
        }
    }
}

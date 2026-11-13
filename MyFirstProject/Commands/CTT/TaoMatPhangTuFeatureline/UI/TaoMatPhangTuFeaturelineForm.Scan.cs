using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using CadEntity = Autodesk.AutoCAD.DatabaseServices.Entity;
using CivSurface = Autodesk.Civil.DatabaseServices.TinSurface;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsButton = System.Windows.Forms.Button;
using WinFormsTextBox = System.Windows.Forms.TextBox;
using WinFormsComboBox = System.Windows.Forms.ComboBox;
using FlowDirection = System.Windows.Forms.FlowDirection;
using Font = System.Drawing.Font;
using WinFormsColor = System.Drawing.Color;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace MyFirstProject.Civil_Tool_2
{
    public partial class TaoMatPhangTuFeaturelineForm
    {
        private void ScanDrawingSourcesAndStyles()
        {
            cboMasterStyles.Items.Clear();
            cboSubSurfaceStyle.Items.Clear();
            cboExistingSurfaces.Items.Clear();
            _existingSurfaceMap.Clear();
            _surfaceOriginationMap.Clear();
            _surfaceTriCountMap.Clear();
            _bySite.Clear();
            _byLayer.Clear();
            _byCorridor.Clear();
            _allFeatureLineIdsInDrawing.Clear();

            var primarySurfaceNames = new List<string>();
            var auxiliarySurfaceNames = new List<string>();

            try
            {
                CivilDocument civDoc = CivilApplication.ActiveDocument;
                using (Transaction tr = _db.TransactionManager.StartTransaction())
                {
                    var styleNameById = new Dictionary<ObjectId, string>();
                    if (civDoc != null)
                    {
                        // 1. Nạp Surface Styles cho cả mặt phẳng chính và mặt phẳng phụ
                        foreach (ObjectId styleId in civDoc.Styles.SurfaceStyles)
                        {
                            if (tr.GetObject(styleId, OpenMode.ForRead) is SurfaceStyle style &&
                                !string.IsNullOrWhiteSpace(style.Name))
                            {
                                styleNameById[styleId] = style.Name;
                                cboMasterStyles.Items.Add(style.Name);
                                cboSubSurfaceStyle.Items.Add(style.Name);
                            }
                        }

                        // 2. Nạp danh sách TIN Surfaces hiện có kèm nguồn gốc & số lượng tam giác
                        foreach (ObjectId surfId in civDoc.GetSurfaceIds())
                        {
                            if (surfId.IsNull || !surfId.IsValid || surfId.IsErased) continue;
                            if (tr.GetObject(surfId, OpenMode.ForRead) is CivSurface surf &&
                                !string.IsNullOrWhiteSpace(surf.Name))
                            {
                                _existingSurfaceMap[surf.Name] = surfId;
                                try { _surfaceOriginationMap[surf.Name] = surf.OriginationType; }
                                catch { _surfaceOriginationMap[surf.Name] = SurfaceOriginationType.None; }
                                try { _surfaceTriCountMap[surf.Name] = surf.GetTriangles(false).Count; }
                                catch { _surfaceTriCountMap[surf.Name] = 0; }

                                string surfStyleName = "";
                                try
                                {
                                    if (!surf.StyleId.IsNull && styleNameById.TryGetValue(surf.StyleId, out string? sn))
                                        surfStyleName = sn;
                                }
                                catch { }

                                if (LooksLikeAuxiliarySubSurface(surf.Name, surfStyleName))
                                    auxiliarySurfaceNames.Add(surf.Name);
                                else
                                    primarySurfaceNames.Add(surf.Name);
                            }
                        }
                    }

                    // 3. Quét ModelSpace để lập chỉ mục Site / Layer / Corridor cho bộ lọc nạp nhanh
                    Dictionary<ObjectId, string> siteNameLookup = new Dictionary<ObjectId, string>();
                    if (civDoc != null)
                    {
                        foreach (ObjectId siteId in civDoc.GetSiteIds())
                        {
                            if (tr.GetObject(siteId, OpenMode.ForRead) is Site site && !string.IsNullOrWhiteSpace(site.Name))
                            {
                                siteNameLookup[siteId] = site.Name;
                            }
                        }
                    }

                    if (tr.GetObject(_db.BlockTableId, OpenMode.ForRead) is BlockTable bt &&
                        tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) is BlockTableRecord ms)
                    {
                        foreach (ObjectId entId in ms)
                        {
                            if (entId.IsNull || !entId.IsValid || entId.IsErased) continue;
                            string dxfName = entId.ObjectClass?.DxfName ?? "";

                            bool isFl = dxfName == "AECC_FEATURE_LINE" || dxfName == "AECC_AUTO_CORRIDOR_FEATURE_LINE";
                            bool isPoly = dxfName == "LWPOLYLINE" || dxfName == "POLYLINE";
                            if (!isFl && !isPoly) continue;

                            if (tr.GetObject(entId, OpenMode.ForRead) is not CadEntity ent) continue;

                            string layerName = string.IsNullOrWhiteSpace(ent.Layer) ? "0" : ent.Layer;
                            if (!_byLayer.TryGetValue(layerName, out var layerList))
                            {
                                layerList = new List<ObjectId>();
                                _byLayer[layerName] = layerList;
                            }
                            layerList.Add(entId);

                            if (ent is AutoCorridorFeatureLine acfl)
                            {
                                _allFeatureLineIdsInDrawing.Add(entId);
                                string corrName = "Corridor";
                                try
                                {
                                    if (!string.IsNullOrWhiteSpace(acfl.CorridorName))
                                        corrName = acfl.CorridorName;
                                }
                                catch { }

                                if (!_byCorridor.TryGetValue(corrName, out var corrList))
                                {
                                    corrList = new List<ObjectId>();
                                    _byCorridor[corrName] = corrList;
                                }
                                corrList.Add(entId);
                            }
                            else if (ent is FeatureLine fl)
                            {
                                _allFeatureLineIdsInDrawing.Add(entId);
                                string siteLabel = "<None> (Không thuộc Site)";
                                try
                                {
                                    if (!fl.SiteId.IsNull && siteNameLookup.TryGetValue(fl.SiteId, out string? sName))
                                    {
                                        siteLabel = sName;
                                    }
                                }
                                catch { }

                                if (!_bySite.TryGetValue(siteLabel, out var siteList))
                                {
                                    siteList = new List<ObjectId>();
                                    _bySite[siteLabel] = siteList;
                                }
                                siteList.Add(entId);
                            }
                        }
                    }

                    tr.Commit();
                }
            }
            catch { }

            // Đưa các mặt phẳng gốc lên đầu danh sách chọn mặt phẳng đích, các mặt phẳng phụ xuống cuối
            foreach (string sName in primarySurfaceNames)
            {
                cboExistingSurfaces.Items.Add(sName);
            }
            foreach (string subName in auxiliarySurfaceNames)
            {
                cboExistingSurfaces.Items.Add(subName);
            }

            if (cboMasterStyles.Items.Count == 0)
            {
                cboMasterStyles.Items.Add("Standard");
            }
            cboMasterStyles.SelectedIndex = 0;

            if (cboSubSurfaceStyle.Items.Count == 0)
            {
                cboSubSurfaceStyle.Items.Add("_No Display");
            }
            SelectPreferredSubSurfaceStyle(_lastSubSurfaceStyle);

            if (cboExistingSurfaces.Items.Count > 0)
            {
                cboExistingSurfaces.SelectedIndex = 0;
            }
            else
            {
                radExistingSurface.Enabled = false;
                radNewSurface.Checked = true;
            }
        }

        private void SelectPreferredSubSurfaceStyle(string preferredStyle)
        {
            string[] priorities = !string.IsNullOrWhiteSpace(preferredStyle)
                ? new[] { preferredStyle, "_No Display", "No Display", "_Khong Hien Thi", "Border Only" }
                : new[] { "_No Display", "No Display", "_Khong Hien Thi", "Border Only" };

            foreach (string p in priorities)
            {
                for (int i = 0; i < cboSubSurfaceStyle.Items.Count; i++)
                {
                    string itemText = cboSubSurfaceStyle.Items[i]?.ToString() ?? "";
                    if (string.Equals(itemText, p, StringComparison.OrdinalIgnoreCase))
                    {
                        cboSubSurfaceStyle.SelectedIndex = i;
                        return;
                    }
                }
            }

            if (cboSubSurfaceStyle.Items.Count > 0)
            {
                cboSubSurfaceStyle.SelectedIndex = 0;
            }
        }

        private void PopulateFilterValuesDropdown()
        {
            cboFilterValue.Items.Clear();
            int mode = cboFilterSource.SelectedIndex;

            if (mode == 0)
            {
                cboFilterValue.Items.Add($"Tất cả Feature Line ({_allFeatureLineIdsInDrawing.Count} đường)");
                cboFilterValue.Enabled = false;
            }
            else if (mode == 1)
            {
                cboFilterValue.Enabled = true;
                foreach (var kvp in _bySite.OrderBy(k => k.Key))
                {
                    cboFilterValue.Items.Add($"{kvp.Key} ({kvp.Value.Count})");
                }
            }
            else if (mode == 2)
            {
                cboFilterValue.Enabled = true;
                foreach (var kvp in _byLayer.OrderBy(k => k.Key))
                {
                    cboFilterValue.Items.Add($"{kvp.Key} ({kvp.Value.Count})");
                }
            }
            else if (mode == 3)
            {
                cboFilterValue.Enabled = true;
                foreach (var kvp in _byCorridor.OrderBy(k => k.Key))
                {
                    cboFilterValue.Items.Add($"{kvp.Key} ({kvp.Value.Count})");
                }
            }

            if (cboFilterValue.Items.Count > 0)
            {
                cboFilterValue.SelectedIndex = 0;
            }
            else
            {
                cboFilterValue.Items.Add("(Không có đối tượng phù hợp)");
                cboFilterValue.SelectedIndex = 0;
                cboFilterValue.Enabled = false;
            }
        }

        private void BtnLoadByFilter_Click(object? sender, EventArgs e)
        {
            int mode = cboFilterSource.SelectedIndex;
            string selectedText = cboFilterValue.SelectedItem?.ToString() ?? "";
            string rawKey = ExtractKeyBeforeCount(selectedText);

            List<ObjectId>? targetIds = null;
            if (mode == 0)
            {
                targetIds = _allFeatureLineIdsInDrawing;
            }
            else if (mode == 1 && _bySite.TryGetValue(rawKey, out var siteIds))
            {
                targetIds = siteIds;
            }
            else if (mode == 2 && _byLayer.TryGetValue(rawKey, out var layerIds))
            {
                targetIds = layerIds;
            }
            else if (mode == 3 && _byCorridor.TryGetValue(rawKey, out var corrIds))
            {
                targetIds = corrIds;
            }

            if (targetIds == null || targetIds.Count == 0)
            {
                MessageBox.Show("Không tìm thấy đối tượng nào khớp với bộ lọc đã chọn.", "Bộ lọc trống", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var merged = _items.Select(x => x.Id).Concat(targetIds).Distinct().ToList();
            PopulateItems(merged);
            lblStatus.Text = $"Đã nạp từ bộ lọc: hiện có {_items.Count} đối tượng trong danh sách.";
        }

        private static string ExtractKeyBeforeCount(string display)
        {
            int lastParen = display.LastIndexOf(" (", StringComparison.Ordinal);
            return lastParen > 0 ? display.Substring(0, lastParen) : display;
        }
    }
}

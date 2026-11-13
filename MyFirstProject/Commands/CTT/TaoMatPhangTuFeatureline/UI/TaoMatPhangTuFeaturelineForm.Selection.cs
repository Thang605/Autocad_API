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
        private void PickSourceEntitiesOnCad(bool append)
        {
            PromptSelectionOptions pso = new PromptSelectionOptions
            {
                MessageForAdding = "\nChọn các đường Feature Line, Corridor FL, 3D/2D Polyline (hở hoặc kín): ",
                AllowDuplicates = false
            };

            SelectionFilter filter = new SelectionFilter(new TypedValue[]
            {
                new TypedValue((int)DxfCode.Operator, "<OR"),
                new TypedValue((int)DxfCode.Start, "AECC_FEATURE_LINE"),
                new TypedValue((int)DxfCode.Start, "AECC_AUTO_CORRIDOR_FEATURE_LINE"),
                new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),
                new TypedValue((int)DxfCode.Start, "POLYLINE"),
                new TypedValue((int)DxfCode.Start, "LINE"),
                new TypedValue((int)DxfCode.Start, "ARC"),
                new TypedValue((int)DxfCode.Operator, "OR>")
            });

            PromptSelectionResult psr;
            using (EditorUserInteraction interaction = _ed.StartUserInteraction(this))
            {
                psr = _ed.GetSelection(pso, filter);
            }

            if (psr.Status != PromptStatus.OK || psr.Value == null || psr.Value.Count == 0)
                return;

            List<ObjectId> pickedIds = psr.Value.GetObjectIds().ToList();
            if (append && _items.Count > 0)
            {
                pickedIds = _items.Select(x => x.Id).Concat(pickedIds).Distinct().ToList();
            }

            PopulateItems(pickedIds);
        }

        private void BtnPickExistingSurface_Click(object? sender, EventArgs e)
        {
            PromptEntityOptions peo = new PromptEntityOptions("\nChọn mặt phẳng TIN Surface đích trên bản vẽ: ");
            peo.SetRejectMessage("\nĐối tượng chọn phải là TIN Surface của Civil 3D.");
            peo.AddAllowedClass(typeof(CivSurface), true);

            PromptEntityResult per;
            using (EditorUserInteraction interaction = _ed.StartUserInteraction(this))
            {
                per = _ed.GetEntity(peo);
            }

            if (per.Status != PromptStatus.OK || !IsIdAlive(per.ObjectId)) return;

            using (Transaction tr = _db.TransactionManager.StartTransaction())
            {
                if (tr.GetObject(per.ObjectId, OpenMode.ForRead) is CivSurface surf && !string.IsNullOrWhiteSpace(surf.Name))
                {
                    _existingSurfaceMap[surf.Name] = per.ObjectId;
                    try { _surfaceOriginationMap[surf.Name] = surf.OriginationType; }
                    catch { _surfaceOriginationMap[surf.Name] = SurfaceOriginationType.None; }
                    try { _surfaceTriCountMap[surf.Name] = surf.GetTriangles(false).Count; }
                    catch { _surfaceTriCountMap[surf.Name] = 0; }

                    if (!cboExistingSurfaces.Items.Contains(surf.Name))
                        cboExistingSurfaces.Items.Add(surf.Name);

                    cboExistingSurfaces.SelectedItem = surf.Name;
                    radExistingSurface.Enabled = true;
                    radExistingSurface.Checked = true;
                    OnSelectedExistingSurfaceChanged();
                }
                tr.Commit();
            }
        }

        private void PickCustomBoundaryEntities(bool isOuter)
        {
            PromptSelectionOptions pso = new PromptSelectionOptions
            {
                MessageForAdding = isOuter
                    ? "\nChọn một hoặc nhiều đường Biên bao ngoài (Outer Boundary) khép kín: "
                    : "\nChọn các đường Lỗ khoét (Hide Boundary) khép kín: ",
                AllowDuplicates = false
            };

            SelectionFilter filter = new SelectionFilter(new TypedValue[]
            {
                new TypedValue((int)DxfCode.Operator, "<OR"),
                new TypedValue((int)DxfCode.Start, "AECC_FEATURE_LINE"),
                new TypedValue((int)DxfCode.Start, "AECC_AUTO_CORRIDOR_FEATURE_LINE"),
                new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),
                new TypedValue((int)DxfCode.Start, "POLYLINE"),
                new TypedValue((int)DxfCode.Operator, "OR>")
            });

            PromptSelectionResult psr;
            using (EditorUserInteraction interaction = _ed.StartUserInteraction(this))
            {
                psr = _ed.GetSelection(pso, filter);
            }

            if (psr.Status != PromptStatus.OK || psr.Value == null || psr.Value.Count == 0)
                return;

            var targetList = isOuter ? _customOuterIds : _customHideIds;
            targetList.Clear();
            targetList.AddRange(psr.Value.GetObjectIds().Where(IsIdAlive));
            UpdateCustomBoundaryLabel();
        }

        private void UpdateCustomBoundaryLabel()
        {
            lblCustomBndInfo.Text = $"Biên riêng đã chọn: {_customOuterIds.Count} Outer | {_customHideIds.Count} Hide";
            lblCustomBndInfo.ForeColor = _customOuterIds.Count > 0
                ? WinFormsColor.FromArgb(20, 110, 55)
                : WinFormsColor.FromArgb(70, 80, 95);
        }

        private void PopulateItems(List<ObjectId> ids)
        {
            _items.Clear();
            lvObjects.BeginUpdate();
            lvObjects.Items.Clear();

            try
            {
                if (ids == null || ids.Count == 0)
                {
                    UpdateSummary();
                    return;
                }

                double midOrd = (double)numMidOrd.Value;
                bool autoClose = chkAutoClose.Checked;

                using (Transaction tr = _db.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId id in ids)
                    {
                        if (!IsIdAlive(id)) continue;

                        CadEntity? ent = tr.GetObject(id, OpenMode.ForRead) as CadEntity;
                        if (ent == null) continue;

                        var curveInfo = CTT_Tao_Mat_Phang_Tu_Featureline_Command.ExtractSourceCurveInfo(
                            id, tr, midOrd, 0.10, autoClose, _items.Count);

                        if (curveInfo == null || curveInfo.Points3D.Count < 2)
                            continue;

                        double length2d = 0.0;
                        double minZ = double.MaxValue;
                        double maxZ = double.MinValue;
                        var pts = curveInfo.Points3D;

                        for (int i = 0; i < pts.Count; i++)
                        {
                            if (pts[i].Z < minZ) minZ = pts[i].Z;
                            if (pts[i].Z > maxZ) maxZ = pts[i].Z;
                            if (i > 0)
                            {
                                double dx = pts[i].X - pts[i - 1].X;
                                double dy = pts[i].Y - pts[i - 1].Y;
                                length2d += Math.Sqrt(dx * dx + dy * dy);
                            }
                        }

                        var item = new SelectedFeatureLineBoundaryItem
                        {
                            Id = id,
                            Handle = ent.Handle.ToString(),
                            DisplayName = curveInfo.DisplayName,
                            TypeName = curveInfo.EntityTypeName,
                            Length2D = length2d,
                            IsClosed = curveInfo.IsClosed,
                            MinZ = minZ == double.MaxValue ? 0.0 : minZ,
                            MaxZ = maxZ == double.MinValue ? 0.0 : maxZ,
                            SourceInfo = ent.Layer ?? "0",
                            SamplePoints = pts
                        };

                        _items.Add(item);
                    }
                    tr.Commit();
                }

                RebuildListViewRows();
            }
            finally
            {
                lvObjects.EndUpdate();
            }

            UpdateSummary();
            lblStatus.Text = $"Đã tải {_items.Count} đối tượng nguồn.";
        }

        private void RefreshItemsClosedState()
        {
            if (_items.Count == 0) return;
            List<ObjectId> currentIds = _items.Select(x => x.Id).ToList();
            PopulateItems(currentIds);
        }

        private void RebuildListViewRows()
        {
            lvObjects.Items.Clear();
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                var lvi = new ListViewItem((i + 1).ToString());
                lvi.SubItems.Add(item.DisplayName);
                lvi.SubItems.Add(item.TypeName);
                lvi.SubItems.Add(item.IsClosed ? "Kín" : "Hở");
                lvi.SubItems.Add(item.Length2D.ToString("F2"));
                lvi.SubItems.Add(item.MinZ.ToString("F2"));
                lvi.SubItems.Add(item.MaxZ.ToString("F2"));
                lvi.SubItems.Add(item.SourceInfo);
                lvi.Tag = item;

                if (item.IsClosed)
                {
                    lvi.ForeColor = WinFormsColor.FromArgb(20, 105, 55);
                }
                else
                {
                    lvi.ForeColor = WinFormsColor.FromArgb(18, 72, 135);
                }

                lvObjects.Items.Add(lvi);
            }
        }

        private void RemoveSelectedItemsInList()
        {
            if (lvObjects.SelectedIndices.Count == 0) return;

            var indicesToRemove = lvObjects.SelectedIndices.Cast<int>().OrderByDescending(i => i).ToList();
            foreach (int idx in indicesToRemove)
            {
                if (idx >= 0 && idx < _items.Count)
                    _items.RemoveAt(idx);
            }

            lvObjects.BeginUpdate();
            RebuildListViewRows();
            lvObjects.EndUpdate();
            UpdateSummary();
        }

        private void ClearAllSourceItems()
        {
            _items.Clear();
            lvObjects.Items.Clear();
            UpdateSummary();
            lblStatus.Text = "Đã xóa toàn bộ danh sách đối tượng nguồn.";
        }

        private void UpdateSummary()
        {
            int total = _items.Count;
            int closedCount = _items.Count(x => x.IsClosed);
            int openCount = total - closedCount;
            double totalLen = _items.Sum(x => x.Length2D);

            if (total == 0)
            {
                lblSummary.Text = "Đã chọn: 0 đối tượng (0 đường hở, 0 đường kín) | Tổng chiều dài: 0.00 m";
                return;
            }

            double minZ = _items.Min(x => x.MinZ);
            double maxZ = _items.Max(x => x.MaxZ);
            lblSummary.Text = $"Đã chọn: {total} đối tượng ({openCount} Hở, {closedCount} Kín) | Tổng dài: {totalLen:N2} m | Cao độ Z: [{minZ:F2} .. {maxZ:F2}] m";
        }

        private void BtnSuggestMaxEdge_Click(object? sender, EventArgs e)
        {
            var openItems = _items.Where(x => !x.IsClosed && x.SamplePoints.Count >= 2).ToList();
            if (openItems.Count < 2)
            {
                MessageBox.Show(
                    "Cần có ít nhất 2 đường Feature Line hở trong danh sách để tự động đo bề rộng dải lân cận.\nGiá trị mặc định 25.0 m vẫn hoạt động tốt cho đa số taluy & nền đường.",
                    "Gợi ý chiều dài cạnh tối đa",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            List<double> nearestGaps = new List<double>();
            for (int i = 0; i < openItems.Count; i++)
            {
                var ptsA = openItems[i].SamplePoints;
                int stepA = Math.Max(1, ptsA.Count / 12);

                for (int p = 0; p < ptsA.Count; p += stepA)
                {
                    Point3d pa = ptsA[p];
                    double bestDistSq = double.MaxValue;

                    for (int j = 0; j < openItems.Count; j++)
                    {
                        if (i == j) continue;
                        var ptsB = openItems[j].SamplePoints;
                        for (int q = 0; q < ptsB.Count; q++)
                        {
                            double dx = pa.X - ptsB[q].X;
                            double dy = pa.Y - ptsB[q].Y;
                            double dSq = dx * dx + dy * dy;
                            if (dSq > 0.04 && dSq < bestDistSq) // bỏ qua trùng nút < 0.2m
                            {
                                bestDistSq = dSq;
                            }
                        }
                    }

                    if (bestDistSq < double.MaxValue)
                    {
                        nearestGaps.Add(Math.Sqrt(bestDistSq));
                    }
                }
            }

            if (nearestGaps.Count == 0) return;

            nearestGaps.Sort();
            // Lấy phân vị 75% của khoảng cách ngang sang đường gần nhất và nhân hệ số đường chéo an toàn 2.2x
            double p75Gap = nearestGaps[(int)Math.Min(nearestGaps.Count - 1, nearestGaps.Count * 0.75)];
            double supp = chkUseSupplementing.Checked ? (double)numSupplementing.Value : 5.0;
            double suggested = Math.Ceiling(Math.Sqrt(p75Gap * p75Gap + supp * supp) * 1.85);
            suggested = Math.Max(3.0, Math.Min(500.0, suggested));

            numMaxEdgeLength.Value = ClampDecimal((decimal)suggested, numMaxEdgeLength.Minimum, numMaxEdgeLength.Maximum);
            lblStatus.Text = $"⚡ Đã gợi ý cạnh biên tối đa = {suggested:F2} m (khoảng cách ngang dải ~ {p75Gap:F2} m).";
        }
    }
}

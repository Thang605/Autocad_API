using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;

using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using MyFirstProject.Extensions;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsFont = System.Drawing.Font;
using Point = System.Drawing.Point;

[assembly: CommandClass(typeof(Civil3DCsharp.CTC_ChonDoan_Corridor_Commands))]

namespace Civil3DCsharp
{
    // ═══════════════════════════════════════════════════════════════
    //  COMMAND CLASS
    // ═══════════════════════════════════════════════════════════════
    public class CTC_ChonDoan_Corridor_Commands
    {
        [CommandMethod("CTC_Chon_Doan_Corridor")]
        public static void CTC_ChonDoan_Corridor()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            ed.WriteMessage("\n=== CTC_ChonDoan_Corridor (Multi-Region) ===\n");

            using (var form = new CorridorMultiEditForm(db))
            {
                var dialogResult = Application.ShowModalDialog(form);
                if (dialogResult != DialogResult.OK || !form.FormAccepted || form.EditItems.Count == 0)
                {
                    ed.WriteMessage("\nĐã hủy lệnh.");
                    return;
                }

                // Áp dụng tất cả thay đổi
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    try
                    {
                        var corridor = (Corridor)tr.GetObject(form.SelectedCorridorId, OpenMode.ForWrite);
                        int changeCount = 0;

                        // Theo dõi thay đổi profile per-baseline (profile áp dụng cho toàn bộ baseline)
                        var profileChanges = new Dictionary<int, ObjectId>();

                        foreach (var item in form.EditItems)
                        {
                            var baseline = corridor.Baselines[item.BaselineIndex];
                            var region = baseline.BaselineRegions[item.RegionIndex];
                            var alignment = (Alignment)tr.GetObject(baseline.AlignmentId, OpenMode.ForRead);
                            bool changed = false;

                            ed.WriteMessage($"\n── Phân đoạn: {item.OriginalName} ──");

                            // 1. Đổi tên
                            if (region.Name != item.NewName)
                            {
                                region.Name = item.NewName;
                                ed.WriteMessage($"\n  ✓ Đổi tên → {item.NewName}");
                                changed = true;
                            }

                            // 2. Đổi Assembly
                            if (region.AssemblyId != item.NewAssemblyId)
                            {
                                region.AssemblyId = item.NewAssemblyId;
                                ed.WriteMessage($"\n  ✓ Assembly → {item.NewAssemblyName}");
                                changed = true;
                            }

                            // 3. Profile (ghi nhận, áp dụng sau per-baseline)
                            if (baseline.ProfileId != item.NewProfileId)
                            {
                                profileChanges[item.BaselineIndex] = item.NewProfileId;
                                ed.WriteMessage($"\n  ✓ Profile → {item.NewProfileName} (áp dụng cho Baseline)");
                                changed = true;
                            }

                            // 4. Lý trình
                            if (Math.Abs(region.StartStation - item.NewStartStation) > 0.001 ||
                                Math.Abs(region.EndStation - item.NewEndStation) > 0.001)
                            {
                                ed.WriteMessage($"\n  Lý trình: {region.StartStation:F3}–{region.EndStation:F3} → {item.NewStartStation:F3}–{item.NewEndStation:F3}");
                                UpdateRegionStations(region, alignment, item.NewStartStation, item.NewEndStation, ed);
                                changed = true;
                            }

                            if (changed) changeCount++;
                        }

                        // Áp dụng profile per-baseline
                        foreach (var kvp in profileChanges)
                        {
                            var baseline = corridor.Baselines[kvp.Key];
                            baseline.SetAlignmentAndProfile(baseline.AlignmentId, kvp.Value);
                        }

                        // Rebuild
                        try
                        {
                            corridor.Rebuild();
                            ed.WriteMessage($"\n\n✅ Rebuild Corridor thành công! ({changeCount} phân đoạn được cập nhật)");
                        }
                        catch (System.Exception ex)
                        {
                            ed.WriteMessage($"\n⚠ Rebuild: {ex.Message}");
                        }

                        tr.Commit();
                        ed.WriteMessage("\n✅ Hoàn tất.\n");
                    }
                    catch (System.Exception ex)
                    {
                        ed.WriteMessage($"\nLỗi: {ex.Message}");
                        tr.Abort();
                    }
                }
            }
        }

        private static void UpdateRegionStations(BaselineRegion region, Alignment alignment, double newStart, double newEnd, Editor ed)
        {
            try
            {
                newStart = Math.Max(newStart, alignment.StartingStation);
                newEnd = Math.Min(newEnd, alignment.EndingStation);
                if (newStart >= newEnd) { ed.WriteMessage("\n  ⚠ Lý trình đầu >= cuối!"); return; }

                bool needsTwoStep = newStart < region.StartStation || newEnd > region.EndStation;
                if (needsTwoStep)
                {
                    region.StartStation = Math.Max(Math.Min(region.StartStation, newStart), alignment.StartingStation);
                    region.EndStation = Math.Min(Math.Max(region.EndStation, newEnd), alignment.EndingStation);
                }
                region.StartStation = newStart;
                region.EndStation = newEnd;
                ed.WriteMessage("\n  ✓ Cập nhật lý trình.");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n  ⚠ Lỗi lý trình: {ex.Message}");
                try { region.StartStation = newStart; region.EndStation = newEnd; ed.WriteMessage("\n  ✓ Fallback OK."); }
                catch (System.Exception ex2) { ed.WriteMessage($"\n  ❌ Fallback: {ex2.Message}"); }
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  DỮ LIỆU: Thông tin 1 phân đoạn cần chỉnh sửa
    // ═══════════════════════════════════════════════════════════════
    public class RegionEditItem
    {
        // Vị trí trong corridor
        public int BaselineIndex;
        public int RegionIndex;
        public ObjectId AlignmentId;

        // Giá trị gốc
        public string OriginalName;
        public ObjectId OriginalAssemblyId;
        public ObjectId OriginalProfileId;
        public double OriginalStartStation;
        public double OriginalEndStation;

        // Giá trị mới (sau khi chỉnh sửa)
        public string NewName;
        public ObjectId NewAssemblyId;
        public string NewAssemblyName;
        public ObjectId NewProfileId;
        public string NewProfileName;
        public double NewStartStation;
        public double NewEndStation;

        // Thông tin hiển thị
        public string BaselineName;
        public string AlignmentName;
        public double ClickedStation;

        public bool IsModified =>
            NewName != OriginalName ||
            NewAssemblyId != OriginalAssemblyId ||
            NewProfileId != OriginalProfileId ||
            Math.Abs(NewStartStation - OriginalStartStation) > 0.001 ||
            Math.Abs(NewEndStation - OriginalEndStation) > 0.001;
    }

    // ═══════════════════════════════════════════════════════════════
    //  COMBOBOX ITEM WRAPPER
    // ═══════════════════════════════════════════════════════════════
    public class IdItem
    {
        public string Name { get; }
        public ObjectId Id { get; }
        public IdItem(string name, ObjectId id) { Name = name; Id = id; }
        public override string ToString() => Name;
    }

    // ═══════════════════════════════════════════════════════════════
    //  MAIN FORM: Chọn Corridor, chọn nhiều điểm, quản lý danh sách
    // ═══════════════════════════════════════════════════════════════
    public class CorridorMultiEditForm : Form
    {
        private Database db;

        // Kết quả
        public ObjectId SelectedCorridorId { get; private set; } = ObjectId.Null;
        public List<RegionEditItem> EditItems { get; private set; } = new List<RegionEditItem>();
        public bool FormAccepted { get; private set; } = false;

        // Controls
        private ComboBox cmbCorridor;
        private Button btnPickPoint;
        private ListView listView;
        private Button btnEdit;
        private Button btnRemove;
        private Button btnOK;
        private Button btnCancel;

        public CorridorMultiEditForm(Database db)
        {
            this.db = db;
            BuildUI();
            LoadCorridors();
        }

        // ═══════════════════════════════════════
        //  XÂY DỰNG GIAO DIỆN
        // ═══════════════════════════════════════
        private void BuildUI()
        {
            this.Text = "CTC_ChonDoan_Corridor — Chọn nhiều phân đoạn";
            this.Size = new Size(620, 520);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowIcon = false;

            // Tiêu đề
            var lblTitle = new WinFormsLabel
            {
                Text = "HIỆU CHỈNH NHIỀU PHÂN ĐOẠN CORRIDOR",
                Location = new Point(15, 10),
                Size = new Size(570, 25),
                Font = new WinFormsFont("Segoe UI", 12F, FontStyle.Bold),
                ForeColor = Color.Navy
            };

            // ── Corridor ──
            var lblCorridor = new WinFormsLabel
            {
                Text = "Corridor:",
                Location = new Point(15, 45),
                Size = new Size(65, 20),
                Font = new WinFormsFont("Segoe UI", 9F)
            };

            cmbCorridor = new ComboBox
            {
                Location = new Point(85, 42),
                Size = new Size(500, 23),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new WinFormsFont("Segoe UI", 9F)
            };
            cmbCorridor.SelectedIndexChanged += (s, e) =>
            {
                btnPickPoint.Enabled = cmbCorridor.SelectedItem is IdItem;
                EditItems.Clear();
                RefreshListView();
                UpdateButtons();
            };

            // ── Nút chọn điểm ──
            btnPickPoint = new Button
            {
                Text = "🎯  Chọn điểm trên bản vẽ (bấm nhiều lần để thêm)...",
                Location = new Point(15, 75),
                Size = new Size(440, 32),
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Enabled = false
            };
            btnPickPoint.Click += BtnPickPoint_Click;

            // ── Nút Sửa / Xóa ──
            btnEdit = new Button
            {
                Text = "✏️ Sửa...",
                Location = new Point(465, 75),
                Size = new Size(60, 32),
                Cursor = Cursors.Hand,
                Enabled = false,
                Font = new WinFormsFont("Segoe UI", 8F)
            };
            btnEdit.Click += BtnEdit_Click;

            btnRemove = new Button
            {
                Text = "❌ Xóa",
                Location = new Point(530, 75),
                Size = new Size(55, 32),
                Cursor = Cursors.Hand,
                Enabled = false,
                Font = new WinFormsFont("Segoe UI", 8F)
            };
            btnRemove.Click += BtnRemove_Click;

            // ── ListView ──
            listView = new ListView
            {
                Location = new Point(15, 115),
                Size = new Size(570, 310),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false,
                Font = new WinFormsFont("Segoe UI", 9F),
                HeaderStyle = ColumnHeaderStyle.Nonclickable
            };

            listView.Columns.Add("Region", 125);
            listView.Columns.Add("Baseline", 100);
            listView.Columns.Add("Assembly", 105);
            listView.Columns.Add("Profile", 105);
            listView.Columns.Add("Lý trình", 110);

            listView.DoubleClick += (s, e) => EditSelectedItem();
            listView.SelectedIndexChanged += (s, e) => UpdateButtons();

            // ── Nút OK / Hủy ──
            btnOK = new Button
            {
                Text = "Đồng ý",
                Location = new Point(400, 440),
                Size = new Size(90, 32),
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold),
                DialogResult = DialogResult.OK,
                Cursor = Cursors.Hand,
                Enabled = false
            };
            btnOK.Click += BtnOK_Click;

            btnCancel = new Button
            {
                Text = "Hủy",
                Location = new Point(498, 440),
                Size = new Size(90, 32),
                DialogResult = DialogResult.Cancel,
                Cursor = Cursors.Hand,
                Font = new WinFormsFont("Segoe UI", 9F)
            };

            this.Controls.AddRange(new Control[] {
                lblTitle, lblCorridor, cmbCorridor,
                btnPickPoint, btnEdit, btnRemove,
                listView, btnOK, btnCancel
            });

            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;
        }

        // ═══════════════════════════════════════
        //  LOAD CORRIDORS
        // ═══════════════════════════════════════
        private void LoadCorridors()
        {
            cmbCorridor.Items.Clear();
            try
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId id in A.Cdoc.CorridorCollection)
                    {
                        var c = tr.GetObject(id, OpenMode.ForRead) as Corridor;
                        if (c != null) cmbCorridor.Items.Add(new IdItem(c.Name, id));
                    }
                    tr.Commit();
                }
            }
            catch { }
            if (cmbCorridor.Items.Count > 0) cmbCorridor.SelectedIndex = 0;
        }

        // ═══════════════════════════════════════
        //  CHỌN ĐIỂM TRÊN BẢN VẼ
        // ═══════════════════════════════════════
        private void BtnPickPoint_Click(object sender, EventArgs e)
        {
            var corridorItem = cmbCorridor.SelectedItem as IdItem;
            if (corridorItem == null) return;

            var ed = Application.DocumentManager.MdiActiveDocument.Editor;

            Point3d clickedPoint;
            using (var interaction = ed.StartUserInteraction(this))
            {
                var ppo = new PromptPointOptions("\nChọn điểm trên bình đồ hoặc trắc dọc (ESC để dừng): ");
                var ppr = ed.GetPoint(ppo);
                interaction.End();
                if (ppr.Status != PromptStatus.OK) return;
                clickedPoint = ppr.Value;
            }

            // Tìm region tại điểm
            var match = FindRegionAtPoint(corridorItem.Id, clickedPoint);
            if (match == null)
            {
                MessageBox.Show(
                    "Không tìm thấy phân đoạn Corridor nào chứa điểm đã chọn.\n" +
                    "Hãy click vào khu vực bình đồ hoặc trắc dọc của Corridor.",
                    "Không tìm thấy", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Kiểm tra trùng lặp
            bool duplicate = EditItems.Any(x =>
                x.BaselineIndex == match.BaselineIndex &&
                x.RegionIndex == match.RegionIndex);

            if (duplicate)
            {
                MessageBox.Show(
                    $"Phân đoạn \"{match.OriginalName}\" đã có trong danh sách.",
                    "Trùng lặp", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            EditItems.Add(match);
            RefreshListView();
            UpdateButtons();
        }

        // ═══════════════════════════════════════
        //  SỬA / XÓA
        // ═══════════════════════════════════════
        private void BtnEdit_Click(object sender, EventArgs e) => EditSelectedItem();
        private void BtnRemove_Click(object sender, EventArgs e)
        {
            if (listView.SelectedItems.Count == 0) return;
            var item = (RegionEditItem)listView.SelectedItems[0].Tag;
            EditItems.Remove(item);
            RefreshListView();
            UpdateButtons();
        }

        private void EditSelectedItem()
        {
            if (listView.SelectedItems.Count == 0) return;
            var item = (RegionEditItem)listView.SelectedItems[0].Tag;

            using (var subForm = new RegionEditSubForm(db, item))
            {
                if (subForm.ShowDialog(this) == DialogResult.OK)
                {
                    RefreshListView();
                    UpdateButtons();
                }
            }
        }

        // ═══════════════════════════════════════
        //  NHẤN ĐỒNG Ý
        // ═══════════════════════════════════════
        private void BtnOK_Click(object sender, EventArgs e)
        {
            if (EditItems.Count == 0) return;

            SelectedCorridorId = ((IdItem)cmbCorridor.SelectedItem).Id;
            FormAccepted = true;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        // ═══════════════════════════════════════
        //  CẬP NHẬT LISTVIEW
        // ═══════════════════════════════════════
        private void RefreshListView()
        {
            listView.Items.Clear();
            foreach (var item in EditItems)
            {
                var lvi = new ListViewItem(item.NewName);
                lvi.SubItems.Add(item.BaselineName);
                lvi.SubItems.Add(item.NewAssemblyName);
                lvi.SubItems.Add(item.NewProfileName);
                lvi.SubItems.Add($"{item.NewStartStation:F3} – {item.NewEndStation:F3}");
                lvi.Tag = item;

                if (item.IsModified)
                {
                    lvi.ForeColor = Color.Blue;
                    lvi.Font = new WinFormsFont(listView.Font, FontStyle.Bold);
                }

                listView.Items.Add(lvi);
            }
        }

        private void UpdateButtons()
        {
            bool hasSelection = listView.SelectedItems.Count > 0;
            btnEdit.Enabled = hasSelection;
            btnRemove.Enabled = hasSelection;
            btnOK.Enabled = EditItems.Count > 0;
        }

        // ═══════════════════════════════════════════════════════════════
        //  TÌM BASELINE / REGION TẠI ĐIỂM
        // ═══════════════════════════════════════════════════════════════
        private RegionEditItem FindRegionAtPoint(ObjectId corridorId, Point3d clickedPoint)
        {
            try
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var corridor = tr.GetObject(corridorId, OpenMode.ForRead) as Corridor;
                    if (corridor == null || corridor.Baselines.Count == 0) { tr.Commit(); return null; }

                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                    var pvClass = RXClass.GetClass(typeof(ProfileView));

                    // ── 1. Kiểm tra ProfileView ──
                    foreach (ObjectId id in ms)
                    {
                        if (!id.ObjectClass.IsDerivedFrom(pvClass)) continue;
                        var pv = tr.GetObject(id, OpenMode.ForRead) as ProfileView;
                        if (pv == null) continue;

                        try
                        {
                            var ext = pv.GeometricExtents;
                            if (clickedPoint.X < ext.MinPoint.X || clickedPoint.X > ext.MaxPoint.X ||
                                clickedPoint.Y < ext.MinPoint.Y || clickedPoint.Y > ext.MaxPoint.Y) continue;

                            double station = 0, elevation = 0;
                            pv.FindStationAndElevationAtXY(clickedPoint.X, clickedPoint.Y, ref station, ref elevation);

                            for (int bi = 0; bi < corridor.Baselines.Count; bi++)
                            {
                                if (corridor.Baselines[bi].AlignmentId != pv.AlignmentId) continue;
                                var result = BuildEditItem(tr, corridor, bi, station);
                                if (result != null) { tr.Commit(); return result; }
                            }
                        }
                        catch { }
                    }

                    // ── 2. Bình đồ ──
                    RegionEditItem bestItem = null;
                    double minOffset = double.MaxValue;

                    for (int bi = 0; bi < corridor.Baselines.Count; bi++)
                    {
                        try
                        {
                            var alignment = tr.GetObject(corridor.Baselines[bi].AlignmentId, OpenMode.ForRead) as Alignment;
                            if (alignment == null) continue;

                            double station = 0, offset = 0;
                            alignment.StationOffset(clickedPoint.X, clickedPoint.Y, ref station, ref offset);

                            if (Math.Abs(offset) < minOffset &&
                                station >= alignment.StartingStation && station <= alignment.EndingStation)
                            {
                                minOffset = Math.Abs(offset);
                                var result = BuildEditItem(tr, corridor, bi, station);
                                if (result != null) bestItem = result;
                            }
                        }
                        catch { }
                    }

                    tr.Commit();
                    return bestItem;
                }
            }
            catch { return null; }
        }

        private RegionEditItem BuildEditItem(Transaction tr, Corridor corridor, int baselineIdx, double station)
        {
            var baseline = corridor.Baselines[baselineIdx];
            if (baseline.BaselineRegions.Count == 0) return null;

            var alignment = tr.GetObject(baseline.AlignmentId, OpenMode.ForRead) as Alignment;
            string alignmentName = alignment?.Name ?? "N/A";

            // Tìm region chứa station
            int targetIdx = -1;
            for (int ri = 0; ri < baseline.BaselineRegions.Count; ri++)
            {
                var r = baseline.BaselineRegions[ri];
                if (station >= r.StartStation && station <= r.EndStation) { targetIdx = ri; break; }
            }

            // Nếu không nằm trong region → lấy gần nhất
            if (targetIdx < 0)
            {
                double minDist = double.MaxValue;
                for (int ri = 0; ri < baseline.BaselineRegions.Count; ri++)
                {
                    var r = baseline.BaselineRegions[ri];
                    double dist = Math.Min(Math.Abs(station - r.StartStation), Math.Abs(station - r.EndStation));
                    if (dist < minDist) { minDist = dist; targetIdx = ri; }
                }
            }

            if (targetIdx < 0) return null;
            var region = baseline.BaselineRegions[targetIdx];

            // Lấy tên Assembly và Profile hiện tại
            string asmName = "N/A";
            try
            {
                var asm = tr.GetObject(region.AssemblyId, OpenMode.ForRead) as Assembly;
                if (asm != null) asmName = asm.Name;
            }
            catch { }

            string prfName = "N/A";
            try
            {
                var prf = tr.GetObject(baseline.ProfileId, OpenMode.ForRead) as Profile;
                if (prf != null) prfName = prf.Name;
            }
            catch { }

            return new RegionEditItem
            {
                BaselineIndex = baselineIdx,
                RegionIndex = targetIdx,
                AlignmentId = baseline.AlignmentId,

                OriginalName = region.Name,
                OriginalAssemblyId = region.AssemblyId,
                OriginalProfileId = baseline.ProfileId,
                OriginalStartStation = region.StartStation,
                OriginalEndStation = region.EndStation,

                NewName = region.Name,
                NewAssemblyId = region.AssemblyId,
                NewAssemblyName = asmName,
                NewProfileId = baseline.ProfileId,
                NewProfileName = prfName,
                NewStartStation = region.StartStation,
                NewEndStation = region.EndStation,

                BaselineName = baseline.Name,
                AlignmentName = alignmentName,
                ClickedStation = station
            };
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  SUB-FORM: Chỉnh sửa chi tiết 1 phân đoạn
    // ═══════════════════════════════════════════════════════════════
    public class RegionEditSubForm : Form
    {
        private Database db;
        private RegionEditItem item;

        private TextBox txtName;
        private ComboBox cmbAssembly;
        private ComboBox cmbProfile;
        private TextBox txtStart;
        private TextBox txtEnd;

        public RegionEditSubForm(Database db, RegionEditItem item)
        {
            this.db = db;
            this.item = item;
            BuildUI();
            LoadAssemblies();
            LoadProfiles();
            PopulateFromItem();
        }

        private void BuildUI()
        {
            this.Text = $"Sửa phân đoạn: {item.OriginalName}";
            this.Size = new Size(430, 330);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowIcon = false;

            // Header
            var lblHeader = new WinFormsLabel
            {
                Text = $"Baseline: {item.BaselineName}   |   Tuyến: {item.AlignmentName}",
                Location = new Point(15, 10),
                Size = new Size(390, 20),
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Italic),
                ForeColor = Color.DarkSlateGray
            };

            // GroupBox
            var grp = new GroupBox
            {
                Text = "Thông số",
                Location = new Point(15, 35),
                Size = new Size(385, 200),
                Font = new WinFormsFont("Segoe UI", 9F)
            };

            var lblN = new WinFormsLabel { Text = "Tên phân đoạn:", Location = new Point(15, 28), Size = new Size(110, 20) };
            txtName = new TextBox { Location = new Point(130, 25), Size = new Size(230, 23) };

            var lblA = new WinFormsLabel { Text = "Assembly:", Location = new Point(15, 63), Size = new Size(110, 20) };
            cmbAssembly = new ComboBox { Location = new Point(130, 60), Size = new Size(230, 23), DropDownStyle = ComboBoxStyle.DropDownList };

            var lblP = new WinFormsLabel { Text = "Profile (Đg đỏ):", Location = new Point(15, 98), Size = new Size(110, 20) };
            cmbProfile = new ComboBox { Location = new Point(130, 95), Size = new Size(230, 23), DropDownStyle = ComboBoxStyle.DropDownList };

            // Ghi chú profile
            var lblNote = new WinFormsLabel
            {
                Text = "⚠ Profile áp dụng cho toàn bộ Baseline",
                Location = new Point(130, 120),
                Size = new Size(240, 16),
                ForeColor = Color.OrangeRed,
                Font = new WinFormsFont("Segoe UI", 7.5F, FontStyle.Italic)
            };

            var lblS = new WinFormsLabel { Text = "Lý trình đầu:", Location = new Point(15, 145), Size = new Size(110, 20) };
            txtStart = new TextBox { Location = new Point(130, 142), Size = new Size(230, 23) };

            var lblE = new WinFormsLabel { Text = "Lý trình cuối:", Location = new Point(15, 175), Size = new Size(110, 20) };
            txtEnd = new TextBox { Location = new Point(130, 172), Size = new Size(230, 23) };

            grp.Controls.AddRange(new Control[] {
                lblN, txtName, lblA, cmbAssembly,
                lblP, cmbProfile, lblNote,
                lblS, txtStart, lblE, txtEnd
            });

            // Buttons
            var btnSave = new Button
            {
                Text = "Lưu",
                Location = new Point(220, 250),
                Size = new Size(85, 30),
                Font = new WinFormsFont("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnSave.Click += BtnSave_Click;

            var btnCancel = new Button
            {
                Text = "Hủy",
                Location = new Point(315, 250),
                Size = new Size(85, 30),
                DialogResult = DialogResult.Cancel,
                Cursor = Cursors.Hand
            };

            this.Controls.AddRange(new Control[] { lblHeader, grp, btnSave, btnCancel });
            this.CancelButton = btnCancel;
        }

        private void LoadAssemblies()
        {
            cmbAssembly.Items.Clear();
            try
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId id in A.Cdoc.AssemblyCollection)
                    {
                        var asm = tr.GetObject(id, OpenMode.ForRead) as Assembly;
                        if (asm != null) cmbAssembly.Items.Add(new IdItem(asm.Name, id));
                    }
                    tr.Commit();
                }
            }
            catch { }
        }

        private void LoadProfiles()
        {
            cmbProfile.Items.Clear();
            try
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var alignment = tr.GetObject(item.AlignmentId, OpenMode.ForRead) as Alignment;
                    if (alignment != null)
                    {
                        foreach (ObjectId pId in alignment.GetProfileIds())
                        {
                            var prf = tr.GetObject(pId, OpenMode.ForRead) as Profile;
                            if (prf != null) cmbProfile.Items.Add(new IdItem(prf.Name, pId));
                        }
                    }
                    tr.Commit();
                }
            }
            catch { }
        }

        private void PopulateFromItem()
        {
            txtName.Text = item.NewName;
            txtStart.Text = item.NewStartStation.ToString("F3");
            txtEnd.Text = item.NewEndStation.ToString("F3");

            // Chọn Assembly hiện tại
            for (int i = 0; i < cmbAssembly.Items.Count; i++)
            {
                if (cmbAssembly.Items[i] is IdItem it && it.Id == item.NewAssemblyId)
                { cmbAssembly.SelectedIndex = i; break; }
            }
            if (cmbAssembly.SelectedIndex < 0 && cmbAssembly.Items.Count > 0)
                cmbAssembly.SelectedIndex = 0;

            // Chọn Profile hiện tại
            for (int i = 0; i < cmbProfile.Items.Count; i++)
            {
                if (cmbProfile.Items[i] is IdItem it && it.Id == item.NewProfileId)
                { cmbProfile.SelectedIndex = i; break; }
            }
            if (cmbProfile.SelectedIndex < 0 && cmbProfile.Items.Count > 0)
                cmbProfile.SelectedIndex = 0;
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
            { MessageBox.Show("Tên không được trống.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            if (cmbAssembly.SelectedItem == null)
            { MessageBox.Show("Chọn Assembly.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            if (cmbProfile.SelectedItem == null)
            { MessageBox.Show("Chọn Profile.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            if (!TryParseStation(txtStart.Text, out double s))
            { MessageBox.Show("Lý trình đầu không hợp lệ.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            if (!TryParseStation(txtEnd.Text, out double en))
            { MessageBox.Show("Lý trình cuối không hợp lệ.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            if (s >= en)
            { MessageBox.Show("Lý trình đầu phải nhỏ hơn cuối.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            // Lưu vào item
            var asmItem = (IdItem)cmbAssembly.SelectedItem;
            var prfItem = (IdItem)cmbProfile.SelectedItem;

            item.NewName = txtName.Text.Trim();
            item.NewAssemblyId = asmItem.Id;
            item.NewAssemblyName = asmItem.Name;
            item.NewProfileId = prfItem.Id;
            item.NewProfileName = prfItem.Name;
            item.NewStartStation = s;
            item.NewEndStation = en;

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private bool TryParseStation(string input, out double station)
        {
            station = 0;
            if (string.IsNullOrWhiteSpace(input)) return false;
            string cleaned = input.Replace("Km", "", StringComparison.OrdinalIgnoreCase)
                                  .Replace("km", "", StringComparison.OrdinalIgnoreCase)
                                  .Replace("+", "").Trim();
            return double.TryParse(cleaned, out station);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil;
using Autodesk.Civil.DatabaseServices;
using Civil3DCsharp;
using MyFirstProject.Extensions;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Acad = Autodesk.AutoCAD.ApplicationServices;

namespace MyFirstProject.Menu_form
{
    public partial class KiemTraProfileForm : Form
    {
        private List<ObjectId> _alignmentIds = new List<ObjectId>();
        private List<ObjectId> _profileIds = new List<ObjectId>();
        private IDesignStandard _currentStandard;
        private List<ProfileCheckItem> _rawResults = new List<ProfileCheckItem>();

        // Bộ nhớ lưu thông số đã chọn ở lần chạy trước
        private static string _lastStandardName = "";
        private static string _lastAlignmentName = "";
        private static string _lastProfileName = "";
        private static string _lastDesignSpeed = "";
        private static string _lastTerrain = "";

        private static bool _lastChkMaxGrade = true;
        private static bool _lastChkMinGrade = true;
        private static bool _lastChkVerticalCurve = true;
        private static bool _lastChkGradeLength = true;

        private static bool _lastChkShowPassed = true;
        private static bool _lastChkShowWarning = true;
        private static bool _lastChkShowFailed = true;

        public KiemTraProfileForm()
        {
            InitializeComponent();
            this.FormClosing += (s, e) => SaveLastUsedValues();
        }

        private void KiemTraProfileForm_Load(object sender, EventArgs e)
        {
            LoadAlignments();

            var standards = StandardFactory.GetAllStandards();
            cbbStandard.DataSource = standards;
            cbbStandard.DisplayMember = "StandardName";

            if (cbbTerrain.Items.Count > 0)
                cbbTerrain.SelectedIndex = 0;

            RestoreLastUsedValues();
        }

        private void RestoreLastUsedValues()
        {
            try
            {
                if (!string.IsNullOrEmpty(_lastStandardName))
                {
                    for (int i = 0; i < cbbStandard.Items.Count; i++)
                    {
                        var std = cbbStandard.Items[i] as IDesignStandard;
                        if (std != null && std.StandardName == _lastStandardName)
                        {
                            cbbStandard.SelectedIndex = i;
                            break;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(_lastDesignSpeed))
                {
                    int idx = cbbDesignSpeed.Items.IndexOf(_lastDesignSpeed);
                    if (idx >= 0) cbbDesignSpeed.SelectedIndex = idx;
                }

                if (!string.IsNullOrEmpty(_lastTerrain))
                {
                    int idx = cbbTerrain.Items.IndexOf(_lastTerrain);
                    if (idx >= 0) cbbTerrain.SelectedIndex = idx;
                }

                if (!string.IsNullOrEmpty(_lastAlignmentName))
                {
                    int idx = cbbAlignments.Items.IndexOf(_lastAlignmentName);
                    if (idx >= 0)
                    {
                        cbbAlignments.SelectedIndex = idx;
                    }
                }

                if (!string.IsNullOrEmpty(_lastProfileName))
                {
                    for (int i = 0; i < cbbProfiles.Items.Count; i++)
                    {
                        if (cbbProfiles.Items[i].ToString().StartsWith(_lastProfileName))
                        {
                            cbbProfiles.SelectedIndex = i;
                            break;
                        }
                    }
                }

                chkMaxGrade.Checked = _lastChkMaxGrade;
                chkMinGrade.Checked = _lastChkMinGrade;
                chkVerticalCurve.Checked = _lastChkVerticalCurve;
                chkGradeLength.Checked = _lastChkGradeLength;

                chkShowPassed.Checked = _lastChkShowPassed;
                chkShowWarning.Checked = _lastChkShowWarning;
                chkShowFailed.Checked = _lastChkShowFailed;
            }
            catch { }
        }

        private void SaveLastUsedValues()
        {
            try
            {
                if (cbbStandard.SelectedItem is IDesignStandard std)
                    _lastStandardName = std.StandardName;

                if (cbbAlignments.SelectedItem != null)
                    _lastAlignmentName = cbbAlignments.SelectedItem.ToString() ?? "";

                if (cbbProfiles.SelectedItem != null)
                    _lastProfileName = cbbProfiles.SelectedItem.ToString()?.Split(' ')[0] ?? "";

                if (cbbDesignSpeed.SelectedItem != null)
                    _lastDesignSpeed = cbbDesignSpeed.SelectedItem.ToString() ?? "";

                if (cbbTerrain.SelectedItem != null)
                    _lastTerrain = cbbTerrain.SelectedItem.ToString() ?? "";

                _lastChkMaxGrade = chkMaxGrade.Checked;
                _lastChkMinGrade = chkMinGrade.Checked;
                _lastChkVerticalCurve = chkVerticalCurve.Checked;
                _lastChkGradeLength = chkGradeLength.Checked;

                _lastChkShowPassed = chkShowPassed.Checked;
                _lastChkShowWarning = chkShowWarning.Checked;
                _lastChkShowFailed = chkShowFailed.Checked;
            }
            catch { }
        }

        private void cbbStandard_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cbbStandard.SelectedIndex < 0) return;
            _currentStandard = (IDesignStandard)cbbStandard.SelectedItem;

            cbbDesignSpeed.Items.Clear();
            foreach (var speed in _currentStandard.SupportedSpeeds)
            {
                cbbDesignSpeed.Items.Add(speed.ToString());
            }

            if (cbbDesignSpeed.Items.Count > 0)
            {
                int idx = cbbDesignSpeed.Items.IndexOf("60");
                if (idx >= 0) cbbDesignSpeed.SelectedIndex = idx;
                else cbbDesignSpeed.SelectedIndex = 0;
            }
        }

        private void LoadAlignments()
        {
            cbbAlignments.Items.Clear();
            _alignmentIds.Clear();

            try
            {
                using (var tr = A.Db.TransactionManager.StartTransaction())
                {
                    var alignmentIds = A.Cdoc.GetAlignmentIds();
                    foreach (ObjectId id in alignmentIds)
                    {
                        var align = tr.GetObject(id, OpenMode.ForRead) as Alignment;
                        if (align != null && !align.IsConnectedAlignment)
                        {
                            cbbAlignments.Items.Add(align.Name);
                            _alignmentIds.Add(id);
                        }
                    }
                    tr.Commit();
                }

                if (cbbAlignments.Items.Count > 0)
                    cbbAlignments.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi tải Alignment: {ex.Message}");
            }
        }

        private void cbbAlignments_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadProfiles();
        }

        private void LoadProfiles()
        {
            cbbProfiles.Items.Clear();
            _profileIds.Clear();

            if (cbbAlignments.SelectedIndex < 0 || cbbAlignments.SelectedIndex >= _alignmentIds.Count)
                return;

            ObjectId alignId = _alignmentIds[cbbAlignments.SelectedIndex];

            try
            {
                using (var tr = A.Db.TransactionManager.StartTransaction())
                {
                    var align = tr.GetObject(alignId, OpenMode.ForRead) as Alignment;
                    if (align != null)
                    {
                        ObjectIdCollection profIds = align.GetProfileIds();
                        foreach (ObjectId pId in profIds)
                        {
                            var prof = tr.GetObject(pId, OpenMode.ForRead) as Profile;
                            if (prof != null)
                            {
                                // Ưu tiên liệt kê Profile thiết kế (Layout Profile)
                                string typeName = prof.ProfileType == ProfileType.FG ? "[Thiết kế]" : "[Bề mặt]";
                                cbbProfiles.Items.Add($"{prof.Name} {typeName}");
                                _profileIds.Add(pId);
                            }
                        }
                    }
                    tr.Commit();
                }

                if (cbbProfiles.Items.Count > 0)
                {
                    // Tự động chọn Profile Thiết kế đầu tiên nếu có
                    int defaultIdx = 0;
                    for (int i = 0; i < cbbProfiles.Items.Count; i++)
                    {
                        if (cbbProfiles.Items[i].ToString().Contains("[Thiết kế]"))
                        {
                            defaultIdx = i;
                            break;
                        }
                    }
                    cbbProfiles.SelectedIndex = defaultIdx;
                }
            }
            catch (Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi tải Profiles: {ex.Message}");
            }
        }

        private void btnPickAlignment_Click(object sender, EventArgs e)
        {
            using (A.Doc.LockDocument())
            {
                var prevVisible = this.Visible;
                if (prevVisible) this.Hide();

                try
                {
                    ObjectId alignId = UserInput.GAlignmentId("\nChọn tim tuyến: ");
                    if (!alignId.IsNull)
                    {
                        using (var tr = A.Db.TransactionManager.StartTransaction())
                        {
                            var align = tr.GetObject(alignId, OpenMode.ForRead) as Alignment;
                            if (align != null)
                            {
                                if (!_alignmentIds.Contains(alignId))
                                {
                                    _alignmentIds.Add(alignId);
                                    cbbAlignments.Items.Add(align.Name);
                                }
                                cbbAlignments.SelectedIndex = _alignmentIds.IndexOf(alignId);
                            }
                            tr.Commit();
                        }
                    }
                }
                catch (Exception ex)
                {
                    A.Ed.WriteMessage($"\nHủy chọn tim tuyến: {ex.Message}");
                }
                finally
                {
                    if (prevVisible) this.Show();
                }
            }
        }

        private void btnCheck_Click(object sender, EventArgs e)
        {
            if (cbbAlignments.SelectedIndex < 0)
            {
                MessageBox.Show("Vui lòng chọn Alignment.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (cbbProfiles.SelectedIndex < 0 || cbbProfiles.SelectedIndex >= _profileIds.Count)
            {
                MessageBox.Show("Vui lòng chọn Profile để kiểm tra.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (cbbStandard.SelectedIndex < 0 || cbbDesignSpeed.SelectedIndex < 0)
            {
                MessageBox.Show("Vui lòng chọn tiêu chuẩn và vận tốc thiết kế.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int speed = int.Parse(cbbDesignSpeed.SelectedItem.ToString());
            string terrain = cbbTerrain.SelectedItem != null ? cbbTerrain.SelectedItem.ToString() : "Đồng bằng";

            var profileParams = _currentStandard.GetProfileParameters(speed, terrain);
            ObjectId profileId = _profileIds[cbbProfiles.SelectedIndex];

            _rawResults.Clear();

            using (A.Doc.LockDocument())
            {
                using (var tr = A.Db.TransactionManager.StartTransaction())
                {
                    var profile = tr.GetObject(profileId, OpenMode.ForRead) as Profile;
                    if (profile != null)
                    {
                        _rawResults = ProfileEvaluator.Evaluate(profile, profileParams);
                    }
                    tr.Commit();
                }
            }

            // Lọc theo các checkbox nội dung
            var filteredResults = _rawResults.Where(r =>
            {
                if (!chkMaxGrade.Checked && r.ItemName.Contains("(i%)")) return false;
                if (!chkMinGrade.Checked && r.ItemName.Contains("Thoát nước")) return false;
                if (!chkGradeLength.Checked && r.ItemName.Contains("Chiều dài đoạn dốc")) return false;
                if (!chkVerticalCurve.Checked && (r.ItemName.Contains("Cong đứng") || r.ItemName.Contains("PVI tại"))) return false;
                return true;
            }).ToList();

            DisplayResults(filteredResults);
        }

        private void DisplayResults(List<ProfileCheckItem> results)
        {
            dgvResults.Rows.Clear();

            int passCount = 0, warnCount = 0, failCount = 0;

            foreach (var item in results)
            {
                if (item.Status == CheckStatus.Pass && !chkShowPassed.Checked) continue;
                if (item.Status == CheckStatus.Warning && !chkShowWarning.Checked) continue;
                if (item.Status == CheckStatus.Fail && !chkShowFailed.Checked) continue;

                int rowIndex = dgvResults.Rows.Add(
                    item.Index,
                    $"{item.Station:F2}",
                    $"{item.Elevation:F2}",
                    item.ItemName,
                    item.ProposedValue,
                    item.StandardRequirement,
                    item.Status == CheckStatus.Pass ? "ĐẠT" : (item.Status == CheckStatus.Warning ? "CẢNH BÁO" : "VI PHẠM"),
                    item.Note
                );

                var row = dgvResults.Rows[rowIndex];
                row.Tag = item;

                if (item.Status == CheckStatus.Pass)
                {
                    passCount++;
                    row.Cells[6].Style.BackColor = Color.LightGreen;
                    row.Cells[6].Style.ForeColor = Color.DarkGreen;
                }
                else if (item.Status == CheckStatus.Warning)
                {
                    warnCount++;
                    row.Cells[6].Style.BackColor = Color.Khaki;
                    row.Cells[6].Style.ForeColor = Color.DarkOrange;
                }
                else
                {
                    failCount++;
                    row.Cells[6].Style.BackColor = Color.MistyRose;
                    row.Cells[6].Style.ForeColor = Color.DarkRed;
                }
            }

            A.Ed.WriteMessage($"\nKiểm tra Profile hoàn tất: {passCount} Đạt, {warnCount} Cảnh báo, {failCount} Vi phạm.");
        }

        private void Filter_CheckedChanged(object sender, EventArgs e)
        {
            btnCheck_Click(sender, e);
        }

        private void btnZoomPVI_Click(object sender, EventArgs e)
        {
            if (dgvResults.CurrentRow == null || dgvResults.CurrentRow.Tag == null)
            {
                MessageBox.Show("Vui lòng chọn dòng trong bảng kết quả để zoom.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var item = dgvResults.CurrentRow.Tag as ProfileCheckItem;
            if (item == null) return;

            if (cbbAlignments.SelectedIndex < 0) return;
            ObjectId alignId = _alignmentIds[cbbAlignments.SelectedIndex];

            using (A.Doc.LockDocument())
            {
                using (var tr = A.Db.TransactionManager.StartTransaction())
                {
                    var align = tr.GetObject(alignId, OpenMode.ForRead) as Alignment;
                    if (align != null)
                    {
                        double x = 0, y = 0;
                        bool locationFound = false;

                        try
                        {
                            double targetStation = item.Station;
                            if (targetStation < align.StartingStation) targetStation = align.StartingStation;
                            if (targetStation > align.EndingStation) targetStation = align.EndingStation;

                            align.PointLocation(targetStation, 0, ref x, ref y);
                            locationFound = true;
                        }
                        catch (System.Exception ex)
                        {
                            MessageBox.Show($"Không thể định vị lý trình {item.Station:F2}m trên tuyến: {ex.Message}", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }

                        if (locationFound)
                        {
                            // Zoom CAD viewport to point (x, y)
                            using (Autodesk.AutoCAD.DatabaseServices.ViewTableRecord view = A.Ed.GetCurrentView())
                            {
                                view.CenterPoint = new Autodesk.AutoCAD.Geometry.Point2d(x, y);
                                view.Height = 100.0; // Zoom height in CAD units
                                view.Width = 150.0;
                                A.Ed.SetCurrentView(view);
                            }
                        }
                    }
                    tr.Commit();
                }
            }
        }

        private void btnDrawErrors_Click(object sender, EventArgs e)
        {
            var failOrWarnItems = _rawResults.Where(r => r.Status == CheckStatus.Fail || r.Status == CheckStatus.Warning).ToList();

            if (failOrWarnItems.Count == 0)
            {
                MessageBox.Show("Không có vị trí vi phạm hoặc cảnh báo nào để đánh dấu!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (cbbAlignments.SelectedIndex < 0) return;
            ObjectId alignId = _alignmentIds[cbbAlignments.SelectedIndex];

            int countMarked = 0;

            using (A.Doc.LockDocument())
            {
                using (var tr = A.Db.TransactionManager.StartTransaction())
                {
                    var align = tr.GetObject(alignId, OpenMode.ForRead) as Alignment;
                    if (align != null)
                    {
                        var btr = (BlockTableRecord)tr.GetObject(A.Db.CurrentSpaceId, OpenMode.ForWrite);

                        foreach (var item in failOrWarnItems)
                        {
                            try
                            {
                                double targetStation = item.Station;
                                if (targetStation < align.StartingStation) targetStation = align.StartingStation;
                                if (targetStation > align.EndingStation) targetStation = align.EndingStation;

                                double x = 0, y = 0;
                                align.PointLocation(targetStation, 0, ref x, ref y);

                                // Màu sắc: Vi phạm = Đỏ (ColorIndex 1), Cảnh báo = Vàng (ColorIndex 2)
                                short colorIndex = (item.Status == CheckStatus.Fail) ? (short)1 : (short)2;

                                // 1. Vẽ vòng tròn ký hiệu tại tọa độ tuyến trên mặt bằng
                                Circle circle = new Circle();
                                circle.Center = new Autodesk.AutoCAD.Geometry.Point3d(x, y, 0);
                                circle.Radius = 15.0; // Bán kính 15m
                                circle.ColorIndex = colorIndex;
                                btr.AppendEntity(circle);
                                tr.AddNewlyCreatedDBObject(circle, true);

                                // 2. Vẽ ký hiệu chữ thập (Crosshair)
                                Line line1 = new Line(new Autodesk.AutoCAD.Geometry.Point3d(x - 25.0, y, 0), new Autodesk.AutoCAD.Geometry.Point3d(x + 25.0, y, 0));
                                line1.ColorIndex = colorIndex;
                                btr.AppendEntity(line1);
                                tr.AddNewlyCreatedDBObject(line1, true);

                                Line line2 = new Line(new Autodesk.AutoCAD.Geometry.Point3d(x, y - 25.0, 0), new Autodesk.AutoCAD.Geometry.Point3d(x, y + 25.0, 0));
                                line2.ColorIndex = colorIndex;
                                btr.AppendEntity(line2);
                                tr.AddNewlyCreatedDBObject(line2, true);

                                // 3. Ghi chú chữ MText thông tin vi phạm
                                MText mtext = new MText();
                                mtext.Location = new Autodesk.AutoCAD.Geometry.Point3d(x + 20.0, y + 20.0, 0);
                                mtext.TextHeight = 5.0;
                                mtext.ColorIndex = colorIndex;
                                string prefix = item.Status == CheckStatus.Fail ? "❌ [VI PHẠM]" : "⚠️ [CẢNH BÁO]";
                                mtext.Contents = $"{prefix} Km{(item.Station / 1000.0):F3}\\n{item.ItemName}\\n{item.ProposedValue}\\nYC: {item.StandardRequirement}";
                                btr.AppendEntity(mtext);
                                tr.AddNewlyCreatedDBObject(mtext, true);

                                countMarked++;
                            }
                            catch { }
                        }
                    }
                    tr.Commit();
                }
            }

            MessageBox.Show($"Đã đánh dấu {countMarked} vị trí vi phạm/cảnh báo trên bản vẽ!", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void btnExportExcel_Click(object sender, EventArgs e)
        {
            if (dgvResults.Rows.Count == 0)
            {
                MessageBox.Show("Không có dữ liệu để xuất.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "CSV File (*.csv)|*.csv|All Files (*.*)|*.*";
                sfd.FileName = $"KetQuaKiemTraProfile_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine("STT,Lý trình (m),Cao độ (m),Hạng mục kiểm tra,Giá trị thiết kế,Yêu cầu tiêu chuẩn,Trạng thái,Ghi chú");

                        foreach (DataGridViewRow row in dgvResults.Rows)
                        {
                            if (!row.IsNewRow)
                            {
                                string stt = row.Cells[0].Value?.ToString() ?? "";
                                string station = row.Cells[1].Value?.ToString() ?? "";
                                string elev = row.Cells[2].Value?.ToString() ?? "";
                                string item = row.Cells[3].Value?.ToString() ?? "";
                                string proposed = row.Cells[4].Value?.ToString() ?? "";
                                string req = row.Cells[5].Value?.ToString() ?? "";
                                string status = row.Cells[6].Value?.ToString() ?? "";
                                string note = row.Cells[7].Value?.ToString() ?? "";

                                sb.AppendLine($"\"{stt}\",\"{station}\",\"{elev}\",\"{item}\",\"{proposed}\",\"{req}\",\"{status}\",\"{note}\"");
                            }
                        }

                        File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                        MessageBox.Show($"Đã xuất file thành công:\n{sfd.FileName}", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Lỗi xuất file: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void dgvResults_SelectionChanged(object sender, EventArgs e)
        {
            // Tự động zoom nhẹ nếu cần
        }
    }
}

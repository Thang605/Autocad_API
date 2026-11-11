using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using MyFirstProject.Civil_Tool;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;

[assembly: CommandClass(typeof(Civil3DCsharp.CTP_DieuChinhDuongDo_Commands))]

namespace Civil3DCsharp
{
    public class CTP_DieuChinhDuongDo_Commands
    {
        // Ghi nhớ đối tượng và PVI đã chọn giữa các lần thực hiện lệnh
        private static ObjectId _lastProfileViewId = ObjectId.Null;
        private static ObjectId _lastProfileId = ObjectId.Null;
        private static int _lastPvi1Index = -1;
        private static int _lastPvi2Index = -1;
        private static int _lastPviCenterIndex = -1;

        [CommandMethod("CTP_Dieu_Chinh_Duong_Do")]
        public void CTP_DieuChinhDuongDo()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            try
            {
                DieuChinhDuongDoForm form = new DieuChinhDuongDoForm();

                // Nạp lại đối tượng đã chọn ở lần thực hiện trước (nếu còn hợp lệ)
                RestoreLastSelectedObjects(db, form);

                // Wire up Pick ProfileView / Profile
                form.btnPickProfileView.Click += (s, e) =>
                {
                    try
                    {
                        using (var interaction = ed.StartUserInteraction(form))
                        {
                            PromptEntityOptions peo = new PromptEntityOptions("\nChọn Profile hoặc Trắc dọc (ProfileView) trên bản vẽ: ");
                            peo.SetRejectMessage("\nĐối tượng chọn phải là Profile hoặc ProfileView!");
                            peo.AddAllowedClass(typeof(ProfileView), true);
                            peo.AddAllowedClass(typeof(Profile), true);

                            PromptEntityResult per = ed.GetEntity(peo);
                            interaction.End();

                            if (per.Status == PromptStatus.OK)
                            {
                                using (Transaction tr = db.TransactionManager.StartTransaction())
                                {
                                    Entity ent = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Entity;
                                    if (ent is ProfileView pv)
                                    {
                                        form.ProfileViewId = pv.ObjectId;
                                        form.txtProfileViewName.Text = pv.Name;

                                        ObjectId alignId = pv.AlignmentId;
                                        if (alignId.IsValid && !alignId.IsNull)
                                        {
                                            Alignment align = tr.GetObject(alignId, OpenMode.ForRead) as Alignment;
                                            if (align != null)
                                            {
                                                LoadProfilesFromAlignment(tr, align, form);
                                            }
                                        }
                                    }
                                    else if (ent is Profile prof)
                                    {
                                        ObjectId alignId = prof.AlignmentId;
                                        if (alignId.IsValid && !alignId.IsNull)
                                        {
                                            Alignment align = tr.GetObject(alignId, OpenMode.ForRead) as Alignment;
                                            if (align != null)
                                            {
                                                LoadProfilesFromAlignment(tr, align, form, prof.ObjectId);
                                                ObjectId pvId = FindProfileViewForAlignment(tr, db, align.ObjectId);
                                                if (pvId.IsValid && !pvId.IsNull)
                                                {
                                                    ProfileView pvObj = tr.GetObject(pvId, OpenMode.ForRead) as ProfileView;
                                                    if (pvObj != null)
                                                    {
                                                        form.ProfileViewId = pvObj.ObjectId;
                                                        form.txtProfileViewName.Text = pvObj.Name;
                                                    }
                                                }
                                            }
                                        }
                                        else
                                        {
                                            form.PopulateProfiles(new List<DieuChinhProfileItem> { new DieuChinhProfileItem(prof.Name, prof.ObjectId) }, prof.ObjectId);
                                        }
                                    }

                                    // Reset PVI selections
                                    form.Pvi1Index = -1;
                                    form.Pvi2Index = -1;
                                    form.PviCenterIndex = -1;
                                    form.UpdatePvi1Display();
                                    form.UpdatePvi2Display();
                                }
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        ed.WriteMessage($"\nLỗi khi chọn đối tượng: {ex.Message}");
                    }
                };

                // Wire up Pick Profile
                form.btnPickProfile.Click += (s, e) =>
                {
                    try
                    {
                        using (var interaction = ed.StartUserInteraction(form))
                        {
                            PromptEntityOptions peo = new PromptEntityOptions("\nChọn Profile trên bản vẽ: ");
                            peo.SetRejectMessage("\nĐối tượng chọn phải là Profile!");
                            peo.AddAllowedClass(typeof(Profile), true);

                            PromptEntityResult per = ed.GetEntity(peo);
                            interaction.End();

                            if (per.Status == PromptStatus.OK)
                            {
                                using (Transaction tr = db.TransactionManager.StartTransaction())
                                {
                                    Profile prof = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Profile;
                                    if (prof != null)
                                    {
                                        ObjectId alignId = prof.AlignmentId;
                                        if (alignId.IsValid && !alignId.IsNull)
                                        {
                                            Alignment align = tr.GetObject(alignId, OpenMode.ForRead) as Alignment;
                                            if (align != null)
                                            {
                                                LoadProfilesFromAlignment(tr, align, form, prof.ObjectId);
                                                if (form.ProfileViewId.IsNull || !form.ProfileViewId.IsValid)
                                                {
                                                    ObjectId pvId = FindProfileViewForAlignment(tr, db, align.ObjectId);
                                                    if (pvId.IsValid && !pvId.IsNull)
                                                    {
                                                        ProfileView pvObj = tr.GetObject(pvId, OpenMode.ForRead) as ProfileView;
                                                        if (pvObj != null)
                                                        {
                                                            form.ProfileViewId = pvObj.ObjectId;
                                                            form.txtProfileViewName.Text = pvObj.Name;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        else
                                        {
                                            form.PopulateProfiles(new List<DieuChinhProfileItem> { new DieuChinhProfileItem(prof.Name, prof.ObjectId) }, prof.ObjectId);
                                        }

                                        // Reset PVI selections
                                        form.Pvi1Index = -1;
                                        form.Pvi2Index = -1;
                                        form.PviCenterIndex = -1;
                                        form.UpdatePvi1Display();
                                        form.UpdatePvi2Display();
                                    }
                                }
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        ed.WriteMessage($"\nLỗi khi chọn Profile: {ex.Message}");
                    }
                };

                // Nút pick 1 điểm trên đoạn Profile để lấy 2 điểm PVI (PVI đầu & PVI sau) - TAB 1
                form.btnPickSegment.Click += (s, e) =>
                {
                    if (form.ProfileId.IsNull || !form.ProfileId.IsValid)
                    {
                        MessageBox.Show("Vui lòng chọn Trắc dọc / Profile trước!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    try
                    {
                        using (var interaction = ed.StartUserInteraction(form))
                        {
                            PromptPointOptions ppo = new PromptPointOptions("\nPick 1 điểm trên đoạn Profile để lấy 2 đỉnh PVI: ");
                            PromptPointResult ppr = ed.GetPoint(ppo);
                            interaction.End();

                            if (ppr.Status == PromptStatus.OK)
                            {
                                bool success = FindSegmentPvis(db, form.ProfileId, form.ProfileViewId, ppr.Value, 
                                    out int idx1, out double sta1, out double elev1,
                                    out int idx2, out double sta2, out double elev2);

                                if (success)
                                {
                                    form.Pvi1Index = idx1;
                                    form.Pvi1Station = sta1;
                                    form.Pvi1Elevation = elev1;
                                    form.UpdatePvi1Display();

                                    form.Pvi2Index = idx2;
                                    form.Pvi2Station = sta2;
                                    form.Pvi2Elevation = elev2;
                                    form.UpdatePvi2Display();

                                    // Auto compute slope and distance
                                    double dist = Math.Abs(sta2 - sta1);
                                    if (dist > 0.0001)
                                    {
                                        double slope = ((elev2 - elev1) / (sta2 - sta1)) * 100.0;
                                        form.numSlope.Value = (decimal)Math.Round(slope, 4);
                                        form.numDistance.Value = (decimal)Math.Round(dist, 3);
                                    }

                                    ed.WriteMessage($"\n✅ Đã chọn đoạn Profile: PVI #{idx1} (Sta {sta1:F2}m) -> PVI #{idx2} (Sta {sta2:F2}m)");
                                }
                                else
                                {
                                    ed.WriteMessage("\n⚠️ Không tìm thấy đoạn PVI hợp lệ tại điểm pick.");
                                }
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        ed.WriteMessage($"\nLỗi khi pick đoạn Profile: {ex.Message}");
                    }
                };

                // Wire up Pick PVI 1 - TAB 1
                form.btnPickPvi1.Click += (s, e) =>
                {
                    if (form.ProfileId.IsNull || !form.ProfileId.IsValid)
                    {
                        MessageBox.Show("Vui lòng chọn Trắc dọc / Profile trước!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    try
                    {
                        using (var interaction = ed.StartUserInteraction(form))
                        {
                            string promptMsg = form.IsFixPvi1
                                ? "\nPick vị trí PVI 1 (cố định) trên trắc dọc: "
                                : "\nPick vị trí PVI 1 (thay đổi) trên trắc dọc: ";
                            PromptPointOptions ppo = new PromptPointOptions(promptMsg);
                            PromptPointResult ppr = ed.GetPoint(ppo);
                            interaction.End();

                            if (ppr.Status == PromptStatus.OK)
                            {
                                int nearestPvi = FindNearestPviIndex(db, form.ProfileId, form.ProfileViewId, ppr.Value, out double sta, out double elev);
                                if (nearestPvi >= 0)
                                {
                                    form.Pvi1Index = nearestPvi;
                                    form.Pvi1Station = sta;
                                    form.Pvi1Elevation = elev;
                                    form.UpdatePvi1Display();
                                    ed.WriteMessage($"\n✅ Đã chọn PVI 1: Index {nearestPvi}, Lý trình: {sta:F2}m, Cao độ: {elev:F3}m");

                                    // Nếu PVI 2 đã chọn -> auto cập nhật L và i
                                    if (form.Pvi2Index >= 0 && Math.Abs(form.Pvi2Station - sta) > 0.0001)
                                    {
                                        double dist = Math.Abs(form.Pvi2Station - sta);
                                        double slope = ((form.Pvi2Elevation - elev) / (form.Pvi2Station - sta)) * 100.0;
                                        form.numDistance.Value = (decimal)Math.Round(dist, 3);
                                        form.numSlope.Value = (decimal)Math.Round(slope, 4);
                                    }
                                }
                                else
                                {
                                    ed.WriteMessage("\n⚠️ Không tìm thấy PVI tương ứng.");
                                }
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        ed.WriteMessage($"\nLỗi khi pick PVI 1: {ex.Message}");
                    }
                };

                // Wire up Pick PVI 2 - TAB 1
                form.btnPickPvi2.Click += (s, e) =>
                {
                    if (form.ProfileId.IsNull || !form.ProfileId.IsValid)
                    {
                        MessageBox.Show("Vui lòng chọn Trắc dọc / Profile trước!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    try
                    {
                        using (var interaction = ed.StartUserInteraction(form))
                        {
                            string promptMsg = form.IsFixPvi1
                                ? "\nPick vị trí PVI 2 (thay đổi) trên trắc dọc: "
                                : "\nPick vị trí PVI 2 (cố định) trên trắc dọc: ";
                            PromptPointOptions ppo = new PromptPointOptions(promptMsg);
                            PromptPointResult ppr = ed.GetPoint(ppo);
                            interaction.End();

                            if (ppr.Status == PromptStatus.OK)
                            {
                                int nearestPvi = FindNearestPviIndex(db, form.ProfileId, form.ProfileViewId, ppr.Value, out double sta, out double elev);
                                if (nearestPvi >= 0)
                                {
                                    form.Pvi2Index = nearestPvi;
                                    form.Pvi2Station = sta;
                                    form.Pvi2Elevation = elev;
                                    form.UpdatePvi2Display();
                                    ed.WriteMessage($"\n✅ Đã chọn PVI 2: Index {nearestPvi}, Lý trình: {sta:F2}m, Cao độ: {elev:F3}m");

                                    // Auto cập nhật L và i từ PVI 1 đến PVI 2
                                    if (form.Pvi1Index >= 0 && Math.Abs(sta - form.Pvi1Station) > 0.0001)
                                    {
                                        double dist = Math.Abs(sta - form.Pvi1Station);
                                        double slope = ((elev - form.Pvi1Elevation) / (sta - form.Pvi1Station)) * 100.0;
                                        form.numDistance.Value = (decimal)Math.Round(dist, 3);
                                        form.numSlope.Value = (decimal)Math.Round(slope, 4);
                                    }
                                }
                                else
                                {
                                    ed.WriteMessage("\n⚠️ Không tìm thấy PVI tương ứng.");
                                }
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        ed.WriteMessage($"\nLỗi khi pick PVI 2: {ex.Message}");
                    }
                };

                // Nút Pick điểm để tính cả Khoảng cách L và Dốc i - TAB 1
                form.btnPickPoint2.Click += (s, e) =>
                {
                    if (form.ProfileId.IsNull || !form.ProfileId.IsValid)
                    {
                        MessageBox.Show("Vui lòng chọn Trắc dọc / Profile trước!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    if (form.IsFixPvi1 && form.Pvi1Index < 0)
                    {
                        MessageBox.Show("Vui lòng chọn PVI 1 (cố định) trước!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    else if (!form.IsFixPvi1 && form.Pvi2Index < 0)
                    {
                        MessageBox.Show("Vui lòng chọn PVI 2 (cố định) trước!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    try
                    {
                        using (var interaction = ed.StartUserInteraction(form))
                        {
                            string promptMsg = form.IsFixPvi1
                                ? "\nPick điểm thứ 2 trên trắc dọc để tính khoảng cách L và dốc i từ PVI 1: "
                                : "\nPick điểm thứ 1 trên trắc dọc để tính khoảng cách L và dốc i đến PVI 2: ";
                            PromptPointOptions ppo = new PromptPointOptions(promptMsg);
                            PromptPointResult ppr = ed.GetPoint(ppo);
                            interaction.End();

                            if (ppr.Status == PromptStatus.OK)
                            {
                                Point3d ptPicked = ppr.Value;
                                double sPick = ptPicked.X;
                                double ePick = ptPicked.Y;

                                if (!form.ProfileViewId.IsNull && form.ProfileViewId.IsValid)
                                {
                                    using (Transaction tr = db.TransactionManager.StartTransaction())
                                    {
                                        ProfileView pv = tr.GetObject(form.ProfileViewId, OpenMode.ForWrite) as ProfileView;
                                        if (pv != null)
                                        {
                                            pv.FindStationAndElevationAtXY(ptPicked.X, ptPicked.Y, ref sPick, ref ePick);
                                        }
                                    }
                                }

                                double s1, e1, s2, e2;
                                if (form.IsFixPvi1)
                                {
                                    s1 = form.Pvi1Station;
                                    e1 = form.Pvi1Elevation;
                                    s2 = sPick;
                                    e2 = ePick;
                                }
                                else
                                {
                                    s1 = sPick;
                                    e1 = ePick;
                                    s2 = form.Pvi2Station;
                                    e2 = form.Pvi2Elevation;
                                }

                                double dist = Math.Abs(s2 - s1);
                                double slope = (Math.Abs(s2 - s1) > 0.0001) ? ((e2 - e1) / (s2 - s1)) * 100.0 : 0.0;

                                form.numDistance.Value = (decimal)Math.Round(dist, 3);
                                form.numSlope.Value = (decimal)Math.Round(slope, 4);

                                ed.WriteMessage($"\n✅ Đã tính: PVI 1 ({s1:F2}m, {e1:F3}m) -> PVI 2 ({s2:F2}m, {e2:F3}m): Khoảng cách L = {dist:F3}m, Dốc i = {slope:F4}%");
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        ed.WriteMessage($"\nLỗi khi pick điểm: {ex.Message}");
                    }
                };

                // Wire up Pick PVI trung tâm (tự động lấy PVI trước & sau) - TAB 2
                form.btnPickCenterPvi.Click += (s, e) =>
                {
                    if (form.ProfileId.IsNull || !form.ProfileId.IsValid)
                    {
                        MessageBox.Show("Vui lòng chọn Trắc dọc / Profile trước!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    try
                    {
                        using (var interaction = ed.StartUserInteraction(form))
                        {
                            PromptPointOptions ppo = new PromptPointOptions("\nPick vị trí đỉnh PVI (đỉnh trung tâm) trên trắc dọc: ");
                            PromptPointResult ppr = ed.GetPoint(ppo);
                            interaction.End();

                            if (ppr.Status == PromptStatus.OK)
                            {
                                int centerIdx = FindNearestPviIndex(db, form.ProfileId, form.ProfileViewId, ppr.Value, out double sta, out double elev);
                                if (centerIdx >= 0)
                                {
                                    using (Transaction tr = db.TransactionManager.StartTransaction())
                                    {
                                        Profile profile = tr.GetObject(form.ProfileId, OpenMode.ForRead) as Profile;
                                        if (profile != null)
                                        {
                                            int totalPvis = profile.PVIs.Count;
                                            if (totalPvis < 3)
                                            {
                                                MessageBox.Show("Profile phải có ít nhất 3 PVI mới có thể điều chỉnh PVI với PVI trước và PVI sau giữ nguyên.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                                return;
                                            }
                                            if (centerIdx == 0)
                                            {
                                                MessageBox.Show($"Đỉnh PVI được chọn là điểm đầu tuyến (#0), không có PVI trước. Vui lòng chọn PVI từ #1 đến #{totalPvis - 2}.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                                return;
                                            }
                                            if (centerIdx == totalPvis - 1)
                                            {
                                                MessageBox.Show($"Đỉnh PVI được chọn là điểm cuối tuyến (#{totalPvis - 1}), không có PVI sau. Vui lòng chọn PVI từ #1 đến #{totalPvis - 2}.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                                return;
                                            }

                                            int prevIdx = centerIdx - 1;
                                            int nextIdx = centerIdx + 1;

                                            ProfilePVI pPrev = profile.PVIs[prevIdx];
                                            ProfilePVI pCenter = profile.PVIs[centerIdx];
                                            ProfilePVI pNext = profile.PVIs[nextIdx];

                                            double sPrev = pPrev.RawStation;
                                            double ePrev = pPrev.Elevation;
                                            double sCenter = pCenter.RawStation;
                                            double eCenter = pCenter.Elevation;
                                            double sNext = pNext.RawStation;
                                            double eNext = pNext.Elevation;

                                            double slopeBefore = (Math.Abs(sCenter - sPrev) > 0.0001) ? ((eCenter - ePrev) / (sCenter - sPrev)) * 100.0 : 0.0;
                                            double slopeAfter = (Math.Abs(sNext - sCenter) > 0.0001) ? ((eNext - eCenter) / (sNext - sCenter)) * 100.0 : 0.0;

                                            form.SetCenterPviData(
                                                centerIdx, sCenter, eCenter,
                                                prevIdx, sPrev, ePrev,
                                                nextIdx, sNext, eNext,
                                                slopeBefore, slopeAfter
                                            );

                                            ed.WriteMessage($"\n✅ Đã chọn PVI trung tâm #{centerIdx} (Sta {sCenter:F2}m). PVI trước #{prevIdx} (Sta {sPrev:F2}m), PVI sau #{nextIdx} (Sta {sNext:F2}m)");
                                        }
                                    }
                                }
                                else
                                {
                                    ed.WriteMessage("\n⚠️ Không tìm thấy PVI tương ứng.");
                                }
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        ed.WriteMessage($"\nLỗi khi pick PVI trung tâm: {ex.Message}");
                    }
                };

                // Hiển thị Dialog
                DialogResult dr = Application.ShowModalDialog(form);

                if (dr == DialogResult.OK && form.FormAccepted)
                {
                    // Lưu lại vết đối tượng và PVI cho lần gọi sau
                    _lastProfileViewId = form.ProfileViewId;
                    _lastProfileId = form.ProfileId;
                    _lastPvi1Index = form.Pvi1Index;
                    _lastPvi2Index = form.Pvi2Index;
                    _lastPviCenterIndex = form.PviCenterIndex;

                    if (form.SelectedTabMode == 1)
                    {
                        ExecuteDieuChinh1Pvi(ed, db, form);
                    }
                    else
                    {
                        ExecuteDieuChinhDuongDo(ed, db, form);
                    }
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n❌ Lỗi thực thi lệnh CTP_DieuChinhDuongDo: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void RestoreLastSelectedObjects(Database db, DieuChinhDuongDoForm form)
        {
            if (_lastProfileId.IsNull || !_lastProfileId.IsValid || _lastProfileId.IsErased) return;

            try
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    Profile profile = tr.GetObject(_lastProfileId, OpenMode.ForRead) as Profile;
                    if (profile != null)
                    {
                        ObjectId alignId = profile.AlignmentId;
                        if (alignId.IsValid && !alignId.IsNull)
                        {
                            Alignment align = tr.GetObject(alignId, OpenMode.ForRead) as Alignment;
                            if (align != null)
                            {
                                LoadProfilesFromAlignment(tr, align, form, _lastProfileId);
                            }
                        }
                        else
                        {
                            form.PopulateProfiles(new List<DieuChinhProfileItem> { new DieuChinhProfileItem(profile.Name, profile.ObjectId) }, profile.ObjectId);
                        }

                        if (!_lastProfileViewId.IsNull && _lastProfileViewId.IsValid && !_lastProfileViewId.IsErased)
                        {
                            ProfileView pv = tr.GetObject(_lastProfileViewId, OpenMode.ForRead) as ProfileView;
                            if (pv != null)
                            {
                                form.ProfileViewId = _lastProfileViewId;
                                form.txtProfileViewName.Text = pv.Name;
                            }
                        }

                        if (_lastPvi1Index >= 0 && _lastPvi1Index < profile.PVIs.Count)
                        {
                            ProfilePVI p1 = profile.PVIs[_lastPvi1Index];
                            form.Pvi1Index = _lastPvi1Index;
                            form.Pvi1Station = p1.RawStation;
                            form.Pvi1Elevation = p1.Elevation;
                            form.UpdatePvi1Display();
                        }

                        if (_lastPvi2Index >= 0 && _lastPvi2Index < profile.PVIs.Count)
                        {
                            ProfilePVI p2 = profile.PVIs[_lastPvi2Index];
                            form.Pvi2Index = _lastPvi2Index;
                            form.Pvi2Station = p2.RawStation;
                            form.Pvi2Elevation = p2.Elevation;
                            form.UpdatePvi2Display();
                        }

                        if (_lastPviCenterIndex > 0 && _lastPviCenterIndex < profile.PVIs.Count - 1)
                        {
                            int cIdx = _lastPviCenterIndex;
                            int pIdx = cIdx - 1;
                            int nIdx = cIdx + 1;

                            ProfilePVI pP = profile.PVIs[pIdx];
                            ProfilePVI pC = profile.PVIs[cIdx];
                            ProfilePVI pN = profile.PVIs[nIdx];

                            double sP = pP.RawStation; double eP = pP.Elevation;
                            double sC = pC.RawStation; double eC = pC.Elevation;
                            double sN = pN.RawStation; double eN = pN.Elevation;

                            double sBefore = (Math.Abs(sC - sP) > 0.0001) ? ((eC - eP) / (sC - sP)) * 100.0 : 0.0;
                            double sAfter = (Math.Abs(sN - sC) > 0.0001) ? ((eN - eC) / (sN - sC)) * 100.0 : 0.0;

                            form.SetCenterPviData(cIdx, sC, eC, pIdx, sP, eP, nIdx, sN, eN, sBefore, sAfter);
                        }
                    }
                }
            }
            catch { }
        }

        private void LoadProfilesFromAlignment(Transaction tr, Alignment align, DieuChinhDuongDoForm form, ObjectId selectedProfId = default)
        {
            var list = new List<DieuChinhProfileItem>();
            ObjectId defaultId = ObjectId.Null;

            foreach (ObjectId profId in align.GetProfileIds())
            {
                Profile p = tr.GetObject(profId, OpenMode.ForRead) as Profile;
                if (p != null)
                {
                    string displayName = p.Name;
                    if (p.ProfileType == ProfileType.FG) displayName += " [Đường đỏ]";
                    else if (p.ProfileType == ProfileType.EG) displayName += " [Tự nhiên]";

                    list.Add(new DieuChinhProfileItem(displayName, profId));
                    if (p.ProfileType == ProfileType.FG && defaultId.IsNull)
                    {
                        defaultId = profId;
                    }
                }
            }

            if (selectedProfId.IsValid && !selectedProfId.IsNull && list.Any(x => x.Id == selectedProfId))
            {
                defaultId = selectedProfId;
            }
            else if (defaultId.IsNull && list.Count > 0)
            {
                defaultId = list[0].Id;
            }

            form.PopulateProfiles(list, defaultId);
        }

        private ObjectId FindProfileViewForAlignment(Transaction tr, Database db, ObjectId alignId)
        {
            BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
            BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;

            var profileViewClass = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(ProfileView));
            foreach (ObjectId id in btr)
            {
                if (id.ObjectClass.IsDerivedFrom(profileViewClass))
                {
                    ProfileView pv = tr.GetObject(id, OpenMode.ForRead) as ProfileView;
                    if (pv != null && pv.AlignmentId == alignId)
                    {
                        return pv.ObjectId;
                    }
                }
            }
            return ObjectId.Null;
        }

        private bool FindSegmentPvis(Database db, ObjectId profileId, ObjectId profileViewId, Point3d pt,
            out int idx1, out double sta1, out double elev1,
            out int idx2, out double sta2, out double elev2)
        {
            idx1 = -1; sta1 = 0; elev1 = 0;
            idx2 = -1; sta2 = 0; elev2 = 0;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                Profile profile = tr.GetObject(profileId, OpenMode.ForWrite) as Profile;
                if (profile == null || profile.PVIs.Count < 2) return false;

                double searchStation = pt.X;
                double searchElev = pt.Y;

                if (!profileViewId.IsNull && profileViewId.IsValid)
                {
                    ProfileView pv = tr.GetObject(profileViewId, OpenMode.ForWrite) as ProfileView;
                    if (pv != null)
                    {
                        pv.FindStationAndElevationAtXY(pt.X, pt.Y, ref searchStation, ref searchElev);
                    }
                }

                int count = profile.PVIs.Count;

                // Tìm đoạn PVI [i, i+1] chứa searchStation
                if (searchStation <= profile.PVIs[0].RawStation)
                {
                    idx1 = 0;
                    idx2 = 1;
                }
                else if (searchStation >= profile.PVIs[count - 1].RawStation)
                {
                    idx1 = count - 2;
                    idx2 = count - 1;
                }
                else
                {
                    for (int i = 0; i < count - 1; i++)
                    {
                        double sCurrent = profile.PVIs[i].RawStation;
                        double sNext = profile.PVIs[i + 1].RawStation;

                        if (searchStation >= sCurrent && searchStation <= sNext)
                        {
                            idx1 = i;
                            idx2 = i + 1;
                            break;
                        }
                    }
                }

                if (idx1 >= 0 && idx2 >= 0)
                {
                    ProfilePVI p1 = profile.PVIs[idx1];
                    ProfilePVI p2 = profile.PVIs[idx2];
                    sta1 = p1.RawStation; elev1 = p1.Elevation;
                    sta2 = p2.RawStation; elev2 = p2.Elevation;
                    return true;
                }

                return false;
            }
        }

        private int FindNearestPviIndex(Database db, ObjectId profileId, ObjectId profileViewId, Point3d pt, out double station, out double elevation)
        {
            station = 0.0;
            elevation = 0.0;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                Profile profile = tr.GetObject(profileId, OpenMode.ForWrite) as Profile;
                if (profile == null || profile.PVIs.Count == 0) return -1;

                double searchStation = pt.X;
                double searchElev = pt.Y;

                if (!profileViewId.IsNull && profileViewId.IsValid)
                {
                    ProfileView pv = tr.GetObject(profileViewId, OpenMode.ForWrite) as ProfileView;
                    if (pv != null)
                    {
                        pv.FindStationAndElevationAtXY(pt.X, pt.Y, ref searchStation, ref searchElev);
                    }
                }

                int bestIdx = -1;
                double minDeltaSta = double.MaxValue;

                for (int i = 0; i < profile.PVIs.Count; i++)
                {
                    ProfilePVI pvi = profile.PVIs[i];
                    double delta = Math.Abs(pvi.RawStation - searchStation);
                    if (delta < minDeltaSta)
                    {
                        minDeltaSta = delta;
                        bestIdx = i;
                        station = pvi.RawStation;
                        elevation = pvi.Elevation;
                    }
                }

                return bestIdx;
            }
        }

        private void ExecuteDieuChinh1Pvi(Editor ed, Database db, DieuChinhDuongDoForm form)
        {
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                Profile profile = tr.GetObject(form.ProfileId, OpenMode.ForWrite) as Profile;
                if (profile == null)
                {
                    ed.WriteMessage("\n❌ Lỗi: Không thể tìm thấy đối tượng Profile.");
                    return;
                }

                int k = form.PviCenterIndex;
                int kPrev = form.PviPrevIndex;
                int kNext = form.PviNextIndex;

                if (k <= 0 || k >= profile.PVIs.Count - 1 || kPrev != k - 1 || kNext != k + 1)
                {
                    ed.WriteMessage("\n❌ Lỗi: Chỉ số PVI không hợp lệ trong Profile.");
                    return;
                }

                ProfilePVI pviPrev = profile.PVIs[kPrev];
                ProfilePVI pviCenter = profile.PVIs[k];
                ProfilePVI pviNext = profile.PVIs[kNext];

                double sPrev = pviPrev.RawStation;
                double ePrev = pviPrev.Elevation;
                double sNext = pviNext.RawStation;
                double eNext = pviNext.Elevation;

                double oldSta = pviCenter.RawStation;
                double oldElev = pviCenter.Elevation;

                double i1 = form.SlopeBefore;
                double i2 = form.SlopeAfter;

                double newSta = oldSta;
                double newElev = oldElev;

                if (form.CenterCalcMode == 0) // Giao điểm 2 dốc
                {
                    if (Math.Abs(i1 - i2) < 0.00001)
                    {
                        ed.WriteMessage("\n❌ Lỗi: Dốc cánh trước và sau bằng nhau (song song), không thể tính giao điểm PVI.");
                        return;
                    }
                    newSta = (100.0 * (eNext - ePrev) - i2 * sNext + i1 * sPrev) / (i1 - i2);
                    newElev = ePrev + (i1 / 100.0) * (newSta - sPrev);
                }
                else if (form.CenterCalcMode == 1) // Cố định lý trình theo dốc trước
                {
                    newSta = oldSta;
                    newElev = ePrev + (i1 / 100.0) * (newSta - sPrev);
                }
                else if (form.CenterCalcMode == 2) // Cố định lý trình theo dốc sau
                {
                    newSta = oldSta;
                    newElev = eNext - (i2 / 100.0) * (sNext - newSta);
                }

                try
                {
                    pviCenter.RawStation = newSta;
                    pviCenter.Elevation = newElev;
                }
                catch
                {
                    profile.PVIs.RemoveAt(k);
                    profile.PVIs.AddPVI(newSta, newElev);
                }

                tr.Commit();

                double finalSlopeBefore = (Math.Abs(newSta - sPrev) > 0.0001) ? ((newElev - ePrev) / (newSta - sPrev)) * 100.0 : 0.0;
                double finalSlopeAfter = (Math.Abs(sNext - newSta) > 0.0001) ? ((eNext - newElev) / (sNext - newSta)) * 100.0 : 0.0;

                ed.WriteMessage("\n==========================================");
                ed.WriteMessage($"\n🎉 ĐÃ ĐIỀU CHỈNH 1 PVI TRUNG TÂM: {profile.Name}");
                ed.WriteMessage($"\n - PVI trước giữ nguyên (#{kPrev}): Sta = {sPrev:F2}m | Elev = {ePrev:F3}m");
                ed.WriteMessage($"\n - PVI sau giữ nguyên (#{kNext}): Sta = {sNext:F2}m | Elev = {eNext:F3}m");
                ed.WriteMessage($"\n - PVI chọn trước điều chỉnh (#{k}): Sta = {oldSta:F2}m | Elev = {oldElev:F3}m");
                ed.WriteMessage($"\n - PVI chọn sau điều chỉnh (#{k}): Sta = {newSta:F2}m | Elev = {newElev:F3}m (ΔS = {newSta - oldSta:+0.00;-0.00;0.00}m, ΔZ = {newElev - oldElev:+0.000;-0.000;0.000}m)");
                ed.WriteMessage($"\n - Dốc cánh trước mới (PVI {kPrev} -> {k}): {finalSlopeBefore:F4}%");
                ed.WriteMessage($"\n - Dốc cánh sau mới (PVI {k} -> {kNext}): {finalSlopeAfter:F4}%");
                ed.WriteMessage("\n==========================================");
            }
        }

        private void ExecuteDieuChinhDuongDo(Editor ed, Database db, DieuChinhDuongDoForm form)
        {
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                Profile profile = tr.GetObject(form.ProfileId, OpenMode.ForWrite) as Profile;
                if (profile == null)
                {
                    ed.WriteMessage("\n❌ Lỗi: Không thể tìm thấy đối tượng Profile.");
                    return;
                }

                int idx1 = form.Pvi1Index;
                int idx2 = form.Pvi2Index;

                if (idx1 < 0 || idx1 >= profile.PVIs.Count || idx2 < 0 || idx2 >= profile.PVIs.Count)
                {
                    ed.WriteMessage("\n❌ Lỗi: Chỉ số PVI không hợp lệ trong Profile.");
                    return;
                }

                ProfilePVI pvi1 = profile.PVIs[idx1];
                ProfilePVI pvi2 = profile.PVIs[idx2];

                double sta1 = pvi1.RawStation;
                double elev1 = pvi1.Elevation;

                double sta2 = pvi2.RawStation;
                double elev2 = pvi2.Elevation;

                double newDist = form.NewDistance;
                double slope = form.SlopePercent;
                int shiftedCount = 0;

                if (form.IsFixPvi1)
                {
                    // Cố định PVI 1, điều chỉnh PVI 2
                    double oldSta2 = sta2;
                    double oldElev2 = elev2;

                    int direction = (idx2 >= idx1) ? 1 : -1;
                    double newSta2 = sta1 + direction * newDist;
                    double newElev2 = elev1 + direction * (slope / 100.0) * newDist;
                    double deltaSta = newSta2 - oldSta2;

                    // Cập nhật vị trí PVI 2
                    try
                    {
                        pvi2.RawStation = newSta2;
                        pvi2.Elevation = newElev2;
                    }
                    catch
                    {
                        profile.PVIs.RemoveAt(idx2);
                        profile.PVIs.AddPVI(newSta2, newElev2);
                    }

                    // Tịnh tiến các PVI phía sau PVI 2 nếu được bật
                    if (form.ShiftSubsequent && Math.Abs(deltaSta) > 0.0001)
                    {
                        if (direction > 0)
                        {
                            for (int k = idx2 + 1; k < profile.PVIs.Count; k++)
                            {
                                ProfilePVI subPvi = profile.PVIs[k];
                                try
                                {
                                    subPvi.RawStation = subPvi.RawStation + deltaSta;
                                    shiftedCount++;
                                }
                                catch { }
                            }
                        }
                        else
                        {
                            for (int k = idx2 - 1; k >= 0; k--)
                            {
                                ProfilePVI subPvi = profile.PVIs[k];
                                try
                                {
                                    subPvi.RawStation = subPvi.RawStation + deltaSta;
                                    shiftedCount++;
                                }
                                catch { }
                            }
                        }
                    }

                    tr.Commit();

                    ed.WriteMessage("\n==========================================");
                    ed.WriteMessage($"\n🎉 ĐÃ ĐIỀU CHỈNH ĐƯỜNG ĐỎ: {profile.Name}");
                    ed.WriteMessage($"\n - PVI 1 cố định (#{idx1}): Sta = {sta1:F2}m | Elev = {elev1:F3}m");
                    ed.WriteMessage($"\n - PVI 2 trước điều chỉnh (#{idx2}): Sta = {oldSta2:F2}m | Elev = {oldElev2:F3}m");
                    ed.WriteMessage($"\n - PVI 2 sau điều chỉnh (#{idx2}): Sta = {newSta2:F2}m | Elev = {newElev2:F3}m");
                    ed.WriteMessage($"\n - Dốc thiết lập (PVI 1 -> PVI 2): {slope:F4}%");
                    ed.WriteMessage($"\n - Khoảng cách mới PVI 1-2: {newDist:F3}m");
                    if (form.ShiftSubsequent)
                    {
                        ed.WriteMessage($"\n - Đã tịnh tiến {shiftedCount} PVI phía sau theo ΔS = {deltaSta:F3}m");
                    }
                    ed.WriteMessage("\n==========================================");
                }
                else
                {
                    // Cố định PVI 2, điều chỉnh PVI 1
                    double oldSta1 = sta1;
                    double oldElev1 = elev1;

                    int direction = (idx1 >= idx2) ? 1 : -1;
                    double newSta1 = sta2 + direction * newDist;
                    double newElev1 = elev2 + direction * (slope / 100.0) * newDist;
                    double deltaSta = newSta1 - oldSta1;

                    // Cập nhật vị trí PVI 1
                    try
                    {
                        pvi1.RawStation = newSta1;
                        pvi1.Elevation = newElev1;
                    }
                    catch
                    {
                        profile.PVIs.RemoveAt(idx1);
                        profile.PVIs.AddPVI(newSta1, newElev1);
                    }

                    // Tịnh tiến các PVI phía trước PVI 1 nếu được bật
                    if (form.ShiftSubsequent && Math.Abs(deltaSta) > 0.0001)
                    {
                        if (direction < 0)
                        {
                            // PVI 1 nằm trước PVI 2 -> tịnh tiến các PVI phía trước PVI 1 (k < idx1)
                            for (int k = idx1 - 1; k >= 0; k--)
                            {
                                ProfilePVI subPvi = profile.PVIs[k];
                                try
                                {
                                    subPvi.RawStation = subPvi.RawStation + deltaSta;
                                    shiftedCount++;
                                }
                                catch { }
                            }
                        }
                        else
                        {
                            // PVI 1 nằm sau PVI 2 -> tịnh tiến các PVI phía sau PVI 1 (k > idx1)
                            for (int k = idx1 + 1; k < profile.PVIs.Count; k++)
                            {
                                ProfilePVI subPvi = profile.PVIs[k];
                                try
                                {
                                    subPvi.RawStation = subPvi.RawStation + deltaSta;
                                    shiftedCount++;
                                }
                                catch { }
                            }
                        }
                    }

                    tr.Commit();

                    ed.WriteMessage("\n==========================================");
                    ed.WriteMessage($"\n🎉 ĐÃ ĐIỀU CHỈNH ĐƯỜNG ĐỎ: {profile.Name}");
                    ed.WriteMessage($"\n - PVI 2 cố định (#{idx2}): Sta = {sta2:F2}m | Elev = {elev2:F3}m");
                    ed.WriteMessage($"\n - PVI 1 trước điều chỉnh (#{idx1}): Sta = {oldSta1:F2}m | Elev = {oldElev1:F3}m");
                    ed.WriteMessage($"\n - PVI 1 sau điều chỉnh (#{idx1}): Sta = {newSta1:F2}m | Elev = {newElev1:F3}m");
                    ed.WriteMessage($"\n - Dốc thiết lập (PVI 1 -> PVI 2): {slope:F4}%");
                    ed.WriteMessage($"\n - Khoảng cách mới PVI 1-2: {newDist:F3}m");
                    if (form.ShiftSubsequent)
                    {
                        ed.WriteMessage($"\n - Đã tịnh tiến {shiftedCount} PVI phía trước/liên quan theo ΔS = {deltaSta:F3}m");
                    }
                    ed.WriteMessage("\n==========================================");
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using MyFirstProject.Civil_Tool;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;

[assembly: CommandClass(typeof(Civil3DCsharp.CTP_BoTri_CongDung_Commands))]

namespace Civil3DCsharp
{
    public class CTP_BoTri_CongDung_Commands
    {
        private static ObjectId _lastProfileViewId = ObjectId.Null;
        private static ObjectId _lastProfileId = ObjectId.Null;
        private static int _lastPviIndex = 1;
        private static int _lastStandardIndex = 0;
        private static int _lastVtkIndex = 2;
        private static int _lastTerrainIndex = 0;

        [CommandMethod("CTP_BoTri_CongDung")]
        public void CTP_BoTri_CongDung()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            try
            {
                BoTriCongDungForm form = new BoTriCongDungForm();

                form.StandardIndex = _lastStandardIndex;
                form.VtkIndex = _lastVtkIndex;
                form.TerrainIndex = _lastTerrainIndex;

                RestoreLastSelectedPvi(db, form);

                // Wire up instant Apply without closing form
                form.OnApplyClicked += (s, e) =>
                {
                    _lastProfileViewId = form.ProfileViewId;
                    _lastProfileId = form.ProfileId;
                    _lastPviIndex = form.PviIndex;
                    _lastStandardIndex = form.StandardIndex;
                    _lastVtkIndex = form.VtkIndex;
                    _lastTerrainIndex = form.TerrainIndex;

                    bool success = ApplyVerticalCurve(ed, db, form);
                    if (success)
                    {
                        try
                        {
                            ed.Regen();
                            ed.UpdateScreen();
                        }
                        catch { }
                    }
                };

                // Wire up Pick PVI Next without closing form
                form.OnPickPviNextClicked += (s, e) =>
                {
                    PickPviFromDrawing(ed, db, form);
                };
                form.OnApplyAllClicked += (s, e) =>
                {
                    _lastStandardIndex = form.StandardIndex;
                    _lastVtkIndex = form.VtkIndex;
                    _lastTerrainIndex = form.TerrainIndex;

                    var result = ApplyVerticalCurvesToAllPvis(ed, db, form);
                    form.SetApplyAllResult(result.SuccessCount, result.FailureCount,
                        result.SkippedCount, result.MinLengthCount);

                    if (result.SuccessCount > 0)
                    {
                        try
                        {
                            ed.Regen();
                            ed.UpdateScreen();
                        }
                        catch { }
                    }
                };

                // Wire up Pick ProfileView
                form.BtnPickProfileView.Click += (s, e) =>
                {
                    try
                    {
                        using (var interaction = ed.StartUserInteraction(form))
                        {
                            PromptEntityOptions peo = new PromptEntityOptions("\nChọn ProfileView (trắc dọc) hoặc Profile trên bản vẽ: ");
                            peo.SetRejectMessage("\nĐối tượng chọn phải là ProfileView hoặc Profile!");
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
                                        form.TxtProfileViewName.Text = pv.Name;

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
                                                        form.TxtProfileViewName.Text = pvObj.Name;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        ed.WriteMessage($"\nLỗi khi chọn ProfileView: {ex.Message}");
                    }
                };

                // Wire up Pick Profile
                form.BtnPickProfile.Click += (s, e) =>
                {
                    try
                    {
                        using (var interaction = ed.StartUserInteraction(form))
                        {
                            PromptEntityOptions peo = new PromptEntityOptions("\nChọn Profile (đường đỏ) trên bản vẽ: ");
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
                                                            form.TxtProfileViewName.Text = pvObj.Name;
                                                        }
                                                    }
                                                }
                                            }
                                        }
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

                form.CmbProfile.SelectedIndexChanged += (s, e) =>
                {
                    if (form.CmbProfile.SelectedItem is DieuChinhProfileItem item && item.Id.IsValid)
                    {
                        form.ProfileId = item.Id;
                        using (Transaction tr = db.TransactionManager.StartTransaction())
                        {
                            LoadPvisFromProfile(tr, item.Id, form);
                        }
                    }
                };

                form.CmbPvi.SelectedIndexChanged += (s, e) =>
                {
                    if (form.CmbPvi.SelectedItem is PviComboItem pviItem && form.ProfileId.IsValid)
                    {
                        using (Transaction tr = db.TransactionManager.StartTransaction())
                        {
                            SelectPviByIndex(tr, form.ProfileId, pviItem.Index, form);
                        }
                    }
                };

                form.BtnPickPvi.Click += (s, e) =>
                {
                    PickPviFromDrawing(ed, db, form);
                };

                DialogResult result = Application.ShowModalDialog(form);

                if (result == DialogResult.OK && form.FormAccepted)
                {
                    _lastProfileViewId = form.ProfileViewId;
                    _lastProfileId = form.ProfileId;
                    _lastPviIndex = form.PviIndex;
                    _lastStandardIndex = form.StandardIndex;
                    _lastVtkIndex = form.VtkIndex;
                    _lastTerrainIndex = form.TerrainIndex;

                    bool success = ApplyVerticalCurve(ed, db, form);
                    if (success)
                    {
                        try
                        {
                            ed.Regen();
                            ed.UpdateScreen();
                        }
                        catch { }
                    }
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n❌ Lỗi khi thực thi CTP_BoTri_CongDung: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private bool PickPviFromDrawing(Editor ed, Database db, BoTriCongDungForm form)
        {
            try
            {
                using (var interaction = ed.StartUserInteraction(form))
                {
                    PromptPointOptions ppo = new PromptPointOptions("\nPick đỉnh PVI hoặc điểm trên trắc dọc (ProfileView): ");
                    PromptPointResult ppr = ed.GetPoint(ppo);
                    interaction.End();

                    if (ppr.Status == PromptStatus.OK)
                    {
                        Point3d ptPicked = ppr.Value;

                        bool found = FindPviAtPoint(db, ptPicked, form.ProfileViewId, form.ProfileId,
                            out ObjectId foundPvId, out ObjectId foundProfId, out int foundPviIdx,
                            out string profName, out double sta, out double elev,
                            out double gIn, out double gOut, out double curRadius, out double curLength);

                        if (found)
                        {
                            form.ProfileViewId = foundPvId;
                            form.ProfileId = foundProfId;

                            using (Transaction tr = db.TransactionManager.StartTransaction())
                            {
                                ProfileView pv = tr.GetObject(foundPvId, OpenMode.ForRead) as ProfileView;
                                if (pv != null) form.TxtProfileViewName.Text = pv.Name;

                                Profile prof = tr.GetObject(foundProfId, OpenMode.ForRead) as Profile;
                                if (prof != null)
                                {
                                    ObjectId alignId = prof.AlignmentId;
                                    if (alignId.IsValid && !alignId.IsNull)
                                    {
                                        Alignment align = tr.GetObject(alignId, OpenMode.ForRead) as Alignment;
                                        if (align != null)
                                        {
                                            LoadProfilesFromAlignment(tr, align, form, foundProfId);
                                        }
                                    }
                                    LoadPvisFromProfile(tr, foundProfId, form, foundPviIdx);
                                }
                            }

                            form.SetPviInformation(profName, foundPviIdx, sta, elev, gIn, gOut, curRadius, curLength);

                            ed.WriteMessage($"\n✅ Đã chọn PVI #{foundPviIdx} trên Profile '{profName}': Lý trình = {sta:F2}m, Cao độ = {elev:F3}m, Δi = {Math.Abs(gIn - gOut):F2}%");
                            return true;
                        }
                        else
                        {
                            ed.WriteMessage("\n⚠️ Không tìm thấy PVI tương ứng tại điểm chọn. Vui lòng thử lại!");
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\nLỗi khi chọn PVI: {ex.Message}");
            }

            return false;
        }

        private void LoadProfilesFromAlignment(Transaction tr, Alignment align, BoTriCongDungForm form, ObjectId selectedProfId = default)
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
            if (defaultId.IsValid && !defaultId.IsNull)
            {
                form.ProfileId = defaultId;
                LoadPvisFromProfile(tr, defaultId, form);
            }
        }

        private void LoadPvisFromProfile(Transaction tr, ObjectId profileId, BoTriCongDungForm form, int selectedPviIdx = -1)
        {
            Profile prof = tr.GetObject(profileId, OpenMode.ForRead) as Profile;
            if (prof == null) return;

            int count = prof.PVIs.Count;
            var pviItems = new List<PviComboItem>();
            for (int i = 0; i < count; i++)
            {
                ProfilePVI pvi = prof.PVIs[i];
                bool isEndpoint = (i == 0 || i == count - 1);
                pviItems.Add(new PviComboItem(i, pvi.RawStation, pvi.Elevation, isEndpoint));
            }

            int defaultPviIdx = (selectedPviIdx >= 0 && selectedPviIdx < count) 
                ? selectedPviIdx 
                : (count > 2 ? 1 : 0);

            form.PopulatePvis(pviItems, defaultPviIdx);

            if (count > 0)
            {
                SelectPviByIndex(tr, profileId, defaultPviIdx, form);
            }
        }

        private void SelectPviByIndex(Transaction tr, ObjectId profileId, int pviIndex, BoTriCongDungForm form)
        {
            Profile prof = tr.GetObject(profileId, OpenMode.ForRead) as Profile;
            if (prof == null || pviIndex < 0 || pviIndex >= prof.PVIs.Count) return;

            int count = prof.PVIs.Count;
            ProfilePVI pvi = prof.PVIs[pviIndex];

            double gIn = 0;
            double gOut = 0;

            if (pviIndex > 0)
            {
                try { gIn = pvi.GradeIn * 100.0; } catch { }
            }

            if (pviIndex < count - 1)
            {
                try { gOut = pvi.GradeOut * 100.0; } catch { }
            }

            double curRadius = 0, curLength = 0;
            if (pviIndex > 0 && pviIndex < count - 1)
            {
                try
                {
                    PropertyInfo pCurveType = pvi.GetType().GetProperty("CurveType");
                    if (pCurveType != null)
                    {
                        object curveTypeValue = pCurveType.GetValue(pvi);
                        if (curveTypeValue != null && curveTypeValue.ToString() != "None")
                        {
                            PropertyInfo pLength = pvi.GetType().GetProperty("Length") ?? pvi.GetType().GetProperty("CurveLength");
                            if (pLength != null)
                            {
                                curLength = Convert.ToDouble(pLength.GetValue(pvi));
                                double deltaI = Math.Abs(gIn - gOut);
                                if (deltaI > 0) curRadius = (100.0 * curLength) / deltaI;
                            }
                        }
                    }
                }
                catch { }
            }

            form.SetPviInformation(prof.Name, pviIndex, pvi.RawStation, pvi.Elevation, gIn, gOut, curRadius, curLength);
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

        private bool FindPviAtPoint(Database db, Point3d ptPicked, ObjectId defaultPvId, ObjectId defaultProfId,
            out ObjectId profileViewId, out ObjectId profileId, out int pviIndex,
            out string profileName, out double station, out double elevation,
            out double gradeIn, out double gradeOut, out double curRadius, out double curLength)
        {
            profileViewId = defaultPvId;
            profileId = defaultProfId;
            pviIndex = -1;
            profileName = "";
            station = 0; elevation = 0;
            gradeIn = 0; gradeOut = 0;
            curRadius = 0; curLength = 0;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                if (profileViewId.IsNull || !profileViewId.IsValid)
                {
                    profileViewId = FindProfileViewNearPoint(tr, db, ptPicked);
                }

                ProfileView pv = null;
                if (!profileViewId.IsNull && profileViewId.IsValid)
                {
                    pv = tr.GetObject(profileViewId, OpenMode.ForRead) as ProfileView;
                }

                double searchStation = ptPicked.X;
                double searchElev = ptPicked.Y;

                if (pv != null)
                {
                    pv.FindStationAndElevationAtXY(ptPicked.X, ptPicked.Y, ref searchStation, ref searchElev);
                    if (profileId.IsNull || !profileId.IsValid)
                    {
                        ObjectId alignId = pv.AlignmentId;
                        if (alignId.IsValid && !alignId.IsNull)
                        {
                            Alignment align = tr.GetObject(alignId, OpenMode.ForRead) as Alignment;
                            if (align != null)
                            {
                                foreach (ObjectId pId in align.GetProfileIds())
                                {
                                    Profile p = tr.GetObject(pId, OpenMode.ForRead) as Profile;
                                    if (p != null && p.ProfileType == ProfileType.FG)
                                    {
                                        profileId = pId;
                                        break;
                                    }
                                }
                                if (profileId.IsNull && align.GetProfileIds().Count > 0)
                                {
                                    profileId = align.GetProfileIds()[0];
                                }
                            }
                        }
                    }
                }

                if (profileId.IsNull || !profileId.IsValid) return false;

                Profile profile = tr.GetObject(profileId, OpenMode.ForRead) as Profile;
                if (profile == null || profile.PVIs.Count < 2) return false;

                profileName = profile.Name;

                int bestIdx = -1;
                double minDist = double.MaxValue;

                for (int i = 0; i < profile.PVIs.Count; i++)
                {
                    ProfilePVI pvi = profile.PVIs[i];
                    double dist = Math.Abs(pvi.RawStation - searchStation);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        bestIdx = i;
                    }
                }

                if (bestIdx >= 0)
                {
                    pviIndex = bestIdx;
                    ProfilePVI targetPvi = profile.PVIs[bestIdx];
                    station = targetPvi.RawStation;
                    elevation = targetPvi.Elevation;

                    if (bestIdx > 0)
                    {
                        try { gradeIn = targetPvi.GradeIn * 100.0; } catch { }
                    }
                    if (bestIdx < profile.PVIs.Count - 1)
                    {
                        try { gradeOut = targetPvi.GradeOut * 100.0; } catch { }
                    }

                    try
                    {
                        PropertyInfo pCurveType = targetPvi.GetType().GetProperty("CurveType");
                        if (pCurveType != null)
                        {
                            object curveTypeValue = pCurveType.GetValue(targetPvi);
                            if (curveTypeValue != null && curveTypeValue.ToString() != "None")
                            {
                                PropertyInfo pLength = targetPvi.GetType().GetProperty("Length") ?? targetPvi.GetType().GetProperty("CurveLength");
                                if (pLength != null)
                                {
                                    curLength = Convert.ToDouble(pLength.GetValue(targetPvi));
                                    double deltaI = Math.Abs(gradeIn - gradeOut);
                                    if (deltaI > 0) curRadius = (100.0 * curLength) / deltaI;
                                }
                            }
                        }
                    }
                    catch { }

                    return true;
                }
            }

            return false;
        }

        private ObjectId FindProfileViewNearPoint(Transaction tr, Database db, Point3d pt)
        {
            BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
            BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;

            var profileViewClass = Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(ProfileView));
            foreach (ObjectId id in btr)
            {
                if (id.ObjectClass.IsDerivedFrom(profileViewClass))
                {
                    ProfileView pv = tr.GetObject(id, OpenMode.ForRead) as ProfileView;
                    if (pv != null)
                    {
                        Extents3d ext = pv.GeometricExtents;
                        if (pt.X >= ext.MinPoint.X && pt.X <= ext.MaxPoint.X &&
                            pt.Y >= ext.MinPoint.Y && pt.Y <= ext.MaxPoint.Y)
                        {
                            return pv.ObjectId;
                        }
                    }
                }
            }
            return ObjectId.Null;
        }

        private void RestoreLastSelectedPvi(Database db, BoTriCongDungForm form)
        {
            if (_lastProfileId.IsNull || !_lastProfileId.IsValid || _lastProfileId.IsErased) return;

            try
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    Profile prof = tr.GetObject(_lastProfileId, OpenMode.ForRead) as Profile;
                    if (prof != null)
                    {
                        ObjectId alignId = prof.AlignmentId;
                        if (alignId.IsValid && !alignId.IsNull)
                        {
                            Alignment align = tr.GetObject(alignId, OpenMode.ForRead) as Alignment;
                            if (align != null)
                            {
                                LoadProfilesFromAlignment(tr, align, form, _lastProfileId);
                            }
                        }
                        if (!_lastProfileViewId.IsNull && _lastProfileViewId.IsValid && !_lastProfileViewId.IsErased)
                        {
                            ProfileView pv = tr.GetObject(_lastProfileViewId, OpenMode.ForRead) as ProfileView;
                            if (pv != null)
                            {
                                form.ProfileViewId = _lastProfileViewId;
                                form.TxtProfileViewName.Text = pv.Name;
                            }
                        }
                        
                        int restoreIdx = (_lastPviIndex >= 0 && _lastPviIndex < prof.PVIs.Count) ? _lastPviIndex : 1;
                        LoadPvisFromProfile(tr, _lastProfileId, form, restoreIdx);
                    }
                }
            }
            catch { }
        }

        private bool ApplyVerticalCurve(Editor ed, Database db, BoTriCongDungForm form)
        {
            if (form.ProfileId.IsNull || !form.ProfileId.IsValid || form.PviIndex < 0) return false;

            double targetLength = form.CurveLength;
            double targetRadius = form.Radius;

            if (targetLength <= 0)
            {
                ed.WriteMessage("\n⚠️ Chiều dài đường cong đứng không hợp lệ.");
                return false;
            }

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    Profile profile = tr.GetObject(form.ProfileId, OpenMode.ForWrite) as Profile;
                    if (profile == null || form.PviIndex >= profile.PVIs.Count)
                    {
                        ed.WriteMessage("\n⚠️ Không tìm thấy Profile hoặc PVI index không hợp lệ.");
                        return false;
                    }

                    if (form.PviIndex == 0 || form.PviIndex == profile.PVIs.Count - 1)
                    {
                        ed.WriteMessage("\n⚠️ Đỉnh PVI đầu hoặc cuối tuyến không thể cắm cong đứng.");
                        return false;
                    }

                    ProfilePVI pvi = profile.PVIs[form.PviIndex];
                    if (pvi == null) return false;

                    bool curveSet = SetPviCurveLength(ed, profile, pvi, form.PviIndex, targetLength);

                    if (!curveSet)
                    {
                        ed.WriteMessage($"\n⚠️ Không thể tự động gán đường cong cho PVI #{form.PviIndex}.");
                        return false;
                    }

                    tr.Commit();

                    ed.WriteMessage($"\n✅ Đã bố trí thành công Đường cong đứng tại PVI #{form.PviIndex}: Chiều dài L = {targetLength:F2}m (Bán kính R ≈ {targetRadius:F1}m)");
                    return true;
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage($"\n❌ Lỗi khi cập nhật đường cong đứng: {ex.Message}\n{ex.StackTrace}");
                    return false;
                }
            }
        }

        private (int SuccessCount, int FailureCount, int SkippedCount, int MinLengthCount)
            ApplyVerticalCurvesToAllPvis(Editor ed, Database db, BoTriCongDungForm form)
        {
            int successCount = 0;
            int failureCount = 0;
            int skippedCount = 0;
            int minLengthCount = 0;

            if (form.ProfileId.IsNull || !form.ProfileId.IsValid)
            {
                ed.WriteMessage("\nKhông tìm thấy Profile để áp dụng hàng loạt.");
                return (0, 1, 0, 0);
            }

            ProfileDesignParameters requirements = form.GetCurrentProfileDesignParameters();
            var targets = new List<(int Index, double Station, double Length, double Radius, bool UsesMinLength)>();

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                Profile profile = tr.GetObject(form.ProfileId, OpenMode.ForRead) as Profile;
                if (profile == null || !profile.IsEditable)
                {
                    ed.WriteMessage("\nProfile không tồn tại hoặc không cho phép chỉnh sửa.");
                    return (0, 1, 0, 0);
                }

                for (int i = 1; i < profile.PVIs.Count - 1; i++)
                {
                    ProfilePVI pvi = profile.PVIs[i];
                    double gradeIn;
                    double gradeOut;

                    try
                    {
                        gradeIn = pvi.GradeIn * 100.0;
                        gradeOut = pvi.GradeOut * 100.0;
                    }
                    catch (System.Exception ex)
                    {
                        skippedCount++;
                        ed.WriteMessage($"\nPVI #{i}: bỏ qua vì không đọc được độ dốc ({ex.Message}).");
                        continue;
                    }

                    double algebraicGradeDiff = Math.Abs(gradeIn - gradeOut);
                    if (algebraicGradeDiff <= 1e-6)
                    {
                        skippedCount++;
                        ed.WriteMessage($"\nPVI #{i}: bỏ qua vì hiệu dốc bằng 0.");
                        continue;
                    }

                    bool isConvex = gradeIn > gradeOut;
                    double normalRadius = isConvex
                        ? requirements.MinConvexRadiusNormal
                        : requirements.MinConcaveRadiusNormal;

                    if (normalRadius <= 0)
                    {
                        failureCount++;
                        ed.WriteMessage($"\nPVI #{i}: tiêu chuẩn không cung cấp bán kính thông thường phù hợp.");
                        continue;
                    }

                    double lengthByRadius = normalRadius * algebraicGradeDiff / 100.0;
                    bool usesMinLength = requirements.MinVerticalCurveLength > lengthByRadius;
                    double targetLength = Math.Max(lengthByRadius, requirements.MinVerticalCurveLength);
                    double effectiveRadius = 100.0 * targetLength / algebraicGradeDiff;

                    targets.Add((i, pvi.RawStation, targetLength, effectiveRadius, usesMinLength));
                }
            }

            foreach (var target in targets)
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    try
                    {
                        Profile profile = tr.GetObject(form.ProfileId, OpenMode.ForWrite) as Profile;
                        if (profile == null || target.Index >= profile.PVIs.Count - 1)
                            throw new InvalidOperationException("PVI không còn tồn tại.");

                        ProfilePVI pvi = profile.PVIs[target.Index];
                        if (!SetPviCurveLength(ed, profile, pvi, target.Index, target.Length))
                            throw new InvalidOperationException("Không thể tạo hoặc cập nhật cong đứng.");

                        tr.Commit();
                        successCount++;
                        if (target.UsesMinLength) minLengthCount++;

                        string source = target.UsesMinLength ? "L tối thiểu" : "R thông thường";
                        ed.WriteMessage(
                            $"\nPVI #{target.Index} (Km {target.Station:F2}): L = {target.Length:F2} m, " +
                            $"R = {target.Radius:F1} m [{source}].");
                    }
                    catch (System.Exception ex)
                    {
                        failureCount++;
                        ed.WriteMessage($"\nPVI #{target.Index} (Km {target.Station:F2}): lỗi - {ex.Message}");
                    }
                }
            }

            ed.WriteMessage(
                $"\nHoàn tất áp cong đứng hàng loạt: thành công {successCount}, lỗi {failureCount}, " +
                $"bỏ qua {skippedCount}; {minLengthCount} PVI dùng L tối thiểu.");

            return (successCount, failureCount, skippedCount, minLengthCount);
        }
        private List<object> GetAllProfileEntities(object entitiesObj)
        {
            List<object> list = new List<object>();
            if (entitiesObj == null) return list;

            Type type = entitiesObj.GetType();

            // 1. Try IEnumerable / IEnumerable<T>
            if (entitiesObj is System.Collections.IEnumerable en)
            {
                foreach (object item in en)
                {
                    if (item != null) list.Add(item);
                }
                if (list.Count > 0) return list;
            }

            // 2. Try Count + Indexer (Item property)
            try
            {
                PropertyInfo pCount = type.GetProperty("Count");
                PropertyInfo pItem = type.GetProperty("Item");
                if (pCount != null && pItem != null)
                {
                    int count = Convert.ToInt32(pCount.GetValue(entitiesObj));
                    for (int i = 0; i < count; i++)
                    {
                        object item = pItem.GetValue(entitiesObj, new object[] { i });
                        if (item != null) list.Add(item);
                    }
                    if (list.Count > 0) return list;
                }
            }
            catch { }

            // 3. Try GetEnumerator method
            try
            {
                MethodInfo mEnum = type.GetMethod("GetEnumerator");
                if (mEnum != null)
                {
                    System.Collections.IEnumerator enumerator = mEnum.Invoke(entitiesObj, null) as System.Collections.IEnumerator;
                    if (enumerator != null)
                    {
                        while (enumerator.MoveNext())
                        {
                            if (enumerator.Current != null) list.Add(enumerator.Current);
                        }
                    }
                }
            }
            catch { }

            return list;
        }

        private bool SetPviCurveLength(Editor ed, Profile profile, ProfilePVI pvi, int pviIndex, double curveLength)
        {
            double pviStation = pvi.RawStation;
            ed.WriteMessage($"\n[Debug SetPviCurveLength] Target PVI #{pviIndex} Station = {pviStation:F2}m, Target Curve Length = {curveLength:F2}m");

            ProfileEntity existingCurve = null;
            try
            {
                existingCurve = pvi.VerticalCurve;
            }
            catch { }

            if (existingCurve != null)
            {
                existingCurve.Length = curveLength;
                ed.WriteMessage($"\n[Debug] Updated existing {existingCurve.GetType().Name}.Length = {curveLength:F2}m for PVI #{pviIndex}");
                return true;
            }

            profile.Entities.AddFreeSymmetricParabolaByPVIAndCurveLength(pvi, curveLength);
            ed.WriteMessage($"\n[Debug] Added symmetric parabola L = {curveLength:F2}m for PVI #{pviIndex}");
            return true;
        }
    }
}

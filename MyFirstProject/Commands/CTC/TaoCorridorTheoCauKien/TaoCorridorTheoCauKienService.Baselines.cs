using System;
using System.Collections.Generic;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;

using Exception = System.Exception;

namespace MyFirstProject.Commands.CTC.TaoCorridorTheoCauKien
{
    internal sealed class ExistingBaselineSnapshot
    {
        public Dictionary<ObjectId, List<Baseline>> FlBaselines { get; } = new Dictionary<ObjectId, List<Baseline>>();
        public Dictionary<(ObjectId AlignId, ObjectId ProfId), List<Baseline>> AlignBaselines { get; } =
            new Dictionary<(ObjectId, ObjectId), List<Baseline>>();
    }

    internal static partial class TaoCorridorTheoCauKienService
    {
        private static ExistingBaselineSnapshot CaptureExistingBaselineSnapshot(
            Database db, Corridor corridor, Dictionary<string, string> clonedMap)
        {
            var snap = new ExistingBaselineSnapshot();
            var clonedToSource = new Dictionary<ObjectId, ObjectId>();

            foreach (var kv in clonedMap)
            {
                if (TryResolveHandle(db, kv.Key, out ObjectId srcId) &&
                    TryResolveHandle(db, kv.Value, out ObjectId clnId))
                {
                    clonedToSource[clnId] = srcId;
                }
            }

            foreach (Baseline bl in corridor.Baselines)
            {
                if (bl.BaselineRegions.Count == 0)
                    continue;

                if (bl.IsFeatureLineBased())
                {
                    ObjectId flId = bl.FeatureLineId;
                    if (flId.IsNull || !flId.IsValid || flId.IsErased) continue;

                    AddBaselineToMap(snap.FlBaselines, flId, bl);
                    if (clonedToSource.TryGetValue(flId, out ObjectId srcId))
                        AddBaselineToMap(snap.FlBaselines, srcId, bl);
                }
                else
                {
                    var key = (bl.AlignmentId, bl.ProfileId);
                    if (!snap.AlignBaselines.TryGetValue(key, out var list))
                    {
                        list = new List<Baseline>();
                        snap.AlignBaselines[key] = list;
                    }
                    list.Add(bl);
                }
            }

            return snap;
        }

        private static void AddBaselineToMap(Dictionary<ObjectId, List<Baseline>> map, ObjectId id, Baseline bl)
        {
            if (!map.TryGetValue(id, out var list))
            {
                list = new List<Baseline>();
                map[id] = list;
            }
            if (!list.Contains(bl))
                list.Add(bl);
        }

        private static void ProcessComponentRow(
            Transaction tr,
            Database db,
            Editor ed,
            TaoCorridorTheoCauKienOptions options,
            CorridorComponentRowItem row,
            Corridor targetCorridor,
            ExistingBaselineSnapshot snapshot,
            HashSet<ObjectId> handledPreExisting,
            Dictionary<string, string> runtimeClonedMap,
            ref int addedBaselines,
            ref int addedRegions,
            ref int keptExistingRegions)
        {
            if (!row.IsEnabled || row.BaselineEntityIds.Count == 0 || row.AssemblyId.IsNull)
                return;

            foreach (ObjectId entityId in row.BaselineEntityIds)
            {
                if (entityId.IsNull || !entityId.IsValid || entityId.IsErased)
                    continue;

                try
                {
                    if (row.SourceType == ComponentBaselineSourceType.FeatureLine)
                    {
                        ProcessFeatureLineEntity(
                            tr, db, ed, options, row, targetCorridor, entityId,
                            snapshot, handledPreExisting, runtimeClonedMap,
                            ref addedBaselines, ref addedRegions, ref keptExistingRegions);
                    }
                    else
                    {
                        ProcessAlignmentEntity(
                            tr, ed, options, row, targetCorridor, entityId,
                            snapshot, handledPreExisting,
                            ref addedBaselines, ref addedRegions, ref keptExistingRegions);
                    }
                }
                catch (Exception ex)
                {
                    ed.WriteMessage($"\n  ⚠ Lỗi xử lý đường dẫn ({entityId.Handle}): {ex.Message}");
                }
            }
        }

        private static void ProcessFeatureLineEntity(
            Transaction tr,
            Database db,
            Editor ed,
            TaoCorridorTheoCauKienOptions options,
            CorridorComponentRowItem row,
            Corridor targetCorridor,
            ObjectId entityId,
            ExistingBaselineSnapshot snapshot,
            HashSet<ObjectId> handledPreExisting,
            Dictionary<string, string> runtimeClonedMap,
            ref int addedBaselines,
            ref int addedRegions,
            ref int keptExistingRegions)
        {
            // BẮT BUỘC: Nếu Baseline đã có Region sẵn trên Corridor tổng → GIỮ NGUYÊN 100% LÝ TRÌNH, KHÔNG THÊM MỚI
            if (snapshot.FlBaselines.TryGetValue(entityId, out var existingBls) && existingBls.Count > 0)
            {
                if (handledPreExisting.Add(entityId))
                    UpdatePreExistingBaselineRegions(ed, options, row, existingBls, ref keptExistingRegions);
                return;
            }

            if (tr.GetObject(entityId, OpenMode.ForRead) is not FeatureLine fl)
                return;

            EnsureFeatureLineHasName(fl);
            string displayName = fl.Name;
            ObjectId resolvedFlId = fl.ObjectId;

            double reqStart = row.StartStation > 0.0 && row.StartStation < fl.Length2D ? row.StartStation : 0.0;
            double reqEnd = row.EndStation > reqStart && row.EndStation <= fl.Length2D + 0.05
                ? Math.Min(fl.Length2D, row.EndStation)
                : fl.Length2D;

            Baseline? baseline = FindNonOverlappingOrEmptyBaseline(targetCorridor, true, resolvedFlId, ObjectId.Null, reqStart, reqEnd);
            if (baseline == null)
            {
                string blName = GetUniqueBaselineName(targetCorridor, $"BL_{displayName}");
                try
                {
                    baseline = targetCorridor.Baselines.Add(blName, resolvedFlId);
                }
                catch
                {
                    ObjectId clonedFlId = CloneFeatureLineAsIndependent(tr, db, fl, $"FL_Cln_{fl.Handle}");
                    if (clonedFlId.IsNull) throw;

                    runtimeClonedMap[GetHandleHex(fl.ObjectId)] = GetHandleHex(clonedFlId);
                    resolvedFlId = clonedFlId;
                    baseline = targetCorridor.Baselines.Add(blName, resolvedFlId);
                    ed.WriteMessage($"\n  ℹ Chuyển đổi FeatureLine phụ thuộc '{displayName}' thành bản sao độc lập để thêm vào Corridor tổng.");
                }
                addedBaselines++;
            }

            if (baseline != null)
            {
                CreateAndConfigureRegion(ed, options, row, targetCorridor, baseline, displayName);
                addedRegions++;
            }
        }

        private static void ProcessAlignmentEntity(
            Transaction tr,
            Editor ed,
            TaoCorridorTheoCauKienOptions options,
            CorridorComponentRowItem row,
            Corridor targetCorridor,
            ObjectId entityId,
            ExistingBaselineSnapshot snapshot,
            HashSet<ObjectId> handledPreExisting,
            ref int addedBaselines,
            ref int addedRegions,
            ref int keptExistingRegions)
        {
            if (tr.GetObject(entityId, OpenMode.ForRead) is not Alignment align)
                return;

            ObjectId profileId = SelectDesignProfileId(tr, align, row.ExplicitProfileId);
            if (profileId.IsNull)
            {
                ed.WriteMessage($"\n  ⚠ Tuyến '{align.Name}' chưa có Profile (Trắc dọc). Bỏ qua tuyến này.");
                return;
            }

            var key = (align.ObjectId, profileId);
            if (snapshot.AlignBaselines.TryGetValue(key, out var existingBls) && existingBls.Count > 0)
            {
                if (handledPreExisting.Add(entityId))
                    UpdatePreExistingBaselineRegions(ed, options, row, existingBls, ref keptExistingRegions);
                return;
            }

            var prof = tr.GetObject(profileId, OpenMode.ForRead) as Profile;
            double reqStart = row.StartStation > align.StartingStation && row.StartStation < align.EndingStation
                ? row.StartStation : align.StartingStation;
            double reqEnd = row.EndStation > reqStart && row.EndStation <= align.EndingStation + 0.05
                ? Math.Min(align.EndingStation, row.EndStation) : align.EndingStation;

            Baseline? baseline = FindNonOverlappingOrEmptyBaseline(targetCorridor, false, align.ObjectId, profileId, reqStart, reqEnd);
            if (baseline == null)
            {
                string blName = GetUniqueBaselineName(targetCorridor, $"BL_{align.Name}_{prof?.Name}");
                baseline = targetCorridor.Baselines.Add(blName, align.ObjectId, profileId);
                addedBaselines++;
            }

            if (baseline != null)
            {
                CreateAndConfigureRegion(ed, options, row, targetCorridor, baseline, align.Name);
                addedRegions++;
            }
        }

        private static void EnsureFeatureLineHasName(FeatureLine fl)
        {
            if (!string.IsNullOrWhiteSpace(fl.Name)) return;
            try
            {
                fl.UpgradeOpen();
                fl.Name = $"FL_{fl.Handle}";
            }
            catch { }
        }

        private static ObjectId CloneFeatureLineAsIndependent(Transaction tr, Database db, FeatureLine srcFl, string desiredName)
        {
            try
            {
                Point3dCollection pts = srcFl.GetPoints(Autodesk.Civil.FeatureLinePointType.AllPoints);
                if (pts == null || pts.Count < 2) return ObjectId.Null;

                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                ObjectId poly3dId;
                using (var poly3d = new Polyline3d(Poly3dType.SimplePoly, pts, false))
                {
                    poly3d.Layer = srcFl.Layer;
                    poly3dId = ms.AppendEntity(poly3d);
                    tr.AddNewlyCreatedDBObject(poly3d, true);
                }

                ObjectId newFlId = FeatureLine.Create(desiredName, poly3dId);
                if (!poly3dId.IsNull && !poly3dId.IsErased)
                    tr.GetObject(poly3dId, OpenMode.ForWrite, false)?.Erase();

                if (!newFlId.IsNull && tr.GetObject(newFlId, OpenMode.ForWrite) is FeatureLine newFl)
                {
                    newFl.Layer = srcFl.Layer;
                    EnsureFeatureLineHasName(newFl);
                }
                return newFlId;
            }
            catch
            {
                return ObjectId.Null;
            }
        }

        private static ObjectId SelectDesignProfileId(Transaction tr, Alignment align, ObjectId explicitProfileId)
        {
            if (!explicitProfileId.IsNull && explicitProfileId.IsValid && !explicitProfileId.IsErased)
                return explicitProfileId;

            ObjectIdCollection profileIds = align.GetProfileIds();
            if (profileIds == null || profileIds.Count == 0) return ObjectId.Null;

            ObjectId layoutCandidate = ObjectId.Null;
            ObjectId firstValid = ObjectId.Null;

            foreach (ObjectId pid in profileIds)
            {
                if (tr.GetObject(pid, OpenMode.ForRead) is not Profile prof) continue;
                if (firstValid.IsNull) firstValid = pid;

                if (prof.Name.StartsWith("DD", StringComparison.OrdinalIgnoreCase) ||
                    prof.Name.Contains("TK", StringComparison.OrdinalIgnoreCase) ||
                    prof.Name.Contains("Connected", StringComparison.OrdinalIgnoreCase))
                {
                    return pid;
                }

                if (prof.ProfileType == ProfileType.FG && layoutCandidate.IsNull)
                    layoutCandidate = pid;
            }

            return !layoutCandidate.IsNull ? layoutCandidate : firstValid;
        }

        private static string GetUniqueBaselineName(Corridor corridor, string baseName)
        {
            var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Baseline bl in corridor.Baselines)
                existingNames.Add(bl.Name);

            if (!existingNames.Contains(baseName)) return baseName;
            int index = 2;
            while (existingNames.Contains($"{baseName}_{index}")) index++;
            return $"{baseName}_{index}";
        }
    }
}

using System;
using System.Collections.Generic;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;

using Exception = System.Exception;

namespace MyFirstProject.Commands.CTC.TaoCorridorTheoCauKien
{
    internal static partial class TaoCorridorTheoCauKienService
    {
        private static bool ProcessSingleBaselineEntity(
            Transaction tr,
            Database db,
            Editor ed,
            TaoCorridorTheoCauKienOptions options,
            CorridorComponentRowItem row,
            Corridor targetCorridor,
            ObjectId entityId,
            ref int totalBaselinesAdded,
            ref int totalRegionsAdded)
        {
            if (entityId.IsNull || !entityId.IsValid || entityId.IsErased)
                return false;

            try
            {
                bool updatedExistingRegion;
                string entityDisplayName;

                Baseline? baseline = row.SourceType == ComponentBaselineSourceType.FeatureLine
                    ? PrepareFeatureLineBaseline(tr, db, ed, options, row, targetCorridor, entityId, out entityDisplayName, out updatedExistingRegion, ref totalBaselinesAdded)
                    : PrepareAlignmentBaseline(tr, ed, options, row, targetCorridor, entityId, out entityDisplayName, out updatedExistingRegion, ref totalBaselinesAdded);

                if (updatedExistingRegion)
                    return true;

                if (baseline == null)
                    return false;

                CreateAndConfigureRegion(ed, options, row, targetCorridor, baseline, entityDisplayName);
                totalRegionsAdded++;
                return true;
            }
            catch (Exception exBl)
            {
                ed.WriteMessage($"\n  ⚠ Lỗi khi thêm đường dẫn ({entityId.Handle}): {exBl.Message}");
                return false;
            }
        }

        private static Baseline? PrepareFeatureLineBaseline(
            Transaction tr,
            Database db,
            Editor ed,
            TaoCorridorTheoCauKienOptions options,
            CorridorComponentRowItem row,
            Corridor targetCorridor,
            ObjectId entityId,
            out string entityDisplayName,
            out bool updatedExistingRegion,
            ref int totalBaselinesAdded)
        {
            entityDisplayName = "";
            updatedExistingRegion = false;

            if (tr.GetObject(entityId, OpenMode.ForRead) is not FeatureLine fl)
                return null;

            EnsureFeatureLineHasName(tr, fl);
            entityDisplayName = fl.Name;
            ObjectId resolvedFlId = fl.ObjectId;

            double flStart = 0.0;
            double flEnd = fl.Length2D;
            double reqStart = row.StartStation > flStart && row.StartStation < flEnd ? row.StartStation : flStart;
            double reqEnd = row.EndStation > reqStart && row.EndStation <= flEnd + 0.05
                ? Math.Min(flEnd, row.EndStation)
                : flEnd;

            // Kiểm tra nếu đã tồn tại Region trùng khớp hoàn toàn trong Corridor đích
            if (TryFindExistingMatchingRegion(targetCorridor, true, resolvedFlId, ObjectId.Null, row.AssemblyId, reqStart, reqEnd, out Baseline? matchedBl, out BaselineRegion? matchedRg) &&
                matchedBl != null && matchedRg != null)
            {
                UpdateExistingRegionSettings(ed, options, row, matchedBl, matchedRg);
                updatedExistingRegion = true;
                return matchedBl;
            }

            // Tìm Baseline cùng FeatureLine chưa bị giao cắt khoảng lý trình, nếu không có thì thêm Baseline mới
            Baseline? baseline = FindReusableBaseline(targetCorridor, true, resolvedFlId, ObjectId.Null, reqStart, reqEnd);
            if (baseline == null)
            {
                string blName = GetUniqueBaselineName(targetCorridor, $"BL_{row.ComponentName}_{entityDisplayName}");
                try
                {
                    baseline = targetCorridor.Baselines.Add(blName, resolvedFlId);
                }
                catch
                {
                    // Trường hợp FeatureLine là AutoCorridorFeatureLine thuộc chính Corridor đích (tự tham chiếu)
                    // → Tạo bản sao FeatureLine độc lập từ tọa độ 3D để cho phép gộp chung vào 1 Corridor
                    ObjectId clonedFlId = CloneFeatureLineAsIndependent(tr, db, fl, $"FL_{row.ComponentName}_{fl.Handle}");
                    if (!clonedFlId.IsNull)
                    {
                        resolvedFlId = clonedFlId;
                        baseline = targetCorridor.Baselines.Add(blName, resolvedFlId);
                        ed.WriteMessage($"\n  ℹ Đã chuyển đổi FeatureLine phụ thuộc '{entityDisplayName}' thành FeatureLine độc lập để gộp vào Corridor '{targetCorridor.Name}'.");
                    }
                    else
                    {
                        throw;
                    }
                }
                totalBaselinesAdded++;
            }

            return baseline;
        }

        private static Baseline? PrepareAlignmentBaseline(
            Transaction tr,
            Editor ed,
            TaoCorridorTheoCauKienOptions options,
            CorridorComponentRowItem row,
            Corridor targetCorridor,
            ObjectId entityId,
            out string entityDisplayName,
            out bool updatedExistingRegion,
            ref int totalBaselinesAdded)
        {
            entityDisplayName = "";
            updatedExistingRegion = false;

            if (tr.GetObject(entityId, OpenMode.ForRead) is not Alignment align)
                return null;

            ObjectId profileId = SelectDesignProfileId(tr, align, row.ExplicitProfileId);
            if (profileId.IsNull)
            {
                ed.WriteMessage($"\n  ⚠ Tuyến '{align.Name}' chưa có Profile (Trắc dọc). Bỏ qua tuyến này.");
                return null;
            }

            var prof = tr.GetObject(profileId, OpenMode.ForRead) as Profile;
            entityDisplayName = align.Name;
            ObjectId resolvedAlignId = align.ObjectId;
            ObjectId resolvedProfId = profileId;

            double alStart = align.StartingStation;
            double alEnd = align.EndingStation;
            double reqStart = row.StartStation > alStart && row.StartStation < alEnd ? row.StartStation : alStart;
            double reqEnd = row.EndStation > reqStart && row.EndStation <= alEnd + 0.05
                ? Math.Min(alEnd, row.EndStation)
                : alEnd;

            if (TryFindExistingMatchingRegion(targetCorridor, false, resolvedAlignId, resolvedProfId, row.AssemblyId, reqStart, reqEnd, out Baseline? matchedBl, out BaselineRegion? matchedRg) &&
                matchedBl != null && matchedRg != null)
            {
                UpdateExistingRegionSettings(ed, options, row, matchedBl, matchedRg);
                updatedExistingRegion = true;
                return matchedBl;
            }

            Baseline? baseline = FindReusableBaseline(targetCorridor, false, resolvedAlignId, resolvedProfId, reqStart, reqEnd);
            if (baseline == null)
            {
                string blName = GetUniqueBaselineName(targetCorridor, $"BL_{row.ComponentName}_{align.Name}_{prof?.Name}");
                baseline = targetCorridor.Baselines.Add(blName, resolvedAlignId, resolvedProfId);
                totalBaselinesAdded++;
            }

            return baseline;
        }

        private static void EnsureFeatureLineHasName(Transaction tr, FeatureLine fl)
        {
            if (!string.IsNullOrWhiteSpace(fl.Name))
                return;

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
                if (pts == null || pts.Count < 2)
                    return ObjectId.Null;

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
                {
                    var tmpPoly = tr.GetObject(poly3dId, OpenMode.ForWrite, false);
                    tmpPoly?.Erase();
                }

                if (!newFlId.IsNull && tr.GetObject(newFlId, OpenMode.ForWrite) is FeatureLine newFl)
                {
                    newFl.Layer = srcFl.Layer;
                    EnsureFeatureLineHasName(tr, newFl);
                }

                return newFlId;
            }
            catch
            {
                return ObjectId.Null;
            }
        }

        /// <summary>
        /// Ưu tiên chọn Đường đỏ thiết kế (Layout Profile / tên bắt đầu bằng DD- hoặc TK-) thay vì đường tự nhiên (EG/TN)
        /// </summary>
        private static ObjectId SelectDesignProfileId(Transaction tr, Alignment align, ObjectId explicitProfileId)
        {
            if (!explicitProfileId.IsNull && explicitProfileId.IsValid && !explicitProfileId.IsErased)
                return explicitProfileId;

            ObjectIdCollection profileIds = align.GetProfileIds();
            if (profileIds == null || profileIds.Count == 0)
                return ObjectId.Null;

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
                {
                    layoutCandidate = pid;
                }
            }

            return !layoutCandidate.IsNull ? layoutCandidate : firstValid;
        }

        private static string GetUniqueBaselineName(Corridor corridor, string baseName)
        {
            var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Baseline bl in corridor.Baselines)
                existingNames.Add(bl.Name);

            if (!existingNames.Contains(baseName))
                return baseName;

            int index = 2;
            while (existingNames.Contains($"{baseName}_{index}"))
                index++;

            return $"{baseName}_{index}";
        }
    }
}

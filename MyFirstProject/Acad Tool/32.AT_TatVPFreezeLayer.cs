// (C) Copyright 2026 by T27
// Lệnh tắt/bật VP Freeze layer của đối tượng được chọn trong viewport hiện hành
//
using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;

using MyFirstProject.Acad_Tool;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Color = Autodesk.AutoCAD.Colors.Color;
using Exception = System.Exception;

[assembly: CommandClass(typeof(Civil3DCsharp.AT_TatVPFreezeLayerCmd))]

namespace Civil3DCsharp
{
    /// <summary>
    /// Model lưu thông tin hiển thị của Layer trong Viewport
    /// </summary>
    public class VpLayerItem
    {
        public ObjectId LayerId { get; set; } = ObjectId.Null;
        public string Name { get; set; } = "";
        public Color CadColor { get; set; } = Color.FromColorIndex(ColorMethod.ByAci, 7);
        public bool IsFrozenInVp { get; set; } = false;
        public bool IsCurrent { get; set; } = false;
        public bool IsLocked { get; set; } = false;
        public bool IsOff { get; set; } = false;
        public bool IsGloballyFrozen { get; set; } = false;
    }

    /// <summary>
    /// Class chứa lệnh Tắt / Bật VP Freeze Layer của đối tượng trong Viewport hiện hành
    /// Các lệnh: AT_VPFREEZE, AT_TATVPFREEZE, VPF
    /// </summary>
    public class AT_TatVPFreezeLayerCmd
    {
        [CommandMethod("AT_VPFREEZE")]
        public void TatVPFreezeLayerCommand()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            try
            {
                using (var form = new TatVPFreezeLayerForm())
                {
                    Application.ShowModalDialog(form);
                }
            }
            catch (Exception ex)
            {
                doc.Editor.WriteMessage($"\n❌ Lỗi khi khởi chạy lệnh AT_VPFREEZE: {ex.Message}");
            }
        }

        #region Helper Methods

        /// <summary>
        /// Xác định Viewport hiện hành hoặc Viewport đang hoạt động trên Layout
        /// </summary>
        public static ObjectId GetActiveOrLayoutViewportId(Database db, out string statusMessage, out int vpNumber)
        {
            statusMessage = "";
            vpNumber = -1;

            if (db.TileMode)
            {
                statusMessage = "Đang ở Model Space. Vui lòng chuyển sang Layout để sử dụng tính năng VP Freeze.";
                return ObjectId.Null;
            }

            short cvport = 1;
            try
            {
                cvport = (short)Application.GetSystemVariable("CVPORT");
            }
            catch { }

            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    LayoutManager layoutMgr = LayoutManager.Current;
                    ObjectId layoutId = layoutMgr.GetLayoutId(layoutMgr.CurrentLayout);
                    if (layoutId == ObjectId.Null)
                    {
                        statusMessage = "Không xác định được Layout hiện tại.";
                        tr.Commit();
                        return ObjectId.Null;
                    }

                    var layout = (Layout)tr.GetObject(layoutId, OpenMode.ForRead);
                    var layoutBtr = (BlockTableRecord)tr.GetObject(layout.BlockTableRecordId, OpenMode.ForRead);

                    // TH1: Đang đứng trong một Viewport nổi (CVPORT > 1)
                    if (cvport > 1)
                    {
                        foreach (ObjectId entId in layoutBtr)
                        {
                            if (entId.ObjectClass.DxfName == "VIEWPORT")
                            {
                                var vp = tr.GetObject(entId, OpenMode.ForRead) as Viewport;
                                if (vp != null && vp.Number == cvport)
                                {
                                    vpNumber = vp.Number;
                                    double scale = vp.CustomScale;
                                    string scaleStr = scale > 0 ? (scale < 1.0 ? $"1:{(1.0 / scale):F0}" : $"{scale:F2}:1") : "N/A";
                                    statusMessage = $"✓ Đang kích hoạt Viewport #{vp.Number} (Tỉ lệ {scaleStr})";
                                    tr.Commit();
                                    return entId;
                                }
                            }
                        }
                    }

                    // TH2: Đang ở Paper Space (CVPORT == 1), tìm các Viewport nổi trên layout
                    var floatingVpIds = new List<ObjectId>();
                    foreach (ObjectId entId in layoutBtr)
                    {
                        if (entId.ObjectClass.DxfName == "VIEWPORT")
                        {
                            var vp = tr.GetObject(entId, OpenMode.ForRead) as Viewport;
                            if (vp != null && vp.Number > 1)
                            {
                                floatingVpIds.Add(entId);
                            }
                        }
                    }

                    if (floatingVpIds.Count == 1)
                    {
                        var vp = tr.GetObject(floatingVpIds[0], OpenMode.ForRead) as Viewport;
                        if (vp != null)
                        {
                            vpNumber = vp.Number;
                            double scale = vp.CustomScale;
                            string scaleStr = scale > 0 ? (scale < 1.0 ? $"1:{(1.0 / scale):F0}" : $"{scale:F2}:1") : "N/A";
                            statusMessage = $"✓ Đã tự động chọn Viewport #{vp.Number} trên layout (Tỉ lệ {scaleStr})";
                            tr.Commit();
                            return floatingVpIds[0];
                        }
                    }
                    else if (floatingVpIds.Count > 1)
                    {
                        statusMessage = $"⚠️ Đang ở Paper Space (Có {floatingVpIds.Count} Viewport trên Layout). Vui lòng bấm 'Chọn Viewport' để chỉ định.";
                        tr.Commit();
                        return ObjectId.Null;
                    }
                    else
                    {
                        statusMessage = "⚠️ Không tìm thấy Viewport nào trên Layout hiện tại.";
                        tr.Commit();
                        return ObjectId.Null;
                    }

                    tr.Commit();
                }
                catch (Exception ex)
                {
                    statusMessage = $"Lỗi tìm viewport: {ex.Message}";
                }
            }

            return ObjectId.Null;
        }

        /// <summary>
        /// Cho phép người dùng pick chọn một Viewport trên Layout
        /// </summary>
        public static ObjectId SelectViewportOnCanvas(Editor ed, Database db)
        {
            ObjectId vpId = ObjectId.Null;
            try
            {
                var peo = new PromptEntityOptions("\nChọn Viewport cần thao tác trên Layout: ");
                peo.SetRejectMessage("\nĐối tượng chọn phải là Viewport!");
                peo.AddAllowedClass(typeof(Viewport), true);

                var per = ed.GetEntity(peo);
                if (per.Status == PromptStatus.OK)
                {
                    using (var tr = db.TransactionManager.StartTransaction())
                    {
                        var vp = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Viewport;
                        if (vp != null)
                        {
                            if (vp.Number > 1)
                            {
                                vpId = per.ObjectId;
                            }
                            else
                            {
                                ed.WriteMessage("\n⚠️ Đây là Paper Space chính (Number = 1), không thể thao tác VP Freeze.");
                            }
                        }
                        tr.Commit();
                    }
                }
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n❌ Lỗi khi chọn Viewport: {ex.Message}");
            }
            return vpId;
        }

        /// <summary>
        /// Lấy danh sách các Layer ID đang bị VP Freeze trong Viewport
        /// </summary>
        public static HashSet<ObjectId> GetFrozenLayerIds(ObjectId viewportId)
        {
            var result = new HashSet<ObjectId>();
            if (viewportId.IsNull || !viewportId.IsValid) return result;

            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return result;

            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                try
                {
                    var vp = tr.GetObject(viewportId, OpenMode.ForRead) as Viewport;
                    if (vp != null)
                    {
                        ObjectIdCollection frozenCol = vp.GetFrozenLayers();
                        if (frozenCol != null)
                        {
                            foreach (ObjectId id in frozenCol)
                            {
                                if (!id.IsNull && id.IsValid)
                                    result.Add(id);
                            }
                        }
                    }
                    tr.Commit();
                }
                catch
                {
                }
            }
            return result;
        }

        /// <summary>
        /// Lấy toàn bộ danh sách Layer trong bản vẽ kèm trạng thái VP Freeze
        /// </summary>
        public static List<VpLayerItem> GetAllLayersWithVpStatus(Database db, ObjectId viewportId)
        {
            var list = new List<VpLayerItem>();
            HashSet<ObjectId> frozenSet = GetFrozenLayerIds(viewportId);

            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    ObjectId currentLayerId = db.Clayer;

                    foreach (ObjectId ltrId in lt)
                    {
                        var ltr = (LayerTableRecord)tr.GetObject(ltrId, OpenMode.ForRead);
                        var item = new VpLayerItem
                        {
                            LayerId = ltrId,
                            Name = ltr.Name,
                            CadColor = ltr.Color,
                            IsFrozenInVp = frozenSet.Contains(ltrId),
                            IsCurrent = (ltrId == currentLayerId),
                            IsLocked = ltr.IsLocked,
                            IsOff = ltr.IsOff,
                            IsGloballyFrozen = ltr.IsFrozen
                        };
                        list.Add(item);
                    }
                    tr.Commit();
                }
                catch { }
            }

            return list.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// Đóng băng (VP Freeze) danh sách Layer trong Viewport
        /// </summary>
        public static int FreezeLayersInVp(ObjectId viewportId, IEnumerable<ObjectId> layerIds)
        {
            if (viewportId.IsNull || !viewportId.IsValid) return 0;
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return 0;

            int count = 0;
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                try
                {
                    var vp = tr.GetObject(viewportId, OpenMode.ForWrite) as Viewport;
                    if (vp != null)
                    {
                        var col = new ObjectIdCollection();
                        foreach (var lid in layerIds)
                        {
                            if (!lid.IsNull && lid.IsValid && !lid.IsErased)
                            {
                                col.Add(lid);
                                count++;
                            }
                        }

                        if (col.Count > 0)
                        {
                            vp.FreezeLayersInViewport(col.GetEnumerator());
                        }
                    }
                    tr.Commit();
                    doc.Editor.Regen();
                }
                catch (Exception ex)
                {
                    doc.Editor.WriteMessage($"\n❌ Lỗi khi đóng băng layer trong Viewport: {ex.Message}");
                }
            }
            return count;
        }

        /// <summary>
        /// Rã đông (VP Thaw) danh sách Layer trong Viewport
        /// </summary>
        public static int ThawLayersInVp(ObjectId viewportId, IEnumerable<ObjectId> layerIds)
        {
            if (viewportId.IsNull || !viewportId.IsValid) return 0;
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return 0;

            int count = 0;
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                try
                {
                    var vp = tr.GetObject(viewportId, OpenMode.ForWrite) as Viewport;
                    if (vp != null)
                    {
                        var col = new ObjectIdCollection();
                        foreach (var lid in layerIds)
                        {
                            if (!lid.IsNull && lid.IsValid && !lid.IsErased)
                            {
                                col.Add(lid);
                                count++;
                            }
                        }

                        if (col.Count > 0)
                        {
                            vp.ThawLayersInViewport(col.GetEnumerator());
                        }
                    }
                    tr.Commit();
                    doc.Editor.Regen();
                }
                catch (Exception ex)
                {
                    doc.Editor.WriteMessage($"\n❌ Lỗi khi rã đông layer trong Viewport: {ex.Message}");
                }
            }
            return count;
        }

        /// <summary>
        /// Rã đông TẤT CẢ các layer đang bị VP Freeze trong Viewport
        /// </summary>
        public static int ThawAllLayersInVp(ObjectId viewportId)
        {
            if (viewportId.IsNull || !viewportId.IsValid) return 0;
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return 0;

            int count = 0;
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                try
                {
                    var vp = tr.GetObject(viewportId, OpenMode.ForWrite) as Viewport;
                    if (vp != null)
                    {
                        ObjectIdCollection frozenCol = vp.GetFrozenLayers();
                        if (frozenCol != null && frozenCol.Count > 0)
                        {
                            count = frozenCol.Count;
                            vp.ThawLayersInViewport(frozenCol.GetEnumerator());
                        }
                    }
                    tr.Commit();
                    doc.Editor.Regen();
                }
                catch (Exception ex)
                {
                    doc.Editor.WriteMessage($"\n❌ Lỗi khi rã đông tất cả layer: {ex.Message}");
                }
            }
            return count;
        }

        #endregion
    }
}

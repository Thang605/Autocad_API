// (C) Copyright 2026 by T27
//
using System;
using System.Collections.Generic;

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Color = Autodesk.AutoCAD.Colors.Color;
using Exception = System.Exception;

[assembly: CommandClass(typeof(Civil3DCsharp.CapNhatMauLayerCmd))]

namespace Civil3DCsharp
{
    /// <summary>
    /// Lệnh Cập Nhật Màu Layer theo Tiêu Chuẩn BIM / EIR (BEP T27)
    /// Lệnh: AT_CapNhatMauLayer / CNML / AT_UpdateLayerColor / AT_MauLayerBIM
    /// </summary>
    public class CapNhatMauLayerCmd
    {
        [CommandMethod("AT_CapNhatMauLayer")]
        [CommandMethod("CNML")]
        [CommandMethod("AT_UpdateLayerColor")]
        [CommandMethod("AT_MauLayerBIM")]
        public void CapNhatMauLayerCommand()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            using (var form = new CapNhatMauLayerForm())
            {
                Application.ShowModalDialog(form);
            }
        }

        /// <summary>
        /// Xử lý cập nhật màu Layer và thông tin Hạng mục/Vật liệu trong AutoCAD Transaction (Luôn dùng OpenMode.ForWrite)
        /// </summary>
        public static int ExecuteUpdateLayerColors(
            List<(string layerName, byte r, byte g, byte b, string categoryName, string materialName)> items,
            bool applyByLayer,
            bool updateDescription,
            bool unlockLayers,
            Action<string> log)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) throw new Exception("Không tìm thấy bản vẽ hiện hành!");

            var db = doc.Database;
            var ed = doc.Editor;
            int updatedCount = 0;
            int byLayerCount = 0;

            var updatedLayerNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (var docLock = doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var layTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForWrite);

                log("Bắt đầu cập nhật màu sắc & thông tin các Layer...");

                foreach (var item in items)
                {
                    string layName = item.layerName;
                    byte r = item.r;
                    byte g = item.g;
                    byte b = item.b;
                    string cat = item.categoryName;
                    string mat = item.materialName;

                    if (string.IsNullOrWhiteSpace(layName)) continue;

                    if (layTable.Has(layName))
                    {
                        var ltr = (LayerTableRecord)tr.GetObject(layTable[layName], OpenMode.ForWrite);

                        if (ltr.IsLocked && unlockLayers)
                        {
                            ltr.IsLocked = false;
                            log($"  🔓 Mở khóa Layer: [{layName}]");
                        }

                        // Cập nhật màu TrueColor
                        ltr.Color = Color.FromRgb(r, g, b);

                        // Cập nhật Description nếu được chọn
                        if (updateDescription)
                        {
                            string descText = "";
                            if (!string.IsNullOrWhiteSpace(cat) && !string.IsNullOrWhiteSpace(mat))
                            {
                                descText = $"{cat} | {mat}";
                            }
                            else if (!string.IsNullOrWhiteSpace(mat))
                            {
                                descText = mat;
                            }
                            else if (!string.IsNullOrWhiteSpace(cat))
                            {
                                descText = cat;
                            }

                            if (!string.IsNullOrEmpty(descText))
                            {
                                ltr.Description = descText;
                            }
                        }

                        updatedCount++;
                        updatedLayerNames.Add(layName);
                        log($"  ✅ Layer: [{layName}] -> RGB({r}, {g}, {b}) | {mat} ({cat})");
                    }
                    else
                    {
                        log($"  ⚠️ Bỏ qua Layer [{layName}] (không tồn tại trong bản vẽ)");
                    }
                }

                // Chuyển màu các đối tượng về ByLayer nếu được chọn
                if (applyByLayer && updatedLayerNames.Count > 0)
                {
                    log("Đang quét và chuyển màu các đối tượng trong Layer về ByLayer...");

                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    foreach (ObjectId btrId in bt)
                    {
                        var btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);

                        // Chỉ quét ModelSpace, PaperSpace layouts và các Block thường (bỏ qua Xref)
                        if (btr.IsFromExternalReference) continue;

                        foreach (ObjectId entId in btr)
                        {
                            var ent = tr.GetObject(entId, OpenMode.ForRead) as Entity;
                            if (ent != null && updatedLayerNames.Contains(ent.Layer))
                            {
                                if (ent.Color.ColorMethod != ColorMethod.ByLayer)
                                {
                                    ent.UpgradeOpen();
                                    ent.Color = Color.FromColorIndex(ColorMethod.ByLayer, 256);
                                    byLayerCount++;
                                }
                            }
                        }
                    }

                    if (byLayerCount > 0)
                    {
                        log($"  ✨ Đã chuyển màu {byLayerCount} đối tượng về ByLayer.");
                    }
                    else
                    {
                        log("  ✨ Tất cả đối tượng đã ở trạng thái ByLayer.");
                    }
                }

                tr.Commit();
            }

            ed.Regen();
            log($"Hoàn tất cập nhật {updatedCount} Layer!");
            ed.WriteMessage($"\n[AT_CapNhatMauLayer] Đã cập nhật thành công {updatedCount} Layer.");

            return updatedCount;
        }
    }
}

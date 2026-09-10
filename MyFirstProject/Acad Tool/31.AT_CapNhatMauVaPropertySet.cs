// (C) Copyright 2026 by T27
//
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Color = Autodesk.AutoCAD.Colors.Color;
using Exception = System.Exception;
using MyFirstProject.Extensions;

[assembly: CommandClass(typeof(Civil3DCsharp.CapNhatMauVaPropertySetCmd))]

namespace Civil3DCsharp
{
    /// <summary>
    /// Lệnh Phối Hợp Cập Nhật Màu Layer & Cập Nhật Property Set theo Mẫu BIM / EIR (BEP T27)
    /// Lệnh: AT_CapNhatMauVaPropertySet / CNMPS / AT_MauVaPropertySet / AT_LayerBimStandard
    /// </summary>
    public class CapNhatMauVaPropertySetCmd
    {
        [CommandMethod("AT_CapNhatMauVaPropertySet")]
        [CommandMethod("CNMPS")]
        [CommandMethod("AT_MauVaPropertySet")]
        [CommandMethod("AT_LayerBimStandard")]
        public void CapNhatMauVaPropertySetCommand()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            using (var form = new CapNhatMauVaPropertySetForm())
            {
                Application.ShowModalDialog(form);
            }
        }

        /// <summary>
        /// Thực thi phối hợp cập nhật Màu Layer, Mô tả Layer, chuyển ByLayer và cập nhật Property Set (Transaction OpenMode.ForWrite)
        /// </summary>
        public static int ExecuteUpdateAll(
            List<LayerBimRowModel> rows,
            string propertySetName,
            bool updateColor,
            bool updatePropertySet,
            bool updateDescription,
            bool applyByLayer,
            bool unlockLayers,
            Action<string> log)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) throw new Exception("Không tìm thấy bản vẽ hiện hành!");

            var db = doc.Database;
            var ed = doc.Editor;
            var timer = Stopwatch.StartNew();

            int updatedLayersCount = 0;
            int byLayerCount = 0;
            int updatedSolidsCount = 0;

            var updatedLayerNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (var docLock = doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var layTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForWrite);

                log("🚀 Bắt đầu quá trình cập nhật Màu Layer và Property Set...");

                // 1. Cập nhật Màu và Mô tả trên LayerTable
                foreach (var item in rows)
                {
                    string layName = item.LayerName;
                    if (string.IsNullOrWhiteSpace(layName)) continue;

                    if (layTable.Has(layName))
                    {
                        var ltr = (LayerTableRecord)tr.GetObject(layTable[layName], OpenMode.ForWrite);

                        if (ltr.IsLocked && unlockLayers)
                        {
                            ltr.IsLocked = false;
                            log($"  🔓 Mở khóa Layer: [{layName}]");
                        }

                        // Cập nhật TrueColor
                        if (updateColor && item.NewColor.HasValue)
                        {
                            ltr.Color = Color.FromRgb(item.NewR!.Value, item.NewG!.Value, item.NewB!.Value);
                        }

                        // Cập nhật Mô tả Description
                        if (updateDescription)
                        {
                            string desc = "";
                            if (!string.IsNullOrWhiteSpace(item.CauKien) && !string.IsNullOrWhiteSpace(item.VatLieu))
                            {
                                desc = $"{item.CauKien} | {item.VatLieu}";
                            }
                            else if (!string.IsNullOrWhiteSpace(item.VatLieu))
                            {
                                desc = item.VatLieu;
                            }
                            else if (!string.IsNullOrWhiteSpace(item.CauKien))
                            {
                                desc = item.CauKien;
                            }

                            if (!string.IsNullOrEmpty(desc))
                            {
                                ltr.Description = desc;
                            }
                        }

                        updatedLayersCount++;
                        updatedLayerNames.Add(layName);
                        string colorInfo = item.NewColor.HasValue ? $"RGB({item.NewR},{item.NewG},{item.NewB})" : "giữ nguyên màu";
                        log($"  ✅ Layer [{layName}] -> {colorInfo} | {item.CauKien} | {item.VatLieu}");
                    }
                    else
                    {
                        log($"  ⚠️ Bỏ qua Layer [{layName}] (không tồn tại trong LayerTable)");
                    }
                }

                // 2. Chuyển màu đối tượng về ByLayer nếu được chọn
                if (applyByLayer && updatedLayerNames.Count > 0)
                {
                    log("Đang quét và chuyển màu các đối tượng trong Layer về ByLayer...");

                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    foreach (ObjectId btrId in bt)
                    {
                        var btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
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
                }

                // 3. Cập nhật Property Set cho 3D Solid và Body
                if (updatePropertySet && !string.IsNullOrEmpty(propertySetName))
                {
                    log($"Đang gán và cập nhật Property Set '{propertySetName}' cho các đối tượng 3D Solid & Body...");

                    var mappings = rows.Select(x => new LayerPropertyMapping
                    {
                        LayerName = x.LayerName,
                        CauKien = x.CauKien,
                        VatLieu = x.VatLieu,
                        SolidCount = x.SolidCount,
                        BodyCount = x.BodyCount,
                        IsSelected = true
                    }).ToList();

                    updatedSolidsCount = PropertySetUtils.ApplyPropertySetsByMapping(tr, propertySetName, mappings);
                    log($"  💎 Đã gán / cập nhật Property Set thành công cho {updatedSolidsCount} đối tượng 3D Solid / Body.");
                }

                tr.Commit();
            }

            ed.Regen();
            timer.Stop();

            log($"Hoàn tất cập nhật: {updatedLayersCount} Layer, {updatedSolidsCount} đối tượng 3D, {byLayerCount} ByLayer trong {timer.ElapsedMilliseconds} ms ({timer.Elapsed.TotalSeconds:F2}s)!");
            ed.WriteMessage($"\n[AT_CapNhatMauVaPropertySet] Đã cập nhật thành công {updatedLayersCount} Layer và {updatedSolidsCount} đối tượng 3D trong {timer.Elapsed.TotalSeconds:F2}s.");

            return updatedSolidsCount;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using CadColor = Autodesk.AutoCAD.Colors.Color;
using CadEntity = Autodesk.AutoCAD.DatabaseServices.Entity;
using CivSurface = Autodesk.Civil.DatabaseServices.TinSurface;

[assembly: CommandClass(typeof(MyFirstProject.Civil_Tool_2.CTT_Trich_Xuat_Solid_Ket_Cau_Ao_Duong_Command))]

namespace MyFirstProject.Civil_Tool_2
{
    public class PavementSolidExtractionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int TotalSolidsCreated { get; set; }
        public List<ObjectId> CreatedSolidIds { get; set; } = new List<ObjectId>();
    }

    public class CTT_Trich_Xuat_Solid_Ket_Cau_Ao_Duong_Command
    {
        [CommandMethod("AT_Xuat_Solid_KCAD_Tu_MatPhang")]
        public static void XuatSolidKcadTuMatPhang()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            Editor ed = doc.Editor;
            Database db = doc.Database;

            try
            {
                using (var form = new TrichXuatSolidKetCauAoDuongForm(db, ed))
                {
                    Application.ShowModalDialog(form);
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n[LỖI] Không thể mở giao diện trích xuất 3D Solid kết cấu áo đường: {ex.Message}");
            }
        }

        /// <summary>
        /// Thực hiện trích xuất các lớp 3D Solid kết cấu áo đường từ danh sách TIN Surface
        /// </summary>
        public static PavementSolidExtractionResult ThucHienTrichXuatSolid(
            Database db,
            Editor ed,
            List<ObjectId> surfaceIds,
            List<PavementLayerItem> activeLayers,
            PavementSolidStackMode stackMode,
            bool keepTwoDecimals,
            bool updateExistingLayerColor,
            Action<int, string>? progressCallback = null)
        {
            if (surfaceIds == null || surfaceIds.Count == 0)
            {
                return new PavementSolidExtractionResult
                {
                    Success = false,
                    Message = "Danh sách mặt phẳng TIN Surface trống."
                };
            }

            if (activeLayers == null || activeLayers.Count == 0)
            {
                return new PavementSolidExtractionResult
                {
                    Success = false,
                    Message = "Danh sách lớp kết cấu áo đường trống."
                };
            }

            List<ObjectId> allCreatedSolids = new List<ObjectId>();
            StringBuilder summaryReport = new StringBuilder();

            try
            {
                Dictionary<int, string> resolvedLayerNames = new Dictionary<int, string>();

                // 1. Tạo mới hoặc đổi tên (sang IN HOA TOÀN BỘ) các Layer trong 1 Transaction riêng và Commit trước
                //    để hàm native CreateSolidsAtDepth nhận diện đúng tên Layer IN HOA trong Database.
                using (Transaction trLayer = db.TransactionManager.StartTransaction())
                {
                    LayerTable? layerTable = trLayer.GetObject(db.LayerTableId, OpenMode.ForWrite) as LayerTable;
                    if (layerTable == null)
                    {
                        return new PavementSolidExtractionResult
                        {
                            Success = false,
                            Message = "Không thể mở bảng LayerTable của bản vẽ."
                        };
                    }

                    for (int i = 0; i < activeLayers.Count; i++)
                    {
                        var layerItem = activeLayers[i];
                        string layerName = TrichXuatSolidKetCauAoDuongForm.BuildLayerName(
                            layerItem.StructureName,
                            layerItem.MaterialName,
                            layerItem.Thickness,
                            keepTwoDecimals).ToUpperInvariant();

                        EnsureLayerExistsAndReady(
                            db,
                            trLayer,
                            layerTable,
                            ref layerName,
                            layerItem.AciColor,
                            updateExistingLayerColor);

                        resolvedLayerNames[i] = layerName;
                    }

                    trLayer.Commit();
                }

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    LayerTable? layerTable = tr.GetObject(db.LayerTableId, OpenMode.ForWrite) as LayerTable;

                    int currentStep = 0;
                    int validSurfaceCount = 0;

                    ed.WriteMessage("\n======================================================================");
                    ed.WriteMessage("\n  TRÍCH XUẤT 3D SOLID KẾT CẤU ÁO ĐƯỜNG (AT_XUAT_SOLID_KCAD_TU_MATPHANG)");
                    ed.WriteMessage("\n======================================================================");

                    // 2. Duyệt qua từng mặt phẳng TIN Surface đã chọn
                    foreach (ObjectId surfId in surfaceIds)
                    {
                        if (surfId.IsNull || !surfId.IsValid || surfId.IsErased)
                            continue;

                        CivSurface? tinSurf = tr.GetObject(surfId, OpenMode.ForWrite) as CivSurface;
                        if (tinSurf == null)
                            continue;

                        validSurfaceCount++;
                        double surfMaxZ = 0.0;
                        bool hasSurfMaxZ = false;
                        try
                        {
                            surfMaxZ = tinSurf.GetGeneralProperties().MaximumElevation;
                            hasSurfMaxZ = true;
                        }
                        catch
                        {
                            try
                            {
                                surfMaxZ = tinSurf.GeometricExtents.MaxPoint.Z;
                                hasSurfMaxZ = true;
                            }
                            catch { }
                        }

                        ed.WriteMessage($"\n\n▶ Mặt phẳng nguồn: {tinSurf.Name}");
                        summaryReport.AppendLine($"▶ Mặt phẳng nguồn: {tinSurf.Name}");

                        double cumulativeDepth = 0.0;

                        // 3. Chiết xuất từng lớp kết cấu áo đường
                        for (int i = 0; i < activeLayers.Count; i++)
                        {
                            var layerItem = activeLayers[i];
                            double thickness = Math.Abs(layerItem.Thickness);
                            if (thickness <= 1e-5) continue;

                            string targetLayerName = resolvedLayerNames[i].ToUpperInvariant();
                            ushort aciColor = (layerItem.AciColor >= 1 && layerItem.AciColor <= 255)
                                ? layerItem.AciColor
                                : (ushort)1;

                            currentStep++;
                            progressCallback?.Invoke(
                                currentStep,
                                $"Đang trích xuất Lớp {i + 1}/{activeLayers.Count}: {targetLayerName} (dày {thickness:0.###}m)...");

                            // Độ sâu truyền vào CreateSolidsAtDepth:
                            // - Âm (-thickness): đùn từ mặt phẳng đi xuống dưới
                            // - Dương (+thickness): đùn từ mặt phẳng đi lên trên
                            double depthParam = (stackMode == PavementSolidStackMode.StackUpFromBottomSurface)
                                ? thickness
                                : -thickness;

                            ObjectIdCollection? solidIds = null;
                            try
                            {
                                solidIds = tinSurf.CreateSolidsAtDepth(depthParam, targetLayerName, aciColor);
                            }
                            catch (System.Exception exCreate)
                            {
                                ed.WriteMessage($"\n  ⚠️ Lỗi khi gọi CreateSolidsAtDepth cho lớp '{targetLayerName}': {exCreate.Message}");
                                continue;
                            }

                            // Đảm bảo Layer sau khi gọi hàm native vẫn giữ nguyên tên IN HOA
                            if (layerTable != null)
                            {
                                EnsureLayerExistsAndReady(
                                    db,
                                    tr,
                                    layerTable,
                                    ref targetLayerName,
                                    aciColor,
                                    updateExistingLayerColor);
                            }

                            int layerSolidCount = 0;
                            if (solidIds != null && solidIds.Count > 0)
                            {
                                foreach (ObjectId sId in solidIds)
                                {
                                    if (sId.IsNull || !sId.IsValid || sId.IsErased) continue;

                                    if (tr.GetObject(sId, OpenMode.ForWrite) is CadEntity solidEnt)
                                    {
                                        // Đảm bảo gắn đúng Layer IN HOA và màu ByLayer
                                        solidEnt.Layer = targetLayerName;
                                        solidEnt.ColorIndex = 256; // ByLayer

                                        // Tính độ dịch chuyển cao độ Z để xếp chồng các lớp kết cấu áo đường
                                        double deltaZ = ComputeVerticalDisplacement(
                                            solidEnt,
                                            hasSurfMaxZ,
                                            surfMaxZ,
                                            thickness,
                                            cumulativeDepth,
                                            stackMode);

                                        if (Math.Abs(deltaZ) > 1e-6)
                                        {
                                            solidEnt.TransformBy(Matrix3d.Displacement(new Vector3d(0, 0, deltaZ)));
                                        }

                                        allCreatedSolids.Add(sId);
                                        layerSolidCount++;
                                    }
                                }
                            }

                            string zRangeText;
                            if (stackMode == PavementSolidStackMode.StackDownFromTopSurface)
                            {
                                zRangeText = $"{-cumulativeDepth:F3}m → {-(cumulativeDepth + thickness):F3}m";
                            }
                            else if (stackMode == PavementSolidStackMode.StackUpFromBottomSurface)
                            {
                                zRangeText = $"+{cumulativeDepth:F3}m → +{cumulativeDepth + thickness:F3}m";
                            }
                            else
                            {
                                zRangeText = $"0.000m → -{thickness:F3}m";
                            }

                            ed.WriteMessage($"\n  • Lớp {i + 1}: Layer [{targetLayerName}] | Dày: {thickness:0.###}m | Cao độ: {zRangeText} | Đã tạo: {layerSolidCount} Solid");
                            summaryReport.AppendLine($"  • Lớp {i + 1}: {targetLayerName} (Dày {thickness:0.###}m, Z: {zRangeText}) -> {layerSolidCount} Solid");

                            cumulativeDepth += thickness;
                        }

                        ed.WriteMessage($"\n  => Tổng chiều dày kết cấu áo đường: {cumulativeDepth:F3} m ({cumulativeDepth * 100.0:F1} cm)");
                        summaryReport.AppendLine($"  => Tổng chiều dày: {cumulativeDepth:F3} m ({cumulativeDepth * 100.0:F1} cm)");
                    }

                    tr.Commit();

                    ed.WriteMessage($"\n\n🎉 HOÀN TẤT! Đã trích xuất tổng cộng {allCreatedSolids.Count} đối tượng 3D Solid trên {validSurfaceCount} mặt phẳng.");
                    ed.WriteMessage("\n======================================================================\n");

                    if (allCreatedSolids.Count == 0)
                    {
                        return new PavementSolidExtractionResult
                        {
                            Success = false,
                            Message = "Không tạo được đối tượng 3D Solid nào từ mặt phẳng đã chọn. Hãy kiểm tra lại lưới tam giác của TIN Surface."
                        };
                    }

                    return new PavementSolidExtractionResult
                    {
                        Success = true,
                        TotalSolidsCreated = allCreatedSolids.Count,
                        CreatedSolidIds = allCreatedSolids,
                        Message = $"Đã trích xuất thành công {allCreatedSolids.Count} đối tượng 3D Solid cho {activeLayers.Count} lớp kết cấu áo đường!\n\n{summaryReport}"
                    };
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n❌ Lỗi trích xuất 3D Solid kết cấu áo đường: {ex.Message}");
                return new PavementSolidExtractionResult
                {
                    Success = false,
                    Message = $"Lỗi khi trích xuất 3D Solid:\n{ex.Message}"
                };
            }
        }

        /// <summary>
        /// Tính toán chính xác độ dịch chuyển theo trục Z cho từng lớp kết cấu áo đường
        /// dựa trên vị trí thực tế của Solid vừa tạo so với mặt phẳng gốc.
        /// </summary>
        private static double ComputeVerticalDisplacement(
            CadEntity solidEnt,
            bool hasSurfMaxZ,
            double surfMaxZ,
            double thickness,
            double cumulativeDepth,
            PavementSolidStackMode stackMode)
        {
            // Kiểm tra xem CreateSolidsAtDepth đã đùn lên trên ([S, S + d]) hay đùn xuống dưới ([S - d, S])
            bool extrudedUpward = (stackMode == PavementSolidStackMode.StackUpFromBottomSurface);

            if (hasSurfMaxZ)
            {
                try
                {
                    double solidMaxZ = solidEnt.GeometricExtents.MaxPoint.Z;
                    // Nếu đỉnh Solid cao hơn đỉnh Surface quá 0.5 * thickness -> Solid đã được đùn lên trên [S, S + d]
                    if (solidMaxZ > surfMaxZ + thickness * 0.5)
                    {
                        extrudedUpward = true;
                    }
                    else
                    {
                        extrudedUpward = false;
                    }
                }
                catch { }
            }

            switch (stackMode)
            {
                case PavementSolidStackMode.StackDownFromTopSurface:
                    // Mong muốn lớp i nằm ở [S - cumulativeDepth - thickness, S - cumulativeDepth]
                    return extrudedUpward
                        ? -(cumulativeDepth + thickness)
                        : -cumulativeDepth;

                case PavementSolidStackMode.StackUpFromBottomSurface:
                    // Mong muốn lớp i nằm ở [S + cumulativeDepth, S + cumulativeDepth + thickness]
                    return extrudedUpward
                        ? cumulativeDepth
                        : (cumulativeDepth + thickness);

                case PavementSolidStackMode.DirectFromSurfaceNoOffset:
                default:
                    // Mong muốn lớp nằm ở [S - thickness, S] ngay dưới mặt phẳng gốc
                    return extrudedUpward ? -thickness : 0.0;
            }
        }

        /// <summary>
        /// Đảm bảo Layer tồn tại, không bị khóa/đóng băng và được gán màu ACI theo cấu hình
        /// </summary>
        private static void EnsureLayerExistsAndReady(
            Database db,
            Transaction tr,
            LayerTable layerTable,
            ref string layerName,
            ushort aciColor,
            bool updateExistingColor)
        {
            layerName = (layerName ?? string.Empty).Trim().ToUpperInvariant();
            try
            {
                SymbolUtilityServices.ValidateSymbolName(layerName, false);
            }
            catch
            {
                layerName = TrichXuatSolidKetCauAoDuongForm.SanitizeLayerPart(layerName).ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(layerName))
                {
                    layerName = "KCAD_0.06";
                }
            }

            short safeAci = (aciColor >= 1 && aciColor <= 255) ? (short)aciColor : (short)1;

            if (layerTable.Has(layerName))
            {
                ObjectId layerId = layerTable[layerName];
                if (tr.GetObject(layerId, OpenMode.ForWrite) is LayerTableRecord ltr)
                {
                    if (ltr.IsLocked) ltr.IsLocked = false;
                    if (ltr.IsFrozen)
                    {
                        try { ltr.IsFrozen = false; } catch { }
                    }
                    if (ltr.IsOff) ltr.IsOff = false;

                    // Quan trọng: LayerTable.Has() trong AutoCAD không phân biệt hoa thường.
                    // Nếu Layer đã tồn tại từ trước với tên chữ thường, đổi qua tên tạm rồi đổi sang IN HOA
                    // để tránh lỗi eDuplicateRecordName của ObjectARX khi chỉ khác chữ hoa/thường.
                    if (!string.Equals(ltr.Name, layerName, StringComparison.Ordinal))
                    {
                        try
                        {
                            string tempName = $"__TMP_{Guid.NewGuid():N}".Substring(0, 18);
                            ltr.Name = tempName;
                            ltr.Name = layerName;
                        }
                        catch
                        {
                            try { ltr.Name = layerName; } catch { }
                        }
                    }

                    if (updateExistingColor)
                    {
                        ltr.Color = CadColor.FromColorIndex(ColorMethod.ByAci, safeAci);
                    }
                }
            }
            else
            {
                LayerTableRecord newLayer = new LayerTableRecord
                {
                    Name = layerName,
                    Color = CadColor.FromColorIndex(ColorMethod.ByAci, safeAci)
                };
                layerTable.Add(newLayer);
                tr.AddNewlyCreatedDBObject(newLayer, true);
            }
        }
    }
}

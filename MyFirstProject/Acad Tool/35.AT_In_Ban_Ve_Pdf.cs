// (C) Copyright 2026 by T27
// Lệnh In Bản Vẽ định dạng PDF theo Block sử dụng Plotter DWG To PDF.pc3
// Tự động nhận diện khổ giấy và chiều ngang/đứng từ kích thước từng Block
// Cấu trúc tên file BẮT BUỘC: [tên layout hiện hành]_[số thứ tự].pdf
// Tối ưu hóa tốc độ in (Fast Mode): Tắt PDFSHX Comments & Transparency flattening
//
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.PlottingServices;
using Autodesk.AutoCAD.Runtime;

using MyFirstProject.Acad_Tool;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(Civil3DCsharp.AT_In_Ban_Ve_Pdf_Commands))]

namespace Civil3DCsharp
{
    /// <summary>
    /// Class chứa lệnh in bản vẽ PDF theo Block với plotter DWG To PDF.pc3
    /// </summary>
    public class AT_In_Ban_Ve_Pdf_Commands
    {
        [CommandMethod("AT_In_Ban_Ve_Pdf")]
        public static void AT_In_Ban_Ve_Pdf()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            Database db = doc.Database;
            Editor ed = doc.Editor;

            ed.WriteMessage("\n═══════════════════════════════════════════════════════════════");
            ed.WriteMessage("\n  🖨️ AT_In_Ban_Ve_Pdf - IN BẢN VẼ PDF THEO BLOCK (DWG TO PDF.PC3)");
            ed.WriteMessage("\n═══════════════════════════════════════════════════════════════\n");

            try
            {
                // 1. Hiển thị Form cấu hình InDwgToPdfForm
                using (var form = new InDwgToPdfForm(ed, db))
                {
                    var dlgResult = Application.ShowModalDialog(form);
                    if (dlgResult != DialogResult.OK || !form.IsConfirmed)
                    {
                        ed.WriteMessage("\n❌ Đã hủy lệnh in bản vẽ PDF.");
                        return;
                    }

                    var validBlocks = form.SelectedBlocks.ToList();
                    if (validBlocks.Count == 0)
                    {
                        ed.WriteMessage("\n⚠️ Không có khung bản vẽ nào trong danh sách để in.");
                        return;
                    }

                    // 2. Thực hiện in từng Block ra 1 file PDF đánh số thứ tự: [tên layout hiện hành]_[số thứ tự].pdf
                    ExecutePlotBlocks(doc, db, ed, form, validBlocks);
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n❌ Lỗi thực thi lệnh AT_In_Ban_Ve_Pdf: {ex.Message}");
                ed.WriteMessage($"\n   Chi tiết: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Xử lý in danh sách Block ra các file PDF độc lập
        /// Tên file có cấu trúc: [tên layout hiện hành]_[số thứ tự].pdf
        /// </summary>
        private static void ExecutePlotBlocks(Document doc, Database db, Editor ed,
            InDwgToPdfForm form, List<BlockPrintItem> validBlocks)
        {
            short oldBgPlot = 0;
            short oldPdfShx = 1;
            short oldTransparencyOverride = 1;

            // 1. Quản lý các biến hệ thống để tối ưu tốc độ in
            try
            {
                object bgVal = Application.GetSystemVariable("BACKGROUNDPLOT");
                if (bgVal is short sVal) oldBgPlot = sVal;
                else if (bgVal is int iVal) oldBgPlot = (short)iVal;
                Application.SetSystemVariable("BACKGROUNDPLOT", 0);
            }
            catch { }

            try
            {
                object shxVal = Application.GetSystemVariable("PDFSHX");
                if (shxVal is short sVal) oldPdfShx = sVal;
                else if (shxVal is int iVal) oldPdfShx = (short)iVal;

                if (form.FastMode)
                {
                    // Tắt tạo ghi chú comment cho font SHX trong PDF -> Tăng tốc in 3-5 lần!
                    Application.SetSystemVariable("PDFSHX", 0);
                }
            }
            catch { }

            try
            {
                object transVal = Application.GetSystemVariable("PLOTTRANSPARENCYOVERRIDE");
                if (transVal is short sVal) oldTransparencyOverride = sVal;
                else if (transVal is int iVal) oldTransparencyOverride = (short)iVal;

                if (form.FastMode)
                {
                    // Tắt làm phẳng độ trong suốt (transparency flattening) để tránh rasterize chậm chạp
                    Application.SetSystemVariable("PLOTTRANSPARENCYOVERRIDE", 0);
                }
            }
            catch { }

            var swTotal = Stopwatch.StartNew();

            try
            {
                if (PlotFactory.ProcessPlotState != ProcessPlotState.NotPlotting)
                {
                    ed.WriteMessage("\n⚠️ Hệ thống in AutoCAD đang bận xử lý tác vụ khác. Vui lòng đợi trong giây lát và thử lại.");
                    return;
                }

                // Xác định chính xác Layout hiện hành
                LayoutManager lm = LayoutManager.Current;
                ObjectId layoutId = ObjectId.Null;
                string currentLayoutName = db.TileMode ? "Model" : lm.CurrentLayout;
                bool isModel = db.TileMode;

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    if (isModel)
                    {
                        BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                        BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                        layoutId = btr.LayoutId;
                    }
                    else
                    {
                        DBDictionary layoutDict = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
                        layoutId = layoutDict.GetAt(currentLayoutName);
                    }
                    tr.Commit();
                }

                if (layoutId == ObjectId.Null)
                {
                    ed.WriteMessage("\n❌ Không tìm thấy không gian Layout hiện hành để in.");
                    return;
                }

                ed.WriteMessage("\n═══════════════════════════════════════════════════════════════");
                ed.WriteMessage($"\n🚀 Bắt đầu in {validBlocks.Count} bản vẽ ({currentLayoutName}) bằng '{form.SelectedDeviceName}'...");
                ed.WriteMessage($"\n   - Cấu trúc tên: [{currentLayoutName}_[STT].pdf]");
                ed.WriteMessage($"\n   - Tối ưu tốc độ: {(form.FastMode ? "BẬT (Tắt PDFSHX & Transparency)" : "TẮT")}");
                ed.WriteMessage($"\n   - Nét in CTB: {form.SelectedPlotStyle}");
                ed.WriteMessage($"\n   - Thư mục lưu: {form.OutputFolder}");
                ed.WriteMessage("\n───────────────────────────────────────────────────────────────\n");

                int successCount = 0;
                int failCount = 0;
                var createdFiles = new List<string>();

                string indexFormat = $"D{form.Digits}";
                int startIndex = form.StartIndex;

                for (int i = 0; i < validBlocks.Count; i++)
                {
                    var block = validBlocks[i];
                    int currentNum = startIndex + i;
                    var swItem = Stopwatch.StartNew();

                    // CẤU TRÚC TIỀN TỐ FILE: [tên layout hiện hành]_[số thứ tự].pdf
                    string fileName = $"{currentLayoutName}_{currentNum.ToString(indexFormat)}.pdf";

                    // Khử ký tự không hợp lệ nếu tên layout có ký tự cấm của Windows
                    foreach (char c in Path.GetInvalidFileNameChars())
                    {
                        fileName = fileName.Replace(c, '_');
                    }

                    string fullPdfPath = Path.Combine(form.OutputFolder, fileName);

                    try
                    {
                        using (Transaction tr = db.TransactionManager.StartTransaction())
                        {
                            Layout layout = (Layout)tr.GetObject(layoutId, OpenMode.ForRead);
                            using (PlotSettings ps = new PlotSettings(layout.ModelType))
                            {
                                ps.CopyFrom(layout);
                                PlotSettingsValidator psv = PlotSettingsValidator.Current;

                                // 1. Tìm Canonical Media Name chuẩn không lề (full bleed) cho khổ giấy
                                string paperName = string.IsNullOrEmpty(block.PaperName) || block.PaperName == "Chưa rõ" ? "A3" : block.PaperName;
                                string canonicalMedia = ResolveCanonicalMediaName(psv, ps, paperName, block.IsLandscape);
                                if (string.IsNullOrEmpty(canonicalMedia))
                                {
                                    canonicalMedia = !string.IsNullOrEmpty(block.CanonicalMediaName)
                                        ? block.CanonicalMediaName
                                        : (block.IsLandscape ? "ISO_full_bleed_A3_(420.00_x_297.00_MM)" : "ISO_full_bleed_A3_(297.00_x_420.00_MM)");
                                }

                                // 2. Cấu hình Plotter DWG To PDF.pc3 & Khổ giấy tự nhận diện
                                psv.SetPlotConfigurationName(ps, form.SelectedDeviceName, canonicalMedia);

                                // 3. Vùng in Window theo extents của Block
                                psv.SetPlotType(ps, Autodesk.AutoCAD.DatabaseServices.PlotType.Window);
                                psv.SetPlotWindowArea(ps, new Extents2d(block.MinPoint.X, block.MinPoint.Y, block.MaxPoint.X, block.MaxPoint.Y));

                                // 4. Tỷ lệ in Fit vào trang giấy đã nhận diện
                                psv.SetUseStandardScale(ps, true);
                                psv.SetStdScaleType(ps, StdScaleType.ScaleToFit);

                                // 5. Căn giữa bản vẽ
                                psv.SetPlotCentered(ps, form.CenterPlot);

                                // 6. Hướng trang in tương ứng với chiều dài/rộng block
                                bool isCanonicalAlreadyPortrait = canonicalMedia.Contains("210.00_x_297.00")
                                    || canonicalMedia.Contains("297.00_x_420.00");

                                PlotRotation rotation = isCanonicalAlreadyPortrait
                                    ? (block.IsLandscape ? PlotRotation.Degrees090 : PlotRotation.Degrees000)
                                    : (block.IsLandscape ? PlotRotation.Degrees000 : PlotRotation.Degrees090);

                                psv.SetPlotRotation(ps, rotation);

                                // 7. Bảng nét in CTB, độ dày nét và độ trong suốt
                                if (!string.IsNullOrEmpty(form.SelectedPlotStyle) && !form.SelectedPlotStyle.Equals("None", StringComparison.OrdinalIgnoreCase))
                                {
                                    try
                                    {
                                        psv.SetCurrentStyleSheet(ps, form.SelectedPlotStyle);
                                    }
                                    catch { }
                                }

                                ps.PlotPlotStyles = form.PlotWithPlotStyles;
                                ps.PrintLineweights = form.PlotWithLineweights;
                                ps.PlotTransparency = !form.FastMode;

                                // 8. Chuẩn bị PlotInfo & Validate
                                PlotInfo pi = new PlotInfo();
                                pi.Layout = layoutId;
                                pi.OverrideSettings = ps;

                                PlotInfoValidator piv = new PlotInfoValidator();
                                piv.MediaMatchingPolicy = MatchingPolicy.MatchEnabled;
                                piv.Validate(pi);

                                // 9. Thực thi xuất từng file PDF
                                using (PlotEngine pe = PlotFactory.CreatePublishEngine())
                                {
                                    pe.BeginPlot(null, null);
                                    pe.BeginDocument(pi, doc.Name, null, 1, true, fullPdfPath);

                                    PlotPageInfo ppi = new PlotPageInfo();
                                    pe.BeginPage(ppi, pi, true, null);
                                    pe.BeginGenerateGraphics(null);
                                    pe.EndGenerateGraphics(null);
                                    pe.EndPage(null);

                                    pe.EndDocument(null);
                                    pe.EndPlot(null);
                                }
                            }
                            tr.Commit();
                        }

                        swItem.Stop();
                        successCount++;
                        createdFiles.Add(fullPdfPath);
                        ed.WriteMessage($"\n   [✓] Bản vẽ {currentNum.ToString(indexFormat)}/{validBlocks.Count} [{block.PaperName} {block.OrientationText}] ({swItem.Elapsed.TotalSeconds:F1}s) -> {fileName}");
                    }
                    catch (System.Exception ex)
                    {
                        failCount++;
                        ed.WriteMessage($"\n   [✗] Bản vẽ {currentNum.ToString(indexFormat)} in thất bại: {ex.Message}");
                    }
                }

                swTotal.Stop();
                double avgSec = successCount > 0 ? swTotal.Elapsed.TotalSeconds / successCount : 0;

                ed.WriteMessage($"\n\n═══════════════════════════════════════════════════════════════");
                ed.WriteMessage($"\n  🎉 HOÀN TẤT IN BẢN VẼ PDF: {successCount} thành công, {failCount} thất bại.");
                ed.WriteMessage($"\n  ⏱️ Tổng thời gian: {swTotal.Elapsed.TotalSeconds:F1}s (Trung bình: {avgSec:F1}s/bản vẽ)");
                ed.WriteMessage($"\n  📁 Thư mục lưu: {form.OutputFolder}");
                ed.WriteMessage($"\n═══════════════════════════════════════════════════════════════\n");

                // Tự động mở thư mục lưu file PDF nếu được chọn
                if (form.OpenFolderAfterPlot && Directory.Exists(form.OutputFolder))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = form.OutputFolder,
                            UseShellExecute = true
                        });
                    }
                    catch { }
                }
            }
            finally
            {
                // Khôi phục các biến hệ thống về trạng thái ban đầu
                try
                {
                    Application.SetSystemVariable("BACKGROUNDPLOT", oldBgPlot);
                    Application.SetSystemVariable("PDFSHX", oldPdfShx);
                    Application.SetSystemVariable("PLOTTRANSPARENCYOVERRIDE", oldTransparencyOverride);
                }
                catch { }
            }
        }

        /// <summary>
        /// Tìm Canonical Media Name chuẩn của thiết bị in (Ưu tiên ISO full bleed không lề)
        /// </summary>
        private static string ResolveCanonicalMediaName(PlotSettingsValidator psv, PlotSettings ps, string paperCode, bool isLandscape)
        {
            try
            {
                var mediaList = psv.GetCanonicalMediaNameList(ps);
                if (mediaList == null || mediaList.Count == 0) return "";

                // 1. Ưu tiên ISO full bleed (lề 0mm không bị viền trắng)
                string fullBleedTag = $"full_bleed_{paperCode}";
                foreach (string m in mediaList)
                {
                    if (m.IndexOf(fullBleedTag, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (paperCode.Equals("A4", StringComparison.OrdinalIgnoreCase))
                        {
                            bool isMediaLandscape = m.Contains("297") && m.IndexOf("297", StringComparison.OrdinalIgnoreCase) < m.IndexOf("210", StringComparison.OrdinalIgnoreCase);
                            if (isLandscape == isMediaLandscape) return m;
                        }
                        else
                        {
                            return m;
                        }
                    }
                }

                // 2. Tìm ISO expand
                string expandTag = $"expand_{paperCode}";
                foreach (string m in mediaList)
                {
                    if (m.IndexOf(expandTag, StringComparison.OrdinalIgnoreCase) >= 0)
                        return m;
                }

                // 3. Tìm ISO chuẩn
                string isoTag = $"ISO_{paperCode}";
                foreach (string m in mediaList)
                {
                    if (m.IndexOf(isoTag, StringComparison.OrdinalIgnoreCase) >= 0)
                        return m;
                }

                // 4. Tìm bất kỳ media nào chứa tên khổ giấy
                foreach (string m in mediaList)
                {
                    if (m.IndexOf(paperCode, StringComparison.OrdinalIgnoreCase) >= 0)
                        return m;
                }

                return mediaList[0];
            }
            catch
            {
                return "";
            }
        }
    }
}

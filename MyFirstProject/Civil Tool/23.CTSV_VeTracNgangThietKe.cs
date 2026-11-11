// (C) Copyright 2015 by  
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Autodesk.AutoCAD.Runtime;
using Acad = Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;
using ATable = Autodesk.AutoCAD.DatabaseServices.Table;

using Civil = Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using Autodesk.Civil.Runtime;
using Autodesk.Civil.Settings;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.Civil.ApplicationServices;
using CivSurface = Autodesk.Civil.DatabaseServices.TinSurface;
using Section = Autodesk.Civil.DatabaseServices.Section;
using Autodesk.Civil;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using MyFirstProject.Extensions;
using MyFirstProject.Civil_Tool;

// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3DCsharp.CTSV_VeTracNgangThietKe_Commands))]

namespace Civil3DCsharp
{
    public class CTSV_VeTracNgangThietKe_Commands
    {
        [CommandMethod("CTS_Ve_Trac_Ngang_Thiet_Ke")]
        public static void CTSV_VeTracNgangThietKe()
        {
            try
            {
                // Show form to get user inputs OUTSIDE of transaction
                using SectionViewDesignForm form = new();

                // Use ShowDialog instead of Application.Run
                var dialogResult = form.ShowDialog();

                if (dialogResult != System.Windows.Forms.DialogResult.OK || !form.DialogResultOK)
                {
                    A.Ed.WriteMessage("\nLệnh đã bị hủy bỏ.");
                    return;
                }

                A.Ed.WriteMessage("\nBắt đầu tạo trắc ngang thiết kế...");

                // Validate form inputs first
                if (form.AlignmentId == ObjectId.Null || !form.AlignmentId.IsValid)
                {
                    A.Ed.WriteMessage("\nAlignment ID không hợp lệ.");
                    return;
                }

                // Now start transaction for the main work
                using Transaction tr = A.Db.TransactionManager.StartTransaction();
                try
                {
                    UserInput UI = new();
                    UtilitiesCAD CAD = new();
                    UtilitiesC3D C3D = new();

                // Get inputs from form
                ObjectId alignmentId = form.AlignmentId;
                Point3d point3D = form.PlacementPoint;

                SectionViewGroupCreationPlacementOptions sectionViewGroupCreationPlacementOptions = new();
                sectionViewGroupCreationPlacementOptions.UseProductionPlacement(form.LayoutTemplatePath, form.LayoutName);

                Alignment? alignment = tr.GetObject(alignmentId, OpenMode.ForRead) as Alignment;
                if (alignment == null)
                {
                    A.Ed.WriteMessage("\nKhông thể mở alignment.");
                    return;
                }

                double startStation = alignment.StartingStation;
                double endstation = alignment.EndingStation;

                // Check if alignment has sample line groups
                if (alignment.GetSampleLineGroupIds().Count == 0)
                {
                    A.Ed.WriteMessage("\nAlignment không có sample line group nào.");
                    return;
                }

                ObjectId sampleLineGroupId = alignment.GetSampleLineGroupIds()[0];
                SampleLineGroup? sampleLineGroup = tr.GetObject(sampleLineGroupId, OpenMode.ForWrite) as SampleLineGroup;
                if (sampleLineGroup == null)
                {
                    A.Ed.WriteMessage("\nKhông thể mở sample line group.");
                    return;
                }

                SectionViewGroupCreationRangeOptions sectionViewGroupCreationRangeOptions = new(sampleLineGroupId);

                ObjectIdCollection sectionSourceIdColl = [];
                SectionSourceCollection sectionSources = sampleLineGroup.GetSectionSources();

                //Add_SectionSource - thiết lập các nguồn dữ liệu cho trắc ngang từ form
                A.Ed.WriteMessage($"\nKiểm tra {sectionSources.Count} section sources...");

                foreach (SectionSource sectionsource in sectionSources)
                {
                    try
                    {
                        // Find corresponding config from form
                        var sourceConfig = form.SectionSources?.FirstOrDefault(s => s.SourceId == sectionsource.SourceId);

                        // Debug thông tin về source
                        string sourceName = GetSourceName(sectionsource.SourceId, tr);

                        A.Ed.WriteMessage($"\n  Source: {sourceName}, Type: {sectionsource.SourceType}, Use: {sourceConfig?.UseSource ?? false}");

                        if (sourceConfig != null && sourceConfig.UseSource)
                        {
                            sectionsource.IsSampled = true;

                            // Apply style with priority for All Codes 1-1000 for corridor sources
                            if (sourceConfig.StyleId != ObjectId.Null && sourceConfig.StyleId.IsValid)
                            {
                                sectionsource.StyleId = sourceConfig.StyleId;

                                // Debug: Show applied style name
                                string appliedStyleName = GetStyleName(sourceConfig.StyleId, tr);
                                A.Ed.WriteMessage($"\n    -> Áp dụng style: {appliedStyleName}");
                            }
                            else
                            {
                                // Apply default style with All Codes priority for corridors
                                ObjectId defaultStyleId = GetDefaultStyleForSectionSource(sectionsource, tr, alignment.Name);
                                if (defaultStyleId != ObjectId.Null)
                                {
                                    sectionsource.StyleId = defaultStyleId;
                                    string defaultStyleName = GetStyleName(defaultStyleId, tr);
                                    A.Ed.WriteMessage($"\n    -> Áp dụng default style: {defaultStyleName}");
                                }
                            }

                            // Add to collection for later use - chỉ bao gồm TinSurface, Corridor, CorridorSurface, Material
                            if (sectionsource.SourceType == SectionSourceType.TinSurface ||
                                sectionsource.SourceType == SectionSourceType.Corridor ||
                                sectionsource.SourceType == SectionSourceType.CorridorSurface ||
                                sectionsource.SourceType == SectionSourceType.Material)
                            {
                                sectionSourceIdColl.Add(sectionsource.SourceId);
                                A.Ed.WriteMessage($"\n    -> Đã thêm vào collection: {sourceName}");
                            }
                        }
                        else
                        {
                            sectionsource.IsSampled = false;
                            A.Ed.WriteMessage($"\n    -> Không sử dụng: {sourceName}");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        A.Ed.WriteMessage($"\nLỗi khi thiết lập section source: {ex.Message}");
                        // Continue with other sources
                    }
                }

                A.Ed.WriteMessage($"\nTổng cộng {sectionSourceIdColl.Count} sources được chọn để vẽ trắc ngang.");

                // Tạo nhóm trắc ngang
                SectionViewGroupCollection sectionViewGroupCollection = sampleLineGroup.SectionViewGroups;
                SectionViewGroup? sectionViewGroup = null;

                try
                {
                    sectionViewGroup = sectionViewGroupCollection.Add(point3D, startStation, endstation, sectionViewGroupCreationRangeOptions, sectionViewGroupCreationPlacementOptions);
                }
                catch (System.Exception ex)
                {
                    A.Ed.WriteMessage($"\nLỗi khi tạo section view group: {ex.Message}");
                    return;
                }

                if (sectionViewGroup == null)
                {
                    A.Ed.WriteMessage("\nKhông thể tạo section view group.");
                    return;
                }

                // Cập nhật styles cho section view group từ form
                if (form.PlotStyleId != ObjectId.Null && form.PlotStyleId.IsValid)
                {
                    try
                    {
                        sectionViewGroup.PlotStyleId = form.PlotStyleId;
                    }
                    catch (System.Exception ex)
                    {
                        A.Ed.WriteMessage($"\nKhông thể thiết lập plot style: {ex.Message}");
                    }
                }

                ObjectIdCollection sectionViewIdColl = sectionViewGroup.GetSectionViewIds();

                //surfaceId - khởi tạo các ObjectId cho các mặt
                ObjectId surfaceTnId = ObjectId.Null;
                ObjectId surfaceTopId = ObjectId.Null;
                ObjectId surfaceDatumId = ObjectId.Null;

                // Thêm nhãn cho các section và bands
                foreach (ObjectId sectionviewId in sectionViewIdColl)
                {
                    if (!sectionviewId.IsValid)
                        continue;

                    SectionView? sectionView = tr.GetObject(sectionviewId, OpenMode.ForWrite) as SectionView;
                    if (sectionView == null)
                        continue;

                    // Apply section view style from form
                    if (form.SectionViewStyleId != ObjectId.Null && form.SectionViewStyleId.IsValid)
                    {
                        try
                        {
                            sectionView.StyleId = form.SectionViewStyleId;
                        }
                        catch (System.Exception ex)
                        {
                            A.Ed.WriteMessage($"\nKhông thể thiết lập section view style: {ex.Message}");
                        }
                    }

                    var sampleLineId = sectionView.SampleLineId;
                    if (!sampleLineId.IsValid)
                        continue;

                    SampleLine? sampleLine = tr.GetObject(sampleLineId, OpenMode.ForRead) as SampleLine;
                    if (sampleLine == null)
                        continue;

                    // Thêm nhãn cho các surface
                    AddSurfaceLabels(sectionSourceIdColl, sampleLine, sectionviewId, tr, form.WeedingTop, form.WeedingTN);

                    // Lấy các section ID để sử dụng cho bands
                    var sectionIds = GetSectionIdsForBands(sectionSourceIdColl, sampleLine, tr);
                    ObjectId sectionTnId = sectionIds.TnId;
                    ObjectId sectionTopId = sectionIds.TopId;
                    ObjectId sectionDatumId = sectionIds.DatumId;

                    // Cập nhật surface IDs cho material list
                    if (sectionIds.TnSurfaceId != ObjectId.Null) surfaceTnId = sectionIds.TnSurfaceId;
                    if (sectionIds.TopSurfaceId != ObjectId.Null) surfaceTopId = sectionIds.TopSurfaceId;
                    if (sectionIds.DatumSurfaceId != ObjectId.Null) surfaceDatumId = sectionIds.DatumSurfaceId;

                    // Thêm các bands cho section view nếu được chọn trong form
                    if (form.ImportBandSet && form.BandSetStyleId != ObjectId.Null)
                    {
                        // Use Band Set instead of individual bands
                        ApplyBandStyleToSectionView(sectionView, form.BandSetStyleId);
                    }
                    else if (form.AddElevationBands || form.AddDistanceBands)
                    {
                        // Use individual bands
                        AddSectionBands(sectionView, form, sectionTopId, sectionTnId);
                    }
                }

                // Tạo Material List nếu được yêu cầu
                if (form.CreateMaterialList)
                {
                    CreateMaterialList(sampleLineGroup, form.MaterialListName, surfaceTnId, surfaceDatumId, tr);
                }

                // Tạo bảng khối lượng (Volume Table) nếu được yêu cầu
                if (form.CreateVolumeTable)
                {
                    CreateVolumeTable(sampleLineGroup, sectionViewGroup, form.TablePosition, surfaceTnId, surfaceDatumId, tr);
                }

                A.Ed.WriteMessage("\n Lệnh CTSV_VeTracNgangThietKe đã hoàn thành thành công!");
                A.Ed.WriteMessage("\n💡 Lưu ý: Để tạo corridor surfaces, sử dụng lệnh riêng biệt: CTSV_TaoCorridorSurface");

                tr.Commit();
                }
                catch (Autodesk.AutoCAD.Runtime.Exception e)
                {
                    A.Ed.WriteMessage($"\n Lỗi AutoCAD khi thực hiện lệnh: {e.Message}");
                    tr.Abort();
                }
                catch (System.Exception e)
                {
                    A.Ed.WriteMessage($"\n Lỗi khi thực hiện lệnh: {e.Message}");
                    tr.Abort();
                }
            }
            catch (System.Exception e)
            {
                A.Ed.WriteMessage($"\n Lỗi khởi tạo lệnh: {e.Message}");
            }
        }

        // Helper method to get source name
        private static string GetSourceName(ObjectId sourceId, Transaction tr)
        {
            try
            {
                if (sourceId == ObjectId.Null || !sourceId.IsValid)
                    return "Invalid Source";

                var entity = tr.GetObject(sourceId, OpenMode.ForRead);
                if (entity is TinSurface surface)
                    return surface.Name ?? "Unnamed Surface";
                else if (entity is Corridor corridor)
                    return corridor.Name ?? "Unnamed Corridor";
                else
                {
                    // Use reflection to get Name property
                    var nameProperty = entity.GetType().GetProperty("Name");
                    if (nameProperty != null)
                    {
                        return nameProperty.GetValue(entity)?.ToString() ?? $"Other ({entity.GetType().Name})";
                    }
                    return $"Other ({entity.GetType().Name})";
                }
            }
            catch
            {
                return "Error Reading Source";
            }
        }

        // Helper method to create material list
        private static void CreateMaterialList(SampleLineGroup sampleLineGroup, string materialListName, ObjectId surfaceTnId, ObjectId surfaceDatumId, Transaction tr)
        {
            try
            {
                A.Ed.WriteMessage($"\n=== Chuẩn bị tạo Material List: {materialListName} ===");

                // Check if we have the required surfaces
                if (surfaceTnId == ObjectId.Null || surfaceDatumId == ObjectId.Null)
                {
                    A.Ed.WriteMessage("\n⚠️ Cần có EG/TN surface và DATUM surface để tạo material list.");

                    // Try to find surfaces from section sources
                    var foundSurfaces = FindSurfacesForMaterialList(sampleLineGroup, tr);
                    if (foundSurfaces.EgSurfaceId != ObjectId.Null && foundSurfaces.DatumSurfaceId != ObjectId.Null)
                    {
                        surfaceTnId = foundSurfaces.EgSurfaceId;
                        surfaceDatumId = foundSurfaces.DatumSurfaceId;
                        A.Ed.WriteMessage($"\n✅ Đã tìm thấy surfaces: EG={GetSurfaceName(surfaceTnId, tr)}, DATUM={GetSurfaceName(surfaceDatumId, tr)}");
                    }
                    else
                    {
                        A.Ed.WriteMessage("\n❌ Không tìm thấy surfaces phù hợp. Bỏ qua tạo material list.");
                        return;
                    }
                }

                // Display instructions for manual material list creation
                A.Ed.WriteMessage($"\n📋 Hướng dẫn tạo Material List thủ công:");
                A.Ed.WriteMessage($"\n1. Trong Prospector, mở rộng Sample Line Groups");
                A.Ed.WriteMessage($"\n2. Right-click vào Sample Line Group > Properties");
                A.Ed.WriteMessage($"\n3. Chuyển đến tab 'Material List'");
                A.Ed.WriteMessage($"\n4. Click 'Add new material' để tạo material list mới");
                A.Ed.WriteMessage($"\n5. Đặt tên: '{materialListName}'");
                A.Ed.WriteMessage($"\n");
                A.Ed.WriteMessage($"\n💡 Cấu trúc Material List cần tạo:");
                A.Ed.WriteMessage($"\n   📁 Đào đất");
                A.Ed.WriteMessage($"\n      ├─ EG ({GetSurfaceName(surfaceTnId, tr)}) - Condition: Below, Type: Cut");
                A.Ed.WriteMessage($"\n      └─ DATUM ({GetSurfaceName(surfaceDatumId, tr)}) - Condition: Above, Type: Cut");
                A.Ed.WriteMessage($"\n   📁 Đắp đất");
                A.Ed.WriteMessage($"\n      ├─ EG ({GetSurfaceName(surfaceTnId, tr)}) - Condition: Above, Type: Fill");
                A.Ed.WriteMessage($"\n      └─ DATUM ({GetSurfaceName(surfaceDatumId, tr)}) - Condition: Below, Type: Fill");
                A.Ed.WriteMessage($"\n");
                A.Ed.WriteMessage($"\n✅ Đã chuẩn bị thông tin để tạo Material List thủ công");

                // Alternative: Try to use Civil 3D's built-in material creation
                try
                {
                    CreateMaterialListProgrammatically(sampleLineGroup, materialListName, surfaceTnId, surfaceDatumId, tr);
                }
                catch (System.Exception ex)
                {
                    A.Ed.WriteMessage($"\n⚠️ Không thể tạo material list tự động: {ex.Message}");
                    A.Ed.WriteMessage($"\n💡 Vui lòng tạo material list thủ công theo hướng dẫn ở trên.");
                }
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n❌ Lỗi khi chuẩn bị Material List: {ex.Message}");
            }
        }

        // Helper method to attempt programmatic material list creation
        private static void CreateMaterialListProgrammatically(SampleLineGroup sampleLineGroup, string materialListName, ObjectId surfaceTnId, ObjectId surfaceDatumId, Transaction tr)
        {
            try
            {
                A.Ed.WriteMessage($"\n🔄 Thử tạo Material List tự động...");

                // For now, just provide detailed instructions since the exact API varies between Civil 3D versions
                A.Ed.WriteMessage($"\n📋 Thông tin cần thiết để tạo Material List:");
                A.Ed.WriteMessage($"\n   - Sample Line Group: {sampleLineGroup.Name}");
                A.Ed.WriteMessage($"\n   - Material List Name: {materialListName}");
                A.Ed.WriteMessage($"\n   - EG/TN Surface: {GetSurfaceName(surfaceTnId, tr)} (ID: {surfaceTnId})");
                A.Ed.WriteMessage($"\n   - DATUM Surface: {GetSurfaceName(surfaceDatumId, tr)} (ID: {surfaceDatumId})");

                A.Ed.WriteMessage($"\n💡 Material List này sẽ tính toán khối lượng đào đắp giữa 2 surfaces:");
                A.Ed.WriteMessage($"\n   • Đào đất: Vùng dưới EG và trên DATUM");
                A.Ed.WriteMessage($"\n   • Đắp đất: Vùng trên EG và dưới DATUM");

                A.Ed.WriteMessage($"\n⚠️ Chức năng tạo Material List tự động sẽ được cải thiện trong phiên bản sau.");
                A.Ed.WriteMessage($"\n💡 Hiện tại vui lòng tạo thủ công theo hướng dẫn ở trên.");
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n❌ Lỗi trong CreateMaterialListProgrammatically: {ex.Message}");
                throw;
            }
        }

        // Helper method to find surfaces for material list
        private static (ObjectId EgSurfaceId, ObjectId DatumSurfaceId) FindSurfacesForMaterialList(SampleLineGroup sampleLineGroup, Transaction tr)
        {
            ObjectId egSurfaceId = ObjectId.Null;
            ObjectId datumSurfaceId = ObjectId.Null;

            try
            {
                SectionSourceCollection sectionSources = sampleLineGroup.GetSectionSources();

                foreach (SectionSource sectionSource in sectionSources)
                {
                    if (sectionSource.SourceType == SectionSourceType.TinSurface && sectionSource.IsSampled)
                    {
                        string sourceName = GetSourceName(sectionSource.SourceId, tr).ToLower();

                        if ((sourceName.Contains("eg") || sourceName.Contains("tn")) && egSurfaceId == ObjectId.Null)
                        {
                            egSurfaceId = sectionSource.SourceId;
                            A.Ed.WriteMessage($"\n  Found EG/TN surface: {GetSourceName(sectionSource.SourceId, tr)}");
                        }
                        else if (sourceName.Contains("datum") && datumSurfaceId == ObjectId.Null)
                        {
                            datumSurfaceId = sectionSource.SourceId;
                            A.Ed.WriteMessage($"\n  Found DATUM surface: {GetSourceName(sectionSource.SourceId, tr)}");
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi khi tìm surfaces: {ex.Message}");
            }

            return (egSurfaceId, datumSurfaceId);
        }

        // Helper method to get surface name
        private static string GetSurfaceName(ObjectId surfaceId, Transaction tr)
        {
            try
            {
                if (surfaceId == ObjectId.Null || !surfaceId.IsValid)
                    return "Invalid Surface";

                var surface = tr.GetObject(surfaceId, OpenMode.ForRead) as TinSurface;
                return surface?.Name ?? "Unknown Surface";
            }
            catch
            {
                return "Error Reading Surface";
            }
        }

        // Helper method to get style name from ObjectId
        private static string GetStyleName(ObjectId styleId, Transaction tr)
        {
            try
            {
                if (styleId == ObjectId.Null || !styleId.IsValid)
                    return "(No Style)";

                var entity = tr.GetObject(styleId, OpenMode.ForRead);
                var nameProperty = entity.GetType().GetProperty("Name");
                if (nameProperty != null)
                {
                    return nameProperty.GetValue(entity)?.ToString() ?? "Unknown Style";
                }
                return "Unknown Style";
            }
            catch
            {
                return "Error Reading Style";
            }
        }

        // Helper method to get default style for section source with All Codes priority
        private static ObjectId GetDefaultStyleForSectionSource(SectionSource sectionSource, Transaction tr, string alignmentName)
        {
            try
            {
                string sourceName = GetSourceName(sectionSource.SourceId, tr);

                if (sectionSource.SourceType == SectionSourceType.TinSurface)
                {
                    if (sourceName.Contains("EG") || sourceName.Contains("TN"))
                    {
                        try { return A.Cdoc.Styles.SectionStyles["1.TN Ground"]; }
                        catch { /* Style not found, continue to fallback */ }
                    }
                    if (sourceName.Contains("top"))
                    {
                        try { return A.Cdoc.Styles.SectionStyles["2.Top Ground"]; }
                        catch { /* Style not found, continue to fallback */ }
                    }
                    if (sourceName.Contains("datum"))
                    {
                        try { return A.Cdoc.Styles.SectionStyles["3.Datum Ground"]; }
                        catch { /* Style not found, continue to fallback */ }
                    }
                }
                else if (sectionSource.SourceType == SectionSourceType.Corridor)
                {
                    // Priority 1: All Codes 1-1000 style for corridor sources
                    ObjectId allCodesStyleId = GetAllCodesStyleForSectionSource();
                    if (allCodesStyleId != ObjectId.Null)
                    {
                        A.Ed.WriteMessage($"\n      -> Corridor sử dụng All Codes style");
                        return allCodesStyleId;
                    }

                    // Fallback for corridor
                    if (sourceName.Contains(alignmentName))
                    {
                        try { return A.Cdoc.Styles.CodeSetStyles["1. All Codes 1-1000"]; }
                        catch { /* Style not found, continue to fallback */ }
                    }
                }
                else if (sectionSource.SourceType == SectionSourceType.CorridorSurface)
                {
                    // For corridor surfaces, also try All Codes first
                    ObjectId allCodesStyleId = GetAllCodesStyleForSectionSource();
                    if (allCodesStyleId != ObjectId.Null)
                    {
                        A.Ed.WriteMessage($"\n      -> Corridor Surface sử dụng All Codes style");
                        return allCodesStyleId;
                    }

                    if (sourceName.Contains("top"))
                    {
                        try { return A.Cdoc.Styles.SectionStyles["2.Top Ground"]; }
                        catch { /* Style not found, continue to fallback */ }
                    }
                    if (sourceName.Contains("datum"))
                    {
                        try { return A.Cdoc.Styles.SectionStyles["3.Datum Ground"]; }
                        catch { /* Style not found, continue to fallback */ }
                    }
                }
                else if (sectionSource.SourceType == SectionSourceType.Material)
                {
                    // Default section style for material
                    try { return A.Cdoc.Styles.SectionStyles["Standard"]; }
                    catch { /* Style not found, continue to fallback */ }
                }
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi khi lấy default style: {ex.Message}");
            }

            return ObjectId.Null;
        }

        // Helper method to get All Codes style for section sources
        private static ObjectId GetAllCodesStyleForSectionSource()
        {
            try
            {
                // Try CodeSet styles first (preferred for corridor sources)
                var codeSetStyles = A.Cdoc.Styles.CodeSetStyles;

                // Priority style names for All Codes
                string[] allCodesStyleNames = {
                    "1. All Codes 1-1000",
                    "All Codes 1-1000",
                    "1.All Codes 1-1000",
                    "All Codes",
                    "1. All Codes",
                    "ALL CODES 1-1000"
                };

                foreach (string styleName in allCodesStyleNames)
                {
                    if (codeSetStyles.Contains(styleName))
                    {
                        A.Ed.WriteMessage($"\n      -> Tìm thấy CodeSet style: {styleName}");
                        return codeSetStyles[styleName];
                    }
                }

                A.Ed.WriteMessage($"\n      -> Không tìm thấy All Codes 1-1000 style");
                return ObjectId.Null;
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi khi tìm All Codes style: {ex.Message}");
                return ObjectId.Null;
            }
        }

        // Helper method to add surface labels
        private static void AddSurfaceLabels(ObjectIdCollection sectionSourceIdColl, SampleLine sampleLine, ObjectId sectionviewId, Transaction tr, double weedingTop, double weedingTN)
        {
            foreach (ObjectId sectionsourcceId in sectionSourceIdColl)
            {
                if (!sectionsourcceId.IsValid)
                    continue;

                try
                {
                    string surfaceName = GetSourceName(sectionsourcceId, tr);

                    if (surfaceName.ToLower().Contains("tn") || surfaceName.ToLower().Contains("eg"))
                    {
                        ObjectId sectionId = sampleLine.GetSectionId(sectionsourcceId);
                        if (sectionId.IsValid)
                        {
                            CreateSurfaceLabel(sectionviewId, sectionId, "Duong giong (EG)", weedingTN);
                        }
                    }
                    else if (surfaceName.ToLower().Contains("top"))
                    {
                        ObjectId sectionId = sampleLine.GetSectionId(sectionsourcceId);
                        if (sectionId.IsValid)
                        {
                            CreateSurfaceLabel(sectionviewId, sectionId, "Duong giong", weedingTop);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    A.Ed.WriteMessage($"\nLỗi khi thêm label cho surface: {ex.Message}");
                }
            }
        }

        // Helper method to create surface labels
        private static void CreateSurfaceLabel(ObjectId sectionviewId, ObjectId sectionId, string styleName, double weeding)
        {
            try
            {
                var styleCollection = A.Cdoc.Styles.LabelStyles.SectionLabelStyles.GradeBreakLabelStyles;
                if (styleCollection.Contains(styleName))
                {
                    using (var tr = A.Db.TransactionManager.TopTransaction)
                    {
                        ObjectId labelId = SectionGradeBreakLabelGroup.Create(sectionviewId, sectionId, styleCollection[styleName]);
                        SectionGradeBreakLabelGroup? labelGroup = tr.GetObject(labelId, OpenMode.ForWrite) as SectionGradeBreakLabelGroup;
                        if (labelGroup != null)
                        {
#pragma warning disable CS0618 // Type or member is obsolete
                            labelGroup.DefaultDimensionAnchorOption = DimensionAnchorOptionType.ViewBottom;
#pragma warning restore CS0618 // Type or member is obsolete
                            labelGroup.Weeding = weeding;
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nKhông thể tạo label với style '{styleName}': {ex.Message}");
            }
        }

        // Helper struct for section IDs
        private struct SectionIdsResult
        {
            public ObjectId TnId;
            public ObjectId TopId;
            public ObjectId DatumId;
            public ObjectId TnSurfaceId;
            public ObjectId TopSurfaceId;
            public ObjectId DatumSurfaceId;
        }

        // Helper method to get section IDs for bands
        private static SectionIdsResult GetSectionIdsForBands(ObjectIdCollection sectionSourceIdColl, SampleLine sampleLine, Transaction tr)
        {
            var result = new SectionIdsResult();

            foreach (ObjectId sectionsourcceId in sectionSourceIdColl)
            {
                if (!sectionsourcceId.IsValid)
                    continue;

                try
                {
                    string surfaceName = GetSourceName(sectionsourcceId, tr);

                    if (surfaceName.ToLower().Contains("tn") || surfaceName.ToLower().Contains("eg"))
                    {
                        result.TnId = sampleLine.GetSectionId(sectionsourcceId);
                        result.TnSurfaceId = sectionsourcceId;
                        A.Ed.WriteMessage($"\nTìm thấy TN/EG surface: {surfaceName}, Section ID: {result.TnId}");
                    }
                    else if (surfaceName.ToLower().Contains("top"))
                    {
                        result.TopId = sampleLine.GetSectionId(sectionsourcceId);
                        result.TopSurfaceId = sectionsourcceId;
                        A.Ed.WriteMessage($"\nTìm thấy TOP surface: {surfaceName}, Section ID: {result.TopId}");
                    }
                    else if (surfaceName.ToLower().Contains("datum"))
                    {
                        result.DatumId = sampleLine.GetSectionId(sectionsourcceId);
                        result.DatumSurfaceId = sectionsourcceId;
                        A.Ed.WriteMessage($"\nTìm thấy DATUM surface: {surfaceName}, Section ID: {result.DatumId}");
                    }
                }
                catch (System.Exception ex)
                {
                    A.Ed.WriteMessage($"\nLỗi khi lấy section ID: {ex.Message}");
                }
            }

            return result;
        }

        // Helper method to add section bands
        private static void AddSectionBands(SectionView sectionView, SectionViewDesignForm form, ObjectId sectionTopId, ObjectId sectionTnId)
        {
            if (sectionTopId.IsValid && sectionTnId.IsValid)
            {
                if (form.AddElevationBands)
                {
                    try
                    {
                        UtilitiesC3D.AddSectionBand(sectionView, "Cao do thiet ke 1-1000", 0, sectionTopId, sectionTnId, 0, form.WeedingTop);
                    }
                    catch (System.Exception ex)
                    {
                        A.Ed.WriteMessage($"\nKhông thể thêm elevation band cho thiết kế: {ex.Message}");
                    }
                }
                if (form.AddDistanceBands)
                {
                    try
                    {
                        UtilitiesC3D.AddSectionBand(sectionView, "Khoang cach mia TK 1-1000", 1, sectionTopId, sectionTnId, 0, form.WeedingTop);
                    }
                    catch (System.Exception ex)
                    {
                        A.Ed.WriteMessage($"\nKhông thể thêm distance band cho thiết kế: {ex.Message}");
                    }
                }
            }

            if (sectionTnId.IsValid)
            {
                if (form.AddElevationBands)
                {
                    try
                    {
                        UtilitiesC3D.AddSectionBand(sectionView, "Cao do tu nhien 1-1000", 2, sectionTnId, sectionTnId, 0, form.WeedingTN);
                    }
                    catch (System.Exception ex)
                    {
                        A.Ed.WriteMessage($"\nKhông thể thêm elevation band cho tự nhiên: {ex.Message}");
                    }
                }
                if (form.AddDistanceBands)
                {
                    try
                    {
                        UtilitiesC3D.AddSectionBand(sectionView, "Khoang cach mia TN 1-1000", 3, sectionTnId, sectionTnId, 0, form.WeedingTN);
                    }
                    catch (System.Exception ex)
                    {
                        A.Ed.WriteMessage($"\nKhông thể thêm distance band cho tự nhiên: {ex.Message}");
                    }
                }
            }
        }

        // Helper method to apply band set style to section view using ImportBandSetStyle
        private static void ApplyBandStyleToSectionView(SectionView sectionView, ObjectId bandSetId)
        {
            try
            {
                if (bandSetId == ObjectId.Null || !bandSetId.IsValid)
                {
                    A.Ed.WriteMessage($"\nBand Set ID không hợp lệ, bỏ qua import band set.");
                    return;
                }

                A.Ed.WriteMessage($"\nImport Band Set cho section view: {sectionView.Name}");

                // Use the correct API method: SectionView.Bands.ImportBandSetStyle(ObjectId)
                // SectionView.Bands returns SectionViewBandSet which has ImportBandSetStyle method
                sectionView.Bands.ImportBandSetStyle(bandSetId);

                A.Ed.WriteMessage($"\n✅ Đã import Band Set thành công.");
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi khi import Band Set: {ex.Message}");
                A.Ed.WriteMessage($"\n💡 Sử dụng individual bands thay thế.");
            }
        }

        // Helper method to get band style name from ObjectId
        private static string GetBandStyleName(ObjectId bandStyleId)
        {
            try
            {
                using (var tr = A.Db.TransactionManager.StartTransaction())
                {
                    var entity = tr.GetObject(bandStyleId, OpenMode.ForRead);
                    var nameProperty = entity.GetType().GetProperty("Name");
                    if (nameProperty != null)
                    {
                        return nameProperty.GetValue(entity)?.ToString() ?? "Unknown Band Style";
                    }
                    tr.Commit();
                }
            }
            catch
            {
                // Fallback to a default style name
            }
            return "Standard";
        }

        // Helper method to create volume table
        private static void CreateVolumeTable(SampleLineGroup sampleLineGroup, SectionViewGroup sectionViewGroup, string tablePosition, ObjectId surfaceTnId, ObjectId surfaceDatumId, Transaction tr)
        {
            try
            {
                A.Ed.WriteMessage($"\n=== Chuẩn bị tạo Bảng khối lượng ===");

                // Kiểm tra surfaces
                if (surfaceTnId == ObjectId.Null || surfaceDatumId == ObjectId.Null)
                {
                    A.Ed.WriteMessage("\n⚠️ Cần có cả EG/TN surface và DATUM surface để tính khối lượng.");
                    A.Ed.WriteMessage("\n💡 Vui lòng đảm bảo đã chọn các surfaces cần thiết.");
                    return;
                }

                // Lấy vị trí của section view group để đặt bảng
                Point3d tableInsertPoint = CalculateTablePosition(sectionViewGroup, tablePosition, tr);

                // Thu thập và tính khối lượng từ các sections
                var volumeData = CalculateVolumeFromSections(sampleLineGroup, surfaceTnId, surfaceDatumId, tr);

                if (volumeData.TotalCut == 0 && volumeData.TotalFill == 0)
                {
                    A.Ed.WriteMessage("\n⚠️ Không có dữ liệu khối lượng để tạo bảng.");
                    A.Ed.WriteMessage("\n💡 Đảm bảo đã có sections được sampled cho các surfaces.");
                    return;
                }

                // Tạo bảng khối lượng
                CreateVolumeTableFromData(volumeData, tableInsertPoint, sampleLineGroup.Name, tr);

                A.Ed.WriteMessage($"\n✅ Đã tạo bảng khối lượng tại vị trí {tablePosition}.");
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n❌ Lỗi khi tạo bảng khối lượng: {ex.Message}");
            }
        }

        // Struct to hold volume calculation results
        private struct VolumeCalculationResult
        {
            public double TotalCut;
            public double TotalFill;
            public List<(double Station, string StakeName, double CutArea, double FillArea)> SectionData;
        }

        // Calculate volume from sections
        private static VolumeCalculationResult CalculateVolumeFromSections(SampleLineGroup sampleLineGroup, ObjectId surfaceTnId, ObjectId surfaceDatumId, Transaction tr)
        {
            var result = new VolumeCalculationResult
            {
                SectionData = []
            };

            try
            {
                ObjectIdCollection sampleLineIds = sampleLineGroup.GetSampleLineIds();
                A.Ed.WriteMessage($"\n  Tìm thấy {sampleLineIds.Count} sample lines.");

                string tnSurfaceName = GetSurfaceName(surfaceTnId, tr);
                string datumSurfaceName = GetSurfaceName(surfaceDatumId, tr);
                A.Ed.WriteMessage($"\n  EG/TN Surface: {tnSurfaceName}");
                A.Ed.WriteMessage($"\n  DATUM Surface: {datumSurfaceName}");

                double prevStation = 0;
                double prevCutArea = 0;
                double prevFillArea = 0;
                bool isFirst = true;

                foreach (ObjectId sampleLineId in sampleLineIds)
                {
                    if (!sampleLineId.IsValid) continue;

                    SampleLine? sampleLine = tr.GetObject(sampleLineId, OpenMode.ForRead) as SampleLine;
                    if (sampleLine == null) continue;

                    double station = sampleLine.Station;
                    string stakeName = sampleLine.Name ?? FormatStationForTable(station);

                    // Get sections for both surfaces at this sample line
                    double tnElevation = 0;
                    double datumElevation = 0;
                    double cutArea = 0;
                    double fillArea = 0;

                    try
                    {
                        // Get section for TN surface
                        ObjectId tnSectionId = sampleLine.GetSectionId(surfaceTnId);
                        ObjectId datumSectionId = sampleLine.GetSectionId(surfaceDatumId);

                        if (tnSectionId.IsValid && datumSectionId.IsValid)
                        {
                            Section? tnSection = tr.GetObject(tnSectionId, OpenMode.ForRead) as Section;
                            Section? datumSection = tr.GetObject(datumSectionId, OpenMode.ForRead) as Section;

                            if (tnSection != null && datumSection != null)
                            {
                                // Calculate cut/fill areas at this section
                                (cutArea, fillArea) = CalculateCutFillAreaBetweenSections(tnSection, datumSection);
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        A.Ed.WriteMessage($"\n  ⚠️ Lỗi lấy section tại {stakeName}: {ex.Message}");
                    }

                    // Calculate volume using average end area method
                    if (!isFirst && (cutArea > 0 || fillArea > 0 || prevCutArea > 0 || prevFillArea > 0))
                    {
                        double distance = Math.Abs(station - prevStation);
                        result.TotalCut += (prevCutArea + cutArea) / 2.0 * distance;
                        result.TotalFill += (prevFillArea + fillArea) / 2.0 * distance;
                    }

                    result.SectionData.Add((station, stakeName, cutArea, fillArea));

                    prevStation = station;
                    prevCutArea = cutArea;
                    prevFillArea = fillArea;
                    isFirst = false;
                }

                A.Ed.WriteMessage($"\n  📊 Tổng khối lượng đào: {result.TotalCut:F2} m³");
                A.Ed.WriteMessage($"\n  📊 Tổng khối lượng đắp: {result.TotalFill:F2} m³");
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n❌ Lỗi khi tính khối lượng: {ex.Message}");
            }

            return result;
        }

        // Calculate cut/fill area between two sections (TN and DATUM)
        private static (double CutArea, double FillArea) CalculateCutFillAreaBetweenSections(Section tnSection, Section datumSection)
        {
            double cutArea = 0;
            double fillArea = 0;

            try
            {
                // Get section points
                var tnPoints = tnSection.SectionPoints;
                var datumPoints = datumSection.SectionPoints;

                if (tnPoints == null || datumPoints == null || tnPoints.Count < 2 || datumPoints.Count < 2)
                    return (0, 0);

                // Simple calculation: compare elevations at matching offsets
                // Cut = where DATUM is below TN (need to excavate)
                // Fill = where DATUM is above TN (need to fill)

                // Get min/max offset range
                double minOffset = double.MaxValue;
                double maxOffset = double.MinValue;

                for (int i = 0; i < tnPoints.Count; i++)
                {
                    double offset = tnPoints[i].Location.X;
                    minOffset = Math.Min(minOffset, offset);
                    maxOffset = Math.Max(maxOffset, offset);
                }
                for (int i = 0; i < datumPoints.Count; i++)
                {
                    double offset = datumPoints[i].Location.X;
                    minOffset = Math.Min(minOffset, offset);
                    maxOffset = Math.Max(maxOffset, offset);
                }

                // Sample at regular intervals and calculate area
                int numSamples = 50;
                double step = (maxOffset - minOffset) / numSamples;

                for (int i = 0; i < numSamples; i++)
                {
                    double offset = minOffset + i * step;
                    double tnElev = InterpolateElevation(tnPoints, offset);
                    double datumElev = InterpolateElevation(datumPoints, offset);

                    double diff = datumElev - tnElev;

                    if (diff > 0) // DATUM above TN = Fill
                        fillArea += diff * step;
                    else // DATUM below TN = Cut  
                        cutArea += Math.Abs(diff) * step;
                }
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n  ⚠️ Lỗi tính diện tích: {ex.Message}");
            }

            return (cutArea, fillArea);
        }

        // Interpolate elevation at a given offset
        private static double InterpolateElevation(SectionPointCollection points, double targetOffset)
        {
            if (points == null || points.Count == 0)
                return 0;

            if (points.Count == 1)
                return points[0].Location.Y;

            // Find the two points that bracket the target offset
            double prevOffset = points[0].Location.X;
            double prevElev = points[0].Location.Y;

            for (int i = 1; i < points.Count; i++)
            {
                double currOffset = points[i].Location.X;
                double currElev = points[i].Location.Y;

                if ((prevOffset <= targetOffset && targetOffset <= currOffset) ||
                    (currOffset <= targetOffset && targetOffset <= prevOffset))
                {
                    // Linear interpolation
                    double t = (targetOffset - prevOffset) / (currOffset - prevOffset);
                    return prevElev + t * (currElev - prevElev);
                }

                prevOffset = currOffset;
                prevElev = currElev;
            }

            // If offset is outside range, return nearest point's elevation
            double firstOffset = points[0].Location.X;
            double lastOffset = points[points.Count - 1].Location.X;

            if (Math.Abs(targetOffset - firstOffset) < Math.Abs(targetOffset - lastOffset))
                return points[0].Location.Y;
            else
                return points[points.Count - 1].Location.Y;
        }

        // Create volume table from calculated data
        private static void CreateVolumeTableFromData(VolumeCalculationResult volumeData, Point3d insertPoint, string groupName, Transaction tr)
        {
            try
            {
                Database db = A.Db;
                BlockTable? bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                if (bt == null) return;

                BlockTableRecord? btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                if (btr == null) return;

                // Tạo bảng tổng hợp đơn giản
                int numRows = 4; // Title + Header + Đào + Đắp 
                int numCols = 2; // Loại + Khối lượng

                ATable table = new();
                table.SetSize(numRows, numCols);
                table.Position = insertPoint;
                table.TableStyle = db.Tablestyle;

                // Thiết lập chiều rộng cột
                table.Columns[0].Width = 60;
                table.Columns[1].Width = 50;

                // Tiêu đề
                string title = $"BẢNG KHỐI LƯỢNG ĐÀO ĐẮP - {groupName}";
                table.Cells[0, 0].TextString = title;
                table.MergeCells(CellRange.Create(table, 0, 0, 0, numCols - 1));
                table.Cells[0, 0].Alignment = CellAlignment.MiddleCenter;
                table.Cells[0, 0].TextHeight = 5.0;
                table.Rows[0].Height = 12.0;

                // Header
                table.Cells[1, 0].TextString = "Hạng mục";
                table.Cells[1, 1].TextString = "Khối lượng (m³)";
                for (int col = 0; col < numCols; col++)
                {
                    table.Cells[1, col].Alignment = CellAlignment.MiddleCenter;
                    table.Cells[1, col].TextHeight = 4.0;
                }
                table.Rows[1].Height = 10.0;

                // Dữ liệu đào
                table.Cells[2, 0].TextString = "Đào đất";
                table.Cells[2, 1].TextString = volumeData.TotalCut.ToString("F2");
                table.Cells[2, 0].Alignment = CellAlignment.MiddleLeft;
                table.Cells[2, 1].Alignment = CellAlignment.MiddleCenter;
                table.Cells[2, 0].TextHeight = 3.5;
                table.Cells[2, 1].TextHeight = 3.5;
                table.Rows[2].Height = 8.0;

                // Dữ liệu đắp
                table.Cells[3, 0].TextString = "Đắp đất";
                table.Cells[3, 1].TextString = volumeData.TotalFill.ToString("F2");
                table.Cells[3, 0].Alignment = CellAlignment.MiddleLeft;
                table.Cells[3, 1].Alignment = CellAlignment.MiddleCenter;
                table.Cells[3, 0].TextHeight = 3.5;
                table.Cells[3, 1].TextHeight = 3.5;
                table.Rows[3].Height = 8.0;

                // Thêm bảng vào model space
                btr.AppendEntity(table);
                tr.AddNewlyCreatedDBObject(table, true);

                A.Ed.WriteMessage($"\n✅ Đã tạo bảng khối lượng.");
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n❌ Lỗi khi tạo bảng: {ex.Message}");
            }
        }

        // Format station for table display
        private static string FormatStationForTable(double station)
        {
            int km = (int)(station / 1000);
            double meters = station % 1000;
            return $"Km{km}+{meters:F3}";
        }

        // Helper method to calculate table position based on section view group
        private static Point3d CalculateTablePosition(SectionViewGroup sectionViewGroup, string tablePosition, Transaction tr)
        {
            try
            {
                // Lấy các section view trong group
                ObjectIdCollection sectionViewIds = sectionViewGroup.GetSectionViewIds();
                if (sectionViewIds.Count == 0)
                    return Point3d.Origin;

                double minX = double.MaxValue, minY = double.MaxValue;
                double maxX = double.MinValue, maxY = double.MinValue;

                // Tính bounding box của tất cả section views
                foreach (ObjectId sectionViewId in sectionViewIds)
                {
                    if (!sectionViewId.IsValid) continue;

                    SectionView? sectionView = tr.GetObject(sectionViewId, OpenMode.ForRead) as SectionView;
                    if (sectionView == null) continue;

                    // Sử dụng GeometricExtents thay vì Width/Height
                    try
                    {
                        var extents = sectionView.GeometricExtents;
                        minX = Math.Min(minX, extents.MinPoint.X);
                        minY = Math.Min(minY, extents.MinPoint.Y);
                        maxX = Math.Max(maxX, extents.MaxPoint.X);
                        maxY = Math.Max(maxY, extents.MaxPoint.Y);
                    }
                    catch
                    {
                        // Fallback to Location only
                        var origin = sectionView.Location;
                        minX = Math.Min(minX, origin.X);
                        minY = Math.Min(minY, origin.Y);
                        maxX = Math.Max(maxX, origin.X + 100); // Assume default width
                        maxY = Math.Max(maxY, origin.Y + 50); // Assume default height
                    }
                }

                // Tính vị trí bảng dựa trên tablePosition
                double offsetX = 50; // Khoảng cách từ bounding box
                double offsetY = 50;

                return tablePosition.ToLower() switch
                {
                    "topleft" => new Point3d(minX - offsetX, maxY + offsetY, 0),
                    "topright" => new Point3d(maxX + offsetX, maxY + offsetY, 0),
                    "bottomleft" => new Point3d(minX - offsetX, minY - offsetY, 0),
                    "bottomright" => new Point3d(maxX + offsetX, minY - offsetY, 0),
                    _ => new Point3d(minX - offsetX, maxY + offsetY, 0) // Default: TopLeft
                };
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n⚠️ Không thể tính vị trí bảng: {ex.Message}. Sử dụng Origin.");
                return Point3d.Origin;
            }
        }

        // Helper method to create total volume table from material list
        private static void CreateTotalVolumeTableFromMaterialList(SampleLineGroup sampleLineGroup, ObjectIdCollection materialListIds, Point3d insertPoint, Transaction tr)
        {
            try
            {
                Database db = A.Db;
                BlockTable? bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                if (bt == null) return;

                BlockTableRecord? btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                if (btr == null) return;

                // Thu thập thông tin khối lượng từ các material lists
                List<(string MaterialName, double CutVolume, double FillVolume)> volumeData = [];

                foreach (ObjectId materialListId in materialListIds)
                {
                    if (!materialListId.IsValid) continue;

                    try
                    {
                        // Lấy material list
                        var materialList = tr.GetObject(materialListId, OpenMode.ForRead);

                        // Lấy tên material list
                        var nameProperty = materialList.GetType().GetProperty("Name");
                        string materialName = nameProperty?.GetValue(materialList)?.ToString() ?? "Unknown";

                        // Lấy khối lượng đào và đắp - sử dụng reflection vì API có thể khác nhau giữa các phiên bản
                        double cutVolume = 0;
                        double fillVolume = 0;

                        // Thử lấy TotalVolume hoặc CutVolume/FillVolume
                        var cutProperty = materialList.GetType().GetProperty("TotalCutVolume") ??
                                         materialList.GetType().GetProperty("CutVolume");
                        var fillProperty = materialList.GetType().GetProperty("TotalFillVolume") ??
                                          materialList.GetType().GetProperty("FillVolume");

                        if (cutProperty != null)
                            cutVolume = Convert.ToDouble(cutProperty.GetValue(materialList) ?? 0);
                        if (fillProperty != null)
                            fillVolume = Convert.ToDouble(fillProperty.GetValue(materialList) ?? 0);

                        volumeData.Add((materialName, cutVolume, fillVolume));
                        A.Ed.WriteMessage($"\n  - {materialName}: Đào = {cutVolume:F2} m³, Đắp = {fillVolume:F2} m³");
                    }
                    catch (System.Exception ex)
                    {
                        A.Ed.WriteMessage($"\n  ⚠️ Không thể đọc material list: {ex.Message}");
                    }
                }

                if (volumeData.Count == 0)
                {
                    A.Ed.WriteMessage("\n⚠️ Không có dữ liệu khối lượng để tạo bảng.");
                    return;
                }

                // Tạo bảng
                int numRows = volumeData.Count + 2; // Header + data + total
                int numCols = 3; // Tên vật liệu, Đào, Đắp

                ATable table = new();
                table.SetSize(numRows, numCols);
                table.Position = insertPoint;
                table.TableStyle = db.Tablestyle;

                // Thiết lập chiều rộng cột
                table.Columns[0].Width = 60; // Tên vật liệu
                table.Columns[1].Width = 40; // Đào
                table.Columns[2].Width = 40; // Đắp

                // Header
                table.Cells[0, 0].TextString = "BẢNG KHỐI LƯỢNG ĐÀO ĐẮP";
                table.MergeCells(CellRange.Create(table, 0, 0, 0, numCols - 1));
                table.Cells[0, 0].Alignment = CellAlignment.MiddleCenter;
                table.Cells[0, 0].TextHeight = 5.0;
                table.Rows[0].Height = 12.0;

                table.Cells[1, 0].TextString = "Loại vật liệu";
                table.Cells[1, 1].TextString = "Đào (m³)";
                table.Cells[1, 2].TextString = "Đắp (m³)";

                for (int col = 0; col < numCols; col++)
                {
                    table.Cells[1, col].Alignment = CellAlignment.MiddleCenter;
                    table.Cells[1, col].TextHeight = 4.0;
                }
                table.Rows[1].Height = 10.0;

                // Dữ liệu
                double totalCut = 0, totalFill = 0;
                for (int i = 0; i < volumeData.Count; i++)
                {
                    int row = i + 2;
                    var data = volumeData[i];

                    table.Cells[row, 0].TextString = data.MaterialName;
                    table.Cells[row, 1].TextString = data.CutVolume.ToString("F2");
                    table.Cells[row, 2].TextString = data.FillVolume.ToString("F2");

                    for (int col = 0; col < numCols; col++)
                    {
                        table.Cells[row, col].Alignment = col == 0 ? CellAlignment.MiddleLeft : CellAlignment.MiddleCenter;
                        table.Cells[row, col].TextHeight = 3.5;
                    }
                    table.Rows[row].Height = 8.0;

                    totalCut += data.CutVolume;
                    totalFill += data.FillVolume;
                }

                // Thêm dòng tổng nếu có nhiều hơn 1 vật liệu
                if (volumeData.Count > 1)
                {
                    table.InsertRows(numRows, 8.0, 1);
                    int totalRow = numRows;

                    table.Cells[totalRow, 0].TextString = "TỔNG CỘNG";
                    table.Cells[totalRow, 1].TextString = totalCut.ToString("F2");
                    table.Cells[totalRow, 2].TextString = totalFill.ToString("F2");

                    for (int col = 0; col < numCols; col++)
                    {
                        table.Cells[totalRow, col].Alignment = CellAlignment.MiddleCenter;
                        table.Cells[totalRow, col].TextHeight = 4.0;
                    }
                }

                // Thêm bảng vào model space
                btr.AppendEntity(table);
                tr.AddNewlyCreatedDBObject(table, true);

                A.Ed.WriteMessage($"\n✅ Đã tạo bảng khối lượng với {volumeData.Count} loại vật liệu.");
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n❌ Lỗi khi tạo bảng: {ex.Message}");
            }
        }
    }
}

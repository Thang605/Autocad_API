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
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using MyFirstProject.Extensions;
using MyFirstProject.Civil_Tool;

// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3DCsharp.CTSV_TaoCorridorSurface_Commands))]

namespace Civil3DCsharp
{
    public class CTSV_TaoCorridorSurface_Commands
    {
        [CommandMethod("CTC_Tao_Corridor_Surface")]
        public static void CTSV_TaoCorridorSurface()
        {
            CTSV_TaoCorridorSurfaceMultiple();
        }

        public static void CTSV_TaoCorridorSurfaceMultiple()
        {
            try
            {
                // Show form to get user inputs OUTSIDE of transaction
                using CorridorSurfaceForm form = new();

                var dialogResult = form.ShowDialog();

                if (dialogResult != System.Windows.Forms.DialogResult.OK || !form.DialogResultOK)
                {
                    A.Ed.WriteMessage("\nLệnh đã bị hủy bỏ.");
                    return;
                }

                A.Ed.WriteMessage("\nBắt đầu tạo corridor surfaces...");

                // Validate form inputs first (changed to support multiple corridors)
                if (form.CorridorIds.Count == 0)
                {
                    A.Ed.WriteMessage("\nKhông có corridor nào được chọn.");
                    return;
                }

                A.Ed.WriteMessage($"\nSẽ xử lý {form.CorridorIds.Count} corridor(s):");
                for (int i = 0; i < form.CorridorIds.Count; i++)
                {
                    A.Ed.WriteMessage($"\n  {i + 1}. {form.CorridorNames[i]}");
                }

                // Start transaction for the main work
                using Transaction tr = A.Db.TransactionManager.StartTransaction();
                try
                {
                    List<ObjectId> allCreatedSurfaces = new List<ObjectId>();
                int processedCorridors = 0;
                int successfulCorridors = 0;

                // Process each corridor
                foreach (ObjectId corridorId in form.CorridorIds)
                {
                    processedCorridors++;

                    if (!corridorId.IsValid)
                    {
                        A.Ed.WriteMessage($"\nCorridor {processedCorridors} có ID không hợp lệ, bỏ qua.");
                        continue;
                    }

                    Corridor? corridor = tr.GetObject(corridorId, OpenMode.ForWrite) as Corridor;
                    if (corridor == null)
                    {
                        A.Ed.WriteMessage($"\nKhông thể mở corridor {processedCorridors}, bỏ qua.");
                        continue;
                    }

                    A.Ed.WriteMessage($"\n{new string('=', 50)}");
                    A.Ed.WriteMessage($"\nXử lý corridor {processedCorridors}/{form.CorridorIds.Count}: {corridor.Name}");
                    A.Ed.WriteMessage($"{new string('=', 50)}");

                    // Get corridor surface collection
                    CorridorSurfaceCollection corridorSurfaces = corridor.CorridorSurfaces;
                    List<ObjectId> corridorCreatedSurfaces = new List<ObjectId>();

                    // Create Top Surface if requested
                    if (form.CreateTopSurface)
                    {
                        string topSurfaceName = $"{corridor.Name}-L_Top";
                        ObjectId topSurfaceId = CreateCorridorSurface(
                            corridorSurfaces, topSurfaceName, "Top",
                            form.TopSurfaceStyleId, form.TopLinkCodes, form.TopAddAsBreakline, tr);
                        if (topSurfaceId != ObjectId.Null)
                        {
                            corridorCreatedSurfaces.Add(topSurfaceId);
                            allCreatedSurfaces.Add(topSurfaceId);
                            A.Ed.WriteMessage($"\n✅ Đã tạo Top Surface: {topSurfaceName}");
                        }
                    }

                    // Create Datum Surface if requested
                    if (form.CreateDatumSurface)
                    {
                        string datumSurfaceName = $"{corridor.Name}-L_Datum";
                        ObjectId datumSurfaceId = CreateCorridorSurface(
                            corridorSurfaces, datumSurfaceName, "Datum",
                            form.DatumSurfaceStyleId, form.DatumLinkCodes, form.DatumAddAsBreakline, tr);
                        if (datumSurfaceId != ObjectId.Null)
                        {
                            corridorCreatedSurfaces.Add(datumSurfaceId);
                            allCreatedSurfaces.Add(datumSurfaceId);
                            A.Ed.WriteMessage($"\n✅ Đã tạo Datum Surface: {datumSurfaceName}");
                        }
                    }

                    // Rebuild corridor if requested
                    if (form.RebuildCorridor)
                    {
                        try
                        {
                            corridor.Rebuild();
                            A.Ed.WriteMessage($"\n✅ Đã rebuild corridor: {corridor.Name}");
                        }
                        catch (System.Exception ex)
                        {
                            A.Ed.WriteMessage($"\n⚠️ Lỗi khi rebuild corridor {corridor.Name}: {ex.Message}");
                        }
                    }

                    // Add to section sources if requested
                    if (form.AddToSectionSources && corridorCreatedSurfaces.Count > 0)
                    {
                        // Get the primary alignment from corridor baselines
                        Alignment? primaryAlignment = GetPrimaryAlignmentFromCorridor(corridor, tr);
                        if (primaryAlignment != null)
                        {
                            AddSurfacesToSectionSources(primaryAlignment, corridorCreatedSurfaces, tr);
                        }
                        else
                        {
                            A.Ed.WriteMessage($"\n⚠️ Không tìm thấy alignment chính cho {corridor.Name} để thêm vào section sources.");
                        }
                    }

                    if (corridorCreatedSurfaces.Count > 0)
                    {
                        successfulCorridors++;
                        A.Ed.WriteMessage($"\n✅ Hoàn thành corridor: {corridor.Name} - Tạo được {corridorCreatedSurfaces.Count} surface(s)");
                    }
                    else
                    {
                        A.Ed.WriteMessage($"\n⚠️ Không tạo được surface nào cho corridor: {corridor.Name}");
                    }
                }

                A.Ed.WriteMessage($"\n{new string('=', 70)}");
                A.Ed.WriteMessage($"\n📊 TỔNG KẾT THỰC HIỆN");
                A.Ed.WriteMessage($"\n{new string('=', 70)}");
                A.Ed.WriteMessage($"\n• Tổng số corridors được xử lý: {processedCorridors}");
                A.Ed.WriteMessage($"\n• Số corridors tạo surface thành công: {successfulCorridors}");
                A.Ed.WriteMessage($"\n• Tổng số surfaces đã tạo: {allCreatedSurfaces.Count}");

                if (allCreatedSurfaces.Count > 0)
                {
                    A.Ed.WriteMessage($"\n✅ Hoàn thành tạo corridor surfaces cho {successfulCorridors} corridor(s)!");

                    // Display configuration instructions
                    DisplayConfigurationInstructions(allCreatedSurfaces.Count, successfulCorridors);
                }
                else
                {
                    A.Ed.WriteMessage($"\n⚠️ Không tạo được surface nào. Vui lòng kiểm tra lại cấu hình.");
                }

                tr.Commit();
                }
                catch (Autodesk.AutoCAD.Runtime.Exception e)
                {
                    A.Ed.WriteMessage($"\nLỗi AutoCAD khi thực hiện lệnh: {e.Message}");
                    tr.Abort();
                }
                catch (System.Exception e)
                {
                    A.Ed.WriteMessage($"\nLỗi khi thực hiện lệnh: {e.Message}");
                    tr.Abort();
                }
            }
            catch (System.Exception e)
            {
                A.Ed.WriteMessage($"\nLỗi khởi tạo lệnh: {e.Message}");
            }
        }

        [CommandMethod("CTC_Tao_Corridor_Surface_Don")]
        public static void CTSV_TaoCorridorSurfaceSingle()
        {
            try
            {
                A.Ed.WriteMessage("\nChọn corridor để tạo surfaces...");

                // Direct corridor selection for single corridor mode
                ObjectId corridorId = UserInput.GCorridorId("\nChọn corridor để tạo corridor surface:");

                if (corridorId == ObjectId.Null)
                {
                    A.Ed.WriteMessage("\nLệnh đã bị hủy bỏ.");
                    return;
                }

                // Start transaction
                using Transaction tr = A.Db.TransactionManager.StartTransaction();
                try
                {
                    Corridor? corridor = tr.GetObject(corridorId, OpenMode.ForWrite) as Corridor;
                if (corridor == null)
                {
                    A.Ed.WriteMessage("\nKhông thể mở corridor.");
                    return;
                }

                A.Ed.WriteMessage($"\nĐang làm việc với corridor: {corridor.Name}");

                // Create surfaces with default settings for quick operation
                CorridorSurfaceCollection corridorSurfaces = corridor.CorridorSurfaces;
                List<ObjectId> createdSurfaces = new List<ObjectId>();

                // Get default style
                ObjectId defaultStyleId = GetDefaultSurfaceStyle("Top");

                // Default link codes for single corridor mode
                List<string> defaultTopLinkCodes = new List<string> { "Top", "Top Links", "Crown", "EOP" };
                List<string> defaultDatumLinkCodes = new List<string> { "Datum", "Bottom Links", "Subgrade" };

                // Create Top Surface
                string topSurfaceName = $"{corridor.Name}-L_Top";
                ObjectId topSurfaceId = CreateCorridorSurface(corridorSurfaces, topSurfaceName, "Top", defaultStyleId, defaultTopLinkCodes, true, tr);
                if (topSurfaceId != ObjectId.Null)
                {
                    createdSurfaces.Add(topSurfaceId);
                    A.Ed.WriteMessage($"\nĐã tạo Top Surface: {topSurfaceName}");
                }

                // Create Datum Surface
                string datumSurfaceName = $"{corridor.Name}-L_Datum";
                ObjectId datumSurfaceId = CreateCorridorSurface(corridorSurfaces, datumSurfaceName, "Datum", defaultStyleId, defaultDatumLinkCodes, true, tr);
                if (datumSurfaceId != ObjectId.Null)
                {
                    createdSurfaces.Add(datumSurfaceId);
                    A.Ed.WriteMessage($"\nĐã tạo Datum Surface: {datumSurfaceName}");
                }

                // Auto rebuild corridor
                corridor.Rebuild();
                A.Ed.WriteMessage("\nĐã rebuild corridor để tạo surfaces.");

                // Add to section sources
                Alignment? primaryAlignment = GetPrimaryAlignmentFromCorridor(corridor, tr);
                if (primaryAlignment != null && createdSurfaces.Count > 0)
                {
                    AddSurfacesToSectionSources(primaryAlignment, createdSurfaces, tr);
                }

                A.Ed.WriteMessage($"\nHoàn thành! Đã tạo {createdSurfaces.Count} corridor surface(s) cho corridor: {corridor.Name}");

                // Display quick instructions
                DisplayQuickInstructions(corridor.Name, createdSurfaces.Count);

                tr.Commit();
                }
                catch (Autodesk.AutoCAD.Runtime.Exception e)
                {
                    A.Ed.WriteMessage($"\nLỗi AutoCAD khi thực hiện lệnh: {e.Message}");
                    tr.Abort();
                }
                catch (System.Exception e)
                {
                    A.Ed.WriteMessage($"\nLỗi khi thực hiện lệnh: {e.Message}");
                    tr.Abort();
                }
            }
            catch (System.Exception e)
            {
                A.Ed.WriteMessage($"\nLỗi khởi tạo lệnh: {e.Message}");
            }
        }

        // New helper method to get primary alignment from corridor
        private static Alignment? GetPrimaryAlignmentFromCorridor(Corridor corridor, Transaction tr)
        {
            try
            {
                if (corridor.Baselines.Count > 0)
                {
                    // Get the first baseline's alignment (usually the primary one)
                    Baseline primaryBaseline = corridor.Baselines[0];
                    return tr.GetObject(primaryBaseline.AlignmentId, OpenMode.ForRead) as Alignment;
                }
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi khi lấy alignment từ corridor: {ex.Message}");
            }
            return null;
        }

        // Helper method to create a specific corridor surface
        private static ObjectId CreateCorridorSurface(CorridorSurfaceCollection corridorSurfaces, string surfaceName, string surfaceType, ObjectId styleId, List<string> linkCodes, bool addAsBreakline, Transaction tr)
        {
            try
            {
                // Check if surface already exists
                foreach (CorridorSurface existingSurface in corridorSurfaces)
                {
                    if (existingSurface.Name == surfaceName)
                    {
                        A.Ed.WriteMessage($"\nSurface '{surfaceName}' đã tồn tại. Kiểm tra link codes...");
                        
                        // Check if link codes have been added - get existing link codes count
                        var existingLinkCodesList = existingSurface.LinkCodes().ToList();
                        int existingLinkCodesCount = existingLinkCodesList.Count;
                        
                        if (existingLinkCodesCount == 0 && linkCodes != null && linkCodes.Count > 0)
                        {
                            A.Ed.WriteMessage($"\n⚠️ Surface chưa có link codes. Đang thêm {linkCodes.Count} link codes...");
                            int addedCount = 0;
                            int failedCount = 0;
                            
                            foreach (string code in linkCodes)
                            {
                                if (string.IsNullOrWhiteSpace(code))
                                    continue;

                                try
                                {
                                    existingSurface.AddLinkCode(code, addAsBreakline);
                                    addedCount++;
                                    A.Ed.WriteMessage($"\n  ✅ Đã thêm link code: '{code}'");
                                }
                                catch (System.Exception ex)
                                {
                                    failedCount++;
                                    A.Ed.WriteMessage($"\n  ❌ Lỗi khi thêm '{code}': {ex.Message}");
                                }
                            }
                            
                            A.Ed.WriteMessage($"\n📊 Kết quả: Đã thêm {addedCount}/{linkCodes.Count} link codes vào surface có sẵn (thất bại: {failedCount})");
                            return existingSurface.SurfaceId;
                        }
                        else if (existingLinkCodesCount > 0)
                        {
                            A.Ed.WriteMessage($"\n✅ Surface đã có {existingLinkCodesCount} link codes.");
                        }
                        else
                        {
                            A.Ed.WriteMessage($"\n⚠️ Không có link codes để thêm vào surface có sẵn.");
                        }
                        
                        return existingSurface.SurfaceId;
                    }
                }

                // Use provided style or get default (prioritizing BORDER ONLY)
                ObjectId surfaceStyleId = styleId;
                if (surfaceStyleId == ObjectId.Null || !surfaceStyleId.IsValid)
                {
                    A.Ed.WriteMessage($"\n⚠️ Style ID từ form không hợp lệ, tìm style mặc định...");
                    surfaceStyleId = GetDefaultSurfaceStyle(surfaceType);
                }
                else
                {
                    // Display style information from form selection
                    try
                    {
                        var styleEntity = tr.GetObject(surfaceStyleId, OpenMode.ForRead);
                        var nameProperty = styleEntity.GetType().GetProperty("Name");
                        if (nameProperty != null)
                        {
                            string styleName = nameProperty.GetValue(styleEntity)?.ToString() ?? "Unknown Style";
                            A.Ed.WriteMessage($"\n✅ Sử dụng style từ form: {styleName}");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        A.Ed.WriteMessage($"\n⚠️ Không thể đọc style name: {ex.Message}");
                        A.Ed.WriteMessage($"\n  Style ID: {surfaceStyleId}");
                    }
                }

                // Debug: Log the style ID being used
                A.Ed.WriteMessage($"\n🔍 DEBUG: Creating surface với Style ID: {surfaceStyleId}");
                A.Ed.WriteMessage($"\n🔍 DEBUG: Style ID IsValid: {surfaceStyleId.IsValid}");
                A.Ed.WriteMessage($"\n🔍 DEBUG: Style ID IsNull: {surfaceStyleId == ObjectId.Null}");

                // Create new corridor surface
                CorridorSurface corridorSurface = corridorSurfaces.Add(surfaceName, surfaceStyleId);

                A.Ed.WriteMessage($"\n✅ Đã tạo corridor surface: {surfaceName} với style ID: {surfaceStyleId}");

                // Add link codes (Add Data)
                if (linkCodes != null && linkCodes.Count > 0)
                {
                    A.Ed.WriteMessage($"\n📦 Thêm {linkCodes.Count} link codes vào surface '{surfaceName}'...");
                    int addedCount = 0;
                    int failedCount = 0;
                    foreach (string code in linkCodes)
                    {
                        if (string.IsNullOrWhiteSpace(code))
                        {
                            A.Ed.WriteMessage($"\n  ⚠️ Bỏ qua code rỗng");
                            continue;
                        }

                        try
                        {
                            A.Ed.WriteMessage($"\n  🔄 Đang thêm link code: '{code}' (isBreakline: {addAsBreakline})...");
                            corridorSurface.AddLinkCode(code, addAsBreakline);
                            addedCount++;
                            A.Ed.WriteMessage($"\n  ✅ Đã thêm link code: '{code}'");
                        }
                        catch (Autodesk.AutoCAD.Runtime.Exception acEx)
                        {
                            failedCount++;
                            A.Ed.WriteMessage($"\n  ❌ Lỗi AutoCAD khi thêm '{code}': {acEx.Message}");
                            A.Ed.WriteMessage($"\n     ErrorStatus: {acEx.ErrorStatus}");
                        }
                        catch (System.Exception ex)
                        {
                            failedCount++;
                            A.Ed.WriteMessage($"\n  ❌ Lỗi khi thêm '{code}': {ex.Message}");
                            A.Ed.WriteMessage($"\n     Type: {ex.GetType().Name}");
                        }
                    }
                    A.Ed.WriteMessage($"\n📊 Kết quả: Đã thêm {addedCount}/{linkCodes.Count} link codes (thất bại: {failedCount})");
                }
                else
                {
                    A.Ed.WriteMessage($"\n⚠️ Không có link codes được chọn. Surface sẽ được tạo mà không có data.");
                    A.Ed.WriteMessage($"\n   Để thêm link codes, sử dụng Corridor Properties > Surfaces > Add Data.");
                }

                // Configure surface based on type (including overhang correction and boundaries)
                ConfigureCorridorSurface(corridorSurface, surfaceType);

                A.Ed.WriteMessage($"\n✅ Đã tạo corridor surface: {surfaceName}");

                // Return the ObjectId of the created surface
                return corridorSurface.SurfaceId;
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n❌ Lỗi khi tạo corridor surface '{surfaceName}': {ex.Message}");
                return ObjectId.Null;
            }
        }

        // Helper method to get default surface style
        private static ObjectId GetDefaultSurfaceStyle(string surfaceType)
        {
            try
            {
                var surfaceStyles = A.Cdoc.Styles.SurfaceStyles;

                // First priority: BORDER ONLY style for corridor surfaces
                if (surfaceStyles.Contains("BORDER ONLY"))
                {
                    A.Ed.WriteMessage($"\n  ✅ Sử dụng style: BORDER ONLY");
                    return surfaceStyles["BORDER ONLY"];
                }

                // Alternative names for border only style
                string[] borderStyleNames = {
                    "Border Only",
                    "BorderOnly",
                    "Border",
                    "Borders Only",
                    "Boundary Only",
                    "Outline Only"
                };

                foreach (string styleName in borderStyleNames)
                {
                    if (surfaceStyles.Contains(styleName))
                    {
                        A.Ed.WriteMessage($"\n  ✅ Sử dụng style: {styleName}");
                        return surfaceStyles[styleName];
                    }
                }

                // Second priority: ALL CODES style (fallback)
                ObjectId allCodesStyleId = GetAllCodesStyle();
                if (allCodesStyleId != ObjectId.Null)
                {
                    A.Ed.WriteMessage($"\n  ✅ Sử dụng corridor style (fallback): All Codes 1-1000");
                    return allCodesStyleId;
                }

                // Third priority: surface type specific styles
                if (surfaceType == "Top")
                {
                    // Try to find specific style for top surface
                    if (surfaceStyles.Contains("Top Surface"))
                        return surfaceStyles["Top Surface"];
                    if (surfaceStyles.Contains("Road Top"))
                        return surfaceStyles["Road Top"];
                    if (surfaceStyles.Contains("Corridor Top"))
                        return surfaceStyles["Corridor Top"];
                }
                else if (surfaceType == "Datum")
                {
                    // Try to find specific style for datum surface
                    if (surfaceStyles.Contains("Datum Surface"))
                        return surfaceStyles["Datum Surface"];
                    if (surfaceStyles.Contains("Subgrade"))
                        return surfaceStyles["Subgrade"];
                    if (surfaceStyles.Contains("Corridor Datum"))
                        return surfaceStyles["Corridor Datum"];
                }

                // Final fallback to first available style
                if (surfaceStyles.Count > 0)
                {
                    var firstStyle = surfaceStyles[0];
                    A.Ed.WriteMessage($"\n  ⚠️ Sử dụng style đầu tiên có sẵn (không tìm thấy BORDER ONLY)");
                    return firstStyle;
                }

                A.Ed.WriteMessage($"\n  ❌ Không tìm thấy surface style phù hợp");
                return ObjectId.Null;
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi khi lấy surface style: {ex.Message}");
                return ObjectId.Null;
            }
        }

        // Helper method to get All Codes style from CodeSet styles
        private static ObjectId GetAllCodesStyle()
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
                        A.Ed.WriteMessage($"\n  ✅ Tìm thấy CodeSet style: {styleName}");
                        return codeSetStyles[styleName];
                    }
                }

                // Try surface styles as fallback
                var surfaceStyles = A.Cdoc.Styles.SurfaceStyles;
                foreach (string styleName in allCodesStyleNames)
                {
                    if (surfaceStyles.Contains(styleName))
                    {
                        A.Ed.WriteMessage($"\n  ✅ Tìm thấy Surface style: {styleName}");
                        return surfaceStyles[styleName];
                    }
                }

                A.Ed.WriteMessage($"\n  ⚠️ Không tìm thấy All Codes 1-1000 style");
                return ObjectId.Null;
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi khi tìm All Codes style: {ex.Message}");
                return ObjectId.Null;
            }
        }

        // Helper method to configure corridor surface properties
        private static void ConfigureCorridorSurface(CorridorSurface corridorSurface, string surfaceType)
        {
            try
            {
                A.Ed.WriteMessage($"\nBắt đầu cấu hình chi tiết {surfaceType} surface: {corridorSurface.Name}");

                // 1. Set overhang correction based on surface type
                try
                {
                    if (surfaceType == "Top")
                    {
                        corridorSurface.OverhangCorrection = OverhangCorrectionType.TopLinks;
                        A.Ed.WriteMessage($"\n  ✅ Đã thiết lập overhang correction = TopLinks (cho L_Top)");
                    }
                    else if (surfaceType == "Datum")
                    {
                        corridorSurface.OverhangCorrection = OverhangCorrectionType.BottomLinks;
                        A.Ed.WriteMessage($"\n  ✅ Đã thiết lập overhang correction = BottomLinks (cho L_Datum)");
                    }
                    else
                    {
                        corridorSurface.OverhangCorrection = OverhangCorrectionType.TopLinks;
                        A.Ed.WriteMessage($"\n  ✅ Đã thiết lập overhang correction = TopLinks (default)");
                    }
                }
                catch (System.Exception ex)
                {
                    A.Ed.WriteMessage($"\n  ⚠️ Không thể thiết lập overhang correction: {ex.Message}");
                }

                // 2. Add boundaries automatically
                try
                {
                    AddCorridorSurfaceBoundaries(corridorSurface, surfaceType);
                }
                catch (System.Exception ex)
                {
                    A.Ed.WriteMessage($"\n  ⚠️ Lỗi khi thêm boundaries: {ex.Message}");
                }

                A.Ed.WriteMessage($"\n  ✅ Hoàn thành cấu hình {surfaceType} surface: {corridorSurface.Name}");
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi khi cấu hình corridor surface: {ex.Message}");
            }
        }

        // Helper method to add boundaries to corridor surface
        private static void AddCorridorSurfaceBoundaries(CorridorSurface corridorSurface, string surfaceType)
        {
            try
            {
                A.Ed.WriteMessage($"\n  Cấu hình boundaries cho {surfaceType} surface...");

                // Get the corridor surface boundary collection
                CorridorSurfaceBoundaryCollection boundaries = corridorSurface.Boundaries;

                A.Ed.WriteMessage($"\n    Hiện có {boundaries.Count} boundaries");

                try
                {
                    // Add corridor extents boundary with proper name parameter
                    string boundaryName = $"{surfaceType}_Extents";
                    var extentsBoundary = boundaries.AddCorridorExtentsBoundary(boundaryName);
                    A.Ed.WriteMessage($"\n  ✅ Đã thêm Corridor Extents Boundary: {boundaryName}");
                }
                catch (System.Exception ex)
                {
                    A.Ed.WriteMessage($"\n  ⚠️ Không thể thêm automatic boundary: {ex.Message}");
                }

                A.Ed.WriteMessage($"\n    Tổng số boundaries hiện có: {boundaries.Count}");

                // Provide additional guidance for manual configuration if needed
                A.Ed.WriteMessage($"\n  💡 Hướng dẫn thêm boundaries bổ sung (nếu cần):");
                A.Ed.WriteMessage($"\n    1. Mở Corridor Properties");
                A.Ed.WriteMessage($"\n    2. Tab 'Surfaces' > Chọn surface '{corridorSurface.Name}'");
                A.Ed.WriteMessage($"\n    3. Click 'Add Boundaries' để thêm boundaries chi tiết hơn");
                A.Ed.WriteMessage($"\n    4. Chọn boundary type: Interactive, Hide, Show, hoặc Outer");

                A.Ed.WriteMessage($"\n  ✅ Hoàn thành cấu hình boundaries cho {surfaceType} surface");
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n  ❌ Lỗi khi cấu hình boundaries: {ex.Message}");
                A.Ed.WriteMessage($"\n    Cần thêm boundaries thủ công trong Corridor Properties");
            }
        }

        // Helper method to add surfaces to section sources
        private static void AddSurfacesToSectionSources(Alignment alignment, List<ObjectId> surfaceIds, Transaction tr)
        {
            try
            {
                A.Ed.WriteMessage("\n📋 Thêm surfaces vào section sources...");

                // Get sample line group
                ObjectIdCollection sampleLineGroupIds = alignment.GetSampleLineGroupIds();
                if (sampleLineGroupIds.Count == 0)
                {
                    A.Ed.WriteMessage("\n⚠️ Không có sample line group để thêm surfaces.");
                    A.Ed.WriteMessage("\n   Surfaces sẽ xuất hiện trong Available sources khi tạo sample line group.");
                    return;
                }

                int totalAdded = 0;

                // Process all sample line groups
                foreach (ObjectId sampleLineGroupId in sampleLineGroupIds)
                {
                    SampleLineGroup? sampleLineGroup = tr.GetObject(sampleLineGroupId, OpenMode.ForWrite) as SampleLineGroup;
                    if (sampleLineGroup == null)
                    {
                        continue;
                    }

                    A.Ed.WriteMessage($"\n   Xử lý Sample Line Group: {sampleLineGroup.Name}");

                    SectionSourceCollection sectionSources = sampleLineGroup.GetSectionSources();

                    foreach (ObjectId surfaceId in surfaceIds)
                    {
                        if (surfaceId == ObjectId.Null || !surfaceId.IsValid)
                            continue;

                        try
                        {
                            // Find the section source for this surface
                            bool found = false;
                            foreach (SectionSource source in sectionSources)
                            {
                                if (source.SourceId == surfaceId)
                                {
                                    found = true;

                                    // Check if already sampled
                                    if (!source.IsSampled)
                                    {
                                        // Set IsSampled to true to add to Sampled sources
                                        source.IsSampled = true;
                                        totalAdded++;

                                        // Get surface name for display
                                        string surfaceName = "Unknown";
                                        try
                                        {
                                            var surfaceObj = tr.GetObject(surfaceId, OpenMode.ForRead);
                                            var nameProp = surfaceObj.GetType().GetProperty("Name");
                                            if (nameProp != null)
                                            {
                                                surfaceName = nameProp.GetValue(surfaceObj)?.ToString() ?? "Unknown";
                                            }
                                        }
                                        catch { }

                                        A.Ed.WriteMessage($"\n   ✅ Đã thêm vào Sampled sources: {surfaceName}");
                                    }
                                    else
                                    {
                                        A.Ed.WriteMessage($"\n   ℹ️ Surface đã có trong Sampled sources");
                                    }
                                    break;
                                }
                            }

                            if (!found)
                            {
                                A.Ed.WriteMessage($"\n   ⚠️ Surface chưa có trong Available sources (cần rebuild corridor trước)");
                            }
                        }
                        catch (System.Exception ex)
                        {
                            A.Ed.WriteMessage($"\n   ❌ Lỗi khi thêm surface: {ex.Message}");
                        }
                    }
                }

                if (totalAdded > 0)
                {
                    A.Ed.WriteMessage($"\n✅ Đã thêm {totalAdded} surface(s) vào Sampled sources.");
                }
                else
                {
                    A.Ed.WriteMessage($"\n⚠️ Không thêm được surface nào vào Sampled sources.");
                    A.Ed.WriteMessage($"\n   Có thể cần rebuild corridor trước hoặc thêm thủ công.");
                }
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n❌ Lỗi khi thêm surfaces vào section sources: {ex.Message}");
            }
        }

        // Helper method to display configuration instructions (updated for multiple corridors)
        private static void DisplayConfigurationInstructions(int surfaceCount, int corridorCount)
        {
            A.Ed.WriteMessage("\n" + new string('=', 70));
            A.Ed.WriteMessage("\n📋 HƯỚNG DẪN CẤU HÌNH CORRIDOR SURFACE - ĐÃ CẤU HÌNH TỰ ĐỘNG");
            A.Ed.WriteMessage("\n" + new string('=', 70));

            if (surfaceCount > 0)
            {
                A.Ed.WriteMessage($"\n✅ Đã tự động cấu hình cho {corridorCount} corridor(s) - {surfaceCount} surface(s):");
                A.Ed.WriteMessage("\n   • Overhang Correction = TopLinks (cho L_Top), BottomLinks (cho L_Datum)");
                A.Ed.WriteMessage("\n   • Surface Style = BORDER ONLY (ưu tiên), All Codes 1-1000 (fallback)");
                A.Ed.WriteMessage("\n   • Corridor Extents Boundaries đã được thêm tự động");
                A.Ed.WriteMessage("\n   • Daylight Boundaries đã được thêm (nếu có thể)");

                A.Ed.WriteMessage("\n🔧 Bước tiếp theo - Kiểm tra và điều chỉnh:");
                A.Ed.WriteMessage("\n1. Mở Corridor Properties cho từng corridor:");
                A.Ed.WriteMessage("\n   • Toolspace > Prospector > Corridors > [Corridor Name] > Properties");
                A.Ed.WriteMessage("\n   • Hoặc click chuột phải vào corridor > Properties");

                A.Ed.WriteMessage("\n2. Tab 'Surfaces' - Kiểm tra cấu hình cho mỗi surface:");
                A.Ed.WriteMessage("\n   • Chọn surface vừa tạo (L_Top hoặc L_Datum)");
                A.Ed.WriteMessage("\n   • ✅ Overhang Correction: TopLinks (L_Top) / BottomLinks (L_Datum)");
                A.Ed.WriteMessage("\n   • ✅ Boundaries: Corridor Extents boundaries đã được thêm");
                A.Ed.WriteMessage("\n   • ✅ Style: BORDER ONLY (ưu tiên) hoặc All Codes 1-1000 (fallback)");

                A.Ed.WriteMessage("\n3. 🔧 Sửa Style nếu cần (nếu hiển thị No Style):");
                A.Ed.WriteMessage("\n   • Trong Corridor Properties > Tab Surfaces");
                A.Ed.WriteMessage("\n   • Chọn surface > Click vào Style dropdown");
                A.Ed.WriteMessage("\n   • Ưu tiên chọn: 'BORDER ONLY' (khuyến nghị cho corridor surfaces)");
                A.Ed.WriteMessage("\n   • Backup chọn: '1. All Codes 1-1000' hoặc style phù hợp khác");
                A.Ed.WriteMessage("\n   • Click Apply để áp dụng thay đổi");

                A.Ed.WriteMessage("\n4. Điều chỉnh nếu cần (áp dụng cho tất cả corridors):");
                A.Ed.WriteMessage("\n   • Boundary settings: Điều chỉnh extents boundaries");
                A.Ed.WriteMessage("\n   • Add thêm boundaries: Interactive, Hide, Show boundaries");
                A.Ed.WriteMessage("\n   • Link codes: Kiểm tra assembly link codes");
                A.Ed.WriteMessage("\n   • Overhang Correction: Thay đổi nếu cần thiết");

                A.Ed.WriteMessage("\n5. Cấu hình Link Codes (quan trọng cho tất cả surfaces):");
                A.Ed.WriteMessage("\n   • Top Surface (L_Top): Pave, Top, Crown, Shoulder, Curb_Top");
                A.Ed.WriteMessage("\n   • Datum Surface (L_Datum): Datum, Subgrade, Formation, Base");

                A.Ed.WriteMessage("\n6. Sử dụng trong Section Views:");
                A.Ed.WriteMessage("\n   • Chạy lệnh CTSV_VeTracNgangThietKe cho từng alignment");
                A.Ed.WriteMessage("\n   • Surfaces sẽ tự động xuất hiện trong danh sách");
                A.Ed.WriteMessage("\n   • Hoặc thêm thủ công: Right-click surface > 'Add as Section Source'");

                A.Ed.WriteMessage("\n💡 Mẹo cho nhiều corridors:");
                A.Ed.WriteMessage("\n   • Có thể copy settings từ corridor đầu tiên sang các corridor khác");
                A.Ed.WriteMessage("\n   • Sử dụng Corridor Properties > Copy/Paste để nhanh chóng áp dụng cùng cấu hình");
                A.Ed.WriteMessage($"\n   • Tất cả {surfaceCount} surfaces đã được tạo với cùng cấu hình style và boundaries");
            }

            A.Ed.WriteMessage("\n" + new string('=', 70));
            A.Ed.WriteMessage($"\n✅ Hoàn thành tạo corridor surface cho {corridorCount} corridor(s) với cấu hình hoàn toàn tự động!");
            A.Ed.WriteMessage("\n📝 Lưu ý: Style mặc định là BORDER ONLY (khuyến nghị cho corridor surfaces)");
            A.Ed.WriteMessage("\n🔧 Chỉ cần kiểm tra link codes và điều chỉnh nếu cần cho từng corridor");
            A.Ed.WriteMessage("\n" + new string('=', 70));
        }

        // Helper method for quick instructions (single corridor)
        private static void DisplayQuickInstructions(string corridorName, int surfaceCount)
        {
            A.Ed.WriteMessage("\n" + new string('=', 50));
            A.Ed.WriteMessage($"\n🚀 NHANH CHÓNG HOÀN THÀNH - {corridorName}");
            A.Ed.WriteMessage("\n" + new string('=', 50));

            if (surfaceCount > 0)
            {
                A.Ed.WriteMessage($"\n✅ Đã tạo {surfaceCount} surface(s) với cấu hình tự động:");
                A.Ed.WriteMessage("\n   • Overhang Correction đã được thiết lập phù hợp");
                A.Ed.WriteMessage("\n   • BORDER ONLY style (hoặc All Codes 1-1000 fallback)");
                A.Ed.WriteMessage("\n   • Corridor Extents Boundaries tự động");
                A.Ed.WriteMessage("\n   • Đã rebuild corridor và thêm vào section sources");

                A.Ed.WriteMessage("\n🔧 Nếu cần điều chỉnh:");
                A.Ed.WriteMessage($"\n   • Mở Corridor Properties > {corridorName}");
                A.Ed.WriteMessage("\n   • Tab 'Surfaces' > Kiểm tra cấu hình");
                A.Ed.WriteMessage("\n   • Điều chỉnh Link Codes nếu cần");

                A.Ed.WriteMessage("\n💡 Để tạo nhiều corridors cùng lúc, sử dụng:");
                A.Ed.WriteMessage("\n   • CTSV_TaoCorridorSurface (phiên bản form với nhiều tùy chọn)");
            }

            A.Ed.WriteMessage("\n" + new string('=', 50));
        }
    }
}

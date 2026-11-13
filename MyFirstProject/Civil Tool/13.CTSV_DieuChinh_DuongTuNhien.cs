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

// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3DCsharp.CTSV_DieuChinh_DuongTuNhien_Commands))]

namespace Civil3DCsharp
{
    public class CTSV_DieuChinh_DuongTuNhien_Commands
    {
        [CommandMethod("CTS_Dieu_Chinh_Duong_Tu_Nhien")]
        public static void CTSVDieuChinhDuongTuNhien()
        {
            // start transaction
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();

                // Step 1: Choose section directly
                A.Ed.WriteMessage("\nChọn section của cọc cần hiệu chỉnh:");
                PromptEntityOptions peo = new("\nChọn section của cọc cần hiệu chỉnh: ");
                peo.SetRejectMessage("\nĐối tượng được chọn không phải là section.");
                peo.AddAllowedClass(typeof(Section), true);
                
                PromptEntityResult per = A.Ed.GetEntity(peo);
                if (per.Status != PromptStatus.OK)
                {
                    A.Ed.WriteMessage("\nĐã hủy lệnh.");
                    return;
                }

                ObjectId sectionId = per.ObjectId;
                Section? section = tr.GetObject(sectionId, OpenMode.ForWrite) as Section;
                if (section == null)
                {
                    A.Ed.WriteMessage("\nKhông thể lấy thông tin section.");
                    return;
                }

                // Step 2: Get section view from section and find source surface, sample line group
                SectionView? sectionView = null;
                CivSurface? sourceSurface = null;
                SampleLineGroup? sampleLineGroup = null;
                SampleLine? currentSampleLine = null;
                
                // Get all entities in the drawing and find section views
                BlockTable? bt = tr.GetObject(A.Db.BlockTableId, OpenMode.ForWrite) as BlockTable;
                if (bt == null)
                {
                    A.Ed.WriteMessage("\nKhông thể truy cập Block Table.");
                    return;
                }

                BlockTableRecord? btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                if (btr == null)
                {
                    A.Ed.WriteMessage("\nKhông thể truy cập Model Space.");
                    return;
                }
                
                RXClass rxSectionView = RXClass.GetClass(typeof(SectionView));
                foreach (ObjectId entId in btr)
                {
                    try
                    {
                        if (!entId.ObjectClass.IsDerivedFrom(rxSectionView)) continue;
                        if (tr.GetObject(entId, OpenMode.ForWrite) is SectionView sv)
                        {
                            ObjectId svSampleLineId = sv.SampleLineId;
                            SampleLine? sl = tr.GetObject(svSampleLineId, OpenMode.ForWrite) as SampleLine;
                            if (sl != null)
                            {
                                // Check if this section belongs to this sample line
                                ObjectId slGroupId = sl.GroupId;
                                SampleLineGroup? slGroup = tr.GetObject(slGroupId, OpenMode.ForWrite) as SampleLineGroup;
                                if (slGroup != null)
                                {
                                    SectionSourceCollection sectionSources = slGroup.GetSectionSources();
                                    foreach (SectionSource source in sectionSources)
                                    {
                                        try
                                        {
                                            ObjectId testSectionId = sl.GetSectionId(source.SourceId);
                                            if (testSectionId == sectionId)
                                            {
                                                sectionView = sv;
                                                currentSampleLine = sl;
                                                sampleLineGroup = slGroup;
                                                
                                                // Try to get the source surface
                                                try
                                                {
                                                    CivSurface? surf = tr.GetObject(source.SourceId, OpenMode.ForWrite) as CivSurface;
                                                    if (surf != null)
                                                    {
                                                        sourceSurface = surf;
                                                        A.Ed.WriteMessage($"\nĐã tìm thấy surface nguồn: '{surf.Name}'");
                                                    }
                                                }
                                                catch
                                                {
                                                    // Surface might not be accessible or valid
                                                }
                                                break;
                                            }
                                        }
                                        catch
                                        {
                                            // Continue if section doesn't exist for this source
                                            continue;
                                        }
                                    }
                                    if (sectionView != null) break;
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Skip entities that can't be processed
                        continue;
                    }
                }

                if (sectionView == null || currentSampleLine == null || sampleLineGroup == null)
                {
                    A.Ed.WriteMessage("\nKhông tìm thấy section view chứa section này.");
                    return;
                }

                if (sourceSurface == null)
                {
                    A.Ed.WriteMessage("\nKhông tìm thấy surface nguồn của section này.");
                    A.Ed.WriteMessage("\nVui lòng sử dụng lệnh với surface được chọn thủ công.");
                    return;
                }

                // Get parent alignment
                ObjectId alignmentId = currentSampleLine.GetParentAlignmentId();
                Alignment? alignment = tr.GetObject(alignmentId, OpenMode.ForWrite) as Alignment;
                if (alignment == null)
                {
                    A.Ed.WriteMessage("\nKhông thể lấy thông tin alignment.");
                    return;
                }

                // Step 2.1: Find previous and next sample lines/sections
                double currentStation = currentSampleLine.Station;
                A.Ed.WriteMessage($"\nStation hiện tại: {currentStation:F3}");
                
                SampleLine? previousSampleLine = null;
                SampleLine? nextSampleLine = null;
                Section? previousSection = null;
                Section? nextSection = null;

                // Get all sample lines in the group and sort by station
                ObjectIdCollection sampleLineIds = sampleLineGroup.GetSampleLineIds();
                var sampleLinesWithStations = new List<(ObjectId id, double station, SampleLine sampleLine)>();

                foreach (ObjectId slId in sampleLineIds)
                {
                    try
                    {
                        SampleLine? sl = tr.GetObject(slId, OpenMode.ForRead) as SampleLine;
                        if (sl != null)
                        {
                            sampleLinesWithStations.Add((slId, sl.Station, sl));
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }

                // Sort by station
                sampleLinesWithStations.Sort((a, b) => a.station.CompareTo(b.station));

                // Find previous and next sample lines
                for (int i = 0; i < sampleLinesWithStations.Count; i++)
                {
                    if (Math.Abs(sampleLinesWithStations[i].station - currentStation) < 0.001)
                    {
                        // Found current sample line
                        if (i > 0)
                        {
                            previousSampleLine = sampleLinesWithStations[i - 1].sampleLine;
                            A.Ed.WriteMessage($"\nĐã tìm thấy sample line trước: Station {previousSampleLine.Station:F3}");
                        }
                        if (i < sampleLinesWithStations.Count - 1)
                        {
                            nextSampleLine = sampleLinesWithStations[i + 1].sampleLine;
                            A.Ed.WriteMessage($"\nĐã tìm thấy sample line sau: Station {nextSampleLine.Station:F3}");
                        }
                        break;
                    }
                }

                if (previousSampleLine == null && nextSampleLine == null)
                {
                    A.Ed.WriteMessage("\nKhông tìm thấy sample line trước hoặc sau để tham chiếu.");
                    return;
                }

                // Get sections for previous and next sample lines
                SectionSourceCollection sources = sampleLineGroup.GetSectionSources();
                foreach (SectionSource source in sources)
                {
                    try
                    {
                        if (tr.GetObject(source.SourceId, OpenMode.ForRead) is CivSurface surf && surf == sourceSurface)
                        {
                            if (previousSampleLine != null)
                            {
                                try
                                {
                                    ObjectId prevSectionId = previousSampleLine.GetSectionId(source.SourceId);
                                    previousSection = tr.GetObject(prevSectionId, OpenMode.ForRead) as Section;
                                    if (previousSection != null)
                                    {
                                        A.Ed.WriteMessage($"\nĐã tìm thấy section trước với {previousSection.SectionPoints.Count} điểm.");
                                    }
                                }
                                catch
                                {
                                    A.Ed.WriteMessage("\nKhông thể lấy section của sample line trước.");
                                }
                            }

                            if (nextSampleLine != null)
                            {
                                try
                                {
                                    ObjectId nextSectionId = nextSampleLine.GetSectionId(source.SourceId);
                                    nextSection = tr.GetObject(nextSectionId, OpenMode.ForRead) as Section;
                                    if (nextSection != null)
                                    {
                                        A.Ed.WriteMessage($"\nĐã tìm thấy section sau với {nextSection.SectionPoints.Count} điểm.");
                                    }
                                }
                                catch
                                {
                                    A.Ed.WriteMessage("\nKhông thể lấy section của sample line sau.");
                                }
                            }
                            break;
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }

                // Step 2.2: Create polylines from previous and next sections and add to surface
                if (previousSection != null && previousSampleLine != null)
                {
                    A.Ed.WriteMessage("\n=== Xử lý section trước ===");
                    ProcessSectionToBreakline(tr, previousSection, previousSampleLine, sectionView, sourceSurface, alignment, bt);
                }

                if (nextSection != null && nextSampleLine != null)
                {
                    A.Ed.WriteMessage("\n=== Xử lý section sau ===");
                    ProcessSectionToBreakline(tr, nextSection, nextSampleLine, sectionView, sourceSurface, alignment, bt);
                }

                A.Ed.WriteMessage("\n=== Hoàn thành xử lý section tham chiếu ===");

                // Step 3: Choose existing polyline (created by AT_PolylineFromSection)
                A.Ed.WriteMessage("\nChọn polyline đã chỉnh sửa (tạo bởi lệnh AT_PolylineFromSection):");
                PromptEntityOptions polylinePeo = new("\nChọn polyline: ");
                polylinePeo.SetRejectMessage("\nĐối tượng được chọn không phải là polyline.");
                polylinePeo.AddAllowedClass(typeof(Polyline), true);
                
                PromptEntityResult polylinePer = A.Ed.GetEntity(polylinePeo);
                if (polylinePer.Status != PromptStatus.OK)
                {
                    A.Ed.WriteMessage("\nĐã hủy lệnh.");
                    return;
                }

                ObjectId polylineId = polylinePer.ObjectId;
                Polyline? selectedPolyline = tr.GetObject(polylineId, OpenMode.ForWrite) as Polyline;
                if (selectedPolyline == null)
                {
                    A.Ed.WriteMessage("\nKhông thể lấy thông tin polyline.");
                    return;
                }

                A.Ed.WriteMessage($"\nĐã chọn polyline với {selectedPolyline.NumberOfVertices} điểm.");
                A.Ed.WriteMessage($"\nSẽ thêm 3D polyline vào surface: '{sourceSurface.Name}'");

                // Step 4: Transform polyline vertices to world coordinates
                double station = currentSampleLine.Station;
                List<Point3d> adjustedPoints = TransformPolylineVertices(sectionView, selectedPolyline, station, alignment);

                if (adjustedPoints.Count < 2)
                {
                    A.Ed.WriteMessage("\nKhông đủ điểm để tạo 3D polyline (cần ít nhất 2 điểm).");
                    return;
                }

                // Step 5: Create 3D Polyline from adjusted points
                Point3dCollection point3dCollection = new Point3dCollection();
                foreach (Point3d point in adjustedPoints)
                {
                    point3dCollection.Add(point);
                }
                
                Polyline3d polyline3d = new Polyline3d(Poly3dType.SimplePoly, point3dCollection, false);
                polyline3d.ColorIndex = 2; // Yellow color for visibility
                polyline3d.Layer = "0";

                // Add 3D polyline to drawing
                BlockTableRecord? modelSpace = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                if (modelSpace == null)
                {
                    A.Ed.WriteMessage("\nKhông thể truy cập Model Space để ghi.");
                    return;
                }

                ObjectId polyline3dId = modelSpace.AppendEntity(polyline3d);
                tr.AddNewlyCreatedDBObject(polyline3d, true);

                A.Ed.WriteMessage($"\nĐã tạo 3D polyline với {adjustedPoints.Count} điểm.");

                // Step 6: Add 3D polyline as breakline to the source surface
                try
                {
                    ObjectIdCollection breaklineIds = new ObjectIdCollection();
                    breaklineIds.Add(polyline3dId);
                    
                    // Parameters matching Civil 3D dialog:
                    // weedingDistance: 15.00m, supplementingDistance: 100.000m, 
                    // midOrdinateDistance: 1.000m, weedingAngle: 0 degrees
                    sourceSurface.BreaklinesDefinition.AddStandardBreaklines(breaklineIds, 15.0, 100.0, 1.0, 0.0);
                    
                    A.Ed.WriteMessage($"\nĐã thêm 3D polyline làm breakline vào surface '{sourceSurface.Name}'.");
                    A.Ed.WriteMessage("\nSurface sẽ được cập nhật tự động để phản ánh thay đổi.");
                }
                catch (System.Exception ex)
                {
                    A.Ed.WriteMessage($"\nLỗi khi thêm breakline vào surface: {ex.Message}");
                    A.Ed.WriteMessage("\n3D polyline đã được tạo nhưng chưa thêm vào surface.");
                }

                A.Ed.WriteMessage($"\nĐã hoàn thành điều chỉnh đường tự nhiên cho surface '{sourceSurface.Name}'.");

                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage($"\nLỗi AutoCAD: {e.Message}");
                A.Ed.WriteMessage($"\nError Code: {e.ErrorStatus}");
                tr.Abort();
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi hệ thống: {ex.Message}");
                A.Ed.WriteMessage($"\nStack trace: {ex.StackTrace}");
                tr.Abort();
            }
        }

        private static void ProcessSectionToBreakline(Transaction tr, Section section, SampleLine sampleLine, 
            SectionView sectionView, CivSurface sourceSurface, Alignment alignment, BlockTable bt)
        {
            try
            {
                // Create polyline from section points
                SectionPointCollection sectionPoints = section.SectionPoints;
                if (sectionPoints.Count < 2)
                {
                    A.Ed.WriteMessage($"\nSection tại station {sampleLine.Station:F3} không có đủ điểm để tạo polyline.");
                    return;
                }

                // Create polyline from section points using the same method as AT_PolylineFromSection
                Polyline? sectionPolyline = CreatePolylineFromSection(sectionView, sectionPoints);
                if (sectionPolyline == null)
                {
                    A.Ed.WriteMessage($"\nKhông thể tạo polyline từ section tại station {sampleLine.Station:F3}.");
                    return;
                }

                // Transform polyline vertices to world coordinates
                double station = sampleLine.Station;
                List<Point3d> adjustedPoints = TransformPolylineVertices(sectionView, sectionPolyline, station, alignment);

                if (adjustedPoints.Count < 2)
                {
                    A.Ed.WriteMessage($"\nKhông đủ điểm để tạo 3D polyline tại station {station:F3}.");
                    sectionPolyline.Dispose();
                    return;
                }

                // Create 3D Polyline from adjusted points
                Point3dCollection point3dCollection = new Point3dCollection();
                foreach (Point3d point in adjustedPoints)
                {
                    point3dCollection.Add(point);
                }
                
                Polyline3d polyline3d = new Polyline3d(Poly3dType.SimplePoly, point3dCollection, false);
                polyline3d.ColorIndex = 3; // Green color for reference sections
                polyline3d.Layer = "0";

                // Add 3D polyline to drawing
                BlockTableRecord? modelSpace = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                if (modelSpace == null)
                {
                    A.Ed.WriteMessage("\nKhông thể truy cập Model Space để ghi.");
                    sectionPolyline.Dispose();
                    polyline3d.Dispose();
                    return;
                }

                ObjectId polyline3dId = modelSpace.AppendEntity(polyline3d);
                tr.AddNewlyCreatedDBObject(polyline3d, true);

                A.Ed.WriteMessage($"\nĐã tạo 3D polyline tại station {station:F3} với {adjustedPoints.Count} điểm.");

                // Add 3D polyline as breakline to surface
                try
                {
                    ObjectIdCollection breaklineIds = new ObjectIdCollection();
                    breaklineIds.Add(polyline3dId);
                    
                    sourceSurface.BreaklinesDefinition.AddStandardBreaklines(breaklineIds, 15.0, 100.0, 1.0, 0.0);
                    
                    A.Ed.WriteMessage($"\nĐã thêm 3D polyline tại station {station:F3} làm breakline vào surface '{sourceSurface.Name}'.");
                }
                catch (System.Exception ex)
                {
                    A.Ed.WriteMessage($"\nLỗi khi thêm breakline tại station {station:F3}: {ex.Message}");
                }

                // Clean up the temporary 2D polyline
                sectionPolyline.Dispose();
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi khi xử lý section tại station {sampleLine.Station:F3}: {ex.Message}");
            }
        }

        private static Polyline? CreatePolylineFromSection(SectionView sectionView, SectionPointCollection sectionPoints)
        {
            try
            {
                // Get section view location
                Point3d sectionViewLocation = sectionView.Location;
                
                // Temporarily disable automatic elevation range to get proper datum
                bool wasElevationAutomatic = sectionView.IsElevationRangeAutomatic;
                try
                {
                    sectionView.IsElevationRangeAutomatic = false;
                    double elevationDatum = sectionView.ElevationMin;

                    Polyline polyline = new();
                    int vertexIndex = 0;

                    foreach (SectionPoint sectionPoint in sectionPoints)
                    {
                        Point3d sectionLocation = sectionPoint.Location;
                        
                        // Convert section coordinates to drawing coordinates
                        double x = sectionViewLocation.X + sectionLocation.X;
                        double y = sectionViewLocation.Y + (sectionLocation.Y - elevationDatum);
                        
                        polyline.AddVertexAt(vertexIndex, new Point2d(x, y), 0, 0, 0);
                        vertexIndex++;
                    }

                    return polyline;
                }
                finally
                {
                    // Restore original elevation range setting
                    sectionView.IsElevationRangeAutomatic = wasElevationAutomatic;
                }
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi tạo polyline từ section: {ex.Message}");
                return null;
            }
        }

        private static List<Point3d> TransformPolylineVertices(SectionView sectionView, Polyline polyline, 
            double station, Alignment alignment)
        {
            if (sectionView == null || polyline == null || alignment == null)
            {
                throw new ArgumentNullException("Invalid parameters for coordinate transformation");
            }

            var worldPoints = new List<Point3d>();
            
            try
            {
                // Get section view location and datum elevation
                Point3d sectionViewLocation = sectionView.Location;
                bool wasElevationAutomatic = sectionView.IsElevationRangeAutomatic;
                
                try
                {
                    sectionView.IsElevationRangeAutomatic = false;
                    double elevationDatum = sectionView.ElevationMin;

                    for (int i = 0; i < polyline.NumberOfVertices; i++)
                    {
                        Point2d vertex = polyline.GetPoint2dAt(i);
                        
                        // Calculate offset and elevation from section view coordinates
                        double offset = vertex.X - sectionViewLocation.X;
                        double elevation = vertex.Y - sectionViewLocation.Y + elevationDatum;

                        // Transform to world coordinates using alignment
                        double easting = 0, northing = 0;
                        alignment.PointLocation(station, offset, ref easting, ref northing);
                        
                        worldPoints.Add(new Point3d(easting, northing, elevation));
                    }
                }
                finally
                {
                    // Restore original elevation range setting
                    sectionView.IsElevationRangeAutomatic = wasElevationAutomatic;
                }
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi trong quá trình chuyển đổi tọa độ: {ex.Message}");
                throw;
            }
            
            return worldPoints;
        }
    }
}

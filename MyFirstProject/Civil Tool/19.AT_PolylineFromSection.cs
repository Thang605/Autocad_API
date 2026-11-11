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
[assembly: CommandClass(typeof(Civil3DCsharp.AT_PolylineFromSection_Commands))]

namespace Civil3DCsharp
{
    public class AT_PolylineFromSection_Commands
    {
        [CommandMethod("CTS_Polyline_Tu_Section")]
        public static void ATPolylineFromSection()
        {
            // start transaction
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();

                // Step 1: Choose section directly
                A.Ed.WriteMessage("\nChọn section để tạo polyline:");
                PromptEntityOptions peo = new("\nChọn section: ");
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

                // Step 2: Get section view and surface information from section
                SectionView? sectionView = null;
                SampleLine? currentSampleLine = null;
                CivSurface? sourceSurface = null;
                
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
                
                foreach (ObjectId entId in btr)
                {
                    try
                    {
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
                                    SectionSourceCollection sources = slGroup.GetSectionSources();
                                    foreach (SectionSource source in sources)
                                    {
                                        try
                                        {
                                            ObjectId testSectionId = sl.GetSectionId(source.SourceId);
                                            if (testSectionId == sectionId)
                                            {
                                                sectionView = sv;
                                                currentSampleLine = sl;
                                                
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

                if (sectionView == null || currentSampleLine == null)
                {
                    A.Ed.WriteMessage("\nKhông tìm thấy section view chứa section này.");
                    return;
                }

                // Step 3: Create polyline from section points
                SectionPointCollection sectionPoints = section.SectionPoints;
                if (sectionPoints.Count < 2)
                {
                    A.Ed.WriteMessage("\nSection không có đủ điểm để tạo polyline.");
                    return;
                }

                // Create polyline from section points in section view coordinate system
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                Polyline sectionPolyline = CreatePolylineFromSection(sectionView, sectionPoints);
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
                if (sectionPolyline == null)
                {
                    A.Ed.WriteMessage("\nKhông thể tạo polyline từ section.");
                    return;
                }

                // Add polyline to drawing for user to see and edit
                BlockTableRecord? acBlkTblRec = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                if (acBlkTblRec == null)
                {
                    A.Ed.WriteMessage("\nKhông thể truy cập Model Space để ghi.");
                    return;
                }

                sectionPolyline.ColorIndex = 1; // Red color for visibility
                sectionPolyline.Layer = "0";
                sectionPolyline.LineWeight = LineWeight.LineWeight100; // Thicker line for better visibility

                ObjectId polylineId = acBlkTblRec.AppendEntity(sectionPolyline);
                tr.AddNewlyCreatedDBObject(sectionPolyline, true);

                A.Ed.WriteMessage($"\nĐã tạo polyline MÀU ĐỎ DÀY từ section với {sectionPolyline.NumberOfVertices} điểm.");
                A.Ed.WriteMessage($"\nPolyline ID: {polylineId}");

                // Step 4: Ask user if they want to automatically add polyline to surface
                if (sourceSurface != null)
                {
                    A.Ed.WriteMessage($"\nĐã tìm thấy surface nguồn: '{sourceSurface.Name}'");
                    
                    PromptKeywordOptions pko = new PromptKeywordOptions("\nBạn có muốn tự động thêm polyline này vào surface không? [Có/Không]");
                    pko.Keywords.Add("Có");
                    pko.Keywords.Add("C");
                    pko.Keywords.Add("Không");
                    pko.Keywords.Add("K");
                    pko.Keywords.Default = "Có";
                    pko.AllowNone = true;

                    PromptResult pkr = A.Ed.GetKeywords(pko);
                    
                    if (pkr.Status == PromptStatus.OK && (pkr.StringResult == "Có" || pkr.StringResult == "C" || string.IsNullOrEmpty(pkr.StringResult)))
                    {
                        // User wants to add polyline to surface automatically
                        try
                        {
                            // Get parent alignment for transformation
                            ObjectId alignmentId = currentSampleLine.GetParentAlignmentId();
                            Alignment? alignment = tr.GetObject(alignmentId, OpenMode.ForWrite) as Alignment;
                            if (alignment == null)
                            {
                                A.Ed.WriteMessage("\nKhông thể lấy thông tin alignment để transform polyline.");
                            }
                            else
                            {
                                // Transform polyline vertices to world coordinates
                                double station = currentSampleLine.Station;
                                List<Point3d> adjustedPoints = TransformPolylineVertices(sectionView, sectionPolyline, station, alignment);

                                if (adjustedPoints.Count < 2)
                                {
                                    A.Ed.WriteMessage("\nKhông đủ điểm để tạo 3D polyline (cần ít nhất 2 điểm).");
                                }
                                else
                                {
                                    // Create 3D Polyline from adjusted points
                                    Point3dCollection point3dCollection = new Point3dCollection();
                                    foreach (Point3d point in adjustedPoints)
                                    {
                                        point3dCollection.Add(point);
                                    }
                                    
                                    Polyline3d polyline3d = new Polyline3d(Poly3dType.SimplePoly, point3dCollection, false);
                                    polyline3d.ColorIndex = 2; // Yellow color for visibility
                                    polyline3d.Layer = "0";

                                    // Add 3D polyline to drawing
                                    ObjectId polyline3dId = acBlkTblRec.AppendEntity(polyline3d);
                                    tr.AddNewlyCreatedDBObject(polyline3d, true);

                                    A.Ed.WriteMessage($"\nĐã tạo 3D polyline với {adjustedPoints.Count} điểm.");

                                    // Add 3D polyline as breakline to surface
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
                                }
                            }
                        }
                        catch (System.Exception ex)
                        {
                            A.Ed.WriteMessage($"\nLỗi khi tự động xử lý polyline: {ex.Message}");
                        }
                    }
                    else
                    {
                        A.Ed.WriteMessage("\nĐã bỏ qua việc tự động thêm vào surface.");
                        A.Ed.WriteMessage("\nHãy chỉnh sửa polyline này theo ý muốn.");
                        A.Ed.WriteMessage("\nSau đó chạy lệnh CTSV_DIEUCHINH_DUONGTUNHIEN để áp dụng lên surface.");
                    }
                }
                else
                {
                    A.Ed.WriteMessage("\nKhông tìm thấy surface nguồn.");
                    A.Ed.WriteMessage("\nHãy chỉnh sửa polyline này theo ý muốn.");
                    A.Ed.WriteMessage("\nSau đó chạy lệnh CTSV_DIEUCHINH_DUONGTUNHIEN để áp dụng lên surface.");
                }

                // Try to zoom to polyline
                try
                {
                    // Calculate bounds
                    List<Point2d> vertices = new List<Point2d>();
                    for (int i = 0; i < sectionPolyline.NumberOfVertices; i++)
                    {
                        vertices.Add(sectionPolyline.GetPoint2dAt(i));
                    }

                    if (vertices.Count > 0)
                    {
                        double minX = vertices.Min(p => p.X);
                        double maxX = vertices.Max(p => p.X);
                        double minY = vertices.Min(p => p.Y);
                        double maxY = vertices.Max(p => p.Y);

                        // Add margin
                        double margin = Math.Max(maxX - minX, maxY - minY) * 0.2;

                        ViewTableRecord view = new()
                        {
                            CenterPoint = new Point2d((minX + maxX) / 2, (minY + maxY) / 2),
                            Height = Math.Max(maxY - minY + margin, 10.0),
                            Width = Math.Max(maxX - minX + margin, 10.0)
                        };
                        A.Ed.SetCurrentView(view);
                        
                        A.Ed.WriteMessage("\nĐã zoom đến polyline.");
                    }
                }
                catch (System.Exception ex)
                {
                    A.Ed.WriteMessage($"\nKhông thể zoom: {ex.Message}");
                }

                A.Ed.WriteMessage($"\n=== HOÀN THÀNH ===");

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
                tr.Abort();
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

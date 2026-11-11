// (C) Copyright 2015 by  
//
using System;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Autodesk.AutoCAD.Runtime;
using Acad = Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;
using ATable = Autodesk.AutoCAD.DatabaseServices.Table;
using AcadDocument = Autodesk.AutoCAD.ApplicationServices.Application;

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
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using Autodesk.AutoCAD.Colors;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using MyFirstProject.Extensions;
using AcadRegion = Autodesk.AutoCAD.DatabaseServices.Region;
// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3DCsharp.AT_TaoOutline_Command))]

namespace Civil3DCsharp
{
    class AT_TaoOutline_Command
    {
       
        [CommandMethod("AT_Tao_Outline")]
        public static void AT_TaoOutline()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            try
            {
                // Yêu cầu người dùng chọn nhiều Polyline
                PromptSelectionOptions selOptions = new()
                {
                    MessageForAdding = "\nChọn các Polyline để tạo outline: ",
                    AllowDuplicates = false
                };

                // Tạo filter chỉ chọn Polyline
                TypedValue[] filterList = new TypedValue[]
          {
 new TypedValue((int)DxfCode.Start, "LWPOLYLINE")
              };
                SelectionFilter filter = new SelectionFilter(filterList);

                PromptSelectionResult selResult = ed.GetSelection(selOptions, filter);
                if (selResult.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\nĐã hủy chọn đối tượng.");
                    return;
                }

                SelectionSet selSet = selResult.Value;
                Database db = doc.Database;

                // Bắt đầu giao dịch
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                    int successCount = 0;
                    int totalCount = selSet.Count;

                    foreach (SelectedObject selObj in selSet)
                    {
                        if (selObj != null)
                        {
                            Polyline? poly = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as Polyline;
                            if (poly != null)
                            {
                                try
                                {
                                    // Lấy độ dày outline từ GlobalWidth của polyline
                                    double outlineThickness = poly.ConstantWidth / 2.0;

                                    // Nếu GlobalWidth = 0, bỏ qua
                                    if (outlineThickness <= 0)
                                    {
                                        ed.WriteMessage($"\nPolyline (ID: {selObj.ObjectId}) có GlobalWidth = 0, bỏ qua.");
                                        continue;
                                    }

                                    // Tạo outline bằng cách offset cả hai phía
                                    DBObjectCollection? outerOffsets = poly.GetOffsetCurves(outlineThickness);
                                    DBObjectCollection? innerOffsets = poly.GetOffsetCurves(-outlineThickness);

                                    // Kiểm tra có tạo được offset không
                                    if ((outerOffsets == null || outerOffsets.Count == 0) &&
                                (innerOffsets == null || innerOffsets.Count == 0))
                                    {
                                        ed.WriteMessage($"\nKhông thể tạo outline cho Polyline (ID: {selObj.ObjectId}).");
                                        continue;
                                    }

                                    // Kiểm tra polyline gốc có closed không
                                    bool isClosed = poly.Closed;

                                    Polyline outlinePoly = new Polyline();
                                    outlinePoly.Layer = poly.Layer;

                                    if (isClosed)
                                    {
                                        // Trường hợp CLOSED: Tạo outline bằng Region
                                        // Sử dụng Region để tạo hình "donut"
                                        DBObjectCollection outerRegions = new DBObjectCollection();
                                        DBObjectCollection innerRegions = new DBObjectCollection();

                                        try
                                        {
                                            // Tạo region từ outer offset
                                            if (outerOffsets != null && outerOffsets.Count > 0)
                                            {
                                                DBObjectCollection outerCurves = new DBObjectCollection();
                                                outerCurves.Add(outerOffsets[0]);
                                                outerRegions = AcadRegion.CreateFromCurves(outerCurves);
                                            }

                                            // Tạo region từ inner offset
                                            if (innerOffsets != null && innerOffsets.Count > 0)
                                            {
                                                DBObjectCollection innerCurves = new DBObjectCollection();
                                                innerCurves.Add(innerOffsets[0]);
                                                innerRegions = AcadRegion.CreateFromCurves(innerCurves);
                                            }

                                            // Trừ inner ra khỏi outer để tạo donut
                                            if (outerRegions.Count > 0 && innerRegions.Count > 0)
                                            {
                                                AcadRegion outerRegion = (AcadRegion)outerRegions[0];
                                                AcadRegion innerRegion = (AcadRegion)innerRegions[0];
                                                outerRegion.BooleanOperation(BooleanOperationType.BoolSubtract, innerRegion);

                                                // Thêm region vào drawing
                                                btr.AppendEntity(outerRegion);
                                                tr.AddNewlyCreatedDBObject(outerRegion, true);
                                                successCount++;

                                                ed.WriteMessage($"\nĐã tạo outline (Region) cho Polyline closed (ID: {selObj.ObjectId}) với độ dày: {outlineThickness * 2:F3} trên layer '{poly.Layer}'");
                                                
                                                // Dispose
                                                innerRegion.Dispose();
                                                // outerRegion đã được thêm vào drawing, không dispose
                                            }

                                            // Dispose offset curves
                                            if (outerOffsets != null)
                                            {
                                                foreach (Autodesk.AutoCAD.DatabaseServices.DBObject obj in outerOffsets)
                                                {
                                                    obj.Dispose();
                                                }
                                            }
                                            if (innerOffsets != null)
                                            {
                                                foreach (Autodesk.AutoCAD.DatabaseServices.DBObject obj in innerOffsets)
                                                {
                                                    obj.Dispose();
                                                }
                                            }
                                            continue; // Bỏ qua phần xử lý dưới
                                        }
                                        catch (System.Exception regEx)
                                        {
                                            ed.WriteMessage($"\nKhông thể tạo Region outline: {regEx.Message}. Sử dụng phương pháp polyline...");
                                            // Fallback to polyline method below
                                        }
                                    }

                                    // Trường hợp KHÔNG CLOSED hoặc Region failed
                                    // Tạo polyline outline khép kín
                                    outlinePoly.Closed = true;

                                    int vertexIndex = 0;

                                    // Xác định xem polyline gốc có closed không
                                    // bool isClosed = poly.Closed; // Đã khai báo ở trên

                                    // Thêm các điểm từ outer offset
                                    if (outerOffsets != null && outerOffsets.Count > 0)
                                    {
                                        Polyline? outerPoly = outerOffsets[0] as Polyline;
                                        if (outerPoly != null)
                                        {
                                            for (int i = 0; i < outerPoly.NumberOfVertices; i++)
                                            {
                                                Point2d pt = outerPoly.GetPoint2dAt(i);
                                                double bulge = outerPoly.GetBulgeAt(i);
                                                outlinePoly.AddVertexAt(vertexIndex++, pt, bulge, 0, 0);
                                            }
                                            
                                            // Nếu outer closed, ta đã có một vòng hoàn chỉnh
                                            // Cần đặt bulge của vertex cuối = 0 để nối sang inner
                                            if (!isClosed && outlinePoly.NumberOfVertices > 0)
                                            {
                                                // Đặt bulge của vertex cuối cùng = 0 (nối thẳng sang inner)
                                                outlinePoly.SetBulgeAt(vertexIndex - 1, 0);
                                            }
                                        }
                                    }

                                    // Thêm các điểm từ inner offset (đảo ngược để tạo vòng khép kín)
                                    if (innerOffsets != null && innerOffsets.Count > 0)
                                    {
                                        Polyline? innerPoly = innerOffsets[0] as Polyline;
                                        if (innerPoly != null)
                                        {
                                            // Đi ngược từ cuối về đầu để tạo vòng khép kín
                                            for (int i = innerPoly.NumberOfVertices - 1; i >= 0; i--)
                                            {
                                                Point2d pt = innerPoly.GetPoint2dAt(i);
                                                   
                                               // Xử lý bulge khi đảo ngược
                                                double bulge = 0;

                                                if (i > 0)
                                                  {
                                                       // Lấy bulge của vertex trước (i-1) và đảo dấu
                                                     bulge = -innerPoly.GetBulgeAt(i - 1);
                                                  }
                                                  else if (innerPoly.Closed)
                                                  {
                                                   // Vertex đầu tiên: nếu closed thì lấy bulge cuối
                                                  bulge = -innerPoly.GetBulgeAt(innerPoly.NumberOfVertices - 1);
                                                  }
                                                 // Nếu không closed và i == 0, bulge = 0
                                                  outlinePoly.AddVertexAt(vertexIndex++, pt, bulge, 0, 0);
                                            }

                                            // Đặt bulge của vertex cuối cùng = 0 để nối thẳng về vertex đầu
                                            if (!isClosed && outlinePoly.NumberOfVertices > 0)
                                            {
                                                outlinePoly.SetBulgeAt(vertexIndex - 1, 0);
                                            }
                                       }
                                    }
     
                                    // Điều chỉnh bulge của vertex cuối cùng để nối về vertex đầu tiên
                                    if (outlinePoly.NumberOfVertices > 0 && outlinePoly.Closed)
 {
     // Vertex cuối cùng cần bulge = 0 để nối thẳng về vertex đầu
         // (hoặc giữ nguyên nếu cần arc)
        // Do ta đã set Closed = true, AutoCAD sẽ tự động nối
           }

                                    // Sao chép các thuộc tính từ đối tượng gốc
                                    outlinePoly.Color = poly.Color;
                                    outlinePoly.Linetype = poly.Linetype;
                                    outlinePoly.LinetypeScale = poly.LinetypeScale;
                                    outlinePoly.LineWeight = poly.LineWeight;
                                    outlinePoly.Elevation = poly.Elevation;
                                    // Đảm bảo ConstantWidth = 0
                                    outlinePoly.ConstantWidth = 0;

                                    // Thêm outline vào drawing
                                    if (outlinePoly.NumberOfVertices > 0)
                                    {
                                        btr.AppendEntity(outlinePoly);
                                        tr.AddNewlyCreatedDBObject(outlinePoly, true);
                                        successCount++;

                                        ed.WriteMessage($"\nĐã tạo outline cho Polyline (ID: {selObj.ObjectId}) với độ dày: {outlineThickness * 2:F3} trên layer '{poly.Layer}'");
                                    }

                                    // Dispose các offset curves
                                    if (outerOffsets != null)
                                    {
                                        foreach (Autodesk.AutoCAD.DatabaseServices.DBObject obj in outerOffsets)
                                        {
                                            obj.Dispose();
                                        }
                                    }
                                    if (innerOffsets != null)
                                    {
                                        foreach (Autodesk.AutoCAD.DatabaseServices.DBObject obj in innerOffsets)
                                        {
                                            obj.Dispose();
                                        }
                                    }
                                }
                                catch (System.Exception ex)
                                {
                                    ed.WriteMessage($"\nLỗi khi tạo outline cho Polyline ID {selObj.ObjectId}: {ex.Message}");
                                    continue;
                                }
                            }
                        }
                    }

                    tr.Commit();

                    // Thông báo kết quả
                    ed.WriteMessage($"\n{new string('=', 50)}");
                    ed.WriteMessage($"\nHoàn thành tạo outline: {successCount}/{totalCount} đối tượng được xử lý thành công.");
                    ed.WriteMessage($"\nCác đối tượng outline đã được tạo trên cùng layer với đối tượng nguồn.");
                    ed.WriteMessage($"\n{new string('=', 50)}");
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\nLỗi: " + ex.Message);
            }
        }
    }
}

// (C) Copyright 2024 by T27
// Lệnh thể hiện độ dốc so với trục X

using System;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.ApplicationServices;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(Civil3DCsharp.AT_DocNgang))]

namespace Civil3DCsharp
{
    public class AT_DocNgang
    {
        /// <summary>
        /// Lệnh tính và hiển thị độ dốc giữa 2 điểm so với trục X
        /// Độ dốc được tính theo phần trăm (%) và góc (độ)
        /// </summary>
        [CommandMethod("AT_Do_Doc")]
        public static void AT_DoDoc()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            try
            {
                // Yêu cầu user chọn điểm đầu
                PromptPointOptions ppo1 = new PromptPointOptions("\nChọn điểm ĐẦU (điểm thấp hơn): ");
                ppo1.AllowNone = false;
                PromptPointResult ppr1 = ed.GetPoint(ppo1);
                
                if (ppr1.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\nĐã hủy lệnh.");
                    return;
                }
                Point3d point1 = ppr1.Value;

                // Yêu cầu user chọn điểm cuối với rubber band
                PromptPointOptions ppo2 = new PromptPointOptions("\nChọn điểm CUỐI (điểm cao hơn): ");
                ppo2.AllowNone = false;
                ppo2.UseBasePoint = true;
                ppo2.BasePoint = point1;
                PromptPointResult ppr2 = ed.GetPoint(ppo2);
                
                if (ppr2.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\nĐã hủy lệnh.");
                    return;
                }
                Point3d point2 = ppr2.Value;

                // Tính khoảng cách theo trục X và Y
                double deltaX = Math.Abs(point2.X - point1.X);
                double deltaY = point2.Y - point1.Y;

                // Kiểm tra nếu deltaX = 0 (đường thẳng đứng)
                if (deltaX < 0.0001)
                {
                    ed.WriteMessage("\n⚠ Đường thẳng gần như thẳng đứng (không thể tính độ dốc).");
                    ed.WriteMessage("\n    ΔX = {0:F4}, ΔY = {1:F4}", deltaX, deltaY);
                    return;
                }

                // Tính độ dốc theo phần trăm
                double slopePercent = (deltaY / deltaX) * 100.0;
                
                // Tính góc so với trục X (độ)
                double angleRad = Math.Atan2(deltaY, deltaX);
                double angleDeg = angleRad * (180.0 / Math.PI);

                // Tính tỉ lệ dốc (ví dụ: 1:10)
                double slopeRatio = deltaX / Math.Abs(deltaY);

                // Tính chiều dài đoạn thẳng
                double length = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

                // Xác định hướng dốc
                string direction = deltaY >= 0 ? "XUỐNG DỐC ↘" : "LÊN DỐC ↗";
                if (Math.Abs(deltaY) < 0.0001)
                    direction = "NGANG →";

                // Hiển thị kết quả trong Command Line
                ed.WriteMessage("\n");
                ed.WriteMessage("\n╔══════════════════════════════════════════════════════════╗");
                ed.WriteMessage("\n║           KẾT QUẢ TÍNH ĐỘ DỐC SO VỚI TRỤC X              ║");
                ed.WriteMessage("\n╠══════════════════════════════════════════════════════════╣");
                ed.WriteMessage("\n║  Điểm 1: ({0:F3}, {1:F3})", point1.X, point1.Y);
                ed.WriteMessage("\n║  Điểm 2: ({0:F3}, {1:F3})", point2.X, point2.Y);
                ed.WriteMessage("\n╠══════════════════════════════════════════════════════════╣");
                ed.WriteMessage("\n║  ΔX (khoảng cách ngang):    {0,12:F3} m", deltaX);
                ed.WriteMessage("\n║  ΔY (chênh cao):            {0,12:F3} m", deltaY);
                ed.WriteMessage("\n║  Chiều dài đoạn thẳng:      {0,12:F3} m", length);
                ed.WriteMessage("\n╠══════════════════════════════════════════════════════════╣");
                ed.WriteMessage("\n║  ★ ĐỘ DỐC:                  {0,12:F2} %", Math.Abs(slopePercent));
                ed.WriteMessage("\n║  ★ GÓC VỚI TRỤC X:          {0,12:F2} °", Math.Abs(angleDeg));
                if (Math.Abs(deltaY) >= 0.0001)
                    ed.WriteMessage("\n║  ★ TỈ LỆ DỐC:              1 : {0:F1}", Math.Abs(slopeRatio));
                ed.WriteMessage("\n║  ★ HƯỚNG:                   {0}", direction);
                ed.WriteMessage("\n╚══════════════════════════════════════════════════════════╝");
                ed.WriteMessage("\n");

                // Hỏi user có muốn vẽ text hiển thị độ dốc không
                PromptKeywordOptions pko = new PromptKeywordOptions("\nBạn có muốn vẽ text thể hiện độ dốc? ");
                pko.Keywords.Add("Co");
                pko.Keywords.Add("Khong");
                pko.Keywords.Default = "Co";
                pko.AllowNone = true;
                
                PromptResult pkr = ed.GetKeywords(pko);
                
                if (pkr.Status == PromptStatus.OK || pkr.Status == PromptStatus.None)
                {
                    string keyword = pkr.StringResult;
                    if (string.IsNullOrEmpty(keyword) || keyword == "Co")
                    {
                        // Vẽ text tại vị trí user chọn
                        DrawSlopeText(point1, point2, slopePercent, angleDeg, slopeRatio);
                    }
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n❌ Lỗi: " + ex.Message);
            }
        }

        /// <summary>
        /// Lệnh tính độ dốc đơn giản - chỉ hiển thị kết quả
        /// </summary>
        [CommandMethod("AT_Do_Doc_Simple")]
        public static void AT_DoDoc_Simple()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            try
            {
                // Yêu cầu user chọn 2 điểm
                PromptPointOptions ppo1 = new PromptPointOptions("\nChọn điểm 1: ");
                PromptPointResult ppr1 = ed.GetPoint(ppo1);
                if (ppr1.Status != PromptStatus.OK) return;

                PromptPointOptions ppo2 = new PromptPointOptions("\nChọn điểm 2: ");
                ppo2.UseBasePoint = true;
                ppo2.BasePoint = ppr1.Value;
                PromptPointResult ppr2 = ed.GetPoint(ppo2);
                if (ppr2.Status != PromptStatus.OK) return;

                Point3d p1 = ppr1.Value;
                Point3d p2 = ppr2.Value;

                // Tính toán
                double dx = p2.X - p1.X;
                double dy = p2.Y - p1.Y;
                double angleRad = Math.Atan2(dy, dx);
                double angleDeg = angleRad * (180.0 / Math.PI);
                double slopePercent = Math.Abs(dx) > 0.0001 ? (dy / Math.Abs(dx)) * 100.0 : double.PositiveInfinity;

                // Hiển thị kết quả ngắn gọn
                if (double.IsInfinity(slopePercent))
                {
                    ed.WriteMessage("\n→ Góc với trục X: {0:F2}° (đường thẳng đứng)", angleDeg);
                }
                else
                {
                    ed.WriteMessage("\n→ Độ dốc: {0:F2}% | Góc: {1:F2}°", Math.Abs(slopePercent), angleDeg);
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n❌ Lỗi: " + ex.Message);
            }
        }

        /// <summary>
        /// Vẽ text thể hiện độ dốc trên bản vẽ
        /// </summary>
        private static void DrawSlopeText(Point3d point1, Point3d point2, double slopePercent, double angleDeg, double slopeRatio)
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            // Hỏi vị trí đặt text
            PromptPointOptions ppo = new PromptPointOptions("\nChọn vị trí đặt text độ dốc: ");
            ppo.AllowNone = false;
            PromptPointResult ppr = ed.GetPoint(ppo);
            
            if (ppr.Status != PromptStatus.OK)
            {
                return;
            }
            Point3d textPoint = ppr.Value;

            // Tạo nội dung text
            string slopeText;
            if (Math.Abs(slopeRatio) >= 1 && !double.IsInfinity(slopeRatio) && !double.IsNaN(slopeRatio))
            {
                slopeText = string.Format("i = {0:F2}% (1:{1:F0})", Math.Abs(slopePercent), Math.Abs(slopeRatio));
            }
            else
            {
                slopeText = string.Format("i = {0:F2}%", Math.Abs(slopePercent));
            }

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    // Mở Block Table để đọc
                    BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    
                    // Mở Model Space để ghi
                    BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                    // Lấy tỉ lệ annotation hiện tại
                    double scale = 1.0;
                    try
                    {
                        AnnotationScale annoScale = db.Cannoscale;
                        scale = annoScale.DrawingUnits;
                    }
                    catch { }

                    // Tính góc xoay text dựa theo hướng đoạn thẳng
                    double dx = point2.X - point1.X;
                    double dy = point2.Y - point1.Y;
                    double textAngle = Math.Atan2(dy, dx);
                    
                    // Nếu góc > 90° hoặc < -90°, xoay thêm 180° để text không bị lộn ngược
                    if (textAngle > Math.PI / 2 || textAngle < -Math.PI / 2)
                    {
                        textAngle += Math.PI;
                    }

                    // Tạo MText với nội dung độ dốc
                    MText mtext = new MText();
                    mtext.Location = textPoint;
                    mtext.Contents = slopeText;
                    mtext.TextHeight = 2.5 * scale;
                    mtext.Rotation = textAngle;
                    mtext.Attachment = AttachmentPoint.MiddleCenter;
                    
                    // Thiết lập layer (tạo mới nếu chưa có)
                    string layerName = "0.DO_DOC";
                    LayerTable lt = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
                    if (!lt.Has(layerName))
                    {
                        lt.UpgradeOpen();
                        LayerTableRecord ltr = new LayerTableRecord();
                        ltr.Name = layerName;
                        ltr.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 3); // Màu xanh lá
                        lt.Add(ltr);
                        tr.AddNewlyCreatedDBObject(ltr, true);
                    }
                    mtext.Layer = layerName;

                    // Thêm MText vào Model Space
                    btr.AppendEntity(mtext);
                    tr.AddNewlyCreatedDBObject(mtext, true);

                    // Vẽ thêm đường gạch xiên thể hiện độ dốc
                    DrawSlopeSymbol(tr, btr, textPoint, textAngle, scale, slopePercent);

                    tr.Commit();
                    ed.WriteMessage("\n✓ Đã vẽ text độ dốc tại vị trí đã chọn.");
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage("\n❌ Lỗi khi vẽ text: " + ex.Message);
                    tr.Abort();
                }
            }
        }

        /// <summary>
        /// Vẽ ký hiệu độ dốc (tam giác nhỏ)
        /// </summary>
        private static void DrawSlopeSymbol(Transaction tr, BlockTableRecord btr, Point3d basePoint, double angle, double scale, double slopePercent)
        {
            try
            {
                double symbolSize = 3.0 * scale;
                double yOffset = -4.0 * scale; // Đặt ký hiệu bên dưới text

                // Tính điểm gốc của ký hiệu
                Point3d symbolBase = new Point3d(
                    basePoint.X + yOffset * Math.Sin(angle),
                    basePoint.Y - yOffset * Math.Cos(angle),
                    0
                );

                // Tạo polyline tam giác thể hiện độ dốc
                using (Polyline pline = new Polyline())
                {
                    // Điểm A (góc trái dưới)
                    double ax = symbolBase.X;
                    double ay = symbolBase.Y;

                    // Điểm B (góc phải dưới)
                    double bx = ax + symbolSize * Math.Cos(angle);
                    double by = ay + symbolSize * Math.Sin(angle);

                    // Điểm C (đỉnh tam giác - thể hiện độ cao)
                    double height = symbolSize * Math.Abs(slopePercent) / 100.0;
                    if (height < 0.5 * scale) height = 0.5 * scale; // Tối thiểu
                    if (height > symbolSize) height = symbolSize; // Tối đa
                    
                    // Điểm C nằm ở giữa nhưng cao hơn
                    double cx = (ax + bx) / 2 - height * Math.Sin(angle);
                    double cy = (ay + by) / 2 + height * Math.Cos(angle);

                    pline.AddVertexAt(0, new Point2d(ax, ay), 0, 0, 0);
                    pline.AddVertexAt(1, new Point2d(bx, by), 0, 0, 0);
                    pline.AddVertexAt(2, new Point2d(cx, cy), 0, 0, 0);
                    pline.Closed = true;
                    pline.Layer = "0.DO_DOC";

                    btr.AppendEntity(pline);
                    tr.AddNewlyCreatedDBObject(pline, true);
                }
            }
            catch
            {
                // Bỏ qua lỗi vẽ ký hiệu
            }
        }

        /// <summary>
        /// Lệnh tính độ dốc từ một đường Line hoặc Polyline có sẵn
        /// </summary>
        [CommandMethod("AT_Do_Doc_Object")]
        public static void AT_DoDoc_Object()
        {
            Document doc = AcadApp.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            try
            {
                // Yêu cầu user chọn đối tượng Line hoặc Polyline
                PromptEntityOptions peo = new PromptEntityOptions("\nChọn Line hoặc Polyline để tính độ dốc: ");
                peo.SetRejectMessage("\n⚠ Chỉ chọn Line hoặc Polyline!");
                peo.AddAllowedClass(typeof(Line), true);
                peo.AddAllowedClass(typeof(Polyline), true);
                
                PromptEntityResult per = ed.GetEntity(peo);
                
                if (per.Status != PromptStatus.OK)
                {
                    return;
                }

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    Entity ent = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Entity;
                    
                    Point3d startPt, endPt;

                    if (ent is Line line)
                    {
                        startPt = line.StartPoint;
                        endPt = line.EndPoint;
                    }
                    else if (ent is Polyline pline)
                    {
                        if (pline.NumberOfVertices < 2)
                        {
                            ed.WriteMessage("\n⚠ Polyline phải có ít nhất 2 đỉnh!");
                            return;
                        }
                        startPt = pline.GetPoint3dAt(0);
                        endPt = pline.GetPoint3dAt(pline.NumberOfVertices - 1);
                    }
                    else
                    {
                        ed.WriteMessage("\n⚠ Đối tượng không hợp lệ!");
                        return;
                    }

                    // Tính toán
                    double dx = endPt.X - startPt.X;
                    double dy = endPt.Y - startPt.Y;
                    double length = Math.Sqrt(dx * dx + dy * dy);
                    double angleRad = Math.Atan2(dy, dx);
                    double angleDeg = angleRad * (180.0 / Math.PI);
                    double slopePercent = Math.Abs(dx) > 0.0001 ? (dy / Math.Abs(dx)) * 100.0 : double.PositiveInfinity;

                    // Hiển thị kết quả
                    ed.WriteMessage("\n");
                    ed.WriteMessage("\n╔══════════════════════════════════════════════════════════╗");
                    ed.WriteMessage("\n║               ĐỘ DỐC CỦA ĐỐI TƯỢNG                       ║");
                    ed.WriteMessage("\n╠══════════════════════════════════════════════════════════╣");
                    ed.WriteMessage("\n║  Điểm đầu: ({0:F3}, {1:F3})", startPt.X, startPt.Y);
                    ed.WriteMessage("\n║  Điểm cuối: ({0:F3}, {1:F3})", endPt.X, endPt.Y);
                    ed.WriteMessage("\n║  Chiều dài: {0:F3} m", length);
                    ed.WriteMessage("\n╠══════════════════════════════════════════════════════════╣");
                    
                    if (double.IsInfinity(slopePercent))
                    {
                        ed.WriteMessage("\n║  ★ ĐƯỜNG THẲNG ĐỨNG (góc = {0:F2}°)", angleDeg);
                    }
                    else
                    {
                        ed.WriteMessage("\n║  ★ ĐỘ DỐC:          {0,12:F2} %", Math.Abs(slopePercent));
                        ed.WriteMessage("\n║  ★ GÓC VỚI TRỤC X:  {0,12:F2} °", angleDeg);
                    }
                    ed.WriteMessage("\n╚══════════════════════════════════════════════════════════╝");
                    ed.WriteMessage("\n");

                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n❌ Lỗi: " + ex.Message);
            }
        }
    }
}

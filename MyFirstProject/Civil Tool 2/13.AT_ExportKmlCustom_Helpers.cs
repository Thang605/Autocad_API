using System;
using System.Xml.Linq;
using System.Collections.Generic;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;
using OSGeo.MapGuide;
using Document = Autodesk.AutoCAD.ApplicationServices.Document;
using AcadEntity = Autodesk.AutoCAD.DatabaseServices.Entity;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;

namespace Civil3DCsharp
{
    public partial class AT_ExportKmlCustom_Commands
    {
        private static string StripMTextFormatting(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            string s = input;
            // Thay thế mã xuống dòng \P, \p thành khoảng trắng
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\\[Pp]", " ");
            // Xóa các mã định dạng dạng \tag...; (như \fArial|b0|i0; \C1; \H2.5; \A1; v.v.)
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\\[A-Za-z0-9]+[^;]*;?", "");
            // Xóa các mã định dạng đơn lẻ như \L, \l, \O, \o, \K, \k, \~
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\\[LlOoKk~]", "");
            // Xóa dấu đóng mở ngoặc nhóm {}
            s = s.Replace("{", "").Replace("}", "");
            // Thu gọn khoảng trắng thừa
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ");
            return s.Trim();
        }

        private static void ExtractPrimitivesFromCollection(
            DBObjectCollection collection,
            Transaction tr,
            System.Text.StringBuilder textAccumulator,
            List<List<Point3d>> geometryList)
        {
            if (collection == null) return;
            foreach (DBObject obj in collection)
            {
                if (obj == null) continue;
                try
                {
                    if (obj is DBText dbT)
                    {
                        if (!string.IsNullOrWhiteSpace(dbT.TextString))
                        {
                            textAccumulator.Append(dbT.TextString.Trim() + " ");
                        }
                    }
                    else if (obj is MText mT)
                    {
                        string txt = string.IsNullOrEmpty(mT.Text) ? mT.Contents : mT.Text;
                        string clean = StripMTextFormatting(txt);
                        if (!string.IsNullOrWhiteSpace(clean))
                        {
                            textAccumulator.Append(clean + " ");
                        }
                    }
                    else if (obj is BlockReference blk)
                    {
                        try
                        {
                            DBObjectCollection subCol = new DBObjectCollection();
                            blk.Explode(subCol);
                            ExtractPrimitivesFromCollection(subCol, tr, textAccumulator, geometryList);
                        }
                        catch { }
                    }
                    else if (obj is Leader leader)
                    {
                        List<Point3d> pts = new List<Point3d>();
                        int count = leader.NumVertices;
                        for (int i = 0; i < count; i++) pts.Add(leader.VertexAt(i));
                        if (pts.Count > 0) geometryList.Add(pts);
                    }
                    else if (obj is MLeader mLeader)
                    {
                        foreach (int ldrIdx in mLeader.GetLeaderIndexes())
                        {
                            foreach (int lineIdx in mLeader.GetLeaderLineIndexes(ldrIdx))
                            {
                                List<Point3d> pts = GetMLeaderVertices(mLeader, lineIdx);
                                if (pts.Count > 0) geometryList.Add(pts);
                            }
                        }
                    }
                    else if (obj is Curve curve)
                    {
                        List<Point3d> pts = GetPointsFromCurve(curve, tr);
                        if (pts.Count > 0) geometryList.Add(pts);
                    }
                    else if (obj is Hatch h)
                    {
                        List<List<Point3d>> hLoops = GetPointsFromHatch(h);
                        if (hLoops != null && hLoops.Count > 0)
                        {
                            geometryList.AddRange(hLoops);
                        }
                    }
                }
                finally
                {
                    try { obj.Dispose(); } catch { }
                }
            }
        }

        public static void ProcessCivilLabel(
            Autodesk.Civil.DatabaseServices.Label civLabel,
            Transaction tr,
            out string labelText,
            out Point3d labelLocation,
            out Point3d anchorLocation,
            out List<List<Point3d>> leaderGeometry)
        {
            leaderGeometry = new List<List<Point3d>>();
            Point3d lblLoc = Point3d.Origin;
            try { lblLoc = civLabel.LabelLocation; }
            catch
            {
                try { lblLoc = civLabel.GeometricExtents.MinPoint; } catch { }
            }
            labelLocation = lblLoc;

            Point3d ancLoc = lblLoc;
            try { ancLoc = civLabel.AnchorInfo.Location; }
            catch { }
            anchorLocation = ancLoc;

            System.Text.StringBuilder sbText = new System.Text.StringBuilder();

            // 1. Thử lấy text override trực tiếp từ các text component của Label (cho NoteLabel / General Note)
            try
            {
                foreach (ObjectId compId in civLabel.GetTextComponentIds())
                {
                    string overrideStr = civLabel.GetTextComponentOverride(compId);
                    if (!string.IsNullOrWhiteSpace(overrideStr))
                    {
                        sbText.Append(overrideStr.Trim() + " ");
                    }
                }
            }
            catch { }

            // 2. Nổ in-memory để trích xuất text động đã được Civil 3D tính toán và các đường leader
            try
            {
                DBObjectCollection exploded = new DBObjectCollection();
                civLabel.Explode(exploded);
                ExtractPrimitivesFromCollection(exploded, tr, sbText, leaderGeometry);
            }
            catch { }

            // 3. Nếu vẫn chưa có text, đọc từ LabelStyleTextComponent
            if (sbText.Length == 0)
            {
                try
                {
                    foreach (ObjectId compId in civLabel.GetTextComponentIds())
                    {
                        var comp = tr.GetObject(compId, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Styles.LabelStyleTextComponent;
                        if (comp != null)
                        {
                            string val = comp.Text.Contents.Value;
                            if (!string.IsNullOrWhiteSpace(val))
                            {
                                string cleanVal = StripMTextFormatting(val);
                                cleanVal = System.Text.RegularExpressions.Regex.Replace(cleanVal, @"<\[[^\]]*\]>", "").Trim();
                                if (!string.IsNullOrWhiteSpace(cleanVal)) sbText.Append(cleanVal + " ");
                            }
                        }
                    }
                }
                catch { }
            }

            string finalTxt = sbText.ToString().Trim();
            if (string.IsNullOrEmpty(finalTxt))
            {
                finalTxt = $"Label_{civLabel.Handle}";
            }
            labelText = finalTxt;

            // 4. Nếu không có đoạn nét nào từ explode nhưng Anchor khác LabelLocation:
            // Tự tạo 1 đoạn leader nối từ Anchor tới LabelLocation
            if (leaderGeometry.Count == 0 && ancLoc.DistanceTo(lblLoc) > 0.01)
            {
                leaderGeometry.Add(new List<Point3d> { ancLoc, lblLoc });
            }
        }

        public static void ExportCivilLabel(
            Autodesk.Civil.DatabaseServices.Label civLabel,
            string labelText,
            Point3d labelLocation,
            List<List<Point3d>> leaderGeometry,
            List<List<Point3d>> textOutlines,
            string colorHex,
            XNamespace ns,
            XElement documentNode,
            MgCoordinateSystemTransform transform,
            MgGeometryFactory geoFactory,
            ref int successLines,
            ref int successPoints,
            ref int failed,
            double lineWidth)
        {
            Point3d? wgsLabelPt = TransformPoint(labelLocation, transform, geoFactory);
            if (!wgsLabelPt.HasValue)
            {
                failed++;
                return;
            }

            XElement placemark = new XElement(ns + "Placemark",
                new XElement(ns + "name", labelText),
                new XElement(ns + "description", $"Civil 3D Label: {civLabel.GetType().Name}\nNội dung: {labelText}"),
                new XElement(ns + "Style",
                    new XElement(ns + "LineStyle",
                        new XElement(ns + "color", colorHex),
                        new XElement(ns + "width", lineWidth.ToString("F1"))
                    ),
                    new XElement(ns + "LabelStyle",
                        new XElement(ns + "color", colorHex),
                        new XElement(ns + "scale", "1.0")
                    ),
                    new XElement(ns + "IconStyle",
                        new XElement(ns + "color", colorHex),
                        new XElement(ns + "scale", "0.5")
                    )
                )
            );

            List<XElement> geomElements = new List<XElement>();

            // 1. Điểm đặt nhãn (Text Point) - nơi Google Earth neo nhãn chữ hiển thị trên bản đồ
            XElement pointGeom = new XElement(ns + "Point",
                new XElement(ns + "coordinates", $"{wgsLabelPt.Value.X:F8},{wgsLabelPt.Value.Y:F8},{wgsLabelPt.Value.Z:F3}")
            );
            geomElements.Add(pointGeom);

            // 2. Các đường leader / đường chỉ dẫn
            if (leaderGeometry != null)
            {
                foreach (List<Point3d> pts in leaderGeometry)
                {
                    System.Text.StringBuilder coordStr = new System.Text.StringBuilder();
                    int validCount = 0;
                    foreach (Point3d pt in pts)
                    {
                        Point3d? wgs = TransformPoint(pt, transform, geoFactory);
                        if (wgs.HasValue)
                        {
                            coordStr.Append($"{wgs.Value.X:F8},{wgs.Value.Y:F8},{wgs.Value.Z:F3} ");
                            validCount++;
                        }
                    }

                    if (validCount > 1)
                    {
                        XElement lineString = new XElement(ns + "LineString",
                            new XElement(ns + "tessellate", "1"),
                            new XElement(ns + "coordinates", coordStr.ToString().Trim())
                        );
                        geomElements.Add(lineString);
                    }
                }
            }

            // 3. Nét vẽ chữ vector (nếu có cache)
            if (textOutlines != null)
            {
                foreach (List<Point3d> pts in textOutlines)
                {
                    System.Text.StringBuilder coordStr = new System.Text.StringBuilder();
                    int validCount = 0;
                    foreach (Point3d pt in pts)
                    {
                        Point3d? wgs = TransformPoint(pt, transform, geoFactory);
                        if (wgs.HasValue)
                        {
                            coordStr.Append($"{wgs.Value.X:F8},{wgs.Value.Y:F8},{wgs.Value.Z:F3} ");
                            validCount++;
                        }
                    }

                    if (validCount > 1)
                    {
                        XElement lineString = new XElement(ns + "LineString",
                            new XElement(ns + "tessellate", "1"),
                            new XElement(ns + "coordinates", coordStr.ToString().Trim())
                        );
                        geomElements.Add(lineString);
                    }
                }
            }

            if (geomElements.Count == 1)
            {
                placemark.Add(geomElements[0]);
            }
            else
            {
                XElement multiGeom = new XElement(ns + "MultiGeometry");
                foreach (var g in geomElements)
                {
                    multiGeom.Add(g);
                }
                placemark.Add(multiGeom);
            }

            documentNode.Add(placemark);
            successPoints++;
            if (leaderGeometry != null && leaderGeometry.Count > 0) successLines += leaderGeometry.Count;
        }

        public static List<List<Point3d>> GetPointsFromHatch(Hatch hatch, Matrix3d? transform = null)
        {
            List<List<Point3d>> allLoops = new List<List<Point3d>>();
            if (hatch == null || hatch.NumberOfLoops == 0) return allLoops;

            Plane plane = new Plane(Point3d.Origin, hatch.Normal);
            Matrix3d ocsToWcs = Matrix3d.PlaneToWorld(plane);
            if (transform.HasValue)
            {
                ocsToWcs = transform.Value * ocsToWcs;
            }

            for (int i = 0; i < hatch.NumberOfLoops; i++)
            {
                try
                {
                    HatchLoop loop = hatch.GetLoopAt(i);
                    List<Point3d> loopPts = new List<Point3d>();

                    if (loop.IsPolyline)
                    {
                        BulgeVertexCollection bvc = loop.Polyline;
                        int count = bvc.Count;
                        for (int j = 0; j < count; j++)
                        {
                            BulgeVertex bv = bvc[j];
                            Point2d p1 = bv.Vertex;
                            double bulge = bv.Bulge;
                            Point2d p2 = bvc[(j + 1) % count].Vertex;

                            if (Math.Abs(bulge) < 1e-5)
                            {
                                Point3d ocsPt = new Point3d(p1.X, p1.Y, hatch.Elevation);
                                loopPts.Add(ocsPt.TransformBy(ocsToWcs));
                            }
                            else
                            {
                                try
                                {
                                    using (CircularArc2d arc = new CircularArc2d(p1, p2, bulge, false))
                                    {
                                        Point2d[] samples = arc.GetSamplePoints(8);
                                        int takeCount = samples.Length > 1 ? samples.Length - 1 : samples.Length;
                                        for (int s = 0; s < takeCount; s++)
                                        {
                                            Point3d ocsPt = new Point3d(samples[s].X, samples[s].Y, hatch.Elevation);
                                            loopPts.Add(ocsPt.TransformBy(ocsToWcs));
                                        }
                                    }
                                }
                                catch
                                {
                                    Point3d ocsPt = new Point3d(p1.X, p1.Y, hatch.Elevation);
                                    loopPts.Add(ocsPt.TransformBy(ocsToWcs));
                                }
                            }
                        }
                    }
                    else
                    {
                        Curve2dCollection curves = loop.Curves;
                        foreach (Curve2d c in curves)
                        {
                            if (c is LineSegment2d line)
                            {
                                Point3d ocsPt = new Point3d(line.StartPoint.X, line.StartPoint.Y, hatch.Elevation);
                                loopPts.Add(ocsPt.TransformBy(ocsToWcs));
                            }
                            else
                            {
                                try
                                {
                                    Point2d[] samples = c.GetSamplePoints(8);
                                    int takeCount = samples.Length > 1 ? samples.Length - 1 : samples.Length;
                                    for (int s = 0; s < takeCount; s++)
                                    {
                                        Point3d ocsPt = new Point3d(samples[s].X, samples[s].Y, hatch.Elevation);
                                        loopPts.Add(ocsPt.TransformBy(ocsToWcs));
                                    }
                                }
                                catch
                                {
                                    Point3d ocsPt = new Point3d(c.StartPoint.X, c.StartPoint.Y, hatch.Elevation);
                                    loopPts.Add(ocsPt.TransformBy(ocsToWcs));
                                }
                            }
                        }
                    }

                    if (loopPts.Count >= 3)
                    {
                        // Đảm bảo vòng khép kín cho KML LinearRing
                        if (loopPts[0].DistanceTo(loopPts[loopPts.Count - 1]) > 1e-4)
                        {
                            loopPts.Add(loopPts[0]);
                        }
                        allLoops.Add(loopPts);
                    }
                }
                catch { }
            }

            // Fallback: nếu không đọc được loops qua API chuẩn, thử explode để lấy các đường biên
            if (allLoops.Count == 0)
            {
                try
                {
                    DBObjectCollection exploded = new DBObjectCollection();
                    hatch.Explode(exploded);
                    foreach (DBObject d in exploded)
                    {
                        if (d is Curve c)
                        {
                            List<Point3d> pts = GetPointsFromCurve(c, null);
                            if (pts.Count >= 2)
                            {
                                if (transform.HasValue)
                                {
                                    for (int pIdx = 0; pIdx < pts.Count; pIdx++)
                                    {
                                        pts[pIdx] = pts[pIdx].TransformBy(transform.Value);
                                    }
                                }
                                if (pts.Count >= 3 && pts[0].DistanceTo(pts[pts.Count - 1]) > 1e-4)
                                {
                                    pts.Add(pts[0]);
                                }
                                allLoops.Add(pts);
                            }
                        }
                        try { d.Dispose(); } catch { }
                    }
                }
                catch { }
            }

            return allLoops;
        }

        public static void ExportHatch(
            Hatch hatch,
            string name,
            string colorHex,
            XNamespace ns,
            XElement documentNode,
            MgCoordinateSystemTransform transform,
            MgGeometryFactory geoFactory,
            ref int successLines,
            ref int failed,
            double lineWidth,
            Matrix3d? combinedTransform = null)
        {
            List<List<Point3d>> loops = GetPointsFromHatch(hatch, transform: combinedTransform);
            if (loops == null || loops.Count == 0)
            {
                failed++;
                return;
            }

            // Xây dựng PolyStyle với độ trong suốt 50% (alpha = 7f)
            string fillColor = "7f" + (colorHex.Length >= 8 ? colorHex.Substring(2) : "ffffff");

            XElement style = new XElement(ns + "Style",
                new XElement(ns + "LineStyle",
                    new XElement(ns + "color", colorHex),
                    new XElement(ns + "width", lineWidth.ToString("F1"))
                ),
                new XElement(ns + "PolyStyle",
                    new XElement(ns + "color", fillColor),
                    new XElement(ns + "fill", "1"),
                    new XElement(ns + "outline", "1")
                )
            );

            List<XElement> polygons = new List<XElement>();

            foreach (List<Point3d> loopPts in loops)
            {
                System.Text.StringBuilder coordStr = new System.Text.StringBuilder();
                int validCount = 0;

                foreach (Point3d pt in loopPts)
                {
                    Point3d? wgsPt = TransformPoint(pt, transform, geoFactory);
                    if (wgsPt.HasValue)
                    {
                        coordStr.Append($"{wgsPt.Value.X:F8},{wgsPt.Value.Y:F8},{wgsPt.Value.Z:F3} ");
                        validCount++;
                    }
                }

                if (validCount >= 3)
                {
                    XElement polygon = new XElement(ns + "Polygon",
                        new XElement(ns + "tessellate", "1"),
                        new XElement(ns + "outerBoundaryIs",
                            new XElement(ns + "LinearRing",
                                new XElement(ns + "coordinates", coordStr.ToString().Trim())
                            )
                        )
                    );
                    polygons.Add(polygon);
                }
            }

            if (polygons.Count > 0)
            {
                XElement placemark = new XElement(ns + "Placemark",
                    new XElement(ns + "name", name),
                    new XElement(ns + "description", $"Hatch: Layer={hatch.Layer}, Pattern={hatch.PatternName}"),
                    style
                );

                if (polygons.Count == 1)
                {
                    placemark.Add(polygons[0]);
                }
                else
                {
                    XElement multiGeometry = new XElement(ns + "MultiGeometry");
                    foreach (var poly in polygons)
                    {
                        multiGeometry.Add(poly);
                    }
                    placemark.Add(multiGeometry);
                }

                documentNode.Add(placemark);
                successLines++;
            }
            else
            {
                failed++;
            }
        }

        private static void CollectTextIds(
            Transaction tr,
            ObjectIdCollection selectedIds,
            HashSet<ObjectId> textIds)
        {
            Editor ed = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor;
            foreach (ObjectId id in selectedIds)
            {
                AcadEntity ent = tr.GetObject(id, OpenMode.ForRead) as AcadEntity;
                if (ent == null) continue;

                if (ent is DBText || ent is MText || ent is Autodesk.Civil.DatabaseServices.Label)
                {
                    textIds.Add(id);
                    ed.WriteMessage($"\n[DEBUG] CollectTextIds ADD: {ent.GetType().FullName} Handle={ent.Handle}");
                }
                else
                {
                    ed.WriteMessage($"\n[DEBUG] CollectTextIds SKIP: {ent.GetType().FullName} Handle={ent.Handle}");
                }
            }
        }

        private static void ExportTextAsVector(
            List<List<Point3d>> cachedPoints,
            string textString,
            string colorHex,
            XNamespace ns,
            XElement documentNode,
            MgCoordinateSystemTransform transform,
            MgGeometryFactory geoFactory,
            ref int successLines,
            ref int failed,
            double width,
            Matrix3d? combinedTransform = null,
            Point3d? fallbackPoint = null)
        {
            if ((cachedPoints == null || cachedPoints.Count == 0) && !fallbackPoint.HasValue)
            {
                failed++;
                return;
            }

            List<XElement> lineStrings = new List<XElement>();

            if (cachedPoints != null)
            {
                foreach (List<Point3d> points in cachedPoints)
                {
                    System.Text.StringBuilder coordStr = new System.Text.StringBuilder();
                    int validCoords = 0;

                    foreach (Point3d pt in points)
                    {
                        Point3d transformedPt = pt;
                        if (combinedTransform.HasValue)
                        {
                            transformedPt = pt.TransformBy(combinedTransform.Value);
                        }

                        Point3d? wgsPt = TransformPoint(transformedPt, transform, geoFactory);
                        if (wgsPt.HasValue)
                        {
                            coordStr.Append($"{wgsPt.Value.X:F8},{wgsPt.Value.Y:F8},{wgsPt.Value.Z:F3} ");
                            validCoords++;
                        }
                    }

                    if (validCoords > 0)
                    {
                        XElement lineString = new XElement(ns + "LineString",
                            new XElement(ns + "tessellate", "1"),
                            new XElement(ns + "coordinates", coordStr.ToString().Trim())
                        );
                        lineStrings.Add(lineString);
                    }
                }
            }

            if (lineStrings.Count > 0)
            {
                XElement placemark = new XElement(ns + "Placemark",
                    new XElement(ns + "name", textString),
                    new XElement(ns + "Style",
                        new XElement(ns + "LineStyle",
                            new XElement(ns + "color", colorHex),
                            new XElement(ns + "width", width.ToString("F1"))
                        ),
                        new XElement(ns + "LabelStyle",
                            new XElement(ns + "scale", "0")
                        )
                    ),
                    new XElement(ns + "MultiGeometry",
                        lineStrings
                    )
                );
                documentNode.Add(placemark);
                successLines++;
            }
            else if (fallbackPoint.HasValue)
            {
                // Fallback khi không có nét vẽ vector: xuất thành Điểm chữ (Point Placemark) có hiển thị nhãn
                Point3d transformedPt = combinedTransform.HasValue ? fallbackPoint.Value.TransformBy(combinedTransform.Value) : fallbackPoint.Value;
                Point3d? wgsPt = TransformPoint(transformedPt, transform, geoFactory);
                if (wgsPt.HasValue)
                {
                    XElement placemark = new XElement(ns + "Placemark",
                        new XElement(ns + "name", textString),
                        new XElement(ns + "Style",
                            new XElement(ns + "LabelStyle",
                                new XElement(ns + "color", colorHex),
                                new XElement(ns + "scale", "1.0")
                            ),
                            new XElement(ns + "IconStyle",
                                new XElement(ns + "scale", "0")
                            )
                        ),
                        new XElement(ns + "Point",
                            new XElement(ns + "coordinates", $"{wgsPt.Value.X:F8},{wgsPt.Value.Y:F8},{wgsPt.Value.Z:F3}")
                        )
                    );
                    documentNode.Add(placemark);
                    successLines++;
                }
                else
                {
                    failed++;
                }
            }
            else
            {
                failed++;
            }
        }

        private static void ProcessBlockReference(
            Document doc,
            BlockReference blockRef,
            Transaction tr,
            MgCoordinateSystemTransform transform,
            MgGeometryFactory geoFactory,
            XNamespace ns,
            XElement documentNode,
            ref int successLines,
            ref int failed,
            Matrix3d parentTransform,
            double blockWidth,
            Dictionary<ObjectId, List<List<Point3d>>> textOutlinesCache,
            Dictionary<ObjectId, string> labelTextsCache,
            HashSet<ObjectId> visitedBlocks)
        {
            try
            {
                // Tránh vòng lặp vô hạn khi block lồng nhau cyclic
                if (visitedBlocks == null || !visitedBlocks.Add(blockRef.BlockTableRecord))
                {
                    return;
                }

                // Hợp nhất ma trận biến đổi của block hiện tại và các block cha trước đó
                Matrix3d combinedTransform = parentTransform * blockRef.BlockTransform;

                using (BlockTableRecord btr = tr.GetObject(blockRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord)
                {
                    if (btr != null)
                    {
                        foreach (ObjectId id in btr)
                        {
                            AcadEntity childEnt = tr.GetObject(id, OpenMode.ForRead) as AcadEntity;
                            if (childEnt == null) continue;

                            // 1. Nếu là Block lồng nhau (Nested Block/XRef), gọi đệ quy với ma trận đã được tích lũy
                            if (childEnt is BlockReference nestedBlockRef)
                            {
                                ProcessBlockReference(doc, nestedBlockRef, tr, transform, geoFactory, ns, documentNode, ref successLines, ref failed, combinedTransform, blockWidth, textOutlinesCache, labelTextsCache, visitedBlocks);
                            }
                            // 1.5 Xử lý Hatch trong block (sử dụng combinedTransform trực tiếp, không qua Clone)
                            else if (childEnt is Hatch hatchEnt)
                            {
                                string hatchColorHex = GetKmlColorString(hatchEnt, tr, blockRef);
                                ExportHatch(hatchEnt, $"{blockRef.Name}_Hatch_{hatchEnt.Handle}", hatchColorHex, ns, documentNode, transform, geoFactory, ref successLines, ref failed, blockWidth, combinedTransform);
                            }
                            else
                            {
                                // Nhân bản và biến đổi hình học sang hệ tọa độ WCS toàn cục
                                using (AcadEntity clonedEnt = childEnt.Clone() as AcadEntity)
                                {
                                    if (clonedEnt != null)
                                    {
                                        clonedEnt.TransformBy(combinedTransform);

                                        // Lấy màu sắc KML (áp dụng luật ByBlock và ByLayer kế thừa từ BlockReference cha)
                                        string childColorHex = GetKmlColorString(clonedEnt, tr, blockRef);

                                        // 2. Xử lý CogoPoint trong block
                                        if (clonedEnt is CogoPoint cogoPoint)
                                        {
                                            Point3d pt = cogoPoint.Location;
                                            Point3d? wgsPt = TransformPoint(pt, transform, geoFactory);
                                            if (wgsPt.HasValue)
                                            {
                                                string name = cogoPoint.PointName;
                                                if (string.IsNullOrEmpty(name))
                                                {
                                                    name = $"Point_{cogoPoint.PointNumber}";
                                                }

                                                XElement placemark = new XElement(ns + "Placemark",
                                                    new XElement(ns + "name", $"{blockRef.Name}_{name}"),
                                                    new XElement(ns + "description", $"CogoPoint #{cogoPoint.PointNumber}\nMô tả: {cogoPoint.RawDescription ?? ""}"),
                                                    new XElement(ns + "Style",
                                                        new XElement(ns + "IconStyle",
                                                            new XElement(ns + "color", childColorHex),
                                                            new XElement(ns + "scale", "1.2")
                                                        )
                                                    ),
                                                    new XElement(ns + "Point",
                                                        new XElement(ns + "coordinates", $"{wgsPt.Value.X:F8},{wgsPt.Value.Y:F8},{wgsPt.Value.Z:F3}")
                                                    )
                                                );
                                                documentNode.Add(placemark);
                                            }
                                        }
                                        // 3. Xử lý các đối tượng Curve (Line, Polyline, Arc...)
                                        else if (clonedEnt is Curve childCurve)
                                        {
                                            List<Point3d> points = GetPointsFromCurve(childCurve, tr);
                                            int subSuccess = 0;
                                            int subFailed = 0;
                                            ExportGeometryList(points, $"{blockRef.Name}_{clonedEnt.GetType().Name}_{clonedEnt.Handle}", childColorHex, ns, documentNode, transform, geoFactory, ref subSuccess, ref subFailed, blockWidth);
                                            successLines += subSuccess;
                                            failed += subFailed;
                                        }
                                        // 3.1 Xử lý đối tượng Leader trong block
                                        else if (clonedEnt is Leader childLeader)
                                        {
                                            List<Point3d> points = new List<Point3d>();
                                            int count = childLeader.NumVertices;
                                            for (int i = 0; i < count; i++)
                                            {
                                                points.Add(childLeader.VertexAt(i));
                                            }
                                            int subSuccess = 0;
                                            int subFailed = 0;
                                            ExportGeometryList(points, $"{blockRef.Name}_{clonedEnt.GetType().Name}_{clonedEnt.Handle}", childColorHex, ns, documentNode, transform, geoFactory, ref subSuccess, ref subFailed, blockWidth);
                                            successLines += subSuccess;
                                            failed += subFailed;
                                        }
                                        // 3.2 Xử lý đối tượng MLeader trong block
                                        else if (clonedEnt is MLeader childMLeader)
                                        {
                                            int subSuccess = 0;
                                            int subFailed = 0;
                                            foreach (int ldrIdx in childMLeader.GetLeaderIndexes())
                                            {
                                                foreach (int lineIdx in childMLeader.GetLeaderLineIndexes(ldrIdx))
                                                {
                                                    List<Point3d> points = GetMLeaderVertices(childMLeader, lineIdx);
                                                    ExportGeometryList(points, $"{blockRef.Name}_{clonedEnt.GetType().Name}_{clonedEnt.Handle}_{ldrIdx}_{lineIdx}", childColorHex, ns, documentNode, transform, geoFactory, ref subSuccess, ref subFailed, blockWidth);
                                                }
                                            }
                                            successLines += subSuccess;
                                            failed += subFailed;
                                        }
                                        // 4. Xử lý các đối tượng Solid (2D Solid) và Face (3D Face)
                                        else if (clonedEnt is Solid || clonedEnt is Face)
                                        {
                                            List<Point3d> points = new List<Point3d>();
                                            try
                                            {
                                                dynamic f = clonedEnt;
                                                points.Add(f.GetPointAt(0));
                                                points.Add(f.GetPointAt(1));
                                                points.Add(f.GetPointAt(3));
                                                points.Add(f.GetPointAt(2));
                                                points.Add(f.GetPointAt(0));
                                            }
                                            catch {}

                                            int subSuccess = 0;
                                            int subFailed = 0;
                                            ExportGeometryList(points, $"{blockRef.Name}_{clonedEnt.GetType().Name}_{clonedEnt.Handle}", childColorHex, ns, documentNode, transform, geoFactory, ref subSuccess, ref subFailed, blockWidth);
                                            successLines += subSuccess;
                                            failed += subFailed;
                                        }
                                        // 5. Xử lý DBText trong block
                                        else if (clonedEnt is DBText dbText)
                                        {
                                            if (textOutlinesCache != null && textOutlinesCache.TryGetValue(childEnt.ObjectId, out List<List<Point3d>> cachedPoints) && cachedPoints != null && cachedPoints.Count > 0)
                                            {
                                                int subSuccess = 0;
                                                int subFailed = 0;
                                                foreach (List<Point3d> pts in cachedPoints)
                                                {
                                                    List<Point3d> transformedPts = new List<Point3d>();
                                                    foreach (Point3d pt in pts)
                                                    {
                                                        transformedPts.Add(pt.TransformBy(combinedTransform));
                                                    }
                                                    ExportGeometryList(transformedPts, $"{blockRef.Name}_Text_{childEnt.Handle}", childColorHex, ns, documentNode, transform, geoFactory, ref subSuccess, ref subFailed, blockWidth);
                                                }
                                                successLines += subSuccess;
                                                failed += subFailed;
                                            }
                                            else
                                            {
                                                // Không có cached outlines → bỏ qua (không tạo placemark)
                                                failed++;
                                            }
                                        }
                                        // 6. Xử lý MText trong block
                                        else if (clonedEnt is MText mText)
                                        {
                                            if (textOutlinesCache != null && textOutlinesCache.TryGetValue(childEnt.ObjectId, out List<List<Point3d>> cachedPoints) && cachedPoints != null && cachedPoints.Count > 0)
                                            {
                                                int subSuccess = 0;
                                                int subFailed = 0;
                                                foreach (List<Point3d> pts in cachedPoints)
                                                {
                                                    List<Point3d> transformedPts = new List<Point3d>();
                                                    foreach (Point3d pt in pts)
                                                    {
                                                        transformedPts.Add(pt.TransformBy(combinedTransform));
                                                    }
                                                    ExportGeometryList(transformedPts, $"{blockRef.Name}_Text_{childEnt.Handle}", childColorHex, ns, documentNode, transform, geoFactory, ref subSuccess, ref subFailed, blockWidth);
                                                }
                                                successLines += subSuccess;
                                                failed += subFailed;
                                            }
                                            else
                                            {
                                                // Không có cached outlines → bỏ qua (không tạo placemark)
                                                failed++;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                failed++;
            }
        }

        private static void ExplodeBlockReferenceRecursive(
            BlockReference blockRef,
            Transaction tr,
            ref List<AcadEntity> resultEntities)
        {
            DBObjectCollection dbObjCol = new DBObjectCollection();
            try
            {
                // Thực hiện rã Block bằng lệnh native của AutoCAD (tự xử lý scale, rotation, dynamic state...)
                blockRef.Explode(dbObjCol);
                foreach (DBObject dbObj in dbObjCol)
                {
                    AcadEntity childEnt = dbObj as AcadEntity;
                    if (childEnt == null)
                    {
                        dbObj.Dispose();
                        continue;
                    }

                    // Nếu là block lồng nhau, tiếp tục rã đệ quy
                    if (childEnt is BlockReference nestedBlockRef)
                    {
                        ExplodeBlockReferenceRecursive(nestedBlockRef, tr, ref resultEntities);
                        nestedBlockRef.Dispose();
                    }
                    else
                    {
                        resultEntities.Add(childEnt);
                    }
                }
            }
            catch
            {
                // Fallback nếu có lỗi: lấy trực tiếp hình học cơ bản từ Block definition
                try
                {
                    using (BlockTableRecord btr = tr.GetObject(blockRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord)
                    {
                        if (btr != null)
                        {
                            Matrix3d mat = blockRef.BlockTransform;
                            foreach (ObjectId id in btr)
                            {
                                AcadEntity defChild = tr.GetObject(id, OpenMode.ForRead) as AcadEntity;
                                if (defChild == null) continue;

                                using (AcadEntity cloned = defChild.Clone() as AcadEntity)
                                {
                                    if (cloned != null)
                                    {
                                        cloned.TransformBy(mat);
                                        if (cloned is BlockReference nestedBRef)
                                        {
                                            ExplodeBlockReferenceRecursive(nestedBRef, tr, ref resultEntities);
                                            nestedBRef.Dispose();
                                        }
                                        else
                                        {
                                            resultEntities.Add(cloned);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }
        }

        private static void ExportGeometryList(
            List<Point3d> points,
            string placemarkName,
            string colorHex,
            XNamespace ns,
            XElement documentNode,
            MgCoordinateSystemTransform transform,
            MgGeometryFactory geoFactory,
            ref int successLines,
            ref int failed,
            double width)
        {
            if (points == null || points.Count == 0)
            {
                failed++;
                return;
            }

            System.Text.StringBuilder coordStr = new System.Text.StringBuilder();
            int validCoords = 0;

            foreach (Point3d pt in points)
            {
                Point3d? wgsPt = TransformPoint(pt, transform, geoFactory);
                if (wgsPt.HasValue)
                {
                    coordStr.Append($"{wgsPt.Value.X:F8},{wgsPt.Value.Y:F8},{wgsPt.Value.Z:F3} ");
                    validCoords++;
                }
            }

            if (validCoords > 0)
            {
                XElement placemark = new XElement(ns + "Placemark",
                    new XElement(ns + "name", placemarkName),
                    new XElement(ns + "Style",
                        new XElement(ns + "LineStyle",
                            new XElement(ns + "color", colorHex),
                            new XElement(ns + "width", width.ToString("F1"))
                        )
                    ),
                    new XElement(ns + "LineString",
                        new XElement(ns + "tessellate", "1"),
                        new XElement(ns + "coordinates", coordStr.ToString().Trim())
                    )
                );
                documentNode.Add(placemark);
                successLines++;
            }
            else
            {
                failed++;
            }
        }

        private static List<Point3d> GetPointsFromCurve(Curve curve, Transaction tr)
        {
            List<Point3d> points = new List<Point3d>();

            if (curve is Line line)
            {
                points.Add(line.StartPoint);
                points.Add(line.EndPoint);
            }
            else if (curve is Polyline polyline)
            {
                if (HasBulges(polyline))
                {
                    try
                    {
                        double length = polyline.Length;
                        double sampleInterval = 0.5; // Lấy mẫu mỗi 0.5m
                        int steps = (int)Math.Ceiling(length / sampleInterval);
                        if (steps < 20) steps = 20;
                        if (steps > 300) steps = 300;
                        
                        double delta = length / steps;
                        for (int i = 0; i <= steps; i++)
                        {
                            try { points.Add(polyline.GetPointAtDist(i * delta)); } catch {}
                        }
                    }
                    catch
                    {
                        for (int i = 0; i < polyline.NumberOfVertices; i++)
                        {
                            points.Add(polyline.GetPoint3dAt(i));
                        }
                        if (polyline.Closed)
                        {
                            points.Add(polyline.GetPoint3dAt(0));
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < polyline.NumberOfVertices; i++)
                    {
                        points.Add(polyline.GetPoint3dAt(i));
                    }
                    if (polyline.Closed)
                    {
                        points.Add(polyline.GetPoint3dAt(0));
                    }
                }
            }
            else if (curve is Polyline3d polyline3d)
            {
                foreach (object obj in polyline3d)
                {
                    if (obj is ObjectId vertId)
                    {
                        PolylineVertex3d vert = tr.GetObject(vertId, OpenMode.ForRead) as PolylineVertex3d;
                        if (vert != null)
                        {
                            points.Add(vert.Position);
                        }
                    }
                    else if (obj is PolylineVertex3d vert)
                    {
                        points.Add(vert.Position);
                    }
                }
            }
            else
            {
                try
                {
                    double length = curve.GetDistanceAtParameter(curve.EndParam) - curve.GetDistanceAtParameter(curve.StartParam);
                    int steps = 100;
                    double delta = length / steps;
                    for (int i = 0; i <= steps; i++)
                    {
                        points.Add(curve.GetPointAtDist(i * delta));
                    }
                }
                catch
                {
                    try
                    {
                        double startParam = curve.StartParam;
                        double endParam = curve.EndParam;
                        int segments = 100;
                        double step = (endParam - startParam) / segments;
                        for (int i = 0; i <= segments; i++)
                        {
                            points.Add(curve.GetPointAtParameter(startParam + (i * step)));
                        }
                    }
                    catch
                    {
                        // Bỏ qua nếu không phân đoạn được
                    }
                }
            }

            return points;
        }

        internal static List<Point3d> GetPointsFromAlignment(Alignment alignment, Transaction tr)
        {
            List<Point3d> points = new List<Point3d>();
            if (alignment == null) return points;

            // Cách 1: Nổ Alignment thành các đối tượng hình học cơ bản (Line, Arc, Polyline, Spline)
            try
            {
                DBObjectCollection subCol = new DBObjectCollection();
                alignment.Explode(subCol);

                if (subCol.Count > 0)
                {
                    foreach (DBObject dbObj in subCol)
                    {
                        if (dbObj is Curve curve)
                        {
                            List<Point3d> curvePts = GetPointsFromCurve(curve, tr);
                            if (curvePts != null && curvePts.Count > 0)
                            {
                                points.AddRange(curvePts);
                            }
                            curve.Dispose();
                        }
                        else
                        {
                            dbObj.Dispose();
                        }
                    }
                }
            }
            catch { }

            // Cách 2 (Fallback): Lấy mẫu qua PointLocation theo Station nếu Explode không ra điểm
            if (points.Count == 0)
            {
                try
                {
                    double startSt = alignment.StartingStation;
                    double endSt = alignment.EndingStation;
                    double len = Math.Abs(endSt - startSt);

                    if (len > 0.001)
                    {
                        int steps = Math.Max(50, Math.Min(10000, (int)Math.Ceiling(len / 1.0)));
                        double delta = len / steps;
                        for (int i = 0; i <= steps; i++)
                        {
                            double st = Math.Min(endSt, startSt + i * delta);
                            try
                            {
                                double easting = 0, northing = 0;
                                alignment.PointLocation(st, 0, ref easting, ref northing);
                                if (Math.Abs(easting) > 0.001 || Math.Abs(northing) > 0.001)
                                {
                                    points.Add(new Point3d(easting, northing, 0));
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }

            return points;
        }

        private static Point3d? TransformPoint(Point3d pt, MgCoordinateSystemTransform transform, MgGeometryFactory geoFactory)
        {
            MgCoordinate sourceCoord = null;
            MgCoordinate targetCoord = null;
            try
            {
                sourceCoord = geoFactory.CreateCoordinateXYZ(pt.X, pt.Y, pt.Z);
                targetCoord = transform.Transform(sourceCoord);
                return new Point3d(targetCoord.GetX(), targetCoord.GetY(), targetCoord.GetZ());
            }
            catch
            {
                return null;
            }
            finally
            {
                if (sourceCoord != null) try { sourceCoord.Dispose(); } catch { }
                if (targetCoord != null) try { targetCoord.Dispose(); } catch { }
            }
        }

        private static MgCoordinateSystem CreateCoordinateSystem(MgCoordinateSystemFactory factory, string srsString)
        {
            if (srsString.Contains("GEOGCS") || srsString.Contains("PROJCS"))
            {
                try { return factory.Create(srsString); } catch { }
            }

            try { return factory.CreateFromCode(srsString); } catch { }
            try { return factory.Create(srsString); } catch { } // fallback

            throw new ArgumentException($"Không thể tạo hệ tọa độ từ chuỗi: '{srsString}'");
        }

        private static bool HasBulges(Polyline polyline)
        {
            for (int i = 0; i < polyline.NumberOfVertices; i++)
            {
                if (polyline.GetBulgeAt(i) != 0.0)
                {
                    return true;
                }
            }
            return false;
        }

        private static string GetKmlColorString(AcadEntity ent, Transaction tr, BlockReference parentBlock = null)
        {
            Autodesk.AutoCAD.Colors.Color acColor = ent.Color;
            
            if (acColor.IsByBlock && parentBlock != null)
            {
                acColor = parentBlock.Color;
            }

            if (acColor.IsByLayer)
            {
                try
                {
                    ObjectId layerId = ent.LayerId;
                    if (parentBlock != null)
                    {
                        using (LayerTableRecord entLayer = tr.GetObject(ent.LayerId, OpenMode.ForRead) as LayerTableRecord)
                        {
                            if (entLayer != null && entLayer.Name == "0")
                            {
                                layerId = parentBlock.LayerId;
                            }
                        }
                    }

                    using (LayerTableRecord layer = tr.GetObject(layerId, OpenMode.ForRead) as LayerTableRecord)
                    {
                        if (layer != null)
                        {
                            acColor = layer.Color;
                        }
                    }
                }
                catch { }
            }

            System.Drawing.Color drawingColor = System.Drawing.Color.White;
            if (!acColor.IsByBlock && !acColor.IsNone)
            {
                try
                {
                    drawingColor = acColor.ColorValue;
                }
                catch
                {
                    drawingColor = System.Drawing.Color.White;
                }
            }

            // KML format: aabbggrr (Alpha, Blue, Green, Red)
            return $"ff{drawingColor.B:x2}{drawingColor.G:x2}{drawingColor.R:x2}";
        }

        private static List<Point3d> GetMLeaderVertices(MLeader mLeader, int leaderLineIndex)
        {
            List<Point3d> points = new List<Point3d>();
            int idx = 0;
            while (true)
            {
                try
                {
                    Point3d pt = mLeader.GetVertex(leaderLineIndex, idx);
                    points.Add(pt);
                    idx++;
                }
                catch
                {
                    break;
                }
            }
            return points;
        }

        private static void ExplodeEntityToPrimitives(DBObject ent, Transaction tr, List<DBObject> primitives)
        {
            if (ent is BlockReference blockRef)
            {
                DBObjectCollection subCol = new DBObjectCollection();
                try
                {
                    blockRef.Explode(subCol);
                    foreach (DBObject subObj in subCol)
                    {
                        ExplodeEntityToPrimitives(subObj, tr, primitives);
                    }
                }
                catch
                {
                    // Fallback to reading from block definition if explode fails
                    try
                    {
                        using (BlockTableRecord btr = tr.GetObject(blockRef.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord)
                        {
                            if (btr != null)
                            {
                                Matrix3d mat = blockRef.BlockTransform;
                                foreach (ObjectId id in btr)
                                {
                                    AcadEntity child = tr.GetObject(id, OpenMode.ForRead) as AcadEntity;
                                    if (child == null) continue;
                                    using (AcadEntity cloned = child.Clone() as AcadEntity)
                                    {
                                        if (cloned != null)
                                        {
                                            cloned.TransformBy(mat);
                                            ExplodeEntityToPrimitives(cloned, tr, primitives);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch {}
                }
                
                if (ent.ObjectId.IsNull)
                {
                    ent.Dispose();
                }
            }
            else if (ent is Autodesk.Civil.DatabaseServices.Label civLabel)
            {
                DBObjectCollection subCol = new DBObjectCollection();
                try
                {
                    civLabel.Explode(subCol);
                    foreach (DBObject subObj in subCol)
                    {
                        ExplodeEntityToPrimitives(subObj, tr, primitives);
                    }
                }
                catch
                {
                    primitives.Add(ent);
                }
            }
            else
            {
                primitives.Add(ent);
            }
        }
    }
}

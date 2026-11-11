using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
// Đảm bảo các using dưới đây đúng namespace của bạn
using MyFirstProject.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;

// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3D_Csharp.Command_XUATBANG_ToaDoPolyline))]

namespace Civil3D_Csharp
{
    /// <summary>
    /// Standalone command for exporting polyline coordinates with intersection calculations
    /// </summary>
    public static class Command_XUATBANG_ToaDoPolyline
    {
        // ==== Data Structures ====
        public class PolylineCoordinateData
        {
            public required string LayerName { get; set; }
            public Point3d LocalOrigin { get; set; }
            public List<Point3d> Vertices { get; set; } = [];
            public List<Point3d> LocalVertices { get; set; } = [];
            public ObjectId PolylineId { get; set; }
        }

        public class IntersectionResult
        {
            public int SequenceNumber { get; set; }
            public required string LayerName { get; set; }
            public double LocalX { get; set; }
            public double LocalY { get; set; }
            public double IntersectionY { get; set; }
        }

        /// <summary>
        /// Main command to export polyline coordinate table with intersections
        /// </summary>
        [CommandMethod("AT_Xuat_Bang_Toa_Do_Polyline")]
        public static void ExportPolylineCoordinateTable()
        {
            try
            {
                var editor = A.Ed;

                editor.WriteMessage("\n" + new string('=', 80));
                editor.WriteMessage("\n📊 XUẤT BẢNG TỌA ĐỘ CÁC ĐƯỜNG POLYLINE");
                editor.WriteMessage("\n" + new string('=', 80));

                // Step 1: Select polylines group 1
                var group1Data = SelectPolylinesWithOrigin("nhóm 1", 1);
                if (group1Data == null || group1Data.Count == 0)
                {
                    editor.WriteMessage("\n⚠ Không có polyline nhóm 1 nào được chọn.");
                    return;
                }

                // Step 2: Select polylines group 2  
                var group2Data = SelectPolylinesWithOrigin("nhóm 2", 2);
                if (group2Data == null || group2Data.Count == 0)
                {
                    editor.WriteMessage("\n⚠ Không có polyline nhóm 2 nào được chọn.");
                    return;
                }

                // Step 3: Check layer matching and create pairs
                var polylinePairs = CreatePolylinePairs(group1Data, group2Data);
                if (polylinePairs.Count == 0)
                {
                    editor.WriteMessage("\n⚠ Không tìm thấy cặp polyline nào có layer trùng nhau.");
                    return;
                }

                editor.WriteMessage($"\n✅ Đã tìm thấy {polylinePairs.Count} cặp polyline có layer trùng nhau:");
                foreach (var pair in polylinePairs)
                {
                    editor.WriteMessage($"   - Layer: {pair.Key}");
                }

                // Step 4: Calculate intersections and create results
                var intersectionResults = CalculateIntersections(polylinePairs);
                if (intersectionResults.Count == 0)
                {
                    editor.WriteMessage("\n⚠ Không tìm thấy điểm giao nào.");
                    return;
                }

                // Step 5: Sort results by layer name then by X coordinate
                var sortedResults = intersectionResults
                    .OrderBy(r => r.LayerName)
                    .ThenBy(r => r.LocalX)
                    .ToList();

                // Update sequence numbers after sorting
                for (int i = 0; i < sortedResults.Count; i++)
                {
                    sortedResults[i].SequenceNumber = i + 1;
                }

                // Step 6: Display results summary
                DisplayResultsSummary(sortedResults);

                // Step 7: Export to AutoCAD table
                CreateAutoCADTable(sortedResults);

                editor.WriteMessage("\n" + new string('=', 80));
                editor.WriteMessage("\n🎉 HOÀN THÀNH XUẤT BẢNG TỌA ĐỘ POLYLINE");
                editor.WriteMessage("\n" + new string('=', 80));
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n❌ Lỗi: {ex.Message}");
            }
        }

        // ==== Helper Methods ====

        private static List<PolylineCoordinateData>? SelectPolylinesWithOrigin(string groupName, int groupNumber)
        {
            try
            {
                var editor = A.Ed;

                editor.WriteMessage($"\n--- BƯỚC {groupNumber}: CHỌN POLYLINE {groupName.ToUpper()} ---");

                // Select polylines
                var polylineIds = UserInput.GSelectionSetWithType($"Chọn các đường polyline {groupName}: ", "LWPOLYLINE");
                if (polylineIds.Count == 0)
                {
                    editor.WriteMessage($"\n⚠ Không có polyline {groupName} nào được chọn.");
                    return null;
                }

                editor.WriteMessage($"\n✅ Đã chọn {polylineIds.Count} polyline {groupName}");

                // Get origin point for local coordinate system
                var originPoint = UserInput.GPoint($"Chọn điểm gốc của hệ trục tọa độ địa phương cho polyline {groupName}: ");
                if (originPoint == Point3d.Origin)
                {
                    editor.WriteMessage($"\n⚠ Không có điểm gốc nào được chọn cho {groupName}.");
                    return null;
                }

                editor.WriteMessage($"\n📍 Điểm gốc {groupName}: X={originPoint.X:F3}, Y={originPoint.Y:F3}");

                // Extract polyline data
                var polylineData = new List<PolylineCoordinateData>();

                using (var tr = A.Db.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId polylineId in polylineIds)
                    {
                        var polyline = tr.GetObject(polylineId, OpenMode.ForRead) as Polyline;
                        if (polyline == null) continue;

                        var data = new PolylineCoordinateData
                        {
                            LayerName = polyline.Layer,
                            LocalOrigin = originPoint,
                            PolylineId = polylineId
                        };

                        // Extract vertices
                        for (int i = 0; i < polyline.NumberOfVertices; i++)
                        {
                            var vertex = polyline.GetPoint3dAt(i);
                            data.Vertices.Add(vertex);

                            // Convert to local coordinates
                            var localVertex = new Point3d(
                                vertex.X - originPoint.X,
                                vertex.Y - originPoint.Y,
                                vertex.Z
                            );
                            data.LocalVertices.Add(localVertex);
                        }

                        polylineData.Add(data);
                    }
                    tr.Commit();
                }

                // Display summary by layer
                var layerSummary = polylineData.GroupBy(p => p.LayerName)
                    .ToDictionary(g => g.Key, g => g.Count());

                editor.WriteMessage($"\n📊 Thống kê polyline {groupName} theo layer:");
                foreach (var layer in layerSummary)
                {
                    editor.WriteMessage($"   - Layer '{layer.Key}': {layer.Value} polyline");
                }

                return polylineData;
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n❌ Lỗi khi chọn polyline {groupName}: {ex.Message}");
                return null;
            }
        }

        private static Dictionary<string, (PolylineCoordinateData group1, List<PolylineCoordinateData> group2)> CreatePolylinePairs(
            List<PolylineCoordinateData> group1Data, List<PolylineCoordinateData> group2Data)
        {
            var pairs = new Dictionary<string, (PolylineCoordinateData, List<PolylineCoordinateData>)>();

            foreach (var poly1 in group1Data)
            {
                var matchingGroup2 = group2Data.Where(p => p.LayerName == poly1.LayerName).ToList();
                if (matchingGroup2.Count > 0)
                {
                    pairs[poly1.LayerName] = (poly1, matchingGroup2);
                }
            }

            return pairs;
        }

        private static List<IntersectionResult> CalculateIntersections(
            Dictionary<string, (PolylineCoordinateData group1, List<PolylineCoordinateData> group2)> polylinePairs)
        {
            var results = new List<IntersectionResult>();

            foreach (var pair in polylinePairs)
            {
                var layerName = pair.Key;
                var group1Poly = pair.Value.group1;
                var group2Polys = pair.Value.group2;

                // For each vertex of group1 polyline
                foreach (var vertex in group1Poly.LocalVertices)
                {
                    // Find intersections with group2 polylines for vertical line through this vertex
                    var intersectionYValues = FindVerticalLineIntersections(vertex.X, group2Polys);

                    // Create result for each intersection
                    foreach (var intersectionY in intersectionYValues)
                    {
                        results.Add(new IntersectionResult
                        {
                            LayerName = layerName,
                            LocalX = vertex.X,
                            LocalY = vertex.Y,
                            IntersectionY = intersectionY
                        });
                    }
                }
            }

            return results;
        }

        private static List<double> FindVerticalLineIntersections(double x, List<PolylineCoordinateData> group2Polys)
        {
            var intersections = new List<double>();

            using (var tr = A.Db.TransactionManager.StartTransaction())
            {
                foreach (var polyData in group2Polys)
                {
                    var polyline = tr.GetObject(polyData.PolylineId, OpenMode.ForRead) as Polyline;
                    if (polyline == null) continue;

                    // Check each segment of the polyline
                    for (int i = 0; i < polyline.NumberOfVertices - (polyline.Closed ? 0 : 1); i++)
                    {
                        var startVertex = polyline.GetPoint3dAt(i);
                        var endVertex = polyline.GetPoint3dAt((i + 1) % polyline.NumberOfVertices);

                        // Convert to local coordinates
                        var startLocal = new Point3d(
                            startVertex.X - polyData.LocalOrigin.X,
                            startVertex.Y - polyData.LocalOrigin.Y,
                            startVertex.Z
                        );

                        var endLocal = new Point3d(
                            endVertex.X - polyData.LocalOrigin.X,
                            endVertex.Y - polyData.LocalOrigin.Y,
                            endVertex.Z
                        );

                        // Check if vertical line intersects this segment
                        var intersectionY = FindVerticalLineSegmentIntersection(x, startLocal, endLocal);
                        if (intersectionY.HasValue)
                        {
                            intersections.Add(intersectionY.Value);
                        }
                    }
                }
                tr.Commit();
            }

            // Remove duplicates and sort
            return [.. intersections.Distinct().OrderBy(y => y)];
        }

        private static double? FindVerticalLineSegmentIntersection(double x, Point3d start, Point3d end)
        {
            const double tolerance = 1e-6;

            // Check if x is within the segment's X range
            var minX = Math.Min(start.X, end.X);
            var maxX = Math.Max(start.X, end.X);

            if (x < minX - tolerance || x > maxX + tolerance)
                return null;

            // Handle vertical segment case
            if (Math.Abs(end.X - start.X) < tolerance)
            {
                if (Math.Abs(x - start.X) < tolerance)
                {
                    // Return midpoint Y for vertical segments
                    return (start.Y + end.Y) / 2;
                }
                return null;
            }

            // Calculate intersection Y using linear interpolation
            var t = (x - start.X) / (end.X - start.X);
            var intersectionY = start.Y + t * (end.Y - start.Y);

            return intersectionY;
        }

        private static void DisplayResultsSummary(List<IntersectionResult> results)
        {
            var editor = A.Ed;

            editor.WriteMessage("\n" + new string('-', 80));
            editor.WriteMessage("\n📊 TÓM TẮT KẾT QUẢ TÍNH TOÁN:");
            editor.WriteMessage("\n" + new string('-', 80));

            editor.WriteMessage($"\n📈 Tổng số điểm giao: {results.Count}");

            var layerStats = results.GroupBy(r => r.LayerName)
                .ToDictionary(g => g.Key, g => g.Count());

            editor.WriteMessage("\n📊 Thống kê theo layer:");
            foreach (var stat in layerStats)
            {
                editor.WriteMessage($"   - Layer '{stat.Key}': {stat.Value} điểm giao");
            }

            // Display first few results as preview
            editor.WriteMessage("\n👁 XEM TRƯỚC DỮ LIỆU (5 dòng đầu):");
            editor.WriteMessage("\n┌─────┬───────────────┬─────────┬─────────┬─────────┐");
            editor.WriteMessage("\n│ STT │     Layer     │    X    │    Y    │ Y Giao  │");
            editor.WriteMessage("\n├─────┼───────────────┼─────────┼─────────┼─────────┤");

            var previewCount = Math.Min(5, results.Count);
            for (int i = 0; i < previewCount; i++)
            {
                var result = results[i];
                editor.WriteMessage($"\n│{result.SequenceNumber,4} │{result.LayerName,15} │{result.LocalX,9:F3} │{result.LocalY,9:F3} │{result.IntersectionY,9:F3} │");
            }

            editor.WriteMessage("\n└─────┴───────────────┴─────────┴─────────┴─────────┘");

            if (results.Count > previewCount)
            {
                editor.WriteMessage($"\n... và {results.Count - previewCount} dòng khác");
            }
        }

        private static void CreateAutoCADTable(List<IntersectionResult> results)
        {
            try
            {
                var editor = A.Ed;

                // Get table insertion point
                var insertionPoint = UserInput.GPoint("\nChọn vị trí đặt bảng tọa độ: ");
                if (insertionPoint == Point3d.Origin)
                {
                    editor.WriteMessage("\n📍 Không có vị trí nào được chọn. Đặt bảng tại gốc tọa độ.");
                    insertionPoint = Point3d.Origin;
                }

                // Prepare data for table creation
                var sequenceNumbers = results.Select(r => r.SequenceNumber.ToString()).ToArray();
                var layerNames = results.Select(r => r.LayerName).ToArray();
                var xCoords = results.Select(r => r.LocalX.ToString("F3")).ToArray();
                var yCoords = results.Select(r => r.LocalY.ToString("F3")).ToArray();
                var intersectionYs = results.Select(r => r.IntersectionY.ToString("F3")).ToArray();

                // Create table
                CreateCustomCoordinateTable(sequenceNumbers, layerNames, xCoords, yCoords, intersectionYs, insertionPoint);

                editor.WriteMessage($"\n✅ Đã tạo bảng AutoCAD thành công với {results.Count} dòng dữ liệu.");
                editor.WriteMessage($"\n📍 Vị trí bảng: X={insertionPoint.X:F3}, Y={insertionPoint.Y:F3}");
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n❌ Lỗi khi tạo bảng: {ex.Message}");
            }
        }

        private static void CreateCustomCoordinateTable(string[] sequenceNumbers, string[] layerNames,
            string[] xCoords, string[] yCoords, string[] intersectionYs, Point3d insertionPoint)
        {
            using var tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                var blockTable = (BlockTable)tr.GetObject(A.Db.BlockTableId, OpenMode.ForRead);
                var blockTableRecord = (BlockTableRecord)tr.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                // Create table
                var table = new Table();
                var rowCount = sequenceNumbers.Length + 2; // +2 for title and header
                var columnCount = 5;

                table.SetSize(rowCount, columnCount);
                table.SetRowHeight(5);
                table.SetColumnWidth(18);
                table.Position = insertionPoint;

                // Set title (merge first row)
                table.MergeCells(CellRange.Create(table, 0, 0, 0, columnCount - 1));
                table.Cells[0, 0].TextHeight = 3;
                table.Cells[0, 0].TextString = "BẢNG TỌA ĐỘ CÁC ĐỈNH POLYLINE VÀ ĐIỂM GIAO";
                table.Cells[0, 0].Alignment = CellAlignment.MiddleCenter;

                // Set headers
                var headers = new[] { "STT", "Tên Layer", "Tọa độ X", "Tọa độ Y", "Y Điểm giao" };
                for (int i = 0; i < headers.Length; i++)
                {
                    table.Cells[1, i].TextHeight = 2.5;
                    table.Cells[1, i].TextString = headers[i];
                    table.Cells[1, i].Alignment = CellAlignment.MiddleCenter;
                }

                // Populate data
                for (int i = 0; i < sequenceNumbers.Length; i++)
                {
                    int rowIndex = i + 2; // Start from row 2 (after title and header)

                    table.Cells[rowIndex, 0].TextHeight = 2;
                    table.Cells[rowIndex, 0].TextString = sequenceNumbers[i];
                    table.Cells[rowIndex, 0].Alignment = CellAlignment.MiddleCenter;

                    table.Cells[rowIndex, 1].TextHeight = 2;
                    table.Cells[rowIndex, 1].TextString = layerNames[i];
                    table.Cells[rowIndex, 1].Alignment = CellAlignment.MiddleCenter;

                    table.Cells[rowIndex, 2].TextHeight = 2;
                    table.Cells[rowIndex, 2].TextString = xCoords[i];
                    table.Cells[rowIndex, 2].Alignment = CellAlignment.MiddleCenter;

                    table.Cells[rowIndex, 3].TextHeight = 2;
                    table.Cells[rowIndex, 3].TextString = yCoords[i];
                    table.Cells[rowIndex, 3].Alignment = CellAlignment.MiddleCenter;

                    table.Cells[rowIndex, 4].TextHeight = 2;
                    table.Cells[rowIndex, 4].TextString = intersectionYs[i];
                    table.Cells[rowIndex, 4].Alignment = CellAlignment.MiddleCenter;
                }

                // Add table to drawing
                table.GenerateLayout();
                blockTableRecord.AppendEntity(table);
                tr.AddNewlyCreatedDBObject(table, true);

                tr.Commit();
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n❌ Lỗi tạo bảng: {ex.Message}");
                tr.Abort();
            }
        }
    }
}
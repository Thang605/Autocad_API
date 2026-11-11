// (C) Copyright 2026 by  
// Tính tổng chiều dài Link trên cắt ngang từ dữ liệu Corridor
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using Autodesk.AutoCAD.Runtime;
using Acad = Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;

using Civil = Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using Autodesk.Civil.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.Civil.ApplicationServices;
using Section = Autodesk.Civil.DatabaseServices.Section;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using MyFirstProject.Extensions;
using MyFirstProject.Civil_Tool;

// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3DCsharp.CTSV_TongChieuDai_CatNgang_Commands))]

namespace Civil3DCsharp
{
    public class CTSV_TongChieuDai_CatNgang_Commands
    {
        /// <summary>
        /// Lệnh tính tổng chiều dài link trên cắt ngang từ dữ liệu Corridor
        /// </summary>
        [CommandMethod("CTS_Tong_Chieu_Dai_Cat_Ngang")]
        public static void CTSV_TongChieuDai_CatNgang()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            try
            {
                ed.WriteMessage("\n=== CTSV_TongChieuDai_CatNgang - Tính Chiều Dài Link trên Cắt Ngang ===\n");

                // Khai báo biến
                ObjectId corridorId = ObjectId.Null;
                ObjectId sampleLineGroupId = ObjectId.Null;
                ObjectId alignmentId = ObjectId.Null;
                List<string> linkCodeNames = new List<string>();

                // 1. Chọn SectionView để lấy thông tin corridor
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    ObjectId sectionViewId = UserInput.GSectionView("Chọn 1 trắc ngang trong nhóm cần tính chiều dài link: ");
                    if (sectionViewId == ObjectId.Null)
                    {
                        ed.WriteMessage("\nKhông thể chọn SectionView.");
                        return;
                    }

                    SectionView? sectionView = tr.GetObject(sectionViewId, OpenMode.ForWrite) as SectionView;
                    if (sectionView == null)
                    {
                        ed.WriteMessage("\nKhông thể mở SectionView.");
                        return;
                    }

                    // Lấy SampleLine và SampleLineGroup
                    SampleLine? sampleLine = tr.GetObject(sectionView.SampleLineId, OpenMode.ForWrite) as SampleLine;
                    if (sampleLine == null)
                    {
                        ed.WriteMessage("\nKhông thể lấy SampleLine từ SectionView.");
                        return;
                    }

                    sampleLineGroupId = sampleLine.GroupId;
                    alignmentId = sampleLine.GetParentAlignmentId();

                    SampleLineGroup? sampleLineGroup = tr.GetObject(sampleLineGroupId, OpenMode.ForWrite) as SampleLineGroup;
                    if (sampleLineGroup == null)
                    {
                        ed.WriteMessage("\nKhông thể mở SampleLineGroup.");
                        return;
                    }

                    ed.WriteMessage($"\n✅ SampleLineGroup: {sampleLineGroup.Name}");

                    // Tìm Corridor từ SectionSources
                    SectionSourceCollection sectionSources = sampleLineGroup.GetSectionSources();
                    foreach (SectionSource source in sectionSources)
                    {
                        if (source.SourceType == SectionSourceType.Corridor && source.IsSampled)
                        {
                            corridorId = source.SourceId;
                            break;
                        }
                    }

                    if (corridorId == ObjectId.Null)
                    {
                        ed.WriteMessage("\n❌ Không tìm thấy Corridor đã sampled trong SectionSources.");
                        ed.WriteMessage("\n   Hãy đảm bảo Corridor đã được thêm vào Section Sources.");
                        return;
                    }

                    // 2. Lấy danh sách Link Codes từ Corridor
                    Corridor? corridor = tr.GetObject(corridorId, OpenMode.ForWrite) as Corridor;
                    if (corridor == null)
                    {
                        ed.WriteMessage("\nKhông thể mở Corridor.");
                        return;
                    }

                    ed.WriteMessage($"\n✅ Corridor: {corridor.Name}");

                    // Duyệt Baselines → BaselineRegions → AppliedAssemblies để lấy link codes
                    HashSet<string> linkCodesSet = new HashSet<string>();

                    foreach (Baseline baseline in corridor.Baselines)
                    {
                        // Chỉ lấy baseline có alignment trùng với alignment của section view
                        if (baseline.AlignmentId != alignmentId)
                            continue;

                        foreach (BaselineRegion region in baseline.BaselineRegions)
                        {
                            // Lấy AppliedAssemblies bằng index
                            AppliedAssemblyCollection appliedAssemblies = region.AppliedAssemblies;
                            if (appliedAssemblies.Count == 0) continue;

                            // Quét assembly đầu tiên để lấy tất cả link codes
                            try
                            {
                                AppliedAssembly appliedAssembly = appliedAssemblies[0];
                                foreach (AppliedSubassembly appliedSub in appliedAssembly.GetAppliedSubassemblies())
                                {
                                    CalculatedLinkCollection links = appliedSub.Links;
                                    foreach (CalculatedLink link in links)
                                    {
                                        foreach (string code in link.CorridorCodes)
                                        {
                                            linkCodesSet.Add(code);
                                        }
                                    }
                                }
                            }
                            catch (System.Exception)
                            {
                                continue;
                            }
                        }
                    }

                    linkCodeNames = linkCodesSet.OrderBy(s => s).ToList();
                    ed.WriteMessage($"\n📋 Tìm thấy {linkCodeNames.Count} link code(s):");
                    foreach (string code in linkCodeNames)
                    {
                        ed.WriteMessage($"\n   - {code}");
                    }

                    tr.Commit();
                }

                if (linkCodeNames.Count == 0)
                {
                    ed.WriteMessage("\n❌ Không tìm thấy link code nào trong Corridor.");
                    return;
                }

                // 3. Hiển thị Form
                LinkLengthForm form = new LinkLengthForm(linkCodeNames);
                var dialogResult = form.ShowDialog();

                if (dialogResult != System.Windows.Forms.DialogResult.OK || !form.FormAccepted)
                {
                    ed.WriteMessage("\nLệnh đã bị hủy.");
                    return;
                }

                string formula = form.Formula;
                string resultLabel = form.ResultLabel;
                double textHeight = form.TextHeight;

                ed.WriteMessage($"\n📐 Công thức: {formula}");
                ed.WriteMessage($"\n📝 Nhãn: {resultLabel}");

                // 4. Chọn vị trí đặt text trên cắt ngang
                Point3d textRefPoint;
                double textOffsetX, textOffsetY;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    // Dùng section view để xác định offset/elevation từ vị trí click
                    ObjectId sectionViewId2 = UserInput.GSectionView("Chọn lại trắc ngang để xác định vị trí đặt kết quả: ");
                    SectionView? svRef = tr.GetObject(sectionViewId2, OpenMode.ForWrite) as SectionView;
                    if (svRef == null)
                    {
                        ed.WriteMessage("\nKhông thể mở SectionView.");
                        return;
                    }

                    Point3d pointClick = UserInput.GPoint("\nChọn vị trí đặt text kết quả trên trắc ngang: ");
                    double refOffset = 0, refElev = 0;
                    svRef.FindOffsetAndElevationAtXY(pointClick.X, pointClick.Y, ref refOffset, ref refElev);
                    textOffsetX = refOffset;
                    textOffsetY = refElev;
                    textRefPoint = pointClick;
                    ed.WriteMessage($"\n✅ Vị trí: offset={textOffsetX:F2}, elevation={textOffsetY:F2}");

                    tr.Commit();
                }

                // 5. Tính toán và ghi kết quả lên tất cả các cắt ngang
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    Corridor? corridor = tr.GetObject(corridorId, OpenMode.ForWrite) as Corridor;
                    if (corridor == null) return;

                    SampleLineGroup? sampleLineGroup = tr.GetObject(sampleLineGroupId, OpenMode.ForWrite) as SampleLineGroup;
                    if (sampleLineGroup == null) return;

                    Alignment? alignment = tr.GetObject(alignmentId, OpenMode.ForWrite) as Alignment;
                    if (alignment == null) return;

                    string layerName = alignment.Name + "_ChieuDaiLink";
                    UtilitiesCAD.CCreateLayer(layerName);

                    // Lấy tất cả section view groups
                    SectionViewGroupCollection sectionViewGroupCollection = sampleLineGroup.SectionViewGroups;
                    SectionViewGroup sectionViewGroup = sectionViewGroupCollection[0];
                    if (sectionViewGroupCollection.Count > 1)
                    {
                        int num = 0;
                        ed.WriteMessage("\nDanh sách nhóm cắt ngang:");
                        foreach (var item in sectionViewGroupCollection)
                        {
                            ed.WriteMessage($"\n  {num}. {sectionViewGroupCollection[num].Name}");
                            num++;
                        }
                        int numPass = UserInput.GInt("Nhập thứ tự nhóm cắt ngang:");
                        sectionViewGroup = sectionViewGroupCollection[numPass];
                    }

                    ObjectIdCollection sectionViewIdColl = sectionViewGroup.GetSectionViewIds();

                    // Xây dựng dictionary chứa chiều dài link theo station
                    // Corridor → Baselines → BaselineRegions → AppliedAssemblies
                    Dictionary<double, Dictionary<string, double>> stationLinkLengths = new Dictionary<double, Dictionary<string, double>>();

                    foreach (Baseline baseline in corridor.Baselines)
                    {
                        if (baseline.AlignmentId != alignmentId)
                            continue;

                        foreach (BaselineRegion region in baseline.BaselineRegions)
                        {
                            IReadOnlyList<double> sortedStations = region.SortedStations();
                            AppliedAssemblyCollection appliedAssemblies = region.AppliedAssemblies;

                            for (int idx = 0; idx < appliedAssemblies.Count && idx < sortedStations.Count; idx++)
                            {
                                double station = sortedStations[idx];
                                try
                                {
                                    AppliedAssembly appliedAssembly = appliedAssemblies[idx];
                                    Dictionary<string, double> linkLengths = new Dictionary<string, double>();

                                    foreach (AppliedSubassembly appliedSub in appliedAssembly.GetAppliedSubassemblies())
                                    {
                                        CalculatedLinkCollection links = appliedSub.Links;
                                        foreach (CalculatedLink link in links)
                                        {
                                            // Tính chiều dài link từ CalculatedPoints
                                            double linkLength = CalculateLinkLength(link);

                                            // Gộp chiều dài theo link code
                                            foreach (string code in link.CorridorCodes)
                                            {
                                                if (linkLengths.ContainsKey(code))
                                                    linkLengths[code] += linkLength;
                                                else
                                                    linkLengths[code] = linkLength;
                                            }
                                        }
                                    }

                                    // Lưu theo station (làm tròn để match với SampleLine station)
                                    double roundedStation = Math.Round(station, 4);
                                    if (!stationLinkLengths.ContainsKey(roundedStation))
                                    {
                                        stationLinkLengths[roundedStation] = linkLengths;
                                    }
                                }
                                catch (System.Exception)
                                {
                                    continue;
                                }
                            }
                        }
                    }

                    ed.WriteMessage($"\n📊 Đã tính chiều dài link tại {stationLinkLengths.Count} stations");

                    // Dữ liệu cho bảng tổng hợp
                    List<string> listLyTrinh = new List<string>();
                    List<string> listTenCoc = new List<string>();
                    List<string> listKetQua = new List<string>();

                    // Mở ModelSpace để tạo text
                    BlockTable? acBlkTbl = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    if (acBlkTbl == null) return;
                    BlockTableRecord? acBlkTblRec = tr.GetObject(acBlkTbl[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                    if (acBlkTblRec == null) return;

                    TextStyleTable? styleTable = tr.GetObject(db.TextStyleTableId, OpenMode.ForRead) as TextStyleTable;
                    ObjectId textStyleId = ObjectId.Null;
                    if (styleTable != null && styleTable.Has("Standard"))
                    {
                        textStyleId = styleTable["Standard"];
                    }

                    int processedCount = 0;

                    // Duyệt từng section view
                    foreach (ObjectId sectionviewId in sectionViewIdColl)
                    {
                        SectionView? sectionView = tr.GetObject(sectionviewId, OpenMode.ForWrite) as SectionView;
                        if (sectionView == null) continue;

                        SampleLine? sampleLine = tr.GetObject(sectionView.SampleLineId, OpenMode.ForWrite) as SampleLine;
                        if (sampleLine == null) continue;

                        double sampleStation = Math.Round(sampleLine.Station, 4);

                        // Tìm station gần nhất trong corridor data
                        Dictionary<string, double>? linkLengthsAtStation = FindClosestStationData(stationLinkLengths, sampleStation);

                        double result = 0;
                        if (linkLengthsAtStation != null)
                        {
                            // Đánh giá công thức
                            result = EvaluateFormula(formula, linkLengthsAtStation);
                        }

                        // Tính vị trí text trên section view
                        double X = sectionView.Location.X;
                        double Y = sectionView.Location.Y;

                        Point3d textPosition = new Point3d(X + textOffsetX, Y + textOffsetY, 0);
                        double textLabelWidth = resultLabel.Length * textHeight * 0.7;
                        Point3d valuePosition = new Point3d(X + textOffsetX + textLabelWidth, Y + textOffsetY, 0);

                        // Tạo text nhãn
                        DBText labelText = new DBText();
                        labelText.SetDatabaseDefaults();
                        labelText.Position = textPosition;
                        labelText.Height = textHeight;
                        labelText.TextString = resultLabel;
                        labelText.Layer = layerName;
                        if (textStyleId != ObjectId.Null)
                            labelText.TextStyleId = textStyleId;
                        acBlkTblRec.AppendEntity(labelText);
                        tr.AddNewlyCreatedDBObject(labelText, true);

                        // Tạo text giá trị
                        DBText valueText = new DBText();
                        valueText.SetDatabaseDefaults();
                        valueText.Position = valuePosition;
                        valueText.Height = textHeight;
                        valueText.TextString = result.ToString("F2") + " m";
                        valueText.Layer = layerName;
                        if (textStyleId != ObjectId.Null)
                            valueText.TextStyleId = textStyleId;
                        acBlkTblRec.AppendEntity(valueText);
                        tr.AddNewlyCreatedDBObject(valueText, true);

                        processedCount++;

                        // Thêm vào danh sách cho bảng tổng hợp
                        listLyTrinh.Add(sampleLine.Station.ToString("N2"));
                        listTenCoc.Add(sampleLine.Name);
                        listKetQua.Add(result.ToString("F2"));
                    }

                    ed.WriteMessage($"\n✅ Đã ghi kết quả lên {processedCount} cắt ngang.");

                    tr.Commit();

                    // 6. Tạo bảng tổng hợp
                    if (listLyTrinh.Count > 0)
                    {
                        UtilitiesCAD.CreateTableKhoiLuong(
                            listLyTrinh.Count, 3,
                            alignment.Name, listLyTrinh, listTenCoc, listKetQua,
                            layerName, "Chiều dài link (" + formula + ")");
                    }
                }

                ed.WriteMessage("\n\n✅ Lệnh CTSV_TongChieuDai_CatNgang hoàn thành!");
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                ed.WriteMessage($"\n❌ Lỗi AutoCAD: {e.Message}");
            }
            catch (System.Exception e)
            {
                ed.WriteMessage($"\n❌ Lỗi: {e.Message}");
                ed.WriteMessage($"\n   Stack: {e.StackTrace}");
            }
        }

        /// <summary>
        /// Tính chiều dài link từ CalculatedPoints (khoảng cách 2D trên cross section)
        /// </summary>
        private static double CalculateLinkLength(CalculatedLink link)
        {
            double totalLength = 0;

            try
            {
                CalculatedPointCollection points = link.CalculatedPoints;
                if (points.Count < 2) return 0;

                for (int i = 0; i < points.Count - 1; i++)
                {
                    CalculatedPoint p1 = points[i];
                    CalculatedPoint p2 = points[i + 1];

                    // Sử dụng tọa độ XYZ (Point3d) để tính khoảng cách
                    Point3d pt1 = p1.XYZ;
                    Point3d pt2 = p2.XYZ;
                    double dx = pt2.X - pt1.X;
                    double dy = pt2.Y - pt1.Y;
                    double dz = pt2.Z - pt1.Z;
                    totalLength += Math.Sqrt(dx * dx + dy * dy + dz * dz);
                }
            }
            catch (System.Exception)
            {
                // Ignore errors
            }

            return totalLength;
        }

        /// <summary>
        /// Tìm dữ liệu station gần nhất với sampleStation
        /// </summary>
        private static Dictionary<string, double>? FindClosestStationData(
            Dictionary<double, Dictionary<string, double>> allData, double targetStation)
        {
            if (allData.Count == 0) return null;

            // Tìm station gần nhất (sai số <= 0.5m)
            double closestStation = allData.Keys.First();
            double minDiff = Math.Abs(closestStation - targetStation);

            foreach (double station in allData.Keys)
            {
                double diff = Math.Abs(station - targetStation);
                if (diff < minDiff)
                {
                    minDiff = diff;
                    closestStation = station;
                }
            }

            if (minDiff <= 0.5)
            {
                return allData[closestStation];
            }

            return null;
        }

        /// <summary>
        /// Đánh giá công thức dạng [LinkCode1] + [LinkCode2] - [LinkCode3] * 2
        /// </summary>
        private static double EvaluateFormula(string formula, Dictionary<string, double> linkLengths)
        {
            try
            {
                // Thay thế [LinkCode] bằng giá trị thực
                string expression = formula;
                Regex regex = new Regex(@"\[([^\]]+)\]");
                
                expression = regex.Replace(expression, match =>
                {
                    string linkCode = match.Groups[1].Value;
                    if (linkLengths.ContainsKey(linkCode))
                    {
                        return linkLengths[linkCode].ToString("F6",
                            System.Globalization.CultureInfo.InvariantCulture);
                    }
                    return "0";
                });

                // Đánh giá biểu thức toán học đơn giản
                return EvaluateMathExpression(expression);
            }
            catch (System.Exception)
            {
                return 0;
            }
        }

        /// <summary>
        /// Đánh giá biểu thức toán học đơn giản hỗ trợ +, -, *, /, ()
        /// </summary>
        private static double EvaluateMathExpression(string expression)
        {
            try
            {
                // Xóa khoảng trắng thừa
                expression = expression.Trim();
                if (string.IsNullOrEmpty(expression)) return 0;

                // Sử dụng DataTable.Compute để đánh giá biểu thức
                var dt = new System.Data.DataTable();
                var result = dt.Compute(expression, "");
                return Convert.ToDouble(result);
            }
            catch (System.Exception)
            {
                return 0;
            }
        }
    }
}

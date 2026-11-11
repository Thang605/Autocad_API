// (C) Copyright 2015 by  
// Thêm Bảng Khối Lượng Cắt Ngang (Volume Tables) cho Section View Group
//
using System;
using System.Collections.Generic;
using System.Linq;

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
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using MyFirstProject.Extensions;
using MyFirstProject.Civil_Tool;
using ATable = Autodesk.AutoCAD.DatabaseServices.Table;
using Section = Autodesk.Civil.DatabaseServices.Section;

// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3DCsharp.CTSV_VolumeTable_Commands))]

namespace Civil3DCsharp
{
    public class CTSV_VolumeTable_Commands
    {
        /// <summary>
        /// Lệnh thêm Volume Tables (Bảng Khối Lượng) cho Section View Group
        /// </summary>
        [CommandMethod("CTS_Them_Bang_KL_Cat_Ngang")]
        public static void CTSV_Them_BangKL_CatNgang()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            try
            {
                ed.WriteMessage("\n=== CTSV_Them_BangKL_CatNgang - Thêm Bảng Khối Lượng Cắt Ngang ===\n");

                // Khai báo các biến cần thiết
                ObjectId sampleLineGroupId = ObjectId.Null;
                string sampleLineGroupName = "";
                List<KeyValuePair<string, ObjectId>> materialListList = new List<KeyValuePair<string, ObjectId>>();
                List<KeyValuePair<string, ObjectId>> tableStyleList = new List<KeyValuePair<string, ObjectId>>();

                // 1. Chọn SectionView và lấy thông tin
                using (Transaction tr = db.TransactionManager.StartOpenCloseTransaction())
                {
                    ObjectId sectionViewId = UserInput.GSectionView("Chọn 1 trắc ngang trong nhóm cần thêm Bảng Khối Lượng: ");
                    if (sectionViewId == ObjectId.Null)
                    {
                        ed.WriteMessage("\nKhông thể chọn SectionView.");
                        return;
                    }

                    SectionView? sectionView = tr.GetObject(sectionViewId, OpenMode.ForRead) as SectionView;
                    if (sectionView == null)
                    {
                        ed.WriteMessage("\nKhông thể mở SectionView.");
                        return;
                    }

                    // Lấy SampleLine và SampleLineGroup từ SectionView
                    ObjectId sampleLineId = sectionView.SampleLineId;
                    SampleLine? sampleLine = tr.GetObject(sampleLineId, OpenMode.ForRead) as SampleLine;
                    if (sampleLine == null)
                    {
                        ed.WriteMessage("\nKhông thể lấy SampleLine từ SectionView.");
                        return;
                    }

                    sampleLineGroupId = sampleLine.GroupId;
                    SampleLineGroup? sampleLineGroup = tr.GetObject(sampleLineGroupId, OpenMode.ForRead) as SampleLineGroup;
                    if (sampleLineGroup == null)
                    {
                        ed.WriteMessage("\nKhông thể mở SampleLineGroup.");
                        return;
                    }

                    sampleLineGroupName = sampleLineGroup.Name;
                    ed.WriteMessage($"\n✅ Đã chọn SampleLineGroup: {sampleLineGroupName}");

                    // Lấy danh sách Material Lists từ SampleLineGroup
                    try
                    {
                        QTOMaterialListCollection materialLists = sampleLineGroup.MaterialLists;
                        ed.WriteMessage($"\n📋 Tìm thấy {materialLists.Count} Material List(s)");
                        
                        int idx = 0;
                        foreach (QTOMaterialList materialList in materialLists)
                        {
                            if (materialList != null)
                            {
                                // Sử dụng Name làm key thay vì Id
                                materialListList.Add(new KeyValuePair<string, ObjectId>(materialList.Name, ObjectId.Null));
                                ed.WriteMessage($"\n   - {materialList.Name}");
                                idx++;
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        ed.WriteMessage($"\n⚠️ Lỗi đọc Material Lists: {ex.Message}");
                    }

                    tr.Commit();
                }

                // Lấy danh sách Table Styles - sử dụng các style mặc định
                // Civil 3D TableStyles API phức tạp, sử dụng defaults thay thế
                tableStyleList.Add(new KeyValuePair<string, ObjectId>("KL đào đắp 1-1000", ObjectId.Null));
                tableStyleList.Add(new KeyValuePair<string, ObjectId>("Standard", ObjectId.Null));
                tableStyleList.Add(new KeyValuePair<string, ObjectId>("Basic", ObjectId.Null));
                ed.WriteMessage($"\n📐 Sử dụng {tableStyleList.Count} Table Style(s) mặc định");

                if (materialListList.Count == 0)
                {
                    ed.WriteMessage("\n❌ Không tìm thấy Material List nào. Vui lòng tạo Material List trước!");
                    ed.WriteMessage("\n   Sử dụng lệnh CTS_Them_MaterialList để tạo Material List.");
                    return;
                }

                if (tableStyleList.Count == 0)
                {
                    ed.WriteMessage("\n⚠️ Không tìm thấy Table Style. Sẽ sử dụng style mặc định.");
                    // Thêm style mặc định
                    tableStyleList.Add(new KeyValuePair<string, ObjectId>("Standard", ObjectId.Null));
                }

                // 2. Hiển thị Form
                VolumeTableForm form = new VolumeTableForm(sampleLineGroupName, materialListList, tableStyleList);
                
                // Apply event handler
                form.OnApplyClicked += (sender, e) =>
                {
                    ApplyVolumeTables(doc, db, ed, sampleLineGroupId, form);
                };

                var dialogResult = form.ShowDialog();

                if (dialogResult != System.Windows.Forms.DialogResult.OK || !form.FormAccepted)
                {
                    ed.WriteMessage("\nLệnh đã bị hủy.");
                    return;
                }

                // 3. Tạo Volume Tables
                ApplyVolumeTables(doc, db, ed, sampleLineGroupId, form);

                ed.WriteMessage("\n\n✅ Lệnh CTSV_Them_BangKL_CatNgang hoàn thành thành công!");
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
        /// Áp dụng Volume Tables vào Sample Line Group
        /// </summary>
        private static void ApplyVolumeTables(Document doc, Database db, Editor ed, ObjectId sampleLineGroupId, VolumeTableForm form)
        {
            // Yêu cầu người dùng chọn điểm chèn bảng
            PromptPointOptions ppo = new PromptPointOptions("\nChọn điểm chèn bảng khối lượng: ");
            ppo.AllowNone = false;
            PromptPointResult ppr = ed.GetPoint(ppo);
            
            if (ppr.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\nĐã hủy chọn điểm chèn.");
                return;
            }

            Point3d insertPoint = ppr.Value;

            using (DocumentLock docLock = doc.LockDocument())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    try
                    {
                        SampleLineGroup? sampleLineGroup = tr.GetObject(sampleLineGroupId, OpenMode.ForWrite) as SampleLineGroup;
                        if (sampleLineGroup == null)
                        {
                            ed.WriteMessage("\n❌ Không thể mở SampleLineGroup.");
                            tr.Abort();
                            return;
                        }

                        ed.WriteMessage($"\n🔄 Đang xử lý {form.VolumeTables.Count} Volume Table(s)...");

                        // Lấy khối lượng từ Material Lists
                        QTOMaterialListCollection materialLists = sampleLineGroup.MaterialLists;
                        
                        // Tạo danh sách dữ liệu khối lượng
                        List<VolumeData> volumeDataList = new List<VolumeData>();

                        foreach (var tableConfig in form.VolumeTables)
                        {
                            try
                            {
                                ed.WriteMessage($"\n   - Bảng: {tableConfig.TableType} / {tableConfig.Style}");
                                ed.WriteMessage($"\n     Material List: {tableConfig.MaterialList}");
                                
                                // Tìm Material List theo tên
                                QTOMaterialList? selectedMaterialList = null;
                                foreach (QTOMaterialList ml in materialLists)
                                {
                                    if (ml.Name == tableConfig.MaterialList)
                                    {
                                        selectedMaterialList = ml;
                                        break;
                                    }
                                }

                                if (selectedMaterialList != null)
                                {
                                    // Lấy khối lượng từ các materials trong list
                                    for (int i = 0; i < selectedMaterialList.Count; i++)
                                    {
                                        QTOMaterial material = selectedMaterialList[i];
                                        string materialName = material.Name;
                                        MaterialQuantityType quantityType = material.QuantityType;

                                        // Tính khối lượng từ TotalVolume của SampleLineGroup
                                        double totalVolume = 0;
                                        try
                                        {
                                            // Sử dụng method GetTotalVolumeResultDataForMaterialList nếu có
                                            // Hoặc tính từ sections
                                            totalVolume = CalculateMaterialVolume(sampleLineGroup, material, tr, ed);
                                        }
                                        catch (System.Exception ex)
                                        {
                                            ed.WriteMessage($"\n       ⚠️ Lỗi tính khối lượng: {ex.Message}");
                                        }

                                        volumeDataList.Add(new VolumeData
                                        {
                                            MaterialListName = tableConfig.MaterialList,
                                            MaterialName = materialName,
                                            QuantityType = quantityType.ToString(),
                                            Volume = totalVolume
                                        });

                                        ed.WriteMessage($"\n       {materialName} ({quantityType}): {totalVolume:F2} m³");
                                    }
                                }
                                else
                                {
                                    ed.WriteMessage($"\n     ⚠️ Không tìm thấy Material List: {tableConfig.MaterialList}");
                                }
                            }
                            catch (System.Exception ex)
                            {
                                ed.WriteMessage($"\n     ⚠️ Lỗi: {ex.Message}");
                            }
                        }

                        // Tạo bảng AutoCAD từ dữ liệu
                        if (volumeDataList.Count > 0)
                        {
                            CreateVolumeTableInDrawing(db, tr, volumeDataList, insertPoint, sampleLineGroup.Name, form);
                            ed.WriteMessage($"\n\n✅ Đã tạo bảng khối lượng với {volumeDataList.Count} dòng dữ liệu.");
                        }
                        else
                        {
                            ed.WriteMessage("\n⚠️ Không có dữ liệu khối lượng để tạo bảng.");
                        }

                        tr.Commit();
                    }
                    catch (System.Exception ex)
                    {
                        ed.WriteMessage($"\n❌ Lỗi khi tạo Volume Tables: {ex.Message}");
                        ed.WriteMessage($"\n   Stack: {ex.StackTrace}");
                        tr.Abort();
                    }
                }
            }
        }

        /// <summary>
        /// Tính khối lượng cho một material dựa trên các section sources
        /// </summary>
        private static double CalculateMaterialVolume(SampleLineGroup sampleLineGroup, QTOMaterial material, Transaction tr, Editor ed)
        {
            double totalVolume = 0;

            try
            {
                // Lấy danh sách sample lines
                ObjectIdCollection sampleLineIds = sampleLineGroup.GetSampleLineIds();
                if (sampleLineIds.Count < 2)
                {
                    ed.WriteMessage("\n       ⚠️ Cần ít nhất 2 sample lines để tính khối lượng.");
                    return 0;
                }

                // Lấy các surface IDs từ section sources của SampleLineGroup
                SectionSourceCollection sectionSources = sampleLineGroup.GetSectionSources();
                ObjectId surface1Id = ObjectId.Null;
                ObjectId surface2Id = ObjectId.Null;

                // Tìm EG (TN) surface và Datum (Top) surface
                foreach (SectionSource source in sectionSources)
                {
                    if (source.SourceType == SectionSourceType.TinSurface ||
                        source.SourceType == SectionSourceType.CorridorSurface)
                    {
                        try
                        {
                            var entity = tr.GetObject(source.SourceId, OpenMode.ForRead);
                            string sourceName = "";
                            var nameProperty = entity.GetType().GetProperty("Name");
                            if (nameProperty != null)
                            {
                                sourceName = nameProperty.GetValue(entity)?.ToString() ?? "";
                            }

                            if (sourceName.ToLower().Contains("eg") || sourceName.ToLower().Contains("tn"))
                            {
                                surface1Id = source.SourceId;
                            }
                            else if (sourceName.ToLower().Contains("top") || sourceName.ToLower().Contains("datum"))
                            {
                                surface2Id = source.SourceId;
                            }
                        }
                        catch { /* Ignore */ }
                    }
                }

                // Nếu không tìm được surfaces cụ thể, lấy 2 surfaces đầu tiên
                if (surface1Id == ObjectId.Null || surface2Id == ObjectId.Null)
                {
                    List<ObjectId> allSurfaceIds = new List<ObjectId>();
                    foreach (SectionSource source in sectionSources)
                    {
                        if (source.SourceType == SectionSourceType.TinSurface ||
                            source.SourceType == SectionSourceType.CorridorSurface)
                        {
                            allSurfaceIds.Add(source.SourceId);
                        }
                    }

                    if (allSurfaceIds.Count >= 2)
                    {
                        surface1Id = allSurfaceIds[0];
                        surface2Id = allSurfaceIds[1];
                    }
                    else
                    {
                        ed.WriteMessage("\n       ⚠️ Cần ít nhất 2 surfaces để tính khối lượng.");
                        return 0;
                    }
                }

                // Tính khối lượng bằng phương pháp trung bình mặt cắt
                double prevStation = 0;
                double prevArea = 0;
                bool isFirst = true;

                foreach (ObjectId sampleLineId in sampleLineIds)
                {
                    if (!sampleLineId.IsValid) continue;

                    SampleLine? sampleLine = tr.GetObject(sampleLineId, OpenMode.ForRead) as SampleLine;
                    if (sampleLine == null) continue;

                    double station = sampleLine.Station;
                    double area = 0;

                    try
                    {
                        // Lấy sections cho 2 surfaces
                        ObjectId section1Id = sampleLine.GetSectionId(surface1Id);
                        ObjectId section2Id = sampleLine.GetSectionId(surface2Id);

                        if (section1Id.IsValid && section2Id.IsValid)
                        {
                            Section? section1 = tr.GetObject(section1Id, OpenMode.ForRead) as Section;
                            Section? section2 = tr.GetObject(section2Id, OpenMode.ForRead) as Section;

                            if (section1 != null && section2 != null)
                            {
                                // Tính diện tích giữa 2 mặt cắt
                                area = CalculateAreaBetweenSections(section1, section2, material.QuantityType);
                            }
                        }
                    }
                    catch { /* Ignore errors for individual sections */ }

                    // Tính khối lượng bằng phương pháp trung bình mặt cắt
                    if (!isFirst && (area > 0 || prevArea > 0))
                    {
                        double distance = Math.Abs(station - prevStation);
                        totalVolume += (prevArea + area) / 2.0 * distance;
                    }

                    prevStation = station;
                    prevArea = area;
                    isFirst = false;
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n       ⚠️ Lỗi tính khối lượng: {ex.Message}");
            }

            return totalVolume;
        }

        /// <summary>
        /// Tính diện tích giữa 2 mặt cắt
        /// </summary>
        private static double CalculateAreaBetweenSections(Section section1, Section section2, MaterialQuantityType quantityType)
        {
            double area = 0;

            try
            {
                var points1 = section1.SectionPoints;
                var points2 = section2.SectionPoints;

                if (points1 == null || points2 == null || points1.Count < 2 || points2.Count < 2)
                    return 0;

                // Tìm phạm vi offset chung
                double minOffset = double.MaxValue;
                double maxOffset = double.MinValue;

                for (int i = 0; i < points1.Count; i++)
                {
                    double offset = points1[i].Location.X;
                    minOffset = Math.Min(minOffset, offset);
                    maxOffset = Math.Max(maxOffset, offset);
                }
                for (int i = 0; i < points2.Count; i++)
                {
                    double offset = points2[i].Location.X;
                    minOffset = Math.Min(minOffset, offset);
                    maxOffset = Math.Max(maxOffset, offset);
                }

                // Tính diện tích bằng cách lấy mẫu
                int numSamples = 50;
                double step = (maxOffset - minOffset) / numSamples;

                for (int i = 0; i < numSamples; i++)
                {
                    double offset = minOffset + i * step;
                    double elev1 = InterpolateElevation(points1, offset);
                    double elev2 = InterpolateElevation(points2, offset);

                    double diff = elev2 - elev1;

                    // Cut: section2 (datum) dưới section1 (EG) - cần đào
                    // Fill: section2 (datum) trên section1 (EG) - cần đắp
                    if (quantityType == MaterialQuantityType.Cut && diff < 0)
                    {
                        area += Math.Abs(diff) * step;
                    }
                    else if (quantityType == MaterialQuantityType.Fill && diff > 0)
                    {
                        area += diff * step;
                    }
                }
            }
            catch { /* Ignore errors */ }

            return area;
        }

        /// <summary>
        /// Nội suy cao độ tại một offset cho trước
        /// </summary>
        private static double InterpolateElevation(SectionPointCollection points, double targetOffset)
        {
            if (points == null || points.Count == 0)
                return 0;

            if (points.Count == 1)
                return points[0].Location.Y;

            // Tìm 2 điểm bao quanh offset
            double prevOffset = points[0].Location.X;
            double prevElev = points[0].Location.Y;

            for (int i = 1; i < points.Count; i++)
            {
                double currOffset = points[i].Location.X;
                double currElev = points[i].Location.Y;

                if ((prevOffset <= targetOffset && targetOffset <= currOffset) ||
                    (currOffset <= targetOffset && targetOffset <= prevOffset))
                {
                    // Nội suy tuyến tính
                    if (Math.Abs(currOffset - prevOffset) < 0.0001)
                        return (prevElev + currElev) / 2;
                    
                    double t = (targetOffset - prevOffset) / (currOffset - prevOffset);
                    return prevElev + t * (currElev - prevElev);
                }

                prevOffset = currOffset;
                prevElev = currElev;
            }

            // Nếu offset ngoài phạm vi, trả về cao độ điểm gần nhất
            double firstOffset = points[0].Location.X;
            double lastOffset = points[points.Count - 1].Location.X;

            if (Math.Abs(targetOffset - firstOffset) < Math.Abs(targetOffset - lastOffset))
                return points[0].Location.Y;
            else
                return points[points.Count - 1].Location.Y;
        }

        /// <summary>
        /// Tạo bảng AutoCAD trong bản vẽ
        /// </summary>
        private static void CreateVolumeTableInDrawing(Database db, Transaction tr, List<VolumeData> volumeDataList, Point3d insertPoint, string groupName, VolumeTableForm form)
        {
            BlockTable? bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
            if (bt == null) return;

            BlockTableRecord? btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
            if (btr == null) return;

            // Xác định số dòng và cột
            int numRows = volumeDataList.Count + 2; // Title + Header + Data
            int numCols = 3; // Material List, Loại vật liệu, Khối lượng

            ATable table = new ATable();
            table.SetSize(numRows, numCols);
            table.Position = insertPoint;
            table.TableStyle = db.Tablestyle;

            // Thiết lập chiều rộng cột
            table.Columns[0].Width = 50;  // Material List
            table.Columns[1].Width = 50;  // Loại vật liệu
            table.Columns[2].Width = 40;  // Khối lượng

            // Tiêu đề
            string title = $"BẢNG KHỐI LƯỢNG ĐÀO ĐẮP - {groupName}";
            table.Cells[0, 0].TextString = title;
            table.MergeCells(CellRange.Create(table, 0, 0, 0, numCols - 1));
            table.Cells[0, 0].Alignment = CellAlignment.MiddleCenter;
            table.Cells[0, 0].TextHeight = 5.0;
            table.Rows[0].Height = 12.0;

            // Header
            table.Cells[1, 0].TextString = "Material List";
            table.Cells[1, 1].TextString = "Loại vật liệu";
            table.Cells[1, 2].TextString = "Khối lượng (m³)";
            for (int col = 0; col < numCols; col++)
            {
                table.Cells[1, col].Alignment = CellAlignment.MiddleCenter;
                table.Cells[1, col].TextHeight = 4.0;
            }
            table.Rows[1].Height = 10.0;

            // Dữ liệu
            double totalCut = 0, totalFill = 0;
            for (int i = 0; i < volumeDataList.Count; i++)
            {
                var data = volumeDataList[i];
                int row = i + 2;

                table.Cells[row, 0].TextString = data.MaterialListName;
                table.Cells[row, 1].TextString = data.MaterialName;
                table.Cells[row, 2].TextString = data.Volume.ToString("F2");

                table.Cells[row, 0].Alignment = CellAlignment.MiddleLeft;
                table.Cells[row, 1].Alignment = CellAlignment.MiddleLeft;
                table.Cells[row, 2].Alignment = CellAlignment.MiddleCenter;

                for (int col = 0; col < numCols; col++)
                {
                    table.Cells[row, col].TextHeight = 3.5;
                }
                table.Rows[row].Height = 8.0;

                // Tính tổng
                if (data.QuantityType == "Cut")
                    totalCut += data.Volume;
                else if (data.QuantityType == "Fill")
                    totalFill += data.Volume;
            }

            // Thêm bảng vào model space
            btr.AppendEntity(table);
            tr.AddNewlyCreatedDBObject(table, true);
        }

        /// <summary>
        /// Helper class để lưu trữ dữ liệu khối lượng
        /// </summary>
        private class VolumeData
        {
            public string MaterialListName { get; set; } = "";
            public string MaterialName { get; set; } = "";
            public string QuantityType { get; set; } = "";
            public double Volume { get; set; } = 0;
        }
    }
}

// (C) Copyright 2026 by T27
// Lệnh tạo COGO Point đa nguồn từ Text, Circle, Point, Table, Excel/CSV

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using ClosedXML.Excel;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using ATable = Autodesk.AutoCAD.DatabaseServices.Table;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using AcadEntity = Autodesk.AutoCAD.DatabaseServices.Entity;
using AcadDBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;
using MyFirstProject.Extensions;

[assembly: CommandClass(typeof(Civil3DCsharp.CTPO_TaoCogoPoint_TongHop_Commands))]

namespace Civil3DCsharp
{
    public class CTPO_TaoCogoPoint_TongHop_Commands
    {
        [CommandMethod("CTPO_TaoCogoPoint_MultiSource")]
        [CommandMethod("CTPO_TaoCogoPoint")]
        [CommandMethod("TAOCOGOPOINT")]
        [CommandMethod("TCP")]
        public static void RunTaoCogoPoint()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            Editor ed = doc.Editor;
            Database db = doc.Database;

            try
            {
                ed.WriteMessage("\n========================================================");
                ed.WriteMessage("\n🎯 KHỞI ĐỘNG LỆNH TẠO COGO POINT ĐA NGUỒN (T27)");
                ed.WriteMessage("\n========================================================");

                // 1. Quét thông tin môi trường bản vẽ Civil 3D
                List<string> layers = new List<string>();
                List<string> surfaces = new List<string>();
                List<string> pointStyles = new List<string> { "<default>" };
                List<string> pointLabelStyles = new List<string> { "<default>" };

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    layers = GetLayerNames(db, tr);
                    surfaces = GetSurfaceNames(tr);
                    pointStyles = GetPointStyleNames(tr);
                    pointLabelStyles = GetPointLabelStyleNames(tr);
                    tr.Commit();
                }

                // 2. Khởi tạo Form UI
                using (var form = new TaoCogoPointForm(layers, surfaces, pointStyles, pointLabelStyles))
                {
                    // Callback 1: Pick Text / MText
                    form.OnPickTexts = (frm) =>
                    {
                        using (var interaction = ed.StartUserInteraction(frm))
                        {
                            PromptSelectionOptions pso = new PromptSelectionOptions
                            {
                                MessageForAdding = "\nChọn các đối tượng Text / MText trên bản vẽ: ",
                                AllowDuplicates = false
                            };
                            TypedValue[] filterList = new TypedValue[]
                            {
                                new TypedValue((int)DxfCode.Operator, "<OR"),
                                new TypedValue((int)DxfCode.Start, "TEXT"),
                                new TypedValue((int)DxfCode.Start, "MTEXT"),
                                new TypedValue((int)DxfCode.Operator, "OR>")
                            };
                            PromptSelectionResult psr = ed.GetSelection(pso, new SelectionFilter(filterList));
                            interaction.End();

                            if (psr.Status == PromptStatus.OK && psr.Value != null)
                            {
                                return psr.Value.GetObjectIds().ToList();
                            }
                        }
                        return new List<ObjectId>();
                    };

                    // Callback 2: Pick Circle
                    form.OnPickCircles = (frm) =>
                    {
                        using (var interaction = ed.StartUserInteraction(frm))
                        {
                            PromptSelectionOptions pso = new PromptSelectionOptions
                            {
                                MessageForAdding = "\nChọn các đối tượng Circle (hình tròn) trên bản vẽ: ",
                                AllowDuplicates = false
                            };
                            TypedValue[] filterList = new TypedValue[]
                            {
                                new TypedValue((int)DxfCode.Start, "CIRCLE")
                            };
                            PromptSelectionResult psr = ed.GetSelection(pso, new SelectionFilter(filterList));
                            interaction.End();

                            if (psr.Status == PromptStatus.OK && psr.Value != null)
                            {
                                return psr.Value.GetObjectIds().ToList();
                            }
                        }
                        return new List<ObjectId>();
                    };

                    // Callback 3: Pick Point
                    form.OnPickPoints = (frm) =>
                    {
                        using (var interaction = ed.StartUserInteraction(frm))
                        {
                            PromptSelectionOptions pso = new PromptSelectionOptions
                            {
                                MessageForAdding = "\nChọn các đối tượng CAD Point (DBPoint) trên bản vẽ: ",
                                AllowDuplicates = false
                            };
                            TypedValue[] filterList = new TypedValue[]
                            {
                                new TypedValue((int)DxfCode.Start, "POINT")
                            };
                            PromptSelectionResult psr = ed.GetSelection(pso, new SelectionFilter(filterList));
                            interaction.End();

                            if (psr.Status == PromptStatus.OK && psr.Value != null)
                            {
                                return psr.Value.GetObjectIds().ToList();
                            }
                        }
                        return new List<ObjectId>();
                    };

                    // Callback 4: Pick Table
                    form.OnPickTable = (frm) =>
                    {
                        using (var interaction = ed.StartUserInteraction(frm))
                        {
                            PromptEntityOptions peo = new PromptEntityOptions("\nChọn 1 đối tượng Bảng (Table) trên bản vẽ: ");
                            peo.SetRejectMessage("\nĐối tượng chọn phải là Bảng (Table)!");
                            peo.AddAllowedClass(typeof(ATable), true);

                            PromptEntityResult per = ed.GetEntity(peo);
                            interaction.End();

                            if (per.Status == PromptStatus.OK)
                            {
                                return per.ObjectId;
                            }
                        }
                        return ObjectId.Null;
                    };

                    // Hiển thị Modal Dialog
                    DialogResult result = Application.ShowModalDialog(form);

                    if (result != DialogResult.OK || !form.FormAccepted)
                    {
                        ed.WriteMessage("\n❌ Đã hủy lệnh tạo COGO Point.");
                        return;
                    }

                    // 3. Lấy cấu hình và thực thi
                    CogoPointCreationConfig config = form.GetConfig();
                    ExecutePointCreation(db, ed, form, config);
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n❌ Lỗi hệ thống: {ex.Message}");
            }
        }

        #region Core Execution Process
        private static void ExecutePointCreation(Database db, Editor ed, TaoCogoPointForm form, CogoPointCreationConfig config)
        {
            ed.WriteMessage($"\n🚀 Đang xử lý dữ liệu nguồn: {config.SourceType}...");

            List<RawPointInput> rawPoints = new List<RawPointInput>();

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    // Lấy Surface nếu có dùng
                    CivSurface? surface = null;
                    string surfaceName = config.SourceType == CogoPointSourceType.Text ? config.TextSurfaceName :
                                         (config.SourceType == CogoPointSourceType.Circle ? config.CircleSurfaceName :
                                         (config.SourceType == CogoPointSourceType.Point ? config.PointSurfaceName : ""));

                    if (!string.IsNullOrEmpty(surfaceName) && surfaceName != "<Không chọn>")
                    {
                        surface = FindSurfaceByName(surfaceName, tr);
                    }

                    // 1. Trích xuất dữ liệu theo nguồn
                    switch (config.SourceType)
                    {
                        case CogoPointSourceType.Text:
                            rawPoints = ExtractPointsFromText(db, tr, form, config, surface);
                            break;

                        case CogoPointSourceType.Circle:
                            rawPoints = ExtractPointsFromCircle(db, tr, form, config, surface);
                            break;

                        case CogoPointSourceType.Point:
                            rawPoints = ExtractPointsFromPoint(db, tr, form, config, surface);
                            break;

                        case CogoPointSourceType.Table:
                            rawPoints = ExtractPointsFromTable(tr, form.SelectedTableId, config);
                            break;

                        case CogoPointSourceType.Excel:
                            rawPoints = ExtractPointsFromExcelOrCsv(config);
                            break;
                    }

                    tr.Commit();
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage($"\n❌ Lỗi khi đọc dữ liệu nguồn: {ex.Message}");
                    return;
                }
            }

            if (rawPoints.Count == 0)
            {
                ed.WriteMessage("\n⚠️ Không tìm thấy điểm nào hợp lệ để tạo COGO Point.");
                return;
            }

            ed.WriteMessage($"\n✓ Thu thập được {rawPoints.Count} điểm dữ liệu ban đầu.");

            // 2. Lọc bỏ trùng lặp nếu bật
            if (config.IgnoreDuplicates)
            {
                int beforeCount = rawPoints.Count;
                rawPoints = FilterDuplicatePoints(rawPoints, config.DuplicateTolerance);
                int removed = beforeCount - rawPoints.Count;
                if (removed > 0)
                {
                    ed.WriteMessage($"\n✓ Đã lọc bỏ {removed} điểm trùng tọa độ (Sai số: {config.DuplicateTolerance:F4}m).");
                }
            }

            // 3. Tạo COGO Point trong Transaction
            int createdCount = 0;
            int errorCount = 0;
            List<ObjectId> newCogoPointIds = new List<ObjectId>();
            List<ObjectId> objectsToErase = new List<ObjectId>();
            List<uint> createdPointNumbers = new List<uint>();

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    CogoPointCollection cogoPointColl = A.Cdoc.CogoPoints;

                    // Lấy Style IDs nếu có (ObjectId.Null nếu chọn <default>)
                    ObjectId pointStyleId = GetPointStyleId(config.PointStyleName, tr);
                    ObjectId labelStyleId = GetLabelStyleId(config.PointLabelStyleName, tr);

                    int currentNum = config.StartNumber;

                    foreach (var pt in rawPoints)
                    {
                        try
                        {
                            Point3d p3d = new Point3d(pt.X, pt.Y, pt.Z);
                            string desc = !string.IsNullOrEmpty(pt.CustomDescription) ? pt.CustomDescription : config.DefaultDescription;
                            string pointName = !string.IsNullOrEmpty(pt.CustomName) ? pt.CustomName : $"{config.Prefix}{currentNum}{config.Suffix}";

                            ObjectId newPtId = cogoPointColl.Add(p3d, desc, false);
                            CogoPoint? cp = tr.GetObject(newPtId, OpenMode.ForWrite) as CogoPoint;
                            if (cp != null)
                            {
                                if (!string.IsNullOrEmpty(pointName))
                                {
                                    try { cp.PointName = pointName; } catch { }
                                }
                                
                                // Nếu pointStyleId == ObjectId.Null (<default>) -> gán ObjectId.Null để xóa override đưa về <default>
                                try { cp.StyleId = pointStyleId; } catch { }

                                // Nếu labelStyleId == ObjectId.Null (<default>) -> gán ObjectId.Null để xóa override đưa về <default>
                                try { cp.LabelStyleId = labelStyleId; } catch { }

                                createdPointNumbers.Add(cp.PointNumber);
                            }

                            newCogoPointIds.Add(newPtId);
                            createdCount++;
                            currentNum += config.Step;

                            if (pt.SourceObjectId.HasValue && pt.SourceObjectId.Value != ObjectId.Null)
                            {
                                objectsToErase.Add(pt.SourceObjectId.Value);
                            }

                            if (createdCount % 100 == 0)
                            {
                                ed.WriteMessage($"\n  - Đã tạo {createdCount}/{rawPoints.Count} CogoPoint...");
                            }
                        }
                        catch (System.Exception exPt)
                        {
                            errorCount++;
                            ed.WriteMessage($"\n  ⚠️ Lỗi tạo điểm ({pt.X:F2}, {pt.Y:F2}): {exPt.Message}");
                        }
                    }

                    // 4. Xóa đối tượng nguồn nếu người dùng chọn
                    bool deleteSource = (config.SourceType == CogoPointSourceType.Text && config.TextDeleteSource) ||
                                        (config.SourceType == CogoPointSourceType.Circle && config.CircleDeleteSource) ||
                                        (config.SourceType == CogoPointSourceType.Point && config.PointDeleteSource);

                    if (deleteSource && objectsToErase.Count > 0)
                    {
                        int erasedCount = 0;
                        foreach (ObjectId eraseId in objectsToErase.Distinct())
                        {
                            try
                            {
                                if (eraseId.IsValid && !eraseId.IsErased)
                                {
                                    AcadEntity? ent = tr.GetObject(eraseId, OpenMode.ForWrite) as AcadEntity;
                                    if (ent != null)
                                    {
                                        ent.Erase();
                                        erasedCount++;
                                    }
                                }
                            }
                            catch { }
                        }
                        ed.WriteMessage($"\n✓ Đã xóa {erasedCount} đối tượng nguồn theo yêu cầu.");
                    }

                    tr.Commit();
                }
                catch (System.Exception ex)
                {
                    tr.Abort();
                    ed.WriteMessage($"\n❌ Lỗi Transaction tạo CogoPoint: {ex.Message}");
                    return;
                }
            }

            // 5. Thêm vào Point Group nếu được chọn (thực hiện sau khi CogoPoint đã commit vào Database)
            if (config.AddToPointGroup && !string.IsNullOrEmpty(config.PointGroupName) && createdPointNumbers.Count > 0)
            {
                AddPointsToPointGroup(db, ed, config.PointGroupName, createdPointNumbers);
            }

            ed.WriteMessage("\n========================================================");
            ed.WriteMessage($"\n✅ HOÀN THÀNH: Đã tạo thành công {createdCount} COGO Point!");
            if (errorCount > 0) ed.WriteMessage($"\n⚠️ Số điểm lỗi: {errorCount}");
            ed.WriteMessage("\n========================================================");
        }
        #endregion

        #region Extraction Logic For Each Source

        // Source 1: Text / MText
        private static List<RawPointInput> ExtractPointsFromText(Database db, Transaction tr, TaoCogoPointForm form, CogoPointCreationConfig config, CivSurface? surface)
        {
            List<RawPointInput> list = new List<RawPointInput>();
            List<ObjectId> targetIds = new List<ObjectId>();

            if (config.TextFilterLayer && !string.IsNullOrEmpty(config.TextLayerName) && config.TextLayerName != "<Tất cả>")
            {
                targetIds = GetAllTextIdsInLayer(db, tr, config.TextLayerName);
            }
            else
            {
                targetIds = form.SelectedTextIds;
            }

            foreach (ObjectId id in targetIds)
            {
                if (id.IsNull || !id.IsValid || id.IsErased) continue;
                // Mở ForWrite để có thể chuyển đổi Justify = Left khi cần thiết
                AcadDBObject obj = tr.GetObject(id, OpenMode.ForWrite);

                Point3d pos = Point3d.Origin;
                string textContent = "";
                string layerName = "";

                if (obj is DBText dbText)
                {
                    if (config.TextForceLeftJustify)
                    {
                        Point3d alignmentPoint = dbText.AlignmentPoint;
                        if ((dbText.Justify == AttachmentPoint.BaseLeft) || 
                            (dbText.Justify == AttachmentPoint.BaseAlign) || 
                            (dbText.Justify == AttachmentPoint.BaseFit))
                        {
                            dbText.Justify = AttachmentPoint.BaseLeft;
                        }
                        else
                        {
                            dbText.Position = alignmentPoint;
                            dbText.Justify = AttachmentPoint.BaseLeft;
                        }
                    }

                    pos = dbText.Position;
                    textContent = dbText.TextString;
                    layerName = dbText.Layer;
                }
                else if (obj is MText mText)
                {
                    if (config.TextForceLeftJustify)
                    {
                        if (mText.Attachment != AttachmentPoint.BottomLeft &&
                            mText.Attachment != AttachmentPoint.MiddleLeft &&
                            mText.Attachment != AttachmentPoint.TopLeft)
                        {
                            mText.Attachment = AttachmentPoint.BottomLeft;
                        }
                    }

                    pos = mText.Location;
                    textContent = mText.Contents;
                    layerName = mText.Layer;
                }
                else continue;

                string cleanContent = CleanMTextString(textContent);

                // Determine Z
                double z = 0;
                if (config.TextZMode == 0) // Parse content
                {
                    if (!TryParseDouble(cleanContent, out z)) continue;
                }
                else if (config.TextZMode == 1) // 3D Position.Z
                {
                    z = pos.Z;
                }
                else if (config.TextZMode == 2) // Fixed Z
                {
                    z = config.TextFixedZ;
                }
                else if (config.TextZMode == 3 && surface != null) // Surface
                {
                    double? sz = GetSurfaceElevation(surface, pos.X, pos.Y);
                    if (!sz.HasValue) continue;
                    z = sz.Value;
                }

                // Determine Name & Description
                string? customName = (config.TextNameMode == 1) ? cleanContent : null;
                string? customDesc = (config.TextDescMode == 1) ? layerName : null;

                list.Add(new RawPointInput
                {
                    X = pos.X,
                    Y = pos.Y,
                    Z = z,
                    CustomName = customName,
                    CustomDescription = customDesc,
                    SourceObjectId = config.TextDeleteSource ? id : null
                });
            }

            return list;
        }

        // Source 2: Circle
        private static List<RawPointInput> ExtractPointsFromCircle(Database db, Transaction tr, TaoCogoPointForm form, CogoPointCreationConfig config, CivSurface? surface)
        {
            List<RawPointInput> list = new List<RawPointInput>();
            List<ObjectId> targetIds = new List<ObjectId>();

            if (config.CircleFilterLayer && !string.IsNullOrEmpty(config.CircleLayerName) && config.CircleLayerName != "<Tất cả>")
            {
                targetIds = GetAllCircleIdsInLayer(db, tr, config.CircleLayerName);
            }
            else
            {
                targetIds = form.SelectedCircleIds;
            }

            // If nearby text is needed, collect all drawing texts for spatial lookup
            List<TextSpatialInfo>? spatialTexts = null;
            if (config.CircleZMode == 1 || config.CircleNameMode == 1)
            {
                spatialTexts = CollectAllTextSpatialInfo(db, tr);
            }

            foreach (ObjectId id in targetIds)
            {
                if (id.IsNull || !id.IsValid || id.IsErased) continue;
                Circle? circle = tr.GetObject(id, OpenMode.ForRead) as Circle;
                if (circle == null) continue;

                Point3d center = circle.Center;
                Point2d center2d = new Point2d(center.X, center.Y);

                // Determine Z
                double z = 0;
                if (config.CircleZMode == 0) // Center.Z
                {
                    z = center.Z;
                }
                else if (config.CircleZMode == 1 && spatialTexts != null) // Nearby Text
                {
                    var nearestZ = FindNearestNumericText(center2d, config.CircleRadiusZ, spatialTexts);
                    if (nearestZ.HasValue) z = nearestZ.Value.NumericValue;
                    else z = center.Z; // Fallback
                }
                else if (config.CircleZMode == 2 && surface != null) // Surface
                {
                    double? sz = GetSurfaceElevation(surface, center.X, center.Y);
                    if (!sz.HasValue) continue;
                    z = sz.Value;
                }
                else if (config.CircleZMode == 3) // Fixed Z
                {
                    z = config.CircleFixedZ;
                }

                // Determine Name
                string? customName = null;
                if (config.CircleNameMode == 1 && spatialTexts != null)
                {
                    var nearestName = FindNearestNameText(center2d, config.CircleRadiusName, spatialTexts);
                    if (nearestName != null) customName = nearestName.Text;
                }

                list.Add(new RawPointInput
                {
                    X = center.X,
                    Y = center.Y,
                    Z = z,
                    CustomName = customName,
                    CustomDescription = circle.Layer,
                    SourceObjectId = config.CircleDeleteSource ? id : null
                });
            }

            return list;
        }

        // Source 3: CAD Point (DBPoint)
        private static List<RawPointInput> ExtractPointsFromPoint(Database db, Transaction tr, TaoCogoPointForm form, CogoPointCreationConfig config, CivSurface? surface)
        {
            List<RawPointInput> list = new List<RawPointInput>();
            List<ObjectId> targetIds = new List<ObjectId>();

            if (config.PointFilterLayer && !string.IsNullOrEmpty(config.PointLayerName) && config.PointLayerName != "<Tất cả>")
            {
                targetIds = GetAllPointIdsInLayer(db, tr, config.PointLayerName);
            }
            else
            {
                targetIds = form.SelectedPointIds;
            }

            // Spatial lookup if needed
            List<TextSpatialInfo>? spatialTexts = null;
            if (config.PointZMode == 1 || config.PointNameMode == 1)
            {
                spatialTexts = CollectAllTextSpatialInfo(db, tr);
            }

            foreach (ObjectId id in targetIds)
            {
                if (id.IsNull || !id.IsValid || id.IsErased) continue;
                DBPoint? pt = tr.GetObject(id, OpenMode.ForRead) as DBPoint;
                if (pt == null) continue;

                Point3d pos = pt.Position;
                Point2d pos2d = new Point2d(pos.X, pos.Y);

                // Determine Z
                double z = 0;
                if (config.PointZMode == 0) // Point.Z
                {
                    z = pos.Z;
                }
                else if (config.PointZMode == 1 && spatialTexts != null) // Nearby Text
                {
                    var nearestZ = FindNearestNumericText(pos2d, config.PointRadiusZ, spatialTexts);
                    if (nearestZ.HasValue) z = nearestZ.Value.NumericValue;
                    else z = pos.Z;
                }
                else if (config.PointZMode == 2 && surface != null) // Surface
                {
                    double? sz = GetSurfaceElevation(surface, pos.X, pos.Y);
                    if (!sz.HasValue) continue;
                    z = sz.Value;
                }
                else if (config.PointZMode == 3) // Fixed Z
                {
                    z = config.PointFixedZ;
                }

                // Determine Name
                string? customName = null;
                if (config.PointNameMode == 1 && spatialTexts != null)
                {
                    var nearestName = FindNearestNameText(pos2d, config.PointRadiusName, spatialTexts);
                    if (nearestName != null) customName = nearestName.Text;
                }

                list.Add(new RawPointInput
                {
                    X = pos.X,
                    Y = pos.Y,
                    Z = z,
                    CustomName = customName,
                    CustomDescription = pt.Layer,
                    SourceObjectId = config.PointDeleteSource ? id : null
                });
            }

            return list;
        }

        // Source 4: Table
        private static List<RawPointInput> ExtractPointsFromTable(Transaction tr, ObjectId tableId, CogoPointCreationConfig config)
        {
            List<RawPointInput> list = new List<RawPointInput>();
            if (tableId.IsNull || !tableId.IsValid) return list;

            var table = tr.GetObject(tableId, OpenMode.ForRead) as ATable;
            if (table == null) return list;

            int startRow = Math.Max(0, config.TableStartRow - 1);

            for (int r = startRow; r < table.Rows.Count; r++)
            {
                try
                {
                    string strX = CleanMTextString(table.Cells[r, config.TableColXIndex]?.TextString ?? "");
                    string strY = CleanMTextString(table.Cells[r, config.TableColYIndex]?.TextString ?? "");

                    if (!TryParseDouble(strX, out double x) || !TryParseDouble(strY, out double y))
                        continue;

                    double z = 0;
                    if (config.TableColZIndex >= 0 && config.TableColZIndex < table.Columns.Count)
                    {
                        string strZ = CleanMTextString(table.Cells[r, config.TableColZIndex]?.TextString ?? "");
                        TryParseDouble(strZ, out z);
                    }

                    string? name = null;
                    if (config.TableColNameIndex >= 0 && config.TableColNameIndex < table.Columns.Count)
                    {
                        name = CleanMTextString(table.Cells[r, config.TableColNameIndex]?.TextString ?? "");
                    }

                    string? desc = null;
                    if (config.TableColDescIndex >= 0 && config.TableColDescIndex < table.Columns.Count)
                    {
                        desc = CleanMTextString(table.Cells[r, config.TableColDescIndex]?.TextString ?? "");
                    }

                    list.Add(new RawPointInput
                    {
                        X = x,
                        Y = y,
                        Z = z,
                        CustomName = string.IsNullOrWhiteSpace(name) ? null : name,
                        CustomDescription = string.IsNullOrWhiteSpace(desc) ? null : desc
                    });
                }
                catch { }
            }

            return list;
        }

        // Source 5: Excel / CSV
        private static List<RawPointInput> ExtractPointsFromExcelOrCsv(CogoPointCreationConfig config)
        {
            List<RawPointInput> list = new List<RawPointInput>();
            string filePath = config.ExcelFilePath;
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return list;

            string ext = Path.GetExtension(filePath).ToLowerInvariant();

            if (ext == ".csv")
            {
                var lines = File.ReadAllLines(filePath, Encoding.UTF8);
                if (lines.Length == 0) return list;

                char separator = lines[0].Contains(';') ? ';' : (lines[0].Contains('\t') ? '\t' : ',');
                int startRow = Math.Max(0, config.ExcelStartRow - 1);

                for (int r = startRow; r < lines.Length; r++)
                {
                    string line = lines[r].Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    var parts = line.Split(separator);
                    if (parts.Length <= Math.Max(config.ExcelColXIndex, config.ExcelColYIndex)) continue;

                    if (!TryParseDouble(parts[config.ExcelColXIndex], out double x) ||
                        !TryParseDouble(parts[config.ExcelColYIndex], out double y))
                        continue;

                    double z = 0;
                    if (config.ExcelColZIndex >= 0 && config.ExcelColZIndex < parts.Length)
                    {
                        TryParseDouble(parts[config.ExcelColZIndex], out z);
                    }

                    string? name = (config.ExcelColNameIndex >= 0 && config.ExcelColNameIndex < parts.Length) ? parts[config.ExcelColNameIndex].Trim() : null;
                    string? desc = (config.ExcelColDescIndex >= 0 && config.ExcelColDescIndex < parts.Length) ? parts[config.ExcelColDescIndex].Trim() : null;

                    list.Add(new RawPointInput
                    {
                        X = x,
                        Y = y,
                        Z = z,
                        CustomName = string.IsNullOrWhiteSpace(name) ? null : name,
                        CustomDescription = string.IsNullOrWhiteSpace(desc) ? null : desc
                    });
                }
            }
            else if (ext == ".xlsx")
            {
                using (var workbook = new XLWorkbook(filePath))
                {
                    var worksheet = string.IsNullOrEmpty(config.ExcelSheetName) ? workbook.Worksheet(1) : workbook.Worksheet(config.ExcelSheetName);
                    int startRow = Math.Max(1, config.ExcelStartRow);
                    int lastRow = worksheet.LastRowUsed()?.RowNumber() ?? startRow;

                    for (int r = startRow; r <= lastRow; r++)
                    {
                        var row = worksheet.Row(r);
                        if (row.IsEmpty()) continue;

                        int colX = config.ExcelColXIndex + 1;
                        int colY = config.ExcelColYIndex + 1;

                        if (!TryGetExcelDouble(row.Cell(colX), out double x) ||
                            !TryGetExcelDouble(row.Cell(colY), out double y))
                            continue;

                        double z = 0;
                        if (config.ExcelColZIndex >= 0)
                        {
                            TryGetExcelDouble(row.Cell(config.ExcelColZIndex + 1), out z);
                        }

                        string? name = (config.ExcelColNameIndex >= 0) ? row.Cell(config.ExcelColNameIndex + 1).GetString().Trim() : null;
                        string? desc = (config.ExcelColDescIndex >= 0) ? row.Cell(config.ExcelColDescIndex + 1).GetString().Trim() : null;

                        list.Add(new RawPointInput
                        {
                            X = x,
                            Y = y,
                            Z = z,
                            CustomName = string.IsNullOrWhiteSpace(name) ? null : name,
                            CustomDescription = string.IsNullOrWhiteSpace(desc) ? null : desc
                        });
                    }
                }
            }

            return list;
        }
        #endregion

        #region Spatial Query Helpers (Finding Nearest Texts)
        private class TextSpatialInfo
        {
            public ObjectId Id { get; set; }
            public Point2d Location { get; set; }
            public string Text { get; set; } = "";
            public double? NumericValue { get; set; }
        }

        private static List<TextSpatialInfo> CollectAllTextSpatialInfo(Database db, Transaction tr)
        {
            List<TextSpatialInfo> list = new List<TextSpatialInfo>();
            try
            {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                foreach (ObjectId id in btr)
                {
                    if (id.IsNull || id.IsErased) continue;
                    AcadDBObject obj = tr.GetObject(id, OpenMode.ForRead);

                    if (obj is DBText dt)
                    {
                        string clean = CleanMTextString(dt.TextString);
                        double? num = TryParseDouble(clean, out double v) ? v : (double?)null;
                        Point3d textPos = dt.Position;
                        if (dt.Justify != AttachmentPoint.BaseLeft && 
                            dt.Justify != AttachmentPoint.BaseAlign && 
                            dt.Justify != AttachmentPoint.BaseFit)
                        {
                            textPos = dt.AlignmentPoint;
                        }

                        list.Add(new TextSpatialInfo
                        {
                            Id = id,
                            Location = new Point2d(textPos.X, textPos.Y),
                            Text = clean,
                            NumericValue = num
                        });
                    }
                    else if (obj is MText mt)
                    {
                        string clean = CleanMTextString(mt.Contents);
                        double? num = TryParseDouble(clean, out double v) ? v : (double?)null;
                        list.Add(new TextSpatialInfo
                        {
                            Id = id,
                            Location = new Point2d(mt.Location.X, mt.Location.Y),
                            Text = clean,
                            NumericValue = num
                        });
                    }
                }
            }
            catch { }
            return list;
        }

        private static (double NumericValue, TextSpatialInfo TextInfo)? FindNearestNumericText(Point2d center, double radius, List<TextSpatialInfo> texts)
        {
            double minDistance = double.MaxValue;
            TextSpatialInfo? bestMatch = null;

            foreach (var t in texts)
            {
                if (!t.NumericValue.HasValue) continue;
                double dist = center.GetDistanceTo(t.Location);
                if (dist <= radius && dist < minDistance)
                {
                    minDistance = dist;
                    bestMatch = t;
                }
            }

            if (bestMatch != null && bestMatch.NumericValue.HasValue)
            {
                return (bestMatch.NumericValue.Value, bestMatch);
            }
            return null;
        }

        private static TextSpatialInfo? FindNearestNameText(Point2d center, double radius, List<TextSpatialInfo> texts)
        {
            double minDistance = double.MaxValue;
            TextSpatialInfo? bestMatch = null;

            foreach (var t in texts)
            {
                if (string.IsNullOrWhiteSpace(t.Text)) continue;
                double dist = center.GetDistanceTo(t.Location);
                if (dist <= radius && dist < minDistance)
                {
                    minDistance = dist;
                    bestMatch = t;
                }
            }

            return bestMatch;
        }
        #endregion

        #region General Helpers
        private static List<RawPointInput> FilterDuplicatePoints(List<RawPointInput> sourcePoints, double tolerance)
        {
            List<RawPointInput> filtered = new List<RawPointInput>();

            foreach (var pt in sourcePoints)
            {
                bool isDuplicate = false;
                foreach (var existing in filtered)
                {
                    double dx = pt.X - existing.X;
                    double dy = pt.Y - existing.Y;
                    if (Math.Sqrt(dx * dx + dy * dy) <= tolerance)
                    {
                        isDuplicate = true;
                        break;
                    }
                }

                if (!isDuplicate)
                {
                    filtered.Add(pt);
                }
            }

            return filtered;
        }

        private static bool TryParseDouble(string? text, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;

            text = text.Trim().Replace(',', '.');
            // Bỏ các tiền tố như "H=", "Z=", "+", v.v.
            text = Regex.Replace(text, @"^[A-Za-z=+\s]+", "");

            return double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryGetExcelDouble(IXLCell cell, out double value)
        {
            value = 0;
            if (cell == null || cell.IsEmpty()) return false;

            if (cell.DataType == XLDataType.Number)
            {
                value = cell.GetDouble();
                return true;
            }

            string s = cell.GetString()?.Trim() ?? "";
            return TryParseDouble(s, out value);
        }

        private static double? GetSurfaceElevation(CivSurface? surface, double x, double y)
        {
            if (surface == null) return null;
            try
            {
                double elev = surface.FindElevationAtXY(x, y);
                if (double.IsNaN(elev) || double.IsInfinity(elev)) return null;
                return elev;
            }
            catch
            {
                return null;
            }
        }

        private static string CleanMTextString(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return Regex.Replace(text, @"\\[A-Za-z0-9]+|\\[PX].*?;|[{}]", "").Trim();
        }

        private static List<string> GetLayerNames(Database db, Transaction tr)
        {
            List<string> list = new List<string>();
            try
            {
                LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                foreach (ObjectId id in lt)
                {
                    LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                    list.Add(ltr.Name);
                }
                list.Sort();
            }
            catch { }
            return list;
        }

        private static List<string> GetSurfaceNames(Transaction tr)
        {
            List<string> list = new List<string> { "<Không chọn>" };
            try
            {
                foreach (ObjectId surfId in A.Cdoc.GetSurfaceIds())
                {
                    CivSurface? surf = tr.GetObject(surfId, OpenMode.ForRead) as CivSurface;
                    if (surf != null) list.Add(surf.Name);
                }
            }
            catch { }
            return list;
        }

        private static CivSurface? FindSurfaceByName(string name, Transaction tr)
        {
            try
            {
                foreach (ObjectId surfId in A.Cdoc.GetSurfaceIds())
                {
                    CivSurface? surf = tr.GetObject(surfId, OpenMode.ForRead) as CivSurface;
                    if (surf != null && surf.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    {
                        return surf;
                    }
                }
            }
            catch { }
            return null;
        }

        private static bool IsDefaultStyleName(string? styleName)
        {
            if (string.IsNullOrWhiteSpace(styleName)) return true;
            string s = styleName.Trim();
            return s.Equals("<default>", StringComparison.OrdinalIgnoreCase) ||
                   s.Equals("<Mặc định>", StringComparison.OrdinalIgnoreCase) ||
                   s.Equals("default", StringComparison.OrdinalIgnoreCase) ||
                   s.Equals("<None>", StringComparison.OrdinalIgnoreCase) ||
                   s.Equals("None", StringComparison.OrdinalIgnoreCase) ||
                   s.StartsWith("<default", StringComparison.OrdinalIgnoreCase) ||
                   s.StartsWith("<mặc định", StringComparison.OrdinalIgnoreCase);
        }

        private static List<string> GetPointStyleNames(Transaction tr)
        {
            List<string> list = new List<string> { "<default>" };
            try
            {
                foreach (ObjectId id in A.Cdoc.Styles.PointStyles)
                {
                    PointStyle? ps = tr.GetObject(id, OpenMode.ForRead) as PointStyle;
                    if (ps != null && !list.Contains(ps.Name)) list.Add(ps.Name);
                }
            }
            catch { }
            return list;
        }

        private static List<string> GetPointLabelStyleNames(Transaction tr)
        {
            List<string> list = new List<string> { "<default>" };
            try
            {
                foreach (ObjectId id in A.Cdoc.Styles.LabelStyles.PointLabelStyles.LabelStyles)
                {
                    LabelStyle? ls = tr.GetObject(id, OpenMode.ForRead) as LabelStyle;
                    if (ls != null && !list.Contains(ls.Name)) list.Add(ls.Name);
                }
            }
            catch { }
            return list;
        }

        private static ObjectId GetPointStyleId(string styleName, Transaction tr)
        {
            if (IsDefaultStyleName(styleName)) return ObjectId.Null;
            try
            {
                foreach (ObjectId id in A.Cdoc.Styles.PointStyles)
                {
                    PointStyle? ps = tr.GetObject(id, OpenMode.ForRead) as PointStyle;
                    if (ps != null && ps.Name.Equals(styleName, StringComparison.OrdinalIgnoreCase))
                    {
                        return id;
                    }
                }
            }
            catch { }
            return ObjectId.Null;
        }

        private static ObjectId GetLabelStyleId(string styleName, Transaction tr)
        {
            if (IsDefaultStyleName(styleName)) return ObjectId.Null;
            try
            {
                foreach (ObjectId id in A.Cdoc.Styles.LabelStyles.PointLabelStyles.LabelStyles)
                {
                    LabelStyle? ls = tr.GetObject(id, OpenMode.ForRead) as LabelStyle;
                    if (ls != null && ls.Name.Equals(styleName, StringComparison.OrdinalIgnoreCase))
                    {
                        return id;
                    }
                }
            }
            catch { }
            return ObjectId.Null;
        }

        private static void AddPointsToPointGroup(Database db, Editor ed, string groupName, List<uint> pointNumbers)
        {
            if (pointNumbers == null || pointNumbers.Count == 0 || string.IsNullOrWhiteSpace(groupName)) return;

            try
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    PointGroupCollection pointGroups = A.Cdoc.PointGroups;
                    ObjectId pgId = ObjectId.Null;

                    if (!pointGroups.Contains(groupName))
                    {
                        pgId = pointGroups.Add(groupName);
                    }
                    else
                    {
                        foreach (ObjectId id in pointGroups)
                        {
                            PointGroup? existingPg = tr.GetObject(id, OpenMode.ForRead) as PointGroup;
                            if (existingPg != null && existingPg.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase))
                            {
                                pgId = id;
                                break;
                            }
                        }
                    }

                    if (pgId == ObjectId.Null || !pgId.IsValid)
                    {
                        ed.WriteMessage($"\n⚠️ Không tìm thấy hoặc không thể tạo Point Group '{groupName}'.");
                        return;
                    }

                    PointGroup? pg = tr.GetObject(pgId, OpenMode.ForWrite) as PointGroup;
                    if (pg == null) return;

                    // Lấy Query hiện tại hoặc tạo mới
                    StandardPointGroupQuery stdQuery = pg.GetQuery() as StandardPointGroupQuery ?? new StandardPointGroupQuery();

                    // Gộp danh sách số điểm mới vào IncludeNumbers
                    string mergedNumbers = MergePointNumberRanges(stdQuery.IncludeNumbers, pointNumbers);
                    stdQuery.IncludeNumbers = mergedNumbers;

                    pg.SetQuery(stdQuery);
                    pg.Update();

                    tr.Commit();
                }

                try
                {
                    A.Cdoc.PointGroups.UpdateAllPointGroups();
                }
                catch { }

                string newRangeSummary = FormatPointNumbersToRangeString(pointNumbers);
                ed.WriteMessage($"\n✓ Đã thêm {pointNumbers.Count} điểm vào Point Group '{groupName}' (Dải số: {newRangeSummary}).");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n⚠️ Lỗi khi thêm điểm vào Point Group '{groupName}': {ex.Message}");
            }
        }

        private static string FormatPointNumbersToRangeString(IEnumerable<uint> numbers)
        {
            var sorted = numbers.Distinct().OrderBy(n => n).ToList();
            if (sorted.Count == 0) return "";

            List<string> ranges = new List<string>();
            uint rangeStart = sorted[0];
            uint rangeEnd = sorted[0];

            for (int i = 1; i < sorted.Count; i++)
            {
                if (sorted[i] == rangeEnd + 1)
                {
                    rangeEnd = sorted[i];
                }
                else
                {
                    ranges.Add(rangeStart == rangeEnd ? $"{rangeStart}" : $"{rangeStart}-{rangeEnd}");
                    rangeStart = sorted[i];
                    rangeEnd = sorted[i];
                }
            }
            ranges.Add(rangeStart == rangeEnd ? $"{rangeStart}" : $"{rangeStart}-{rangeEnd}");

            return string.Join(",", ranges);
        }

        private static string MergePointNumberRanges(string? existingRangeStr, IEnumerable<uint> newNumbers)
        {
            HashSet<uint> allNumbers = new HashSet<uint>();

            if (!string.IsNullOrWhiteSpace(existingRangeStr))
            {
                string[] parts = existingRangeStr.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string part in parts)
                {
                    string p = part.Trim();
                    if (p.Contains('-'))
                    {
                        string[] bounds = p.Split('-');
                        if (bounds.Length == 2 &&
                            uint.TryParse(bounds[0].Trim(), out uint start) &&
                            uint.TryParse(bounds[1].Trim(), out uint end))
                        {
                            uint min = Math.Min(start, end);
                            uint max = Math.Max(start, end);
                            for (uint n = min; n <= max; n++)
                            {
                                allNumbers.Add(n);
                            }
                        }
                    }
                    else if (uint.TryParse(p, out uint singleNum))
                    {
                        allNumbers.Add(singleNum);
                    }
                }
            }

            foreach (uint num in newNumbers)
            {
                allNumbers.Add(num);
            }

            return FormatPointNumbersToRangeString(allNumbers);
        }

        private static List<ObjectId> GetAllTextIdsInLayer(Database db, Transaction tr, string layerName)
        {
            List<ObjectId> list = new List<ObjectId>();
            try
            {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                foreach (ObjectId id in btr)
                {
                    if (id.IsNull || id.IsErased) continue;
                    AcadDBObject dbo = tr.GetObject(id, OpenMode.ForRead);
                    if (dbo is DBText dt && dt.Layer.Equals(layerName, StringComparison.OrdinalIgnoreCase))
                    {
                        list.Add(id);
                    }
                    else if (dbo is MText mt && mt.Layer.Equals(layerName, StringComparison.OrdinalIgnoreCase))
                    {
                        list.Add(id);
                    }
                }
            }
            catch { }
            return list;
        }

        private static List<ObjectId> GetAllCircleIdsInLayer(Database db, Transaction tr, string layerName)
        {
            List<ObjectId> list = new List<ObjectId>();
            try
            {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                foreach (ObjectId id in btr)
                {
                    if (id.IsNull || id.IsErased) continue;
                    Circle? circle = tr.GetObject(id, OpenMode.ForRead) as Circle;
                    if (circle != null && circle.Layer.Equals(layerName, StringComparison.OrdinalIgnoreCase))
                    {
                        list.Add(id);
                    }
                }
            }
            catch { }
            return list;
        }

        private static List<ObjectId> GetAllPointIdsInLayer(Database db, Transaction tr, string layerName)
        {
            List<ObjectId> list = new List<ObjectId>();
            try
            {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                foreach (ObjectId id in btr)
                {
                    if (id.IsNull || id.IsErased) continue;
                    DBPoint? pt = tr.GetObject(id, OpenMode.ForRead) as DBPoint;
                    if (pt != null && pt.Layer.Equals(layerName, StringComparison.OrdinalIgnoreCase))
                    {
                        list.Add(id);
                    }
                }
            }
            catch { }
            return list;
        }
        #endregion
    }
}

// (C) Copyright 2026 by T27
// Lệnh căn lề cho Text/MText
// - Căn lề theo phương X (Left/Center/Right)
// - Căn lề theo phương Y (Top/Middle/Bottom)
// - Thiết lập Justify cho bản thân mỗi text
//
using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(Civil3DCsharp.AT_CanLe_ChoText_Commands))]

namespace Civil3DCsharp
{
    public class AT_CanLe_ChoText_Commands
    {
        /// <summary>
        /// Thông tin vị trí của 1 text object
        /// </summary>
        private class TextInfo
        {
            public ObjectId Id { get; set; }
            public bool IsDBText { get; set; }
            public Point3d Position { get; set; }  // Vị trí hiện tại (insertion point hoặc alignment point)
            public Extents3d Extents { get; set; }  // Bounding box
        }

        [CommandMethod("AT_Can_Le_Cho_Text")]
        public static void AT_CanLe_ChoText()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            var db = doc.Database;

            try
            {
                // 1. Hiển thị form
                using var form = new MyFirstProject.Acad_Tool.CanLeTextForm();
                var dialogResult = Application.ShowModalDialog(form);

                if (dialogResult != System.Windows.Forms.DialogResult.OK || !form.FormAccepted)
                {
                    ed.WriteMessage("\n Đã hủy lệnh.");
                    return;
                }

                int alignX = form.AlignX;      // 0=Không, 1=Left, 2=Center, 3=Right
                int alignY = form.AlignY;      // 0=Không, 1=Top, 2=Middle, 3=Bottom
                int justifyIndex = form.JustifyIndex;  // index trong JustifyOptions
                bool pickPoint = form.PickPoint;

                // 2. Vòng lặp: chọn đối tượng → căn lề → lặp lại (Esc để kết thúc)
                var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "TEXT,MTEXT") });

                while (true)
                {
                    // 2a. Chọn các text
                    var selOpts = new PromptSelectionOptions
                    {
                        MessageForAdding = "\nChọn các Text/MText cần căn lề (Esc để kết thúc): "
                    };
                    var selResult = ed.GetSelection(selOpts, filter);

                    if (selResult.Status != PromptStatus.OK)
                    {
                        ed.WriteMessage("\n Kết thúc lệnh căn lề.");
                        break;
                    }

                    // 2b. Nếu chọn pick point, yêu cầu user chọn điểm căn lề
                    Point3d? pickPt = null;
                    if (pickPoint && (alignX > 0 || alignY > 0))
                    {
                        var ptResult = ed.GetPoint("\nChọn điểm căn lề: ");
                        if (ptResult.Status != PromptStatus.OK)
                        {
                            ed.WriteMessage("\n Kết thúc lệnh căn lề.");
                            break;
                        }
                        pickPt = ptResult.Value;
                    }

                    int totalCount = selResult.Value.Count;
                    int successCount = 0;

                    // 3. Xử lý trong transaction
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        // Thu thập thông tin text
                        var textInfos = new List<TextInfo>();

                        foreach (SelectedObject selObj in selResult.Value)
                        {
                            if (selObj == null) continue;

                            var ent = tr.GetObject(selObj.ObjectId, OpenMode.ForWrite);
                            if (ent == null) continue;

                            try
                            {
                                var info = new TextInfo { Id = selObj.ObjectId };

                                if (ent is DBText dbText)
                                {
                                    info.IsDBText = true;
                                    info.Position = GetDBTextPosition(dbText);
                                    info.Extents = dbText.GeometricExtents;
                                }
                                else if (ent is MText mText)
                                {
                                    info.IsDBText = false;
                                    info.Position = mText.Location;
                                    info.Extents = mText.GeometricExtents;
                                }
                                else continue;

                                textInfos.Add(info);
                            }
                            catch
                            {
                                // Bỏ qua text không lấy được extents
                            }
                        }

                        if (textInfos.Count == 0)
                        {
                            ed.WriteMessage("\n Không tìm thấy text hợp lệ.");
                            tr.Commit();
                            continue;  // Quay lại chọn đối tượng
                        }

                        bool doAlignX = alignX > 0;
                        bool doAlignY = alignY > 0;

                        // ===== BƯỚC 1: Thiết lập justify cho tất cả text TRƯỚC =====
                        if (justifyIndex > 0)
                        {
                            foreach (var info in textInfos)
                            {
                                try
                                {
                                    var ent = tr.GetObject(info.Id, OpenMode.ForWrite);
                                    if (ent is DBText dbText)
                                        SetDBTextJustify(dbText, justifyIndex);
                                    else if (ent is MText mText)
                                        SetMTextAttachment(mText, justifyIndex);
                                }
                                catch { }
                            }
                        }

                        // ===== BƯỚC 2: Cập nhật lại extents sau khi đã đổi justify =====
                        foreach (var info in textInfos)
                        {
                            try
                            {
                                var ent = tr.GetObject(info.Id, OpenMode.ForWrite);
                                if (ent is DBText dbText)
                                {
                                    info.Position = GetDBTextPosition(dbText);
                                    info.Extents = dbText.GeometricExtents;
                                }
                                else if (ent is MText mText)
                                {
                                    info.Position = mText.Location;
                                    info.Extents = mText.GeometricExtents;
                                }
                            }
                            catch { }
                        }

                        // ===== BƯỚC 3: Tính toán vị trí căn lề mục tiêu =====
                        double targetX = 0, targetY = 0;

                        if (pickPt.HasValue)
                        {
                            targetX = pickPt.Value.X;
                            targetY = pickPt.Value.Y;
                        }
                        else
                        {
                            if (doAlignX)
                            {
                                switch (alignX)
                                {
                                    case 1: targetX = textInfos.Min(t => t.Extents.MinPoint.X); break;
                                    case 2: targetX = textInfos.Average(t => (t.Extents.MinPoint.X + t.Extents.MaxPoint.X) / 2.0); break;
                                    case 3: targetX = textInfos.Max(t => t.Extents.MaxPoint.X); break;
                                }
                            }

                            if (doAlignY)
                            {
                                switch (alignY)
                                {
                                    case 1: targetY = textInfos.Max(t => t.Extents.MaxPoint.Y); break;
                                    case 2: targetY = textInfos.Average(t => (t.Extents.MinPoint.Y + t.Extents.MaxPoint.Y) / 2.0); break;
                                    case 3: targetY = textInfos.Min(t => t.Extents.MinPoint.Y); break;
                                }
                            }
                        }

                        // ===== BƯỚC 4: Di chuyển căn lề cho từng text =====
                        foreach (var info in textInfos)
                        {
                            try
                            {
                                var ent = tr.GetObject(info.Id, OpenMode.ForWrite);

                                if (ent is DBText dbText)
                                {
                                    if (doAlignX || doAlignY)
                                        MoveDBText(dbText, info, targetX, targetY, doAlignX, doAlignY, alignX, alignY);
                                }
                                else if (ent is MText mText)
                                {
                                    if (doAlignX || doAlignY)
                                        MoveMText(mText, info, targetX, targetY, doAlignX, doAlignY, alignX, alignY);
                                }

                                successCount++;
                            }
                            catch
                            {
                                // Bỏ qua text lỗi
                            }
                        }

                        tr.Commit();
                    }

                    ed.WriteMessage($"\nHoàn thành: {successCount}/{totalCount} text đã được xử lý.");
                } // end while
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\nLỗi: {ex.Message}");
            }
        }

        /// <summary>
        /// Lấy vị trí thực tế của DBText (xử lý Position vs AlignmentPoint)
        /// </summary>
        private static Point3d GetDBTextPosition(DBText dbText)
        {
            if (dbText.Justify == AttachmentPoint.BaseLeft ||
                dbText.Justify == AttachmentPoint.BaseAlign ||
                dbText.Justify == AttachmentPoint.BaseFit)
            {
                return dbText.Position;
            }
            return dbText.AlignmentPoint;
        }

        /// <summary>
        /// Thiết lập Justify cho DBText mà không thay đổi vị trí hiển thị
        /// </summary>
        private static void SetDBTextJustify(DBText dbText, int justifyIndex)
        {
            // Lưu vị trí cũ (alignment point thực tế)
            Point3d oldPos = GetDBTextPosition(dbText);

            // Map index -> AttachmentPoint
            AttachmentPoint newJustify = MapIndexToAttachmentPoint(justifyIndex);

            // Set justify
            dbText.Justify = newJustify;

            // Cập nhật vị trí để text không bị di chuyển
            if (newJustify == AttachmentPoint.BaseLeft)
            {
                dbText.Position = oldPos;
            }
            else
            {
                dbText.AlignmentPoint = oldPos;
            }
        }

        /// <summary>
        /// Thiết lập Attachment cho MText
        /// </summary>
        private static void SetMTextAttachment(MText mText, int justifyIndex)
        {
            AttachmentPoint newAttach = MapIndexToAttachmentPoint(justifyIndex);
            mText.Attachment = newAttach;
        }

        /// <summary>
        /// Di chuyển DBText theo căn lề
        /// </summary>
        private static void MoveDBText(DBText dbText, TextInfo info, double targetX, double targetY,
            bool doAlignX, bool doAlignY, int alignX, int alignY)
        {
            // Tính khoảng cách cần di chuyển dựa trên extents
            double dx = 0, dy = 0;

            if (doAlignX)
            {
                double currentRefX = GetReferenceX(info.Extents, alignX);
                dx = targetX - currentRefX;
            }

            if (doAlignY)
            {
                double currentRefY = GetReferenceY(info.Extents, alignY);
                dy = targetY - currentRefY;
            }

            if (Math.Abs(dx) > 1e-6 || Math.Abs(dy) > 1e-6)
            {
                Vector3d displacement = new Vector3d(dx, dy, 0);
                dbText.TransformBy(Matrix3d.Displacement(displacement));
            }
        }

        /// <summary>
        /// Di chuyển MText theo căn lề
        /// </summary>
        private static void MoveMText(MText mText, TextInfo info, double targetX, double targetY,
            bool doAlignX, bool doAlignY, int alignX, int alignY)
        {
            double dx = 0, dy = 0;

            if (doAlignX)
            {
                double currentRefX = GetReferenceX(info.Extents, alignX);
                dx = targetX - currentRefX;
            }

            if (doAlignY)
            {
                double currentRefY = GetReferenceY(info.Extents, alignY);
                dy = targetY - currentRefY;
            }

            if (Math.Abs(dx) > 1e-6 || Math.Abs(dy) > 1e-6)
            {
                Vector3d displacement = new Vector3d(dx, dy, 0);
                mText.TransformBy(Matrix3d.Displacement(displacement));
            }
        }

        /// <summary>
        /// Lấy tọa độ X tham chiếu để căn lề dựa trên extents
        /// </summary>
        private static double GetReferenceX(Extents3d ext, int alignX)
        {
            return alignX switch
            {
                1 => ext.MinPoint.X,  // Left
                2 => (ext.MinPoint.X + ext.MaxPoint.X) / 2.0,  // Center
                3 => ext.MaxPoint.X,  // Right
                _ => ext.MinPoint.X
            };
        }

        /// <summary>
        /// Lấy tọa độ Y tham chiếu để căn lề dựa trên extents
        /// </summary>
        private static double GetReferenceY(Extents3d ext, int alignY)
        {
            return alignY switch
            {
                1 => ext.MaxPoint.Y,  // Top
                2 => (ext.MinPoint.Y + ext.MaxPoint.Y) / 2.0,  // Middle
                3 => ext.MinPoint.Y,  // Bottom
                _ => ext.MinPoint.Y
            };
        }

        /// <summary>
        /// Map combobox index to AttachmentPoint
        /// Index: 0=Không đổi, 1=TopLeft, 2=TopCenter, ... 12=BaseRight
        /// </summary>
        private static AttachmentPoint MapIndexToAttachmentPoint(int index)
        {
            return index switch
            {
                1 => AttachmentPoint.TopLeft,
                2 => AttachmentPoint.TopCenter,
                3 => AttachmentPoint.TopRight,
                4 => AttachmentPoint.MiddleLeft,
                5 => AttachmentPoint.MiddleCenter,
                6 => AttachmentPoint.MiddleRight,
                7 => AttachmentPoint.BottomLeft,
                8 => AttachmentPoint.BottomCenter,
                9 => AttachmentPoint.BottomRight,
                10 => AttachmentPoint.BaseLeft,
                11 => AttachmentPoint.BaseCenter,
                12 => AttachmentPoint.BaseRight,
                _ => AttachmentPoint.BaseLeft
            };
        }
    }
}

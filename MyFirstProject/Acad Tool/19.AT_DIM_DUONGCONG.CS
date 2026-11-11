// (C) Copyright 2026
// Lệnh ghi Label cho các đối tượng Arc bằng Civil 3D General Segment Label
using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

// Civil 3D
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;

[assembly: CommandClass(typeof(Civil3DCsharp.AT_DIM_DUONGCONG_Command))]

namespace Civil3DCsharp
{
    public class AT_DIM_DUONGCONG_Command
    {
        /// <summary>
        /// Lệnh ghi Label cho các đối tượng Arc sử dụng Civil 3D General Segment Label
        /// </summary>
        [CommandMethod("AT_Dim_Duong_Cong")]
        public static void AT_DIM_DUONGCONG()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;

            try
            {
                // Hiển thị form để chọn Curve Label Style
                var form = new MyFirstProject.ArcLabelForm();
                var dialogResult = Application.ShowModalDialog(form);

                if (dialogResult != System.Windows.Forms.DialogResult.OK || !form.FormAccepted)
                {
                    ed.WriteMessage("\nĐã hủy lệnh.");
                    return;
                }

                ObjectId selectedCurveLabelStyleId = form.SelectedCurveLabelStyleId;
                double labelRatio = form.LabelRatio;

                if (selectedCurveLabelStyleId == ObjectId.Null)
                {
                    ed.WriteMessage("\nKhông tìm thấy Curve Label Style được chọn.");
                    return;
                }

                // Yêu cầu người dùng chọn nhiều Arc
                PromptSelectionOptions selOptions = new PromptSelectionOptions()
                {
                    MessageForAdding = "\nChọn các đối tượng Arc để ghi label: ",
                    AllowDuplicates = false
                };

                // Tạo filter chỉ chọn Arc
                TypedValue[] filterList = new TypedValue[]
                {
                    new TypedValue((int)DxfCode.Start, "ARC")
                };
                SelectionFilter filter = new SelectionFilter(filterList);

                PromptSelectionResult selResult = ed.GetSelection(selOptions, filter);
                if (selResult.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\nĐã hủy chọn đối tượng.");
                    return;
                }

                SelectionSet selSet = selResult.Value;
                int successCount = 0;
                int totalCount = selSet.Count;

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    foreach (SelectedObject selObj in selSet)
                    {
                        if (selObj == null) continue;

                        Arc? arc = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as Arc;
                        if (arc == null) continue;

                        try
                        {
                            // Tạo General Segment Label cho Arc
                            // Sử dụng overload đơn giản với 2 tham số
                            // Sau đó cập nhật label style
                            ObjectId labelId = GeneralSegmentLabel.Create(
                                selObj.ObjectId,    // featureId: ObjectId của Arc
                                labelRatio          // ratio: vị trí label từ form
                            );

                            if (labelId != ObjectId.Null)
                            {
                                // Mở label và cập nhật style nếu cần
                                GeneralSegmentLabel? label = tr.GetObject(labelId, OpenMode.ForWrite) as GeneralSegmentLabel;
                                if (label != null)
                                {
                                    label.CurveLabelStyleId = selectedCurveLabelStyleId;
                                }
                                successCount++;
                            }
                        }
                        catch (System.Exception ex)
                        {
                            ed.WriteMessage($"\nLỗi khi tạo label cho Arc ID {selObj.ObjectId}: {ex.Message}");
                        }
                    }

                    tr.Commit();
                }

                // Thông báo kết quả
                ed.WriteMessage($"\n{new string('=', 50)}");
                ed.WriteMessage($"\nHoàn thành: Đã tạo {successCount}/{totalCount} label.");
                ed.WriteMessage($"\n{new string('=', 50)}");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\nLỗi: " + ex.Message);
            }
        }
    }
}

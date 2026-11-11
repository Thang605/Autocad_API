// (C) Copyright 2024 by T27
// Lệnh xoay đối tượng theo viewport hiện hành
//
using System;

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using Autodesk.Civil.DatabaseServices;
using MyFirstProject.Extensions;

// Alias để tránh xung đột namespace giữa AutoCAD và Civil 3D
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using Label = Autodesk.Civil.DatabaseServices.Label;

// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3DCsharp.AT_XoayDoiTuong_TheoViewport))]

namespace Civil3DCsharp
{
    /// <summary>
    /// Class chứa lệnh xoay đối tượng theo viewport hiện hành
    /// Hỗ trợ các loại đối tượng:
    /// - AutoCAD: DBText, MText, BlockReference, MLeader
    /// - Civil 3D: Label (base class), NoteLabel, SurfaceElevationLabel, AlignmentLabel, ProfileLabel, v.v.
    /// </summary>
    class AT_XoayDoiTuong_TheoViewport
    {
        /// <summary>
        /// Xoay các đối tượng được chọn theo góc xoay của viewport hiện hành
        /// </summary>
        [CommandMethod("AT_Xoay_Doi_Tuong_Theo_Viewport")]
        public static void XoayDoiTuong_TheoViewport()
        {
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                // Lấy góc xoay viewport hiện hành
                ViewTableRecord acView = A.Ed.GetCurrentView();
                double angleViewport = acView.ViewTwist;
                double angleDegrees = angleViewport * 180.0 / Math.PI;
                A.Ed.WriteMessage($"\n Góc xoay viewport hiện hành: {angleDegrees:F2}°");

                // Chọn các đối tượng cần xoay
                ObjectIdCollection objectIdColl = UserInput.GSelectionSet("\n Chọn các đối tượng cần xoay theo viewport:");

                if (objectIdColl == null || objectIdColl.Count == 0)
                {
                    A.Ed.WriteMessage("\n Không có đối tượng nào được chọn.");
                    return;
                }

                int countRotated = 0;
                int countSkipped = 0;

                // Tính góc xoay (normalize về khoảng 0 đến 2*PI)
                double rotationAngle = NormalizeAngle(-angleViewport);

                foreach (ObjectId objId in objectIdColl)
                {
                    try
                    {
                        var entity = tr.GetObject(objId, OpenMode.ForWrite);

                        if (entity is DBText text)
                        {
                            text.Rotation = rotationAngle;
                            countRotated++;
                        }
                        else if (entity is MText mText)
                        {
                            mText.Rotation = rotationAngle;
                            countRotated++;
                        }
                        else if (entity is NoteLabel noteLabel)
                        {
                            noteLabel.RotationAngle = rotationAngle;
                            countRotated++;
                        }
                        else if (entity is Label label)
                        {
                            try
                            {
                                label.RotationAngle = rotationAngle;
                                countRotated++;
                            }
                            catch
                            {
                                // Fallback: xoay bằng TransformBy
                                try
                                {
                                    Point3d labelLocation = label.LabelLocation;
                                    Matrix3d rotationMatrix = Matrix3d.Rotation(rotationAngle, Vector3d.ZAxis, labelLocation);
                                    label.TransformBy(rotationMatrix);
                                    countRotated++;
                                }
                                catch
                                {
                                    countSkipped++;
                                }
                            }
                        }
                        else if (entity is BlockReference blockRef)
                        {
                            blockRef.Rotation = rotationAngle;
                            countRotated++;
                        }
                        else if (entity is MLeader mLeader)
                        {
                            if (mLeader.ContentType == ContentType.MTextContent)
                            {
                                MText mTextContent = mLeader.MText;
                                if (mTextContent != null)
                                {
                                    mTextContent.Rotation = rotationAngle;
                                    mLeader.MText = mTextContent;
                                }
                            }
                            countRotated++;
                        }
                        else
                        {
                            countSkipped++;
                        }
                    }
                    catch
                    {
                        countSkipped++;
                    }
                }

                // Thông báo kết quả
                A.Ed.WriteMessage($"\n Đã xoay {countRotated} đối tượng.");
                if (countSkipped > 0)
                    A.Ed.WriteMessage($" Bỏ qua {countSkipped} đối tượng.");

                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage($"\n Lỗi: {e.Message}");
            }
        }

        /// <summary>
        /// Xoay đối tượng theo viewport với điểm cơ sở xoay
        /// </summary>
        [CommandMethod("AT_Xoay_Doi_Tuong_Theo_Viewport_V2")]
        public static void XoayDoiTuong_TheoViewport_V2()
        {
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                ViewTableRecord acView = A.Ed.GetCurrentView();
                double angleViewport = acView.ViewTwist;
                double angleDegrees = angleViewport * 180.0 / Math.PI;
                A.Ed.WriteMessage($"\n Góc xoay viewport hiện hành: {angleDegrees:F2}°");

                ObjectIdCollection objectIdColl = UserInput.GSelectionSet("\n Chọn các đối tượng cần xoay:");

                if (objectIdColl == null || objectIdColl.Count == 0)
                {
                    A.Ed.WriteMessage("\n Không có đối tượng nào được chọn.");
                    return;
                }

                Point3d basePoint = UserInput.GPoint("\n Chọn điểm cơ sở xoay:");
                double rotationAngle = -angleViewport;
                int countRotated = 0;

                foreach (ObjectId objId in objectIdColl)
                {
                    Entity entity = (Entity)tr.GetObject(objId, OpenMode.ForWrite);
                    Matrix3d rotationMatrix = Matrix3d.Rotation(rotationAngle, Vector3d.ZAxis, basePoint);
                    entity.TransformBy(rotationMatrix);
                    countRotated++;
                }

                A.Ed.WriteMessage($"\n Đã xoay {countRotated} đối tượng quanh điểm cơ sở.");
                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage($"\n Lỗi: {e.Message}");
            }
        }

        /// <summary>
        /// Chuẩn hóa góc về khoảng 0 đến 2*PI
        /// </summary>
        private static double NormalizeAngle(double angle)
        {
            double twoPi = 2 * Math.PI;
            double result = angle % twoPi;
            if (result < 0)
                result += twoPi;
            return result;
        }
    }
}

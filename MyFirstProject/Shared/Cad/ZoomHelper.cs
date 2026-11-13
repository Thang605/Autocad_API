using System;
using System.Collections.Generic;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

using MyFirstProject.Shared.UI;

namespace MyFirstProject.Shared.Cad
{
    /// <summary>Zoom màn hình tới vùng / đối tượng. Hỗ trợ view xoay (ViewTwist) và UCS bất kỳ.</summary>
    public static class ZoomHelper
    {
        /// <summary>
        /// Zoom tới khung bao (tọa độ WCS). <paramref name="margin"/> = 1.2 nghĩa là chừa thêm 20% xung quanh.
        /// </summary>
        public static void ZoomToExtents(Editor ed, Extents3d extents, double margin = 1.2)
        {
            using (ViewTableRecord view = ed.GetCurrentView())
            {
                // Ma trận DCS → WCS của view hiện hành, đảo lại để đưa khung bao về DCS.
                Matrix3d dcsToWcs =
                    Matrix3d.Rotation(-view.ViewTwist, view.ViewDirection, view.Target) *
                    Matrix3d.Displacement(view.Target - Point3d.Origin) *
                    Matrix3d.PlaneToWorld(view.ViewDirection);

                Extents3d dcs = extents;
                dcs.TransformBy(dcsToWcs.Inverse());

                double width = dcs.MaxPoint.X - dcs.MinPoint.X;
                double height = dcs.MaxPoint.Y - dcs.MinPoint.Y;
                if (width < 1e-6 && height < 1e-6)
                {
                    width = 10.0;
                    height = 10.0;
                }

                // Giữ đúng tỷ lệ khung nhìn hiện tại.
                double ratio = view.Height > 1e-9 ? view.Width / view.Height : 1.0;
                if (height < 1e-9 || width / height > ratio)
                    height = width / ratio;
                else
                    width = height * ratio;

                view.CenterPoint = new Point2d((dcs.MinPoint.X + dcs.MaxPoint.X) / 2.0, (dcs.MinPoint.Y + dcs.MaxPoint.Y) / 2.0);
                view.Width = width * margin;
                view.Height = height * margin;
                ed.SetCurrentView(view);
            }
        }

        /// <summary>
        /// Zoom tới tập đối tượng. Bỏ qua ObjectId đã xóa / không có khung bao. Trả về false nếu không có gì để zoom.
        /// </summary>
        public static bool ZoomToEntities(Editor ed, IEnumerable<ObjectId> ids, double margin = 1.2)
        {
            Database db = ed.Document.Database;
            Extents3d total = new Extents3d();
            bool hasAny = false;

            using (Transaction tr = db.TransactionManager.StartOpenCloseTransaction())
            {
                foreach (ObjectId id in PersistentState.FilterAlive(ids, db))
                {
                    if (tr.GetObject(id, OpenMode.ForRead) is not Entity ent) continue;

                    // Entity.Bounds trả null thay vì ném eNullExtents như GeometricExtents.
                    Extents3d? bounds = ent.Bounds;
                    if (!bounds.HasValue) continue;

                    if (hasAny)
                    {
                        total.AddExtents(bounds.Value);
                    }
                    else
                    {
                        total = bounds.Value;
                        hasAny = true;
                    }
                }
                tr.Commit();
            }

            if (!hasAny) return false;
            ZoomToExtents(ed, total, margin);
            return true;
        }
    }
}

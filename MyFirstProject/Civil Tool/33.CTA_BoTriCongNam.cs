using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using MyFirstProject.Civil_Tool;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(Civil3DCsharp.CTA_BoTriCongNam_Commands))]

namespace Civil3DCsharp
{
    public class CTA_BoTriCongNam_Commands
    {
        private static ObjectId _lastAlignmentId = ObjectId.Null;
        private static int _lastEntity1Id = -1;
        private static int _lastEntity2Id = -1;
        private static int _lastStandardIndex = 0;
        private static int _lastVtkIndex = 1;

        [CommandMethod("CTA_Bo_Tri_Cong_Nam")]
        public void CTA_BoTriCongNam()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            try
            {
                BoTriCongNamForm form = new BoTriCongNamForm();

                // Nạp lại thông tin đối tượng đã chọn trước đó (nếu còn hợp lệ trong bản vẽ)
                if (!_lastAlignmentId.IsNull && _lastAlignmentId.IsValid && !_lastAlignmentId.IsErased)
                {
                    try
                    {
                        using (Transaction tr = db.TransactionManager.StartTransaction())
                        {
                            Alignment align = tr.GetObject(_lastAlignmentId, OpenMode.ForRead, false, true) as Alignment;
                            if (align != null)
                            {
                                form.AlignmentId = _lastAlignmentId;
                                form.TxtAlignName.Text = align.Name;
                                form.Entity1Id = _lastEntity1Id;
                                if (_lastEntity1Id != -1) form.LblEnt1.Text = $"Cánh tuyến 1: ID {_lastEntity1Id}";
                                form.Entity2Id = _lastEntity2Id;
                                if (_lastEntity2Id != -1) form.LblEnt2.Text = $"Cánh tuyến 2: ID {_lastEntity2Id}";
                                if (_lastEntity1Id != -1 && _lastEntity2Id != -1)
                                {
                                    form.DeflectionAngle = CalculateDeflectionAngle(db, _lastAlignmentId, _lastEntity1Id, _lastEntity2Id);
                                }
                            }
                        }
                    }
                    catch { }
                }
                form.StandardIndex = _lastStandardIndex;
                form.VtkIndex = _lastVtkIndex;

                // Wire up pick events
                form.BtnPickAlignment.Click += (s, e) =>
                {
                    try
                    {
                        using (var interaction = ed.StartUserInteraction(form))
                        {
                            PromptEntityOptions peo = new PromptEntityOptions("\nChọn Alignment (tuyến đường): ");
                            peo.SetRejectMessage("\nĐối tượng chọn phải là Alignment!");
                            peo.AddAllowedClass(typeof(Alignment), true);
                            PromptEntityResult per = ed.GetEntity(peo);
                            interaction.End();
                            
                            if (per.Status == PromptStatus.OK)
                            {
                                using (Transaction tr = db.TransactionManager.StartTransaction())
                                {
                                    Alignment align = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Alignment;
                                    if (align != null)
                                    {
                                        form.AlignmentId = per.ObjectId;
                                        form.TxtAlignName.Text = align.Name;
                                        form.Entity1Id = -1;
                                        form.Entity2Id = -1;
                                        form.LblEnt1.Text = "Cánh tuyến 1: (Chưa chọn)";
                                        form.LblEnt2.Text = "Cánh tuyến 2: (Chưa chọn)";
                                        form.DeflectionAngle = 0;
                                    }
                                }
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        ed.WriteMessage($"\nLỗi khi chọn Alignment: {ex.Message}");
                    }
                };

                form.BtnPickEnt1.Click += (s, e) =>
                {
                    if (form.AlignmentId.IsNull)
                    {
                        MessageBox.Show("Vui lòng chọn Alignment trước!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    try
                    {
                        using (var interaction = ed.StartUserInteraction(form))
                        {
                            int entId = PickAlignmentLineEntity(ed, db, form.AlignmentId, "\nChọn Cánh tuyến 1 (đoạn thẳng 1): ");
                            interaction.End();
                            
                            if (entId != -1)
                            {
                                form.Entity1Id = entId;
                                form.LblEnt1.Text = $"Cánh tuyến 1: ID {entId}";
                                if (form.Entity2Id != -1)
                                {
                                    form.DeflectionAngle = CalculateDeflectionAngle(db, form.AlignmentId, form.Entity1Id, form.Entity2Id);
                                }
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        ed.WriteMessage($"\nLỗi khi chọn cánh tuyến 1: {ex.Message}");
                    }
                };

                form.BtnPickEnt2.Click += (s, e) =>
                {
                    if (form.AlignmentId.IsNull)
                    {
                        MessageBox.Show("Vui lòng chọn Alignment trước!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    try
                    {
                        using (var interaction = ed.StartUserInteraction(form))
                        {
                            int entId = PickAlignmentLineEntity(ed, db, form.AlignmentId, "\nChọn Cánh tuyến 2 (đoạn thẳng 2): ");
                            interaction.End();
                            
                            if (entId != -1)
                            {
                                form.Entity2Id = entId;
                                form.LblEnt2.Text = $"Cánh tuyến 2: ID {entId}";
                                if (form.Entity1Id != -1)
                                {
                                    form.DeflectionAngle = CalculateDeflectionAngle(db, form.AlignmentId, form.Entity1Id, form.Entity2Id);
                                }
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        ed.WriteMessage($"\nLỗi khi chọn cánh tuyến 2: {ex.Message}");
                    }
                };

                // Show dialog Modally
                DialogResult dr = Application.ShowModalDialog(form);

                if (dr == DialogResult.OK)
                {
                    _lastAlignmentId = form.AlignmentId;
                    _lastEntity1Id = form.Entity1Id;
                    _lastEntity2Id = form.Entity2Id;
                    _lastStandardIndex = form.StandardIndex;
                    _lastVtkIndex = form.VtkIndex;

                    ExecuteBoTriCongNam(ed, db, form.AlignmentId, form.Entity1Id, form.Entity2Id, form.Radius, form.SpiralLength);
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n❌ Lỗi thực thi lệnh CTA_BoTriCongNam: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private int PickAlignmentLineEntity(Editor ed, Database db, ObjectId alignId, string promptMsg)
        {
            PromptPointOptions ppo = new PromptPointOptions(promptMsg);
            PromptPointResult ppr = ed.GetPoint(ppo);
            if (ppr.Status != PromptStatus.OK) return -1;

            Point3d pt = ppr.Value;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                Alignment align = tr.GetObject(alignId, OpenMode.ForRead) as Alignment;
                if (align == null) return -1;

                int bestEntityId = -1;
                double minDistance = double.MaxValue;

                for (int i = 0; i < align.Entities.Count; i++)
                {
                    AlignmentEntity ent = align.Entities.GetEntityByOrder(i);
                    if (ent.EntityType == AlignmentEntityType.Line)
                    {
                        AlignmentLine line = ent as AlignmentLine;
                        if (line != null)
                        {
                            // Calculate distance from point to line segment
                            Point2d p1 = new Point2d(line.StartPoint.X, line.StartPoint.Y);
                            Point2d p2 = new Point2d(line.EndPoint.X, line.EndPoint.Y);
                            Point2d pTest = new Point2d(pt.X, pt.Y);

                            double dist = DistanceToSegment(pTest, p1, p2);
                            if (dist < minDistance)
                            {
                                minDistance = dist;
                                bestEntityId = ent.EntityId;
                            }
                        }
                    }
                }

                if (bestEntityId != -1)
                {
                    ed.WriteMessage($"\n✅ Đã chọn cánh tuyến ID: {bestEntityId} (Khoảng cách: {minDistance:F2}m)");
                }
                else
                {
                    ed.WriteMessage("\n⚠️ Không tìm thấy đoạn thẳng (Line) nào gần điểm chọn.");
                }

                return bestEntityId;
            }
        }

        private double CalculateDeflectionAngle(Database db, ObjectId alignId, int ent1Id, int ent2Id)
        {
            if (alignId.IsNull || ent1Id == -1 || ent2Id == -1) return 0;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                Alignment align = tr.GetObject(alignId, OpenMode.ForRead) as Alignment;
                if (align == null) return 0;

                AlignmentLine line1 = null;
                AlignmentLine line2 = null;

                for (int i = 0; i < align.Entities.Count; i++)
                {
                    AlignmentEntity ent = align.Entities.GetEntityByOrder(i);
                    if (ent.EntityId == ent1Id) line1 = ent as AlignmentLine;
                    if (ent.EntityId == ent2Id) line2 = ent as AlignmentLine;
                }

                if (line1 != null && line2 != null)
                {
                    Vector2d v1 = new Vector2d(line1.EndPoint.X - line1.StartPoint.X, line1.EndPoint.Y - line1.StartPoint.Y);
                    Vector2d v2 = new Vector2d(line2.EndPoint.X - line2.StartPoint.X, line2.EndPoint.Y - line2.StartPoint.Y);

                    double angle1 = Math.Atan2(v1.Y, v1.X);
                    double angle2 = Math.Atan2(v2.Y, v2.X);

                    double diff = Math.Abs(angle2 - angle1) * (180.0 / Math.PI);
                    if (diff > 180.0) diff = Math.Abs(360.0 - diff);
                    return diff;
                }
            }
            return 0;
        }

        private double DistanceToSegment(Point2d p, Point2d a, Point2d b)
        {
            double l2 = (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);
            if (l2 == 0) return Math.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y));

            double t = ((p.X - a.X) * (b.X - a.X) + (p.Y - a.Y) * (b.Y - a.Y)) / l2;
            t = Math.Max(0, Math.Min(1, t));

            Point2d proj = new Point2d(a.X + t * (b.X - a.X), a.Y + t * (b.Y - a.Y));
            return Math.Sqrt((p.X - proj.X) * (p.X - proj.X) + (p.Y - proj.Y) * (p.Y - proj.Y));
        }

        private void ExecuteBoTriCongNam(Editor ed, Database db, ObjectId alignId, int ent1Id, int ent2Id, double R, double Ls)
        {
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                Alignment align = tr.GetObject(alignId, OpenMode.ForWrite) as Alignment;
                if (align == null) return;

                try
                {
                    // Sắp xếp lại thứ tự ent1Id và ent2Id theo chiều lý trình để tránh lỗi API
                    int order1 = -1, order2 = -1;
                    for (int i = 0; i < align.Entities.Count; i++)
                    {
                        var ent = align.Entities.GetEntityByOrder(i);
                        if (ent.EntityId == ent1Id) order1 = i;
                        if (ent.EntityId == ent2Id) order2 = i;
                    }
                    if (order1 != -1 && order2 != -1 && order1 > order2)
                    {
                        int temp = ent1Id;
                        ent1Id = ent2Id;
                        ent2Id = temp;
                    }

                    if (Ls > 0)
                    {
                        // Add Free Spiral-Curve-Spiral (SCS)
                        align.Entities.AddFreeSCS(
                            ent1Id,
                            ent2Id,
                            Ls,
                            Ls,
                            SpiralParamType.Length,
                            R,
                            false,
                            Autodesk.Civil.SpiralType.Clothoid
                        );
                        ed.WriteMessage($"\n🎉 Đã bố trí đường cong chuyển tiếp (Spiral-Curve-Spiral): R = {R}m, Ls = {Ls}m giữa cánh tuyến {ent1Id} và {ent2Id}.");
                    }
                    else
                    {
                        // Add Free Curve (Circle arc)
                        align.Entities.AddFreeCurve(
                            ent1Id,
                            ent2Id,
                            R,
                            CurveParamType.Radius,
                            false,
                            CurveType.Compound
                        );
                        ed.WriteMessage($"\n🎉 Đã bố trí đường cong tròn (Free Curve): R = {R}m giữa cánh tuyến {ent1Id} và {ent2Id}.");
                    }

                    tr.Commit();
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage($"\n❌ Lỗi khi thêm đường cong vào Alignment: {ex.Message}");
                    MessageBox.Show($"Lỗi khi bố trí cong nằm: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}

// (C) Copyright 2026 by T27
// Lệnh Đồng Bộ Đỉnh Polyline (AT_DongBoDinhPolyline / DBPL / SYNC_PL)
// Kiểm tra 2 polyline có cùng số đỉnh không và đồng bộ tọa độ đỉnh của polyline 1 theo polyline 2
//

using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(Civil3DCsharp.AT_DongBoDinhPolyline))]

namespace Civil3DCsharp
{
    public class AT_DongBoDinhPolyline
    {
        /// <summary>
        /// Lệnh chính: Đồng Bộ Đỉnh Polyline với Form giao diện tương tác
        /// </summary>
        [CommandMethod("AT_DongBoDinhPolyline")]
        [CommandMethod("DBPL")]
        [CommandMethod("SYNC_PL")]
        public static void DongBoDinhPolylineCommand()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            Editor ed = doc.Editor;
            Database db = doc.Database;

            try
            {
                // Khởi tạo Form
                using (var form = new DongBoDinhPolylineForm())
                {
                    // Callback 1: Lấy thông tin Polyline theo ObjectId
                    form.OnGetPolylineInfo = (objId) =>
                    {
                        if (objId.IsNull || !objId.IsValid || objId.IsErased) return null;
                        using (var tr = db.TransactionManager.StartTransaction())
                        {
                            var info = ExtractPolylineInfo(objId, tr);
                            tr.Commit();
                            return info;
                        }
                    };

                    // Callback 2: Pick Polyline 1 (Đích) trên CAD
                    form.OnPickTargetPolyline = () =>
                    {
                        return PickPolylineOnCanvas(ed, db, form, "\n📍 Chọn Polyline 1 (ĐÍCH - Cần thay đổi tọa độ): ");
                    };

                    // Callback 3: Pick Polyline 2 (Nguồn) trên CAD
                    form.OnPickSourcePolyline = () =>
                    {
                        return PickPolylineOnCanvas(ed, db, form, "\n📍 Chọn Polyline 2 (NGUỒN - Mẫu tọa độ): ");
                    };

                    // Callback 4: Thực thi đồng bộ đỉnh
                    form.OnExecuteSync = (targetInfo, sourceInfo, config) =>
                    {
                        return ExecuteSyncVertices(doc, targetInfo, sourceInfo, config);
                    };

                    // Tự động khôi phục Polyline đã chọn ở phiên trước (nếu còn tồn tại)
                    PolylineInfoItem? initialTarget = null;
                    PolylineInfoItem? initialSource = null;

                    using (var tr = db.TransactionManager.StartTransaction())
                    {
                        ObjectId lastTargetId = DongBoDinhPolylineForm.GetLastTargetId();
                        ObjectId lastSourceId = DongBoDinhPolylineForm.GetLastSourceId();

                        if (!lastTargetId.IsNull && lastTargetId.IsValid && !lastTargetId.IsErased)
                        {
                            initialTarget = ExtractPolylineInfo(lastTargetId, tr);
                        }

                        if (!lastSourceId.IsNull && lastSourceId.IsValid && !lastSourceId.IsErased)
                        {
                            initialSource = ExtractPolylineInfo(lastSourceId, tr);
                        }

                        tr.Commit();
                    }

                    form.LoadInitialPolylines(initialTarget, initialSource);

                    // Hiển thị Form Modal Dialog
                    Application.ShowModalDialog(form);
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n❌ Lỗi thực thi lệnh Đồng Bộ Đỉnh Polyline: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Trích xuất toàn bộ thông tin đỉnh và thuộc tính của Polyline (hỗ trợ Lightweight, 2D Heavy, 3D)
        /// </summary>
        public static PolylineInfoItem? ExtractPolylineInfo(ObjectId polylineId, Transaction tr)
        {
            if (polylineId.IsNull || !polylineId.IsValid || polylineId.IsErased)
                return null;

            var entity = tr.GetObject(polylineId, OpenMode.ForRead) as Entity;
            if (entity == null) return null;

            var info = new PolylineInfoItem
            {
                Id = polylineId,
                Handle = entity.Handle.ToString(),
                Layer = entity.Layer
            };

            // 1. Lightweight Polyline (Autodesk.AutoCAD.DatabaseServices.Polyline)
            if (entity is Polyline lwPl)
            {
                info.TypeName = "LwPolyline (2D)";
                info.VertexCount = lwPl.NumberOfVertices;
                info.Length = lwPl.Length;
                info.IsClosed = lwPl.Closed;
                info.Elevation = lwPl.Elevation;

                for (int i = 0; i < lwPl.NumberOfVertices; i++)
                {
                    Point2d pt = lwPl.GetPoint2dAt(i);
                    double bulge = lwPl.GetBulgeAt(i);
                    info.Vertices.Add(new PolylineVertexInfo
                    {
                        Index = i,
                        X = pt.X,
                        Y = pt.Y,
                        Z = lwPl.Elevation,
                        Bulge = bulge
                    });
                }
                return info;
            }

            // 2. Heavy 2D Polyline (Autodesk.AutoCAD.DatabaseServices.Polyline2d)
            if (entity is Polyline2d pl2d)
            {
                info.TypeName = "Polyline2D (Heavy)";
                info.Length = pl2d.Length;
                info.IsClosed = pl2d.Closed;
                info.Elevation = pl2d.Elevation;

                int idx = 0;
                foreach (ObjectId vId in pl2d)
                {
                    var v = tr.GetObject(vId, OpenMode.ForRead) as Vertex2d;
                    if (v != null && v.VertexType != Vertex2dType.SplineControlVertex)
                    {
                        info.Vertices.Add(new PolylineVertexInfo
                        {
                            Index = idx++,
                            X = v.Position.X,
                            Y = v.Position.Y,
                            Z = v.Position.Z,
                            Bulge = v.Bulge
                        });
                    }
                }
                info.VertexCount = info.Vertices.Count;
                return info;
            }

            // 3. 3D Polyline (Autodesk.AutoCAD.DatabaseServices.Polyline3d)
            if (entity is Polyline3d pl3d)
            {
                info.TypeName = "Polyline3D";
                info.Length = pl3d.Length;
                info.IsClosed = pl3d.Closed;
                info.Elevation = 0;

                int idx = 0;
                foreach (ObjectId vId in pl3d)
                {
                    var v = tr.GetObject(vId, OpenMode.ForRead) as PolylineVertex3d;
                    if (v != null && v.VertexType != Vertex3dType.ControlVertex)
                    {
                        info.Vertices.Add(new PolylineVertexInfo
                        {
                            Index = idx++,
                            X = v.Position.X,
                            Y = v.Position.Y,
                            Z = v.Position.Z,
                            Bulge = 0
                        });
                    }
                }
                info.VertexCount = info.Vertices.Count;
                return info;
            }

            return null;
        }

        /// <summary>
        /// Chọn Polyline tương tác trên AutoCAD Canvas
        /// </summary>
        private static PolylineInfoItem? PickPolylineOnCanvas(Editor ed, Database db, System.Windows.Forms.Form form, string promptMsg)
        {
            using (ed.StartUserInteraction(form))
            {
                var peo = new PromptEntityOptions(promptMsg);
                peo.SetRejectMessage("\n⚠️ Đối tượng được chọn phải là Polyline (2D hoặc 3D)!");
                peo.AddAllowedClass(typeof(Polyline), true);
                peo.AddAllowedClass(typeof(Polyline2d), true);
                peo.AddAllowedClass(typeof(Polyline3d), true);

                var per = ed.GetEntity(peo);
                if (per.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\nĐã hủy chọn Polyline.");
                    return null;
                }

                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var info = ExtractPolylineInfo(per.ObjectId, tr);
                    tr.Commit();
                    if (info != null)
                    {
                        ed.WriteMessage($"\n✅ Đã chọn: {info.TypeName} (Layer: {info.Layer}, Số đỉnh: {info.VertexCount})");
                    }
                    return info;
                }
            }
        }

        /// <summary>
        /// Thực thi đồng bộ tọa độ đỉnh từ Polyline nguồn sang Polyline đích
        /// </summary>
        private static bool ExecuteSyncVertices(Document doc, PolylineInfoItem targetInfo, PolylineInfoItem sourceInfo, PolylineSyncConfig config)
        {
            Editor ed = doc.Editor;
            Database db = doc.Database;

            if (targetInfo.VertexCount != sourceInfo.VertexCount)
            {
                ed.WriteMessage($"\n❌ Lỗi: Polyline 1 ({targetInfo.VertexCount} đỉnh) và Polyline 2 ({sourceInfo.VertexCount} đỉnh) không cùng số đỉnh!");
                return false;
            }

            using (DocumentLock docLock = doc.LockDocument())
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var targetEntity = tr.GetObject(targetInfo.Id, OpenMode.ForWrite) as Entity;
                    if (targetEntity == null)
                    {
                        ed.WriteMessage("\n❌ Không thể mở Polyline đích để ghi dữ liệu.");
                        return false;
                    }

                    // Chuẩn bị danh sách đỉnh nguồn
                    var srcVerts = new List<PolylineVertexInfo>(sourceInfo.Vertices);

                    if (config.ReverseOrder)
                    {
                        int n = srcVerts.Count;
                        var reversedList = new List<PolylineVertexInfo>();

                        for (int i = 0; i < n; i++)
                        {
                            var original = srcVerts[n - 1 - i];
                            // Khi đảo chiều polyline, bulge của đoạn i đi ngược lại sẽ là -bulge của đoạn tương ứng trước đó
                            double newBulge = 0;
                            if (n - 2 - i >= 0)
                            {
                                newBulge = -srcVerts[n - 2 - i].Bulge;
                            }
                            else if (sourceInfo.IsClosed && n > 0)
                            {
                                newBulge = -srcVerts[n - 1].Bulge;
                            }

                            reversedList.Add(new PolylineVertexInfo
                            {
                                Index = i,
                                X = original.X,
                                Y = original.Y,
                                Z = original.Z,
                                Bulge = newBulge
                            });
                        }
                        srcVerts = reversedList;
                    }

                    // 1. Trường hợp Polyline Đích là Lightweight Polyline
                    if (targetEntity is Polyline lwPlTarget)
                    {
                        if (lwPlTarget.NumberOfVertices != srcVerts.Count)
                        {
                            ed.WriteMessage("\n❌ Số đỉnh thực tế của Polyline đích không khớp!");
                            return false;
                        }

                        for (int i = 0; i < srcVerts.Count; i++)
                        {
                            var sv = srcVerts[i];
                            lwPlTarget.SetPointAt(i, new Point2d(sv.X, sv.Y));
                            if (config.SyncBulge)
                            {
                                lwPlTarget.SetBulgeAt(i, sv.Bulge);
                            }
                        }

                        if (config.SyncElevation)
                        {
                            lwPlTarget.Elevation = sourceInfo.Elevation;
                        }

                        if (config.SyncClosed)
                        {
                            lwPlTarget.Closed = sourceInfo.IsClosed;
                        }
                    }
                    // 2. Trường hợp Polyline Đích là Heavy 2D Polyline
                    else if (targetEntity is Polyline2d pl2dTarget)
                    {
                        int idx = 0;
                        foreach (ObjectId vId in pl2dTarget)
                        {
                            var v = tr.GetObject(vId, OpenMode.ForWrite) as Vertex2d;
                            if (v != null && v.VertexType != Vertex2dType.SplineControlVertex && idx < srcVerts.Count)
                            {
                                var sv = srcVerts[idx];
                                v.Position = new Point3d(sv.X, sv.Y, config.SyncElevation ? sv.Z : v.Position.Z);
                                if (config.SyncBulge)
                                {
                                    v.Bulge = sv.Bulge;
                                }
                                idx++;
                            }
                        }

                        if (config.SyncClosed)
                        {
                            pl2dTarget.Closed = sourceInfo.IsClosed;
                        }
                    }
                    // 3. Trường hợp Polyline Đích là 3D Polyline
                    else if (targetEntity is Polyline3d pl3dTarget)
                    {
                        int idx = 0;
                        foreach (ObjectId vId in pl3dTarget)
                        {
                            var v = tr.GetObject(vId, OpenMode.ForWrite) as PolylineVertex3d;
                            if (v != null && v.VertexType != Vertex3dType.ControlVertex && idx < srcVerts.Count)
                            {
                                var sv = srcVerts[idx];
                                v.Position = new Point3d(sv.X, sv.Y, config.SyncElevation ? sv.Z : v.Position.Z);
                                idx++;
                            }
                        }

                        if (config.SyncClosed)
                        {
                            pl3dTarget.Closed = sourceInfo.IsClosed;
                        }
                    }

                    tr.Commit();
                    ed.WriteMessage($"\n✅ ĐỒNG BỘ THÀNH CÔNG: Đã cập nhật {srcVerts.Count} đỉnh của Polyline 1 (Handle: {targetInfo.Handle}) theo Polyline 2 (Handle: {sourceInfo.Handle}).");
                    return true;
                }
                catch (System.Exception ex)
                {
                    tr.Abort();
                    ed.WriteMessage($"\n❌ Lỗi khi cập nhật Polyline: {ex.Message}");
                    return false;
                }
            }
        }
    }
}

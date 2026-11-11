// (C) Copyright 2015 by  
//
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Linq;

using Autodesk.AutoCAD.Runtime;
using Acad = Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;
using ATable = Autodesk.AutoCAD.DatabaseServices.Table;
using AcadDocument = Autodesk.AutoCAD.ApplicationServices.Application;

using Civil = Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using Autodesk.Civil.Runtime;
using Autodesk.Civil.Settings;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.Civil.ApplicationServices;
using CivSurface = Autodesk.Civil.DatabaseServices.TinSurface;
using Section = Autodesk.Civil.DatabaseServices.Section;
using Autodesk.Civil;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using Autodesk.AutoCAD.Colors;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using MyFirstProject.Extensions;
using WinFormsLabel = System.Windows.Forms.Label;
using AcadRegion = Autodesk.AutoCAD.DatabaseServices.Region;

// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3DCsharp.AT_Surface_frompolyline_Commands))]

namespace Civil3DCsharp
{
    public class AT_Surface_frompolyline_Commands
    {
        [CommandMethod("AT_Surface_From_Polyline")]
        public static void AT_Surface_frompolyline()
        {
            // start transaction
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();

                // Hiển thị form để nhập thông tin
                using (var inputForm = new SurfaceFromPolylineForm())
                {
                    if (inputForm.ShowDialog() != DialogResult.OK)
                    {
                        A.Ed.WriteMessage("\nĐã hủy lệnh.");
                        return;
                    }

                    // Lấy thông tin từ form
                    List<ObjectId> polylineIds = inputForm.PolylineIds;
                    ObjectId sourceSurfaceId = inputForm.SurfaceId;

                    if (polylineIds.Count == 0 || sourceSurfaceId == ObjectId.Null)
                    {
                        A.Ed.WriteMessage("\nThông tin không hợp lệ. Vui lòng chọn lại.");
                        return;
                    }

                    // Lấy source surface để lấy cao độ
                    CivSurface? sourceSurface = tr.GetObject(sourceSurfaceId, OpenMode.ForRead) as CivSurface;
                    if (sourceSurface == null)
                    {
                        A.Ed.WriteMessage("\nKhông thể lấy thông tin source surface.");
                        return;
                    }

                    A.Ed.WriteMessage($"\nBắt đầu tạo {polylineIds.Count} surface riêng biệt từ polylines với cao độ từ surface '{sourceSurface.Name}'...");

                    int successCount = 0;
                    foreach (ObjectId polylineId in polylineIds)
                    {
                        // Lấy polyline
                        Polyline? polyline = tr.GetObject(polylineId, OpenMode.ForRead) as Polyline;
                        if (polyline == null)
                        {
                            A.Ed.WriteMessage($"\nKhông thể lấy thông tin polyline ID: {polylineId}");
                            continue;
                        }

                        // Kiểm tra polyline có đóng không
                        if (!polyline.Closed)
                        {
                            A.Ed.WriteMessage($"\nPolyline ID {polylineId} không phải là polyline đóng. Bỏ qua.");
                            continue;
                        }

                        A.Ed.WriteMessage($"\nXử lý polyline với {polyline.NumberOfVertices} đỉnh...");

                        // Tạo surface từ polyline với cao độ từ source surface
                        ObjectId surfaceId = CreateSurfaceFromPolyline(polyline, sourceSurface, successCount + 1, tr);
                        
                        if (surfaceId != ObjectId.Null)
                        {
                            successCount++;
                            A.Ed.WriteMessage($"\nĐã tạo thành công surface từ polyline ID: {polylineId}");
                        }
                        else
                        {
                            A.Ed.WriteMessage($"\nKhông thể tạo surface từ polyline ID: {polylineId}");
                        }
                    }

                    if (successCount > 0)
                    {
                        A.Ed.WriteMessage($"\nĐã tạo thành công {successCount}/{polylineIds.Count} surfaces từ polylines với cao độ từ surface '{sourceSurface.Name}'."); 
                    }
                    else
                    {
                        A.Ed.WriteMessage("\nKhông thể tạo surface nào. Vui lòng kiểm tra lại dữ liệu đầu vào.");
                    }
                }

                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage($"\nLỗi AutoCAD: {e.Message}");
                tr.Abort();
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi hệ thống: {ex.Message}");
                tr.Abort();
            }
        }

        private static ObjectId CreateSurfaceFromPolyline(Polyline polyline, CivSurface sourceSurface, int surfaceNumber, Transaction tr)
        {
            try
            {
                A.Ed.WriteMessage($"\nTạo surface từ polyline...");

                // Tạo danh sách các điểm 3D từ polyline với cao độ từ source surface
                List<Point3d> surfacePoints = new List<Point3d>();

                for (int i = 0; i < polyline.NumberOfVertices; i++)
                {
                    Point2d vertex2d = polyline.GetPoint2dAt(i);
                    Point3d vertex3d = new Point3d(vertex2d.X, vertex2d.Y, 0);
                    
                    try
                    {
                        // Lấy cao độ từ source surface
                        double elevation = sourceSurface.FindElevationAtXY(vertex3d.X, vertex3d.Y);
                        
                        // Tạo điểm với cao độ từ surface
                        Point3d surfacePoint = new Point3d(vertex3d.X, vertex3d.Y, elevation);
                        surfacePoints.Add(surfacePoint);
                        
                        A.Ed.WriteMessage($"\nĐiểm {i + 1}: ({surfacePoint.X:F3}, {surfacePoint.Y:F3}, {surfacePoint.Z:F3})");
                    }
                    catch (Autodesk.Civil.CivilException)
                    {
                        A.Ed.WriteMessage($"\nKhông thể lấy cao độ tại điểm ({vertex3d.X:F3}, {vertex3d.Y:F3}) từ source surface. Sử dụng cao độ 0.");
                        
                        // Sử dụng cao độ 0 nếu không tìm được trên surface
                        Point3d surfacePoint = new Point3d(vertex3d.X, vertex3d.Y, 0);
                        surfacePoints.Add(surfacePoint);
                    }
                }

                if (surfacePoints.Count < 3)
                {
                    A.Ed.WriteMessage($"\nKhông đủ điểm để tạo surface (cần ít nhất 3 điểm, có {surfacePoints.Count} điểm)");
                    return ObjectId.Null;
                }

                // Tạo polyline 3D từ các điểm với cao độ thực tế
                ObjectId polyline3dId = CreatePolyline3DFromPoints(surfacePoints, surfaceNumber, tr);
                
                if (polyline3dId == ObjectId.Null)
                {
                    A.Ed.WriteMessage($"\nKhông thể tạo polyline 3D từ polyline");
                    return ObjectId.Null;
                }

                // Tạo surface từ polyline 3D
                ObjectId surfaceId = CreateSurfaceFromPolyline3D(polyline3dId, surfacePoints, surfaceNumber, tr);
                
                return surfaceId;
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi khi tạo surface: {ex.Message}");
                return ObjectId.Null;
            }
        }

        private static ObjectId CreatePolyline3DFromPoints(List<Point3d> points, int polylineNumber, Transaction tr)
        {
            try
            {
                A.Ed.WriteMessage($"\nTạo polyline 3D từ {points.Count} điểm...");

                // Tạo tên cho polyline 3D
                string polylineName = $"Polyline3D_From_Polyline_{polylineNumber}";

                using (Polyline3d poly3d = new Polyline3d())
                {
                    poly3d.PolyType = Poly3dType.SimplePoly;
                    poly3d.Closed = true;
                    
                    // Thêm polyline 3D vào database trước
                    BlockTable? bt = tr.GetObject(A.Db.BlockTableId, OpenMode.ForRead) as BlockTable;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    BlockTableRecord? btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                    
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    ObjectId poly3dId = btr.AppendEntity(poly3d);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                    tr.AddNewlyCreatedDBObject(poly3d, true);

                    // Thêm các vertices vào polyline 3D
                    foreach (Point3d pt in points)
                    {
                        PolylineVertex3d vertex = new PolylineVertex3d(pt);
                        poly3d.AppendVertex(vertex);
                        btr.AppendEntity(vertex);
                        tr.AddNewlyCreatedDBObject(vertex, true);
                    }

                    A.Ed.WriteMessage($"\nĐã tạo Polyline3D '{polylineName}' với {points.Count} điểm");
                    return poly3dId;
                }
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi khi tạo Polyline3D: {ex.Message}");
                return ObjectId.Null;
            }
        }

        private static ObjectId CreateSurfaceFromPolyline3D(ObjectId polyline3dId, List<Point3d> points, int surfaceNumber, Transaction tr)
        {
            try
            {
                A.Ed.WriteMessage($"\nTạo surface framework từ polyline 3D...");

                // Tạo tên cho surface
                string surfaceName = $"Surface_From_Polyline3D_{surfaceNumber}";

                // Lấy default surface style
                ObjectId surfaceStyleId = GetDefaultSurfaceStyleId();
                
                // Tạo TIN surface mới
                ObjectId newSurfaceId = ObjectId.Null;
                
                try 
                {
                    if (surfaceStyleId != ObjectId.Null)
                    {
                        newSurfaceId = TinSurface.Create(surfaceName, surfaceStyleId);
                        A.Ed.WriteMessage($"\nĐã tạo surface '{surfaceName}' với style");
                    }
                    else
                    {
                        // Tạo surface với style mặc định
                        CivilDocument civilDoc = CivilApplication.ActiveDocument;
                        if (civilDoc != null)
                        {
                            // Lấy style mặc định từ collection
                            SurfaceStyleCollection surfaceStyles = civilDoc.Styles.SurfaceStyles;
                            ObjectId defaultStyleId = ObjectId.Null;
                            
                            foreach (ObjectId styleId in surfaceStyles)
                            {
                                defaultStyleId = styleId;
                                break; // Lấy style đầu tiên
                            }
                            
                            if (defaultStyleId != ObjectId.Null)
                            {
                                newSurfaceId = TinSurface.Create(surfaceName, defaultStyleId);
                                A.Ed.WriteMessage($"\nĐã tạo surface '{surfaceName}' với style đầu tiên");
                            }
                            else
                            {
                                A.Ed.WriteMessage($"\nKhông tìm được style để tạo surface");
                                return ObjectId.Null;
                            }
                        }
                        else
                        {
                            A.Ed.WriteMessage($"\nKhông thể truy cập CivilDocument");
                            return ObjectId.Null;
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    A.Ed.WriteMessage($"\nLỗi khi tạo surface: {ex.Message}");
                    return ObjectId.Null;
                }

                if (newSurfaceId == ObjectId.Null)
                {
                    A.Ed.WriteMessage($"\nKhông thể tạo surface '{surfaceName}'");
                    return ObjectId.Null;
                }

                // Thông báo thành công và hướng dẫn user
                A.Ed.WriteMessage($"\n✓ Đã tạo surface '{surfaceName}' thành công!");
                A.Ed.WriteMessage($"\n✓ Đã tạo Polyline3D với {points.Count} điểm có cao độ từ source surface");
                A.Ed.WriteMessage($"\n");
                A.Ed.WriteMessage($"\n--- HƯỚNG DẪN TIẾP THEO ---");
                A.Ed.WriteMessage($"\nĐể thêm dữ liệu vào surface '{surfaceName}':");
                A.Ed.WriteMessage($"\n1. Mở Civil 3D Toolspace > Prospector");
                A.Ed.WriteMessage($"\n2. Tìm surface '{surfaceName}' > Definition > Breaklines");
                A.Ed.WriteMessage($"\n3. Right-click > Add... và chọn polyline 3D vừa tạo");
                A.Ed.WriteMessage($"\n4. Hoặc thêm các điểm vào Point Groups và import vào surface");
                A.Ed.WriteMessage($"\n");
                
                // Hiển thị thông tin các điểm
                A.Ed.WriteMessage($"\nThông tin các điểm với cao độ từ source surface:");
                for (int i = 0; i < Math.Min(points.Count, 10); i++) // Hiển thị tối đa 10 điểm
                {
                    Point3d point = points[i];
                    A.Ed.WriteMessage($"\n  Điểm {i + 1}: X={point.X:F3}, Y={point.Y:F3}, Z={point.Z:F3}");
                }
                
                if (points.Count > 10)
                {
                    A.Ed.WriteMessage($"\n  ... và {points.Count - 10} điểm khác");
                }

                return newSurfaceId;
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi khi tạo surface từ polyline 3D: {ex.Message}");
                return ObjectId.Null;
            }
        }

        private static ObjectId GetDefaultSurfaceStyleId()
        {
            try
            {
                using (Transaction tr = A.Db.TransactionManager.StartTransaction())
                {
                    // Lấy surface style collection
                    CivilDocument civilDoc = CivilApplication.ActiveDocument;
                    SurfaceStyleCollection surfaceStyles = civilDoc.Styles.SurfaceStyles;
                    
                    // Thử lấy style mặc định hoặc style đầu tiên
                    foreach (ObjectId styleId in surfaceStyles)
                    {
                        SurfaceStyle? style = tr.GetObject(styleId, OpenMode.ForRead) as SurfaceStyle;
                        if (style != null)
                        {
                            tr.Commit();
                            return styleId;
                        }
                    }
                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi khi lấy surface style: {ex.Message}");
            }
            
            // Trả về ObjectId.Null nếu không tìm được style
            return ObjectId.Null;
        }
    }

    // Form để nhập thông tin
    public class SurfaceFromPolylineForm : Form
    {
        private Button? btnSelectPolylines;
        private Button? btnSelectSurface;
        private WinFormsLabel? lblPolylines;
        private WinFormsLabel? lblSurface;
        private Button? btnOK;
        private Button? btnCancel;

        public List<ObjectId> PolylineIds { get; private set; } = new List<ObjectId>();
        public ObjectId SurfaceId { get; private set; } = ObjectId.Null;

        public SurfaceFromPolylineForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Tạo Surfaces từ Polylines";
            this.Size = new System.Drawing.Size(400, 200);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Polylines selection
            lblPolylines = new WinFormsLabel()
            {
                Text = "Polylines:",
                Location = new System.Drawing.Point(20, 20),
                Size = new System.Drawing.Size(100, 23),
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };

            btnSelectPolylines = new Button()
            {
                Text = "Chọn Polylines",
                Location = new System.Drawing.Point(130, 20),
                Size = new System.Drawing.Size(200, 30)
            };
            btnSelectPolylines.Click += BtnSelectPolylines_Click;

            // Surface selection
            lblSurface = new WinFormsLabel()
            {
                Text = "Source Surface:",
                Location = new System.Drawing.Point(20, 60),
                Size = new System.Drawing.Size(100, 23),
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft
            };

            btnSelectSurface = new Button()
            {
                Text = "Chọn Source Surface",
                Location = new System.Drawing.Point(130, 60),
                Size = new System.Drawing.Size(200, 30)
            };
            btnSelectSurface.Click += BtnSelectSurface_Click;

            // Buttons
            btnOK = new Button()
            {
                Text = "OK",
                Location = new System.Drawing.Point(180, 110),
                Size = new System.Drawing.Size(70, 30),
                DialogResult = DialogResult.OK
            };
            btnOK.Click += BtnOK_Click;

            btnCancel = new Button()
            {
                Text = "Cancel",
                Location = new System.Drawing.Point(260, 110),
                Size = new System.Drawing.Size(70, 30),
                DialogResult = DialogResult.Cancel
            };

            this.Controls.AddRange(new Control[] {
                lblPolylines, btnSelectPolylines,
                lblSurface, btnSelectSurface,
                btnOK, btnCancel
            });

            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;
        }

        private void BtnSelectPolylines_Click(object? sender, EventArgs e)
        {
            this.Hide();
            try
            {
                // Chọn nhiều polyline bằng selection set (quét chọn)
                PolylineIds.Clear();
                
                A.Ed.WriteMessage("\nChọn các polylines đóng để tạo surfaces: ");
                
                // Tạo selection filter để chỉ chọn polylines
                TypedValue[] filterArray = new TypedValue[]
                {
                    new TypedValue((int)DxfCode.Start, "LWPOLYLINE")
                };
                SelectionFilter filter = new SelectionFilter(filterArray);
                
                // Cho phép user chọn nhiều objects bằng cách quét hoặc click
                PromptSelectionOptions selectionOptions = new PromptSelectionOptions()
                {
                    MessageForAdding = "\nChọn các polylines (có thể quét hoặc click từng cái): ",
                    AllowDuplicates = false
                };
                
                PromptSelectionResult selectionResult = A.Ed.GetSelection(selectionOptions, filter);
                
                if (selectionResult.Status == PromptStatus.OK)
                {
                    SelectionSet selectionSet = selectionResult.Value;
                    ObjectId[] selectedIds = selectionSet.GetObjectIds();
                    
                    // Kiểm tra và thêm chỉ những polylines đóng
                    using (Transaction tr = A.Db.TransactionManager.StartTransaction())
                    {
                        int validCount = 0;
                        foreach (ObjectId id in selectedIds)
                        {
                            Polyline? polyline = tr.GetObject(id, OpenMode.ForRead) as Polyline;
                            if (polyline != null && polyline.Closed)
                            {
                                PolylineIds.Add(id);
                                validCount++;
                            }
                            else if (polyline != null && !polyline.Closed)
                            {
                                A.Ed.WriteMessage($"\nPolyline ID {id} không đóng, bỏ qua.");
                            }
                        }
                        tr.Commit();
                        
                        A.Ed.WriteMessage($"\nĐã chọn {validCount}/{selectedIds.Length} polylines hợp lệ (đóng)");
                    }
                }
                else if (selectionResult.Status == PromptStatus.Cancel)
                {
                    A.Ed.WriteMessage("\nĐã hủy chọn polylines.");
                }
                else
                {
                    A.Ed.WriteMessage("\nKhông có polylines nào được chọn.");
                }

                if (PolylineIds.Count > 0 && btnSelectPolylines != null)
                {
                    btnSelectPolylines.Text = $"Đã chọn {PolylineIds.Count} Polylines";
                    btnSelectPolylines.BackColor = System.Drawing.Color.LightGreen;
                }
                else if (btnSelectPolylines != null)
                {
                    btnSelectPolylines.Text = "Chọn Polylines";
                    btnSelectPolylines.BackColor = System.Drawing.SystemColors.Control;
                }
            }
            catch (System.Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Lỗi khi chọn polylines: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.Show();
            }
        }

        private void BtnSelectSurface_Click(object? sender, EventArgs e)
        {
            this.Hide();
            try
            {
                SurfaceId = UserInput.GSurfaceId("Chọn source surface để lấy cao độ: ");
                if (SurfaceId != ObjectId.Null && btnSelectSurface != null)
                {
                    btnSelectSurface.Text = "Đã chọn Source Surface";
                    btnSelectSurface.BackColor = System.Drawing.Color.LightGreen;
                }
            }
            catch (System.Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Lỗi khi chọn source surface: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.Show();
            }
        }

        private void BtnOK_Click(object? sender, EventArgs e)
        {
            if (PolylineIds.Count == 0)
            {
                MessageBox.Show("Vui lòng chọn ít nhất một polyline!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (SurfaceId == ObjectId.Null)
            {
                MessageBox.Show("Vui lòng chọn source surface!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}

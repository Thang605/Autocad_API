// (C) Copyright 2024 by T27
// Lệnh bố trí viewport theo hình polyline được chọn trong Model
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.ApplicationServices;

using MyFirstProject.Extensions;

// Alias để tránh xung đột namespace
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3DCsharp.AT_BoTri_ViewPort_TheoHinh))]

namespace Civil3DCsharp
{
    /// <summary>
    /// Enum cho hướng bố trí viewport
    /// </summary>
    public enum ViewportArrangement
    {
        Horizontal,  // Bố trí theo chiều ngang
        Vertical     // Bố trí theo chiều dọc
    }

    /// <summary>
    /// Enum cho cách sắp xếp polyline
    /// </summary>
    public enum PolylineSortOrder
    {
        TopToBottom,     // Từ trên xuống dưới (Y giảm dần) - Mặc định
        BottomToTop,     // Từ dưới lên trên (Y tăng dần)
        LeftToRight,     // Từ trái sang phải (X tăng dần)
        RightToLeft,     // Từ phải sang trái (X giảm dần)
        NoSort           // Giữ nguyên thứ tự chọn
    }

    /// <summary>
    /// Class chứa thông tin về tỉ lệ từ bản vẽ
    /// </summary>
    public class ScaleInfo
    {
        public string Name { get; set; } = "";
        public double PaperUnits { get; set; }
        public double DrawingUnits { get; set; }
        public double ScaleValue => PaperUnits / DrawingUnits; // Ví dụ: 1:100 = 0.01
        
        public override string ToString()
        {
            return Name;
        }
    }

    /// <summary>
    /// Class chứa lệnh bố trí viewport theo polyline trong Model
    /// </summary>
    public class AT_BoTri_ViewPort_TheoHinh
    {
        /// <summary>
        /// Lấy danh sách tất cả tỉ lệ có sẵn trong bản vẽ
        /// </summary>
        private static List<ScaleInfo> GetDrawingScales(Database db)
        {
            List<ScaleInfo> scales = new();
            
            try
            {
                // Lấy ObjectContextManager để truy cập annotation scales
                ObjectContextManager ocm = db.ObjectContextManager;
                if (ocm != null)
                {
                    // Lấy collection các annotation scales
                    ObjectContextCollection occ = ocm.GetContextCollection("ACDB_ANNOTATIONSCALES");
                    if (occ != null)
                    {
                        foreach (ObjectContext oc in occ)
                        {
                            if (oc is AnnotationScale annoScale)
                            {
                                // Bỏ qua tỉ lệ từ file Xref (có chứa _XREF trong tên)
                                if (annoScale.Name.Contains("_XREF", StringComparison.OrdinalIgnoreCase))
                                    continue;
                                
                                scales.Add(new ScaleInfo
                                {
                                    Name = annoScale.Name,
                                    PaperUnits = annoScale.PaperUnits,
                                    DrawingUnits = annoScale.DrawingUnits
                                });
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n⚠️ Lỗi khi lấy danh sách tỉ lệ: {ex.Message}");
            }

            // Sắp xếp theo DrawingUnits
            scales = scales.OrderBy(s => s.DrawingUnits).ToList();
            
            return scales;
        }

        /// <summary>
        /// Lệnh chính: Bố trí viewport theo hình polyline trong Model (hỗ trợ nhiều polyline)
        /// </summary>
        [CommandMethod("AT_Bo_Tri_Viewport_Theo_Hinh")]
        public static void BoTri_ViewPort_TheoHinh()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            try
            {
                ed.WriteMessage("\n=== BỐ TRÍ VIEWPORT THEO HÌNH (Multiple) ===");

                // Kiểm tra xem đang ở Model space không
                if (db.TileMode == false)
                {
                    ed.WriteMessage("\n⚠️ Bạn đang ở Paper space. Vui lòng chuyển sang Model space để chọn polyline.");
                    ed.WriteMessage("\n   Gõ lệnh MODEL hoặc nhấn phím Tab để chuyển.");
                    return;
                }

                // Bước 1: Chọn nhiều polyline trong Model space
                ObjectIdCollection polylineIds = UserInput.GSelectionSetWithType(
                    "\n Chọn các Polyline trong Model space:", "LWPOLYLINE");
                
                if (polylineIds == null || polylineIds.Count == 0)
                {
                    ed.WriteMessage("\n❌ Không có polyline nào được chọn. Lệnh đã hủy.");
                    return;
                }

                // Kiểm tra và lọc polyline đóng
                List<PolylineInfo> polylineInfos = new();
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId id in polylineIds)
                    {
                        Polyline polyline = (Polyline)tr.GetObject(id, OpenMode.ForRead);
                        if (polyline.Closed)
                        {
                            var extents = polyline.GeometricExtents;
                            Point3d center = new Point3d(
                                (extents.MinPoint.X + extents.MaxPoint.X) / 2,
                                (extents.MinPoint.Y + extents.MaxPoint.Y) / 2, 0);
                            
                            Point3dCollection points = new();
                            for (int i = 0; i < polyline.NumberOfVertices; i++)
                            {
                                points.Add(polyline.GetPoint3dAt(i));
                            }

                            polylineInfos.Add(new PolylineInfo
                            {
                                Id = id,
                                Center = center,
                                TopLeft = new Point3d(extents.MinPoint.X, extents.MaxPoint.Y, 0), // Góc trái trên
                                Width = extents.MaxPoint.X - extents.MinPoint.X,
                                Height = extents.MaxPoint.Y - extents.MinPoint.Y,
                                Points = points
                            });
                        }
                    }
                    tr.Commit();
                }

                if (polylineInfos.Count == 0)
                {
                    ed.WriteMessage("\n⚠️ Không có polyline đóng nào được chọn. Vui lòng chọn polyline closed.");
                    return;
                }

                ed.WriteMessage($"\n📐 Đã chọn {polylineInfos.Count} polyline đóng.");

                // Bước 2: Lấy danh sách tỉ lệ từ bản vẽ
                List<ScaleInfo> drawingScales = GetDrawingScales(db);
                
                if (drawingScales.Count == 0)
                {
                    ed.WriteMessage("\n⚠️ Không tìm thấy tỉ lệ nào trong bản vẽ.");
                    return;
                }

                // Bước 3: Lấy danh sách Layout
                List<string> layouts = new();
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    DBDictionary layoutDict = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
                    foreach (DBDictionaryEntry entry in layoutDict)
                    {
                        if (entry.Key != "Model")
                        {
                            layouts.Add(entry.Key);
                        }
                    }
                    tr.Commit();
                }
                layouts.Sort(); // Sắp xếp tên layout

                // Bước 4: Hiển thị form nhập liệu
                ScaleInfo? selectedScale;
                ViewportArrangement arrangement;
                PolylineSortOrder sortOrder;
                string selectedLayoutName = "";

                using (var form = new ViewportSettingsForm(drawingScales, layouts))
                {
                    if (form.ShowDialog() != DialogResult.OK)
                    {
                        ed.WriteMessage("\n❌ Đã hủy lệnh.");
                        return;
                    }
                    
                    selectedScale = form.SelectedScale;
                    arrangement = form.Arrangement;
                    sortOrder = form.SortOrder;
                    selectedLayoutName = form.SelectedLayout;
                }
                
                if (selectedScale == null)
                {
                    ed.WriteMessage("\n❌ Không chọn được tỉ lệ. Lệnh đã hủy.");
                    return;
                }
                
                double customScale = selectedScale.ScaleValue;
                ed.WriteMessage($"\n✅ Tỉ lệ đã chọn: {selectedScale.Name}");
                ed.WriteMessage($"\n📏 Hướng bố trí: {arrangement}");
                ed.WriteMessage($"\n📋 Sắp xếp: {sortOrder}");
                ed.WriteMessage($"\n📑 Layout đích: {selectedLayoutName}");

                // Bước 5: Sắp xếp polyline (nếu có nhiều)
                if (polylineInfos.Count > 1)
                {
                    polylineInfos = SortPolylines(polylineInfos, sortOrder);
                    
                    // Hiển thị thứ tự
                    for (int i = 0; i < polylineInfos.Count; i++)
                    {
                        ed.WriteMessage($"\n   {i + 1}. Polyline tại Y={polylineInfos[i].Center.Y:F0}, X={polylineInfos[i].Center.X:F0}");
                    }
                }

                // Bước 6: Khoảng cách mặc định
                double spacing = 10.0;

                // Bước 7: Chuyển sang Layout
                ed.WriteMessage($"\n\n📋 Chuyển sang Layout {selectedLayoutName} để đặt viewport...");
                
                LayoutManager layoutMgr = LayoutManager.Current;
                if (layoutMgr.CurrentLayout != selectedLayoutName)
                {
                    layoutMgr.CurrentLayout = selectedLayoutName;
                    ed.WriteMessage($"\n📋 Đã chuyển sang Layout: {selectedLayoutName}");
                }

                doc.SendStringToExecute("REGEN ", false, false, false);
                System.Threading.Thread.Sleep(200);

                // Bước 8: Nhập khoảng cách bằng cách chọn 2 điểm trong Layout (nếu có nhiều polyline)
                if (polylineInfos.Count > 1)
                {
                    ed.WriteMessage("\n📏 Chọn 2 điểm trong Layout để xác định khoảng cách giữa các TopLeft viewport:");
                    
                    PromptPointOptions ppo1 = new("\n Chọn điểm thứ nhất:");
                    ppo1.AllowNone = false;
                    PromptPointResult ppr1 = ed.GetPoint(ppo1);
                    
                    if (ppr1.Status == PromptStatus.OK)
                    {
                        PromptPointOptions ppo2 = new("\n Chọn điểm thứ hai:");
                        ppo2.BasePoint = ppr1.Value;
                        ppo2.UseBasePoint = true;
                        ppo2.AllowNone = false;
                        PromptPointResult ppr2 = ed.GetPoint(ppo2);
                        
                        if (ppr2.Status == PromptStatus.OK)
                        {
                            // Tính khoảng cách giữa 2 điểm
                            spacing = ppr1.Value.DistanceTo(ppr2.Value);
                        }
                    }
                    ed.WriteMessage($"\n📏 Khoảng cách: {spacing:F2} units");
                }

                // Bước 9: Cho user chọn điểm đặt viewport đầu tiên (góc trái trên)
                PromptPointOptions ppo = new("\n Chọn góc trái trên của viewport đầu tiên trong Layout:");
                ppo.AllowNone = false;
                PromptPointResult ppr = ed.GetPoint(ppo);
                
                if (ppr.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n❌ Không có điểm nào được chọn. Lệnh đã hủy.");
                    return;
                }
                Point3d insertPoint = ppr.Value;

                // Bước 10: Tạo các viewport
                CreateMultipleViewports(db, ed, polylineInfos, insertPoint, customScale, arrangement, spacing);

                ed.WriteMessage($"\n\n✅ Đã tạo {polylineInfos.Count} viewport thành công!");
                ed.WriteMessage("\n   💡 Mẹo: Bạn có thể dùng lệnh VPCLIP để điều chỉnh boundary của viewport.");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n❌ Lỗi: {ex.Message}");
                ed.WriteMessage($"\n   Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Lấy tỉ lệ từ danh sách tỉ lệ có sẵn trong bản vẽ
        /// Sử dụng keywords số thứ tự để tránh xung đột prefix matching
        /// </summary>
        private static ScaleInfo? GetScaleFromDrawing(Editor ed, List<ScaleInfo> scales)
        {
            if (scales.Count == 0)
            {
                ed.WriteMessage("\n⚠️ Không tìm thấy tỉ lệ nào trong bản vẽ. Sử dụng tỉ lệ mặc định 1:100.");
                return new ScaleInfo { Name = "1:100", PaperUnits = 1, DrawingUnits = 100 };
            }

            // Tìm tỉ lệ mặc định (1:100 hoặc tương đương)
            int defaultIndex = scales.FindIndex(s => s.Name == "1:100");
            if (defaultIndex < 0) defaultIndex = scales.FindIndex(s => s.DrawingUnits == 100);
            if (defaultIndex < 0) defaultIndex = 0;
            
            ScaleInfo defaultScale = scales[defaultIndex];

            // Hiển thị danh sách tỉ lệ
            ed.WriteMessage("\n\n📏 Danh sách tỉ lệ có sẵn:");
            
            // Build keyword list với format: TL + mẫu số (padding 5 chữ số)
            // Ví dụ: TL00001 (1:1), TL00010 (1:10), TL00100 (1:100), TL00150 (1:150)
            List<string> keywordList = new();
            Dictionary<string, ScaleInfo> keywordToScale = new();
            
            for (int i = 0; i < scales.Count; i++)
            {
                // Dùng mẫu số với padding để tránh prefix matching
                int denominator = (int)scales[i].DrawingUnits;
                string keyword = $"TL{denominator:D5}"; // TL00100, TL00150, TL00200...
                keywordList.Add(keyword);
                keywordToScale[keyword] = scales[i];
                
                // Hiển thị mapping cho user
                ed.WriteMessage($"\n   {keyword} = {scales[i].Name}");
            }
            
            // Default keyword
            int defaultDenom = (int)defaultScale.DrawingUnits;
            string defaultKeyword = $"TL{defaultDenom:D5}";

            // Tạo prompt options với keywords
            string keywordDisplay = string.Join("/", keywordList);
            PromptKeywordOptions pko = new($"\n Chọn tỉ lệ [{keywordDisplay}] <{defaultKeyword}>:");
            
            foreach (var keyword in keywordList)
            {
                pko.Keywords.Add(keyword);
            }
            pko.Keywords.Default = defaultKeyword;
            pko.AllowNone = true;
            pko.AllowArbitraryInput = false;

            PromptResult pr = ed.GetKeywords(pko);
            
            if (pr.Status == PromptStatus.None || pr.Status == PromptStatus.Cancel)
            {
                return defaultScale;
            }
            else if (pr.Status == PromptStatus.OK)
            {
                string selectedKeyword = pr.StringResult;
                if (keywordToScale.TryGetValue(selectedKeyword, out ScaleInfo? selectedScale))
                {
                    return selectedScale;
                }
            }

            return defaultScale;
        }

        /// <summary>
        /// Tạo nhiều viewport từ danh sách polyline
        /// Điểm đặt viewport là góc trái trên của polyline
        /// Khoảng cách giữa các viewport là khoảng cách giữa các góc trái trên
        /// </summary>
        private static void CreateMultipleViewports(Database db, Editor ed,
            List<PolylineInfo> polylineInfos, Point3d startPoint, double customScale,
            ViewportArrangement arrangement, double spacing)
        {
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    LayoutManager layoutMgr = LayoutManager.Current;
                    Layout layout = (Layout)tr.GetObject(layoutMgr.GetLayoutId(layoutMgr.CurrentLayout), OpenMode.ForRead);
                    BlockTableRecord paperSpace = (BlockTableRecord)tr.GetObject(layout.BlockTableRecordId, OpenMode.ForWrite);

                    // Điểm TopLeft đầu tiên trên paper space
                    Point3d currentTopLeft = startPoint;

                    for (int i = 0; i < polylineInfos.Count; i++)
                    {
                        PolylineInfo info = polylineInfos[i];
                        
                        // Tính toán kích thước viewport trên Paper space
                        double paperWidth = info.Width * customScale;
                        double paperHeight = info.Height * customScale;

                        // Tính tâm viewport từ góc trái trên
                        // TopLeft -> Center: X + width/2, Y - height/2
                        Point3d viewportCenter = new Point3d(
                            currentTopLeft.X + paperWidth / 2,
                            currentTopLeft.Y - paperHeight / 2,
                            0);

                        // Tạo viewport mới
                        Viewport viewport = new()
                        {
                            CenterPoint = viewportCenter,
                            Width = paperWidth,
                            Height = paperHeight,
                            CustomScale = customScale,
                            ViewCenter = new Point2d(info.Center.X, info.Center.Y)
                        };

                        ObjectId viewportId = paperSpace.AppendEntity(viewport);
                        tr.AddNewlyCreatedDBObject(viewport, true);
                        viewport.On = true;

                        // Tạo polyline clipping boundary
                        // Sử dụng góc trái trên làm điểm tham chiếu
                        Polyline clipPolyline = new();
                        for (int j = 0; j < info.Points.Count; j++)
                        {
                            Point3d modelPoint = info.Points[j];
                            // Chuyển đổi từ Model space sang Paper space dựa trên TopLeft
                            double paperX = currentTopLeft.X + (modelPoint.X - info.TopLeft.X) * customScale;
                            double paperY = currentTopLeft.Y + (modelPoint.Y - info.TopLeft.Y) * customScale;
                            clipPolyline.AddVertexAt(j, new Point2d(paperX, paperY), 0, 0, 0);
                        }
                        clipPolyline.Closed = true;

                        ObjectId clipPolylineId = paperSpace.AppendEntity(clipPolyline);
                        tr.AddNewlyCreatedDBObject(clipPolyline, true);

                        viewport.NonRectClipEntityId = clipPolylineId;
                        viewport.NonRectClipOn = true;
                        viewport.Locked = true;

                        ed.WriteMessage($"\n   ✅ Viewport {i + 1}: TopLeft=({currentTopLeft.X:F2}, {currentTopLeft.Y:F2})");

                        // Tính vị trí TopLeft cho viewport tiếp theo
                        if (i < polylineInfos.Count - 1)
                        {
                            if (arrangement == ViewportArrangement.Horizontal)
                            {
                                // Di chuyển sang phải: TopLeft2.X = TopLeft1.X + spacing
                                currentTopLeft = new Point3d(
                                    currentTopLeft.X + spacing,
                                    currentTopLeft.Y,
                                    0);
                            }
                            else
                            {
                                // Di chuyển xuống dưới: TopLeft2.Y = TopLeft1.Y - spacing
                                currentTopLeft = new Point3d(
                                    currentTopLeft.X,
                                    currentTopLeft.Y - spacing,
                                    0);
                            }
                        }
                    }

                    tr.Commit();
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage($"\n❌ Lỗi khi tạo viewport: {ex.Message}");
                    tr.Abort();
                    throw;
                }
            }
        }

        /// <summary>
        /// Sắp xếp danh sách polyline theo tiêu chí được chọn
        /// </summary>
        private static List<PolylineInfo> SortPolylines(List<PolylineInfo> polylines, PolylineSortOrder sortOrder)
        {
            return sortOrder switch
            {
                PolylineSortOrder.TopToBottom => polylines.OrderByDescending(p => p.Center.Y).ToList(),
                PolylineSortOrder.BottomToTop => polylines.OrderBy(p => p.Center.Y).ToList(),
                PolylineSortOrder.LeftToRight => polylines.OrderBy(p => p.Center.X).ToList(),
                PolylineSortOrder.RightToLeft => polylines.OrderByDescending(p => p.Center.X).ToList(),
                PolylineSortOrder.NoSort => polylines,
                _ => polylines.OrderByDescending(p => p.Center.Y).ToList()
            };
        }

        /// <summary>
        /// Class lưu thông tin polyline
        /// </summary>
        private class PolylineInfo
        {
            public ObjectId Id { get; set; }
            public Point3d Center { get; set; }
            public Point3d TopLeft { get; set; }  // Góc trái trên (MinX, MaxY)
            public double Width { get; set; }
            public double Height { get; set; }
            public Point3dCollection Points { get; set; } = new();
        }
    }

    /// <summary>
    /// Form nhập liệu cho lệnh AT_BoTri_ViewPort_TheoHinh
    /// </summary>
    public class ViewportSettingsForm : Form
    {
        // Properties để trả về kết quả
        public ScaleInfo? SelectedScale { get; private set; }
        public ViewportArrangement Arrangement { get; private set; } = ViewportArrangement.Horizontal;
        public PolylineSortOrder SortOrder { get; private set; } = PolylineSortOrder.TopToBottom;
        public string SelectedLayout { get; private set; } = "";

        // Controls
        private ComboBox cmbScale = null!;
        private RadioButton rbHorizontal = null!;
        private RadioButton rbVertical = null!;
        private ComboBox cmbSortOrder = null!;
        private ComboBox cmbLayout = null!;
        private Button btnOK = null!;
        private Button btnCancel = null!;

        private List<ScaleInfo> _scales;
        private List<string> _layouts;

        public ViewportSettingsForm(List<ScaleInfo> scales, List<string> layouts)
        {
            _scales = scales;
            _layouts = layouts;
            InitializeComponent();
            LoadData();
        }

        private void InitializeComponent()
        {
            this.Text = "Bố Trí Viewport Theo Hình";
            this.Size = new Size(400, 320); // Tăng chiều cao để chứa thêm layout
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;

            int y = 20;
            int labelWidth = 120;
            int controlX = 140;
            int controlWidth = 220;

            // Label Tỉ lệ
            var lblScale = new Label
            {
                Text = "Tỉ lệ viewport:",
                Location = new Point(20, y + 3),
                Size = new Size(labelWidth, 23),
                AutoSize = false
            };

            // ComboBox Tỉ lệ
            cmbScale = new ComboBox
            {
                Location = new Point(controlX, y),
                Size = new Size(controlWidth, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            y += 40;

            // Label Layout
            var lblLayout = new Label
            {
                Text = "Layout đích:",
                Location = new Point(20, y + 3),
                Size = new Size(labelWidth, 23),
                AutoSize = false
            };

            // ComboBox Layout
            cmbLayout = new ComboBox
            {
                Location = new Point(controlX, y),
                Size = new Size(controlWidth, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            y += 40;

            // Label Hướng bố trí
            var lblArrangement = new Label
            {
                Text = "Hướng bố trí:",
                Location = new Point(20, y + 3),
                Size = new Size(labelWidth, 23),
                AutoSize = false
            };

            // Radio buttons cho hướng bố trí
            rbHorizontal = new RadioButton
            {
                Text = "Ngang (→)",
                Location = new Point(controlX, y),
                Size = new Size(100, 23),
                Checked = true
            };

            rbVertical = new RadioButton
            {
                Text = "Dọc (↓)",
                Location = new Point(controlX + 110, y),
                Size = new Size(100, 23)
            };

            y += 40;

            // Label Sắp xếp
            var lblSort = new Label
            {
                Text = "Sắp xếp polyline:",
                Location = new Point(20, y + 3),
                Size = new Size(labelWidth, 23),
                AutoSize = false
            };

            // ComboBox Sắp xếp
            cmbSortOrder = new ComboBox
            {
                Location = new Point(controlX, y),
                Size = new Size(controlWidth, 25),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbSortOrder.Items.AddRange(new object[]
            {
                "Từ trên xuống (↓)",
                "Từ dưới lên (↑)",
                "Từ trái sang phải (→)",
                "Từ phải sang trái (←)",
                "Không sắp xếp"
            });
            cmbSortOrder.SelectedIndex = 0;

            y += 50;

            // Buttons
            btnOK = new Button
            {
                Text = "OK",
                Location = new Point(100, y),
                Size = new Size(80, 30),
                DialogResult = DialogResult.OK
            };
            btnOK.Click += BtnOK_Click;

            btnCancel = new Button
            {
                Text = "Hủy",
                Location = new Point(200, y),
                Size = new Size(80, 30),
                DialogResult = DialogResult.Cancel
            };

            // Add controls
            this.Controls.AddRange(new Control[]
            {
                lblScale, cmbScale,
                lblLayout, cmbLayout,
                lblArrangement, rbHorizontal, rbVertical,
                lblSort, cmbSortOrder,
                btnOK, btnCancel
            });

            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;
        }

        private void LoadData()
        {
            // Load Scales
            cmbScale.Items.Clear();
            int defaultIndex = 0;
            for (int i = 0; i < _scales.Count; i++)
            {
                cmbScale.Items.Add(_scales[i].Name);
                if (_scales[i].Name == "1:100" || _scales[i].DrawingUnits == 100)
                {
                    defaultIndex = i;
                }
            }
            if (cmbScale.Items.Count > 0)
            {
                cmbScale.SelectedIndex = defaultIndex;
            }

            // Load Layouts
            cmbLayout.Items.Clear();
            foreach (var layout in _layouts)
            {
                cmbLayout.Items.Add(layout);
            }
            
            // Tìm layout hiện tại hoặc layout đầu tiên
            string currentLayout = Application.DocumentManager.MdiActiveDocument.Database.LayoutDictionaryId.Database.TileMode ? "" : LayoutManager.Current.CurrentLayout;
            int layoutIndex = _layouts.IndexOf(currentLayout);
            if (layoutIndex >= 0)
                cmbLayout.SelectedIndex = layoutIndex;
            else if (cmbLayout.Items.Count > 0)
                cmbLayout.SelectedIndex = 0;
        }

        private void BtnOK_Click(object? sender, EventArgs e)
        {
            // Lấy tỉ lệ đã chọn
            if (cmbScale.SelectedIndex >= 0 && cmbScale.SelectedIndex < _scales.Count)
            {
                SelectedScale = _scales[cmbScale.SelectedIndex];
            }

            // Lấy layout đã chọn
            if (cmbLayout.SelectedItem != null)
            {
                SelectedLayout = cmbLayout.SelectedItem.ToString() ?? "";
            }

            // Lấy hướng bố trí
            Arrangement = rbHorizontal.Checked ? ViewportArrangement.Horizontal : ViewportArrangement.Vertical;

            // Lấy thứ tự sắp xếp
            SortOrder = cmbSortOrder.SelectedIndex switch
            {
                0 => PolylineSortOrder.TopToBottom,
                1 => PolylineSortOrder.BottomToTop,
                2 => PolylineSortOrder.LeftToRight,
                3 => PolylineSortOrder.RightToLeft,
                4 => PolylineSortOrder.NoSort,
                _ => PolylineSortOrder.TopToBottom
            };

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}

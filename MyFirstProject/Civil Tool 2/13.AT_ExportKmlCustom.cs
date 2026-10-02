using System;
using System.IO;
using System.IO.Compression;
using System.Xml.Linq;
using System.Windows.Forms;
using System.Collections.Generic;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;
using MyFirstProject.Extensions;
using OSGeo.MapGuide;
using Autodesk.Gis.Map;
using Autodesk.Gis.Map.Platform;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Document = Autodesk.AutoCAD.ApplicationServices.Document;
using AcadEntity = Autodesk.AutoCAD.DatabaseServices.Entity;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;

[assembly: CommandClass(typeof(Civil3DCsharp.AT_ExportKmlCustom_Commands))]

namespace Civil3DCsharp
{
    // Form cấu hình thông số xuất KML
    public class ExportKmlSettingsForm : Form
    {
        public double StandardLineWidth { get; private set; } = 3.0;
        public double BlockLineWidth { get; private set; } = 1.5;
        public double TextLineWidth { get; private set; } = 2.0;
        public bool FormAccepted { get; private set; } = false;
        public bool OverrideTextColor { get; private set; } = false;
        public System.Drawing.Color SelectedTextColor { get; private set; } = System.Drawing.Color.Yellow;

        private NumericUpDown numStandard;
        private NumericUpDown numBlock;
        private NumericUpDown numText;
        private CheckBox chkOverrideColor;
        private Panel pnlColorPreview;
        private Button btnChooseColor;
        private Button btnOk;
        private Button btnCancel;

        public ExportKmlSettingsForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Cấu hình xuất Google Earth KML";
            this.Size = new System.Drawing.Size(320, 310);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowIcon = false;

            // GroupBox thiết lập độ dày nét
            GroupBox groupBox = new GroupBox();
            groupBox.Text = "Thiết lập độ dày nét vẽ";
            groupBox.Location = new System.Drawing.Point(12, 12);
            groupBox.Size = new System.Drawing.Size(280, 135);

            // Nét vẽ đối tượng thường
            System.Windows.Forms.Label lblStandard = new System.Windows.Forms.Label();
            lblStandard.Text = "Đường thông thường:";
            lblStandard.Location = new System.Drawing.Point(15, 28);
            lblStandard.Size = new System.Drawing.Size(120, 20);

            numStandard = new NumericUpDown();
            numStandard.DecimalPlaces = 1;
            numStandard.Minimum = 0.5M;
            numStandard.Maximum = 10.0M;
            numStandard.Value = 3.0M;
            numStandard.Increment = 0.5M;
            numStandard.Location = new System.Drawing.Point(150, 26);
            numStandard.Size = new System.Drawing.Size(80, 20);

            // Nét vẽ đối tượng trong Block/XRef
            System.Windows.Forms.Label lblBlock = new System.Windows.Forms.Label();
            lblBlock.Text = "Đường trong Block/XRef:";
            lblBlock.Location = new System.Drawing.Point(15, 63);
            lblBlock.Size = new System.Drawing.Size(130, 20);

            numBlock = new NumericUpDown();
            numBlock.DecimalPlaces = 1;
            numBlock.Minimum = 0.5M;
            numBlock.Maximum = 10.0M;
            numBlock.Value = 1.5M;
            numBlock.Increment = 0.5M;
            numBlock.Location = new System.Drawing.Point(150, 61);
            numBlock.Size = new System.Drawing.Size(80, 20);

            // Nét vẽ Text/Label
            System.Windows.Forms.Label lblText = new System.Windows.Forms.Label();
            lblText.Text = "Text / Label:";
            lblText.Location = new System.Drawing.Point(15, 98);
            lblText.Size = new System.Drawing.Size(130, 20);

            numText = new NumericUpDown();
            numText.DecimalPlaces = 1;
            numText.Minimum = 0.5M;
            numText.Maximum = 10.0M;
            numText.Value = 2.0M;
            numText.Increment = 0.5M;
            numText.Location = new System.Drawing.Point(150, 96);
            numText.Size = new System.Drawing.Size(80, 20);

            groupBox.Controls.Add(lblStandard);
            groupBox.Controls.Add(numStandard);
            groupBox.Controls.Add(lblBlock);
            groupBox.Controls.Add(numBlock);
            groupBox.Controls.Add(lblText);
            groupBox.Controls.Add(numText);

            // GroupBox thiết lập màu sắc chữ
            GroupBox groupColor = new GroupBox();
            groupColor.Text = "Màu sắc chữ / Label";
            groupColor.Location = new System.Drawing.Point(12, 155);
            groupColor.Size = new System.Drawing.Size(280, 65);

            chkOverrideColor = new CheckBox();
            chkOverrideColor.Text = "Ghi đè màu chữ";
            chkOverrideColor.Location = new System.Drawing.Point(15, 25);
            chkOverrideColor.Size = new System.Drawing.Size(120, 20);
            chkOverrideColor.Checked = false;
            chkOverrideColor.CheckedChanged += ChkOverrideColor_CheckedChanged;

            pnlColorPreview = new Panel();
            pnlColorPreview.Location = new System.Drawing.Point(145, 25);
            pnlColorPreview.Size = new System.Drawing.Size(30, 20);
            pnlColorPreview.BorderStyle = BorderStyle.FixedSingle;
            pnlColorPreview.BackColor = System.Drawing.Color.Yellow;
            pnlColorPreview.Enabled = false;
            pnlColorPreview.Cursor = Cursors.Hand;
            pnlColorPreview.Click += PnlColorPreview_Click;

            btnChooseColor = new Button();
            btnChooseColor.Text = "...";
            btnChooseColor.Location = new System.Drawing.Point(185, 23);
            btnChooseColor.Size = new System.Drawing.Size(35, 24);
            btnChooseColor.Enabled = false;
            btnChooseColor.Click += BtnChooseColor_Click;

            groupColor.Controls.Add(chkOverrideColor);
            groupColor.Controls.Add(pnlColorPreview);
            groupColor.Controls.Add(btnChooseColor);

            // Nút Đồng ý
            btnOk = new Button();
            btnOk.Text = "Đồng ý";
            btnOk.DialogResult = DialogResult.OK;
            btnOk.Location = new System.Drawing.Point(110, 235);
            btnOk.Size = new System.Drawing.Size(85, 28);
            btnOk.Click += BtnOk_Click;

            // Nút Hủy bỏ
            btnCancel = new Button();
            btnCancel.Text = "Hủy bỏ";
            btnCancel.DialogResult = DialogResult.Cancel;
            btnCancel.Location = new System.Drawing.Point(205, 235);
            btnCancel.Size = new System.Drawing.Size(85, 28);

            this.Controls.Add(groupBox);
            this.Controls.Add(groupColor);
            this.Controls.Add(btnOk);
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
        }

        private void ChkOverrideColor_CheckedChanged(object sender, EventArgs e)
        {
            pnlColorPreview.Enabled = chkOverrideColor.Checked;
            btnChooseColor.Enabled = chkOverrideColor.Checked;
        }

        private void PnlColorPreview_Click(object sender, EventArgs e)
        {
            ChooseColor();
        }

        private void BtnChooseColor_Click(object sender, EventArgs e)
        {
            ChooseColor();
        }

        private void ChooseColor()
        {
            using (ColorDialog cd = new ColorDialog())
            {
                cd.Color = pnlColorPreview.BackColor;
                if (cd.ShowDialog() == DialogResult.OK)
                {
                    pnlColorPreview.BackColor = cd.Color;
                    SelectedTextColor = cd.Color;
                }
            }
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            StandardLineWidth = (double)numStandard.Value;
            BlockLineWidth = (double)numBlock.Value;
            TextLineWidth = (double)numText.Value;
            OverrideTextColor = chkOverrideColor.Checked;
            SelectedTextColor = pnlColorPreview.BackColor;
            FormAccepted = true;
            this.Close();
        }
    }

    public partial class AT_ExportKmlCustom_Commands
    {
        [CommandMethod("AT_ExportKmlCustom")]
        public static void AT_ExportKmlCustom()
        {
            Document doc = A.Doc;
            Database db = A.Db;
            Editor ed = A.Ed;

            try
            {
                // 1. Kiểm tra và lấy hệ tọa độ của bản vẽ
                AcMapMap map = null;
                try
                {
                    map = AcMapMap.GetCurrentMap();
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage($"\n[Lỗi] Không thể truy cập Map 3D engine: {ex.Message}");
                    return;
                }

                if (map == null)
                {
                    ed.WriteMessage("\n[Lỗi] Không tìm thấy bản đồ Map 3D đang hoạt động.");
                    return;
                }

                string sourceSrsWkt = map.GetMapSRS();
                if (string.IsNullOrEmpty(sourceSrsWkt) || sourceSrsWkt.Equals("Unitless", StringComparison.OrdinalIgnoreCase))
                {
                    ed.WriteMessage("\n[Cảnh báo] Bản vẽ chưa được gán Hệ tọa độ (Sử dụng lệnh MAPCSASSIGN). Vui lòng gán hệ tọa độ trước khi xuất sang KML.");
                    return;
                }

                // Hiển thị Settings Form đầu tiên
                double standardWidth = 3.0;
                double blockWidth = 1.5;
                double textWidth = 2.0;
                bool overrideTextColor = false;
                string customTextColorHex = "";

                using (ExportKmlSettingsForm settingsForm = new ExportKmlSettingsForm())
                {
                    DialogResult settingsResult = Application.ShowModalDialog(settingsForm);
                    if (settingsResult != DialogResult.OK || !settingsForm.FormAccepted)
                    {
                        ed.WriteMessage("\nĐã hủy lệnh.");
                        return;
                    }
                    standardWidth = settingsForm.StandardLineWidth;
                    blockWidth = settingsForm.BlockLineWidth;
                    textWidth = settingsForm.TextLineWidth;
                    overrideTextColor = settingsForm.OverrideTextColor;
                    if (overrideTextColor)
                    {
                        var col = settingsForm.SelectedTextColor;
                        customTextColorHex = $"ff{col.B:x2}{col.G:x2}{col.R:x2}";
                    }
                }

                string displaySrs = sourceSrsWkt;
                try
                {
                    MgCoordinateSystemFactory tempFactory = new MgCoordinateSystemFactory();
                    try
                    {
                        string csCode = tempFactory.ConvertWktToCoordinateSystemCode(sourceSrsWkt);
                        if (!string.IsNullOrEmpty(csCode))
                        {
                            displaySrs = csCode;
                        }
                    }
                    finally
                    {
                        tempFactory.Dispose();
                    }
                }
                catch { }

                ed.WriteMessage($"\nHệ tọa độ bản vẽ nhận diện được: {displaySrs}");

                // 2. Cho người dùng chọn các đối tượng cần xuất
                PromptSelectionOptions selOpts = new PromptSelectionOptions();
                selOpts.MessageForAdding = "\nChọn các đối tượng (Curve, Polyline, Hatch, CogoPoint, Block, XRef, Text, MText, Civil 3D Label) để xuất sang Google Earth KML: ";
                PromptSelectionResult selRes = ed.GetSelection(selOpts);

                if (selRes.Status != PromptStatus.OK || selRes.Value == null)
                {
                    ed.WriteMessage("\nĐã hủy chọn đối tượng.");
                    return;
                }

                // 3. Hỏi đường dẫn lưu file KMZ / KML
                string filePath = string.Empty;
                using (SaveFileDialog sfd = new SaveFileDialog())
                {
                    sfd.Filter = "KMZ Files (*.kmz)|*.kmz|KML Files (*.kml)|*.kml";
                    sfd.Title = "Lưu file KMZ hoặc KML";
                    sfd.FileName = Path.GetFileNameWithoutExtension(doc.Name) + "_GoogleEarth.kmz";
                    if (sfd.ShowDialog() == DialogResult.OK)
                    {
                        filePath = sfd.FileName;
                    }
                }

                if (string.IsNullOrEmpty(filePath))
                {
                    ed.WriteMessage("\nĐã hủy lưu file.");
                    return;
                }

                // Thu thập danh sách ObjectIds được chọn
                ObjectIdCollection selectedIds = new ObjectIdCollection();
                foreach (SelectedObject selObj in selRes.Value)
                {
                    if (selObj != null) selectedIds.Add(selObj.ObjectId);
                }

                // Thu thập các ObjectIds của chữ độc lập hoặc nằm trong block, nhãn Civil 3D
                HashSet<ObjectId> textIdsToProcess = new HashSet<ObjectId>();
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    CollectTextIds(tr, selectedIds, textIdsToProcess);
                    tr.Commit();
                }

                // Thực hiện sinh trước nét vẽ chữ và trích xuất nhãn Civil 3D (ở ngoài transaction chính)
                Dictionary<ObjectId, List<List<Point3d>>> textOutlinesCache = new Dictionary<ObjectId, List<List<Point3d>>>();
                Dictionary<ObjectId, string> labelTextsCache = new Dictionary<ObjectId, string>();
                Dictionary<ObjectId, Point3d> labelLocationsCache = new Dictionary<ObjectId, Point3d>();
                Dictionary<ObjectId, List<List<Point3d>>> labelLeadersCache = new Dictionary<ObjectId, List<List<Point3d>>>();
                Dictionary<ObjectId, string> blockColorCache = new Dictionary<ObjectId, string>();

                if (textIdsToProcess.Count > 0)
                {
                    ed.WriteMessage($"\nĐang tiền xử lý {textIdsToProcess.Count} đối tượng chữ/nhãn Civil 3D để xuất...");
                    foreach (ObjectId textId in textIdsToProcess)
                    {
                        bool isLabel = false;
                        using (Transaction tempTr = db.TransactionManager.StartTransaction())
                        {
                            DBObject obj = tempTr.GetObject(textId, OpenMode.ForRead);
                            if (obj is Autodesk.Civil.DatabaseServices.Label civLabel)
                            {
                                isLabel = true;
                                ProcessCivilLabel(civLabel, tempTr, out string labelText, out Point3d labelLoc, out Point3d anchorLoc, out List<List<Point3d>> leaderGeom);
                                labelTextsCache[textId] = labelText;
                                labelLocationsCache[textId] = labelLoc;
                                labelLeadersCache[textId] = leaderGeom;
                                ed.WriteMessage($"\n  [Civil 3D Label] '{labelText}' ({leaderGeom.Count} đoạn nét leader)");
                            }
                            tempTr.Commit();
                        }

                        if (!isLabel)
                        {
                            List<Curve> outlines = GetTextOutlines(doc, textId);
                            List<List<Point3d>> cachedPoints = new List<List<Point3d>>();
                            
                            using (Transaction tempTr = db.TransactionManager.StartTransaction())
                            {
                                foreach (Curve textCurve in outlines)
                                {
                                    List<Point3d> points = GetPointsFromCurve(textCurve, tempTr);
                                    if (points.Count > 0)
                                    {
                                        cachedPoints.Add(points);
                                    }
                                    textCurve.Dispose();
                                }
                                tempTr.Commit();
                            }
                            textOutlinesCache[textId] = cachedPoints;
                        }
                    }
                }

                // Tiền xử lý BlockReference: Nổ database-resident (giống lệnh EXPLODE thủ công)
                // (Áp dụng cho General Note Label và các Block có chứa Text)
                foreach (SelectedObject selObj in selRes.Value)
                {
                    if (selObj == null) continue;
                    ObjectId selId = selObj.ObjectId;

                    bool isBlockRef = false;
                    string blockColor = null;
                    string blockName = "";
                    using (Transaction tempTr = db.TransactionManager.StartTransaction())
                    {
                        AcadEntity ent = tempTr.GetObject(selId, OpenMode.ForRead) as AcadEntity;
                        isBlockRef = (ent is BlockReference) && !(ent is Autodesk.Civil.DatabaseServices.Label);
                        if (isBlockRef)
                        {
                            blockColor = GetKmlColorString(ent, tempTr);
                            blockName = ((BlockReference)ent).Name;
                            ed.WriteMessage($"\n[DEBUG] Block pre-cache: Name={blockName} Type={ent.GetType().FullName}");
                        }
                        tempTr.Commit();
                    }

                    if (!isBlockRef) continue;
                    if (textOutlinesCache.ContainsKey(selId)) continue;

                    // Nổ block nhiều cấp bằng cách thêm kết quả vào DB sau mỗi lần nổ
                    // (giống thao tác thủ công: EXPLODE → chọn kết quả → EXPLODE lại)
                    List<ObjectId> currentLevelIds = new List<ObjectId> { selId };
                    List<ObjectId> allTempIds = new List<ObjectId>(); // Tất cả entity tạm cần xóa

                    try
                    {
                        int maxDepth = 5; // Giới hạn độ sâu nổ
                        for (int depth = 0; depth < maxDepth; depth++)
                        {
                            List<ObjectId> nextLevelIds = new List<ObjectId>();
                            bool hasBlocksToExplode = false;

                            foreach (ObjectId currentId in currentLevelIds)
                            {
                                using (Transaction tempTr = db.TransactionManager.StartTransaction())
                                {
                                    AcadEntity currentEnt = tempTr.GetObject(currentId, OpenMode.ForRead) as AcadEntity;
                                    if (currentEnt == null || !(currentEnt is BlockReference currentBlockRef))
                                    {
                                        nextLevelIds.Add(currentId); // Giữ nguyên primitive
                                        tempTr.Commit();
                                        continue;
                                    }

                                    DBObjectCollection subCol = new DBObjectCollection();
                                    bool explodeOk = false;
                                    try
                                    {
                                        currentBlockRef.Explode(subCol);
                                        explodeOk = true;
                                        ed.WriteMessage($"\n[DEBUG] Nổ cấp {depth + 1}: {subCol.Count} kết quả");
                                        foreach (DBObject subDbg in subCol)
                                        {
                                            ed.WriteMessage($"\n[DEBUG]   -> {subDbg.GetType().FullName}");
                                        }
                                    }
                                    catch (System.Exception ex)
                                    {
                                        ed.WriteMessage($"\n[Cảnh báo] Lỗi nổ Block cấp {depth + 1}: {ex.Message}");
                                    }

                                    if (explodeOk && subCol.Count > 0)
                                    {
                                        BlockTableRecord btr = (BlockTableRecord)tempTr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                                        foreach (DBObject subObj in subCol)
                                        {
                                            if (subObj is AcadEntity subEnt)
                                            {
                                                btr.AppendEntity(subEnt);
                                                tempTr.AddNewlyCreatedDBObject(subEnt, true);
                                                nextLevelIds.Add(subEnt.ObjectId);
                                                if (currentId != selId) // Không xóa entity gốc đã chọn
                                                {
                                                    // Entity cấp trung gian sẽ được xóa sau
                                                }
                                                allTempIds.Add(subEnt.ObjectId);
                                                if (subEnt is BlockReference)
                                                {
                                                    hasBlocksToExplode = true;
                                                }
                                            }
                                            else
                                            {
                                                subObj.Dispose();
                                            }
                                        }
                                    }
                                    else
                                    {
                                        nextLevelIds.Add(currentId); // Không nổ được, giữ nguyên
                                    }

                                    tempTr.Commit();
                                }
                            }

                            currentLevelIds = nextLevelIds;
                            if (!hasBlocksToExplode) break;
                        }

                        // Thu thập kết quả cuối cùng (primitives)
                        List<List<Point3d>> blockGeometry = new List<List<Point3d>>();
                        List<ObjectId> tempBlockTextIds = new List<ObjectId>();
                        System.Text.StringBuilder blockTextAccumulator = new System.Text.StringBuilder();
                        bool hasAnyText = false;
                        string resolvedColor = blockColor;

                        using (Transaction tempTr = db.TransactionManager.StartTransaction())
                        {
                            foreach (ObjectId primId in currentLevelIds)
                            {
                                if (primId == selId) continue; // Bỏ qua entity gốc

                                AcadEntity primEnt = null;
                                try { primEnt = tempTr.GetObject(primId, OpenMode.ForRead) as AcadEntity; }
                                catch { continue; }
                                if (primEnt == null) continue;

                                if (primEnt is Curve curve)
                                {
                                    List<Point3d> pts = GetPointsFromCurve(curve, tempTr);
                                    if (pts.Count > 0) blockGeometry.Add(pts);
                                    // Lấy màu từ entity con (ưu tiên entity có màu thực tế)
                                    if (resolvedColor == null || resolvedColor == "ffffffff")
                                    {
                                        string entColor = GetKmlColorString(primEnt, tempTr);
                                        if (entColor != "ffffffff") resolvedColor = entColor;
                                    }
                                }
                                else if (primEnt is Hatch hatch)
                                {
                                    List<List<Point3d>> hLoops = GetPointsFromHatch(hatch);
                                    if (hLoops != null && hLoops.Count > 0)
                                    {
                                        blockGeometry.AddRange(hLoops);
                                        if (resolvedColor == null || resolvedColor == "ffffffff")
                                        {
                                            string entColor = GetKmlColorString(primEnt, tempTr);
                                            if (entColor != "ffffffff") resolvedColor = entColor;
                                        }
                                    }
                                }
                                else if (primEnt is Leader leader)
                                {
                                    List<Point3d> pts = new List<Point3d>();
                                    int count = leader.NumVertices;
                                    for (int i = 0; i < count; i++) pts.Add(leader.VertexAt(i));
                                    if (pts.Count > 0) blockGeometry.Add(pts);
                                }
                                else if (primEnt is MLeader mLeader)
                                {
                                    foreach (int ldrIdx in mLeader.GetLeaderIndexes())
                                    {
                                        foreach (int lineIdx in mLeader.GetLeaderLineIndexes(ldrIdx))
                                        {
                                            List<Point3d> pts = GetMLeaderVertices(mLeader, lineIdx);
                                            if (pts.Count > 0) blockGeometry.Add(pts);
                                        }
                                    }
                                }
                                else if (primEnt is DBText dbT)
                                {
                                    string txt = dbT.TextString;
                                    if (!string.IsNullOrWhiteSpace(txt))
                                    {
                                        blockTextAccumulator.Append(txt + " ");
                                        tempBlockTextIds.Add(primId);
                                        hasAnyText = true;
                                    }
                                }
                                else if (primEnt is MText mT)
                                {
                                    string txt = mT.Text;
                                    if (!string.IsNullOrWhiteSpace(txt))
                                    {
                                        blockTextAccumulator.Append(txt + " ");
                                        try { mT.UpgradeOpen(); mT.BackgroundFill = false; mT.DowngradeOpen(); } catch {}
                                        tempBlockTextIds.Add(primId);
                                        hasAnyText = true;
                                    }
                                }
                            }
                            tempTr.Commit();
                        }

                        if (!hasAnyText && blockGeometry.Count == 0)
                        {
                            // Không có gì để cache, xóa temp và bỏ qua
                        }
                        else
                        {
                            if (tempBlockTextIds.Count > 0)
                            {
                                ed.WriteMessage($"\nĐang sinh nét vẽ chữ cho Block (chứa {tempBlockTextIds.Count} đối tượng text)...");
                            }

                            foreach (ObjectId tempTextId in tempBlockTextIds)
                            {
                                List<Curve> outlines = GetTextOutlines(doc, tempTextId);
                                using (Transaction tempTr = db.TransactionManager.StartTransaction())
                                {
                                    foreach (Curve textCurve in outlines)
                                    {
                                        List<Point3d> points = GetPointsFromCurve(textCurve, tempTr);
                                        if (points.Count > 0) blockGeometry.Add(points);
                                        textCurve.Dispose();
                                    }
                                    tempTr.Commit();
                                }
                            }

                            if (blockGeometry.Count > 0)
                            {
                                textOutlinesCache[selId] = blockGeometry;
                                string labelText = blockTextAccumulator.ToString().Trim();
                                if (!string.IsNullOrEmpty(labelText))
                                {
                                    labelTextsCache[selId] = labelText;
                                }
                                // Lưu màu đã phân giải
                                if (!string.IsNullOrEmpty(resolvedColor) && resolvedColor != "ffffffff")
                                {
                                    blockColorCache[selId] = resolvedColor;
                                }
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        ed.WriteMessage($"\n[Cảnh báo] Lỗi xử lý Block: {ex.Message}");
                    }
                    finally
                    {
                        // Xóa tất cả entity tạm
                        using (Transaction tempTr = db.TransactionManager.StartTransaction())
                        {
                            foreach (ObjectId tempId in allTempIds)
                            {
                                try
                                {
                                    AcadEntity tempEnt = tempTr.GetObject(tempId, OpenMode.ForWrite) as AcadEntity;
                                    if (tempEnt != null && !tempEnt.IsErased) tempEnt.Erase();
                                }
                                catch {}
                            }
                            tempTr.Commit();
                        }
                    }
                }


                // 4. Tạo cấu trúc KML sử dụng LINQ to XML
                XNamespace ns = "http://www.opengis.net/kml/2.2";

                XElement documentNode = new XElement(ns + "Document",
                    new XElement(ns + "name", Path.GetFileNameWithoutExtension(filePath)),
                    new XElement(ns + "description", $"Xuất từ AutoCAD Civil 3D. Hệ tọa độ gốc: {displaySrs}")
                );

                XElement kmlRoot = new XElement(ns + "kml",
                    new XAttribute("xmlns", ns.NamespaceName),
                    documentNode
                );

                int successLines = 0;
                int successPoints = 0;
                int failed = 0;

                // Khởi tạo Factory chuyển đổi tọa độ và Geometry Factory
                MgCoordinateSystemFactory factory = new MgCoordinateSystemFactory();
                MgGeometryFactory geoFactory = new MgGeometryFactory();
                MgCoordinateSystem sourceSys = null;
                MgCoordinateSystem targetSys = null;
                MgCoordinateSystemTransform transform = null;

                try
                {
                    sourceSys = CreateCoordinateSystem(factory, sourceSrsWkt);
                    targetSys = factory.CreateFromCode("LL84"); // LL84 đại diện cho WGS84
                    transform = factory.GetTransform(sourceSys, targetSys);

                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        foreach (SelectedObject selObj in selRes.Value)
                        {
                            AcadEntity ent = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as AcadEntity;
                            if (ent == null) continue;

                            // Xử lý CogoPoint (Civil 3D)
                            if (ent is CogoPoint cogoPoint)
                            {
                                Point3d pt = cogoPoint.Location;
                                Point3d? wgsPt = TransformPoint(pt, transform, geoFactory);
                                if (wgsPt.HasValue)
                                {
                                    string name = cogoPoint.PointName;
                                    if (string.IsNullOrEmpty(name))
                                    {
                                        name = $"Point_{cogoPoint.PointNumber}";
                                    }

                                    string colorHexPoint = GetKmlColorString(cogoPoint, tr);
                                    XElement placemark = new XElement(ns + "Placemark",
                                        new XElement(ns + "name", name),
                                        new XElement(ns + "description", $"CogoPoint #{cogoPoint.PointNumber}\nMô tả: {cogoPoint.RawDescription ?? ""}"),
                                        new XElement(ns + "Style",
                                            new XElement(ns + "IconStyle",
                                                new XElement(ns + "color", colorHexPoint),
                                                new XElement(ns + "scale", "1.2")
                                            )
                                        ),
                                        new XElement(ns + "Point",
                                            new XElement(ns + "coordinates", $"{wgsPt.Value.X:F8},{wgsPt.Value.Y:F8},{wgsPt.Value.Z:F3}")
                                        )
                                    );
                                    documentNode.Add(placemark);
                                    successPoints++;
                                }
                                else
                                {
                                    failed++;
                                }
                            }
                            // *** Xử lý Civil 3D Label (PHẢI đứng TRƯỚC Curve vì Label kế thừa từ Curve) ***
                            else if (ent is Autodesk.Civil.DatabaseServices.Label civLabel)
                            {
                                string colorHexLabel = overrideTextColor ? customTextColorHex : GetKmlColorString(civLabel, tr);
                                textOutlinesCache.TryGetValue(civLabel.ObjectId, out List<List<Point3d>> cachedPoints);
                                labelTextsCache.TryGetValue(civLabel.ObjectId, out string labelText);
                                if (string.IsNullOrEmpty(labelText))
                                {
                                    labelText = $"Label_{civLabel.Handle}";
                                }

                                Point3d labelLoc = Point3d.Origin;
                                if (!labelLocationsCache.TryGetValue(civLabel.ObjectId, out labelLoc))
                                {
                                    try { labelLoc = civLabel.LabelLocation; }
                                    catch
                                    {
                                        try { labelLoc = civLabel.GeometricExtents.MinPoint; } catch { }
                                    }
                                }

                                labelLeadersCache.TryGetValue(civLabel.ObjectId, out List<List<Point3d>> leaderGeom);

                                ExportCivilLabel(
                                    civLabel,
                                    labelText,
                                    labelLoc,
                                    leaderGeom,
                                    cachedPoints,
                                    colorHexLabel,
                                    ns,
                                    documentNode,
                                    transform,
                                    geoFactory,
                                    ref successLines,
                                    ref successPoints,
                                    ref failed,
                                    textWidth);
                            }
                            // Xử lý riêng cho Alignment (Civil 3D Alignment không kế thừa từ Curve)
                            else if (ent is Alignment alignment)
                            {
                                List<Point3d> points = GetPointsFromAlignment(alignment, tr);
                                string colorHexCurve = GetKmlColorString(alignment, tr);
                                // Kiểm tra nếu màu mặc định bị trắng/trống thì gán màu đỏ nổi bật (KML hex: ff0000ff -> Red)
                                if (string.IsNullOrEmpty(colorHexCurve) || colorHexCurve == "ffffffff" || colorHexCurve == "ff000000")
                                {
                                    colorHexCurve = "ff0000ff"; // Đỏ tươi nổi bật trên bản đồ Google Earth
                                }

                                string alignName = alignment.Name;
                                if (string.IsNullOrEmpty(alignName)) alignName = $"Alignment_{alignment.Handle}";

                                if (points.Count > 0)
                                {
                                    System.Text.StringBuilder coordStr = new System.Text.StringBuilder();
                                    int validCoords = 0;

                                    foreach (Point3d pt in points)
                                    {
                                        Point3d? wgsPt = TransformPoint(pt, transform, geoFactory);
                                        if (wgsPt.HasValue)
                                        {
                                            coordStr.Append($"{wgsPt.Value.X:F8},{wgsPt.Value.Y:F8},{wgsPt.Value.Z:F3} ");
                                            validCoords++;
                                        }
                                    }

                                    if (validCoords > 0)
                                    {
                                        XElement placemark = new XElement(ns + "Placemark",
                                            new XElement(ns + "name", alignName),
                                            new XElement(ns + "Style",
                                                new XElement(ns + "LineStyle",
                                                    new XElement(ns + "color", colorHexCurve),
                                                    new XElement(ns + "width", standardWidth.ToString("F1"))
                                                )
                                            ),
                                            new XElement(ns + "LineString",
                                                new XElement(ns + "tessellate", "1"),
                                                new XElement(ns + "coordinates", coordStr.ToString().Trim())
                                            )
                                        );
                                        documentNode.Add(placemark);
                                        successLines++;
                                        ed.WriteMessage($"\n  [Thành công] Đã xuất Alignment '{alignName}': {validCoords} điểm tọa độ.");
                                    }
                                    else
                                    {
                                        failed++;
                                    }
                                }
                                else
                                {
                                    failed++;
                                }
                            }
                            // Xử lý các đối tượng Curve (Line, Polyline, Arc, Circle...)
                            // Lưu ý: Label đã được xử lý ở trên, không vào nhánh này
                            else if (ent is Curve curve)
                            {
                                List<Point3d> points = GetPointsFromCurve(curve, tr);
                                string colorHexCurve = GetKmlColorString(curve, tr);

                                if (points.Count > 0)
                                {
                                    System.Text.StringBuilder coordStr = new System.Text.StringBuilder();
                                    int validCoords = 0;

                                    foreach (Point3d pt in points)
                                    {
                                        Point3d? wgsPt = TransformPoint(pt, transform, geoFactory);
                                        if (wgsPt.HasValue)
                                        {
                                            coordStr.Append($"{wgsPt.Value.X:F8},{wgsPt.Value.Y:F8},{wgsPt.Value.Z:F3} ");
                                            validCoords++;
                                        }
                                    }

                                    if (validCoords > 0)
                                    {
                                        XElement placemark = new XElement(ns + "Placemark",
                                            new XElement(ns + "name", $"{ent.GetType().Name}_{ent.Handle}"),
                                            new XElement(ns + "Style",
                                                new XElement(ns + "LineStyle",
                                                    new XElement(ns + "color", colorHexCurve),
                                                    new XElement(ns + "width", standardWidth.ToString("F1"))
                                                )
                                            ),
                                            new XElement(ns + "LineString",
                                                new XElement(ns + "tessellate", "1"),
                                                new XElement(ns + "coordinates", coordStr.ToString().Trim())
                                            )
                                        );
                                        documentNode.Add(placemark);
                                        successLines++;
                                    }
                                    else
                                    {
                                        failed++;
                                    }
                                }
                                else
                                {
                                    failed++;
                                }
                            }
                            // Xử lý đối tượng Hatch (Mặt cắt / Vùng tô)
                            else if (ent is Hatch hatch)
                            {
                                string colorHexHatch = GetKmlColorString(hatch, tr);
                                string hatchName = $"{hatch.PatternName}_{hatch.Handle}";
                                if (string.IsNullOrEmpty(hatch.PatternName))
                                {
                                    hatchName = $"Hatch_{hatch.Handle}";
                                }
                                ExportHatch(hatch, hatchName, colorHexHatch, ns, documentNode, transform, geoFactory, ref successLines, ref failed, standardWidth);
                            }
                            // Xử lý đối tượng Leader
                            else if (ent is Leader leader)
                            {
                                List<Point3d> points = new List<Point3d>();
                                int count = leader.NumVertices;
                                for (int i = 0; i < count; i++)
                                {
                                    points.Add(leader.VertexAt(i));
                                }
                                string colorHex = GetKmlColorString(leader, tr);
                                ExportGeometryList(points, $"{ent.GetType().Name}_{ent.Handle}", colorHex, ns, documentNode, transform, geoFactory, ref successLines, ref failed, standardWidth);
                            }
                            // Xử lý đối tượng MLeader
                            else if (ent is MLeader mLeader)
                            {
                                string colorHex = GetKmlColorString(mLeader, tr);
                                foreach (int ldrIdx in mLeader.GetLeaderIndexes())
                                {
                                    foreach (int lineIdx in mLeader.GetLeaderLineIndexes(ldrIdx))
                                    {
                                        List<Point3d> points = GetMLeaderVertices(mLeader, lineIdx);
                                        ExportGeometryList(points, $"{ent.GetType().Name}_{ent.Handle}_{ldrIdx}_{lineIdx}", colorHex, ns, documentNode, transform, geoFactory, ref successLines, ref failed, standardWidth);
                                    }
                                }
                            }
                            // Xử lý DBText
                            else if (ent is DBText dbText)
                            {
                                string colorHexText = overrideTextColor ? customTextColorHex : GetKmlColorString(dbText, tr);
                                textOutlinesCache.TryGetValue(dbText.ObjectId, out List<List<Point3d>> cachedPoints);
                                ExportTextAsVector(cachedPoints, dbText.TextString, colorHexText, ns, documentNode, transform, geoFactory, ref successLines, ref failed, textWidth, fallbackPoint: dbText.Position);
                            }
                            // Xử lý MText
                            else if (ent is MText mText)
                            {
                                string colorHexText = overrideTextColor ? customTextColorHex : GetKmlColorString(mText, tr);
                                string txtStr = mText.Text;
                                if (string.IsNullOrEmpty(txtStr))
                                {
                                    txtStr = mText.Contents;
                                }
                                string cleanTxt = StripMTextFormatting(txtStr);
                                textOutlinesCache.TryGetValue(mText.ObjectId, out List<List<Point3d>> cachedPoints);
                                ExportTextAsVector(cachedPoints, cleanTxt, colorHexText, ns, documentNode, transform, geoFactory, ref successLines, ref failed, textWidth, fallbackPoint: mText.Location);
                            }
                            // Xử lý BlockReference (Bao gồm cả Block/XRef lồng nhau)
                            else if (ent is BlockReference blockRef)
                            {
                                ed.WriteMessage($"\n[DEBUG] Export BlockRef: Name={blockRef.Name} Handle={blockRef.Handle}");
                                bool hasCachedBlock = textOutlinesCache.TryGetValue(blockRef.ObjectId, out List<List<Point3d>> cachedBlockOutlines2);
                                ed.WriteMessage($"\n[DEBUG]   Cache found={hasCachedBlock} Count={(cachedBlockOutlines2?.Count ?? 0)}");
                                // Kiểm tra dữ liệu đã được tiền xử lý bằng phương pháp nổ block
                                if (textOutlinesCache.TryGetValue(blockRef.ObjectId, out List<List<Point3d>> cachedBlockOutlines) && cachedBlockOutlines != null && cachedBlockOutlines.Count > 0)
                                {
                                    // Ưu tiên màu đã phân giải từ entity con, fallback sang layer color
                                    string colorHex;
                                    if (!blockColorCache.TryGetValue(blockRef.ObjectId, out colorHex) || string.IsNullOrEmpty(colorHex))
                                    {
                                        colorHex = GetKmlColorString(blockRef, tr);
                                        if (colorHex == "ffffffff")
                                        {
                                            try
                                            {
                                                LayerTableRecord layer = (LayerTableRecord)tr.GetObject(blockRef.LayerId, OpenMode.ForRead);
                                                var layerColor = layer.Color.ColorValue;
                                                colorHex = $"ff{layerColor.B:x2}{layerColor.G:x2}{layerColor.R:x2}";
                                            }
                                            catch { colorHex = "ff00ffff"; } // Fallback yellow
                                        }
                                    }
                                    labelTextsCache.TryGetValue(blockRef.ObjectId, out string blockLabel);
                                    if (string.IsNullOrEmpty(blockLabel))
                                    {
                                        blockLabel = blockRef.Name;
                                    }
                                    ExportTextAsVector(cachedBlockOutlines, blockLabel, colorHex, ns, documentNode, transform, geoFactory, ref successLines, ref failed, blockWidth);
                                }
                                else
                                {
                                    int blockLinesExported = 0;
                                    HashSet<ObjectId> visitedBlocks = new HashSet<ObjectId>();
                                    ProcessBlockReference(doc, blockRef, tr, transform, geoFactory, ns, documentNode, ref blockLinesExported, ref failed, Matrix3d.Identity, blockWidth, textOutlinesCache, labelTextsCache, visitedBlocks);
                                    successLines += blockLinesExported;
                                }
                            }
                        }
                        tr.Commit();
                    }
                }
                finally
                {
                    if (transform != null) try { transform.Dispose(); } catch { }
                    if (sourceSys != null) try { sourceSys.Dispose(); } catch { }
                    if (targetSys != null) try { targetSys.Dispose(); } catch { }
                    if (factory != null) try { factory.Dispose(); } catch { }
                    if (geoFactory != null) try { geoFactory.Dispose(); } catch { }
                }

                // 5. Ghi file KMZ hoặc KML
                string extension = Path.GetExtension(filePath);
                if (extension.Equals(".kmz", StringComparison.OrdinalIgnoreCase))
                {
                    using (FileStream fs = new FileStream(filePath, FileMode.Create))
                    using (ZipArchive zip = new ZipArchive(fs, ZipArchiveMode.Create))
                    {
                        ZipArchiveEntry entry = zip.CreateEntry("doc.kml");
                        using (Stream entryStream = entry.Open())
                        {
                            kmlRoot.Save(entryStream);
                        }
                    }
                    ed.WriteMessage($"\n\n======================================");
                    ed.WriteMessage($"\n[Thành công] Đã xuất bản vẽ sang KMZ!");
                }
                else
                {
                    kmlRoot.Save(filePath);
                    ed.WriteMessage($"\n\n======================================");
                    ed.WriteMessage($"\n[Thành công] Đã xuất bản vẽ sang KML!");
                }

                ed.WriteMessage($"\n  - Đường dẫn: {filePath}");
                ed.WriteMessage($"\n  - Số đối tượng hình học xuất được: {successLines}");
                ed.WriteMessage($"\n  - Số điểm CogoPoint: {successPoints}");
                if (failed > 0)
                {
                    ed.WriteMessage($"\n  - Số đối tượng thất bại: {failed}");
                }
                ed.WriteMessage($"\n======================================");

                // Tự động mở file KML bằng ứng dụng mặc định (ví dụ Google Earth)
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true
                    });
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage($"\n[Cảnh báo] Không thể tự động mở file: {ex.Message}");
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n[Lỗi hệ thống] {ex.Message}");
            }
        }
    }
}

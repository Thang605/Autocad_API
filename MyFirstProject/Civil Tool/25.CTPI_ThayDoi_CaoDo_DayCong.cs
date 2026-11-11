using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using Autodesk.AutoCAD.Runtime;
using Acad = Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;
using ATable = Autodesk.AutoCAD.DatabaseServices.Table;

using Civil = Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;
using Autodesk.Civil.Runtime;
using Autodesk.Civil.Settings;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.Civil.ApplicationServices;
using CivSurface = Autodesk.Civil.DatabaseServices.Surface;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using MyFirstProject.Extensions;

// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(Civil3DCsharp.CTPi_ThayDoi_CaoDo_DayCong_Commands))]

namespace Civil3DCsharp
{
    public class CTPi_ThayDoi_CaoDo_DayCong_Commands
    {
        // Static variables to remember last input values
        private static double _lastStartInvertElevation = 10.0;
        private static double _lastSlope = 0.005; // Default 0.5%

        [CommandMethod("CT_Thay_Doi_Cao_Do_Day_Cong")]
        public static void CTPi_ThayDoi_CaoDo_DayCong()
        {
            // start transaction
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                
                // Get pipe selection - allow selection from both plan and profile view
                ObjectId pipeId = SelectPipeFromPlanOrProfile(tr);
                
                if (pipeId == ObjectId.Null)
                {
                    A.Ed.WriteMessage("\nKhông có ống cống nào được chọn.");
                    return;
                }

                // Get the pipe to analyze current conditions
                Pipe? pipe = tr.GetObject(pipeId, OpenMode.ForRead) as Pipe;
                if (pipe == null)
                {
                    A.Ed.WriteMessage("\nĐối tượng được chọn không phải là ống cống.");
                    return;
                }

                // Get pipe diameter in mm for slope calculation
                double diameterMm = pipe.InnerDiameterOrWidth * 1000; // Convert from m to mm
                
                // Calculate default slope using formula -1/D (where D is diameter in mm)
                double defaultSlope = -1.0 / diameterMm; // This gives negative decimal slope
                
                // Calculate current pipe information
                double currentStartInvertElevation = pipe.StartPoint.Z - (pipe.InnerDiameterOrWidth / 2);
                double currentEndInvertElevation = pipe.EndPoint.Z - (pipe.InnerDiameterOrWidth / 2);
                
                // Get start cover directly from pipe property
                double currentStartCover = pipe.CoverOfStartPoint;
                
                // Calculate end cover (if reference surface exists)
                double currentEndCover = 0;
                if (pipe.RefSurfaceId != ObjectId.Null)
                {
                    try
                    {
                        CivSurface? refSurface = tr.GetObject(pipe.RefSurfaceId, OpenMode.ForRead) as CivSurface;
                        if (refSurface != null)
                        {
                            // End cover
                            double groundElevationEnd = refSurface.FindElevationAtXY(pipe.EndPoint.X, pipe.EndPoint.Y);
                            double pipeTopElevationEnd = pipe.EndPoint.Z + (pipe.InnerDiameterOrWidth / 2);
                            currentEndCover = groundElevationEnd - pipeTopElevationEnd;
                        }
                    }
                    catch
                    {
                        // If can't get surface elevation, set cover to 0
                        currentEndCover = 0;
                    }
                }
                
                double currentSlope = 0;

                // Calculate current slope
                if (pipe.Length3DCenterToCenter > 0)
                {
                    currentSlope = (currentStartInvertElevation - currentEndInvertElevation) / pipe.Length3DCenterToCenter;
                }

                // Create and show the form
                using (var form = new PipeInvertElevationForm())
                {
                    // Set current pipe information with default slope
                    form.SetCurrentPipeInfo(
                        currentStartInvertElevation,
                        currentEndInvertElevation,
                        currentStartCover,
                        currentEndCover,
                        currentSlope,
                        pipe.Length3DCenterToCenter,
                        diameterMm,
                        defaultSlope,
                        _lastStartInvertElevation,
                        _lastSlope
                    );

                    DialogResult result = Application.ShowModalDialog(form);

                    if (result != DialogResult.OK)
                    {
                        A.Ed.WriteMessage("\nThao tác bị hủy bỏ.");
                        return;
                    }

                    // Get new values from form
                    double invertElevationChange = form.NewInvertElevationChange;
                    double newSlope = form.NewSlope;
                    bool adjustStartEnd = form.AdjustStartEnd; // true = start, false = end
                    bool isAbsoluteValue = form.IsAbsoluteValue; // true = absolute, false = relative
                    bool applyInvertElevation = form.ApplyInvertElevationChange;
                    bool applySlope = form.ApplySlope;

                    // Remember values for next time
                    _lastStartInvertElevation = invertElevationChange;
                    _lastSlope = newSlope;

                    // Apply changes to the single pipe
                    Pipe? pipeForWrite = tr.GetObject(pipeId, OpenMode.ForWrite) as Pipe;
                    if (pipeForWrite != null)
                    {
                        // Set flow direction
                        pipeForWrite.FlowDirectionMethod = FlowDirectionMethodType.StartToEnd;
                        
                        double newStartInvertElevation;
                        double newEndInvertElevation;
                        
                        if (adjustStartEnd)
                        {
                            // Adjust start end
                            if (applyInvertElevation)
                            {
                                newStartInvertElevation = currentStartInvertElevation + invertElevationChange;
                                double newStartCenterElevation = newStartInvertElevation + (pipe.InnerDiameterOrWidth / 2);
                                
                                Point3d newStartPoint = new Point3d(pipe.StartPoint.X, pipe.StartPoint.Y, newStartCenterElevation);
                                pipeForWrite.StartPoint = newStartPoint;
                            }
                            
                            if (applySlope)
                            {
                                pipeForWrite.SetSlopeHoldStart(newSlope);
                            }
                            
                            // Calculate new invert elevations
                            newStartInvertElevation = pipeForWrite.StartPoint.Z - (pipeForWrite.InnerDiameterOrWidth / 2);
                            newEndInvertElevation = pipeForWrite.EndPoint.Z - (pipeForWrite.InnerDiameterOrWidth / 2);

                            A.Ed.WriteMessage($"\nĐã cập nhật cao độ đáy cống (điều chỉnh đầu cống).");
                            if (applyInvertElevation)
                            {
                                if (isAbsoluteValue)
                                {
                                    A.Ed.WriteMessage($"\nLoại thay đổi: Cao độ tuyệt đối");
                                    A.Ed.WriteMessage($"\nCao độ đáy cống đầu mới được đặt: {newStartInvertElevation:F3}m");
                                }
                                else
                                {
                                    A.Ed.WriteMessage($"\nLoại thay đổi: Khoảng thay đổi tương đối");
                                    A.Ed.WriteMessage($"\nKhoảng thay đổi: {invertElevationChange:F3}m");
                                }
                                A.Ed.WriteMessage($"\nCao độ đáy cống đầu cũ: {currentStartInvertElevation:F3}m");
                                A.Ed.WriteMessage($"\nCao độ đáy cống đầu mới: {newStartInvertElevation:F3}m");
                            }
                            else
                            {
                                A.Ed.WriteMessage($"\nCao độ đáy cống giữ nguyên.");
                                A.Ed.WriteMessage($"\nCao độ đáy cống đầu: {newStartInvertElevation:F3}m");
                            }
                            A.Ed.WriteMessage($"\nCao độ đáy cống cuối mới: {newEndInvertElevation:F3}m");
                        }
                        else
                        {
                            // Adjust end
                            if (applyInvertElevation)
                            {
                                newEndInvertElevation = currentEndInvertElevation + invertElevationChange;
                                double newEndCenterElevation = newEndInvertElevation + (pipe.InnerDiameterOrWidth / 2);
                                
                                Point3d newEndPoint = new Point3d(pipe.EndPoint.X, pipe.EndPoint.Y, newEndCenterElevation);
                                pipeForWrite.EndPoint = newEndPoint;
                            }
                            
                            if (applySlope)
                            {
                                pipeForWrite.SetSlopeHoldEnd(newSlope);
                            }
                            
                            // Calculate new invert elevations
                            newStartInvertElevation = pipeForWrite.StartPoint.Z - (pipeForWrite.InnerDiameterOrWidth / 2);
                            newEndInvertElevation = pipeForWrite.EndPoint.Z - (pipeForWrite.InnerDiameterOrWidth / 2);

                            A.Ed.WriteMessage($"\nĐã cập nhật cao độ đáy cống (điều chỉnh cuối cống).");
                            if (applyInvertElevation)
                            {
                                if (isAbsoluteValue)
                                {
                                    A.Ed.WriteMessage($"\nLoại thay đổi: Cao độ tuyệt đối");
                                    A.Ed.WriteMessage($"\nCao độ đáy cống cuối mới được đặt: {newEndInvertElevation:F3}m");
                                }
                                else
                                {
                                    A.Ed.WriteMessage($"\nLoại thay đổi: Khoảng thay đổi tương đối");
                                    A.Ed.WriteMessage($"\nKhoảng thay đổi: {invertElevationChange:F3}m");
                                }
                                A.Ed.WriteMessage($"\nCao độ đáy cống cuối cũ: {currentEndInvertElevation:F3}m");
                                A.Ed.WriteMessage($"\nCao độ đáy cống cuối mới: {newEndInvertElevation:F3}m");
                            }
                            else
                            {
                                A.Ed.WriteMessage($"\nCao độ đáy cống giữ nguyên.");
                                A.Ed.WriteMessage($"\nCao độ đáy cống cuối: {newEndInvertElevation:F3}m");
                            }
                            A.Ed.WriteMessage($"\nCao độ đáy cống đầu mới: {newStartInvertElevation:F3}m");
                        }

                        A.Ed.WriteMessage($"\nĐường kính cống: {diameterMm:F0}mm");
                        if (applySlope)
                        {
                            A.Ed.WriteMessage($"\nDốc mặc định (-1/D): {(defaultSlope * 100):F3}%");
                            A.Ed.WriteMessage($"\nDốc cống mới: {(newSlope * 100):F3}%");
                        }
                        else
                        {
                            // Calculate actual slope from pipe
                            double actualSlope = (pipeForWrite.StartPoint.Z - (pipeForWrite.InnerDiameterOrWidth / 2) - 
                                                 (pipeForWrite.EndPoint.Z - (pipeForWrite.InnerDiameterOrWidth / 2))) / 
                                                 pipeForWrite.Length3DCenterToCenter;
                            A.Ed.WriteMessage($"\nDốc cống giữ nguyên: {(actualSlope * 100):F3}%");
                        }
                        A.Ed.WriteMessage($"\nChiều dài cống: {pipeForWrite.Length3DCenterToCenter:F3}m");
                    }
                }

                tr.Commit();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                A.Ed.WriteMessage($"\nLỗi AutoCAD: {e.Message}");
            }
            catch (System.Exception e)
            {
                A.Ed.WriteMessage($"\nLỗi hệ thống: {e.Message}");
            }
        }

        /// <summary>
        /// Select a pipe from either plan view or profile view
        /// </summary>
        private static ObjectId SelectPipeFromPlanOrProfile(Transaction tr)
        {
            // Use entity selection without class filtering to allow profile entities
            PromptEntityOptions peo = new PromptEntityOptions("\nChọn 1 ống cống cần thay đổi cao độ (trên bình đồ hoặc profile): ");
            peo.AllowNone = false;

            PromptEntityResult per = A.Ed.GetEntity(peo);
            
            if (per.Status != PromptStatus.OK)
            {
                return ObjectId.Null;
            }

            try
            {
                Autodesk.AutoCAD.DatabaseServices.DBObject selectedObj = tr.GetObject(per.ObjectId, OpenMode.ForRead);
                
                // Check if it's a Pipe directly (selected from plan view)
                if (selectedObj is Pipe)
                {
                    A.Ed.WriteMessage("\nĐã chọn ống cống từ bình đồ.");
                    return per.ObjectId;
                }
                
                // Check if it's a profile view part (pipe or structure in profile)
                Type selectedType = selectedObj.GetType();
                string selectedTypeName = selectedType.Name;
                string selectedFullTypeName = selectedType.FullName ?? "";
                
                // Check if this is ProfileViewPart or similar profile entity
                bool isProfileViewPart = selectedFullTypeName.Contains("ProfileViewPart") ||
                                        selectedTypeName.Contains("ProfileViewPart") ||
                                        selectedFullTypeName.Contains("ProfileNetworkPart") ||
                                        selectedTypeName.Contains("ProfileNetworkPart") ||
                                        selectedTypeName.Contains("ProfileEntity");
                
                if (isProfileViewPart)
                {
                    // Try to get ModelPartId property which contains the actual Pipe/Structure ObjectId
                    var modelPartIdProperty = selectedType.GetProperty("ModelPartId");
                    
                    if (modelPartIdProperty != null && modelPartIdProperty.PropertyType == typeof(ObjectId))
                    {
                        try
                        {
                            object? val = modelPartIdProperty.GetValue(selectedObj);
                            ObjectId modelPartId = val != null ? (ObjectId)val : ObjectId.Null;
                            
                            if (modelPartId != ObjectId.Null)
                            {
                                Autodesk.AutoCAD.DatabaseServices.DBObject modelObj = tr.GetObject(modelPartId, OpenMode.ForRead);
                                
                                if (modelObj is Pipe)
                                {
                                    A.Ed.WriteMessage("\nĐã chọn ống cống từ profile view.");
                                    return modelPartId;
                                }
                                else if (modelObj is Structure)
                                {
                                    A.Ed.WriteMessage("\nĐối tượng được chọn là Structure (hố ga), không phải Pipe (ống cống).");
                                    return ObjectId.Null;
                                }
                            }
                        }
                        catch (System.Exception ex)
                        {
                            A.Ed.WriteMessage($"\nLỗi khi đọc ModelPartId: {ex.Message}");
                        }
                    }
                    
                    // If ModelPartId doesn't work, try other common property names
                    string[] alternativePropertyNames = { "EntityId", "RefEntityId", "NetworkEntityId", "PartEntityId" };
                    
                    foreach (string propName in alternativePropertyNames)
                    {
                        var prop = selectedType.GetProperty(propName);
                        if (prop != null && prop.PropertyType == typeof(ObjectId))
                        {
                            try
                            {
                                object? val = prop.GetValue(selectedObj);
                                ObjectId propOid = val != null ? (ObjectId)val : ObjectId.Null;
                                if (propOid != ObjectId.Null)
                                {
                                    Autodesk.AutoCAD.DatabaseServices.DBObject propObj = tr.GetObject(propOid, OpenMode.ForRead);
                                    if (propObj is Pipe)
                                    {
                                        A.Ed.WriteMessage($"\nĐã chọn ống cống từ profile view (qua thuộc tính {propName}).");
                                        return propOid;
                                    }
                                }
                            }
                            catch
                            {
                                continue;
                            }
                        }
                    }
                    
                    A.Ed.WriteMessage("\nKhông thể xác định ống cống từ profile view part.");
                }
                else
                {
                    A.Ed.WriteMessage($"\nĐối tượng được chọn ({selectedTypeName}) không phải là Pipe hoặc Profile View Part.");
                }
                
                return ObjectId.Null;
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi khi xử lý đối tượng được chọn: {ex.Message}");
                return ObjectId.Null;
            }
        }
    }

    // Form class for pipe invert elevation adjustment
    public partial class PipeInvertElevationForm : Form
    {
        public double NewInvertElevationChange { get; private set; }
        public double NewSlope { get; private set; }
        public bool AdjustStartEnd { get; private set; } // true = start, false = end
        public bool IsAbsoluteValue { get; private set; } // true = absolute elevation, false = relative change
        public bool ApplyInvertElevationChange { get; private set; } // true = apply elevation change
        public bool ApplySlope { get; private set; } // true = apply slope

        private System.Windows.Forms.Label lblCurrentInfo;
        private System.Windows.Forms.Label lblInvertElevationChange;
        private System.Windows.Forms.Label lblSlope;
        private System.Windows.Forms.TextBox txtInvertElevationChange;
        private System.Windows.Forms.TextBox txtSlope;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnUseDefaultSlope;
        private System.Windows.Forms.GroupBox grpCurrent;
        private System.Windows.Forms.GroupBox grpNew;
        private System.Windows.Forms.Label lblSlopeFormula;
        private System.Windows.Forms.GroupBox grpEndSelection;
        private System.Windows.Forms.RadioButton rbStart;
        private System.Windows.Forms.RadioButton rbEnd;
        private System.Windows.Forms.Label lblEndSelectionNote;
        private System.Windows.Forms.GroupBox grpValueType;
        private System.Windows.Forms.RadioButton rbRelativeChange;
        private System.Windows.Forms.RadioButton rbAbsoluteValue;
        private System.Windows.Forms.Label lblValueTypeNote;
        private System.Windows.Forms.CheckBox chkApplyInvertElevation;
        private System.Windows.Forms.CheckBox chkApplySlope;

        private double _defaultSlope;
        private double _diameterMm;
        private double _currentStartInvertElevation;
        private double _currentEndInvertElevation;

        public PipeInvertElevationForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.lblCurrentInfo = new System.Windows.Forms.Label();
            this.lblInvertElevationChange = new System.Windows.Forms.Label();
            this.lblSlope = new System.Windows.Forms.Label();
            this.txtInvertElevationChange = new System.Windows.Forms.TextBox();
            this.txtSlope = new System.Windows.Forms.TextBox();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnUseDefaultSlope = new System.Windows.Forms.Button();
            this.grpCurrent = new System.Windows.Forms.GroupBox();
            this.grpNew = new System.Windows.Forms.GroupBox();
            this.lblSlopeFormula = new System.Windows.Forms.Label();
            this.grpEndSelection = new System.Windows.Forms.GroupBox();
            this.rbStart = new System.Windows.Forms.RadioButton();
            this.rbEnd = new System.Windows.Forms.RadioButton();
            this.lblEndSelectionNote = new System.Windows.Forms.Label();
            this.grpValueType = new System.Windows.Forms.GroupBox();
            this.rbRelativeChange = new System.Windows.Forms.RadioButton();
            this.rbAbsoluteValue = new System.Windows.Forms.RadioButton();
            this.lblValueTypeNote = new System.Windows.Forms.Label();
            this.chkApplyInvertElevation = new System.Windows.Forms.CheckBox();
            this.chkApplySlope = new System.Windows.Forms.CheckBox();

            this.SuspendLayout();

            // Form
            this.Text = "Thay đổi cao độ đáy cống";
            this.Size = new System.Drawing.Size(450, 660);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Current Info Group
            this.grpCurrent.Text = "Thông tin hiện tại";
            this.grpCurrent.Location = new System.Drawing.Point(12, 12);
            this.grpCurrent.Size = new System.Drawing.Size(410, 140);

            // Current Info Label
            this.lblCurrentInfo.Location = new System.Drawing.Point(10, 25);
            this.lblCurrentInfo.Size = new System.Drawing.Size(390, 105);
            this.lblCurrentInfo.Text = "Thông tin sẽ được hiển thị ở đây";

            // End Selection Group
            this.grpEndSelection.Text = "Chọn đầu cống cần điều chỉnh";
            this.grpEndSelection.Location = new System.Drawing.Point(12, 160);
            this.grpEndSelection.Size = new System.Drawing.Size(410, 80);

            // Start Radio Button
            this.rbStart.Text = "Đầu cống (Start)";
            this.rbStart.Location = new System.Drawing.Point(20, 25);
            this.rbStart.Size = new System.Drawing.Size(120, 20);
            this.rbStart.Checked = true;
            this.rbStart.CheckedChanged += new EventHandler(this.rbEnd_CheckedChanged);

            // End Radio Button
            this.rbEnd.Text = "Cuối cống (End)";
            this.rbEnd.Location = new System.Drawing.Point(160, 25);
            this.rbEnd.Size = new System.Drawing.Size(120, 20);
            this.rbEnd.CheckedChanged += new EventHandler(this.rbEnd_CheckedChanged);

            // End Selection Note
            this.lblEndSelectionNote.Text = "Lưu ý: Điều chỉnh đầu cống sẽ giữ nguyên cuối cống, điều chỉnh cuối cống sẽ giữ nguyên đầu cống";
            this.lblEndSelectionNote.Location = new System.Drawing.Point(10, 50);
            this.lblEndSelectionNote.Size = new System.Drawing.Size(390, 25);
            this.lblEndSelectionNote.ForeColor = System.Drawing.Color.Blue;

            // Value Type Group
            this.grpValueType.Text = "Loại giá trị cần thay đổi";
            this.grpValueType.Location = new System.Drawing.Point(12, 250);
            this.grpValueType.Size = new System.Drawing.Size(410, 80);

            // Relative Change Radio Button
            this.rbRelativeChange.Text = "Khoảng thay đổi (m)";
            this.rbRelativeChange.Location = new System.Drawing.Point(20, 25);
            this.rbRelativeChange.Size = new System.Drawing.Size(140, 20);
            this.rbRelativeChange.Checked = true;
            this.rbRelativeChange.CheckedChanged += new EventHandler(this.rbValueType_CheckedChanged);

            // Absolute Value Radio Button
            this.rbAbsoluteValue.Text = "Cao độ đáy cống mới (m)";
            this.rbAbsoluteValue.Location = new System.Drawing.Point(180, 25);
            this.rbAbsoluteValue.Size = new System.Drawing.Size(150, 20);
            this.rbAbsoluteValue.CheckedChanged += new EventHandler(this.rbValueType_CheckedChanged);

            // Value Type Note
            this.lblValueTypeNote.Text = "Khoảng thay đổi: ±x mét so với hiện tại | Cao độ tuyệt đối: cao độ đáy cống mới";
            this.lblValueTypeNote.Location = new System.Drawing.Point(10, 50);
            this.lblValueTypeNote.Size = new System.Drawing.Size(390, 25);
            this.lblValueTypeNote.ForeColor = System.Drawing.Color.Blue;

            // New Values Group
            this.grpNew.Text = "Giá trị mới";
            this.grpNew.Location = new System.Drawing.Point(12, 340);
            this.grpNew.Size = new System.Drawing.Size(410, 200);

            // Apply Invert Elevation CheckBox
            this.chkApplyInvertElevation.Text = "Sử dụng cao độ đáy cống";
            this.chkApplyInvertElevation.Location = new System.Drawing.Point(10, 25);
            this.chkApplyInvertElevation.Size = new System.Drawing.Size(180, 20);
            this.chkApplyInvertElevation.Checked = true;
            this.chkApplyInvertElevation.CheckedChanged += new EventHandler(this.chkApplyInvertElevation_CheckedChanged);

            // Invert Elevation Change Label
            this.lblInvertElevationChange.Text = "Khoảng thay đổi cao độ đáy (m):";
            this.lblInvertElevationChange.Location = new System.Drawing.Point(30, 50);
            this.lblInvertElevationChange.Size = new System.Drawing.Size(180, 23);

            // Invert Elevation Change TextBox
            this.txtInvertElevationChange.Location = new System.Drawing.Point(220, 47);
            this.txtInvertElevationChange.Size = new System.Drawing.Size(100, 23);
            this.txtInvertElevationChange.Text = "0.000";

            // Apply Slope CheckBox
            this.chkApplySlope.Text = "Sử dụng dốc cống";
            this.chkApplySlope.Location = new System.Drawing.Point(10, 85);
            this.chkApplySlope.Size = new System.Drawing.Size(150, 20);
            this.chkApplySlope.Checked = true;
            this.chkApplySlope.CheckedChanged += new EventHandler(this.chkApplySlope_CheckedChanged);

            // Slope Label
            this.lblSlope.Text = "Dốc cống (%):";
            this.lblSlope.Location = new System.Drawing.Point(30, 110);
            this.lblSlope.Size = new System.Drawing.Size(150, 23);

            // Slope TextBox
            this.txtSlope.Location = new System.Drawing.Point(220, 107);
            this.txtSlope.Size = new System.Drawing.Size(100, 23);
            this.txtSlope.Text = "0.5";

            // Use Default Slope Button
            this.btnUseDefaultSlope.Text = "Dùng dốc mặc định";
            this.btnUseDefaultSlope.Location = new System.Drawing.Point(310, 107);
            this.btnUseDefaultSlope.Size = new System.Drawing.Size(90, 23);
            this.btnUseDefaultSlope.Click += new EventHandler(this.btnUseDefaultSlope_Click);

            // Slope Formula Label
            this.lblSlopeFormula.Text = "Công thức dốc mặc định: -1/D";
            this.lblSlopeFormula.Location = new System.Drawing.Point(30, 140);
            this.lblSlopeFormula.Size = new System.Drawing.Size(370, 40);
            this.lblSlopeFormula.ForeColor = System.Drawing.Color.Blue;

            // OK Button
            this.btnOK.Text = "OK";
            this.btnOK.Location = new System.Drawing.Point(267, 560);
            this.btnOK.Size = new System.Drawing.Size(75, 30);
            this.btnOK.Click += new EventHandler(this.btnOK_Click);

            // Cancel Button
            this.btnCancel.Text = "Hủy";
            this.btnCancel.Location = new System.Drawing.Point(347, 560);
            this.btnCancel.Size = new System.Drawing.Size(75, 30);
            this.btnCancel.Click += new EventHandler(this.btnCancel_Click);

            // Add controls to groups
            this.grpCurrent.Controls.Add(this.lblCurrentInfo);
            
            this.grpEndSelection.Controls.Add(this.rbStart);
            this.grpEndSelection.Controls.Add(this.rbEnd);
            this.grpEndSelection.Controls.Add(this.lblEndSelectionNote);

            this.grpValueType.Controls.Add(this.rbRelativeChange);
            this.grpValueType.Controls.Add(this.rbAbsoluteValue);
            this.grpValueType.Controls.Add(this.lblValueTypeNote);
            
            this.grpNew.Controls.Add(this.chkApplyInvertElevation);
            this.grpNew.Controls.Add(this.lblInvertElevationChange);
            this.grpNew.Controls.Add(this.txtInvertElevationChange);
            this.grpNew.Controls.Add(this.chkApplySlope);
            this.grpNew.Controls.Add(this.lblSlope);
            this.grpNew.Controls.Add(this.txtSlope);
            this.grpNew.Controls.Add(this.btnUseDefaultSlope);
            this.grpNew.Controls.Add(this.lblSlopeFormula);

            // Add controls to form
            this.Controls.Add(this.grpCurrent);
            this.Controls.Add(this.grpEndSelection);
            this.Controls.Add(this.grpValueType);
            this.Controls.Add(this.grpNew);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.btnCancel);

            this.ResumeLayout(false);
        }

        public void SetCurrentPipeInfo(double currentStartInvertElevation, double currentEndInvertElevation,
                                     double currentStartCover, double currentEndCover, double currentSlope, 
                                     double pipeLength, double diameterMm, double defaultSlope, 
                                     double lastInvertElevationChange, double lastSlope)
        {
            _diameterMm = diameterMm;
            _defaultSlope = defaultSlope;
            _currentStartInvertElevation = currentStartInvertElevation;
            _currentEndInvertElevation = currentEndInvertElevation;

            string info = $"Cao độ đáy cống đầu: {currentStartInvertElevation:F3} m\n";
            info += $"Cao độ đáy cống cuối: {currentEndInvertElevation:F3} m\n";
            info += $"Độ che phủ đầu cống: {currentStartCover:F3} m\n";
            info += $"Độ che phủ cuối cống: {currentEndCover:F3} m\n";
            info += $"Dốc cống hiện tại: {(currentSlope * 100):F3} %\n";
            info += $"Chiều dài cống: {pipeLength:F3} m";

            this.lblCurrentInfo.Text = info;

            // Update slope formula label with actual values
            this.lblSlopeFormula.Text = $"Công thức dốc mặc định: -1/D = -1/{diameterMm:F0}mm = {(defaultSlope * 100):F3}%";

            // Set default values from last input
            this.txtInvertElevationChange.Text = lastInvertElevationChange.ToString("F3");
            this.txtSlope.Text = (lastSlope * 100).ToString("F3");
        }

        private void rbValueType_CheckedChanged(object sender, EventArgs e)
        {
            if (rbRelativeChange.Checked)
            {
                this.lblInvertElevationChange.Text = "Khoảng thay đổi cao độ đáy (m):";
                this.txtInvertElevationChange.Text = "0.000";
            }
            else if (rbAbsoluteValue.Checked)
            {
                this.lblInvertElevationChange.Text = "Cao độ đáy cống mới (m):";
                // Set current elevation based on selected end
                if (rbStart.Checked)
                {
                    this.txtInvertElevationChange.Text = _currentStartInvertElevation.ToString("F3");
                }
                else
                {
                    this.txtInvertElevationChange.Text = _currentEndInvertElevation.ToString("F3");
                }
            }
        }

        private void rbEnd_CheckedChanged(object sender, EventArgs e)
        {
            // Update the absolute value when end selection changes and absolute mode is selected
            if (rbAbsoluteValue.Checked)
            {
                if (rbStart.Checked)
                {
                    this.txtInvertElevationChange.Text = _currentStartInvertElevation.ToString("F3");
                }
                else if (rbEnd.Checked)
                {
                    this.txtInvertElevationChange.Text = _currentEndInvertElevation.ToString("F3");
                }
            }
        }

        private void btnUseDefaultSlope_Click(object sender, EventArgs e)
        {
            // Set the default slope calculated from -1/D formula
            this.txtSlope.Text = (_defaultSlope * 100).ToString("F3");
        }

        private void chkApplyInvertElevation_CheckedChanged(object sender, EventArgs e)
        {
            bool enabled = chkApplyInvertElevation.Checked;
            lblInvertElevationChange.Enabled = enabled;
            txtInvertElevationChange.Enabled = enabled;
        }

        private void chkApplySlope_CheckedChanged(object sender, EventArgs e)
        {
            bool enabled = chkApplySlope.Checked;
            lblSlope.Enabled = enabled;
            txtSlope.Enabled = enabled;
            btnUseDefaultSlope.Enabled = enabled;
            lblSlopeFormula.Enabled = enabled;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            try
            {
                // Store checkbox states
                ApplyInvertElevationChange = chkApplyInvertElevation.Checked;
                ApplySlope = chkApplySlope.Checked;

                // Check if at least one option is selected
                if (!ApplyInvertElevationChange && !ApplySlope)
                {
                    MessageBox.Show("Vui lòng chọn ít nhất một tùy chọn (cao độ đáy cống hoặc dốc cống).", "Cảnh báo", 
                                  MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Validate and parse invert elevation change/value only if applied
                if (ApplyInvertElevationChange)
                {
                    if (!double.TryParse(txtInvertElevationChange.Text, out double inputValue))
                    {
                        string fieldName = rbRelativeChange.Checked ? "Khoảng thay đổi cao độ đáy cống" : "Cao độ đáy cống mới";
                        MessageBox.Show($"{fieldName} không hợp lệ. Vui lòng nhập số thực.", "Lỗi", 
                                      MessageBoxButtons.OK, MessageBoxIcon.Error);
                        txtInvertElevationChange.Focus();
                        return;
                    }

                    // Calculate the actual change value
                    double invertElevationChange;
                    if (rbRelativeChange.Checked)
                    {
                        // Direct relative change
                        invertElevationChange = inputValue;
                    }
                    else
                    {
                        // Calculate change from absolute value
                        double currentElevation = rbStart.Checked ? _currentStartInvertElevation : _currentEndInvertElevation;
                        invertElevationChange = inputValue - currentElevation;
                    }

                    // Reasonable elevation change validation (warn if > 5m change)
                    if (Math.Abs(invertElevationChange) > 5.0)
                    {
                        var result = MessageBox.Show(
                            $"Khoảng thay đổi cao độ {invertElevationChange:F3}m có vẻ lớn.\nBạn có muốn tiếp tục?", 
                            "Cảnh báo", 
                            MessageBoxButtons.YesNo, 
                            MessageBoxIcon.Warning);
                        
                        if (result == DialogResult.No)
                        {
                            txtInvertElevationChange.Focus();
                            return;
                        }
                    }

                    NewInvertElevationChange = invertElevationChange;
                }
                else
                {
                    NewInvertElevationChange = 0;
                }

                // Validate and parse slope only if applied
                if (ApplySlope)
                {
                    if (!double.TryParse(txtSlope.Text, out double slopePercent))
                    {
                        MessageBox.Show("Dốc cống không hợp lệ. Vui lòng nhập số thực.", "Lỗi", 
                                      MessageBoxButtons.OK, MessageBoxIcon.Error);
                        txtSlope.Focus();
                        return;
                    }

                    // Convert percentage to decimal
                    double slope = slopePercent / 100.0;

                    // Reasonable slope validation (between -10% and +10%)
                    if (Math.Abs(slope) > 0.1)
                    {
                        var result = MessageBox.Show(
                            $"Dốc cống {slopePercent:F3}% có vẻ không hợp lý.\nBạn có muốn tiếp tục?", 
                            "Cảnh báo", 
                            MessageBoxButtons.YesNo, 
                            MessageBoxIcon.Warning);
                        
                        if (result == DialogResult.No)
                        {
                            txtSlope.Focus();
                            return;
                        }
                    }

                    NewSlope = slope;
                }
                else
                {
                    NewSlope = 0;
                }

                AdjustStartEnd = rbStart.Checked; // true = start, false = end
                IsAbsoluteValue = rbAbsoluteValue.Checked; // true = absolute, false = relative
                
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi", 
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}

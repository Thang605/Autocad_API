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
[assembly: CommandClass(typeof(Civil3DCsharp.CTC_TaoCorridor_ChoTuyenDuong_Commands))]

namespace Civil3DCsharp
{
    public class CTC_TaoCorridor_ChoTuyenDuong_Commands
    {
        // Static variables to remember last input values
        private static string _lastCorridorName = "Corridor_";
        private static string _lastAssemblyName = "";
        private static string _lastCodeSetStyleName = "";
        private static ObjectId _lastTargetSurfaceId = ObjectId.Null;
        private static double _lastFrequencyDistance = 25.0;
        private static double _lastStartOffset = -25.0;
        private static double _lastEndOffset = 25.0;
        private static bool _lastCreateSingleCorridor = false;

        [CommandMethod("CTC_Tao_Corridor_Cho_Tuyen_Duong")]
        public static void CTC_TaoCorridor_ChoTuyenDuong()
        {
            // start transaction
            using Transaction tr = A.Db.TransactionManager.StartTransaction();
            try
            {
                UserInput UI = new();
                UtilitiesCAD CAD = new();
                UtilitiesC3D C3D = new();
                
                // Get multiple alignment selection
                A.Ed.WriteMessage("\nChọn các tuyến đường để tạo corridor:");
                ObjectIdCollection alignmentIds = UserInput.GSelectionSetWithType("\nChọn các tuyến đường:", "AECC_ALIGNMENT");
                
                if (alignmentIds.Count == 0)
                {
                    A.Ed.WriteMessage("\nKhông có tuyến đường nào được chọn.");
                    return;
                }

                // Get available assemblies
                List<string> assemblyNames = new List<string>();
                foreach (ObjectId assemblyId in A.Cdoc.AssemblyCollection)
                {
                    Assembly? assembly = tr.GetObject(assemblyId, OpenMode.ForRead) as Assembly;
                    if (assembly != null)
                    {
                        assemblyNames.Add(assembly.Name);
                    }
                }

                // Get available code set styles
                List<string> codeSetStyleNames = new List<string>();
                foreach (ObjectId codeSetStyleId in A.Cdoc.Styles.CodeSetStyles)
                {
                    CodeSetStyle? codeSetStyle = tr.GetObject(codeSetStyleId, OpenMode.ForRead) as CodeSetStyle;
                    if (codeSetStyle != null)
                    {
                        codeSetStyleNames.Add(codeSetStyle.Name);
                    }
                }

                // Get available surfaces
                List<string> surfaceNames = new List<string>();
                List<ObjectId> surfaceIds = new List<ObjectId>();
                foreach (ObjectId surfaceId in A.Cdoc.GetSurfaceIds())
                {
                    CivSurface? surface = tr.GetObject(surfaceId, OpenMode.ForRead) as CivSurface;
                    if (surface != null)
                    {
                        surfaceNames.Add(surface.Name);
                        surfaceIds.Add(surfaceId);
                    }
                }

                // Create and show the form
                using (var form = new CorridorCreationForm())
                {
                    // Set available options and last values
                    form.SetAvailableOptions(
                        assemblyNames.ToArray(),
                        codeSetStyleNames.ToArray(),
                        surfaceNames.ToArray(),
                        _lastCorridorName,
                        _lastAssemblyName,
                        _lastCodeSetStyleName,
                        _lastTargetSurfaceId,
                        surfaceIds.ToArray(),
                        _lastFrequencyDistance,
                        _lastStartOffset,
                        _lastEndOffset,
                        _lastCreateSingleCorridor
                    );

                    DialogResult result = Application.ShowModalDialog(form);

                    if (result != DialogResult.OK)
                    {
                        A.Ed.WriteMessage("\nThao tác bị hủy bỏ.");
                        return;
                    }

                    // Get values from form
                    string corridorNamePrefix = form.CorridorNamePrefix;
                    string assemblyName = form.SelectedAssemblyName;
                    string codeSetStyleName = form.SelectedCodeSetStyleName;
                    ObjectId targetSurfaceId = form.SelectedTargetSurfaceId;
                    double frequencyDistance = form.FrequencyDistance;
                    double startOffset = form.StartOffset;
                    double endOffset = form.EndOffset;
                    bool createSingleCorridor = form.CreateSingleCorridor;

                    // Remember values for next time
                    _lastCorridorName = corridorNamePrefix;
                    _lastAssemblyName = assemblyName;
                    _lastCodeSetStyleName = codeSetStyleName;
                    _lastTargetSurfaceId = targetSurfaceId;
                    _lastFrequencyDistance = frequencyDistance;
                    _lastStartOffset = startOffset;
                    _lastEndOffset = endOffset;
                    _lastCreateSingleCorridor = createSingleCorridor;

                    // Get assembly and code set style objects
                    ObjectId assemblyId = A.Cdoc.AssemblyCollection[assemblyName];
                    ObjectId codeSetStyleId = A.Cdoc.Styles.CodeSetStyles[codeSetStyleName];

                    if (createSingleCorridor)
                    {
                        // Create single corridor for all alignments
                        CreateSingleCorridorForAllAlignments(tr, alignmentIds, corridorNamePrefix, assemblyId, 
                                                           codeSetStyleId, targetSurfaceId, frequencyDistance, 
                                                           startOffset, endOffset, assemblyName, codeSetStyleName);
                    }
                    else
                    {
                        // Create separate corridor for each alignment (original functionality)
                        CreateSeparateCorridorsForEachAlignment(tr, alignmentIds, corridorNamePrefix, assemblyId, 
                                                              codeSetStyleId, targetSurfaceId, frequencyDistance, 
                                                              startOffset, endOffset, assemblyName, codeSetStyleName);
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

        private static void CreateSingleCorridorForAllAlignments(Transaction tr, ObjectIdCollection alignmentIds,
                                                               string corridorNamePrefix, ObjectId assemblyId,
                                                               ObjectId codeSetStyleId, ObjectId targetSurfaceId,
                                                               double frequencyDistance, double startOffset,
                                                               double endOffset, string assemblyName,
                                                               string codeSetStyleName)
        {
            try
            {
                // Create single corridor name
                string corridorName = $"{corridorNamePrefix}Combined";
                
                // Check if corridor already exists
                foreach (ObjectId existingCorridorId in A.Cdoc.CorridorCollection)
                {
                    Corridor? existingCorridor = tr.GetObject(existingCorridorId, OpenMode.ForRead) as Corridor;
                    if (existingCorridor != null && existingCorridor.Name == corridorName)
                    {
                        A.Ed.WriteMessage($"\nCorridor '{corridorName}' đã tồn tại. Vui lòng xóa hoặc đổi tên trước khi tạo mới.");
                        return;
                    }
                }

                // Create new corridor
                ObjectId corridorId = A.Cdoc.CorridorCollection.Add(corridorName);
                Corridor? corridor = tr.GetObject(corridorId, OpenMode.ForWrite) as Corridor;
                
                if (corridor == null)
                {
                    A.Ed.WriteMessage($"\nKhông thể tạo corridor '{corridorName}'.");
                    return;
                }
                
                // Set code set style after creation
                corridor.CodeSetStyleId = codeSetStyleId;

                int successCount = 0;
                int totalCount = alignmentIds.Count;

                // Create baselines for each alignment
                foreach (ObjectId alignmentId in alignmentIds)
                {
                    Alignment? alignment = tr.GetObject(alignmentId, OpenMode.ForRead) as Alignment;
                    if (alignment == null)
                    {
                        A.Ed.WriteMessage($"\nKhông thể đọc tuyến đường với ID: {alignmentId}");
                        continue;
                    }

                    try
                    {
                        // Check if alignment has profiles
                        ObjectIdCollection profileIds = alignment.GetProfileIds();
                        if (profileIds.Count == 0)
                        {
                            A.Ed.WriteMessage($"\nTuyến đường '{alignment.Name}' không có profile. Bỏ qua...");
                            continue;
                        }

                        // Use the first profile
                        ObjectId profileId = profileIds[0];
                        Profile? profile = tr.GetObject(profileId, OpenMode.ForRead) as Profile;
                        if (profile == null)
                        {
                            A.Ed.WriteMessage($"\nKhông thể đọc profile cho tuyến đường '{alignment.Name}'. Bỏ qua...");
                            continue;
                        }

                        // Create baseline
                        string baselineName = $"BL-{alignment.Name}-{profile.Name}";
                        Baseline baseline = corridor.Baselines.Add(baselineName, alignmentId, profileId);

                        // Create baseline region
                        double startStation = alignment.StartingStation;
                        double endStation = alignment.EndingStation;
                        string regionName = $"RG-{alignment.Name}-{startStation:F2}-{endStation:F2}";
                        
                        BaselineRegion baselineRegion = baseline.BaselineRegions.Add(regionName, assemblyId, startStation, endStation);

                        // Set frequency for assembly sections
                        UtilitiesC3D.SetFrequencySection(baselineRegion, (int)frequencyDistance);

                        // Set target surface if specified
                        if (targetSurfaceId != ObjectId.Null)
                        {
                            try
                            {
                                SetCorridorTargetSurface(baselineRegion, targetSurfaceId);
                            }
                            catch (System.Exception ex)
                            {
                                A.Ed.WriteMessage($"\nCảnh báo: Không thể đặt target surface cho baseline '{baselineName}': {ex.Message}");
                            }
                        }

                        // Set offset targets if needed
                        SetCorridorOffsetTargets(baselineRegion, alignmentId, startOffset, endOffset);

                        A.Ed.WriteMessage($"\nĐã thêm baseline '{baselineName}' vào corridor.");
                        successCount++;
                    }
                    catch (System.Exception ex)
                    {
                        A.Ed.WriteMessage($"\nLỗi khi thêm baseline cho tuyến đường '{alignment.Name}': {ex.Message}");
                    }
                }

                // Rebuild corridor after adding all baselines
                if (successCount > 0)
                {
                    corridor.Rebuild();
                    A.Ed.WriteMessage($"\nĐã rebuild corridor '{corridorName}' thành công.");
                }

                A.Ed.WriteMessage($"\n=== Kết quả Single Corridor ===");
                A.Ed.WriteMessage($"\nTên corridor: {corridorName}");
                A.Ed.WriteMessage($"\nĐã thêm thành công: {successCount}/{totalCount} baseline");
                A.Ed.WriteMessage($"\nAssembly sử dụng: {assemblyName}");
                A.Ed.WriteMessage($"\nCode Set Style: {codeSetStyleName}");
                A.Ed.WriteMessage($"\nKhoảng cách section: {frequencyDistance}m");
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nLỗi khi tạo single corridor: {ex.Message}");
            }
        }

        private static void CreateSeparateCorridorsForEachAlignment(Transaction tr, ObjectIdCollection alignmentIds,
                                                                  string corridorNamePrefix, ObjectId assemblyId,
                                                                  ObjectId codeSetStyleId, ObjectId targetSurfaceId,
                                                                  double frequencyDistance, double startOffset,
                                                                  double endOffset, string assemblyName,
                                                                  string codeSetStyleName)
        {
            int successCount = 0;
            int totalCount = alignmentIds.Count;

            // Create corridors for each selected alignment
            foreach (ObjectId alignmentId in alignmentIds)
            {
                Alignment? alignment = tr.GetObject(alignmentId, OpenMode.ForRead) as Alignment;
                if (alignment == null)
                {
                    A.Ed.WriteMessage($"\nKhông thể đọc tuyến đường với ID: {alignmentId}");
                    continue;
                }

                try
                {
                    // Check if alignment has profiles
                    ObjectIdCollection profileIds = alignment.GetProfileIds();
                    if (profileIds.Count == 0)
                    {
                        A.Ed.WriteMessage($"\nTuyến đường '{alignment.Name}' không có profile. Bỏ qua...");
                        continue;
                    }

                    // Use the first profile
                    ObjectId profileId = profileIds[0];
                    Profile? profile = tr.GetObject(profileId, OpenMode.ForRead) as Profile;
                    if (profile == null)
                    {
                        A.Ed.WriteMessage($"\nKhông thể đọc profile cho tuyến đường '{alignment.Name}'. Bỏ qua...");
                        continue;
                    }

                    // Create corridor name based on alignment name
                    string corridorName = $"{corridorNamePrefix}{alignment.Name}";

                    // Check if corridor already exists
                    bool corridorExists = false;
                    foreach (ObjectId existingCorridorId in A.Cdoc.CorridorCollection)
                    {
                        Corridor? existingCorridor = tr.GetObject(existingCorridorId, OpenMode.ForRead) as Corridor;
                        if (existingCorridor != null && existingCorridor.Name == corridorName)
                        {
                            corridorExists = true;
                            break;
                        }
                    }

                    if (corridorExists)
                    {
                        A.Ed.WriteMessage($"\nCorridor '{corridorName}' đã tồn tại. Bỏ qua...");
                        continue;
                    }

                    // Create new corridor
                    ObjectId corridorId = A.Cdoc.CorridorCollection.Add(corridorName);
                    Corridor? corridor = tr.GetObject(corridorId, OpenMode.ForWrite) as Corridor;
                    
                    if (corridor == null)
                    {
                        A.Ed.WriteMessage($"\nKhông thể tạo corridor cho tuyến đường '{alignment.Name}'. Bỏ qua...");
                        continue;
                    }
                    
                    // Set code set style after creation
                    corridor.CodeSetStyleId = codeSetStyleId;

                    // Create baseline
                    string baselineName = $"BL-{alignment.Name}-{profile.Name}";
                    Baseline baseline = corridor.Baselines.Add(baselineName, alignmentId, profileId);

                    // Create baseline region
                    double startStation = alignment.StartingStation;
                    double endStation = alignment.EndingStation;
                    string regionName = $"RG-{alignment.Name}-{startStation:F2}-{endStation:F2}";
                    
                    BaselineRegion baselineRegion = baseline.BaselineRegions.Add(regionName, assemblyId, startStation, endStation);

                    // Set frequency for assembly sections
                    UtilitiesC3D.SetFrequencySection(baselineRegion, (int)frequencyDistance);

                    // Set target surface if specified
                    if (targetSurfaceId != ObjectId.Null)
                    {
                        try
                        {
                            SetCorridorTargetSurface(baselineRegion, targetSurfaceId);
                        }
                        catch (System.Exception ex)
                        {
                            A.Ed.WriteMessage($"\nCảnh báo: Không thể đặt target surface cho corridor '{corridorName}': {ex.Message}");
                        }
                    }

                    // Set offset targets if needed
                    SetCorridorOffsetTargets(baselineRegion, alignmentId, startOffset, endOffset);

                    // Rebuild corridor
                    corridor.Rebuild();

                    A.Ed.WriteMessage($"\nĐã tạo corridor '{corridorName}' thành công.");
                    successCount++;
                }
                catch (System.Exception ex)
                {
                    A.Ed.WriteMessage($"\nLỗi khi tạo corridor cho tuyến đường '{alignment.Name}': {ex.Message}");
                }
            }

            A.Ed.WriteMessage($"\n=== Kết quả Multiple Corridors ===");
            A.Ed.WriteMessage($"\nĐã tạo thành công: {successCount}/{totalCount} corridor");
            A.Ed.WriteMessage($"\nAssembly sử dụng: {assemblyName}");
            A.Ed.WriteMessage($"\nCode Set Style: {codeSetStyleName}");
            A.Ed.WriteMessage($"\nKhoảng cách section: {frequencyDistance}m");
        }

        private static void SetCorridorTargetSurface(BaselineRegion baselineRegion, ObjectId targetSurfaceId)
        {
            try
            {
                // Get the target information collection for this baseline region
                SubassemblyTargetInfoCollection targetInfoCollection = baselineRegion.GetTargets();
                
                if (targetInfoCollection.Count == 0)
                {
                    A.Ed.WriteMessage($"\nKhông có target nào cho baseline region này.");
                    return;
                }

                // Set target surface for all subassembly targets that accept surface targets
                bool targetSet = false;
                foreach (SubassemblyTargetInfo targetInfo in targetInfoCollection)
                {
                    // Check if this target can accept surface targets by checking the target type
                    // Different target types: Width, Offset, Elevation, Slope, etc.
                    if (targetInfo.TargetType.ToString().Contains("Surface") || 
                        targetInfo.TargetType.ToString().Contains("Elevation"))
                    {
                        try
                        {
                            // Clear existing targets and add the new surface
                            ObjectIdCollection surfaceTargets = new ObjectIdCollection();
                            surfaceTargets.Add(targetSurfaceId);
                            targetInfo.TargetIds = surfaceTargets;
                            targetInfo.TargetToOption = SubassemblyTargetToOption.Nearest;
                            targetSet = true;
                            
                            A.Ed.WriteMessage($"\nĐã set target surface cho subassembly: {targetInfo.SubassemblyName}, Target type: {targetInfo.TargetType}");
                        }
                        catch (System.Exception subEx)
                        {
                            A.Ed.WriteMessage($"\nKhông thể set target cho {targetInfo.SubassemblyName}: {subEx.Message}");
                        }
                    }
                }

                if (targetSet)
                {
                    // Apply the target settings back to the baseline region
                    baselineRegion.SetTargets(targetInfoCollection);
                    A.Ed.WriteMessage($"\nĐã áp dụng target surface thành công.");
                }
                else
                {
                    // Try to set for all available targets if no specific surface targets found
                    A.Ed.WriteMessage($"\nKhông tìm thấy target surface elevation cụ thể. Thử áp dụng cho tất cả targets...");
                    
                    foreach (SubassemblyTargetInfo targetInfo in targetInfoCollection)
                    {
                        try
                        {
                            // Try to set surface for any target that might accept it
                            ObjectIdCollection surfaceTargets = new ObjectIdCollection();
                            surfaceTargets.Add(targetSurfaceId);
                            targetInfo.TargetIds = surfaceTargets;
                            targetInfo.TargetToOption = SubassemblyTargetToOption.Nearest;
                            
                            A.Ed.WriteMessage($"\nThử set target cho: {targetInfo.SubassemblyName}, Type: {targetInfo.TargetType}");
                        }
                        catch (System.Exception subEx)
                        {
                            A.Ed.WriteMessage($"\nKhông thể set target cho {targetInfo.SubassemblyName}: {subEx.Message}");
                        }
                    }
                    
                    try
                    {
                        baselineRegion.SetTargets(targetInfoCollection);
                        A.Ed.WriteMessage($"\nĐã thử áp dụng target surface.");
                    }
                    catch (System.Exception setEx)
                    {
                        A.Ed.WriteMessage($"\nLỗi khi áp dụng targets: {setEx.Message}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                throw new System.Exception($"Không thể đặt target surface: {ex.Message}");
            }
        }

        private static void SetCorridorOffsetTargets(BaselineRegion baselineRegion, ObjectId alignmentId, double startOffset, double endOffset)
        {
            try
            {
                // Get the target information collection for this baseline region
                SubassemblyTargetInfoCollection targetInfoCollection = baselineRegion.GetTargets();
                
                if (targetInfoCollection.Count == 0)
                {
                    return; // No targets to set
                }

                // Look for width or offset targets that might use alignment references
                bool offsetTargetSet = false;
                foreach (SubassemblyTargetInfo targetInfo in targetInfoCollection)
                {
                    // Check if this is a width or offset target type
                    string targetTypeStr = targetInfo.TargetType.ToString();
                    if (targetTypeStr.Contains("Width") || targetTypeStr.Contains("Offset"))
                    {
                        try
                        {
                            // For alignment-based offset targets, we might set the alignment as target
                            ObjectIdCollection alignmentTargets = new ObjectIdCollection();
                            alignmentTargets.Add(alignmentId);
                            targetInfo.TargetIds = alignmentTargets;
                            targetInfo.TargetToOption = SubassemblyTargetToOption.Nearest;
                            offsetTargetSet = true;
                            
                            A.Ed.WriteMessage($"\nĐã set alignment target cho: {targetInfo.SubassemblyName}, Type: {targetInfo.TargetType}");
                        }
                        catch (System.Exception subEx)
                        {
                            // This is optional, so we don't throw errors
                            A.Ed.WriteMessage($"\nKhông thể set alignment target cho {targetInfo.SubassemblyName}: {subEx.Message}");
                        }
                    }
                }

                if (offsetTargetSet)
                {
                    try
                    {
                        baselineRegion.SetTargets(targetInfoCollection);
                        A.Ed.WriteMessage($"\nĐã áp dụng alignment targets cho width/offset.");
                    }
                    catch (System.Exception setEx)
                    {
                        A.Ed.WriteMessage($"\nCảnh báo: Không thể áp dụng alignment targets: {setEx.Message}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\nCảnh báo: Không thể đặt offset targets: {ex.Message}");
            }
        }
    }

    // Form class for corridor creation
    public partial class CorridorCreationForm : Form
    {
        public string CorridorNamePrefix { get; private set; } = "";
        public string SelectedAssemblyName { get; private set; } = "";
        public string SelectedCodeSetStyleName { get; private set; } = "";
        public ObjectId SelectedTargetSurfaceId { get; private set; } = ObjectId.Null;
        public double FrequencyDistance { get; private set; } = 25.0;
        public double StartOffset { get; private set; } = -25.0;
        public double EndOffset { get; private set; } = 25.0;
        public bool CreateSingleCorridor { get; private set; } = false;

        private System.Windows.Forms.Label lblCorridorName = null!;
        private System.Windows.Forms.Label lblAssembly = null!;
        private System.Windows.Forms.Label lblCodeSetStyle = null!;
        private System.Windows.Forms.Label lblTargetSurface = null!;
        private System.Windows.Forms.Label lblFrequency = null!;
        private System.Windows.Forms.Label lblStartOffset = null!;
        private System.Windows.Forms.Label lblEndOffset = null!;
        
        private System.Windows.Forms.TextBox txtCorridorName = null!;
        private System.Windows.Forms.ComboBox cmbAssembly = null!;
        private System.Windows.Forms.ComboBox cmbCodeSetStyle = null!;
        private System.Windows.Forms.ComboBox cmbTargetSurface = null!;
        private System.Windows.Forms.TextBox txtFrequency = null!;
        private System.Windows.Forms.TextBox txtStartOffset = null!;
        private System.Windows.Forms.TextBox txtEndOffset = null!;
        
        private System.Windows.Forms.Button btnOK = null!;
        private System.Windows.Forms.Button btnCancel = null!;
        private System.Windows.Forms.GroupBox grpBasicSettings = null!;
        private System.Windows.Forms.GroupBox grpAdvancedSettings = null!;
        private System.Windows.Forms.GroupBox grpCorridorMode = null!;
        private System.Windows.Forms.RadioButton rdbMultipleCorridors = null!;
        private System.Windows.Forms.RadioButton rdbSingleCorridor = null!;

        private ObjectId[] _surfaceIds = new ObjectId[0];

        public CorridorCreationForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            // Standard Font
            var standardFont = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular);
            var boldFont = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);

            this.lblCorridorName = new System.Windows.Forms.Label();
            this.lblAssembly = new System.Windows.Forms.Label();
            this.lblCodeSetStyle = new System.Windows.Forms.Label();
            this.lblTargetSurface = new System.Windows.Forms.Label();
            this.lblFrequency = new System.Windows.Forms.Label();
            this.lblStartOffset = new System.Windows.Forms.Label();
            this.lblEndOffset = new System.Windows.Forms.Label();
            
            this.txtCorridorName = new System.Windows.Forms.TextBox();
            this.cmbAssembly = new System.Windows.Forms.ComboBox();
            this.cmbCodeSetStyle = new System.Windows.Forms.ComboBox();
            this.cmbTargetSurface = new System.Windows.Forms.ComboBox();
            this.txtFrequency = new System.Windows.Forms.TextBox();
            this.txtStartOffset = new System.Windows.Forms.TextBox();
            this.txtEndOffset = new System.Windows.Forms.TextBox();
            
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.grpBasicSettings = new System.Windows.Forms.GroupBox();
            this.grpAdvancedSettings = new System.Windows.Forms.GroupBox();
            this.grpCorridorMode = new System.Windows.Forms.GroupBox();
            this.rdbMultipleCorridors = new System.Windows.Forms.RadioButton();
            this.rdbSingleCorridor = new System.Windows.Forms.RadioButton();

            this.SuspendLayout();

            // Form
            this.Text = "Tạo Corridor cho Tuyến Đường";
            this.Size = new System.Drawing.Size(550, 650);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Font = standardFont;

            // Corridor Mode Group
            this.grpCorridorMode.Text = "Chế độ tạo Corridor";
            this.grpCorridorMode.Font = boldFont;
            this.grpCorridorMode.Location = new System.Drawing.Point(20, 20);
            this.grpCorridorMode.Size = new System.Drawing.Size(495, 90);

            // Multiple Corridors Radio Button
            this.rdbMultipleCorridors.Text = "Tạo nhiều corridor (mỗi tuyến đường 1 corridor)";
            this.rdbMultipleCorridors.Font = standardFont;
            this.rdbMultipleCorridors.Location = new System.Drawing.Point(20, 25);
            this.rdbMultipleCorridors.Size = new System.Drawing.Size(460, 25);
            this.rdbMultipleCorridors.Checked = true;
            this.rdbMultipleCorridors.CheckedChanged += new EventHandler(this.rdb_CheckedChanged);

            // Single Corridor Radio Button
            this.rdbSingleCorridor.Text = "Tạo 1 corridor cho tất cả (mỗi tuyến đường 1 baseline)";
            this.rdbSingleCorridor.Font = standardFont;
            this.rdbSingleCorridor.Location = new System.Drawing.Point(20, 55);
            this.rdbSingleCorridor.Size = new System.Drawing.Size(460, 25);
            this.rdbSingleCorridor.CheckedChanged += new EventHandler(this.rdb_CheckedChanged);

            // Basic Settings Group
            this.grpBasicSettings.Text = "Thiết lập cơ bản";
            this.grpBasicSettings.Font = boldFont;
            this.grpBasicSettings.Location = new System.Drawing.Point(20, 120);
            this.grpBasicSettings.Size = new System.Drawing.Size(495, 220);

            // Corridor Name Label
            this.lblCorridorName.Text = "Tiền tố tên Corridor:";
            this.lblCorridorName.Font = standardFont;
            this.lblCorridorName.Location = new System.Drawing.Point(20, 35);
            this.lblCorridorName.Size = new System.Drawing.Size(180, 25);

            // Corridor Name TextBox
            this.txtCorridorName.Location = new System.Drawing.Point(200, 32);
            this.txtCorridorName.Size = new System.Drawing.Size(270, 25);
            this.txtCorridorName.Font = standardFont;
            this.txtCorridorName.Text = "Corridor_";

            // Assembly Label
            this.lblAssembly.Text = "Mẫu mặt cắt (Assembly):";
            this.lblAssembly.Font = standardFont;
            this.lblAssembly.Location = new System.Drawing.Point(20, 75);
            this.lblAssembly.Size = new System.Drawing.Size(180, 25);

            // Assembly ComboBox
            this.cmbAssembly.Location = new System.Drawing.Point(200, 72);
            this.cmbAssembly.Size = new System.Drawing.Size(270, 25);
            this.cmbAssembly.Font = standardFont;
            this.cmbAssembly.DropDownStyle = ComboBoxStyle.DropDownList;

            // Code Set Style Label
            this.lblCodeSetStyle.Text = "Code Set Style:";
            this.lblCodeSetStyle.Font = standardFont;
            this.lblCodeSetStyle.Location = new System.Drawing.Point(20, 115);
            this.lblCodeSetStyle.Size = new System.Drawing.Size(180, 25);

            // Code Set Style ComboBox
            this.cmbCodeSetStyle.Location = new System.Drawing.Point(200, 112);
            this.cmbCodeSetStyle.Size = new System.Drawing.Size(270, 25);
            this.cmbCodeSetStyle.Font = standardFont;
            this.cmbCodeSetStyle.DropDownStyle = ComboBoxStyle.DropDownList;

            // Target Surface Label
            this.lblTargetSurface.Text = "Bề mặt tham chiếu:";
            this.lblTargetSurface.Font = standardFont;
            this.lblTargetSurface.Location = new System.Drawing.Point(20, 155);
            this.lblTargetSurface.Size = new System.Drawing.Size(180, 25);

            // Target Surface ComboBox
            this.cmbTargetSurface.Location = new System.Drawing.Point(200, 152);
            this.cmbTargetSurface.Size = new System.Drawing.Size(270, 25);
            this.cmbTargetSurface.Font = standardFont;
            this.cmbTargetSurface.DropDownStyle = ComboBoxStyle.DropDownList;

            // Advanced Settings Group
            this.grpAdvancedSettings.Text = "Thiết lập nâng cao";
            this.grpAdvancedSettings.Font = boldFont;
            this.grpAdvancedSettings.Location = new System.Drawing.Point(20, 350);
            this.grpAdvancedSettings.Size = new System.Drawing.Size(495, 180);

            // Frequency Label
            this.lblFrequency.Text = "Khoảng cách section (m):";
            this.lblFrequency.Font = standardFont;
            this.lblFrequency.Location = new System.Drawing.Point(20, 35);
            this.lblFrequency.Size = new System.Drawing.Size(180, 25);

            // Frequency TextBox
            this.txtFrequency.Location = new System.Drawing.Point(200, 32);
            this.txtFrequency.Size = new System.Drawing.Size(120, 25);
            this.txtFrequency.Font = standardFont;
            this.txtFrequency.Text = "25.0";

            // Start Offset Label
            this.lblStartOffset.Text = "Offset bắt đầu (m):";
            this.lblStartOffset.Font = standardFont;
            this.lblStartOffset.Location = new System.Drawing.Point(20, 75);
            this.lblStartOffset.Size = new System.Drawing.Size(180, 25);

            // Start Offset TextBox
            this.txtStartOffset.Location = new System.Drawing.Point(200, 72);
            this.txtStartOffset.Size = new System.Drawing.Size(120, 25);
            this.txtStartOffset.Font = standardFont;
            this.txtStartOffset.Text = "-25.0";

            // End Offset Label
            this.lblEndOffset.Text = "Offset kết thúc (m):";
            this.lblEndOffset.Font = standardFont;
            this.lblEndOffset.Location = new System.Drawing.Point(20, 115);
            this.lblEndOffset.Size = new System.Drawing.Size(180, 25);

            // End Offset TextBox
            this.txtEndOffset.Location = new System.Drawing.Point(200, 112);
            this.txtEndOffset.Size = new System.Drawing.Size(120, 25);
            this.txtEndOffset.Font = standardFont;
            this.txtEndOffset.Text = "25.0";

            // OK Button
            this.btnOK.Text = "Tạo Corridor";
            this.btnOK.Location = new System.Drawing.Point(290, 560);
            this.btnOK.Size = new System.Drawing.Size(110, 35);
            this.btnOK.Font = boldFont;
            this.btnOK.Click += new EventHandler(this.btnOK_Click);

            // Cancel Button
            this.btnCancel.Text = "Hủy";
            this.btnCancel.Location = new System.Drawing.Point(415, 560);
            this.btnCancel.Size = new System.Drawing.Size(100, 35);
            this.btnCancel.Font = standardFont;
            this.btnCancel.Click += new EventHandler(this.btnCancel_Click);

            // Add controls to groups
            this.grpCorridorMode.Controls.Add(this.rdbMultipleCorridors);
            this.grpCorridorMode.Controls.Add(this.rdbSingleCorridor);

            this.grpBasicSettings.Controls.Add(this.lblCorridorName);
            this.grpBasicSettings.Controls.Add(this.txtCorridorName);
            this.grpBasicSettings.Controls.Add(this.lblAssembly);
            this.grpBasicSettings.Controls.Add(this.cmbAssembly);
            this.grpBasicSettings.Controls.Add(this.lblCodeSetStyle);
            this.grpBasicSettings.Controls.Add(this.cmbCodeSetStyle);
            this.grpBasicSettings.Controls.Add(this.lblTargetSurface);
            this.grpBasicSettings.Controls.Add(this.cmbTargetSurface);

            this.grpAdvancedSettings.Controls.Add(this.lblFrequency);
            this.grpAdvancedSettings.Controls.Add(this.txtFrequency);
            this.grpAdvancedSettings.Controls.Add(this.lblStartOffset);
            this.grpAdvancedSettings.Controls.Add(this.txtStartOffset);
            this.grpAdvancedSettings.Controls.Add(this.lblEndOffset);
            this.grpAdvancedSettings.Controls.Add(this.txtEndOffset);

            // Add controls to form
            this.Controls.Add(this.grpCorridorMode);
            this.Controls.Add(this.grpBasicSettings);
            this.Controls.Add(this.grpAdvancedSettings);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.btnCancel);

            this.ResumeLayout(false);
        }

        private void rdb_CheckedChanged(object? sender, EventArgs e)
        {
            if (rdbSingleCorridor.Checked)
            {
                lblCorridorName.Text = "Tên Corridor:";
                txtCorridorName.Text = txtCorridorName.Text.Replace("Corridor_", "CorridorCombined");
            }
            else
            {
                lblCorridorName.Text = "Tiền tố tên Corridor:";
                txtCorridorName.Text = txtCorridorName.Text.Replace("CorridorCombined", "Corridor_");
            }
        }

        public void SetAvailableOptions(string[] assemblyNames, string[] codeSetStyleNames, string[] surfaceNames,
                                      string lastCorridorName, string lastAssemblyName, string lastCodeSetStyleName,
                                      ObjectId lastTargetSurfaceId, ObjectId[] surfaceIds,
                                      double lastFrequencyDistance, double lastStartOffset, double lastEndOffset,
                                      bool lastCreateSingleCorridor)
        {
            // Store surface IDs for later reference
            _surfaceIds = surfaceIds;

            // Set assembly options
            this.cmbAssembly.Items.Clear();
            this.cmbAssembly.Items.AddRange(assemblyNames);
            if (!string.IsNullOrEmpty(lastAssemblyName) && this.cmbAssembly.Items.Contains(lastAssemblyName))
            {
                this.cmbAssembly.SelectedItem = lastAssemblyName;
            }
            else if (this.cmbAssembly.Items.Count > 0)
            {
                this.cmbAssembly.SelectedIndex = 0;
            }

            // Set code set style options
            this.cmbCodeSetStyle.Items.Clear();
            this.cmbCodeSetStyle.Items.AddRange(codeSetStyleNames);
            if (!string.IsNullOrEmpty(lastCodeSetStyleName) && this.cmbCodeSetStyle.Items.Contains(lastCodeSetStyleName))
            {
                this.cmbCodeSetStyle.SelectedItem = lastCodeSetStyleName;
            }
            else if (this.cmbCodeSetStyle.Items.Count > 0)
            {
                this.cmbCodeSetStyle.SelectedIndex = 0;
            }

            // Set surface options
            this.cmbTargetSurface.Items.Clear();
            this.cmbTargetSurface.Items.Add("(Không chọn)");
            this.cmbTargetSurface.Items.AddRange(surfaceNames);
            this.cmbTargetSurface.SelectedIndex = 0; // Default to "Không chọn"

            // Try to find and select the last target surface
            if (lastTargetSurfaceId != ObjectId.Null)
            {
                for (int i = 0; i < surfaceIds.Length; i++)
                {
                    if (surfaceIds[i] == lastTargetSurfaceId && i + 1 < this.cmbTargetSurface.Items.Count)
                    {
                        this.cmbTargetSurface.SelectedIndex = i + 1; // +1 because of "(Không chọn)" item
                        break;
                    }
                }
            }

            // Set corridor mode
            this.rdbSingleCorridor.Checked = lastCreateSingleCorridor;
            this.rdbMultipleCorridors.Checked = !lastCreateSingleCorridor;

            // Set other default values
            this.txtCorridorName.Text = lastCorridorName;
            this.txtFrequency.Text = lastFrequencyDistance.ToString("F1");
            this.txtStartOffset.Text = lastStartOffset.ToString("F1");
            this.txtEndOffset.Text = lastEndOffset.ToString("F1");

            // Update label based on mode
            rdb_CheckedChanged(null, EventArgs.Empty);
        }

        private void btnOK_Click(object? sender, EventArgs e)
        {
            try
            {
                // Validate corridor name prefix
                if (string.IsNullOrWhiteSpace(txtCorridorName.Text))
                {
                    MessageBox.Show("Vui lòng nhập tên corridor.", "Lỗi", 
                                  MessageBoxButtons.OK, MessageBoxIcon.Error);
                    txtCorridorName.Focus();
                    return;
                }

                // Validate assembly selection
                if (cmbAssembly.SelectedIndex == -1)
                {
                    MessageBox.Show("Vui lòng chọn mẫu mặt cắt (Assembly).", "Lỗi", 
                                  MessageBoxButtons.OK, MessageBoxIcon.Error);
                    cmbAssembly.Focus();
                    return;
                }

                // Validate code set style selection
                if (cmbCodeSetStyle.SelectedIndex == -1)
                {
                    MessageBox.Show("Vui lòng chọn Code Set Style.", "Lỗi", 
                                  MessageBoxButtons.OK, MessageBoxIcon.Error);
                    cmbCodeSetStyle.Focus();
                    return;
                }

                // Validate and parse frequency distance
                if (!double.TryParse(txtFrequency.Text, out double frequencyDistance) || frequencyDistance <= 0)
                {
                    MessageBox.Show("Khoảng cách section phải là số dương.", "Lỗi", 
                                  MessageBoxButtons.OK, MessageBoxIcon.Error);
                    txtFrequency.Focus();
                    return;
                }

                // Validate and parse start offset
                if (!double.TryParse(txtStartOffset.Text, out double startOffset))
                {
                    MessageBox.Show("Offset bắt đầu không hợp lệ. Vui lòng nhập số thực.", "Lỗi", 
                                  MessageBoxButtons.OK, MessageBoxIcon.Error);
                    txtStartOffset.Focus();
                    return;
                }

                // Validate and parse end offset
                if (!double.TryParse(txtEndOffset.Text, out double endOffset))
                {
                    MessageBox.Show("Offset kết thúc không hợp lệ. Vui lòng nhập số thực.", "Lỗi", 
                                  MessageBoxButtons.OK, MessageBoxIcon.Error);
                    txtEndOffset.Focus();
                    return;
                }

                // Validate offset range
                if (startOffset >= endOffset)
                {
                    MessageBox.Show("Offset bắt đầu phải nhỏ hơn offset kết thúc.", "Lỗi", 
                                  MessageBoxButtons.OK, MessageBoxIcon.Error);
                    txtStartOffset.Focus();
                    return;
                }

                // Set return values
                CorridorNamePrefix = txtCorridorName.Text.Trim();
                SelectedAssemblyName = cmbAssembly.SelectedItem?.ToString() ?? "";
                SelectedCodeSetStyleName = cmbCodeSetStyle.SelectedItem?.ToString() ?? "";
                CreateSingleCorridor = rdbSingleCorridor.Checked;
                
                // Set target surface ID
                if (cmbTargetSurface.SelectedIndex > 0) // 0 is "(Không chọn)"
                {
                    int surfaceIndex = cmbTargetSurface.SelectedIndex - 1;
                    if (surfaceIndex >= 0 && surfaceIndex < _surfaceIds.Length)
                    {
                        SelectedTargetSurfaceId = _surfaceIds[surfaceIndex];
                    }
                }
                else
                {
                    SelectedTargetSurfaceId = ObjectId.Null;
                }

                FrequencyDistance = frequencyDistance;
                StartOffset = startOffset;
                EndOffset = endOffset;
                
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi", 
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnCancel_Click(object? sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}

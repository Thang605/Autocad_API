using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using CadEntity = Autodesk.AutoCAD.DatabaseServices.Entity;
using CivSurface = Autodesk.Civil.DatabaseServices.TinSurface;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsButton = System.Windows.Forms.Button;
using WinFormsTextBox = System.Windows.Forms.TextBox;
using WinFormsComboBox = System.Windows.Forms.ComboBox;
using FlowDirection = System.Windows.Forms.FlowDirection;
using Font = System.Drawing.Font;
using WinFormsColor = System.Drawing.Color;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace MyFirstProject.Civil_Tool_2
{
    public partial class TaoMatPhangTuFeaturelineForm : Form
    {
        // ═══════════════════════════════════════════════════════════════
        //  PERSISTENT STATE (BẮT BUỘC THEO QUY TẮC RULE 2)
        // ═══════════════════════════════════════════════════════════════
        private static Size _lastFormSize = new Size(1080, 790);
        private static bool _lastCreateNewSurface = true;
        private static string _lastSurfaceName = "";
        private static ObjectId _lastExistingSurfaceId = ObjectId.Null;
        private static string _lastExistingSurfaceName = "";
        private static bool _lastUseManagedSubSurface = true;
        private static string _lastSubSurfaceSuffix = "";
        private static string _lastSubSurfaceStyle = "_No Display";
        private static string _lastMasterSurfaceStyle = "";
        private static int _lastFilterSourceIdx = 0;
        private static string _lastFilterValue = "";
        private static FeatureLineBoundaryMode _lastBoundaryMode = FeatureLineBoundaryMode.AutoSmart;
        private static double _lastMidOrdDistance = 0.10;
        private static bool _lastUseSupplementing = true;
        private static double _lastSupplementingDistance = 2.00;
        private static bool _lastLimitMaxEdge = true;
        private static double _lastMaxEdgeLength = 25.00;
        private static bool _lastAutoClose = true;
        private static bool _lastTreatNestedAsHole = false;
        private static bool _lastRebuildAuto = true;
        private static bool _lastOverwriteOverlap = true;
        private static bool _lastSafeCorridorCombine = true;
        private static List<ObjectId> _lastSelectedIds = new List<ObjectId>();
        private static List<ObjectId> _lastCustomOuterIds = new List<ObjectId>();
        private static List<ObjectId> _lastCustomHideIds = new List<ObjectId>();
        private static List<ObjectId> _lastExecutedIds = new List<ObjectId>();
        private static List<ObjectId> _lastExecutedOuterIds = new List<ObjectId>();
        private static List<ObjectId> _lastExecutedHideIds = new List<ObjectId>();
        private static string _lastExecutedTargetDesc = "";

        // ═══════════════════════════════════════════════════════════════
        //  FIELDS
        // ═══════════════════════════════════════════════════════════════
        private readonly Database _db;
        private readonly Editor _ed;
        private readonly List<SelectedFeatureLineBoundaryItem> _items = new List<SelectedFeatureLineBoundaryItem>();
        private readonly List<ObjectId> _customOuterIds = new List<ObjectId>();
        private readonly List<ObjectId> _customHideIds = new List<ObjectId>();
        private readonly Dictionary<string, ObjectId> _existingSurfaceMap = new Dictionary<string, ObjectId>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, SurfaceOriginationType> _surfaceOriginationMap = new Dictionary<string, SurfaceOriginationType>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _surfaceTriCountMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly List<Font> _managedFonts = new List<Font>();
        private Font _fontBold9 = null!;
        private Font _fontRegular9 = null!;
        private ToolTip _toolTip = null!;
        private bool _suppressSubSuffixTracking = false;
        private bool _userCustomizedSubSuffix = false;
        private bool _completedSuccessfully = false;

        // Dữ liệu cache cho bộ lọc nạp nhanh
        private readonly Dictionary<string, List<ObjectId>> _bySite = new Dictionary<string, List<ObjectId>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<ObjectId>> _byLayer = new Dictionary<string, List<ObjectId>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<ObjectId>> _byCorridor = new Dictionary<string, List<ObjectId>>(StringComparer.OrdinalIgnoreCase);
        private readonly List<ObjectId> _allFeatureLineIdsInDrawing = new List<ObjectId>();

        // UI Controls
        private ListView lvObjects = null!;
        private WinFormsLabel lblSummary = null!;
        private WinFormsLabel lblStatus = null!;
        private WinFormsComboBox cboFilterSource = null!;
        private WinFormsComboBox cboFilterValue = null!;
        private WinFormsButton btnLoadByFilter = null!;
        private WinFormsButton btnSelectNew = null!;
        private WinFormsButton btnSelectAdd = null!;
        private WinFormsButton btnRestoreLast = null!;
        private WinFormsButton btnRemoveSelected = null!;
        private WinFormsButton btnClearAll = null!;
        private CheckBox chkAutoClose = null!;

        private RadioButton radNewSurface = null!;
        private RadioButton radExistingSurface = null!;
        private WinFormsTextBox txtSurfaceName = null!;
        private WinFormsButton btnGenName = null!;
        private WinFormsComboBox cboExistingSurfaces = null!;
        private WinFormsButton btnPickExistingSurface = null!;
        private Panel pnlExistingOptions = null!;
        private WinFormsLabel lblExistingSurfInfo = null!;
        private CheckBox chkCreateManagedSubSurface = null!;
        private Panel pnlSubSurfaceConfig = null!;
        private WinFormsLabel lblSubPrefix = null!;
        private WinFormsTextBox txtSubSurfaceSuffix = null!;
        private WinFormsButton btnNextSubSuffix = null!;
        private WinFormsLabel lblSubFullNamePreview = null!;
        private WinFormsComboBox cboSubSurfaceStyle = null!;
        private CheckBox chkOverwriteOverlap = null!;
        private CheckBox chkSafeCorridorCombine = null!;
        private WinFormsComboBox cboMasterStyles = null!;
        private CheckBox chkRebuildAuto = null!;

        private RadioButton radBoundaryAuto = null!;
        private RadioButton radBoundaryCustom = null!;
        private RadioButton radBoundaryNone = null!;
        private Panel pnlCustomBoundary = null!;
        private WinFormsButton btnPickOuterBnd = null!;
        private WinFormsButton btnPickHideBnd = null!;
        private WinFormsButton btnClearBnd = null!;
        private WinFormsLabel lblCustomBndInfo = null!;
        private CheckBox chkNestedClosedAsHole = null!;

        private NumericUpDown numMidOrd = null!;
        private CheckBox chkUseSupplementing = null!;
        private NumericUpDown numSupplementing = null!;
        private CheckBox chkLimitMaxEdge = null!;
        private NumericUpDown numMaxEdgeLength = null!;
        private WinFormsButton btnSuggestMaxEdge = null!;

        private ProgressBar progressBar = null!;
        private WinFormsButton btnSyncDynamic = null!;
        private WinFormsButton btnExecute = null!;
        private WinFormsButton btnClose = null!;

        public TaoMatPhangTuFeaturelineForm(Database db, Editor ed)
        {
            _db = db;
            _ed = ed;

            InitializeCustomUI();
            ScanDrawingSourcesAndStyles();
            RestoreLastSettings();
            TryRestorePreviousSelection();
            UpdateControlStates();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _toolTip?.Dispose();
                foreach (Font f in _managedFonts)
                {
                    f?.Dispose();
                }
                _managedFonts.Clear();
            }
            base.Dispose(disposing);
        }

        private Font CreateFont(float size, FontStyle style = FontStyle.Regular)
        {
            var f = new Font("Segoe UI", size, style, GraphicsUnit.Point);
            _managedFonts.Add(f);
            return f;
        }
    }
}

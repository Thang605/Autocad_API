using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Civil3DCsharp;
using MyFirstProject.Extensions;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using CivSurface = Autodesk.Civil.DatabaseServices.TinSurface;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using WinFormsColor = System.Drawing.Color;
using WinFormsFont = System.Drawing.Font;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsPoint = System.Drawing.Point;

namespace MyFirstProject.Civil_Tool
{
    public partial class KiemTraTamNhinForm : Form
    {

        private static int _lastSourceMode = 0; // 0: Alignment, 1: Polyline
        private static ObjectId _lastAlignmentId = ObjectId.Null;
        private static ObjectId _lastProfileId = ObjectId.Null;
        private static ObjectId _lastSurfaceId = ObjectId.Null;
        private static ObjectId _lastPolylineId = ObjectId.Null;

        private static string _lastStandardName = "TCVN 4054:2005";
        private static int _lastSpeed = 60;
        private static int _lastSightTypeIndex = 0; // 0: Dừng xe (SSD), 1: Vượt xe (PSD)
        private static decimal _lastReqSight = 75m;
        private static decimal _lastInterval = 20m;
        private static decimal _lastLaneOffset = 1.75m;
        private static decimal _lastEyeHeight = 1.25m;
        private static decimal _lastTargetHeight = 0.10m;

        private static bool _lastCheckGrade = true;
        private static bool _lastCheckClearance = true;
        private static bool _lastCheck3D = true;
        private static bool _lastFilterOnlyErrors = false;
        private static bool _lastDrawEyePath = true;
        private static Size _lastFormSize = new Size(1180, 780);



        // Group 1: Nguồn đường kiểm tra (Alignment vs Polyline)
        private RadioButton rbSourceAlignment;
        private RadioButton rbSourcePolyline;

        private Panel pnlAlignmentControls;
        private ComboBox cbbAlignment;
        private Button btnPickAlignment;
        private ComboBox cbbProfile;

        private Panel pnlPolylineControls;
        private TextBox txtPolylineInfo;
        private Button btnPickPolyline;
        private Button btnConvertTo3D;

        private ComboBox cbbSurface;
        private Button btnPickSurface;

        // Group 2: Tiêu chuẩn & Vận tốc
        private ComboBox cbbStandard;
        private ComboBox cbbSpeed;
        private Button btnLookupTable;
        private Button btnResetStandard;
        private RadioButton rbStoppingSight;
        private RadioButton rbPassingSight;
        private WinFormsLabel lblReqSight;
        private NumericUpDown numReqSight;
        private NumericUpDown numInterval;
        private NumericUpDown numLaneOffset;
        private NumericUpDown numEyeHeight;
        private NumericUpDown numTargetHeight;
        private Panel pnlStdSummary;
        private WinFormsLabel lblStdSummaryTitle;
        private WinFormsLabel lblStdSight;
        private WinFormsLabel lblStdVertical;
        private WinFormsLabel lblStdHorizontal;

        // Group 3: Tùy chọn kiểm tra
        private CheckBox chkCheckGrade;
        private CheckBox chkCheckClearance;
        private CheckBox chkCheck3DSurface;
        private CheckBox chkFilterOnlyErrors;
        private CheckBox chkDrawEyePath;

        // Group 4: Phạm vi lý trình
        private TextBox txtStartStation;
        private TextBox txtEndStation;

        // Group 5: Bảng kết quả & Thống kê
        private DataGridView dgvResults;
        private WinFormsLabel lblSummary;

        // Group 6: Nút hành động
        private Button btnAnalyze;
        private Button btnZoomCAD;
        private Button btnDrawRays;
        private Button btnDrawMarks;
        private Button btnExportExcel;
        private Button btnClose;


        private List<SightCheckItem> _analysisResults = new List<SightCheckItem>();
        private ObjectId _selectedPolylineId = ObjectId.Null;
        private bool _isLoading = true;
        private bool _isUpdatingSpeed = false;

        public KiemTraTamNhinForm()
        {
            _isLoading = true;
            try
            {
                InitializeCustomComponents();
                LoadCivilObjects();
                RestoreLastSettings();
            }
            finally
            {
                _isLoading = false;
            }

            // Sau khi khôi phục xong toàn bộ cài đặt người dùng, cập nhật thẻ tóm tắt chuẩn mà KHÔNG đè controls
            ApplyStandardLookup(updateControls: false);

            this.FormClosing += (s, e) => SaveCurrentSettings();
        }

        private void RbSourceMode_CheckedChanged(object sender, EventArgs e)
        {
            if (_isLoading) return;
            bool isAlign = rbSourceAlignment.Checked;
            pnlAlignmentControls.Visible = isAlign;
            pnlPolylineControls.Visible = !isAlign;

            if (isAlign)
            {
                CbbAlignment_SelectedIndexChanged(null, EventArgs.Empty);
            }
            else
            {
                UpdatePolylineStationDisplay();
            }
        }
    }
}

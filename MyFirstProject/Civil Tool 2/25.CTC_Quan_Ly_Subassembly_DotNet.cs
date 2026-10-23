using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.Runtime;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Exception = System.Exception;
using C3DSubassembly = Autodesk.Civil.DatabaseServices.Subassembly;
using C3DAssembly = Autodesk.Civil.DatabaseServices.Assembly;
using C3DShape = Autodesk.Civil.DatabaseServices.Shape;
using C3DPoint = Autodesk.Civil.DatabaseServices.Point;
using C3DLink = Autodesk.Civil.DatabaseServices.Link;
using StockSATemplate = Subassembly.SATemplate;
using StockUtilities = Subassembly.Utilities;
using StockCodes = Subassembly.CodesSpecific;

[assembly: CommandClass(typeof(Civil3DCsharp.CTC_Quan_Ly_Subassembly_DotNet_Command))]

namespace Civil3DCsharp
{
    #region 1. SDK & LỚP CƠ SỞ C# CHO SUBASSEMBLY .NET (TRÍCH XUẤT TỪ C3DStockSubassemblies & Autodesk.Civil.Runtime)

    /// <summary>
    /// Lớp cơ sở C# kế thừa trực tiếp từ Subassembly.SATemplate (C3DStockSubassemblies.dll)
    /// và bọc các hàm tiện ích từ Autodesk.Civil.Runtime + Subassembly.Utilities + CodesSpecific.
    /// Mọi Subassembly viết bằng C# chỉ cần kế thừa lớp này.
    /// </summary>
    public abstract class CustomSubassemblyBase : StockSATemplate
    {
        public const int SideRight = StockUtilities.Right; // 0
        public const int SideLeft = StockUtilities.Left;   // 1

        /// <summary>
        /// Đăng ký tham số kiểu Long (Integer) vừa là Input vừa là Output
        /// </summary>
        protected static void AddParamLong(CorridorState state, string name, int defaultValue, bool isInputOutput = true)
        {
            IParam param = state.ParamsLong.Add(name, defaultValue);
            if (param != null && isInputOutput)
            {
                param.Access = ParamAccessType.InputAndOutput;
            }
        }

        /// <summary>
        /// Đăng ký tham số kiểu Double vừa là Input vừa là Output
        /// </summary>
        protected static void AddParamDouble(CorridorState state, string name, double defaultValue, bool isInputOutput = true)
        {
            IParam param = state.ParamsDouble.Add(name, defaultValue);
            if (param != null && isInputOutput)
            {
                param.Access = ParamAccessType.InputAndOutput;
            }
        }

        /// <summary>
        /// Đăng ký tham số kiểu String vừa là Input vừa là Output
        /// </summary>
        protected static void AddParamString(CorridorState state, string name, string defaultValue, bool isInputOutput = true)
        {
            IParam param = state.ParamsString.Add(name, defaultValue);
            if (param != null && isInputOutput)
            {
                param.Access = ParamAccessType.InputAndOutput;
            }
        }

        /// <summary>
        /// Đọc giá trị tham số Long an toàn
        /// </summary>
        protected static int GetParamLong(CorridorState state, string name, int defaultValue)
        {
            try
            {
                return state.ParamsLong.Value(name);
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Đọc giá trị tham số Double an toàn
        /// </summary>
        protected static double GetParamDouble(CorridorState state, string name, double defaultValue)
        {
            try
            {
                return state.ParamsDouble.Value(name);
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Đọc giá trị tham số String an toàn
        /// </summary>
        protected static string GetParamString(CorridorState state, string name, string defaultValue)
        {
            try
            {
                return state.ParamsString.Value(name);
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Hệ số lật phía Trái (-1.0) / Phải (+1.0)
        /// </summary>
        protected static double GetSideFlip(int side)
        {
            return (side == SideLeft) ? -1.0 : 1.0;
        }

        /// <summary>
        /// Thêm Point vào mặt cắt Subassembly kèm danh sách Point Codes
        /// </summary>
        protected static C3DPoint AddCorridorPoint(CorridorState state, double dx, double dy, params string[] codes)
        {
            C3DPoint pt = state.Points.Add(dx, dy, "");
            if (codes != null)
            {
                foreach (string c in codes)
                {
                    if (!string.IsNullOrWhiteSpace(c))
                        pt.Codes.TryAdd(c);
                }
            }
            return pt;
        }

        /// <summary>
        /// Thêm Link nối 2 Point vào mặt cắt Subassembly kèm danh sách Link Codes
        /// </summary>
        protected static C3DLink AddCorridorLink(CorridorState state, C3DPoint pt1, C3DPoint pt2, params string[] codes)
        {
            C3DPoint[] pts = new C3DPoint[] { pt1, pt2 };
            C3DLink lnk = state.Links.Add(pts, "");
            if (codes != null)
            {
                foreach (string c in codes)
                {
                    if (!string.IsNullOrWhiteSpace(c))
                        lnk.Codes.TryAdd(c);
                }
            }
            return lnk;
        }

        /// <summary>
        /// Thêm Shape khép kín từ mảng các Link kèm danh sách Shape Codes
        /// </summary>
        protected static C3DShape AddCorridorShape(CorridorState state, C3DLink[] links, params string[] codes)
        {
            C3DShape shp = state.Shapes.Add(links, "");
            if (codes != null)
            {
                foreach (string c in codes)
                {
                    if (!string.IsNullOrWhiteSpace(c))
                        shp.Codes.TryAdd(c);
                }
            }
            return shp;
        }
    }

    #endregion

    #region 2. CÁC CLASS SUBASSEMBLY C# MẪU (CHUYỂN NGỮ TỪ C3DStockSubassemblies SANG C#)

    /// <summary>
    /// [1] Làn xe cơ bản 1 lớp viết bằng C# (Chuyển ngữ từ Subassembly.BasicLane của Civil 3D)
    /// </summary>
    public class CustomBasicLaneCS : CustomSubassemblyBase
    {
        private const int DefaultSide = SideRight;
        private const double DefaultWidth = 3.50;
        private const double DefaultDepth = 0.20;
        private const double DefaultSlope = -0.02;

        protected override void GetInputParametersImplement(CorridorState corridorState)
        {
            base.GetInputParametersImplement(corridorState);
            AddParamLong(corridorState, StockUtilities.Side, DefaultSide, false);
            AddParamDouble(corridorState, "Width", DefaultWidth, false);
            AddParamDouble(corridorState, "Depth", DefaultDepth, false);
            AddParamDouble(corridorState, "Slope", DefaultSlope, false);
        }

        protected override void GetOutputParametersImplement(CorridorState corridorState)
        {
            base.GetOutputParametersImplement(corridorState);
            AddParamLong(corridorState, StockUtilities.Side, DefaultSide, true);
            AddParamDouble(corridorState, "Width", DefaultWidth, true);
            AddParamDouble(corridorState, "Depth", DefaultDepth, true);
            AddParamDouble(corridorState, "Slope", DefaultSlope, true);
        }

        protected override void DrawImplement(CorridorState corridorState)
        {
            int side = GetParamLong(corridorState, StockUtilities.Side, DefaultSide);
            double flip = GetSideFlip(side);

            double width = GetParamDouble(corridorState, "Width", DefaultWidth);
            double depth = GetParamDouble(corridorState, "Depth", DefaultDepth);
            double slope = GetParamDouble(corridorState, "Slope", DefaultSlope);

            if (width <= 0)
            {
                StockUtilities.RecordError(corridorState, CorridorError.ValueShouldNotBeLessThanOrEqualToZero, "Width", nameof(CustomBasicLaneCS));
                width = DefaultWidth;
            }
            if (depth <= 0)
            {
                StockUtilities.RecordError(corridorState, CorridorError.ValueShouldNotBeLessThanOrEqualToZero, "Depth", nameof(CustomBasicLaneCS));
                depth = DefaultDepth;
            }

            double x0 = 0.0, y0 = 0.0;
            double x1 = flip * width, y1 = width * slope;
            double x2 = x0, y2 = y0 - depth;
            double x3 = x1, y3 = y1 - depth;

            C3DPoint p1 = AddCorridorPoint(corridorState, x0, y0, StockCodes.Codes.Crown.Code);
            C3DPoint p2 = AddCorridorPoint(corridorState, x1, y1, StockCodes.Codes.ETW.Code);
            C3DPoint p3 = AddCorridorPoint(corridorState, x2, y2, StockCodes.Codes.CrownSubBase.Code);
            C3DPoint p4 = AddCorridorPoint(corridorState, x3, y3, StockCodes.Codes.ETWSubBase.Code);

            C3DLink l1 = AddCorridorLink(corridorState, p1, p2, StockCodes.Codes.Top.Code, StockCodes.Codes.Pave.Code);
            C3DLink l2 = AddCorridorLink(corridorState, p2, p4);
            C3DLink l3 = AddCorridorLink(corridorState, p4, p3, StockCodes.Codes.Datum.Code, StockCodes.Codes.SubBase.Code);
            C3DLink l4 = AddCorridorLink(corridorState, p3, p1);

            AddCorridorShape(corridorState, new[] { l1, l2, l3, l4 }, StockCodes.Codes.Pave1.Code);

            AddParamLong(corridorState, StockUtilities.Side, side, true);
            AddParamDouble(corridorState, "Width", width, true);
            AddParamDouble(corridorState, "Depth", depth, true);
            AddParamDouble(corridorState, "Slope", slope, true);
        }
    }

    /// <summary>
    /// [2] Kết cấu áo đường 4 lớp viết bằng C# (Pave1, Pave2, Base, SubBase)
    /// </summary>
    public class CustomMultiLayerLaneCS : CustomSubassemblyBase
    {
        private const int DefaultSide = SideRight;
        private const double DefaultWidth = 3.50;
        private const double DefaultSlope = -0.02;
        private const double DefaultPave1Depth = 0.05;
        private const double DefaultPave2Depth = 0.07;
        private const double DefaultBaseDepth = 0.18;
        private const double DefaultSubbaseDepth = 0.30;

        protected override void GetInputParametersImplement(CorridorState corridorState)
        {
            base.GetInputParametersImplement(corridorState);
            AddParamLong(corridorState, StockUtilities.Side, DefaultSide, false);
            AddParamDouble(corridorState, "Width", DefaultWidth, false);
            AddParamDouble(corridorState, "Slope", DefaultSlope, false);
            AddParamDouble(corridorState, "Pave1Depth", DefaultPave1Depth, false);
            AddParamDouble(corridorState, "Pave2Depth", DefaultPave2Depth, false);
            AddParamDouble(corridorState, "BaseDepth", DefaultBaseDepth, false);
            AddParamDouble(corridorState, "SubbaseDepth", DefaultSubbaseDepth, false);
        }

        protected override void GetOutputParametersImplement(CorridorState corridorState)
        {
            base.GetOutputParametersImplement(corridorState);
            AddParamLong(corridorState, StockUtilities.Side, DefaultSide, true);
            AddParamDouble(corridorState, "Width", DefaultWidth, true);
            AddParamDouble(corridorState, "Slope", DefaultSlope, true);
            AddParamDouble(corridorState, "Pave1Depth", DefaultPave1Depth, true);
            AddParamDouble(corridorState, "Pave2Depth", DefaultPave2Depth, true);
            AddParamDouble(corridorState, "BaseDepth", DefaultBaseDepth, true);
            AddParamDouble(corridorState, "SubbaseDepth", DefaultSubbaseDepth, true);
        }

        protected override void DrawImplement(CorridorState corridorState)
        {
            int side = GetParamLong(corridorState, StockUtilities.Side, DefaultSide);
            double flip = GetSideFlip(side);

            double width = Math.Max(0.1, GetParamDouble(corridorState, "Width", DefaultWidth));
            double slope = GetParamDouble(corridorState, "Slope", DefaultSlope);
            double d1 = Math.Max(0.0, GetParamDouble(corridorState, "Pave1Depth", DefaultPave1Depth));
            double d2 = Math.Max(0.0, GetParamDouble(corridorState, "Pave2Depth", DefaultPave2Depth));
            double d3 = Math.Max(0.0, GetParamDouble(corridorState, "BaseDepth", DefaultBaseDepth));
            double d4 = Math.Max(0.0, GetParamDouble(corridorState, "SubbaseDepth", DefaultSubbaseDepth));

            double[] depths = { d1, d2, d3, d4 };
            string[] shapeCodes =
            {
                StockCodes.Codes.Pave1.Code,
                StockCodes.Codes.Pave2.Code,
                StockCodes.Codes.Base.Code,
                StockCodes.Codes.SubBase.Code
            };
            string[] innerPointCodes =
            {
                StockCodes.Codes.CrownPave1.Code,
                StockCodes.Codes.CrownPave2.Code,
                StockCodes.Codes.CrownBase.Code,
                StockCodes.Codes.CrownSub.Code
            };
            string[] outerPointCodes =
            {
                StockCodes.Codes.ETWPave1.Code,
                StockCodes.Codes.ETWPave2.Code,
                StockCodes.Codes.ETWBase.Code,
                StockCodes.Codes.ETWSub.Code
            };

            double xIn = 0.0;
            double xOut = flip * width;
            double dySlope = width * slope;

            double currentDepth = 0.0;
            C3DPoint topIn = AddCorridorPoint(corridorState, xIn, 0.0, StockCodes.Codes.Crown.Code);
            C3DPoint topOut = AddCorridorPoint(corridorState, xOut, dySlope, StockCodes.Codes.ETW.Code);
            C3DLink topLink = AddCorridorLink(corridorState, topIn, topOut, StockCodes.Codes.Top.Code, StockCodes.Codes.Pave.Code);

            int lastValidIdx = -1;
            for (int i = depths.Length - 1; i >= 0; i--)
            {
                if (depths[i] > 1e-5) { lastValidIdx = i; break; }
            }

            for (int i = 0; i < depths.Length; i++)
            {
                if (depths[i] <= 1e-5) continue;

                currentDepth += depths[i];
                bool isBottomLayer = (i == lastValidIdx);

                C3DPoint botIn = AddCorridorPoint(corridorState, xIn, -currentDepth, innerPointCodes[i]);
                C3DPoint botOut = AddCorridorPoint(corridorState, xOut, dySlope - currentDepth, outerPointCodes[i]);

                C3DLink rightLink = AddCorridorLink(corridorState, topOut, botOut);
                C3DLink botLink = isBottomLayer
                    ? AddCorridorLink(corridorState, botOut, botIn, StockCodes.Codes.Datum.Code, StockCodes.Codes.SubBase.Code)
                    : AddCorridorLink(corridorState, botOut, botIn, shapeCodes[i]);
                C3DLink leftLink = AddCorridorLink(corridorState, botIn, topIn);

                AddCorridorShape(corridorState, new[] { topLink, rightLink, botLink, leftLink }, shapeCodes[i]);

                topIn = botIn;
                topOut = botOut;
                topLink = botLink;
            }
        }
    }

    /// <summary>
    /// [3] Bó vỉa & Đan rãnh viết bằng C# (Chuyển ngữ từ Subassembly.BasicCurbAndGutter)
    /// </summary>
    public class CustomCurbAndGutterCS : CustomSubassemblyBase
    {
        private const int DefaultSide = SideRight;
        private const double DefaultGutterWidth = 0.30;
        private const double DefaultGutterSlope = -0.05;
        private const double DefaultCurbWidth = 0.18;
        private const double DefaultCurbHeight = 0.15;
        private const double DefaultCurbDepth = 0.35;

        protected override void GetInputParametersImplement(CorridorState corridorState)
        {
            base.GetInputParametersImplement(corridorState);
            AddParamLong(corridorState, StockUtilities.Side, DefaultSide, false);
            AddParamDouble(corridorState, "GutterWidth", DefaultGutterWidth, false);
            AddParamDouble(corridorState, "GutterSlope", DefaultGutterSlope, false);
            AddParamDouble(corridorState, "CurbWidth", DefaultCurbWidth, false);
            AddParamDouble(corridorState, "CurbHeight", DefaultCurbHeight, false);
            AddParamDouble(corridorState, "CurbDepth", DefaultCurbDepth, false);
        }

        protected override void GetOutputParametersImplement(CorridorState corridorState)
        {
            base.GetOutputParametersImplement(corridorState);
            AddParamLong(corridorState, StockUtilities.Side, DefaultSide, true);
            AddParamDouble(corridorState, "GutterWidth", DefaultGutterWidth, true);
            AddParamDouble(corridorState, "GutterSlope", DefaultGutterSlope, true);
            AddParamDouble(corridorState, "CurbWidth", DefaultCurbWidth, true);
            AddParamDouble(corridorState, "CurbHeight", DefaultCurbHeight, true);
            AddParamDouble(corridorState, "CurbDepth", DefaultCurbDepth, true);
        }

        protected override void DrawImplement(CorridorState corridorState)
        {
            int side = GetParamLong(corridorState, StockUtilities.Side, DefaultSide);
            double flip = GetSideFlip(side);

            double gw = Math.Max(0.05, GetParamDouble(corridorState, "GutterWidth", DefaultGutterWidth));
            double gs = GetParamDouble(corridorState, "GutterSlope", DefaultGutterSlope);
            double cw = Math.Max(0.05, GetParamDouble(corridorState, "CurbWidth", DefaultCurbWidth));
            double ch = Math.Max(0.05, GetParamDouble(corridorState, "CurbHeight", DefaultCurbHeight));
            double cd = Math.Max(ch + 0.05, GetParamDouble(corridorState, "CurbDepth", DefaultCurbDepth));

            double x0 = 0.0, y0 = 0.0;
            double x1 = flip * gw, y1 = gw * gs;
            double x2 = flip * (gw + 0.03), y2 = y1 + ch;
            double x3 = flip * (gw + cw), y3 = y2;
            double x4 = x3, y4 = y1 + ch - cd;
            double x5 = 0.0, y5 = y4;

            C3DPoint p0 = AddCorridorPoint(corridorState, x0, y0, StockCodes.Codes.Flange.Code);
            C3DPoint p1 = AddCorridorPoint(corridorState, x1, y1, StockCodes.Codes.Flowline_Gutter.Code);
            C3DPoint p2 = AddCorridorPoint(corridorState, x2, y2, StockCodes.Codes.TopCurb.Code);
            C3DPoint p3 = AddCorridorPoint(corridorState, x3, y3, StockCodes.Codes.BackCurb.Code);
            C3DPoint p4 = AddCorridorPoint(corridorState, x4, y4, StockCodes.Codes.BottomCurb.Code);
            C3DPoint p5 = AddCorridorPoint(corridorState, x5, y5);

            C3DLink l0 = AddCorridorLink(corridorState, p0, p1, StockCodes.Codes.Top.Code, StockCodes.Codes.Curb.Code);
            C3DLink l1 = AddCorridorLink(corridorState, p1, p2, StockCodes.Codes.Top.Code, StockCodes.Codes.Curb.Code);
            C3DLink l2 = AddCorridorLink(corridorState, p2, p3, StockCodes.Codes.Top.Code, StockCodes.Codes.Curb.Code);
            C3DLink l3 = AddCorridorLink(corridorState, p3, p4, StockCodes.Codes.Curb.Code);
            C3DLink l4 = AddCorridorLink(corridorState, p4, p5, StockCodes.Codes.Datum.Code, StockCodes.Codes.Curb.Code);
            C3DLink l5 = AddCorridorLink(corridorState, p5, p0, StockCodes.Codes.Curb.Code);

            AddCorridorShape(corridorState, new[] { l0, l1, l2, l3, l4, l5 }, StockCodes.Codes.Curb.Code);
        }
    }

    /// <summary>
    /// [4] Rãnh biên hình thang có lớp gia cố viết bằng C# (Chuyển ngữ từ Subassembly.SideDitch)
    /// </summary>
    public class CustomSideDitchCS : CustomSubassemblyBase
    {
        private const int DefaultSide = SideRight;
        private const double DefaultForeslopeWidth = 0.80;
        private const double DefaultForeslope = -0.50;
        private const double DefaultBottomWidth = 0.40;
        private const double DefaultBackslopeWidth = 0.80;
        private const double DefaultBackslope = 0.50;
        private const double DefaultLiningDepth = 0.10;

        protected override void GetInputParametersImplement(CorridorState corridorState)
        {
            base.GetInputParametersImplement(corridorState);
            AddParamLong(corridorState, StockUtilities.Side, DefaultSide, false);
            AddParamDouble(corridorState, "ForeslopeWidth", DefaultForeslopeWidth, false);
            AddParamDouble(corridorState, "Foreslope", DefaultForeslope, false);
            AddParamDouble(corridorState, "BottomWidth", DefaultBottomWidth, false);
            AddParamDouble(corridorState, "BackslopeWidth", DefaultBackslopeWidth, false);
            AddParamDouble(corridorState, "Backslope", DefaultBackslope, false);
            AddParamDouble(corridorState, "LiningDepth", DefaultLiningDepth, false);
        }

        protected override void GetOutputParametersImplement(CorridorState corridorState)
        {
            base.GetOutputParametersImplement(corridorState);
            AddParamLong(corridorState, StockUtilities.Side, DefaultSide, true);
            AddParamDouble(corridorState, "ForeslopeWidth", DefaultForeslopeWidth, true);
            AddParamDouble(corridorState, "Foreslope", DefaultForeslope, true);
            AddParamDouble(corridorState, "BottomWidth", DefaultBottomWidth, true);
            AddParamDouble(corridorState, "BackslopeWidth", DefaultBackslopeWidth, true);
            AddParamDouble(corridorState, "Backslope", DefaultBackslope, true);
            AddParamDouble(corridorState, "LiningDepth", DefaultLiningDepth, true);
        }

        protected override void DrawImplement(CorridorState corridorState)
        {
            int side = GetParamLong(corridorState, StockUtilities.Side, DefaultSide);
            double flip = GetSideFlip(side);

            double fWidth = Math.Max(0.1, GetParamDouble(corridorState, "ForeslopeWidth", DefaultForeslopeWidth));
            double fSlope = GetParamDouble(corridorState, "Foreslope", DefaultForeslope);
            double bWidth = Math.Max(0.0, GetParamDouble(corridorState, "BottomWidth", DefaultBottomWidth));
            double bkWidth = Math.Max(0.1, GetParamDouble(corridorState, "BackslopeWidth", DefaultBackslopeWidth));
            double bkSlope = GetParamDouble(corridorState, "Backslope", DefaultBackslope);
            double lining = Math.Max(0.05, GetParamDouble(corridorState, "LiningDepth", DefaultLiningDepth));

            double x0 = 0.0, y0 = 0.0;
            double x1 = flip * fWidth, y1 = fWidth * fSlope;
            double x2 = flip * (fWidth + bWidth), y2 = y1;
            double x3 = flip * (fWidth + bWidth + bkWidth), y3 = y2 + bkWidth * bkSlope;

            C3DPoint p0 = AddCorridorPoint(corridorState, x0, y0, StockCodes.Codes.DitchIn.Code);
            C3DPoint p1 = AddCorridorPoint(corridorState, x1, y1, StockCodes.Codes.FlowlineDitch.Code);
            C3DPoint p2 = AddCorridorPoint(corridorState, x2, y2, StockCodes.Codes.FlowlineDitch.Code);
            C3DPoint p3 = AddCorridorPoint(corridorState, x3, y3, StockCodes.Codes.DitchOut.Code);

            C3DPoint p0Sub = AddCorridorPoint(corridorState, x0, y0 - lining);
            C3DPoint p1Sub = AddCorridorPoint(corridorState, x1, y1 - lining);
            C3DPoint p2Sub = AddCorridorPoint(corridorState, x2, y2 - lining);
            C3DPoint p3Sub = AddCorridorPoint(corridorState, x3, y3 - lining);

            C3DLink lTop1 = AddCorridorLink(corridorState, p0, p1, StockCodes.Codes.Top.Code, StockCodes.Codes.Ditch.Code);
            C3DLink lTop2 = AddCorridorLink(corridorState, p1, p2, StockCodes.Codes.Top.Code, StockCodes.Codes.Ditch.Code);
            C3DLink lTop3 = AddCorridorLink(corridorState, p2, p3, StockCodes.Codes.Top.Code, StockCodes.Codes.Ditch.Code);

            C3DLink lRight = AddCorridorLink(corridorState, p3, p3Sub);
            C3DLink lBot3 = AddCorridorLink(corridorState, p3Sub, p2Sub, StockCodes.Codes.Datum.Code);
            C3DLink lBot2 = AddCorridorLink(corridorState, p2Sub, p1Sub, StockCodes.Codes.Datum.Code);
            C3DLink lBot1 = AddCorridorLink(corridorState, p1Sub, p0Sub, StockCodes.Codes.Datum.Code);
            C3DLink lLeft = AddCorridorLink(corridorState, p0Sub, p0);

            AddCorridorShape(corridorState, new[] { lTop1, lTop2, lTop3, lRight, lBot3, lBot2, lBot1, lLeft }, StockCodes.Codes.Ditch.Code);
        }
    }

    #endregion

    #region 3. DATA MODELS & ENGINE QUẢN LÝ SUBASSEMBLY .NET

    public class SubassemblyParamItem
    {
        public string Key { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string DataType { get; set; } = "Double"; // Double, Long, String
        public string Value { get; set; } = "";
    }

    public class DrawingSubassemblyInfo
    {
        public ObjectId SubassemblyId { get; set; }
        public string Name { get; set; } = "";
        public string AssemblyName { get; set; } = "(Chưa gắn Assembly)";
        public string GenerateMode { get; set; } = "";
        public string AssemblyDllPath { get; set; } = "";
        public string ClassName { get; set; } = "";
        public string Version { get; set; } = "";
        public string StatusText { get; set; } = "";
        public SubassemblyStatus RawStatus { get; set; }
        public List<SubassemblyParamItem> Parameters { get; set; } = new List<SubassemblyParamItem>();
    }

    public class DotNetSubassemblyClassInfo
    {
        public string DisplayGroup { get; set; } = ""; // "Custom C# (Civil3D_Tools)" hoặc "Stock Civil 3D (C3DStockSubassemblies)"
        public string ClassName { get; set; } = "";
        public string FullClassName { get; set; } = "";
        public string DllPath { get; set; } = "";
        public string Description { get; set; } = "";
        public List<SubassemblyParamItem> DefaultParams { get; set; } = new List<SubassemblyParamItem>();

        public override string ToString() => $"{ClassName} [{DisplayGroup}]";
    }

    public static class SubassemblyDotNetEngine
    {
        public const string StockDllDefaultPath = @"C:\ProgramData\Autodesk\C3D 2027\enu\C3DStockSubassemblies.dll";
        public const string RoadCatalogFolderPath = @"C:\ProgramData\Autodesk\C3D 2027\enu\Tool Catalogs\Road Catalog";

        private static Dictionary<string, (string CategoryName, List<SubassemblyParamItem> Params)>? _cachedStockAtcParams;

        /// <summary>
        /// Quét các file C3D Metric * Subassembly Catalog.atc (hoặc Imperial) để lấy đúng bộ tham số chuẩn của từng Stock Subassembly
        /// </summary>
        private static Dictionary<string, (string CategoryName, List<SubassemblyParamItem> Params)> GetStockAtcParameterMap()
        {
            if (_cachedStockAtcParams != null)
                return _cachedStockAtcParams;

            _cachedStockAtcParams = new Dictionary<string, (string, List<SubassemblyParamItem>)>(StringComparer.OrdinalIgnoreCase);

            try
            {
                if (!Directory.Exists(RoadCatalogFolderPath))
                    return _cachedStockAtcParams;

                bool isFeet = false;
                try { isFeet = StockUtilities.IsProjectUnitsFeet(); } catch { }

                string prefix = isFeet ? "C3D Imperial*Subassembly Catalog.atc" : "C3D Metric*Subassembly Catalog.atc";
                var atcFiles = Directory.GetFiles(RoadCatalogFolderPath, prefix)
                    .Where(f => !Path.GetFileName(f).Contains("VBA", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (atcFiles.Count == 0)
                {
                    atcFiles = Directory.GetFiles(RoadCatalogFolderPath, "*Subassembly Catalog.atc")
                        .Where(f => !Path.GetFileName(f).Contains("VBA", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                foreach (string atcFile in atcFiles)
                {
                    try
                    {
                        string categoryShort = Path.GetFileNameWithoutExtension(atcFile)
                            .Replace("C3D Metric ", "")
                            .Replace("C3D Imperial ", "")
                            .Replace(" Subassembly Catalog", "");

                        var xmlDoc = new System.Xml.XmlDocument();
                        xmlDoc.Load(atcFile);

                        var toolNodes = xmlDoc.SelectNodes("//Tool");
                        if (toolNodes == null) continue;

                        foreach (System.Xml.XmlNode toolNode in toolNodes)
                        {
                            var dotNetNode = toolNode.SelectSingleNode("./Data/AeccDbSubassembly/DotNetClass");
                            if (dotNetNode == null) continue;

                            string fullClass = dotNetNode.InnerText?.Trim() ?? "";
                            if (string.IsNullOrEmpty(fullClass)) continue;

                            string shortClass = fullClass.Contains('.')
                                ? fullClass.Substring(fullClass.LastIndexOf('.') + 1)
                                : fullClass;

                            var paramList = new List<SubassemblyParamItem>();
                            var paramsNode = toolNode.SelectSingleNode("./Data/AeccDbSubassembly/Params");
                            if (paramsNode != null)
                            {
                                foreach (System.Xml.XmlNode pNode in paramsNode.ChildNodes)
                                {
                                    if (pNode.NodeType != System.Xml.XmlNodeType.Element) continue;
                                    string key = pNode.Name;
                                    if (key.Equals("Version", StringComparison.OrdinalIgnoreCase) ||
                                        key.Equals("SE_AOR_Behavior", StringComparison.OrdinalIgnoreCase))
                                        continue;

                                    string rawType = pNode.Attributes?["DataType"]?.Value ?? "Double";
                                    string normType = rawType.Equals("Long", StringComparison.OrdinalIgnoreCase) || rawType.Equals("Bool", StringComparison.OrdinalIgnoreCase)
                                        ? "Long"
                                        : rawType.Equals("String", StringComparison.OrdinalIgnoreCase)
                                            ? "String"
                                            : "Double";

                                    // Chỉ lấy text trực tiếp của node (tránh gộp text của các thẻ con <Enum>)
                                    string defaultVal = "";
                                    foreach (System.Xml.XmlNode child in pNode.ChildNodes)
                                    {
                                        if (child.NodeType == System.Xml.XmlNodeType.Text || child.NodeType == System.Xml.XmlNodeType.CDATA)
                                        {
                                            defaultVal += child.Value;
                                        }
                                    }
                                    defaultVal = defaultVal.Trim();

                                    // Xây dựng mô tả gợi ý nếu có <Enum>
                                    string desc = key;
                                    var enumNode = pNode.SelectSingleNode("./Enum");
                                    if (enumNode != null && enumNode.HasChildNodes)
                                    {
                                        var enumOptions = new List<string>();
                                        foreach (System.Xml.XmlNode opt in enumNode.ChildNodes)
                                        {
                                            if (opt.NodeType == System.Xml.XmlNodeType.Element)
                                            {
                                                enumOptions.Add($"{opt.Name}={opt.InnerText.Trim()}");
                                            }
                                        }
                                        if (enumOptions.Count > 0)
                                        {
                                            desc = $"{key} ({string.Join(", ", enumOptions)})";
                                        }
                                    }

                                    paramList.Add(new SubassemblyParamItem
                                    {
                                        Key = key,
                                        DisplayName = desc,
                                        DataType = normType,
                                        Value = string.IsNullOrEmpty(defaultVal) ? (normType == "String" ? "" : "0") : defaultVal
                                    });
                                }
                            }

                            _cachedStockAtcParams[shortClass] = (categoryShort, paramList);
                            _cachedStockAtcParams[fullClass] = (categoryShort, paramList);
                        }
                    }
                    catch { }
                }
            }
            catch { }

            return _cachedStockAtcParams;
        }

        /// <summary>
        /// Quét toàn bộ các class kế thừa SATemplate từ dự án hiện tại (C#) và từ C3DStockSubassemblies.dll
        /// </summary>
        public static List<DotNetSubassemblyClassInfo> DiscoverAvailableSubassemblyClasses()
        {
            var result = new List<DotNetSubassemblyClassInfo>();
            Type baseType = typeof(StockSATemplate);
            var stockAtcMap = GetStockAtcParameterMap();

            // 1. Quét các class C# trong chính project Civil3D_Tools hiện tại
            try
            {
                var currentAsm = typeof(CustomSubassemblyBase).Assembly;
                string currentDllPath = currentAsm.Location;

                var customTypes = currentAsm.GetExportedTypes()
                    .Where(t => !t.IsAbstract && baseType.IsAssignableFrom(t))
                    .OrderBy(t => t.Name);

                foreach (var t in customTypes)
                {
                    result.Add(new DotNetSubassemblyClassInfo
                    {
                        DisplayGroup = "C# Tự Viết (Dự án hiện tại)",
                        ClassName = t.Name,
                        FullClassName = t.FullName ?? t.Name,
                        DllPath = currentDllPath,
                        Description = GetCustomClassDescription(t.Name),
                        DefaultParams = GetDefaultParametersForClass(t.Name)
                    });
                }
            }
            catch { }

            // 2. Quét toàn bộ 118+ Stock Subassemblies từ C3DStockSubassemblies.dll
            try
            {
                var stockAsm = baseType.Assembly;
                string stockDllPath = File.Exists(StockDllDefaultPath) ? StockDllDefaultPath : stockAsm.Location;

                var stockTypes = stockAsm.GetExportedTypes()
                    .Where(t => !t.IsAbstract && baseType.IsAssignableFrom(t))
                    .OrderBy(t => t.Name);

                foreach (var t in stockTypes)
                {
                    string fullClassName = t.FullName ?? $"Subassembly.{t.Name}";
                    List<SubassemblyParamItem> defaultParams;
                    string categoryDesc = "Civil 3D Stock";

                    if (stockAtcMap.TryGetValue(t.Name, out var foundAtc) && foundAtc.Params.Count > 0)
                    {
                        categoryDesc = $"Nhóm {foundAtc.CategoryName} - {foundAtc.Params.Count} tham số chuẩn từ Catalog .atc";
                        defaultParams = foundAtc.Params.Select(p => new SubassemblyParamItem
                        {
                            Key = p.Key,
                            DisplayName = p.DisplayName,
                            DataType = p.DataType,
                            Value = p.Value
                        }).ToList();
                    }
                    else
                    {
                        defaultParams = GetDefaultParametersForClass(t.Name);
                    }

                    result.Add(new DotNetSubassemblyClassInfo
                    {
                        DisplayGroup = "Civil 3D Stock (C3DStockSubassemblies.dll)",
                        ClassName = t.Name,
                        FullClassName = fullClassName,
                        DllPath = stockDllPath,
                        Description = $"Cấu kiện chuẩn Civil 3D ({fullClassName}) | {categoryDesc}",
                        DefaultParams = defaultParams
                    });
                }
            }
            catch { }

            return result;
        }

        private static string GetCustomClassDescription(string className)
        {
            return className switch
            {
                nameof(CustomBasicLaneCS) => "Làn xe cơ bản 1 lớp viết bằng C# (Width, Depth, Slope, Side)",
                nameof(CustomMultiLayerLaneCS) => "Kết cấu áo đường 4 lớp viết bằng C# (Pave1, Pave2, Base, Subbase)",
                nameof(CustomCurbAndGutterCS) => "Bó vỉa & đan rãnh viết bằng C# (GutterWidth, GutterSlope, CurbWidth, CurbHeight, CurbDepth)",
                nameof(CustomSideDitchCS) => "Rãnh biên hình thang có gia cố viết bằng C# (Foreslope, BottomWidth, Backslope, LiningDepth)",
                _ => $"Subassembly C# tùy biến ({className})"
            };
        }

        public static List<SubassemblyParamItem> GetDefaultParametersForClass(string className)
        {
            var list = new List<SubassemblyParamItem>
            {
                new SubassemblyParamItem { Key = "Side", DisplayName = "Phía chèn (0=Phải, 1=Trái)", DataType = "Long", Value = "0" }
            };

            switch (className)
            {
                case nameof(CustomMultiLayerLaneCS):
                    list.Add(new SubassemblyParamItem { Key = "Width", DisplayName = "Bề rộng làn (m)", DataType = "Double", Value = "3.50" });
                    list.Add(new SubassemblyParamItem { Key = "Slope", DisplayName = "Độ dốc ngang (m/m)", DataType = "Double", Value = "-0.02" });
                    list.Add(new SubassemblyParamItem { Key = "Pave1Depth", DisplayName = "Chiều dày BTN lớp trên (m)", DataType = "Double", Value = "0.05" });
                    list.Add(new SubassemblyParamItem { Key = "Pave2Depth", DisplayName = "Chiều dày BTN lớp dưới (m)", DataType = "Double", Value = "0.07" });
                    list.Add(new SubassemblyParamItem { Key = "BaseDepth", DisplayName = "Chiều dày Móng trên (m)", DataType = "Double", Value = "0.18" });
                    list.Add(new SubassemblyParamItem { Key = "SubbaseDepth", DisplayName = "Chiều dày Móng dưới (m)", DataType = "Double", Value = "0.30" });
                    break;

                case nameof(CustomCurbAndGutterCS):
                    list.Add(new SubassemblyParamItem { Key = "GutterWidth", DisplayName = "Bề rộng đan rãnh (m)", DataType = "Double", Value = "0.30" });
                    list.Add(new SubassemblyParamItem { Key = "GutterSlope", DisplayName = "Độ dốc đan rãnh (m/m)", DataType = "Double", Value = "-0.05" });
                    list.Add(new SubassemblyParamItem { Key = "CurbWidth", DisplayName = "Bề rộng bó vỉa (m)", DataType = "Double", Value = "0.18" });
                    list.Add(new SubassemblyParamItem { Key = "CurbHeight", DisplayName = "Chiều cao bó vỉa (m)", DataType = "Double", Value = "0.15" });
                    list.Add(new SubassemblyParamItem { Key = "CurbDepth", DisplayName = "Tổng chiều dày bó vỉa (m)", DataType = "Double", Value = "0.35" });
                    break;

                case nameof(CustomSideDitchCS):
                    list.Add(new SubassemblyParamItem { Key = "ForeslopeWidth", DisplayName = "Bề rộng mái trong (m)", DataType = "Double", Value = "0.80" });
                    list.Add(new SubassemblyParamItem { Key = "Foreslope", DisplayName = "Độ dốc mái trong (m/m)", DataType = "Double", Value = "-0.50" });
                    list.Add(new SubassemblyParamItem { Key = "BottomWidth", DisplayName = "Bề rộng đáy rãnh (m)", DataType = "Double", Value = "0.40" });
                    list.Add(new SubassemblyParamItem { Key = "BackslopeWidth", DisplayName = "Bề rộng mái ngoài (m)", DataType = "Double", Value = "0.80" });
                    list.Add(new SubassemblyParamItem { Key = "Backslope", DisplayName = "Độ dốc mái ngoài (m/m)", DataType = "Double", Value = "0.50" });
                    list.Add(new SubassemblyParamItem { Key = "LiningDepth", DisplayName = "Chiều dày gia cố rãnh (m)", DataType = "Double", Value = "0.10" });
                    break;

                default:
                    list.Add(new SubassemblyParamItem { Key = "Width", DisplayName = "Bề rộng (Width)", DataType = "Double", Value = "3.50" });
                    list.Add(new SubassemblyParamItem { Key = "Depth", DisplayName = "Chiều dày (Depth)", DataType = "Double", Value = "0.20" });
                    list.Add(new SubassemblyParamItem { Key = "Slope", DisplayName = "Độ dốc (Slope)", DataType = "Double", Value = "-0.02" });
                    break;
            }

            return list;
        }

        /// <summary>
        /// Đọc danh sách toàn bộ Subassembly đang có trong bản vẽ hiện hành
        /// </summary>
        public static List<DrawingSubassemblyInfo> GetDrawingSubassemblies(Database db)
        {
            var list = new List<DrawingSubassemblyInfo>();

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var civilDoc = CivilDocument.GetCivilDocument(db);

                // Map SubassemblyId -> AssemblyName
                var saToAsmMap = new Dictionary<ObjectId, string>();
                foreach (ObjectId asmId in civilDoc.AssemblyCollection)
                {
                    if (tr.GetObject(asmId, OpenMode.ForRead) is C3DAssembly asm)
                    {
                        foreach (AssemblyGroup grp in asm.Groups)
                        {
                            foreach (ObjectId saId in grp.GetSubassemblyIds())
                            {
                                saToAsmMap[saId] = $"{asm.Name} ({grp.Name})";
                            }
                        }
                    }
                }

                foreach (ObjectId saId in civilDoc.SubassemblyCollection)
                {
                    if (saId.IsNull || !saId.IsValid || saId.IsErased) continue;
                    if (tr.GetObject(saId, OpenMode.ForRead) is not C3DSubassembly sa) continue;

                    var gen = sa.GeometryGenerator;
                    string ver = string.IsNullOrWhiteSpace(sa.Version) ? "R2019+" : sa.Version;

                    var info = new DrawingSubassemblyInfo
                    {
                        SubassemblyId = saId,
                        Name = sa.Name,
                        AssemblyName = saToAsmMap.TryGetValue(saId, out string? asmName) ? asmName : "(Rời / Chưa gắn Assembly)",
                        GenerateMode = gen.GeometryGenerateMode.ToString(),
                        AssemblyDllPath = gen.ProjectOrAssemblyName ?? "",
                        ClassName = gen.MacroOrClassName ?? "",
                        Version = ver,
                        RawStatus = sa.Status,
                        StatusText = FormatStatus(sa.Status)
                    };

                    // Đọc danh sách tham số hiện tại của Subassembly
                    try
                    {
                        foreach (ParamLong p in sa.ParamsLong)
                        {
                            info.Parameters.Add(new SubassemblyParamItem
                            {
                                Key = p.Key,
                                DisplayName = string.IsNullOrWhiteSpace(p.DisplayName) ? p.Key : p.DisplayName,
                                DataType = "Long",
                                Value = p.Value.ToString()
                            });
                        }
                        foreach (ParamDouble p in sa.ParamsDouble)
                        {
                            info.Parameters.Add(new SubassemblyParamItem
                            {
                                Key = p.Key,
                                DisplayName = string.IsNullOrWhiteSpace(p.DisplayName) ? p.Key : p.DisplayName,
                                DataType = "Double",
                                Value = p.Value.ToString("0.####")
                            });
                        }
                        foreach (ParamString p in sa.ParamsString)
                        {
                            info.Parameters.Add(new SubassemblyParamItem
                            {
                                Key = p.Key,
                                DisplayName = string.IsNullOrWhiteSpace(p.DisplayName) ? p.Key : p.DisplayName,
                                DataType = "String",
                                Value = p.Value ?? ""
                            });
                        }
                    }
                    catch { }

                    list.Add(info);
                }

                tr.Commit();
            }

            return list;
        }

        public static string FormatStatus(SubassemblyStatus status)
        {
            return status switch
            {
                SubassemblyStatus.UpToDate => "Up To Date (Đã cập nhật)",
                SubassemblyStatus.OutOfDate => "Out Of Date (Cần cập nhật)",
                SubassemblyStatus.FileNotFound => "File Not Found (Thiếu DLL)",
                _ => status.ToString()
            };
        }

        /// <summary>
        /// Tạo file Catalog .atc chuẩn cho 1 hoặc nhiều class Subassembly .NET.
        /// Lưu ý: Trong thẻ ItemID idValue="{GUID}" có dấu ngoặc nhọn {}, nhưng khi truyền itemId vào
        /// SubassemblyCollection.ImportSubassembly thì truyền GUID KHÔNG có ngoặc nhọn (vì hàm API tự bọc '{{{0}}}').
        /// </summary>
        public static string GenerateAtcCatalogXml(
            string catalogTitle,
            IEnumerable<(string ToolName, string RawGuidWithoutBraces, string DllPath, string FullClassName, string Units, List<SubassemblyParamItem> Params)> tools)
        {
            var sb = new StringBuilder();
            string catId = "{" + Guid.NewGuid().ToString().ToUpperInvariant() + "}";
            string nowIso = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss");

            sb.AppendLine("<Category>");
            sb.AppendLine($"  <ItemID idValue=\"{catId}\"/>");
            sb.AppendLine("  <Properties>");
            sb.AppendLine($"    <ItemName>{SecurityElementEscape(catalogTitle)}</ItemName>");
            sb.AppendLine("    <AccessRight>1</AccessRight>");
            sb.AppendLine($"    <Description>Catalog Subassembly .NET tạo từ Civil3D_Tools</Description>");
            sb.AppendLine($"    <Time createdUniversalDateTime=\"{nowIso}\" modifiedUniversalDateTime=\"{nowIso}\"/>");
            sb.AppendLine("  </Properties>");
            sb.AppendLine("  <CustomData/>");
            sb.AppendLine("  <Source/>");
            sb.AppendLine("  <Palettes/>");
            sb.AppendLine("  <Packages/>");
            sb.AppendLine("  <Tools>");

            foreach (var t in tools)
            {
                string cleanGuid = t.RawGuidWithoutBraces.Trim().Trim('{', '}');
                string bracedGuid = "{" + cleanGuid + "}";

                sb.AppendLine("    <Tool>");
                sb.AppendLine($"      <ItemID idValue=\"{bracedGuid}\"/>");
                sb.AppendLine("      <Properties>");
                sb.AppendLine($"        <ItemName>{SecurityElementEscape(t.ToolName)}</ItemName>");
                sb.AppendLine($"        <Description>Subassembly .NET: {SecurityElementEscape(t.FullClassName)}</Description>");
                sb.AppendLine($"        <Time createdUniversalDateTime=\"{nowIso}\" modifiedUniversalDateTime=\"{nowIso}\"/>");
                sb.AppendLine("      </Properties>");
                sb.AppendLine("      <Source/>");
                sb.AppendLine("      <StockToolRef idValue=\"{7F55AAC0-0256-48D7-BFA5-914702663FDE}\"/>");
                sb.AppendLine("      <Data>");
                sb.AppendLine("        <AeccDbSubassembly>");
                sb.AppendLine("          <GeometryGenerateMode>UseDotNet</GeometryGenerateMode>");
                sb.AppendLine($"          <DotNetClass Assembly=\"{SecurityElementEscape(t.DllPath)}\">{SecurityElementEscape(t.FullClassName)}</DotNetClass>");
                sb.AppendLine("          <Params>");
                sb.AppendLine("            <Version DataType=\"String\" DisplayName=\"Version\" Description=\"Version\" ReadOnly=\"true\">R2019</Version>");

                if (t.Params != null)
                {
                    foreach (var p in t.Params)
                    {
                        if (string.IsNullOrWhiteSpace(p.Key) || p.Key.Equals("Version", StringComparison.OrdinalIgnoreCase))
                            continue;

                        string dt = p.DataType switch
                        {
                            "Long" => "Long",
                            "String" => "String",
                            _ => "Double"
                        };
                        string typeInfoAttr = dt == "Double" ? " TypeInfo=\"16\"" : "";
                        string disp = SecurityElementEscape(string.IsNullOrWhiteSpace(p.DisplayName) ? p.Key : p.DisplayName);
                        string val = SecurityElementEscape(p.Value);

                        sb.AppendLine($"            <{p.Key} DataType=\"{dt}\"{typeInfoAttr} DisplayName=\"{disp}\" Description=\"{disp}\">{val}</{p.Key}>");
                    }
                }

                sb.AppendLine("          </Params>");
                sb.AppendLine("        </AeccDbSubassembly>");
                sb.AppendLine($"        <Units>{t.Units}</Units>");
                sb.AppendLine("      </Data>");
                sb.AppendLine("    </Tool>");
            }

            sb.AppendLine("  </Tools>");
            sb.AppendLine("  <StockTools/>");
            sb.AppendLine("</Category>");
            return sb.ToString();
        }

        private static string SecurityElementEscape(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return System.Security.SecurityElement.Escape(input) ?? input;
        }

        /// <summary>
        /// Tạo mới Subassembly .NET (C# hoặc Stock) vào bản vẽ và tùy chọn gắn vào Assembly
        /// </summary>
        public static ObjectId CreateAndAttachDotNetSubassembly(
            Database db,
            string subassemblyName,
            string dllPath,
            string fullClassName,
            List<SubassemblyParamItem> parameters,
            ObjectId targetAssemblyId,
            Point3d insertLocation,
            out string message)
        {
            message = "";
            string tempAtcPath = Path.Combine(Path.GetTempPath(), $"C3D_DotNetSA_{Guid.NewGuid():N}.atc");
            // QUAN TRỌNG: ImportSubassembly bên trong AeccDbMgd.dll dùng String.Format("//Tool[ItemID[@idValue='{{{0}}}']]", itemId)
            // nên tham số itemId truyền vào KHÔNG ĐƯỢC có dấu ngoặc nhọn { }!
            string rawGuidWithoutBraces = Guid.NewGuid().ToString().ToUpperInvariant();

            try
            {
                bool isFeet = false;
                try { isFeet = StockUtilities.IsProjectUnitsFeet(); } catch { }
                string units = isFeet ? "foot" : "m";

                string xml = GenerateAtcCatalogXml(
                    "TempCatalog",
                    new[] { (subassemblyName, rawGuidWithoutBraces, dllPath, fullClassName, units, parameters) });

                File.WriteAllText(tempAtcPath, xml, Encoding.UTF8);

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    var civilDoc = CivilDocument.GetCivilDocument(db);

                    // Nếu gắn vào Assembly và chưa chọn điểm đặt thì lấy luôn vị trí của Assembly
                    Point3d actualInsertPt = insertLocation;
                    if (!targetAssemblyId.IsNull && targetAssemblyId.IsValid && !targetAssemblyId.IsErased)
                    {
                        if (tr.GetObject(targetAssemblyId, OpenMode.ForRead) is C3DAssembly targetAsm)
                        {
                            actualInsertPt = targetAsm.Location;
                        }
                    }

                    // Đảm bảo tên Subassembly không bị trùng trong bản vẽ (vì ImportSubassembly sẽ báo lỗi "Subassembly name is duplicated")
                    string uniqueSaName = EnsureUniqueSubassemblyName(civilDoc, tr, subassemblyName);

                    ObjectId saId = ObjectId.Null;
                    try
                    {
                        saId = civilDoc.SubassemblyCollection.ImportSubassembly(
                            uniqueSaName,
                            tempAtcPath,
                            rawGuidWithoutBraces,
                            actualInsertPt);
                    }
                    catch
                    {
                        // Dự phòng: Nếu là Stock Subassembly thì gọi thêm ImportStockSubassembly
                        if (fullClassName.StartsWith("Subassembly.", StringComparison.OrdinalIgnoreCase))
                        {
                            saId = civilDoc.SubassemblyCollection.ImportStockSubassembly(
                                uniqueSaName,
                                fullClassName,
                                actualInsertPt);
                        }
                        else
                        {
                            throw;
                        }
                    }

                    if (saId.IsNull || !saId.IsValid)
                    {
                        message = "Không thể khởi tạo Subassembly vào bản vẽ.";
                        return ObjectId.Null;
                    }

                    if (tr.GetObject(saId, OpenMode.ForWrite) is C3DSubassembly sa)
                    {
                        var gen = sa.GeometryGenerator;
                        gen.GeometryGenerateMode = SubassemblyGeometryGenerateMode.UseDotNet;
                        gen.ProjectOrAssemblyName = dllPath;
                        gen.MacroOrClassName = fullClassName;
                        sa.GeometryGenerator = gen;

                        ApplyParametersToSubassembly(sa, parameters);
                        sa.Run();
                    }

                    // Nếu có chọn Assembly đích thì gắn trực tiếp vào Assembly
                    if (!targetAssemblyId.IsNull && targetAssemblyId.IsValid && !targetAssemblyId.IsErased)
                    {
                        if (tr.GetObject(targetAssemblyId, OpenMode.ForWrite) is C3DAssembly asm)
                        {
                            asm.AddSubassembly(saId);
                            message = $"Đã tạo Subassembly '{uniqueSaName}' ({fullClassName}) và gắn vào Assembly '{asm.Name}'.";
                        }
                    }
                    else
                    {
                        message = $"Đã tạo Subassembly '{uniqueSaName}' ({fullClassName}) tại ({actualInsertPt.X:F2}, {actualInsertPt.Y:F2}).";
                    }

                    tr.Commit();
                    return saId;
                }
            }
            finally
            {
                try { if (File.Exists(tempAtcPath)) File.Delete(tempAtcPath); } catch { }
            }
        }

        private static string EnsureUniqueSubassemblyName(CivilDocument civilDoc, Transaction tr, string baseName)
        {
            var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ObjectId id in civilDoc.SubassemblyCollection)
            {
                if (tr.GetObject(id, OpenMode.ForRead) is C3DSubassembly sa && !string.IsNullOrEmpty(sa.Name))
                {
                    existingNames.Add(sa.Name);
                }
            }

            if (!existingNames.Contains(baseName))
                return baseName;

            int counter = 1;
            while (existingNames.Contains($"{baseName}_{counter:D2}"))
            {
                counter++;
            }
            return $"{baseName}_{counter:D2}";
        }

        /// <summary>
        /// Cập nhật các tham số và/hoặc đổi Class .NET cho 1 Subassembly có sẵn trên bản vẽ
        /// </summary>
        public static bool UpdateExistingSubassembly(
            Database db,
            ObjectId subassemblyId,
            string newDllPath,
            string newFullClassName,
            List<SubassemblyParamItem> parameters,
            bool rebuildCorridors,
            out string statusMsg)
        {
            statusMsg = "";
            if (subassemblyId.IsNull || !subassemblyId.IsValid || subassemblyId.IsErased)
            {
                statusMsg = "Subassembly ID không hợp lệ.";
                return false;
            }

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                if (tr.GetObject(subassemblyId, OpenMode.ForWrite) is not C3DSubassembly sa)
                {
                    statusMsg = "Không mở được đối tượng Subassembly.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(newDllPath) && !string.IsNullOrWhiteSpace(newFullClassName))
                {
                    var gen = sa.GeometryGenerator;
                    gen.GeometryGenerateMode = SubassemblyGeometryGenerateMode.UseDotNet;
                    gen.ProjectOrAssemblyName = newDllPath;
                    gen.MacroOrClassName = newFullClassName;
                    sa.GeometryGenerator = gen;
                }

                ApplyParametersToSubassembly(sa, parameters);
                sa.Run();

                int rebuiltCount = 0;
                if (rebuildCorridors)
                {
                    var civilDoc = CivilDocument.GetCivilDocument(db);
                    foreach (ObjectId corrId in civilDoc.CorridorCollection)
                    {
                        if (tr.GetObject(corrId, OpenMode.ForWrite) is Corridor corr)
                        {
                            corr.Rebuild();
                            rebuiltCount++;
                        }
                    }
                }

                tr.Commit();
                statusMsg = rebuiltCount > 0
                    ? $"Đã cập nhật Subassembly '{sa.Name}' và Rebuild {rebuiltCount} Corridor."
                    : $"Đã cập nhật và chạy lại (Run) Subassembly '{sa.Name}'.";
                return true;
            }
        }

        private static void ApplyParametersToSubassembly(C3DSubassembly sa, List<SubassemblyParamItem> parameters)
        {
            if (parameters == null) return;

            foreach (var item in parameters)
            {
                if (string.IsNullOrWhiteSpace(item.Key)) continue;

                try
                {
                    if (item.DataType.Equals("Long", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(item.Value, out int lVal))
                        {
                            try { sa.ParamsLong[item.Key].Value = lVal; }
                            catch { sa.ParamsLong.Add(item.Key, lVal); }
                        }
                    }
                    else if (item.DataType.Equals("Double", StringComparison.OrdinalIgnoreCase))
                    {
                        if (double.TryParse(item.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double dVal) ||
                            double.TryParse(item.Value, out dVal))
                        {
                            try { sa.ParamsDouble[item.Key].Value = dVal; }
                            catch { sa.ParamsDouble.Add(item.Key, dVal); }
                        }
                    }
                    else if (item.DataType.Equals("String", StringComparison.OrdinalIgnoreCase))
                    {
                        try { sa.ParamsString[item.Key].Value = item.Value; }
                        catch { sa.ParamsString.Add(item.Key, item.Value); }
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// Chạy cập nhật (Run) toàn bộ các Subassembly .NET trong bản vẽ (trích xuất từ RoadwaySample)
        /// </summary>
        public static int UpdateAllDotNetSubassemblies(Database db, bool forceRunAll, bool rebuildCorridors, out List<string> logs)
        {
            logs = new List<string>();
            int updatedCount = 0;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var civilDoc = CivilDocument.GetCivilDocument(db);
                foreach (ObjectId saId in civilDoc.SubassemblyCollection)
                {
                    if (tr.GetObject(saId, OpenMode.ForWrite) is not C3DSubassembly sa) continue;
                    if (sa.GeometryGenerator.GeometryGenerateMode != SubassemblyGeometryGenerateMode.UseDotNet)
                        continue;

                    if (sa.Status == SubassemblyStatus.FileNotFound)
                    {
                        logs.Add($"[Bỏ qua] {sa.Name}: Không tìm thấy file DLL ({sa.GeometryGenerator.ProjectOrAssemblyName})");
                        continue;
                    }

                    if (forceRunAll || sa.Status == SubassemblyStatus.OutOfDate)
                    {
                        sa.Run();
                        updatedCount++;
                        logs.Add($"[Đã cập nhật] {sa.Name} -> {sa.GeometryGenerator.MacroOrClassName}");
                    }
                }

                if (rebuildCorridors && updatedCount > 0)
                {
                    foreach (ObjectId corrId in civilDoc.CorridorCollection)
                    {
                        if (tr.GetObject(corrId, OpenMode.ForWrite) is Corridor corr)
                        {
                            corr.Rebuild();
                            logs.Add($"[Rebuild Corridor] {corr.Name}");
                        }
                    }
                }

                tr.Commit();
            }

            return updatedCount;
        }

        /// <summary>
        /// Đóng gói bản vẽ hiện hành cùng toàn bộ các DLL Subassembly thành file .zip (trích xuất từ RoadwaySample)
        /// </summary>
        public static bool PackageDrawingWithSubassemblies(Database db, string targetZipFile, out string statusMessage)
        {
            statusMessage = "";
            if (string.IsNullOrWhiteSpace(db.Filename) || !File.Exists(db.Filename))
            {
                statusMessage = "Vui lòng lưu bản vẽ (Save) trước khi đóng gói.";
                return false;
            }

            string tempTargetFolder = Path.Combine(Path.GetTempPath(), "C3D_SA_Pkg_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(tempTargetFolder);
                var handledFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    var civilDoc = CivilDocument.GetCivilDocument(db);
                    foreach (ObjectId saId in civilDoc.SubassemblyCollection)
                    {
                        if (tr.GetObject(saId, OpenMode.ForWrite) is not C3DSubassembly sa) continue;
                        if (sa.Status == SubassemblyStatus.FileNotFound) continue;

                        string srcFile = sa.GeometryGenerator.ProjectOrAssemblyName;
                        if (string.IsNullOrWhiteSpace(srcFile) || !File.Exists(srcFile)) continue;

                        if (!handledFiles.TryGetValue(srcFile, out string? copiedFile))
                        {
                            copiedFile = Path.Combine(tempTargetFolder, Path.GetFileName(srcFile));
                            File.Copy(srcFile, copiedFile, true);
                            handledFiles[srcFile] = copiedFile;
                        }
                    }

                    tr.Commit();
                }

                // Copy file DWG hiện tại vào thư mục đóng gói
                string dwgCopy = Path.Combine(tempTargetFolder, Path.GetFileName(db.Filename));
                File.Copy(db.Filename, dwgCopy, true);

                if (File.Exists(targetZipFile)) File.Delete(targetZipFile);

                using (var zip = Package.Open(targetZipFile, FileMode.OpenOrCreate))
                {
                    foreach (string filePath in Directory.GetFiles(tempTargetFolder))
                    {
                        var uri = PackUriHelper.CreatePartUri(new Uri(".\\" + Path.GetFileName(filePath), UriKind.Relative));
                        if (zip.PartExists(uri)) zip.DeletePart(uri);

                        var part = zip.CreatePart(uri, "", CompressionOption.Normal);
                        using var srcStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        using var dstStream = part.GetStream();
                        srcStream.CopyTo(dstStream);
                    }
                }

                statusMessage = $"Đã đóng gói bản vẽ và {handledFiles.Count} file DLL Subassembly vào: {targetZipFile}";
                return true;
            }
            catch (Exception ex)
            {
                statusMessage = "Lỗi đóng gói: " + ex.Message;
                return false;
            }
            finally
            {
                try { if (Directory.Exists(tempTargetFolder)) Directory.Delete(tempTargetFolder, true); } catch { }
            }
        }
    }

    #endregion

    #region 4. LỆNH AUTOCAD / CIVIL 3D CHÍNH

    public class CTC_Quan_Ly_Subassembly_DotNet_Command
    {
        [CommandMethod("CTC_Quan_Ly_Subassembly_DotNet")]
        public static void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var db = doc.Database;
            var ed = doc.Editor;

            ed.WriteMessage("\n==================================================================");
            ed.WriteMessage("\n [CTC] QUẢN LÝ & KHỞI TẠO SUBASSEMBLY .NET (C# / STOCK CIVIL 3D)");
            ed.WriteMessage("\n==================================================================\n");

            using (var form = new MyFirstProject.Civil_Tool_2.QuanLySubassemblyDotNetForm(db, ed))
            {
                Application.ShowModalDialog(form);
            }
        }
    }

    #endregion
}

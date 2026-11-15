using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using ClosedXML.Excel;
using MyFirstProject.Extensions;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using CivSurface = Autodesk.Civil.DatabaseServices.TinSurface;

namespace Civil3DCsharp
{
    public enum SightCheckStatus
    {
        Pass,       // Đạt yêu cầu
        Warning,    // Cảnh báo (tiệm cận hoặc cần dạt tầm nhìn bụng cong)
        Fail        // Không đạt (bị che khuất hoặc R không đủ)
    }

    public enum SightType
    {
        Stopping,   // Tầm nhìn dừng xe (SSD)
        Passing     // Tầm nhìn vượt xe (PSD)
    }
}

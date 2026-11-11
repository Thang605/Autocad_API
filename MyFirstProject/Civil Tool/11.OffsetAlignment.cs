// (C) Copyright 2015 by  
//
// AT_OffsetAlignment Command for Civil 3D
// This file contains the AT_OffsetAlignment command and its required dependencies
// 
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices.Styles;
using System;
using System.Collections.Generic;

// This line is not mandatory, but improves loading performances
[assembly: CommandClass(typeof(MyFirstProject.AT_OffsetAlignment_Civil))]

namespace MyFirstProject
{
    /// <summary>
    /// Class containing AT_OffsetAlignment command for Civil 3D
    /// </summary>
    public class AT_OffsetAlignment_Civil
    {
        // Lệnh tạo offset alignment
        [CommandMethod("CTA_Offset_Alignment")]
        public static void AT_OffsetAlignment()
        {
            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                using var form = new OffsetAlignmentForm();
                var result = Autodesk.AutoCAD.ApplicationServices.Application.ShowModalDialog(form);

                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    using Transaction tr = doc.Database.TransactionManager.StartTransaction();

                    var alignmentService = new AlignmentServiceHelper();
                    var alignment = (Alignment)tr.GetObject(form.ParentAlignmentId, OpenMode.ForRead);

                    // Create first offset (Right/Default)
                    string name1 = form.BothSides ? $"{form.NewAlignmentName}_Right" : form.NewAlignmentName;
                    ObjectId offsetId1 = alignmentService.CreateOffsetAlignment(
                        alignment,
                        name1,
                        form.OffsetWidth,
                        form.StartStation,
                        form.EndStation,
                        form.SelectedStyleId
                    );

                    // Create second offset if requested (Left)
                    ObjectId offsetId2 = ObjectId.Null;
                    if (form.BothSides)
                    {
                        string name2 = $"{form.NewAlignmentName}_Left";
                        offsetId2 = alignmentService.CreateOffsetAlignment(
                            alignment,
                            name2,
                            -form.OffsetWidth,
                            form.StartStation,
                            form.EndStation,
                            form.SelectedStyleId
                        );
                    }

                    if (offsetId1 != ObjectId.Null || offsetId2 != ObjectId.Null)
                    {
                        ed.WriteMessage($"\nĐã tạo offset alignment: {form.NewAlignmentName}");
                    }
                    else
                    {
                        ed.WriteMessage("\nKhông thể tạo offset alignment!");
                    }
                    tr.Commit();
                }
            }
            catch (System.Exception e)
            {
                ed.WriteMessage($"\nLỗi: {e.Message}");
            }
        }
    }

    #region Helper Classes for OffsetAlignment

    /// <summary>
    /// Helper class for user input operations
    /// </summary>
    public class UserInputHelper
    {
        private readonly Autodesk.AutoCAD.EditorInput.Editor _editor;

        public UserInputHelper()
        {
            _editor = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor;
        }

        public double GetDouble(string prompt)
        {
            var options = new Autodesk.AutoCAD.EditorInput.PromptDoubleOptions(prompt)
            {
                AllowNegative = true,
                AllowZero = true,
                UseDefaultValue = false
            };

            var result = _editor.GetDouble(options);
            if (result.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
            {
                return result.Value;
            }
            return 0.0;
        }

        public Autodesk.AutoCAD.EditorInput.PromptDoubleResult GetDoubleResult(string prompt, string[]? keywords = null, string? defaultKeyword = null)
        {
            var options = new Autodesk.AutoCAD.EditorInput.PromptDoubleOptions(prompt)
            {
                AllowNegative = true,
                AllowZero = true,
                UseDefaultValue = true,
                DefaultValue = 10.0
            };

            // Add keywords if provided
            if (keywords != null && keywords.Length > 0)
            {
                foreach (string keyword in keywords)
                {
                    options.Keywords.Add(keyword);
                }
                options.Keywords.Default = defaultKeyword ?? keywords[0];
            }

            return _editor.GetDouble(options);
        }

        public ObjectId GetAlignmentId(string prompt)
        {
            var options = new Autodesk.AutoCAD.EditorInput.PromptEntityOptions(prompt);
            options.SetRejectMessage("\nĐối tượng không phải là Alignment.");
            options.AddAllowedClass(typeof(Alignment), true);

            var result = _editor.GetEntity(options);
            if (result.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
            {
                return result.ObjectId;
            }
            return ObjectId.Null;
        }

        public Point3d GetPoint(string prompt)
        {
            var options = new Autodesk.AutoCAD.EditorInput.PromptPointOptions(prompt);
            var result = _editor.GetPoint(options);
            if (result.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
            {
                return result.Value;
            }
            return Point3d.Origin;
        }
    }

    /// <summary>
    /// Helper class for alignment operations
    /// </summary>
    public class AlignmentServiceHelper
    {
        private readonly Autodesk.AutoCAD.EditorInput.Editor _editor;

        public AlignmentServiceHelper()
        {
            _editor = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor;
        }

        /// <summary>
        /// Gets station and offset from a point relative to an alignment
        /// </summary>
        public double GetStationAndOffsetFromPoint(Point3d point, Alignment alignment, out double station, out double offset)
        {
            station = 0;
            offset = 0;

            ArgumentNullException.ThrowIfNull(alignment);

            try
            {
                alignment.StationOffset(point.X, point.Y, ref station, ref offset);
                _editor.WriteMessage($"\nStation: {station:F2} m, Offset: {offset:F2} m");
            }
            catch (Autodesk.AutoCAD.Runtime.Exception e)
            {
                _editor.WriteMessage($"Error getting station from point: {e.Message}");
            }
            return station;
        }

        /// <summary>
        /// Gets alignment style ID by name (if not found, returns default style)
        /// </summary>
        public ObjectId GetAlignmentStyleIdByName(string styleName)
        {
            CivilDocument civilDoc = CivilApplication.ActiveDocument;
            if (string.IsNullOrWhiteSpace(styleName))
                return civilDoc.Styles.AlignmentStyles[0]; // default

            foreach (ObjectId id in civilDoc.Styles.AlignmentStyles)
            {
                // Access style name through DBObject
                Autodesk.AutoCAD.DatabaseServices.DBObject styleObj = id.GetObject(OpenMode.ForRead) as Autodesk.AutoCAD.DatabaseServices.DBObject;
                if (styleObj != null)
                {
                    var nameProp = styleObj.GetType().GetProperty("Name");
                    if (nameProp != null)
                    {
                        string? name = nameProp.GetValue(styleObj) as string;
                        if (!string.IsNullOrEmpty(name) && name.Equals(styleName, StringComparison.OrdinalIgnoreCase))
                            return id;
                    }
                }
            }
            _editor.WriteMessage($"\nKhông tìm thấy style '{styleName}', dùng style mặc định.");
            return civilDoc.Styles.AlignmentStyles[0];
        }

        /// <summary>
        /// Creates offset alignment from a parent alignment, allows passing styleId
        /// </summary>
        public ObjectId CreateOffsetAlignment(Alignment parentAlignment, string offsetName, double offsetWidth,
            double startStation, double endStation, ObjectId styleId)
        {
            ArgumentNullException.ThrowIfNull(parentAlignment);
            if (offsetWidth == 0)
                throw new ArgumentException("Offset width must not be zero", nameof(offsetWidth));
            if (startStation < parentAlignment.StartingStation || endStation > parentAlignment.EndingStation || startStation >= endStation)
                throw new ArgumentException("Invalid station range");

            Database db = parentAlignment.Database;
            ObjectId newAlignmentId = ObjectId.Null;

            // Ensure unique name
            string uniqueName = GetUniqueAlignmentName(db, offsetName);

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    ObjectId offsetId = Alignment.CreateOffsetAlignment(
                        uniqueName,
                        parentAlignment.ObjectId,
                        offsetWidth,
                        styleId,
                        startStation,
                        endStation
                    );

                    if (offsetId != ObjectId.Null)
                    {
                        newAlignmentId = offsetId;
                    }
                    tr.Commit();
                }
                catch (System.Exception ex)
                {
                    _editor.WriteMessage($"\nLỗi tạo offset alignment: {ex.Message}");
                    tr.Abort();
                }
            }
            return newAlignmentId;
        }

        private string GetUniqueAlignmentName(Database db, string baseName)
        {
            CivilDocument civilDoc = CivilApplication.ActiveDocument;
            string proposedName = baseName;
            int counter = 1;

            bool nameExists = true;
            while (nameExists)
            {
                nameExists = false;
                foreach (ObjectId alignId in civilDoc.GetAlignmentIds())
                {
                    using var tr = db.TransactionManager.StartTransaction();
                    var align = (Alignment)tr.GetObject(alignId, OpenMode.ForRead);
                    if (align.Name.Equals(proposedName, StringComparison.OrdinalIgnoreCase))
                    {
                        nameExists = true;
                        proposedName = $"{baseName} ({counter++})";
                        break;
                    }
                }
            }
            return proposedName;
        }

        /// <summary>
        /// Returns list of all Alignment Styles (Id and Name)
        /// </summary>
        public List<(ObjectId Id, string Name)> GetAllAlignmentStyles()
        {
            var result = new List<(ObjectId, string)>();
            CivilDocument civilDoc = CivilApplication.ActiveDocument;

            foreach (ObjectId id in civilDoc.Styles.AlignmentStyles)
            {
                try
                {
                    using var tr = id.Database.TransactionManager.StartTransaction();
                    var styleObj = tr.GetObject(id, OpenMode.ForRead);
                    string styleName = "Unknown Style";

                    // Try different ways to get the style name
                    if (styleObj is Autodesk.Civil.DatabaseServices.Styles.AlignmentStyle alignStyle)
                    {
                        styleName = alignStyle.Name;
                    }
                    else
                    {
                        // Fallback method using reflection
                        var nameProp = styleObj.GetType().GetProperty("Name");
                        if (nameProp != null && nameProp.CanRead)
                        {
                            var name = nameProp.GetValue(styleObj) as string;
                            if (!string.IsNullOrEmpty(name))
                            {
                                styleName = name;
                            }
                        }
                    }

                    result.Add((id, styleName));
                    tr.Commit();
                }
                catch (System.Exception ex)
                {
                    _editor.WriteMessage($"\nLỗi đọc style: {ex.Message}");
                    result.Add((id, "Error Reading Style"));
                }
            }
            return result;
        }
    }

    #endregion
}

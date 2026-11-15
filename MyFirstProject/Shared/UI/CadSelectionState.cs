using System.Drawing;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace MyFirstProject.Shared.UI
{
    /// <summary>Destination required by a concrete command dialog.</summary>
    public enum CadSelectionDestination { None, Text, Point }

    /// <summary>Session settings owned independently by each concrete command form.</summary>
    public sealed class CadSelectionState
    {
        internal Database? Database;
        internal ObjectId[] SourceIds = [];
        internal ObjectId TargetId = ObjectId.Null;
        internal Point3d Point;
        internal bool HasPoint;
        internal Size FormSize;
    }
}

using Autodesk.AutoCAD.DatabaseServices;

namespace MyFirstProject.Civil_Tool
{
    public class ObjectIdItem
    {
        public string Name { get; set; } = "";
        public ObjectId Id { get; set; } = ObjectId.Null;
        public override string ToString() => Name;
    }
}

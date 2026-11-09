## 2024-05-19 - [Optimize RXClass Lookups]
**Learning:** Calling `RXClass.GetClass(typeof(T))` or `Autodesk.AutoCAD.Runtime.RXObject.GetClass(typeof(T))` inside a loop is extremely slow and degrades performance for ModelSpace entity iterations.
**Action:** Cache the class type variable outside the iteration block and utilize `id.ObjectClass.IsDerivedFrom(cachedTypeClass)` or use `id.ObjectClass.DxfName` string checks prior to resolving objects with `tr.GetObject(id, OpenMode.ForRead)`.

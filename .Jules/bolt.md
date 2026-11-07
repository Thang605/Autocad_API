## 2024-11-06 - [Performance] Avoid O(N) ModelSpace Scans using Editor.SelectAll and SelectionFilter
**Learning:**
Scanning the entire ModelSpace block table record (`BlockTableRecord.ModelSpace`) element by element within a transaction (especially when retrieving text, circles, points, or entities by layer) is an O(N) operation and a major performance bottleneck for large DWG files. Relying on manually looping and type-checking can dramatically slow down commands like `CTPO_TaoCogoPoint_MultiSource`.

**Action:**
Replaced all O(N) ModelSpace `foreach (ObjectId id in btr)` loops in `MyFirstProject/Civil Tool/47.CTPO_TaoCogoPoint_TongHop.cs` (specifically `GetAllTextIdsInLayer`, `GetAllCircleIdsInLayer`, `GetAllPointIdsInLayer`, and `CollectAllTextSpatialInfo`) with AutoCAD's native, optimized `Editor.SelectAll(new SelectionFilter(TypedValue[]))` method. Used specific `DxfCode.LayoutName`, `DxfCode.LayerName`, and `DxfCode.Start` filter arrays for targeted selection, which pushes the heavy lifting to the C++ core and achieves significantly faster execution.

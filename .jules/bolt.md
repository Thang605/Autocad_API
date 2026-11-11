
## 2024-11-11 - Hoist RXClass.GetClass Out of Iteration Loops
**Learning:** Calling `RXClass.GetClass(typeof(T))` inside a `foreach` loop over AutoCAD entity IDs (like a ModelSpace iteration) introduces significant, repeated reflection overhead, particularly in O(N) operations.
**Action:** Always hoist `RXClass.GetClass` declarations outside of iteration loops to instantiate the RXClass object only once.

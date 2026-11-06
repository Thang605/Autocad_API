
## 2024-11-06 - [ModelSpace iteration bottleneck]
**Learning:** Calling `tr.GetObject()` inside a full ModelSpace iteration loop is extremely slow and acts as a major performance bottleneck, as it forces the database to open and read every single entity even if it's the wrong type.
**Action:** Always pre-filter using `id.ObjectClass.IsDerivedFrom(RXClass.GetClass(typeof(TargetType)))`. This is an O(1) metadata check that avoids opening irrelevant entities, drastically improving large-drawing scan performance.

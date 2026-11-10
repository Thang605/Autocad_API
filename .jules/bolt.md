## 2024-05-15 - [ModelSpace O(N) Iteration Pre-Filtering]
**Learning:** O(N) iteration in ModelSpace is slow due to `tr.GetObject()` calls. Use pre-filtering with `RXClass.GetClass` or string properties.
**Action:** Use pre-filtering with `objId.ObjectClass.DxfName` to avoid expensive DBObject retrieval when checking object types.

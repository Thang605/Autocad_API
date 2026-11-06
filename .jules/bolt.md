## 2026-11-05 - AutoCAD Table Bulk Update Regeneration
**Learning:** When modifying multiple cells in an AutoCAD `Table` object within a loop, AutoCAD recalculates and regenerates its geometry and layout for every single cell property change. This leads to severe $O(N \times M)$ performance bottlenecks for large tables.
**Action:** Always wrap bulk updates to AutoCAD tables with `table.SuppressRegenerateTable(true);` and a `try/finally` block that restores `table.SuppressRegenerateTable(false);` before calling layout regeneration methods like `RecomputeTableBlock()`.

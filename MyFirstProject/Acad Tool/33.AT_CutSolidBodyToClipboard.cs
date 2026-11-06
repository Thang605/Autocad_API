// (C) Copyright 2026 by T27
// Lệnh chọn nhanh tất cả 3D Solid và Body có trong bản vẽ hiện hành và CUT (Cắt) vào Clipboard để dán sang bản vẽ khác
//
using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using Exception = System.Exception;

[assembly: CommandClass(typeof(Civil3DCsharp.AT_CutSolidBodyCmd))]

namespace Civil3DCsharp
{
    /// <summary>
    /// Class chứa lệnh chọn nhanh tất cả 3D Solid và Body trong bản vẽ hiện hành và CẮT (CUTCLIP) vào Clipboard
    /// Lệnh: CUTSB, CSB, AT_CutSolidBody, AT_CutSolidBodyToClipboard, CUT_SOLID_BODY
    /// </summary>
    public class AT_CutSolidBodyCmd
    {
        [CommandMethod("CUTSB")]
        public static void CutSolidBodyToClipboard()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            Database db = doc.Database;
            Editor ed = doc.Editor;

            try
            {
                int solidCount = 0;
                int bodyCount = 0;
                int thawedLayersCount = 0;
                int turnedOnLayersCount = 0;
                int unlockedLayersCount = 0;
                bool switchedToModelSpace = false;

                // ── Bước 1: Dùng SelectionFilter để AutoCAD engine lọc nhanh ──
                // Filter: 3DSOLID hoặc BODY — engine xử lý nội bộ, nhanh hơn duyệt thủ công
                SelectionFilter filter = new SelectionFilter(new TypedValue[]
                {
                    new TypedValue(-4, "<OR"),
                    new TypedValue(0, "3DSOLID"),
                    new TypedValue(0, "BODY"),
                    new TypedValue(-4, "OR>")
                });

                PromptSelectionResult selResult = ed.SelectAll(filter);

                // ── Bước 2: Nếu không tìm thấy ở space hiện tại, thử Model Space ──
                if (selResult.Status != PromptStatus.OK || selResult.Value == null || selResult.Value.Count == 0)
                {
                    // Đang ở Paper Space? Thử chuyển sang Model Space
                    if (db.TileMode == false) // Đang ở Layout (Paper Space)
                    {
                        // Kiểm tra Model Space có đối tượng không bằng cách quét nhanh
                        bool hasObjectsInModel = false;
                        using (Transaction trCheck = db.TransactionManager.StartTransaction())
                        {
                            BlockTable bt = (BlockTable)trCheck.GetObject(db.BlockTableId, OpenMode.ForRead);
                            BlockTableRecord modelBtr = (BlockTableRecord)trCheck.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                            RXClass solid3dClass = RXClass.GetClass(typeof(Solid3d));
                            RXClass bodyClass = RXClass.GetClass(typeof(Body));

                            foreach (ObjectId id in modelBtr)
                            {
                                if (id.IsNull || id.IsErased) continue;
                                // Chỉ kiểm tra ObjectClass — KHÔNG mở entity → cực nhanh
                                if (id.ObjectClass.IsDerivedFrom(solid3dClass) || id.ObjectClass.IsDerivedFrom(bodyClass))
                                {
                                    hasObjectsInModel = true;
                                    break; // Chỉ cần tìm thấy 1 cái là đủ
                                }
                            }
                            trCheck.Commit();
                        }

                        if (hasObjectsInModel)
                        {
                            ed.WriteMessage("\n[Thông báo] Phát hiện các đối tượng 3D Solid/Body trong Model Space. Đang tự động chuyển sang Model Space...");
                            Application.SetSystemVariable("TILEMODE", 1);
                            switchedToModelSpace = true;

                            // SelectAll lại sau khi chuyển sang Model Space
                            selResult = ed.SelectAll(filter);
                        }
                    }
                }

                if (selResult.Status != PromptStatus.OK || selResult.Value == null || selResult.Value.Count == 0)
                {
                    ed.WriteMessage("\n[Thông báo] Không tìm thấy đối tượng 3D Solid hoặc Body nào trong không gian hiện hành.");
                    return;
                }

                SelectionSet ss = selResult.Value;
                ObjectId[] selectedIds = ss.GetObjectIds();

                // ── Bước 3: Xử lý Layer hàng loạt (Thaw / TurnOn / Unlock) ──
                // Thu thập LayerId cần xử lý trước, rồi xử lý 1 lần duy nhất
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    HashSet<ObjectId> processedLayers = new HashSet<ObjectId>();
                    List<ObjectId> validIds = new List<ObjectId>(selectedIds.Length);

                    foreach (ObjectId id in selectedIds)
                    {
                        Entity ent = (Entity)tr.GetObject(id, OpenMode.ForRead);
                        if (ent == null || ent.IsErased) continue;

                        // Đếm loại
                        string dxf = ent.GetRXClass().DxfName;
                        if (dxf.Equals("3DSOLID", StringComparison.OrdinalIgnoreCase))
                            solidCount++;
                        else
                            bodyCount++;

                        // Xử lý layer (mỗi layer chỉ xử lý 1 lần)
                        if (processedLayers.Add(ent.LayerId))
                        {
                            LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(ent.LayerId, OpenMode.ForRead);
                            bool needWrite = ltr.IsFrozen || ltr.IsOff || ltr.IsLocked;
                            if (needWrite)
                            {
                                ltr.UpgradeOpen();
                                if (ltr.IsFrozen) { ltr.IsFrozen = false; thawedLayersCount++; }
                                if (ltr.IsOff) { ltr.IsOff = false; turnedOnLayersCount++; }
                                if (ltr.IsLocked) { ltr.IsLocked = false; unlockedLayersCount++; }
                            }
                        }

                        // Đảm bảo Visible
                        if (!ent.Visible)
                        {
                            ent.UpgradeOpen();
                            ent.Visible = true;
                        }

                        validIds.Add(id);
                    }

                    tr.Commit();

                    if (validIds.Count == 0)
                    {
                        ed.WriteMessage("\n[Thông báo] Không tìm thấy đối tượng 3D Solid hoặc Body hợp lệ nào.");
                        return;
                    }

                    // ── Bước 4: Set Implied Selection và gọi CUTCLIP ──
                    ed.SetImpliedSelection(validIds.ToArray());
                }

                doc.SendStringToExecute("_.CUTCLIP\n", true, false, false);

                // ── Bước 5: Thông báo kết quả ──
                ed.WriteMessage("\n=======================================================");
                ed.WriteMessage($"\n[Thành công] Đã CẮT (CUTCLIP) và nạp {solidCount + bodyCount} đối tượng vào Clipboard:");
                ed.WriteMessage($"\n  • 3D Solid: {solidCount} đối tượng");
                ed.WriteMessage($"\n  • Body:     {bodyCount} đối tượng");
                if (thawedLayersCount > 0)
                {
                    ed.WriteMessage($"\n  • Đã rã đông (Thaw): {thawedLayersCount} layer đóng băng để chọn đủ đối tượng.");
                }
                if (turnedOnLayersCount > 0)
                {
                    ed.WriteMessage($"\n  • Đã bật (Turn On): {turnedOnLayersCount} layer bị tắt để chọn đủ đối tượng.");
                }
                if (unlockedLayersCount > 0)
                {
                    ed.WriteMessage($"\n  • Đã mở khóa (Unlock): {unlockedLayersCount} layer bị khóa để có thể Cắt (Cut).");
                }
                if (switchedToModelSpace)
                {
                    ed.WriteMessage($"\n  • Đã chuyển từ Layout sang Model Space.");
                }
                ed.WriteMessage("\n-------------------------------------------------------");
                ed.WriteMessage("\n→ Toàn bộ đối tượng đã được CẮT vào Clipboard (đã xóa khỏi bản vẽ hiện hành)!");
                ed.WriteMessage("\n→ Sang bản vẽ mới:");
                ed.WriteMessage("\n   - Dùng lệnh 'PASTEORIG' để dán chính xác vào TỌA ĐỘ GỐC.");
                ed.WriteMessage("\n   - Dùng 'Ctrl+V' (PASTECLIP) để đặt vị trí tùy ý.");
                ed.WriteMessage("\n=======================================================\n");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n[Lỗi] Không thể cắt đối tượng vào Clipboard: {ex.Message}\n");
            }
        }
    }
}

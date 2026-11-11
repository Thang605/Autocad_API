using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.ApplicationServices;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(Civil3DCsharp.ATTXTEXP))]

namespace Civil3DCsharp
{
    public class ATTXTEXP
    {
        [CommandMethod("AT_Explode_Text")]
        public void ExplodeText()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            // 1. Select Text or MText
            TypedValue[] tvs = new TypedValue[]
            {
                new TypedValue((int)DxfCode.Operator, "<OR"),
                new TypedValue((int)DxfCode.Start, "TEXT"),
                new TypedValue((int)DxfCode.Start, "MTEXT"),
                new TypedValue((int)DxfCode.Operator, "OR>")
            };
            SelectionFilter filter = new SelectionFilter(tvs);
            PromptSelectionOptions pso = new PromptSelectionOptions();
            pso.MessageForAdding = "\n Select text to explode:";
            PromptSelectionResult psr = ed.GetSelection(pso, filter);

            if (psr.Status != PromptStatus.OK) return;
            SelectionSet ss = psr.Value;

            // 2. Save current view to restore later
            using (ViewTableRecord view = ed.GetCurrentView())
            {
                // 3. Process each object
                string tempFile = Path.Combine(Path.GetTempPath(), "temp_txtexp.wmf");
                
                try
                {
                    ObjectId[] ids = ss.GetObjectIds();
                    
                    // Turn off background plot and other interfering settings
                    object? oldBgPlot = Application.GetSystemVariable("BACKGROUNDPLOT");
                    object? oldCmdecho = Application.GetSystemVariable("CMDECHO");
                    object? oldFileDia = Application.GetSystemVariable("FILEDIA");

                    Application.SetSystemVariable("BACKGROUNDPLOT", 0);
                    Application.SetSystemVariable("CMDECHO", 0);
                    Application.SetSystemVariable("FILEDIA", 0);

                    try 
                    {
                        foreach (ObjectId id in ids)
                        {
                            ExplodeOneText(doc, id, tempFile);
                        }
                    }
                    finally
                    {
                        Application.SetSystemVariable("BACKGROUNDPLOT", oldBgPlot);
                        Application.SetSystemVariable("CMDECHO", oldCmdecho);
                        Application.SetSystemVariable("FILEDIA", oldFileDia);
                    }
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage($"\n Error: {ex.Message}");
                }
                finally
                {
                    if (File.Exists(tempFile))
                    {
                        try { File.Delete(tempFile); } catch { }
                    }
                    
                    // Restore view
                    ed.SetCurrentView(view);
                }
            }
        }

        private void ExplodeOneText(Document doc, ObjectId textId, string tempFile)
        {
            Database db = doc.Database;
            Editor ed = doc.Editor;
            
            ObjectId cloneId = ObjectId.Null;
            double originalRotation = 0;
            Point3d originalCenter = Point3d.Origin;
            ObjectId layerId = ObjectId.Null;
            int colorIndex = 256; // ByLayer
            ObjectId linetypeId = ObjectId.Null;
            LineWeight lineWeight = LineWeight.ByLineWeightDefault;

            // --- STEP 1: PREPARE (CLONE & NORMALIZE) ---
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                Entity? ent = tr.GetObject(textId, OpenMode.ForRead) as Entity;
                if (ent == null || (!(ent is DBText) && !(ent is MText))) return;

                layerId = ent.LayerId;
                colorIndex = ent.ColorIndex;
                linetypeId = ent.LinetypeId;
                lineWeight = ent.LineWeight;

                Extents3d e = ent.GeometricExtents;
                originalCenter = e.MinPoint + ((e.MaxPoint - e.MinPoint) / 2.0);

                if (ent is DBText txt) originalRotation = txt.Rotation;
                else if (ent is MText mtxt) originalRotation = mtxt.Rotation;

                // Clone
                Entity clone = (Entity)ent.Clone();
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                btr.AppendEntity(clone);
                tr.AddNewlyCreatedDBObject(clone, true);
                cloneId = clone.ObjectId;

                // Normalize Clone (Move to Origin, Rotate to 0)
                // 1. Un-Rotate
                if (clone is DBText cTxt) cTxt.Rotation = 0;
                else if (clone is MText cMtxt) cMtxt.Rotation = 0;

                // 2. Center at Origin
                Extents3d cloneExt = clone.GeometricExtents;
                Point3d cloneCenter = cloneExt.MinPoint + ((cloneExt.MaxPoint - cloneExt.MinPoint) / 2.0);
                Matrix3d moveMat = Matrix3d.Displacement(Point3d.Origin - cloneCenter);
                clone.TransformBy(moveMat);

                tr.Commit();
            }

            // --- STEP 2: EXECUTE WMF ---
            try
            {
                // Zoom to Clone
                Extents3d cloneExtents;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    Entity clone = (Entity)tr.GetObject(cloneId, OpenMode.ForRead);
                    cloneExtents = clone.GeometricExtents;
                    tr.Commit();
                }
                ZoomToExtents(ed, cloneExtents, 1.05);
                
                // Select Clone
                ed.SetImpliedSelection(new ObjectId[] { cloneId });

                // WMFOUT
                try { ed.Command("_.WMFOUT", tempFile, ""); }
                catch { throw new System.Exception("WMFOUT failed"); }

                // WMFIN
                try { ed.Command("_.WMFIN", tempFile, Point3d.Origin, "2.0", "2.0", "0.0"); }
                catch { throw new System.Exception("WMFIN failed"); }
            }
            catch
            {
                // Cleanup if fail
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    try {
                        Entity clone = (Entity)tr.GetObject(cloneId, OpenMode.ForWrite);
                        clone.Erase();
                    } catch {}
                    tr.Commit();
                }
                return;
            }

            // --- STEP 3: POST-PROCESS (EXPLODE & RESTORE) ---
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                
                // Find Last Entity (The WMF Block)
                // WMFIN creates a BlockReference or MInsertBlock
                ObjectId lastId = btr.Cast<ObjectId>().LastOrDefault();
                
                // Verify it's not our clone
                if (lastId == cloneId) 
                {
                    // Failed to import?
                    tr.Commit();
                    return;
                }

                Entity? wmfEnt = tr.GetObject(lastId, OpenMode.ForWrite) as Entity;
                if (wmfEnt is BlockReference wmfBlock)
                {
                    DBObjectCollection explodedObjects = new DBObjectCollection();
                    wmfBlock.Explode(explodedObjects);

                    // Calculate Restore Matrix
                    // We need to trace back from WMF Center -> Origin -> Original Center
                    
                    // 1. Get WMF Center
                    Extents3d wmfExt = new Extents3d();
                    bool hasExt = false;
                    foreach (DBObject obj in explodedObjects)
                    {
                        if (obj is Entity e && e is Curve)
                        {
                            try { wmfExt.AddExtents(e.GeometricExtents); hasExt = true; } catch {}
                        }
                    }

                    if (hasExt)
                    {
                        Point3d wmfCenter = wmfExt.MinPoint + ((wmfExt.MaxPoint - wmfExt.MinPoint) / 2.0);
                        
                        // Transform Logic:
                        // P_final = (P_wmf - wmfCenter) * Rotation(origRot) + origCenter
                        
                        Matrix3d alignMat = Matrix3d.Displacement(Point3d.Origin - wmfCenter);
                        Matrix3d rotMat = Matrix3d.Rotation(originalRotation, Vector3d.ZAxis, Point3d.Origin);
                        Matrix3d posMat = Matrix3d.Displacement(originalCenter - Point3d.Origin);
                        
                        Matrix3d finalMat = posMat * rotMat * alignMat;

                        foreach (DBObject obj in explodedObjects)
                        {
                            if (obj is Entity newEnt)
                            {
                                newEnt.TransformBy(finalMat);
                                newEnt.LayerId = layerId;
                                newEnt.ColorIndex = colorIndex;
                                newEnt.LinetypeId = linetypeId;
                                newEnt.LineWeight = lineWeight;

                                btr.AppendEntity(newEnt);
                                tr.AddNewlyCreatedDBObject(newEnt, true);
                            }
                        }
                    }
                    wmfBlock.Erase();
                }

                // Cleanup
                try {
                    Entity clone = (Entity)tr.GetObject(cloneId, OpenMode.ForWrite);
                    clone.Erase();
                    
                    Entity original = (Entity)tr.GetObject(textId, OpenMode.ForWrite);
                    original.Erase();
                } catch { }

                tr.Commit();
            }
        }


        private void ZoomToExtents(Editor ed, Extents3d ext, double factor)
        {
            using (ViewTableRecord view = ed.GetCurrentView())
            {
                // Calculate center and height
                Point3d min = ext.MinPoint;
                Point3d max = ext.MaxPoint;
                
                Point2d min2d = new Point2d(min.X, min.Y);
                Point2d max2d = new Point2d(max.X, max.Y);
                
                view.CenterPoint = min2d + ((max2d - min2d) / 2.0);
                view.Height = (max2d.Y - min2d.Y) * factor;
                view.Width = (max2d.X - min2d.X) * factor;
                
                ed.SetCurrentView(view);
            }
        }
    
    }
}

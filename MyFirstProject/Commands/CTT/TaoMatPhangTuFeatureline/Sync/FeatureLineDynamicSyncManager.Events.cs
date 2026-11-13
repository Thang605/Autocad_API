using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using CadEntity = Autodesk.AutoCAD.DatabaseServices.Entity;
using CivSurface = Autodesk.Civil.DatabaseServices.TinSurface;
using DBObject = Autodesk.AutoCAD.DatabaseServices.DBObject;
using DocumentCollectionEventArgs = Autodesk.AutoCAD.ApplicationServices.DocumentCollectionEventArgs;

namespace MyFirstProject.Civil_Tool_2
{
    /// <summary>
    /// Trình tự động lắng nghe thay đổi của Feature Line / Polyline nguồn và đồng bộ chính xác
    /// cả lưới tam giác lẫn ranh giới (Boundary / DeleteLines) của mặt phẳng phụ & toàn bộ mặt phẳng tổng phụ thuộc.
    /// </summary>
    public static partial class FeatureLineDynamicSyncManager
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessageW(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        private const uint WM_NULL = 0x0000;

        private const string StopSyncDomainKey = "Civil3DTools.FlSurfaceSync.StopAction";
        private static readonly object _syncLock = new object();
        private static bool _startedInThisAssembly = false;
        private static bool _idleHookAttached = false;
        private static bool _isInternalRebuilding = false;

        private static readonly HashSet<IntPtr> _attachedDocPtrs = new HashSet<IntPtr>();
        private static readonly HashSet<IntPtr> _scannedLegacyDbPtrs = new HashSet<IntPtr>();
        private static readonly Dictionary<IntPtr, HashSet<string>> _pendingHandlesByDb = new Dictionary<IntPtr, HashSet<string>>();
        private static readonly Dictionary<IntPtr, HashSet<string>> _pendingSurfaceHandlesByDb = new Dictionary<IntPtr, HashSet<string>>();
        private static readonly HashSet<IntPtr> _pendingLegacyScanDbPtrs = new HashSet<IntPtr>();

        public static void EnsureStarted()
        {
            lock (_syncLock)
            {
                AppDomain domain = AppDomain.CurrentDomain;
                if (_startedInThisAssembly)
                    return;

                if (domain.GetData(StopSyncDomainKey) is Action oldStop)
                {
                    try { oldStop(); } catch { }
                    domain.SetData(StopSyncDomainKey, null);
                }

                try
                {
                    Application.DocumentManager.DocumentCreated += OnDocumentCreated;
                    Application.DocumentManager.DocumentToBeDestroyed += OnDocumentToBeDestroyed;
                    Application.DocumentManager.DocumentBecameCurrent += OnDocumentBecameCurrent;

                    foreach (Document doc in Application.DocumentManager)
                    {
                        AttachDocument(doc);
                    }

                    domain.SetData(StopSyncDomainKey, new Action(Stop));
                    _startedInThisAssembly = true;
                    ScheduleIdleIfNeeded();
                }
                catch { }
            }
        }

        public static void Stop()
        {
            lock (_syncLock)
            {
                try
                {
                    Application.DocumentManager.DocumentCreated -= OnDocumentCreated;
                    Application.DocumentManager.DocumentToBeDestroyed -= OnDocumentToBeDestroyed;
                    Application.DocumentManager.DocumentBecameCurrent -= OnDocumentBecameCurrent;
                }
                catch { }

                try
                {
                    if (_idleHookAttached)
                    {
                        Application.Idle -= OnApplicationIdle;
                        _idleHookAttached = false;
                    }
                }
                catch { }

                try
                {
                    foreach (Document doc in Application.DocumentManager)
                    {
                        DetachDocument(doc);
                    }
                }
                catch { }

                _attachedDocPtrs.Clear();
                _scannedLegacyDbPtrs.Clear();
                _pendingHandlesByDb.Clear();
                _pendingSurfaceHandlesByDb.Clear();
                _pendingLegacyScanDbPtrs.Clear();
                _startedInThisAssembly = false;

                try
                {
                    if (AppDomain.CurrentDomain.GetData(StopSyncDomainKey) is Action curStop &&
                        curStop.Method == typeof(FeatureLineDynamicSyncManager).GetMethod(nameof(Stop)))
                    {
                        AppDomain.CurrentDomain.SetData(StopSyncDomainKey, null);
                    }
                }
                catch { }
            }
        }

        private static void OnDocumentCreated(object sender, DocumentCollectionEventArgs e)
        {
            if (e?.Document != null)
            {
                AttachDocument(e.Document);
                ScheduleIdleIfNeeded();
            }
        }

        private static void OnDocumentBecameCurrent(object sender, DocumentCollectionEventArgs e)
        {
            if (e?.Document != null)
            {
                AttachDocument(e.Document);
                ScheduleIdleIfNeeded();
            }
        }

        private static void OnDocumentToBeDestroyed(object sender, DocumentCollectionEventArgs e)
        {
            if (e?.Document != null)
            {
                DetachDocument(e.Document);
            }
        }

        private static void AttachDocument(Document doc)
        {
            if (doc == null || doc.IsDisposed) return;

            IntPtr docPtr = doc.UnmanagedObject;
            lock (_syncLock)
            {
                if (_attachedDocPtrs.Contains(docPtr)) return;

                try
                {
                    Database db = doc.Database;
                    if (db != null && !db.IsDisposed)
                    {
                        db.ObjectModified += OnDatabaseObjectModified;
                        db.ObjectErased += OnDatabaseObjectErased;
                        IntPtr dbPtr = db.UnmanagedObject;
                        if (!_scannedLegacyDbPtrs.Contains(dbPtr))
                        {
                            _pendingLegacyScanDbPtrs.Add(dbPtr);
                        }
                    }

                    doc.CommandEnded += OnDocumentCommandEnded;
                    doc.CommandCancelled += OnDocumentCommandEnded;
                    _attachedDocPtrs.Add(docPtr);
                }
                catch { }
            }
        }

        private static void DetachDocument(Document doc)
        {
            if (doc == null) return;

            lock (_syncLock)
            {
                try
                {
                    Database db = doc.Database;
                    if (db != null && !db.IsDisposed)
                    {
                        db.ObjectModified -= OnDatabaseObjectModified;
                        db.ObjectErased -= OnDatabaseObjectErased;
                        IntPtr dbPtr = db.UnmanagedObject;
                        _pendingHandlesByDb.Remove(dbPtr);
                        _pendingSurfaceHandlesByDb.Remove(dbPtr);
                        _pendingLegacyScanDbPtrs.Remove(dbPtr);
                        _scannedLegacyDbPtrs.Remove(dbPtr);
                    }
                }
                catch { }

                try
                {
                    doc.CommandEnded -= OnDocumentCommandEnded;
                    doc.CommandCancelled -= OnDocumentCommandEnded;
                }
                catch { }

                try { _attachedDocPtrs.Remove(doc.UnmanagedObject); } catch { }
            }
        }

        private static void OnDocumentCommandEnded(object sender, CommandEventArgs e)
        {
            if (_isInternalRebuilding) return;
            ScheduleIdleIfNeeded();
        }

        private static void OnDatabaseObjectModified(object sender, ObjectEventArgs e)
        {
            HandleDatabaseObjectChanged(sender as Database, e?.DBObject);
        }

        private static void OnDatabaseObjectErased(object sender, ObjectErasedEventArgs e)
        {
            HandleDatabaseObjectChanged(sender as Database, e?.DBObject);
        }

        private static void HandleDatabaseObjectChanged(Database? db, DBObject? dbObj)
        {
            if (_isInternalRebuilding || db == null || db.IsDisposed || dbObj == null)
                return;

            if (dbObj is CivSurface modSurf)
            {
                string surfHandle = modSurf.Handle.ToString();
                if (!string.IsNullOrEmpty(surfHandle))
                {
                    lock (_syncLock)
                    {
                        IntPtr dbPtr = db.UnmanagedObject;
                        if (!_pendingSurfaceHandlesByDb.TryGetValue(dbPtr, out var sSet))
                        {
                            sSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            _pendingSurfaceHandlesByDb[dbPtr] = sSet;
                        }
                        sSet.Add(surfHandle);
                        ScheduleIdleIfNeeded();
                    }
                }
                return;
            }

            string? targetHandle = null;
            if (dbObj is FeatureLine ||
                dbObj is AutoCorridorFeatureLine ||
                dbObj is Polyline ||
                dbObj is Polyline3d ||
                dbObj is Polyline2d ||
                dbObj is Line ||
                dbObj is Arc)
            {
                targetHandle = dbObj.Handle.ToString();
            }
            else if (dbObj is PolylineVertex3d || dbObj is Vertex2d)
            {
                ObjectId ownerId = dbObj.OwnerId;
                if (!ownerId.IsNull)
                {
                    targetHandle = ownerId.Handle.ToString();
                }
            }

            if (string.IsNullOrEmpty(targetHandle))
                return;

            lock (_syncLock)
            {
                IntPtr dbPtr = db.UnmanagedObject;
                if (!_pendingHandlesByDb.TryGetValue(dbPtr, out var set))
                {
                    set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    _pendingHandlesByDb[dbPtr] = set;
                }
                set.Add(targetHandle);
                ScheduleIdleIfNeeded();
            }
        }

        private static void ScheduleIdleIfNeeded()
        {
            lock (_syncLock)
            {
                bool hasWork = _pendingLegacyScanDbPtrs.Count > 0 ||
                               _pendingHandlesByDb.Count > 0 ||
                               _pendingSurfaceHandlesByDb.Count > 0;
                if (hasWork)
                {
                    if (!_idleHookAttached)
                    {
                        try
                        {
                            Application.Idle += OnApplicationIdle;
                            _idleHookAttached = true;
                        }
                        catch { }
                    }

                    try
                    {
                        PostMessageW(Application.MainWindow.Handle, WM_NULL, IntPtr.Zero, IntPtr.Zero);
                    }
                    catch { }
                }
            }
        }

        private static bool IsCommandActive()
        {
            try
            {
                object val = Application.GetSystemVariable("CMDACTIVE");
                return val != null && Convert.ToInt32(val) > 0;
            }
            catch
            {
                return false;
            }
        }

        private static void OnApplicationIdle(object? sender, EventArgs e)
        {
            if (_isInternalRebuilding) return;

            Document? activeDoc = null;
            try { activeDoc = Application.DocumentManager.MdiActiveDocument; } catch { }
            if (activeDoc == null || activeDoc.IsDisposed) return;

            try
            {
                if (!activeDoc.Editor.IsQuiescent || IsCommandActive())
                    return;
            }
            catch
            {
                return;
            }

            Database activeDb = activeDoc.Database;
            if (activeDb == null || activeDb.IsDisposed) return;
            IntPtr activeDbPtr = activeDb.UnmanagedObject;

            bool needLegacyScan = false;
            HashSet<string>? modifiedHandles = null;
            HashSet<string>? modifiedSurfaceHandles = null;

            lock (_syncLock)
            {
                needLegacyScan = _pendingLegacyScanDbPtrs.Remove(activeDbPtr);
                if (_pendingHandlesByDb.TryGetValue(activeDbPtr, out var queued) && queued.Count > 0)
                {
                    modifiedHandles = new HashSet<string>(queued, StringComparer.OrdinalIgnoreCase);
                    _pendingHandlesByDb.Remove(activeDbPtr);
                }
                if (_pendingSurfaceHandlesByDb.TryGetValue(activeDbPtr, out var queuedSurf) && queuedSurf.Count > 0)
                {
                    modifiedSurfaceHandles = new HashSet<string>(queuedSurf, StringComparer.OrdinalIgnoreCase);
                    _pendingSurfaceHandlesByDb.Remove(activeDbPtr);
                }

                if (_pendingLegacyScanDbPtrs.Count == 0 &&
                    _pendingHandlesByDb.Count == 0 &&
                    _pendingSurfaceHandlesByDb.Count == 0 &&
                    _idleHookAttached)
                {
                    try
                    {
                        Application.Idle -= OnApplicationIdle;
                        _idleHookAttached = false;
                    }
                    catch { }
                }
            }

            if (!needLegacyScan &&
                (modifiedHandles == null || modifiedHandles.Count == 0) &&
                (modifiedSurfaceHandles == null || modifiedSurfaceHandles.Count == 0))
            {
                return;
            }

            try
            {
                ProcessSyncForDocument(activeDoc, needLegacyScan, modifiedHandles, modifiedSurfaceHandles, forceAll: false, out _, out _);
            }
            catch { }
        }
    }
}

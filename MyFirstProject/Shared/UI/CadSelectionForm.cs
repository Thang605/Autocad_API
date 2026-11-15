using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using WinFormsLabel = System.Windows.Forms.Label;

namespace MyFirstProject.Shared.UI
{
    /// <summary>Modal selection and destination UI; concrete commands own their session state.</summary>
    public class CadSelectionForm : Form
    {
        private readonly Document _document;
        private readonly Database _database;
        private readonly ObjectId _databaseIdentity;
        private readonly CadSelectionState _state;
        private readonly string _sourcePrompt;
        private readonly string? _sourceDxfFilter;
        private readonly CadSelectionDestination _destination;
        private readonly string _destinationPrompt;
        private readonly WinFormsLabel _sourceStatus = FormBuilder.Label("");
        private readonly WinFormsLabel _destinationStatus = FormBuilder.Label("");
        private readonly WinFormsLabel _error = FormBuilder.Label("");
        private readonly Button _execute = FormBuilder.ActionButton("Thực hiện", ButtonKind.Primary);
        private ObjectId[] _sourceIds = [];
        private ObjectId _targetId = ObjectId.Null;
        private Point3d _point;
        private bool _hasPoint;

        /// <summary>Builds the fixed input surface for one concrete command.</summary>
        public CadSelectionForm(Document document, string title, string description,
            string sourcePrompt, string? sourceDxfFilter, CadSelectionDestination destination,
            string destinationPrompt, CadSelectionState state)
        {
            _document = document;
            _database = document.Database;
            _databaseIdentity = _database.BlockTableId;
            _state = state;
            _sourcePrompt = sourcePrompt;
            _sourceDxfFilter = sourceDxfFilter;
            _destination = destination;
            _destinationPrompt = destinationPrompt;
            BuildLayout(title, description);
            RestoreLastSettings();
            FormClosing += (_, _) => SaveCurrentSettings();
        }

        public ObjectId[] SelectedIds => (ObjectId[])_sourceIds.Clone();
        public ObjectId TargetId => _targetId;
        public Point3d SelectedPoint => _point;

        /// <summary>Rejects stale inputs without silently executing a surviving subset.</summary>
        public bool ValidateInputs()
        {
            _error.Text = "";
            if (!IsOriginalDrawing())
                return Reject("Bản vẽ hiện hành đã thay đổi. Hãy mở lại lệnh.");
            if (_sourceIds.Length == 0 || _sourceIds.Any(id => !PersistentState.IsAlive(id, _database)))
                return Reject("Hãy chọn lại đầy đủ đối tượng nguồn còn hợp lệ.");
            using Transaction tr = _database.TransactionManager.StartTransaction();
            if (_sourceIds.Any(id => !MatchesSource(tr.GetObject(id, OpenMode.ForRead))))
                return Reject("Đối tượng nguồn không phù hợp với bộ lọc của lệnh.");
            if (_destination == CadSelectionDestination.Text
                && (!PersistentState.IsAlive(_targetId, _database)
                    || !IsText(tr.GetObject(_targetId, OpenMode.ForRead))))
                return Reject("Hãy chọn Text/MText đích còn hợp lệ.");
            if (_destination == CadSelectionDestination.Point && !_hasPoint)
                return Reject("Hãy chọn điểm thao tác.");
            tr.Commit();
            return true;
        }

        /// <summary>Builds responsive sections with consistent keyboard actions.</summary>
        private void BuildLayout(string title, string description)
        {
            FormBuilder.ApplyStandardStyle(this, title, new Size(620, 430));
            var sourcePick = FormBuilder.ActionButton("Chọn đối tượng nguồn");
            sourcePick.Click += (_, _) => RunPick(PickSource);
            sourcePick.AccessibleName = "Chọn đối tượng nguồn trên bản vẽ";
            var destinationPick = FormBuilder.ActionButton(
                _destination == CadSelectionDestination.Text ? "Chọn Text/MText đích" : "Chọn điểm");
            destinationPick.Click += (_, _) => RunPick(PickDestination);
            var cancel = FormBuilder.ActionButton("Hủy");
            cancel.DialogResult = DialogResult.Cancel;
            _execute.Click += (_, _) => AcceptInputs();
            AcceptButton = _execute;
            CancelButton = cancel;
            _error.ForeColor = Color.DarkRed;
            _error.AccessibleName = "Thông báo xác thực đầu vào";
            var sections = new System.Collections.Generic.List<Control>
            {
                BuildSection("Thông tin", FormBuilder.Label(description)),
                BuildSection("Nguồn", _sourceStatus, sourcePick)
            };
            if (_destination != CadSelectionDestination.None)
                sections.Add(BuildSection(_destination == CadSelectionDestination.Point ? "Điểm thao tác" : "Đích kết quả", _destinationStatus, destinationPick));
            else
                destinationPick.Dispose();
            sections.Add(BuildSection("Trạng thái", _error));
            for (int i = 0; i < sections.Count; i++) sections[i].TabIndex = i;
            _execute.TabIndex = 0;
            cancel.TabIndex = 1;
            FormBuilder.BuildBody(this, FormBuilder.ButtonBar(_execute, cancel), sections.ToArray());
        }

        /// <summary>Creates a resizable single-column group without clipped labels.</summary>
        private static GroupBox BuildSection(string title, params Control[] controls)
        {
            var group = new GroupBox { Text = title, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(10) };
            var grid = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = controls.Length };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < controls.Length; i++)
            {
                controls[i].TabIndex = i;
                controls[i].Dock = DockStyle.Top;
                if (controls[i] is WinFormsLabel label) label.MaximumSize = new Size(540, 0);
                grid.Controls.Add(controls[i], 0, i);
            }
            group.Controls.Add(grid);
            return group;
        }

        /// <summary>Keeps CAD errors within the modal interaction and reports their original message.</summary>
        private void RunPick(Action pick)
        {
            try
            {
                if (!IsOriginalDrawing())
                {
                    Reject("Bản vẽ hiện hành đã thay đổi. Hãy mở lại lệnh.");
                    return;
                }
                using (_document.Editor.StartUserInteraction(this)) pick();
                UpdateStatus();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                _document.Editor.WriteMessage(ex.Message);
                Reject(ex.Message);
            }
        }

        /// <summary>Retains previous source selection when the canvas prompt is canceled.</summary>
        private void PickSource()
        {
            var options = new PromptSelectionOptions { MessageForAdding = _sourcePrompt };
            var result = _sourceDxfFilter == null
                ? _document.Editor.GetSelection(options)
                : _document.Editor.GetSelection(options,
                    new SelectionFilter([new TypedValue((int)DxfCode.Start, _sourceDxfFilter)]));
            if (result.Status == PromptStatus.OK && result.Value != null)
                _sourceIds = result.Value.GetObjectIds();
        }

        /// <summary>Captures raw point coordinates or a text target only on successful prompts.</summary>
        private void PickDestination()
        {
            if (_destination == CadSelectionDestination.Point)
            {
                var result = _document.Editor.GetPoint(new PromptPointOptions(_destinationPrompt));
                if (result.Status == PromptStatus.OK)
                {
                    _point = result.Value;
                    _hasPoint = true;
                }
                return;
            }
            var options = new PromptEntityOptions(_destinationPrompt);
            options.SetRejectMessage("\n- Bạn phải chọn đúng đối tượng Text hoặc MText!");
            options.AddAllowedClass(typeof(DBText), true);
            options.AddAllowedClass(typeof(MText), true);
            var entity = _document.Editor.GetEntity(options);
            if (entity.Status == PromptStatus.OK) _targetId = entity.ObjectId;
        }

        /// <summary>Closes only after validating all input values against the current drawing.</summary>
        private void AcceptInputs()
        {
            try
            {
                if (!ValidateInputs()) return;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                _document.Editor.WriteMessage(ex.Message);
                Reject(ex.Message);
            }
        }

        /// <summary>Restores only live, correctly typed inputs belonging to this database.</summary>
        private void RestoreLastSettings()
        {
            PersistentState.RestoreFormSize(this, _state.FormSize);
            if (MatchesDatabase(_state.Database))
            {
                using Transaction tr = _database.TransactionManager.StartTransaction();
                _sourceIds = PersistentState.FilterAlive(_state.SourceIds, _database)
                    .Where(id => MatchesSource(tr.GetObject(id, OpenMode.ForRead))).ToArray();
                if (PersistentState.IsAlive(_state.TargetId, _database)
                    && IsText(tr.GetObject(_state.TargetId, OpenMode.ForRead))) _targetId = _state.TargetId;
                _point = _state.Point;
                _hasPoint = _state.HasPoint;
                tr.Commit();
            }
            UpdateStatus();
        }

        /// <summary>Saves this concrete command's choices and normal window size for the session.</summary>
        private void SaveCurrentSettings()
        {
            _state.Database = _database;
            _state.SourceIds = SelectedIds;
            _state.TargetId = _targetId;
            _state.Point = _point;
            _state.HasPoint = _hasPoint;
            _state.FormSize = PersistentState.CaptureFormSize(this, _state.FormSize);
        }

        /// <summary>Matches the original source filter while preserving unfiltered command inputs.</summary>
        private bool MatchesSource(DBObject source) => _sourceDxfFilter == null
            || (_sourceDxfFilter == "DIMENSION" ? source is Dimension : source.GetRXClass().DxfName == _sourceDxfFilter);

        /// <summary>Recognizes both supported destination types.</summary>
        private static bool IsText(DBObject value) => value is DBText or MText;

        /// <summary>Compares database-owned identity instead of transient managed wrappers.</summary>
        private bool MatchesDatabase(Database? database)
        {
            try
            {
                return database != null && PersistentState.IsAlive(_databaseIdentity)
                    && database.BlockTableId == _databaseIdentity;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (Autodesk.AutoCAD.Runtime.Exception)
            {
                return false;
            }
        }

        /// <summary>Rejects a changed or closed drawing before picking or accepting inputs.</summary>
        private bool IsOriginalDrawing()
        {
            try
            {
                var activeDocument = Application.DocumentManager.MdiActiveDocument;
                return activeDocument != null && MatchesDatabase(activeDocument.Database)
                    && MatchesDatabase(_document.Database);
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            catch (Autodesk.AutoCAD.Runtime.Exception)
            {
                return false;
            }
        }

        /// <summary>Displays validation failures without nesting another modal dialog.</summary>
        private bool Reject(string message)
        {
            _error.Text = message;
            for (Control? parent = _error.Parent; parent != null; parent = parent.Parent)
            {
                if (parent is ScrollableControl { AutoScroll: true } scroller)
                    scroller.ScrollControlIntoView(_error);
            }
            TryWriteValidationMessage(message);
            return false;
        }

        /// <summary>Keeps validation visible even if the original editor is no longer available.</summary>
        private void TryWriteValidationMessage(string message)
        {
            try
            {
                _document.Editor.WriteMessage("\n" + message);
            }
            catch (ObjectDisposedException)
            {
                // The on-form message remains available after a document closes.
            }
            catch (Autodesk.AutoCAD.Runtime.Exception)
            {
                // Editor feedback must not prevent displaying the validation failure.
            }
        }

        /// <summary>Reflects selected values and disables execution while required inputs are absent.</summary>
        private void UpdateStatus()
        {
            _sourceStatus.Text = $"Đã chọn {_sourceIds.Length} đối tượng.";
            _destinationStatus.Text = _destination == CadSelectionDestination.Point
                ? (_hasPoint ? $"Vị trí: {_point}" : "Chưa chọn vị trí.")
                : (_targetId.IsNull ? "Chưa chọn Text/MText." : $"Text/MText: {_targetId}");
            _execute.Enabled = _sourceIds.Length > 0 && (_destination == CadSelectionDestination.None
                || (_destination == CadSelectionDestination.Point ? _hasPoint : !_targetId.IsNull));
            _error.Text = "";
        }
    }
}

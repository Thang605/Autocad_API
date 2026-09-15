using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.Civil.DatabaseServices;
using MyFirstProject.Extensions;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsFont = System.Drawing.Font;
using WinFormsPoint = System.Drawing.Point;

namespace MyFirstProject.Civil_Tool
{
    /// <summary>
    /// Form nhập dữ liệu cho lệnh tạo Corridor Rẽ Phải
    /// Enhanced with ObjectId memory and improved object picking
    /// </summary>
    public partial class CorridorRePhai_InputForm : Form
    {
        // Static variables to remember last input values (names for fallback)
        private static string _lastCorridorName = "";
 private static string _lastTargetAlignment1Name = "";
   private static string _lastTargetAlignment2Name = "";
        private static string _lastAssemblyName = "";
        private static int _lastNumberOfAlignments = 1;

        // NEW: Remember actual ObjectIds for better persistence
        private static ObjectId _lastCorridorId = ObjectId.Null;
 private static ObjectId _lastTargetAlignment1Id = ObjectId.Null;
   private static ObjectId _lastTargetAlignment2Id = ObjectId.Null;
        private static ObjectId _lastAssemblyId = ObjectId.Null;

      // Properties to return data
        public ObjectId SelectedCorridorId { get; private set; } = ObjectId.Null;
    public ObjectId TargetAlignment1Id { get; private set; } = ObjectId.Null;
        public ObjectId TargetAlignment2Id { get; private set; } = ObjectId.Null;
     public ObjectId SelectedAssemblyId { get; private set; } = ObjectId.Null;
        public string SelectedAssemblyName { get; private set; } = "";
        public int NumberOfTurnAlignments { get; private set; } = 1;
        public bool FormAccepted { get; private set; } = false;

        // UI Controls
        private WinFormsLabel lblTitle = null!;
        private WinFormsLabel lblCorridor = null!;
        private WinFormsLabel lblTargetAlignment1 = null!;
        private WinFormsLabel lblTargetAlignment2 = null!;
     private WinFormsLabel lblAssembly = null!;
     private WinFormsLabel lblNumberOfAlignments = null!;
        
        private ComboBox cmbCorridor = null!;
        private ComboBox cmbTargetAlignment1 = null!;
        private ComboBox cmbTargetAlignment2 = null!;
        private ComboBox cmbAssembly = null!;
  private NumericUpDown numNumberOfAlignments = null!;
        
        private Button btnPickCorridor = null!;
        private Button btnPickAlignment1 = null!;
        private Button btnPickAlignment2 = null!;
    private Button btnPickAssembly = null!;
        
        private Button btnOK = null!;
   private Button btnCancel = null!;
        private GroupBox grpBasicSettings = null!;

        // Data storage
        private Dictionary<string, ObjectId> _corridorDict = new();
  private Dictionary<string, ObjectId> _alignmentDict = new();
        private Dictionary<string, ObjectId> _assemblyDict = new();

   public CorridorRePhai_InputForm()
        {
     InitializeComponent();
         LoadData();
            RestoreLastUsedValues();
    }

        private void InitializeComponent()
{
            // Initialize controls
     this.lblTitle = new WinFormsLabel();
 this.lblCorridor = new WinFormsLabel();
            this.lblTargetAlignment1 = new WinFormsLabel();
    this.lblTargetAlignment2 = new WinFormsLabel();
          this.lblAssembly = new WinFormsLabel();
         this.lblNumberOfAlignments = new WinFormsLabel();
            
      this.cmbCorridor = new ComboBox();
        this.cmbTargetAlignment1 = new ComboBox();
      this.cmbTargetAlignment2 = new ComboBox();
      this.cmbAssembly = new ComboBox();
     this.numNumberOfAlignments = new NumericUpDown();
        
            this.btnPickCorridor = new Button();
          this.btnPickAlignment1 = new Button();
            this.btnPickAlignment2 = new Button();
         this.btnPickAssembly = new Button();
            
 this.btnOK = new Button();
   this.btnCancel = new Button();
       this.grpBasicSettings = new GroupBox();

        this.SuspendLayout();

            // Form
   this.Text = "Tạo Corridor Rẽ Phải - Đường Đô Thị";
            this.Size = new Size(600, 480);
        this.StartPosition = FormStartPosition.CenterScreen;
      this.FormBorderStyle = FormBorderStyle.FixedDialog;
      this.MaximizeBox = false;
       this.MinimizeBox = false;

            // Title Label
            this.lblTitle.Text = "THIẾT LẬP THÔNG SỐ TẠO CORRIDOR RẼ PHẢI";
          this.lblTitle.Font = new WinFormsFont("Microsoft Sans Serif", 11F, FontStyle.Bold);
            this.lblTitle.Location = new WinFormsPoint(20, 15);
            this.lblTitle.Size = new Size(550, 25);
            this.lblTitle.TextAlign = ContentAlignment.MiddleCenter;
            this.lblTitle.ForeColor = Color.DarkBlue;

            // Basic Settings Group
    this.grpBasicSettings.Text = "Cấu hình cơ bản";
            this.grpBasicSettings.Location = new WinFormsPoint(12, 50);
            this.grpBasicSettings.Size = new Size(560, 330);

        // Corridor Label & ComboBox & Pick Button
            this.lblCorridor.Text = "Corridor chính:";
         this.lblCorridor.Location = new WinFormsPoint(15, 35);
  this.lblCorridor.Size = new Size(150, 23);

            this.cmbCorridor.Location = new WinFormsPoint(170, 32);
            this.cmbCorridor.Size = new Size(295, 23);
            this.cmbCorridor.DropDownStyle = ComboBoxStyle.DropDownList;

          this.btnPickCorridor.Text = "📍";
            this.btnPickCorridor.Location = new WinFormsPoint(470, 31);
         this.btnPickCorridor.Size = new Size(70, 25);
            this.btnPickCorridor.Font = new WinFormsFont("Microsoft Sans Serif", 8F);
            this.btnPickCorridor.Click += BtnPickCorridor_Click;
            this.btnPickCorridor.UseVisualStyleBackColor = true;

  // Target Alignment 1 Label & ComboBox & Pick Button
            this.lblTargetAlignment1.Text = "Target Alignment 1 (Trái):";
            this.lblTargetAlignment1.Location = new WinFormsPoint(15, 75);
            this.lblTargetAlignment1.Size = new Size(150, 23);

       this.cmbTargetAlignment1.Location = new WinFormsPoint(170, 72);
      this.cmbTargetAlignment1.Size = new Size(295, 23);
     this.cmbTargetAlignment1.DropDownStyle = ComboBoxStyle.DropDownList;

this.btnPickAlignment1.Text = "📍";
            this.btnPickAlignment1.Location = new WinFormsPoint(470, 71);
            this.btnPickAlignment1.Size = new Size(70, 25);
        this.btnPickAlignment1.Font = new WinFormsFont("Microsoft Sans Serif", 8F);
        this.btnPickAlignment1.Click += BtnPickAlignment1_Click;
     this.btnPickAlignment1.UseVisualStyleBackColor = true;

            // Target Alignment 2 Label & ComboBox & Pick Button
   this.lblTargetAlignment2.Text = "Target Alignment 2 (Phải):";
            this.lblTargetAlignment2.Location = new WinFormsPoint(15, 115);
   this.lblTargetAlignment2.Size = new Size(150, 23);

            this.cmbTargetAlignment2.Location = new WinFormsPoint(170, 112);
     this.cmbTargetAlignment2.Size = new Size(295, 23);
 this.cmbTargetAlignment2.DropDownStyle = ComboBoxStyle.DropDownList;

            this.btnPickAlignment2.Text = "📍";
    this.btnPickAlignment2.Location = new WinFormsPoint(470, 111);
this.btnPickAlignment2.Size = new Size(70, 25);
      this.btnPickAlignment2.Font = new WinFormsFont("Microsoft Sans Serif", 8F);
            this.btnPickAlignment2.Click += BtnPickAlignment2_Click;
      this.btnPickAlignment2.UseVisualStyleBackColor = true;

    // Assembly Label & ComboBox & Pick Button
            this.lblAssembly.Text = "Assembly:";
        this.lblAssembly.Location = new WinFormsPoint(15, 155);
      this.lblAssembly.Size = new Size(150, 23);

this.cmbAssembly.Location = new WinFormsPoint(170, 152);
       this.cmbAssembly.Size = new Size(295, 23);
  this.cmbAssembly.DropDownStyle = ComboBoxStyle.DropDownList;

      this.btnPickAssembly.Text = "📍";
         this.btnPickAssembly.Location = new WinFormsPoint(470, 151);
        this.btnPickAssembly.Size = new Size(70, 25);
this.btnPickAssembly.Font = new WinFormsFont("Microsoft Sans Serif", 8F);
            this.btnPickAssembly.Click += BtnPickAssembly_Click;
      this.btnPickAssembly.UseVisualStyleBackColor = true;

            // Number of Alignments Label & NumericUpDown
      this.lblNumberOfAlignments.Text = "Số lượng đường rẽ phải:";
this.lblNumberOfAlignments.Location = new WinFormsPoint(15, 195);
     this.lblNumberOfAlignments.Size = new Size(150, 23);

       this.numNumberOfAlignments.Location = new WinFormsPoint(170, 192);
  this.numNumberOfAlignments.Size = new Size(120, 23);
            this.numNumberOfAlignments.Minimum = 1;
            this.numNumberOfAlignments.Maximum = 50;
       this.numNumberOfAlignments.Value = _lastNumberOfAlignments > 0 ? _lastNumberOfAlignments : 1;

        // Info Label
            var lblInfo = new WinFormsLabel
     {
    Text = "💡 Tip: Nhấn nút 📍 để chọn trực tiếp trên model\n" +
      "Sau khi nhấn OK, bạn sẽ chọn từng cặp Alignment-Polyline.",
     Location = new WinFormsPoint(15, 240),
         Size = new Size(525, 50),
      ForeColor = Color.DarkGreen,
      Font = new WinFormsFont("Microsoft Sans Serif", 9F, FontStyle.Italic)
     };

            var lblLastUsed = new WinFormsLabel
            {
    Text = "🔄 Các giá trị được ghi nhớ từ lần chạy trước",
    Location = new WinFormsPoint(15, 295),
    Size = new Size(525, 20),
    ForeColor = Color.Gray,
       Font = new WinFormsFont("Microsoft Sans Serif", 8F, FontStyle.Italic)
     };

         // OK Button
          this.btnOK.Text = "OK";
this.btnOK.Location = new WinFormsPoint(370, 395);
            this.btnOK.Size = new Size(90, 35);
            this.btnOK.Font = new WinFormsFont("Microsoft Sans Serif", 9F, FontStyle.Bold);
        this.btnOK.Click += BtnOK_Click;

            // Cancel Button
            this.btnCancel.Text = "Hủy";
         this.btnCancel.Location = new WinFormsPoint(470, 395);
this.btnCancel.Size = new Size(90, 35);
  this.btnCancel.Click += BtnCancel_Click;

            // Add controls to group
     this.grpBasicSettings.Controls.AddRange(new Control[] {
          lblCorridor, cmbCorridor, btnPickCorridor,
  lblTargetAlignment1, cmbTargetAlignment1, btnPickAlignment1,
       lblTargetAlignment2, cmbTargetAlignment2, btnPickAlignment2,
             lblAssembly, cmbAssembly, btnPickAssembly,
  lblNumberOfAlignments, numNumberOfAlignments,
      lblInfo, lblLastUsed
            });

      // Add controls to form
      this.Controls.AddRange(new Control[] {
           lblTitle,
      grpBasicSettings,
        btnOK,
          btnCancel
        });

            this.ResumeLayout(false);
        }

        private void LoadData()
        {
     try
            {
     using (var tr = A.Db.TransactionManager.StartTransaction())
        {
           // Load Corridors
         LoadCorridors(tr);

         // Load Alignments
   LoadAlignments(tr);

                  // Load Assemblies
        LoadAssemblies(tr);

           tr.Commit();
   }
            }
   catch (System.Exception ex)
            {
     MessageBox.Show($"Lỗi khi tải dữ liệu: {ex.Message}", 
         "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        }

    private void LoadCorridors(Transaction tr)
        {
     cmbCorridor.Items.Clear();
            _corridorDict.Clear();

        try
   {
              foreach (ObjectId corridorId in A.Cdoc.CorridorCollection)
          {
        if (tr.GetObject(corridorId, OpenMode.ForRead) is Corridor corridor)
  {
 string name = corridor.Name ?? "Unnamed";
    cmbCorridor.Items.Add(name);
    _corridorDict[name] = corridorId;
             }
                }

    if (cmbCorridor.Items.Count > 0)
        {
cmbCorridor.SelectedIndex = 0;
      }
            }
          catch (System.Exception ex)
   {
     A.Ed.WriteMessage($"\nLỗi khi tải corridors: {ex.Message}");
            }
     }

private void LoadAlignments(Transaction tr)
        {
      cmbTargetAlignment1.Items.Clear();
  cmbTargetAlignment2.Items.Clear();
            _alignmentDict.Clear();

  try
        {
    // Get alignments using the correct API
     ObjectIdCollection alignmentIds = A.Cdoc.GetAlignmentIds();
   foreach (ObjectId alignmentId in alignmentIds)
       {
   if (tr.GetObject(alignmentId, OpenMode.ForRead) is Alignment alignment)
    {
      string name = alignment.Name ?? "Unnamed";
      cmbTargetAlignment1.Items.Add(name);
cmbTargetAlignment2.Items.Add(name);
    _alignmentDict[name] = alignmentId;
     }
           }

      if (cmbTargetAlignment1.Items.Count > 0)
           {
        cmbTargetAlignment1.SelectedIndex = 0;
 }

                if (cmbTargetAlignment2.Items.Count > 1)
   {
    cmbTargetAlignment2.SelectedIndex = 1;
         }
              else if (cmbTargetAlignment2.Items.Count > 0)
       {
      cmbTargetAlignment2.SelectedIndex = 0;
  }
         }
            catch (System.Exception ex)
            {
    A.Ed.WriteMessage($"\nLỗi khi tải alignments: {ex.Message}");
            }
    }

        private void LoadAssemblies(Transaction tr)
        {
cmbAssembly.Items.Clear();
            _assemblyDict.Clear();

  try
          {
          foreach (ObjectId assemblyId in A.Cdoc.AssemblyCollection)
    {
        if (tr.GetObject(assemblyId, OpenMode.ForRead) is Assembly assembly)
      {
        string name = assembly.Name ?? "Unnamed";
     cmbAssembly.Items.Add(name);
              _assemblyDict[name] = assemblyId;
         }
   }

     if (cmbAssembly.Items.Count > 0)
    {
        cmbAssembly.SelectedIndex = 0;
       }
            }
            catch (System.Exception ex)
  {
     A.Ed.WriteMessage($"\nLỗi khi tải assemblies: {ex.Message}");
    }
        }

        /// <summary>
   /// Restore last used values with ObjectId-based memory
        /// Prioritizes ObjectId over name for better persistence
      /// </summary>
        private void RestoreLastUsedValues()
        {
            try
    {
        using (var tr = A.Db.TransactionManager.StartTransaction())
           {
          bool restoredAny = false;

   // Restore corridor selection (try ObjectId first, then name)
           if (_lastCorridorId != ObjectId.Null && !_lastCorridorId.IsErased)
 {
             if (TryRestoreObjectById(tr, _lastCorridorId, cmbCorridor, _corridorDict))
   {
   restoredAny = true;
 A.Ed.WriteMessage("\n✅ Restored Corridor from last session");
        }
       }
  else if (!string.IsNullOrEmpty(_lastCorridorName) && cmbCorridor.Items.Contains(_lastCorridorName))
       {
        cmbCorridor.SelectedItem = _lastCorridorName;
              restoredAny = true;
       }

 // Restore target alignment 1
              if (_lastTargetAlignment1Id != ObjectId.Null && !_lastTargetAlignment1Id.IsErased)
      {
     if (TryRestoreObjectById(tr, _lastTargetAlignment1Id, cmbTargetAlignment1, _alignmentDict))
       {
  restoredAny = true;
   A.Ed.WriteMessage("\n✅ Restored Target Alignment 1 from last session");
         }
     }
           else if (!string.IsNullOrEmpty(_lastTargetAlignment1Name) && cmbTargetAlignment1.Items.Contains(_lastTargetAlignment1Name))
        {
  cmbTargetAlignment1.SelectedItem = _lastTargetAlignment1Name;
            restoredAny = true;
   }

            // Restore target alignment 2
           if (_lastTargetAlignment2Id != ObjectId.Null && !_lastTargetAlignment2Id.IsErased)
            {
    if (TryRestoreObjectById(tr, _lastTargetAlignment2Id, cmbTargetAlignment2, _alignmentDict))
        {
         restoredAny = true;
       A.Ed.WriteMessage("\n✅ Restored Target Alignment 2 from last session");
   }
       }
   else if (!string.IsNullOrEmpty(_lastTargetAlignment2Name) && cmbTargetAlignment2.Items.Contains(_lastTargetAlignment2Name))
          {
     cmbTargetAlignment2.SelectedItem = _lastTargetAlignment2Name;
                 restoredAny = true;
                    }

         // Restore assembly
     if (_lastAssemblyId != ObjectId.Null && !_lastAssemblyId.IsErased)
           {
     if (TryRestoreObjectById(tr, _lastAssemblyId, cmbAssembly, _assemblyDict))
     {
     restoredAny = true;
          A.Ed.WriteMessage("\n✅ Restored Assembly from last session");
}
          }
    else if (!string.IsNullOrEmpty(_lastAssemblyName) && cmbAssembly.Items.Contains(_lastAssemblyName))
      {
       cmbAssembly.SelectedItem = _lastAssemblyName;
         restoredAny = true;
     }

 // Restore number of alignments
 if (_lastNumberOfAlignments > 0)
        {
        numNumberOfAlignments.Value = _lastNumberOfAlignments;
          }

if (restoredAny)
               {
                     A.Ed.WriteMessage("\n🔄 Form values restored from last session");
        }

     tr.Commit();
    }
      }
            catch (System.Exception ex)
            {
       A.Ed.WriteMessage($"\n⚠️ Lỗi khi khôi phục giá trị: {ex.Message}");
            }
        }

        /// <summary>
        /// Try to restore object by ObjectId and select it in combobox
        /// Returns true if successful
        /// </summary>
        private bool TryRestoreObjectById(Transaction tr, ObjectId objId, ComboBox combo, Dictionary<string, ObjectId> dict)
        {
    try
        {
            if (objId == ObjectId.Null || objId.IsErased)
return false;

            var obj = tr.GetObject(objId, OpenMode.ForRead);
                if (obj == null)
      return false;

        string name = "";
 if (obj is Corridor corridor)
     name = corridor.Name ?? "Unnamed";
           else if (obj is Alignment alignment)
  name = alignment.Name ?? "Unnamed";
     else if (obj is Assembly assembly)
    name = assembly.Name ?? "Unnamed";

   if (!string.IsNullOrEmpty(name))
   {
        // Ensure it's in the dictionary
         if (!dict.ContainsKey(name))
        {
    dict[name] = objId;
          if (!combo.Items.Contains(name))
   {
   combo.Items.Add(name);
      }
          }

            // Select it
       combo.SelectedItem = name;
return true;
     }
}
    catch (System.Exception ex)
        {
         A.Ed.WriteMessage($"\n⚠️ Could not restore object: {ex.Message}");
            }

        return false;
   }

    /// <summary>
        /// Save last used values including ObjectIds for better persistence
        /// </summary>
private void SaveLastUsedValues()
        {
      try
            {
        // Save names (fallback)
    _lastCorridorName = cmbCorridor.SelectedItem?.ToString() ?? "";
           _lastTargetAlignment1Name = cmbTargetAlignment1.SelectedItem?.ToString() ?? "";
                _lastTargetAlignment2Name = cmbTargetAlignment2.SelectedItem?.ToString() ?? "";
        _lastAssemblyName = cmbAssembly.SelectedItem?.ToString() ?? "";
           _lastNumberOfAlignments = (int)numNumberOfAlignments.Value;

         // Save ObjectIds (primary)
         _lastCorridorId = SelectedCorridorId;
          _lastTargetAlignment1Id = TargetAlignment1Id;
     _lastTargetAlignment2Id = TargetAlignment2Id;
           _lastAssemblyId = SelectedAssemblyId;

     A.Ed.WriteMessage("\n💾 Form settings saved for next session");
            }
         catch (System.Exception ex)
      {
      A.Ed.WriteMessage($"\n⚠️ Lỗi khi lưu giá trị: {ex.Message}");
         }
    }

        // Pick from Model event handlers - Enhanced with better feedback
        private void BtnPickCorridor_Click(object? sender, EventArgs e)
        {
            try
            {
                ObjectId pickedId = ObjectId.Null;
                using (var interaction = A.Ed.StartUserInteraction(this))
                {
                    pickedId = UserInput.GCorridorId("\nChọn corridor trên model: ");
                }

                if (pickedId != ObjectId.Null)
                {
                    using (var tr = A.Db.TransactionManager.StartTransaction())
                    {
                        if (tr.GetObject(pickedId, OpenMode.ForRead) is Corridor corridor)
                        {
                            string name = corridor.Name ?? "Unnamed";
                            
                            // Add to dictionary if not exists
                            if (!_corridorDict.ContainsKey(name))
                            {
                                _corridorDict[name] = pickedId;
                                cmbCorridor.Items.Add(name);
                            }
                            else
                            {
                                // Update the ObjectId in case it changed
                                _corridorDict[name] = pickedId;
                            }
                            
                            // Select in combobox
                            cmbCorridor.SelectedItem = name;
                            
                            // Console feedback only - no MessageBox
                            A.Ed.WriteMessage($"\n✅ Đã chọn corridor: {name}");
                        }
                        tr.Commit();
                    }
                }
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n❌ Lỗi: {ex.Message}");
            }
        }

        private void BtnPickAlignment1_Click(object? sender, EventArgs e)
        {
            try
            {
                ObjectId pickedId = ObjectId.Null;
                using (var interaction = A.Ed.StartUserInteraction(this))
                {
                    pickedId = UserInput.GAlignmentId("\nChọn Target Alignment 1 trên model: ");
                }

                if (pickedId != ObjectId.Null)
                {
                    using (var tr = A.Db.TransactionManager.StartTransaction())
                    {
                        if (tr.GetObject(pickedId, OpenMode.ForRead) is Alignment alignment)
                        {
                            string name = alignment.Name ?? "Unnamed";
                            
                            // Add to dictionary if not exists
                            if (!_alignmentDict.ContainsKey(name))
                            {
                                _alignmentDict[name] = pickedId;
                                cmbTargetAlignment1.Items.Add(name);
                                cmbTargetAlignment2.Items.Add(name);
                            }
                            else
                            {
                                _alignmentDict[name] = pickedId;
                            }
                            
                            // Select in combobox
                            cmbTargetAlignment1.SelectedItem = name;
                            
                            // Console feedback only - no MessageBox
                            A.Ed.WriteMessage($"\n✅ Đã chọn Target Alignment 1: {name}");
                        }
                        tr.Commit();
                    }
                }
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n❌ Lỗi: {ex.Message}");
            }
        }

        private void BtnPickAlignment2_Click(object? sender, EventArgs e)
        {
            try
            {
                ObjectId pickedId = ObjectId.Null;
                using (var interaction = A.Ed.StartUserInteraction(this))
                {
                    pickedId = UserInput.GAlignmentId("\nChọn Target Alignment 2 trên model: ");
                }

                if (pickedId != ObjectId.Null)
                {
                    using (var tr = A.Db.TransactionManager.StartTransaction())
                    {
                        if (tr.GetObject(pickedId, OpenMode.ForRead) is Alignment alignment)
                        {
                            string name = alignment.Name ?? "Unnamed";
                            
                            // Add to dictionary if not exists
                            if (!_alignmentDict.ContainsKey(name))
                            {
                                _alignmentDict[name] = pickedId;
                                cmbTargetAlignment1.Items.Add(name);
                                cmbTargetAlignment2.Items.Add(name);
                            }
                            else
                            {
                                _alignmentDict[name] = pickedId;
                            }
                            
                            // Select in combobox
                            cmbTargetAlignment2.SelectedItem = name;
                            
                            // Console feedback only - no MessageBox
                            A.Ed.WriteMessage($"\n✅ Đã chọn Target Alignment 2: {name}");
                        }
                        tr.Commit();
                    }
                }
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n❌ Lỗi: {ex.Message}");
            }
        }

        private void BtnPickAssembly_Click(object? sender, EventArgs e)
        {
            try
            {
                ObjectId pickedId = ObjectId.Null;
                using (var interaction = A.Ed.StartUserInteraction(this))
                {
                    pickedId = UserInput.GSelectionAnObject("\nChọn assembly trên model: ");
                }

                if (pickedId != ObjectId.Null)
                {
                    using (var tr = A.Db.TransactionManager.StartTransaction())
                    {
                        if (tr.GetObject(pickedId, OpenMode.ForRead) is Assembly assembly)
                        {
                            string name = assembly.Name ?? "Unnamed";
                            
                            // Add to dictionary if not exists
                            if (!_assemblyDict.ContainsKey(name))
                            {
                                _assemblyDict[name] = pickedId;
                                cmbAssembly.Items.Add(name);
                            }
                            else
                            {
                                _assemblyDict[name] = pickedId;
                            }
                            
                            // Select in combobox
                            cmbAssembly.SelectedItem = name;
                            
                            // Console feedback only - no MessageBox
                            A.Ed.WriteMessage($"\n✅ Đã chọn assembly: {name}");
                        }
                        tr.Commit();
                    }
                }
            }
            catch (System.Exception ex)
            {
                A.Ed.WriteMessage($"\n❌ Lỗi: {ex.Message}");
            }
        }

        private void BtnOK_Click(object? sender, EventArgs e)
      {
            try
       {
    // Validate Corridor
      if (cmbCorridor.SelectedIndex == -1)
     {
       MessageBox.Show("Vui lòng chọn Corridor chính.", 
           "Thiếu thông tin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
   cmbCorridor.Focus();
        return;
 }

        // Validate Target Alignments
   if (cmbTargetAlignment1.SelectedIndex == -1)
     {
            MessageBox.Show("Vui lòng chọn Target Alignment 1.", 
    "Thiếu thông tin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        cmbTargetAlignment1.Focus();
           return;
      }

      if (cmbTargetAlignment2.SelectedIndex == -1)
                {
         MessageBox.Show("Vui lòng chọn Target Alignment 2.", 
    "Thiếu thông tin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
     cmbTargetAlignment2.Focus();
    return;
    }

         // Check if same alignment selected
     if (cmbTargetAlignment1.SelectedItem?.ToString() == 
         cmbTargetAlignment2.SelectedItem?.ToString())
                {
    MessageBox.Show("Target Alignment 1 và 2 không được trùng nhau.", 
      "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
        }

                // Validate Assembly
        if (cmbAssembly.SelectedIndex == -1)
     {
       MessageBox.Show("Vui lòng chọn Assembly.", 
  "Thiếu thông tin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
       cmbAssembly.Focus();
             return;
          }

          // Get selected values
string corridorName = cmbCorridor.SelectedItem?.ToString() ?? "";
   string alignment1Name = cmbTargetAlignment1.SelectedItem?.ToString() ?? "";
     string alignment2Name = cmbTargetAlignment2.SelectedItem?.ToString() ?? "";
 string assemblyName = cmbAssembly.SelectedItem?.ToString() ?? "";

                SelectedCorridorId = _corridorDict.ContainsKey(corridorName) 
       ? _corridorDict[corridorName] : ObjectId.Null;
      TargetAlignment1Id = _alignmentDict.ContainsKey(alignment1Name) 
 ? _alignmentDict[alignment1Name] : ObjectId.Null;
     TargetAlignment2Id = _alignmentDict.ContainsKey(alignment2Name) 
          ? _alignmentDict[alignment2Name] : ObjectId.Null;
            SelectedAssemblyId = _assemblyDict.ContainsKey(assemblyName) 
 ? _assemblyDict[assemblyName] : ObjectId.Null;
        SelectedAssemblyName = assemblyName;
                NumberOfTurnAlignments = (int)numNumberOfAlignments.Value;

      // Validate ObjectIds
       if (SelectedCorridorId == ObjectId.Null || 
        TargetAlignment1Id == ObjectId.Null || 
    TargetAlignment2Id == ObjectId.Null || 
      SelectedAssemblyId == ObjectId.Null)
       {
       MessageBox.Show("Không thể lấy ObjectId của các đối tượng đã chọn.", 
            "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
    return;
      }

 // Save last used values (including ObjectIds)
    SaveLastUsedValues();

        FormAccepted = true;
            DialogResult = DialogResult.OK;
        Close();
         }
            catch (System.Exception ex)
         {
                MessageBox.Show($"❌ Lỗi: {ex.Message}", 
        "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
  }
      }

        private void BtnCancel_Click(object? sender, EventArgs e)
      {
          FormAccepted = false;
            DialogResult = DialogResult.Cancel;
   Close();
        }
    }
}

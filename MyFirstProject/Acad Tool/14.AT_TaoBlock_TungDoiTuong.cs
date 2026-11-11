// (C) Copyright 2024 by T27
// Lệnh tạo block từ các đối tượng text
// - Mỗi block chứa 1 đối tượng text đã chọn
// - Điểm đặt block trùng với điểm đặt text
// - Tùy chọn: Move lên surface và xoay nghiêng so với mặt phẳng XY
//
using System;
using System.Windows.Forms;

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.ApplicationServices;

using Autodesk.Civil.DatabaseServices;
using MyFirstProject.Extensions;

// Alias để tránh xung đột namespace
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using CivSurface = Autodesk.Civil.DatabaseServices.TinSurface;
using AcadEntity = Autodesk.AutoCAD.DatabaseServices.Entity;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsComboBox = System.Windows.Forms.ComboBox;
using WinFormsButton = System.Windows.Forms.Button;
using WinFormsTextBox = System.Windows.Forms.TextBox;
using WinFormsCheckBox = System.Windows.Forms.CheckBox;

[assembly: CommandClass(typeof(Civil3DCsharp.AT_TaoBlock_TungDoiTuong_Commands))]

namespace Civil3DCsharp
{
    /// <summary>
    /// Form chọn các tham số cho lệnh tạo block từ text
    /// </summary>
    public class TaoBlockTungDoiTuongForm : Form
    {
        // Properties trả về kết quả
        public string SelectedSurfaceName { get; private set; } = "";
        public double RotationAngle { get; private set; } = 0;
        public bool MoveToSurface { get; private set; } = false;
        public bool RotateFromXYPlane { get; private set; } = false;
        public string BlockNamePrefix { get; private set; } = "TXT_";

        // Controls
        private WinFormsTextBox txtBlockPrefix = null!;
        private WinFormsComboBox comboBoxSurface = null!;
        private WinFormsTextBox txtRotationAngle = null!;
        private WinFormsCheckBox chkMoveToSurface = null!;
        private WinFormsCheckBox chkRotateFromXYPlane = null!;

        public TaoBlockTungDoiTuongForm()
        {
            InitializeComponent();
            LoadSurfaces();
        }

        private void InitializeComponent()
        {
            // Form settings
            Text = "Tạo Block từ Text";
            ClientSize = new System.Drawing.Size(420, 280);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            int y = 20;
            int labelX = 20;
            int controlX = 20;
            int controlWidth = 380;

            // Tiền tố tên block
            var lblPrefix = new WinFormsLabel
            {
                Text = "Tiền tố tên Block:",
                Location = new System.Drawing.Point(labelX, y),
                AutoSize = true
            };
            Controls.Add(lblPrefix);

            y += 25;
            txtBlockPrefix = new WinFormsTextBox
            {
                Text = "TXT_",
                Location = new System.Drawing.Point(controlX, y),
                Size = new System.Drawing.Size(controlWidth, 26)
            };
            Controls.Add(txtBlockPrefix);

            // Checkbox Move to Surface
            y += 40;
            chkMoveToSurface = new WinFormsCheckBox
            {
                Text = "Move block lên bề mặt Surface",
                Location = new System.Drawing.Point(controlX, y),
                AutoSize = true,
                Checked = false
            };
            chkMoveToSurface.CheckedChanged += (s, e) => comboBoxSurface.Enabled = chkMoveToSurface.Checked;
            Controls.Add(chkMoveToSurface);

            // ComboBox Surface
            y += 30;
            comboBoxSurface = new WinFormsComboBox
            {
                Location = new System.Drawing.Point(controlX, y),
                Size = new System.Drawing.Size(controlWidth, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Enabled = false
            };
            Controls.Add(comboBoxSurface);

            // Checkbox Rotate
            y += 40;
            chkRotateFromXYPlane = new WinFormsCheckBox
            {
                Text = "Xoay block nghiêng so với mặt phẳng XY",
                Location = new System.Drawing.Point(controlX, y),
                AutoSize = true,
                Checked = false
            };
            chkRotateFromXYPlane.CheckedChanged += (s, e) => txtRotationAngle.Enabled = chkRotateFromXYPlane.Checked;
            Controls.Add(chkRotateFromXYPlane);

            // Góc xoay
            y += 30;
            var lblAngle = new WinFormsLabel
            {
                Text = "Góc nghiêng (độ):",
                Location = new System.Drawing.Point(labelX, y + 3),
                AutoSize = true
            };
            Controls.Add(lblAngle);

            txtRotationAngle = new WinFormsTextBox
            {
                Text = "90",
                Location = new System.Drawing.Point(150, y),
                Size = new System.Drawing.Size(80, 26),
                Enabled = false
            };
            Controls.Add(txtRotationAngle);

            // Buttons
            y += 50;
            var btnOK = new WinFormsButton
            {
                Text = "OK",
                Location = new System.Drawing.Point(120, y),
                Size = new System.Drawing.Size(80, 30),
                DialogResult = DialogResult.None
            };
            btnOK.Click += BtnOK_Click;
            Controls.Add(btnOK);

            var btnCancel = new WinFormsButton
            {
                Text = "Hủy",
                Location = new System.Drawing.Point(220, y),
                Size = new System.Drawing.Size(80, 30),
                DialogResult = DialogResult.Cancel
            };
            Controls.Add(btnCancel);

            AcceptButton = btnOK;
            CancelButton = btnCancel;
        }

        private void LoadSurfaces()
        {
            try
            {
                using var tr = A.Db.TransactionManager.StartTransaction();
                var surfaceIds = A.Cdoc.GetSurfaceIds();
                foreach (ObjectId surfaceId in surfaceIds)
                {
                    if (tr.GetObject(surfaceId, OpenMode.ForRead) is CivSurface surface && !surface.IsErased)
                    {
                        comboBoxSurface.Items.Add(surface.Name);
                    }
                }
                tr.Commit();

                if (comboBoxSurface.Items.Count > 0)
                    comboBoxSurface.SelectedIndex = 0;
            }
            catch { }
        }

        private void BtnOK_Click(object? sender, EventArgs e)
        {
            // Validate
            if (string.IsNullOrWhiteSpace(txtBlockPrefix.Text))
            {
                MessageBox.Show("Vui lòng nhập tiền tố tên block!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (chkMoveToSurface.Checked && comboBoxSurface.SelectedItem == null)
            {
                MessageBox.Show("Vui lòng chọn surface!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (chkRotateFromXYPlane.Checked && !double.TryParse(txtRotationAngle.Text, out double angle))
            {
                MessageBox.Show("Góc nghiêng phải là số!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Set results
            BlockNamePrefix = txtBlockPrefix.Text.Trim();
            SelectedSurfaceName = comboBoxSurface.SelectedItem?.ToString() ?? "";
            MoveToSurface = chkMoveToSurface.Checked;
            RotateFromXYPlane = chkRotateFromXYPlane.Checked;
            if (RotateFromXYPlane)
                RotationAngle = double.Parse(txtRotationAngle.Text);

            DialogResult = DialogResult.OK;
            Close();
        }
    }

    /// <summary>
    /// Lệnh tạo block từ text
    /// </summary>
    public class AT_TaoBlock_TungDoiTuong_Commands
    {
        private static int _blockCounter = 1;

        [CommandMethod("AT_Tao_Block_Tung_Doi_Tuong")]
        public static void AT_TaoBlock_TungDoiTuong()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            var db = doc.Database;

            try
            {
                // Hiển thị form
                using var form = new TaoBlockTungDoiTuongForm();
                if (form.ShowDialog() != DialogResult.OK)
                {
                    ed.WriteMessage("\nĐã hủy lệnh.");
                    return;
                }

                // Lấy tham số từ form
                string blockPrefix = form.BlockNamePrefix;
                string surfaceName = form.SelectedSurfaceName;
                double rotationAngle = form.RotationAngle;
                bool moveToSurface = form.MoveToSurface;
                bool rotateFromXY = form.RotateFromXYPlane;

                // Chọn text
                var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "TEXT,MTEXT") });
                var selResult = ed.GetSelection(new PromptSelectionOptions { MessageForAdding = "\nChọn các đối tượng Text/MText: " }, filter);

                if (selResult.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\nĐã hủy chọn.");
                    return;
                }

                int successCount = 0;
                int totalCount = selResult.Value.Count;

                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForWrite);
                    var modelSpace = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    // Lấy surface nếu cần
                    CivSurface? surface = null;
                    if (moveToSurface)
                    {
                        foreach (ObjectId surfaceId in A.Cdoc.GetSurfaceIds())
                        {
                            if (tr.GetObject(surfaceId, OpenMode.ForRead) is CivSurface surf && surf.Name == surfaceName)
                            {
                                surface = surf;
                                break;
                            }
                        }
                    }

                    // Xử lý từng text
                    foreach (SelectedObject selObj in selResult.Value)
                    {
                        try
                        {
                            if (tr.GetObject(selObj.ObjectId, OpenMode.ForWrite) is not AcadEntity textEntity) continue;

                            // Lấy thông tin text
                            Point3d insertionPoint;
                            double textRotation;

                            if (textEntity is DBText dbText)
                            {
                                textRotation = dbText.Rotation;
                                insertionPoint = IsLeftJustified(dbText.Justify) ? dbText.Position : dbText.AlignmentPoint;
                            }
                            else if (textEntity is MText mText)
                            {
                                insertionPoint = mText.Location;
                                textRotation = mText.Rotation;
                            }
                            else continue;

                            // Tạo block definition
                            string blockName = GetUniqueBlockName(bt, blockPrefix);
                            var blockDef = new BlockTableRecord { Name = blockName, Origin = Point3d.Origin };
                            var blockDefId = bt.Add(blockDef);
                            tr.AddNewlyCreatedDBObject(blockDef, true);

                            // Clone text vào block (di chuyển về gốc)
                            var clonedEntity = (AcadEntity)textEntity.Clone();
                            clonedEntity.TransformBy(Matrix3d.Displacement(Point3d.Origin - insertionPoint));
                            blockDef.AppendEntity(clonedEntity);
                            tr.AddNewlyCreatedDBObject(clonedEntity, true);

                            // Xác định vị trí block
                            var blockPosition = insertionPoint;
                            if (surface != null)
                            {
                                try
                                {
                                    double z = surface.FindElevationAtXY(insertionPoint.X, insertionPoint.Y);
                                    blockPosition = new Point3d(insertionPoint.X, insertionPoint.Y, z);
                                }
                                catch { }
                            }

                            // Tạo block reference
                            var blockRef = new BlockReference(blockPosition, blockDefId);

                            // Xoay nghiêng theo hướng text
                            if (rotateFromXY && rotationAngle != 0)
                            {
                                double angleRad = rotationAngle * Math.PI / 180.0;
                                var axis = new Vector3d(Math.Cos(textRotation), Math.Sin(textRotation), 0);
                                blockRef.TransformBy(Matrix3d.Rotation(angleRad, axis, blockPosition));
                            }

                            modelSpace.AppendEntity(blockRef);
                            tr.AddNewlyCreatedDBObject(blockRef, true);

                            // Xóa text gốc
                            textEntity.Erase();
                            successCount++;
                        }
                        catch { }
                    }

                    tr.Commit();
                }

                ed.WriteMessage($"\nHoàn thành: {successCount}/{totalCount} block đã tạo.");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\nLỗi: {ex.Message}");
            }
        }

        private static bool IsLeftJustified(AttachmentPoint justify)
        {
            return justify == AttachmentPoint.BaseLeft ||
                   justify == AttachmentPoint.TopLeft ||
                   justify == AttachmentPoint.MiddleLeft ||
                   justify == AttachmentPoint.BottomLeft;
        }

        private static string GetUniqueBlockName(BlockTable bt, string prefix)
        {
            string name;
            do { name = $"{prefix}{_blockCounter++}"; }
            while (bt.Has(name));
            return name;
        }
    }
}

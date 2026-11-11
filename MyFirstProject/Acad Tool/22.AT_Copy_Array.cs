// (C) Copyright 2024 by T27
// Lệnh Copy Array - Copy đối tượng theo khoảng cách và số lượng
// - Chọn đối tượng cần copy
// - Form nhập khoảng cách, số lượng, chọn phương
// - Nhớ giá trị cho lần chạy sau
//
using System;
using System.Drawing;
using System.Windows.Forms;

using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.ApplicationServices;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using WinFormsLabel = System.Windows.Forms.Label;
using WinFormsFont = System.Drawing.Font;
using WinFormsPoint = System.Drawing.Point;

[assembly: CommandClass(typeof(Civil3DCsharp.AT_Copy_Array_Commands))]

namespace Civil3DCsharp
{
    /// <summary>
    /// Form nhập thông số Copy Array
    /// </summary>
    public class CopyArrayForm : Form
    {
        // Static variables - nhớ giá trị lần cuối
        private static double _lastKhoangCach = 421;
        private static int _lastSoLuong = 1;
        private static int _lastPhuong = 3; // 0=Lên, 1=Xuống, 2=Trái, 3=Phải

        // Properties trả về kết quả
        public double KhoangCach { get; private set; } = 421;
        public int SoLuong { get; private set; } = 1;
        public int Phuong { get; private set; } = 0;
        public bool FormAccepted { get; private set; } = false;

        // UI Controls
        private NumericUpDown numKhoangCach = null!;
        private NumericUpDown numSoLuong = null!;
        private RadioButton rdLen = null!;
        private RadioButton rdXuong = null!;
        private RadioButton rdTrai = null!;
        private RadioButton rdPhai = null!;
        private Button btnOK = null!;
        private Button btnCancel = null!;

        public CopyArrayForm()
        {
            InitializeComponent();
            RestoreLastUsedValues();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Form
            this.Text = "Copy Array";
            this.Size = new Size(350, 290);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // Title
            var lblTitle = new WinFormsLabel
            {
                Text = "COPY ARRAY",
                Font = new WinFormsFont("Microsoft Sans Serif", 11F, FontStyle.Bold),
                Location = new WinFormsPoint(20, 12),
                Size = new Size(300, 25),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.DarkBlue
            };

            // Khoảng cách
            var lblKhoangCach = new WinFormsLabel
            {
                Text = "Khoảng cách:",
                Location = new WinFormsPoint(20, 50),
                Size = new Size(100, 23)
            };

            numKhoangCach = new NumericUpDown
            {
                Location = new WinFormsPoint(130, 48),
                Size = new Size(180, 23),
                DecimalPlaces = 1,
                Minimum = -100000,
                Maximum = 100000,
                Value = 421
            };

            // Số lượng
            var lblSoLuong = new WinFormsLabel
            {
                Text = "Số lượng:",
                Location = new WinFormsPoint(20, 82),
                Size = new Size(100, 23)
            };

            numSoLuong = new NumericUpDown
            {
                Location = new WinFormsPoint(130, 80),
                Size = new Size(180, 23),
                DecimalPlaces = 0,
                Minimum = 1,
                Maximum = 10000,
                Value = 1
            };

            // Phương - GroupBox
            var grpPhuong = new GroupBox
            {
                Text = "Phương copy",
                Location = new WinFormsPoint(20, 115),
                Size = new Size(295, 85)
            };

            rdLen = new RadioButton
            {
                Text = "Lên (↑)",
                Location = new WinFormsPoint(15, 25),
                Size = new Size(120, 23),
                Checked = true
            };

            rdXuong = new RadioButton
            {
                Text = "Xuống (↓)",
                Location = new WinFormsPoint(150, 25),
                Size = new Size(120, 23)
            };

            rdTrai = new RadioButton
            {
                Text = "Trái (←)",
                Location = new WinFormsPoint(15, 52),
                Size = new Size(120, 23)
            };

            rdPhai = new RadioButton
            {
                Text = "Phải (→)",
                Location = new WinFormsPoint(150, 52),
                Size = new Size(120, 23)
            };

            grpPhuong.Controls.AddRange(new Control[] { rdLen, rdXuong, rdTrai, rdPhai });

            // Buttons
            btnOK = new Button
            {
                Text = "OK",
                Location = new WinFormsPoint(130, 210),
                Size = new Size(80, 30),
                Font = new WinFormsFont("Microsoft Sans Serif", 9F, FontStyle.Bold)
            };
            btnOK.Click += BtnOK_Click;

            btnCancel = new Button
            {
                Text = "Hủy",
                Location = new WinFormsPoint(220, 210),
                Size = new Size(80, 30)
            };
            btnCancel.Click += BtnCancel_Click;

            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;

            // Add controls
            this.Controls.AddRange(new Control[] {
                lblTitle, lblKhoangCach, numKhoangCach,
                lblSoLuong, numSoLuong,
                grpPhuong,
                btnOK, btnCancel
            });

            this.ResumeLayout(false);
        }

        private void RestoreLastUsedValues()
        {
            numKhoangCach.Value = (decimal)_lastKhoangCach;
            numSoLuong.Value = _lastSoLuong;

            switch (_lastPhuong)
            {
                case 0: rdLen.Checked = true; break;
                case 1: rdXuong.Checked = true; break;
                case 2: rdTrai.Checked = true; break;
                case 3: rdPhai.Checked = true; break;
            }
        }

        private void SaveLastUsedValues()
        {
            _lastKhoangCach = (double)numKhoangCach.Value;
            _lastSoLuong = (int)numSoLuong.Value;
            _lastPhuong = GetSelectedPhuong();
        }

        private int GetSelectedPhuong()
        {
            if (rdLen.Checked) return 0;
            if (rdXuong.Checked) return 1;
            if (rdTrai.Checked) return 2;
            if (rdPhai.Checked) return 3;
            return 0;
        }

        private void BtnOK_Click(object? sender, EventArgs e)
        {
            KhoangCach = (double)numKhoangCach.Value;
            SoLuong = (int)numSoLuong.Value;
            Phuong = GetSelectedPhuong();

            SaveLastUsedValues();

            FormAccepted = true;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void BtnCancel_Click(object? sender, EventArgs e)
        {
            FormAccepted = false;
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }

    public class AT_Copy_Array_Commands
    {
        [CommandMethod("AT_Copy_Array")]
        public static void AT_Copy_Array()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;

            try
            {
                // Hiển thị form
                CopyArrayForm form = new();
                if (form.ShowDialog() != DialogResult.OK || !form.FormAccepted)
                {
                    ed.WriteMessage("\nĐã hủy lệnh.");
                    return;
                }

                double khoangCach = form.KhoangCach;
                int soLuong = form.SoLuong;
                int phuong = form.Phuong;

                // Xác định vector hướng theo phương đã chọn
                Vector3d direction;
                string tenPhuong;
                switch (phuong)
                {
                    case 0: direction = Vector3d.YAxis; tenPhuong = "Lên"; break;       // Lên = +Y
                    case 1: direction = -Vector3d.YAxis; tenPhuong = "Xuống"; break;    // Xuống = -Y
                    case 2: direction = -Vector3d.XAxis; tenPhuong = "Trái"; break;     // Trái = -X
                    case 3: direction = Vector3d.XAxis; tenPhuong = "Phải"; break;      // Phải = +X
                    default: direction = Vector3d.YAxis; tenPhuong = "Lên"; break;
                }

                // Chọn đối tượng
                PromptSelectionOptions selOpt = new PromptSelectionOptions();
                selOpt.MessageForAdding = "\nChọn các đối tượng cần copy array: ";
                PromptSelectionResult selResult = ed.GetSelection(selOpt);

                if (selResult.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\nĐã hủy chọn đối tượng.");
                    return;
                }

                SelectionSet selSet = selResult.Value;
                int totalObjects = selSet.Count;

                // Thực hiện copy
                int successCount = 0;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(
                        SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);

                    for (int i = 1; i <= soLuong; i++)
                    {
                        Vector3d displacement = direction * khoangCach * i;

                        foreach (SelectedObject selObj in selSet)
                        {
                            if (selObj == null) continue;

                            try
                            {
                                Entity entity = (Entity)tr.GetObject(selObj.ObjectId, OpenMode.ForRead);
                                Entity clonedEntity = (Entity)entity.Clone();
                                clonedEntity.TransformBy(Matrix3d.Displacement(displacement));
                                modelSpace.AppendEntity(clonedEntity);
                                tr.AddNewlyCreatedDBObject(clonedEntity, true);
                                successCount++;
                            }
                            catch (System.Exception ex)
                            {
                                ed.WriteMessage($"\nLỗi khi copy: {ex.Message}");
                            }
                        }
                    }

                    tr.Commit();
                }

                // Thông báo kết quả
                ed.WriteMessage($"\n===== COPY ARRAY HOÀN THÀNH =====");
                ed.WriteMessage($"\nPhương: {tenPhuong} | Khoảng cách: {khoangCach} | Số lượng: {soLuong}");
                ed.WriteMessage($"\nĐã tạo {successCount} đối tượng mới ({soLuong} x {totalObjects}).");
                ed.WriteMessage($"\n=================================");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\nLỗi: {ex.Message}");
            }
        }
    }
}

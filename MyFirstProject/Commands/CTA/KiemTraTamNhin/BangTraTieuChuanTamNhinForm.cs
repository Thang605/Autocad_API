using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using ClosedXML.Excel;
using MyFirstProject.Extensions;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
using CivSurface = Autodesk.Civil.DatabaseServices.TinSurface;

namespace Civil3DCsharp
{
    /// <summary>
    /// Hộp thoại tra cứu và hiển thị toàn bộ bảng tiêu chuẩn thiết kế tầm nhìn
    /// </summary>
    public class BangTraTieuChuanTamNhinForm : System.Windows.Forms.Form
    {
        public SightStandardDefinition? SelectedStandard { get; private set; }
        public SightStandardEntry? SelectedEntry { get; private set; }

        private System.Windows.Forms.DataGridView dgv;
        private System.Windows.Forms.ComboBox cbbStd;

        public BangTraTieuChuanTamNhinForm(string defaultStandardName, int defaultSpeed)
        {
            this.Text = "BẢNG TRA CỨU TIÊU CHUẨN THIẾT KẾ TẦM NHÌN (TCVN & AASHTO)";
            this.Size = new System.Drawing.Size(1050, 480);
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Font = new System.Drawing.Font("Segoe UI", 9.0f, System.Drawing.FontStyle.Regular);
            this.MinimizeBox = false;

            var pnlTop = new System.Windows.Forms.Panel { Dock = System.Windows.Forms.DockStyle.Top, Height = 48, Padding = new System.Windows.Forms.Padding(10, 8, 10, 8) };
            this.Controls.Add(pnlTop);

            var lbl = new System.Windows.Forms.Label { Text = "Chọn tiêu chuẩn để xem:", Location = new System.Drawing.Point(10, 14), AutoSize = true, Font = new System.Drawing.Font("Segoe UI", 9.0f, System.Drawing.FontStyle.Bold) };
            pnlTop.Controls.Add(lbl);

            cbbStd = new System.Windows.Forms.ComboBox { Location = new System.Drawing.Point(170, 11), Width = 260, DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList };
            cbbStd.DataSource = SightStandardRepo.GetAllStandards();
            cbbStd.DisplayMember = "Name";
            cbbStd.SelectedIndexChanged += (s, e) => LoadData();
            pnlTop.Controls.Add(cbbStd);

            var btnApply = new System.Windows.Forms.Button
            {
                Text = "✔ Chọn áp dụng thông số hàng này",
                Location = new System.Drawing.Point(445, 10),
                Width = 240,
                Height = 28,
                BackColor = System.Drawing.Color.FromArgb(28, 64, 112),
                ForeColor = System.Drawing.Color.White,
                Font = new System.Drawing.Font("Segoe UI", 9.0f, System.Drawing.FontStyle.Bold)
            };
            btnApply.Click += (s, e) => ApplySelection();
            pnlTop.Controls.Add(btnApply);

            dgv = new System.Windows.Forms.DataGridView
            {
                Dock = System.Windows.Forms.DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
                BackgroundColor = System.Drawing.Color.White,
                AlternatingRowsDefaultCellStyle = new System.Windows.Forms.DataGridViewCellStyle { BackColor = System.Drawing.Color.FromArgb(248, 250, 252) }
            };
            dgv.DoubleClick += (s, e) => ApplySelection();
            this.Controls.Add(dgv);

            // Chọn tiêu chuẩn mặc định
            for (int i = 0; i < cbbStd.Items.Count; i++)
            {
                if (((SightStandardDefinition)cbbStd.Items[i]).Name == defaultStandardName)
                {
                    cbbStd.SelectedIndex = i;
                    break;
                }
            }

            LoadData();

            // Chọn hàng theo speed
            for (int r = 0; r < dgv.Rows.Count; r++)
            {
                if (dgv.Rows[r].Tag is SightStandardEntry entry && entry.Speed == defaultSpeed)
                {
                    dgv.Rows[r].Selected = true;
                    break;
                }
            }
        }

        private void LoadData()
        {
            if (cbbStd.SelectedItem is not SightStandardDefinition std) return;

            dgv.Columns.Clear();
            dgv.Columns.Add("colSpeed", "V_tk (km/h)");
            dgv.Columns.Add("colStopping", "S dừng min (m)");
            dgv.Columns.Add("colPassing", "S vượt min (m)");
            dgv.Columns.Add("colConvex", "R lồi tt (m)");
            dgv.Columns.Add("colConvexLim", "R lồi gh (m)");
            dgv.Columns.Add("colConcave", "R lõm tt (m)");
            dgv.Columns.Add("colConcaveLim", "R lõm gh (m)");
            dgv.Columns.Add("colRHoriz", "R nằm gh (m)");
            dgv.Columns.Add("colMaxGrade", "i_max (%)");
            dgv.Columns.Add("colLane", "Làn xe (m)");
            dgv.Columns.Add("colOffset", "Offset tâm (m)");
            dgv.Columns.Add("colEye", "Mắt h1 (m)");
            dgv.Columns.Add("colTgt", "Vật h2 (m)");

            foreach (var ent in std.Entries)
            {
                int rIdx = dgv.Rows.Add(
                    ent.Speed,
                    ent.StoppingSightDist,
                    ent.PassingSightDist > 0 ? ent.PassingSightDist.ToString() : "-",
                    ent.MinConvexRadius,
                    ent.MinConvexRadiusLimit,
                    ent.MinConcaveRadius,
                    ent.MinConcaveRadiusLimit,
                    ent.MinHorizontalRadius,
                    $"{ent.MaxGrade:F1}%",
                    $"{ent.LaneWidth:F2}",
                    $"{ent.DefaultLaneOffset:F2}",
                    $"{std.EyeHeight:F2}",
                    $"{std.TargetHeightStopping:F2}"
                );
                dgv.Rows[rIdx].Tag = ent;
            }

            foreach (System.Windows.Forms.DataGridViewColumn col in dgv.Columns)
            {
                col.HeaderCell.Style.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
                col.DefaultCellStyle.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleCenter;
            }
            dgv.AutoResizeColumns();
        }

        private void ApplySelection()
        {
            if (cbbStd.SelectedItem is SightStandardDefinition std && dgv.CurrentRow?.Tag is SightStandardEntry ent)
            {
                SelectedStandard = std;
                SelectedEntry = ent;
                this.DialogResult = System.Windows.Forms.DialogResult.OK;
                this.Close();
            }
        }
    }
}
